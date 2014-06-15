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
//#define OUTPUT_DEBUG_DATA

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
        public static double BridgeAngle(Polygons outline, SliceLayer prevLayer, string debugName = "")
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

#if OUTPUT_DEBUG_DATA
            string outlineString = outline.WriteToString();
            string partOutlineString = "";
            foreach (SliceLayerPart prevLayerPart in prevLayer.parts)
            {
                foreach (Polygon prevPartOutline in prevLayerPart.outline)
                {
                    partOutlineString += prevPartOutline.WriteToString();
                }

                partOutlineString += "|";
            }
#endif

            if (islands.Count == 1)
            {
                int count = islands[0].Count;

                // Lets find the area of each concave section and take the clossing of the largest.

                // we need to find the first convex angle to be our start of finding the cancave area
                int startIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    IntPoint prev = islands[0][(i + count - 1) % count];
                    IntPoint curr = islands[0][i];
                    IntPoint next = islands[0][(i + 1) % count];

                    double cross = (prev - curr).Cross(next - curr);
                    if (cross < 0)
                    {
                        startIndex = i;
                        break;
                    }
                }

                double longestSide = 0;
                double bestAngle = -1;

                // check if it is concave
                for (int i = 0; i < count; i++)
                {
                    IntPoint prev = islands[0][(startIndex + i + count - 1) % count];
                    IntPoint curr = islands[0][(startIndex + i) % count];
                    IntPoint next = islands[0][(startIndex + i + 1) % count];

                    if ((prev - curr).Cross(next - curr) > 0)
                    {
                        IntPoint convexStart = prev;

                        // We found a concave angle. now we want to find the first non-concave angle and make
                        // a bridge at the start and end angle of the concave region 
                        for (int j = i+1; j < count; j++)
                        {
                            IntPoint prev2 = islands[0][(startIndex + j + count - 1) % count];
                            IntPoint curr2 = islands[0][(startIndex + j) % count];
                            IntPoint next2 = islands[0][(startIndex + j + 1) % count];

                            if ((prev2 - curr2).Cross(next2 - curr2) <= 0)
                            {
                                IntPoint sideDelta = curr2 - convexStart;
                                double lengthOfSide = sideDelta.Length();
                                if (lengthOfSide > longestSide)
                                {
                                    bestAngle = Math.Atan2(sideDelta.Y, sideDelta.X) * 180 / Math.PI;
#if OUTPUT_DEBUG_DATA
                                    islands.SaveToGCode("{0} - angle {1:0.}.gcode".FormatWith(debugName, bestAngle));

#endif
                                    i = j+1;
                                    break;
                                }
                            }
                        }
                    }
                }

                Range0To360(ref bestAngle);
                return bestAngle;
            }

            if (islands.Count > 5 || islands.Count < 1)
            {
                return -1;
            }

            //Next find the 2 largest islands that we rest on.
            double biggestArea = 0;
            double nextBiggestArea = 0;
            int indexOfBiggest = -1;
            int indexOfNextBigest = -1;
            for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
            {
                //Skip internal holes
                if (!islands[islandIndex].Orientation())
                {
                    continue;
                }

                double area = Math.Abs(islands[islandIndex].Area());
                if (area > biggestArea)
                {
                    if (biggestArea > nextBiggestArea)
                    {
                        nextBiggestArea = biggestArea;
                        indexOfNextBigest = indexOfBiggest;
                    }
                    biggestArea = area;
                    indexOfBiggest = islandIndex;
                }
                else if (area > nextBiggestArea)
                {
                    nextBiggestArea = area;
                    indexOfNextBigest = islandIndex;
                }
            }

            if (indexOfBiggest < 0 || indexOfNextBigest < 0)
            {
                return -1;
            }

            IntPoint center1 = islands[indexOfBiggest].CenterOfMass();
            IntPoint center2 = islands[indexOfNextBigest].CenterOfMass();

            double angle = Math.Atan2(center2.Y - center1.Y, center2.X - center1.X) / Math.PI * 180;
            Range0To360(ref angle);
#if OUTPUT_DEBUG_DATA
            islands.SaveToGCode("{0} - angle {1:0.}.gcode".FormatWith(debugName, angle));
#endif
            return angle;
        }

        static void Range0To360(ref double angle)
        {
            if (angle < 0)
            {
                angle += 360;
            }
            if (angle > 360)
            {
                angle -= 360;
            }
        }
    }
}