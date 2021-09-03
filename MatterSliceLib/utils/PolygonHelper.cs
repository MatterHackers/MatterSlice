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

using System.Collections.Generic;
using System.IO;
using MSClipperLib;
using MatterHackers.Agg;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;
using MatterHackers.VectorMath;
using System;
using static MSClipperLib.CLPolygonExtensions;
using System.Linq;

namespace MatterHackers.MatterSlice
{
	public static class PolygonHelper
	{
		public static IntPoint CenterOfMass(this Polygon polygon)
		{
			IntPoint center = default(IntPoint);
			for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
			{
				center += polygon[positionIndex];
			}

			center /= polygon.Count;
			return center;
		}

		public static Polygon CreateConvexHull(this Polygon inPolygon)
		{
			return new Polygon(GrahamScan.GetConvexHull(inPolygon));
		}

		private static void DiscoverAndAddTurns(Polygon inputPolygon,
			long neighborhood,
			CandidateGroup candidateGroup,
			Func<double, bool> validDelta)
		{
			var polygon2 = inputPolygon.Select(i => new Vector2(i.X, i.Y)).ToList();
			var count = inputPolygon.Count;
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
				if (validDelta(delta))
				{
					candidateGroup.ConditionalAdd(new CandidatePoint(delta, i, currentPoint));
				}
			}
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
			public double SameDelta { get; set; }

			public CandidateGroup(double sameDelta)
			{
				this.SameDelta = sameDelta;
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
					|| Math.Abs(point.neighborDelta) >= Math.Abs(this[0].neighborDelta) - SameDelta)
				{
					// remove all points that are worse than the new one
					for (int i = Count - 1; i >= 0; i--)
					{
						if (Math.Abs(this[i].neighborDelta) + SameDelta < Math.Abs(point.neighborDelta))
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
			SEAM_PLACEMENT seamPlacement = SEAM_PLACEMENT.FURTHEST_BACK,
			IntPoint? startPosition = null)
		{
			var count = inputPolygon.Count;

			var positiveGroup = new CandidateGroup(extrusionWidth_um);
			var negativeGroup = new CandidateGroup(extrusionWidth_um / 4);

			// look for relatively big concave turns
			DiscoverAndAddTurns(inputPolygon,
				extrusionWidth_um * 4,
				negativeGroup,
				delta => delta < -extrusionWidth_um / 2);

			if (negativeGroup.Count == 0)
			{
				// look for relatively big convex turns
				DiscoverAndAddTurns(inputPolygon,
					extrusionWidth_um * 4,
					positiveGroup,
					delta => delta > extrusionWidth_um * 2);

				if (positiveGroup.Count == 0)
				{
					negativeGroup.SameDelta = extrusionWidth_um / 16;
					// look for small concave turns
					DiscoverAndAddTurns(inputPolygon,
						extrusionWidth_um,
						negativeGroup,
						delta => delta < -extrusionWidth_um / 8);

					if (seamPlacement == SEAM_PLACEMENT.RANDOMIZED)
					{
						// look for very small concave turns
						DiscoverAndAddTurns(inputPolygon,
							extrusionWidth_um,
							negativeGroup,
							delta => delta < -extrusionWidth_um / 32);

						// choose at random which point to pick
						if (negativeGroup.Count > 1)
						{
							var selectedPoint = (int)(inputPolygon.GetLongHashCode() % (ulong)negativeGroup.Count);
							var singlePoint = negativeGroup[selectedPoint];
							// remove every point except the random one we want
							negativeGroup.Clear();
							negativeGroup.Add(singlePoint);
						}
					}
				}
			}

			if (negativeGroup.Count > 0)
			{
				return negativeGroup.GetBestIndex(startPosition);
			}
			else if (positiveGroup.Count > 0)
			{
				return positiveGroup.GetBestIndex(startPosition);
			}
			else // there is not really good candidate
			{
				switch (seamPlacement)
				{
					case SEAM_PLACEMENT.CENTERED_IN_BACK:
						// find the point that is most directly behind the center point of this path
						var center = default(IntPoint);
						foreach (var point in inputPolygon)
						{
							center += point;
						}

						center /= count;

						// start with forward
						var bestDeltaAngle = double.MaxValue;
						int bestAngleIndexIndex = 0;
						// get the furthest back index
						for (var i = 0; i < count; i++)
						{
							var direction = inputPolygon[i] - center;
							var deltaAngle = MathHelper.GetDeltaAngle(MathHelper.Range0ToTau(Math.Atan2(direction.Y, direction.X)), Math.PI * .5);

							if (Math.Abs(deltaAngle) < bestDeltaAngle)
							{
								bestAngleIndexIndex = i;
								bestDeltaAngle = Math.Abs(deltaAngle);
							}
						}

						// If can't find good candidate go with vertex most in a single direction
						return bestAngleIndexIndex;

					case SEAM_PLACEMENT.RANDOMIZED:
						return (int)(inputPolygon.GetLongHashCode() % (ulong)inputPolygon.Count);

					case SEAM_PLACEMENT.FURTHEST_BACK:
					default:
						var currentFurthestBack = new IntPoint(long.MaxValue, long.MinValue);
						int furthestBackIndex = 0;
						// get the furthest back index
						for (var i = 0; i < count; i++)
						{
							var currentPoint = inputPolygon[i];

							if (currentPoint.Y >= currentFurthestBack.Y)
							{
								if (currentPoint.Y > currentFurthestBack.Y
									|| currentPoint.X < currentFurthestBack.X)
								{
									furthestBackIndex = i;
									currentFurthestBack = currentPoint;
								}
							}
						}

						// If can't find good candidate go with vertex most in a single direction
						return furthestBackIndex;

					case SEAM_PLACEMENT.FASTEST:
						if (startPosition != null)
						{
							return inputPolygon.FindClosestIndex(startPosition.Value);
						}
						return 0;
				}
			}
		}

		public static ulong GetLongHashCode(this Polygon polygon)
		{
			ulong hash = polygon.Count.GetLongHashCode();
			for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
			{
				var point = polygon[pointIndex];
				hash = point.X.GetLongHashCode(hash);
				hash = Agg.agg_basics.GetLongHashCode(point.X, hash);
				hash = Agg.agg_basics.GetLongHashCode(point.Y, hash);
			}

			return hash;
		}
		public static void SetSpeed(this Polygon polygon, long speed)
		{
			for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
			{
				polygon[pointIndex] = new IntPoint(polygon[pointIndex])
				{
					Speed = speed
				};
			}
		}

		public static bool DescribesSameShape(this Polygon a, Polygon b)
		{
			if (a.Count != b.Count)
			{
				return false;
			}

			// find first same point
			for (int indexB = 0; indexB < b.Count; indexB++)
			{
				if (a[0] == b[indexB])
				{
					// check if any point are different
					for (int indexA = 1; indexA < a.Count; indexA++)
					{
						if (a[indexA] != b[(indexB + indexA) % b.Count])
						{
							return false;
						}
					}

					// they are all the same
					return true;
				}
			}

			return false;
		}

		public static IntPoint getBoundaryPointWithOffset(Polygon poly, int point_idx, long offset)
		{
			IntPoint p0 = poly[(point_idx > 0) ? (point_idx - 1) : (poly.size() - 1)];
			IntPoint p1 = poly[point_idx];
			IntPoint p2 = poly[(point_idx < (poly.size() - 1)) ? (point_idx + 1) : 0];

			IntPoint off0 = (p1 - p0).Normal(1000).CrossZ(); // 1.0 for some precision
			IntPoint off1 = (p2 - p1).Normal(1000).CrossZ(); // 1.0 for some precision
			IntPoint n = (off0 + off1).Normal(-offset);

			return p1 + n;
		}

		public static bool Inside(this Polygon polygon, IntPoint testPoint)
		{
			int positionOnPolygon = Clipper.PointInPolygon(testPoint, polygon);
			if (positionOnPolygon == 0) // not inside or on boundary
			{
				return false;
			}

			return true;
		}

		public static bool IsVertexConcave(this Polygon vertices, int vertex)
		{
			IntPoint current = vertices[vertex];
			IntPoint next = vertices[(vertex + 1) % vertices.Count];
			IntPoint previous = vertices[vertex == 0 ? vertices.Count - 1 : vertex - 1];

			IntPoint left = new IntPoint(current.X - previous.X, current.Y - previous.Y);
			IntPoint right = new IntPoint(next.X - current.X, next.Y - current.Y);

			long cross = (left.X * right.Y) - (left.Y * right.X);

			return cross < 0;
		}

		public static long MinX(this Polygon polygon)
		{
			long minX = long.MaxValue;
			foreach (var point in polygon)
			{
				if (point.X < minX)
				{
					minX = point.X;
				}
			}

			return minX;
		}

		public static void OptimizePolygon(this Polygon polygon)
		{
			IntPoint previousPoint = polygon[polygon.Count - 1];
			for (int i = 0; i < polygon.Count; i++)
			{
				IntPoint currentPoint = polygon[i];
				if ((previousPoint - currentPoint).IsShorterThen(10))
				{
					polygon.RemoveAt(i);
					i--;
				}
				else
				{
					IntPoint nextPoint;
					if (i < polygon.Count - 1)
					{
						nextPoint = polygon[i + 1];
					}
					else
					{
						nextPoint = polygon[0];
					}

					IntPoint diff0 = (currentPoint - previousPoint).SetLength(1000000);
					IntPoint diff2 = (currentPoint - nextPoint).SetLength(1000000);

					long d = diff0.Dot(diff2);
					if (d < -999999000000)
					{
						polygon.RemoveAt(i);
						i--;
					}
					else
					{
						previousPoint = currentPoint;
					}
				}
			}
		}

		public static bool Orientation(this Polygon polygon)
		{
			return Clipper.Orientation(polygon);
		}

		public static void Reverse(this Polygon polygon)
		{
			polygon.Reverse();
		}

		public static Polygons ConvertToLines(this Polygon polygon, bool closedLoop, long minLengthRequired = 0)
		{
			var linePolygons = new Polygons();

			if (minLengthRequired == 0 || polygon.PolygonLengthSquared() > minLengthRequired * minLengthRequired)
			{
				if (polygon.Count > 2)
				{
					int endIndex = closedLoop ? polygon.Count : polygon.Count - 1;
					for (int vertexIndex = 0; vertexIndex < endIndex; vertexIndex++)
					{
						linePolygons.Add(new Polygon() { polygon[vertexIndex], polygon[(vertexIndex + 1) % polygon.Count] });
					}
				}
				else
				{
					linePolygons.Add(polygon);
				}
			}

			return linePolygons;
		}

		public static void SaveToGCode(this Polygon polygon, string filename)
		{
			double scale = 1000;
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer segments\n");
			int extrudeAmount = 0;
			double firstX = 0;
			double firstY = 0;
			for (int intPointIndex = 0; intPointIndex < polygon.Count; intPointIndex++)
			{
				double x = (double)polygon[intPointIndex].X / scale;
				double y = (double)polygon[intPointIndex].Y / scale;
				if (intPointIndex == 0)
				{
					firstX = x;
					firstY = y;
					stream.Write("G1 X{0} Y{1}\n", x, y);
				}
				else
				{
					stream.Write("G1 X{0} Y{1} E{2}\n", x, y, ++extrudeAmount);
				}
			}

			stream.Write("G1 X{0} Y{1} E{2}\n", firstX, firstY, ++extrudeAmount);

			stream.Close();
		}

		public static int size(this Polygon polygon)
		{
			return polygon.Count;
		}

		// true if p0 -> p1 -> p2 is strictly convex.
		private static bool convex3(long x0, long y0, long x1, long y1, long x2, long y2)
		{
			return (y1 - y0) * (x1 - x2) > (x0 - x1) * (y2 - y1);
		}

		private static bool convex3(IntPoint p0, IntPoint p1, IntPoint p2)
		{
			return convex3(p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y);
		}

		// operator to sort IntPoint by y
		// (and then by X, where Y are equal)
		public class IntPointSorterYX : IComparer<IntPoint>
		{
			public virtual int Compare(IntPoint a, IntPoint b)
			{
				if (a.Y == b.Y)
				{
					return a.X.CompareTo(b.X);
				}
				else
				{
					return a.Y.CompareTo(b.Y);
				}
			}
		}
	}

	// Axis aligned boundary box
	public class Aabb
	{
		public IntPoint min, max;

		public Aabb()
		{
			min = new IntPoint(long.MinValue, long.MinValue);
			max = new IntPoint(long.MinValue, long.MinValue);
		}

		public Aabb(Polygons polys)
		{
			min = new IntPoint(long.MinValue, long.MinValue);
			max = new IntPoint(long.MinValue, long.MinValue);
			Calculate(polys);
		}

		public void Calculate(Polygons polys)
		{
			min = new IntPoint(long.MaxValue, long.MaxValue);
			max = new IntPoint(long.MinValue, long.MinValue);
			for (int i = 0; i < polys.Count; i++)
			{
				for (int j = 0; j < polys[i].Count; j++)
				{
					if (min.X > polys[i][j].X)
					{
						min.X = polys[i][j].X;
					}

					if (min.Y > polys[i][j].Y)
					{
						min.Y = polys[i][j].Y;
					}

					if (max.X < polys[i][j].X)
					{
						max.X = polys[i][j].X;
					}

					if (max.Y < polys[i][j].Y)
					{
						max.Y = polys[i][j].Y;
					}
				}
			}
		}

		public bool Hit(Aabb other)
		{
			if (max.X < other.min.X)
			{
				return false;
			}

			if (min.X > other.max.X)
			{
				return false;
			}

			if (max.Y < other.min.Y)
			{
				return false;
			}

			if (min.Y > other.max.Y)
			{
				return false;
			}

			return true;
		}

		public void Expand(long amount)
		{
			min.X -= amount;
			min.Y -= amount;
			max.X += amount;
			max.Y += amount;
		}
	}
}