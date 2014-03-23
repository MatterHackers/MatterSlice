/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ClipperLib;

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

        GCodePathConfig skirtConfig = new GCodePathConfig();
        GCodePathConfig inset0Config = new GCodePathConfig();
        GCodePathConfig insetXConfig = new GCodePathConfig();
        GCodePathConfig fillConfig = new GCodePathConfig();
        GCodePathConfig supportConfig = new GCodePathConfig();

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

        public bool processFile(string input_filename)
        {
            if (!gcode.isOpened())
            {
                return false;
            }

            Stopwatch timeKeeperTotal = new Stopwatch();
            timeKeeperTotal.Start();
            SliceDataStorage storage = new SliceDataStorage();
            preSetup(config.extrusionWidth_µm);
            if (!prepareModel(storage, input_filename))
            {
                return false;
            }

            processSliceData(storage);
            writeGCode(storage);

            LogOutput.logProgress("process", 1, 1); //Report to the GUI that a file has been fully processed.
            LogOutput.log("Total time elapsed {0:0.00}s.\n".FormatWith(timeKeeperTotal.Elapsed.Seconds));

            return true;
        }

        public void finalize()
        {
            if (!gcode.isOpened())
            {
                return;
            }

            gcode.finalize(maxObjectHeight, config.travelSpeedMmPerS, config.endCode);

            gcode.Close();
        }

        void preSetup(int extrusionWidth)
        {
            skirtConfig.setData(config.normalPrintSpeedMmPerS, extrusionWidth, "SKIRT");
            inset0Config.setData(config.outsidePerimeterSpeedMmPerS, extrusionWidth, "WALL-OUTER");
            insetXConfig.setData(config.insidePerimeterSpeedMmPerS, extrusionWidth, "WALL-INNER");
            fillConfig.setData(config.infillSpeedMmPerS, extrusionWidth, "FILL");
            supportConfig.setData(config.normalPrintSpeedMmPerS, extrusionWidth, "SUPPORT");

            for (int n = 1; n < ConfigConstants.MAX_EXTRUDERS; n++)
            {
                gcode.setExtruderOffset(n, config.extruderOffsets[n]);
            }

            gcode.setFlavor(config.gcodeFlavor);
            gcode.setRetractionSettings(config.retractionAmount, config.retractionSpeedMmPerS, config.retractionAmountExtruderSwitch, config.minimumExtrusionBeforeRetraction_µm, config.retractionZHopMm);
        }

        bool prepareModel(SliceDataStorage storage, string input_filename)
        {
            timeKeeper.Restart();
            LogOutput.log("Loading {0} from disk...\n".FormatWith(input_filename));
            SimpleModel model = SimpleModel.loadModelFromFile(input_filename, config.modelRotationMatrix);
            if (model == null)
            {
                LogOutput.logError("Failed to load model: {0}\n".FormatWith(input_filename));
                return false;
            }
            LogOutput.log("Loaded from disk in {0:0.000}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();
            LogOutput.log("Analyzing and optimizing model...\n");
            OptimizedModel optomizedModel = new OptimizedModel(model, new Point3(config.objectCenterPosition_µm.X, config.objectCenterPosition_µm.Y, -config.objectSink_µm));
            for (int volumeIndex = 0; volumeIndex < model.volumes.Count; volumeIndex++)
            {
                LogOutput.log("  Face counts: {0} . {1} {2:0.0}%\n".FormatWith((int)model.volumes[volumeIndex].faces.Count, (int)optomizedModel.volumes[volumeIndex].faces.Count, (double)(optomizedModel.volumes[volumeIndex].faces.Count) / (double)(model.volumes[volumeIndex].faces.Count) * 100));
                LogOutput.log("  Vertex counts: {0} . {1} {2:0.0}%\n".FormatWith((int)model.volumes[volumeIndex].faces.Count * 3, (int)optomizedModel.volumes[volumeIndex].points.Count, (double)(optomizedModel.volumes[volumeIndex].points.Count) / (double)(model.volumes[volumeIndex].faces.Count * 3) * 100));
            }

            LogOutput.log("Optimize model {0:0.000}s \n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Reset();
#if DEBUG
            optomizedModel.saveDebugSTL("debug_output.stl");
#endif

            LogOutput.log("Slicing model...\n");
            List<Slicer> slicerList = new List<Slicer>();
            for (int volumeIdx = 0; volumeIdx < optomizedModel.volumes.Count; volumeIdx++)
            {
                Slicer slicer = new Slicer(optomizedModel.volumes[volumeIdx], config.initialLayerThickness_µm, config.layerThickness_µm,
                    (config.fixHorrible & ConfigConstants.FIX_HORRIBLE.KEEP_NONE_CLOSED) == ConfigConstants.FIX_HORRIBLE.KEEP_NONE_CLOSED,
                    (config.fixHorrible & ConfigConstants.FIX_HORRIBLE.EXTENSIVE_STITCHING) == ConfigConstants.FIX_HORRIBLE.EXTENSIVE_STITCHING);
                slicerList.Add(slicer);
                for (int layerNr = 0; layerNr < slicer.layers.Count; layerNr++)
                {
                    //Reporting the outline here slows down the engine quite a bit, so only do so when debugging.
                    //logPolygons("outline", layerNr, slicer.layers[layerNr].z, slicer.layers[layerNr].polygonList);
                    LogOutput.logPolygons("openoutline", layerNr, slicer.layers[layerNr].z, slicer.layers[layerNr].openPolygonList);
                }
            }
            LogOutput.log("Sliced model in {0:0.000}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();

            LogOutput.log("Generating support map...\n");
            SupportPolyGenerator.generateSupportGrid(storage.support, optomizedModel, config.supportAngleDegrees, config.supportEverywhere > 0, config.supportXYDistance, config.supportZDistance);

            storage.modelSize = optomizedModel.modelSize;
            storage.modelMin = optomizedModel.vMin;
            storage.modelMax = optomizedModel.vMax;

            LogOutput.log("Generating layer parts...\n");
            for (int volumeIdx = 0; volumeIdx < slicerList.Count; volumeIdx++)
            {
                storage.volumes.Add(new SliceVolumeStorage());
                LayerPart.createLayerParts(storage.volumes[volumeIdx], slicerList[volumeIdx], config.fixHorrible & (ConfigConstants.FIX_HORRIBLE.UNION_ALL_TYPE_A | ConfigConstants.FIX_HORRIBLE.UNION_ALL_TYPE_B | ConfigConstants.FIX_HORRIBLE.UNION_ALL_TYPE_C));
                slicerList[volumeIdx] = null;

                //Add the raft offset to each layer.
                for (int layerNr = 0; layerNr < storage.volumes[volumeIdx].layers.Count; layerNr++)
                {
                    storage.volumes[volumeIdx].layers[layerNr].printZ += config.raftBaseThickness + config.raftInterfaceThickness;
                }          
            }
            LogOutput.log("Generated layer parts in {0:0.000}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();
            return true;
        }

        void processSliceData(SliceDataStorage storage)
        {
            //carveMultipleVolumes(storage.volumes);
            MultiVolumes.generateMultipleVolumesOverlap(storage.volumes, config.multiVolumeOverlapPercent);
#if DEBUG
            LayerPart.dumpLayerparts(storage, "output.html");
#endif

            int totalLayers = storage.volumes[0].layers.Count;
            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                {
                    int insetCount = config.perimeterCount;
                    if (config.spiralizeMode && (int)(layerNr) < config.downSkinCount && layerNr % 2 == 1)
                    {
                        //Add extra insets every 2 layers when spiralizing, this makes bottoms of cups watertight.
                        insetCount += 5;
                    }

                    SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
                    int extrusionWidth = config.extrusionWidth_µm;
                    if (layerNr == 0)
                    {
                        extrusionWidth = config.firstLayerExtrusionWidth_µm;
                    }
                    Inset.generateInsets(layer, extrusionWidth, insetCount);

                    for (int partNr = 0; partNr < layer.parts.Count; partNr++)
                    {
                        if (layer.parts[partNr].insets.Count > 0)
                        {
                            LogOutput.logPolygons("inset0", layerNr, layer.printZ, layer.parts[partNr].insets[0]);
                            for (int inset = 1; inset < layer.parts[partNr].insets.Count; inset++)
                            {
                                LogOutput.logPolygons("insetx", layerNr, layer.printZ, layer.parts[partNr].insets[inset]);
                            }
                        }
                    }
                }
                LogOutput.logProgress("inset", layerNr + 1, totalLayers);
            }

            if (config.enableOozeShield)
            {
                for (int layerNr = 0; layerNr < totalLayers; layerNr++)
                {
                    Polygons oozeShield = new Polygons();
                    for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                    {
                        for (int partNr = 0; partNr < storage.volumes[volumeIdx].layers[layerNr].parts.Count; partNr++)
                        {
                            oozeShield = oozeShield.CreateUnion(storage.volumes[volumeIdx].layers[layerNr].parts[partNr].outline.Offset(2000));
                        }
                    }
                    storage.oozeShield.Add(oozeShield);
                }

                for (int layerNr = 0; layerNr < totalLayers; layerNr++)
                {
                    storage.oozeShield[layerNr] = storage.oozeShield[layerNr].Offset(-1000).Offset(1000);
                }

                int offsetAngle = (int)Math.Tan(60.0 * Math.PI / 180) * config.layerThickness_µm;//Allow for a 60deg angle in the oozeShield.
                for (int layerNr = 1; layerNr < totalLayers; layerNr++)
                {
                    storage.oozeShield[layerNr] = storage.oozeShield[layerNr].CreateUnion(storage.oozeShield[layerNr - 1].Offset(-offsetAngle));
                }

                for (int layerNr = totalLayers - 1; layerNr > 0; layerNr--)
                {
                    storage.oozeShield[layerNr - 1] = storage.oozeShield[layerNr - 1].CreateUnion(storage.oozeShield[layerNr].Offset(-offsetAngle));
                }
            }
            LogOutput.log("Generated inset in {0:0.000}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();

            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                //Only generate up/downskin and infill for the first X layers when spiralize is choosen.
                if (!config.spiralizeMode || (int)(layerNr) < config.downSkinCount)
                {
                    for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                    {
                        int extrusionWidth = config.extrusionWidth_µm;
                        if (layerNr == 0)
                        {
                            extrusionWidth = config.firstLayerExtrusionWidth_µm;
                        }

                        Skin.generateSkins(layerNr, storage.volumes[volumeIdx], extrusionWidth, config.downSkinCount, config.upSkinCount, config.infillOverlapPercent);
                        Skin.generateSparse(layerNr, storage.volumes[volumeIdx], extrusionWidth, config.downSkinCount, config.upSkinCount);

                        SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
                        for (int partNr = 0; partNr < layer.parts.Count; partNr++)
                        {
                            LogOutput.logPolygons("skin", layerNr, layer.printZ, layer.parts[partNr].skinOutline);
                        }
                    }
                }
                LogOutput.logProgress("skin", layerNr + 1, totalLayers);
            }
            LogOutput.log("Generated up/down skin in {0:0.000}s\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();

            if (config.wipeTowerSize > 0)
            {
                Polygon p = new Polygon();
                storage.wipeTower.Add(p);
                p.Add(new IntPoint(storage.modelMin.x - 3000, storage.modelMax.y + 3000));
                p.Add(new IntPoint(storage.modelMin.x - 3000, storage.modelMax.y + 3000 + config.wipeTowerSize));
                p.Add(new IntPoint(storage.modelMin.x - 3000 - config.wipeTowerSize, storage.modelMax.y + 3000 + config.wipeTowerSize));
                p.Add(new IntPoint(storage.modelMin.x - 3000 - config.wipeTowerSize, storage.modelMax.y + 3000));

                storage.wipePoint = new IntPoint(storage.modelMin.x - 3000 - config.wipeTowerSize / 2, storage.modelMax.y + 3000 + config.wipeTowerSize / 2);
            }

            Skirt.generateSkirt(storage, config.skirtDistance_µm, config.firstLayerExtrusionWidth_µm, config.skirtLineCount, config.skirtMinLength_µm, config.initialLayerThickness_µm);
            Raft.generateRaft(storage, config.raftExtraDistanceAroundPart_µm);

            for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
            {
                for (int layerNr = 0; layerNr < totalLayers; layerNr++)
                {
                    for (int partNr = 0; partNr < storage.volumes[volumeIdx].layers[layerNr].parts.Count; partNr++)
                    {
                        if (layerNr > 0)
                        {
                            storage.volumes[volumeIdx].layers[layerNr].parts[partNr].bridgeAngle = Bridge.bridgeAngle(storage.volumes[volumeIdx].layers[layerNr].parts[partNr], storage.volumes[volumeIdx].layers[layerNr - 1]);
                        }
                        else
                        {
                            storage.volumes[volumeIdx].layers[layerNr].parts[partNr].bridgeAngle = -1;
                        }
                    }
                }
            }
        }

        void writeGCode(SliceDataStorage storage)
        {
            if (fileNr == 1)
            {
                if (gcode.getFlavor() == ConfigConstants.GCODE_FLAVOR.ULTIGCODE)
                {
                    gcode.writeComment("FLAVOR:UltiGCode");
                    gcode.writeComment("TIME:<__TIME__>");
                    gcode.writeComment("MATERIAL:<FILAMENT>");
                    gcode.writeComment("MATERIAL2:<FILAMEN2>");
                }
                gcode.writeCode(config.startCode);
                if (gcode.getFlavor() == ConfigConstants.GCODE_FLAVOR.BFB)
                {
                    gcode.writeComment("enable auto-retraction");
                    gcode.writeLine("M227 S{0} P{1}".FormatWith(config.retractionAmount * 2560 / 1000, config.retractionAmount * 2560 / 1000));
                }
         
            }
            else
            {
                gcode.writeFanCommand(0);
                gcode.resetExtrusionValue();
                gcode.writeRetraction();
                gcode.setZ(maxObjectHeight + 5000);
                gcode.writeMove(gcode.getPositionXY(), config.travelSpeedMmPerS, 0);
                gcode.writeMove(new IntPoint(storage.modelMin.x, storage.modelMin.y), config.travelSpeedMmPerS, 0);
            }
            fileNr++;

            int totalLayers = storage.volumes[0].layers.Count;
            gcode.writeComment("Layer count: {0}".FormatWith(totalLayers));

            if (config.raftBaseThickness > 0 && config.raftInterfaceThickness > 0)
            {
                GCodePathConfig raftBaseConfig = new GCodePathConfig(config.firstLayerSpeedMmPerS, config.raftBaseLinewidth, "SUPPORT");
                GCodePathConfig raftInterfaceConfig = new GCodePathConfig(config.firstLayerSpeedMmPerS, config.raftInterfaceLinewidth, "SUPPORT");
                {
                    gcode.writeComment("LAYER:-2");
                    gcode.writeComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeedMmPerS, config.retractionMinimumDistance);
                    gcodeLayer.setAlwaysRetract(true);
                    gcode.setZ(config.raftBaseThickness);
                    gcode.setExtrusion(config.raftBaseThickness, config.filamentDiameter_µm, config.filamentFlowPercent);
                    gcodeLayer.writePolygonsByOptimizer(storage.raftOutline, raftBaseConfig);

                    Polygons raftLines = new Polygons();
                    Infill.generateLineInfill(storage.raftOutline, raftLines, config.raftBaseLinewidth, config.raftLineSpacing, config.infillOverlapPercent, 0);
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftBaseConfig);

                    gcodeLayer.writeGCode(false, config.raftBaseThickness);
                }

                {
                    gcode.writeComment("LAYER:-1");
                    gcode.writeComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeedMmPerS, config.retractionMinimumDistance);
                    if (config.supportExtruder > 0)
                    {
                        gcodeLayer.setExtruder(config.supportExtruder);
                    }

                    gcodeLayer.setAlwaysRetract(true);
                    gcode.setZ(config.raftBaseThickness + config.raftInterfaceThickness);
                    gcode.setExtrusion(config.raftInterfaceThickness, config.filamentDiameter_µm, config.filamentFlowPercent);

                    Polygons raftLines = new Polygons();
                    Infill.generateLineInfill(storage.raftOutline, raftLines, config.raftInterfaceLinewidth, config.raftLineSpacing, config.infillOverlapPercent, 90);
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftInterfaceConfig);

                    gcodeLayer.writeGCode(false, config.raftInterfaceThickness);
                }
            }

            int volumeIdx = 0;
            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                LogOutput.logProgress("export", layerNr + 1, totalLayers);
                
                int extrusionWidth = config.extrusionWidth_µm;
                if (layerNr == 0)
                {
                    extrusionWidth = config.firstLayerExtrusionWidth_µm;
                }

                if (layerNr == 0)
                {
                    skirtConfig.setData(config.firstLayerSpeedMmPerS, extrusionWidth, "SKIRT");
                    inset0Config.setData(config.firstLayerSpeedMmPerS, extrusionWidth, "WALL-OUTER");
                    insetXConfig.setData(config.firstLayerSpeedMmPerS, extrusionWidth, "WALL-INNER");
                    fillConfig.setData(config.firstLayerSpeedMmPerS, extrusionWidth, "FILL");
                    supportConfig.setData(config.firstLayerSpeedMmPerS, extrusionWidth, "SUPPORT");
                }
                else
                {
                    skirtConfig.setData(config.normalPrintSpeedMmPerS, extrusionWidth, "SKIRT");
                    inset0Config.setData(config.outsidePerimeterSpeedMmPerS, extrusionWidth, "WALL-OUTER");
                    insetXConfig.setData(config.insidePerimeterSpeedMmPerS, extrusionWidth, "WALL-INNER");
                    fillConfig.setData(config.infillSpeedMmPerS, extrusionWidth, "FILL");
                    supportConfig.setData(config.normalPrintSpeedMmPerS, extrusionWidth, "SUPPORT");
                }

                gcode.writeComment("LAYER:{0}".FormatWith(layerNr));
                if (layerNr == 0)
                {
                    gcode.setExtrusion(config.initialLayerThickness_µm, config.filamentDiameter_µm, config.filamentFlowPercent);
                }
                else
                {
                    gcode.setExtrusion(config.layerThickness_µm, config.filamentDiameter_µm, config.filamentFlowPercent);
                }

                GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeedMmPerS, config.retractionMinimumDistance);
                int z = config.initialLayerThickness_µm + layerNr * config.layerThickness_µm;
                z += config.raftBaseThickness + config.raftInterfaceThickness;
                gcode.setZ(z);

                bool printSupportFirst = (storage.support.generated && config.supportExtruder > 0 && config.supportExtruder == gcodeLayer.getExtruder());
                if (printSupportFirst)
                {
                    addSupportToGCode(storage, gcodeLayer, layerNr, extrusionWidth);
                }

                for (int volumeCnt = 0; volumeCnt < storage.volumes.Count; volumeCnt++)
                {
                    if (volumeCnt > 0)
                    {
                        volumeIdx = (volumeIdx + 1) % storage.volumes.Count;
                    }

                    addVolumeLayerToGCode(storage, gcodeLayer, volumeIdx, layerNr, extrusionWidth);
                }

                if (!printSupportFirst)
                {
                    addSupportToGCode(storage, gcodeLayer, layerNr, extrusionWidth);
                }

                //Finish the layer by applying speed corrections for minimum layer times.
                gcodeLayer.forceMinimumLayerTime(config.minimumLayerTimeSeconds, config.minimumFeedrateMmPerS);

                int fanSpeed = config.fanSpeedMinPercent;
                if (gcodeLayer.getExtrudeSpeedFactor() <= 50)
                {
                    fanSpeed = config.fanSpeedMaxPercent;
                }
                else
                {
                    int n = gcodeLayer.getExtrudeSpeedFactor() - 50;
                    fanSpeed = config.fanSpeedMinPercent * n / 50 + config.fanSpeedMaxPercent * (50 - n) / 50;
                }
                if ((int)(layerNr) < config.firstLayerToAllowFan)
                {
                    // Don't allow the fan below this layer
                    fanSpeed = 0;
                }
                gcode.writeFanCommand(fanSpeed);

                gcodeLayer.writeGCode(config.doCoolHeadLift, (int)(layerNr) > 0 ? config.layerThickness_µm : config.initialLayerThickness_µm);
            }

            LogOutput.log("Wrote layers in {0:0.00}s.\n".FormatWith(timeKeeper.Elapsed.Seconds));
            timeKeeper.Restart();
            gcode.tellFileSize();
            gcode.writeFanCommand(0);

            //Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
            maxObjectHeight = Math.Max(maxObjectHeight, storage.modelSize.z);
        }

        //Add a single layer from a single mesh-volume to the GCode
        void addVolumeLayerToGCode(SliceDataStorage storage, GCodePlanner gcodeLayer, int volumeIdx, int layerNr, int extrusionWidth)
        {
            int prevExtruder = gcodeLayer.getExtruder();
            bool extruderChanged = gcodeLayer.setExtruder(volumeIdx);
            if (layerNr == 0 && volumeIdx == 0)
            {
                gcodeLayer.writePolygonsByOptimizer(storage.skirt, skirtConfig);
            }

            SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
            if (extruderChanged)
            {
                addWipeTower(storage, gcodeLayer, layerNr, prevExtruder, extrusionWidth);
            }

            if (storage.oozeShield.Count > 0 && storage.volumes.Count > 1)
            {
                gcodeLayer.setAlwaysRetract(true);
                gcodeLayer.writePolygonsByOptimizer(storage.oozeShield[layerNr], skirtConfig);
                LogOutput.logPolygons("oozeshield", layerNr, layer.printZ, storage.oozeShield[layerNr]);
                gcodeLayer.setAlwaysRetract(!config.enableCombing);
            }

            PathOrderOptimizer partOrderOptimizer = new PathOrderOptimizer(gcode.getPositionXY());
            for (int partNr = 0; partNr < layer.parts.Count; partNr++)
            {
                partOrderOptimizer.addPolygon(layer.parts[partNr].insets[0][0]);
            }
            partOrderOptimizer.optimize();

            for (int partCounter = 0; partCounter < partOrderOptimizer.polyOrder.Count; partCounter++)
            {
                SliceLayerPart part = layer.parts[partOrderOptimizer.polyOrder[partCounter]];

                if (config.enableCombing)
                {
                    gcodeLayer.setCombBoundary(part.combBoundery);
                }
                else
                {
                    gcodeLayer.setAlwaysRetract(true);
                }

                if (config.perimeterCount > 0)
                {
                    if (config.spiralizeMode)
                    {
                        if ((int)(layerNr) >= config.downSkinCount)
                        {
                            inset0Config.spiralize = true;
                        }

                        if ((int)(layerNr) == config.downSkinCount && part.insets.Count > 0)
                        {
                            gcodeLayer.writePolygonsByOptimizer(part.insets[0], insetXConfig);
                        }
                    }

                    for (int insetNr = part.insets.Count - 1; insetNr > -1; insetNr--)
                    {
                        if (insetNr == 0)
                        {
                            gcodeLayer.writePolygonsByOptimizer(part.insets[insetNr], inset0Config);
                        }
                        else
                        {
                            gcodeLayer.writePolygonsByOptimizer(part.insets[insetNr], insetXConfig);
                        }
                    }
                }

                Polygons fillPolygons = new Polygons();
                int fillAngle = config.infillAngleDegrees;
                if ((layerNr & 1) == 1)
                {
                    fillAngle += 90;
                }
                //int sparseSteps[1] = {extrusionWidth};
                //generateConcentricInfill(part.skinOutline, fillPolygons, sparseSteps, 1);
                Infill.generateLineInfill(part.skinOutline, fillPolygons, extrusionWidth, extrusionWidth, config.infillOverlapPercent, (part.bridgeAngle > -1) ? part.bridgeAngle : fillAngle);
                //int sparseSteps[2] = {extrusionWidth*5, extrusionWidth * 0.8};
                //generateConcentricInfill(part.sparseOutline, fillPolygons, sparseSteps, 2);
                if (config.sparseInfillLineDistance > 0)
                {
                    if (config.sparseInfillLineDistance > extrusionWidth * 4)
                    {
                        Infill.generateLineInfill(part.sparseOutline, fillPolygons, extrusionWidth, config.sparseInfillLineDistance * 2, config.infillOverlapPercent, fillAngle);
                        int fillAngle90 = fillAngle + 90;
                        if (fillAngle90 > 360)
                        {
                            fillAngle90 -= 360;
                        }

                        Infill.generateLineInfill(part.sparseOutline, fillPolygons, extrusionWidth, config.sparseInfillLineDistance * 2, config.infillOverlapPercent, fillAngle90);
                    }
                    else
                    {
                        Infill.generateLineInfill(part.sparseOutline, fillPolygons, extrusionWidth, config.sparseInfillLineDistance, config.infillOverlapPercent, fillAngle);
                    }
                }

                gcodeLayer.writePolygonsByOptimizer(fillPolygons, fillConfig);
                LogOutput.logPolygons("infill", layerNr, layer.printZ, fillPolygons);

                //After a layer part, make sure the nozzle is inside the comb boundary, so we do not retract on the perimeter.
                if (!config.spiralizeMode || (int)(layerNr) < config.downSkinCount)
                {
                    gcodeLayer.moveInsideCombBoundary(extrusionWidth * 2);
                }
            }
            gcodeLayer.setCombBoundary(null);
        }

        void addSupportToGCode(SliceDataStorage storage, GCodePlanner gcodeLayer, int layerNr, int extrusionWidth)
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
                    addWipeTower(storage, gcodeLayer, layerNr, prevExtruder, extrusionWidth);
                }

                if (storage.oozeShield.Count > 0 && storage.volumes.Count == 1)
                {
                    gcodeLayer.setAlwaysRetract(true);
                    gcodeLayer.writePolygonsByOptimizer(storage.oozeShield[layerNr], skirtConfig);
                    gcodeLayer.setAlwaysRetract(config.enableCombing);
                }
            }

            int z = config.initialLayerThickness_µm + layerNr * config.layerThickness_µm;
            SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, z);
            for (int volumeCnt = 0; volumeCnt < storage.volumes.Count; volumeCnt++)
            {
                SliceLayer layer = storage.volumes[volumeCnt].layers[layerNr];
                for (int n = 0; n < layer.parts.Count; n++)
                {
                    supportGenerator.polygons = supportGenerator.polygons.CreateDifference(layer.parts[n].outline.Offset(config.supportXYDistance));
                }
            }
            //Contract and expand the suppory polygons so small sections are removed and the final polygon is smoothed a bit.
            supportGenerator.polygons = supportGenerator.polygons.Offset(-extrusionWidth * 3);
            supportGenerator.polygons = supportGenerator.polygons.Offset(extrusionWidth * 3);
            LogOutput.logPolygons("support", layerNr, z, supportGenerator.polygons);

            List<Polygons> supportIslands = supportGenerator.polygons.SplitIntoParts();
            PathOrderOptimizer islandOrderOptimizer = new PathOrderOptimizer(gcode.getPositionXY());

            for (int n = 0; n < supportIslands.Count; n++)
            {
                islandOrderOptimizer.addPolygon(supportIslands[n][0]);
            }
            islandOrderOptimizer.optimize();
         
            for(int n=0; n<supportIslands.Count; n++)
            {
                Polygons island = supportIslands[islandOrderOptimizer.polyOrder[n]];
                Polygons supportLines = new Polygons();
                if (config.supportLineDistance > 0)
                {
                    switch(config.supportType)
                    {
                        case ConfigConstants.SUPPORT_TYPE.GRID:
                            if (config.supportLineDistance > extrusionWidth * 4)
                            {
                                Infill.generateLineInfill(island, supportLines, extrusionWidth, config.supportLineDistance * 2, config.infillOverlapPercent, 0);
                                Infill.generateLineInfill(island, supportLines, extrusionWidth, config.supportLineDistance * 2, config.infillOverlapPercent, 90);
                            }
                            else
                            {
                                Infill.generateLineInfill(island, supportLines, extrusionWidth, config.supportLineDistance, config.infillOverlapPercent, (layerNr & 1) == 1 ? 0 : 90);
                            }
                            break;

                        case ConfigConstants.SUPPORT_TYPE.LINES:
                            Infill.generateLineInfill(island, supportLines, extrusionWidth, config.supportLineDistance, config.infillOverlapPercent, 0);
                            break;
                    }                    
                }

                gcodeLayer.forceRetract();
                if (config.enableCombing)
                {
                    gcodeLayer.setCombBoundary(island);
                }

                gcodeLayer.writePolygonsByOptimizer(island, supportConfig);
                gcodeLayer.writePolygonsByOptimizer(supportLines, supportConfig);
                gcodeLayer.setCombBoundary(null);
            }
        }

        void addWipeTower(SliceDataStorage storage, GCodePlanner gcodeLayer, int layerNr, int prevExtruder, int extrusionWidth)
        {
            if (config.wipeTowerSize < 1)
            {
                return;
            }

            //If we changed extruder, print the wipe/prime tower for this nozzle;
            gcodeLayer.writePolygonsByOptimizer(storage.wipeTower, supportConfig);
            Polygons fillPolygons = new Polygons();
            Infill.generateLineInfill(storage.wipeTower, fillPolygons, extrusionWidth, extrusionWidth, config.infillOverlapPercent, 45 + 90 * (layerNr % 2));
            gcodeLayer.writePolygonsByOptimizer(fillPolygons, supportConfig);

            //Make sure we wipe the old extruder on the wipe tower.
            gcodeLayer.writeTravel(storage.wipePoint - config.extruderOffsets[prevExtruder] + config.extruderOffsets[gcodeLayer.getExtruder()]);
        }
    }
}