/*
Copyright (c) 2013, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using ClipperLib;

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

        public int GetMaxElement()
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

        public bool testLength(int len)
        {
            if (x > len || x < -len)
            {
                return false;
            }

            if (y > len || y < -len)
            {
                return false;
            }

            if (z > len || z < -len)
            {
                return false;
            }

            return LengthSquared() <= len * len;
        }

        public long LengthSquared()
        {
            return (long)x * (long)x + (long)y * (long)y + (long)z * (long)z;
        }

        public int Length()
        {
            return (int)Math.Sqrt(LengthSquared());
        }

        public static Point3 cross(Point3 p0, Point3 p1)
        {
            return new Point3(
                p0.y * p1.z - p0.z * p1.y,
                p0.z * p1.x - p0.x * p1.z,
                p0.x * p1.y - p0.y * p1.x);
        }

        public override string ToString()
        {
            return string.Format("x:{0:.1}, y:{1:.1}, z:{2:.1}", x, y, z);
        }
    }

    public class PointMatrix
    {
        double[] matrix = new double[4];

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
            return new IntPoint((long)(p.X * matrix[0] + p.Y * matrix[1]), (long)(p.X * matrix[2] + p.Y * matrix[3]));
        }

        public IntPoint unapply(IntPoint p)
        {
            return new IntPoint((long)(p.X * matrix[0] + p.Y * matrix[2]), (long)(p.X * matrix[1] + p.Y * matrix[3]));
        }

        public void apply(Polygons polys)
        {
            for (int i = 0; i < polys.Count; i++)
            {
                for (int j = 0; j < polys[i].Count; j++)
                {
                    polys[i][j] = apply(polys[i][j]);
                }
            }
        }
    }

    /* Axis aligned boundary box */
    public class AABB
    {
        public IntPoint min;
        public IntPoint max;

        public AABB()
        {
            min = new IntPoint(long.MinValue, long.MinValue);
            max = new IntPoint(long.MaxValue, long.MaxValue);
        }

        public AABB(Polygons polys)
            : this()
        {
            calculate(polys);
        }

        public void calculate(Polygons polys)
        {
            max = new IntPoint(long.MinValue, long.MinValue);
            min = new IntPoint(long.MaxValue, long.MaxValue);
            for (int i = 0; i < polys.Count; i++)
            {
                for (int j = 0; j < polys[i].Count; j++)
                {
                    if (min.X > polys[i][j].X) min.X = polys[i][j].X;
                    if (min.Y > polys[i][j].Y) min.Y = polys[i][j].Y;
                    if (max.X < polys[i][j].X) max.X = polys[i][j].X;
                    if (max.Y < polys[i][j].Y) max.Y = polys[i][j].Y;
                }
            }
        }

        public bool hit(AABB other)
        {
            if (max.X < other.min.X) return false;
            if (min.X > other.max.X) return false;
            if (max.Y < other.min.Y) return false;
            if (min.Y > other.max.Y) return false;
            return true;
        }
    }
}