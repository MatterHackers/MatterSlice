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

	public static class MultiVolumes
	{
		public static void RemoveVolumesIntersections(List<PartLayers> volumes)
		{
			//Go trough all the volumes, and remove the previous volume outlines from our own outline, so we never have overlapped areas.
			for (int volumeToRemoveFromIndex = volumes.Count - 1; volumeToRemoveFromIndex >= 0; volumeToRemoveFromIndex--)
			{
				for (int volumeToRemoveIndex = volumeToRemoveFromIndex - 1; volumeToRemoveIndex >= 0; volumeToRemoveIndex--)
				{
					for (int layerIndex = 0; layerIndex < volumes[volumeToRemoveFromIndex].Layers.Count; layerIndex++)
					{
						MeshLayers layerToRemoveFrom = volumes[volumeToRemoveFromIndex].Layers[layerIndex];
						MeshLayers layerToRemove = volumes[volumeToRemoveIndex].Layers[layerIndex];
						for (int partToRemoveFromIndex = 0; partToRemoveFromIndex < layerToRemoveFrom.layerData.Count; partToRemoveFromIndex++)
						{
							for (int partToRemove = 0; partToRemove < layerToRemove.layerData.Count; partToRemove++)
							{
								layerToRemoveFrom.layerData[partToRemoveFromIndex].TotalOutline = layerToRemoveFrom.layerData[partToRemoveFromIndex].TotalOutline.CreateDifference(layerToRemove.layerData[partToRemove].TotalOutline);
							}
						}
					}
				}
			}
		}

		//Expand each layer a bit and then keep the extra overlapping parts that overlap with other volumes.
		//This generates some overlap in dual extrusion, for better bonding in touching parts.
		public static void OverlapMultipleVolumesSlightly(List<PartLayers> volumes, int overlap)
		{
			if (volumes.Count < 2 || overlap <= 0)
			{
				return;
			}

			for (int layerIndex = 0; layerIndex < volumes[0].Layers.Count; layerIndex++)
			{
				Polygons fullLayer = new Polygons();
				for (int volIdx = 0; volIdx < volumes.Count; volIdx++)
				{
					MeshLayers layer1 = volumes[volIdx].Layers[layerIndex];
					for (int p1 = 0; p1 < layer1.layerData.Count; p1++)
					{
						fullLayer = fullLayer.CreateUnion(layer1.layerData[p1].TotalOutline.Offset(20));
					}
				}
				fullLayer = fullLayer.Offset(-20);

				for (int volumeIndex = 0; volumeIndex < volumes.Count; volumeIndex++)
				{
					MeshLayers layer1 = volumes[volumeIndex].Layers[layerIndex];
					for (int partIndex = 0; partIndex < layer1.layerData.Count; partIndex++)
					{
						layer1.layerData[partIndex].TotalOutline = fullLayer.CreateIntersection(layer1.layerData[partIndex].TotalOutline.Offset(overlap / 2));
					}
				}
			}
		}
	}
}