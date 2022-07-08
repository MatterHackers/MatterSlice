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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Pathfinding;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
    public class LayerDataStorage
	{
		private int lastLayerWithChange = -1;
		private bool calculatedLastLayer = false;

		public List<ExtruderLayers> Extruders = new List<ExtruderLayers>();
		public IntPoint modelSize, modelMin, modelMax;
		public Polygons raftOutline = new Polygons();

		public Polygons Skirt { get; private set; } = new Polygons();

		public Polygons Brims { get; private set; } = new Polygons();

		public SupportLayers Support = null;

		public List<Polygons> WipeShield { get; set; } = new List<Polygons>();

		public IntPoint WipeCenter_um { get; private set; }

		public List<Polygons> WipeTower { get; private set; } = new List<Polygons>();

		public List<Polygons> FuzzyLayerBounds { get; private set; } = new List<Polygons>();

		public Polygons WipeLayer(int layerIndex)
		{
			if (WipeTower.Count == 0)
			{
				WipeTower.Add(new Polygons());
			}

			if (layerIndex < WipeTower.Count)
			{
				return WipeTower[layerIndex];
			}

			return WipeTower[WipeTower.Count - 1];
		}

		private int primesThisLayer = 0;

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
				var wipeShield = new Polygons();
				for (int extruderIndex = 0; extruderIndex < this.Extruders.Count; extruderIndex++)
				{
					wipeShield = wipeShield.CreateUnion(this.Extruders[extruderIndex].Layers[layerIndex].AllOutlines.Offset(config.WipeShieldDistanceFromShapes_um));

					void AddSupportLayer(List<Polygons> add)
					{
						if (add != null
							&& layerIndex < add.Count
							&& add[layerIndex] != null)
						{
							wipeShield = wipeShield.CreateUnion(add[layerIndex].Offset(config.WipeShieldDistanceFromShapes_um));
						}
					}

					if (this.Support != null)
					{
						AddSupportLayer(this.Support.AirGappedBottomOutlines);
						AddSupportLayer(this.Support.InterfaceLayers);
						AddSupportLayer(this.Support.SparseSupportOutlines);
					}
				}

				this.WipeShield.Add(wipeShield);
			}

			for (int layerIndex = 0; layerIndex < totalLayers; layerIndex++)
			{
				// get rid of thin sections by reducing than expanding back the outlines
				this.WipeShield[layerIndex] = this.WipeShield[layerIndex].Offset(-1000).Offset(1000);
			}

			long offsetAngle_um = (long)(Math.Tan(60.0 * Math.PI / 180) * config.LayerThickness_um); // Allow for a 60deg angle in the wipeShield.
			for (int layerIndex = 1; layerIndex < totalLayers; layerIndex++)
			{
				this.WipeShield[layerIndex] = this.WipeShield[layerIndex].CreateUnion(this.WipeShield[layerIndex - 1].Offset(-offsetAngle_um));
			}

			for (int layerIndex = totalLayers - 1; layerIndex > 0; layerIndex--)
			{
				this.WipeShield[layerIndex - 1] = this.WipeShield[layerIndex - 1].CreateUnion(this.WipeShield[layerIndex].Offset(-offsetAngle_um));
			}
		}

		public void CreateRequiredInsets(ConfigSettings config, int outputLayerIndex, int extruderIndex)
		{
			if (extruderIndex < this.Extruders.Count)
			{
				var startIndex = Math.Max(0, outputLayerIndex - config.NumberOfBottomLayers - 1);
				// figure out how many layers we need to calculate
				var endIndex = outputLayerIndex + config.NumberOfTopLayers + 1;
				var threadExtra = 10;
				endIndex = (endIndex / threadExtra) * threadExtra + threadExtra;

				// now bias more in to help multi threading
				// and clamp to the number there are to make sure we can do it
				endIndex = Math.Min(endIndex, this.Extruders[extruderIndex].Layers.Count);

				// free up the insets from the previous layer
				if (startIndex > config.NumberOfBottomLayers + 1)
				{
					SliceLayer previousLayer = this.Extruders[extruderIndex].Layers[startIndex - 2];
					previousLayer.FreeIslandMemory();
				}

				using (new QuickTimer2Report("GenerateInsets"))
				{
					for (int layerIndex = startIndex; layerIndex < endIndex; layerIndex++)
					{
						SliceLayer layer = this.Extruders[extruderIndex].Layers[layerIndex];

						if (layer.Islands.Count > 0
							&& !layer.CreatedInsets)
						{
							layer.CreatedInsets = true;
							int insetCount = config.GetNumberOfPerimeters();
							if (config.ContinuousSpiralOuterPerimeter && (int)layerIndex < config.NumberOfBottomLayers && layerIndex % 2 == 1)
							{
								// Add extra insets every 2 layers when spiralizing, this makes bottoms of cups watertight.
								insetCount += 1;
							}

							Polygons fuzzyBounds = null;
							if (layerIndex > 0
								&& FuzzyLayerBounds != null
								&& FuzzyLayerBounds.Count > layerIndex)
							{
								fuzzyBounds = FuzzyLayerBounds[layerIndex];
							}

							if (layerIndex == 0)
							{
								layer.GenerateInsets(config, fuzzyBounds, config.FirstLayerExtrusionWidth_um, config.FirstLayerExtrusionWidth_um, insetCount);
							}
							else
							{
								layer.GenerateInsets(config, fuzzyBounds, config.ExtrusionWidth_um, config.OutsideExtrusionWidth_um, insetCount);
							}
						}
					}
				}

				using (new QuickTimer2Report("GenerateTopAndBottoms"))
				{
					// Only generate bottom and top layers and infill for the first X layers when spiralize is chosen.
					if (!config.ContinuousSpiralOuterPerimeter || (int)outputLayerIndex < config.NumberOfBottomLayers)
					{
						if (outputLayerIndex == 0)
						{
							this.Extruders[extruderIndex].GenerateTopAndBottoms(config, outputLayerIndex, config.FirstLayerExtrusionWidth_um, config.FirstLayerExtrusionWidth_um, config.NumberOfBottomLayers, config.NumberOfTopLayers, config.InfillExtendIntoPerimeter_um);
						}
						else
						{
							this.Extruders[extruderIndex].GenerateTopAndBottoms(config, outputLayerIndex, config.ExtrusionWidth_um, config.OutsideExtrusionWidth_um, config.NumberOfBottomLayers, config.NumberOfTopLayers, config.InfillExtendIntoPerimeter_um);
						}
					}
				}
			}
		}

		public void CalculateInfillData(ConfigSettings config,
			int extruderIndex,
			int layerIndex,
			LayerIsland part,
			Polygons bottomFillLines,
			Polygons sparseFillPolygons = null,
			Polygons solidFillPolygons = null,
			Polygons firstTopFillPolygons = null,
			Polygons topFillPolygons = null,
			Polygons bridgePolygons = null,
			Polygons bridgeAreas = null)
		{
			double alternatingInfillAngle = config.InfillStartingAngle;
			if ((layerIndex % 2) == 0)
			{
				alternatingInfillAngle += 90;
			}

			// generate infill for the bottom layer including bridging
			foreach (Polygons bottomFillIsland in part.BottomPaths.ProcessIntoSeparateIslands())
			{
				if (layerIndex > 0)
				{
					if (this.Support != null)
					{
						double infillAngle = config.SupportInterfaceLayers > 0 ? config.InfillStartingAngle : config.InfillStartingAngle + 90;
						Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, infillAngle);
					}
					else
					{
						SliceLayer previousLayer = this.Extruders[extruderIndex].Layers[layerIndex - 1];

						if (bridgePolygons != null
							&& previousLayer.BridgeAngle(bottomFillIsland, config.GetNumberOfPerimeters() * config.ExtrusionWidth_um, out double bridgeAngle, bridgeAreas))
						{
							// TODO: Make this code handle very complex pathing between different sizes or layouts of support under the island to fill.
							Infill.GenerateLinePaths(bottomFillIsland, bridgePolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, bridgeAngle);
						}
						else // we still need to extrude at bridging speed
						{
							Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle, 0, config.BridgeSpeed);
						}
					}
				}
				else
				{
					Infill.GenerateLinePaths(bottomFillIsland, bottomFillLines, config.FirstLayerExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle);
				}
			}

			// generate infill for the top most layer
			if (topFillPolygons != null)
			{
				foreach (Polygons outline in part.TopPaths.ProcessIntoSeparateIslands())
				{
					// the top layer always draws the infill in the same direction (for aesthetics)
					Infill.GenerateLinePaths(outline, topFillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle);
				}
			}

			// generate infill for the top layers (but not the top most)
			if (firstTopFillPolygons != null)
			{
				foreach (Polygons outline in part.FirstTopPaths.ProcessIntoSeparateIslands())
				{
					Infill.GenerateLinePaths(outline, firstTopFillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle);
				}
			}

			// generate infill intermediate layers
			if (solidFillPolygons != null)
			{
				foreach (Polygons outline in part.SolidInfillPaths.ProcessIntoSeparateIslands())
				{
					Infill.GenerateLinePaths(outline, solidFillPolygons, config.ExtrusionWidth_um, config.InfillExtendIntoPerimeter_um, alternatingInfillAngle);
				}
			}

			// generate infill intermediate layers
			if (sparseFillPolygons != null)
			{
				// generate the sparse infill for this part on this layer
				if (config.InfillPercent > 0)
				{
					switch (config.InfillType)
					{
						case INFILL_TYPE.LINES:
							Infill.GenerateLineInfill(config, part.SparseInfillPaths, sparseFillPolygons, alternatingInfillAngle);
							break;

						case INFILL_TYPE.GRID:
							Infill.GenerateGridInfill(config, part.SparseInfillPaths, sparseFillPolygons, config.InfillStartingAngle);
							break;

						case INFILL_TYPE.TRIANGLES:
							Infill.GenerateTriangleInfill(config, part.SparseInfillPaths, sparseFillPolygons, config.InfillStartingAngle);
							break;

						case INFILL_TYPE.GYROID:
							GyroidInfill.Generate(config, part.SparseInfillPaths, sparseFillPolygons, true, layerIndex);
							break;

						case INFILL_TYPE.HEXAGON:
							Infill.GenerateHexagonInfill(config, part.SparseInfillPaths, sparseFillPolygons, config.InfillStartingAngle, layerIndex);
							break;

						case INFILL_TYPE.CONCENTRIC:
							Infill.GenerateConcentricInfill(config, part.SparseInfillPaths, sparseFillPolygons);
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
		}

		public void CreateWipeTower(ConfigSettings config, ExtruderLayers wipeTowerLayers)
		{
			if (wipeTowerLayers != null
				&& wipeTowerLayers.Layers.Count > 0
				&& wipeTowerLayers.Layers[0].AllOutlines.Count > 0)
			{
				for (int i = 0; i < wipeTowerLayers.Layers.Count; i++)
				{
					var layer = wipeTowerLayers.Layers[i];

					if (layer.AllOutlines.PolygonLength() > 0)
					{
						this.WipeTower.Add(layer.AllOutlines);
					}
					else
					{
						this.WipeTower.Add(this.WipeTower[i - 1]);
					}
				}
			}
			else if (config.WipeTowerSize_um < 1
				|| LastLayerWithChange(config) == -1)
			{
				return;
			}
			else
			{
				var wipeTowerShape = new Polygon();

				var size = config.WipeTowerSize_um;
				WipeCenter_um = new IntPoint(this.modelMin.X - 3000 - size / 2,
					this.modelMax.Y + 3000 + size / 2);

				var points = 100;
				for (int i = 0; i < points; i++)
				{
					var angle = Math.PI * 2 * i / points;
					wipeTowerShape.Add(WipeCenter_um + new IntPoint(Math.Cos(angle) * size / 2, Math.Sin(angle) * size / 2));
				}

				this.WipeTower.Add(new Polygons() { wipeTowerShape });
			}

			var wipeTowerBounds = this.WipeTower[0].GetBounds();

			WipeCenter_um = new IntPoint(
				wipeTowerBounds.minX + (wipeTowerBounds.maxX - wipeTowerBounds.minX) / 2,
				wipeTowerBounds.minY + (wipeTowerBounds.maxY - wipeTowerBounds.minY) / 2);
		}

		public void CreateFuzzyBoundaries(ConfigSettings config, ExtruderLayers fuzzyLayers)
		{
			if (fuzzyLayers != null
				&& fuzzyLayers.Layers.Count > 0
				&& fuzzyLayers.Layers.Any(l => l.AllOutlines.Count > 0))
			{
				for (int i = 0; i < fuzzyLayers.Layers.Count; i++)
				{
					this.FuzzyLayerBounds.Add(fuzzyLayers.Layers[i].AllOutlines);
				}
			}
		}

		public void DumpLayerparts(string filename)
		{
			var streamToWriteTo = new StreamWriter(filename);
			streamToWriteTo.Write("<!DOCTYPE html><html><body>");

			for (int extruderIndex = 0; extruderIndex < this.Extruders.Count; extruderIndex++)
			{
				for (int layerNr = 0; layerNr < this.Extruders[extruderIndex].Layers.Count; layerNr++)
				{
					streamToWriteTo.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style=\"width: 500px; height:500px\">\n");
					SliceLayer layer = this.Extruders[extruderIndex].Layers[layerNr];
					for (int i = 0; i < layer.Islands.Count; i++)
					{
						LayerIsland part = layer.Islands[i];
						for (int j = 0; j < part.IslandOutline.Count; j++)
						{
							streamToWriteTo.Write("<polygon points=\"");

							for (int k = 0; k < part.IslandOutline[j].Count; k++)
							{
								streamToWriteTo.Write("{0},{1} ".FormatWith((float)(part.IslandOutline[j][k].X - modelMin.X) / modelSize.X * 500, (float)(part.IslandOutline[j][k].Y - modelMin.Y) / modelSize.Y * 500));
							}

							if (j == 0)
							{
								streamToWriteTo.Write("\" style=\"fill:gray; stroke:black;stroke-width:1\" />\n");
							}
							else
							{
								streamToWriteTo.Write("\" style=\"fill:red; stroke:black;stroke-width:1\" />\n");
							}
						}
					}

					streamToWriteTo.Write("</svg>\n");
				}
			}

			streamToWriteTo.Write("</body></html>");
			streamToWriteTo.Close();
		}

		[Conditional("DEBUG")]
		private void CheckNoExtruderPrimed(ConfigSettings config)
		{
			if (primesThisLayer > 0)
			{
				throw new Exception("No extruders should be primed");
			}
		}

		public bool EnsureWipeTowerIsSolid(int layerIndex, PathFinder pathFinder, LayerGCodePlanner layerGcodePlanner, GCodePathConfig fillConfig, ConfigSettings config)
		{
			if (layerIndex >= LastLayerWithChange(config))
			{
				return false;
			}

			// If layer index == 0 do all the loops from the outside-in, in order (no lines should be in the wipe tower)
			if (layerIndex == 0 && !config.EnableRaft)
			{
				CheckNoExtruderPrimed(config);

				long insetPerLoop = fillConfig.LineWidth_um;

				Polygons outlineForExtruder = this.WipeLayer(layerIndex);

				var fillPolygons = new Polygons();
				while (outlineForExtruder.Count > 0)
				{
					for (int polygonIndex = 0; polygonIndex < outlineForExtruder.Count; polygonIndex++)
					{
						Polygon newInset = outlineForExtruder[polygonIndex];
						fillPolygons.Add(newInset);
					}

					outlineForExtruder = outlineForExtruder.Offset(-insetPerLoop);
				}

				// set the path planner to avoid islands
				if (this.HaveWipeTower(config, layerIndex))
				{
					layerGcodePlanner.QueueTravel(WipeCenter_um, pathFinder, fillConfig.LiftOnTravel);
				}

				// turn off the planner for the wipe tower
				layerGcodePlanner.QueueWipeTowerPolygons(fillPolygons, fillConfig);
			}
			else
			{
				// print all of the extruder loops that have not already been printed
				int maxPrimingLoops = MaxPrimingLoops(config);

				for (int primeLoop = primesThisLayer; primeLoop < maxPrimingLoops; primeLoop++)
				{
					// write the loops for this extruder, but don't change to it. We are just filling the prime tower.
					PrimeOnWipeTower(layerIndex, layerGcodePlanner, pathFinder, fillConfig, config, false);
				}
			}

			// clear the history of printer extruders for the next layer
			primesThisLayer = 0;

			return true;
		}

		public void GenerateRaftOutlines(long extraDistanceAroundPart_um, ConfigSettings config)
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

			storage.raftOutline = storage.raftOutline.CreateUnion(storage.WipeLayer(0).Offset(extraDistanceAroundPart_um));

			if (storage.WipeShield.Count > 0
				&& storage.WipeShield[0].Count > 0)
			{
				storage.raftOutline = storage.raftOutline.CreateUnion(storage.WipeShield[0].Offset(extraDistanceAroundPart_um));
			}

			if (storage.Support != null)
			{
				storage.raftOutline = storage.raftOutline.CreateUnion(storage.Support.GetBedOutlines().Offset(extraDistanceAroundPart_um));
			}
		}

		public void GenerateSkirt(long distance_um, long extrusionWidth_um, int numberOfLoops, int brimCount, long minLength_um, ConfigSettings config)
		{
			Polygons islandsToSkirtAround = GetSkirtBounds(
				config,
				this,
				distance_um > 0,
				distance_um,
				extrusionWidth_um,
				brimCount);

			if (islandsToSkirtAround.Count > 0)
			{
				// Find convex hull for the skirt outline
				var convexHull = new Polygons(new[] { islandsToSkirtAround.CreateConvexHull() });

				// Create skirt loops from the ConvexHull
				for (int skirtLoop = 0; skirtLoop < numberOfLoops; skirtLoop++)
				{
					long offsetDistance = distance_um + extrusionWidth_um * (skirtLoop + 1) - extrusionWidth_um / 2;

					this.Skirt.AddAll(convexHull.Offset(offsetDistance));

					int length = (int)this.Skirt.PolygonLength();
					if (skirtLoop + 1 >= numberOfLoops && length > 0 && length < minLength_um)
					{
						// add more loops for as long as we have not extruded enough length
						numberOfLoops++;
					}
				}
			}
		}

		private int MaxPrimingLoops(ConfigSettings config)
		{
			return Support != null ? config.ExtruderCount * 2 - 2 : config.ExtruderCount - 1;
		}

		public void GenerateWipeTowerInfill(int extruderIndex, Polygons partOutline, Polygons outputfillPolygons, long extrusionWidth_um, ConfigSettings config)
		{
			int maxPrimingLoops = MaxPrimingLoops(config);

			Polygons outlineForExtruder = partOutline.Offset(-extrusionWidth_um * extruderIndex);

			var loopsPrinted = 0;
			long insetPerLoop = extrusionWidth_um * maxPrimingLoops;
			while (outlineForExtruder.Count > 0
				&& loopsPrinted < config.WipeTowerPerimetersPerExtruder)
			{
				for (int polygonIndex = 0; polygonIndex < outlineForExtruder.Count; polygonIndex++)
				{
					Polygon newInset = outlineForExtruder[polygonIndex];
					newInset.Add(newInset[0]); // add in the last move so it is a solid polygon
					outputfillPolygons.Add(newInset);
				}

				outlineForExtruder = outlineForExtruder.Offset(-insetPerLoop);
				loopsPrinted++;
			}

			outputfillPolygons.Reverse();
		}

		public bool HaveWipeTower(ConfigSettings config, int layerIndex)
		{
			if (WipeTower == null
				|| WipeTower.Count == 0
				|| WipeTower[0].Count == 0
				|| layerIndex > LastLayerWithChange(config) + 1)
			{
				return false;
			}

			return true;
		}

		public bool PrimeOnWipeTower(int layerIndex, LayerGCodePlanner layerGcodePlanner, PathFinder pathFinder, GCodePathConfig fillConfig, ConfigSettings config, bool airGapped)
		{
			if (!HaveWipeTower(config, layerIndex)
				|| (layerIndex == 0 && !config.EnableRaft))
			{
				return false;
			}

			if (airGapped)
			{
				// don't print the wipe tower with air gap height
				layerGcodePlanner.CurrentZ -= config.SupportAirGap_um;
			}

			// If we changed extruder, print the wipe/prime tower for this nozzle;
			var fillPolygons = new Polygons();
			GenerateWipeTowerInfill(primesThisLayer, this.WipeLayer(layerIndex), fillPolygons, fillConfig.LineWidth_um, config);

			if (fillPolygons.Count > 0)
			{
				// move over to the wipe tower with the layer planner in place
				layerGcodePlanner.QueueTravel(WipeCenter_um, pathFinder, fillConfig.LiftOnTravel);

				// extrude a tiny amount of material so as to trigger the un-retract while in the center of the tower
				layerGcodePlanner.QueueWipeTowerPolygons(new Polygons() { new Polygon() { WipeCenter_um, WipeCenter_um + new IntPoint(config.ExtrusionWidth_um / 2, 0) } }, fillConfig);

				// print the wipe tower with no planning
				layerGcodePlanner.QueueWipeTowerPolygons(fillPolygons, fillConfig);

				layerGcodePlanner.ForceRetract();

				if (airGapped)
				{
					// don't print the wipe tower with air gap height
					layerGcodePlanner.CurrentZ += config.SupportAirGap_um;
				}

				primesThisLayer++;
				return true;
			}

			return false;
		}

		public void WriteRaftGCodeIfRequired(GCodeExport gcode, ConfigSettings config)
		{
			LayerDataStorage storage = this;
			if (config.ShouldGenerateRaft())
			{
				var raftBaseConfig = new GCodePathConfig("raftBaseConfig", "SUPPORT", config.DefaultAcceleration);
				raftBaseConfig.SetData(config.FirstLayerSpeed, config.RaftBaseExtrusionWidth_um);

				var raftMiddleConfig = new GCodePathConfig("raftMiddleConfig", "SUPPORT", config.DefaultAcceleration);
				raftMiddleConfig.SetData(config.RaftPrintSpeed, config.RaftInterfaceExtrusionWidth_um);

				var raftSurfaceConfig = new GCodePathConfig("raftMiddleConfig", "SUPPORT", config.DefaultAcceleration);
				raftSurfaceConfig.SetData((config.RaftSurfacePrintSpeed > 0) ? config.RaftSurfacePrintSpeed : config.RaftPrintSpeed, config.RaftSurfaceExtrusionWidth_um);

				// create the raft base
				{
					gcode.WriteComment("RAFT BASE");
					var layerPlanner = new LayerGCodePlanner(config, gcode, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
					if (config.RaftExtruder >= 0)
					{
						// if we have a specified raft extruder use it
						layerPlanner.SetExtruder(config.RaftExtruder);
					}
					else if (config.SupportExtruder >= 0)
					{
						// else preserve the old behavior of using the support extruder if set.
						layerPlanner.SetExtruder(config.SupportExtruder);
					}

					gcode.CurrentZ_um = config.RaftBaseThickness_um;

					gcode.LayerChanged(-3, config.RaftBaseThickness_um);

					gcode.SetExtrusion(config.RaftBaseThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);

					// write the skirt around the raft
					layerPlanner.QueuePolygonsByOptimizer(storage.Skirt, null, raftBaseConfig, 0);

					List<Polygons> raftIslands = storage.raftOutline.ProcessIntoSeparateIslands();
					foreach (var raftIsland in raftIslands)
					{
						// write the outline of the raft
						layerPlanner.QueuePolygonsByOptimizer(raftIsland, null, raftBaseConfig, 0);

						var raftLines = new Polygons();
						Infill.GenerateLinePaths(
							raftIsland.Offset(-config.RaftBaseExtrusionWidth_um),
							raftLines,
							config.RaftBaseLineSpacing_um,
							config.InfillExtendIntoPerimeter_um,
							0);

						// write the inside of the raft base
						layerPlanner.QueuePolygonsByOptimizer(raftLines, null, raftBaseConfig, 0);

						if (config.RetractWhenChangingIslands)
						{
							layerPlanner.ForceRetract();
						}
					}

					layerPlanner.WriteQueuedGCode(config.RaftBaseThickness_um);
				}

				// raft middle layers
				{
					gcode.WriteComment("RAFT MIDDLE");
					var layerPlanner = new LayerGCodePlanner(config, gcode, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
					gcode.CurrentZ_um = config.RaftBaseThickness_um + config.RaftInterfaceThicknes_um;
					gcode.LayerChanged(-2, config.RaftInterfaceThicknes_um);
					gcode.SetExtrusion(config.RaftInterfaceThicknes_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);

					var raftLines = new Polygons();
					Infill.GenerateLinePaths(storage.raftOutline, raftLines, config.RaftInterfaceLineSpacing_um, config.InfillExtendIntoPerimeter_um, 45);
					layerPlanner.QueuePolygonsByOptimizer(raftLines, null, raftMiddleConfig, 0);

					layerPlanner.WriteQueuedGCode(config.RaftInterfaceThicknes_um);
				}

				for (int raftSurfaceIndex = 1; raftSurfaceIndex <= config.RaftSurfaceLayers; raftSurfaceIndex++)
				{
					gcode.WriteComment("RAFT SURFACE");
					var layerPlanner = new LayerGCodePlanner(config, gcode, config.TravelSpeed, config.MinimumTravelToCauseRetraction_um, config.PerimeterStartEndOverlapRatio);
					gcode.CurrentZ_um = config.RaftBaseThickness_um + config.RaftInterfaceThicknes_um + config.RaftSurfaceThickness_um * raftSurfaceIndex;
					gcode.LayerChanged(-1, config.RaftSurfaceThickness_um);
					gcode.SetExtrusion(config.RaftSurfaceThickness_um, config.FilamentDiameter_um, config.ExtrusionMultiplier);

					var raftLines = new Polygons();
					if (raftSurfaceIndex == config.RaftSurfaceLayers)
					{
						// make sure the top layer of the raft is 90 degrees offset to the first layer of the part so that it has minimum contact points.
						Infill.GenerateLinePaths(storage.raftOutline, raftLines, config.RaftSurfaceLineSpacing_um, config.InfillExtendIntoPerimeter_um, config.InfillStartingAngle + 90);
					}
					else
					{
						Infill.GenerateLinePaths(storage.raftOutline, raftLines, config.RaftSurfaceLineSpacing_um, config.InfillExtendIntoPerimeter_um, 90 * raftSurfaceIndex);
					}

					layerPlanner.QueuePolygonsByOptimizer(raftLines, null, raftSurfaceConfig, 0);

					layerPlanner.WriteQueuedGCode(config.RaftInterfaceThicknes_um);
				}
			}
		}

		private static Polygons GetSkirtBounds(ConfigSettings config, LayerDataStorage storage, bool externalOnly, long distance_um, long extrusionWidth_um, int brimCount)
		{
			bool hasWipeTower = storage.WipeLayer(0).PolygonLength() > 0;

			var skirtPolygons = new Polygons();

			if (config.EnableRaft)
			{
				skirtPolygons = skirtPolygons.CreateUnion(storage.raftOutline);
			}
			else
			{
				var allOutlines = hasWipeTower ? new Polygons(storage.WipeLayer(0).Offset(-extrusionWidth_um / 2)) : new Polygons();

				if (storage.WipeShield.Count > 0
					&& storage.WipeShield[0].Count > 0)
				{
					allOutlines = allOutlines.CreateUnion(storage.WipeShield[0].Offset(-extrusionWidth_um / 2));
				}

				// Loop over every extruder
				for (int extrudeIndex = 0; extrudeIndex < storage.Extruders.Count; extrudeIndex++)
				{
					// Only process the first extruder on spiral vase or
					// skip extruders that have empty layers
					if (config.ContinuousSpiralOuterPerimeter)
					{
						SliceLayer layer0 = storage.Extruders[extrudeIndex].Layers[0];
						if (layer0.Islands.Count > 0)
						{
							allOutlines.AddAll(layer0.Islands[0]?.IslandOutline);
						}
					}
					else
					{
						// Add the layers outline to allOutlines
						SliceLayer layer = storage.Extruders[extrudeIndex].Layers[0];
						foreach (var island in layer.Islands)
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
					Polygons brimIslandOutlines = new Polygons();

					// Grow each island by the current brim distance
					// Union the island brims
					brimIslandOutlines = brimIslandOutlines.CreateUnion(allOutlines);

					if (storage.Support != null)
					{
						brimIslandOutlines = brimIslandOutlines.CreateUnion(storage.Support.GetBedOutlines());
					}

					Polygons brimLoops = new Polygons();
					for (int brimIndex = 0; brimIndex < brimCount; brimIndex++)
					{
						// Extend the polygons to account for the brim (ensures convex hull takes this data into account)
						brimLoops.AddAll(brimIslandOutlines.Offset(extrusionWidth_um * brimIndex + extrusionWidth_um / 2));
					}

					storage.Brims.AddAll(brimLoops);

					// and extend the bounds of the skirt polygons
					skirtPolygons = skirtPolygons.CreateUnion(brimIslandOutlines.Offset(extrusionWidth_um * brimCount));
				}

				skirtPolygons = skirtPolygons.CreateUnion(allOutlines);

				if (storage.Support != null)
				{
					skirtPolygons = skirtPolygons.CreateUnion(storage.Support.GetBedOutlines());
				}
			}

			return skirtPolygons;
		}

		public int LastLayerWithChange(ConfigSettings config)
		{
			if (calculatedLastLayer)
			{
				return lastLayerWithChange;
			}

			int numLayers = Extruders[0].Layers.Count;
			int firstExtruderWithData = -1;
			for (int checkLayer = numLayers - 1; checkLayer >= 0; checkLayer--)
			{
				for (int extruderToCheck = 0; extruderToCheck < config.ExtruderCount; extruderToCheck++)
				{
					if ((extruderToCheck < Extruders.Count && Extruders[extruderToCheck].Layers[checkLayer].AllOutlines.Count > 0)
						|| (config.SupportExtruder == extruderToCheck && Support != null && Support.HasNormalSupport(checkLayer))
						|| (config.SupportInterfaceExtruder == extruderToCheck && Support != null && Support.HasInterfaceSupport(checkLayer)))
					{
						if (firstExtruderWithData == -1)
						{
							firstExtruderWithData = extruderToCheck;
						}
						else
						{
							if (firstExtruderWithData != extruderToCheck)
							{
								// have to remember the layer one above this so that we can switch back
								lastLayerWithChange = checkLayer + 1;
								calculatedLastLayer = true;
								return lastLayerWithChange;
							}
						}
					}
				}
			}

			calculatedLastLayer = true;
			return -1;
		}
	}
}