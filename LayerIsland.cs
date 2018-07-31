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

using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	using System;
	using Pathfinding;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

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

		public Polygons InfillToolPaths { get; set; } = new Polygons();

		/// <summary>
		/// The IslandOutline inset as many times as there are perimeters for the part.
		/// </summary>
		public List<Polygons> InsetToolPaths { get; set; } = new List<Polygons>();

		/// <summary>
		/// The outline of the island as defined by the original mesh polygons (not inset at all).
		/// </summary>
		public Polygons IslandOutline { get; set; } = new Polygons();

		public PathFinder PathFinder { get; private set; }
		// The outline that the tool head will actually follow (the center of the extrusion)
		public Polygons SolidBottomToolPaths { get; set; } = new Polygons();
		public Polygons SolidInfillToolPaths { get; set; } = new Polygons();
		public Polygons SolidFirstOnSparseToolPaths { get; set; } = new Polygons();
		public Polygons SolidTopToolPaths { get; set; } = new Polygons();

		public void GenerateInsets(int extrusionWidth_um, int outerExtrusionWidth_um, int insetCount, bool avoidCrossingPerimeters)
		{
			LayerIsland part = this;
			part.BoundingBox.Calculate(part.IslandOutline);

			part.PathFinder = new PathFinder(part.IslandOutline, extrusionWidth_um * 3 / 2, useInsideCache: avoidCrossingPerimeters);
			if (insetCount == 0)
			{
				// if we have no insets defined still create one
				part.InsetToolPaths.Add(part.IslandOutline);
			}
			else // generate the insets
			{
				int currentOffset = 0;

				// Inset 0 will use the outerExtrusionWidth_um, everyone else will use extrusionWidth_um
				int offsetBy = outerExtrusionWidth_um / 2;

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
	};
}