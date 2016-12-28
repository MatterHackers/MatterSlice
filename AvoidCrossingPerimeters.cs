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
		public Polygons BoundaryPolygons;
		public List<Tuple<int, int, IntPoint>> Crossings = new List<Tuple<int, int, IntPoint>>();

		public AvoidCrossingPerimeters(Polygons boundaryPolygons)
		{
			this.BoundaryPolygons = boundaryPolygons;
		}

		static bool saveDebugData = false;
		bool boundary = false;
		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside)
		{
			pathThatIsInside.Clear();
			if (saveDebugData)
			{
				using (StreamWriter sw = File.AppendText("test.txt"))
				{
					if (boundary)
					{
						string pointsString = BoundaryPolygons.WriteToString();
						sw.WriteLine(pointsString);
					}
					sw.WriteLine(startPoint.ToString() + "  " + endPoint.ToString());
				}
			}

			//Check if we are inside the comb boundaries
			if (!BoundaryPolygons.PointIsInside(startPoint))
			{
				if (!BoundaryPolygons.MovePointInsideBoundary(startPoint, out startPoint))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				pathThatIsInside.Add(startPoint);
			}

			bool addEndpoint = false;
			if (!BoundaryPolygons.PointIsInside(endPoint))
			{
				if (!BoundaryPolygons.MovePointInsideBoundary(endPoint, out endPoint))
				{
					//If we fail to move the point inside the comb boundary we need to retract.
					return false;
				}

				addEndpoint = true;
			}

			// get all the crossings
			Crossings.Clear();
			BoundaryPolygons.FindCrossingPoints(startPoint, endPoint, Crossings);
			Crossings.Sort(new MatterHackers.MatterSlice.PolygonsHelper.DirectionSorter(startPoint, endPoint));

			// remove duplicates
			for (int i = 0; i < Crossings.Count - 1; i++)
			{
				while (i + 1 < Crossings.Count
					&& (Crossings[i].Item3 - Crossings[i + 1].Item3).LengthSquared() < 4)
				{
					Crossings.RemoveAt(i);
				}
			}

			if(Crossings.Count == 1)
			{
				Crossings.Clear();
			}

			// remove the start and end point if they are in the list
			if (Crossings.Count > 0
				&& pathThatIsInside.Count > 0
				&& Crossings[0].Item3 == pathThatIsInside[0])
			{
				pathThatIsInside.RemoveAt(0);
			}

			if (Crossings.Count > 0 
				&& addEndpoint
				&& Crossings[Crossings.Count-1].Item3 == endPoint)
			{
				addEndpoint = false;
			}

			// if crossing are 0 
			//We're not crossing any boundaries. So skip the comb generation.
			if (Crossings.Count == 0
				&& !addEndpoint 
				&& pathThatIsInside.Count == 0)
			{
				//Only skip if we didn't move the start and end point.
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
			foreach(Tuple<int, int> crossingPair in CrossingIterator(Crossings))
			{
				Tuple<int, int, IntPoint> crossingStart = Crossings[crossingPair.Item1];
				Tuple<int, int, IntPoint> crossingEnd = Crossings[crossingPair.Item2];

				var network = new IntPointPathNetwork(BoundaryPolygons[crossingStart.Item1]);

				IntPoint startLinkA = BoundaryPolygons[crossingStart.Item1][crossingStart.Item2];
				IntPoint startLinkB = BoundaryPolygons[crossingStart.Item1][(crossingStart.Item2 + 1) % BoundaryPolygons[crossingStart.Item1].Count];

				IntPoint endLinkA = BoundaryPolygons[crossingEnd.Item1][crossingEnd.Item2];
				IntPoint endLinkB = BoundaryPolygons[crossingEnd.Item1][(crossingEnd.Item2 + 1) % BoundaryPolygons[crossingEnd.Item1].Count];

				Path<IntPointNode> path = network.FindPath(crossingStart.Item3, startLinkA, startLinkB, crossingEnd.Item3, endLinkA, endLinkB);

				// the start intersection for this crossing set
				pathThatIsInside.Add(crossingStart.Item3);

				if (crossingStart.Item3 != crossingEnd.Item3)
				{
					foreach (var node in path.nodes)
					{
						pathThatIsInside.Add(node.Position);
					}
				}

				// add the last intersection for this crossing set
				pathThatIsInside.Add(crossingEnd.Item3);
			}

			// add the end point if needed
			if (addEndpoint)
			{
				pathThatIsInside.Add(endPoint);
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
			return BoundaryPolygons.MovePointInsideBoundary(testPosition, out inPolyPosition);
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