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
	using MatterHackers.VectorMath;
	using System;
	using System.Linq;
	using Polygon = List<IntPoint>;

	public static class CLPolygonExtensions
	{
		public static Polygon CreateFromString(string polygonString, double scale = 1)
		{
			var output = new Polygon();
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
				var nextIntPoint = new IntPoint(double.Parse(elementX.Substring(elementX.IndexOf(':') + 1)) * scale,
					double.Parse(elementY.Substring(3)) * scale);
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
		/// If turn amount is the same, bias towards the smallest y position.
		/// </summary>
		/// <param name="inputPolygon">The polygon to analyze</param>
		/// <param name="considerAsSameY">Range to treat y positions as the same value.</param>
		/// <param name="startPosition">If two or more angles are similar, choose the one close to the start</param>
		/// <returns>The position that has the largest turn angle</returns>
		public static int FindGreatestTurnIndex(this Polygon inputPolygon,
			long extrusionWidth_um = 3,
			IntPoint? startPosition = null)
		{
			var count = inputPolygon.Count;
			var polygon2 = inputPolygon.Select(i => new Vector2(i.X, i.Y)).ToList();
			var localOffset = new List<(int index, double delta)>(count);

			var positiveGroup = new CandidateGroup(extrusionWidth_um / 2);
			var negativeGroup = new CandidateGroup(extrusionWidth_um / 2);

			var currentFurthestBack = new IntPoint(long.MaxValue, long.MinValue);
			int furthestBackIndex = 0;

			var neighborhood = extrusionWidth_um * 4;

			for (var i = 0; i < count; i++)
			{
				var position = polygon2[i];
				var prevPosition = polygon2[(count + i - 1) % count];
				var nextPosition = polygon2[(count + i + 1) % count];
				var angle = position.GetTurnAmount(prevPosition, nextPosition);
				var lengthToPoint = polygon2.LengthTo(i);

				var leftPosition = polygon2.GetPositionAt(lengthToPoint - neighborhood);
				var rightPosition = polygon2.GetPositionAt(lengthToPoint + neighborhood);
				var nearAngle = position.GetTurnAmount(leftPosition, rightPosition);
				var directionNormal = (rightPosition - leftPosition).GetNormal().GetPerpendicularRight();
				var delta = Vector2.Dot(directionNormal, position - leftPosition);

				var currentPoint = inputPolygon[i];
				if (delta < -extrusionWidth_um / 2)
				{
					negativeGroup.ConditionalAdd(new CandidatePoint(delta, i, currentPoint));
				}
				else if(delta > extrusionWidth_um * 2)
				{
					positiveGroup.ConditionalAdd(new CandidatePoint(delta, i, currentPoint));
				}

				if (currentPoint.Y >= currentFurthestBack.Y)
				{
					if (currentPoint.Y > currentFurthestBack.Y
						|| currentPoint.X < currentFurthestBack.X)
					{
						furthestBackIndex = i;
						currentFurthestBack = currentPoint;
					}
				}

				localOffset.Add((i, delta));
			}

			if (negativeGroup.Count > 0)
			{
				return negativeGroup.GetBestIndex(startPosition);
			}
			else if (positiveGroup.Count > 0)
			{
				return positiveGroup.GetBestIndex(startPosition);
			}
			else
			{
				// If can't find good candidate go with vertex most in a single direction
				return furthestBackIndex;
			}
		}

		public static IntRect GetBounds(this Polygon inPolygon)
		{
			if (inPolygon.Count == 0)
			{
				return new IntRect(0, 0, 0, 0);
			}

			var result = new IntRect
			{
				minX = inPolygon[0].X
			};
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
			var position = new IntPoint();
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
					position = nextPoint;
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
			internal double neighborDelta;
			internal int turnIndex;

			internal CandidatePoint(double neighborDelta, int turnIndex, IntPoint position)
			{
				this.turnIndex = turnIndex;
				this.neighborDelta = neighborDelta;
				this.position = position;
			}
		}

		public class CandidateGroup : List<CandidatePoint>
		{
			private readonly double sameDelta;

			public CandidateGroup(double sameDelta)
			{
				this.sameDelta = sameDelta;
			}

			/// <summary>
			/// Get the best turn for this polygon.
			/// </summary>
			/// <param name="startPosition">If two or more points are similar, choose the one closest to start</param>
			/// <returns></returns>
			public int GetBestIndex(IntPoint? startPosition)
			{
				bool outsideEdge = this[this.Count - 1].neighborDelta > 0;

				if (startPosition == null)
				{
					// sort to the back
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
				else // sort them by distance from start
				{
					this.Sort((a, b) =>
					{
						var distToA = (a.position - startPosition.Value).LengthSquared();
						var distToB = (b.position - startPosition.Value).LengthSquared();
						return distToB.CompareTo(distToA);
					});
				}

				return this[this.Count - 1].turnIndex;
			}

			public void ConditionalAdd(CandidatePoint point)
			{
				// If this is better than our worst point
				// or it is within sameTurn of our best point
				if (Count == 0
					|| Math.Abs(point.neighborDelta) >= Math.Abs(this[Count - 1].neighborDelta)
					|| Math.Abs(point.neighborDelta) >= Math.Abs(this[0].neighborDelta) - sameDelta)
				{
					// remove all points that are worse than the new one
					for (int i = Count - 1; i >= 0; i--)
					{
						if (Math.Abs(this[i].neighborDelta) + sameDelta < Math.Abs(point.neighborDelta))
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
							if (Math.Abs(point.neighborDelta) >= Math.Abs(this[i].neighborDelta))
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