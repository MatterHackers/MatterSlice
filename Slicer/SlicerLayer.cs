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
	using System.IO;
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public class ClosePolygonResult
	{
		//The result of trying to find a point on a closed polygon line. This gives back the point index, the polygon index, and the point of the connection.
		//The line on which the point lays is between pointIdx-1 and pointIdx
		public IntPoint intersectionPoint;

		public int pointIdx;
		public int polygonIdx;
	}

	public class GapCloserResult
	{
		public bool AtoB;
		public long len;
		public int pointIndexA;
		public int pointIndexB;
		public int polygonIndex;
	}

	public class MeshProcessingLayer
	{
		public Polygons PolygonList = new Polygons();
		public List<SlicePerimeterSegment> SegmentList = new List<SlicePerimeterSegment>();

		private Polygons openPolygonList = new Polygons();
		private int z;

		public MeshProcessingLayer(int z)
		{
			this.z = z;
		}

		public MeshProcessingLayer(int z, string segmentListData)
		{
			this.z = z;
			SegmentList = CreateSegmentListFromString(segmentListData);
		}

		public int Z { get { return z; } }

		public static List<SlicePerimeterSegment> CreateSegmentListFromString(string segmentListData)
		{
			List<SlicePerimeterSegment> output = new List<SlicePerimeterSegment>();
			string[] segmentData = segmentListData.Split('|');
			foreach (string segment in segmentData)
			{
				if (segment != "")
				{
					List<IntPoint> outPoints = new Polygon();
					string[] points = segment.Split('&');
					foreach (string point in points)
					{
						string[] coordinates = point.Split(',');
						string elementX = coordinates[0];
						string elementY = coordinates[1];
                        int xIndex = elementX.IndexOf("x:");
                        int yIndex = elementY.IndexOf("y:");
						outPoints.Add(new IntPoint(int.Parse(elementX.Substring(xIndex+2)), int.Parse(elementY.Substring(yIndex+2))));
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

		public void DumpPolygonsToGcode(System.IO.StreamWriter stream, double scale, double extrudeAmount)
		{
			for (int openPolygonIndex = 0; openPolygonIndex < openPolygonList.Count; openPolygonIndex++)
			{
				Polygon openPolygon = openPolygonList[openPolygonIndex];

				if (openPolygon.Count > 0)
				{
					// move to the start without extruding (so it is a move)
					stream.Write("G1 X{0}Y{1}\n", (double)(openPolygon[0].X) / scale,
						(double)(openPolygon[0].Y) / scale);
					for (int intPointIndex = 1; intPointIndex < openPolygon.Count; intPointIndex++)
					{
						// do all the points with extruding
						stream.Write("G1 X{0}Y{1}E{2}\n", (double)(openPolygon[intPointIndex].X) / scale,
							(double)(openPolygon[intPointIndex].Y) / scale, extrudeAmount++);
					}
					// go back to the start extruding
					stream.Write("G1 X{0}Y{1}E{2}\n", (double)(openPolygon[0].X) / scale,
						(double)(openPolygon[0].Y) / scale, extrudeAmount++);
				}
			}
		}

		public void MakePolygons()
		{
			if (false) // you can use this output segments for debugging
			{
				using (StreamWriter stream = File.AppendText("segments.txt"))
				{
					stream.WriteLine(DumpSegmentListToString(SegmentList));
				}
			}

			CreateFastIndexLookup();

			for (int startingSegmentIndex = 0; startingSegmentIndex < SegmentList.Count; startingSegmentIndex++)
			{
				if (SegmentList[startingSegmentIndex].hasBeenAddedToPolygon)
				{
					continue;
				}

				Polygon poly = new Polygon();
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

			// Link up all the missing ends, closing up the smallest gaps first. This is an inefficient implementation which can run in O(n*n*n) time.
			while (true)
			{
				long bestScore = 10000 * 10000;
				int bestA = -1;
				int bestB = -1;
				bool reversed = false;
				for (int polygonAIndex = 0; polygonAIndex < openPolygonList.Count; polygonAIndex++)
				{
					if (openPolygonList[polygonAIndex].Count < 1)
					{
						continue;
					}

					for (int polygonBIndex = 0; polygonBIndex < openPolygonList.Count; polygonBIndex++)
					{
						if (openPolygonList[polygonBIndex].Count < 1)
						{
							continue;
						}

						IntPoint diff1 = openPolygonList[polygonAIndex][openPolygonList[polygonAIndex].Count - 1] - openPolygonList[polygonBIndex][0];
						long distSquared1 = (diff1).LengthSquared();
						if (distSquared1 < bestScore)
						{
							bestScore = distSquared1;
							bestA = polygonAIndex;
							bestB = polygonBIndex;
							reversed = false;

							if (bestScore == 0)
							{
								// found a perfect match stop looking
								break;
							}
						}

						if (polygonAIndex != polygonBIndex)
						{
							IntPoint diff2 = openPolygonList[polygonAIndex][openPolygonList[polygonAIndex].Count - 1] - openPolygonList[polygonBIndex][openPolygonList[polygonBIndex].Count - 1];
							long distSquared2 = (diff2).LengthSquared();
							if (distSquared2 < bestScore)
							{
								bestScore = distSquared2;
								bestA = polygonAIndex;
								bestB = polygonBIndex;
								reversed = true;

								if (bestScore == 0)
								{
									// found a perfect match stop looking
									break;
								}
							}
						}
					}

					if (bestScore == 0)
					{
						// found a perfect match stop looking
						break;
					}
				}

				if (bestScore >= 10000 * 10000)
				{
					break;
				}

				if (bestA == bestB) // This loop connects to itself, close the polygon.
				{
					PolygonList.Add(new Polygon(openPolygonList[bestA]));
					openPolygonList[bestA].Clear(); // B is cleared as it is A
				}
				else
				{
					if (reversed)
					{
						if (openPolygonList[bestA].Count > openPolygonList[bestB].Count)
						{
							for (int indexB = openPolygonList[bestB].Count-1; indexB >=0; indexB--)
							{
								openPolygonList[bestA].Add(openPolygonList[bestB][indexB]);
							}
							openPolygonList[bestB].Clear();
						}
						else
						{
							for (int indexA = openPolygonList[bestA].Count - 1; indexA >= 0; indexA--)
							{
								openPolygonList[bestB].Add(openPolygonList[bestA][indexA]);
							}
							openPolygonList[bestA].Clear();
						}
					}
					else
					{
						openPolygonList[bestA].AddRange(openPolygonList[bestB]);
						openPolygonList[bestB].Clear();
					}
				}
			}

			//Remove all the tiny polygons, or polygons that are not closed. As they do not contribute to the actual print.
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

			//Finally optimize all the polygons. Every point removed saves time in the long run.
			double minimumDistanceToCreateNewPosition = 10;
			PolygonList = Clipper.CleanPolygons(PolygonList, minimumDistanceToCreateNewPosition);
		}

		private GapCloserResult FindPolygonGapCloser(IntPoint ip0, IntPoint ip1)
		{
			GapCloserResult ret = new GapCloserResult();
			ClosePolygonResult c1 = FindPolygonPointClosestTo(ip0);
			ClosePolygonResult c2 = FindPolygonPointClosestTo(ip1);
			if (c1.polygonIdx < 0 || c1.polygonIdx != c2.polygonIdx)
			{
				ret.len = -1;
				return ret;
			}
			ret.polygonIndex = c1.polygonIdx;
			ret.pointIndexA = c1.pointIdx;
			ret.pointIndexB = c2.pointIdx;
			ret.AtoB = true;

			if (ret.pointIndexA == ret.pointIndexB)
			{
				//Connection points are on the same line segment.
				ret.len = (ip0 - ip1).Length();
			}
			else
			{
				//Find out if we have should go from A to B or the other way around.
				IntPoint p0 = PolygonList[ret.polygonIndex][ret.pointIndexA];
				long lenA = (p0 - ip0).Length();
				for (int i = ret.pointIndexA; i != ret.pointIndexB; i = (i + 1) % PolygonList[ret.polygonIndex].Count)
				{
					IntPoint p1 = PolygonList[ret.polygonIndex][i];
					lenA += (p0 - p1).Length();
					p0 = p1;
				}
				lenA += (p0 - ip1).Length();

				p0 = PolygonList[ret.polygonIndex][ret.pointIndexB];
				long lenB = (p0 - ip1).Length();
				for (int i = ret.pointIndexB; i != ret.pointIndexA; i = (i + 1) % PolygonList[ret.polygonIndex].Count)
				{
					IntPoint p1 = PolygonList[ret.polygonIndex][i];
					lenB += (p0 - p1).Length();
					p0 = p1;
				}
				lenB += (p0 - ip0).Length();

				if (lenA < lenB)
				{
					ret.AtoB = true;
					ret.len = lenA;
				}
				else
				{
					ret.AtoB = false;
					ret.len = lenB;
				}
			}
			return ret;
		}

		private ClosePolygonResult FindPolygonPointClosestTo(IntPoint input)
		{
			ClosePolygonResult ret = new ClosePolygonResult();
			for (int n = 0; n < PolygonList.Count; n++)
			{
				IntPoint p0 = PolygonList[n][PolygonList[n].Count - 1];
				for (int i = 0; i < PolygonList[n].Count; i++)
				{
					IntPoint p1 = PolygonList[n][i];

					//Q = A + Normal( B - A ) * ((( B - A ) dot ( P - A )) / VSize( A - B ));
					IntPoint pDiff = p1 - p0;
					long lineLength = (pDiff).Length();
					if (lineLength > 1)
					{
						long distOnLine = (pDiff).Dot(input - p0) / lineLength;
						if (distOnLine >= 0 && distOnLine <= lineLength)
						{
							IntPoint q = p0 + pDiff * distOnLine / lineLength;
							if ((q - input).ShorterThen(100))
							{
								ret.intersectionPoint = q;
								ret.polygonIdx = n;
								ret.pointIdx = i;
								return ret;
							}
						}
					}
					p0 = p1;
				}
			}
			ret.polygonIdx = -1;
			return ret;
		}

		Dictionary<long, List<int>> startIndexes = new Dictionary<long, List<int>>();
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

		static readonly bool runLookupTest = false;
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

			if (runLookupTest)
			{
				if (lookupSegmentIndex != searchSegmentIndex)
				{
					int a = 0;
				}
			}

			return lookupSegmentIndex;
		}
	}
}