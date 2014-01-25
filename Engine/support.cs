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
#if false
void swap(Point3 p0, Point3 p1)
{
    Point3 tmp = p0;
    p0 = p1;
    p1 = tmp;
}
void swap(long n, long m)
{
    long tmp = n;
    n = m;
    m = tmp;
}

int cmp_SupportPoint(void a, void b)
{
    return ((SupportPoint*)a).z - ((SupportPoint*)b).z;
}
#endif

    public class SupportPolyGenerator
    {
        public Polygons polygons;

        public SupportStorage storage;
        public double cosAngle;
        public int z;
        public int supportZDistance;
        public bool everywhere;
        public int[] done;

        bool needSupportAt(IntPoint p)
        {
            if (p.X < 1) return false;
            if (p.Y < 1) return false;
            if (p.X >= storage.gridWidth - 1) return false;
            if (p.Y >= storage.gridHeight - 1) return false;
            if (done[p.X + p.Y * storage.gridWidth] != 0) return false;

            int n = (int)(p.X + p.Y * storage.gridWidth);

            throw new NotImplementedException();
#if false
            if (everywhere)
            {
                bool ok = false;
                for (int i = 0; i < storage.grid[n].Count; i += 2)
                {
                    if (storage.grid[n][i].cosAngle >= cosAngle && storage.grid[n][i].z + supportZDistance >= z && (i == 0 || storage.grid[n][i - 1].z - supportZDistance < z))
                    {
                        ok = true;
                        break;
                    }
                }
                if (!ok) return false;
            }
            else
            {
                if (storage.grid[n].Count < 1) return false;
                if (storage.grid[n][0].cosAngle < cosAngle) return false;
                if (storage.grid[n][0].z - supportZDistance < z) return false;
            }
#endif
            return true;
        }

        int nr = 0;
        void lazyFill(IntPoint startPoint)
        {
            nr++;
            Polygon poly = new Polygon();
            Polygon tmpPoly = new Polygon();

            while (true)
            {
                IntPoint p = startPoint;
                done[p.X + p.Y * storage.gridWidth] = nr;
                while (needSupportAt(p + new IntPoint(1, 0)))
                {
                    p.X++;
                    done[p.X + p.Y * storage.gridWidth] = nr;
                }

                tmpPoly.Add(startPoint * storage.gridScale + storage.gridOffset - new IntPoint(storage.gridScale / 2, 0));
                poly.Add(p * storage.gridScale + storage.gridOffset);
                startPoint.Y++;
                while (!needSupportAt(startPoint) && startPoint.X <= p.X)
                {
                    startPoint.X++;
                }

                if (startPoint.X > p.X)
                {
                    for (int n = 0; n < tmpPoly.Count; n++)
                    {
                        poly.Add(tmpPoly[tmpPoly.Count - n - 1]);
                    }
                    polygons.Add(poly);
                    return;
                }

                while (needSupportAt(startPoint - new IntPoint(1, 0)) && startPoint.X > 1)
                {
                    startPoint.X--;
                }
            }
        }

        public SupportPolyGenerator(SupportStorage storage, int z, int angle, bool everywhere, int supportDistance, int supportZDistance)
        {
            this.storage = storage;
            this.z = z;
            this.everywhere = everywhere;
            cosAngle = Math.Cos((double)(90 - angle) / 180.0 * Math.PI) - 0.01;
            this.supportZDistance = supportZDistance;

            this.done = new int[storage.gridWidth * storage.gridHeight];

            for (int y = 1; y < storage.gridHeight; y++)
            {
                for (int x = 1; x < storage.gridWidth; x++)
                {
                    if (!needSupportAt(new IntPoint(x, y)) || done[(int)(x + y * storage.gridWidth)] != 0)
                    {
                        continue;
                    }

                    lazyFill(new IntPoint(x, y));
                }
            }

            done = null;

            Polygons tmpPolys, tmpPolys2;
            //Do a morphological opening.
            tmpPolys = Clipper.OffsetPolygons(polygons, storage.gridScale * 4, ClipperLib.JoinType.jtSquare, 2, false);
            tmpPolys2 = Clipper.OffsetPolygons(tmpPolys, -storage.gridScale * 8 - supportDistance, ClipperLib.JoinType.jtSquare, 2, false);
            polygons = Clipper.OffsetPolygons(tmpPolys2, storage.gridScale * 4, ClipperLib.JoinType.jtSquare, 2, false);

            /*
            if (xAxis)
            {
                for(int y=0; y<storage.gridHeight; y+=4)
                {
                    for(int x=0; x<storage.gridWidth; x++)
                    {
                        if (!needSupportAt(new IntPoint(x, y))) continue;
                        int startX = x;
                        while(x < storage.gridWidth && needSupportAt(new IntPoint(x, y)))
                        {
                            x ++;
                        }
                        x --;
                        if (x > startX)
                        {
                            new IntPoint p0(startX * storage.gridScale + storage.gridOffset.X, y * storage.gridScale + storage.gridOffset.Y);
                            new IntPoint p1(x * storage.gridScale + storage.gridOffset.X, y * storage.gridScale + storage.gridOffset.Y);
                            ClipperLib::Polygon p;
                            p.Add(p0);
                            p.Add(p1);
                            polygons.Add(p);
                        }
                    }
                }
            }else{
                for(int x=0; x<storage.gridWidth; x+=1)
                {
                    for(int y=0; y<storage.gridHeight; y++)
                    {
                        if (!needSupportAt(new IntPoint(x, y))) continue;
                    
                        int startY = y;
                        while(y < storage.gridHeight && needSupportAt(Point(x, y)))
                        {
                            y ++;
                        }
                        y --;
                        if (y > startY)
                        {
                            Point p0(x * storage.gridScale + storage.gridOffset.X, startY * storage.gridScale + storage.gridOffset.Y);
                            Point p1(x * storage.gridScale + storage.gridOffset.X, y * storage.gridScale + storage.gridOffset.Y);
                            ClipperLib::Polygon p;
                            p.Add(p0);
                            p.Add(p1);
                            polygons.Add(p);
                        }
                    }
                }
            }
            */
        }
    }
}
