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
using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterSlice
{
	public static class MatterSlice
	{
		private static void print_usage()
		{
			LogOutput.LogError("usage: MatterSlice [-h] [-d] [-v] [-t] [-m 3x3matrix]\n       [-b boolean math] [-c <config file>]\n       [-s <settingkey>=<value>] -o <output.gcode> <model.stl>\n\n");
			LogOutput.LogError("    [] enclose optional settings, <> are required.\n\n");
			LogOutput.LogError("    -h Show this message.\n");
			LogOutput.LogError("    -d Save the currently loaded settings to settings.ini (useful to see\n       all settings).\n");
			LogOutput.LogError("    -v Increment verbose level.\n");
			LogOutput.LogError("    -t Run unit tests.\n");
			LogOutput.LogError("    -m A 3x3 matrix for translating and rotating the layers.\n");
			LogOutput.LogError("    -c A config file to apply to the current settings.\n       Can be applied multiple times.\n       Formated like the default.ini (partial settings are fine).\n");
			LogOutput.LogError("    -s Specify a setting on the command line.\n       Uses the same names and values as default.ini.\n");
			LogOutput.LogError("    model.stl, the file that will be loaded and sliced.\n");
		}

		public static bool Canceled { get; set; } = false;

		public static int ProcessArgs(string argsInString)
		{
			Canceled = false;
			List<string> commands = new List<string>();
			foreach (string command in SplitCommandLine.DoSplit(argsInString))
			{
				commands.Add(command);
			}

			string[] args = commands.ToArray();
			return ProcessArgs(args);
		}

		public static void Stop()
		{
			Canceled = true;
		}

		public static int ProcessArgs(string[] args)
		{
			if (args.Length == 0)
			{
				print_usage();
				return 0;
			}

			ConfigSettings config = new ConfigSettings();
			FffProcessor processor = new FffProcessor(config);

			LogOutput.Log("\nMatterSlice version {0}\n\n".FormatWith(ConfigConstants.VERSION));

			if (ProcessArgs(args, config, processor) == 0)
			{
				return 0;
			}

			if (!Canceled)
			{
				using (new QuickTimer2Report("DoProcessing"))
				{
					processor.DoProcessing();
				}
			}

			if (!Canceled)
			{
				processor.Dispose();
			}

			if (Canceled)
			{
				processor.Cancel();
			}

			Canceled = true;

			return 0;
		}

		public static int ProcessArgs(string[] args, ConfigSettings config, FffProcessor processor)
		{
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

							case 'o':
								argn++;
								if (!processor.SetTargetFile(args[argn]))
								{
									LogOutput.LogError("Failed to open {0} for output.\n".FormatWith(args[argn]));
									return 1;
								}

								break;

							case 'c':
								{
									// Read a config file from the given path
									argn++;
									if (!config.ReadSettings(args[argn]))
									{
										LogOutput.LogError("Failed to read config '{0}'\n".FormatWith(args[argn]));
									}

									// process any matrix and mesh requested by config file
									List<string> commands = new List<string>();
									foreach (string command in SplitCommandLine.DoSplit(config.AdditionalArgsToProcess))
									{
										commands.Add(command);
									}

									string[] subArgs = commands.ToArray();
									ProcessArgs(subArgs, config, processor);
								}

								break;

							case 'd':
								config.DumpSettings("settings.ini");
								break;

							case 's':
								{
									argn++;
									int equalsPos = args[argn].IndexOf('=');
									if (equalsPos != -1)
									{
										string key = args[argn].Substring(0, equalsPos);
										string value = args[argn].Substring(equalsPos + 1);
										if (key.Length > 1)
										{
											if (!config.SetSetting(key, value))
											{
												LogOutput.LogError("Setting not found: {0} {1}\n".FormatWith(key, value));
											}
										}
									}
								}

								break;

							case 'm':
								argn++;
								string[] matrixValues = args[argn].Split(',');
								var loadedMatrix = Matrix4X4.Identity;
								for (int i = 0; i < 4; i++)
								{
									for (int j = 0; j < 4; j++)
									{
										string valueString = matrixValues[i * 4 + j];
										double value;
										if (double.TryParse(valueString, out value))
										{
											loadedMatrix[i, j] = value;
										}
									}
								}

								config.ModelMatrix = loadedMatrix;
								break;

							default:
								throw new NotImplementedException("Unknown option: {0}\n".FormatWith(str));
								// LogOutput.logError("Unknown option: {0}\n".FormatWith(str));
								// break;
						}
					}
				}
				else
				{
					using (new QuickTimer2Report("LoadStlFile"))
					{
						processor.LoadStlFile(args[argn]);
					}
				}
			}

			return 1;
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}
	}
}