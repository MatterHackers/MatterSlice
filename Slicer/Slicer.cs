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

using MatterSlice.ClipperLib;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;

	public class SlicePerimeterSegment
	{
		public IntPoint start;
		public IntPoint end;
		public bool hasBeenAddedToPolygon;

		public SlicePerimeterSegment()
		{
		}

		public SlicePerimeterSegment(IntPoint start, IntPoint end)
		{
			this.start = start;
			this.end = end;
		}
	}

	public class Slicer
	{
		public List<MeshProcessingLayer> layers = new List<MeshProcessingLayer>();
		public Point3 modelSize;
		public Point3 modelMin;

		public Slicer(OptimizedMesh ov, ConfigSettings config)
		{
			int initialLayerThickness_um = config.firstLayerThickness_um;
			int layerThickness_um = config.layerThickness_um;

			modelSize = ov.containingCollection.size_um;
			modelMin = ov.containingCollection.minXYZ_um;

			long heightWithoutFirstLayer = modelSize.z - initialLayerThickness_um - config.bottomClipAmount_um;
			int countOfNormalThicknessLayers = Math.Max(0, (int)((heightWithoutFirstLayer / (double)layerThickness_um) + .5));

			int layerCount = countOfNormalThicknessLayers;
			if (initialLayerThickness_um > 0)
			{
				// we have to add in the first layer (that is a differnt size)
				layerCount++;
			}

			LogOutput.Log(string.Format("Layer count: {0}\n", layerCount));
			layers.Capacity = layerCount;
			for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			{
				int z;
				if (layerIndex == 0)
				{
					z = initialLayerThickness_um / 2;
				}
				else
				{
					z = initialLayerThickness_um + layerThickness_um / 2 + layerThickness_um * (layerIndex - 1);
				}
				layers.Add(new MeshProcessingLayer(z));
			}

			for (int faceIndex = 0; faceIndex < ov.facesTriangle.Count; faceIndex++)
			{
				Point3 p0 = ov.vertices[ov.facesTriangle[faceIndex].vertexIndex[0]].position;
				Point3 p1 = ov.vertices[ov.facesTriangle[faceIndex].vertexIndex[1]].position;
				Point3 p2 = ov.vertices[ov.facesTriangle[faceIndex].vertexIndex[2]].position;
				long minZ = p0.z;
				long maxZ = p0.z;
				if (p1.z < minZ) minZ = p1.z;
				if (p2.z < minZ) minZ = p2.z;
				if (p1.z > maxZ) maxZ = p1.z;
				if (p2.z > maxZ) maxZ = p2.z;

				for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
				{
					int z = layers[layerIndex].Z;
					if (z < minZ || z > maxZ)
					{
						continue;
					}

					SlicePerimeterSegment polyCrossingAtThisZ;
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

					polyCrossingAtThisZ.hasBeenAddedToPolygon = false;
					layers[layerIndex].SegmentList.Add(polyCrossingAtThisZ);
				}
			}

			for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
			{
				layers[layerIndex].MakePolygons();
			}
		}

		public SlicePerimeterSegment GetCrossingAtZ(Point3 singlePointOnSide, Point3 otherSide1, Point3 otherSide2, int z)
		{
			SlicePerimeterSegment seg = new SlicePerimeterSegment();
			seg.start.X = (long)(singlePointOnSide.x + (double)(otherSide1.x - singlePointOnSide.x) * (double)(z - singlePointOnSide.z) / (double)(otherSide1.z - singlePointOnSide.z) + .5);
			seg.start.Y = (long)(singlePointOnSide.y + (double)(otherSide1.y - singlePointOnSide.y) * (double)(z - singlePointOnSide.z) / (double)(otherSide1.z - singlePointOnSide.z) + .5);
			seg.end.X = (long)(singlePointOnSide.x + (double)(otherSide2.x - singlePointOnSide.x) * (double)(z - singlePointOnSide.z) / (double)(otherSide2.z - singlePointOnSide.z) + .5);
			seg.end.Y = (long)(singlePointOnSide.y + (double)(otherSide2.y - singlePointOnSide.y) * (double)(z - singlePointOnSide.z) / (double)(otherSide2.z - singlePointOnSide.z) + .5);
			return seg;
		}

		public void DumpSegmentsToGcode(string filename)
		{
			double scale = 1000;
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer segments");
			int extrudeAmount = 0;
			for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
			{
				stream.Write("; LAYER:{0}\n".FormatWith(layerIndex));
				List<SlicePerimeterSegment> segmentList = layers[layerIndex].SegmentList;
				for (int segmentIndex = 0; segmentIndex < segmentList.Count; segmentIndex++)
				{
					stream.Write("G1 X{0}Y{1}\n", (double)(segmentList[segmentIndex].start.X) / scale,
						(double)(segmentList[segmentIndex].start.Y) / scale);
					stream.Write("G1 X{0}Y{1}E{2}\n", (double)(segmentList[segmentIndex].end.X) / scale,
						(double)(segmentList[segmentIndex].end.Y) / scale,
						extrudeAmount++);
				}
			}
			stream.Close();
		}

		public void DumpPolygonsToGcode(string filename)
		{
			double scale = 1000;
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer polygons");
			int extrudeAmount = 0;
			for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
			{
				stream.Write("; LAYER:{0}\n".FormatWith(layerIndex));
				for (int polygonIndex = 0; polygonIndex < layers[layerIndex].PolygonList.Count; polygonIndex++)
				{
					Polygon polygon = layers[layerIndex].PolygonList[polygonIndex];
					if (polygon.Count > 0)
					{
						// move to the start without extruding (so it is a move)
						stream.Write("G1 X{0}Y{1}\n", (double)(polygon[0].X) / scale,
							(double)(polygon[0].Y) / scale);
						for (int intPointIndex = 1; intPointIndex < polygon.Count; intPointIndex++)
						{
							// do all the points with extruding
							stream.Write("G1 X{0}Y{1}E{2}\n", (double)(polygon[intPointIndex].X) / scale,
								(double)(polygon[intPointIndex].Y) / scale, extrudeAmount++);
						}
						// go back to the start extruding
						stream.Write("G1 X{0}Y{1}E{2}\n", (double)(polygon[0].X) / scale,
							(double)(polygon[0].Y) / scale, extrudeAmount++);
					}
				}

				layers[layerIndex].DumpPolygonsToGcode(stream, scale, extrudeAmount);
			}
			stream.Close();
		}
	}
}