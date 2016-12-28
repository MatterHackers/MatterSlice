using System;
using System.Collections.Generic;
using MSClipperLib;

namespace Pathfinding
{
	using Datastructures;
	using MatterHackers.MatterSlice;
	using Polygon = List<IntPoint>;

	public class IntPointPathNetwork : IPathNetwork<IntPointNode>
	{
		private static int allocCount = 0;

		private List<IntPointNode> Nodes = new List<IntPointNode>();
		private IPathNode pathGoal = null;
		private IPathNode pathStart = null;

		public IntPointPathNetwork(Polygon data)
		{
			for (int i = 0; i < data.Count; i++)
			{
				IntPointNode node = new IntPointNode(data[i]);
				node.CostMultiplier = 1;
				Nodes.Add(node);
			}

			int lastLinkIndex = data.Count - 1;
			for (int i = 0; i < data.Count; i++)
			{
				AddPathLink(Nodes[lastLinkIndex], Nodes[i]);
				lastLinkIndex = i;
			}
		}

		public IntPointNode InsertNode(IntPoint newPosition, IntPoint linkToA, IntPoint linkToB)
		{
			IntPointNode nodeA = FindNode(linkToA);
			IntPointNode nodeB = FindNode(linkToB);

			if(nodeA != null && nodeB != null)
			{
				return InsertNode(newPosition, nodeA, nodeB);
			}

			return null;
		}
		
		public IntPointNode InsertNode(IntPoint newPosition, IntPointNode nodeA, IntPointNode nodeB)
		{
			var newNode = new IntPointNode(newPosition);
			AddPathLink(newNode, nodeA);
			AddPathLink(newNode, nodeB);

			return newNode;
		}

		public IntPointNode FindNode(IntPoint position)
		{
			foreach(var node in Nodes)
			{
				if(node.Position == position)
				{
					return node;
				}
			}

			return null;
		}

		public Path<IntPointNode> FindPath(IntPoint startPosition, IntPoint startLinkA, IntPoint startLinkB, 
			IntPoint endPosition, IntPoint endLinkA, IntPoint endLinkB)
		{
			var startNode = InsertNode(startPosition, startLinkA, startLinkB);
			var endNode = InsertNode(endPosition, endLinkA, endLinkB);

			// if startPosition and endPosition are on the same line
			if ((startLinkA == endLinkA && startLinkB == endLinkB)
				|| (startLinkA == endLinkB && startLinkB == endLinkA))
			{
				// connect them
				AddPathLink(startNode, endNode);
			}

			var path = FindPath(startNode, endNode, true);

			Remove(startNode);
			Remove(endNode);

			return path;
		}

		public Path<IntPointNode> FindPath(IPathNode start, IPathNode goal, bool reset)
		{
			if (start == null || goal == null)
			{
				return new Path<IntPointNode>(new IntPointNode[] { }, 0f, PathStatus.DESTINATION_UNREACHABLE, 0);
			}

			if (start == goal)
			{
				return new Path<IntPointNode>(new IntPointNode[] { }, 0f, PathStatus.ALREADY_THERE, 0);
			}

			int testCount = 0;

			if (reset)
			{
				Reset();
			}

			start.IsStartNode = true;
			goal.IsGoalNode = true;
			List<IntPointNode> resultNodeList = new List<IntPointNode>();

			IPathNode currentNode = start;
			IPathNode goalNode = goal;

			currentNode.Visited = true;
			currentNode.LinkLeadingHere = null;
			AStarStack nodesToVisit = new AStarStack();
			PathStatus pathResult = PathStatus.NOT_CALCULATED_YET;
			testCount = 1;

			while (pathResult == PathStatus.NOT_CALCULATED_YET)
			{
				foreach (PathLink l in currentNode.Links)
				{
					IPathNode otherNode = l.GetOtherNode(currentNode);

					if (!otherNode.Visited)
					{
						TryQueueNewNode(otherNode, l, nodesToVisit, goalNode);
					}
				}

				if (nodesToVisit.Count == 0)
				{
					pathResult = PathStatus.DESTINATION_UNREACHABLE;
				}
				else
				{
					currentNode = nodesToVisit.Pop();
					testCount++;

					// Console.WriteLine("testing new node: " + (currentNode as TileNode).localPoint);
					currentNode.Visited = true;

					if (currentNode == goalNode)
					{
						pathResult = PathStatus.FOUND_GOAL;
					}
				}
			}

			// Path finished, collect
			float tLength = 0;

			if (pathResult == PathStatus.FOUND_GOAL)
			{
				tLength = currentNode.PathCostHere;

				while (currentNode != start)
				{
					resultNodeList.Add((IntPointNode)currentNode);
					currentNode = currentNode.LinkLeadingHere.GetOtherNode(currentNode);
				}

				resultNodeList.Add((IntPointNode)currentNode);
				resultNodeList.Reverse();
			}

			return new Path<IntPointNode>(resultNodeList.ToArray(), tLength, pathResult, testCount);
		}

		public void Reset()
		{
			Console.WriteLine("Reset " + Nodes.Count + " nodes");
			foreach (IPathNode node in Nodes)
			{
				node.IsGoalNode = false;
				node.IsStartNode = false;
				node.DistanceToGoal = 0f;
				node.PathCostHere = 0f;
				node.Visited = false;
				node.LinkLeadingHere = null;
			}
		}

		public void SetGoal(IPoint pPosition)
		{
			pathGoal = Nodes[pPosition.GetHashCode()];
			pathGoal.IsGoalNode = true;
		}

		public void SetStart(IPoint pPosition)
		{
			pathStart = Nodes[pPosition.GetHashCode()];
			pathStart.IsStartNode = true;
		}

		private static void IncAlloc()
		{
			allocCount++;
			Console.WriteLine("Alloc count: " + allocCount);
		}

		private static void TryQueueNewNode(IPathNode pNewNode, PathLink pLink, AStarStack pNodesToVisit, IPathNode pGoal)
		{
			IPathNode previousNode = pLink.GetOtherNode(pNewNode);
			float linkDistance = pLink.Distance;
			float newPathCost = previousNode.PathCostHere + pNewNode.CostMultiplier * linkDistance;

			if (pNewNode.LinkLeadingHere == null || (pNewNode.PathCostHere > newPathCost))
			{
				pNewNode.DistanceToGoal = pNewNode.DistanceTo(pGoal) * 2f;
				pNewNode.PathCostHere = newPathCost;
				pNewNode.LinkLeadingHere = pLink;
				pNodesToVisit.Push(pNewNode);
			}
		}

		private PathLink AddPathLink(IntPointNode nodeA, IntPointNode nodeB)
		{
			PathLink link = nodeB.GetLinkTo(nodeA);

			if (link == null)
			{
				link = new PathLink(nodeA, nodeB);
			}

			link.Distance = (nodeA.Position - nodeB.Position).Length();
			nodeA.Links.Add(link);
			nodeB.Links.Add(link);

			return link;
		}

		private void Remove(IntPointNode nodeToRemove)
		{
			for (int i = nodeToRemove.Links.Count - 1; i >= 0; i--)
			{
				var link = nodeToRemove.Links[i];
				var otherNode = link.nodeA == nodeToRemove ? link.nodeB : link.nodeA;
				nodeToRemove.Links.Remove(link);
				otherNode.Links.Remove(link);
			}
			Nodes.Remove(nodeToRemove);
		}
	}
}