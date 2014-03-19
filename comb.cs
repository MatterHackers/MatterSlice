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

    public class Comb
    {
        Polygons bounderyPolygons;

        long[] minX;
        long[] maxX;
        int[] minIdx;
        int[] maxIdx;

        PointMatrix matrix;
        IntPoint startPoint;
        IntPoint endPoint;

        bool preTest(IntPoint startPoint, IntPoint endPoint)
        {
            return collisionTest(startPoint, endPoint);
        }

        bool collisionTest(IntPoint startPoint, IntPoint endPoint)
        {
            IntPoint diff = endPoint - startPoint;

            matrix = new PointMatrix(diff);
            this.startPoint = matrix.apply(startPoint);
            this.endPoint = matrix.apply(endPoint);

            for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
            {
                Polygon boundryPolygon = bounderyPolygons[bounderyIndex];
                if (boundryPolygon.Count < 1)
                {
                    continue;
                }

                IntPoint p0 = matrix.apply(boundryPolygon[boundryPolygon.Count - 1]);
                for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
                {
                    IntPoint p1 = matrix.apply(boundryPolygon[pointIndex]);
                    if ((p0.Y > startPoint.Y && p1.Y < startPoint.Y) || (p1.Y > startPoint.Y && p0.Y < startPoint.Y))
                    {
                        long x = p0.X + (p1.X - p0.X) * (startPoint.Y - p0.Y) / (p1.Y - p0.Y);

                        if (x > startPoint.X && x < endPoint.X)
                        {
                            return true;
                        }
                    }
                    p0 = p1;
                }
            }
            return false;
        }

        void calcMinMax()
        {
            for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
            {
                Polygon boundryPolygon = bounderyPolygons[bounderyIndex];

                minX[bounderyIndex] = long.MaxValue;
                maxX[bounderyIndex] = long.MinValue;
                IntPoint p0 = matrix.apply(boundryPolygon[boundryPolygon.Count - 1]);
                for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
                {
                    IntPoint p1 = matrix.apply(boundryPolygon[pointIndex]);
                    if ((p0.Y > startPoint.Y && p1.Y < startPoint.Y) || (p1.Y > startPoint.Y && p0.Y < startPoint.Y))
                    {
                        long x = p0.X + (p1.X - p0.X) * (startPoint.Y - p0.Y) / (p1.Y - p0.Y);

                        if (x >= startPoint.X && x <= endPoint.X)
                        {
                            if (x < minX[bounderyIndex]) 
                            { 
                                minX[bounderyIndex] = x; 
                                minIdx[bounderyIndex] = pointIndex; 
                            }

                            if (x > maxX[bounderyIndex]) 
                            { 
                                maxX[bounderyIndex] = x; 
                                maxIdx[bounderyIndex] = pointIndex; 
                            }
                        }
                    }
                    p0 = p1;
                }
            }
        }

        int getPolygonIndexAbove(long startingPolygonIndex)
        {
            long min = long.MaxValue;
            int ret = int.MaxValue;
            
            for (int polygonIndex = 0; polygonIndex < bounderyPolygons.Count; polygonIndex++)
            {
                if (minX[polygonIndex] > startingPolygonIndex && minX[polygonIndex] < min)
                {
                    min = minX[polygonIndex];
                    ret = polygonIndex;
                }
            }
            
            return ret;
        }

        IntPoint getBounderyPointWithOffset(int polygonIndex, int pointIndex)
        {
            IntPoint previousPoint = bounderyPolygons[polygonIndex][(pointIndex > 0) ? (pointIndex - 1) : (bounderyPolygons[polygonIndex].Count - 1)];
            IntPoint currentPoint = bounderyPolygons[polygonIndex][pointIndex];
            IntPoint nextPoint = bounderyPolygons[polygonIndex][(pointIndex < (bounderyPolygons[polygonIndex].Count - 1)) ? (pointIndex + 1) : (0)];

            IntPoint off0 = ((currentPoint - previousPoint).normal(1000)).GetPerpendicularLeft();
            IntPoint off1 = ((nextPoint - currentPoint).normal(1000)).GetPerpendicularLeft();
            IntPoint n = (off0 + off1).normal(200);

            return currentPoint + n;
        }

        public Comb(Polygons _boundery)
        {
            this.bounderyPolygons = _boundery;
            minX = new long[bounderyPolygons.Count];
            maxX = new long[bounderyPolygons.Count];
            minIdx = new int[bounderyPolygons.Count];
            maxIdx = new int[bounderyPolygons.Count];
        }

        public bool checkInside(IntPoint p)
        {
            // Check if we are inside the comb boundary. We do this by tracing from the point towards the negative X direction,
            // every boundary we cross increments the crossings counter. If we have an even number of crossings then we are not inside the boundary
            int crossings = 0;
            for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
            {
                Polygon boundryPolygon = bounderyPolygons[bounderyIndex];

                if (boundryPolygon.Count < 1)
                {
                    continue;
                }

                IntPoint p0 = boundryPolygon[boundryPolygon.Count - 1];
                for (int i = 0; i < boundryPolygon.Count; i++)
                {
                    IntPoint p1 = boundryPolygon[i];

                    if ((p0.Y >= p.Y && p1.Y < p.Y) || (p1.Y > p.Y && p0.Y <= p.Y))
                    {
                        long x = p0.X + (p1.X - p0.X) * (p.Y - p0.Y) / (p1.Y - p0.Y);
                        if (x >= p.X)
                        {
                            crossings++;
                        }
                    }
                    p0 = p1;
                }
            }

            if ((crossings % 2) == 0)
            {
                return false;
            }

            return true;
        }

        public bool moveInside(IntPoint p, int distance = 100)
        {
            IntPoint ret = p;
            long bestDist = 2000 * 2000;
            for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
            {
                Polygon boundryPolygon = bounderyPolygons[bounderyIndex];

                if (boundryPolygon.Count < 1)
                {
                    continue;
                }

                IntPoint p0 = boundryPolygon[boundryPolygon.Count - 1];
                for (int i = 0; i < boundryPolygon.Count; i++)
                {
                    IntPoint p1 = boundryPolygon[i];

                    //Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
                    IntPoint pDiff = p1 - p0;
                    long lineLength = (pDiff).vSize();
                    long distOnLine = (pDiff).Dot(p - p0) / lineLength;
                    if (distOnLine < 10)
                        distOnLine = 10;
                    if (distOnLine > lineLength - 10)
                        distOnLine = lineLength - 10;
                    IntPoint q = p0 + pDiff * distOnLine / lineLength;

                    long dist = (q - p).vSize2();
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        ret = q + ((p1 - p0).normal(distance)).GetPerpendicularLeft();
                    }

                    p0 = p1;
                }
            }
            if (bestDist < 2000 * 2000)
            {
                p = ret;
                return true;
            }
            return false;
        }

        public bool calc(IntPoint startPoint, IntPoint endPoint, List<IntPoint> combPoints)
        {
            if ((endPoint - startPoint).shorterThen(1500))
            {
                return true;
            }

            bool addEndpoint = false;
            //Check if we are inside the comb boundaries
            if (!checkInside(startPoint))
            {
                if (!moveInside(startPoint))
                {
                    //If we fail to move the point inside the comb boundary we need to retract.
                    return false;
                }

                combPoints.Add(startPoint);
            }
            if (!checkInside(endPoint))
            {
                if (!moveInside(endPoint))
                {
                    //If we fail to move the point inside the comb boundary we need to retract.
                    return false;
                }

                addEndpoint = true;
            }

            //Check if we are crossing any bounderies, and pre-calculate some values.
            if (!preTest(startPoint, endPoint))
            {
                //We're not crossing any boundaries. So skip the comb generation.
                if (!addEndpoint && combPoints.Count == 0)
                {
                    //Only skip if we didn't move the start and end point.
                    return true;
                }
            }

            //Calculate the minimum and maximum positions where we cross the comb boundary
            calcMinMax();

            long x = startPoint.X;
            List<IntPoint> pointList = new List<IntPoint>();
            // Now walk trough the crossings, for every boundary we cross, find the initial cross point and the exit point. Then add all the points in between
            // to the pointList and continue with the next boundary we will cross, until there are no more boundaries to cross.
            // This gives a path from the start to finish curved around the holes that it encounters.
            while (true)
            {
                int n = getPolygonIndexAbove(x);
                if (n == int.MaxValue)
                {
                    break;
                }

                pointList.Add(matrix.unapply(new IntPoint(minX[n] - 200, startPoint.Y)));
                if ((minIdx[n] - maxIdx[n] + bounderyPolygons[n].Count) % bounderyPolygons[n].Count > (maxIdx[n] - minIdx[n] + bounderyPolygons[n].Count) % bounderyPolygons[n].Count)
                {
                    for (int i = minIdx[n]; i != maxIdx[n]; i = (i < bounderyPolygons[n].Count - 1) ? (i + 1) : (0))
                    {
                        pointList.Add(getBounderyPointWithOffset(n, i));
                    }
                }
                else
                {
                    minIdx[n]--;
                    if (minIdx[n] == int.MaxValue)
                    {
                        minIdx[n] = bounderyPolygons[n].Count - 1;
                    }

                    maxIdx[n]--;
                    if (maxIdx[n] == int.MaxValue)
                    {
                        maxIdx[n] = bounderyPolygons[n].Count - 1;
                    }

                    for (int i = minIdx[n]; i != maxIdx[n]; i = (i > 0) ? (i - 1) : (bounderyPolygons[n].Count - 1))
                    {
                        pointList.Add(getBounderyPointWithOffset(n, i));
                    }
                }
                pointList.Add(matrix.unapply(new IntPoint(maxX[n] + 200, startPoint.Y)));

                x = maxX[n];
            }
            pointList.Add(endPoint);

            //Optimize the pointList, skip each point we could already reach by not crossing a boundary. This smooths out the path and makes it skip any unneeded corners.
            IntPoint p0 = startPoint;
            for (int n = 1; n < pointList.Count; n++)
            {
                if (collisionTest(p0, pointList[n]))
                {
                    if (collisionTest(p0, pointList[n - 1]))
                    {
                        return false;
                    }

                    p0 = pointList[n - 1];
                    combPoints.Add(p0);
                }
            }
            
            if (addEndpoint)
            {
                combPoints.Add(endPoint);
            }

            return true;
        }
    }
}
