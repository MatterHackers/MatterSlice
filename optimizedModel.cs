/*
Copyright (C) 2013 David Braam
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
    public class OptimizedFace
    {
        public int[] vertexIndex = new int[3];
        // each face can be touching 3 other faces (along its edges)
        public int[] touchingFaces = new int[3];
    }

    public class OptimizedPoint3
    {
        public Point3 position;
        public List<int> usedByFacesList = new List<int>();

        public OptimizedPoint3(Point3 position)
        {
            this.position = position;
        }
    }

    public class OptimizedVolume
    {
        const int MELD_DIST = 30;

        public OptimizedModel model;
        public List<OptimizedPoint3> vertices = new List<OptimizedPoint3>();
        public List<OptimizedFace> facesTriangle = new List<OptimizedFace>();

        public OptimizedVolume(SimpleVolume volume, OptimizedModel model)
        {
            this.model = model;
            vertices.Capacity = volume.faceTriangles.Count * 3;
            facesTriangle.Capacity = volume.faceTriangles.Count;

            Dictionary<int, List<int>> indexMap = new Dictionary<int, List<int>>();

            Stopwatch t = new Stopwatch();
            t.Start();
            for (int i = 0; i < volume.faceTriangles.Count; i++)
            {
                OptimizedFace f = new OptimizedFace();
                if ((i % 1000 == 0) && t.Elapsed.Seconds > 2)
                {
                    LogOutput.logProgress("optimized", i + 1, volume.faceTriangles.Count);
                }
                for (int j = 0; j < 3; j++)
                {
                    Point3 p = volume.faceTriangles[i].v[j];
                    int hash = (int)(((p.x + MELD_DIST / 2) / MELD_DIST) ^ (((p.y + MELD_DIST / 2) / MELD_DIST) << 10) ^ (((p.z + MELD_DIST / 2) / MELD_DIST) << 20));
                    int idx = 0;
                    bool add = true;
                    if (indexMap.ContainsKey(hash))
                    {
                        for (int n = 0; n < indexMap[hash].Count; n++)
                        {
                            if ((vertices[indexMap[hash][n]].position - p).testLength(MELD_DIST))
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
                        indexMap[hash].Add(vertices.Count);
                        idx = vertices.Count;
                        vertices.Add(new OptimizedPoint3(p));
                    }
                    f.vertexIndex[j] = idx;
                }
                if (f.vertexIndex[0] != f.vertexIndex[1] && f.vertexIndex[0] != f.vertexIndex[2] && f.vertexIndex[1] != f.vertexIndex[2])
                {
                    //Check if there is a face with the same points
                    bool duplicate = false;
                    for (int _idx0 = 0; _idx0 < vertices[f.vertexIndex[0]].usedByFacesList.Count; _idx0++)
                    {
                        for (int _idx1 = 0; _idx1 < vertices[f.vertexIndex[1]].usedByFacesList.Count; _idx1++)
                        {
                            for (int _idx2 = 0; _idx2 < vertices[f.vertexIndex[2]].usedByFacesList.Count; _idx2++)
                            {
                                if (vertices[f.vertexIndex[0]].usedByFacesList[_idx0] == vertices[f.vertexIndex[1]].usedByFacesList[_idx1] && vertices[f.vertexIndex[0]].usedByFacesList[_idx0] == vertices[f.vertexIndex[2]].usedByFacesList[_idx2])
                                    duplicate = true;
                            }
                        }
                    }
                    if (!duplicate)
                    {
                        vertices[f.vertexIndex[0]].usedByFacesList.Add(facesTriangle.Count);
                        vertices[f.vertexIndex[1]].usedByFacesList.Add(facesTriangle.Count);
                        vertices[f.vertexIndex[2]].usedByFacesList.Add(facesTriangle.Count);
                        facesTriangle.Add(f);
                    }
                }
            }
            //fprintf(stdout, "\rAll faces are optimized in %5.1fs.\n",timeElapsed(t));

            int openFacesCount = 0;
            for (int i = 0; i < facesTriangle.Count; i++)
            {
                OptimizedFace f = facesTriangle[i];
                f.touchingFaces[0] = getFaceIdxWithPoints(f.vertexIndex[0], f.vertexIndex[1], i);
                f.touchingFaces[1] = getFaceIdxWithPoints(f.vertexIndex[1], f.vertexIndex[2], i);
                f.touchingFaces[2] = getFaceIdxWithPoints(f.vertexIndex[2], f.vertexIndex[0], i);
                if (f.touchingFaces[0] == -1)
                    openFacesCount++;
                if (f.touchingFaces[1] == -1)
                    openFacesCount++;
                if (f.touchingFaces[2] == -1)
                    openFacesCount++;
            }
            //fprintf(stdout, "  Number of open faces: %i\n", openFacesCount);
        }

        public int getFaceIdxWithPoints(int idx0, int idx1, int notFaceIdx)
        {
            for (int i = 0; i < vertices[idx0].usedByFacesList.Count; i++)
            {
                int f0 = vertices[idx0].usedByFacesList[i];
                if (f0 == notFaceIdx) continue;
                for (int j = 0; j < vertices[idx1].usedByFacesList.Count; j++)
                {
                    int f1 = vertices[idx1].usedByFacesList[j];
                    if (f1 == notFaceIdx) continue;
                    if (f0 == f1) return f0;
                }
            }
            return -1;
        }
    };
    public class OptimizedModel
    {
        public List<OptimizedVolume> volumes = new List<OptimizedVolume>();
        public Point3 size;
        public Point3 minXYZ;
        public Point3 maxXYZ;

        public OptimizedModel(SimpleModel model, Point3 center, bool centerObjectInXy)
        {
            for (int i = 0; i < model.volumes.Count; i++)
            {
                volumes.Add(new OptimizedVolume(model.volumes[i], this));
            }

            minXYZ = model.minXYZ();
            maxXYZ = model.maxXYZ();

            if (centerObjectInXy)
            {
                Point3 vOffset = new Point3((minXYZ.x + maxXYZ.x) / 2, (minXYZ.y + maxXYZ.y) / 2, minXYZ.z);
                vOffset -= center;
                for (int i = 0; i < volumes.Count; i++)
                {
                    for (int n = 0; n < volumes[i].vertices.Count; n++)
                    {
                        volumes[i].vertices[n].position -= vOffset;
                    }
                }

                minXYZ -= vOffset;
                maxXYZ -= vOffset;
            }

            size = maxXYZ - minXYZ;
        }

        public void saveDebugSTL(string filename)
        {
#if true
            OptimizedVolume vol = volumes[0];

            using (StreamWriter stream = new StreamWriter(filename))
            {
                BinaryWriter f = new BinaryWriter(stream.BaseStream);
                Byte[] header = new Byte[80];

                f.Write(header);

                int n = vol.facesTriangle.Count;

                f.Write(n);

                for (int i = 0; i < vol.facesTriangle.Count; i++)
                {
                    // stl expects a normal (we don't care about it's data)
                    f.Write((float)1);
                    f.Write((float)1);
                    f.Write((float)1);

                    for (int vert = 0; vert < 3; vert++)
                    {
                        f.Write((float)(vol.vertices[vol.facesTriangle[i].vertexIndex[vert]].position.x / 1000.0));
                        f.Write((float)(vol.vertices[vol.facesTriangle[i].vertexIndex[vert]].position.y / 1000.0));
                        f.Write((float)(vol.vertices[vol.facesTriangle[i].vertexIndex[vert]].position.z / 1000.0));
                    }

                    f.Write((short)0);
                }
            }
#else
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
#endif
        }
    }
}



