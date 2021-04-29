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

using System.Linq;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{

	[TestFixture, Category("MatterSlice")]
	public class IslandDetectionTests
	{
		[Test]
		public void CorrectIslandCount()
		{
			string engineStlFile = TestUtilities.GetStlPath("Engine-Benchmark");
			string engineGCodeFile = TestUtilities.GetTempGCodePath("Engine-Benchmark.gcode");

			var config = new ConfigSettings();
			config.FirstLayerThickness = .2;
			config.LayerThickness = .2;
			config.NumberOfSkirtLoops = 0;
			config.InfillPercent = 0;
			config.NumberOfTopLayers = 0;
			config.NumberOfBottomLayers = 0;
			config.NumberOfPerimeters = 1;
			config.MergeOverlappingLines = false;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(engineGCodeFile);
			processor.LoadStlFile(engineStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Finalize();

			var loadedGCode = TestUtilities.LoadGCodeFile(engineGCodeFile);
			var layers = TestUtilities.LayerCount(loadedGCode);
			Assert.AreEqual(195, layers);

			var layerPolygons = TestUtilities.GetAllExtrusionPolygons(loadedGCode);

			Assert.AreEqual(17, layerPolygons[32].Count);
			for (int i = 33; i < 44; i++)
			{
				Assert.AreEqual(13, layerPolygons[i].Where(p => p.Count > 2).Count());
			}
		}

		[Test]
		public void CorrectIslandCount2()
		{
			void Test(bool mergeOverlaps, bool expandWalls)
			{
				string engineStlFile = TestUtilities.GetStlPath("all_layers");
				string engineGCodeFile = TestUtilities.GetTempGCodePath($"all_layers - merge overlap {mergeOverlaps} - expand walls {expandWalls}.gcode");

				var config = new ConfigSettings();
				config.FirstLayerThickness = .2;
				config.LayerThickness = .2;
				config.NumberOfSkirtLoops = 0;
				config.InfillPercent = 0;
				config.NumberOfTopLayers = 0;
				config.NumberOfBottomLayers = 0;
				config.NumberOfPerimeters = 1;
				config.MergeOverlappingLines = mergeOverlaps;
				config.ExpandThinWalls = expandWalls;
				config.FillThinGaps = false;
				config.AvoidCrossingPerimeters = false;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(engineGCodeFile);
				processor.LoadStlFile(engineStlFile);
				// slice and save it
				processor.DoProcessing();
				processor.Finalize();

				var loadedGCode = TestUtilities.LoadGCodeFile(engineGCodeFile);
				var layers = TestUtilities.LayerCount(loadedGCode);
				Assert.AreEqual(45, layers);

				var expectedIslands = new int[]
				{
					7, 7, 7, 5, 5, // 0 - 4
					5, 5, 5, 5, 5, // 5 - 9
					5, 5, 5, 5, 5, // 10 - 14
					5, 5, 5, 5, 5,
					5, 5, 5, 5, 5,
					5, 4, 4, 4, 4,
					4, 4, 4, 4, 4,
					4, 4, 4, 4, 4,
					4, 4, 4, 4, 4,
				};

				var layerPolygons = TestUtilities.GetAllExtrusionPolygons(loadedGCode);

				Assert.AreEqual(45, layerPolygons.Where(i => i.Count > 2).Count());
				for (int i = 1; i < layers; i++)
				{
					Assert.AreEqual(expectedIslands[i], layerPolygons[i].Where(p => p.Count > 2).Count());
				}
			}

			Test(false, false);
			Test(false, true);
			Test(true, false);
			Test(true, true);
		}
	}
}