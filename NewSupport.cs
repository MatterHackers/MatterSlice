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

		public NewSupport(int numLayers, ConfigSettings config, PartLayers storage)
        {
            for (int i = 0; i < numLayers; i++)
            {
                allPotentialSupportOutlines.Add(new Polygons());
                allPartOutlines.Add(new Polygons());
				allRequiredSupportOutlines.Add(new Polygons());
				allDownOutlines.Add(new Polygons());
                easyGrabDistanceOutlines.Add(new Polygons());
            }

            // create starting support outlines
            CalculateAllPartOutlines(numLayers, config, storage);
            FindAllPotentialSupportOutlines(numLayers, config, storage);
            RemoveSelfSupportedSections(numLayers, config, storage);
            ExpandToEasyGrabDistance(numLayers, config, storage);
			AccumulateDownPolygons(numLayers, config, storage);
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


        private void CalculateAllPartOutlines(int numLayers, ConfigSettings config, PartLayers storage)
        {
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
        }

        private void FindAllPotentialSupportOutlines(int numLayers, ConfigSettings config, PartLayers storage)
        {
            // calculate all the non-supported areas
            for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
            {
                Polygons aboveLayerPolys = allPartOutlines[layerIndex+1];
                Polygons curLayerPolys = allPartOutlines[layerIndex];
                Polygons supportedAreas = aboveLayerPolys.CreateDifference(curLayerPolys.Offset(10));
                allPotentialSupportOutlines[layerIndex] = Clipper.CleanPolygons(supportedAreas, cleanDistance_um);
            }
        }

        private void RemoveSelfSupportedSections(int numLayers, ConfigSettings config, PartLayers storage)
		{
			// calculate all the non-supported areas
			for (int layerIndex = numLayers - 1; layerIndex > 0; layerIndex--)
			{
				if (allPotentialSupportOutlines[layerIndex - 1].Count > 0)
				{
					if (allPotentialSupportOutlines[layerIndex].Count > 0)
					{
						allRequiredSupportOutlines[layerIndex] = allPotentialSupportOutlines[layerIndex].Offset(-config.extrusionWidth_um / 2);
						allRequiredSupportOutlines[layerIndex] = allRequiredSupportOutlines[layerIndex].Offset(config.extrusionWidth_um / 2);
						allRequiredSupportOutlines[layerIndex] = Clipper.CleanPolygons(allRequiredSupportOutlines[layerIndex], cleanDistance_um);
					}
				}
				else
				{
					allRequiredSupportOutlines[layerIndex] = allPotentialSupportOutlines[layerIndex].DeepCopy();
				}
			}
		}

        private void ExpandToEasyGrabDistance(int numLayers, ConfigSettings config, PartLayers storage)
        {
            // calculate all the non-supported areas
            for (int layerIndex = numLayers - 1; layerIndex >= 0; layerIndex--)
            {
                Polygons curLayerPolys = allRequiredSupportOutlines[layerIndex];
                easyGrabDistanceOutlines[layerIndex] = Clipper.CleanPolygons(curLayerPolys.Offset(config.extrusionWidth_um * 2 + config.supportXYDistance_um), cleanDistance_um);
            }
        }

        public void AccumulateDownPolygons(int numLayers, ConfigSettings config, PartLayers storage)
		{
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons aboveRequiredSupport = easyGrabDistanceOutlines[layerIndex + 1];
			
				// get all the polygons above us
                Polygons accumulatedAbove = allDownOutlines[layerIndex+1].CreateUnion(aboveRequiredSupport);
				
				// add in the support on this level
				Polygons curRequiredSupport = easyGrabDistanceOutlines[layerIndex];
				Polygons totalSupportThisLayer = accumulatedAbove.CreateUnion(curRequiredSupport);

				// remove the solid polys on this level
				Polygons remainingAbove = totalSupportThisLayer.CreateDifference(allPartOutlines[layerIndex]);

				allDownOutlines[layerIndex] = Clipper.CleanPolygons(remainingAbove, cleanDistance_um);
			}
		}

		public void AddSupports(GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig, GCodePathConfig supportInterfaceConfig)
		{
			List<Polygons> outlinesToRender = null;
			//outlinesToRender = allPartOutlines;
			//outlinesToRender = allPotentialSupportOutlines;
			//outlinesToRender = allRequiredSupportOutlines;
            outlinesToRender = allDownOutlines;

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