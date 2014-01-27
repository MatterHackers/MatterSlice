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
    using Polygons = List<IntPoint>;
    using PolygonRef = Polygon;

    public class SupportPolyGenerator
    {
        public Polygons polygons;

        SupportStorage storage;
        double cosAngle;
        int z;
        int supportZDistance;
        bool everywhere;
        int done;

        void swap(int p0, int p1)
        {
            int tmp = p0;
            p0 = p1;
            p1 = tmp;
        }

        public int cmp_SupportPoint(SupportPoint a, SupportPoint b)
        {
            return a.z - b.z;
        }

        public void generateSupportGrid(SupportStorage storage, OptimizedModel om, int supportAngle, bool supportEverywhere, int supportXYDistance, int supportZDistance)
{
    storage.generated = false;
    if (supportAngle < 0)
        return;
    storage.generated = true;
    
    storage.gridOffset.X = om.vMin.x;
    storage.gridOffset.Y = om.vMin.y;
    storage.gridScale = 200;
    storage.gridWidth = (om.modelSize.x / storage.gridScale) + 1;
    storage.gridHeight = (om.modelSize.y / storage.gridScale) + 1;
    storage.grid = new List<SupportPoint>[storage.gridWidth * storage.gridHeight];
    storage.angle = supportAngle;
    storage.everywhere = supportEverywhere;
    storage.XYDistance = supportXYDistance;
    storage.ZDistance = supportZDistance;

    for(unsigned int volumeIdx = 0; volumeIdx < om.volumes.size(); volumeIdx++)
    {
        OptimizedVolume* vol = &om.volumes[volumeIdx];
        for(unsigned int faceIdx = 0; faceIdx < vol.faces.size(); faceIdx++)
        {
            OptimizedFace* face = &vol.faces[faceIdx];
            Point3 v0 = vol.points[face.index[0]].p;
            Point3 v1 = vol.points[face.index[1]].p;
            Point3 v2 = vol.points[face.index[2]].p;
            
            Point3 normal = (v1 - v0).cross(v2 - v0);
            int normalSize = normal.vSize();
            
            double cosAngle = fabs(double(normal.z) / double(normalSize));
            
            v0.x = (v0.x - storage.gridOffset.X) / storage.gridScale;
            v0.y = (v0.y - storage.gridOffset.Y) / storage.gridScale;
            v1.x = (v1.x - storage.gridOffset.X) / storage.gridScale;
            v1.y = (v1.y - storage.gridOffset.Y) / storage.gridScale;
            v2.x = (v2.x - storage.gridOffset.X) / storage.gridScale;
            v2.y = (v2.y - storage.gridOffset.Y) / storage.gridScale;

            if (v0.x > v1.x) swap(v0, v1);
            if (v1.x > v2.x) swap(v1, v2);
            if (v0.x > v1.x) swap(v0, v1);
            for(long x=v0.x; x<v1.x; x++)
            {
                long y0 = v0.y + (v1.y - v0.y) * (x - v0.x) / (v1.x - v0.x);
                long y1 = v0.y + (v2.y - v0.y) * (x - v0.x) / (v2.x - v0.x);
                long z0 = v0.z + (v1.z - v0.z) * (x - v0.x) / (v1.x - v0.x);
                long z1 = v0.z + (v2.z - v0.z) * (x - v0.x) / (v2.x - v0.x);

                if (y0 > y1) { swap(y0, y1); swap(z0, z1); }
                for(int y=y0; y<y1; y++)
                    storage.grid[x+y*storage.gridWidth].push_back(SupportPoint(z0 + (z1 - z0) * (y-y0) / (y1-y0), cosAngle));
            }
            
            for(int x=v1.x; x<v2.x; x++)
            {
                long y0 = v1.y + (v2.y - v1.y) * (x - v1.x) / (v2.x - v1.x);
                long y1 = v0.y + (v2.y - v0.y) * (x - v0.x) / (v2.x - v0.x);
                long z0 = v1.z + (v2.z - v1.z) * (x - v1.x) / (v2.x - v1.x);
                long z1 = v0.z + (v2.z - v0.z) * (x - v0.x) / (v2.x - v0.x);

                if (y0 > y1) { swap(y0, y1); swap(z0, z1); }
                for(int y=y0; y<y1; y++)
                    storage.grid[x+y*storage.gridWidth].push_back(SupportPoint(z0 + (z1 - z0) * (y-y0) / (y1-y0), cosAngle));
            }
        }
    }
    
    for(int x=0; x<storage.gridWidth; x++)
    {
        for(int y=0; y<storage.gridHeight; y++)
        {
            unsigned int n = x+y*storage.gridWidth;
            qsort(storage.grid[n].data(), storage.grid[n].size(), sizeof(SupportPoint), cmp_SupportPoint);
        }
    }
    storage.gridOffset.X += storage.gridScale / 2;
    storage.gridOffset.Y += storage.gridScale / 2;
}

        public bool needSupportAt(Point p)
{
    if (p.X < 1) return false;
    if (p.Y < 1) return false;
    if (p.X >= storage.gridWidth - 1) return false;
    if (p.Y >= storage.gridHeight - 1) return false;
    if (done[p.X + p.Y * storage.gridWidth]) return false;
    
    unsigned int n = p.X+p.Y*storage.gridWidth;
    
    if (everywhere)
    {
        bool ok = false;
        for(unsigned int i=0; i<storage.grid[n].size(); i+=2)
        {
            if (storage.grid[n][i].cosAngle >= cosAngle && storage.grid[n][i].z - supportZDistance >= z && (i == 0 || storage.grid[n][i-1].z + supportZDistance < z))
            {
                ok = true;
                break;
            }
        }
        if (!ok) return false;
    }else{
        if (storage.grid[n].size() < 1) return false;
        if (storage.grid[n][0].cosAngle < cosAngle) return false;
        if (storage.grid[n][0].z - supportZDistance < z) return false;
    }
    return true;
}

        public void lazyFill(Point startPoint)
{
    static int nr = 0;
    nr++;
    PolygonRef poly = polygons.newPoly();
    Polygon tmpPoly;

    while(1)
    {
        Point p = startPoint;
        done[p.X + p.Y * storage.gridWidth] = nr;
        while(needSupportAt(p + Point(1, 0)))
        {
            p.X ++;
            done[p.X + p.Y * storage.gridWidth] = nr;
        }
        tmpPoly.add(startPoint * storage.gridScale + storage.gridOffset - Point(storage.gridScale/2, 0));
        poly.add(p * storage.gridScale + storage.gridOffset);
        startPoint.Y++;
        while(!needSupportAt(startPoint) && startPoint.X <= p.X)
            startPoint.X ++;
        if (startPoint.X > p.X)
        {
            for(unsigned int n=0;n<tmpPoly.size();n++)
            {
                poly.add(tmpPoly[tmpPoly.size()-n-1]);
            }
            polygons.add(poly);
            return;
        }
        while(needSupportAt(startPoint - Point(1, 0)) && startPoint.X > 1)
            startPoint.X --;
    }
}

        public SupportPolyGenerator(SupportStorage storage, int z)
{
    this.storage = storage;
this.z = z;
this.everywhere = storage.everywhere;

    if (!storage.generated)
        return;
    
    cosAngle = cos(double(90 - storage.angle) / 180.0 * M_PI) - 0.01;
    this.supportZDistance = storage.ZDistance;

    done = new int[storage.gridWidth*storage.gridHeight];
    memset(done, 0, sizeof(int) * storage.gridWidth*storage.gridHeight);
    
    for(int y=1; y<storage.gridHeight; y++)
    {
        for(int x=1; x<storage.gridWidth; x++)
        {
            if (!needSupportAt(Point(x, y)) || done[x + y * storage.gridWidth]) continue;
            
            lazyFill(Point(x, y));
        }
    }

    done = null;
    
    polygons = polygons.offset(storage.XYDistance);
}

    }
}

