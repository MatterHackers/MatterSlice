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
        public List<Polygons> supportPolygons = new List<Polygons>();

        public NewSupport(int numLayers, ConfigSettings config, SliceVolumeStorage storage)
        {
            for (int i = 0; i < numLayers; i++)
            {
                supportPolygons.Add(null);
            }

            // create starting support outlines
            CreateAllPossibleSupport(numLayers, config, storage);
            // remove any portion that will be sitting on existing parts
            RemoveExistingIntersection(numLayers, storage);
            // clip to xy distance from all parts
            // change top layers into interface layers
            // expand interface layers to be grabable outside the mesh
            // clip expanded interface layers to existing parts xy

            // reduce columns by nozzel width to make them shrink to minimum volume as they decend towards the bottom
            // make support sparse (slice lines and stop extrusion to make weak)

            // remove top layers that are in the air gap range
        }


        private void CreateAllPossibleSupport(int numLayers, ConfigSettings config, SliceVolumeStorage storage)
        {
            Polygons accumulatedBottoms = new Polygons();

            // calculate all the non-supported areas
            for (int layerIndex = numLayers - 1; layerIndex > 0; layerIndex--)
            {
                SliceLayer belowLayer = storage.layers[layerIndex - 1];
                SliceLayer curLayer = storage.layers[layerIndex];
                for (int partIndex = 0; partIndex < curLayer.parts.Count; partIndex++)
                {
                    SliceLayerPart curPart = curLayer.parts[partIndex];
                    Polygons curLayerPolys = curPart.TotalOutline.Offset(-config.outsideExtrusionWidth_um/2);
                    if (belowLayer.parts.Count > 0)
                    {
                        SliceLayerPart belowPart = belowLayer.parts[0];
                        Polygons belowLayerPolys = belowPart.TotalOutline.Offset(-config.outsideExtrusionWidth_um / 2);
                        Polygons activeLayerPolys = curLayerPolys.CreateDifference(belowLayerPolys);

                        accumulatedBottoms = accumulatedBottoms.CreateUnion(activeLayerPolys);
                        accumulatedBottoms = Clipper.CleanPolygons(accumulatedBottoms, cleanDistance_um);
                    }
                    else // have to add everything from this layer
                    {
                        accumulatedBottoms = accumulatedBottoms.CreateUnion(curLayerPolys);
                        accumulatedBottoms = Clipper.CleanPolygons(accumulatedBottoms, cleanDistance_um);
                    }
                }

                supportPolygons[layerIndex] = new Polygons();
                foreach (Polygon poly in accumulatedBottoms)
                {
                    supportPolygons[layerIndex].Add(new Polygon(poly));
                }
            }
        }

        private void RemoveExistingIntersection(int numLayers, SliceVolumeStorage storage)
        {
            for (int layerIndex = numLayers - 1; layerIndex > 0; layerIndex--)
            {
                SliceLayer curLayer = storage.layers[layerIndex];
                for (int partIndex = 0; partIndex < curLayer.parts.Count; partIndex++)
                {
                    SliceLayerPart curPart = curLayer.parts[partIndex];
                    Polygons curLayerPolys = curPart.TotalOutline;

                    supportPolygons[layerIndex] = supportPolygons[layerIndex].CreateDifference(curLayerPolys);
                    supportPolygons[layerIndex] = Clipper.CleanPolygons(supportPolygons[layerIndex], cleanDistance_um);
                }
            }
        }

        internal void AddSupportToGCode(GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig)
        {
            if (supportPolygons[layerIndex] != null)
            {
                // make sure last top layer is correct height to leave air gap
                gcodeLayer.WritePolygonsByOptimizer(supportPolygons[layerIndex], supportNormalConfig);
            }
        }
    }
}