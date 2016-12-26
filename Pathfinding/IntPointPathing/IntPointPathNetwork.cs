using System;
using System.Collections.Generic;
using MSClipperLib;

namespace Pathfinding
{
	using MatterHackers.MatterSlice;
	using Polygon = List<IntPoint>;

	public class IntPointPathNetwork : IPathNetwork<IntPointNode>
	{
		private static int allocCount = 0;
		private static int OFFSET = 10000;

		private Dictionary<int, IntPointNode> _nodes = new Dictionary<int, IntPointNode>(100000);
		private IPathNode _pathGoal = null;
		private IPathNode _pathStart = null;

		public IntPointPathNetwork(string data)
		{
			Setup(data, 10, 10);
		}

		public IntPointPathNetwork(Polygon data)
		{
			Setup(data);
		}

		public IntPointPathNetwork(string data, int width, int height)
		{
			Setup(data, width, height);
		}

		public IntPointNode GetNode(int pX, int pY)
		{
			return _nodes[pX + pY * OFFSET];
		}

		public IntPointNode GetNode(IPoint pPoint)
		{
			return _nodes[pPoint.GetHashCode()];
		}

		public void Reset()
		{
			foreach (IPathNode node in _nodes.Values)
			{
				node.IsGoalNode = false;
				node.IsStartNode = false;
				node.DistanceToGoal = 0f;
				node.PathCostHere = 0f;
				node.Visited = false;
				node.LinkLeadingHere = null;
			}
			Console.WriteLine("Reset " + _nodes.Values.Count + " nodes");
		}

		public void SetGoal(IPoint pPosition)
		{
			_pathGoal = _nodes[pPosition.GetHashCode()];
			_pathGoal.IsGoalNode = true;
		}

		public void SetStart(IPoint pPosition)
		{
			_pathStart = _nodes[pPosition.GetHashCode()];
			_pathStart.IsStartNode = true;
		}

		internal void Setup(Polygon data)
		{
			for (int i = 0; i < data.Count; i++)
			{
				IntPointNode n = new IntPointNode(data[i]);
				n.number = 1;
				_nodes.Add((int)n.localPoint.X + (int)n.localPoint.Y * OFFSET, n);
			}
		}

		internal void Setup(string data, int width, int height)
		{
			string[] values = data.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			//Console.WriteLine("value count " + values.Length);
			for (int i = 0; i < values.Length; i++)
			{
				IntPointNode n = new IntPointNode(i % height, i / width);
				n.number = Convert.ToInt32(values[i]);
				_nodes.Add((int)n.localPoint.X + (int)n.localPoint.Y * OFFSET, n);
			}

			int size = (width * height);
			for (int i = 0; i < size; i++)
			{
				int x = i % height;
				int y = i / width;
				// Console.WriteLine("setting links for " + x + ", " + y + ", i " + i);
				IntPointNode start = GetNode(x, y);
				IntPointNode outputNode;

				if (TryGetNode(x + 1, y, out outputNode))
				{
					AddPathLink(start, outputNode);
				}

				if (TryGetNode(x - 1, y, out outputNode))
				{
					AddPathLink(start, outputNode);
				}

				if (TryGetNode(x, y + 1, out outputNode))
				{
					AddPathLink(start, outputNode);
				}

				if (TryGetNode(x, y - 1, out outputNode))
				{
					AddPathLink(start, outputNode);
				}
			}
		}

		internal bool TryGetNode(int pX, int pY, out IntPointNode outputNode)
		{
			return _nodes.TryGetValue(pX + pY * OFFSET, out outputNode);
		}

		private static void IncAlloc()
		{
			allocCount++;
			Console.WriteLine("Alloc count: " + allocCount);
		}

		private void AddPathLink(IntPointNode nodeA, IntPointNode nodeB)
		{
			PathLink link = nodeB.GetLinkTo(nodeA);

			if (link == null)
			{
				link = new PathLink(nodeA, nodeB);
			}

			link.Distance = (nodeA.localPoint - nodeB.localPoint).Length();
			nodeA.Links.Add(link);
			nodeB.Links.Add(link);
		}
	}
}