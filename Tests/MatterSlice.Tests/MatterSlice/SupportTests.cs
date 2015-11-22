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

using MatterSlice.ClipperLib;
using NUnit.Framework;
using MatterHackers.MatterSlice;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice.Tests
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    [TestFixture, Category("MatterSlice.SupportTests")]
    public class SupportTests
    {
        [Test]
        public void TestCorrectSupportLayer()
        {
            {
                ConfigSettings config = new ConfigSettings();

                List<Polygons> partOutlines = new List<Polygons>();
                for(int i=0; i<5; i++)
                    partOutlines.Add(new Polygons());

                Polygons cubeOutline = PolygonsHelper.CreateFromString("x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|");
                for (int i = 0; i < 5; i++)
                    partOutlines.Add(cubeOutline);

                PartLayers layerData = CreateLayerData(partOutlines);
                NewSupport supportGenerator = new NewSupport(10, config, layerData);

                // check the all part outlines
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Assert.IsTrue(supportGenerator.allPartOutlines[i].Count == 0);
                    }

                    for (int i = 5; i < 10; i++)
                    {
                        Assert.IsTrue(supportGenerator.allPartOutlines[i].Count == 1);
                        Assert.IsTrue(supportGenerator.allPartOutlines[i][0].Count == 4);
                        Assert.IsTrue(supportGenerator.allPartOutlines[i][0].DescribesSameShape(cubeOutline[0]));
                    }
                }

                // check the potential support outlines
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[i].Count == 0);
                    }
                    Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[4].Count == 1);
                    Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[4][0].Count == 4);
                    Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[4][0].DescribesSameShape(cubeOutline[0]));
                    for (int i = 5; i < 10; i++)
                    {
                        Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[i].Count == 0);
                    }
                }

                // check the required support outlines
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[i].Count == 1);
                        Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[i][0].Count == 4);
                        Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[i][0].DescribesSameShape(cubeOutline[0]));
                    }
                    for (int i = 5; i < 10; i++)
                    {
                        Assert.IsTrue(supportGenerator.allPotentialSupportOutlines[i].Count == 0);
                    }
                }
            }
        }

        private static PartLayers CreateLayerData(List<Polygons> totalLayerOutlines)
        {
            int numLayers = totalLayerOutlines.Count;
            PartLayers layerData = new PartLayers();
            layerData.Layers = new List<SliceLayerParts>();
            for (int layerIndex = 0; layerIndex < numLayers; layerIndex++)
            {
                SliceLayerParts layer = new SliceLayerParts();
                layer.parts = new List<SliceLayerPart>();
                SliceLayerPart part = new SliceLayerPart();
                part.TotalOutline = totalLayerOutlines[layerIndex];
                Inset.GenerateInsets(part, 500, 500, 2);
                layer.parts.Add(part);
                layerData.Layers.Add(layer);
            }
            return layerData;
        }
    }
}