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

using ClipperLib;

//Include Clipper to get the ClipperLib::IntPoint definition, which we reuse as Point definition.
using System;
using System.Collections.Generic;

using static System.Math;

namespace MatterHackers.MatterSlice
{
	using Newtonsoft.Json;
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public struct Point3
	{
		public long x, y, z;

		[JsonIgnore]
		public IntPoint XYPoint { get { return new IntPoint(x, y); } }

		public override string ToString()
		{
			return "x:{0} y:{1} z:{2}".FormatWith(x, y, z);
		}

		public Point3(IntPoint xy, long z)
		{
			this.x = xy.X;
			this.y = xy.Y;
			this.z = z;
		}

		public Point3(double _x, double _y, double _z)
			: this(Convert.ToInt64(_x), Convert.ToInt64(_y), Convert.ToInt64(_z))
		{
		}

		public Point3(long _x, long _y, long _z = 0)
		{
			this.x = _x;
			this.y = _y;
			this.z = _z;
		}

		public static Point3 operator -(Point3 left, Point3 right)
		{
			return new Point3(left.x - right.x, left.y - right.y, left.z - right.z);
		}

		public static Point3 operator *(Point3 left, long right)
		{
			return new Point3(left.x * right, left.y * right, left.z * right);
		}

		public static Point3 operator /(Point3 left, long right)
		{
			return new Point3(left.x / right, left.y / right, left.z / right);
		}

		public static bool operator !=(Point3 left, Point3 right)
		{
			return left.x != right.x || left.y != right.y || left.z != right.z;
		}

		public static Point3 operator /(Point3 point, int i)
		{
			return new Point3(Convert.ToInt64(point.x / (double)i), Convert.ToInt64(point.y / (double)i), Convert.ToInt64(point.z / (double)i));
		}

		public static Point3 operator +(Point3 left, Point3 right)
		{
			return new Point3(left.x + right.x, left.y + right.y, left.z + right.z);
		}

		public bool ShorterThen(long len)
		{
			if (x > len || x < -len)
				return false;
			if (y > len || y < -len)
				return false;
			if (z > len || z < -len)
				return false;
			return LengthSquared() <= len * len;
		}

		public static bool operator ==(Point3 left, Point3 right)
		{
			return left.x == right.x && left.y == right.y && left.z == right.z;
		}

		public bool AbsLengthLEQ(long minLength)
		{
			if (x > minLength || x < -minLength)
				return false;
			if (y > minLength || y < -minLength)
				return false;
			if (z > minLength || z < -minLength)
				return false;
			return LengthSquared() <= minLength * minLength;
		}

		public long Dot(Point3 p)
		{
			return this.x * p.x + this.y * p.y + this.z * p.z;
		}

		public Point3 Cross(Point3 p)
		{
			return new Point3(
				y * p.z - z * p.y,
				z * p.x - x * p.z,
				x * p.y - y * p.x);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Point3))
			{
				return false;
			}

			return this == (Point3)obj;
		}

		public override int GetHashCode()
		{
			return new { x, y, z }.GetHashCode();
		}


		public double LengthMm()
		{
			return Sqrt(LengthSquared()) / 1000.0;
		}

		public long Length()
		{
			return (long)Sqrt(LengthSquared());
		}

		public long LengthSquared()
		{
			return (long)x * (long)x + (long)y * (long)y + (long)z * (long)z;
		}

		public long max()
		{
			if (x > y && x > z)
			{
				return x;
			}
			if (y > z)
			{
				return y;
			}

			return z;
		}
	}
}