/*
Copyright (c) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
*/

/*
The integer point classes are used as soon as possible and represent microns in 2D or 3D space.
Integer points are used to avoid floating point rounding errors, and because ClipperLib uses them.
*/

//Include Clipper to get the ClipperLib::IntPoint definition, which we reuse as Point definition.
using System;
using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    public struct Point3
    {
        public int x, y, z;

        public Point3(double _x, double _y, double _z)
            : this((int)(_x + .5), (int)(_y + .5), (int)(_z + .5))
        {
        }

        public Point3(int _x, int _y, int _z)
        {
            this.x = _x;
            this.y = _y;
            this.z = _z;
        }

        public static Point3 operator +(Point3 left, Point3 right)
        {
            return new Point3(left.x + right.x, left.y + right.y, left.z + right.z);
        }

        public static Point3 operator -(Point3 left, Point3 right)
        {
            return new Point3(left.x - right.x, left.y - right.y, left.z - right.z);
        }

        public static Point3 operator /(Point3 point, int i)
        {
            return new Point3(point.x / i, point.y / i, point.z / i);
        }

        public static bool operator ==(Point3 left, Point3 right)
        {
            return left.x == right.x && left.y == right.y && left.z == right.z;
        }

        public static bool operator !=(Point3 left, Point3 right)
        {
            return left.x != right.x || left.y != right.y || left.z != right.z;
        }

        public override int GetHashCode()
        {
            return new { x, y, z }.GetHashCode();
        }

        public int max()
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

        public bool testLength(long len)
        {
            if (x > len || x < -len)
                return false;
            if (y > len || y < -len)
                return false;
            if (z > len || z < -len)
                return false;
            return vSize2() <= len * len;
        }

        public long vSize2()
        {
            return (long)x * (long)x + (long)y * (long)y + (long)z * (long)z;
        }

        public int vSize()
        {
            return (int)Math.Sqrt(vSize2());
        }

        public Point3 cross(Point3 p)
        {
            return new Point3(
                y * p.z - z * p.y,
                z * p.x - x * p.z,
                x * p.y - y * p.x);
        }
    }

    static class IntPointHelper
    {
        public static bool IsShorterThen(this IntPoint p0, long len)
        {
            if (p0.X > len || p0.X < -len)
            {
                return false;
            }

            if (p0.Y > len || p0.Y < -len)
            {
                return false;
            }

            return p0.LengthSquared() <= len * len;
        }

        public static long Length(this IntPoint p0)
        {
            return (long)Math.Sqrt(p0.LengthSquared());
        }
        
        public static long LengthSquared(this IntPoint p0)
        {
            return p0.X * p0.X + p0.Y * p0.Y;
        }

        public static long Cross(this IntPoint left, IntPoint right)
        {
            return left.X * right.Y - left.Y * right.X;
        }

        public static string OutputInMm(this IntPoint p0)
        {
            return string.Format("[{0},{1}]", p0.X / 1000.0, p0.Y / 1000.0);
        }

        public static IntPoint SetLength(this IntPoint p0, long len)
        {
            long _len = p0.Length();
            if (_len < 1)
            {
                return new IntPoint(len, 0);
            }

            return p0 * len / _len;
        }

        public static bool shorterThen(this IntPoint p0, long len)
        {
            if (p0.X > len || p0.X < -len)
                return false;
            if (p0.Y > len || p0.Y < -len)
                return false;
            return p0.LengthSquared() <= len * len;
        }

        public static int vSize(this IntPoint p0)
        {
            return (int)Math.Sqrt(p0.LengthSquared());
        }

        public static double LengthMm(this IntPoint p0)
        {
            double fx = (double)(p0.X) / 1000.0;
            double fy = (double)(p0.Y) / 1000.0;
            return Math.Sqrt(fx * fx + fy * fy);
        }

        public static IntPoint normal(this IntPoint p0, int len)
        {
            int _len = p0.vSize();
            if (_len < 1)
            {
                return new IntPoint(len, 0);
            }

            return p0 * len / _len;
        }

        public static IntPoint GetPerpendicularLeft(this IntPoint p0)
        {
            return new IntPoint(-p0.Y, p0.X);
        }

        public static long Dot(this IntPoint p0, IntPoint p1)
        {
            return p0.X * p1.X + p0.Y * p1.Y;
        }
    }

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
            matrix[0] = Math.Cos(rotation);
            matrix[1] = -Math.Sin(rotation);
            matrix[2] = -matrix[1];
            matrix[3] = matrix[0];
        }

        public PointMatrix(IntPoint p)
        {
            matrix[0] = p.X;
            matrix[1] = p.Y;
            double f = Math.Sqrt((matrix[0] * matrix[0]) + (matrix[1] * matrix[1]));
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
}