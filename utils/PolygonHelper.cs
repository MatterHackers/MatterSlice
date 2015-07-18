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

using MatterSlice.ClipperLib;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterSlice
{
	using Paths = List<List<IntPoint>>;

	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	// Axis aligned boundary box
	public class Aabb
	{
		public IntPoint min, max;

		public Aabb()
		{
			min = new IntPoint(long.MinValue, long.MinValue);
			max = new IntPoint(long.MinValue, long.MinValue);
		}

		public Aabb(Polygons polys)
		{
			min = new IntPoint(long.MinValue, long.MinValue);
			max = new IntPoint(long.MinValue, long.MinValue);
			Calculate(polys);
		}

		public void Calculate(Polygons polys)
		{
			min = new IntPoint(long.MaxValue, long.MaxValue);
			max = new IntPoint(long.MinValue, long.MinValue);
			for (int i = 0; i < polys.Count; i++)
			{
				for (int j = 0; j < polys[i].Count; j++)
				{
					if (min.X > polys[i][j].X) min.X = polys[i][j].X;
					if (min.Y > polys[i][j].Y) min.Y = polys[i][j].Y;
					if (max.X < polys[i][j].X) max.X = polys[i][j].X;
					if (max.Y < polys[i][j].Y) max.Y = polys[i][j].Y;
				}
			}
		}

		public bool Hit(Aabb other)
		{
			if (max.X < other.min.X) return false;
			if (min.X > other.max.X) return false;
			if (max.Y < other.min.Y) return false;
			if (min.Y > other.max.Y) return false;
			return true;
		}
	}

	internal static class PolygonHelper
	{
		public static double Area(this Polygon polygon)
		{
			return Clipper.Area(polygon);
		}

		public static IntPoint CenterOfMass(this Polygon polygon)
		{
			IntPoint center = new IntPoint();
			for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
			{
				center += polygon[positionIndex];
			}

			center /= polygon.Count;
			return center;
		}

		public static bool Inside(this Polygon polygon, IntPoint testPoint)
		{
			int positionOnPolygon = Clipper.PointInPolygon(testPoint, polygon);
			if (positionOnPolygon == 0) // not inside or on boundary
			{
				return false;
			}

			return true;
		}

		public static Polygon CreateFromString(string polygonString)
		{
			Polygon output = new Polygon();
			string[] intPointData = polygonString.Split(',');
			for (int i = 0; i < intPointData.Length - 1; i += 2)
			{
				string elementX = intPointData[i];
				string elementY = intPointData[i + 1];
				IntPoint nextIntPoint = new IntPoint(int.Parse(elementX.Substring(2)), int.Parse(elementY.Substring(3)));
				output.Add(nextIntPoint);
			}

			return output;
		}

		public static void OptimizePolygon(this Polygon polygon)
		{
			IntPoint previousPoint = polygon[polygon.Count - 1];
			for (int i = 0; i < polygon.Count; i++)
			{
				IntPoint currentPoint = polygon[i];
				if ((previousPoint - currentPoint).IsShorterThen(10))
				{
					polygon.RemoveAt(i);
					i--;
				}
				else
				{
					IntPoint nextPoint;
					if (i < polygon.Count - 1)
					{
						nextPoint = polygon[i + 1];
					}
					else
					{
						nextPoint = polygon[0];
					}

					IntPoint diff0 = (currentPoint - previousPoint).SetLength(1000000);
					IntPoint diff2 = (currentPoint - nextPoint).SetLength(1000000);

					long d = diff0.Dot(diff2);
					if (d < -999999000000)
					{
						polygon.RemoveAt(i);
						i--;
					}
					else
					{
						previousPoint = currentPoint;
					}
				}
			}
		}

		public static bool Orientation(this Polygon polygon)
		{
			return Clipper.Orientation(polygon);
		}

		public static long PolygonLength(this Polygon polygon)
		{
			long length = 0;
			IntPoint previousPoint = polygon[polygon.Count - 1];
			for (int n = 0; n < polygon.Count; n++)
			{
				IntPoint currentPoint = polygon[n];
				length += (previousPoint - currentPoint).Length();
				previousPoint = currentPoint;
			}
			return length;
		}

		public static void Reverse(this Polygon polygon)
		{
			polygon.Reverse();
		}

		public static void SaveToGCode(this Polygon polygon, string filename)
		{
			double scale = 1000;
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer segments\n");
			int extrudeAmount = 0;
			double firstX = 0;
			double firstY = 0;
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

			stream.Close();
		}

		public static string WriteToString(this Polygon polygon)
		{
			string total = "";
			foreach (IntPoint point in polygon)
			{
				total += point.ToString() + ",";
			}
			return total;
		}
	}

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

		public static List<Polygons> CreateLayerOutlines(this Polygons polygons, LayerOpperation opperation)
		{
			List<Polygons> ret = new List<Polygons>();
			Clipper clipper = new Clipper();
			PolyTree resultPolyTree = new PolyTree();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			if (opperation == LayerOpperation.UnionAll)
			{
				clipper.Execute(ClipType.ctUnion, resultPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
			}
			else
			{
				clipper.Execute(ClipType.ctUnion, resultPolyTree);
			}

			polygons._processPolyTreeNode(resultPolyTree, ret);
			return ret;
		}

		public static Polygons CreateUnion(this Polygons polygons, Polygons other)
		{
			Polygons ret = new Polygons();
			Clipper clipper = new Clipper();
			clipper.AddPaths(polygons, PolyType.ptSubject, true);
			clipper.AddPaths(other, PolyType.ptSubject, true);
			clipper.Execute(ClipType.ctUnion, ret, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
			return ret;
		}

		public static bool Inside(this Polygons polygons, IntPoint testPoint)
		{
			if (polygons.Count < 1)
			{
				return false;
			}

			// we can just test the first one first as we know that there is a special case in
			// the silcer that all the other polygons are inside this one.
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

		public static Polygons Offset(this Polygons polygons, int distance)
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
			double scale = Math.Max(size.X, size.Y) / scaleDenominator;
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

		public static string WriteToString(this Polygons polygons)
		{
			string total = "";
			foreach (Polygon polygon in polygons)
			{
				total += polygon.WriteToString() + "|";
			}
			return total;
		}

		private static void _processPolyTreeNode(this Polygons polygonsIn, PolyNode node, List<Polygons> ret)
		{
			for (int n = 0; n < node.ChildCount; n++)
			{
				PolyNode child = node.Childs[n];
				Polygons polygons = new Polygons();
				polygons.Add(child.Contour);
				for (int i = 0; i < child.ChildCount; i++)
				{
					polygons.Add(child.Childs[i].Contour);
					polygonsIn._processPolyTreeNode(child.Childs[i], ret);
				}
				ret.Add(polygons);
			}
		}
	}
}