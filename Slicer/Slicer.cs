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
        public bool hasBeenAddedToPolygon;
    }

    public class Slicer
    {
        public List<SlicerLayer> layers = new List<SlicerLayer>();
        public Point3 modelSize;
        public Point3 modelMin;

        public Slicer(OptimizedVolume ov, ConfigSettings config)
        {
            int initialLayerThickness_um = config.firstLayerThickness_um;
            int layerThickness_um = config.layerThickness_um;
            ConfigConstants.REPAIR_OUTLINES outlineRepairTypes = config.repairOutlines;

            modelSize = ov.parentModel.size_um;
            modelMin = ov.parentModel.minXYZ_um;

            int heightWithoutFirstLayer = modelSize.z - initialLayerThickness_um - config.bottomClipAmount_um;
            int countOfNormalThicknessLayers = (int)((heightWithoutFirstLayer / (double)layerThickness_um) + .5);

			int layerCount = countOfNormalThicknessLayers;
			if (initialLayerThickness_um > 0)
			{
				// we have to add in the first layer (that is a differnt size)
				layerCount++; 
			}

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
                    layers[layerIndex].z = initialLayerThickness_um / 2;
                }
                else
                {
                    layers[layerIndex].z = initialLayerThickness_um + layerThickness_um / 2 + layerThickness_um * (layerIndex-1);
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
                    if (z < minZ || z > maxZ)
                    {
                        continue;
                    }

                    SlicerSegment polyCrossingAtThisZ;
                    if (p0.z < z && p1.z >= z && p2.z >= z)
                    {
                        // p1   p2
                        // --------
                        //   p0
                        polyCrossingAtThisZ = GetCrossingAtZ(p0, p2, p1, z);
                    }
                    else if (p0.z >= z && p1.z < z && p2.z < z)
                    {
                        //   p0
                        // --------
                        // p1  p2
                        polyCrossingAtThisZ = GetCrossingAtZ(p0, p1, p2, z);
                    }
                    else if (p1.z < z && p0.z >= z && p2.z >= z)
                    {
                        // p0   p2
                        // --------
                        //   p1
                        polyCrossingAtThisZ = GetCrossingAtZ(p1, p0, p2, z);
                    }
                    else if (p1.z >= z && p0.z < z && p2.z < z)
                    {
                        //   p1
                        // --------
                        // p0  p2
                        polyCrossingAtThisZ = GetCrossingAtZ(p1, p2, p0, z);
                    }
                    else if (p2.z < z && p1.z >= z && p0.z >= z)
                    {
                        // p1   p0
                        // --------
                        //   p2
                        polyCrossingAtThisZ = GetCrossingAtZ(p2, p1, p0, z);
                    }
                    else if (p2.z >= z && p1.z < z && p0.z < z)
                    {
                        //   p2
                        // --------
                        // p1  p0
                        polyCrossingAtThisZ = GetCrossingAtZ(p2, p0, p1, z);
                    }
                    else
                    {
                        //Not all cases create a segment, because a point of a face could create just a dot, and two touching faces
                        //  on the slice would create two segments
                        continue;
                    }
                    layers[layerIndex].faceTo2DSegmentIndex[faceIndex] = layers[layerIndex].segmentList.Count;
                    polyCrossingAtThisZ.faceIndex = faceIndex;
                    polyCrossingAtThisZ.hasBeenAddedToPolygon = false;
                    layers[layerIndex].segmentList.Add(polyCrossingAtThisZ);
                }
            }

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                layers[layerIndex].MakePolygons(ov, outlineRepairTypes);
            }
        }

        public SlicerSegment GetCrossingAtZ(Point3 singlePointOnSide, Point3 otherSide1, Point3 otherSide2, int z)
        {
            SlicerSegment seg = new SlicerSegment();
            seg.start.X = (long)(singlePointOnSide.x + (double)(otherSide1.x - singlePointOnSide.x) * (double)(z - singlePointOnSide.z) / (double)(otherSide1.z - singlePointOnSide.z) + .5);
            seg.start.Y = (long)(singlePointOnSide.y + (double)(otherSide1.y - singlePointOnSide.y) * (double)(z - singlePointOnSide.z) / (double)(otherSide1.z - singlePointOnSide.z) + .5);
            seg.end.X = (long)(singlePointOnSide.x + (double)(otherSide2.x - singlePointOnSide.x) * (double)(z - singlePointOnSide.z) / (double)(otherSide2.z - singlePointOnSide.z) + .5);
            seg.end.Y = (long)(singlePointOnSide.y + (double)(otherSide2.y - singlePointOnSide.y) * (double)(z - singlePointOnSide.z) / (double)(otherSide2.z - singlePointOnSide.z) + .5);
            return seg;
        }

        readonly double scaleDenominator = 150;
        public void DumpSegmentsToGcode(string filename)
        {
            double scale = Math.Max(modelSize.x, modelSize.y) / scaleDenominator;
            StreamWriter stream = new StreamWriter(filename);
            stream.Write("; some gcode to look at the layer segments");
            int extrudeAmount = 0;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                stream.Write("; LAYER:{0}\n".FormatWith(layerIndex));
                List<SlicerSegment> segmentList = layers[layerIndex].segmentList;
                for (int segmentIndex = 0; segmentIndex < segmentList.Count; segmentIndex++)
                {
                    stream.Write("G1 X{0}Y{1}\n", (double)(segmentList[segmentIndex].start.X - modelMin.x) / scale,
                        (double)(segmentList[segmentIndex].start.Y - modelMin.y) / scale);
                    stream.Write("G1 X{0}Y{1}E{2}\n", (double)(segmentList[segmentIndex].end.X - modelMin.x) / scale,
                        (double)(segmentList[segmentIndex].end.Y - modelMin.y) / scale,
                        extrudeAmount++);
                }
            }
            stream.Close();
        }

        public void DumpPolygonsToGcode(string filename)
        {
            double scale = Math.Max(modelSize.x, modelSize.y) / scaleDenominator;
            StreamWriter stream = new StreamWriter(filename);
            stream.Write("; some gcode to look at the layer polygons");
            int extrudeAmount = 0;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                stream.Write("; LAYER:{0}\n".FormatWith(layerIndex));
                for (int polygonIndex = 0; polygonIndex < layers[layerIndex].polygonList.Count; polygonIndex++)
                {
                    Polygon polygon = layers[layerIndex].polygonList[polygonIndex];
                    if (polygon.Count > 0)
                    {
                        // move to the start without extruding (so it is a move)
                        stream.Write("G1 X{0}Y{1}\n", (double)(polygon[0].X - modelMin.x) / scale,
                            (double)(polygon[0].Y - modelMin.y) / scale);
                        for (int intPointIndex = 1; intPointIndex < polygon.Count; intPointIndex++)
                        {
                            // do all the points with extruding
                            stream.Write("G1 X{0}Y{1}E{2}\n", (double)(polygon[intPointIndex].X - modelMin.x) / scale,
                                (double)(polygon[intPointIndex].Y - modelMin.y) / scale, extrudeAmount++);
                        }
                        // go back to the start extruding
                        stream.Write("G1 X{0}Y{1}E{2}\n", (double)(polygon[0].X - modelMin.x) / scale,
                            (double)(polygon[0].Y - modelMin.y) / scale, extrudeAmount++);
                    }
                }

                for (int openPolygonIndex = 0; openPolygonIndex < layers[layerIndex].openPolygonList.Count; openPolygonIndex++)
                {
                    Polygon openPolygon = layers[layerIndex].openPolygonList[openPolygonIndex];

                    if (openPolygon.Count > 0)
                    {
                        // move to the start without extruding (so it is a move)
                        stream.Write("G1 X{0}Y{1}\n", (double)(openPolygon[0].X - modelMin.x) / scale,
                            (double)(openPolygon[0].Y - modelMin.y) / scale);
                        for (int intPointIndex = 1; intPointIndex < openPolygon.Count; intPointIndex++)
                        {
                            // do all the points with extruding
                            stream.Write("G1 X{0}Y{1}E{2}\n", (double)(openPolygon[intPointIndex].X - modelMin.x) / scale,
                                (double)(openPolygon[intPointIndex].Y - modelMin.y) / scale, extrudeAmount++);
                        }
                        // go back to the start extruding
                        stream.Write("G1 X{0}Y{1}E{2}\n", (double)(openPolygon[0].X - modelMin.x) / scale,
                            (double)(openPolygon[0].Y - modelMin.y) / scale, extrudeAmount++);
                    }
                }
            }
            stream.Close();
        }

        public void DumpPolygonsToHTML(string filename)
        {
            double scale = Math.Max(modelSize.x, modelSize.y) / scaleDenominator;
            StreamWriter stream = new StreamWriter(filename);
            stream.Write("<!DOCTYPE html><html><body>\n");
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                stream.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style='width:{0}px;height:{1}px'>\n".FormatWith((int)(modelSize.x / scale), (int)(modelSize.y / scale)));
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
                        stream.Write("{0},{1} ", (double)(polygon[intPointIndex].X - modelMin.x) / scale, (double)(polygon[intPointIndex].Y - modelMin.y) / scale);
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
                        stream.Write("{0},{1} ", (double)(openPolygon[n].X - modelMin.x) / scale, (double)(openPolygon[n].Y - modelMin.y) / scale);
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