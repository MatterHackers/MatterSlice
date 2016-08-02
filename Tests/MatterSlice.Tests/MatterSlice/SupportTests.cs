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

using MSClipperLib;
using NUnit.Framework;
using MatterHackers.MatterSlice;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice.Tests
{
	using System;
	using Polygon = List<MSClipperLib.IntPoint>;
	using Polygons = List<List<MSClipperLib.IntPoint>>;

	[TestFixture, Category("MatterSlice.SupportTests")]
	public class SupportTests
	{
		[Test]
		public void TestCorrectSupportLayer()
		{
			// test the supports for a simple cube in the air
			{
				ConfigSettings config = new ConfigSettings();
				config.LayerThickness = .5;
				config.SupportXYDistanceFromObject = 0;
				config.SupportInterfaceLayers = 0;
				config.MinimizeSupportColumns = false;

				List<Polygons> partOutlines = new List<Polygons>();
				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(new Polygons());
				}

				Polygons cubeOutline = PolygonsHelper.CreateFromString("x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|");
				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(cubeOutline);
				}

				ExtruderLayers layerData = CreateLayerData(partOutlines);
				NewSupport supportGenerator = new NewSupport(config, new List<ExtruderLayers>() { layerData }, 0);

				Polygons cubeOutlineResults = PolygonsHelper.CreateFromString("x:200, y:200,x:9800, y:200,x:9800, y:9800,x:200, y:9800,|");

				// check the all part outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 4, 4, 4, 4, 4, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, };
					CheckLayers(supportGenerator.insetPartOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				// check the potential support outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, cubeOutlineResults, null, null, null, null, null };
					CheckLayers(supportGenerator.allPotentialSupportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				// check the required support outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, cubeOutlineResults, null, null, null, null, null };
					CheckLayers(supportGenerator.allRequiredSupportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				// check the generated support outlines
				{
					List<int> polygonsCounts = new List<int> { 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 4, 4, 4, 4, 4, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, null, null, null, null, null };
					CheckLayers(supportGenerator.supportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				// check the interface support outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, null, null, null, null, null };
					CheckLayers(supportGenerator.interfaceLayers, polygonsCounts, polygon0Counts, poly0Paths);
				}
			}

			// test the supports for a cube that is 1/2 width just under the main part
			{
				ConfigSettings config = new ConfigSettings();
				config.SupportInterfaceLayers = 0;
				config.LayerThickness = .5;
				config.SupportXYDistanceFromObject = .1;

				// 14 XXXXXXXXXXXXXXXXXXXX
				// 13 XXXXXXXXXXXXXXXXXXXX
				// 12 XXXXXXXXXXXXXXXXXXXX
				// 11 XXXXXXXXXXXXXXXXXXXX
				// 10 XXXXXXXXXXXXXXXXXXXX
				// 9  XXXXXXXXXX           <- interface layer
				// 8  XXXXXXXXXX           <- interface layer
				// 7  XXXXXXXXXX     ^ - requires support  
				// 6  XXXXXXXXXX
				// 5  XXXXXXXXXX
				// 4             <- interface layer
				// 3             <- interface layer
				// 2      ^ - requires support  
				// 1 
				// 0 

				List<Polygons> partOutlines = new List<Polygons>();
				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(new Polygons());
				}

				Polygons halfCubeOutline = PolygonsHelper.CreateFromString("x:0, y:0,x:5000, y:0,x:5000, y:10000,x:0, y:10000,|");
				Polygons halfCubeOutlineResults = halfCubeOutline.Offset(-200);
				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(halfCubeOutline);
				}

				Polygons cubeOutline = PolygonsHelper.CreateFromString("x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|");
				Polygons cubeOutlineResults = cubeOutline.Offset(-200);
				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(cubeOutline);
				}

				ExtruderLayers layerData = CreateLayerData(partOutlines);
				NewSupport supportGenerator = new NewSupport(config, new List<ExtruderLayers>() { layerData }, 1);

				// check the all part outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0,
						1, 1, 1, 1, 1,
						1, 1, 1, 1, 1,};
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0,
						4, 4, 4, 4, 4,
						4, 4, 4, 4, 4,};
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null,
						halfCubeOutlineResults, halfCubeOutlineResults, halfCubeOutlineResults, halfCubeOutlineResults, halfCubeOutlineResults,
						cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, };
					CheckLayers(supportGenerator.insetPartOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				Polygons layer9Support = PolygonsHelper.CreateFromString("x:5000, y:200,x:9800, y:200,x:9800, y:9800,x:5000, y:9800,|");
				// check the potential support outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 4, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, halfCubeOutlineResults, null, null, null, null, layer9Support, null, null, null, null, null };
					CheckLayers(supportGenerator.allPotentialSupportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				// check the required support outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 4, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, halfCubeOutlineResults, null, null, null, null, layer9Support, null, null, null, null, null };
					CheckLayers(supportGenerator.allRequiredSupportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				if (false)
				{
					// check the generated support outlines
					{
						List<int> polygonsCounts = new List<int> { 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, };
						List<int> polygon0Counts = new List<int> { 4, 4, 4, 4, 4, 8, 8, 8, 0, 0, 0, 0, 0, 0, 0, };
						List<Polygons> poly0Paths = new List<Polygons>() { cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, cubeOutlineResults, null, null, null, null, null };
						CheckLayers(supportGenerator.supportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
					}

					// check the interface support outlines
					{
						List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
						List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
						List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, null, null, null, null, null };
						CheckLayers(supportGenerator.interfaceLayers, polygonsCounts, polygon0Counts, poly0Paths);
					}
				}
			}
		}

		[Test]
		public void TestInternalSupportCanBeDisabled()
		{
			// test the supports for a cube that is 1/2 width just under the main part
			{
				ConfigSettings config = new ConfigSettings();
				config.SupportInterfaceLayers = 0;
				config.LayerThickness = .5;
				config.SupportXYDistanceFromObject = .1;
				config.GenerateInternalSupport = false;

				// 19      XXXXXXXXXX
				// 18      XXXXXXXXXX
				// 17      XXXXXXXXXX
				// 16      XXXXXXXXXX
				// 15      XXXXXXXXXX  
				// 14            ^ - no support, internal
				// 13
				// 12
				// 11 
				// 10
				// 9  XXXXXXXXXXXXXXXXXXXX
				// 8  XXXXXXXXXXXXXXXXXXXX
				// 7  XXXXXXXXXXXXXXXXXXXX
				// 6  XXXXXXXXXXXXXXXXXXXX
				// 5  XXXXXXXXXXXXXXXXXXXX <- at air gap height
				// 4                        <- interface layer
				// 3                        <- interface layer
				// 2            ^ - requires support  
				// 1 
				// 0
			}
		}

		[Test]
		public void TestBottomLayerAirGap()
		{
			// test the supports for a cube that is 1/2 width just under the main part
			{
				ConfigSettings config = new ConfigSettings();
				config.SupportInterfaceLayers = 2;
				config.LayerThickness = .5;
				config.SupportXYDistanceFromObject = .1;
				config.MinimizeSupportColumns = false;

				// 14      XXXXXXXXXX
				// 13      XXXXXXXXXX
				// 12      XXXXXXXXXX
				// 11      XXXXXXXXXX
				// 10      XXXXXXXXXX  <- at air gap height
				// 9                        <- interface layer
				// 8                        <- interface layer
				// 7            ^ - requires support  
				// 6 
				// 5                        <- at air gap height
				// 4  XXXXXXXXXXXXXXXXXXXX
				// 3  XXXXXXXXXXXXXXXXXXXX
				// 1  XXXXXXXXXXXXXXXXXXXX
				// 1  XXXXXXXXXXXXXXXXXXXX
				// 0  XXXXXXXXXXXXXXXXXXXX

				List<Polygons> partOutlines = new List<Polygons>();
				Polygons bottomCubeOutline = PolygonsHelper.CreateFromString("x:0, y:0,x:10000, y:0,x:10000, y:10000,x:0, y:10000,|");
				Polygons bottomCubeOutlineResults = bottomCubeOutline.Offset(-200);
				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(bottomCubeOutline);
				}

				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(new Polygons());
				}

				Polygons topCubeOutline = PolygonsHelper.CreateFromString("x:2500, y:2500,x:7500, y:2500,x:7500, y:7500,x:2500, y:7500,|");
				Polygons topCubeOutlineResults = topCubeOutline.Offset(-200);
				for (int i = 0; i < 5; i++)
				{
					partOutlines.Add(topCubeOutline);
				}

				ExtruderLayers layerData = CreateLayerData(partOutlines);
				NewSupport supportGenerator = new NewSupport(config, new List<ExtruderLayers>() { layerData }, 1);

				// check the all part outlines
				{
					List<int> polygonsCounts = new List<int> {1, 1, 1, 1, 1,
						0, 0, 0, 0, 0,
						1, 1, 1, 1, 1,};
					List<int> polygon0Counts = new List<int> { 4, 4, 4, 4, 4,
						0, 0, 0, 0, 0,
						4, 4, 4, 4, 4,};
					List<Polygons> poly0Paths = new List<Polygons>() {bottomCubeOutlineResults, bottomCubeOutlineResults, bottomCubeOutlineResults, bottomCubeOutlineResults, bottomCubeOutlineResults,
						null, null, null, null, null,
						topCubeOutlineResults, topCubeOutlineResults, topCubeOutlineResults, topCubeOutlineResults, topCubeOutlineResults, };
					CheckLayers(supportGenerator.insetPartOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				// check the potential support outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, null, null, null, null, topCubeOutlineResults, null, null, null, null, null };
					CheckLayers(supportGenerator.allPotentialSupportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				// check the required support outlines
				{
					List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, };
					List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, };
					List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, null, null, null, null, topCubeOutlineResults, null, null, null, null, null };
					CheckLayers(supportGenerator.allRequiredSupportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
				}

				{
					Polygons expectedSupportOutlines = topCubeOutlineResults.Offset(1000);
					// check the air gapped bottom support outlines (only 5)
					{
						List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
						List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
						List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, expectedSupportOutlines, null, null, null, null, null, null };
						CheckLayers(supportGenerator.airGappedBottomOutlines, polygonsCounts, polygon0Counts, poly0Paths);
					}


					// check the generated support outlines (only 6 and 7)
					{
						List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, };
						List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 0, 4, 4, 0, 0, 0, 0, 0, 0, 0, };
						List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, null, expectedSupportOutlines, expectedSupportOutlines, null, null, null, null };
						CheckLayers(supportGenerator.supportOutlines, polygonsCounts, polygon0Counts, poly0Paths);
					}

					// check the interface support outlines (8 and 9)
					{
						List<int> polygonsCounts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, };
						List<int> polygon0Counts = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 0, 0, 0, 0, 0, };
						List<Polygons> poly0Paths = new List<Polygons>() { null, null, null, null, null, null, null, null, expectedSupportOutlines, expectedSupportOutlines, null, null, null, null, null, };
						CheckLayers(supportGenerator.interfaceLayers, polygonsCounts, polygon0Counts, poly0Paths);
					}
				}
			}
		}
		private void CheckLayers(List<Polygons> polygonsToValidate, List<int> polygonsCounts, List<int> polygon0Counts, List<Polygons> poly0Paths)
		{
			for (int i = 0; i < polygonsToValidate.Count; i++)
			{
				Assert.IsTrue(polygonsToValidate[i].Count == polygonsCounts[i]);
				if (polygonsToValidate[i].Count > 0)
				{
					Assert.IsTrue(polygonsToValidate[i][0].Count == polygon0Counts[i]);
					Assert.IsTrue(polygonsToValidate[i][0].DescribesSameShape(poly0Paths[i][0]));
				}
			}
		}

		private static ExtruderLayers CreateLayerData(List<Polygons> totalLayerOutlines)
		{
			int numLayers = totalLayerOutlines.Count;
			ExtruderLayers layerData = new ExtruderLayers();
			layerData.Layers = new List<SliceLayer>();
			for (int layerIndex = 0; layerIndex < numLayers; layerIndex++)
			{
				SliceLayer layer = new SliceLayer();
				layer.AllOutlines = totalLayerOutlines[layerIndex];
				layerData.Layers.Add(layer);
			}
			return layerData;
		}
	}
}
