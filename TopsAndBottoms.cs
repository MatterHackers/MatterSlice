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

		public static void GenerateTopAndBottom(int layerIndex, PartLayers storage, int extrusionWidth_um, int outerPerimeterWidth_um, int downLayerCount, int upLayerCount)
		{
			SliceLayerParts layer = storage.Layers[layerIndex];

			for (int partIndex = 0; partIndex < layer.layerSliceData.Count; partIndex++)
			{
				MeshLayerData part = layer.layerSliceData[partIndex];
				// this is the entire extrusion width to make sure we are outside of the extrusion line
				Polygons insetWithOffset = part.Insets[part.Insets.Count - 1].Offset(-extrusionWidth_um);
				Polygons infillOutlines = new Polygons(insetWithOffset);

				// calculate the bottom outlines
				if (downLayerCount > 0)
				{
					Polygons bottomOutlines = new Polygons(insetWithOffset);

					if (layerIndex - 1 >= 0)
					{
						bottomOutlines = RemoveAdditionalOutlinesForPart(storage.Layers[layerIndex - 1], part, bottomOutlines);
						RemoveSmallAreas(extrusionWidth_um, bottomOutlines);
					}

					infillOutlines = infillOutlines.CreateDifference(bottomOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					part.SolidBottomOutlines = bottomOutlines;
				}

				// calculate the top outlines
				if(upLayerCount > 0)
				{
					Polygons topOutlines = new Polygons(insetWithOffset);
					topOutlines = topOutlines.CreateDifference(part.SolidBottomOutlines);
					topOutlines = Clipper.CleanPolygons(topOutlines, cleanDistance_um);

					if (part.Insets.Count > 1)
					{
						// Add thin wall filling by taking the area between the insets.
						Polygons thinWalls = part.Insets[0].Offset(-outerPerimeterWidth_um / 2).CreateDifference(part.Insets[1].Offset(extrusionWidth_um / 2));
						topOutlines.AddAll(thinWalls);
					}

					for (int insetIndex = 1; insetIndex < part.Insets.Count-1; insetIndex++)
					{
						Polygons thinWalls = part.Insets[insetIndex].Offset(-extrusionWidth_um / 2).CreateDifference(part.Insets[insetIndex+1].Offset(extrusionWidth_um / 2));
						topOutlines.AddAll(thinWalls);
					}

					if (layerIndex + 1 < storage.Layers.Count)
					{
						// Remove the top layer that is above this one to get only the data that is a top layer on this layer.
						topOutlines = RemoveAdditionalOutlinesForPart(storage.Layers[layerIndex + 1], part, topOutlines);
					}

					RemoveSmallAreas(extrusionWidth_um, topOutlines);

					infillOutlines = infillOutlines.CreateDifference(topOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					part.SolidTopOutlines = topOutlines;
				}

				// calculate the solid infill outlines
				if (upLayerCount > 1 || downLayerCount > 1)
				{
					Polygons solidInfillOutlines = new Polygons(insetWithOffset);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(part.SolidBottomOutlines);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(part.SolidTopOutlines);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);

					int upEnd = layerIndex + upLayerCount + 1;
					if (upEnd <= storage.Layers.Count && layerIndex - downLayerCount >= 0)
					{
						Polygons totalPartsToRemove = new Polygons(insetWithOffset);

						int upStart = layerIndex + 2;

						for (int layerToTest = upStart; layerToTest < upEnd; layerToTest++)
						{
							totalPartsToRemove = AddAllOutlines(storage.Layers[layerToTest], part, totalPartsToRemove);
							totalPartsToRemove = Clipper.CleanPolygons(totalPartsToRemove, cleanDistance_um);
						}

						int downStart = layerIndex - 1;
						int downEnd = layerIndex - downLayerCount;

						for (int layerToTest = downStart; layerToTest >= downEnd; layerToTest--)
						{
							totalPartsToRemove = AddAllOutlines(storage.Layers[layerToTest], part, totalPartsToRemove);
							totalPartsToRemove = Clipper.CleanPolygons(totalPartsToRemove, cleanDistance_um);
						}

						solidInfillOutlines = solidInfillOutlines.CreateDifference(totalPartsToRemove);
						RemoveSmallAreas(extrusionWidth_um, solidInfillOutlines);

						solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					}

					part.SolidInfillOutlines = solidInfillOutlines;
					infillOutlines = infillOutlines.CreateDifference(solidInfillOutlines);
				}

				RemoveSmallAreas(extrusionWidth_um, infillOutlines);
				infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);
				part.InfillOutlines = infillOutlines;
			}
		}

		private static void RemoveSmallAreas(int extrusionWidth, Polygons solidInfillOutlinesUp)
		{
			double minAreaSize = (2 * Math.PI * (extrusionWidth / 1000.0) * (extrusionWidth / 1000.0)) * 0.1;
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

		private static Polygons RemoveAdditionalOutlinesForPart(SliceLayerParts layerToSubtract, MeshLayerData partToUseAsBounds, Polygons polygonsToSubtractFrom)
		{
			for (int partIndex = 0; partIndex < layerToSubtract.layerSliceData.Count; partIndex++)
			{
				if (partToUseAsBounds.BoundingBox.Hit(layerToSubtract.layerSliceData[partIndex].BoundingBox))
				{
					polygonsToSubtractFrom = polygonsToSubtractFrom.CreateDifference(layerToSubtract.layerSliceData[partIndex].Insets[layerToSubtract.layerSliceData[partIndex].Insets.Count - 1]);

					polygonsToSubtractFrom = Clipper.CleanPolygons(polygonsToSubtractFrom, cleanDistance_um);
				}
			}

			return polygonsToSubtractFrom;
		}

		private static Polygons AddAllOutlines(SliceLayerParts layerToAdd, MeshLayerData partToUseAsBounds, Polygons polysToAddTo)
		{
			Polygons polysToIntersect = new Polygons();
			for (int partIndex = 0; partIndex < layerToAdd.layerSliceData.Count; partIndex++)
			{
				if (partToUseAsBounds.BoundingBox.Hit(layerToAdd.layerSliceData[partIndex].BoundingBox))
				{
					polysToIntersect = polysToIntersect.CreateUnion(layerToAdd.layerSliceData[partIndex].Insets[layerToAdd.layerSliceData[partIndex].Insets.Count - 1]);
					polysToIntersect = Clipper.CleanPolygons(polysToIntersect, cleanDistance_um);
				}
			}

			polysToAddTo = polysToAddTo.CreateIntersection(polysToIntersect);

			return polysToAddTo;
		}
	}
}