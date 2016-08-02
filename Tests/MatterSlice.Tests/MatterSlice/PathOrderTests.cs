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
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterSlice.Tests
{
	using static PathOrderOptimizer;
	using Polygon = List<MSClipperLib.IntPoint>;
	using Polygons = List<List<MSClipperLib.IntPoint>>;

	[TestFixture, Category("MatterSlice.PathOrderTests")]
	public class PathOrderTests
	{
		[Test]
		public void CorrectSeamPlacement()
		{
			// coincident points return 0 angle
			{
				MSClipperLib.IntPoint p1 = new MSClipperLib.IntPoint(10, 0);
				MSClipperLib.IntPoint p2 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p3 = new MSClipperLib.IntPoint(0, 0);
				Assert.IsTrue(PathOrderOptimizer.GetTurnAmount(p1, p2, p3) == 0);
			}

			// no turn returns a 0 angle
			{
				MSClipperLib.IntPoint p1 = new MSClipperLib.IntPoint(10, 0);
				MSClipperLib.IntPoint p2 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p3 = new MSClipperLib.IntPoint(-10, 0);
				Assert.IsTrue(PathOrderOptimizer.GetTurnAmount(p1, p2, p3) == 0);
			}

			// 90 turn works
			{
				MSClipperLib.IntPoint p1 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p2 = new MSClipperLib.IntPoint(10, 0);
				MSClipperLib.IntPoint p3 = new MSClipperLib.IntPoint(10, 10);
				Assert.AreEqual(PathOrderOptimizer.GetTurnAmount(p1, p2, p3), Math.PI / 2, .001);

				MSClipperLib.IntPoint p4 = new MSClipperLib.IntPoint(0, 10);
				MSClipperLib.IntPoint p5 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p6 = new MSClipperLib.IntPoint(10, 0);
				Assert.AreEqual(PathOrderOptimizer.GetTurnAmount(p4, p5, p6), Math.PI / 2, .001);
			}

			// -90 turn works
			{
				MSClipperLib.IntPoint p1 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p2 = new MSClipperLib.IntPoint(10, 0);
				MSClipperLib.IntPoint p3 = new MSClipperLib.IntPoint(10, -10);
				Assert.AreEqual(PathOrderOptimizer.GetTurnAmount(p1, p2, p3), -Math.PI / 2, .001);
			}

			// 45 turn works
			{
				MSClipperLib.IntPoint p1 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p2 = new MSClipperLib.IntPoint(10, 0);
				MSClipperLib.IntPoint p3 = new MSClipperLib.IntPoint(15, 5);
				Assert.AreEqual(Math.PI / 4, PathOrderOptimizer.GetTurnAmount(p1, p2, p3), .001);

				MSClipperLib.IntPoint p4 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p5 = new MSClipperLib.IntPoint(-10, 0);
				MSClipperLib.IntPoint p6 = new MSClipperLib.IntPoint(-15, -5);
				Assert.AreEqual(Math.PI / 4, PathOrderOptimizer.GetTurnAmount(p4, p5, p6), .001);
			}

			// -45 turn works
			{
				MSClipperLib.IntPoint p1 = new MSClipperLib.IntPoint(0, 0);
				MSClipperLib.IntPoint p2 = new MSClipperLib.IntPoint(10, 0);
				MSClipperLib.IntPoint p3 = new MSClipperLib.IntPoint(15, -5);
				Assert.AreEqual(-Math.PI / 4, PathOrderOptimizer.GetTurnAmount(p1, p2, p3), .001);
			}

			// find the right point wound ccw
			{
				// 4________3
				// |       /
				// |      /2
				// |      \
				// |0______\1
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(100, 0), new MSClipperLib.IntPoint(70, 50), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(0, 100) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 2);
			}

			// find the right point wound ccw
			{
				// 3________2
				// |       |
				// |       |
				// |       |
				// |0______|1
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(100, 0), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(0, 100) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 3);
			}

			// find the right point wound ccw
			{
				// 1________0
				// |       |
				// |       |
				// |       |
				// |2______|3
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(100, 0) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 1);
			}

			// find the right point wound cw
			{
				// 1________2
				// |       |
				// |       |
				// |       |
				// |0______|3
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(100, 0) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 1);
			}

            // find the right point wound cw
            {
				// 0________1
				// |       |
				// |       |
				// |       |
				// |3______|2
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(100, 0), new MSClipperLib.IntPoint(0, 0) };
                int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
                Assert.IsTrue(bestPoint == 0);
            }

            // find the right point wound ccw
            {
				// 4________3
				// |       /
				// |      /2
				// |      \
				// |0______\1
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(1000, 0), new MSClipperLib.IntPoint(900, 500), new MSClipperLib.IntPoint(1000, 1000), new MSClipperLib.IntPoint(0, 1000) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 2);
			}

            // ccw
            {
				// 2________1
				// |       /
				// |      /0
				// |      \
				// |3______\4
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(90, 50), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(100, 0) };
                int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
                Assert.IsTrue(bestPoint == 0);
            }

            // ccw
            {
				// 2________1
				//  \      /
				//   \3   /0
				//   /    \
				//  /4_____\5
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(90, 50), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(10, 50), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(100, 0) };
                int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
                Assert.IsTrue(bestPoint == 3);
            }

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0 less angle
				//   /    \
				//  /4_____\5
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(950, 500), new MSClipperLib.IntPoint(1000, 1000), new MSClipperLib.IntPoint(0, 1000), new MSClipperLib.IntPoint(100, 500), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(1000, 0) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 3);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0 more angle
				//   /    \
				//  /4_____\5
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(550, 500), new MSClipperLib.IntPoint(1000, 1000), new MSClipperLib.IntPoint(0, 1000), new MSClipperLib.IntPoint(100, 500), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(1000, 0) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 5________4
				//  \      /
				//   \0   /3
				//   /    \
				//  /1_____\2
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(10, 50), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(100, 0), new MSClipperLib.IntPoint(90, 50), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(0, 100), };
                int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
                Assert.IsTrue(bestPoint == 0);
            }

            // find the right point wound cw (inside hole loops)
            {
				// 1________2
				// |       /
				// |      /3
				// |      \
				// |0______\4
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(90, 50), new MSClipperLib.IntPoint(100, 0) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 1);
			}

            // find the right point wound cw
            {
				// 2________3
				// |       /
				// |      /4
				// |      \
				// |1______\0
				Polygon testPoints = new Polygon { new MSClipperLib.IntPoint(100, 0), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(90, 50) };
				int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
				Assert.IsTrue(bestPoint == 2);
			}

            // cw
            {
				// 4________5
				//  \      /
				//   \3   /0
				//   /    \
				//  /2_____\1
				Polygon testPoints = new Polygon
				{
                    new MSClipperLib.IntPoint(90, 50), new MSClipperLib.IntPoint(100, 0), new MSClipperLib.IntPoint(0, 0), new MSClipperLib.IntPoint(10, 50), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(100, 100)
                };
                int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
                Assert.IsTrue(bestPoint == 4);
            }

            // cw
            {
				// 1________2
				//  \      /
				//   \0   /3
				//   /    \
				//  /5_____\4
				Polygon testPoints = new Polygon
				{
                    new MSClipperLib.IntPoint(10, 50), new MSClipperLib.IntPoint(0, 100), new MSClipperLib.IntPoint(100, 100), new MSClipperLib.IntPoint(90, 50), new MSClipperLib.IntPoint(100, 0), new MSClipperLib.IntPoint(0, 0),
                };
                int bestPoint = PathOrderOptimizer.GetBestIndex(testPoints);
                Assert.IsTrue(bestPoint == 1);
            }
        }
    }
}