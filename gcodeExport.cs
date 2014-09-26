/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
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
using System.IO;
using System.Collections.Generic;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class GCodeExport
    {
        StreamWriter f;
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
        int currentSpeed, retractionSpeed;
        int zPos_um;
        bool isRetracted;
        int extruderIndex;
        int currentFanSpeed;
        ConfigConstants.OUTPUT_TYPE outputType;

        double[] totalFilament = new double[ConfigConstants.MAX_EXTRUDERS];
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
                totalFilament[e] = 0.0;
            }

            currentSpeed = 0;
            retractionSpeed = 45;
            isRetracted = true;
            SetOutputType(ConfigConstants.OUTPUT_TYPE.REPRAP);
            f = new StreamWriter(Console.OpenStandardOutput());
        }

        public void Close()
        {
            f.Close();
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
            f = new StreamWriter(filename);
        }

        public bool isOpened()
        {
            return f != null;
        }

        public void setExtrusion(int layerThickness, int filamentDiameter, double extrusionMultiplier)
        {
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
                return totalFilament[extruderIndexToGet] + extrusionAmount_mm;
            }

            return totalFilament[extruderIndexToGet];
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
            f.Write("; {0}\n".FormatWith(comment));
        }

        public void writeLine(string line)
        {
            f.Write("{0}\n".FormatWith(line));
        }

        public void resetExtrusionValue()
        {
            if (extrusionAmount_mm != 0.0 && outputType != ConfigConstants.OUTPUT_TYPE.MAKERBOT)
            {
                f.Write("G92 {0}0\n".FormatWith(extruderCharacter[extruderIndex]));
                totalFilament[extruderIndex] += extrusionAmount_mm;
                extrusionAmountAtPreviousRetraction_mm -= extrusionAmount_mm;
                extrusionAmount_mm = 0.0;
            }
        }

        public void writeDelay(double timeAmount)
        {
            f.Write("G4 P{0}\n".FormatWith((int)(timeAmount * 1000)));
            totalPrintTime += timeAmount;
        }

        internal static int Round(double value)
        {
            return value < 0 ? (int)(value - 0.5) : (int)(value + 0.5);
        }

        public void writeMove(IntPoint movePosition_um, int speed, int lineWidth_um)
        {
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
                            //f.Write("; %f e-per-mm %d mm-width %d mm/s\n", extrusionPerMM, lineWidth, speed);
                            f.Write("M108 S{0:0.0}\n".FormatWith(rpm * 10));
                            currentSpeed = (int)(rpm * 10);
                        }
                        f.Write("M101\n");
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
                        f.Write("M103\n");
                        isRetracted = true;
                    }
                }
                double xWritePosition = (double)(movePosition_um.X - extruderOffset_um[extruderIndex].x) / 1000;
                double yWritePosition = (double)(movePosition_um.Y - extruderOffset_um[extruderIndex].y) / 1000;
                double zWritePosition = (double)(zPos_um - extruderOffset_um[extruderIndex].z) / 1000;
                f.Write("G1 X{0:0.00} Y{1:0.00} Z{2:0.00} F{3:0.0}\n".FormatWith(xWritePosition, yWritePosition, zWritePosition, fspeed));
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
                            f.Write("G1 Z{0:0.00}\n".FormatWith(zWritePosition));
                        }

                        if (outputType == ConfigConstants.OUTPUT_TYPE.ULTIGCODE)
                        {
                            f.Write("G11\n");
                        }
                        else
                        {
                            f.Write("G1 F{0} {1}{2:0.00000}\n".FormatWith(retractionSpeed * 60, extruderCharacter[extruderIndex], extrusionAmount_mm));
                      
                            currentSpeed = retractionSpeed;
                            estimateCalculator.plan(new TimeEstimateCalculator.Position(
                                currentPosition_um.x / 1000.0,
                                currentPosition_um.y / 1000.0,
                                currentPosition_um.z / 1000.0, 
                                extrusionAmount_mm), 
                                currentSpeed);
                        }

                        if (extrusionAmount_mm > 10000.0)
                        {
                            //According to https://github.com/Ultimaker/CuraEngine/issues/14 having more then 21m of extrusion causes inaccuracies. So reset it every 10m, just to be sure.
                            resetExtrusionValue();
                        }
                        isRetracted = false;
                    }
                    extrusionAmount_mm += extrusionPerMm * lineWidth_um / 1000.0 * diff.LengthMm();
                    f.Write("G1");
                }
                else
                {
                    f.Write("G0");
                }

                if (currentSpeed != speed)
                {
                    f.Write(" F{0}".FormatWith(speed * 60));
                    currentSpeed = speed;
                }
                double xWritePosition = (double)(movePosition_um.X - extruderOffset_um[extruderIndex].x) / 1000.0;
                double yWritePosition = (double)(movePosition_um.Y - extruderOffset_um[extruderIndex].y) / 1000.0;
                f.Write(" X{0:0.00} Y{1:0.00}".FormatWith(xWritePosition, yWritePosition));
                if (zPos_um != currentPosition_um.z)
                {
                    double zWritePosition = (double)(zPos_um - extruderOffset_um[extruderIndex].z) / 1000.0;
                    f.Write(" Z{0:0.00}".FormatWith(zWritePosition));
                }
                if (lineWidth_um != 0)
                {
                    f.Write(" {0}{1:0.00000}".FormatWith(extruderCharacter[extruderIndex], extrusionAmount_mm));
                }
                f.Write("\n");
            }

            currentPosition_um = new Point3(movePosition_um.X, movePosition_um.Y, zPos_um);
            estimateCalculator.plan(new TimeEstimateCalculator.Position(currentPosition_um.x / 1000.0, currentPosition_um.y / 1000.0, currentPosition_um.z / 1000.0, extrusionAmount_mm), speed);
        }

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
                    f.Write("G10\n");
                }
                else
                {
                    f.Write("G1 F{0} {1}{2:0.00000}\n".FormatWith(retractionSpeed * 60, extruderCharacter[extruderIndex], extrusionAmount_mm - retractionAmount_mm));
                    currentSpeed = retractionSpeed;
                    estimateCalculator.plan(new TimeEstimateCalculator.Position((double)(currentPosition_um.x) / 1000.0, (currentPosition_um.y) / 1000.0, (double)(currentPosition_um.z) / 1000.0, extrusionAmount_mm - retractionAmount_mm), currentSpeed);
                }
                if (retractionZHop_mm > 0)
                {
                    double zWritePosition = (double)(currentPosition_um.z - extruderOffset_um[extruderIndex].z) / 1000 + retractionZHop_mm;
                    f.Write("G1 Z{0:0.00}\n".FormatWith(zWritePosition));
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
                f.Write("G10 S1\n");
            }
            else
            {
                f.Write("G1 F{0} {1}{2:0.0000}\n", retractionSpeed * 60, extruderCharacter[extruderIndex], extrusionAmount_mm - extruderSwitchRetraction_mm);
                currentSpeed = retractionSpeed;
            }

            resetExtrusionValue();
            extruderIndex = newExtruder;
            if (outputType == ConfigConstants.OUTPUT_TYPE.MACH3)
            {
                resetExtrusionValue();
            }

            isRetracted = true;
            if (outputType == ConfigConstants.OUTPUT_TYPE.MAKERBOT)
            {
                f.Write("M135 T{0}\n".FormatWith(extruderIndex));
            }
            else
            {
                f.Write("T{0}\n".FormatWith(extruderIndex));
            }
        }

        public void writeCode(string str)
        {
            f.Write("{0}\n".FormatWith(str));
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
                    f.Write("M126 T0 ; value = {0}\n".FormatWith(speed * 255 / 100));
                }
                else
                {
                    f.Write("M106 S{0}\n".FormatWith(speed * 255 / 100));
                }
            }
            else
            {
                if (outputType == ConfigConstants.OUTPUT_TYPE.MAKERBOT)
                {
                    f.Write("M127 T0\n");
                }
                else
                {
                    f.Write("M107\n");
                }
            }
            currentFanSpeed = speed;
        }

        public long getFileSize()
        {
            return f.BaseStream.Length;
        }

        public void tellFileSize()
        {
            double fsize = f.BaseStream.Length;
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

        public GCodePathConfig() { }
        public GCodePathConfig(int speed, int lineWidth, string name)
        {
            this.speed = speed;
            this.lineWidth = lineWidth;
            this.name = name;
        }

        public void setData(int speed, int lineWidth, string name)
        {
            this.speed = speed;
            this.lineWidth = lineWidth;
            this.name = name;
        }
    }

    public class GCodePath
    {
        public GCodePathConfig config;
        public bool retract;
        public int extruderIndex;
        public List<IntPoint> points = new List<IntPoint>();
        public bool done;//Path is finished, no more moves should be added, and a new path should be started instead of any appending done to this one.
    }

    //The GCodePlanner class stores multiple moves that are planned.
    // It facilitates the combing to keep the head inside the print.
    // It also keeps track of the print time estimate for this planning so speed adjustments can be made for the minimum-layer-time.
    public class GCodePlanner
    {
        GCodeExport gcode = new GCodeExport();

        IntPoint lastPosition;
        List<GCodePath> paths = new List<GCodePath>();
        AvoidCrossingPerimeters avaidCrossingPerimeters;

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
            avaidCrossingPerimeters = null;
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
            ret.retract = false;
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

        public void setCombBoundary(Polygons polygons)
        {
            if (polygons != null)
            {
                avaidCrossingPerimeters = new AvoidCrossingPerimeters(polygons);
            }
            else
            {
                avaidCrossingPerimeters = null;
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
                if (!(lastPosition - positionToMoveTo).ShorterThen(retractionMinimumDistance))
                {
                    path.retract = true;
                }
                forceRetraction = false;
            }
            else if (avaidCrossingPerimeters != null)
            {
                List<IntPoint> pointList = new List<IntPoint>();
                if (avaidCrossingPerimeters.CreatePathInsideBoundary(lastPosition, positionToMoveTo, pointList))
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
                    if(lineLength > (20 * 1000))
                    {
                        path.retract = true;
                    }
                }
                else
                {
                    if (!(lastPosition - positionToMoveTo).ShorterThen(retractionMinimumDistance))
                    {
                        // We are moving relatively far and are going to cross a boundary so do a retraction.
                        path.retract = true;
                    }
                }
            }
            else if (alwaysRetract)
            {
                if (!(lastPosition - positionToMoveTo).ShorterThen(retractionMinimumDistance))
                {
                    path.retract = true;
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

        public void moveInsideCombBoundary(int distance)
        {
            if (avaidCrossingPerimeters == null || avaidCrossingPerimeters.PointIsInsideBoundary(lastPosition))
            {
                return;
            }

            IntPoint p = lastPosition;
            if (avaidCrossingPerimeters.MovePointInsideBoundary(ref p, distance))
            {
                //Move inside again, so we move out of tight 90deg corners
                avaidCrossingPerimeters.MovePointInsideBoundary(ref p, distance);
                if (avaidCrossingPerimeters.PointIsInsideBoundary(p))
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
            for (int i = 1; i < polygon.Count; i++)
            {
                IntPoint destination = polygon[(startIndex + i) % polygon.Count];
                writeExtrusionMove(destination, config);
                currentPosition = destination;
            }

            if (polygon.Count > 2)
            {
                writeExtrusionMove(polygon[startIndex], config);
            }
        }

        public void writePolygonsByOptimizer(Polygons polygons, GCodePathConfig config)
        {
            PathOrderOptimizer orderOptimizer = new PathOrderOptimizer(lastPosition);
            for (int i = 0; i < polygons.Count; i++)
            {
                orderOptimizer.addPolygon(polygons[i]);
            }

            orderOptimizer.optimize();

            for (int i = 0; i < orderOptimizer.polyOrder.Count; i++)
            {
                int polygonIndex = orderOptimizer.polyOrder[i];
                writePolygon(polygons[polygonIndex], orderOptimizer.polyStart[polygonIndex], config);
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
                else if (path.retract)
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
                        gcode.setZ((int)(z + layerThickness * length / totalLength));
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