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

using MSClipperLib;
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
        Stack<int> extruderIndexStack = new Stack<int>();
        enum BooleanType { None, Union, Difference, Intersection };

        public BooleanProcessing(List<ExtruderLayers> extruders, string booleanOpperations)
        {
            int parseIndex = 0;
            int totalLayers = extruders[0].Layers.Count;
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
                        extruderIndexStack.Push(GetNextNumber(booleanOpperations, parseIndex, out skipCount));
                        parseIndex += skipCount;
                        break;
                }

                if (typeToDo != BooleanType.None)
                {
                    numberOfOpens--;
                    int extruderBIndex = extruderIndexStack.Pop();
                    int extruderAIndex = extruderIndexStack.Pop();
                    if (extruders[extruderBIndex].Layers.Count != extruders[extruderAIndex].Layers.Count ||
                        extruders[extruderAIndex].Layers.Count != totalLayers)
                    {
                        throw new Exception("These should be the same.");
                    }

                    for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
                    {
                        SliceLayer layerA = extruders[extruderAIndex].Layers[layerIndex];
                        SliceLayer layerB = extruders[extruderBIndex].Layers[layerIndex];
                        DoLayerBooleans(layerA, layerB, typeToDo);
                    }
                    layersToRemove.Add(extruderBIndex);

                    extruderIndexStack.Push(extruderAIndex);

                    if (numberOfOpens == 0)
                    {
                        currentExtruder++;
                    }
                }
            }

            layersToRemove.Sort();
            for (int i = layersToRemove.Count - 1; i >= 0; i--)
            {
                extruders.RemoveAt(layersToRemove[i]);
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

        private static void DoLayerBooleans(SliceLayer layersA, SliceLayer layersB, BooleanType booleanType)
        {
            switch (booleanType)
            {
                case BooleanType.Union:
                    if (layersB.AllOutlines.Count == 0)
                    {
                        // do nothing we will keep the content of A
                    }
                    else if (layersA.AllOutlines.Count == 0)
                    {
                        // there is nothing in A so set it to the content of B
                        layersA.AllOutlines = layersB.AllOutlines;
                    }
                    else
                    {
                        layersA.AllOutlines = layersA.AllOutlines.CreateUnion(layersB.AllOutlines);
                    }
                    break;
                case BooleanType.Difference:
                    layersA.AllOutlines = layersA.AllOutlines.CreateDifference(layersB.AllOutlines);
                    break;
                case BooleanType.Intersection:
                    layersA.AllOutlines = layersA.AllOutlines.CreateIntersection(layersB.AllOutlines);
                    break;
            }
        }
    }

	public static class MultiExtruders
	{
		public static void ProcessBooleans(List<ExtruderLayers> allPartsLayers, string booleanOpperations)
		{
			BooleanProcessing processor = new BooleanProcessing(allPartsLayers, booleanOpperations);
		}

		public static void RemoveExtruderIntersections(List<ExtruderLayers> extruders)
		{
            //Go trough all the extruders, and remove the previous extruders outlines from our own outline, so we never have overlapped areas.
            for (int extruderIndex = extruders.Count - 1; extruderIndex >= 0; extruderIndex--)
			{
				for (int otherExtuderIndex = extruderIndex - 1; otherExtuderIndex >= 0; otherExtuderIndex--)
				{
					for (int layerIndex = 0; layerIndex < extruders[extruderIndex].Layers.Count; layerIndex++)
					{
						SliceLayer layerToRemoveFrom = extruders[extruderIndex].Layers[layerIndex];
						SliceLayer layerToRemove = extruders[otherExtuderIndex].Layers[layerIndex];
                        layerToRemoveFrom.AllOutlines = layerToRemoveFrom.AllOutlines.CreateDifference(layerToRemove.AllOutlines);
					}
				}
			}
		}

		//Expand each layer a bit and then keep the extra overlapping parts that overlap with other extruders.
		//This generates some overlap in dual extrusion, for better bonding in touching parts.
		public static void OverlapMultipleExtrudersSlightly(List<ExtruderLayers> extruders, int overlapUm)
		{
			if (extruders.Count < 2 || overlapUm <= 0)
			{
				return;
			}

			for (int layerIndex = 0; layerIndex < extruders[0].Layers.Count; layerIndex++)
			{
				Polygons fullLayer = new Polygons();
				for (int extruderIndex = 0; extruderIndex < extruders.Count; extruderIndex++)
				{
					SliceLayer layer1 = extruders[extruderIndex].Layers[layerIndex];
                    fullLayer = fullLayer.CreateUnion(layer1.AllOutlines.Offset(20));
				}
				fullLayer = fullLayer.Offset(-20);

				for (int extruderIndex = 0; extruderIndex < extruders.Count; extruderIndex++)
				{
					SliceLayer layer1 = extruders[extruderIndex].Layers[layerIndex];
                    layer1.AllOutlines = fullLayer.CreateIntersection(layer1.AllOutlines.Offset(overlapUm / 2));
				}
			}
		}
	}
}