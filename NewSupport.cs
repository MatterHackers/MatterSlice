/*
This file is part of MatterSlice. A commandline utility for
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

using MatterSlice.ClipperLib;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class NewSupport
	{
        readonly static double cleanDistance_um = 10;
        List<Polygons> allPartOutlines = new List<Polygons>();
		List<Polygons> allPotentialSupportOutlines = new List<Polygons>();
		List<Polygons> allRequiredSupportOutlines = new List<Polygons>();
        List<Polygons> easyGrabDistanceOutlines = new List<Polygons>();
		List<Polygons> allDownOutlines = new List<Polygons>();
		List<Polygons> clippedToXyOutlines = new List<Polygons>();

		public NewSupport(int numLayers, ConfigSettings config, PartLayers storage)
        {
			// create starting support outlines
			allPartOutlines = CalculateAllPartOutlines(numLayers, config, storage);

			allPotentialSupportOutlines = FindAllPotentialSupportOutlines(allPartOutlines, numLayers, config, storage);

            allRequiredSupportOutlines = RemoveSelfSupportedSections(allPotentialSupportOutlines, numLayers, config, storage);

			easyGrabDistanceOutlines = ExpandToEasyGrabDistance(allRequiredSupportOutlines, numLayers, config, storage);

			allDownOutlines = AccumulateDownPolygons(easyGrabDistanceOutlines, allPartOutlines, numLayers, config, storage);

			clippedToXyOutlines = ClipToXyDistance(allDownOutlines, allPartOutlines, numLayers, config, storage);
			// clip to xy distance from all parts
			// change top layers into interface layers
			// expand interface layers to be grabable outside the mesh
			// clip expanded interface layers to existing parts xy

			// reduce columns by nozzel width to make them shrink to minimum volume as they decend towards the bed
			// grow columns by nozzel width to make them expand to minimum volume as they decend towards the bed
			// expand columns towards the bed so they have a good hold area on the bed
			// clip expanded interface layers to existing parts xy
			// make support sparse (slice lines and stop extrusion to make weak)
		}

		static List<Polygons> CreateEmptyPolygons(int numLayers)
		{
			List<Polygons> polygonsList = new List<Polygons>();
            for (int i = 0; i < numLayers; i++)
			{
				polygonsList.Add(new Polygons());
			}

			return polygonsList;
        }

		private static List<Polygons> CalculateAllPartOutlines(int numLayers, ConfigSettings config, PartLayers storage)
        {
			List<Polygons> allPartOutlines = CreateEmptyPolygons(numLayers);
            // calculate the combind outlines for everything
            for (int layerIndex = numLayers - 1; layerIndex >= 0; layerIndex--)
            {
                Polygons allOutlines = new Polygons();

                SliceLayerParts curLayer = storage.Layers[layerIndex];
                for (int curLayerPartIndex = 0; curLayerPartIndex < curLayer.parts.Count; curLayerPartIndex++)
                {
                    SliceLayerPart curPart = curLayer.parts[curLayerPartIndex];
                    Polygons curLayerPolys = curPart.TotalOutline;
                    allOutlines = allOutlines.CreateUnion(curLayerPolys);
                    allOutlines = Clipper.CleanPolygons(allOutlines, cleanDistance_um);
                }

                allPartOutlines[layerIndex] = allOutlines.DeepCopy();
            }

			return allPartOutlines;
        }

		private static List<Polygons> FindAllPotentialSupportOutlines(List<Polygons> inputPolys, int numLayers, ConfigSettings config, PartLayers storage)
        {
			List<Polygons> allPotentialSupportOutlines = CreateEmptyPolygons(numLayers);
			// calculate all the non-supported areas
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
            {
                Polygons aboveLayerPolys = inputPolys[layerIndex+1];
                Polygons curLayerPolys = inputPolys[layerIndex];
                Polygons supportedAreas = aboveLayerPolys.CreateDifference(curLayerPolys.Offset(10));
                allPotentialSupportOutlines[layerIndex] = Clipper.CleanPolygons(supportedAreas, cleanDistance_um);
            }

			return allPotentialSupportOutlines;
        }

		private static List<Polygons> RemoveSelfSupportedSections(List<Polygons> inputPolys, int numLayers, ConfigSettings config, PartLayers storage)
		{
			List<Polygons> allRequiredSupportOutlines = CreateEmptyPolygons(numLayers);
			// calculate all the non-supported areas
			for (int layerIndex = numLayers - 1; layerIndex > 0; layerIndex--)
			{
				if (inputPolys[layerIndex - 1].Count > 0)
				{
					if (inputPolys[layerIndex].Count > 0)
					{
						allRequiredSupportOutlines[layerIndex] = inputPolys[layerIndex].Offset(-config.extrusionWidth_um / 2);
						allRequiredSupportOutlines[layerIndex] = allRequiredSupportOutlines[layerIndex].Offset(config.extrusionWidth_um / 2);
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

		private static List<Polygons> ExpandToEasyGrabDistance(List<Polygons> inputPolys, int numLayers, ConfigSettings config, PartLayers storage)
        {
			List<Polygons> easyGrabDistanceOutlines = CreateEmptyPolygons(numLayers);
			// calculate all the non-supported areas
			for (int layerIndex = numLayers - 1; layerIndex >= 0; layerIndex--)
            {
                Polygons curLayerPolys = inputPolys[layerIndex];
                easyGrabDistanceOutlines[layerIndex] = Clipper.CleanPolygons(curLayerPolys.Offset(config.extrusionWidth_um + config.supportXYDistance_um), cleanDistance_um);
            }

			return easyGrabDistanceOutlines;
        }

		private static List<Polygons> AccumulateDownPolygons(List<Polygons> inputPolys, List<Polygons> allPartOutlines, int numLayers, ConfigSettings config, PartLayers storage)
		{
			List<Polygons> allDownOutlines = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons aboveRequiredSupport = inputPolys[layerIndex + 1];
			
				// get all the polygons above us
                Polygons accumulatedAbove = allDownOutlines[layerIndex+1].CreateUnion(aboveRequiredSupport);
				
				// add in the support on this level
				Polygons curRequiredSupport = inputPolys[layerIndex];
				Polygons totalSupportThisLayer = accumulatedAbove.CreateUnion(curRequiredSupport);

				// remove the solid polys on this level
				Polygons remainingAbove = totalSupportThisLayer.CreateDifference(allPartOutlines[layerIndex]);

				allDownOutlines[layerIndex] = Clipper.CleanPolygons(remainingAbove, cleanDistance_um);
			}

			return allDownOutlines;
        }

		private static List<Polygons> ClipToXyDistance(List<Polygons> inputPolys, List<Polygons> allPartOutlines, int numLayers, ConfigSettings config, PartLayers storage)
		{
			List<Polygons> clippedToXyOutlines = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				// add in the support on this level
				Polygons curRequiredSupport = inputPolys[layerIndex];
				Polygons expandedlayerPolys = allPartOutlines[layerIndex].Offset(config.supportXYDistance_um);
				Polygons totalSupportThisLayer = curRequiredSupport.CreateDifference(expandedlayerPolys);

				clippedToXyOutlines[layerIndex] = Clipper.CleanPolygons(totalSupportThisLayer, cleanDistance_um);
			}

			return clippedToXyOutlines;
		}

		public void AddSupports(GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig, GCodePathConfig supportInterfaceConfig)
		{
			List<Polygons> outlinesToRender = null;
			//outlinesToRender = allPartOutlines;
			//outlinesToRender = allPotentialSupportOutlines;
			//outlinesToRender = allRequiredSupportOutlines;
            //outlinesToRender = allDownOutlines;
			outlinesToRender = clippedToXyOutlines;

			if (outlinesToRender[layerIndex] != null)
			{
				gcodeLayer.WritePolygonsByOptimizer(outlinesToRender[layerIndex], supportNormalConfig);
			}
		}

		public void AddAirGappedBottomLayers(GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig, GCodePathConfig supportInterfaceConfig)
		{
			throw new NotImplementedException();
		}

		public bool HaveBottomLayers(int layerIndex)
		{
			return false;
		}
	}
}