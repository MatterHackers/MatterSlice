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
using System.IO;

using ClipperLib;
using C5;

namespace MatterHackers.MatterSlice
{
    using Point = IntPoint;
    using PolygonRef = Polygon;

    public class SlicerSegment
    {
        public Point start, end;
        public int faceIndex;
        public bool addedToPolygon;
    }

    public class closePolygonResult
    {
        //The result of trying to find a point on a closed polygon line. This gives back the point index, the polygon index, and the point of the connection.
        //The line on which the point lays is between pointIdx-1 and pointIdx
        public Point intersectionPoint;
        public int polygonIdx;
        public int pointIdx;
    }

    public class gapCloserResult
    {
        public long len;
        public int polygonIdx;
        public int pointIdxA;
        public int pointIdxB;
        public bool AtoB;
    }

    public class SlicerLayer
    {
        public List<SlicerSegment> segmentList = new List<SlicerSegment>();
        public TreeDictionary<int, int> faceToSegmentIndex = new TreeDictionary<int,int>();

        public int z;
        public Polygons polygonList;
        public Polygons openPolygonList;

        public void makePolygons(OptimizedVolume ov, bool keepNoneClosed, bool extensiveStitching)
        {
            for (int startSegment = 0; startSegment < segmentList.Count; startSegment++)
            {
                if (segmentList[startSegment].addedToPolygon)
                    continue;

                Polygon poly = new Polygon();
                poly.Add(segmentList[startSegment].start);

                int segmentIndex = startSegment;
                bool canClose;
                while (true)
                {
                    canClose = false;
                    segmentList[segmentIndex].addedToPolygon = true;
                    Point p0 = segmentList[segmentIndex].end;
                    poly.add(p0);
                    int nextIndex = -1;
                    OptimizedFace face = ov.faces[segmentList[segmentIndex].faceIndex];
                    for (int i = 0; i < 3; i++)
                    {
                        if (face.touching[i] > -1)
                        {
                            int foundIndex = 0;
                            bool found = faceToSegmentIndex.Find(face.touching[i], out foundIndex);
                            if (found && foundIndex != faceToSegmentIndex.FindMax().Value)
                            {
                                IntPoint p1 = segmentList[faceToSegmentIndex[face.touching[i]]].start;
                                IntPoint diff = p0 - p1;
                                if (diff.IsShorterThen(10))
                                {
                                    if (faceToSegmentIndex[face.touching[i]] == (int)startSegment)
                                    {
                                        canClose = true;
                                    }

                                    if (segmentList[faceToSegmentIndex[face.touching[i]]].addedToPolygon)
                                    {
                                        continue;
                                    }

                                    nextIndex = faceToSegmentIndex[face.touching[i]];
                                }
                            }
                        }
                    }
                    if (nextIndex == -1)
                        break;
                    segmentIndex = nextIndex;
                }
                if (canClose)
                    polygonList.add(poly);
                else
                    openPolygonList.add(poly);
            }
            //Clear the segmentList to save memory, it is no longer needed after this point.
            segmentList.Clear();

            //Connecting polygons that are not closed yet, as models are not always perfect manifold we need to join some stuff up to get proper polygons
            //First link up polygon ends that are within 2 microns.
            for (int i = 0; i < openPolygonList.Count; i++)
            {
                if (openPolygonList[i].Count < 1) continue;
                for (int j = 0; j < openPolygonList.Count; j++)
                {
                    if (openPolygonList[j].Count < 1) continue;

                    Point diff = openPolygonList[i][openPolygonList[i].Count - 1] - openPolygonList[j][0];
                    long distSquared = (diff).vSize2();

                    if (distSquared < 2 * 2)
                    {
                        if (i == j)
                        {
                            polygonList.add(openPolygonList[i]);
                            openPolygonList[i].clear();
                            break;
                        }
                        else
                        {
                            for (int n = 0; n < openPolygonList[j].Count; n++)
                                openPolygonList[i].add(openPolygonList[j][n]);

                            openPolygonList[j].clear();
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

                        Point diff1 = openPolygonList[i][openPolygonList[i].Count - 1] - openPolygonList[j][0];
                        long distSquared1 = (diff1).vSize2();
                        if (distSquared1 < bestScore)
                        {
                            bestScore = distSquared1;
                            bestA = i;
                            bestB = j;
                            reversed = false;
                        }

                        if (i != j)
                        {
                            Point diff2 = openPolygonList[i][openPolygonList[i].Count - 1] - openPolygonList[j][openPolygonList[j].Count - 1];
                            long distSquared2 = (diff2).vSize2();
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
                    break;

                if (bestA == bestB)
                {
                    polygonList.add(openPolygonList[bestA]);
                    openPolygonList[bestA].clear();
                }
                else
                {
                    if (reversed)
                    {
                        if (openPolygonList[bestA].polygonLength() > openPolygonList[bestB].polygonLength())
                        {
                            for (int n = openPolygonList[bestB].Count - 1; n >= 0; n--)
                                openPolygonList[bestA].add(openPolygonList[bestB][n]);
                            openPolygonList[bestB].clear();
                        }
                        else
                        {
                            for (int n = openPolygonList[bestA].Count - 1; n >= 0; n--)
                                openPolygonList[bestB].add(openPolygonList[bestA][n]);
                            openPolygonList[bestA].clear();
                        }
                    }
                    else
                    {
                        for (int n = 0; n < openPolygonList[bestB].Count; n++)
                            openPolygonList[bestA].add(openPolygonList[bestB][n]);
                        openPolygonList[bestB].clear();
                    }
                }
            }

            if (extensiveStitching)
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
                                PolygonRef poly = polygonList.newPoly();
                                for (int j = bestResult.pointIdxA; j != bestResult.pointIdxB; j = (j + 1) % polygonList[bestResult.polygonIdx].Count)
                                    poly.add(polygonList[bestResult.polygonIdx][j]);
                                for (int j = openPolygonList[bestA].Count - 1; (int)(j) >= 0; j--)
                                    poly.add(openPolygonList[bestA][j]);
                                openPolygonList[bestA].clear();
                            }
                            else
                            {
                                int n = polygonList.Count;
                                polygonList.add(openPolygonList[bestA]);
                                for (int j = bestResult.pointIdxB; j != bestResult.pointIdxA; j = (j + 1) % polygonList[bestResult.polygonIdx].Count)
                                    polygonList[n].add(polygonList[bestResult.polygonIdx][j]);
                                openPolygonList[bestA].clear();
                            }
                        }
                        else
                        {
                            if (bestResult.pointIdxA == bestResult.pointIdxB)
                            {
                                for (int n = 0; n < openPolygonList[bestA].Count; n++)
                                    openPolygonList[bestB].add(openPolygonList[bestA][n]);
                                openPolygonList[bestA].clear();
                            }
                            else if (bestResult.AtoB)
                            {
                                Polygon poly = new Polygon();
                                for (int n = bestResult.pointIdxA; n != bestResult.pointIdxB; n = (n + 1) % polygonList[bestResult.polygonIdx].Count)
                                    poly.add(polygonList[bestResult.polygonIdx][n]);
                                for (int n = poly.Count - 1; (int)(n) >= 0; n--)
                                    openPolygonList[bestB].add(poly[n]);
                                for (int n = 0; n < openPolygonList[bestA].Count; n++)
                                    openPolygonList[bestB].add(openPolygonList[bestA][n]);
                                openPolygonList[bestA].clear();
                            }
                            else
                            {
                                for (int n = bestResult.pointIdxB; n != bestResult.pointIdxA; n = (n + 1) % polygonList[bestResult.polygonIdx].Count)
                                    openPolygonList[bestB].add(polygonList[bestResult.polygonIdx][n]);
                                for (int n = openPolygonList[bestA].Count - 1; n >= 0; n--)
                                    openPolygonList[bestB].add(openPolygonList[bestA][n]);
                                openPolygonList[bestA].clear();
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            /*
            int q=0;
            for(int i=0;i<openPolygonList.Count;i++)
            {
                if (openPolygonList[i].Count < 2) continue;
                if (!q) log("***\n");
                log("S: %f %f\n", float(openPolygonList[i][0].X), float(openPolygonList[i][0].Y));
                log("E: %f %f\n", float(openPolygonList[i][openPolygonList[i].Count-1].X), float(openPolygonList[i][openPolygonList[i].Count-1].Y));
                q = 1;
            }
            */
            //if (q) exit(1);

            if (keepNoneClosed)
            {
                for (int n = 0; n < openPolygonList.Count; n++)
                {
                    if (openPolygonList[n].Count > 0)
                        polygonList.add(openPolygonList[n]);
                }
            }
            //Clear the openPolygonList to save memory, the only reason to keep it after this is for debugging.
            //openPolygonList.clear();

            //Remove all the tiny polygons, or polygons that are not closed. As they do not contribute to the actual print.
            int snapDistance = 1000;
            for (int i = 0; i < polygonList.Count; i++)
            {
                int length = 0;

                for (int n = 1; n < polygonList[i].Count; n++)
                {
                    length += (polygonList[i][n] - polygonList[i][n - 1]).vSize();
                    if (length > snapDistance)
                        break;
                }
                if (length < snapDistance)
                {
                    polygonList.remove(i);
                    i--;
                }
            }

            //Finally optimize all the polygons. Every point removed saves time in the long run.
            PolygonOptimizer.optimizePolygons(polygonList);
        }

        gapCloserResult findPolygonGapCloser(Point ip0, Point ip1)
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
                Point p0 = polygonList[ret.polygonIdx][ret.pointIdxA];
                long lenA = (p0 - ip0).vSize();
                for (int i = ret.pointIdxA; i != ret.pointIdxB; i = (i + 1) % polygonList[ret.polygonIdx].Count)
                {
                    Point p1 = polygonList[ret.polygonIdx][i];
                    lenA += (p0 - p1).vSize();
                    p0 = p1;
                }
                lenA += (p0 - ip1).vSize();

                p0 = polygonList[ret.polygonIdx][ret.pointIdxB];
                long lenB = (p0 - ip1).vSize();
                for (int i = ret.pointIdxB; i != ret.pointIdxA; i = (i + 1) % polygonList[ret.polygonIdx].Count)
                {
                    Point p1 = polygonList[ret.polygonIdx][i];
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

        closePolygonResult findPolygonPointClosestTo(Point input)
        {
            closePolygonResult ret = new closePolygonResult();
            for (int n = 0; n < polygonList.Count; n++)
            {
                Point p0 = polygonList[n][polygonList[n].Count - 1];
                for (int i = 0; i < polygonList[n].Count; i++)
                {
                    Point p1 = polygonList[n][i];

                    //Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
                    Point pDiff = p1 - p0;
                    long lineLength = (pDiff).vSize();
                    if (lineLength > 1)
                    {
                        long distOnLine = (pDiff).dot(input - p0) / lineLength;
                        if (distOnLine >= 0 && distOnLine <= lineLength)
                        {
                            Point q = p0 + pDiff * distOnLine / lineLength;
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

    public class Slicer
    {
        public List<SlicerLayer> layers;
        public Point3 modelSize, modelMin;

        public Slicer(OptimizedVolume ov, int initial, int thickness, bool keepNoneClosed, bool extensiveStitching)
        {
            modelSize = ov.model.modelSize;
            modelMin = ov.model.vMin;

            int layerCount = (modelSize.z - initial) / thickness + 1;
            LogOutput.log(string.Format("Layer count: {0}\n", layerCount));
            layers.Capacity = layerCount;

            for (int layerNr = 0; layerNr < layerCount; layerNr++)
            {
                layers[layerNr].z = initial + thickness * layerNr;
            }

            for (int i = 0; i < ov.faces.Count; i++)
            {
                Point3 p0 = ov.points[ov.faces[i].index[0]].p;
                Point3 p1 = ov.points[ov.faces[i].index[1]].p;
                Point3 p2 = ov.points[ov.faces[i].index[2]].p;
                int minZ = p0.z;
                int maxZ = p0.z;
                if (p1.z < minZ) minZ = p1.z;
                if (p2.z < minZ) minZ = p2.z;
                if (p1.z > maxZ) maxZ = p1.z;
                if (p2.z > maxZ) maxZ = p2.z;

                for (int layerNr = (minZ - initial) / thickness; layerNr <= (maxZ - initial) / thickness; layerNr++)
                {
                    int z = layerNr * thickness + initial;
                    if (z < minZ) continue;
                    if (layerNr < 0) continue;

                    SlicerSegment s;
                    if (p0.z < z && p1.z >= z && p2.z >= z)
                        s = project2D(p0, p2, p1, z);
                    else if (p0.z > z && p1.z < z && p2.z < z)
                        s = project2D(p0, p1, p2, z);

                    else if (p1.z < z && p0.z >= z && p2.z >= z)
                        s = project2D(p1, p0, p2, z);
                    else if (p1.z > z && p0.z < z && p2.z < z)
                        s = project2D(p1, p2, p0, z);

                    else if (p2.z < z && p1.z >= z && p0.z >= z)
                        s = project2D(p2, p1, p0, z);
                    else if (p2.z > z && p1.z < z && p0.z < z)
                        s = project2D(p2, p0, p1, z);
                    else
                    {
                        //Not all cases create a segment, because a point of a face could create just a dot, and two touching faces
                        //  on the slice would create two segments
                        continue;
                    }
                    layers[layerNr].faceToSegmentIndex[i] = layers[layerNr].segmentList.Count;
                    s.faceIndex = i;
                    s.addedToPolygon = false;
                    layers[layerNr].segmentList.Add(s);
                }
            }

            for (int layerNr = 0; layerNr < layers.Count; layerNr++)
            {
                layers[layerNr].makePolygons(ov, keepNoneClosed, extensiveStitching);
            }
        }

        public SlicerSegment project2D(Point3 p0, Point3 p1, Point3 p2, int z)
        {
            SlicerSegment seg = new SlicerSegment();
            seg.start.X = p0.x + (long)(p1.x - p0.x) * (long)(z - p0.z) / (long)(p1.z - p0.z);
            seg.start.Y = p0.y + (long)(p1.y - p0.y) * (long)(z - p0.z) / (long)(p1.z - p0.z);
            seg.end.X = p0.x + (long)(p2.x - p0.x) * (long)(z - p0.z) / (long)(p2.z - p0.z);
            seg.end.Y = p0.y + (long)(p2.y - p0.y) * (long)(z - p0.z) / (long)(p2.z - p0.z);
            return seg;
        }

        void dumpSegmentsToHTML(string filename)
        {
            float scale = Math.Max(modelSize.x, modelSize.y) / 1500;
            StreamWriter f = new StreamWriter(filename);
            f.Write("<!DOCTYPE html><html><body>\n");
            for (int i = 0; i < layers.Count; i++)
            {
                f.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style='width:%ipx;height:%ipx'>\n", (int)(modelSize.x / scale), (int)(modelSize.y / scale));
                f.Write("<marker id='MidMarker' viewBox='0 0 10 10' refX='5' refY='5' markerUnits='strokeWidth' markerWidth='10' markerHeight='10' stroke='lightblue' stroke-width='2' fill='none' orient='auto'>");
                f.Write("<path d='M 0 0 L 10 5 M 0 10 L 10 5'/>");
                f.Write("</marker>");
                f.Write("<g fill-rule='evenodd' style=\"fill: gray; stroke:black;stroke-width:1\">\n");
                f.Write("<path marker-mid='url(#MidMarker)' d=\"");
                for (int j = 0; j < layers[i].polygonList.Count; j++)
                {
                    PolygonRef p = layers[i].polygonList[j];
                    for (int n = 0; n < p.Count; n++)
                    {
                        if (n == 0)
                            f.Write("M");
                        else
                            f.Write("L");
                        f.Write("%f,%f ", (float)(p[n].X - modelMin.x) / scale, (float)(p[n].Y - modelMin.y) / scale);
                    }
                    f.Write("Z\n");
                }
                f.Write("\"/>");
                f.Write("</g>\n");
                for (int j = 0; j < layers[i].openPolygonList.Count; j++)
                {
                    PolygonRef p = layers[i].openPolygonList[j];
                    if (p.Count < 1) continue;
                    f.Write("<polyline marker-mid='url(#MidMarker)' points=\"");
                    for (int n = 0; n < p.Count; n++)
                    {
                        f.Write("%f,%f ", (float)(p[n].X - modelMin.x) / scale, (float)(p[n].Y - modelMin.y) / scale);
                    }
                    f.Write("\" style=\"fill: none; stroke:red;stroke-width:1\" />\n");
                }
                f.Write("</svg>\n");
            }
            f.Write("</body></html>");
            f.Close();
        }
    }
}