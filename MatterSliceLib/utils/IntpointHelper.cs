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

// Include Clipper to get the ClipperLib::IntPoint definition, which we reuse as Point definition.
using System;
using MSClipperLib;
using static System.Math;

namespace MatterHackers.MatterSlice
{
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
	}

;

	public static class IntPointHelper
	{
		public static IntPoint Cross(this IntPoint left, IntPoint right)
		{
			return new IntPoint(
				left.Y * right.Z - left.Z * right.Y,
				left.Z * right.X - left.X * right.Z,
				left.X * right.Y - left.Y * right.X);
		}

		public static long CrossXy(this IntPoint left, IntPoint right)
		{
			return left.X * right.Y - left.Y * right.X;
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

			if (thisPoint.Z > len || thisPoint.Z < -len)
			{
				return false;
			}

			return thisPoint.LengthSquared() <= len * len;
		}

		public static double LengthMm(this IntPoint thisPoint)
		{
			double fx = (double)thisPoint.X / 1000.0;
			double fy = (double)thisPoint.Y / 1000.0;
			double fz = (double)thisPoint.Z / 1000.0;
			return Sqrt(fx * fx + fy * fy + fz * fz);
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
				return new IntPoint(len, 0)
				{
					Width = thisPoint.Width
				};
			}

			return thisPoint * len / _len;
		}

		public static string OutputInMm(this IntPoint thisPoint)
		{
			return string.Format("[{0},{1},{2}]", thisPoint.X / 1000.0, thisPoint.Y / 1000.0, thisPoint.Z / 1000.0);
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

			return MSClipperLib.IntPointExtensions.LengthSquared(polygon) <= length * length;
		}

		public static bool ShorterThen(this IntPoint thisPoint, long len)
		{
			if (thisPoint.X > len || thisPoint.X < -len)
			{
				return false;
			}

			if (thisPoint.Y > len || thisPoint.Y < -len)
			{
				return false;
			}

			if (thisPoint.Z > len || thisPoint.Z < -len)
			{
				return false;
			}

			return thisPoint.LengthSquared() <= len * len;
		}
	}
}