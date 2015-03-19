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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterSlice.ClipperLib;
using MatterHackers.MatterSlice;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
	[TestFixture]
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
				config.minimumTravelToCauseRetraction = 2000;
				fffProcessor processor = new fffProcessor(config);
				processor.setTargetFile(gCodeWithIslands);
				processor.LoadStlFile(meshWithIslands);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] gcodeContents = TestUtlities.LoadGCodeFile(gCodeWithIslands);
				int numLayers = TestUtlities.CountLayers(gcodeContents);
				for (int i = 1; i < numLayers - 1; i++)
				{
					string[] layer = TestUtlities.GetGCodeForLayer(gcodeContents, i);
					int numRetractions = TestUtlities.CountRetractions(layer);
					Assert.IsTrue(numRetractions == 4);
				}
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
				processor.setTargetFile(manifoldGCode);
				processor.LoadStlFile(manifoldFile);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();
			}

			{
				// load a model that has some faces pointing the wroing way
				ConfigSettings config = new ConfigSettings();
				fffProcessor processor = new fffProcessor(config);
				processor.setTargetFile(nonManifoldGCode);
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
	}

	[TestFixture]
	public class ClipperTests
	{
		[Test]
		public void CleanPolygons()
		{
			// remove a single point that is going to be coincident
			{
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				testPath.Add(new IntPoint(5, 0));
				testPath.Add(new IntPoint(11, 0));
				testPath.Add(new IntPoint(5, 20));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, 10);
				Assert.IsTrue(cleanedPath.Count == 3);
			}

			// don't remove a non collinear point
			{
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				testPath.Add(new IntPoint(50, 5));
				testPath.Add(new IntPoint(100, 0));
				testPath.Add(new IntPoint(50, 200));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, 4);
				Assert.IsTrue(cleanedPath.Count == 4);
			}

			// now remove that point with a higher tolerance
			{
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				testPath.Add(new IntPoint(50, 5));
				testPath.Add(new IntPoint(100, 0));
				testPath.Add(new IntPoint(50, 200));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, 6);
				Assert.IsTrue(cleanedPath.Count == 3);
			}

			// now remove a bunch of points
			{
				int mergeDist = 10;
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				Random randY = new Random(0);
				for (int i = 2; i < 58; i++)
				//	for (int i = 2; i < 98; i++)
				{
					testPath.Add(new IntPoint(i, (int)(randY.NextDouble() * mergeDist - mergeDist/2)));
				}
				testPath.Add(new IntPoint(100, 0));
				testPath.Add(new IntPoint(50, 200));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, mergeDist);
				Assert.IsTrue(cleanedPath.Count == 3);
				//Assert.IsTrue(cleanedPath.Contains(new IntPoint(0, 0)));
				//Assert.IsTrue(cleanedPath.Contains(new IntPoint(100, 0)));
				//Assert.IsTrue(cleanedPath.Contains(new IntPoint(50, 200)));
			}
		}
	}

	public static class SlicingTests
	{
		static bool ranTests = false;

		public static bool RanTests { get { return ranTests; } }
		public static void Run()
		{
			if (!ranTests)
			{
				SlicerLayerTests slicerLayerTests = new SlicerLayerTests();
				slicerLayerTests.WindingDirectionDoesNotMatter();
				slicerLayerTests.AlwaysRetractOnIslandChange();

				ClipperTests clipperTests = new ClipperTests();
				clipperTests.CleanPolygons();

				ranTests = true;
			}
		}
	}
}
