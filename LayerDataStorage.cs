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

using ClipperLib;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using System.IO;
	using Polygons = List<List<IntPoint>>;

	public class LayerDataStorage
	{
		public List<ExtruderLayers> Extruders = new List<ExtruderLayers>();
		public Point3 modelSize, modelMin, modelMax;
		public Polygons raftOutline = new Polygons();
		public Polygons skirt = new Polygons();
		public NewSupport support = null;
		public IntPoint wipePoint;
		public List<Polygons> wipeShield = new List<Polygons>();
		public Polygons wipeTower = new Polygons();

		public void CreateIslandData()
		{
			for (int extruderIndex = 0; extruderIndex < Extruders.Count; extruderIndex++)
			{
				Extruders[extruderIndex].CreateIslandData();
			}
		}

		public void DumpLayerparts(string filename)
		{
			LayerDataStorage storage = this;
			StreamWriter streamToWriteTo = new StreamWriter(filename);
			streamToWriteTo.Write("<!DOCTYPE html><html><body>");
			Point3 modelSize = storage.modelSize;
			Point3 modelMin = storage.modelMin;

			for (int extruderIndex = 0; extruderIndex < storage.Extruders.Count; extruderIndex++)
			{
				for (int layerNr = 0; layerNr < storage.Extruders[extruderIndex].Layers.Count; layerNr++)
				{
					streamToWriteTo.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style=\"width: 500px; height:500px\">\n");
					SliceLayer layer = storage.Extruders[extruderIndex].Layers[layerNr];
					for (int i = 0; i < layer.Islands.Count; i++)
					{
						LayerIsland part = layer.Islands[i];
						for (int j = 0; j < part.IslandOutline.Count; j++)
						{
							streamToWriteTo.Write("<polygon points=\"");
							for (int k = 0; k < part.IslandOutline[j].Count; k++)
								streamToWriteTo.Write("{0},{1} ".FormatWith((float)(part.IslandOutline[j][k].X - modelMin.x) / modelSize.x * 500, (float)(part.IslandOutline[j][k].Y - modelMin.y) / modelSize.y * 500));
							if (j == 0)
								streamToWriteTo.Write("\" style=\"fill:gray; stroke:black;stroke-width:1\" />\n");
							else
								streamToWriteTo.Write("\" style=\"fill:red; stroke:black;stroke-width:1\" />\n");
						}
					}
					streamToWriteTo.Write("</svg>\n");
				}
			}
			streamToWriteTo.Write("</body></html>");
			streamToWriteTo.Close();
		}

		public void GenerateRaftOutlines(int extraDistanceAroundPart_um, ConfigSettings config)
		{
			LayerDataStorage storage = this;
			for (int extruderIndex = 0; extruderIndex < storage.Extruders.Count; extruderIndex++)
			{
				if (config.continuousSpiralOuterPerimeter && extruderIndex > 0)
				{
					continue;
				}

				if (storage.Extruders[extruderIndex].Layers.Count < 1)
				{
					continue;
				}

				SliceLayer layer = storage.Extruders[extruderIndex].Layers[0];
				// let's find the first layer that has something in it for the raft rather than a zero layer
				if (layer.Islands.Count == 0 && storage.Extruders[extruderIndex].Layers.Count > 2) layer = storage.Extruders[extruderIndex].Layers[1];
				for (int partIndex = 0; partIndex < layer.Islands.Count; partIndex++)
				{
					if (config.continuousSpiralOuterPerimeter && partIndex > 0)
					{
						continue;
					}

					storage.raftOutline = storage.raftOutline.CreateUnion(layer.Islands[partIndex].IslandOutline.Offset(extraDistanceAroundPart_um));
				}
			}

			storage.raftOutline = storage.raftOutline.CreateUnion(storage.wipeTower.Offset(extraDistanceAroundPart_um));
			if (storage.support != null)
			{
				storage.raftOutline = storage.raftOutline.CreateUnion(storage.support.GetBedOutlines().Offset(extraDistanceAroundPart_um));
			}
		}

		public void GenerateSkirt(int distance, int extrusionWidth_um, int numberOfLoops, int minLength, int initialLayerHeight, ConfigSettings config)
		{
			LayerDataStorage storage = this;
			bool externalOnly = (distance > 0);
			for (int skirtLoop = 0; skirtLoop < numberOfLoops; skirtLoop++)
			{
				int offsetDistance = distance + extrusionWidth_um * skirtLoop + extrusionWidth_um / 2;

				Polygons skirtPolygons = new Polygons(storage.wipeTower.Offset(offsetDistance));
				for (int extrudeIndex = 0; extrudeIndex < storage.Extruders.Count; extrudeIndex++)
				{
					if (config.continuousSpiralOuterPerimeter && extrudeIndex > 0)
					{
						continue;
					}

					if (storage.Extruders[extrudeIndex].Layers.Count < 1)
					{
						continue;
					}

					SliceLayer layer = storage.Extruders[extrudeIndex].Layers[0];
					for (int islandIndex = 0; islandIndex < layer.Islands.Count; islandIndex++)
					{
						if (config.continuousSpiralOuterPerimeter && islandIndex > 0)
						{
							continue;
						}

						if (externalOnly)
						{
							Polygons outline0 = new Polygons();
							outline0.Add(layer.Islands[islandIndex].IslandOutline[0]);
							//outline0.Add(layer.Islands[islandIndex].IslandOutline[0].CreateConvexHull());
							skirtPolygons = skirtPolygons.CreateUnion(outline0.Offset(offsetDistance));
						}
						else
						{
							skirtPolygons = skirtPolygons.CreateUnion(layer.Islands[islandIndex].IslandOutline.Offset(offsetDistance));
						}
					}
				}

				if (storage.support != null)
				{
					skirtPolygons = skirtPolygons.CreateUnion(storage.support.GetBedOutlines().Offset(offsetDistance));
				}

				//Remove small inner skirt holes. Holes have a negative area, remove anything smaller then 100x extrusion "area"
				for (int n = 0; n < skirtPolygons.Count; n++)
				{
					double area = skirtPolygons[n].Area();
					if (area < 0 && area > -extrusionWidth_um * extrusionWidth_um * 100)
					{
						skirtPolygons.RemoveAt(n--);
					}
				}

				storage.skirt.AddAll(skirtPolygons);

				int lenght = (int)storage.skirt.PolygonLength();
				if (skirtLoop + 1 >= numberOfLoops && lenght > 0 && lenght < minLength)
				{
					// add more loops for as long as we have not extruded enough length
					numberOfLoops++;
				}
			}
		}

		public void WriteRaftGCodeIfRequired(ConfigSettings config, GCodeExport gcode)
		{
			LayerDataStorage storage = this;
			if (config.ShouldGenerateRaft())
			{
				GCodePathConfig raftBaseConfig = new GCodePathConfig(config.firstLayerSpeed, config.raftBaseExtrusionWidth_um, "SUPPORT");
				GCodePathConfig raftMiddleConfig = new GCodePathConfig(config.raftPrintSpeed, config.raftInterfaceExtrusionWidth_um, "SUPPORT");
				GCodePathConfig raftSurfaceConfig = new GCodePathConfig((config.raftSurfacePrintSpeed > 0) ? config.raftSurfacePrintSpeed : config.raftPrintSpeed, config.raftSurfaceExtrusionWidth_um, "SUPPORT");

				// create the raft base
				{
					gcode.WriteComment("LAYER:-3");
					gcode.WriteComment("RAFT BASE");
					GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_um);
					if (config.raftExtruder >= 0)
					{
						// if we have a specified raft extruder use it
						gcodeLayer.SetExtruder(config.raftExtruder);
					}
					else if (config.supportExtruder >= 0)
					{
						// else preserve the old behavior of using the support extruder if set.
						gcodeLayer.SetExtruder(config.supportExtruder);
					}

					gcode.setZ(config.raftBaseThickness_um);
					gcode.SetExtrusion(config.raftBaseThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);

					Polygons raftLines = new Polygons();
					Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftBaseLineSpacing_um, config.infillExtendIntoPerimeter_um, 0);

					// write the skirt around the raft
					gcodeLayer.QueuePolygonsByOptimizer(storage.skirt, raftBaseConfig);

					// write the outline of the raft
					gcodeLayer.QueuePolygonsByOptimizer(storage.raftOutline, raftBaseConfig);

					// write the inside of the raft base
					gcodeLayer.QueuePolygonsByOptimizer(raftLines, raftBaseConfig);

					gcodeLayer.WriteQueuedGCode(config.raftBaseThickness_um);
				}

				if (config.raftFanSpeedPercent > 0)
				{
					gcode.WriteFanCommand(config.raftFanSpeedPercent);
				}

				// raft middle layers
				{
					gcode.WriteComment("LAYER:-2");
					gcode.WriteComment("RAFT MIDDLE");
					GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_um);
					gcode.setZ(config.raftBaseThickness_um + config.raftInterfaceThicknes_um);
					gcode.SetExtrusion(config.raftInterfaceThicknes_um, config.filamentDiameter_um, config.extrusionMultiplier);

					Polygons raftLines = new Polygons();
					Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftInterfaceLineSpacing_um, config.infillExtendIntoPerimeter_um, 45);
					gcodeLayer.QueuePolygonsByOptimizer(raftLines, raftMiddleConfig);

					gcodeLayer.WriteQueuedGCode(config.raftInterfaceThicknes_um);
				}

				for (int raftSurfaceIndex = 1; raftSurfaceIndex <= config.raftSurfaceLayers; raftSurfaceIndex++)
				{
					gcode.WriteComment("LAYER:-1");
					gcode.WriteComment("RAFT SURFACE");
					GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.travelSpeed, config.minimumTravelToCauseRetraction_um);
					gcode.setZ(config.raftBaseThickness_um + config.raftInterfaceThicknes_um + config.raftSurfaceThickness_um * raftSurfaceIndex);
					gcode.SetExtrusion(config.raftSurfaceThickness_um, config.filamentDiameter_um, config.extrusionMultiplier);

					Polygons raftLines = new Polygons();
					if (raftSurfaceIndex == config.raftSurfaceLayers)
					{
						// make sure the top layer of the raft is 90 degrees offset to the first layer of the part so that it has minimum contact points.
						Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftSurfaceLineSpacing_um, config.infillExtendIntoPerimeter_um, config.infillStartingAngle + 90);
					}
					else
					{
						Infill.GenerateLinePaths(storage.raftOutline, ref raftLines, config.raftSurfaceLineSpacing_um, config.infillExtendIntoPerimeter_um, 90 * raftSurfaceIndex);
					}
					gcodeLayer.QueuePolygonsByOptimizer(raftLines, raftSurfaceConfig);

					gcodeLayer.WriteQueuedGCode(config.raftInterfaceThicknes_um);
				}
			}
		}
	}
}