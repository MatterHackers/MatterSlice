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

using System.Collections.Generic;

namespace MatterHackers.Pathfinding.Datastructures
{
	public class AStarStack
	{
		private Dictionary<long, IPathNode> _nodes = new Dictionary<long, IPathNode>();

		public int Count
		{
			get
			{
				return _nodes.Values.Count;
			}
		}

		public IPathNode Pop()
		{
			IPathNode result = null;

			foreach (IPathNode p in _nodes.Values)
			{
				if (result == null || p.CompareTo(result) == 1)
				{
					result = p;    //p has a shorter distance than result
				}
			}

			if (result == null)
			{
				return null;
			}
			else
			{
				_nodes.Remove(result.GetUniqueID());
				return result;
			}
		}

		public void Push(IPathNode pNode)
		{
			_nodes[pNode.GetUniqueID()] = pNode;
		}
	}
}