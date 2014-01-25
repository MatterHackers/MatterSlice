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

namespace MatterHackers.MatterSlice
{
    public static class inset
    {
        public static void generateInsets(SliceLayerPart part, int offset, int insetCount)
        {
            part.combBoundery = Clipper.OffsetPolygons(part.outline, -offset, ClipperLib.JoinType.jtSquare, 2, false);
            if (insetCount == 0)
            {
                part.insets.Add(part.outline);
                return;
            }

            for (int i = 0; i < insetCount; i++)
            {
                part.insets.Add(new Polygons());
                part.insets[i] = Clipper.OffsetPolygons(part.outline, -offset * i - offset / 2, ClipperLib.JoinType.jtSquare, 2, false);
                part.insets[i].optimizePolygons();
                if (part.insets[i].Count < 1)
                {
                    part.insets.RemoveAt(part.insets.Count - 1);
                    break;
                }
            }
        }

        public static void generateInsets(SliceLayer layer, int offset, int insetCount)
        {
            for (int partNr = 0; partNr < layer.parts.Count; partNr++)
            {
                inset.generateInsets(layer.parts[partNr], offset, insetCount);
            }

            //Remove the parts which did not generate an inset. As these parts are too small to print,
            // and later code can now assume that there is always minimal 1 inset line.
            for (int partNr = 0; partNr < layer.parts.Count; partNr++)
            {
                if (layer.parts[partNr].insets.Count < 1)
                {
                    throw new NotImplementedException();
                    //layer.parts.erase(partNr);
                    partNr -= 1;
                }
            }
        }
    }
}
