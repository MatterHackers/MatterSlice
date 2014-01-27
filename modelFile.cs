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
    //#define SET_MIN(n, m) do { if ((m) < (n)) n = m; } while(0)
    //#define SET_MAX(n, m) do { if ((m) > (n)) n = m; } while(0)

    /* A SimpleFace is a 3 dimensional model triangle with 3 points. These points are already converted to integers */
    public class SimpleFace
    {
        public Point3[] v = new Point[3];

        public SimpleFace(Point3 v0, Point3 v1, Point3 v2) { v[0] = v0; v[1] = v1; v[2] = v2; }
    };

    /* A SimpleVolume is the most basic reprisentation of a 3D model. It contains all the faces as SimpleTriangles, with nothing fancy. */
    public class SimpleVolume
    {
        public List<SimpleFace> faces;

        public void addFace(Point3 v0, Point3 v1, Point3 v2)
        {
            faces.Add(SimpleFace(v0, v1, v2));
        }

        public Point3 min()
        {
            Point3 ret = faces[0].v[0];
            for (int i = 0; i < faces.Count; i++)
            {
                SET_MIN(ret.x, faces[i].v[0].x);
                SET_MIN(ret.y, faces[i].v[0].y);
                SET_MIN(ret.z, faces[i].v[0].z);
                SET_MIN(ret.x, faces[i].v[1].x);
                SET_MIN(ret.y, faces[i].v[1].y);
                SET_MIN(ret.z, faces[i].v[1].z);
                SET_MIN(ret.x, faces[i].v[2].x);
                SET_MIN(ret.y, faces[i].v[2].y);
                SET_MIN(ret.z, faces[i].v[2].z);
            }
            return ret;
        }

        public Point3 max()
        {
            Point3 ret = faces[0].v[0];
            for (int i = 0; i < faces.Count; i++)
            {
                SET_MAX(ret.x, faces[i].v[0].x);
                SET_MAX(ret.y, faces[i].v[0].y);
                SET_MAX(ret.z, faces[i].v[0].z);
                SET_MAX(ret.x, faces[i].v[1].x);
                SET_MAX(ret.y, faces[i].v[1].y);
                SET_MAX(ret.z, faces[i].v[1].z);
                SET_MAX(ret.x, faces[i].v[2].x);
                SET_MAX(ret.y, faces[i].v[2].y);
                SET_MAX(ret.z, faces[i].v[2].z);
            }
            return ret;
        }
    }

    //A SimpleModel is a 3D model with 1 or more 3D volumes.
    public class SimpleModel
    {
        public static StreamWriter binaryMeshBlob;

        public List<SimpleVolume> volumes;

        public Point3 min()
        {
            Point3 ret = volumes[0].min();
            for (int i = 0; i < volumes.Count; i++)
            {
                Point3 v = volumes[i].min();
                SET_MIN(ret.x, v.x);
                SET_MIN(ret.y, v.y);
                SET_MIN(ret.z, v.z);
            }
            return ret;
        }

        public Point3 max()
        {
            Point3 ret = volumes[0].max();
            for (int i = 0; i < volumes.Count; i++)
            {
                Point3 v = volumes[i].max();
                SET_MAX(ret.x, v.x);
                SET_MAX(ret.y, v.y);
                SET_MAX(ret.z, v.z);
            }
            return ret;
        }

#if false
        /* Custom fgets function to support Mac line-ends in Ascii STL files. OpenSCAD produces this when used on Mac */
        void* fgets_(char* ptr, size_t len, FILE* f)
        {
            while (len && fread(ptr, 1, 1, f) > 0)
            {
                if (*ptr == '\n' || *ptr == '\r')
                {
                    *ptr = '\0';
                    return ptr;
                }
                ptr++;
                len--;
            }
            return NULL;
        }
#endif

        SimpleModel loadModelSTL_ascii(string filename, FMatrix3x3 matrix)
        {
            throw new NotImplementedException();
#if false
    SimpleModel m = new SimpleModel();
    m->volumes.Add(SimpleVolume());
    SimpleVolume* vol = &m->volumes[0];
    FILE* f = fopen(filename, "rt");
    char buffer[1024];
    FPoint3 vertex;
    int n = 0;
    Point3 v0(0,0,0), v1(0,0,0), v2(0,0,0);
    while(fgets_(buffer, sizeof(buffer), f))
    {
        if (sscanf(buffer, " vertex %lf %lf %lf", vertex.x, vertex.y, vertex.z) == 3)
        {
            n++;
            switch(n)
            {
            case 1:
                v0 = matrix.apply(vertex);
                break;
            case 2:
                v1 = matrix.apply(vertex);
                break;
            case 3:
                v2 = matrix.apply(vertex);
                vol->addFace(v0, v1, v2);
                n = 0;
                break;
            }
        }
    }
    fclose(f);
    return m;
#endif
        }

        SimpleModel loadModelSTL_binary(string filename, FMatrix3x3 matrix)
        {
            throw new NotImplementedException();
#if false
    StreamReader f = new StreamReader(filename);
    char[] buffer = new char[80];
    int faceCount;
    //Skip the header
    if (fread(buffer, 80, 1, f) != 1)
    {
        fclose(f);
        return NULL;
    }
    //Read the face count
    if (fread(&faceCount, sizeof(int), 1, f) != 1)
    {
        fclose(f);
        return NULL;
    }
    //For each face read:
    //float(x,y,z) = normal, float(X,Y,Z)*3 = vertexes, uint16_t = flags
    SimpleModel m = new SimpleModel();
    m->volumes.Add(SimpleVolume());
    SimpleVolume* vol = &m->volumes[0];
	if(vol == NULL)
	{
		fclose(f);
		return NULL;
	}

    for(int i=0;i<faceCount;i++)
    {
        if (fread(buffer, sizeof(float) * 3, 1, f) != 1)
        {
            fclose(f);
            return NULL;
        }
        float v[9];
        if (fread(v, sizeof(float) * 9, 1, f) != 1)
        {
            fclose(f);
            return NULL;
        }
        Point3 v0 = matrix.apply(FPoint3(v[0], v[1], v[2]));
        Point3 v1 = matrix.apply(FPoint3(v[3], v[4], v[5]));
        Point3 v2 = matrix.apply(FPoint3(v[6], v[7], v[8]));
        vol->addFace(v0, v1, v2);
        if (fread(buffer, sizeof(uint16_t), 1, f) != 1)
        {
            fclose(f);
            return NULL;
        }
    } 
    fclose(f);
    return m;
#endif
        }

        SimpleModel loadModelSTL(string filename, FMatrix3x3 matrix)
        {
            throw new NotImplementedException();
#if false
    FILE* f = fopen(filename, "r");
    char buffer[6];
    if (f == NULL)
        return NULL;
    
    if (fread(buffer, 5, 1, f) != 1)
    {
        fclose(f);
        return NULL;
    }
    fclose(f);

    buffer[5] = '\0';
    if (strcasecmp(buffer, "SOLID") == 0)
    {
        return loadModelSTL_ascii(filename, matrix);
    }
    return loadModelSTL_binary(filename, matrix);
}

SimpleModel loadModel(string filename, FMatrix3x3 matrix)
{
    string ext = strrchr(filename, '.');
    if (ext && strcasecmp(ext, ".stl") == 0)
    {
        return loadModelSTL(filename, matrix);
    }
    if (filename[0] == '#' && binaryMeshBlob != NULL)
    {
        SimpleModel m = new SimpleModel();
        
        while(*filename == '#')
        {
            filename++;
            
            m->volumes.Add(SimpleVolume());
            SimpleVolume* vol = &m->volumes[m->volumes.Count-1];
            int32_t n, pNr = 0;
            if (fread(&n, 1, sizeof(int32_t), binaryMeshBlob) < 1)
                return NULL;
            log("Reading mesh from binary blob with %i vertexes\n", n);
            Point3 v[3];
            while(n)
            {
                float f[3];
                if (fread(f, 3, sizeof(float), binaryMeshBlob) < 1)
                    return NULL;
                FPoint3 fp(f[0], f[1], f[2]);
                v[pNr++] = matrix.apply(fp);
                if (pNr == 3)
                {
                    vol->addFace(v[0], v[1], v[2]);
                    pNr = 0;
                }
                n--;
            }
        }
        return m;
    }
    return NULL;
#endif
        }
    }
}