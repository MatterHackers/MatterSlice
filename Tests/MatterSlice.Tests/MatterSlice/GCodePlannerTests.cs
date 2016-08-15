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

using ClipperLib;
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
		public void MakeCloseSegmentsMergable()
		{
			// check that we can cut up a single segment
			{
				List<Segment> segmentsControl = Segment.ConvertPathToSegments(new List<Point3>()
				{
					new Point3(0, 0),
					new Point3(2500, 0),
					new Point3(5000, 0),
				}, false);

				List<Point3> cuts = new List<Point3>()
				{
					new Point3(2500, 0),
					new Point3(2500, 401),
				};

				Segment segmentToCut = new Segment()
				{
					Start = new Point3(0, 0),
					End = new Point3(5000, 0),
				};
					

				List<Segment> segmentsTest = segmentToCut.GetSplitSegmentForVertecies(cuts, 400);
				Assert.IsTrue(segmentsControl.Count == segmentsTest.Count);
				for(int i=0; i<segmentsTest.Count; i++)
				{
					Assert.IsTrue(segmentsTest[i] == segmentsControl[i]);
				}
			}

			// A path that needs to have points inserted to do the correct thing
			{
				// |\      /|                     |\      /|
				// | \s___/ |   				  | \____/ |
				// |________|	create points ->  |_.____._|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>()
				{
					new Point3(5000, 50),
					new Point3(0, 10000),
					new Point3(0, 0),
					new Point3(15000, 0),
					new Point3(15000, 10000),
					new Point3(10000, 50),
				};
				Assert.IsTrue(perimeter.Count == 6);
				List<Point3> correctedPath = planner.MakeCloseSegmentsMergable(perimeter, 400 / 4);
				Assert.IsTrue(correctedPath.Count == 8);
			}

			// A path that has been clipped
			{
				//  ____________s             ____________ __
				//           *    goes to  -> 

				long mergeDistance = 400 / 4;
				Segment segment = new Segment(new Point3(5000, 50), new Point3(0, 50));
				List<Segment> segments = segment.GetSplitSegmentForVertecies(new List<Point3> { new Point3(4500, 0) }, mergeDistance);
				Assert.IsTrue(segments.Count == 2);
				Assert.IsTrue(segments[0] == new Segment(new Point3(5000, 50), new Point3(4500, 50)));
				Assert.IsTrue(segments[1] == new Segment(new Point3(4500, 50), new Point3(0, 50)));
			}

			// A path that has been clipped
			{
				//  ____________s   
				//  |_________	  goes to  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				long mergeDistance = 400 / 4;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() { new Point3(5000, 50), new Point3(0, 50), new Point3(0, 0), new Point3(4500, 0)};
				Assert.IsTrue(perimeter.Count == 4);
				List<Point3> correctedPath = planner.MakeCloseSegmentsMergable(perimeter, mergeDistance, false);
				Assert.IsTrue(correctedPath.Count == 5);
				Assert.IsTrue(correctedPath[0] == new Point3(5000, 50));
				Assert.IsTrue(correctedPath[1] == new Point3(4500, 50));
				Assert.IsTrue(correctedPath[2] == new Point3(0, 50));
				Assert.IsTrue(correctedPath[3] == new Point3(0, 0));
				Assert.IsTrue(correctedPath[4] == new Point3(4500, 0));
			}


			// Make sure we work correctly when splitting the closing segment.
			{
				// |\      /|                     |\      /|
				// | \____/ |   				  | \____/ |
				// |_______s|	create points ->  |_.____._|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>()
				{
					new Point3(15000, 0),
					new Point3(15000, 10000),
					new Point3(10000, 50),
					new Point3(5000, 50),
					new Point3(0, 10000),
					new Point3(0, 0),
				};
				Assert.IsTrue(perimeter.Count == 6);
				List<Point3> correctedPath = planner.MakeCloseSegmentsMergable(perimeter, 400 / 4);
				Assert.IsTrue(correctedPath.Count == 8);
			}
		}

		[Test]
		public void FindThinFeaturesTests()
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
				List<Point3> perimeter = new List<Point3>() { new Point3(0, 0, 0), new Point3(5000, 0, 0), new Point3(5000, 5000, 0), new Point3(0, 5000, 0) };
				Assert.IsTrue(perimeter.Count == 4);
				List<PathAndWidth> thinLines;
				bool foundThinLines = planner.FindThinLines(perimeter, 400 / 2, out thinLines);
				Assert.IsFalse(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Path.Count == 0);
			}

			// A very simple collapse lower left start
			{
				//  ____________   
				// s|__________|	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() { new Point3(0, 0), new Point3(5000, 0), new Point3(5000, 50), new Point3(0, 50) };
				List<PathAndWidth> thinLines;
				bool foundThinLines = planner.FindThinLines(perimeter, 400, out thinLines);
				Assert.IsTrue(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Path.Count == 2);
				Assert.IsTrue(thinLines[0].ExtrusionWidthUm == 50);
			}

			// A path that needs to have points inserted to do the correct thing
			{
				// |\      /|                     |\      /|
				// | \s___/ |   				  | \    / |
				// |________|	create points ->  |__----__|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>()
				{
					new Point3(5000, 50),
					new Point3(0, 10000),
					new Point3(0, 0),
					new Point3(15000, 0),
					new Point3(15000, 10000),
					new Point3(10000, 50),
				};
				List<PathAndWidth> thinLines;
				bool foundThinLines = planner.FindThinLines(perimeter, 400, out thinLines);
				Assert.IsTrue(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Path.Count == 2);
				Assert.IsTrue(thinLines[0].ExtrusionWidthUm == 50);
			}

			// Simple overlap (s is the start runing ccw)
			{
				//     _____    _____ 5000,5000             _____    _____
				//     |   |    |   |					    |   |    |   |
				//     |   |____|   |					    |   |    |   |
				//     |   s____    |    this is too thin   |    ----    | 5000,2500
				//     |   |    |   |		and goes to ->  |   |    |   |
				// 0,0 |___|    |___|					    |___|    |___|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>()
				{ 
					// bottom of center line
					new Point3(1000, 2500-25), new Point3(4000, 2500-25),
					// right leg
					new Point3(4000, 0), new Point3(5000, 0), new Point3(5000, 5000), new Point3(4000, 5000), 
					// top of center line
					new Point3(4000, 2500+27), new Point3(1000, 2500+27),
					// left leg
					new Point3(1000, 5000), new Point3(0, 5000), new Point3(0, 0), new Point3(1000, 0),
				};
				List<PathAndWidth> thinLines;
				bool foundThinLines = planner.FindThinLines(perimeter, 400, out thinLines);
				Assert.IsTrue(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Path.Count == 2);
				Assert.IsTrue(thinLines[0].ExtrusionWidthUm == 52);
			}
		}

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
				List<Point3> perimeter = new List<Point3>() { new Point3(0, 0, 0), new Point3(5000, 0, 0), new Point3(5000, 5000, 0), new Point3(0, 5000, 0)};
				Assert.IsTrue(perimeter.Count == 4);
				List<PathAndWidth> thinLines;
				planner.RemovePerimeterOverlaps(perimeter, 400 / 4, out thinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Path.Count == 5); // it is 5 because we return a closed path (points = 0, 1, 2, 3, 0)
				for (int i = 0; i < perimeter.Count; i++)
				{
					Assert.IsTrue(perimeter[i] == thinLines[0].Path[i]);
				}
			}

			// A very simple collapse lower left start
			{
				//  ____________   
				// s|__________|	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() { new Point3(0, 0), new Point3(5000, 0), new Point3(5000, 50), new Point3(0, 50)};
				List<PathAndWidth> correctedPath;
				planner.RemovePerimeterOverlaps(perimeter, 400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Path.Count == 2);
				Assert.IsTrue(correctedPath[0].ExtrusionWidthUm == 450);
			}

			// A very simple collapse upper left start
			{
				// s____________   
				//  |__________|	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() { new Point3(0, 50), new Point3(0, 0), new Point3(5000, 0), new Point3(5000, 50) };
				List<PathAndWidth> correctedPath;
				planner.RemovePerimeterOverlaps(perimeter, 200, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Path.Count == 2);
				Assert.IsTrue(correctedPath[0].ExtrusionWidthUm == 200);
				Assert.IsTrue(correctedPath[1].ExtrusionWidthUm == 250);
				Assert.IsTrue(correctedPath[2].ExtrusionWidthUm == 200);
			}

			// A very simple collapse upper right start
			{
				//  ____________s   
				//  |__________|	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() { new Point3(5000, 50), new Point3(0, 50), new Point3(0, 0), new Point3(5000, 0), new Point3(5000, 50) };
				List<PathAndWidth> correctedPath;
				planner.RemovePerimeterOverlaps(perimeter, 400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Path.Count == 2);
				Assert.IsTrue(correctedPath[0].ExtrusionWidthUm == 450);
			}

			// A very simple collapse lower left start
			{
				//  ____________   
				//  |__________|s	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() { new Point3(5000, 0), new Point3(5000, 50), new Point3(0, 50), new Point3(0, 0)};
				List<PathAndWidth> correctedPath;
				planner.RemovePerimeterOverlaps(perimeter, 400 / 4, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Path.Count == 2);
			}

			// A path that has been clipped
			{
				//  ____________s   
				//  |_________	  goes to  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() { new Point3(5000, 50), new Point3(0, 50), new Point3(0, 0), new Point3(4500, 0) };
				List<PathAndWidth> correctedPath;
				bool removedLines = planner.RemovePerimeterOverlaps(perimeter, 400, out correctedPath, false);
				Assert.IsTrue(removedLines);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Path.Count == 2);
				Assert.IsTrue(correctedPath[0].ExtrusionWidthUm == 400);
				Assert.IsTrue(correctedPath[0].Path[0] == new Point3(5000, 50));
				Assert.IsTrue(correctedPath[0].Path[1] == new Point3(4500, 50));
				Assert.IsTrue(correctedPath[1].Path.Count == 2);
				Assert.IsTrue(correctedPath[1].ExtrusionWidthUm == 450);
				Assert.IsTrue(correctedPath[1].Path[0] == new Point3(4500, 25));
				Assert.IsTrue(correctedPath[1].Path[1] == new Point3(0, 25));
				Assert.IsTrue(correctedPath[2].Path.Count == 2);
				Assert.IsTrue(correctedPath[2].ExtrusionWidthUm == 400);
			}

			// A path that needs to have points inserted to do the correct thing
			{
				// |\      /|                     |\      /|
				// | \s___/ |   				  | \    / |
				// |________|	create points ->  |__----__|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>()
				{
					new Point3(5000, 50),
					new Point3(0, 10000),
					new Point3(0, 0),
					new Point3(15000, 0),
					new Point3(15000, 10000),
					new Point3(10000, 50),
				};
				List<PathAndWidth> correctedPath;
				planner.RemovePerimeterOverlaps(perimeter, 400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Path.Count == 4);
				Assert.IsTrue(correctedPath[1].Path.Count == 2);
				Assert.IsTrue(correctedPath[2].Path.Count == 4);
			}

			// Simple overlap (s is the start runing ccw)
			{
				//     _____    _____ 5000,5000             _____    _____
				//     |   |    |   |					    |   |    |   |
				//     |   |____|   |					    |   |    |   |
				//     |   s____    |    this is too thin   |    ----    | 5000,2500
				//     |   |    |   |		and goes to ->  |   |    |   |
				// 0,0 |___|    |___|					    |___|    |___|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				List<Point3> perimeter = new List<Point3>() 
				{ 
					// bottom of center line
					new Point3(1000, 2500-25), new Point3(4000, 2500-25),
					// right leg
					new Point3(4000, 0), new Point3(5000, 0), new Point3(5000, 5000), new Point3(4000, 5000), 
					// top of center line
					new Point3(4000, 2500+25), new Point3(1000, 2500+25),
					// left leg
					new Point3(1000, 5000), new Point3(0, 5000), new Point3(0, 0), new Point3(1000, 0), 
				};
				List<PathAndWidth> correctedPath;
				planner.RemovePerimeterOverlaps(perimeter, 400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Path.Count == 2);
				Assert.IsTrue(correctedPath[1].Path.Count == 6);
				Assert.IsTrue(correctedPath[2].Path.Count == 6);
			}
		}
	}
}
