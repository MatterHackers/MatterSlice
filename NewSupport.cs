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

        internal List<Polygons> allPartOutlines = new List<Polygons>();
        internal List<Polygons> allPotentialSupportOutlines = new List<Polygons>();
        internal List<Polygons> allRequiredSupportOutlines = new List<Polygons>();
        internal List<Polygons> easyGrabDistanceOutlines = new List<Polygons>();
        //List<Polygons> pushedUpTopOutlines = new List<Polygons>();
        internal List<Polygons> supportOutlines = new List<Polygons>();
        internal List<Polygons> interfaceLayers = new List<Polygons>();

        double interfaceLayersMm;
        double grabDistanceMm;

        public Polygons GetRequiredSupportAreas(int layerIndex)
		{
			if (layerIndex < allRequiredSupportOutlines.Count && layerIndex >= 0)
			{
				return allRequiredSupportOutlines[layerIndex];
			}

			return new Polygons();
        }

		public NewSupport(ConfigSettings config, PartLayers storage, double interfaceLayersMm, double grabDistanceMm)
        {
            this.interfaceLayersMm = interfaceLayersMm;
            this.grabDistanceMm = grabDistanceMm;
            // create starting support outlines
            allPartOutlines = CalculateAllPartOutlines(config, storage);

			allPotentialSupportOutlines = FindAllPotentialSupportOutlines(allPartOutlines, config);

            allRequiredSupportOutlines = RemoveSelfSupportedSections(allPotentialSupportOutlines, config);

			easyGrabDistanceOutlines = ExpandToEasyGrabDistance(allRequiredSupportOutlines, (int)(grabDistanceMm*1000));

			//pushedUpTopOutlines = PushUpTops(easyGrabDistanceOutlines, numLayers, config);

            int numInterfaceLayers = (int)(interfaceLayersMm*1000) / config.layerThickness_um;
			interfaceLayers = CreateInterfacelayers(easyGrabDistanceOutlines, numInterfaceLayers);
			interfaceLayers = ClipToXyDistance(interfaceLayers, allPartOutlines, config);

			supportOutlines = AccumulateDownPolygons(easyGrabDistanceOutlines, allPartOutlines);
            supportOutlines = ClipToXyDistance(supportOutlines, allPartOutlines, config);

            supportOutlines = CalculateDifferencePerLayer(supportOutlines, interfaceLayers);
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

		private static List<Polygons> CalculateAllPartOutlines(ConfigSettings config, PartLayers storage)
        {
            int numLayers = storage.Layers.Count;

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

		private static List<Polygons> FindAllPotentialSupportOutlines(List<Polygons> inputPolys, ConfigSettings config)
        {
            int numLayers = inputPolys.Count;
            List<Polygons> allPotentialSupportOutlines = CreateEmptyPolygons(numLayers);
			// calculate all the non-supported areas
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
            {
                Polygons aboveLayerPolys = inputPolys[layerIndex+1];
                Polygons curLayerPolys = inputPolys[layerIndex];
                Polygons supportedAreas = aboveLayerPolys.CreateDifference(curLayerPolys);
                allPotentialSupportOutlines[layerIndex] = Clipper.CleanPolygons(supportedAreas, cleanDistance_um);
            }

			return allPotentialSupportOutlines;
        }

		private static List<Polygons> RemoveSelfSupportedSections(List<Polygons> inputPolys, ConfigSettings config)
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

		private static List<Polygons> PushUpTops(List<Polygons> inputPolys, ConfigSettings config)
		{
            int numLayers = inputPolys.Count;

            return inputPolys;
			int layersFor2Mm = 2000 / config.layerThickness_um;
			List<Polygons> pushedUpPolys = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 1; layerIndex >= 0; layerIndex--)
			{
				for (int layerToAddToIndex = Math.Min(layerIndex+ layersFor2Mm, numLayers - 1); layerToAddToIndex >= 0; layerToAddToIndex--)
				{
				}

				Polygons curLayerPolys = inputPolys[layerIndex];
				pushedUpPolys[layerIndex] = Clipper.CleanPolygons(curLayerPolys.Offset(config.extrusionWidth_um + config.supportXYDistance_um), cleanDistance_um);
			}

			return pushedUpPolys;
		}

		private static List<Polygons> AccumulateDownPolygons(List<Polygons> inputPolys, List<Polygons> allPartOutlines)
		{
            int numLayers = inputPolys.Count;

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

        private static List<Polygons> CreateInterfacelayers(List<Polygons> inputPolys, int numInterfaceLayers)
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

		private static List<Polygons> ClipToXyDistance(List<Polygons> inputPolys, List<Polygons> allPartOutlines, ConfigSettings config)
		{
            int numLayers = inputPolys.Count;

            List<Polygons> clippedToXyOutlines = CreateEmptyPolygons(numLayers);
			for (int layerIndex = numLayers - 2; layerIndex >= 0; layerIndex--)
			{
				Polygons curRequiredSupport = inputPolys[layerIndex];
				Polygons expandedlayerPolys = allPartOutlines[layerIndex].Offset(config.supportXYDistance_um);
				Polygons totalSupportThisLayer = curRequiredSupport.CreateDifference(expandedlayerPolys);

				clippedToXyOutlines[layerIndex] = Clipper.CleanPolygons(totalSupportThisLayer, cleanDistance_um);
			}

			return clippedToXyOutlines;
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

		public void WriteNormalSupportLayer(GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig, GCodePathConfig supportInterfaceConfig)
		{
            if (false)
            {
                List<Polygons> outlinesToRender = null;
                //outlinesToRender = allPartOutlines;
                //outlinesToRender = allPotentialSupportOutlines;
                //outlinesToRender = allRequiredSupportOutlines;
                //outlinesToRender = allDownOutlines;
                //outlinesToRender = pushedUpTopOutlines;
                outlinesToRender = supportOutlines;
                //outlinesToRender = interfaceLayers;

                gcodeLayer.WritePolygonsByOptimizer(outlinesToRender[layerIndex], supportNormalConfig);
            }
            else
            {
                gcodeLayer.WritePolygonsByOptimizer(supportOutlines[layerIndex], supportNormalConfig);

                gcodeLayer.WritePolygonsByOptimizer(interfaceLayers[layerIndex], supportInterfaceConfig);
            }
		}

		public void WriteAirGappedBottomLayer(GCodePlanner gcodeLayer, int layerIndex, GCodePathConfig supportNormalConfig, GCodePathConfig supportInterfaceConfig)
		{
			throw new NotImplementedException();
		}

        public bool HaveBottomLayers(int layerIndex)
        {
            return false;
        }
    }
}