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
		public List<QuadTree<int>> OutlineEdgeQuadTrees { get; private set; }

		public Polygons BoundaryPolygons { get; private set; }
		public List<QuadTree<int>> BoundaryEdgeQuadTrees { get; private set; }
		public List<QuadTree<int>> BoundaryPointQuadTrees { get; private set; }

		private static bool storeBoundary = false;

		public AvoidCrossingPerimeters(Polygons outlinePolygons, long avoidInset)
		{
			OutlinePolygons = outlinePolygons;
			OutlineEdgeQuadTrees = OutlinePolygons.GetEdgeQuadTrees();
			BoundaryPolygons = outlinePolygons.Offset(avoidInset);
			BoundaryEdgeQuadTrees = BoundaryPolygons.GetEdgeQuadTrees();
			BoundaryPointQuadTrees = BoundaryPolygons.GetPointQuadTrees();

			if (storeBoundary)
			{
				string pointsString = outlinePolygons.WriteToString();
			}

			foreach (var polygon in BoundaryPolygons)
			{
				Waypoints.AddClosedPolygon(polygon);
			}

			removePointList = new WayPointsToRemove(Waypoints);
		}

		public IntPointPathNetwork Waypoints { get; private set; } = new IntPointPathNetwork();
		WayPointsToRemove removePointList;

		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside)
		{
			// neither needed to be moved
			if (BoundaryPolygons.FindIntersection(startPoint, endPoint, BoundaryEdgeQuadTrees) == Intersection.None
				&& BoundaryPolygons.PointIsInside((startPoint + endPoint) / 2, BoundaryEdgeQuadTrees))
			{
				return true;
			}

			removePointList.Dispose();

			pathThatIsInside.Clear();

			//Check if we are inside the boundaries
			Tuple<int, int, IntPoint> startPolyPointPosition = null;
			if (!BoundaryPolygons.PointIsInside(startPoint, BoundaryEdgeQuadTrees))
			{
				if (!BoundaryPolygons.MovePointInsideBoundary(startPoint, out startPolyPointPosition, BoundaryEdgeQuadTrees))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				startPoint = startPolyPointPosition.Item3;
			}

			Tuple<int, int, IntPoint> endPolyPointPosition = null;
			if (!BoundaryPolygons.PointIsInside(endPoint, BoundaryEdgeQuadTrees))
			{
				if (!BoundaryPolygons.MovePointInsideBoundary(endPoint, out endPolyPointPosition, BoundaryEdgeQuadTrees))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				endPoint = endPolyPointPosition.Item3;
			}

			if (BoundaryPolygons.FindIntersection(startPoint, endPoint, BoundaryEdgeQuadTrees) == Intersection.None
				&& BoundaryPolygons.PointIsInside((startPoint + endPoint) / 2, BoundaryEdgeQuadTrees))
			{
				pathThatIsInside.Add(startPoint);
				pathThatIsInside.Add(endPoint);
				return true;
			}

			var crossings = new List<Tuple<int, int, IntPoint>>(BoundaryPolygons.FindCrossingPoints(startPoint, endPoint, BoundaryEdgeQuadTrees));
			crossings.Sort(new DirectionSorter(startPoint, endPoint));

			IntPointNode startNode = AddTempWayPoint(removePointList, startPoint);
			IntPointNode endNode = AddTempWayPoint(removePointList, endPoint);

			int index = 0;
			IntPointNode previousNode = startNode;
			foreach (var crossing in crossings.SkipSame())
			{
				// for every crossing try to connect it up in the waypoint data
				IntPointNode crossingNode = AddTempWayPoint(removePointList, crossing.Item3);
				if (BoundaryPolygons.PointIsInside((previousNode.Position + crossingNode.Position) / 2, BoundaryEdgeQuadTrees))
				{
					Waypoints.AddPathLink(previousNode, crossingNode);
				}
				// also connect it to the next and prev points on the polygon it came from
				IntPointNode prevPolyPointNode = Waypoints.FindNode(BoundaryPolygons[crossing.Item1][crossing.Item2]);
				Waypoints.AddPathLink(crossingNode, prevPolyPointNode);
				IntPointNode nextPolyPointNode = Waypoints.FindNode(BoundaryPolygons[crossing.Item1][(crossing.Item2 + 1)%BoundaryPolygons[crossing.Item1].Count]);
				Waypoints.AddPathLink(crossingNode, nextPolyPointNode);
				previousNode = crossingNode;
			}

			Waypoints.AddPathLink(previousNode, endNode);

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
			removePointList.Add(node);
			return node;
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
			return BoundaryPolygons.FindIntersection(Waypoints.Nodes[nodeIndexA].Position, Waypoints.Nodes[nodeIndexB].Position, BoundaryEdgeQuadTrees) == Intersection.Intersect;
		}

		private bool LinkIsInside(int nodeIndexA, int nodeIndexB)
		{
			IntPoint pointA = Waypoints.Nodes[nodeIndexA].Position;
			IntPoint pointB = Waypoints.Nodes[nodeIndexB].Position;

			Tuple<int, int> index = BoundaryPolygons.FindPoint(pointA, BoundaryPointQuadTrees);
			if (index != null)
			{
				var polygon = BoundaryPolygons[index.Item1];

				IntPoint next = polygon[(index.Item2 + 1) % polygon.Count];
				if (pointB == next)
				{
					return true;
				}

				next = polygon[(index.Item2 + polygon.Count - 1) % polygon.Count];
				if (pointB == next)
				{
					return true;
				}
			}


			if (!BoundaryPolygons.PointIsInside((pointA + pointB) / 2, BoundaryEdgeQuadTrees))
			{
				return false;
			}

			var crossings = new List<Tuple<int, int, IntPoint>>(BoundaryPolygons.FindCrossingPoints(pointA, pointB, BoundaryEdgeQuadTrees));
			crossings.Sort(new MatterHackers.MatterSlice.DirectionSorter(pointA, pointB));
			IntPoint start = pointA;
			foreach(var crossing in crossings)
			{
				if(start != crossing.Item3
					&& !BoundaryPolygons.PointIsInside((start + crossing.Item3) / 2, BoundaryEdgeQuadTrees))
				{
					return false;
				}
				start = crossing.Item3;
			}

			return true;
		}
	}
}