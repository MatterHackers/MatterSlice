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

using MatterSlice.ClipperLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;

	public class GCodeExport
	{
		private int currentFanSpeed;
		private Point3 currentPosition_um;
		private double currentSpeed;
		private TimeEstimateCalculator estimateCalculator = new TimeEstimateCalculator();
		private char[] extruderCharacter = new char[ConfigConstants.MAX_EXTRUDERS];
		private int extruderIndex;
		private Point3[] extruderOffset_um = new Point3[ConfigConstants.MAX_EXTRUDERS];
		private double extruderSwitchRetraction_mm;
		private double extrusionAmount_mm;
		private double extrusionAmountAtPreviousRetraction_mm;
		private double extrusionPerMm;
		private StreamWriter gcodeFileStream;
		private bool isRetracted;
		private double minimumExtrusionBeforeRetraction_mm;
		private ConfigConstants.OUTPUT_TYPE outputType;
		private double retractionAmount_mm;
		private int retractionSpeed;
		private List<IntPoint> retractionWipePath = new Polygon();
		private double retractionZHop_mm;
		private string toolChangeCode;
		private double[] totalFilament_mm = new double[ConfigConstants.MAX_EXTRUDERS];
		private double totalPrintTime;
		private double unretractExtrusionExtra_mm;
		private bool wipeAfterRetraction;
		private int zPos_um;

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
			SetOutputType(ConfigConstants.OUTPUT_TYPE.REPRAP);
			gcodeFileStream = new StreamWriter(Console.OpenStandardOutput());
		}

		public void Close()
		{
			gcodeFileStream.Close();
		}

		public void Finalize(int maxObjectHeight, int moveSpeed, string endCode)
		{
			WriteFanCommand(0);
			WriteRetraction();
			setZ(maxObjectHeight + 5000);
			WriteMove(GetPositionXY(), moveSpeed, 0);
			WriteCode(endCode);
			WriteComment("filament used = {0:0.0}".FormatWith(GetTotalFilamentUsed(0) + GetTotalFilamentUsed(1)));
			WriteComment("filament used extruder 1 (mm) = {0:0.0}".FormatWith(GetTotalFilamentUsed(0)));
			WriteComment("filament used extruder 2 (mm) = {0:0.0}".FormatWith(GetTotalFilamentUsed(1)));
			WriteComment("total print time (s) = {0:0}".FormatWith(GetTotalPrintTime()));

			LogOutput.Log("Print time: {0}\n".FormatWith((int)(GetTotalPrintTime())));
			LogOutput.Log("Filament: {0}\n".FormatWith((int)(GetTotalFilamentUsed(0))));
			LogOutput.Log("Filament2: {0}\n".FormatWith((int)(GetTotalFilamentUsed(1))));

			if (GetOutputType() == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
			{
				string numberString;
				numberString = "{0}".FormatWith((int)(GetTotalPrintTime()));
				//replaceTagInStart("<__TIME__>", numberString);
				numberString = "{0}".FormatWith((int)(GetTotalFilamentUsed(0)));
				//replaceTagInStart("<FILAMENT>", numberString);
				numberString = "{0}".FormatWith((int)(GetTotalFilamentUsed(1)));
				//replaceTagInStart("<FILAMEN2>", numberString);
			}
		}

		public int GetExtruderIndex()
		{
			return extruderIndex;
		}

		public long GetFileSize()
		{
			return gcodeFileStream.BaseStream.Length;
		}

		public ConfigConstants.OUTPUT_TYPE GetOutputType()
		{
			return this.outputType;
		}

		public IntPoint GetPositionXY()
		{
			return new IntPoint(currentPosition_um.x, currentPosition_um.y);
		}

		public int GetPositionZ()
		{
			return currentPosition_um.z;
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

		public void ResetExtrusionValue(double extraExtrudeAmount_mm = 0)
		{
			if (extrusionAmount_mm != 0.0)
			{
				gcodeFileStream.Write("G92 {0}0\n".FormatWith(extruderCharacter[extruderIndex]));
				totalFilament_mm[extruderIndex] += extrusionAmount_mm;
				extrusionAmountAtPreviousRetraction_mm -= extrusionAmount_mm;
				extrusionAmount_mm = extraExtrudeAmount_mm;
			}
		}

		public void SetExtruderOffset(int extruderIndex, IntPoint extruderOffset_um, int z_offset_um)
		{
			this.extruderOffset_um[extruderIndex] = new Point3(extruderOffset_um.X, extruderOffset_um.Y, z_offset_um);
		}

		public void SetExtrusion(int layerThickness, int filamentDiameter, double extrusionMultiplier)
		{
			//double feedRateRatio = 1 + (Math.PI / 4 - 1) * layerThickness / extrusionWidth;
			//extrusionMultiplier *= feedRateRatio;
			double filamentArea = Math.PI * ((double)(filamentDiameter) / 1000.0 / 2.0) * ((double)(filamentDiameter) / 1000.0 / 2.0);
			if (outputType == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)//UltiGCode uses volume extrusion as E value, and thus does not need the filamentArea in the mix.
			{
				extrusionPerMm = (double)(layerThickness) / 1000.0;
			}
			else
			{
				extrusionPerMm = (double)(layerThickness) / 1000.0 / filamentArea * extrusionMultiplier;
			}
		}

		public void SetFilename(string filename)
		{
			filename = filename.Replace("\"", "");
			gcodeFileStream = new StreamWriter(filename);
		}

		public void SetOutputType(ConfigConstants.OUTPUT_TYPE outputType)
		{
			this.outputType = outputType;
			if (outputType == ConfigConstants.OUTPUT_TYPE.MACH3)
			{
				for (int n = 0; n < ConfigConstants.MAX_EXTRUDERS; n++)
				{
					extruderCharacter[n] = (char)('A' + n);
				}
			}
			else
			{
				for (int n = 0; n < ConfigConstants.MAX_EXTRUDERS; n++)
				{
					extruderCharacter[n] = 'E';
				}
			}
		}

		public void SetRetractionSettings(double retractionAmount, int retractionSpeed, double extruderSwitchRetraction, double minimumExtrusionBeforeRetraction_mm, double retractionZHop_mm, bool wipeAfterRetraction, double unretractExtrusionExtra_mm)
		{
			this.unretractExtrusionExtra_mm = unretractExtrusionExtra_mm;
			this.wipeAfterRetraction = wipeAfterRetraction;
			this.retractionAmount_mm = retractionAmount;
			this.retractionSpeed = retractionSpeed;
			this.extruderSwitchRetraction_mm = extruderSwitchRetraction;
			this.minimumExtrusionBeforeRetraction_mm = minimumExtrusionBeforeRetraction_mm;
			this.retractionZHop_mm = retractionZHop_mm;
		}

		public void SetToolChangeCode(string toolChangeCode)
		{
			this.toolChangeCode = toolChangeCode;
		}

		public void setZ(int z)
		{
			this.zPos_um = z;
		}

		public void SwitchExtruder(int newExtruder)
		{
			if (extruderIndex == newExtruder)
			{
				return;
			}

			if (outputType == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
			{
				gcodeFileStream.Write("G10 S1\n");
			}
			else
			{
				gcodeFileStream.Write("G1 F{0} {1}{2:0.####}\n", retractionSpeed * 60, extruderCharacter[extruderIndex], extrusionAmount_mm - extruderSwitchRetraction_mm);
				currentSpeed = retractionSpeed;
			}

			ResetExtrusionValue();
			extruderIndex = newExtruder;
			if (outputType == ConfigConstants.OUTPUT_TYPE.MACH3)
			{
				ResetExtrusionValue();
			}

			isRetracted = true;
			extrusionAmount_mm = extruderSwitchRetraction_mm;
			gcodeFileStream.Write("T{0}\n".FormatWith(extruderIndex));

			if (toolChangeCode != null && toolChangeCode != "")
			{
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

		public void WriteDelay(double timeAmount)
		{
			gcodeFileStream.Write("G4 P{0}\n".FormatWith((int)(timeAmount * 1000)));
			totalPrintTime += timeAmount;
		}

		public void WriteFanCommand(int speed)
		{
			if (currentFanSpeed == speed)
			{
				return;
			}

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

		public void WriteMove(IntPoint movePosition_um, double speed, int lineWidth_um)
		{
			StringBuilder lineToWrite = new StringBuilder();
			if (outputType == ConfigConstants.OUTPUT_TYPE.BFB)
			{
				movePosition_um = WriteMoveBFBPartial(movePosition_um, speed, lineWidth_um, lineToWrite);
			}
			else
			{
				//Normal E handling.
				if (lineWidth_um != 0)
				{
					IntPoint diff = movePosition_um - GetPositionXY();
					if (isRetracted)
					{
						if (retractionZHop_mm > 0)
						{
							double zWritePosition = (double)(currentPosition_um.z - extruderOffset_um[extruderIndex].z) / 1000;
							lineToWrite.Append("G1 Z{0:0.###}\n".FormatWith(zWritePosition));
						}

						if (extrusionAmount_mm > 10000.0)
						{
							//According to https://github.com/Ultimaker/CuraEngine/issues/14 having more then 21m of extrusion causes inaccuracies. So reset it every 10m, just to be sure.
							ResetExtrusionValue(retractionAmount_mm);
						}

						if (outputType == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
						{
							lineToWrite.Append("G11\n");
						}
						else
						{
							lineToWrite.Append("G1 F{0} {1}{2:0.#####}\n".FormatWith(retractionSpeed * 60, extruderCharacter[extruderIndex], extrusionAmount_mm));

							currentSpeed = retractionSpeed;
							estimateCalculator.plan(new TimeEstimateCalculator.Position(
								currentPosition_um.x / 1000.0,
								currentPosition_um.y / 1000.0,
								currentPosition_um.z / 1000.0,
								extrusionAmount_mm),
								currentSpeed);
						}

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

				double xWritePosition = (double)(movePosition_um.X - extruderOffset_um[extruderIndex].x) / 1000.0;
				double yWritePosition = (double)(movePosition_um.Y - extruderOffset_um[extruderIndex].y) / 1000.0;
				lineToWrite.Append(" X{0:0.###} Y{1:0.###}".FormatWith(xWritePosition, yWritePosition));

				if (zPos_um != currentPosition_um.z)
				{
					double zWritePosition = (double)(zPos_um - extruderOffset_um[extruderIndex].z) / 1000.0;
					lineToWrite.Append(" Z{0:0.###}".FormatWith(zWritePosition));
				}

				if (lineWidth_um != 0)
				{
					lineToWrite.Append(" {0}{1:0.#####}".FormatWith(extruderCharacter[extruderIndex], extrusionAmount_mm));
				}

				lineToWrite.Append("\n");
			}

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

			currentPosition_um = new Point3(movePosition_um.X, movePosition_um.Y, zPos_um);
			estimateCalculator.plan(new TimeEstimateCalculator.Position(currentPosition_um.x / 1000.0, currentPosition_um.y / 1000.0, currentPosition_um.z / 1000.0, extrusionAmount_mm), speed);
		}

		public void WriteRetraction()
		{
			double initialSpeed = currentSpeed;
			if (outputType == ConfigConstants.OUTPUT_TYPE.BFB)//BitsFromBytes does automatic retraction.
			{
				return;
			}

			if (retractionAmount_mm > 0
				&& !isRetracted
				&& extrusionAmountAtPreviousRetraction_mm + minimumExtrusionBeforeRetraction_mm < extrusionAmount_mm)
			{
				if (outputType == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
				{
					gcodeFileStream.Write("G10\n");
				}
				else
				{
					gcodeFileStream.Write("G1 F{0} {1}{2:0.#####}\n".FormatWith(retractionSpeed * 60, extruderCharacter[extruderIndex], extrusionAmount_mm - retractionAmount_mm));
					currentSpeed = retractionSpeed;
					estimateCalculator.plan(new TimeEstimateCalculator.Position((double)(currentPosition_um.x) / 1000.0, (currentPosition_um.y) / 1000.0, (double)(currentPosition_um.z) / 1000.0, extrusionAmount_mm - retractionAmount_mm), currentSpeed);
				}

				AddRetractionWipeIfRequired(initialSpeed);

				if (retractionZHop_mm > 0)
				{
					double zWritePosition = (double)(currentPosition_um.z - extruderOffset_um[extruderIndex].z) / 1000 + retractionZHop_mm;
					gcodeFileStream.Write("G1 Z{0:0.###}\n".FormatWith(zWritePosition));
				}

				// Make sure after a retraction that we will extrude the extra amount on unretraction that the settings want.
				extrusionAmount_mm += unretractExtrusionExtra_mm;

				extrusionAmountAtPreviousRetraction_mm = extrusionAmount_mm;
				isRetracted = true;
			}
		}

		internal static int Round(double value)
		{
			return value < 0 ? (int)(value - 0.5) : (int)(value + 0.5);
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
					gcodeFileStream.Write("X{0:0.###} Y{1:0.###}\n".FormatWith((p.X - extruderOffset_um[extruderIndex].x) / 1000.0, (p.Y - extruderOffset_um[extruderIndex].y) / 1000.0));
					estimateCalculator.plan(new TimeEstimateCalculator.Position(p.X / 1000.0, p.Y / 1000.0, currentPosition_um.z / 1000.0, 0), currentSpeed);
				}
				retractionWipePath.Clear();
			}
		}

		private IntPoint WriteMoveBFBPartial(IntPoint movePosition_um, double speed, int lineWidth_um, StringBuilder lineToWrite)
		{
			//For Bits From Bytes machines, we need to handle this completely differently. As they do not use E values, they use RPM values
			double fspeed = speed * 60;
			double rpm = (extrusionPerMm * (double)(lineWidth_um) / 1000.0) * speed * 60;

			//All BFB machines have 4mm per RPM extrusion.
			const double mm_per_rpm = 4.0;
			rpm /= mm_per_rpm;
			if (rpm > 0)
			{
				if (isRetracted)
				{
					if (currentSpeed != (int)(rpm * 10))
					{
						//lineToWrite.Append("; %f e-per-mm %d mm-width %d mm/s\n", extrusionPerMM, lineWidth, speed);
						lineToWrite.Append("M108 S{0:0.0}\n".FormatWith(rpm * 10));
						currentSpeed = (int)(rpm * 10);
					}
					lineToWrite.Append("M101\n");
					isRetracted = false;
				}
				// Fix the speed by the actual RPM we are asking, because of rounding errors we cannot get all RPM values, but we have a lot more resolution in the feedrate value.
				// (Trick copied from KISSlicer, thanks Jonathan)
				fspeed *= (rpm / (Round(rpm * 100) / 100));

				//Increase the extrusion amount to calculate the amount of filament used.
				IntPoint diff = movePosition_um - GetPositionXY();
				extrusionAmount_mm += extrusionPerMm * lineWidth_um / 1000.0 * diff.LengthMm();
			}
			else
			{
				//If we are not extruding, check if we still need to disable the extruder. This causes a retraction due to auto-retraction.
				if (!isRetracted)
				{
					lineToWrite.Append("M103\n");
					isRetracted = true;
				}
			}
			double xWritePosition = (double)(movePosition_um.X - extruderOffset_um[extruderIndex].x) / 1000.0;
			double yWritePosition = (double)(movePosition_um.Y - extruderOffset_um[extruderIndex].y) / 1000.0;
			double zWritePosition = (double)(zPos_um - extruderOffset_um[extruderIndex].z) / 1000.0;
			// These values exist in microns (integer) so there is an absolute limit to precision of 1/1000th.
			lineToWrite.Append("G1 X{0:0.###} Y{1:0.###} Z{2:0.###} F{3:0.#}\n".FormatWith(xWritePosition, yWritePosition, zWritePosition, fspeed));
			return movePosition_um;
		}
	}
}