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

/*
The integer point classes are used as soon as possible and represent microns in 2D or 3D space.
Integer points are used to avoid floating point rounding errors, and because ClipperLib uses them.
*/

using MatterSlice.ClipperLib;

//Include Clipper to get the ClipperLib::IntPoint definition, which we reuse as Point definition.
using System;
using System.Collections.Generic;

using static System.Math;

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public class PointMatrix
	{
		public double[] matrix = new double[4];

		public PointMatrix()
		{
			matrix[0] = 1;
			matrix[1] = 0;
			matrix[2] = 0;
			matrix[3] = 1;
		}

		public PointMatrix(double rotation)
		{
			rotation = rotation / 180 * Math.PI;
			matrix[0] = Cos(rotation);
			matrix[1] = -Sin(rotation);
			matrix[2] = -matrix[1];
			matrix[3] = matrix[0];
		}

		public PointMatrix(IntPoint p)
		{
			matrix[0] = p.X;
			matrix[1] = p.Y;
			double f = Sqrt((matrix[0] * matrix[0]) + (matrix[1] * matrix[1]));
			matrix[0] /= f;
			matrix[1] /= f;
			matrix[2] = -matrix[1];
			matrix[3] = matrix[0];
		}

		public IntPoint apply(IntPoint p)
		{
			return new IntPoint(p.X * matrix[0] + p.Y * matrix[1], p.X * matrix[2] + p.Y * matrix[3]);
		}

		public IntPoint unapply(IntPoint p)
		{
			return new IntPoint(p.X * matrix[0] + p.Y * matrix[2], p.X * matrix[1] + p.Y * matrix[3]);
		}
	};

	internal static class IntPointHelper
	{
		public static Polygon CreateConvexHull(Polygons polygons)
		{
			Polygon allPoints = new Polygon();
			foreach (Polygon polygon in polygons)
			{
				allPoints.AddRange(polygon);
			}

			return CreateConvexHull(allPoints);
		}

		// Find the convex hull of a set of (x,y) points.
		// returns a polygon which form the convex hull.
		// Could be:
		//     - empty (if input is empty, or if there is an internal error)
		//     - single point (if all input points are identical)
		//     - 2 points (if >=2 input points, but all collinear)
		//     - 3 or more points forming a convex polygon in CCW order.
		//
		// Returns 0  (or -1 if an internal
		// error occurs, in which case the result will be empty).
		// No case has been found in testing which causes an internal error. It's
		// possible that this could occur if 64-bit multplies and adds overflow.
		public static Polygon CreateConvexHull(Polygon inPolygon)
		{
			Polygon points = new Polygon(inPolygon);
			int count = points.Count;

			if (count <= 2)
			{
				if (count == 2
					&& points[0].X == points[1].X
					&& points[1].Y == points[1].Y)
				{
					// remove one point if two are the same
					points.RemoveAt(1);
				}
				return points;
			}

			// step 1: sort the values in order from min y to max y.
			// (and increasing X where Y are equal)
			points.Sort(new IntPointSorterYX());

			long minY = points[0].Y;
			long maxY = points[count - 1].Y;

			// (2) make a pass, find the min and max x.
			long minX = points[0].X;
			long maxX = minX;
			for (int i = 1; i < count; i++)
			{
				minX = Min(minX, points[i].X);
				maxX = Max(maxX, points[i].X);
			}

			long upperLeftX;
			long upperRightX;

			long bottomLeftY;
			long bottomRightY;

			long bottomLeftX;
			long bottomRightX;

			int leftCount;
			int rightCount;
			int indexOfLowestPoint;

			// OK next step is to find out if there's more than one identical
			// 'y' value at the end of the list. Don't forget to
			// consider the case where all of these are identical...
			{
				int sameYFromEndCount = 1;
				int lastValidIndex = count - 1;
				upperLeftX = upperRightX = points[lastValidIndex].X;
				while (sameYFromEndCount < count && points[lastValidIndex - sameYFromEndCount].Y == maxY)
				{
					sameYFromEndCount++;
				}

				// if more than one, they will be sorted by increasing X.
				// Delete any in the middle.
				//
				if (sameYFromEndCount >= 2)
				{
					int deleteCount = sameYFromEndCount - 2;	// always delete this many...
					upperRightX = points[count - 1].X;
					upperLeftX = points[count - sameYFromEndCount].X;
					if (upperLeftX == upperRightX) deleteCount++;		// delete one more if all the same...
					if (deleteCount > 0)
					{
						points[count - 1 - deleteCount] = points[count - 1];
						count -= deleteCount;
					}
				}
			}

			// We may now have only 1 or 2 points.
			if (count <= 2)
			{
				points = new Polygon();
				return points;
			}

			// same thing at the bottoom
			{
				int sameYFromStartCount = 1;
				bottomLeftX = bottomRightX = points[0].X;
				while (sameYFromStartCount < count && points[sameYFromStartCount].Y == minY)
				{
					sameYFromStartCount++;
				}

				// if more than one, They will be sorted left to right. Delete any in the middle.
				indexOfLowestPoint = 0;
				if (sameYFromStartCount >= 2)
				{
					int deleteCount = sameYFromStartCount - 2;	// always delete this many...
					bottomLeftX = points[0].X;
					bottomRightX = points[sameYFromStartCount - 1].X;
					if (bottomLeftX == bottomRightX)
					{
						deleteCount++; // delete one more if all the same...
					}
					else
					{
						indexOfLowestPoint = 1;			// otherwise we start with 1,
					}

					if (deleteCount > 0)
					{
						for (int i = 0; i < count - (deleteCount + 1); i++)
						{
							points[1 + i] = points[deleteCount + 1 + i];
							count -= deleteCount;
						}
					}
				}

				// OK, we now have the 'UL/UR' points and the 'LL/LR' points.
				// Make a left-side list and a right-side list, each
				// of capacity 'num'.
				// note that 'pleft' is in reverse order...
				// pright grows up from 0, and pleft down from 'num+1',
				// in the same array - they can't overlap.
				Polygon temp_array = new Polygon(points);
				temp_array.Add(new IntPoint());
				temp_array.Add(new IntPoint());
				int pleftIndex = count + 1;
				int prightIndex = 0;

				// set up left and right
				temp_array[pleftIndex] = points[0];
				leftCount = 1;
				temp_array[prightIndex] = points[indexOfLowestPoint];
				rightCount = 1;

				bottomLeftY = bottomRightY = minY;

				for (int ipos = indexOfLowestPoint + 1; ipos < count; ipos++)
				{
					IntPoint pointToCheck = temp_array[ipos];		// get new point.

					// left side test:
					// is the new point is strictly to the left of a line from (bottomLeftX, BottomLeftY) to ( upperLeftX, maxy )?
					if (convex3(upperLeftX, maxY, pointToCheck.X, pointToCheck.Y, bottomLeftX, bottomLeftY))
					{
						// if so, append to the left side list, but first peel off any existing
						// points there which would be  on or inside the new line.
						while (leftCount > 1 && !convex3(pointToCheck, temp_array[pleftIndex - (leftCount - 1)], temp_array[pleftIndex - (leftCount - 2)]))
						{
							--leftCount;
						}
						temp_array[pleftIndex - leftCount] = pointToCheck;
						leftCount++;
						bottomLeftX = pointToCheck.X;
						bottomLeftY = pointToCheck.Y;
					}
					else if (convex3(bottomRightX, bottomRightY, pointToCheck.X, pointToCheck.Y, upperRightX, maxY)) // right side test is the new point strictly to the right of a line from (URx, maxy) to ( DRx, DRy )?
					{
						// if so, append to the right side list, but first peel off any existing
						// points there which would be  on or inside the new line.
						//
						while (rightCount > 1 && !convex3(temp_array[prightIndex + rightCount - 2], temp_array[prightIndex + rightCount - 1], pointToCheck))
						{
							--rightCount;
						}
						temp_array[prightIndex + rightCount] = pointToCheck;
						rightCount++;
						bottomRightX = pointToCheck.X;
						bottomRightY = pointToCheck.Y;
					}
					// if neither of the above are true we throw out the point.
				}

				// now add the 'maxy' points to the lists (it will have failed the insert test)
				temp_array[pleftIndex - leftCount] = new IntPoint(upperLeftX, maxY);
				++leftCount;
				if (upperRightX > upperLeftX)
				{
					temp_array[prightIndex + rightCount] = new IntPoint(upperRightX, maxY);
					++rightCount;
				}
				// now copy the lists to the output area.
				//
				// if both lists have the same lower point, delete one now.
				// (pleft could be empty as a result!)
				if (indexOfLowestPoint == 0)
				{
					--pleftIndex;
					--leftCount;
				}

				// this condition should be true now...
				if (!(leftCount + rightCount <= count))
				{
					// failure... return the original concave list
					return inPolygon;
				}

				// now just pack the pright and pleft lists into the output.
				count = leftCount + rightCount;

				for (int i = 0; i < rightCount; i++)
				{
					points[i] = temp_array[i + prightIndex];
				}

				if (leftCount > 0)
				{
					for (int i = 0; i < leftCount; i++)
					{
						points[i + rightCount] = temp_array[i + pleftIndex - (leftCount - 1)];
					}
				}

				if (points.Count > count)
				{
					Polygon newList = new Polygon();
					newList.AddRange(points.GetRange(0, count));
					points = newList;
				}

				return points;
			}
		}

		public static long Cross(this IntPoint left, IntPoint right)
		{
			return left.X * right.Y - left.Y * right.X;
		}

		public static bool shorterThen(this IntPoint polygon, long length)
		{
			if (polygon.X > length || polygon.X < -length)
			{
				return false;
			}

			if (polygon.Y > length || polygon.Y < -length)
			{
				return false;
			}

			return vSize2(polygon) <= length * length;
		}

		public static long vSize2(this IntPoint polygon)
		{
			return polygon.LengthSquared();
		}

		public static IntPoint CrossZ(this IntPoint thisPoint)
		{
			return new IntPoint(-thisPoint.Y, thisPoint.X);
		}

		public static long Dot(this IntPoint thisPoint, IntPoint p1)
		{
			return thisPoint.X * p1.X + thisPoint.Y * p1.Y;
		}

		public static int GetLineSide(this IntPoint pointToTest, IntPoint start, IntPoint end)
		{
			//It is 0 on the line, and +1 on one side, -1 on the other side.
			long distanceToLine = (end.Y - start.X) * (pointToTest.Y - start.Y) - (end.Y - start.Y) * (pointToTest.X - start.Y);
			if (distanceToLine > 0)
			{
				return 1;
			}
			else if (distanceToLine < 0)
			{
				return -1;
			}

			return 0;
		}

		public static IntPoint GetPerpendicularLeft(this IntPoint thisPoint)
		{
			return new IntPoint(-thisPoint.Y, thisPoint.X);
		}

		public static IntPoint GetRotated(this IntPoint thisPoint, double radians)
		{
			double CosVal, SinVal;

			CosVal = (double)Cos(radians);
			SinVal = (double)Sin(radians);

			IntPoint output;
			output.X = (long)(Round(thisPoint.X * CosVal - thisPoint.Y * SinVal));
			output.Y = (long)(Round(thisPoint.Y * CosVal + thisPoint.X * SinVal));

			return output;
		}

		public static bool IsShorterThen(this IntPoint thisPoint, long len)
		{
			if (thisPoint.X > len || thisPoint.X < -len)
			{
				return false;
			}

			if (thisPoint.Y > len || thisPoint.Y < -len)
			{
				return false;
			}

			return thisPoint.LengthSquared() <= len * len;
		}

		public static long Length(this IntPoint thisPoint)
		{
			return (long)Sqrt(thisPoint.LengthSquared());
		}

		public static double LengthMm(this IntPoint thisPoint)
		{
			double fx = (double)(thisPoint.X) / 1000.0;
			double fy = (double)(thisPoint.Y) / 1000.0;
			return Sqrt(fx * fx + fy * fy);
		}

		public static long LengthSquared(this IntPoint thisPoint)
		{
			return thisPoint.X * thisPoint.X + thisPoint.Y * thisPoint.Y;
		}

		public static bool LongerThen(this IntPoint p0, long len)
		{
			return !ShorterThen(p0, len);
		}

		public static IntPoint Normal(this IntPoint thisPoint, long len)
		{
			long _len = thisPoint.Length();
			if (_len < 1)
			{
				return new IntPoint(len, 0);
			}

			return thisPoint * len / _len;
		}

		public static string OutputInMm(this IntPoint thisPoint)
		{
			return string.Format("[{0},{1}]", thisPoint.X / 1000.0, thisPoint.Y / 1000.0);
		}

		public static IntPoint SetLength(this IntPoint thisPoint, long len)
		{
			long _len = thisPoint.Length();
			if (_len < 1)
			{
				return new IntPoint(len, 0);
			}

			return thisPoint * len / _len;
		}

		public static bool ShorterThen(this IntPoint thisPoint, long len)
		{
			if (thisPoint.X > len || thisPoint.X < -len)
				return false;
			if (thisPoint.Y > len || thisPoint.Y < -len)
				return false;
			return thisPoint.LengthSquared() <= len * len;
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
}