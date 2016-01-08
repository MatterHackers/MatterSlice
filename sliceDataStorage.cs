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
	using System.IO;
	using Polygons = List<List<IntPoint>>;

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

		public void DumpLayerparts(string filename)
		{
			LayerDataStorage storage = this;
			StreamWriter streamToWriteTo = new StreamWriter(filename);
			streamToWriteTo.Write("<!DOCTYPE html><html><body>");
			Point3 modelSize = storage.modelSize;
			Point3 modelMin = storage.modelMin;

			for (int extruderIndex = 0; extruderIndex < storage.Extruders.Count; extruderIndex++)
			{
				for (int layerNr = 0; layerNr < storage.Extruders[extruderIndex].Layers.Count; layerNr++)
				{
					streamToWriteTo.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style=\"width: 500px; height:500px\">\n");
					SliceLayer layer = storage.Extruders[extruderIndex].Layers[layerNr];
					for (int i = 0; i < layer.Islands.Count; i++)
					{
						LayerIsland part = layer.Islands[i];
						for (int j = 0; j < part.IslandOutline.Count; j++)
						{
							streamToWriteTo.Write("<polygon points=\"");
							for (int k = 0; k < part.IslandOutline[j].Count; k++)
								streamToWriteTo.Write("{0},{1} ".FormatWith((float)(part.IslandOutline[j][k].X - modelMin.x) / modelSize.x * 500, (float)(part.IslandOutline[j][k].Y - modelMin.y) / modelSize.y * 500));
							if (j == 0)
								streamToWriteTo.Write("\" style=\"fill:gray; stroke:black;stroke-width:1\" />\n");
							else
								streamToWriteTo.Write("\" style=\"fill:red; stroke:black;stroke-width:1\" />\n");
						}
					}
					streamToWriteTo.Write("</svg>\n");
				}
			}
			streamToWriteTo.Write("</body></html>");
			streamToWriteTo.Close();
		}
	}
}
