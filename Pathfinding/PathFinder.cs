/*
Copyright (c) 2015, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg.VertexSource;
using MatterHackers.QuadTree;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.Pathfinding
{
	public class PathFinder
	{
		public bool IsSimpleConvex { get; set; } = false;
		public static Action<PathFinder, Polygon, IntPoint, IntPoint> CalculatedPath = null;
		private static string lastOutlineString = "";
		private static bool saveBadPathToDisk = false;
		public string Name { get; private set; }

		public PathFinder(Polygons outlinePolygons,
			long avoidInset,
			IntRect? stayInsideBounds = null,
			bool useInsideCache = true,
			string name = "")
		{
			this.Name = name;
			if (outlinePolygons.Count == 0)
			{
				return;
			}

			// Check if the outline is convex and no holes, if it is, don't create pathing data we can move anywhere in this object
			if (outlinePolygons.Count == 1)
			{
				var currentPolygon = outlinePolygons[0];
				int pointCount = currentPolygon.Count;
				double negativeTurns = 0;
				double positiveTurns = 0;
				for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
				{
					int prevIndex = (pointIndex + pointCount - 1) % pointCount;
					int nextIndex = (pointIndex + 1) % pointCount;
					IntPoint prevPoint = currentPolygon[prevIndex];
					IntPoint currentPoint = currentPolygon[pointIndex];
					IntPoint nextPoint = currentPolygon[nextIndex];

					double turnAmount = currentPoint.GetTurnAmount(prevPoint, nextPoint);

					if (turnAmount < 0)
					{
						negativeTurns += turnAmount;
					}
					else
					{
						positiveTurns += turnAmount;
					}
				}

				if (positiveTurns == 0 || negativeTurns == 0)
				{
					// all the turns are the same way this thing is convex
					IsSimpleConvex = true;
				}
			}

			InsetAmount = avoidInset;

			var outsidePolygons = FixWinding(outlinePolygons);
			outsidePolygons = Clipper.CleanPolygons(outsidePolygons, InsetAmount / 60);
			if (stayInsideBounds != null)
			{
				var boundary = stayInsideBounds.Value;
				outsidePolygons.Add(new Polygon()
				{
					new IntPoint(boundary.minX, boundary.minY),
					new IntPoint(boundary.maxX, boundary.minY),
					new IntPoint(boundary.maxX, boundary.maxY),
					new IntPoint(boundary.minX, boundary.maxY),
				});

				outsidePolygons = FixWinding(outsidePolygons);
			}

			// set it to 1/4 the inset amount
			int devisor = 4;
			OutlineData = new PathingData(outsidePolygons, Math.Abs(avoidInset / devisor), useInsideCache);
		}

		public long InsetAmount { get; private set; }

		public PathingData OutlineData { get; private set; }

		private long FindNodeDist { get { return InsetAmount / 100; } }

		public bool AllPathSegmentsAreInsideOutlines(Polygon pathThatIsInside, IntPoint startPoint, IntPoint endPoint, bool writeErrors = false, int layerIndex = -1)
		{
			if (this.OutlineData == null)
			{
				return true;
			}

			// if (outlineData.Polygons.Count > 1) throw new Exception();
			// check that this path does not exit the outline
			for (int i = 0; i < pathThatIsInside.Count - 1; i++)
			{
				var start = pathThatIsInside[i];
				var end = pathThatIsInside[i + 1];

				if (start != startPoint
					&& start != endPoint
					&& end != endPoint
					&& end != startPoint
					&& (end - start).Length() > 0)
				{
					if (!ValidPoint(OutlineData, start + (end - start) / 4)
						|| !ValidPoint(OutlineData, start + (end - start) / 2)
						|| !ValidPoint(OutlineData, start + (end - start) * 3 / 4)
						|| !ValidPoint(OutlineData, start + (end - start) / 10)
						|| !ValidPoint(OutlineData, start + (end - start) * 9 / 10)
						|| (start - end).Length() > 1000000)
					{
						// an easy way to get the path
						if (writeErrors)
						{
							WriteErrorForTesting(layerIndex, startPoint, endPoint, (end - start).Length());
						}

						return false;
					}
				}
			}

			return true;
		}

		public bool CreatePathInsideBoundary(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside, bool optimizePath = true, int layerIndex = -1)
		{
			if (IsSimpleConvex)
			{
				return true;
			}

			var goodPath = CalculatePath(startPointIn, endPointIn, pathThatIsInside, layerIndex);
			if (goodPath)
			{
				if (optimizePath)
				{
					OptimizePathPoints(pathThatIsInside);
				}

				if (pathThatIsInside.Count == 0)
				{
					if ((startPointIn - endPointIn).Length() > InsetAmount * 3)
					{
						pathThatIsInside.Add(startPointIn);
						pathThatIsInside.Add(endPointIn);
					}
					else
					{
						return true;
					}
				}

				CutIntoSmallSegments(pathThatIsInside);

				MovePointsInsideIfPossible(pathThatIsInside);

				var cleanPath = pathThatIsInside.CleanClosedPolygon(InsetAmount / 2);
				pathThatIsInside.Clear();
				pathThatIsInside.AddRange(cleanPath);

				// remove any segment that goes to one point and then back to same point (a -> b -> a)
				RemoveUTurnSegments(startPointIn, endPointIn, pathThatIsInside);
			}

			// Remove0LengthSegments(startPointIn, endPointIn, pathThatIsInside);

			if (saveBadPathToDisk)
			{
				AllPathSegmentsAreInsideOutlines(pathThatIsInside, startPointIn, endPointIn, true, layerIndex);
			}

			CalculatedPath?.Invoke(this, pathThatIsInside, startPointIn, endPointIn);

			return goodPath;
		}

		public bool MovePointInsideBoundary(IntPoint testPosition, out IntPoint inPolyPosition)
		{
			inPolyPosition = testPosition;
			if (!PointIsInsideBoundary(testPosition))
			{
				(int polyIndex, int pointIndex, IntPoint position) endPolyPointPosition = (-1, -1, default(IntPoint));
				OutlineData.Polygons.MovePointInsideBoundary(testPosition,
					out endPolyPointPosition,
					OutlineData.EdgeQuadTrees,
					OutlineData.PointKDTrees,
					OutlineData.PointIsInside);

				if (endPolyPointPosition.pointIndex != -1)
				{
					inPolyPosition = endPolyPointPosition.position;
					return true;
				}
			}

			return false;
		}

		public bool PointIsInsideBoundary(IntPoint intPoint)
		{
			return OutlineData.PointIsInside(intPoint) == QTPolygonsExtensions.InsideState.Inside;
		}

		private static void Remove0LengthSegments(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside)
		{
			if (pathThatIsInside.Count > 1)
			{
				IntPoint startPoint = startPointIn;
				for (int i = 0; i < pathThatIsInside.Count - 1; i++)
				{
					IntPoint endPoint = pathThatIsInside[i];

					if (endPoint == startPoint)
					{
						// and remove the end point (it is the same as the start point
						pathThatIsInside.RemoveAt(i);
						// don't advance past the start point
						i--;
					}
					else
					{
						startPoint = endPoint;
					}
				}
			}
		}

		private static void RemoveUTurnSegments(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside)
		{
			if (pathThatIsInside.Count > 1)
			{
				IntPoint startPoint = startPointIn;
				for (int i = 0; i < pathThatIsInside.Count - 1; i++)
				{
					IntPoint testPoint = pathThatIsInside[i];
					IntPoint endPoint = i < pathThatIsInside.Count - 2 ? pathThatIsInside[i + 1] : endPointIn;

					if (endPoint == startPoint)
					{
						// remove the test point
						pathThatIsInside.RemoveAt(i);
						// and remove the end point (it is the same as the start point
						pathThatIsInside.RemoveAt(i);
						// don't advance past the start point
						i--;
					}
					else
					{
						startPoint = testPoint;
					}
				}
			}
		}

		private IntPointNode AddTempWayPoint(WayPointsToRemove removePointList, IntPoint position)
		{
			var node = OutlineData.Waypoints.AddNode(position);
			removePointList.Add(node);
			return node;
		}

		private bool CalculatePath(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside, int layerIndex)
		{
			double z = startPointIn.Z;
			startPointIn.Z = 0;
			endPointIn.Z = 0;
			if (OutlineData?.Polygons == null
				|| OutlineData?.Polygons.Count == 0)
			{
				return false;
			}

			// neither needed to be moved
			if (OutlineData.Polygons.FindIntersection(startPointIn, endPointIn, OutlineData.EdgeQuadTrees) == Intersection.None
				&& OutlineData.PointIsInside((startPointIn + endPointIn) / 2) == QTPolygonsExtensions.InsideState.Inside)
			{
				return true;
			}

			OutlineData.RemovePointList.Dispose();

			pathThatIsInside.Clear();

			// Check if we are inside the boundaries
			var lastAddedNode = GetWayPointInside(startPointIn, out IntPointNode startPlanNode);

			var lastToAddNode = GetWayPointInside(endPointIn, out IntPointNode endPlanNode);

			long startToEndDistanceSqrd = (endPointIn - startPointIn).LengthSquared();
			long moveStartInDistanceSqrd = (startPlanNode.Position - lastAddedNode.Position).LengthSquared();
			long moveEndInDistanceSqrd = (endPlanNode.Position - lastToAddNode.Position).LengthSquared();
			// if we move both points less than the distance of this segment
			if (startToEndDistanceSqrd < moveStartInDistanceSqrd
				&& startToEndDistanceSqrd < moveEndInDistanceSqrd)
			{
				// then go ahead and say it is a good path
				return true;
			}

			var crossings = new List<(int polyIndex, int pointIndex, IntPoint position)>(OutlineData.Polygons.FindCrossingPoints(lastAddedNode.Position, lastToAddNode.Position, OutlineData.EdgeQuadTrees));
			if (crossings.Count == 0)
			{
				return true;
			}

			crossings.Sort(new PolygonAndPointDirectionSorter(lastAddedNode.Position, lastToAddNode.Position));
			foreach (var (polyIndex, pointIndex, position) in crossings.SkipSame())
			{
				IntPointNode crossingNode = OutlineData.Waypoints.FindNode(position, FindNodeDist);
				// for every crossing try to connect it up in the waypoint data
				if (crossingNode == null)
				{
					crossingNode = AddTempWayPoint(OutlineData.RemovePointList, position);
					// also connect it to the next and prev points on the polygon it came from
					HookUpToEdge(crossingNode, polyIndex, pointIndex);
				}

				if (lastAddedNode != crossingNode
					&& (SegmentIsAllInside(lastAddedNode, crossingNode) || lastAddedNode.Links.Count == 0))
				{
					OutlineData.Waypoints.AddPathLink(lastAddedNode, crossingNode);
				}
				else if (crossingNode.Links.Count == 0)
				{
					// link it to the edge it is on
					HookUpToEdge(crossingNode, polyIndex, pointIndex);
				}

				lastAddedNode = crossingNode;
			}

			if (lastAddedNode != lastToAddNode
				&& (OutlineData.PointIsInside((lastAddedNode.Position + lastToAddNode.Position) / 2) == QTPolygonsExtensions.InsideState.Inside
					|| lastToAddNode.Links.Count == 0))
			{
				// connect the last crossing to the end node
				OutlineData.Waypoints.AddPathLink(lastAddedNode, lastToAddNode);
			}

			Path<IntPointNode> path = OutlineData.Waypoints.FindPath(startPlanNode, endPlanNode, true);

			foreach (var node in path.Nodes.SkipSamePosition())
			{
				pathThatIsInside.Add(new IntPoint(node.Position, z));
			}

			if (path.Nodes.Length == 0 && saveBadPathToDisk)
			{
				WriteErrorForTesting(layerIndex, startPointIn, endPointIn, 0);
				return false;
			}

			return true;
		}

		private void CutIntoSmallSegments(Polygon pathThatIsInside)
		{
			var cutLength = InsetAmount;
			// Make every segment be a maximum of cutLength long
			if (OutlineData.DistanceFromOutside != null
				&& pathThatIsInside.Count >= 2
				&& InsetAmount > 0)
			{
				var startIndex = PointIsInsideBoundary(pathThatIsInside[0]) ? 0 : 1;
				IntPoint startPoint = pathThatIsInside[startIndex];

				var endIndex = PointIsInsideBoundary(pathThatIsInside[pathThatIsInside.Count - 1]) ? pathThatIsInside.Count - 1 : pathThatIsInside.Count - 2;
				var cutLengthSquared = cutLength * cutLength;
				for (int i = startIndex; i <= pathThatIsInside.Count - 2; i++)
				{
					startPoint = pathThatIsInside[i];
					IntPoint endPoint = pathThatIsInside[i + 1];

					if ((endPoint - startPoint).LengthSquared() > cutLengthSquared)
					{
						int steps = (int)((endPoint - startPoint).Length() / cutLength);
						for (int cut = 1; cut < steps; cut++)
						{
							IntPoint newPosition = startPoint + (endPoint - startPoint) * cut / steps;
							pathThatIsInside.Insert(i + 1, newPosition);
							i++;
						}
					}
				}
			}
		}

		private Polygons FixWinding(Polygons polygonsToPathAround)
		{
			polygonsToPathAround = Clipper.CleanPolygons(polygonsToPathAround);
			Polygon boundsPolygon = new Polygon();
			IntRect bounds = Clipper.GetBounds(polygonsToPathAround);
			bounds.minX -= 10;
			bounds.maxY += 10;
			bounds.maxX += 10;
			bounds.minY -= 10;

			boundsPolygon.Add(new IntPoint(bounds.minX, bounds.minY));
			boundsPolygon.Add(new IntPoint(bounds.maxX, bounds.minY));
			boundsPolygon.Add(new IntPoint(bounds.maxX, bounds.maxY));
			boundsPolygon.Add(new IntPoint(bounds.minX, bounds.maxY));

			Clipper clipper = new Clipper();

			clipper.AddPaths(polygonsToPathAround, PolyType.ptSubject, true);
			clipper.AddPath(boundsPolygon, PolyType.ptClip, true);

			PolyTree intersectionResult = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, intersectionResult);

			Polygons outputPolygons = Clipper.ClosedPathsFromPolyTree(intersectionResult);

			return outputPolygons;
		}

		private IntPointNode GetWayPointInside(IntPoint position, out IntPointNode waypointAtPosition)
		{
			waypointAtPosition = null;
			OutlineData.Polygons.MovePointInsideBoundary(position, out (int polyIndex, int pointIndex, IntPoint position) foundPolyPointPosition, OutlineData.EdgeQuadTrees, OutlineData.PointKDTrees, OutlineData.PointIsInside);
			if (foundPolyPointPosition.polyIndex == -1)
			{
				// The point is already inside
				var existingNode = OutlineData.Waypoints.FindNode(position, FindNodeDist);
				if (existingNode == null)
				{
					waypointAtPosition = AddTempWayPoint(OutlineData.RemovePointList, position);
					return waypointAtPosition;
				}

				waypointAtPosition = existingNode;
				return waypointAtPosition;
			}
			else // The point had to be moved inside the polygon
			{
				if (position == foundPolyPointPosition.position)
				{
					var existingNode = OutlineData.Waypoints.FindNode(position, FindNodeDist);
					if (existingNode != null)
					{
						waypointAtPosition = existingNode;
						return waypointAtPosition;
					}
					else
					{
						// get the way point that we need to insert
						waypointAtPosition = AddTempWayPoint(OutlineData.RemovePointList, position);
						HookUpToEdge(waypointAtPosition, foundPolyPointPosition.polyIndex, foundPolyPointPosition.pointIndex);
						return waypointAtPosition;
					}
				}
				else // the point was outside, hook it up to the nearest edge
				{
					// find the start node if we can
					IntPointNode startNode = OutlineData.Waypoints.FindNode(foundPolyPointPosition.position, FindNodeDist);

					// After that create a temp way point at the current position
					waypointAtPosition = AddTempWayPoint(OutlineData.RemovePointList, position);
					if (startNode != null)
					{
						OutlineData.Waypoints.AddPathLink(startNode, waypointAtPosition);
					}
					else
					{
						// get the way point that we need to insert
						startNode = AddTempWayPoint(OutlineData.RemovePointList, foundPolyPointPosition.position);
						HookUpToEdge(startNode, foundPolyPointPosition.polyIndex, foundPolyPointPosition.pointIndex);
						OutlineData.Waypoints.AddPathLink(startNode, waypointAtPosition);
					}

					return startNode;
				}
			}
		}

		private void HookUpToEdge(IntPointNode crossingNode, int polyIndex, int pointIndex)
		{
			int count = OutlineData.Polygons[polyIndex].Count;
			if (count > 0)
			{
				pointIndex = (pointIndex + count) % count;
				IntPointNode prevPolyPointNode = OutlineData.Waypoints.FindNode(OutlineData.Polygons[polyIndex][pointIndex]);
				OutlineData.Waypoints.AddPathLink(crossingNode, prevPolyPointNode);
				IntPointNode nextPolyPointNode = OutlineData.Waypoints.FindNode(OutlineData.Polygons[polyIndex][(pointIndex + 1) % count]);
				OutlineData.Waypoints.AddPathLink(crossingNode, nextPolyPointNode);
			}
		}

		private void MovePointsInsideIfPossible(Polygon pathThatIsInside)
		{
			if (OutlineData.DistanceFromOutside != null)
			{
				// move every segment that can be inside the boundary to be within the boundary
				if (pathThatIsInside.Count > 1 && InsetAmount > 0)
				{
					for (int i = 0; i < pathThatIsInside.Count - 1; i++)
					{
						IntPoint testPoint = pathThatIsInside[i];

						if (OutlineData.MovePointAwayFromEdge(testPoint, InsetAmount, out IntPoint inPolyPosition))
						{
							// It moved so test if it is a good point
							// if (OutlineData.Polygons.FindIntersection(startPoint, inPolyPosition, OutlineData.EdgeQuadTrees) != Intersection.Intersect
							// && OutlineData.Polygons.FindIntersection(inPolyPosition, endPoint, OutlineData.EdgeQuadTrees) != Intersection.Intersect)
							{
								testPoint = inPolyPosition;
								pathThatIsInside[i] = testPoint;
							}
						}
					}
				}
			}
		}

		private void OptimizePathPoints(Polygon pathThatIsInside)
		{
			for (int startIndex = 0; startIndex < pathThatIsInside.Count - 2; startIndex++)
			{
				var startPosition = pathThatIsInside[startIndex];
				for (int endIndex = pathThatIsInside.Count - 1; endIndex > startIndex + 1; endIndex--)
				{
					var endPosition = pathThatIsInside[endIndex];

					var crossings = new List<(int polyIndex, int pointIndex, IntPoint position)>(OutlineData.Polygons.FindCrossingPoints(startPosition, endPosition, OutlineData.EdgeQuadTrees));

					bool isCrossingEdge = false;
					foreach (var (polyIndex, pointIndex, position) in crossings)
					{
						if (position != startPosition
							&& position != endPosition)
						{
							isCrossingEdge = true;
							break;
						}
					}

					if (!isCrossingEdge
						&& OutlineData.PointIsInside((startPosition + endPosition) / 2) == QTPolygonsExtensions.InsideState.Inside)
					{
						// remove A+1 - B-1
						for (int removeIndex = endIndex - 1; removeIndex > startIndex; removeIndex--)
						{
							pathThatIsInside.RemoveAt(removeIndex);
						}

						endIndex = pathThatIsInside.Count - 1;
					}
				}
			}
		}

		private bool SegmentIsAllInside(IntPointNode lastAddedNode, IntPointNode crossingNode)
		{
			if (OutlineData.DistanceFromOutside == null)
			{
				// check just the center point
				return OutlineData.PointIsInside((lastAddedNode.Position + crossingNode.Position) / 2) == QTPolygonsExtensions.InsideState.Inside;
			}
			else
			{
				// check many points along the line
				return OutlineData.PointIsInside((lastAddedNode.Position + crossingNode.Position) / 2) == QTPolygonsExtensions.InsideState.Inside
					&& OutlineData.PointIsInside(lastAddedNode.Position + (crossingNode.Position - lastAddedNode.Position) / 4) == QTPolygonsExtensions.InsideState.Inside
					&& OutlineData.PointIsInside(lastAddedNode.Position + (crossingNode.Position - lastAddedNode.Position) * 3 / 4) == QTPolygonsExtensions.InsideState.Inside;
			}
		}

		private bool ValidPoint(PathingData outlineData, IntPoint position)
		{
			long movedDist = 0;
			OutlineData.Polygons.MovePointInsideBoundary(position, out (int polyIndex, int pointIndex, IntPoint position) movedPosition, OutlineData.EdgeQuadTrees, OutlineData.PointKDTrees, OutlineData.PointIsInside);
			if (movedPosition.polyIndex != -1)
			{
				movedDist = (position - movedPosition.position).Length();
			}

			if (outlineData.Polygons.TouchingEdge(position, outlineData.EdgeQuadTrees)
			|| outlineData.PointIsInside(position) != QTPolygonsExtensions.InsideState.Outside
			|| movedDist <= 200)
			{
				return true;
			}

			return false;
		}

		private void WriteErrorForTesting(int layerIndex, IntPoint startPoint, IntPoint endPoint, long edgeLength)
		{
			long length = (startPoint - endPoint).Length();
			string outlineString = OutlineData.Polygons.WriteToString();
			// just some code to set a break point on
			string fullPath = Path.GetFullPath("DebugPathFinder.txt");
			// fullPath = "C:/Development/MCCentral/MatterControl/bin/Debug/DebugPathFinder.txt";
			if (fullPath.Contains("MatterControl"))
			{
				using (StreamWriter sw = File.AppendText(fullPath))
				{
					if (lastOutlineString != outlineString)
					{
						sw.WriteLine("");
						sw.WriteLine($"polyPath = \"{outlineString}\";");
						lastOutlineString = outlineString;
					}

					sw.WriteLine($"// layerIndex = {layerIndex}");
					sw.WriteLine($"// Length of this segment (start->end) {length}. Length of bad edge {edgeLength}");
					sw.WriteLine($"// startOverride = new MSIntPoint({startPoint.X}, {startPoint.Y}); endOverride = new MSIntPoint({endPoint.X}, {endPoint.Y});");
					sw.WriteLine($"TestSinglePathIsInside(polyPath, new IntPoint({startPoint.X}, {startPoint.Y}), new IntPoint({endPoint.X}, {endPoint.Y}));");
				}
			}
		}
	}
}