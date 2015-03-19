/*
This file is part of MatterSlice. A commandline utility for 
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
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

    public class GapCloserResult
    {
        public long len;
        public int polygonIndex;
        public int pointIndexA;
        public int pointIndexB;
        public bool AtoB;
    }

    public class ClosePolygonResult
    {
        //The result of trying to find a point on a closed polygon line. This gives back the point index, the polygon index, and the point of the connection.
        //The line on which the point lays is between pointIdx-1 and pointIdx
        public IntPoint intersectionPoint;
        public int polygonIdx;
        public int pointIdx;
    }

    public class SlicerLayer
    {
        public List<SlicerSegment> segmentList = new List<SlicerSegment>();
        public Dictionary<int, int> faceTo2DSegmentIndex = new Dictionary<int, int>();

        public int z;
        public Polygons polygonList = new Polygons();
        public Polygons openPolygonList = new Polygons();

        public void MakePolygons(OptimizedVolume optomizedMesh, ConfigConstants.REPAIR_OUTLINES outlineRepairTypes)
        {
            for (int startingSegmentIndex = 0; startingSegmentIndex < segmentList.Count; startingSegmentIndex++)
            {
                if (segmentList[startingSegmentIndex].hasBeenAddedToPolygon)
                {
                    continue;
                }

                Polygon poly = new Polygon();
                // We start by adding the start, as we will add ends from now on.
                IntPoint polygonStartPosition = segmentList[startingSegmentIndex].start;
                poly.Add(polygonStartPosition);
                

                int segmentIndexBeingAdded = startingSegmentIndex;
                bool canClose;

                bool lastAddWasAStart = true;

                while (true)
                {
                    canClose = false;
                    segmentList[segmentIndexBeingAdded].hasBeenAddedToPolygon = true;
                    IntPoint addedSegmentEndPoint = segmentList[segmentIndexBeingAdded].end;
                    if (!lastAddWasAStart)
                    {
                        // the last added point was an end so add the start as the text end point
                        addedSegmentEndPoint = segmentList[segmentIndexBeingAdded].start;
                    }

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
                                if (addedSegmentEndPoint == foundSegmentStart)
                                {
                                    // if we have looped back around to where we started
                                    if (addedSegmentEndPoint == polygonStartPosition)
                                    {
                                        canClose = true;
                                    }

                                    // If this segment has already been added
                                    if (segmentList[touchingSegmentIndex].hasBeenAddedToPolygon)
                                    {
                                        continue;
                                    }

                                    nextSegmentToCheckIndex = touchingSegmentIndex;
                                    lastAddWasAStart = true;
                                }
                                else // let's check if the other side of this segment can hook up (the normal is facing the wrong way)
                                {
                                    IntPoint foundSegmentEnd = segmentList[touchingSegmentIndex].end;
                                    if (addedSegmentEndPoint == foundSegmentEnd)
                                    {
                                        // if we have looped back around to where we started
                                        if (addedSegmentEndPoint == polygonStartPosition)
                                        {
                                            canClose = true;
                                        }

                                        // If this segment has already been added
                                        if (segmentList[touchingSegmentIndex].hasBeenAddedToPolygon)
                                        {
                                            continue;
                                        }

                                        nextSegmentToCheckIndex = touchingSegmentIndex;
                                        lastAddWasAStart = false;
                                    }
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

            // Link up all the missing ends, closing up the smallest gaps first. This is an inefficient implementation which can run in O(n*n*n) time.
            while (true)
            {
                long bestScore = 10000 * 10000;
                int bestA = -1;
                int bestB = -1;
                bool reversed = false;
                for (int polygonAIndex = 0; polygonAIndex < openPolygonList.Count; polygonAIndex++)
                {
                    if (openPolygonList[polygonAIndex].Count < 1)
                    {
                        continue;
                    }

                    for (int polygonBIndex = 0; polygonBIndex < openPolygonList.Count; polygonBIndex++)
                    {
                        if (openPolygonList[polygonBIndex].Count < 1)
                        {
                            continue;
                        }

                        IntPoint diff1 = openPolygonList[polygonAIndex][openPolygonList[polygonAIndex].Count - 1] - openPolygonList[polygonBIndex][0];
                        long distSquared1 = (diff1).LengthSquared();
                        if (distSquared1 < bestScore)
                        {
                            bestScore = distSquared1;
                            bestA = polygonAIndex;
                            bestB = polygonBIndex;
                            reversed = false;

							if (bestScore == 0)
							{
								// found a perfect match stop looking
								break;
							}
                        }

                        if (polygonAIndex != polygonBIndex)
                        {
                            IntPoint diff2 = openPolygonList[polygonAIndex][openPolygonList[polygonAIndex].Count - 1] - openPolygonList[polygonBIndex][openPolygonList[polygonBIndex].Count - 1];
                            long distSquared2 = (diff2).LengthSquared();
                            if (distSquared2 < bestScore)
                            {
                                bestScore = distSquared2;
                                bestA = polygonAIndex;
                                bestB = polygonBIndex;
                                reversed = true;
							
								if (bestScore == 0)
								{
									// found a perfect match stop looking
									break;
								}
							}
                        }
					}
				
					if (bestScore == 0)
					{
						// found a perfect match stop looking
						break;
					}
				}

                if (bestScore >= 10000 * 10000)
                {
                    break;
                }

                if (bestA == bestB)
                {
                    polygonList.Add(new Polygon(openPolygonList[bestA]));
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
                                openPolygonList[bestA].AddRange(openPolygonList[bestB]);
                                openPolygonList[bestB].Clear();
                            }
                            else
                            {
                                openPolygonList[bestB].AddRange(openPolygonList[bestA]);
                                openPolygonList[bestA].Clear();
                            }
                        }
                        else
                        {
                            openPolygonList[bestB].AddRange(openPolygonList[bestA]);
                            openPolygonList[bestB].Clear();
                        }
                    }
                    else
                    {
                        openPolygonList[bestA].AddRange(openPolygonList[bestB]);
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
                    GapCloserResult bestResult = new GapCloserResult();
                    bestResult.len = long.MaxValue;
                    bestResult.polygonIndex = -1;
                    bestResult.pointIndexA = -1;
                    bestResult.pointIndexB = -1;

                    for (int i = 0; i < openPolygonList.Count; i++)
                    {
                        if (openPolygonList[i].Count < 1) continue;

                        {
                            GapCloserResult res = FindPolygonGapCloser(openPolygonList[i][0], openPolygonList[i][openPolygonList[i].Count - 1]);
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

                            GapCloserResult res = FindPolygonGapCloser(openPolygonList[i][0], openPolygonList[j][openPolygonList[j].Count - 1]);
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
                            if (bestResult.pointIndexA == bestResult.pointIndexB)
                            {
                                polygonList.Add(new Polygon(openPolygonList[bestA]));
                                openPolygonList[bestA].Clear();
                            }
                            else if (bestResult.AtoB)
                            {
                                Polygon poly = new Polygon();
                                polygonList.Add(poly);
                                for (int j = bestResult.pointIndexA; j != bestResult.pointIndexB; j = (j + 1) % polygonList[bestResult.polygonIndex].Count)
                                {
                                    poly.Add(polygonList[bestResult.polygonIndex][j]);
                                }

                                poly.AddRange(openPolygonList[bestA]);
                                openPolygonList[bestA].Clear();
                            }
                            else
                            {
                                int n = polygonList.Count;
                                polygonList.Add(new Polygon(openPolygonList[bestA]));
                                for (int j = bestResult.pointIndexB; j != bestResult.pointIndexA; j = (j + 1) % polygonList[bestResult.polygonIndex].Count)
                                {
                                    polygonList[n].Add(polygonList[bestResult.polygonIndex][j]);
                                }
                                openPolygonList[bestA].Clear();
                            }
                        }
                        else
                        {
                            if (bestResult.pointIndexA == bestResult.pointIndexB)
                            {
                                openPolygonList[bestB].AddRange(openPolygonList[bestA]);
                                openPolygonList[bestA].Clear();
                            }
                            else if (bestResult.AtoB)
                            {
                                Polygon poly = new Polygon();
                                for (int n = bestResult.pointIndexA; n != bestResult.pointIndexB; n = (n + 1) % polygonList[bestResult.polygonIndex].Count)
                                {
                                    poly.Add(polygonList[bestResult.polygonIndex][n]);
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
                                for (int n = bestResult.pointIndexB; n != bestResult.pointIndexA; n = (n + 1) % polygonList[bestResult.polygonIndex].Count)
                                {
                                    openPolygonList[bestB].Add(polygonList[bestResult.polygonIndex][n]);
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

            if ((outlineRepairTypes & ConfigConstants.REPAIR_OUTLINES.KEEP_OPEN) == ConfigConstants.REPAIR_OUTLINES.KEEP_OPEN)
            {
                for (int n = 0; n < openPolygonList.Count; n++)
                {
                    if (openPolygonList[n].Count > 0)
                    {
                        polygonList.Add(new Polygon(openPolygonList[n]));
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
			double minimumDistanceToCreateNewPosition = 10;
            polygonList = Clipper.CleanPolygons(polygonList, minimumDistanceToCreateNewPosition);
        }

        GapCloserResult FindPolygonGapCloser(IntPoint ip0, IntPoint ip1)
        {
            GapCloserResult ret = new GapCloserResult();
            ClosePolygonResult c1 = FindPolygonPointClosestTo(ip0);
            ClosePolygonResult c2 = FindPolygonPointClosestTo(ip1);
            if (c1.polygonIdx < 0 || c1.polygonIdx != c2.polygonIdx)
            {
                ret.len = -1;
                return ret;
            }
            ret.polygonIndex = c1.polygonIdx;
            ret.pointIndexA = c1.pointIdx;
            ret.pointIndexB = c2.pointIdx;
            ret.AtoB = true;

            if (ret.pointIndexA == ret.pointIndexB)
            {
                //Connection points are on the same line segment.
                ret.len = (ip0 - ip1).vSize();
            }
            else
            {
                //Find out if we have should go from A to B or the other way around.
                IntPoint p0 = polygonList[ret.polygonIndex][ret.pointIndexA];
                long lenA = (p0 - ip0).vSize();
                for (int i = ret.pointIndexA; i != ret.pointIndexB; i = (i + 1) % polygonList[ret.polygonIndex].Count)
                {
                    IntPoint p1 = polygonList[ret.polygonIndex][i];
                    lenA += (p0 - p1).vSize();
                    p0 = p1;
                }
                lenA += (p0 - ip1).vSize();

                p0 = polygonList[ret.polygonIndex][ret.pointIndexB];
                long lenB = (p0 - ip1).vSize();
                for (int i = ret.pointIndexB; i != ret.pointIndexA; i = (i + 1) % polygonList[ret.polygonIndex].Count)
                {
                    IntPoint p1 = polygonList[ret.polygonIndex][i];
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

        ClosePolygonResult FindPolygonPointClosestTo(IntPoint input)
        {
            ClosePolygonResult ret = new ClosePolygonResult();
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
                            if ((q - input).ShorterThen(100))
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