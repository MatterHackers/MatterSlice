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

		public static void Generate(int layerIndex, ExtruderLayers extruder, int extrusionWidth_um, int outerPerimeterWidth_um, int downLayerCount, int upLayerCount)
		{
			SliceLayer layer = extruder.Layers[layerIndex];

			for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				LayerIsland island = layer.Islands[islandIndex];
				// this is the entire extrusion width to make sure we are outside of the extrusion line
				Polygons insetWithOffset = island.InsetToolPaths[island.InsetToolPaths.Count - 1].Offset(-extrusionWidth_um);
				Polygons infillOutlines = new Polygons(insetWithOffset);

				// calculate the bottom outlines
				if (downLayerCount > 0)
				{
					Polygons bottomOutlines = new Polygons(insetWithOffset);

					if (layerIndex - 1 >= 0)
					{
						bottomOutlines = RemoveIslandsFromPolygons(extruder.Layers[layerIndex - 1].Islands, island.BoundingBox, bottomOutlines);
						RemoveSmallAreas(extrusionWidth_um, bottomOutlines);
					}

					infillOutlines = infillOutlines.CreateDifference(bottomOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					island.SolidBottomToolPaths = bottomOutlines;
				}

				// calculate the top outlines
				if(upLayerCount > 0)
				{
					Polygons topOutlines = new Polygons(insetWithOffset);
					topOutlines = topOutlines.CreateDifference(island.SolidBottomToolPaths);
					topOutlines = Clipper.CleanPolygons(topOutlines, cleanDistance_um);

					if (island.InsetToolPaths.Count > 1)
					{
						// Add thin wall filling by taking the area between the insets.
						Polygons thinWalls = island.InsetToolPaths[0].Offset(-outerPerimeterWidth_um / 2).CreateDifference(island.InsetToolPaths[1].Offset(extrusionWidth_um / 2));
						topOutlines.AddAll(thinWalls);
					}

					for (int insetIndex = 1; insetIndex < island.InsetToolPaths.Count-1; insetIndex++)
					{
						Polygons thinWalls = island.InsetToolPaths[insetIndex].Offset(-extrusionWidth_um / 2).CreateDifference(island.InsetToolPaths[insetIndex+1].Offset(extrusionWidth_um / 2));
						topOutlines.AddAll(thinWalls);
					}

					if (layerIndex + 1 < extruder.Layers.Count)
					{
						// Remove the top layer that is above this one to get only the data that is a top layer on this layer.
						topOutlines = RemoveIslandsFromPolygons(extruder.Layers[layerIndex + 1].Islands, island.BoundingBox, topOutlines);
					}

					RemoveSmallAreas(extrusionWidth_um, topOutlines);

					infillOutlines = infillOutlines.CreateDifference(topOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					island.SolidTopToolPaths = topOutlines;
				}

				// calculate the solid infill outlines
				if (upLayerCount > 1 || downLayerCount > 1)
				{
					Polygons solidInfillOutlines = new Polygons(insetWithOffset);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(island.SolidBottomToolPaths);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(island.SolidTopToolPaths);
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);

					int upEnd = layerIndex + upLayerCount + 1;
					if (upEnd <= extruder.Layers.Count && layerIndex - downLayerCount >= 0)
					{
						Polygons totalPartsToRemove = new Polygons(insetWithOffset);

						int upStart = layerIndex + 2;

						for (int layerToTest = upStart; layerToTest < upEnd; layerToTest++)
						{
							totalPartsToRemove = AddIslandsToPolygons(extruder.Layers[layerToTest].Islands, island.BoundingBox, totalPartsToRemove);
							totalPartsToRemove = Clipper.CleanPolygons(totalPartsToRemove, cleanDistance_um);
						}

						int downStart = layerIndex - 1;
						int downEnd = layerIndex - downLayerCount;

						for (int layerToTest = downStart; layerToTest >= downEnd; layerToTest--)
						{
							totalPartsToRemove = AddIslandsToPolygons(extruder.Layers[layerToTest].Islands, island.BoundingBox, totalPartsToRemove);
							totalPartsToRemove = Clipper.CleanPolygons(totalPartsToRemove, cleanDistance_um);
						}

						solidInfillOutlines = solidInfillOutlines.CreateDifference(totalPartsToRemove);
						RemoveSmallAreas(extrusionWidth_um, solidInfillOutlines);

						solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					}

					island.SolidInfillToolPaths = solidInfillOutlines;
					infillOutlines = infillOutlines.CreateDifference(solidInfillOutlines);
				}

				RemoveSmallAreas(extrusionWidth_um, infillOutlines);
				infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);
				island.InfillToolPaths = infillOutlines;
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

		private static Polygons RemoveIslandsFromPolygons(List<LayerIsland> islands, Aabb boundsToConsider, Polygons polygonsToSubtractFrom)
		{
			for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
			{
				if (boundsToConsider.Hit(islands[islandIndex].BoundingBox))
				{
					polygonsToSubtractFrom = polygonsToSubtractFrom.CreateDifference(islands[islandIndex].InsetToolPaths[islands[islandIndex].InsetToolPaths.Count - 1]);

					polygonsToSubtractFrom = Clipper.CleanPolygons(polygonsToSubtractFrom, cleanDistance_um);
				}
			}

			return polygonsToSubtractFrom;
		}

		private static Polygons AddIslandsToPolygons(List<LayerIsland> islands, Aabb boundsToConsider, Polygons polysToAddTo)
		{
			Polygons polysToIntersect = new Polygons();
			for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
			{
				if (boundsToConsider.Hit(islands[islandIndex].BoundingBox))
				{
					polysToIntersect = polysToIntersect.CreateUnion(islands[islandIndex].InsetToolPaths[islands[islandIndex].InsetToolPaths.Count - 1]);
					polysToIntersect = Clipper.CleanPolygons(polysToIntersect, cleanDistance_um);
				}
			}

			polysToAddTo = polysToAddTo.CreateIntersection(polysToIntersect);

			return polysToAddTo;
		}
	}
}