/*
This file is part of MatterSlice. A commandline utility for
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using MatterSlice.ClipperLib;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using Polygons = List<List<IntPoint>>;

	public static class TopsAndBottoms
	{
		readonly static double cleanDistance_um = 10;

		public static void Generate(int layerIndex, SliceVolumeStorage storage, int extrusionWidth, int downLayerCount, int upLayerCount)
		{
			SliceLayer layer = storage.layers[layerIndex];

			for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
			{
				SliceLayerPart part = layer.parts[partIndex];

				Polygons topOutlines = part.insets[part.insets.Count - 1].Offset(-extrusionWidth / 2);
				Polygons downOutlines = topOutlines;

				if (part.insets.Count > 1)
				{
					// Add thin wall filling by taking the area between the insets.
					Polygons thinWalls = part.insets[0].Offset(-extrusionWidth / 2).CreateDifference(part.insets[1].Offset(extrusionWidth / 2));
					topOutlines.AddAll(thinWalls);
					downOutlines.AddAll(thinWalls);
				}

				if (layerIndex - downLayerCount >= 0)
				{
					SliceLayer bottomLayer = storage.layers[layerIndex - downLayerCount];
					for (int bottomLayerPartIndex = 0; bottomLayerPartIndex < bottomLayer.parts.Count; bottomLayerPartIndex++)
					{
						if (part.boundingBox.Hit(bottomLayer.parts[bottomLayerPartIndex].boundingBox))
						{
							downOutlines = downOutlines.CreateDifference(bottomLayer.parts[bottomLayerPartIndex].insets[bottomLayer.parts[bottomLayerPartIndex].insets.Count - 1]);

							downOutlines = Clipper.CleanPolygons(downOutlines, cleanDistance_um);
						}
					}
				}

				if (layerIndex + upLayerCount < storage.layers.Count)
				{
					SliceLayer upLayer = storage.layers[layerIndex + upLayerCount];
					for (int upLayerPartIndex = 0; upLayerPartIndex < upLayer.parts.Count; upLayerPartIndex++)
					{
						if (part.boundingBox.Hit(upLayer.parts[upLayerPartIndex].boundingBox))
						{
							topOutlines = topOutlines.CreateDifference(upLayer.parts[upLayerPartIndex].insets[upLayer.parts[upLayerPartIndex].insets.Count - 1]);

							topOutlines = Clipper.CleanPolygons(topOutlines, cleanDistance_um);
						}
					}
				}

				part.solidTopAndBottomOutlines = topOutlines.CreateUnion(downOutlines);

				double minAreaSize = (2 * Math.PI * (extrusionWidth / 1000.0) * (extrusionWidth / 1000.0)) * 0.3;
				for (int outlineIndex = 0; outlineIndex < part.solidTopAndBottomOutlines.Count; outlineIndex++)
				{
					double area = Math.Abs(part.solidTopAndBottomOutlines[outlineIndex].Area()) / 1000.0 / 1000.0;
					if (area < minAreaSize) // Only create an up/down Outline if the area is large enough. So you do not create tiny blobs of "trying to fill"
					{
						part.solidTopAndBottomOutlines.RemoveAt(outlineIndex);
						outlineIndex -= 1;
					}
				}
			}
		}

		public static void GenerateInfillAreas(int layerIndex, SliceVolumeStorage storage, int extrusionWidth, int downLayerCount, int upLayerCount)
		{
			SliceLayer layer = storage.layers[layerIndex];

			for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
			{
				SliceLayerPart part = layer.parts[partIndex];

				Polygons infillOutlines = part.insets[part.insets.Count - 1].Offset(-extrusionWidth / 2);
				Polygons downOutlines = infillOutlines;
				Polygons upOutlines = infillOutlines;

				if ((int)(layerIndex - downLayerCount) >= 0)
				{
					SliceLayer downLayer = storage.layers[layerIndex - downLayerCount];
					for (int downLayerPartIndex = 0; downLayerPartIndex < downLayer.parts.Count; downLayerPartIndex++)
					{
						if (part.boundingBox.Hit(downLayer.parts[downLayerPartIndex].boundingBox))
						{
							if (downLayer.parts[downLayerPartIndex].insets.Count > 1)
							{
								downOutlines = downOutlines.CreateDifference(downLayer.parts[downLayerPartIndex].insets[downLayer.parts[downLayerPartIndex].insets.Count - 2]);
							}
							else
							{
								downOutlines = downOutlines.CreateDifference(downLayer.parts[downLayerPartIndex].insets[downLayer.parts[downLayerPartIndex].insets.Count - 1]);
							}
						}

						downOutlines = Clipper.CleanPolygons(downOutlines, cleanDistance_um);
					}
				}

				if ((int)(layerIndex + upLayerCount) < (int)storage.layers.Count)
				{
					SliceLayer upLayer = storage.layers[layerIndex + upLayerCount];
					for (int upLayerPartIndex = 0; upLayerPartIndex < upLayer.parts.Count; upLayerPartIndex++)
					{
						if (part.boundingBox.Hit(upLayer.parts[upLayerPartIndex].boundingBox))
						{
							if (upLayer.parts[upLayerPartIndex].insets.Count > 1)
							{
								upOutlines = upOutlines.CreateDifference(upLayer.parts[upLayerPartIndex].insets[upLayer.parts[upLayerPartIndex].insets.Count - 2]);
							}
							else
							{
								upOutlines = upOutlines.CreateDifference(upLayer.parts[upLayerPartIndex].insets[upLayer.parts[upLayerPartIndex].insets.Count - 1]);
							}
						}

						upOutlines = Clipper.CleanPolygons(upOutlines, cleanDistance_um);
					}
				}

				Polygons result = upOutlines.CreateUnion(downOutlines);

				double minAreaSize = 3.0;//(2 * M_PI * ((double)(config.extrusionWidth) / 1000.0) * ((double)(config.extrusionWidth) / 1000.0)) * 3;
				for (int polygonIndex = 0; polygonIndex < result.Count; polygonIndex++)
				{
					double area = Math.Abs(result[polygonIndex].Area()) / 1000.0 / 1000.0;
					if (area < minAreaSize) /* Only create an up/down outlines if the area is large enough. So you do not create tiny blobs of "trying to fill" */
					{
						result.RemoveAt(polygonIndex);
						polygonIndex -= 1;
					}
				}

				part.infillOutline = infillOutlines.CreateDifference(result);
			}
		}
	}
}