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
using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public class ConfigSettings
    {
        [Flags]
        public enum CorrectionType
        {
            NONE = 0x01,
            FIX_HORRIBLE_UNION_ALL_TYPE_A = 0x01,
            FIX_HORRIBLE_UNION_ALL_TYPE_B = 0x02,
            FIX_HORRIBLE_EXTENSIVE_STITCHING = 0x04,
            FIX_HORRIBLE_UNION_ALL_TYPE_C = 0x08,
            FIX_HORRIBLE_KEEP_NONE_CLOSED = 0x10,
        };

        /**
         * RepRap flavored GCode is Marlin/Sprinter/Repetier based GCode. 
         *  This is the most commonly used GCode set.
         *  G0 for moves, G1 for extrusion.
         *  E values give mm of filament extrusion.
         *  Retraction is done on E values with G1. Start/end code is added.
         *  M106 Sxxx and M107 are used to turn the fan on/off.
         **/

        /**
     * UltiGCode flavored is Marlin based GCode. 
     *  UltiGCode uses less settings on the slicer and puts more settings in the firmware. This makes for more hardware/material independed GCode.
     *  G0 for moves, G1 for extrusion.
     *  E values give mm^3 of filament extrusion. Ignores the filament diameter setting.
     *  Retraction is done with G10 and G11. Retraction settings are ignored. G10 S1 is used for multi-extruder switch retraction.
     *  Start/end code is not added.
     *  M106 Sxxx and M107 are used to turn the fan on/off.
     **/

        public enum GCodeFlavor { GCODE_FLAVOR_REPRAP, GCODE_FLAVOR_ULTIGCODE };

        Dictionary<string, string> _index;
        public int layerThickness;
        public int initialLayerThickness;
        public int filamentDiameter;
        public int filamentFlow;
        public int extrusionWidth;
        public int insetCount;
        public int downSkinCount;
        public int upSkinCount;
        public int sparseInfillLineDistance;
        public int infillOverlap;
        public int skirtDistance;
        public int skirtLineCount;
        public int skirtMinLength;
        public int retractionAmount;
        public int retractionAmountExtruderSwitch;
        public int retractionSpeed;
        public int retractionMinimalDistance;
        public int minimalExtrusionBeforeRetraction;
        public bool enableCombing;
        public int multiVolumeOverlap;

        public int initialSpeedupLayers;
        public int initialLayerSpeed;
        public int printSpeed;
        public int infillSpeed;
        public int moveSpeed;
        public int fanOnLayerNr;

        //Support material
        public int supportAngle;
        public int supportEverywhere;
        public int supportLineDistance;
        public int supportXYDistance;
        public int supportZDistance;
        public int supportExtruder;

        //Cool settings
        public int minimalLayerTime;
        public int minimalFeedrate;
        public int coolHeadLift;
        public int fanSpeedMin;
        public int fanSpeedMax;

        //Raft settings
        public int raftMargin;
        public int raftLineSpacing;
        public int raftBaseThickness;
        public int raftBaseLinewidth;
        public int raftInterfaceThickness;
        public int raftInterfaceLinewidth;

        public FMatrix3x3 matrix = new FMatrix3x3();
        public IntPoint objectPosition;
        public int objectSink;

        public ConfigSettings.CorrectionType fixHorrible;
        public GCodeFlavor gcodeFlavor;

        public IntPoint[] extruderOffset = new IntPoint[16];
        public string startCode;
        public string endCode;

        public ConfigSettings()
        {
        }

        public bool setSetting(string key, string value)
        {
            throw new NotImplementedException();
        }
    }
}