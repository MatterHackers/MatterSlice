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
	using System.Linq;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	[Flags]
	internal enum Altered
	{ remove = 1, merged = 2 };

	public static class QTPolygonsExtensions
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

		/// <summary>
		/// Create the list of polygon segments (not closed) that represent the parts of the source polygons that are close (almost touching).
		/// </summary>
		/// <param name="polygons"></param>
		/// <param name="overlapMergeAmount">If edges under consideration, are this distance or less appart (but greater than minimumRequiredWidth) they will generate edges</param>
		/// <param name="minimumRequiredWidth">If the distance between edges is less this they will not be generated. This lets us avoid considering very very thin lines.</param>
		/// <param name="onlyMergeLines">The output segments that are calculated</param>
		/// <param name="pathIsClosed">Is the source path closed (does not contain the last edge but assumes it).</param>
		/// <returns></returns>
		public static bool FindThinLines(this Polygons polygons, long overlapMergeAmount, long minimumRequiredWidth, out Polygons onlyMergeLines, bool pathIsClosed = true)
		{
			bool pathHasMergeLines = false;

			polygons = MakeCloseSegmentsMergable(polygons, overlapMergeAmount, pathIsClosed);

			// make a copy that has every point duplicated (so that we have them as segments).
			List<Segment> polySegments = Segment.ConvertToSegments(polygons);

			Altered[] markedAltered = new Altered[polySegments.Count];

			var touchingEnumerator = new CloseSegmentsIterator(polySegments, overlapMergeAmount);
			int segmentCount = polySegments.Count;
			// now walk every segment and check if there is another segment that is similar enough to merge them together
			for (int firstSegmentIndex = 0; firstSegmentIndex < segmentCount; firstSegmentIndex++)
			{
				foreach (int checkSegmentIndex in touchingEnumerator.GetTouching(firstSegmentIndex, segmentCount))
				{
					// The first point of start and the last point of check (the path will be coming back on itself).
					long startDelta = (polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length();
					// if the segments are similar enough
					if (startDelta < overlapMergeAmount)
					{
						// The last point of start and the first point of check (the path will be coming back on itself).
						long endDelta = (polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length();
						if (endDelta < overlapMergeAmount)
						{
							// move the first segments points to the average of the merge positions
							long startEndWidth = Math.Abs((polySegments[firstSegmentIndex].Start - polySegments[checkSegmentIndex].End).Length());
							long endStartWidth = Math.Abs((polySegments[firstSegmentIndex].End - polySegments[checkSegmentIndex].Start).Length());
							long width = Math.Min(startEndWidth, endStartWidth);

							if (width > minimumRequiredWidth)
							{
								// We need to check if the new start position is on the inside of the curve. We can only add thin lines on the insides of our exisiting curves.
								IntPoint newStartPosition = (polySegments[firstSegmentIndex].Start + polySegments[checkSegmentIndex].End) / 2; // the start;
								IntPoint newStartDirection = newStartPosition - polySegments[firstSegmentIndex].Start;
								IntPoint normalLeft = (polySegments[firstSegmentIndex].End - polySegments[firstSegmentIndex].Start).GetPerpendicularLeft();
								long dotProduct = normalLeft.Dot(newStartDirection);
								if (dotProduct > 0)
								{
									pathHasMergeLines = true;

									polySegments[firstSegmentIndex].Start = newStartPosition;
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
				}
			}

			// remove the marked segments
			for (int segmentIndex = segmentCount - 1; segmentIndex >= 0; segmentIndex--)
			{
				// remove every segment that has not been merged
				if (markedAltered[segmentIndex] != Altered.merged)
				{
					polySegments.RemoveAt(segmentIndex);
				}
			}

			// go through the polySegments and create a new polygon for every connected set of segments
			onlyMergeLines = new Polygons();
			Polygon currentPolygon = new Polygon();
			onlyMergeLines.Add(currentPolygon);
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
					onlyMergeLines.Add(currentPolygon);
				}
			}

			// add the end point
			if (polySegments.Count > 0)
			{
				currentPolygon.Add(polySegments[polySegments.Count - 1].End);
			}

			//long cleanDistance = overlapMergeAmount / 40;
			//Clipper.CleanPolygons(onlyMergeLines, cleanDistance);

			return pathHasMergeLines;
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

		public static IntPoint Center(this Polygons polygons)
		{
			IntPoint center = new IntPoint();
			int count = 0;
			foreach (var polygon in polygons)
			{
				for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
				{
					center += polygon[positionIndex];
					count++;
				}
			}

			if (count > 0)
			{
				center /= count;
			}
			return center;
		}

		public static Polygons MakeCloseSegmentsMergable(this Polygons polygonsToSplit, long distanceNeedingAdd, bool pathsAreClosed = true)
		{
			Polygons splitPolygons = new Polygons();
			for (int i = 0; i < polygonsToSplit.Count; i++)
			{
				Polygon accumulatedSplits = polygonsToSplit[i];
				for (int j = 0; j < polygonsToSplit.Count; j++)
				{
					accumulatedSplits = QTPolygonExtensions.MakeCloseSegmentsMergable(accumulatedSplits, polygonsToSplit[j], distanceNeedingAdd, pathsAreClosed);
				}
				splitPolygons.Add(accumulatedSplits);
			}

			return splitPolygons;
		}

		public static (int polyIndex, int pointIndex, IntPoint position) FindClosestPoint(this Polygons boundaryPolygons, IntPoint position, Func<int, Polygon, bool> considerPolygon = null, Func<int, IntPoint, bool> considerPoint = null)
		{
			var polyPointPosition = (-1, -1, new IntPoint());

			long bestDist = long.MaxValue;
			for (int polygonIndex = 0; polygonIndex < boundaryPolygons.Count; polygonIndex++)
			{
				if (considerPolygon == null || considerPolygon(polygonIndex, boundaryPolygons[polygonIndex]))
				{
					var closestToPoly = boundaryPolygons[polygonIndex].FindClosestPoint(position, considerPoint);
					if (closestToPoly.index != -1)
					{
						long length = (closestToPoly.Item2 - position).Length();
						if (length < bestDist)
						{
							bestDist = length;
							polyPointPosition = (polygonIndex, closestToPoly.Item1, closestToPoly.Item2);
						}
					}
				}
			}

			return polyPointPosition;
		}

		public static void MovePointInsideBoundary(this Polygons boundaryPolygons, IntPoint startPosition, out Tuple<int, int, IntPoint> polyPointPosition, 
			List<QuadTree<int>> edgeQuadTrees = null,
			List<QuadTree<int>> pointQuadTrees = null)
		{
			Tuple<int, int, IntPoint> bestPolyPointPosition = new Tuple<int, int, IntPoint>(0, 0, startPosition);

			if (boundaryPolygons.PointIsInside(startPosition, edgeQuadTrees, pointQuadTrees))
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

		public static bool PointIsInside(this Polygons polygons, IntPoint testPoint, List<QuadTree<int>> edgeQuadTrees = null, List<QuadTree<int>> pointQuadTrees = null)
		{
			if (polygons.TouchingEdge(testPoint, edgeQuadTrees))
			{
				return true;
			}

			int insideCount = 0;
			for (int i = 0; i < polygons.Count; i++)
			{
				var polygon = polygons[i];
				if (polygon.PointIsInside(testPoint, pointQuadTrees == null ? null : pointQuadTrees[i]) != 0)
				{
					insideCount++;
				}
			}

			return (insideCount % 2 == 1);
		}

		public static IEnumerable<Tuple<int, int, IntPoint>> SkipSame(this IEnumerable<Tuple<int, int, IntPoint>> source)
		{
			Tuple<int, int, IntPoint> lastItem = new Tuple<int, int, IntPoint>(-1, -1, new IntPoint(long.MaxValue, long.MaxValue));
			foreach (var item in source)
			{
				if (item.Item1 != -1)
				{
					if (item.Item3 != lastItem.Item3)
					{
						yield return item;
					}
					lastItem = item;
				}
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