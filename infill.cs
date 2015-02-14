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
using System.Diagnostics;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class Infill
    {
        public static void GenerateLinePaths(Polygons in_outline, ref Polygons result, int lineSpacing, int infillExtendIntoPerimeter_um, double rotation, long rotationOffset = 0)
        {
            if (in_outline.Count > 0)
            {
                Polygons outlines = in_outline.Offset(infillExtendIntoPerimeter_um);
                if (outlines.Count > 0)
                {
                    PointMatrix matrix = new PointMatrix(-(rotation + 90)); // we are rotating the part so we rotate by the negative so the lines go the way we expect

                    outlines.applyMatrix(matrix);

                    AABB boundary = new AABB(outlines);

                    boundary.min.X = ((boundary.min.X / lineSpacing) - 1) * lineSpacing - rotationOffset;
                    int xLineCount = (int)((boundary.max.X - boundary.min.X + (lineSpacing - 1)) / lineSpacing);
                    Polygons unclipedPatern = new Polygons();

					long firstX = boundary.min.X / lineSpacing * lineSpacing;
                    for (int lineIndex = 0; lineIndex < xLineCount; lineIndex++)
                    {
                        Polygon line = new Polygon();
                        line.Add(new IntPoint(firstX + lineIndex * lineSpacing, boundary.min.Y));
                        line.Add(new IntPoint(firstX + lineIndex * lineSpacing, boundary.max.Y));
                        unclipedPatern.Add(line);
                    }

                    PolyTree ret = new PolyTree();
                    Clipper clipper = new Clipper();
                    clipper.AddPaths(unclipedPatern, PolyType.ptSubject, false);
                    clipper.AddPaths(outlines, PolyType.ptClip, true);
                    clipper.Execute(ClipType.ctIntersection, ret, PolyFillType.pftPositive, PolyFillType.pftEvenOdd);

                    Polygons newSegments = Clipper.OpenPathsFromPolyTree(ret);
                    PointMatrix inversematrix = new PointMatrix((rotation + 90));
                    newSegments.applyMatrix(inversematrix);
                    
                    result.AddRange(newSegments);
                }
            }
        }

		public static void GenerateHexLinePaths(Polygons in_outline, ref Polygons result, int lineSpacing, int infillExtendIntoPerimeter_um, double rotationDegrees, int layerIndex)
		{
			int extraRotationAngle = 0;
			if (in_outline.Count > 0)
			{
				Polygons outlines = in_outline.Offset(infillExtendIntoPerimeter_um);
				if (outlines.Count > 0)
				{
					int perIncrementOffset = (int)(lineSpacing * Math.Sqrt(3) / 2 + .5);
					PointMatrix matrix = new PointMatrix(-(rotationDegrees + extraRotationAngle)); // we are rotating the part so we rotate by the negative so the lines go the way we expect

					outlines.applyMatrix(matrix);

					AABB boundary = new AABB(outlines);

					boundary.min.X = ((boundary.min.X / lineSpacing) - 1) * lineSpacing;
					boundary.min.Y = ((boundary.min.Y / perIncrementOffset) - 1) * perIncrementOffset;
					int xLineCount = (int)((boundary.max.X - boundary.min.X + (lineSpacing - 1)) / lineSpacing) + 1;
					Polygons unclipedPatern = new Polygons();

					//foreach(IntPoint startPoint in StartPositionIterator(boundary, lineSpacing, layerIndex);

					int yLineCount = (int)((boundary.max.Y - boundary.min.Y + (perIncrementOffset - 1)) / perIncrementOffset) + 1;
					long firstY = boundary.min.Y;
					for (int yIndex = 2; yIndex < yLineCount-2; yIndex++)
					{
						Polygon attachedLine = new Polygon();
						long xOffsetForY = lineSpacing / 2;
						if ((yIndex % 2) == 0) // if we are at every other y
						{
							xOffsetForY = 0;
						}
						long firstX = boundary.min.X + xOffsetForY;
						for (int xIndex = 2; xIndex < xLineCount-2; xIndex++)
						{
							// what we are adding are the little plusses that define the points
							//    |
							//    |
							//    /\   
							//   /  \
							//    
							IntPoint left = new IntPoint(firstX + xIndex * lineSpacing, firstY + yIndex * perIncrementOffset);
							IntPoint right = new IntPoint(firstX + (xIndex + 1) * lineSpacing, firstY + yIndex * perIncrementOffset);
							IntPoint top = new IntPoint((left.X + right.X) / 2, firstY + (yIndex + 1) * perIncrementOffset);
							IntPoint center = (left + right + top) / 3;

							attachedLine.Add(left); attachedLine.Add(center);
							attachedLine.Add(center); attachedLine.Add(right);
							unclipedPatern.Add(new Polygon() { top, center });

						}
						unclipedPatern.Add(attachedLine);
					}

					PolyTree ret = new PolyTree();
					Clipper clipper = new Clipper();
					clipper.AddPaths(unclipedPatern, PolyType.ptSubject, false);
					clipper.AddPaths(outlines, PolyType.ptClip, true);
					clipper.Execute(ClipType.ctIntersection, ret, PolyFillType.pftPositive, PolyFillType.pftEvenOdd);

					Polygons newSegments = Clipper.OpenPathsFromPolyTree(ret);
					PointMatrix inversematrix = new PointMatrix((rotationDegrees + extraRotationAngle));
					newSegments.applyMatrix(inversematrix);

					result.AddRange(newSegments);
				}
			}
		}
		
		public static void GenerateLineInfill(ConfigSettings config, Polygons partOutline, ref Polygons fillPolygons, int extrusionWidth_um, double fillAngle, int linespacing_um = 0)
        {
            if (linespacing_um == 0)
            {
                if (config.infillPercent <= 0)
                {
                    throw new Exception("infillPercent must be gerater than 0.");
                }

                linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100));
            }
            GenerateLinePaths(partOutline, ref fillPolygons, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle);
        }

        public static void GenerateGridInfill(ConfigSettings config, Polygons partOutline, ref Polygons fillPolygons, int extrusionWidth_um, double fillAngle, int linespacing_um = 0)
        {
            if (linespacing_um == 0)
            {
				if (config.infillPercent <= 0)
				{
					throw new Exception("infillPercent must be gerater than 0.");
				}
				linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100) * 2);
            }

            Infill.GenerateLinePaths(partOutline, ref fillPolygons, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle);

            fillAngle += 90;
            if (fillAngle > 360)
            {
                fillAngle -= 360;
            }

            Infill.GenerateLinePaths(partOutline, ref fillPolygons, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle);
        }

		static IntPoint hexOffset = new IntPoint(0, 0);
		public static void GenerateHexagonInfill(ConfigSettings config, Polygons partOutline, ref Polygons fillPolygons, int extrusionWidth_um, double fillAngle, int layerIndex)
		{
			if (config.infillPercent <= 0)
			{
				throw new Exception("infillPercent must be gerater than 0.");
			}

			int linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100) * 3);

			Infill.GenerateHexLinePaths(partOutline, ref fillPolygons, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle, layerIndex);
		}

        public static void GenerateTriangleInfill(ConfigSettings config, Polygons partOutline, ref Polygons fillPolygons, int extrusionWidth_um, double fillAngle)
        {
            if (config.infillPercent <= 0)
            {
                throw new Exception("infillPercent must be gerater than 0.");
            }

            int linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100) * 3);

            long offset = linespacing_um / 2;

            Infill.GenerateLinePaths(partOutline, ref fillPolygons, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle, offset);

            fillAngle += 60;
            if (fillAngle > 360)
            {
                fillAngle -= 360;
            }

            Infill.GenerateLinePaths(partOutline, ref fillPolygons, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle, offset);

            fillAngle += 60;
            if (fillAngle > 360)
            {
                fillAngle -= 360;
            }

            Infill.GenerateLinePaths(partOutline, ref fillPolygons, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle, offset);
        }

        public static void generateConcentricInfill(ConfigSettings config, Polygons partOutline, ref Polygons fillPolygons, int extrusionWidth_um, double fillAngle)
        {
            Polygons outlineCopy = new Polygons(partOutline);
            int linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100));
            while (outlineCopy.Count > 0)
            {
                for (int outlineIndex = 0; outlineIndex < outlineCopy.Count; outlineIndex++)
                {
                    Polygon r = outlineCopy[outlineIndex];
                    fillPolygons.Add(r);
                }
                outlineCopy = outlineCopy.Offset(-linespacing_um);
            }
        }
    }
}