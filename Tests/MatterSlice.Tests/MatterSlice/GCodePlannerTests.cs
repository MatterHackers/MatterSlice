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

using MatterSlice.ClipperLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterSlice.Tests
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	[TestFixture, Category("MatterSlice.GCodePlannerTests")]
	public class GCodePlannerTests
	{
		[Test]
		public void GetPathsWithOverlapsRemovedTests()
		{
			// Make sure we don't do anything to a simple perimeter.
			{
				// ____________
				// |          |
				// |          |
				// |          |
				// |__________|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				Polygon perimeter = new Polygon() { new IntPoint(0, 0), new IntPoint(5000, 0), new IntPoint(5000, 5000), new IntPoint(0, 5000) };
				Polygons correctedPath = planner.GetPathsWithOverlapsRemoved(perimeter, 400/4);
				Assert.IsTrue(correctedPath.Count == 1);
				Assert.IsTrue(correctedPath[0].Count == 4);
			}

			// Simple overlap
			{
				// _____    _____                       _____    _____
				// |   |    |   |					    |   |    |   |
				// |   |____|   |					    |   |    |   |
				// |    ____    |    this is too thin   |    ----    | 
				// |   |    |   |		and goes to ->  |   |    |   |
				// |___|    |___|					    |___|    |___|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				Polygon perimeter = new Polygon() { new IntPoint(0, 0), new IntPoint(5, 0), new IntPoint(5, 5), new IntPoint(0, 5) };
				Polygons correctedPath = planner.GetPathsWithOverlapsRemoved(perimeter, 400);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 6);
				Assert.IsTrue(correctedPath[1].Count == 2);
				Assert.IsTrue(correctedPath[2].Count == 6);
			}
		}
	}
}
