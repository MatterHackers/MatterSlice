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
	using Polygon = List<IntPoint>;
	public class GCodePath
	{
		public GCodePathConfig config;

		public Polygon polygon = new Polygon();

		/// <summary>
		/// Path is finished, no more moves should be added, and a new path should be started instead of any appending done to this one.
		/// </summary>
		internal bool done;

		internal int extruderIndex;

		public GCodePath()
		{
		}

		public GCodePath(GCodePath copyPath)
		{
			this.config = copyPath.config;
			this.done = copyPath.done;
			this.extruderIndex = copyPath.extruderIndex;
			this.Retract = copyPath.Retract;
			this.polygon = new Polygon(copyPath.polygon);
		}

		internal bool Retract { get; set; }

		public long Length(bool pathIsClosed)
		{
			long totalLength = 0;
			for (int pointIndex = 0; pointIndex < polygon.Count - 1; pointIndex++)
			{
				// Calculate distance between 2 points
				totalLength += (polygon[pointIndex] - polygon[pointIndex + 1]).Length();
			}

			if (pathIsClosed)
			{
				// add in the move back to the start
				totalLength += (polygon[polygon.Count - 1] - polygon[0]).Length();
			}

			return totalLength;
		}
	}
}