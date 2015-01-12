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
            return new Point3((int)(point.x / i + .5), (int)(point.y / i + .5), (int)(point.z / i + .5));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Point3))
            {
                return false;
            }

            return this == (Point3)obj;
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

        public long LengthSquared()
        {
            return (long)x * (long)x + (long)y * (long)y + (long)z * (long)z;
        }

        public int Length()
        {
            return (int)Math.Sqrt(LengthSquared());
        }

        public Point3 Cross(Point3 p)
        {
            return new Point3(
                y * p.z - z * p.y,
                z * p.x - x * p.z,
                x * p.y - y * p.x);
        }
    }

    static class IntPointHelper
    {
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
            return (long)Math.Sqrt(thisPoint.LengthSquared());
        }
        
        public static long LengthSquared(this IntPoint thisPoint)
        {
            return thisPoint.X * thisPoint.X + thisPoint.Y * thisPoint.Y;
        }

        public static long Cross(this IntPoint left, IntPoint right)
        {
            return left.X * right.Y - left.Y * right.X;
        }

        public static IntPoint CrossZ(this IntPoint thisPoint)
        {
            return new IntPoint(-thisPoint.Y, thisPoint.X);
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

        public static bool LongerThen(this IntPoint p0, long len)
        {
            return !ShorterThen(p0, len);
        }

        public static int vSize(this IntPoint thisPoint)
        {
            return (int)Math.Sqrt(thisPoint.LengthSquared());
        }

        public static double LengthMm(this IntPoint thisPoint)
        {
            double fx = (double)(thisPoint.X) / 1000.0;
            double fy = (double)(thisPoint.Y) / 1000.0;
            return Math.Sqrt(fx * fx + fy * fy);
        }

        public static IntPoint normal(this IntPoint thisPoint, int len)
        {
            int _len = thisPoint.vSize();
            if (_len < 1)
            {
                return new IntPoint(len, 0);
            }

            return thisPoint * len / _len;
        }

        public static IntPoint GetPerpendicularLeft(this IntPoint thisPoint)
        {
            return new IntPoint(-thisPoint.Y, thisPoint.X);
        }

        public static long Dot(this IntPoint thisPoint, IntPoint p1)
        {
            return thisPoint.X * p1.X + thisPoint.Y * p1.Y;
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