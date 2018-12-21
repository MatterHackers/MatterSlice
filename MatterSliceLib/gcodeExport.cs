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
using System.IO;
using System.Text;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	public class GCodeExport
	{
		public int CurrentFanSpeed { get; private set; }
		private IntPoint currentPosition_um;
		private double currentSpeed;
		private TimeEstimateCalculator estimateCalculator = new TimeEstimateCalculator();
		private int extruderIndex;
		private double[] extruderZOffset_um = new double[ConfigConstants.MAX_EXTRUDERS];
		private bool[] extruderHaseBeenRetracted = new bool[ConfigConstants.MAX_EXTRUDERS];
		private double extrusionAmount_mm;
		private double extrusionAmountAtPreviousRetraction_mm;
		private double extrusionPerMm;
		private StreamWriter gcodeFileStream;
		private bool isRetracted;
		private string layerChangeCode;
		private ConfigSettings config;
		private double[] totalFilament_mm = new double[ConfigConstants.MAX_EXTRUDERS];
		private double layerPrintTime;
		double _layerSpeedRatio = 1;

		public double LayerSpeedRatio
		{
			get { return _layerSpeedRatio; }
			set
			{
				var maxChange = .1;
				if (Math.Abs(_layerSpeedRatio - value) > maxChange)
				{
					_layerSpeedRatio = value > _layerSpeedRatio ? _layerSpeedRatio + maxChange : _layerSpeedRatio - maxChange;
				}
				else
				{
					_layerSpeedRatio = value;
				}

				_layerSpeedRatio = Math.Min(1, Math.Max(maxChange, _layerSpeedRatio));
			}
		}

		public GCodeExport(ConfigSettings config)
		{
			this.config = config;
			extrusionAmount_mm = 0;
			extrusionPerMm = 0;
			extrusionAmountAtPreviousRetraction_mm = -1;
			extruderIndex = 0;
			CurrentFanSpeed = -1;

			layerPrintTime = 0.0;
			for (int e = 0; e < ConfigConstants.MAX_EXTRUDERS; e++)
			{
				totalFilament_mm[e] = 0.0;
			}

			currentSpeed = 0;
			isRetracted = true;
			gcodeFileStream = new StreamWriter(Console.OpenStandardOutput());
		}

		public double CurrentZ => CurrentZ_um / 1000.0;
		public long CurrentZ_um { get; set; }

		public int LayerIndex { get; set; } = 0;

		public void Close()
		{
			gcodeFileStream.Close();
		}

		public void Finalize(long maxObjectHeight, int moveSpeed, string endCode)
		{
			WriteFanCommand(0);
			WriteCode(endCode);
			WriteComment("filament used = {0:0.0}".FormatWith(GetTotalFilamentUsed(0) + GetTotalFilamentUsed(1)));
			WriteComment("filament used extruder 1 (mm) = {0:0.0}".FormatWith(GetTotalFilamentUsed(0)));
			WriteComment("filament used extruder 2 (mm) = {0:0.0}".FormatWith(GetTotalFilamentUsed(1)));
			WriteComment("total print time (s) = {0:0}".FormatWith(GetTotalPrintTime()));

			LogOutput.Log("Print time: {0}\n".FormatWith((int)(GetTotalPrintTime())));
			LogOutput.Log("Filament: {0}\n".FormatWith((int)(GetTotalFilamentUsed(0))));
			LogOutput.Log("Filament2: {0}\n".FormatWith((int)(GetTotalFilamentUsed(1))));
		}

		public int GetExtruderIndex()
		{
			return extruderIndex;
		}

		public long GetFileSize()
		{
			return gcodeFileStream.BaseStream.Length;
		}

		public IntPoint GetPosition()
		{
			return currentPosition_um;
		}

		public IntPoint GetPositionXY()
		{
			return new IntPoint(currentPosition_um.X, currentPosition_um.Y);
		}

		public long GetPositionZ()
		{
			return currentPosition_um.Z;
		}

		public double GetTotalFilamentUsed(int extruderIndexToGet)
		{
			if (extruderIndexToGet == extruderIndex)
			{
				return totalFilament_mm[extruderIndexToGet] + extrusionAmount_mm;
			}

			return totalFilament_mm[extruderIndexToGet];
		}

		public double GetTotalPrintTime()
		{
			return layerPrintTime;
		}

		public bool IsOpened()
		{
			return gcodeFileStream != null;
		}

		public void LayerChanged(int layerIndex, long layerHeight_um)
		{
			LayerIndex = layerIndex;
			if (!string.IsNullOrEmpty(layerChangeCode))
			{
				WriteComment("Layer Change GCode");
				WriteCode(layerChangeCode.Replace("[layer_num]", layerIndex.ToString()));
			}

			WriteComment($"LAYER_HEIGHT:{layerHeight_um / 1000.0:0.####}");
		}

		public void ResetExtrusionValue(double extraExtrudeAmount_mm = 0)
		{
			if (extrusionAmount_mm != 0.0)
			{
				gcodeFileStream.Write("G92 E0 ; reset extrusion\n");
				totalFilament_mm[extruderIndex] += extrusionAmount_mm;
				extrusionAmountAtPreviousRetraction_mm -= extrusionAmount_mm;
				extrusionAmount_mm = extraExtrudeAmount_mm;
			}
		}

		public void SetExtruderOffset(int extruderIndex, int z_offset_um)
		{
			this.extruderZOffset_um[extruderIndex] = z_offset_um;
		}

		public void SetExtrusion(int layerThickness, int filamentDiameter, double extrusionMultiplier)
		{
			//double feedRateRatio = 1 + (Math.PI / 4 - 1) * layerThickness / extrusionWidth;
			//extrusionMultiplier *= feedRateRatio;
			double filamentArea = Math.PI * ((double)(filamentDiameter) / 1000.0 / 2.0) * ((double)(filamentDiameter) / 1000.0 / 2.0);
			extrusionPerMm = (double)(layerThickness) / 1000.0 / filamentArea * extrusionMultiplier;
		}

		public void SetFilename(string filename)
		{
			filename = filename.Replace("\"", "");
			gcodeFileStream = new StreamWriter(filename);
		}

		public void SetLayerChangeCode(string layerChangeCode)
		{
			this.layerChangeCode = layerChangeCode;
		}

		public void SwitchExtruder(int newExtruder)
		{
			if (extruderIndex == newExtruder)
			{
				return;
			}

			if(newExtruder == 1 
				&& config.BeforeToolchangeCode1 != "")
			{
				var code = config.BeforeToolchangeCode1.Replace("[wipe_tower_x]", config.WipeCenterX.ToString());
				code = code.Replace("[wipe_tower_y]", config.WipeCenterY.ToString());
				code = code.Replace("[wipe_tower_z]", CurrentZ.ToString("0.####"));
				WriteCode("; Before Tool 1 Change GCode");
				WriteCode(code);
			}
			else if (!string.IsNullOrEmpty(config.BeforeToolchangeCode))
			{
				var code = config.BeforeToolchangeCode.Replace("[wipe_tower_x]", config.WipeCenterX.ToString());
				code = code.Replace("[wipe_tower_y]", config.WipeCenterY.ToString());
				code = code.Replace("[wipe_tower_z]", CurrentZ.ToString("0.####"));
				WriteCode("; Before Tool Change GCode");
				WriteCode(code);
			}

			if (config.RetractionOnExtruderSwitch != 0)
			{
				extruderHaseBeenRetracted[extruderIndex] = true;
				gcodeFileStream.Write("G1 E{0:0.####} F{1} ; retract\n", extrusionAmount_mm - config.RetractionOnExtruderSwitch, config.RetractionSpeed * 60);
			}

			currentSpeed = config.RetractionSpeed;

			ResetExtrusionValue();
			extruderIndex = newExtruder;

			isRetracted = true;
			if (config.RetractionOnExtruderSwitch != 0
				&& extruderHaseBeenRetracted[extruderIndex])
			{
				extrusionAmount_mm = config.RetractionOnExtruderSwitch;
			}

			gcodeFileStream.Write("T{0} ; switch extruder\n".FormatWith(extruderIndex));

			if (newExtruder == 1 
				&& !string.IsNullOrEmpty(config.ToolChangeCode1))
			{
				var code = config.ToolChangeCode1.Replace("[wipe_tower_x]", config.WipeCenterX.ToString());
				code = code.Replace("[wipe_tower_y]", config.WipeCenterY.ToString());
				code = code.Replace("[wipe_tower_z]", CurrentZ.ToString("0.####"));
				WriteCode("; After Tool 1 Change GCode");
				WriteCode(code);
			}
			else if (!string.IsNullOrEmpty(config.ToolChangeCode))
			{
				var code = config.ToolChangeCode.Replace("[wipe_tower_x]", config.WipeCenterX.ToString());
				code = code.Replace("[wipe_tower_y]", config.WipeCenterY.ToString());
				code = code.Replace("[wipe_tower_z]", CurrentZ.ToString("0.####"));
				WriteCode("; After Tool Change GCode");
				WriteCode(code);
			}

			// if there is a wipe tower go to it
		}

		public void UpdateLayerPrintTime()
		{
			layerPrintTime += estimateCalculator.calculate();
			estimateCalculator.reset();
		}

		public void WriteCode(string str)
		{
			gcodeFileStream.Write("{0}\n".FormatWith(str));
		}

		public void WriteComment(string comment)
		{
			gcodeFileStream.Write($"; {comment}\n");
		}

		/// <summary>
		/// Emit a fan command right now. This will not be part of the queued commands.
		/// </summary>
		/// <param name="speed"></param>
		public void WriteFanCommand(int speed)
		{
			if (CurrentFanSpeed == speed)
			{
				return;
			}

			// Exhaust the buffer before changing the fan speed
			gcodeFileStream.Write("M400\n");

			if (speed > 0)
			{
				gcodeFileStream.Write("M106 S{0}\n".FormatWith(speed * 255 / 100));
			}
			else
			{
				gcodeFileStream.Write("M107\n");
			}
			CurrentFanSpeed = speed;
		}

		public void WriteLine(string line)
		{
			gcodeFileStream.Write("{0}\n".FormatWith(line));
		}

		public void WriteMove(IntPoint movePosition_um, double speed, long lineWidth_um)
		{
			StringBuilder lineToWrite = new StringBuilder();

			if (currentPosition_um == movePosition_um)
			{
				return;
			}

			//Normal E handling.
			if (lineWidth_um != 0)
			{
				IntPoint diff = movePosition_um - GetPosition();
				if (isRetracted)
				{
					if (config.RetractionZHop > 0)
					{
						double zWritePosition = (double)(currentPosition_um.Z - extruderZOffset_um[extruderIndex]) / 1000;
						lineToWrite.Append("G1 Z{0:0.###}\n".FormatWith(zWritePosition));
					}

					if (config.ResetLongExtrusion
						&& extrusionAmount_mm > 10000.0)
					{
						//According to https://github.com/Ultimaker/CuraEngine/issues/14 having more then 21m of extrusion causes inaccuracies. So reset it every 10m, just to be sure.
						ResetExtrusionValue(config.RetractionOnTravel);
					}

					lineToWrite.Append("G1 F{0} E{1:0.#####}\n".FormatWith(config.RetractionSpeed * 60, extrusionAmount_mm));

					currentSpeed = config.RetractionSpeed;
					estimateCalculator.plan(new TimeEstimateCalculator.Position(
						currentPosition_um.X / 1000.0,
						currentPosition_um.Y / 1000.0,
						currentPosition_um.Z / 1000.0,
						extrusionAmount_mm),
						currentSpeed);

					isRetracted = false;
				}

				extrusionAmount_mm += extrusionPerMm * lineWidth_um / 1000.0 * diff.LengthMm();
				lineToWrite.Append("G1");
			}
			else
			{
				lineToWrite.Append("G0");
			}

			if (currentSpeed != speed)
			{
				lineToWrite.Append(" F{0}".FormatWith((int)(speed * 60)));
				currentSpeed = speed;
			}

			double xWritePosition = (double)(movePosition_um.X) / 1000.0;
			double yWritePosition = (double)(movePosition_um.Y) / 1000.0;
			lineToWrite.Append(" X{0:0.###} Y{1:0.###}".FormatWith(xWritePosition, yWritePosition));

			if (movePosition_um.Z != currentPosition_um.Z)
			{
				double zWritePosition = (double)(movePosition_um.Z - extruderZOffset_um[extruderIndex]) / 1000.0;
				if (lineWidth_um == 0
					&& isRetracted)
				{
					zWritePosition += config.RetractionZHop;
				}
				lineToWrite.Append(" Z{0:0.###}".FormatWith(zWritePosition));
			}

			if (lineWidth_um != 0)
			{
				lineToWrite.Append(" E{0:0.#####}".FormatWith(extrusionAmount_mm));
			}

			lineToWrite.Append("\n");

			if (lineToWrite.Length > 0)
			{
				string lineAsString = lineToWrite.ToString();
				gcodeFileStream.Write(lineAsString);
			}

			currentPosition_um = movePosition_um;
			estimateCalculator.plan(new TimeEstimateCalculator.Position(currentPosition_um.X / 1000.0, currentPosition_um.Y / 1000.0, currentPosition_um.Z / 1000.0, extrusionAmount_mm), speed);
		}

		public void WriteRetraction(double timeForNextMove, bool forceRetraction)
		{
			double initialSpeed = currentSpeed;

			if (config.RetractionOnTravel > 0
				&& !isRetracted
				&& (forceRetraction || extrusionAmountAtPreviousRetraction_mm + config.MinimumExtrusionBeforeRetraction < extrusionAmount_mm))
			{
				gcodeFileStream.Write("G1 F{0} E{1:0.#####}\n".FormatWith(config.RetractionSpeed * 60, extrusionAmount_mm - config.RetractionOnTravel));
				currentSpeed = config.RetractionSpeed;
				var timePosition = new TimeEstimateCalculator.Position((double)(currentPosition_um.X) / 1000.0,
					(currentPosition_um.Y) / 1000.0,
					(double)(currentPosition_um.Z) / 1000.0, extrusionAmount_mm - config.RetractionOnTravel);
				estimateCalculator.plan(timePosition, currentSpeed);

				if (config.RetractionZHop > 0)
				{
					double zWritePosition = (double)(currentPosition_um.Z - extruderZOffset_um[extruderIndex]) / 1000 + config.RetractionZHop;
					gcodeFileStream.Write("G1 Z{0:0.###}\n".FormatWith(zWritePosition));
				}

				// calculate how much time since retract and figure out how much extra extrusion to apply
				double amountOfExtraExtrusionToApply = 1;

				if (config.RetractRestartExtraTimeToApply > 0)
				{
					timeForNextMove = Math.Min(timeForNextMove, config.RetractRestartExtraTimeToApply);
					amountOfExtraExtrusionToApply = timeForNextMove / config.RetractRestartExtraTimeToApply;
				}

				// Make sure after a retraction that we will extrude the extra amount on unretraction that the settings want.
				extrusionAmount_mm += config.UnretractExtraExtrusion * amountOfExtraExtrusionToApply;

				extrusionAmountAtPreviousRetraction_mm = extrusionAmount_mm;
				isRetracted = true;
			}
		}
	}
}