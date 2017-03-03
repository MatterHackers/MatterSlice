/*
This file is part of MatterSlice. A command line utility for
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	using Pathfinding;
	using QuadTree;
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	// Fused Filament Fabrication processor.
	public class fffProcessor
	{
		private long maxObjectHeight;
		private int fileNumber;
		private GCodeExport gcode = new GCodeExport();
		private ConfigSettings config;
		private Stopwatch timeKeeper = new Stopwatch();

		private SimpleMeshCollection simpleMeshCollection = new SimpleMeshCollection();
		private OptimizedMeshCollection optomizedMeshCollection;
		private LayerDataStorage slicingData = new LayerDataStorage();

		private GCodePathConfig skirtConfig = new GCodePathConfig("skirtConfig");

		private GCodePathConfig inset0Config = new GCodePathConfig("inset0Config")
		{
			DoSeamHiding = true,
		};

		private GCodePathConfig insetXConfig = new GCodePathConfig("insetXConfig");
		private GCodePathConfig fillConfig = new GCodePathConfig("fillConfig");
		private GCodePathConfig topFillConfig = new GCodePathConfig("topFillConfig");
		private GCodePathConfig bottomFillConfig = new GCodePathConfig("bottomFillConfig");
		private GCodePathConfig airGappedBottomConfig = new GCodePathConfig("airGappedBottomConfig");
		private GCodePathConfig bridgeConfig = new GCodePathConfig("bridgeConfig");
		private GCodePathConfig supportNormalConfig = new GCodePathConfig("supportNormalConfig");
		private GCodePathConfig supportInterfaceConfig = new GCodePathConfig("supportInterfaceConfig");

		public fffProcessor(ConfigSettings config)
		{
			this.config = config;
			fileNumber = 1;
			maxObjectHeight = 0;
		}

		public bool SetTargetFile(string filename)
		{
			gcode.SetFilename(filename);
			{
				gcode.WriteComment("Generated with MatterSlice {0}".FormatWith(ConfigConstants.VERSION));
			}

			return gcode.IsOpened();
		}

		public void DoProcessing()
		{
			if (!gcode.IsOpened()
				|| simpleMeshCollection.SimpleMeshes.Count == 0)
			{
				return;
			}

			timeKeeper.Restart();
			LogOutput.Log("Analyzing and optimizing model...\n");
			optomizedMeshCollection = new OptimizedMeshCollection(simpleMeshCollection);
			if (MatterSlice.Canceled)
			{
				return;
			}

			optomizedMeshCollection.SetPositionAndSize(simpleMeshCollection, config.PositionToPlaceObjectCenter_um.X, config.PositionToPlaceObjectCenter_um.Y, -config.BottomClipAmount_um, config.CenterObjectInXy);
			for (int meshIndex = 0; meshIndex < simpleMeshCollection.SimpleMeshes.Count; meshIndex++)
			{
				LogOutput.Log("  Face counts: {0} . {1} {2:0.0}%\n".FormatWith((int)simpleMeshCollection.SimpleMeshes[meshIndex].faceTriangles.Count, (int)optomizedMeshCollection.OptimizedMeshes[meshIndex].facesTriangle.Count, (double)(optomizedMeshCollection.OptimizedMeshes[meshIndex].facesTriangle.Count) / (double)(simpleMeshCollection.SimpleMeshes[meshIndex].faceTriangles.Count) * 100));
				LogOutput.Log("  Vertex counts: {0} . {1} {2:0.0}%\n".FormatWith((int)simpleMeshCollection.SimpleMeshes[meshIndex].faceTriangles.Count * 3, (int)optomizedMeshCollection.OptimizedMeshes[meshIndex].vertices.Count, (double)(optomizedMeshCollection.OptimizedMeshes[meshIndex].vertices.Count) / (double)(simpleMeshCollection.SimpleMeshes[meshIndex].faceTriangles.Count * 3) * 100));
			}

			LogOutput.Log("Optimize model {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Reset();

			Stopwatch timeKeeperTotal = new Stopwatch();
			timeKeeperTotal.Start();
			preSetup(config.ExtrusionWidth_um);

			SliceModels(slicingData);

			ProcessSliceData(slicingData);
			if (MatterSlice.Canceled)
			{
				return;
			}

			WriteGCode(slicingData);
			if (MatterSlice.Canceled)
			{
				return;
			}

			LogOutput.logProgress("process", 1, 1); //Report to the GUI that a file has been fully processed.
			LogOutput.Log("Total time elapsed {0:0.00}s.\n".FormatWith(timeKeeperTotal.Elapsed.TotalSeconds));
		}

		public bool LoadStlFile(string input_filename)
		{
			preSetup(config.ExtrusionWidth_um);
			timeKeeper.Restart();
			LogOutput.Log("Loading {0} from disk...\n".FormatWith(input_filename));
			if (!SimpleMeshCollection.LoadModelFromFile(simpleMeshCollection, input_filename, config.ModelRotationMatrix))
			{
				LogOutput.LogError("Failed to load model: {0}\n".FormatWith(input_filename));
				return false;
			}
			LogOutput.Log("Loaded from disk in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			return true;
		}

		public void finalize()
		{
			if (!gcode.IsOpened())
			{
				return;
			}

			gcode.Finalize(maxObjectHeight, config.TravelSpeed, config.EndCode);

			gcode.Close();
		}

		private void preSetup(int extrusionWidth)
		{
			skirtConfig.SetData(config.InsidePerimetersSpeed, extrusionWidth, "SKIRT");
			inset0Config.SetData(config.OutsidePerimeterSpeed, config.OutsideExtrusionWidth_um, "WALL-OUTER");
			insetXConfig.SetData(config.InsidePerimetersSpeed, extrusionWidth, "WALL-INNER");

			fillConfig.SetData(config.InfillSpeed, extrusionWidth, "FILL", false);
			topFillConfig.SetData(config.TopInfillSpeed, extrusionWidth, "TOP-FILL", false);
			bottomFillConfig.SetData(config.InfillSpeed, extrusionWidth, "BOTTOM-FILL", false);
			airGappedBottomConfig.SetData(config.FirstLayerSpeed, extrusionWidth, "AIR-GAP", false);
			bridgeConfig.SetData(config.BridgeSpeed, extrusionWidth, "BRIDGE");

			supportNormalConfig.SetData(config.SupportMaterialSpeed, extrusionWidth, "SUPPORT");
			supportInterfaceConfig.SetData(config.SupportMaterialSpeed - 1, extrusionWidth, "SUPPORT-INTERFACE");

			for (int extruderIndex = 0; extruderIndex < ConfigConstants.MAX_EXTRUDERS; extruderIndex++)
			{
				gcode.SetExtruderOffset(extruderIndex, config.ExtruderOffsets[extruderIndex], -config.ZOffset_um);
			}

			gcode.SetRetractionSettings(config.RetractionOnTravel, config.RetractionSpeed, config.RetractionOnExtruderSwitch, config.MinimumExtrusionBeforeRetraction, config.RetractionZHop, config.WipeAfterRetraction, config.UnretractExtraExtrusion, config.UnretractExtraOnExtruderSwitch);
			gcode.SetToolChangeCode(config.ToolChangeCode, config.BeforeToolchangeCode);

			gcode.SetLayerChangeCode(config.LayerChangeCode);
		}

		private void SliceModels(LayerDataStorage slicingData)
		{
			timeKeeper.Restart();
#if false
            optomizedModel.saveDebugSTL("debug_output.stl");
#endif

			LogOutput.Log("Slicing model...\n");
			List<ExtruderData> extruderList = new List<ExtruderData>();
			for (int optimizedMeshIndex = 0; optimizedMeshIndex < optomizedMeshCollection.OptimizedMeshes.Count; optimizedMeshIndex++)
			{
				ExtruderData extruderData = new ExtruderData(optomizedMeshCollection.OptimizedMeshes[optimizedMeshIndex], config);
				extruderList.Add(extruderData);
			}

#if false
            slicerList[0].DumpSegmentsToGcode("Volume 0 Segments.gcode");
            slicerList[0].DumpPolygonsToGcode("Volume 0 Polygons.gcode");
            //slicerList[0].DumpPolygonsToHTML("Volume 0 Polygons.html");
#endif

			LogOutput.Log("Sliced model in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			slicingData.modelSize = optomizedMeshCollection.size_um;
			slicingData.modelMin = optomizedMeshCollection.minXYZ_um;
			slicingData.modelMax = optomizedMeshCollection.maxXYZ_um;

			var extraPathingConsideration = new Polygons();
			foreach (var polygons in slicingData.wipeShield)
			{
				extraPathingConsideration.AddRange(polygons);
			}
			extraPathingConsideration.AddRange(slicingData.wipeTower);

			for (int extruderIndex = 0; extruderIndex < extruderList.Count; extruderIndex++)
			{
				slicingData.Extruders.Add(new ExtruderLayers());
				slicingData.Extruders[extruderIndex].InitializeLayerData(extruderList[extruderIndex], config, extruderIndex, extruderList.Count, extraPathingConsideration);

				if (config.EnableRaft)
				{
					//Add the raft offset to each layer.
					for (int layerIndex = 0; layerIndex < slicingData.Extruders[extruderIndex].Layers.Count; layerIndex++)
					{
						slicingData.Extruders[extruderIndex].Layers[layerIndex].LayerZ += config.RaftBaseThickness_um + config.RaftInterfaceThicknes_um;
					}
				}
			}
			LogOutput.Log("Generated layer parts in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
		}

		private void ProcessSliceData(LayerDataStorage slicingData)
		{
			if (config.ContinuousSpiralOuterPerimeter)
			{
				config.NumberOfTopLayers = 0;
				config.InfillPercent = 0;
			}

			MultiExtruders.ProcessBooleans(slicingData.Extruders, config.BooleanOpperations);

			config.SetExtruderCount(slicingData.Extruders.Count);

			MultiExtruders.RemoveExtruderIntersections(slicingData.Extruders);
			MultiExtruders.OverlapMultipleExtrudersSlightly(slicingData.Extruders, config.MultiExtruderOverlapPercent);
#if False
            LayerPart.dumpLayerparts(slicingData, "output.html");
#endif

			if (config.GenerateSupport && !config.ContinuousSpiralOuterPerimeter)
			{
				LogOutput.Log("Generating support map...\n");
				slicingData.support = new NewSupport(config, slicingData.Extruders, 1);
			}

			slicingData.CreateIslandData();

			int totalLayers = slicingData.Extruders[0].Layers.Count;
			if (config.outputOnlyFirstLayer)
			{
				totalLayers = 1;
			}
#if DEBUG
			for (int extruderIndex = 1; extruderIndex < slicingData.Extruders.Count; extruderIndex++)
			{
				if (totalLayers != slicingData.Extruders[extruderIndex].Layers.Count)
				{
					throw new Exception("All the extruders must have the same number of layers (they just can have empty layers).");
				}
			}
#endif

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				for (int extruderIndex = 0; extruderIndex < slicingData.Extruders.Count; extruderIndex++)
				{
					if (MatterSlice.Canceled)
					{
						return;
					}
					int insetCount = config.NumberOfPerimeters;
					if (config.ContinuousSpiralOuterPerimeter && (int)(layerIndex) < config.NumberOfBottomLayers && layerIndex % 2 == 1)
					{
						//Add extra insets every 2 layers when spiralizing, this makes bottoms of cups watertight.
						insetCount += 1;
					}

					SliceLayer layer = slicingData.Extruders[extruderIndex].Layers[layerIndex];

					if (layerIndex == 0)
					{
						layer.GenerateInsets(config.FirstLayerExtrusionWidth_um, config.FirstLayerExtrusionWidth_um, insetCount, config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter);
					}
					else
					{
						layer.GenerateInsets(config.ExtrusionWidth_um, config.OutsideExtrusionWidth_um, insetCount, config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter);
					}
				}
				LogOutput.Log("Creating Insets {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));
			}

			slicingData.CreateWipeShield(totalLayers, config);

			LogOutput.Log("Generated inset in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				if (MatterSlice.Canceled)
				{
					return;
				}

				//Only generate bottom and top layers and infill for the first X layers when spiralize is chosen.
				if (!config.ContinuousSpiralOuterPerimeter || (int)(layerIndex) < config.NumberOfBottomLayers)
				{
					for (int extruderIndex = 0; extruderIndex < slicingData.Extruders.Count; extruderIndex++)
					{
						if (layerIndex == 0)
						{
							slicingData.Extruders[extruderIndex].GenerateTopAndBottoms(layerIndex, config.FirstLayerExtrusionWidth_um, config.FirstLayerExtrusionWidth_um, config.NumberOfBottomLayers, config.NumberOfTopLayers);
						}
						else
						{
							slicingData.Extruders[extruderIndex].GenerateTopAndBottoms(layerIndex, config.ExtrusionWidth_um, config.OutsideExtrusionWidth_um, config.NumberOfBottomLayers, config.NumberOfTopLayers);
						}
					}
				}
				LogOutput.Log("Creating Top & Bottom Layers {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));
			}
			LogOutput.Log("Generated top bottom layers in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			slicingData.CreateWipeTower(totalLayers, config);

			if (config.EnableRaft)
			{
				slicingData.GenerateRaftOutlines(config.RaftExtraDistanceAroundPart_um, config);

				slicingData.GenerateSkirt(
					config.SkirtDistance_um + config.RaftBaseExtrusionWidth_um,
					config.RaftBaseExtrusionWidth_um,
					config.NumberOfSkirtLoops,
					config.NumberOfBrimLoops,
					config.SkirtMinLength_um,
					config);
			}
			else
			{
				slicingData.GenerateSkirt(
					config.SkirtDistance_um,
					config.FirstLayerExtrusionWidth_um,
					config.NumberOfSkirtLoops,
					config.NumberOfBrimLoops,
					config.SkirtMinLength_um,
					config);
			}
		}

		private void WriteGCode(LayerDataStorage slicingData)
		{
			gcode.WriteComment("filamentDiameter = {0}".FormatWith(config.FilamentDiameter));
			gcode.WriteComment("extrusionWidth = {0}".FormatWith(config.ExtrusionWidth));
			gcode.WriteComment("firstLayerExtrusionWidth = {0}".FormatWith(config.FirstLayerExtrusionWidth));
			gcode.WriteComment("layerThickness = {0}".FormatWith(config.LayerThickness));

			if (config.EnableRaft)
			{
				gcode.WriteComment("firstLayerThickness = {0}".FormatWith(config.RaftBaseThickness_um / 1000.0));
			}
			else
			{
				gcode.WriteComment("firstLayerThickness = {0}".FormatWith(config.FirstLayerThickness));
			}

			if (fileNumber == 1)
			{
				gcode.WriteCode(config.StartCode);
			}
			else
			{
				gcode.WriteFanCommand(0);
				gcode.ResetExtrusionValue();
				gcode.WriteRetraction();
				gcode.SetZ(maxObjectHeight + 5000);
				gcode.WriteMove(gcode.GetPosition(), config.TravelSpeed, 0);
				gcode.WriteMove(new IntPoint(slicingData.modelMin.X, slicingData.modelMin.Y, gcode.CurrentZ), config.TravelSpeed, 0);
			}
			fileNumber++;

			int totalLayers = slicingData.Extruders[0].Layers.Count;
			if (config.outputOnlyFirstLayer)
			{
				totalLayers = 1;
			}

			// let's remove any of the layers on top that are empty
			{
				for (int layerIndex = totalLayers - 1; layerIndex >= 0; layerIndex--)
				{
					bool layerHasData = false;
					foreach (ExtruderLayers currentExtruder in slicingData.Extruders)
					{
						SliceLayer currentLayer = currentExtruder.Layers[layerIndex];
						for (int partIndex = 0; partIndex < currentExtruder.Layers[layerIndex].Islands.Count; partIndex++)
						{
							LayerIsland currentIsland = currentLayer.Islands[partIndex];
							if (currentIsland.IslandOutline.Count > 0)
							{
								layerHasData = true;
								break;
							}
						}
					}

					if (layerHasData)
					{
						break;
					}
					totalLayers--;
				}
			}
			gcode.WriteComment("Layer count: {0}".FormatWith(totalLayers));

			// keep the raft generation code inside of raft
			slicingData.WriteRaftGCodeIfRequired(gcode, config);

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				if (MatterSlice.Canceled)
				{
					return;
				}

				if (config.outputOnlyFirstLayer && layerIndex > 0)
				{
					break;
				}

				LogOutput.Log("Writing Layers {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));

				LogOutput.logProgress("export", layerIndex + 1, totalLayers);

				if (layerIndex == 0)
				{
					skirtConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "SKIRT");
					inset0Config.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "WALL-OUTER");
					insetXConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "WALL-INNER");

					fillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "FILL", false);
					topFillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "TOP-FILL", false);
					bottomFillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "BOTTOM-FILL", false);
					airGappedBottomConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "AIR-GAP", false);
					bridgeConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um, "BRIDGE");

					supportNormalConfig.SetData(config.FirstLayerSpeed, config.SupportExtrusionWidth_um, "SUPPORT");
					supportInterfaceConfig.SetData(config.FirstLayerSpeed, config.ExtrusionWidth_um, "SUPPORT-INTERFACE");
				}
				else
				{
					skirtConfig.SetData(config.InsidePerimetersSpeed, config.ExtrusionWidth_um, "SKIRT");
					inset0Config.SetData(config.OutsidePerimeterSpeed, config.OutsideExtrusionWidth_um, "WALL-OUTER");
					insetXConfig.SetData(config.InsidePerimetersSpeed, config.ExtrusionWidth_um, "WALL-INNER");

					fillConfig.SetData(config.InfillSpeed, config.ExtrusionWidth_um, "FILL", false);
					topFillConfig.SetData(config.TopInfillSpeed, config.ExtrusionWidth_um, "TOP-FILL", false);
					bottomFillConfig.SetData(config.InfillSpeed, config.ExtrusionWidth_um, "BOTTOM-FILL", false);
					airGappedBottomConfig.SetData(config.FirstLayerSpeed, config.ExtrusionWidth_um, "AIR-GAP", false);
					bridgeConfig.SetData(config.BridgeSpeed, config.ExtrusionWidth_um, "BRIDGE");

					supportNormalConfig.SetData(config.SupportMaterialSpeed, config.SupportExtrusionWidth_um, "SUPPORT");
					supportInterfaceConfig.SetData(config.FirstLayerSpeed - 1, config.ExtrusionWidth_um, "SUPPORT-INTERFACE");
				}

				if (layerIndex == 0)
				{
					gcode.SetExtrusion(config.FirstLayerThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);
				}
				else
				{
					gcode.SetExtrusion(config.LayerThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);
				}

				GCodePlanner layerGcodePlanner = new GCodePlanner(gcode, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
				if (layerIndex == 0
					&& config.RetractionZHop > 0)
				{
					layerGcodePlanner.ForceRetract();
				}

				// get the correct height for this layer
				int z = config.FirstLayerThickness_um + layerIndex * config.LayerThickness_um;
				if (config.EnableRaft)
				{
					z += config.RaftBaseThickness_um + config.RaftInterfaceThicknes_um + config.RaftSurfaceLayers * config.RaftSurfaceThickness_um;
					if (layerIndex == 0)
					{
						// We only raise the first layer of the print up by the air gap.
						// To give it:
						//   Less press into the raft
						//   More time to cool
						//   more surface area to air while extruding
						z += config.RaftAirGap_um;
					}
				}

				gcode.SetZ(z);

				gcode.LayerChanged(layerIndex);

				// We only create the skirt if we are on layer 0.
				if (layerIndex == 0 && !config.ShouldGenerateRaft())
				{
					QueueSkirtToGCode(slicingData, layerGcodePlanner, layerIndex);
				}

				int fanSpeedPercent = GetFanSpeed(layerIndex, layerGcodePlanner);

				for (int extruderIndex = 0; extruderIndex < config.MaxExtruderCount(); extruderIndex++)
				{
					int prevExtruder = layerGcodePlanner.GetExtruder();
					bool extruderChanged = layerGcodePlanner.SetExtruder(extruderIndex);

					if (extruderChanged 
						&& slicingData.HaveWipeTower(config)
						&& layerIndex < slicingData.LastLayerWithChange(config))
					{
						slicingData.PrimeOnWipeTower(extruderIndex, layerIndex, layerGcodePlanner, fillConfig, config);
						//Make sure we wipe the old extruder on the wipe tower.
						layerGcodePlanner.QueueTravel(slicingData.wipePoint - config.ExtruderOffsets[prevExtruder] + config.ExtruderOffsets[layerGcodePlanner.GetExtruder()]);
					}

					if (layerIndex == 0)
					{
						QueueExtruderLayerToGCode(slicingData, layerGcodePlanner, extruderIndex, layerIndex, config.FirstLayerExtrusionWidth_um, z);
					}
					else
					{
						QueueExtruderLayerToGCode(slicingData, layerGcodePlanner, extruderIndex, layerIndex, config.ExtrusionWidth_um, z);
					}

					if (config.AvoidCrossingPerimeters
						&& slicingData.Extruders != null
						&& slicingData.Extruders.Count > extruderIndex)
					{
						// set the path planner to avoid islands
						SliceLayer layer = slicingData.Extruders[extruderIndex].Layers[layerIndex];
						layerGcodePlanner.PathFinder = layer.PathFinder;
					}

					if (slicingData.support != null)
					{
						if ((config.SupportExtruder <= 0 && extruderIndex == 0)
							|| config.SupportExtruder == extruderIndex)
						{
							if (slicingData.support.QueueNormalSupportLayer(config, layerGcodePlanner, layerIndex, supportNormalConfig))
							{
								// we move out of the island so we aren't in it.
								islandCurrentlyInside = null;
							}
						}
						if ((config.SupportInterfaceExtruder <= 0 && extruderIndex == 0)
							|| config.SupportInterfaceExtruder == extruderIndex)
						{
							if(slicingData.support.QueueInterfaceSupportLayer(config, layerGcodePlanner, layerIndex, supportInterfaceConfig))
							{
								// we move out of the island so we aren't in it.
								islandCurrentlyInside = null;
							}
						}
					}
				}

				slicingData.EnsureWipeTowerIsSolid(layerIndex, layerGcodePlanner, fillConfig, config);

				if (slicingData.support != null)
				{
					z += config.SupportAirGap_um;
					gcode.SetZ(z);

					for (int extruderIndex = 0; extruderIndex < slicingData.Extruders.Count; extruderIndex++)
					{
						QueueAirGappedExtruderLayerToGCode(slicingData, layerGcodePlanner, extruderIndex, layerIndex, config.ExtrusionWidth_um, z);
					}

					slicingData.support.QueueAirGappedBottomLayer(config, layerGcodePlanner, layerIndex, airGappedBottomConfig);
				}

				//Finish the layer by applying speed corrections for minimum layer times.
				layerGcodePlanner.ForceMinimumLayerTime(config.MinimumLayerTimeSeconds, config.MinimumPrintingSpeed);

				gcode.WriteFanCommand(fanSpeedPercent);

				int currentLayerThickness_um = config.LayerThickness_um;
				if (layerIndex <= 0)
				{
					currentLayerThickness_um = config.FirstLayerThickness_um;
				}

				layerGcodePlanner.WriteQueuedGCode(currentLayerThickness_um, fanSpeedPercent, config.BridgeFanSpeedPercent);
			}

			LogOutput.Log("Wrote layers in {0:0.00}s.\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
			gcode.TellFileSize();
			gcode.WriteFanCommand(0);

			//Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
			maxObjectHeight = Math.Max(maxObjectHeight, slicingData.modelSize.Z);
		}

		void QueuePerimeterWithMergOverlaps(Polygon perimeterToCheckForMerge, int layerIndex, GCodePlanner gcodeLayer, GCodePathConfig config)
		{
			Polygons pathsWithOverlapsRemoved = null;
			bool pathHadOverlaps = false;
			bool pathIsClosed = true;

			if (perimeterToCheckForMerge.Count > 2)
			{
				pathHadOverlaps = perimeterToCheckForMerge.MergePerimeterOverlaps(config.lineWidth_um, out pathsWithOverlapsRemoved, pathIsClosed)
					&& pathsWithOverlapsRemoved.Count > 0;
			}

			if (pathHadOverlaps)
			{
				bool oldClosedLoop = config.closedLoop;
				config.closedLoop = false;
				QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, pathsWithOverlapsRemoved, config, SupportWriteType.UnsupportedAreas);
				config.closedLoop = oldClosedLoop;
			}
			else
			{
				QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, new Polygons() { perimeterToCheckForMerge }, config, SupportWriteType.UnsupportedAreas);
			}
		}

		private int GetFanSpeed(int layerIndex, GCodePlanner gcodeLayer)
		{
			int fanSpeedPercent = config.FanSpeedMinPercent;
			if (gcodeLayer.getExtrudeSpeedFactor() <= 50)
			{
				fanSpeedPercent = config.FanSpeedMaxPercent;
			}
			else
			{
				int n = gcodeLayer.getExtrudeSpeedFactor() - 50;
				fanSpeedPercent = config.FanSpeedMinPercent * n / 50 + config.FanSpeedMaxPercent * (50 - n) / 50;
			}

			if (layerIndex < config.FirstLayerToAllowFan)
			{
				// Don't allow the fan below this layer
				fanSpeedPercent = 0;
			}
			return fanSpeedPercent;
		}

		private void QueueSkirtToGCode(LayerDataStorage slicingData, GCodePlanner gcodeLayer, int layerIndex)
		{
			if (slicingData.skirt.Count > 0
				&& slicingData.skirt[0].Count > 0)
			{
				IntPoint lowestPoint = slicingData.skirt[0][0];

				// lets make sure we start with the most outside loop
				foreach (Polygon polygon in slicingData.skirt)
				{
					foreach (IntPoint position in polygon)
					{
						if (position.Y < lowestPoint.Y)
						{
							lowestPoint = polygon[0];
						}
					}
				}

				gcodeLayer.QueueTravel(lowestPoint);
			}

			gcodeLayer.QueuePolygonsByOptimizer(slicingData.skirt, skirtConfig);
		}

		LayerIsland islandCurrentlyInside = null;

		//Add a single layer from a single extruder to the GCode
		private void QueueExtruderLayerToGCode(LayerDataStorage slicingData, GCodePlanner layerGcodePlanner, int extruderIndex, int layerIndex, int extrusionWidth_um, long currentZ_um)
		{
			if (extruderIndex > slicingData.Extruders.Count - 1)
			{
				return;
			}

			SliceLayer layer = slicingData.Extruders[extruderIndex].Layers[layerIndex];

			if (layer.AllOutlines.Count == 0
				&& config.WipeShieldDistanceFromObject == 0)
			{
				// don't do anything on this layer
				return;
			}

			if (slicingData.wipeShield.Count > 0 && slicingData.Extruders.Count > 1)
			{
				layerGcodePlanner.ForceRetract();
				layerGcodePlanner.QueuePolygonsByOptimizer(slicingData.wipeShield[layerIndex], skirtConfig);
				layerGcodePlanner.ForceRetract();
			}

			PathOrderOptimizer islandOrderOptimizer = new PathOrderOptimizer(new IntPoint());
			for (int partIndex = 0; partIndex < layer.Islands.Count; partIndex++)
			{
				if (config.ContinuousSpiralOuterPerimeter && partIndex > 0)
				{
					continue;
				}

				if (config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter)
				{
					islandOrderOptimizer.AddPolygon(layer.Islands[partIndex].IslandOutline[0]);
				}
				else
				{
					islandOrderOptimizer.AddPolygon(layer.Islands[partIndex].InsetToolPaths[0][0]);
				}
			}
			islandOrderOptimizer.Optimize();

			List<Polygons> bottomFillIslandPolygons = new List<Polygons>();

			for (int islandOrderIndex = 0; islandOrderIndex < islandOrderOptimizer.bestIslandOrderIndex.Count; islandOrderIndex++)
			{
				if (config.ContinuousSpiralOuterPerimeter && islandOrderIndex > 0)
				{
					continue;
				}

				LayerIsland island = layer.Islands[islandOrderOptimizer.bestIslandOrderIndex[islandOrderIndex]];

				if (config.AvoidCrossingPerimeters)
				{
					MoveToIsland(layerGcodePlanner, layer, island);
				}
				else
				{
					if(config.RetractWhenChangingIslands)  layerGcodePlanner.ForceRetract();
				}

				Polygons fillPolygons = new Polygons();
				Polygons topFillPolygons = new Polygons();
				Polygons bridgePolygons = new Polygons();

				Polygons bottomFillPolygons = new Polygons();

				CalculateInfillData(slicingData, extruderIndex, layerIndex, island, bottomFillPolygons, fillPolygons, topFillPolygons, bridgePolygons);
				bottomFillIslandPolygons.Add(bottomFillPolygons);

				if (config.NumberOfPerimeters > 0)
				{
					if (config.ContinuousSpiralOuterPerimeter
						&& layerIndex >= config.NumberOfBottomLayers)
					{
						inset0Config.spiralize = true;
					}

					// Put all the insets into a new list so we can keep track of what has been printed.
					// The island could be a rectangle with 4 screew holes. So, with 3 perimeters that colud be the outside 3 + the foles 4 * 3, 15 polygons.
					List<Polygons> insetsForThisIsland = new List<Polygons>(island.InsetToolPaths.Count);
					for (int insetIndex = 0; insetIndex < island.InsetToolPaths.Count; insetIndex++)
					{
						insetsForThisIsland.Add(new Polygons());
						for (int polygonIndex = 0; polygonIndex < island.InsetToolPaths[insetIndex].Count; polygonIndex++)
						{
							if (island.InsetToolPaths[insetIndex][polygonIndex].Count > 0)
							{
								insetsForThisIsland[insetIndex].Add(island.InsetToolPaths[insetIndex][polygonIndex]);
							}
						}
					}

					// If we are on the very first layer we always start with the outside so that we can stick to the bed better.
					if (config.OutsidePerimetersFirst || layerIndex == 0 || inset0Config.spiralize)
					{
						if (inset0Config.spiralize)
						{
							if (island.InsetToolPaths.Count > 0)
							{
								Polygon outsideSinglePolygon = island.InsetToolPaths[0][0];
								layerGcodePlanner.QueuePolygonsByOptimizer(new Polygons() { outsideSinglePolygon }, inset0Config);
							}
						}
						else
						{
							int insetCount = CountInsetsToPrint(insetsForThisIsland);
							while (insetCount > 0)
							{
								bool limitDistance = false;
								if (island.InsetToolPaths.Count > 0)
								{
									QueueClosetsInset(insetsForThisIsland[0], limitDistance, inset0Config, layerIndex, layerGcodePlanner);
								}

								// Move to the closest inset 1 and print it
								for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count; insetIndex++)
								{
									limitDistance = QueueClosetsInset(insetsForThisIsland[insetIndex], limitDistance, insetXConfig, layerIndex, layerGcodePlanner);
								}

								insetCount = CountInsetsToPrint(insetsForThisIsland);
							}
						}
					}
					else // This is so we can do overhangs better (the outside can stick a bit to the inside).
					{
						int insetCount = CountInsetsToPrint(insetsForThisIsland);
						if(insetCount == 0 
							&& config.ExpandThinWalls
							&& island.IslandOutline.Count > 0
							&& island.IslandOutline[0].Count > 0)
						{
							// There are no insets but we should still try to go to the start position of the first perimeter if we are expanding thin walls
							layerGcodePlanner.QueueTravel(island.IslandOutline[0][0]);
						}
						while (insetCount > 0)
						{
							bool limitDistance = false;
							if (island.InsetToolPaths.Count > 0)
							{
								// Print the insets from inside to out (count - 1 to 0).
								for (int insetIndex = island.InsetToolPaths.Count - 1; insetIndex >= 0; insetIndex--)
								{
									if (!config.ContinuousSpiralOuterPerimeter
										&& insetIndex == island.InsetToolPaths.Count - 1)
									{
										var closestInsetStart = FindBestPoint(insetsForThisIsland[0], layerGcodePlanner.LastPosition);
										if(closestInsetStart.X != long.MinValue)
										{
											layerGcodePlanner.QueueTravel(closestInsetStart);
										}
									}

									limitDistance = QueueClosetsInset(
										insetsForThisIsland[insetIndex],
										limitDistance,
										insetIndex == 0 ? inset0Config : insetXConfig,
										layerIndex,
										layerGcodePlanner);

									// Figure out if there is another inset
								}
							}

							insetCount = CountInsetsToPrint(insetsForThisIsland);
						}
					}

					// Find the thin lines for this layer and add them to the queue
					if (config.FillThinGaps && !config.ContinuousSpiralOuterPerimeter)
					{
						for (int perimeter = 0; perimeter < config.NumberOfPerimeters; perimeter++)
						{
							Polygons thinLines = null;
							if (island.IslandOutline.Offset(-extrusionWidth_um * (1 + perimeter)).FindThinLines(extrusionWidth_um + 2, extrusionWidth_um / 5, out thinLines, true))
							{
								fillPolygons.AddRange(thinLines);
							}
						}
					}

					if (config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter)
					{
						Polygons thinLines = null;
						// Collect all of the lines up to one third the extrusion diameter
						//string perimeterString = Newtonsoft.Json.JsonConvert.SerializeObject(island.IslandOutline);
						if (island.IslandOutline.FindThinLines(extrusionWidth_um + 2, extrusionWidth_um / 3, out thinLines, true))
						{
							for(int polyIndex=thinLines.Count-1; polyIndex>=0; polyIndex--)
							{
								var polygon = thinLines[polyIndex];

								if (polygon.Count == 2
									&& (polygon[0] - polygon[1]).Length() < config.ExtrusionWidth_um / 4)
								{
									thinLines.RemoveAt(polyIndex);
								}
								else
								{
									for (int i = 0; i < polygon.Count; i++)
									{
										if (polygon[i].Width > 0)
										{
											polygon[i] = new IntPoint(polygon[i])
											{
												Width = extrusionWidth_um,
											};
										}
									}
								}
							}

							fillPolygons.AddRange(thinLines);
						}
					}
				}

				// Write the bridge polygons after the perimeter so they will have more to hold to while bridging the gaps.
				// It would be even better to slow down the perimeters that are part of bridges but that is for later.
				if (bridgePolygons.Count > 0)
				{
					QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, bridgePolygons, bridgeConfig, SupportWriteType.UnsupportedAreas);
				}

				// TODO: Put all of these segments into a list that can be queued together and still preserver their individual config settings.
				// This will make the total amount of travel while printing infill much less.
				layerGcodePlanner.QueuePolygonsByOptimizer(fillPolygons, fillConfig);
				QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, bottomFillPolygons, bottomFillConfig, SupportWriteType.UnsupportedAreas);
				layerGcodePlanner.QueuePolygonsByOptimizer(topFillPolygons, topFillConfig);
			}
		}

		private void MoveToIsland(GCodePlanner layerGcodePlanner, SliceLayer layer, LayerIsland island)
		{
			if(config.ContinuousSpiralOuterPerimeter)
			{
				return;
			}

			if (island.IslandOutline.Count > 0)
			{
				// If we are already in the island we are going to, don't go there.
				if (island.PathFinder.OutlinePolygons.PointIsInside(layerGcodePlanner.LastPosition, island.PathFinder.OutlineEdgeQuadTrees))
				{
					islandCurrentlyInside = island;
					layerGcodePlanner.PathFinder = island.PathFinder;
					return;
				}

				var closestPointOnNextIsland = island.IslandOutline.FindClosestPoint(layerGcodePlanner.LastPosition);
				IntPoint closestNextIslandPoint = island.IslandOutline[closestPointOnNextIsland.Item1][closestPointOnNextIsland.Item2];

				if (islandCurrentlyInside?.PathFinder?.OutlinePolygons.Count > 0
					&& islandCurrentlyInside?.PathFinder?.OutlinePolygons?[0]?.Count > 3)
				{
					// start by moving within the last island to the closet point to the next island
					var polygons = islandCurrentlyInside.PathFinder.OutlinePolygons;
					if (polygons.Count > 0)
					{
						var closestPointOnLastIsland = polygons.FindClosestPoint(closestNextIslandPoint);

						IntPoint closestLastIslandPoint = polygons[closestPointOnLastIsland.Item1][closestPointOnLastIsland.Item2];
						// make sure we are planning within the last island we were using
						layerGcodePlanner.PathFinder = islandCurrentlyInside.PathFinder;
						layerGcodePlanner.QueueTravel(closestLastIslandPoint);
					}
				}

				// let's move to this island avoiding running into any other islands
				layerGcodePlanner.PathFinder = layer.PathFinder;
				// find the closest point to where we are now
				// and do a move to there
				if (config.RetractWhenChangingIslands) layerGcodePlanner.ForceRetract();
				layerGcodePlanner.QueueTravel(closestNextIslandPoint);
				// and remember that we are now in the new island
				islandCurrentlyInside = island;
				// set the path planner to the current island
				layerGcodePlanner.PathFinder = island.PathFinder;
			}
		}

		public IntPoint FindBestPoint(Polygons boundaryPolygons, IntPoint position)
		{
			IntPoint polyPointPosition = new IntPoint(long.MinValue, long.MinValue);

			long bestDist = long.MaxValue;
			for (int polygonIndex = 0; polygonIndex < boundaryPolygons.Count; polygonIndex++)
			{
				IntPoint closestToPoly = boundaryPolygons[polygonIndex].FindGreatestTurnPosition(config.ExtrusionWidth_um);
				if (closestToPoly != null)
				{
					long length = (closestToPoly - position).Length();
					if (length < bestDist)
					{
						bestDist = length;
						polyPointPosition = closestToPoly;
					}
				}
			}

			return polyPointPosition;
		}

		private bool QueueClosetsInset(Polygons insetsToConsider, bool limitDistance, GCodePathConfig pathConfig, int layerIndex, GCodePlanner gcodeLayer)
		{
			// This is the furthest away we will accept a new starting point
			long maxDist_um = long.MaxValue;
			if (limitDistance)
			{
				// Make it relative to the size of the nozzle
				maxDist_um = config.ExtrusionWidth_um * 4;
			}

			int polygonPrintedIndex = -1;

			for (int polygonIndex = 0; polygonIndex < insetsToConsider.Count; polygonIndex++)
			{
				Polygon currentPolygon = insetsToConsider[polygonIndex];
				int bestPoint = currentPolygon.FindClosestPositionIndex(gcodeLayer.LastPosition);
				if (bestPoint > -1)
				{
					long distance = (currentPolygon[bestPoint] - gcodeLayer.LastPosition).Length();
					if (distance < maxDist_um)
					{
						maxDist_um = distance;
						polygonPrintedIndex = polygonIndex;
					}
				}
			}

			if (polygonPrintedIndex > -1)
			{
				if (config.MergeOverlappingLines)
				{
					QueuePerimeterWithMergOverlaps(insetsToConsider[polygonPrintedIndex], layerIndex, gcodeLayer, pathConfig);
				}
				else
				{
					QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, new Polygons() { insetsToConsider[polygonPrintedIndex] }, pathConfig, SupportWriteType.UnsupportedAreas);
				}
				insetsToConsider.RemoveAt(polygonPrintedIndex);
				return true;
			}

			// Return the original limitDistance value if we didn't match a polygon
			return limitDistance;
		}

		private int CountInsetsToPrint(List<Polygons> insetsToPrint)
		{
			int total = 0;
			foreach (Polygons polygons in insetsToPrint)
			{
				total += polygons.Count;
			}

			return total;
		}

		//Add a single layer from a single extruder to the GCode
		private void QueueAirGappedExtruderLayerToGCode(LayerDataStorage slicingData, GCodePlanner layerGcodePlanner, int extruderIndex, int layerIndex, int extrusionWidth_um, long currentZ_um)
		{
			if (config.GenerateSupport
				&& !config.ContinuousSpiralOuterPerimeter
				&& layerIndex > 0)
			{
				int prevExtruder = layerGcodePlanner.GetExtruder();
				bool extruderChanged = layerGcodePlanner.SetExtruder(extruderIndex);

				if (extruderChanged
					&& slicingData.HaveWipeTower(config)
					&& layerIndex < slicingData.LastLayerWithChange(config))
				{
					slicingData.PrimeOnWipeTower(extruderIndex, layerIndex, layerGcodePlanner, fillConfig, config);
					//Make sure we wipe the old extruder on the wipe tower.
					layerGcodePlanner.QueueTravel(slicingData.wipePoint - config.ExtruderOffsets[prevExtruder] + config.ExtruderOffsets[layerGcodePlanner.GetExtruder()]);
				}

				SliceLayer layer = slicingData.Extruders[extruderIndex].Layers[layerIndex];

				PathOrderOptimizer islandOrderOptimizer = new PathOrderOptimizer(new IntPoint());
				for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
				{
					if (config.ContinuousSpiralOuterPerimeter && islandIndex > 0)
					{
						continue;
					}

					if (layer.Islands[islandIndex].InsetToolPaths.Count > 0)
					{
						islandOrderOptimizer.AddPolygon(layer.Islands[islandIndex].InsetToolPaths[0][0]);
					}
				}
				islandOrderOptimizer.Optimize();

				for (int islandOrderIndex = 0; islandOrderIndex < islandOrderOptimizer.bestIslandOrderIndex.Count; islandOrderIndex++)
				{
					if (config.ContinuousSpiralOuterPerimeter && islandOrderIndex > 0)
					{
						continue;
					}

					LayerIsland island = layer.Islands[islandOrderOptimizer.bestIslandOrderIndex[islandOrderIndex]];

					Polygons bottomFillPolygons = new Polygons();
					CalculateInfillData(slicingData, extruderIndex, layerIndex, island, bottomFillPolygons);
					// TODO: check if we are going to output anything
					bool outputDataForIsland = true;

					if (outputDataForIsland)
					{
						if (config.AvoidCrossingPerimeters)
						{
							MoveToIsland(layerGcodePlanner, layer, island);
						}
						else
						{
							if (config.RetractWhenChangingIslands) layerGcodePlanner.ForceRetract();
						}
						
						MoveToIsland(layerGcodePlanner, layer, island);

						if (config.NumberOfPerimeters > 0
							&& config.ContinuousSpiralOuterPerimeter
							&& layerIndex >= config.NumberOfBottomLayers)
						{
							inset0Config.spiralize = true;
						}

						// Print everything but the first perimeter from the outside in so the little parts have more to stick to.
						for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count; insetIndex++)
						{
							QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, island.InsetToolPaths[insetIndex], airGappedBottomConfig, SupportWriteType.SupportedAreas);
						}
						// then 0
						if (island.InsetToolPaths.Count > 0)
						{
							QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, island.InsetToolPaths[0], airGappedBottomConfig, SupportWriteType.SupportedAreas);
						}

						QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, bottomFillPolygons, airGappedBottomConfig, SupportWriteType.SupportedAreas);
					}
				}
			}

			// gcodeLayer.SetPathFinder(null);
		}

		private enum SupportWriteType
		{ UnsupportedAreas, SupportedAreas };

		private void QueuePolygonsConsideringSupport(int layerIndex, GCodePlanner gcodeLayer, Polygons polygonsToWrite, GCodePathConfig fillConfig, SupportWriteType supportWriteType)
		{
			bool oldLoopValue = fillConfig.closedLoop;

			if (config.GenerateSupport
				&& layerIndex > 0
				&& !config.ContinuousSpiralOuterPerimeter)
			{
				Polygons supportOutlines = slicingData.support.GetRequiredSupportAreas(layerIndex).Offset(fillConfig.lineWidth_um / 2);

				if (supportWriteType == SupportWriteType.UnsupportedAreas)
				{
					if (supportOutlines.Count > 0)
					{
						// write segments at normal height (don't write segments needing air gap)
						Polygons polysToWriteAtNormalHeight = new Polygons();
						Polygons polysToWriteAtAirGapHeight = new Polygons();

						GetSegmentsConsideringSupport(polygonsToWrite, supportOutlines, polysToWriteAtNormalHeight, polysToWriteAtAirGapHeight, false, fillConfig.closedLoop);
						fillConfig.closedLoop = false;
						gcodeLayer.QueuePolygonsByOptimizer(polysToWriteAtNormalHeight, fillConfig);
					}
					else
					{
						gcodeLayer.QueuePolygonsByOptimizer(polygonsToWrite, fillConfig);
					}
				}
				else if (supportOutlines.Count > 0) // we are checking the supported areas
				{
					// detect and write segments at air gap height
					Polygons polysToWriteAtNormalHeight = new Polygons();
					Polygons polysToWriteAtAirGapHeight = new Polygons();

					GetSegmentsConsideringSupport(polygonsToWrite, supportOutlines, polysToWriteAtNormalHeight, polysToWriteAtAirGapHeight, true, fillConfig.closedLoop);
					fillConfig.closedLoop = false;
					gcodeLayer.QueuePolygonsByOptimizer(polysToWriteAtAirGapHeight, fillConfig);
				}
			}
			else if (supportWriteType == SupportWriteType.UnsupportedAreas)
			{
				gcodeLayer.QueuePolygonsByOptimizer(polygonsToWrite, fillConfig);
			}

			fillConfig.closedLoop = oldLoopValue;
		}

		private void GetSegmentsConsideringSupport(Polygons polygonsToWrite, Polygons supportOutlines, Polygons polysToWriteAtNormalHeight, Polygons polysToWriteAtAirGapHeight, bool forAirGap, bool closedLoop)
		{
			// make an expanded area to constrain our segments to
			Polygons maxSupportOutlines = supportOutlines.Offset(fillConfig.lineWidth_um * 2 + config.SupportXYDistance_um);

			Polygons polygonsToWriteAsLines = PolygonsHelper.ConvertToLines(polygonsToWrite, closedLoop);

			foreach (Polygon poly in polygonsToWriteAsLines)
			{
				Polygons polygonsIntersectSupport = supportOutlines.CreateLineIntersections(new Polygons() { poly });
				// write the bottoms that are not sitting on supported areas
				if (polygonsIntersectSupport.Count == 0)
				{
					if (!forAirGap)
					{
						polysToWriteAtNormalHeight.Add(poly);
					}
				}
				else
				{
					// clip the lines to the bigger support maxSupportOutlines
					if (forAirGap)
					{
						polysToWriteAtAirGapHeight.AddRange(maxSupportOutlines.CreateLineIntersections(new Polygons() { poly }));
					}
					else
					{
						polysToWriteAtNormalHeight.AddRange(maxSupportOutlines.CreateLineDifference(new Polygons() { poly }));
					}
				}
			}
		}

		private void CalculateInfillData(LayerDataStorage slicingData, int extruderIndex, int layerIndex, LayerIsland part, Polygons bottomFillLines, Polygons fillPolygons = null, Polygons topFillPolygons = null, Polygons bridgePolygons = null)
		{
			// generate infill for the bottom layer including bridging
			foreach (Polygons bottomFillIsland in part.SolidBottomToolPaths.ProcessIntoSeparatIslands())
			{
				if (layerIndex > 0)
				{
					if (config.GenerateSupport)
					{
						if (config.SupportInterfaceLayers > 0)
						{
							Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle);
						}
						else
						{
							Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle + 90);
						}
					}
					else
					{
						SliceLayer previousLayer = slicingData.Extruders[extruderIndex].Layers[layerIndex - 1];
						previousLayer.GenerateFillConsideringBridging(bottomFillIsland, bottomFillLines, config, bridgePolygons);
					}
				}
				else
				{
					Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.FirstLayerExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle);
				}
			}

			// generate infill for the top layer
			if (topFillPolygons != null)
			{
				foreach (Polygons outline in part.SolidTopToolPaths.ProcessIntoSeparatIslands())
				{
					Infill.GenerateLinePaths(outline, topFillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle);
				}
			}

			// generate infill intermediate layers
			if (fillPolygons != null)
			{
				foreach (Polygons outline in part.SolidInfillToolPaths.ProcessIntoSeparatIslands())
				{
					if (true) // use the old infill method
					{
						Infill.GenerateLinePaths(outline, fillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle + 90 * (layerIndex % 2));
					}
					else // use the new concentric infill (not tested enough yet) have to handle some bad cases better
					{
						double oldInfillPercent = config.InfillPercent;
						config.InfillPercent = 100;
						Infill.GenerateConcentricInfill(config, outline, fillPolygons);
						config.InfillPercent = oldInfillPercent;
					}
				}

				double fillAngle = config.InfillStartingAngle;

				// generate the sparse infill for this part on this layer
				if (config.InfillPercent > 0)
				{
					switch (config.InfillType)
					{
						case ConfigConstants.INFILL_TYPE.LINES:
							if ((layerIndex & 1) == 1)
							{
								fillAngle += 90;
							}
							Infill.GenerateLineInfill(config, part.InfillToolPaths, fillPolygons, fillAngle);
							break;

						case ConfigConstants.INFILL_TYPE.GRID:
							Infill.GenerateGridInfill(config, part.InfillToolPaths, fillPolygons, fillAngle);
							break;

						case ConfigConstants.INFILL_TYPE.TRIANGLES:
							Infill.GenerateTriangleInfill(config, part.InfillToolPaths, fillPolygons, fillAngle);
							break;

						case ConfigConstants.INFILL_TYPE.HEXAGON:
							Infill.GenerateHexagonInfill(config, part.InfillToolPaths, fillPolygons, fillAngle, layerIndex);
							break;

						case ConfigConstants.INFILL_TYPE.CONCENTRIC:
							Infill.GenerateConcentricInfill(config, part.InfillToolPaths, fillPolygons);
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
		}

		public void Cancel()
		{
			if (gcode.IsOpened())
			{
				gcode.Close();
			}
		}
	}
}