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

        public static void GenerateTopAndBottom(int layerIndex, SliceVolumeStorage storage, int extrusionWidth, ConfigSettings config)
        {
            var downLayerCount = config.numberOfBottomLayers;
            var upLayerCount = config.numberOfTopLayers; 
            
            SliceLayer layer = storage.layers[layerIndex];

			for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
			{
				SliceLayerPart part = layer.parts[partIndex];
				Polygons insetWithOffset = part.Insets[part.Insets.Count - 1].Offset(-extrusionWidth / 2);
				Polygons infillOutlines = new Polygons(insetWithOffset);

				// calculate the bottom outlines
				if (downLayerCount > 0)
				{
					Polygons bottomOutlines = new Polygons(insetWithOffset);

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
					Polygons topOutlines = new Polygons(insetWithOffset);
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
                    var solidInfillOutlines = new Polygons(insetWithOffset);
                    solidInfillOutlines = solidInfillOutlines.CreateDifference(part.SolidBottomOutlines);
                    solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
                    solidInfillOutlines = solidInfillOutlines.CreateDifference(part.SolidTopOutlines);
                    solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);

                    var upStart = layerIndex + 2;
                    var upEnd = layerIndex + upLayerCount + 1;
                    var downStart = layerIndex - 1;
                    var downEnd = layerIndex - downLayerCount;

                    if (upEnd <= storage.layers.Count && downEnd >= 0)
                    {
                        var makeInfillSolid = false;
                        var totalPartsToRemove = new Polygons(insetWithOffset);

                        for (var layerToTest = upStart; layerToTest < upEnd; layerToTest++)
                        {
                            totalPartsToRemove = AddAllOutlines(storage.layers[layerToTest], part, totalPartsToRemove, ref makeInfillSolid, config);
                            totalPartsToRemove = Clipper.CleanPolygons(totalPartsToRemove, cleanDistance_um);
                            if (makeInfillSolid) break;
                        }

                        for (var layerToTest = downStart; layerToTest >= downEnd; layerToTest--)
                        {
                            totalPartsToRemove = AddAllOutlines(storage.layers[layerToTest], part, totalPartsToRemove, ref makeInfillSolid, config);
                            totalPartsToRemove = Clipper.CleanPolygons(totalPartsToRemove, cleanDistance_um);
                            if (makeInfillSolid) break;
                        }

                        if (!makeInfillSolid)
                        {
                            solidInfillOutlines = solidInfillOutlines.CreateDifference(totalPartsToRemove);
                            RemoveSmallAreas(extrusionWidth, solidInfillOutlines);
                        }

                        solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
                    }

                    part.SolidInfillOutlines = solidInfillOutlines;
                    infillOutlines = infillOutlines.CreateDifference(solidInfillOutlines);

                    Polygons totalInfillOutlines = null;
                    double totalInfillArea = 0.0;

                    if (config.infillSolidProportion > 0)
                    {
                        totalInfillOutlines = infillOutlines.CreateUnion(solidInfillOutlines);
                        totalInfillArea = totalInfillOutlines.TotalArea();
                    }

                    if (config.infillSolidProportion > 0)
                    {
                        var solidInfillArea = solidInfillOutlines.TotalArea();
                        if (solidInfillArea > totalInfillArea * config.infillSolidProportion)
                        {
                            solidInfillOutlines = solidInfillOutlines.CreateUnion(infillOutlines);
                            infillOutlines = new Polygons();
                            part.SolidInfillOutlines = solidInfillOutlines;
                        }
                        var solidTopOutlinesArea = part.SolidTopOutlines.TotalArea();
                        if (totalInfillArea < solidTopOutlinesArea * config.infillSolidProportion / 2)
                        {
                            var totalSolidTop = totalInfillOutlines.CreateUnion(part.SolidTopOutlines);
                            part.SolidTopOutlines = totalSolidTop;
                            part.SolidInfillOutlines = new Polygons();
                            infillOutlines = part.InfillOutlines = new Polygons();
                        }
                        var solidBottomOutlinesArea = part.SolidBottomOutlines.TotalArea();
                        if (totalInfillArea < solidBottomOutlinesArea * config.infillSolidProportion / 2)
                        {
                            var totalSolidBottom = totalInfillOutlines.CreateUnion(part.SolidBottomOutlines);
                            part.SolidBottomOutlines = totalSolidBottom;
                            part.SolidInfillOutlines = new Polygons();
                            infillOutlines = part.InfillOutlines = new Polygons();
                        }
                    }
                    if (config.minInfillArea_mm2 > 0)
                    {
                        var infillArea = infillOutlines.TotalArea()/1e6; // convert from um2 to mm2
                        if (infillArea < config.minInfillArea_mm2)
                        {
                            solidInfillOutlines = solidInfillOutlines.CreateUnion(infillOutlines);
                            infillOutlines = new Polygons();
                            part.SolidInfillOutlines = solidInfillOutlines;
                        }
                    }
                }

                RemoveSmallAreas(extrusionWidth, infillOutlines);
                infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);
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

        private static Polygons AddAllOutlines(SliceLayer layerToAdd, SliceLayerPart partToUseAsBounds, Polygons polysToAddTo, ref bool makeInfillSolid, ConfigSettings config)
        {           
            Polygons polysToIntersect = new Polygons();
			for (int partIndex = 0; partIndex < layerToAdd.parts.Count; partIndex++)
			{
                var partToConsider = layerToAdd.parts[partIndex];

                if (config.smallProtrusionProportion > 0)
                {
                    // If the part under consideration intersects the part from the current layer and 
                    // the area of that intersection is less than smallProtrusionProportion % of the part under consideration solidify the infill
                    var intersection = partToUseAsBounds.TotalOutline.CreateIntersection(partToConsider.TotalOutline);
                    if (intersection.Count > 0) // They do intersect
                    {
                        if (intersection.TotalArea() < partToConsider.TotalOutline.TotalArea() * config.smallProtrusionProportion)
                        {
                            makeInfillSolid = true;
                            return polysToAddTo;
                        }
                    }
                }

                if (partToUseAsBounds.BoundingBox.Hit(partToConsider.BoundingBox))
                {
                    polysToIntersect =
                        polysToIntersect.CreateUnion(
                            layerToAdd.parts[partIndex].Insets[partToConsider.Insets.Count - 1]);
                    polysToIntersect = Clipper.CleanPolygons(polysToIntersect, cleanDistance_um);
                }
            }

			polysToAddTo = polysToAddTo.CreateIntersection(polysToIntersect);

			return polysToAddTo;
		}
	}
}