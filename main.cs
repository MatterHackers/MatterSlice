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

namespace MatterHackers.MatterSlice
{
    public class MatterSlice
    {
void print_usage()
{
    Console.Write("usage: MatterSlice [-h] [-v] [-m 3x3matrix] [-s <settingkey>=<value>] -o <output.gcode> <model.stl>\n");
}

void main(string[] args)
{
    ConfigSettings config = new ConfigSettings();
    fffProcessor processor = new fffProcessor(config);

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
    config.fanFullOnLayerNr = 2;
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
    config.enableOozeShield = 0;
    config.enableCombing = 1;
    config.wipeTowerSize = 0;
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

    config.spiralizeMode = 0;
    config.fixHorrible = 0;
    config.gcodeFlavor = ConfigSettings.GCODE_FLAVOR_REPRAP;
    
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

    Console.WriteLine(string.Format("MatterSlice version {0}", ConfigSettings.VERSION));

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
                    return;
                case 'v':
                    LogOutput.verbose_level++;
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
                    if (!processor.setTargetFile(args[argn]))
                    {
                        LogOutput.logError(string.Format("Failed to open {0} for output.\n", args[argn]));
                        return;
                    }
                    break;
                case 's':
                    {
                        argn++;
                                    throw new NotImplementedException();
#if false
                        char* valuePtr = strchr(argv[argn], '=');
                        if (valuePtr)
                        {
                            *valuePtr++ = '\0';
                            
                            if (!config.setSetting(argv[argn], valuePtr))
                                Console.Write("Setting not found: %s %s\n", argv[argn], valuePtr);
                        }
#endif
                    }
                    break;
                case 'm':
                    argn++;
                                    throw new NotImplementedException();
#if false
                        sscanf(argv[argn], "%lf,%lf,%lf,%lf,%lf,%lf,%lf,%lf,%lf",
                        &config.matrix.m[0][0], &config.matrix.m[0][1], &config.matrix.m[0][2],
                        &config.matrix.m[1][0], &config.matrix.m[1][1], &config.matrix.m[1][2],
                        &config.matrix.m[2][0], &config.matrix.m[2][1], &config.matrix.m[2][2]);
#endif
                    break;
                default:
                    LogOutput.logError(string.Format("Unknown option: {0}\n", str));
                    break;
                }
            }
        }
        else
        {
            try 
            {
                processor.processFile(args[argn]);
            }
            catch(Exception e)
            {
                Console.Write(string.Format("{0}", e));
                Console.Write(string.Format("InnerException: {0}", e.InnerException));
            }
        }
    }
    
    processor.finalize();
}
    }
}