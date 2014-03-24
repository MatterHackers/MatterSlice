/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

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
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class Infill
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

                outline = outline.Offset(-offsets[step]);
                if (outline.Count < 1)
                {
                    break;
                }

                step = (step + 1) % offsetsSize;
            }
        }

        public static void generateLineInfill(Polygons in_outline, Polygons result, int extrusionWidth, int lineSpacing, int infillExtendIntoPerimeter_µm, double rotation)
        {
            Polygons outlines = in_outline.Offset(infillExtendIntoPerimeter_µm);
            PointMatrix matrix = new PointMatrix(rotation);

            outlines.applyMatrix(matrix);

            AABB boundary = new AABB(outlines);

            boundary.min.X = ((boundary.min.X / lineSpacing) - 1) * lineSpacing;
            int lineCount = (int)((boundary.max.X - boundary.min.X + (lineSpacing - 1)) / lineSpacing);
            List<List<long>> cutList = new List<List<long>>();
            for (int n = 0; n < lineCount; n++)
            {
                cutList.Add(new List<long>());
            }

            for (int outlineIndex = 0; outlineIndex < outlines.Count; outlineIndex++)
            {
                Polygon currentOutline = outlines[outlineIndex];
                IntPoint lastPoint = currentOutline[currentOutline.Count - 1];
                for (int pointIndex = 0; pointIndex < currentOutline.Count; pointIndex++)
                {
                    IntPoint currentPoint = currentOutline[pointIndex];
                    int idx0 = (int)((currentPoint.X - boundary.min.X) / lineSpacing);
                    int idx1 = (int)((lastPoint.X - boundary.min.X) / lineSpacing);
                    
                    long xMin = Math.Min(currentPoint.X, lastPoint.X);
                    long xMax = Math.Max(currentPoint.X, lastPoint.X);

                    if (currentPoint.X > lastPoint.X)
                    {
                        xMin = lastPoint.X; 
                        xMax = currentPoint.X; 
                    }

                    if (idx0 > idx1) 
                    {
                        int tmp = idx0; 
                        idx0 = idx1; 
                        idx1 = tmp; 
                    }

                    for (int idx = idx0; idx <= idx1; idx++)
                    {
                        int x = (int)((idx * lineSpacing) + boundary.min.X + lineSpacing / 2);
                        if (x < xMin) continue;
                        if (x >= xMax) continue;
                        int y = (int)(currentPoint.Y + (lastPoint.Y - currentPoint.Y) * (x - currentPoint.X) / (lastPoint.X - currentPoint.X));
                        cutList[idx].Add(y);
                    }

                    lastPoint = currentPoint;
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
                    result.Add(p);
                    p.Add(matrix.unapply(new IntPoint(x, cutList[idx2][i])));
                    p.Add(matrix.unapply(new IntPoint(x, cutList[idx2][i + 1])));
                }

                idx2 += 1;
            }
        }
    }
}