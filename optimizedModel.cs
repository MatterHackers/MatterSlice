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

namespace MatterHackers.MatterSlice
{
    public class OptimizedFace
    {
        public int[] index = new int[3];
        public int[] touching = new int[3];
    }

    public class OptimizedPoint3
    {
        public Point3 p;
        public List<int> faceIndexList;

        public OptimizedPoint3(Point3 p)
        {
            this.p = p;
        }
    }

    public class OptimizedVolume
    {
        const int MELD_DIST = 30;

        public OptimizedModel model;
        public List<OptimizedPoint3> points;
        public List<OptimizedFace> faces;

        public OptimizedVolume(SimpleVolume volume, OptimizedModel model)
        {
            this.model = model;
            points.Capacity = volume.faces.Count * 3;
            faces.Capacity = volume.faces.Count;

            Dictionary<int, List<int>> indexMap = new Dictionary<int,List<int>>();

            Stopwatch t = new Stopwatch();
            t.Start();
            for (int i = 0; i < volume.faces.Count; i++)
            {
                OptimizedFace f = new OptimizedFace();
                if ((i % 1000 == 0) && t.Elapsed.Seconds > 2)
                {
                    LogOutput.logProgress("optimized", i + 1, volume.faces.Count);
                }
                for (int j = 0; j < 3; j++)
                {
                    Point3 p = volume.faces[i].v[j];
                    int hash = ((p.x + MELD_DIST / 2) / MELD_DIST) ^ (((p.y + MELD_DIST / 2) / MELD_DIST) << 10) ^ (((p.z + MELD_DIST / 2) / MELD_DIST) << 20);
                    int idx = 0;
                    bool add = true;
                    //if (indexMap.ContainsKey(hash))
                    {
                        for (int n = 0; n < indexMap[hash].Count; n++)
                        {
                            if ((points[indexMap[hash][n]].p - p).testLength(MELD_DIST))
                            {
                                idx = indexMap[hash][n];
                                add = false;
                                break;
                            }
                        }
                    }
                    if (add)
                    {
                        if (!indexMap.ContainsKey(hash))
                        {
                            indexMap.Add(hash, new List<int>());
                        }
                        indexMap[hash].Add(points.Count);
                        idx = points.Count;
                        points.Add(new OptimizedPoint3(p));
                    }
                    f.index[j] = idx;
                }
                if (f.index[0] != f.index[1] && f.index[0] != f.index[2] && f.index[1] != f.index[2])
                {
                    //Check if there is a face with the same points
                    bool duplicate = false;
                    for (int _idx0 = 0; _idx0 < points[f.index[0]].faceIndexList.Count; _idx0++)
                    {
                        for (int _idx1 = 0; _idx1 < points[f.index[1]].faceIndexList.Count; _idx1++)
                        {
                            for (int _idx2 = 0; _idx2 < points[f.index[2]].faceIndexList.Count; _idx2++)
                            {
                                if (points[f.index[0]].faceIndexList[_idx0] == points[f.index[1]].faceIndexList[_idx1] && points[f.index[0]].faceIndexList[_idx0] == points[f.index[2]].faceIndexList[_idx2])
                                    duplicate = true;
                            }
                        }
                    }
                    if (!duplicate)
                    {
                        points[f.index[0]].faceIndexList.Add(faces.Count);
                        points[f.index[1]].faceIndexList.Add(faces.Count);
                        points[f.index[2]].faceIndexList.Add(faces.Count);
                        faces.Add(f);
                    }
                }
            }
            //fprintf(stdout, "\rAll faces are optimized in %5.1fs.\n",timeElapsed(t));

            int openFacesCount = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                OptimizedFace f = faces[i];
                f.touching[0] = getFaceIdxWithPoints(f.index[0], f.index[1], i);
                f.touching[1] = getFaceIdxWithPoints(f.index[1], f.index[2], i);
                f.touching[2] = getFaceIdxWithPoints(f.index[2], f.index[0], i);
                if (f.touching[0] == -1)
                    openFacesCount++;
                if (f.touching[1] == -1)
                    openFacesCount++;
                if (f.touching[2] == -1)
                    openFacesCount++;
            }
            //fprintf(stdout, "  Number of open faces: %i\n", openFacesCount);
        }

        public int getFaceIdxWithPoints(int idx0, int idx1, int notFaceIdx)
        {
            for (int i = 0; i < points[idx0].faceIndexList.Count; i++)
            {
                int f0 = points[idx0].faceIndexList[i];
                if (f0 == notFaceIdx) continue;
                for (int j = 0; j < points[idx1].faceIndexList.Count; j++)
                {
                    int f1 = points[idx1].faceIndexList[j];
                    if (f1 == notFaceIdx) continue;
                    if (f0 == f1) return f0;
                }
            }
            return -1;
        }
    };
    public class OptimizedModel
    {
        public List<OptimizedVolume> volumes;
        public Point3 modelSize;
        public Point3 vMin, vMax;

        public OptimizedModel(SimpleModel model, Point3 center)
        {
            for (int i = 0; i < model.volumes.Count; i++)
                volumes.Add(new OptimizedVolume(model.volumes[i], this));
            vMin = model.min();
            vMax = model.max();

            Point3 vOffset = new Point3((vMin.x + vMax.x) / 2, (vMin.y + vMax.y) / 2, vMin.z);
            vOffset -= center;
            for (int i = 0; i < volumes.Count; i++)
                for (int n = 0; n < volumes[i].points.Count; n++)
                    volumes[i].points[n].p -= vOffset;

            modelSize = vMax - vMin;
            vMin -= vOffset;
            vMax -= vOffset;
        }

        public void saveDebugSTL(string filename)
        {
            throw new NotImplementedException();
#if false
    char buffer[80] = "MatterSlice_STL_export";
    int n;
    uint16_t s;
    float flt;
    OptimizedVolume* vol = &volumes[0];
    FILE* f = fopen(filename, "wb");
    fwrite(buffer, 80, 1, f);
    n = vol.faces.Count;
    fwrite(&n, sizeof(n), 1, f);
    for(int i=0;i<vol.faces.Count;i++)
    {
        flt = 0;
        s = 0;
        fwrite(&flt, sizeof(flt), 1, f);
        fwrite(&flt, sizeof(flt), 1, f);
        fwrite(&flt, sizeof(flt), 1, f);

        flt = vol.points[vol.faces[i].index[0]].p.x / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[0]].p.y / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[0]].p.z / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[1]].p.x / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[1]].p.y / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[1]].p.z / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[2]].p.x / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[2]].p.y / 1000.0; fwrite(&flt, sizeof(flt), 1, f);
        flt = vol.points[vol.faces[i].index[2]].p.z / 1000.0; fwrite(&flt, sizeof(flt), 1, f);

        fwrite(&s, sizeof(s), 1, f);
    }
    fclose(f);
    //Export the open faces so you can view the with MatterControl (hacky)
    /*
    char gcodeFilename[1024];
    strcpy(gcodeFilename, filename);
    strcpy(strchr(gcodeFilename, '.'), ".gcode");
    f = fopen(gcodeFilename, "w");
    for(int i=0;i<faces.Count;i++)
    {
        for(int j=0;j<3;j++)
        {
            if (faces[i].touching[j] == -1)
            {
                Point3 p0 = points[faces[i].index[j]].p;
                Point3 p1 = points[faces[i].index[(j+1)%3]].p;
                fprintf(f, ";Model error(open face): (%f, %f, %f) (%f, %f, %f)\n", p0.x / 1000.0, p0.y / 1000.0, p0.z / 1000.0, p1.x / 1000.0, p1.y / 1000.0, p1.z / 1000.0);
            }
        }
    }
    fclose(f);
    */
#endif
        }
    }
}



