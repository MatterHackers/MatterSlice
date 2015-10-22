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
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public class PathOrderOptimizer
	{
		private IntPoint startPosition;
		private List<Polygon> polygons = new List<Polygon>();
		public List<int> startIndexInPolygon = new List<int>();
		public List<int> bestPolygonOrderIndex = new List<int>();

		public PathOrderOptimizer(IntPoint startPoint)
		{
			this.startPosition = startPoint;
		}

		public void AddPolygon(Polygon polygon)
		{
			this.polygons.Add(polygon);
		}

		public void AddPolygons(Polygons polygons)
		{
			for (int i = 0; i < polygons.Count; i++)
			{
				this.polygons.Add(polygons[i]);
			}
		}

		private static int GetClosestIndex(Polygon currentPolygon, IntPoint startPosition)
		{
			int bestPointIndex = -1;
			double closestDist = double.MaxValue;
			for (int pointIndex = 0; pointIndex < currentPolygon.Count; pointIndex++)
			{
				double dist = (currentPolygon[pointIndex] - startPosition).LengthSquared();
				if (dist < closestDist)
				{
					bestPointIndex = pointIndex;
					closestDist = dist;
				}
			}

			return bestPointIndex;
		}

		public static double GetTurnAmount(IntPoint prevPoint, IntPoint currentPoint, IntPoint nextPoint)
		{
			if (prevPoint != currentPoint
				&& currentPoint != nextPoint
				&& nextPoint != prevPoint)
			{
				prevPoint = currentPoint - prevPoint;
				nextPoint -= currentPoint;

				double prevAngle = Math.Atan2(prevPoint.Y, prevPoint.X);
				IntPoint rotatedPrev = prevPoint.GetRotated(-prevAngle);

				// undo the rotation
				nextPoint = nextPoint.GetRotated(-prevAngle);
				double angle = Math.Atan2(nextPoint.Y, nextPoint.X); ;

				return angle;
			}

			return 0;
		}

		public static int GetBestEdgeIndex(Polygon currentPolygon)
		{
			double totalTurns = 0;
			double bestPositiveTurn = double.MinValue;
			int bestPositiveTurnIndex = -1;
			double bestNegativeTurn = double.MaxValue;
			int bestNegativeTurnIndex = -1;

			int pointCount = currentPolygon.Count;
			for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
			{
				int prevIndex = ((pointIndex + pointCount - 1) % pointCount);
				int nextIndex = ((pointIndex + 1) % pointCount);
				IntPoint prevPoint = currentPolygon[prevIndex];
				IntPoint currentPoint = currentPolygon[pointIndex];
				IntPoint nextPoint = currentPolygon[nextIndex];

				double turnAmount = GetTurnAmount(prevPoint, currentPoint, nextPoint);
				totalTurns += turnAmount;

				if (turnAmount < 0)
				{
					if (turnAmount < bestNegativeTurn)
					{
						bestNegativeTurn = turnAmount;
						bestNegativeTurnIndex = pointIndex;
					}
				}
				else
				{
					if (turnAmount > bestPositiveTurn)
					{
						bestPositiveTurn = turnAmount;
						bestPositiveTurnIndex = pointIndex;
					}
				}
			}

			if (totalTurns > 0)
			{
				if (bestNegativeTurnIndex >= 0)
				{
					return bestNegativeTurnIndex;
				}
				return bestPositiveTurnIndex;
			}
			else
			{
				if (bestPositiveTurnIndex >= 0)
				{
					return bestPositiveTurnIndex;
				}
				return bestNegativeTurnIndex;
			}
		}

		private static double GetAngle(IntPoint prevPoint, IntPoint currentPoint, IntPoint nextPoint)
		{
			throw new NotImplementedException();
		}

		public void Optimize(GCodePathConfig config = null)
		{
			bool canTravelForwardOrBackward = config != null && !config.closedLoop;
			// Find the point that is closest to our current position (start position)
			bool[] polygonHasBeenAdded = new bool[polygons.Count];
			for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
			{
				Polygon currentPolygon = polygons[polygonIndex];
				if (canTravelForwardOrBackward || currentPolygon.Count < 3)
				{
					startIndexInPolygon.Add(0);
				}
				else // This is a closed loop.
				{
					// some code for helping create unit tests
					//string polyString = currentPolygon.WriteToString();
					//currentPolygon.SaveToGCode("perimeter.gcode");

					// this is our new seam hiding code
					int bestPointIndex = GetBestEdgeIndex(currentPolygon);
					//int bestPointIndex = GetClosestIndex(currentPolygon, startPosition);
					startIndexInPolygon.Add(bestPointIndex);
				}
			}

			IntPoint currentPosition = startPosition;
			// We loop over the polygon list twice, at each inner loop we only pick one polygon.
			for (int polygonIndexOuterLoop = 0; polygonIndexOuterLoop < polygons.Count; polygonIndexOuterLoop++)
			{
				int bestPolygonIndex = -1;
				double bestDist = double.MaxValue;
				for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
				{
					if (polygonHasBeenAdded[polygonIndex] || polygons[polygonIndex].Count < 1)
					{
						continue;
					}

					// If there are only 2 points (a single line) or the path is marked as travel both ways, we are willing to start from the start or the end.
					if (polygons[polygonIndex].Count == 2 || canTravelForwardOrBackward)
					{
						double distToSart = (polygons[polygonIndex][0] - currentPosition).LengthSquared();
						if (distToSart < bestDist)
						{
							bestPolygonIndex = polygonIndex;
							bestDist = distToSart;
							startIndexInPolygon[polygonIndex] = 0;
						}

						double distToEnd = (polygons[polygonIndex][polygons[polygonIndex].Count - 1] - currentPosition).LengthSquared();
						if (distToEnd < bestDist)
						{
							bestPolygonIndex = polygonIndex;
							bestDist = distToEnd;
							startIndexInPolygon[polygonIndex] = 1;
						}
					}
					else
					{
						double dist = (polygons[polygonIndex][startIndexInPolygon[polygonIndex]] - currentPosition).LengthSquared();
						if (dist < bestDist)
						{
							bestPolygonIndex = polygonIndex;
							bestDist = dist;
						}
					}
				}

				if (bestPolygonIndex > -1)
				{
					if (polygons[bestPolygonIndex].Count == 2 || canTravelForwardOrBackward)
					{
						// get the point that is opposite from the one we started on
						int startIndex = startIndexInPolygon[bestPolygonIndex];
						if (startIndex == 0)
						{
							currentPosition = polygons[bestPolygonIndex][polygons[bestPolygonIndex].Count - 1];
						}
						else
						{
							currentPosition = polygons[bestPolygonIndex][0];
						}
					}
					else
					{
						currentPosition = polygons[bestPolygonIndex][startIndexInPolygon[bestPolygonIndex]];
					}
					polygonHasBeenAdded[bestPolygonIndex] = true;
					bestPolygonOrderIndex.Add(bestPolygonIndex);
				}
			}

			currentPosition = startPosition;
			foreach (int bestPolygonIndex in bestPolygonOrderIndex)
			{
				int bestStartPoint = -1;
				double bestDist = double.MaxValue;
				if (canTravelForwardOrBackward)
				{
					bestDist = (polygons[bestPolygonIndex][0] - currentPosition).LengthSquared();
					bestStartPoint = 0;

					// check if the end is better
					int endIndex = polygons[bestPolygonIndex].Count - 1;
					double dist = (polygons[bestPolygonIndex][endIndex] - currentPosition).LengthSquared();
					if (dist < bestDist)
					{
						bestStartPoint = endIndex;
						bestDist = dist;
					}

					startIndexInPolygon[bestPolygonIndex] = bestStartPoint;
				}
				else
				{
					for (int pointIndex = 0; pointIndex < polygons[bestPolygonIndex].Count; pointIndex++)
					{
						double dist = (polygons[bestPolygonIndex][pointIndex] - currentPosition).LengthSquared();
						if (dist < bestDist)
						{
							bestStartPoint = pointIndex;
							bestDist = dist;
						}
					}
				}

				if (polygons[bestPolygonIndex].Count == 2 || canTravelForwardOrBackward)
				{
					if (bestStartPoint == 0)
					{
						currentPosition = polygons[bestPolygonIndex][polygons[bestPolygonIndex].Count - 1];
					}
					else
					{
						currentPosition = polygons[bestPolygonIndex][0];
					}
				}
				else
				{
					currentPosition = polygons[bestPolygonIndex][bestStartPoint];
				}
			}
		}
	}
}