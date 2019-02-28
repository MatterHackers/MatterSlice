/*
Copyright (c) 2018, John Lewin
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
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
	public class OutputTestsBase
	{
		protected bool buildControlFiles = false;

		protected static string baseLineText = null;

		private void ProcessGCode(ConfigSettings config, string stlPath, string outputPath)
		{
			var processor = new fffProcessor(config);
			processor.SetTargetFile(outputPath);
			processor.LoadStlFile(stlPath);
			processor.DoProcessing();
			processor.finalize();
		}

		protected void RunGCodeTest(string testName, Action<ConfigSettings> action = null)
		{
			this.RunGCodeTest(testName, null, action);
		}

		protected void RunGCodeTest(string testName, string testFileName, Action<ConfigSettings> action = null)
		{
			if (!buildControlFiles && testName == "baseline")
			{
				return;
			}

			if (!buildControlFiles && baseLineText == null)
			{
				baseLineText = File.ReadAllText(TestUtilities.GetControlGCodePath("baseline"));
			}

			string controlPath = TestUtilities.GetControlGCodePath(testName);

			string outputPath = (buildControlFiles) ? controlPath : TestUtilities.GetTempGCodePath(testName);

			var configSettings = new ConfigSettings();

			action?.Invoke(configSettings);

			if (testFileName == null)
			{
				testFileName = "primitives.stl";
			}

			ProcessGCode(configSettings, TestUtilities.GetStlPath(testFileName), outputPath);

			if (!buildControlFiles)
			{
				string testResults = File.ReadAllText(outputPath);

				Assert.AreNotEqual(controlPath, outputPath, "Control and test paths must differ");
				Assert.AreNotEqual(testResults, baseLineText, "Test does not vary from baseline: " + testName);
				Assert.AreEqual(
					File.ReadAllText(controlPath),
					testResults,
					"Test varies from expected control output: " + testName);
			}
		}
	}

	[TestFixture, Category("MatterSlice.OutputTests")]
	public class OutputTests : OutputTestsBase
	{
		[OneTimeSetUp]
		public void Initialize()
		{
		}

		[Test]
		public void BaselineTest()
		{
			RunGCodeTest("baseline");
		}

		[Test]
		public void AvoidCrossingPerimetersTest()
		{
			this.RunGCodeTest("AvoidCrossingPerimeters", (settings) =>
			{
				settings.AvoidCrossingPerimeters = false; // Default(true)
			});
		}

		[Test, Ignore("Needs additional setup")]
		public void BooleanOperationsTest()
		{
			//this.RunGCodeTest("BooleanOperations", (settings) =>
			//{
			//	settings.BooleanOperations = "";
			//});
		}

		[Test, Ignore("Needs additional setup")]
		public void AdditionalArgsToProcessTest()
		{
			//this.RunGCodeTest("AdditionalArgsToProcess", (settings) =>
			//{
			//	settings.AdditionalArgsToProcess = "";
			//});
		}

		[Test]
		public void BottomInfillSpeedTest()
		{
			this.RunGCodeTest("BottomInfillSpeed", (settings) =>
			{
				settings.BottomInfillSpeed = 20.1; // Default(20.0)
			});
		}

		[Test]
		public void BridgeOverInfillTest()
		{
			this.RunGCodeTest("BridgeOverInfill", (settings) =>
			{
				settings.BridgeOverInfill = true; // Default(false)
			});
		}

		[Test]
		public void BridgeFanSpeedPercentTest()
		{
			this.RunGCodeTest("BridgeFanSpeedPercent", "Bridge Test - CoveredU 30 Degrees.stl", (settings) =>
			{
				settings.BridgeSpeed = 5;
				settings.FanSpeedMinPercent = 35;
				settings.FanSpeedMaxPercent = 100;
				settings.BridgeFanSpeedPercent = 19;
			});
		}

		[Test]
		public void BridgeSpeedTest()
		{
			this.RunGCodeTest("BridgeSpeed", "Bridge Test - CoveredU 30 Degrees.stl", (settings) =>
			{
				settings.BridgeSpeed = 23; // Default(20)
			});
		}

		[Test]
		public void ContinuousSpiralOuterPerimeterTest()
		{
			this.RunGCodeTest("ContinuousSpiralOuterPerimeter", "SimpleHole.stl", (settings) =>
			{
				settings.ContinuousSpiralOuterPerimeter = true; // Default(false)
			});
		}

		[Test]
		public void EnableRaftTest()
		{
			this.RunGCodeTest("EnableRaft", (settings) =>
			{
				settings.EnableRaft = true; // Default(false)
			});
		}

		[Test]
		public void EndCodeTest()
		{
			this.RunGCodeTest("EndCode", (settings) =>
			{
				settings.EndCode = "XXXXXXXXX M104 S0                     ;extruder heater off\nM140 S0                     ;heated bed heater off (if you have it)\nM84                         ;steppers off\n";
			});
		}

		[Test]
		public void ExpandThinWallsTest()
		{
			this.RunGCodeTest("ExpandThinWalls", "ThinWallsRect.stl", (settings) =>
			{
				settings.ExpandThinWalls = true; // Default(false)
			});
		}

		[Test]
		public void ExtrusionMultiplierTest()
		{
			this.RunGCodeTest("ExtrusionMultiplier", (settings) =>
			{
				settings.ExtrusionMultiplier = 1.1; // Default(1.0)
			});
		}

		[Test]
		public void ExtrusionWidthTest()
		{
			this.RunGCodeTest("ExtrusionWidth", (settings) =>
			{
				settings.ExtrusionWidth = .5; // Default(0.4)
			});
		}

		[Test]
		public void FanSpeedMinPercentTest()
		{
			this.RunGCodeTest("FanSpeedMinPercent", (settings) =>
			{
				settings.MinFanSpeedLayerTime = 5;
				settings.MaxFanSpeedLayerTime = 10;
				settings.FanSpeedMaxPercent = 100;

				settings.FanSpeedMinPercent = 55; // Default(100)
			});
		}

		[Test]
		public void MinFanSpeedLayerTimeTest()
		{
			this.RunGCodeTest("MinFanSpeedLayerTime", (settings) =>
			{
				settings.MaxFanSpeedLayerTime = 15;
				settings.FanSpeedMaxPercent = 100;

				settings.MinFanSpeedLayerTime = 7; // Default(300)
			});
		}

		[Test]
		public void FanSpeedMaxPercentTest()
		{
			this.RunGCodeTest("FanSpeedMaxPercent", (settings) =>
			{
				settings.MinFanSpeedLayerTime = 5;
				settings.MaxFanSpeedLayerTime = 10;

				settings.FanSpeedMaxPercent = 80; // Default(100)
			});
		}

		[Test]
		public void MaxFanSpeedLayerTimeTest()
		{
			this.RunGCodeTest("MaxFanSpeedLayerTime", (settings) =>
			{
				settings.MinFanSpeedLayerTime = 5;
				settings.FanSpeedMaxPercent = 90;

				settings.MaxFanSpeedLayerTime = 16; // Default(300)
			});
		}

		[Test]
		public void FilamentDiameterTest()
		{
			this.RunGCodeTest("FilamentDiameter", (settings) =>
			{
				settings.FilamentDiameter = 2.99; // Default(2.89)
			});
		}

		[Test]
		public void FillThinGapsTest()
		{
			this.RunGCodeTest("FillThinGaps", (settings) =>
			{
				settings.FillThinGaps = true; // Default(false)
			});
		}

		[Test]
		public void FirstLayerExtrusionWidthTest()
		{
			this.RunGCodeTest("FirstLayerExtrusionWidth", (settings) =>
			{
				settings.FirstLayerExtrusionWidth = .9; // Default(0.8)
			});
		}

		[Test]
		public void FirstLayerSpeedTest()
		{
			this.RunGCodeTest("FirstLayerSpeed", (settings) =>
			{
				settings.FirstLayerThickness = 3;
				settings.FirstLayerSpeed = 23; // Default(20.0)
			});
		}

		[Test]
		public void NumberOfFirstLayersTest()
		{
			this.RunGCodeTest("NumberOfFirstLayers", (settings) =>
			{
				settings.NumberOfFirstLayers = 3; // Default(0)
			});
		}

		[Test]
		public void FirstLayerThicknessTest()
		{
			this.RunGCodeTest("FirstLayerThickness", (settings) =>
			{
				settings.FirstLayerThickness = .4; // Default(0.3)
			});
		}

		[Test]
		public void FirstLayerToAllowFanTest()
		{
			this.RunGCodeTest("FirstLayerToAllowFan", (settings) =>
			{
				settings.FirstLayerToAllowFan = 5; // Default(2)
			});
		}

		[Test]
		public void GenerateSupportPerimeterTest()
		{
			this.RunGCodeTest("GenerateSupportPerimeter", "Support Material 2 Bars.stl", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.GenerateSupportPerimeter = false; // Default(true)
			});
		}

		[Test]
		public void InfillExtendIntoPerimeterTest()
		{
			this.RunGCodeTest("InfillExtendIntoPerimeter", (settings) =>
			{
				settings.InfillExtendIntoPerimeter = .16; // Default(0.06)
			});
		}

		[Test]
		public void InfillPercentTest()
		{
			this.RunGCodeTest("InfillPercent", "smallbox.stl", (settings) =>
			{
				settings.InfillPercent = 10; // Default(20.0)
			});
		}

		[Test]
		public void InfillSpeedTest()
		{
			this.RunGCodeTest("InfillSpeed", (settings) =>
			{
				settings.InfillSpeed = 23; // Default(50)
			});
		}

		[Test]
		public void InfillStartingAngleTest()
		{
			this.RunGCodeTest("InfillStartingAngle", (settings) =>
			{
				settings.InfillStartingAngle = 20; // Default(45.0)
			});
		}

		[Test]
		public void InfillTypeTest()
		{
			this.RunGCodeTest("InfillType", "smallbox.stl", (settings) =>
			{
				settings.InfillType = ConfigConstants.INFILL_TYPE.LINES; // Default(0)
			});
		}

		[Test]
		public void InsidePerimetersSpeedTest()
		{
			this.RunGCodeTest("InsidePerimetersSpeed", "smallbox.stl", (settings) =>
			{
				settings.InsidePerimetersSpeed = 23; // Default(50)
			});
		}

		[Test]
		public void LayerChangeCodeTest()
		{
			this.RunGCodeTest("LayerChangeCode", (settings) =>
			{
				settings.LayerChangeCode = "XXXXXXXXX ; LAYER:[layer_num]";
			});
		}

		[Test]
		public void LayerThicknessTest()
		{
			this.RunGCodeTest("LayerThickness", (settings) =>
			{
				settings.LayerThickness = .2; // Default(0.1)
			});
		}

		[Test]
		public void MergeOverlappingLinesTest()
		{
			this.RunGCodeTest("MergeOverlappingLines", (settings) =>
			{
				settings.MergeOverlappingLines = false; // Default(true)
			});
		}

		[Test]
		public void MinimumExtrusionBeforeRetractionTest()
		{
			this.RunGCodeTest("MinimumExtrusionBeforeRetraction", (settings) =>
			{
				settings.MinimumExtrusionBeforeRetraction = 1; // Default(0.0)
			});
		}

		[Test]
		public void MinimumLayerTimeSecondsTest()
		{
			this.RunGCodeTest("MinimumLayerTimeSeconds", (settings) =>
			{
				settings.MinimumLayerTimeSeconds = 8; // Default(5)
			});
		}

		[Test]
		public void MinimumPrintingSpeedTest()
		{
			this.RunGCodeTest("MinimumPrintingSpeed", (settings) =>
			{
				settings.MinimumPrintingSpeed = 13; // Default(10)
			});
		}

		[Test]
		public void MinimumTravelToCauseRetractionTest()
		{
			this.RunGCodeTest("MinimumTravelToCauseRetraction", (settings) =>
			{
				settings.MinimumTravelToCauseRetraction = 2; // Default(10.0)
			});
		}

		[Test]
		public void ModelMatrixTest()
		{
			//this.RunGCodeTest("ModelMatrix", (settings) =>
			//{
			//	settings.ModelMatrix = ""; // Default({"Row0":{"X":1.0,"Y":0.0,"Z":0.0,"W":0.0,"Xy":{"X":1.0,"Y":0.0},"Xyz":{"X":1.0,"Y":0.0,"Z":0.0}},"Row1":{"X":0.0,"Y":1.0,"Z":0.0,"W":0.0,"Xy":{"X":0.0,"Y":1.0},"Xyz":{"X":0.0,"Y":1.0,"Z":0.0}},"Row2":{"X":0.0,"Y":0.0,"Z":1.0,"W":0.0,"Xy":{"X":0.0,"Y":0.0},"Xyz":{"X":0.0,"Y":0.0,"Z":1.0}},"Row3":{"X":0.0,"Y":0.0,"Z":0.0,"W":1.0,"Xy":{"X":0.0,"Y":0.0},"Xyz":{"X":0.0,"Y":0.0,"Z":0.0}}})
			//});
		}

		[Test, Ignore("Unclear how to test")]
		public void MultiExtruderOverlapPercentTest()
		{
			this.RunGCodeTest("MultiExtruderOverlapPercent", (settings) =>
			{
				settings.MultiExtruderOverlapPercent = 3; // Default(0)
			});
		}

		[Test]
		public void NumberOfBottomLayersTest()
		{
			this.RunGCodeTest("NumberOfBottomLayers", (settings) =>
			{
				settings.NumberOfBottomLayers = 2; // Default(6)
			});
		}

		[Test]
		public void NumberOfBrimLoopsTest()
		{
			this.RunGCodeTest("NumberOfBrimLoops", (settings) =>
			{
				settings.NumberOfBrimLoops = 3; // Default(0)
			});
		}

		[Test]
		public void NumberOfPerimetersTest()
		{
			this.RunGCodeTest("NumberOfPerimeters", (settings) =>
			{
				settings.NumberOfPerimeters = 5; // Default(2)
			});
		}

		[Test]
		public void NumberOfSkirtLoopsTest()
		{
			this.RunGCodeTest("NumberOfSkirtLoops", (settings) =>
			{
				settings.NumberOfSkirtLoops = 4; // Default(1)
			});
		}

		[Test]
		public void NumberOfTopLayersTest()
		{
			this.RunGCodeTest("NumberOfTopLayers", (settings) =>
			{
				settings.NumberOfTopLayers = 2; // Default(6)
			});
		}

		[Test]
		public void outputOnlyFirstLayerTest()
		{
			this.RunGCodeTest("outputOnlyFirstLayer", (settings) =>
			{
				settings.outputOnlyFirstLayer = true; // Default(false)
			});
		}

		[Test]
		public void OutsidePerimeterExtrusionWidthTest()
		{
			this.RunGCodeTest("OutsidePerimeterExtrusionWidth", (settings) =>
			{
				settings.OutsidePerimeterExtrusionWidth = .5; // Default(0.4)
			});
		}

		[Test]
		public void OutsidePerimetersFirstTest()
		{
			this.RunGCodeTest("OutsidePerimetersFirst", (settings) =>
			{
				settings.OutsidePerimetersFirst = true; // Default(false)
			});
		}

		[Test]
		public void OutsidePerimeterSpeedTest()
		{
			this.RunGCodeTest("OutsidePerimeterSpeed", (settings) =>
			{
				settings.OutsidePerimeterSpeed = 53; // Default(50)
			});
		}

		[Test]
		public void PerimeterStartEndOverlapRatioTest()
		{
			this.RunGCodeTest("PerimeterStartEndOverlapRatio", (settings) =>
			{
				settings.EnableRaft = true;
				settings.PerimeterStartEndOverlapRatio = .7; // Default(1.0)
			});
		}

		[Test]
		public void RaftAirGapTest()
		{
			this.RunGCodeTest("RaftAirGap", (settings) =>
			{
				settings.EnableRaft = true;
				settings.RaftAirGap = .3; // Default(0.2)
			});
		}

		[Test]
		public void RaftExtraDistanceAroundPartTest()
		{
			this.RunGCodeTest("RaftExtraDistanceAroundPart", (settings) =>
			{
				settings.EnableRaft = true;
				settings.RaftExtraDistanceAroundPart = 3; // Default(5.0)
			});
		}

		[Test]
		public void RaftExtruderTest()
		{
			this.RunGCodeTest("RaftExtruder", (settings) =>
			{
				settings.EnableRaft = true;
				settings.RaftExtruder = 2; // Default(-1)
			});
		}

		[Test]
		public void RaftPrintSpeedTest()
		{
			this.RunGCodeTest("RaftPrintSpeed", (settings) =>
			{
				settings.EnableRaft = true;
				settings.RaftPrintSpeed = 3; // Default(0)
			});
		}

		[Test, Ignore("Unclear how to test")]
		public void RetractionOnExtruderSwitchTest()
		{
			this.RunGCodeTest("RetractionOnExtruderSwitch", (settings) =>
			{
				settings.RetractionOnExtruderSwitch = 14.6; // Default(14.5)
			});
		}

		[Test]
		public void RetractionOnTravelTest()
		{
			this.RunGCodeTest("RetractionOnTravel", (settings) =>
			{
				settings.RetractionOnTravel = 4.6; // Default(4.5)
			});
		}

		[Test]
		public void RetractionSpeedTest()
		{
			this.RunGCodeTest("RetractionSpeed", (settings) =>
			{
				settings.RetractionSpeed = 48; // Default(45)
			});
		}

		[Test]
		public void RetractionZHopTest()
		{
			this.RunGCodeTest("RetractionZHop", (settings) =>
			{
				settings.RetractionZHop = .1; // Default(0.0)
			});
		}

		[Test]
		public void RetractWhenChangingIslandsTest()
		{
			this.RunGCodeTest("RetractWhenChangingIslands", (settings) =>
			{
				settings.RetractWhenChangingIslands = false; // Default(true)
			});
		}

		[Test]
		public void SkirtDistanceFromObjectTest()
		{
			this.RunGCodeTest("SkirtDistanceFromObject", (settings) =>
			{
				settings.SkirtDistanceFromObject = 6.1; // Default(6.0)
			});
		}

		[Test]
		public void SkirtMinLengthTest()
		{
			this.RunGCodeTest("SkirtMinLength", (settings) =>
			{
				settings.NumberOfSkirtLoops = 0;
				settings.SkirtMinLength = 3; // Default(0)
			});
		}

		[Test]
		public void StartCodeTest()
		{
			this.RunGCodeTest("StartCode", (settings) =>
			{
				settings.StartCode = "XXXXXXXXX M109 S210     ;Heatup to 210C\nG21           ;metric values\nG90           ;absolute positioning\nG28           ;Home\nG92 E0        ;zero the extruded length\n";
			});
		}

		[Test]
		public void SupportAirGapTest()
		{
			this.RunGCodeTest("SupportAirGap", "Support Material 2 Bars.stl", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportAirGap = .4; // Default(0.3)
			});
		}

		[Test]
		public void SupportExtruderTest()
		{
			this.RunGCodeTest("SupportExtruder", "Support Material 2 Bars.stl", (settings) =>
			{
				settings.ExtruderCount = 3;
				settings.GenerateSupport = true;
				settings.SupportExtruder = 2; // Default(-1)
			});
		}

		[Test]
		public void SupportInfillStartingAngleTest()
		{
			this.RunGCodeTest("SupportInfillStartingAngle", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportInfillStartingAngle = .1; // Default(0.0)
			});
		}

		[Test]
		public void SupportInterfaceExtruderTest()
		{
			this.RunGCodeTest("SupportInterfaceExtruder", (settings) =>
			{
				settings.ExtruderCount = 3;
				settings.GenerateSupport = true;
				settings.SupportInterfaceExtruder = 2; // Default(-1)
			});
		}

		[Test]
		public void SupportInterfaceLayersTest()
		{
			this.RunGCodeTest("SupportInterfaceLayers", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportInterfaceLayers = 6; // Default(3)
			});
		}

		[Test]
		public void SupportLineSpacingTest()
		{
			this.RunGCodeTest("SupportLineSpacing", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportLineSpacing = 2.1; // Default(2.0)
			});
		}

		[Test]
		public void SupportMaterialSpeedTest()
		{
			this.RunGCodeTest("SupportMaterialSpeed", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportMaterialSpeed = 43; // Default(40)
			});
		}

		[Test]
		public void SupportNumberOfLayersToSkipInZTest()
		{
			this.RunGCodeTest("SupportNumberOfLayersToSkipInZ", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportNumberOfLayersToSkipInZ = 4; // Default(1)
			});
		}

		[Test]
		public void SupportPercentTest()
		{
			this.RunGCodeTest("SupportPercent", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportPercent = 30; // Default(50.0)
			});
		}

		[Test]
		public void SupportTypeTest()
		{
			this.RunGCodeTest("SupportType", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportType = ConfigConstants.SUPPORT_TYPE.LINES; // Default(0)
			});
		}

		[Test]
		public void SupportXYDistanceFromObjectTest()
		{
			this.RunGCodeTest("SupportXYDistanceFromObject", (settings) =>
			{
				settings.GenerateSupport = true;
				settings.SupportXYDistanceFromObject = .9; // Default(0.7)
			});
		}

		[Test]
		public void TopInfillSpeedTest()
		{
			this.RunGCodeTest("TopInfillSpeed", "smallbox.stl", (settings) =>
			{
				settings.TopInfillSpeed = 10; // Default(20.0)
			});
		}

		[Test]
		public void TravelSpeedTest()
		{
			this.RunGCodeTest("TravelSpeed", (settings) =>
			{
				settings.TravelSpeed = 203; // Default(200)
			});
		}

		[Test]
		public void UnretractExtraExtrusionTest()
		{
			this.RunGCodeTest("UnretractExtraExtrusion", (settings) =>
			{
				settings.UnretractExtraExtrusion = .1; // Default(0.0)
			});
		}

		[Test, Ignore("Unclear how to test")]
		public void RetractRestartExtraTimeToApplyTest()
		{
			this.RunGCodeTest("RetractRestartExtraTimeToApply", (settings) =>
			{
				settings.RetractRestartExtraTimeToApply = .1; // Default(0.0)
			});
		}

		[Test, Ignore("Unclear how to test")]
		public void UnretractExtraOnExtruderSwitchTest()
		{
			this.RunGCodeTest("UnretractExtraOnExtruderSwitch", (settings) =>
			{
				settings.UnretractExtraOnExtruderSwitch = .1; // Default(0.0)
			});
		}

		[Test]
		public void ResetLongExtrusionTest()
		{
			this.RunGCodeTest("ResetLongExtrusion", "Cylinder50Sides.stl", (settings) =>
			{
				settings.ExtrusionMultiplier = 10;
				settings.InfillPercent = 98;
				settings.ResetLongExtrusion = true; // Default(false)
			});
		}

		[Test, Ignore("Too hard to setup multi-material")]
		public void WipeShieldDistanceFromObjectTest()
		{
			this.RunGCodeTest("WipeShieldDistanceFromObject", (settings) =>
			{
				settings.WipeShieldDistanceFromObject = .1; // Default(0.0)
			});
		}

		[Test, Ignore("Unclear how to test")]
		public void WipeTowerSizeTest()
		{
			this.RunGCodeTest("WipeTowerSize", (settings) =>
			{
				settings.WipeTowerSize = 5.1; // Default(5.0)
			});
		}

		[Test]
		public void ZOffsetTest()
		{
			this.RunGCodeTest("ZOffset", (settings) =>
			{
				settings.ZOffset = .1; // Default(0.0)
			});
		}



	}
}