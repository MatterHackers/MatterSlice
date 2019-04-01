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
	using System.Linq;
	using Pathfinding;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public static class ExtensionMethods
	{
		public static IEnumerable<int> CurrentThenOtherIndexes(this List<ExtruderLayers> extruderLayers, int activeExtruderIndex)
		{
			// Extruders in normal order
			var extruders = Enumerable.Range(0, extruderLayers.Count).ToList();

			// Ensure we stay on the active extruder
			// - When activeIndex is not E0, change extruder ordering to start with the current, then iterate the rest in normal order
			if (activeExtruderIndex != 0)
			{
				extruders.Remove(activeExtruderIndex);
				extruders.Insert(0, activeExtruderIndex);
			}

			return extruders;
		}
	}

	// Fused Filament Fabrication processor.
	public class fffProcessor
	{
		private long maxObjectHeight;
		private int fileNumber;
		private GCodeExport gcodeExport;
		private ConfigSettings config;
		private Stopwatch timeKeeper = new Stopwatch();

		private SimpleMeshCollection simpleMeshCollection = new SimpleMeshCollection();
		private OptimizedMeshCollection optimizedMeshCollection;
		private LayerDataStorage slicingData = new LayerDataStorage();

		private GCodePathConfig skirtConfig;
		private GCodePathConfig inset0Config;
		private GCodePathConfig insetXConfig;
		private GCodePathConfig fillConfig;
		private GCodePathConfig topFillConfig;
		private GCodePathConfig firstTopFillConfig;
		private GCodePathConfig bottomFillConfig;
		private GCodePathConfig airGappedBottomInsetConfig;
		private GCodePathConfig airGappedBottomConfig;
		private GCodePathConfig bridgeConfig;
		private GCodePathConfig supportNormalConfig;
		private GCodePathConfig supportInterfaceConfig;

		public fffProcessor(ConfigSettings config)
		{
			// make sure we are not canceled when starting a new Processor
			MatterSlice.Canceled = false;

			this.config = config;
			fileNumber = 1;
			maxObjectHeight = 0;
			gcodeExport = new GCodeExport(config);
		}

		public bool SetTargetFile(string filename)
		{
			gcodeExport.SetFilename(filename);
			{
				gcodeExport.WriteComment("Generated with MatterSlice {0}".FormatWith(ConfigConstants.VERSION));
			}

			return gcodeExport.IsOpened();
		}

		public void DoProcessing()
		{
			if (!gcodeExport.IsOpened()
				|| simpleMeshCollection.SimpleMeshes.Count == 0)
			{
				return;
			}

			timeKeeper.Restart();
			optimizedMeshCollection = new OptimizedMeshCollection(simpleMeshCollection);
			if (MatterSlice.Canceled)
			{
				return;
			}

			optimizedMeshCollection.SetSize(simpleMeshCollection);
			LogOutput.Log("Optimized model in {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Reset();

			var timeKeeperTotal = Stopwatch.StartNew();

			gcodeExport.SetLayerChangeCode(config.LayerChangeCode);

			SliceModels(slicingData);

			ProcessSliceData(slicingData);

			var extraPathingConsideration = new Polygons();
			foreach (var polygons in slicingData.WipeShield)
			{
				extraPathingConsideration.AddRange(polygons);
			}
			extraPathingConsideration.AddRange(slicingData.WipeTower);
			ExtruderLayers.InitializeLayerPathing(config, extraPathingConsideration, slicingData.Extruders);

			if (MatterSlice.Canceled)
			{
				return;
			}

			// Stuff island data in for NewSupport and used by path optimizer in WriteGCode below
			if (supportOutlines != null)
			{
				foreach (var extruder in slicingData.Extruders)
				{
					for (var i = 0; i < extruder.Layers.Count; i++)
					{
						var layer = extruder.Layers[i];
						layer.Islands.Add(supportIslands[i]);
					}
				}
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
			timeKeeper.Restart();
			if (!SimpleMeshCollection.LoadModelFromFile(simpleMeshCollection, input_filename, config.ModelMatrix))
			{
				if (!input_filename.Contains("assets"))
				{
					LogOutput.LogError("Failed to load model: {0}\n".FormatWith(input_filename));
				}
				return false;
			}
			return true;
		}

		public void finalize()
		{
			if (!gcodeExport.IsOpened())
			{
				return;
			}

			gcodeExport.Finalize(maxObjectHeight, config.TravelSpeed, config.EndCode);

			gcodeExport.Close();
		}

		private void SliceModels(LayerDataStorage slicingData)
		{
			timeKeeper.Restart();
#if false
            optimizedModel.saveDebugSTL("debug_output.stl");
#endif

			var extruderList = new List<ExtruderData>();

			foreach (var optimizedMesh in optimizedMeshCollection.OptimizedMeshes)
			{
				var extruderData = new ExtruderData(optimizedMesh, config);
				extruderList.Add(extruderData);
				extruderData.ReleaseMemory();
			}

#if false
            slicerList[0].DumpSegmentsToGcode("Volume 0 Segments.gcode");
            slicerList[0].DumpPolygonsToGcode("Volume 0 Polygons.gcode");
            //slicerList[0].DumpPolygonsToHTML("Volume 0 Polygons.html");
#endif

			LogOutput.Log("Sliced model in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			slicingData.modelSize = optimizedMeshCollection.size_um;
			slicingData.modelMin = optimizedMeshCollection.minXYZ_um;
			slicingData.modelMax = optimizedMeshCollection.maxXYZ_um;

			foreach (var extruderData in extruderList)
			{
				var extruderLayer = new ExtruderLayers(extruderData, config.outputOnlyFirstLayer);
				slicingData.Extruders.Add(extruderLayer);

				if (config.EnableRaft)
				{
					// Add the raft offset to each layer.
					foreach (var sliceLayer in extruderLayer.Layers)
					{
						sliceLayer.LayerZ += config.RaftBaseThickness_um + config.RaftInterfaceThicknes_um;
					}
				}
			}

			// make the path finding data include all the layer info

			LogOutput.Log("Generated layer parts in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
		}

		private ExtruderLayers supportOutlines = null;
		private List<LayerIsland> supportIslands = null;

		private void ProcessSliceData(LayerDataStorage slicingData)
		{
			if (config.ContinuousSpiralOuterPerimeter)
			{
				config.NumberOfTopLayers = 0;
				config.InfillPercent = 0;
			}

			slicingData.Extruders = MultiExtruders.ProcessBooleans(slicingData.Extruders, config.BooleanOperations);

			MultiExtruders.RemoveExtruderIntersections(slicingData.Extruders);

			// Is the last extruder data actually wipe tower definitions?
			bool userGeneratedWipeTower = config.BooleanOperations.Contains("W");
			ExtruderLayers wipeTowerOutlines = null;
			if (userGeneratedWipeTower)
			{
				wipeTowerOutlines = slicingData.Extruders[slicingData.Extruders.Count - 1];
				// Last extruder was support material, remove it from the list.
				slicingData.Extruders.RemoveAt(slicingData.Extruders.Count - 1);
			}

			// Is the last extruder data actually support definitions?
			bool userGeneratedSupport = config.BooleanOperations.Contains("S");
			ExtruderLayers supportOutlines = null;
			if (userGeneratedSupport)
			{
				supportOutlines = slicingData.Extruders[slicingData.Extruders.Count - 1];
				// Last extruder was support material, remove it from the list.
				slicingData.Extruders.RemoveAt(slicingData.Extruders.Count - 1);
			}

			MultiExtruders.OverlapMultipleExtrudersSlightly(slicingData.Extruders, config.MultiExtruderOverlapPercent);
#if False
			LayerPart.dumpLayerparts(slicingData, "output.html");
#endif

			if (supportOutlines != null
				&& !config.ContinuousSpiralOuterPerimeter)
			{
				timeKeeper.Restart();
				slicingData.Support = new NewSupport(config, slicingData.Extruders, supportOutlines, 0);
				LogOutput.Log("Generating supports in {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			}

			slicingData.CreateIslandData();

			supportIslands = new List<LayerIsland>();

			if (supportOutlines != null)
			{
				foreach (var region in supportOutlines.Layers)
				{
					foreach (Polygons island in region.AllOutlines.ProcessIntoSeparateIslands())
					{
						var layerIsland = new LayerIsland()
						{
							IslandOutline = island,
							PathFinder = new PathFinder(island, config.ExtrusionWidth_um * 3 / 2, useInsideCache: config.AvoidCrossingPerimeters),
						};

						layerIsland.BoundingBox.Calculate(layerIsland.IslandOutline);

						supportIslands.Add(layerIsland);
					}
				}
			}

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

			slicingData.CreateWipeShield(totalLayers, config);

			timeKeeper.Restart();

			slicingData.CreateWipeTower(totalLayers, config, wipeTowerOutlines);

			int extrudersUsedInLayer0 = this.ExtrudersUsedInLayer0(config, slicingData).Count();

			if (config.EnableRaft)
			{
				slicingData.GenerateRaftOutlines(config.RaftExtraDistanceAroundPart_um, config);

				slicingData.GenerateSkirt(
					config.SkirtDistance_um + config.RaftBaseExtrusionWidth_um,
					config.RaftBaseExtrusionWidth_um,
					config.NumberOfSkirtLoops * extrudersUsedInLayer0,
					config.NumberOfBrimLoops,
					config.SkirtMinLength_um,
					config);
			}
			else
			{
				slicingData.GenerateSkirt(
					config.SkirtDistance_um,
					config.FirstLayerExtrusionWidth_um,
					config.NumberOfSkirtLoops * extrudersUsedInLayer0,
					config.NumberOfBrimLoops,
					config.SkirtMinLength_um,
					config);
			}
		}

		private void WriteGCode(LayerDataStorage slicingData)
		{
			gcodeExport.WriteComment("filamentDiameter = {0}".FormatWith(config.FilamentDiameter));
			gcodeExport.WriteComment("extrusionWidth = {0}".FormatWith(config.ExtrusionWidth));
			gcodeExport.WriteComment("firstLayerExtrusionWidth = {0}".FormatWith(config.FirstLayerExtrusionWidth));
			gcodeExport.WriteComment("layerThickness = {0}".FormatWith(config.LayerThickness));

			if (config.EnableRaft)
			{
				gcodeExport.WriteComment("firstLayerThickness = {0}".FormatWith(config.RaftBaseThickness_um / 1000.0));
			}
			else
			{
				gcodeExport.WriteComment("firstLayerThickness = {0}".FormatWith(config.FirstLayerThickness));
			}

			if (fileNumber == 1)
			{
				gcodeExport.WriteCode(config.StartCode);
			}
			else
			{
				gcodeExport.WriteFanCommand(0);
				gcodeExport.ResetExtrusionValue();
				gcodeExport.WriteRetraction(0, false);
				gcodeExport.CurrentZ_um = maxObjectHeight + 5000;
				gcodeExport.WriteMove(gcodeExport.GetPosition(), config.TravelSpeed, 0);
				gcodeExport.WriteMove(new IntPoint(slicingData.modelMin.X, slicingData.modelMin.Y, gcodeExport.CurrentZ_um), config.TravelSpeed, 0);
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
			gcodeExport.WriteComment("Layer count: {0}".FormatWith(totalLayers));

			// keep the raft generation code inside of raft
			slicingData.WriteRaftGCodeIfRequired(gcodeExport, config);

			skirtConfig = new GCodePathConfig("skirtConfig", "SKIRT");
			inset0Config = new GCodePathConfig("inset0Config", "WALL-OUTER") { DoSeamHiding = true };
			insetXConfig = new GCodePathConfig("insetXConfig", "WALL-INNER");
			fillConfig = new GCodePathConfig("fillConfig", "FILL", closedLoop: false);
			topFillConfig = new GCodePathConfig("topFillConfig", "TOP-FILL", closedLoop: false);
			firstTopFillConfig = new GCodePathConfig("firstTopFillConfig", "FIRST-TOP-Fill", closedLoop: false);
			bottomFillConfig = new GCodePathConfig("bottomFillConfig", "BOTTOM-FILL", closedLoop: false);
			airGappedBottomInsetConfig = new GCodePathConfig("airGappedBottomInsetConfig", "AIR-GAP-INSET");
			airGappedBottomConfig = new GCodePathConfig("airGappedBottomConfig", "AIR-GAP", closedLoop: false);
			bridgeConfig = new GCodePathConfig("bridgeConfig", "BRIDGE");
			supportNormalConfig = new GCodePathConfig("supportNormalConfig", "SUPPORT");
			supportInterfaceConfig = new GCodePathConfig("supportInterfaceConfig", "SUPPORT-INTERFACE");

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

				if (layerIndex < config.NumberOfFirstLayers)
				{
					skirtConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
					inset0Config.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
					insetXConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);

					fillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
					topFillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
					firstTopFillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerThickness_um);
					bottomFillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
					airGappedBottomConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
					airGappedBottomInsetConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
					bridgeConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);

					supportNormalConfig.SetData(config.FirstLayerSpeed, config.ExtrusionWidth_um);
					supportInterfaceConfig.SetData(config.FirstLayerSpeed, config.ExtrusionWidth_um);
				}
				else
				{
					skirtConfig.SetData(config.InsidePerimetersSpeed, config.ExtrusionWidth_um);
					inset0Config.SetData(config.OutsidePerimeterSpeed, config.OutsideExtrusionWidth_um);
					insetXConfig.SetData(config.InsidePerimetersSpeed, config.ExtrusionWidth_um);

					fillConfig.SetData(config.InfillSpeed, config.ExtrusionWidth_um);
					topFillConfig.SetData(config.TopInfillSpeed, config.ExtrusionWidth_um);
					firstTopFillConfig.SetData(config.BridgeSpeed, config.ExtrusionWidth_um);
					bottomFillConfig.SetData(config.BottomInfillSpeed, config.ExtrusionWidth_um);
					airGappedBottomConfig.SetData(config.AirGapSpeed, config.ExtrusionWidth_um);
					airGappedBottomInsetConfig.SetData(config.AirGapSpeed, config.ExtrusionWidth_um);
					bridgeConfig.SetData(config.BridgeSpeed, config.ExtrusionWidth_um);

					supportNormalConfig.SetData(config.SupportMaterialSpeed, config.ExtrusionWidth_um);
					supportInterfaceConfig.SetData(config.InterfaceLayerSpeed, config.ExtrusionWidth_um);
				}

				if (layerIndex == 0)
				{
					gcodeExport.SetExtrusion(config.FirstLayerThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);
				}
				else
				{
					gcodeExport.SetExtrusion(config.LayerThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);
				}

				LayerGCodePlanner layerPlanner = new LayerGCodePlanner(config, gcodeExport, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
				if (layerIndex == 0
					&& config.RetractionZHop > 0)
				{
					layerPlanner.ForceRetract();
				}

				// get the correct height for this layer
				long z = config.FirstLayerThickness_um + layerIndex * config.LayerThickness_um;
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

				gcodeExport.CurrentZ_um = z;

				if (layerIndex == 0)
				{
					gcodeExport.LayerChanged(layerIndex, config.FirstLayerThickness_um);
				}
				else
				{
					gcodeExport.LayerChanged(layerIndex, config.LayerThickness_um);
				}

				// start out with the fan off for this layer (the minimum layer fan speed will be applied later as the gcode is output)
				layerPlanner.QueueFanCommand(0, fillConfig);

				// hold the current layer path finder
				PathFinder layerPathFinder = null;

				// Loop over extruders in preferred order
				var activeThenOtherExtruders = slicingData.Extruders.CurrentThenOtherIndexes(activeExtruderIndex: layerPlanner.GetExtruder());
				foreach (int extruderIndex in activeThenOtherExtruders)
				{
					if (config.AvoidCrossingPerimeters
						&& slicingData.Extruders != null
						&& slicingData.Extruders.Count > extruderIndex)
					{
						// set the path planner to avoid islands
						layerPathFinder = slicingData.Extruders[extruderIndex].Layers[layerIndex].PathFinder;
						layerPlanner.PathFinder = layerPathFinder;
					}

					ChangeExtruderIfRequired(slicingData, layerPathFinder, layerIndex, layerPlanner, extruderIndex, false);

					// create the insets required by this extruder layer
					CreateRequiredInsets(config, slicingData, layerIndex, extruderIndex);

					if (layerIndex == 0)
					{
						QueueExtruderLayerToGCode(slicingData, layerPlanner, extruderIndex, layerIndex, config.FirstLayerExtrusionWidth_um, z);
					}
					else
					{
						QueueExtruderLayerToGCode(slicingData, layerPlanner, extruderIndex, layerIndex, config.ExtrusionWidth_um, z);
					}

					if (slicingData.Support != null)
					{
						bool movedToIsland = false;

						var usableExtruderIndex = config.SupportExtruder < config.ExtruderCount ? config.SupportExtruder : 0;

						if (layerIndex < supportIslands.Count)
						{
							SliceLayer layer = slicingData.Extruders[extruderIndex].Layers[layerIndex];
							MoveToIsland(layerPlanner, layer, supportIslands[layerIndex]);
							movedToIsland = true;
						}

						if ((usableExtruderIndex <= 0 && extruderIndex == 0)
							|| usableExtruderIndex == extruderIndex)
						{
							if (slicingData.Support.QueueNormalSupportLayer(config, layerPlanner, layerIndex, supportNormalConfig))
							{
								// we move out of the island so we aren't in it.
								islandCurrentlyInside = null;
							}
						}

						usableExtruderIndex = config.SupportInterfaceExtruder < config.ExtruderCount ? config.SupportInterfaceExtruder : 0;

						if ((usableExtruderIndex <= 0 && extruderIndex == 0)
							|| usableExtruderIndex == extruderIndex)
						{
							if (slicingData.Support.QueueInterfaceSupportLayer(config, layerPlanner, layerIndex, supportInterfaceConfig))
							{
								// we move out of the island so we aren't in it.
								islandCurrentlyInside = null;
							}
						}

						if (movedToIsland)
						{
							islandCurrentlyInside = null;
						}
					}
				}

				if (slicingData.Support != null)
				{
					z += config.SupportAirGap_um;
					gcodeExport.CurrentZ_um = z;

					foreach (int extruderIndex in slicingData.Extruders.CurrentThenOtherIndexes(activeExtruderIndex: layerPlanner.GetExtruder()))
					{
						QueueAirGappedExtruderLayerToGCode(slicingData, layerPathFinder, layerPlanner, extruderIndex, layerIndex, config.ExtrusionWidth_um, z);
					}

					slicingData.Support.QueueAirGappedBottomLayer(config, layerPlanner, layerIndex, airGappedBottomConfig);

					// don't print the wipe tower with air gap height
					z -= config.SupportAirGap_um;
					gcodeExport.CurrentZ_um = z;
				}

				if (slicingData.EnsureWipeTowerIsSolid(layerIndex, layerPathFinder, layerPlanner, fillConfig, config))
				{
					islandCurrentlyInside = null;
				}

				long currentLayerThickness_um = config.LayerThickness_um;
				if (layerIndex <= 0)
				{
					currentLayerThickness_um = config.FirstLayerThickness_um;
				}

				layerPlanner.FinalizeLayerFanSpeeds(layerIndex);
				layerPlanner.WriteQueuedGCode(currentLayerThickness_um);
			}

			LogOutput.Log("Wrote layers in {0:0.00}s.\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
			gcodeExport.WriteFanCommand(0);

			if (MatterSlice.Canceled)
			{
				return;
			}

			gcodeExport.WriteLine("; MatterSlice Completed Successfully");

			//Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
			maxObjectHeight = Math.Max(maxObjectHeight, slicingData.modelSize.Z);
		}

		private void ChangeExtruderIfRequired(LayerDataStorage slicingData,
			PathFinder layerPathFinder,
			int layerIndex,
			LayerGCodePlanner layerGcodePlanner,
			int extruderIndex,
			bool airGapped)
		{
			bool extruderUsedForSupport = slicingData.Support != null
				&& ((slicingData.Support.SparseSupportOutlines[layerIndex].Count > 0 && config.SupportExtruder == extruderIndex)
					|| (slicingData.Support.InterfaceLayers[layerIndex].Count > 0 && config.SupportInterfaceExtruder == extruderIndex));

			bool extruderUsedForParts = extruderIndex < slicingData.Extruders.Count
				&& slicingData.Extruders[extruderIndex].Layers[layerIndex].Islands.Count > 0;

			bool extruderUsedForWipeTower = extruderUsedForParts
				&& slicingData.NeedToPrintWipeTower(layerIndex, config);

			if ((extruderUsedForSupport
				|| extruderUsedForWipeTower
				|| extruderUsedForParts)
				&& layerGcodePlanner.ToolChangeRequired(extruderIndex))
			{
				// make sure that any moves we make while doing wiping are planned around the parts on the bed
				if (config.AvoidCrossingPerimeters)
				{
					// we can alway use extruder 0 as all layer PathFinders are the same object
					SliceLayer layer = slicingData.Extruders[0].Layers[layerIndex];
					layerGcodePlanner.PathFinder = layer.PathFinder;
					// and forget that we are in any island
					islandCurrentlyInside = null;
				}

				if (extruderUsedForWipeTower
					|| (slicingData.NeedToPrintWipeTower(layerIndex, config)
						&& extruderUsedForSupport))
				{
					int prevExtruder = layerGcodePlanner.GetExtruder();

					if(layerIndex > 0)
					{
						if (config.AvoidCrossingPerimeters)
						{
							// set the path planner to avoid islands
							layerGcodePlanner.PathFinder = slicingData.Extruders[extruderIndex].Layers[layerIndex].PathFinder;
						}
						// make sure we path plan our way to the wipe tower before switching extruders
						layerGcodePlanner.QueueTravel(slicingData.WipeCenter_um);
						islandCurrentlyInside = null;
					}

					// then change extruders
					layerGcodePlanner.SetExtruder(extruderIndex);

					DoSkirtAndBrim(slicingData, layerIndex, layerGcodePlanner, extruderIndex, extruderUsedForSupport);

					if(slicingData.PrimeOnWipeTower(layerIndex, layerGcodePlanner, layerPathFinder, fillConfig, config, airGapped))
					{
						islandCurrentlyInside = null;
					}
				}
				else if(extruderIndex < slicingData.Extruders.Count)
				{
					// then change extruders
					layerGcodePlanner.SetExtruder(extruderIndex);

					DoSkirtAndBrim(slicingData, layerIndex, layerGcodePlanner, extruderIndex, extruderUsedForSupport);
				}
			}
			else
			{
				DoSkirtAndBrim(slicingData, layerIndex, layerGcodePlanner, extruderIndex, extruderUsedForSupport);
			}
		}

		bool havePrintedBrims = false;

		private void DoSkirtAndBrim(LayerDataStorage slicingData, int layerIndex, LayerGCodePlanner layerGcodePlanner, int extruderIndex, bool extruderUsedForSupport)
		{
			// if we are on layer 0 we still need to print the skirt and brim
			if (layerIndex == 0)
			{
				var extrudersInLayer0 = this.ExtrudersUsedInLayer0(config, slicingData);

				var extruderUsed = extruderIndex >= 0
					&& extruderIndex < slicingData.Extruders.Count
					&& extrudersInLayer0.Contains(extruderIndex);

				if (!config.ShouldGenerateRaft()
					&& (extruderUsed || extruderUsedForSupport))
				{
					QueueSkirtToGCode(slicingData, layerGcodePlanner, layerIndex, extruderIndex);

					// we don't print a brim if we have a raft
					if (!havePrintedBrims)
					{
						QueueBrimsToGCode(slicingData, layerGcodePlanner, layerIndex, extruderIndex);
						havePrintedBrims = true;
					}
				}
			}
		}

		private void CreateRequiredInsets(ConfigSettings config, LayerDataStorage slicingData, int outputLayerIndex, int extruderIndex)
		{
			if (extruderIndex < slicingData.Extruders.Count)
			{
				var startIndex = Math.Max(0, outputLayerIndex - config.NumberOfBottomLayers - 1);
				var endIndex = Math.Min(slicingData.Extruders[extruderIndex].Layers.Count - 1, outputLayerIndex + config.NumberOfTopLayers + 1);

				// free up the insets from the previous layer
				if (startIndex > config.NumberOfBottomLayers + 1)
				{
					SliceLayer previousLayer = slicingData.Extruders[extruderIndex].Layers[startIndex - 2];
					previousLayer.FreeIslandMemory();
				}

				for (int layerIndex = startIndex; layerIndex <= endIndex; layerIndex++)
				{
					SliceLayer layer = slicingData.Extruders[extruderIndex].Layers[layerIndex];

					if (layer.Islands.Count > 0
						&& !layer.CreatedInsets)
					{
						layer.CreatedInsets = true;
						int insetCount = config.NumberOfPerimeters;
						if (config.ContinuousSpiralOuterPerimeter && (int)(layerIndex) < config.NumberOfBottomLayers && layerIndex % 2 == 1)
						{
							//Add extra insets every 2 layers when spiralizing, this makes bottoms of cups watertight.
							insetCount += 1;
						}

						if (layerIndex == 0)
						{
							layer.GenerateInsets(config.FirstLayerExtrusionWidth_um, config.FirstLayerExtrusionWidth_um, insetCount, config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter, config.AvoidCrossingPerimeters);
						}
						else
						{
							layer.GenerateInsets(config.ExtrusionWidth_um, config.OutsideExtrusionWidth_um, insetCount, config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter, config.AvoidCrossingPerimeters);
						}
					}
				}

				//Only generate bottom and top layers and infill for the first X layers when spiralize is chosen.
				if (!config.ContinuousSpiralOuterPerimeter || (int)(outputLayerIndex) < config.NumberOfBottomLayers)
				{
					if (outputLayerIndex == 0)
					{
						slicingData.Extruders[extruderIndex].GenerateTopAndBottoms(config, outputLayerIndex, config.FirstLayerExtrusionWidth_um, config.FirstLayerExtrusionWidth_um, config.NumberOfBottomLayers, config.NumberOfTopLayers, config.InfillExtendIntoPerimeter_um);
					}
					else
					{
						slicingData.Extruders[extruderIndex].GenerateTopAndBottoms(config, outputLayerIndex, config.ExtrusionWidth_um, config.OutsideExtrusionWidth_um, config.NumberOfBottomLayers, config.NumberOfTopLayers, config.InfillExtendIntoPerimeter_um);
					}
				}
			}
		}

		void QueuePerimeterWithMergeOverlaps(Polygon perimeterToCheckForMerge, int layerIndex, LayerGCodePlanner gcodeLayer, GCodePathConfig config)
		{
			Polygons pathsWithOverlapsRemoved = null;
			bool pathHadOverlaps = false;
			bool pathIsClosed = true;

			if (perimeterToCheckForMerge.Count > 2)
			{
				pathHadOverlaps = perimeterToCheckForMerge.MergePerimeterOverlaps(config.LineWidthUM, out pathsWithOverlapsRemoved, pathIsClosed)
					&& pathsWithOverlapsRemoved.Count > 0;
			}

			if (pathHadOverlaps)
			{
				bool oldClosedLoop = config.ClosedLoop;
				config.ClosedLoop = false;
				QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, pathsWithOverlapsRemoved, config, SupportWriteType.UnsupportedAreas);
				config.ClosedLoop = oldClosedLoop;
			}
			else
			{
				QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, new Polygons() { perimeterToCheckForMerge }, config, SupportWriteType.UnsupportedAreas);
			}
		}

		private void QueueSkirtToGCode(LayerDataStorage slicingData, LayerGCodePlanner gcodeLayer, int layerIndex, int extruderIndex)
		{
			var extrudersInLayer0 = this.ExtrudersUsedInLayer0(config, slicingData).Count();

			var loopsPerExtruder = slicingData.Skirt.Count / extrudersInLayer0;

			// Single extruder in layer 0 - print all on target extruder
			if (extrudersInLayer0 == 1)
			{
				for (int i = loopsPerExtruder - 1; i >= 0; i--)
				{
					gcodeLayer.QueuePolygonByOptimizer(slicingData.Skirt[i], null, skirtConfig, layerIndex);
				}

				return;
			}

			// Multiple extruders in layer 0 - split up skirt loops
			var loopIndex = loopsPerExtruder * ((config.ExtruderCount - 1) - extruderIndex);

			for (int i = loopsPerExtruder - 1; i >= 0; i--)
			{
				gcodeLayer.QueuePolygonByOptimizer(slicingData.Skirt[loopIndex + i], null, skirtConfig, layerIndex);
			}
		}

		private HashSet<int> ExtrudersUsedInLayer0(ConfigSettings config, LayerDataStorage slicingData)
		{
			var layer0Extruders = slicingData.Extruders.Where(e => e.UsedInLayer(0)).Select((e, index) => index);

			var usedExtruders = new HashSet<int>(layer0Extruders);

			if (slicingData.Support != null)
			{
				// Add interface layer extruder from layer 0
				if (slicingData.Support.InterfaceLayers.Any() == true
					&& slicingData.Support.InterfaceLayers[0].Any())
				{
					usedExtruders.Add(config.SupportInterfaceExtruder);
				}

				// Add support extruders from layer 0
				if (slicingData.Support.SparseSupportOutlines.Any()
					&& slicingData.Support.SparseSupportOutlines[0].Any())
				{
					usedExtruders.Add(config.SupportExtruder);
				}
			}

			// Add raft extruders from layer 0
			if (slicingData.raftOutline.Any()
				&& slicingData.raftOutline[0].Any())
			{
				usedExtruders.Add(config.RaftExtruder);
			}

			return usedExtruders;
		}

		private void QueueBrimsToGCode(LayerDataStorage slicingData, LayerGCodePlanner gcodeLayer, int layerIndex, int extruderIndex)
		{
			gcodeLayer.QueuePolygonsByOptimizer(slicingData.Brims, null, skirtConfig, layerIndex);
		}

		LayerIsland islandCurrentlyInside = null;

		//Add a single layer from a single extruder to the GCode
		private void QueueExtruderLayerToGCode(LayerDataStorage slicingData,
			LayerGCodePlanner layerGcodePlanner,
			int extruderIndex,
			int layerIndex,
			long extrusionWidth_um,
			long currentZ_um)
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

			// the wipe shield is only generated one time per layer
			if (slicingData.WipeShield.Count > 0
				&& slicingData.WipeShield[layerIndex].Count > 0
				&& slicingData.Extruders.Count > 1)
			{
				layerGcodePlanner.ForceRetract();
				layerGcodePlanner.QueuePolygonsByOptimizer(slicingData.WipeShield[layerIndex], null, skirtConfig, layerIndex);
				layerGcodePlanner.ForceRetract();
				// remember that we have already laid down the wipe shield by clearing the data for this layer
				slicingData.WipeShield[layerIndex].Clear();
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
			islandOrderOptimizer.Optimize(layer.PathFinder, layerIndex);

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

				var fillPolygons = new Polygons();
				var topFillPolygons = new Polygons();
				var firstTopFillPolygons = new Polygons();
				var bridgePolygons = new Polygons();

				var bottomFillPolygons = new Polygons();

				CalculateInfillData(slicingData, extruderIndex, layerIndex, island, bottomFillPolygons, fillPolygons, firstTopFillPolygons, topFillPolygons, bridgePolygons);
				bottomFillIslandPolygons.Add(bottomFillPolygons);

				int fanBeforeBridgePolygons = gcodeExport.CurrentFanSpeed;

				if (config.NumberOfPerimeters > 0)
				{
					if (config.ContinuousSpiralOuterPerimeter
						&& layerIndex >= config.NumberOfBottomLayers)
					{
						inset0Config.Spiralize = true;
					}

					// Put all the insets into a new list so we can keep track of what has been printed.
					// The island could be a rectangle with 4 screw holes. So, with 3 perimeters that could be the outside 3 + the holes 4 * 3, 15 polygons.
					List<Polygons> insetsForThisIsland = new List<Polygons>(island.InsetToolPaths.Count);

					foreach (var insetToolPath in island.InsetToolPaths)
					{
						var polygons = new Polygons();

						foreach(var insetPolygon in insetToolPath)
						{
							if (insetPolygon.Count > 0)
							{
								polygons.Add(insetPolygon);
							}
						}

						insetsForThisIsland.Add(polygons);
					}

					GCodePathConfig overrideConfig = null;
					if (bridgePolygons.Count > 0)
					{
						// turn it on for bridge (or keep it on)
						layerGcodePlanner.QueueFanCommand(config.BridgeFanSpeedPercent, bridgeConfig);
						overrideConfig = bridgeConfig;
					}
					// If we are on the very first layer we always start with the outside so that we can stick to the bed better.
					if (config.OutsidePerimetersFirst || layerIndex == 0 || inset0Config.Spiralize)
					{
						if (inset0Config.Spiralize)
						{
							if (island.InsetToolPaths.Count > 0)
							{
								Polygon outsideSinglePolygon = island.InsetToolPaths[0][0];
								layerGcodePlanner.QueuePolygonsByOptimizer(new Polygons() { outsideSinglePolygon }, null, inset0Config, layerIndex);
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
									QueueClosestInset(insetsForThisIsland[0], limitDistance, overrideConfig == null ? inset0Config : overrideConfig, layerIndex, layerGcodePlanner);
								}

								// Move to the closest inset 1 and print it
								for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count; insetIndex++)
								{
									limitDistance = QueueClosestInset(insetsForThisIsland[insetIndex], limitDistance, overrideConfig == null ? insetXConfig : overrideConfig, layerIndex, layerGcodePlanner);
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
										var closestInsetStart = FindBestPoint(insetsForThisIsland[0], layerGcodePlanner.LastPosition, layerIndex);
										if (closestInsetStart.X != long.MinValue)
										{
											(int polyIndex, int pointIndex, IntPoint position) found = (-1, -1, closestInsetStart);
											for (int findInsetIndex = island.InsetToolPaths.Count - 1; findInsetIndex  >= 0; findInsetIndex--)
											{
												found = insetsForThisIsland[findInsetIndex].FindClosestPoint(closestInsetStart);
												if (found.polyIndex != -1
													&& found.pointIndex != -1)
												{
													break;
												}
											}

											if (found.polyIndex != -1
												&& found.pointIndex != -1)
											{
												layerGcodePlanner.QueueTravel(found.position);
											}
											else
											{
												layerGcodePlanner.QueueTravel(closestInsetStart);
											}
										}
									}

									limitDistance = QueueClosestInset(
										insetsForThisIsland[insetIndex],
										limitDistance,
										overrideConfig == null ? (insetIndex == 0 ? inset0Config : insetXConfig) : overrideConfig,
										layerIndex,
										layerGcodePlanner);

									if (insetIndex == 0)
									{
										// If we are on the outside perimeter move in before we travel (so we don't retract on the outside)
										(int polyIndex, int pointIndex, IntPoint position) found2 = (-1, -1, layerGcodePlanner.LastPosition);
										var distFromLastPoint = double.PositiveInfinity;
										for (int findInsetIndex = island.InsetToolPaths.Count - 1; findInsetIndex >= 1; findInsetIndex--)
										{
											found2 = island.InsetToolPaths[findInsetIndex].FindClosestPoint(layerGcodePlanner.LastPosition);
											if (found2.polyIndex != -1
												&& found2.pointIndex != -1)
											{
												distFromLastPoint = (found2.position - layerGcodePlanner.LastPosition).Length();
												if (distFromLastPoint < config.MinimumTravelToCauseRetraction_um)
												{
													break;
												}
											}
										}

										if (found2.polyIndex != -1
											&& found2.pointIndex != -1
											&& distFromLastPoint < config.MinimumTravelToCauseRetraction_um)
										{
											layerGcodePlanner.QueueTravel(found2.position, forceUniquePath: true);
										}
									}
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
					// Set it back to what it was
					layerGcodePlanner.QueueFanCommand(fanBeforeBridgePolygons, fillConfig);
				}

				// TODO: Put all of these segments into a list that can be queued together and still preserve their individual config settings.
				// This will make the total amount of travel while printing infill much less.
				layerGcodePlanner.QueuePolygonsByOptimizer(fillPolygons, island.PathFinder, fillConfig, layerIndex);
				QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, bottomFillPolygons, bottomFillConfig, SupportWriteType.UnsupportedAreas);
				if (firstTopFillPolygons.Count > 0)
				{
					int fanSpeedPercent = gcodeExport.CurrentFanSpeed;
					// turn it on for bridge (or keep it on)
					layerGcodePlanner.QueueFanCommand(Math.Max(fanSpeedPercent, config.BridgeFanSpeedPercent), bridgeConfig);
					layerGcodePlanner.QueuePolygonsByOptimizer(firstTopFillPolygons, island.PathFinder, firstTopFillConfig, layerIndex);
					// Set it back to what it was
					layerGcodePlanner.QueueFanCommand(fanSpeedPercent, fillConfig);
				}
				layerGcodePlanner.QueuePolygonsByOptimizer(topFillPolygons, island.PathFinder, topFillConfig, layerIndex);
			}
		}

		private void MoveToIsland(LayerGCodePlanner layerGcodePlanner, SliceLayer layer, LayerIsland island)
		{
			if(config.ContinuousSpiralOuterPerimeter)
			{
				return;
			}

			if (island.IslandOutline.Count > 0)
			{
				// If we are already in the island we are going to, don't go there, or there is only one island.
				if ((layer.Islands.Count == 1 && config.ExtruderCount == 1)
					|| layer.PathFinder.OutlineData?.Polygons.Count < 3
					|| island.PathFinder.OutlineData.Polygons.PointIsInside(layerGcodePlanner.LastPosition, island.PathFinder.OutlineData.EdgeQuadTrees, island.PathFinder.OutlineData.PointQuadTrees) == true)
				{
					islandCurrentlyInside = island;
					layerGcodePlanner.PathFinder = island.PathFinder;
					return;
				}

				var closestPointOnNextIsland = island.IslandOutline.FindClosestPoint(layerGcodePlanner.LastPosition);
				IntPoint closestNextIslandPoint = island.IslandOutline[closestPointOnNextIsland.polyIndex][closestPointOnNextIsland.pointIndex];

				if (islandCurrentlyInside?.PathFinder?.OutlineData.Polygons.Count > 0
					&& islandCurrentlyInside?.PathFinder?.OutlineData.Polygons?[0]?.Count > 3)
				{
					// start by moving within the last island to the closet point to the next island
					var polygons = islandCurrentlyInside.PathFinder.OutlineData.Polygons;
					if (polygons.Count > 0)
					{
						var closestPointOnLastIsland = polygons.FindClosestPoint(closestNextIslandPoint);

						IntPoint closestLastIslandPoint = polygons[closestPointOnLastIsland.Item1][closestPointOnLastIsland.Item2];
						// make sure we are planning within the last island we were using
						layerGcodePlanner.PathFinder = islandCurrentlyInside.PathFinder;
						layerGcodePlanner.QueueTravel(closestLastIslandPoint);
					}
				}

				// how far are we going to move
				var delta = closestNextIslandPoint - layerGcodePlanner.LastPosition;
				var moveDistance = delta.Length();
				var islandAvoidDistance = layer.PathFinder.InsetAmount;
				if (moveDistance > islandAvoidDistance)
				{
					// make sure we are not planning moves for the move away from the island
					layerGcodePlanner.PathFinder = null;
					// move away from our current island as much as the inset amount to avoid planning around where we are
					var awayFromIslandPosition = layerGcodePlanner.LastPosition + delta.Normal(islandAvoidDistance);
					layerGcodePlanner.QueueTravel(awayFromIslandPosition);

					// let's move to this island avoiding running into any other islands
					layerGcodePlanner.PathFinder = layer.PathFinder;
					// find the closest point to where we are now
					// and do a move to there
				}

				// retract if we need to
				if (config.RetractWhenChangingIslands)
				{
					layerGcodePlanner.ForceRetract();
				}

				// go to the next point
				layerGcodePlanner.QueueTravel(closestNextIslandPoint);

				// and remember that we are now in the new island
				islandCurrentlyInside = island;
				// set the path planner to the current island
				layerGcodePlanner.PathFinder = island.PathFinder;
			}
		}

		public IntPoint FindBestPoint(Polygons boundaryPolygons, IntPoint position, int layerIndex)
		{
			IntPoint polyPointPosition = new IntPoint(long.MinValue, long.MinValue);

			long bestDist = long.MaxValue;
			for (int polygonIndex = 0; polygonIndex < boundaryPolygons.Count; polygonIndex++)
			{
				IntPoint closestToPoly = boundaryPolygons[polygonIndex].FindGreatestTurnPosition(config.ExtrusionWidth_um, layerIndex, position);
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

		private bool QueueClosestInset(Polygons insetsToConsider, bool limitDistance, GCodePathConfig pathConfig, int layerIndex, LayerGCodePlanner gcodeLayer)
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
				if (config.MergeOverlappingLines
					&& pathConfig != inset0Config) // we do not merge the outer perimeter
				{
					QueuePerimeterWithMergeOverlaps(insetsToConsider[polygonPrintedIndex], layerIndex, gcodeLayer, pathConfig);
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
		private void QueueAirGappedExtruderLayerToGCode(LayerDataStorage slicingData, PathFinder layerPathFinder, LayerGCodePlanner layerGcodePlanner, int extruderIndex, int layerIndex, long extrusionWidth_um, long currentZ_um)
		{
			if (slicingData.Support != null
				&& !config.ContinuousSpiralOuterPerimeter
				&& layerIndex > 0)
			{
				SliceLayer layer = slicingData.Extruders[extruderIndex].Layers[layerIndex];

				var islandOrderOptimizer = new PathOrderOptimizer(new IntPoint());
				for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
				{
					if (layer.Islands[islandIndex].InsetToolPaths.Count > 0)
					{
						islandOrderOptimizer.AddPolygon(layer.Islands[islandIndex].InsetToolPaths[0][0]);
					}
				}
				islandOrderOptimizer.Optimize(layer.PathFinder, layerIndex);

				for (int islandOrderIndex = 0; islandOrderIndex < islandOrderOptimizer.bestIslandOrderIndex.Count; islandOrderIndex++)
				{
					LayerIsland island = layer.Islands[islandOrderOptimizer.bestIslandOrderIndex[islandOrderIndex]];

					var bottomFillPolygons = new Polygons();
					CalculateInfillData(slicingData, extruderIndex, layerIndex, island, bottomFillPolygons);

					// TODO: check if we are going to output anything
					bool outputDataForIsland = false;
					// Print everything but the first perimeter from the outside in so the little parts have more to stick to.
					for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count; insetIndex++)
					{
						outputDataForIsland |= QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, island.InsetToolPaths[insetIndex], airGappedBottomInsetConfig, SupportWriteType.SupportedAreasCheckOnly);
					}
					// then 0
					if (island.InsetToolPaths.Count > 0)
					{
						outputDataForIsland |= QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, island.InsetToolPaths[0], airGappedBottomInsetConfig, SupportWriteType.SupportedAreasCheckOnly);
					}

					outputDataForIsland |= QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, bottomFillPolygons, airGappedBottomConfig, SupportWriteType.SupportedAreasCheckOnly);

					if (outputDataForIsland)
					{
						ChangeExtruderIfRequired(slicingData, layerPathFinder, layerIndex, layerGcodePlanner, extruderIndex, true);

						if (!config.AvoidCrossingPerimeters
							&& config.RetractWhenChangingIslands)
						{
							layerGcodePlanner.ForceRetract();
						}

						MoveToIsland(layerGcodePlanner, layer, island);

						// Print everything but the first perimeter from the outside in so the little parts have more to stick to.
						for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count; insetIndex++)
						{
							QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, island.InsetToolPaths[insetIndex], airGappedBottomInsetConfig, SupportWriteType.SupportedAreas);
						}
						// then 0
						if (island.InsetToolPaths.Count > 0)
						{
							QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, island.InsetToolPaths[0], airGappedBottomInsetConfig, SupportWriteType.SupportedAreas);
						}

						QueuePolygonsConsideringSupport(layerIndex, layerGcodePlanner, bottomFillPolygons, airGappedBottomConfig, SupportWriteType.SupportedAreas);
					}
				}
			}

			// gcodeLayer.SetPathFinder(null);
		}

		private enum SupportWriteType { UnsupportedAreas, SupportedAreas, SupportedAreasCheckOnly };

		/// <summary>
		/// Return true if any polygons are actually output
		/// </summary>
		/// <param name="layerIndex"></param>
		/// <param name="gcodeLayer"></param>
		/// <param name="polygonsToWrite"></param>
		/// <param name="fillConfig"></param>
		/// <param name="supportWriteType"></param>
		/// <returns></returns>
		private bool QueuePolygonsConsideringSupport(int layerIndex, LayerGCodePlanner gcodeLayer, Polygons polygonsToWrite, GCodePathConfig fillConfig, SupportWriteType supportWriteType)
		{
			bool polygonsWereOutput = false;
			bool oldLoopValue = fillConfig.ClosedLoop;

			if (slicingData.Support != null
				&& layerIndex > 0
				&& !config.ContinuousSpiralOuterPerimeter)
			{
				Polygons supportOutlines = slicingData.Support.GetRequiredSupportAreas(layerIndex).Offset(fillConfig.LineWidthUM / 2);

				if (supportWriteType == SupportWriteType.UnsupportedAreas)
				{
					if (supportOutlines.Count > 0)
					{
						// write segments at normal height (don't write segments needing air gap)
						Polygons polysToWriteAtNormalHeight = new Polygons();
						Polygons polysToWriteAtAirGapHeight = new Polygons();

						GetSegmentsConsideringSupport(polygonsToWrite, supportOutlines, polysToWriteAtNormalHeight, polysToWriteAtAirGapHeight, false, fillConfig.ClosedLoop);
						fillConfig.ClosedLoop = false;
						polygonsWereOutput |= gcodeLayer.QueuePolygonsByOptimizer(polysToWriteAtNormalHeight, null, fillConfig, layerIndex);
					}
					else
					{
						polygonsWereOutput |= gcodeLayer.QueuePolygonsByOptimizer(polygonsToWrite, null, fillConfig, layerIndex);
					}
				}
				else if (supportOutlines.Count > 0) // we are checking the supported areas
				{
					// detect and write segments at air gap height
					Polygons polysToWriteAtNormalHeight = new Polygons();
					Polygons polysToWriteAtAirGapHeight = new Polygons();

					GetSegmentsConsideringSupport(polygonsToWrite, supportOutlines, polysToWriteAtNormalHeight, polysToWriteAtAirGapHeight, true, fillConfig.ClosedLoop);
					fillConfig.ClosedLoop = false;

					if (supportWriteType == SupportWriteType.SupportedAreasCheckOnly)
					{
						polygonsWereOutput = polysToWriteAtAirGapHeight.Count > 0;
					}
					else
					{
						polygonsWereOutput |= gcodeLayer.QueuePolygonsByOptimizer(polysToWriteAtAirGapHeight, null, fillConfig, layerIndex);
					}
				}
			}
			else if (supportWriteType == SupportWriteType.UnsupportedAreas)
			{
				polygonsWereOutput |= gcodeLayer.QueuePolygonsByOptimizer(polygonsToWrite, null, fillConfig, layerIndex);
			}

			fillConfig.ClosedLoop = oldLoopValue;

			return polygonsWereOutput;
		}

		private void GetSegmentsConsideringSupport(Polygons polygonsToWrite, Polygons supportOutlines, Polygons polysToWriteAtNormalHeight, Polygons polysToWriteAtAirGapHeight, bool forAirGap, bool closedLoop)
		{
			// make an expanded area to constrain our segments to
			Polygons maxSupportOutlines = supportOutlines.Offset(fillConfig.LineWidthUM * 2 + config.SupportXYDistance_um);

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

		private void CalculateInfillData(LayerDataStorage slicingData, int extruderIndex, int layerIndex, LayerIsland part, Polygons bottomFillLines, Polygons fillPolygons = null, Polygons firstTopFillPolygons = null, Polygons topFillPolygons = null, Polygons bridgePolygons = null)
		{
			double alternatingInfillAngle = config.InfillStartingAngle;
			if ((layerIndex % 2) == 0)
			{
				alternatingInfillAngle += 90;
			}

			// generate infill for the bottom layer including bridging
			foreach (Polygons bottomFillIsland in part.BottomPaths.ProcessIntoSeparateIslands())
			{
				if (layerIndex > 0)
				{
					if (slicingData.Support != null)
					{
						double infillAngle = config.SupportInterfaceLayers > 0 ? config.InfillStartingAngle : config.InfillStartingAngle + 90;
						Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, infillAngle);
					}
					else
					{
						SliceLayer previousLayer = slicingData.Extruders[extruderIndex].Layers[layerIndex - 1];

						double bridgeAngle = 0;
						if (bridgePolygons != null
							&& previousLayer.BridgeAngle(bottomFillIsland, out bridgeAngle))
						{
							// TODO: Make this code handle very complex pathing between different sizes or layouts of support under the island to fill.
							Infill.GenerateLinePaths(bottomFillIsland, bridgePolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, bridgeAngle);
						}
						else
						{
							/*
							foreach(var line in bottomFillIsland)
							{
								bottomFillLines.Add(line);
							} */

							Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle);
						}
					}
				}
				else
				{
					Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.FirstLayerExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle);
				}
			}

			// generate infill for the top layer
			if (topFillPolygons != null)
			{
				foreach (Polygons outline in part.TopPaths.ProcessIntoSeparateIslands())
				{
					// the top layer always draws the infill in the same direction (for aesthetics)
					Infill.GenerateLinePaths(outline, topFillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle);
				}
			}
			// generate infill for the top layer
			if (firstTopFillPolygons != null)
			{
				foreach (Polygons outline in part.FirstTopPaths.ProcessIntoSeparateIslands())
				{
					Infill.GenerateLinePaths(outline, firstTopFillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle);
				}
			}

			// generate infill intermediate layers
			if (fillPolygons != null)
			{
				foreach (Polygons outline in part.SolidInfillPaths.ProcessIntoSeparateIslands())
				{
					Infill.GenerateLinePaths(outline, fillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle);
				}

				// generate the sparse infill for this part on this layer
				if (config.InfillPercent > 0)
				{
					switch (config.InfillType)
					{
						case ConfigConstants.INFILL_TYPE.LINES:
							Infill.GenerateLineInfill(config, part.SparseInfillPaths, fillPolygons, alternatingInfillAngle);
							break;

						case ConfigConstants.INFILL_TYPE.GRID:
							Infill.GenerateGridInfill(config, part.SparseInfillPaths, fillPolygons, config.InfillStartingAngle);
							break;

						case ConfigConstants.INFILL_TYPE.TRIANGLES:
							Infill.GenerateTriangleInfill(config, part.SparseInfillPaths, fillPolygons, config.InfillStartingAngle);
							break;

						case ConfigConstants.INFILL_TYPE.HEXAGON:
							Infill.GenerateHexagonInfill(config, part.SparseInfillPaths, fillPolygons, config.InfillStartingAngle, layerIndex);
							break;

						case ConfigConstants.INFILL_TYPE.CONCENTRIC:
							Infill.GenerateConcentricInfill(config, part.SparseInfillPaths, fillPolygons);
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
		}

		public void Cancel()
		{
			if (gcodeExport.IsOpened())
			{
				gcodeExport.Close();
			}
		}
	}
}