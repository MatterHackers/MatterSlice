/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.VectorMath;
using MSClipperLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice.Tests
{
	public struct MovementInfo : IEquatable<MovementInfo>
	{
		public double extrusion;

		public double feedRate;

		public string line;

		public Vector3 position;

		public bool Equals(MovementInfo other)
		{
			if (extrusion == other.extrusion
				&& feedRate == other.feedRate
				&& position == other.position)
			{
				return true;
			}

			return false;
		}

		public override string ToString()
		{
			return $"{position} : {extrusion} : {line}";
		}
	}

	public static class TestUtilities
	{
		public static string MatterSliceBaseDirectory = ResolveProjectPath(new string[] { "..", ".." });

		private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");

		private static string tempGCodePath = Path.Combine(MatterSliceBaseDirectory, "GCode_Test");

		

		public static bool CheckForRaft(string[] gcodefile)
		{
			bool hasRaft = false;

			foreach (string line in gcodefile)
			{
				if (line.Contains("RAFT"))
				{
					hasRaft = true;
				}
			}
			return hasRaft;
		}

		public static void ClearTempGCode()
		{
			if (Directory.Exists(tempGCodePath))
			{
				Directory.Delete(tempGCodePath, true);
				while (Directory.Exists(tempGCodePath))
				{
				}
			}
			Directory.CreateDirectory(tempGCodePath);
			while (!Directory.Exists(tempGCodePath))
			{
			}
		}

		public static int CountRetractions(string[] layer)
		{
			int retractions = 0;
			foreach (string line in layer)
			{
				if (line.IsRetraction())
				{
					retractions++;
				}
			}

			return retractions;
		}

		/// <summary>
		/// Get the extrusion polygons for every layer
		/// </summary>
		/// <param name="loadedGCode">The source gcode separated by line</param>
		/// <returns>A list of all the polygons by layer</returns>
		public static List<Polygons> GetAllLayersExtrusionPolygons(this string[] loadedGCode)
		{
			var layerCount = TestUtilities.LayerCount(loadedGCode);

			var layerPolygons = new List<Polygons>(layerCount);
			var movementInfo = default(MovementInfo);
			for (int i = 0; i < layerCount; i++)
			{
				layerPolygons.Add(TestUtilities.GetExtrusionPolygonsForLayer(loadedGCode.GetLayer(i), ref movementInfo, false));
			}

			return layerPolygons;
		}

		public static List<IEnumerable<MovementInfo>> GetAllLayersMovements(this string[] loadedGCode)
		{
			var layerCount = TestUtilities.LayerCount(loadedGCode);

			var layerMovements = new List<IEnumerable<MovementInfo>>(layerCount);
			for (int i = 0; i < layerCount; i++)
			{
				layerMovements.Add(TestUtilities.GetLayerMovements(loadedGCode.GetLayer(i)));
			}

			return layerMovements;
		}

		public static List<string[]> GetAllLayers(this string[] loadedGCode)
		{
			var layerCount = TestUtilities.LayerCount(loadedGCode);

			var gcodeLayers = new List<string[]>(layerCount);
			for (int i = 0; i < layerCount; i++)
			{
				gcodeLayers.Add(loadedGCode.GetLayer(i));
			}

			return gcodeLayers;
		}

		/// <summary>
		/// Get the travel polygons for every layer
		/// </summary>
		/// <param name="loadedGCode">The source gcode separated by line</param>
		/// <returns>A list of all the polygons by layer</returns>
		public static List<Polygons> GetAllTravelPolygons(this string[] loadedGCode)
		{
			var layerCount = TestUtilities.LayerCount(loadedGCode);

			var layerPolygons = new List<Polygons>(layerCount);
			var lastMovement = default(MovementInfo);
			for (int i = 0; i < layerCount; i++)
			{
				layerPolygons.Add(TestUtilities.GetTravelPolygonsForLayer(loadedGCode.GetLayer(i), ref lastMovement));
			}

			return layerPolygons;
		}

		public static string GetControlGCodePath(string testName)
		{
			string directory = Path.Combine(MatterSliceBaseDirectory, "GCode_Control");
			Directory.CreateDirectory(directory);

			return Path.Combine(directory, testName + ".gcode");
		}

		public static Polygons GetExtrusionPolygonsForLayer(this string[] layerGCode, bool validateOverlaps = true)
		{
			var movementInfo = default(MovementInfo);
			return GetExtrusionPolygonsForLayer(layerGCode, ref movementInfo, validateOverlaps);
		}

		public static Polygons GetExtrusionPolygonsForLayer(this string[] layerGCode, ref MovementInfo movementInfo, bool validateOverlaps = true)
		{
			var foundPolygons = new Polygons();

			bool extruding = false;
			int movementCount = 0;
			double movementAmount = double.MaxValue / 2; // always add a new extrusion the first time
			var lastMovement = movementInfo;
			var lastLastMovement = movementInfo;
			foreach (MovementInfo currentMovement in TestUtilities.GetLayerMovements(layerGCode, lastMovement))
			{
				bool isRetraction = currentMovement.extrusion != lastMovement.extrusion && (currentMovement.position == lastMovement.position);
				bool isZLift = currentMovement.position.x == lastMovement.position.x && currentMovement.position.y == lastMovement.position.y && currentMovement.position.z != lastMovement.position.z;
				bool isExtrude = !isRetraction && ! isZLift && currentMovement.extrusion != lastMovement.extrusion;

				if (extruding)
				{
					if (isExtrude)
					{
						// add to the extrusion
						foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
						(long)(currentMovement.position.x * 1000),
						(long)(currentMovement.position.y * 1000),
						(long)(currentMovement.position.z * 1000)));
					}
					else
					{
						// we are switching so add in the point to the last extrude
						extruding = false;
						movementAmount = 0;
						if (foundPolygons[foundPolygons.Count - 1].Count == 1)
						{
							foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
							(long)(lastMovement.position.x * 1000),
							(long)(lastMovement.position.y * 1000),
							(long)(lastMovement.position.z * 1000)));
						}
					}
				}
				else // not extruding
				{
					if (isExtrude)
					{
						if (movementAmount >= 0)
						{
							// starting a new extrusion
							foundPolygons.Add(new Polygon());

							// add in the last position
							foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
								(long)(lastMovement.position.x * 1000),
								(long)(lastMovement.position.y * 1000),
								(long)(lastMovement.position.z * 1000)));
						}

						foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
							(long)(currentMovement.position.x * 1000),
							(long)(currentMovement.position.y * 1000),
							(long)(currentMovement.position.z * 1000)));
						extruding = true;
					}
					else // do nothing waiting for extrude
					{
						movementAmount += (currentMovement.position - lastMovement.position).Length;
					}
				}

				lastLastMovement = lastMovement;
				lastMovement = currentMovement;
				movementCount++;
			}

			for (int i = foundPolygons.Count - 1; i >= 0; i--)
			{
				if (!extruding && foundPolygons[i].Count == 1)
				{
					foundPolygons.RemoveAt(i);
				}
				else if (foundPolygons[foundPolygons.Count - 1].Count == 1)
				{
					foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
						(long)(lastLastMovement.position.x * 1000),
						(long)(lastLastMovement.position.y * 1000),
						(long)(lastLastMovement.position.z * 1000)));
					break;
				}
			}

			movementInfo = lastMovement;

			// validate that the polygons do not double extrude
			if (validateOverlaps)
			{
				Assert.IsFalse(HasOverlapingSegments(foundPolygons));
			}

			return foundPolygons;
		}

		public static bool HasOverlapingSegments(this Polygons polygons)
		{
			var foundSegments = new HashSet<(IntPoint p1, IntPoint p2)>();

			for (var polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
			{
				var polygon = polygons[polygonIndex];
				var first = true;
				var prevPoint = default(IntPoint);
				for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
				{
					var currentPoint = polygon[pointIndex];
					if (first)
					{
						prevPoint = currentPoint;
						first = false;
					}
					else
					{
						var minXYZ = prevPoint;
						var maxXYZ = currentPoint;
						// make sure min is less than max
						if (minXYZ.X > maxXYZ.X
							|| (minXYZ.X == maxXYZ.X && minXYZ.Y > maxXYZ.Y)
							|| (minXYZ.X == maxXYZ.X && minXYZ.Y == maxXYZ.Y && minXYZ.Z > maxXYZ.Z))
						{
							minXYZ = currentPoint;
							maxXYZ = prevPoint;
						}

						if (foundSegments.Contains((minXYZ, maxXYZ)))
						{
							return true;
						}

						foundSegments.Add((minXYZ, maxXYZ));

						prevPoint = currentPoint;
					}
				}
			}

			return false;
		}

		public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref double readValue, int startIndex = 0)
		{
			int stringPos = stringWithNumber.IndexOf(stringToCheckAfter, startIndex);
			if (stringPos != -1)
			{
				stringPos += stringToCheckAfter.Length;
				readValue = GetNextNumber(stringWithNumber, ref stringPos);

				return true;
			}

			return false;
		}

		public static string[] GetLayer(this string[] gcodeContents, int layerIndex)
		{
			var layerLines = new List<string>();
			int currentLayer = -1;
			foreach (string line in gcodeContents)
			{
				if (line.Contains("LAYER:"))
				{
					currentLayer++;
					if (currentLayer > layerIndex)
					{
						break;
					}
				}

				if (currentLayer == layerIndex)
				{
					layerLines.Add(line);
				}
			}

			return layerLines.ToArray();
		}

		/// <summary>
		/// Get the count of every extrusion at a given angle
		/// </summary>
		/// <param name="islands">The polygons to consider</param>
		/// <returns>A list of the angle and the count of lines at that angle</returns>
		public static Dictionary<double, int> GetLineAngles(Polygons polygons)
		{
			var angleCount = new Dictionary<double, int>();

			foreach (var polygon in polygons)
			{
				for (int i = 0; i < polygon.Count - 1; i++)
				{
					var start = new Vector2(polygon[i].X, polygon[i].Y);
					var end = new Vector2(polygon[i + 1].X, polygon[i + 1].Y);
					var angle1 = (int)((end - start).GetAngle0To2PI() * 360 / (2 * Math.PI));
					var angle2 = (int)((start - end).GetAngle0To2PI() * 360 / (2 * Math.PI));
					var angle = Math.Min(angle1, angle2);
					var count = 0;
					if (angleCount.ContainsKey(angle))
					{
						count = angleCount[angle];
					}

					angleCount[angle] = count + 1;
				}
			}

			return angleCount;
		}

		public static string GetStlPath(string file)
		{
			return Path.ChangeExtension(Path.Combine(MatterSliceBaseDirectory, "SampleSTLs", file), "stl");
		}

		public static string GetTempGCodePath(string file)
		{
			string fullPath = Path.ChangeExtension(Path.Combine(MatterSliceBaseDirectory, "Tests", "TestData", "Temp", file), "gcode");
			// Make sure the output directory exists
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
			return fullPath;
		}

		public static Polygons GetTravelPolygonsForLayer(this string[] layerGCode, ref MovementInfo movementInfo)
		{
			var foundPolygons = new Polygons();

			bool traveling = false;
			MovementInfo lastMovement = movementInfo;
			foreach (MovementInfo currentMovement in TestUtilities.GetLayerMovements(layerGCode, lastMovement))
			{
				bool isTravel = currentMovement.extrusion == lastMovement.extrusion;

				if (traveling)
				{
					if (isTravel)
					{
						// add to the travel
						foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
							(long)(currentMovement.position.x * 1000),
							(long)(currentMovement.position.y * 1000),
							(long)(currentMovement.position.z * 1000)));
					}
					else
					{
						traveling = false;
					}
				}
				else // not traveling
				{
					if (isTravel)
					{
						// starting a new travel
						foundPolygons.Add(new Polygon());

						if (lastMovement.position != currentMovement.position)
						{
							foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
								(long)(lastMovement.position.x * 1000),
								(long)(lastMovement.position.y * 1000),
								(long)(lastMovement.position.z * 1000)));
						}

						foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
							(long)(currentMovement.position.x * 1000),
							(long)(currentMovement.position.y * 1000),
							(long)(currentMovement.position.z * 1000)));

						traveling = true;
					}
				}

				lastMovement = currentMovement;
			}

			for (int i = foundPolygons.Count - 1; i >= 0; i--)
			{
				if (foundPolygons[i].PolygonLength() == 0)
				{
					foundPolygons.RemoveAt(i);
				}
			}

			movementInfo = lastMovement;
			return foundPolygons;
		}

		public static bool IsRetraction(this string line)
		{
			if (line.StartsWith("G1 "))
			{
				if (line.Contains("E")
					&& !line.Contains("X")
					&& !line.Contains("Y")
					&& !line.Contains("Z"))
				{
					return true;
				}
			}

			return false;
		}

		public static int LayerCount(this string[] gcodeContents)
		{
			int layers = 0;
			int layerCount = 0;
			foreach (string line in gcodeContents)
			{
				if (line.Contains("Layer count"))
				{
					layerCount = int.Parse(line.Split(':')[1]);
				}

				if (line.Contains("LAYER:"))
				{
					layers++;
				}
			}

			if (layerCount != layers)
			{
				throw new Exception("The reported layers and counted layers should be the same.");
			}

			return layers;
		}

		public static string[] LoadGCodeFile(string gcodeFile)
		{
			return File.ReadAllLines(gcodeFile);
		}

		public static int FindMoveIndex(this IEnumerable<MovementInfo> moves, Vector3 position)
		{
			var index = 0;
			position /= 1000.0;
			foreach (var move in moves)
			{
				if (move.position == position)
				{
					return index;
				}

				index++;
			}

			return -1;
		}

		public static IEnumerable<MovementInfo> GetLayerMovements(this string[] gcodeContents, Nullable<MovementInfo> startingMovement = null, bool onlyG1s = false)
		{
			var currentPosition = default(MovementInfo);
			if (startingMovement != null)
			{
				currentPosition = startingMovement.Value;
			}

			foreach (string inLine in gcodeContents)
			{
				string line = inLine;
				currentPosition.line = line;
				// make sure we don't parse comments
				if (line.Contains(";"))
				{
					line = line.Split(';')[0];
				}

				if ((!onlyG1s && line.StartsWith("G0 "))
					|| line.StartsWith("G1 "))
				{
					GetFirstNumberAfter("X", line, ref currentPosition.position.x);
					GetFirstNumberAfter("Y", line, ref currentPosition.position.y);
					GetFirstNumberAfter("Z", line, ref currentPosition.position.z);
					GetFirstNumberAfter("E", line, ref currentPosition.extrusion);
					GetFirstNumberAfter("F", line, ref currentPosition.feedRate);

					yield return currentPosition;
				}
			}
		}

		public enum PolygonTypes
		{
			Unknown,
			Travel,
			Retraction,
			Extrusion
		}

		public static List<(Polygon polygon, PolygonTypes type)> GetLayerPolygons(IEnumerable<MovementInfo> layerMovements, ref MovementInfo lastMovement)
		{
			var layerPolygons = new List<(Polygon polygon, PolygonTypes type)>();
			Polygon currentPolygon = null;
			var lastPolygonType = PolygonTypes.Unknown;
			foreach (var movement in layerMovements)
			{
				PolygonTypes segmentType = PolygonTypes.Unknown;
				// if the next movement is changed
				if (!movement.Equals(lastMovement))
				{
					// figure out what type of movement it is
					if (movement.extrusion != lastMovement.extrusion)
					{
						// a retraction or an extrusion
						if (lastMovement.position.x == movement.position.x
							&& lastMovement.position.y == movement.position.y)
						{
							// retraction
							segmentType = PolygonTypes.Retraction;
						}
						else
						{
							// extrusion
							segmentType = PolygonTypes.Extrusion;
						}
					}
					else
					{
						// a travel
						segmentType = PolygonTypes.Travel;
					}

					// if we have a change in movement type add a polygon
					if (segmentType != lastPolygonType)
					{
						currentPolygon = new Polygon();
						layerPolygons.Add((currentPolygon, segmentType));
						lastPolygonType = segmentType;
						currentPolygon.Add(new IntPoint(lastMovement.position.x * 1000, lastMovement.position.y * 1000, lastMovement.position.z * 1000)
						{
							Speed = (long)movement.feedRate
						});
					}

					// and add to the current polygon
					currentPolygon.Add(new IntPoint(movement.position.x * 1000, movement.position.y * 1000, movement.position.z * 1000)
					{
						Speed = (long)movement.feedRate
					});
				}

				lastMovement = movement;
			}

			return layerPolygons;
		}

		private static string ResolveProjectPath(string[] relativePathPieces, [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null)
		{
			return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath), "..", Path.Combine(relativePathPieces)));
		}

		public static string[] SliceAndGetGCode(string stlName, Action<ConfigSettings> action = null)
		{
			string thinWallsSTL = TestUtilities.GetStlPath($"{stlName}.stl");
			string thinWallsGCode = TestUtilities.GetTempGCodePath($"{stlName}.gcode");

			var config = new ConfigSettings();

			action?.Invoke(config);

			var processor = new FffProcessor(config);
			processor.SetTargetFile(thinWallsGCode);
			processor.LoadStlFile(thinWallsSTL);
			// slice and save it
			processor.DoProcessing();
			processor.Dispose();

			return TestUtilities.LoadGCodeFile(thinWallsGCode);
		}

		internal static void CheckPolysAreSimilar(string aGCodeFile, string bGCodeFile)
		{
			var aLoadedGcode = TestUtilities.LoadGCodeFile(aGCodeFile);
			var bLoadedGCode = TestUtilities.LoadGCodeFile(bGCodeFile);
			var aLayerCount = TestUtilities.LayerCount(aLoadedGcode);
			Assert.AreEqual(aLayerCount, TestUtilities.LayerCount(bLoadedGCode));
			for (int layerIndex = 0; layerIndex < aLayerCount; layerIndex++)
			{
				var aLayerGCode = TestUtilities.GetLayer(aLoadedGcode, layerIndex);
				var bLayerGCode = TestUtilities.GetLayer(bLoadedGCode, layerIndex);
				var aPolys = TestUtilities.GetExtrusionPolygonsForLayer(aLayerGCode, false);
				var bPolys = TestUtilities.GetExtrusionPolygonsForLayer(bLayerGCode, false);
				// Assert.AreEqual(aPolys.Count, bPolys.Count);
				if (aPolys.Count > 0)
				{
					var aPoly = aPolys[0];
					var bPoly = bPolys[0];
					for (int aPointIndex = 0; aPointIndex < aPoly.Count; aPointIndex++)
					{
						var found = false;
						for (int bPointIndex = 0; bPointIndex < bPoly.Count; bPointIndex++)
						{
							if ((aPoly[aPointIndex] - bPoly[bPointIndex]).Length() < 10)
							{
								found = true;
								break;
							}
						}

						Assert.IsTrue(found);
					}
				}
			}
		}

		internal static bool UsesExtruder(string[] gcodeContent, int extruderIndex)
		{
			string startToCheckFor = "T{0}".FormatWith(extruderIndex);
			foreach (string line in gcodeContent)
			{
				if (line.StartsWith(startToCheckFor))
				{
					return true;
				}
			}

			return false;
		}

		private static double GetNextNumber(String source, ref int startIndex)
		{
			Match numberMatch = numberRegex.Match(source, startIndex);
			String returnString = numberMatch.Value;
			startIndex = numberMatch.Index + numberMatch.Length;
			double returnVal;
			double.TryParse(returnString, NumberStyles.Number, CultureInfo.InvariantCulture, out returnVal);
			return returnVal;
		}
	}
}