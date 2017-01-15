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
	using Polygons = List<List<IntPoint>>;

	public static class PolygonsExtensions
	{
		public static IEnumerable<Tuple<int, int, IntPoint>> FindCrossingPoints(this Polygons polygons, IntPoint start, IntPoint end, List<QuadTree<int>> edgeQuadTrees = null)
		{
			for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
			{
				List<Tuple<int, IntPoint>> polyCrossings = new List<Tuple<int, IntPoint>>();
				foreach (var crossing in polygons[polyIndex].FindCrossingPoints(start, end, edgeQuadTrees == null ? null : edgeQuadTrees[polyIndex]))
				{
					yield return new Tuple<int, int, IntPoint>(polyIndex, crossing.Item1, crossing.Item2);
				}
			}
		}

		public static Intersection FindIntersection(this Polygons polygons, IntPoint start, IntPoint end, List<QuadTree<int>> edgeQuadTrees = null)
		{
			Intersection bestIntersection = Intersection.None;
			for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
			{
				var result = polygons[polyIndex].FindIntersection(start, end, edgeQuadTrees == null ? null : edgeQuadTrees[polyIndex]);
				if (result == Intersection.Intersect)
				{
					return Intersection.Intersect;
				}
				else if (result == Intersection.Colinear)
				{
					bestIntersection = Intersection.Colinear;
				}
			}

			return bestIntersection;
		}

		public static Tuple<int, int> FindPoint(this Polygons polygons, IntPoint position, List<QuadTree<int>> pointQuadTrees = null)
		{
			if (pointQuadTrees != null)
			{
				for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
				{
					int pointIndex = polygons[polyIndex].FindPoint(position, pointQuadTrees[polyIndex]);
					if (pointIndex != -1)
					{
						return new Tuple<int, int>(polyIndex, pointIndex);
					}
				}
			}
			else
			{
				for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
				{
					int pointIndex = polygons[polyIndex].FindPoint(position);
					if (pointIndex != -1)
					{
						return new Tuple<int, int>(polyIndex, pointIndex);
					}
				}
			}

			return null;
		}

		public static List<QuadTree<int>> GetEdgeQuadTrees(this Polygons polygons, int splitCount = 5, long expandDist = 1)
		{
			var quadTrees = new List<QuadTree<int>>();
			foreach (var polygon in polygons)
			{
				quadTrees.Add(polygon.GetEdgeQuadTree(splitCount, expandDist));
			}

			return quadTrees;
		}

		public static List<QuadTree<int>> GetPointQuadTrees(this Polygons polygons, int splitCount = 5, long expandDist = 1)
		{
			var quadTrees = new List<QuadTree<int>>();
			foreach (var polygon in polygons)
			{
				quadTrees.Add(polygon.GetPointQuadTree(splitCount, expandDist));
			}

			return quadTrees;
		}

		public static void MovePointInsideBoundary(this Polygons boundaryPolygons, IntPoint startPosition, out Tuple<int, int, IntPoint> polyPointPosition, List<QuadTree<int>> edgeQuadTrees = null)
		{
			Tuple<int, int, IntPoint> bestPolyPointPosition = new Tuple<int, int, IntPoint>(0, 0, startPosition);

			if (boundaryPolygons.PointIsInside(startPosition, edgeQuadTrees))
			{
				// already inside
				polyPointPosition = null;
				return;
			}

			long bestDist = long.MaxValue;
			IntPoint bestMoveDelta = new IntPoint();
			for (int polygonIndex = 0; polygonIndex < boundaryPolygons.Count; polygonIndex++)
			{
				var boundaryPolygon = boundaryPolygons[polygonIndex];
				if (boundaryPolygon.Count < 3)
				{
					continue;
				}

				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
					IntPoint segmentStart = boundaryPolygon[pointIndex];

					IntPoint pointRelStart = startPosition - segmentStart;
					long distFromStart = pointRelStart.Length();
					if (distFromStart < bestDist)
					{
						bestDist = distFromStart;
						bestPolyPointPosition = new Tuple<int, int, IntPoint>(polygonIndex, pointIndex, segmentStart);
					}

					IntPoint segmentEnd = boundaryPolygon[(pointIndex + 1) % boundaryPolygon.Count];

					IntPoint segmentDelta = segmentEnd - segmentStart;
					long segmentLength = segmentDelta.Length();
					IntPoint segmentLeft = segmentDelta.GetPerpendicularLeft();
					long segmentLeftLength = segmentLeft.Length();

					long distanceFromStart = segmentDelta.Dot(pointRelStart) / segmentLength;

					if (distanceFromStart >= 0 && distanceFromStart <= segmentDelta.Length())
					{
						long distToBoundarySegment = segmentLeft.Dot(pointRelStart) / segmentLeftLength;

						if (Math.Abs(distToBoundarySegment) < bestDist)
						{
							IntPoint pointAlongCurrentSegment = startPosition;
							if (distToBoundarySegment != 0)
							{
								pointAlongCurrentSegment = startPosition - segmentLeft * distToBoundarySegment / segmentLeftLength;
							}

							bestDist = Math.Abs(distToBoundarySegment);
							bestPolyPointPosition = new Tuple<int, int, IntPoint>(polygonIndex, pointIndex, pointAlongCurrentSegment);
							bestMoveDelta = segmentLeft;
						}
					}

					segmentStart = segmentEnd;
				}
			}

			polyPointPosition = bestPolyPointPosition;
		}

		public static bool PointIsInside(this Polygons polygons, IntPoint testPoint, List<QuadTree<int>> edgeQuadTrees = null)
		{
			int insideCount = 0;
			foreach (var polygon in polygons)
			{
				if (polygons.TouchingEdge(testPoint, edgeQuadTrees))
				{
					insideCount++;
				}
				else
				{
					if (polygon.PointIsInside(testPoint) != 0)
					{
						insideCount++;
					}
				}
			}

			return (insideCount % 2 == 1);
		}

		public static IEnumerable<Tuple<int, int, IntPoint>> SkipSame(this IEnumerable<Tuple<int, int, IntPoint>> source)
		{
			Tuple<int, int, IntPoint> lastItem = new Tuple<int, int, IntPoint>(-1, -1, new IntPoint(long.MaxValue, long.MaxValue));
			foreach (var item in source)
			{
				if (item.Item3 != lastItem.Item3)
				{
					yield return item;
				}
				lastItem = item;
			}
		}

		public static bool TouchingEdge(this Polygons polygons, IntPoint testPosition, List<QuadTree<int>> edgeQuadTrees = null)
		{
			for (int i = 0; i < polygons.Count; i++)
			{
				if (polygons[i].TouchingEdge(testPosition, edgeQuadTrees == null ? null : edgeQuadTrees[i]))
				{
					return true;
				}
			}

			return false;
		}
	}
}