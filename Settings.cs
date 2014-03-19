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
using System.Reflection;

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

    // this class is so that we can change the name of a variable and not break old settings files
    [System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple=true)]
    public class LegacyName : System.Attribute
    {
        private string name;
        public string Name { get { return name; } }

        public LegacyName(string name)
        {
            this.name = name;
        }
    }

    // all the variables in this class will be saved and loaded from settings files
    public class ConfigSettings
    {
        // if you were to change the layerThickness variable you would add a legacy name so that we can still use old settings
        //[LegacyName("exampleLegacyLayerThickness")]
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
        public int retractionZHop;
        public bool enableCombing;
        public bool enableOozeShield;
        public int wipeTowerSize;
        public int multiVolumeOverlap;

        public int initialSpeedupLayers;
        public int initialLayerSpeed;
        public int printSpeed;
        public int infillSpeed;
        public int inset0Speed;
        public int insetXSpeed;
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
        public bool coolHeadLift;
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

        public int fixHorrible;
        public bool spiralizeMode;
        public int gcodeFlavor;

        public IntPoint[] extruderOffset = new IntPoint[ConfigConstants.MAX_EXTRUDERS];
        public string startCode;
        public string endCode;

        public ConfigSettings()
        {
            SetToDefault();
        }

        public void SetToDefault()
        {
            filamentDiameter = 2890;
            filamentFlow = 100;
            initialLayerThickness = 300;
            layerThickness = 100;
            extrusionWidth = 400;
            insetCount = 2;
            downSkinCount = 6;
            upSkinCount = 6;
            initialSpeedupLayers = 4;
            initialLayerSpeed = 20;
            printSpeed = 50;
            infillSpeed = 50;
            inset0Speed = 50;
            insetXSpeed = 50;
            moveSpeed = 200;
            fanFullOnLayerNr = 2;
            skirtDistance = 6000;
            skirtLineCount = 1;
            skirtMinLength = 0;
            sparseInfillLineDistance = 100 * extrusionWidth / 20;
            infillOverlap = 15;
            objectPosition.X = 102500;
            objectPosition.Y = 102500;
            objectSink = 0;
            supportAngle = -1;
            supportEverywhere = 0;
            supportLineDistance = sparseInfillLineDistance;
            supportExtruder = -1;
            supportXYDistance = 700;
            supportZDistance = 150;
            retractionAmount = 4500;
            retractionSpeed = 45;
            retractionAmountExtruderSwitch = 14500;
            retractionMinimalDistance = 1500;
            minimalExtrusionBeforeRetraction = 100;
            enableOozeShield = false;
            enableCombing = true;
            wipeTowerSize = 0;
            multiVolumeOverlap = 0;

            minimalLayerTime = 5;
            minimalFeedrate = 10;
            coolHeadLift = false;
            fanSpeedMin = 100;
            fanSpeedMax = 100;

            raftMargin = 5000;
            raftLineSpacing = 1000;
            raftBaseThickness = 0;
            raftBaseLinewidth = 0;
            raftInterfaceThickness = 0;
            raftInterfaceLinewidth = 0;

            spiralizeMode = false;
            fixHorrible = 0;
            gcodeFlavor = ConfigConstants.GCODE_FLAVOR_REPRAP;

            startCode =
                            "M109 S210     ;Heatup to 210C\n" +
                            "G21           ;metric values\n" +
                            "G90           ;absolute positioning\n" +
                            "G28           ;Home\n" +
                            "G1 Z15.0 F300 ;move the platform down 15mm\n" +
                            "G92 E0        ;zero the extruded length\n" +
                            "G1 F200 E5    ;extrude 5mm of feed stock\n" +
                            "G92 E0        ;zero the extruded length again\n";
            endCode =
                "M104 S0                     ;extruder heater off\n" +
                "M140 S0                     ;heated bed heater off (if you have it)\n" +
                "G91                            ;relative positioning\n" +
                "G1 E-1 F300                    ;retract the filament a bit before lifting the nozzle, to release some of the pressure\n" +
                "G1 Z+0.5 E-5 X-20 Y-20 F9000   ;move Z up a bit and retract filament even more\n" +
                "G28 X0 Y0                      ;move X/Y to min endstops, so the head is out of the way\n" +
                "M84                         ;steppers off\n" +
                "G90                         ;absolute positioning\n";
        }

        public void DumpSettings(string fileName)
        {
            List<string> lines = new List<string>();
            FieldInfo[] fields;
            fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                System.Attribute[] attributes = System.Attribute.GetCustomAttributes(field);
                foreach (Attribute attribute in attributes)
                {
                    LegacyName legacyName = attribute as LegacyName;
                    if (legacyName != null)
                    {
                        string Name = legacyName.Name;
                    }
                }
                string name = field.Name;
                object value = field.GetValue(this);
                switch (field.FieldType.Name)
                {
                    case "Int32":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "Boolean":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "FMatrix3x3":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "IntPoint":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "IntPoint[]":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "String":
                        lines.Add("{0}={1}".FormatWith(name, value).Replace("\n", "\\n"));
                        break;

                    default:
                        throw new NotImplementedException("unknown type");
                }
            }

            lines.Sort();
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName))
            {
                foreach (string line in lines)
                {
                    file.WriteLine(line);
                }
            }
        }

        public bool SetSetting(string key, string value)
        {
            value = value.Replace("\"", "");
            switch (key)
            {
                case "layerThickness":
                    layerThickness = int.Parse(value);
                    return true;

                case "initialLayerThickness":
                    initialLayerThickness = int.Parse(value);
                    return true;

                case "filamentDiameter":
                    filamentDiameter = int.Parse(value);
                    return true;

                case "filamentFlow":
                    filamentFlow = int.Parse(value);
                    return true;

                case "extrusionWidth":
                    extrusionWidth = int.Parse(value);
                    return true;

                case "insetCount":
                    insetCount = int.Parse(value);
                    return true;

                case "downSkinCount":
                    downSkinCount = int.Parse(value);
                    return true;

                case "upSkinCount":
                    upSkinCount = int.Parse(value);
                    return true;

                case "sparseInfillLineDistance":
                    sparseInfillLineDistance = int.Parse(value);
                    return true;

                case "infillOverlap":
                    infillOverlap = int.Parse(value);
                    return true;

                case "skirtDistance":
                    skirtDistance = int.Parse(value);
                    return true;

                case "skirtLineCount":
                    skirtLineCount = int.Parse(value);
                    return true;

                case "skirtMinLength":
                    skirtMinLength = (int)double.Parse(value);
                    return true;

                case "initialSpeedupLayers":
                    initialSpeedupLayers = int.Parse(value);
                    return true;

                case "initialLayerSpeed":
                    initialLayerSpeed = int.Parse(value);
                    return true;

                case "printSpeed":
                    printSpeed = int.Parse(value);
                    return true;

                case "infillSpeed":
                    infillSpeed = int.Parse(value);
                    return true;

                case "inset0Speed":
                    inset0Speed = int.Parse(value);
                    return true;

                case "insetXSpeed":
                    insetXSpeed = int.Parse(value);
                    return true;

                case "moveSpeed":
                    moveSpeed = int.Parse(value);
                    return true;

                case "fanFullOnLayerNr":
                    fanFullOnLayerNr = int.Parse(value);
                    return true;

                case "supportAngle":
                    supportAngle = int.Parse(value);
                    return true;

                case "supportEverywhere":
                    supportEverywhere = int.Parse(value);
                    return true;

                case "supportLineDistance":
                    supportLineDistance = int.Parse(value);
                    return true;

                case "supportXYDistance":
                    supportXYDistance = int.Parse(value);
                    return true;

                case "supportZDistance":
                    supportZDistance = int.Parse(value);
                    return true;

                case "supportExtruder":
                    supportExtruder = int.Parse(value);
                    return true;

                case "retractionAmount":
                    retractionAmount = int.Parse(value);
                    return true;

                case "retractionSpeed":
                    retractionSpeed = int.Parse(value);
                    return true;

                case "retractionAmountExtruderSwitch":
                    retractionAmountExtruderSwitch = int.Parse(value);
                    return true;

                case "retractionMinimalDistance":
                    retractionMinimalDistance = int.Parse(value);
                    return true;

                case "minimalExtrusionBeforeRetraction":
                    minimalExtrusionBeforeRetraction = int.Parse(value);
                    return true;

                case "enableCombing":
                    enableCombing = value == "1";
                    return true;

                case "enableOozeShield":
                    enableOozeShield = value == "1";
                    return true;

                case "wipeTowerSize":
                    wipeTowerSize = int.Parse(value);
                    return true;

                case "multiVolumeOverlap":
                    multiVolumeOverlap = int.Parse(value);
                    return true;

                case "objectPosition.X":
                    objectPosition.X = int.Parse(value);
                    return true;

                case "objectPosition.Y":
                    objectPosition.Y = int.Parse(value);
                    return true;

                case "objectSink":
                    objectSink = int.Parse(value);
                    return true;

                case "raftMargin":
                    raftMargin = int.Parse(value);
                    return true;

                case "raftLineSpacing":
                    raftLineSpacing = int.Parse(value);
                    return true;

                case "raftBaseThickness":
                    raftBaseThickness = int.Parse(value);
                    return true;

                case "raftBaseLinewidth":
                    raftBaseLinewidth = int.Parse(value);
                    return true;

                case "raftInterfaceThickness":
                    raftInterfaceThickness = int.Parse(value);
                    return true;

                case "raftInterfaceLinewidth":
                    raftInterfaceLinewidth = int.Parse(value);
                    return true;

                case "minimalLayerTime":
                    minimalLayerTime = int.Parse(value);
                    return true;

                case "minimalFeedrate":
                    minimalFeedrate = int.Parse(value);
                    return true;

                case "coolHeadLift":
                    coolHeadLift = value == "1";
                    return true;

                case "fanSpeedMin":
                    fanSpeedMin = int.Parse(value);
                    return true;

                case "fanSpeedMax":
                    fanSpeedMax = int.Parse(value);
                    return true;

                case "fixHorrible":
                    fixHorrible = int.Parse(value);
                    return true;

                case "spiralizeMode":
                    sparseInfillLineDistance = int.Parse(value);
                    return true;

                case "gcodeFlavor":
                    gcodeFlavor = int.Parse(value);
                    return true;

                case "extruderOffset[1].X":
                    extruderOffset[1].X = int.Parse(value);
                    return true;

                case "extruderOffset[1].Y":
                    extruderOffset[1].Y = int.Parse(value);
                    return true;

                case "extruderOffset[2].X":
                    extruderOffset[2].X = int.Parse(value);
                    return true;

                case "extruderOffset[2].Y":
                    extruderOffset[2].Y = int.Parse(value);
                    return true;

                case "extruderOffset[3].X":
                    extruderOffset[3].X = int.Parse(value);
                    return true;

                case "extruderOffset[3].Y":
                    extruderOffset[3].Y = int.Parse(value);
                    return true;

                case "startCode":
                    this.startCode = value;
                    return true;

                case "endCode":
                    this.endCode = value;
                    return true;
            }
            return false;
        }
    }

    public class ConfigConstants
    {
        public const string VERSION = "1.0";

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
    }
}

