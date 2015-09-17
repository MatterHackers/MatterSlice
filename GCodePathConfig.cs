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
using System.Text;

namespace MatterHackers.MatterSlice
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	//The GCodePathConfig is the configuration for moves/extrusion actions. This defines at which width the line is printed and at which speed.
	public class GCodePathConfig
	{
		public bool closedLoop = true;
		public int lineWidth;
		public string name;
		public double speed;
		public bool spiralize;

		public GCodePathConfig()
		{
		}

		public GCodePathConfig(double speed, int lineWidth, string name)
		{
			this.speed = speed;
			this.lineWidth = lineWidth;
			this.name = name;
		}

		public void setData(double speed, int lineWidth, string name, bool closedLoop = true)
		{
			this.closedLoop = closedLoop;
			this.speed = speed;
			this.lineWidth = lineWidth;
			this.name = name;
		}
	}
}