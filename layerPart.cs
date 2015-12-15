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
using System.IO;

namespace MatterHackers.MatterSlice
{
	using Polygons = List<List<IntPoint>>;

	public static class LayerPart
	{
		/*
		The layer-part creation step is the first step in creating actual useful data for 3D printing.
		It takes the result of the Slice step, which is an unordered list of polygons, and makes groups of polygons,
		each of these groups is called a "part", which sometimes are also known as "islands". These parts represent
		isolated areas in the 2D layer with possible holes.

		Creating "parts" is an important step, as all elements in a single part should be printed before going to another part.
		Every bit inside a single part can be printed without the nozzle leaving the boundery of this part.

		It's also the first step that stores the result in the "data storage" so all other steps can access it.
		*/

		private static void CreateLayerWithParts(SliceLayer singleExtruder, SlicerLayer layer)
		{
			singleExtruder.TotalOutline = layer.PolygonList;
            List<Polygons> separtedIntoIslands = layer.PolygonList.ProcessIntoSeparatIslands();

			for (int islandIndex = 0; islandIndex < separtedIntoIslands.Count; islandIndex++)
			{
				singleExtruder.Islands.Add(new LayerIsland());
				singleExtruder.Islands[islandIndex].IslandOutline = separtedIntoIslands[islandIndex];

				singleExtruder.Islands[islandIndex].BoundingBox.Calculate(singleExtruder.Islands[islandIndex].IslandOutline);
			}
		}

		public static void CreateLayerParts(ExtruderLayers singleExtruder, Slicer slicer)
		{
			for (int layerIndex = 0; layerIndex < slicer.layers.Count; layerIndex++)
			{
				singleExtruder.Layers.Add(new SliceLayer());
				singleExtruder.Layers[layerIndex].LayerZ = slicer.layers[layerIndex].Z;
				LayerPart.CreateLayerWithParts(singleExtruder.Layers[layerIndex], slicer.layers[layerIndex]);
			}
		}

		public static void DumpLayerparts(LayerDataStorage storage, string filename)
		{
			StreamWriter streamToWriteTo = new StreamWriter(filename);
			streamToWriteTo.Write("<!DOCTYPE html><html><body>");
			Point3 modelSize = storage.modelSize;
			Point3 modelMin = storage.modelMin;

			for (int volumeIdx = 0; volumeIdx < storage.Extruders.Count; volumeIdx++)
			{
				for (int layerNr = 0; layerNr < storage.Extruders[volumeIdx].Layers.Count; layerNr++)
				{
					streamToWriteTo.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style=\"width: 500px; height:500px\">\n");
					SliceLayer layer = storage.Extruders[volumeIdx].Layers[layerNr];
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