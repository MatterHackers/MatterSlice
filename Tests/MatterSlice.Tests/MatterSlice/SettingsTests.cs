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
	using System;
	using Polygons = List<List<IntPoint>>;

	[TestFixture, Category("MatterSlice")]
	public class SliceSettingsTests
	{
		#region Inset order tests

		[Test]
		public void InnerPerimeterFirstCorrect()
		{
			// By default we need to do the inner perimeters first
			string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-perimeter.gcode");

			ConfigSettings config = new ConfigSettings();
			config.NumberOfPerimeters = 3;
			config.InfillPercent = 0;
			config.NumberOfTopLayers = 0;
			config.NumberOfBottomLayers = 0;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] gcode = TestUtlities.LoadGCodeFile(boxGCodeFile);

			MovementInfo movement = new MovementInfo();
			{
				// check layer 1
				string[] layer1Info = TestUtlities.GetGCodeForLayer(gcode, 1);
				Polygons layer1Polygons = TestUtlities.GetExtrusionPolygons(layer1Info, ref movement);
				// make sure there are 3
				Assert.IsTrue(layer1Polygons.Count == 3);
				// make sure they are in the right order (first layer is outside in)
				Assert.IsTrue(layer1Polygons[0].MinX() > layer1Polygons[1].MinX());
			}

			{
				// check layer 2
				string[] layer2Info = TestUtlities.GetGCodeForLayer(gcode, 2);
				Polygons layer2Polygons = TestUtlities.GetExtrusionPolygons(layer2Info, ref movement);

				// make sure there are 3
				Assert.IsTrue(layer2Polygons.Count == 3);
				// make sure they are in the right order (other layers are inside out)
				Assert.IsTrue(layer2Polygons[0].MinX() > layer2Polygons[1].MinX());
			}
		}

		[Test]
		public void OuterPerimeterFirstCorrect()
		{
			string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-perimeter.gcode");

			ConfigSettings config = new ConfigSettings();
			config.NumberOfPerimeters = 3;
			config.OutsidePerimetersFirst = true;
			config.InfillPercent = 0;
			config.NumberOfTopLayers = 0;
			config.NumberOfBottomLayers = 0;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] gcode = TestUtlities.LoadGCodeFile(boxGCodeFile);

			MovementInfo movement = new MovementInfo();
			{
				// check layer 1
				string[] layer1Info = TestUtlities.GetGCodeForLayer(gcode, 1);
				Polygons layer1Polygons = TestUtlities.GetExtrusionPolygons(layer1Info, ref movement);
				// make sure there are 3
				Assert.IsTrue(layer1Polygons.Count == 3);
				// make sure they are in the right order (first layer is outside in)
				Assert.IsTrue(layer1Polygons[0].MinX() < layer1Polygons[1].MinX());
			}

			{
				// check layer 2
				string[] layer2Info = TestUtlities.GetGCodeForLayer(gcode, 2);
				Polygons layer2Polygons = TestUtlities.GetExtrusionPolygons(layer2Info, ref movement);

				// make sure there are 3
				Assert.IsTrue(layer2Polygons.Count == 3);
				// make sure they are in the right order (other layers are inside out)
				Assert.IsTrue(layer2Polygons[0].MinX() < layer2Polygons[1].MinX());
			}
		}

		#endregion Inset order tests

		[Test, Category("WorkInProgress")]
		public void AllInsidesBeforeAnyOutsides()
		{
			string thinAttachStlFile = TestUtlities.GetStlPath("Thin Attach");
			string thinAttachGCodeFile = TestUtlities.GetTempGCodePath("Thin Attach.gcode");

			ConfigSettings config = new ConfigSettings();
			config.NumberOfPerimeters = 2;
			config.InfillPercent = 0;
			config.NumberOfTopLayers = 0;
			config.FirstLayerExtrusionWidth = .4;
			config.NumberOfBottomLayers = 0;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(thinAttachGCodeFile);
			processor.LoadStlFile(thinAttachStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] gcode = TestUtlities.LoadGCodeFile(thinAttachGCodeFile);

			// should look like this
			// ____________   ____________
			// | _______  |	  | _______  |
			// | |      | |	  | |      | |
			// | |      | |___| |      | |
			// | |      | ____  |      | |
			// | |______| |   | |______| |
			// |__________|   |__________|
			MovementInfo movement = new MovementInfo();
			{
				// check layer 1
				string[] layer1Info = TestUtlities.GetGCodeForLayer(gcode, 1);
				Polygons layer1Polygons = TestUtlities.GetExtrusionPolygons(layer1Info, ref movement);
				// make sure there are 5
				Assert.IsTrue(layer1Polygons.Count == 3);
				// make sure they are in the right order (two inner polygons print first)
				Assert.IsTrue(layer1Polygons[0].MinX() > layer1Polygons[1].MinX());
				Assert.IsTrue(layer1Polygons[0].MinX() > layer1Polygons[2].MinX());
			}

			{
				// check layer 2
				string[] layer2Info = TestUtlities.GetGCodeForLayer(gcode, 2);
				Polygons layer2Polygons = TestUtlities.GetExtrusionPolygons(layer2Info, ref movement);

				// make sure there are 3
				Assert.IsTrue(layer2Polygons.Count == 3);
				// make sure they are in the right order (two inner polygons print first)
				Assert.IsTrue(layer2Polygons[0].MinX() > layer2Polygons[1].MinX());
				Assert.IsTrue(layer2Polygons[0].MinX() > layer2Polygons[2].MinX());
			}
		}

		[Test]
		public void AllMovesRequiringRetractionDoRetraction()
		{
			string baseFileName = "ab retraction test";
			string stlToLoad = TestUtlities.GetStlPath(baseFileName + ".stl");

			// check that default is support printed with extruder 0
			{
				string gcodeToCreate = TestUtlities.GetTempGCodePath(baseFileName + "_retract_.gcode");

				ConfigSettings config = new ConfigSettings();
				config.RetractionZHop = 5;
				config.MinimumTravelToCauseRetraction = 2;
				config.MinimumExtrusionBeforeRetraction = 0;
				config.MergeOverlappingLines = false;
				config.FirstLayerExtrusionWidth = .5;
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] gcodeContents = TestUtlities.LoadGCodeFile(gcodeToCreate);
				int layerCount = TestUtlities.CountLayers(gcodeContents);
				bool firstPosition = true;
				MovementInfo lastMovement = new MovementInfo();
				MovementInfo lastExtrusion = new MovementInfo();
				bool lastMoveIsExtrusion = true;
				for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
				{
					string[] layerGCode = TestUtlities.GetGCodeForLayer(gcodeContents, layerIndex);
					int movementIndex = 0;
					foreach (MovementInfo movement in TestUtlities.Movements(layerGCode, lastMovement))
					{
						if (!firstPosition)
						{
							bool isTravel = lastMovement.extrusion == movement.extrusion;
							if (isTravel)
							{
								Vector3 lastPosition = lastMovement.position;
								lastPosition.z = 0;
								Vector3 currenPosition = movement.position;
								currenPosition.z = 0;
								double xyLength = (lastPosition - currenPosition).Length;
								if (xyLength > config.MinimumTravelToCauseRetraction
									&& lastMoveIsExtrusion)
								{
									Assert.GreaterOrEqual(movement.position.z, lastExtrusion.position.z);
								}

								lastMoveIsExtrusion = false;
							}
							else
							{
								lastMoveIsExtrusion = true;
								lastExtrusion = movement;
							}

							lastMoveIsExtrusion = !isTravel;
						}

						lastMovement = movement;
						firstPosition = false;
						movementIndex++;
					}
				}
				Assert.IsFalse(TestUtlities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtlities.UsesExtruder(gcodeContents, 2));
			}
		}

		[Test]
		public void BottomClipCorrectNumberOfLayers()
		{
			// test .1 layer height
			Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .2))) == 49);
			Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .31))) == 48);
			Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .4))) == 48);
		}

		[Test]
		public void CanSetExtruderForSupportMaterial()
		{
			string baseFileName = "Support Material 2 Bars";
			string stlToLoad = TestUtlities.GetStlPath(baseFileName + ".stl");

			// check that default is support printed with extruder 0
			{
				string gcodeToCreate = TestUtlities.GetTempGCodePath(baseFileName + "_0_.gcode");

				ConfigSettings config = new ConfigSettings();
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] gcodeContents = TestUtlities.LoadGCodeFile(gcodeToCreate);
				Assert.IsFalse(TestUtlities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtlities.UsesExtruder(gcodeContents, 2));
			}

			// check that support is printed with extruder 1
			{
				string gcodeToCreate = TestUtlities.GetTempGCodePath(baseFileName + "_1b_.gcode");

				ConfigSettings config = new ConfigSettings();
				config.SupportExtruder = 1;
				config.GenerateSupport = true;
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] gcodeContents = TestUtlities.LoadGCodeFile(gcodeToCreate);
				Assert.IsTrue(TestUtlities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtlities.UsesExtruder(gcodeContents, 2));
			}

			// check that support interface is printed with extruder 1
			{
				string gcodeToCreate = TestUtlities.GetTempGCodePath(baseFileName + "_1i_.gcode");

				ConfigSettings config = new ConfigSettings();
				config.SupportInterfaceExtruder = 1;
				config.GenerateSupport = true;
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] gcodeContents = TestUtlities.LoadGCodeFile(gcodeToCreate);
				Assert.IsTrue(TestUtlities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtlities.UsesExtruder(gcodeContents, 2));
			}

			// check that support and interface can be set separately
			{
				string gcodeToCreate = TestUtlities.GetTempGCodePath(baseFileName + "_1b2i_.gcode");

				ConfigSettings config = new ConfigSettings();
				config.SupportExtruder = 1;
				config.SupportInterfaceExtruder = 2;
				config.GenerateSupport = true;
				fffProcessor processor = new fffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.finalize();

				string[] gcodeContents = TestUtlities.LoadGCodeFile(gcodeToCreate);
				Assert.IsTrue(TestUtlities.UsesExtruder(gcodeContents, 1));
				Assert.IsTrue(TestUtlities.UsesExtruder(gcodeContents, 2));
			}
		}

		[Test]
		public void CorrectNumberOfLayersForLayerHeights()
		{
			// test .1 layer height
			Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.1, .1))) == 100);
			Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .1))) == 99);
			Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2))) == 50);
			Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.05, .2))) == 51);
		}

		public void DoHas2WallRingsAllTheWayUp(string fileName, int expectedLayerCount, bool checkRadius = false)
		{
			string stlFile = TestUtlities.GetStlPath(fileName);
			string gCodeFile = TestUtlities.GetTempGCodePath(fileName + ".gcode");

			ConfigSettings config = new ConfigSettings();
			config.InfillPercent = 0;
			config.NumberOfPerimeters = 1;
			config.FirstLayerExtrusionWidth = .2;
			config.LayerThickness = .2;
			config.NumberOfBottomLayers = 0;
			config.NumberOfTopLayers = 0;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(gCodeFile);
			processor.LoadStlFile(stlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] gcodeLines = TestUtlities.LoadGCodeFile(gCodeFile);

			int layerCount = TestUtlities.CountLayers(gcodeLines);
			Assert.IsTrue(layerCount == expectedLayerCount);

			MovementInfo movement = new MovementInfo();
			for (int i = 0; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtlities.GetGCodeForLayer(gcodeLines, i);

				if (i > 0)
				{
					Polygons layerPolygons = TestUtlities.GetExtrusionPolygons(layerInfo, ref movement);

					Assert.IsTrue(layerPolygons.Count == 2);

					if (checkRadius)
					{
						Assert.IsTrue(layerPolygons[0].Count > 10);
						Assert.IsTrue(layerPolygons[1].Count > 10);

						if (false)
						{
							foreach (var polygon in layerPolygons)
							{
								double radiusForPolygon = polygon[0].LengthMm();
								foreach (var point in polygon)
								{
									Assert.AreEqual(radiusForPolygon, point.LengthMm(), 15);
								}
							}
						}
					}
				}
				else
				{
					TestUtlities.GetExtrusionPolygons(layerInfo, ref movement);
				}
			}
		}

		[Test]
		public void DualMaterialPrintMovesCorrectly()
		{
			DualMaterialPrintMovesCorrectly(false);
			DualMaterialPrintMovesCorrectly(true);
		}

		public void DualMaterialPrintMovesCorrectly(bool createWipeTower)
		{
			string leftPart = "Box Left";
			string rightPart = "Box Right";
			string leftStlFile = TestUtlities.GetStlPath(leftPart);
			string rightStlFile = TestUtlities.GetStlPath(rightPart);

			string outputGCodeFileName = TestUtlities.GetTempGCodePath("DualPartMoves");

			ConfigSettings config = new ConfigSettings();
			config.FirstLayerThickness = .2;
			config.CenterObjectInXy = false;
			config.LayerThickness = .2;
			config.NumberOfBottomLayers = 0;
			if (createWipeTower)
			{
				config.WipeTowerSize = 10;
			}
			else
			{
				config.WipeTowerSize = 0;
			}
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(outputGCodeFileName);
			processor.LoadStlFile(leftStlFile);
			processor.LoadStlFile(rightStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] gCodeContent = TestUtlities.LoadGCodeFile(outputGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtlities.CountLayers(gCodeContent);
			Assert.IsTrue(layerCount == 50);

			bool hadMoveLessThan85 = false;

			MovementInfo lastMovement = new MovementInfo();
			for (int i = 0; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtlities.GetGCodeForLayer(gCodeContent, i);

				// check that all layers move up continuously
				foreach (MovementInfo movement in TestUtlities.Movements(layerInfo, lastMovement, onlyG1s: true))
				{
					if (i > 2)
					{
						if (createWipeTower)
						{
							Assert.IsTrue(movement.position.x > 75 && movement.position.y > 10, "Moves don't go to 0");
							if (movement.position.x < 85)
							{
								hadMoveLessThan85 = true;
							}
						}
						else
						{
							Assert.IsTrue(movement.position.x > 85 && movement.position.y > 10, "Moves don't go to 0");
						}
					}
					lastMovement = movement;
				}
			}

			if (createWipeTower)
			{
				Assert.IsTrue(hadMoveLessThan85, "found a wipe tower");
			}
		}

		[Test]
		public void EachLayersHeigherThanLast()
		{
			CheckLayersIncrement("cone", "spiralCone.gcode");
		}

		[Test]
		public void ExportGCodeWithRaft()
		{
			//test that file has raft
			Assert.IsTrue(TestUtlities.CheckForRaft(TestUtlities.LoadGCodeFile(CreateGCodeWithRaft(true))) == true);
			Assert.IsTrue(TestUtlities.CheckForRaft(TestUtlities.LoadGCodeFile(CreateGcodeWithoutRaft(false))) == false);
		}

		[Test]
		public void Has2WallRingsAllTheWayUp()
		{
			DoHas2WallRingsAllTheWayUp("SimpleHole", 25);
			DoHas2WallRingsAllTheWayUp("CylinderWithHole", 50);
			DoHas2WallRingsAllTheWayUp("Thinning Walls Ring", 45, true);
		}

		[Test]
		public void SpiralVaseCreatesContinuousLift()
		{
			CheckSpiralCone("cone", "spiralCone.gcode");

			CheckSpiralCylinder("Cylinder50Sides", "Cylinder50Sides.gcode", 100);
			CheckSpiralCylinder("Cylinder2Wall50Sides", "Cylinder2Wall50Sides.gcode", 100);
			CheckSpiralCylinder("Thinning Walls Ring", "Thinning Walls Ring.gcode", 45);

			// now do it again with thin walls enabled
			CheckSpiralCone("cone", "spiralCone.gcode", true);

			CheckSpiralCylinder("Cylinder50Sides", "Cylinder50Sides.gcode", 100, true);
			CheckSpiralCylinder("Cylinder2Wall50Sides", "Cylinder2Wall50Sides.gcode", 100, true);
			CheckSpiralCylinder("Thinning Walls Ring", "Thinning Walls Ring.gcode", 45, true);
		}

		private static void CheckLayersIncrement(string stlFile, string gcodeFile)
		{
			string risingLayersStlFile = TestUtlities.GetStlPath(stlFile);
			string risingLayersGCodeFileName = TestUtlities.GetTempGCodePath(gcodeFile);

			ConfigSettings config = new ConfigSettings();
			config.FirstLayerThickness = .2;
			config.CenterObjectInXy = false;
			config.LayerThickness = .2;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(risingLayersGCodeFileName);
			processor.LoadStlFile(risingLayersStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] risingLayersGCodeContent = TestUtlities.LoadGCodeFile(risingLayersGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtlities.CountLayers(risingLayersGCodeContent);
			Assert.IsTrue(layerCount == 50);

			MovementInfo startingPosition = new MovementInfo();
			for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			{
				string[] layerInfo = TestUtlities.GetGCodeForLayer(risingLayersGCodeContent, layerIndex);
				int movementIndex = 0;
				// check that all layers move up
				foreach (MovementInfo movement in TestUtlities.Movements(layerInfo, startingPosition))
				{
					if (movement.line.Contains("X")
						|| movement.line.Contains("Y")
						|| movement.line.Contains("Z"))
					{
						if (layerIndex > 0)
						{
							Assert.AreEqual(movement.position.z, .2 + layerIndex * .2, .001);
							Assert.IsTrue(movement.position.z >= startingPosition.position.z);
						}
					}

					// always go up
					startingPosition.position = new Vector3(0, 0, Math.Max(startingPosition.position.z, movement.position.z));
					movementIndex++;
				}
			}
		}

		private static void CheckSpiralCone(string stlFile, string gcodeFile, bool enableThinWalls = false)
		{
			string cylinderStlFile = TestUtlities.GetStlPath(stlFile);
			string cylinderGCodeFileName = TestUtlities.GetTempGCodePath(gcodeFile);

			ConfigSettings config = new ConfigSettings();
			config.FirstLayerThickness = .2;
			config.CenterObjectInXy = false;
			config.LayerThickness = .2;
			if (enableThinWalls)
			{
				config.ExpandThinWalls = true;
				config.FillThinGaps = true;
			}
			config.NumberOfBottomLayers = 0;
			config.ContinuousSpiralOuterPerimeter = true;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(cylinderGCodeFileName);
			processor.LoadStlFile(cylinderStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] cylinderGCodeContent = TestUtlities.LoadGCodeFile(cylinderGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtlities.CountLayers(cylinderGCodeContent);
			Assert.IsTrue(layerCount == 50);

			for (int i = 2; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtlities.GetGCodeForLayer(cylinderGCodeContent, i);

				// check that all layers move up continuously
				MovementInfo lastMovement = new MovementInfo();
				foreach (MovementInfo movement in TestUtlities.Movements(layerInfo))
				{
					Assert.IsTrue(movement.position.z > lastMovement.position.z);

					lastMovement = movement;
				}

				double radiusForLayer = 5.0 + (20.0 - 5.0) / layerCount * i;

				bool first = true;
				lastMovement = new MovementInfo();
				// check that all moves are on the outside of the cylinder (not crossing to a new point)
				foreach (MovementInfo movement in TestUtlities.Movements(layerInfo))
				{
					if (!first)
					{
						Assert.IsTrue((movement.position - lastMovement.position).Length < 2);

						Vector3 xyOnly = new Vector3(movement.position.x, movement.position.y, 0);
						Assert.AreEqual(radiusForLayer, xyOnly.Length, .3);
					}

					lastMovement = movement;
					first = false;
				}
			}
		}

		private static void CheckSpiralCylinder(string stlFile, string gcodeFile, int expectedLayers, bool enableThinWalls = false)
		{
			string cylinderStlFile = TestUtlities.GetStlPath(stlFile);
			string cylinderGCodeFileName = TestUtlities.GetTempGCodePath(gcodeFile);

			ConfigSettings config = new ConfigSettings();
			config.FirstLayerThickness = .2;
			config.CenterObjectInXy = false;
			config.LayerThickness = .2;
			if (enableThinWalls)
			{
				config.ExpandThinWalls = true;
				config.FillThinGaps = true;
			}
			config.NumberOfBottomLayers = 0;
			config.ContinuousSpiralOuterPerimeter = true;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(cylinderGCodeFileName);
			processor.LoadStlFile(cylinderStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] cylinderGCodeContent = TestUtlities.LoadGCodeFile(cylinderGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtlities.CountLayers(cylinderGCodeContent);
			Assert.IsTrue(layerCount == expectedLayers);

			for (int i = 2; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtlities.GetGCodeForLayer(cylinderGCodeContent, i);

				// check that all layers move up continuously
				MovementInfo lastMovement = new MovementInfo();
				foreach (MovementInfo movement in TestUtlities.Movements(layerInfo))
				{
					Assert.IsTrue(movement.position.z > lastMovement.position.z);

					lastMovement = movement;
				}

				bool first = true;
				lastMovement = new MovementInfo();
				// check that all moves are on the outside of the cylinder (not crossing to a new point)
				foreach (MovementInfo movement in TestUtlities.Movements(layerInfo))
				{
					if (!first)
					{
						Assert.IsTrue((movement.position - lastMovement.position).Length < 2);

						Vector3 xyOnly = new Vector3(movement.position.x, movement.position.y, 0);
						Assert.AreEqual(9.8, xyOnly.Length, .3);
					}

					lastMovement = movement;
					first = false;
				}
			}
		}

		private string CreateGCodeForLayerHeights(double firstLayerHeight, double otherLayerHeight, double bottomClip = 0)
		{
			string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-f{0}_o{1}_c{2}.gcode".FormatWith(firstLayerHeight, otherLayerHeight, bottomClip));

			ConfigSettings config = new ConfigSettings();
			config.FirstLayerThickness = firstLayerHeight;
			config.LayerThickness = otherLayerHeight;
			config.BottomClipAmount = bottomClip;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			return boxGCodeFile;
		}

		private string CreateGcodeWithoutRaft(bool hasRaft)
		{
			string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

			ConfigSettings config = new ConfigSettings();
			config.EnableRaft = hasRaft;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			return boxGCodeFile;
		}

		private string CreateGCodeWithRaft(bool hasRaft)
		{
			string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

			ConfigSettings config = new ConfigSettings();
			config.EnableRaft = hasRaft;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			return boxGCodeFile;
		}
	}
}