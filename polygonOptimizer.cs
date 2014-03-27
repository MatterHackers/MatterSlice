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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class PolygonOptimizer
    {
        public static void optimizePolygon(Polygon poly)
        {
            long minimumDistanceToCreateNewPosition = 10;

            IntPoint previousPosition = poly[poly.Count - 1];
            for (int pointIndex = 0; pointIndex < poly.Count; pointIndex++)
            {
                IntPoint currentPosition = poly[pointIndex];
                if ((currentPosition - previousPosition).shorterThen(minimumDistanceToCreateNewPosition))
                {
                    // anything shorter than 
                    poly.RemoveAt(pointIndex);
                    pointIndex--;
                }
                else if ((currentPosition - previousPosition).shorterThen(500))
                {
                    IntPoint nextPosition;
                    if (pointIndex < poly.Count - 1)
                    {
                        nextPosition = poly[pointIndex+1];
                    }
                    else
                    {
                        nextPosition = poly[0];
                    }
              
                    IntPoint normalPrevToCur = (currentPosition - previousPosition).normal(100000);
                    IntPoint normalCurToNext = (nextPosition - currentPosition).normal(100000);
              
                    long d = normalPrevToCur.Dot(normalCurToNext);
                    long xLengthToConsiderSameDirection = 95000;
                    if (d > (xLengthToConsiderSameDirection * xLengthToConsiderSameDirection)) // if they are mostly going the same direction 
                    {
                        poly.RemoveAt(pointIndex);
                        pointIndex --; // check this point again against the next point
                    }
                    else
                    {
                        previousPosition = currentPosition;
                    }
                }
                else
                {
                    previousPosition = currentPosition;
                }           
            }
        }

        public static void optimizePolygons(Polygons polys)
        {
            for (int n = 0; n < polys.Count; n++)
            {
                optimizePolygon(polys[n]);
                if (polys[n].Count < 3)
                {
                    polys.RemoveAt(n);
                    n--;
                }
            }
        }
    }
}