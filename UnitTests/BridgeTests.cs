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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterSlice.ClipperLib;
using MatterHackers.MatterSlice;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    [TestFixture]
    public class BridgeAngleTests
    {
        [Test]
        public void TestConvexBottomLayer()
        {
            // Check that we can cross two islands that are both at 45s
            //   /
            //  /     /
            //       /
            {
                string outlineString = "x:5655, y:-706,x:-706, y:5656,x:-5655, y:707,x:706, y:-5655,|";
                string partOutlineString = "x:706, y:6364,x:-706, y:7778,x:-7777, y:707,x:-6363, y:-706,|x:7777, y:-706,x:6363, y:707,x:-706, y:-6363,x:706, y:-7777,|";
                double bridgeAngle = GetAngleForData(outlineString, partOutlineString, "two islands 45");
                Assert.AreEqual(bridgeAngle, 315, .01);
            }

            // Check that we can close a u shape the right way
            //  ______
            //  |    |
            //  |    |
            //  |    |
            //  |    |
            {
                string outlineString = "x:104500, y:109000,x:95501, y:109000,x:95501, y:91001,x:104500, y:91001,|";
                string partOutlineString = "x:96001, y:108500,x:104000, y:108500,x:104000, y:89501,x:106000, y:89501,x:106000, y:110500,x:94001, y:110500,x:94001, y:89501,x:96001, y:89501,|";
                double bridgeAngle = GetAngleForData(outlineString, partOutlineString, "upsidedown u");
                Assert.AreEqual(bridgeAngle, 0, .01);
            }

            // Check that we can close a u shape the right way
            // Same as last but rotated 45 degrees
            {
                string outlineString = "x:124112, y:123394,x:110819, y:136688,x:104313, y:130182,x:117607, y:116889,|";
                string partOutlineString = "x:118596, y:116748,x:105162, y:130182,x:110819, y:135839,x:124253, y:122405,x:125667, y:123819,x:110819, y:138667,x:102334, y:130182,x:117182, y:115334,|";
                double bridgeAngle = GetAngleForData(outlineString, partOutlineString, "45 u");
                Assert.AreEqual(bridgeAngle, 45, .01);
            }

            // Check that we can close a u shape the right way
            // Same as last but rotated 31 degrees
            {
                string outlineString = "x:122939, y:121055,x:113257, y:137169,x:105372, y:132431,x:115053, y:116317,|";
                string partOutlineString = "x:115979, y:115941,x:106195, y:132226,x:113052, y:136346,x:122837, y:120061,x:124551, y:121091,x:113736, y:139090,x:103450, y:132910,x:114265, y:114911,|";
                double bridgeAngle = GetAngleForData(outlineString, partOutlineString, "30 u");
                Assert.AreEqual(bridgeAngle, 31, .01);
            }

            // thisi is a left open u with curved inner turns
            // _____________
            //              |
            //              |
            //              |
            // _____________
            {
                string outlineString = "x:110550, y:92203,x:111151, y:92519,x:111740, y:93040,x:112186, y:93687,x:112465, y:94420,x:112550, y:95126,x:112550, y:104875,x:112467, y:105565,x:112193, y:106296,x:111752, y:106945,x:111166, y:107470,x:110473, y:107839,x:110203, y:107907,x:110146, y:108050,x:90610, y:108050,x:90610, y:91951,x:110550, y:91951,|";
                string partOutlineString = "x:122250, y:90207,x:123103, y:90256,x:123948, y:90380,x:124779, y:90577,x:125461, y:90792,x:126251, y:91119,x:127009, y:91514,x:127755, y:91992,x:127879, y:92088,x:126227, y:93740,x:125859, y:93506,x:125279, y:93204,x:124674, y:92953,x:124050, y:92757,x:123411, y:92615,x:122763, y:92530,x:122217, y:92506,x:121456, y:92530,x:120808, y:92615,x:120169, y:92757,x:119545, y:92953,x:118940, y:93204,x:118360, y:93506,x:117808, y:93857,x:117120, y:94411,x:116807, y:94698,x:116365, y:95180,x:115966, y:95699,x:115615, y:96251,x:115313, y:96831,x:115062, y:97436,x:114866, y:98060,x:114724, y:98699,x:114639, y:99347,x:114610, y:100000,x:114639, y:100654,x:114724, y:101302,x:114866, y:101941,x:115062, y:102565,x:115313, y:103170,x:115615, y:103750,x:115966, y:104302,x:116365, y:104821,x:116807, y:105303,x:117374, y:105811,x:117899, y:106202,x:118455, y:106545,x:119040, y:106838,x:119648, y:107080,x:120274, y:107267,x:120915, y:107400,x:121563, y:107476,x:122217, y:107495,x:122870, y:107457,x:123516, y:107363,x:124153, y:107212,x:124774, y:107007,x:125374, y:106747,x:125920, y:106456,x:126227, y:106261,x:127879, y:107913,x:127730, y:108028,x:127009, y:108487,x:126251, y:108882,x:125461, y:109209,x:124645, y:109466,x:123811, y:109651,x:122963, y:109763,x:122109, y:109800,x:89110, y:109800,x:89110, y:106800,x:109300, y:106800,x:109301, y:106785,x:109543, y:106784,x:110011, y:106669,x:110438, y:106444,x:110798, y:106124,x:111072, y:105727,x:111242, y:105277,x:111300, y:104800,x:111300, y:95201,x:111242, y:94722,x:111071, y:94272,x:110797, y:93875,x:110436, y:93555,x:110009, y:93331,x:109541, y:93216,x:109300, y:93216,x:109300, y:93201,x:89110, y:93201,x:89110, y:90201,|";
                double bridgeAngle = GetAngleForData(outlineString, partOutlineString, "rounded left open u");
                Assert.AreEqual(bridgeAngle, 269, .1);
            }
        }

        private static double GetAngleForData(string outlineString, string partOutlineString, string debugName)
        {
            Polygons outline = PolygonsHelper.CreateFromString(outlineString);

            SliceLayer prevLayer = new SliceLayer();
            prevLayer.parts = new List<SliceLayerPart>();
            SliceLayerPart part = new SliceLayerPart();
            part.outline = PolygonsHelper.CreateFromString(partOutlineString);
            prevLayer.parts.Add(part);
            prevLayer.parts[0].boundaryBox.calculate(prevLayer.parts[0].outline);

            double bridgeAngle = Bridge.BridgeAngle(outline, prevLayer, debugName);
            return bridgeAngle;
        }
    }

    public static class BridgeTests
    {
        static bool ranTests = false;

        public static bool RanTests { get { return ranTests; } }
        public static void Run()
        {
            if (!ranTests)
            {
                BridgeAngleTests bridgeAngleTests = new BridgeAngleTests();
                bridgeAngleTests.TestConvexBottomLayer();

                ranTests = true;
            }
        }
    }
}
