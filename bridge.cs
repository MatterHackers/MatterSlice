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
using System.Collections.Generic;
using System.Diagnostics;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class Bridge
    {
        public static int bridgeAngle(Polygons outline, SliceLayer prevLayer)
        {
            AABB boundaryBox = new AABB(outline);
            //To detect if we have a bridge, first calculate the intersection of the current layer with the previous layer.
            // This gives us the islands that the layer rests on.
            Polygons islands = new Polygons();
            foreach(SliceLayerPart prevLayerPart in prevLayer.parts)
            {
                if (!boundaryBox.hit(prevLayerPart.boundaryBox))
                {
                    continue;
                }

                islands.AddRange(outline.CreateIntersection(prevLayerPart.outline));
            }

            if (islands.Count == 1)
            {
                int count = islands[0].Count;
                // check if it is concave
                for (int i = 0; i < count; i++)
                {
                    continue; // disable all this for now
                    IntPoint prev = islands[0][(i + count - 1) % count];
                    IntPoint curr = islands[0][i];
                    IntPoint next = islands[0][(i + 1) % count];

                    if ((prev - curr).Cross(next - curr) > 0)
                    {
                        IntPoint convexStart = prev;

                        // We found a concave angle. now we want to find the first non-concave angle and make
                        // a bridge at the start and en angle of the concave region 
                        for (int j = i+1; j < count; j++)
                        {
                            IntPoint prev2 = islands[0][(j + count - 1) % count];
                            IntPoint curr2 = islands[0][j];
                            IntPoint next2 = islands[0][(j + 1) % count];

                            if ((prev2 - curr2).Cross(next2 - curr2) <= 0)
                            {
                                int angleOfConvexStartToEnd = (int)(Math.Atan2((curr2 - convexStart).Y, (curr2 - convexStart).X) * 180 / Math.PI + 90);
                                return angleOfConvexStartToEnd;
                            }
                        }

                        int angleOfConvexStartToEnd2 = (int)(Math.Atan2((next - prev).Y, (next - prev).X) * 180 / Math.PI + 90);
                        return angleOfConvexStartToEnd2;
                    }
                }
            }

            if (islands.Count > 5 || islands.Count < 1)
            {
                return -1;
            }

            //Next find the 2 largest islands that we rest on.
            double area1 = 0;
            double area2 = 0;
            int idx1 = -1;
            int idx2 = -1;
            for (int n = 0; n < islands.Count; n++)
            {
                //Skip internal holes
                if (!islands[n].Orientation())
                {
                    continue;
                }

                double area = Math.Abs(islands[n].Area());
                if (area > area1)
                {
                    if (area1 > area2)
                    {
                        area2 = area1;
                        idx2 = idx1;
                    }
                    area1 = area;
                    idx1 = n;
                }
                else if (area > area2)
                {
                    area2 = area;
                    idx2 = n;
                }
            }

            if (idx1 < 0 || idx2 < 0)
            {
                return -1;
            }

            IntPoint center1 = islands[idx1].CenterOfMass();
            IntPoint center2 = islands[idx2].CenterOfMass();

            double angle = Math.Atan2(center2.X - center1.X, center2.Y - center1.Y) / Math.PI * 180;
            if (angle < 0)
            {
                angle += 360;
            }

            return (int)angle;
        }
    }
}