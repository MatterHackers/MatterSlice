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

namespace MatterHackers.MatterSlice
{
	/// <summary>
	/// Contains the configuration for moves/extrusion actions. This defines at which width the line is printed and at which speed.
	/// </summary>
	public class GCodePathConfig
	{
		public bool closedLoop = true;
		public int lineWidth_um;
		public string gcodeComment;
		public double speed;
		private string Name { get; set; }
		public bool spiralize;
		public bool doSeamHiding;

		public GCodePathConfig(string configName)
		{
			this.Name = configName;
		}

		/// <summary>
		/// Set the data for a path config. This is used to define how different parts (infill, perimeters) are written to gcode.
		/// </summary>
		/// <param name="speed"></param>
		/// <param name="lineWidth_um"></param>
		/// <param name="gcodeComment"></param>
		/// <param name="closedLoop"></param>
		public void SetData(double speed, int lineWidth_um, string gcodeComment, bool closedLoop = true)
		{
			this.closedLoop = closedLoop;
			this.speed = speed;
			this.lineWidth_um = lineWidth_um;
			this.gcodeComment = gcodeComment;
		}
	}
}