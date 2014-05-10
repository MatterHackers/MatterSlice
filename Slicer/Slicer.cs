/*
Copyright (c) 2013 David Braam
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
using System.IO;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class SlicerSegment
    {
        public IntPoint start;
        public IntPoint end;
        public int faceIndex;
        public bool addedToPolygon;
    }

    public class closePolygonResult
    {
        //The result of trying to find a point on a closed polygon line. This gives back the point index, the polygon index, and the point of the connection.
        //The line on which the point lays is between pointIdx-1 and pointIdx
        public IntPoint intersectionPoint;
        public int polygonIdx;
        public int pointIdx;
    }

    public class gapCloserResult
    {
        public long len;
        public int polygonIdx;
        public int pointIdxA;
        public int pointIdxB;
        public bool AtoB;
    }

    public class Slicer
    {
        public List<SlicerLayer> layers = new List<SlicerLayer>();
        public Point3 modelSize;
        public Point3 modelMin;

        public Slicer(OptimizedVolume ov, int initialLayerThickness, int layerThickness, ConfigConstants.REPAIR_OUTLINES outlineRepairTypes)
        {
            modelSize = ov.model.size;
            modelMin = ov.model.minXYZ;

            int heightWithoutFirstLayer = modelSize.z - initialLayerThickness;
            int countOfNormalThicknessLayers = heightWithoutFirstLayer / layerThickness;
            
            int layerCount = countOfNormalThicknessLayers + 1; // we have to add in the first layer (that is a differnt size)
            LogOutput.log(string.Format("Layer count: {0}\n", layerCount));
            layers.Capacity = layerCount;
            for (int i = 0; i < layerCount; i++)
            {
                layers.Add(new SlicerLayer());
            }

            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                if (layerIndex == 0)
                {
                    layers[layerIndex].z = initialLayerThickness / 2;
                }
                else
                {
                    layers[layerIndex].z = initialLayerThickness + layerThickness / 2 + layerThickness * layerIndex;
                }
            }

            for (int faceIndex = 0; faceIndex < ov.facesTriangle.Count; faceIndex++)
            {
                Point3 p0 = ov.vertices[ov.facesTriangle[faceIndex].vertexIndex[0]].position;
                Point3 p1 = ov.vertices[ov.facesTriangle[faceIndex].vertexIndex[1]].position;
                Point3 p2 = ov.vertices[ov.facesTriangle[faceIndex].vertexIndex[2]].position;
                int minZ = p0.z;
                int maxZ = p0.z;
                if (p1.z < minZ) minZ = p1.z;
                if (p2.z < minZ) minZ = p2.z;
                if (p1.z > maxZ) maxZ = p1.z;
                if (p2.z > maxZ) maxZ = p2.z;

                for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                {
                    int z = layers[layerIndex].z;
                    if (z < minZ || layerIndex < 0)
                    {
                        continue;
                    }

                    SlicerSegment polyCrossingAtThisZ;
                    if (p0.z < z && p1.z >= z && p2.z >= z)
                    {
                        // p1   p2
                        // --------
                        //   p0
                        polyCrossingAtThisZ = getCrossingAtZ(p0, p2, p1, z);
                    }
                    else if (p0.z >= z && p1.z < z && p2.z < z)
                    {
                        //   p0
                        // --------
                        // p1  p2
                        polyCrossingAtThisZ = getCrossingAtZ(p0, p1, p2, z);
                    }
                    else if (p1.z < z && p0.z >= z && p2.z >= z)
                    {
                        // p0   p2
                        // --------
                        //   p1
                        polyCrossingAtThisZ = getCrossingAtZ(p1, p0, p2, z);
                    }
                    else if (p1.z >= z && p0.z < z && p2.z < z)
                    {
                        //   p1
                        // --------
                        // p0  p2
                        polyCrossingAtThisZ = getCrossingAtZ(p1, p2, p0, z);
                    }
                    else if (p2.z < z && p1.z >= z && p0.z >= z)
                    {
                        // p1   p0
                        // --------
                        //   p2
                        polyCrossingAtThisZ = getCrossingAtZ(p2, p1, p0, z);
                    }
                    else if (p2.z >= z && p1.z < z && p0.z < z)
                    {
                        //   p2
                        // --------
                        // p1  p0
                        polyCrossingAtThisZ = getCrossingAtZ(p2, p0, p1, z);
                    }
                    else
                    {
                        //Not all cases create a segment, because a point of a face could create just a dot, and two touching faces
                        //  on the slice would create two segments
                        continue;
                    }
                    layers[layerIndex].faceTo2DSegmentIndex[faceIndex] = layers[layerIndex].segmentList.Count;
                    polyCrossingAtThisZ.faceIndex = faceIndex;
                    polyCrossingAtThisZ.addedToPolygon = false;
                    layers[layerIndex].segmentList.Add(polyCrossingAtThisZ);
                }
            }

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                layers[layerIndex].makePolygons(ov, outlineRepairTypes);
            }
        }

        public SlicerSegment getCrossingAtZ(Point3 singlePointOnSide, Point3 otherSide1, Point3 otherSide2, int z)
        {
            SlicerSegment seg = new SlicerSegment();
            seg.start.X = singlePointOnSide.x + (long)(otherSide1.x - singlePointOnSide.x) * (long)(z - singlePointOnSide.z) / (long)(otherSide1.z - singlePointOnSide.z);
            seg.start.Y = singlePointOnSide.y + (long)(otherSide1.y - singlePointOnSide.y) * (long)(z - singlePointOnSide.z) / (long)(otherSide1.z - singlePointOnSide.z);
            seg.end.X = singlePointOnSide.x + (long)(otherSide2.x - singlePointOnSide.x) * (long)(z - singlePointOnSide.z) / (long)(otherSide2.z - singlePointOnSide.z);
            seg.end.Y = singlePointOnSide.y + (long)(otherSide2.y - singlePointOnSide.y) * (long)(z - singlePointOnSide.z) / (long)(otherSide2.z - singlePointOnSide.z);
            return seg;
        }

        public void DumpSegmentsToHTML(string filename)
        {
            float scale = Math.Max(modelSize.x, modelSize.y) / 1500;
            StreamWriter stream = new StreamWriter(filename);
            stream.Write("; some gcode to look at the layers");
            int extrudeAmount = 0;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                stream.Write("; LAYER:{0}\n".FormatWith(layerIndex));
                for (int j = 0; j < layers[layerIndex].polygonList.Count; j++)
                {
                    List<SlicerSegment> segmentList = layers[layerIndex].segmentList;
                    for (int segmentIndex = 0; segmentIndex < segmentList.Count; segmentIndex++)
                    {
                        stream.Write("G1 X{0}Y{1}\n", (float)(segmentList[segmentIndex].start.X - modelMin.x) / scale,
                            (float)(segmentList[segmentIndex].start.Y - modelMin.y) / scale);
                        stream.Write("G1 X{0}Y{1}E{2}\n", (float)(segmentList[segmentIndex].end.X - modelMin.x) / scale,
                            (float)(segmentList[segmentIndex].end.Y - modelMin.y) / scale,
                            extrudeAmount++);
                    }
                }
            }
            stream.Close();
        }

        public void DumpPolygonsToHTML(string filename)
        {
            float scale = Math.Max(modelSize.x, modelSize.y) / 1500;
            StreamWriter stream = new StreamWriter(filename);
            stream.Write("<!DOCTYPE html><html><body>\n");
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                stream.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style='width:%ipx;height:%ipx'>\n", (int)(modelSize.x / scale), (int)(modelSize.y / scale));
                stream.Write("<marker id='MidMarker' viewBox='0 0 10 10' refX='5' refY='5' markerUnits='strokeWidth' markerWidth='10' markerHeight='10' stroke='lightblue' stroke-width='2' fill='none' orient='auto'>");
                stream.Write("<path d='M 0 0 L 10 5 M 0 10 L 10 5'/>");
                stream.Write("</marker>");
                stream.Write("<g fill-rule='evenodd' style=\"fill: gray; stroke:black;stroke-width:1\">\n");
                stream.Write("<path marker-mid='url(#MidMarker)' d=\"");
                for (int polygonIndex = 0; polygonIndex < layers[layerIndex].polygonList.Count; polygonIndex++)
                {
                    Polygon polygon = layers[layerIndex].polygonList[polygonIndex];
                    for (int intPointIndex = 0; intPointIndex < polygon.Count; intPointIndex++)
                    {
                        if (intPointIndex == 0)
                        {
                            stream.Write("M");
                        }
                        else
                        {
                            stream.Write("L");
                        }
                        stream.Write("{0},{1} ", (float)(polygon[intPointIndex].X - modelMin.x) / scale, (float)(polygon[intPointIndex].Y - modelMin.y) / scale);
                    }
                    stream.Write("Z\n");
                }
                stream.Write("\"/>");
                stream.Write("</g>\n");
                for (int openPolygonIndex = 0; openPolygonIndex < layers[layerIndex].openPolygonList.Count; openPolygonIndex++)
                {
                    Polygon openPolygon = layers[layerIndex].openPolygonList[openPolygonIndex];
                    if (openPolygon.Count < 1) continue;
                    stream.Write("<polyline marker-mid='url(#MidMarker)' points=\"");
                    for (int n = 0; n < openPolygon.Count; n++)
                    {
                        stream.Write("{0},{1} ", (float)(openPolygon[n].X - modelMin.x) / scale, (float)(openPolygon[n].Y - modelMin.y) / scale);
                    }
                    stream.Write("\" style=\"fill: none; stroke:red;stroke-width:1\" />\n");
                }
                stream.Write("</svg>\n");
            }
            stream.Write("</body></html>");
            stream.Close();
        }
    }
}