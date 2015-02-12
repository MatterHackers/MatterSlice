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
using System.Collections.Generic;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class GCodeExport
    {
        StreamWriter gcodeFileStream;
        double extrusionAmount_mm;
        double extrusionPerMm;
        double retractionAmount_mm;
        double retractionZHop_mm;
        double extruderSwitchRetraction_mm;
        double minimumExtrusionBeforeRetraction_mm;
        double extrusionAmountAtPreviousRetraction_mm;
        Point3 currentPosition_um;
        Point3[] extruderOffset_um = new Point3[ConfigConstants.MAX_EXTRUDERS];
        char[] extruderCharacter = new char[ConfigConstants.MAX_EXTRUDERS];
        int currentSpeed;
        int retractionSpeed;
        int zPos_um;
        bool isRetracted;
        int extruderIndex;
        int currentFanSpeed;
        ConfigConstants.OUTPUT_TYPE outputType;

        double[] totalFilament_mm = new double[ConfigConstants.MAX_EXTRUDERS];
        double totalPrintTime;
        TimeEstimateCalculator estimateCalculator = new TimeEstimateCalculator();

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

        public void setExtruderOffset(int extruderIndex, IntPoint extruderOffset_um, int z_offset_um)
        {
            this.extruderOffset_um[extruderIndex] = new Point3(extruderOffset_um.X, extruderOffset_um.Y, z_offset_um);
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

        public ConfigConstants.OUTPUT_TYPE GetOutputType()
        {
            return this.outputType;
        }

        public void setFilename(string filename)
        {
            filename = filename.Replace("\"", "");
            gcodeFileStream = new StreamWriter(filename);
        }

        public bool isOpened()
        {
            return gcodeFileStream != null;
        }

		public void setExtrusion(int layerThickness, int filamentDiameter, double extrusionMultiplier)
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

        public void setRetractionSettings(double retractionAmount, int retractionSpeed, double extruderSwitchRetraction, double minimumExtrusionBeforeRetraction_mm, double retractionZHop_mm)
        {
            this.retractionAmount_mm = retractionAmount;
            this.retractionSpeed = retractionSpeed;
            this.extruderSwitchRetraction_mm = extruderSwitchRetraction;
            this.minimumExtrusionBeforeRetraction_mm = minimumExtrusionBeforeRetraction_mm;
            this.retractionZHop_mm = retractionZHop_mm;
        }

        public void setZ(int z)
        {
            this.zPos_um = z;
        }

        public IntPoint getPositionXY()
        {
            return new IntPoint(currentPosition_um.x, currentPosition_um.y);
        }

        public int getPositionZ()
        {
            return currentPosition_um.z;
        }

        public int getExtruderIndex()
        {
            return extruderIndex;
        }

        public double getTotalFilamentUsed(int extruderIndexToGet)
        {
            if (extruderIndexToGet == extruderIndex)
            {
                return totalFilament_mm[extruderIndexToGet] + extrusionAmount_mm;
            }

            return totalFilament_mm[extruderIndexToGet];
        }

        public double getTotalPrintTime()
        {
            return totalPrintTime;
        }

        public void updateTotalPrintTime()
        {
            totalPrintTime += estimateCalculator.calculate();
            estimateCalculator.reset();
        }

        public void writeComment(string comment)
        {
            gcodeFileStream.Write("; {0}\n".FormatWith(comment));
        }

        public void writeLine(string line)
        {
            gcodeFileStream.Write("{0}\n".FormatWith(line));
        }

        public void resetExtrusionValue()
        {
            if (extrusionAmount_mm != 0.0 && outputType != ConfigConstants.OUTPUT_TYPE.MAKERBOT)
            {
                gcodeFileStream.Write("G92 {0}0\n".FormatWith(extruderCharacter[extruderIndex]));
                totalFilament_mm[extruderIndex] += extrusionAmount_mm;
                extrusionAmountAtPreviousRetraction_mm -= extrusionAmount_mm;
                extrusionAmount_mm = 0.0;
            }
        }

        public void writeDelay(double timeAmount)
        {
            gcodeFileStream.Write("G4 P{0}\n".FormatWith((int)(timeAmount * 1000)));
            totalPrintTime += timeAmount;
        }

        internal static int Round(double value)
        {
            return value < 0 ? (int)(value - 0.5) : (int)(value + 0.5);
        }

        public void writeMove(IntPoint movePosition_um, int speed, int lineWidth_um)
        {
            StringBuilder lineToWrite = new StringBuilder();
            if (outputType == ConfigConstants.OUTPUT_TYPE.BFB)
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
                    IntPoint diff = movePosition_um - getPositionXY();
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
            }
            else
            {
                //Normal E handling.
                if (lineWidth_um != 0)
                {
                    IntPoint diff = movePosition_um - getPositionXY();
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
                            resetExtrusionValue();
                        }
                        else
                        {
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
#if DEBUG
                // this is just to debug when we right exactly the same line twice.
                if (lineAsString == lastLineWriten)
                {
                }
                lastLineWriten = lineAsString;
#endif
            }
            currentPosition_um = new Point3(movePosition_um.X, movePosition_um.Y, zPos_um);
            estimateCalculator.plan(new TimeEstimateCalculator.Position(currentPosition_um.x / 1000.0, currentPosition_um.y / 1000.0, currentPosition_um.z / 1000.0, extrusionAmount_mm), speed);
        }

#if DEBUG
        string lastLineWriten = "";
#endif

        public void writeRetraction()
        {
            if (outputType == ConfigConstants.OUTPUT_TYPE.BFB)//BitsFromBytes does automatic retraction.
            {
                return;
            }

            if (retractionAmount_mm > 0 && !isRetracted && extrusionAmountAtPreviousRetraction_mm + minimumExtrusionBeforeRetraction_mm < extrusionAmount_mm)
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
                if (retractionZHop_mm > 0)
                {
                    double zWritePosition = (double)(currentPosition_um.z - extruderOffset_um[extruderIndex].z) / 1000 + retractionZHop_mm;
                    gcodeFileStream.Write("G1 Z{0:0.###}\n".FormatWith(zWritePosition));
                }
                extrusionAmountAtPreviousRetraction_mm = extrusionAmount_mm;
                isRetracted = true;
            }
        }

        public void switchExtruder(int newExtruder)
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

            resetExtrusionValue();
            extruderIndex = newExtruder;
            if (outputType == ConfigConstants.OUTPUT_TYPE.MACH3)
            {
                resetExtrusionValue();
            }

            isRetracted = true;
            extrusionAmount_mm = extruderSwitchRetraction_mm;
            if (outputType == ConfigConstants.OUTPUT_TYPE.MAKERBOT)
            {
                gcodeFileStream.Write("M135 T{0}\n".FormatWith(extruderIndex));
            }
            else
            {
                gcodeFileStream.Write("T{0}\n".FormatWith(extruderIndex));
            }
        }

        public void writeCode(string str)
        {
            gcodeFileStream.Write("{0}\n".FormatWith(str));
        }

        public void writeFanCommand(int speed)
        {
            if (currentFanSpeed == speed)
            {
                return;
            }

            if (speed > 0)
            {
                if (outputType == ConfigConstants.OUTPUT_TYPE.MAKERBOT)
                {
                    gcodeFileStream.Write("M126 T0 ; value = {0}\n".FormatWith(speed * 255 / 100));
                }
                else
                {
                    gcodeFileStream.Write("M106 S{0}\n".FormatWith(speed * 255 / 100));
                }
            }
            else
            {
                if (outputType == ConfigConstants.OUTPUT_TYPE.MAKERBOT)
                {
                    gcodeFileStream.Write("M127 T0\n");
                }
                else
                {
                    gcodeFileStream.Write("M107\n");
                }
            }
            currentFanSpeed = speed;
        }

        public long getFileSize()
        {
            return gcodeFileStream.BaseStream.Length;
        }

        public void tellFileSize()
        {
            double fsize = gcodeFileStream.BaseStream.Length;
            if (fsize > 1024 * 1024)
            {
                fsize /= 1024.0 * 1024.0;
                LogOutput.log("Wrote {0:0.0} MB.\n".FormatWith(fsize));
            }
            if (fsize > 1024)
            {
                fsize /= 1024.0;
                LogOutput.log("Wrote {0:0.0} kilobytes.\n".FormatWith(fsize));
            }
        }


        public void finalize(int maxObjectHeight, int moveSpeed, string endCode)
        {
            writeFanCommand(0);
            writeRetraction();
            setZ(maxObjectHeight + 5000);
            writeMove(getPositionXY(), moveSpeed, 0);
            writeCode(endCode);
            writeComment("filament used = {0:0.0}".FormatWith(getTotalFilamentUsed(0) + getTotalFilamentUsed(1)));
            writeComment("filament used extruder 1 (mm) = {0:0.0}".FormatWith(getTotalFilamentUsed(0)));
            writeComment("filament used extruder 2 (mm) = {0:0.0}".FormatWith(getTotalFilamentUsed(1)));
            writeComment("total print time (s) = {0:0}".FormatWith(getTotalPrintTime()));
            
            LogOutput.log("Print time: {0}\n".FormatWith((int)(getTotalPrintTime())));
            LogOutput.log("Filament: {0}\n".FormatWith((int)(getTotalFilamentUsed(0))));
            LogOutput.log("Filament2: {0}\n".FormatWith((int)(getTotalFilamentUsed(1))));

            if (GetOutputType() == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
            {
                string numberString;
                numberString = "{0}".FormatWith((int)(getTotalPrintTime()));
                //replaceTagInStart("<__TIME__>", numberString);
                numberString = "{0}".FormatWith((int)(getTotalFilamentUsed(0)));
                //replaceTagInStart("<FILAMENT>", numberString);
                numberString = "{0}".FormatWith((int)(getTotalFilamentUsed(1)));
                //replaceTagInStart("<FILAMEN2>", numberString);
            }
        }
    }

    //The GCodePathConfig is the configuration for moves/extrusion actions. This defines at which width the line is printed and at which speed.
    public class GCodePathConfig
    {
        public int speed;
        public int lineWidth;
        public string name;
        public bool spiralize;
		public bool closedLoop = true;

        public GCodePathConfig() { }
        public GCodePathConfig(int speed, int lineWidth, string name)
        {
            this.speed = speed;
            this.lineWidth = lineWidth;
            this.name = name;
        }

        public void setData(int speed, int lineWidth, string name, bool closedLoop = true)
        {
			this.closedLoop = closedLoop;
            this.speed = speed;
            this.lineWidth = lineWidth;
            this.name = name;
        }
    }

    public class GCodePath
    {
        public GCodePathConfig config;
        public bool retract;
        public bool Retract { get { return retract; } set { retract = value; } }
        public int extruderIndex;
        public List<IntPoint> points = new List<IntPoint>();
        public bool done;//Path is finished, no more moves should be added, and a new path should be started instead of any appending done to this one.
    }

    //The GCodePlanner class stores multiple moves that are planned.
	// It facilitates the avoidCrossingPerimeters to keep the head inside the print.
    // It also keeps track of the print time estimate for this planning so speed adjustments can be made for the minimum-layer-time.
    public class GCodePlanner
    {
        GCodeExport gcode = new GCodeExport();

        IntPoint lastPosition;
        List<GCodePath> paths = new List<GCodePath>();
        AvoidCrossingPerimeters outerPerimetersToAvoidCrossing;

        GCodePathConfig travelConfig = new GCodePathConfig();
        int extrudeSpeedFactor;
        int travelSpeedFactor;
        int currentExtruderIndex;
        int retractionMinimumDistance;
        bool forceRetraction;
        bool alwaysRetract;
        double extraTime;
        double totalPrintTime;

        public GCodePlanner(GCodeExport gcode, int travelSpeed, int retractionMinimumDistance)
        {
            this.gcode = gcode;
            travelConfig = new GCodePathConfig(travelSpeed, 0, "travel");

            lastPosition = gcode.getPositionXY();
            outerPerimetersToAvoidCrossing = null;
            extrudeSpeedFactor = 100;
            travelSpeedFactor = 100;
            extraTime = 0.0;
            totalPrintTime = 0.0;
            forceRetraction = false;
            alwaysRetract = false;
            currentExtruderIndex = gcode.getExtruderIndex();
            this.retractionMinimumDistance = retractionMinimumDistance;
        }

        GCodePath getLatestPathWithConfig(GCodePathConfig config)
        {
            if (paths.Count > 0
                && paths[paths.Count - 1].config == config
                && !paths[paths.Count - 1].done)
            {
                return paths[paths.Count - 1];
            }

            paths.Add(new GCodePath());
            GCodePath ret = paths[paths.Count - 1];
            ret.Retract = false;
            ret.config = config;
            ret.extruderIndex = currentExtruderIndex;
            ret.done = false;
            return ret;
        }

        void forceNewPathStart()
        {
            if (paths.Count > 0)
            {
                paths[paths.Count - 1].done = true;
            }
        }

        public bool setExtruder(int extruder)
        {
            if (extruder == currentExtruderIndex)
            {
                return false;
            }

            currentExtruderIndex = extruder;
            return true;
        }

        public int getExtruder()
        {
            return currentExtruderIndex;
        }

        public void SetOuterPerimetersToAvoidCrossing(Polygons polygons)
        {
            if (polygons != null)
            {
                outerPerimetersToAvoidCrossing = new AvoidCrossingPerimeters(polygons);
            }
            else
            {
                outerPerimetersToAvoidCrossing = null;
            }
        }

        public void setAlwaysRetract(bool alwaysRetract)
        {
            this.alwaysRetract = alwaysRetract;
        }

        public void forceRetract()
        {
            forceRetraction = true;
        }

        public void setExtrudeSpeedFactor(int speedFactor)
        {
            if (speedFactor < 1) speedFactor = 1;
            this.extrudeSpeedFactor = speedFactor;
        }

        public int getExtrudeSpeedFactor()
        {
            return this.extrudeSpeedFactor;
        }

        public void setTravelSpeedFactor(int speedFactor)
        {
            if (speedFactor < 1) speedFactor = 1;
            this.travelSpeedFactor = speedFactor;
        }

        public int getTravelSpeedFactor()
        {
            return this.travelSpeedFactor;
        }

        public void writeTravel(IntPoint positionToMoveTo)
        {
            GCodePath path = getLatestPathWithConfig(travelConfig);

            if (forceRetraction)
            {
                path.Retract = true;
                forceRetraction = false;
            }
            else if (outerPerimetersToAvoidCrossing != null)
            {
                List<IntPoint> pointList = new List<IntPoint>();
                if (outerPerimetersToAvoidCrossing.CreatePathInsideBoundary(lastPosition, positionToMoveTo, pointList))
                {
                    long lineLength = 0;
                    // we can stay inside so move within the boundary
                    for (int pointIndex = 0; pointIndex < pointList.Count; pointIndex++)
                    {
                        path.points.Add(pointList[pointIndex]);
                        if(pointIndex > 0)
                        {
                            lineLength += (pointList[pointIndex] - pointList[pointIndex-1]).Length();
                        }
                    }

                    // If the internal move is very long (20 mm), do a retration anyway
                    if (lineLength > retractionMinimumDistance)
                    {
                        path.Retract = true;
                    }
                }
                else
                {
                    if ((lastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance))
                    {
                        // We are moving relatively far and are going to cross a boundary so do a retraction.
                        path.Retract = true;
                    }
                }
            }
            else if (alwaysRetract)
            {
                if ((lastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance))
                {
                    path.Retract = true;
                }
            }
            
            path.points.Add(positionToMoveTo);
            lastPosition = positionToMoveTo;
        }

        public void writeExtrusionMove(IntPoint destination, GCodePathConfig config)
        {
            getLatestPathWithConfig(config).points.Add(destination);
            lastPosition = destination;
        }

        public void MoveInsideTheOuterPerimeter(int distance)
        {
            if (outerPerimetersToAvoidCrossing == null || outerPerimetersToAvoidCrossing.PointIsInsideBoundary(lastPosition))
            {
                return;
            }

            IntPoint p = lastPosition;
            if (outerPerimetersToAvoidCrossing.MovePointInsideBoundary(ref p, distance))
            {
                //Move inside again, so we move out of tight 90deg corners
                outerPerimetersToAvoidCrossing.MovePointInsideBoundary(ref p, distance);
                if (outerPerimetersToAvoidCrossing.PointIsInsideBoundary(p))
                {
                    writeTravel(p);
                    //Make sure the that any retraction happens after this move, not before it by starting a new move path.
                    forceNewPathStart();
                }
            }
        }

        public void writePolygon(Polygon polygon, int startIndex, GCodePathConfig config)
        {
            IntPoint currentPosition = polygon[startIndex];
            writeTravel(currentPosition);
			if (config.closedLoop)
			{
				for (int positionIndex = 1; positionIndex < polygon.Count; positionIndex++)
				{
					IntPoint destination = polygon[(startIndex + positionIndex) % polygon.Count];
					writeExtrusionMove(destination, config);
					currentPosition = destination;
				}

				// We need to actually close the polygon so go back to the first point
				if (polygon.Count > 2)
				{
					writeExtrusionMove(polygon[startIndex], config);
				}
			}
			else // we are not closed 
			{
				if (startIndex == 0)
				{
					for (int positionIndex = 1; positionIndex < polygon.Count; positionIndex++)
					{
						IntPoint destination = polygon[positionIndex];
						writeExtrusionMove(destination, config);
						currentPosition = destination;
					}
				}
				else
				{
					for (int positionIndex = polygon.Count - 1; positionIndex >= 1; positionIndex-- )
					{
						IntPoint destination = polygon[(startIndex + positionIndex) % polygon.Count];
						writeExtrusionMove(destination, config);
						currentPosition = destination;
					}
				}
			}
        }

        public void writePolygonsByOptimizer(Polygons polygons, GCodePathConfig config)
        {
            PathOrderOptimizer orderOptimizer = new PathOrderOptimizer(lastPosition);
            orderOptimizer.AddPolygons(polygons);

            orderOptimizer.Optimize(config);

            for (int i = 0; i < orderOptimizer.bestPolygonOrderIndex.Count; i++)
            {
                int polygonIndex = orderOptimizer.bestPolygonOrderIndex[i];
                writePolygon(polygons[polygonIndex], orderOptimizer.startIndexInPolygon[polygonIndex], config);
            }
        }

        public void forceMinimumLayerTime(double minTime, int minimumPrintingSpeed)
        {
            IntPoint lastPosition = gcode.getPositionXY();
            double travelTime = 0.0;
            double extrudeTime = 0.0;
            for (int n = 0; n < paths.Count; n++)
            {
                GCodePath path = paths[n];
                for (int pointIndex = 0; pointIndex < path.points.Count; pointIndex++)
                {
                    IntPoint currentPosition = path.points[pointIndex];
                    double thisTime = (lastPosition - currentPosition).LengthMm() / (double)(path.config.speed);
                    if (path.config.lineWidth != 0)
                    {
                        extrudeTime += thisTime;
                    }
                    else
                    {
                        travelTime += thisTime;
                    }

                    lastPosition = currentPosition;
                }
            }

            double totalTime = extrudeTime + travelTime;
            if (totalTime < minTime && extrudeTime > 0.0)
            {
                double minExtrudeTime = minTime - travelTime;
                if (minExtrudeTime < 1)
                {
                    minExtrudeTime = 1;
                }

                double factor = extrudeTime / minExtrudeTime;
                for (int n = 0; n < paths.Count; n++)
                {
                    GCodePath path = paths[n];
                    if (path.config.lineWidth == 0)
                    {
                        continue;
                    }

                    int speed = (int)(path.config.speed * factor);
                    if (speed < minimumPrintingSpeed)
                    {
                        factor = (double)(minimumPrintingSpeed) / (double)(path.config.speed);
                    }
                }

                //Only slow down with the minimum time if that will be slower then a factor already set. First layer slowdown also sets the speed factor.
                if (factor * 100 < getExtrudeSpeedFactor())
                {
                    setExtrudeSpeedFactor((int)(factor * 100));
                }
                else
                {
                    factor = getExtrudeSpeedFactor() / 100.0;
                }

                if (minTime - (extrudeTime / factor) - travelTime > 0.1)
                {
                    //TODO: Use up this extra time (circle around the print?)
                    this.extraTime = minTime - (extrudeTime / factor) - travelTime;
                }
                this.totalPrintTime = (extrudeTime / factor) + travelTime;
            }
            else
            {
                this.totalPrintTime = totalTime;
            }
        }

        public void writeGCode(bool liftHeadIfNeeded, int layerThickness)
        {
            GCodePathConfig lastConfig = null;
            int extruderIndex = gcode.getExtruderIndex();

            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                GCodePath path = paths[pathIndex];
                if (extruderIndex != path.extruderIndex)
                {
                    extruderIndex = path.extruderIndex;
                    gcode.switchExtruder(extruderIndex);
                }
                else if (path.Retract)
                {
                    gcode.writeRetraction();
                }
                if (path.config != travelConfig && lastConfig != path.config)
                {
                    gcode.writeComment("TYPE:{0}".FormatWith(path.config.name));
                    lastConfig = path.config;
                }
                
                int speed = path.config.speed;
                if (path.config.lineWidth != 0)
                {
                    // Only apply the extrudeSpeedFactor to extrusion moves
                    speed = speed * extrudeSpeedFactor / 100;
                }
                else
                {
                    speed = speed * travelSpeedFactor / 100;
                }

                if (path.points.Count == 1 
                    && path.config != travelConfig 
                    && (gcode.getPositionXY() - path.points[0]).ShorterThen(path.config.lineWidth * 2))
                {
                    //Check for lots of small moves and combine them into one large line
                    IntPoint nextPosition = path.points[0];
                    int i = pathIndex + 1;
                    while (i < paths.Count && paths[i].points.Count == 1 && (nextPosition - paths[i].points[0]).ShorterThen(path.config.lineWidth * 2))
                    {
                        nextPosition = paths[i].points[0];
                        i++;
                    }
                    if (paths[i - 1].config == travelConfig)
                    {
                        i--;
                    }

                    if (i > pathIndex + 2)
                    {
                        nextPosition = gcode.getPositionXY();
                        for (int x = pathIndex; x < i - 1; x += 2)
                        {
                            long oldLen = (nextPosition - paths[x].points[0]).vSize();
                            IntPoint newPoint = (paths[x].points[0] + paths[x + 1].points[0]) / 2;
                            long newLen = (gcode.getPositionXY() - newPoint).vSize();
                            if (newLen > 0)
                            {
                                gcode.writeMove(newPoint, speed, (int)(path.config.lineWidth * oldLen / newLen));
                            }

                            nextPosition = paths[x + 1].points[0];
                        }
                        gcode.writeMove(paths[i - 1].points[0], speed, path.config.lineWidth);
                        pathIndex = i - 1;
                        continue;
                    }
                }

                bool spiralize = path.config.spiralize;
                if (spiralize)
                {
                    //Check if we are the last spiralize path in the list, if not, do not spiralize.
                    for (int m = pathIndex + 1; m < paths.Count; m++)
                    {
                        if (paths[m].config.spiralize)
                        {
                            spiralize = false;
                        }
                    }
                }
                if (spiralize)
                {
                    //If we need to spiralize then raise the head slowly by 1 layer as this path progresses.
                    double totalLength = 0;
                    int z = gcode.getPositionZ();
                    IntPoint currentPosition = gcode.getPositionXY();
                    for (int pointIndex = 0; pointIndex < path.points.Count; pointIndex++)
                    {
                        IntPoint nextPosition = path.points[pointIndex];
                        totalLength += (currentPosition - nextPosition).LengthMm();
                        currentPosition = nextPosition;
                    }

                    double length = 0.0;
                    currentPosition = gcode.getPositionXY();
                    for (int i = 0; i < path.points.Count; i++)
                    {
                        IntPoint nextPosition = path.points[i];
                        length += (currentPosition - nextPosition).LengthMm();
                        currentPosition = nextPosition;
                        gcode.setZ((int)(z + layerThickness * length / totalLength + .5));
                        gcode.writeMove(path.points[i], speed, path.config.lineWidth);
                    }
                }
                else
                {
                    for (int pointIndex = 0; pointIndex < path.points.Count; pointIndex++)
                    {
                        gcode.writeMove(path.points[pointIndex], speed, path.config.lineWidth);
                    }
                }
            }

            gcode.updateTotalPrintTime();
            if (liftHeadIfNeeded && extraTime > 0.0)
            {
                gcode.writeComment("Small layer, adding delay of {0}".FormatWith(extraTime));
                gcode.writeRetraction();
                gcode.setZ(gcode.getPositionZ() + 3000);
                gcode.writeMove(gcode.getPositionXY(), travelConfig.speed, 0);
                gcode.writeMove(gcode.getPositionXY() - new IntPoint(-20000, 0), travelConfig.speed, 0);
                gcode.writeDelay(extraTime);
            }
        }
    }
}