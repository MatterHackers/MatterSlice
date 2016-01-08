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
    }
}