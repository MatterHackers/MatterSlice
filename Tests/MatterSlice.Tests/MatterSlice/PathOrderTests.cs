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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Pathfinding;
using MatterHackers.QuadTree;
using MSClipperLib;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
	using Polygon = List<IntPoint>;

	[TestFixture, Category("MatterSlice.PathOrderTests")]
	public class PathOrderTests
	{
		[Test]
		public void CorrectPolygonOrder()
		{
			//                  |
			//     2       1    |    1       2
			//                  |
			//                  |
			//     3       0    |    0       3
			// _________________|__________________
			//                  |
			//     3       0    |    0       3
			//                  |
			//                  |
			//     2       1    |    1       2
			//                  |

			string polyQ1String = "x:5, y:5,x:5, y:10,x:10, y:10,x:10, y:5,|";
			var polyQ1 = CLPolygonsExtensions.CreateFromString(polyQ1String)[0];

			string polyQ2String = "x:-5, y:5,x:-5, y:10,x:-10, y:10,x:-10, y:5,|";
			var polyQ2 = CLPolygonsExtensions.CreateFromString(polyQ2String)[0];

			string polyQ3String = "x:-5, y:-5,x:-5, y:-10,x:-10, y:-10,x:-10, y:-5,|";
			var polyQ3 = CLPolygonsExtensions.CreateFromString(polyQ3String)[0];

			string polyQ4String = "x:5, y:-5,x:5, y:-10,x:10, y:-10,x:10, y:-5,|";
			var polyQ4 = CLPolygonsExtensions.CreateFromString(polyQ4String)[0];

			var settings = new ConfigSettings();

			// test simple finding
			{
				var pPathOrderOptimizer = new PathOrderOptimizer(settings);
				pPathOrderOptimizer.AddPolygon(polyQ1, 0);
				pPathOrderOptimizer.AddPolygon(polyQ2, 1);

				// starting at low far right
				pPathOrderOptimizer.Optimize(new IntPoint(20, 0), null, 0, false, null);
				Assert.AreEqual(2, pPathOrderOptimizer.OptimizedPaths.Count);
				Assert.AreEqual(0, pPathOrderOptimizer.OptimizedPaths[0].SourcePolyIndex);
				Assert.AreEqual(3, pPathOrderOptimizer.OptimizedPaths[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[1].SourcePolyIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.OptimizedPaths[1].PointIndex);

				// starting at high far right
				pPathOrderOptimizer.Optimize(new IntPoint(20, 20), null, 0, false, null);
				Assert.AreEqual(2, pPathOrderOptimizer.OptimizedPaths.Count);
				Assert.AreEqual(0, pPathOrderOptimizer.OptimizedPaths[0].SourcePolyIndex);
				Assert.AreEqual(2, pPathOrderOptimizer.OptimizedPaths[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[1].SourcePolyIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[1].PointIndex);

				// starting at high far left
				pPathOrderOptimizer.Optimize(new IntPoint(-20, 20), null, 0, false, null);
				Assert.AreEqual(2, pPathOrderOptimizer.OptimizedPaths.Count);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[0].SourcePolyIndex);
				Assert.AreEqual(2, pPathOrderOptimizer.OptimizedPaths[0].PointIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.OptimizedPaths[1].SourcePolyIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[1].PointIndex);
			}

			// test that single lines connect correctly
			{
				var pPathOrderOptimizer = new PathOrderOptimizer(settings);
				pPathOrderOptimizer.AddPolygons(
					CLPolygonsExtensions.CreateFromString(
						"x:0, y:0,x:500, y:0,|x:0, y:100,x:500, y:100,|x:0, y:200,x:500, y:200,|x:0, y:300,x:500, y:300,|"));

				// starting at low far right
				pPathOrderOptimizer.Optimize(new IntPoint(0, 0), null, 0, false, null);
				Assert.AreEqual(4, pPathOrderOptimizer.OptimizedPaths.Count);
				Assert.AreEqual(0, pPathOrderOptimizer.OptimizedPaths[0].SourcePolyIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[1].SourcePolyIndex);
				Assert.AreEqual(2, pPathOrderOptimizer.OptimizedPaths[2].SourcePolyIndex);
				Assert.AreEqual(3, pPathOrderOptimizer.OptimizedPaths[3].SourcePolyIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.OptimizedPaths[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[1].PointIndex);
				Assert.AreEqual(0, pPathOrderOptimizer.OptimizedPaths[0].PointIndex);
				Assert.AreEqual(1, pPathOrderOptimizer.OptimizedPaths[1].PointIndex);
			}
		}

		public static (Graphics2D graphics, Affine transform) ImageWithPolygonOutline(Polygon polygon, int width)
		{
			var polygonBounds = polygon.GetBounds();

			var height = (int)Math.Round((double)width / polygonBounds.Width() * polygonBounds.Height());

			var image = new ImageBuffer(width + 4, height + 4);

			// Set the transform to image space
			var polygonsToImageTransform = Affine.NewIdentity();
			// move it to 0, 0
			polygonsToImageTransform *= Affine.NewTranslation(-polygonBounds.minX, -polygonBounds.minY);
			// scale to fit cache
			polygonsToImageTransform *= Affine.NewScaling(width / (double)polygonBounds.Width(), height / (double)polygonBounds.Height());
			// and move it in 2 pixels
			polygonsToImageTransform *= Affine.NewTranslation(2, 2);

			// and render the polygon to the image
			var graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			var vertices = PathingData.CreatePathStorage(new List<Polygon>() { polygon });
			graphics.Render(new VertexSourceApplyTransform(new Stroke(vertices, 100), polygonsToImageTransform), Color.Black);
			return (graphics, polygonsToImageTransform);
		}

		public static void SavePolygonToImage(Polygon polygon, string fileName, int width, Action<Graphics2D, Affine> render)
		{
			var graphicsAndTransform = ImageWithPolygonOutline(polygon, width);
			render?.Invoke(graphicsAndTransform.graphics, graphicsAndTransform.transform);
			// ImageIO.SaveImageData(fileName, graphicsAndTransform.graphics.DestImage);
		}

		[Test]
		public void SeemOnPolygon()
		{
			return;
			var polygon1String = "x:-9.85, y:-11.85,x:-9.64, y:-11.82,x:-9.16, y:-11.62,x:-8.9, y:-11.53,x:-8.17, y:-11.51,x:-7.89, y:-11.46,x:-7.7, y:-11.38,x:-7.48, y:-11.2,x:-7.27, y:-11.07,x:-7.04, y:-11.02,x:-1.18, y:-10.97,x:6.86, y:-11.01,x:7.13, y:-11.03,x:7.37, y:-11.11,x:7.66, y:-11.35,x:7.85, y:-11.45,x:8.08, y:-11.5,x:8.91, y:-11.53,x:9.17, y:-11.62,x:9.64, y:-11.82,x:9.85, y:-11.85,x:10, y:-11.82,x:10.15, y:-11.74,x:10.25, y:-11.66,x:10.31, y:-11.56,x:10.62, y:-10.95,x:10.79, y:-10.75,x:12.01, y:-9.86,x:12.48, y:-9.43,x:12.91, y:-8.97,x:13.29, y:-8.49,x:13.6, y:-8.03,x:13.92, y:-7.45,x:14.24, y:-6.75,x:14.44, y:-6.14,x:14.6, y:-5.49,x:14.71, y:-4.84,x:14.76, y:-4.18,x:14.76, y:-3.61,x:14.71, y:-2.94,x:14.58, y:-2.19,x:14.4, y:-1.53,x:14.16, y:-0.88,x:13.88, y:-0.26,x:13.46, y:0.44,x:13.05, y:1,x:12.62, y:1.49,x:12.14, y:1.94,x:11.63, y:2.35,x:11.08, y:2.71,x:10.5, y:3.03,x:9.89, y:3.29,x:9.18, y:3.54,x:9.04, y:3.78,x:8.76, y:4.2,x:8.36, y:4.44,x:7.79, y:4.7,x:7.2, y:4.9,x:6.68, y:5.01,x:6.16, y:5.05,x:-5.99, y:5.06,x:-6.39, y:5.04,x:-6.84, y:4.98,x:-7.21, y:4.9,x:-7.66, y:4.75,x:-8.09, y:4.57,x:-8.75, y:4.2,x:-8.87, y:4.04,x:-9.17, y:3.53,x:-9.5, y:3.43,x:-10.2, y:3.16,x:-10.79, y:2.88,x:-11.35, y:2.54,x:-11.89, y:2.15,x:-12.38, y:1.72,x:-12.84, y:1.24,x:-13.23, y:0.76,x:-13.57, y:0.26,x:-13.9, y:-0.32,x:-14.2, y:-0.98,x:-14.42, y:-1.61,x:-14.59, y:-2.27,x:-14.7, y:-2.93,x:-14.75, y:-3.61,x:-14.75, y:-4.18,x:-14.7, y:-4.84,x:-14.57, y:-5.61,x:-14.4, y:-6.26,x:-14.16, y:-6.91,x:-13.86, y:-7.55,x:-13.5, y:-8.18,x:-13.11, y:-8.71,x:-12.69, y:-9.21,x:-12.24, y:-9.65,x:-11.75, y:-10.06,x:-10.78, y:-10.76,x:-10.6, y:-10.96,x:-10.32, y:-11.55,|";
			var polygon1 = CLPolygonsExtensions.CreateFromString(polygon1String, 1000)[0];
			var index = polygon1.FindGreatestTurnIndex(400, 0, SEAM_PLACEMENT.FURTHEST_BACK, default(IntPoint));
			SavePolygonToImage(polygon1, "C:\\temp\\polygon.jpg", 640, (g, t) =>
			{
				var circle = new Ellipse(polygon1[0].X, polygon1[0].Y, 2, 2);
				g.Render(new VertexSourceApplyTransform(new Stroke(circle, 1000), t), Color.Blue);
				circle = new Ellipse(polygon1[index].X, polygon1[index].Y, 2, 2);
				g.Render(new VertexSourceApplyTransform(new Stroke(circle, 1000), t), Color.Red);
			});
		}

		[Test]
		public void CorrectSeamPlacement()
		{
			// coincident points return 0 angle
			{
				var p1 = new IntPoint(10, 0);
				var p2 = new IntPoint(0, 0);
				var p3 = new IntPoint(0, 0);
				Assert.IsTrue(p2.GetTurnAmount(p1, p3) == 0);
			}

			// no turn returns a 0 angle
			{
				var p1 = new IntPoint(10, 0);
				var p2 = new IntPoint(0, 0);
				var p3 = new IntPoint(-10, 0);
				Assert.IsTrue(p2.GetTurnAmount(p1, p3) == 0);
			}

			// 90 turn works
			{
				var p1 = new IntPoint(0, 0);
				var p2 = new IntPoint(10, 0);
				var p3 = new IntPoint(10, 10);
				Assert.AreEqual(p2.GetTurnAmount(p1, p3), Math.PI / 2, .001);

				var p4 = new IntPoint(0, 10);
				var p5 = new IntPoint(0, 0);
				var p6 = new IntPoint(10, 0);
				Assert.AreEqual(p5.GetTurnAmount(p4, p6), Math.PI / 2, .001);
			}

			// -90 turn works
			{
				var p1 = new IntPoint(0, 0);
				var p2 = new IntPoint(10, 0);
				var p3 = new IntPoint(10, -10);
				Assert.AreEqual(p2.GetTurnAmount(p1, p3), -Math.PI / 2, .001);
			}

			// 45 turn works
			{
				var p1 = new IntPoint(0, 0);
				var p2 = new IntPoint(10, 0);
				var p3 = new IntPoint(15, 5);
				Assert.AreEqual(Math.PI / 4, p2.GetTurnAmount(p1, p3), .001);

				var p4 = new IntPoint(0, 0);
				var p5 = new IntPoint(-10, 0);
				var p6 = new IntPoint(-15, -5);
				Assert.AreEqual(Math.PI / 4, p5.GetTurnAmount(p4, p6), .001);
			}

			// -45 turn works
			{
				var p1 = new IntPoint(0, 0);
				var p2 = new IntPoint(10, 0);
				var p3 = new IntPoint(15, -5);
				Assert.AreEqual(-Math.PI / 4, p2.GetTurnAmount(p1, p3), .001);
			}

			// find the right point wound ccw
			{
				// 4________3
				// |       /
				// |      /2
				// |      \
				// |0______\1
				var testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(70, 50), new IntPoint(100, 100), new IntPoint(0, 100) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 2);
			}

			// find the right point wound ccw
			{
				// 3________2
				// |       |
				// |       |
				// |       |
				// |0______|1
				var testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(100, 100), new IntPoint(0, 100) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 3);
			}

			// find closest greatest ccw
			{
				// 3________2
				// |       |
				// |       |
				// |       |
				// |0______|1
				var testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(100, 100), new IntPoint(0, 100) };
				int bestPoint = testPoints.FindGreatestTurnIndex(startPosition: new IntPoint(110, 110));
				Assert.IsTrue(bestPoint == 2);
			}

			// find the right point wound ccw
			{
				// 1________0
				// |       |
				// |       |
				// |       |
				// |2______|3
				var testPoints = new List<IntPoint> { new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 1);
			}

			// find the right point wound cw
			{
				// 1________2
				// |       |
				// |       |
				// |       |
				// |0______|3
				var testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 1); // this is an inside perimeter so we place the seem to the front
			}

			// find the right point wound cw
			{
				// 0________1
				// |       |
				// |       |
				// |       |
				// |3______|2
				var testPoints = new List<IntPoint> { new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(100, 0), new IntPoint(0, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// find the right point wound ccw
			{
				// 4________3
				// |       /
				// |      /2
				// |      \
				// |0______\1
				var testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(1000, 0), new IntPoint(900, 500), new IntPoint(1000, 1000), new IntPoint(0, 1000) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 2 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 2);
			}

			// ccw shallow
			{
				// 2________1
				// |       /
				// |      /0
				// |      \
				// |3______\4
				var testPoints = new List<IntPoint> { new IntPoint(90, 50), new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 0 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 2________1
				// |       /
				// |      /0
				// |      \
				// |3______\4
				var testPoints = new List<IntPoint> { new IntPoint(90, 50), new IntPoint(200, 100), new IntPoint(0, 100), new IntPoint(0, 0), new IntPoint(200, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0
				//   /    \
				//  /4_____\5
				var testPoints = new List<IntPoint> { new IntPoint(90, 50), new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(10, 50), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 3 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0
				//   /    \
				//  /4_____\5
				var testPoints = new List<IntPoint> { new IntPoint(90, 50), new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(10, 50), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex(startPosition: new IntPoint(95, 50));
				// 3 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0
				//   /    \
				//  /4_____\5
				var testPoints = new List<IntPoint> { new IntPoint(55, 50), new IntPoint(100, 100), new IntPoint(0, 100), new IntPoint(45, 50), new IntPoint(0, 0), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 3);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0 less angle
				//   /    \
				//  /4_____\5
				var testPoints = new List<IntPoint> { new IntPoint(950, 500), new IntPoint(1000, 1000), new IntPoint(0, 1000), new IntPoint(100, 500), new IntPoint(0, 0), new IntPoint(1000, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 2 is too shallow to have the seem
				Assert.IsTrue(bestPoint == 3);
			}

			// ccw
			{
				// 2________1
				//  \      /
				//   \3   /0 more angle
				//   /    \
				//  /4_____\5
				var testPoints = new List<IntPoint> { new IntPoint(550, 500), new IntPoint(1000, 1000), new IntPoint(0, 1000), new IntPoint(100, 500), new IntPoint(0, 0), new IntPoint(1000, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// ccw
			{
				// 5________4
				//  \      /
				//   \0   /3
				//   /    \
				//  /1_____\2
				var testPoints = new List<IntPoint> { new IntPoint(10, 50), new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(90, 50), new IntPoint(100, 100), new IntPoint(0, 100), };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				// 0 is too shallow
				Assert.IsTrue(bestPoint == 3);
			}

			// ccw
			{
				// 5________4
				//  \      /
				//   \0   /3
				//   /    \
				//  /1_____\2
				var testPoints = new List<IntPoint> { new IntPoint(45, 50), new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(55, 50), new IntPoint(100, 100), new IntPoint(0, 100), };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 3);
			}

			// find the right point wound cw (inside hole loops)
			{
				// 1________2
				// |       /
				// |      /3
				// |      \
				// |0______\4
				var testPoints = new List<IntPoint> { new IntPoint(0, 0), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(90, 50), new IntPoint(100, 0) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 4); // everything over 90 degrees is treated the same so the front left is the best
			}

			// find the right point wound cw
			{
				// 2________3
				// |       /
				// |      /4
				// |      \
				// |1______\0
				var testPoints = new List<IntPoint> { new IntPoint(100, 0), new IntPoint(0, 0), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(90, 50) };
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 0);
			}

			// cw
			{
				// 4________5
				//  \      /
				//   \3   /0
				//   /    \
				//  /2_____\1
				var testPoints = new List<IntPoint>
				{
					new IntPoint(90, 50), new IntPoint(100, 0), new IntPoint(0, 0), new IntPoint(10, 50), new IntPoint(0, 100), new IntPoint(100, 100)
				};
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 4);
			}

			// cw
			{
				// 1________2
				//  \      /
				//   \0   /3
				//   /    \
				//  /5_____\4
				var testPoints = new List<IntPoint>
				{
					new IntPoint(10, 50), new IntPoint(0, 100), new IntPoint(100, 100), new IntPoint(90, 50), new IntPoint(100, 0), new IntPoint(0, 0),
				};
				int bestPoint = testPoints.FindGreatestTurnIndex();
				Assert.IsTrue(bestPoint == 4);
			}
		}
	}
}