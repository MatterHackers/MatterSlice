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

	/// <summary>
	/// Represents the data for one island.
	/// A single island can be more than one polygon as they have both the outline and the hole polygons.
	/// </summary>
	public class LayerIsland
	{
		public Aabb BoundingBox = new Aabb();
		public Polygons IslandOutline = new Polygons();
		public Polygons AvoidCrossingBoundery = new Polygons();
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
	};

	public class SliceLayer
	{
		public long LayerZ;
		public Polygons AllOutlines = new Polygons();
        public List<LayerIsland> Islands = null;

		public void GenerateInsets(int extrusionWidth_um, int outerExtrusionWidth_um, int insetCount)
		{
			SliceLayer layer = this;
			for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				layer.Islands[islandIndex].GenerateInsets(extrusionWidth_um, outerExtrusionWidth_um, insetCount);
			}

			//Remove the parts which did not generate an inset. As these parts are too small to print,
			// and later code can now assume that there is always minimum 1 inset line.
			for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				if (layer.Islands[islandIndex].InsetToolPaths.Count < 1)
				{
					layer.Islands.RemoveAt(islandIndex);
					islandIndex -= 1;
				}
			}
		}

		public void CreateIslandData()
        {
            List<Polygons> separtedIntoIslands = AllOutlines.ProcessIntoSeparatIslands();

            Islands = new List<LayerIsland>();
            for (int islandIndex = 0; islandIndex < separtedIntoIslands.Count; islandIndex++)
            {
                Islands.Add(new LayerIsland());
                Islands[islandIndex].IslandOutline = separtedIntoIslands[islandIndex];

                Islands[islandIndex].BoundingBox.Calculate(Islands[islandIndex].IslandOutline);
            }
        }
    };

    public class LayerDataStorage
	{
		public Point3 modelSize, modelMin, modelMax;
		public Polygons skirt = new Polygons();
		public Polygons raftOutline = new Polygons();
		public List<Polygons> wipeShield = new List<Polygons>();
		public List<ExtruderLayers> Extruders = new List<ExtruderLayers>();

        public NewSupport support = null;
        public Polygons wipeTower = new Polygons();
		public IntPoint wipePoint;

        public void CreateIslandData()
        {
            for (int extruderIndex = 0; extruderIndex < Extruders.Count; extruderIndex++)
            {
                Extruders[extruderIndex].CreateIslandData();
            }
        }
    }
}