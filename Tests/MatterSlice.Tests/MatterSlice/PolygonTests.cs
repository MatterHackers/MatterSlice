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

using MSClipperLib;
using NUnit.Framework;
using MatterHackers.MatterSlice;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice.Tests
{
	using System;
	using Polygon = List<MSClipperLib.IntPoint>;
	using System.Reflection;

	[TestFixture, Category("MatterSlice.PolygonHelpers")]
	public class PolygonHelperTests
	{
		// currently this tests the 'area', 'centerOfMass', and 'inside'
		// operations.

		//using ClipperLib::IntPoint;
		//using ClipperLib::cInt;

		private int N_test_poly = test_poly_points.Length / test_poly_points[0].Length;

		// Thes are the points to be tested
		// All are either inside, or outside the polygon; but several are very
		// close; also, many are quite far, but have x or y coordinates which happen to match
		// points on the polygon.
		//
		private static int[][] test_points_inside = new int[][]
		{
			new int[] {28965, 26594 },        // barely inside
			new int[] {28964, 26595 },        // barely inside
			new int[] {28421, 58321},
			new int[] { 50082, 50081},
			new int[] { 50081, 50026},
			new int[] { 50083, 58212},
			new int[] { 71021, 82391},
			new int[] { 36321, 82390},
			new int[] { 36320, 82390},
			new int[] { 50081, 50023},
			new int[] { 28421, 82390},
			new int[] { 50081,  2324}     // in a corner, inside
		};

		private static int[][] test_points_outside = new int[][]
		{
			new int[] {28964, 26594 },        // barely outside
			new int[] {28421, 20123},
			new int[] { 20621, 82391},
			new int[] { 50082, 82391},
			new int[] {92192, 58212 },
			new int[] {92191, 98307 },
			new int[] {92192, 98306 },
			new int[] {92193, 98305 },
			new int[] { 36322, 82391},
			new int[] { 36322, 82392},
			new int[] { 20620, 35000},        // wholly outside at -X
			new int[] { 92194, 59000},        // wholly outside at +X
			new int[] { 50083, 50023},        // in a corner but outside
			new int[] { 50083,  2324},
			new int[] { 28420, 82390}
		};

		//
		// This is a polgon for the test.
		// All X, Y coords are in the range 0 .. 100000
		// It gets transformed in various ways to increase test coverage
		// (this causes the same 'close calls' to appear in different situations,
		// increasing Y, decreasing Y; negative/positive values, etc)
		//
		private static int[][] test_poly_points = new int[][]
		{
			new int[] { 20621, 34081},
			new int[] { 38410, 18119},
			new int[] { 50082,  2321},
			new int[] { 50082, 50026},
			new int[] { 71021, 42192},
			new int[] { 92192, 73212},
			new int[] { 71021, 98306},
			new int[] { 50082, 58212},
			new int[] { 36321, 82391},
			new int[] { 28421, 82391}
		};

		// this table defines how the area of a test polygon depends on the lowest
		// 4 bits of the transform code
		//
		private static double[] transform_area_factor = new double[]
		{
			1.0,  -1.0,  2.0,  4.0,
			-1.0,  1.0, -2.0, -4.0,
			-1.0,  1.0, -2.0, -4.0,
			1.0,  -1.0,  2.0,  4.0 
		};

		//
		// The area of the test poly, as a reference
		//
		private double RefArea;

		//
		// The 'center of mass', as a reference
		//
		private MSClipperLib.IntPoint RefCenterOfMass;

		public void findRefValues()
		{
			double area_sum = 0;
			double com_sum_x = 0;
			double com_sum_y = 0;
			int n = N_test_poly;
			// choose a 'middle' reference point. This doesn't affect the result
			// except for rounding errors, which are improved when this value
			// is within, or in the neighborhood of the polgon.
			int xmid = 50000;
			int ymid = 50000;

			for (int i = 0, j = n - 1; i < n; j = i, i++)
			{
				double p0x = (double)test_poly_points[j][0] - xmid;
				double p0y = (double)test_poly_points[j][1] - ymid;
				double p1x = (double)test_poly_points[i][0] - xmid;
				double p1y = (double)test_poly_points[i][1] - ymid;
				// This calculation is twice the area of the triangle
				// formed with mid, p0, p1
				double tarea = p0x * p1y - p0y * p1x;
				area_sum += tarea;
				// the centroid of the triangle (relative to 'mid') is
				// 1/3 the sum of p0 and p1. Make a sum of these
				// weighted by area.
				com_sum_x += (p0x + p1x) * tarea;
				com_sum_y += (p0y + p1y) * tarea;
			}

			RefArea = 0.5 * area_sum;

			int com_x = xmid + (int)(Math.Floor(0.5 + com_sum_x / (3.0 * area_sum)));
			int com_y = ymid + (int)(Math.Floor(0.5 + com_sum_y / (3.0 * area_sum)));

			RefCenterOfMass = new MSClipperLib.IntPoint(com_x, com_y);
		}

		[Test, Ignore("Not Finished")]
		public void PolygonHelperTestCases()
		{
			findRefValues();
			for (int i = 0; i < 64; i++)
			{
				polygonTest(i);
			}
		}

		public void polygonTest(int xform_code)
		{
			Polygon test_path = new Polygon();

			for (int i = 0; i < N_test_poly; i++)
			{
				test_path.Add((MSClipperLib.IntPoint)transformPoint(new MSClipperLib.IntPoint(test_poly_points[i][0], test_poly_points[i][1]), xform_code));
			}

			Polygon tpoly = new Polygon(test_path);

			// check area
			//
			double found_area = tpoly.Area();
			double ref_area = RefArea * transform_area_factor[xform_code & 15];
			Assert.IsTrue(found_area == ref_area);

			// check center of mass
			MSClipperLib.IntPoint found_com = PolygonHelper.CenterOfMass(tpoly);
			MSClipperLib.IntPoint ref_com = transformPoint(RefCenterOfMass, xform_code);
			Assert.IsTrue(Math.Abs(found_com.X - ref_com.X) <= 1);
			Assert.IsTrue(Math.Abs(found_com.Y - ref_com.Y) <= 1);

			// check 'inside' points.
			for (int i = 0; i < test_points_inside.Length / test_points_inside[0].Length; i++)
			{
				MSClipperLib.IntPoint tpt = transformPoint(new MSClipperLib.IntPoint(test_points_inside[i][0], test_points_inside[i][1]), xform_code);
				Assert.IsTrue(tpoly.Inside(tpt));
			}

			for (int i = 0; i < test_points_outside.Length / test_points_outside[0].Length; i++)
			{
				MSClipperLib.IntPoint tpt = transformPoint(new MSClipperLib.IntPoint(test_points_outside[i][0], test_points_outside[i][1]), xform_code);
				Assert.IsFalse(tpoly.Inside(tpt));
			}
		}

		//
		// transform a point according to a transform code
		// Operations are as follows (and in the following order):
		//   if bits [1:0] are 11: double X and Y
		//   if bit 4 : subtract 100000 from X
		//   if bit 5 : subtract 100000 from Y
		//   if bit 2 : X -> -X
		//   if bit 3 : Y -> -Y
		//   according to bits 1,0:
		//      00 nothing
		//      01 swap X,y
		//      10 {X,Y}  <- {X+Y, X-Y}
		//      11 (nothing)
		// The operations are contrived so that the change in area depends on bits 3,2,1,0 only.
		//
		private static IntPoint transformPoint(MSClipperLib.IntPoint p, int transform_code)
		{
			if ((transform_code & 3) == 3)
			{
				p.X *= 2;
				p.Y *= 2;
			}
			if ((transform_code & 0x10) != 0) p.X -= 100000;
			if ((transform_code & 0x20) != 0) p.Y -= 100000;
			if ((transform_code & 4) != 0) p.X = -p.X;
			if ((transform_code & 8) != 0) p.Y = -p.Y;
			switch ((transform_code & 3))
			{
				case 0:
					break;

				case 1:
					long temp = p.Y;
					p.Y = p.X;
					p.X = temp;
					break;

				case 2:
					{
						MSClipperLib.IntPoint tmp = p; // rotate 45 and scale by sqrt(2)
						p.X = tmp.X - tmp.Y;
						p.Y = tmp.X + tmp.Y;
					}
					break;

				case 3:
				default:
					break;
			}
			return p;
		}
	}
}