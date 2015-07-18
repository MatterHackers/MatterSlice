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
		public static void RemoveVolumesIntersections(List<SliceVolumeStorage> volumes)
		{
			//Go trough all the volumes, and remove the previous volume outlines from our own outline, so we never have overlapped areas.
			for (int volumeToRemoveFromIndex = volumes.Count - 1; volumeToRemoveFromIndex >= 0; volumeToRemoveFromIndex--)
			{
				for (int volumeToRemoveIndex = volumeToRemoveFromIndex - 1; volumeToRemoveIndex >= 0; volumeToRemoveIndex--)
				{
					for (int layerIndex = 0; layerIndex < volumes[volumeToRemoveFromIndex].layers.Count; layerIndex++)
					{
						SliceLayer layerToRemoveFrom = volumes[volumeToRemoveFromIndex].layers[layerIndex];
						SliceLayer layerToRemove = volumes[volumeToRemoveIndex].layers[layerIndex];
						for (int partToRemoveFromIndex = 0; partToRemoveFromIndex < layerToRemoveFrom.parts.Count; partToRemoveFromIndex++)
						{
							for (int partToRemove = 0; partToRemove < layerToRemove.parts.Count; partToRemove++)
							{
								layerToRemoveFrom.parts[partToRemoveFromIndex].TotalOutline = layerToRemoveFrom.parts[partToRemoveFromIndex].TotalOutline.CreateDifference(layerToRemove.parts[partToRemove].TotalOutline);
							}
						}
					}
				}
			}
		}

		//Expand each layer a bit and then keep the extra overlapping parts that overlap with other volumes.
		//This generates some overlap in dual extrusion, for better bonding in touching parts.
		public static void OverlapMultipleVolumesSlightly(List<SliceVolumeStorage> volumes, int overlap)
		{
			if (volumes.Count < 2 || overlap <= 0)
			{
				return;
			}

			for (int layerIndex = 0; layerIndex < volumes[0].layers.Count; layerIndex++)
			{
				Polygons fullLayer = new Polygons();
				for (int volIdx = 0; volIdx < volumes.Count; volIdx++)
				{
					SliceLayer layer1 = volumes[volIdx].layers[layerIndex];
					for (int p1 = 0; p1 < layer1.parts.Count; p1++)
					{
						fullLayer = fullLayer.CreateUnion(layer1.parts[p1].TotalOutline.Offset(20));
					}
				}
				fullLayer = fullLayer.Offset(-20);

				for (int volumeIndex = 0; volumeIndex < volumes.Count; volumeIndex++)
				{
					SliceLayer layer1 = volumes[volumeIndex].layers[layerIndex];
					for (int partIndex = 0; partIndex < layer1.parts.Count; partIndex++)
					{
						layer1.parts[partIndex].TotalOutline = fullLayer.CreateIntersection(layer1.parts[partIndex].TotalOutline.Offset(overlap / 2));
					}
				}
			}
		}
	}
}