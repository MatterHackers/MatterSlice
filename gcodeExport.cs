/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

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
using System.IO;
using System.Collections.Generic;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class GCodeExport
    {
        StreamWriter f;
        double extrusionAmount;
        double extrusionPerMM;
        double retractionAmount;
        double retractionZHop;
        double extruderSwitchRetraction;
        double minimumExtrusionBeforeRetraction;
        double extrusionAmountAtPreviousRetraction;
        Point3 currentPosition;
        IntPoint[] extruderOffset = new IntPoint[ConfigConstants.MAX_EXTRUDERS];
        char[] extruderCharacter = new char[ConfigConstants.MAX_EXTRUDERS];
        int currentSpeed, retractionSpeed;
        int zPos;
        bool isRetracted;
        int extruderNr;
        int currentFanSpeed;
        ConfigConstants.GCODE_OUTPUT_TYPE outputType;

        double[] totalFilament = new double[ConfigConstants.MAX_EXTRUDERS];
        double totalPrintTime;
        TimeEstimateCalculator estimateCalculator = new TimeEstimateCalculator();

        public GCodeExport()
        {
            extrusionAmount = 0;
            extrusionPerMM = 0;
            retractionAmount = 4.5;
            minimumExtrusionBeforeRetraction = 0.0;
            extrusionAmountAtPreviousRetraction = -10000;
            extruderSwitchRetraction = 14.5;
            extruderNr = 0;
            currentFanSpeed = -1;

            totalPrintTime = 0.0;
            for (int e = 0; e < ConfigConstants.MAX_EXTRUDERS; e++)
            {
                totalFilament[e] = 0.0;
            }

            currentSpeed = 0;
            retractionSpeed = 45;
            isRetracted = true;
            SetOutputType(ConfigConstants.GCODE_OUTPUT_TYPE.REPRAP);
            f = new StreamWriter(Console.OpenStandardOutput());
        }

        public void Close()
        {
            f.Close();
        }

        public void replaceTagInStart(string tag, string replaceValue)
        {
            throw new NotImplementedException();
#if false
    long oldPos = ftello64(f);
    
    char[] buffer = new char[1024];
    fseeko64(f, 0, SEEK_SET);
    fread(buffer, 1024, 1, f);
    
    char* c = strstr(buffer, tag);
    memset(c, ' ', strlen(tag));
    if (c) memcpy(c, replaceValue, strlen(replaceValue));
    
    fseeko64(f, 0, SEEK_SET);
    fwrite(buffer, 1024, 1, f);
    
    fseeko64(f, oldPos, SEEK_SET);
#endif
        }

        public void setExtruderOffset(int id, IntPoint p)
        {
            extruderOffset[id] = p;
        }

        public void SetOutputType(ConfigConstants.GCODE_OUTPUT_TYPE outputType)
        {
            this.outputType = outputType;
            if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.MACH3)
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

        public ConfigConstants.GCODE_OUTPUT_TYPE GetOutputType()
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

        public void setExtrusion(int layerThickness, int filamentDiameter, int flowPercent)
        {
            double filamentArea = Math.PI * ((double)(filamentDiameter) / 1000.0 / 2.0) * ((double)(filamentDiameter) / 1000.0 / 2.0);
            if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.ULTIGCODE)//UltiGCode uses volume extrusion as E value, and thus does not need the filamentArea in the mix.
            {
                extrusionPerMM = (double)(layerThickness) / 1000.0;
            }
            else
            {
                extrusionPerMM = (double)(layerThickness) / 1000.0 / filamentArea * (double)(flowPercent) / 100.0;
            }
        }

        public void setRetractionSettings(int retractionAmount, int retractionSpeed, int extruderSwitchRetraction, int minimumExtrusionBeforeRetraction, double retractionZHop)
        {
            this.retractionAmount = (double)(retractionAmount) / 1000.0;
            this.retractionSpeed = retractionSpeed;
            this.extruderSwitchRetraction = (double)(extruderSwitchRetraction) / 1000.0;
            this.minimumExtrusionBeforeRetraction = (double)(minimumExtrusionBeforeRetraction) / 1000.0;
            this.retractionZHop = retractionZHop;
        }

        public void setZ(int z)
        {
            this.zPos = z;
        }

        public IntPoint getPositionXY()
        {
            return new IntPoint(currentPosition.x, currentPosition.y);
        }

        public int getPositionZ()
        {
            return currentPosition.z;
        }

        public int getExtruderNr()
        {
            return extruderNr;
        }

        public double getTotalFilamentUsed(int e)
        {
            if (e == extruderNr)
            {
                return totalFilament[e] + extrusionAmount;
            }

            return totalFilament[e];
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
            f.Write(";{0}\n".FormatWith(comment));
        }

        public void writeLine(string line)
        {
            f.Write(";{0}\n".FormatWith(line));
        }

        public void resetExtrusionValue()
        {
            if (extrusionAmount != 0.0 && outputType != ConfigConstants.GCODE_OUTPUT_TYPE.MAKERBOT)
            {
                f.Write("G92 {0}0\n".FormatWith(extruderCharacter[extruderNr]));
                totalFilament[extruderNr] += extrusionAmount;
                extrusionAmountAtPreviousRetraction -= extrusionAmount;
                extrusionAmount = 0.0;
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

        public void writeMove(IntPoint p, int speed, int lineWidth)
        {
            if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.BFB)
            {
                //For Bits From Bytes machines, we need to handle this completely differently. As they do not use E values, they use RPM values
                double fspeed = speed * 60;
                double rpm = (extrusionPerMM * (double)(lineWidth) / 1000.0) * speed * 60;
                
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
                f.Write("G1 X{0:0.00} Y{1:0.00} Z{0:0.00} F{0:0.0}\n".FormatWith((double)(p.X - extruderOffset[extruderNr].X) / 1000, (double)(p.Y - extruderOffset[extruderNr].Y) / 1000, (double)(zPos) / 1000, fspeed));
            }
            else
            {
                //Normal E handling.
                if (lineWidth != 0)
                {
                    IntPoint diff = p - getPositionXY();
                    if (isRetracted)
                    {
                        if (retractionZHop > 0)
                        {
                            f.Write("G1 Z{0:0.00}\n".FormatWith(currentPosition.z - retractionZHop));
                        }

                        if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.ULTIGCODE)
                        {
                            f.Write("G11\n");
                        }
                        else
                        {
                            f.Write("G1 F{0} {1}{2:0.00000}\n".FormatWith(retractionSpeed * 60, extruderCharacter[extruderNr], extrusionAmount));
                      
                            currentSpeed = retractionSpeed;
                            estimateCalculator.plan(new TimeEstimateCalculator.Position((double)(p.X) / 1000.0, (p.Y) / 1000.0, (double)(zPos) / 1000.0, extrusionAmount), currentSpeed);
                        }

                        if (extrusionAmount > 10000.0)
                        {
                            //According to https://github.com/Ultimaker/CuraEngine/issues/14 having more then 21m of extrusion causes inaccuracies. So reset it every 10m, just to be sure.
                            resetExtrusionValue();
                        }
                        isRetracted = false;
                    }
                    extrusionAmount += extrusionPerMM * (double)(lineWidth) / 1000.0 * diff.vSizeMM();
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
                f.Write(" X{0:0.00} Y{1:0.00}".FormatWith((double)(p.X - extruderOffset[extruderNr].X) / 1000, (double)(p.Y - extruderOffset[extruderNr].Y) / 1000));
                if (zPos != currentPosition.z)
                {
                    f.Write(" Z{0:0.00}".FormatWith((double)(zPos) / 1000));
                }
                if (lineWidth != 0)
                {
                    f.Write(" {0}{1:0.00000}".FormatWith(extruderCharacter[extruderNr], extrusionAmount));
                }
                f.Write("\n");
            }

            currentPosition = new Point3(p.X, p.Y, zPos);
            estimateCalculator.plan(new TimeEstimateCalculator.Position((double)(currentPosition.x) / 1000.0, (currentPosition.y) / 1000.0, (double)(currentPosition.z) / 1000.0, extrusionAmount), speed);
        }

        public void writeRetraction()
        {
            if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.BFB)//BitsFromBytes does automatic retraction.
            {
                return;
            }

            if (retractionAmount > 0 && !isRetracted && extrusionAmountAtPreviousRetraction + minimumExtrusionBeforeRetraction < extrusionAmount)
            {
                if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.ULTIGCODE)
                {
                    f.Write("G10\n");
                }
                else
                {
                    f.Write("G1 F{0} {1}{2:0.00000}\n".FormatWith(retractionSpeed * 60, extruderCharacter[extruderNr], extrusionAmount - retractionAmount));
                    currentSpeed = retractionSpeed;
                    estimateCalculator.plan(new TimeEstimateCalculator.Position((double)(currentPosition.x) / 1000.0, (currentPosition.y) / 1000.0, (double)(currentPosition.z) / 1000.0, extrusionAmount - retractionAmount), currentSpeed);
                }
                if (retractionZHop > 0)
                {
                    f.Write("G1 Z{0:0.00}\n".FormatWith(currentPosition.z + retractionZHop));
                }
                extrusionAmountAtPreviousRetraction = extrusionAmount;
                isRetracted = true;
            }
        }

        public void switchExtruder(int newExtruder)
        {
            if (extruderNr == newExtruder)
            {
                return;
            }

            if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.ULTIGCODE)
            {
                f.Write("G10 S1\n");
            }
            else
            {
                f.Write("G1 F{0} {1}{2:0.0000}\n", retractionSpeed * 60, extruderCharacter[extruderNr], extrusionAmount - extruderSwitchRetraction);
                currentSpeed = retractionSpeed;
            }

            resetExtrusionValue();
            extruderNr = newExtruder;
            if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.MACH3)
            {
                resetExtrusionValue();
            }

            isRetracted = true;
            if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.MAKERBOT)
            {
                f.Write("M135 T{0}\n".FormatWith(extruderNr));
            }
            else
            {
                f.Write("T{0}\n".FormatWith(extruderNr));
            }
        }

        public void writeCode(string str)
        {
            f.Write("{0}\n".FormatWith(str));
        }

        public void writeFanCommand(int speed)
        {
            if (currentFanSpeed == speed)
                return;
            if (speed > 0)
            {
                if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.MAKERBOT)
                    f.Write("M126 T0 ; value = {0}\n".FormatWith(speed * 255 / 100));
                else
                    f.Write("M106 S{0}\n".FormatWith(speed * 255 / 100));
            }
            else
            {
                if (outputType == ConfigConstants.GCODE_OUTPUT_TYPE.MAKERBOT)
                    f.Write("M127 T0\n");
                else
                    f.Write("M107\n");
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
            LogOutput.log("Print time: {0}\n".FormatWith((int)(getTotalPrintTime())));
            LogOutput.log("Filament: {0}\n".FormatWith((int)(getTotalFilamentUsed(0))));
            LogOutput.log("Filament2: {0}\n".FormatWith((int)(getTotalFilamentUsed(1))));

            if (GetOutputType() == ConfigConstants.GCODE_OUTPUT_TYPE.ULTIGCODE)
            {
                string numberString;
                numberString = "{0}".FormatWith((int)(getTotalPrintTime()));
                replaceTagInStart("<__TIME__>", numberString);
                numberString = "{0}".FormatWith((int)(getTotalFilamentUsed(0)));
                replaceTagInStart("<FILAMENT>", numberString);
                numberString = "{0}".FormatWith((int)(getTotalFilamentUsed(1)));
                replaceTagInStart("<FILAMEN2>", numberString);
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
        public int extruder;
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
        Comb comb;

        GCodePathConfig travelConfig = new GCodePathConfig();
        int extrudeSpeedFactor;
        int travelSpeedFactor;
        int currentExtruder;
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
            comb = null;
            extrudeSpeedFactor = 100;
            travelSpeedFactor = 100;
            extraTime = 0.0;
            totalPrintTime = 0.0;
            forceRetraction = false;
            alwaysRetract = false;
            currentExtruder = gcode.getExtruderNr();
            this.retractionMinimumDistance = retractionMinimumDistance;
        }

        GCodePath getLatestPathWithConfig(GCodePathConfig config)
        {
            if (paths.Count > 0 && paths[paths.Count - 1].config == config && !paths[paths.Count - 1].done)
                return paths[paths.Count - 1];
            paths.Add(new GCodePath());
            GCodePath ret = paths[paths.Count - 1];
            ret.retract = false;
            ret.config = config;
            ret.extruder = currentExtruder;
            ret.done = false;
            return ret;
        }

        void forceNewPathStart()
        {
            if (paths.Count > 0)
                paths[paths.Count - 1].done = true;
        }

        public bool setExtruder(int extruder)
        {
            if (extruder == currentExtruder)
                return false;
            currentExtruder = extruder;
            return true;
        }

        public int getExtruder()
        {
            return currentExtruder;
        }

        public void setCombBoundary(Polygons polygons)
        {
            if (polygons != null)
                comb = new Comb(polygons);
            else
                comb = null;
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

        public void writeTravel(IntPoint p)
        {
            GCodePath path = getLatestPathWithConfig(travelConfig);
            if (forceRetraction)
            {
                if (!(lastPosition - p).shorterThen(retractionMinimumDistance))
                {
                    path.retract = true;
                }
                forceRetraction = false;
            }
            else if (comb != null)
            {
                List<IntPoint> pointList = new List<IntPoint>();
                if (comb.calc(lastPosition, p, pointList))
                {
                    for (int n = 0; n < pointList.Count; n++)
                    {
                        path.points.Add(pointList[n]);
                    }
                }
                else
                {
                    if (!(lastPosition - p).shorterThen(retractionMinimumDistance))
                    {
                        path.retract = true;
                    }
                }
            }
            else if (alwaysRetract)
            {
                if (!(lastPosition - p).shorterThen(retractionMinimumDistance))
                {
                    path.retract = true;
                }
            }
            path.points.Add(p);
            lastPosition = p;
        }

        public void writeExtrusionMove(IntPoint p, GCodePathConfig config)
        {
            getLatestPathWithConfig(config).points.Add(p);
            lastPosition = p;
        }

        public void moveInsideCombBoundary(int distance)
        {
            if (comb == null || comb.checkInside(lastPosition)) return;
            IntPoint p = lastPosition;
            if (comb.moveInside(p, distance))
            {
                //Move inside again, so we move out of tight 90deg corners
                comb.moveInside(p, distance);
                if (comb.checkInside(p))
                {
                    writeTravel(p);
                    //Make sure the that any retraction happens after this move, not before it by starting a new move path.
                    forceNewPathStart();
                }
            }
        }

        public void writePolygon(Polygon polygon, int startIdx, GCodePathConfig config)
        {
            IntPoint p0 = polygon[startIdx];
            writeTravel(p0);
            for (int i = 1; i < polygon.Count; i++)
            {
                IntPoint p1 = polygon[(startIdx + i) % polygon.Count];
                writeExtrusionMove(p1, config);
                p0 = p1;
            }
            if (polygon.Count > 2)
                writeExtrusionMove(polygon[startIdx], config);
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
                int nr = orderOptimizer.polyOrder[i];
                writePolygon(polygons[nr], orderOptimizer.polyStart[nr], config);
            }
        }

        public void forceMinimumLayerTime(double minTime, int minimumSpeed)
        {
            IntPoint p0 = gcode.getPositionXY();
            double travelTime = 0.0;
            double extrudeTime = 0.0;
            for (int n = 0; n < paths.Count; n++)
            {
                GCodePath path = paths[n];
                for (int i = 0; i < path.points.Count; i++)
                {
                    double thisTime = (p0 - path.points[i]).vSizeMM() / (double)(path.config.speed);
                    if (path.config.lineWidth != 0)
                        extrudeTime += thisTime;
                    else
                        travelTime += thisTime;
                    p0 = path.points[i];
                }
            }
            double totalTime = extrudeTime + travelTime;
            if (totalTime < minTime && extrudeTime > 0.0)
            {
                double minExtrudeTime = minTime - travelTime;
                if (minExtrudeTime < 1)
                    minExtrudeTime = 1;
                double factor = extrudeTime / minExtrudeTime;
                for (int n = 0; n < paths.Count; n++)
                {
                    GCodePath path = paths[n];
                    if (path.config.lineWidth == 0)
                        continue;
                    int speed = (int)(path.config.speed * factor);
                    if (speed < minimumSpeed)
                        factor = (double)(minimumSpeed) / (double)(path.config.speed);
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
            int extruder = gcode.getExtruderNr();

            for (int n = 0; n < paths.Count; n++)
            {
                GCodePath path = paths[n];
                if (extruder != path.extruder)
                {
                    extruder = path.extruder;
                    gcode.switchExtruder(extruder);
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

                if (path.config.lineWidth != 0)// Only apply the extrudeSpeedFactor to extrusion moves
                    speed = speed * extrudeSpeedFactor / 100;
                else
                    speed = speed * travelSpeedFactor / 100;

                if (path.points.Count == 1 && path.config != travelConfig && (gcode.getPositionXY() - path.points[0]).shorterThen(path.config.lineWidth * 2))
                {
                    //Check for lots of small moves and combine them into one large line
                    IntPoint p0 = path.points[0];
                    int i = n + 1;
                    while (i < paths.Count && paths[i].points.Count == 1 && (p0 - paths[i].points[0]).shorterThen(path.config.lineWidth * 2))
                    {
                        p0 = paths[i].points[0];
                        i++;
                    }
                    if (paths[i - 1].config == travelConfig)
                    {
                        i--;
                    }

                    if (i > n + 2)
                    {
                        p0 = gcode.getPositionXY();
                        for (int x = n; x < i - 1; x += 2)
                        {
                            long oldLen = (p0 - paths[x].points[0]).vSize();
                            IntPoint newPoint = (paths[x].points[0] + paths[x + 1].points[0]) / 2;
                            long newLen = (gcode.getPositionXY() - newPoint).vSize();
                            if (newLen > 0)
                            {
                                gcode.writeMove(newPoint, speed, (int)(path.config.lineWidth * oldLen / newLen));
                            }

                            p0 = paths[x + 1].points[0];
                        }
                        gcode.writeMove(paths[i - 1].points[0], speed, path.config.lineWidth);
                        n = i - 1;
                        continue;
                    }
                }

                bool spiralize = path.config.spiralize;
                if (spiralize)
                {
                    //Check if we are the last spiralize path in the list, if not, do not spiralize.
                    for (int m = n + 1; m < paths.Count; m++)
                    {
                        if (paths[m].config.spiralize)
                            spiralize = false;
                    }
                }
                if (spiralize)
                {
                    //If we need to spiralize then raise the head slowly by 1 layer as this path progresses.
                    double totalLength = 0;
                    int z = gcode.getPositionZ();
                    IntPoint p0 = gcode.getPositionXY();
                    for (int i = 0; i < path.points.Count; i++)
                    {
                        IntPoint p1 = path.points[i];
                        totalLength += (p0 - p1).vSizeMM();
                        p0 = p1;
                    }

                    double length = 0.0;
                    p0 = gcode.getPositionXY();
                    for (int i = 0; i < path.points.Count; i++)
                    {
                        IntPoint p1 = path.points[i];
                        length += (p0 - p1).vSizeMM();
                        p0 = p1;
                        gcode.setZ((int)(z + layerThickness * length / totalLength));
                        gcode.writeMove(path.points[i], speed, path.config.lineWidth);
                    }
                }
                else
                {
                    for (int i = 0; i < path.points.Count; i++)
                    {
                        gcode.writeMove(path.points[i], speed, path.config.lineWidth);
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