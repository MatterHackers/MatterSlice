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

    public static class Raft
    {
        public static void GenerateRaftOutlines(SliceDataStorage storage, int extraDistanceAroundPart_um)
        {
            for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
            {
                if (storage.volumes[volumeIdx].layers.Count < 1)
                {
                    continue;
                }

                SliceLayer layer = storage.volumes[volumeIdx].layers[0];
				// let's find the first layer that has something in it for the raft rather than a zero layer
				if (layer.parts.Count == 0 && storage.volumes[volumeIdx].layers.Count > 2) layer = storage.volumes[volumeIdx].layers[1];
                for (int i = 0; i < layer.parts.Count; i++)
                {
                    storage.raftOutline = storage.raftOutline.CreateUnion(layer.parts[i].outline.Offset(extraDistanceAroundPart_um));
                }
            }

            SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, 0);
            storage.raftOutline = storage.raftOutline.CreateUnion(storage.wipeTower.Offset(extraDistanceAroundPart_um));
            storage.raftOutline = storage.raftOutline.CreateUnion(supportGenerator.supportPolygons.Offset(extraDistanceAroundPart_um));
        }

        public static bool ShouldGenerateRaft(ConfigSettings config)
        {
            return config.enableRaft
                && config.raftBaseThickness_um > 0
                && config.raftInterfaceThicknes_um > 0;
        }

        public static void GenerateRaftGCodeIfRequired(SliceDataStorage storage, ConfigSettings config, GCodeExport gcode)
        {
            if (ShouldGenerateRaft(config))
            {
                GCodePathConfig raftBaseConfig = new GCodePathConfig(config.firstLayerSpeed, config.raftBaseExtrusionWidth_um, "SUPPORT");
                GCodePathConfig raftMiddleConfig = new GCodePathConfig(config.raftPrintSpeed, config.raftInterfaceExtrusionWidth_um, "SUPPORT");
                GCodePathConfig raftSurfaceConfig = new GCodePathConfig((config.raftSurfacePrintSpeed > 0) ? config.raftSurfacePrintSpeed : config.raftPrintSpeed, config.raftSurfaceExtrusionWidth_um, "SUPPORT");

                // create the raft base
                {
                    gcode.writeComment("LAYER:-3");
                    gcode.writeComment("RAFT BASE");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_um);
                    if (config.raftExtruder > 0)
                    {
                        // if we have a specified raft extruder use it
                        gcodeLayer.setExtruder(config.raftExtruder);
                    }
                    else if (config.supportExtruder > 0)
                    {
                        // else preserve the old behavior of using the support extruder if set.
                        gcodeLayer.setExtruder(config.supportExtruder);
                    }

                    gcode.setZ(config.raftBaseThickness_um);
                    gcode.setExtrusion(config.raftBaseThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);
                    gcodeLayer.writePolygonsByOptimizer(storage.raftOutline, raftBaseConfig);

                    Polygons raftLines = new Polygons();
                    Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftBaseLineSpacing_um, config.infillExtendIntoPerimeter_um, 0);
                    gcodeLayer.writePolygonsByOptimizer(storage.skirt, raftBaseConfig);
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftBaseConfig);

                    gcodeLayer.writeGCode(false, config.raftBaseThickness_um);
                }

                if (config.raftFanSpeedPercent > 0)
                {
                    gcode.writeFanCommand(config.raftFanSpeedPercent);
                }

                // raft middle layers
                {
                    gcode.writeComment("LAYER:-2");
                    gcode.writeComment("RAFT MIDDLE");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_um);
                    gcode.setZ(config.raftBaseThickness_um + config.raftInterfaceThicknes_um);
                    gcode.setExtrusion(config.raftInterfaceThicknes_um, config.filamentDiameter_um, config.extrusionMultiplier);

                    Polygons raftLines = new Polygons();
                    Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftInterfaceLineSpacing_um, config.infillExtendIntoPerimeter_um, 45);
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftMiddleConfig);

                    gcodeLayer.writeGCode(false, config.raftInterfaceThicknes_um);
                }

                for (int raftSurfaceIndex = 1; raftSurfaceIndex <= config.raftSurfaceLayers; raftSurfaceIndex++)
                {
                    gcode.writeComment("LAYER:-1");
                    gcode.writeComment("RAFT SURFACE");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_um);
                    gcode.setZ(config.raftBaseThickness_um + config.raftInterfaceThicknes_um + config.raftSurfaceThickness_um * raftSurfaceIndex);
                    gcode.setExtrusion(config.raftSurfaceThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);

                    Polygons raftLines = new Polygons();
                    if (raftSurfaceIndex == config.raftSurfaceLayers)
                    {
                        // make sure the top layer of the raft is 90 degrees offset to the first layer of the part so that it has minimum contact points.
                        Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftSurfaceLineSpacing_um, config.infillExtendIntoPerimeter_um, config.infillStartingAngle + 90);
                    }
                    else
                    {
                        Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftSurfaceLineSpacing_um, config.infillExtendIntoPerimeter_um, 90 * raftSurfaceIndex);
                    }
                    gcodeLayer.writePolygonsByOptimizer(raftLines, raftSurfaceConfig);

                    gcodeLayer.writeGCode(false, config.raftInterfaceThicknes_um);
                }
            }
        }
    }
}