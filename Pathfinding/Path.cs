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

using System.Text;

namespace MatterHackers.Pathfinding
{
	public enum PathStatus
	{
		NOT_CALCULATED_YET,
		DESTINATION_UNREACHABLE,
		FOUND_GOAL,
		ALREADY_THERE
	}

	public struct Path<PathNodeType> where PathNodeType : IPathNode
	{
		public Path(PathNodeType[] pNodes, float pathLength, PathStatus pStatus, int pPathSearchTestCount)
		{
			Nodes = pNodes;
			PathLength = pathLength;
			Status = pStatus;
			PathSearchTestCount = pPathSearchTestCount;
		}

		public static Path<PathNodeType> EMPTY
		{
			get
			{
				return new Path<PathNodeType>(new PathNodeType[0], 0f, PathStatus.NOT_CALCULATED_YET, 0);
			}
		}

		public PathNodeType LastNode
		{
			get
			{
				return Nodes[Nodes.Length - 1];
			}
		}

		public PathNodeType[] Nodes { get; private set; }
		public float PathLength { get; private set; }
		public int PathSearchTestCount { get; private set; }
		public PathStatus Status { get; private set; }

		public static bool operator !=(Path<PathNodeType> a, Path<PathNodeType> b)
		{
			return !a.Equals(b);
		}

		public static bool operator ==(Path<PathNodeType> a, Path<PathNodeType> b)
		{
			return a.Equals(b);
		}

		public override bool Equals(object pOther)
		{
			if (!(pOther is Path<PathNodeType>))
			{
				return false;
			}

			var other = (Path<PathNodeType>)pOther;
			if (Status != other.Status)
			{
				return false;
			}
			else if (PathLength != other.PathLength)
			{
				return false;
			}

			for (int i = 0; i < PathLength; i++)
			{
				if ((System.IEquatable<PathNodeType>)Nodes[i] != (System.IEquatable<PathNodeType>)other.Nodes[i])
				{
					return false;
				}
			}

			return true;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("Path: \n[ ");

			foreach (IPathNode ipn in Nodes)
			{
				sb.Append(ipn.ToString() + ",\n");
			}

			sb.Append("]");
			return sb.ToString();
		}
	}
}