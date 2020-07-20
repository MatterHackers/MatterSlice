using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.QuadTree;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public static class GyroidPolygonsExtensions
	{
		public static void AddLine(this Polygons polygons, IntPoint start, IntPoint end)
		{
			polygons.Add(new Polygon() { start, end });
		}
	}

	public static class GyroidInfill
	{
		public static void Generate(ConfigSettings config,
			Polygons in_outline,
			Polygons result_lines,
			bool zigZag,
			int layerIndex)
		{
			// generate infill based on the gyroid equation: sin_x * cos_y + sin_y * cos_z + sin_z * cos_x = 0
			// kudos to the author of the Slic3r implementation equation code, the equation code here is based on that
			Polygons outline = in_outline.Offset(config.ExtrusionWidth_um / 2);
			var aabb = outline.GetBounds();

			var linespacing_um = (int)(config.ExtrusionWidth_um / (config.InfillPercent / 100));
			var z = layerIndex * config.LayerThickness_um;

			int pitch = (int)(linespacing_um * 2.41); // this produces similar density to the "line" infill pattern
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
			var odd_line_coords = new List<double>();
			var even_line_coords = new List<double>();
			var result = new Polygons();
			var chains = new Polygon[] { new Polygon(), new Polygon() }; // [start_points[], end_points[]]
			var connected_to = new List<int>[] { new List<int>(), new List<int>() }; // [chain_indices[], chain_indices[]]
			var line_numbers = new List<int>(); // which row/column line a chain is part of
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
					IntPoint last = default(IntPoint);
					bool last_inside = false;
					int chain_end_index = 0;
					var chain_end = new IntPoint[2];
					for (long y = (long)((Math.Floor(aabb.minY / (double)pitch) - 1) * pitch); y <= aabb.maxY + pitch; y += pitch)
					{
						for (int i = 0; i < num_coords; ++i)
						{
							var current = new IntPoint(x + (((num_columns & 1) == 1) ? odd_line_coords[i] : even_line_coords[i]) / 2 + pitch, y + (long)(i * step));
							bool current_inside = outline.PointIsInside(current);
							if (!is_first_point)
							{
								if (last_inside && current_inside)
								{
									// line doesn't hit the boundary, add the whole line
									result.AddLine(last, current);
								}
								else if (last_inside != current_inside)
								{
									// line hits the boundary, add the part that's inside the boundary
									var line = new Polygons();
									line.AddLine(last, current);
									line = outline.CreateLineIntersections(line);
									if (line.Count > 0)
									{
										// some of the line is inside the boundary
										result.AddLine(line[0][0], line[0][1]);

										var end = line[0][(line[0][0] != last && line[0][0] != current) ? 0 : 1];
										var lineNumber = num_columns;
										if (zigZag)
										{
											chain_end[chain_end_index] = end;
											if (++chain_end_index == 2)
											{
												chains[0].Add(chain_end[0]);
												chains[1].Add(chain_end[1]);
												chain_end_index = 0;
												connected_to[0].Add(int.MaxValue);
												connected_to[1].Add(int.MaxValue);
												line_numbers.Add(lineNumber);
											}
										}
									}
									else
									{
										// none of the line is inside the boundary so the point that's actually on the boundary
										// is the chain end
										var end = last_inside ? last : current;
										var lineNumber = num_columns;
										if (zigZag)
										{
											chain_end[chain_end_index] = end;
											if (++chain_end_index == 2)
											{
												chains[0].Add(chain_end[0]);
												chains[1].Add(chain_end[1]);
												chain_end_index = 0;
												connected_to[0].Add(int.MaxValue);
												connected_to[1].Add(int.MaxValue);
												line_numbers.Add(lineNumber);
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
					IntPoint last = default(IntPoint);
					bool last_inside = false;
					int chain_end_index = 0;
					var chain_end = new IntPoint[2];
					for (long x = (long)((Math.Floor(aabb.minX / (double)pitch) - 1) * pitch); x <= aabb.maxX + pitch; x += pitch)
					{
						for (int i = 0; i < num_coords; ++i)
						{
							var current = new IntPoint(x + (long)(i * step), y + (((num_rows & 1) == 1) ? odd_line_coords[i] : even_line_coords[i]) / 2);
							bool current_inside = outline.PointIsInside(current);
							if (!is_first_point)
							{
								if (last_inside && current_inside)
								{
									// line doesn't hit the boundary, add the whole line
									result.AddLine(last, current);
								}
								else if (last_inside != current_inside)
								{
									// line hits the boundary, add the part that's inside the boundary
									var line = new Polygons();
									line.AddLine(last, current);
									line = outline.CreateLineIntersections(line);
									if (line.Count > 0)
									{
										// some of the line is inside the boundary
										result.AddLine(line[0][0], line[0][1]);
										var end = line[0][(line[0][0] != last && line[0][0] != current) ? 0 : 1];
										var lineNumber = num_rows;
										if (zigZag)
										{
											chain_end[chain_end_index] = end;
											if (++chain_end_index == 2)
											{
												chains[0].Add(chain_end[0]);
												chains[1].Add(chain_end[1]);
												chain_end_index = 0;
												connected_to[0].Add(int.MaxValue);
												connected_to[1].Add(int.MaxValue);
												line_numbers.Add(lineNumber);
											}
										}
									}
									else
									{
										// none of the line is inside the boundary so the point that's actually on the boundary
										// is the chain end
										var end = (last_inside) ? last : current;
										var lineNumber = num_rows;
										if (zigZag)
										{
											chain_end[chain_end_index] = end;
											if (++chain_end_index == 2)
											{
												chains[0].Add(chain_end[0]);
												chains[1].Add(chain_end[1]);
												chain_end_index = 0;
												connected_to[0].Add(int.MaxValue);
												connected_to[1].Add(int.MaxValue);
												line_numbers.Add(lineNumber);
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

			if (zigZag && chains[0].Count > 0)
			{
				// zig-zag connecting consists of joining alternate chain ends to make a chain of chains
				// the basic algorithm is that we follow the infill area boundary and as we progress we are either drawing a connector or not
				// whenever we come across the end of a chain we toggle the connector drawing state
				// things are made more complicated by the fact that we want to avoid generating loops and so we need to keep track
				// of the identity of the first chain in a connected sequence

				int chain_ends_remaining = chains[0].Count * 2;

				foreach (var outline_poly in outline)
				{
					var connector_points = new Polygon(); // the points that make up a connector line

					// we need to remember the first chain processed and the path to it from the first outline point
					// so that later we can possibly connect to it from the last chain processed
					int first_chain_chain_index = int.MaxValue;
					var path_to_first_chain = new Polygon();

					bool drawing = false; // true when a connector line is being (potentially) created

					// keep track of the chain+point that a connector line started at
					int connector_start_chain_index = int.MaxValue;
					int connector_start_point_index = int.MaxValue;

					IntPoint cur_point = default(IntPoint); // current point of interest - either an outline point or a chain end

					// go round all of the region's outline and find the chain ends that meet it
					// quit the loop early if we have seen all the chain ends and are not currently drawing a connector
					for (int outline_point_index = 0; (chain_ends_remaining > 0 || drawing) && outline_point_index < outline_poly.Count; ++outline_point_index)
					{
						var op0 = outline_poly[outline_point_index];
						var op1 = outline_poly[(outline_point_index + 1) % outline_poly.Count()];
						var points_on_outline_chain_index = new List<int>();
						var points_on_outline_point_index = new List<int>();

						// collect the chain ends that meet this segment of the outline
						for (int chain_index = 0; chain_index < chains[0].Count; ++chain_index)
						{
							for (int point_index = 0; point_index < 2; ++point_index)
							{
								// don't include chain ends that are close to the segment but are beyond the segment ends
								int beyond = 0;
								if (GetDist2FromLineSegment(op0, chains[point_index][chain_index], op1, ref beyond) < 10
									&& beyond != 0)
								{
									points_on_outline_point_index.Add(point_index);
									points_on_outline_chain_index.Add(chain_index);
								}
							}
						}

						if (outline_point_index == 0 || (op0 - cur_point).Length() > 100)
						{
							// this is either the first outline point or it is another outline point that is not too close to cur_point

							if (first_chain_chain_index == int.MaxValue)
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
							var nearest_point_dist_squared = double.MaxValue;
							for (int pi = 0; pi < points_on_outline_chain_index.Count; ++pi)
							{
								var first = chains[points_on_outline_point_index[pi]][points_on_outline_chain_index[pi]];
								var dist_squared = (first - cur_point).LengthSquared();
								if (dist_squared < nearest_point_dist_squared)
								{
									nearest_point_dist_squared = dist_squared;
									nearest_point_index = pi;
								}
							}

							int point_index = points_on_outline_point_index[nearest_point_index];
							int chain_index = points_on_outline_chain_index[nearest_point_index];

							// make the chain end the current point and add it to the connector line
							cur_point = chains[point_index][chain_index];

							if (drawing && connector_points.Count > 0
								&& (cur_point - connector_points.Last()).Length() < 100)
							{
								// this chain end will be too close to the last connector point so throw away the last connector point
								connector_points.RemoveAt(connector_points.Count - 1);
							}

							connector_points.Add(cur_point);

							if (first_chain_chain_index == int.MaxValue)
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
										result.AddLine(connector_points[pi - 1], connector_points[pi]);
									}

									drawing = false;
									connector_points.Clear();
									// remember the connection
									connected_to[point_index][chain_index] = connector_start_chain_index;
									connected_to[connector_start_point_index][connector_start_chain_index] = chain_index;
								}
								else
								{
									// start a new connector from the current location
									connector_points.Clear();
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
								if (connector_start_chain_index == int.MaxValue || line_numbers[chain_index] != line_numbers[connector_start_chain_index])
								{
									connector_start_chain_index = chain_index;
									connector_start_point_index = point_index;
								}
							}

							// done with this chain end
							if (points_on_outline_chain_index.Count > 0)
							{
								points_on_outline_chain_index.RemoveAt(Math.Min(points_on_outline_chain_index.Count - 1, points_on_outline_chain_index[0] + nearest_point_index));
							}

							if (points_on_outline_chain_index.Count > 0)
							{
								points_on_outline_chain_index.RemoveAt(Math.Min(points_on_outline_chain_index.Count - 1, points_on_outline_chain_index[0] + nearest_point_index));
							}

							// decrement total amount of work to do
							--chain_ends_remaining;
						}
					}

					// we have now visited all the points in the outline, if a connector was (potentially) being drawn
					// check whether the first chain is already connected to the last chain and, if not, draw the
					// connector between
					if (drawing && first_chain_chain_index != int.MaxValue
						&& first_chain_chain_index != connector_start_chain_index
						&& connected_to[0][first_chain_chain_index] != connector_start_chain_index
						&& connected_to[1][first_chain_chain_index] != connector_start_chain_index)
					{
						// output the connector line segments from the last chain to the first point in the outline
						connector_points.Add(outline_poly[0]);
						for (int pi = 1; pi < connector_points.Count; ++pi)
						{
							result.AddLine(connector_points[pi - 1], connector_points[pi]);
						}

						// output the connector line segments from the first point in the outline to the first chain
						for (int pi = 1; pi < path_to_first_chain.Count; ++pi)
						{
							result.AddLine(path_to_first_chain[pi - 1], path_to_first_chain[pi]);
						}
					}

					if (chain_ends_remaining < 1)
					{
						break;
					}
				}
			}

			result_lines.AddRange(result);
		}

		/*
         * Get the squared distance from point b to a line *segment* from a to c.
         *
         * In case b is on a or c, b_is_beyond_ac should become 0.
         * param a the first point of the line segment
         * param b the point to measure the distance from
         * param c the second point on the line segment
         * param b_is_beyond_ac optional output parameter: whether b is closest to the line segment (0), to a (-1) or b (1)
         * */
		private static long GetDist2FromLineSegment(IntPoint a, IntPoint b, IntPoint c, ref int b_is_beyond_ac)
		{
			/* 
            *     a,
            *     /|
            *    / |
            * b,/__|, x
            *   \  |
            *    \ |
            *     \|
            *      'c
            * 
            * x = b projected on ac
            * ax = ab dot ac / vSize(ac)
            * xb = ab - ax
            * error = vSize(xb)
            */
			IntPoint ac = c - a;
			long ac_size = ac.Length();

			IntPoint ab = b - a;
			if (ac_size == 0)
			{
				long ab_dist2 = ab.LengthSquared();
				if (ab_dist2 == 0 && b_is_beyond_ac != 0)
				{
					b_is_beyond_ac = 0; // a is on b is on c
				}

				// otherwise variable b_is_beyond_ac remains its value; it doesn't make sense to choose between -1 and 1
				return ab_dist2;
			}

			long projected_x = ab.Dot(ac);
			long ax_size = projected_x / ac_size;

			if (ax_size < 0)
			{
				// b is 'before' segment ac
				b_is_beyond_ac = -1;
				return ab.LengthSquared();
			}

			if (ax_size > ac_size)
			{
				// b is 'after' segment ac
				b_is_beyond_ac = 1;
				return (b - c).LengthSquared();
			}

			b_is_beyond_ac = 0;
			IntPoint ax = ac * ax_size / ac_size;
			IntPoint bx = ab - ax;
			return bx.LengthSquared();
			// return vSize2(ab) - ax_size*ax_size; // less accurate
		}
	}
}
