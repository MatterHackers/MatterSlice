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
using System.Diagnostics;
using System.IO;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public static class PolygonOptimizer
    {
        public static void optimizePolygon(Polygon poly)
        {
            IntPoint p0 = poly[poly.Count - 1];
            for (int i = 0; i < poly.Count; i++)
            {
                IntPoint p1 = poly[i];
                if ((p0 - p1).shorterThen(10))
                {
                    poly.RemoveAt(i);
                    i--;
                }
                else
                {
                    IntPoint p2;
                    if (i < poly.Count - 1)
                    {
                        p2 = poly[i + 1];
                    }
                    else
                    {
                        p2 = poly[0];
                    }

                    IntPoint diff0 = (p1 - p0).normal(1000000);
                    IntPoint diff2 = (p1 - p2).normal(1000000);

                    long d = diff0.dot(diff2);
                    if (d < long.MinValue)
                    {
                        poly.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        p0 = p1;
                    }
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