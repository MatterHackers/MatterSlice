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
	using System.IO;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class PathFinder
	{
		private static bool simpleHookup = true;
		private static bool storeBoundary = false;
		private WayPointsToRemove removePointList;

		public PathFinder(Polygons outlinePolygons, long avoidInset, bool stayInside = true)
		{
			if (outlinePolygons.Count == 0)
			{
				return;
			}

			OutlinePolygons = Clipper.CleanPolygons(outlinePolygons, avoidInset / 60);
			InsetAmount = avoidInset;
			if (!stayInside)
			{
				var boundary = outlinePolygons.GetBounds();
				boundary.Inflate(avoidInset * 4000);
				OutlinePolygons = new Polygons(outlinePolygons.Count + 1);
				OutlinePolygons.Add(new Polygon()
				{
					new IntPoint(boundary.minX, boundary.minY),
					new IntPoint(boundary.maxX, boundary.minY),
					new IntPoint(boundary.maxX, boundary.maxY),
					new IntPoint(boundary.minX, boundary.maxY),
				});

				foreach (var polygon in outlinePolygons)
				{
					var reversedPolygon = new Polygon();
					for (int i = 0; i < polygon.Count; i++)
					{
						reversedPolygon.Add(polygon[(polygon.Count - 1) - i]);
					}

					OutlinePolygons.Add(reversedPolygon);
				}
			}

			BoundaryPolygons = OutlinePolygons.Offset(stayInside ? -avoidInset : -2 * avoidInset);

			OutlineEdgeQuadTrees = OutlinePolygons.GetEdgeQuadTrees();
			OutlinePointQuadTrees = OutlinePolygons.GetPointQuadTrees();

			BoundaryEdgeQuadTrees = BoundaryPolygons.GetEdgeQuadTrees();
			BoundaryPointQuadTrees = BoundaryPolygons.GetPointQuadTrees();

			if (storeBoundary)
			{
				string pointsString = OutlinePolygons.WriteToString();
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
						Func<int, Polygon, bool> ConsiderPolygon = (polyIndex, poly) =>
						{
							return polyIndex != indexA
								&& poly.GetWindingDirection() > 0;
						};

						// find the closest two points between A and any other polygon
						IntPoint bestAPos = polyA.Center();
						Func<int, IntPoint, bool> ConsiderPoint = (polyIndex, edgeEnd) =>
						{
							if(OutlinePolygons.PointIsInside((bestAPos + edgeEnd) / 2, OutlineEdgeQuadTrees))
							{
								return true;
							}
							return false;
						};

						var bestBPoly = BoundaryPolygons.FindClosestPoint(bestAPos, ConsiderPolygon, ConsiderPoint);
						if (bestBPoly == null)
						{
							// find one that intersects
							bestBPoly = BoundaryPolygons.FindClosestPoint(bestAPos, ConsiderPolygon);
						}
						if (bestBPoly != null)
						{
							bestAPos = polyA.FindClosestPoint(bestBPoly.Item3).Item2;
							var bestBResult = BoundaryPolygons[bestBPoly.Item1].FindClosestPoint(bestAPos, ConsiderPoint);
							IntPoint bestBPos = new IntPoint();
							if (bestBResult != null)
							{
								bestBPos = bestBResult.Item2;
							}
							else
							{ 
								// find one that intersects
								bestBPos = BoundaryPolygons[bestBPoly.Item1].FindClosestPoint(bestAPos).Item2;
							}
							bestAPos = polyA.FindClosestPoint(bestBPos).Item2;
							bestBPos = BoundaryPolygons[bestBPoly.Item1].FindClosestPoint(bestAPos).Item2;

							// hook the polygons up along this connection
							IntPointNode nodeA = Waypoints.FindNode(bestAPos);
							IntPointNode nodeB = Waypoints.FindNode(bestBPos);
							Waypoints.AddPathLink(nodeA, nodeB);
						}
					}
				}
			}
			else // hook up using thin lines code
			{
				// this is done with merge close edges and finding candidates
				// then joining the ends of the merged segments with the closest points
				Polygons thinLines;
				if (OutlinePolygons.FindThinLines(avoidInset * 2, 0, out thinLines))
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
							var closestStart = allPolygons.FindClosestPoint(thinPolygon[0], (polyIndex, poly) => { return polyIndex == thinIndex; });
							var closestEnd = allPolygons.FindClosestPoint(thinPolygon[thinPolygon.Count - 1], (polyIndex, poly) => { return polyIndex == thinIndex; }); // last point
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
		public long InsetAmount { get; private set; }
		public List<QuadTree<int>> OutlineEdgeQuadTrees { get; private set; }
		public List<QuadTree<int>> OutlinePointQuadTrees { get; private set; }
		public Polygons OutlinePolygons { get; private set; }
		public Polygons ThinLinePolygons { get; private set; }
		public IntPointPathNetwork Waypoints { get; private set; } = new IntPointPathNetwork();

		public bool AllPathSegmentsAreInsideOutlines(Polygon pathThatIsInside, IntPoint startPoint, IntPoint endPoint, bool writeErrors = false)
		{
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
					if (writeErrors)
					{
						WriteErrorForTesting(startPoint, endPoint);
					}

					return false;
				}
			}

			return true;
		}

		private void WriteErrorForTesting(IntPoint startPoint, IntPoint endPoint)
		{
			var bounds = OutlinePolygons.GetBounds();
			long length = (startPoint - endPoint).Length();
			string startEndString = $"start:({startPoint.X}, {startPoint.Y}), end:({endPoint.X}, {endPoint.Y})";
			string outlineString = OutlinePolygons.WriteToString();
			// just some code to set a break point on
			string fullPath = Path.GetFullPath("DebugPathFinder.txt");
			if (fullPath.Contains("MatterControl"))
			{
				using (StreamWriter sw = File.AppendText(fullPath))
				{
					sw.WriteLine($"polyPath = \"{outlineString}\";");
					sw.WriteLine($"TestSinglePathIsInside(polyPath, new IntPoint({startPoint.X}, {startPoint.Y}), new IntPoint({endPoint.X}, {endPoint.Y}));");
					sw.WriteLine("");
				}
			}
		}

		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside, bool optomizePath = true)
		{
			double z = startPoint.Z;
			startPoint.Z = 0;
			endPoint.Z = 0;
			if (BoundaryPolygons == null || BoundaryPolygons.Count == 0)
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
			AddWaypointNodeForPosition(startPoint, out startNode, out startPolyPointPosition);

			IntPointNode endNode = null;
			Tuple<int, int, IntPoint> endPolyPointPosition = null;
			AddWaypointNodeForPosition(endPoint, out endNode, out endPolyPointPosition);

			// Check if we already have the solution between start and end
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
			else // the original start is inside so lets make sure we clip from it
			{
				pathThatIsInside.Add(startPoint);
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
				//WriteErrorForTesting(startPoint, endPoint);
				return false;
			}

			if (optomizePath)
			{
				OptomizePathPoints(pathThatIsInside);
			}

			if (startPolyPointPosition == null
				&& pathThatIsInside.Count > 0
				&& pathThatIsInside[0] == startPoint)
			{
				pathThatIsInside.RemoveAt(0);
			}

			//AllPathSegmentsAreInsideOutlines(pathThatIsInside, startPoint, endPoint, true);

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

		private void AddWaypointNodeForPosition(IntPoint position, out IntPointNode waypointNode, out Tuple<int, int, IntPoint> foundPolyPointPosition)
		{
			BoundaryPolygons.MovePointInsideBoundary(position, out foundPolyPointPosition, BoundaryEdgeQuadTrees);
			if (foundPolyPointPosition == null)
			{
				waypointNode = AddTempWayPoint(removePointList, position);
			}
			else
			{
				if (position == foundPolyPointPosition.Item3)
				{
					// it is very close to the edge we did not actually succeed in moving it
					// add it the normal way
					waypointNode = AddTempWayPoint(removePointList, position);
					HookUpToEdge(waypointNode, foundPolyPointPosition.Item1, foundPolyPointPosition.Item2);
					foundPolyPointPosition = null;
				}
				else
				{
					waypointNode = AddTempWayPoint(removePointList, foundPolyPointPosition.Item3);
					HookUpToEdge(waypointNode, foundPolyPointPosition.Item1, foundPolyPointPosition.Item2);
				}
			}
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

		private bool ValidPoint(IntPoint position)
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
	}
}