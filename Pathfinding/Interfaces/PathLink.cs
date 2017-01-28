// Copyright(c) 2012 Erik Svedäng, Johannes Gotlén, 2017 Lars Brubaker
//
// This software is provided 'as-is', without any express or implied
// warranty.In no event will the authors be held liable for any damages
// arising from the use of this software.
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
// 1. The origin of this software must not be misrepresented; you must not
//    claim that you wrote the original software.If you use this software
//    in a product, an acknowledgment in the product documentation would be
//    appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must not be
//    misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source distribution.

using System;
using System.Collections.Generic;

namespace MatterHackers.Pathfinding
{
	public class PathLink : IEnumerable<IPathNode>
	{
		public IPathNode nodeA;
		public IPathNode nodeB;
		private float distance;

		public PathLink(IPathNode pNodeA, IPathNode pNodeB)
		{
			Distance = pNodeA.DistanceTo(pNodeB);
			nodeA = pNodeA;
			nodeB = pNodeB;
		}

		public int Count
		{
			get
			{
				return 2;
			}
		}

		public float Distance
		{
			get { return distance; }
			set
			{
				distance = value;
			}
		}

		public IPathNode this[int index]
		{
			get
			{
				if (index == 0)
				{
					return nodeA;
				}

				if (index == 1)
				{
					return nodeB;
				}

				return null;
			}
			set
			{
				if (index == 0)
				{
					nodeA = value;
				}

				if (index == 1)
				{
					nodeB = value;
				}
			}
		}

		public void Add(IPathNode item)
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			nodeA = null;
			nodeB = null;
		}

		public bool Contains(IPathNode item)
		{
			if (nodeA == item || nodeB == item)
			{
				return true;
			}

			return false;
		}

		public IEnumerator<IPathNode> GetEnumerator()
		{
			yield return nodeA;
			yield return nodeB;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			yield return nodeA;
			yield return nodeB;
		}

		public IPathNode GetOtherNode(IPathNode pSelf)
		{
			if (nodeA == pSelf)
			{
				return nodeB;
			}
			else if (nodeB == pSelf)
			{
				return nodeA;
			}
			else {
				throw new Exception("Function must be used with a parameter that's contained by the link");
			}
		}

		public int IndexOf(IPathNode item)
		{
			throw new NotImplementedException();
		}

		public void Insert(int index, IPathNode item)
		{
			throw new NotImplementedException();
		}

		public void RemoveAt(int index)
		{
			throw new NotImplementedException();
		}
	}
}