//The MIT License(MIT)

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
	/// A quad tree where leaf nodes contain a quad and a unique instance of T.
	/// For example, if you are developing a game, you might use QuadTree<GameObject>
	/// for collisions, or QuadTree<int> if you just want to populate it with IDs.
	/// </summary>
	public class QuadTree<T>
	{
		internal static Stack<Branch<T>> branchPool = new Stack<Branch<T>>();
		internal static Stack<Leaf<T>> leafPool = new Stack<Leaf<T>>();

		private Dictionary<T, Leaf<T>> leafLookup = new Dictionary<T, Leaf<T>>();
		internal int splitCount;

		public List<T> QueryResults { get; private set; } = new List<T>();

		/// <summary>
		/// Creates a new QuadTree.
		/// </summary>
		/// <param name="splitCount">How many leaves a branch can hold before it splits into sub-branches.</param>
		/// <param name="region">The region that your quadtree occupies, all inserted quads should fit into this.</param>
		public QuadTree(int splitCount, ref Quad region)
		{
			this.splitCount = splitCount;
			Root = CreateBranch(this, null, ref region);
		}

		/// <summary>
		/// Creates a new QuadTree.
		/// </summary>
		/// <param name="splitCount">How many leaves a branch can hold before it splits into sub-branches.</param>
		/// <param name="region">The region that your quadtree occupies, all inserted quads should fit into this.</param>
		public QuadTree(int splitCount, Quad region)
			: this(splitCount, ref region)
		{
		}

		/// <summary>
		/// Creates a new QuadTree.
		/// </summary>
		/// <param name="splitCount">How many leaves a branch can hold before it splits into sub-branches.</param>
		/// <param name="minX">X position of the region.</param>
		/// <param name="minY">Y position of the region.</param>
		/// <param name="maxX">Width of the region.</param>
		/// <param name="maxY">Height of the region.</param>
		public QuadTree(int splitCount, long minX, long minY, long maxX, long maxY)
			: this(splitCount, new Quad(minX, minY, maxX, maxY))
		{
		}

		public Branch<T> Root { get; private set; }

		/// <summary>
		/// QuadTree internally keeps pools of Branches and Leaves. If you want to clear these to clean up memory,
		/// you can call this function. Most of the time you'll want to leave this alone, though.
		/// </summary>
		public static void ClearPools()
		{
			branchPool = new Stack<Branch<T>>();
			leafPool = new Stack<Leaf<T>>();
			Branch<T>.tempPool = new Stack<List<Leaf<T>>>();
		}

		/// <summary>
		/// Clear the QuadTree. This will remove all leaves and branches. If you have a lot of moving objects,
		/// you probably want to call Clear() every frame, and re-insert every object. Branches and leaves are pooled.
		/// </summary>
		public void Clear()
		{
			Root.Clear();
			Root.Tree = this;
			leafLookup.Clear();
		}

		/// <summary>
		/// Count how many branches are in the QuadTree.
		/// </summary>
		public int CountBranches()
		{
			int count = 0;
			CountBranches(Root, ref count);
			return count;
		}

		/// <summary>
		/// Find all other values whose areas are overlapping the specified value.
		/// </summary>
		/// <returns>True if any collisions were found.</returns>
		/// <param name="value">The value to check collisions against.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public void FindCollisions(T value)
		{
			QueryResults.Clear();
			Leaf<T> leaf;
			if (leafLookup.TryGetValue(value, out leaf))
			{
				var branch = leaf.ContainingBranch;

				//Add the leaf's siblings (prevent it from colliding with itself)
				if (branch.Leaves.Count > 0)
				{
					for (int i = 0; i < branch.Leaves.Count; ++i)
					{
						if (leaf != branch.Leaves[i] && leaf.Quad.Intersects(ref branch.Leaves[i].Quad))
						{
							QueryResults.Add(branch.Leaves[i].Value);
						}
					}
				}

				//Add the branch's children
				if (branch.Split)
				{
					for (int i = 0; i < 4; ++i)
					{
						if (branch.Branches[i] != null)
						{
							branch.Branches[i].SearchQuad(leaf.Quad, QueryResults);
						}
					}
				}

				//Add all leaves back to the root
				branch = branch.Parent;
				while (branch != null)
				{
					if (branch.Leaves.Count > 0)
					{
						for (int i = 0; i < branch.Leaves.Count; ++i)
						{
							if (leaf.Quad.Intersects(ref branch.Leaves[i].Quad))
							{
								QueryResults.Add(branch.Leaves[i].Value);
							}
						}
					}
					branch = branch.Parent;
				}
			}
		}

		/// <summary>
		/// Insert a new leaf node into the QuadTree.
		/// </summary>
		/// <param name="value">The leaf value.</param>
		/// <param name="quad">The leaf size.</param>
		public void Insert(T value, ref Quad quad)
		{
			Leaf<T> leaf;
			if (!leafLookup.TryGetValue(value, out leaf))
			{
				leaf = CreateLeaf(value, ref quad);
				leafLookup.Add(value, leaf);
			}
			Root.Insert(leaf);
		}

		/// <summary>
		/// Insert a new leaf node into the QuadTree.
		/// </summary>
		/// <param name="value">The leaf value.</param>
		/// <param name="quad">The leaf quad.</param>
		public void Insert(T value, Quad quad)
		{
			Insert(value, ref quad);
		}

		/// <summary>
		/// Insert a new leaf node into the QuadTree.
		/// </summary>
		/// <param name="value">The leaf value.</param>
		/// <param name="x">X position of the leaf.</param>
		/// <param name="y">Y position of the leaf.</param>
		/// <param name="width">Width of the leaf.</param>
		/// <param name="height">Height of the leaf.</param>
		public void Insert(T value, long minX, long minY, long maxX, long maxY)
		{
			var quad = new Quad(minX, minY, maxX, maxY);
			Insert(value, ref quad);
		}

		/// <summary>
		/// Find all values contained in the specified area.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="quad">The area to search.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public void SearchArea(Quad quad)
		{
			QueryResults.Clear();
			Root.SearchQuad(quad, QueryResults);
		}

		/// <summary>
		/// Find all values contained in the specified area.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="quad">The area to search.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public void SearchArea(Quad quad, List<T> output)
		{
			SearchArea(quad, output);
		}

		/// <summary>
		/// Find all values contained in the specified area.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="x">X position to search.</param>
		/// <param name="y">Y position to search.</param>
		/// <param name="width">Width of the search area.</param>
		/// <param name="height">Height of the search area.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public void SearchArea(long x, long y, long width, long height, List<T> output)
		{
			var quad = new Quad(x, y, x + width, y + height);
			SearchArea(quad, output);
		}

		/// <summary>
		/// Find all values overlapping the specified point.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public void SearchPoint(long x, long y)
		{
			QueryResults.Clear();
			Root.SearchPoint(x, y, QueryResults);
		}

		internal static Branch<T> CreateBranch(QuadTree<T> tree, Branch<T> parent, ref Quad quad)
		{
			var branch = branchPool.Count > 0 ? branchPool.Pop() : new Branch<T>();
			branch.Tree = tree;
			branch.Parent = parent;
			branch.Split = false;
			long midX = quad.MinX + (quad.MaxX - quad.MinX) / 2;
			long midY = quad.MinY + (quad.MaxY - quad.MinY) / 2;
			branch.Quads[0].Set(quad.MinX, quad.MinY, midX, midY);
			branch.Quads[1].Set(midX, quad.MinY, quad.MaxX, midY);
			branch.Quads[2].Set(midX, midY, quad.MaxX, quad.MaxY);
			branch.Quads[3].Set(quad.MinX, midY, midX, quad.MaxY);
			return branch;
		}

		private static Leaf<T> CreateLeaf(T value, ref Quad quad)
		{
			var leaf = leafPool.Count > 0 ? leafPool.Pop() : new Leaf<T>();
			leaf.Value = value;
			leaf.Quad = quad;
			return leaf;
		}

		private void CountBranches(Branch<T> branch, ref int count)
		{
			++count;
			if (branch.Split)
			{
				for (int i = 0; i < 4; ++i)
				{
					if (branch.Branches[i] != null)
					{
						CountBranches(branch.Branches[i], ref count);
					}
				}
			}
		}
	}
}