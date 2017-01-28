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
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	[TestFixture, Category("MatterSlice.GCodePlannerTests")]
	public class GCodePlannerTests
	{
		[Test]
		public void ClipSegmentTests()
		{
			GCodePath inPath = Newtonsoft.Json.JsonConvert.DeserializeObject<GCodePath>("{\"config\":{\"closedLoop\":true,\"lineWidth_um\":500,\"gcodeComment\":\"WALL-OUTER\",\"speed\":18.9,\"spiralize\":false,\"doSeamHiding\":true,\"Name\":\"inset0Config\"},\"points\":[{\"x\":105366,\"y\":108976,\"z\":300},{\"x\":105743,\"y\":109188,\"z\":300},{\"x\":106352,\"y\":109582,\"z\":300},{\"x\":106606,\"y\":109674,\"z\":300},{\"x\":107513,\"y\":110120,\"z\":300},{\"x\":107766,\"y\":110836,\"z\":300},{\"x\":107804,\"y\":110986,\"z\":300},{\"x\":107806,\"y\":111124,\"z\":300},{\"x\":106720,\"y\":116034,\"z\":300},{\"x\":106669,\"y\":116205,\"z\":300},{\"x\":106345,\"y\":116505,\"z\":300},{\"x\":103152,\"y\":117619,\"z\":300},{\"x\":102975,\"y\":117661,\"z\":300},{\"x\":102749,\"y\":117546,\"z\":300},{\"x\":101132,\"y\":116186,\"z\":300},{\"x\":100997,\"y\":115990,\"z\":300},{\"x\":100845,\"y\":115704,\"z\":300},{\"x\":100673,\"y\":114777,\"z\":300},{\"x\":100959,\"y\":109833,\"z\":300},{\"x\":101785,\"y\":109149,\"z\":300},{\"x\":101836,\"y\":109129,\"z\":300},{\"x\":101976,\"y\":109137,\"z\":300},{\"x\":102415,\"y\":109277,\"z\":300},{\"x\":102712,\"y\":108910,\"z\":300},{\"x\":103239,\"y\":108477,\"z\":300},{\"x\":103355,\"y\":108453,\"z\":300},{\"x\":103710,\"y\":108924,\"z\":300},{\"x\":104116,\"y\":108883,\"z\":300},{\"x\":104330,\"y\":108371,\"z\":300},{\"x\":104867,\"y\":108245,\"z\":300},{\"x\":104888,\"y\":108273,\"z\":300},{\"x\":104980,\"y\":109145,\"z\":300}]}");

			GCodePath controlPath = Newtonsoft.Json.JsonConvert.DeserializeObject<GCodePath>("{\"config\":{\"closedLoop\":true,\"lineWidth_um\":500,\"gcodeComment\":\"WALL-OUTER\",\"speed\":18.9,\"spiralize\":false,\"doSeamHiding\":true,\"Name\":\"inset0Config\"},\"points\":[{\"x\":105366,\"y\":108976,\"z\":300},{\"x\":105743,\"y\":109188,\"z\":300},{\"x\":106352,\"y\":109582,\"z\":300},{\"x\":106606,\"y\":109674,\"z\":300},{\"x\":107513,\"y\":110120,\"z\":300},{\"x\":107766,\"y\":110836,\"z\":300},{\"x\":107804,\"y\":110986,\"z\":300},{\"x\":107806,\"y\":111124,\"z\":300},{\"x\":106720,\"y\":116034,\"z\":300},{\"x\":106669,\"y\":116205,\"z\":300},{\"x\":106345,\"y\":116505,\"z\":300},{\"x\":103152,\"y\":117619,\"z\":300},{\"x\":102975,\"y\":117661,\"z\":300},{\"x\":102749,\"y\":117546,\"z\":300},{\"x\":101132,\"y\":116186,\"z\":300},{\"x\":100997,\"y\":115990,\"z\":300},{\"x\":100845,\"y\":115704,\"z\":300},{\"x\":100673,\"y\":114777,\"z\":300},{\"x\":100959,\"y\":109833,\"z\":300},{\"x\":101785,\"y\":109149,\"z\":300},{\"x\":101836,\"y\":109129,\"z\":300},{\"x\":101976,\"y\":109137,\"z\":300},{\"x\":102415,\"y\":109277,\"z\":300},{\"x\":102712,\"y\":108910,\"z\":300},{\"x\":103239,\"y\":108477,\"z\":300},{\"x\":103355,\"y\":108453,\"z\":300},{\"x\":103710,\"y\":108924,\"z\":300},{\"x\":104116,\"y\":108883,\"z\":300},{\"x\":104330,\"y\":108371,\"z\":300},{\"x\":104867,\"y\":108245,\"z\":300},{\"x\":104888,\"y\":108273,\"z\":300},{\"x\":104927,\"y\":108647,\"z\":300}]}");

			GCodePath testPath = GCodePlanner.TrimPerimeter(inPath, 0);

			Assert.IsTrue(controlPath.polygon.Count == testPath.polygon.Count);
			for (int i = 0; i < controlPath.polygon.Count; i++)
			{
				Assert.IsTrue(controlPath.polygon[i] == testPath.polygon[i]);
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

				Polygon perimeter = new Polygon() { new IntPoint(0, 0, 0), new IntPoint(5000, 0, 0), new IntPoint(5000, 5000, 0), new IntPoint(0, 5000, 0) };
				Assert.IsTrue(perimeter.Count == 4);
				Polygons thinLines;
				bool foundThinLines = perimeter.FindThinLines(400 / 2, 0, out thinLines);
				Assert.IsFalse(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Count == 0);
			}

			// A very simple collapse lower left start
			{
				//  ____________
				// s|__________|	  very simple  -> ----------

				Polygon perimeter = new Polygon() { new IntPoint(0, 0), new IntPoint(5000, 0), new IntPoint(5000, 50), new IntPoint(0, 50) };
				Polygons thinLines;
				bool foundThinLines = perimeter.FindThinLines(400, 0, out thinLines);
				Assert.IsTrue(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Count == 2);
				Assert.IsTrue(thinLines[0][0].Width == 50);
			}

			// A path that needs to have points inserted to do the correct thing
			{
				// |\      /|                     |\      /|
				// | \s___/ |   				  | \    / |
				// |________|	create points ->  |__----__|

				Polygon perimeter = new Polygon()
				{
					new IntPoint(5000, 50),
					new IntPoint(0, 10000),
					new IntPoint(0, 0),
					new IntPoint(15000, 0),
					new IntPoint(15000, 10000),
					new IntPoint(10000, 50),
				};
				Polygons thinLines;
				bool foundThinLines = perimeter.FindThinLines(400, 0, out thinLines);
				Assert.IsTrue(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Count == 2);
				Assert.IsTrue(thinLines[0][0].Width == 50);
			}

			// Simple overlap (s is the start runing ccw)
			{
				//     _____    _____ 5000,5000             _____    _____
				//     |   |    |   |					    |   |    |   |
				//     |   |____|   |					    |   |    |   |
				//     |   s____    |    this is too thin   |    ----    | 5000,2500
				//     |   |    |   |		and goes to ->  |   |    |   |
				// 0,0 |___|    |___|					    |___|    |___|

				Polygon perimeter = new Polygon()
				{
					// bottom of center line
					new IntPoint(1000, 2500-25), new IntPoint(4000, 2500-25),
					// right leg
					new IntPoint(4000, 0), new IntPoint(5000, 0), new IntPoint(5000, 5000), new IntPoint(4000, 5000),
					// top of center line
					new IntPoint(4000, 2500+27), new IntPoint(1000, 2500+27),
					// left leg
					new IntPoint(1000, 5000), new IntPoint(0, 5000), new IntPoint(0, 0), new IntPoint(1000, 0),
				};
				Polygons thinLines;
				bool foundThinLines = perimeter.FindThinLines(400, 0, out thinLines);
				Assert.IsTrue(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Count == 2);
				Assert.IsTrue(thinLines[0][0].Width == 52);
			}

			// Simple overlap that must not be generated (s is the start runing ccw)
			{
				//     s_____________ 5000,5000             s_____________
				//     |    ____    |					    |    ____    |
				//     |   |    |___|					    |   |    |   |
				//     |   |     ___     this is too thin   |   |     ---  5000,2500
				//     |   |____|   |		but most not    |   |____|   |
				// 0,0 |____________|		go to	->      |____________|

				Polygon perimeter = new Polygon()
				{
					new IntPoint(0, 5000), new IntPoint(0, 0), new IntPoint(5000, 0), new IntPoint(5000, 2500-27),
					new IntPoint(4000, 2500-27), new IntPoint(4000, 1000), new IntPoint(1000, 1000),
					new IntPoint(1000, 4000), new IntPoint(4000, 4000), new IntPoint(4000, 2500+27), new IntPoint(5000, 2500+27),
					new IntPoint(5000, 5000), new IntPoint(0, 5000),
				};
				Polygons thinLines;
				bool foundThinLines = perimeter.FindThinLines(400, 0, out thinLines);
				Assert.IsFalse(foundThinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Count == 0);
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
				Polygon perimeter = new Polygon() { new IntPoint(0, 0, 0), new IntPoint(5000, 0, 0), new IntPoint(5000, 5000, 0), new IntPoint(0, 5000, 0) };
				Assert.IsTrue(perimeter.Count == 4);
				Polygons thinLines;
				perimeter.MergePerimeterOverlaps(400 / 4, out thinLines);
				Assert.IsTrue(thinLines.Count == 1);
				Assert.IsTrue(thinLines[0].Count == 5); // it is 5 because we return a closed path (points = 0, 1, 2, 3, 0)
				for (int i = 0; i < perimeter.Count; i++)
				{
					Assert.IsTrue(perimeter[i] == thinLines[0][i]);
				}
			}

			// A very simple collapse lower left start
			{
				//  ____________
				// s|__________|	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				Polygon perimeter = new Polygon() { new IntPoint(0, 0), new IntPoint(5000, 0), new IntPoint(5000, 50), new IntPoint(0, 50) };
				Polygons correctedPath;
				perimeter.MergePerimeterOverlaps(400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 2);
				Assert.IsTrue(correctedPath[0][0].Width == 450);
			}

			// A very simple collapse upper left start
			{
				// s____________
				//  |__________|	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				Polygon perimeter = new Polygon() { new IntPoint(0, 50), new IntPoint(0, 0), new IntPoint(5000, 0), new IntPoint(5000, 50) };
				Polygons correctedPath;
				perimeter.MergePerimeterOverlaps(200, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 2);
				Assert.IsTrue(correctedPath[0][0].Width == 200);
				Assert.IsTrue(correctedPath[1][0].Width == 250);
				Assert.IsTrue(correctedPath[2][0].Width == 200);
			}

			// A very simple collapse upper right start
			{
				//  ____________s
				//  |__________|	  very simple  -> ----------

				Polygon perimeter = new Polygon() { new IntPoint(5000, 50), new IntPoint(0, 50), new IntPoint(0, 0), new IntPoint(5000, 0), new IntPoint(5000, 50) };
				Polygons correctedPath;
				perimeter.MergePerimeterOverlaps(400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 2);
				Assert.IsTrue(correctedPath[0][0].Width == 450);
			}

			// A very simple collapse lower left start
			{
				//  ____________
				//  |__________|s	  very simple  -> ----------

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				Polygon perimeter = new Polygon() { new IntPoint(5000, 0), new IntPoint(5000, 50), new IntPoint(0, 50), new IntPoint(0, 0) };
				Polygons correctedPath;
				perimeter.MergePerimeterOverlaps(400 / 4, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 2);
			}

			// A path that has been clipped
			{
				//  ____________s
				//  |_________	  goes to  -> ----------

				Polygon perimeter = new Polygon() { new IntPoint(5000, 50), new IntPoint(0, 50), new IntPoint(0, 0), new IntPoint(4500, 0) };
				Polygons correctedPath;
				bool removedLines = perimeter.MergePerimeterOverlaps(400, out correctedPath, false);
				Assert.IsTrue(removedLines);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 2);
				Assert.IsTrue(correctedPath[0][0].Width == 400);
				Assert.IsTrue(correctedPath[0][0] == new IntPoint(5000, 50));
				Assert.IsTrue(correctedPath[0][1] == new IntPoint(4500, 50));
				Assert.IsTrue(correctedPath[1].Count == 2);
				Assert.IsTrue(correctedPath[1][0].Width == 450);
				Assert.IsTrue(correctedPath[1][0] == new IntPoint(4500, 25));
				Assert.IsTrue(correctedPath[1][1] == new IntPoint(0, 25));
				Assert.IsTrue(correctedPath[2].Count == 2);
				Assert.IsTrue(correctedPath[2][0].Width == 400);
			}

			// A path that needs to have points inserted to do the correct thing
			{
				// |\      /|                     |\      /|
				// | \s___/ |   				  | \    / |
				// |________|	create points ->  |__----__|

				Polygon perimeter = new Polygon()
				{
					new IntPoint(5000, 50),
					new IntPoint(0, 10000),
					new IntPoint(0, 0),
					new IntPoint(15000, 0),
					new IntPoint(15000, 10000),
					new IntPoint(10000, 50),
				};
				Polygons correctedPath;
				perimeter.MergePerimeterOverlaps(400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 4);
				Assert.IsTrue(correctedPath[1].Count == 2);
				Assert.IsTrue(correctedPath[2].Count == 4);
			}

			// Simple overlap (s is the start runing ccw)
			{
				//     _____    _____ 5000,5000             _____    _____
				//     |   |    |   |					    |   |    |   |
				//     |   |____|   |					    |   |    |   |
				//     |   s____    |    this is too thin   |    ----    | 5000,2500
				//     |   |    |   |		and goes to ->  |   |    |   |
				// 0,0 |___|    |___|					    |___|    |___|

				Polygon perimeter = new Polygon()
				{
					// bottom of center line
					new IntPoint(1000, 2500-25), new IntPoint(4000, 2500-25),
					// right leg
					new IntPoint(4000, 0), new IntPoint(5000, 0), new IntPoint(5000, 5000), new IntPoint(4000, 5000),
					// top of center line
					new IntPoint(4000, 2500+25), new IntPoint(1000, 2500+25),
					// left leg
					new IntPoint(1000, 5000), new IntPoint(0, 5000), new IntPoint(0, 0), new IntPoint(1000, 0),
				};
				Polygons correctedPath;
				perimeter.MergePerimeterOverlaps(400, out correctedPath);
				Assert.IsTrue(correctedPath.Count == 3);
				Assert.IsTrue(correctedPath[0].Count == 2);
				Assert.IsTrue(correctedPath[1].Count == 6);
				Assert.IsTrue(correctedPath[2].Count == 6);
			}
		}

		[Test]
		public void MakeCloseSegmentsMergable()
		{
			// check that we can cut up a single segment
			{
				List<Segment> segmentsControl = Segment.ConvertToSegments(new Polygon()
				{
					new IntPoint(0, 0),
					new IntPoint(2500, 0),
					new IntPoint(5000, 0),
				}, false);

				Polygon cuts = new Polygon()
				{
					new IntPoint(2500, 0),
					new IntPoint(2500, 401),
				};

				Segment segmentToCut = new Segment()
				{
					Start = new IntPoint(0, 0),
					End = new IntPoint(5000, 0),
				};

				var touchingEnumerator = new PolygonEdgeIterator(cuts, 400);
				List<Segment> segmentsTest = segmentToCut.GetSplitSegmentForVertecies(touchingEnumerator);
				Assert.IsTrue(segmentsControl.Count == segmentsTest.Count);
				for (int i = 0; i < segmentsTest.Count; i++)
				{
					Assert.IsTrue(segmentsTest[i] == segmentsControl[i]);
				}
			}

			// A path that needs to have points inserted to do the correct thing
			{
				// |\      /|                     |\      /|
				// | \s___/ |   				  | \____/ |
				// |________|	create points ->  |_.____._|

				Polygon perimeter = new Polygon()
				{
					new IntPoint(5000, 50),
					new IntPoint(0, 10000),
					new IntPoint(0, 0),
					new IntPoint(15000, 0),
					new IntPoint(15000, 10000),
					new IntPoint(10000, 50),
				};
				Assert.IsTrue(perimeter.Count == 6);
				Polygon correctedPath = perimeter.MakeCloseSegmentsMergable(400 / 4);
				Assert.IsTrue(correctedPath.Count == 8);
			}

			// A path that has been clipped
			{
				//  ____________s             ____________ __
				//           *    goes to  ->

				long mergeDistance = 400 / 4;
				Segment segment = new Segment(new IntPoint(5000, 50), new IntPoint(0, 50));
				var touchingEnumerator = new PolygonEdgeIterator(new Polygon { new IntPoint(4500, 0) }, mergeDistance);
				List<Segment> segments = segment.GetSplitSegmentForVertecies(touchingEnumerator);
				Assert.IsTrue(segments.Count == 2);
				Assert.IsTrue(segments[0] == new Segment(new IntPoint(5000, 50), new IntPoint(4500, 50)));
				Assert.IsTrue(segments[1] == new Segment(new IntPoint(4500, 50), new IntPoint(0, 50)));
			}

			// A path that has been clipped
			{
				//  ____________s
				//  |_________	  goes to  -> ----------

				long mergeDistance = 400 / 4;
				Polygon perimeter = new Polygon() { new IntPoint(5000, 50), new IntPoint(0, 50), new IntPoint(0, 0), new IntPoint(4500, 0) };
				Assert.IsTrue(perimeter.Count == 4);
				Polygon correctedPath = perimeter.MakeCloseSegmentsMergable(mergeDistance, false);
				Assert.IsTrue(correctedPath.Count == 5);
				Assert.IsTrue(correctedPath[0] == new IntPoint(5000, 50));
				Assert.IsTrue(correctedPath[1] == new IntPoint(4500, 50));
				Assert.IsTrue(correctedPath[2] == new IntPoint(0, 50));
				Assert.IsTrue(correctedPath[3] == new IntPoint(0, 0));
				Assert.IsTrue(correctedPath[4] == new IntPoint(4500, 0));
			}

			// Make sure we work correctly when splitting the closing segment.
			{
				// |\      /|                     |\      /|
				// | \____/ |   				  | \____/ |
				// |_______s|	create points ->  |_.____._|

				int travelSpeed = 50;
				int retractionMinimumDistance = 20;
				GCodePlanner planner = new GCodePlanner(new GCodeExport(), travelSpeed, retractionMinimumDistance);
				Polygon perimeter = new Polygon()
				{
					new IntPoint(15000, 0),
					new IntPoint(15000, 10000),
					new IntPoint(10000, 50),
					new IntPoint(5000, 50),
					new IntPoint(0, 10000),
					new IntPoint(0, 0),
				};
				Assert.IsTrue(perimeter.Count == 6);
				Polygon correctedPath = perimeter.MakeCloseSegmentsMergable(400 / 4);
				Assert.IsTrue(correctedPath.Count == 8);
			}
		}

		[Test]
		public void MergePathsIgnoringCollinearLines()
		{
			//GCodePath path = Newtonsoft.Json.JsonConvert.DeserializeObject<GCodePath>("{\"config\":{\"closedLoop\":true,\"lineWidth_um\":500,\"gcodeComment\":\"WALL-OUTER\",\"speed\":15.0,\"spiralize\":false,\"doSeamHiding\":true,\"Name\":\"inset0Config\"},\"points\":[{\"X\":0,\"Y\":6290,\"Width\":0,\"Z\":200},{\"X\":0,\"Y\":6290,\"Width\":0,\"Z\":200},{\"X\":0,\"Y\":6290,\"Width\":0,\"Z\":200},{\"X\":401,\"Y\":6277,\"Width\":0,\"Z\":200},{\"X\":787,\"Y\":6240,\"Width\":0,\"Z\":200},{\"X\":1185,\"Y\":6177,\"Width\":0,\"Z\":200},{\"X\":1564,\"Y\":6092,\"Width\":0,\"Z\":200},{\"X\":1944,\"Y\":5982,\"Width\":0,\"Z\":200},{\"X\":2315,\"Y\":5848,\"Width\":0,\"Z\":200},{\"X\":2671,\"Y\":5693,\"Width\":0,\"Z\":200},{\"X\":3036,\"Y\":5508,\"Width\":0,\"Z\":200},{\"X\":3369,\"Y\":5310,\"Width\":0,\"Z\":200},{\"X\":3691,\"Y\":5093,\"Width\":0,\"Z\":200}]}");
			GCodePath path = Newtonsoft.Json.JsonConvert.DeserializeObject<GCodePath>("{\"config\":{\"closedLoop\":true,\"lineWidth_um\":500,\"gcodeComment\":\"WALL-OUTER\",\"speed\":15.0,\"spiralize\":false,\"doSeamHiding\":true,\"Name\":\"inset0Config\"},\"points\":[{\"X\":0,\"Y\":6290,\"Width\":0,\"Z\":200},{\"X\":0,\"Y\":6290,\"Width\":0,\"Z\":200},{\"X\":0,\"Y\":6290,\"Width\":0,\"Z\":200}]}");

			Polygons pathsWithOverlapsRemoved;
			bool pathIsClosed = false;

			bool pathHadOverlaps = path.polygon.MergePerimeterOverlaps(path.config.lineWidth_um, out pathsWithOverlapsRemoved, pathIsClosed)
				&& pathsWithOverlapsRemoved.Count > 0;

			Assert.IsFalse(pathHadOverlaps);
		}

		[Test]
		public void QuadTreeWorking()
		{
			var tree = new QuadTree<int>(5, 10, 10, 2000, 2000);
			tree.Insert(0, new Quad(50, 50, 60, 60));
			tree.Insert(1, new Quad(52, 53, 60, 60));
			//tree.Insert(0, new Quad(500, 50, 560, 60));
			//tree.Insert(0, new Quad(20, 50, 61, 60));
			//tree.Insert(0, new Quad(150, 50, 160, 60));

			int count = 0;
			tree.FindCollisions(0);
			foreach (var index in tree.QueryResults)
			{
				count++;
			}

			Assert.IsTrue(count == 1);
		}
	}
}