/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

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
    // A SimpleFace is a 3 dimensional model triangle with 3 points. These points are already converted to integers
    public class SimpleFace
    {
        public Point3[] v = new Point3[3];

        public SimpleFace(Point3 v0, Point3 v1, Point3 v2) { v[0] = v0; v[1] = v1; v[2] = v2; }
    };

    // A SimpleVolume is the most basic reprisentation of a 3D model. It contains all the faces as SimpleTriangles, with nothing fancy.
    public class SimpleVolume
    {
        public List<SimpleFace> faces = new List<SimpleFace>();

        void SET_MIN(ref int n, int m)
        {
            if ((m) < (n))
                n = m;
        }

        void SET_MAX(ref int n, int m)
        {
            if ((m) > (n))
                n = m;
        }

        public void addFace(Point3 v0, Point3 v1, Point3 v2)
        {
            faces.Add(new SimpleFace(v0, v1, v2));
        }

        public Point3 min()
        {
            Point3 ret = faces[0].v[0];
            for (int i = 0; i < faces.Count; i++)
            {
                SET_MIN(ref ret.x, faces[i].v[0].x);
                SET_MIN(ref ret.y, faces[i].v[0].y);
                SET_MIN(ref ret.z, faces[i].v[0].z);
                SET_MIN(ref ret.x, faces[i].v[1].x);
                SET_MIN(ref ret.y, faces[i].v[1].y);
                SET_MIN(ref ret.z, faces[i].v[1].z);
                SET_MIN(ref ret.x, faces[i].v[2].x);
                SET_MIN(ref ret.y, faces[i].v[2].y);
                SET_MIN(ref ret.z, faces[i].v[2].z);
            }
            return ret;
        }

        public Point3 max()
        {
            Point3 ret = faces[0].v[0];
            for (int i = 0; i < faces.Count; i++)
            {
                SET_MAX(ref ret.x, faces[i].v[0].x);
                SET_MAX(ref ret.y, faces[i].v[0].y);
                SET_MAX(ref ret.z, faces[i].v[0].z);
                SET_MAX(ref ret.x, faces[i].v[1].x);
                SET_MAX(ref ret.y, faces[i].v[1].y);
                SET_MAX(ref ret.z, faces[i].v[1].z);
                SET_MAX(ref ret.x, faces[i].v[2].x);
                SET_MAX(ref ret.y, faces[i].v[2].y);
                SET_MAX(ref ret.z, faces[i].v[2].z);
            }
            return ret;
        }
    }

    //A SimpleModel is a 3D model with 1 or more 3D volumes.
    public class SimpleModel
    {
        public static StreamWriter binaryMeshBlob;

        public List<SimpleVolume> volumes = new List<SimpleVolume>();

        void SET_MIN(ref int n, int m)
        {
            if ((m) < (n))
                n = m;
        }

        void SET_MAX(ref int n, int m)
        {
            if ((m) > (n))
                n = m;
        }

        public Point3 min()
        {
            Point3 ret = volumes[0].min();
            for (int i = 0; i < volumes.Count; i++)
            {
                Point3 v = volumes[i].min();
                SET_MIN(ref ret.x, v.x);
                SET_MIN(ref ret.y, v.y);
                SET_MIN(ref ret.z, v.z);
            }
            return ret;
        }

        public Point3 max()
        {
            Point3 ret = volumes[0].max();
            for (int i = 0; i < volumes.Count; i++)
            {
                Point3 v = volumes[i].max();
                SET_MAX(ref ret.x, v.x);
                SET_MAX(ref ret.y, v.y);
                SET_MAX(ref ret.z, v.z);
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

#if true
        public static SimpleModel loadModelSTL_ascii(string filename, FMatrix3x3 matrix)
        {
            SimpleModel m = new SimpleModel();
            m.volumes.Add(new SimpleVolume());
            SimpleVolume vol = m.volumes[0];
            using (StreamReader f = new StreamReader(filename))
            {
                // check for "SOLID"

                FPoint3 vertex = new FPoint3();
                int n = 0;
                Point3 v0 = new Point3(0, 0, 0);
                Point3 v1 = new Point3(0, 0, 0);
                Point3 v2 = new Point3(0, 0, 0);
                string line = f.ReadLine();
                while (line != null)
                {
                    var parts = line.Trim().Split(' ');
                    if (parts[0].Trim() == "vertex")
                    {
                        vertex.x = Convert.ToDouble(parts[1]);
                        vertex.y = Convert.ToDouble(parts[2]);
                        vertex.z = Convert.ToDouble(parts[3]);

                        // change the scale from mm to micrometers
                        vertex *= 1000.0;

                        n++;
                        switch (n)
                        {
                            case 1:
                                v0 = matrix.apply(vertex);
                                break;
                            case 2:
                                v1 = matrix.apply(vertex);
                                break;
                            case 3:
                                v2 = matrix.apply(vertex);
                                vol.addFace(v0, v1, v2);
                                n = 0;
                                break;
                        }
                    }
                    line = f.ReadLine();
                }

                return m;
            }
        }
#else
        SimpleModel loadModelSTL_ascii(string filename, FMatrix3x3 matrix)
        {
    SimpleModel m = new SimpleModel();
    m.volumes.Add(SimpleVolume());
    SimpleVolume* vol = &m.volumes[0];
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
                vol.addFace(v0, v1, v2);
                n = 0;
                break;
            }
        }
    }
    fclose(f);
    return m;
        }
#endif

        static SimpleModel loadModelSTL_binary(string filename, FMatrix3x3 matrix)
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
    m.volumes.Add(SimpleVolume());
    SimpleVolume* vol = &m.volumes[0];
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
        vol.addFace(v0, v1, v2);
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

        public static SimpleModel loadModelFromFile(string filename, FMatrix3x3 matrix)
        {
            SimpleModel fromAsciiModel = loadModelSTL_ascii(filename, matrix);
            if (fromAsciiModel == null)
            {
                return loadModelSTL_binary(filename, matrix);
            }

            return fromAsciiModel;
        }
    }
}