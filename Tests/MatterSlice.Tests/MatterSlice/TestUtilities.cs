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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using MSClipperLib;
using NUnit.Framework;
using MatterHackers.VectorMath;
using System.Linq;
using System.Reflection;
using Polygon = System.Collections.Generic.List<MSClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<MSClipperLib.IntPoint>>;

namespace MatterHackers.MatterSlice.Tests
{
	public struct MovementInfo
	{
		public string line;
		public double extrusion;
		public double feedRate;
		public MatterHackers.MatterSlice.Vector3 position;
	}

	public static class TestUtilities
	{
		private static string matterSliceBaseDirectory = TestContext.CurrentContext.ResolveProjectPath(4);
		private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");
		private static string tempGCodePath = Path.Combine(matterSliceBaseDirectory, "GCode_Test");

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

		public static int CountRetractions(string[] layer)
		{
			int retractions = 0;
			foreach (string line in layer)
			{
				if (line.StartsWith("G1 "))
				{
					if (line.Contains("E")
						&& !line.Contains("X")
						&& !line.Contains("Y")
						&& !line.Contains("Z"))
					{
						retractions++;
					}
				}
			}

			return retractions;
		}

		public static Polygons GetExtrusionPolygonsForLayer(this string[] layerGCode, long movementToIgnore = 0)
		{
			var movementInfo = default(MovementInfo);
			return GetExtrusionPolygonsForLayer(layerGCode, ref movementInfo, movementToIgnore);
		}

		public static Polygons GetTravelPolygonsForLayer(this string[] layerGCode)
		{
			var movementInfo = default(MovementInfo);
			return GetTravelPolygonsForLayer(layerGCode, ref movementInfo);
		}

		public static Polygons GetExtrusionPolygonsForLayer(this string[] layerGCode, ref MovementInfo movementInfo, long movementToIgnore = 0)
		{
			var foundPolygons = new Polygons();

			bool extruding = false;
			// check that all moves are on the outside of the cylinder (not crossing to a new point)
			int movementCount = 0;
			double movementAmount = double.MaxValue/2; // always add a new extrusion the first time
			MovementInfo lastMovement = movementInfo;
			foreach (MovementInfo currentMovement in TestUtilities.Movements(layerGCode, lastMovement))
			{
				bool isExtrude = currentMovement.extrusion != lastMovement.extrusion;

				if (extruding)
				{
					// add to the extrusion
					foundPolygons[foundPolygons.Count - 1].Add(new IntPoint(
						(long)(currentMovement.position.x * 1000),
						(long)(currentMovement.position.y * 1000),
						(long)(currentMovement.position.z * 1000)));

					if (!isExtrude)
					{
						// we are switching so add in the point to the last extrude
						extruding = false;
						movementAmount = 0;
					}
				}
				else // not extruding
				{
					if (isExtrude)
					{
						if (movementAmount >= movementToIgnore)
						{
							// starting a new extrusion
							foundPolygons.Add(new Polygon());
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

				lastMovement = currentMovement;
				movementCount++;
			}

			for (int i = foundPolygons.Count - 1; i >= 0; i--)
			{
				if (foundPolygons[i].Count == 1)
				{
					foundPolygons.RemoveAt(i);
				}
			}

			movementInfo = lastMovement;
			return foundPolygons;
		}

		public static Polygons GetTravelPolygonsForLayer(this string[] layerGCode, ref MovementInfo movementInfo)
		{
			var foundPolygons = new Polygons();

			bool traveling = false;
			MovementInfo lastMovement = movementInfo;
			foreach (MovementInfo currentMovement in TestUtilities.Movements(layerGCode, lastMovement))
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

		public static string[] GetGCodeForLayer(this string[] gcodeContents, int layerIndex)
		{
			var layerLines = new List<string>();
			int currentLayer = -1;
			foreach (string line in gcodeContents)
			{
				if (line.Contains("LAYER:"))
				{
					currentLayer++;
					if(currentLayer > layerIndex)
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

		public static string GetStlPath(string file)
		{
			return Path.ChangeExtension(Path.Combine(matterSliceBaseDirectory, "SampleSTLs", file), "stl");
		}

		public static string GetControlGCodePath(string testName)
		{
			string directory = Path.Combine(matterSliceBaseDirectory, "GCode_Control");
			Directory.CreateDirectory(directory);

			return Path.Combine(directory, testName + ".gcode");
		}

		public static string GetTempGCodePath(string file)
		{
			string fullPath = Path.ChangeExtension(Path.Combine(matterSliceBaseDirectory, "Tests", "TestData", "Temp", file), "gcode");
			// Make sure the output directory exists
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
			return fullPath;
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
			processor.Finalize();

			return TestUtilities.LoadGCodeFile(thinWallsGCode);
		}

		public static string[] LoadGCodeFile(string gcodeFile)
		{
			return File.ReadAllLines(gcodeFile);
		}

		public static IEnumerable<MovementInfo> Movements(this string[] gcodeContents, Nullable<MovementInfo> startingMovement = null, bool onlyG1s = false)
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

		public static string ResolveProjectPath(this TestContext context, int stepsToProjectRoot, params string[] relativePathSteps)
		{
			string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			var allPathSteps = new List<string> { assemblyPath };
			allPathSteps.AddRange(Enumerable.Repeat("..", stepsToProjectRoot));

			if (relativePathSteps.Any())
			{
				allPathSteps.AddRange(relativePathSteps);
			}

			return Path.GetFullPath(Path.Combine(allPathSteps.ToArray()));
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

		internal static void CheckPolysAreSimilar(string aGCodeFile, string bGCodeFile)
		{
			var aLoadedGcode = TestUtilities.LoadGCodeFile(aGCodeFile);
			var bLoadedGCode = TestUtilities.LoadGCodeFile(bGCodeFile);
			var aLayerCount = TestUtilities.LayerCount(aLoadedGcode);
			Assert.AreEqual(aLayerCount, TestUtilities.LayerCount(bLoadedGCode));
			for (int layerIndex = 0; layerIndex < aLayerCount; layerIndex++)
			{
				var aLayerGCode = TestUtilities.GetGCodeForLayer(aLoadedGcode, layerIndex);
				var bLayerGCode = TestUtilities.GetGCodeForLayer(bLoadedGCode, layerIndex);
				var aPolys = TestUtilities.GetExtrusionPolygonsForLayer(aLayerGCode);
				var bPolys = TestUtilities.GetExtrusionPolygonsForLayer(bLayerGCode);
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

		/// <summary>
		/// Get the extrusion polygons for every layer
		/// </summary>
		/// <param name="loadedGCode">The source gcode separated by line</param>
		/// <returns>A list of all the polygons by layer</returns>
		public static List<Polygons> GetAllExtrusionPolygons(this string[] loadedGCode)
		{
			var layerCount = TestUtilities.LayerCount(loadedGCode);

			var layerPolygons = new List<Polygons>(layerCount);
			for (int i = 0; i < layerCount; i++)
			{
				layerPolygons.Add(TestUtilities.GetExtrusionPolygonsForLayer(loadedGCode.GetGCodeForLayer(i)));
			}

			return layerPolygons;
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
			for (int i = 0; i < layerCount; i++)
			{
				layerPolygons.Add(TestUtilities.GetTravelPolygonsForLayer(loadedGCode.GetGCodeForLayer(i)));
			}

			return layerPolygons;
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
	}
}