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
using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public static class Bridge
    {
        public static double bridgeAngle(SliceLayerPart part, SliceLayer prevLayer)
        {
            //To detect if we have a bridge, first calculate the intersection of the current layer with the previous layer.
            // This gives us the islands that the layer rests on.
            Clipper bridgeClip = new Clipper();
            bridgeClip.AddPolygon(part.outline[0], PolyType.ptSubject);

            for (int n = 0; n < prevLayer.parts.Count; n++)
            {
                if (!part.boundaryBox.hit(prevLayer.parts[n].boundaryBox)) continue;

                bridgeClip.AddPolygon(prevLayer.parts[n].outline[0], ClipperLib.PolyType.ptClip);
            }

            Polygons islands = new Polygons();
            bridgeClip.Execute(ClipperLib.ClipType.ctIntersection, islands);
            if (islands.Count > 5)
            {
                return -1;
            }

            //Next find the 2 largest islands that we rest on.
            double area1 = 0;
            double area2 = 0;
            int idx1 = -1;
            int idx2 = -1;
            for (int n = 0; n < islands.Count; n++)
            {
                double area = Math.Abs(Clipper.Area(islands[n]));
                if (area > area1)
                {
                    if (area1 > area2)
                    {
                        area2 = area1;
                        idx2 = idx1;
                    }
                    area1 = area;
                    idx1 = n;
                }
                else if (area > area2)
                {
                    area2 = area;
                    idx2 = n;
                }
            }

            if (idx1 < 0 || idx2 < 0)
                return -1;

            IntPoint center1 = islands[idx1].centerOfMass();
            IntPoint center2 = islands[idx2].centerOfMass();

            double angle = Math.Atan2(center2.X - center1.X, center2.Y - center1.Y) / Math.PI * 180;
            if (angle < 0) angle += 360;
            return angle;
        }
    }
}