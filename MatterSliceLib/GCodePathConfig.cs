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

namespace MatterHackers.MatterSlice
{
	/// <summary>
	/// Contains the configuration for moves/extrusion actions. This defines at which width the line is printed and at which speed.
	/// </summary>
	public class GCodePathConfig
	{
		public GCodePathConfig(string configName, string gcodeComment)
		{
			this.Name = configName;
			this.GCodeComment = gcodeComment;
		}

		public bool ClosedLoop { get; set; } = true;

		public bool DoSeamHiding { get; set; }

		public string GCodeComment { get; set; }

		public long LineWidth_um { get; set; }

		public string Name { get; set; }

		public double Speed { get; private set; }

		public bool Spiralize { get; set; }

		/// <summary>
		/// Set the data for a path config. This is used to define how different parts (infill, perimeters) are written to gcode.
		/// </summary>
		/// <param name="speed"></param>
		/// <param name="lineWidth_um"></param>
		public void SetData(double speed, long lineWidth_um)
		{
			this.Speed = speed;
			this.LineWidth_um = lineWidth_um;
		}

		public GCodePathConfig Clone(string newConfigName, string newGCodeComment)
		{
			return new GCodePathConfig(newConfigName, newGCodeComment)
			{
				ClosedLoop = this.ClosedLoop,
				DoSeamHiding = this.DoSeamHiding,
				LineWidth_um = this.LineWidth_um,
				Speed = this.Speed,
				Spiralize = this.Spiralize
			};
		}
	}
}