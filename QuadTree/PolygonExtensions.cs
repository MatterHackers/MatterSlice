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
	using Polygons = List<List<IntPoint>>;

	public enum Intersection { None, Colinear, Intersect }

	public static class QTPolygonExtensions
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

		public static IntPoint Center(this Polygon polygon)
		{
			IntPoint center = new IntPoint();
			for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
			{
				center += polygon[positionIndex];
			}

			center /= polygon.Count;
			return center;
		}

		// The main function that returns true if line segment 'startA-endA'
		// and 'startB-endB' intersect.
		public static bool DoIntersect(IntPoint startA, IntPoint endA, IntPoint startB, IntPoint endB)
		{
			return GetIntersection(startA, endA, startB, endB) != Intersection.None;
		}

		public static (int index, IntPoint position) FindClosestPoint(this Polygon polygon, IntPoint position, Func<int, IntPoint, bool> considerPoint = null)
		{
			var polyPointPosition = (-1, new IntPoint());

			long bestDist = long.MaxValue;
			for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
			{
				var point = polygon[pointIndex];
				long length = (point - position).Length();
				if (length < bestDist)
				{
					if (considerPoint == null || considerPoint(pointIndex, point))
					{
						bestDist = length;
						polyPointPosition = (pointIndex, point);
					}
				}
			}

			return polyPointPosition;
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
				pointQuadTree.SearchPoint(position.X, position.Y);
				foreach (var index in pointQuadTree.QueryResults)
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

		public static bool FindThinLines(this Polygon polygon, long overlapMergeAmount_um, long minimumRequiredWidth_um, out Polygons onlyMergeLines, bool pathIsClosed = true)
		{
			return QTPolygonsExtensions.FindThinLines(new Polygons { polygon }, overlapMergeAmount_um, minimumRequiredWidth_um, out onlyMergeLines, pathIsClosed);
		}

		public static QuadTree<int> GetEdgeQuadTree(this Polygon polygon, int splitCount = 5, long expandDist = 1)
		{
			var bounds = polygon.GetBounds();
			bounds.Inflate(expandDist);
			var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.minY, bounds.maxX, bounds.maxY);
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
			var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.minY, bounds.maxX, bounds.maxY);
			for (int i = 0; i < polygon.Count; i++)
			{
				quadTree.Insert(i, polygon[i].X - expandDist, polygon[i].Y - expandDist, polygon[i].X + expandDist, polygon[i].Y + expandDist);
			}

			return quadTree;
		}

		public static Polygon MakeCloseSegmentsMergable(this Polygon polygonToSplit, long distanceNeedingAdd, bool pathIsClosed = true)
		{
			return MakeCloseSegmentsMergable(polygonToSplit, polygonToSplit, distanceNeedingAdd, pathIsClosed);
		}

		public static Polygon MakeCloseSegmentsMergable(this Polygon polygonToSplit, Polygon pointsToSplitOn, long distanceNeedingAdd, bool pathIsClosed = true)
		{
			List<Segment> segments = Segment.ConvertToSegments(polygonToSplit, pathIsClosed);

			var touchingEnumerator = new PolygonEdgeIterator(pointsToSplitOn, distanceNeedingAdd);

			// for every segment
			for (int segmentIndex = segments.Count - 1; segmentIndex >= 0; segmentIndex--)
			{
				List<Segment> newSegments = segments[segmentIndex].GetSplitSegmentForVertecies(touchingEnumerator);
				if (newSegments?.Count > 0)
				{
					// remove the old segment
					segments.RemoveAt(segmentIndex);
					// add the new ones
					segments.InsertRange(segmentIndex, newSegments);
				}
			}

			Polygon segmentedPolygon = new Polygon(segments.Count);

			foreach (var segment in segments)
			{
				segmentedPolygon.Add(segment.Start);
			}

			if (!pathIsClosed)
			{
				// add the last point
				segmentedPolygon.Add(segments[segments.Count - 1].End);
			}

			return segmentedPolygon;
		}

		public static bool MergePerimeterOverlaps(this Polygon perimeter, long overlapMergeAmount_um, out Polygons separatedPolygons, bool pathIsClosed = true)
		{
			separatedPolygons = new Polygons();

			long cleanDistance_um = overlapMergeAmount_um / 40;

			Polygons cleanedPolygs = Clipper.CleanPolygons(new Polygons() { perimeter }, cleanDistance_um);
			perimeter = cleanedPolygs[0];

			if (perimeter.Count == 0)
			{
				return false;
			}
			bool pathWasOptomized = false;

			for (int i = 0; i < perimeter.Count; i++)
			{
				perimeter[i] = new IntPoint(perimeter[i])
				{
					Width = overlapMergeAmount_um
				};
			}

			perimeter = QTPolygonExtensions.MakeCloseSegmentsMergable(perimeter, overlapMergeAmount_um, pathIsClosed);

			// make a copy that has every point duplicated (so that we have them as segments).
			List<Segment> polySegments = Segment.ConvertToSegments(perimeter, pathIsClosed);

			Altered[] markedAltered = new Altered[polySegments.Count];

			var touchingEnumerator = new CloseSegmentsIterator(polySegments, overlapMergeAmount_um);
			int segmentCount = polySegments.Count;
			// now walk every segment and check if there is another segment that is similar enough to merge them together
			for (int firstSegmentIndex = 0; firstSegmentIndex < segmentCount; firstSegmentIndex++)
			{
				foreach (int checkSegmentIndex in touchingEnumerator.GetTouching(firstSegmentIndex, segmentCount))
				{
					// The first point of start and the last point of check (the path will be coming back on itself).
					long startDelta = (polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length();
					// if the segments are similar enough
					if (startDelta < overlapMergeAmount_um)
					{
						// The last point of start and the first point of check (the path will be coming back on itself).
						long endDelta = (polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length();
						if (endDelta < overlapMergeAmount_um)
						{
							// only considre the merge if the directions of the lines are towards eachother
							var firstSegmentDirection = polySegments[firstSegmentIndex].End - polySegments[firstSegmentIndex].Start;
							var checkSegmentDirection = polySegments[checkSegmentIndex].End - polySegments[checkSegmentIndex].Start;
							if (firstSegmentDirection.Dot(checkSegmentDirection) > 0)
							{
								continue;
							}
							pathWasOptomized = true;
							// move the first segments points to the average of the merge positions
							long startEndWidth = Math.Abs((polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length());
							long endStartWidth = Math.Abs((polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length());
							long width = Math.Min(startEndWidth, endStartWidth) + overlapMergeAmount_um;
							polySegments[firstSegmentIndex].Start = (polySegments[firstSegmentIndex].Start + polySegments[checkSegmentIndex].End) / 2; // the start
							polySegments[firstSegmentIndex].Start.Width = width;
							polySegments[firstSegmentIndex].End = (polySegments[firstSegmentIndex].End + polySegments[checkSegmentIndex].Start) / 2; // the end
							polySegments[firstSegmentIndex].End.Width = width;

							markedAltered[firstSegmentIndex] = Altered.merged;
							// mark this segment for removal
							markedAltered[checkSegmentIndex] = Altered.remove;
							// We only expect to find one match for each segment, so move on to the next segment
							break;
						}
					}
				}
			}

			// remove the marked segments
			for (int segmentIndex = segmentCount - 1; segmentIndex >= 0; segmentIndex--)
			{
				if (markedAltered[segmentIndex] == Altered.remove)
				{
					polySegments.RemoveAt(segmentIndex);
				}
			}

			// go through the polySegments and create a new polygon for every connected set of segments
			Polygon currentPolygon = new Polygon();
			separatedPolygons.Add(currentPolygon);
			// put in the first point
			for (int segmentIndex = 0; segmentIndex < polySegments.Count; segmentIndex++)
			{
				// add the start point
				currentPolygon.Add(polySegments[segmentIndex].Start);

				// if the next segment is not connected to this one
				if (segmentIndex < polySegments.Count - 1
					&& polySegments[segmentIndex].End != polySegments[segmentIndex + 1].Start)
				{
					// add the end point
					currentPolygon.Add(polySegments[segmentIndex].End);

					// create a new polygon
					currentPolygon = new Polygon();
					separatedPolygons.Add(currentPolygon);
				}
			}

			// add the end point
			currentPolygon.Add(polySegments[polySegments.Count - 1].End);

			return pathWasOptomized;
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
		public static int PointIsInside(this Polygon polygon, IntPoint testPoint, QuadTree<int> pointQuadTree = null)
		{
			if (polygon.FindPoint(testPoint, pointQuadTree) != -1)
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

		/// <summary>
		/// Return 1 if ccw -1 if cw
		/// </summary>
		/// <param name="polygon"></param>
		/// <returns></returns>
		public static int GetWindingDirection(this Polygon polygon)
		{
			int pointCount = polygon.Count;
			double totalTurns = 0;
			for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
			{
				int prevIndex = ((pointIndex + pointCount - 1) % pointCount);
				int nextIndex = ((pointIndex + 1) % pointCount);
				IntPoint prevPoint = polygon[prevIndex];
				IntPoint currentPoint = polygon[pointIndex];
				IntPoint nextPoint = polygon[nextIndex];

				double turnAmount = currentPoint.GetTurnAmount(prevPoint, nextPoint);

				totalTurns += turnAmount;
			}

			return totalTurns > 0 ? 1 : -1;
		}
	}
}