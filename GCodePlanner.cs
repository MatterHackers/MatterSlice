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

using MatterSlice.ClipperLib;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	//The GCodePlanner class stores multiple moves that are planned.
	// It facilitates the avoidCrossingPerimeters to keep the head inside the print.
	// It also keeps track of the print time estimate for this planning so speed adjustments can be made for the minimum-layer-time.
	public class GCodePlanner
	{
		private bool alwaysRetract;

		private int currentExtruderIndex;

		private double extraTime;

		private int extrudeSpeedFactor;

		private bool forceRetraction;

		private GCodeExport gcode = new GCodeExport();

		private IntPoint lastPosition;

		private AvoidCrossingPerimeters outerPerimetersToAvoidCrossing;

		private List<GCodePath> paths = new List<GCodePath>();

		private int retractionMinimumDistance_um;

		private double totalPrintTime;

		private GCodePathConfig travelConfig = new GCodePathConfig();

		private int travelSpeedFactor;

		public GCodePlanner(GCodeExport gcode, int travelSpeed, int retractionMinimumDistance_um)
		{
			this.gcode = gcode;
			travelConfig = new GCodePathConfig(travelSpeed, 0, "travel");

			lastPosition = gcode.GetPositionXY();
			outerPerimetersToAvoidCrossing = null;
			extrudeSpeedFactor = 100;
			travelSpeedFactor = 100;
			extraTime = 0.0;
			totalPrintTime = 0.0;
			forceRetraction = false;
			alwaysRetract = false;
			currentExtruderIndex = gcode.GetExtruderIndex();
			this.retractionMinimumDistance_um = retractionMinimumDistance_um;
		}

		public void ForceMinimumLayerTime(double minTime, int minimumPrintingSpeed)
		{
			IntPoint lastPosition = gcode.GetPositionXY();
			double travelTime = 0.0;
			double extrudeTime = 0.0;
			for (int n = 0; n < paths.Count; n++)
			{
				GCodePath path = paths[n];
				for (int pointIndex = 0; pointIndex < path.points.Count; pointIndex++)
				{
					IntPoint currentPosition = path.points[pointIndex];
					double thisTime = (lastPosition - currentPosition).LengthMm() / (double)(path.config.speed);
					if (path.config.lineWidth != 0)
					{
						extrudeTime += thisTime;
					}
					else
					{
						travelTime += thisTime;
					}

					lastPosition = currentPosition;
				}
			}

			double totalTime = extrudeTime + travelTime;
			if (totalTime < minTime && extrudeTime > 0.0)
			{
				double minExtrudeTime = minTime - travelTime;
				if (minExtrudeTime < 1)
				{
					minExtrudeTime = 1;
				}

				double factor = extrudeTime / minExtrudeTime;
				for (int n = 0; n < paths.Count; n++)
				{
					GCodePath path = paths[n];
					if (path.config.lineWidth == 0)
					{
						continue;
					}

					int speed = (int)(path.config.speed * factor);
					if (speed < minimumPrintingSpeed)
					{
						factor = (double)(minimumPrintingSpeed) / (double)(path.config.speed);
					}
				}

				//Only slow down with the minimum time if that will be slower then a factor already set. First layer slowdown also sets the speed factor.
				if (factor * 100 < getExtrudeSpeedFactor())
				{
					SetExtrudeSpeedFactor((int)(factor * 100));
				}
				else
				{
					factor = getExtrudeSpeedFactor() / 100.0;
				}

				if (minTime - (extrudeTime / factor) - travelTime > 0.1)
				{
					//TODO: Use up this extra time (circle around the print?)
					this.extraTime = minTime - (extrudeTime / factor) - travelTime;
				}
				this.totalPrintTime = (extrudeTime / factor) + travelTime;
			}
			else
			{
				this.totalPrintTime = totalTime;
			}
		}

		public void ForceRetract()
		{
			forceRetraction = true;
		}

		public int getExtruder()
		{
			return currentExtruderIndex;
		}

		public int getExtrudeSpeedFactor()
		{
			return this.extrudeSpeedFactor;
		}

		public Polygons GetPathsWithOverlapsRemoved(Polygon perimeter, int overlapMergeAmount_um)
		{
			// make a copy that has every point duplicatiod (so that we have them as segments).
			Polygon polySegments = new Polygon(perimeter.Count * 2);
			for (int i = 0; i < perimeter.Count-1; i++)
			{
				IntPoint point = perimeter[i];
				IntPoint nextPoint = perimeter[i+1];

				polySegments.Add(point);
				polySegments.Add(nextPoint);
			}

			// now walk every segment and check if there is another segment that is similar enough to merge them together
			for (int firstSegmentIndex = 0; firstSegmentIndex < polySegments.Count; firstSegmentIndex += 2)
			{
				for (int checkSegmentIndex = firstSegmentIndex + 2; checkSegmentIndex < polySegments.Count; checkSegmentIndex += 2)
				{
					// The first point of start and the last point of check (the path will be coming back on itself).
					long startDelta = (polySegments[firstSegmentIndex] - polySegments[checkSegmentIndex+1]).Length();
					// if the segmets are similar enough
					if (startDelta < overlapMergeAmount_um)
					{
						// The last point of start and the first point of check (the path will be coming back on itself).
						long endDelta = (polySegments[firstSegmentIndex + 1] - polySegments[checkSegmentIndex]).Length();
						if (endDelta < overlapMergeAmount_um)
						{
							// move the first segments points to the average of the merge positions
							polySegments[firstSegmentIndex] = (polySegments[firstSegmentIndex] + polySegments[checkSegmentIndex + 1]) / 2; // the start
							polySegments[firstSegmentIndex + 1] = (polySegments[firstSegmentIndex + 1] + polySegments[checkSegmentIndex]) / 2; // the end

							// remove the second segment
							polySegments.RemoveRange(checkSegmentIndex, 2);
							// We only expect to find one match for each segment, so move on to the next segment
							break;
						}
					}
				}
			}

			// go through the polySegmets and create a new polygon for every connected set of segmets
			Polygons separatedPolygons = new Polygons();
			Polygon currentPolygon = new Polygon();
			separatedPolygons.Add(currentPolygon);
			// put in the first point
			for (int segmentIndex = 0; segmentIndex < polySegments.Count; segmentIndex += 2)
			{
				// add the start point
				currentPolygon.Add(polySegments[segmentIndex]);

				// if the next segment is not connected to this one
				if (segmentIndex < polySegments.Count - 2
					&& polySegments[segmentIndex + 1] != polySegments[segmentIndex + 2])
				{
					// add the end point
					currentPolygon.Add(polySegments[segmentIndex + 1]);

					// create a new polygon
					currentPolygon = new Polygon();
					separatedPolygons.Add(currentPolygon);
				}
			}

			// add the end point
			currentPolygon.Add(polySegments[polySegments.Count - 1]);

			return separatedPolygons;
		}

		public int getTravelSpeedFactor()
		{
			return this.travelSpeedFactor;
		}

		public void MoveInsideTheOuterPerimeter(int distance)
		{
			if (outerPerimetersToAvoidCrossing == null || outerPerimetersToAvoidCrossing.PointIsInsideBoundary(lastPosition))
			{
				return;
			}

			IntPoint p = lastPosition;
			if (outerPerimetersToAvoidCrossing.MovePointInsideBoundary(ref p, distance))
			{
				//Move inside again, so we move out of tight 90deg corners
				outerPerimetersToAvoidCrossing.MovePointInsideBoundary(ref p, distance);
				if (outerPerimetersToAvoidCrossing.PointIsInsideBoundary(p))
				{
					WriteTravel(p);
					//Make sure the that any retraction happens after this move, not before it by starting a new move path.
					ForceNewPathStart();
				}
			}
		}

		public void SetAlwaysRetract(bool alwaysRetract)
		{
			this.alwaysRetract = alwaysRetract;
		}

		public bool SetExtruder(int extruder)
		{
			if (extruder == currentExtruderIndex)
			{
				return false;
			}

			currentExtruderIndex = extruder;
			return true;
		}

		public void SetExtrudeSpeedFactor(int speedFactor)
		{
			if (speedFactor < 1) speedFactor = 1;
			this.extrudeSpeedFactor = speedFactor;
		}

		public void SetOuterPerimetersToAvoidCrossing(Polygons polygons)
		{
			if (polygons != null)
			{
				outerPerimetersToAvoidCrossing = new AvoidCrossingPerimeters(polygons);
			}
			else
			{
				outerPerimetersToAvoidCrossing = null;
			}
		}

		public void SetTravelSpeedFactor(int speedFactor)
		{
			if (speedFactor < 1) speedFactor = 1;
			this.travelSpeedFactor = speedFactor;
		}

		public void WriteExtrusionMove(IntPoint destination, GCodePathConfig config)
		{
			GetLatestPathWithConfig(config).points.Add(destination);
			lastPosition = destination;
		}

		public void WriteGCode(bool liftHeadIfNeeded, int layerThickness)
		{
			GCodePathConfig lastConfig = null;
			int extruderIndex = gcode.GetExtruderIndex();

			for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
			{
				GCodePath path = paths[pathIndex];
				if (extruderIndex != path.extruderIndex)
				{
					extruderIndex = path.extruderIndex;
					gcode.SwitchExtruder(extruderIndex);
				}
				else if (path.Retract)
				{
					gcode.WriteRetraction();
				}
				if (path.config != travelConfig && lastConfig != path.config)
				{
					gcode.WriteComment("TYPE:{0}".FormatWith(path.config.name));
					lastConfig = path.config;
				}

				int speed = path.config.speed;
				if (path.config.lineWidth != 0)
				{
					// Only apply the extrudeSpeedFactor to extrusion moves
					speed = speed * extrudeSpeedFactor / 100;
				}
				else
				{
					speed = speed * travelSpeedFactor / 100;
				}

				if (path.points.Count == 1
					&& path.config != travelConfig
					&& (gcode.GetPositionXY() - path.points[0]).ShorterThen(path.config.lineWidth * 2))
				{
					//Check for lots of small moves and combine them into one large line
					IntPoint nextPosition = path.points[0];
					int i = pathIndex + 1;
					while (i < paths.Count && paths[i].points.Count == 1 && (nextPosition - paths[i].points[0]).ShorterThen(path.config.lineWidth * 2))
					{
						nextPosition = paths[i].points[0];
						i++;
					}
					if (paths[i - 1].config == travelConfig)
					{
						i--;
					}

					if (i > pathIndex + 2)
					{
						nextPosition = gcode.GetPositionXY();
						for (int x = pathIndex; x < i - 1; x += 2)
						{
							long oldLen = (nextPosition - paths[x].points[0]).vSize();
							IntPoint newPoint = (paths[x].points[0] + paths[x + 1].points[0]) / 2;
							long newLen = (gcode.GetPositionXY() - newPoint).vSize();
							if (newLen > 0)
							{
								gcode.WriteMove(newPoint, speed, (int)(path.config.lineWidth * oldLen / newLen));
							}

							nextPosition = paths[x + 1].points[0];
						}

						gcode.WriteMove(paths[i - 1].points[0], speed, path.config.lineWidth);
						pathIndex = i - 1;
						continue;
					}
				}

				bool spiralize = path.config.spiralize;
				if (spiralize)
				{
					//Check if we are the last spiralize path in the list, if not, do not spiralize.
					for (int m = pathIndex + 1; m < paths.Count; m++)
					{
						if (paths[m].config.spiralize)
						{
							spiralize = false;
						}
					}
				}
				if (spiralize)
				{
					//If we need to spiralize then raise the head slowly by 1 layer as this path progresses.
					double totalLength = 0;
					int z = gcode.GetPositionZ();
					IntPoint currentPosition = gcode.GetPositionXY();
					for (int pointIndex = 0; pointIndex < path.points.Count; pointIndex++)
					{
						IntPoint nextPosition = path.points[pointIndex];
						totalLength += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
					}

					double length = 0.0;
					currentPosition = gcode.GetPositionXY();
					for (int i = 0; i < path.points.Count; i++)
					{
						IntPoint nextPosition = path.points[i];
						length += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
						gcode.setZ((int)(z + layerThickness * length / totalLength + .5));
						gcode.WriteMove(path.points[i], speed, path.config.lineWidth);
					}
				}
				else
				{
					/* // This is test code to remove double drawn small perimeter lines.
					if (path.config.lineWidth > 0
						&& path.points.Count > 2 // If the count is not greater than 2 there is no way it can ovelap itself.
						&& gcode.GetPositionXY() == path.points[path.points.Count - 1])
					{
						Polygons pathsWithOverlapsRemoved = GetPathsWithOverlapsRemoved(path.points, path.config.lineWidth / 2);
						if (pathsWithOverlapsRemoved.Count > 0)
						{
							for (int polygonIndex = 0; polygonIndex < pathsWithOverlapsRemoved.Count; polygonIndex++)
							{
								int startIndex = 0;
								Polygon polygon = pathsWithOverlapsRemoved[polygonIndex];
								if (polygonIndex > 0)
								{
									gcode.WriteMove(polygon[0], travelConfig.speed, 0);
									startIndex = 1; // We skip the first point in the next extrusion, because we just moved to it.
								}

								for (int pointIndex = startIndex; pointIndex < polygon.Count; pointIndex++)
								{
									gcode.WriteMove(polygon[pointIndex], speed, path.config.lineWidth);
								}
							}
						}
					}
					else
					 */
					{
						//TrimPerimeterIfNeeded(path);

						for (int i = 0; i < path.points.Count; i++)
						{
							gcode.WriteMove(path.points[i], speed, path.config.lineWidth);
						}
					}
				}
			}

			gcode.UpdateTotalPrintTime();
			if (liftHeadIfNeeded && extraTime > 0.0)
			{
				gcode.WriteComment("Small layer, adding delay of {0}".FormatWith(extraTime));
				gcode.WriteRetraction();
				gcode.setZ(gcode.GetPositionZ() + 3000);
				gcode.WriteMove(gcode.GetPositionXY(), travelConfig.speed, 0);
				gcode.WriteMove(gcode.GetPositionXY() - new IntPoint(-20000, 0), travelConfig.speed, 0);
				gcode.WriteDelay(extraTime);
			}
		}

		public void WritePolygon(Polygon polygon, int startIndex, GCodePathConfig config)
		{
			IntPoint currentPosition = polygon[startIndex];

			if (!config.spiralize
				&& (lastPosition.X != currentPosition.X
				|| lastPosition.Y != currentPosition.Y))
			{
				WriteTravel(currentPosition);
			}

			if (config.closedLoop)
			{
				for (int positionIndex = 1; positionIndex < polygon.Count; positionIndex++)
				{
					IntPoint destination = polygon[(startIndex + positionIndex) % polygon.Count];
					WriteExtrusionMove(destination, config);
					currentPosition = destination;
				}

				// We need to actually close the polygon so go back to the first point
				if (polygon.Count > 2)
				{
					WriteExtrusionMove(polygon[startIndex], config);
				}
			}
			else // we are not closed
			{
				if (startIndex == 0)
				{
					for (int positionIndex = 1; positionIndex < polygon.Count; positionIndex++)
					{
						IntPoint destination = polygon[positionIndex];
						WriteExtrusionMove(destination, config);
						currentPosition = destination;
					}
				}
				else
				{
					for (int positionIndex = polygon.Count - 1; positionIndex >= 1; positionIndex--)
					{
						IntPoint destination = polygon[(startIndex + positionIndex) % polygon.Count];
						WriteExtrusionMove(destination, config);
						currentPosition = destination;
					}
				}
			}
		}

		public void WritePolygonsByOptimizer(Polygons polygons, GCodePathConfig config)
		{
			PathOrderOptimizer orderOptimizer = new PathOrderOptimizer(lastPosition);
			orderOptimizer.AddPolygons(polygons);

			orderOptimizer.Optimize(config);

			for (int i = 0; i < orderOptimizer.bestPolygonOrderIndex.Count; i++)
			{
				int polygonIndex = orderOptimizer.bestPolygonOrderIndex[i];
				WritePolygon(polygons[polygonIndex], orderOptimizer.startIndexInPolygon[polygonIndex], config);
			}
		}

		public void WriteTravel(IntPoint positionToMoveTo)
		{
			GCodePath path = GetLatestPathWithConfig(travelConfig);

			if (forceRetraction)
			{
				path.Retract = true;
				forceRetraction = false;
			}
			else if (outerPerimetersToAvoidCrossing != null)
			{
				List<IntPoint> pointList = new List<IntPoint>();
				if (outerPerimetersToAvoidCrossing.CreatePathInsideBoundary(lastPosition, positionToMoveTo, pointList))
				{
					long lineLength_um = 0;
					// we can stay inside so move within the boundary
					for (int pointIndex = 0; pointIndex < pointList.Count; pointIndex++)
					{
						path.points.Add(pointList[pointIndex]);
						if (pointIndex > 0)
						{
							lineLength_um += (pointList[pointIndex] - pointList[pointIndex - 1]).Length();
						}
					}

					// If the internal move is very long (20 mm), do a retration anyway
					if (lineLength_um > retractionMinimumDistance_um)
					{
						path.Retract = true;
					}
				}
				else
				{
					if ((lastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance_um))
					{
						// We are moving relatively far and are going to cross a boundary so do a retraction.
						path.Retract = true;
					}
				}
			}
			else if (alwaysRetract)
			{
				if ((lastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance_um))
				{
					path.Retract = true;
				}
			}

			path.points.Add(positionToMoveTo);
			lastPosition = positionToMoveTo;
		}

		private static void TrimPerimeterIfNeeded(GCodePath path)
		{
			if (path.config.name == "WALL-OUTER" || path.config.name == "WALL-INNER")
			{
				double currentDistance = 0;
				double targetDistance = (long)(path.config.lineWidth * .90);

				if (path.points.Count > 1)
				{
					for (int pointIndex = path.points.Count - 1; pointIndex > 0; pointIndex--)
					{
						// Calculate distance between 2 points
						currentDistance = (path.points[pointIndex] - path.points[pointIndex - 1]).Length();

						// If distance exceeds clip distance:
						//  - Sets the new last path point
						if (currentDistance > targetDistance)
						{
							DoublePoint dir = new DoublePoint((path.points[pointIndex].X - path.points[pointIndex - 1].X) / currentDistance, (path.points[pointIndex].Y - path.points[pointIndex - 1].Y) / currentDistance);

							double newDistance = currentDistance - targetDistance;
							dir.X *= newDistance;
							dir.Y *= newDistance;

							IntPoint clippedEndpoint = path.points[pointIndex - 1] + new IntPoint(dir.X, dir.Y);

							path.points[pointIndex] = clippedEndpoint;
							break;
						}
						else if (currentDistance == targetDistance)
						{
							// Pops off last point because it is at the limit distance
							path.points.RemoveAt(path.points.Count - 1);
							break;
						}
						else
						{
							// Pops last point and reduces distance remaining to target
							targetDistance = targetDistance - currentDistance;
							path.points.RemoveAt(path.points.Count - 1);
						}
					}
				}
			}
		}

		private void ForceNewPathStart()
		{
			if (paths.Count > 0)
			{
				paths[paths.Count - 1].done = true;
			}
		}

		private GCodePath GetLatestPathWithConfig(GCodePathConfig config)
		{
			if (paths.Count > 0
				&& paths[paths.Count - 1].config == config
				&& !paths[paths.Count - 1].done)
			{
				return paths[paths.Count - 1];
			}

			paths.Add(new GCodePath());
			GCodePath ret = paths[paths.Count - 1];
			ret.Retract = false;
			ret.config = config;
			ret.extruderIndex = currentExtruderIndex;
			ret.done = false;
			return ret;
		}

		internal class GCodePath
		{
			internal GCodePathConfig config;
			internal bool done;
			internal int extruderIndex;
			internal List<IntPoint> points = new List<IntPoint>();
			internal bool retract;

			internal bool Retract { get { return retract; } set { retract = value; } }

			//Path is finished, no more moves should be added, and a new path should be started instead of any appending done to this one.
		}
	}
}