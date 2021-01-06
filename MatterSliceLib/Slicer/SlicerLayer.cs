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
using System.IO;
using MSClipperLib;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice
{
	public class MeshProcessingLayer
	{
		public Polygons PolygonList = new Polygons();
		public List<SlicePerimeterSegment> SegmentList = new List<SlicePerimeterSegment>();

		private static readonly bool runLookupTest = false;
		private Polygons openPolygonList = new Polygons();
		private Dictionary<long, List<int>> startIndexes = new Dictionary<long, List<int>>();
		private readonly long z;

		public MeshProcessingLayer(long z)
		{
			this.z = z;
		}

		public MeshProcessingLayer(int z, string segmentListData)
		{
			this.z = z;
			SegmentList = CreateSegmentListFromString(segmentListData);
		}

		public long Z { get { return z; } }

		public static List<SlicePerimeterSegment> CreateSegmentListFromString(string segmentListData)
		{
			var output = new List<SlicePerimeterSegment>();
			string[] segmentData = segmentListData.Split('|');
			foreach (string segment in segmentData)
			{
				if (segment != "")
				{
					var outPoints = new Polygon();
					string[] points = segment.Split('&');
					foreach (string point in points)
					{
						string[] coordinates = point.Split(',');
						string elementX = coordinates[0];
						string elementY = coordinates[1];
						int xIndex = elementX.IndexOf("x:");
						int yIndex = elementY.IndexOf("y:");
						outPoints.Add(new IntPoint(int.Parse(elementX.Substring(xIndex + 2)), int.Parse(elementY.Substring(yIndex + 2))));
					}

					output.Add(new SlicePerimeterSegment(outPoints[0], outPoints[1]));
				}
			}

			return output;
		}

		public static string DumpSegmentListToString(List<SlicePerimeterSegment> segmentList)
		{
			string total = "";
			foreach (SlicePerimeterSegment point in segmentList)
			{
				total += point.start.ToString() + "&";
				total += point.end.ToString() + "|";
			}

			return total;
		}

		public void DumpPolygonsToGcode(StreamWriter stream, double scale, double extrudeAmount)
		{
			for (int openPolygonIndex = 0; openPolygonIndex < openPolygonList.Count; openPolygonIndex++)
			{
				Polygon openPolygon = openPolygonList[openPolygonIndex];

				if (openPolygon.Count > 0)
				{
					// move to the start without extruding (so it is a move)
					stream.Write("G1 X{0}Y{1}\n", (double)openPolygon[0].X / scale,
						(double)openPolygon[0].Y / scale);
					for (int intPointIndex = 1; intPointIndex < openPolygon.Count; intPointIndex++)
					{
						// do all the points with extruding
						stream.Write("G1 X{0}Y{1}E{2}\n", (double)openPolygon[intPointIndex].X / scale,
							(double)openPolygon[intPointIndex].Y / scale, extrudeAmount++);
					}

					// go back to the start extruding
					stream.Write("G1 X{0}Y{1}E{2}\n", (double)openPolygon[0].X / scale,
						(double)openPolygon[0].Y / scale, extrudeAmount++);
				}
			}
		}

		public void MakePolygons()
		{
#if false // you can use this output segments for debugging
			using (StreamWriter stream = File.AppendText("segments.txt"))
			{
				stream.WriteLine(DumpSegmentListToString(SegmentList));
			}
#endif

			CreateFastIndexLookup();

			for (int startingSegmentIndex = 0; startingSegmentIndex < SegmentList.Count; startingSegmentIndex++)
			{
				if (SegmentList[startingSegmentIndex].hasBeenAddedToPolygon)
				{
					continue;
				}

				var poly = new Polygon();
				// We start by adding the start, as we will add ends from now on.
				IntPoint polygonStartPosition = SegmentList[startingSegmentIndex].start;
				poly.Add(polygonStartPosition);

				int segmentIndexBeingAdded = startingSegmentIndex;
				bool canClose;

				while (true)
				{
					canClose = false;
					SegmentList[segmentIndexBeingAdded].hasBeenAddedToPolygon = true;
					IntPoint addedSegmentEndPoint = SegmentList[segmentIndexBeingAdded].end;

					poly.Add(addedSegmentEndPoint);
					segmentIndexBeingAdded = GetTouchingSegmentIndex(addedSegmentEndPoint);
					if (segmentIndexBeingAdded == -1)
					{
						// if we have looped back around to where we started
						if (addedSegmentEndPoint == polygonStartPosition)
						{
							canClose = true;
						}

						break;
					}
					else
					{
						IntPoint foundSegmentStart = SegmentList[segmentIndexBeingAdded].start;
						if (addedSegmentEndPoint == foundSegmentStart)
						{
							// if we have looped back around to where we started
							if (addedSegmentEndPoint == polygonStartPosition)
							{
								canClose = true;
							}
						}
					}
				}

				if (canClose)
				{
					PolygonList.Add(poly);
				}
				else
				{
					openPolygonList.Add(new Polygon(poly));
				}
			}

			// Remove all polygons from the open polygon list that have 0 points
			for (int i = openPolygonList.Count - 1; i >= 0; i--)
			{
				// add in the position of the last point
				if (openPolygonList[i].Count == 0)
				{
					openPolygonList.RemoveAt(i);
				}
				else // check if every point is the same
				{
					bool allSame = true;
					var first = openPolygonList[i][0];
					for (int j = 1; j < openPolygonList[i].Count; j++)
					{
						if (openPolygonList[i][j] != first)
						{
							allSame = false;
							break;
						}
					}

					if (allSame)
					{
						openPolygonList.RemoveAt(i);
					}
				}
			}

			var startSorter = new SortedIntPoint();
			for (int i = 0; i < openPolygonList.Count; i++)
			{
				startSorter.Add(i, openPolygonList[i][0]);
			}

			startSorter.Sort();

			var endSorter = new SortedIntPoint();
			for (int i = 0; i < openPolygonList.Count; i++)
			{
				endSorter.Add(i, openPolygonList[i][openPolygonList[i].Count - 1]);
			}

			endSorter.Sort();

			// Link up all the missing ends, closing up the smallest gaps first. This is an inefficient implementation which can run in O(n*n*n) time.
			while (true)
			{
				double bestScore = double.MaxValue;
				int bestA = -1;
				int bestB = -1;
				bool reversed = false;
				for (int polygonAIndex = 0; polygonAIndex < openPolygonList.Count; polygonAIndex++)
				{
					if (openPolygonList[polygonAIndex].Count < 1)
					{
						continue;
					}

					var aEndPosition = openPolygonList[polygonAIndex][openPolygonList[polygonAIndex].Count - 1];
					// find the closestStartFromEnd
					int bStartIndex = startSorter.FindClosetIndex(aEndPosition, out double distanceToStartSqrd);
					if (distanceToStartSqrd < bestScore)
					{
						bestScore = distanceToStartSqrd;
						bestA = polygonAIndex;
						bestB = bStartIndex;
						reversed = false;

						if (bestScore == 0)
						{
							// found a perfect match stop looking
							break;
						}
					}

					// find the closestStartFromStart
					int bEndIndex = endSorter.FindClosetIndex(aEndPosition, out double distanceToEndSqrd, polygonAIndex);
					if (distanceToEndSqrd < bestScore)
					{
						bestScore = distanceToEndSqrd;
						bestA = polygonAIndex;
						bestB = bEndIndex;
						reversed = true;

						if (bestScore == 0)
						{
							// found a perfect match stop looking
							break;
						}
					}

					if (bestScore == 0)
					{
						// found a perfect match stop looking
						break;
					}
				}

				if (bestScore >= double.MaxValue)
				{
					// we could not find any points to connect this to
					break;
				}

				if (bestA == bestB) // This loop connects to itself, close the polygon.
				{
					PolygonList.Add(new Polygon(openPolygonList[bestA]));
					openPolygonList[bestA].Clear(); // B is cleared as it is A
					endSorter.Remove(bestA);
					startSorter.Remove(bestA);
				}
				else
				{
					if (reversed)
					{
						if (openPolygonList[bestA].Count > openPolygonList[bestB].Count)
						{
							for (int indexB = openPolygonList[bestB].Count - 1; indexB >= 0; indexB--)
							{
								openPolygonList[bestA].Add(openPolygonList[bestB][indexB]);
							}

							openPolygonList[bestB].Clear();
							endSorter.Remove(bestB);
							startSorter.Remove(bestB);
						}
						else
						{
							for (int indexA = openPolygonList[bestA].Count - 1; indexA >= 0; indexA--)
							{
								openPolygonList[bestB].Add(openPolygonList[bestA][indexA]);
							}

							openPolygonList[bestA].Clear();
							endSorter.Remove(bestA);
							startSorter.Remove(bestA);
						}
					}
					else
					{
						openPolygonList[bestA].AddRange(openPolygonList[bestB]);
						openPolygonList[bestB].Clear();
						endSorter.Remove(bestB);
						startSorter.Remove(bestB);
					}
				}
			}

			// Remove all the tiny polygons, or polygons that are not closed. As they do not contribute to the actual print.
			int minimumPerimeter = 1000;
			for (int polygonIndex = 0; polygonIndex < PolygonList.Count; polygonIndex++)
			{
				long perimeterLength = 0;

				for (int intPointIndex = 1; intPointIndex < PolygonList[polygonIndex].Count; intPointIndex++)
				{
					perimeterLength += (PolygonList[polygonIndex][intPointIndex] - PolygonList[polygonIndex][intPointIndex - 1]).Length();
					if (perimeterLength > minimumPerimeter)
					{
						break;
					}
				}

				if (perimeterLength < minimumPerimeter)
				{
					PolygonList.RemoveAt(polygonIndex);
					polygonIndex--;
				}
			}

			// Finally optimize all the polygons. Every point removed saves time in the long run.
			double minimumDistanceToCreateNewPosition = 10;
			PolygonList = Clipper.CleanPolygons(PolygonList, minimumDistanceToCreateNewPosition);
		}

		public void ReleaseMemory()
		{
			SegmentList = null;

			openPolygonList = null;
			startIndexes = null;
		}

		private void CreateFastIndexLookup()
		{
			for (int startingSegmentIndex = 0; startingSegmentIndex < SegmentList.Count; startingSegmentIndex++)
			{
				long positionKey = GetPositionKey(SegmentList[startingSegmentIndex].start);
				if (!startIndexes.ContainsKey(positionKey))
				{
					startIndexes.Add(positionKey, new List<int>());
				}

				startIndexes[positionKey].Add(startingSegmentIndex);
			}
		}

		private long GetPositionKey(IntPoint intPoint)
		{
			return intPoint.X + (intPoint.Y << 31);
		}

		private int GetTouchingSegmentIndex(IntPoint addedSegmentEndPoint)
		{
			int searchSegmentIndex = -1;
			if (runLookupTest)
			{
				for (int segmentIndex = 0; segmentIndex < SegmentList.Count; segmentIndex++)
				{
					if (!SegmentList[segmentIndex].hasBeenAddedToPolygon)
					{
						if (SegmentList[segmentIndex].start == addedSegmentEndPoint)
						{
							searchSegmentIndex = segmentIndex;
						}
					}
				}
			}

			int lookupSegmentIndex = -1;
			long positionKey = GetPositionKey(addedSegmentEndPoint);
			if (startIndexes.ContainsKey(positionKey))
			{
				foreach (int index in startIndexes[positionKey])
				{
					if (!SegmentList[index].hasBeenAddedToPolygon)
					{
						if (SegmentList[index].start == addedSegmentEndPoint)
						{
							lookupSegmentIndex = index;
						}
					}
				}
			}

			return lookupSegmentIndex;
		}
	}
}