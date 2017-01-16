﻿/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;

namespace MSClipperLib
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public static class CLPolygonsExtensions
	{
		public static Polygons CreateFromString(string polygonsPackedString)
		{
			Polygons output = new Polygons();
			string[] polygons = polygonsPackedString.Split('|');
			foreach (string polygonString in polygons)
			{
				Polygon nextPoly = CLPolygonExtensions.CreateFromString(polygonString);
				if (nextPoly.Count > 0)
				{
					output.Add(nextPoly);
				}
			}
			return output;
		}

		public static IntRect GetBounds(this Polygons polygons)
		{
			var totalBounds = new IntRect(long.MaxValue, long.MaxValue, long.MinValue, long.MinValue);
			foreach (var polygon in polygons)
			{
				var polyBounds = polygon.GetBounds();
				totalBounds = totalBounds.ExpandToInclude(polyBounds);
			}

			return totalBounds;
		}

		public static Polygons Offset(this Polygons polygons, long distance)
		{
			ClipperOffset offseter = new ClipperOffset();
			offseter.AddPaths(polygons, JoinType.jtMiter, EndType.etClosedPolygon);
			Polygons solution = new Polygons();
			offseter.Execute(ref solution, distance);
			return solution;
		}

		public static long PolygonLength(this Polygons polygons, bool areClosed = true)
		{
			long length = 0;
			for (int i = 0; i < polygons.Count; i++)
			{
				length += polygons[i].PolygonLength(areClosed);
			}

			return length;
		}

		public static string WriteToString(this Polygons polygons)
		{
			string total = "";
			foreach (Polygon polygon in polygons)
			{
				total += polygon.WriteToString() + "|";
			}
			return total;
		}
	}
}