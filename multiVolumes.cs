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
	using System;
	using System.Linq;
	using Polygons = List<List<IntPoint>>;

    public class BooleanProcessing
    {
        int currentExtruder = 0;
        int numberOfOpens = 0;
        public List<ExtruderLayers> FinalLayers { get; internal set; }
        List<int> layersToRemove = new List<int>();
        Stack<int> operandsIndexStack = new Stack<int>();
        enum BooleanType { None, Union, Difference, Intersection };

        public BooleanProcessing(List<ExtruderLayers> allPartsLayers, string booleanOpperations)
        {
            int parseIndex = 0;
            while (parseIndex < booleanOpperations.Length)
            {
                BooleanType typeToDo = BooleanType.None;

                switch (booleanOpperations[parseIndex])
                {
                    case '(': // start union
                    case '[': // start intersection
                    case '{': // start difference
                        numberOfOpens++;
                        parseIndex++;
                        break;

                    case ')': // end union
                        typeToDo = BooleanType.Union;
                        parseIndex++;
                        break;

                    case '}': // end difference
                        typeToDo = BooleanType.Difference;
                        parseIndex++;
                        break;

                    case ']': // end intersection
                        typeToDo = BooleanType.Intersection;
                        parseIndex++;
                        break;

                    case ',':
                        parseIndex++;
                        break;

                    default:
                        // get the number for the operand index
                        int skipCount = 0;
                        operandsIndexStack.Push(GetNextNumber(booleanOpperations, parseIndex, out skipCount));
                        parseIndex += skipCount;
                        break;
                }

                if (typeToDo != BooleanType.None)
                {
                    numberOfOpens--;
                    int meshToAddIndex = operandsIndexStack.Pop();
                    int destMeshIndex = operandsIndexStack.Pop();
                    int layersToMerge = Math.Max(allPartsLayers[meshToAddIndex].Layers.Count, allPartsLayers[destMeshIndex].Layers.Count);
                    for (int layerIndex = 0; layerIndex < allPartsLayers[destMeshIndex].Layers.Count; layerIndex++)
                    {
                        SliceLayer layersToUnionInto = allPartsLayers[destMeshIndex].Layers[layerIndex];
                        SliceLayer layersToAddToUnion = allPartsLayers[meshToAddIndex].Layers[layerIndex];
                        DoLayerBooleans(layersToUnionInto, layersToAddToUnion, typeToDo);
                    }
                    layersToRemove.Add(meshToAddIndex);

                    operandsIndexStack.Push(destMeshIndex);

                    if (numberOfOpens == 0)
                    {
                        currentExtruder++;
                    }
                }
            }

            layersToRemove.Sort();
            for (int i = layersToRemove.Count - 1; i >= 0; i--)
            {
                allPartsLayers.RemoveAt(layersToRemove[i]);
            }
        }

        private int GetNextNumber(string numberString, int index, out int skipCount)
        {
            string digits = new string(numberString.Substring(index).TakeWhile(c => Char.IsDigit(c)).ToArray());
            skipCount = digits.Length;
            int result;
            if (Int32.TryParse(digits, out result))
            {
                return result;
            }

            throw new FormatException("not a number");
        }

        private static void DoLayerBooleans(SliceLayer layersToUnionInto, SliceLayer layersToAddToUnion, BooleanType booleanType)
        {
            int sliceDataIndex = 0;
            if (layersToAddToUnion.Islands.Count > 1
                || layersToUnionInto.Islands.Count > 1)
            {
                throw new Exception("check this out. LBB");
            }
            switch (booleanType)
            {
                case BooleanType.Union:
                    if (layersToAddToUnion.Islands.Count == 0
                        || layersToAddToUnion.Islands[sliceDataIndex] == null)
                    {
                        int a = 0;
                        // do nothing
                    }
                    else if (layersToUnionInto.Islands.Count == 0
                        || layersToUnionInto.Islands[sliceDataIndex] == null)
                    {
                        layersToUnionInto.Islands = layersToAddToUnion.Islands;
                    }
                    else
                    {
                        layersToUnionInto.Islands[sliceDataIndex].IslandOutline = layersToUnionInto.Islands[sliceDataIndex].IslandOutline.CreateUnion(layersToAddToUnion.Islands[sliceDataIndex].IslandOutline);
                    }
                    break;
                case BooleanType.Difference:
                    layersToUnionInto.Islands[sliceDataIndex].IslandOutline = layersToUnionInto.Islands[sliceDataIndex].IslandOutline.CreateDifference(layersToAddToUnion.Islands[sliceDataIndex].IslandOutline);
                    break;
                case BooleanType.Intersection:
                    layersToUnionInto.Islands[sliceDataIndex].IslandOutline = layersToUnionInto.Islands[sliceDataIndex].IslandOutline.CreateIntersection(layersToAddToUnion.Islands[sliceDataIndex].IslandOutline);
                    break;
            }
        }
    }

	public static class MultiVolumes
	{
		public static void ProcessBooleans(List<ExtruderLayers> allPartsLayers, string booleanOpperations)
		{
			BooleanProcessing processor = new BooleanProcessing(allPartsLayers, booleanOpperations);
		}

		public static void RemoveVolumesIntersections(List<ExtruderLayers> volumes)
		{
			//Go trough all the volumes, and remove the previous volume outlines from our own outline, so we never have overlapped areas.
			for (int volumeToRemoveFromIndex = volumes.Count - 1; volumeToRemoveFromIndex >= 0; volumeToRemoveFromIndex--)
			{
				for (int volumeToRemoveIndex = volumeToRemoveFromIndex - 1; volumeToRemoveIndex >= 0; volumeToRemoveIndex--)
				{
					for (int layerIndex = 0; layerIndex < volumes[volumeToRemoveFromIndex].Layers.Count; layerIndex++)
					{
						SliceLayer layerToRemoveFrom = volumes[volumeToRemoveFromIndex].Layers[layerIndex];
						SliceLayer layerToRemove = volumes[volumeToRemoveIndex].Layers[layerIndex];
						for (int partToRemoveFromIndex = 0; partToRemoveFromIndex < layerToRemoveFrom.Islands.Count; partToRemoveFromIndex++)
						{
							for (int partToRemove = 0; partToRemove < layerToRemove.Islands.Count; partToRemove++)
							{
								layerToRemoveFrom.Islands[partToRemoveFromIndex].IslandOutline = layerToRemoveFrom.Islands[partToRemoveFromIndex].IslandOutline.CreateDifference(layerToRemove.Islands[partToRemove].IslandOutline);
							}
						}
					}
				}
			}
		}

		//Expand each layer a bit and then keep the extra overlapping parts that overlap with other volumes.
		//This generates some overlap in dual extrusion, for better bonding in touching parts.
		public static void OverlapMultipleVolumesSlightly(List<ExtruderLayers> volumes, int overlap)
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
					SliceLayer layer1 = volumes[volIdx].Layers[layerIndex];
					for (int p1 = 0; p1 < layer1.Islands.Count; p1++)
					{
						fullLayer = fullLayer.CreateUnion(layer1.Islands[p1].IslandOutline.Offset(20));
					}
				}
				fullLayer = fullLayer.Offset(-20);

				for (int volumeIndex = 0; volumeIndex < volumes.Count; volumeIndex++)
				{
					SliceLayer layer1 = volumes[volumeIndex].Layers[layerIndex];
					for (int partIndex = 0; partIndex < layer1.Islands.Count; partIndex++)
					{
						layer1.Islands[partIndex].IslandOutline = fullLayer.CreateIntersection(layer1.Islands[partIndex].IslandOutline.Offset(overlap / 2));
					}
				}
			}
		}
	}
}