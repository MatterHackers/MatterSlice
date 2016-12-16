using System.Collections.Generic;
using MSClipperLib;
using QuadTree;

namespace MatterHackers.MatterSlice
{
	public class CloseSegmentsIterator
	{
		static bool newMethod = true;

		QuadTree<int> tree;

		public CloseSegmentsIterator(List<Segment> polySegments, long overlapAmount)
		{
			if (newMethod)
			{
				IntRect bounds = new IntRect();
				List<Quad> quads = new List<Quad>(polySegments.Count);
				for (int i = 0; i < polySegments.Count; i++)
				{
					var quad = new Quad(polySegments[i].Left - overlapAmount,
						polySegments[i].Bottom - overlapAmount,
						polySegments[i].Right + overlapAmount,
						polySegments[i].Top + overlapAmount);

					if(i==0)
					{
						bounds = new IntRect(quad.MinX, quad.MinY, quad.MaxX, quad.MaxY);
					}
					else
					{
						bounds.ExpandToInclude(new IntRect(quad.MinX, quad.MinY, quad.MaxX, quad.MaxY));
					}

					quads.Add(quad);
				}

				tree = new QuadTree<int>(5, new Quad(bounds.left, bounds.top, bounds.right, bounds.bottom));
				for (int i = 0; i < quads.Count; i++)
				{
					tree.Insert(i, quads[i]);
				}
			}
		}

		public IEnumerable<int> GetTouching(int firstSegmentIndex, int endIndexExclusive)
		{
			if (newMethod)
			{
				foreach(var segmentIndex in tree.FindCollisions(firstSegmentIndex))
				{
					yield return segmentIndex;
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

	public class ClosePointsIterator
	{
		static bool newMethod = true;
		QuadTree<int> tree;

		public List<IntPoint> SourcePoints { get; private set; }
		public long OverlapAmount { get; private set; }

		public ClosePointsIterator(List<IntPoint> sourcePoints, long overlapAmount)
		{
			this.OverlapAmount = overlapAmount;
			this.SourcePoints = sourcePoints;
			if (newMethod)
			{
				IntRect bounds = new IntRect();
				List<Quad> quads = new List<Quad>(sourcePoints.Count);
				for (int i = 0; i < sourcePoints.Count; i++)
				{
					var quad = new Quad(sourcePoints[i].X - overlapAmount,
						sourcePoints[i].Y - overlapAmount,
						sourcePoints[i].X + overlapAmount,
						sourcePoints[i].Y + overlapAmount);

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

				tree = new QuadTree<int>(5, new Quad(bounds.left, bounds.top, bounds.right, bounds.bottom));
				for (int i = 0; i < quads.Count; i++)
				{
					tree.Insert(i, quads[i]);
				}
			}
		}

		public IEnumerable<int> GetTouching(Quad touchingBounds)
		{
			if (newMethod)
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