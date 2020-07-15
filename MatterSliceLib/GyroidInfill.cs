using MatterHackers.QuadTree;
using MSClipperLib;
using System;
using System.Collections.Generic;
using System.Text;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterSliceLib
{
    public class GyroidInfill
    {
#if false
        Polygon outline = new Polygon();

        public void generateTotalGyroidInfill(Polygons result_lines,
            bool zig_zaggify,
            long outline_offset,
            long infill_line_width,
            long line_distance,
            Polygon in_outline,
            long z)
        {
            // generate infill based on the gyroid equation: sin_x * cos_y + sin_y * cos_z + sin_z * cos_x = 0
            // kudos to the author of the Slic3r implementation equation code, the equation code here is based on that
            Polygon outline = in_outline.Offset(outline_offset);
            var aabb = outline.GetBounds();

            int pitch = (int)(line_distance * 2.41); // this produces similar density to the "line" infill pattern
            int num_steps = 4;
            int step = pitch / num_steps;
            while (step > 500 && num_steps < 16)
            {
                num_steps *= 2;
                step = pitch / num_steps;
            }

            pitch = step * num_steps; // recalculate to avoid precision errors
            double z_rads = 2 * Math.PI * z / pitch;
            double cos_z = Math.Cos(z_rads);
            double sin_z = Math.Sin(z_rads);
            List<double> odd_line_coords = new List<double>();
            List<double> even_line_coords = new List<double>();
            Polygons result = new Polygons();
            List<IntPoint> chains = new List<IntPoint>(2); // [start_points[], end_points[]]
            List<int> connected_to = new List<int>(); // [chain_indices[], chain_indices[]]
            List<int> line_numbers = new List<int>(); // which row/column line a chain is part of
            if (Math.Abs(sin_z) <= Math.Abs(cos_z))
            {
                // "vertical" lines
                double phase_offset = ((cos_z < 0) ? Math.PI : 0) + Math.PI;
                for (long y = 0; y < pitch; y += step)
                {
                    double y_rads = 2 * Math.PI * y / pitch;
                    double a = cos_z;
                    double b = Math.Sin(y_rads + phase_offset);
                    double odd_c = sin_z * Math.Cos(y_rads + phase_offset);
                    double even_c = sin_z * Math.Cos(y_rads + phase_offset + Math.PI);
                    double h = Math.Sqrt(a * a + b * b);
                    double odd_x_rads = ((h != 0) ? Math.Asin(odd_c / h) + Math.Asin(b / h) : 0) - Math.PI / 2;
                    double even_x_rads = ((h != 0) ? Math.Asin(even_c / h) + Math.Asin(b / h) : 0) - Math.PI / 2;
                    odd_line_coords.Add(odd_x_rads / Math.PI * pitch);
                    even_line_coords.Add(even_x_rads / Math.PI * pitch);
                }

                int num_coords = odd_line_coords.Count;
                int num_columns = 0;
                for (long x = (long)((Math.Floor(aabb.minX / (double)pitch) - 2.25) * pitch); x <= aabb.maxX + pitch / 2; x += pitch / 2)
                {
                    bool is_first_point = true;
                    IntPoint last;
                    bool last_inside = false;
                    int chain_end_index = 0;
                    List<IntPoint> chain_end = new List<IntPoint>();
                    for (long y = (long)((Math.Floor(aabb.minY / (double)pitch) - 1) * pitch); y <= aabb.maxY + pitch; y += pitch)
                    {
                        for (int i = 0; i < num_coords; ++i)
                        {
                            IntPoint current = new IntPoint(x + (((num_columns & 1) == 1) ? odd_line_coords[i] : even_line_coords[i]) / 2 + pitch, y + (long)(i * step));
                            bool current_inside = outline.PointIsInside(current) == 1;
                            if (!is_first_point)
                            {
                                if (last_inside && current_inside)
                                {
                                    // line doesn't hit the boundary, add the whole line
                                    result.addLine(last, current);
                                }
                                else if (last_inside != current_inside)
                                {
                                    // line hits the boundary, add the part that's inside the boundary
                                    Polygons line;
                                    line.addLine(last, current);
                                    line = outline.intersectionPolyLines(line);
                                    if (line.Count > 0)
                                    {
                                        // some of the line is inside the boundary
                                        result.addLine(line[0][0], line[0][1]);
                                        if (zig_zaggify)
                                        {
                                            chain_end[chain_end_index] = line[0][(line[0][0] != last && line[0][0] != current) ? 0 : 1];
                                            if (++chain_end_index == 2)
                                            {
                                                chains[0].Add(chain_end[0]);
                                                chains[1].Add(chain_end[1]);
                                                chain_end_index = 0;
                                                connected_to[0].Add(std::numeric_limits<int>::max());
                                                connected_to[1].Add(std::numeric_limits<int>::max());
                                                line_numbers.Add(num_columns);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // none of the line is inside the boundary so the point that's actually on the boundary
                                        // is the chain end
                                        if (zig_zaggify)
                                        {
                                            chain_end[chain_end_index] = (last_inside) ? last : current;
                                            if (++chain_end_index == 2)
                                            {
                                                chains[0].Add(chain_end[0]);
                                                chains[1].Add(chain_end[1]);
                                                chain_end_index = 0;
                                                connected_to[0].Add(std::numeric_limits<int>::max());
                                                connected_to[1].Add(std::numeric_limits<int>::max());
                                                line_numbers.Add(num_columns);
                                            }
                                        }
                                    }
                                }
                            }

                            last = current;
                            last_inside = current_inside;
                            is_first_point = false;
                        }
                    }

                    ++num_columns;
                }
            }
            else
            {
                // "horizontal" lines
                double phase_offset = (sin_z < 0) ? Math.PI : 0;
                for (long x = 0; x < pitch; x += step)
                {
                    double x_rads = 2 * Math.PI * x / pitch;
                    double a = sin_z;
                    double b = Math.Cos(x_rads + phase_offset);
                    double odd_c = cos_z * Math.Sin(x_rads + phase_offset + Math.PI);
                    double even_c = cos_z * Math.Sin(x_rads + phase_offset);
                    double h = Math.Sqrt(a * a + b * b);
                    double odd_y_rads = ((h != 0) ? Math.Asin(odd_c / h) + Math.Asin(b / h) : 0) + Math.PI / 2;
                    double even_y_rads = ((h != 0) ? Math.Asin(even_c / h) + Math.Asin(b / h) : 0) + Math.PI / 2;
                    odd_line_coords.Add(odd_y_rads / Math.PI * pitch);
                    even_line_coords.Add(even_y_rads / Math.PI * pitch);
                }
                int num_coords = odd_line_coords.Count;
                int num_rows = 0;
                for (long y = (long)((Math.Floor(aabb.minY / (double)pitch) - 1) * pitch); y <= aabb.maxY + pitch / 2; y += pitch / 2)
                {
                    bool is_first_point = true;
                    IntPoint last;
                    bool last_inside = false;
                    int chain_end_index = 0;
                    Polygon chain_end = new Polygon();
                    for (long x = (long)((Math.Floor(aabb.minX / (double)pitch) - 1) * pitch); x <= aabb.maxX + pitch; x += pitch)
                    {
                        for (int i = 0; i < num_coords; ++i)
                        {
                            IntPoint current = new IntPoint(x + (long)(i * step), y + (((num_rows & 1) == 1) ? odd_line_coords[i] : even_line_coords[i]) / 2);
                            bool current_inside = outline.inside(current, true);
                            if (!is_first_point)
                            {
                                if (last_inside && current_inside)
                                {
                                    // line doesn't hit the boundary, add the whole line
                                    result.addLine(last, current);
                                }
                                else if (last_inside != current_inside)
                                {
                                    // line hits the boundary, add the part that's inside the boundary
                                    Polygons line;
                                    line.addLine(last, current);
                                    line = outline.intersectionPolyLines(line);
                                    if (line.Count > 0)
                                    {
                                        // some of the line is inside the boundary
                                        result.addLine(line[0][0], line[0][1]);
                                        if (zig_zaggify)
                                        {
                                            chain_end[chain_end_index] = line[0][(line[0][0] != last && line[0][0] != current) ? 0 : 1];
                                            if (++chain_end_index == 2)
                                            {
                                                chains[0].Add(chain_end[0]);
                                                chains[1].Add(chain_end[1]);
                                                chain_end_index = 0;
                                                connected_to[0].Add(std::numeric_limits<int>::max());
                                                connected_to[1].Add(std::numeric_limits<int>::max());
                                                line_numbers.Add(num_rows);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // none of the line is inside the boundary so the point that's actually on the boundary
                                        // is the chain end
                                        if (zig_zaggify)
                                        {
                                            chain_end[chain_end_index] = (last_inside) ? last : current;
                                            if (++chain_end_index == 2)
                                            {
                                                chains[0].Add(chain_end[0]);
                                                chains[1].Add(chain_end[1]);
                                                chain_end_index = 0;
                                                connected_to[0].Add(std::numeric_limits<int>::max());
                                                connected_to[1].Add(std::numeric_limits<int>::max());
                                                line_numbers.Add(num_rows);
                                            }
                                        }
                                    }
                                }
                            }

                            last = current;
                            last_inside = current_inside;
                            is_first_point = false;
                        }
                    }

                    ++num_rows;
                }
            }

            if (zig_zaggify && chains[0].Count > 0)
            {
                // zig-zaggification consists of joining alternate chain ends to make a chain of chains
                // the basic algorithm is that we follow the infill area boundary and as we progress we are either drawing a connector or not
                // whenever we come across the end of a chain we toggle the connector drawing state
                // things are made more complicated by the fact that we want to avoid generating loops and so we need to keep track
                // of the indentity of the first chain in a connected sequence

                int chain_ends_remaining = chains[0].Count * 2;

                for (ConstPolygonRef outline_poly : outline)
                {
                    List<Point> connector_points; // the points that make up a connector line

                    // we need to remember the first chain processed and the path to it from the first outline point
                    // so that later we can possibly connect to it from the last chain processed
                    int first_chain_chain_index = std::numeric_limits<int>::max();
                    List<Point> path_to_first_chain;

                    bool drawing = false; // true when a connector line is being (potentially) created

                    // keep track of the chain+point that a connector line started at
                    int connector_start_chain_index = std::numeric_limits<int>::max();
                    int connector_start_point_index = std::numeric_limits<int>::max();

                    Point cur_point; // current point of interest - either an outline point or a chain end

                    // go round all of the region's outline and find the chain ends that meet it
                    // quit the loop early if we have seen all the chain ends and are not currently drawing a connector
                    for (int outline_point_index = 0; (chain_ends_remaining > 0 || drawing) && outline_point_index < outline_poly.Count; ++outline_point_index)
                    {
                        Point op0 = outline_poly[outline_point_index];
                        Point op1 = outline_poly[(outline_point_index + 1) % outline_poly.Count];
                        List<int> points_on_outline_chain_index;
                        List<int> points_on_outline_point_index;

                        // collect the chain ends that meet this segment of the outline
                        for (int chain_index = 0; chain_index < chains[0].Count; ++chain_index)
                        {
                            for (int point_index = 0; point_index < 2; ++point_index)
                            {
                                // don't include chain ends that are close to the segment but are beyond the segment ends
                                short beyond = 0;
                                if (LinearAlg2D::getDist2FromLineSegment(op0, chains[point_index][chain_index], op1, &beyond) < 10 && !beyond)
                                {
                                    points_on_outline_point_index.Add(point_index);
                                    points_on_outline_chain_index.Add(chain_index);
                                }
                            }
                        }

                        if (outline_point_index == 0 || vSize2(op0 - cur_point) > 100)
                        {
                            // this is either the first outline point or it is another outline point that is not too close to cur_point

                            if (first_chain_chain_index == std::numeric_limits<int>::max())
                            {
                                // include the outline point in the path to the first chain
                                path_to_first_chain.Add(op0);
                            }

                            cur_point = op0;
                            if (drawing)
                            {
                                // include the start point of this outline segment in the connector
                                connector_points.Add(op0);
                            }
                        }

                        // iterate through each of the chain ends that meet the current outline segment
                        while (points_on_outline_chain_index.Count > 0)
                        {
                            // find the nearest chain end to the current point
                            int nearest_point_index = 0;
                            float nearest_point_dist2 = std::numeric_limits<float>::infinity();
                            for (int pi = 0; pi < points_on_outline_chain_index.Count; ++pi)
                            {
                                float dist2 = vSize2f(chains[points_on_outline_point_index[pi]][points_on_outline_chain_index[pi]] - cur_point);
                                if (dist2 < nearest_point_dist2)
                                {
                                    nearest_point_dist2 = dist2;
                                    nearest_point_index = pi;
                                }
                            }
                            int point_index = points_on_outline_point_index[nearest_point_index];
                            int chain_index = points_on_outline_chain_index[nearest_point_index];

                            // make the chain end the current point and add it to the connector line
                            cur_point = chains[point_index][chain_index];

                            if (drawing && connector_points.Count > 0 && vSize2(cur_point - connector_points.back()) < 100)
                            {
                                // this chain end will be too close to the last connector point so throw away the last connector point
                                connector_points.pop_back();
                            }
                            connector_points.Add(cur_point);

                            if (first_chain_chain_index == std::numeric_limits<int>::max())
                            {
                                // this is the first chain to be processed, remember it
                                first_chain_chain_index = chain_index;
                                path_to_first_chain.Add(cur_point);
                            }

                            if (drawing)
                            {
                                // add the connector line segments but only if
                                //  1 - the start/end points are not the opposite ends of the same chain
                                //  2 - the other end of the current chain is not connected to the chain the connector line is coming from

                                if (chain_index != connector_start_chain_index && connected_to[(point_index + 1) % 2][chain_index] != connector_start_chain_index)
                                {
                                    for (int pi = 1; pi < connector_points.Count; ++pi)
                                    {
                                        result.addLine(connector_points[pi - 1], connector_points[pi]);
                                    }
                                    drawing = false;
                                    connector_points.clear();
                                    // remember the connection
                                    connected_to[point_index][chain_index] = connector_start_chain_index;
                                    connected_to[connector_start_point_index][connector_start_chain_index] = chain_index;
                                }
                                else
                                {
                                    // start a new connector from the current location
                                    connector_points.clear();
                                    connector_points.Add(cur_point);

                                    // remember the chain+point that the connector started from
                                    connector_start_chain_index = chain_index;
                                    connector_start_point_index = point_index;
                                }
                            }
                            else
                            {
                                // we have just jumped a gap so now we want to start drawing again
                                drawing = true;

                                // if this connector is the first to be created or we are not connecting chains from the same row/column,
                                // remember the chain+point that this connector is starting from
                                if (connector_start_chain_index == std::numeric_limits<int>::max() || line_numbers[chain_index] != line_numbers[connector_start_chain_index])
                                {
                                    connector_start_chain_index = chain_index;
                                    connector_start_point_index = point_index;
                                }
                            }

                            // done with this chain end
                            points_on_outline_chain_index.erase(points_on_outline_chain_index.begin() + nearest_point_index);
                            points_on_outline_point_index.erase(points_on_outline_point_index.begin() + nearest_point_index);

                            // decrement total amount of work to do
                            --chain_ends_remaining;
                        }
                    }

                    // we have now visited all the points in the outline, if a connector was (potentially) being drawn
                    // check whether the first chain is already connected to the last chain and, if not, draw the
                    // connector between
                    if (drawing && first_chain_chain_index != std::numeric_limits<int>::max()
                        && first_chain_chain_index != connector_start_chain_index
                        && connected_to[0][first_chain_chain_index] != connector_start_chain_index
                        && connected_to[1][first_chain_chain_index] != connector_start_chain_index)
                    {
                        // output the connector line segments from the last chain to the first point in the outline
                        connector_points.Add(outline_poly[0]);
                        for (int pi = 1; pi < connector_points.Count; ++pi)
                        {
                            result.addLine(connector_points[pi - 1], connector_points[pi]);
                        }
                        // output the connector line segments from the first point in the outline to the first chain
                        for (int pi = 1; pi < path_to_first_chain.Count; ++pi)
                        {
                            result.addLine(path_to_first_chain[pi - 1], path_to_first_chain[pi]);
                        }
                    }

                    if (chain_ends_remaining < 1)
                    {
                        break;
                    }
                }
            }

            result_lines = result;
        }
#endif
    }
}
