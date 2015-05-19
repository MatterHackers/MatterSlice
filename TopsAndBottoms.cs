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
		public static void Generate(int layerIndex, SliceVolumeStorage storage, int extrusionWidth, int downSkinCount, int upSkinCount)
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

				if (layerIndex - downSkinCount >= 0)
				{
					SliceLayer layer2 = storage.layers[layerIndex - downSkinCount];
					for (int partIndex2 = 0; partIndex2 < layer2.parts.Count; partIndex2++)
					{
						if (part.boundaryBox.hit(layer2.parts[partIndex2].boundaryBox))
						{
							downOutlines = downOutlines.CreateDifference(layer2.parts[partIndex2].insets[layer2.parts[partIndex2].insets.Count - 1]);
						}
					}
				}

				if (layerIndex + upSkinCount < storage.layers.Count)
				{
					SliceLayer layer2 = storage.layers[layerIndex + upSkinCount];
					for (int partIndex2 = 0; partIndex2 < layer2.parts.Count; partIndex2++)
					{
						if (part.boundaryBox.hit(layer2.parts[partIndex2].boundaryBox))
						{
							topOutlines = topOutlines.CreateDifference(layer2.parts[partIndex2].insets[layer2.parts[partIndex2].insets.Count - 1]);
						}
					}
				}

				part.topAndBottomOutlines = topOutlines.CreateUnion(downOutlines);

				double minAreaSize = (2 * Math.PI * (extrusionWidth / 1000.0) * (extrusionWidth / 1000.0)) * 0.3;
				for (int outlineIndex = 0; outlineIndex < part.topAndBottomOutlines.Count; outlineIndex++)
				{
					double area = Math.Abs(part.topAndBottomOutlines[outlineIndex].Area()) / 1000.0 / 1000.0;
					if (area < minAreaSize) // Only create an up/down skin if the area is large enough. So you do not create tiny blobs of "trying to fill"
					{
						part.topAndBottomOutlines.RemoveAt(outlineIndex);
						outlineIndex -= 1;
					}
				}
			}
		}

		public static void GenerateSparse(int layerIndex, SliceVolumeStorage storage, int extrusionWidth, int downSkinCount, int upSkinCount)
		{
			SliceLayer layer = storage.layers[layerIndex];

			for (int partNr = 0; partNr < layer.parts.Count; partNr++)
			{
				SliceLayerPart part = layer.parts[partNr];

				Polygons sparse = part.insets[part.insets.Count - 1].Offset(-extrusionWidth / 2);
				Polygons downskin = sparse;
				Polygons upskin = sparse;

				if ((int)(layerIndex - downSkinCount) >= 0)
				{
					SliceLayer layer2 = storage.layers[layerIndex - downSkinCount];
					for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
					{
						if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
						{
							if (layer2.parts[partNr2].insets.Count > 1)
							{
								downskin = downskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 2]);
							}
							else
							{
								downskin = downskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1]);
							}
						}
					}
				}
				if ((int)(layerIndex + upSkinCount) < (int)storage.layers.Count)
				{
					SliceLayer layer2 = storage.layers[layerIndex + upSkinCount];
					for (int partNr2 = 0; partNr2 < layer2.parts.Count; partNr2++)
					{
						if (part.boundaryBox.hit(layer2.parts[partNr2].boundaryBox))
						{
							if (layer2.parts[partNr2].insets.Count > 1)
							{
								upskin = upskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 2]);
							}
							else
							{
								upskin = upskin.CreateDifference(layer2.parts[partNr2].insets[layer2.parts[partNr2].insets.Count - 1]);
							}
						}
					}
				}

				Polygons result = upskin.CreateUnion(downskin);

				double minAreaSize = 3.0;//(2 * M_PI * ((double)(config.extrusionWidth) / 1000.0) * ((double)(config.extrusionWidth) / 1000.0)) * 3;
				for (int i = 0; i < result.Count; i++)
				{
					double area = Math.Abs(result[i].Area()) / 1000.0 / 1000.0;
					if (area < minAreaSize) /* Only create an up/down skin if the area is large enough. So you do not create tiny blobs of "trying to fill" */
					{
						result.RemoveAt(i);
						i -= 1;
					}
				}

				part.sparseOutline = sparse.CreateDifference(result);
			}
		}
	}
}