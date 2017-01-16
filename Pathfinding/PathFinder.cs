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

namespace MatterHackers.Pathfinding
{
	using System;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class PathFinder
	{
		private static bool storeBoundary = false;
		private WayPointsToRemove removePointList;

		public PathFinder(Polygons outlinePolygons, long avoidInset)
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
				Waypoints.AddPolygon(polygon);
			}

			// hook up path segments between the separate islands
			Polygons thinLines;
			if (OutlinePolygons.FindThinLines(avoidInset*-2, 0, out thinLines))
			{
				ThinLinePolygons = thinLines;
				foreach (var polygon in ThinLinePolygons)
				{
					if (polygon.Count > 1
						&& polygon.PolygonLength() > avoidInset/-4)
					{
						Waypoints.AddPolygon(polygon);
						// now hook up the start and end of this polygon to the existing way points
						var closestStart = BoundaryPolygons.FindClosestPoint(polygon[0]);
						var closestEnd = BoundaryPolygons.FindClosestPoint(polygon[polygon.Count-1]); // last point
						if (OutlinePolygons.PointIsInside((closestStart.Item3 + closestEnd.Item3) / 2, OutlineEdgeQuadTrees))
						{
							IntPointNode nodeA = Waypoints.FindNode(closestStart.Item3);
							IntPointNode nodeB = Waypoints.FindNode(closestEnd.Item3);
							Waypoints.AddPathLink(nodeA, nodeB);
						}
					}
				}
			}

			// this is done with merge close edges and finding candidates
			// then joining the ends of the merged segments with the closest points


			removePointList = new WayPointsToRemove(Waypoints);
		}

		public List<QuadTree<int>> BoundaryEdgeQuadTrees { get; private set; }
		public List<QuadTree<int>> BoundaryPointQuadTrees { get; private set; }
		public Polygons BoundaryPolygons { get; private set; }
		public List<QuadTree<int>> OutlineEdgeQuadTrees { get; private set; }
		public Polygons OutlinePolygons { get; private set; }
		public Polygons ThinLinePolygons { get; private set; }
		public IntPointPathNetwork Waypoints { get; private set; } = new IntPointPathNetwork();

		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside)
		{
			if (BoundaryPolygons.Count == 0)
			{
				return false;
			}

			// neither needed to be moved
			if (BoundaryPolygons.FindIntersection(startPoint, endPoint, BoundaryEdgeQuadTrees) == Intersection.None
				&& BoundaryPolygons.PointIsInside((startPoint + endPoint) / 2, BoundaryEdgeQuadTrees))
			{
				return true;
			}

			removePointList.Dispose();

			pathThatIsInside.Clear();

			//Check if we are inside the boundaries
			IntPointNode startNode = null;
			Tuple<int, int, IntPoint> startPolyPointPosition = null;
			BoundaryPolygons.MovePointInsideBoundary(startPoint, out startPolyPointPosition, BoundaryEdgeQuadTrees);
			if (startPolyPointPosition == null)
			{
				startNode = AddTempWayPoint(removePointList, startPoint);
			}
			else
			{
				startNode = AddTempWayPoint(removePointList, startPolyPointPosition.Item3);
				HookUpToEdge(startNode, startPolyPointPosition.Item1, startPolyPointPosition.Item2);
			}

			IntPointNode endNode = null;
			Tuple<int, int, IntPoint> endPolyPointPosition = null;
			BoundaryPolygons.MovePointInsideBoundary(endPoint, out endPolyPointPosition, BoundaryEdgeQuadTrees);
			if (endPolyPointPosition == null)
			{
				endNode = AddTempWayPoint(removePointList, endPoint);
			}
			else
			{
				endNode = AddTempWayPoint(removePointList, endPolyPointPosition.Item3);
				HookUpToEdge(endNode, endPolyPointPosition.Item1, endPolyPointPosition.Item2);
			}

			if (BoundaryPolygons.FindIntersection(startPoint, endPoint, BoundaryEdgeQuadTrees) != Intersection.Intersect
				&& BoundaryPolygons.PointIsInside((startPoint + endPoint) / 2, BoundaryEdgeQuadTrees))
			{
				pathThatIsInside.Add(startPoint);
				pathThatIsInside.Add(endPoint);
				return true;
			}

			if (startPolyPointPosition != null
				&& endPolyPointPosition != null
				&& startPolyPointPosition.Item1 == endPolyPointPosition.Item1
				&& startPolyPointPosition.Item2 == endPolyPointPosition.Item2)
			{
				// they are on the same edge hook them up
				Waypoints.AddPathLink(startNode, endNode);
			}

			var crossings = new List<Tuple<int, int, IntPoint>>(BoundaryPolygons.FindCrossingPoints(startNode.Position, endNode.Position, BoundaryEdgeQuadTrees));
			crossings.Sort(new PolygonAndPointDirectionSorter(startNode.Position, endNode.Position));

			IntPointNode previousNode = startNode;
			foreach (var crossing in crossings.SkipSame())
			{
				IntPointNode crossingNode = Waypoints.FindNode(crossing.Item3);
				// for every crossing try to connect it up in the waypoint data
				if (crossingNode == null)
				{
					crossingNode = AddTempWayPoint(removePointList, crossing.Item3);
					if (BoundaryPolygons.PointIsInside((previousNode.Position + crossingNode.Position) / 2, BoundaryEdgeQuadTrees))
					{
						Waypoints.AddPathLink(previousNode, crossingNode);
					}
					// also connect it to the next and prev points on the polygon it came from
					HookUpToEdge(crossingNode, crossing.Item1, crossing.Item2);
					previousNode = crossingNode;
				}

				if(crossingNode != startNode
					&& startNode.Links.Count == 0)
				{
					// connect the start to the first node
					Waypoints.AddPathLink(crossingNode, startNode);
				}
			}

			if (previousNode != endNode
				&& endNode.Links.Count == 0)
			{
				// connect the last crossing to the end node
				Waypoints.AddPathLink(previousNode, endNode);
			}

			if (BoundaryPolygons.PointIsInside((previousNode.Position + endNode.Position) / 2, BoundaryEdgeQuadTrees))
			{
				Waypoints.AddPathLink(previousNode, endNode);
			}

			Path<IntPointNode> path = Waypoints.FindPath(startNode, endNode, true);

			if (startPolyPointPosition != null)
			{
				pathThatIsInside.Add(startNode.Position);
			}

			var lastAdd = startNode.Position;
			foreach (var node in path.Nodes.SkipSamePosition(startNode.Position))
			{
				pathThatIsInside.Add(node.Position);
				lastAdd = node.Position;
			}

			if (endPolyPointPosition != null
				&& endNode.Position != lastAdd)
			{
				pathThatIsInside.Add(endNode.Position);
			}

			if (path.Nodes.Length == 0)
			{
				return false;
			}

			return true;
		}

		public bool MovePointInsideBoundary(IntPoint testPosition, out IntPoint inPolyPosition)
		{
			inPolyPosition = testPosition;
			if (!BoundaryPolygons.PointIsInside(testPosition))
			{
				Tuple<int, int, IntPoint> endPolyPointPosition = null;
				BoundaryPolygons.MovePointInsideBoundary(testPosition, out endPolyPointPosition);

				inPolyPosition = endPolyPointPosition.Item3;
				return true;
			}

			return false;
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

		private void HookUpToEdge(IntPointNode crossingNode, int polyIndex, int pointIndex)
		{
			int count = BoundaryPolygons[polyIndex].Count;
			pointIndex = (pointIndex + count) % count;
			IntPointNode prevPolyPointNode = Waypoints.FindNode(BoundaryPolygons[polyIndex][pointIndex]);
			Waypoints.AddPathLink(crossingNode, prevPolyPointNode);
			IntPointNode nextPolyPointNode = Waypoints.FindNode(BoundaryPolygons[polyIndex][(pointIndex + 1) % count]);
			Waypoints.AddPathLink(crossingNode, nextPolyPointNode);
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
			crossings.Sort(new PolygonAndPointDirectionSorter(pointA, pointB));
			IntPoint start = pointA;
			foreach (var crossing in crossings)
			{
				if (start != crossing.Item3
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