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
    public class SliceSettingsTests
    {
        string CreateGCodeForLayerHeights(double firstLayerHeight, double otherLayerHeight, double bottomClip = 0)
        {
            string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
            string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-f{0}_o{1}_c{2}.gcode".FormatWith(firstLayerHeight, otherLayerHeight, bottomClip));

            ConfigSettings config = new ConfigSettings();
            config.firstLayerThickness = firstLayerHeight;
            config.layerThickness = otherLayerHeight;
            config.bottomClipAmount = bottomClip;
            fffProcessor processor = new fffProcessor(config);
            processor.setTargetFile(boxGCodeFile);
            processor.LoadStlFile(box20MmStlFile);
            // slice and save it
            processor.DoProcessing();
            processor.finalize();

            return boxGCodeFile;
        }

        string CreateGCodeWithRaft(bool hasRaft)
        {
            string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
            string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

            ConfigSettings config = new ConfigSettings();
            config.enableRaft = hasRaft;
            fffProcessor processor = new fffProcessor(config);
            processor.setTargetFile(boxGCodeFile);
            processor.LoadStlFile(box20MmStlFile);
            // slice and save it
            processor.DoProcessing();
            processor.finalize();

            return boxGCodeFile;
        }

        string CreateGcodeWithoutRaft(bool hasRaft)
        {
            string box20MmStlFile = TestUtlities.GetStlPath("20mm-box");
            string boxGCodeFile = TestUtlities.GetTempGCodePath("20mm-box-f{0}.gcode".FormatWith(hasRaft));

            ConfigSettings config = new ConfigSettings();
            config.enableRaft = hasRaft;
            fffProcessor processor = new fffProcessor(config);
            processor.setTargetFile(boxGCodeFile);
            processor.LoadStlFile(box20MmStlFile);
            // slice and save it
            processor.DoProcessing();
            processor.finalize();

            return boxGCodeFile;
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

        [Test]
        public void BottomClipCorrectNumberOfLayers()
        {
            // test .1 layer height
            Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .2))) == 49);
            Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .31))) == 48);
            Assert.IsTrue(TestUtlities.CountLayers(TestUtlities.LoadGCodeFile(CreateGCodeForLayerHeights(.2, .2, .4))) == 48);
        }

        [Test]
        public void ExportGCodeWithRaft()
        {
            //test that file has raft
            Assert.IsTrue(TestUtlities.CheckForRaft(TestUtlities.LoadGCodeFile(CreateGCodeWithRaft(true))) == true);
            Assert.IsTrue(TestUtlities.CheckForRaft(TestUtlities.LoadGCodeFile(CreateGcodeWithoutRaft(false))) == false);
        }

    }

 
    public static class SettingsTests
    {
        static bool ranTests = false;

        public static bool RanTests { get { return ranTests; } }
        public static void Run()
        {
            if (!ranTests)
            {
                SliceSettingsTests settingsTests = new SliceSettingsTests();
                settingsTests.CorrectNumberOfLayersForLayerHeights();             
                settingsTests.BottomClipCorrectNumberOfLayers();
                settingsTests.ExportGCodeWithRaft();
               

                ranTests = true;
            }
        }
    }
}
