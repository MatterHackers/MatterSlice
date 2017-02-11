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
using System.IO;
using MatterHackers.QuadTree;
using MSClipperLib;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
	[TestFixture, Category("MatterSlice.SlicerLayerTests")]
	public class SlicerLayerTests
	{
		[Test]
		public void AlwaysRetractOnIslandChange()
		{
			string meshWithIslands = TestUtlities.GetStlPath("comb");
			string gCodeWithIslands = TestUtlities.GetTempGCodePath("comb-box");

			{
				// load a model that has 3 islands
				ConfigSettings config = new ConfigSettings();
				// make sure no retractions are going to occure that are island crossing
				config.MinimumTravelToCauseRetraction = 2000;
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(gCodeWithIslands);
				processor.LoadStlFile(meshWithIslands);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] gcodeContents = TestUtlities.LoadGCodeFile(gCodeWithIslands);
				int numLayers = TestUtlities.CountLayers(gcodeContents);
				for (int i = 1; i < numLayers - 1; i++)
				{
					string[] layer = TestUtlities.GetGCodeForLayer(gcodeContents, i);
					int totalRetractions = TestUtlities.CountRetractions(layer);
					Assert.IsTrue(totalRetractions == 6);
				}
			}
		}

		[Test]
		public void AllPerimetersGoInPolgonDirection()
		{
			string thinWallsSTL = TestUtlities.GetStlPath("ThinWallsRect.stl");
			string thinWallsGCode = TestUtlities.GetTempGCodePath("ThinWallsRect.stl");

			{
				// load a model that is correctly manifold
				ConfigSettings config = new ConfigSettings();
				config.ExpandThinWalls = true;
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(thinWallsGCode);
				processor.LoadStlFile(thinWallsSTL);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] thinWallsGCodeContent = TestUtlities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtlities.CountLayers(thinWallsGCodeContent);
				for(int i= 2; i< layerCount-2; i++)
				{
					var layerGCode = TestUtlities.GetGCodeForLayer(thinWallsGCodeContent, i);
					var polygons = TestUtlities.GetExtrusionPolygons(layerGCode, 1000);
					foreach(var polygon in polygons)
					{
						Assert.AreEqual(1, polygon.GetWindingDirection());
					}
				}
			}
		}

		[Test]
		public void CubePolygonWindingDirectionDoesNotMatter()
		{
			// simplified
			{
				// 								 (112500, 112500)
				//                                     /|
				//                                    / |
				//                                   /  |
				//                                  /   |
				//                                 /    |
				//                                /     |
				//                               /      |
				//                              /       |
				//                             /        |
				//                            /         |
				//                       0 ^ /         2|^
				//                          /           |
				//                         /            |
				//                        /             |
				//                       /              |
				//                      /               |
				//                     /                |
				//                    /                 |
				//                   /                  |
				//                  /                   o (112500, 94601)
				//                 /                   3|^
				// (92501, 92501) /_____________________| (112500, 92501)
				//                         1 >

				string[] segmentsToCheck = { "x: 92501, y: 92501 & x:112500, y: 112500 | x:92501, y:92501 & x:112500, y:92501 | x:112500, y: 94601 & x:112500, y: 112500 | x:112500, y: 92501 & x:112500, y: 94601 |" };
				CheckLayersAreSinglePolygon(segmentsToCheck);
			}

			// simplified
			{
				// (92501, 112500)  ______________2_>________________ (112500, 112500)
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                 0|^                             3|^
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               |
				//                  |                               o (112500, 94601)
				//                  |                              4|^
				// (92501, 92501)   |_______________________________| (112500, 92501)
				//                                1 >

				string[] segmentsToCheck = { "x: 92501, y: 92501 & x:92501, y: 112500 | x:112500, y: 92501 & x:92501, y: 92501 | x:92501, y: 112500 & x:112500, y: 112500 | x:112500, y: 94601 & x:112500, y: 112500 | x:112500, y: 92501 & x:112500, y: 94601 |" };
				CheckLayersAreSinglePolygon(segmentsToCheck);
			}

			// single fail case
			{
				// x: 92,501, y: 92,501 & x:92,501, y: 94,601
				//  (92.5, 92.5)-> (92.5, 94.6)
				//                                                  (92.5, 92.5) -> (92.5, 112.5)  |
				// x: 92,501, y: 94,601 & x:92,501, y: 112,500
				//  (92.5, 94.6)-> (92.5, 112.5)

				// x: 112,500, y: 92,501 & x:94,601, y: 92,501
				//     (112.5, 92.5)-> (94.6, 92.5)
				//                                                  (112.5, 92.5) -> (92.5, 92.5) ___
				// x: 94,601, y: 92,501 & x:92,501, y: 92,501
				//  (94.6, 92.5) -> (92.5, 92.5)

				// x: 92,501, y: 112,500 & x:94,601, y: 112,500
				//  (92.5, 112.5) -> (94.6, 112.5)
				//                                                  (92.5, 112.5) -> (112.5, 112.5) __
				// x: 94,601, y: 112,500 & x:112,500, y: 112,500
				//  (94.6, 112.5) -> (112.5, 112.5)

				// this is the strange one
				// x: 112,500, y: 94,601 & x:112,500, y: 112,500
				//  (112.5, 94.6) -> (112.5, 112.5)
				//                                                  (112.5, 92.5) - (112.5, 112.5) |
				// x: 112,500, y: 92,501 & x:112,500, y: 94,601
				//  (112.5, 92.5) -> (112.5, 94.6)

				string[] segmentsToCheck = { "x: 92501, y: 92501 & x:92501, y: 94601 | x:92501, y: 94601 & x:92501, y: 112500 | x:112500, y: 92501 & x:94601, y: 92501 | x:94601, y: 92501 & x:92501, y: 92501 | x:92501, y: 112500 & x:94601, y: 112500 | x:94601, y: 112500 & x:112500, y: 112500 | x:112500, y: 94601 & x:112500, y: 112500 | x:112500, y: 92501 & x:112500, y: 94601 |" };
				CheckLayersAreSinglePolygon(segmentsToCheck);
			}

			// lots from an actual file
			{
				string pathToData = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "CubeSegmentsX2.txt");

				string[] segmentsToCheck = File.ReadAllLines(pathToData);
				CheckLayersAreSinglePolygon(segmentsToCheck);
			}
		}

		[Test]
		public void DumpSegmentsWorks()
		{
			List<SlicePerimeterSegment> testSegments = new List<SlicePerimeterSegment>();
			testSegments.Add(new SlicePerimeterSegment(new IntPoint(1, 2), new IntPoint(3, 4)));
			testSegments.Add(new SlicePerimeterSegment(new IntPoint(4, 2), new IntPoint(5, 4)));
			testSegments.Add(new SlicePerimeterSegment(new IntPoint(3, 2), new IntPoint(9, 4)));
			testSegments.Add(new SlicePerimeterSegment(new IntPoint(6, 2), new IntPoint(3, 7)));

			string segmentsString = MeshProcessingLayer.DumpSegmentListToString(testSegments);
			List<SlicePerimeterSegment> outSegments = MeshProcessingLayer.CreateSegmentListFromString(segmentsString);

			Assert.True(testSegments.Count == outSegments.Count);
			for (int i = 0; i < testSegments.Count; i++)
			{
				Assert.True(testSegments[i].start == outSegments[i].start);
				Assert.True(testSegments[i].end == outSegments[i].end);
			}
		}

		[TestFixtureSetUp]
		public void TestSetup()
		{
			// Ensure the temp directory exists
			string tempDirectory = Path.GetDirectoryName(TestUtlities.GetTempGCodePath("na"));
			Directory.CreateDirectory(tempDirectory);
		}

		[Test]
		public void TetrahedronPolygonWindingDirectionDoesNotMatter()
		{
			// ccw
			{
				//      2
				//     /\
				//    /  \
				//  0/____\ 1

				string[] segmentsToCheck = { "x:0, y:0&x:10000, y:0|x:10000, y:0&x:5000, y:10000|x:5000, y:10000&x:0, y:0|", };
				CheckLayersAreSinglePolygon(segmentsToCheck);
			}

			// cw
			{
				//      1
				//     /\
				//    /  \
				//  0/____\ 2

				string[] segmentsToCheck = { "x:0, y:0&x:10000, y:0|x:5000, y:10000&x:0, y:0|x:10000, y:0&x:5000, y:10000|", };
				CheckLayersAreSinglePolygon(segmentsToCheck);
			}
		}

		[Test]
		public void TwoRingSegmentsCreatedCorrectly()
		{
			// lots from an actual file
			{
				string pathToData = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TwoRingSegmentsTestData.txt");

				string[] segmentsToCheck = File.ReadAllLines(pathToData);
				CheckLayersAreSinglePolygon(segmentsToCheck, 2);
			}
		}

		[Test]
		public void WindingDirectionDoesNotMatter()
		{
			string manifoldFile = TestUtlities.GetStlPath("20mm-box");
			string manifoldGCode = TestUtlities.GetTempGCodePath("20mm-box");
			string nonManifoldFile = TestUtlities.GetStlPath("20mm-box bad winding");
			string nonManifoldGCode = TestUtlities.GetTempGCodePath("20mm-box bad winding");

			{
				// load a model that is correctly manifold
				ConfigSettings config = new ConfigSettings();
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(manifoldGCode);
				processor.LoadStlFile(manifoldFile);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();
			}

			{
				// load a model that has some faces pointing the wrong way
				ConfigSettings config = new ConfigSettings();
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(nonManifoldGCode);
				processor.LoadStlFile(nonManifoldFile);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();
			}

			// load both gcode files and check that they are the same
			string manifoldGCodeContent = File.ReadAllText(manifoldGCode);
			string nonManifoldGCodeContent = File.ReadAllText(nonManifoldGCode);
			Assert.AreEqual(manifoldGCodeContent, nonManifoldGCodeContent);
		}

		private static void CheckLayersAreSinglePolygon(string[] segmentsToCheck, int expectedCount = 1)
		{
			foreach (string line in segmentsToCheck)
			{
				List<SlicePerimeterSegment> segmentsList = MeshProcessingLayer.CreateSegmentListFromString(line);
				MeshProcessingLayer layer = new MeshProcessingLayer(1, line);
				layer.MakePolygons();

				Assert.IsTrue(layer.PolygonList.Count == expectedCount);
			}
		}
	}
}