﻿/*
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

using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
    [TestFixture, Category("MatterSlice")]
	public class SliceSettingsTests
	{
		private string CreateGCodeForLayerHeights(double firstLayerHeight, double otherLayerHeight, double bottomClip = 0)
		{
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-f{0}_o{1}_c{2}.gcode".FormatWith(firstLayerHeight, otherLayerHeight, bottomClip));

			ConfigSettings config = new ConfigSettings();
			config.firstLayerThickness = firstLayerHeight;
			config.layerThickness = otherLayerHeight;
			config.bottomClipAmount = bottomClip;
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
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

			ConfigSettings config = new ConfigSettings();
			config.enableRaft = hasRaft;
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
			string box20MmStlFile = TestUtilities.GetStlPath("20mm-box");
			string boxGCodeFile = TestUtilities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

			ConfigSettings config = new ConfigSettings();
			config.enableRaft = hasRaft;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(boxGCodeFile);
			processor.LoadStlFile(box20MmStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			return boxGCodeFile;
		}

		[Test]
		public void SpiralVaseCreatesContinuousLift()
		{
			CheckCylinder("Cylinder50Sides", "Cylinder50Sides.gcode");

			CheckCylinder("Cylinder2Wall50Sides", "Cylinder2Wall50Sides.gcode");
		}

		private static void CheckCylinder(string stlFile, string gcodeFile)
		{
			string cylinderStlFile = TestUtilities.GetStlPath(stlFile);
			string cylinderGCodeFileName = TestUtilities.GetTempGCodePath(gcodeFile);

			ConfigSettings config = new ConfigSettings();
			config.firstLayerThickness = .2;
			config.centerObjectInXy = false;
			config.layerThickness = .2;
			config.numberOfBottomLayers = 0;
			config.continuousSpiralOuterPerimeter = true;
			fffProcessor processor = new fffProcessor(config);
			processor.SetTargetFile(cylinderGCodeFileName);
			processor.LoadStlFile(cylinderStlFile);
			// slice and save it
			processor.DoProcessing();
			processor.finalize();

			string[] cylinderGCodeContent = TestUtilities.LoadGCodeFile(cylinderGCodeFileName);

			// test .1 layer height
			int layerCount = TestUtilities.CountLayers(cylinderGCodeContent);
			Assert.IsTrue(layerCount == 100);

			for (int i = 2; i < layerCount - 3; i++)
			{
				string[] layerInfo = TestUtilities.GetGCodeForLayer(cylinderGCodeContent, i);

				// check that all layers move up continuously
				MovementInfo lastMovement = new MovementInfo();
				foreach (MovementInfo movement in TestUtilities.Movements(layerInfo))
				{
					Assert.IsTrue(movement.position.z > lastMovement.position.z);

					lastMovement = movement;
				}

				bool first = true;
				lastMovement = new MovementInfo();
				// check that all moves are on the outside of the cylinder (not crossing to a new point)
				foreach (MovementInfo movement in TestUtilities.Movements(layerInfo))
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

		[Test]
		public void CorrectNumberOfLayersForLayerHeights()
		{
			// test .1 layer height
			Assert.IsTrue(TestUtilities.CountLayers(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.1, .1))) == 100);
			Assert.IsTrue(TestUtilities.CountLayers(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .1))) == 99);
			Assert.IsTrue(TestUtilities.CountLayers(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2))) == 50);
			Assert.IsTrue(TestUtilities.CountLayers(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.05, .2))) == 51);
		}

		[Test]
		public void BottomClipCorrectNumberOfLayers()
		{
			// test .1 layer height
			Assert.IsTrue(TestUtilities.CountLayers(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .2))) == 49);
			Assert.IsTrue(TestUtilities.CountLayers(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .31))) == 48);
			Assert.IsTrue(TestUtilities.CountLayers(TestUtilities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .4))) == 48);
		}

		[Test]
		public void ExportGCodeWithRaft()
		{
			//test that file has raft
			Assert.IsTrue(TestUtilities.CheckForRaft(TestUtilities.LoadGCodeFile(CreateGCodeWithRaft(true))) == true);
			Assert.IsTrue(TestUtilities.CheckForRaft(TestUtilities.LoadGCodeFile(CreateGcodeWithoutRaft(false))) == false);
		}
	}
}