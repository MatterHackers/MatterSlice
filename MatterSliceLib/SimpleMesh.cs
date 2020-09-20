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
using MatterHackers.VectorMath;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	// A SimpleVolume is the most basic representation of a 3D model. It contains all the faces as SimpleTriangles, with nothing fancy.
	public class SimpleMesh
	{
		public List<SimpleFace> FaceTriangles { get; set; } = new List<SimpleFace>();

		public void AddFaceTriangle(IntPoint v0, IntPoint v1, IntPoint v2)
		{
			FaceTriangles.Add(new SimpleFace(v0, v1, v2));
		}

		public IntPoint MaxXYZ_um()
		{
			if (FaceTriangles.Count < 1)
			{
				return new IntPoint(0, 0, 0);
			}

			IntPoint ret = FaceTriangles[0].Vertices[0];
			for (int i = 0; i < FaceTriangles.Count; i++)
			{
				SET_MAX(ref ret.X, FaceTriangles[i].Vertices[0].X);
				SET_MAX(ref ret.Y, FaceTriangles[i].Vertices[0].Y);
				SET_MAX(ref ret.Z, FaceTriangles[i].Vertices[0].Z);
				SET_MAX(ref ret.X, FaceTriangles[i].Vertices[1].X);
				SET_MAX(ref ret.Y, FaceTriangles[i].Vertices[1].Y);
				SET_MAX(ref ret.Z, FaceTriangles[i].Vertices[1].Z);
				SET_MAX(ref ret.X, FaceTriangles[i].Vertices[2].X);
				SET_MAX(ref ret.Y, FaceTriangles[i].Vertices[2].Y);
				SET_MAX(ref ret.Z, FaceTriangles[i].Vertices[2].Z);
			}

			return ret;
		}

		public IntPoint MinXYZ_um()
		{
			if (FaceTriangles.Count < 1)
			{
				return new IntPoint(0, 0, 0);
			}

			IntPoint ret = FaceTriangles[0].Vertices[0];
			for (int faceIndex = 0; faceIndex < FaceTriangles.Count; faceIndex++)
			{
				SET_MIN(ref ret.X, FaceTriangles[faceIndex].Vertices[0].X);
				SET_MIN(ref ret.Y, FaceTriangles[faceIndex].Vertices[0].Y);
				SET_MIN(ref ret.Z, FaceTriangles[faceIndex].Vertices[0].Z);
				SET_MIN(ref ret.X, FaceTriangles[faceIndex].Vertices[1].X);
				SET_MIN(ref ret.Y, FaceTriangles[faceIndex].Vertices[1].Y);
				SET_MIN(ref ret.Z, FaceTriangles[faceIndex].Vertices[1].Z);
				SET_MIN(ref ret.X, FaceTriangles[faceIndex].Vertices[2].X);
				SET_MIN(ref ret.Y, FaceTriangles[faceIndex].Vertices[2].Y);
				SET_MIN(ref ret.Z, FaceTriangles[faceIndex].Vertices[2].Z);
			}

			return ret;
		}

		private void SET_MAX(ref long n, long m)
		{
			if (m > n)
			{
				n = m;
			}
		}

		private void SET_MIN(ref long n, long m)
		{
			if (m < n)
			{
				n = m;
			}
		}

		// A SimpleFace is a 3 dimensional model triangle with 3 points. These points are already converted to integers
		public class SimpleFace
		{
			public IntPoint[] Vertices { get; private set; } = new IntPoint[3];

			public SimpleFace(IntPoint v0, IntPoint v1, IntPoint v2)
			{
				Vertices[0] = v0;
				Vertices[1] = v1;
				Vertices[2] = v2;
			}
		}
	}
}