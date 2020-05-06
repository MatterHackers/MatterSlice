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
	using System.Linq;
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
		/// This will find the largest turn in a given models. It prefers concave turns to convex turns.
		/// If turn amount is the same bias towards the smallest y position.
		/// </summary>
		/// <param name="inputPolygon"></param>
		/// <param name="considerAsSameY">Range to treat y positions as the same value.</param>
		/// <returns></returns>
		public static IntPoint FindGreatestTurnPosition(this Polygon inputPolygon, long considerAsSameY, int layerIndex, IntPoint? startPosition = null)
		{
			Polygon currentPolygon = Clipper.CleanPolygon(inputPolygon, considerAsSameY / 8);

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

			if (negativeGroup.Count > 0)
			{
				if (positiveGroup.Count > 0
					// the negative group is a small turn and the positive group is a big turn
					&& ((Math.Abs(negativeGroup[0].turnAmount) < Math.PI / 4
							&& Math.Abs(positiveGroup[0].turnAmount) > Math.PI / 4)
						// the negative turn amount is very small
						|| Math.Abs(negativeGroup[0].turnAmount) < Math.PI / 8))
				{
					// return the positive rather than the negative turn
					return currentPolygon[positiveGroup.GetBestIndex(layerIndex, startPosition)];
				}

				return currentPolygon[negativeGroup.GetBestIndex(layerIndex, startPosition)];
			}
			else if (positiveGroup.Count > 0)
			{
				return currentPolygon[positiveGroup.GetBestIndex(layerIndex, startPosition)];
			}
			else
			{
				// If can't find good candidate go with vertex most in a single direction
				return currentPolygon[furthestBackIndex];
			}
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

		public static Polygon CleanClosedPolygon(this Polygon polygon, double distance = 1.415)
		{
			if (polygon.Count == 0)
			{
				return new Polygon();
			}

			var result = new Polygon(polygon);

			var distSqrd = distance * distance;

			var removeIndices = new HashSet<int>();

			// loop over all points starting at the front
			for (int startIndex = 0; startIndex < result.Count - 2; startIndex++)
			{
				var startPosition = result[startIndex];

				// accumulate all the collinear points from this point
				for (int endIndex = startIndex+2; endIndex < result.Count; endIndex++)
				{
					var endPosition = result[endIndex];

					bool allInbetweenIsCollinear = true;

					// check that every point between start and end is collinear
					for (int testIndex = startIndex+1; testIndex < endIndex; testIndex++)
					{
						var testPosition = result[testIndex];
						if (!Clipper.SlopesNearCollinear(startPosition, testPosition, endPosition, distSqrd))
						{
							allInbetweenIsCollinear = false;
							break;
						}
					}

					if (allInbetweenIsCollinear)
					{
						for (int testIndex = startIndex + 1; testIndex < endIndex; testIndex++)
						{
							removeIndices.Add(testIndex);
						}
					}
					else
					{
						startIndex = endIndex - 2;
						// move on to next start
						break;
					}
				}
			}

			var removeList = removeIndices.ToList();
			removeList.Sort();
			for(int i= removeList.Count-1; i>=0; i--)
			{
				result.RemoveAt(removeList[i]);
			}

			return result;
		}

		public static IntPoint GetPositionAllongPath(this Polygon polygon, double ratioAlongPath, bool isClosed = true)
		{
			IntPoint position = new IntPoint();
			var totalLength = polygon.PolygonLength(isClosed);
			var distanceToGoal = (long)(totalLength * ratioAlongPath + .5);
			long length = 0;
			if (polygon.Count > 1)
			{
				position = polygon[0];
				IntPoint currentPoint = polygon[0];

				int polygonCount = polygon.Count;
				for (int i = 1; i < (isClosed ? polygonCount + 1 : polygonCount); i++)
				{
					IntPoint nextPoint = polygon[i % polygonCount];
					var segmentLength = (nextPoint - currentPoint).Length();
					if(length + segmentLength > distanceToGoal)
					{
						// return the distance along this segment
						var distanceAlongThisSegment = distanceToGoal - length;
						var delteFromCurrent = (nextPoint - currentPoint) * distanceAlongThisSegment / segmentLength;
						return currentPoint + delteFromCurrent;
					}
					length += segmentLength;
					currentPoint = nextPoint;
				}
			}

			return position;
		}

		public static long PolygonLength(this Polygon polygon, bool isClosed = true)
		{
			long length = 0;
			if (polygon.Count > 1)
			{
				IntPoint previousPoint = polygon[0];
				if (isClosed)
				{
					previousPoint = polygon[polygon.Count - 1];
				}
				for (int i = isClosed ? 0 : 1; i < polygon.Count; i++)
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

			/// <summary>
			/// Get the best turn for this polygon. If there are multiple turns that are all just as good choose one with a bias for layer index.
			/// </summary>
			/// <param name="layerIndex"></param>
			/// <returns></returns>
			public int GetBestIndex(int layerIndex, IntPoint? startPosition)
			{
				bool shallowTurn = Math.Abs(this[this.Count - 1].turnAmount) < .3;
				bool outsideEdge = this[this.Count - 1].turnAmount > 0;

				if (shallowTurn || startPosition == null)
				{
					if (outsideEdge) // sort to the back
					{
						this.Sort((a, b) =>
						{
							if (a.position.Y == b.position.Y)
							{
								return b.position.X.CompareTo(a.position.X);
							}
							else
							{
								return a.position.Y.CompareTo(b.position.Y);
							}
						});
					}
					else // sort to the front
					{
						this.Sort((a, b) =>
						{
							if (a.position.Y == b.position.Y)
							{
								return b.position.X.CompareTo(a.position.X);
							}
							else
							{
								return b.position.Y.CompareTo(a.position.Y);
							}
						});
					}
				}
				else // sort them by distance from start
				{
					this.Sort((a, b) =>
					{
						var distToA = (a.position - startPosition.Value).LengthSquared();
						var distToB = (b.position - startPosition.Value).LengthSquared();
						return distToB.CompareTo(distToA);
					});
				}

				// if we have a very shallow turn (the outer edge of a circle)
				if (shallowTurn)
				{
					// stager 3 places so the seam is more together but not a line
					int seemShift = layerIndex % 3;
					if (!outsideEdge) // we are on the inside of a circular hole (or similar)
					{
						// stager up to 5 to make the seam have less surface
						seemShift = layerIndex % 5;
					}

					if(this.Count > seemShift)
					{
						return this[this.Count - seemShift - 1].turnIndex;
					}
				}

				return this[this.Count - 1].turnIndex;
			}

			public void ConditionalAdd(CandidatePoint point)
			{
				if(Math.Abs(point.turnAmount) > Math.PI/2)
				{
					// we keep everything bigger than 90 degrees
					// remove all points that are worse than 90 degree
					for (int i = Count - 1; i >= 0; i--)
					{
						if (Math.Abs(this[i].turnAmount)  < Math.PI / 2)
						{
							RemoveAt(i);
						}
						else
						{
							break;
						}
					}

					// and add this point
					Add(point);
					return;
				}

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
						else
						{
							break;
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