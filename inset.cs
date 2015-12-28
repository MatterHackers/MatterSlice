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
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using Polygons = List<List<IntPoint>>;

	public static class Inset
	{
		private static readonly double minimumDistanceToCreateNewPosition = 10;

		public static void GenerateInsets(LayerIsland part, int extrusionWidth_um, int outerExtrusionWidth_um, int insetCount)
		{
            part.BoundingBox.Calculate(part.IslandOutline);

			part.AvoidCrossingBoundery = part.IslandOutline;//.Offset(-extrusionWidth_um);
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
					// Incriment by half the offset amount
					currentOffset += offsetBy;
		
					Polygons currentInset = part.IslandOutline.Offset(-currentOffset);
					// make sure our polygon data is reasonable
					currentInset = Clipper.CleanPolygons(currentInset, minimumDistanceToCreateNewPosition);

					// check that we have actuall paths
					if (currentInset.Count > 0)
					{
						part.InsetToolPaths.Add(currentInset);

						// Incriment by the second half
						currentOffset += offsetBy;
					}
					else
					{
						// we are done making insets as we have no arrea left
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

		public static void generateInsets(SliceLayer layer, int extrusionWidth_um, int outerExtrusionWidth_um, int insetCount)
		{
			for (int partIndex = 0; partIndex < layer.Islands.Count; partIndex++)
			{
				GenerateInsets(layer.Islands[partIndex], extrusionWidth_um, outerExtrusionWidth_um, insetCount);
			}

			//Remove the parts which did not generate an inset. As these parts are too small to print,
			// and later code can now assume that there is always minimum 1 inset line.
			for (int partIndex = 0; partIndex < layer.Islands.Count; partIndex++)
			{
				if (layer.Islands[partIndex].InsetToolPaths.Count < 1)
				{
					layer.Islands.RemoveAt(partIndex);
					partIndex -= 1;
				}
			}
		}
	}
}