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

using System;
using System.Collections.Generic;
using MSClipperLib;
using NUnit.Framework;
using MatterHackers.QuadTree;

namespace MatterHackers.MatterSlice.Tests
{
	[TestFixture, Category("MatterSlice.PathOrderTests")]
	public class PathOrderTests
	{
		[Test]
		public void CorrectPolygonOrder()
		{
			//                  |
			//     2       1    |    1       2
			//                  |
			//                  |
			//     3       0    |    0       3
			// _________________|__________________
			//                  |
			//     3       0    |    0       3
			//                  |
			//                  |
			//     2       1    |    1       2
			//                  |

			string polyQ1String = "x:5, y:5,x:5, y:10,x:10, y:10,x:10, y:5,|";
			var polyQ1 = CLPolygonsExtensions.CreateFromString(polyQ1String)[0];

			string polyQ2String = "x:-5, y:5,x:-5, y:10,x:-10, y:10,x:-10, y:5,|";
			var polyQ2 = CLPolygonsExtensions.CreateFromString(polyQ2String)[0];

			string polyQ3String = "x:-5, y:-5,x:-5, y:-10,x:-10, y:-10,x:-10, y:-5,|";
			var polyQ3 = CLPolygonsExtensions.CreateFromString(polyQ3String)[0];

			string polyQ4String = "x:5, y:-5,x:5, y:-10,x:10, y:-10,x:10, y:-5,|";
			var polyQ4 = CLPolygonsExtensions.CreateFromString(polyQ4String)[0];

			var settings = new ConfigSettings();

			// test simple finding
			{
				var pPathOrderOptimizer = new PathOrderOptimizer(settings);
				pPathOrderOptimizer.AddPolygon(polyQ1);
				pPathOrderOptimizer.AddPolygon(polyQ2);

				// starting at low far right
				pPathOrderOptimizer.Optimize(new IntPoint(20, 0), null, 0, false, null);
				Assert.AreEqual(2, pPathOrderOptimizer.Order.Count);
				Assert.AreEqual(0, pPathOrderOptimizer.Order[0].PolyIndex);
				Assert.AreEqual(3, pPathOrderOptimizer.Order[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[1].PolyIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.Order[1].PointIndex);

				// starting at high far right
				pPathOrderOptimizer.Optimize(new IntPoint(20, 20), null, 0, false, null);
				Assert.AreEqual(2, pPathOrderOptimizer.Order.Count);
				Assert.AreEqual(0, pPathOrderOptimizer.Order[0].PolyIndex);
				Assert.AreEqual(2, pPathOrderOptimizer.Order[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[1].PolyIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[1].PointIndex);

				// starting at high far left
				pPathOrderOptimizer.Optimize(new IntPoint(-20, 20), null, 0, false, null);
				Assert.AreEqual(2, pPathOrderOptimizer.Order.Count);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[0].PolyIndex);
				Assert.AreEqual(2, pPathOrderOptimizer.Order[0].PointIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.Order[1].PolyIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[1].PointIndex);
			}

			// test that single lines connect correctly
			{
				var pPathOrderOptimizer = new PathOrderOptimizer(settings);
				pPathOrderOptimizer.AddPolygons(
					CLPolygonsExtensions.CreateFromString(
						"x:0, y:0,x:500, y:0,|x:0, y:100,x:500, y:100,|x:0, y:200,x:500, y:200,|x:0, y:300,x:500, y:300,|"));

				// starting at low far right
				pPathOrderOptimizer.Optimize(new IntPoint(0, 0), null, 0, false, null);
				Assert.AreEqual(4, pPathOrderOptimizer.Order.Count);
				Assert.AreEqual(0, pPathOrderOptimizer.Order[0].PolyIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[1].PolyIndex);
				Assert.AreEqual(2, pPathOrderOptimizer.Order[2].PolyIndex);
				Assert.AreEqual(3, pPathOrderOptimizer.Order[3].PolyIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.Order[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[1].PointIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.Order[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.Order[1].PointIndex);
			}
		}

		[Test]
		public void CorrectSeamPlacement()
		{
			// coincident points return 0 angle
			{
				IntPoint p1 = new IntPoint(10, 0);
				IntPoint p2 = new IntPoint(0, 0);
				IntPoint p3 = new IntPoint(0, 0);
				Assert.IsTrue(p2.GetTurnAmount(p1, p3) == 0);
			}

			// no turn returns a 0 angle
			{
				IntPoint p1 = new IntPoint(10, 0);
				IntPoint p2 = new IntPoint(0, 0);
				IntPoint p3 = new IntPoint(-10, 0);
				Assert.IsTrue(p2.GetTurnAmount(p1, p3) == 0);
			}

			// 90 turn works
			{
				IntPoint p1 = new IntPoint(0, 0);
				IntPoint p2 = new IntPoint(10, 0);
				IntPoint p3 = new IntPoint(10, 10);
				Assert.AreEqual(p2.GetTurnAmount(p1, p3), Math.PI / 2, .001);

				IntPoint p4 = new IntPoint(0, 10);
				IntPoint p5 = new IntPoint(0, 0);
				IntPoint p6 = new IntPoint(10, 0);
				Assert.AreEqual(p5.GetTurnAmount(p4, p6), Math.PI / 2, .001);
			}

			// -90 turn works
			{
				IntPoint p1 = new IntPoint(0, 0);
				IntPoint p2 = new IntPoint(10, 0);
				IntPoint p3 = new IntPoint(10, -10);
				Assert.AreEqual(p2.GetTurnAmount(p1, p3), -Math.PI / 2, .001);
			}

			// 45 turn works
			{
				IntPoint p1 = new IntPoint(0, 0);
				IntPoint p2 = new IntPoint(10, 0);
				IntPoint p3 = new IntPoint(15, 5);
				Assert.AreEqual(Math.PI / 4, p2.GetTurnAmount(p1, p3), .001);

				IntPoint p4 = new IntPoint(0, 0);
				IntPoint p5 = new IntPoint(-10, 0);
				IntPoint p6 = new IntPoint(-15, -5);
				Assert.AreEqual(Math.PI / 4, p5.GetTurnAmount(p4, p6), .001);
			}

			// -45 turn works
			{
				IntPoint p1 = new IntPoint(0, 0);
				IntPoint p2 = new IntPoint(10, 0);
				IntPoint p3 = new IntPoint(15, -5);
				Assert.AreEqual(-Math.PI / 4, p2.GetTurnAmount(p1, p3), .001);
			}

			// find the right point wound ccw
			{
				// 4________3
				// |       /
				// |      /2
				// |      \
				// |0______\1
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(70, 50), new IntPoint(100, 100), new IntPoint(0, 100) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 2);
			}

			// find the right point wound ccw
			{
				// 3________2
				// |       |
				// |       |
				// |       |
				// |0______|1
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(100, 100), new IntPoint(0, 100) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 3);
			}

			// find the right point wound ccw
			{
				// 1________0
				// |       |
				// |       |
				// |       |
				// |2______|3
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 1);
			}

			// find the right point wound cw
			{
				// 1________2
				// |       |
				// |       |
				// |       |
				// |0______|3
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0); // this is an inside perimeter so we place the seem to the front
			}

			// find the right point wound cw
			{
				// 0________1
				// |       |
				// |       |
				// |       |
				// |3______|2
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(100, 0), new IntPoint(0, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 3);
			}

			// find the right point wound ccw
			{
				// 4________3
				// |       /
				// |      /2
				// |      \
				// |0______\1
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(1000, 0), new IntPoint(900, 500), new IntPoint(1000, 1000), new IntPoint(0, 1000) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 2 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 4);
			}

			// ccw shallow
			{
				// 2________1
				// |       /
				// |      /0
				// |      \
				// |3______\4
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(90, 50), new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 0 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 2);
			}

			// ccw
			{
				// 2________1
				// |       /
				// |      /0
				// |      \
				// |3______\4
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(90, 50), new IntPoint(200, 100), new IntPoint(0, 100), new IntPoint(0, 0), new IntPoint(200, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0
				//   /    \
				//  /4_____\5
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(90, 50), new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(10, 50), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 3 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 2);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0
				//   /    \
				//  /4_____\5
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(55, 50), new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(45, 50), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 3);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0 less angle
				//   /    \
				//  /4_____\5
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(950, 500), new IntPoint(1000, 1000), new IntPoint(0, 1000), new IntPoint(100, 500), new IntPoint(0, 0), new IntPoint(1000, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 2 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 2);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0 more angle
				//   /    \
				//  /4_____\5
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(550, 500), new IntPoint(1000, 1000), new IntPoint(0, 1000), new IntPoint(100, 500), new IntPoint(0, 0), new IntPoint(1000, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 5________4
				//  \      /
				//   \0   /3
				//   /    \
				//  /1_____\2
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(10, 50), new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(90, 50), new IntPoint(100, 100), new IntPoint(0, 100), };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 0 is too shallow
				Assert.IsTrue(bestPoint == 5);
			}

			// ccw
			{
				// 5________4
				//  \      /
				//   \0   /3
				//   /    \
				//  /1_____\2
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(45, 50), new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(55, 50), new IntPoint(100, 100), new IntPoint(0, 100), };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// find the right point wound cw (inside hole loops)
			{
				// 1________2
				// |       /
				// |      /3
				// |      \
				// |0______\4
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(90, 50), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0); // everything over 90 degrees is treated the same so the front left is the best
			}

			// find the right point wound cw
			{
				// 2________3
				// |       /
				// |      /4
				// |      \
				// |1______\0
				List<IntPoint> testPoints = new List<IntPoint> { new IntPoint(100, 0), new IntPoint(0, 0), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(90, 50) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// cw
			{
				// 4________5
				//  \      /
				//   \3   /0
				//   /    \
				//  /2_____\1
				List<IntPoint> testPoints = new List<IntPoint>
				{
					new IntPoint(90, 50), new IntPoint(100, 0), new IntPoint(0, 0), new IntPoint(10, 50), new IntPoint(0, 100), new IntPoint(100, 100)
				};
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 2);
			}

			// cw
			{
				// 1________2
				//  \      /
				//   \0   /3
				//   /    \
				//  /5_____\4
				List<IntPoint> testPoints = new List<IntPoint>
				{
					new IntPoint(10, 50), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(90, 50), new IntPoint(100, 0), new IntPoint(0, 0),
				};
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 5);
			}
		}
	}
}