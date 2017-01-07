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
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	using System;
	using Pathfinding;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class AvoidCrossingPerimeters
	{
		public Polygons OutlinePolygons { get; private set; }
		public List<QuadTree<int>> OutlineQuadTrees { get; private set; }

		public Polygons BoundaryPolygons { get; private set; }
		public List<QuadTree<int>> BoundaryEdgeQuadTrees { get; private set; }
		public List<QuadTree<int>> BoundaryPointQuadTrees { get; private set; }

		private static bool storeBoundary = false;

		public AvoidCrossingPerimeters(Polygons outlinePolygons, long avoidInset)
		{
			OutlinePolygons = outlinePolygons;
			OutlineQuadTrees = OutlinePolygons.GetEdgeQuadTrees();
			BoundaryPolygons = outlinePolygons.Offset(avoidInset);
			BoundaryEdgeQuadTrees = BoundaryPolygons.GetEdgeQuadTrees();
			BoundaryPointQuadTrees = BoundaryPolygons.GetPointQuadTrees();

			if (storeBoundary)
			{
				string pointsString = outlinePolygons.WriteToString();
			}

			foreach (var polygon in BoundaryPolygons)
			{
				for (int i = 0; i < polygon.Count; i++)
				{
					if (polygon.IsVertexConcave(i))
					{
						Waypoints.AddNode(polygon[i]);
					}
				}
			}

			for (int nodeIndexA = 0; nodeIndexA < Waypoints.Nodes.Count; nodeIndexA++)
			{
				CreateLinks(nodeIndexA, nodeIndexA + 1);
			}

			removePointList = new WayPointsToRemove(Waypoints);
		}

		public IntPointPathNetwork Waypoints { get; private set; } = new IntPointPathNetwork();
		WayPointsToRemove removePointList;

		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside)
		{
			// neither needed to be moved
			if (BoundaryPolygons.FindIntersection(startPoint, endPoint) == Intersection.None
				&& BoundaryPolygons.PointIsInside((startPoint + endPoint) / 2))
			{
				return true;
			}

			removePointList.Dispose();

			pathThatIsInside.Clear();

			//Check if we are inside the boundaries
			Tuple<int, int, IntPoint> startPolyPointPosition = null;
			if (!BoundaryPolygons.PointIsInside(startPoint))
			{
				if (!BoundaryPolygons.MovePointInsideBoundary(startPoint, out startPolyPointPosition))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				startPoint = startPolyPointPosition.Item3;
			}

			Tuple<int, int, IntPoint> endPolyPointPosition = null;
			if (!BoundaryPolygons.PointIsInside(endPoint))
			{
				if (!BoundaryPolygons.MovePointInsideBoundary(endPoint, out endPolyPointPosition))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				endPoint = endPolyPointPosition.Item3;
			}

			if (BoundaryPolygons.FindIntersection(startPoint, endPoint) == Intersection.None
				&& BoundaryPolygons.PointIsInside((startPoint + endPoint) / 2))
			{
				pathThatIsInside.Add(startPoint);
				pathThatIsInside.Add(endPoint);
				return true;
			}

			IntPointNode startNode = AddTempWayPoint(removePointList, startPoint);
			IntPointNode endNode = AddTempWayPoint(removePointList, endPoint);

			// else

			Path<IntPointNode> path = Waypoints.FindPath(startNode, endNode, true);

			foreach (var node in path.nodes)
			{
				pathThatIsInside.Add(node.Position);
			}

			return true;
		}

		public bool MovePointInsideBoundary(IntPoint testPosition, out IntPoint inPolyPosition)
		{
			inPolyPosition = testPosition;
			if (!BoundaryPolygons.PointIsInside(testPosition))
			{
				Tuple<int, int, IntPoint> endPolyPointPosition = null;
				if (!BoundaryPolygons.MovePointInsideBoundary(testPosition, out endPolyPointPosition))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					inPolyPosition = new IntPoint();
					return false;
				}
				inPolyPosition = endPolyPointPosition.Item3;
			}

			return true;
		}

		public bool PointIsInsideBoundary(IntPoint intPoint)
		{
			return BoundaryPolygons.PointIsInside(intPoint);
		}

		private IntPointNode AddTempWayPoint(WayPointsToRemove removePointList, IntPoint position)
		{
			var node = Waypoints.AddNode(position);
			CreateLinks(Waypoints.Nodes.Count - 1, 0);
			removePointList.Add(node);
			return node;
		}

		private void CreateLinks(int nodeIndexA, int nodeIndexBStart)
		{
			for (int nodeIndexB = nodeIndexBStart; nodeIndexB < Waypoints.Nodes.Count; nodeIndexB++)
			{
				if (nodeIndexA != nodeIndexB
					&& LinkIsInside(nodeIndexA, nodeIndexB)
					&& !LinkIntersectsPolygon(nodeIndexA, nodeIndexB))
				{
					Waypoints.AddPathLink(Waypoints.Nodes[nodeIndexA], Waypoints.Nodes[nodeIndexB]);
				}
			}
		}

		private bool DoesLineCrossBoundary(IntPoint startPoint, IntPoint endPoint)
		{
			for (int boundaryIndex = 0; boundaryIndex < BoundaryPolygons.Count; boundaryIndex++)
			{
				Polygon boundaryPolygon = BoundaryPolygons[boundaryIndex];
				if (boundaryPolygon.Count < 1)
				{
					continue;
				}

				IntPoint lastPosition = boundaryPolygon[boundaryPolygon.Count - 1];
				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
					IntPoint currentPosition = boundaryPolygon[pointIndex];
					int startSide = startPoint.GetLineSide(lastPosition, currentPosition);
					int endSide = endPoint.GetLineSide(lastPosition, currentPosition);
					if (startSide != 0)
					{
						if (startSide + endSide == 0)
						{
							// each point is distinctly on a different side
							return true;
						}
					}
					else
					{
						// if we terminate on the line that will count as crossing
						return true;
					}

					if (endSide == 0)
					{
						// if we terminate on the line that will count as crossing
						return true;
					}

					lastPosition = currentPosition;
				}
			}
			return false;
		}

		private bool LinkIntersectsPolygon(int nodeIndexA, int nodeIndexB)
		{
			return BoundaryPolygons.FindIntersection(Waypoints.Nodes[nodeIndexA].Position, Waypoints.Nodes[nodeIndexB].Position) == Intersection.Intersect;
		}

		private bool LinkIsInside(int nodeIndexA, int nodeIndexB)
		{
			IntPoint pointA = Waypoints.Nodes[nodeIndexA].Position;

			Tuple<int, int> index = BoundaryPolygons.FindPoint(pointA, BoundaryPointQuadTrees);

			if (index != null)
			{
				IntPoint pointB = Waypoints.Nodes[nodeIndexB].Position;

				var polygon = BoundaryPolygons[index.Item1];

				IntPoint next = polygon[(index.Item2 + 1) % polygon.Count];

				if (pointB == next)
				{
					return true;
				}
			}

			return BoundaryPolygons.PointIsInside((Waypoints.Nodes[nodeIndexA].Position + Waypoints.Nodes[nodeIndexB].Position) / 2);
		}
	}
}