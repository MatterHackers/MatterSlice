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

using MSClipperLib;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using System.IO;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class AvoidCrossingPerimeters
	{
		private Polygons boundaryPolygons;

		private int[] indexOfMaxX;
		private int[] indexOfMinX;
		private PointMatrix lineToSameYMatrix;
		private long[] maxXPosition;
		private long[] minXPosition;
		private IntPoint rotatedStartPoint;
		private IntPoint rotatedEndPoint;

		public AvoidCrossingPerimeters(Polygons boundaryPolygons)
		{
			this.boundaryPolygons = boundaryPolygons;
			minXPosition = new long[boundaryPolygons.Count];
			maxXPosition = new long[boundaryPolygons.Count];
			indexOfMinX = new int[boundaryPolygons.Count];
			indexOfMaxX = new int[boundaryPolygons.Count];
		}

		static bool saveDebugData = false;
		bool boundary = false;
		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, List<IntPoint> pathThatIsInside)
		{
			if (saveDebugData)
			{
				using (StreamWriter sw = File.AppendText("test.txt"))
				{
					if (boundary)
					{
						string pointsString = boundaryPolygons.WriteToString();
						sw.WriteLine(pointsString);
					}
					sw.WriteLine(startPoint.ToString() + "  " + endPoint.ToString());
				}
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

			// Check if we are crossing any boundaries
			if (!DoesLineCrossBoundary(startPoint, endPoint))
			{
				//We're not crossing any boundaries. So skip the comb generation.
				if (!addEndpoint && pathThatIsInside.Count == 0)
				{
					//Only skip if we didn't move the start and end point.
					return true;
				}
			}

			// Calculate the matrix to change points so they are in the direction of the line segment.
			{
				IntPoint diff = endPoint - startPoint;

				lineToSameYMatrix = new PointMatrix(diff);
				this.rotatedStartPoint = lineToSameYMatrix.apply(startPoint);
				this.rotatedEndPoint = lineToSameYMatrix.apply(endPoint);
			}


			// Calculate the minimum and maximum positions where we cross the comb boundary
			CalcMinMax();

			long nomalizedStartX = rotatedStartPoint.X;
			List<IntPoint> pointList = new List<IntPoint>();
			// Now walk trough the crossings, for every boundary we cross, find the initial cross point and the exit point.
			// Then add all the points in between to the pointList and continue with the next boundary we will cross,
			// until there are no more boundaries to cross.
			// This gives a path from the start to finish curved around the holes that it encounters.
			while (true)
			{
				// if we go up enough we should run into the boundary
				int abovePolyIndex = GetPolygonIndexAbove(nomalizedStartX);
				if (abovePolyIndex < 0)
				{
					break;
				}

				pointList.Add(lineToSameYMatrix.unapply(new IntPoint(minXPosition[abovePolyIndex] - 200, rotatedStartPoint.Y)));
				if ((indexOfMinX[abovePolyIndex] - indexOfMaxX[abovePolyIndex] + boundaryPolygons[abovePolyIndex].Count) % boundaryPolygons[abovePolyIndex].Count > (indexOfMaxX[abovePolyIndex] - indexOfMinX[abovePolyIndex] + boundaryPolygons[abovePolyIndex].Count) % boundaryPolygons[abovePolyIndex].Count)
				{
					for (int i = indexOfMinX[abovePolyIndex]; i != indexOfMaxX[abovePolyIndex]; i = (i < boundaryPolygons[abovePolyIndex].Count - 1) ? (i + 1) : (0))
					{
						pointList.Add(GetBoundaryPointWithOffset(abovePolyIndex, i));
					}
				}
				else
				{
					indexOfMinX[abovePolyIndex]--;
					if (indexOfMinX[abovePolyIndex] == -1)
					{
						indexOfMinX[abovePolyIndex] = boundaryPolygons[abovePolyIndex].Count - 1;
					}

					indexOfMaxX[abovePolyIndex]--;
					if (indexOfMaxX[abovePolyIndex] == -1)
					{
						indexOfMaxX[abovePolyIndex] = boundaryPolygons[abovePolyIndex].Count - 1;
					}

					for (int i = indexOfMinX[abovePolyIndex]; i != indexOfMaxX[abovePolyIndex]; i = (i > 0) ? (i - 1) : (boundaryPolygons[abovePolyIndex].Count - 1))
					{
						pointList.Add(GetBoundaryPointWithOffset(abovePolyIndex, i));
					}
				}
				pointList.Add(lineToSameYMatrix.unapply(new IntPoint(maxXPosition[abovePolyIndex] + 200, rotatedStartPoint.Y)));

				nomalizedStartX = maxXPosition[abovePolyIndex];
			}
			pointList.Add(endPoint);

			if (addEndpoint)
			{
				pointList.Add(endPoint);
			}

#if false
			// Optimize the pointList, skip each point we could already reach by connecting directly to the next point.
			for (int startIndex = 0; startIndex < pointList.Count - 2; startIndex++)
			{
				IntPoint startPosition = pointList[startIndex];
				// make sure there is at least one point between the start and the end to optimize
				if (pointList.Count > startIndex + 2)
				{
					for (int checkIndex = pointList.Count - 1; checkIndex > startIndex + 1; checkIndex--)
					{
						IntPoint checkPosition = pointList[checkIndex];
						if (!DoesLineCrossBoundary(startPosition, checkPosition))
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
#endif

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
			for (int boundaryIndex = 0; boundaryIndex < boundaryPolygons.Count; boundaryIndex++)
			{
				Polygon boundaryPolygon = boundaryPolygons[boundaryIndex];

				if (boundaryPolygon.Count < 1)
				{
					continue;
				}

				IntPoint previousPoint = boundaryPolygon[boundaryPolygon.Count - 1];
				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
					IntPoint currentPoint = boundaryPolygon[pointIndex];

					//Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
					IntPoint deltaToCurrent = currentPoint - previousPoint;
					long deltaLength = deltaToCurrent.Length();
					long distToBoundarySegment = deltaToCurrent.Dot(pointToMove - previousPoint) / deltaLength;
					if (distToBoundarySegment < 10)
					{
						distToBoundarySegment = 10;
					}

					if (distToBoundarySegment > deltaLength - 10)
					{
						distToBoundarySegment = deltaLength - 10;
					}

					IntPoint pointAlongCurrentSegment = previousPoint + deltaToCurrent * distToBoundarySegment / deltaLength;

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
			return boundaryPolygons.Inside(pointToTest);
		}

		private void CalcMinMax()
		{
			int errorDist = 100;
			for (int boundaryIndex = 0; boundaryIndex < boundaryPolygons.Count; boundaryIndex++)
			{
				Polygon boundaryPolygon = boundaryPolygons[boundaryIndex];

				minXPosition[boundaryIndex] = long.MaxValue;
				maxXPosition[boundaryIndex] = long.MinValue;
				IntPoint previousPosition = lineToSameYMatrix.apply(boundaryPolygon[boundaryPolygon.Count - 1]);
				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
					IntPoint currentPosition = lineToSameYMatrix.apply(boundaryPolygon[pointIndex]);
					if ((previousPosition.Y + errorDist >= rotatedStartPoint.Y && currentPosition.Y - errorDist <= rotatedStartPoint.Y)
						|| (currentPosition.Y + errorDist >= rotatedStartPoint.Y && previousPosition.Y - errorDist <= rotatedStartPoint.Y)) // prev -> current crosses the start -> end
					{
						if (currentPosition.Y != previousPosition.Y)
						{
							long x = previousPosition.X + (currentPosition.X - previousPosition.X) * (rotatedStartPoint.Y - previousPosition.Y) / (currentPosition.Y - previousPosition.Y);

							if (x + errorDist >= rotatedStartPoint.X && x - errorDist <= rotatedEndPoint.X)
							{
								if (x - errorDist <= minXPosition[boundaryIndex])
								{
									minXPosition[boundaryIndex] = x;
									indexOfMinX[boundaryIndex] = pointIndex;
								}

								if (x + errorDist > maxXPosition[boundaryIndex])
								{
									maxXPosition[boundaryIndex] = x;
									indexOfMaxX[boundaryIndex] = pointIndex;
								}
							}
						}
					}

					previousPosition = currentPosition;
				}
			}
		}

		private bool DoesLineCrossBoundary(IntPoint startPoint, IntPoint endPoint)
		{
			for (int boundaryIndex = 0; boundaryIndex < boundaryPolygons.Count; boundaryIndex++)
			{
				Polygon boundaryPolygon = boundaryPolygons[boundaryIndex];
				if (boundaryPolygon.Count < 1)
				{
					continue;
				}

				IntPoint lastPosition = boundaryPolygon[boundaryPolygon.Count - 1];
				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
					IntPoint currentPosition = boundaryPolygon[pointIndex];
					int startSide = startPoint.GetLineSide(lastPosition, currentPosition);
					int endSide = endPoint.GetLineSide(lastPosition, currentPosition);
					if (startSide != 0)
					{
						if (startSide + endSide == 0)
						{
							// each point is distinctly on a different side
							return true;
						}
					}
					else
					{
						// if we terminate on the line that will count as crossing
						return true;
					}
					
					if (endSide == 0)
					{
						// if we terminate on the line that will count as crossing
						return true;
					}

					lastPosition = currentPosition;
				}
			}
			return false;
		}

		private IntPoint GetBoundaryPointWithOffset(int polygonIndex, int pointIndex)
		{
			int previousIndex = pointIndex - 1;
			if (previousIndex < 0)
			{
				previousIndex = boundaryPolygons[polygonIndex].Count - 1;
			}

			IntPoint previousPoint = boundaryPolygons[polygonIndex][previousIndex];
			IntPoint currentPoint = boundaryPolygons[polygonIndex][pointIndex];

			int nextIndex = pointIndex + 1;
			if (nextIndex >= boundaryPolygons[polygonIndex].Count)
			{
				nextIndex = 0;
			}
			IntPoint nextPoint = boundaryPolygons[polygonIndex][nextIndex];

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

			for (int polygonIndex = 0; polygonIndex < boundaryPolygons.Count; polygonIndex++)
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