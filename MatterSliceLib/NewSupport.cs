/*
This file is part of MatterSlice. A command line utility for
generating 3D printing GCode.

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
using MatterHackers.Pathfinding;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class NewSupport
	{
		private static double cleanDistance_um = 10;

		public NewSupport(ConfigSettings config, List<ExtruderLayers> extruders, ExtruderLayers userGeneratedSupport)
		{
			cleanDistance_um = config.ExtrusionWidth_um / 10;
			// create starting support outlines
			List<Polygons> allPartOutlines = CalculateAllPartOutlines(config, extruders);
			_InsetPartOutlines = CreateInsetPartOutlines(allPartOutlines, config.ExtrusionWidth_um / 2);

			if (userGeneratedSupport == null)
			{
				long supportWidth_um = (long)(config.ExtrusionWidth_um * (100 - config.SupportPercent) / 100);

				_AllUnsupportedAreas = FindAllUnsupportedAreas(_InsetPartOutlines, supportWidth_um);

				_RequiredSupportAreas = RemoveSelfSupportedAreas(_AllUnsupportedAreas, supportWidth_um);

				if (!config.GenerateInternalSupport)
				{
					_RequiredSupportAreas = RemoveSupportFromInternalSpaces(_RequiredSupportAreas, _InsetPartOutlines);
				}

				SparseSupportOutlines = AccumulateDownPolygons(config, _RequiredSupportAreas, _InsetPartOutlines);

				SparseSupportOutlines = ExpandToEasyGrabDistance(SparseSupportOutlines, config.SupportGrabDistance_um - supportWidth_um);
			}
			else
			{
				int numSupportLayers = userGeneratedSupport.Layers.Count;
				SparseSupportOutlines = CreateEmptyPolygons(numSupportLayers);

				// calculate the combined outlines for everything
				for (int layerIndex = 0; layerIndex < numSupportLayers; layerIndex++)
				{
					SparseSupportOutlines[layerIndex] = userGeneratedSupport.Layers[layerIndex].AllOutlines.DeepCopy();
				}
			}

			// remove the actual parts from the support data
			SparseSupportOutlines = ClipToXyDistance(SparseSupportOutlines, _InsetPartOutlines, config);

			// create the interface layers
			InterfaceLayers = CreateInterfaceLayers(SparseSupportOutlines, config.SupportInterfaceLayers);

			// and the bottom support layers
			AirGappedBottomOutlines = CreateAirGappedBottomLayers(SparseSupportOutlines, _InsetPartOutlines);

			// remove the interface layers from the normal support layers
			SparseSupportOutlines = CalculateDifferencePerLayer(SparseSupportOutlines, InterfaceLayers);
			// remove the airGappedBottomOutlines layers from the normal support layers
			SparseSupportOutlines = CalculateDifferencePerLayer(SparseSupportOutlines, AirGappedBottomOutlines);
			// make sure we don't print an interface layer where there is a bottom layer
			InterfaceLayers = CalculateDifferencePerLayer(InterfaceLayers, AirGappedBottomOutlines);
		}

		// List<Polygons> pushedUpTopOutlines = new List<Polygons>();
		public List<Polygons> AirGappedBottomOutlines { get; }

		public List<Polygons> InterfaceLayers { get; }

		public List<Polygons> SparseSupportOutlines { get; }

		public List<Polygons> _AllUnsupportedAreas { get; }

		public List<Polygons> _RequiredSupportAreas { get; }

		#region // unit testing data

		public List<Polygons> _InsetPartOutlines { get; }

		#endregion // unit testing data

		public Polygons GetBedOutlines()
		{
			return SparseSupportOutlines[0].CreateUnion(InterfaceLayers[0]);
		}

		private static List<Polygons> ExpandToEasyGrabDistance(List<Polygons> inputPolys, long grabDistance_um)
		{
			int numLayers = inputPolys.Count;

			List<Polygons> easyGrabDistanceOutlines = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 1; layerIndex >= 0; layerIndex--)
			{
				Polygons curLayerPolys = inputPolys[layerIndex];
				easyGrabDistanceOutlines[layerIndex] = Clipper.CleanPolygons(curLayerPolys.Offset(grabDistance_um), cleanDistance_um);
			}

			return easyGrabDistanceOutlines;
		}

		private static List<Polygons> FindAllUnsupportedAreas(List<Polygons> inputPolys, long supportWidth_um)
		{
			int numLayers = inputPolys.Count;
			List<Polygons> allPotentialSupportOutlines = CreateEmptyPolygons(numLayers);
			// calculate all the non-supported areas
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons aboveLayerPolys = inputPolys[layerIndex + 1];
				Polygons curLayerPolys = inputPolys[layerIndex].Offset(supportWidth_um);
				Polygons areasNeedingSupport = aboveLayerPolys.CreateDifference(curLayerPolys);
				allPotentialSupportOutlines[layerIndex] = Clipper.CleanPolygons(areasNeedingSupport, cleanDistance_um);
			}

			return allPotentialSupportOutlines;
		}

		private static List<Polygons> RemoveSelfSupportedAreas(List<Polygons> inputPolys, long supportWidth_um)
		{
			int numLayers = inputPolys.Count;

			List<Polygons> allRequiredSupportOutlines = CreateEmptyPolygons(numLayers);
			// calculate all the non-supported areas
			for (int layerIndex = numLayers - 1; layerIndex > 0; layerIndex--)
			{
				if (inputPolys[layerIndex - 1].Count > 0)
				{
					if (inputPolys[layerIndex].Count > 0)
					{
						Polygons expandedLayerBellow = inputPolys[layerIndex - 1].Offset(supportWidth_um);

						allRequiredSupportOutlines[layerIndex] = inputPolys[layerIndex].CreateDifference(expandedLayerBellow);
						allRequiredSupportOutlines[layerIndex] = Clipper.CleanPolygons(allRequiredSupportOutlines[layerIndex], cleanDistance_um);
					}
				}
				else
				{
					allRequiredSupportOutlines[layerIndex] = inputPolys[layerIndex].DeepCopy();
				}
			}

			return allRequiredSupportOutlines;
		}

		private static List<Polygons> RemoveSupportFromInternalSpaces(List<Polygons> inputPolys, List<Polygons> allPartOutlines)
		{
			int numLayers = inputPolys.Count;

			Polygons accumulatedLayers = new Polygons();
			for (int layerIndex = 0; layerIndex < numLayers; layerIndex++)
			{
				accumulatedLayers = accumulatedLayers.CreateUnion(allPartOutlines[layerIndex]);
				accumulatedLayers = Clipper.CleanPolygons(accumulatedLayers, cleanDistance_um);

				inputPolys[layerIndex] = inputPolys[layerIndex].CreateDifference(accumulatedLayers);
				inputPolys[layerIndex] = Clipper.CleanPolygons(inputPolys[layerIndex], cleanDistance_um);
			}

			return inputPolys;
		}

		private static List<Polygons> AccumulateDownPolygons(ConfigSettings config, List<Polygons> inputPolys, List<Polygons> allPartOutlines)
		{
			int numLayers = inputPolys.Count;

			long nozzleSize = config.ExtrusionWidth_um;

			List<Polygons> allDownOutlines = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons aboveRequiredSupport = inputPolys[layerIndex + 1];

				// get all the polygons above us
				Polygons accumulatedAbove = allDownOutlines[layerIndex + 1].CreateUnion(aboveRequiredSupport);

				// add in the support on this level
				Polygons curRequiredSupport = inputPolys[layerIndex];

				Polygons totalSupportThisLayer = accumulatedAbove.CreateUnion(curRequiredSupport);

				// remove the solid polygons on this level
				Polygons remainingAbove = totalSupportThisLayer.CreateDifference(allPartOutlines[layerIndex]);

				allDownOutlines[layerIndex] = Clipper.CleanPolygons(remainingAbove, cleanDistance_um);
			}

			return allDownOutlines;
		}

		public Polygons GetRequiredSupportAreas(int layerIndex)
		{
			layerIndex--;
			if (layerIndex < InterfaceLayers.Count && layerIndex >= 0)
			{
				if (InterfaceLayers[layerIndex].Count > 0)
				{
					return InterfaceLayers[layerIndex];
				}
				else if (layerIndex < SparseSupportOutlines.Count)
				{
					return SparseSupportOutlines[layerIndex];
				}
			}

			return new Polygons();
		}

		public bool HasInterfaceSupport(int layerIndex)
		{
			return InterfaceLayers[layerIndex].Count > 0;
		}

		public bool HasNormalSupport(int layerIndex)
		{
			return SparseSupportOutlines[layerIndex].Count > 0;
		}

		public bool HaveAirGappedBottomLayer(int layerIndex)
		{
			return AirGappedBottomOutlines[layerIndex].Count > 0;
		}

		public void QueueAirGappedBottomLayer(ConfigSettings config, LayerGCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig)
		{
			// normal support
			Polygons currentAirGappedBottoms = AirGappedBottomOutlines[layerIndex];
			List<Polygons> supportIslands = currentAirGappedBottoms.ProcessIntoSeparateIslands();

			foreach (Polygons supportIsland in supportIslands)
			{
				PathFinder pathFinder = null;
				if (config.AvoidCrossingPerimeters)
				{
					pathFinder = new PathFinder(supportIsland, -config.ExtrusionWidth_um / 2, useInsideCache: config.AvoidCrossingPerimeters, name: "air gap");
				}

				// force a retract if changing islands
				if (config.RetractWhenChangingIslands)
				{
					gcodeLayer.ForceRetract();
				}

				var infillOffset = -config.ExtrusionWidth_um + config.InfillExtendIntoPerimeter_um;

				// make a border if layer 0
				if (config.GenerateSupportPerimeter || layerIndex == 0)
				{
					var closedLoop = supportNormalConfig.ClosedLoop;
					supportNormalConfig.ClosedLoop = true;
					gcodeLayer.QueuePolygonsByOptimizer(supportIsland.Offset(-config.ExtrusionWidth_um / 2), pathFinder, supportNormalConfig, layerIndex);
					infillOffset = config.ExtrusionWidth_um * -2 + config.InfillExtendIntoPerimeter_um;
					supportNormalConfig.ClosedLoop = closedLoop;
				}

				Polygons infillOutline = supportIsland.Offset(infillOffset);
				Polygons islandInfillLines = new Polygons();
				switch (config.SupportType)
				{
					case ConfigConstants.SUPPORT_TYPE.GRID:
						Infill.GenerateGridInfill(config, infillOutline, islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
						break;

					case ConfigConstants.SUPPORT_TYPE.LINES:
						Infill.GenerateLineInfill(config, infillOutline, islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
						break;
				}

				gcodeLayer.QueuePolygonsByOptimizer(islandInfillLines, pathFinder, supportNormalConfig, 0);
			}
		}

		public bool QueueInterfaceSupportLayer(ConfigSettings config, LayerGCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportInterfaceConfig)
		{
			// interface
			bool outputPaths = false;
			Polygons interfaceOutlines = InterfaceLayers[layerIndex];
			if (interfaceOutlines.Count > 0)
			{
				List<Polygons> interfaceIslands = interfaceOutlines.ProcessIntoSeparateIslands();

				foreach (Polygons interfaceIsland in interfaceIslands)
				{
					PathFinder pathFinder = null;
					if (config.AvoidCrossingPerimeters)
					{
						pathFinder = new PathFinder(interfaceIsland, -config.ExtrusionWidth_um / 2, useInsideCache: config.AvoidCrossingPerimeters, name: "interface");
					}

					// force a retract if changing islands
					if (config.RetractWhenChangingIslands)
					{
						gcodeLayer.ForceRetract();
					}

					var infillOffset = -config.ExtrusionWidth_um + config.InfillExtendIntoPerimeter_um;

					// make a border if layer 0
					if (config.GenerateSupportPerimeter || layerIndex == 0)
					{
						if (gcodeLayer.QueuePolygonsByOptimizer(interfaceIsland.Offset(-config.ExtrusionWidth_um / 2), pathFinder, supportInterfaceConfig, 0))
						{
							outputPaths = true;
						}

						infillOffset = config.ExtrusionWidth_um * -2 + config.InfillExtendIntoPerimeter_um;
					}

					Polygons supportLines = new Polygons();
					Infill.GenerateLineInfill(config, interfaceIsland.Offset(infillOffset), supportLines, config.InfillStartingAngle + 90, config.ExtrusionWidth_um);
					if (gcodeLayer.QueuePolygonsByOptimizer(supportLines, pathFinder, supportInterfaceConfig, 0))
					{
						outputPaths = true;
					}
				}
			}

			return outputPaths;
		}

		public bool QueueNormalSupportLayer(ConfigSettings config, LayerGCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig)
		{
			// normal support
			Polygons currentSupportOutlines = SparseSupportOutlines[layerIndex];
			List<Polygons> supportIslands = currentSupportOutlines.ProcessIntoSeparateIslands();

			List<PathFinder> pathFinders = new List<PathFinder>();
			List<Polygons> infillOutlines = new List<Polygons>();
			for (int i = 0; i < supportIslands.Count; i++)
			{
				pathFinders.Add(null);
				infillOutlines.Add(null);
			}

			Agg.Parallel.For(0, supportIslands.Count, (index) =>
			{
				var infillOffset = -config.ExtrusionWidth_um + config.InfillExtendIntoPerimeter_um;
				var supportIsland = supportIslands[index];
				infillOutlines[index] = supportIsland.Offset(infillOffset);

				if (config.AvoidCrossingPerimeters)
				{
					pathFinders[index] = new PathFinder(infillOutlines[index], -config.ExtrusionWidth_um / 2, useInsideCache: config.AvoidCrossingPerimeters, name: "normal support");
				}
			});

			bool outputPaths = false;
			for (int i = 0; i < supportIslands.Count; i++)
			{
				var supportIsland = supportIslands[i];

				// force a retract if changing islands
				if (config.RetractWhenChangingIslands)
				{
					gcodeLayer.ForceRetract();
				}

				// make a border if layer 0
				if (config.GenerateSupportPerimeter || layerIndex == 0)
				{
					if (gcodeLayer.QueuePolygonsByOptimizer(supportIsland.Offset(-config.ExtrusionWidth_um / 2), pathFinders[i], supportNormalConfig, 0))
					{
						outputPaths = true;
					}
				}

				Polygons islandInfillLines = new Polygons();
				if (layerIndex == 0)
				{
					// on the first layer print this as solid
					Infill.GenerateLineInfill(config, infillOutlines[i], islandInfillLines, config.SupportInfillStartingAngle, config.ExtrusionWidth_um);
				}
				else
				{
					switch (config.SupportType)
					{
						case ConfigConstants.SUPPORT_TYPE.GRID:
							Infill.GenerateGridInfill(config, infillOutlines[i], islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
							break;

						case ConfigConstants.SUPPORT_TYPE.LINES:
							Infill.GenerateLineInfill(config, infillOutlines[i], islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
							break;
					}
				}

				if (gcodeLayer.QueuePolygonsByOptimizer(islandInfillLines, pathFinders[i], supportNormalConfig, 0))
				{
					outputPaths |= true;
				}
			}

			return outputPaths;
		}

		private static List<Polygons> CalculateAllPartOutlines(ConfigSettings config, List<ExtruderLayers> Extruders)
		{
			int numLayers = Extruders[0].Layers.Count;

			List<Polygons> allPartOutlines = CreateEmptyPolygons(numLayers);

			foreach (var extruder in Extruders)
			{
				// calculate the combined outlines for everything
				for (int layerIndex = numLayers - 1; layerIndex >= 0; layerIndex--)
				{
					allPartOutlines[layerIndex] = allPartOutlines[layerIndex].CreateUnion(extruder.Layers[layerIndex].AllOutlines);
				}
			}

			return allPartOutlines;
		}

		private static List<Polygons> CalculateDifferencePerLayer(List<Polygons> inputPolys, List<Polygons> outlinesToRemove)
		{
			int numLayers = inputPolys.Count;

			List<Polygons> diferenceLayers = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons curRequiredSupport = inputPolys[layerIndex];
				Polygons totalSupportThisLayer = curRequiredSupport.CreateDifference(outlinesToRemove[layerIndex]);

				diferenceLayers[layerIndex] = Clipper.CleanPolygons(totalSupportThisLayer, cleanDistance_um);
			}

			return diferenceLayers;
		}

		private static List<Polygons> ClipToXyDistance(List<Polygons> inputPolys, List<Polygons> allPartOutlines, ConfigSettings config)
		{
			int numLayers = inputPolys.Count;

			List<Polygons> clippedToXyOutlines = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons curRequiredSupport = inputPolys[layerIndex];
				Polygons expandedlayerPolys = allPartOutlines[layerIndex].Offset(config.SupportXYDistance_um + config.ExtrusionWidth_um / 2);
				Polygons totalSupportThisLayer = curRequiredSupport.CreateDifference(expandedlayerPolys);

				clippedToXyOutlines[layerIndex] = Clipper.CleanPolygons(totalSupportThisLayer, cleanDistance_um);
			}

			return clippedToXyOutlines;
		}

		private static List<Polygons> CreateAirGappedBottomLayers(List<Polygons> inputPolys, List<Polygons> allPartOutlines)
		{
			int numLayers = inputPolys.Count;

			List<Polygons> airGappedBottoms = CreateEmptyPolygons(numLayers);
			for (int layerIndex = 1; layerIndex < numLayers; layerIndex++)
			{
				Polygons belowOutlines = allPartOutlines[layerIndex - 1];

				Polygons curRequiredSupport = inputPolys[layerIndex];
				Polygons airGapArea = belowOutlines.CreateIntersection(curRequiredSupport);

				airGappedBottoms[layerIndex] = airGapArea;
			}

			return airGappedBottoms;
		}

		private static List<Polygons> CreateEmptyPolygons(int numLayers)
		{
			List<Polygons> polygonsList = new List<Polygons>();
			for (int i = 0; i < numLayers; i++)
			{
				polygonsList.Add(new Polygons());
			}

			return polygonsList;
		}

		private static List<Polygons> CreateInsetPartOutlines(List<Polygons> inputPolys, long insetAmount_um)
		{
			int numLayers = inputPolys.Count;
			List<Polygons> allInsetOutlines = CreateEmptyPolygons(numLayers);
			// calculate all the non-supported areas
			for (int layerIndex = 0; layerIndex < numLayers; layerIndex++)
			{
				Polygons insetPolygons = Clipper.CleanPolygons(inputPolys[layerIndex].Offset(-insetAmount_um), cleanDistance_um);
				List<Polygons> insetIslands = insetPolygons.ProcessIntoSeparateIslands();

				foreach (Polygons insetOutline in insetIslands)
				{
					insetOutline.RemoveSmallAreas(insetAmount_um * 2);
					foreach (var islandPart in insetOutline)
					{
						allInsetOutlines[layerIndex].Add(islandPart);
					}
				}
			}

			return allInsetOutlines;
		}

		private static List<Polygons> CreateInterfaceLayers(List<Polygons> inputPolys, int numInterfaceLayers)
		{
			int numLayers = inputPolys.Count;

			List<Polygons> allInterfaceLayers = CreateEmptyPolygons(numLayers);
			if (numInterfaceLayers > 0)
			{
				for (int layerIndex = 0; layerIndex < numLayers; layerIndex++)
				{
					Polygons requiredInterfacePolys = inputPolys[layerIndex].DeepCopy();

					if (layerIndex < numLayers - 1)
					{
						Polygons intersectionsAbove = inputPolys[layerIndex + 1].DeepCopy();

						for (int aboveIndex = layerIndex + 2; aboveIndex < Math.Min(layerIndex + numInterfaceLayers + 1, numLayers); aboveIndex++)
						{
							intersectionsAbove = intersectionsAbove.CreateIntersection(inputPolys[aboveIndex]);
							intersectionsAbove = Clipper.CleanPolygons(intersectionsAbove, cleanDistance_um);
						}

						requiredInterfacePolys = requiredInterfacePolys.CreateDifference(intersectionsAbove);
						requiredInterfacePolys = Clipper.CleanPolygons(requiredInterfacePolys, cleanDistance_um);
					}

					allInterfaceLayers[layerIndex] = requiredInterfacePolys;
				}
			}

			return allInterfaceLayers;
		}
	}
}
