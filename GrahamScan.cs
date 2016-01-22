using System;
using System.Collections.Generic;

/*
 * Copyright (c) 2015, John Lewin
 * Copyright (c) 2010, Bart Kiers
 *
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 */
namespace cg
{
	/// 
	public sealed class GrahamScan : IComparer<GrahamScan.Point>
	{
		[Serializable]
		public struct Point
		{
			public int x, y;

			public Point(int newX, int newY)
			{
				x = newX;
				y = newY;
			}

			public override string ToString()
			{
				return string.Format("[{0},{1}]", this.x, this.y);
			}
		}


		/// <summary>
		/// An enum denoting a directional-turn between 3 points (vectors).
		/// </summary>
		protected internal enum Turn
		{
			Clockwise,
			CounterClockwise,
			Collinear
		}

		Point lowest;

		/// <summary>
		/// Returns true if all points in <code>points</code> are collinear.
		/// </summary>
		/// <param name="points"> the list of points. </param>
		/// <returns>       true if all points in <code>points</code> are collinear. </returns>
		protected internal static bool areAllCollinear(IList<Point> points)
		{

			if (points.Count < 2)
			{
				return true;
			}

			Point a = points[0];
			Point b = points[1];

			for (int i = 2; i < points.Count; i++)
			{

				Point c = points[i];

				if (GetTurn(a, b, c) != Turn.Collinear)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Returns the convex hull of the points created from the list
		/// <code>points</code>. Note that the first and last point in the
		/// returned <code>List&lt;java.awt.Point&gt;</code> are the same
		/// point.
		/// </summary>
		/// <param name="points">The list of points. </param>
		/// <returns>The convex hull of the points created from the list <code>points</code>. </returns>
		public IList<Point> getConvexHull(List<Point> points)
		{

			this.lowest = getLowestPoint(points);

			List<Point> sorted;

			points.Sort(this);

			sorted = points;

			if (sorted.Count < 3)
			{
				throw new System.ArgumentException("can only create a convex hull of 3 or more unique points");
			}

			if (areAllCollinear(sorted))
			{
				throw new System.ArgumentException("cannot create a convex hull from collinear points");
			}

			Stack<Point> stack = new Stack<Point>();
			stack.Push(sorted[0]);
			stack.Push(sorted[1]);

			for (int i = 2; i < sorted.Count; i++)
			{
				Point head = sorted[i];
				Point middle = stack.Pop();
				Point tail = stack.Peek();

				Turn turn = GetTurn(tail, middle, head);
				switch (turn)
				{
					case Turn.CounterClockwise:
						stack.Push(middle);
						stack.Push(head);
						break;
					case Turn.Clockwise:
						i--;
						break;
					case Turn.Collinear:
						stack.Push(head);
						break;
				}
			}

			// close the hull
			stack.Push(sorted[0]);

			return new List<Point>(stack);
		}

		/// <summary>
		/// Returns the points with the lowest y coordinate. In case more than 1 such
		/// point exists, the one with the lowest x coordinate is returned.
		/// </summary>
		/// <param name="points"> the list of points to return the lowest point from. </param>
		/// <returns>       the points with the lowest y coordinate. In case more than
		///               1 such point exists, the one with the lowest x coordinate
		///               is returned. </returns>
		private static Point getLowestPoint(IList<Point> points)
		{
			Point lowest = points[0];

			for (int i = 1; i < points.Count; i++)
			{
				Point temp = points[i];

				if (temp.y < lowest.y || (temp.y == lowest.y && temp.x < lowest.x))
				{
					lowest = temp;
				}
			}

			return lowest;
		}

		/// <summary>
		/// Returns a sorted set of points from the list <code>points</code>. The
		/// set of points are sorted in increasing order of the angle they and the
		/// lowest point <tt>P</tt> make with the x-axis. If tow (or more) points
		/// form the same angle towards <tt>P</tt>, the one closest to <tt>P</tt>
		/// comes first.
		/// </summary>
		/// <param name="points"> the list of points to sort. </param>
		/// <returns>       a sorted set of points from the list <code>points</code>. </returns>
		/// <seealso cref= GrahamScan#getLowestPoint(java.util.List) </seealso>
		public int Compare(Point a, Point b)
		{
			if (a.Equals(b))
			{
				return 0;
			}

			// use longs to guard against int-underflow
			double thetaA = Math.Atan2((long)a.y - lowest.y, (long)a.x - lowest.x);
			double thetaB = Math.Atan2((long)b.y - lowest.y, (long)b.x - lowest.x);

			if (thetaA < thetaB)
			{
				return -1;
			}
			else if (thetaA > thetaB)
			{
				return 1;
			}
			else
			{
				// collinear with the 'lowest' point, let the point closest to it come first

				// use longs to guard against int-over/underflow
				double distanceA = Math.Sqrt((((long)lowest.x - a.x) * ((long)lowest.x - a.x)) + (((long)lowest.y - a.y) * ((long)lowest.y - a.y)));
				double distanceB = Math.Sqrt((((long)lowest.x - b.x) * ((long)lowest.x - b.x)) + (((long)lowest.y - b.y) * ((long)lowest.y - b.y)));

				if (distanceA < distanceB)
				{
					return -1;
				}
				else
				{
					return 1;
				}
			}
		}
	


	/// <summary>
	/// Returns the GrahamScan#Turn formed by traversing through the
	/// ordered points <code>a</code>, <code>b</code> and <code>c</code>.
	/// More specifically, the cross product <tt>C</tt> between the
	/// 3 points (vectors) is calculated:
	/// 
	/// <tt>(b.x-a.x * c.y-a.y) - (b.y-a.y * c.x-a.x)</tt>
	/// 
	/// and if <tt>C</tt> is less than 0, the turn is CLOCKWISE, if
	/// <tt>C</tt> is more than 0, the turn is COUNTER_CLOCKWISE, else
	/// the three points are COLLINEAR.
	/// </summary>
	/// <param name="a"> the starting point. </param>
	/// <param name="b"> the second point. </param>
	/// <param name="c"> the end point. </param>
	/// <returns> the GrahamScan#Turn formed by traversing through the
	///         ordered points <code>a</code>, <code>b</code> and
	///         <code>c</code>. </returns>
	protected internal static Turn GetTurn(Point a, Point b, Point c)
		{

			// use longs to guard against int-over/underflow
			long crossProduct = (((long)b.x - a.x) * ((long)c.y - a.y)) - (((long)b.y - a.y) * ((long)c.x - a.x));

			if (crossProduct > 0)
			{
				return Turn.CounterClockwise;
			}
			else if (crossProduct < 0)
			{
				return Turn.Clockwise;
			}
			else
			{
				return Turn.Collinear;
			}
		}
	}

}