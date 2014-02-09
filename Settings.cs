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
using System.Linq;
using System.Text;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public class _ConfigSettingIndex
    {
        public string key;
        int ptr;

        _ConfigSettingIndex(string key, int ptr)
        {
            this.key = key;
            this.ptr = ptr;
            throw new NotImplementedException();
        }
    }

    public class ConfigSettings
    {
        public const string VERSION = "13.12";

        public const int FIX_HORRIBLE_UNION_ALL_TYPE_A = 0x01;
        public const int FIX_HORRIBLE_UNION_ALL_TYPE_B = 0x02;
        public const int FIX_HORRIBLE_EXTENSIVE_STITCHING = 0x04;
        public const int FIX_HORRIBLE_UNION_ALL_TYPE_C = 0x08;
        public const int FIX_HORRIBLE_KEEP_NONE_CLOSED = 0x10;

        /**
         * RepRap flavored GCode is Marlin/Sprinter/Repetier based GCode. 
         *  This is the most commonly used GCode set.
         *  G0 for moves, G1 for extrusion.
         *  E values give mm of filament extrusion.
         *  Retraction is done on E values with G1. Start/end code is added.
         *  M106 Sxxx and M107 are used to turn the fan on/off.
         **/
        public const int GCODE_FLAVOR_REPRAP = 0;
        /**
         * UltiGCode flavored is Marlin based GCode. 
         *  UltiGCode uses less settings on the slicer and puts more settings in the firmware. This makes for more hardware/material independed GCode.
         *  G0 for moves, G1 for extrusion.
         *  E values give mm^3 of filament extrusion. Ignores the filament diameter setting.
         *  Retraction is done with G10 and G11. Retraction settings are ignored. G10 S1 is used for multi-extruder switch retraction.
         *  Start/end code is not added.
         *  M106 Sxxx and M107 are used to turn the fan on/off.
         **/
        public const int GCODE_FLAVOR_ULTIGCODE = 1;
        /**
         * Makerbot flavored GCode.
         *  Looks a lot like RepRap GCode with a few changes. Requires MakerWare to convert to X3G files.
         *   Heating needs to be done with M104 Sxxx T0
         *   No G21 or G90
         *   Fan ON is M126 T0 (No fan strength control?)
         *   Fan OFF is M127 T0
         *   Homing is done with G162 X Y F2000
         **/
        public const int GCODE_FLAVOR_MAKERBOT = 2;

        public const int MAX_EXTRUDERS = 16;

        List<_ConfigSettingIndex> _index;
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
        public int enableOozeShield;
        public int wipeTowerSize;
        public int multiVolumeOverlap;

        public int initialSpeedupLayers;
        public int initialLayerSpeed;
        public int printSpeed;
        public int infillSpeed;
        public int moveSpeed;
        public int fanFullOnLayerNr;

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

        public FMatrix3x3 matrix;
        public IntPoint objectPosition;
        public int objectSink;

        public int fixHorrible;
        public bool spiralizeMode;
        public int gcodeFlavor;

        public IntPoint[] extruderOffset = new IntPoint[MAX_EXTRUDERS];
        public string startCode;
        public string endCode;

#if false
//#define STRINGIFY(_s) #_s
//#define SETTING(name) _index.push_back(_ConfigSettingIndex(STRINGIFY(name), &name))
//#define SETTING2(name, altName) _index.push_back(_ConfigSettingIndex(STRINGIFY(name), &name)); _index.push_back(_ConfigSettingIndex(STRINGIFY(altName), &name))
#endif

        public ConfigSettings()
        {
            throw new NotImplementedException();
#if false
    SETTING(layerThickness);
    SETTING(initialLayerThickness);
    SETTING(filamentDiameter);
    SETTING(filamentFlow);
    SETTING(extrusionWidth);
    SETTING(insetCount);
    SETTING(downSkinCount);
    SETTING(upSkinCount);
    SETTING(sparseInfillLineDistance);
    SETTING(infillOverlap);
    SETTING(skirtDistance);
    SETTING(skirtLineCount);
    SETTING(skirtMinLength);

    SETTING(initialSpeedupLayers);
    SETTING(initialLayerSpeed);
    SETTING(printSpeed);
    SETTING(infillSpeed);
    SETTING(moveSpeed);
    SETTING(fanFullOnLayerNr);
    
    SETTING(supportAngle);
    SETTING(supportEverywhere);
    SETTING(supportLineDistance);
    SETTING(supportXYDistance);
    SETTING(supportZDistance);
    SETTING(supportExtruder);
    
    SETTING(retractionAmount);
    SETTING(retractionSpeed);
    SETTING(retractionAmountExtruderSwitch);
    SETTING(retractionMinimalDistance);
    SETTING(minimalExtrusionBeforeRetraction);
    SETTING(enableCombing);
    SETTING(enableOozeShield);
    SETTING(wipeTowerSize);
    SETTING(multiVolumeOverlap);
    SETTING2(objectPosition.X, posx);
    SETTING2(objectPosition.Y, posy);
    SETTING(objectSink);

    SETTING(raftMargin);
    SETTING(raftLineSpacing);
    SETTING(raftBaseThickness);
    SETTING(raftBaseLinewidth);
    SETTING(raftInterfaceThickness);
    SETTING(raftInterfaceLinewidth);
    
    SETTING(minimalLayerTime);
    SETTING(minimalFeedrate);
    SETTING(coolHeadLift);
    SETTING(fanSpeedMin);
    SETTING(fanSpeedMax);
    
    SETTING(fixHorrible);
    SETTING(spiralizeMode);
    SETTING(gcodeFlavor);
    
    SETTING(extruderOffset[1].X);
    SETTING(extruderOffset[1].Y);
    SETTING(extruderOffset[2].X);
    SETTING(extruderOffset[2].Y);
    SETTING(extruderOffset[3].X);
    SETTING(extruderOffset[3].Y);
#endif
        }

        public bool setSetting(string key, string value)
        {
            throw new NotImplementedException();
#if false
    for(int n=0; n < _index.size(); n++)
    {
        if (strcasecmp(key, _index[n].key) == 0)
        {
            *_index[n].ptr = atoi(value);
            return true;
        }
    }
    if (strcasecmp(key, "startCode") == 0)
    {
        this.startCode = value;
        return true;
    }
    if (strcasecmp(key, "endCode") == 0)
    {
        this.endCode = value;
        return true;
    }
#endif
            return false;
        }
    }
}
