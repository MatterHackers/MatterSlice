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
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.LayerCount(loadedGCode);

				var layerPolygons = loadedGCode.GetAllExtrusionPolygons();

				for (int i = 2; i < layerCount - 2; i++)
				{
					Assert.LessOrEqual(layerPolygons[i].Count, 4, "We should not have added more than the 4 sides");
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
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(thinWallsGCode);
				int layerCount = TestUtilities.LayerCount(loadedGCode);

				for (int i = 0; i < layerCount; i++)
				{
					var movements = loadedGCode.GetLayer(i).Movements().ToList();

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
		}

		[Test]
		public void CreatingInfill()
		{
			string infillSTL = TestUtilities.GetStlPath("has_infill");
			string infillGCode = TestUtilities.GetTempGCodePath("has_infill.gcode");
			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				config.ExpandThinWalls = true;
				config.NumberOfPerimeters = 1;
				config.AvoidCrossingPerimeters = false;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

				for (int i = 0; i < 100; i++)
				{
					var movements = loadedGCode.GetLayer(i).Movements().ToList();
					Assert.GreaterOrEqual(movements.Count, 100, $"Layer {i} should have more than 100 extrusions.");
				}
			}
		}

		[Test]
		public void ThinRingHasNoCrossingSegments()
		{
			string infillSTL = TestUtilities.GetStlPath("thin_ring");
			string infillGCode = TestUtilities.GetTempGCodePath("thin_ring.gcode");
			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "thin_ring_config.ini");
				config.ReadSettings(settingsPath);
				config.SkirtDistanceFromObject = 7.5;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

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

				var layers = loadedGCode.GetAllExtrusionPolygons();
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
		}

		[Test, Ignore("WIP")]
		public void ThinFeaturesFillAndCenter()
		{
			string infillSTL = TestUtilities.GetStlPath("thin_wall");
			string infillGCode = TestUtilities.GetTempGCodePath("thin_wall.gcode");
			{
				// load a model that was showing unwanted holes 
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "thin_wall.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

				var layers = loadedGCode.GetAllExtrusionPolygons();
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
		}

		private static void AllLayersHaveSinglExtrusionLine(string[] loadedGCode, int start)
		{
			var layers = loadedGCode.GetAllExtrusionPolygons();
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
			string infillSTL = TestUtilities.GetStlPath("petal_holes");
			string infillGCode = TestUtilities.GetTempGCodePath("petal_holes.gcode");
			{
				// load a model that was showing unwanted holes 
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "petal_holes.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

				AllLayersHaveSinglExtrusionLine(loadedGCode, 3);
			}
		}

		[Test, Ignore("WIP")]
		public void LoopsOnRosePetal()
		{
			string infillSTL = TestUtilities.GetStlPath("petal_loops");
			string infillGCode = TestUtilities.GetTempGCodePath("petal_loops.gcode");
			{
				// load a model that was showing unwanted holes 
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "petal_loops.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

				AllLayersHaveSinglExtrusionLine(loadedGCode, 5);
			}
		}

		[Test]
		public void RingLoopsSeamAligned()
		{
			string infillSTL = TestUtilities.GetStlPath("ring_loops");
			string infillGCode = TestUtilities.GetTempGCodePath("ring_loops.gcode");
			{
				// load a model that was showing unaligned perimeters
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "ring_loops.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

				var extrusionLayers = TestUtilities.GetAllExtrusionPolygons(loadedGCode);
				foreach(var extrusions in extrusionLayers)
				{
					Assert.AreEqual(4, extrusions.Count);
				}
			}
		}

		[Test]
		public void ThinRingHasNoCrossingSegments2()
		{
			string infillSTL = TestUtilities.GetStlPath("thin_gap_fill_ring");
			string infillGCode = TestUtilities.GetTempGCodePath("thin_gap_fill_ring.gcode");
			{
				// load a model that is correctly manifold
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "thin_gap_fill.ini");
				config.ReadSettings(settingsPath);
				config.SkirtDistanceFromObject = 7.5;
				var processor = new FffProcessor(config);
				processor.SetTargetFile(infillGCode);
				processor.LoadStlFile(infillSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(infillGCode);

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

				var layers = loadedGCode.GetAllExtrusionPolygons();
				// start at 6 to skip the bottom layers (only care about the ring)
				for (int i = 6; i < layers.Count - 1; i++)
				{
					var longest = LongestMove(layers[i]);
					Assert.Less(longest, 3000, $"Segment length was: {longest}, should be smaller.");
				}
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
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "thin_ring_config.ini");
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

				var layers = loadedGCode.GetAllExtrusionPolygons();
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
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "no_retractions_config.ini");
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
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "Avoid Crossing With Support.ini");
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
							var end = new Vector3(polygon[i+1]) / 1000.0;
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
			string badTravelSTL = TestUtilities.GetStlPath("bad_travel");
			string badTravelGCode = TestUtilities.GetTempGCodePath("bad_travel.gcode");
			{
				// load a model that is (or was) having many erroneous travels
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "bad_travel_settings.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(badTravelGCode);
				processor.LoadStlFile(badTravelSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(badTravelGCode);

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
		}

		[Test]
		public void ValidatePerimetersOrderAndLiftOnTopLayer()
		{
			string twoHoleSTL = TestUtilities.GetStlPath("AvoidCrossing2Holes");
			string validatePerimetersGCode = TestUtilities.GetTempGCodePath("validate_perimeters_and_top_layer.gcode");
			{
				// load a model that is (or was) having many erroneous travels
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "validate_perimeters_and_top_layer.ini");
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

					var topMovements = topLayer.Movements().ToList();
					var lastMovement = default(MovementInfo);
					var topTravels = topLayer.GetTravelPolygonsForLayer(ref lastMovement);
					foreach (var travel in topTravels)
					{
						// if we go more than 2 mm
						if (travel.PolygonLength() > 3000)
						{
							// find the instruction before
							var moveIndex = topMovements.FindMoveIndex(new Vector3(travel[0]));
							// we are going to move a lot, we need to retract
							var hadRetraction = false;
							for (int i = 1; i < 7; i++)
							{
								hadRetraction |= topMovements[moveIndex - i].line.IsRetraction();
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
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "double_perimeter_error.ini");
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
		public void CheckForMoveToOrigin()
		{
			LayerGCodePlanner.TestingDistanceFromOrigin = 1000;
			string testSTL = TestUtilities.GetStlPath("move_to_origin");
			string moveToOriginGCode = TestUtilities.GetTempGCodePath("move_to_origin.gcode");
			{
				// load a model that is (or was) having many erroneous travels
				var config = new ConfigSettings();
				string settingsPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "move_to_origin_settings.ini");
				config.ReadSettings(settingsPath);
				var processor = new FffProcessor(config);
				processor.SetTargetFile(moveToOriginGCode);
				processor.LoadStlFile(testSTL);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] loadedGCode = TestUtilities.LoadGCodeFile(moveToOriginGCode);

				// the radius of the loop we ore planning around
				// var stlRadius = 127;
				var layers = loadedGCode.GetAllTravelPolygons();
				for (int i = 0; i < layers.Count; i++)
				{
					var polys = layers[i];
					// skip the first move (the one getting to the part)
					foreach (var poly in polys)
					{
						foreach (var point in poly)
						{
							Assert.Greater(point.X, 1000, $"No travel should have an X less than 1000 (1 mm), was: {point.X}");
							Assert.Greater(point.Y, 1000, $"No travel should have an Y less than 1000 (1 mm), was: {point.Y}");
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

				var layerPolygons = loadedGCode.GetAllExtrusionPolygons();

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

				var layerPolygons = loadedGCode.GetAllExtrusionPolygons();

				Assert.AreEqual(10, layerPolygons[10].Count);
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

				var layerPolygons = loadedGCode.GetAllExtrusionPolygons();

				Assert.AreEqual(10, layerPolygons[10].Count);
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