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

		private static void CreateLayerWithParts(SliceLayer storageLayer, SlicerLayer layer, ConfigConstants.REPAIR_OVERLAPS unionAllType)
		{
			if ((unionAllType & ConfigConstants.REPAIR_OVERLAPS.REVERSE_ORIENTATION) == ConfigConstants.REPAIR_OVERLAPS.REVERSE_ORIENTATION)
			{
				for (int i = 0; i < layer.PolygonList.Count; i++)
				{
					if (layer.PolygonList[i].Orientation())
					{
						layer.PolygonList[i].Reverse();
					}
				}
			}

			List<Polygons> result;
			if ((unionAllType & ConfigConstants.REPAIR_OVERLAPS.UNION_ALL_TOGETHER) == ConfigConstants.REPAIR_OVERLAPS.UNION_ALL_TOGETHER)
			{
				result = layer.PolygonList.Offset(1000).CreateLayerOutlines(PolygonsHelper.LayerOpperation.UnionAll);
			}
			else
			{
				result = layer.PolygonList.CreateLayerOutlines(PolygonsHelper.LayerOpperation.EvenOdd);
			}

			for (int i = 0; i < result.Count; i++)
			{
				storageLayer.parts.Add(new SliceLayerPart());
				if ((unionAllType & ConfigConstants.REPAIR_OVERLAPS.UNION_ALL_TOGETHER) == ConfigConstants.REPAIR_OVERLAPS.UNION_ALL_TOGETHER)
				{
					storageLayer.parts[i].totalOutline.Add(result[i][0]);
					storageLayer.parts[i].totalOutline = storageLayer.parts[i].totalOutline.Offset(-1000);
				}
				else
				{
					storageLayer.parts[i].totalOutline = result[i];
				}

				storageLayer.parts[i].boundingBox.Calculate(storageLayer.parts[i].totalOutline);
			}
		}

		public static void CreateLayerParts(SliceVolumeStorage storage, Slicer slicer, ConfigConstants.REPAIR_OVERLAPS unionAllType)
		{
			for (int layerIndex = 0; layerIndex < slicer.layers.Count; layerIndex++)
			{
				storage.layers.Add(new SliceLayer());
				storage.layers[layerIndex].printZ = slicer.layers[layerIndex].Z;
				LayerPart.CreateLayerWithParts(storage.layers[layerIndex], slicer.layers[layerIndex], unionAllType);
			}
		}

		public static void DumpLayerparts(SliceDataStorage storage, string filename)
		{
			StreamWriter streamToWriteTo = new StreamWriter(filename);
			streamToWriteTo.Write("<!DOCTYPE html><html><body>");
			Point3 modelSize = storage.modelSize;
			Point3 modelMin = storage.modelMin;

			for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
			{
				for (int layerNr = 0; layerNr < storage.volumes[volumeIdx].layers.Count; layerNr++)
				{
					streamToWriteTo.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style=\"width: 500px; height:500px\">\n");
					SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
					for (int i = 0; i < layer.parts.Count; i++)
					{
						SliceLayerPart part = layer.parts[i];
						for (int j = 0; j < part.totalOutline.Count; j++)
						{
							streamToWriteTo.Write("<polygon points=\"");
							for (int k = 0; k < part.totalOutline[j].Count; k++)
								streamToWriteTo.Write("{0},{1} ".FormatWith((float)(part.totalOutline[j][k].X - modelMin.x) / modelSize.x * 500, (float)(part.totalOutline[j][k].Y - modelMin.y) / modelSize.y * 500));
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