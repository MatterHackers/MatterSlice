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

namespace MatterHackers.MatterSlice
{
	using System;
	using System.IO;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class LayerDataStorage
	{
		public List<ExtruderLayers> Extruders = new List<ExtruderLayers>();
		public IntPoint modelSize, modelMin, modelMax;
		public Polygons raftOutline = new Polygons();
		public Polygons skirt = new Polygons();
		public NewSupport support = null;
		public IntPoint wipePoint;
		public List<Polygons> wipeShield = new List<Polygons>();
		public Polygons wipeTower = new Polygons();

		private bool[] extrudersThatHaveBeenPrimed = null;

		public void CreateIslandData()
		{
			for (int extruderIndex = 0; extruderIndex < Extruders.Count; extruderIndex++)
			{
				Extruders[extruderIndex].CreateIslandData();
			}
		}

		public void CreateWipeShield(int totalLayers, ConfigSettings config)
		{
			if (config.WipeShieldDistanceFromShapes_um <= 0)
			{
				return;
			}

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				Polygons wipeShield = new Polygons();
				for (int extruderIndex = 0; extruderIndex < this.Extruders.Count; extruderIndex++)
				{
					for (int islandIndex = 0; islandIndex < this.Extruders[extruderIndex].Layers[layerIndex].Islands.Count; islandIndex++)
					{
						wipeShield = wipeShield.CreateUnion(this.Extruders[extruderIndex].Layers[layerIndex].Islands[islandIndex].IslandOutline.Offset(config.WipeShieldDistanceFromShapes_um));
					}
				}
				this.wipeShield.Add(wipeShield);
			}

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				this.wipeShield[layerIndex] = this.wipeShield[layerIndex].Offset(-1000).Offset(1000);
			}

			int offsetAngle = (int)Math.Tan(60.0 * Math.PI / 180) * config.LayerThickness_um;//Allow for a 60deg angle in the wipeShield.
			for (int layerIndex = 1; layerIndex < totalLayers; layerIndex++)
			{
				this.wipeShield[layerIndex] = this.wipeShield[layerIndex].CreateUnion(this.wipeShield[layerIndex - 1].Offset(-offsetAngle));
			}

			for (int layerIndex = totalLayers - 1; layerIndex > 0; layerIndex--)
			{
				this.wipeShield[layerIndex - 1] = this.wipeShield[layerIndex - 1].CreateUnion(this.wipeShield[layerIndex].Offset(-offsetAngle));
			}
		}

		public void CreateWipeTower(int totalLayers, ConfigSettings config)
		{
			if (config.WipeTowerSize_um < 1
				|| LastLayerWithChange(config) == -1)
			{
				return;
			}

			extrudersThatHaveBeenPrimed = new bool[config.MaxExtruderCount()];

			Polygon wipeTowerShape = new Polygon();
			wipeTowerShape.Add(new IntPoint(this.modelMin.X - 3000, this.modelMax.Y + 3000));
			wipeTowerShape.Add(new IntPoint(this.modelMin.X - 3000, this.modelMax.Y + 3000 + config.WipeTowerSize_um));
			wipeTowerShape.Add(new IntPoint(this.modelMin.X - 3000 - config.WipeTowerSize_um, this.modelMax.Y + 3000 + config.WipeTowerSize_um));
			wipeTowerShape.Add(new IntPoint(this.modelMin.X - 3000 - config.WipeTowerSize_um, this.modelMax.Y + 3000));

			this.wipeTower.Add(wipeTowerShape);
			this.wipePoint = new IntPoint(this.modelMin.X - 3000 - config.WipeTowerSize_um / 2, this.modelMax.Y + 3000 + config.WipeTowerSize_um / 2);
		}

		public void DumpLayerparts(string filename)
		{
			LayerDataStorage storage = this;
			StreamWriter streamToWriteTo = new StreamWriter(filename);
			streamToWriteTo.Write("<!DOCTYPE html><html><body>");
			IntPoint modelSize = storage.modelSize;
			IntPoint modelMin = storage.modelMin;

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
								streamToWriteTo.Write("{0},{1} ".FormatWith((float)(part.IslandOutline[j][k].X - modelMin.X) / modelSize.X * 500, (float)(part.IslandOutline[j][k].Y - modelMin.Y) / modelSize.Y * 500));
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

		public void EnsureWipeTowerIsSolid(int layerIndex, GCodePlanner gcodeLayer, GCodePathConfig fillConfig, ConfigSettings config)
		{
			if (layerIndex >= LastLayerWithChange(config)
				|| extrudersThatHaveBeenPrimed == null)
			{
				return;
			}

			// print all of the extruder loops that have not already been printed
			for (int extruderIndex = 0; extruderIndex < config.MaxExtruderCount(); extruderIndex++)
			{
				if (!extrudersThatHaveBeenPrimed[extruderIndex])
				{
					// write the loops for this extruder, but don't change to it. We are just filling the prime tower.
					PrimeOnWipeTower(extruderIndex, 0, gcodeLayer, fillConfig, config);
				}

				// clear the history of printer extruders for the next layer
				extrudersThatHaveBeenPrimed[extruderIndex] = false;
			}
		}

		public void GenerateRaftOutlines(int extraDistanceAroundPart_um, ConfigSettings config)
		{
			LayerDataStorage storage = this;
			for (int extruderIndex = 0; extruderIndex < storage.Extruders.Count; extruderIndex++)
			{
				if (config.ContinuousSpiralOuterPerimeter && extruderIndex > 0)
				{
					continue;
				}

				if (storage.Extruders[extruderIndex].Layers.Count < 1)
				{
					continue;
				}

				SliceLayer layer = storage.Extruders[extruderIndex].Layers[0];
				// let's find the first layer that has something in it for the raft rather than a zero layer
				if (layer.Islands.Count == 0 && storage.Extruders[extruderIndex].Layers.Count > 2)
				{
					layer = storage.Extruders[extruderIndex].Layers[1];
				}

				for (int partIndex = 0; partIndex < layer.Islands.Count; partIndex++)
				{
					if (config.ContinuousSpiralOuterPerimeter && partIndex > 0)
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

		public void GenerateSkirt(int distance, int extrusionWidth_um, int numberOfLoops, int brimCount, int minLength, ConfigSettings config)
		{
			LayerDataStorage storage = this;
			bool externalOnly = (distance > 0);

			List<Polygons> skirtLoops = new List<Polygons>();

			Polygons skirtPolygons = GetSkirtBounds(config, storage, externalOnly, distance, extrusionWidth_um, brimCount);

			if (skirtPolygons.Count > 0)
			{

				// Find convex hull for the skirt outline
				Polygons convexHull = new Polygons(new[] { skirtPolygons.CreateConvexHull() });

				// Create skirt loops from the ConvexHull
				for (int skirtLoop = 0; skirtLoop < numberOfLoops; skirtLoop++)
				{
					int offsetDistance = distance + extrusionWidth_um * skirtLoop + extrusionWidth_um / 2;

					storage.skirt.AddAll(convexHull.Offset(offsetDistance));

					int length = (int)storage.skirt.PolygonLength();
					if (skirtLoop + 1 >= numberOfLoops && length > 0 && length < minLength)
					{
						// add more loops for as long as we have not extruded enough length
						numberOfLoops++;
					}
				}
			}
		}

		public void GenerateWipeTowerInfill(int extruderIndex, Polygons partOutline, Polygons outputfillPolygons, long extrusionWidth_um, ConfigSettings config)
		{
			Polygons outlineForExtruder = partOutline.Offset(-extrusionWidth_um * extruderIndex);

			long insetPerLoop = extrusionWidth_um * config.MaxExtruderCount();
			while (outlineForExtruder.Count > 0)
			{
				for (int polygonIndex = 0; polygonIndex < outlineForExtruder.Count; polygonIndex++)
				{
					Polygon newInset = outlineForExtruder[polygonIndex];
					newInset.Add(newInset[0]); // add in the last move so it is a solid polygon
					outputfillPolygons.Add(newInset);
				}
				outlineForExtruder = outlineForExtruder.Offset(-insetPerLoop);
			}

			outputfillPolygons.Reverse();
		}

		public bool HaveWipeTower(ConfigSettings config)
		{
			if (extrudersThatHaveBeenPrimed == null
				 || config.WipeTowerSize_um == 0
				 || LastLayerWithChange(config) == -1)
			{
				return false;
			}

			return true;
		}

		public void PrimeOnWipeTower(int extruderIndex, int layerIndex, GCodePlanner gcodeLayer, GCodePathConfig fillConfig, ConfigSettings config)
		{
			if (!HaveWipeTower(config)
				|| layerIndex > LastLayerWithChange(config) + 1)
			{
				return;
			}

			//If we changed extruder, print the wipe/prime tower for this nozzle;
			Polygons fillPolygons = new Polygons();
			GenerateWipeTowerInfill(extruderIndex, this.wipeTower, fillPolygons, fillConfig.lineWidth_um, config);
			gcodeLayer.QueuePolygons(fillPolygons, fillConfig);

			extrudersThatHaveBeenPrimed[extruderIndex] = true;
		}

		public void WriteRaftGCodeIfRequired(GCodeExport gcode, ConfigSettings config)
		{
			LayerDataStorage storage = this;
			if (config.ShouldGenerateRaft())
			{
				GCodePathConfig raftBaseConfig = new GCodePathConfig("raftBaseConfig");
				raftBaseConfig.SetData(config.FirstLayerSpeed, config.RaftBaseExtrusionWidth_um, "SUPPORT");

				GCodePathConfig raftMiddleConfig = new GCodePathConfig("raftMiddleConfig");
				raftMiddleConfig.SetData(config.RaftPrintSpeed, config.RaftInterfaceExtrusionWidth_um, "SUPPORT");

				GCodePathConfig raftSurfaceConfig = new GCodePathConfig("raftMiddleConfig");
				raftSurfaceConfig.SetData((config.RaftSurfacePrintSpeed > 0) ? config.RaftSurfacePrintSpeed : config.RaftPrintSpeed, config.RaftSurfaceExtrusionWidth_um, "SUPPORT");

				// create the raft base
				{
					gcode.WriteComment("RAFT BASE");
					GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
					if (config.RaftExtruder >= 0)
					{
						// if we have a specified raft extruder use it
						gcodeLayer.SetExtruder(config.RaftExtruder);
					}
					else if (config.SupportExtruder >= 0)
					{
						// else preserve the old behavior of using the support extruder if set.
						gcodeLayer.SetExtruder(config.SupportExtruder);
					}

					gcode.SetZ(config.RaftBaseThickness_um);

					gcode.LayerChanged(-3);

					gcode.SetExtrusion(config.RaftBaseThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);

					// write the skirt around the raft
					gcodeLayer.QueuePolygonsByOptimizer(storage.skirt, raftBaseConfig);

					List<Polygons> raftIslands = storage.raftOutline.ProcessIntoSeparatIslands();
					foreach (var raftIsland in raftIslands)
					{
						// write the outline of the raft
						gcodeLayer.QueuePolygonsByOptimizer(raftIsland, raftBaseConfig);

						Polygons raftLines = new Polygons();
						Infill.GenerateLinePaths(raftIsland.Offset(-config.RaftBaseExtrusionWidth_um) , raftLines, config.RaftBaseLineSpacing_um, config.InfillExtendIntoPerimeter_um, 0);

						// write the inside of the raft base
						gcodeLayer.QueuePolygonsByOptimizer(raftLines, raftBaseConfig);

						if (config.RetractWhenChangingIslands)
						{
							gcodeLayer.ForceRetract();
						}
					}

					gcodeLayer.WriteQueuedGCode(config.RaftBaseThickness_um);
				}

				if (config.RaftFanSpeedPercent > 0)
				{
					gcode.WriteFanCommand(config.RaftFanSpeedPercent);
				}

				// raft middle layers
				{
					gcode.WriteComment("RAFT MIDDLE");
					GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
					gcode.SetZ(config.RaftBaseThickness_um + config.RaftInterfaceThicknes_um);
					gcode.LayerChanged(-2);
					gcode.SetExtrusion(config.RaftInterfaceThicknes_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);

					Polygons raftLines = new Polygons();
					Infill.GenerateLinePaths(storage.raftOutline, raftLines, config.RaftInterfaceLineSpacing_um, config.InfillExtendIntoPerimeter_um, 45);
					gcodeLayer.QueuePolygonsByOptimizer(raftLines, raftMiddleConfig);

					gcodeLayer.WriteQueuedGCode(config.RaftInterfaceThicknes_um);
				}

				for (int raftSurfaceIndex = 1; raftSurfaceIndex <= config.RaftSurfaceLayers; raftSurfaceIndex++)
				{
					gcode.WriteComment("RAFT SURFACE");
					GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
					gcode.SetZ(config.RaftBaseThickness_um + config.RaftInterfaceThicknes_um + config.RaftSurfaceThickness_um * raftSurfaceIndex);
					gcode.LayerChanged(-1);
					gcode.SetExtrusion(config.RaftSurfaceThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);

					Polygons raftLines = new Polygons();
					if (raftSurfaceIndex == config.RaftSurfaceLayers)
					{
						// make sure the top layer of the raft is 90 degrees offset to the first layer of the part so that it has minimum contact points.
						Infill.GenerateLinePaths(storage.raftOutline, raftLines, config.RaftSurfaceLineSpacing_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle + 90);
					}
					else
					{
						Infill.GenerateLinePaths(storage.raftOutline, raftLines, config.RaftSurfaceLineSpacing_um, config.InfillExtendIntoPerimeter_um, 90 * raftSurfaceIndex);
					}
					gcodeLayer.QueuePolygonsByOptimizer(raftLines, raftSurfaceConfig);

					gcodeLayer.WriteQueuedGCode(config.RaftInterfaceThicknes_um);
				}
			}
		}

		private static Polygons GetSkirtBounds(ConfigSettings config, LayerDataStorage storage, bool externalOnly, int distance, int extrusionWidth_um, int brimCount)
		{
			bool hasWipeTower = storage.wipeTower.PolygonLength() > 0;

			Polygons skirtPolygons = new Polygons();

			if (config.EnableRaft)
			{
				skirtPolygons = skirtPolygons.CreateUnion(storage.raftOutline);
			}
			else
			{
				Polygons allOutlines = hasWipeTower ? new Polygons(storage.wipeTower) : new Polygons();

				// Loop over every extruder
				for (int extrudeIndex = 0; extrudeIndex < storage.Extruders.Count; extrudeIndex++)
				{
					// Only process the first extruder on spiral vase or
					// skip extruders that have empty layers
					if (config.ContinuousSpiralOuterPerimeter)
					{
						SliceLayer layer0 = storage.Extruders[extrudeIndex].Layers[0];
						allOutlines.AddAll(layer0.Islands[0]?.IslandOutline);
					}
					else
					{
						// Add the layers outline to allOutlines
						SliceLayer layer = storage.Extruders[extrudeIndex].Layers[0];
						foreach(var island in layer.Islands)
						{
							if (island.IslandOutline?.Count > 0)
							{
								allOutlines.Add(island.IslandOutline[0]);
							}
						}
					}
				}

				if (brimCount > 0)
				{
					Polygons unionedIslandOutlines = new Polygons();

					// Grow each island by the current brim distance
					// Union the island brims
					unionedIslandOutlines = unionedIslandOutlines.CreateUnion(allOutlines);

					if (storage.support != null)
					{
						unionedIslandOutlines = unionedIslandOutlines.CreateUnion(storage.support.GetBedOutlines());
					}

					Polygons brimLoops = new Polygons();

					// Loop over the requested brimCount creating and unioning a new perimeter for each island
					for (int brimIndex = 0; brimIndex < brimCount; brimIndex++)
					{
						int offsetDistance = extrusionWidth_um * brimIndex + extrusionWidth_um / 2;

						// Extend the polygons to account for the brim (ensures convex hull takes this data into account)
						brimLoops.AddAll(unionedIslandOutlines.Offset(offsetDistance));
					}

					// TODO: This is a quick hack, reuse the skirt data to stuff in the brim. Good enough from proof of concept
					storage.skirt.AddAll(brimLoops);

					skirtPolygons = skirtPolygons.CreateUnion(brimLoops);
				}
				else
				{
					skirtPolygons = skirtPolygons.CreateUnion(allOutlines);
				}

				if (storage.support != null)
				{
					skirtPolygons = skirtPolygons.CreateUnion(storage.support.GetBedOutlines());
				}
			}

			return skirtPolygons;
		}

		public int LastLayerWithChange(ConfigSettings config)
		{
			int numLayers = Extruders[0].Layers.Count;
			int firstExtruderWithData = -1;
			for (int checkLayer = numLayers - 1; checkLayer >= 0; checkLayer--)
			{
				for (int extruderToCheck = 0; extruderToCheck < config.MaxExtruderCount(); extruderToCheck++)
				{
					if ((extruderToCheck < Extruders.Count && Extruders[extruderToCheck].Layers[checkLayer].AllOutlines.Count > 0)
						|| (config.SupportExtruder == extruderToCheck && support != null && support.HasNormalSupport(checkLayer))
						|| (config.SupportInterfaceExtruder == extruderToCheck && support != null && support.HasInterfaceSupport(checkLayer)))
					{
						if (firstExtruderWithData == -1)
						{
							firstExtruderWithData = extruderToCheck;
						}
						else
						{
							if (firstExtruderWithData != extruderToCheck)
							{
								return checkLayer;
							}
						}
					}
				}
			}

			return -1;
		}
	}
}