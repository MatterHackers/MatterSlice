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

using System;
using System.Collections.Generic;
using System.Diagnostics;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    // Fused Filament Fabrication processor.
    public class fffProcessor
    {
        int maxObjectHeight;
        int fileNr;
        GCodeExport gcode = new GCodeExport();
        ConfigSettings config;
        Stopwatch timeKeeper = new Stopwatch();

        SimpleModel simpleModel = new SimpleModel();
        OptimizedModel optomizedModel;
        SliceDataStorage storage = new SliceDataStorage();

        GCodePathConfig skirtConfig = new GCodePathConfig();
        GCodePathConfig inset0Config = new GCodePathConfig();
        GCodePathConfig insetXConfig = new GCodePathConfig();
        GCodePathConfig fillConfig = new GCodePathConfig();
        GCodePathConfig bridgConfig = new GCodePathConfig();
        GCodePathConfig supportNormalConfig = new GCodePathConfig();
        GCodePathConfig supportInterfaceConfig = new GCodePathConfig();

        public fffProcessor(ConfigSettings config)
        {
            this.config = config;
            fileNr = 1;
            maxObjectHeight = 0;
        }

        public bool setTargetFile(string filename)
        {
            gcode.setFilename(filename);
            {
                gcode.writeComment("Generated with MatterSlice {0}".FormatWith(ConfigConstants.VERSION));
            }

            return gcode.isOpened();
        }

        public void DoProcessing()
        {
            if (!gcode.isOpened())
            {
                return;
            }

            timeKeeper.Restart();
            LogOutput.log("Analyzing and optimizing model...\n");
            optomizedModel = new OptimizedModel(simpleModel);
			if (MatterSlice.Canceled)
			{
				return;
			}
            optomizedModel.SetPositionAndSize(simpleModel, config.positionToPlaceObjectCenter_um.X, config.positionToPlaceObjectCenter_um.Y, -config.bottomClipAmount_um, config.centerObjectInXy);
            for (int volumeIndex = 0; volumeIndex < simpleModel.volumes.Count; volumeIndex++)
            {
                LogOutput.log("  Face counts: {0} . {1} {2:0.0}%\n".FormatWith((int)simpleModel.volumes[volumeIndex].faceTriangles.Count, (int)optomizedModel.volumes[volumeIndex].facesTriangle.Count, (double)(optomizedModel.volumes[volumeIndex].facesTriangle.Count) / (double)(simpleModel.volumes[volumeIndex].faceTriangles.Count) * 100));
                LogOutput.log("  Vertex counts: {0} . {1} {2:0.0}%\n".FormatWith((int)simpleModel.volumes[volumeIndex].faceTriangles.Count * 3, (int)optomizedModel.volumes[volumeIndex].vertices.Count, (double)(optomizedModel.volumes[volumeIndex].vertices.Count) / (double)(simpleModel.volumes[volumeIndex].faceTriangles.Count * 3) * 100));
            }

            LogOutput.log("Optimize model {0:0.0}s \n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Reset();

            Stopwatch timeKeeperTotal = new Stopwatch();
            timeKeeperTotal.Start();
            preSetup(config.extrusionWidth_um);
            sliceModels(storage);

            processSliceData(storage);
			if (MatterSlice.Canceled)
			{
				return;
			}
			writeGCode(storage);
			if (MatterSlice.Canceled)
			{
				return;
			}

            LogOutput.logProgress("process", 1, 1); //Report to the GUI that a file has been fully processed.
            LogOutput.log("Total time elapsed {0:0.00}s.\n".FormatWith(timeKeeperTotal.Elapsed.Seconds));
        }

        public bool LoadStlFile(string input_filename)
        {
            preSetup(config.extrusionWidth_um);
            timeKeeper.Restart();
            LogOutput.log("Loading {0} from disk...\n".FormatWith(input_filename));
            if (!SimpleModel.loadModelFromFile(simpleModel, input_filename, config.modelRotationMatrix))
            {
                LogOutput.logError("Failed to load model: {0}\n".FormatWith(input_filename));
                return false;
            }
            LogOutput.log("Loaded from disk in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            return true;
        }

        public void finalize()
        {
            if (!gcode.isOpened())
            {
                return;
            }

            gcode.finalize(maxObjectHeight, config.travelSpeed, config.endCode);

            gcode.Close();
        }

        void preSetup(int extrusionWidth)
        {
            skirtConfig.setData(config.insidePerimetersSpeed, extrusionWidth, "SKIRT");
            inset0Config.setData(config.outsidePerimeterSpeed, extrusionWidth, "WALL-OUTER");
            insetXConfig.setData(config.insidePerimetersSpeed, extrusionWidth, "WALL-INNER");
            fillConfig.setData(config.infillSpeed, extrusionWidth, "FILL", false);
            bridgConfig.setData(config.bridgeSpeed, extrusionWidth, "BRIDGE");
            supportNormalConfig.setData(config.supportMaterialSpeed, extrusionWidth, "SUPPORT");
            supportInterfaceConfig.setData(config.supportMaterialSpeed, extrusionWidth, "SUPPORT-INTERFACE");

            for (int extruderIndex = 0; extruderIndex < ConfigConstants.MAX_EXTRUDERS; extruderIndex++)
            {
                gcode.setExtruderOffset(extruderIndex, config.extruderOffsets[extruderIndex] * 1000, -config.zOffset_um);
            }

            gcode.SetOutputType(config.outputType);
            gcode.setRetractionSettings(config.retractionOnTravel, config.retractionSpeed, config.retractionOnExtruderSwitch, config.minimumExtrusionBeforeRetraction, config.retractionZHop);
        }

        void sliceModels(SliceDataStorage storage)
        {
            timeKeeper.Restart();
#if False
            optomizedModel.saveDebugSTL("debug_output.stl");
#endif

            LogOutput.log("Slicing model...\n");
            List<Slicer> slicerList = new List<Slicer>();
            for (int volumeIndex = 0; volumeIndex < optomizedModel.volumes.Count; volumeIndex++)
            {
                Slicer slicer = new Slicer(optomizedModel.volumes[volumeIndex], config);
                slicerList.Add(slicer);
            }

#if false
            slicerList[0].DumpSegmentsToGcode("Volume 0 Segments.gcode");
            slicerList[0].DumpPolygonsToGcode("Volume 0 Polygons.gcode");
            slicerList[0].DumpPolygonsToHTML("Volume 0 Polygons.html");
#endif

            LogOutput.log("Sliced model in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();

            LogOutput.log("Generating support map...\n");
            storage.support.GenerateSupportGrid(optomizedModel, config);

            storage.modelSize = optomizedModel.size_um;
            storage.modelMin = optomizedModel.minXYZ_um;
            storage.modelMax = optomizedModel.maxXYZ_um;

            LogOutput.log("Generating layer parts...\n");
            for (int volumeIndex = 0; volumeIndex < slicerList.Count; volumeIndex++)
            {
                storage.volumes.Add(new SliceVolumeStorage());
                LayerPart.createLayerParts(storage.volumes[volumeIndex], slicerList[volumeIndex], config.repairOverlaps);

                if (config.enableRaft)
                {
                    //Add the raft offset to each layer.
                    for (int layerNr = 0; layerNr < storage.volumes[volumeIndex].layers.Count; layerNr++)
                    {
                        storage.volumes[volumeIndex].layers[layerNr].printZ += config.raftBaseThickness_um + config.raftInterfaceThicknes_um;
                    }
                }
            }
            LogOutput.log("Generated layer parts in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();
        }

        void processSliceData(SliceDataStorage storage)
        {
            MultiVolumes.RemoveVolumesIntersections(storage.volumes);
            MultiVolumes.OverlapMultipleVolumesSlightly(storage.volumes, config.multiVolumeOverlapPercent);
#if False
            LayerPart.dumpLayerparts(storage, "output.html");
#endif

            int totalLayers = storage.volumes[0].layers.Count;
#if DEBUG
            for (int volumeIndex = 1; volumeIndex < storage.volumes.Count; volumeIndex++)
            {
                if (totalLayers != storage.volumes[volumeIndex].layers.Count)
                {
                    throw new Exception("All the valumes must have the same number of layers (they just can have empty layers).");
                }
            }
#endif

            for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
            {
                for (int volumeIndex = 0; volumeIndex < storage.volumes.Count; volumeIndex++)
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

                    SliceLayer layer = storage.volumes[volumeIndex].layers[layerIndex];
                    int extrusionWidth = config.extrusionWidth_um;
                    if (layerIndex == 0)
                    {
                        extrusionWidth = config.firstLayerExtrusionWidth_um;
                    }
                    Inset.generateInsets(layer, extrusionWidth, insetCount);
                }
                LogOutput.log("Creating Insets {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));
            }

            if (config.wipeShieldDistanceFromShapes_um > 0)
            {
                CreateWipeShields(storage, totalLayers);
            }

            LogOutput.log("Generated inset in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
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
                    for (int volumeIndex = 0; volumeIndex < storage.volumes.Count; volumeIndex++)
                    {
                        int extrusionWidth = config.extrusionWidth_um;
                        if (layerIndex == 0)
                        {
                            extrusionWidth = config.firstLayerExtrusionWidth_um;
                        }

                        Skin.generateTopAndBottomLayers(layerIndex, storage.volumes[volumeIndex], extrusionWidth, config.numberOfBottomLayers, config.numberOfTopLayers);
                        Skin.generateSparse(layerIndex, storage.volumes[volumeIndex], extrusionWidth, config.numberOfBottomLayers, config.numberOfTopLayers);
                    }
                }
                LogOutput.log("Creating Top & Bottom Layers {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));
            }
            LogOutput.log("Generated top bottom layers in {0:0.0}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();

            if (config.wipeTowerSize_um > 0)
            {
                Polygon p = new Polygon();
                storage.wipeTower.Add(p);
                p.Add(new IntPoint(storage.modelMin.x - 3000, storage.modelMax.y + 3000));
                p.Add(new IntPoint(storage.modelMin.x - 3000, storage.modelMax.y + 3000 + config.wipeTowerSize_um));
                p.Add(new IntPoint(storage.modelMin.x - 3000 - config.wipeTowerSize_um, storage.modelMax.y + 3000 + config.wipeTowerSize_um));
                p.Add(new IntPoint(storage.modelMin.x - 3000 - config.wipeTowerSize_um, storage.modelMax.y + 3000));

                storage.wipePoint = new IntPoint(storage.modelMin.x - 3000 - config.wipeTowerSize_um / 2, storage.modelMax.y + 3000 + config.wipeTowerSize_um / 2);
            }

            if (config.enableRaft)
            {
                Raft.GenerateRaftOutlines(storage, config.raftExtraDistanceAroundPart_um);
                Skirt.generateSkirt(storage,
                    config.skirtDistance_um + config.raftBaseLineSpacing_um,
                    config.raftBaseLineSpacing_um,
                    config.numberOfSkirtLoops, 
                    config.skirtMinLength_um, 
                    config.raftBaseThickness_um);
            }
            else
            {
                Skirt.generateSkirt(storage, 
                    config.skirtDistance_um, 
                    config.firstLayerExtrusionWidth_um, 
                    config.numberOfSkirtLoops, 
                    config.skirtMinLength_um, 
                    config.firstLayerThickness_um);
            }
        }

        private void CreateWipeShields(SliceDataStorage storage, int totalLayers)
        {
            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                Polygons wipeShield = new Polygons();
                for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                {
                    for (int partNr = 0; partNr < storage.volumes[volumeIdx].layers[layerNr].parts.Count; partNr++)
                    {
                        wipeShield = wipeShield.CreateUnion(storage.volumes[volumeIdx].layers[layerNr].parts[partNr].outline.Offset(config.wipeShieldDistanceFromShapes_um));
                    }
                }
                storage.wipeShield.Add(wipeShield);
            }

            for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
            {
                storage.wipeShield[layerIndex] = storage.wipeShield[layerIndex].Offset(-1000).Offset(1000);
            }

            int offsetAngle = (int)Math.Tan(60.0 * Math.PI / 180) * config.layerThickness_um;//Allow for a 60deg angle in the wipeShield.
            for (int layerNr = 1; layerNr < totalLayers; layerNr++)
            {
                storage.wipeShield[layerNr] = storage.wipeShield[layerNr].CreateUnion(storage.wipeShield[layerNr - 1].Offset(-offsetAngle));
            }

            for (int layerNr = totalLayers - 1; layerNr > 0; layerNr--)
            {
                storage.wipeShield[layerNr - 1] = storage.wipeShield[layerNr - 1].CreateUnion(storage.wipeShield[layerNr].Offset(-offsetAngle));
            }
        }

        void writeGCode(SliceDataStorage storage)
        {
            gcode.writeComment("filamentDiameter = {0}".FormatWith(config.filamentDiameter));
            gcode.writeComment("extrusionWidth = {0}".FormatWith(config.extrusionWidth));
            gcode.writeComment("firstLayerExtrusionWidth = {0}".FormatWith(config.firstLayerExtrusionWidth));
            gcode.writeComment("layerThickness = {0}".FormatWith(config.layerThickness));
            gcode.writeComment("firstLayerThickness = {0}".FormatWith(config.firstLayerThickness));

            if (fileNr == 1)
            {
                if (gcode.GetOutputType() == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
                {
                    gcode.writeComment("TYPE:UltiGCode");
                    gcode.writeComment("TIME:<__TIME__>");
                    gcode.writeComment("MATERIAL:<FILAMENT>");
                    gcode.writeComment("MATERIAL2:<FILAMEN2>");
                }
                gcode.writeCode(config.startCode);
                if (gcode.GetOutputType() == ConfigConstants.OUTPUT_TYPE.BFB)
                {
                    gcode.writeComment("enable auto-retraction");
                    gcode.writeLine("M227 S{0} P{1}".FormatWith(config.retractionOnTravel * 2560, config.retractionOnTravel * 2560));
                }

            }
            else
            {
                gcode.writeFanCommand(0);
                gcode.resetExtrusionValue();
                gcode.writeRetraction();
                gcode.setZ(maxObjectHeight + 5000);
                gcode.writeMove(gcode.getPositionXY(), config.travelSpeed, 0);
                gcode.writeMove(new IntPoint(storage.modelMin.x, storage.modelMin.y), config.travelSpeed, 0);
            }
            fileNr++;
            
            int totalLayers = storage.volumes[0].layers.Count;
            // let's remove any of the layers on top that are empty
            {
                for (int layerIndex = totalLayers - 1; layerIndex >= 0; layerIndex--)
                {
                    bool layerHasData = false;
                    foreach (SliceVolumeStorage currentVolume in storage.volumes)
                    {
                        SliceLayer currentLayer = currentVolume.layers[layerIndex];
                        for (int partIndex = 0; partIndex < currentVolume.layers[layerIndex].parts.Count; partIndex++)
                        {
                            SliceLayerPart currentPart = currentLayer.parts[partIndex];
                            if (currentPart.outline.Count > 0)
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
            gcode.writeComment("Layer count: {0}".FormatWith(totalLayers));

            // keep the raft generation code inside of raft
            Raft.GenerateRaftGCodeIfRequired(storage, config, gcode);

            int volumeIdx = 0;
            for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
            {
				if (MatterSlice.Canceled)
				{
					return;
				}
				LogOutput.log("Writing Layers {0}/{1}\n".FormatWith(layerIndex + 1, totalLayers));

                LogOutput.logProgress("export", layerIndex + 1, totalLayers);

                int extrusionWidth_um = config.extrusionWidth_um;
                if (layerIndex == 0)
                {
                    extrusionWidth_um = config.firstLayerExtrusionWidth_um;
                }

                if (layerIndex == 0)
                {
                    skirtConfig.setData(config.firstLayerSpeed, extrusionWidth_um, "SKIRT");
                    inset0Config.setData(config.firstLayerSpeed, extrusionWidth_um, "WALL-OUTER");
                    insetXConfig.setData(config.firstLayerSpeed, extrusionWidth_um, "WALL-INNER");
                    fillConfig.setData(config.firstLayerSpeed, extrusionWidth_um, "FILL", false);
                    bridgConfig.setData(config.firstLayerSpeed, extrusionWidth_um, "BRIDGE");
					
					supportNormalConfig.setData(config.firstLayerSpeed, config.supportExtrusionWidth_um, "SUPPORT");
					supportInterfaceConfig.setData(config.firstLayerSpeed, config.extrusionWidth_um, "SUPPORT-INTERFACE");
				}
                else
                {
                    skirtConfig.setData(config.insidePerimetersSpeed, extrusionWidth_um, "SKIRT");
                    inset0Config.setData(config.outsidePerimeterSpeed, extrusionWidth_um, "WALL-OUTER");
                    insetXConfig.setData(config.insidePerimetersSpeed, extrusionWidth_um, "WALL-INNER");
                    fillConfig.setData(config.infillSpeed, extrusionWidth_um, "FILL", false);
                    bridgConfig.setData(config.bridgeSpeed, extrusionWidth_um, "BRIDGE");
					
					supportNormalConfig.setData(config.supportMaterialSpeed, config.supportExtrusionWidth_um, "SUPPORT");
					supportInterfaceConfig.setData(config.supportMaterialSpeed, config.extrusionWidth_um, "SUPPORT-INTERFACE");
				}

                gcode.writeComment("LAYER:{0}".FormatWith(layerIndex));
                if (layerIndex == 0)
                {
                    gcode.setExtrusion(config.firstLayerThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);
                }
                else
                {
                    gcode.setExtrusion(config.layerThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);
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

                bool printSupportFirst = (storage.support.generated && config.supportExtruder > 0 && config.supportExtruder == gcodeLayer.getExtruder());
                if (printSupportFirst)
                {
                    AddSupportToGCode(storage, gcodeLayer, layerIndex, config);
                }

                int fanSpeedPercent = GetFanSpeed(layerIndex, gcodeLayer);

                for (int volumeCnt = 0; volumeCnt < storage.volumes.Count; volumeCnt++)
                {
                    if (volumeCnt > 0)
                    {
                        volumeIdx = (volumeIdx + 1) % storage.volumes.Count;
                    }

                    AddVolumeLayerToGCode(storage, gcodeLayer, volumeIdx, layerIndex, extrusionWidth_um, fanSpeedPercent);
                }

                if (!printSupportFirst)
                {
                    AddSupportToGCode(storage, gcodeLayer, layerIndex, config);
                }

                //Finish the layer by applying speed corrections for minimum layer times.
                gcodeLayer.forceMinimumLayerTime(config.minimumLayerTimeSeconds, config.minimumPrintingSpeed);

                gcode.writeFanCommand(fanSpeedPercent);

                int currentLayerThickness_um = config.layerThickness_um;
                if (layerIndex <= 0)
                {
                    currentLayerThickness_um = config.firstLayerThickness_um;
                }

                gcodeLayer.writeGCode(config.doCoolHeadLift, currentLayerThickness_um);
            }

            LogOutput.log("Wrote layers in {0:0.00}s.\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();
            gcode.tellFileSize();
            gcode.writeFanCommand(0);

            //Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
            maxObjectHeight = Math.Max(maxObjectHeight, storage.modelSize.z);
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

        //Add a single layer from a single mesh-volume to the GCode
        void AddVolumeLayerToGCode(SliceDataStorage storage, GCodePlanner gcodeLayer, int volumeIndex, int layerIndex, int extrusionWidth_um, int fanSpeedPercent)
        {
            int prevExtruder = gcodeLayer.getExtruder();
            bool extruderChanged = gcodeLayer.setExtruder(volumeIndex);
            if (layerIndex == 0 && volumeIndex == 0 && !Raft.ShouldGenerateRaft(config))
            {
                if (storage.skirt.Count > 0
                    && storage.skirt[0].Count > 0)
                {
                    IntPoint lowestPoint = storage.skirt[0][0];

                    // lets make sure we start with the most outside loop
                    foreach (Polygon polygon in storage.skirt)
                    {
                        foreach (IntPoint position in polygon)
                        {
                            if (position.Y < lowestPoint.Y)
                            {
                                lowestPoint = polygon[0];
                            }
                        }
                    }

                    gcodeLayer.writeTravel(lowestPoint);
                }

                gcodeLayer.writePolygonsByOptimizer(storage.skirt, skirtConfig);
            }

            SliceLayer layer = storage.volumes[volumeIndex].layers[layerIndex];
            if (extruderChanged)
            {
                addWipeTower(storage, gcodeLayer, layerIndex, prevExtruder, extrusionWidth_um);
            }

            if (storage.wipeShield.Count > 0 && storage.volumes.Count > 1)
            {
                gcodeLayer.setAlwaysRetract(true);
                gcodeLayer.writePolygonsByOptimizer(storage.wipeShield[layerIndex], skirtConfig);
                gcodeLayer.setAlwaysRetract(!config.avoidCrossingPerimeters);
            }

			PathOrderOptimizer partOrderOptimizer = new PathOrderOptimizer(new IntPoint());
            for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
            {
                partOrderOptimizer.AddPolygon(layer.parts[partIndex].insets[0][0]);
            }
            partOrderOptimizer.Optimize();

            for (int partCounter = 0; partCounter < partOrderOptimizer.bestPolygonOrderIndex.Count; partCounter++)
            {
                SliceLayerPart part = layer.parts[partOrderOptimizer.bestPolygonOrderIndex[partCounter]];

                if (config.avoidCrossingPerimeters)
                {
                    gcodeLayer.SetOuterPerimetersToAvoidCrossing(part.combBoundery);
                }
                else
                {
                    gcodeLayer.setAlwaysRetract(true);
                }

				Polygons fillPolygons = new Polygons();
				Polygons bridgePolygons = new Polygons();

				CalculateInfillData(storage, volumeIndex, layerIndex, extrusionWidth_um, part, ref fillPolygons, ref bridgePolygons);

				// Write the bidge polgons out first so the perimeter will have more to hold to while bridging the gaps.
				// It would be even better to slow down the perimeters that are part of bridges but that is a bit harder.
				if (bridgePolygons.Count > 0)
				{
					gcode.writeFanCommand(config.bridgeFanSpeedPercent);
					gcodeLayer.writePolygonsByOptimizer(bridgePolygons, bridgConfig);
					gcode.writeFanCommand(fanSpeedPercent);
				}
				
				if (config.numberOfPerimeters > 0)
                {
                    if (partCounter > 0)
                    {
                        gcodeLayer.forceRetract();
                    }

                    if (config.continuousSpiralOuterPerimeter)
                    {
                        if (layerIndex >= config.numberOfBottomLayers)
                        {
                            inset0Config.spiralize = true;
                        }

                        if (layerIndex == config.numberOfBottomLayers && part.insets.Count > 0)
                        {
                            gcodeLayer.writePolygonsByOptimizer(part.insets[0], insetXConfig);
                        }
                    }

					// If we are on the very first layer we start with the outside in so that we can stick to the bed better.
					if (config.outsidePerimetersFirst || layerIndex == 0)
					{
						// First the outside (this helps with accuracy)
						if (part.insets.Count > 0)
						{
							gcodeLayer.writePolygonsByOptimizer(part.insets[0], inset0Config);
						}
						for (int perimeterIndex = 1; perimeterIndex < part.insets.Count; perimeterIndex++)
						{
							gcodeLayer.writePolygonsByOptimizer(part.insets[perimeterIndex], insetXConfig);
						}
					}
					else // This is so we can do overhanges better (the outside can stick a bit to the inside).
					{
						// Print everything but the first perimeter from the outside in so the little parts have more to stick to.
						for (int perimeterIndex = 1; perimeterIndex < part.insets.Count; perimeterIndex++)
						{
							gcodeLayer.writePolygonsByOptimizer(part.insets[perimeterIndex], insetXConfig);
						}
						// then 0
						if (part.insets.Count > 0)
						{
							gcodeLayer.writePolygonsByOptimizer(part.insets[0], inset0Config);
						}
					}
                }

                gcodeLayer.writePolygonsByOptimizer(fillPolygons, fillConfig);

                //After a layer part, make sure the nozzle is inside the comb boundary, so we do not retract on the perimeter.
                if (!config.continuousSpiralOuterPerimeter || layerIndex < config.numberOfBottomLayers)
                {
                    gcodeLayer.MoveInsideTheOuterPerimeter(extrusionWidth_um * 2);
                }
            }
            gcodeLayer.SetOuterPerimetersToAvoidCrossing(null);
        }

		private void CalculateInfillData(SliceDataStorage storage, int volumeIndex, int layerIndex, int extrusionWidth_um, SliceLayerPart part, ref Polygons fillPolygons, ref Polygons bridgePolygons)
		{
			// generate infill for outline including bridging
			foreach (Polygons outline in part.skinOutline.SplitIntoParts())
			{
				double partFillAngle = config.infillStartingAngle;
				if ((layerIndex & 1) == 1)
				{
					partFillAngle += 90;
				}
				if (layerIndex > 0)
				{
					double bridgeAngle;
					if (Bridge.BridgeAngle(outline, storage.volumes[volumeIndex].layers[layerIndex - 1], out bridgeAngle))
					{
						Infill.GenerateLinePaths(outline, ref bridgePolygons, extrusionWidth_um, config.infillExtendIntoPerimeter_um, bridgeAngle);
					}
					else
					{
						Infill.GenerateLinePaths(outline, ref fillPolygons, extrusionWidth_um, config.infillExtendIntoPerimeter_um, partFillAngle);
					}
				}
				else
				{
					Infill.GenerateLinePaths(outline, ref fillPolygons, extrusionWidth_um, config.infillExtendIntoPerimeter_um, partFillAngle);
				}
			}

			double fillAngle = config.infillStartingAngle;

			// generate the infill for this part on this layer
			if (config.infillPercent > 0)
			{
				switch (config.infillType)
				{
					case ConfigConstants.INFILL_TYPE.LINES:
						if ((layerIndex & 1) == 1)
						{
							fillAngle += 90;
						}
						Infill.GenerateLineInfill(config, part.sparseOutline, ref fillPolygons, extrusionWidth_um, fillAngle);
						break;

					case ConfigConstants.INFILL_TYPE.GRID:
						Infill.GenerateGridInfill(config, part.sparseOutline, ref fillPolygons, extrusionWidth_um, fillAngle);
						break;

					case ConfigConstants.INFILL_TYPE.TRIANGLES:
						Infill.GenerateTriangleInfill(config, part.sparseOutline, ref fillPolygons, extrusionWidth_um, fillAngle);
						break;

					case ConfigConstants.INFILL_TYPE.HEXAGON:
						Infill.GenerateHexagonInfill(config, part.sparseOutline, ref fillPolygons, extrusionWidth_um, fillAngle, layerIndex);
						break;

					case ConfigConstants.INFILL_TYPE.CONCENTRIC:
						Infill.generateConcentricInfill(config, part.sparseOutline, ref fillPolygons, extrusionWidth_um, fillAngle);
						break;

					default:
						throw new NotImplementedException();
				}
			}
		}

        void AddSupportToGCode(SliceDataStorage storage, GCodePlanner gcodeLayer, int layerIndex, ConfigSettings config)
        {
            if (!storage.support.generated)
            {
                return;
            }

            if (config.supportExtruder > -1)
            {
                int prevExtruder = gcodeLayer.getExtruder();
                if (gcodeLayer.setExtruder(config.supportExtruder))
                {
                    addWipeTower(storage, gcodeLayer, layerIndex, prevExtruder, config.extrusionWidth_um);
                }

                if (storage.wipeShield.Count > 0 && storage.volumes.Count == 1)
                {
                    gcodeLayer.setAlwaysRetract(true);
                    gcodeLayer.writePolygonsByOptimizer(storage.wipeShield[layerIndex], skirtConfig);
                    gcodeLayer.setAlwaysRetract(config.avoidCrossingPerimeters);
                }
            }

            int currentZHeight_um = config.firstLayerThickness_um;
            if (layerIndex == 0)
            {
                currentZHeight_um /= 2;
            }
            else
            {
                if(layerIndex > 1)
                {
                    currentZHeight_um += (layerIndex-1) * config.layerThickness_um;
                }
                currentZHeight_um += config.layerThickness_um / 2;
            }
            
            SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, currentZHeight_um);

            WriteSupportPolygons(storage, gcodeLayer, layerIndex, config, supportGenerator.supportPolygons, SupportType.General);

            if (config.supportInterfaceExtruder != -1
                && config.supportInterfaceExtruder != config.supportExtruder)
            {
                gcodeLayer.setExtruder(config.supportInterfaceExtruder);
            }
            WriteSupportPolygons(storage, gcodeLayer, layerIndex, config, supportGenerator.interfacePolygons, SupportType.Interface);
        }

        enum SupportType { General, Interface };
        private void WriteSupportPolygons(SliceDataStorage storage, GCodePlanner gcodeLayer, int layerIndex, ConfigSettings config, Polygons supportPolygons, SupportType interfaceLayer)
        {
            for (int volumeIndex = 0; volumeIndex < storage.volumes.Count; volumeIndex++)
            {
                SliceLayer layer = storage.volumes[volumeIndex].layers[layerIndex];
                for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
                {
                    supportPolygons = supportPolygons.CreateDifference(layer.parts[partIndex].outline.Offset(config.supportXYDistance_um));
                }
            }

            //Contract and expand the support polygons so small sections are removed and the final polygon is smoothed a bit.
            supportPolygons = supportPolygons.Offset(-config.extrusionWidth_um * 1);
            supportPolygons = supportPolygons.Offset(config.extrusionWidth_um * 1);

            List<Polygons> supportIslands = supportPolygons.SplitIntoParts();
            PathOrderOptimizer islandOrderOptimizer = new PathOrderOptimizer(gcode.getPositionXY());

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
                            Infill.GenerateLineInfill(config, island, ref supportLines, config.extrusionWidth_um, config.supportInfillStartingAngle + 90, config.extrusionWidth_um);
                            break;

                        case SupportType.General:
                            switch (config.supportType)
                            {
                                case ConfigConstants.SUPPORT_TYPE.GRID:
                                    Infill.GenerateGridInfill(config, island, ref supportLines, config.supportExtrusionWidth_um, config.supportInfillStartingAngle, config.supportLineSpacing_um);
                                    break;

                                case ConfigConstants.SUPPORT_TYPE.LINES:
                                    Infill.GenerateLineInfill(config, island, ref supportLines, config.supportExtrusionWidth_um, config.supportInfillStartingAngle, config.supportLineSpacing_um);
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
                        gcodeLayer.writePolygonsByOptimizer(supportLines, supportInterfaceConfig);
                        break;

                    case SupportType.General:
                        if (config.supportType == ConfigConstants.SUPPORT_TYPE.GRID)
                        {
                            gcodeLayer.writePolygonsByOptimizer(island, supportNormalConfig);
                        }
                        gcodeLayer.writePolygonsByOptimizer(supportLines, supportNormalConfig);
                        break;

                    default:
                        throw new NotImplementedException();
                }

                gcodeLayer.SetOuterPerimetersToAvoidCrossing(null);
            }
        }

        void addWipeTower(SliceDataStorage storage, GCodePlanner gcodeLayer, int layerNr, int prevExtruder, int extrusionWidth_um)
        {
            if (config.wipeTowerSize_um < 1)
            {
                return;
            }

            //If we changed extruder, print the wipe/prime tower for this nozzle;
            gcodeLayer.writePolygonsByOptimizer(storage.wipeTower, supportInterfaceConfig);
            Polygons fillPolygons = new Polygons();
            Infill.GenerateLinePaths(storage.wipeTower, ref fillPolygons, extrusionWidth_um, config.infillExtendIntoPerimeter_um, 45 + 90 * (layerNr % 2));
            gcodeLayer.writePolygonsByOptimizer(fillPolygons, supportInterfaceConfig);

            //Make sure we wipe the old extruder on the wipe tower.
            gcodeLayer.writeTravel(storage.wipePoint - config.extruderOffsets[prevExtruder] + config.extruderOffsets[gcodeLayer.getExtruder()]);
        }

		public void Cancel()
		{
			if (gcode.isOpened())
			{
				gcode.Close();
			}
		}
	}
}