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

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public static class MultiVolumes
    {
        public static void carveMultipleVolumes(List<SliceVolumeStorage> volumes)
        {
            //Go trough all the volumes, and remove the previous volume outlines from our own outline, so we never have overlapped areas.
            for (int idx = 0; idx < volumes.Count; idx++)
            {
                for (int idx2 = 0; idx2 < idx; idx2++)
                {
                    for (int layerNr = 0; layerNr < volumes[idx].layers.Count; layerNr++)
                    {
                        SliceLayer layer1 = volumes[idx].layers[layerNr];
                        SliceLayer layer2 = volumes[idx2].layers[layerNr];
                        for (int p1 = 0; p1 < layer1.parts.Count; p1++)
                        {
                            for (int p2 = 0; p2 < layer2.parts.Count; p2++)
                            {
                                layer1.parts[p1].outline = layer1.parts[p1].outline.CreateDifference(layer2.parts[p2].outline);
                            }
                        }
                    }
                }
            }
        }

        //Expand each layer a bit and then keep the extra overlapping parts that overlap with other volumes.
        //This generates some overlap in dual extrusion, for better bonding in touching parts.
        public static void generateMultipleVolumesOverlap(List<SliceVolumeStorage> volumes, int overlap)
        {
            if (volumes.Count < 2 || overlap <= 0) return;

            for (int layerNr = 0; layerNr < volumes[0].layers.Count; layerNr++)
            {
                Polygons fullLayer = new Polygons();
                for (int volIdx = 0; volIdx < volumes.Count; volIdx++)
                {
                    SliceLayer layer1 = volumes[volIdx].layers[layerNr];
                    for (int p1 = 0; p1 < layer1.parts.Count; p1++)
                    {
                        fullLayer = fullLayer.CreateUnion(layer1.parts[p1].outline.Offset(20));
                    }
                }
                fullLayer = fullLayer.Offset(-20);

                for (int volIdx = 0; volIdx < volumes.Count; volIdx++)
                {
                    SliceLayer layer1 = volumes[volIdx].layers[layerNr];
                    for (int p1 = 0; p1 < layer1.parts.Count; p1++)
                    {
                        layer1.parts[p1].outline = fullLayer.CreateIntersection(layer1.parts[p1].outline.Offset(overlap / 2));
                    }
                }
            }
        }
    }
}