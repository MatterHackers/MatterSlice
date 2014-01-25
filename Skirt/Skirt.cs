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

using ClipperLib;

using MatterHackers.MatterSlice;

namespace MatterHackers.MatterSlice
{
    public static class Skirt
    {
        public static void generateSkirt(SliceDataStorage storage, int distance, int extrusionWidth, int count, int minLength)
        {
            long length = 0;
            for (int skirtNr = 0; skirtNr < count; skirtNr++)
            {
                Clipper skirtUnion = new Clipper();
                for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                {
                    if (storage.volumes[volumeIdx].layers.Count < 1) continue;
                    SliceLayer layer = storage.volumes[volumeIdx].layers[0];
                    for (int i = 0; i < layer.parts.Count; i++)
                    {
                        Polygons skirt = Clipper.OffsetPolygons(layer.parts[i].outline, distance + extrusionWidth * skirtNr + extrusionWidth / 2, ClipperLib.JoinType.jtSquare, 2, false);
                        skirtUnion.AddPolygon(skirt[0], ClipperLib.PolyType.ptSubject);
                    }
                }
                Polygons skirtResult = new Polygons();
                skirtUnion.Execute(ClipperLib.ClipType.ctUnion, skirtResult, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);
                for (int n = 0; n < skirtResult.Count; n++)
                {
                    storage.skirt.Add(skirtResult[n]);
                    length += skirtResult[n].polygonLength();
                }
                if (skirtNr + 1 >= count && length > 0 && length < minLength)
                    count++;
            }
        }
    }
}

