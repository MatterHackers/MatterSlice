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
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
    public static class MatterSlice
    {
        static void print_usage()
        {
            Console.Write("usage: MatterSlice [-h] [-v] [-m 3x3matrix] [-s <settingkey>=<value>] -o <output.gcode> <model.stl>\n");
        }

        static int Main(string[] args)
        {
            return ProcessArgs(args);
        }

        public static int ProcessArgs(string argsInString)
        {
            List<string> commands = new List<string>();
            foreach (string command in SplitCommandLine.DoSplit(argsInString))
            {
                commands.Add(command);
            }
            string[] args = commands.ToArray();
            return ProcessArgs(args);
        }

        public static int ProcessArgs(string[] args)
        {
            ConfigSettings config = new ConfigSettings();
            fffProcessor processor = new fffProcessor(config);

            Console.WriteLine("MatterSlice version {0}".FormatWith(ConfigConstants.VERSION));

            config.DumpSettings("settings.ini");
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
                                    LogOutput.logError("Failed to open {0} for output.\n".FormatWith(args[argn]));
                                    return 1;
                                }
                                break;
                            case 's':
                                {
                                    argn++;
                                    string[] keyValue = args[argn].Split('=');
                                    if (keyValue.Length > 1)
                                    {
                                        if (!config.SetSetting(keyValue[0], keyValue[1]))
                                        {
                                            Console.Write("Setting not found: %s %s\n", keyValue[0], keyValue[1]);
                                        }
                                    }
                                }
                                break;

                            case 'm':
                                argn++;
                                throw new NotImplementedException("m");
#if false
                        sscanf(argv[argn], "%lf,%lf,%lf,%lf,%lf,%lf,%lf,%lf,%lf",
                        &config.matrix.m[0][0], &config.matrix.m[0][1], &config.matrix.m[0][2],
                        &config.matrix.m[1][0], &config.matrix.m[1][1], &config.matrix.m[1][2],
                        &config.matrix.m[2][0], &config.matrix.m[2][1], &config.matrix.m[2][2]);
#endif
                                break;

                            default:
                                LogOutput.logError("Unknown option: {0}\n".FormatWith(str));
                                break;
                        }
                    }
                }
                else
                {
#if !DEBUG
                    try
#endif
                    {
                        processor.processFile(args[argn]);
                    }
#if !DEBUG
                    catch (Exception e)
                    {
                        Console.Write("{0}".FormatWith( e));
                        Console.Write("InnerException: {0}".FormatWith( e.InnerException));
                        return 1;
                    }
#endif
                }
            }

            processor.finalize();
            return 0;
        }
    }
}