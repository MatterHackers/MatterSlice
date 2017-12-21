/*
Copyright (c) 2015, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.Pathfinding
{
	using MatterHackers.Agg;
	using MatterHackers.Agg.Image;
	using MatterHackers.Agg.Transform;
	using MatterHackers.Agg.VertexSource;
	using QuadTree;
	using Polygons = List<List<IntPoint>>;
	using static System.Math;
	using MatterHackers.Agg.ImageProcessing;
	/// <summary>
	/// This is to hold all the data that lets us switch between Boundry and Outline pathing.
	/// </summary>
	public class PathingData
	{
		private Affine polygonsToImageTransform;
		private double unitsPerPixel;
		private bool usingPathingCache;
		IntRect polygonBounds;

		internal PathingData(Polygons polygons, double unitsPerPixel, bool usingPathingCache)
		{
			this.usingPathingCache = usingPathingCache;

			Polygons = polygons;
			polygonBounds = Polygons.GetBounds();
			SetGoodUnitsPerPixel(unitsPerPixel);

			EdgeQuadTrees = Polygons.GetEdgeQuadTrees();
			PointQuadTrees = Polygons.GetPointQuadTrees();

			foreach (var polygon in Polygons)
			{
				Waypoints.AddPolygon(polygon);
			}

			RemovePointList = new WayPointsToRemove(Waypoints);

			if (usingPathingCache)
			{
				GenerateIsideCache();
			}
		}

		const int maxImageSize = 4096;
		private void SetGoodUnitsPerPixel(double unitsPerPixel)
		{
			unitsPerPixel = Max(unitsPerPixel, 1);
			if (polygonBounds.Width() / unitsPerPixel > maxImageSize)
			{
				unitsPerPixel = Max(1, polygonBounds.Width() / maxImageSize);
			}
			if (polygonBounds.Height() / unitsPerPixel > maxImageSize)
			{
				unitsPerPixel = Max(1, polygonBounds.Height() / maxImageSize);
			}
			if (polygonBounds.Width() / unitsPerPixel < 32)
			{
				unitsPerPixel = polygonBounds.Width() / 32;
			}
			if (polygonBounds.Height() / unitsPerPixel > maxImageSize)
			{
				unitsPerPixel = polygonBounds.Height() / 32;
			}

			this.unitsPerPixel = Max(1, unitsPerPixel);
		}

		public List<QuadTree<int>> EdgeQuadTrees { get; }
		public ImageBuffer InsideCache { get; private set; }
		public ImageBuffer InsetMap { get; private set; }
		public List<QuadTree<int>> PointQuadTrees { get; }
		public Polygons Polygons { get; }
		public WayPointsToRemove RemovePointList { get; }
		public IntPointPathNetwork Waypoints { get; } = new IntPointPathNetwork();

		public static VertexStorage CreatePathStorage(List<List<IntPoint>> polygons)
		{
			VertexStorage output = new VertexStorage();

			foreach (List<IntPoint> polygon in polygons)
			{
				bool first = true;
				foreach (IntPoint point in polygon)
				{
					if (first)
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandMoveTo);
						first = false;
					}
					else
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandLineTo);
					}
				}

				output.ClosePolygon();
			}

			return output;
		}

		public bool MovePointAwayFromEdge(IntPoint testPoint, long distance, out IntPoint result)
		{
			int distanceInPixels = (int)Round(distance / unitsPerPixel);
			result = testPoint;
			bool movedPoint = false;

			for (int i = 0; i < distanceInPixels + distanceInPixels/2; i++)
			{
				// check each direction to see if we can increase our InsetMap value
				double x = result.X;
				double y = result.Y;
				polygonsToImageTransform.transform(ref x, ref y);
				int xi = (int)Round(x);
				int yi = (int)Round(y);

				int current = GetInsetMapValue(xi, yi);
				if(current == 255)
				{
					// we've made it all the way inside
				}

				var offset = new IntPoint();
				movedPoint |= CheckInsetPixel(current, xi, - 1, yi, + 0, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, - 1, yi, - 1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, + 0, yi, - 1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, + 1, yi, - 1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, + 1, yi, + 0, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, + 1, yi, + 1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, + 0, yi, + 1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, - 1, yi, + 1, ref offset);

				if (offset.X < 0) x -= 1; else if (offset.X > 0) x += 1;
				if (offset.Y < 0) y -= 1; else if (offset.Y > 0) y += 1;

				// if we did not succeed at moving either point
				if(x == testPoint.X && y == testPoint.Y)
				{
					x += 1;
				}
				polygonsToImageTransform.inverse_transform(ref x, ref y);
				result = new IntPoint(Round(x), Round(y));
			}

			return movedPoint;
		}

		private bool CheckInsetPixel(int current, int xi, int ox, int yi, int oy, ref IntPoint result)
		{
			int value = GetInsetMapValue(xi + ox, yi + oy);
			if (value > current)
			{
				result += new IntPoint(ox, oy);
				return true;
			}

			return false;
		}

		private int GetInsetMapValue(int xi, int yi)
		{
			if (xi >= 0 && xi < InsetMap.Width
				&& yi >= 0 && yi < InsetMap.Height)
			{
				var buffer = InsetMap.GetBuffer();
				var offset = InsetMap.GetBufferOffsetXY(xi, yi);
				return buffer[offset];
			}

			return 0;
		}

		public QTPolygonsExtensions.InsideState PointIsInside(IntPoint testPoint)
		{
			if (!usingPathingCache)
			{
				if (Polygons.PointIsInside(testPoint, EdgeQuadTrees, PointQuadTrees))
				{
					return QTPolygonsExtensions.InsideState.Inside;
				}

				return QTPolygonsExtensions.InsideState.Outside;
			}

			// translate the test point to the image coordinates
			double xd = testPoint.X;
			double yd = testPoint.Y;
			polygonsToImageTransform.transform(ref xd, ref yd);
			int xi = (int)Round(xd);
			int yi = (int)Round(yd);

			int pixelSum = 0;
			for (int offsetX = -1; offsetX <= 1; offsetX++)
			{
				for (int offsetY = -1; offsetY <= 1; offsetY++)
				{
					int x = xi + offsetX;
					int y = yi + offsetY;
					if (x >= 0 && x < InsideCache.Width
						&& y >= 0 && y < InsideCache.Height)
					{
						pixelSum += InsideCache.GetBuffer()[InsideCache.GetBufferOffsetXY(x, y)];
					}
				}
			}

			if (pixelSum == 0)
			{
				return QTPolygonsExtensions.InsideState.Outside;
			}
			else if (pixelSum / 9 == 255)
			{
				return QTPolygonsExtensions.InsideState.Inside;
			}

			// The cache could not definitively tell us, so check the polygons
			if (Polygons.PointIsInside(testPoint, EdgeQuadTrees, PointQuadTrees))
			{
				return QTPolygonsExtensions.InsideState.Inside;
			}

			return QTPolygonsExtensions.InsideState.Outside;
		}

		private void GenerateIsideCache()
		{
			int width = (int)Round(polygonBounds.Width() / unitsPerPixel);
			int height = (int)Round(polygonBounds.Height() / unitsPerPixel);

			InsideCache = new ImageBuffer(width + 4, height + 4, 8, new blender_gray(1));

			// Set the transform to image space
			polygonsToImageTransform = Affine.NewIdentity();
			// move it to 0, 0
			polygonsToImageTransform *= Affine.NewTranslation(-polygonBounds.minX, -polygonBounds.minY);
			// scale to fit cache
			polygonsToImageTransform *= Affine.NewScaling(width / (double)polygonBounds.Width(), height / (double)polygonBounds.Height());
			// and move it in 2 pixels
			polygonsToImageTransform *= Affine.NewTranslation(2, 2);

			// and render the polygon to the image
			InsideCache.NewGraphics2D().Render(new VertexSourceApplyTransform(CreatePathStorage(Polygons), polygonsToImageTransform), Color.White);

			// Now lets create an image that we can use to move points inside the outline
			// First create an image that is fully set on all color values of the original image
			InsetMap = new ImageBuffer(InsideCache);
			InsetMap.DoThreshold(1);
			// Then erode the image multiple times to get the a map of desired insets
			int count = 8;
			int step = 255/count;
			ImageBuffer last = InsetMap;
			for (int i = 0; i < count; i++)
			{
				var erode = new ImageBuffer(last);
				erode.DoErode3x3Binary(255);
				Paint(InsetMap, erode, (i + 1) * step);
				last = erode;
			}
		}

		private void Paint(ImageBuffer dest, ImageBuffer source, int level)
		{
			int height = source.Height;
			int width = source.Width;
			int sourceStrideInBytes = source.StrideInBytes();
			int destStrideInBytes = dest.StrideInBytes();
			byte[] sourceBuffer = source.GetBuffer();
			byte[] destBuffer = dest.GetBuffer();

			for (int y = 1; y < height - 1; y++)
			{
				int offset = source.GetBufferOffsetY(y);
				for (int x = 1; x < width - 1; x++)
				{
					if (destBuffer[offset] == 255 // the dest is white
						&& sourceBuffer[offset] == 0) // the dest is cleared
					{
						destBuffer[offset] = (byte)level;
					}
					offset++;
				}
			}
		}
	}
}