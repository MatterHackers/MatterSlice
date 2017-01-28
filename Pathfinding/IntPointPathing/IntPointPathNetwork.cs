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
	using Datastructures;
	using Polygon = List<IntPoint>;

	public class IntPointPathNetwork : IPathNetwork<IntPointNode>
	{
		private static int allocCount = 0;

		private IPathNode pathGoal = null;
		private IPathNode pathStart = null;

		public IntPointPathNetwork()
		{
		}

		public IntPointPathNetwork(Polygon data)
		{
			AddPolygon(data);
		}

		public List<IntPointNode> Nodes { get; private set; } = new List<IntPointNode>();

		public void AddPolygon(Polygon polygon, bool closed = true, float costMultiplier = 1)
		{
			// remember what node we started with
			int startNode = Nodes.Count;
			// add all the points of the polygon
			for (int i = 0; i < polygon.Count; i++)
			{
				IntPointNode node = new IntPointNode(polygon[i]);
				node.CostMultiplier = costMultiplier;
				Nodes.Add(node);
			}

			// add all the links to the new nodes we added
			if (closed)
			{
				int lastLinkIndex = polygon.Count - 1 + startNode;
				for (int i = startNode; i < polygon.Count + startNode; i++)
				{
					AddPathLink(Nodes[lastLinkIndex], Nodes[i]);
					lastLinkIndex = i;
				}
			}
			else
			{
				for (int i = startNode + 1; i < polygon.Count + startNode; i++)
				{
					AddPathLink(Nodes[i-1], Nodes[i]);
				}
			}
		}

		public IntPointNode AddNode(IntPoint newPosition, IntPoint linkToA, IntPoint linkToB, float costMultiplier = 1)
		{
			IntPointNode nodeA = FindNode(linkToA);
			IntPointNode nodeB = FindNode(linkToB);

			if (nodeA != null && nodeB != null)
			{
				return AddNode(newPosition, nodeA, nodeB, costMultiplier);
			}

			return null;
		}

		public IntPointNode AddNode(IntPoint newPosition, IntPointNode nodeA, IntPointNode nodeB, float costMultiplier = 1)
		{
			IntPointNode newNode = AddNode(newPosition, costMultiplier);

			AddPathLink(newNode, nodeA);
			AddPathLink(newNode, nodeB);

			return newNode;
		}

		public IntPointNode AddNode(IntPoint newPosition, float costMultiplier = 1)
		{
			var newNode = new IntPointNode(newPosition);
			Nodes.Add(newNode);
			return newNode;
		}

		public PathLink AddPathLink(IntPointNode nodeA, IntPointNode nodeB)
		{
			if(nodeA == nodeB || nodeB.Position == nodeA.Position)
			{
				throw new ArgumentException();
			}
			PathLink link = nodeB.GetLinkTo(nodeA);

			if (link == null)
			{
				link = new PathLink(nodeA, nodeB);
				link.Distance = (nodeA.Position - nodeB.Position).Length();
				nodeA.Links.Add(link);
				nodeB.Links.Add(link);
			}

			return link;
		}

		public IntPointNode FindNode(IntPoint position, long minDist = 0)
		{
			foreach (var node in Nodes)
			{
				if ((node.Position - position).LengthSquared() <= minDist * minDist)
				{
					return node;
				}
			}

			return null;
		}

		public Path<IntPointNode> FindPath(IntPoint startPosition, IntPoint startLinkA, IntPoint startLinkB,
			IntPoint endPosition, IntPoint endLinkA, IntPoint endLinkB)
		{
			using (WayPointsToRemove removePointList = new WayPointsToRemove(this))
			{
				var startNode = AddNode(startPosition, startLinkA, startLinkB);
				removePointList.Add(startNode);
				var endNode = AddNode(endPosition, endLinkA, endLinkB);
				removePointList.Add(endNode);

				// if startPosition and endPosition are on the same line
				if ((startLinkA == endLinkA && startLinkB == endLinkB)
					|| (startLinkA == endLinkB && startLinkB == endLinkA))
				{
					// connect them
					AddPathLink(startNode, endNode);
				}

				var path = FindPath(startNode, endNode, true);

				return path;
			}
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

		public void Remove(IntPointNode nodeToRemove)
		{
			if (nodeToRemove != null)
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

		public void Reset()
		{
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
	}

	public class WayPointsToRemove : List<IntPointNode>, IDisposable
	{
		private IntPointPathNetwork network;

		public WayPointsToRemove(IntPointPathNetwork network)
		{
			this.network = network;
		}

		public void Dispose()
		{
			for (int i = Count - 1; i >= 0; i--)
			{
				network.Remove(this[i]);
			}
		}
	}
}