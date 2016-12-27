using System;
using System.Collections.Generic;
using MatterHackers.MatterSlice;
using MSClipperLib;
using Pathfinding;

namespace Pathfinding
{
	public class IntPointNode : IPathNode
	{
		#region IPathNode Members

		public IntPointNode(int pX, int pY)
		{
			LocalPoint = new IntPoint(pX, pY);
		}

		public IntPointNode(IntPoint intPoint)
		{
			LocalPoint = intPoint;
		}

		public IntPoint LocalPoint { get; private set; }

		public float PathCostHere { get; set; }

		public float DistanceToGoal { get; set; }

		public float CostMultiplier { get; set; } = 1;

		public bool IsStartNode { get; set; }

		public bool IsGoalNode { get; set; }

		public bool Visited { get; set; }

		public List<PathLink> Links { get; private set; } = new List<PathLink>();

		public PathLink LinkLeadingHere { get; set; }

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

		public void AddLink(PathLink pLink)
		{
			Links.Add(pLink);
		}

		public void RemoveLink(PathLink pLink)
		{
			Links.Remove(pLink);
		}

		#endregion

		#region IPoint Members

		public virtual float DistanceTo(Pathfinding.IPoint pPoint)
		{
			if (pPoint is IntPointNode)
			{
				IntPointNode otherNode = pPoint as IntPointNode;
				return (LocalPoint - otherNode.LocalPoint).Length();
			}
			else {
				throw new NotImplementedException();
			}
		}

		public override int GetHashCode()
		{
			return (int)LocalPoint.X + (int)(LocalPoint.Y * 1000);
		}

		#endregion

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
			else {
				return -1;
			}
		}

		#endregion

		public virtual long GetUniqueID()
		{
			return LocalPoint.X ^ LocalPoint.Y;
		}
	}
}
