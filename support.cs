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
        public Polygons interfacePolygons = new Polygons();
        public Polygons supportPolygons = new Polygons();

        SupportStorage supportStorage = new SupportStorage();
        double cosAngle;
        int supportZDistance_um;
        int interfaceZDistance_um;
        bool generateInternalSupport;
        bool[] done;

        int extraErrorGap = 20;
        public bool needSupportAt(IntPoint pointToCheckIfNeedsSupport, int z)
        {
            if (pointToCheckIfNeedsSupport.X < 1
                || pointToCheckIfNeedsSupport.Y < 1
                || pointToCheckIfNeedsSupport.X >= supportStorage.gridWidth - 1
                || pointToCheckIfNeedsSupport.Y >= supportStorage.gridHeight - 1
                || done[pointToCheckIfNeedsSupport.X + pointToCheckIfNeedsSupport.Y * supportStorage.gridWidth])
            {
                return false;
            }

            int gridIndex = (int)(pointToCheckIfNeedsSupport.X + pointToCheckIfNeedsSupport.Y * supportStorage.gridWidth);

            if (generateInternalSupport)
            {
                // We add 2 each time as we are wanting to look at the bottom face (even faces), and the intersections are bottom - top - bottom - top - etc.
                for (int zIndex = 0; zIndex < supportStorage.xYGridOfSupportPoints[gridIndex].Count; zIndex += 2)
                {
                    SupportPoint currentBottomSupportPoint = supportStorage.xYGridOfSupportPoints[gridIndex][zIndex];
                    bool angleNeedsSupport = currentBottomSupportPoint.cosAngle >= cosAngle;
                    if (angleNeedsSupport)
                    {
                        bool zIsBelowBottomSupportPoint = z <= currentBottomSupportPoint.z - interfaceZDistance_um - supportZDistance_um - extraErrorGap;
                        if (zIndex == 0)
                        {
                            if (zIsBelowBottomSupportPoint)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            SupportPoint previousTopSupportPoint = supportStorage.xYGridOfSupportPoints[gridIndex][zIndex - 1];
                            bool zIsAbovePrevSupportPoint = z > previousTopSupportPoint.z + supportZDistance_um;
                            if (zIsBelowBottomSupportPoint && zIsAbovePrevSupportPoint)
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
                if (supportStorage.xYGridOfSupportPoints[gridIndex].Count == 0)
                {
                    // there are no points needing support here
                    return false;
                }

                if (supportStorage.xYGridOfSupportPoints[gridIndex][0].cosAngle < cosAngle)
                {
                    // The angle does not need support
                    return false;
                }

                if (z >= supportStorage.xYGridOfSupportPoints[gridIndex][0].z - interfaceZDistance_um - supportZDistance_um - extraErrorGap)
                {
                    // the spot is above the place we need to support
                    return false;
                }
            }

            return true;
        }

        public bool needInterfaceAt(IntPoint pointToCheckIfNeedsSupport, int z)
        {
            if (pointToCheckIfNeedsSupport.X < 1
                || pointToCheckIfNeedsSupport.Y < 1
                || pointToCheckIfNeedsSupport.X >= supportStorage.gridWidth - 1
                || pointToCheckIfNeedsSupport.Y >= supportStorage.gridHeight - 1
                || done[pointToCheckIfNeedsSupport.X + pointToCheckIfNeedsSupport.Y * supportStorage.gridWidth])
            {
                return false;
            }

            int gridIndex = (int)(pointToCheckIfNeedsSupport.X + pointToCheckIfNeedsSupport.Y * supportStorage.gridWidth);

            if (generateInternalSupport)
            {
                // We add 2 each time as we are wanting to look at the bottom face (even faces), and the intersections are bottom - top - bottom - top - etc.
                for (int zIndex = 0; zIndex < supportStorage.xYGridOfSupportPoints[gridIndex].Count; zIndex += 2)
                {
                    SupportPoint currentBottomSupportPoint = supportStorage.xYGridOfSupportPoints[gridIndex][zIndex];
                    bool angleNeedsSupport = currentBottomSupportPoint.cosAngle >= cosAngle;
                    if (angleNeedsSupport)
                    {
                        bool zIsBelowSupportPoint = z <= currentBottomSupportPoint.z - supportZDistance_um - extraErrorGap;
                        bool zIsWithinInterfaceGap = z >= currentBottomSupportPoint.z - supportZDistance_um - extraErrorGap - interfaceZDistance_um;
                        if (zIndex == 0)
                        {
                            if (zIsBelowSupportPoint && zIsWithinInterfaceGap)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            SupportPoint previousTopSupportPoint = supportStorage.xYGridOfSupportPoints[gridIndex][zIndex - 1];
                            bool zIsAbovePrevSupportPoint = z > previousTopSupportPoint.z + supportZDistance_um;
                            if (zIsBelowSupportPoint
                                && zIsWithinInterfaceGap
                                && zIsAbovePrevSupportPoint)
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
                if (supportStorage.xYGridOfSupportPoints[gridIndex].Count == 0)
                {
                    // there are no points needing support here
                    return false;
                }

                if (supportStorage.xYGridOfSupportPoints[gridIndex][0].cosAngle < cosAngle)
                {
                    // The angle does not need support
                    return false;
                }

                if (z >= supportStorage.xYGridOfSupportPoints[gridIndex][0].z - supportZDistance_um - extraErrorGap)
                {
                    // the spot is above the place we need to support
                    return false;
                }

                if (z < supportStorage.xYGridOfSupportPoints[gridIndex][0].z - interfaceZDistance_um - supportZDistance_um - extraErrorGap)
                {
                    // the spot is not within the interface gap
                    return false;
                }
            }

            return true;
        }

        public void lazyFill(Polygons polysToWriteTo, IntPoint startPoint, int z)
        {
            Polygon poly = new Polygon();
            polysToWriteTo.Add(poly);

            Polygon tmpPoly = new Polygon();

            while (true)
            {
                IntPoint p = startPoint;
                done[p.X + p.Y * supportStorage.gridWidth] = true;
                while (needSupportAt(p + new IntPoint(1, 0), z))
                {
                    p.X++;
                    done[p.X + p.Y * supportStorage.gridWidth] = true;
                }
                tmpPoly.Add(startPoint * supportStorage.gridScale + supportStorage.gridOffset - new IntPoint(supportStorage.gridScale / 2, 0));
                poly.Add(p * supportStorage.gridScale + supportStorage.gridOffset);
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
                    polysToWriteTo.Add(poly);
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
            this.supportStorage = storage;
            this.generateInternalSupport = storage.generateInternalSupport;

            if (!storage.generated)
            {
                return;
            }

            cosAngle = Math.Cos((double)(storage.endAngle) / 180.0 * Math.PI) - 0.01;
            this.supportZDistance_um = storage.supportZDistance_um;
            this.interfaceZDistance_um = 0;
            //this.interfaceZDistance_um = 3 * supportZDistance_um;

            done = new bool[storage.gridWidth * storage.gridHeight];

            for (int y = 1; y < storage.gridHeight; y++)
            {
                for (int x = 1; x < storage.gridWidth; x++)
                {
                    if (!done[x + y * storage.gridWidth])
                    {
                        if (needSupportAt(new IntPoint(x, y), z))
                        {
                            lazyFill(supportPolygons, new IntPoint(x, y), z);
                        }
#if false
                        //else if (needInterfaceAt(new IntPoint(x, y), z))
                        {
                            //lazyFill(supportPolygons, new IntPoint(x, y), z);
                            //lazyFill(interfacePolygons, new IntPoint(x, y), z);
                        }
#endif
                    }
                }
            }

            supportPolygons = supportPolygons.Offset(storage.supportXYDistance_um);
            interfacePolygons = supportPolygons.Offset(storage.supportXYDistance_um);
        }
    }
}

