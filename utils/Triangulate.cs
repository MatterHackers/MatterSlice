using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MSClipperLib;
namespace MatterHackers.MatterSlice.utils
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public static class Triangulate
	{
		public static Polygons Do(this Polygons polygons)
		{
			//CachedTesselator teselatedSource = new CachedTesselator();
			throw new NotImplementedException();
		}
	}
}
