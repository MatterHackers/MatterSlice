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
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	using Pathfinding;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class SliceLayer
	{
		public Polygons AllOutlines { get; set; }
		public PathFinder PathFinder { get; set; }
		public List<LayerIsland> Islands = null;
		public long LayerZ;
		private static bool OUTPUT_DEBUG_DATA = false;

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
								if (OUTPUT_DEBUG_DATA)
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

		public bool BridgeAngle(Polygons areaAboveToFill, out double bridgeAngle, string debugName = "")
		{
			SliceLayer layerToRestOn = this;
			bridgeAngle = -1;
			Aabb boundaryBox = new Aabb(areaAboveToFill);
			//To detect if we have a bridge, first calculate the intersection of the current layer with the previous layer.
			// This gives us the islands that the layer rests on.
			Polygons islandsToRestOn = new Polygons();
			foreach (LayerIsland islandToRestOn in layerToRestOn.Islands)
			{
				if (!boundaryBox.Hit(islandToRestOn.BoundingBox))
				{
					continue;
				}

				islandsToRestOn.AddRange(areaAboveToFill.CreateIntersection(islandToRestOn.IslandOutline));
			}

			if (OUTPUT_DEBUG_DATA)
			{
				string outlineString = areaAboveToFill.WriteToString();
				string islandOutlineString = "";
				foreach (LayerIsland prevLayerIsland in layerToRestOn.Islands)
				{
					foreach (Polygon islandOutline in prevLayerIsland.IslandOutline)
					{
						islandOutlineString += islandOutline.WriteToString();
					}

					islandOutlineString += "|";
				}

				string islandsString = islandsToRestOn.WriteToString();
			}

			if (islandsToRestOn.Count > 5 || islandsToRestOn.Count < 1)
			{
				return false;
			}

			if (islandsToRestOn.Count == 1)
			{
				return GetSingleIslandAngle(areaAboveToFill, islandsToRestOn[0], out bridgeAngle, debugName);
			}

			// Find the 2 largest islands that we rest on.
			double biggestArea = 0;
			double nextBiggestArea = 0;
			int indexOfBiggest = -1;
			int indexOfNextBigest = -1;
			for (int islandIndex = 0; islandIndex < islandsToRestOn.Count; islandIndex++)
			{
				//Skip internal holes
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
			if (OUTPUT_DEBUG_DATA)
			{
				islandsToRestOn.SaveToGCode("{0} - angle {1:0.}.gcode".FormatWith(debugName, bridgeAngle));
			}
			return true;
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

		public void GenerateFillConsideringBridging(Polygons bottomFillIsland, Polygons bottomFillLines, ConfigSettings config, Polygons bridgePolygons, string debugName = "")
		{
			double bridgeAngle = 0;
			if (bridgePolygons != null && this.BridgeAngle(bottomFillIsland, out bridgeAngle))
			{
				// TODO: Make this code handle very complex pathing between different sizes or layouts of support under the island to fill.
				Infill.GenerateLinePaths(bottomFillIsland, bridgePolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, bridgeAngle);
			}
			else
			{
				Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle);
			}
		}

		public void GenerateInsets(int extrusionWidth_um, int outerExtrusionWidth_um, int insetCount, bool expandThinWalls)
		{
			SliceLayer layer = this;
			for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
			{
				layer.Islands[islandIndex].GenerateInsets(extrusionWidth_um, outerExtrusionWidth_um, insetCount);
			}

			if (!expandThinWalls)
			{
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
	}
}