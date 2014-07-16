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
        public IntPoint startPosition;
        public List<Polygon> polygons = new List<Polygon>();
        public List<int> polyStart = new List<int>();
        public List<int> polyOrder = new List<int>();

        public PathOrderOptimizer(IntPoint startPoint)
        {
            this.startPosition = startPoint;
        }

        public void addPolygon(Polygon polygon)
        {
            this.polygons.Add(polygon);
        }

        public void addPolygons(Polygons polygons)
        {
            for (int i = 0; i < polygons.Count; i++)
            {
                this.polygons.Add(polygons[i]);
            }
        }

        public void optimize()
        {
            List<bool> picked = new List<bool>();
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
                polyStart.Add(bestPointIndex);
                picked.Add(false);
            }

            IntPoint incommingPerpundicularNormal = new IntPoint(0, 0);
            IntPoint currentPosition = startPosition;
            for (int positionIndex = 0; positionIndex < polygons.Count; positionIndex++)
            {
                int bestIndex = -1;
                double bestDist = double.MaxValue;
                for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
                {
                    if (picked[polygonIndex] || polygons[polygonIndex].Count < 1)
                    {
                        continue;
                    }

                    if (polygons[polygonIndex].Count == 2)
                    {
                        double dist = (polygons[polygonIndex][0] - currentPosition).LengthSquared();
                        dist += Math.Abs(incommingPerpundicularNormal.Dot(polygons[polygonIndex][1] - polygons[polygonIndex][0].normal(1000))) * 0.0001f;
                        if (dist < bestDist)
                        {
                            bestIndex = polygonIndex;
                            bestDist = dist;
                            polyStart[polygonIndex] = 0;
                        }
                        
                        dist = (polygons[polygonIndex][1] - currentPosition).LengthSquared();
                        dist += Math.Abs(incommingPerpundicularNormal.Dot(polygons[polygonIndex][0] - polygons[polygonIndex][1].normal(1000))) * 0.0001f;
                        if (dist < bestDist)
                        {
                            bestIndex = polygonIndex;
                            bestDist = dist;
                            polyStart[polygonIndex] = 1;
                        }
                    }
                    else
                    {
                        double dist = (polygons[polygonIndex][polyStart[polygonIndex]] - currentPosition).LengthSquared();
                        if (dist < bestDist)
                        {
                            bestIndex = polygonIndex;
                            bestDist = dist;
                        }
                    }
                }

                if (bestIndex > -1)
                {
                    if (polygons[bestIndex].Count == 2)
                    {
                        int endIndex = (polyStart[bestIndex] + 1) % 2;
                        currentPosition = polygons[bestIndex][endIndex];
                        incommingPerpundicularNormal = (polygons[bestIndex][endIndex] - polygons[bestIndex][polyStart[bestIndex]]).normal(1000).CrossZ();
                    }
                    else
                    {
                        currentPosition = polygons[bestIndex][polyStart[bestIndex]];
                        incommingPerpundicularNormal = new IntPoint(0, 0);
                    }
                    picked[bestIndex] = true;
                    polyOrder.Add(bestIndex);
                }
            }

            currentPosition = startPosition;
            for (int n = 0; n < polyOrder.Count; n++)
            {
                int nr = polyOrder[n];
                int best = -1;
                double bestDist = double.MaxValue;
                for (int i = 0; i < polygons[nr].Count; i++)
                {
                    double dist = (polygons[nr][i] - currentPosition).LengthSquared();
                    if (dist < bestDist)
                    {
                        best = i;
                        bestDist = dist;
                    }
                }
                polyStart[nr] = best;
                if (polygons[nr].Count <= 2)
                {
                    currentPosition = polygons[nr][(best + 1) % 2];
                }
                else
                {
                    currentPosition = polygons[nr][best];
                }
            }
        }
    }
}
