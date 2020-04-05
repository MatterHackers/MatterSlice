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
using MatterHackers.Pathfinding;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class PathOrderOptimizer
	{
		public List<int> BestIslandOrderIndex { get; private set; } = new List<int>();

		public List<int> StartIndexInPolygon { get; private set; } = new List<int>();

		private readonly List<Polygon> polygons = new List<Polygon>();
		private readonly ConfigSettings config;

		public PathOrderOptimizer(ConfigSettings config)
		{
			this.config = config;
		}

		public void AddPolygon(Polygon polygon)
		{
			this.polygons.Add(polygon);
		}

		public void AddPolygons(Polygons polygons)
		{
			for (int i = 0; i < polygons.Count; i++)
			{
				this.polygons.Add(polygons[i]);
			}
		}

		public void Optimize(IntPoint startPosition, PathFinder pathFinder, int layerIndex, GCodePathConfig pathConfig = null)
		{
			bool canTravelForwardOrBackward = pathConfig != null && !pathConfig.ClosedLoop;
			// Find the point that is closest to our current position (start position)
			bool[] polygonHasBeenAdded = new bool[this.polygons.Count];
			for (int polygonIndex = 0; polygonIndex < this.polygons.Count; polygonIndex++)
			{
				Polygon currentPolygon = this.polygons[polygonIndex];
				if (canTravelForwardOrBackward || currentPolygon.Count < 3)
				{
					this.StartIndexInPolygon.Add(0);
				}
				else // This is a closed loop.
				{
					// some code for helping create unit tests
					// string polyString = currentPolygon.WriteToString();
					// currentPolygon.SaveToGCode("perimeter.gcode");

					// this is our new seam hiding code
					int bestPointIndex;
					if (pathConfig != null
						&& pathConfig.DoSeamHiding
						&& !pathConfig.Spiralize)
					{
						bestPointIndex = currentPolygon.FindGreatestTurnIndex(startPosition, layerIndex, pathConfig.LineWidth_um);
					}
					else
					{
						bestPointIndex = currentPolygon.FindClosestPositionIndex(startPosition);
					}

					this.StartIndexInPolygon.Add(bestPointIndex);
				}
			}

			IntPoint currentPosition = startPosition;
			// We loop over the polygon list twice, at each inner loop we only pick one polygon.
			for (int polygonIndexOuter = 0; polygonIndexOuter < this.polygons.Count; polygonIndexOuter++)
			{
				int bestPolygonIndex = -1;
				double bestDist = double.MaxValue;
				for (int polygonIndexInner = 0; polygonIndexInner < this.polygons.Count; polygonIndexInner++)
				{
					if (polygonHasBeenAdded[polygonIndexInner] || this.polygons[polygonIndexInner].Count < 1)
					{
						continue;
					}

					// If there are only 2 points (a single line) or the path is marked as travel both ways, we are willing to start from the start or the end.
					if (this.polygons[polygonIndexInner].Count == 2 || canTravelForwardOrBackward)
					{
						double distToSart = (this.polygons[polygonIndexInner][0] - currentPosition).LengthSquared();
						if (distToSart <= bestDist)
						{
							bestPolygonIndex = polygonIndexInner;
							bestDist = distToSart;
							this.StartIndexInPolygon[polygonIndexInner] = 0;
						}

						int endIndex = this.polygons[bestPolygonIndex].Count - 1;
						double distToEnd = (this.polygons[polygonIndexInner][endIndex] - currentPosition).LengthSquared();
						if (distToEnd < bestDist)
						{
							bestPolygonIndex = polygonIndexInner;
							bestDist = distToEnd;
							this.StartIndexInPolygon[polygonIndexInner] = 1;
						}
					}
					else
					{
						double dist = (this.polygons[polygonIndexInner][this.StartIndexInPolygon[polygonIndexInner]] - currentPosition).LengthSquared();
						if (dist < bestDist)
						{
							bestPolygonIndex = polygonIndexInner;
							bestDist = dist;
						}
					}
				}

				if (bestPolygonIndex > -1)
				{
					if (this.polygons[bestPolygonIndex].Count == 2 || canTravelForwardOrBackward)
					{
						// get the point that is opposite from the one we started on
						int startIndex = this.StartIndexInPolygon[bestPolygonIndex];
						if (startIndex == 0)
						{
							int endIndex = this.polygons[bestPolygonIndex].Count - 1;
							currentPosition = this.polygons[bestPolygonIndex][endIndex];
						}
						else
						{
							currentPosition = this.polygons[bestPolygonIndex][0];
						}
					}
					else
					{
						currentPosition = this.polygons[bestPolygonIndex][this.StartIndexInPolygon[bestPolygonIndex]];
					}

					polygonHasBeenAdded[bestPolygonIndex] = true;
					this.BestIslandOrderIndex.Add(bestPolygonIndex);
				}
			}

			currentPosition = startPosition;
			foreach (int bestPolygonIndex in this.BestIslandOrderIndex)
			{
				int bestStartPoint = -1;
				double bestDist = double.MaxValue;
				if (canTravelForwardOrBackward)
				{
					bestDist = (this.polygons[bestPolygonIndex][0] - currentPosition).LengthSquared();
					bestStartPoint = 0;

					// check if the end is better
					int endIndex = this.polygons[bestPolygonIndex].Count - 1;
					double dist = (this.polygons[bestPolygonIndex][endIndex] - currentPosition).LengthSquared();
					if (dist < bestDist)
					{
						bestStartPoint = endIndex;
						bestDist = dist;
					}

					this.StartIndexInPolygon[bestPolygonIndex] = bestStartPoint;
				}
				else
				{
					for (int pointIndex = 0; pointIndex < this.polygons[bestPolygonIndex].Count; pointIndex++)
					{
						double dist = (this.polygons[bestPolygonIndex][pointIndex] - currentPosition).LengthSquared();
						if (dist < bestDist)
						{
							bestStartPoint = pointIndex;
							bestDist = dist;
						}
					}
				}

				if (this.polygons[bestPolygonIndex].Count == 2 || canTravelForwardOrBackward)
				{
					if (bestStartPoint == 0)
					{
						var endIndex = this.polygons[bestPolygonIndex].Count - 1;
						currentPosition = this.polygons[bestPolygonIndex][endIndex];
					}
					else
					{
						currentPosition = this.polygons[bestPolygonIndex][0];
					}
				}
				else
				{
					currentPosition = this.polygons[bestPolygonIndex][bestStartPoint];
				}
			}
		}
	}
}