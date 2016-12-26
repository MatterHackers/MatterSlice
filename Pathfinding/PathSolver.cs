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
using Pathfinding.Datastructures;
using System.Threading;

namespace Pathfinding
{
    public class PathSolver<PathNodeType> where PathNodeType : IPathNode
    {
        private void TryQueueNewTile(IPathNode pNewNode, PathLink pLink, AStarStack pNodesToVisit, IPathNode pGoal)
        {
            IPathNode previousNode = pLink.GetOtherNode(pNewNode);
            float linkDistance = pLink.Distance;
            float newPathCost = previousNode.PathCostHere + pNewNode.BaseCost + linkDistance;
            
            if (pNewNode.LinkLeadingHere == null || (pNewNode.PathCostHere > newPathCost)) {
                pNewNode.DistanceToGoal = pNewNode.DistanceTo(pGoal) * 2f;
                pNewNode.PathCostHere = newPathCost;
                pNewNode.LinkLeadingHere = pLink;
                pNodesToVisit.Push(pNewNode);
            }
        }
	
        public Path<PathNodeType> FindPath(IPathNode pStart, IPathNode pGoal, IPathNetwork<PathNodeType> pNetwork, bool pReset)
        {
#if DEBUG
			if(pNetwork == null) {
				throw new Exception("pNetwork is null");
			}
#endif
			if (pStart == null || pGoal == null) {
				return new Path<PathNodeType>(new PathNodeType[] {}, 0f, PathStatus.DESTINATION_UNREACHABLE, 0);
			}

			if (pStart == pGoal) {
				return new Path<PathNodeType>(new PathNodeType[] {}, 0f, PathStatus.ALREADY_THERE, 0);
			}

            int testCount = 0;
			
			if(pReset) {
            	pNetwork.Reset();
			}
			
            pStart.IsStartNode = true;
            pGoal.IsGoalNode = true;
            List<PathNodeType> resultNodeList = new List<PathNodeType>();
            
            IPathNode currentNode = pStart;
            IPathNode goalNode = pGoal;
            
            currentNode.Visited = true;
            currentNode.LinkLeadingHere = null;
            AStarStack nodesToVisit = new AStarStack();
            PathStatus pathResult = PathStatus.NOT_CALCULATED_YET;
            testCount = 1;
            
            while (pathResult == PathStatus.NOT_CALCULATED_YET) {
                foreach (PathLink l in currentNode.Links) {
                    IPathNode otherNode = l.GetOtherNode(currentNode);
                    
                    if (!otherNode.Visited) {
                        TryQueueNewTile(otherNode, l, nodesToVisit, goalNode);
                    }
                }
                
                if (nodesToVisit.Count == 0) {
                    pathResult = PathStatus.DESTINATION_UNREACHABLE;
                }
                else {
                    currentNode = nodesToVisit.Pop();
                    testCount++;

                    // Console.WriteLine("testing new node: " + (currentNode as TileNode).localPoint);
                    currentNode.Visited = true;
                    
                    if (currentNode == goalNode) {
                        pathResult = PathStatus.FOUND_GOAL;
                    }
                }
            }
            
            // Path finished, collect
            float tLength = 0;

            if (pathResult == PathStatus.FOUND_GOAL) {
                tLength = currentNode.PathCostHere;
                
                while (currentNode != pStart) {
                    resultNodeList.Add((PathNodeType)currentNode);
                    currentNode = currentNode.LinkLeadingHere.GetOtherNode(currentNode);
                }
                
                resultNodeList.Add((PathNodeType)currentNode);
                resultNodeList.Reverse();
            }
            
            return new Path<PathNodeType>(resultNodeList.ToArray(), tLength, pathResult, testCount);
        }
		
		/*
        public delegate void PathWasFound(Path<PathNodeType> newPath);

        public void FindPathAsync(IPathNode pStart, IPathNode pGoal, IPathNetwork<PathNodeType> pNetwork, PathWasFound pOnPathWasFound, bool pReset)
        {
           	//ThreadPool.QueueUserWorkItem(o => {
                Path<PathNodeType> path = FindPath(pStart, pGoal, pNetwork, pReset);
				pOnPathWasFound(path);
           	//});
        }
        */
    }
}
