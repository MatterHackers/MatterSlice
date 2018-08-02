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

		public static GCodePath TrimGCodePath(GCodePath inPath, long targetDistance)
		{
			GCodePath path = new GCodePath(inPath);
			// get a new trimmed polygon
			path.Polygon = path.Polygon.Trim(targetDistance);

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
				for (int pointIndex = 0; pointIndex < path.Polygon.Count; pointIndex++)
				{
					IntPoint currentPosition = path.Polygon[pointIndex];
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

			gcodeExport.LayerTime = extrudeTime + travelTime;
			if (gcodeExport.LayerTime < minTime && extrudeTime > 0.0)
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
				this.totalPrintTime = gcodeExport.LayerTime;
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
			GetLatestPathWithConfig(config).Polygon.Add(new IntPoint(destination, CurrentZ));
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

		public GCodePath QueueFanCommand(int fanSpeedPercent, GCodePathConfig config)
		{
			var path = GetNewPath(config);
			path.FanPercent = fanSpeedPercent;
			return path;
		}

		public void QueuePolygons(Polygons polygons, GCodePathConfig config)
		{
			foreach (var polygon in polygons)
			{
				QueuePolygon(polygon, 0, config);
			}
		}

		public bool QueuePolygonsByOptimizer(Polygons polygons, PathFinder pathFinder, GCodePathConfig config, int layerIndex)
		{
			if (polygons.Count == 0)
			{
				return false;
			}

			PathOrderOptimizer orderOptimizer = new PathOrderOptimizer(LastPosition);
			orderOptimizer.AddPolygons(polygons);

			orderOptimizer.Optimize(pathFinder, layerIndex, config);

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
							path.Polygon.Add(new IntPoint(pathPolygon[positionIndex], CurrentZ)
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

			// Always check if the distance is greater than the amount need to retract.
			if ((LastPosition - positionToMoveTo).LongerThen(retractionMinimumDistance_um))
			{
				path.Retract = RetractType.Requested;
			}

			path.Polygon.Add(new IntPoint(positionToMoveTo, CurrentZ)
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

		public void WriteQueuedGCode(int layerThickness)
		{
			GCodePathConfig lastConfig = null;
			int extruderIndex = gcodeExport.GetExtruderIndex();

			for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
			{
				var path = paths[pathIndex];
				if (extruderIndex != path.ExtruderIndex)
				{
					extruderIndex = path.ExtruderIndex;
					gcodeExport.SwitchExtruder(extruderIndex);
				}
				else if (path.Retract != RetractType.None)
				{
					double timeOfMove = 0;

					if (path.config.lineWidth_um == 0)
					{
						var lengthToStart = (gcodeExport.GetPosition() - path.Polygon[0]).Length();
						var lengthOfMove = lengthToStart + path.Polygon.PolygonLength();
						timeOfMove = lengthOfMove / 1000.0 / path.config.speed;
					}

					gcodeExport.WriteRetraction(timeOfMove, path.Retract == RetractType.Force);
				}
				if (lastConfig != path.config && path.config != travelConfig)
				{
					gcodeExport.WriteComment("TYPE:{0}".FormatWith(path.config.gcodeComment));
					lastConfig = path.config;
				}
				if (path.FanPercent != -1)
				{
					gcodeExport.WriteFanCommand(path.FanPercent);
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

				if (path.Polygon.Count == 1
					&& path.config != travelConfig
					&& (gcodeExport.GetPositionXY() - path.Polygon[0]).ShorterThen(path.config.lineWidth_um * 2))
				{
					//Check for lots of small moves and combine them into one large line
					IntPoint nextPosition = path.Polygon[0];
					int i = pathIndex + 1;
					while (i < paths.Count && paths[i].Polygon.Count == 1 && (nextPosition - paths[i].Polygon[0]).ShorterThen(path.config.lineWidth_um * 2))
					{
						nextPosition = paths[i].Polygon[0];
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
							long oldLen = (nextPosition - paths[x].Polygon[0]).Length();
							IntPoint newPoint = (paths[x].Polygon[0] + paths[x + 1].Polygon[0]) / 2;
							long newLen = (gcodeExport.GetPosition() - newPoint).Length();
							if (newLen > 0)
							{
								gcodeExport.WriteMove(newPoint, speed, (int)(path.config.lineWidth_um * oldLen / newLen));
							}

							nextPosition = paths[x + 1].Polygon[0];
						}

						long lineWidth_um = path.config.lineWidth_um;
						if (paths[i - 1].Polygon[0].Width != 0)
						{
							lineWidth_um = paths[i - 1].Polygon[0].Width;
						}

						gcodeExport.WriteMove(paths[i - 1].Polygon[0], speed, lineWidth_um);
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
					for (int pointIndex = 0; pointIndex < path.Polygon.Count; pointIndex++)
					{
						IntPoint nextPosition = path.Polygon[pointIndex];
						totalLength += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
					}

					double length = 0.0;
					currentPosition = gcodeExport.GetPositionXY();
					for (int i = 0; i < path.Polygon.Count; i++)
					{
						IntPoint nextPosition = path.Polygon[i];
						length += (currentPosition - nextPosition).LengthMm();
						currentPosition = nextPosition;
						IntPoint nextExtrusion = path.Polygon[i];
						nextExtrusion.Z = (int)(z + layerThickness * length / totalLength + .5);
						gcodeExport.WriteMove(nextExtrusion, speed, path.config.lineWidth_um);
					}
				}
				else
				{
					var loopStart = gcodeExport.GetPosition();
					int pointCount = path.Polygon.Count;

					bool outerPerimeter = (path.config.gcodeComment == "WALL-OUTER" || path.config.gcodeComment == "WALL-INNER");
					bool completeLoop = (pointCount > 0 && path.Polygon[pointCount - 1] == loopStart);
					bool trimmed = outerPerimeter && completeLoop && perimeterStartEndOverlapRatio < 1;

					// This is test code to remove double drawn small perimeter lines.
					if (trimmed)
					{
						long targetDistance = (long)(path.config.lineWidth_um * (1 - perimeterStartEndOverlapRatio));
						path = TrimGCodePath(path, targetDistance);
						// update the point count after trimming
						pointCount = path.Polygon.Count;
					}

					for (int i = 0; i < pointCount; i++)
					{
						long lineWidth_um = path.config.lineWidth_um;
						if (path.Polygon[i].Width != 0)
						{
							lineWidth_um = path.Polygon[i].Width;
						}

						gcodeExport.WriteMove(path.Polygon[i], speed, lineWidth_um);
					}

					if (trimmed)
					{
						// go back to the start of the loop
						gcodeExport.WriteMove(loopStart, speed, 0);

						var length = path.Polygon.PolygonLength();
						// retract while moving on down the perimeter
						//gcodeExport.WriteRetraction
						// then drive down it just a bit more to make sure we have a clean overlap
						//var extraMove = TrimGCodePath(path, perimeterStartEndOverlapRatio);
					}
				}
			}

			gcodeExport.UpdateTotalPrintTime();
		}

		private void ForceNewPathStart()
		{
			if (paths.Count > 0)
			{
				paths[paths.Count - 1].Done = true;
			}
		}

		private GCodePath GetLatestPathWithConfig(GCodePathConfig config)
		{
			if (paths.Count > 0
				&& paths[paths.Count - 1].config == config
				&& !paths[paths.Count - 1].Done)
			{
				return paths[paths.Count - 1];
			}

			var path = GetNewPath(config);
			return path;
		}

		private GCodePath GetNewPath(GCodePathConfig config)
		{
			GCodePath path = new GCodePath();
			paths.Add(path);
			path.Retract = RetractType.None;
			path.ExtruderIndex = currentExtruderIndex;
			path.Done = false;
			path.config = config;

			return path;
		}

		private void ValidatePaths()
		{
			bool first = true;
			IntPoint lastPosition = new IntPoint();
			for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
			{
				var path = paths[pathIndex];
				for (int polyIndex = 0; polyIndex < path.Polygon.Count; polyIndex++)
				{
					var position = path.Polygon[polyIndex];
					if (first)
					{
						first = false;
					}
					else
					{
						if (pathIndex == paths.Count - 1
							&& polyIndex == path.Polygon.Count - 1
							&& lastValidPathFinder != null
							&& !lastValidPathFinder.OutlineData.Polygons.PointIsInside((position + lastPosition) / 2))
						{
							// an easy way to get the path
							string startEndString = $"start:({position.X}, {position.Y}), end:({lastPosition.X}, {lastPosition.Y})";
							string outlineString = lastValidPathFinder.OutlineData.Polygons.WriteToString();
							long length = (position - lastPosition).Length();
						}
					}
					lastPosition = position;
				}
			}
		}
	}
}