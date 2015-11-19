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
        List<Polygons> allPossibleSupportOutlines = new List<Polygons>();
        List<Polygons> minimumSupportOutlines = new List<Polygons>();

        public NewSupport(int numLayers, ConfigSettings config, PartLayers storage)
        {
            for (int i = 0; i < numLayers; i++)
            {
                allPossibleSupportOutlines.Add(null);
                allPartOutlines.Add(null);
            }

            // create starting support outlines
            CalculateAllPartOutlines(numLayers, config, storage);
            FindAllPossibleSupportOutlines(numLayers, config, storage);
			RemoveSelfSupportedSections(numLayers, config, storage);
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

        private void FindAllPossibleSupportOutlines(int numLayers, ConfigSettings config, PartLayers storage)
        {
            // calculate all the non-supported areas
            for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
            {
                Polygons aboveLayerPolys = allPartOutlines[layerIndex+1];
                Polygons curLayerPolys = allPartOutlines[layerIndex];
                Polygons supportedAreas = aboveLayerPolys.CreateDifference(curLayerPolys.Offset(10));
                allPossibleSupportOutlines[layerIndex] = Clipper.CleanPolygons(supportedAreas, cleanDistance_um);
            }
        }

        private void RemoveSelfSupportedSections(int numLayers, ConfigSettings config, PartLayers storage)
		{
			// calculate all the non-supported areas
			for(int i=0; i<allPossibleSupportOutlines.Count; i++)
			{
				if (allPossibleSupportOutlines[i] != null)
				{
					allPossibleSupportOutlines[i] = allPossibleSupportOutlines[i].Offset(-config.extrusionWidth_um / 2);
					allPossibleSupportOutlines[i] = allPossibleSupportOutlines[i].Offset(config.extrusionWidth_um / 2);
				}
			}
		}

        internal void AddSupportToGCode(GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig)
        {
            if (allPossibleSupportOutlines[layerIndex] != null)
            {
                // make sure last top layer is correct height to leave air gap
                gcodeLayer.WritePolygonsByOptimizer(allPossibleSupportOutlines[layerIndex], supportNormalConfig);
            }
        }
    }
}