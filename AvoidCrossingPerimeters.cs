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

using MSClipperLib;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using System;
	using System.IO;
	using Pathfinding;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class AvoidCrossingPerimeters
	{
		public IntPointPathNetwork Waypoints { get; private set; } = new IntPointPathNetwork();

		public Polygons BoundaryPolygons;
		public List<Tuple<int, int, IntPoint>> Crossings = new List<Tuple<int, int, IntPoint>>();

		public AvoidCrossingPerimeters(Polygons boundaryPolygons)
		{
			this.BoundaryPolygons = boundaryPolygons;

			foreach (var polygon in BoundaryPolygons)
			{
				for (int i = 0; i < polygon.Count; i++)
				{
					if (polygon.IsVertexConcave(i))
					{
						Waypoints.AddNode(polygon[i], 1);
					}
				}
			}

			for (int nodeIndexA = 0; nodeIndexA < Waypoints.Nodes.Count; nodeIndexA++)
			{
				for (int nodeIndexB = nodeIndexA + 1; nodeIndexB < Waypoints.Nodes.Count; nodeIndexB++)
				{
					if (BoundaryPolygons.PointIsInside((Waypoints.Nodes[nodeIndexA].Position + Waypoints.Nodes[nodeIndexB].Position) / 2))
					{
						if (BoundaryPolygons.FindIntersection(Waypoints.Nodes[nodeIndexA].Position, Waypoints.Nodes[nodeIndexB].Position) != Intersection.Intersect)
						{
							// they don't intersect (they might have touching end points but we don't care
							// hook this up
							Waypoints.AddPathLink(Waypoints.Nodes[nodeIndexA], Waypoints.Nodes[nodeIndexB]);
						}
					}
				}
			}
		}

		static bool storeBoundary = false;
		public bool CreatePathInsideBoundary(IntPoint inStartPoint, IntPoint inEndPoint, Polygon pathThatIsInside)
		{
			using (WayPointsToRemove removePointList = new WayPointsToRemove(Waypoints))
			{
				pathThatIsInside.Clear();
				if (storeBoundary)
				{
					string pointsString = BoundaryPolygons.WriteToString();
				}

				IntPointNode startNode = null;
				Tuple<int, int, IntPoint> startPolyPointPosition = null;
				//Check if we are inside the boundaries
				if (!BoundaryPolygons.PointIsInside(inStartPoint))
				{
					if (!BoundaryPolygons.MovePointInsideBoundary(inStartPoint, out startPolyPointPosition))
					{
						//If we fail to move the point inside the comb boundary we need to retract.
						return false;
					}

					startNode = AddWayPoint(removePointList, startPolyPointPosition);
				}

				if(startNode == null)
				{
					startNode = Waypoints.AddNode(inStartPoint);
					startPolyPointPosition = new Tuple<int, int, IntPoint>(0, 0, inStartPoint);
				}

				IntPointNode endNode = null;
				Tuple<int, int, IntPoint> endPolyPointPosition = null;
				if (!BoundaryPolygons.PointIsInside(inEndPoint))
				{
					if (!BoundaryPolygons.MovePointInsideBoundary(inEndPoint, out endPolyPointPosition))
					{
						//If we fail to move the point inside the comb boundary we need to retract.
						return false;
					}

					endNode = AddWayPoint(removePointList, endPolyPointPosition);
				}

				if(endNode == null)
				{
					endNode = Waypoints.AddNode(inEndPoint);
					endPolyPointPosition = new Tuple<int, int, IntPoint>(0, 0, inEndPoint);
				}

				// connect start and end if required

				// get all the crossings
				Crossings.Clear();
				BoundaryPolygons.FindCrossingPoints(startPolyPointPosition.Item3, endPolyPointPosition.Item3, Crossings);
				Crossings.Sort(new MatterHackers.MatterSlice.PolygonsHelper.DirectionSorter(startPolyPointPosition.Item3, endPolyPointPosition.Item3));

				// remove duplicates (they can happen when crossing vertices)
				for (int i = 0; i < Crossings.Count - 1; i++)
				{
					while (i + 1 < Crossings.Count
						&& (Crossings[i].Item3 - Crossings[i + 1].Item3).LengthSquared() < 4)
					{
						Crossings.RemoveAt(i);
					}
				}

				// start and end are inside and we didn't cross anything
				if (Crossings.Count == 0
					&& startNode == null
					&& endNode == null)
				{
					return true;
				}

				// else

				// add a move to the start of the crossing
				// try to go CW and CWW take the path that is the shortest and add it to the list

				// Now walk trough the crossings, for every boundary we cross, find the initial cross point and the exit point.
				// Then add all the points in between to the pointList and continue with the next boundary we will cross,
				// until there are no more boundaries to cross.
				// This gives a path from the start to finish curved around the holes that it encounters.
				// for each pair of crossings
				foreach (var crossing in Crossings)
				{
					AddWayPoint(removePointList, crossing);
				}


				Path<IntPointNode> path = Waypoints.FindPath(startNode, endNode, true);

				foreach (var node in path.nodes)
				{
					pathThatIsInside.Add(node.Position);
				}
#if false
			// Optimize the pointList, skip each point we could already reach by connecting directly to the next point.
			for (int startIndex = 0; startIndex < pointList.Count - 2; startIndex++)
			{
				IntPoint startPosition = pointList[startIndex];
				// make sure there is at least one point between the start and the end to optimize
				if (pointList.Count > startIndex + 2)
				{
					for (int checkIndex = pointList.Count - 1; checkIndex > startIndex + 1; checkIndex--)
					{
						IntPoint checkPosition = pointList[checkIndex];
						if (!DoesLineCrossBoundary(startPosition, checkPosition))
						{
							// Remove all the points from startIndex+1 to checkIndex-1, inclusive.
							for (int i = startIndex + 1; i < checkIndex; i++)
							{
								pointList.RemoveAt(startIndex + 1);
							}

							// we removed all the points up to start so we are done with the inner loop
							break;
						}
					}
				}
			}
#endif

				return true;
			}
		}

		private IntPointNode AddWayPoint(WayPointsToRemove removePointList, Tuple<int, int, IntPoint> startPolyPointPosition)
		{
			int polyCount = BoundaryPolygons[startPolyPointPosition.Item1].Count;
			var startNode = Waypoints.AddNode(startPolyPointPosition.Item3,
				BoundaryPolygons[startPolyPointPosition.Item1][startPolyPointPosition.Item2],
				BoundaryPolygons[startPolyPointPosition.Item1][(startPolyPointPosition.Item2 + 1) % polyCount]);
			removePointList.Add(startNode);

			return startNode;
		}

		private IEnumerable<Tuple<int, int>> CrossingIterator(List<Tuple<int, int, IntPoint>> crossings)
		{
			int startIndex = -1;
			for(int i=0; i<crossings.Count; i++)
			{
				// check if we are looking for a new set
				if(startIndex == -1)
				{
					// this is the start of the new set
					startIndex = i;
				}
				else // looking for the end of a set
				{
					// found the end of the same polygon
					if(crossings[startIndex].Item1 == crossings[i].Item1)
					{
						// if the midpoint of this segment is inside the polygon
						if (PointIsInsideBoundary((crossings[startIndex].Item3 + crossings[i].Item3) / 2))
						{
							// we set the start to the end and keep looking
							startIndex = i;
						}
						else
						{
							// return the set
							yield return new Tuple<int, int>(startIndex, i);
							// we are now looking for a new set
							startIndex = i;
						}
					}
					else // didn't find an end, consider it a new start
					{
						startIndex = i;
					}
				}

			}
		}

		public bool PointIsInsideBoundary(IntPoint intPoint)
		{
			return BoundaryPolygons.PointIsInside(intPoint);
		}

		public bool MovePointInsideBoundary(IntPoint testPosition, out IntPoint inPolyPosition)
		{
			Tuple<int, int, IntPoint> endPolyPointPosition = null;
			if (!BoundaryPolygons.PointIsInside(testPosition))
			{
				if (!BoundaryPolygons.MovePointInsideBoundary(testPosition, out endPolyPointPosition))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					inPolyPosition = new IntPoint();
					return false;
				}
			}

			inPolyPosition = endPolyPointPosition.Item3;
			return true;
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
	}
}