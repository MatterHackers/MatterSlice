﻿/*
This file is part of MatterSlice. A command line utility for
generating 3D printing GCode.

Copyright (c) 2015, Lars Brubaker

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

using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	using Polygons = List<List<IntPoint>>;

	public class ExtruderLayers
	{
		public List<SliceLayer> Layers = new List<SliceLayer>();

		private static readonly double cleanDistance_um = 10;

		public void CreateIslandData()
		{
			for (int layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
			{
				Layers[layerIndex].CreateIslandData();
			}
		}

		public void GenerateTopAndBottoms(int layerIndex, int extrusionWidth_um, int outerPerimeterWidth_um, int downLayerCount, int upLayerCount, long infillExtendIntoPerimeter_um)
		{
			var clippingOffset = infillExtendIntoPerimeter_um * 2;

			ExtruderLayers extruder = this;
			SliceLayer layer = extruder.Layers[layerIndex];

			for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				LayerIsland island = layer.Islands[islandIndex];
				if (island.InsetToolPaths.Count == 0)
				{
					continue;
				}
				// this is the entire extrusion width to make sure we are outside of the extrusion line
				Polygons lastInset = island.InsetToolPaths[island.InsetToolPaths.Count - 1];
				Polygons insetWithOffset = lastInset.Offset(-extrusionWidth_um);
				Polygons infillOutlines = new Polygons(insetWithOffset);

				// calculate the bottom outlines
				if (downLayerCount > 0)
				{
					Polygons bottomOutlines = new Polygons(insetWithOffset);

					if (layerIndex - 1 >= 0)
					{
						var previousLayer = extruder.Layers[layerIndex - 1];

						bottomOutlines = RemoveIslandsFromPolygons(previousLayer.Islands, island.BoundingBox, bottomOutlines);
						bottomOutlines.RemoveSmallAreas(extrusionWidth_um);
					}

					infillOutlines = infillOutlines.CreateDifference(bottomOutlines);
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					island.SolidBottomToolPaths = bottomOutlines;
				}

				// calculate the top outlines
				if (upLayerCount > 0)
				{
					Polygons topOutlines = new Polygons(insetWithOffset);
					topOutlines = topOutlines.CreateDifference(island.SolidBottomToolPaths.Offset(clippingOffset));
					topOutlines = Clipper.CleanPolygons(topOutlines, cleanDistance_um);

					if (layerIndex + 1 < extruder.Layers.Count)
					{
						// Remove the top layer that is above this one to get only the data that is a top layer on this layer.
						topOutlines = RemoveIslandsFromPolygons(extruder.Layers[layerIndex + 1].Islands, island.BoundingBox, topOutlines);
					}

					topOutlines.RemoveSmallAreas(extrusionWidth_um);

					infillOutlines = infillOutlines.CreateDifference(topOutlines.Offset(clippingOffset));
					infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);

					island.SolidTopToolPaths = topOutlines;
				}

				// calculate the solid infill outlines
				if (upLayerCount > 1 || downLayerCount > 1)
				{
					Polygons solidInfillOutlines = new Polygons(insetWithOffset);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(island.SolidBottomToolPaths.Offset(clippingOffset));
					solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					solidInfillOutlines = solidInfillOutlines.CreateDifference(island.SolidTopToolPaths.Offset(clippingOffset));

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
						solidInfillOutlines.RemoveSmallAreas(extrusionWidth_um);

						solidInfillOutlines = Clipper.CleanPolygons(solidInfillOutlines, cleanDistance_um);
					}

					island.SolidInfillToolPaths = solidInfillOutlines;
					infillOutlines = infillOutlines.CreateDifference(solidInfillOutlines.Offset(clippingOffset));
				}

				infillOutlines.RemoveSmallAreas(extrusionWidth_um);
				infillOutlines = Clipper.CleanPolygons(infillOutlines, cleanDistance_um);
				island.InfillToolPaths = infillOutlines;
			}
		}

		public void InitializeLayerData(ExtruderData extruderData, ConfigSettings config, int extruderIndex)
		{
			for (int layerIndex = 0; layerIndex < extruderData.layers.Count; layerIndex++)
			{
				if (config.outputOnlyFirstLayer && layerIndex > 0)
				{
					break;
				}

				Layers.Add(new SliceLayer());
				Layers[layerIndex].LayerZ = extruderData.layers[layerIndex].Z;

				Layers[layerIndex].AllOutlines = extruderData.layers[layerIndex].PolygonList;

				Layers[layerIndex].AllOutlines = Layers[layerIndex].AllOutlines.GetCorrectedWinding();
			}
		}

		public static void InitializeLayerPathing(ConfigSettings config, Polygons extraPathingConsideration, List<ExtruderLayers> extruders)
		{
			for (int layerIndex = 0; layerIndex < extruders[0].Layers.Count; layerIndex++)
			{
				LogOutput.Log("Generating Layer Outlines {0}/{1}\n".FormatWith(layerIndex + 1, extruders[0].Layers.Count));

				long avoidInset = config.ExtrusionWidth_um * 3 / 2;

				var allOutlines = new Polygons();
				for (int extruderIndex =0; extruderIndex < extruders.Count; extruderIndex++)
				{
					allOutlines.AddRange(extruders[extruderIndex].Layers[layerIndex].AllOutlines);
				}

				var boundary = allOutlines.GetBounds();
				var extraBoundary = extraPathingConsideration.GetBounds();

				boundary.ExpandToInclude(extraBoundary);
				boundary.Inflate(config.ExtrusionWidth_um * 10);

				var pathFinder = new Pathfinding.PathFinder(allOutlines, avoidInset, boundary);

				// assign the same pathing to all extruders for this layer
				for (int extruderIndex = 0; extruderIndex < extruders.Count; extruderIndex++)
				{
					extruders[extruderIndex].Layers[layerIndex].PathFinder = pathFinder;
				}
			}
		}

		public bool OnlyHasBottom(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].SolidBottomToolPaths.Count == 1
				&& Layers[layerToCheck].Islands[0].SolidTopToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidInfillToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].InfillToolPaths.Count == 0;
		}

		public bool OnlyHasInfill(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].SolidBottomToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidTopToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidInfillToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].InfillToolPaths.Count == 1;
		}

		public bool OnlyHasSolidInfill(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].SolidBottomToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidTopToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidInfillToolPaths.Count == 1
				&& Layers[layerToCheck].Islands[0].InfillToolPaths.Count == 0;
		}

		public bool OnlyHasTop(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].SolidBottomToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidTopToolPaths.Count == 1
				&& Layers[layerToCheck].Islands[0].SolidInfillToolPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].InfillToolPaths.Count == 0;
		}

		private static Polygons AddIslandsToPolygons(List<LayerIsland> islands, Aabb boundsToConsider, Polygons polysToAddTo)
		{
			Polygons polysToIntersect = new Polygons();
			for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
			{
				if (boundsToConsider.Hit(islands[islandIndex].BoundingBox))
				{
					if (islands[islandIndex].InsetToolPaths.Count > 0)
					{
						polysToIntersect = polysToIntersect.CreateUnion(islands[islandIndex].InsetToolPaths[islands[islandIndex].InsetToolPaths.Count - 1]);
						polysToIntersect = Clipper.CleanPolygons(polysToIntersect, cleanDistance_um);
					}
				}
			}

			polysToAddTo = polysToAddTo.CreateIntersection(polysToIntersect);

			return polysToAddTo;
		}

		private static Polygons RemoveIslandsFromPolygons(List<LayerIsland> islands, Aabb boundsToConsider, Polygons polygonsToSubtractFrom)
		{
			for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
			{
				if (boundsToConsider.Hit(islands[islandIndex].BoundingBox))
				{
					if (islands[islandIndex].InsetToolPaths.Count > 0)
					{
						polygonsToSubtractFrom = polygonsToSubtractFrom.CreateDifference(islands[islandIndex].InsetToolPaths[0]);
						polygonsToSubtractFrom = Clipper.CleanPolygons(polygonsToSubtractFrom, cleanDistance_um);
					}
				}
			}

			return polygonsToSubtractFrom;
		}
	}
}