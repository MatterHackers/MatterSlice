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
		public long InsetAmount { get; private set; }

		public PathFinder(Polygons outlinePolygons, long avoidInset, bool stayInside = true)
		{
			InsetAmount = avoidInset;
			if (!stayInside)
			{
				var reversedPolygons = new Polygons(outlinePolygons.Count);
				//make a copy with reversed winding
				foreach (var polygon in outlinePolygons)
				{
					var reversedPolygon = new Polygon();
					reversedPolygons.Add(reversedPolygon);
					for (int i = 0; i < polygon.Count; i++)
					{
						reversedPolygon.Add(polygon[((polygon.Count - 1) - i) % polygon.Count]);
					}
				}
				outlinePolygons = reversedPolygons;
				//outlinePolygons = new Polygons(outlinePolygons);
				// now and a rect round all of them
				var bounds = outlinePolygons.GetBounds();
				IntPoint lowerLeft = new IntPoint(bounds.minX - avoidInset * 8, bounds.minY - avoidInset * 8);
				IntPoint upperRight = new IntPoint(bounds.maxX + avoidInset * 8, bounds.maxY + avoidInset * 8);
				outlinePolygons.Add(new Polygon()
				{
					new IntPoint(lowerLeft),
					new IntPoint(lowerLeft.X, upperRight.Y),
					new IntPoint(upperRight),
					new IntPoint(upperRight.X, lowerLeft.Y),
				});
			}

			OutlinePolygons = outlinePolygons;
			OutlineEdgeQuadTrees = OutlinePolygons.GetEdgeQuadTrees();
			OutlinePointQuadTrees = OutlinePolygons.GetPointQuadTrees();
			BoundaryPolygons = outlinePolygons.Offset(stayInside ? -avoidInset : -2*avoidInset);
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
					var polyA = BoundaryPolygons[indexA];
					if (polyA.GetWindingDirection() > 0)
					{
						// find the closest two point between A and any other polygon
						IntPoint bestAPos = polyA.Center();
						var bestBPoly = BoundaryPolygons.FindClosestPoint(bestAPos, indexA);
						bestAPos = polyA.FindClosestPoint(bestBPoly.Item3).Item2;
						var bestBPos = BoundaryPolygons[bestBPoly.Item1].FindClosestPoint(bestAPos).Item2;
						bestAPos = polyA.FindClosestPoint(bestBPos).Item2;
						bestBPos = BoundaryPolygons[bestBPoly.Item1].FindClosestPoint(bestAPos).Item2;

						// hook the polygons up along this connection
						IntPointNode nodeA = Waypoints.FindNode(bestAPos);
						IntPointNode nodeB = Waypoints.FindNode(bestBPos);
						Waypoints.AddPathLink(nodeA, nodeB);
					}
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
					for (int thinIndex = 0; thinIndex < thinLines.Count; thinIndex++)
					{
						var thinPolygon = thinLines[thinIndex];
						if (thinPolygon.Count > 1)
						{
							Waypoints.AddPolygon(thinPolygon, false);
						}
					}

					Polygons allPolygons = new Polygons(thinLines);
					allPolygons.AddRange(BoundaryPolygons);
					for (int thinIndex = 0; thinIndex < thinLines.Count; thinIndex++)
					{
						var thinPolygon = thinLines[thinIndex];
						if (thinPolygon.Count > 1)
						{
							// now hook up the start and end of this polygon to the existing way points
							var closestStart = allPolygons.FindClosestPoint(thinPolygon[0], thinIndex);
							var closestEnd = allPolygons.FindClosestPoint(thinPolygon[thinPolygon.Count - 1], thinIndex); // last point
							if (OutlinePolygons.PointIsInside((closestStart.Item3 + closestEnd.Item3) / 2, OutlineEdgeQuadTrees))
							{
								IntPointNode nodeA = Waypoints.FindNode(closestStart.Item3);
								IntPointNode nodeB = Waypoints.FindNode(closestEnd.Item3);
								if (nodeA == null || nodeB == null)
								{
									int stop = 0; // debug this. It should not happen
								}
								else
								{
									Waypoints.AddPathLink(nodeA, nodeB);
								}
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
				if (startPoint == startPolyPointPosition.Item3)
				{
					// it is very close to the edge we did not actually succeed in moving it
					// add it the normal way
					startNode = AddTempWayPoint(removePointList, startPoint);
					startPolyPointPosition = null;
				}
				else
				{ 
					startNode = AddTempWayPoint(removePointList, startPolyPointPosition.Item3);
					HookUpToEdge(startNode, startPolyPointPosition.Item1, startPolyPointPosition.Item2);
				}
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
				if (endPoint == endPolyPointPosition.Item3)
				{
					// it is very close to the edge we did not actually succeed in moving it
					// add it the normal way
					endNode = AddTempWayPoint(removePointList, endPoint);
					endPolyPointPosition = null;
				}
				else
				{
					endNode = AddTempWayPoint(removePointList, endPolyPointPosition.Item3);
					HookUpToEdge(endNode, endPolyPointPosition.Item1, endPolyPointPosition.Item2);
				}
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

			//AllPathSegmentsAreInsideOutlines(pathThatIsInside, startPoint, endPoint);

			if (path.Nodes.Length == 0)
			{
				return false;
			}

			if (optomizePath)
			{
				OptomizePathPoints(pathThatIsInside);
			}

			//AllPathSegmentsAreInsideOutlines(pathThatIsInside, startPoint, endPoint);

			return true;
		}

		public bool AllPathSegmentsAreInsideOutlines(Polygon pathThatIsInside, IntPoint startPoint, IntPoint endPoint)
		{
			#if !DEBUG
			return true;
			#endif
			// check that this path does not exit the outline
			for (int i = 0; i < pathThatIsInside.Count - 1; i++)
			{
				var start = pathThatIsInside[i];
				var end = pathThatIsInside[i + 1];

				if (!ValidPoint(start + (end - start) / 4)
					|| !ValidPoint(start + (end - start) / 2)
					|| !ValidPoint(start + (end - start) * 3 / 4)
					|| !ValidPoint(start + (end - start) / 10)
					|| !ValidPoint(start + (end - start) * 9 / 10)
					)
				{
					// an easy way to get the path
					string startEndString = $"start:({startPoint.X}, {startPoint.Y}), end:({endPoint.X}, {endPoint.Y})";
					string outlineString = OutlinePolygons.WriteToString();
					// just some code to set a break point on
					return false;
				}
			}

			return true;
		}

		bool ValidPoint(IntPoint position)
		{
			Tuple<int, int, IntPoint> movedPosition;
			long movedDist = 0;
			BoundaryPolygons.MovePointInsideBoundary(position, out movedPosition, BoundaryEdgeQuadTrees);
			if (movedPosition != null)
			{
				movedDist = (position - movedPosition.Item3).Length();
			}

			if (OutlinePolygons.TouchingEdge(position, OutlineEdgeQuadTrees)
			|| OutlinePolygons.PointIsInside(position, OutlineEdgeQuadTrees)
			|| movedDist <= 1)
			{
				return true;
			}

			return false;
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