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
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

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
    [System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = true)]
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
        // if you were to change the layerThicknessMm variable you would add a legacy name so that we can still use old settings
        //[LegacyName("layerThickness")] // the name before we added Mm
        public double layerThicknessMm;
        public int layerThickness_µm { get { return (int)(layerThicknessMm * 1000); } }

        public double initialLayerThicknessMm;
        public int initialLayerThickness_µm { get { return (int)(initialLayerThicknessMm * 1000); } }

        public double filamentDiameterMm;
        public int filamentDiameter_µm { get { return (int)(filamentDiameterMm * 1000); } }

        public int filamentFlowPercent;
        
        public double firstLayerExtrusionWidthMm;
        public int firstLayerExtrusionWidth_µm { get { return (int)(firstLayerExtrusionWidthMm * 1000); } }

        public double extrusionWidthMm;
        public int extrusionWidth_µm { get { return (int)(extrusionWidthMm * 1000); } }

        public int perimeterCount;
        public int downSkinCount;
        public int upSkinCount;
        public int sparseInfillLineDistance;
        public int infillOverlapPercent;
        public int infillAngleDegrees;

        public int skirtDistanceMm;
        public int skirtDistance_µm { get { return (int)(skirtDistanceMm * 1000); } }
        
        public int skirtLineCount;

        public int skirtMinLengthMm;
        public int skirtMinLength_µm { get { return (int)(skirtMinLengthMm * 1000); } }

        public int retractionAmount;
        public int retractionAmountExtruderSwitch;
        public int retractionSpeedMmPerS;
        public int retractionMinimumDistance;

        public double minimumExtrusionBeforeRetractionMm;
        public int minimumExtrusionBeforeRetraction_µm { get { return (int)(minimumExtrusionBeforeRetractionMm * 1000); } }
        
        public double retractionZHopMm;
        public bool enableCombing;
        public bool enableOozeShield;
        public int wipeTowerSize;
        public int multiVolumeOverlapPercent;

        // speed settings
        public int firstLayerSpeedMmPerS;
        public int printSpeedMmPerS;
        public int infillSpeedMmPerS;
        public int outsidePerimeterSpeedMmPerS;
        public int insidePerimeterSpeedsMmPerS;
        public int moveSpeedMmPerS;
        public int firstLayerToAllowFan;

        //Support material
        public ConfigConstants.SUPPORT_TYPE supportType;
        public int supportAngleDegrees;
        public int supportEverywhere;
        public int supportLineDistance;
        public int supportXYDistance;
        public int supportZDistance;
        public int supportExtruder;

        //Cool settings
        public int minimumLayerTimeSeconds;
        public int minimumFeedrateMmPerS;
        public bool coolHeadLift;
        public int fanSpeedMinPercent;
        public int fanSpeedMaxPercent;

        //Raft settings
        public int raftMargin;
        public int raftLineSpacing;
        public int raftBaseThickness;
        public int raftBaseLinewidth;
        public int raftInterfaceThickness;
        public int raftInterfaceLinewidth;

        public FMatrix3x3 modelRotationMatrix = new FMatrix3x3();

        public DoublePoint objectCenterPositionMm;
        public IntPoint objectCenterPosition_µm { get { return new IntPoint(objectCenterPositionMm.X * 1000, objectCenterPositionMm.Y * 1000); } }

        public double objectSinkMm;
        public int objectSink_µm { get { return (int)(objectSinkMm * 1000); } }

        public ConfigConstants.FIX_HORRIBLE fixHorrible;
        public bool spiralizeMode;
        public ConfigConstants.GCODE_FLAVOR gcodeFlavor;

        public IntPoint[] extruderOffsets = new IntPoint[ConfigConstants.MAX_EXTRUDERS];
        public string startCode;
        public string endCode;

        public ConfigSettings()
        {
            SetToDefault();
        }

        public void SetToDefault()
        {
            filamentDiameterMm = 2.89;
            filamentFlowPercent = 100;
            initialLayerThicknessMm = .3;
            layerThicknessMm = .1;
            firstLayerExtrusionWidthMm = .8;
            extrusionWidthMm = .4;
            perimeterCount = 2;
            downSkinCount = 6;
            upSkinCount = 6;
            firstLayerSpeedMmPerS = 20;
            printSpeedMmPerS = 50;
            infillSpeedMmPerS = 50;
            outsidePerimeterSpeedMmPerS = 50;
            insidePerimeterSpeedsMmPerS = 50;
            moveSpeedMmPerS = 200;
            firstLayerToAllowFan = 2;
            skirtDistanceMm = 6;
            skirtLineCount = 1;
            skirtMinLengthMm = 0;
            sparseInfillLineDistance = 100 * extrusionWidth_µm / 20;
            infillOverlapPercent = 15;
            infillAngleDegrees = 45;
            objectCenterPositionMm.X = 102.5;
            objectCenterPositionMm.Y = 102.5;
            objectSinkMm = 0;
            supportType = ConfigConstants.SUPPORT_TYPE.GRID;
            supportAngleDegrees = -1;
            supportEverywhere = 0;
            supportLineDistance = sparseInfillLineDistance;
            supportExtruder = -1;
            supportXYDistance = 700;
            supportZDistance = 150;
            retractionAmount = 4500;
            retractionSpeedMmPerS = 45;
            retractionAmountExtruderSwitch = 14500;
            retractionMinimumDistance = 1500;
            minimumExtrusionBeforeRetractionMm = .1;
            enableOozeShield = false;
            enableCombing = true;
            wipeTowerSize = 0;
            multiVolumeOverlapPercent = 0;

            minimumLayerTimeSeconds = 5;
            minimumFeedrateMmPerS = 10;
            coolHeadLift = false;
            fanSpeedMinPercent = 100;
            fanSpeedMaxPercent = 100;

            raftMargin = 5000;
            raftLineSpacing = 1000;
            raftBaseThickness = 0;
            raftBaseLinewidth = 0;
            raftInterfaceThickness = 0;
            raftInterfaceLinewidth = 0;

            spiralizeMode = false;
            fixHorrible = 0;
            gcodeFlavor = ConfigConstants.GCODE_FLAVOR.REPRAP;

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
                    case "Double":
                    case "Boolean":
                    case "FMatrix3x3":
                        // all these setting just output correctly with ToString() so we don't have to do anything special.
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "IntPoint":
                        lines.Add("{0}={1}".FormatWith(name, ((IntPoint)value).OutputInMm()));
                        break;

                    case "DoublePoint":
                        lines.Add("{0}=[{1},{2}]".FormatWith(name, ((DoublePoint)value).X, ((DoublePoint)value).Y));
                        break;

                    case "IntPoint[]":
                        {
                            IntPoint[] valueIntArray = value as IntPoint[];
                            string values = "[";
                            bool first = true;
                            foreach(IntPoint intPoint in valueIntArray)
                            {
                                if (!first)
                                {
                                    values += ",";
                                }
                                values = values + intPoint.OutputInMm();
                                first = false;
                            }
                            lines.Add("{0}={1}]".FormatWith(name, values));
                        }
                        break;

                    case "String":
                        // change the cariage returns to '\n's in the file
                        lines.Add("{0}={1}".FormatWith(name, value).Replace("\n", "\\n"));
                        break;

                    case "FIX_HORRIBLE":
                    case "SUPPORT_TYPE":
                    case "GCODE_FLAVOR":
                        // all the enums can be output by this function
                        lines.Add("{0}={1} # {2}".FormatWith(name, value, GetEnumHelpText(field.FieldType, field.FieldType.Name)));
                        break;

                    default:
                        throw new NotImplementedException("unknown type '{0}'".FormatWith(field.FieldType.Name));
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

        private static string GetEnumHelpText(Type type, string enumName)
        {
            bool first = true;
            string helpLine = "Available Values: ";
            FieldInfo[] fields = type.GetFields();
            foreach (FieldInfo field in fields)
            {
                string[] names = field.ToString().Split(' ');
                if (names.Length == 2 && names[0] == enumName)
                {
                    if (!first)
                    {
                        helpLine += ", ";
                    }
                    helpLine += names[1];
                    first = false;
                }
            }

            return helpLine;
        }

        public bool SetSetting(string keyToSet, string valueToSetTo)
        {
            valueToSetTo = valueToSetTo.Replace("\"", "").Trim();

            List<string> lines = new List<string>();
            FieldInfo[] fields;
            fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                System.Attribute[] attributes = System.Attribute.GetCustomAttributes(field);
                List<string> possibleNames = new List<string>();
                possibleNames.Add(field.Name);
                foreach (Attribute attribute in attributes)
                {
                    LegacyName legacyName = attribute as LegacyName;
                    if (legacyName != null)
                    {
                        possibleNames.Add(legacyName.Name);
                    }
                }

                if (possibleNames.Contains(keyToSet))
                {
                    string name = field.Name;
                    object value = field.GetValue(this);
                    switch (field.FieldType.Name)
                    {
                        case "Int32":
                            field.SetValue(this, (int)double.Parse(valueToSetTo));
                            break;

                        case "Double":
                            field.SetValue(this, double.Parse(valueToSetTo));
                            break;

                        case "Boolean":
                            field.SetValue(this, bool.Parse(valueToSetTo));
                            break;

                        case "FMatrix3x3":
                            {
                                field.SetValue(this, new FMatrix3x3(valueToSetTo));
                            }
                            break;

                        case "DoublePoint":
                            {
                                string bracketContents = GetInsides(valueToSetTo, '[', ']');
                                string[] xyValues = bracketContents.Split(',');
                                field.SetValue(this, new DoublePoint(double.Parse(xyValues[0]), double.Parse(xyValues[1])));
                            }
                            break;

                        case "IntPoint":
                            {
                                string bracketContents = GetInsides(valueToSetTo, '[', ']');
                                string[] xyValues = bracketContents.Split(',');
                                field.SetValue(this, new IntPoint(double.Parse(xyValues[0]), double.Parse(xyValues[1])));
                            }
                            break;

                        case "IntPoint[]":
                            {
                                string bracketContents = GetInsides(valueToSetTo, '[', ']');
                                List<IntPoint> points = new List<IntPoint>();

                                string intPointString;
                                int nextIndex = GetInsides(out intPointString, bracketContents, '[', ']', 0);
                                do
                                {
                                    string[] xyValues = intPointString.Split(',');
                                    points.Add(new IntPoint(double.Parse(xyValues[0]), double.Parse(xyValues[1])));

                                    nextIndex = GetInsides(out intPointString, bracketContents, '[', ']', nextIndex);
                                } while (nextIndex != -1);
                                field.SetValue(this, points.ToArray());
                            }
                            break;

                        case "String":
                            field.SetValue(this, valueToSetTo);
                            break;

                        case "FIX_HORRIBLE":
                        case "SUPPORT_TYPE":
                        case "GCODE_FLAVOR":
                            field.SetValue(this, Enum.Parse(field.FieldType, valueToSetTo));
                            break;

                        default:
                            throw new NotImplementedException("unknown type");
                    }

                    return true;
                }
            }
            return false;
        }

        private string GetInsides(string content, char startingChar, char endingChar)
        {
            string insides;
            GetInsides(out insides, content, startingChar, endingChar, 0);
            return insides;
        }

        private int GetInsides(out string insides, string content, char startingChar, char endingChar, int startIndex, int endIndex = -1)
        {
            if (endIndex == -1)
            {
                endIndex = content.Length;
            }
            insides = "";
            int firstOpen = -1;
            int openCount = 0;
            int endPosition = -1;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (content[i] == startingChar)
                {
                    if (firstOpen == -1)
                    {
                        firstOpen = i;
                    }
                    openCount++;
                }
                else if (openCount > 0 && content[i] == endingChar)
                {
                    openCount--;
                    if (openCount == 0)
                    {
                        endPosition = i;
                        insides = content.Substring(firstOpen+1, i-(firstOpen+1));
                        break;
                    }
                }
            }

            return endPosition;
        }

        public bool ReadSettings(string fileName)
        {
            if (File.Exists(fileName))
            {
                string[] lines = File.ReadAllLines(fileName);
                for(int i=0; i< lines.Length; i++)
                {
                    string line = lines[i];
                    int commentStart = line.IndexOf("#");
                    if(commentStart >= 0)
                    {
                        line = line.Substring(0, commentStart);
                    }

                    int equalsPos = line.IndexOf('=');
                    if (equalsPos > 0)
                    {
                        string key = line.Substring(0, equalsPos).Trim();
                        string value = line.Substring(equalsPos + 1).Trim();
                        if (key.Length > 0 && value.Length > 0)
                        {
                            SetSetting(key, value);
                        }
                    }
                }

                return true;
            }

            return false;
        }
    }

    public class ConfigConstants
    {
        public const string VERSION = "1.0";

        [Flags]
        public enum FIX_HORRIBLE
        {
            NONE,
            UNION_ALL_TYPE_A = 0x01,
            UNION_ALL_TYPE_B = 0x02,
            EXTENSIVE_STITCHING = 0x04,
            UNION_ALL_TYPE_C = 0x08,
            KEEP_NONE_CLOSED = 0x10,
        }

        /**
         * * Type of support material.
         * * Grid is a X/Y grid with an outline, which is very strong, provides good support. But in some cases is hard to remove.
         * * Lines give a row of lines which break off one at a time, making them easier to remove, but they do not support as good as the grid support.
         * */
        public enum SUPPORT_TYPE
        {
            GRID,
            LINES
        }

        public enum GCODE_FLAVOR
        {
            /**
             * RepRap flavored GCode is Marlin/Sprinter/Repetier based GCode. 
             *  This is the most commonly used GCode set.
             *  G0 for moves, G1 for extrusion.
             *  E values give mm of filament extrusion.
             *  Retraction is done on E values with G1. Start/end code is added.
             *  M106 Sxxx and M107 are used to turn the fan on/off.
             **/
            REPRAP,
            /**
             * UltiGCode flavored is Marlin based GCode. 
             *  UltiGCode uses less settings on the slicer and puts more settings in the firmware. This makes for more hardware/material independed GCode.
             *  G0 for moves, G1 for extrusion.
             *  E values give mm^3 of filament extrusion. Ignores the filament diameter setting.
             *  Retraction is done with G10 and G11. Retraction settings are ignored. G10 S1 is used for multi-extruder switch retraction.
             *  Start/end code is not added.
             *  M106 Sxxx and M107 are used to turn the fan on/off.
             **/
            ULTIGCODE,
            /**
             * Makerbot flavored GCode.
             *  Looks a lot like RepRap GCode with a few changes. Requires MakerWare to convert to X3G files.
             *   Heating needs to be done with M104 Sxxx T0
             *   No G21 or G90
             *   Fan ON is M126 T0 (No fan strength control?)
             *   Fan OFF is M127 T0
             *   Homing is done with G162 X Y F2000
             **/
            MAKERBOT,

            /**
             * Bits From Bytes GCode.
             *  BFB machines use RPM instead of E. Which is coupled to the F instead of independed. (M108 S[deciRPM])
             *  Need X,Y,Z,F on every line.
             *  Needs extruder ON/OFF (M101, M103), has auto-retrection (M227 S[2560*mm] P[2560*mm])
             **/
            BFB,

            /**
              * MACH3 GCode
              *  MACH3 is CNC control software, which expects A/B/C/D for extruders, instead of E.
              **/
            MACH3,
        }


        public const int MAX_EXTRUDERS = 16;
    }
}

