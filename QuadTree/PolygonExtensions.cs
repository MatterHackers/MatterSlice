using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MSClipperLib;

namespace MatterHackers.QuadTree
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public static class PolygonExtensions
	{
		public static QuadTree<int> GetEdgeQuadTree(this Polygon polygon, int splitCount = 5, long expandDist = 1)
		{
			var bounds = polygon.GetBounds();
			bounds.Inflate(expandDist);
			var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.maxY, bounds.maxX, bounds.minY);
			for (int i = 0; i < polygon.Count; i++)
			{
				var currentPoint = polygon[i];
				var nextPoint = polygon[i == polygon.Count - 1 ? 0 : i + 1];
				quadTree.Insert(i, new Quad(Math.Min(nextPoint.X, currentPoint.X) - expandDist,
					Math.Min(nextPoint.Y, currentPoint.Y) - expandDist,
					Math.Max(nextPoint.X, currentPoint.X) + expandDist,
					Math.Max(nextPoint.Y, currentPoint.Y) + expandDist));
			}

			return quadTree;
		}

		public static QuadTree<int> GetPointQuadTree(this Polygon polygon, int splitCount = 5, long expandDist = 1)
		{
			var bounds = polygon.GetBounds();
			bounds.Inflate(expandDist);
			var quadTree = new QuadTree<int>(splitCount, bounds.minX, bounds.maxY, bounds.maxX, bounds.minY);
			for (int i = 0; i < polygon.Count; i++)
			{
				quadTree.Insert(i, polygon[i].X - expandDist, polygon[i].Y - expandDist, polygon[i].X + expandDist, polygon[i].Y + expandDist);
			}

			return quadTree;
		}
	}
}
