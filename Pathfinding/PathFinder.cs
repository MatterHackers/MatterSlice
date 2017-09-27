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
	using MatterHackers.Agg;
	using MatterHackers.Agg.Image;
	using MatterHackers.Agg.Transform;
	using MatterHackers.Agg.VertexSource;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class PathFinder
	{
		public static Action<PathFinder, Polygon, IntPoint, IntPoint> CalculatedPath = null;
		private static string lastOutlineString = "";
		private static bool saveBadPathToDisk = false;
		private PathingData boundryData;
		public PathingData OutlineData { get; }

		public PathingData PathingData
		{
			get
			{
				if (useOutlineAsBoundry)
				{
					return OutlineData;
				}

				return boundryData;
			}
		}

		private bool useOutlineAsBoundry = false;

		public PathFinder(Polygons inOutlinePolygons, long avoidInset, IntRect? stayInsideBounds = null)
		{
			if (inOutlinePolygons.Count == 0)
			{
				return;
			}

			InsetAmount = avoidInset;

			var outlinePolygons = FixWinding(inOutlinePolygons);
			outlinePolygons = Clipper.CleanPolygons(outlinePolygons, InsetAmount / 60);
			if (stayInsideBounds != null)
			{
				var boundary = stayInsideBounds.Value;
				outlinePolygons.Add(new Polygon()
				{
					new IntPoint(boundary.minX, boundary.minY),
					new IntPoint(boundary.maxX, boundary.minY),
					new IntPoint(boundary.maxX, boundary.maxY),
					new IntPoint(boundary.minX, boundary.maxY),
				});

				outlinePolygons = FixWinding(outlinePolygons);
			}

			var boundaryPolygons = outlinePolygons.Offset(stayInsideBounds == null ? -InsetAmount : -2 * InsetAmount);
			boundaryPolygons = FixWinding(boundaryPolygons);

			// set it to 1/4 the inset amount
			int devisor = 4;
			boundryData = new PathingData(boundaryPolygons, avoidInset / devisor);
			OutlineData = new PathingData(outlinePolygons, avoidInset / devisor);
		}

		public long InsetAmount { get; private set; }

		private long findNodeDist { get { return InsetAmount / 100; } }

		private WayPointsToRemove RemoveBoundryPointList
		{
			get
			{
				if (useOutlineAsBoundry)
				{
					return OutlineData.RemovePointList;
				}

				return boundryData.RemovePointList;
			}
		}

		public bool AllPathSegmentsAreInsideOutlines(Polygon pathThatIsInside, IntPoint startPoint, IntPoint endPoint, bool writeErrors = false)
		{
			// check that this path does not exit the outline
			for (int i = 0; i < pathThatIsInside.Count - 1; i++)
			{
				var start = pathThatIsInside[i];
				var end = pathThatIsInside[i + 1];

				if (start != startPoint
					&& start != endPoint
					&& end != endPoint
					&& end != startPoint)
				{
					if (!ValidPoint(start + (end - start) / 4)
						|| !ValidPoint(start + (end - start) / 2)
						|| !ValidPoint(start + (end - start) * 3 / 4)
						|| !ValidPoint(start + (end - start) / 10)
						|| !ValidPoint(start + (end - start) * 9 / 10)
						|| (start - end).Length() > 1000000)
					{
						// an easy way to get the path
						if (writeErrors)
						{
							WriteErrorForTesting(startPoint, endPoint, (end - start).Length());
						}

						return false;
					}
				}
			}
			     
			return true;
		}

		public bool CreatePathInsideBoundary(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside, bool optomizePath = true)
		{
			var goodPath = CreatePathInsideBoundaryInternal(startPointIn, endPointIn, pathThatIsInside);

			bool agressivePathSolution = true;
			if (agressivePathSolution && !goodPath)
			{
				// could not find a path in the Boundry find one in the outline
				useOutlineAsBoundry = true;
				goodPath = CreatePathInsideBoundaryInternal(startPointIn, endPointIn, pathThatIsInside);
				useOutlineAsBoundry = false;

				if (goodPath)
				{
					// move every segment that can be inside the boundry to be within the boundry
					if (pathThatIsInside.Count > 1)
					{
						IntPoint startPoint = startPointIn;
						for (int i = 0; i < pathThatIsInside.Count-1; i++)
						{
							IntPoint testPoint = pathThatIsInside[i];
							IntPoint endPoint = i < pathThatIsInside.Count-2 ? pathThatIsInside[i+1] : endPointIn;

							IntPoint inPolyPosition;
							if (MovePointInsideBoundary(testPoint, out inPolyPosition))
							{
								useOutlineAsBoundry = true;
								// It moved so test if it is a good point
								if (PathingData.Polygons.FindIntersection(startPoint, inPolyPosition, PathingData.EdgeQuadTrees) != Intersection.Intersect
									&& PathingData.Polygons.FindIntersection(inPolyPosition, endPoint, PathingData.EdgeQuadTrees) != Intersection.Intersect)
								{
									testPoint = inPolyPosition;
									pathThatIsInside[i] = testPoint;
								}

								useOutlineAsBoundry = false;
							}

							startPoint = testPoint;
						}
					}
				}
			}

			// remove any segment that goes to one point and then back to same point (a -> b -> a)
			if (pathThatIsInside.Count > 1)
			{
				IntPoint startPoint = startPointIn;
				for (int i = 0; i < pathThatIsInside.Count - 1; i++)
				{
					IntPoint testPoint = pathThatIsInside[i];
					IntPoint endPoint = i < pathThatIsInside.Count - 2 ? pathThatIsInside[i + 1] : endPointIn;

					if(endPoint == startPoint)
					{
						pathThatIsInside.RemoveAt(i);
						i--;
					}

					startPoint = testPoint;
				}
			}

			if (optomizePath)
			{
				OptomizePathPoints(pathThatIsInside);
			}

			if (saveBadPathToDisk)
			{
				AllPathSegmentsAreInsideOutlines(pathThatIsInside, startPointIn, endPointIn, true);
			}

			CalculatedPath?.Invoke(this, pathThatIsInside, startPointIn, endPointIn);

			return goodPath;
		}

		private bool CreatePathInsideBoundaryInternal(IntPoint startPointIn, IntPoint endPointIn, Polygon pathThatIsInside)
		{
			double z = startPointIn.Z;
			startPointIn.Z = 0;
			endPointIn.Z = 0;
			if (PathingData?.Polygons == null 
				|| PathingData?.Polygons.Count == 0)
			{
				return false;
			}

			// neither needed to be moved
			if (PathingData.Polygons.FindIntersection(startPointIn, endPointIn, PathingData.EdgeQuadTrees) == Intersection.None
				&& PathingData.PointIsInside((startPointIn + endPointIn) / 2))
			{
				return true;
			}

			RemoveBoundryPointList.Dispose();

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

			var crossings = new List<Tuple<int, int, IntPoint>>(PathingData.Polygons.FindCrossingPoints(lastAddedNode.Position, lastToAddNode.Position, PathingData.EdgeQuadTrees));
			crossings.Sort(new PolygonAndPointDirectionSorter(lastAddedNode.Position, lastToAddNode.Position));
			foreach (var crossing in crossings.SkipSame())
			{
				IntPointNode crossingNode = PathingData.Waypoints.FindNode(crossing.Item3, findNodeDist);
				// for every crossing try to connect it up in the waypoint data
				if (crossingNode == null)
				{
					crossingNode = AddTempWayPoint(RemoveBoundryPointList, crossing.Item3);
					// also connect it to the next and prev points on the polygon it came from
					HookUpToEdge(crossingNode, crossing.Item1, crossing.Item2);
				}

				if (lastAddedNode != crossingNode
					&& PathingData.PointIsInside((lastAddedNode.Position + crossingNode.Position) / 2))
				{
					PathingData.Waypoints.AddPathLink(lastAddedNode, crossingNode);
				}
				else if (crossingNode.Links.Count == 0)
				{
					// link it to the edge it is on
					HookUpToEdge(crossingNode, crossing.Item1, crossing.Item2);
				}
				lastAddedNode = crossingNode;
			}

			if (lastAddedNode != lastToAddNode
				&& PathingData.PointIsInside((lastAddedNode.Position + lastToAddNode.Position) / 2))
			{
				// connect the last crossing to the end node
				PathingData.Waypoints.AddPathLink(lastAddedNode, lastToAddNode);
			}

			Path<IntPointNode> path = PathingData.Waypoints.FindPath(startPlanNode, endPlanNode, true);

			foreach (var node in path.Nodes.SkipSamePosition())
			{
				pathThatIsInside.Add(new IntPoint(node.Position, z));
			}

			if (path.Nodes.Length == 0)
			{
				if (saveBadPathToDisk)
				{
					WriteErrorForTesting(startPointIn, endPointIn, 0);
				}
				return false;
			}

			return true;
		}

		public bool MovePointInsideBoundary(IntPoint testPosition, out IntPoint inPolyPosition)
		{
			inPolyPosition = testPosition;
			if (!PathingData.PointIsInside(testPosition))
			{
				Tuple<int, int, IntPoint> endPolyPointPosition = null;
				PathingData.Polygons.MovePointInsideBoundary(testPosition, out endPolyPointPosition, 
					PathingData.EdgeQuadTrees, 
					PathingData.PointQuadTrees,
					PathingData.PointIsInside);

				inPolyPosition = endPolyPointPosition.Item3;
				return true;
			}

			return false;
		}

		public bool PointIsInsideBoundary(IntPoint intPoint)
		{
			return PathingData.PointIsInside(intPoint);
		}

		private IntPointNode AddTempWayPoint(WayPointsToRemove removePointList, IntPoint position)
		{
			var node = PathingData.Waypoints.AddNode(position);
			removePointList.Add(node);
			return node;
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
			PathingData.Polygons.MovePointInsideBoundary(position, out foundPolyPointPosition, PathingData.EdgeQuadTrees, PathingData.PointQuadTrees, PathingData.PointIsInside);
			if (foundPolyPointPosition == null)
			{
				// The point is already inside
				var existingNode = PathingData.Waypoints.FindNode(position, findNodeDist);
				if (existingNode == null)
				{
					waypointAtPosition = AddTempWayPoint(RemoveBoundryPointList, position);
					return waypointAtPosition;
				}
				waypointAtPosition = existingNode;
				return waypointAtPosition;
			}
			else // The point had to be moved inside the polygon
			{
				if (position == foundPolyPointPosition.Item3)
				{
					var existingNode = PathingData.Waypoints.FindNode(position, findNodeDist);
					if (existingNode != null)
					{
						waypointAtPosition = existingNode;
						return waypointAtPosition;
					}
					else
					{
						// get the way point that we need to insert
						waypointAtPosition = AddTempWayPoint(RemoveBoundryPointList, position);
						HookUpToEdge(waypointAtPosition, foundPolyPointPosition.Item1, foundPolyPointPosition.Item2);
						return waypointAtPosition;
					}
				}
				else // the point was outside, hook it up to the nearest edge
				{
					// find the start node if we can
					IntPointNode startNode = PathingData.Waypoints.FindNode(foundPolyPointPosition.Item3, findNodeDist);

					// After that create a temp way point at the current position
					waypointAtPosition = AddTempWayPoint(RemoveBoundryPointList, position);
					if (startNode != null)
					{
						PathingData.Waypoints.AddPathLink(startNode, waypointAtPosition);
					}
					else
					{
						// get the way point that we need to insert
						startNode = AddTempWayPoint(RemoveBoundryPointList, foundPolyPointPosition.Item3);
						HookUpToEdge(startNode, foundPolyPointPosition.Item1, foundPolyPointPosition.Item2);
						PathingData.Waypoints.AddPathLink(startNode, waypointAtPosition);
					}
					return startNode;
				}
			}
		}

		private void HookUpToEdge(IntPointNode crossingNode, int polyIndex, int pointIndex)
		{
			int count = PathingData.Polygons[polyIndex].Count;
			pointIndex = (pointIndex + count) % count;
			IntPointNode prevPolyPointNode = PathingData.Waypoints.FindNode(PathingData.Polygons[polyIndex][pointIndex]);
			PathingData.Waypoints.AddPathLink(crossingNode, prevPolyPointNode);
			IntPointNode nextPolyPointNode = PathingData.Waypoints.FindNode(PathingData.Polygons[polyIndex][(pointIndex + 1) % count]);
			PathingData.Waypoints.AddPathLink(crossingNode, nextPolyPointNode);
		}

		private void OptomizePathPoints(Polygon pathThatIsInside)
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

					var crossings = new List<Tuple<int, int, IntPoint>>(PathingData.Polygons.FindCrossingPoints(startPosition, endPosition, PathingData.EdgeQuadTrees));

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
						&& PathingData.PointIsInside((startPosition + endPosition) / 2))
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

		private bool ValidPoint(IntPoint position)
		{
			Tuple<int, int, IntPoint> movedPosition;
			long movedDist = 0;
			PathingData.Polygons.MovePointInsideBoundary(position, out movedPosition, PathingData.EdgeQuadTrees, PathingData.PointQuadTrees);
			if (movedPosition != null)
			{
				movedDist = (position - movedPosition.Item3).Length();
			}

			if (OutlineData.Polygons.TouchingEdge(position, OutlineData.EdgeQuadTrees)
			|| OutlineData.PointIsInside(position)
			|| movedDist <= 1)
			{
				return true;
			}

			return false;
		}

		private void WriteErrorForTesting(IntPoint startPoint, IntPoint endPoint, long edgeLength)
		{
			var bounds = OutlineData.Polygons.GetBounds();
			long length = (startPoint - endPoint).Length();
			string startEndString = $"start:({startPoint.X}, {startPoint.Y}), end:({endPoint.X}, {endPoint.Y})";
			string outlineString = OutlineData.Polygons.WriteToString();
			// just some code to set a break point on
			string fullPath = Path.GetFullPath("DebugPathFinder.txt");
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
		double unitsPerPixel;

		public WayPointsToRemove RemovePointList { get; }
		public IntPointPathNetwork Waypoints { get; } = new IntPointPathNetwork();
		public List<QuadTree<int>> EdgeQuadTrees { get; }
		public Polygons Polygons { get; }
		public List<QuadTree<int>> PointQuadTrees { get; }
		public ImageBuffer InsideCache { get; private set; }

		internal PathingData(Polygons polygons, double unitsPerPixel)
		{
			this.unitsPerPixel = unitsPerPixel;

			Polygons = polygons;
			EdgeQuadTrees = Polygons.GetEdgeQuadTrees();
			PointQuadTrees = Polygons.GetPointQuadTrees();

			foreach (var polygon in Polygons)
			{
				Waypoints.AddPolygon(polygon);
			}

			RemovePointList = new WayPointsToRemove(Waypoints);

			GenerateIsideCache();
		}

		public bool PointIsInside(IntPoint testPoint)
		{
			bool insideCacheResult = false;
			// translate the test point to the image coordinates
			double x = testPoint.X;
			double y = testPoint.Y;
			polygonsToImageTransform.transform(ref x, ref y);

			if(x >= 0 && x < InsideCache.Width
				&& y >= 0 && y < InsideCache.Height)
			{
				var valueAtPoint = InsideCache.GetPixel((int)x, (int)y);
				insideCacheResult = valueAtPoint.red > 0;
			}

			//if(Polygons.PointIsInside(testPoint, EdgeQuadTrees, PointQuadTrees) != insideCacheResult)
			{
				int a = 0;
			}

			return insideCacheResult;
		}

		Affine polygonsToImageTransform;
		private void GenerateIsideCache()
		{
			var bounds = Polygons.GetBounds();
			var width = (int)(bounds.Width() / unitsPerPixel + .5);
			var height = (int)(bounds.Height() / unitsPerPixel + .5);

			InsideCache = new ImageBuffer(width + 4, height + 4, 8, new blender_gray(1));
			//InsideCache.NewGraphics2D().DrawString("Test", 0, 20, color: RGBA_Bytes.White);

			polygonsToImageTransform = Affine.NewIdentity();
			// move it to 0, 0
			polygonsToImageTransform *= Affine.NewTranslation(-bounds.minX, -bounds.minY);
			// scale to fit cache
			polygonsToImageTransform *= Affine.NewScaling(width / (double)bounds.Width(), height / (double)bounds.Height());
			// and move it in 2 pixels
			polygonsToImageTransform *= Affine.NewTranslation(2, 2);

			InsideCache.NewGraphics2D().Render(new VertexSourceApplyTransform(CreatePathStorage(Polygons), polygonsToImageTransform), RGBA_Bytes.White);
		}

		public static PathStorage CreatePathStorage(List<List<IntPoint>> polygons)
		{
			PathStorage output = new PathStorage();

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
	}
}