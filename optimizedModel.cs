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

        public OptimizedModel parentModel;
        public List<OptimizedPoint3> vertices = new List<OptimizedPoint3>();
        public List<OptimizedFace> facesTriangle = new List<OptimizedFace>();

        public OptimizedVolume(SimpleVolume volume, OptimizedModel model)
        {
            this.parentModel = model;
            vertices.Capacity = volume.faceTriangles.Count * 3;
            facesTriangle.Capacity = volume.faceTriangles.Count;

            Dictionary<int, List<int>> indexMap = new Dictionary<int, List<int>>();

            Stopwatch t = new Stopwatch();
            t.Start();
            for (int faceIndex = 0; faceIndex < volume.faceTriangles.Count; faceIndex++)
            {
				if (MatterSlice.Canceled)
				{
					return;
				}
				OptimizedFace optimizedFace = new OptimizedFace();
                if ((faceIndex % 1000 == 0) && t.Elapsed.Seconds > 2)
                {
                    LogOutput.logProgress("optimized", faceIndex + 1, volume.faceTriangles.Count);
                }
                for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                {
                    Point3 p = volume.faceTriangles[faceIndex].vertices[vertexIndex];
                    int hash = (int)(((p.x + MELD_DIST / 2) / MELD_DIST) ^ (((p.y + MELD_DIST / 2) / MELD_DIST) << 10) ^ (((p.z + MELD_DIST / 2) / MELD_DIST) << 20));
                    int idx = 0;
                    bool add = true;
                    if (indexMap.ContainsKey(hash))
                    {
                        for (int n = 0; n < indexMap[hash].Count; n++)
                        {
                            if ((vertices[indexMap[hash][n]].position - p).AbsLengthLEQ(MELD_DIST))
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
                    optimizedFace.vertexIndex[vertexIndex] = idx;
                }
                if (optimizedFace.vertexIndex[0] != optimizedFace.vertexIndex[1] && optimizedFace.vertexIndex[0] != optimizedFace.vertexIndex[2] && optimizedFace.vertexIndex[1] != optimizedFace.vertexIndex[2])
                {
                    //Check if there is a face with the same points
                    bool duplicate = false;
                    for (int _idx0 = 0; _idx0 < vertices[optimizedFace.vertexIndex[0]].usedByFacesList.Count; _idx0++)
                    {
                        for (int _idx1 = 0; _idx1 < vertices[optimizedFace.vertexIndex[1]].usedByFacesList.Count; _idx1++)
                        {
                            for (int _idx2 = 0; _idx2 < vertices[optimizedFace.vertexIndex[2]].usedByFacesList.Count; _idx2++)
                            {
                                if (vertices[optimizedFace.vertexIndex[0]].usedByFacesList[_idx0] == vertices[optimizedFace.vertexIndex[1]].usedByFacesList[_idx1] && vertices[optimizedFace.vertexIndex[0]].usedByFacesList[_idx0] == vertices[optimizedFace.vertexIndex[2]].usedByFacesList[_idx2])
                                    duplicate = true;
                            }
                        }
                    }
                    if (!duplicate)
                    {
                        vertices[optimizedFace.vertexIndex[0]].usedByFacesList.Add(facesTriangle.Count);
                        vertices[optimizedFace.vertexIndex[1]].usedByFacesList.Add(facesTriangle.Count);
                        vertices[optimizedFace.vertexIndex[2]].usedByFacesList.Add(facesTriangle.Count);
                        facesTriangle.Add(optimizedFace);
                    }
                }
            }
            //fprintf(stdout, "\rAll faces are optimized in %5.1fs.\n",timeElapsed(t));

            int openFacesCount = 0;
            for (int faceIndex = 0; faceIndex < facesTriangle.Count; faceIndex++)
            {
                OptimizedFace optimizedFace = facesTriangle[faceIndex];
                optimizedFace.touchingFaces[0] = getOtherFaceIndexContainingVertices(optimizedFace.vertexIndex[0], optimizedFace.vertexIndex[1], faceIndex);
                optimizedFace.touchingFaces[1] = getOtherFaceIndexContainingVertices(optimizedFace.vertexIndex[1], optimizedFace.vertexIndex[2], faceIndex);
                optimizedFace.touchingFaces[2] = getOtherFaceIndexContainingVertices(optimizedFace.vertexIndex[2], optimizedFace.vertexIndex[0], faceIndex);
                if (optimizedFace.touchingFaces[0] == -1)
                {
                    openFacesCount++;
                }
                if (optimizedFace.touchingFaces[1] == -1)
                {
                    openFacesCount++;
                }
                if (optimizedFace.touchingFaces[2] == -1)
                {
                    openFacesCount++;
                }
            }
            //fprintf(stdout, "  Number of open faces: %i\n", openFacesCount);
        }

        public int getOtherFaceIndexContainingVertices(int vertex1Index, int vertex2Index, int faceWeKnow)
        {
            for (int vertex1FaceIndex = 0; vertex1FaceIndex < vertices[vertex1Index].usedByFacesList.Count; vertex1FaceIndex++)
            {
                int faceUsingVertex1 = vertices[vertex1Index].usedByFacesList[vertex1FaceIndex];
                if (faceUsingVertex1 == faceWeKnow)
                {
                    continue;
                }

                for (int vertex2FaceIndex = 0; vertex2FaceIndex < vertices[vertex2Index].usedByFacesList.Count; vertex2FaceIndex++)
                {
                    int faceUsingVertex2 = vertices[vertex2Index].usedByFacesList[vertex2FaceIndex];
                    if (faceUsingVertex2 == faceWeKnow)
                    {
                        continue;
                    }
                    
                    if (faceUsingVertex1 == faceUsingVertex2)
                    {
                        return faceUsingVertex1;
                    }
                }
            }

            return -1;
        }
    }

    public class OptimizedModel
    {
        public List<OptimizedVolume> volumes = new List<OptimizedVolume>();
        public Point3 size_um;
        public Point3 minXYZ_um;
        public Point3 maxXYZ_um;

        public OptimizedModel(SimpleModel model)
        {
            for (int i = 0; i < model.volumes.Count; i++)
            {
                volumes.Add(new OptimizedVolume(model.volumes[i], this));
				if (MatterSlice.Canceled)
				{
					return;
				}
			}
        }

        public void SetPositionAndSize(SimpleModel model, long xCenter_um, long yCenter_um, long zClip_um, bool centerObjectInXy)
        {
            minXYZ_um = model.minXYZ_um();
            maxXYZ_um = model.maxXYZ_um();

            if (centerObjectInXy)
            {
                Point3 modelXYCenterZBottom_um = new Point3((minXYZ_um.x + maxXYZ_um.x) / 2, (minXYZ_um.y + maxXYZ_um.y) / 2, minXYZ_um.z);
                modelXYCenterZBottom_um -= new Point3(xCenter_um, yCenter_um, zClip_um);
                for (int i = 0; i < volumes.Count; i++)
                {
                    for (int n = 0; n < volumes[i].vertices.Count; n++)
                    {
                        volumes[i].vertices[n].position -= modelXYCenterZBottom_um;
                    }
                }

                minXYZ_um -= modelXYCenterZBottom_um;
                maxXYZ_um -= modelXYCenterZBottom_um;
            }
            else // we still need to put in the bottom clip
            {
                // Ofset by bed center and correctly position in z
                Point3 modelZBottom_um = new Point3(-xCenter_um, -yCenter_um, minXYZ_um.z - zClip_um);
                for (int i = 0; i < volumes.Count; i++)
                {
                    for (int n = 0; n < volumes[i].vertices.Count; n++)
                    {
                        volumes[i].vertices[n].position -= modelZBottom_um;
                    }
                }

                minXYZ_um -= modelZBottom_um;
                maxXYZ_um -= modelZBottom_um;
            }

            size_um = maxXYZ_um - minXYZ_um;
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



