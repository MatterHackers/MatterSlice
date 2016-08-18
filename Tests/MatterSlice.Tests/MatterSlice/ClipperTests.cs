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

using MSClipperLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterSlice.Tests
{
	[TestFixture, Category("MatterSlice.ClipperTests")]
	public class ClipperTests
	{
		[Test]
		public void CleanPolygons()
		{
			// remove a single point that is going to be coincident
			{
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				testPath.Add(new IntPoint(5, 0));
				testPath.Add(new IntPoint(11, 0));
				testPath.Add(new IntPoint(5, 20));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, 10);
				Assert.IsTrue(cleanedPath.Count == 3);
			}

			// don't remove a non collinear point
			{
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				testPath.Add(new IntPoint(50, 5));
				testPath.Add(new IntPoint(100, 0));
				testPath.Add(new IntPoint(50, 200));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, 4);
				Assert.IsTrue(cleanedPath.Count == 4);
			}

			// now remove that point with a higher tolerance
			{
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				testPath.Add(new IntPoint(50, 5));
				testPath.Add(new IntPoint(100, 0));
				testPath.Add(new IntPoint(50, 200));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, 6);
				Assert.IsTrue(cleanedPath.Count == 3);
			}

			// now remove a bunch of points
			{
				int mergeDist = 10;
				List<IntPoint> testPath = new List<IntPoint>();
				testPath.Add(new IntPoint(0, 0));
				Random randY = new Random(0);
				for (int i = 2; i < 58; i++)
				//	for (int i = 2; i < 98; i++)
				{
					testPath.Add(new IntPoint(i, (int)(randY.NextDouble() * mergeDist - mergeDist / 2)));
				}
				testPath.Add(new IntPoint(100, 0));
				testPath.Add(new IntPoint(50, 200));

				List<IntPoint> cleanedPath = Clipper.CleanPolygon(testPath, mergeDist);
				Assert.IsTrue(cleanedPath.Count == 3);
				//Assert.IsTrue(cleanedPath.Contains(new IntPoint(0, 0)));
				//Assert.IsTrue(cleanedPath.Contains(new IntPoint(100, 0)));
				//Assert.IsTrue(cleanedPath.Contains(new IntPoint(50, 200)));
			}
		}
	}
}