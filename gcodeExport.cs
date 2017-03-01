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
using System.IO;
using System.Text;
using MatterHackers.Pathfinding;
using MatterHackers.QuadTree;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	public class GCodeExport
	{
		private string beforeToolchangeCode;
		private int currentFanSpeed;
		private IntPoint currentPosition_um;
		private double currentSpeed;
		private TimeEstimateCalculator estimateCalculator = new TimeEstimateCalculator();
		private int extruderIndex;
		private IntPoint[] extruderOffset_um = new IntPoint[ConfigConstants.MAX_EXTRUDERS];
		private double extruderSwitchRetraction_mm;
		private double extrusionAmount_mm;
		private double extrusionAmountAtPreviousRetraction_mm;
		private double extrusionPerMm;
		private StreamWriter gcodeFileStream;
		private bool isRetracted;
		private string layerChangeCode;
		private double minimumExtrusionBeforeRetraction_mm;
		private double retractionAmount_mm;
		private int retractionSpeed;
		private List<IntPoint> retractionWipePath = new List<IntPoint>();
		private double retractionZHop_mm;
		private string toolChangeCode;
		private double[] totalFilament_mm = new double[ConfigConstants.MAX_EXTRUDERS];
		private double totalPrintTime;
		private double unretractExtraOnExtruderSwitch_mm;
		private double unretractExtrusionExtra_mm;
		private bool wipeAfterRetraction;
		private long zPos_um;

		public GCodeExport()
		{
			extrusionAmount_mm = 0;
			extrusionPerMm = 0;
			retractionAmount_mm = 0;
			minimumExtrusionBeforeRetraction_mm = 0.0;
			extrusionAmountAtPreviousRetraction_mm = -1;
			extruderSwitchRetraction_mm = 14.5;
			extruderIndex = 0;
			currentFanSpeed = -1;

			totalPrintTime = 0.0;
			for (int e = 0; e < ConfigConstants.MAX_EXTRUDERS; e++)
			{
				totalFilament_mm[e] = 0.0;
			}

			currentSpeed = 0;
			retractionSpeed = 45;
			isRetracted = true;
			gcodeFileStream = new StreamWriter(Console.OpenStandardOutput());
		}

		public long CurrentZ { get { return zPos_um; } }

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
			return totalPrintTime;
		}

		public bool IsOpened()
		{
			return gcodeFileStream != null;
		}

		public void LayerChanged(int layerIndex)
		{
			if (!string.IsNullOrEmpty(layerChangeCode))
			{
				WriteCode("; Layer Change GCode");
				WriteCode(layerChangeCode.Replace("[layer_num]", layerIndex.ToString()));
			}
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

		public void SetExtruderOffset(int extruderIndex, IntPoint extruderOffset_um, int z_offset_um)
		{
			this.extruderOffset_um[extruderIndex] = new IntPoint(extruderOffset_um.X, extruderOffset_um.Y, z_offset_um);
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

		public void SetRetractionSettings(double retractionAmount, int retractionSpeed, double extruderSwitchRetraction, double minimumExtrusionBeforeRetraction_mm, double retractionZHop_mm, bool wipeAfterRetraction, double unretractExtrusionExtra_mm, double unretractExtraOnExtruderSwitch_mm)
		{
			this.unretractExtrusionExtra_mm = unretractExtrusionExtra_mm;
			this.unretractExtraOnExtruderSwitch_mm = unretractExtraOnExtruderSwitch_mm;
			this.wipeAfterRetraction = wipeAfterRetraction;
			this.retractionAmount_mm = retractionAmount;
			this.retractionSpeed = retractionSpeed;
			this.extruderSwitchRetraction_mm = extruderSwitchRetraction;
			this.minimumExtrusionBeforeRetraction_mm = minimumExtrusionBeforeRetraction_mm;
			this.retractionZHop_mm = retractionZHop_mm;
		}

		public void SetToolChangeCode(string toolChangeCode, string beforeToolchangeCode)
		{
			this.toolChangeCode = toolChangeCode;
			this.beforeToolchangeCode = beforeToolchangeCode;
		}

		public void SetZ(long z)
		{
			this.zPos_um = z;
		}

		public void SwitchExtruder(int newExtruder)
		{
			if (extruderIndex == newExtruder)
			{
				return;
			}

			if (!string.IsNullOrEmpty(beforeToolchangeCode))
			{
				WriteCode("; Before Tool Change GCode");
				WriteCode(beforeToolchangeCode);
			}

			if (extruderSwitchRetraction_mm != 0)
			{
				gcodeFileStream.Write("G1 F{0} E{1:0.####} ; retract\n", retractionSpeed * 60, extrusionAmount_mm - extruderSwitchRetraction_mm);
			}

			currentSpeed = retractionSpeed;

			ResetExtrusionValue();
			extruderIndex = newExtruder;

			isRetracted = true;
			extrusionAmount_mm = extruderSwitchRetraction_mm + unretractExtraOnExtruderSwitch_mm;

			gcodeFileStream.Write("T{0} ; switch extruder\n".FormatWith(extruderIndex));

			if (toolChangeCode != null && toolChangeCode != "")
			{
				WriteCode("; After Tool Change GCode");
				WriteCode(toolChangeCode);
			}
		}

		public void TellFileSize()
		{
			double fsize = gcodeFileStream.BaseStream.Length;
			if (fsize > 1024 * 1024)
			{
				fsize /= 1024.0 * 1024.0;
				LogOutput.Log("Wrote {0:0.0} MB.\n".FormatWith(fsize));
			}
			if (fsize > 1024)
			{
				fsize /= 1024.0;
				LogOutput.Log("Wrote {0:0.0} kilobytes.\n".FormatWith(fsize));
			}
		}

		public void UpdateTotalPrintTime()
		{
			totalPrintTime += estimateCalculator.calculate();
			estimateCalculator.reset();
		}

		public void WriteCode(string str)
		{
			gcodeFileStream.Write("{0}\n".FormatWith(str));
		}

		public void WriteComment(string comment)
		{
			gcodeFileStream.Write("; {0}\n".FormatWith(comment));
		}

		public void WriteFanCommand(int speed)
		{
			if (currentFanSpeed == speed)
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
			currentFanSpeed = speed;
		}

		public void WriteLine(string line)
		{
			gcodeFileStream.Write("{0}\n".FormatWith(line));
		}

		public void WriteMove(IntPoint movePosition_um, double speed, long lineWidth_um)
		{
			StringBuilder lineToWrite = new StringBuilder();

			if(movePosition_um.Width != lineWidth_um)
			{
				int a = 0;
			}

			if(currentPosition_um == movePosition_um)
			{
				return;
			}

			//Normal E handling.
			if (lineWidth_um != 0)
			{
				IntPoint diff = movePosition_um - GetPosition();
				if (isRetracted)
				{
					if (retractionZHop_mm > 0)
					{
						double zWritePosition = (double)(currentPosition_um.Z - extruderOffset_um[extruderIndex].Z) / 1000;
						lineToWrite.Append("G1 Z{0:0.###}\n".FormatWith(zWritePosition));
					}

					if (extrusionAmount_mm > 10000.0)
					{
						//According to https://github.com/Ultimaker/CuraEngine/issues/14 having more then 21m of extrusion causes inaccuracies. So reset it every 10m, just to be sure.
						ResetExtrusionValue(retractionAmount_mm);
					}

					lineToWrite.Append("G1 F{0} E{1:0.#####}\n".FormatWith(retractionSpeed * 60, extrusionAmount_mm));

					currentSpeed = retractionSpeed;
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
				lineToWrite.Append(" F{0}".FormatWith(speed * 60));
				currentSpeed = speed;
			}

			double xWritePosition = (double)(movePosition_um.X - extruderOffset_um[extruderIndex].X) / 1000.0;
			double yWritePosition = (double)(movePosition_um.Y - extruderOffset_um[extruderIndex].Y) / 1000.0;
			lineToWrite.Append(" X{0:0.###} Y{1:0.###}".FormatWith(xWritePosition, yWritePosition));

			if (movePosition_um.Z != currentPosition_um.Z)
			{
				double zWritePosition = (double)(movePosition_um.Z - extruderOffset_um[extruderIndex].Z) / 1000.0;
				if (lineWidth_um == 0
					&& isRetracted)
				{
					zWritePosition += retractionZHop_mm;
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

			//If wipe enabled remember path, but stop at 100 moves to keep memory usage low
			if (wipeAfterRetraction)
			{
				retractionWipePath.Add(movePosition_um);
				if (retractionWipePath.Count > 100)
				{
					retractionWipePath.RemoveAt(0);
				}
			}

			currentPosition_um = movePosition_um;
			estimateCalculator.plan(new TimeEstimateCalculator.Position(currentPosition_um.X / 1000.0, currentPosition_um.Y / 1000.0, currentPosition_um.Z / 1000.0, extrusionAmount_mm), speed);
		}

		public void WriteRetraction()
		{
			double initialSpeed = currentSpeed;

			if (retractionAmount_mm > 0
				&& !isRetracted
				&& extrusionAmountAtPreviousRetraction_mm + minimumExtrusionBeforeRetraction_mm < extrusionAmount_mm)
			{
				gcodeFileStream.Write("G1 F{0} E{1:0.#####}\n".FormatWith(retractionSpeed * 60, extrusionAmount_mm - retractionAmount_mm));
				currentSpeed = retractionSpeed;
				estimateCalculator.plan(new TimeEstimateCalculator.Position((double)(currentPosition_um.X) / 1000.0, (currentPosition_um.Y) / 1000.0, (double)(currentPosition_um.Z) / 1000.0, extrusionAmount_mm - retractionAmount_mm), currentSpeed);

				AddRetractionWipeIfRequired(initialSpeed);

				if (retractionZHop_mm > 0)
				{
					double zWritePosition = (double)(currentPosition_um.Z - extruderOffset_um[extruderIndex].Z) / 1000 + retractionZHop_mm;
					gcodeFileStream.Write("G1 Z{0:0.###}\n".FormatWith(zWritePosition));
				}

				// Make sure after a retraction that we will extrude the extra amount on unretraction that the settings want.
				extrusionAmount_mm += unretractExtrusionExtra_mm;

				extrusionAmountAtPreviousRetraction_mm = extrusionAmount_mm;
				isRetracted = true;
			}
		}

		private void AddRetractionWipeIfRequired(double initialSpeed)
		{
			//This wipes the extruder back along the previous path after retracting.
			if (wipeAfterRetraction && retractionWipePath.Count >= 2)
			{
				IntPoint lastP = retractionWipePath[retractionWipePath.Count - 1];
				int indexStepDirection = -1;
				int i = retractionWipePath.Count - 2;
				double wipeDistanceMm = 10;
				long wipeLeft = (long)(wipeDistanceMm * 1000);

				while (wipeLeft > 0)
				{
					IntPoint p = retractionWipePath[i];
					long len = (lastP - p).Length();

					//Check if we're out of moves
					if (indexStepDirection > 0 && i == retractionWipePath.Count - 1)
					{
						break;
					}
					//Reverse direction (once) to get wipe length if required.
					else if (indexStepDirection < 0 && i == 0)
					{
						indexStepDirection = 1;
					}
					i += indexStepDirection;

					//If move is longer than wipe remaining, calculate angle and move along path but stop short.
					if (len > wipeLeft)
					{
						IntPoint direction = p - lastP;
						long directionLength = direction.Length();
						direction *= wipeLeft;
						direction /= directionLength;
						p = lastP + direction;
						len = wipeLeft;
					}
					wipeLeft -= len;
					lastP = p;
					gcodeFileStream.Write("G0 ");
					if (currentSpeed != initialSpeed)
					{
						currentSpeed = initialSpeed;
						gcodeFileStream.Write("F{0} ".FormatWith(currentSpeed * 60));
					}
					gcodeFileStream.Write("X{0:0.###} Y{1:0.###}\n".FormatWith((p.X - extruderOffset_um[extruderIndex].X) / 1000.0, (p.Y - extruderOffset_um[extruderIndex].Y) / 1000.0));
					estimateCalculator.plan(new TimeEstimateCalculator.Position(p.X / 1000.0, p.Y / 1000.0, currentPosition_um.Z / 1000.0, 0), currentSpeed);
				}
				retractionWipePath.Clear();
			}
		}
	}
}