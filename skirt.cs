/*
Copyright (c) 2013, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
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

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Point = IntPoint;
    using PolygonRef = Polygon;

    public class Skirt
    {
        public static void generateSkirt(SliceDataStorage storage, int distance, int extrusionWidth, int count, int minLength, int initialLayerHeight)
        {
            for (int skirtNr = 0; skirtNr < count; skirtNr++)
            {
                int offsetDistance = distance + extrusionWidth * skirtNr + extrusionWidth / 2;

                Polygons skirtPolygons = new Polygons(storage.wipeTower.offset(offsetDistance));
                for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                {
                    if (storage.volumes[volumeIdx].layers.Count < 1) continue;
                    SliceLayer layer = storage.volumes[volumeIdx].layers[0];
                    for (int i = 0; i < layer.parts.Count; i++)
                    {
                        skirtPolygons = skirtPolygons.unionPolygons(layer.parts[i].outline.offset(offsetDistance));
                    }
                }

                SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, initialLayerHeight);
                skirtPolygons = skirtPolygons.unionPolygons(supportGenerator.polygons.offset(offsetDistance));

                //Remove small inner skirt holes. Holes have a negative area, remove anything smaller then 100x extrusion "area"
                for (int n = 0; n < skirtPolygons.Count; n++)
                {
                    double area = skirtPolygons[n].area();
                    if (area < 0 && area > -extrusionWidth * extrusionWidth * 100)
                        skirtPolygons.remove(n--);
                }

                storage.skirt.add(skirtPolygons);

                int lenght = (int)storage.skirt.polygonLength();
                if (skirtNr + 1 >= count && lenght > 0 && lenght < minLength)
                    count++;
            }
        }
    }
}