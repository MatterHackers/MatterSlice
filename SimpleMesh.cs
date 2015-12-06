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
using System.IO;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterSlice
{
	// A SimpleFace is a 3 dimensional model triangle with 3 points. These points are already converted to integers
	public class SimpleFace
	{
		public Point3[] vertices = new Point3[3];

		public SimpleFace(Point3 v0, Point3 v1, Point3 v2)
		{
			vertices[0] = v0; vertices[1] = v1; vertices[2] = v2;
		}
	};

	// A SimpleVolume is the most basic reprisentation of a 3D model. It contains all the faces as SimpleTriangles, with nothing fancy.
	public class SimpleMesh
	{
		public List<SimpleFace> faceTriangles = new List<SimpleFace>();

		private void SET_MIN(ref int n, int m)
		{
			if ((m) < (n))
				n = m;
		}

		private void SET_MIN(ref long n, long m)
		{
			if ((m) < (n))
				n = m;
		}

		private void SET_MAX(ref int n, int m)
		{
			if ((m) > (n))
				n = m;
		}

		private void SET_MAX(ref long n, long m)
		{
			if ((m) > (n))
				n = m;
		}

		public void addFaceTriangle(Point3 v0, Point3 v1, Point3 v2)
		{
			faceTriangles.Add(new SimpleFace(v0, v1, v2));
		}

		public Point3 minXYZ_um()
		{
			if (faceTriangles.Count < 1)
			{
				return new Point3(0, 0, 0);
			}

			Point3 ret = faceTriangles[0].vertices[0];
			for (int faceIndex = 0; faceIndex < faceTriangles.Count; faceIndex++)
			{
				SET_MIN(ref ret.x, faceTriangles[faceIndex].vertices[0].x);
				SET_MIN(ref ret.y, faceTriangles[faceIndex].vertices[0].y);
				SET_MIN(ref ret.z, faceTriangles[faceIndex].vertices[0].z);
				SET_MIN(ref ret.x, faceTriangles[faceIndex].vertices[1].x);
				SET_MIN(ref ret.y, faceTriangles[faceIndex].vertices[1].y);
				SET_MIN(ref ret.z, faceTriangles[faceIndex].vertices[1].z);
				SET_MIN(ref ret.x, faceTriangles[faceIndex].vertices[2].x);
				SET_MIN(ref ret.y, faceTriangles[faceIndex].vertices[2].y);
				SET_MIN(ref ret.z, faceTriangles[faceIndex].vertices[2].z);
			}
			return ret;
		}

		public Point3 maxXYZ_um()
		{
			if (faceTriangles.Count < 1)
			{
				return new Point3(0, 0, 0);
			}

			Point3 ret = faceTriangles[0].vertices[0];
			for (int i = 0; i < faceTriangles.Count; i++)
			{
				SET_MAX(ref ret.x, faceTriangles[i].vertices[0].x);
				SET_MAX(ref ret.y, faceTriangles[i].vertices[0].y);
				SET_MAX(ref ret.z, faceTriangles[i].vertices[0].z);
				SET_MAX(ref ret.x, faceTriangles[i].vertices[1].x);
				SET_MAX(ref ret.y, faceTriangles[i].vertices[1].y);
				SET_MAX(ref ret.z, faceTriangles[i].vertices[1].z);
				SET_MAX(ref ret.x, faceTriangles[i].vertices[2].x);
				SET_MAX(ref ret.y, faceTriangles[i].vertices[2].y);
				SET_MAX(ref ret.z, faceTriangles[i].vertices[2].z);
			}
			return ret;
		}
	}

	//A SimpleModel is a 3D model with 1 or more 3D volumes.
	public class SimpleMeshCollection
	{
		public List<SimpleMesh> SimpleMeshes = new List<SimpleMesh>();

		public Point3 minXYZ_um()
		{
			if (SimpleMeshes.Count < 1)
			{
				return new Point3(0, 0, 0);
			}

			Point3 minXYZ = SimpleMeshes[0].minXYZ_um();
			for (int volumeIndex = 1; volumeIndex < SimpleMeshes.Count; volumeIndex++)
			{
				Point3 volumeMinXYZ = SimpleMeshes[volumeIndex].minXYZ_um();
				minXYZ.x = Math.Min(minXYZ.x, volumeMinXYZ.x);
				minXYZ.y = Math.Min(minXYZ.y, volumeMinXYZ.y);
				minXYZ.z = Math.Min(minXYZ.z, volumeMinXYZ.z);
			}
			return minXYZ;
		}

		public Point3 maxXYZ_um()
		{
			if (SimpleMeshes.Count < 1)
			{
				return new Point3(0, 0, 0);
			}

			Point3 maxXYZ = SimpleMeshes[0].maxXYZ_um();
			for (int volumeIndex = 1; volumeIndex < SimpleMeshes.Count; volumeIndex++)
			{
				Point3 volumeMaxXYZ = SimpleMeshes[volumeIndex].maxXYZ_um();
				maxXYZ.x = Math.Max(maxXYZ.x, volumeMaxXYZ.x);
				maxXYZ.y = Math.Max(maxXYZ.y, volumeMaxXYZ.y);
				maxXYZ.z = Math.Max(maxXYZ.z, volumeMaxXYZ.z);
			}
			return maxXYZ;
		}

		public static bool loadModelSTL_ascii(SimpleMeshCollection simpleModel, string filename, FMatrix3x3 matrix)
		{
			SimpleMesh vol = new SimpleMesh();
			using (StreamReader f = new StreamReader(filename))
			{
				// check for "SOLID"

				FPoint3 vertex = new FPoint3();
				int n = 0;
				Point3 v0 = new Point3(0, 0, 0);
				Point3 v1 = new Point3(0, 0, 0);
				Point3 v2 = new Point3(0, 0, 0);
				string line = f.ReadLine();
				Regex onlySingleSpaces = new Regex("\\s+", RegexOptions.Compiled);
				while (line != null)
				{
					line = onlySingleSpaces.Replace(line, " ");
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
								vol.addFaceTriangle(v0, v1, v2);
								n = 0;
								break;
						}
					}
					line = f.ReadLine();
				}
			}

			if (vol.faceTriangles.Count > 3)
			{
				simpleModel.SimpleMeshes.Add(vol);
				return true;
			}

			return false;
		}

		private static bool loadModelSTL_binary(SimpleMeshCollection simpleModel, string filename, FMatrix3x3 matrix)
		{
			SimpleMesh vol = new SimpleMesh();
			using (FileStream stlStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				// load it as a binary stl
				// skip the first 80 bytes
				// read in the number of triangles
				stlStream.Position = 0;
				BinaryReader br = new BinaryReader(stlStream);
				byte[] fileContents = br.ReadBytes((int)stlStream.Length);
				int currentPosition = 80;
				uint numTriangles = System.BitConverter.ToUInt32(fileContents, currentPosition);
				long bytesForNormals = numTriangles * 3 * 4;
				long bytesForVertices = numTriangles * 3 * 4;
				long bytesForAttributs = numTriangles * 2;
				currentPosition += 4;
				long numBytesRequiredForVertexData = currentPosition + bytesForNormals + bytesForVertices + bytesForAttributs;
				if (fileContents.Length < numBytesRequiredForVertexData || numTriangles < 4)
				{
					stlStream.Close();
					return false;
				}

				Point3[] vector = new Point3[3];
				for (int i = 0; i < numTriangles; i++)
				{
					// skip the normal
					currentPosition += 3 * 4;
					for (int j = 0; j < 3; j++)
					{
						vector[j] = new Point3(
							System.BitConverter.ToSingle(fileContents, currentPosition + 0 * 4) * 1000,
							System.BitConverter.ToSingle(fileContents, currentPosition + 1 * 4) * 1000,
							System.BitConverter.ToSingle(fileContents, currentPosition + 2 * 4) * 1000);
						currentPosition += 3 * 4;
					}
					currentPosition += 2; // skip the attribute

					vol.addFaceTriangle(vector[2], vector[1], vector[0]);
				}
			}

			if (vol.faceTriangles.Count > 3)
			{
				simpleModel.SimpleMeshes.Add(vol);
				return true;
			}

			return false;
		}

		public static bool LoadModelFromFile(SimpleMeshCollection simpleModel, string filename, FMatrix3x3 matrix)
		{
			if (!loadModelSTL_ascii(simpleModel, filename, matrix))
			{
				return loadModelSTL_binary(simpleModel, filename, matrix);
			}

			return true;
		}
	}
}