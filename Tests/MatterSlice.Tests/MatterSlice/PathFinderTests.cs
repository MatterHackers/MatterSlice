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
					Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
					Assert.AreEqual(4, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(-10, 10), insidePath[0]);
					Assert.AreEqual(new IntPoint(0, 10), insidePath[1]);
					Assert.AreEqual(new IntPoint(1000, 10), insidePath[2]);
					Assert.AreEqual(new IntPoint(1010, 10), insidePath[3]);
				}

				{
					IntPoint startPoint = new IntPoint(-10, 501);
					IntPoint endPoint = new IntPoint(1010, 501);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, false);
					Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
					Assert.AreEqual(8, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(-10, 501), insidePath[0]);
					Assert.AreEqual(new IntPoint(0, 501), insidePath[1]);
					Assert.AreEqual(new IntPoint(100, 501), insidePath[2]);
					Assert.AreEqual(new IntPoint(100, 900), insidePath[3]);
					Assert.AreEqual(new IntPoint(900, 900), insidePath[4]);
					Assert.AreEqual(new IntPoint(900, 501), insidePath[5]);
					Assert.AreEqual(new IntPoint(1000, 501), insidePath[6]);
					Assert.AreEqual(new IntPoint(1010, 501), insidePath[7]);
				}

				{
					IntPoint startPoint = new IntPoint(-10, 501);
					IntPoint endPoint = new IntPoint(1010, 501);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, true);
					Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
					Assert.AreEqual(6, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(-10, 501), insidePath[0]);
					Assert.AreEqual(new IntPoint(0, 501), insidePath[1]);
					Assert.AreEqual(new IntPoint(100, 900), insidePath[2]);
					Assert.AreEqual(new IntPoint(900, 900), insidePath[3]);
					Assert.AreEqual(new IntPoint(1000, 501), insidePath[4]);
					Assert.AreEqual(new IntPoint(1010, 501), insidePath[5]);
				}

				{
					IntPoint startPoint = new IntPoint(-10, 499);
					IntPoint endPoint = new IntPoint(1010, 499);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, false);
					Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
					Assert.AreEqual(8, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(-10, 499), insidePath[0]);
					Assert.AreEqual(new IntPoint(0, 499), insidePath[1]);
					Assert.AreEqual(new IntPoint(100, 499), insidePath[2]);
					Assert.AreEqual(new IntPoint(100, 100), insidePath[3]);
					Assert.AreEqual(new IntPoint(900, 100), insidePath[4]);
					Assert.AreEqual(new IntPoint(900, 499), insidePath[5]);
					Assert.AreEqual(new IntPoint(1000, 499), insidePath[6]);
					Assert.AreEqual(new IntPoint(1010, 499), insidePath[7]);
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
					boundaryPolygons.MovePointInsideBoundary(startPoint, out outPoint);
					Assert.AreEqual(0, outPoint.Item1);
					Assert.AreEqual(3, outPoint.Item2);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);

					testHarness.OutlinePolygons.MovePointInsideBoundary(startPoint, out outPoint);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);

					testHarness.OutlinePolygons.MovePointInsideBoundary(startPoint, out outPoint, testHarness.OutlineEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(startPoint, out outPoint);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(startPoint, out outPoint, testHarness.BoundaryEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(0, 5), outPoint.Item3);
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));

					// move endpoint inside
					testHarness.OutlinePolygons.MovePointInsideBoundary(endPoint, out outPoint);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);
					testHarness.OutlinePolygons.MovePointInsideBoundary(endPoint, out outPoint, testHarness.OutlineEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(endPoint, out outPoint);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);
					testHarness.BoundaryPolygons.MovePointInsideBoundary(endPoint, out outPoint, testHarness.BoundaryEdgeQuadTrees);
					Assert.AreEqual(new IntPoint(40, 5), outPoint.Item3);

					Assert.AreEqual(4, insidePath.Count);
					Assert.AreEqual(new IntPoint(-10, 5), insidePath[0]);
					Assert.AreEqual(new IntPoint(0, 5), insidePath[1]);
					Assert.AreEqual(new IntPoint(40, 5), insidePath[2]);
					Assert.AreEqual(new IntPoint(50, 5), insidePath[3]);
				}

				// test being just below the lower line
				{
					IntPoint startPoint = new IntPoint(10, -1);
					IntPoint endPoint = new IntPoint(30, -1);
					PathFinder testHarness = new PathFinder(boundaryPolygons, 0);
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
					Assert.AreEqual(3, insidePath.Count);
					// move start to the 0th vertex
					Assert.AreEqual(new IntPoint(10, -1), insidePath[0]);
					Assert.AreEqual(new IntPoint(30, 0), insidePath[1]);
					Assert.AreEqual(new IntPoint(30, -1), insidePath[2]);
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
					Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
					Assert.AreEqual(7, insidePath.Count);
					Assert.AreEqual(startPoint, insidePath[0]);
					// move start to the 0th vertex
					Assert.AreEqual(boundaryPolygons[0][0], insidePath[1]);
					// next collide with edge 1
					Assert.AreEqual(new IntPoint(400, 365), insidePath[2]);
					// the next 3 points are the is the 2 - 3 index
					Assert.AreEqual(boundaryPolygons[0][2], insidePath[3]);
					Assert.AreEqual(boundaryPolygons[0][3], insidePath[4]);
					// the last point is created on the 3 edge
					Assert.AreEqual(endPoint, insidePath[6]);
				}
			}

			// 'thin middle' from sample stls
			{
				string polyPath = "x:185000, y:48496,x:184599, y:48753,x:184037, y:49167,x:183505, y:49620,x:183007, y:50108,x:182544, y:50631,x:182118, y:51184,x:181732, y:51765,x:181388, y:52372,x:181087, y:53001,x:180830, y:53650,x:180619, y:54315,x:180455, y:54993,x:180339, y:55682,x:180271, y:56376,x:180250, y:57000,x:180274, y:57697,x:180347, y:58391,x:180468, y:59079,x:180637, y:59756,x:180853, y:60420,x:181114, y:61067,x:181420, y:61694,x:181769, y:62299,x:182159, y:62877,x:182589, y:63427,x:183056, y:63946,x:183558, y:64431,x:184093, y:64880,x:184658, y:65290,x:185000, y:65504,x:185000, y:67000,x:175000, y:67000,x:175000, y:65504,x:175342, y:65290,x:175907, y:64880,x:176442, y:64431,x:176944, y:63946,x:177411, y:63427,x:177841, y:62877,x:178231, y:62299,x:178580, y:61694,x:178886, y:61067,x:179147, y:60420,x:179363, y:59756,x:179532, y:59079,x:179653, y:58391,x:179726, y:57697,x:179747, y:56927,x:179718, y:56230,x:179640, y:55537,x:179514, y:54850,x:179340, y:54174,x:179120, y:53512,x:178854, y:52867,x:178543, y:52242,x:178190, y:51640,x:177796, y:51065,x:177362, y:50519,x:176891, y:50003,x:176386, y:49522,x:175848, y:49077,x:175306, y:48688,x:175000, y:48496,x:175000, y:47000,x:185000, y:47000,|";
				TestSinglePathIsInside(polyPath, new IntPoint(178000, 64600), new IntPoint(177700, 49000));
			}

			// Part of the Roctopus
			{
				string polyPath = "x:180987, y:20403,x:181958, y:20720,x:182687, y:21125,x:183185, y:21539,x:183724, y:22089,x:184172, y:22730,x:184432, y:23368,x:184566, y:24089,x:184578, y:24921,x:184441, y:25747,x:184138, y:26475,x:183702, y:27108,x:183167, y:27632,x:182555, y:27938,x:182554, y:27915,x:183212, y:26818,x:183508, y:26189,x:183688, y:25518,x:183702, y:24819,x:183621, y:24184,x:183484, y:23674,x:183255, y:23224,x:182918, y:22778,x:182487, y:22368,x:181976, y:22020,x:181364, y:21750,x:180632, y:21570,x:179830, y:21508,x:178999, y:21597,x:178189, y:21845,x:177449, y:22265,x:176813, y:22859,x:176321, y:23632,x:175995, y:24513,x:175862, y:25429,x:175872, y:26264,x:175972, y:26905,x:176188, y:27451,x:176539, y:27988,x:177591, y:29295,x:178233, y:30002,x:178925, y:30609,x:179753, y:31225,x:181582, y:32508,x:182470, y:33170,x:183251, y:33809,x:184288, y:34722,x:184911, y:35349,x:185137, y:35539,x:186071, y:36681,x:186896, y:38053,x:187406, y:39517,x:187639, y:41002,x:187634, y:42431,x:187518, y:43727,x:187327, y:45100,x:187191, y:46163,x:187244, y:46837,x:187680, y:47318,x:188627, y:47808,x:189985, y:48275,x:191310, y:48512,x:192509, y:48565,x:193422, y:48457,x:194109, y:48238,x:194588, y:47981,x:195075, y:47583,x:195522, y:47082,x:195918, y:46451,x:196200, y:45708,x:196388, y:44868,x:196506, y:43938,x:196730, y:41538,x:196819, y:40094,x:196773, y:37109,x:196834, y:35631,x:196979, y:34313,x:197200, y:33298,x:197487, y:32482,x:197826, y:31759,x:198234, y:31130,x:198724, y:30598,x:199307, y:30160,x:199994, y:29811,x:200755, y:29542,x:201565, y:29346,x:202350, y:29254,x:203038, y:29300,x:203676, y:29448,x:204307, y:29659,x:204899, y:29943,x:205424, y:30309,x:205900, y:30744,x:206341, y:31238,x:206715, y:31833,x:206965, y:32519,x:207143, y:33310,x:207182, y:34036,x:207104, y:34673,x:206913, y:35265,x:206572, y:35843,x:206049, y:36420,x:205435, y:36922,x:205929, y:36318,x:206313, y:35713,x:206555, y:35184,x:206669, y:34662,x:206664, y:34091,x:206565, y:33504,x:206382, y:32797,x:206130, y:32161,x:205799, y:31657,x:205422, y:31244,x:205028, y:30885,x:204600, y:30592,x:204117, y:30379,x:203593, y:30223,x:203038, y:30101,x:202429, y:30065,x:201741, y:30167,x:201062, y:30366,x:200483, y:30623,x:199990, y:30925,x:199574, y:31266,x:199217, y:31675,x:198904, y:32183,x:198633, y:32779,x:198396, y:33458,x:198216, y:34325,x:198104, y:35476,x:198081, y:36793,x:198163, y:38160,x:198353, y:40158,x:198617, y:43362,x:198656, y:44453,x:198615, y:45530,x:198462, y:46622,x:198167, y:47769,x:197709, y:48863,x:197066, y:49776,x:196194, y:50597,x:195038, y:51434,x:193637, y:52208,x:192093, y:52812,x:190610, y:53302,x:189555, y:53699,x:189483, y:53790,x:189240, y:54023,x:189870, y:54330,x:191325, y:54913,x:191948, y:55224,x:192658, y:55617,x:193809, y:56344,x:195832, y:57799,x:196769, y:58278,x:197372, y:58499,x:197647, y:58583,x:198439, y:58743,x:199215, y:58770,x:200046, y:58679,x:200931, y:58398,x:201874, y:57859,x:202756, y:57213,x:203460, y:56618,x:204312, y:55816,x:205986, y:54164,x:206874, y:53342,x:207625, y:52798,x:208545, y:52312,x:209568, y:51966,x:210726, y:51807,x:212015, y:51921,x:213285, y:52307,x:214386, y:52963,x:215257, y:53751,x:215835, y:54533,x:216176, y:55228,x:216419, y:55928,x:216533, y:56583,x:216548, y:57239,x:216479, y:57902,x:216334, y:58558,x:216106, y:59194,x:215789, y:59796,x:215395, y:60348,x:214941, y:60836,x:214419, y:61238,x:213825, y:61537,x:213167, y:61717,x:212305, y:61786,x:211454, y:61716,x:210705, y:61550,x:209916, y:61220,x:209234, y:60749,x:208680, y:60175,x:208307, y:59559,x:208098, y:59019,x:208340, y:59545,x:208769, y:60117,x:209370, y:60593,x:210067, y:60933,x:210763, y:61096,x:211524, y:61154,x:212240, y:61105,x:212828, y:60984,x:213352, y:60798,x:213759, y:60542,x:214152, y:60192,x:214511, y:59770,x:214815, y:59290,x:215054, y:58788,x:215216, y:58298,x:215291, y:57950,x:215359, y:57432,x:215365, y:56936,x:215305, y:56473,x:215150, y:55983,x:214881, y:55426,x:214423, y:54818,x:213710, y:54203,x:212829, y:53688,x:211868, y:53383,x:210909, y:53310,x:210037, y:53500,x:209316, y:53802,x:208618, y:54153,x:207997, y:54541,x:207446, y:54969,x:206021, y:56191,x:204174, y:57856,x:203385, y:58526,x:202461, y:59199,x:201468, y:59819,x:200471, y:60329,x:199478, y:60703,x:198498, y:60915,x:197518, y:60972,x:196524, y:60881,x:195578, y:60684,x:194820, y:60448,x:194437, y:60302,x:192694, y:59529,x:191458, y:59122,x:190389, y:59075,x:189599, y:59243,x:189119, y:59480,x:188967, y:59755,x:189138, y:60058,x:189492, y:60369,x:189982, y:60778,x:190706, y:61527,x:192915, y:64443,x:194141, y:65892,x:195052, y:66844,x:195423, y:67211,x:196758, y:68398,x:198071, y:69403,x:199439, y:70365,x:200717, y:71099,x:202913, y:72157,x:204008, y:72800,x:204968, y:73463,x:205634, y:74031,x:206099, y:74589,x:206454, y:75221,x:206715, y:75884,x:206896, y:76533,x:207018, y:77274,x:207099, y:78211,x:207085, y:79216,x:206922, y:80161,x:206631, y:81044,x:206235, y:81863,x:205756, y:82559,x:205214, y:83077,x:204585, y:83487,x:203844, y:83863,x:203024, y:84143,x:202158, y:84264,x:201327, y:84245,x:200611, y:84099,x:199981, y:83836,x:199208, y:83381,x:198524, y:82824,x:198000, y:82203,x:197634, y:81550,x:197428, y:80900,x:197369, y:80197,x:197444, y:79391,x:197649, y:78570,x:197947, y:77856,x:198417, y:77141,x:199039, y:76555,x:199747, y:76111,x:200465, y:75861,x:201119, y:75768,x:201568, y:75828,x:201370, y:75857,x:200504, y:76041,x:199836, y:76319,x:199224, y:76786,x:198722, y:77383,x:198375, y:78043,x:198143, y:78740,x:198016, y:79457,x:197992, y:80115,x:198066, y:80648,x:198260, y:81141,x:198596, y:81674,x:199057, y:82193,x:199610, y:82627,x:200233, y:82994,x:200788, y:83214,x:201351, y:83321,x:201979, y:83341,x:202645, y:83249,x:203325, y:83020,x:203958, y:82705,x:204484, y:82351,x:204925, y:81918,x:205306, y:81366,x:205608, y:80745,x:205819, y:80110,x:205929, y:79424,x:205930, y:78648,x:205852, y:77906,x:205726, y:77320,x:205538, y:76797,x:205279, y:76245,x:204919, y:75718,x:204431, y:75271,x:203748, y:74854,x:202806, y:74411,x:201708, y:74016,x:200555, y:73744,x:199238, y:73411,x:197642, y:72835,x:195746, y:72077,x:194074, y:71350,x:192410, y:70529,x:191010, y:69738,x:189880, y:69177,x:189033, y:69002,x:188632, y:69143,x:188385, y:69199,x:188137, y:69548,x:187886, y:69735,x:187701, y:70180,x:187481, y:70479,x:187298, y:70942,x:187111, y:71313,x:186769, y:72125,x:186440, y:72800,x:186051, y:73382,x:185528, y:73918,x:184909, y:74379,x:184231, y:74736,x:183561, y:75004,x:182965, y:75199,x:182284, y:75347,x:181327, y:75468,x:180496, y:75700,x:180318, y:75949,x:180141, y:76112,x:180086, y:76664,x:180173, y:77274,x:180368, y:77957,x:180679, y:78667,x:181066, y:79371,x:181480, y:80035,x:182251, y:81168,x:182632, y:81660,x:184193, y:83325,x:184754, y:83985,x:185231, y:84649,x:185585, y:85388,x:185780, y:86273,x:185815, y:87357,x:185684, y:88689,x:185330, y:90050,x:184696, y:91229,x:183903, y:92198,x:183076, y:92932,x:182176, y:93440,x:181171, y:93732,x:180096, y:93801,x:178993, y:93641,x:177995, y:93326,x:177232, y:92923,x:176627, y:92444,x:176105, y:91903,x:175681, y:91306,x:175373, y:90655,x:175171, y:89917,x:175070, y:89059,x:175129, y:88161,x:175405, y:87296,x:175879, y:86467,x:176543, y:85691,x:177298, y:85058,x:178055, y:84644,x:178755, y:84418,x:179373, y:84351,x:179442, y:84380,x:179770, y:84454,x:179747, y:84655,x:179364, y:84745,x:178801, y:84821,x:178187, y:85061,x:177588, y:85492,x:177068, y:86120,x:176648, y:86853,x:176330, y:87663,x:176168, y:88425,x:176134, y:89154,x:176208, y:89794,x:176365, y:90293,x:176618, y:90728,x:176984, y:91168,x:177444, y:91578,x:177986, y:91925,x:178621, y:92198,x:179363, y:92384,x:180167, y:92449,x:180996, y:92362,x:181802, y:92108,x:182541, y:91676,x:183176, y:91062,x:183671, y:90261,x:183998, y:89346,x:184126, y:88390,x:184115, y:87534,x:184021, y:86837,x:183834, y:86301,x:183533, y:85823,x:183188, y:85437,x:182689, y:84952,x:181611, y:83970,x:180398, y:82914,x:179492, y:82110,x:178808, y:81390,x:178359, y:80861,x:177642, y:79951,x:177167, y:79245,x:176767, y:78517,x:176405, y:77628,x:176005, y:76582,x:175847, y:76085,x:175745, y:75690,x:175599, y:75315,x:175501, y:74961,x:175368, y:74780,x:175135, y:74336,x:174982, y:74209,x:174599, y:73774,x:174020, y:73279,x:173611, y:72735,x:173276, y:72076,x:172946, y:71256,x:172661, y:70411,x:172373, y:69309,x:172153, y:68586,x:172033, y:68314,x:171817, y:68071,x:171579, y:67664,x:171237, y:67464,x:170765, y:67107,x:169792, y:66739,x:168840, y:66649,x:167984, y:66723,x:167083, y:66840,x:166292, y:66981,x:165533, y:67146,x:164756, y:67395,x:163912, y:67811,x:163137, y:68366,x:162573, y:69023,x:162218, y:69839,x:162087, y:70880,x:162138, y:72011,x:162333, y:73102,x:162588, y:74209,x:162817, y:75390,x:162978, y:76630,x:163023, y:77913,x:162955, y:79071,x:162775, y:79935,x:162504, y:80635,x:162163, y:81306,x:161700, y:81957,x:161060, y:82599,x:160310, y:83177,x:159517, y:83634,x:158728, y:83939,x:157988, y:84060,x:157236, y:84044,x:156411, y:83935,x:155575, y:83701,x:154792, y:83309,x:154120, y:82823,x:153615, y:82301,x:153215, y:81684,x:152866, y:80916,x:152620, y:80086,x:152541, y:79307,x:152608, y:78582,x:152830, y:77911,x:152905, y:77822,x:153207, y:77342,x:153511, y:77055,x:153775, y:76765,x:154348, y:76321,x:154577, y:76176,x:154461, y:76276,x:153922, y:76893,x:153761, y:77144,x:153558, y:77516,x:153417, y:77865,x:153314, y:78549,x:153308, y:79182,x:153393, y:79812,x:153624, y:80561,x:153923, y:81187,x:154258, y:81676,x:154662, y:82080,x:155168, y:82445,x:155769, y:82741,x:156457, y:82933,x:157156, y:83026,x:157788, y:83028,x:158393, y:82918,x:159015, y:82675,x:159606, y:82322,x:160121, y:81885,x:160548, y:81389,x:160874, y:80861,x:161119, y:80309,x:161301, y:79736,x:161405, y:79006,x:161412, y:77982,x:161296, y:76799,x:161029, y:75583,x:160675, y:74363,x:160299, y:73166,x:160009, y:71911,x:159909, y:70518,x:160011, y:69167,x:160320, y:68050,x:160947, y:67008,x:161956, y:65962,x:163111, y:65032,x:164168, y:64343,x:165180, y:63751,x:166193, y:63128,x:169256, y:61157,x:170457, y:60375,x:170856, y:60050,x:171030, y:59754,x:170876, y:59482,x:170285, y:59194,x:169576, y:58985,x:168448, y:58773,x:167071, y:58584,x:163714, y:58186,x:162743, y:58110,x:161898, y:58082,x:161169, y:58093,x:160519, y:58170,x:159905, y:58340,x:159294, y:58596,x:158653, y:58921,x:157303, y:59682,x:155806, y:60502,x:155027, y:60894,x:154125, y:61309,x:153354, y:61596,x:152453, y:61862,x:151577, y:62081,x:150889, y:62228,x:150177, y:62296,x:149238, y:62279,x:148390, y:62209,x:147601, y:62086,x:146995, y:61903,x:146363, y:61625,x:145752, y:61276,x:145207, y:60881,x:144708, y:60354,x:144229, y:59610,x:143852, y:58830,x:143621, y:58096,x:143523, y:57432,x:143495, y:56752,x:143546, y:56061,x:143692, y:55379,x:143932, y:54723,x:144260, y:54104,x:144661, y:53538,x:145123, y:53043,x:145645, y:52636,x:146227, y:52335,x:146903, y:52145,x:147203, y:52118,x:147709, y:52136,x:148517, y:52310,x:148783, y:52406,x:148506, y:52414,x:147746, y:52565,x:147265, y:52729,x:147102, y:52763,x:146599, y:52959,x:146180, y:53238,x:145787, y:53617,x:145433, y:54071,x:145136, y:54583,x:144911, y:55120,x:144796, y:55563,x:144698, y:56144,x:144649, y:56637,x:144650, y:57123,x:144712, y:57597,x:144870, y:58133,x:145167, y:58806,x:145549, y:59448,x:145965, y:59889,x:146402, y:60213,x:146853, y:60491,x:147311, y:60707,x:147764, y:60865,x:148283, y:60970,x:148942, y:61030,x:149596, y:61038,x:150096, y:60984,x:150239, y:60953,x:151213, y:60695,x:151891, y:60468,x:152537, y:60200,x:153221, y:59868,x:154015, y:59447,x:154833, y:58965,x:155583, y:58449,x:157038, y:57384,x:157803, y:56898,x:158621, y:56497,x:159490, y:56183,x:160405, y:55955,x:161499, y:55781,x:164223, y:55542,x:165291, y:55408,x:167049, y:55136,x:168596, y:54786,x:169718, y:54441,x:170062, y:54350,x:170851, y:53913,x:170864, y:53667,x:170739, y:53147,x:170522, y:52750,x:170385, y:52433,x:170029, y:51889,x:169300, y:51088,x:168388, y:50170,x:167525, y:49462,x:166476, y:48856,x:165272, y:48405,x:164414, y:48024,x:164118, y:47844,x:163719, y:47717,x:163189, y:47476,x:163013, y:47322,x:162616, y:47021,x:162001, y:46134,x:161901, y:45177,x:162149, y:44587,x:162664, y:44069,x:163078, y:43816,x:163365, y:43405,x:163679, y:43268,x:163830, y:43252,x:164332, y:43318,x:164394, y:43314,x:164959, y:43432,x:165109, y:43438,x:165453, y:43533,x:165820, y:43725,x:167689, y:44205,x:169414, y:44990,x:170733, y:45625,x:172387, y:46390,x:173412, y:47007,x:174299, y:47493,x:175117, y:47775,x:175872, y:47756,x:176613, y:47541,x:177312, y:47277,x:177970, y:46982,x:178587, y:46649,x:179042, y:46341,x:179401, y:46069,x:179980, y:45605,x:181197, y:44579,x:182196, y:43578,x:182978, y:42509,x:183391, y:41461,x:183450, y:40434,x:183280, y:39468,x:182999, y:38600,x:182609, y:37809,x:182113, y:37076,x:181582, y:36426,x:181090, y:35879,x:180500, y:35289,x:177903, y:32811,x:177068, y:31941,x:176264, y:30984,x:175554, y:30014,x:174998, y:29106,x:174591, y:28231,x:174329, y:27355,x:174237, y:26350,x:174342, y:25087,x:174687, y:23785,x:175316, y:22660,x:176105, y:21736,x:176931, y:21039,x:177828, y:20559,x:178831, y:20288,x:179898, y:20235,|";
				//TestSinglePathIsInside(polyPath, new IntPoint(153099, 78023), new IntPoint(153104, 77984));
			}

			// small 'ab' text
			{
				string polyPath = "x:106312, y:94207,x:106981, y:94453,x:107541, y:94846,x:107993, y:95371,x:108311, y:95963,x:108584, y:96774,x:108726, y:97616,x:108774, y:98539,x:108729, y:99447,x:108594, y:100281,x:108365, y:101024,x:108035, y:101661,x:107600, y:102181,x:107071, y:102566,x:106428, y:102809,x:105667, y:102889,x:104808, y:102794,x:104062, y:102514,x:103438, y:102008,x:102968, y:101265,x:102938, y:101275,x:102960, y:101922,x:102969, y:105875,x:100774, y:105875,x:100773, y:95721,x:100711, y:94281,x:102845, y:94285,x:102899, y:94675,x:102955, y:95640,x:102969, y:95640,x:103197, y:95245,x:103454, y:94929,x:103749, y:94667,x:104071, y:94460,x:104417, y:94306,x:104782, y:94203,x:105157, y:94144,x:105532, y:94125,|x:104409, y:95641,x:104075, y:95759,x:103770, y:95939,x:103501, y:96218,x:103273, y:96602,x:103093, y:97100,x:102977, y:97710,x:102938, y:98477,x:102977, y:99247,x:103094, y:99878,x:103287, y:100398,x:103508, y:100769,x:103784, y:101051,x:104094, y:101234,x:104424, y:101333,x:104758, y:101367,x:105526, y:101193,x:106063, y:100671,x:106379, y:99791,x:106485, y:98539,x:106372, y:97217,x:106032, y:96316,x:105483, y:95797,x:104736, y:95626,|";
				TestSinglePathIsInside(polyPath, new IntPoint(102125, 96835), new IntPoint(104770, 101569));
			}

			// Here is a test case that was failing.
			// A pathing layer from a complex model (part of a skeletal face)
			{
				string polyPath = "x:181532, y:32784,x:182504, y:33027,x:182999, y:33218,x:183662, y:33512,x:184507, y:33965,x:185051, y:34758,x:185172, y:34836,x:185527, y:34975,x:186756, y:35606,x:186941, y:35749,x:187345, y:35918,x:187631, y:36096,x:188163, y:36270,x:188743, y:36524,x:189168, y:36659,x:189814, y:36917,x:190668, y:37193,x:191321, y:37463,x:191549, y:37577,x:192009, y:37882,x:192095, y:37971,x:192344, y:38292,x:192465, y:38535,x:192556, y:38842,x:192583, y:39357,x:192624, y:39517,x:192924, y:39710,x:192958, y:39753,x:194292, y:40061,x:194464, y:40125,x:195907, y:40772,x:195983, y:40959,x:196188, y:41617,x:196244, y:41671,x:197050, y:41397,x:197121, y:41398,x:197644, y:41731,x:197883, y:41975,x:198194, y:42190,x:198325, y:42241,x:198759, y:42236,x:199291, y:42065,x:199613, y:41884,x:200110, y:41574,x:200987, y:40990,x:204097, y:39314,x:204576, y:39117,x:204989, y:39102,x:205668, y:39293,x:206060, y:39646,x:206268, y:40026,x:207504, y:42782,x:208074, y:44104,x:208193, y:44583,x:208207, y:45057,x:208160, y:45478,x:208188, y:45821,x:208228, y:45939,x:208281, y:46224,x:208408, y:46499,x:208598, y:46814,x:208779, y:47188,x:209056, y:48025,x:209041, y:48582,x:208846, y:49283,x:208785, y:49601,x:208771, y:49874,x:208858, y:50376,x:209148, y:51060,x:210336, y:53089,x:210454, y:53242,x:210449, y:54009,x:210627, y:54250,x:210868, y:54344,x:211457, y:54436,x:211658, y:54615,x:212896, y:55129,x:213092, y:55269,x:213484, y:55708,x:213597, y:55871,x:214107, y:56730,x:214874, y:58189,x:215463, y:59462,x:215778, y:60488,x:215976, y:61472,x:216050, y:61574,x:216157, y:61809,x:216206, y:61850,x:216687, y:61975,x:218392, y:61633,x:218974, y:61691,x:219317, y:61776,x:220553, y:62169,x:222296, y:62850,x:223266, y:63210,x:224479, y:63755,x:224857, y:64013,x:224926, y:64084,x:225100, y:64341,x:225065, y:64596,x:224778, y:65059,x:223587, y:66364,x:222423, y:67721,x:221358, y:69067,x:220890, y:69696,x:219938, y:70898,x:219362, y:71491,x:218997, y:71809,x:218602, y:71946,x:218521, y:71987,x:218084, y:72010,x:217683, y:71932,x:217051, y:71726,x:216140, y:71305,x:215464, y:70949,x:214960, y:70610,x:214393, y:70162,x:213663, y:69449,x:211632, y:67231,x:208324, y:63761,x:206415, y:61869,x:204250, y:59705,x:199615, y:54860,x:198702, y:53828,x:197920, y:52801,x:197018, y:51717,x:196391, y:51088,x:196120, y:50841,x:195194, y:50147,x:194381, y:49624,x:193646, y:49201,x:192819, y:48851,x:191342, y:48411,x:190230, y:48181,x:189270, y:47941,x:188801, y:47804,x:187261, y:47411,x:185949, y:47162,x:184678, y:46956,x:183352, y:46817,x:182180, y:46755,x:180256, y:46712,x:177932, y:46755,x:176295, y:46832,x:175231, y:46953,x:173850, y:47175,x:172886, y:47362,x:169916, y:48126,x:168594, y:48396,x:167470, y:48718,x:166545, y:49046,x:165496, y:49621,x:164989, y:49940,x:164444, y:50324,x:163529, y:51054,x:162722, y:51915,x:162320, y:52400,x:161498, y:53446,x:160268, y:54851,x:155439, y:59894,x:152728, y:62612,x:149276, y:66156,x:147726, y:67806,x:146162, y:69552,x:145674, y:70033,x:145026, y:70574,x:144827, y:70694,x:144439, y:70964,x:143936, y:71209,x:143418, y:71488,x:142646, y:71828,x:141898, y:72065,x:141631, y:72057,x:141096, y:71995,x:140963, y:71945,x:140379, y:71402,x:139419, y:70107,x:138592, y:69087,x:136568, y:66821,x:136363, y:66194,x:136380, y:66161,x:136413, y:66149,x:136567, y:65287,x:136669, y:64565,x:136658, y:64352,x:136822, y:63668,x:137150, y:63139,x:137866, y:62761,x:138780, y:62398,x:139917, y:61993,x:140851, y:61709,x:141344, y:61658,x:141759, y:61680,x:142819, y:61854,x:142941, y:61858,x:143066, y:61828,x:143378, y:61704,x:143716, y:61360,x:143926, y:60834,x:144337, y:59660,x:144490, y:59318,x:145184, y:57896,x:145867, y:56634,x:146568, y:55524,x:146743, y:55402,x:147100, y:55112,x:148109, y:54696,x:148197, y:54640,x:148470, y:54412,x:148934, y:54355,x:149382, y:54189,x:149410, y:53843,x:149518, y:53564,x:149584, y:53033,x:150312, y:51873,x:150966, y:50638,x:151087, y:50277,x:151144, y:50056,x:151156, y:49487,x:150918, y:48536,x:150929, y:47960,x:151082, y:47401,x:151188, y:47196,x:151317, y:46892,x:151410, y:46739,x:151711, y:46085,x:151735, y:45977,x:151755, y:45335,x:151713, y:44731,x:151794, y:44218,x:152133, y:43399,x:153700, y:39919,x:153746, y:39860,x:153938, y:39721,x:154308, y:39392,x:154686, y:39191,x:154807, y:39150,x:155140, y:39108,x:155305, y:39159,x:155637, y:39228,x:158935, y:41008,x:160473, y:41996,x:161073, y:42222,x:161563, y:42258,x:161657, y:42187,x:161804, y:42141,x:162105, y:41913,x:162275, y:41735,x:162772, y:41416,x:162863, y:41419,x:163548, y:41624,x:163684, y:41479,x:163771, y:41477,x:163923, y:40884,x:164040, y:40763,x:165476, y:40102,x:165648, y:40041,x:166762, y:39779,x:167007, y:39622,x:167207, y:39536,x:167245, y:39444,x:167390, y:38769,x:167549, y:38367,x:167868, y:37964,x:168093, y:37774,x:168355, y:37587,x:169096, y:37251,x:169858, y:37014,x:170550, y:36724,x:171458, y:36420,x:171823, y:36246,x:172365, y:36046,x:172802, y:35779,x:173091, y:35655,x:173901, y:35171,x:174324, y:35025,x:174906, y:34622,x:175072, y:34354,x:175627, y:33821,x:175866, y:33676,x:176563, y:33365,x:177786, y:32908,x:178940, y:32718,x:180599, y:32707,|";
				TestSinglePathIsInside(polyPath, new IntPoint(155001, 39529), new IntPoint(138209, 64104));

				polyPath = "x:98796, y:79479,x:100612, y:79435,x:102342, y:79590,x:106724, y:79947,x:107840, y:80148,x:109263, y:80364,x:109974, y:80597,x:110048, y:80635,x:110304, y:80823,x:110511, y:81115,x:110597, y:81343,x:110679, y:81653,x:110838, y:82083,x:111110, y:82188,x:111197, y:82258,x:112542, y:82486,x:113074, y:82640,x:114079, y:83039,x:114502, y:83229,x:114604, y:83336,x:114738, y:83720,x:114809, y:83993,x:115010, y:84116,x:115831, y:83938,x:116355, y:84279,x:116621, y:84531,x:116871, y:84672,x:117145, y:84700,x:117664, y:84582,x:118944, y:83927,x:120279, y:83188,x:122446, y:82041,x:122870, y:81914,x:123105, y:81890,x:123383, y:81935,x:123457, y:81977,x:123731, y:82023,x:123887, y:82117,x:124307, y:82323,x:124538, y:82635,x:124646, y:82732,x:126289, y:86587,x:126500, y:87290,x:126455, y:88342,x:126586, y:88936,x:126555, y:89279,x:126404, y:89795,x:126313, y:90224,x:126377, y:90853,x:126471, y:91157,x:126683, y:91662,x:126953, y:92136,x:128311, y:94320,x:128478, y:95030,x:128604, y:95269,x:128916, y:95320,x:129025, y:95359,x:129229, y:95354,x:129766, y:95221,x:130502, y:95237,x:130544, y:95254,x:130822, y:95481,x:131328, y:96324,x:131482, y:96537,x:134403, y:101920,x:134788, y:103136,x:135001, y:103425,x:135556, y:103616,x:136961, y:103701,x:138093, y:103953,x:140392, y:104653,x:142496, y:105369,x:143121, y:105705,x:143365, y:105954,x:143507, y:106336,x:143419, y:106678,x:143101, y:107229,x:142025, y:108732,x:140953, y:110333,x:139677, y:112307,x:138740, y:113794,x:138310, y:114405,x:138183, y:114527,x:137945, y:114687,x:137498, y:114811,x:137111, y:114776,x:136715, y:114711,x:136313, y:114601,x:135591, y:114343,x:134930, y:114019,x:134323, y:113621,x:133632, y:113067,x:132755, y:112173,x:130424, y:109673,x:127036, y:106145,x:123923, y:102969,x:115520, y:94126,x:114512, y:93164,x:114010, y:92713,x:113651, y:92427,x:113085, y:92020,x:112409, y:91627,x:111708, y:91282,x:109945, y:90631,x:108803, y:90278,x:107605, y:89877,x:106611, y:89582,x:106021, y:89427,x:104933, y:89199,x:104044, y:89091,x:103322, y:89042,x:102727, y:89028,x:100287, y:89102,x:99240, y:89096,x:97197, y:89025,x:95682, y:89101,x:94618, y:89258,x:94109, y:89368,x:92171, y:89917,x:90916, y:90331,x:90132, y:90564,x:88247, y:91256,x:87150, y:91794,x:86092, y:92527,x:85430, y:93092,x:84423, y:94059,x:75924, y:102991,x:73012, y:105974,x:69737, y:109386,x:66951, y:112396,x:66370, y:112980,x:65706, y:113546,x:65105, y:113945,x:64213, y:114374,x:63347, y:114673,x:62738, y:114779,x:62146, y:114758,x:61731, y:114580,x:61614, y:114436,x:61219, y:113845,x:58753, y:110019,x:57793, y:108586,x:56765, y:107173,x:56709, y:107057,x:56556, y:106828,x:56563, y:106716,x:56550, y:106638,x:56629, y:106333,x:56889, y:105847,x:57137, y:105551,x:57297, y:105431,x:58347, y:105049,x:59788, y:104565,x:62489, y:103782,x:63119, y:103687,x:63401, y:103661,x:64299, y:103660,x:64812, y:103560,x:64903, y:103476,x:65097, y:103229,x:65490, y:101951,x:67798, y:97691,x:68384, y:96731,x:68903, y:96056,x:68980, y:95989,x:69637, y:95575,x:70408, y:95623,x:70706, y:95671,x:71456, y:95908,x:71691, y:95922,x:72037, y:95886,x:72092, y:95754,x:71831, y:94497,x:71980, y:93853,x:72033, y:93691,x:72734, y:92497,x:73201, y:91742,x:73515, y:91060,x:73593, y:90789,x:73662, y:90362,x:73657, y:90159,x:73489, y:89426,x:73357, y:89098,x:73381, y:88910,x:73500, y:88410,x:73403, y:87333,x:73618, y:86604,x:75197, y:82858,x:75256, y:82783,x:75334, y:82594,x:75582, y:82367,x:76690, y:81884,x:76873, y:81899,x:77061, y:81870,x:77419, y:82025,x:79500, y:83119,x:81806, y:84411,x:82325, y:84628,x:82868, y:84741,x:83231, y:84597,x:83525, y:84319,x:84138, y:83888,x:84946, y:84158,x:85068, y:84128,x:85306, y:83330,x:85349, y:83265,x:86717, y:82665,x:87594, y:82402,x:88011, y:82311,x:88625, y:82246,x:88795, y:82143,x:88972, y:82106,x:89061, y:81984,x:89117, y:81810,x:89394, y:81121,x:89561, y:80888,x:89911, y:80632,x:90187, y:80507,x:90640, y:80374,x:90969, y:80304,x:93058, y:79973,x:94251, y:79852,x:97579, y:79591,|";
				TestSinglePathIsInside(polyPath, new IntPoint(59966, 105138), new IntPoint(72176, 96073));

				polyPath = "x:61172, y:90528,x:62778, y:85990,x:62968, y:85599,x:63076, y:85443,x:63246, y:85306,x:63553, y:85151,x:64231, y:85121,x:66020, y:85291,x:67460, y:85459,x:67836, y:85559,x:68041, y:85630,x:68403, y:85863,x:68653, y:86080,x:68948, y:86491,x:69324, y:86951,x:69383, y:86998,x:69501, y:86996,x:69885, y:87064,x:70518, y:86788,x:71152, y:86614,x:71850, y:86545,x:72404, y:86606,x:73380, y:86588,x:73619, y:86532,x:74439, y:86281,x:75172, y:85980,x:75404, y:85824,x:76407, y:85018,x:76768, y:84704,x:76898, y:84615,x:77417, y:84404,x:77494, y:84185,x:77630, y:83988,x:77711, y:83738,x:77799, y:83265,x:77881, y:82299,x:77940, y:82186,x:78651, y:81819,x:79078, y:81505,x:81222, y:79325,x:81321, y:79140,x:81555, y:78849,x:81602, y:78756,x:81599, y:78268,x:81542, y:78042,x:81357, y:77681,x:81065, y:77023,x:81059, y:76835,x:81102, y:76577,x:81235, y:76258,x:81509, y:75879,x:82243, y:75088,x:82965, y:74459,x:83137, y:74140,x:83189, y:73949,x:83176, y:73708,x:83203, y:73161,x:83473, y:72538,x:83955, y:71851,x:84232, y:71540,x:84892, y:70947,x:85420, y:70624,x:85784, y:70436,x:86595, y:70134,x:87224, y:69806,x:87309, y:69741,x:87437, y:69606,x:87662, y:69445,x:87757, y:68797,x:87829, y:68435,x:87949, y:68172,x:88461, y:67520,x:89199, y:66830,x:89898, y:66400,x:90725, y:66274,x:90916, y:66270,x:91169, y:66297,x:91458, y:66190,x:91621, y:66007,x:91818, y:65649,x:92340, y:64903,x:93041, y:64014,x:93365, y:63521,x:93881, y:63064,x:94399, y:63022,x:94510, y:63036,x:95316, y:63307,x:95728, y:63387,x:95955, y:63460,x:96236, y:63384,x:96527, y:62957,x:96708, y:62784,x:97115, y:62605,x:97934, y:62525,x:98357, y:62536,x:98942, y:62582,x:99397, y:62765,x:99784, y:62969,x:100009, y:62939,x:100074, y:62957,x:100237, y:62854,x:100711, y:62608,x:102235, y:62536,x:102559, y:62581,x:102928, y:62697,x:103258, y:62822,x:103360, y:62927,x:103748, y:63402,x:104258, y:63405,x:104601, y:63307,x:105406, y:62957,x:105457, y:62957,x:106009, y:63118,x:106341, y:63323,x:106447, y:63413,x:107238, y:64459,x:108048, y:65648,x:108314, y:65998,x:108487, y:66164,x:108768, y:66273,x:109437, y:66285,x:110087, y:66437,x:110362, y:66589,x:110669, y:66797,x:110926, y:67024,x:111603, y:67681,x:111826, y:67941,x:112053, y:68400,x:112161, y:68741,x:112225, y:69277,x:112283, y:69454,x:112421, y:69528,x:112592, y:69737,x:113195, y:70072,x:113500, y:70182,x:113722, y:70353,x:114439, y:70580,x:114840, y:70884,x:115110, y:71045,x:115253, y:71152,x:115564, y:71430,x:115778, y:71677,x:116270, y:72326,x:116560, y:72843,x:116736, y:73507,x:116765, y:73972,x:116833, y:74230,x:117056, y:74557,x:117840, y:75236,x:118735, y:76270,x:118781, y:76419,x:118901, y:77000,x:118805, y:77219,x:118443, y:77831,x:118377, y:77989,x:118316, y:78242,x:118286, y:78516,x:118408, y:78944,x:118775, y:79440,x:119996, y:80678,x:120445, y:81103,x:120891, y:81560,x:121083, y:81733,x:122009, y:82216,x:122112, y:83383,x:122187, y:83753,x:122402, y:84269,x:122579, y:84380,x:122993, y:84687,x:123369, y:84859,x:124545, y:85909,x:124733, y:86033,x:126114, y:86550,x:127219, y:86642,x:128250, y:86542,x:129111, y:86693,x:129671, y:86973,x:130150, y:87041,x:130598, y:86933,x:130640, y:86907,x:131150, y:86345,x:131387, y:86019,x:131634, y:85791,x:132132, y:85553,x:132297, y:85498,x:133639, y:85318,x:134657, y:85228,x:134881, y:85261,x:136577, y:86094,x:137262, y:86528,x:137432, y:86816,x:138747, y:90551,x:138768, y:90910,x:138647, y:91531,x:138307, y:92477,x:138344, y:92608,x:138367, y:92946,x:138493, y:93142,x:138690, y:93387,x:138791, y:93462,x:138914, y:93801,x:138954, y:94303,x:139018, y:94682,x:139040, y:95150,x:138977, y:95486,x:138808, y:95747,x:138628, y:96256,x:138474, y:96450,x:138225, y:96559,x:138186, y:96607,x:136854, y:96923,x:136306, y:97088,x:136159, y:97183,x:135906, y:97403,x:134979, y:97500,x:134574, y:97578,x:134382, y:97631,x:134139, y:97773,x:134010, y:97908,x:133857, y:98245,x:133791, y:99062,x:134049, y:99550,x:134206, y:99940,x:133954, y:101861,x:133966, y:103594,x:133984, y:103811,x:134115, y:104417,x:134549, y:105558,x:134617, y:105906,x:134768, y:106501,x:135563, y:107708,x:135783, y:108270,x:135856, y:108512,x:135994, y:108810,x:136417, y:109588,x:137257, y:110596,x:137886, y:111179,x:138335, y:111379,x:139013, y:111418,x:140483, y:111335,x:141463, y:111302,x:141662, y:111243,x:142496, y:111141,x:142614, y:111247,x:142678, y:111274,x:143343, y:113009,x:143470, y:113277,x:143509, y:113603,x:143460, y:113868,x:143343, y:114133,x:142931, y:114659,x:142588, y:115253,x:142501, y:115351,x:142071, y:116456,x:142116, y:116760,x:142221, y:117186,x:142391, y:117563,x:142594, y:117871,x:143020, y:118215,x:143264, y:118359,x:144191, y:118640,x:144474, y:118703,x:145356, y:118950,x:145908, y:119267,x:146335, y:119771,x:146617, y:120220,x:148086, y:122437,x:150674, y:126078,x:151128, y:126742,x:153389, y:130223,x:154078, y:131425,x:154429, y:132216,x:154702, y:132630,x:155026, y:133459,x:155065, y:133525,x:155116, y:133843,x:155125, y:134227,x:155058, y:134832,x:154926, y:135155,x:154332, y:136263,x:153749, y:136967,x:152672, y:138172,x:151950, y:138710,x:150178, y:139750,x:149347, y:140174,x:149123, y:140335,x:148398, y:140733,x:147561, y:141015,x:146687, y:141214,x:146395, y:141264,x:145233, y:141353,x:145082, y:141339,x:144523, y:141212,x:143236, y:140515,x:142861, y:140221,x:142372, y:139795,x:141838, y:139192,x:141643, y:138944,x:141270, y:138288,x:141039, y:137813,x:140741, y:136855,x:140509, y:135483,x:139559, y:124715,x:139394, y:123473,x:139004, y:121469,x:138556, y:119689,x:138412, y:119261,x:137506, y:116084,x:136991, y:113779,x:136597, y:112202,x:135934, y:110068,x:135882, y:109704,x:135809, y:109444,x:135314, y:108297,x:135185, y:107881,x:135175, y:107920,x:134738, y:106818,x:134194, y:105803,x:133803, y:105239,x:133078, y:104359,x:132430, y:103690,x:132083, y:103384,x:130639, y:102277,x:129880, y:101758,x:127928, y:100362,x:126273, y:99039,x:124690, y:97625,x:123187, y:96341,x:121620, y:95091,x:120595, y:94413,x:119777, y:93947,x:118355, y:93298,x:117177, y:92856,x:115345, y:92337,x:114794, y:92199,x:113381, y:91924,x:112246, y:91784,x:111457, y:91761,x:109761, y:91808,x:107025, y:91826,x:105996, y:91849,x:97712, y:91831,x:95635, y:91850,x:93106, y:91839,x:92571, y:91820,x:88111, y:91804,x:87294, y:91847,x:86375, y:91953,x:85166, y:92176,x:84481, y:92337,x:82783, y:92835,x:81704, y:93225,x:80449, y:93783,x:79540, y:94255,x:78471, y:94954,x:77030, y:96046,x:75027, y:97754,x:73731, y:98920,x:72955, y:99562,x:71882, y:100402,x:68847, y:102567,x:67767, y:103427,x:66861, y:104331,x:66213, y:105141,x:65548, y:106256,x:65120, y:107133,x:64587, y:108352,x:64265, y:109189,x:63764, y:110642,x:63324, y:112049,x:63054, y:113029,x:62390, y:115971,x:61824, y:118015,x:61376, y:119473,x:61202, y:120088,x:60810, y:121743,x:60661, y:122481,x:60440, y:123797,x:60291, y:124835,x:59717, y:131789,x:59335, y:135826,x:59083, y:137118,x:58861, y:137903,x:58786, y:138115,x:58353, y:139071,x:58272, y:139214,x:57862, y:139734,x:57597, y:140008,x:57128, y:140417,x:56957, y:140527,x:55634, y:141261,x:54739, y:141520,x:54227, y:141509,x:53713, y:141455,x:53357, y:141365,x:52803, y:141195,x:51038, y:140442,x:50912, y:140363,x:49570, y:139701,x:49349, y:139570,x:48711, y:139061,x:48030, y:138607,x:47216, y:137947,x:46636, y:137336,x:45996, y:136606,x:45557, y:136010,x:44759, y:134683,x:44736, y:134456,x:44722, y:133959,x:44860, y:133499,x:45070, y:133110,x:45409, y:132285,x:45472, y:132191,x:46179, y:130768,x:46967, y:129522,x:49160, y:126191,x:51524, y:122876,x:53535, y:119837,x:53907, y:119386,x:54058, y:119275,x:54599, y:119009,x:55996, y:118665,x:56676, y:118353,x:56880, y:118218,x:57077, y:117999,x:57470, y:117636,x:57770, y:117126,x:57869, y:116857,x:57813, y:116384,x:57548, y:115629,x:56990, y:114696,x:56827, y:114500,x:56499, y:113987,x:56446, y:113825,x:56406, y:113546,x:56414, y:113450,x:56502, y:113114,x:56644, y:112809,x:57048, y:111776,x:57361, y:111343,x:57675, y:111294,x:58033, y:111327,x:58381, y:111408,x:58931, y:111415,x:59615, y:111302,x:60205, y:111058,x:60414, y:110924,x:60750, y:110625,x:61308, y:109937,x:61613, y:109479,x:61757, y:109122,x:62054, y:108607,x:63044, y:106591,x:63187, y:106217,x:63383, y:105831,x:63637, y:105202,x:63957, y:104272,x:64099, y:103725,x:64192, y:102816,x:64131, y:101890,x:64073, y:101702,x:64067, y:99841,x:63905, y:98213,x:63858, y:97971,x:63465, y:97669,x:61997, y:97277,x:61566, y:96776,x:61420, y:96390,x:61166, y:95946,x:61046, y:95636,x:60939, y:95437,x:60887, y:95062,x:60881, y:94838,x:60956, y:94121,x:61039, y:93657,x:61127, y:93404,x:61561, y:93007,x:61622, y:92484,x:61181, y:91290,x:61140, y:90833,|";
				TestSinglePathIsInside(polyPath, new IntPoint(135177, 107718), new IntPoint(64040, 97859));

				polyPath = "x:61172, y:90528,x:62778, y:85990,x:62968, y:85599,x:63076, y:85443,x:63246, y:85306,x:63553, y:85151,x:64231, y:85121,x:66020, y:85291,x:67460, y:85459,x:67836, y:85559,x:68041, y:85630,x:68403, y:85863,x:68653, y:86080,x:68948, y:86491,x:69324, y:86951,x:69383, y:86998,x:69501, y:86996,x:69885, y:87064,x:70518, y:86788,x:71152, y:86614,x:71850, y:86545,x:72404, y:86606,x:73380, y:86588,x:73619, y:86532,x:74439, y:86281,x:75172, y:85980,x:75404, y:85824,x:76407, y:85018,x:76768, y:84704,x:76898, y:84615,x:77417, y:84404,x:77494, y:84185,x:77630, y:83988,x:77711, y:83738,x:77799, y:83265,x:77881, y:82299,x:77940, y:82186,x:78651, y:81819,x:79078, y:81505,x:81222, y:79325,x:81321, y:79140,x:81555, y:78849,x:81602, y:78756,x:81599, y:78268,x:81542, y:78042,x:81357, y:77681,x:81065, y:77023,x:81059, y:76835,x:81102, y:76577,x:81235, y:76258,x:81509, y:75879,x:82243, y:75088,x:82965, y:74459,x:83137, y:74140,x:83189, y:73949,x:83176, y:73708,x:83203, y:73161,x:83473, y:72538,x:83955, y:71851,x:84232, y:71540,x:84892, y:70947,x:85420, y:70624,x:85784, y:70436,x:86595, y:70134,x:87224, y:69806,x:87309, y:69741,x:87437, y:69606,x:87662, y:69445,x:87757, y:68797,x:87829, y:68435,x:87949, y:68172,x:88461, y:67520,x:89199, y:66830,x:89898, y:66400,x:90725, y:66274,x:90916, y:66270,x:91169, y:66297,x:91458, y:66190,x:91621, y:66007,x:91818, y:65649,x:92340, y:64903,x:93041, y:64014,x:93365, y:63521,x:93881, y:63064,x:94399, y:63022,x:94510, y:63036,x:95316, y:63307,x:95728, y:63387,x:95955, y:63460,x:96236, y:63384,x:96527, y:62957,x:96708, y:62784,x:97115, y:62605,x:97934, y:62525,x:98357, y:62536,x:98942, y:62582,x:99397, y:62765,x:99784, y:62969,x:100009, y:62939,x:100074, y:62957,x:100237, y:62854,x:100711, y:62608,x:102235, y:62536,x:102559, y:62581,x:102928, y:62697,x:103258, y:62822,x:103360, y:62927,x:103748, y:63402,x:104258, y:63405,x:104601, y:63307,x:105406, y:62957,x:105457, y:62957,x:106009, y:63118,x:106341, y:63323,x:106447, y:63413,x:107238, y:64459,x:108048, y:65648,x:108314, y:65998,x:108487, y:66164,x:108768, y:66273,x:109437, y:66285,x:110087, y:66437,x:110362, y:66589,x:110669, y:66797,x:110926, y:67024,x:111603, y:67681,x:111826, y:67941,x:112053, y:68400,x:112161, y:68741,x:112225, y:69277,x:112283, y:69454,x:112421, y:69528,x:112592, y:69737,x:113195, y:70072,x:113500, y:70182,x:113722, y:70353,x:114439, y:70580,x:114840, y:70884,x:115110, y:71045,x:115253, y:71152,x:115564, y:71430,x:115778, y:71677,x:116270, y:72326,x:116560, y:72843,x:116736, y:73507,x:116765, y:73972,x:116833, y:74230,x:117056, y:74557,x:117840, y:75236,x:118735, y:76270,x:118781, y:76419,x:118901, y:77000,x:118805, y:77219,x:118443, y:77831,x:118377, y:77989,x:118316, y:78242,x:118286, y:78516,x:118408, y:78944,x:118775, y:79440,x:119996, y:80678,x:120445, y:81103,x:120891, y:81560,x:121083, y:81733,x:122009, y:82216,x:122112, y:83383,x:122187, y:83753,x:122402, y:84269,x:122579, y:84380,x:122993, y:84687,x:123369, y:84859,x:124545, y:85909,x:124733, y:86033,x:126114, y:86550,x:127219, y:86642,x:128250, y:86542,x:129111, y:86693,x:129671, y:86973,x:130150, y:87041,x:130598, y:86933,x:130640, y:86907,x:131150, y:86345,x:131387, y:86019,x:131634, y:85791,x:132132, y:85553,x:132297, y:85498,x:133639, y:85318,x:134657, y:85228,x:134881, y:85261,x:136577, y:86094,x:137262, y:86528,x:137432, y:86816,x:138747, y:90551,x:138768, y:90910,x:138647, y:91531,x:138307, y:92477,x:138344, y:92608,x:138367, y:92946,x:138493, y:93142,x:138690, y:93387,x:138791, y:93462,x:138914, y:93801,x:138954, y:94303,x:139018, y:94682,x:139040, y:95150,x:138977, y:95486,x:138808, y:95747,x:138628, y:96256,x:138474, y:96450,x:138225, y:96559,x:138186, y:96607,x:136854, y:96923,x:136306, y:97088,x:136159, y:97183,x:135906, y:97403,x:134979, y:97500,x:134574, y:97578,x:134382, y:97631,x:134139, y:97773,x:134010, y:97908,x:133857, y:98245,x:133791, y:99062,x:134049, y:99550,x:134206, y:99940,x:133954, y:101861,x:133966, y:103594,x:133984, y:103811,x:134115, y:104417,x:134549, y:105558,x:134617, y:105906,x:134768, y:106501,x:135563, y:107708,x:135783, y:108270,x:135856, y:108512,x:135994, y:108810,x:136417, y:109588,x:137257, y:110596,x:137886, y:111179,x:138335, y:111379,x:139013, y:111418,x:140483, y:111335,x:141463, y:111302,x:141662, y:111243,x:142496, y:111141,x:142614, y:111247,x:142678, y:111274,x:143343, y:113009,x:143470, y:113277,x:143509, y:113603,x:143460, y:113868,x:143343, y:114133,x:142931, y:114659,x:142588, y:115253,x:142501, y:115351,x:142071, y:116456,x:142116, y:116760,x:142221, y:117186,x:142391, y:117563,x:142594, y:117871,x:143020, y:118215,x:143264, y:118359,x:144191, y:118640,x:144474, y:118703,x:145356, y:118950,x:145908, y:119267,x:146335, y:119771,x:146617, y:120220,x:148086, y:122437,x:150674, y:126078,x:151128, y:126742,x:153389, y:130223,x:154078, y:131425,x:154429, y:132216,x:154702, y:132630,x:155026, y:133459,x:155065, y:133525,x:155116, y:133843,x:155125, y:134227,x:155058, y:134832,x:154926, y:135155,x:154332, y:136263,x:153749, y:136967,x:152672, y:138172,x:151950, y:138710,x:150178, y:139750,x:149347, y:140174,x:149123, y:140335,x:148398, y:140733,x:147561, y:141015,x:146687, y:141214,x:146395, y:141264,x:145233, y:141353,x:145082, y:141339,x:144523, y:141212,x:143236, y:140515,x:142861, y:140221,x:142372, y:139795,x:141838, y:139192,x:141643, y:138944,x:141270, y:138288,x:141039, y:137813,x:140741, y:136855,x:140509, y:135483,x:139559, y:124715,x:139394, y:123473,x:139004, y:121469,x:138556, y:119689,x:138412, y:119261,x:137506, y:116084,x:136991, y:113779,x:136597, y:112202,x:135934, y:110068,x:135882, y:109704,x:135809, y:109444,x:135314, y:108297,x:135185, y:107881,x:135175, y:107920,x:134738, y:106818,x:134194, y:105803,x:133803, y:105239,x:133078, y:104359,x:132430, y:103690,x:132083, y:103384,x:130639, y:102277,x:129880, y:101758,x:127928, y:100362,x:126273, y:99039,x:124690, y:97625,x:123187, y:96341,x:121620, y:95091,x:120595, y:94413,x:119777, y:93947,x:118355, y:93298,x:117177, y:92856,x:115345, y:92337,x:114794, y:92199,x:113381, y:91924,x:112246, y:91784,x:111457, y:91761,x:109761, y:91808,x:107025, y:91826,x:105996, y:91849,x:97712, y:91831,x:95635, y:91850,x:93106, y:91839,x:92571, y:91820,x:88111, y:91804,x:87294, y:91847,x:86375, y:91953,x:85166, y:92176,x:84481, y:92337,x:82783, y:92835,x:81704, y:93225,x:80449, y:93783,x:79540, y:94255,x:78471, y:94954,x:77030, y:96046,x:75027, y:97754,x:73731, y:98920,x:72955, y:99562,x:71882, y:100402,x:68847, y:102567,x:67767, y:103427,x:66861, y:104331,x:66213, y:105141,x:65548, y:106256,x:65120, y:107133,x:64587, y:108352,x:64265, y:109189,x:63764, y:110642,x:63324, y:112049,x:63054, y:113029,x:62390, y:115971,x:61824, y:118015,x:61376, y:119473,x:61202, y:120088,x:60810, y:121743,x:60661, y:122481,x:60440, y:123797,x:60291, y:124835,x:59717, y:131789,x:59335, y:135826,x:59083, y:137118,x:58861, y:137903,x:58786, y:138115,x:58353, y:139071,x:58272, y:139214,x:57862, y:139734,x:57597, y:140008,x:57128, y:140417,x:56957, y:140527,x:55634, y:141261,x:54739, y:141520,x:54227, y:141509,x:53713, y:141455,x:53357, y:141365,x:52803, y:141195,x:51038, y:140442,x:50912, y:140363,x:49570, y:139701,x:49349, y:139570,x:48711, y:139061,x:48030, y:138607,x:47216, y:137947,x:46636, y:137336,x:45996, y:136606,x:45557, y:136010,x:44759, y:134683,x:44736, y:134456,x:44722, y:133959,x:44860, y:133499,x:45070, y:133110,x:45409, y:132285,x:45472, y:132191,x:46179, y:130768,x:46967, y:129522,x:49160, y:126191,x:51524, y:122876,x:53535, y:119837,x:53907, y:119386,x:54058, y:119275,x:54599, y:119009,x:55996, y:118665,x:56676, y:118353,x:56880, y:118218,x:57077, y:117999,x:57470, y:117636,x:57770, y:117126,x:57869, y:116857,x:57813, y:116384,x:57548, y:115629,x:56990, y:114696,x:56827, y:114500,x:56499, y:113987,x:56446, y:113825,x:56406, y:113546,x:56414, y:113450,x:56502, y:113114,x:56644, y:112809,x:57048, y:111776,x:57361, y:111343,x:57675, y:111294,x:58033, y:111327,x:58381, y:111408,x:58931, y:111415,x:59615, y:111302,x:60205, y:111058,x:60414, y:110924,x:60750, y:110625,x:61308, y:109937,x:61613, y:109479,x:61757, y:109122,x:62054, y:108607,x:63044, y:106591,x:63187, y:106217,x:63383, y:105831,x:63637, y:105202,x:63957, y:104272,x:64099, y:103725,x:64192, y:102816,x:64131, y:101890,x:64073, y:101702,x:64067, y:99841,x:63905, y:98213,x:63858, y:97971,x:63465, y:97669,x:61997, y:97277,x:61566, y:96776,x:61420, y:96390,x:61166, y:95946,x:61046, y:95636,x:60939, y:95437,x:60887, y:95062,x:60881, y:94838,x:60956, y:94121,x:61039, y:93657,x:61127, y:93404,x:61561, y:93007,x:61622, y:92484,x:61181, y:91290,x:61140, y:90833,|";
				TestSinglePathIsInside(polyPath, new IntPoint(79439, 94082), new IntPoint(137615, 112153));

				polyPath = "x:61172, y:90528,x:62778, y:85990,x:62968, y:85599,x:63076, y:85443,x:63246, y:85306,x:63553, y:85151,x:64231, y:85121,x:66020, y:85291,x:67460, y:85459,x:67836, y:85559,x:68041, y:85630,x:68403, y:85863,x:68653, y:86080,x:68948, y:86491,x:69324, y:86951,x:69383, y:86998,x:69501, y:86996,x:69885, y:87064,x:70518, y:86788,x:71152, y:86614,x:71850, y:86545,x:72404, y:86606,x:73380, y:86588,x:73619, y:86532,x:74439, y:86281,x:75172, y:85980,x:75404, y:85824,x:76407, y:85018,x:76768, y:84704,x:76898, y:84615,x:77417, y:84404,x:77494, y:84185,x:77630, y:83988,x:77711, y:83738,x:77799, y:83265,x:77881, y:82299,x:77940, y:82186,x:78651, y:81819,x:79078, y:81505,x:81222, y:79325,x:81321, y:79140,x:81555, y:78849,x:81602, y:78756,x:81599, y:78268,x:81542, y:78042,x:81357, y:77681,x:81065, y:77023,x:81059, y:76835,x:81102, y:76577,x:81235, y:76258,x:81509, y:75879,x:82243, y:75088,x:82965, y:74459,x:83137, y:74140,x:83189, y:73949,x:83176, y:73708,x:83203, y:73161,x:83473, y:72538,x:83955, y:71851,x:84232, y:71540,x:84892, y:70947,x:85420, y:70624,x:85784, y:70436,x:86595, y:70134,x:87224, y:69806,x:87309, y:69741,x:87437, y:69606,x:87662, y:69445,x:87757, y:68797,x:87829, y:68435,x:87949, y:68172,x:88461, y:67520,x:89199, y:66830,x:89898, y:66400,x:90725, y:66274,x:90916, y:66270,x:91169, y:66297,x:91458, y:66190,x:91621, y:66007,x:91818, y:65649,x:92340, y:64903,x:93041, y:64014,x:93365, y:63521,x:93881, y:63064,x:94399, y:63022,x:94510, y:63036,x:95316, y:63307,x:95728, y:63387,x:95955, y:63460,x:96236, y:63384,x:96527, y:62957,x:96708, y:62784,x:97115, y:62605,x:97934, y:62525,x:98357, y:62536,x:98942, y:62582,x:99397, y:62765,x:99784, y:62969,x:100009, y:62939,x:100074, y:62957,x:100237, y:62854,x:100711, y:62608,x:102235, y:62536,x:102559, y:62581,x:102928, y:62697,x:103258, y:62822,x:103360, y:62927,x:103748, y:63402,x:104258, y:63405,x:104601, y:63307,x:105406, y:62957,x:105457, y:62957,x:106009, y:63118,x:106341, y:63323,x:106447, y:63413,x:107238, y:64459,x:108048, y:65648,x:108314, y:65998,x:108487, y:66164,x:108768, y:66273,x:109437, y:66285,x:110087, y:66437,x:110362, y:66589,x:110669, y:66797,x:110926, y:67024,x:111603, y:67681,x:111826, y:67941,x:112053, y:68400,x:112161, y:68741,x:112225, y:69277,x:112283, y:69454,x:112421, y:69528,x:112592, y:69737,x:113195, y:70072,x:113500, y:70182,x:113722, y:70353,x:114439, y:70580,x:114840, y:70884,x:115110, y:71045,x:115253, y:71152,x:115564, y:71430,x:115778, y:71677,x:116270, y:72326,x:116560, y:72843,x:116736, y:73507,x:116765, y:73972,x:116833, y:74230,x:117056, y:74557,x:117840, y:75236,x:118735, y:76270,x:118781, y:76419,x:118901, y:77000,x:118805, y:77219,x:118443, y:77831,x:118377, y:77989,x:118316, y:78242,x:118286, y:78516,x:118408, y:78944,x:118775, y:79440,x:119996, y:80678,x:120445, y:81103,x:120891, y:81560,x:121083, y:81733,x:122009, y:82216,x:122112, y:83383,x:122187, y:83753,x:122402, y:84269,x:122579, y:84380,x:122993, y:84687,x:123369, y:84859,x:124545, y:85909,x:124733, y:86033,x:126114, y:86550,x:127219, y:86642,x:128250, y:86542,x:129111, y:86693,x:129671, y:86973,x:130150, y:87041,x:130598, y:86933,x:130640, y:86907,x:131150, y:86345,x:131387, y:86019,x:131634, y:85791,x:132132, y:85553,x:132297, y:85498,x:133639, y:85318,x:134657, y:85228,x:134881, y:85261,x:136577, y:86094,x:137262, y:86528,x:137432, y:86816,x:138747, y:90551,x:138768, y:90910,x:138647, y:91531,x:138307, y:92477,x:138344, y:92608,x:138367, y:92946,x:138493, y:93142,x:138690, y:93387,x:138791, y:93462,x:138914, y:93801,x:138954, y:94303,x:139018, y:94682,x:139040, y:95150,x:138977, y:95486,x:138808, y:95747,x:138628, y:96256,x:138474, y:96450,x:138225, y:96559,x:138186, y:96607,x:136854, y:96923,x:136306, y:97088,x:136159, y:97183,x:135906, y:97403,x:134979, y:97500,x:134574, y:97578,x:134382, y:97631,x:134139, y:97773,x:134010, y:97908,x:133857, y:98245,x:133791, y:99062,x:134049, y:99550,x:134206, y:99940,x:133954, y:101861,x:133966, y:103594,x:133984, y:103811,x:134115, y:104417,x:134549, y:105558,x:134617, y:105906,x:134768, y:106501,x:135563, y:107708,x:135783, y:108270,x:135856, y:108512,x:135994, y:108810,x:136417, y:109588,x:137257, y:110596,x:137886, y:111179,x:138335, y:111379,x:139013, y:111418,x:140483, y:111335,x:141463, y:111302,x:141662, y:111243,x:142496, y:111141,x:142614, y:111247,x:142678, y:111274,x:143343, y:113009,x:143470, y:113277,x:143509, y:113603,x:143460, y:113868,x:143343, y:114133,x:142931, y:114659,x:142588, y:115253,x:142501, y:115351,x:142071, y:116456,x:142116, y:116760,x:142221, y:117186,x:142391, y:117563,x:142594, y:117871,x:143020, y:118215,x:143264, y:118359,x:144191, y:118640,x:144474, y:118703,x:145356, y:118950,x:145908, y:119267,x:146335, y:119771,x:146617, y:120220,x:148086, y:122437,x:150674, y:126078,x:151128, y:126742,x:153389, y:130223,x:154078, y:131425,x:154429, y:132216,x:154702, y:132630,x:155026, y:133459,x:155065, y:133525,x:155116, y:133843,x:155125, y:134227,x:155058, y:134832,x:154926, y:135155,x:154332, y:136263,x:153749, y:136967,x:152672, y:138172,x:151950, y:138710,x:150178, y:139750,x:149347, y:140174,x:149123, y:140335,x:148398, y:140733,x:147561, y:141015,x:146687, y:141214,x:146395, y:141264,x:145233, y:141353,x:145082, y:141339,x:144523, y:141212,x:143236, y:140515,x:142861, y:140221,x:142372, y:139795,x:141838, y:139192,x:141643, y:138944,x:141270, y:138288,x:141039, y:137813,x:140741, y:136855,x:140509, y:135483,x:139559, y:124715,x:139394, y:123473,x:139004, y:121469,x:138556, y:119689,x:138412, y:119261,x:137506, y:116084,x:136991, y:113779,x:136597, y:112202,x:135934, y:110068,x:135882, y:109704,x:135809, y:109444,x:135314, y:108297,x:135185, y:107881,x:135175, y:107920,x:134738, y:106818,x:134194, y:105803,x:133803, y:105239,x:133078, y:104359,x:132430, y:103690,x:132083, y:103384,x:130639, y:102277,x:129880, y:101758,x:127928, y:100362,x:126273, y:99039,x:124690, y:97625,x:123187, y:96341,x:121620, y:95091,x:120595, y:94413,x:119777, y:93947,x:118355, y:93298,x:117177, y:92856,x:115345, y:92337,x:114794, y:92199,x:113381, y:91924,x:112246, y:91784,x:111457, y:91761,x:109761, y:91808,x:107025, y:91826,x:105996, y:91849,x:97712, y:91831,x:95635, y:91850,x:93106, y:91839,x:92571, y:91820,x:88111, y:91804,x:87294, y:91847,x:86375, y:91953,x:85166, y:92176,x:84481, y:92337,x:82783, y:92835,x:81704, y:93225,x:80449, y:93783,x:79540, y:94255,x:78471, y:94954,x:77030, y:96046,x:75027, y:97754,x:73731, y:98920,x:72955, y:99562,x:71882, y:100402,x:68847, y:102567,x:67767, y:103427,x:66861, y:104331,x:66213, y:105141,x:65548, y:106256,x:65120, y:107133,x:64587, y:108352,x:64265, y:109189,x:63764, y:110642,x:63324, y:112049,x:63054, y:113029,x:62390, y:115971,x:61824, y:118015,x:61376, y:119473,x:61202, y:120088,x:60810, y:121743,x:60661, y:122481,x:60440, y:123797,x:60291, y:124835,x:59717, y:131789,x:59335, y:135826,x:59083, y:137118,x:58861, y:137903,x:58786, y:138115,x:58353, y:139071,x:58272, y:139214,x:57862, y:139734,x:57597, y:140008,x:57128, y:140417,x:56957, y:140527,x:55634, y:141261,x:54739, y:141520,x:54227, y:141509,x:53713, y:141455,x:53357, y:141365,x:52803, y:141195,x:51038, y:140442,x:50912, y:140363,x:49570, y:139701,x:49349, y:139570,x:48711, y:139061,x:48030, y:138607,x:47216, y:137947,x:46636, y:137336,x:45996, y:136606,x:45557, y:136010,x:44759, y:134683,x:44736, y:134456,x:44722, y:133959,x:44860, y:133499,x:45070, y:133110,x:45409, y:132285,x:45472, y:132191,x:46179, y:130768,x:46967, y:129522,x:49160, y:126191,x:51524, y:122876,x:53535, y:119837,x:53907, y:119386,x:54058, y:119275,x:54599, y:119009,x:55996, y:118665,x:56676, y:118353,x:56880, y:118218,x:57077, y:117999,x:57470, y:117636,x:57770, y:117126,x:57869, y:116857,x:57813, y:116384,x:57548, y:115629,x:56990, y:114696,x:56827, y:114500,x:56499, y:113987,x:56446, y:113825,x:56406, y:113546,x:56414, y:113450,x:56502, y:113114,x:56644, y:112809,x:57048, y:111776,x:57361, y:111343,x:57675, y:111294,x:58033, y:111327,x:58381, y:111408,x:58931, y:111415,x:59615, y:111302,x:60205, y:111058,x:60414, y:110924,x:60750, y:110625,x:61308, y:109937,x:61613, y:109479,x:61757, y:109122,x:62054, y:108607,x:63044, y:106591,x:63187, y:106217,x:63383, y:105831,x:63637, y:105202,x:63957, y:104272,x:64099, y:103725,x:64192, y:102816,x:64131, y:101890,x:64073, y:101702,x:64067, y:99841,x:63905, y:98213,x:63858, y:97971,x:63465, y:97669,x:61997, y:97277,x:61566, y:96776,x:61420, y:96390,x:61166, y:95946,x:61046, y:95636,x:60939, y:95437,x:60887, y:95062,x:60881, y:94838,x:60956, y:94121,x:61039, y:93657,x:61127, y:93404,x:61561, y:93007,x:61622, y:92484,x:61181, y:91290,x:61140, y:90833,|";
				TestSinglePathIsInside(polyPath, new IntPoint(145465, 139640), new IntPoint(133698, 104384));

				polyPath = "x:93366, y:91331, z:1350, width:0,x:94003, y:91156, z:1350, width:0,x:94320, y:91122, z:1350, width:0,x:94384, y:91084, z:1350, width:0,x:94489, y:91062, z:1350, width:0,x:94542, y:90990, z:1350, width:0,x:94707, y:90558, z:1350, width:0,x:94787, y:90446, z:1350, width:0,x:94968, y:90314, z:1350, width:0,x:95097, y:90256, z:1350, width:0,x:95329, y:90187, z:1350, width:0,x:96538, y:89988, z:1350, width:0,x:99450, y:89738, z:1350, width:0,x:100275, y:89717, z:1350, width:0,x:103355, y:89975, z:1350, width:0,x:103882, y:90070, z:1350, width:0,x:104540, y:90165, z:1350, width:0,x:104978, y:90298, z:1350, width:0,x:105145, y:90412, z:1350, width:0,x:105249, y:90557, z:1350, width:0,x:105413, y:91048, z:1350, width:0,x:105591, y:91129, z:1350, width:0,x:106259, y:91240, z:1350, width:0,x:106536, y:91320, z:1350, width:0,x:107259, y:91620, z:1350, width:0,x:107314, y:91678, z:1350, width:0,x:107414, y:91999, z:1350, width:0,x:107520, y:92064, z:1350, width:0,x:107924, y:91976, z:1350, width:0,x:108175, y:92140, z:1350, width:0,x:108302, y:92261, z:1350, width:0,x:108432, y:92333, z:1350, width:0,x:108565, y:92347, z:1350, width:0,x:108855, y:92277, z:1350, width:0,x:111216, y:91019, z:1350, width:0,x:111433, y:90954, z:1350, width:0,x:111544, y:90943, z:1350, width:0,x:111692, y:90967, z:1350, width:0,x:111731, y:90989, z:1350, width:0,x:111864, y:91012, z:1350, width:0,x:112148, y:91161, z:1350, width:0,x:112317, y:91366, z:1350, width:0,x:113139, y:93293, z:1350, width:0,x:113242, y:93641, z:1350, width:0,x:113218, y:94175, z:1350, width:0,x:113284, y:94469, z:1350, width:0,x:113269, y:94631, z:1350, width:0,x:113149, y:95107, z:1350, width:0,x:113181, y:95420, z:1350, width:0,x:113336, y:95829, z:1350, width:0,x:114178, y:97187, z:1350, width:0,x:114250, y:97506, z:1350, width:0,x:114327, y:97652, z:1350, width:0,x:114504, y:97690, z:1350, width:0,x:114590, y:97687, z:1350, width:0,x:114913, y:97608, z:1350, width:0,x:115235, y:97615, z:1350, width:0,x:115407, y:97740, z:1350, width:0,x:115734, y:98261, z:1350, width:0,x:117175, y:100909, z:1350, width:0,x:117389, y:101565, z:1350, width:0,x:117492, y:101708, z:1350, width:0,x:117735, y:101795, z:1350, width:0,x:118474, y:101848, z:1350, width:0,x:119040, y:101973, z:1350, width:0,x:119767, y:102190, z:1350, width:0,x:121240, y:102679, z:1350, width:0,x:121550, y:102847, z:1350, width:0,x:121677, y:102974, z:1350, width:0,x:121747, y:103167, z:1350, width:0,x:121703, y:103336, z:1350, width:0,x:121538, y:103620, z:1350, width:0,x:121023, y:104343, z:1350, width:0,x:120480, y:105152, z:1350, width:0,x:119364, y:106900, z:1350, width:0,x:119150, y:107204, z:1350, width:0,x:118971, y:107342, z:1350, width:0,x:118749, y:107405, z:1350, width:0,x:118356, y:107356, z:1350, width:0,x:117877, y:107203, z:1350, width:0,x:117462, y:107011, z:1350, width:0,x:117159, y:106812, z:1350, width:0,x:116723, y:106446, z:1350, width:0,x:113947, y:103525, z:1350, width:0,x:111963, y:101490, z:1350, width:0,x:107755, y:97062, z:1350, width:0,x:106991, y:96348, z:1350, width:0,x:106533, y:96005, z:1350, width:0,x:106199, y:95811, z:1350, width:0,x:105857, y:95643, z:1350, width:0,x:104952, y:95307, z:1350, width:0,x:103293, y:94785, z:1350, width:0,x:102578, y:94619, z:1350, width:0,x:102018, y:94542, z:1350, width:0,x:101363, y:94511, z:1350, width:0,x:100138, y:94548, z:1350, width:0,x:98594, y:94509, z:1350, width:0,x:97838, y:94548, z:1350, width:0,x:97308, y:94626, z:1350, width:0,x:96678, y:94785, z:1350, width:0,x:95086, y:95275, z:1350, width:0,x:94126, y:95627, z:1350, width:0,x:93576, y:95898, z:1350, width:0,x:93048, y:96265, z:1350, width:0,x:92717, y:96547, z:1350, width:0,x:92208, y:97036, z:1350, width:0,x:88729, y:100697, z:1350, width:0,x:85212, y:104334, z:1350, width:0,x:83286, y:106392, z:1350, width:0,x:82854, y:106776, z:1350, width:0,x:82611, y:106936, z:1350, width:0,x:82111, y:107187, z:1350, width:0,x:81732, y:107315, z:1350, width:0,x:81373, y:107390, z:1350, width:0,x:81077, y:107379, z:1350, width:0,x:80895, y:107305, z:1350, width:0,x:80817, y:107226, z:1350, width:0,x:78888, y:104270, z:1350, width:0,x:78388, y:103583, z:1350, width:0,x:78283, y:103409, z:1350, width:0,x:78280, y:103315, z:1350, width:0,x:78320, y:103157, z:1350, width:0,x:78444, y:102927, z:1350, width:0,x:78530, y:102823, z:1350, width:0,x:78655, y:102711, z:1350, width:0,x:79692, y:102348, z:1350, width:0,x:81250, y:101888, z:1350, width:0,x:81706, y:101826, z:1350, width:0,x:82155, y:101827, z:1350, width:0,x:82413, y:101776, z:1350, width:0,x:82555, y:101611, z:1350, width:0,x:82751, y:100971, z:1350, width:0,x:83932, y:98791, z:1350, width:0,x:84226, y:98308, z:1350, width:0,x:84457, y:98006, z:1350, width:0,x:84806, y:97775, z:1350, width:0,x:85148, y:97796, z:1350, width:0,x:85338, y:97826, z:1350, width:0,x:85814, y:97977, z:1350, width:0,x:86003, y:97969, z:1350, width:0,x:86053, y:97849, z:1350, width:0,x:85935, y:97262, z:1350, width:0,x:86045, y:96806, z:1350, width:0,x:86556, y:95953, z:1350, width:0,x:86767, y:95521, z:1350, width:0,x:86840, y:95172, z:1350, width:0,x:86838, y:95075, z:1350, width:0,x:86750, y:94693, z:1350, width:0,x:86690, y:94546, z:1350, width:0,x:86760, y:94205, z:1350, width:0,x:86708, y:93668, z:1350, width:0,x:86806, y:93340, z:1350, width:0,x:87675, y:91291, z:1350, width:0,x:87787, y:91192, z:1350, width:0,x:88352, y:90940, z:1350, width:0,x:88440, y:90947, z:1350, width:0,x:88536, y:90933, z:1350, width:0,x:88720, y:91012, z:1350, width:0,x:89638, y:91495, z:1350, width:0,x:90901, y:92198, z:1350, width:0,x:91173, y:92312, z:1350, width:0,x:91441, y:92366, z:1350, width:0,x:91625, y:92293, z:1350, width:0,x:91766, y:92160, z:1350, width:0,x:92060, y:91953, z:1350, width:0,x:92464, y:92086, z:1350, width:0,x:92523, y:92072, z:1350, width:0,x:92663, y:91639, z:1350, width:0,|";
				// Length of this segment (start->end) 15257.
				// startOverride = new MSIntPoint(119160, 104727); endOverride = new MSIntPoint(111711, 91412);
				TestSinglePathIsInside(polyPath, new IntPoint(119160, 104727), new IntPoint(111711, 91412));
			}

			// optomized small shapes
			{
				string polyPath = "x:85032, y:92906,x:83438, y:87666,x:88142, y:90718,|x:85436, y:90297,x:85244, y:90445,x:85925, y:91424,|";
				// Length of this segment (start->end) 1847.
				// startOverride = new MSIntPoint(84590, 89182); endOverride = new MSIntPoint(85070, 90966);
				TestSinglePathIsInside(polyPath, new IntPoint(84590, 89182), new IntPoint(85070, 90966));

				polyPath = "x:99832, y:88697,x:88119, y:101091,x:82212, y:86495,|x:86663, y:99419,x:86538, y:90868,x:86525, y:90775,x:86307, y:90633,|";
				// Length of this segment (start->end) 4789.
				// startOverride = new MSIntPoint(83319, 87278); endOverride = new MSIntPoint(86711, 90660);
				//TestSinglePathIsInside(polyPath, new IntPoint(83319, 87278), new IntPoint(86711, 90660));

				polyPath = "x:219655, y:7130,x:212349, y:44250,x:210115, y:46125,x:207012, y:47846,x:211866, y:55536,x:249231, y:52809,x:176266, y:113595,|";
				// Length of this segment (start->end) 32286.
				// startOverride = new MSIntPoint(205084, 78424); endOverride = new MSIntPoint(213725, 47315);
				TestSinglePathIsInside(polyPath, new IntPoint(205084, 78424), new IntPoint(213725, 47315));
			}
		}

		[Test]
		public void CoinHasCrossingsWhenPathingTurnedOff()
		{
			// By default we need to do the inner perimeters first
			string coinStlFile = TestUtlities.GetStlPath("MatterControl - Coin");
			string coinGCodeFile = TestUtlities.GetTempGCodePath("MatterControl - Coin.gcode");

			bool calledPathingCode = false;
			ConfigSettings config = new ConfigSettings();
			config.NumberOfPerimeters = 3;
			config.FirstLayerExtrusionWidth = config.ExtrusionWidth;
			config.AvoidCrossingPerimeters = false;
			PathFinder.CalculatedPath += (pathFinder, pathThatIsInside, startPoint, endPoint) =>
			{
				calledPathingCode = true;
			};
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(coinGCodeFile);
			processor.LoadStlFile(coinStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			// with avoid off we should find a bad path
			Assert.IsFalse(calledPathingCode);
		}

		[Test]
		public void CoinHasNoCrossingsWhenPathingTurnedOn()
		{
			// By default we need to do the inner perimeters first
			string coinStlFile = TestUtlities.GetStlPath("MatterControl - Coin");
			string coinGCodeFile = TestUtlities.GetTempGCodePath("MatterControl - Coin.gcode");

			bool foundBadPath = false;
			ConfigSettings config = new ConfigSettings();
			config.NumberOfPerimeters = 3;
			// It would be nice if we made good paths without setting this
			config.FirstLayerExtrusionWidth = config.ExtrusionWidth;
			config.AvoidCrossingPerimeters = true;
			PathFinder.CalculatedPath += (pathFinder, pathThatIsInside, startPoint, endPoint) =>
			{
				if (!pathFinder.AllPathSegmentsAreInsideOutlines(pathThatIsInside, startPoint, endPoint))
				{
					foundBadPath = true;
				}
			};
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(coinGCodeFile);
			processor.LoadStlFile(coinStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			// with avoid off we should find a bad path
			Assert.IsFalse(foundBadPath);
		}

		private void TestSinglePathIsInside(string partOutlineString, IntPoint startPoint, IntPoint endPoint)
		{
			Polygons boundaryPolygons = CLPolygonsExtensions.CreateFromString(partOutlineString);
			PathFinder testHarness = new PathFinder(boundaryPolygons, 600);

			Polygon insidePath = new Polygon();
			// not optimized
			Assert.IsTrue(testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, false));
			Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));

			// and optimized
			Assert.IsTrue(testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath, true));
			Assert.IsTrue(testHarness.AllPathSegmentsAreInsideOutlines(insidePath, startPoint, endPoint));
		}
	}
}