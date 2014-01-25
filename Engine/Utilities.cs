/*
Copyright (c) 2013, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterSlice
{
    public static class Utilities
    {
        public static void log(string message)
        {
            Console.Write(message);
        }

        public static void logError(string message)
        {
            Console.Write(message);
        }

        public static void Output(string message)
        {
            Console.Write(message);
        }

        public static void logProgress(string progressType, int current, int total)
        {
            Console.Write(string.Format("Progress:{0}, {1},{2}", progressType, current, total));
        }
    }
}
