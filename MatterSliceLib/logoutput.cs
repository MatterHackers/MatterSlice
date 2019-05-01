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

using System;

namespace MatterHackers.MatterSlice
{
	public static class LogOutput
	{
		public static event EventHandler GetLogWrites;

		public static int verbose_level;

		public static void Log(string output)
		{
			Console.Write(output);

			GetLogWrites?.Invoke(output, null);
		}

		public static void LogError(string message)
		{
			Console.Write(message);
		}

		public static void logProgress(string type, int value, int maxValue)
		{
			if (verbose_level < 2)
			{
				return;
			}

			Console.Write("Progress:{0}:{1}:{2}\n".FormatWith(type, value, maxValue));
		}
	}
}