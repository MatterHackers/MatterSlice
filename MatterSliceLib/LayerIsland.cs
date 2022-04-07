/*
This file is part of MatterSlice. A command line utility for
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

using System;
using System.Collections.Generic;
using MatterHackers.Pathfinding;
using MatterHackers.QuadTree;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class InsetPaths
	{
		public InsetPaths Children { get; set; }

		public Polygon InsetPath { get; set; }
	}

	/// <summary>
	/// Represents the data for one island.
	/// A single island can be more than one polygon as they have both the outline and the hole polygons.
	/// </summary>
	public class LayerIsland
	{
		public Aabb BoundingBox = new Aabb();

		private static readonly double minimumDistanceToCreateNewPosition = 10;

		public LayerIsland()
		{
		}

		/// <summary>
		/// Constructs a new LayerIsland based on referenced Polygons and calculates its BoundingBox.
		/// </summary>
		/// <param name="islandOutline">The LayerIsland outlines.</param>
		public LayerIsland(Polygons islandOutline)
		{
			this.IslandOutline = islandOutline;
			this.BoundingBox.Calculate(this.IslandOutline);
		}

		/// <summary>
		/// The outline of the island as defined by the original mesh polygons (not inset at all).
		/// </summary>
		public Polygons IslandOutline { get; set; } = new Polygons();

		/// <summary>
		/// The IslandOutline inset as many times as there are perimeters for the part.
		/// </summary>
		public List<Polygons> InsetToolPaths { get; set; } = new List<Polygons>();

		public PathFinder PathFinder { get; internal set; }

		// The outline that the tool head will actually follow (the center of the extrusion)
		public Polygons BottomPaths { get; set; } = new Polygons();

		public Polygons SparseInfillPaths { get; set; } = new Polygons();

		public Polygons SolidInfillPaths { get; set; } = new Polygons();

		public Polygons FirstTopPaths { get; set; } = new Polygons();

		public Polygons TopPaths { get; set; } = new Polygons();

		private static Random rand = new Random();

		private Polygons MakeFuzzy(Polygons polygons, Polygons fuzzyBounds, ConfigSettings config)
		{
			if (polygons.Count < 1
				|| fuzzyBounds?.Count > 0 != true)
			{
				return polygons;
			}

			var fuzziness = config.FuzzyThickness_um;
			var avg_dist_between_points = config.FuzzyPointDist_um;
			var min_dist_between_points = avg_dist_between_points * 3 / 4; // hardcoded: the point distance may vary between 3/4 and 5/4 the supplied value
			var range_random_point_dist = avg_dist_between_points / 2;

			var fuzzyPolygons = new Polygons();
			foreach (var polygon in polygons)
			{
				var fuzzyPolygon = new Polygon();
				fuzzyPolygons.Add(fuzzyPolygon);

				// get every line segment including the last
				for (int i=0; i<polygon.Count; i++)
                {
					var next = (i + 1) % polygon.Count;
					// create the line segment
					var line = new Polygon() { polygon[i], polygon[next] };
					var outsideLines = fuzzyBounds.CreateLineDifference(new Polygons() { line });
					if (outsideLines.Count == 1
						&& ((outsideLines[0][0] == line[0]
						&& outsideLines[0][1] == line[1])
						|| (outsideLines[0][0] == line[1]
						&& outsideLines[0][1] == line[0])))
					{
						// it is not in the area needing fuzzing
						fuzzyPolygon.Add(line[0]);
						fuzzyPolygon.Add(line[1]);
					}
					else
					{
						var insideLines = fuzzyBounds.CreateLineIntersections(new Polygons() { line });
						// sort the segments along the line direction
						var start = line[0];

						// iterate over all the sorted segments
						// if it does not need fuzzing add it to the output
						// else fuzz the segments and add all the fuzzed pieces

						// generate points in between p0 and p1
						var dist_left_over = (min_dist_between_points / 4) + rand.Next() % (min_dist_between_points / 4); // the distance to be traversed on the line before making the first new point
						var p0 = start;
						var p1 = line[1];

						// 'a' is the (next) new point between p0 and p1
						var p0p1 = p1 - p0;
						var p0p1_size = p0p1.Length();
						var p0pa_dist = dist_left_over;
						if (p0pa_dist >= p0p1_size)
						{
							fuzzyPolygon.Add(p1 - (p0p1 / 2));
						}

						for (; p0pa_dist < p0p1_size; p0pa_dist += min_dist_between_points + rand.Next() % range_random_point_dist)
						{
							var r = rand.Next() % (fuzziness * 2) - fuzziness;
							var perp_to_p0p1 = p0p1.GetPerpendicularLeftXY();
							var fuzz = perp_to_p0p1.Normal(r);
							fuzzyPolygon.Add(p0 + p0p1.Normal(p0pa_dist) + fuzz);
						}
					}
				}
			}

			return fuzzyPolygons;
		}

		public void GenerateInsets(ConfigSettings config, Polygons fuzzyBounds, long extrusionWidth_um, long outerExtrusionWidth_um, int insetCount, bool avoidCrossingPerimeters)
		{
			LayerIsland part = this;
			part.BoundingBox.Calculate(part.IslandOutline);

			if (avoidCrossingPerimeters)
			{
				part.PathFinder = new PathFinder(part.IslandOutline, extrusionWidth_um * 3 / 2, useInsideCache: avoidCrossingPerimeters, name: "inset island");
			}

			if (insetCount == 0)
			{
				// if we have no insets defined still create one
				part.InsetToolPaths.Add(part.IslandOutline);
			}
			else // generate the insets
			{
				long currentOffset = 0;

				// Inset 0 will use the outerExtrusionWidth_um, everyone else will use extrusionWidth_um
				long offsetBy = outerExtrusionWidth_um / 2;

				for (int i = 0; i < insetCount; i++)
				{
					// Increment by half the offset amount
					currentOffset += offsetBy;

					Polygons currentInset = part.IslandOutline.Offset(-currentOffset);

					// make the outer perimeter fuzzy if needed
					if (i == 0)
					{
						currentInset = MakeFuzzy(currentInset, fuzzyBounds, config);
					}
                    
					// make sure our polygon data is reasonable
					if (config.MergeOverlappingLines)
					{
						// be aggressive about maintaining small polygons
						currentInset = Clipper.CleanPolygons(currentInset);
					}
					else
					{
						// clean the polygon to make it have less jaggies
						currentInset = Clipper.CleanPolygons(currentInset, minimumDistanceToCreateNewPosition);
					}

					// check that we have actual paths
					if (currentInset.Count > 0)
					{
						var run = true;
						// if we are centering the seam put a point exactly in back
						if (run && (config.SeamPlacement == SEAM_PLACEMENT.ALWAYS_CENTERED_IN_BACK
							|| config.SeamPlacement == SEAM_PLACEMENT.CENTERED_IN_BACK))
						{
							foreach (var polygon in currentInset)
							{
								var count = polygon.Count;
								if (count > 2)
								{
									// if we are going to center the seam in the back make sure there is a vertex to center on that is exactly in back
									var centeredIndex = polygon.GetCenteredInBackIndex(out IntPoint center);
									var start = -1;
									var end = -1;

									if (polygon[centeredIndex].X <= center.X)
									{
										// start should always be left of center
										start = centeredIndex;
										// is the next point right of center
										end = (centeredIndex + 1) % count;
										if (polygon[end].X < center.X)
										{
											// no it is left of center
											// is the previous point right of center
											end = (centeredIndex + count - 1) % count;
											if (polygon[end].X < center.X)
											{
												// it is still left of center (so no crossing)
												continue; // skip placing a seam at this point
											}
										}
									}
									else if (polygon[centeredIndex].X >= center.X)
									{
										// we are to the right of center so this is the end
										end = centeredIndex;
										// set start to the left of center
										start = (centeredIndex + 1) % count;
										if (polygon[start].X > center.X)
										{
											// start is also right of center it need to be left
											start = (centeredIndex + count - 1) % count;
											if (polygon[start].X > center.X)
											{
												// it is still right of center skip this point
												continue;
											}
										}
									}

									// find the y intercept
									var delta = polygon[end] - polygon[start];
									if (delta.X != 0)
									{
										var insert = Math.Max(start, end);
										if (insert == count -1 && (start == 0 || end == 0))
										{
											insert = count;
										}
										var ratio = (center.X - polygon[start].X) / (double)delta.X;
										polygon.Insert(insert, new IntPoint(center.X, polygon[start].Y + (polygon[end].Y - polygon[start].Y) * ratio));
									}
								}
							}
						}

						part.InsetToolPaths.Add(currentInset);

						// Increment by the second half
						currentOffset += offsetBy;
					}
					else
					{
						// we are done making insets as we have no area left
						break;
					}

					if (i == 0)
					{
						// Reset offset amount to half the standard extrusion width
						offsetBy = extrusionWidth_um / 2;
					}
				}
			}
		}
	}
}