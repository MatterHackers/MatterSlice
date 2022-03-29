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
using MatterHackers.VectorMath;

namespace MatterHackers.MatterSlice
{
    public class MonotonicSorter
    {
		private IntPoint lastPosition;
		private Polygons sorted;
        private List<bool> linePrinted;
		private Vector2 perpendicular;
		private double lineWidth_um;

        private Vector2 AsVector2(IntPoint intPoint)
        {
            return new Vector2(intPoint.X / 1000.0, intPoint.Y / 1000.0);
        }

        private bool LinesAreTouching(int indexA, int indexB)
		{
            var startA = AsVector2(sorted[indexA][0]);
            var endA = AsVector2(sorted[indexA][1]);
            var normal = Vector2.Normalize(endA - startA);

            var startB = AsVector2(sorted[indexB][0]);
            var endB = AsVector2(sorted[indexB][1]);
            var deltaB = endB - startB;
            if (Vector2.Dot(normal, deltaB) < 0)
			{
                // swap B
                var hold = startB;
                startB = endB;
                endB = hold;
			}

            bool PointWithinLine(Vector2 point, Vector2 start, Vector2 end)
            {
                var lineDelta = end - start;
                var lineLength = lineDelta.Length;
                var lineNormal = Vector2.Normalize(lineDelta);

                var pointDelta = point - start;
                var pointLength = Vector2.Dot(lineNormal, pointDelta);
                if (pointLength < 0)
				{
                    return false;
				}

                if (pointLength > lineLength)
				{
                    return false;
				}

                return true;
            }

            bool PointWithinA(Vector2 point)
            {
                return PointWithinLine(point, startA, endA);
            }

            bool PointWithinB(Vector2 point)
            {
                return PointWithinLine(point, startB, endB);
            }

            if (PointWithinA(startB)
                || PointWithinA(endB)
                || PointWithinB(startA)
                || PointWithinB(endA))
			{
                var distance = Math.Abs(Vector2.Dot(perpendicular, startB - startA));
                if (Math.Abs(distance - lineWidth_um) < 1)
                {
                    return true;
                }
			}

            return false;
		}

        private bool EverythingLeftHasBeenPrinted(int checkIndex)
        {
            // check that there is no unprinted touching segment on the left (down the perpendicular) that needs to be printed
            for (var i = checkIndex - 1; i >= 0; i--)
            {
                // first check if there is an unprinted segment to the left
                if (!linePrinted[i])
                {
                    var startA = AsVector2(sorted[checkIndex][0]);
                    var startB = AsVector2(sorted[i][0]);
                    var distance = Math.Abs(Vector2.Dot(perpendicular, startB - startA));
                    if (Math.Abs(distance) > lineWidth_um * 2)
                    {
                        // the tested line is too far back to be touching so stop checking, we are good.
                        return true;
                    }

                    // check if that unprinted segment is touching this one
                    if (LinesAreTouching(checkIndex, i))
					{
                        return false;
					}
                }
            }

            return true;
        }

        private long DistFromLastPositionSquared(int indexA)
		{
            var a0tob0 = (sorted[indexA][0] - lastPosition).LengthSquared();
            var a0tob1 = (sorted[indexA][0] - lastPosition).LengthSquared();

            return Math.Min(a0tob0, a0tob1);
        }

        private int AdvanceToNextRightSegment(int lastIndex)
        {
            var nextIndex = lastIndex + 1;
            if (nextIndex < sorted.Count
                && !linePrinted[nextIndex]
                && LinesAreTouching(lastIndex, nextIndex))
            {
                return nextIndex;
            }

            return sorted.Count;
        }

        int ClosestAvailableUnprintedSegment()
        {
            // find the best segment that has nothing to the left and is the closest to last position
            var closestNextSegment = sorted.Count;
            var bestDistSquared = long.MaxValue;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!linePrinted[i]
                    && EverythingLeftHasBeenPrinted(i))
                {
                    var minDistSquared = DistFromLastPositionSquared(i);
                    if (minDistSquared < bestDistSquared)
                    {
                        // keep track of closest to our segment
                        bestDistSquared = minDistSquared;
                        closestNextSegment = i;
                    }
                }
            }

            return closestNextSegment;
        }

        private IEnumerable<int> NextIndex
		{
            get
			{
                var printedCount = 0;
                while (printedCount < linePrinted.Count)
                {
                    var first = true;
                    int i = ClosestAvailableUnprintedSegment();
                    var leftError = false;
                    while (i < sorted.Count)
                    {
                        if (!linePrinted[i]
                            && (first || (leftError = EverythingLeftHasBeenPrinted(i))))
                        {
                            first = false;
                            linePrinted[i] = true;
                            printedCount++;
                            yield return i;
                            i = AdvanceToNextRightSegment(i);
                        }
                        else if (leftError)
						{
                            // start over at the beginning and look for the next start point
                            i = sorted.Count;
						}
                        else
						{
                            // move on to the next point
                            i++;
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
        public MonotonicSorter(Polygons polygons, IntPoint lastPosition, long lineWidth_um)
        {
            this.lineWidth_um = lineWidth_um / 1000.0;
            if (polygons.Count > 0)
            {
                this.lastPosition = lastPosition;

                var count = polygons.Count;
                sorted = new Polygons(count);
                linePrinted = new List<bool>(count);

                var (_, perpendicularIntPoint) = polygons.GetPerpendicular();
                perpendicular = new Vector2(perpendicularIntPoint.X, perpendicularIntPoint.Y).GetNormal();

                // find the point minimum point in this direction
                var minDistance = double.MaxValue;
                var minIndex = 0;
                for (var i = 0; i < polygons.Count; i++)
                {
                    var polygon = polygons[i];

                    // add the point with width
                    sorted.Add(new Polygon());
                    sorted[i].Add(new IntPoint(polygon[0]) { Width = lineWidth_um });
                    sorted[i].Add(new IntPoint(polygon[1]) { Width = lineWidth_um });
                    linePrinted.Add(false);

                    var distance = perpendicularIntPoint.Dot(polygon[0]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        minIndex = i;
                    }
                }

                // sort the polygons based on the distance from the minimum
                sorted.Sort((a, b) =>
                {
                    return perpendicularIntPoint.Dot(a[0]).CompareTo(perpendicularIntPoint.Dot(b[0]));
                });

                // reverse the list if the end is closer to the lastPosition
                var minStart = Math.Min((sorted[0][0] - lastPosition).LengthSquared(), (sorted[0][1] - lastPosition).LengthSquared());
                var minEnd = Math.Min((sorted[count - 1][0] - lastPosition).LengthSquared(), (sorted[count - 1][1] - lastPosition).LengthSquared());
                if (minEnd < minStart)
                {
                    sorted.Reverse();
                    // and make sure we understand the positive direction
                    perpendicularIntPoint *= -1;
                }
            }
        }
    }
}