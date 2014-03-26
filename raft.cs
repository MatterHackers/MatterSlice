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

    public static class Raft
    {
        public static void GenerateRaftOutlines(SliceDataStorage storage, int extraDistanceAroundPart_µm)
        {
            for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
            {
                if (storage.volumes[volumeIdx].layers.Count < 1)
                {
                    continue;
                }

                SliceLayer layer = storage.volumes[volumeIdx].layers[0];
                for (int i = 0; i < layer.parts.Count; i++)
                {
                    storage.raftOutline = storage.raftOutline.CreateUnion(layer.parts[i].outline.Offset(extraDistanceAroundPart_µm));
                }
            }

            SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, 0);
            storage.raftOutline = storage.raftOutline.CreateUnion(storage.wipeTower.Offset(extraDistanceAroundPart_µm));
            storage.raftOutline = storage.raftOutline.CreateUnion(supportGenerator.polygons.Offset(extraDistanceAroundPart_µm));
        }

        public static void GenerateRaftGCodeIfRequired(SliceDataStorage storage, ConfigSettings config, GCodeExport gcode)
        {
            if (config.raftBaseThickness_µm > 0 && config.raftInterfaceThicknes_µm > 0)
            {
                GCodePathConfig raftBaseConfig = new GCodePathConfig(config.firstLayerSpeed, config.raftBaseThickness_µm, "SUPPORT");
                GCodePathConfig raftMiddleConfig = new GCodePathConfig(config.raftPrintSpeed, config.raftInterfaceLinewidth_µm, "SUPPORT");
                GCodePathConfig raftInterfaceConfig = new GCodePathConfig(config.raftPrintSpeed, config.raftInterfaceLinewidth_µm, "SUPPORT");
                GCodePathConfig raftSurfaceConfig = new GCodePathConfig((config.raftSurfacePrintSpeed > 0) ? config.raftSurfacePrintSpeed : config.raftPrintSpeed, config.raftSurfaceLinewidth, "SUPPORT");

                {
                    gcode.writeComment("LAYER:-2");
                    gcode.writeComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_µm);
                    if (config.supportExtruder > 0)
                    {
                        gcodeLayer.setExtruder(config.supportExtruder);
                    }

                    gcodeLayer.setAlwaysRetract(true);
                    gcode.setZ(config.raftBaseThickness_µm);
                    gcode.setExtrusion(config.raftBaseThickness_µm, config.filamentDiameter_µm, config.extrusionMultiplier);
                    gcodeLayer.writePolygonsByOptimizer(storage.raftOutline, raftBaseConfig);

                    Polygons raftLines = new Polygons();
                    Infill.generateLineInfill(storage.raftOutline, raftLines, config.raftBaseThickness_µm, config.raftLineSpacing_µm, config.infillOverlapPerimeter_µm, 0);
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftBaseConfig);

                    gcodeLayer.writeGCode(false, config.raftBaseThickness_µm);
                }

                if (config.raftFanSpeedPercent > 0)
                {
                    gcode.writeFanCommand(config.raftFanSpeedPercent);
                }

                {
                    gcode.writeComment("LAYER:-1");
                    gcode.writeComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_µm);
                    gcodeLayer.setAlwaysRetract(true);
                    gcode.setZ(config.raftBaseThickness_µm + config.raftInterfaceThicknes_µm);
                    gcode.setExtrusion(config.raftInterfaceThicknes_µm, config.filamentDiameter_µm, config.extrusionMultiplier);

                    Polygons raftLines = new Polygons();
                    Infill.generateLineInfill(storage.raftOutline, raftLines, config.raftInterfaceLinewidth_µm, config.raftInterfaceLineSpacing, config.infillOverlapPerimeter_µm, 45);
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftInterfaceConfig);

                    gcodeLayer.writeGCode(false, config.raftInterfaceThicknes_µm);
                }

                for (int raftSurfaceLayer = 1; raftSurfaceLayer <= config.raftSurfaceLayers; raftSurfaceLayer++)
                {
                    gcode.writeComment("LAYER:FullRaft");
                    gcode.writeComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_µm);
                    gcodeLayer.setAlwaysRetract(true);
                    gcode.setZ(config.raftBaseThickness_µm + config.raftInterfaceThicknes_µm + config.raftSurfaceThickness * raftSurfaceLayer);
                    gcode.setExtrusion(config.raftSurfaceThickness, config.filamentDiameter_µm, config.extrusionMultiplier);

                    Polygons raftLines = new Polygons();
                    Infill.generateLineInfill(storage.raftOutline, raftLines, config.raftSurfaceLinewidth, config.raftSurfaceLineSpacing, config.infillOverlapPerimeter_µm, 90);
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftSurfaceConfig);

                    gcodeLayer.writeGCode(false, config.raftInterfaceThicknes_µm);
                }
            }
        }
    }
}