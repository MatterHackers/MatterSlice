/*
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

	public static class PolygonExtensions
	{
		public static IntRect GetBounds(this Polygon inPolygon)
		{
			if (inPolygon.Count == 0)
			{
				return new IntRect(0, 0, 0, 0);
			}

			IntRect result = new IntRect();
			result.minX = inPolygon[0].X;
			result.maxX = result.minX;
			result.minY = inPolygon[0].Y;
			result.maxY = result.minY;
			for (int pointIndex = 1; pointIndex < inPolygon.Count; pointIndex++)
			{
				if (inPolygon[pointIndex].X < result.minX)
				{
					result.minX = inPolygon[pointIndex].X;
				}
				else if (inPolygon[pointIndex].X > result.maxX)
				{
					result.maxX = inPolygon[pointIndex].X;
				}

				if (inPolygon[pointIndex].Y > result.maxY)
				{
					result.maxY = inPolygon[pointIndex].Y;
				}
				else if (inPolygon[pointIndex].Y < result.minY)
				{
					result.minY = inPolygon[pointIndex].Y;
				}
			}

			return result;
		}
	}
}