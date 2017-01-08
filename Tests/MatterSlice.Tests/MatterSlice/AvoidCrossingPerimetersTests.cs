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
using System.Collections.Generic;

namespace MatterHackers.MatterSlice.Tests
{
	using Polygons = List<List<IntPoint>>;
	using Polygon = List<IntPoint>;
	using System;
	[TestFixture, Category("MatterSlice")]
	public class AvoidCrossingPerimetersTests
	{
		[Test]
		public void InsideOutsidePoints()
		{
			{
				// a square with a hole (outside is ccw inside is cw)
				// __________
				// | _____  |
				// | |    | |
				// | |____| |
				// |________|
				string partOutlineString = "x:0, y:0,x:1000, y:0,x:1000, y:1000,x:0, y:1000,|x:100, y:100,x:0, y:900,x:900, y:900,x:900, y:0,|";
				Polygons boundaryPolygons = PolygonsHelper.CreateFromString(partOutlineString);
				AvoidCrossingPerimeters testHarness = new AvoidCrossingPerimeters(boundaryPolygons, 0);
				Assert.IsTrue(testHarness.PointIsInsideBoundary(new IntPoint(1, 1)));
			}

			{
				// ______________2__
				// |               |
				// 3               |
				// |               |
				// |               |
				// |               1
				// |__0____________|

				Polygon test = new Polygon();
				test.Add(new IntPoint(0, 0));
				test.Add(new IntPoint(40, 0));
				test.Add(new IntPoint(40, 40));
				test.Add(new IntPoint(0, 40));

				Polygons boundaryPolygons = new Polygons();
				boundaryPolygons.Add(test);

				// test moving across the lower part
				{
					IntPoint startPoint = new IntPoint(-10, 5);
					IntPoint endPoint = new IntPoint(50, 5);
					AvoidCrossingPerimeters testHarness = new AvoidCrossingPerimeters(boundaryPolygons, 0);

					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(new IntPoint(-1, 5)));
					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(new IntPoint(-1, 5), testHarness.OutlineEdgeQuadTrees));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(1, 5)));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(1, 5), testHarness.OutlineEdgeQuadTrees));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(0, 5)));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(0, 5), testHarness.OutlineEdgeQuadTrees));

					Polygon insidePath = new Polygon();
					Tuple<int, int, IntPoint> outPoint;
					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(startPoint));
					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(startPoint, testHarness.OutlineEdgeQuadTrees));

					// move startpoint inside
					testHarness.OutlinePolygons.MovePointInsideBoundary(startPoint, out outPoint);
					Assert.AreEqual(new IntPoint(0, 5),  outPoint.Item3);
					testHarness.OutlinePolygons.MovePointInsideBoundary(startPoint, out outPoint, testHarness.OutlineEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(startPoint, out outPoint);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(startPoint, out outPoint, testHarness.BoundaryEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);

					// move endpoint inside
					testHarness.OutlinePolygons.MovePointInsideBoundary(endPoint, out outPoint);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);
					testHarness.OutlinePolygons.MovePointInsideBoundary(endPoint, out outPoint, testHarness.OutlineEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(endPoint, out outPoint);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(endPoint, out outPoint, testHarness.BoundaryEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);

					Assert.AreEqual(2, insidePath.Count);
					Assert.AreEqual(new IntPoint(0, 5), insidePath[0]);
					Assert.AreEqual(new IntPoint(40,5), insidePath[1]);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(0, 5), insidePath[0]);
					Assert.AreEqual(boundaryPolygons[0][0], insidePath[1]);
					Assert.AreEqual(boundaryPolygons[0][1], insidePath[2]);
					Assert.AreEqual(new IntPoint(40, 5), insidePath[4]);
				}

				// test being just below the lower line
				{
					IntPoint startPoint = new IntPoint(10, -1);
					IntPoint endPoint = new IntPoint(30, -1);
					AvoidCrossingPerimeters testHarness = new AvoidCrossingPerimeters(boundaryPolygons, 0);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.AreEqual(2, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(10, 0), insidePath[0]);
					Assert.AreEqual(new IntPoint(30, 0), insidePath[1]);
				}
			}

			{
				// Run a bunch of paths against a known test shape
				// _____________________4______         ________________________________0________ (644, 415)
				// | (85, 117)     (338, 428) |         | (400, 421)                            |
				// |                          |         1                                       |
				// |                          |         |                                       |
				// 5                          |         |                                       |
				// |                          3         |                                       |
				// |               (344, 325) |_______2_| (399, 324)                            |
				// |                                                                            |
				// |                                                                            |
				// |      ___1____                    404, 291   ___0____                       |
				// |      |       _________                      |       _________              |
				// |      |               |                      |               | 591, 236		|
				// |      |               2                      |               1              |
				// |       |             |                       |             |                |
				// |       0             |                       3             |                |
				// |       |       ____3_|                        |       ____2_| 587, 158		|
				// |       |_______                     406, 121  |_______                      7
				// |                                                                            |
				// |__6_________________________________________________________________________|
				//  (98, 81)                                                          (664, 75)
				//
				string partOutlineString = "x: 644, y: 415,x: 400, y: 421,x: 399, y: 324,x: 344, y: 325,x: 338, y: 428,x: 85, y: 417,x: 98, y: 81,x: 664, y: 75,";
				partOutlineString += "| x:404, y: 291,x: 591, y: 236,x: 587, y: 158,x: 406, y: 121,";
				partOutlineString += "| x:154, y: 162,x: 159, y: 235,x: 343, y: 290,x: 340, y: 114,|";
				Polygons boundaryPolygons = PolygonsHelper.CreateFromString(partOutlineString);
				{
					IntPoint startPoint = new IntPoint(672, 435);
					IntPoint endPoint = new IntPoint(251, 334);
					AvoidCrossingPerimeters testHarness = new AvoidCrossingPerimeters(boundaryPolygons, 0);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.AreEqual(5, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(boundaryPolygons[0][1], insidePath[0]);
					// next collide with edge 1
					Assert.AreEqual(new IntPoint(400, 365), insidePath[1]);
					// the next 3 points are the is the 2 - 3 index
					Assert.AreEqual(boundaryPolygons[0][2], insidePath[2]);
					Assert.AreEqual(boundaryPolygons[0][3], insidePath[3]);
					// the last point is created on the 3 edge
					Assert.AreEqual(new IntPoint(343, 353), insidePath[4]);
				}
			}

			// Here is a test case that was failing.
			{
				// Looks a little like this
				// _____
				// |   |
				// | O |
				// | O |
				// |___|

				string partOutlineString = "x:90501, y:80501,x:109500, y:80501,x:109500, y:119500,x:90501, y:119500,|x:97387, y:104041,x:95594, y:105213,x:94278, y:106903,x:93583, y:108929,x:93583, y:111071,x:94278, y:113097,x:95594, y:114787,x:97387, y:115959,x:99464, y:116485,x:101598, y:116307,x:103559, y:115447,x:105135, y:113996,x:106154, y:112113,x:106507, y:110000,x:106154, y:107887,x:105135, y:106004,x:103559, y:104553,x:101598, y:103693,x:99464, y:103515,|x:97387, y:84042,x:95594, y:85214,x:94278, y:86904,x:93583, y:88930,x:93583, y:91072,x:94278, y:93098,x:95594, y:94788,x:97387, y:95960,x:99464, y:96486,x:101598, y:96308,x:103559, y:95448,x:105135, y:93997,x:106154, y:92114,x:106507, y:90001,x:106154, y:87888,x:105135, y:86005,x:103559, y:84554,x:101598, y:83694,x:99464, y:83516,|";
				Polygons boundaryPolygons = PolygonsHelper.CreateFromString(partOutlineString);
				IntPoint startPoint = new IntPoint(95765, 114600);
				IntPoint endPoint = new IntPoint(99485, 96234);
				AvoidCrossingPerimeters testHarness = new AvoidCrossingPerimeters(boundaryPolygons, 0);

				{
					IntPoint startPointInside = startPoint;
					testHarness.MovePointInsideBoundary(startPointInside, out startPointInside);
					IntPoint endPointInside = endPoint;
					testHarness.MovePointInsideBoundary(endPointInside, out endPointInside);

					Assert.IsTrue(testHarness.PointIsInsideBoundary(startPointInside));
					Assert.IsTrue(testHarness.PointIsInsideBoundary(endPointInside));

					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPointInside, endPointInside, insidePath);
					Assert.AreEqual(6, insidePath.Count); // It needs to go around the cicle so it needs many points (2 is a definate fail).
				}

				{
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.IsTrue(insidePath.Count == 6); // two more than the last test to get the points in the right place
				}
			}
		}
	}
}