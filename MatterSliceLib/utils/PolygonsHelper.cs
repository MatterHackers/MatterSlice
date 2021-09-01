/*
This file is part of MatterSlice. A commandline utility for
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.QuadTree;
using MSClipperLib;
using static System.Math;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public static class PolygonsHelper
	{
		public enum LayerOperation
		{
			EvenOdd,
			UnionAll
		}

		public static void AddAll(this Polygons polygons, Polygons other)
		{
			for (int n = 0; n < other.Count; n++)
			{
				polygons.Add(other[n]);
			}
		}

		public static void ApplyMatrix(this Polygons polygons, PointMatrix matrix)
		{
			for (int i = 0; i < polygons.Count; i++)
			{
				for (int j = 0; j < polygons[i].Count; j++)
				{
					polygons[i][j] = matrix.apply(polygons[i][j]);
				}
			}
		}

		public static void ApplyTranslation(this Polygons polygons, IntPoint translation)
		{
			for (int i = 0; i < polygons.Count; i++)
			{
				for (int j = 0; j < polygons[i].Count; j++)
				{
					polygons[i][j] = polygons[i][j] + translation;
				}
			}
		}

		public static Polygons ConvertToLines(this Polygons polygons, bool closedLoop, long minLengthRequired = 0)
		{
			var linePolygons = new Polygons();
			foreach (Polygon polygon in polygons)
			{
				linePolygons.AddRange(polygon.ConvertToLines(closedLoop, minLengthRequired));
			}

			return linePolygons;
		}

		public static Polygon CreateConvexHull(this Polygons polygons)
		{
			return polygons.SelectMany(s => s).ToList().CreateConvexHull();
		}

		public static Polygons CreateDifference(this Polygons polygons, Polygons other)
		{
			var ret = new Polygons();
			var clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.AddPaths(other, PolyType.ptClip, true);
			clipper.Execute(ClipType.ctDifference, ret);
			return ret;
		}

		public static Polygons CreateIntersection(this Polygons polygons, Polygons other)
		{
			var ret = new Polygons();
			var clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.AddPaths(other, PolyType.ptClip, true);
			clipper.Execute(ClipType.ctIntersection, ret);
			return ret;
		}

		public static Polygons CreateLineDifference(this Polygons areaToRemove, Polygons linePolygons)
		{
			var clipper = new Clipper();

			clipper.AddPaths(linePolygons, PolyType.ptSubject, false);
			clipper.AddPaths(areaToRemove, PolyType.ptClip, true);

			var clippedLines = new PolyTree();

			clipper.Execute(ClipType.ctDifference, clippedLines);

			return Clipper.OpenPathsFromPolyTree(clippedLines);
		}

		public static Polygons CreateLineIntersections(this Polygons areaToIntersect, Polygons lines)
		{
			var clipper = new Clipper();

			clipper.AddPaths(lines, PolyType.ptSubject, false);
			clipper.AddPaths(areaToIntersect, PolyType.ptClip, true);

			var clippedLines = new PolyTree();

			clipper.Execute(ClipType.ctIntersection, clippedLines);

			return Clipper.OpenPathsFromPolyTree(clippedLines);
		}

		public static Polygons CreateLineIntersections(this Polygons areaToIntersect, Polygon line)
		{
			var clipper = new Clipper();

			clipper.AddPath(line, PolyType.ptSubject, false);
			clipper.AddPaths(areaToIntersect, PolyType.ptClip, true);

			var clippedLines = new PolyTree();

			clipper.Execute(ClipType.ctIntersection, clippedLines);

			return Clipper.OpenPathsFromPolyTree(clippedLines);
		}

		public static Polygons CreateUnion(this Polygons polygons, Polygons other)
		{
			var clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.AddPaths(other, PolyType.ptSubject, true);

			var ret = new Polygons();
			clipper.Execute(ClipType.ctUnion, ret, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
			return ret;
		}

		public static Polygons DeepCopy(this Polygons polygons)
		{
			var deepCopy = new Polygons();
			foreach (Polygon poly in polygons)
			{
				deepCopy.Add(new Polygon(poly));
			}

			return deepCopy;
		}

		public static Polygons StitchPolygonsTogether(this Polygons polygonsToStitch)
		{
			var polysToAdd = new Polygons(polygonsToStitch);
			var stitched = new Polygons();

			while (polysToAdd.Count > 0)
			{
				var index = polysToAdd.Count - 1;
				var polygon = new Polygon(polysToAdd[index]);
				polysToAdd.RemoveAt(index);
				stitched.Add(polygon);

				bool didAdd;
				do
				{
					didAdd = false;

					// add any appropriate polygon to the end
					for (int i = polysToAdd.Count - 1; i >= 0; i--)
					{
						var start = polygon[0];
						var end = polygon[polygon.Count - 1];

						var polyToCheck = polysToAdd[i];
						var checkEndIndex = polyToCheck.Count - 1;
						// Check if this polygon can be added to the one we are building
						if (polyToCheck[0] == start || polyToCheck[checkEndIndex] == start)
						{
							if (polyToCheck[0] == start)
							{
								polyToCheck.Reverse();
							}
							polygon.InsertRange(0, polyToCheck);
							didAdd = true;
						}
						else if (polyToCheck[0] == end || polyToCheck[checkEndIndex] == end)
						{
							if (polyToCheck[checkEndIndex] == end)
							{
								polyToCheck.Reverse();
							}
							polygon.AddRange(polyToCheck);
							didAdd = true;
						}

						if (didAdd)
						{
							// remove this on and process again
							polysToAdd.RemoveAt(i);
							break;
						}
					}
				} while (didAdd);
			}

			return stitched;
		}
		
		public static Polygons GetCorrectedWinding(this Polygons polygonsToFix)
		{
			polygonsToFix = Clipper.CleanPolygons(polygonsToFix);
			var boundsPolygon = new Polygon();
			IntRect bounds = Clipper.GetBounds(polygonsToFix);
			bounds.minX -= 10;
			bounds.minY -= 10;
			bounds.maxY += 10;
			bounds.maxX += 10;

			boundsPolygon.Add(new IntPoint(bounds.minX, bounds.minY));
			boundsPolygon.Add(new IntPoint(bounds.maxX, bounds.minY));
			boundsPolygon.Add(new IntPoint(bounds.maxX, bounds.maxY));
			boundsPolygon.Add(new IntPoint(bounds.minX, bounds.maxY));

			var clipper = new Clipper();

			clipper.AddPaths(polygonsToFix, PolyType.ptSubject, true);
			clipper.AddPath(boundsPolygon, PolyType.ptClip, true);

			var intersectionResult = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, intersectionResult);

			Polygons outputPolygons = Clipper.ClosedPathsFromPolyTree(intersectionResult);

			return outputPolygons;
		}

		public static void OptimizePolygons(this Polygons polygons)
		{
			for (int n = 0; n < polygons.Count; n++)
			{
				polygons[n].OptimizePolygon();
				if (polygons[n].Count < 3)
				{
					polygons.RemoveAt(n);
					n--;
				}
			}
		}

		public static Polygons ProcessEvenOdd(this Polygons polygons)
		{
			var ret = new Polygons();
			var clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.Execute(ClipType.ctUnion, ret);
			return ret;
		}

		public static List<Polygons> ProcessIntoSeparateIslands(this Polygons polygons)
		{
			var ret = new List<Polygons>();
			var clipper = new Clipper();
			var resultPolyTree = new PolyTree();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.Execute(ClipType.ctUnion, resultPolyTree);

			polygons.ProcessPolyTreeNodeIntoSeparateIslands(resultPolyTree, ret);
			return ret;
		}

		public static void RemoveSmallAreas(this Polygons polygons, long extrusionWidth)
		{
			double areaOfExtrusion = extrusionWidth / 1000.0 * (extrusionWidth / 1000.0); // convert from microns to mm's.
			double minAreaSize = areaOfExtrusion / 2;
			for (int outlineIndex = polygons.Count - 1; outlineIndex >= 0; outlineIndex--)
			{
				double area = Math.Abs(polygons[outlineIndex].Area()) / 1000.0 / 1000.0; // convert from microns to mm's.

				// Only create an up/down Outline if the area is large enough. So you do not create tiny blobs of "trying to fill"
				if (area < minAreaSize)
				{
					polygons.RemoveAt(outlineIndex);
				}
			}
		}

		public static void SaveToGCode(this Polygons polygons, string filename)
		{
			double scale = 1000;
			var stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer segments\n");
			int extrudeAmount = 0;
			double firstX = 0;
			double firstY = 0;
			for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
			{
				Polygon polygon = polygons[polygonIndex];

				for (int intPointIndex = 0; intPointIndex < polygon.Count; intPointIndex++)
				{
					double x = (double)polygon[intPointIndex].X / scale;
					double y = (double)polygon[intPointIndex].Y / scale;
					if (intPointIndex == 0)
					{
						firstX = x;
						firstY = y;
						stream.Write("G1 X{0} Y{1}\n", x, y);
					}
					else
					{
						stream.Write("G1 X{0} Y{1} E{2}\n", x, y, ++extrudeAmount);
					}
				}

				stream.Write("G1 X{0} Y{1} E{2}\n", firstX, firstY, ++extrudeAmount);
			}

			stream.Close();
		}

		public static void SaveToSvg(this Polygons polygons, string filename)
		{
			double scaleDenominator = 150;
			IntRect bounds = Clipper.GetBounds(polygons);
			long temp = bounds.maxY;
			bounds.maxY = bounds.minY;
			bounds.minY = temp;

			var size = new IntPoint(bounds.maxX - bounds.minX, bounds.maxY - bounds.minY);
			double scale = Max(size.X, size.Y) / scaleDenominator;

			var scaledWidth = (int)Math.Abs(size.X / scale);
			scaledWidth += 10 - scaledWidth % 10;

			var scaledHeight = (int)Math.Abs(size.Y / scale);
			scaledHeight += 10 - scaledHeight % 10;

			using (var stream = new StreamWriter(filename))
			{
				stream.WriteLine(@"
<svg xmlns='http://www.w3.org/2000/svg' version='1.1'>

  <svg  x='10' y='10' width='{0}' height='{1}' overflow='visible'>
    <marker id='MidMarker' viewBox='0 0 5 5' refX='2.5' refY='2.5' markerUnits='strokeWidth' markerWidth='5' markerHeight='5' stroke='lightblue' stroke-width='.5' fill='none' orient='auto'>
      <path d='M 0 0 L 5 2.5 M 0 5 L 5 2.5'/>
    </marker>
    <defs>
      <pattern id='smallGrid' width='1' height='1' patternUnits='userSpaceOnUse'>
        <path d='M 1 0 L 0 0 0 1' fill='none' stroke='#ccc' stroke-width='0.2' />
      </pattern>
      <pattern id='grid' width='10' height='10' patternUnits='userSpaceOnUse'>
        <rect width='10' height='10' fill='url(#smallGrid)' />
        <path d='M 10 0 L 0 0 0 10' fill='none' stroke='#ccc' stroke-width='0.4' />
      </pattern>
      <marker id='arrowS' markerWidth='15' markerHeight='15' refX='1.4' refY='3' orient='auto' markerUnits='strokeWidth' viewBox='0 0 20 20'>
        <path d='M0,3 L7,6 L7,0 z' />
      </marker>
      <marker id='arrowE' markerWidth='15' markerHeight='15' refX='5.6' refY='3' orient='auto' markerUnits='strokeWidth' viewBox='0 0 20 20'>
        <path d='M0,0 L0,6 L7,3 z' />
      </marker>
    </defs>
    <rect width='100%' height='100%' fill='url(#grid)' opacity='0.5' transform='translate(0, 0)' />

    <g fill-rule='evenodd' style='fill: gray; stroke:#333; stroke-width:1'>", scaledWidth, scaledHeight);

				stream.Write("    <path marker-mid='url(#MidMarker)' d='");

				for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
				{
					Polygon polygon = polygons[polygonIndex];
					for (int intPointIndex = 0; intPointIndex < polygon.Count; intPointIndex++)
					{
						if (intPointIndex == 0)
						{
							stream.Write("M");
						}
						else
						{
							stream.Write("L");
						}

						stream.Write("{0},{1} ", (double)(polygon[intPointIndex].X - bounds.minX) / scale, (double)(polygon[intPointIndex].Y - bounds.maxY) / scale);
					}

					stream.Write("Z");
				}

				stream.WriteLine("'/>");
				stream.WriteLine("    </g>");

				// Grid markers
				for (var i = 0; i <= scaledWidth / 10; i++)
				{
					stream.WriteLine("    <text x='{0}' y='-1' style='fill: #bbb; font-size: 0.13em;'>{1}</text>", i * 10 - 1, i * 10);
				}

				for (var i = 1; i <= scaledHeight / 10; i++)
				{
					stream.WriteLine("    <text x='-4.5' y='{0}' style='fill: #bbb; font-size: 0.13em;'>{1}</text>", i * 10, i * 10);
				}

				for (int openPolygonIndex = 0; openPolygonIndex < polygons.Count; openPolygonIndex++)
				{
					Polygon openPolygon = polygons[openPolygonIndex];

					if (openPolygon.Count < 1)
					{
						continue;
					}

					stream.Write("    <polyline marker-mid='url(#MidMarker)' points='");

					for (int n = 0; n < openPolygon.Count; n++)
					{
						stream.Write("{0},{1} ", (double)(openPolygon[n].X - bounds.minX) / scale, (double)(openPolygon[n].Y - bounds.maxY) / scale);
					}

					stream.WriteLine("' style='fill: none; stroke:red; stroke-width:0.3' />");
				}

				stream.WriteLine("  </svg>");
				stream.WriteLine("</svg>");
			}
		}

		public static bool SegmentTouching(this Polygons polygons, IntPoint start, IntPoint end)
		{
			for (int polyIndex = 0; polyIndex < polygons.Count; polyIndex++)
			{
				var polyCrossings = new List<Tuple<int, IntPoint>>();
				if (polygons[polyIndex].SegmentTouching(start, end))
				{
					return true;
				}
			}

			return false;
		}

		public static int size(this Polygons polygons)
		{
			return polygons.Count;
		}

		/// <summary>
		/// Return a list of polygons of this polygon split into triangles.
		/// </summary>
		/// <param name="polygons"></param>
		/// <returns></returns>
		public static Polygons Triangulate(this Polygons polygons)
		{
			throw new NotImplementedException();
		}

		private static void ProcessPolyTreeNodeIntoSeparateIslands(this Polygons polygonsIn, PolyNode node, List<Polygons> ret)
		{
			for (int n = 0; n < node.ChildCount; n++)
			{
				PolyNode child = node.Childs[n];
				var polygons = new Polygons();
				polygons.Add(child.Contour);
				for (int i = 0; i < child.ChildCount; i++)
				{
					polygons.Add(child.Childs[i].Contour);
					polygonsIn.ProcessPolyTreeNodeIntoSeparateIslands(child.Childs[i], ret);
				}

				ret.Add(polygons);
			}
		}
	}
}