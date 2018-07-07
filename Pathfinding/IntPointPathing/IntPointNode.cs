// Copyright(c) 2017 Lars Brubaker
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
using MSClipperLib;

namespace MatterHackers.Pathfinding
{
	public static class NodeExtensions
	{
		public static IEnumerable<IntPointNode> SkipSamePosition(this IEnumerable<IntPointNode> source)
		{
			return SkipSamePosition(source, new IntPoint(long.MaxValue, long.MaxValue));
		}

		public static IEnumerable<IntPointNode> SkipSamePosition(this IEnumerable<IntPointNode> source, IntPoint skipPosition)
		{
			foreach (var item in source)
			{
				if (item.Position != skipPosition)
				{
					yield return item;
				}
				skipPosition = item.Position;
			}
		}
	}

	public class IntPointNode : IPathNode
	{
		#region IPathNode Members

		public IntPointNode(long pX, long pY)
		{
			Position = new IntPoint(pX, pY);
		}

		public IntPointNode(IntPoint intPoint)
		{
			Position = intPoint;
		}

		public float CostMultiplier { get; set; } = 1;
		public float DistanceToGoal { get; set; }
		public bool IsGoalNode { get; set; }
		public bool IsStartNode { get; set; }
		public PathLink LinkLeadingHere { get; set; }
		public List<PathLink> Links { get; private set; } = new List<PathLink>();
		public float PathCostHere { get; set; }
		public IntPoint Position { get; private set; }
		public bool Visited { get; set; }

		public void AddLink(PathLink pLink)
		{
			Links.Add(pLink);
		}

		public PathLink GetLinkTo(IPathNode pNode)
		{
			if (Links != null)
			{
				foreach (PathLink p in Links)
				{
					if (p.Contains(pNode))
					{
						return p;
					}
				}
			}

			return null;
		}

		public void RemoveLink(PathLink pLink)
		{
			Links.Remove(pLink);
		}

		#endregion IPathNode Members

		#region IPoint Members

		public virtual float DistanceTo(Pathfinding.IPoint pPoint)
		{
			if (pPoint is IntPointNode)
			{
				IntPointNode otherNode = pPoint as IntPointNode;
				return (Position - otherNode.Position).Length();
			}
			else
			{
				throw new NotImplementedException();
			}
		}

		public override int GetHashCode()
		{
			return (int)Position.X + (int)(Position.Y * 1000);
		}

		#endregion IPoint Members

		#region IComparable Members

		public int CompareTo(object obj)
		{
			IntPointNode target = obj as IntPointNode;
			float targetValue = target.PathCostHere + target.DistanceToGoal;
			float thisValue = PathCostHere + DistanceToGoal;

			if (targetValue > thisValue)
			{
				return 1;
			}
			else if (targetValue == thisValue)
			{
				return 0;
			}
			else
			{
				return -1;
			}
		}

		#endregion IComparable Members

		public virtual long GetUniqueID()
		{
			return Position.X ^ Position.Y;
		}

		public override string ToString()
		{
			return $"Pos: {Position.X}, {Position.Y} - Links: {Links.Count}";
		}
	}
}