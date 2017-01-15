using System;
using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.Pathfinding
{
	public class IntPointDirectionSorter : IComparer<Tuple<int, IntPoint>>
	{
		private IntPoint direction;
		private long length;
		private IntPoint start;

		public IntPointDirectionSorter(IntPoint start, IntPoint end)
		{
			this.start = start;
			this.direction = end - start;
			length = direction.Length();
		}

		public int Compare(Tuple<int, IntPoint> a, Tuple<int, IntPoint> b)
		{
			if (length > 0)
			{
				long distToA = direction.Dot(a.Item2 - start) / length;
				long distToB = direction.Dot(b.Item2 - start) / length;

				return distToA.CompareTo(distToB);
			}

			return 0;
		}
	}

	public class PolygonAndPointDirectionSorter : IComparer<Tuple<int, int, IntPoint>>
	{
		private IntPoint direction;
		private long length;
		private IntPoint start;

		public PolygonAndPointDirectionSorter(IntPoint start, IntPoint end)
		{
			this.start = start;
			this.direction = end - start;
			length = direction.Length();
		}

		public int Compare(Tuple<int, int, IntPoint> a, Tuple<int, int, IntPoint> b)
		{
			if (length > 0)
			{
				long distToA = direction.Dot(a.Item3 - start) / length;
				long distToB = direction.Dot(b.Item3 - start) / length;

				return distToA.CompareTo(distToB);
			}

			return 0;
		}
	}
}