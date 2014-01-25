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
using System.IO;
using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public class SliceLayerPart
    {
        public AABB boundaryBox = new AABB();
        public Polygons outline = new Polygons();
        public Polygons combBoundery = new Polygons();
        public List<Polygons> insets = new List<Polygons>();
        public Polygons skinOutline = new Polygons();
        public Polygons sparseOutline = new Polygons();
        public double bridgeAngle;
    }

    public class SliceLayer
    {
        public List<SliceLayerPart> parts = new List<SliceLayerPart>();

        public static void createLayerWithParts(SliceLayer storageLayer, SlicerLayer layer, ConfigSettings.CorrectionType unionAllType)
        {
            Polygons polyList = new Polygons();
            for (int i = 0; i < layer.polygonList.Count; i++)
            {
                Polygon p = new Polygon();
                p.Add(layer.polygonList[i][0]);
                for (int j = 1; j < layer.polygonList[i].Count; j++)
                {
                    p.Add(layer.polygonList[i][j]);
                }

                bool isTypeB = (unionAllType & ConfigSettings.CorrectionType.FIX_HORRIBLE_UNION_ALL_TYPE_B) == ConfigSettings.CorrectionType.FIX_HORRIBLE_UNION_ALL_TYPE_B;
                if (isTypeB && Clipper.Orientation(p))
                {
                    p.Reverse();
                }
                polyList.Add(p);
            }

            PolyTree resultPolys = new PolyTree();
            Clipper clipper = new Clipper();
            clipper.AddPolygons(polyList, ClipperLib.PolyType.ptSubject);
            if (unionAllType != 0)
            {
                clipper.Execute( ClipperLib.ClipType.ctUnion, resultPolys, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);
            }
            else
            {
                clipper.Execute(ClipperLib.ClipType.ctUnion, resultPolys);
            }

            for (int i = 0; i < resultPolys.Total; i++)
            {
                storageLayer.parts.Add(new SliceLayerPart());
#if false // this is just a copy of what it was suposed to do
        storageLayer.parts[i].outline.push_back(resultPolys[i].outer);
        for(unsigned int j=0; j<resultPolys[i].holes.size(); j++)
        {
            storageLayer.parts[i].outline.push_back(resultPolys[i].holes[j]);
        }
        storageLayer.parts[i].boundaryBox.calculate(storageLayer.parts[i].outline);
#endif

                PolyNode node = resultPolys.GetFirst();
                storageLayer.parts[i].outline.Add(node.Contour);
                for (int j = 0; j < node.ChildCount; j++)
                {
                    if (node.Childs[j].IsHole)
                    {
                        storageLayer.parts[i].outline.Add(node.Childs[j].Contour);
                    }
                }

                storageLayer.parts[i].boundaryBox.calculate(storageLayer.parts[i].outline);
            }
        }

    }

    public class SupportPoint
    {
        public int z;
        public double cosAngle;

        public SupportPoint(int z, double cosAngle)
        {
            this.z = z;
            this.cosAngle = cosAngle;
        }
    }

    public class SupportStorage
    {
        public IntPoint gridOffset;
        public int gridScale;
        public int gridWidth, gridHeight;
        public List<SupportPoint> grid;

        public static void generateSupportGrid(SupportStorage storage, OptimizedModel om)
        {
            storage.gridOffset.X = om.vMin.x;
            storage.gridOffset.Y = om.vMin.y;
            storage.gridScale = 200;
            storage.gridWidth = (om.modelSize.x / storage.gridScale) + 1;
            storage.gridHeight = (om.modelSize.y / storage.gridScale) + 1;
            storage.grid = new List<SupportPoint>(storage.gridWidth * storage.gridHeight);

            for (int volumeIdx = 0; volumeIdx < om.volumes.Count; volumeIdx++)
            {
                OptimizedVolume vol = om.volumes[volumeIdx];
                for (int faceIdx = 0; faceIdx < vol.faces.Count; faceIdx++)
                {
                    OptimizedFace face = vol.faces[faceIdx];
                    Point3 v0 = vol.points[face.index[0]].point;
                    Point3 v1 = vol.points[face.index[1]].point;
                    Point3 v2 = vol.points[face.index[2]].point;

                    Point3 normal = Point3.cross((v1 - v0), (v2 - v0));
                    int normalSize = normal.Length();

                    double cosAngle = Math.Abs((double)(normal.z) / (double)(normalSize));

                    v0.x = (int)(v0.x - storage.gridOffset.X) / storage.gridScale;
                    v0.y = (int)(v0.y - storage.gridOffset.Y) / storage.gridScale;
                    v1.x = (int)(v1.x - storage.gridOffset.X) / storage.gridScale;
                    v1.y = (int)(v1.y - storage.gridOffset.Y) / storage.gridScale;
                    v2.x = (int)(v2.x - storage.gridOffset.X) / storage.gridScale;
                    v2.y = (int)(v2.y - storage.gridOffset.Y) / storage.gridScale;

                    if (v0.x > v1.x) swap(ref v0, ref v1);
                    if (v1.x > v2.x) swap(ref v1, ref v2);
                    if (v0.x > v1.x) swap(ref v0, ref v1);
                    for (long x = v0.x; x < v1.x; x++)
                    {
                        long y0 = v0.y + (v1.y - v0.y) * (x - v0.x) / (v1.x - v0.x);
                        long y1 = v0.y + (v2.y - v0.y) * (x - v0.x) / (v2.x - v0.x);
                        long z0 = v0.z + (v1.z - v0.z) * (x - v0.x) / (v1.x - v0.x);
                        long z1 = v0.z + (v2.z - v0.z) * (x - v0.x) / (v2.x - v0.x);

                        if (y0 > y1) 
                        {
                            swap(ref y0, ref y1); 
                            swap(ref z0, ref z1); 
                        }

                        for (long y = y0; y < y1; y++)
                        {
                            int index = (int)(x + y * storage.gridWidth);
                            throw new NotImplementedException();
#if false
                            storage.grid[index].Add(new SupportPoint(z0 + (z1 - z0) * (y - y0) / (y1 - y0), cosAngle));
#endif
                        }
                    }
                    for (long x = v1.x; x < v2.x; x++)
                    {
                        long y0 = v1.y + (v2.y - v1.y) * (x - v1.x) / (v2.x - v1.x);
                        long y1 = v0.y + (v2.y - v0.y) * (x - v0.x) / (v2.x - v0.x);
                        long z0 = v1.z + (v2.z - v1.z) * (x - v1.x) / (v2.x - v1.x);
                        long z1 = v0.z + (v2.z - v0.z) * (x - v0.x) / (v2.x - v0.x);

                        if (y0 > y1) { swap(ref y0, ref y1); swap(ref z0, ref z1); }
                        for (long y = y0; y < y1; y++)
                        {
                            int index = (int)(x + y * storage.gridWidth);
                            throw new NotImplementedException();
#if false
                            storage.grid[index].Add(new SupportPoint(z0 + (z1 - z0) * (y - y0) / (y1 - y0), cosAngle));
#endif
                        }
                    }
                }
            }

            for (int x = 0; x < storage.gridWidth; x++)
            {
                for (int y = 0; y < storage.gridHeight; y++)
                {
                    int n = x + y * storage.gridWidth;
                    throw new NotImplementedException();
                    //qsort(storage.grid[n].data(), storage.grid[n].size(), sizeof(SupportPoint), cmp_SupportPoint);
                }
            }
            storage.gridOffset.X += storage.gridScale / 2;
            storage.gridOffset.Y += storage.gridScale / 2;
        }

        private static void swap(ref long v0, ref long v1)
        {
            long tmp = v0;
            v0 = v1;
            v1 = tmp;
        }

        private static void swap(ref Point3 v0, ref Point3 v1)
        {
            Point3 tmp = v0;
            v0 = v1;
            v1 = tmp;
        }
    }

    public class SliceVolumeStorage
    {
        public List<SliceLayer> layers = new List<SliceLayer>();

        public static void createLayerParts(SliceVolumeStorage storage, Slicer slicer, ConfigSettings.CorrectionType unionAllType)
        {
            for (int layerNr = 0; layerNr < slicer.layers.Count; layerNr++)
            {
                storage.layers.Add(new SliceLayer());
                SliceLayer.createLayerWithParts(storage.layers[layerNr], slicer.layers[layerNr], unionAllType);
                //LayerPartsLayer(&slicer.layers[layerNr])
            }
        }
    }

    public class SliceDataStorage
    {
        public Point3 modelSize;
        public Point3 modelMin;
        public Point3 modelMax;

        public Polygons skirt = new Polygons();
        public Polygons raftOutline = new Polygons();
        public List<SliceVolumeStorage> volumes = new List<SliceVolumeStorage>();

        public SupportStorage support;


        public static void dumpLayerparts(SliceDataStorage storage, string filename)
        {
            using(StreamWriter writer = new StreamWriter(filename))
            {
                writer.Write("<!DOCTYPE html><html><body>");
                Point3 modelSize = storage.modelSize;
                Point3 modelMin = storage.modelMin;
    
                for(int volumeIdx=0; volumeIdx<storage.volumes.Count; volumeIdx++)
                {
                    for(int layerNr=0;layerNr<storage.volumes[volumeIdx].layers.Count; layerNr++)
                    {
                        writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style=\"width: 500px; height:500px\">\n");
                        SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
                        for(int i=0;i<layer.parts.Count;i++)
                        {
                            SliceLayerPart part = layer.parts[i];
                            
                            for(int j=0;j<part.outline.Count;j++)
                            {
                                writer.Write("<polygon points=\"");

                                for (int k = 0; k < part.outline[j].Count; k++)
                                {
                                    writer.Write(string.Format("{0},{1} ", (part.outline[j][k].X - modelMin.x) / modelSize.x * 500, (part.outline[j][k].Y - modelMin.y) / modelSize.y * 500));
                                }

                                if (j == 0)
                                {
                                    writer.Write("\" style=\"fill:gray; stroke:black;stroke-width:1\" />\n");
                                }
                                else
                                {
                                    writer.Write("\" style=\"fill:red; stroke:black;stroke-width:1\" />\n");
                                }
                            }
                        }
                        writer.Write("</svg>\n");
                    }
                }
                writer.Write("</body></html>");
            }
        }
    }
}