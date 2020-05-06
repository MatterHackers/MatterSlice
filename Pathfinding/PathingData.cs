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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.QuadTree;
using MSClipperLib;
using KdTree;
using static System.Math;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.Pathfinding
{
    /// <summary>
    /// This is to hold all the data that lets us switch between Boundary and Outline pathing.
    /// </summary>
    public class PathingData
	{
		private Affine polygonsToImageTransform;
		private double unitsPerPixel;
		private bool usingPathingCache;
		private IntRect polygonBounds;

		public PathingData(Polygons polygons, double unitsPerPixel, bool usingPathingCache)
		{
			this.usingPathingCache = usingPathingCache;

			Polygons = polygons;
			polygonBounds = Polygons.GetBounds();
			SetGoodUnitsPerPixel(unitsPerPixel);

			EdgeQuadTrees = Polygons.GetEdgeQuadTrees();
			PointKDTrees = Polygons.ConditionalKDTrees();

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

		private const int MaxImageSize = 4096;

		private void SetGoodUnitsPerPixel(double unitsPerPixel)
		{
			unitsPerPixel = Max(unitsPerPixel, 1);

			// if x or y are bigger than max Image Size, scale down
			if (polygonBounds.Width() / unitsPerPixel > MaxImageSize)
			{
				unitsPerPixel = Max(1, polygonBounds.Width() / MaxImageSize);
			}

			if (polygonBounds.Height() / unitsPerPixel > MaxImageSize)
			{
				unitsPerPixel = Max(1, polygonBounds.Height() / MaxImageSize);
			}

			// make sure that both axis have at least 32 pixels in them (may make one axis bigger than max image size)
			if (polygonBounds.Width() / unitsPerPixel < 32)
			{
				unitsPerPixel = polygonBounds.Width() / 32;
			}

			if (polygonBounds.Height() / unitsPerPixel < 32)
			{
				unitsPerPixel = polygonBounds.Height() / 32;
			}

			this.unitsPerPixel = Max(1, unitsPerPixel);
		}

		public List<QuadTree<int>> EdgeQuadTrees { get; }

		public ImageBuffer DistanceFromOutside { get; private set; }

		public List<KdTree<long, int>> PointKDTrees { get; }

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
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.MoveTo);
						first = false;
					}
					else
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.LineTo);
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

			for (int i = 0; i < distanceInPixels + distanceInPixels / 2; i++)
			{
				// check each direction to see if we can increase our InsetMap value
				double x = result.X;
				double y = result.Y;
				polygonsToImageTransform.transform(ref x, ref y);
				int xi = (int)Round(x);
				int yi = (int)Round(y);

				int current = GetInsetMapValue(xi, yi);
				if (current > distanceInPixels + distanceInPixels / 2)
				{
					// we've made it all the way inside
					return movedPoint;
				}

				var offset = default(IntPoint);
				movedPoint |= CheckInsetPixel(current, xi, -1, yi, +0, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, -1, yi, -1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, +0, yi, -1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, +1, yi, -1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, +1, yi, +0, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, +1, yi, +1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, +0, yi, +1, ref offset);
				movedPoint |= CheckInsetPixel(current, xi, -1, yi, +1, ref offset);

				if (offset.X < 0)
				{
					x -= 1;
				}
				else if (offset.X > 0)
				{
					x += 1;
				}

				if (offset.Y < 0)
				{
					y -= 1;
				}
				else if (offset.Y > 0)
				{
					y += 1;
				}

				// if we did not succeed at moving either point
				if (offset.X == 0 && offset.Y == 0)
				{
					return movedPoint;
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
			if (xi >= 0 && xi < DistanceFromOutside.Width
				&& yi >= 0 && yi < DistanceFromOutside.Height)
			{
				var buffer = DistanceFromOutside.GetBuffer();
				var offset = DistanceFromOutside.GetBufferOffsetXY(xi, yi);
				return buffer[offset];
			}

			return 0;
		}

		public QTPolygonsExtensions.InsideState PointIsInside(IntPoint testPoint)
		{
			if (!usingPathingCache)
			{
				if (Polygons.PointIsInside(testPoint, EdgeQuadTrees, PointKDTrees))
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

			int distanceFromOutside = 0;
			if (yi >= 0 && yi < DistanceFromOutside.Height
				&& xi >= 0 && xi < DistanceFromOutside.Width)
			{
				distanceFromOutside = DistanceFromOutside.GetBuffer()[DistanceFromOutside.GetBufferOffsetXY(xi, yi)];
			}

			if (distanceFromOutside == 0)
			{
				return QTPolygonsExtensions.InsideState.Outside;
			}
			else if (distanceFromOutside > 1)
			{
				return QTPolygonsExtensions.InsideState.Inside;
			}

			// The cache could not definitively tell us, so check the polygons
			if (Polygons.PointIsInside(testPoint, EdgeQuadTrees, PointKDTrees))
			{
				return QTPolygonsExtensions.InsideState.Inside;
			}

			return QTPolygonsExtensions.InsideState.Outside;
		}

		private void GenerateIsideCache()
		{
			int width = (int)Round(polygonBounds.Width() / unitsPerPixel);
			int height = (int)Round(polygonBounds.Height() / unitsPerPixel);

			DistanceFromOutside = new ImageBuffer(width + 4, height + 4, 8, new blender_gray(1));

			// Set the transform to image space
			polygonsToImageTransform = Affine.NewIdentity();
			// move it to 0, 0
			polygonsToImageTransform *= Affine.NewTranslation(-polygonBounds.minX, -polygonBounds.minY);
			// scale to fit cache
			polygonsToImageTransform *= Affine.NewScaling(width / (double)polygonBounds.Width(), height / (double)polygonBounds.Height());
			// and move it in 2 pixels
			polygonsToImageTransform *= Affine.NewTranslation(2, 2);

			// and render the polygon to the image
			DistanceFromOutside.NewGraphics2D().Render(new VertexSourceApplyTransform(CreatePathStorage(Polygons), polygonsToImageTransform), Color.White);

			// Now lets create an image that we can use to move points inside the outline
			// First create an image that is fully set on all color values of the original image
			DistanceFromOutside.DoThreshold(1);

			CalculateDistance(DistanceFromOutside);

			// var image32 = new ImageBuffer(DistanceFromOutside.Width, DistanceFromOutside.Height);
			// image32.NewGraphics2D().Render(DistanceFromOutside, 0, 0);

			// Agg.Platform.AggContext.ImageIO.SaveImageData("c:\\temp\\DistanceFromOutside.png", image32);
		}

		private void CalculateDistance(ImageBuffer image)
		{
			var maxDist = (image.Height + image.Width) > 255 ? 255 : (image.Height + image.Width);
			var buffer = image.GetBuffer();

			// O(n^2) solution to find the Manhattan distance to "on" pixels in a two dimension array
			// traverse from top left to bottom right
			Agg.Parallel.For(0, image.Height, (y) =>
			// for (int y = 0; y < image.Height; y++)
			{
				var yOffset = image.GetBufferOffsetY(y);
				for (int x = 0; x < image.Width; x++)
				{
					if (buffer[yOffset + x] == 0)
					{
						// first pass and pixel was off, it remains a zero
					}
					else
					{
						// pixel was on
						// It is at most the sum of the lengths of the array
						// away from a pixel that is off
						buffer[yOffset + x] = (byte)maxDist;
						// or one more than the pixel to the north
						if (x > 0)
						{
							var value = Math.Min(buffer[yOffset + x], buffer[yOffset + x - 1] + 1);
							buffer[yOffset + x] = (byte)value;
						}

						// or one more than the pixel to the west
						if (y > 0)
						{
							var value = Math.Min(buffer[yOffset + x], buffer[yOffset - image.Width + x] + 1);
							buffer[yOffset + x] = (byte)value;
						}
					}
				}
			});

			// traverse from bottom right to top left
			Agg.Parallel.For(0, image.Height, (y0ToHeight) =>
			// for (int y = image.Height - 1; y >= 0; y--)
			{
				var y = image.Height - y0ToHeight - 1;
				var yOffset = image.GetBufferOffsetY(y);
				for (int x = image.Width - 1; x >= 0; x--)
				{
					// either what we had on the first pass
					// or one more than the pixel to the south
					if (x + 1 < image.Width)
					{
						var value = Math.Min(buffer[yOffset + x], buffer[yOffset + x + 1] + 1);
						buffer[yOffset + x] = (byte)value;
					}

					// or one more than the pixel to the east
					if (y + 1 < image.Height)
					{
						var value = Math.Min(buffer[yOffset + x], buffer[yOffset + image.Width + x] + 1);
						buffer[yOffset + x] = (byte)value;
					}
				}
			});
		}
	}
}