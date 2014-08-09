/*
Copyright (c) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
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

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class SupportPolyGenerator
    {
        public Polygons polygons = new Polygons();

        SupportStorage storage = new SupportStorage();
        double cosAngle;
        int supportZDistance;
        bool generateInternalSupport;
        int[] done;

        static void swap(ref int p0, ref int p1)
        {
            int tmp = p0;
            p0 = p1;
            p1 = tmp;
        }

        static void swap(ref long p0, ref long p1)
        {
            long tmp = p0;
            p0 = p1;
            p1 = tmp;
        }

        static void swap(ref Point3 p0, ref Point3 p1)
        {
            Point3 tmp = p0;
            p0 = p1;
            p1 = tmp;
        }

        public int cmp_SupportPoint(SupportPoint a, SupportPoint b)
        {
            return a.z - b.z;
        }

        public static void generateSupportGrid(SupportStorage storage, OptimizedModel model, ConfigSettings config)
        {
            storage.generated = false;
            if (config.supportEndAngle < 0)
            {
                return;
            }

            storage.generated = true;

            storage.gridOffset.X = model.minXYZ.x;
            storage.gridOffset.Y = model.minXYZ.y;
            storage.gridScale = 200;
            storage.gridWidth = (model.size.x / storage.gridScale) + 1;
            storage.gridHeight = (model.size.y / storage.gridScale) + 1;
            int gridSize = storage.gridWidth * storage.gridHeight;
            storage.xYGridOfSupportPoints = new List<List<SupportPoint>>(gridSize);
            for(int i=0; i<gridSize; i++)
            {
                storage.xYGridOfSupportPoints.Add(new List<SupportPoint>());
            }

            storage.endAngle = config.supportEndAngle;
            storage.generateInternalSupport = config.generateInternalSupport;
            storage.XYDistance = config.supportXYDistance_um;
            storage.supportZDistance = config.supportNumberOfLayersToSkipInZ * config.layerThickness_um;

            for (int volumeIndex = 0; volumeIndex < model.volumes.Count; volumeIndex++)
            {
                OptimizedVolume vol = model.volumes[volumeIndex];
                for (int faceIndex = 0; faceIndex < vol.facesTriangle.Count; faceIndex++)
                {
                    OptimizedFace faceTriangle = vol.facesTriangle[faceIndex];
                    Point3 v0 = vol.vertices[faceTriangle.vertexIndex[0]].position;
                    Point3 v1 = vol.vertices[faceTriangle.vertexIndex[1]].position;
                    Point3 v2 = vol.vertices[faceTriangle.vertexIndex[2]].position;

                    Point3 normal = (v1 - v0).cross(v2 - v0);
                    int normalSize = normal.vSize();

                    double cosAngle = Math.Abs((double)(normal.z) / (double)(normalSize));

                    v0.x = (int)((v0.x - storage.gridOffset.X) / storage.gridScale + .5);
                    v0.y = (int)((v0.y - storage.gridOffset.Y) / storage.gridScale + .5);
                    v1.x = (int)((v1.x - storage.gridOffset.X) / storage.gridScale + .5);
                    v1.y = (int)((v1.y - storage.gridOffset.Y) / storage.gridScale + .5);
                    v2.x = (int)((v2.x - storage.gridOffset.X) / storage.gridScale + .5);
                    v2.y = (int)((v2.y - storage.gridOffset.Y) / storage.gridScale + .5);

                    if (v0.x > v1.x) swap(ref v0, ref v1);
                    if (v1.x > v2.x) swap(ref v1, ref v2);
                    if (v0.x > v1.x) swap(ref v0, ref v1);
                    for (long x = v0.x; x < v1.x; x++)
                    {
                        long y0 = (long)(v0.y + (v1.y - v0.y) * (x - v0.x) / (double)(v1.x - v0.x) + .5);
                        long y1 = (long)(v0.y + (v2.y - v0.y) * (x - v0.x) / (double)(v2.x - v0.x) + .5);
                        long z0 = (long)(v0.z + (v1.z - v0.z) * (x - v0.x) / (double)(v1.x - v0.x) + .5);
                        long z1 = (long)(v0.z + (v2.z - v0.z) * (x - v0.x) / (double)(v2.x - v0.x) + .5);

                        if (y0 > y1) 
                        {
                            swap(ref y0, ref y1);
                            swap(ref z0, ref z1); 
                        }

                        for (long y = y0; y < y1; y++)
                        {
                            SupportPoint newSupportPoint = new SupportPoint((int)(z0 + (z1 - z0) * (y - y0) / (double)(y1 - y0) + .5), cosAngle);
                            storage.xYGridOfSupportPoints[(int)(x + y * storage.gridWidth)].Add(newSupportPoint);
                        }
                    }

                    for (int x = v1.x; x < v2.x; x++)
                    {
                        long y0 = (long)(v1.y + (v2.y - v1.y) * (x - v1.x) / (double)(v2.x - v1.x) + .5);
                        long y1 = (long)(v0.y + (v2.y - v0.y) * (x - v0.x) / (double)(v2.x - v0.x) + .5);
                        long z0 = (long)(v1.z + (v2.z - v1.z) * (x - v1.x) / (double)(v2.x - v1.x) + .5);
                        long z1 = (long)(v0.z + (v2.z - v0.z) * (x - v0.x) / (double)(v2.x - v0.x) + .5);

                        if (y0 > y1) 
                        {
                            swap(ref y0, ref y1);
                            swap(ref z0, ref z1); 
                        }

                        for (int y = (int)y0; y < y1; y++)
                        {
                            storage.xYGridOfSupportPoints[x + y * storage.gridWidth].Add(new SupportPoint((int)(z0 + (z1 - z0) * (double)(y - y0) / (y1 - y0)+.5), cosAngle));
                        }
                    }
                }
            }

            for (int x = 0; x < storage.gridWidth; x++)
            {
                for (int y = 0; y < storage.gridHeight; y++)
                {
                    int n = x + y * storage.gridWidth;
                    storage.xYGridOfSupportPoints[n].Sort(SortSupportsOnZ);
                }
            }
            storage.gridOffset.X += storage.gridScale / 2;
            storage.gridOffset.Y += storage.gridScale / 2;
        }

        private static int SortSupportsOnZ(SupportPoint one, SupportPoint two)
        {
            return one.z.CompareTo(two.z);
        }

        public bool needSupportAt(IntPoint pointToCheckIfNeedsSupport, int z)
        {
            if (pointToCheckIfNeedsSupport.X < 1 
                || pointToCheckIfNeedsSupport.Y < 1 
                || pointToCheckIfNeedsSupport.X >= storage.gridWidth - 1 
                || pointToCheckIfNeedsSupport.Y >= storage.gridHeight - 1
                || done[pointToCheckIfNeedsSupport.X + pointToCheckIfNeedsSupport.Y * storage.gridWidth] != 0)
            {
                return false;
            }

            int gridIndex = (int)(pointToCheckIfNeedsSupport.X + pointToCheckIfNeedsSupport.Y * storage.gridWidth);

            if (generateInternalSupport)
            {
                for (int zIndex = 0; zIndex < storage.xYGridOfSupportPoints[gridIndex].Count; zIndex += 2)
                {
                    bool angleNeedsSupport = storage.xYGridOfSupportPoints[gridIndex][zIndex].cosAngle >= cosAngle;
                    if (angleNeedsSupport)
                    {
                        bool zIsBelowSupportPoint = z < storage.xYGridOfSupportPoints[gridIndex][zIndex].z - supportZDistance;
                        if (zIndex == 0)
                        {
                            if (zIsBelowSupportPoint)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            bool zIsAbovePrevSupportPoint = z > storage.xYGridOfSupportPoints[gridIndex][zIndex - 1].z + supportZDistance;
                            if (zIsBelowSupportPoint && zIsAbovePrevSupportPoint)
                            {
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            else // we only ever look up to the first point needing support (the 0th index)
            {
                if (storage.xYGridOfSupportPoints[gridIndex].Count == 0)
                {
                    // there are no points needing support here
                    return false;
                }

                if (storage.xYGridOfSupportPoints[gridIndex][0].cosAngle < cosAngle)
                {
                    // The angle does not need support
                    return false;
                }

                if (z >= storage.xYGridOfSupportPoints[gridIndex][0].z - supportZDistance)
                {
                    // the spot is above the place we need to support
                    return false;
                }
            }

            return true;
        }

        public void lazyFill(IntPoint startPoint, int z)
        {
            int nr = 0;
            nr++;
            Polygon poly = new Polygon();
            polygons.Add(poly);

            Polygon tmpPoly = new Polygon();

            while (true)
            {
                IntPoint p = startPoint;
                done[p.X + p.Y * storage.gridWidth] = nr;
                while (needSupportAt(p + new IntPoint(1, 0), z))
                {
                    p.X++;
                    done[p.X + p.Y * storage.gridWidth] = nr;
                }
                tmpPoly.Add(startPoint * storage.gridScale + storage.gridOffset - new IntPoint(storage.gridScale / 2, 0));
                poly.Add(p * storage.gridScale + storage.gridOffset);
                startPoint.Y++;
                while (!needSupportAt(startPoint, z) && startPoint.X <= p.X)
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

                while (needSupportAt(startPoint - new IntPoint(1, 0), z) && startPoint.X > 1)
                {
                    startPoint.X--;
                }
            }
        }

        public SupportPolyGenerator(SupportStorage storage, int z)
        {
            this.storage = storage;
            this.generateInternalSupport = storage.generateInternalSupport;

            if (!storage.generated)
            {
                return;
            }

            cosAngle = Math.Cos((double)(storage.endAngle) / 180.0 * Math.PI) - 0.01;
            this.supportZDistance = storage.supportZDistance;

            done = new int[(int)(storage.gridWidth * storage.gridHeight)];

            for (int y = 1; y < storage.gridHeight; y++)
            {
                for (int x = 1; x < storage.gridWidth; x++)
                {
                    if (!needSupportAt(new IntPoint(x, y), z) || done[x + y * storage.gridWidth] != 0)
                    {
                        continue;
                    }

                    lazyFill(new IntPoint(x, y), z);
                }
            }

            done = null;

            polygons = polygons.Offset(storage.XYDistance);
        }
    }
}

