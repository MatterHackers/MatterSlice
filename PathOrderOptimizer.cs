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
		public List<int> bestIslandOrderIndex = new List<int>();

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

		public static int GetClosestIndex(Polygon currentPolygon, IntPoint startPosition)
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
				IntPoint rotatedPrev = prevPoint.GetRotatedAboutZ(-prevAngle);

				// undo the rotation
				nextPoint = nextPoint.GetRotatedAboutZ(-prevAngle);
				double angle = Math.Atan2(nextPoint.Y, nextPoint.X); ;

				return angle;
			}

			return 0;
		}

		public struct CandidatePoint
		{
			internal double turnAmount;
			internal int turnIndex;
			internal IntPoint position;

			internal CandidatePoint(double turnAmount, int turnIndex, IntPoint position)
			{
				this.turnIndex = turnIndex;
				this.turnAmount = turnAmount;
				this.position = position;
			}
		}

		public static double DegreesToRadians(double degrees)
		{
			const double degToRad = System.Math.PI / 180.0f;
			return degrees * degToRad;
		}

		public class CandidateGroup : List<CandidatePoint>
		{
			double sameTurn;

			public CandidateGroup(double sameTurn)
			{
				this.sameTurn = sameTurn;
			}

			public int BestIndex
			{
				get
				{
					IntPoint currentFurthestBack = new IntPoint(long.MaxValue, long.MinValue);
					int furthestBackIndex = 0;

					for (int i = 0; i < Count; i++)
					{
						IntPoint currentPoint = this[i].position;
						if (currentPoint.Y >= currentFurthestBack.Y)
						{
							if (currentPoint.Y > currentFurthestBack.Y
								|| currentPoint.X < currentFurthestBack.X)
							{
								furthestBackIndex = this[i].turnIndex;
								currentFurthestBack = currentPoint;
							}
						}
					}

					return furthestBackIndex;
				}
			}

			internal void ConditionalAdd(CandidatePoint point)
			{
				// If this is better than our worst point
				// or it is within sameTurn of our best point
				if (Count == 0
					|| Math.Abs(point.turnAmount) >= Math.Abs(this[Count - 1].turnAmount)
					|| Math.Abs(point.turnAmount) >= Math.Abs(this[0].turnAmount) - sameTurn)
				{
					// remove all points that are worse than the new one
					for (int i = Count - 1; i >= 0; i--)
					{
						if (Math.Abs(this[i].turnAmount) + sameTurn < Math.Abs(point.turnAmount))
						{
							RemoveAt(i);
						}
					}

					if (Count > 0)
					{
						for (int i = 0; i < Count; i++)
						{
							if (Math.Abs(point.turnAmount) >= Math.Abs(this[i].turnAmount))
							{
								// insert it sorted
								Insert(i, point);
								return;
							}
						}

						// still insert it at the end
						Add(point);
					}
					else
					{
						Add(point);
					}
				}
			}
		}

		public static int GetBestIndex(Polygon inputPolygon, long lineWidth = 3)
		{
			// code to make the seam go to the back most position
			//return GetClosestIndex(inputPolygon, new IntPoint(0, 50000000));

			// code to go to a specific position (would have to have it come from setting)
			//return GetClosestIndex(inputPolygon, config.SeamPosition);

			IntPoint bestPosition = GetBestPosition(inputPolygon, lineWidth);
			return GetClosestIndex(inputPolygon, bestPosition);
		}

		public static IntPoint GetBestPosition(Polygon inputPolygon, long lineWidth)
		{
			IntPoint currentFurthestBackActual = new IntPoint(long.MaxValue, long.MinValue);
			{
				int actualFurthestBack = 0;
				for (int pointIndex = 0; pointIndex < inputPolygon.Count; pointIndex++)
				{
					IntPoint currentPoint = inputPolygon[pointIndex];

					if (currentPoint.Y >= currentFurthestBackActual.Y)
					{
						if (currentPoint.Y > currentFurthestBackActual.Y
							|| currentPoint.X < currentFurthestBackActual.X)
						{
							actualFurthestBack = pointIndex;
							currentFurthestBackActual = currentPoint;
						}
					}
				}
			}

			Polygon currentPolygon = Clipper.CleanPolygon(inputPolygon, lineWidth/4);
			// TODO: other considerations
			// collect & bucket options and then choose the closest

			if(currentPolygon.Count == 0)
			{
				return inputPolygon[0];
			}

			double totalTurns = 0;
            CandidateGroup positiveGroup = new CandidateGroup(DegreesToRadians(35));
            CandidateGroup negativeGroup = new CandidateGroup(DegreesToRadians(10));

            IntPoint currentFurthestBack = new IntPoint(long.MaxValue, long.MinValue);
			int furthestBackIndex = 0;

			double minTurnToChoose = DegreesToRadians(1);
			long minSegmentLengthToConsiderSquared = 50 * 50;

			int pointCount = currentPolygon.Count;
			for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
			{
				int prevIndex = ((pointIndex + pointCount - 1) % pointCount);
				int nextIndex = ((pointIndex + 1) % pointCount);
				IntPoint prevPoint = currentPolygon[prevIndex];
				IntPoint currentPoint = currentPolygon[pointIndex];
				IntPoint nextPoint = currentPolygon[nextIndex];

				if(currentPoint.Y >= currentFurthestBack.Y)
				{
					if(currentPoint.Y > currentFurthestBack.Y
						|| currentPoint.X < currentFurthestBack.X)
					{
						furthestBackIndex = pointIndex;
						currentFurthestBack = currentPoint;
					}
				}

				long lengthPrevToCurSquared = (prevPoint - currentPoint).LengthSquared();
				long lengthCurToNextSquared = (nextPoint - currentPoint).LengthSquared();
				bool distanceLongeEnough = lengthCurToNextSquared > minSegmentLengthToConsiderSquared && lengthPrevToCurSquared > minSegmentLengthToConsiderSquared;

				double turnAmount = GetTurnAmount(prevPoint, currentPoint, nextPoint);

				totalTurns += turnAmount;

				if (turnAmount < 0)
				{
					// threshold angles, don't pick angles that are too shallow
					// threshold line lengths, don't pick big angles hiding in TINY lines
					if (Math.Abs(turnAmount ) > minTurnToChoose
						&& distanceLongeEnough)
					{
                        negativeGroup.ConditionalAdd(new CandidatePoint(turnAmount, pointIndex, currentPoint));
					}
				}
				else
				{
					if (Math.Abs(turnAmount ) > minTurnToChoose
						&& distanceLongeEnough)
					{
                        positiveGroup.ConditionalAdd(new CandidatePoint(turnAmount, pointIndex, currentPoint));
					}
				}
			}

			IntPoint positionToReturn = new IntPoint();
			if (totalTurns > 0) // ccw
			{
				if (negativeGroup.Count > 0)
				{
					positionToReturn = currentPolygon[negativeGroup.BestIndex];
				}
				else if (positiveGroup.Count > 0)
				{
					positionToReturn = currentPolygon[positiveGroup.BestIndex];
				}
				else
				{
					// If can't find good candidate go with vertex most in a single direction
					positionToReturn = currentPolygon[furthestBackIndex];
				}
			}
			else // cw
			{
				if (negativeGroup.Count > 0)
				{
					positionToReturn = currentPolygon[negativeGroup.BestIndex];
				}
				else if (positiveGroup.Count > 0)
				{
					positionToReturn = currentPolygon[positiveGroup.BestIndex];
				}
				else
				{
					// If can't find good candidate go with vertex most in a single direction
					positionToReturn = currentPolygon[furthestBackIndex];
				}
			}

			if(Math.Abs(currentFurthestBackActual.Y - positionToReturn.Y) < lineWidth)
			{
				return currentFurthestBackActual;
			}

			return positionToReturn;
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
					int bestPointIndex;
                    if (config != null 
						&& config.doSeamHiding
                        && !config.spiralize)
					{
						bestPointIndex = GetBestIndex(currentPolygon, config.lineWidth_um);
					}
					else
					{
						bestPointIndex = GetClosestIndex(currentPolygon, startPosition);
					}

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
						if (distToSart <= bestDist)
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
					bestIslandOrderIndex.Add(bestPolygonIndex);
				}
			}

			currentPosition = startPosition;
			foreach (int bestPolygonIndex in bestIslandOrderIndex)
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