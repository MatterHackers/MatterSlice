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

using MSClipperLib;
using System.Collections.Generic;
using System.IO;
using static System.Math;

namespace MatterHackers.MatterSlice
{
	using System;
	using System.Linq;
	using Paths = List<List<IntPoint>>;

	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	internal static class PolygonsHelper
	{
		public enum LayerOpperation { EvenOdd, UnionAll };

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

		public static Polygon CreateConvexHull(this Polygons polygons)
		{
			return polygons.SelectMany(s => s).ToList().CreateConvexHull();
		}

		public static Polygons GetCorrectedWinding(this Polygons polygonsToFix)
		{
			polygonsToFix = Clipper.CleanPolygons(polygonsToFix);
			Polygon boundsPolygon = new Polygon();
			IntRect bounds = Clipper.GetBounds(polygonsToFix);
			bounds.left -= 10;
			bounds.bottom += 10;
			bounds.right += 10;
			bounds.top -= 10;

			boundsPolygon.Add(new IntPoint(bounds.left, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.bottom));
			boundsPolygon.Add(new IntPoint(bounds.left, bounds.bottom));

			Clipper clipper = new Clipper();

			clipper.AddPaths(polygonsToFix, PolyType.ptSubject, true);
			clipper.AddPath(boundsPolygon, PolyType.ptClip, true);

			PolyTree intersectionResult = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, intersectionResult);

			Polygons outputPolygons = Clipper.ClosedPathsFromPolyTree(intersectionResult);

			return outputPolygons;
		}

		public static Polygons CreateLineDifference(this Polygons linePolygons, Polygons removePolygons)
		{
			Clipper clipper = new Clipper();

			clipper.AddPaths(linePolygons, PolyType.ptSubject, false);
			clipper.AddPaths(removePolygons, PolyType.ptClip, true);

			PolyTree clippedLines = new PolyTree();

			clipper.Execute(ClipType.ctDifference, clippedLines);

			return Clipper.OpenPathsFromPolyTree(clippedLines);
		}

		public static Polygons CreateDifference(this Polygons polygons, Polygons other)
		{
			Polygons ret = new Polygons();
			Clipper clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.AddPaths(other, PolyType.ptClip, true);
			clipper.Execute(ClipType.ctDifference, ret);
			return ret;
		}

		public static Polygons CreateFromString(string polygonsPackedString)
		{
			Polygons output = new Polygons();
			string[] polygons = polygonsPackedString.Split('|');
			foreach (string polygonString in polygons)
			{
				Polygon nextPoly = PolygonHelper.CreateFromString(polygonString);
				if (nextPoly.Count > 0)
				{
					output.Add(nextPoly);
				}
			}
			return output;
		}

		public static Polygons CreateIntersection(this Polygons polygons, Polygons other)
		{
			Polygons ret = new Polygons();
			Clipper clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.AddPaths(other, PolyType.ptClip, true);
			clipper.Execute(ClipType.ctIntersection, ret);
			return ret;
		}

		public static void RemoveSmallAreas(this Polygons polygons, long extrusionWidth)
		{
			double areaOfExtrusion = (extrusionWidth / 1000.0) * (extrusionWidth / 1000.0); // convert from microns to mm's.
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

		public static Polygons CreateLineIntersections(this Polygons polygons, Polygons other)
		{
			Clipper clipper = new Clipper();

			clipper.AddPaths(other, PolyType.ptSubject, false);
			clipper.AddPaths(polygons, PolyType.ptClip, true);

			PolyTree clippedLines = new PolyTree();

			clipper.Execute(ClipType.ctIntersection, clippedLines);

			return Clipper.OpenPathsFromPolyTree(clippedLines);
		}

		public static List<Polygons> ProcessIntoSeparatIslands(this Polygons polygons)
		{
			List<Polygons> ret = new List<Polygons>();
			Clipper clipper = new Clipper();
			PolyTree resultPolyTree = new PolyTree();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.Execute(ClipType.ctUnion, resultPolyTree);

			polygons.ProcessPolyTreeNodeIntoSeparatIslands(resultPolyTree, ret);
			return ret;
		}

		public static Polygons CreateUnion(this Polygons polygons, Polygons other)
		{
			Clipper clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.AddPaths(other, PolyType.ptSubject, true);

			Polygons ret = new Polygons();
			clipper.Execute(ClipType.ctUnion, ret, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
			return ret;
		}

		public static Polygons DeepCopy(this Polygons polygons)
		{
			Polygons deepCopy = new Polygons();
			foreach (Polygon poly in polygons)
			{
				deepCopy.Add(new Polygon(poly));
			}

			return deepCopy;
		}

		public static bool Inside(this Polygons polygons, IntPoint testPoint)
		{
			if (polygons.Count < 1)
			{
				return false;
			}

			// we can just test the first one first as we know that there is a special case in
			// the slicer that all the other polygons are inside this one.
			int positionOnOuterPerimeter = Clipper.PointInPolygon(testPoint, polygons[0]);
			if (positionOnOuterPerimeter == 0) // not inside or on boundary
			{
				// If we are not inside the outer perimeter we are not inside.
				return false;
			}

			for (int polygonIndex = 1; polygonIndex < polygons.Count; polygonIndex++)
			{
				int positionOnHole = Clipper.PointInPolygon(testPoint, polygons[polygonIndex]);
				if (positionOnHole == 1) // inside the hole
				{
					return false;
				}
			}

			return true;
		}

		public static Polygons Offset(this Polygons polygons, long distance)
		{
			ClipperOffset offseter = new ClipperOffset();
			offseter.AddPaths(polygons, JoinType.jtMiter, EndType.etClosedPolygon);
			Paths solution = new Polygons();
			offseter.Execute(ref solution, distance);
			return solution;
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

		public static bool polygonCollidesWithlineSegment(Polygons polys, IntPoint transformed_startPoint, IntPoint transformed_endPoint, PointMatrix transformation_matrix)
		{
			foreach (Polygon poly in polys)
			{
				if (poly.size() == 0) { continue; }
				if (PolygonHelper.polygonCollidesWithlineSegment(poly, transformed_startPoint, transformed_endPoint, transformation_matrix))
				{
					return true;
				}
			}

			return false;
		}

		public static bool polygonCollidesWithlineSegment(Polygons polys, IntPoint startPoint, IntPoint endPoint)
		{
			IntPoint diff = endPoint - startPoint;

			PointMatrix transformation_matrix = new PointMatrix(diff);
			IntPoint transformed_startPoint = transformation_matrix.apply(startPoint);
			IntPoint transformed_endPoint = transformation_matrix.apply(endPoint);

			return polygonCollidesWithlineSegment(polys, transformed_startPoint, transformed_endPoint, transformation_matrix);
		}

		public static long PolygonLength(this Polygons polygons)
		{
			long length = 0;
			for (int i = 0; i < polygons.Count; i++)
			{
				IntPoint previousPoint = polygons[i][polygons[i].Count - 1];
				for (int n = 0; n < polygons[i].Count; n++)
				{
					IntPoint currentPoint = polygons[i][n];
					length += (previousPoint - currentPoint).Length();
					previousPoint = currentPoint;
				}
			}
			return length;
		}

		public static Polygons ProcessEvenOdd(this Polygons polygons)
		{
			Polygons ret = new Polygons();
			Clipper clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.Execute(ClipType.ctUnion, ret);
			return ret;
		}

		public static void SaveToGCode(this Polygons polygons, string filename)
		{
			double scale = 1000;
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer segments\n");
			int extrudeAmount = 0;
			double firstX = 0;
			double firstY = 0;
			for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
			{
				Polygon polygon = polygons[polygonIndex];

				for (int intPointIndex = 0; intPointIndex < polygon.Count; intPointIndex++)
				{
					double x = (double)(polygon[intPointIndex].X) / scale;
					double y = (double)(polygon[intPointIndex].Y) / scale;
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
			long temp = bounds.bottom;
			bounds.bottom = bounds.top;
			bounds.top = temp;
			IntPoint size = new IntPoint(bounds.right - bounds.left, bounds.top - bounds.bottom);
			double scale = Max(size.X, size.Y) / scaleDenominator;
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("<!DOCTYPE html><html><body>\n");
			stream.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" style='width:{0}px;height:{1}px'>\n".FormatWith((int)(size.X / scale), (int)(size.Y / scale)));
			stream.Write("<marker id='MidMarker' viewBox='0 0 10 10' refX='5' refY='5' markerUnits='strokeWidth' markerWidth='10' markerHeight='10' stroke='lightblue' stroke-width='2' fill='none' orient='auto'>");
			stream.Write("<path d='M 0 0 L 10 5 M 0 10 L 10 5'/>");
			stream.Write("</marker>");
			stream.Write("<g fill-rule='evenodd' style=\"fill: gray; stroke:black;stroke-width:1\">\n");
			stream.Write("<path marker-mid='url(#MidMarker)' d=\"");
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
					stream.Write("{0},{1} ", (double)(polygon[intPointIndex].X - bounds.left) / scale, (double)(polygon[intPointIndex].Y - bounds.bottom) / scale);
				}
				stream.Write("Z\n");
			}
			stream.Write("\"/>");
			stream.Write("</g>\n");
			for (int openPolygonIndex = 0; openPolygonIndex < polygons.Count; openPolygonIndex++)
			{
				Polygon openPolygon = polygons[openPolygonIndex];
				if (openPolygon.Count < 1) continue;
				stream.Write("<polyline marker-mid='url(#MidMarker)' points=\"");
				for (int n = 0; n < openPolygon.Count; n++)
				{
					stream.Write("{0},{1} ", (double)(openPolygon[n].X - bounds.left) / scale, (double)(openPolygon[n].Y - bounds.bottom) / scale);
				}
				stream.Write("\" style=\"fill: none; stroke:red;stroke-width:1\" />\n");
			}
			stream.Write("</svg>\n");

			stream.Write("</body></html>");
			stream.Close();
		}

		public static int size(this Polygons polygons)
		{
			return polygons.Count;
		}

		public static string WriteToString(this Polygons polygons)
		{
			string total = "";
			foreach (Polygon polygon in polygons)
			{
				total += polygon.WriteToString() + "|";
			}
			return total;
		}

		private static void ProcessPolyTreeNodeIntoSeparatIslands(this Polygons polygonsIn, PolyNode node, List<Polygons> ret)
		{
			for (int n = 0; n < node.ChildCount; n++)
			{
				PolyNode child = node.Childs[n];
				Polygons polygons = new Polygons();
				polygons.Add(child.Contour);
				for (int i = 0; i < child.ChildCount; i++)
				{
					polygons.Add(child.Childs[i].Contour);
					polygonsIn.ProcessPolyTreeNodeIntoSeparatIslands(child.Childs[i], ret);
				}
				ret.Add(polygons);
			}
		}

		public static Polygons ConvertToLines(Polygons polygons)
		{
			Polygons linePolygons = new Polygons();
			foreach(Polygon polygon in polygons)
			{
				if (polygon.Count > 2)
				{
					for (int vertexIndex = 0; vertexIndex < polygon.Count; vertexIndex++)
					{
						linePolygons.Add(new Polygon() { polygon[vertexIndex], polygon[(vertexIndex + 1) % polygon.Count] });
					}
				}
				else
				{
					linePolygons.Add(polygon);
				}
			}

			return linePolygons;
		}
	}
}