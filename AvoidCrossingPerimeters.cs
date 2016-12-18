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
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class AvoidCrossingPerimeters
	{
		public Polygons BoundaryPolygons;

		private int[] indexOfMaxX;
		private int[] indexOfMinX;
		private long[] maxXPosition;
		private long[] minXPosition;

		public AvoidCrossingPerimeters(Polygons boundaryPolygons)
		{
			this.BoundaryPolygons = boundaryPolygons;
			minXPosition = new long[boundaryPolygons.Count];
			maxXPosition = new long[boundaryPolygons.Count];
			indexOfMinX = new int[boundaryPolygons.Count];
			indexOfMaxX = new int[boundaryPolygons.Count];
		}

		static bool saveDebugData = false;
		bool boundary = false;
		public bool CreatePathInsideBoundary(IntPoint startPoint, IntPoint endPoint, Polygon pathThatIsInside)
		{
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
			//FindCrossingPoints(startPoint, endPoint, CrossingPoints);

			// if crossing are 0 
			//We're not crossing any boundaries. So skip the comb generation.
			if (!addEndpoint && pathThatIsInside.Count == 0)
			{
				//Only skip if we didn't move the start and end point.
				return true;
			}

			// else

			// sort them in the order of the start end direction

			// for each pair of crossings

			// add a move to the start of the crossing
			// try to go CW and CWW take the path that is the shortest and add it to the list

			Polygon pointList = new Polygon();
			// Now walk trough the crossings, for every boundary we cross, find the initial cross point and the exit point.
			// Then add all the points in between to the pointList and continue with the next boundary we will cross,
			// until there are no more boundaries to cross.
			// This gives a path from the start to finish curved around the holes that it encounters.
			pointList.Add(endPoint);

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

			foreach (IntPoint point in pointList)
			{
				pathThatIsInside.Add(point);
			}

			return true;
		}

		public bool PointIsInsideBoundary(IntPoint intPoint)
		{
			return BoundaryPolygons.PointIsInside(intPoint);
		}

		public bool MovePointInsideBoundary(IntPoint testPosition, out IntPoint inPolyPosition)
		{
			return BoundaryPolygons.MovePointInsideBoundary(testPosition, out inPolyPosition);
		}

		/// <summary>
		/// The start and end points must already be in the bounding polygon
		/// </summary>
		/// <param name="startPoint"></param>
		/// <param name="endPoint"></param>
		/// <param name="crossingPoints"></param>
		public void FindCrossingPoints(IntPoint startPoint, IntPoint endPoint, Polygon crossingPoints)
		{
			crossingPoints.Clear();

			foreach (var boundaryPolygon in BoundaryPolygons)
			{
				for (int pointIndex = 0; pointIndex < boundaryPolygon.Count; pointIndex++)
				{
				}
			}
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