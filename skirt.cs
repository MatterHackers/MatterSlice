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

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class Skirt
    {
        public static void generateSkirt(SliceDataStorage storage, int distance, int extrusionWidth_um, int numberOfLoops, int minLength, int initialLayerHeight, ConfigSettings config)
        {
            bool externalOnly = (distance > 0);
            for (int skirtLoop = 0; skirtLoop < numberOfLoops; skirtLoop++)
            {
                int offsetDistance = distance + extrusionWidth_um * skirtLoop + extrusionWidth_um / 2;

                Polygons skirtPolygons = new Polygons(storage.wipeTower.Offset(offsetDistance));
                for (int volumeIndex = 0; volumeIndex < storage.volumes.Count; volumeIndex++)
                {
					if (config.continuousSpiralOuterPerimeter && volumeIndex > 0)
					{
						continue;
					}
					
					if (storage.volumes[volumeIndex].layers.Count < 1)
                    {
                        continue;
                    }

                    SliceLayer layer = storage.volumes[volumeIndex].layers[0];
                    for (int partIndex = 0; partIndex < layer.parts.Count; partIndex++)
                    {
						if (config.continuousSpiralOuterPerimeter && partIndex > 0)
						{
							continue;
						}
						
						if (externalOnly)
                        {
                            Polygons p = new Polygons();
                            p.Add(layer.parts[partIndex].outline[0]);
                            skirtPolygons = skirtPolygons.CreateUnion(p.Offset(offsetDistance));
                        }
                        else
                        {
                            skirtPolygons = skirtPolygons.CreateUnion(layer.parts[partIndex].outline.Offset(offsetDistance));
                        }
                    }
                }

                SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, initialLayerHeight);
                skirtPolygons = skirtPolygons.CreateUnion(supportGenerator.supportPolygons.Offset(offsetDistance));

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

                int lenght = (int)storage.skirt.polygonLength();
                if (skirtLoop + 1 >= numberOfLoops && lenght > 0 && lenght < minLength)
                {
                    // add more loops for as long as we have not extruded enough length
                    numberOfLoops++;
                }
            }
        }
    }
}