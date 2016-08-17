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
				AvoidCrossingPerimeters testHarness = new AvoidCrossingPerimeters(boundaryPolygons);
				Assert.IsTrue(testHarness.PointIsInsideBoundary(new IntPoint(1, 1)));
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
				AvoidCrossingPerimeters testHarness = new AvoidCrossingPerimeters(boundaryPolygons);

				{
					IntPoint startPointInside = startPoint;
					testHarness.MovePointInsideBoundary(ref startPointInside);
					IntPoint endPointInside = endPoint;
					testHarness.MovePointInsideBoundary(ref endPointInside);

					Assert.IsTrue(testHarness.PointIsInsideBoundary(startPointInside));
					Assert.IsTrue(testHarness.PointIsInsideBoundary(endPointInside));

					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPointInside, endPointInside, insidePath);
					Assert.IsTrue(insidePath.Count > 10); // It needs to go around the cicle so it needs many points (2 is a definate fail).
				}

				{
					Polygon insidePath = new Polygon();
					testHarness.CreatePathInsideBoundary(startPoint, endPoint, insidePath);
					Assert.IsTrue(insidePath.Count > 12); // two more than the last test to get the points in the right place
				}
			}
		}
	}
}