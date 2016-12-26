using System;
using System.Collections.Generic;
using MatterHackers.MatterSlice;
using MSClipperLib;
using Pathfinding;

namespace Pathfinding
{
	public class IntPointNode : IPathNode
	{
		public int number = 0;

		private IntPoint _localPoint;
		private float _costHere;
		private float _distanceToGoal;
		private bool _isStartNode = false;
		private bool _isGoalNode = false;
		private bool _visited = false;
		private List<PathLink> _links = new List<PathLink>();
		private PathLink _previousLink = null;

		#region IPathNode Members

		public IntPointNode(int pX, int pY)
		{
			_localPoint = new IntPoint(pX, pY);
		}

		public IntPointNode(IntPoint intPoint)
		{
			_localPoint = intPoint;
		}

		public IntPoint localPoint
		{
			get
			{
				return _localPoint;
			}
		}

		public float PathCostHere
		{
			get
			{
				return _costHere;
			}
			set
			{
				_costHere = value;
			}
		}

		public float DistanceToGoal
		{
			get
			{
				return _distanceToGoal;
			}
			set
			{
				_distanceToGoal = value;
			}
		}

		public float BaseCost
		{
			get
			{
				return number * 10;
			}
		}

		public bool IsStartNode
		{
			get
			{
				return _isStartNode;
			}
			set
			{
				_isStartNode = value;
			}
		}

		public bool IsGoalNode
		{
			get
			{
				return _isGoalNode;
			}
			set
			{
				_isGoalNode = value;
			}
		}

		public bool Visited
		{
			get
			{
				return _visited;
			}
			set
			{
				_visited = value;
			}
		}

		public List<PathLink> Links
		{
			get
			{
				return _links;
			}
			set
			{
				_links = value;
			}
		}

		public PathLink LinkLeadingHere
		{
			get
			{
				return _previousLink;
			}
			set
			{
				_previousLink = value;
			}
		}

		public PathLink GetLinkTo(IPathNode pNode)
		{
			if (_links != null)
			{
				foreach (PathLink p in _links)
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
			_links.Add(pLink);
		}

		public void RemoveLink(PathLink pLink)
		{
			_links.Remove(pLink);
		}

		#endregion

		#region IPoint Members

		public virtual float DistanceTo(Pathfinding.IPoint pPoint)
		{
			if (pPoint is IntPointNode)
			{
				IntPointNode otherNode = pPoint as IntPointNode;
				return (_localPoint - otherNode._localPoint).Length();
			}
			else {
				throw new NotImplementedException();
			}
		}

		public override int GetHashCode()
		{
			return (int)_localPoint.X + (int)(_localPoint.Y * 1000);
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
			return _localPoint.X ^ _localPoint.Y;
		}
	}
}
