﻿//The MIT License(MIT)

//Copyright(c) 2015 ChevyRay, 2017 Lars Brubaker

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Collections.Generic;
using MSClipperLib;

namespace MatterHackers.QuadTree
{
	/// <summary>
	/// Used by the QuadTree to represent a rectangular area.
	/// </summary>
	public struct Quad
	{
		public Quad(IntPoint testPosition, int expandDist = 1) : this()
		{
			MinX = testPosition.X - expandDist;
			MinY = testPosition.Y - expandDist;
			MaxX = testPosition.X + expandDist;
			MaxY = testPosition.Y + expandDist;
		}

		public Quad(IntPoint start, IntPoint end) : this()
		{
			MinX = Math.Min(start.X, end.X) - 1;
			MinY = Math.Min(start.Y, end.Y) - 1;
			MaxX = Math.Max(start.X, end.X) + 1;
			MaxY = Math.Max(start.Y, end.Y) + 1;
		}

		/// <summary>
		/// Construct a new Quad.
		/// </summary>
		/// <param name="minX">Minimum x.</param>
		/// <param name="minY">Minimum y.</param>
		/// <param name="maxX">Max x.</param>
		/// <param name="maxY">Max y.</param>
		public Quad(long minX, long minY, long maxX, long maxY)
		{
			MinX = minX;
			MinY = minY;
			MaxX = maxX;
			MaxY = maxY;
		}

		public long MaxX { get; private set; }
		public long MaxY { get; private set; }
		public long MinX { get; private set; }
		public long MinY { get; private set; }

		/// <summary>
		/// Check if this Quad can completely contain another.
		/// </summary>
		public bool Contains(ref Quad other)
		{
			if (MinX < other.MinX
				&& MinY < other.MinY
				&& MaxX > other.MaxX
				&& MaxY > other.MaxY)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Check if this Quad contains the point.
		/// </summary>
		public bool Contains(long x, long y)
		{
			if (x > MinX
				&& y > MinY
				&& x < MaxX
				&& y < MaxY)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Check if this Quad intersects with another.
		/// </summary>
		public bool Intersects(Quad other)
		{
			if (MinX < other.MaxX
				&& MinY < other.MaxY
				&& MaxX > other.MinX
				&& MaxY > other.MinY)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Set the Quad's position.
		/// </summary>
		/// <param name="minX">Minimum x.</param>
		/// <param name="minY">Minimum y.</param>
		/// <param name="maxX">Max x.</param>
		/// <param name="maxY">Max y.</param>
		public void Set(long minX, long minY, long maxX, long maxY)
		{
			MinX = minX;
			MinY = minY;
			MaxX = maxX;
			MaxY = maxY;
		}
	}
}