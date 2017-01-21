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

namespace MSClipperLib
{
	using System;
	using static System.Math;

	public static class IntPointExtensions
	{
		public static IntPoint CrossZ(this IntPoint thisPoint)
		{
			return new IntPoint(-thisPoint.Y, thisPoint.X);
		}

		public static long Dot(this IntPoint thisPoint, IntPoint p1)
		{
			return thisPoint.X * p1.X + thisPoint.Y * p1.Y + thisPoint.Z * p1.Z;
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

		public static int GetLineSideXY(this IntPoint pointToTest, IntPoint start, IntPoint end)
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

		public static IntPoint GetRotated(this IntPoint thisPoint, double radians)
		{
			double cos = (double)Cos(radians);
			double sin = (double)Sin(radians);

			IntPoint output;
			output.X = (long)(Round(thisPoint.X * cos - thisPoint.Y * sin));
			output.Y = (long)(Round(thisPoint.Y * cos + thisPoint.X * sin));
			output.Z = thisPoint.Z;
			output.Width = thisPoint.Width;

			return output;
		}

		public static double GetTurnAmount(this IntPoint currentPoint,  IntPoint prevPoint, IntPoint nextPoint)
		{
			if (prevPoint != currentPoint
				&& currentPoint != nextPoint
				&& nextPoint != prevPoint)
			{
				prevPoint = currentPoint - prevPoint;
				nextPoint -= currentPoint;

				double prevAngle = Math.Atan2(prevPoint.Y, prevPoint.X);
				IntPoint rotatedPrev = prevPoint.GetRotated(-prevAngle);

				// undo the rotation
				nextPoint = nextPoint.GetRotated(-prevAngle);
				double angle = Math.Atan2(nextPoint.Y, nextPoint.X);

				return angle;
			}

			return 0;
		}

		public static IntPoint GetPerpendicularLeft(this IntPoint thisPoint)
		{
			return new IntPoint(-thisPoint.Y, thisPoint.X)
			{
				Width = thisPoint.Width
			};
		}

		public static IntPoint GetPerpendicularLeftXY(this IntPoint thisPoint)
		{
			return new IntPoint(-thisPoint.Y, thisPoint.X, thisPoint.Z);
		}

		public static IntPoint GetPerpendicularRight(this IntPoint thisPoint)
		{
			return new IntPoint(thisPoint.Y, -thisPoint.X)
			{
				Width = thisPoint.Width
			};
		}

		public static IntPoint GetPerpendicularRightXY(this IntPoint thisPoint)
		{
			return new IntPoint(thisPoint.Y, -thisPoint.X, thisPoint.Z);
		}

		public static long Length(this IntPoint thisPoint)
		{
			return (long)Sqrt(thisPoint.LengthSquared());
		}

		public static long LengthSquared(this IntPoint thisPoint)
		{
			return thisPoint.X * thisPoint.X + thisPoint.Y * thisPoint.Y + thisPoint.Z * thisPoint.Z;
		}
	}
}