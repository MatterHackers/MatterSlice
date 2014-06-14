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
                int bridgeAngle = GetAngleForData(outlineString, partOutlineString);
                Assert.IsTrue(bridgeAngle == 135);
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
                int bridgeAngle = GetAngleForData(outlineString, partOutlineString);
                Assert.IsTrue(bridgeAngle == 90);
            }

            // Check that we can close a u shape the right way
            // Same as last but rotated 45 degrees
            {
                string outlineString = "x:124112, y:123394,x:110819, y:136688,x:104313, y:130182,x:117607, y:116889,|";
                string partOutlineString = "x:118596, y:116748,x:105162, y:130182,x:110819, y:135839,x:124253, y:122405,x:125667, y:123819,x:110819, y:138667,x:102334, y:130182,x:117182, y:115334,|";
                int bridgeAngle = GetAngleForData(outlineString, partOutlineString);
                Assert.IsTrue(bridgeAngle == 135);
            }

            // Check that we can close a u shape the right way
            // Same as last but rotated 31 degrees
            {
                string outlineString = "x:122939, y:121055,x:113257, y:137169,x:105372, y:132431,x:115053, y:116317,|";
                string partOutlineString = "x:115979, y:115941,x:106195, y:132226,x:113052, y:136346,x:122837, y:120061,x:124551, y:121091,x:113736, y:139090,x:103450, y:132910,x:114265, y:114911,|";
                int bridgeAngle = GetAngleForData(outlineString, partOutlineString);
                Assert.IsTrue(bridgeAngle == 120);
            }
        }

        private static int GetAngleForData(string outlineString, string partOutlineString)
        {
            Polygons outline = PolygonsHelper.CreateFromString(outlineString);

            SliceLayer prevLayer = new SliceLayer();
            prevLayer.parts = new List<SliceLayerPart>();
            SliceLayerPart part = new SliceLayerPart();
            part.outline = PolygonsHelper.CreateFromString(partOutlineString);
            prevLayer.parts.Add(part);
            prevLayer.parts[0].boundaryBox.calculate(prevLayer.parts[0].outline);

            int bridgeAngle = Bridge.BridgeAngle(outline, prevLayer);
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
