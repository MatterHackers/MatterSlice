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

using System;
using ClipperLib;
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

		private GCodeExport gcodeExport = new GCodeExport();

		public long CurrentZ { get { return gcodeExport.CurrentZ; } }
		
		public IntPoint LastPosition
		{
			get; private set;
		}

		private AvoidCrossingPerimeters outerPerimetersToAvoidCrossing;

		private List<GCodePath> paths = new List<GCodePath>();

		private int retractionMinimumDistance_um;

		private double totalPrintTime;

		private GCodePathConfig travelConfig = new GCodePathConfig();

		private int travelSpeedFactor;

		public GCodePlanner(GCodeExport gcode, int travelSpeed, int retractionMinimumDistance_um)
		{
			this.gcodeExport = gcode;
			travelConfig = new GCodePathConfig();
			travelConfig.SetData(travelSpeed, 0, "travel");

			LastPosition = gcode.GetPositionXY();
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
			Point3 lastPosition = gcodeExport.GetPosition();
			double travelTime = 0.0;
			double extrudeTime = 0.0;
			for (int n = 0; n < paths.Count; n++)
			{
				GCodePath path = paths[n];
				for (int pointIndex = 0; pointIndex < path.points.Count; pointIndex++)
				{
					Point3 currentPosition = path.points[pointIndex];
					double thisTime = (lastPosition - currentPosition).LengthMm() / (double)(path.config.speed);
					if (path.config.lineWidth_um != 0)
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
					if (path.config.lineWidth_um == 0)
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

		[Flags]
		enum Altered { remove = 1, merged = 2 };

		public List<List<Point3>> GetPathsWithOverlapsRemoved(List<Point3> perimeter, int overlapMergeAmount_um)
		{
			// make a copy that has every point duplicated (so that we have them as segments).
			List<Point3> polySegments = new List<Point3>(perimeter.Count * 2);
			for (int i = 0; i < perimeter.Count - 1; i++)
			{
				Point3 point = perimeter[i];
				Point3 nextPoint = perimeter[i + 1];

				polySegments.Add(point);
				polySegments.Add(nextPoint);
			}

			Altered[] markedAltered = new Altered[polySegments.Count/2];

			int segmentCount = polySegments.Count / 2;
			// now walk every segment and check if there is another segment that is similar enough to merge them together
			for (int firstSegmentIndex = 0; firstSegmentIndex < segmentCount; firstSegmentIndex++)
			{
				int firstPointIndex = firstSegmentIndex * 2;
				for (int checkSegmentIndex = firstSegmentIndex + 1; checkSegmentIndex < segmentCount; checkSegmentIndex++)
				{
					int checkPointIndex = checkSegmentIndex * 2;
					// The first point of start and the last point of check (the path will be coming back on itself).
					long startDelta = (polySegments[firstPointIndex] - polySegments[checkPointIndex + 1]).Length();
					// if the segments are similar enough
					if (startDelta < overlapMergeAmount_um)
					{
						// The last point of start and the first point of check (the path will be coming back on itself).
						long endDelta = (polySegments[firstPointIndex + 1] - polySegments[checkPointIndex]).Length();
						if (endDelta < overlapMergeAmount_um)
						{
							// move the first segments points to the average of the merge positions
							polySegments[firstPointIndex] = (polySegments[firstPointIndex] + polySegments[checkPointIndex + 1]) / 2; // the start
							polySegments[firstPointIndex + 1] = (polySegments[firstPointIndex + 1] + polySegments[checkPointIndex]) / 2; // the end

							markedAltered[firstSegmentIndex] = Altered.merged;
							// mark this segment for removal
							markedAltered[checkSegmentIndex] = Altered.remove;
							// We only expect to find one match for each segment, so move on to the next segment
							break;
						}
					}
				}
			}

			// Check for perimeter edges that need to be removed that are the u turns of sections that go back on themselves.
			//  __________
			// |__________|	->  |--------|  the 2 vertical sections should be removed
			for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
			{
				int prevSegmentIndex = (int)((uint)(segmentIndex - 1) % (uint)segmentCount);
				int nextSegmentIndex = (segmentIndex + 1) % segmentCount;
				if ((markedAltered[nextSegmentIndex] == Altered.merged && markedAltered[prevSegmentIndex] == Altered.remove)
					|| (markedAltered[nextSegmentIndex] == Altered.remove && markedAltered[prevSegmentIndex] == Altered.merged))
				{
					markedAltered[segmentIndex] = Altered.remove;
				}
			}

			// remove the marked segments
			for (int segmentIndex = segmentCount - 1; segmentIndex >= 0; segmentIndex--)
			{
				int pointIndex = segmentIndex * 2;
				if (markedAltered[segmentIndex] == Altered.remove)
				{
					polySegments.RemoveRange(pointIndex, 2);
				}
			}

			// go through the polySegments and create a new polygon for every connected set of segments
			List<List<Point3>> separatedPolygons = new List<List<Point3>>();
			List<Point3> currentPolygon = new List<Point3>();
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
					currentPolygon = new List<Point3>();
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
			if (outerPerimetersToAvoidCrossing == null || outerPerimetersToAvoidCrossing.PointIsInsideBoundary(LastPosition))
			{
				return;
			}

			IntPoint p = LastPosition;
			if (outerPerimetersToAvoidCrossing.MovePointInsideBoundary(ref p, distance))
			{
				//Move inside again, so we move out of tight 90deg corners
				outerPerimetersToAvoidCrossing.MovePointInsideBoundary(ref p, distance);
				if (outerPerimetersToAvoidCrossing.PointIsInsideBoundary(p))
				{
					QueueTravel(p);
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

		public void QueueExtrusionMove(IntPoint destination, GCodePathConfig config)
		{
			GetLatestPathWithConfig(config).points.Add(new Point3(destination, CurrentZ));
			LastPosition = destination;
		}

		public void WriteQueuedGCode(int layerThickness)
		{
			GCodePathConfig lastConfig = null;
			int extruderIndex = gcodeExport.GetExtruderIndex();

			for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
			{
				GCodePath path = paths[pathIndex];
				if (extruderIndex != path.extruderIndex)
				{
					extruderIndex = path.extruderIndex;
					gcodeExport.SwitchExtruder(extruderIndex);
				}
				else if (path.Retract)
				{
					gcodeExport.WriteRetraction();
				}
				if (path.config != travelConfig && lastConfig != path.config)
				{
					gcodeExport.WriteComment("TYPE:{0}".FormatWith(path.config.gcodeComment));
					lastConfig = path.config;
				}

				double speed = path.config.speed;
				if (path.config.lineWidth_um != 0)
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
					&& (gcodeExport.GetPositionXY() - path.points[0].XYPoint).ShorterThen(path.config.lineWidth_um * 2))
				{
					//Check for lots of small moves and combine them into one large line
					Point3 nextPosition = path.points[0];
					int i = pathIndex + 1;
					while (i < paths.Count && paths[i].points.Count == 1 && (nextPosition - paths[i].points[0]).ShorterThen(path.config.lineWidth_um * 2))
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
						nextPosition = gcodeExport.GetPosition();
						for (int x = pathIndex; x < i - 1; x += 2)
						{
							long oldLen = (nextPosition - paths[x].points[0]).Length();
							Point3 newPoint = (paths[x].points[0] + paths[x + 1].points[0]) / 2;
							long newLen = (gcodeExport.GetPosition() - newPoint).Length();
							if (newLen > 0)
							{
								gcodeExport.WriteMove(newPoint, speed, (int)(path.config.lineWidth_um * oldLen / newLen));
							}

							nextPosition = paths[x + 1].points[0];
						}

						gcodeExport.WriteMove(paths[i - 1].points[0], speed, path.config.lineWidth_um);
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

				if (spiralize) // if we are still in spiralize mode
				{
					//If we need to spiralize then raise the head slowly by 1 layer as this path progresses.
					double totalLength = 0;
					long z = gcodeExport.GetPositionZ();
					IntPoint currentPosition = gcodeExport.GetPositionXY();
					for (int pointIndex = 0; pointIndex < path.points.Count; pointIndex++)
					{
						IntPoint nextPosition = path.points[pointIndex].XYPoint;
						totalLength += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
					}

					double length = 0.0;
					currentPosition = gcodeExport.GetPositionXY();
					for (int i = 0; i < path.points.Count; i++)
					{
						IntPoint nextPosition = path.points[i].XYPoint;
						length += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
						Point3 nextExtrusion = path.points[i];
						nextExtrusion.z = (int)(z + layerThickness * length / totalLength + .5);
						gcodeExport.WriteMove(nextExtrusion, speed, path.config.lineWidth_um);
					}
				}
				else
				{
					// This is test code to remove double drawn small perimeter lines.
					if (RemoveDoubleDrawPerimeterLines(path, speed))
					{
						return;
					}
					else
					{
						TrimPerimeterIfNeeded(path);

						for (int i = 0; i < path.points.Count; i++)
						{
							gcodeExport.WriteMove(path.points[i], speed, path.config.lineWidth_um);
						}
					}
				}
			}

			gcodeExport.UpdateTotalPrintTime();
		}

		private bool RemoveDoubleDrawPerimeterLines(GCodePath path, double speed)
		{
			return false;
			if (path.config.lineWidth_um > 0
				&& path.points.Count > 2 // If the count is not greater than 2 there is no way it can overlap itself.
				&& gcodeExport.GetPosition() == path.points[path.points.Count - 1])
			{
				List<List<Point3>> pathsWithOverlapsRemoved = GetPathsWithOverlapsRemoved(path.points, path.config.lineWidth_um / 2);
				if (pathsWithOverlapsRemoved.Count > 0)
				{
					for (int polygonIndex = 0; polygonIndex < pathsWithOverlapsRemoved.Count; polygonIndex++)
					{
						int startIndex = 0;
						List<Point3> polygon = pathsWithOverlapsRemoved[polygonIndex];
						if (polygonIndex > 0)
						{
							gcodeExport.WriteMove(polygon[0], travelConfig.speed, 0);
							startIndex = 1; // We skip the first point in the next extrusion, because we just moved to it.
						}

						for (int pointIndex = startIndex; pointIndex < polygon.Count; pointIndex++)
						{
							gcodeExport.WriteMove(polygon[pointIndex], speed, path.config.lineWidth_um);
						}
					}
				}
			}
		}

		public void QueuePolygon(Polygon polygon, int startIndex, GCodePathConfig config)
		{
			IntPoint currentPosition = polygon[startIndex];

			if (!config.spiralize
				&& (LastPosition.X != currentPosition.X
				|| LastPosition.Y != currentPosition.Y))
			{
				QueueTravel(currentPosition);
			}

			if (config.closedLoop)
			{
				for (int positionIndex = 1; positionIndex < polygon.Count; positionIndex++)
				{
					IntPoint destination = polygon[(startIndex + positionIndex) % polygon.Count];
					QueueExtrusionMove(destination, config);
					currentPosition = destination;
				}

				// We need to actually close the polygon so go back to the first point
				if (polygon.Count > 2)
				{
					QueueExtrusionMove(polygon[startIndex], config);
				}
			}
			else // we are not closed
			{
				if (startIndex == 0)
				{
					for (int positionIndex = 1; positionIndex < polygon.Count; positionIndex++)
					{
						IntPoint destination = polygon[positionIndex];
						QueueExtrusionMove(destination, config);
						currentPosition = destination;
					}
				}
				else
				{
					for (int positionIndex = polygon.Count - 1; positionIndex >= 1; positionIndex--)
					{
						IntPoint destination = polygon[(startIndex + positionIndex) % polygon.Count];
						QueueExtrusionMove(destination, config);
						currentPosition = destination;
					}
				}
			}
		}

		public void QueuePolygonsByOptimizer(Polygons polygons, GCodePathConfig config)
		{
			IslandOrderOptimizer orderOptimizer = new IslandOrderOptimizer(LastPosition);
			orderOptimizer.AddPolygons(polygons);

			orderOptimizer.Optimize(config);

			for (int i = 0; i < orderOptimizer.bestIslandOrderIndex.Count; i++)
			{
				int polygonIndex = orderOptimizer.bestIslandOrderIndex[i];
				QueuePolygon(polygons[polygonIndex], orderOptimizer.startIndexInPolygon[polygonIndex], config);
			}
		}

		public void QueueTravel(IntPoint positionToMoveTo)
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
				if (outerPerimetersToAvoidCrossing.CreatePathInsideBoundary(LastPosition, positionToMoveTo, pointList))
				{
					long lineLength_um = 0;
					// we can stay inside so move within the boundary
					for (int pointIndex = 0; pointIndex < pointList.Count; pointIndex++)
					{
						path.points.Add(new Point3(pointList[pointIndex], CurrentZ));
						if (pointIndex > 0)
						{
							lineLength_um += (pointList[pointIndex] - pointList[pointIndex - 1]).Length();
						}
					}

					// If the internal move is very long (20 mm), do a retraction anyway
					if (lineLength_um > retractionMinimumDistance_um)
					{
						path.Retract = true;
					}
				}
				else
				{
					if ((LastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance_um))
					{
						// We are moving relatively far and are going to cross a boundary so do a retraction.
						path.Retract = true;
					}
				}
			}
			else if (alwaysRetract)
			{
				if ((LastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance_um))
				{
					path.Retract = true;
				}
			}

			path.points.Add(new Point3(positionToMoveTo, CurrentZ));
			LastPosition = positionToMoveTo;
		}

		private static void TrimPerimeterIfNeeded(GCodePath path)
		{
			if (path.config.gcodeComment == "WALL-OUTER" || path.config.gcodeComment == "WALL-INNER")
			{
				long currentDistance = 0;
				long targetDistance = (long)(path.config.lineWidth_um * .90);

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
							long newDistance = currentDistance - targetDistance;

							Point3 dir = (path.points[pointIndex] - path.points[pointIndex - 1]) * newDistance / currentDistance;

							Point3 clippedEndpoint = path.points[pointIndex - 1] + dir;

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
			internal List<Point3> points = new List<Point3>();

			internal bool Retract { get; set; }

			//Path is finished, no more moves should be added, and a new path should be started instead of any appending done to this one.
		}

		public Polygon MakeCloseSegmentsMergable(Polygon perimeter, int distanceNeedingAdd)
		{
			throw new System.NotImplementedException();
		}
	}
}