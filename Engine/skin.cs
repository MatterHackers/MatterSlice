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
using ClipperLib;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
    public static class Skin
    {
        public static void generateSkins(int layerNr, SliceVolumeStorage storage, int extrusionWidth, int downSkinCount, int upSkinCount, int infillOverlap)
        {
            SliceLayer layer = storage.layers[layerNr];

            for (int partNr = 0; partNr < layer.parts.Count; partNr++)
            {
                SliceLayerPart part = layer.parts[partNr];
                Polygons temp;

                temp = Clipper.OffsetPolygons(part.insets[part.insets.Count - 1], -extrusionWidth / 2, ClipperLib.JoinType.jtSquare, 2, false);

                Clipper downskinClipper = new Clipper();
                Clipper upskinClipper = new Clipper();
                downskinClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);
                upskinClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);

                if (part.insets.Count > 1)
                {
                    ClipperLib.Clipper thinWallClipper = new Clipper();

                    temp = Clipper.OffsetPolygons(part.insets[0], -extrusionWidth / 2 - extrusionWidth * infillOverlap / 100, ClipperLib.JoinType.jtSquare, 2, false);
                    thinWallClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);

                    temp = Clipper.OffsetPolygons(part.insets[1], extrusionWidth * 6 / 10, ClipperLib.JoinType.jtSquare, 2, false);
                    thinWallClipper.AddPolygons(temp, ClipperLib.PolyType.ptClip);

                    thinWallClipper.Execute(ClipperLib.ClipType.ctDifference, temp);
                    downskinClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);
                    upskinClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);
                }

                if ((int)(layerNr - downSkinCount) >= 0)
                {
                    SliceLayer layer2 = storage.layers[layerNr - downSkinCount];
                    for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
                    {
                        if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
                        {
                            downskinClipper.AddPolygons(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1], ClipperLib.PolyType.ptClip);
                        }
                    }
                }
                if ((int)(layerNr + upSkinCount) < storage.layers.Count)
                {
                    SliceLayer layer2 = storage.layers[layerNr + upSkinCount];
                    for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
                    {
                        if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
                        {
                            upskinClipper.AddPolygons(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1], ClipperLib.PolyType.ptClip);
                        }
                    }
                }

                Polygons downSkin = new Polygons();
                Polygons upSkin = new Polygons();
                downskinClipper.Execute(ClipperLib.ClipType.ctDifference, downSkin);
                upskinClipper.Execute(ClipperLib.ClipType.ctDifference, upSkin);

                {
                    Clipper skinCombineClipper = new Clipper();
                    skinCombineClipper.AddPolygons(downSkin, ClipperLib.PolyType.ptSubject);
                    skinCombineClipper.AddPolygons(upSkin, ClipperLib.PolyType.ptClip);
                    skinCombineClipper.Execute(ClipperLib.ClipType.ctUnion, part.skinOutline);
                }

                double minAreaSize = 2 * Math.PI * (double)(extrusionWidth / 1000.0) * (double)(extrusionWidth / 1000.0) * 0.3;
                for (int i = 0; i < part.skinOutline.Count; i++)
                {
                    double area = Math.Abs(Clipper.Area(part.skinOutline[i])) / 1000.0 / 1000.0;
                    if (area < minAreaSize) /* Only create an up/down skin if the area is large enough. So you do not create tiny blobs of "trying to fill" */
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

                Polygons temp = Clipper.OffsetPolygons(part.insets[part.insets.Count - 1], -extrusionWidth / 2, ClipperLib.JoinType.jtSquare, 2, false);

                Clipper downskinClipper = new Clipper();
                Clipper upskinClipper = new Clipper();
                downskinClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);
                upskinClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);
                if ((int)(layerNr - downSkinCount) >= 0)
                {
                    SliceLayer layer2 = storage.layers[layerNr - downSkinCount];
                    for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
                    {
                        if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
                        {
                            if (layer2.parts[partNr2].insets.Count > 1)
                            {
                                downskinClipper.AddPolygons(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 2], ClipperLib.PolyType.ptClip);
                            }
                            else
                            {
                                downskinClipper.AddPolygons(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1], ClipperLib.PolyType.ptClip);
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
                                upskinClipper.AddPolygons(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 2], ClipperLib.PolyType.ptClip);
                            }
                            else
                            {
                                upskinClipper.AddPolygons(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1], ClipperLib.PolyType.ptClip);
                            }
                        }
                    }
                }

                Polygons downSkin = new Polygons();
                Polygons upSkin = new Polygons();
                Polygons result = new Polygons();
                downskinClipper.Execute(ClipperLib.ClipType.ctDifference, downSkin);
                upskinClipper.Execute(ClipperLib.ClipType.ctDifference, upSkin);

                {
                    Clipper skinClipper = new Clipper();
                    skinClipper.AddPolygons(downSkin, ClipperLib.PolyType.ptSubject);
                    skinClipper.AddPolygons(upSkin, ClipperLib.PolyType.ptClip);
                    skinClipper.Execute(ClipperLib.ClipType.ctUnion, result);
                }

                double minAreaSize = 3.0;//(2 * M_PI * (double(config.extrusionWidth) / 1000.0) * (double(config.extrusionWidth) / 1000.0)) * 3;
                for (int i = 0; i < result.Count; i++)
                {
                    double area = Math.Abs(Clipper.Area(result[i])) / 1000.0 / 1000.0;
                    if (area < minAreaSize) /* Only create an up/down skin if the area is large enough. So you do not create tiny blobs of "trying to fill" */
                    {
                        result.RemoveAt(i);
                        i -= 1;
                    }
                }

                Clipper sparseClipper = new Clipper();
                sparseClipper.AddPolygons(temp, ClipperLib.PolyType.ptSubject);
                sparseClipper.AddPolygons(result, ClipperLib.PolyType.ptClip);
                sparseClipper.Execute(ClipperLib.ClipType.ctDifference, part.sparseOutline);
            }
        }
    }
}

