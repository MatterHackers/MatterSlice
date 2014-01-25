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
    public static class infill
    {
        public static void generateConcentricInfill(Polygons outline, Polygons result, int[] offsets, int offsetsSize)
        {
            int step = 0;
            while (true)
            {
                for (int polygonNr = 0; polygonNr < outline.Count; polygonNr++)
                {
                    result.Add(outline[polygonNr]);
                }
                outline = Clipper.OffsetPolygons(outline, -offsets[step], ClipperLib.JoinType.jtSquare, 2, false);
                if (outline.Count < 1)
                {
                    break;
                }
                step = (step + 1) % offsetsSize;
            }
        }

        public static void generateLineInfill(Polygons in_outline, Polygons result, int extrusionWidth, int lineSpacing, int infillOverlap, double rotation)
        {
            Polygons outline;
            outline = Clipper.OffsetPolygons(in_outline, extrusionWidth * infillOverlap / 100, ClipperLib.JoinType.jtSquare, 2, false);
            PointMatrix matrix = new PointMatrix(rotation);

            matrix.apply(outline);

            AABB boundary = new AABB(outline);

            int lineCount = (int)((boundary.max.X - boundary.min.X + (lineSpacing - 1)) / lineSpacing);
            List<long>[] cutList = new List<long>[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                cutList[i] = new List<long>();
            }
            for (int polyNr = 0; polyNr < outline.Count; polyNr++)
            {
                IntPoint p1 = outline[polyNr][outline[polyNr].Count - 1];
                for (int i = 0; i < outline[polyNr].Count; i++)
                {
                    IntPoint p0 = outline[polyNr][i];
                    int idx0 = (int)(p0.X - boundary.min.X) / lineSpacing;
                    int idx1 = (int)(p1.X - boundary.min.X) / lineSpacing;
                    long xMin = p0.X, xMax = p1.X;
                    if (p0.X > p1.X) { xMin = p1.X; xMax = p0.X; }
                    if (idx0 > idx1) { int tmp = idx0; idx0 = idx1; idx1 = tmp; }
                    for (int idx = idx0; idx <= idx1; idx++)
                    {
                        int x = (int)((idx * lineSpacing) + boundary.min.X + lineSpacing / 2);
                        if (x < xMin) continue;
                        if (x >= xMax) continue;
                        int y = (int)(p0.Y + (p1.Y - p0.Y) * (x - p0.X) / (p1.X - p0.X));
                        cutList[idx].Add(y);
                    }
                    p1 = p0;
                }
            }

            int idx2 = 0;
            for (long x = boundary.min.X + lineSpacing / 2; x < boundary.max.X; x += lineSpacing)
            {
                cutList[idx2].Sort();
                for (int i = 0; i + 1 < cutList[idx2].Count; i += 2)
                {
                    if (cutList[idx2][i + 1] - cutList[idx2][i] < extrusionWidth / 5)
                    {
                        continue;
                    }
                    Polygon p = new Polygon();
                    p.Add(matrix.unapply(new IntPoint(x, cutList[idx2][i])));
                    p.Add(matrix.unapply(new IntPoint(x, cutList[idx2][i + 1])));
                    result.Add(p);
                }
                idx2 += 1;
            }
        }
    }
}
