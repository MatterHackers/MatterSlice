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

		public void GenerateInsets(long extrusionWidth_um, long outerExtrusionWidth_um, int insetCount, bool avoidCrossingPerimeters)
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
					// make sure our polygon data is reasonable
					currentInset = Clipper.CleanPolygons(currentInset, minimumDistanceToCreateNewPosition);

					// check that we have actual paths
					if (currentInset.Count > 0)
					{
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