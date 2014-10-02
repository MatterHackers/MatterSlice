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

    public class PathOrderOptimizer
    {
        IntPoint startPosition;
        List<Polygon> polygons = new List<Polygon>();
        public List<int> startIndexInPolygon = new List<int>();
        public List<int> bestPolygonOrderIndex = new List<int>();

        public PathOrderOptimizer(IntPoint startPoint)
        {
            this.startPosition = startPoint;
        }

        public void AddPolygon(Polygon polygon)
        {
            this.polygons.Add(polygon);
        }

        public void AddPolygons(Polygons polygons)
        {
            for (int i = 0; i < polygons.Count; i++)
            {
                this.polygons.Add(polygons[i]);
            }
        }

        public void Optimize()
        {
            // Find the point that is closest to our current position (start position)
            bool[] polygonHasBeenAdded = new bool[polygons.Count];
            for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
            {
                int bestPointIndex = -1;
                double closestDist = double.MaxValue;
                Polygon currentPolygon = polygons[polygonIndex];
                for (int pointIndex = 0; pointIndex < currentPolygon.Count; pointIndex++)
                {
                    double dist = (currentPolygon[pointIndex] - startPosition).LengthSquared();
                    if (dist < closestDist)
                    {
                        bestPointIndex = pointIndex;
                        closestDist = dist;
                    }
                }
                startIndexInPolygon.Add(bestPointIndex);
            }

            //IntPoint incommingPerpendicularNormal = new IntPoint(0, 0);
            IntPoint currentPosition = startPosition;
            // We loop over the polygon list twice, as each inner loop we only pick one polygon.
            for (int polygonIndexOuterLoop = 0; polygonIndexOuterLoop < polygons.Count; polygonIndexOuterLoop++)
            {
                int bestPolygonIndex = -1;
                double bestDist = double.MaxValue;
                for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
                {
                    if (polygonHasBeenAdded[polygonIndex] || polygons[polygonIndex].Count < 1)
                    {
                        continue;
                    }

                    // If there are only 2 points (a single line) we are willing to start from the start or the end.
                    if (polygons[polygonIndex].Count == 2)
                    {
                        double distToSart = (polygons[polygonIndex][0] - currentPosition).LengthSquared();
                        //dist += Math.Abs(incommingPerpendicularNormal.Dot(polygons[polygonIndex][1] - polygons[polygonIndex][0].normal(1000))) * 0.0001f;
                        if (distToSart < bestDist)
                        {
                            bestPolygonIndex = polygonIndex;
                            bestDist = distToSart;
                            startIndexInPolygon[polygonIndex] = 0;
                        }
                        
                        double distToEnd = (polygons[polygonIndex][1] - currentPosition).LengthSquared();
                        //dist += Math.Abs(incommingPerpendicularNormal.Dot(polygons[polygonIndex][0] - polygons[polygonIndex][1].normal(1000))) * 0.0001f;
                        if (distToEnd < bestDist)
                        {
                            bestPolygonIndex = polygonIndex;
                            bestDist = distToEnd;
                            startIndexInPolygon[polygonIndex] = 1;
                        }
                    }
                    else
                    {
                        double dist = (polygons[polygonIndex][startIndexInPolygon[polygonIndex]] - currentPosition).LengthSquared();
                        if (dist < bestDist)
                        {
                            bestPolygonIndex = polygonIndex;
                            bestDist = dist;
                        }
                    }
                }

                if (bestPolygonIndex > -1)
                {
                    if (polygons[bestPolygonIndex].Count == 2)
                    {
                        // get the point that is opposite from the one we started on
                        int startIndex = startIndexInPolygon[bestPolygonIndex];
                        int endIndex = (startIndex + 1) % 2;
                        currentPosition = polygons[bestPolygonIndex][endIndex];
                        //incommingPerpendicularNormal = (polygons[bestIndex][endIndex] - polygons[bestIndex][polyStart[bestIndex]]).normal(1000).CrossZ();
                    }
                    else
                    {
                        currentPosition = polygons[bestPolygonIndex][startIndexInPolygon[bestPolygonIndex]];
                        //incommingPerpendicularNormal = new IntPoint(0, 0);
                    }
                    polygonHasBeenAdded[bestPolygonIndex] = true;
                    bestPolygonOrderIndex.Add(bestPolygonIndex);
                }
            }

            currentPosition = startPosition;
            foreach(int bestPolygonIndex in bestPolygonOrderIndex)
            {
                int bestStartPoint = -1;
                double bestDist = double.MaxValue;
                for (int pointIndex = 0; pointIndex < polygons[bestPolygonIndex].Count; pointIndex++)
                {
                    double dist = (polygons[bestPolygonIndex][pointIndex] - currentPosition).LengthSquared();
                    if (dist < bestDist)
                    {
                        bestStartPoint = pointIndex;
                        bestDist = dist;
                    }
                }
                startIndexInPolygon[bestPolygonIndex] = bestStartPoint;
                if (polygons[bestPolygonIndex].Count == 2)
                {
                    currentPosition = polygons[bestPolygonIndex][(bestStartPoint + 1) % 2];
                }
                else
                {
                    currentPosition = polygons[bestPolygonIndex][bestStartPoint];
                }
            }
        }
    }
}
