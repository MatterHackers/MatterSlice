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
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Point = IntPoint;
    using Polygon = List<IntPoint>;
    using Polygons = List<Polygon>;
    using PolygonRef = Polygon;

    public static class LayerPart
    {
        /*
        The layer-part creation step is the first step in creating actual useful data for 3D printing.
        It takes the result of the Slice step, which is an unordered list of polygons, and makes groups of polygons,
        each of these groups is called a "part", which sometimes are also known as "islands". These parts represent
        isolated areas in the 2D layer with possible holes.

        Creating "parts" is an important step, as all elements in a single part should be printed before going to another part.
        And all every bit inside a single part can be printed without the nozzle leaving the boundery of this part.

        It's also the first step that stores the result in the "data storage" so all other steps can access it.
        */


        public static void createLayerWithParts(SliceLayer storageLayer, SlicerLayer layer, int unionAllType)
        {
            if ((unionAllType & ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_B) == ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_B)
            {
                for (int i = 0; i < layer.polygonList.Count; i++)
                {
                    if (layer.polygonList[i].orientation())
                        layer.polygonList[i].reverse();
                }
            }

            List<Polygons> result;
            if ((unionAllType & ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_C) == ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_C)
                result = layer.polygonList.offset(1000).splitIntoParts(unionAllType);
            else
                result = layer.polygonList.splitIntoParts(unionAllType);

            for (int i = 0; i < result.Count; i++)
            {
                storageLayer.parts.Add(new SliceLayerPart());
                if ((unionAllType & ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_C) == ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_C)
                {
                    storageLayer.parts[i].outline.add(result[i][0]);
                    storageLayer.parts[i].outline = storageLayer.parts[i].outline.offset(-1000);
                }
                else
                    storageLayer.parts[i].outline = result[i];
                storageLayer.parts[i].boundaryBox.calculate(storageLayer.parts[i].outline);
            }
        }

        public static void createLayerParts(SliceVolumeStorage storage, Slicer slicer, int unionAllType)
        {
            for (int layerNr = 0; layerNr < slicer.layers.Count; layerNr++)
            {
                storage.layers.Add(new SliceLayer());
                LayerPart.createLayerWithParts(storage.layers[layerNr], slicer.layers[layerNr], unionAllType);
            }
        }

        public static void dumpLayerparts(SliceDataStorage storage, string filename)
        {
            StreamWriter f = new StreamWriter(filename);
            f.Write("<!DOCTYPE html><html><body>");
            Point3 modelSize = storage.modelSize;
            Point3 modelMin = storage.modelMin;

            for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
            {
                for (int layerNr = 0; layerNr < storage.volumes[volumeIdx].layers.Count; layerNr++)
                {
                    f.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style=\"width: 500px; height:500px\">\n");
                    SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
                    for (int i = 0; i < layer.parts.Count; i++)
                    {
                        SliceLayerPart part = layer.parts[i];
                        for (int j = 0; j < part.outline.Count; j++)
                        {
                            f.Write("<polygon points=\"");
                            for (int k = 0; k < part.outline[j].Count; k++)
                                f.Write("%f,%f ", (float)(part.outline[j][k].X - modelMin.x) / modelSize.x * 500, (float)(part.outline[j][k].Y - modelMin.y) / modelSize.y * 500);
                            if (j == 0)
                                f.Write("\" style=\"fill:gray; stroke:black;stroke-width:1\" />\n");
                            else
                                f.Write("\" style=\"fill:red; stroke:black;stroke-width:1\" />\n");
                        }
                    }
                    f.Write("</svg>\n");
                }
            }
            f.Write("</body></html>");
            f.Close();
        }

    }
}