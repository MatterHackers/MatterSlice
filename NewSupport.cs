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
using MSClipperLib;

// TODO:
// split the part into multiple areas for support pillars
// Create extra upward support for small features (tip of a rotated box)
// sparse write the support layers so they are easier to remove
// make column reduced support work

// DONE:
// check frost morn, should have support under unsupported parts
// make sure all air gapped layers are written after ALL extruder normal layers
// Make the on model material be air gapped
// Offset the output data to account for nozzle diameter (currently they are just the outlines not the extrude positions)
// Make skirt consider support outlines
// Make raft consider support outlines
// Make sure we work correctly with the support extruder set.
// Make from bed only work (no internal support)
// Fix extra extruder material on top of interface layer

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class NewSupport
	{
		private static double cleanDistance_um = 10;

		public NewSupport(ConfigSettings config, List<ExtruderLayers> Extruders, double grabDistanceMm, ExtruderLayers userGeneratedSupport = null)
		{
			cleanDistance_um = config.ExtrusionWidth_um / 10;
			long supportWidth_um = (long)(config.ExtrusionWidth_um * (100 - config.SupportPercent) / 100);
			// create starting support outlines
			List<Polygons> allPartOutlines = CalculateAllPartOutlines(config, Extruders);
			_InsetPartOutlines = CreateInsetPartOutlines(allPartOutlines, config.ExtrusionWidth_um / 2);

			List<Polygons> easyGrabDistanceOutlines;
			if (userGeneratedSupport == null)
			{
				_AllPotentialSupportOutlines = FindAllPotentialSupportOutlines(_InsetPartOutlines, supportWidth_um);

				List<Polygons> allRequiredSupportOutlines = RemoveSelfSupportedSections(_AllPotentialSupportOutlines, supportWidth_um);

				if (!config.GenerateInternalSupport)
				{
					allRequiredSupportOutlines = RemoveSupportFromInternalSpaces(allRequiredSupportOutlines, _InsetPartOutlines);
				}

				easyGrabDistanceOutlines = ExpandToEasyGrabDistance(allRequiredSupportOutlines, (int)(grabDistanceMm * 1000));

				SupportOutlines = AccumulateDownPolygons(config, easyGrabDistanceOutlines, _InsetPartOutlines);
			}
			else
			{
				int numSupportLayers = userGeneratedSupport.Layers.Count;
				easyGrabDistanceOutlines = CreateEmptyPolygons(numSupportLayers);

				// calculate the combined outlines for everything
				for (int layerIndex = 0; layerIndex < numSupportLayers; layerIndex++)
				{
					easyGrabDistanceOutlines[layerIndex] = allPartOutlines[layerIndex].CreateUnion(userGeneratedSupport.Layers[layerIndex].AllOutlines);
					easyGrabDistanceOutlines[layerIndex].ProcessIntoSeparateIslands();
				}
			}

			// remove the actual parts from the interface data
			InterfaceLayers = ClipToXyDistance(easyGrabDistanceOutlines, _InsetPartOutlines, config);
			InterfaceLayers = CreateInterfaceLayers(InterfaceLayers, config.SupportInterfaceLayers);

			// remove the actual parts from the support data
			SupportOutlines = ClipToXyDistance(SupportOutlines, _InsetPartOutlines, config);

			// remove the interface layers from the normal support layers
			SupportOutlines = CalculateDifferencePerLayer(SupportOutlines, InterfaceLayers);

			AirGappedBottomOutlines = CreateAirGappedBottomLayers(SupportOutlines, _InsetPartOutlines);
			// remove the airGappedBottomOutlines layers from the normal support layers
			SupportOutlines = CalculateDifferencePerLayer(SupportOutlines, AirGappedBottomOutlines);
		}

		//List<Polygons> pushedUpTopOutlines = new List<Polygons>();
		public List<Polygons> AirGappedBottomOutlines { get; }

		public List<Polygons> InterfaceLayers { get; }
		public List<Polygons> SupportOutlines { get; }

		#region // unit testing data

		public List<Polygons> _AllPotentialSupportOutlines { get; }
		public List<Polygons> _AllRequiredSupportOutlines { get; }
		public List<Polygons> _InsetPartOutlines { get; }

		#endregion // unit testing data

		public Polygons GetBedOutlines()
		{
			return SupportOutlines[0].CreateUnion(InterfaceLayers[0]);
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
				else if (layerIndex < SupportOutlines.Count)
				{
					return SupportOutlines[layerIndex];
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
			return SupportOutlines[layerIndex].Count > 0;
		}

		public void QueueAirGappedBottomLayer(ConfigSettings config, GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig)
		{
			// normal support
			Polygons currentAirGappedBottoms = AirGappedBottomOutlines[layerIndex];
			currentAirGappedBottoms = currentAirGappedBottoms.Offset(-config.ExtrusionWidth_um / 2);
			List<Polygons> supportIslands = currentAirGappedBottoms.ProcessIntoSeparateIslands();

			foreach (Polygons islandOutline in supportIslands)
			{
				// force a retract if changing islands
				if (config.RetractWhenChangingIslands)
				{
					gcodeLayer.ForceRetract();
				}

				Polygons islandInfillLines = new Polygons();
				// render a grid of support
				if (config.GenerateSupportPerimeter)
				{
					Polygons outlines = Clipper.CleanPolygons(islandOutline, config.ExtrusionWidth_um / 4);
					gcodeLayer.QueuePolygonsByOptimizer(outlines, supportNormalConfig);
				}
				Polygons infillOutline = islandOutline.Offset(-config.ExtrusionWidth_um / 2);
				switch (config.SupportType)
				{
					case ConfigConstants.SUPPORT_TYPE.GRID:
						Infill.GenerateGridInfill(config, infillOutline, islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
						break;

					case ConfigConstants.SUPPORT_TYPE.LINES:
						Infill.GenerateLineInfill(config, infillOutline, islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
						break;
				}
				gcodeLayer.QueuePolygonsByOptimizer(islandInfillLines, supportNormalConfig);
			}
		}

		public bool QueueInterfaceSupportLayer(ConfigSettings config, GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportInterfaceConfig)
		{
			// interface
			bool outputPaths = false;
			Polygons currentInterfaceOutlines2 = InterfaceLayers[layerIndex].Offset(-config.ExtrusionWidth_um / 2);
			if (currentInterfaceOutlines2.Count > 0)
			{
				List<Polygons> interfaceIslands = currentInterfaceOutlines2.ProcessIntoSeparateIslands();

				foreach (Polygons interfaceOutline in interfaceIslands)
				{
					// force a retract if changing islands
					if (config.RetractWhenChangingIslands)
					{
						gcodeLayer.ForceRetract();
					}

					// make a border if layer 0
					if (layerIndex == 0)
					{
						Polygons infillOutline = interfaceOutline.Offset(-supportInterfaceConfig.lineWidth_um / 2);
						Polygons outlines = Clipper.CleanPolygons(infillOutline, config.ExtrusionWidth_um / 4);
						if (gcodeLayer.QueuePolygonsByOptimizer(outlines, supportInterfaceConfig))
						{
							outputPaths = true;
						}
					}

					Polygons supportLines = new Polygons();
					Infill.GenerateLineInfill(config, interfaceOutline, supportLines, config.InfillStartingAngle + 90, config.ExtrusionWidth_um);
					if (gcodeLayer.QueuePolygonsByOptimizer(supportLines, supportInterfaceConfig))
					{
						outputPaths = true;
					}
				}
			}

			return outputPaths;
		}

		public bool QueueNormalSupportLayer(ConfigSettings config, GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig)
		{
			// normal support
			Polygons currentSupportOutlines = SupportOutlines[layerIndex];
			currentSupportOutlines = currentSupportOutlines.Offset(-supportNormalConfig.lineWidth_um / 2);
			List<Polygons> supportIslands = currentSupportOutlines.ProcessIntoSeparateIslands();

			bool outputPaths = false;
			foreach (Polygons islandOutline in supportIslands)
			{
				// force a retract if changing islands
				if (config.RetractWhenChangingIslands)
				{
					gcodeLayer.ForceRetract();
				}

				Polygons islandInfillLines = new Polygons();
				// render a grid of support
				if (config.GenerateSupportPerimeter || layerIndex == 0)
				{
					Polygons outlines = Clipper.CleanPolygons(islandOutline, config.ExtrusionWidth_um / 4);
					if (gcodeLayer.QueuePolygonsByOptimizer(outlines, supportNormalConfig))
					{
						outputPaths = true;
					}
				}

				Polygons infillOutline = islandOutline.Offset(-supportNormalConfig.lineWidth_um / 2);

				if (layerIndex == 0)
				{
					// on the first layer print this as solid
					Infill.GenerateLineInfill(config, infillOutline, islandInfillLines, config.SupportInfillStartingAngle, config.ExtrusionWidth_um);
				}
				else
				{
					switch (config.SupportType)
					{
						case ConfigConstants.SUPPORT_TYPE.GRID:
							Infill.GenerateGridInfill(config, infillOutline, islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
							break;

						case ConfigConstants.SUPPORT_TYPE.LINES:
							Infill.GenerateLineInfill(config, infillOutline, islandInfillLines, config.SupportInfillStartingAngle, config.SupportLineSpacing_um);
							break;
					}
				}

				if (gcodeLayer.QueuePolygonsByOptimizer(islandInfillLines, supportNormalConfig))
				{
					outputPaths |= true;
				}
			}

			return outputPaths;
		}

		private static List<Polygons> AccumulateDownPolygons(ConfigSettings config, List<Polygons> inputPolys, List<Polygons> allPartOutlines)
		{
			int numLayers = inputPolys.Count;

			long nozzleSize = config.ExtrusionWidth_um;
			long areaToTryAndBe = 20 * 20 * nozzleSize * nozzleSize; // 10 x 10 mm approximately (assuming .5 nozzle)

			List<Polygons> allDownOutlines = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons aboveRequiredSupport = inputPolys[layerIndex + 1];

				// get all the polygons above us
				Polygons accumulatedAbove = allDownOutlines[layerIndex + 1].CreateUnion(aboveRequiredSupport);

				// experimental and not working well enough yet
				if (config.MinimizeSupportColumns)
				{
					// reduce the amount of support material used
					for (int i = accumulatedAbove.Count - 1; i >= 0; i--)
					{
						Polygon polygon = accumulatedAbove[i];
						double polyArea = polygon.Area();
						if (polyArea > areaToTryAndBe)
						{
							Polygons offsetPolygons = new Polygons() { polygon }.Offset(-config.ExtrusionWidth_um / 2);
							accumulatedAbove.RemoveAt(i);
							foreach (Polygon polyToAdd in offsetPolygons)
							{
								accumulatedAbove.Insert(i, polyToAdd);
							}
						}
						else if (polyArea < areaToTryAndBe * .9)
						{
							Polygons offsetPolygons = new Polygons() { polygon }.Offset(config.ExtrusionWidth_um / 2);
							accumulatedAbove.RemoveAt(i);
							foreach (Polygon polyToAdd in offsetPolygons)
							{
								accumulatedAbove.Insert(i, polyToAdd);
							}
						}
					}
				}

				// add in the support on this level
				Polygons curRequiredSupport = inputPolys[layerIndex];

				Polygons totalSupportThisLayer = accumulatedAbove.CreateUnion(curRequiredSupport);

				// remove the solid polygons on this level
				Polygons remainingAbove = totalSupportThisLayer.CreateDifference(allPartOutlines[layerIndex]);

				allDownOutlines[layerIndex] = Clipper.CleanPolygons(remainingAbove, cleanDistance_um);
			}

			return allDownOutlines;
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
				Polygons expandedlayerPolys = allPartOutlines[layerIndex].Offset(config.SupportXYDistance_um);
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
					Polygons accumulatedAbove = inputPolys[layerIndex].DeepCopy();

					for (int addIndex = layerIndex + 1; addIndex < Math.Min(layerIndex + numInterfaceLayers, numLayers - 2); addIndex++)
					{
						accumulatedAbove = accumulatedAbove.CreateUnion(inputPolys[addIndex]);
						accumulatedAbove = Clipper.CleanPolygons(accumulatedAbove, cleanDistance_um);
					}

					allInterfaceLayers[layerIndex] = accumulatedAbove;
				}
			}

			return allInterfaceLayers;
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

		private static List<Polygons> FindAllPotentialSupportOutlines(List<Polygons> inputPolys, long supportWidth_um)
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

		private static List<Polygons> RemoveSelfSupportedSections(List<Polygons> inputPolys, long supportWidth_um)
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
	}
}