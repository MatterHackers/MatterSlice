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
using System.Collections.Generic;
using System.IO;
using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public class GCodeExport
    {
        public StreamWriter gcodeFile;
        public double extrusionAmount;
        public double extrusionPerMM;
        public double retractionAmount;
        public double extruderSwitchRetraction;
        public double minimalExtrusionBeforeRetraction;
        public double extrusionAmountAtPreviousRetraction;
        public Point3 currentPosition;
        public IntPoint[] extruderOffset = new IntPoint[16];
        public double currentSpeed, retractionSpeed;
        public int zPos;
        public bool isRetracted;
        public int extruderNr;
        public int currentFanSpeed;
        public ConfigSettings.GCodeFlavor flavor;

        public double totalFilament;

        public double totalPrintTime;

        public GCodeExport()
        {
            currentPosition = new Point3();
            extrusionAmount = 0;
            extrusionPerMM = 0;
            retractionAmount = 4.5;
            minimalExtrusionBeforeRetraction = 0.0;
            extrusionAmountAtPreviousRetraction = -10000;
            extruderSwitchRetraction = 14.5;
            extruderNr = 0;
            currentFanSpeed = -1;

            totalPrintTime = 0.0;
            totalFilament = 0.0;

            currentSpeed = 0;
            retractionSpeed = 45;
            isRetracted = true;
        }

        public void setExtruderOffset(int id, IntPoint p)
        {
            extruderOffset[id] = p;
        }

        public void setFlavor(ConfigSettings.GCodeFlavor flavor)
        {
            this.flavor = flavor;
        }

        public ConfigSettings.GCodeFlavor getFlavor()
        {
            return this.flavor;
        }

        public void setFilename(string filename)
        {
            gcodeFile = new StreamWriter(filename);
            gcodeFile.AutoFlush = true;
        }

        public bool isValid()
        {
            return gcodeFile != null;
        }

        public void setExtrusion(int layerThickness, int filamentDiameter, int flow)
        {
            double filamentArea = Math.PI * (double)filamentDiameter / 1000.0 / 2.0 * (double)filamentDiameter / 1000.0 / 2.0;
            if (flavor == ConfigSettings.GCodeFlavor.GCODE_FLAVOR_ULTIGCODE)
            {
                //UltiGCode uses volume extrusion as E value, and thus does not need the filamentArea in the mix.
                extrusionPerMM = (double)(layerThickness) / 1000.0;
            }
            else
            {
                extrusionPerMM = (double)(layerThickness) / 1000.0 / filamentArea * (double)(flow) / 100.0;
            }
        }

        public void setRetractionSettings(int retractionAmount, int retractionSpeed, int extruderSwitchRetraction, int minimalExtrusionBeforeRetraction)
        {
            this.retractionAmount = (double)retractionAmount / 1000.0;
            this.retractionSpeed = retractionSpeed;
            this.extruderSwitchRetraction = (double)extruderSwitchRetraction / 1000.0;
            this.minimalExtrusionBeforeRetraction = (double)minimalExtrusionBeforeRetraction / 1000.0;
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

        public double getTotalFilamentUsed()
        {
            return totalFilament + extrusionAmount;
        }

        public double getTotalPrintTime()
        {
            return totalPrintTime;
        }

        public void addComment(string comment)
        {
            gcodeFile.Write(string.Format(";{0}", comment));
        }

        public void addLine(string line)
        {
            throw new NotImplementedException();
#if false
        va_list args;
        va_start(args, line);
        vfprintf(f, line, args);
        fprintf(f, "\n");
        va_end(args);
#endif
        }

        public void resetExtrusionValue()
        {
            if (extrusionAmount != 0.0)
            {
                gcodeFile.WriteLine("G92 E0");
                totalFilament += extrusionAmount;
                extrusionAmountAtPreviousRetraction -= extrusionAmount;
                extrusionAmount = 0.0;
            }
        }

        public void addDelay(double timeAmount)
        {
            gcodeFile.WriteLine(string.Format("G4 P{0}", (int)(timeAmount * 1000)));
        }

        public void addMove(IntPoint p, double speed, int lineWidth)
        {
            if (lineWidth != 0)
            {
                IntPoint diff = p - getPositionXY();
                if (isRetracted)
                {
                    if (flavor == ConfigSettings.GCodeFlavor.GCODE_FLAVOR_ULTIGCODE)
                    {
                        gcodeFile.WriteLine("G11\n");
                    }
                    else
                    {
                        gcodeFile.WriteLine(string.Format("G1 F{0} E{1:.5}\n", retractionSpeed * 60, extrusionAmount));
                        currentSpeed = retractionSpeed;
                    }
                    if (extrusionAmount > 1000.0)
                    {
                        // reset this so that it will not over flow a float in some firmware.
                        resetExtrusionValue();
                    }

                    isRetracted = false;
                }
                extrusionAmount += extrusionPerMM * (double)lineWidth / 1000.0 * diff.LengthInMm();
                gcodeFile.Write("G1");
            }
            else
            {
                gcodeFile.Write("G0");
            }

            if (currentSpeed != speed)
            {
                gcodeFile.Write(string.Format(" F{0}", speed * 60));
                currentSpeed = speed;
            }
            gcodeFile.Write(string.Format(" X{0:.2} Y{1:.2}", (p.X - extruderOffset[extruderNr].X) / 1000, (p.Y - extruderOffset[extruderNr].Y) / 1000));
            if (zPos != currentPosition.z)
            {
                gcodeFile.Write(string.Format(" Z{0:.2}", zPos / 1000));
            }
            if (lineWidth != 0)
            {
                gcodeFile.Write(string.Format(" E{0:.5}", extrusionAmount));
            }
            gcodeFile.Write("\n");

            currentPosition = new Point3(p.X, p.Y, zPos);
        }

        public void addRetraction()
        {
            if (retractionAmount > 0 && !isRetracted && extrusionAmountAtPreviousRetraction + minimalExtrusionBeforeRetraction < extrusionAmount)
            {
                if (flavor == ConfigSettings.GCodeFlavor.GCODE_FLAVOR_ULTIGCODE)
                {
                    gcodeFile.WriteLine("G10");
                }
                else
                {
                    gcodeFile.WriteLine(string.Format("G1 F{0} E{1:.5}", retractionSpeed * 60, extrusionAmount - retractionAmount));
                    currentSpeed = retractionSpeed;
                }
                extrusionAmountAtPreviousRetraction = extrusionAmount;
                isRetracted = true;
            }
        }

        public void switchExtruder(int newExtruder)
        {
            if (extruderNr == newExtruder)
                return;

            extruderNr = newExtruder;

            switch(flavor)
            {
                case ConfigSettings.GCodeFlavor.GCODE_FLAVOR_ULTIGCODE:
                    gcodeFile.WriteLine("G10 S1\n");
                    break;

                case ConfigSettings.GCodeFlavor.GCODE_FLAVOR_REPRAP:
                    gcodeFile.WriteLine(string.Format("G1 F{0} E{1:.4}", retractionSpeed * 60, extrusionAmount - extruderSwitchRetraction));
                    currentSpeed = retractionSpeed;
                    break;

                default:
                    throw new NotImplementedException();
            }
            isRetracted = true;
            gcodeFile.WriteLine(string.Format("T{0}", extruderNr));
        }

        public void addCode(string str)
        {
            gcodeFile.WriteLine(str);
        }

        public void addFanCommand(int speed)
        {
            if (currentFanSpeed == speed)
            {
                return;
            }

            if (speed > 0)
            {
                gcodeFile.WriteLine(string.Format("M106 S{0}", speed * 255 / 100));
            }
            else
            {
                gcodeFile.WriteLine("M107");
            }

            currentFanSpeed = speed;
        }

        public long getFileSize()
        {
            return gcodeFile.BaseStream.Length;
        }

        public void tellFileSize()
        {
            double fsize = getFileSize();
            if (fsize > 1024 * 1024)
            {
                fsize /= 1024.0 * 1024.0;
                Console.WriteLine(string.Format("Wrote {0:.1} MB.\n", fsize));
            }
            if (fsize > 1024)
            {
                fsize /= 1024.0;
                Utilities.Output(string.Format("Wrote {0:.1} kilobytes.\n", fsize));
            }
        }
    }

    public class GCodePathConfig
    {
        public int speed;
        public int lineWidth;
        public string name;

        public GCodePathConfig(int speed, int lineWidth, string name)
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
    }

    public class GCodePlanner
    {
        GCodeExport gcode;

        IntPoint lastPosition;
        List<GCodePath> paths = new List<GCodePath>();
        Comb comb;

        GCodePathConfig moveConfig;
        double extrudeSpeedFactor;
        int currentExtruder;
        int retractionMinimalDistance;
        bool forceRetraction;
        bool alwaysRetract;
        double extraTime;
        double totalPrintTime;

        public GCodePath getLatestPathWithConfig(GCodePathConfig config)
        {
            if (paths.Count > 0 && paths[paths.Count - 1].config == config)
            {
                return paths[paths.Count - 1];
            }
            paths.Add(new GCodePath());
            GCodePath ret = paths[paths.Count - 1];
            ret.retract = false;
            ret.config = config;
            ret.extruder = currentExtruder;
            return ret;
        }

        public void forceNewPathStart()
        {
            paths.Add(new GCodePath());
            GCodePath ret = paths[paths.Count - 1];
            ret.retract = false;
            ret.config = moveConfig;
            ret.extruder = currentExtruder;
        }

        public GCodePlanner(GCodeExport gcode, int moveSpeed, int retractionMinimalDistance)
        {
            this.gcode = gcode;
            this.moveConfig = new GCodePathConfig(moveSpeed, 0, "move");

            lastPosition = gcode.getPositionXY();
            comb = null;
            extrudeSpeedFactor = 100;
            extraTime = 0.0;
            totalPrintTime = 0.0;
            forceRetraction = false;
            alwaysRetract = false;
            currentExtruder = gcode.getExtruderNr();
            this.retractionMinimalDistance = retractionMinimalDistance;
        }

        public void setExtruder(int extruder)
        {
            currentExtruder = extruder;
        }

        public void setCombBoundary(Polygons polygons)
        {
            if (polygons != null)
            {
                comb = new Comb(polygons);
            }
            else
            {
                comb = null;
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

        public void setExtrudeSpeedFactor(double speedFactor)
        {
            if (speedFactor < 1) speedFactor = 1;
            this.extrudeSpeedFactor = speedFactor;
        }

        public double getExtrudeSpeedFactor()
        {
            return this.extrudeSpeedFactor;
        }

        public void addMove(IntPoint p)
        {
            GCodePath path = getLatestPathWithConfig(moveConfig);
            if (forceRetraction)
            {
                if (!(lastPosition - p).IsShorterThen(1500))
                {
                    path.retract = true;
                }
                forceRetraction = false;
            }
            else if (comb != null)
            {
                Polygon pointList = new Polygon();
                if (comb.calc(ref lastPosition, ref p, pointList))
                {
                    for (int n = 0; n < pointList.Count; n++)
                    {
                        path.points.Add(pointList[n]);
                    }
                }
                else
                {
                    if (!(lastPosition - p).IsShorterThen(retractionMinimalDistance))
                    {
                        path.retract = true;
                    }
                }
            }
            else if (alwaysRetract)
            {
                if (!(lastPosition - p).IsShorterThen(retractionMinimalDistance))
                {
                    path.retract = true;
                }
            }
            path.points.Add(p);
            lastPosition = p;
        }

        public void addExtrusionMove(IntPoint p, GCodePathConfig config)
        {
            getLatestPathWithConfig(config).points.Add(p);
            lastPosition = p;
        }

        public void moveInsideCombBoundary()
        {
            if (comb == null || comb.checkInside(lastPosition))
            {
                return;
            }

            IntPoint p = lastPosition;
            if (comb.moveInside(ref p))
            {
                addMove(p);
                //Make sure the that any retraction happens after this move, not before it by starting a new move path.
                forceNewPathStart();
            }
        }

        public void addPolygon(Polygon polygon, int startIdx, GCodePathConfig config)
        {
            IntPoint p0 = polygon[startIdx];
            addMove(p0);
            for (int i = 1; i < polygon.Count; i++)
            {
                IntPoint p1 = polygon[(startIdx + i) % polygon.Count];
                addExtrusionMove(p1, config);
                p0 = p1;
            }
            if (polygon.Count > 2)
                addExtrusionMove(polygon[startIdx], config);
        }

        public void addPolygonsByOptimizer(Polygons polygons, GCodePathConfig config)
        {
            PathOptimizer orderOptimizer = new PathOptimizer(lastPosition);
            for (int i = 0; i < polygons.Count; i++)
            {
                orderOptimizer.addPolygon(polygons[i]);
            }
            orderOptimizer.optimize();
            for (int i = 0; i < orderOptimizer.polyOrder.Count; i++)
            {
                int nr = orderOptimizer.polyOrder[i];
                addPolygon(polygons[nr], orderOptimizer.polyStart[nr], config);
            }
        }

        public void forceMinimalLayerTime(double minTime, int minimalSpeed)
        {
            IntPoint p0 = gcode.getPositionXY();
            double travelTime = 0.0;
            double extrudeTime = 0.0;
            for (int n = 0; n < paths.Count; n++)
            {
                GCodePath path = paths[n];
                for (int i = 0; i < path.points.Count; i++)
                {
                    double thisTime = (p0 - path.points[i]).LengthInMm() / (double)path.config.speed;
                    if (path.config.lineWidth != 0)
                    {
                        extrudeTime += thisTime;
                    }
                    else
                    {
                        travelTime += thisTime;
                    }

                    p0 = path.points[i];
                }
            }
            double totalTime = extrudeTime + travelTime;
            if (totalTime < minTime)
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

                    double speed = path.config.speed * factor;
                    if (speed < minimalSpeed)
                    {
                        factor = (double)(minimalSpeed) / (double)(path.config.speed);
                    }
                }
                setExtrudeSpeedFactor(factor * 100);

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

        public void writeGCode(bool liftHeadIfNeeded)
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
                    gcode.addRetraction();
                }
                if (path.config != moveConfig && lastConfig != path.config)
                {
                    gcode.addComment(string.Format("TYPE:{0}", path.config.name));
                    lastConfig = path.config;
                }
                double speed = path.config.speed;

                if (path.config.lineWidth != 0)// Only apply the extrudeSpeedFactor to extrusion moves
                {
                    speed = speed * extrudeSpeedFactor / 100;
                }

                if (path.points.Count == 1 && path.config != moveConfig && (gcode.getPositionXY() - path.points[0]).IsShorterThen(path.config.lineWidth * 2))
                {
                    //Check for lots of small moves and combine them into one large line
                    IntPoint p0 = path.points[0];
                    int i = n + 1;
                    while (i < paths.Count && paths[i].points.Count == 1 && (p0 - paths[i].points[0]).IsShorterThen(path.config.lineWidth * 2))
                    {
                        p0 = paths[i].points[0];
                        i++;
                    }
                    if (paths[i - 1].config == moveConfig)
                    {
                        i--;
                    }

                    if (i > n + 2)
                    {
                        p0 = gcode.getPositionXY();
                        for (int x = n; x < i - 1; x += 2)
                        {
                            long oldLen = (p0 - paths[x].points[0]).Length();
                            IntPoint newPoint = (paths[x].points[0] + paths[x + 1].points[0]) / 2;
                            long newLen = (gcode.getPositionXY() - newPoint).Length();
                            if (newLen > 0)
                            {
                                gcode.addMove(newPoint, speed, (int)(path.config.lineWidth * oldLen / newLen));
                            }

                            p0 = paths[x + 1].points[0];
                        }
                        gcode.addMove(paths[i - 1].points[0], speed, path.config.lineWidth);
                        n = i - 1;
                        continue;
                    }
                }
                for (int i = 0; i < path.points.Count; i++)
                {
                    gcode.addMove(path.points[i], speed, path.config.lineWidth);
                }
            }

            gcode.totalPrintTime += this.totalPrintTime;
            if (liftHeadIfNeeded && extraTime > 0.0)
            {
                gcode.totalPrintTime += extraTime;

                gcode.addComment(string.Format("Small layer, adding delay of {0}", extraTime));
                gcode.addRetraction();
                gcode.setZ(gcode.getPositionZ() + 3000);
                gcode.addMove(gcode.getPositionXY(), moveConfig.speed, 0);
                gcode.addMove(gcode.getPositionXY() - new IntPoint(-20000, 0), moveConfig.speed, 0);
                gcode.addDelay(extraTime);
            }
        }
    }
}