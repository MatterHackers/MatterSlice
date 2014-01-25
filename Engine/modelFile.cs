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
using System.IO;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
    // A SimpleFace is a 3 dimensional model triangle with 3 points. These points are already converted to integers
    public class SimpleFace
    {
        public Point3[] v = new Point3[3];

        public SimpleFace(Point3 v0, Point3 v1, Point3 v2)
        {
            v[0] = v0; v[1] = v1; v[2] = v2;
        }
    }

    // A SimpleVolume is the most basic reprisentation of a 3D model. It contains all the faces as SimpleTriangles, with nothing fancy.
    public class SimpleVolume
    {
        public List<SimpleFace> faces = new List<SimpleFace>();

        public void addFace(Point3 v0, Point3 v1, Point3 v2)
        {
            faces.Add(new SimpleFace(v0, v1, v2));
        }

        public Point3 min()
        {
            Point3 ret = faces[0].v[0];
            for (int i = 0; i < faces.Count; i++)
            {
                ret.x = Math.Min(ret.x, Math.Min(faces[i].v[0].x, Math.Min(faces[i].v[1].x, faces[i].v[2].x)));
                ret.y = Math.Min(ret.y, Math.Min(faces[i].v[0].y, Math.Min(faces[i].v[1].y, faces[i].v[2].y)));
                ret.z = Math.Min(ret.z, Math.Min(faces[i].v[0].z, Math.Min(faces[i].v[1].z, faces[i].v[2].z)));
            }
            return ret;
        }

        public Point3 max()
        {
            Point3 ret = faces[0].v[0];
            for (int i = 0; i < faces.Count; i++)
            {
                ret.x = Math.Max(ret.x, Math.Max(faces[i].v[0].x, Math.Max(faces[i].v[1].x, faces[i].v[2].x)));
                ret.y = Math.Max(ret.y, Math.Max(faces[i].v[0].y, Math.Max(faces[i].v[1].y, faces[i].v[2].y)));
                ret.z = Math.Max(ret.z, Math.Max(faces[i].v[0].z, Math.Max(faces[i].v[1].z, faces[i].v[2].z)));
            }
            return ret;
        }
    }

    //A SimpleModel is a 3D model with 1 or more 3D volumes.
    public class SimpleModel
    {
        public List<SimpleVolume> volumes = new List<SimpleVolume>();

        public Point3 min()
        {
            Point3 ret = volumes[0].min();
            for (int i = 0; i < volumes.Count; i++)
            {
                Point3 v = volumes[i].min();
                ret.x = Math.Min(ret.x, v.x);
                ret.y = Math.Min(ret.y, v.y);
                ret.z = Math.Min(ret.z, v.z);
            }
            return ret;
        }

        public Point3 max()
        {
            Point3 ret = volumes[0].max();
            for (int i = 0; i < volumes.Count; i++)
            {
                Point3 v = volumes[i].max();
                ret.x = Math.Max(ret.x, v.x);
                ret.y = Math.Max(ret.y, v.y);
                ret.z = Math.Max(ret.z, v.z);
            }
            return ret;
        }

        public static SimpleModel loadModel(string filename, FMatrix3x3 matrix)
        {
            if (Path.GetExtension(filename).ToUpper() == ".STL")
            {
                return loadModelSTL(filename, matrix);
            }

            return null;
        }


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

        public static SimpleModel loadModelSTL_binary(string filename, FMatrix3x3 matrix)
        {
            throw new NotImplementedException();
#if false
            FILE* f = fopen(filename, "rb");
            char buffer = new char[80];
            uint faceCount;
            //Skip the header
            if (fread(buffer, 80, 1, f) != 1)
            {
                fclose(f);
                return NULL;
            }
            //Read the face count
            if (fread(&faceCount, sizeof(uint), 1, f) != 1)
            {
                fclose(f);
                return NULL;
            }
            //For each face read:
            //float(x,y,z) = normal, float(X,Y,Z)*3 = vertexes, uint16_t = flags
            SimpleModel* m = new SimpleModel();
            m.volumes.Add(SimpleVolume());
            SimpleVolume* vol = &m.volumes[0];
            for (uint i = 0; i < faceCount; i++)
            {
                if (fread(buffer, sizeof(float) * 3, 1, f) != 1)
                {
                    fclose(f);
                    return NULL;
                }
                float[] v = new float[9];
                if (fread(v, sizeof(float) * 9, 1, f) != 1)
                {
                    fclose(f);
                    return NULL;
                }
                Point3 v0 = matrix.apply(new FPoint3(v[0], v[1], v[2]));
                Point3 v1 = matrix.apply(new FPoint3(v[3], v[4], v[5]));
                Point3 v2 = matrix.apply(new FPoint3(v[6], v[7], v[8]));
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

        public static SimpleModel loadModelSTL(string filename, FMatrix3x3 matrix)
        {
            SimpleModel fromAsciiModel = loadModelSTL_ascii(filename, matrix);
            if(fromAsciiModel == null)
            {
                return loadModelSTL_binary(filename, matrix);
            }

            return fromAsciiModel;
        }

        public void saveModelSTL(string filename)
        {
            using (StreamWriter stream = new StreamWriter(filename))
            {
                BinaryWriter f = new BinaryWriter(stream.BaseStream);
                Byte[] header = new Byte[80];

                f.Write(header);

                int n = volumes[0].faces.Count;

                f.Write(n);

                for (int i = 0; i < volumes[0].faces.Count; i++)
                {
                    // stl expects a normal (we don't care about it's data)
                    f.Write((float)1);
                    f.Write((float)1);
                    f.Write((float)1);

                    for (int vert = 0; vert < 3; vert++)
                    {
                        f.Write((float)(volumes[0].faces[i].v[vert].x / 1000.0));
                        f.Write((float)(volumes[0].faces[i].v[vert].y / 1000.0));
                        f.Write((float)(volumes[0].faces[i].v[vert].z / 1000.0));
                    }

                    f.Write((short)0);
                }
            }

        }

    }
}