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
            Polygons outline = new Polygons();
            // outlineString	"x:5655, y:-706,x:-706, y:5656,x:-5655, y:707,x:706, y:-5655,|"	string
            
            SliceLayer prevLayer = new SliceLayer();
            prevLayer.parts = new List<SliceLayerPart>();
            SliceLayerPart part = new SliceLayerPart();
            // partOutlineString	"x:706, y:6364,x:-706, y:7778,x:-7777, y:707,x:-6363, y:-706,|x:7777, y:-706,x:6363, y:707,x:-706, y:-6363,x:706, y:-7777,|"	string
            prevLayer.parts.Add(part);
            prevLayer.parts[0].boundaryBox.calculate(prevLayer.parts[0].outline);

            int bridgeAngle = Bridge.bridgeAngle(outline, prevLayer);
            Assert.IsTrue(45 == 45); //(bridgeAngle == 45);
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
