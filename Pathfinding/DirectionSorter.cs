/*
Copyright (c) 2015, Lars Brubaker
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

using System;
using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.Pathfinding
{
	public class IntPointDirectionSorter : IComparer<(int pointIndex, IntPoint position)>
	{
		private IntPoint direction;
		private long length;
		private IntPoint start;

		public IntPointDirectionSorter(IntPoint start, IntPoint end)
		{
			this.start = start;
			this.direction = end - start;
			length = direction.Length();
		}

		public int Compare((int pointIndex, IntPoint position) a, (int pointIndex, IntPoint position) b)
		{
			if (length > 0)
			{
				long distToA = direction.Dot(a.Item2 - start) / length;
				long distToB = direction.Dot(b.Item2 - start) / length;

				return distToA.CompareTo(distToB);
			}

			return 0;
		}
	}

	public class PolygonAndPointDirectionSorter : IComparer<(int polyIndex, int pointIndex, IntPoint position)>
	{
		private IntPoint direction;
		private long length;
		private IntPoint start;

		public PolygonAndPointDirectionSorter(IntPoint start, IntPoint end)
		{
			this.start = start;
			this.direction = end - start;
			length = direction.Length();
		}

		public int Compare((int polyIndex, int pointIndex, IntPoint position) a, 
			(int polyIndex, int pointIndex, IntPoint position) b)
		{
			if (length > 0)
			{
				long distToA = direction.Dot(a.Item3 - start) / length;
				long distToB = direction.Dot(b.Item3 - start) / length;

				return distToA.CompareTo(distToB);
			}

			return 0;
		}
	}
}