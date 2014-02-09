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
using System.IO;
using System.Collections.Generic;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Point = IntPoint;
    using PolygonRef = Polygon;

    public class PathOrderOptimizer
    {
        public Point startPoint;
        public List<PolygonRef> polygons;
        public List<int> polyStart;
        public List<int> polyOrder;

        public PathOrderOptimizer(Point startPoint)
        {
            this.startPoint = startPoint;
        }

        public void addPolygon(PolygonRef polygon)
        {
            this.polygons.Add(polygon);
        }

        public void addPolygons(Polygons polygons)
        {
            for (int i = 0; i < polygons.Count; i++)
                this.polygons.Add(polygons[i]);
        }

        public void optimize()
{
    List<bool> picked = new List<bool>();
    for(int i=0;i<polygons.Count; i++)
    {
        int best = -1;
        float bestDist = float.MaxValue;
        PolygonRef poly = polygons[i];
        for( int j=0; j<poly.Count; j++)
        {
            float dist = (poly[j] - startPoint).vSize2f();
            if (dist < bestDist)
            {
                best = j;
                bestDist = dist;
            }
        }
        polyStart.Add(best);
        picked.Add(false);
    }

    Point p0 = startPoint;
    for( int n=0; n<polygons.Count; n++)
    {
        int best = -1;
        float bestDist = float.MaxValue;
        for( int i=0;i<polygons.Count; i++)
        {
            if (picked[i] || polygons[i].Count < 1)
                continue;
            if (polygons[i].Count == 2)
            {
                float dist = (polygons[i][0] - p0).vSize2f();
                if (dist < bestDist)
                {
                    best = i;
                    bestDist = dist;
                    polyStart[i] = 0;
                }
                dist = (polygons[i][1] - p0).vSize2f();
                if (dist < bestDist)
                {
                    best = i;
                    bestDist = dist;
                    polyStart[i] = 1;
                }
            }else{
                float dist = (polygons[i][polyStart[i]] - p0).vSize2f();
                if (dist < bestDist)
                {
                    best = i;
                    bestDist = dist;
                }
            }
        }
        if (best > -1)
        {
            if (polygons[best].Count == 2)
            {
                p0 = polygons[best][(polyStart[best] + 1) % 2];
            }else{
                p0 = polygons[best][polyStart[best]];
            }
            picked[best] = true;
            polyOrder.Add(best);
        }
    }
    
    p0 = startPoint;
    for( int n=0; n<polyOrder.Count; n++)
    {
        int nr = polyOrder[n];
        int best = -1;
        float bestDist = float.MaxValue;
        for( int i=0;i<polygons[nr].Count; i++)
        {
            float dist = (polygons[nr][i] - p0).vSize2f();
            if (dist < bestDist)
            {
                best = i;
                bestDist = dist;
            }
        }
        polyStart[nr] = best;
        if (polygons[nr].Count <= 2)
        {
            p0 = polygons[nr][(best + 1) % 2];
        }else{
            p0 = polygons[nr][best];
        }
    }
        }
    }
}
