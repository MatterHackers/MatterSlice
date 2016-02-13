/*
This file is part of MatterSlice. A command line utility for
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

using ClipperLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MatterHackers.MatterSlice
{
	public class ConfigConstants
	{
		public const int MAX_EXTRUDERS = 16;
		public const string VERSION = "1.0";

		public enum INFILL_TYPE
		{
			GRID,
			LINES,
			TRIANGLES,
			HEXAGON,
			CONCENTRIC,
		}

		public enum OUTPUT_TYPE
		{
			/**
			 * RepRap GCode is Marlin/Sprinter/Repetier based GCode.
			 *  This is the most commonly used GCode set.
			 *  G0 for moves, G1 for extrusion.
			 *  E values give mm of filament extrusion.
			 *  Retraction is done on E values with G1. Start/end code is added.
			 *  M106 Sxxx and M107 are used to turn the fan on/off.
			 **/
			REPRAP,
			/**
			 * UltiGCode is Marlin based GCode.
			 *  UltiGCode uses less settings on the slicer and puts more settings in the firmware. This makes for more hardware/material independent GCode.
			 *  G0 for moves, G1 for extrusion.
			 *  E values give mm^3 of filament extrusion. Ignores the filament diameter setting.
			 *  Retraction is done with G10 and G11. Retraction settings are ignored. G10 S1 is used for multi-extruder switch retraction.
			 *  Start/end code is not added.
			 *  M106 Sxxx and M107 are used to turn the fan on/off.
			 **/
			ULTIGCODE,

			/**
			  * MACH3 GCode
			  *  MACH3 is CNC control software, which expects A/B/C/D for extruders, instead of E.
			  **/
			MACH3,
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
	}

	// all the variables in this class will be saved and loaded from settings files
	public class ConfigSettings
	{
		public ConfigSettings()
		{
			SetToDefault();
		}

		[SettingDescription("Avoid crossing any of the perimeters of a shape while printing its parts.")]
		public bool avoidCrossingPerimeters;

		[SettingDescription("The amount to clip off the bottom of the part, in millimeters.")]
		public double bottomClipAmount;

		[SettingDescription("This is the speed to print the bottom layers infill, mm/s.")]
		public double bottomInfillSpeed;

		[SettingDescription("The speed to run the fan during bridging.")]
		public int bridgeFanSpeedPercent;

		public bool retractWhenChangingIslands;

		[SettingDescription("mm/s.")]
		public int bridgeSpeed;

		[SettingDescription("Describes if 'positionToPlaceObjectCenter' should be used.")]
		public bool centerObjectInXy;

		// other
		[SettingDescription("This will cause the z height to raise continuously while on the outer perimeter.")]
		public bool continuousSpiralOuterPerimeter;

		// Raft settings
		[SettingDescription("mm.")]
		public bool enableRaft;

		public string endCode;

		public IntPoint[] extruderOffsets = new IntPoint[ConfigConstants.MAX_EXTRUDERS];

		[SettingDescription("Lets you adjust how much material to extrude.")]
		public double extrusionMultiplier;

		[SettingDescription("The width of the line to extrude.")]
		public double extrusionWidth;

		public int fanSpeedMaxPercent;

		public int fanSpeedMinPercent;

		[SettingDescription("The width of the filament being fed into the extruder, in millimeters.")]
		public double filamentDiameter;

		[SettingDescription("The width of the line to extrude for the first layer.")]
		public double firstLayerExtrusionWidth;

		// speed settings
		[SettingDescription("This is the speed to print everything on the first layer, mm/s.")]
		public double firstLayerSpeed;

		[SettingDescription("The height of the first layer to print, in millimeters.")]
		public double firstLayerThickness;

		[SettingDescription("The fan will be force to stay off below this layer.")]
		public int firstLayerToAllowFan;

		[SettingDescription("If True, support will be generated within the part as well as from the bed.")]
		public bool generateInternalSupport;

		[SettingDescription("If True, an external perimeter will be created around each support island.")]
		public bool generateSupportPerimeter;

		[SettingDescription("If True, support will be generated from the bed. If false no support will be generated at all.")]
		public bool generateSupport;

		[SettingDescription("The percent of support to generate.")]
		public double supportPercent;

		[SettingDescription("The amount the infill extends into the perimeter in millimeters.")]
		public double infillExtendIntoPerimeter;

		[SettingDescription("The percent of filled space to open space while infilling.")]
		public double infillPercent;

		[SettingDescription("mm/s.")]
		public int infillSpeed;

		[SettingDescription("The starting angle that infill lines will be drawn at (angle in x y).")]
		public double infillStartingAngle = 45;

		public ConfigConstants.INFILL_TYPE infillType;

		[SettingDescription("The speed of all perimeters but the outside one. mm/s.")]
		public int insidePerimetersSpeed;

		// if you were to change the layerThicknessMm variable you would add a legacy name so that we can still use old settings
		//[LegacyName("layerThickness")] // the name before we added Mm
		public double layerThickness;

		public bool minimizeSupportColumns;

		[SettingDescription("mm.")]
		public double minimumExtrusionBeforeRetraction;

		//Cool settings
		public int minimumLayerTimeSeconds;

		[SettingDescription("The minimum speed that the extruder is allowed to move while printing. mm/s.")]
		public int minimumPrintingSpeed;

		[SettingDescription("The minimum travel distance that will require a retraction")]
		public double minimumTravelToCauseRetraction;

		// object transform
		public FMatrix3x3 modelRotationMatrix = new FMatrix3x3();

		public int multiExtruderOverlapPercent;
		public int numberOfBottomLayers;
		public int numberOfPerimeters;

		[SettingDescription("The number of loops to draw around objects. Can be used to help hold them down.")]
		public int numberOfSkirtLoops;

		public int numberOfTopLayers;
		public ConfigConstants.OUTPUT_TYPE outputType;

		[SettingDescription("The extrusion width of all outside perimeters")]
		public double outsidePerimeterExtrusionWidth;

		[SettingDescription("Print the outside perimeter before the inside ones. This can help with accuracy.")]
		public bool outsidePerimetersFirst;

		[SettingDescription("The speed of the first perimeter. mm/s.")]
		public int outsidePerimeterSpeed;

		public DoublePoint positionToPlaceObjectCenter;
		public double raftAirGap;
		public double raftExtraDistanceAroundPart;
		public int raftExtruder;

		[SettingDescription("The speed to run the fan during raft printing.")]
		public int raftFanSpeedPercent;

		// Raft read only info
		public int raftPrintSpeed;

		// repair settings
		public double retractionOnExtruderSwitch;

		public double retractionOnTravel;

		[SettingDescription("mm/s.")]
		public int retractionSpeed;

		[SettingDescription("The amount to move the extruder up in z after retracting (before a move). mm.")]
		public double retractionZHop;

		[SettingDescription("How far from objects the first skirt loop should be, in millimeters.")]
		public double skirtDistanceFromObject;

		[SettingDescription("The minimum length of the skirt line, in millimeters.")]
		public int skirtMinLength;

		public string startCode;
		public double supportAirGap;
		public int supportExtruder;

		[SettingDescription("Support extrusion percent.")]
		public double supportExtrusionPercent;

		[SettingDescription("The starting angle that the support lines will be drawn at (similar to infill start angle).")]
		public double supportInfillStartingAngle;

		public int supportInterfaceExtruder;
		public int supportInterfaceLayers;
		public double supportLineSpacing;

		[SettingDescription("mm/s.")]
		public int supportMaterialSpeed;

		[SettingDescription("The number of layers to skip in z. The gap between the support and the model.")]
		public int supportNumberOfLayersToSkipInZ;

		//Support material
		public ConfigConstants.SUPPORT_TYPE supportType;

		[SettingDescription("The closest xy distance that support will be to the object. mm/s.")]
		public double supportXYDistanceFromObject;

		public string toolChangeCode;

		[SettingDescription("This is the speed to print the top layer infill, mm/s.")]
		public double topInfillSpeed;

		[SettingDescription("The speed to move when not extruding material. mm/s.")]
		public int travelSpeed;

		[SettingDescription("The amount of extra extrusion to do when unretracting (resume printing after retraction).")]
		public double unretractExtraExtrusion = 0;

		[SettingDescription("If set will cause the head to try to wipe itself off after retracting.")]
		public bool wipeAfterRetraction;

		[SettingDescription("If greater than 0 this creates an outline around shapes so the extrude will be wiped when entering.")]
		public double wipeShieldDistanceFromObject;

		[SettingDescription("Unlike the wipe shield this is a square of size*size in the lower left corner for wiping during extruder changing.")]
		public double wipeTowerSize;

		public double zOffset;

		public string BooleanOpperations { get; set; } = "";
		public int bottomClipAmount_um { get { return (int)(bottomClipAmount * 1000); } }

		public int extrusionWidth_um { get { return (int)(extrusionWidth * 1000); } }

		public int filamentDiameter_um { get { return (int)(filamentDiameter * 1000); } }

		public int firstLayerExtrusionWidth_um { get { return (int)(firstLayerExtrusionWidth * 1000); } }

		public int firstLayerThickness_um { get { return (int)(firstLayerThickness * 1000); } }

		public int infillExtendIntoPerimeter_um { get { return (int)(infillExtendIntoPerimeter * 1000); } }

		public int layerThickness_um { get { return (int)(layerThickness * 1000); } }

		public int minimumTravelToCauseRetraction_um { get { return (int)(minimumTravelToCauseRetraction * 1000); } }

		public int outsideExtrusionWidth_um { get { return (int)(outsidePerimeterExtrusionWidth * 1000); } }

		public IntPoint positionToPlaceObjectCenter_um { get { return new IntPoint(positionToPlaceObjectCenter.X * 1000, positionToPlaceObjectCenter.Y * 1000); } }

		public int raftAirGap_um { get { return (int)(raftAirGap * 1000); } }

		public int raftBaseExtrusionWidth_um { get { return extrusionWidth_um * 3; } }

		public int raftBaseLineSpacing_um { get { return (int)(extrusionWidth_um * 4); } }

		public int raftBaseThickness_um { get { return extrusionWidth_um * 300 / 400; } }

		public int raftExtraDistanceAroundPart_um { get { return (int)(raftExtraDistanceAroundPart * 1000); } }

		public int raftInterfaceExtrusionWidth_um { get { return extrusionWidth_um * 350 / 400; } }

		public int raftInterfaceLineSpacing_um { get { return extrusionWidth_um * 1000 / 400; } }

		// the least it can be in the raftExtrusionWidth_um
		public int raftInterfaceThicknes_um { get { return extrusionWidth_um * 250 / 400; } }

		public int raftSurfaceExtrusionWidth_um { get { return extrusionWidth_um * 400 / 400; } }

		public int raftSurfaceLayers { get { return 2; } }

		public int raftSurfaceLineSpacing_um { get { return extrusionWidth_um * 400 / 400; } }

		public int raftSurfacePrintSpeed { get { return raftPrintSpeed; } }

		public int raftSurfaceThickness_um { get { return extrusionWidth_um * 250 / 400; } }

		public int skirtDistance_um { get { return (int)(skirtDistanceFromObject * 1000); } }

		public int skirtMinLength_um { get { return (int)(skirtMinLength * 1000); } }

		public int supportAirGap_um { get { return (int)(supportAirGap * 1000); } }

		public int supportExtrusionWidth_um { get { return (int)(extrusionWidth * (supportExtrusionPercent / 100.0) * 1000); } }

		public int supportLineSpacing_um { get { return (int)(supportLineSpacing * 1000); } }

		public int supportXYDistance_um { get { return (int)(supportXYDistanceFromObject * 1000); } }

		public int wipeShieldDistanceFromShapes_um { get { return (int)(wipeShieldDistanceFromObject * 1000); } }

		public int wipeTowerSize_um { get { return (int)(wipeTowerSize * 1000); } }

		// .4 mm for .4 mm nozzle
		public int zOffset_um { get { return (int)(zOffset * 1000); } }

		// .4 mm for .4 mm nozzle
		public void DumpSettings(string fileName)
		{
			List<string> lines = new List<string>();
			FieldInfo[] fields;
			fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
			foreach (FieldInfo field in fields)
			{
				string fieldDescription = "";
				System.Attribute[] attributes = System.Attribute.GetCustomAttributes(field);
				foreach (Attribute attribute in attributes)
				{
					LegacyName legacyName = attribute as LegacyName;
					if (legacyName != null)
					{
						string Name = legacyName.Name;
					}

					SettingDescription description = attribute as SettingDescription;
					if (description != null)
					{
						fieldDescription = " # {0}".FormatWith(description.Description);
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
						lines.Add("{0}={1}{2}".FormatWith(name, value, fieldDescription));
						break;

					case "IntPoint":
						lines.Add("{0}={1}{2}".FormatWith(name, ((IntPoint)value).OutputInMm(), fieldDescription));
						break;

					case "DoublePoint":
						lines.Add("{0}=[{1},{2}]{3}".FormatWith(name, ((DoublePoint)value).X, ((DoublePoint)value).Y, fieldDescription));
						break;

					case "IntPoint[]":
						{
							IntPoint[] valueIntArray = value as IntPoint[];
							string values = "[";
							bool first = true;
							foreach (IntPoint intPoint in valueIntArray)
							{
								if (!first)
								{
									values += ",";
								}
								values = values + intPoint.OutputInMm();
								first = false;
							}
							lines.Add("{0}={1}]{2}".FormatWith(name, values, fieldDescription));
						}
						break;

					case "String":
						if (fieldDescription != "")
						{
							throw new Exception("We can't output a description on a string as we need to write whatever the string says.");
						}
						// change the cariage returns to '\n's in the file
						lines.Add("{0}={1}".FormatWith(name, value).Replace("\n", "\\n"));
						break;

					case "REPAIR_OUTLINES":
					case "REPAIR_OVERLAPS":
					case "SUPPORT_TYPE":
					case "OUTPUT_TYPE":
					case "INFILL_TYPE":
						// all the enums can be output by this function
						lines.Add("{0}={1} # {2}{3}".FormatWith(name, value, GetEnumHelpText(field.FieldType, field.FieldType.Name), fieldDescription));
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

		// .250 mm for .4 mm nozzle
		public bool ReadSettings(string fileName)
		{
			if (File.Exists(fileName))
			{
				string[] lines = File.ReadAllLines(fileName);
				for (int i = 0; i < lines.Length; i++)
				{
					string line = lines[i];
					int commentStart = line.IndexOf("#");
					if (commentStart >= 0)
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

		// .35 mm for .4 mm nozzle
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
									points.Add(new IntPoint(double.Parse(xyValues[0]) * 1000, double.Parse(xyValues[1]) * 1000));

									nextIndex = GetInsides(out intPointString, bracketContents, '[', ']', nextIndex);
								} while (nextIndex != -1);
								field.SetValue(this, points.ToArray());
							}
							break;

						case "String":
							field.SetValue(this, valueToSetTo.Replace("\\n", "\n"));
							break;

						case "REPAIR_OVERLAPS":
						case "REPAIR_OUTLINES":
						case "SUPPORT_TYPE":
						case "INFILL_TYPE":
						case "OUTPUT_TYPE":
							try
							{
								valueToSetTo = valueToSetTo.Replace('|', ',');
								field.SetValue(this, Enum.Parse(field.FieldType, valueToSetTo));
							}
							catch (Exception)
							{
							}
							break;

						default:
							throw new NotImplementedException("unknown type");
					}

					return true;
				}
			}
			return false;
		}

		// 1 mm for .4 mm nozzle
		public void SetToDefault()
		{
			filamentDiameter = 2.89;
			extrusionMultiplier = 1;
			firstLayerThickness = .3;
			layerThickness = .1;
			firstLayerExtrusionWidth = .8;
			extrusionWidth = .4;
			supportExtrusionPercent = 100;
			numberOfPerimeters = 2;
			numberOfBottomLayers = 6;
			numberOfTopLayers = 6;
			firstLayerSpeed = 20;
			topInfillSpeed = 20;
			bottomInfillSpeed = 20;
			supportMaterialSpeed = 40;
			infillSpeed = 50;
			bridgeSpeed = 20;
			bridgeFanSpeedPercent = 100;
			retractWhenChangingIslands = true;
			raftFanSpeedPercent = 100;
			outsidePerimeterSpeed = 50;
			outsidePerimeterExtrusionWidth = extrusionWidth;
			insidePerimetersSpeed = 50;
			travelSpeed = 200;
			firstLayerToAllowFan = 2;
			skirtDistanceFromObject = 6;
			numberOfSkirtLoops = 1;
			skirtMinLength = 0;
			infillPercent = 20;
			infillExtendIntoPerimeter = .06;
			infillStartingAngle = 45;
			infillType = ConfigConstants.INFILL_TYPE.GRID;
			centerObjectInXy = true;
			positionToPlaceObjectCenter.X = 102.5;
			positionToPlaceObjectCenter.Y = 102.5;
			bottomClipAmount = 0;

			// raft settings
			enableRaft = false;
			raftAirGap = .2; // .2 mm for .4 mm nozzle
			supportAirGap = .3; //
			raftExtraDistanceAroundPart = 5;

			supportType = ConfigConstants.SUPPORT_TYPE.GRID;
			generateSupport = false;
			supportPercent = 50;
			generateInternalSupport = true;
			generateSupportPerimeter = true;
			raftExtruder = -1;
			supportLineSpacing = extrusionWidth * 5;
			supportExtruder = -1;
			supportXYDistanceFromObject = .7;
			supportNumberOfLayersToSkipInZ = 1;
			supportInterfaceLayers = 3;
			minimizeSupportColumns = false; // experimental and not working well enough yet
			supportInterfaceExtruder = -1;
			retractionOnTravel = 4.5;
			retractionSpeed = 45;
			retractionOnExtruderSwitch = 14.5;
			minimumTravelToCauseRetraction = 10;
			minimumExtrusionBeforeRetraction = 0;
			wipeShieldDistanceFromObject = 0;
			avoidCrossingPerimeters = true;
			wipeTowerSize = 5;
			multiExtruderOverlapPercent = 0;

			minimumLayerTimeSeconds = 5;
			minimumPrintingSpeed = 10;
			fanSpeedMinPercent = 100;
			fanSpeedMaxPercent = 100;

			continuousSpiralOuterPerimeter = false;
			outputType = ConfigConstants.OUTPUT_TYPE.REPRAP;

			startCode =
							"M109 S210     ;Heatup to 210C\n" +
							"G21           ;metric values\n" +
							"G90           ;absolute positioning\n" +
							"G28           ;Home\n" +
							"G92 E0        ;zero the extruded length\n";
			endCode =
				"M104 S0                     ;extruder heater off\n" +
				"M140 S0                     ;heated bed heater off (if you have it)\n" +
				"M84                         ;steppers off\n";
		}

		public bool ShouldGenerateRaft()
		{
			ConfigSettings config = this;
			return config.enableRaft
				&& config.raftBaseThickness_um > 0
				&& config.raftInterfaceThicknes_um > 0;
		}

		// .25 mm for .4 mm nozzle
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
						insides = content.Substring(firstOpen + 1, i - (firstOpen + 1));
						break;
					}
				}
			}

			return endPosition;
		}
	}

	// this class is so that we can change the name of a variable and not break old settings files
	[System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = true)]
	public class LegacyName : System.Attribute
	{
		private string name;

		public LegacyName(string name)
		{
			this.name = name;
		}

		public string Name { get { return name; } }
	}

	// this class is so that we can add a help text to variables in the config file
	[System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field)]
	public class SettingDescription : System.Attribute
	{
		private string description;

		public SettingDescription(string description)
		{
			this.description = description;
		}

		public string Description { get { return description; } }
	}
}