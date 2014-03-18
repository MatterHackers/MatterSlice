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
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class Skin
    {
        public static void generateSkins(int layerNr, SliceVolumeStorage storage, int extrusionWidth, int downSkinCount, int upSkinCount, int infillOverlap)
        {
            SliceLayer layer = storage.layers[layerNr];

            for (int partNr = 0; partNr < layer.parts.Count; partNr++)
            {
                SliceLayerPart part = layer.parts[partNr];

                Polygons upskin = part.insets[part.insets.Count - 1].Offset(-extrusionWidth / 2);
                Polygons downskin = upskin;

                if (part.insets.Count > 1)
                {
                    //Add thin wall filling by taking the area between the insets.
                    Polygons thinWalls = part.insets[0].Offset(-extrusionWidth / 2 - extrusionWidth * infillOverlap / 100).CreateDifference(part.insets[1].Offset(extrusionWidth * 6 / 10));
                    upskin.AddAll(thinWalls);
                    downskin.AddAll(thinWalls);
                }
                if ((int)(layerNr - downSkinCount) >= 0)
                {
                    SliceLayer layer2 = storage.layers[layerNr - downSkinCount];
                    for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
                    {
                        if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
                            downskin = downskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1]);
                    }
                }
                if ((int)(layerNr + upSkinCount) < (int)storage.layers.Count)
                {
                    SliceLayer layer2 = storage.layers[layerNr + upSkinCount];
                    for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
                    {
                        if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
                            upskin = upskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1]);
                    }
                }

                part.skinOutline = upskin.CreateUnion(downskin);

                double minAreaSize = (2 * Math.PI * ((double)(extrusionWidth) / 1000.0) * ((double)(extrusionWidth) / 1000.0)) * 0.3;
                for (int i = 0; i < part.skinOutline.Count; i++)
                {
                    double area = Math.Abs(part.skinOutline[i].Area()) / 1000.0 / 1000.0;
                    if (area < minAreaSize) // Only create an up/down skin if the area is large enough. So you do not create tiny blobs of "trying to fill"
                    {
                        part.skinOutline.RemoveAt(i);
                        i -= 1;
                    }
                }
            }
        }

        public static void generateSparse(int layerNr, SliceVolumeStorage storage, int extrusionWidth, int downSkinCount, int upSkinCount)
        {
            SliceLayer layer = storage.layers[layerNr];

            for (int partNr = 0; partNr < layer.parts.Count; partNr++)
            {
                SliceLayerPart part = layer.parts[partNr];

                Polygons sparse = part.insets[part.insets.Count - 1].Offset(-extrusionWidth / 2);
                Polygons downskin = sparse;
                Polygons upskin = sparse;

                if ((int)(layerNr - downSkinCount) >= 0)
                {
                    SliceLayer layer2 = storage.layers[layerNr - downSkinCount];
                    for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
                    {
                        if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
                        {
                            if (layer2.parts[partNr2].insets.Count > 1)
                            {
                                downskin = downskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 2]);
                            }
                            else
                            {
                                downskin = downskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1]);
                            }
                        }
                    }
                }
                if ((int)(layerNr + upSkinCount) < (int)storage.layers.Count)
                {
                    SliceLayer layer2 = storage.layers[layerNr + upSkinCount];
                    for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
                    {
                        if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
                        {
                            if (layer2.parts[partNr2].insets.Count > 1)
                            {
                                upskin = upskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 2]);
                            }
                            else
                            {
                                upskin = upskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1]);
                            }
                        }
                    }
                }

                Polygons result = upskin.CreateUnion(downskin);

                double minAreaSize = 3.0;//(2 * M_PI * ((double)(config.extrusionWidth) / 1000.0) * ((double)(config.extrusionWidth) / 1000.0)) * 3;
                for (int i = 0; i < result.Count; i++)
                {
                    double area = Math.Abs(result[i].Area()) / 1000.0 / 1000.0;
                    if (area < minAreaSize) /* Only create an up/down skin if the area is large enough. So you do not create tiny blobs of "trying to fill" */
                    {
                        result.RemoveAt(i);
                        i -= 1;
                    }
                }

                part.sparseOutline = sparse.CreateDifference(result);
            }
        }
    }
}