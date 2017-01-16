﻿/*
Copyright (c) 2014, Lars Brubaker
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

using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.QuadTree
{
	public class CloseSegmentsIterator
	{
		private QuadTree<int> tree;
		private bool useQuadTree = true;

		public CloseSegmentsIterator(List<Segment> polySegments, long overlapAmount = 0, bool useQuadTree = true)
		{
			if (useQuadTree)
			{
				IntRect bounds = new IntRect();
				List<Quad> quads = new List<Quad>(polySegments.Count);
				for (int i = 0; i < polySegments.Count; i++)
				{
					var quad = new Quad(polySegments[i].Left - overlapAmount,
						polySegments[i].Bottom - overlapAmount,
						polySegments[i].Right + overlapAmount,
						polySegments[i].Top + overlapAmount);

					if (i == 0)
					{
						bounds = new IntRect(quad.MinX, quad.MinY, quad.MaxX, quad.MaxY);
					}
					else
					{
						bounds.ExpandToInclude(new IntRect(quad.MinX, quad.MinY, quad.MaxX, quad.MaxY));
					}

					quads.Add(quad);
				}

				tree = new QuadTree<int>(5, new Quad(bounds.minX, bounds.minY, bounds.maxX, bounds.maxY));
				for (int i = 0; i < quads.Count; i++)
				{
					tree.Insert(i, quads[i]);
				}
			}
		}

		public IEnumerable<int> GetTouching(int firstSegmentIndex, int endIndexExclusive)
		{
			if (useQuadTree)
			{
				foreach (var segmentIndex in tree.FindCollisions(firstSegmentIndex))
				{
					if (segmentIndex >= firstSegmentIndex)
					{
						yield return segmentIndex;
					}
				}
			}
			else
			{
				for (int i = firstSegmentIndex; i < endIndexExclusive; i++)
				{
					yield return i;
				}
			}
		}
	}

	public class PolygonEdgeIterator
	{
		private static bool newMethod = true;
		private QuadTree<int> tree;

		public PolygonEdgeIterator(List<IntPoint> sourcePoints, long overlapAmount, QuadTree<int> treeToUse = null)
		{
			this.OverlapAmount = overlapAmount;
			this.SourcePoints = sourcePoints;
			if (newMethod)
			{
				tree = treeToUse;
				if (tree == null)
				{
					tree = sourcePoints.GetEdgeQuadTree(5, overlapAmount);
				}
			}
		}

		public long OverlapAmount { get; private set; }
		public List<IntPoint> SourcePoints { get; private set; }

		public IEnumerable<int> GetTouching(Quad touchingBounds)
		{
			if (tree != null)
			{
				foreach (var index in tree.SearchArea(touchingBounds))
				{
					yield return index;
				}
			}
			else
			{
				for (int i = 0; i < SourcePoints.Count; i++)
				{
					yield return i;
				}
			}
		}
	}
}