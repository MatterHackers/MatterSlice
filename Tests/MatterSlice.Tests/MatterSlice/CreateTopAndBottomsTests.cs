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
	using Polygons = List<List<IntPoint>>;

	[TestFixture, Category("MatterSlice")]
	public class CreateToAndBottomTests
	{
		[Test]
		public void CorrectNumberOfBottoms()
		{
			// 3 bottom layers and no top layers
			{
				// A simple cube that should have enough bottom layers
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons partOutline = CLPolygonsExtensions.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				ExtruderLayers extruder = CreateLayerData(partOutline, numLayers);
				GenerateLayers(extruder, 400, 3, 0, 0);
				Assert.IsTrue(extruder.OnlyHasBottom(0));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(1));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(2));
				Assert.IsTrue(extruder.OnlyHasInfill(3));
			}

			// 3 bottom layers and 1 top layer
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = CLPolygonsExtensions.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				ExtruderLayers extruder = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(extruder, 400, 3, 1, 0);
				Assert.IsTrue(extruder.OnlyHasBottom(0));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(1));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(2));
				Assert.IsTrue(extruder.OnlyHasInfill(3));
			}

			// 3 bottom layers and 3 top layers
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = CLPolygonsExtensions.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				ExtruderLayers extruder = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(extruder, 400, 3, 3, 0);
				Assert.IsTrue(extruder.OnlyHasBottom(0));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(1));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(2));
				Assert.IsTrue(extruder.OnlyHasInfill(3));
			}
		}

		[Test]
		public void CorrectNumberOfTops()
		{
			// 3 top layers and no bottom layers
			{
				// A simple cube that should have enough bottom layers
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = CLPolygonsExtensions.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				ExtruderLayers extruder = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(extruder, 400, 0, 3, 0);
				for (int i = 0; i < 7; i++)
				{
					Assert.IsTrue(extruder.OnlyHasInfill(i));
				}
				Assert.IsTrue(extruder.OnlyHasFirstTop(7));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(8));
				Assert.IsTrue(extruder.OnlyHasTop(9));
			}

			// 3 bottom layers and 1 top layer
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = CLPolygonsExtensions.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				ExtruderLayers extruder = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(extruder, 400, 3, 1, 0);
				Assert.IsTrue(extruder.OnlyHasBottom(0));
				for (int i = 1; i < 3; i++)
				{
					Assert.IsTrue(extruder.OnlyHasSolidInfill(i));
				}
				for (int i = 3; i < 9; i++)
				{
					Assert.IsTrue(extruder.OnlyHasInfill(i));
				}
				Assert.IsTrue(extruder.OnlyHasTop(9));
			}

			// 3 top layers and 3 bottom layers
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = CLPolygonsExtensions.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				ExtruderLayers extruder = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(extruder, 400, 3, 3, 0);
				Assert.IsTrue(extruder.OnlyHasBottom(0));
				for (int i = 1; i < 3; i++)
				{
					Assert.IsTrue(extruder.OnlyHasSolidInfill(i));
				}
				for (int i = 3; i < 7; i++)
				{
					Assert.IsTrue(extruder.OnlyHasInfill(i));
				}
				Assert.IsTrue(extruder.OnlyHasFirstTop(7));
				Assert.IsTrue(extruder.OnlyHasSolidInfill(8));
				Assert.IsTrue(extruder.OnlyHasTop(9));
			}
		}

		private static ExtruderLayers CreateLayerData(Polygons inset0Outline, int numLayers)
		{
			ExtruderLayers layerData = new ExtruderLayers();
			layerData.Layers = new List<SliceLayer>();
			for (int i = 0; i < numLayers; i++)
			{
				SliceLayer layer = new SliceLayer();
				layer.Islands = new List<LayerIsland>();
				LayerIsland part = new LayerIsland();
				part.InsetToolPaths = new List<Polygons>();
				part.InsetToolPaths.Add(inset0Outline);
				part.BoundingBox = new Aabb(inset0Outline);
				layer.Islands.Add(part);
				layerData.Layers.Add(layer);
			}

			return layerData;
		}

		private static void GenerateLayers(ExtruderLayers extruder, int extrusionWidthUm, int bottomLayers, int topLayers, long infillExtendIntoPerimeter_um)
		{
			int numLayers = extruder.Layers.Count;
			for (int layerIndex = 0; layerIndex < numLayers; layerIndex++)
			{
				extruder.GenerateTopAndBottoms(null, layerIndex, extrusionWidthUm, extrusionWidthUm, bottomLayers, topLayers, infillExtendIntoPerimeter_um);
			}
		}
	}
}