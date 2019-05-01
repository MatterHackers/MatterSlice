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

/**
    The TimeEstimateCalculator class generates a estimate of printing time calculated with acceleration in mind.
    Some of this code has been adapted from the Marlin sources.
*/

using System;
using System.Collections.Generic;

public class TimeEstimateCalculator
{
	public static int E_AXIS = 3;
	public static int NUM_AXIS = 4;
	public static int X_AXIS = 0;
	public static int Y_AXIS = 1;
	public static int Z_AXIS = 2;
	private double acceleration = 3000;
	private List<Block> blocks = new List<Block>();
	private Position currentPosition = new Position();
	private double[] max_acceleration = new double[] { 9000, 9000, 100, 10000 };
	private double max_e_jerk = 5.0;
	private double[] max_feedrate = new double[] { 600, 600, 40, 25 };
	private double max_xy_jerk = 20.0;
	private double max_z_jerk = 0.4;
	private double MINIMUM_PLANNER_SPEED = 0.05;// (mm/sec)
	private double minimumfeedrate = 0.01;
	private Position previous_feedrate;

	private double previous_nominal_feedrate;

	public double calculate()
	{
		reverse_pass();
		forward_pass();
		recalculate_trapezoids();

		double totalTime = 0;
		for (int n = 0; n < blocks.Count; n++)
		{
			double plateau_distance = blocks[n].decelerate_after - blocks[n].accelerate_until;

			totalTime += acceleration_time_from_distance(blocks[n].initial_feedrate, blocks[n].accelerate_until, blocks[n].acceleration);
			totalTime += plateau_distance / blocks[n].nominal_feedrate;
			totalTime += acceleration_time_from_distance(blocks[n].final_feedrate, blocks[n].distance - blocks[n].decelerate_after, blocks[n].acceleration);
		}

		return totalTime;
	}

	public void plan(Position newPos, double feedrate)
	{
		Block block = new Block();

		block.maxTravel = 0;
		for (int n = 0; n < NUM_AXIS; n++)
		{
			block.delta[n] = newPos[n] - currentPosition[n];
			block.absDelta[n] = Math.Abs(block.delta[n]);
			block.maxTravel = Math.Max(block.maxTravel, block.absDelta[n]);
		}

		if (block.maxTravel <= 0)
		{
			return;
		}

		if (feedrate < minimumfeedrate)
		{
			feedrate = minimumfeedrate;
		}

		block.distance = Math.Sqrt(square(block.absDelta[0]) + square(block.absDelta[1]) + square(block.absDelta[2]));
		if (block.distance == 0.0)
		{
			block.distance = block.absDelta[3];
		}

		block.nominal_feedrate = feedrate;

		Position current_feedrate = new Position();
		Position current_abs_feedrate = new Position();
		double feedrate_factor = 1.0;
		for (int n = 0; n < NUM_AXIS; n++)
		{
			current_feedrate[n] = block.delta[n] * feedrate / block.distance;
			current_abs_feedrate[n] = Math.Abs(current_feedrate[n]);
			if (current_abs_feedrate[n] > max_feedrate[n])
			{
				feedrate_factor = Math.Min(feedrate_factor, max_feedrate[n] / current_abs_feedrate[n]);
			}
		}

		// TODO: XY_FREQUENCY_LIMIT

		if (feedrate_factor < 1.0)
		{
			for (int n = 0; n < NUM_AXIS; n++)
			{
				current_feedrate[n] *= feedrate_factor;
				current_abs_feedrate[n] *= feedrate_factor;
			}

			block.nominal_feedrate *= feedrate_factor;
		}

		block.acceleration = acceleration;
		for (int n = 0; n < NUM_AXIS; n++)
		{
			if (block.acceleration * (block.absDelta[n] / block.distance) > max_acceleration[n])
			{
				block.acceleration = max_acceleration[n];
			}
		}

		double vmax_junction = max_xy_jerk / 2;
		double vmax_junction_factor = 1.0;
		if (current_abs_feedrate[Z_AXIS] > max_z_jerk / 2)
		{
			vmax_junction = Math.Min(vmax_junction, max_z_jerk / 2);
		}

		if (current_abs_feedrate[E_AXIS] > max_e_jerk / 2)
		{
			vmax_junction = Math.Min(vmax_junction, max_e_jerk / 2);
		}

		vmax_junction = Math.Min(vmax_junction, block.nominal_feedrate);
		double safe_speed = vmax_junction;

		if ((blocks.Count > 0) && (previous_nominal_feedrate > 0.0001))
		{
			double xy_jerk = Math.Sqrt(square(current_feedrate[X_AXIS] - previous_feedrate[X_AXIS]) + square(current_feedrate[Y_AXIS] - previous_feedrate[Y_AXIS]));
			vmax_junction = block.nominal_feedrate;
			if (xy_jerk > max_xy_jerk)
			{
				vmax_junction_factor = max_xy_jerk / xy_jerk;
			}

			if (Math.Abs(current_feedrate[Z_AXIS] - previous_feedrate[Z_AXIS]) > max_z_jerk)
			{
				vmax_junction_factor = Math.Min(vmax_junction_factor, max_z_jerk / Math.Abs(current_feedrate[Z_AXIS] - previous_feedrate[Z_AXIS]));
			}

			if (Math.Abs(current_feedrate[E_AXIS] - previous_feedrate[E_AXIS]) > max_e_jerk)
			{
				vmax_junction_factor = Math.Min(vmax_junction_factor, max_e_jerk / Math.Abs(current_feedrate[E_AXIS] - previous_feedrate[E_AXIS]));
			}

			vmax_junction = Math.Min(previous_nominal_feedrate, vmax_junction * vmax_junction_factor); // Limit speed to max previous speed
		}

		block.max_entry_speed = vmax_junction;

		double v_allowable = max_allowable_speed(-block.acceleration, MINIMUM_PLANNER_SPEED, block.distance);
		block.entry_speed = Math.Min(vmax_junction, v_allowable);
		block.nominal_length_flag = block.nominal_feedrate <= v_allowable;
		block.recalculate_flag = true; // Always calculate trapezoid for new block

		previous_feedrate = current_feedrate;
		previous_nominal_feedrate = block.nominal_feedrate;

		currentPosition = newPos;

		calculate_trapezoid_for_block(block, block.entry_speed / block.nominal_feedrate, safe_speed / block.nominal_feedrate);

		blocks.Add(block);
	}

	public void reset()
	{
		blocks.Clear();
	}

	public void setPosition(Position newPos)
	{
		currentPosition = newPos;
	}

	// This function gives the time it needs to accelerate from an initial speed to reach a final distance.
	private static double acceleration_time_from_distance(double initial_feedrate, double distance, double acceleration)
	{
		double discriminant = Math.Sqrt(square(initial_feedrate) - 2 * acceleration * -distance);
		return (-initial_feedrate + discriminant) / acceleration;
	}

	// Calculates the distance (not time) it takes to accelerate from initial_rate to target_rate using the given acceleration:
	private static double estimate_acceleration_distance(double initial_rate, double target_rate, double acceleration)
	{
		if (acceleration == 0)
		{
			return 0.0;
		}

		return (square(target_rate) - square(initial_rate)) / (2.0 * acceleration);
	}

	// This function gives you the point at which you must start braking (at the rate of -acceleration) if
	// you started at speed initial_rate and accelerated until this point and want to end at the final_rate after
	// a total travel of distance. This can be used to compute the intersection point between acceleration and
	// deceleration in the cases where the trapezoid has no plateau (i.e. never reaches maximum speed)
	private static double intersection_distance(double initial_rate, double final_rate, double acceleration, double distance)
	{
		if (acceleration == 0.0)
		{
			return 0.0;
		}

		return (2.0 * acceleration * distance - square(initial_rate) + square(final_rate)) / (4.0 * acceleration);
	}

	// Calculates the maximum allowable speed at this point when you must be able to reach target_velocity using the
	// acceleration within the allotted distance.
	private static double max_allowable_speed(double acceleration, double target_velocity, double distance)
	{
		return Math.Sqrt(target_velocity * target_velocity - 2 * acceleration * distance);
	}

	private static double square(double a)
	{
		return a * a;
	}

	// Calculates trapezoid parameters so that the entry- and exit-speed is compensated by the provided factors.
	private void calculate_trapezoid_for_block(Block block, double entry_factor, double exit_factor)
	{
		double initial_feedrate = block.nominal_feedrate * entry_factor;
		double final_feedrate = block.nominal_feedrate * exit_factor;

		double acceleration = block.acceleration;
		double accelerate_distance = estimate_acceleration_distance(initial_feedrate, block.nominal_feedrate, acceleration);
		double decelerate_distance = estimate_acceleration_distance(block.nominal_feedrate, final_feedrate, -acceleration);

		// Calculate the size of Plateau of Nominal Rate.
		double plateau_distance = block.distance - accelerate_distance - decelerate_distance;

		// Is the Plateau of Nominal Rate smaller than nothing? That means no cruising, and we will
		// have to use intersection_distance() to calculate when to abort acceleration and start braking
		// in order to reach the final_rate exactly at the end of this block.
		if (plateau_distance < 0)
		{
			accelerate_distance = intersection_distance(initial_feedrate, final_feedrate, acceleration, block.distance);
			accelerate_distance = Math.Max(accelerate_distance, 0.0); // Check limits due to numerical round-off
			accelerate_distance = Math.Min(accelerate_distance, block.distance);// (We can cast here to unsigned, because the above line ensures that we are above zero)
			plateau_distance = 0;
		}

		block.accelerate_until = accelerate_distance;
		block.decelerate_after = accelerate_distance + plateau_distance;
		block.initial_feedrate = initial_feedrate;
		block.final_feedrate = final_feedrate;
	}

	private void forward_pass()
	{
		Block[] block = new Block[] { null, null, null };
		for (int n = 0; n < blocks.Count; n++)
		{
			block[0] = block[1];
			block[1] = block[2];
			block[2] = blocks[n];
			planner_forward_pass_kernel(block[0], block[1], block[2]);
		}

		planner_forward_pass_kernel(block[1], block[2], null);
	}

	// The kernel called by accelerationPlanner::calculate() when scanning the plan from first to last entry.
	private void planner_forward_pass_kernel(Block previous, Block current, Block next)
	{
		if (previous == null)
		{
			return;
		}

		// If the previous block is an acceleration block, but it is not long enough to complete the
		// full speed change within the block, we need to adjust the entry speed accordingly. Entry
		// speeds have already been reset, maximized, and reverse planned by reverse planner.
		// If nominal length is true, max junction speed is guaranteed to be reached. No need to recheck.
		if (!previous.nominal_length_flag)
		{
			if (previous.entry_speed < current.entry_speed)
			{
				double entry_speed = Math.Min(current.entry_speed, max_allowable_speed(-previous.acceleration, previous.entry_speed, previous.distance));

				// Check for junction speed change
				if (current.entry_speed != entry_speed)
				{
					current.entry_speed = entry_speed;
					current.recalculate_flag = true;
				}
			}
		}
	}

	// The kernel called by accelerationPlanner::calculate() when scanning the plan from last to first entry.
	private void planner_reverse_pass_kernel(Block previous, Block current, Block next)
	{
		if (current == null || next == null)
		{
			return;
		}

		// If entry speed is already at the maximum entry speed, no need to recheck. Block is cruising.
		// If not, block in state of acceleration or deceleration. Reset entry speed to maximum and
		// check for maximum allowable speed reductions to ensure maximum possible planned speed.
		if (current.entry_speed != current.max_entry_speed)
		{
			// If nominal length true, max junction speed is guaranteed to be reached. Only compute
			// for max allowable speed if block is decelerating and nominal length is false.
			if ((!current.nominal_length_flag) && (current.max_entry_speed > next.entry_speed))
			{
				current.entry_speed = Math.Min(current.max_entry_speed, max_allowable_speed(-current.acceleration, next.entry_speed, current.distance));
			}
			else
			{
				current.entry_speed = current.max_entry_speed;
			}

			current.recalculate_flag = true;
		}
	}

	// Recalculates the trapezoid speed profiles for all blocks in the plan according to the
	// entry_factor for each junction. Must be called by planner_recalculate() after
	// updating the blocks.
	private void recalculate_trapezoids()
	{
		Block current;
		Block next = null;

		for (int n = 0; n < blocks.Count; n++)
		{
			current = next;
			next = blocks[n];
			if (current != null)
			{
				// Recalculate if current block entry or exit junction speed has changed.
				if (current.recalculate_flag || next.recalculate_flag)
				{
					// NOTE: Entry and exit factors always > 0 by all previous logic operations.
					calculate_trapezoid_for_block(current, current.entry_speed / current.nominal_feedrate, next.entry_speed / current.nominal_feedrate);
					current.recalculate_flag = false; // Reset current only to ensure next trapezoid is computed
				}
			}
		}

		// Last/newest block in buffer. Exit speed is set with MINIMUM_PLANNER_SPEED. Always recalculated.
		if (next != null)
		{
			calculate_trapezoid_for_block(next, next.entry_speed / next.nominal_feedrate, MINIMUM_PLANNER_SPEED / next.nominal_feedrate);
			next.recalculate_flag = false;
		}
	}

	private void reverse_pass()
	{
		Block[] block = new Block[] { null, null, null };
		for (int n = blocks.Count - 1; n >= 0; n--)
		{
			block[2] = block[1];
			block[1] = block[0];
			block[0] = blocks[n];
			planner_reverse_pass_kernel(block[0], block[1], block[2]);
		}
	}

	public class Block
	{
		public Position absDelta = new Position();
		public double accelerate_until;
		public double acceleration;
		public double decelerate_after;
		public Position delta = new Position();
		public double distance;
		public double entry_speed;
		public double final_feedrate;
		public double initial_feedrate;
		public double max_entry_speed;
		public double maxTravel;
		public double nominal_feedrate;
		public bool nominal_length_flag;
		public bool recalculate_flag;
	}

;

	public class Position
	{
		public double[] axis = new double[NUM_AXIS];

		public Position()
		{
			for (int n = 0; n < NUM_AXIS; n++)
			{
				axis[n] = 0;
			}
		}

		public Position(double x, double y, double z, double e)
		{
			axis[0] = x; axis[1] = y; axis[2] = z; axis[3] = e;
		}

		public double this[int index]
		{
			get
			{
				return axis[index];
			}

			set
			{
				axis[index] = value;
			}
		}
	}

;
}