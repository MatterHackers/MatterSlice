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
using System.Diagnostics;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public static class LogOutput
    {
        public static int verbose_level;

        public static void logError(string message)
        {
            Console.Write(message);
        }

        public static void _log(string message)
        {
            if (verbose_level < 1)
                return;

            Console.Write(message);
        }

        public static void log(string output)
        {
            Console.Write(output);
        }

        public static void logProgress(string type, int value, int maxValue)
        {
            if (verbose_level < 2)
                return;

            Console.Write(string.Format("Progress:{0}:{1}:{2}\n", type, value, maxValue));
        }

        internal static void logPolygons(string p, int layerNr, int p_2, List<Polygon> list)
        {
#if false
 guiSocket.sendNr(GUI_CMD_SEND_POLYGONS);
        guiSocket.sendNr(polygons.size());
        guiSocket.sendNr(layerNr);
        guiSocket.sendNr(z);
        guiSocket.sendNr(strlen(name));
        guiSocket.sendAll(name, strlen(name));
        for(unsigned int n=0; n<polygons.size(); n++)
        {
            PolygonRef polygon = polygons[n];
            guiSocket.sendNr(polygon.size());
            guiSocket.sendAll(polygon.data(), polygon.size() * sizeof(Point));
        }
#endif
        }
    }
}