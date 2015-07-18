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

		public static void GenerateTopAndBottom(int layerIndex, SliceVolumeStorage storage, int extrusionWidth, int downLayerCount, int upLayerCount)
		{
			SliceLayer layer = storage.layers[layerIndex];

			for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
			{
				SliceLayerPart part = layer.parts[partIndex];
				Polygons infillOutlines = part.Insets[part.Insets.Count - 1].Offset(-extrusionWidth / 2);

				// calculate the bottom outlines
				if (downLayerCount > 0)
				{
					Polygons bottomOutlines = part.Insets[part.Insets.Count - 1].Offset(-extrusionWidth / 2);

					if (layerIndex - 1 >= 0)
					{
						bottomOutlines = RemoveAdditionalOutlinesForPart(storage.layers[layerIndex - 1], part, bottomOutlines);
						RemoveSmallAreas(extrusionWidth, bottomOutlines);
					}

					infillOutlines = infillOutlines.CreateDifference(bottomOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					part.SolidBottomOutlines = bottomOutlines;
				}

				// calculate the top outlines
				if(upLayerCount > 0)
				{
					Polygons topOutlines = part.Insets[part.Insets.Count - 1].Offset(-extrusionWidth / 2);
					topOutlines = topOutlines.CreateDifference(part.SolidBottomOutlines);
					topOutlines = Clipper.CleanPolygons(topOutlines, cleanDistance_um);

					if (part.Insets.Count > 1)
					{
						// Add thin wall filling by taking the area between the insets.
						Polygons thinWalls = part.Insets[0].Offset(-extrusionWidth / 2).CreateDifference(part.Insets[1].Offset(extrusionWidth / 2));
						topOutlines.AddAll(thinWalls);
					}

					if (layerIndex + 1 < storage.layers.Count)
					{
						topOutlines = RemoveAdditionalOutlinesForPart(storage.layers[layerIndex + 1], part, topOutlines);
						RemoveSmallAreas(extrusionWidth, topOutlines);
					}

					infillOutlines = infillOutlines.CreateDifference(topOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					part.SolidTopOutlines = topOutlines;
				}

				// calculate the solid infill outlines
				if (upLayerCount > 1 || downLayerCount > 1)
				{
					Polygons solidInfillOutlines = part.Insets[part.Insets.Count - 1].Offset(-extrusionWidth / 2);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(part.SolidBottomOutlines);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(part.SolidTopOutlines);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);

					bool haveAddedAnything = false;
					Polygons partsToRemove = new Polygons();

					int upStart = layerIndex + 2;
					int upEnd = Math.Min(layerIndex + upLayerCount + 1, storage.layers.Count);

					for (int layerToTest = upStart; layerToTest < upEnd; layerToTest++)
					{
						partsToRemove = AddAllOutlines(storage.layers[layerToTest], part, partsToRemove, ref haveAddedAnything);
						partsToRemove = Clipper.CleanPolygons(partsToRemove, cleanDistance_um);
					}

					solidInfillOutlines = solidInfillOutlines.CreateDifference(partsToRemove);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);

					int downStart = layerIndex - 1;
					int downEnd = Math.Max(layerIndex - downLayerCount, downLayerCount - 1);

					for (int layerToTest = downStart; layerToTest >= downEnd; layerToTest--)
					{
						partsToRemove = AddAllOutlines(storage.layers[layerToTest], part, partsToRemove, ref haveAddedAnything);
						partsToRemove = Clipper.CleanPolygons(partsToRemove, cleanDistance_um);
					}

					solidInfillOutlines = solidInfillOutlines.CreateDifference(partsToRemove);
					RemoveSmallAreas(extrusionWidth, solidInfillOutlines);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					part.SolidInfillOutlines = solidInfillOutlines;

					infillOutlines = infillOutlines.CreateDifference(solidInfillOutlines);
					RemoveSmallAreas(extrusionWidth, infillOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);
					part.InfillOutlines = infillOutlines;
				}
				else
				{
					RemoveSmallAreas(extrusionWidth, infillOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);
					part.InfillOutlines = infillOutlines;
				}

				part.InfillOutlines = infillOutlines;
			}
		}

		private static void RemoveSmallAreas(int extrusionWidth, Polygons solidInfillOutlinesUp)
		{
			double minAreaSize = (2 * Math.PI * (extrusionWidth / 1000.0) * (extrusionWidth / 1000.0)) * 0.3;
			for (int outlineIndex = 0; outlineIndex < solidInfillOutlinesUp.Count; outlineIndex++)
			{
				double area = Math.Abs(solidInfillOutlinesUp[outlineIndex].Area()) / 1000.0 / 1000.0;
				if (area < minAreaSize) // Only create an up/down Outline if the area is large enough. So you do not create tiny blobs of "trying to fill"
				{
					solidInfillOutlinesUp.RemoveAt(outlineIndex);
					outlineIndex -= 1;
				}
			}
		}

		private static Polygons RemoveAdditionalOutlinesForPart(SliceLayer layerToSubtract, SliceLayerPart partToUseAsBounds, Polygons polygonsToSubtractFrom)
		{
			for (int partIndex = 0; partIndex < layerToSubtract.parts.Count; partIndex++)
			{
				if (partToUseAsBounds.BoundingBox.Hit(layerToSubtract.parts[partIndex].BoundingBox))
				{
					polygonsToSubtractFrom = polygonsToSubtractFrom.CreateDifference(layerToSubtract.parts[partIndex].Insets[layerToSubtract.parts[partIndex].Insets.Count - 1]);

					polygonsToSubtractFrom = Clipper.CleanPolygons(polygonsToSubtractFrom, cleanDistance_um);
				}
			}

			return polygonsToSubtractFrom;
		}

		private static Polygons AddAllOutlines(SliceLayer layerToAdd, SliceLayerPart partToUseAsBounds, Polygons polysToAddTo, ref bool haveAddedAnything)
		{
			Polygons polysToIntersect = new Polygons();
			bool didAdd = false;
			for (int partIndex = 0; partIndex < layerToAdd.parts.Count; partIndex++)
			{
				if (partToUseAsBounds.BoundingBox.Hit(layerToAdd.parts[partIndex].BoundingBox))
				{
					polysToIntersect = polysToIntersect.CreateUnion(layerToAdd.parts[partIndex].Insets[layerToAdd.parts[partIndex].Insets.Count - 1]);
					polysToIntersect = Clipper.CleanPolygons(polysToIntersect, cleanDistance_um);
				}
			}

			if (haveAddedAnything)
			{
				polysToAddTo = polysToAddTo.CreateIntersection(polysToIntersect);
			}
			else
			{
				polysToAddTo = polysToIntersect;
				haveAddedAnything = true;
			}

			return polysToAddTo;
		}
	}
}