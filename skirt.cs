/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
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
        public static void generateSkirt(SliceDataStorage storage, int distance, int extrusionWidth_um, int numberOfLoops, int minLength, int initialLayerHeight)
        {
            bool externalOnly = (distance > 0);
            for (int skirtLoop = 0; skirtLoop < numberOfLoops; skirtLoop++)
            {
                int offsetDistance = distance + extrusionWidth_um * skirtLoop + extrusionWidth_um / 2;

                Polygons skirtPolygons = new Polygons(storage.wipeTower.Offset(offsetDistance));
                for (int volumeIndex = 0; volumeIndex < storage.volumes.Count; volumeIndex++)
                {
                    if (storage.volumes[volumeIndex].layers.Count < 1)
                    {
                        continue;
                    }

                    SliceLayer layer = storage.volumes[volumeIndex].layers[0];
                    for (int i = 0; i < layer.parts.Count; i++)
                    {
                        if (externalOnly)
                        {
                            Polygons p = new Polygons();
                            p.Add(layer.parts[i].outline[0]);
                            skirtPolygons = skirtPolygons.CreateUnion(p.Offset(offsetDistance));
                        }
                        else
                        {
                            skirtPolygons = skirtPolygons.CreateUnion(layer.parts[i].outline.Offset(offsetDistance));
                        }
                    }
                }

                SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, initialLayerHeight);
                skirtPolygons = skirtPolygons.CreateUnion(supportGenerator.polygons.Offset(offsetDistance));

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