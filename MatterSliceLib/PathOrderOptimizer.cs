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

using MatterHackers.Pathfinding;
using MSClipperLib;
using System;
using System.Collections.Generic;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class PolyAndPoint
	{
		public int PointIndex { get; set; }

		public int PolyIndex { get; set; }

		public PolyAndPoint()
		{
		}

		public PolyAndPoint(int poly, int point)
		{
			this.PolyIndex = poly;
			this.PointIndex = point;
		}
	}

	public class PathOrderOptimizer
	{
		private readonly ConfigSettings config;
		private readonly List<Polygon> polygons = new List<Polygon>();

		public PathOrderOptimizer(ConfigSettings config)
		{
			this.config = config;
		}

		public List<PolyAndPoint> Order { get; private set; } = new List<PolyAndPoint>();

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
			this.Order.Clear();

			bool doSeamHiding = pathConfig != null && pathConfig.DoSeamHiding && !pathConfig.Spiralize;
			bool canTravelForwardOrBackward = pathConfig != null && !pathConfig.ClosedLoop;
			// Find the point that is closest to our current position (start position)

			var completatedPolygons = new HashSet<int>();

			IntPoint currentPosition = startPosition;
			while (completatedPolygons.Count < polygons.Count)
			{
				var closestPolyPoint = FindClosestPolyAndPoint(currentPosition,
					completatedPolygons,
					doSeamHiding,
					layerIndex,
					pathConfig != null ? pathConfig.LineWidth_um : 0,
					canTravelForwardOrBackward,
					out IntPoint endPosition);

				Order.Add(closestPolyPoint);
				completatedPolygons.Add(closestPolyPoint.PolyIndex);

				currentPosition = endPosition;
			}
		}

		private PolyAndPoint FindClosestPolyAndPoint(IntPoint currentPosition,
			HashSet<int> compleatedPolygons,
			bool doSeamHiding,
			int layerIndex,
			long lineWidth_um,
			bool canTravelForwardOrBackward,
			out IntPoint endPosition)
		{
			endPosition = currentPosition;
			var bestDistSquared = double.MaxValue;
			var bestResult = new PolyAndPoint();
			for (int i = 0; i < polygons.Count; i++)
			{
				if (compleatedPolygons.Contains(i))
				{
					// skip this polygon it has been processed
					continue;
				}

				int pointIndex = FindClosestPoint(polygons[i],
					currentPosition,
					doSeamHiding,
					canTravelForwardOrBackward,
					layerIndex,
					lineWidth_um,
					out double distanceSquared,
					out IntPoint polyEndPosition);

				if (distanceSquared < bestDistSquared)
				{
					bestDistSquared = distanceSquared;
					endPosition = polyEndPosition;
					bestResult = new PolyAndPoint(i, pointIndex);
				}
			}

			return bestResult;
		}

		private int FindClosestPoint(Polygon polygon,
			IntPoint currentPosition,
			bool doSeamHiding,
			bool canTravelForwardOrBackward,
			int layerIndex,
			long lineWidth_um,
			out double bestDistSquared,
			out IntPoint endPosition)
		{
			int bestPoint;
			if (canTravelForwardOrBackward || polygon.Count == 2)
			{
				int endIndex = polygon.Count - 1;

				bestDistSquared = (polygon[0] - currentPosition).LengthSquared();
				bestPoint = 0;
				endPosition = polygon[endIndex];

				// check if the end is better
				double distSquared = (polygon[endIndex] - currentPosition).LengthSquared();
				if (distSquared < bestDistSquared)
				{
					bestDistSquared = distSquared;
					bestPoint = endIndex;
					endPosition = polygon[0];
				}
			}
			else
			{
				if (doSeamHiding)
				{
					bestPoint = polygon.FindGreatestTurnIndex(currentPosition, layerIndex, lineWidth_um);
				}
				else
				{
					bestPoint = polygon.FindClosestPositionIndex(currentPosition);
				}

				bestDistSquared = (polygon[bestPoint] - currentPosition).LengthSquared();

				endPosition = polygon[bestPoint];
			}

			return bestPoint;
		}
	}
}