/*
Copyright (c) 2021, Lars Brubaker
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
using System.IO;
using System.Linq;
using System.Threading;
using MatterHackers.QuadTree;
using MSClipperLib;
using NUnit.Framework;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
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
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gCodeWithIslands);
				int numLayers = TestUtilities.LayerCount(gcodeContents);
				for (int i = 1; i < numLayers - 1; i++)
				{
					string[] layer = TestUtilities.GetLayer(gcodeContents, i);
					int totalRetractions = TestUtilities.CountRetractions(layer);
					if (i == 6)
					{
						// on this single layer there are 6 retractions
						Assert.IsTrue(totalRetractions == 6);
					}
					else
					{
						Assert.IsTrue(totalRetractions == 4);
					}
				}
			}
		}

		[Test]
		public void AllPerimetersGoInPolgonDirection()
		{
			var loadedGCode = SliceMeshWithProfile("ThinWallsRect", out _);

			int layerCount = TestUtilities.LayerCount(loadedGCode);

			var layerPolygons = loadedGCode.GetAllLayersExtrusionPolygons();

			for (int i = 2; i < layerCount - 2; i++)
			{
				Assert.LessOrEqual(layerPolygons[i].Count, 4, "We should not have added more than the 4 sides");
				foreach (var polygon in layerPolygons[i])
				{
					Assert.AreEqual(1, polygon.GetWindingDirection());
				}
			}
		}

		[Test]
		public void AllPerimetersGoCCW()
		{
			string badWindingStl = TestUtilities.GetStlPath("merg_winding_cw");
			string badWindingGCode = TestUtilities.GetTempGCodePath("merg_winding_cw.gcode");
			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "merg_winding_cw.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(badWindingGCode);
				processor.LoadStlFile(badWindingStl);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(badWindingGCode);
				int layerCount = TestUtilities.LayerCount(loadedGCode);

				var layerPolygons = loadedGCode.GetAllLayersExtrusionPolygons();

				for (int i = 0; i < layerCount - 1; i++)
				{
					var loops = layerPolygons[i].Where(p => p.Count > 5);
					// Assert.LessOrEqual(loops.Count(), 2, "We should always only have 2 loops");
					foreach (var polygon in loops)
					{
						Assert.AreEqual(1, polygon.GetWindingDirection());
					}
				}
			}
		}

		private string[] SliceMeshWithProfile(string rootName, out ConfigSettings config, string overrideSTL = null, Action<ConfigSettings> overrideSettings = null)
		{
			string stlFileName = TestUtilities.GetStlPath(string.IsNullOrEmpty(overrideSTL) ? rootName : overrideSTL);
			string gcodeFileName = TestUtilities.GetTempGCodePath(rootName + ".gcode");
			// load a model that is correctly manifold
			config = new ConfigSettings();
			string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", rootName + ".ini");
			config.ReadSettings(settingsPath);
			overrideSettings?.Invoke(config);

			// and clear the settings that we are overriding
			config.BooleanOperations = "";
			config.AdditionalArgsToProcess = "";

			var processor = new FffProcessor(config);
			processor.SetTargetFile(gcodeFileName);
			processor.LoadStlFile(stlFileName);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			return TestUtilities.LoadGCodeFile(gcodeFileName);
		}

		[Test]
		public void MonotonicOnlyInIsland()
		{
			var loadedGCode = SliceMeshWithProfile("monotonic_bad_extrusion", out _);

			int layerCount = TestUtilities.LayerCount(loadedGCode);

			var layerPolygons = loadedGCode.GetAllLayersExtrusionPolygons();

			for (int i = 1; i < layerCount - 1; i++)
			{
				var infillLines = layerPolygons[i].Where(p => p.Count == 2);
				// Assert.LessOrEqual(loops.Count(), 2, "We should always only have 2 loops");
				foreach (var infillLine in infillLines)
				{
					Assert.LessOrEqual(infillLine.PolygonLength(), 30000);
				}
			}
		}

		[Test]
		public void SupportConnectedOptimaly()
		{
			GCodeExport.CheckForZeroPositions = false;
			var loadedGCode = SliceMeshWithProfile("two disks", out _);
			int layerCount = TestUtilities.LayerCount(loadedGCode);

			for (int i = 0; i < layerCount; i++)
			{
				var movements = loadedGCode.GetLayer(i).GetLayerMovements().ToList();

				int longMoveCount = 0;
				for (var j = 1; j < movements.Count - 2; j++)
				{
					var start = movements[j - 1];
					var end = movements[j];
					if (end.extrusion - start.extrusion == 0
						&& (end.position - start.position).Length > 5)
					{
						longMoveCount++;
					}
				}

				Assert.LessOrEqual(longMoveCount, 8);
			}
		}

		[Test]
		public void CreatingInfill()
		{
			var loadedGCode = SliceMeshWithProfile("has_infill", out _);

			for (int i = 0; i < 100; i++)
			{
				var movements = loadedGCode.GetLayer(i).GetLayerMovements().ToList();
				Assert.GreaterOrEqual(movements.Count, 100, $"Layer {i} should have more than 100 extrusions.");
			}
		}

		[Test]
		public void ThinRingHasNoCrossingSegments()
		{
			var loadedGCode = SliceMeshWithProfile("thin_ring", out ConfigSettings config);

			double LongestMove(Polygons polys)
			{
				double longest = 0;
				for (int i = 0; i < polys.Count - 1; i++)
				{
					var poly = polys[i];
					var next = polys[i + 1];
					var length = (poly[poly.Count - 1] - next[0]).Length();
					longest = Math.Max(longest, length);
				}

				return longest;
			}

			var layers = loadedGCode.GetAllLayersExtrusionPolygons();
			for (int i = 0; i < 15; i++)
			{
				if (i == 0)
				{
					// on the first layer we are looking for a single move that is the right length from the skirt to the part
					var longest = LongestMove(layers[i]);
					Assert.AreEqual(config.SkirtDistance_um + config.ExtrusionWidth_um, longest, 500, "The skirt must be the correct distance from the outside of the part");
				}
				else // check that there are no 
				{
					var longest = LongestMove(layers[i]);
					Assert.Less(longest, 3000, $"Segment length was: {longest}, should be smaller.");
				}
			}
		}

		[Test, Ignore("WIP")]
		public void ThinFeaturesFillAndCenter()
		{
			var loadedGCode = SliceMeshWithProfile("monotonic_bad_extrusion", out _);

			var layers = loadedGCode.GetAllLayersExtrusionPolygons();
			for (int i = 1; i < 163; i++)
			{
				var layer = layers[i];
				Assert.AreEqual(1, layer.Count, "There is one polygon on every layer (up to 163)");
				foreach (var poly in layer)
				{
					var cleaned = Clipper.CleanPolygons(new Polygons() { poly }, 10);
					Assert.AreEqual(2, poly.Count, "Each polygon should only be a line");
					Assert.AreEqual(0, poly[0].X, "The points should be centered on 0");
					Assert.AreEqual(0, poly[1].X, "The points should be centered on 0");
				}
			}
		}

		private static void AllLayersHaveSinglExtrusionLine(string[] loadedGCode, int start)
		{
			var layers = loadedGCode.GetAllLayersExtrusionPolygons();
			for (int i = start; i < layers.Count; i++)
			{
				var layer = layers[i];
				Assert.AreEqual(1, layer.Count, "Three should only be one polygon per layer");
				var poly = layer[0];
				Assert.AreNotEqual(poly[0], poly[poly.Count - 1], "The polygon should not wrap around (it is a line).");
			}
		}

		[Test, Ignore("WIP")]
		public void ThinGapsOnRosePetal()
		{
			var loadedGCode = SliceMeshWithProfile("monotonic_bad_extrusion", out _);

			AllLayersHaveSinglExtrusionLine(loadedGCode, 3);
		}

		[Test, Ignore("WIP")]
		public void LoopsOnRosePetal()
		{
			var loadedGCode = SliceMeshWithProfile("monotonic_bad_extrusion", out _);

			AllLayersHaveSinglExtrusionLine(loadedGCode, 5);
		}

		void LoopsAreTouching(Polygons polygons)
		{
			for (int i = 0; i < polygons.Count; i++)
			{
				var startLoop = polygons[i];
				if (startLoop.Count > 2)
				{
					var startPoint = startLoop[0];
					var foundMachingLoop = false;
					for (int j = 0; j < polygons.Count; j++)
					{
						var checkLoop = polygons[j];
						if (j != i
							&& checkLoop.Count > 2)
						{
							var distToCheckStart = (startPoint - checkLoop[0]).Length();
							var distToCheckEnd = (startPoint - checkLoop[checkLoop.Count - 1]).Length();
							if (distToCheckStart < 2000
								|| distToCheckEnd < 2000)
							{
								foundMachingLoop = true;
								break;
							}
						}
					}

					Assert.IsTrue(foundMachingLoop);
				}
			}
		}

		[Test]
		public void RingLoopsSeamAligned()
		{
			var loadedGCode = SliceMeshWithProfile("ring_loops", out _);
			var extrusionLayers = TestUtilities.GetAllLayersExtrusionPolygons(loadedGCode);
			for (int i = 0; i < extrusionLayers.Count; i++)
			{
				var extrusions = extrusionLayers[i];
				Assert.LessOrEqual(extrusions.Count, 6);

				LoopsAreTouching(extrusions);
			}

			var movementLayers = TestUtilities.GetAllLayersMovements(loadedGCode);
			for (int i = 2; i < 25; i++)
			{
				foreach (var movement in movementLayers[i])
				{
					Assert.AreEqual((i + 1) * .25, movement.position.z);
				}
			}
		}

		[Test, Ignore("WorkIProgress")]
		public void PerimetersOutsideToIn()
		{
			var loadedGCode = SliceMeshWithProfile("perimeters_outside_in", out _);
			var extrusionLayers = TestUtilities.GetAllLayersExtrusionPolygons(loadedGCode);
			for (int layerIndex = 0; layerIndex < extrusionLayers.Count; layerIndex++)
			{
				var extrusions = extrusionLayers[layerIndex];
				// three should be 3 loops per layer
				Assert.AreEqual(3, extrusions.Count);

				// make sure every perimeter in inside the previous one (printing outside in)
				for (int perimeterIndex = 1; perimeterIndex < 3; perimeterIndex++)
				{
					var distanceToPrev = extrusions[perimeterIndex - 1][0].Length();
					var distanceToCurrent = extrusions[perimeterIndex][0].Length();
					Assert.Greater(distanceToPrev, distanceToCurrent, $"Perimeter {perimeterIndex} shoud be insed perimeter {perimeterIndex-1}, loops need to be in order");
				}
			}
		}

		[Test]
		public void FirstLayerIsFirstLayerSpeed()
		{
			var loadedGCode = SliceMeshWithProfile("monotonic_bad_extrusion", out ConfigSettings config, "PerimeterLoops");
			var layers = loadedGCode.GetAllLayers();
			var layerMovements = TestUtilities.GetLayerMovements(layers[0]);
			var lastPosition = new MovementInfo();
			var layerPolygons = TestUtilities.GetLayerPolygons(layerMovements, ref lastPosition);

			foreach (var polygon in layerPolygons)
			{
				if (polygon.type == TestUtilities.PolygonTypes.Extrusion)
				{
					foreach (var point in polygon.polygon)
					{
						Assert.AreEqual(config.FirstLayerSpeed * 60, point.Speed);
					}
				}
			}
		}

		[Test]
		public void ThinRingHasNoCrossingSegments2()
		{
			var loadedGCode = SliceMeshWithProfile("thin_gap_fill_ring", out _);

			double LongestMove(Polygons polys)
			{
				double longest = 0;
				var last = polys[0][0];
				foreach (var poly in polys)
				{
					for (int j = 0; j < poly.Count; j++)
					{
						var length = (poly[j] - last).Length();
						if (length > 3000)
						{
							int a = 0;
						}
						longest = Math.Max(longest, length);
						last = poly[j];
					}
				}

				return longest;
			}

			var layers = loadedGCode.GetAllLayersExtrusionPolygons();
			// start at 6 to skip the bottom layers (only care about the ring)
			for (int i = 6; i < layers.Count - 1; i++)
			{
				var longest = LongestMove(layers[i]);
				Assert.Less(longest, 3000, $"Segment length was: {longest}, should be smaller.");
			}
		}

		[Test]
		public void SupportTowerHasCorrectRetractions()
		{
			string infillSTLA = TestUtilities.GetStlPath("dice_body");
			string infillSTLB = TestUtilities.GetStlPath("dice_numbers");
			string infillGCode = TestUtilities.GetTempGCodePath("dice_dual.gcode");
			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "thin_ring.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTLA);
				processor.LoadStlFile(infillSTLB);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

				Assert.IsTrue(loadedGCode.Contains("T1 ; switch extruder"));
				Assert.IsTrue(loadedGCode.Contains("T0 ; switch extruder"));

				var layers = loadedGCode.GetAllLayersExtrusionPolygons();
				for (int i = 0; i < layers.Count; i++)
				{
					var polys = layers[i];
					foreach (var poly in polys)
					{
						for (int j = 0; j < poly.Count - 1; j++)
						{
							int a = 0;
						}
					}
				}
			}
		}

		[Test]
		public void NoLayerChangeRetractions()
		{
			string infillSTL = TestUtilities.GetStlPath("no_layer_change_retractions");
			string infillGCode = TestUtilities.GetTempGCodePath("no_layer_change_retractions.gcode");
			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "no_retractions_config.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(infillGCode);

				int numLayers = TestUtilities.LayerCount(gcodeContents);
				for (int i = 6; i < numLayers - 2; i++)
				{
					string[] layer = TestUtilities.GetLayer(gcodeContents, i);
					int totalRetractions = TestUtilities.CountRetractions(layer);
					Assert.IsTrue(totalRetractions == 0);
				}
			}
		}

		[Test]
		public void AvoidCrossingWithWhenSupportsCreated()
		{
			// validate the function
			Assert.AreEqual(5, new Vector3(0, 0, 0).DistanceToSegment(new Vector3(5, 0, 0), new Vector3(25, 0, 0)));
			Assert.AreEqual(5, new Vector3(30, 0, 0).DistanceToSegment(new Vector3(5, 0, 0), new Vector3(25, 0, 0)));
			Assert.AreEqual(5, new Vector3(15, 5, 0).DistanceToSegment(new Vector3(5, 0, 0), new Vector3(25, 0, 0)));
			Assert.AreEqual(5, new Vector3(17, -5, 0).DistanceToSegment(new Vector3(5, 0, 0), new Vector3(25, 0, 0)));

			string infillSTL = TestUtilities.GetStlPath("Avoid Crossing With Support");
			string infillGCode = TestUtilities.GetTempGCodePath("Avoid Crossing With Support.gcode");
			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "Avoid Crossing With Support.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(infillGCode);

				var layers = gcodeContents.GetAllTravelPolygons();
				var checkCenter = new Vector3(100, 100, 0);
				for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
				{
					var layer = layers[layerIndex];
					for (int polygonIndex = 0; polygonIndex < layer.Count; polygonIndex++)
					{
						var polygon = layer[polygonIndex];
						for (int i = 0; i < polygon.Count - 1; i++)
						{
							var start = new Vector3(polygon[i]) / 1000.0;
							var end = new Vector3(polygon[i + 1]) / 1000.0;
							var distFromLine = checkCenter.DistanceToSegment(start, end);
							// assert that no line gets closer than 5mm to 100,100 (this is a hole and should be avoided)
							Assert.Greater(distFromLine, 5);
						}
					}
				}
			}
		}

		[Test]
		public void CheckForExcesiveTravels()
		{
			var loadedGCode = SliceMeshWithProfile("bad_travel", out _);

			// the radius of the loop we are planning around
			// var stlRadius = 127;
			var layers = loadedGCode.GetAllTravelPolygons();
			for (int i = 0; i < layers.Count; i++)
			{
				var polys = layers[i];
				// skip the first move (the one getting to the part)
				for (int j = 1; j < polys.Count; j++)
				{
					var poly = polys[j];
					var startToEnd = (poly[poly.Count - 1] - poly[0]).Length();
					var length = poly.PolygonLength();
					var ratio = length / (double)startToEnd;
					Assert.Less(ratio, 4, $"No travel should be more than 2x the direct distance, was: {ratio}");
				}
			}
		}

		private void PointsAtHeight(Polygon poly, double height)
		{
			foreach (var point in poly)
			{
				Assert.AreEqual(height, point.Z, "All heights should be at the expected height");
			}
		}

		[Test]
		public void CheckForCorrectSupportOffsetMonotonic()
		{
			GCodeExport.CheckForZeroPositions = false;
			var loadedGCode = SliceMeshWithProfile("bad_support", out _);

			// We had a bug with this profile that made infill not at the air gap height
			// This test proves that the infill is at the air gap height.
			// Specifically that no material is below the air gap height on the first part layer
			var layersTravels = loadedGCode.GetAllTravelPolygons();
			var layersExtrusions = loadedGCode.GetAllLayersExtrusionPolygons();

			// assert all extrusion at correct height for last support layer
			for (int i = 0; i < 22; i++)
			{
				var layerPolygons = layersExtrusions[i];
				// assert there is a perimeter
				Assert.IsTrue(layerPolygons.Where(e => e.Count > 3).Any(), "There is a perimeter");

				for (int j = 0; j < layerPolygons.Count; j++)
				{
					PointsAtHeight(layerPolygons[j], 250 + 250 * i);
				}
			}


			// assert all extrusion at correct height for first bottom layer
			foreach (var poly in layersExtrusions[22])
			{
				PointsAtHeight(poly, 6350);
			}

			// assert all travels at correct height
			var polysAboveLayer = 0;
			foreach (var poly in layersTravels[21])
			{
				foreach (var point in poly)
				{
					if (point.Z > 5500)
					{
						polysAboveLayer++;
					}
				}
			}

			Assert.Less(polysAboveLayer, 5, "There should be very few z-hop travels");
		}

		void ValidatePolygons(Polygons polygons, Polygons expectedPolygons)
		{
			Assert.AreEqual(expectedPolygons.Count, polygons.Count);

			if (expectedPolygons != null)
			{
				for (int i = 0; i < expectedPolygons.Count; i++)
				{
					for (int j = 0; j < expectedPolygons[i].Count; j++)
					{
						Assert.AreEqual(expectedPolygons[i][j], polygons[i][j]);
					}
				}
			}
		}

		void Validate(string[] gcode, int expectedPoints, Polygons expectedExtrusions, Polygons expectedTravels)
		{
			var movements = TestUtilities.GetLayerMovements(gcode, default(MovementInfo)).ToList();
			Assert.AreEqual(expectedPoints, movements.Count);

			{
				var movementInfo = default(MovementInfo);
				var extrusions = TestUtilities.GetExtrusionPolygonsForLayer(gcode, ref movementInfo);
				if (expectedExtrusions == null)
				{
					Assert.AreEqual(0, extrusions.Count);
				}
				else
				{
					ValidatePolygons(extrusions, expectedExtrusions);
				}
			}

			{
				var movementInfo = default(MovementInfo); // reset the movement info
				var travels = TestUtilities.GetTravelPolygonsForLayer(gcode, ref movementInfo);

				if (expectedTravels == null)
				{
					Assert.AreEqual(0, travels.Count);
				}
				else
				{
					ValidatePolygons(travels, expectedTravels);
				}
			}
		}

		[Test]
		public void LayerPolygonsParsedCorrectly()
		{
			// a single extrusion
			Validate(new string[] { "G1 X0Y0Z0", "G1 X1Y0Z0E1" },
				2,
				new Polygons { new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) } },
				null);

			// a single travel
			Validate(new string[] { "G1 X0Y0Z0", "G1 X1Y0Z0" },
				2,
				null,
				new Polygons { new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) } });

			// a travel then an extrusion
			Validate(new string[] { "G1 X0Y0Z0", "G1 X1Y0Z0", "G1 X2Y0Z0E1" },
				3,
				new Polygons { new Polygon() { new IntPoint(1000, 0, 0), new IntPoint(2000, 0, 0) } },
				new Polygons { new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) } });

			// an extrusion then a travel
			Validate(new string[] { "G1 X0Y0Z0", "G1 X1Y0Z0E1", "G1 X2Y0Z0" },
				3,
				new Polygons { new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) } },
				new Polygons { new Polygon() { new IntPoint(1000, 0, 0), new IntPoint(2000, 0, 0) } });

			// a travel then an extrusion then a travel
			Validate(new string[] { "G1 X0Y0Z0", "G1 X1Y0Z0", "G1 X2Y0Z0E1", "G1 X3Y0Z0" },
				4,
				new Polygons
				{
					new Polygon() { new IntPoint(1000, 0, 0), new IntPoint(2000, 0, 0) }
				},
				new Polygons
				{
					new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) },
					new Polygon() { new IntPoint(2000, 0, 0), new IntPoint(3000, 0, 0) },
				});

			// an extrusion then a travel then and extrusion
			Validate(new string[] { "G1 X0Y0Z0", "G1 X1Y0Z0E1", "G1 X2Y0Z0", "G1 X3Y0Z0E2" },
				4,
				new Polygons
				{
					new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) },
					new Polygon() { new IntPoint(2000, 0, 0), new IntPoint(3000, 0, 0) }
				},
				new Polygons
				{
					new Polygon() { new IntPoint(1000, 0, 0), new IntPoint(2000, 0, 0) }
				});

			// a single extrusion then a retraction
			Validate(new string[] { "G1 X0Y0Z0", "G1 X1Y0Z0E1", "G1 X1Y0Z0E2" },
				3,
				new Polygons { new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) } },
				null);

			// a retraction than an extrusion then a retraction
			Validate(new string[] { "G1 X0Y0Z0", "G1 X0Y0Z0E1", "G1 X1Y0Z0E2", "G1 X1Y0Z0E3" },
				4,
				new Polygons { new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 0, 0) } },
				null);

			// a single loop
			Validate(new string[]
			{
				// loop
				"G1 X10Y0Z0E2", "G1 X5Y10Z0E3", "G1 X0Y0Z0E4",
			},
			3,
			new Polygons
			{
				new Polygon() { new IntPoint(0, 0, 0), new IntPoint(10000, 0, 0), new IntPoint(5000, 10000, 0), new IntPoint(0, 0, 0) },
			},
			null);

			// a series of loops
			Validate(new string[]
			{
				// first loop
				"G1 X10Y0Z0E2", "G1 X5Y10Z0E3", "G1 X0Y0Z0E4",
				// travel
				"G1 X1Y1Z0",
				// second loop
				"G1 X9Y1Z0E5", "G1 X5Y9Z0E6", "G1 X1Y1Z0E7",
				// travel
				"G1 X2Y2Z0",
				// third loop
				"G1 X8Y2Z0E5", "G1 X5Y8Z0E6", "G1 X2Y2Z0E7",
			},
			11,
			new Polygons
			{
				new Polygon() { new IntPoint(0, 0, 0), new IntPoint(10000, 0, 0), new IntPoint(5000, 10000, 0), new IntPoint(0, 0, 0) },
				new Polygon() { new IntPoint(1000, 1000, 0), new IntPoint(9000, 1000, 0), new IntPoint(5000, 9000, 0), new IntPoint(1000, 1000, 0) },
				new Polygon() { new IntPoint(2000, 2000, 0), new IntPoint(8000, 2000, 0), new IntPoint(5000, 8000, 0), new IntPoint(2000, 2000, 0) },
			},
			new Polygons
			{
				new Polygon() { new IntPoint(0, 0, 0), new IntPoint(1000, 1000, 0) },
				new Polygon() { new IntPoint(1000, 1000, 0), new IntPoint(2000, 2000, 0) }
			});

			// check that we get the rigt number of extrusion moves
			{
				string pathToData = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "three_extrusion_loops.gcode");
				string[] gcode = File.ReadAllLines(pathToData);
				var movementInfo = default(MovementInfo);
				var extrusions = TestUtilities.GetExtrusionPolygonsForLayer(gcode, ref movementInfo);

				Assert.AreEqual(3, extrusions.Count);
				Assert.IsTrue(SegmentLongerThan(extrusions[0], 20000));
				Assert.AreEqual(13, extrusions[0].Count);
				Assert.AreEqual(14, extrusions[1].Count);
				Assert.AreEqual(14, extrusions[2].Count);
			}
		}

		bool SegmentLongerThan(Polygon polygon, double length)
        {
			// check that all the segments are shorter than 20mm
			for (int j = 1; j < polygon.Count; j++)
			{
				var segmentLength = (polygon[j] - polygon[j - 1]).Length();
				if(segmentLength > length)
                {
					return true;
                }
			}

			return false;
		}


		[Test]
		public void CheckForCorrectZGapHeight()
		{
			var loadedGCode = SliceMeshWithProfile("bad_zgap_layer", out _);
			var layersExtrusions = loadedGCode.GetAllLayersExtrusionPolygons();

			// layer 4 is the z-gap layer
			var movementInfo = default(MovementInfo);
			var layer4 = loadedGCode.GetLayer(4);
			var zGapLayer = TestUtilities.GetExtrusionPolygonsForLayer(layer4, ref movementInfo, false);

			var lengths = zGapLayer.Where(i => i[0].Z == 1450).Select(i => i.PolygonLength());

			var foundAirGap = false;
			for(int i=0; i < zGapLayer.Count; i++)
            {
				var polygon = zGapLayer[i];
				
				// find all polygons that are at the air height
				if (polygon[0].Z == 1450)
				{
					foundAirGap = true;

					Assert.IsFalse(SegmentLongerThan(polygon, 20000));
				}
			}

			Assert.IsTrue(foundAirGap, "There must be some bottom air-gap layer polygons");
		}

		[Test]
		public void CheckForCorrectSupportOffsetNormal()
		{
			GCodeExport.CheckForZeroPositions = false;
			// turn off monotonic infill
			var loadedGCode = SliceMeshWithProfile("bad_support", out _, null, (settings) =>
            {
				settings.MonotonicSolidInfill = false;
            });

			// We had a bug with this profile that made infill not at the air gap height
			// This test proves that the infill is at the air gap height.
			// Specifically that no material is below the air gap height on the first part layer
			var layersTravels = loadedGCode.GetAllTravelPolygons();
			var layersExtrusions = loadedGCode.GetAllLayersExtrusionPolygons();

			// assert there is a perimeter
			Assert.IsTrue(layersExtrusions[21].Where(e => e.Count > 3).Any(), "There is a perimeter");

			// assert all extrusion at correct height for last support layer
			foreach (var poly in layersExtrusions[21])
			{
				PointsAtHeight(poly, 5500);
			}

			// assert all extrusion at correct height for first bottom layer
			foreach (var poly in layersExtrusions[22])
			{
				PointsAtHeight(poly, 6350);
			}

			// assert all travels at correct height
			var polysAboveLayer = 0;
			foreach (var poly in layersTravels[21])
			{
				foreach (var point in poly)
				{
					if (point.Z > 5500)
					{
						polysAboveLayer++;
					}
				}
			}

			Assert.Less(polysAboveLayer, 5, "There should be very few z-hop travels");
		}

		[Test]
		public void ValidatePerimetersOrderAndLiftOnTopLayer()
		{
			string twoHoleSTL = TestUtilities.GetStlPath("AvoidCrossing2Holes");
			string validatePerimetersGCode = TestUtilities.GetTempGCodePath("validate_perimeters_and_top_layer.gcode");
			{
				// load a model that is (or was) having many erroneous travels
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "validate_perimeters_and_top_layer.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(validatePerimetersGCode);
				processor.LoadStlFile(twoHoleSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(validatePerimetersGCode);

				var layers = loadedGCode.GetAllLayers();

				// validate that all perimeters render as groups
				for (int i = 1; i < layers.Count - 1; i++)
				{
					var extrusions = TestUtilities.GetExtrusionPolygonsForLayer(layers[i]);
					// remove retractions
					extrusions.RemoveSmallAreas(1);
					// expected number of loops
					Assert.AreEqual(9, extrusions.Count);

					// check that each set of polygons are part of the same perimeter group
					Assert.IsTrue(extrusions[0].Count == extrusions[1].Count && extrusions[1].Count == extrusions[2].Count);
					Assert.IsTrue(extrusions[3].Count == extrusions[4].Count && extrusions[4].Count == extrusions[5].Count);
					Assert.IsTrue(Math.Abs(extrusions[6].Count - extrusions[7].Count) <= 1 && Math.Abs(extrusions[7].Count  - extrusions[8].Count) <= 1);
				}

				// validate that the top layer has no moves that are long that don't retract
				{
					var topLayerIndex = layers.Count - 1;
					var topLayer = loadedGCode.GetLayer(topLayerIndex);

					var topMovements = topLayer.GetLayerMovements().ToList();
					var lastMovement = default(MovementInfo);
					var topTravels = topLayer.GetTravelPolygonsForLayer(ref lastMovement);
					for (int i=1; i < topTravels.Count; i++)
					{
						var travel = topTravels[i];
						// if we go more than 2 mm
						if (travel.PolygonLength() > 3000)
						{
							// we are going to move a lot, we need to retract
							var hadRetraction = false;
							for (int j = i - 1; j > Math.Max(0, i - 7); j++)
							{
								hadRetraction |= topMovements[j].line.IsRetraction();
								if (hadRetraction)
								{
									break;
								}
							}
							Assert.IsTrue(hadRetraction);
						}
					}
				}
			}
		}

		[Test]
		public void EliminateDoublePerimeter()
		{
			string twoHoleSTL = TestUtilities.GetStlPath("double_perimeter_error");
			string validatePerimetersGCode = TestUtilities.GetTempGCodePath("double_perimeter_error.gcode");
			{
				// load a model that is (or was) having many erroneous travels
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "double_perimeter_error.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(validatePerimetersGCode);
				processor.LoadStlFile(twoHoleSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(validatePerimetersGCode);

				var layers = loadedGCode.GetAllLayers();

				// validate that all perimeters render as groups
				for (int i = 1; i < layers.Count; i++)
				{
					// this will check for overlapping segments
					TestUtilities.GetExtrusionPolygonsForLayer(layers[i]);
				}
			}
		}

		[Test]
		public void MaintainPerimeterCount()
		{
			// the error this was showing was that the perimeter count did not stay at 3 for the left bottom part of the model when merge overlapping lines is turned on

			// first prove that we get the right result with merge overlapping lines turned off
			{
				string twoHoleSTL = TestUtilities.GetStlPath("bad_perimeter_count");
				string validatePerimetersGCode = TestUtilities.GetTempGCodePath("bad_perimeter_count_1.gcode");
				{
					// load a model that is (or was) having the wrong number of perimeters on part of the layers
					var config = new ConfigSettings();
					string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "bad_perimeter_count.ini");
					config.ReadSettings(settingsPath);
					Assert.IsTrue(config.MergeOverlappingLines);
					config.MergeOverlappingLines = false;
					var processor = new FffProcessor(config);
					processor.SetTargetFile(validatePerimetersGCode);
					processor.LoadStlFile(twoHoleSTL);
					// slice and save it
					processor.DoProcessing();
					processor.Dispose();

					string[] loadedGCode = TestUtilities.LoadGCodeFile(validatePerimetersGCode);

					var layers = loadedGCode.GetAllLayers();

					// validate that all perimeters render as groups
					for (int i = 0; i < layers.Count - 1; i++)
					{
						// this will check for overlapping segments
						var perimeters = TestUtilities.GetExtrusionPolygonsForLayer(layers[i]);
						Assert.AreEqual(4, perimeters.Count, "There should be 4 perimeters on every level");
					}
				}
			}

			// than that we get the same result with it turned on
			{
				string twoHoleSTL = TestUtilities.GetStlPath("bad_perimeter_count");
				string validatePerimetersGCode = TestUtilities.GetTempGCodePath("bad_perimeter_count_2.gcode");
				{
					// load a model that is (or was) having the wrong number of perimeters on part of the layers
					var config = new ConfigSettings();
					string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "bad_perimeter_count.ini");
					config.ReadSettings(settingsPath);
					Assert.IsTrue(config.MergeOverlappingLines);
					var processor = new FffProcessor(config);
					processor.SetTargetFile(validatePerimetersGCode);
					processor.LoadStlFile(twoHoleSTL);
					// slice and save it
					processor.DoProcessing();
					processor.Dispose();

					string[] loadedGCode = TestUtilities.LoadGCodeFile(validatePerimetersGCode);

					var layers = loadedGCode.GetAllLayers();

					// validate that all perimeters render as groups
					for (int i = 0; i < layers.Count - 1; i++)
					{
						// this will check for overlapping segments
						var perimeters = TestUtilities.GetExtrusionPolygonsForLayer(layers[i]);
						Assert.AreEqual(4, perimeters.Count, "There should be 4 perimeters on every level");
					}
				}
			}
		}

		[Test]
		public void MonotonicHasMinimalTravels()
		{
			// when monotonic is on for this part there were too many travels
			var loadedGCode = SliceMeshWithProfile("bad_monotonic", out _);

			var layers = loadedGCode.GetAllLayers();

			// validate that all layers have limited travels
			for (int i = 0; i < layers.Count - 1; i++)
			{
				// this will check for overlapping segments
				var movementInfo = new MovementInfo();
				var travels = TestUtilities.GetTravelPolygonsForLayer(layers[i], ref movementInfo);
				var longTravels = 0;
				foreach(var travel in travels)
                {
					if (travel.PolygonLength() > 3000)
                    {
						longTravels++;
                    }
                }
				Assert.LessOrEqual(longTravels, 10, "There should be less than 10 travels on every level");
			}
		}

		[Test]
		public void MonotonicHasMinimalTravels2()
		{
			// when monotonic is on for this part there were too many travels
			var loadedGCode = SliceMeshWithProfile("bad_monotonic2", out _);

			var layers = loadedGCode.GetAllLayers();

			// validate that all layers have limited travels
			for (int i = 0; i < layers.Count - 1; i++)
			{
				// this will check for overlapping segments
				var movementInfo = new MovementInfo();
				var travels = TestUtilities.GetTravelPolygonsForLayer(layers[i], ref movementInfo);
				var longTravels = 0;
				foreach (var travel in travels)
				{
					if (travel.PolygonLength() > 10000)
					{
						longTravels++;
					}
				}
				Assert.LessOrEqual(longTravels, 20, "There should be less than 10 travels on every level");
			}
		}

		[Test]
		public void ParseLayerPolygonsCorrectly()
		{
			var gcode = @"; Layer Change GCode
; LAYER:11
; LAYER_HEIGHT:0.25
; TYPE:FILL
G0 F12000 X5.678 Y6.736 Z3
; TYPE:WALL-OUTER
G1 F1200 X-13.92 Y6.736 E105.83318
G1 X-13.92 Y-12.862 E106.66692
G1 X5.678 Y-12.862 E107.50066
G1 X5.678 Y6.336 E108.31738
G0 X5.678 Y6.736
G0 F12000 X5.278 Y6.336
; TYPE:WALL-INNER
G1 F1200 X-13.52 Y6.336 E109.11708
G1 X-13.52 Y-12.462 E109.91679
G1 X5.278 Y-12.462 E110.71649
G1 X5.278 Y5.936 E111.49918
G0 X5.278 Y6.336
G0 F12000 X4.878 Y5.936
G1 F1200 X-13.12 Y5.936 E112.26485
G1 X-13.12 Y-12.062 E113.03052
G1 X4.878 Y-12.062 E113.79619
G1 X4.878 Y5.536 E114.54485
G0 X4.878 Y5.936
";

			var layerMovements = TestUtilities.GetLayerMovements(gcode.Split('\n')).ToList();
			var lastPosition = new MovementInfo();
			var layerPolygons = TestUtilities.GetLayerPolygons(layerMovements, ref lastPosition);
			Assert.AreEqual(7, layerPolygons.Count);
			for (int i=1; i<layerPolygons.Count; i++)
			{
				var speed = 0L;
				var foundSpeed = false;
				// all points have same speed
				foreach(var point in layerPolygons[i].polygon)
				{
					if (speed == 0)
					{
						Assert.False(foundSpeed);
						speed = point.Speed;
						foundSpeed = true;
					}
					else
					{
						if (layerPolygons[i].type == TestUtilities.PolygonTypes.Extrusion)
						{
							Assert.AreEqual(speed, point.Speed);
						}
					}
				}

				Assert.True(foundSpeed);
			}
		}

		[Test]
		public void Perimeter0CloseTo1()
		{
			string stlPath = TestUtilities.GetStlPath("perimeter_0_close_to_1");
			string validateGCode = TestUtilities.GetTempGCodePath("perimeter_0_close_to_1.gcode");
			{
				// load a model that is (or was) having many erroneous travels
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "perimeter_0_close_to_1.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(validateGCode);
				processor.LoadStlFile(stlPath);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(validateGCode);

				var layers = loadedGCode.GetAllLayers();

				var lastPosition = new MovementInfo();
				var outerPerimeterSpeed = 1500;
				// validate that all perimeters render as groups
				for (int layerIndex = 1; layerIndex < layers.Count; layerIndex++)
				{
					// find each polygon that has a speed of outerPerimeterSpeed
					var layerMovements = TestUtilities.GetLayerMovements(layers[layerIndex]);
					var layerPolygons = TestUtilities.GetLayerPolygons(layerMovements, ref lastPosition);
					var outerPerimeterCount = 0;
					for(int polygonIndex = 0; polygonIndex < layerPolygons.Count; polygonIndex ++)
					{
						var polygon = layerPolygons[polygonIndex].polygon;
						if(polygon[polygon.Count - 1].Speed == outerPerimeterSpeed
							&& layerPolygons[polygonIndex].type == TestUtilities.PolygonTypes.Extrusion)
						{
							outerPerimeterCount++;
							// we only care about the retraction on the outer perimeter
							if (polygonIndex > layerPolygons.Count / 2)
							{
								// make sure the previous polygon is a travel
								Assert.AreEqual(TestUtilities.PolygonTypes.Travel, layerPolygons[polygonIndex - 1].type);
								// make sure the travel polygon to each is sorter than 3 mm
								Assert.Less(layerPolygons[polygonIndex - 1].polygon.PolygonLength(), 3200);
							}
						}
					}

					// make sure there are at least 2
					Assert.AreEqual(2, outerPerimeterCount);
				}
			}
		}

		[Test]
		public void CheckForMoveToOrigin()
		{
			LayerGCodePlanner.TestingDistanceFromOrigin = 1000;
			string testSTL = TestUtilities.GetStlPath("move_to_origin");
			string moveToOriginGCode = TestUtilities.GetTempGCodePath("move_to_origin.gcode");
			{
				// load a model that is (or was) having many erroneous travels
				var config = new ConfigSettings();
				string settingsPath = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "move_to_origin_settings.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(moveToOriginGCode);
				processor.LoadStlFile(testSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(moveToOriginGCode);

				var first = true;
				// the radius of the loop we ore planning around
				// var stlRadius = 127;
				var layers = loadedGCode.GetAllTravelPolygons();
				for (int i = 0; i < layers.Count; i++)
				{
					var polys = layers[i];
					foreach (var poly in polys)
					{
						foreach (var point in poly)
						{
							// skip the first move (the one getting to the part)
							if (!first)
							{
								Assert.Greater(point.X, 1000, $"No travel should have an X less than 1000 (1 mm), was: {point.X}");
								Assert.Greater(point.Y, 1000, $"No travel should have an Y less than 1000 (1 mm), was: {point.Y}");
							}

							first = false;
						}
					}
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
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.LayerCount(loadedGCode);
				Assert.AreEqual(50, layerCount);

				var layerPolygons = loadedGCode.GetAllLayersExtrusionPolygons();

				Assert.AreEqual(6, layerPolygons[10].Where(i => i.Count > 2).Count());
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
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.LayerCount(loadedGCode);
				Assert.AreEqual(50, layerCount);

				var layerPolygons = loadedGCode.GetAllLayersExtrusionPolygons();

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
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.LayerCount(loadedGCode);
				Assert.AreEqual(50, layerCount);

				var layerPolygons = loadedGCode.GetAllLayersExtrusionPolygons();

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
				string pathToData = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "CubeSegmentsX2.txt");

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
				string pathToData = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "TwoRingSegmentsTestData.txt");

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
				processor.Dispose();
			}

			{
				// load a model that has some faces pointing the wrong way
				var config = new ConfigSettings();
				var processor = new FffProcessor(config);
				processor.SetTargetFile(nonManifoldGCode);
				processor.LoadStlFile(nonManifoldFile);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();
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