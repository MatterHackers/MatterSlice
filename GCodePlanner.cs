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
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using Pathfinding;
	using QuadTree;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	//The GCodePlanner class stores multiple moves that are planned.
	// It facilitates the avoidCrossingPerimeters to keep the head inside the print.
	// It also keeps track of the print time estimate for this planning so speed adjustments can be made for the minimum-layer-time.
	public class GCodePlanner
	{
		private int currentExtruderIndex;

		private double extraTime;

		private bool forceRetraction;

		private GCodeExport gcodeExport = new GCodeExport();

		private PathFinder lastValidPathFinder;
		private PathFinder pathFinder;
		private List<GCodePath> paths = new List<GCodePath>();

		private double perimeterStartEndOverlapRatio;

		private int retractionMinimumDistance_um;

		private double totalPrintTime;

		private GCodePathConfig travelConfig;

		public GCodePlanner(GCodeExport gcode, int travelSpeed, int retractionMinimumDistance_um, double perimeterStartEndOverlap = 0)
		{
			this.gcodeExport = gcode;
			travelConfig = new GCodePathConfig("travelConfig");
			travelConfig.SetData(travelSpeed, 0, "travel");

			LastPosition = gcode.GetPositionXY();
			extraTime = 0.0;
			totalPrintTime = 0.0;
			forceRetraction = false;
			currentExtruderIndex = gcode.GetExtruderIndex();
			this.retractionMinimumDistance_um = retractionMinimumDistance_um;

			this.perimeterStartEndOverlapRatio = Math.Max(0, Math.Min(1, perimeterStartEndOverlap));
		}

		public long CurrentZ { get { return gcodeExport.CurrentZ; } }

		public IntPoint LastPosition
		{
			get; private set;
		}

		public PathFinder PathFinder
		{
			get
			{
				return pathFinder;
			}
			set
			{
				if (value != null
					&& lastValidPathFinder != value)
				{
					lastValidPathFinder = value;
				}
				pathFinder = value;
			}
		}

		public static GCodePath TrimPerimeter(GCodePath inPath, double perimeterStartEndOverlapRatio)
		{
			GCodePath path = new GCodePath(inPath);
			long currentDistance = 0;
			long targetDistance = (long)(path.config.lineWidth_um * (1 - perimeterStartEndOverlapRatio));

			if (path.polygon.Count > 1)
			{
				for (int pointIndex = path.polygon.Count - 1; pointIndex > 0; pointIndex--)
				{
					// Calculate distance between 2 points
					currentDistance = (path.polygon[pointIndex] - path.polygon[pointIndex - 1]).Length();

					// If distance exceeds clip distance:
					//  - Sets the new last path point
					if (currentDistance > targetDistance)
					{
						long newDistance = currentDistance - targetDistance;
						if (targetDistance > 50) // Don't clip segments less than 50 um. We get too much truncation error.
						{
							IntPoint dir = (path.polygon[pointIndex] - path.polygon[pointIndex - 1]) * newDistance / currentDistance;

							IntPoint clippedEndpoint = path.polygon[pointIndex - 1] + dir;

							path.polygon[pointIndex] = clippedEndpoint;
						}
						break;
					}
					else if (currentDistance == targetDistance)
					{
						// Pops off last point because it is at the limit distance
						path.polygon.RemoveAt(path.polygon.Count - 1);
						break;
					}
					else
					{
						// Pops last point and reduces distance remaining to target
						targetDistance -= currentDistance;
						path.polygon.RemoveAt(path.polygon.Count - 1);
					}
				}
			}

			return path;
		}

		public void ForceMinimumLayerTime(double minTime, int minimumPrintingSpeed)
		{
			IntPoint lastPosition = gcodeExport.GetPosition();
			double travelTime = 0.0;
			double extrudeTime = 0.0;
			for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
			{
				GCodePath path = paths[pathIndex];
				for (int pointIndex = 0; pointIndex < path.polygon.Count; pointIndex++)
				{
					IntPoint currentPosition = path.polygon[pointIndex];
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

				gcodeExport.LayerSpeedRatio = GetNewLayerSpeedRatio(minimumPrintingSpeed, extrudeTime, minExtrudeTime);

				if (minTime - (extrudeTime / gcodeExport.LayerSpeedRatio) - travelTime > 0.1)
				{
					//TODO: Use up this extra time (circle around the print?)
					this.extraTime = minTime - (extrudeTime / gcodeExport.LayerSpeedRatio) - travelTime;
				}
				this.totalPrintTime = (extrudeTime / gcodeExport.LayerSpeedRatio) + travelTime;
			}
			else
			{
				this.totalPrintTime = totalTime;
			}
		}

		private double GetNewLayerSpeedRatio(int minimumPrintingSpeed, double extrudeTime, double minExtrudeTime)
		{
			double newLayerSpeedRatio = extrudeTime / minExtrudeTime;
			foreach (var path in paths)
			{
				if (path.config.lineWidth_um == 0)
				{
					continue;
				}

				int speed = (int)(path.config.speed * newLayerSpeedRatio);
				if (speed < minimumPrintingSpeed)
				{
					newLayerSpeedRatio = (double)(minimumPrintingSpeed) / (double)(path.config.speed);
				}
			}

			//Only slow down with the minimum time if that will be slower then a factor already set. First layer slowdown also sets the speed factor.
			return newLayerSpeedRatio;
		}

		public void ForceRetract()
		{
			forceRetraction = true;
		}

		public int GetExtruder()
		{
			return currentExtruderIndex;
		}

		public void QueueExtrusionMove(IntPoint destination, GCodePathConfig config)
		{
			GetLatestPathWithConfig(config).polygon.Add(new IntPoint(destination, CurrentZ));
			LastPosition = destination;

			//ValidatePaths();
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

		public void QueuePolygons(Polygons polygons, GCodePathConfig config)
		{
			foreach (var polygon in polygons)
			{
				QueuePolygon(polygon, 0, config);
			}
		}

		public bool QueuePolygonsByOptimizer(Polygons polygons, GCodePathConfig config)
		{
			if (polygons.Count == 0)
			{
				return false;
			}

			PathOrderOptimizer orderOptimizer = new PathOrderOptimizer(LastPosition);
			orderOptimizer.AddPolygons(polygons);

			orderOptimizer.Optimize(config);

			for (int i = 0; i < orderOptimizer.bestIslandOrderIndex.Count; i++)
			{
				int polygonIndex = orderOptimizer.bestIslandOrderIndex[i];
				QueuePolygon(polygons[polygonIndex], orderOptimizer.startIndexInPolygon[polygonIndex], config);
			}

			return true;
		}

		public void QueueTravel(IntPoint positionToMoveTo)
		{
			GCodePath path = GetLatestPathWithConfig(travelConfig);

			if (forceRetraction)
			{
				path.Retract = RetractType.Force;
				forceRetraction = false;
			}

			if (PathFinder != null)
			{
				Polygon pathPolygon = new Polygon();
				if (PathFinder.CreatePathInsideBoundary(LastPosition, positionToMoveTo, pathPolygon, true, gcodeExport.LayerIndex))
				{
					IntPoint lastPathPosition = LastPosition;
					long lineLength_um = 0;

					if (pathPolygon.Count > 0)
					{
						// we can stay inside so move within the boundary
						for (int positionIndex = 0; positionIndex < pathPolygon.Count; positionIndex++)
						{
							path.polygon.Add(new IntPoint(pathPolygon[positionIndex], CurrentZ)
							{
								Width = 0
							});
							lineLength_um += (pathPolygon[positionIndex] - lastPathPosition).Length();
							lastPathPosition = pathPolygon[positionIndex];
						}

						// If the internal move is very long (> retractionMinimumDistance_um), do a retraction
						if (lineLength_um > retractionMinimumDistance_um)
						{
							path.Retract = RetractType.Requested;
						}
					}
					// else the path is good it just goes directly to the positionToMoveTo
				}
				else if ((LastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance_um / 10))
				{
					// can't find a good path and moving more than a very little bit
					path.Retract = RetractType.Requested;
				}
			}

			// Always check if the distance is greated than the amount need to retract.
			if ((LastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance_um))
			{
				path.Retract = RetractType.Requested;
			}

			path.polygon.Add(new IntPoint(positionToMoveTo, CurrentZ)
			{
				Width = 0,
			});

			LastPosition = positionToMoveTo;

			//ValidatePaths();
		}

		public bool ExtruderWillChange(int extruder)
		{
			if (extruder == currentExtruderIndex)
			{
				return false;
			}

			return true;
		}

		public void SetExtruder(int extruder)
		{
			currentExtruderIndex = extruder;
		}

		public void WriteQueuedGCode(int layerThickness, int fanSpeedPercent = -1, int bridgeFanSpeedPercent = -1)
		{
			GCodePathConfig lastConfig = null;
			int extruderIndex = gcodeExport.GetExtruderIndex();

			for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
			{
				var path = paths[pathIndex];
				if (extruderIndex != path.extruderIndex)
				{
					extruderIndex = path.extruderIndex;
					gcodeExport.SwitchExtruder(extruderIndex);
				}
				else if (path.Retract != RetractType.None)
				{
					double timeOfMove = 0;

					if (path.config.lineWidth_um == 0)
					{
						var lengthToStart = (gcodeExport.GetPosition() - path.polygon[0]).Length();
						var lengthOfMove = lengthToStart + path.polygon.PolygonLength();
						timeOfMove = lengthOfMove / 1000.0 / path.config.speed;
					}

					gcodeExport.WriteRetraction(timeOfMove, path.Retract == RetractType.Force);
				}
				if (path.config != travelConfig && lastConfig != path.config)
				{
					if (path.config.gcodeComment == "BRIDGE" && bridgeFanSpeedPercent != -1)
					{
						gcodeExport.WriteFanCommand(bridgeFanSpeedPercent);
					}
					else if (lastConfig?.gcodeComment == "BRIDGE" && bridgeFanSpeedPercent != -1)
					{
						gcodeExport.WriteFanCommand(fanSpeedPercent);
					}

					gcodeExport.WriteComment("TYPE:{0}".FormatWith(path.config.gcodeComment));
					lastConfig = path.config;
				}

				double speed = path.config.speed;

				if (path.config.lineWidth_um != 0)
				{
					// Prevent cooling overrides from affecting bridge moves
					if (path.config.gcodeComment != "BRIDGE")
					{
						speed = speed * gcodeExport.LayerSpeedRatio;
					}
				}

				if (path.polygon.Count == 1
					&& path.config != travelConfig
					&& (gcodeExport.GetPositionXY() - path.polygon[0]).ShorterThen(path.config.lineWidth_um * 2))
				{
					//Check for lots of small moves and combine them into one large line
					IntPoint nextPosition = path.polygon[0];
					int i = pathIndex + 1;
					while (i < paths.Count && paths[i].polygon.Count == 1 && (nextPosition - paths[i].polygon[0]).ShorterThen(path.config.lineWidth_um * 2))
					{
						nextPosition = paths[i].polygon[0];
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
							long oldLen = (nextPosition - paths[x].polygon[0]).Length();
							IntPoint newPoint = (paths[x].polygon[0] + paths[x + 1].polygon[0]) / 2;
							long newLen = (gcodeExport.GetPosition() - newPoint).Length();
							if (newLen > 0)
							{
								gcodeExport.WriteMove(newPoint, speed, (int)(path.config.lineWidth_um * oldLen / newLen));
							}

							nextPosition = paths[x + 1].polygon[0];
						}

						long lineWidth_um = path.config.lineWidth_um;
						if (paths[i - 1].polygon[0].Width != 0)
						{
							lineWidth_um = paths[i - 1].polygon[0].Width;
						}

						gcodeExport.WriteMove(paths[i - 1].polygon[0], speed, lineWidth_um);
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
					for (int pointIndex = 0; pointIndex < path.polygon.Count; pointIndex++)
					{
						IntPoint nextPosition = path.polygon[pointIndex];
						totalLength += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
					}

					double length = 0.0;
					currentPosition = gcodeExport.GetPositionXY();
					for (int i = 0; i < path.polygon.Count; i++)
					{
						IntPoint nextPosition = path.polygon[i];
						length += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
						IntPoint nextExtrusion = path.polygon[i];
						nextExtrusion.Z = (int)(z + layerThickness * length / totalLength + .5);
						gcodeExport.WriteMove(nextExtrusion, speed, path.config.lineWidth_um);
					}
				}
				else
				{
					// This is test code to remove double drawn small perimeter lines.
					if (path.config.gcodeComment == "WALL-OUTER" || path.config.gcodeComment == "WALL-INNER")
					{
						//string perimeterString = Newtonsoft.Json.JsonConvert.SerializeObject(path);
						if (perimeterStartEndOverlapRatio < 1)
						{
							path = TrimPerimeter(path, perimeterStartEndOverlapRatio);
						}
					}

					int outputCount = path.polygon.Count;
					for (int i = 0; i < outputCount; i++)
					{
						long lineWidth_um = path.config.lineWidth_um;
						if (path.polygon[i].Width != 0)
						{
							lineWidth_um = path.polygon[i].Width;
						}

						gcodeExport.WriteMove(path.polygon[i], speed, lineWidth_um);
					}
				}
			}

			gcodeExport.UpdateTotalPrintTime();
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
			ret.Retract = RetractType.None;
			ret.config = config;
			ret.extruderIndex = currentExtruderIndex;
			ret.done = false;
			return ret;
		}

		private void ValidatePaths()
		{
			bool first = true;
			IntPoint lastPosition = new IntPoint();
			for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
			{
				var path = paths[pathIndex];
				for (int polyIndex = 0; polyIndex < path.polygon.Count; polyIndex++)
				{
					var position = path.polygon[polyIndex];
					if (first)
					{
						first = false;
					}
					else
					{
						if (pathIndex == paths.Count - 1
							&& polyIndex == path.polygon.Count - 1
							&& lastValidPathFinder != null
							&& !lastValidPathFinder.OutlineData.Polygons.PointIsInside((position + lastPosition) / 2))
						{
							// an easy way to get the path
							string startEndString = $"start:({position.X}, {position.Y}), end:({lastPosition.X}, {lastPosition.Y})";
							string outlineString = lastValidPathFinder.OutlineData.Polygons.WriteToString();
							long length = (position - lastPosition).Length();
							int a = 0;
						}
					}
					lastPosition = position;
				}
			}
		}
	}
}