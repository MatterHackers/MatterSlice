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
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using System.Collections.Generic;
using System;

namespace MatterHackers.MatterSlice
{
    public class MonotonicSorter
    {
		private IntPoint lastPosition;
		private Polygons sorted;
        private List<bool> linePrinted;
		private IntPoint perpendicular;

        private bool EverythingLeftHasBeenPrinted(int checkIndex)
		{
            // check that there is no unprinted touching segment on the left (down the perpendicular) that needs to be printed
            for (int i = 0; i < checkIndex; i++)
            {
                // first check if there is an unprinted segment to the left
                if (!linePrinted[i])
                {
                    // check if that unprinted segment is touching this one
                }
            }

            return true;
        }

        private IEnumerable<int> NextIndex
		{
            get
			{
                var printedCount = 0;
                while (printedCount < linePrinted.Count)
                {
                    var first = true;
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        if (!linePrinted[i]
                            && (first || EverythingLeftHasBeenPrinted(i)))
                        {
                            linePrinted[i] = true;
                            printedCount++;
                            yield return i;
                        }
                    }
                }
            }
        }

		public IEnumerable<Polygon> Ordered
        {
            get
            {
                if (sorted != null && sorted.Count > 0)
                {
                    foreach(var i in NextIndex)
                    {
                        var distTo0 = (lastPosition - sorted[i][0]).LengthSquared();
                        var distTo1 = (lastPosition - sorted[i][1]).LengthSquared();
                        if(distTo1 < distTo0)
						{
                            // swap points 0 and 1
                            var old0 = sorted[i][0];
                            sorted[i][0] = sorted[i][1];
                            sorted[i][1] = old0;
                        }

                        yield return sorted[i];
                        lastPosition = sorted[i][1];
                    }
                }
            }
        }

        /// <summary>
        /// It is expected that all the polygons are set, are parallel and have exactly 2 points each
        /// </summary>
        /// <param name="polygons"></param>
        public MonotonicSorter(Polygons polygons, GCodePathConfig pathConfig, IntPoint lastPosition)
        {
            if (polygons.Count > 0)
            {
                this.lastPosition = lastPosition;

                var count = polygons.Count;
                sorted = new Polygons(count);
                linePrinted = new List<bool>(count);

                // find the longest segment (or at least a long segment)
                var maxLengthSquared = 0.0;
                var maxIndex = 0;
                for (var i = 0; i < polygons.Count; i++)
                {
                    var polygon = polygons[i];

                    var lengthSquared = polygon.PolygonLengthSquared(false);
                    if (lengthSquared > maxLengthSquared)
                    {
                        maxLengthSquared = lengthSquared;
                        maxIndex = i;

                        if (lengthSquared > 100000)
                        {
                            // long enough to get a good perpendicular
                            break;
                        }
                    }
                }

                // get the perpendicular
                perpendicular = (polygons[maxIndex][1] - polygons[maxIndex][0]).GetPerpendicularLeft();

                // find the point minimum point in this direction
                var minDistance = double.MaxValue;
                var minIndex = 0;
                for (var i = 0; i < polygons.Count; i++)
                {
                    var polygon = polygons[i];

                    // add the point with width
                    sorted.Add(new Polygon());
                    sorted[i].Add(new IntPoint(polygon[0]) { Width = pathConfig.LineWidth_um });
                    sorted[i].Add(new IntPoint(polygon[1]) { Width = pathConfig.LineWidth_um });
                    linePrinted.Add(false);

                    var distance = perpendicular.Dot(polygon[0]);
                    if(distance < minDistance)
					{
                        minDistance = distance;
                        minIndex = i;
					}
                }

                // sort the polygons based on the distance from the minimum
                sorted.Sort((a, b) =>
                {
                    return perpendicular.Dot(a[0]).CompareTo(perpendicular.Dot(b[0]));

                });

                // reverse the list if the end is closer to the lastPosition
                var minStart = Math.Min((sorted[0][0] - lastPosition).LengthSquared(), (sorted[0][1] - lastPosition).LengthSquared());
                var minEnd = Math.Min((sorted[count - 1][0] - lastPosition).LengthSquared(), (sorted[count - 1][1] - lastPosition).LengthSquared());
                if (minEnd < minStart)
				{
                    sorted.Reverse();
                    // and make sure we understand the positive direction
                    perpendicular *= -1;
				}
            }
        }
   }
}