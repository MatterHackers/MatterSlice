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

namespace MatterHackers.MatterSlice
{
	public class DMatrix3x3
	{
		public double[,] m = new double[3, 3];

		public DMatrix3x3()
		{
			m[0, 0] = 1.0;
			m[1, 0] = 0.0;
			m[2, 0] = 0.0;
			m[0, 1] = 0.0;
			m[1, 1] = 1.0;
			m[2, 1] = 0.0;
			m[0, 2] = 0.0;
			m[1, 2] = 0.0;
			m[2, 2] = 1.0;
		}

		public DMatrix3x3(string valueToSetTo)
		{
			valueToSetTo = valueToSetTo.Replace("[", "");
			valueToSetTo = valueToSetTo.Replace("]", "");
			string[] values = valueToSetTo.Split(',');

			m[0, 0] = double.Parse(values[0]);
			m[1, 0] = double.Parse(values[1]);
			m[2, 0] = double.Parse(values[2]);
			m[0, 1] = double.Parse(values[3]);
			m[1, 1] = double.Parse(values[4]);
			m[2, 1] = double.Parse(values[5]);
			m[0, 2] = double.Parse(values[6]);
			m[1, 2] = double.Parse(values[7]);
			m[2, 2] = double.Parse(values[8]);
		}

		public IntPoint apply(Vector3 p)
		{
			return new IntPoint(
				p.x * m[0, 0] + p.y * m[1, 0] + p.z * m[2, 0],
				p.x * m[0, 1] + p.y * m[1, 1] + p.z * m[2, 1],
				p.x * m[0, 2] + p.y * m[1, 2] + p.z * m[2, 2]);
		}

		public override string ToString()
		{
			return "[[{0},{1},{2}],[{3},{4},{5}],[{6},{7},{8}]]".FormatWith(
				m[0, 0], m[1, 0], m[2, 0],
				m[0, 1], m[1, 1], m[2, 1],
				m[0, 2], m[1, 2], m[2, 2]);
		}
	}
}