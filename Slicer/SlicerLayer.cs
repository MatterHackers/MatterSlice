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
using System.IO;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class SlicerLayer
    {
        public List<SlicerSegment> segmentList = new List<SlicerSegment>();
        public Dictionary<int, int> faceTo2DSegmentIndex = new Dictionary<int, int>();

        public int z;
        public Polygons polygonList = new Polygons();
        public Polygons openPolygonList = new Polygons();

        public void makePolygons(OptimizedVolume optomizedMesh, ConfigConstants.REPAIR_OUTLINES outlineRepairTypes)
        {
            for (int startingSegmentIndex = 0; startingSegmentIndex < segmentList.Count; startingSegmentIndex++)
            {
                if (segmentList[startingSegmentIndex].addedToPolygon)
                {
                    continue;
                }

                Polygon poly = new Polygon();
                // We start by adding the start, as we will add ends from now on.
                poly.Add(segmentList[startingSegmentIndex].start);

                int segmentIndexBeingAdded = startingSegmentIndex;
                bool canClose;

                while (true)
                {
                    canClose = false;
                    segmentList[segmentIndexBeingAdded].addedToPolygon = true;
                    IntPoint addedSegmentEndPoint = segmentList[segmentIndexBeingAdded].end;
                    poly.Add(addedSegmentEndPoint);
                    int nextSegmentToCheckIndex = -1;
                    OptimizedFace face = optomizedMesh.facesTriangle[segmentList[segmentIndexBeingAdded].faceIndex];
                    for (int connectedFaceIndex = 0; connectedFaceIndex < 3; connectedFaceIndex++)
                    {
                        int testFaceIndex = face.touchingFaces[connectedFaceIndex];
                        if (testFaceIndex > -1)
                        {
                            // If the connected face has an edge that is in the segment list
                            if (faceTo2DSegmentIndex.ContainsKey(testFaceIndex))
                            {
                                int touchingSegmentIndex = faceTo2DSegmentIndex[testFaceIndex];
                                IntPoint foundSegmentStart = segmentList[touchingSegmentIndex].start;
                                IntPoint diff = addedSegmentEndPoint - foundSegmentStart;
                                if (diff.IsShorterThen(2))
                                {
                                    // if we have looped back around to where we started
                                    if (touchingSegmentIndex == startingSegmentIndex)
                                    {
                                        canClose = true;
                                    }

                                    // If this segment has already been added
                                    if (segmentList[touchingSegmentIndex].addedToPolygon)
                                    {
                                        continue;
                                    }

                                    nextSegmentToCheckIndex = touchingSegmentIndex;
                                }
                            }
                        }
                    }

                    if (nextSegmentToCheckIndex == -1)
                    {
                        break;
                    }

                    segmentIndexBeingAdded = nextSegmentToCheckIndex;
                }
                if (canClose)
                {
                    polygonList.Add(poly);
                }
                else
                {
                    openPolygonList.Add(poly);
                }
            }

            //Clear the segmentList to save memory, it is no longer needed after this point.
#if !DEBUG
            segmentList.Clear();
#endif

            //Connecting polygons that are not closed yet, as models are not always perfect manifold we need to join some stuff up to get proper polygons
            //First link up polygon ends that are within 2 microns.
            for (int i = 0; i < openPolygonList.Count; i++)
            {
                if (openPolygonList[i].Count < 1) continue;
                for (int j = 0; j < openPolygonList.Count; j++)
                {
                    if (openPolygonList[j].Count < 1) continue;

                    IntPoint diff = openPolygonList[i][openPolygonList[i].Count - 1] - openPolygonList[j][0];
                    long distSquared = (diff).LengthSquared();

                    if (distSquared < 2 * 2)
                    {
                        if (i == j)
                        {
                            polygonList.Add(openPolygonList[i]);
                            openPolygonList[i].Clear();
                            break;
                        }
                        else
                        {
                            for (int n = 0; n < openPolygonList[j].Count; n++)
                                openPolygonList[i].Add(openPolygonList[j][n]);

                            openPolygonList[j].Clear();
                        }
                    }
                }
            }

            //Next link up all the missing ends, closing up the smallest gaps first. This is an inefficient implementation which can run in O(n*n*n) time.
            while (true)
            {
                long bestScore = 10000 * 10000;
                int bestA = -1;
                int bestB = -1;
                bool reversed = false;
                for (int i = 0; i < openPolygonList.Count; i++)
                {
                    if (openPolygonList[i].Count < 1) continue;
                    for (int j = 0; j < openPolygonList.Count; j++)
                    {
                        if (openPolygonList[j].Count < 1) continue;

                        IntPoint diff1 = openPolygonList[i][openPolygonList[i].Count - 1] - openPolygonList[j][0];
                        long distSquared1 = (diff1).LengthSquared();
                        if (distSquared1 < bestScore)
                        {
                            bestScore = distSquared1;
                            bestA = i;
                            bestB = j;
                            reversed = false;
                        }

                        if (i != j)
                        {
                            IntPoint diff2 = openPolygonList[i][openPolygonList[i].Count - 1] - openPolygonList[j][openPolygonList[j].Count - 1];
                            long distSquared2 = (diff2).LengthSquared();
                            if (distSquared2 < bestScore)
                            {
                                bestScore = distSquared2;
                                bestA = i;
                                bestB = j;
                                reversed = true;
                            }
                        }
                    }
                }

                if (bestScore >= 10000 * 10000)
                {
                    break;
                }

                if (bestA == bestB)
                {
                    polygonList.Add(openPolygonList[bestA]);
                    openPolygonList[bestA].Clear();
                }
                else
                {
                    if (reversed)
                    {
                        if (openPolygonList[bestA].PolygonLength() > openPolygonList[bestB].PolygonLength())
                        {
                            if (openPolygonList[bestA].PolygonLength() > openPolygonList[bestB].PolygonLength())
                            {
                                for (int n = openPolygonList[bestB].Count - 1; n >= 0; n--)
                                    openPolygonList[bestA].Add(openPolygonList[bestB][n]);
                                openPolygonList[bestB].Clear();
                            }
                            else
                            {
                                for (int n = openPolygonList[bestA].Count - 1; n >= 0; n--)
                                    openPolygonList[bestB].Add(openPolygonList[bestA][n]);
                                openPolygonList[bestA].Clear();
                            }
                        }
                        else
                        {
                            for (int n = openPolygonList[bestA].Count - 1; n >= 0; n--)
                                openPolygonList[bestB].Add(openPolygonList[bestA][n]);
                            openPolygonList[bestB].Clear();
                        }
                    }
                    else
                    {
                        for (int n = 0; n < openPolygonList[bestB].Count; n++)
                            openPolygonList[bestA].Add(openPolygonList[bestB][n]);
                        openPolygonList[bestB].Clear();
                    }
                }
            }


            if ((outlineRepairTypes & ConfigConstants.REPAIR_OUTLINES.EXTENSIVE_STITCHING) == ConfigConstants.REPAIR_OUTLINES.EXTENSIVE_STITCHING)
            {
                //For extensive stitching find 2 open polygons that are touching 2 closed polygons.
                // Then find the sortest path over this polygon that can be used to connect the open polygons,
                // And generate a path over this shortest bit to link up the 2 open polygons.
                // (If these 2 open polygons are the same polygon, then the final result is a closed polyon)

                while (true)
                {
                    int bestA = -1;
                    int bestB = -1;
                    gapCloserResult bestResult = new gapCloserResult();
                    bestResult.len = long.MaxValue;
                    bestResult.polygonIdx = -1;
                    bestResult.pointIdxA = -1;
                    bestResult.pointIdxB = -1;

                    for (int i = 0; i < openPolygonList.Count; i++)
                    {
                        if (openPolygonList[i].Count < 1) continue;

                        {
                            gapCloserResult res = findPolygonGapCloser(openPolygonList[i][0], openPolygonList[i][openPolygonList[i].Count - 1]);
                            if (res.len > 0 && res.len < bestResult.len)
                            {
                                bestA = i;
                                bestB = i;
                                bestResult = res;
                            }
                        }

                        for (int j = 0; j < openPolygonList.Count; j++)
                        {
                            if (openPolygonList[j].Count < 1 || i == j) continue;

                            gapCloserResult res = findPolygonGapCloser(openPolygonList[i][0], openPolygonList[j][openPolygonList[j].Count - 1]);
                            if (res.len > 0 && res.len < bestResult.len)
                            {
                                bestA = i;
                                bestB = j;
                                bestResult = res;
                            }
                        }
                    }

                    if (bestResult.len < long.MaxValue)
                    {
                        if (bestA == bestB)
                        {
                            if (bestResult.pointIdxA == bestResult.pointIdxB)
                            {
                                polygonList.Add(openPolygonList[bestA]);
                                openPolygonList[bestA].Clear();
                            }
                            else if (bestResult.AtoB)
                            {
                                Polygon poly = new Polygon();
                                polygonList.Add(poly);
                                for (int j = bestResult.pointIdxA; j != bestResult.pointIdxB; j = (j + 1) % polygonList[bestResult.polygonIdx].Count)
                                {
                                    poly.Add(polygonList[bestResult.polygonIdx][j]);
                                }

                                for (int j = openPolygonList[bestA].Count - 1; (int)(j) >= 0; j--)
                                {
                                    poly.Add(openPolygonList[bestA][j]);
                                }

                                openPolygonList[bestA].Clear();
                            }
                            else
                            {
                                int n = polygonList.Count;
                                polygonList.Add(openPolygonList[bestA]);
                                for (int j = bestResult.pointIdxB; j != bestResult.pointIdxA; j = (j + 1) % polygonList[bestResult.polygonIdx].Count)
                                {
                                    polygonList[n].Add(polygonList[bestResult.polygonIdx][j]);
                                }
                                openPolygonList[bestA].Clear();
                            }
                        }
                        else
                        {
                            if (bestResult.pointIdxA == bestResult.pointIdxB)
                            {
                                for (int n = 0; n < openPolygonList[bestA].Count; n++)
                                {
                                    openPolygonList[bestB].Add(openPolygonList[bestA][n]);
                                }

                                openPolygonList[bestA].Clear();
                            }
                            else if (bestResult.AtoB)
                            {
                                Polygon poly = new Polygon();
                                for (int n = bestResult.pointIdxA; n != bestResult.pointIdxB; n = (n + 1) % polygonList[bestResult.polygonIdx].Count)
                                {
                                    poly.Add(polygonList[bestResult.polygonIdx][n]);
                                }

                                for (int n = poly.Count - 1; (int)(n) >= 0; n--)
                                {
                                    openPolygonList[bestB].Add(poly[n]);
                                }

                                for (int n = 0; n < openPolygonList[bestA].Count; n++)
                                {
                                    openPolygonList[bestB].Add(openPolygonList[bestA][n]);
                                }

                                openPolygonList[bestA].Clear();
                            }
                            else
                            {
                                for (int n = bestResult.pointIdxB; n != bestResult.pointIdxA; n = (n + 1) % polygonList[bestResult.polygonIdx].Count)
                                {
                                    openPolygonList[bestB].Add(polygonList[bestResult.polygonIdx][n]);
                                }

                                for (int n = openPolygonList[bestA].Count - 1; n >= 0; n--)
                                {
                                    openPolygonList[bestB].Add(openPolygonList[bestA][n]);
                                }

                                openPolygonList[bestA].Clear();
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if ((outlineRepairTypes & ConfigConstants.REPAIR_OUTLINES.KEEP_NON_CLOSED) == ConfigConstants.REPAIR_OUTLINES.KEEP_NON_CLOSED)
            {
                for (int n = 0; n < openPolygonList.Count; n++)
                {
                    if (openPolygonList[n].Count > 0)
                    {
                        polygonList.Add(openPolygonList[n]);
                    }
                }
            }

            //Remove all the tiny polygons, or polygons that are not closed. As they do not contribute to the actual print.
            int snapDistance = 1000;
            for (int polygonIndex = 0; polygonIndex < polygonList.Count; polygonIndex++)
            {
                int length = 0;

                for (int intPointIndex = 1; intPointIndex < polygonList[polygonIndex].Count; intPointIndex++)
                {
                    length += (polygonList[polygonIndex][intPointIndex] - polygonList[polygonIndex][intPointIndex - 1]).vSize();
                    if (length > snapDistance)
                    {
                        break;
                    }
                }
                if (length < snapDistance)
                {
                    polygonList.RemoveAt(polygonIndex);
                    polygonIndex--;
                }
            }

            //Finally optimize all the polygons. Every point removed saves time in the long run.
            long minimumDistanceToCreateNewPosition = 10;
            polygonList = Clipper.CleanPolygons(polygonList, minimumDistanceToCreateNewPosition);
        }

        gapCloserResult findPolygonGapCloser(IntPoint ip0, IntPoint ip1)
        {
            gapCloserResult ret = new gapCloserResult();
            closePolygonResult c1 = findPolygonPointClosestTo(ip0);
            closePolygonResult c2 = findPolygonPointClosestTo(ip1);
            if (c1.polygonIdx < 0 || c1.polygonIdx != c2.polygonIdx)
            {
                ret.len = -1;
                return ret;
            }
            ret.polygonIdx = c1.polygonIdx;
            ret.pointIdxA = c1.pointIdx;
            ret.pointIdxB = c2.pointIdx;
            ret.AtoB = true;

            if (ret.pointIdxA == ret.pointIdxB)
            {
                //Connection points are on the same line segment.
                ret.len = (ip0 - ip1).vSize();
            }
            else
            {
                //Find out if we have should go from A to B or the other way around.
                IntPoint p0 = polygonList[ret.polygonIdx][ret.pointIdxA];
                long lenA = (p0 - ip0).vSize();
                for (int i = ret.pointIdxA; i != ret.pointIdxB; i = (i + 1) % polygonList[ret.polygonIdx].Count)
                {
                    IntPoint p1 = polygonList[ret.polygonIdx][i];
                    lenA += (p0 - p1).vSize();
                    p0 = p1;
                }
                lenA += (p0 - ip1).vSize();

                p0 = polygonList[ret.polygonIdx][ret.pointIdxB];
                long lenB = (p0 - ip1).vSize();
                for (int i = ret.pointIdxB; i != ret.pointIdxA; i = (i + 1) % polygonList[ret.polygonIdx].Count)
                {
                    IntPoint p1 = polygonList[ret.polygonIdx][i];
                    lenB += (p0 - p1).vSize();
                    p0 = p1;
                }
                lenB += (p0 - ip0).vSize();

                if (lenA < lenB)
                {
                    ret.AtoB = true;
                    ret.len = lenA;
                }
                else
                {
                    ret.AtoB = false;
                    ret.len = lenB;
                }
            }
            return ret;
        }

        closePolygonResult findPolygonPointClosestTo(IntPoint input)
        {
            closePolygonResult ret = new closePolygonResult();
            for (int n = 0; n < polygonList.Count; n++)
            {
                IntPoint p0 = polygonList[n][polygonList[n].Count - 1];
                for (int i = 0; i < polygonList[n].Count; i++)
                {
                    IntPoint p1 = polygonList[n][i];

                    //Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
                    IntPoint pDiff = p1 - p0;
                    long lineLength = (pDiff).vSize();
                    if (lineLength > 1)
                    {
                        long distOnLine = (pDiff).Dot(input - p0) / lineLength;
                        if (distOnLine >= 0 && distOnLine <= lineLength)
                        {
                            IntPoint q = p0 + pDiff * distOnLine / lineLength;
                            if ((q - input).shorterThen(100))
                            {
                                ret.intersectionPoint = q;
                                ret.polygonIdx = n;
                                ret.pointIdx = i;
                                return ret;
                            }
                        }
                    }
                    p0 = p1;
                }
            }
            ret.polygonIdx = -1;
            return ret;
        }
    }
}