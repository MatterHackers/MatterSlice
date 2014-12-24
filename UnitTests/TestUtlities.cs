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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterSlice.ClipperLib;
using MatterHackers.MatterSlice;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
    public class TestUtlities
    {
        static string tempGCodePath = "../../GCode_Test";
        public static string GetStlPath(string file)
        {
            return Path.ChangeExtension(Path.Combine("../../SampleSTLs", file), "stl");
        }

        public static string GetTempGCodePath(string file)
        {
            return Path.ChangeExtension(Path.Combine(tempGCodePath, file), "gcode");
        }

        public static string GetControlGCodePath(string file)
        {
            string fileAndPath = Path.ChangeExtension(Path.Combine("../../GCode_Control", file), "gcode");
            return fileAndPath;
        }

        public static string GetTestGCodePath(string file)
        {
            string fileAndPath = Path.ChangeExtension(Path.Combine(tempGCodePath, file), "gocde");
            return fileAndPath;
        }

        public static string[] LoadGCodeFile(string gcodeFile)
        {
            return File.ReadAllLines(gcodeFile);
        }

        public static int CountLayers(string[] gcodeContents)
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

        public static string[] GetGCodeForLayer(string[] gcodeContents, int layerIndex)
        {
            List<string> layerLines = new List<string>();
            int currentLayer = -1;
            foreach (string line in gcodeContents)
            {
                if (line.Contains("LAYER:"))
                {
                    currentLayer++;
                }

                if (currentLayer == layerIndex)
                {
                    layerLines.Add(line);
                }
            }

            return layerLines.ToArray();
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
    }
}
