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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Pathfinding;
using MatterHackers.QuadTree;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class SliceLayer
	{
		public Polygons AllOutlines { get; set; }

		public PathFinder PathFinder { get; set; }

		public List<LayerIsland> Islands { get; set; } = null;

		public bool CreatedInsets { get; set; } = false;

		public long LayerZ { get; set; }

		private static readonly bool outputDebugData = false;

		public static bool GetSingleIslandAngle(Polygons outline, Polygon island, out double bridgeAngle, string debugName)
		{
			bridgeAngle = -1;
			int island0PointCount = island.Count;

			// Check if the island exactly matches the outline (if it does no bridging is going to happen)
			if (outline.Count == 1 && island0PointCount == outline[0].Count)
			{
				for (int i = 0; i < island0PointCount; i++)
				{
					if (island[i] != outline[0][i])
					{
						break;
					}
				}

				// they are all the same so we don't need to change the angle
				return false;
			}

			// we need to find the first convex angle to be our start of finding the concave area
			int startIndex = 0;
			for (int i = 0; i < island0PointCount; i++)
			{
				IntPoint currentPoint = island[i];

				if (outline[0].Contains(currentPoint))
				{
					startIndex = i;
					break;
				}
			}

			double longestSide = 0;
			double bestAngle = -1;

			// check if it is concave
			for (int island0PointIndex = 0; island0PointIndex < island0PointCount; island0PointIndex++)
			{
				IntPoint curr = island[(startIndex + island0PointIndex) % island0PointCount];

				if (!outline[0].Contains(curr))
				{
					IntPoint prev = island[(startIndex + island0PointIndex + island0PointCount - 1) % island0PointCount];
					IntPoint convexStart = prev;

					// We found a concave angle. now we want to find the first non-concave angle and make
					// a bridge at the start and end angle of the concave region
					for (int j = island0PointIndex + 1; j < island0PointCount + island0PointIndex; j++)
					{
						IntPoint curr2 = island[(startIndex + j) % island0PointCount];

						if (outline[0].Contains(curr2))
						{
							IntPoint sideDelta = curr2 - convexStart;
							double lengthOfSide = sideDelta.Length();
							if (lengthOfSide > longestSide)
							{
								bestAngle = Math.Atan2(sideDelta.Y, sideDelta.X) * 180 / Math.PI;
								longestSide = lengthOfSide;
								if (outputDebugData)
								{
									island.SaveToGCode("{0} - angle {1:0.}.gcode".FormatWith(debugName, bestAngle));
								}

								island0PointIndex = j + 1;
								break;
							}
						}
					}
				}
			}

			if (bestAngle == -1)
			{
				return false;
			}

			Range0To360(ref bestAngle);
			bridgeAngle = bestAngle;
			return true;
		}

		public bool BridgeAngle(Polygons areaGoingOnTop, long perimeterExpandDistance, out double bridgeAngle, Polygons bridgeAreas, string debugName = "")
		{
			SliceLayer layerToRestOn = this;
			bridgeAngle = -1;
			var boundaryBox = new Aabb(areaGoingOnTop);
			boundaryBox.Expand(perimeterExpandDistance);
			// To detect if we have a bridge, first calculate the intersection of the current layer with the previous layer.
			// This gives us the islands that the layer rests on.

			var islandsToRestOn = new Polygons();

			foreach (LayerIsland islandToRestOn in layerToRestOn.Islands)
			{
				if (!boundaryBox.Hit(islandToRestOn.BoundingBox))
				{
					continue;
				}

				islandsToRestOn.AddRange(areaGoingOnTop.CreateIntersection(islandToRestOn.IslandOutline));
			}

			if (bridgeAreas != null)
			{
				bridgeAreas.AddRange(areaGoingOnTop.CreateDifference(layerToRestOn.AllOutlines));
			}

			if (outputDebugData)
			{
				WriteDebugData(areaGoingOnTop, layerToRestOn, islandsToRestOn);
			}

			if (islandsToRestOn.Count > 5 || islandsToRestOn.Count < 1)
			{
				return false;
			}

			if (islandsToRestOn.Count == 1)
			{
				return GetSingleIslandAngle(areaGoingOnTop, islandsToRestOn[0], out bridgeAngle, debugName);
			}

			// Find the 2 largest islands that we rest on.
			double biggestArea = 0;
			double nextBiggestArea = 0;
			int indexOfBiggest = -1;
			int indexOfNextBigest = -1;
			for (int islandIndex = 0; islandIndex < islandsToRestOn.Count; islandIndex++)
			{
				// Skip internal holes
				if (!islandsToRestOn[islandIndex].Orientation())
				{
					continue;
				}

				double area = Math.Abs(islandsToRestOn[islandIndex].Area());
				if (area > biggestArea)
				{
					if (biggestArea > nextBiggestArea)
					{
						nextBiggestArea = biggestArea;
						indexOfNextBigest = indexOfBiggest;
					}

					biggestArea = area;
					indexOfBiggest = islandIndex;
				}
				else if (area > nextBiggestArea)
				{
					nextBiggestArea = area;
					indexOfNextBigest = islandIndex;
				}
			}

			if (indexOfBiggest < 0 || indexOfNextBigest < 0)
			{
				return false;
			}

			IntPoint center1 = islandsToRestOn[indexOfBiggest].CenterOfMass();
			IntPoint center2 = islandsToRestOn[indexOfNextBigest].CenterOfMass();

			bridgeAngle = Math.Atan2(center2.Y - center1.Y, center2.X - center1.X) / Math.PI * 180;
			Range0To360(ref bridgeAngle);
			if (outputDebugData)
			{
				islandsToRestOn.SaveToGCode("{0} - angle {1:0.}.gcode".FormatWith(debugName, bridgeAngle));
			}

			return true;
		}

		private static void WriteDebugData(Polygons areaAboveToFill, SliceLayer layerToRestOn, Polygons islandsToRestOn)
		{
			// string outlineString = areaAboveToFill.WriteToString();
			string islandOutlineString = "";
			foreach (LayerIsland prevLayerIsland in layerToRestOn.Islands)
			{
				foreach (Polygon islandOutline in prevLayerIsland.IslandOutline)
				{
					islandOutlineString += islandOutline.WriteToString();
				}

				islandOutlineString += "|";
			}

			// string islandsString = islandsToRestOn.WriteToString();
		}

		public void CreateIslandData()
		{
			// Build Islands from outlines
			this.Islands = (from outline in this.AllOutlines.ProcessIntoSeparateIslands()
							select new LayerIsland(outline)).ToList();
		}

		public void GenerateInsets(ConfigSettings config, long extrusionWidth_um, long outerExtrusionWidth_um, int insetCount)
		{
			var expandThinWalls = config.ExpandThinWalls && !config.ContinuousSpiralOuterPerimeter;
			var avoidCrossingPerimeters = config.AvoidCrossingPerimeters;

			SliceLayer layer = this;
			for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				layer.Islands[islandIndex].GenerateInsets(config, extrusionWidth_um, outerExtrusionWidth_um, insetCount, avoidCrossingPerimeters);
			}

			if (!expandThinWalls)
			{
				// Remove the parts which did not generate an inset. As these parts are too small to print,
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
		}

		private static void Range0To360(ref double angle)
		{
			if (angle < 0)
			{
				angle += 360;
			}

			if (angle > 360)
			{
				angle -= 360;
			}
		}

		public void FreeIslandMemory()
		{
			Islands.Clear();
			Islands = null;
		}
	}
}