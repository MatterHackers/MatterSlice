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

	/*
	SliceData
	+ Layers[]
	  + LayerParts[]
		+ OutlinePolygons[]
		+ Insets[]
		  + Polygons[]
		+ SkinPolygons[]
	*/

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
	};

	public class SliceLayer
	{
		public long LayerZ;
		public Polygons TotalOutline = new Polygons();
        public List<LayerIsland> Islands = null;

        public void CreateIslandData()
        {
            List<Polygons> separtedIntoIslands = TotalOutline.ProcessIntoSeparatIslands();

            Islands = new List<LayerIsland>();
            for (int islandIndex = 0; islandIndex < separtedIntoIslands.Count; islandIndex++)
            {
                Islands.Add(new LayerIsland());
                Islands[islandIndex].IslandOutline = separtedIntoIslands[islandIndex];

                Islands[islandIndex].BoundingBox.Calculate(Islands[islandIndex].IslandOutline);
            }
        }
    };

	public class ExtruderLayers
	{
		public List<SliceLayer> Layers = new List<SliceLayer>();

        public void CreateIslandData()
        {
            for (int layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
            {
                Layers[layerIndex].CreateIslandData();
            }
        }

        public void InitializeLayerData(Slicer slicer)
        {
            for (int layerIndex = 0; layerIndex < slicer.layers.Count; layerIndex++)
            {
                Layers.Add(new SliceLayer());
                Layers[layerIndex].LayerZ = slicer.layers[layerIndex].Z;

                Layers[layerIndex].TotalOutline = slicer.layers[layerIndex].PolygonList;

                Layers[layerIndex].TotalOutline = Layers[layerIndex].TotalOutline.GetCorrectedWinding();
            }
        }
    }

    public class LayerDataStorage
	{
		public Point3 modelSize, modelMin, modelMax;
		public Polygons skirt = new Polygons();
		public Polygons raftOutline = new Polygons();
		public List<Polygons> wipeShield = new List<Polygons>();
		public List<ExtruderLayers> Extruders = new List<ExtruderLayers>();

        public NewSupport newSupport = null;
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