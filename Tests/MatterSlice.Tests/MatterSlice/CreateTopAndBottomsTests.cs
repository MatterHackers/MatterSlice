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
using System.Collections.Generic;

namespace MatterHackers.MatterSlice.Tests
{
	using Polygons = List<List<IntPoint>>;
	using Polygon = List<IntPoint>;

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
				Polygons inset0Outline = PolygonsHelper.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				PartLayers layerData = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(layerData, 400, 3, 0);
				Assert.IsTrue(OnlyHasBottom(layerData, 0));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 1));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 2));
				Assert.IsTrue(OnlyHasInfill(layerData, 3));
			}

			// 3 bottom layers and 1 top layer
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = PolygonsHelper.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				PartLayers layerData = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(layerData, 400, 3, 1);
				Assert.IsTrue(OnlyHasBottom(layerData, 0));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 1));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 2));
				Assert.IsTrue(OnlyHasInfill(layerData, 3));
			}

			// 3 bottom layers and 3 top layers
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = PolygonsHelper.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				PartLayers layerData = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(layerData, 400, 3, 3);
				Assert.IsTrue(OnlyHasBottom(layerData, 0));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 1));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 2));
				Assert.IsTrue(OnlyHasInfill(layerData, 3));
			}
		}

		[Test]
		public void CorrectNumberOfTops()
		{
			// 3 top layers and no bottom layers
			{
				// A simple cube that should have enough bottom layers
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = PolygonsHelper.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				PartLayers layerData = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(layerData, 400, 0, 3);
				Assert.IsTrue(OnlyHasTop(layerData, 9));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 8));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 7));
				Assert.IsTrue(OnlyHasInfill(layerData, 6));
			}

			// 3 top layers and 1 bottom layer
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = PolygonsHelper.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				PartLayers layerData = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(layerData, 400, 3, 1);
				Assert.IsTrue(OnlyHasBottom(layerData, 0));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 1));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 2));
				Assert.IsTrue(OnlyHasInfill(layerData, 3));
			}

			// 3 top layers and 3 bottom layers
			{
				string inset0OutlineString = "x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|";
				Polygons inset0Outline = PolygonsHelper.CreateFromString(inset0OutlineString);
				int numLayers = 10;
				PartLayers layerData = CreateLayerData(inset0Outline, numLayers);
				GenerateLayers(layerData, 400, 3, 3);
				Assert.IsTrue(OnlyHasBottom(layerData, 0));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 1));
				Assert.IsTrue(OnlyHasSolidInfill(layerData, 2));
				Assert.IsTrue(OnlyHasInfill(layerData, 3));
			}
		}

		private static void GenerateLayers(PartLayers layerData, int extrusionWidth, int bottomLayers, int topLayers)
		{
			int numLayers = layerData.Layers.Count;
			for (int i = 0; i < numLayers; i++)
			{
				TopsAndBottoms.GenerateTopAndBottom(i, layerData, extrusionWidth, extrusionWidth, bottomLayers, topLayers);
			}
		}

		private static PartLayers CreateLayerData(Polygons inset0Outline, int numLayers)
		{
			PartLayers layerData = new PartLayers();
			layerData.Layers = new List<SliceLayerParts>();
			for (int i = 0; i < numLayers; i++)
			{
				SliceLayerParts layer = new SliceLayerParts();
				layer.layerSliceData = new List<SliceLayerPart>();
				SliceLayerPart part = new SliceLayerPart();
				part.Insets = new List<Polygons>();
				part.Insets.Add(inset0Outline);
				part.BoundingBox = new Aabb(inset0Outline);
				layer.layerSliceData.Add(part);
				layerData.Layers.Add(layer);
			}
			return layerData;
		}

		private static bool OnlyHasBottom(PartLayers layerData, int layerToCheck)
		{
			return layerData.Layers[layerToCheck].layerSliceData.Count == 1
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidBottomOutlines.Count == 1
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidTopOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidInfillOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].InfillOutlines.Count == 0;
		}

		private static bool OnlyHasTop(PartLayers layerData, int layerToCheck)
		{
			return layerData.Layers[layerToCheck].layerSliceData.Count == 1
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidBottomOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidTopOutlines.Count == 1
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidInfillOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].InfillOutlines.Count == 0;
		}

		private static bool OnlyHasSolidInfill(PartLayers layerData, int layerToCheck)
		{
			return layerData.Layers[layerToCheck].layerSliceData.Count == 1
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidBottomOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidTopOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidInfillOutlines.Count == 1
				&& layerData.Layers[layerToCheck].layerSliceData[0].InfillOutlines.Count == 0;
		}

		private static bool OnlyHasInfill(PartLayers layerData, int layerToCheck)
		{
			return layerData.Layers[layerToCheck].layerSliceData.Count == 1
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidBottomOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidTopOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].SolidInfillOutlines.Count == 0
				&& layerData.Layers[layerToCheck].layerSliceData[0].InfillOutlines.Count == 1;
		}
	}
}