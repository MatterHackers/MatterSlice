/*
This file is part of MatterSlice. A commandline utility for 
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
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
        public static bool BridgeAngle(Polygons outline, SliceLayer prevLayer, out double bridgeAngle, string debugName = "")
        {
            bridgeAngle = -1;
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

            string islandsString = islands.WriteToString();
#endif

            if (islands.Count > 5 || islands.Count < 1)
            {
                return false;
            }

            if (islands.Count == 1)
            {
                return GetSingleIslandAngle(outline, islands[0], out bridgeAngle, debugName);
            }

            // Find the 2 largest islands that we rest on.
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
                return false;
            }

            IntPoint center1 = islands[indexOfBiggest].CenterOfMass();
            IntPoint center2 = islands[indexOfNextBigest].CenterOfMass();

            bridgeAngle = Math.Atan2(center2.Y - center1.Y, center2.X - center1.X) / Math.PI * 180;
            Range0To360(ref bridgeAngle);
#if OUTPUT_DEBUG_DATA
            islands.SaveToGCode("{0} - angle {1:0.}.gcode".FormatWith(debugName, bridgeAngle));
#endif
            return true;
        }

        public static bool GetSingleIslandAngle(Polygons outline, Polygon island, out double bridgeAngle, string debugName)
        {
            bridgeAngle = -1;
            int island0PointCount = island.Count;

            // Check if the island exactly matches the outline (if it does no bridging is going to happen)
            if (outline.Count == 1 && island0PointCount == outline[0].Count)
            {
                for (int i = 0; i < island0PointCount; i++)
                {
                    if (island[i] != outline[0][i])
                    {
                        break;
                    }
                }

                // they are all the same so we don't need to change the angle
                return false;
            }

            // we need to find the first convex angle to be our start of finding the cancave area
            int startIndex = 0;
            for (int i = 0; i < island0PointCount; i++)
            {
                IntPoint curr = island[i];

                if (outline[0].Contains(curr))
                {
                    startIndex = i;
                    break;
                }
            }

            double longestSide = 0;
            double bestAngle = -1;

            // check if it is concave
            for (int i = 0; i < island0PointCount; i++)
            {
                IntPoint curr = island[(startIndex + i) % island0PointCount];

                if (!outline[0].Contains(curr))
                {
                    IntPoint prev = island[(startIndex + i + island0PointCount - 1) % island0PointCount];
                    IntPoint convexStart = prev;

                    // We found a concave angle. now we want to find the first non-concave angle and make
                    // a bridge at the start and end angle of the concave region 
                    for (int j = i + 1; j < island0PointCount + i; j++)
                    {
                        IntPoint curr2 = island[(startIndex + j) % island0PointCount];

                        if (outline[0].Contains(curr2))
                        {
                            IntPoint sideDelta = curr2 - convexStart;
                            double lengthOfSide = sideDelta.Length();
                            if (lengthOfSide > longestSide)
                            {
                                bestAngle = Math.Atan2(sideDelta.Y, sideDelta.X) * 180 / Math.PI;
                                longestSide = lengthOfSide;
#if OUTPUT_DEBUG_DATA
                                island.SaveToGCode("{0} - angle {1:0.}.gcode".FormatWith(debugName, bestAngle));

#endif
                                i = j + 1;
                                break;
                            }
                        }
                    }
                }
            }

            if (bestAngle == -1)
            {
                return false;
            }

            Range0To360(ref bestAngle);
            bridgeAngle = bestAngle;
            return true;
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