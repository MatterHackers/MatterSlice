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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Pathfinding;
using MatterHackers.QuadTree;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class OptimizedPath
	{
		public int PointIndex { get; set; }

		public int SourcePolyIndex { get; set; } = -1;

		public bool IsExtrude { get; set; } = true;

		public bool FoundPath { get; } = false;

		public OptimizedPath()
		{
		}

		public OptimizedPath(int poly, int point, bool isExtrude, bool foundPath)
		{
			this.SourcePolyIndex = poly;
			this.PointIndex = point;
			this.IsExtrude = isExtrude;
			this.FoundPath = foundPath;
		}

		public override string ToString()
		{
			var description = IsExtrude ? "extrude" : "travel";
			return $"poly: {SourcePolyIndex} point: {PointIndex} {description}";
		}
	}

	public class PathOrderOptimizer
	{
		private readonly ConfigSettings config;

		public List<INearestNeighbours<int>> Accelerator { get; private set; } = new List<INearestNeighbours<int>>();

		public Polygons Polygons { get; private set; } = new Polygons();

		public PathOrderOptimizer(ConfigSettings config)
		{
			this.config = config;
		}

		public List<OptimizedPath> OptimizedPaths { get; private set; } = new List<OptimizedPath>();

		public List<int> Indices { get; private set; } = new List<int>();

		public void AddPolygon(Polygon polygon, int polygonIndex)
		{
			if (polygon.Count > 0)
			{
				this.Polygons.Add(polygon);
				this.Indices.Add(polygonIndex);
				this.Accelerator.Add(polygon.GetNearestNeighbourAccelerator());
			}
		}

		public void AddPolygons(Polygons polygons)
		{
			for (int i = 0; i < polygons.Count; i++)
			{
				this.AddPolygon(polygons[i], i);
			}
		}

		public void Optimize(IntPoint startPosition, PathFinder pathFinder, int layerIndex, bool addMovePolys, GCodePathConfig pathConfig = null)
		{
			// pathFinder = null;

			this.OptimizedPaths.Clear();

			bool doSeamHiding = pathConfig != null && pathConfig.DoSeamHiding && !pathConfig.Spiralize;
			bool canTravelForwardOrBackward = pathConfig != null && !pathConfig.ClosedLoop;
			// Find the point that is closest to our current position (start position)

			var completedPolygons = new HashSet<int>();

			var polygonAccelerator = Polygons.GetQuadTree();

			IntPoint currentPosition = startPosition;
			while (completedPolygons.Count < Polygons.Count)
			{
				var closestPolyPoint = FindClosestPolyAndPoint(currentPosition,
					polygonAccelerator,
					completedPolygons,
					doSeamHiding,
					layerIndex,
					pathConfig != null ? pathConfig.LineWidth_um : 0,
					canTravelForwardOrBackward,
					out IntPoint endPosition);

				// if we have a path finder check if we have actually found the shortest path
				if (pathFinder != null
					&& closestPolyPoint.SourcePolyIndex != -1
					&& closestPolyPoint.PointIndex != -1
					&& closestPolyPoint.FoundPath)
				{
					// the position that we are going to move to to begin the next polygon (the other side of the endPosition)
					var nextStartPosition = Polygons[closestPolyPoint.SourcePolyIndex][closestPolyPoint.PointIndex];
					var pathPolygon = new Polygon();
					// path find the start and end that we found to find out how far it is
					if (pathFinder.CreatePathInsideBoundary(currentPosition, nextStartPosition, pathPolygon, true, layerIndex))
					{
						var pathLength = pathPolygon.PolygonLength();
						var directLength = (nextStartPosition - currentPosition).Length();

						var center = pathPolygon.GetPositionAllongPath(.5, pathConfig != null ? pathConfig.ClosedLoop : false);

						var tryAgain = false;
						do
						{
							tryAgain = false;
							if (pathLength > config.MinimumTravelToCauseRetraction_um / 10
								&& pathLength > directLength * 2)
							{
								// try to find a closer place to go to by looking at the center of the returned path
								var midPolyPoint = FindClosestPolyAndPoint(center,
									polygonAccelerator,
									completedPolygons,
									doSeamHiding,
									layerIndex,
									pathConfig != null ? pathConfig.LineWidth_um : 0,
									canTravelForwardOrBackward,
									out IntPoint midEndPosition);

								if (midPolyPoint.SourcePolyIndex != -1
									&& midPolyPoint.PointIndex != -1
									&& closestPolyPoint.FoundPath)
								{
									var midStartPosition = Polygons[midPolyPoint.SourcePolyIndex][midPolyPoint.PointIndex];

									if (pathFinder.CreatePathInsideBoundary(currentPosition, midStartPosition, pathPolygon, true, layerIndex))
									{
										var midPathLength = pathPolygon.PolygonLength();
										if (midPathLength < pathLength)
										{
											closestPolyPoint = midPolyPoint;
											endPosition = midEndPosition;
											pathLength = midPathLength;
											center = pathPolygon.GetPositionAllongPath(.5, pathConfig != null ? pathConfig.ClosedLoop : false);
											tryAgain = true;
										}
									}
								}
							}
						}
						while (tryAgain);
					}
				}

				if (closestPolyPoint.SourcePolyIndex == -1)
				{
					// could not find any next point
					break;
				}

				OptimizedPaths.Add(closestPolyPoint);
				completedPolygons.Add(closestPolyPoint.SourcePolyIndex);

				currentPosition = endPosition;
			}
		}

		private OptimizedPath FindClosestPolyAndPoint(IntPoint currentPosition,
			QuadTree<int> polygonAccelerator,
			HashSet<int> completedPolygons,
			bool doSeamHiding,
			int layerIndex,
			long lineWidth_um,
			bool canTravelForwardOrBackward,
			out IntPoint endPosition)
		{
			endPosition = currentPosition;
			var bestDistSquared = double.MaxValue;
			var bestResult = new OptimizedPath();
			foreach (var indexAndDistance in polygonAccelerator.IterateClosest(currentPosition, () => bestDistSquared))
			{
				var index = indexAndDistance.Item1;
				if (completedPolygons.Contains(Indices[index]))
				{
					// skip this polygon it has been processed
					continue;
				}

				int pointIndex = FindClosestPoint(Polygons[index],
					Accelerator[index],
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
					// the actual lookup in the input data needs to map to the source indices
					bestResult = new OptimizedPath(Indices[index], pointIndex, true, false);
				}
			}

			return bestResult;
		}

		private int FindClosestPoint(Polygon polygon,
			INearestNeighbours<int> accelerator,
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
					bestPoint = polygon.FindGreatestTurnIndex(lineWidth_um, currentPosition);
				}
				else
				{
					bestPoint = polygon.FindClosestPositionIndex(currentPosition, accelerator);
				}

				bestDistSquared = (polygon[bestPoint] - currentPosition).LengthSquared();

				endPosition = polygon[bestPoint];
			}

			return bestPoint;
		}

		public Polygon ConvertToCcwPolygon(Polygons polygons, long lineWidth_um)
		{
			var connectedPolygon = new Polygon();

			var lastPosition = polygons[OptimizedPaths[0].SourcePolyIndex][OptimizedPaths[0].PointIndex];
			foreach (var optimizedPath in this.OptimizedPaths)
			{
				var polygon = polygons[optimizedPath.SourcePolyIndex];
				var startIndex = optimizedPath.PointIndex;
				var firstPosition = polygon[startIndex];
				var length = (lastPosition - firstPosition).Length();
				if (length > lineWidth_um / 2)
				{
					// the next point is too far from the last point, not a connected path
					return null;
				}

				for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
				{
					var destination = polygon[(startIndex + positionIndex) % polygon.Count];
					// don't add exactly the same point twice
					if (connectedPolygon.Count == 0
						|| destination != lastPosition)
					{
						connectedPolygon.Add(destination);
						lastPosition = destination;
					}
				}
			}

			if (connectedPolygon.GetWindingDirection() == -1)
			{
				// reverse it
				connectedPolygon.Reverse();
			}

			return connectedPolygon;
		}
	}
}