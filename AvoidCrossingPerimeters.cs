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
	using System;
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
		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside)
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

			bool addEndpoint = false;
			if (!PointIsInsideBoundary(endPoint))
			{
				if (!MovePointInsideBoundary(ref endPoint))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				addEndpoint = true;
			}

			// get all the crossings

			// if crossing are 0 
			//We're not crossing any boundaries. So skip the comb generation.
			if (!addEndpoint && pathThatIsInside.Count == 0)
			{
				//Only skip if we didn't move the start and end point.
				return true;
			}

			// else

			// sort them in the order of the start end direction

			// for each pair of crossings

			// add a move to the start of the crossing
			// try to go CW and CWW take the path that is the shortest and add it to the list


			// Calculate the matrix to change points so they are in the direction of the line segment.
			{
				IntPoint diff = endPoint - startPoint;

				lineToSameYMatrix = new PointMatrix(diff);
				this.rotatedStartPoint = lineToSameYMatrix.apply(startPoint);
				this.rotatedEndPoint = lineToSameYMatrix.apply(endPoint);
			}


			long nomalizedStartX = rotatedStartPoint.X;
			Polygon pointList = new Polygon();
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

			foreach (IntPoint point in pointList)
			{
				pathThatIsInside.Add(point);
			}

			return true;
		}

		public bool MovePointInsideBoundary(ref IntPoint pointToMove)
		{
			if (boundaryPolygons.PointIsInside(pointToMove))
			{
				// already inside
				return false;
			}

			long bestDist = long.MaxValue;
			IntPoint bestPosition = pointToMove;
			IntPoint bestMoveNormal = new IntPoint();
			foreach (var boundaryPolygon in boundaryPolygons)
			{
				if (boundaryPolygon.Count < 3)
				{
					continue;
				}

				IntPoint segmentStart = boundaryPolygon[boundaryPolygon.Count - 1];
				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
					IntPoint pointRelStart = pointToMove - segmentStart;
					long distFromStart = pointRelStart.Length();
					if (distFromStart < bestDist)
					{
						bestDist = distFromStart;
						bestPosition = segmentStart;
					}

					IntPoint segmentEnd = boundaryPolygon[pointIndex];

					//Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
					IntPoint segmentDelta = segmentEnd - segmentStart;
					IntPoint normal = segmentDelta.Normal(1000);
					IntPoint normalToRight = normal.GetPerpendicularLeft();

					long distanceFromStart = normal.Dot(pointRelStart) / 1000;

					if (distanceFromStart >= 0 && distanceFromStart <= segmentDelta.Length())
					{
						long distToBoundarySegment = normalToRight.Dot(pointRelStart) / 1000;

						if (Math.Abs(distToBoundarySegment) < bestDist)
						{
							IntPoint pointAlongCurrentSegment = pointToMove;
							if (distToBoundarySegment != 0)
							{
								pointAlongCurrentSegment = pointToMove - normalToRight * distToBoundarySegment / 1000;
							}

							bestDist = Math.Abs(distToBoundarySegment);
							bestPosition = pointAlongCurrentSegment;
							bestMoveNormal = normalToRight;
						}
					}

					segmentStart = segmentEnd;
				}
			}

			pointToMove = bestPosition;

			int tryCount = 0;
			if (!boundaryPolygons.PointIsInside(pointToMove))
			{
				long normalLength = bestMoveNormal.Length();
				do
				{
					pointToMove = bestPosition + (bestMoveNormal * (1<<tryCount) / normalLength) * ((tryCount % 2) == 0 ? 1 : -1);
					tryCount++;
				} while (!boundaryPolygons.PointIsInside(pointToMove) && tryCount < 10) ;
			}

			if(tryCount > 8)
			{
				return false;
			}

			return true;
		}

		public bool PointIsInsideBoundary(IntPoint pointToTest)
		{
			return boundaryPolygons.PointIsInside(pointToTest);
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