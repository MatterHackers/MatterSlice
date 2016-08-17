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

using MSClipperLib;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using Polygons = List<List<IntPoint>>;

	/// <summary>
	/// Represents the data for one island.
	/// A single island can be more than one polygon as they have both the outline and the hole polygons.
	/// </summary>
	public class LayerIsland
	{
		public Aabb BoundingBox = new Aabb();
		/// <summary>
		/// The outline of the island as defined by the original mesh polygons (not inset at all).
		/// </summary>
		public Polygons IslandOutline = new Polygons();
		public Polygons AvoidCrossingBoundary = new Polygons();
		/// <summary>
		/// The IslandOutline inset as many times as there are perimeters for the part.
		/// </summary>
		public List<Polygons> InsetToolPaths = new List<Polygons>();
		public Polygons SolidTopToolPaths = new Polygons();
		public Polygons SolidBottomToolPaths = new Polygons();
		public Polygons SolidInfillToolPaths = new Polygons();
		public Polygons InfillToolPaths = new Polygons();

		private static readonly double minimumDistanceToCreateNewPosition = 10;

		public void GenerateInsets(int extrusionWidth_um, int outerExtrusionWidth_um, int insetCount)
		{
			LayerIsland part = this;
			part.BoundingBox.Calculate(part.IslandOutline);

			part.AvoidCrossingBoundary = part.IslandOutline.Offset(-extrusionWidth_um);
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