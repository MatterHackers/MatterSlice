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

        long[] minXPosition;
        long[] maxXPosition;
        int[] indexOfMinX;
        int[] indexOfMaxX;

        PointMatrix matrix;
        IntPoint startPoint;
        IntPoint endPoint;

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

                IntPoint lastPosition = matrix.apply(boundryPolygon[boundryPolygon.Count - 1]);
                for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
                {
                    IntPoint currentPosition = matrix.apply(boundryPolygon[pointIndex]);
                    if ((lastPosition.Y > startPoint.Y && currentPosition.Y < startPoint.Y) 
                        || (currentPosition.Y > startPoint.Y && lastPosition.Y < startPoint.Y))
                    {
                        long x = lastPosition.X + (currentPosition.X - lastPosition.X) * (startPoint.Y - lastPosition.Y) / (currentPosition.Y - lastPosition.Y);

                        if (x > startPoint.X && x < endPoint.X)
                        {
                            return true;
                        }
                    }

                    lastPosition = currentPosition;
                }
            }
            return false;
        }

        void calcMinMax()
        {
            for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
            {
                Polygon boundryPolygon = bounderyPolygons[bounderyIndex];

                minXPosition[bounderyIndex] = long.MaxValue;
                maxXPosition[bounderyIndex] = long.MinValue;
                IntPoint lastPosition = matrix.apply(boundryPolygon[boundryPolygon.Count - 1]);
                for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
                {
                    IntPoint currentPosition = matrix.apply(boundryPolygon[pointIndex]);
                    if ((lastPosition.Y > startPoint.Y && currentPosition.Y < startPoint.Y) || (currentPosition.Y > startPoint.Y && lastPosition.Y < startPoint.Y))
                    {
                        long x = lastPosition.X + (currentPosition.X - lastPosition.X) * (startPoint.Y - lastPosition.Y) / (currentPosition.Y - lastPosition.Y);

                        if (x >= startPoint.X && x <= endPoint.X)
                        {
                            if (x < minXPosition[bounderyIndex]) 
                            { 
                                minXPosition[bounderyIndex] = x; 
                                indexOfMinX[bounderyIndex] = pointIndex; 
                            }

                            if (x > maxXPosition[bounderyIndex]) 
                            { 
                                maxXPosition[bounderyIndex] = x; 
                                indexOfMaxX[bounderyIndex] = pointIndex; 
                            }
                        }
                    }

                    lastPosition = currentPosition;
                }
            }
        }

        int getPolygonIndexAbove(long startingPolygonIndex)
        {
            long minXFound = long.MaxValue;
            int abovePolyIndex = -1;
            
            for (int polygonIndex = 0; polygonIndex < bounderyPolygons.Count; polygonIndex++)
            {
                if (minXPosition[polygonIndex] > startingPolygonIndex 
                    && minXPosition[polygonIndex] < minXFound)
                {
                    minXFound = minXPosition[polygonIndex];
                    abovePolyIndex = polygonIndex;
                }
            }
            
            return abovePolyIndex;
        }

        IntPoint getBounderyPointWithOffset(int polygonIndex, int pointIndex)
        {
            int previousIndex = pointIndex - 1;
            if(previousIndex < 0) 
            {
                previousIndex = bounderyPolygons[polygonIndex].Count - 1;
            }
            IntPoint previousPoint = bounderyPolygons[polygonIndex][previousIndex];

            IntPoint currentPoint = bounderyPolygons[polygonIndex][pointIndex];

            int nextIndex = pointIndex + 1;
            if(nextIndex >= bounderyPolygons[polygonIndex].Count)
            {
                nextIndex = 0;
            }
            IntPoint nextPoint = bounderyPolygons[polygonIndex][nextIndex];

            IntPoint off0 = ((currentPoint - previousPoint).normal(1000)).GetPerpendicularLeft();
            IntPoint off1 = ((nextPoint - currentPoint).normal(1000)).GetPerpendicularLeft();
            IntPoint n = (off0 + off1).normal(200);

            return currentPoint + n;
        }

        public Comb(Polygons bounderyPolygons)
        {
            this.bounderyPolygons = bounderyPolygons;
            minXPosition = new long[bounderyPolygons.Count];
            maxXPosition = new long[bounderyPolygons.Count];
            indexOfMinX = new int[bounderyPolygons.Count];
            indexOfMaxX = new int[bounderyPolygons.Count];
        }

        public bool checkInside(IntPoint pointToCheck)
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

                IntPoint lastPoint = boundryPolygon[boundryPolygon.Count - 1];
                for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
                {
                    IntPoint currentPoint = boundryPolygon[pointIndex];

                    if ((lastPoint.Y >= pointToCheck.Y && currentPoint.Y < pointToCheck.Y) 
                        || (currentPoint.Y > pointToCheck.Y && lastPoint.Y <= pointToCheck.Y))
                    {
                        long x = lastPoint.X + (currentPoint.X - lastPoint.X) * (pointToCheck.Y - lastPoint.Y) / (currentPoint.Y - lastPoint.Y);
                        if (x >= pointToCheck.X)
                        {
                            crossings++;
                        }
                    }
                    lastPoint = currentPoint;
                }
            }

            if ((crossings % 2) == 0)
            {
                return false;
            }

            return true;
        }

        public bool moveInside(IntPoint pointToMove, int distance = 100)
        {
            IntPoint newPosition = pointToMove;
            long bestDist = 2000 * 2000;
            for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
            {
                Polygon boundryPolygon = bounderyPolygons[bounderyIndex];

                if (boundryPolygon.Count < 1)
                {
                    continue;
                }

                IntPoint lastPoint = boundryPolygon[boundryPolygon.Count - 1];
                for (int i = 0; i < boundryPolygon.Count; i++)
                {
                    IntPoint currentPoint = boundryPolygon[i];

                    //Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
                    IntPoint deltaToCurrent = currentPoint - lastPoint;
                    long deltaLength = deltaToCurrent.vSize();
                    long distOnLine = deltaToCurrent.Dot(pointToMove - lastPoint) / deltaLength;
                    if (distOnLine < 10)
                    {
                        distOnLine = 10;
                    }

                    if (distOnLine > deltaLength - 10)
                    {
                        distOnLine = deltaLength - 10;
                    }

                    IntPoint q = lastPoint + deltaToCurrent * distOnLine / deltaLength;

                    long dist = (q - pointToMove).vSize2();
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        newPosition = q + ((currentPoint - lastPoint).normal(distance)).GetPerpendicularLeft();
                    }

                    lastPoint = currentPoint;
                }
            }

            if (bestDist < 2000 * 2000)
            {
                pointToMove = newPosition;
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
            if (!collisionTest(startPoint, endPoint))
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

            long startX = startPoint.X;
            List<IntPoint> pointList = new List<IntPoint>();
            // Now walk trough the crossings, for every boundary we cross, find the initial cross point and the exit point. 
            // Then add all the points in between to the pointList and continue with the next boundary we will cross, 
            // until there are no more boundaries to cross.
            // This gives a path from the start to finish curved around the holes that it encounters.
            while (true)
            {
                int abovePolyIndex = getPolygonIndexAbove(startX);
                if (abovePolyIndex < 0)
                {
                    break;
                }

                pointList.Add(matrix.unapply(new IntPoint(minXPosition[abovePolyIndex] - 200, startPoint.Y)));
                if ((indexOfMinX[abovePolyIndex] - indexOfMaxX[abovePolyIndex] + bounderyPolygons[abovePolyIndex].Count) % bounderyPolygons[abovePolyIndex].Count > (indexOfMaxX[abovePolyIndex] - indexOfMinX[abovePolyIndex] + bounderyPolygons[abovePolyIndex].Count) % bounderyPolygons[abovePolyIndex].Count)
                {
                    for (int i = indexOfMinX[abovePolyIndex]; i != indexOfMaxX[abovePolyIndex]; i = (i < bounderyPolygons[abovePolyIndex].Count - 1) ? (i + 1) : (0))
                    {
                        pointList.Add(getBounderyPointWithOffset(abovePolyIndex, i));
                    }
                }
                else
                {
                    indexOfMinX[abovePolyIndex]--;
                    if (indexOfMinX[abovePolyIndex] == -1)
                    {
                        indexOfMinX[abovePolyIndex] = bounderyPolygons[abovePolyIndex].Count - 1;
                    }

                    indexOfMaxX[abovePolyIndex]--;
                    if (indexOfMaxX[abovePolyIndex] == -1)
                    {
                        indexOfMaxX[abovePolyIndex] = bounderyPolygons[abovePolyIndex].Count - 1;
                    }

                    for (int i = indexOfMinX[abovePolyIndex]; i != indexOfMaxX[abovePolyIndex]; i = (i > 0) ? (i - 1) : (bounderyPolygons[abovePolyIndex].Count - 1))
                    {
                        pointList.Add(getBounderyPointWithOffset(abovePolyIndex, i));
                    }
                }
                pointList.Add(matrix.unapply(new IntPoint(maxXPosition[abovePolyIndex] + 200, startPoint.Y)));

                startX = maxXPosition[abovePolyIndex];
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
