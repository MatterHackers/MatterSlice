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

namespace MatterHackers.MatterSlice
{
	using Polygons = List<List<IntPoint>>;

	/*
	SliceData
	+ Layers[]
	  + LayerParts[]
		+ OutlinePolygons[]
		+ Insets[]
		  + Polygons[]
		+ SkinPolygons[]
	*/

	public class SliceLayerPart
	{
		public Aabb BoundingBox = new Aabb();
		public Polygons TotalOutline = new Polygons();
		public Polygons AvoidCrossingBoundery = new Polygons();
		public List<Polygons> Insets = new List<Polygons>();
		public Polygons SolidTopOutlines = new Polygons();
		public Polygons SolidBottomOutlines = new Polygons();
		public Polygons SolidInfillOutlines = new Polygons();
		public Polygons InfillOutlines = new Polygons();
	};

	public class SliceLayerParts
	{
		public long printZ;
		public List<SliceLayerPart> parts = new List<SliceLayerPart>();
	};

	public class SupportPoint
	{
		public int z;
		public double angleFromHorizon;

		public SupportPoint(int z, double angleFromHorizon)
		{
			this.z = z;
			this.angleFromHorizon = angleFromHorizon;
		}
	}

	public class SupportStorage
	{
		public bool generated;
		public int endAngleDegrees;
		public bool generateInternalSupport;
		public int supportXYDistance_um;
		public int supportLayerHeight_um;
		public int supportZGapLayers;
		public int supportInterfaceLayers;

		public IntPoint gridOffset;
		public int gridScale;
		public long gridWidth, gridHeight;
		public List<List<SupportPoint>> xYGridOfSupportPoints = new List<List<SupportPoint>>();

		private static void swap(ref int p0, ref int p1)
		{
			int tmp = p0;
			p0 = p1;
			p1 = tmp;
		}

		private static void swap(ref long p0, ref long p1)
		{
			long tmp = p0;
			p0 = p1;
			p1 = tmp;
		}

		private static void swap(ref Point3 p0, ref Point3 p1)
		{
			Point3 tmp = p0;
			p0 = p1;
			p1 = tmp;
		}

		private static int SortSupportsOnZ(SupportPoint one, SupportPoint two)
		{
			return one.z.CompareTo(two.z);
		}

		public void GenerateSupportGrid(OptimizedMeshCollection model, ConfigSettings config)
		{
			this.generated = false;
			if (config.supportEndAngle < 0)
			{
				return;
			}

			this.generated = true;

			this.gridOffset.X = model.minXYZ_um.x;
			this.gridOffset.Y = model.minXYZ_um.y;
			this.gridScale = 200;
			this.gridWidth = (model.size_um.x / this.gridScale) + 1;
			this.gridHeight = (model.size_um.y / this.gridScale) + 1;
			int gridSize = (int)(this.gridWidth * this.gridHeight);
			this.xYGridOfSupportPoints = new List<List<SupportPoint>>(gridSize);
			for (int i = 0; i < gridSize; i++)
			{
				this.xYGridOfSupportPoints.Add(new List<SupportPoint>());
			}

			this.endAngleDegrees = config.supportEndAngle;
			this.generateInternalSupport = config.generateInternalSupport;
			this.supportXYDistance_um = config.supportXYDistance_um;
			this.supportLayerHeight_um = config.layerThickness_um;
			this.supportZGapLayers = config.supportNumberOfLayersToSkipInZ;
			this.supportInterfaceLayers = config.supportInterfaceLayers;

			// This should really be a ray intersection as later code is going to count on it being an even odd list of bottoms and tops.
			// As it is we are finding the hit on the plane but not checking for good intersection with the triangle.
			for (int volumeIndex = 0; volumeIndex < model.OptimizedMeshes.Count; volumeIndex++)
			{
				OptimizedMesh vol = model.OptimizedMeshes[volumeIndex];
				for (int faceIndex = 0; faceIndex < vol.facesTriangle.Count; faceIndex++)
				{
					OptimizedFace faceTriangle = vol.facesTriangle[faceIndex];
					Point3 v0 = vol.vertices[faceTriangle.vertexIndex[0]].position;
					Point3 v1 = vol.vertices[faceTriangle.vertexIndex[1]].position;
					Point3 v2 = vol.vertices[faceTriangle.vertexIndex[2]].position;

					// get the angle of this polygon
					double angleFromHorizon;
					{
						FPoint3 v0f = new FPoint3(v0);
						FPoint3 v1f = new FPoint3(v1);
						FPoint3 v2f = new FPoint3(v2);
						FPoint3 normal = (v1f - v0f).Cross(v2f - v0f);
						normal.z = Math.Abs(normal.z);

						angleFromHorizon = (Math.PI / 2) - FPoint3.CalculateAngle(normal, FPoint3.Up);
					}

					v0.x = (int)((v0.x - this.gridOffset.X) / (double)this.gridScale + .5);
					v0.y = (int)((v0.y - this.gridOffset.Y) / (double)this.gridScale + .5);
					v1.x = (int)((v1.x - this.gridOffset.X) / (double)this.gridScale + .5);
					v1.y = (int)((v1.y - this.gridOffset.Y) / (double)this.gridScale + .5);
					v2.x = (int)((v2.x - this.gridOffset.X) / (double)this.gridScale + .5);
					v2.y = (int)((v2.y - this.gridOffset.Y) / (double)this.gridScale + .5);

					if (v0.x > v1.x) swap(ref v0, ref v1);
					if (v1.x > v2.x) swap(ref v1, ref v2);
					if (v0.x > v1.x) swap(ref v0, ref v1);
					for (long x = v0.x; x < v1.x; x++)
					{
						long y0 = (long)(v0.y + (v1.y - v0.y) * (x - v0.x) / (double)(v1.x - v0.x) + .5);
						long y1 = (long)(v0.y + (v2.y - v0.y) * (x - v0.x) / (double)(v2.x - v0.x) + .5);
						long z0 = (long)(v0.z + (v1.z - v0.z) * (x - v0.x) / (double)(v1.x - v0.x) + .5);
						long z1 = (long)(v0.z + (v2.z - v0.z) * (x - v0.x) / (double)(v2.x - v0.x) + .5);

						if (y0 > y1)
						{
							swap(ref y0, ref y1);
							swap(ref z0, ref z1);
						}

						for (long y = y0; y < y1; y++)
						{
							SupportPoint newSupportPoint = new SupportPoint((int)(z0 + (z1 - z0) * (y - y0) / (double)(y1 - y0) + .5), angleFromHorizon);
							this.xYGridOfSupportPoints[(int)(x + y * this.gridWidth)].Add(newSupportPoint);
						}
					}

					for (long x = v1.x; x < v2.x; x++)
					{
						long y0 = (long)(v1.y + (v2.y - v1.y) * (x - v1.x) / (double)(v2.x - v1.x) + .5);
						long y1 = (long)(v0.y + (v2.y - v0.y) * (x - v0.x) / (double)(v2.x - v0.x) + .5);
						long z0 = (long)(v1.z + (v2.z - v1.z) * (x - v1.x) / (double)(v2.x - v1.x) + .5);
						long z1 = (long)(v0.z + (v2.z - v0.z) * (x - v0.x) / (double)(v2.x - v0.x) + .5);

						if (y0 > y1)
						{
							swap(ref y0, ref y1);
							swap(ref z0, ref z1);
						}

						for (int y = (int)y0; y < y1; y++)
						{
							this.xYGridOfSupportPoints[(int)(x + y * this.gridWidth)].Add(new SupportPoint((int)(z0 + (z1 - z0) * (double)(y - y0) / (double)(y1 - y0) + .5), angleFromHorizon));
						}
					}
				}
			}

			for (int x = 0; x < this.gridWidth; x++)
			{
				for (int y = 0; y < this.gridHeight; y++)
				{
					int gridIndex = (int)(x + y * this.gridWidth);
					List<SupportPoint> currentList = this.xYGridOfSupportPoints[gridIndex];
					currentList.Sort(SortSupportsOnZ);

					if (currentList.Count > 1)
					{
						// now remove duplicates (try to make it a better bottom and top list)
						for (int i = currentList.Count - 1; i >= 1; i--)
						{
							if (currentList[i].z == currentList[i - 1].z)
							{
								currentList.RemoveAt(i);
							}
						}
					}
				}
			}
			this.gridOffset.X += this.gridScale / 2;
			this.gridOffset.Y += this.gridScale / 2;
		}
	}

	public class PartLayers
	{
		public List<SliceLayerParts> Layers = new List<SliceLayerParts>();
	}

	public class SliceDataStorage
	{
		public Point3 modelSize, modelMin, modelMax;
		public Polygons skirt = new Polygons();
		public Polygons raftOutline = new Polygons();
		public List<Polygons> wipeShield = new List<Polygons>();
		public List<PartLayers> AllPartsLayers = new List<PartLayers>();

		public SupportStorage support = new SupportStorage();
		public Polygons wipeTower = new Polygons();
		public IntPoint wipePoint;
	}
}