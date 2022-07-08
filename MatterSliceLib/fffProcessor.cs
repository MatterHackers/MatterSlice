/*
This file is part of MatterSlice. A command line utility for
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2021, Lars Brubaker

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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Pathfinding;
using MatterHackers.QuadTree;
using MatterHackers.VectorMath;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
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
	public class FffProcessor
	{
		private long maxObjectHeight;
		private int fileNumber;
		private GCodeExport gcodeExport;
		private ConfigSettings config;
		private Stopwatch timeKeeper = new Stopwatch();

		private SimpleMeshCollection simpleMeshCollection = new SimpleMeshCollection();
		private OptimizedMeshCollection optimizedMeshCollection;
		private LayerDataStorage layerDataStorage = new LayerDataStorage();

		private GCodePathConfig skirtConfig;
		private GCodePathConfig inset0Config;
		private GCodePathConfig insetXConfig;
		private GCodePathConfig sparseFillConfig;
		private GCodePathConfig solidFillConfig;
		private GCodePathConfig topFillConfig;
		private GCodePathConfig firstTopFillConfig;
		private GCodePathConfig bottomFillConfig;
		private GCodePathConfig airGappedBottomInsetConfig;
		private GCodePathConfig airGappedBottomConfig;
		private GCodePathConfig bridgeConfig;
		private GCodePathConfig supportNormalConfig;
		private GCodePathConfig supportInterfaceConfig;

		public FffProcessor(ConfigSettings config)
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
			LogOutput.Log("Optimized model: {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Reset();

			var timeKeeperTotal = Stopwatch.StartNew();

			gcodeExport.SetLayerChangeCode(config.LayerChangeCode);

			SliceModels(layerDataStorage);

			ProcessSliceData(layerDataStorage);

			var extraPathingConsideration = new Polygons();
			foreach (var polygons in layerDataStorage.WipeShield)
			{
				extraPathingConsideration.AddRange(polygons);
			}

			foreach (var polygons in layerDataStorage.WipeTower)
			{
				extraPathingConsideration.AddRange(polygons);
			}

			ExtruderLayers.InitializeLayerPathing(config, extraPathingConsideration, layerDataStorage.Extruders);
			LogOutput.Log("Generated Outlines: {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Reset();

			if (MatterSlice.Canceled)
			{
				return;
			}

			WriteGCode(layerDataStorage);
			if (MatterSlice.Canceled)
			{
				return;
			}

			LogOutput.logProgress("process", 1, 1); // Report to the GUI that a file has been fully processed.
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

		public void Dispose()
		{
			if (!gcodeExport.IsOpened())
			{
				return;
			}

			gcodeExport.Finalize(maxObjectHeight, config.TravelSpeed, config.EndCode);

			gcodeExport.Close();
		}

		private void SliceModels(LayerDataStorage layerDataStorage)
		{
			timeKeeper.Restart();
#if false
			optimizedModel.saveDebugSTL("debug_output.stl");
#endif

			var extruderDataLayers = new List<ExtruderData>();
			for (int i = 0; i < optimizedMeshCollection.OptimizedMeshes.Count; i++)
			{
				extruderDataLayers.Add(null);
			}

			Agg.Parallel.For(0, optimizedMeshCollection.OptimizedMeshes.Count, (index) =>
			{
				var optimizedMesh = optimizedMeshCollection.OptimizedMeshes[index];
				var extruderData = new ExtruderData(optimizedMesh, config);
				extruderDataLayers[index] = extruderData;
				extruderData.ReleaseMemory();
			});

#if false
			slicerList[0].DumpSegmentsToGcode("Volume 0 Segments.gcode");
			slicerList[0].DumpPolygonsToGcode("Volume 0 Polygons.gcode");
			// slicerList[0].DumpPolygonsToHTML("Volume 0 Polygons.html");
#endif

			LogOutput.Log("Sliced model: {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			layerDataStorage.modelSize = optimizedMeshCollection.size_um;
			layerDataStorage.modelMin = optimizedMeshCollection.minXYZ_um;
			layerDataStorage.modelMax = optimizedMeshCollection.maxXYZ_um;

			for (int i = 0; i < extruderDataLayers.Count; i++)
			{
				layerDataStorage.Extruders.Add(null);
			}

			for (int index = 0; index < extruderDataLayers.Count; index++)
			{
				var extruderLayer = new ExtruderLayers(extruderDataLayers[index], config.outputOnlyFirstLayer);
				layerDataStorage.Extruders[index] = extruderLayer;

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

			LogOutput.Log("Generated layer parts: {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
		}

		private void ProcessSliceData(LayerDataStorage layerDataStorage)
		{
			if (config.ContinuousSpiralOuterPerimeter)
			{
				config.NumberOfTopLayers = 0;
				config.InfillPercent = 0;
			}

			LogOutput.Log("Fixing touching surfaces");
			layerDataStorage.Extruders = MultiExtruders.ProcessBooleans(layerDataStorage.Extruders, config.BooleanOperations);
			LogOutput.Log("Touching surfaces fixed: {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			LogOutput.Log("Removing extruder intersections");
			MultiExtruders.RemoveExtruderIntersections(layerDataStorage.Extruders, config);
			LogOutput.Log("Extruder intersections removed: {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			// Is the last extruder data actually fuzzy boundary definitions?
			bool fuzzyBoundary = config.BooleanOperations.Contains("F");
			if (fuzzyBoundary)
			{
				var fuzzyLayers = layerDataStorage.Extruders[layerDataStorage.Extruders.Count - 1];
				// Last extruder was support material, remove it from the list.
				layerDataStorage.Extruders.RemoveAt(layerDataStorage.Extruders.Count - 1);
				layerDataStorage.CreateFuzzyBoundaries(config, fuzzyLayers);
			}

			// Is the last extruder data actually wipe tower definitions?
			bool userGeneratedWipeTower = config.BooleanOperations.Contains("W");
			ExtruderLayers wipeTowerLayers = null;
			if (userGeneratedWipeTower)
			{
				wipeTowerLayers = layerDataStorage.Extruders[layerDataStorage.Extruders.Count - 1];
				// Last extruder was support material, remove it from the list.
				layerDataStorage.Extruders.RemoveAt(layerDataStorage.Extruders.Count - 1);
			}

			// Is the last extruder data actually support definitions?
			bool userGeneratedSupport = config.BooleanOperations.Contains("S");
			ExtruderLayers supportLayers = null;
			if (userGeneratedSupport)
			{
				supportLayers = layerDataStorage.Extruders[layerDataStorage.Extruders.Count - 1];
				// Last extruder was support material, remove it from the list.
				layerDataStorage.Extruders.RemoveAt(layerDataStorage.Extruders.Count - 1);
			}

			MultiExtruders.OverlapMultipleExtrudersSlightly(layerDataStorage.Extruders, config.MultiExtruderOverlapPercent);
#if False
			LayerPart.dumpLayerparts(layerDataStorage, "output.html");
#endif

			if ((config.GenerateSupport || supportLayers != null)
				&& !config.ContinuousSpiralOuterPerimeter)
			{
				LogOutput.Log("Generating supports\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
				timeKeeper.Restart();
				layerDataStorage.Support = new SupportLayers(config, layerDataStorage.Extruders, supportLayers);
				LogOutput.Log("Generated supports: {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			}

			layerDataStorage.CreateIslandData();

			int totalLayers = layerDataStorage.Extruders[0].Layers.Count;
			if (config.outputOnlyFirstLayer)
			{
				totalLayers = 1;
			}
#if DEBUG
			for (int extruderIndex = 1; extruderIndex < layerDataStorage.Extruders.Count; extruderIndex++)
			{
				if (totalLayers != layerDataStorage.Extruders[extruderIndex].Layers.Count)
				{
					throw new Exception("All the extruders must have the same number of layers (they just can have empty layers).");
				}
			}
#endif

			layerDataStorage.CreateWipeShield(totalLayers, config);

			timeKeeper.Restart();

			layerDataStorage.CreateWipeTower(config, wipeTowerLayers);

			int extrudersUsedInLayer0 = this.ExtrudersUsedInLayer0(config, layerDataStorage).Count();

			if (config.EnableRaft)
			{
				layerDataStorage.GenerateRaftOutlines(config.RaftExtraDistanceAroundPart_um, config);

				layerDataStorage.GenerateSkirt(
					config.SkirtDistance_um + config.RaftBaseExtrusionWidth_um,
					config.RaftBaseExtrusionWidth_um,
					config.NumberOfSkirtLoops * extrudersUsedInLayer0,
					config.NumberOfBrimLoops,
					config.SkirtMinLength_um,
					config);
			}
			else
			{
				layerDataStorage.GenerateSkirt(
					config.SkirtDistance_um,
					config.FirstLayerExtrusionWidth_um,
					config.NumberOfSkirtLoops * extrudersUsedInLayer0,
					config.NumberOfBrimLoops,
					config.SkirtMinLength_um,
					config);
			}
		}

		private void WriteGCode(LayerDataStorage layerDataStorage)
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
				gcodeExport.WriteMove(gcodeExport.PositionXy_um, config.TravelSpeed, 0);
				gcodeExport.WriteMove(new IntPoint(layerDataStorage.modelMin.X, layerDataStorage.modelMin.Y, gcodeExport.CurrentZ_um), config.TravelSpeed, 0);
			}

			fileNumber++;

			int totalLayers = layerDataStorage.Extruders[0].Layers.Count;
			if (config.outputOnlyFirstLayer)
			{
				totalLayers = 1;
			}

			// let's remove any of the layers on top that are empty
			{
				for (int layerIndex = totalLayers - 1; layerIndex >= 0; layerIndex--)
				{
					bool layerHasData = false;
					foreach (ExtruderLayers currentExtruder in layerDataStorage.Extruders)
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
			layerDataStorage.WriteRaftGCodeIfRequired(gcodeExport, config);

			skirtConfig = new GCodePathConfig("skirtConfig", "SKIRT", config.DefaultAcceleration);
			inset0Config = new GCodePathConfig("inset0Config", "WALL-OUTER", config.PerimeterAcceleration) { DoSeamHiding = true };
			insetXConfig = new GCodePathConfig("insetXConfig", "WALL-INNER", config.PerimeterAcceleration);
			sparseFillConfig = new GCodePathConfig("fillConfig", "FILL", config.DefaultAcceleration) { ClosedLoop = false };
			solidFillConfig = new GCodePathConfig("fillConfig", "FILL", config.DefaultAcceleration) { ClosedLoop = false, LiftOnTravel = true };
			topFillConfig = new GCodePathConfig("topFillConfig", "TOP-FILL", config.DefaultAcceleration) { ClosedLoop = false, LiftOnTravel = true };
			firstTopFillConfig = new GCodePathConfig("firstTopFillConfig", "FIRST-TOP-Fill", config.DefaultAcceleration) { ClosedLoop = false };
			bottomFillConfig = new GCodePathConfig("bottomFillConfig", "BOTTOM-FILL", config.DefaultAcceleration) { ClosedLoop = false, LiftOnTravel = true };
			airGappedBottomInsetConfig = new GCodePathConfig("airGappedBottomInsetConfig", "AIR-GAP-INSET", config.DefaultAcceleration);
			airGappedBottomConfig = new GCodePathConfig("airGappedBottomConfig", "AIR-GAP", config.DefaultAcceleration) { ClosedLoop = false };
			bridgeConfig = new GCodePathConfig("bridgeConfig", "BRIDGE", config.DefaultAcceleration);
			supportNormalConfig = new GCodePathConfig("supportNormalConfig", "SUPPORT", config.DefaultAcceleration);
			supportInterfaceConfig = new GCodePathConfig("supportInterfaceConfig", "SUPPORT-INTERFACE", config.DefaultAcceleration);

			using (new QuickTimer2Report("All Layers"))
			{
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

					lock (locker)
					{
						LogOutput.Log("Writing Layers {0}/{1}\n".FormatWith(layerWriten, totalLayers));

						LogOutput.logProgress("export", layerWriten, totalLayers);
						layerWriten++;
					}

					if (layerIndex < config.NumberOfFirstLayers)
					{
						skirtConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
						inset0Config.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
						insetXConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);

						sparseFillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
						solidFillConfig.SetData(config.FirstLayerSpeed, config.FirstLayerExtrusionWidth_um);
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

						sparseFillConfig.SetData(config.InfillSpeed, config.ExtrusionWidth_um);
						solidFillConfig.SetData(config.InfillSpeed, config.ExtrusionWidth_um);
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

					var layerPlanner = new LayerGCodePlanner(config, gcodeExport, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
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
					layerPlanner.QueueFanCommand(0, sparseFillConfig, true);

					// hold the current layer path finder
					PathFinder layerPathFinder = null;

					var supportExturderIndex2 = config.SupportExtruder < config.ExtruderCount ? config.SupportExtruder : 0;
					var interfaceExturderIndex = config.SupportInterfaceExtruder < config.ExtruderCount ? config.SupportInterfaceExtruder : 0;

					// Loop over extruders in preferred order
					var activeThenOtherExtruders = layerDataStorage.Extruders.CurrentThenOtherIndexes(activeExtruderIndex: layerPlanner.GetExtruder());
					foreach (int extruderIndex in activeThenOtherExtruders)
					{
						if (config.AvoidCrossingPerimeters
							&& layerDataStorage.Extruders != null
							&& layerDataStorage.Extruders.Count > extruderIndex)
						{
							// set the path planner to avoid islands
							layerPathFinder = layerDataStorage.Extruders[extruderIndex].Layers[layerIndex].PathFinder;
						}

						ChangeExtruderIfRequired(layerDataStorage, layerPathFinder, layerIndex, layerPlanner, extruderIndex, false);

						using (new QuickTimer2Report("CreateRequiredInsets"))
						{
							// create the insets required by this extruder layer
							layerDataStorage.CreateRequiredInsets(config, layerIndex, extruderIndex);
						}

						if (layerIndex == 0)
						{
							QueueExtruderLayerToGCode(layerDataStorage, layerPlanner, extruderIndex, layerIndex, config.FirstLayerExtrusionWidth_um);
						}
						else 
						{
							QueueExtruderLayerToGCode(layerDataStorage, layerPlanner, extruderIndex, layerIndex, config.ExtrusionWidth_um);
						}

						if (layerDataStorage.Support != null)
						{
							bool movedToIsland = false;

							layerPathFinder = layerDataStorage.Extruders[extruderIndex].Layers[layerIndex].PathFinder;

							if ((supportExturderIndex2 <= 0 && extruderIndex == 0)
								|| supportExturderIndex2 == extruderIndex)
							{
								if (layerDataStorage.Support.QueueNormalSupportLayer(config, layerPlanner, layerIndex, supportNormalConfig))
								{
									// we move out of the island so we aren't in it.
									islandCurrentlyInside = null;
								}
							}

							if ((interfaceExturderIndex <= 0 && extruderIndex == 0)
								|| interfaceExturderIndex == extruderIndex)
							{
								if (layerDataStorage.Support.QueueInterfaceSupportLayer(config, layerPlanner, layerIndex, supportInterfaceConfig))
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

					if (layerDataStorage.Support != null)
					{
						z += config.SupportAirGap_um;
						gcodeExport.CurrentZ_um = z;

						bool extrudedAirGappedSupport = false;
						foreach (int extruderIndex in layerDataStorage.Extruders.CurrentThenOtherIndexes(activeExtruderIndex: layerPlanner.GetExtruder()))
						{
							QueueAirGappedExtruderLayerToGCode(layerDataStorage, layerPathFinder, layerPlanner, extruderIndex, layerIndex, config.ExtrusionWidth_um, z);
							if (!extrudedAirGappedSupport
								&& ((interfaceExturderIndex <= 0 && extruderIndex == 0) || interfaceExturderIndex == extruderIndex))
							{
								if (layerDataStorage.Support.HaveAirGappedBottomLayer(layerIndex))
								{
									ChangeExtruderIfRequired(layerDataStorage, layerPathFinder, layerIndex, layerPlanner, extruderIndex, true);
								}

								layerDataStorage.Support.QueueAirGappedBottomLayer(config, layerPlanner, layerIndex, airGappedBottomConfig, layerPathFinder);
								extrudedAirGappedSupport = true;
							}
						}

						// don't print the wipe tower with air gap height
						z -= config.SupportAirGap_um;
						gcodeExport.CurrentZ_um = z;
					}

					if (layerDataStorage.EnsureWipeTowerIsSolid(layerIndex, layerPathFinder, layerPlanner, sparseFillConfig, config))
					{
						islandCurrentlyInside = null;
					}

					long currentLayerThickness_um = config.LayerThickness_um;
					if (layerIndex <= 0)
					{
						currentLayerThickness_um = config.FirstLayerThickness_um;
					}

					layerPlanner.FinalizeLayerFanSpeeds(layerIndex);
					using (new QuickTimer2Report("WriteQueuedGCode"))
					{
						layerPlanner.WriteQueuedGCode(currentLayerThickness_um);
					}
				}
			}

			LogOutput.Log("Wrote layers: {0:0.00}s.\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
			gcodeExport.WriteFanCommand(0);

			if (MatterSlice.Canceled)
			{
				return;
			}

			gcodeExport.WriteLine("; MatterSlice Completed Successfully");

			// Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
			maxObjectHeight = Math.Max(maxObjectHeight, layerDataStorage.modelSize.Z);

			QuickTimer2Report.Report();
		}

		private void ChangeExtruderIfRequired(LayerDataStorage layerDataStorage,
			PathFinder layerPathFinder,
			int layerIndex,
			LayerGCodePlanner layerGcodePlanner,
			int extruderIndex,
			bool airGapped)
		{
			bool extruderUsedForSupport = layerDataStorage.Support != null
				&& ((layerDataStorage.Support.SparseSupportOutlines[layerIndex].Count > 0 && config.SupportExtruder == extruderIndex)
					|| (layerDataStorage.Support.InterfaceLayers[layerIndex].Count > 0 && config.SupportInterfaceExtruder == extruderIndex));

			bool extruderUsedForParts = extruderIndex < layerDataStorage.Extruders.Count
				&& layerDataStorage.Extruders[extruderIndex].Layers[layerIndex].Islands.Count > 0;

			bool extruderUsedForWipeTower = extruderUsedForParts
				&& layerDataStorage.HaveWipeTower(config, layerIndex);

			if ((extruderUsedForSupport
				|| extruderUsedForWipeTower
				|| extruderUsedForParts)
				&& layerGcodePlanner.ToolChangeRequired(extruderIndex))
			{
				// make sure that any moves we make while doing wiping are planned around the parts on the bed
				if (config.AvoidCrossingPerimeters)
				{
					// and forget that we are in any island
					islandCurrentlyInside = null;
				}

				if (extruderUsedForWipeTower
					|| (layerDataStorage.HaveWipeTower(config, layerIndex)
						&& extruderUsedForSupport))
				{
					int prevExtruder = layerGcodePlanner.GetExtruder();

					if (layerIndex > 0)
					{
						PathFinder pathFinder = null;
						if (config.AvoidCrossingPerimeters)
						{
							// set the path planner to avoid islands
							pathFinder = layerDataStorage.Extruders[extruderIndex].Layers[layerIndex].PathFinder;
						}

						// make sure we path plan our way to the wipe tower before switching extruders
						layerGcodePlanner.QueueTravel(layerDataStorage.WipeCenter_um, pathFinder, sparseFillConfig.LiftOnTravel);
						islandCurrentlyInside = null;
					}

					// then change extruders
					layerGcodePlanner.SetExtruder(extruderIndex);

					DoSkirtAndBrim(layerDataStorage, layerPathFinder, layerIndex, layerGcodePlanner, extruderIndex, extruderUsedForSupport);

					if (layerDataStorage.PrimeOnWipeTower(layerIndex, layerGcodePlanner, layerPathFinder, sparseFillConfig, config, airGapped))
					{
						islandCurrentlyInside = null;
					}
				}
				else if (extruderIndex < layerDataStorage.Extruders.Count)
				{
					// then change extruders
					layerGcodePlanner.SetExtruder(extruderIndex);

					DoSkirtAndBrim(layerDataStorage, layerPathFinder, layerIndex, layerGcodePlanner, extruderIndex, extruderUsedForSupport);
				}
			}
			else
			{
				DoSkirtAndBrim(layerDataStorage, layerPathFinder, layerIndex, layerGcodePlanner, extruderIndex, extruderUsedForSupport);
			}
		}

		private bool havePrintedBrims = false;

		private void DoSkirtAndBrim(LayerDataStorage layerDataStorage, PathFinder layerPathFinder, int layerIndex, LayerGCodePlanner layerGcodePlanner, int extruderIndex, bool extruderUsedForSupport)
		{
			// if we are on layer 0 we still need to print the skirt and brim
			if (layerIndex == 0)
			{
				var extrudersInLayer0 = this.ExtrudersUsedInLayer0(config, layerDataStorage);

				var extruderUsed = extruderIndex >= 0
					&& extruderIndex < layerDataStorage.Extruders.Count
					&& extrudersInLayer0.Contains(extruderIndex);

				if (!config.ShouldGenerateRaft()
					&& (extruderUsed || extruderUsedForSupport))
				{
					QueueSkirtToGCode(layerDataStorage, layerPathFinder, layerGcodePlanner, layerIndex, extruderIndex);

					// we don't print a brim if we have a raft
					if (!havePrintedBrims)
					{
						QueueBrimsToGCode(layerDataStorage, layerGcodePlanner, layerIndex);
						havePrintedBrims = true;
					}
				}
			}
			else if (layerIndex < config.NumberOfBrimLayers)
			{
				layerGcodePlanner.QueuePolygonsByOptimizer(layerDataStorage.Brims, null, skirtConfig, layerIndex);
			}
		}

		private void QueueSkirtToGCode(LayerDataStorage layerDataStorage, PathFinder layerPathFinder, LayerGCodePlanner gcodeLayer, int layerIndex, int extruderIndex)
		{
			var extrudersInLayer0 = this.ExtrudersUsedInLayer0(config, layerDataStorage).Count();

			var loopsPerExtruder = layerDataStorage.Skirt.Count / extrudersInLayer0;

			// Single extruder in layer 0 - print all on target extruder
			if (extrudersInLayer0 == 1)
			{
				for (int i = loopsPerExtruder - 1; i >= 0; i--)
				{
					gcodeLayer.QueuePolygonByOptimizer(layerDataStorage.Skirt[i], layerPathFinder, skirtConfig, layerIndex);
				}

				return;
			}

			// Multiple extruders in layer 0 - split up skirt loops
			var loopIndex = loopsPerExtruder * (config.ExtruderCount - 1 - extruderIndex);

			for (int i = loopsPerExtruder - 1; i >= 0; i--)
			{
				var index = loopIndex + i;
				if (index < layerDataStorage.Skirt.Count)
				{
					gcodeLayer.QueuePolygonByOptimizer(layerDataStorage.Skirt[index], layerPathFinder, skirtConfig, layerIndex);
				}
			}
		}

		private HashSet<int> ExtrudersUsedInLayer0(ConfigSettings config, LayerDataStorage layerDataStorage)
		{
			var layer0Extruders = new List<int>();
			for (int i = 0; i < layerDataStorage.Extruders.Count; i++)
			{
				if (layerDataStorage.Extruders[i].UsedInLayer(0))
				{
					layer0Extruders.Add(i);
				}
			}

			var usedExtruders = new HashSet<int>(layer0Extruders);

			if (layerDataStorage.Support != null)
			{
				// Add interface layer extruder from layer 0
				if (layerDataStorage.Support.InterfaceLayers.Any() == true
					&& layerDataStorage.Support.InterfaceLayers[0].Any())
				{
					usedExtruders.Add(config.SupportInterfaceExtruder);
				}

				// Add support extruders from layer 0
				if (layerDataStorage.Support.SparseSupportOutlines.Any()
					&& layerDataStorage.Support.SparseSupportOutlines[0].Any())
				{
					usedExtruders.Add(config.SupportExtruder);
				}
			}

			// Add raft extruders from layer 0
			if (layerDataStorage.raftOutline.Any()
				&& layerDataStorage.raftOutline[0].Any())
			{
				usedExtruders.Add(config.RaftExtruder);
			}

			if (layerDataStorage.Brims.Any()
				&& layerDataStorage.Brims[0].Any())
			{
				usedExtruders.Add(config.BrimExtruder);
			}

			return usedExtruders;
		}

		private void QueueBrimsToGCode(LayerDataStorage layerDataStorage, LayerGCodePlanner gcodeLayer, int layerIndex)
		{
			if (!gcodeLayer.LastPositionSet)
			{
				var maxPoint = new IntPoint(long.MinValue, long.MinValue);
				foreach (var polygon in layerDataStorage.Brims)
				{
					foreach (var point in polygon)
					{
						if (point.X >= maxPoint.X)
						{
							if (point.X > maxPoint.X)
							{
								maxPoint = point;
							}
							else if (point.Y > maxPoint.Y)
							{
								maxPoint = point;
							}
						}
					}
				}

				if (maxPoint.X > long.MinValue
					&& maxPoint.Y > long.MinValue)
				{
					// before we start the brim make sure we are printing from the outside in
					gcodeLayer.QueueTravel(maxPoint, null, sparseFillConfig.LiftOnTravel);
				}
			}

			if (config.BrimExtruder >= 0)
			{
				// if we have a specified brim extruder use it
				gcodeLayer.SetExtruder(config.BrimExtruder);
			}


			gcodeLayer.QueuePolygonsByOptimizer(layerDataStorage.Brims, null, skirtConfig, layerIndex);
		}

		private LayerIsland islandCurrentlyInside = null;
		private int layerWriten = 1;
		private object locker = new object();

		// Add a single layer from a single extruder to the GCode
		private void QueueExtruderLayerToGCode(LayerDataStorage layerDataStorage,
			LayerGCodePlanner layerGcodePlanner,
			int extruderIndex,
			int layerIndex,
			long extrusionWidth_um)
		{
			if (extruderIndex > layerDataStorage.Extruders.Count - 1)
			{
				return;
			}

			SliceLayer layer = layerDataStorage.Extruders[extruderIndex].Layers[layerIndex];

			if (layer.AllOutlines.Count == 0
				&& config.WipeShieldDistanceFromObject == 0)
			{
				layerGcodePlanner.QueueTravel(layerGcodePlanner.LastPosition_um, null, false);
				// don't do anything on this layer
				return;
			}

			// the wipe shield is only generated one time per layer
			if (layerDataStorage.WipeShield.Count > 0
				&& layerDataStorage.WipeShield[layerIndex].Count > 0
				&& layerDataStorage.Extruders.Count > 1)
			{
				layerGcodePlanner.ForceRetract();
				layerGcodePlanner.QueuePolygonsByOptimizer(layerDataStorage.WipeShield[layerIndex], layer.PathFinder, skirtConfig, layerIndex);
				layerGcodePlanner.ForceRetract();
				// remember that we have already laid down the wipe shield by clearing the data for this layer
				layerDataStorage.WipeShield[layerIndex].Clear();
			}

			var islandOrderOptimizer = new PathOrderOptimizer(config);
			for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				if ((config.ContinuousSpiralOuterPerimeter
					&& islandIndex > 0)
					|| layer.Islands[islandIndex].InsetToolPaths.Count == 0
					|| (layer.Islands[islandIndex].InsetToolPaths.Count == 1
						&& layer.Islands[islandIndex].InsetToolPaths[0].Count == 1
						&& layer.Islands[islandIndex].InsetToolPaths[0][0].Count == 0))
				{
					continue;
				}

				islandOrderOptimizer.AddPolygon(layer.Islands[islandIndex].InsetToolPaths[0][0], islandIndex);
			}

			islandOrderOptimizer.Optimize(layerGcodePlanner.LastPosition_um, layer.PathFinder, layerIndex, false);

			for (int islandOrderIndex = 0; islandOrderIndex < islandOrderOptimizer.OptimizedPaths.Count; islandOrderIndex++)
			{
				if (config.ContinuousSpiralOuterPerimeter && islandOrderIndex > 0)
				{
					continue;
				}

				LayerIsland island = layer.Islands[islandOrderOptimizer.OptimizedPaths[islandOrderIndex].SourcePolyIndex];

				if (config.AvoidCrossingPerimeters)
				{
					MoveToIsland(layerGcodePlanner, layer, island);
				}
				else
				{
					if (config.RetractWhenChangingIslands
						&& !InsideIsland(island, layerGcodePlanner)
						&& islandOrderOptimizer.OptimizedPaths.Count > 1)
					{
						layerGcodePlanner.ForceRetract();
					}
				}

				var sparseFillPolygons = new Polygons();
				var solidFillPolygons = new Polygons();
				var topFillPolygons = new Polygons();
				var firstTopFillPolygons = new Polygons();
				var bridgePolygons = new Polygons();
				var bridgeAreas = new Polygons();

				var bottomFillPolygons = new Polygons();

				layerDataStorage.CalculateInfillData(config,
					extruderIndex,
					layerIndex,
					island,
					bottomFillPolygons,
					sparseFillPolygons,
					solidFillPolygons,
					firstTopFillPolygons,
					topFillPolygons,
					bridgePolygons,
					bridgeAreas);

				if (config.GetNumberOfPerimeters() > 0)
				{
					if (config.ContinuousSpiralOuterPerimeter
						&& layerIndex >= config.NumberOfBottomLayers)
					{
						inset0Config.Spiralize = true;
					}

					if (bridgePolygons.Count > 0)
					{
						// turn it on for bridge (or keep it on)
						layerGcodePlanner.QueueFanCommand(config.BridgeFanSpeedPercent, bridgeConfig, false);
					}

					var insetToolPaths = island.InsetToolPaths;

					bool LayerHasSolidInfill()
					{
						return topFillPolygons.Count > 0
							|| bottomFillPolygons.Count > 0
							|| solidFillPolygons.Count > 0;
					}

					// If we are on the very first layer we always start with the outside so that we can stick to the bed better.
					{
						if (inset0Config.Spiralize)
						{
							if (insetToolPaths.Count > 0)
							{
								Polygon outsideSinglePolygon = insetToolPaths[0][0];
								layerGcodePlanner.QueuePolygonsByOptimizer(new Polygons() { outsideSinglePolygon }, null, inset0Config, layerIndex);
							}
						}
						else
                        {
                            var insetAccelerators = new List<QuadTree<int>>();
                            foreach (var inset in insetToolPaths)
                            {
                                insetAccelerators.Add(inset.GetQuadTree());
                            }

							var printInsideOut = NeedToPrintInsideOut(layerIndex);
							var insetsThatHaveBeenAdded = new HashSet<Polygon>();

                            var insetOrder = new List<(int perimeterIndex, int polyIndex, int pointIndex)>();
                            var foundAnyPath = true;
                            var lastPosition_um = layerGcodePlanner.LastPosition_um;
                            var first = true;
                            while (insetsThatHaveBeenAdded.Count < CountInsetsToPrint(insetToolPaths)
                                && foundAnyPath)
                            {
                                foundAnyPath = false;
                                bool limitDistance = false;
                                if (insetToolPaths.Count > 0)
                                {
                                    IntPoint nextOuterStart;
                                    if (first)
                                    {
                                        // only consider the actual outer polygon
                                        var firstPerimeter = new Polygons() { insetToolPaths[0][0] };
										nextOuterStart = FindBestPoint(firstPerimeter,
											firstPerimeter.GetQuadTree(),
											lastPosition_um,
											layerIndex,
											(poly) => !insetsThatHaveBeenAdded.Contains(poly));

										// check if we should prime before printing the outside perimeter (use 2 to be close but not touching)
										// TODO: remove this when we have close priming working
										var primingLoop = 2;
										if (!printInsideOut && insetToolPaths.Count > primingLoop)
                                        {
											AddClosestInset(insetOrder,
												ref nextOuterStart,
												insetToolPaths[primingLoop],
												insetAccelerators[primingLoop],
												primingLoop,
												insetsThatHaveBeenAdded,
												limitDistance,
												out bool foundAPath2);
										}

										first = false;
                                    }
                                    else
                                    {
                                        nextOuterStart = FindBestPoint(insetToolPaths[0], insetAccelerators[0], lastPosition_um, layerIndex, (poly) => !insetsThatHaveBeenAdded.Contains(poly));
                                    }

                                    if (!CloseToUnprintedPerimeter(insetToolPaths, insetsThatHaveBeenAdded, nextOuterStart))
                                    {
                                        if (insetToolPaths.Count > 1)
                                        {
                                            foreach (var poly in insetToolPaths[1].Where(p => !insetsThatHaveBeenAdded.Contains(p)))
                                            {
                                                if (poly.Count > 2)
                                                {
                                                    // try a spot along a 1 perimeter
                                                    var nextOuterStart2 = FindBestPoint(insetToolPaths[0], insetAccelerators[0], poly[poly.Count / 2], layerIndex, (poly2) => !insetsThatHaveBeenAdded.Contains(poly2));
                                                    if (CloseToUnprintedPerimeter(insetToolPaths, insetsThatHaveBeenAdded, nextOuterStart2))
                                                    {
                                                        nextOuterStart = nextOuterStart2;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    lastPosition_um = nextOuterStart;

                                    AddClosestInset(insetOrder,
                                        ref lastPosition_um,
                                        insetToolPaths[0],
                                        insetAccelerators[0],
                                        0,
                                        insetsThatHaveBeenAdded,
                                        limitDistance,
                                        out bool foundAPath);

                                    foundAnyPath |= foundAPath;
                                }

                                // Move to the closest inset 1 and print it
                                for (int insetIndex = 1; insetIndex < insetToolPaths.Count; insetIndex++)
                                {
                                    limitDistance = AddClosestInset(insetOrder,
                                        ref lastPosition_um,
                                        insetToolPaths[insetIndex],
                                        insetAccelerators[insetIndex],
                                        insetIndex,
                                        insetsThatHaveBeenAdded,
                                        limitDistance,
                                        out bool foundAPath);

                                    foundAnyPath |= foundAPath;
                                }
                            }

                            var liftOnTravel = false;
                            // if we are printing top layers and going to do z-lifting make sure we don't cross over the top layer while moving between islands
                            if (config.RetractionZHop > 0 && LayerHasSolidInfill())
                            {
                                liftOnTravel = true;
                                inset0Config.LiftOnTravel = true;
                                insetXConfig.LiftOnTravel = true;
                            }

                            void MoveInFromEdge(Polygon polygon)
                            {
                                // If we are on the outside perimeter move in before we travel (so we don't retract on the outside)
                                (int polyIndex, int pointIndex, IntPoint position) found2 = (-1, -1, layerGcodePlanner.LastPosition_um);
                                var distFromLastPoint = double.PositiveInfinity;
                                for (int findInsetIndex = insetToolPaths.Count - 1; findInsetIndex >= 1; findInsetIndex--)
                                {
                                    found2 = insetToolPaths[findInsetIndex].FindClosestPoint(layerGcodePlanner.LastPosition_um);
                                    if (found2.polyIndex != -1
                                        && found2.pointIndex != -1)
                                    {
                                        distFromLastPoint = (found2.position - layerGcodePlanner.LastPosition_um).Length();
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
                                    layerGcodePlanner.QueueTravel(found2.position, island.PathFinder, liftOnTravel, forceUniquePath: true);
                                }
                            }

                            // if we are printing from the inside out
                            if (printInsideOut)
                            {
                                insetOrder.Reverse();
                            }
							else
                            {
								// make sure the 
                            }

                            // queue the paths in order
                            for (int i = 0; i < insetOrder.Count; i++)
                            {
                                var perimeterIndex = insetOrder[i].perimeterIndex;
                                var pathConfig = perimeterIndex == 0 ? inset0Config : insetXConfig;
                                var polygonIndex = insetOrder[i].polyIndex;
                                var polygon = insetToolPaths[perimeterIndex][polygonIndex];
                                var pointIndex = insetOrder[i].pointIndex;

                                bool printedMerged = false;
                                if (config.MergeOverlappingLines)
                                {
                                    var mergedCount = 0;
                                    var polygonsToMerge = new Polygons() { polygon };
                                    // while the next perimeter is also part of our perimeter set
                                    while (i < insetOrder.Count - 1
                                        && insetOrder[i + 1].perimeterIndex == perimeterIndex)
                                    {
                                        i++;
                                        mergedCount++;
                                        polygonsToMerge.Add(insetToolPaths[perimeterIndex][insetOrder[i].polyIndex]);
                                    }

                                    var mergedPerimeters = polygonsToMerge.MergePerimeterOverlaps(pathConfig.LineWidth_um, true);
									if (mergedPerimeters?.Count > 1)
									{
										// delete any tiny segments
										for (int j = mergedPerimeters.Count - 1; j >= 0; j--)
										{
											if (mergedPerimeters[j].PolygonLength() < extrusionWidth_um * 2)
                                            {
												mergedPerimeters.RemoveAt(j);
                                            }
										}
									}
									
									if (mergedPerimeters?.Count > 1)
									{
										layerGcodePlanner.QueueTravel(polygon[pointIndex], island.PathFinder, pathConfig.LiftOnTravel);
                                        var closed = pathConfig.ClosedLoop;
                                        var hide = pathConfig.DoSeamHiding;
                                        pathConfig.ClosedLoop = false;
                                        pathConfig.DoSeamHiding = false;
                                        var singlePerimeters = new Polygons();
                                        foreach (var merged in mergedPerimeters)
                                        {
                                            for (int j = 1; j < merged.Count; j++)
                                            {
                                                singlePerimeters.Add(new Polygon() { merged[j - 1], merged[j] });
                                            }
                                        }

                                        QueuePolygonsConsideringSupport(layerIndex,
											island.PathFinder,
											layerGcodePlanner,
											singlePerimeters,
											pathConfig,
											SupportWriteType.UnsupportedAreas,
											layerGcodePlanner.QueuePolygonsByOptimizer,
											bridgeAreas);
                                        pathConfig.ClosedLoop = closed;
                                        pathConfig.DoSeamHiding = hide;
                                        printedMerged = true;
                                    }
                                    else
                                    {
                                        i -= mergedCount;
                                    }
                                }

                                if (!printedMerged)
                                {
                                    layerGcodePlanner.QueueTravel(polygon[pointIndex], island.PathFinder, pathConfig.LiftOnTravel);
                                    QueuePolygonsConsideringSupport(layerIndex,
										island.PathFinder,
										layerGcodePlanner,
										new Polygons() { polygon },
										pathConfig,
										SupportWriteType.UnsupportedAreas,
										layerGcodePlanner.QueuePolygonsByOptimizer,
										bridgeAreas);
                                }

                                if (printInsideOut && perimeterIndex == 0)
                                {
                                    MoveInFromEdge(polygon);
                                }
                            }

                            // reset the retraction distance
                            inset0Config.LiftOnTravel = false;
                            insetXConfig.LiftOnTravel = false;
                        }
                    }


					// Find the thin gaps for this layer and add them to the queue
					if (config.FillThinGaps && !config.ContinuousSpiralOuterPerimeter)
					{
						var thinGapPolygons = new Polygons();

						for (int perimeter = 0; perimeter < config.GetNumberOfPerimeters(); perimeter++)
						{
							if (island.IslandOutline.Offset(-extrusionWidth_um * (1 + perimeter)).FindThinLines(extrusionWidth_um + 2, extrusionWidth_um / 10, out Polygons thinLines, true))
							{
								thinGapPolygons.AddRange(thinLines);
							}
						}

						// add all fill polygons together so they can be optimized in one step
						sparseFillPolygons.AddRange(thinGapPolygons.ConvertToLines(false));
					}
				}

				// Write the bridge polygons after the perimeter so they will have more to hold to while bridging the gaps.
				// It would be even better to slow down the perimeters that are part of bridges but that is for later.
				if (bridgePolygons.Count > 0)
				{
					QueuePolygonsConsideringSupport(layerIndex,
						null,
						layerGcodePlanner,
						bridgePolygons,
						bridgeConfig,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsByOptimizer);
					// Set it back to what it was
					layerGcodePlanner.RestoreNormalFanSpeed(sparseFillConfig);
				}

				bool FillInSameDirection(Polygons existing, Polygons adding)
                {
					if (adding.Count > 0)
					{
						if (existing.Count > 0)
						{
							// check if they go in the same direction
							var ep_um = existing.GetPerpendicular().perpendicular;
							var ep = new Vector2(ep_um.X, ep_um.Y).GetNormal();
							var ap_um = adding.GetPerpendicular().perpendicular;
							var ap = new Vector2(ap_um.X, ap_um.Y).GetNormal();

							if (Math.Abs(ep.Dot(ap)) < .1)
                            {
								return false;
                            }
						}

						// there is only adding polygons, so we can add them
						return true;
					}

					// there are no polygons to add so don't
					return false;
				}

				// Put all of these segments into a list that can be queued together and still preserve their individual config settings.
				// This makes the total amount of travel while printing infill much less.
				if (topFillConfig.Speed == solidFillConfig.Speed)
				{
					// check that the lines go in the same direction before combining
					if (!config.MonotonicSolidInfill
						|| FillInSameDirection(solidFillPolygons, topFillPolygons))
					{
						// they are good to combine
						solidFillPolygons.AddRange(topFillPolygons);
						topFillPolygons.Clear();
					}
				}

				if (layerIndex > 0)
				{
					if (bottomFillConfig.Speed == solidFillConfig.Speed
						&& !config.MonotonicSolidInfill)
					{
						for (int i = bottomFillPolygons.Count - 1; i >= 0; i--)
						{
							if (bottomFillPolygons[i].PolygonLength() < config.TreatAsBridge_um)
							{
								if (config.MonotonicSolidInfill)
								{
									// check that the lines go in the same direction before combining
								}
								else // they are good to combine
								{
									solidFillPolygons.Add(bottomFillPolygons[i]);
									bottomFillPolygons.RemoveAt(i);
								}
							}
							else
							{
								bottomFillPolygons[i].SetSpeed(config.BridgeSpeed);
							}
						}
					}
					else
					{
						foreach (var polygon in bottomFillPolygons)
						{
							if (polygon.PolygonLength() > config.TreatAsBridge_um)
							{
								polygon.SetSpeed(config.BridgeSpeed);
							}
						}
					}
				}

				// sparse fill
				QueuePolygonsConsideringSupport(layerIndex,
					island.PathFinder,
					layerGcodePlanner,
					sparseFillPolygons,
					sparseFillConfig,
					SupportWriteType.UnsupportedAreas,
					layerGcodePlanner.QueuePolygonsByOptimizer);

				if (config.MonotonicSolidInfill)
				{
					// solid fill
					QueuePolygonsConsideringSupport(layerIndex,
						island.PathFinder,
						layerGcodePlanner,
						solidFillPolygons,
						solidFillConfig,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsMonotonic);

					// bottom layers
					QueuePolygonsConsideringSupport(layerIndex,
						island.PathFinder,
						layerGcodePlanner,
						bottomFillPolygons,
						bottomFillConfig,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsMonotonic);
				}
				else
				{
					// solid fill
					QueuePolygonsConsideringSupport(layerIndex,
						island.PathFinder,
						layerGcodePlanner,
						solidFillPolygons,
						solidFillConfig,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsByOptimizer);

					// bottom layers
					QueuePolygonsConsideringSupport(layerIndex,
						island.PathFinder,
						layerGcodePlanner,
						bottomFillPolygons,
						bottomFillConfig,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsByOptimizer);
				}

				if (firstTopFillPolygons.Count > 0)
				{
					if (config.BridgeOverInfill)
					{
						// turn it on for bridge (or keep it on)
						var topLayerBridgeSpeed = Math.Max(layerGcodePlanner.LastNormalFanPercent, config.BridgeFanSpeedPercent);
						layerGcodePlanner.QueueFanCommand(topLayerBridgeSpeed, bridgeConfig, false);
					}
					layerGcodePlanner.QueuePolygonsByOptimizer(firstTopFillPolygons, island.PathFinder, firstTopFillConfig, layerIndex);
					// Set it back to what it was
					layerGcodePlanner.RestoreNormalFanSpeed(sparseFillConfig);
				}

				if (config.MonotonicSolidInfill)
				{
					QueuePolygonsConsideringSupport(layerIndex,
						island.PathFinder,
						layerGcodePlanner,
						topFillPolygons,
						topFillConfig,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsMonotonic);
				}
				else
				{
					QueuePolygonsConsideringSupport(layerIndex,
						island.PathFinder,
						layerGcodePlanner,
						topFillPolygons,
						topFillConfig,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsByOptimizer);
				}
			}

			if (config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter)
			{
				// Collect all of the lines up to one third the extrusion diameter
				// string perimeterString = Newtonsoft.Json.JsonConvert.SerializeObject(island.IslandOutline);
				if (layer.AllOutlines.FindThinLines(extrusionWidth_um + 2, extrusionWidth_um / 3, out Polygons thinLines, true))
				{
					for (int polyIndex = thinLines.Count - 1; polyIndex >= 0; polyIndex--)
					{
						var polygon = thinLines[polyIndex];

						// remove any disconnected short segments
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

					var pathFinder = new PathFinder(layer.AllOutlines,
						inset0Config.LineWidth_um * 3 / 2,
						null,
						config.AvoidCrossingPerimeters,
						$"layer {layerIndex}");

					inset0Config.ClosedLoop = false;
					QueuePolygonsConsideringSupport(layerIndex,
						pathFinder,
						layerGcodePlanner,
						thinLines,
						inset0Config,
						SupportWriteType.UnsupportedAreas,
						layerGcodePlanner.QueuePolygonsByOptimizer);
					inset0Config.ClosedLoop = true;
				}
			}
		}

		/// <summary>
		/// Check if this islands perimeters need to print inside out.
		/// This will check both user prefs and layer requirments
		/// </summary>
		/// <param name="layerIndex"></param>
		/// <returns>If the perimeters should be printed inside out</returns>
        private bool NeedToPrintInsideOut(int layerIndex)
        {
			// If we are on the first layer or the user wants outside first
            if(config.OutsidePerimetersFirst || layerIndex == 0)
            {
				// if we are not generating support and the user wants outside in
				if (!config.GenerateSupport
					&& config.OutsidePerimetersFirst)
				{
					// Check if the current island reaches out too far from the previous layer
					var outerPerimeterWillBeInAir = false;
					if (outerPerimeterWillBeInAir)
                    {
						return true;
                    }
				}

				return false;
            }

			// If we have not found a reason to print outside in, we need to print inside out
			return true;
        }

        private bool InsideIsland(LayerIsland island, LayerGCodePlanner layerGcodePlanner)
		{
			return island.PathFinder?.OutlineData.Polygons.PointIsInside(layerGcodePlanner.LastPosition_um, island.PathFinder.OutlineData.EdgeQuadTrees, island.PathFinder.OutlineData.NearestNeighboursList) == true;
		}

		private void MoveToIsland(LayerGCodePlanner layerGcodePlanner, SliceLayer layer, LayerIsland island)
		{
			if (config.ContinuousSpiralOuterPerimeter)
			{
				return;
			}

			if (island.IslandOutline.Count > 0)
			{
				// If we are already in the island we are going to, don't go there, or there is only one island.
				if ((layer.Islands.Count == 1 && config.ExtruderCount == 1)
					|| layer.PathFinder?.OutlineData?.Polygons.Count < 3
					|| island.PathFinder?.OutlineData.Polygons.PointIsInside(layerGcodePlanner.LastPosition_um, island.PathFinder.OutlineData.EdgeQuadTrees, island.PathFinder.OutlineData.NearestNeighboursList) == true)
				{
					islandCurrentlyInside = island;
					return;
				}

				// retract if we need to
				if (config.RetractWhenChangingIslands
					&& !InsideIsland(island, layerGcodePlanner))
				{
					layerGcodePlanner.ForceRetract();
				}

				var (polyIndex, pointIndex, position) = island.IslandOutline.FindClosestPoint(layerGcodePlanner.LastPosition_um);
				IntPoint closestNextIslandPoint = island.IslandOutline[polyIndex][pointIndex];

				PathFinder pathFinder = null;
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
						pathFinder = islandCurrentlyInside.PathFinder;
						layerGcodePlanner.QueueTravel(closestLastIslandPoint, pathFinder, solidFillConfig.LiftOnTravel);
					}
				}

				// how far are we going to move
				var delta = closestNextIslandPoint - layerGcodePlanner.LastPosition_um;
				var moveDistance = delta.Length();
				if (layer.PathFinder != null
					&& moveDistance > layer.PathFinder.InsetAmount
					&& layerGcodePlanner.LastPositionSet)
				{
					// make sure we are not planning moves for the move away from the island
					// move away from our current island as much as the inset amount to avoid planning around where we are
					var awayFromIslandPosition = layerGcodePlanner.LastPosition_um + delta.Normal(layer.PathFinder.InsetAmount);
					layerGcodePlanner.QueueTravel(awayFromIslandPosition, null, solidFillConfig.LiftOnTravel);

					// let's move to this island avoiding running into any other islands
					pathFinder = layer.PathFinder;
					// find the closest point to where we are now
					// and do a move to there
				}

				// go to the next point
				layerGcodePlanner.QueueTravel(closestNextIslandPoint, pathFinder, solidFillConfig.LiftOnTravel);

				// and remember that we are now in the new island
				islandCurrentlyInside = island;
			}
		}

		public IntPoint FindBestPoint(Polygons boundaryPolygons, QuadTree<int> accelerator, IntPoint position, int layerIndex, Func<Polygon, bool> evaluatePolygon = null)
		{
			IntPoint polyPointPosition = new IntPoint(long.MinValue, long.MinValue);

			long bestDist = long.MaxValue;
			foreach (var polygonIndex in accelerator.IterateClosest(position, () => bestDist))
			{
				if (evaluatePolygon?.Invoke(boundaryPolygons[polygonIndex.Item1]) != false
					&& boundaryPolygons[polygonIndex.Item1] != null
					&& boundaryPolygons[polygonIndex.Item1].Count > 0)
				{
					var closestIndex = boundaryPolygons[polygonIndex.Item1].FindGreatestTurnIndex(layerIndex, 
						config.ExtrusionWidth_um,
						config.SeamPlacement,
						position);
					IntPoint closestToPoly = boundaryPolygons[polygonIndex.Item1][closestIndex];
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
			}

			if (bestDist < long.MaxValue)
			{
				return polyPointPosition;
			}

			return position;
		}

		private bool AddClosestInset(List<(int perimeterIndex, int polyIndex, int pointIndex)> insetOrder,
			ref IntPoint lastPosition_um,
			Polygons insetsToConsider,
			QuadTree<int> accelerator,
			int insetIndex,
			HashSet<Polygon> insetsThatHaveBeenAdded,
			bool limitDistance,
			out bool foundAPath)
		{
			foundAPath = false;

			// This is the furthest away we will accept a new starting point
			long maxDist_um = long.MaxValue;
			if (limitDistance)
			{
				// Make it relative to the size of the nozzle
				maxDist_um = config.ExtrusionWidth_um * 4;
			}

			int bestPolygonIndex = -1;
			int bestPointIndex = -1;
			var bestPoint = lastPosition_um;

			foreach (var closest in accelerator.IterateClosest(lastPosition_um, () => maxDist_um))
			{
				Polygon currentPolygon = insetsToConsider[closest.Item1];
				if (insetsThatHaveBeenAdded.Contains(currentPolygon))
				{
					continue;
				}

				int closentIndex = currentPolygon.FindClosestPositionIndex(lastPosition_um);
				if (closentIndex > -1)
				{
					long distance = (currentPolygon[closentIndex] - lastPosition_um).Length();
					if (distance < maxDist_um)
					{
						maxDist_um = distance;
						bestPolygonIndex = closest.Item1;
						bestPointIndex = closentIndex;
						bestPoint = currentPolygon[closentIndex];
						if (distance == 0)
						{
							break;
						}
					}
				}
				else
				{
					insetsThatHaveBeenAdded.Add(currentPolygon);
				}
			}

			if (bestPolygonIndex > -1)
			{
				insetOrder.Add((insetIndex, bestPolygonIndex, bestPointIndex));
				insetsThatHaveBeenAdded.Add(insetsToConsider[bestPolygonIndex]);
				lastPosition_um = bestPoint;
				foundAPath = maxDist_um != long.MaxValue;
				return false;
			}

			foundAPath = maxDist_um != long.MaxValue;

			// Return the original limitDistance value if we didn't match a polygon
			return limitDistance;
		}

		private int CountInsetsToPrint(List<Polygons> insetsToPrint)
		{
			var total = 0;
			foreach (Polygons polygons in insetsToPrint)
			{
				total += polygons.Count;
			}

			return total;
		}

		private bool CloseToUnprintedPerimeter(List<Polygons> insetsToPrint, HashSet<Polygon> printed, IntPoint testPosition)
		{
			if (insetsToPrint.Count > 1)
			{
				foreach (Polygon polygon in insetsToPrint[1])
				{
					if (!printed.Contains(polygon))
					{
						var closest = polygon.FindClosestPoint(testPosition);
						if ((closest.position - testPosition).Length() < config.ExtrusionWidth_um * 4)
						{
							return true;
						}
					}
				}
			}

			return false;
		}
	

		// Add a single layer from a single extruder to the GCode
		private void QueueAirGappedExtruderLayerToGCode(LayerDataStorage layerDataStorage, PathFinder layerPathFinder, LayerGCodePlanner layerGcodePlanner, int extruderIndex, int layerIndex, long extrusionWidth_um, long currentZ_um)
		{
			if (layerDataStorage.Support != null
				&& !config.ContinuousSpiralOuterPerimeter
				&& layerIndex > 0)
			{
				SliceLayer layer = layerDataStorage.Extruders[extruderIndex].Layers[layerIndex];

				var islandOrderOptimizer = new PathOrderOptimizer(config);
				for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
				{
					if (layer.Islands[islandIndex].InsetToolPaths.Count > 0)
					{
						islandOrderOptimizer.AddPolygon(layer.Islands[islandIndex].InsetToolPaths[0][0], islandIndex);
					}
				}

				islandOrderOptimizer.Optimize(layerGcodePlanner.LastPosition_um, layer.PathFinder, layerIndex, false);

				for (int islandOrderIndex = 0; islandOrderIndex < islandOrderOptimizer.OptimizedPaths.Count; islandOrderIndex++)
				{
					LayerIsland island = layer.Islands[islandOrderOptimizer.OptimizedPaths[islandOrderIndex].SourcePolyIndex];

					var bottomFillPolygons = new Polygons();
					
					layerDataStorage.CalculateInfillData(config, extruderIndex, layerIndex, island, bottomFillPolygons);

					// TODO: check if we are going to output anything
					bool outputDataForIsland = false;
					// Print everything but the first perimeter from the outside in so the little parts have more to stick to.
					for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count; insetIndex++)
					{
						outputDataForIsland |= QueuePolygonsConsideringSupport(layerIndex,
							null,
							layerGcodePlanner,
							island.InsetToolPaths[insetIndex],
							airGappedBottomInsetConfig,
							SupportWriteType.SupportedAreasCheckOnly,
							layerGcodePlanner.QueuePolygonsByOptimizer);
					}

					// then 0
					if (island.InsetToolPaths.Count > 0)
					{
						outputDataForIsland |= QueuePolygonsConsideringSupport(layerIndex,
							null,
							layerGcodePlanner,
							island.InsetToolPaths[0],
							airGappedBottomInsetConfig,
							SupportWriteType.SupportedAreasCheckOnly,
							layerGcodePlanner.QueuePolygonsByOptimizer);
					}

					outputDataForIsland |= QueuePolygonsConsideringSupport(layerIndex,
						null,
						layerGcodePlanner,
						bottomFillPolygons,
						airGappedBottomConfig,
						SupportWriteType.SupportedAreasCheckOnly,
						layerGcodePlanner.QueuePolygonsByOptimizer);

					if (outputDataForIsland)
					{
						ChangeExtruderIfRequired(layerDataStorage, layerPathFinder, layerIndex, layerGcodePlanner, extruderIndex, true);

						if (!config.AvoidCrossingPerimeters
							&& config.RetractWhenChangingIslands
							&& !InsideIsland(island, layerGcodePlanner))
						{
							layerGcodePlanner.ForceRetract();
						}

						MoveToIsland(layerGcodePlanner, layer, island);

						// Print everything but the first perimeter from the outside in so the little parts have more to stick to.
						for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count; insetIndex++)
						{
							QueuePolygonsConsideringSupport(layerIndex,
								island.PathFinder,
								layerGcodePlanner,
								island.InsetToolPaths[insetIndex],
								airGappedBottomInsetConfig,
								SupportWriteType.SupportedAreas,
								layerGcodePlanner.QueuePolygonsByOptimizer);
						}

						// then 0
						if (island.InsetToolPaths.Count > 0)
						{
							QueuePolygonsConsideringSupport(layerIndex,
								island.PathFinder,
								layerGcodePlanner,
								island.InsetToolPaths[0],
								airGappedBottomInsetConfig,
								SupportWriteType.SupportedAreas,
								layerGcodePlanner.QueuePolygonsByOptimizer);
						}

						QueuePolygonsConsideringSupport(layerIndex,
							island.PathFinder,
							layerGcodePlanner,
							bottomFillPolygons,
							airGappedBottomConfig,
							SupportWriteType.SupportedAreas,
							layerGcodePlanner.QueuePolygonsByOptimizer);
					}
				}
			}

			// gcodeLayer.SetPathFinder(null);
		}

		private enum SupportWriteType
		{
			UnsupportedAreas,
			SupportedAreas,
			SupportedAreasCheckOnly
		}

		/// <summary>
		/// Return true if any polygons are actually output.
		/// </summary>
		/// <param name="layerIndex"></param>
		/// <param name="gcodeLayer"></param>
		/// <param name="polygonsToWrite"></param>
		/// <param name="fillConfig"></param>
		/// <param name="supportWriteType"></param>
		/// <returns></returns>
		private bool QueuePolygonsConsideringSupport(int layerIndex,
			PathFinder pathFinder,
			LayerGCodePlanner gcodeLayer,
			Polygons polygonsToWrite,
			GCodePathConfig fillConfig,
			SupportWriteType supportWriteType,
			Func<Polygons, PathFinder, GCodePathConfig, int, bool> queueMethod,
			Polygons bridgeAreas = null)
		{
			bool polygonsWereOutput = false;

			if (layerDataStorage.Support != null
				&& layerIndex > 0
				&& !config.ContinuousSpiralOuterPerimeter)
			{
				Polygons supportOutlines = layerDataStorage.Support.GetRequiredSupportAreas(layerIndex).Offset(fillConfig.LineWidth_um / 2);

				if (supportWriteType == SupportWriteType.UnsupportedAreas)
				{
					if (supportOutlines.Count > 0)
					{
						// write segments at normal height (don't write segments needing air gap)
						Polygons polysToWriteAtNormalHeight = new Polygons();
						Polygons polysToWriteAtAirGapHeight = new Polygons();

						GetSegmentsConsideringSupport(polygonsToWrite, supportOutlines, polysToWriteAtNormalHeight, polysToWriteAtAirGapHeight, false, fillConfig.ClosedLoop);

						if (bridgeAreas != null
							&& bridgeAreas.Count > 0)
						{
							// We have some bridging happening trim our outlines against this and slow down the ones that are
							// crossing the bridge areas.
							var polygonsWithBridgeSlowdowns = ChangeSpeedOverBridgedAreas(polygonsToWrite, bridgeAreas, fillConfig.ClosedLoop);
							bool oldValue = fillConfig.ClosedLoop;
							fillConfig.ClosedLoop = false;
							polygonsWereOutput |= queueMethod(polygonsWithBridgeSlowdowns, pathFinder, fillConfig, layerIndex);
							fillConfig.ClosedLoop = oldValue;
						}
						else // there is no bridging so we do not need to slow anything down to cross gaps
						{
							polygonsWereOutput |= queueMethod(polysToWriteAtNormalHeight, pathFinder, fillConfig, layerIndex);
						}
					}
					else
					{
						polygonsWereOutput |= queueMethod(polygonsToWrite, pathFinder, fillConfig, layerIndex);
					}
				}
				else if (supportOutlines.Count > 0) // we are checking the supported areas
				{
					// detect and write segments at air gap height
					Polygons polysToWriteAtNormalHeight = new Polygons();
					Polygons polysToWriteAtAirGapHeight = new Polygons();

					GetSegmentsConsideringSupport(polygonsToWrite, supportOutlines, polysToWriteAtNormalHeight, polysToWriteAtAirGapHeight, true, fillConfig.ClosedLoop);

					if (supportWriteType == SupportWriteType.SupportedAreasCheckOnly)
					{
						polygonsWereOutput = polysToWriteAtAirGapHeight.Count > 0;
					}
					else
					{
						var closedLoop = fillConfig.ClosedLoop;
						fillConfig.ClosedLoop = false;
						polygonsWereOutput |= queueMethod(polysToWriteAtAirGapHeight, pathFinder, fillConfig, layerIndex);
						fillConfig.ClosedLoop = closedLoop;
					}
				}
			}
			else if (supportWriteType == SupportWriteType.UnsupportedAreas)
			{
				if (bridgeAreas != null
					&& bridgeAreas.Count > 0)
				{
					// We have some bridging happening trim our outlines against this and slow down the one that are
					// crossing the bridge areas.
					var polygonsWithBridgeSlowdowns = ChangeSpeedOverBridgedAreas(polygonsToWrite, bridgeAreas, fillConfig.ClosedLoop);
					// convert this back into a closed loop
					var stitched = polygonsWithBridgeSlowdowns.StitchPolygonsTogether();
					if (stitched.Count == 1)
					{
						polygonsWereOutput |= queueMethod(stitched, pathFinder, fillConfig, layerIndex);
					}
					else
					{
						bool oldValue = fillConfig.ClosedLoop;
						fillConfig.ClosedLoop = false;
						polygonsWereOutput |= queueMethod(polygonsWithBridgeSlowdowns, pathFinder, fillConfig, layerIndex);
						fillConfig.ClosedLoop = oldValue;
					}
				}
				else // there is no bridging so we do not need to slow anything down to cross gaps
				{
					polygonsWereOutput |= queueMethod(polygonsToWrite, pathFinder, fillConfig, layerIndex);
				}
			}

			return polygonsWereOutput;
		}

		private void GetSegmentsConsideringSupport(Polygons polygonsToWrite, Polygons supportOutlines, Polygons polysToWriteAtNormalHeight, Polygons polysToWriteAtAirGapHeight, bool forAirGap, bool closedLoop)
		{
			// make an expanded area to constrain our segments to
			Polygons maxSupportOutlines = supportOutlines.Offset(solidFillConfig.LineWidth_um * 2 + config.SupportXYDistance_um);

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

		private Polygons ChangeSpeedOverBridgedAreas(Polygons polygonsToWrite,
			Polygons bridgedAreas,
			bool closedLoop)
		{
			var polygonsWithBridgeSlowdowns = new Polygons();
			// make an expanded area to constrain our segments to
			Polygons bridgeAreaIncludingPerimeters = bridgedAreas.Offset((config.GetNumberOfPerimeters() + 1) * config.ExtrusionWidth_um);

			Polygons polygonsToWriteAsLines = PolygonsHelper.ConvertToLines(polygonsToWrite, closedLoop);

			Polygons polygonsIntersectSupport = bridgeAreaIncludingPerimeters.CreateLineIntersections(polygonsToWriteAsLines);
			// write the bottoms that are not sitting on supported areas
			if (polygonsIntersectSupport.Count == 0)
			{
				// these lines do not intersect the bridge area
				polygonsWithBridgeSlowdowns.AddRange(polygonsToWriteAsLines);
			}
			else
			{
				var polysToSolw = bridgeAreaIncludingPerimeters.CreateLineIntersections(polygonsToWriteAsLines);
				var stitched = polysToSolw.StitchPolygonsTogether();
				foreach (var polyToSolw in stitched)
				{
					var length = polyToSolw.PolygonLength(false);
					if (length > config.TreatAsBridge_um)
					{
						for (int i = 0; i < polyToSolw.Count; i++)
						{
							polyToSolw[i] = new IntPoint(polyToSolw[i])
							{
								Speed = config.BridgeSpeed
							};
						}
					}
				}

				polygonsWithBridgeSlowdowns.AddRange(stitched);
				polygonsWithBridgeSlowdowns.AddRange(bridgeAreaIncludingPerimeters.CreateLineDifference(polygonsToWriteAsLines));
			}

			return polygonsWithBridgeSlowdowns;
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