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

using MSClipperLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MatterHackers.MatterSlice
{
	public class ConfigConstants
	{
		public const int MAX_EXTRUDERS = 4;
		public const string VERSION = "1.0";

		public enum INFILL_TYPE
		{
			GRID,
			LINES,
			TRIANGLES,
			HEXAGON,
			CONCENTRIC,
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
		// Store all public instance properties in a local static list to resolve settings mappings
		private static List<PropertyInfo> allProperties = typeof(ConfigSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

		public ConfigSettings()
		{
			SetToDefault();
		}

		[SettingDescription("Avoid crossing any of the perimeters of a shape while printing its parts.")]
		public bool AvoidCrossingPerimeters { get; set; }

		[SettingDescription("The amount to clip off the bottom of the part, in millimeters.")]
		public double BottomClipAmount { get; set; }

		[SettingDescription("This is the speed to print the bottom layers infill, mm/s.")]
		public double BottomInfillSpeed { get; set; }

		[SettingDescription("The speed to run the fan during bridging.")]
		public int BridgeFanSpeedPercent { get; set; }

		public bool RetractWhenChangingIslands { get; set; }

		[SettingDescription("mm/s.")]
		public int BridgeSpeed { get; set; }

		[SettingDescription("Describes if 'positionToPlaceObjectCenter' should be used.")]
		public bool CenterObjectInXy { get; set; }

		// other
		[SettingDescription("This will cause the z height to raise continuously while on the outer perimeter.")]
		public bool ContinuousSpiralOuterPerimeter { get; set; }

		// Raft settings
		[SettingDescription("mm.")]
		public bool EnableRaft { get; set; }

		public string EndCode { get; set; }

		public IntPoint[] ExtruderOffsets { get; set; } = new IntPoint[ConfigConstants.MAX_EXTRUDERS];

		[SettingDescription("Lets you adjust how much material to extrude.")]
		public double ExtrusionMultiplier { get; set; }

		[SettingDescription("The width of the line to extrude.")]
		public double ExtrusionWidth { get; set; }

		public int FanSpeedMaxPercent { get; set; }

		public int FanSpeedMinPercent { get; set; }

		[SettingDescription("The width of the filament being fed into the extruder, in millimeters.")]
		public double FilamentDiameter { get; set; }

		[SettingDescription("The width of the line to extrude for the first layer.")]
		public double FirstLayerExtrusionWidth { get; set; }

		// speed settings
		[SettingDescription("This is the speed to print everything on the first layer, mm/s.")]
		public double FirstLayerSpeed { get; set; }

		[SettingDescription("The height of the first layer to print, in millimeters.")]
		public double FirstLayerThickness { get; set; }

		[SettingDescription("The fan will be force to stay off below this layer.")]
		public int FirstLayerToAllowFan { get; set; }

		[SettingDescription("If True, support will be generated within the part as well as from the bed.")]
		public bool GenerateInternalSupport { get; set; }

		[SettingDescription("If True, an external perimeter will be created around each support island.")]
		public bool GenerateSupportPerimeter { get; set; }

		[SettingDescription("If True, support will be generated from the bed. If false no support will be generated at all.")]
		public bool GenerateSupport { get; set; }

		[SettingDescription("The percent of support to generate.")]
		public double SupportPercent { get; set; }

		[SettingDescription("The amount the infill extends into the perimeter in millimeters.")]
		public double InfillExtendIntoPerimeter { get; set; }

		[SettingDescription("The percent of filled space to open space while infilling.")]
		public double InfillPercent { get; set; }

		[SettingDescription("mm/s.")]
		public int InfillSpeed { get; set; }

		[SettingDescription("The starting angle that infill lines will be drawn at (angle in x y).")]
		public double InfillStartingAngle { get; set; } = 45;

		public ConfigConstants.INFILL_TYPE InfillType { get; set; }

		[SettingDescription("The speed of all perimeters but the outside one. mm/s.")]
		public int InsidePerimetersSpeed { get; set; }

		// if you were to change the layerThicknessMm variable you would add a legacy name so that we can still use old settings
		//[LegacyName("layerThickness")] // the name before we added Mm
		public double LayerThickness { get; set; }

		public bool MinimizeSupportColumns { get; set; }

		[SettingDescription("mm.")]
		public double MinimumExtrusionBeforeRetraction { get; set; }

		//Cool settings
		public int MinimumLayerTimeSeconds { get; set; }

		[SettingDescription("The minimum speed that the extruder is allowed to move while printing. mm/s.")]
		public int MinimumPrintingSpeed { get; set; }

		[SettingDescription("The minimum travel distance that will require a retraction")]
		public double MinimumTravelToCauseRetraction { get; set; }

		// object transform
		public FMatrix3x3 ModelRotationMatrix { get; set; } = new FMatrix3x3();

		public int MultiExtruderOverlapPercent { get; set; }
		public int NumberOfBottomLayers { get; set; }
		public int NumberOfPerimeters { get; set; }

		public int NumberOfTopLayers { get; set; }

		[SettingDescription("The number of loops to draw around the convex hull")]
		public int NumberOfSkirtLoops { get; set; }

		[SettingDescription("The number of loops to draw around islands.")]
		public int NumberOfBrimLoops { get; set; }

		[SettingDescription("Output only the first layer of the print.")]
		public bool outputOnlyFirstLayer { get; set; }

		[SettingDescription("The extrusion width of all outside perimeters")]
		public double OutsidePerimeterExtrusionWidth { get; set; }

		[SettingDescription("Print the outside perimeter before the inside ones. This can help with accuracy.")]
		public bool OutsidePerimetersFirst { get; set; }

		[SettingDescription("The ratio that the end of a perimeter will overlap the start in nozzle diameters.")]
		public double PerimeterStartEndOverlapRatio { get; set; } = 1;

		[SettingDescription("The speed of the first perimeter. mm/s.")]
		public int OutsidePerimeterSpeed { get; set; }

		public DoublePoint PositionToPlaceObjectCenter { get; set; } = new DoublePoint(0, 0);
		public double RaftAirGap { get; set; }
		public double RaftExtraDistanceAroundPart { get; set; }
		public int RaftExtruder { get; set; }

		[SettingDescription("The speed to run the fan during raft printing.")]
		public int RaftFanSpeedPercent { get; set; }

		// Raft read only info
		public int RaftPrintSpeed { get; set; }

		// repair settings
		public double RetractionOnExtruderSwitch { get; set; }

		public double UnretractExtraOnExtruderSwitch { get; set; }

		public double RetractionOnTravel { get; set; }

		[SettingDescription("mm/s.")]
		public int RetractionSpeed { get; set; }

		[SettingDescription("The amount to move the extruder up in z after retracting (before a move). mm.")]
		public double RetractionZHop { get; set; }

		[SettingDescription("How far from objects the first skirt loop should be, in millimeters.")]
		public double SkirtDistanceFromObject { get; set; }

		[SettingDescription("The minimum length of the skirt line, in millimeters.")]
		public int SkirtMinLength { get; set; }

		public string StartCode { get; set; }
		public double SupportAirGap { get; set; }
		public int SupportExtruder { get; set; }

		[SettingDescription("Support extrusion percent.")]
		public double SupportExtrusionPercent { get; set; }

		[SettingDescription("The starting angle that the support lines will be drawn at (similar to infill start angle).")]
		public double SupportInfillStartingAngle { get; set; }

		public int SupportInterfaceExtruder { get; set; }
		public int SupportInterfaceLayers { get; set; }
		public double SupportLineSpacing { get; set; }

		[SettingDescription("mm/s.")]
		public int SupportMaterialSpeed { get; set; }

		[SettingDescription("The number of layers to skip in z. The gap between the support and the model.")]
		public int SupportNumberOfLayersToSkipInZ { get; set; }

		//Support material
		public ConfigConstants.SUPPORT_TYPE SupportType { get; set; }

		[SettingDescription("The closest xy distance that support will be to the object. mm/s.")]
		public double SupportXYDistanceFromObject { get; set; }

		public string BeforeToolchangeCode { get; set; }

		public string ToolChangeCode { get; set; }

		[SettingDescription("This is the speed to print the top layer infill, mm/s.")]
		public double TopInfillSpeed { get; set; }

		[SettingDescription("The speed to move when not extruding material. mm/s.")]
		public int TravelSpeed { get; set; }

		[SettingDescription("The amount of extra extrusion to do when unretracting (resume printing after retraction).")]
		public double UnretractExtraExtrusion { get; set; } = 0;

		[SettingDescription("If set will cause the head to try to wipe itself off after retracting.")]
		public bool WipeAfterRetraction { get; set; }

		[SettingDescription("If greater than 0 this creates an outline around shapes so the extrude will be wiped when entering.")]
		public double WipeShieldDistanceFromObject { get; set; }

		[SettingDescription("Unlike the wipe shield this is a square of size*size in the lower left corner for wiping during extruder changing.")]
		public double WipeTowerSize { get; set; }

		public double ZOffset { get; set; }

		public string BooleanOpperations { get; set; } = "";
		public int BottomClipAmount_um => (int)(BottomClipAmount * 1000);

		public int ExtrusionWidth_um => (int)(ExtrusionWidth * 1000);

		public int FilamentDiameter_um => (int)(FilamentDiameter * 1000);

		public int FirstLayerExtrusionWidth_um => (int)(FirstLayerExtrusionWidth * 1000);

		public int FirstLayerThickness_um => (int)(FirstLayerThickness * 1000);

		public int InfillExtendIntoPerimeter_um => (int)(InfillExtendIntoPerimeter * 1000);

		public int LayerThickness_um => (int)(LayerThickness * 1000);

		public int MinimumTravelToCauseRetraction_um => (int)(MinimumTravelToCauseRetraction * 1000);

		public int OutsideExtrusionWidth_um => (int)(OutsidePerimeterExtrusionWidth * 1000);

		public IntPoint PositionToPlaceObjectCenter_um => new IntPoint(PositionToPlaceObjectCenter.X * 1000, PositionToPlaceObjectCenter.Y * 1000);

		public int RaftAirGap_um => (int)(RaftAirGap * 1000);

		public int RaftBaseExtrusionWidth_um => ExtrusionWidth_um * 3;

		public int RaftBaseLineSpacing_um => (int)(ExtrusionWidth_um * 4);

		public int RaftBaseThickness_um => ExtrusionWidth_um * 300 / 400;

		public int RaftExtraDistanceAroundPart_um => (int)(RaftExtraDistanceAroundPart * 1000);

		public int RaftInterfaceExtrusionWidth_um => ExtrusionWidth_um * 350 / 400;

		public int RaftInterfaceLineSpacing_um => ExtrusionWidth_um * 1000 / 400;

		// the least it can be in the raftExtrusionWidth_um
		public int RaftInterfaceThicknes_um => ExtrusionWidth_um * 250 / 400;

		public int RaftSurfaceExtrusionWidth_um => ExtrusionWidth_um * 400 / 400;

		public int RaftSurfaceLayers => 2;

		public int RaftSurfaceLineSpacing_um => ExtrusionWidth_um * 400 / 400;

		public int RaftSurfacePrintSpeed => RaftPrintSpeed;

		public int RaftSurfaceThickness_um => ExtrusionWidth_um * 250 / 400;

		public int SkirtDistance_um => (int)(SkirtDistanceFromObject * 1000);

		public int SkirtMinLength_um => (int)(SkirtMinLength * 1000);

		public int SupportAirGap_um => (int)(SupportAirGap * 1000);

		public int SupportExtrusionWidth_um => (int)(ExtrusionWidth * (SupportExtrusionPercent / 100.0) * 1000);

		private int extruderCount = 0;
		public void SetExtruderCount(int count)
		{
			extruderCount = count;
		}

		public int MaxExtruderCount()
		{
			return Math.Max(extruderCount, Math.Max(SupportExtruder + 1, SupportInterfaceExtruder + 1));
		}

		public int SupportLineSpacing_um => (int)(SupportLineSpacing * 1000);

		public int SupportXYDistance_um => (int)(SupportXYDistanceFromObject * 1000);

		public int WipeShieldDistanceFromShapes_um => (int)(WipeShieldDistanceFromObject * 1000);

		public int WipeTowerSize_um => (int)(WipeTowerSize * 1000);

		// .4 mm for .4 mm nozzle
		public int ZOffset_um => (int)(ZOffset * 1000);

		// .4 mm for .4 mm nozzle
		public void DumpSettings(string fileName)
		{
			List<string> lines = new List<string>();
			foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				string fieldDescription = "";
				foreach (Attribute attribute in Attribute.GetCustomAttributes(property))
				{
					SettingDescription description = attribute as SettingDescription;
					if (description != null)
					{
						fieldDescription = " # {0}".FormatWith(description.Description);
					}
				}

				string name = property.Name;
				object value = property.GetValue(this);

				switch (property.PropertyType.Name)
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

						// Replace newline characters with escaped newline characters
						lines.Add("{0}={1}".FormatWith(name, value).Replace("\n", "\\n"));
						break;

					case "REPAIR_OUTLINES":
					case "REPAIR_OVERLAPS":
					case "SUPPORT_TYPE":
					case "OUTPUT_TYPE":
					case "INFILL_TYPE":
						// all the enums can be output by this function
						lines.Add("{0}={1} # {2}{3}".FormatWith(name, value, GetEnumHelpText(property.PropertyType, property.PropertyType.Name), fieldDescription));
						break;

					default:
						throw new NotImplementedException("unknown type '{0}'".FormatWith(property.PropertyType.Name));
				}
			}

			lines.Sort();

			File.WriteAllLines(fileName, lines.ToArray());
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

			foreach (PropertyInfo property in allProperties)
			{
				// List of case insensitive names that will import as this property
				HashSet<string> possibleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				possibleNames.Add(property.Name);

				// TODO: No one makes use of the LegacyName attribute thus the possibleNames HashSet and the LegacyName class could be removed as part of a code cleanup pass
				//
				// Including any mapped LegacyName attributes
				foreach (Attribute attribute in Attribute.GetCustomAttributes(property))
				{
					LegacyName legacyName = attribute as LegacyName;
					if (legacyName != null)
					{
						possibleNames.Add(legacyName.Name);
					}
				}

				if (possibleNames.Contains(keyToSet))
				{
					switch (property.PropertyType.Name)
					{
						case "Int32":
							property.SetValue(this, (int)double.Parse(valueToSetTo));
							break;

						case "Double":
							property.SetValue(this, double.Parse(valueToSetTo));
							break;

						case "Boolean":
							property.SetValue(this, bool.Parse(valueToSetTo));
							break;

						case "FMatrix3x3":
							{
								property.SetValue(this, new FMatrix3x3(valueToSetTo));
							}
							break;

						case "DoublePoint":
							{
								string bracketContents = GetInsides(valueToSetTo, '[', ']');
								string[] xyValues = bracketContents.Split(',');
								property.SetValue(this, new DoublePoint(double.Parse(xyValues[0]), double.Parse(xyValues[1])));
							}
							break;

						case "IntPoint":
							{
								string bracketContents = GetInsides(valueToSetTo, '[', ']');
								string[] xyValues = bracketContents.Split(',');
								property.SetValue(this, new IntPoint(double.Parse(xyValues[0]), double.Parse(xyValues[1])));
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
								property.SetValue(this, points.ToArray());
							}
							break;

						case "String":
							property.SetValue(this, valueToSetTo.Replace("\\n", "\n"));
							break;

						case "REPAIR_OVERLAPS":
						case "REPAIR_OUTLINES":
						case "SUPPORT_TYPE":
						case "INFILL_TYPE":
						case "OUTPUT_TYPE":
							try
							{
								valueToSetTo = valueToSetTo.Replace('|', ',');
								property.SetValue(this, Enum.Parse(property.PropertyType, valueToSetTo));
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
			FilamentDiameter = 2.89;
			ExtrusionMultiplier = 1;
			FirstLayerThickness = .3;
			LayerThickness = .1;
			FirstLayerExtrusionWidth = .8;
			ExtrusionWidth = .4;
			SupportExtrusionPercent = 100;
			NumberOfPerimeters = 2;
			NumberOfBottomLayers = 6;
			NumberOfTopLayers = 6;
			FirstLayerSpeed = 20;
			TopInfillSpeed = 20;
			BottomInfillSpeed = 20;
			SupportMaterialSpeed = 40;
			InfillSpeed = 50;
			BridgeSpeed = 20;
			BridgeFanSpeedPercent = 100;
			RetractWhenChangingIslands = true;
			RaftFanSpeedPercent = 100;
			OutsidePerimeterSpeed = 50;
			OutsidePerimeterExtrusionWidth = ExtrusionWidth;
			InsidePerimetersSpeed = 50;
			TravelSpeed = 200;
			FirstLayerToAllowFan = 2;
			SkirtDistanceFromObject = 6;
			NumberOfSkirtLoops = 1;
			NumberOfBrimLoops = 0;
			SkirtMinLength = 0;
			InfillPercent = 20;
			InfillExtendIntoPerimeter = .06;
			InfillStartingAngle = 45;
			InfillType = ConfigConstants.INFILL_TYPE.GRID;
			CenterObjectInXy = true;
			PositionToPlaceObjectCenter = new DoublePoint(102.5, 102.5);
			BottomClipAmount = 0;

			// raft settings
			EnableRaft = false;
			RaftAirGap = .2; // .2 mm for .4 mm nozzle
			SupportAirGap = .3; //
			RaftExtraDistanceAroundPart = 5;

			SupportType = ConfigConstants.SUPPORT_TYPE.GRID;
			GenerateSupport = false;
			SupportPercent = 50;
			GenerateInternalSupport = true;
			GenerateSupportPerimeter = true;
			RaftExtruder = -1;
			SupportLineSpacing = ExtrusionWidth * 5;
			SupportExtruder = -1;
			SupportXYDistanceFromObject = .7;
			SupportNumberOfLayersToSkipInZ = 1;
			SupportInterfaceLayers = 3;
			MinimizeSupportColumns = false; // experimental and not working well enough yet
			SupportInterfaceExtruder = -1;
			RetractionOnTravel = 4.5;
			RetractionSpeed = 45;
			RetractionOnExtruderSwitch = 14.5;
			UnretractExtraOnExtruderSwitch = 0;
			MinimumTravelToCauseRetraction = 10;
			MinimumExtrusionBeforeRetraction = 0;
			WipeShieldDistanceFromObject = 0;
			AvoidCrossingPerimeters = true;
			WipeTowerSize = 5;
			MultiExtruderOverlapPercent = 0;

			MinimumLayerTimeSeconds = 5;
			MinimumPrintingSpeed = 10;
			FanSpeedMinPercent = 100;
			FanSpeedMaxPercent = 100;

			ContinuousSpiralOuterPerimeter = false;

			StartCode =
							"M109 S210     ;Heatup to 210C\n" +
							"G21           ;metric values\n" +
							"G90           ;absolute positioning\n" +
							"G28           ;Home\n" +
							"G92 E0        ;zero the extruded length\n";
			EndCode =
				"M104 S0                     ;extruder heater off\n" +
				"M140 S0                     ;heated bed heater off (if you have it)\n" +
				"M84                         ;steppers off\n";
		}

		public bool ShouldGenerateRaft()
		{
			ConfigSettings config = this;
			return config.EnableRaft
				&& config.RaftBaseThickness_um > 0
				&& config.RaftInterfaceThicknes_um > 0;
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
	[System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
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