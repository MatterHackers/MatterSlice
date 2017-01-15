/*
Copyright (c) 2014, Lars Brubaker
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
using MSClipperLib;

namespace MatterHackers.QuadTree
{
	using Polygon = List<IntPoint>;

	public enum Intersection { None, Colinear, Intersect }

	public static class PolygonExtensions
	{
		public static bool CalcIntersection(IntPoint a1, IntPoint a2,
													  IntPoint b1, IntPoint b2,
													  out IntPoint position)
		{
			position = new IntPoint();

			long intersection_epsilon = 1;
			long num = (a1.Y - b1.Y) * (b2.X - b1.X) - (a1.X - b1.X) * (b2.Y - b1.Y);
			long den = (a2.X - a1.X) * (b2.Y - b1.Y) - (a2.Y - a1.Y) * (b2.X - b1.X);
			if (Math.Abs(den) < intersection_epsilon)
			{
				return false;
			}

			position.X = a1.X + (a2.X - a1.X) * num / den;
			position.Y = a1.Y + (a2.Y - a1.Y) * num / den;

			return true;
		}

		// The main function that returns true if line segment 'startA-endA'
		// and 'startB-endB' intersect.
		public static bool DoIntersect(IntPoint startA, IntPoint endA, IntPoint startB, IntPoint endB)
		{
			return GetIntersection(startA, endA, startB, endB) != Intersection.None;
		}

		public static IEnumerable<Tuple<int, IntPoint>> FindCrossingPoints(this Polygon polygon, IntPoint start, IntPoint end, QuadTree<int> edgeQuadTree = null)
		{
			IntPoint segmentDelta = end - start;
			long segmentLength = segmentDelta.Length();
			var edgeIterator = new PolygonEdgeIterator(polygon, 1, edgeQuadTree);
			foreach (var i in edgeIterator.GetTouching(new Quad(start, end)))
			{
				IntPoint edgeStart = polygon[i];
				IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
				IntPoint intersection = new IntPoint();

				if (OnSegment(edgeStart, start, edgeEnd))
				{
					yield return new Tuple<int, IntPoint>(i, start);
				}
				else if (OnSegment(edgeStart, end, edgeEnd))
				{
					yield return new Tuple<int, IntPoint>(i, end);
				}
				else if (DoIntersect(start, end, edgeStart, edgeEnd)
					&& CalcIntersection(start, end, edgeStart, edgeEnd, out intersection))
				{
					IntPoint pointRelStart = intersection - start;
					yield return new Tuple<int, IntPoint>(i, intersection);
				}
			}
		}

		public static Intersection FindIntersection(this Polygon polygon, IntPoint start, IntPoint end, QuadTree<int> edgeQuadTree = null)
		{
			Intersection bestIntersection = Intersection.None;

			IntPoint segmentDelta = end - start;
			var edgeIterator = new PolygonEdgeIterator(polygon, 1, edgeQuadTree);
			foreach (var i in edgeIterator.GetTouching(new Quad(start, end)))
			{
				IntPoint edgeStart = polygon[i];

				// if we share a vertex we cannot be crossing the line
				IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
				if (start == edgeStart || start == edgeEnd || end == edgeStart || end == edgeEnd
					|| start == polygon[i] || end == polygon[i])
				{
					bestIntersection = Intersection.Colinear;
				}
				else
				{
					var result = GetIntersection(start, end, edgeStart, edgeEnd);
					if (result == Intersection.Intersect)
					{
						return Intersection.Intersect;
					}
					else if (result == Intersection.Colinear)
					{
						bestIntersection = Intersection.Colinear;
					}
				}
			}

			return bestIntersection;
		}

		/// <summary>
		/// Return the point index or -1 if not a vertex of the polygon
		/// </summary>
		/// <param name="polygon"></param>
		/// <param name="position"></param>
		/// <returns></returns>
		public static int FindPoint(this Polygon polygon, IntPoint position, QuadTree<int> pointQuadTree = null)
		{
			if (pointQuadTree != null)
			{
				foreach (var index in pointQuadTree.SearchPoint(position.X, position.Y))
				{
					if (position == polygon[index])
					{
						return index;
					}
				}
			}
			else
			{
				for (int i = 0; i < polygon.Count; i++)
				{
					if (position == polygon[i])
					{
						return i;
					}
				}
			}

			return -1;
		}

		public static QuadTree<int> GetEdgeQuadTree(this Polygon polygon, int splitCount = 5, long expandDist = 1)
		{
			var bounds = polygon.GetBounds();
			bounds.Inflate(expandDist);
			var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.maxY, bounds.maxX, bounds.minY);
			for (int i = 0; i < polygon.Count; i++)
			{
				var currentPoint = polygon[i];
				var nextPoint = polygon[i == polygon.Count - 1 ? 0 : i + 1];
				quadTree.Insert(i, new Quad(Math.Min(nextPoint.X, currentPoint.X) - expandDist,
					Math.Min(nextPoint.Y, currentPoint.Y) - expandDist,
					Math.Max(nextPoint.X, currentPoint.X) + expandDist,
					Math.Max(nextPoint.Y, currentPoint.Y) + expandDist));
			}

			return quadTree;
		}

		public static Intersection GetIntersection(IntPoint startA, IntPoint endA, IntPoint startB, IntPoint endB)
		{
			// Find the four orientations needed for general and
			// special cases
			int o1 = Orientation(startA, endA, startB);
			int o2 = Orientation(startA, endA, endB);
			int o3 = Orientation(startB, endB, startA);
			int o4 = Orientation(startB, endB, endA);

			// Special Cases
			// startA, endA and startB are collinear and startB lies on segment startA endA
			if (o1 == 0 && OnSegment(startA, startB, endA)) return Intersection.Colinear;

			// startA, endA and startB are collinear and endB lies on segment startA-endA
			if (o2 == 0 && OnSegment(startA, endB, endA)) return Intersection.Colinear;

			// startB, endB and startA are collinear and startA lies on segment startB-endB
			if (o3 == 0 && OnSegment(startB, startA, endB)) return Intersection.Colinear;

			// startB, endB and endA are collinear and endA lies on segment startB-endB
			if (o4 == 0 && OnSegment(startB, endA, endB)) return Intersection.Colinear;

			// General case
			if (o1 != o2 && o3 != o4)
			{
				return Intersection.Intersect;
			}

			return Intersection.None; // Doesn't fall in any of the above cases
		}

		public static QuadTree<int> GetPointQuadTree(this Polygon polygon, int splitCount = 5, long expandDist = 1)
		{
			var bounds = polygon.GetBounds();
			bounds.Inflate(expandDist);
			var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.maxY, bounds.maxX, bounds.minY);
			for (int i = 0; i < polygon.Count; i++)
			{
				quadTree.Insert(i, polygon[i].X - expandDist, polygon[i].Y - expandDist, polygon[i].X + expandDist, polygon[i].Y + expandDist);
			}

			return quadTree;
		}

		public static bool OnSegment(IntPoint start, IntPoint testPosition, IntPoint end)
		{
			if (start == end)
			{
				if (testPosition == start)
				{
					return true;
				}

				return false;
			}

			IntPoint segmentDelta = end - start;
			long segmentLength = segmentDelta.Length();
			IntPoint pointRelStart = testPosition - start;
			long distanceFromStart = segmentDelta.Dot(pointRelStart) / segmentLength;

			if (distanceFromStart >= 0 && distanceFromStart <= segmentLength)
			{
				IntPoint segmentDeltaLeft = segmentDelta.GetPerpendicularLeft();
				long distanceFromStartLeft = segmentDeltaLeft.Dot(pointRelStart) / segmentLength;

				if (distanceFromStartLeft == 0)
				{
					return true;
				}
			}

			return false;
		}

		// To find orientation of ordered triplet (p, q, r).
		// The function returns following values
		// 0 --> p, q and r are collinear
		// 1 --> Clockwise
		// 2 --> Counterclockwise
		public static int Orientation(IntPoint start, IntPoint end, IntPoint test)
		{
			// See http://www.geeksforgeeks.org/orientation-3-ordered-points/
			// for details of below formula.
			long val = (end.Y - start.Y) * (test.X - end.X) -
					  (end.X - start.X) * (test.Y - end.Y);

			if (val == 0)
			{
				return 0;
			}

			return (val > 0) ? 1 : 2; // clockwise or counterclockwise
		}

		//returns 0 if false, +1 if true, -1 if pt ON polygon boundary
		public static int PointIsInside(this Polygon polygon, IntPoint testPoint)
		{
			if (polygon.FindPoint(testPoint) != -1)
			{
				return -1;
			}
			return Clipper.PointInPolygon(testPoint, polygon);
		}

		public static bool SegmentTouching(this Polygon polygon, IntPoint start, IntPoint end)
		{
			IntPoint segmentDelta = end - start;
			IntPoint edgeStart = polygon[0];
			for (int i = 0; i < polygon.Count; i++)
			{
				IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
				if (DoIntersect(start, end, edgeStart, edgeEnd))
				{
					return true;
				}

				edgeStart = edgeEnd;
			}

			return false;
		}

		public static bool TouchingEdge(this Polygon polygon, IntPoint testPosition, QuadTree<int> edgeQuadTree = null)
		{
			var edgeIterator = new PolygonEdgeIterator(polygon, 1, edgeQuadTree);
			foreach (var i in edgeIterator.GetTouching(new Quad(testPosition)))
			{
				IntPoint edgeStart = polygon[i];
				IntPoint edgeEnd = polygon[(i + 1) % polygon.Count];
				if (OnSegment(edgeStart, testPosition, edgeEnd))
				{
					return true;
				}
			}

			return false;
		}
	}
}