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

using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.Pathfinding
{
	using System;
	using System.IO;
	using MatterHackers.Agg;
	using MatterHackers.Agg.Image;
	using MatterHackers.Agg.Transform;
	using MatterHackers.Agg.VertexSource;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;
	using static System.Math;
	using MatterHackers.Agg.ImageProcessing;

	public class PathFinder
	{
		public static Action<PathFinder, Polygon, IntPoint, IntPoint> CalculatedPath = null;
		private static string lastOutlineString = "";
		private static bool saveBadPathToDisk = false;

		public PathFinder(Polygons outlinePolygons, long avoidInset, IntRect? stayInsideBounds = null, bool useInsideCache = true)
		{
			if (outlinePolygons.Count == 0)
			{
				return;
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

			var insidePolygons = outsidePolygons.Offset(stayInsideBounds == null ? -InsetAmount : -2 * InsetAmount);
			insidePolygons = FixWinding(insidePolygons);

			// set it to 1/4 the inset amount
			int devisor = 4;
			OutlineData = new PathingData(outsidePolygons, avoidInset / devisor, useInsideCache);
		}

		public long InsetAmount { get; private set; }
		public PathingData OutlineData { get; private set; }

		private long findNodeDist { get { return InsetAmount / 100; } }

		public bool AllPathSegmentsAreInsideOutlines(Polygon pathThatIsInside, IntPoint startPoint, IntPoint endPoint, bool writeErrors = false, int layerIndex = -1)
		{
			if(this.OutlineData == null)
			{
				return true;
			}

			//if (outlineData.Polygons.Count > 1) throw new Exception();
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
			var goodPath = CreatePathInsideBoundaryInternal(startPointIn, endPointIn, pathThatIsInside, layerIndex);
			if (goodPath)
			{
				MovePointsInsideIfPossible(startPointIn, endPointIn, pathThatIsInside);
			}

			// remove any segment that goes to one point and then back to same point (a -> b -> a)
			RemoveUTurnSegments(startPointIn, endPointIn, pathThatIsInside);

			if (optimizePath)
			{
				OptimizePathPoints(pathThatIsInside);
			}

			//Remove0LengthSegments(startPointIn, endPointIn, pathThatIsInside);

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
				Tuple<int, int, IntPoint> endPolyPointPosition = null;
				OutlineData.Polygons.MovePointInsideBoundary(testPosition, out endPolyPointPosition,
					OutlineData.EdgeQuadTrees,
					OutlineData.PointQuadTrees,
					OutlineData.PointIsInside);

				if (endPolyPointPosition != null)
				{
					inPolyPosition = endPolyPointPosition.Item3;
					return true;
				}
			}

			return false;
		}

		public bool PointIsInsideBoundary(IntPoint intPoint)
		{
			return OutlineData.PointIsInside(intPoint) == QTPolygonsExtensions.InsideState.Inside;
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

		private IntPointNode AddTempWayPoint(WayPointsToRemove removePointList, IntPoint position)
		{
			var node = OutlineData.Waypoints.AddNode(position);
			removePointList.Add(node);
			return node;
		}

		private bool CreatePathInsideBoundaryInternal(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside, int layerIndex)
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

			//Check if we are inside the boundaries
			IntPointNode startPlanNode = null;
			var lastAddedNode = GetWayPointInside(startPointIn, out startPlanNode);

			IntPointNode endPlanNode = null;
			var lastToAddNode = GetWayPointInside(endPointIn, out endPlanNode);

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

			var crossings = new List<Tuple<int, int, IntPoint>>(OutlineData.Polygons.FindCrossingPoints(lastAddedNode.Position, lastToAddNode.Position, OutlineData.EdgeQuadTrees));
			if (crossings.Count == 0)
			{
				return true;
			}
			crossings.Sort(new PolygonAndPointDirectionSorter(lastAddedNode.Position, lastToAddNode.Position));
			foreach (var crossing in crossings.SkipSame())
			{
				IntPointNode crossingNode = OutlineData.Waypoints.FindNode(crossing.Item3, findNodeDist);
				// for every crossing try to connect it up in the waypoint data
				if (crossingNode == null)
				{
					crossingNode = AddTempWayPoint(OutlineData.RemovePointList, crossing.Item3);
					// also connect it to the next and prev points on the polygon it came from
					HookUpToEdge(crossingNode, crossing.Item1, crossing.Item2);
				}

				if (lastAddedNode != crossingNode
					&& (SegmentIsAllInside(lastAddedNode, crossingNode) || lastAddedNode.Links.Count == 0))
				{
					OutlineData.Waypoints.AddPathLink(lastAddedNode, crossingNode);
				}
				else if (crossingNode.Links.Count == 0)
				{
					// link it to the edge it is on
					HookUpToEdge(crossingNode, crossing.Item1, crossing.Item2);
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

		private bool SegmentIsAllInside(IntPointNode lastAddedNode, IntPointNode crossingNode)
		{
			if (OutlineData.InsideCache == null)
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
			Tuple<int, int, IntPoint> foundPolyPointPosition;
			waypointAtPosition = null;
			OutlineData.Polygons.MovePointInsideBoundary(position, out foundPolyPointPosition, OutlineData.EdgeQuadTrees, OutlineData.PointQuadTrees, OutlineData.PointIsInside);
			if (foundPolyPointPosition == null)
			{
				// The point is already inside
				var existingNode = OutlineData.Waypoints.FindNode(position, findNodeDist);
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
				if (position == foundPolyPointPosition.Item3)
				{
					var existingNode = OutlineData.Waypoints.FindNode(position, findNodeDist);
					if (existingNode != null)
					{
						waypointAtPosition = existingNode;
						return waypointAtPosition;
					}
					else
					{
						// get the way point that we need to insert
						waypointAtPosition = AddTempWayPoint(OutlineData.RemovePointList, position);
						HookUpToEdge(waypointAtPosition, foundPolyPointPosition.Item1, foundPolyPointPosition.Item2);
						return waypointAtPosition;
					}
				}
				else // the point was outside, hook it up to the nearest edge
				{
					// find the start node if we can
					IntPointNode startNode = OutlineData.Waypoints.FindNode(foundPolyPointPosition.Item3, findNodeDist);

					// After that create a temp way point at the current position
					waypointAtPosition = AddTempWayPoint(OutlineData.RemovePointList, position);
					if (startNode != null)
					{
						OutlineData.Waypoints.AddPathLink(startNode, waypointAtPosition);
					}
					else
					{
						// get the way point that we need to insert
						startNode = AddTempWayPoint(OutlineData.RemovePointList, foundPolyPointPosition.Item3);
						HookUpToEdge(startNode, foundPolyPointPosition.Item1, foundPolyPointPosition.Item2);
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

		private void MovePointsInsideIfPossible(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside)
		{
			// move every segment that can be inside the boundry to be within the boundry
			if (pathThatIsInside.Count > 1)
			{
				IntPoint startPoint = startPointIn;
				for (int i = 0; i < pathThatIsInside.Count - 1; i++)
				{
					IntPoint testPoint = pathThatIsInside[i];
					IntPoint endPoint = i < pathThatIsInside.Count - 2 ? pathThatIsInside[i + 1] : endPointIn;

					IntPoint inPolyPosition;
					if(OutlineData.MovePointAwayFromEdge(testPoint, InsetAmount, out inPolyPosition))
					{
						// It moved so test if it is a good point
						//if (OutlineData.Polygons.FindIntersection(startPoint, inPolyPosition, OutlineData.EdgeQuadTrees) != Intersection.Intersect
							//&& OutlineData.Polygons.FindIntersection(inPolyPosition, endPoint, OutlineData.EdgeQuadTrees) != Intersection.Intersect)
						{
							testPoint = inPolyPosition;
							pathThatIsInside[i] = testPoint;
						}
					}

					startPoint = testPoint;
				}
			}
		}

		private void OptimizePathPoints(Polygon pathThatIsInside)
		{
			for (int startIndex = 0; startIndex < pathThatIsInside.Count - 2; startIndex++)
			{
				var startPosition = pathThatIsInside[startIndex];
				if (startPosition.X < -10000)
				{
					int a = 0;
				}
				for (int endIndex = pathThatIsInside.Count - 1; endIndex > startIndex + 1; endIndex--)
				{
					var endPosition = pathThatIsInside[endIndex];

					var crossings = new List<Tuple<int, int, IntPoint>>(OutlineData.Polygons.FindCrossingPoints(startPosition, endPosition, OutlineData.EdgeQuadTrees));

					bool isCrossingEdge = false;
					foreach (var cross in crossings)
					{
						if (cross.Item3 != startPosition
							&& cross.Item3 != endPosition)
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

		private bool ValidPoint(PathingData outlineData, IntPoint position)
		{
			Tuple<int, int, IntPoint> movedPosition;
			long movedDist = 0;
			OutlineData.Polygons.MovePointInsideBoundary(position, out movedPosition, OutlineData.EdgeQuadTrees, OutlineData.PointQuadTrees, OutlineData.PointIsInside);
			if (movedPosition != null)
			{
				movedDist = (position - movedPosition.Item3).Length();
			}

			if (outlineData.Polygons.TouchingEdge(position, outlineData.EdgeQuadTrees)
			|| outlineData.PointIsInside(position) != QTPolygonsExtensions.InsideState.Outside
			|| movedDist <= 1)
			{
				return true;
			}

			return false;
		}

		private void WriteErrorForTesting(int layerIndex, IntPoint startPoint, IntPoint endPoint, long edgeLength)
		{
			var bounds = OutlineData.Polygons.GetBounds();
			long length = (startPoint - endPoint).Length();
			string startEndString = $"start:({startPoint.X}, {startPoint.Y}), end:({endPoint.X}, {endPoint.Y})";
			string outlineString = OutlineData.Polygons.WriteToString();
			// just some code to set a break point on
			string fullPath = Path.GetFullPath("DebugPathFinder.txt");
			//fullPath = "C:/Development/MCCentral/MatterControl/bin/Debug/DebugPathFinder.txt";
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

	/// <summary>
	/// This is to hold all the data that lets us switch between Boundry and Outline pathing.
	/// </summary>
	public class PathingData
	{
		private Affine polygonsToImageTransform;
		private double unitsPerPixel;
		private bool usingPathingCache;
		IntRect polygonBounds;

		internal PathingData(Polygons polygons, double unitsPerPixel, bool usingPathingCache)
		{
			this.usingPathingCache = usingPathingCache;

			Polygons = polygons;
			polygonBounds = Polygons.GetBounds();
			SetGoodUnitsPerPixel(unitsPerPixel);

			EdgeQuadTrees = Polygons.GetEdgeQuadTrees();
			PointQuadTrees = Polygons.GetPointQuadTrees();

			foreach (var polygon in Polygons)
			{
				Waypoints.AddPolygon(polygon);
			}

			RemovePointList = new WayPointsToRemove(Waypoints);

			GenerateIsideCache();
		}

		private void SetGoodUnitsPerPixel(double unitsPerPixel)
		{
			unitsPerPixel = Max(unitsPerPixel, 1);
			if (polygonBounds.Width() / unitsPerPixel > 1024)
			{
				unitsPerPixel = Max(1, polygonBounds.Width() / 1024);
			}
			if (polygonBounds.Height() / unitsPerPixel > 1024)
			{
				unitsPerPixel = Max(1, polygonBounds.Height() / 1024);
			}
			if (polygonBounds.Width() / unitsPerPixel < 32)
			{
				unitsPerPixel = polygonBounds.Width() / 32;
			}
			if (polygonBounds.Height() / unitsPerPixel > 1024)
			{
				unitsPerPixel = polygonBounds.Height() / 32;
			}

			this.unitsPerPixel = Max(1, unitsPerPixel);
		}

		public List<QuadTree<int>> EdgeQuadTrees { get; }
		public ImageBuffer InsideCache { get; private set; }
		public ImageBuffer InsetMap { get; private set; }
		public List<QuadTree<int>> PointQuadTrees { get; }
		public Polygons Polygons { get; }
		public WayPointsToRemove RemovePointList { get; }
		public IntPointPathNetwork Waypoints { get; } = new IntPointPathNetwork();

		public static VertexStorage CreatePathStorage(List<List<IntPoint>> polygons)
		{
			VertexStorage output = new VertexStorage();

			foreach (List<IntPoint> polygon in polygons)
			{
				bool first = true;
				foreach (IntPoint point in polygon)
				{
					if (first)
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandMoveTo);
						first = false;
					}
					else
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandLineTo);
					}
				}

				output.ClosePolygon();
			}

			return output;
		}

		public bool MovePointAwayFromEdge(IntPoint testPoint, long distance, out IntPoint result)
		{
			int distanceInPixels = Max(1, (int)Round(distance / unitsPerPixel));
			result = testPoint;
			bool movedPoint = false;

			for (int i = 0; i < distanceInPixels; i++)
			{
				// check each direction to see if we can increase our InsetMap value
				double x = result.X;
				double y = result.Y;
				polygonsToImageTransform.transform(ref x, ref y);
				int xi = (int)Round(x);
				int yi = (int)Round(y);

				int current = GetInsetMapValue(xi, yi);

				movedPoint |= CheckInsetPixel(current, xi - 1, yi + 0, ref result);
				movedPoint |= CheckInsetPixel(current, xi - 1, yi - 1, ref result);
				movedPoint |= CheckInsetPixel(current, xi + 0, yi - 1, ref result);
				movedPoint |= CheckInsetPixel(current, xi + 1, yi - 1, ref result);
				movedPoint |= CheckInsetPixel(current, xi + 1, yi + 0, ref result);
				movedPoint |= CheckInsetPixel(current, xi + 1, yi + 1, ref result);
				movedPoint |= CheckInsetPixel(current, xi + 0, yi + 1, ref result);
				movedPoint |= CheckInsetPixel(current, xi - 1, yi + 1, ref result);
			}

			return movedPoint;
		}

		private bool CheckInsetPixel(int current, int xi, int yi, ref IntPoint result)
		{
			int value = GetInsetMapValue(xi, yi);
			if (value > current)
			{
				double x = xi;
				double y = yi;
				polygonsToImageTransform.inverse_transform(ref x, ref y);
				result = new IntPoint(Round(x), Round(y));
				return true;
			}

			return false;
		}

		private int GetInsetMapValue(int xi, int yi)
		{
			if (xi >= 0 && xi < InsetMap.Width
				&& yi >= 0 && yi < InsetMap.Height)
			{
				var buffer = InsetMap.GetBuffer();
				var offset = InsetMap.GetBufferOffsetXY(xi, yi);
				return buffer[offset];
			}

			return 0;
		}

		public QTPolygonsExtensions.InsideState PointIsInside(IntPoint testPoint)
		{
			if (!usingPathingCache)
			{
				if (Polygons.PointIsInside(testPoint, EdgeQuadTrees, PointQuadTrees))
				{
					return QTPolygonsExtensions.InsideState.Inside;
				}

				return QTPolygonsExtensions.InsideState.Outside;
			}

			// translate the test point to the image coordinates
			double xd = testPoint.X;
			double yd = testPoint.Y;
			polygonsToImageTransform.transform(ref xd, ref yd);
			int xi = (int)Round(xd);
			int yi = (int)Round(yd);

			int pixelSum = 0;
			for(int offsetX = -1; offsetX <= 1; offsetX++)
			{
				for (int offsetY = -1; offsetY <= 1; offsetY++)
				{
					int x = xi + offsetX;
					int y = yi + offsetY;
					if (x >= 0 && x < InsideCache.Width
						&& y >= 0 && y < InsideCache.Height)
					{
						pixelSum += InsideCache.GetBuffer()[InsideCache.GetBufferOffsetXY(x, y)];
					}
				}
			}

			if (pixelSum == 0)
			{
				return QTPolygonsExtensions.InsideState.Outside;
			}
			else if (pixelSum / 9 == 255)
			{
				return QTPolygonsExtensions.InsideState.Inside;
			}

			return QTPolygonsExtensions.InsideState.Unknown;
		}

		private void GenerateIsideCache()
		{
			int width = (int)Round(polygonBounds.Width() / unitsPerPixel);
			int height = (int)Round(polygonBounds.Height() / unitsPerPixel);

			InsideCache = new ImageBuffer(width + 4, height + 4, 8, new blender_gray(1));

			// Set the transform to image space
			polygonsToImageTransform = Affine.NewIdentity();
			// move it to 0, 0
			polygonsToImageTransform *= Affine.NewTranslation(-polygonBounds.minX, -polygonBounds.minY);
			// scale to fit cache
			polygonsToImageTransform *= Affine.NewScaling(width / (double)polygonBounds.Width(), height / (double)polygonBounds.Height());
			// and move it in 2 pixels
			polygonsToImageTransform *= Affine.NewTranslation(2, 2);

			// and render the polygon to the image
			InsideCache.NewGraphics2D().Render(new VertexSourceApplyTransform(CreatePathStorage(Polygons), polygonsToImageTransform), Color.White);

			// Now lets create an image that we can use to move points inside the outline
			// First create an image that is fully set on all color values of the original image
			InsetMap = new ImageBuffer(InsideCache);
			InsetMap.DoThreshold(1);
			// Then erode the image multiple times to get the a map of desired insets
			int count = 8;
			int step = 255/count;
			ImageBuffer last = InsetMap;
			for (int i = 0; i < count; i++)
			{
				var erode = new ImageBuffer(last);
				erode.DoErode3x3Binary(255);
				Paint(InsetMap, erode, (i + 1) * step);
				last = erode;
			}
		}

		private void Paint(ImageBuffer dest, ImageBuffer source, int level)
		{
			int height = source.Height;
			int width = source.Width;
			int sourceStrideInBytes = source.StrideInBytes();
			int destStrideInBytes = dest.StrideInBytes();
			byte[] sourceBuffer = source.GetBuffer();
			byte[] destBuffer = dest.GetBuffer();

			for (int y = 1; y < height - 1; y++)
			{
				int offset = source.GetBufferOffsetY(y);
				for (int x = 1; x < width - 1; x++)
				{
					if (destBuffer[offset] == 255 // the dest is white
						&& sourceBuffer[offset] == 0) // the dest is cleared
					{
						destBuffer[offset] = (byte)level;
					}
					offset++;
				}
			}
		}
	}
}