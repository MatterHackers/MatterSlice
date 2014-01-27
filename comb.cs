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
using System.IO;
using System.Collections.Generic;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Point = IntPoint;
    public class Comb
    {
        Polygons boundery;

        long[] minX;
        long[] maxX;
        int[] minIdx;
        int[] maxIdx;

        PointMatrix matrix;
        Point sp;
        Point ep;

        bool preTest(Point startPoint, Point endPoint)
        {
            return collisionTest(startPoint, endPoint);
        }

        bool collisionTest(Point startPoint, Point endPoint)
        {
            Point diff = endPoint - startPoint;

            matrix = PointMatrix(diff);
            sp = matrix.apply(startPoint);
            ep = matrix.apply(endPoint);

            for (int n = 0; n < boundery.Count; n++)
            {
                if (boundery[n].Count < 1)
                    continue;
                Point p0 = matrix.apply(boundery[n][boundery[n].Count - 1]);
                for (int i = 0; i < boundery[n].Count; i++)
                {
                    Point p1 = matrix.apply(boundery[n][i]);
                    if ((p0.Y > sp.Y && p1.Y < sp.Y) || (p1.Y > sp.Y && p0.Y < sp.Y))
                    {
                        long x = p0.X + (p1.X - p0.X) * (sp.Y - p0.Y) / (p1.Y - p0.Y);

                        if (x > sp.X && x < ep.X)
                            return true;
                    }
                    p0 = p1;
                }
            }
            return false;
        }

        void calcMinMax()
        {
            for (int n = 0; n < boundery.Count; n++)
            {
                minX[n] = long.MaxValue;
                maxX[n] = long.MinValue;
                Point p0 = matrix.apply(boundery[n][boundery[n].Count - 1]);
                for (int i = 0; i < boundery[n].Count; i++)
                {
                    Point p1 = matrix.apply(boundery[n][i]);
                    if ((p0.Y > sp.Y && p1.Y < sp.Y) || (p1.Y > sp.Y && p0.Y < sp.Y))
                    {
                        long x = p0.X + (p1.X - p0.X) * (sp.Y - p0.Y) / (p1.Y - p0.Y);

                        if (x >= sp.X && x <= ep.X)
                        {
                            if (x < minX[n]) { minX[n] = x; minIdx[n] = i; }
                            if (x > maxX[n]) { maxX[n] = x; maxIdx[n] = i; }
                        }
                    }
                    p0 = p1;
                }
            }
        }

        int getPolygonAbove(long x)
        {
            long min = long.MaxValue;
            int ret = int.MaxValue;
            for (int n = 0; n < boundery.Count; n++)
            {
                if (minX[n] > x && minX[n] < min)
                {
                    min = minX[n];
                    ret = n;
                }
            }
            return ret;
        }

        Point getBounderyPointWithOffset(int polygonNr, int idx)
        {
            Point p0 = boundery[polygonNr][(idx > 0) ? (idx - 1) : (boundery[polygonNr].Count - 1)];
            Point p1 = boundery[polygonNr][idx];
            Point p2 = boundery[polygonNr][(idx < (boundery[polygonNr].Count - 1)) ? (idx + 1) : (0)];

            Point off0 = crossZ(normal(p1 - p0, 1000));
            Point off1 = crossZ(normal(p2 - p1, 1000));
            Point n = normal(off0 + off1, 200);

            return p1 + n;
        }

        public Comb(Polygons _boundery)
        {
            this.boundery = _boundery;
            minX = new long[boundery.Count];
            maxX = new long[boundery.Count];
            minIdx = new int[boundery.Count];
            maxIdx = new int[boundery.Count];
        }

        public bool checkInside(Point p)
        {
            //Check if we are inside the comb boundary. We do this by tracing from the point towards the negative X direction,
            //  every boundary we cross increments the crossings counter. If we have an even number of crossings then we are not inside the boundary
            int crossings = 0;
            for (int n = 0; n < boundery.Count; n++)
            {
                if (boundery[n].Count < 1)
                    continue;
                Point p0 = boundery[n][boundery[n].Count - 1];
                for (int i = 0; i < boundery[n].Count; i++)
                {
                    Point p1 = boundery[n][i];

                    if ((p0.Y >= p.Y && p1.Y < p.Y) || (p1.Y > p.Y && p0.Y <= p.Y))
                    {
                        long x = p0.X + (p1.X - p0.X) * (p.Y - p0.Y) / (p1.Y - p0.Y);
                        if (x >= p.X)
                            crossings++;
                    }
                    p0 = p1;
                }
            }
            if ((crossings % 2) == 0)
                return false;
            return true;
        }

        public bool moveInside(Point p, int distance)
        {
            Point ret = p;
            long bestDist = 2000 * 2000;
            for (int n = 0; n < boundery.Count; n++)
            {
                if (boundery[n].Count < 1)
                    continue;
                Point p0 = boundery[n][boundery[n].Count - 1];
                for (int i = 0; i < boundery[n].Count; i++)
                {
                    Point p1 = boundery[n][i];

                    //Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
                    Point pDiff = p1 - p0;
                    long lineLength = (pDiff).vSize();
                    long distOnLine = (pDiff).dot(p - p0) / lineLength;
                    if (distOnLine < 10)
                        distOnLine = 10;
                    if (distOnLine > lineLength - 10)
                        distOnLine = lineLength - 10;
                    Point q = p0 + pDiff * distOnLine / lineLength;

                    long dist = (q - p).vSize2();
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        ret = q + ((p1 - p0).normal(distance)).crossZ();
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

        public bool calc(Point startPoint, Point endPoint, List<Point> combPoints)
        {
            if (shorterThen(endPoint - startPoint, 1500))
                return true;

            bool addEndpoint = false;
            //Check if we are inside the comb boundaries
            if (!checkInside(startPoint))
            {
                if (!moveInside(startPoint))    //If we fail to move the point inside the comb boundary we need to retract.
                    return false;
                combPoints.Add(startPoint);
            }
            if (!checkInside(endPoint))
            {
                if (!moveInside(&endPoint))    //If we fail to move the point inside the comb boundary we need to retract.
                    return false;
                addEndpoint = true;
            }

            //Check if we are crossing any bounderies, and pre-calculate some values.
            if (!preTest(startPoint, endPoint))
            {
                //We're not crossing any boundaries. So skip the comb generation.
                if (!addEndpoint && combPoints.Count == 0) //Only skip if we didn't move the start and end point.
                    return true;
            }

            //Calculate the minimum and maximum positions where we cross the comb boundary
            calcMinMax();

            long x = sp.X;
            List<Point> pointList;
            //Now walk trough the crossings, for every boundary we cross, find the initial cross point and the exit point. Then add all the points in between
            // to the pointList and continue with the next boundary we will cross, until there are no more boundaries to cross.
            // This gives a path from the start to finish curved around the holes that it encounters.
            while (true)
            {
                int n = getPolygonAbove(x);
                if (n == int.MaxValue) break;

                pointList.Add(matrix.unapply(Point(minX[n] - 200, sp.Y)));
                if ((minIdx[n] - maxIdx[n] + boundery[n].Count) % boundery[n].Count > (maxIdx[n] - minIdx[n] + boundery[n].Count) % boundery[n].Count)
                {
                    for (int i = minIdx[n]; i != maxIdx[n]; i = (i < boundery[n].Count - 1) ? (i + 1) : (0))
                    {
                        pointList.Add(getBounderyPointWithOffset(n, i));
                    }
                }
                else
                {
                    minIdx[n]--;
                    if (minIdx[n] == int.MaxValue) minIdx[n] = boundery[n].Count - 1;
                    maxIdx[n]--;
                    if (maxIdx[n] == int.MaxValue) maxIdx[n] = boundery[n].Count - 1;
                    for (int i = minIdx[n]; i != maxIdx[n]; i = (i > 0) ? (i - 1) : (boundery[n].Count - 1))
                    {
                        pointList.Add(getBounderyPointWithOffset(n, i));
                    }
                }
                pointList.Add(matrix.unapply(new Point(maxX[n] + 200, sp.Y)));

                x = maxX[n];
            }
            pointList.Add(endPoint);

            //Optimize the pointList, skip each point we could already reach by not crossing a boundary. This smooths out the path and makes it skip any unneeded corners.
            Point p0 = startPoint;
            for (int n = 1; n < pointList.Count; n++)
            {
                if (collisionTest(p0, pointList[n]))
                {
                    if (collisionTest(p0, pointList[n - 1]))
                        return false;
                    p0 = pointList[n - 1];
                    combPoints.Add(p0);
                }
            }
            if (addEndpoint)
                combPoints.Add(endPoint);
            return true;
        }
    }
}
