/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
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
        public static void GenerateLinePaths(Polygons in_outline, Polygons result, int extrusionWidth_um, int lineSpacing, int infillExtendIntoPerimeter_um, double rotation, long rotationOffset = 0)
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
                    int lineCount = (int)((boundary.max.X - boundary.min.X + (lineSpacing - 1)) / lineSpacing);
                    Polygons unclipedPatern = new Polygons();
                    long firstX = boundary.min.X / lineSpacing * lineSpacing;
                    for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
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

        public static void GenerateLineInfill(ConfigSettings config, SliceLayerPart part, Polygons fillPolygons, int extrusionWidth_um, double fillAngle)
        {
            if (config.infillPercent <= 0)
            {
                throw new Exception("infillPercent must be gerater than 0.");
            }

            int linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100));
            GenerateLinePaths(part.sparseOutline, fillPolygons, extrusionWidth_um, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle);
        }

        public static void GenerateGridInfill(ConfigSettings config, SliceLayerPart part, Polygons fillPolygons, int extrusionWidth_um, double fillAngle)
        {
            if (config.infillPercent <= 0)
            {
                throw new Exception("infillPercent must be gerater than 0.");
            }

            int linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100) * 2);

            Infill.GenerateLinePaths(part.sparseOutline, fillPolygons, config.extrusionWidth_um, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle);

            fillAngle += 90;
            if (fillAngle > 360)
            {
                fillAngle -= 360;
            }

            Infill.GenerateLinePaths(part.sparseOutline, fillPolygons, extrusionWidth_um, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle);
        }

        public static void GenerateTriangleInfill(ConfigSettings config, SliceLayerPart part, Polygons fillPolygons, int extrusionWidth_um, double fillAngle, long printZ)
        {
            if (config.infillPercent <= 0)
            {
                throw new Exception("infillPercent must be gerater than 0.");
            }

            int linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100) * 3);

            //long offset = printZ % linespacing_um;
            long offset = linespacing_um / 2;

            Infill.GenerateLinePaths(part.sparseOutline, fillPolygons, config.extrusionWidth_um, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle, offset);

            fillAngle += 60;
            if (fillAngle > 360)
            {
                fillAngle -= 360;
            }

            Infill.GenerateLinePaths(part.sparseOutline, fillPolygons, extrusionWidth_um, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle, offset);

            fillAngle += 60;
            if (fillAngle > 360)
            {
                fillAngle -= 360;
            }

            Infill.GenerateLinePaths(part.sparseOutline, fillPolygons, extrusionWidth_um, linespacing_um, config.infillExtendIntoPerimeter_um, fillAngle, offset);
        }

        public static void generateConcentricInfill(ConfigSettings config, SliceLayerPart part, Polygons fillPolygons, int extrusionWidth_um, double fillAngle)
        {
            int linespacing_um = (int)(config.extrusionWidth_um / (config.infillPercent / 100));
            while (part.sparseOutline.Count > 0)
            {
                for (int outlineIndex = 0; outlineIndex < part.sparseOutline.Count; outlineIndex++)
                {
                    Polygon r = part.sparseOutline[outlineIndex];
                    fillPolygons.Add(r);
                }
                part.sparseOutline = part.sparseOutline.Offset(-linespacing_um);
            }
        }
    }
}