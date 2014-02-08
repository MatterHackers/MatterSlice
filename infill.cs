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
    using Polygon = List<IntPoint>;
    using Polygons = List<Polygon>;
    using PolygonRef = Polygon;

	public static class Infill
	{
public static void generateConcentricInfill(Polygons outline, Polygons result, int offsets[], int offsetsSize)
{
    int step = 0;
    while(1)
    {
        for(int polygonNr=0; polygonNr<outline.size(); polygonNr++)
            result.add(outline[polygonNr]);
        outline = outline.offset(-offsets[step]);
        if (outline.size() < 1)
            break;
        step = (step + 1) % offsetsSize;
    }
}

int compare_long(long a, long b)
{
    long n = a - long;
    if (n < 0) return -1;
    if (n > 0) return 1;
    return 0;
}

public static void generateLineInfill(const Polygons in_outline, Polygons result, int extrusionWidth, int lineSpacing, int infillOverlap, double rotation)
{
    Polygons outline = in_outline.offset(extrusionWidth * infillOverlap / 100);
    PointMatrix matrix(rotation);
    
    outline.applyMatrix(matrix);
    
    AABB boundary(outline);
    
    boundary.min.X = ((boundary.min.X / lineSpacing) - 1) * lineSpacing;
    int lineCount = (boundary.max.X - boundary.min.X + (lineSpacing - 1)) / lineSpacing;
    List<List<long> > cutList = new List<List<long>>();
    for(int n=0; n<lineCount; n++)
        cutList.push_back(List<long>());

    for(int polyNr=0; polyNr < outline.size(); polyNr++)
    {
        Point p1 = outline[polyNr][outline[polyNr].size()-1];
        for(int i=0; i < outline[polyNr].size(); i++)
        {
            Point p0 = outline[polyNr][i];
            int idx0 = (p0.X - boundary.min.X) / lineSpacing;
            int idx1 = (p1.X - boundary.min.X) / lineSpacing;
            long xMin = p0.X, xMax = p1.X;
            if (p0.X > p1.X) { xMin = p1.X; xMax = p0.X; }
            if (idx0 > idx1) { int tmp = idx0; idx0 = idx1; idx1 = tmp; }
            for(int idx = idx0; idx<=idx1; idx++)
            {
                int x = (idx * lineSpacing) + boundary.min.X + lineSpacing / 2;
                if (x < xMin) continue;
                if (x >= xMax) continue;
                int y = p0.Y + (p1.Y - p0.Y) * (x - p0.X) / (p1.X - p0.X);
                cutList[idx].push_back(y);
            }
            p1 = p0;
        }
    }
    
    int idx = 0;
    for(long x = boundary.min.X + lineSpacing / 2; x < boundary.max.X; x += lineSpacing)
    {
        qsort(cutList[idx].data(), cutList[idx].size(), sizeof(long), compare_long);
        for(int i = 0; i + 1 < cutList[idx].size(); i+=2)
        {
            if (cutList[idx][i+1] - cutList[idx][i] < extrusionWidth / 5)
                continue;
            PolygonRef p = result.newPoly();
            p.add(matrix.unapply(Point(x, cutList[idx][i])));
            p.add(matrix.unapply(Point(x, cutList[idx][i+1])));
        }
        idx += 1;
    }
}

	}
}
