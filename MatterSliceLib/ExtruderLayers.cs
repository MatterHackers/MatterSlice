/*
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

using System;
using System.Collections.Generic;
using System.Linq;
using MSClipperLib;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class ExtruderLayers
	{
		public List<SliceLayer> Layers = new List<SliceLayer>();

		private static readonly double cleanDistance_um = 10;

		public ExtruderLayers()
		{
		}

		/// <summary>
		///  Gets a value indicating whether the extruder is used in any layer.
		/// </summary>
		public bool Used => this.Layers.Any(layer => layer.Islands.Count > 0);

		/// <summary>
		/// Indicates if the extruder is used in the given layer.
		/// </summary>
		/// <param name="layerIndex">The layer to check.</param>
		/// <returns>A something.</returns>
		public bool UsedInLayer(int layerIndex)
		{
			return this.Layers[layerIndex].Islands.Count > 0;
		}

		/// <summary>
		/// Construct a new instance based on layers from an existing ExtruderData.
		/// </summary>
		/// <param name="extruderData">The ExtruderData containing layers to process.</param>
		/// <param name="outputOnlyFirstLayer">An indicator if only the first layer should be processed.</param>
		public ExtruderLayers(ExtruderData extruderData, bool outputOnlyFirstLayer)
		{
			// Initialize LayerData
			for (int layerIndex = 0; layerIndex < extruderData.layers.Count; layerIndex++)
			{
				if (outputOnlyFirstLayer && layerIndex > 0)
				{
					break;
				}

				var meshProcessingLayer = extruderData.layers[layerIndex];

				this.Layers.Add(new SliceLayer()
				{
					LayerZ = meshProcessingLayer.Z,
					AllOutlines = meshProcessingLayer.PolygonList.GetCorrectedWinding()
				});
			}
		}

		public void CreateIslandData()
		{
			for (int layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
			{
				Layers[layerIndex].CreateIslandData();
			}
		}

		public void GenerateTopAndBottoms(ConfigSettings config, int layerIndex, long extrusionWidth_um, long outerPerimeterWidth_um, int downLayerCount, int upLayerCount, long infillExtendIntoPerimeter_um)
		{
			var clippingOffset = infillExtendIntoPerimeter_um * 2;

			ExtruderLayers extruder = this;
			SliceLayer layer = extruder.Layers[layerIndex];

			Agg.Parallel.For(0, layer.Islands.Count, (islandIndex) =>
			// for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				LayerIsland island = layer.Islands[islandIndex];
				if (island.InsetToolPaths.Count == 0)
				{
					return;
				}

				// this is the entire extrusion width to make sure we are outside of the extrusion line
				Polygons lastInset = island.InsetToolPaths[island.InsetToolPaths.Count - 1];
				Polygons infillRegionPath = lastInset.Offset(-extrusionWidth_um);
				Polygons sparseInfillPaths = new Polygons(infillRegionPath);

				// calculate the bottom outlines
				if (downLayerCount > 0)
				{
					Polygons bottomOutlines = new Polygons(infillRegionPath);

					if (layerIndex - 1 >= 0)
					{
						var previousLayer = extruder.Layers[layerIndex - 1];

						bottomOutlines = RemoveIslandsFromPolygons(previousLayer.Islands, island.BoundingBox, bottomOutlines);
						bottomOutlines.RemoveSmallAreas(extrusionWidth_um);
					}

					sparseInfillPaths = sparseInfillPaths.CreateDifference(bottomOutlines);
					sparseInfillPaths = Clipper.CleanPolygons(sparseInfillPaths, cleanDistance_um);

					island.BottomPaths = bottomOutlines;
				}

				// calculate the top outlines
				if (upLayerCount > 0)
				{
					Polygons topOutlines = new Polygons(infillRegionPath);
					topOutlines = topOutlines.CreateDifference(island.BottomPaths.Offset(clippingOffset));
					topOutlines = Clipper.CleanPolygons(topOutlines, cleanDistance_um);

					if (layerIndex + 1 < extruder.Layers.Count)
					{
						// Remove the top layer that is above this one to get only the data that is a top layer on this layer.
						topOutlines = RemoveIslandsFromPolygons(extruder.Layers[layerIndex + 1].Islands, island.BoundingBox, topOutlines);
					}

					topOutlines.RemoveSmallAreas(extrusionWidth_um);

					sparseInfillPaths = sparseInfillPaths.CreateDifference(topOutlines.Offset(clippingOffset));
					sparseInfillPaths = Clipper.CleanPolygons(sparseInfillPaths, cleanDistance_um);

					island.TopPaths = topOutlines;
				}

				if (upLayerCount <= 0 && downLayerCount <= 0)
				{
					// Assign infill directly if no top/bottom solid layers
					island.SparseInfillPaths = sparseInfillPaths;
				}
				else
				{
					// calculate the solid infill outlines
					Polygons solidInfillPaths = new Polygons(infillRegionPath);

					// remove all the top layers
					solidInfillPaths = solidInfillPaths.CreateDifference(island.BottomPaths.Offset(clippingOffset));
					solidInfillPaths = Clipper.CleanPolygons(solidInfillPaths, cleanDistance_um);

					// remove all the bottom layers
					solidInfillPaths = solidInfillPaths.CreateDifference(island.TopPaths.Offset(clippingOffset));
					solidInfillPaths = Clipper.CleanPolygons(solidInfillPaths, cleanDistance_um);

					int upEnd = layerIndex + upLayerCount + 1;
					if (upEnd <= extruder.Layers.Count && layerIndex - downLayerCount >= 0)
					{
						// find all the regions that have more top and bottom layers than should be solid (will remain sparse)
						Polygons regionsThatWillBeSparse = new Polygons(infillRegionPath);

						int upStart = layerIndex + 2;

						Agg.Parallel.For(upStart, upEnd, (layerToTest) =>
						// for (int layerToTest = upStart; layerToTest < upEnd; layerToTest++)
						{
							regionsThatWillBeSparse = IntersectWithPolygons(extruder.Layers[layerToTest].Islands, island.BoundingBox, regionsThatWillBeSparse);
							regionsThatWillBeSparse = Clipper.CleanPolygons(regionsThatWillBeSparse, cleanDistance_um);
						});

						// find all the solid infill bottom layers
						int downStart = Math.Max(0, layerIndex - 1);
						int downEnd = Math.Max(0, layerIndex - downLayerCount);

						Agg.Parallel.For(downStart, downEnd, (layerToTest) =>
						// for (int layerToTest = downStart; layerToTest >= downEnd; layerToTest--)
						{
							regionsThatWillBeSparse = IntersectWithPolygons(extruder.Layers[layerToTest].Islands, island.BoundingBox, regionsThatWillBeSparse);
							regionsThatWillBeSparse = Clipper.CleanPolygons(regionsThatWillBeSparse, cleanDistance_um);
						});

						solidInfillPaths = solidInfillPaths.CreateDifference(regionsThatWillBeSparse);
						solidInfillPaths.RemoveSmallAreas(extrusionWidth_um);
						solidInfillPaths = Clipper.CleanPolygons(solidInfillPaths, cleanDistance_um);
					}

					// remove the solid infill from the sparse infill
					sparseInfillPaths = sparseInfillPaths.CreateDifference(solidInfillPaths.Offset(clippingOffset));
					sparseInfillPaths.RemoveSmallAreas(extrusionWidth_um);
					sparseInfillPaths = Clipper.CleanPolygons(sparseInfillPaths, cleanDistance_um);
					island.SparseInfillPaths = sparseInfillPaths;

					if (config == null  // this is to make our tests test the bridgeOverInfill
						|| config.BridgeOverInfill)
					{
						// now figure out what part of the solid infill is actually first top layers and switch it to that
						// we can only have a first top y layer at the bottom of the top layers
						if (layerIndex == extruder.Layers.Count - upLayerCount)
						{
							// all of it is first top layers
							island.FirstTopPaths = solidInfillPaths;
							solidInfillPaths = new Polygons();
						}
						else if (layerIndex > 0
							&& layerIndex < extruder.Layers.Count - upLayerCount)
						{
							// Intersect the current solid layer with the previous spars layer
							// that will be all of the new solid layers that are currently on sparse layer

							var firstTopPaths = new Polygons(solidInfillPaths);
							firstTopPaths = IntersectWithSparsePolygons(extruder.Layers[layerIndex - 1].Islands, island.BoundingBox, firstTopPaths);
							firstTopPaths.RemoveSmallAreas(extrusionWidth_um);
							firstTopPaths = Clipper.CleanPolygons(firstTopPaths, cleanDistance_um);

							if (firstTopPaths.Count > 0)
							{
								solidInfillPaths = solidInfillPaths.CreateDifference(firstTopPaths.Offset(clippingOffset));
								solidInfillPaths.RemoveSmallAreas(extrusionWidth_um);
								solidInfillPaths = Clipper.CleanPolygons(solidInfillPaths, cleanDistance_um);

								island.FirstTopPaths = firstTopPaths;
							}
						}
					}

					island.SolidInfillPaths = solidInfillPaths;
				}
			});
		}

		static HashSet<int> layersSeen = new HashSet<int>();
		static object locker = new object();

		public static void InitializeLayerPathing(ConfigSettings config, Polygons extraPathingConsideration, List<ExtruderLayers> extruders)
		{
			Agg.Parallel.For(0, extruders[0].Layers.Count, (layerIndex) =>
			// for (int layerIndex = 0; layerIndex < extruders[0].Layers.Count; layerIndex++)
			{
				if (MatterSlice.Canceled)
				{
					return;
				}

				lock (locker)
				{
					if (!layersSeen.Contains(layerIndex))
					{
						layersSeen.Add(layerIndex);
					}

					LogOutput.Log("Generating Outlines {0}/{1}\n".FormatWith(layersSeen.Count(), extruders[0].Layers.Count));
				}

				long avoidInset = config.ExtrusionWidth_um * 3 / 2;

				var allOutlines = new Polygons();
				for (int extruderIndex = 0; extruderIndex < extruders.Count; extruderIndex++)
				{
					allOutlines.AddRange(extruders[extruderIndex].Layers[layerIndex].AllOutlines);
				}

				var boundary = allOutlines.GetBounds();
				var extraBoundary = extraPathingConsideration.GetBounds();

				boundary.ExpandToInclude(extraBoundary);
				boundary.Inflate(config.ExtrusionWidth_um * 10);

				if (config.AvoidCrossingPerimeters)
				{
					var pathFinder = new Pathfinding.PathFinder(allOutlines, avoidInset, boundary, config.AvoidCrossingPerimeters, $"layer {layerIndex}");

					// assign the same pathing to all extruders for this layer
					for (int extruderIndex = 0; extruderIndex < extruders.Count; extruderIndex++)
					{
						extruders[extruderIndex].Layers[layerIndex].PathFinder = pathFinder;
					}
				}
			});
		}

		public bool OnlyHasBottom(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].BottomPaths.Count == 1
				&& Layers[layerToCheck].Islands[0].FirstTopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].TopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidInfillPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SparseInfillPaths.Count == 0;
		}

		public bool OnlyHasInfill(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].BottomPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].FirstTopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].TopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidInfillPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SparseInfillPaths.Count == 1;
		}

		public bool OnlyHasSolidInfill(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].BottomPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].FirstTopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].TopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidInfillPaths.Count == 1
				&& Layers[layerToCheck].Islands[0].SparseInfillPaths.Count == 0;
		}

		public bool OnlyHasFirstTop(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].BottomPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].FirstTopPaths.Count == 1
				&& Layers[layerToCheck].Islands[0].TopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SolidInfillPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SparseInfillPaths.Count == 0;
		}

		public bool OnlyHasTop(int layerToCheck)
		{
			return Layers[layerToCheck].Islands.Count == 1
				&& Layers[layerToCheck].Islands[0].BottomPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].FirstTopPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].TopPaths.Count == 1
				&& Layers[layerToCheck].Islands[0].SolidInfillPaths.Count == 0
				&& Layers[layerToCheck].Islands[0].SparseInfillPaths.Count == 0;
		}

		private static Polygons IntersectWithPolygons(List<LayerIsland> islands, Aabb boundsToConsider, Polygons polysToIntersect)
		{
			Polygons polysFromIslands = new Polygons();
			for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
			{
				if (boundsToConsider.Hit(islands[islandIndex].BoundingBox))
				{
					if (islands[islandIndex].InsetToolPaths.Count > 0)
					{
						polysFromIslands = polysFromIslands.CreateUnion(islands[islandIndex].InsetToolPaths[islands[islandIndex].InsetToolPaths.Count - 1]);
						polysFromIslands = Clipper.CleanPolygons(polysFromIslands, cleanDistance_um);
					}
				}
			}

			polysToIntersect = polysToIntersect.CreateIntersection(polysFromIslands);

			return polysToIntersect;
		}

		private static Polygons IntersectWithSparsePolygons(List<LayerIsland> islands, Aabb boundsToConsider, Polygons polysToIntersect)
		{
			Polygons polysFromIslands = new Polygons();
			for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
			{
				if (boundsToConsider.Hit(islands[islandIndex].BoundingBox))
				{
					if (islands[islandIndex].InsetToolPaths.Count > 0)
					{
						polysFromIslands = polysFromIslands.CreateUnion(islands[islandIndex].SparseInfillPaths);
						polysFromIslands = Clipper.CleanPolygons(polysFromIslands, cleanDistance_um);
					}
				}
			}

			polysToIntersect = polysToIntersect.CreateIntersection(polysFromIslands);

			return polysToIntersect;
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