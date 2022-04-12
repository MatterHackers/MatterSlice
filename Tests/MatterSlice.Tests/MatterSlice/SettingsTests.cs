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
using System.Linq;
using MSClipperLib;
using NUnit.Framework;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice.Tests
{
	[TestFixture, Category("MatterSlice")]
	public class SliceSettingsTests
	{
		#region Inset order tests

		[Test]
		public void InnerPerimeterFirstCorrect()
		{
			// By default we need to do the inner perimeters first
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-perimeter.gcode");

			var config = new ConfigSettings();
			config.NumberOfPerimeters = 3;
			config.InfillPercent = 0;
			config.NumberOfTopLayers = 0;
			config.NumberOfBottomLayers = 0;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] gcode = TestUtilities.LoadGCodeFile(boxGCodeFile);

			var movement = default(MovementInfo);
			{
				// check layer 1
				string[] layer1Info = TestUtilities.GetLayer(gcode, 1);
				Polygons layer1Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer1Info, ref movement);
				// make sure there are 3
				Assert.IsTrue(layer1Polygons.Count == 3);
				// make sure they are in the right order (first layer is outside in)
				Assert.IsTrue(layer1Polygons[0].MinX() > layer1Polygons[1].MinX());
			}

			{
				// check layer 2
				string[] layer2Info = TestUtilities.GetLayer(gcode, 2);
				Polygons layer2Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer2Info, ref movement);

				// make sure there are 3
				Assert.IsTrue(layer2Polygons.Count == 3);
				// make sure they are in the right order (other layers are inside out)
				Assert.IsTrue(layer2Polygons[0].MinX() > layer2Polygons[1].MinX());
			}
		}

		[Test]
		public void SliceFileWithLeadingLowercaseN()
		{
			// Stl with leading n - tests past regression due to c:\path\name.stl where \n in path breaks during stuff/unstuff behavior
			string stlPath = TestUtilities.GetStlPath("name-with-leading-n");

			string gcodePath = TestUtilities.GetTempGCodePath(nameof(SliceFileWithLeadingLowercaseN));

			// Create config file
			var configFilePath = Path.ChangeExtension(gcodePath, "ini");
			using (var stream = new StreamWriter(configFilePath))
			{
				stream.WriteLine($"additionalArgsToProcess = -m \"1,0,0,0,0,1,0,0,0,0,1,0,5,0,0,1\" \"{stlPath}\"");
			}

			// Slice file
			MatterSlice.ProcessArgs($"-v -o \"{gcodePath}\" -c \"{configFilePath}\"");

			// Load and validate generated GCode
			string[] gcode = TestUtilities.LoadGCodeFile(gcodePath);

			var movement = default(MovementInfo);

			// check layer 1
			var layer1Info = TestUtilities.GetLayer(gcode, 1);
			var layer1Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer1Info, ref movement, false);
			Assert.AreEqual(2, layer1Polygons.Where(i => i.Count > 2).Count());

			// check layer 2
			var layer2Info = TestUtilities.GetLayer(gcode, 2);
			var layer2Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer2Info, ref movement, false);
			Assert.AreEqual(2, layer2Polygons.Where(i => i.Count > 2).Count());
		}

		[Test]
		public void SliceFileWithSpaceInName()
		{
			// Stl with space in file name - tests past regression due to spaces in file name
			string stlPath = TestUtilities.GetStlPath("Box Left");

			string gcodePath = TestUtilities.GetTempGCodePath(nameof(SliceFileWithSpaceInName));

			// Create config file
			var configFilePath = Path.ChangeExtension(gcodePath, "ini");
			using (var stream = new StreamWriter(configFilePath))
			{
				stream.WriteLine($"additionalArgsToProcess = -m \"1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1\" \"{stlPath}\"");
			}

			// Slice file
			MatterSlice.ProcessArgs($"-v -o \"{gcodePath}\" -c \"{configFilePath}\"");

			// Load and validate generated GCode
			string[] gcode = TestUtilities.LoadGCodeFile(gcodePath);

			var movement = default(MovementInfo);

			// check layer 1
			var layer1Info = TestUtilities.GetLayer(gcode, 1);
			var layer1Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer1Info, ref movement, false);
			Assert.AreEqual(2, layer1Polygons.Where(i => i.Count > 2).Count());

			// check layer 2
			var layer2Info = TestUtilities.GetLayer(gcode, 2);
			var layer2Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer2Info, ref movement, false);
			Assert.AreEqual(2, layer2Polygons.Where(i => i.Count > 2).Count());
		}

		[Test]
		public void SliceFileWithSpaceInGCodePath()
		{
			// GCode file with space in file name
			string gcodePath = TestUtilities.GetTempGCodePath("gcode file with space");

			string stlPath = TestUtilities.GetStlPath("Box Left");

			// Create config file
			var configFilePath = Path.ChangeExtension(gcodePath, "ini");
			using (var stream = new StreamWriter(configFilePath))
			{
				stream.WriteLine($"additionalArgsToProcess = -m \"1,0,0,0,0,1,0,0,0,0,1,0,5,0,0,1\" \"{stlPath}\"");
			}

			// Slice file
			MatterSlice.ProcessArgs($"-v -o \"{gcodePath}\" -c \"{configFilePath}\"");

			// Load and validate generated GCode
			string[] gcode = TestUtilities.LoadGCodeFile(gcodePath);

			var movement = default(MovementInfo);

			// check layer 1
			var layer1Info = TestUtilities.GetLayer(gcode, 1);
			var layer1Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer1Info, ref movement, false);
			Assert.AreEqual(2, layer1Polygons.Where(i => i.Count > 2).Count());

			// check layer 2
			var layer2Info = TestUtilities.GetLayer(gcode, 2);
			var layer2Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer2Info, ref movement, false);
			Assert.AreEqual(2, layer2Polygons.Where(i => i.Count > 2).Count());
		}

		[Test]
		public void OuterPerimeterFirstCorrect()
		{
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-perimeter.gcode");

			var config = new ConfigSettings();
			config.NumberOfPerimeters = 3;
			config.OutsidePerimetersFirst = true;
			config.InfillPercent = 0;
			config.NumberOfTopLayers = 0;
			config.NumberOfBottomLayers = 0;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] gcode = TestUtilities.LoadGCodeFile(boxGCodeFile);

			var outerPerimeterIndex = 1;
			var secondPerimeterIndex = 2;
			var thirdPerimeterIndex = 0;

			var movement = default(MovementInfo);
			{
				// check layer 1
				var layer1Info = TestUtilities.GetLayer(gcode, 1);
				var layerPolygons = TestUtilities.GetExtrusionPolygonsForLayer(layer1Info, ref movement);
				// make sure there are 3
				Assert.IsTrue(layerPolygons.Count == 3);
				// perimeters should be in 3 1 2 order so that we have priming happening before the outer perimeter
				Assert.IsTrue(layerPolygons[outerPerimeterIndex].MinX() < layerPolygons[secondPerimeterIndex].MinX());
				Assert.IsTrue(layerPolygons[outerPerimeterIndex].MinX() < layerPolygons[thirdPerimeterIndex].MinX());
				Assert.IsTrue(layerPolygons[secondPerimeterIndex].MinX() < layerPolygons[thirdPerimeterIndex].MinX());
			}

			{
				// check layer 2
				var layer2Info = TestUtilities.GetLayer(gcode, 2);
				var layerPolygons = TestUtilities.GetExtrusionPolygonsForLayer(layer2Info, ref movement);

				// make sure there are 3
				Assert.IsTrue(layerPolygons.Count == 3);
				// make sure they are in the right order (other layers are inside out)
				Assert.IsTrue(layerPolygons[outerPerimeterIndex].MinX() < layerPolygons[secondPerimeterIndex].MinX());
				Assert.IsTrue(layerPolygons[outerPerimeterIndex].MinX() < layerPolygons[thirdPerimeterIndex].MinX());
				Assert.IsTrue(layerPolygons[secondPerimeterIndex].MinX() < layerPolygons[thirdPerimeterIndex].MinX());
			}
		}

		#endregion Inset order tests

		[Test, Ignore("WorkInProgress")]
		public void AllInsidesBeforeAnyOutsides()
		{
			string thinAttachStlFile = TestUtilities.GetStlPath("Thin Attach");
			string thinAttachGCodeFile = TestUtilities.GetTempGCodePath("Thin Attach.gcode");

			var config = new ConfigSettings();
			config.NumberOfPerimeters = 2;
			config.InfillPercent = 0;
			config.NumberOfTopLayers = 0;
			config.FirstLayerExtrusionWidth = .4;
			config.NumberOfBottomLayers = 0;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(thinAttachGCodeFile);
			processor.LoadStlFile(thinAttachStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] gcode = TestUtilities.LoadGCodeFile(thinAttachGCodeFile);

			// should look like this
			// ____________   ____________
			// | _______  |	  | _______  |
			// | |      | |	  | |      | |
			// | |      | |___| |      | |
			// | |      | ____  |      | |
			// | |______| |   | |______| |
			// |__________|   |__________|
			var movement = default(MovementInfo);
			{
				// check layer 1
				string[] layer1Info = TestUtilities.GetLayer(gcode, 1);
				Polygons layer1Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer1Info, ref movement);
				// make sure there are 5
				Assert.IsTrue(layer1Polygons.Count == 3);
				// make sure they are in the right order (two inner polygons print first)
				Assert.IsTrue(layer1Polygons[0].MinX() > layer1Polygons[1].MinX());
				Assert.IsTrue(layer1Polygons[0].MinX() > layer1Polygons[2].MinX());
			}

			{
				// check layer 2
				string[] layer2Info = TestUtilities.GetLayer(gcode, 2);
				Polygons layer2Polygons = TestUtilities.GetExtrusionPolygonsForLayer(layer2Info, ref movement);

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
			GCodeExport.CheckForZeroPositions = false;
			AllMovesRequiringRetractionDoRetraction("ab retraction test");
			AllMovesRequiringRetractionDoRetraction("MH Coin In Shadow");

			string settingsIniFile = Path.Combine(TestUtilities.MatterSliceBaseDirectory, "Tests", "TestData", "MH Coin Settings.ini");
			AllMovesRequiringRetractionDoRetraction("MH Coin In Shadow", settingsIniFile);
		}

		public void AllMovesRequiringRetractionDoRetraction(string baseFileName, string settingsIniFile = "")
		{
			string stlToLoad = TestUtilities.GetStlPath(baseFileName + ".stl");

			// check that default is support printed with extruder 0
			string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_retract_.gcode");

			var config = new ConfigSettings();
			if (settingsIniFile == "")
			{
				config.MinimumTravelToCauseRetraction = 2;
				config.MinimumExtrusionBeforeRetraction = 0;
				config.MergeOverlappingLines = false;
				config.FirstLayerExtrusionWidth = .5;
			}
			else
			{
				config.ReadSettings(settingsIniFile);
			}

			// this is what we detect
			config.RetractionZHop = 5;

			var processor = new FffProcessor(config);
			processor.SetTargetFile(gcodeToCreate);
			processor.LoadStlFile(stlToLoad);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
			int layerCount = TestUtilities.LayerCount(gcodeContents);
			bool firstPosition = true;
			var lastMovement = default(MovementInfo);
			var lastExtrusion = default(MovementInfo);
			bool lastMoveIsExtrusion = true;
			for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			{
				string[] layerGCode = TestUtilities.GetLayer(gcodeContents, layerIndex);
				int movementIndex = 0;
				foreach (MovementInfo movement in TestUtilities.GetLayerMovements(layerGCode, lastMovement))
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

			// make sure we don't switch extruders
			Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 1));
			Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
		}

		[Test]
		public void CanSetExtruderForSupportMaterial()
		{
			string baseFileName = "Support Material 2 Bars";
			string stlToLoad = TestUtilities.GetStlPath(baseFileName + ".stl");
			string supportToLoad = TestUtilities.GetStlPath("2BarsSupport.stl");

			// check that default is support printed with extruder 0
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_0_.gcode");

				var config = new ConfigSettings();
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
			}

			// check that support is printed with extruder 0
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_1b_.gcode");

				var config = new ConfigSettings();
				config.ExtruderCount = 1;
				config.SupportExtruder = 1; // from a 0 based index
											// this is a hack, but it is the signaling mechanism for support
				config.BooleanOperations = "S";
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(supportToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
			}

			// check that support is printed with extruder 1
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_1b_.gcode");

				var config = new ConfigSettings();
				config.SupportExtruder = 1;
				config.ExtruderCount = 2;
				// this is a hack, but it is the signaling mechanism for support
				config.BooleanOperations = "S";
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// we have to have a mesh for every extruder
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(supportToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsTrue(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
			}

			// check that support interface is printed with extruder 0
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_1i_.gcode");

				var config = new ConfigSettings();
				config.ExtruderCount = 1;
				config.SupportInterfaceExtruder = 1;
				// this is a hack, but it is the signaling mechanism for support
				config.BooleanOperations = "S";
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(supportToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
			}

			// check that support interface is printed with extruder 1
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_1i_.gcode");

				var config = new ConfigSettings();
				config.ExtruderCount = 2;
				config.SupportInterfaceExtruder = 1;
				// this is a hack, but it is the signaling mechanism for support
				config.BooleanOperations = "S";
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// we have to have a mesh for every extruder
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(supportToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsTrue(TestUtilities.UsesExtruder(gcodeContents, 0));
				Assert.IsTrue(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
			}

			// check that support and interface can be set separately
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_1b2i_.gcode");

				var config = new ConfigSettings();
				config.ExtruderCount = 1;
				config.SupportExtruder = 1;
				config.SupportInterfaceExtruder = 2;
				// this is a hack, but it is the signaling mechanism for support
				config.BooleanOperations = "S";
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(supportToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
			}

			// check that support and interface can be set separately
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_1b2i_.gcode");

				var config = new ConfigSettings();
				config.ExtruderCount = 2;
				config.SupportExtruder = 1;
				config.SupportInterfaceExtruder = 2;
				// this is a hack, but it is the signaling mechanism for support
				config.BooleanOperations = "S";
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// we have to have a mesh for every extruder
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(supportToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsTrue(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsFalse(TestUtilities.UsesExtruder(gcodeContents, 2));
			}

			// check that support and interface can be set separately
			{
				string gcodeToCreate = TestUtilities.GetTempGCodePath(baseFileName + "_1b2i_.gcode");

				var config = new ConfigSettings();
				config.ExtruderCount = 3;
				config.SupportExtruder = 1;
				config.SupportInterfaceExtruder = 2;
				// this is a hack, but it is the signaling mechanism for support
				config.BooleanOperations = "S";
				var processor = new FffProcessor(config);
				processor.SetTargetFile(gcodeToCreate);
				processor.LoadStlFile(stlToLoad);
				// we have to have a mesh for every extruder
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(stlToLoad);
				processor.LoadStlFile(supportToLoad);
				// slice and save it
				processor.DoProcessing();
				processor.Dispose();

				string[] gcodeContents = TestUtilities.LoadGCodeFile(gcodeToCreate);
				Assert.IsTrue(TestUtilities.UsesExtruder(gcodeContents, 1));
				Assert.IsTrue(TestUtilities.UsesExtruder(gcodeContents, 2));
			}
		}

		[Test]
		public void CorrectNumberOfLayersForLayerHeights()
		{
			// test .1 layer height
			Assert.AreEqual(100, TestUtilities.LayerCount(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.1, .1))));
			Assert.AreEqual(99, TestUtilities.LayerCount(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .1))));
			Assert.AreEqual(50, TestUtilities.LayerCount(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2))));
			Assert.AreEqual(51, TestUtilities.LayerCount(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.05, .2))));
		}

		[Test]
		public void SingleLayerCreated()
		{
			string point3mmStlFile = TestUtilities.GetStlPath("Point3mm");
			string point3mmGCodeFile = TestUtilities.GetTempGCodePath("Point3mm.gcode");

			var config = new ConfigSettings();
			config.FirstLayerThickness = .25;
			config.LayerThickness = .25;
			config.NumberOfSkirtLoops = 0;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(point3mmGCodeFile);
			processor.LoadStlFile(point3mmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			var loadedGCode = TestUtilities.LoadGCodeFile(point3mmGCodeFile);
			var layers = TestUtilities.LayerCount(loadedGCode);
			Assert.AreEqual(1, layers);
			var totalExtrusions = TestUtilities.GetExtrusionPolygonsForLayer(loadedGCode);
#if __ANDROID__
			Assert.IsTrue(totalExtrusions.Count > 0);
			Assert.IsTrue(totalExtrusions[0].PolygonLength() > 100);
#else
			Assert.Greater(totalExtrusions.Count, 0);
			Assert.Greater(totalExtrusions[0].PolygonLength(), 100);
#endif
		}

		public void DoHas2WallRingsAllTheWayUp(string fileName, int expectedLayerCount, bool checkRadius = false)
		{
			string stlFile = TestUtilities.GetStlPath(fileName);
			string gCodeFile = TestUtilities.GetTempGCodePath(fileName + ".gcode");

			var config = new ConfigSettings();
			config.InfillPercent = 0;
			config.NumberOfPerimeters = 1;
			config.FirstLayerExtrusionWidth = .2;
			config.LayerThickness = .2;
			config.NumberOfBottomLayers = 0;
			config.NumberOfTopLayers = 0;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(gCodeFile);
			processor.LoadStlFile(stlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] gcodeLines = TestUtilities.LoadGCodeFile(gCodeFile);

			int layerCount = TestUtilities.LayerCount(gcodeLines);
			Assert.IsTrue(layerCount == expectedLayerCount);

			var movement = default(MovementInfo);
			for (int i = 0; i < layerCount - 10; i++)
			{
				string[] layerInfo = TestUtilities.GetLayer(gcodeLines, i);

				if (i > 0)
				{
					Polygons layerPolygons = TestUtilities.GetExtrusionPolygonsForLayer(layerInfo, ref movement);

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
					TestUtilities.GetExtrusionPolygonsForLayer(layerInfo, ref movement);
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
			string leftStlFile = TestUtilities.GetStlPath(leftPart);
			string rightStlFile = TestUtilities.GetStlPath(rightPart);

			string outputGCodeFileName = TestUtilities.GetTempGCodePath("DualPartMoves");

			var config = new ConfigSettings();
			config.ExtruderCount = 2;
			config.FirstLayerThickness = .2;
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

			var processor = new FffProcessor(config);
			processor.SetTargetFile(outputGCodeFileName);
			processor.LoadStlFile(leftStlFile);
			processor.LoadStlFile(rightStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] gCodeContent = TestUtilities.LoadGCodeFile(outputGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtilities.LayerCount(gCodeContent);
			Assert.IsTrue(layerCount == 50);

			bool hadMoveLessThan85 = false;

			var lastMovement = default(MovementInfo);
			for (int i = 0; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtilities.GetLayer(gCodeContent, i);

				// check that all layers move up continuously
				foreach (MovementInfo movement in TestUtilities.GetLayerMovements(layerInfo, lastMovement, onlyG1s: true))
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
		public void DualMaterialNoRetraction()
		{
			DualMaterialNoRetraction(0);
			DualMaterialNoRetraction(1);
		}

		public void DualMaterialNoRetraction(int material)
		{
			GCodeExport.CheckForZeroPositions = false;
			string shortCubeName = "CubePoint2High";
			string shortCube = TestUtilities.GetStlPath(shortCubeName);

			string outputGCodeFileName = TestUtilities.GetTempGCodePath($"CubeNoRetractions{material}");

			var config = new ConfigSettings();
			config.ExtruderCount = 2;
			config.FirstLayerThickness = .2;
			config.LayerThickness = .2;

			var processor = new FffProcessor(config);
			processor.SetTargetFile(outputGCodeFileName);
			for (int i = 0; i < material; i++)
			{
				string skipExtruder = TestUtilities.GetStlPath("TooSmallToPrint");

				processor.LoadStlFile(skipExtruder);
			}

			processor.LoadStlFile(shortCube);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] gCodeContent = TestUtilities.LoadGCodeFile(outputGCodeFileName);

			// test layer count
			int layerCount = TestUtilities.LayerCount(gCodeContent);
			Assert.AreEqual(1, layerCount);

			int retractions = TestUtilities.CountRetractions(gCodeContent);
			Assert.AreEqual(1, retractions, $"Material {material} should have no retractions");
		}


		[Test]
		public void EachLayersHeigherThanLast()
		{
			CheckLayersIncrement("cone", "spiralCone.gcode");
		}

		[Test]
		public void ExportGCodeWithRaft()
		{
			// test that file has raft
			Assert.IsTrue(TestUtilities.CheckForRaft(TestUtilities.LoadGCodeFile(CreateGCodeWithRaft(true))) == true);
			Assert.IsTrue(TestUtilities.CheckForRaft(TestUtilities.LoadGCodeFile(CreateGcodeWithoutRaft(false))) == false);
		}

		[Test]
		public void Has2WallRingsAllTheWayUp()
		{
			DoHas2WallRingsAllTheWayUp("SimpleHole", 25);
			DoHas2WallRingsAllTheWayUp("CylinderWithHole", 50);
			DoHas2WallRingsAllTheWayUp("Thinning Walls Ring", 49, true);
		}

		[Test]
		public void SpiralVaseCreatesContinuousLift()
		{
			CheckSpiralCone("cone", "spiralCone.gcode");

			CheckSpiralCylinder("Cylinder50Sides", "Cylinder50Sides.gcode", 100);
			CheckSpiralCylinder("Cylinder2Wall50Sides", "Cylinder2Wall50Sides.gcode", 100);
			CheckSpiralCylinder("Thinning Walls Ring", "Thinning Walls Ring.gcode", 50);

			// now do it again with thin walls enabled
			CheckSpiralCone("cone", "spiralCone.gcode", true);

			CheckSpiralCylinder("Cylinder50Sides", "Cylinder50Sides.gcode", 100, true);
			CheckSpiralCylinder("Cylinder2Wall50Sides", "Cylinder2Wall50Sides.gcode", 100, true);
			CheckSpiralCylinder("Thinning Walls Ring", "Thinning Walls Ring.gcode", 50, true);
		}

		private static void CheckLayersIncrement(string stlFile, string gcodeFile)
		{
			string risingLayersStlFile = TestUtilities.GetStlPath(stlFile);
			string risingLayersGCodeFileName = TestUtilities.GetTempGCodePath(gcodeFile);

			var config = new ConfigSettings();
			config.FirstLayerThickness = .2;
			config.LayerThickness = .2;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(risingLayersGCodeFileName);
			processor.LoadStlFile(risingLayersStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] risingLayersGCodeContent = TestUtilities.LoadGCodeFile(risingLayersGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtilities.LayerCount(risingLayersGCodeContent);
			Assert.IsTrue(layerCount == 50);

			var startingPosition = default(MovementInfo);
			for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			{
				string[] layerInfo = TestUtilities.GetLayer(risingLayersGCodeContent, layerIndex);
				int movementIndex = 0;
				// check that all layers move up
				foreach (MovementInfo movement in TestUtilities.GetLayerMovements(layerInfo, startingPosition))
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
			string cylinderStlFile = TestUtilities.GetStlPath(stlFile);
			string cylinderGCodeFileName = TestUtilities.GetTempGCodePath(gcodeFile);

			var config = new ConfigSettings
			{
				FirstLayerThickness = .2,
				LayerThickness = .2,
				NumberOfBottomLayers = 0,
				ContinuousSpiralOuterPerimeter = true
			};

			if (enableThinWalls)
			{
				config.ExpandThinWalls = true;
				config.FillThinGaps = true;
			}

			var processor = new FffProcessor(config);
			processor.SetTargetFile(cylinderGCodeFileName);
			processor.LoadStlFile(cylinderStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] cylinderGCodeContent = TestUtilities.LoadGCodeFile(cylinderGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtilities.LayerCount(cylinderGCodeContent);
			Assert.AreEqual(50, layerCount, "SpiralCone should have 50 layers");

			for (int i = 2; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtilities.GetLayer(cylinderGCodeContent, i);

				// check that all layers move up continuously
				var lastMovement = default(MovementInfo);
				foreach (MovementInfo movement in TestUtilities.GetLayerMovements(layerInfo))
				{
#if __ANDROID__
					Assert.IsTrue(movement.position.z > lastMovement.position.z);
#else
					Assert.Greater(movement.position.z, lastMovement.position.z, "Z position should increment per layer");
#endif
					lastMovement = movement;
				}

				double radiusForLayer = 5.0 + (20.0 - 5.0) / layerCount * i;

				bool first = true;
				lastMovement = default(MovementInfo);
				// check that all moves are on the outside of the cylinder (not crossing to a new point)
				foreach (MovementInfo movement in TestUtilities.GetLayerMovements(layerInfo))
				{
					if (!first)
					{
						Assert.IsTrue((movement.position - lastMovement.position).Length < 2);

						var xyOnly = new Vector3(movement.position.x, movement.position.y, 0);
						Assert.AreEqual(radiusForLayer, xyOnly.Length, .3);
					}

					lastMovement = movement;
					first = false;
				}
			}
		}

		private static void CheckSpiralCylinder(string stlFile, string gcodeFile, int expectedLayers, bool enableThinWalls = false)
		{
			string cylinderStlFile = TestUtilities.GetStlPath(stlFile);
			string cylinderGCodeFileName = TestUtilities.GetTempGCodePath(gcodeFile);

			var config = new ConfigSettings();
			config.FirstLayerThickness = .2;
			config.LayerThickness = .2;
			if (enableThinWalls)
			{
				config.ExpandThinWalls = true;
				config.FillThinGaps = true;
			}

			config.NumberOfBottomLayers = 0;
			config.ContinuousSpiralOuterPerimeter = true;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(cylinderGCodeFileName);
			processor.LoadStlFile(cylinderStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			string[] cylinderGCodeContent = TestUtilities.LoadGCodeFile(cylinderGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtilities.LayerCount(cylinderGCodeContent);
			Assert.IsTrue(layerCount == expectedLayers);

			for (int i = 2; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtilities.GetLayer(cylinderGCodeContent, i);

				// check that all layers move up continuously
				var lastMovement = default(MovementInfo);
				foreach (MovementInfo movement in TestUtilities.GetLayerMovements(layerInfo))
				{
					Assert.IsTrue(movement.position.z > lastMovement.position.z);

					lastMovement = movement;
				}

				bool first = true;
				lastMovement = default(MovementInfo);
				// check that all moves are on the outside of the cylinder (not crossing to a new point)
				foreach (MovementInfo movement in TestUtilities.GetLayerMovements(layerInfo))
				{
					if (!first)
					{
						Assert.IsTrue((movement.position - lastMovement.position).Length < 2);

						var xyOnly = new Vector3(movement.position.x, movement.position.y, 0);
						Assert.AreEqual(9.8, xyOnly.Length, .3);
					}

					lastMovement = movement;
					first = false;
				}
			}
		}

		private string CreateGCodeForLayerHeights(double firstLayerHeight, double otherLayerHeight)
		{
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-f{0}_o{1}.gcode".FormatWith(firstLayerHeight, otherLayerHeight));

			var config = new ConfigSettings();
			config.FirstLayerThickness = firstLayerHeight;
			config.LayerThickness = otherLayerHeight;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			return boxGCodeFile;
		}

		private string CreateGcodeWithoutRaft(bool hasRaft)
		{
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

			var config = new ConfigSettings();
			config.EnableRaft = hasRaft;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			return boxGCodeFile;
		}

		private string CreateGCodeWithRaft(bool hasRaft)
		{
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

			var config = new ConfigSettings();
			config.EnableRaft = hasRaft;
			var processor = new FffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			return boxGCodeFile;
		}
	}
}