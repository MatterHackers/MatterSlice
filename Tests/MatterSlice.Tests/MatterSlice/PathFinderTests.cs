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

using System.Collections.Generic;
using MSClipperLib;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
	using System;
	using Pathfinding;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	[TestFixture, Category("MatterSlice")]
	public class PathFinderTests
	{
		[Test]
		public void PathSolverTests()
		{
			{
				// a square with a hole (outside is ccw inside is cw)
				// _______________2__
				// | __1__________  |
				// 3 |            | |
				// | |            2 |
				// | |            | |
				// | 0            | |
				// | |__________3_| 1
				// |__0_____________|
				string partOutlineString = "x:0, y:0,x:1000, y:0,x:1000, y:1000,x:0, y:1000,|x:100, y:100,x:100, y:900,x:900, y:900,x:900, y:100,|";
				Polygons boundaryPolygons = CLPolygonsExtensions.CreateFromString(partOutlineString);

				// test the inside polygon has correct crossings
				{
					string insidePartOutlineString = "x:100, y:100,x:100, y:900,x:900, y:900,x:900, y:100,|";
					Polygons insideBoundaryPolygons = CLPolygonsExtensions.CreateFromString(insidePartOutlineString);
					IntPoint startPoint = new IntPoint(-10, 10);
					IntPoint endPoint = new IntPoint(1010, 10);
					var crossings = new List<Tuple<int, int, IntPoint>>(insideBoundaryPolygons.FindCrossingPoints(startPoint, endPoint));
					crossings.Sort(new PolygonAndPointDirectionSorter(startPoint, endPoint));
				}

				PathFinder testHarness = new PathFinder(boundaryPolygons, 0);
				Assert.IsTrue(testHarness.PointIsInsideBoundary(new IntPoint(1, 1)));
				// test being just below the lower line
				{
					IntPoint startPoint = new IntPoint(-10, 10);
					IntPoint endPoint = new IntPoint(1010, 10);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.AreEqual(2, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(0, 10), insidePath[0]);
					Assert.AreEqual(new IntPoint(1000, 10), insidePath[1]);
				}

				{
					IntPoint startPoint = new IntPoint(-10, 501);
					IntPoint endPoint = new IntPoint(1010, 501);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, false);
					Assert.AreEqual(6, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(0, 501), insidePath[0]);
					Assert.AreEqual(new IntPoint(100, 501), insidePath[1]);
					Assert.AreEqual(new IntPoint(100, 900), insidePath[2]);
					Assert.AreEqual(new IntPoint(900, 900), insidePath[3]);
					Assert.AreEqual(new IntPoint(900, 501), insidePath[4]);
					Assert.AreEqual(new IntPoint(1000, 501), insidePath[5]);
				}

				{
					IntPoint startPoint = new IntPoint(-10, 501);
					IntPoint endPoint = new IntPoint(1010, 501);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, true);
					Assert.AreEqual(4, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(0, 501), insidePath[0]);
					Assert.AreEqual(new IntPoint(100, 900), insidePath[1]);
					Assert.AreEqual(new IntPoint(900, 900), insidePath[2]);
					Assert.AreEqual(new IntPoint(1000, 501), insidePath[3]);
				}

				{
					IntPoint startPoint = new IntPoint(-10, 499);
					IntPoint endPoint = new IntPoint(1010, 499);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, false);
					Assert.AreEqual(6, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(0, 499), insidePath[0]);
					Assert.AreEqual(new IntPoint(100, 499), insidePath[1]);
					Assert.AreEqual(new IntPoint(100, 100), insidePath[2]);
					Assert.AreEqual(new IntPoint(900, 100), insidePath[3]);
					Assert.AreEqual(new IntPoint(900, 499), insidePath[4]);
					Assert.AreEqual(new IntPoint(1000, 499), insidePath[5]);
				}
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
					PathFinder testHarness = new PathFinder(boundaryPolygons, 0);

					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(new IntPoint(-1, 5)));
					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(new IntPoint(-1, 5), testHarness.OutlineEdgeQuadTrees));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(1, 5)));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(1, 5), testHarness.OutlineEdgeQuadTrees));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(0, 5)));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(0, 5), testHarness.OutlineEdgeQuadTrees));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(40, 5)));
					Assert.IsTrue(testHarness.OutlinePolygons.PointIsInside(new IntPoint(40, 5), testHarness.OutlineEdgeQuadTrees));

					Polygon insidePath = new Polygon();
					Tuple<int, int, IntPoint> outPoint;
					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(startPoint));
					Assert.IsFalse(testHarness.OutlinePolygons.PointIsInside(startPoint, testHarness.OutlineEdgeQuadTrees));

					// validate some dependant functions
					Assert.IsTrue(QTPolygonExtensions.OnSegment(test[0], new IntPoint(20, 0), test[1]));
					Assert.IsFalse(QTPolygonExtensions.OnSegment(test[0], new IntPoint(-10, 0), test[1]));
					Assert.IsFalse(QTPolygonExtensions.OnSegment(test[0], new IntPoint(50, 0), test[1]));

					// move startpoint inside
					testHarness.OutlinePolygons.MovePointInsideBoundary(startPoint, out outPoint);
					Assert.AreEqual(0, outPoint.Item1);
					Assert.AreEqual(3, outPoint.Item2);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);

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
					Assert.AreEqual(new IntPoint(40, 5), insidePath[1]);
				}

				// test being just below the lower line
				{
					IntPoint startPoint = new IntPoint(10, -1);
					IntPoint endPoint = new IntPoint(30, -1);
					PathFinder testHarness = new PathFinder(boundaryPolygons, 0);
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
				Polygons boundaryPolygons = CLPolygonsExtensions.CreateFromString(partOutlineString);
				{
					IntPoint startPoint = new IntPoint(672, 435);
					IntPoint endPoint = new IntPoint(251, 334);
					PathFinder testHarness = new PathFinder(boundaryPolygons, 0);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, false);
					Assert.AreEqual(6, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(boundaryPolygons[0][0], insidePath[0]);
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
				Polygons boundaryPolygons = CLPolygonsExtensions.CreateFromString(partOutlineString);
				IntPoint startPoint = new IntPoint(95765, 114600);
				IntPoint endPoint = new IntPoint(99485, 96234);
				PathFinder testHarness = new PathFinder(boundaryPolygons, 0);

				{
					IntPoint startPointInside = startPoint;
					testHarness.MovePointInsideBoundary(startPointInside, out startPointInside);
					IntPoint endPointInside = endPoint;
					testHarness.MovePointInsideBoundary(endPointInside, out endPointInside);

					Assert.IsTrue(testHarness.PointIsInsideBoundary(endPointInside));

					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPointInside, endPointInside, insidePath);
					Assert.GreaterOrEqual(insidePath.Count, 6); // It needs to go around the cicle so it needs many points (2 is a definate fail).
				}

				{
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.GreaterOrEqual(insidePath.Count, 6); // It needs to go around the circle so it needs many points (2 is a definite fail).
				}
			}

			// A pathing layer from a complex model (part of a skeletal face)
			{
				string partOutlineString = "x:181532, y:32784, z:8300, width:0,x:182504, y:33027, z:8300, width:0,x:182999, y:33218, z:8300, width:0,x:183662, y:33512, z:8300, width:0,x:184507, y:33965, z:8300, width:0,x:185051, y:34758, z:8300, width:0,x:185172, y:34836, z:8300, width:0,x:185527, y:34975, z:8300, width:0,x:186756, y:35606, z:8300, width:0,x:186941, y:35749, z:8300, width:0,x:187345, y:35918, z:8300, width:0,x:187631, y:36096, z:8300, width:0,x:188163, y:36270, z:8300, width:0,x:188743, y:36524, z:8300, width:0,x:189168, y:36659, z:8300, width:0,x:189814, y:36917, z:8300, width:0,x:190668, y:37193, z:8300, width:0,x:191321, y:37463, z:8300, width:0,x:191549, y:37577, z:8300, width:0,x:192009, y:37882, z:8300, width:0,x:192095, y:37971, z:8300, width:0,x:192344, y:38292, z:8300, width:0,x:192465, y:38535, z:8300, width:0,x:192556, y:38842, z:8300, width:0,x:192583, y:39357, z:8300, width:0,x:192624, y:39517, z:8300, width:0,x:192924, y:39710, z:8300, width:0,x:192958, y:39753, z:8300, width:0,x:194292, y:40061, z:8300, width:0,x:194464, y:40125, z:8300, width:0,x:195907, y:40772, z:8300, width:0,x:195983, y:40959, z:8300, width:0,x:196188, y:41617, z:8300, width:0,x:196244, y:41671, z:8300, width:0,x:197050, y:41397, z:8300, width:0,x:197121, y:41398, z:8300, width:0,x:197644, y:41731, z:8300, width:0,x:197883, y:41975, z:8300, width:0,x:198194, y:42190, z:8300, width:0,x:198325, y:42241, z:8300, width:0,x:198759, y:42236, z:8300, width:0,x:199291, y:42065, z:8300, width:0,x:199613, y:41884, z:8300, width:0,x:200110, y:41574, z:8300, width:0,x:200987, y:40990, z:8300, width:0,x:204097, y:39314, z:8300, width:0,x:204576, y:39117, z:8300, width:0,x:204989, y:39102, z:8300, width:0,x:205668, y:39293, z:8300, width:0,x:206060, y:39646, z:8300, width:0,x:206268, y:40026, z:8300, width:0,x:207504, y:42782, z:8300, width:0,x:208074, y:44104, z:8300, width:0,x:208193, y:44583, z:8300, width:0,x:208207, y:45057, z:8300, width:0,x:208160, y:45478, z:8300, width:0,x:208188, y:45821, z:8300, width:0,x:208228, y:45939, z:8300, width:0,x:208281, y:46224, z:8300, width:0,x:208408, y:46499, z:8300, width:0,x:208598, y:46814, z:8300, width:0,x:208779, y:47188, z:8300, width:0,x:209056, y:48025, z:8300, width:0,x:209041, y:48582, z:8300, width:0,x:208846, y:49283, z:8300, width:0,x:208785, y:49601, z:8300, width:0,x:208771, y:49874, z:8300, width:0,x:208858, y:50376, z:8300, width:0,x:209148, y:51060, z:8300, width:0,x:210336, y:53089, z:8300, width:0,x:210454, y:53242, z:8300, width:0,x:210449, y:54009, z:8300, width:0,x:210627, y:54250, z:8300, width:0,x:210868, y:54344, z:8300, width:0,x:211457, y:54436, z:8300, width:0,x:211658, y:54615, z:8300, width:0,x:212896, y:55129, z:8300, width:0,x:213092, y:55269, z:8300, width:0,x:213484, y:55708, z:8300, width:0,x:213597, y:55871, z:8300, width:0,x:214107, y:56730, z:8300, width:0,x:214874, y:58189, z:8300, width:0,x:215463, y:59462, z:8300, width:0,x:215778, y:60488, z:8300, width:0,x:215976, y:61472, z:8300, width:0,x:216050, y:61574, z:8300, width:0,x:216157, y:61809, z:8300, width:0,x:216206, y:61850, z:8300, width:0,x:216687, y:61975, z:8300, width:0,x:218392, y:61633, z:8300, width:0,x:218974, y:61691, z:8300, width:0,x:219317, y:61776, z:8300, width:0,x:220553, y:62169, z:8300, width:0,x:222296, y:62850, z:8300, width:0,x:223266, y:63210, z:8300, width:0,x:224479, y:63755, z:8300, width:0,x:224857, y:64013, z:8300, width:0,x:224926, y:64084, z:8300, width:0,x:225100, y:64341, z:8300, width:0,x:225065, y:64596, z:8300, width:0,x:224778, y:65059, z:8300, width:0,x:223587, y:66364, z:8300, width:0,x:222423, y:67721, z:8300, width:0,x:221358, y:69067, z:8300, width:0,x:220890, y:69696, z:8300, width:0,x:219938, y:70898, z:8300, width:0,x:219362, y:71491, z:8300, width:0,x:218997, y:71809, z:8300, width:0,x:218602, y:71946, z:8300, width:0,x:218521, y:71987, z:8300, width:0,x:218084, y:72010, z:8300, width:0,x:217683, y:71932, z:8300, width:0,x:217051, y:71726, z:8300, width:0,x:216140, y:71305, z:8300, width:0,x:215464, y:70949, z:8300, width:0,x:214960, y:70610, z:8300, width:0,x:214393, y:70162, z:8300, width:0,x:213663, y:69449, z:8300, width:0,x:211632, y:67231, z:8300, width:0,x:208324, y:63761, z:8300, width:0,x:206415, y:61869, z:8300, width:0,x:204250, y:59705, z:8300, width:0,x:199615, y:54860, z:8300, width:0,x:198702, y:53828, z:8300, width:0,x:197920, y:52801, z:8300, width:0,x:197018, y:51717, z:8300, width:0,x:196391, y:51088, z:8300, width:0,x:196120, y:50841, z:8300, width:0,x:195194, y:50147, z:8300, width:0,x:194381, y:49624, z:8300, width:0,x:193646, y:49201, z:8300, width:0,x:192819, y:48851, z:8300, width:0,x:191342, y:48411, z:8300, width:0,x:190230, y:48181, z:8300, width:0,x:189270, y:47941, z:8300, width:0,x:188801, y:47804, z:8300, width:0,x:187261, y:47411, z:8300, width:0,x:185949, y:47162, z:8300, width:0,x:184678, y:46956, z:8300, width:0,x:183352, y:46817, z:8300, width:0,x:182180, y:46755, z:8300, width:0,x:180256, y:46712, z:8300, width:0,x:177932, y:46755, z:8300, width:0,x:176295, y:46832, z:8300, width:0,x:175231, y:46953, z:8300, width:0,x:173850, y:47175, z:8300, width:0,x:172886, y:47362, z:8300, width:0,x:169916, y:48126, z:8300, width:0,x:168594, y:48396, z:8300, width:0,x:167470, y:48718, z:8300, width:0,x:166545, y:49046, z:8300, width:0,x:165496, y:49621, z:8300, width:0,x:164989, y:49940, z:8300, width:0,x:164444, y:50324, z:8300, width:0,x:163529, y:51054, z:8300, width:0,x:162722, y:51915, z:8300, width:0,x:162320, y:52400, z:8300, width:0,x:161498, y:53446, z:8300, width:0,x:160268, y:54851, z:8300, width:0,x:155439, y:59894, z:8300, width:0,x:152728, y:62612, z:8300, width:0,x:149276, y:66156, z:8300, width:0,x:147726, y:67806, z:8300, width:0,x:146162, y:69552, z:8300, width:0,x:145674, y:70033, z:8300, width:0,x:145026, y:70574, z:8300, width:0,x:144827, y:70694, z:8300, width:0,x:144439, y:70964, z:8300, width:0,x:143936, y:71209, z:8300, width:0,x:143418, y:71488, z:8300, width:0,x:142646, y:71828, z:8300, width:0,x:141898, y:72065, z:8300, width:0,x:141631, y:72057, z:8300, width:0,x:141096, y:71995, z:8300, width:0,x:140963, y:71945, z:8300, width:0,x:140379, y:71402, z:8300, width:0,x:139419, y:70107, z:8300, width:0,x:138592, y:69087, z:8300, width:0,x:136568, y:66821, z:8300, width:0,x:136363, y:66194, z:8300, width:0,x:136380, y:66161, z:8300, width:0,x:136413, y:66149, z:8300, width:0,x:136567, y:65287, z:8300, width:0,x:136669, y:64565, z:8300, width:0,x:136658, y:64352, z:8300, width:0,x:136822, y:63668, z:8300, width:0,x:137150, y:63139, z:8300, width:0,x:137866, y:62761, z:8300, width:0,x:138780, y:62398, z:8300, width:0,x:139917, y:61993, z:8300, width:0,x:140851, y:61709, z:8300, width:0,x:141344, y:61658, z:8300, width:0,x:141759, y:61680, z:8300, width:0,x:142819, y:61854, z:8300, width:0,x:142941, y:61858, z:8300, width:0,x:143066, y:61828, z:8300, width:0,x:143378, y:61704, z:8300, width:0,x:143716, y:61360, z:8300, width:0,x:143926, y:60834, z:8300, width:0,x:144337, y:59660, z:8300, width:0,x:144490, y:59318, z:8300, width:0,x:145184, y:57896, z:8300, width:0,x:145867, y:56634, z:8300, width:0,x:146568, y:55524, z:8300, width:0,x:146743, y:55402, z:8300, width:0,x:147100, y:55112, z:8300, width:0,x:148109, y:54696, z:8300, width:0,x:148197, y:54640, z:8300, width:0,x:148470, y:54412, z:8300, width:0,x:148934, y:54355, z:8300, width:0,x:149382, y:54189, z:8300, width:0,x:149410, y:53843, z:8300, width:0,x:149518, y:53564, z:8300, width:0,x:149584, y:53033, z:8300, width:0,x:150312, y:51873, z:8300, width:0,x:150966, y:50638, z:8300, width:0,x:151087, y:50277, z:8300, width:0,x:151144, y:50056, z:8300, width:0,x:151156, y:49487, z:8300, width:0,x:150918, y:48536, z:8300, width:0,x:150929, y:47960, z:8300, width:0,x:151082, y:47401, z:8300, width:0,x:151188, y:47196, z:8300, width:0,x:151317, y:46892, z:8300, width:0,x:151410, y:46739, z:8300, width:0,x:151711, y:46085, z:8300, width:0,x:151735, y:45977, z:8300, width:0,x:151755, y:45335, z:8300, width:0,x:151713, y:44731, z:8300, width:0,x:151794, y:44218, z:8300, width:0,x:152133, y:43399, z:8300, width:0,x:153700, y:39919, z:8300, width:0,x:153746, y:39860, z:8300, width:0,x:153938, y:39721, z:8300, width:0,x:154308, y:39392, z:8300, width:0,x:154686, y:39191, z:8300, width:0,x:154807, y:39150, z:8300, width:0,x:155140, y:39108, z:8300, width:0,x:155305, y:39159, z:8300, width:0,x:155637, y:39228, z:8300, width:0,x:158935, y:41008, z:8300, width:0,x:160473, y:41996, z:8300, width:0,x:161073, y:42222, z:8300, width:0,x:161563, y:42258, z:8300, width:0,x:161657, y:42187, z:8300, width:0,x:161804, y:42141, z:8300, width:0,x:162105, y:41913, z:8300, width:0,x:162275, y:41735, z:8300, width:0,x:162772, y:41416, z:8300, width:0,x:162863, y:41419, z:8300, width:0,x:163548, y:41624, z:8300, width:0,x:163684, y:41479, z:8300, width:0,x:163771, y:41477, z:8300, width:0,x:163923, y:40884, z:8300, width:0,x:164040, y:40763, z:8300, width:0,x:165476, y:40102, z:8300, width:0,x:165648, y:40041, z:8300, width:0,x:166762, y:39779, z:8300, width:0,x:167007, y:39622, z:8300, width:0,x:167207, y:39536, z:8300, width:0,x:167245, y:39444, z:8300, width:0,x:167390, y:38769, z:8300, width:0,x:167549, y:38367, z:8300, width:0,x:167868, y:37964, z:8300, width:0,x:168093, y:37774, z:8300, width:0,x:168355, y:37587, z:8300, width:0,x:169096, y:37251, z:8300, width:0,x:169858, y:37014, z:8300, width:0,x:170550, y:36724, z:8300, width:0,x:171458, y:36420, z:8300, width:0,x:171823, y:36246, z:8300, width:0,x:172365, y:36046, z:8300, width:0,x:172802, y:35779, z:8300, width:0,x:173091, y:35655, z:8300, width:0,x:173901, y:35171, z:8300, width:0,x:174324, y:35025, z:8300, width:0,x:174906, y:34622, z:8300, width:0,x:175072, y:34354, z:8300, width:0,x:175627, y:33821, z:8300, width:0,x:175866, y:33676, z:8300, width:0,x:176563, y:33365, z:8300, width:0,x:177786, y:32908, z:8300, width:0,x:178940, y:32718, z:8300, width:0,x:180599, y:32707, z:8300, width:0,|";
				TestSinglePathIsInside(partOutlineString, new IntPoint(155001, 39529), new IntPoint(138209, 64104));
			}

			// 'thin middle' from sample stls
			{
				string thinMiddle = "x:185000, y:48496,x:184599, y:48753,x:184037, y:49167,x:183505, y:49620,x:183007, y:50108,x:182544, y:50631,x:182118, y:51184,x:181732, y:51765,x:181388, y:52372,x:181087, y:53001,x:180830, y:53650,x:180619, y:54315,x:180455, y:54993,x:180339, y:55682,x:180271, y:56376,x:180250, y:57000,x:180274, y:57697,x:180347, y:58391,x:180468, y:59079,x:180637, y:59756,x:180853, y:60420,x:181114, y:61067,x:181420, y:61694,x:181769, y:62299,x:182159, y:62877,x:182589, y:63427,x:183056, y:63946,x:183558, y:64431,x:184093, y:64880,x:184658, y:65290,x:185000, y:65504,x:185000, y:67000,x:175000, y:67000,x:175000, y:65504,x:175342, y:65290,x:175907, y:64880,x:176442, y:64431,x:176944, y:63946,x:177411, y:63427,x:177841, y:62877,x:178231, y:62299,x:178580, y:61694,x:178886, y:61067,x:179147, y:60420,x:179363, y:59756,x:179532, y:59079,x:179653, y:58391,x:179726, y:57697,x:179747, y:56927,x:179718, y:56230,x:179640, y:55537,x:179514, y:54850,x:179340, y:54174,x:179120, y:53512,x:178854, y:52867,x:178543, y:52242,x:178190, y:51640,x:177796, y:51065,x:177362, y:50519,x:176891, y:50003,x:176386, y:49522,x:175848, y:49077,x:175306, y:48688,x:175000, y:48496,x:175000, y:47000,x:185000, y:47000,|";
				TestSinglePathIsInside(thinMiddle, new IntPoint(178000, 64600), new IntPoint(177700, 49000));
			}

			// Part of the Rotopus
			{
				string partOutlineString = "x:180987, y:20403, z:1100, width:0,x:181958, y:20720, z:1100, width:0,x:182687, y:21125, z:1100, width:0,x:183185, y:21539, z:1100, width:0,x:183724, y:22089, z:1100, width:0,x:184172, y:22730, z:1100, width:0,x:184432, y:23368, z:1100, width:0,x:184566, y:24089, z:1100, width:0,x:184578, y:24921, z:1100, width:0,x:184441, y:25747, z:1100, width:0,x:184138, y:26475, z:1100, width:0,x:183702, y:27108, z:1100, width:0,x:183167, y:27632, z:1100, width:0,x:182555, y:27938, z:1100, width:0,x:182554, y:27915, z:1100, width:0,x:183212, y:26818, z:1100, width:0,x:183508, y:26189, z:1100, width:0,x:183688, y:25518, z:1100, width:0,x:183702, y:24819, z:1100, width:0,x:183621, y:24184, z:1100, width:0,x:183484, y:23674, z:1100, width:0,x:183255, y:23224, z:1100, width:0,x:182918, y:22778, z:1100, width:0,x:182487, y:22368, z:1100, width:0,x:181976, y:22020, z:1100, width:0,x:181364, y:21750, z:1100, width:0,x:180632, y:21570, z:1100, width:0,x:179830, y:21508, z:1100, width:0,x:178999, y:21597, z:1100, width:0,x:178189, y:21845, z:1100, width:0,x:177449, y:22265, z:1100, width:0,x:176813, y:22859, z:1100, width:0,x:176321, y:23632, z:1100, width:0,x:175995, y:24513, z:1100, width:0,x:175862, y:25429, z:1100, width:0,x:175872, y:26264, z:1100, width:0,x:175972, y:26905, z:1100, width:0,x:176188, y:27451, z:1100, width:0,x:176539, y:27988, z:1100, width:0,x:177591, y:29295, z:1100, width:0,x:178233, y:30002, z:1100, width:0,x:178925, y:30609, z:1100, width:0,x:179753, y:31225, z:1100, width:0,x:181582, y:32508, z:1100, width:0,x:182470, y:33170, z:1100, width:0,x:183251, y:33809, z:1100, width:0,x:184288, y:34722, z:1100, width:0,x:184911, y:35349, z:1100, width:0,x:185137, y:35539, z:1100, width:0,x:186071, y:36681, z:1100, width:0,x:186896, y:38053, z:1100, width:0,x:187406, y:39517, z:1100, width:0,x:187639, y:41002, z:1100, width:0,x:187634, y:42431, z:1100, width:0,x:187518, y:43727, z:1100, width:0,x:187327, y:45100, z:1100, width:0,x:187191, y:46163, z:1100, width:0,x:187244, y:46837, z:1100, width:0,x:187680, y:47318, z:1100, width:0,x:188627, y:47808, z:1100, width:0,x:189985, y:48275, z:1100, width:0,x:191310, y:48512, z:1100, width:0,x:192509, y:48565, z:1100, width:0,x:193422, y:48457, z:1100, width:0,x:194109, y:48238, z:1100, width:0,x:194588, y:47981, z:1100, width:0,x:195075, y:47583, z:1100, width:0,x:195522, y:47082, z:1100, width:0,x:195918, y:46451, z:1100, width:0,x:196200, y:45708, z:1100, width:0,x:196388, y:44868, z:1100, width:0,x:196506, y:43938, z:1100, width:0,x:196730, y:41538, z:1100, width:0,x:196819, y:40094, z:1100, width:0,x:196773, y:37109, z:1100, width:0,x:196834, y:35631, z:1100, width:0,x:196979, y:34313, z:1100, width:0,x:197200, y:33298, z:1100, width:0,x:197487, y:32482, z:1100, width:0,x:197826, y:31759, z:1100, width:0,x:198234, y:31130, z:1100, width:0,x:198724, y:30598, z:1100, width:0,x:199307, y:30160, z:1100, width:0,x:199994, y:29811, z:1100, width:0,x:200755, y:29542, z:1100, width:0,x:201565, y:29346, z:1100, width:0,x:202350, y:29254, z:1100, width:0,x:203038, y:29300, z:1100, width:0,x:203676, y:29448, z:1100, width:0,x:204307, y:29659, z:1100, width:0,x:204899, y:29943, z:1100, width:0,x:205424, y:30309, z:1100, width:0,x:205900, y:30744, z:1100, width:0,x:206341, y:31238, z:1100, width:0,x:206715, y:31833, z:1100, width:0,x:206965, y:32519, z:1100, width:0,x:207143, y:33310, z:1100, width:0,x:207182, y:34036, z:1100, width:0,x:207104, y:34673, z:1100, width:0,x:206913, y:35265, z:1100, width:0,x:206572, y:35843, z:1100, width:0,x:206049, y:36420, z:1100, width:0,x:205435, y:36922, z:1100, width:0,x:205929, y:36318, z:1100, width:0,x:206313, y:35713, z:1100, width:0,x:206555, y:35184, z:1100, width:0,x:206669, y:34662, z:1100, width:0,x:206664, y:34091, z:1100, width:0,x:206565, y:33504, z:1100, width:0,x:206382, y:32797, z:1100, width:0,x:206130, y:32161, z:1100, width:0,x:205799, y:31657, z:1100, width:0,x:205422, y:31244, z:1100, width:0,x:205028, y:30885, z:1100, width:0,x:204600, y:30592, z:1100, width:0,x:204117, y:30379, z:1100, width:0,x:203593, y:30223, z:1100, width:0,x:203038, y:30101, z:1100, width:0,x:202429, y:30065, z:1100, width:0,x:201741, y:30167, z:1100, width:0,x:201062, y:30366, z:1100, width:0,x:200483, y:30623, z:1100, width:0,x:199990, y:30925, z:1100, width:0,x:199574, y:31266, z:1100, width:0,x:199217, y:31675, z:1100, width:0,x:198904, y:32183, z:1100, width:0,x:198633, y:32779, z:1100, width:0,x:198396, y:33458, z:1100, width:0,x:198216, y:34325, z:1100, width:0,x:198104, y:35476, z:1100, width:0,x:198081, y:36793, z:1100, width:0,x:198163, y:38160, z:1100, width:0,x:198353, y:40158, z:1100, width:0,x:198617, y:43362, z:1100, width:0,x:198656, y:44453, z:1100, width:0,x:198615, y:45530, z:1100, width:0,x:198462, y:46622, z:1100, width:0,x:198167, y:47769, z:1100, width:0,x:197709, y:48863, z:1100, width:0,x:197066, y:49776, z:1100, width:0,x:196194, y:50597, z:1100, width:0,x:195038, y:51434, z:1100, width:0,x:193637, y:52208, z:1100, width:0,x:192093, y:52812, z:1100, width:0,x:190610, y:53302, z:1100, width:0,x:189555, y:53699, z:1100, width:0,x:189483, y:53790, z:1100, width:0,x:189240, y:54023, z:1100, width:0,x:189870, y:54330, z:1100, width:0,x:191325, y:54913, z:1100, width:0,x:191948, y:55224, z:1100, width:0,x:192658, y:55617, z:1100, width:0,x:193809, y:56344, z:1100, width:0,x:195832, y:57799, z:1100, width:0,x:196769, y:58278, z:1100, width:0,x:197372, y:58499, z:1100, width:0,x:197647, y:58583, z:1100, width:0,x:198439, y:58743, z:1100, width:0,x:199215, y:58770, z:1100, width:0,x:200046, y:58679, z:1100, width:0,x:200931, y:58398, z:1100, width:0,x:201874, y:57859, z:1100, width:0,x:202756, y:57213, z:1100, width:0,x:203460, y:56618, z:1100, width:0,x:204312, y:55816, z:1100, width:0,x:205986, y:54164, z:1100, width:0,x:206874, y:53342, z:1100, width:0,x:207625, y:52798, z:1100, width:0,x:208545, y:52312, z:1100, width:0,x:209568, y:51966, z:1100, width:0,x:210726, y:51807, z:1100, width:0,x:212015, y:51921, z:1100, width:0,x:213285, y:52307, z:1100, width:0,x:214386, y:52963, z:1100, width:0,x:215257, y:53751, z:1100, width:0,x:215835, y:54533, z:1100, width:0,x:216176, y:55228, z:1100, width:0,x:216419, y:55928, z:1100, width:0,x:216533, y:56583, z:1100, width:0,x:216548, y:57239, z:1100, width:0,x:216479, y:57902, z:1100, width:0,x:216334, y:58558, z:1100, width:0,x:216106, y:59194, z:1100, width:0,x:215789, y:59796, z:1100, width:0,x:215395, y:60348, z:1100, width:0,x:214941, y:60836, z:1100, width:0,x:214419, y:61238, z:1100, width:0,x:213825, y:61537, z:1100, width:0,x:213167, y:61717, z:1100, width:0,x:212305, y:61786, z:1100, width:0,x:211454, y:61716, z:1100, width:0,x:210705, y:61550, z:1100, width:0,x:209916, y:61220, z:1100, width:0,x:209234, y:60749, z:1100, width:0,x:208680, y:60175, z:1100, width:0,x:208307, y:59559, z:1100, width:0,x:208098, y:59019, z:1100, width:0,x:208340, y:59545, z:1100, width:0,x:208769, y:60117, z:1100, width:0,x:209370, y:60593, z:1100, width:0,x:210067, y:60933, z:1100, width:0,x:210763, y:61096, z:1100, width:0,x:211524, y:61154, z:1100, width:0,x:212240, y:61105, z:1100, width:0,x:212828, y:60984, z:1100, width:0,x:213352, y:60798, z:1100, width:0,x:213759, y:60542, z:1100, width:0,x:214152, y:60192, z:1100, width:0,x:214511, y:59770, z:1100, width:0,x:214815, y:59290, z:1100, width:0,x:215054, y:58788, z:1100, width:0,x:215216, y:58298, z:1100, width:0,x:215291, y:57950, z:1100, width:0,x:215359, y:57432, z:1100, width:0,x:215365, y:56936, z:1100, width:0,x:215305, y:56473, z:1100, width:0,x:215150, y:55983, z:1100, width:0,x:214881, y:55426, z:1100, width:0,x:214423, y:54818, z:1100, width:0,x:213710, y:54203, z:1100, width:0,x:212829, y:53688, z:1100, width:0,x:211868, y:53383, z:1100, width:0,x:210909, y:53310, z:1100, width:0,x:210037, y:53500, z:1100, width:0,x:209316, y:53802, z:1100, width:0,x:208618, y:54153, z:1100, width:0,x:207997, y:54541, z:1100, width:0,x:207446, y:54969, z:1100, width:0,x:206021, y:56191, z:1100, width:0,x:204174, y:57856, z:1100, width:0,x:203385, y:58526, z:1100, width:0,x:202461, y:59199, z:1100, width:0,x:201468, y:59819, z:1100, width:0,x:200471, y:60329, z:1100, width:0,x:199478, y:60703, z:1100, width:0,x:198498, y:60915, z:1100, width:0,x:197518, y:60972, z:1100, width:0,x:196524, y:60881, z:1100, width:0,x:195578, y:60684, z:1100, width:0,x:194820, y:60448, z:1100, width:0,x:194437, y:60302, z:1100, width:0,x:192694, y:59529, z:1100, width:0,x:191458, y:59122, z:1100, width:0,x:190389, y:59075, z:1100, width:0,x:189599, y:59243, z:1100, width:0,x:189119, y:59480, z:1100, width:0,x:188967, y:59755, z:1100, width:0,x:189138, y:60058, z:1100, width:0,x:189492, y:60369, z:1100, width:0,x:189982, y:60778, z:1100, width:0,x:190706, y:61527, z:1100, width:0,x:192915, y:64443, z:1100, width:0,x:194141, y:65892, z:1100, width:0,x:195052, y:66844, z:1100, width:0,x:195423, y:67211, z:1100, width:0,x:196758, y:68398, z:1100, width:0,x:198071, y:69403, z:1100, width:0,x:199439, y:70365, z:1100, width:0,x:200717, y:71099, z:1100, width:0,x:202913, y:72157, z:1100, width:0,x:204008, y:72800, z:1100, width:0,x:204968, y:73463, z:1100, width:0,x:205634, y:74031, z:1100, width:0,x:206099, y:74589, z:1100, width:0,x:206454, y:75221, z:1100, width:0,x:206715, y:75884, z:1100, width:0,x:206896, y:76533, z:1100, width:0,x:207018, y:77274, z:1100, width:0,x:207099, y:78211, z:1100, width:0,x:207085, y:79216, z:1100, width:0,x:206922, y:80161, z:1100, width:0,x:206631, y:81044, z:1100, width:0,x:206235, y:81863, z:1100, width:0,x:205756, y:82559, z:1100, width:0,x:205214, y:83077, z:1100, width:0,x:204585, y:83487, z:1100, width:0,x:203844, y:83863, z:1100, width:0,x:203024, y:84143, z:1100, width:0,x:202158, y:84264, z:1100, width:0,x:201327, y:84245, z:1100, width:0,x:200611, y:84099, z:1100, width:0,x:199981, y:83836, z:1100, width:0,x:199208, y:83381, z:1100, width:0,x:198524, y:82824, z:1100, width:0,x:198000, y:82203, z:1100, width:0,x:197634, y:81550, z:1100, width:0,x:197428, y:80900, z:1100, width:0,x:197369, y:80197, z:1100, width:0,x:197444, y:79391, z:1100, width:0,x:197649, y:78570, z:1100, width:0,x:197947, y:77856, z:1100, width:0,x:198417, y:77141, z:1100, width:0,x:199039, y:76555, z:1100, width:0,x:199747, y:76111, z:1100, width:0,x:200465, y:75861, z:1100, width:0,x:201119, y:75768, z:1100, width:0,x:201568, y:75828, z:1100, width:0,x:201370, y:75857, z:1100, width:0,x:200504, y:76041, z:1100, width:0,x:199836, y:76319, z:1100, width:0,x:199224, y:76786, z:1100, width:0,x:198722, y:77383, z:1100, width:0,x:198375, y:78043, z:1100, width:0,x:198143, y:78740, z:1100, width:0,x:198016, y:79457, z:1100, width:0,x:197992, y:80115, z:1100, width:0,x:198066, y:80648, z:1100, width:0,x:198260, y:81141, z:1100, width:0,x:198596, y:81674, z:1100, width:0,x:199057, y:82193, z:1100, width:0,x:199610, y:82627, z:1100, width:0,x:200233, y:82994, z:1100, width:0,x:200788, y:83214, z:1100, width:0,x:201351, y:83321, z:1100, width:0,x:201979, y:83341, z:1100, width:0,x:202645, y:83249, z:1100, width:0,x:203325, y:83020, z:1100, width:0,x:203958, y:82705, z:1100, width:0,x:204484, y:82351, z:1100, width:0,x:204925, y:81918, z:1100, width:0,x:205306, y:81366, z:1100, width:0,x:205608, y:80745, z:1100, width:0,x:205819, y:80110, z:1100, width:0,x:205929, y:79424, z:1100, width:0,x:205930, y:78648, z:1100, width:0,x:205852, y:77906, z:1100, width:0,x:205726, y:77320, z:1100, width:0,x:205538, y:76797, z:1100, width:0,x:205279, y:76245, z:1100, width:0,x:204919, y:75718, z:1100, width:0,x:204431, y:75271, z:1100, width:0,x:203748, y:74854, z:1100, width:0,x:202806, y:74411, z:1100, width:0,x:201708, y:74016, z:1100, width:0,x:200555, y:73744, z:1100, width:0,x:199238, y:73411, z:1100, width:0,x:197642, y:72835, z:1100, width:0,x:195746, y:72077, z:1100, width:0,x:194074, y:71350, z:1100, width:0,x:192410, y:70529, z:1100, width:0,x:191010, y:69738, z:1100, width:0,x:189880, y:69177, z:1100, width:0,x:189033, y:69002, z:1100, width:0,x:188632, y:69143, z:1100, width:0,x:188385, y:69199, z:1100, width:0,x:188137, y:69548, z:1100, width:0,x:187886, y:69735, z:1100, width:0,x:187701, y:70180, z:1100, width:0,x:187481, y:70479, z:1100, width:0,x:187298, y:70942, z:1100, width:0,x:187111, y:71313, z:1100, width:0,x:186769, y:72125, z:1100, width:0,x:186440, y:72800, z:1100, width:0,x:186051, y:73382, z:1100, width:0,x:185528, y:73918, z:1100, width:0,x:184909, y:74379, z:1100, width:0,x:184231, y:74736, z:1100, width:0,x:183561, y:75004, z:1100, width:0,x:182965, y:75199, z:1100, width:0,x:182284, y:75347, z:1100, width:0,x:181327, y:75468, z:1100, width:0,x:180496, y:75700, z:1100, width:0,x:180318, y:75949, z:1100, width:0,x:180141, y:76112, z:1100, width:0,x:180086, y:76664, z:1100, width:0,x:180173, y:77274, z:1100, width:0,x:180368, y:77957, z:1100, width:0,x:180679, y:78667, z:1100, width:0,x:181066, y:79371, z:1100, width:0,x:181480, y:80035, z:1100, width:0,x:182251, y:81168, z:1100, width:0,x:182632, y:81660, z:1100, width:0,x:184193, y:83325, z:1100, width:0,x:184754, y:83985, z:1100, width:0,x:185231, y:84649, z:1100, width:0,x:185585, y:85388, z:1100, width:0,x:185780, y:86273, z:1100, width:0,x:185815, y:87357, z:1100, width:0,x:185684, y:88689, z:1100, width:0,x:185330, y:90050, z:1100, width:0,x:184696, y:91229, z:1100, width:0,x:183903, y:92198, z:1100, width:0,x:183076, y:92932, z:1100, width:0,x:182176, y:93440, z:1100, width:0,x:181171, y:93732, z:1100, width:0,x:180096, y:93801, z:1100, width:0,x:178993, y:93641, z:1100, width:0,x:177995, y:93326, z:1100, width:0,x:177232, y:92923, z:1100, width:0,x:176627, y:92444, z:1100, width:0,x:176105, y:91903, z:1100, width:0,x:175681, y:91306, z:1100, width:0,x:175373, y:90655, z:1100, width:0,x:175171, y:89917, z:1100, width:0,x:175070, y:89059, z:1100, width:0,x:175129, y:88161, z:1100, width:0,x:175405, y:87296, z:1100, width:0,x:175879, y:86467, z:1100, width:0,x:176543, y:85691, z:1100, width:0,x:177298, y:85058, z:1100, width:0,x:178055, y:84644, z:1100, width:0,x:178755, y:84418, z:1100, width:0,x:179373, y:84351, z:1100, width:0,x:179442, y:84380, z:1100, width:0,x:179770, y:84454, z:1100, width:0,x:179747, y:84655, z:1100, width:0,x:179364, y:84745, z:1100, width:0,x:178801, y:84821, z:1100, width:0,x:178187, y:85061, z:1100, width:0,x:177588, y:85492, z:1100, width:0,x:177068, y:86120, z:1100, width:0,x:176648, y:86853, z:1100, width:0,x:176330, y:87663, z:1100, width:0,x:176168, y:88425, z:1100, width:0,x:176134, y:89154, z:1100, width:0,x:176208, y:89794, z:1100, width:0,x:176365, y:90293, z:1100, width:0,x:176618, y:90728, z:1100, width:0,x:176984, y:91168, z:1100, width:0,x:177444, y:91578, z:1100, width:0,x:177986, y:91925, z:1100, width:0,x:178621, y:92198, z:1100, width:0,x:179363, y:92384, z:1100, width:0,x:180167, y:92449, z:1100, width:0,x:180996, y:92362, z:1100, width:0,x:181802, y:92108, z:1100, width:0,x:182541, y:91676, z:1100, width:0,x:183176, y:91062, z:1100, width:0,x:183671, y:90261, z:1100, width:0,x:183998, y:89346, z:1100, width:0,x:184126, y:88390, z:1100, width:0,x:184115, y:87534, z:1100, width:0,x:184021, y:86837, z:1100, width:0,x:183834, y:86301, z:1100, width:0,x:183533, y:85823, z:1100, width:0,x:183188, y:85437, z:1100, width:0,x:182689, y:84952, z:1100, width:0,x:181611, y:83970, z:1100, width:0,x:180398, y:82914, z:1100, width:0,x:179492, y:82110, z:1100, width:0,x:178808, y:81390, z:1100, width:0,x:178359, y:80861, z:1100, width:0,x:177642, y:79951, z:1100, width:0,x:177167, y:79245, z:1100, width:0,x:176767, y:78517, z:1100, width:0,x:176405, y:77628, z:1100, width:0,x:176005, y:76582, z:1100, width:0,x:175847, y:76085, z:1100, width:0,x:175745, y:75690, z:1100, width:0,x:175599, y:75315, z:1100, width:0,x:175501, y:74961, z:1100, width:0,x:175368, y:74780, z:1100, width:0,x:175135, y:74336, z:1100, width:0,x:174982, y:74209, z:1100, width:0,x:174599, y:73774, z:1100, width:0,x:174020, y:73279, z:1100, width:0,x:173611, y:72735, z:1100, width:0,x:173276, y:72076, z:1100, width:0,x:172946, y:71256, z:1100, width:0,x:172661, y:70411, z:1100, width:0,x:172373, y:69309, z:1100, width:0,x:172153, y:68586, z:1100, width:0,x:172033, y:68314, z:1100, width:0,x:171817, y:68071, z:1100, width:0,x:171579, y:67664, z:1100, width:0,x:171237, y:67464, z:1100, width:0,x:170765, y:67107, z:1100, width:0,x:169792, y:66739, z:1100, width:0,x:168840, y:66649, z:1100, width:0,x:167984, y:66723, z:1100, width:0,x:167083, y:66840, z:1100, width:0,x:166292, y:66981, z:1100, width:0,x:165533, y:67146, z:1100, width:0,x:164756, y:67395, z:1100, width:0,x:163912, y:67811, z:1100, width:0,x:163137, y:68366, z:1100, width:0,x:162573, y:69023, z:1100, width:0,x:162218, y:69839, z:1100, width:0,x:162087, y:70880, z:1100, width:0,x:162138, y:72011, z:1100, width:0,x:162333, y:73102, z:1100, width:0,x:162588, y:74209, z:1100, width:0,x:162817, y:75390, z:1100, width:0,x:162978, y:76630, z:1100, width:0,x:163023, y:77913, z:1100, width:0,x:162955, y:79071, z:1100, width:0,x:162775, y:79935, z:1100, width:0,x:162504, y:80635, z:1100, width:0,x:162163, y:81306, z:1100, width:0,x:161700, y:81957, z:1100, width:0,x:161060, y:82599, z:1100, width:0,x:160310, y:83177, z:1100, width:0,x:159517, y:83634, z:1100, width:0,x:158728, y:83939, z:1100, width:0,x:157988, y:84060, z:1100, width:0,x:157236, y:84044, z:1100, width:0,x:156411, y:83935, z:1100, width:0,x:155575, y:83701, z:1100, width:0,x:154792, y:83309, z:1100, width:0,x:154120, y:82823, z:1100, width:0,x:153615, y:82301, z:1100, width:0,x:153215, y:81684, z:1100, width:0,x:152866, y:80916, z:1100, width:0,x:152620, y:80086, z:1100, width:0,x:152541, y:79307, z:1100, width:0,x:152608, y:78582, z:1100, width:0,x:152830, y:77911, z:1100, width:0,x:152905, y:77822, z:1100, width:0,x:153207, y:77342, z:1100, width:0,x:153511, y:77055, z:1100, width:0,x:153775, y:76765, z:1100, width:0,x:154348, y:76321, z:1100, width:0,x:154577, y:76176, z:1100, width:0,x:154461, y:76276, z:1100, width:0,x:153922, y:76893, z:1100, width:0,x:153761, y:77144, z:1100, width:0,x:153558, y:77516, z:1100, width:0,x:153417, y:77865, z:1100, width:0,x:153314, y:78549, z:1100, width:0,x:153308, y:79182, z:1100, width:0,x:153393, y:79812, z:1100, width:0,x:153624, y:80561, z:1100, width:0,x:153923, y:81187, z:1100, width:0,x:154258, y:81676, z:1100, width:0,x:154662, y:82080, z:1100, width:0,x:155168, y:82445, z:1100, width:0,x:155769, y:82741, z:1100, width:0,x:156457, y:82933, z:1100, width:0,x:157156, y:83026, z:1100, width:0,x:157788, y:83028, z:1100, width:0,x:158393, y:82918, z:1100, width:0,x:159015, y:82675, z:1100, width:0,x:159606, y:82322, z:1100, width:0,x:160121, y:81885, z:1100, width:0,x:160548, y:81389, z:1100, width:0,x:160874, y:80861, z:1100, width:0,x:161119, y:80309, z:1100, width:0,x:161301, y:79736, z:1100, width:0,x:161405, y:79006, z:1100, width:0,x:161412, y:77982, z:1100, width:0,x:161296, y:76799, z:1100, width:0,x:161029, y:75583, z:1100, width:0,x:160675, y:74363, z:1100, width:0,x:160299, y:73166, z:1100, width:0,x:160009, y:71911, z:1100, width:0,x:159909, y:70518, z:1100, width:0,x:160011, y:69167, z:1100, width:0,x:160320, y:68050, z:1100, width:0,x:160947, y:67008, z:1100, width:0,x:161956, y:65962, z:1100, width:0,x:163111, y:65032, z:1100, width:0,x:164168, y:64343, z:1100, width:0,x:165180, y:63751, z:1100, width:0,x:166193, y:63128, z:1100, width:0,x:169256, y:61157, z:1100, width:0,x:170457, y:60375, z:1100, width:0,x:170856, y:60050, z:1100, width:0,x:171030, y:59754, z:1100, width:0,x:170876, y:59482, z:1100, width:0,x:170285, y:59194, z:1100, width:0,x:169576, y:58985, z:1100, width:0,x:168448, y:58773, z:1100, width:0,x:167071, y:58584, z:1100, width:0,x:163714, y:58186, z:1100, width:0,x:162743, y:58110, z:1100, width:0,x:161898, y:58082, z:1100, width:0,x:161169, y:58093, z:1100, width:0,x:160519, y:58170, z:1100, width:0,x:159905, y:58340, z:1100, width:0,x:159294, y:58596, z:1100, width:0,x:158653, y:58921, z:1100, width:0,x:157303, y:59682, z:1100, width:0,x:155806, y:60502, z:1100, width:0,x:155027, y:60894, z:1100, width:0,x:154125, y:61309, z:1100, width:0,x:153354, y:61596, z:1100, width:0,x:152453, y:61862, z:1100, width:0,x:151577, y:62081, z:1100, width:0,x:150889, y:62228, z:1100, width:0,x:150177, y:62296, z:1100, width:0,x:149238, y:62279, z:1100, width:0,x:148390, y:62209, z:1100, width:0,x:147601, y:62086, z:1100, width:0,x:146995, y:61903, z:1100, width:0,x:146363, y:61625, z:1100, width:0,x:145752, y:61276, z:1100, width:0,x:145207, y:60881, z:1100, width:0,x:144708, y:60354, z:1100, width:0,x:144229, y:59610, z:1100, width:0,x:143852, y:58830, z:1100, width:0,x:143621, y:58096, z:1100, width:0,x:143523, y:57432, z:1100, width:0,x:143495, y:56752, z:1100, width:0,x:143546, y:56061, z:1100, width:0,x:143692, y:55379, z:1100, width:0,x:143932, y:54723, z:1100, width:0,x:144260, y:54104, z:1100, width:0,x:144661, y:53538, z:1100, width:0,x:145123, y:53043, z:1100, width:0,x:145645, y:52636, z:1100, width:0,x:146227, y:52335, z:1100, width:0,x:146903, y:52145, z:1100, width:0,x:147203, y:52118, z:1100, width:0,x:147709, y:52136, z:1100, width:0,x:148517, y:52310, z:1100, width:0,x:148783, y:52406, z:1100, width:0,x:148506, y:52414, z:1100, width:0,x:147746, y:52565, z:1100, width:0,x:147265, y:52729, z:1100, width:0,x:147102, y:52763, z:1100, width:0,x:146599, y:52959, z:1100, width:0,x:146180, y:53238, z:1100, width:0,x:145787, y:53617, z:1100, width:0,x:145433, y:54071, z:1100, width:0,x:145136, y:54583, z:1100, width:0,x:144911, y:55120, z:1100, width:0,x:144796, y:55563, z:1100, width:0,x:144698, y:56144, z:1100, width:0,x:144649, y:56637, z:1100, width:0,x:144650, y:57123, z:1100, width:0,x:144712, y:57597, z:1100, width:0,x:144870, y:58133, z:1100, width:0,x:145167, y:58806, z:1100, width:0,x:145549, y:59448, z:1100, width:0,x:145965, y:59889, z:1100, width:0,x:146402, y:60213, z:1100, width:0,x:146853, y:60491, z:1100, width:0,x:147311, y:60707, z:1100, width:0,x:147764, y:60865, z:1100, width:0,x:148283, y:60970, z:1100, width:0,x:148942, y:61030, z:1100, width:0,x:149596, y:61038, z:1100, width:0,x:150096, y:60984, z:1100, width:0,x:150239, y:60953, z:1100, width:0,x:151213, y:60695, z:1100, width:0,x:151891, y:60468, z:1100, width:0,x:152537, y:60200, z:1100, width:0,x:153221, y:59868, z:1100, width:0,x:154015, y:59447, z:1100, width:0,x:154833, y:58965, z:1100, width:0,x:155583, y:58449, z:1100, width:0,x:157038, y:57384, z:1100, width:0,x:157803, y:56898, z:1100, width:0,x:158621, y:56497, z:1100, width:0,x:159490, y:56183, z:1100, width:0,x:160405, y:55955, z:1100, width:0,x:161499, y:55781, z:1100, width:0,x:164223, y:55542, z:1100, width:0,x:165291, y:55408, z:1100, width:0,x:167049, y:55136, z:1100, width:0,x:168596, y:54786, z:1100, width:0,x:169718, y:54441, z:1100, width:0,x:170062, y:54350, z:1100, width:0,x:170851, y:53913, z:1100, width:0,x:170864, y:53667, z:1100, width:0,x:170739, y:53147, z:1100, width:0,x:170522, y:52750, z:1100, width:0,x:170385, y:52433, z:1100, width:0,x:170029, y:51889, z:1100, width:0,x:169300, y:51088, z:1100, width:0,x:168388, y:50170, z:1100, width:0,x:167525, y:49462, z:1100, width:0,x:166476, y:48856, z:1100, width:0,x:165272, y:48405, z:1100, width:0,x:164414, y:48024, z:1100, width:0,x:164118, y:47844, z:1100, width:0,x:163719, y:47717, z:1100, width:0,x:163189, y:47476, z:1100, width:0,x:163013, y:47322, z:1100, width:0,x:162616, y:47021, z:1100, width:0,x:162001, y:46134, z:1100, width:0,x:161901, y:45177, z:1100, width:0,x:162149, y:44587, z:1100, width:0,x:162664, y:44069, z:1100, width:0,x:163078, y:43816, z:1100, width:0,x:163365, y:43405, z:1100, width:0,x:163679, y:43268, z:1100, width:0,x:163830, y:43252, z:1100, width:0,x:164332, y:43318, z:1100, width:0,x:164394, y:43314, z:1100, width:0,x:164959, y:43432, z:1100, width:0,x:165109, y:43438, z:1100, width:0,x:165453, y:43533, z:1100, width:0,x:165820, y:43725, z:1100, width:0,x:167689, y:44205, z:1100, width:0,x:169414, y:44990, z:1100, width:0,x:170733, y:45625, z:1100, width:0,x:172387, y:46390, z:1100, width:0,x:173412, y:47007, z:1100, width:0,x:174299, y:47493, z:1100, width:0,x:175117, y:47775, z:1100, width:0,x:175872, y:47756, z:1100, width:0,x:176613, y:47541, z:1100, width:0,x:177312, y:47277, z:1100, width:0,x:177970, y:46982, z:1100, width:0,x:178587, y:46649, z:1100, width:0,x:179042, y:46341, z:1100, width:0,x:179401, y:46069, z:1100, width:0,x:179980, y:45605, z:1100, width:0,x:181197, y:44579, z:1100, width:0,x:182196, y:43578, z:1100, width:0,x:182978, y:42509, z:1100, width:0,x:183391, y:41461, z:1100, width:0,x:183450, y:40434, z:1100, width:0,x:183280, y:39468, z:1100, width:0,x:182999, y:38600, z:1100, width:0,x:182609, y:37809, z:1100, width:0,x:182113, y:37076, z:1100, width:0,x:181582, y:36426, z:1100, width:0,x:181090, y:35879, z:1100, width:0,x:180500, y:35289, z:1100, width:0,x:177903, y:32811, z:1100, width:0,x:177068, y:31941, z:1100, width:0,x:176264, y:30984, z:1100, width:0,x:175554, y:30014, z:1100, width:0,x:174998, y:29106, z:1100, width:0,x:174591, y:28231, z:1100, width:0,x:174329, y:27355, z:1100, width:0,x:174237, y:26350, z:1100, width:0,x:174342, y:25087, z:1100, width:0,x:174687, y:23785, z:1100, width:0,x:175316, y:22660, z:1100, width:0,x:176105, y:21736, z:1100, width:0,x:176931, y:21039, z:1100, width:0,x:177828, y:20559, z:1100, width:0,x:178831, y:20288, z:1100, width:0,x:179898, y:20235, z:1100, width:0,|";
				//TestSinglePathIsInside(partOutlineString, new IntPoint(153099, 78023), new IntPoint(153104, 77984));
			}

			
		}

		private void TestSinglePathIsInside(string partOutlineString, IntPoint startPoint, IntPoint endPoint)
		{
			Polygons boundaryPolygons = CLPolygonsExtensions.CreateFromString(partOutlineString);
			PathFinder testHarness = new PathFinder(boundaryPolygons, -600);

			Polygon insidePath = new Polygon();
			// not optimized
			testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, false);
			Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));

			// and optimized
			testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, true);
			Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
		}
	}
}