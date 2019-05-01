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

using System.Collections.Generic;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;

namespace MatterHackers.MatterSlice
{
	public enum RetractType
	{
		None,
		Requested,
		Force,
	}

	public class GCodePath
	{
		private GCodePathConfig _config;

		public GCodePathConfig Config
		{
			get => _config;
			set
			{
				_config = value;
				Speed = _config.Speed;
			}
		}

		public GCodePath()
		{
		}

		public GCodePath(GCodePath copyPath)
		{
			this.Config = copyPath.Config;
			this.Speed = copyPath.Speed;
			this.Done = copyPath.Done;
			this.ExtruderIndex = copyPath.ExtruderIndex;
			this.Retract = copyPath.Retract;
			this.Polygon = new Polygon(copyPath.Polygon);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the path is finished, no more moves should be added, and a new path should be started instead of any appending done to this one.
		/// </summary>
		public bool Done { get; set; }

		public int ExtruderIndex { get; set; }

		public int FanPercent { get; set; } = -1;

		public Polygon Polygon { get; set; } = new Polygon();

		public RetractType Retract { get; set; } = RetractType.None;

		public double Speed { get; internal set; }

		public long Length(bool pathIsClosed)
		{
			long totalLength = 0;
			for (int pointIndex = 0; pointIndex < Polygon.Count - 1; pointIndex++)
			{
				// Calculate distance between 2 points
				totalLength += (Polygon[pointIndex] - Polygon[pointIndex + 1]).Length();
			}

			if (pathIsClosed)
			{
				// add in the move back to the start
				totalLength += (Polygon[Polygon.Count - 1] - Polygon[0]).Length();
			}

			return totalLength;
		}
	}
}