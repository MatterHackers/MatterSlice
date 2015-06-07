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
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public class AvoidCrossingPerimeters
	{
		private Polygons bounderyPolygons;

		private int[] indexOfMaxX;
		private int[] indexOfMinX;
		private PointMatrix matrix;
		private long[] maxXPosition;
		private long[] minXPosition;
		private IntPoint nomalizedStartPoint;
		private IntPoint normalizedEndPoint;

		public AvoidCrossingPerimeters(Polygons bounderyPolygons)
		{
			this.bounderyPolygons = bounderyPolygons;
			minXPosition = new long[bounderyPolygons.Count];
			maxXPosition = new long[bounderyPolygons.Count];
			indexOfMinX = new int[bounderyPolygons.Count];
			indexOfMaxX = new int[bounderyPolygons.Count];
		}

		bool saveDebugData = false;
		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, List<IntPoint> pathThatIsInside)
		{
			if (saveDebugData)
			{
				string boundryPolygons = bounderyPolygons.WriteToString();
			}

			if ((endPoint - startPoint).ShorterThen(1500))
			{
				// If the movement is very short (not a lot of time to ooze filament)
				// then don't add any points
				return true;
			}

			bool addEndpoint = false;
			//Check if we are inside the comb boundaries
			if (!PointIsInsideBoundary(startPoint))
			{
				if (!MovePointInsideBoundary(ref startPoint))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				pathThatIsInside.Add(startPoint);
			}

			if (!PointIsInsideBoundary(endPoint))
			{
				if (!MovePointInsideBoundary(ref endPoint))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				addEndpoint = true;
			}

			// Check if we are crossing any bounderies, and pre-calculate some values.
			if (!DoesLineCrossBoundery(startPoint, endPoint))
			{
				//We're not crossing any boundaries. So skip the comb generation.
				if (!addEndpoint && pathThatIsInside.Count == 0)
				{
					//Only skip if we didn't move the start and end point.
					return true;
				}
			}

			// Calculate the minimum and maximum positions where we cross the comb boundary
			CalcMinMax();

			long nomalizedStartX = nomalizedStartPoint.X;
			List<IntPoint> pointList = new List<IntPoint>();
			// Now walk trough the crossings, for every boundary we cross, find the initial cross point and the exit point.
			// Then add all the points in between to the pointList and continue with the next boundary we will cross,
			// until there are no more boundaries to cross.
			// This gives a path from the start to finish curved around the holes that it encounters.
			while (true)
			{
				// if we go up enough we should run into the boundry
				int abovePolyIndex = GetPolygonIndexAbove(nomalizedStartX);
				if (abovePolyIndex < 0)
				{
					break;
				}

				pointList.Add(matrix.unapply(new IntPoint(minXPosition[abovePolyIndex] - 200, nomalizedStartPoint.Y)));
				if ((indexOfMinX[abovePolyIndex] - indexOfMaxX[abovePolyIndex] + bounderyPolygons[abovePolyIndex].Count) % bounderyPolygons[abovePolyIndex].Count > (indexOfMaxX[abovePolyIndex] - indexOfMinX[abovePolyIndex] + bounderyPolygons[abovePolyIndex].Count) % bounderyPolygons[abovePolyIndex].Count)
				{
					for (int i = indexOfMinX[abovePolyIndex]; i != indexOfMaxX[abovePolyIndex]; i = (i < bounderyPolygons[abovePolyIndex].Count - 1) ? (i + 1) : (0))
					{
						pointList.Add(GetBounderyPointWithOffset(abovePolyIndex, i));
					}
				}
				else
				{
					indexOfMinX[abovePolyIndex]--;
					if (indexOfMinX[abovePolyIndex] == -1)
					{
						indexOfMinX[abovePolyIndex] = bounderyPolygons[abovePolyIndex].Count - 1;
					}

					indexOfMaxX[abovePolyIndex]--;
					if (indexOfMaxX[abovePolyIndex] == -1)
					{
						indexOfMaxX[abovePolyIndex] = bounderyPolygons[abovePolyIndex].Count - 1;
					}

					for (int i = indexOfMinX[abovePolyIndex]; i != indexOfMaxX[abovePolyIndex]; i = (i > 0) ? (i - 1) : (bounderyPolygons[abovePolyIndex].Count - 1))
					{
						pointList.Add(GetBounderyPointWithOffset(abovePolyIndex, i));
					}
				}
				pointList.Add(matrix.unapply(new IntPoint(maxXPosition[abovePolyIndex] + 200, nomalizedStartPoint.Y)));

				nomalizedStartX = maxXPosition[abovePolyIndex];
			}
			pointList.Add(endPoint);

			if (addEndpoint)
			{
				pointList.Add(endPoint);
			}

			// Optimize the pointList, skip each point we could already reach by connecting directly to the next point.
			for (int startIndex = 0; startIndex < pointList.Count - 2; startIndex++)
			{
				IntPoint startPosition = pointList[startIndex];
				// make sure there is at least one point between the start and the end to optomize
				if (pointList.Count > startIndex + 2)
				{
					for (int checkIndex = pointList.Count - 1; checkIndex > startIndex + 1; checkIndex--)
					{
						IntPoint checkPosition = pointList[checkIndex];
						if (!DoesLineCrossBoundery(startPosition, checkPosition))
						{
							// Remove all the points from startIndex+1 to checkIndex-1, inclusive.
							for (int i = startIndex + 1; i < checkIndex; i++)
							{
								pointList.RemoveAt(startIndex + 1);
							}

							// we removed all the points up to start so we are done with the inner loop
							break;
						}
					}
				}
			}

			foreach (IntPoint point in pointList)
			{
				pathThatIsInside.Add(point);
			}

			return true;
		}

		public bool MovePointInsideBoundary(ref IntPoint pointToMove, int maxDistanceToMove = 100)
		{
			IntPoint newPosition = pointToMove;
			long bestDist = 2000 * 2000;
			for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
			{
				Polygon boundryPolygon = bounderyPolygons[bounderyIndex];

				if (boundryPolygon.Count < 1)
				{
					continue;
				}

				IntPoint previousPoint = boundryPolygon[boundryPolygon.Count - 1];
				for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
				{
					IntPoint currentPoint = boundryPolygon[pointIndex];

					//Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
					IntPoint deltaToCurrent = currentPoint - previousPoint;
					long deltaLength = deltaToCurrent.Length();
					long distToBoundrySegment = deltaToCurrent.Dot(pointToMove - previousPoint) / deltaLength;
					if (distToBoundrySegment < 10)
					{
						distToBoundrySegment = 10;
					}

					if (distToBoundrySegment > deltaLength - 10)
					{
						distToBoundrySegment = deltaLength - 10;
					}

					IntPoint pointAlongCurrentSegment = previousPoint + deltaToCurrent * distToBoundrySegment / deltaLength;

					long dist = (pointAlongCurrentSegment - pointToMove).LengthSquared();
					if (dist < bestDist)
					{
						bestDist = dist;
						newPosition = pointAlongCurrentSegment + ((currentPoint - previousPoint).Normal(maxDistanceToMove)).GetPerpendicularLeft();
					}

					previousPoint = currentPoint;
				}
			}

			if (bestDist < 2000 * 2000)
			{
				pointToMove = newPosition;
				return true;
			}

			return false;
		}

		public bool PointIsInsideBoundary(IntPoint pointToTest)
		{
			return bounderyPolygons.Inside(pointToTest);
		}

		private void CalcMinMax()
		{
			for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
			{
				Polygon boundryPolygon = bounderyPolygons[bounderyIndex];

				minXPosition[bounderyIndex] = long.MaxValue;
				maxXPosition[bounderyIndex] = long.MinValue;
				IntPoint previousPosition = matrix.apply(boundryPolygon[boundryPolygon.Count - 1]);
				for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
				{
					IntPoint currentPosition = matrix.apply(boundryPolygon[pointIndex]);
					if ((previousPosition.Y >= nomalizedStartPoint.Y && currentPosition.Y <= nomalizedStartPoint.Y)
						|| (currentPosition.Y >= nomalizedStartPoint.Y && previousPosition.Y <= nomalizedStartPoint.Y))
					{
						long x = previousPosition.X + (currentPosition.X - previousPosition.X) * (nomalizedStartPoint.Y - previousPosition.Y) / (currentPosition.Y - previousPosition.Y);

						if (x >= nomalizedStartPoint.X && x <= normalizedEndPoint.X)
						{
							if (x <= minXPosition[bounderyIndex])
							{
								minXPosition[bounderyIndex] = x;
								indexOfMinX[bounderyIndex] = pointIndex;
							}

							if (x > maxXPosition[bounderyIndex])
							{
								maxXPosition[bounderyIndex] = x;
								indexOfMaxX[bounderyIndex] = pointIndex;
							}
						}
					}

					previousPosition = currentPosition;
				}
			}
		}

		private bool DoesLineCrossBoundery(IntPoint startPoint, IntPoint endPoint)
		{
			IntPoint diff = endPoint - startPoint;

			{
				matrix = new PointMatrix(diff);
				this.nomalizedStartPoint = matrix.apply(startPoint);
				this.normalizedEndPoint = matrix.apply(endPoint);
			}

			for (int bounderyIndex = 0; bounderyIndex < bounderyPolygons.Count; bounderyIndex++)
			{
				Polygon boundryPolygon = bounderyPolygons[bounderyIndex];
				if (boundryPolygon.Count < 1)
				{
					continue;
				}

				IntPoint lastPosition = boundryPolygon[boundryPolygon.Count - 1];
				for (int pointIndex = 0; pointIndex < boundryPolygon.Count; pointIndex++)
				{
					IntPoint currentPosition = boundryPolygon[pointIndex];
					int startSide = startPoint.GetLineSide(lastPosition, currentPosition);
					int endSide = endPoint.GetLineSide(lastPosition, currentPosition);
					if (startSide != 0 && startSide + endSide == 0)
					{
						return true;
					}

					lastPosition = currentPosition;
				}
			}
			return false;
		}

		private IntPoint GetBounderyPointWithOffset(int polygonIndex, int pointIndex)
		{
			int previousIndex = pointIndex - 1;
			if (previousIndex < 0)
			{
				previousIndex = bounderyPolygons[polygonIndex].Count - 1;
			}

			IntPoint previousPoint = bounderyPolygons[polygonIndex][previousIndex];
			IntPoint currentPoint = bounderyPolygons[polygonIndex][pointIndex];

			int nextIndex = pointIndex + 1;
			if (nextIndex >= bounderyPolygons[polygonIndex].Count)
			{
				nextIndex = 0;
			}
			IntPoint nextPoint = bounderyPolygons[polygonIndex][nextIndex];

			// assuming a ccw winding this will give us a point inside of the edge
			IntPoint leftNormalOfPrevEdge = ((currentPoint - previousPoint).Normal(1000)).GetPerpendicularLeft();
			IntPoint leftNormalOfCurrentEdge = ((nextPoint - currentPoint).Normal(1000)).GetPerpendicularLeft();
			IntPoint offsetToBeInside = (leftNormalOfPrevEdge + leftNormalOfCurrentEdge).Normal(200);

			return currentPoint + offsetToBeInside;
		}

		private int GetPolygonIndexAbove(long startingPolygonIndex)
		{
			long minXFound = long.MaxValue;
			int abovePolyIndex = -1;

			for (int polygonIndex = 0; polygonIndex < bounderyPolygons.Count; polygonIndex++)
			{
				if (minXPosition[polygonIndex] > startingPolygonIndex
					&& minXPosition[polygonIndex] < minXFound)
				{
					minXFound = minXPosition[polygonIndex];
					abovePolyIndex = polygonIndex;
				}
			}

			return abovePolyIndex;
		}
	}
}