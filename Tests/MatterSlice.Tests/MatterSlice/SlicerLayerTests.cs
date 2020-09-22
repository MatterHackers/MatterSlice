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
using System.Linq;
using System.Threading;
using MatterHackers.QuadTree;
using MSClipperLib;
using NUnit.Framework;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice.Tests
{
#if __ANDROID__
	[TestFixture, Category("MatterSlice.SlicerLayerTests")]
#else
	[TestFixture, Category("MatterSlice.SlicerLayerTests"), Apartment(ApartmentState.STA)]
#endif
	public class SlicerLayerTests
	{
		[Test]
		public void AlwaysRetractOnIslandChange()
		{
			string meshWithIslands = TestUtilities.GetStlPath("comb");
			string gCodeWithIslands = TestUtilities.GetTempGCodePath("comb-box");

			{
				// load a model that has 3 islands
				var config = new ConfigSettings();
				// make sure no retractions are going to occur that are island crossing
				config.MinimumTravelToCauseRetraction = 2000;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gCodeWithIslands);
				processor.LoadStlFile(meshWithIslands);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gCodeWithIslands);
				int numLayers = TestUtilities.CountLayers(gcodeContents);
				for (int i = 1; i < numLayers - 1; i++)
				{
					string[] layer = TestUtilities.GetGCodeForLayer(gcodeContents, i);
					int totalRetractions = TestUtilities.CountRetractions(layer);
					Assert.IsTrue(totalRetractions == 4);
				}
			}
		}

		[Test]
		public void AllPerimetersGoInPolgonDirection()
		{
			string thinWallsSTL = TestUtilities.GetStlPath("ThinWallsRect");
			string thinWallsGCode = TestUtilities.GetTempGCodePath("ThinWallsRect.gcode");

			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				config.ExpandThinWalls = true;
				config.MergeOverlappingLines = false;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(thinWallsGCode);
				processor.LoadStlFile(thinWallsSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.CountLayers(loadedGCode);

				var layerPolygons = new List<Polygons>();
				for (int i = 0; i < layerCount; i++)
				{
					layerPolygons.Add(TestUtilities.GetExtrusionPolygons(loadedGCode.GetGCodeForLayer(i)));
				}

				for (int i = 2; i < layerCount - 2; i++)
				{
					Assert.LessOrEqual(layerPolygons[i].Count, 4, "We should not have add more than the 4 sides");
					foreach (var polygon in layerPolygons[i])
					{
						Assert.AreEqual(1, polygon.GetWindingDirection());
					}
				}
			}
		}

		[Test]
		public void SupportConnectedOptimaly()
		{
			string thinWallsSTL = TestUtilities.GetStlPath("two disks");
			string thinWallsGCode = TestUtilities.GetTempGCodePath("two disks.gcode");

			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				config.GenerateSupport = true;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(thinWallsGCode);
				processor.LoadStlFile(thinWallsSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.CountLayers(loadedGCode);

				for (int i = 0; i < layerCount; i++)
				{
					var layer = loadedGCode.GetGCodeForLayer(i).Movements().ToList();

					int longMoveCount = 0;
					for (var j = 1; j < layer.Count - 2; j++)
					{
						var start = layer[j - 1];
						var end = layer[j];
						if (end.extrusion - start.extrusion == 0
							&& (end.position - start.position).Length > 5)
						{
							longMoveCount++;
						}
					}

					Assert.LessOrEqual(longMoveCount, 6);
				}
			}
		}

		[Test]
		public void ExpandThinWallsFindsWalls()
		{
			string thinWallsSTL = TestUtilities.GetStlPath("ThinWalls");

			// without expand thin walls
			{
				string thinWallsGCode = TestUtilities.GetTempGCodePath("ThinWalls1.gcode");
				var config = new ConfigSettings
				{
					FirstLayerThickness = .2,
					LayerThickness = .2,
					NumberOfSkirtLoops = 0,
					InfillPercent = 0,
					NumberOfTopLayers = 0,
					NumberOfBottomLayers = 0,
					NumberOfPerimeters = 1,
					ExpandThinWalls = false,
					MergeOverlappingLines = false
				};
				var processor = new FffProcessor(config);
				processor.SetTargetFile(thinWallsGCode);
				processor.LoadStlFile(thinWallsSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.CountLayers(loadedGCode);
				Assert.AreEqual(50, layerCount);

				var layerPolygons = new List<Polygons>();
				for (int i = 0; i < layerCount; i++)
				{
					layerPolygons.Add(TestUtilities.GetExtrusionPolygons(loadedGCode.GetGCodeForLayer(i)));
				}

				Assert.AreEqual(6, layerPolygons[10].Count);
			}

			// with expand thin walls
			{
				string thinWallsGCode = TestUtilities.GetTempGCodePath("ThinWalls2.gcode");
				var config = new ConfigSettings();
				config.FirstLayerThickness = .2;
				config.LayerThickness = .2;
				config.NumberOfSkirtLoops = 0;
				config.InfillPercent = 0;
				config.NumberOfTopLayers = 0;
				config.NumberOfBottomLayers = 0;
				config.NumberOfPerimeters = 1;
				config.ExpandThinWalls = true;
				config.MergeOverlappingLines = false;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(thinWallsGCode);
				processor.LoadStlFile(thinWallsSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.CountLayers(loadedGCode);
				Assert.AreEqual(50, layerCount);

				var layerPolygons = new List<Polygons>();
				for (int i = 0; i < layerCount; i++)
				{
					layerPolygons.Add(TestUtilities.GetExtrusionPolygons(loadedGCode.GetGCodeForLayer(i)));
				}

				Assert.AreEqual(9, layerPolygons[10].Count);
			}

			// with expand thin walls and with merge overlapping lines
			{
				string thinWallsGCode = TestUtilities.GetTempGCodePath("ThinWalls3.gcode");
				var config = new ConfigSettings();
				config.FirstLayerThickness = .2;
				config.LayerThickness = .2;
				config.NumberOfSkirtLoops = 0;
				config.InfillPercent = 0;
				config.NumberOfTopLayers = 0;
				config.NumberOfBottomLayers = 0;
				config.NumberOfPerimeters = 1;
				config.ExpandThinWalls = true;
				config.MergeOverlappingLines = true;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(thinWallsGCode);
				processor.LoadStlFile(thinWallsSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.CountLayers(loadedGCode);
				Assert.AreEqual(50, layerCount);

				var layerPolygons = new List<Polygons>();
				for (int i = 0; i < layerCount; i++)
				{
					layerPolygons.Add(TestUtilities.GetExtrusionPolygons(loadedGCode.GetGCodeForLayer(i)));
				}

				Assert.AreEqual(9, layerPolygons[10].Count);
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
				LayersHaveCorrectPolygonCount(segmentsToCheck);
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
				LayersHaveCorrectPolygonCount(segmentsToCheck);
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
				LayersHaveCorrectPolygonCount(segmentsToCheck);
			}

			// lots from an actual file
			{
				string pathToData = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "CubeSegmentsX2.txt");

				string[] segmentsToCheck = File.ReadAllLines(pathToData);
				LayersHaveCorrectPolygonCount(segmentsToCheck);
			}
		}

		[Test]
		public void DumpSegmentsWorks()
		{
			var testSegments = new List<SlicePerimeterSegment>();
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

#if __ANDROID__
		[TestFixtureSetUp]
#else
		[OneTimeSetUp]
#endif
		public void TestSetup()
		{
			// Ensure the temp directory exists
			string tempDirectory = Path.GetDirectoryName(TestUtilities.GetTempGCodePath("na"));
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
				LayersHaveCorrectPolygonCount(segmentsToCheck);
			}

			// cw
			{
				//      1
				//     /\
				//    /  \
				//  0/____\ 2

				string[] segmentsToCheck = { "x:0, y:0&x:10000, y:0|x:5000, y:10000&x:0, y:0|x:10000, y:0&x:5000, y:10000|", };
				LayersHaveCorrectPolygonCount(segmentsToCheck);
			}
		}

		[Test]
		public void TwoRingSegmentsCreatedCorrectly()
		{
			// lots from an actual file
			{
				string pathToData = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TwoRingSegmentsTestData.txt");

				string[] segmentsToCheck = File.ReadAllLines(pathToData);
				LayersHaveCorrectPolygonCount(segmentsToCheck, 2);
			}
		}

		[Test]
		public void WindingDirectionDoesNotMatter()
		{
			string manifoldFile = TestUtilities.GetStlPath("20mm-box");
			string manifoldGCode = TestUtilities.GetTempGCodePath("20mm-box");
			string nonManifoldFile = TestUtilities.GetStlPath("20mm-box bad winding");
			string nonManifoldGCode = TestUtilities.GetTempGCodePath("20mm-box bad winding");

			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				var processor = new FffProcessor(config);
				processor.SetTargetFile(manifoldGCode);
				processor.LoadStlFile(manifoldFile);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();
			}

			{
				// load a model that has some faces pointing the wrong way
				var config = new ConfigSettings();
				var processor = new FffProcessor(config);
				processor.SetTargetFile(nonManifoldGCode);
				processor.LoadStlFile(nonManifoldFile);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();
			}

			// load both gcode files and check that they are the same
			TestUtilities.CheckPolysAreSimilar(manifoldGCode, nonManifoldGCode);
		}

		private static void LayersHaveCorrectPolygonCount(string[] segmentsToCheck, int expectedCount = 1)
		{
			foreach (string line in segmentsToCheck)
			{
				List<SlicePerimeterSegment> segmentsList = MeshProcessingLayer.CreateSegmentListFromString(line);
				var layer = new MeshProcessingLayer(1, line);
				layer.MakePolygons();

				Assert.AreEqual(expectedCount, layer.PolygonList.Count, "Did not have the expected perimeter count.");
			}
		}
	}
}