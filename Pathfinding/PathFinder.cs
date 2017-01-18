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
	using System.Linq;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class PathFinder
	{
		private static bool simpleHookup = true;
		private static bool storeBoundary = false;
		private WayPointsToRemove removePointList;

		public PathFinder(Polygons outlinePolygons, long avoidInset)
		{
			OutlinePolygons = outlinePolygons;
			OutlineEdgeQuadTrees = OutlinePolygons.GetEdgeQuadTrees();
			OutlinePointQuadTrees = OutlinePolygons.GetPointQuadTrees();
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
			if (simpleHookup) // do a simple hookup
			{
				for (int indexA = 0; indexA < BoundaryPolygons.Count - 1; indexA++)
				{
					// find the closest two point between A and any other polygon
					IntPoint bestAPos = BoundaryPolygons[indexA].Center();
					var bestBPoly = BoundaryPolygons.FindClosestPoint(bestAPos, indexA);
					bestAPos = BoundaryPolygons[indexA].FindClosestPoint(bestBPoly.Item3).Item2;
					var bestBPos = BoundaryPolygons[bestBPoly.Item1].FindClosestPoint(bestAPos).Item2;
					bestAPos = BoundaryPolygons[indexA].FindClosestPoint(bestBPos).Item2;
					bestBPos = BoundaryPolygons[bestBPoly.Item1].FindClosestPoint(bestAPos).Item2;

					// hook the polygons up along this connection
					IntPointNode nodeA = Waypoints.FindNode(bestAPos);
					IntPointNode nodeB = Waypoints.FindNode(bestBPos);
					Waypoints.AddPathLink(nodeA, nodeB);
				}
			}
			else // hook up using thin lines code
			{
				// this is done with merge close edges and finding candidates
				// then joining the ends of the merged segments with the closest points
				Polygons thinLines;
				if (OutlinePolygons.FindThinLines(avoidInset * -2, 0, out thinLines))
				{
					ThinLinePolygons = thinLines;
					foreach (var polygon in ThinLinePolygons)
					{
						if (polygon.Count > 1
							&& polygon.PolygonLength() > avoidInset / -4)
						{
							Waypoints.AddPolygon(polygon, false);
							// now hook up the start and end of this polygon to the existing way points
							var closestStart = BoundaryPolygons.FindClosestPoint(polygon[0]);
							var closestEnd = BoundaryPolygons.FindClosestPoint(polygon[polygon.Count - 1]); // last point
							if (OutlinePolygons.PointIsInside((closestStart.Item3 + closestEnd.Item3) / 2, OutlineEdgeQuadTrees))
							{
								IntPointNode nodeA = Waypoints.FindNode(closestStart.Item3);
								IntPointNode nodeB = Waypoints.FindNode(closestEnd.Item3);
								Waypoints.AddPathLink(nodeA, nodeB);
							}
						}
					}
				}
			}

			removePointList = new WayPointsToRemove(Waypoints);
		}

		public List<QuadTree<int>> BoundaryEdgeQuadTrees { get; private set; }
		public List<QuadTree<int>> BoundaryPointQuadTrees { get; private set; }
		public Polygons BoundaryPolygons { get; private set; }
		public List<QuadTree<int>> OutlineEdgeQuadTrees { get; private set; }
		public List<QuadTree<int>> OutlinePointQuadTrees { get; private set; }
		public Polygons OutlinePolygons { get; private set; }
		public Polygons ThinLinePolygons { get; private set; }
		public IntPointPathNetwork Waypoints { get; private set; } = new IntPointPathNetwork();

		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside, bool optomizePath = true)
		{
			double z = startPoint.Z;
			startPoint.Z = 0;
			endPoint.Z = 0;
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
				pathThatIsInside.Add(new IntPoint(startPoint, z));
				pathThatIsInside.Add(new IntPoint(endPoint, z));
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

				if (crossingNode != startNode
					&& startNode.Links.Count == 0)
				{
					// connect the start to the first node
					Waypoints.AddPathLink(crossingNode, startNode);
				}
			}

			if (previousNode != endNode
				&& endNode.Links.Count == 0)
			{
				if (BoundaryPolygons.PointIsInside((previousNode.Position + endNode.Position) / 2, BoundaryEdgeQuadTrees))
				{
					// connect the last crossing to the end node
					Waypoints.AddPathLink(previousNode, endNode);
				}
				else // hook the end node up to the closest line
				{
					var closestEdgeToEnd = BoundaryPolygons.FindClosestPoint(endNode.Position).Item3;

					// hook the polygons up along this connection
					IntPointNode nodeA = Waypoints.FindNode(closestEdgeToEnd);
					Waypoints.AddPathLink(endNode, nodeA);
				}
			}

			if (BoundaryPolygons.PointIsInside((previousNode.Position + endNode.Position) / 2, BoundaryEdgeQuadTrees))
			{
				Waypoints.AddPathLink(previousNode, endNode);
			}

			Path<IntPointNode> path = Waypoints.FindPath(startNode, endNode, true);

			if (startPolyPointPosition != null)
			{
				pathThatIsInside.Add(new IntPoint(startNode.Position, z));
			}

			var lastAdd = startNode.Position;
			foreach (var node in path.Nodes.SkipSamePosition(startNode.Position))
			{
				pathThatIsInside.Add(new IntPoint(node.Position, z));
				lastAdd = node.Position;
			}

			if (endPolyPointPosition != null
				&& endNode.Position != lastAdd)
			{
				pathThatIsInside.Add(new IntPoint(endNode.Position, z));
			}

			if (path.Nodes.Length == 0)
			{
				return false;
			}

			if (optomizePath)
			{
				OptomizePathPoints(pathThatIsInside);
			}

			if (true)// check that the path we are going to use does not exit the outline
			{
				for (int i = 0; i < pathThatIsInside.Count - 1; i++)
				{
					var start = pathThatIsInside[i];
					var end = pathThatIsInside[i + 1];

					if (!OutlinePolygons.PointIsInside(start + (end - start) / 4, OutlinePointQuadTrees)
						|| !OutlinePolygons.PointIsInside(start + (end - start) / 2, OutlinePointQuadTrees)
						|| !OutlinePolygons.PointIsInside(start + (end - start) * 3 / 4, OutlinePointQuadTrees)
						|| !OutlinePolygons.PointIsInside(start + (end - start) / 10, OutlinePointQuadTrees)
						|| !OutlinePolygons.PointIsInside(start + (end - start) * 9 / 10, OutlinePointQuadTrees)
						)
					{
						// an easy way to get the path
						string startEndString = $"start:({startPoint.X}, {startPoint.Y}), end:({endPoint.X}, {endPoint.Y})";
						string outlineString = OutlinePolygons.WriteToString();
						// just some code to set a break point on
						int a = 0;
					}
				}
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

		private void HookUpToEdge(IntPointNode crossingNode, int polyIndex, int pointIndex)
		{
			int count = BoundaryPolygons[polyIndex].Count;
			pointIndex = (pointIndex + count) % count;
			IntPointNode prevPolyPointNode = Waypoints.FindNode(BoundaryPolygons[polyIndex][pointIndex]);
			Waypoints.AddPathLink(crossingNode, prevPolyPointNode);
			IntPointNode nextPolyPointNode = Waypoints.FindNode(BoundaryPolygons[polyIndex][(pointIndex + 1) % count]);
			Waypoints.AddPathLink(crossingNode, nextPolyPointNode);
		}

		private void OptomizePathPoints(Polygon pathThatIsInside)
		{
			var endCount = -1;
			var startCount = pathThatIsInside.Count;
			while (startCount > 0 && startCount != endCount)
			{
				startCount = pathThatIsInside.Count;
				for (int indexA = 0; indexA < pathThatIsInside.Count; indexA++)
				{
					var positionA = pathThatIsInside[indexA];
					var indexB = indexA + 2;
					if (indexB < pathThatIsInside.Count)
					{
						var positionB = pathThatIsInside[indexB];

						var crossings = new List<Tuple<int, int, IntPoint>>(BoundaryPolygons.FindCrossingPoints(positionA, positionB, BoundaryEdgeQuadTrees));
						bool hasOtherThanAB = false;
						foreach (var cross in crossings)
						{
							if (cross.Item3 != positionA
								&& cross.Item3 != positionB)
							{
								hasOtherThanAB = true;
								break;
							}
						}
						
						if (!hasOtherThanAB
							&& BoundaryPolygons.PointIsInside((positionA + positionB) / 2, BoundaryEdgeQuadTrees))
						{
							// remove A+1
							pathThatIsInside.RemoveAt(indexA + 1);
							indexA--;
						}
					}
				}
				endCount = pathThatIsInside.Count;
			}
		}
	}
}