/*
This file is part of MatterSlice. A commandline utility for
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
using MatterSlice.ClipperLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MatterHackers.MatterSlice
{
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
		private SliceDataStorage slicingData = new SliceDataStorage();

		private GCodePathConfig skirtConfig = new GCodePathConfig();
		private GCodePathConfig inset0Config = new GCodePathConfig()
		{
			doSeamHiding = true,
		};
		private GCodePathConfig insetXConfig = new GCodePathConfig();
		private GCodePathConfig fillConfig = new GCodePathConfig();
		private GCodePathConfig topFillConfig = new GCodePathConfig();
		private GCodePathConfig bridgConfig = new GCodePathConfig();
		private GCodePathConfig supportNormalConfig = new GCodePathConfig();
		private GCodePathConfig supportInterfaceConfig = new GCodePathConfig();

		NewSupport newSupport = null;

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
			if (!gcode.IsOpened())
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
			optomizedMeshCollection.SetPositionAndSize(simpleMeshCollection, config.positionToPlaceObjectCenter_um.X, config.positionToPlaceObjectCenter_um.Y, -config.bottomClipAmount_um, config.centerObjectInXy);
			for (int volumeIndex = 0; volumeIndex < simpleMeshCollection.SimpleMeshes.Count; volumeIndex++)
			{
				LogOutput.Log("  Face counts: {0} . {1} {2:0.0}%\n".FormatWith((int)simpleMeshCollection.SimpleMeshes[volumeIndex].faceTriangles.Count, (int)optomizedMeshCollection.OptimizedMeshes[volumeIndex].facesTriangle.Count, (double)(optomizedMeshCollection.OptimizedMeshes[volumeIndex].facesTriangle.Count) / (double)(simpleMeshCollection.SimpleMeshes[volumeIndex].faceTriangles.Count) * 100));
				LogOutput.Log("  Vertex counts: {0} . {1} {2:0.0}%\n".FormatWith((int)simpleMeshCollection.SimpleMeshes[volumeIndex].faceTriangles.Count * 3, (int)optomizedMeshCollection.OptimizedMeshes[volumeIndex].vertices.Count, (double)(optomizedMeshCollection.OptimizedMeshes[volumeIndex].vertices.Count) / (double)(simpleMeshCollection.SimpleMeshes[volumeIndex].faceTriangles.Count * 3) * 100));
			}

			LogOutput.Log("Optimize model {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Reset();

			Stopwatch timeKeeperTotal = new Stopwatch();
			timeKeeperTotal.Start();
			preSetup(config.extrusionWidth_um);
			sliceModels(slicingData);

			processSliceData(slicingData);
			if (MatterSlice.Canceled)
			{
				return;
			}
			writeGCode(slicingData);
			if (MatterSlice.Canceled)
			{
				return;
			}

			LogOutput.logProgress("process", 1, 1); //Report to the GUI that a file has been fully processed.
			LogOutput.Log("Total time elapsed {0:0.00}s.\n".FormatWith(timeKeeperTotal.Elapsed.TotalSeconds));
		}

		public bool LoadStlFile(string input_filename)
		{
			preSetup(config.extrusionWidth_um);
			timeKeeper.Restart();
			LogOutput.Log("Loading {0} from disk...\n".FormatWith(input_filename));
			if (!SimpleMeshCollection.LoadModelFromFile(simpleMeshCollection, input_filename, config.modelRotationMatrix))
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

			gcode.Finalize(maxObjectHeight, config.travelSpeed, config.endCode);

			gcode.Close();
		}

		private void preSetup(int extrusionWidth)
		{
			skirtConfig.SetData(config.insidePerimetersSpeed, extrusionWidth, "SKIRT");
			inset0Config.SetData(config.outsidePerimeterSpeed, config.outsideExtrusionWidth_um, "WALL-OUTER");
			insetXConfig.SetData(config.insidePerimetersSpeed, extrusionWidth, "WALL-INNER");
			fillConfig.SetData(config.infillSpeed, extrusionWidth, "FILL", false);
			topFillConfig.SetData(config.topInfillSpeed, extrusionWidth, "TOP-FILL", false);
			bridgConfig.SetData(config.bridgeSpeed, extrusionWidth, "BRIDGE");
			supportNormalConfig.SetData(config.supportMaterialSpeed, extrusionWidth, "SUPPORT");
			supportInterfaceConfig.SetData(config.supportMaterialSpeed - 5, extrusionWidth, "SUPPORT-INTERFACE");

			for (int extruderIndex = 0; extruderIndex < ConfigConstants.MAX_EXTRUDERS; extruderIndex++)
			{
				gcode.SetExtruderOffset(extruderIndex, config.extruderOffsets[extruderIndex], -config.zOffset_um);
			}

			gcode.SetOutputType(config.outputType);
			gcode.SetRetractionSettings(config.retractionOnTravel, config.retractionSpeed, config.retractionOnExtruderSwitch, config.minimumExtrusionBeforeRetraction, config.retractionZHop, config.wipeAfterRetraction, config.unretractExtraExtrusion);
			gcode.SetToolChangeCode(config.toolChangeCode);
		}

		private void sliceModels(SliceDataStorage slicingData)
		{
			timeKeeper.Restart();
#if false
            optomizedModel.saveDebugSTL("debug_output.stl");
#endif

			LogOutput.Log("Slicing model...\n");
			List<Slicer> slicerList = new List<Slicer>();
			for (int optimizedMeshIndex = 0; optimizedMeshIndex < optomizedMeshCollection.OptimizedMeshes.Count; optimizedMeshIndex++)
			{
				Slicer slicer = new Slicer(optomizedMeshCollection.OptimizedMeshes[optimizedMeshIndex], config);
				slicerList.Add(slicer);
			}

#if false
            slicerList[0].DumpSegmentsToGcode("Volume 0 Segments.gcode");
            slicerList[0].DumpPolygonsToGcode("Volume 0 Polygons.gcode");
            //slicerList[0].DumpPolygonsToHTML("Volume 0 Polygons.html");
#endif

			LogOutput.Log("Sliced model in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			LogOutput.Log("Generating support map...\n");
			slicingData.support.GenerateSupportGrid(optomizedMeshCollection, config);

			slicingData.modelSize = optomizedMeshCollection.size_um;
			slicingData.modelMin = optomizedMeshCollection.minXYZ_um;
			slicingData.modelMax = optomizedMeshCollection.maxXYZ_um;

			LogOutput.Log("Generating layer parts...\n");
			for (int partIndex = 0; partIndex < slicerList.Count; partIndex++)
			{
				slicingData.AllPartsLayers.Add(new PartLayers());
				LayerPart.CreateLayerParts(slicingData.AllPartsLayers[partIndex], slicerList[partIndex], config.repairOverlaps);

				if (config.enableRaft)
				{
					//Add the raft offset to each layer.
					for (int layerNr = 0; layerNr < slicingData.AllPartsLayers[partIndex].Layers.Count; layerNr++)
					{
						slicingData.AllPartsLayers[partIndex].Layers[layerNr].printZ += config.raftBaseThickness_um + config.raftInterfaceThicknes_um;
					}
				}
			}
			LogOutput.Log("Generated layer parts in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
		}

		private void processSliceData(SliceDataStorage slicingData)
		{
			if (config.continuousSpiralOuterPerimeter)
			{
				config.numberOfTopLayers = 0;
				config.infillPercent = 0;
			}

			MultiVolumes.RemoveVolumesIntersections(slicingData.AllPartsLayers);
			MultiVolumes.OverlapMultipleVolumesSlightly(slicingData.AllPartsLayers, config.multiVolumeOverlapPercent);
#if False
            LayerPart.dumpLayerparts(slicingData, "output.html");
#endif

            int totalLayers = slicingData.AllPartsLayers[0].Layers.Count;
#if DEBUG
			for (int volumeIndex = 1; volumeIndex < slicingData.AllPartsLayers.Count; volumeIndex++)
			{
				if (totalLayers != slicingData.AllPartsLayers[volumeIndex].Layers.Count)
				{
					throw new Exception("All the volumes must have the same number of layers (they just can have empty layers).");
				}
			}
#endif

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				for (int volumeIndex = 0; volumeIndex < slicingData.AllPartsLayers.Count; volumeIndex++)
				{
					if (MatterSlice.Canceled)
					{
						return;
					}
					int insetCount = config.numberOfPerimeters;
					if (config.continuousSpiralOuterPerimeter && (int)(layerIndex) < config.numberOfBottomLayers && layerIndex % 2 == 1)
					{
						//Add extra insets every 2 layers when spiralizing, this makes bottoms of cups watertight.
						insetCount += 5;
					}

					SliceLayerParts layer = slicingData.AllPartsLayers[volumeIndex].Layers[layerIndex];

					if (layerIndex == 0)
					{
						Inset.generateInsets(layer, config.firstLayerExtrusionWidth_um, config.firstLayerExtrusionWidth_um, insetCount);
					}
					else
					{
						Inset.generateInsets(layer, config.extrusionWidth_um, config.outsideExtrusionWidth_um, insetCount);
					}
				}
				LogOutput.Log("Creating Insets {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));
			}

			if (config.wipeShieldDistanceFromShapes_um > 0)
			{
				CreateWipeShields(slicingData, totalLayers);
			}

			LogOutput.Log("Generated inset in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				if (MatterSlice.Canceled)
				{
					return;
				}
				
				//Only generate bottom and top layers and infill for the first X layers when spiralize is choosen.
				if (!config.continuousSpiralOuterPerimeter || (int)(layerIndex) < config.numberOfBottomLayers)
				{
					for (int volumeIndex = 0; volumeIndex < slicingData.AllPartsLayers.Count; volumeIndex++)
					{
						if (layerIndex == 0)
						{
							TopsAndBottoms.GenerateTopAndBottom(layerIndex, slicingData.AllPartsLayers[volumeIndex], config.firstLayerExtrusionWidth_um, config.firstLayerExtrusionWidth_um, config.numberOfBottomLayers, config.numberOfTopLayers);
						}
						else
						{
							TopsAndBottoms.GenerateTopAndBottom(layerIndex, slicingData.AllPartsLayers[volumeIndex], config.extrusionWidth_um, config.outsideExtrusionWidth_um, config.numberOfBottomLayers, config.numberOfTopLayers);
						}
					}
				}
				LogOutput.Log("Creating Top & Bottom Layers {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));
			}
			LogOutput.Log("Generated top bottom layers in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();

			if (config.wipeTowerSize_um > 0)
			{
				Polygon p = new Polygon();
				slicingData.wipeTower.Add(p);
				p.Add(new IntPoint(slicingData.modelMin.x - 3000, slicingData.modelMax.y + 3000));
				p.Add(new IntPoint(slicingData.modelMin.x - 3000, slicingData.modelMax.y + 3000 + config.wipeTowerSize_um));
				p.Add(new IntPoint(slicingData.modelMin.x - 3000 - config.wipeTowerSize_um, slicingData.modelMax.y + 3000 + config.wipeTowerSize_um));
				p.Add(new IntPoint(slicingData.modelMin.x - 3000 - config.wipeTowerSize_um, slicingData.modelMax.y + 3000));

				slicingData.wipePoint = new IntPoint(slicingData.modelMin.x - 3000 - config.wipeTowerSize_um / 2, slicingData.modelMax.y + 3000 + config.wipeTowerSize_um / 2);
			}

			if (config.enableRaft)
			{
				Raft.GenerateRaftOutlines(slicingData, config.raftExtraDistanceAroundPart_um, config);

				Skirt.generateSkirt(slicingData,
					config.skirtDistance_um + config.raftBaseLineSpacing_um,
					config.raftBaseLineSpacing_um,
					config.numberOfSkirtLoops,
					config.skirtMinLength_um,
					config.raftBaseThickness_um, config);
			}
			else
			{
				Skirt.generateSkirt(slicingData,
					config.skirtDistance_um,
					config.firstLayerExtrusionWidth_um,
					config.numberOfSkirtLoops,
					config.skirtMinLength_um,
					config.firstLayerThickness_um, config);
			}
		}

		private void CreateWipeShields(SliceDataStorage slicingData, int totalLayers)
		{
			for (int layerNr = 0; layerNr < totalLayers; layerNr++)
			{
				Polygons wipeShield = new Polygons();
				for (int volumeIdx = 0; volumeIdx < slicingData.AllPartsLayers.Count; volumeIdx++)
				{
					for (int partNr = 0; partNr < slicingData.AllPartsLayers[volumeIdx].Layers[layerNr].parts.Count; partNr++)
					{
						wipeShield = wipeShield.CreateUnion(slicingData.AllPartsLayers[volumeIdx].Layers[layerNr].parts[partNr].TotalOutline.Offset(config.wipeShieldDistanceFromShapes_um));
					}
				}
				slicingData.wipeShield.Add(wipeShield);
			}

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				slicingData.wipeShield[layerIndex] = slicingData.wipeShield[layerIndex].Offset(-1000).Offset(1000);
			}

			int offsetAngle = (int)Math.Tan(60.0 * Math.PI / 180) * config.layerThickness_um;//Allow for a 60deg angle in the wipeShield.
			for (int layerNr = 1; layerNr < totalLayers; layerNr++)
			{
				slicingData.wipeShield[layerNr] = slicingData.wipeShield[layerNr].CreateUnion(slicingData.wipeShield[layerNr - 1].Offset(-offsetAngle));
			}

			for (int layerNr = totalLayers - 1; layerNr > 0; layerNr--)
			{
				slicingData.wipeShield[layerNr - 1] = slicingData.wipeShield[layerNr - 1].CreateUnion(slicingData.wipeShield[layerNr].Offset(-offsetAngle));
			}
		}

		private void writeGCode(SliceDataStorage slicingData)
		{
			gcode.WriteComment("filamentDiameter = {0}".FormatWith(config.filamentDiameter));
			gcode.WriteComment("extrusionWidth = {0}".FormatWith(config.extrusionWidth));
			gcode.WriteComment("firstLayerExtrusionWidth = {0}".FormatWith(config.firstLayerExtrusionWidth));
			gcode.WriteComment("layerThickness = {0}".FormatWith(config.layerThickness));
			gcode.WriteComment("firstLayerThickness = {0}".FormatWith(config.firstLayerThickness));

			if (fileNumber == 1)
			{
				if (gcode.GetOutputType() == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
				{
					gcode.WriteComment("TYPE:UltiGCode");
					gcode.WriteComment("TIME:<__TIME__>");
					gcode.WriteComment("MATERIAL:<FILAMENT>");
					gcode.WriteComment("MATERIAL2:<FILAMEN2>");
				}
				gcode.WriteCode(config.startCode);
			}
			else
			{
				gcode.WriteFanCommand(0);
				gcode.ResetExtrusionValue();
				gcode.WriteRetraction();
				gcode.setZ(maxObjectHeight + 5000);
				gcode.WriteMove(gcode.GetPosition(), config.travelSpeed, 0);
				gcode.WriteMove(new Point3(slicingData.modelMin.x, slicingData.modelMin.y, gcode.CurrentZ), config.travelSpeed, 0);
			}
			fileNumber++;

			int totalLayers = slicingData.AllPartsLayers[0].Layers.Count;
			// let's remove any of the layers on top that are empty
			{
				for (int layerIndex = totalLayers - 1; layerIndex >= 0; layerIndex--)
				{
					bool layerHasData = false;
					foreach (PartLayers currentVolume in slicingData.AllPartsLayers)
					{
						SliceLayerParts currentLayer = currentVolume.Layers[layerIndex];
						for (int partIndex = 0; partIndex < currentVolume.Layers[layerIndex].parts.Count; partIndex++)
						{
							SliceLayerPart currentPart = currentLayer.parts[partIndex];
							if (currentPart.TotalOutline.Count > 0)
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
			Raft.WriteRaftGCodeIfRequired(slicingData, config, gcode);

			if (config.useNewSupport)
			{
				newSupport = new NewSupport(config, slicingData.AllPartsLayers[0], 1);
			}

			int volumeIndex = 0;
			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				if (MatterSlice.Canceled)
				{
					return;
				}
				LogOutput.Log("Writing Layers {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));

				LogOutput.logProgress("export", layerIndex + 1, totalLayers);

				if (layerIndex == 0)
				{
					skirtConfig.SetData(config.firstLayerSpeed, config.firstLayerExtrusionWidth_um, "SKIRT");
					inset0Config.SetData(config.firstLayerSpeed, config.firstLayerExtrusionWidth_um, "WALL-OUTER");
					insetXConfig.SetData(config.firstLayerSpeed, config.firstLayerExtrusionWidth_um, "WALL-INNER");
					fillConfig.SetData(config.firstLayerSpeed, config.firstLayerExtrusionWidth_um, "FILL", false);
					bridgConfig.SetData(config.firstLayerSpeed, config.firstLayerExtrusionWidth_um, "BRIDGE");

					supportNormalConfig.SetData(config.firstLayerSpeed, config.supportExtrusionWidth_um, "SUPPORT");
					supportInterfaceConfig.SetData(config.firstLayerSpeed, config.extrusionWidth_um, "SUPPORT-INTERFACE");
				}
				else
				{
					skirtConfig.SetData(config.insidePerimetersSpeed, config.extrusionWidth_um, "SKIRT");
					inset0Config.SetData(config.outsidePerimeterSpeed, config.outsideExtrusionWidth_um, "WALL-OUTER");
					insetXConfig.SetData(config.insidePerimetersSpeed, config.extrusionWidth_um, "WALL-INNER");
					fillConfig.SetData(config.infillSpeed, config.extrusionWidth_um, "FILL", false);
					bridgConfig.SetData(config.bridgeSpeed, config.extrusionWidth_um, "BRIDGE");

					supportNormalConfig.SetData(config.supportMaterialSpeed, config.supportExtrusionWidth_um, "SUPPORT");
					supportInterfaceConfig.SetData(config.supportMaterialSpeed - 5, config.extrusionWidth_um, "SUPPORT-INTERFACE");
				}

				gcode.WriteComment("LAYER:{0}".FormatWith(layerIndex));
				if (layerIndex == 0)
				{
					gcode.SetExtrusion(config.firstLayerThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);
				}
				else
				{
					gcode.SetExtrusion(config.layerThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);
				}

				GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_um);

				// get the correct height for this layer
				int z = config.firstLayerThickness_um + layerIndex * config.layerThickness_um;
				if (config.enableRaft)
				{
					z += config.raftBaseThickness_um + config.raftInterfaceThicknes_um + config.raftSurfaceLayers * config.raftSurfaceThickness_um;
					if (layerIndex == 0)
					{
						// We only raise the first layer of the print up by the air gap.
						// To give it:
						//   Less press into the raft
						//   More time to cool
						//   more surface area to air while extruding
						z += config.raftAirGap_um;
					}
				}

				gcode.setZ(z);

				// We only create the skirt if we are on layer 0 and the first volume and there is no raft.
				if (layerIndex == 0 && volumeIndex == 0 && !Raft.ShouldGenerateRaft(config))
				{
					QueueSkirtToGCode(slicingData, gcodeLayer, volumeIndex, layerIndex);
				}

				bool printSupportFirst = (slicingData.support.generated && config.supportExtruder >= 0 && config.supportExtruder == gcodeLayer.getExtruder());
				if (printSupportFirst)
				{
					QueueSupportToGCode(slicingData, gcodeLayer, layerIndex, config);
				}

				if (newSupport != null)
				{
					newSupport.QueueNormalSupportLayer(config, gcodeLayer, layerIndex, supportNormalConfig, supportInterfaceConfig);
                }

				int fanSpeedPercent = GetFanSpeed(layerIndex, gcodeLayer);

				for (int volumeCnt = 0; volumeCnt < slicingData.AllPartsLayers.Count; volumeCnt++)
				{
					if (volumeCnt > 0)
					{
						volumeIndex = (volumeIndex + 1) % slicingData.AllPartsLayers.Count;
					}

					if (layerIndex == 0)
					{
						QueueVolumeLayerToGCode(slicingData, gcodeLayer, volumeIndex, layerIndex, config.firstLayerExtrusionWidth_um, fanSpeedPercent, z);
					}
					else
					{
						QueueVolumeLayerToGCode(slicingData, gcodeLayer, volumeIndex, layerIndex, config.extrusionWidth_um, fanSpeedPercent, z);
					}
				}

				if (!printSupportFirst)
				{
					QueueSupportToGCode(slicingData, gcodeLayer, layerIndex, config);
				}

				if (newSupport != null)
				{
					if (newSupport.HaveBottomLayers(layerIndex))
					{
						if (!config.enableRaft || layerIndex > 0)
						{
							z += config.raftAirGap_um;
							gcode.setZ(z);
						}

						newSupport.QueueAirGappedBottomLayer(gcodeLayer, layerIndex, supportNormalConfig, supportInterfaceConfig);
					}
				}

				//Finish the layer by applying speed corrections for minimum layer times.
				gcodeLayer.ForceMinimumLayerTime(config.minimumLayerTimeSeconds, config.minimumPrintingSpeed);

				gcode.WriteFanCommand(fanSpeedPercent);

				int currentLayerThickness_um = config.layerThickness_um;
				if (layerIndex <= 0)
				{
					currentLayerThickness_um = config.firstLayerThickness_um;
				}

				gcodeLayer.WriteQueuedGCode(currentLayerThickness_um);
			}

			LogOutput.Log("Wrote layers in {0:0.00}s.\n".FormatWith(timeKeeper.Elapsed.TotalSeconds));
			timeKeeper.Restart();
			gcode.TellFileSize();
			gcode.WriteFanCommand(0);

			//Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
			maxObjectHeight = Math.Max(maxObjectHeight, slicingData.modelSize.z);
		}

		private int GetFanSpeed(int layerIndex, GCodePlanner gcodeLayer)
		{
			int fanSpeedPercent = config.fanSpeedMinPercent;
			if (gcodeLayer.getExtrudeSpeedFactor() <= 50)
			{
				fanSpeedPercent = config.fanSpeedMaxPercent;
			}
			else
			{
				int n = gcodeLayer.getExtrudeSpeedFactor() - 50;
				fanSpeedPercent = config.fanSpeedMinPercent * n / 50 + config.fanSpeedMaxPercent * (50 - n) / 50;
			}

			if (layerIndex < config.firstLayerToAllowFan)
			{
				// Don't allow the fan below this layer
				fanSpeedPercent = 0;
			}
			return fanSpeedPercent;
		}

		private void QueueSkirtToGCode(SliceDataStorage slicingData, GCodePlanner gcodeLayer, int volumeIndex, int layerIndex)
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

		int lastPartIndex = 0;
		//Add a single layer from a single mesh-volume to the GCode
		private void QueueVolumeLayerToGCode(SliceDataStorage slicingData, GCodePlanner gcodeLayer, int volumeIndex, int layerIndex, int extrusionWidth_um, int fanSpeedPercent, long currentZ_um)
		{
			int prevExtruder = gcodeLayer.getExtruder();
			bool extruderChanged = gcodeLayer.SetExtruder(volumeIndex);

			SliceLayerParts layer = slicingData.AllPartsLayers[volumeIndex].Layers[layerIndex];
			if (extruderChanged)
			{
				addWipeTower(slicingData, gcodeLayer, layerIndex, prevExtruder, extrusionWidth_um);
			}

			if (slicingData.wipeShield.Count > 0 && slicingData.AllPartsLayers.Count > 1)
			{
				gcodeLayer.SetAlwaysRetract(true);
				gcodeLayer.QueuePolygonsByOptimizer(slicingData.wipeShield[layerIndex], skirtConfig);
				gcodeLayer.SetAlwaysRetract(!config.avoidCrossingPerimeters);
			}

			PathOrderOptimizer partOrderOptimizer = new PathOrderOptimizer(new IntPoint());
			for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
			{
				if (config.continuousSpiralOuterPerimeter && partIndex > 0)
				{
					continue;
				}

				partOrderOptimizer.AddPolygon(layer.parts[partIndex].Insets[0][0]);
			}
			partOrderOptimizer.Optimize();

            List<Polygons> airGapBottomFillPolygons = new List<Polygons>(); ;

			for (int partIndex = 0; partIndex < partOrderOptimizer.bestPolygonOrderIndex.Count; partIndex++)
			{
				if (config.continuousSpiralOuterPerimeter && partIndex > 0)
				{
					continue;
				}

				SliceLayerPart part = layer.parts[partOrderOptimizer.bestPolygonOrderIndex[partIndex]];

				if (config.avoidCrossingPerimeters)
				{
					gcodeLayer.SetOuterPerimetersToAvoidCrossing(part.AvoidCrossingBoundery);
				}
				else
				{
					gcodeLayer.SetAlwaysRetract(true);
				}

				Polygons fillPolygons = new Polygons();
				Polygons bottomFillPolygons = new Polygons();
				Polygons topFillPolygons = new Polygons();
				Polygons bridgePolygons = new Polygons();

				CalculateInfillData(slicingData, volumeIndex, layerIndex, part, ref bottomFillPolygons, ref fillPolygons, ref topFillPolygons, ref bridgePolygons);

                airGapBottomFillPolygons.Add(bottomFillPolygons);

                // Write the bridge polgons out first so the perimeter will have more to hold to while bridging the gaps.
                // It would be even better to slow down the perimeters that are part of bridges but that is a bit harder.
                if (bridgePolygons.Count > 0)
				{
					gcode.WriteFanCommand(config.bridgeFanSpeedPercent);
					gcodeLayer.QueuePolygonsByOptimizer(bridgePolygons, bridgConfig);
				}

				if (config.numberOfPerimeters > 0)
				{
					if (partIndex != lastPartIndex)
					{
						// force a retract if changing islands
						gcodeLayer.ForceRetract();
						lastPartIndex = partIndex;
					}

					if (config.continuousSpiralOuterPerimeter)
					{
						if (layerIndex >= config.numberOfBottomLayers)
						{
							inset0Config.spiralize = true;
						}
					}

					// If we are on the very first layer we start with the outside so that we can stick to the bed better.
					if (config.outsidePerimetersFirst || layerIndex == 0 || inset0Config.spiralize)
					{
						// First the outside (this helps with accuracy)
						if (part.Insets.Count > 0)
						{
							QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, part.Insets[0], inset0Config, SupportWriteType.UnsuportedAreas);
						}

						if (!inset0Config.spiralize)
						{
							for (int perimeterIndex = 1; perimeterIndex < part.Insets.Count; perimeterIndex++)
							{
								QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, part.Insets[perimeterIndex], insetXConfig, SupportWriteType.UnsuportedAreas);
							}
						}
					}
					else // This is so we can do overhanges better (the outside can stick a bit to the inside).
					{
						// Figure out where the seam hiding start point is for inset 0 and move to that spot so
						// we have the minimum travel while starting inset 0 after printing the rest of the insets
						if (part?.Insets?[0]?[0]?.Count > 0)
						{
							int bestPoint = PathOrderOptimizer.GetBestEdgeIndex(part.Insets[0][0]);
							gcodeLayer.QueueTravel(part.Insets[0][0][bestPoint]);
						}

						// Print everything but the first perimeter from the outside in so the little parts have more to stick to.
						for (int perimeterIndex = 1; perimeterIndex < part.Insets.Count; perimeterIndex++)
						{
							QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, part.Insets[perimeterIndex], insetXConfig, SupportWriteType.UnsuportedAreas);
						}
						// then 0
						if (part.Insets.Count > 0)
						{
							QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, part.Insets[0], inset0Config, SupportWriteType.UnsuportedAreas);
						}
					}
				}

				gcodeLayer.QueuePolygonsByOptimizer(fillPolygons, fillConfig);

                QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, bottomFillPolygons, fillConfig, SupportWriteType.UnsuportedAreas);

				gcodeLayer.QueuePolygonsByOptimizer(topFillPolygons, topFillConfig);

				//After a layer part, make sure the nozzle is inside the comb boundary, so we do not retract on the perimeter.
				if (!config.continuousSpiralOuterPerimeter || layerIndex < config.numberOfBottomLayers)
				{
					gcodeLayer.MoveInsideTheOuterPerimeter(extrusionWidth_um * 2);
				}
			}

            if (true)
            {
                for (int partIndex = 0; partIndex < partOrderOptimizer.bestPolygonOrderIndex.Count; partIndex++)
                {
                    // Now write any areas that need to be on support at the air gap height
                    if (config.useNewSupport
                        && !config.continuousSpiralOuterPerimeter
                        && layerIndex > 0)
                    {
                        SliceLayerPart part = layer.parts[partOrderOptimizer.bestPolygonOrderIndex[partIndex]];

                        gcode.setZ(currentZ_um + config.raftAirGap_um);

                        // Print everything but the first perimeter from the outside in so the little parts have more to stick to.
                        for (int perimeterIndex = 1; perimeterIndex < part.Insets.Count; perimeterIndex++)
                        {
                            QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, part.Insets[perimeterIndex], insetXConfig, SupportWriteType.SupportedAreas);
                        }
                        // then 0
                        if (part.Insets.Count > 0)
                        {
                            QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, part.Insets[0], inset0Config, SupportWriteType.SupportedAreas);
                        }

                        QueuePolygonsConsideringSupport(layerIndex, gcodeLayer, airGapBottomFillPolygons[partIndex], fillConfig, SupportWriteType.SupportedAreas);
                    }
                }
            }

            gcodeLayer.SetOuterPerimetersToAvoidCrossing(null);
		}

		enum SupportWriteType { UnsuportedAreas, SupportedAreas };

		private void QueuePolygonsConsideringSupport(int layerIndex, GCodePlanner gcodeLayer, Polygons polygonsToWrite, GCodePathConfig fillConfig, SupportWriteType supportWriteType)
		{
			if (config.useNewSupport && layerIndex > 0)
			{
				if (supportWriteType == SupportWriteType.UnsuportedAreas)
				{
					// don't write the bottoms that are sitting on supported areas (they will be written at air gap distance later).
					Polygons polygonsNotOnSupport = polygonsToWrite.CreateDifference(newSupport.GetRequiredSupportAreas(layerIndex));
					gcodeLayer.QueuePolygonsByOptimizer(polygonsNotOnSupport, fillConfig);
				}
				else
				{
					Polygons supportOutlines = newSupport.GetRequiredSupportAreas(layerIndex);
					if (supportOutlines.Count > 0)
					{
						// write the bottoms that are sitting on supported areas.
						Polygons polygonsOnSupport;
						if (polygonsToWrite.Count == 1)
						{
							polygonsOnSupport = supportOutlines.CreateIntersection(polygonsToWrite);
						}
						else
						{
							polygonsOnSupport = supportOutlines.CreateLineIntersections(polygonsToWrite);
						}
						gcodeLayer.QueuePolygonsByOptimizer(polygonsOnSupport, fillConfig);
					}
				}
			}
			else if(supportWriteType == SupportWriteType.UnsuportedAreas)
            {
				gcodeLayer.QueuePolygonsByOptimizer(polygonsToWrite, fillConfig);
			}
		}

		private void CalculateInfillData(SliceDataStorage slicingData, int volumeIndex, int layerIndex, SliceLayerPart part, ref Polygons bottomFillPolygons, ref Polygons fillPolygons, ref Polygons topFillPolygons, ref Polygons bridgePolygons)
		{
			// generate infill the bottom layer including bridging
			foreach (Polygons outline in part.SolidBottomOutlines.CreateLayerOutlines(PolygonsHelper.LayerOpperation.EvenOdd))
			{
				if (layerIndex > 0)
				{
					double bridgeAngle;
					if (!config.useNewSupport && 
						Bridge.BridgeAngle(outline, slicingData.AllPartsLayers[volumeIndex].Layers[layerIndex - 1], out bridgeAngle))
					{
						Infill.GenerateLinePaths(outline, ref bridgePolygons, config.extrusionWidth_um, config.infillExtendIntoPerimeter_um, bridgeAngle);
					}
					else
					{
						Infill.GenerateLinePaths(outline, ref bottomFillPolygons, config.extrusionWidth_um, config.infillExtendIntoPerimeter_um, config.infillStartingAngle);
					}
				}
				else
				{
					Infill.GenerateLinePaths(outline, ref bottomFillPolygons, config.firstLayerExtrusionWidth_um, config.infillExtendIntoPerimeter_um, config.infillStartingAngle);
				}
			}

			// generate infill for the top layer
			foreach (Polygons outline in part.SolidTopOutlines.CreateLayerOutlines(PolygonsHelper.LayerOpperation.EvenOdd))
			{
				Infill.GenerateLinePaths(outline, ref topFillPolygons, config.extrusionWidth_um, config.infillExtendIntoPerimeter_um, config.infillStartingAngle);
			}

			// generate infill intermediate layers
			foreach (Polygons outline in part.SolidInfillOutlines.CreateLayerOutlines(PolygonsHelper.LayerOpperation.EvenOdd))
			{
				if (true) // use the old infill method
				{
					Infill.GenerateLinePaths(outline, ref fillPolygons, config.extrusionWidth_um, config.infillExtendIntoPerimeter_um, config.infillStartingAngle + 90 * (layerIndex % 2));
				}
				else // use the new concentric infill (not tested enough yet) have to handle some bad casses better
				{
					double oldInfillPercent = config.infillPercent;
					config.infillPercent = 100;
					Infill.GenerateConcentricInfill(config, outline, ref fillPolygons);
					config.infillPercent = oldInfillPercent;
				}
			}

			double fillAngle = config.infillStartingAngle;

			// generate the sparse infill for this part on this layer
			if (config.infillPercent > 0)
			{
				switch (config.infillType)
				{
					case ConfigConstants.INFILL_TYPE.LINES:
						if ((layerIndex & 1) == 1)
						{
							fillAngle += 90;
						}
						Infill.GenerateLineInfill(config, part.InfillOutlines, ref fillPolygons, fillAngle);
						break;

					case ConfigConstants.INFILL_TYPE.GRID:
						Infill.GenerateGridInfill(config, part.InfillOutlines, ref fillPolygons, fillAngle);
						break;

					case ConfigConstants.INFILL_TYPE.TRIANGLES:
						Infill.GenerateTriangleInfill(config, part.InfillOutlines, ref fillPolygons, fillAngle);
						break;

					case ConfigConstants.INFILL_TYPE.HEXAGON:
						Infill.GenerateHexagonInfill(config, part.InfillOutlines, ref fillPolygons, fillAngle, layerIndex);
						break;

					case ConfigConstants.INFILL_TYPE.CONCENTRIC:
						Infill.GenerateConcentricInfill(config, part.InfillOutlines, ref fillPolygons);
						break;

					default:
						throw new NotImplementedException();
				}
			}
		}

		private void QueueSupportToGCode(SliceDataStorage slicingData, GCodePlanner gcodeLayer, int layerIndex, ConfigSettings config)
		{
			if (!slicingData.support.generated)
			{
				return;
			}

			if (config.supportExtruder > -1)
			{
				int prevExtruder = gcodeLayer.getExtruder();
				if (gcodeLayer.SetExtruder(config.supportExtruder))
				{
					addWipeTower(slicingData, gcodeLayer, layerIndex, prevExtruder, config.extrusionWidth_um);
				}

				if (slicingData.wipeShield.Count > 0 && slicingData.AllPartsLayers.Count == 1)
				{
					gcodeLayer.SetAlwaysRetract(true);
					gcodeLayer.QueuePolygonsByOptimizer(slicingData.wipeShield[layerIndex], skirtConfig);
					gcodeLayer.SetAlwaysRetract(config.avoidCrossingPerimeters);
				}
			}

			int currentZHeight_um = config.firstLayerThickness_um;
			if (layerIndex == 0)
			{
				currentZHeight_um /= 2;
			}
			else
			{
				if (layerIndex > 1)
				{
					currentZHeight_um += (layerIndex - 1) * config.layerThickness_um;
				}
				currentZHeight_um += config.layerThickness_um / 2;
			}

			SupportPolyGenerator supportGenerator = new SupportPolyGenerator(slicingData.support, currentZHeight_um);

			QueueSupportPolygons(slicingData, gcodeLayer, layerIndex, config, supportGenerator.supportPolygons, SupportType.General);

			if (config.supportInterfaceExtruder != -1
				&& config.supportInterfaceExtruder != config.supportExtruder)
			{
				gcodeLayer.SetExtruder(config.supportInterfaceExtruder);
			}
			QueueSupportPolygons(slicingData, gcodeLayer, layerIndex, config, supportGenerator.interfacePolygons, SupportType.Interface);
		}

		private enum SupportType { General, Interface };

		private void QueueSupportPolygons(SliceDataStorage slicingData, GCodePlanner gcodeLayer, int layerIndex, ConfigSettings config, Polygons supportPolygons, SupportType interfaceLayer)
		{
			for (int volumeIndex = 0; volumeIndex < slicingData.AllPartsLayers.Count; volumeIndex++)
			{
				SliceLayerParts layer = slicingData.AllPartsLayers[volumeIndex].Layers[layerIndex];
				for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
				{
					supportPolygons = supportPolygons.CreateDifference(layer.parts[partIndex].TotalOutline.Offset(config.supportXYDistance_um));
				}
			}

			//Contract and expand the support polygons so small sections are removed and the final polygon is smoothed a bit.
			supportPolygons = supportPolygons.Offset(-config.extrusionWidth_um * 1);
			supportPolygons = supportPolygons.Offset(config.extrusionWidth_um * 1);

			List<Polygons> supportIslands = supportPolygons.CreateLayerOutlines(PolygonsHelper.LayerOpperation.EvenOdd);
			PathOrderOptimizer islandOrderOptimizer = new PathOrderOptimizer(gcode.GetPositionXY());

			for (int islandIndex = 0; islandIndex < supportIslands.Count; islandIndex++)
			{
				islandOrderOptimizer.AddPolygon(supportIslands[islandIndex][0]);
			}
			islandOrderOptimizer.Optimize();

			for (int islandIndex = 0; islandIndex < supportIslands.Count; islandIndex++)
			{
				Polygons island = supportIslands[islandOrderOptimizer.bestPolygonOrderIndex[islandIndex]];
				Polygons supportLines = new Polygons();
				if (config.supportLineSpacing_um > 0)
				{
					switch (interfaceLayer)
					{
						case SupportType.Interface:
							Infill.GenerateLineInfill(config, island, ref supportLines, config.supportInfillStartingAngle + 90, config.extrusionWidth_um);
							break;

						case SupportType.General:
							switch (config.supportType)
							{
								case ConfigConstants.SUPPORT_TYPE.GRID:
									Infill.GenerateGridInfill(config, island, ref supportLines, config.supportInfillStartingAngle, config.supportLineSpacing_um);
									break;

								case ConfigConstants.SUPPORT_TYPE.LINES:
									Infill.GenerateLineInfill(config, island, ref supportLines, config.supportInfillStartingAngle, config.supportLineSpacing_um);
									break;
							}
							break;

						default:
							throw new NotImplementedException();
					}
				}

				if (config.avoidCrossingPerimeters)
				{
					gcodeLayer.SetOuterPerimetersToAvoidCrossing(island);
				}

				switch (interfaceLayer)
				{
					case SupportType.Interface:
						gcodeLayer.QueuePolygonsByOptimizer(supportLines, supportInterfaceConfig);
						break;

					case SupportType.General:
						if (config.supportType == ConfigConstants.SUPPORT_TYPE.GRID)
						{
							gcodeLayer.QueuePolygonsByOptimizer(island, supportNormalConfig);
						}
						gcodeLayer.QueuePolygonsByOptimizer(supportLines, supportNormalConfig);
						break;

					default:
						throw new NotImplementedException();
				}

				gcodeLayer.SetOuterPerimetersToAvoidCrossing(null);
			}
		}

		private void addWipeTower(SliceDataStorage slicingData, GCodePlanner gcodeLayer, int layerNr, int prevExtruder, int extrusionWidth_um)
		{
			if (config.wipeTowerSize_um < 1)
			{
				return;
			}

			//If we changed extruder, print the wipe/prime tower for this nozzle;
			gcodeLayer.QueuePolygonsByOptimizer(slicingData.wipeTower, supportInterfaceConfig);
			Polygons fillPolygons = new Polygons();
			Infill.GenerateLinePaths(slicingData.wipeTower, ref fillPolygons, extrusionWidth_um, config.infillExtendIntoPerimeter_um, 45 + 90 * (layerNr % 2));
			gcodeLayer.QueuePolygonsByOptimizer(fillPolygons, supportInterfaceConfig);

			//Make sure we wipe the old extruder on the wipe tower.
			gcodeLayer.QueueTravel(slicingData.wipePoint - config.extruderOffsets[prevExtruder] + config.extruderOffsets[gcodeLayer.getExtruder()]);
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