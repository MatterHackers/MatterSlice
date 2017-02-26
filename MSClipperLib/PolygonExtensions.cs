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

using System.Collections.Generic;

namespace MSClipperLib
{
	using System;
	using Polygon = List<IntPoint>;

	public static class CLPolygonExtensions
	{
		public static Polygon CreateFromString(string polygonString)
		{
			Polygon output = new Polygon();
			string[] intPointData = polygonString.Split(',');
			int increment = 2;
			if (polygonString.Contains("width"))
			{
				increment = 4;
			}
			for (int i = 0; i < intPointData.Length - 1; i += increment)
			{
				string elementX = intPointData[i];
				string elementY = intPointData[i + 1];
				IntPoint nextIntPoint = new IntPoint(int.Parse(elementX.Substring(elementX.IndexOf(':') + 1)), int.Parse(elementY.Substring(3)));
				output.Add(nextIntPoint);
			}

			return output;
		}

		public static double DegreesToRadians(double degrees)
		{
			const double degToRad = System.Math.PI / 180.0f;
			return degrees * degToRad;
		}

		/// <summary>
		/// This will find the largest turn in a given models. It preffers concave turns to convex turns.
		/// </summary>
		/// <param name="inputPolygon"></param>
		/// <param name="lineWidth"></param>
		/// <returns></returns>
		public static int FindGreatestTurnIndex(this Polygon inputPolygon, long lineWidth = 3)
		{
			// code to make the seam go to the back most position
			//return inputPolygon.LargestTurnIndex(new IntPoint(0, 50000000));

			// code to go to a specific position (would have to have it come from setting)
			//return inputPolygon.LargestTurnIndex(config.SeamPosition);

			IntPoint bestPosition = inputPolygon.FindGreatestTurnPosition(lineWidth);
			return inputPolygon.FindClosestPositionIndex(bestPosition);
		}

		public static int FindClosestPositionIndex(this Polygon polygon, IntPoint position)
		{
			int bestPointIndex = -1;
			double closestDist = double.MaxValue;
			for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
			{
				double dist = (polygon[pointIndex] - position).LengthSquared();
				if (dist < closestDist)
				{
					bestPointIndex = pointIndex;
					closestDist = dist;
				}
			}

			return bestPointIndex;
		}

		/// <summary>
		/// This will find the largest turn in a given models. It preffers concave turns to convex turns.
		/// If turn amount is the same bias towards the smallest y position.
		/// </summary>
		/// <param name="inputPolygon"></param>
		/// <param name="considerAsSameY">Range to treat y positions as the same value.</param>
		/// <returns></returns>
		public static IntPoint FindGreatestTurnPosition(this Polygon inputPolygon, long considerAsSameY)
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

			Polygon currentPolygon = Clipper.CleanPolygon(inputPolygon, considerAsSameY / 4);

			// collect & bucket options and then choose the closest
			if (currentPolygon.Count == 0)
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

				if (currentPoint.Y >= currentFurthestBack.Y)
				{
					if (currentPoint.Y > currentFurthestBack.Y
						|| currentPoint.X < currentFurthestBack.X)
					{
						furthestBackIndex = pointIndex;
						currentFurthestBack = currentPoint;
					}
				}

				long lengthPrevToCurSquared = (prevPoint - currentPoint).LengthSquared();
				long lengthCurToNextSquared = (nextPoint - currentPoint).LengthSquared();
				bool distanceLongeEnough = lengthCurToNextSquared > minSegmentLengthToConsiderSquared && lengthPrevToCurSquared > minSegmentLengthToConsiderSquared;

				double turnAmount = currentPoint.GetTurnAmount(prevPoint, nextPoint);

				totalTurns += turnAmount;

				if (turnAmount < 0)
				{
					// threshold angles, don't pick angles that are too shallow
					// threshold line lengths, don't pick big angles hiding in TINY lines
					if (Math.Abs(turnAmount) > minTurnToChoose
						&& distanceLongeEnough)
					{
						negativeGroup.ConditionalAdd(new CandidatePoint(turnAmount, pointIndex, currentPoint));
					}
				}
				else
				{
					if (Math.Abs(turnAmount) > minTurnToChoose
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

			if (Math.Abs(currentFurthestBackActual.Y - positionToReturn.Y) < considerAsSameY)
			{
				return currentFurthestBackActual;
			}

			return positionToReturn;
		}

		public static IntRect GetBounds(this Polygon inPolygon)
		{
			if (inPolygon.Count == 0)
			{
				return new IntRect(0, 0, 0, 0);
			}

			IntRect result = new IntRect();
			result.minX = inPolygon[0].X;
			result.maxX = result.minX;
			result.minY = inPolygon[0].Y;
			result.maxY = result.minY;
			for (int pointIndex = 1; pointIndex < inPolygon.Count; pointIndex++)
			{
				if (inPolygon[pointIndex].X < result.minX)
				{
					result.minX = inPolygon[pointIndex].X;
				}
				else if (inPolygon[pointIndex].X > result.maxX)
				{
					result.maxX = inPolygon[pointIndex].X;
				}

				if (inPolygon[pointIndex].Y > result.maxY)
				{
					result.maxY = inPolygon[pointIndex].Y;
				}
				else if (inPolygon[pointIndex].Y < result.minY)
				{
					result.minY = inPolygon[pointIndex].Y;
				}
			}

			return result;
		}

		public static long PolygonLength(this Polygon polygon, bool areClosed = true)
		{
			long length = 0;
			if (polygon.Count > 1)
			{
				IntPoint previousPoint = polygon[0];
				if (areClosed)
				{
					previousPoint = polygon[polygon.Count - 1];
				}
				for (int i = areClosed ? 0 : 1; i < polygon.Count; i++)
				{
					IntPoint currentPoint = polygon[i];
					length += (previousPoint - currentPoint).Length();
					previousPoint = currentPoint;
				}
			}

			return length;
		}

		public static string WriteToString(this Polygon polygon)
		{
			string total = "";
			foreach (IntPoint point in polygon)
			{
				total += point.ToString() + ",";
			}
			return total;
		}

		public struct CandidatePoint
		{
			internal IntPoint position;
			internal double turnAmount;
			internal int turnIndex;

			internal CandidatePoint(double turnAmount, int turnIndex, IntPoint position)
			{
				this.turnIndex = turnIndex;
				this.turnAmount = turnAmount;
				this.position = position;
			}
		}

		public class CandidateGroup : List<CandidatePoint>
		{
			private double sameTurn;

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
	}
}