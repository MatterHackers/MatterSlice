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

namespace MatterHackers.MatterSlice
{
	/*
	Floating point 3D points are used during model loading as 3D vectors.
	They represent millimeters in 3D space.
	*/

	public struct Vector3
	{
		public static readonly Vector3 Up = new Vector3(0, 0, 1);

		public double x, y, z;

		public Vector3(double _x, double _y, double _z)
		{
			x = _x;
			y = _y;
			z = _z;
		}

		public Vector3(IntPoint v0)
		{
			this.x = v0.X;
			this.y = v0.Y;
			this.z = v0.Z;
		}

		public static Vector3 operator +(Vector3 left, Vector3 right)
		{
			return new Vector3(left.x + right.x, left.y + right.y, left.z + right.z);
		}

		public static Vector3 operator -(Vector3 left, Vector3 right)
		{
			return new Vector3(left.x - right.x, left.y - right.y, left.z - right.z);
		}

		public static Vector3 operator *(Vector3 p, double d)
		{
			return new Vector3(p.x * d, p.y * d, p.z * d);
		}

		public static Vector3 operator /(Vector3 p, double d)
		{
			return new Vector3(p.x / d, p.y / d, p.z / d);
		}

		public static bool operator ==(Vector3 p0, Vector3 p1)
		{
			return p0.x == p1.x && p0.y == p1.y && p0.z == p1.z;
		}

		public static bool operator !=(Vector3 p0, Vector3 p1)
		{
			return p0.x != p1.x || p0.y != p1.y || p0.z != p1.z;
		}

		private double max()
		{
			if (x > y && x > z) return x;
			if (y > z) return y;
			return z;
		}

		public double LengthSquared()
		{
			return x * x + y * y + z * z;
		}

		public static double CalculateAngle(Vector3 first, Vector3 second)
		{
			return Math.Acos((Vector3.Dot(first, second)) / (first.Length * second.Length));
		}

		public double Length
		{
			get
			{
				return Math.Sqrt(LengthSquared());
			}
		}

		public static double Dot(Vector3 left, Vector3 right)
		{
			return left.x * right.x + left.y * right.y + left.z * right.z;
		}

		public Vector3 Cross(Vector3 p)
		{
			return new Vector3(
				y * p.z - z * p.y,
				z * p.x - x * p.z,
				x * p.y - y * p.x);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Vector3))
				return false;

			return this.Equals((Vector3)obj);
		}

		public bool Equals(Vector3 other)
		{
			return
				x == other.x &&
				y == other.y &&
				z == other.z;
		}

		public override int GetHashCode()
		{
			return new { x, y, z }.GetHashCode();
		}

		private bool testLength(double len)
		{
			return vSize2() <= len * len;
		}

		private double vSize2()
		{
			return x * x + y * y + z * z;
		}

		private double vSize()
		{
			return Math.Sqrt(vSize2());
		}
	}

	public class FMatrix3x3
	{
		public double[,] m = new double[3, 3];

		public FMatrix3x3()
		{
			m[0, 0] = 1.0;
			m[1, 0] = 0.0;
			m[2, 0] = 0.0;
			m[0, 1] = 0.0;
			m[1, 1] = 1.0;
			m[2, 1] = 0.0;
			m[0, 2] = 0.0;
			m[1, 2] = 0.0;
			m[2, 2] = 1.0;
		}

		public FMatrix3x3(string valueToSetTo)
		{
			valueToSetTo = valueToSetTo.Replace("[", "");
			valueToSetTo = valueToSetTo.Replace("]", "");
			string[] values = valueToSetTo.Split(',');

			m[0, 0] = double.Parse(values[0]);
			m[1, 0] = double.Parse(values[1]);
			m[2, 0] = double.Parse(values[2]);
			m[0, 1] = double.Parse(values[3]);
			m[1, 1] = double.Parse(values[4]);
			m[2, 1] = double.Parse(values[5]);
			m[0, 2] = double.Parse(values[6]);
			m[1, 2] = double.Parse(values[7]);
			m[2, 2] = double.Parse(values[8]);
		}

		public IntPoint apply(Vector3 p)
		{
			return new IntPoint(
				(p.x * m[0, 0] + p.y * m[1, 0] + p.z * m[2, 0]),
				(p.x * m[0, 1] + p.y * m[1, 1] + p.z * m[2, 1]),
				(p.x * m[0, 2] + p.y * m[1, 2] + p.z * m[2, 2]));
		}

		public override string ToString()
		{
			return "[[{0},{1},{2}],[{3},{4},{5}],[{6},{7},{8}]]".FormatWith(
				m[0, 0], m[1, 0], m[2, 0],
				m[0, 1], m[1, 1], m[2, 1],
				m[0, 2], m[1, 2], m[2, 2]);
		}
	}
}