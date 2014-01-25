/*
Copyright (c) 2013, Lars Brubaker

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

using MatterHackers.MatterSlice;

namespace MatterHackers.MatterSlice
{
    public class SliceItApp
    {
        const string VERSION = "0.0.1";

        int verbose_level;
        int maxObjectHeight;

        void processFile(string input_filename, ConfigSettings config, GCodeExport gcode, bool firstFile)
        {
            for (int n = 1; n < 16; n++)
            {
                gcode.setExtruderOffset(n, config.extruderOffset[n]);
            }
            gcode.setFlavor(config.gcodeFlavor);

            Stopwatch t = new Stopwatch();
            t.Start();
            Utilities.log(string.Format("Loading {0} from disk...\n", input_filename));
            SimpleModel loadedMesh = SimpleModel.loadModel(input_filename, config.matrix);
            if (loadedMesh == null)
            {
                Utilities.log(string.Format("Failed to load model: {0}\n", input_filename));
                return;
            }
            //loadedMesh.saveModelSTL("original after load.stl");
            Utilities.log(string.Format("Loaded from disk in {0:0.000}s\n", t.Elapsed.Seconds));
            Utilities.log("Analyzing and optimizing model...\n");
            OptimizedModel optomizedModel = new OptimizedModel(loadedMesh, new Point3(config.objectPosition.X, config.objectPosition.Y, -config.objectSink));
            for (int v = 0; v < loadedMesh.volumes.Count; v++)
            {
                Utilities.log(string.Format("  Face counts: loaded:{0} after optomized:{1} {2:0.0}%\n", loadedMesh.volumes[v].faces.Count, optomizedModel.volumes[v].faces.Count, (float)(optomizedModel.volumes[v].faces.Count) / (float)(loadedMesh.volumes[v].faces.Count) * 100));
                Utilities.log(string.Format("  Vertex counts: loaded:{0} optomized:{1} {2:0.0}%\n", (int)loadedMesh.volumes[v].faces.Count * 3, (int)optomizedModel.volumes[v].points.Count, (float)(optomizedModel.volumes[v].points.Count) / (float)(loadedMesh.volumes[v].faces.Count * 3) * 100));
            }
#if !DEBUG
            // allow the memory to be freed
            loadedMesh = null;
#endif
            Utilities.log(string.Format("Optimize model {0:0.000}s \n", t.Elapsed.Seconds));
            //om.saveDebugSTL("output.stl");

            Utilities.log("Slicing model...\n");
            List<Slicer> slicerList = new List<Slicer>();
            for (int volumeIdx = 0; volumeIdx < optomizedModel.volumes.Count; volumeIdx++)
            {
                bool extensiveStitching = (config.fixHorrible & ConfigSettings.CorrectionType.FIX_HORRIBLE_EXTENSIVE_STITCHING) == ConfigSettings.CorrectionType.FIX_HORRIBLE_EXTENSIVE_STITCHING;
                bool keepNonClosed = (config.fixHorrible & ConfigSettings.CorrectionType.FIX_HORRIBLE_KEEP_NONE_CLOSED) == ConfigSettings.CorrectionType.FIX_HORRIBLE_KEEP_NONE_CLOSED;
                slicerList.Add(new Slicer(optomizedModel.volumes[volumeIdx], config.initialLayerThickness / 2, config.layerThickness, keepNonClosed, extensiveStitching));
                slicerList[volumeIdx].dumpSegments("segments_output.html");
            }
            Utilities.log(string.Format("Sliced model in {0:0.000}s\n", t.Elapsed.Seconds));

            SliceDataStorage storage = new SliceDataStorage();
            if (config.supportAngle > -1)
            {
                Console.WriteLine("Generating support map...\n");
                SupportStorage.generateSupportGrid(storage.support, optomizedModel);
            }
            storage.modelSize = optomizedModel.modelSize;
            storage.modelMin = optomizedModel.vMin;
            storage.modelMax = optomizedModel.vMax;
            optomizedModel = null;

            Utilities.log("Generating layer parts...\n");
            for (int volumeIdx = 0; volumeIdx < slicerList.Count; volumeIdx++)
            {
                storage.volumes.Add(new SliceVolumeStorage());
                SliceVolumeStorage.createLayerParts(storage.volumes[volumeIdx], slicerList[volumeIdx], config.fixHorrible & (ConfigSettings.CorrectionType.FIX_HORRIBLE_UNION_ALL_TYPE_A | ConfigSettings.CorrectionType.FIX_HORRIBLE_UNION_ALL_TYPE_B));
                slicerList[volumeIdx] = null;
            }
            //carveMultipleVolumes(storage.volumes);
            multiValumes.generateMultipleVolumesOverlap(storage.volumes, config.multiVolumeOverlap);
            Utilities.log(string.Format("Generated layer parts in {0}s\n", t.Elapsed.Seconds));
            SliceDataStorage.dumpLayerparts(storage, "layer parts.html");

            int totalLayers = storage.volumes[0].layers.Count;
            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                {
                    inset.generateInsets(storage.volumes[volumeIdx].layers[layerNr], config.extrusionWidth, config.insetCount);
                }
                Utilities.logProgress("inset", layerNr + 1, totalLayers);
            }
            Utilities.log(string.Format("Generated inset in {0:0.000}s\n", t.Elapsed.Seconds));
            SliceDataStorage.dumpLayerparts(storage, "insets .html");

            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                {
                    Skin.generateSkins(layerNr, storage.volumes[volumeIdx], config.extrusionWidth, config.downSkinCount, config.upSkinCount, config.infillOverlap);
                    Skin.generateSparse(layerNr, storage.volumes[volumeIdx], config.extrusionWidth, config.downSkinCount, config.upSkinCount);
                }
                Utilities.logProgress("skin", layerNr + 1, totalLayers);
            }
            Utilities.log(string.Format("Generated up/down skin in {0:0.000}s\n", t.Elapsed.Seconds));
            Skirt.generateSkirt(storage, config.skirtDistance, config.extrusionWidth, config.skirtLineCount, config.skirtMinLength);
            Raft.generateRaft(storage, config.raftMargin, config.supportAngle, config.supportEverywhere > 0, config.supportXYDistance);

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

            gcode.setRetractionSettings(config.retractionAmount, config.retractionSpeed, config.retractionAmountExtruderSwitch, config.minimalExtrusionBeforeRetraction);
            if (firstFile)
            {
                if (gcode.getFlavor() == ConfigSettings.GCodeFlavor.GCODE_FLAVOR_ULTIGCODE)
                {
                    gcode.addCode(";FLAVOR:UltiGCode");
                }
                else
                {
                    gcode.addCode(config.startCode);
                }
            }
            else
            {
                gcode.addFanCommand(0);
                gcode.resetExtrusionValue();
                gcode.addRetraction();
                gcode.setZ(maxObjectHeight + 5000);
                gcode.addMove(new IntPoint(storage.modelMin.x, storage.modelMin.y), config.moveSpeed, 0);
            }
            gcode.addComment(string.Format("total_layers={0}", totalLayers));

            GCodePathConfig skirtConfig = new GCodePathConfig(config.printSpeed, config.extrusionWidth, "SKIRT");
            GCodePathConfig inset0Config = new GCodePathConfig(config.printSpeed, config.extrusionWidth, "WALL-OUTER");
            GCodePathConfig inset1Config = new GCodePathConfig(config.printSpeed, config.extrusionWidth, "WALL-INNER");
            GCodePathConfig fillConfig = new GCodePathConfig(config.infillSpeed, config.extrusionWidth, "FILL");
            GCodePathConfig supportConfig = new GCodePathConfig(config.printSpeed, config.extrusionWidth, "SUPPORT");

            if (config.raftBaseThickness > 0 && config.raftInterfaceThickness > 0)
            {
                GCodePathConfig raftBaseConfig = new GCodePathConfig(config.initialLayerSpeed, config.raftBaseLinewidth, "SUPPORT");
                GCodePathConfig raftInterfaceConfig = new GCodePathConfig(config.initialLayerSpeed, config.raftInterfaceLinewidth, "SUPPORT");
                {
                    gcode.addComment("LAYER:-2");
                    gcode.addComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.moveSpeed, config.retractionMinimalDistance);
                    gcode.setZ(config.raftBaseThickness);
                    gcode.setExtrusion(config.raftBaseThickness, config.filamentDiameter, config.filamentFlow);
                    gcodeLayer.addPolygonsByOptimizer(storage.raftOutline, raftBaseConfig);

                    Polygons raftLines = new Polygons();
                    infill.generateLineInfill(storage.raftOutline, raftLines, config.raftBaseLinewidth, config.raftLineSpacing, config.infillOverlap, 0);
                    gcodeLayer.addPolygonsByOptimizer(raftLines, raftBaseConfig);

                    gcodeLayer.writeGCode(false);
                }

                {
                    gcode.addComment("LAYER:-1");
                    gcode.addComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.moveSpeed, config.retractionMinimalDistance);
                    gcode.setZ(config.raftBaseThickness + config.raftInterfaceThickness);
                    gcode.setExtrusion(config.raftInterfaceThickness, config.filamentDiameter, config.filamentFlow);

                    Polygons raftLines = new Polygons();
                    infill.generateLineInfill(storage.raftOutline, raftLines, config.raftInterfaceLinewidth, config.raftLineSpacing, config.infillOverlap, 90);
                    gcodeLayer.addPolygonsByOptimizer(raftLines, raftInterfaceConfig);

                    gcodeLayer.writeGCode(false);
                }
            }

            int volumeIdx2 = 0;
            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                Utilities.logProgress("export", layerNr + 1, totalLayers);

                GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.moveSpeed, config.retractionMinimalDistance);
                gcode.addComment(string.Format("LAYER:{0}", layerNr));
                int z = config.initialLayerThickness + layerNr * config.layerThickness;
                z += config.raftBaseThickness + config.raftInterfaceThickness;
                gcode.setZ(z);
                if (layerNr == 0)
                {
                    gcodeLayer.addPolygonsByOptimizer(storage.skirt, skirtConfig);
                }

                for (int volumeCnt = 0; volumeCnt < storage.volumes.Count; volumeCnt++)
                {
                    if (volumeCnt > 0)
                    {
                        volumeIdx2 = (volumeIdx2 + 1) % storage.volumes.Count;
                    }

                    SliceLayer layer = storage.volumes[volumeIdx2].layers[layerNr];
                    gcodeLayer.setExtruder(volumeIdx2);

                    PathOptimizer partOrderOptimizer = new PathOptimizer(gcode.getPositionXY());
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

                        gcodeLayer.forceRetract();
                        if (config.insetCount > 0)
                        {
                            for (int insetNr = part.insets.Count - 1; insetNr > -1; insetNr--)
                            {
                                if (insetNr == 0)
                                {
                                    gcodeLayer.addPolygonsByOptimizer(part.insets[insetNr], inset0Config);
                                }
                                else
                                {
                                    gcodeLayer.addPolygonsByOptimizer(part.insets[insetNr], inset1Config);
                                }
                            }
                        }

                        Polygons fillPolygons = new Polygons();
                        int fillAngle = 45;
                        if ((layerNr & 0x1) == 1)
                        {
                            fillAngle += 90;
                        }

                        //int sparseSteps[1] = {config.extrusionWidth};
                        //generateConcentricInfill(part.skinOutline, fillPolygons, sparseSteps, 1);
                        infill.generateLineInfill(part.skinOutline, fillPolygons, config.extrusionWidth, config.extrusionWidth, config.infillOverlap, (part.bridgeAngle > -1) ? part.bridgeAngle : fillAngle);
                        //int sparseSteps[2] = {config.extrusionWidth*5, config.extrusionWidth * 0.8};
                        //generateConcentricInfill(part.sparseOutline, fillPolygons, sparseSteps, 2);
                        if (config.sparseInfillLineDistance > 0)
                        {
                            if (config.sparseInfillLineDistance > config.extrusionWidth * 4)
                            {
                                infill.generateLineInfill(part.sparseOutline, fillPolygons, config.extrusionWidth, config.sparseInfillLineDistance * 2, config.infillOverlap, 45);
                                infill.generateLineInfill(part.sparseOutline, fillPolygons, config.extrusionWidth, config.sparseInfillLineDistance * 2, config.infillOverlap, 45 + 90);
                            }
                            else
                            {
                                infill.generateLineInfill(part.sparseOutline, fillPolygons, config.extrusionWidth, config.sparseInfillLineDistance, config.infillOverlap, fillAngle);
                            }
                        }

                        gcodeLayer.addPolygonsByOptimizer(fillPolygons, fillConfig);

                        //After a layer part, make sure the nozzle is inside the comb boundary, so we do not retract on the perimeter.
                        gcodeLayer.moveInsideCombBoundary();
                    }
                    gcodeLayer.setCombBoundary(null);
                }
                if (config.supportAngle > -1)
                {
                    if (config.supportExtruder > -1)
                        gcodeLayer.setExtruder(config.supportExtruder);
                    SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, z, config.supportAngle, config.supportEverywhere > 0, config.supportXYDistance, config.supportZDistance);

                    Polygons supportLines = new Polygons();
                    if (config.supportLineDistance > 0)
                    {
                        if (config.supportLineDistance > config.extrusionWidth * 4)
                        {
                            infill.generateLineInfill(supportGenerator.polygons, supportLines, config.extrusionWidth, config.supportLineDistance * 2, config.infillOverlap, 0);
                            infill.generateLineInfill(supportGenerator.polygons, supportLines, config.extrusionWidth, config.supportLineDistance * 2, config.infillOverlap, 90);
                        }
                        else
                        {
                            infill.generateLineInfill(supportGenerator.polygons, supportLines, config.extrusionWidth, config.supportLineDistance, config.infillOverlap, (layerNr & 1) != 0 ? 0 : 90);
                        }
                    }

                    gcodeLayer.addPolygonsByOptimizer(supportGenerator.polygons, supportConfig);
                    gcodeLayer.addPolygonsByOptimizer(supportLines, supportConfig);
                }

                //Finish the layer by applying speed corrections for minimal layer times and slowdown for the initial layer.
                if ((int)layerNr < config.initialSpeedupLayers)
                {
                    int n = config.initialSpeedupLayers;
                    int layer0Factor = config.initialLayerSpeed * 100 / config.printSpeed;
                    gcodeLayer.setExtrudeSpeedFactor((layer0Factor * (n - layerNr) + 100 * (layerNr)) / n);
                }
                gcodeLayer.forceMinimalLayerTime(config.minimalLayerTime, config.minimalFeedrate);
                if (layerNr == 0)
                {
                    gcode.setExtrusion(config.initialLayerThickness, config.filamentDiameter, config.filamentFlow);
                }
                else
                {
                    gcode.setExtrusion(config.layerThickness, config.filamentDiameter, config.filamentFlow);
                }

                if ((int)layerNr >= config.fanOnLayerNr)
                {
                    double speed = config.fanSpeedMin;
                    if (gcodeLayer.getExtrudeSpeedFactor() <= 50)
                    {
                        speed = config.fanSpeedMax;
                    }
                    else
                    {
                        double n = gcodeLayer.getExtrudeSpeedFactor() - 50;
                        speed = config.fanSpeedMin * n / 50 + config.fanSpeedMax * (50 - n) / 50;
                    }

                    gcode.addFanCommand((int)speed);
                }
                else
                {
                    gcode.addFanCommand(0);
                }
                gcodeLayer.writeGCode(config.coolHeadLift > 0);
            }

            /* support debug
            for(int y=0; y<storage.support.gridHeight; y++)
            {
                for(int x=0; x<storage.support.gridWidth; x++)
                {
                    uint n = x+y*storage.support.gridWidth;
                    if (storage.support.grid[n].Count < 1) continue;
                    int z = storage.support.grid[n][0].z;
                    gcode.addMove(Point3(x * storage.support.gridScale + storage.support.gridOffset.X, y * storage.support.gridScale + storage.support.gridOffset.Y, 0), 0);
                    gcode.addMove(Point3(x * storage.support.gridScale + storage.support.gridOffset.X, y * storage.support.gridScale + storage.support.gridOffset.Y, z), z);
                    gcode.addMove(Point3(x * storage.support.gridScale + storage.support.gridOffset.X, y * storage.support.gridScale + storage.support.gridOffset.Y, 0), 0);
                }
            }
            //*/

            Utilities.log(string.Format("Wrote layers in {0:0.00}s.\n", t.Elapsed.Seconds));
            gcode.tellFileSize();
            gcode.addFanCommand(0);

            Utilities.logProgress("process", 1, 1);
            Utilities.log(string.Format("Total time elapsed {0:0.00}s.\n", t.Elapsed.Seconds));

            //Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
            maxObjectHeight = Math.Max(maxObjectHeight, storage.modelSize.z);
        }

        void print_usage()
        {
            Console.WriteLine("usage: MatterSlice [-h] [-v] [-m 3x3matrix] [-s <settingkey>=<value>] -o <output.gcode> <model.stl>");
        }

        int RunTheApp(string[] args)
        {
            GCodeExport gcode = new GCodeExport();
            ConfigSettings config = new ConfigSettings();
            int fileNr = 0;

            config.filamentDiameter = 2890;
            config.filamentFlow = 100;
            config.initialLayerThickness = 300;
            config.layerThickness = 100;
            config.extrusionWidth = 400;
            config.insetCount = 2;
            config.downSkinCount = 6;
            config.upSkinCount = 6;
            config.initialSpeedupLayers = 4;
            config.initialLayerSpeed = 20;
            config.printSpeed = 50;
            config.infillSpeed = 50;
            config.moveSpeed = 200;
            config.fanOnLayerNr = 2;
            config.skirtDistance = 6000;
            config.skirtLineCount = 1;
            config.skirtMinLength = 0;
            config.sparseInfillLineDistance = 100 * config.extrusionWidth / 20;
            config.infillOverlap = 15;
            config.objectPosition.X = 102500;
            config.objectPosition.Y = 102500;
            config.objectSink = 0;
            config.supportAngle = -1;
            config.supportEverywhere = 0;
            config.supportLineDistance = config.sparseInfillLineDistance;
            config.supportExtruder = -1;
            config.supportXYDistance = 700;
            config.supportZDistance = 150;
            config.retractionAmount = 4500;
            config.retractionSpeed = 45;
            config.retractionAmountExtruderSwitch = 14500;
            config.retractionMinimalDistance = 1500;
            config.minimalExtrusionBeforeRetraction = 100;
            config.enableCombing = true;
            config.multiVolumeOverlap = 0;

            config.minimalLayerTime = 5;
            config.minimalFeedrate = 10;
            config.coolHeadLift = 1;
            config.fanSpeedMin = 100;
            config.fanSpeedMax = 100;

            config.raftMargin = 5000;
            config.raftLineSpacing = 1000;
            config.raftBaseThickness = 0;
            config.raftBaseLinewidth = 0;
            config.raftInterfaceThickness = 0;
            config.raftInterfaceLinewidth = 0;

            config.fixHorrible = 0;
            config.gcodeFlavor = ConfigSettings.GCodeFlavor.GCODE_FLAVOR_REPRAP;

            config.startCode =
                "M109 S210     ;Heatup to 210C\n" +
                "G21           ;metric values\n" +
                "G90           ;absolute positioning\n" +
                "G28           ;Home\n" +
                "G1 Z15.0 F300 ;move the platform down 15mm\n" +
                "G92 E0        ;zero the extruded length\n" +
                "G1 F200 E5    ;extrude 5mm of feed stock\n" +
                "G92 E0        ;zero the extruded length again\n";
            config.endCode =
                "M104 S0                     ;extruder heater off\n" +
                "M140 S0                     ;heated bed heater off (if you have it)\n" +
                "G91                            ;relative positioning\n" +
                "G1 E-1 F300                    ;retract the filament a bit before lifting the nozzle, to release some of the pressure\n" +
                "G1 Z+0.5 E-5 X-20 Y-20 F9000   ;move Z up a bit and retract filament even more\n" +
                "G28 X0 Y0                      ;move X/Y to min endstops, so the head is out of the way\n" +
                "M84                         ;steppers off\n" +
                "G90                         ;absolute positioning\n";

            Console.WriteLine(string.Format("MatterSlice version {0}", VERSION));

            if (args.Length == 0)
            {
                print_usage();
                return 0;
            }

            for (int argn = 0; argn < args.Length; argn++)
            {
                string str = args[argn];
                if (str[0] == '-')
                {
                    for (int stringIndex = 1; stringIndex < str.Length; stringIndex++)
                    {
                        switch (str[stringIndex])
                        {
                            case 'h':
                                print_usage();
                                return 0;

                            case 'v':
                                verbose_level++;
                                break;

                            case 'b':
                                argn++;
                                throw new NotImplementedException();
#if false
                                binaryMeshBlob = fopen(args[argn], "rb");
#endif
                                break;

                            case 'o':
                                argn++;
                                gcode.setFilename(args[argn]);
                                if (!gcode.isValid())
                                {
                                    Utilities.logError(string.Format("Failed to open %s for output.\n", args[argn]));
                                    return 1;
                                }
                                gcode.addComment(string.Format("Generated with MaterSlice {0}", VERSION));
                                break;

                            case 's':
                                {
                                    throw new NotImplementedException();
#if false
                                    argn++;
                                    char* valuePtr = strchr(args[argn], '=');
                                    if (valuePtr)
                                    {
                                        *valuePtr++ = '\0';

                                        if (!config.setSetting(args[argn], valuePtr))
                                        {
                                            Output(string.Format("Setting found: {0}\n", args[argn]));
                                        }
                                    }
#endif
                                }
                                break;

                            case 'm':
                                argn++;
                                throw new NotImplementedException();
#if false
                                sscanf(args[argn], "%lf,%lf,%lf,%lf,%lf,%lf,%lf,%lf,%lf",
                                    &config.matrix.m[0][0], &config.matrix.m[0][1], &config.matrix.m[0][2],
                                    &config.matrix.m[1][0], &config.matrix.m[1][1], &config.matrix.m[1][2],
                                    &config.matrix.m[2][0], &config.matrix.m[2][1], &config.matrix.m[2][2]);
#endif
                                break;

                            default:
                                Utilities.logError(string.Format("Unknown option: {0}\n", str));
                                break;
                        }
                    }
                }
                else
                {
                    if (!gcode.isValid())
                    {
                        Utilities.logError("No output file specified\n");
                        return 1;
                    }

                    processFile(args[argn], config, gcode, fileNr == 0);
                    fileNr++;
                }
            }
            if (gcode.isValid())
            {
                gcode.addFanCommand(0);
                gcode.setZ(maxObjectHeight + 5000);
                gcode.addCode(config.endCode);
                Utilities.log(string.Format("Print time: {0}\n", gcode.getTotalPrintTime()));
                Utilities.log(string.Format("Filament: {0}\n", gcode.getTotalFilamentUsed()));
            }
            return 0;
        }

        static int Main(string[] args)
        {
            SliceItApp app = new SliceItApp();
            int status = app.RunTheApp(args);
            Console.Read();
            return status;
        }
    }
}