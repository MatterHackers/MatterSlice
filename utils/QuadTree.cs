//The MIT License(MIT)

//Copyright(c) 2015 ChevyRay

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

namespace QuadTree
{
	/// <summary>
	/// Used by the QuadTree to represent a rectangular area.
	/// </summary>
	public struct Quad
	{
		public long MaxX;
		public long MaxY;
		public long MinX;
		public long MinY;
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

		/// <summary>
		/// Check if this Quad can completely contain another.
		/// </summary>
		public bool Contains(ref Quad other)
		{
			return other.MinX >= MinX && other.MinY >= MinY && other.MaxX <= MaxX && other.MaxY <= MaxY;
		}

		/// <summary>
		/// Check if this Quad contains the point.
		/// </summary>
		public bool Contains(long x, long y)
		{
			return x > MinX && y > MinY && x < MaxX && y < MaxY;
		}

		/// <summary>
		/// Check if this Quad intersects with another.
		/// </summary>
		public bool Intersects(ref Quad other)
		{
			return MinX < other.MaxX && MinY < other.MaxY && MaxX > other.MinX && MaxY > other.MinY;
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

	/// <summary>
	/// A quad tree where leaf nodes contain a quad and a unique instance of T.
	/// For example, if you are developing a game, you might use QuadTree<GameObject>
	/// for collisions, or QuadTree<int> if you just want to populate it with IDs.
	/// </summary>
	public class QuadTree<T>
	{
		internal static Stack<Branch> branchPool = new Stack<Branch>();
		internal static Stack<Leaf> leafPool = new Stack<Leaf>();

		internal Dictionary<T, Leaf> leafLookup = new Dictionary<T, Leaf>();
		internal int splitCount;
		 Branch root;
		/// <summary>
		/// Creates a new QuadTree.
		/// </summary>
		/// <param name="splitCount">How many leaves a branch can hold before it splits into sub-branches.</param>
		/// <param name="region">The region that your quadtree occupies, all inserted quads should fit into this.</param>
		public QuadTree(int splitCount, ref Quad region)
		{
			this.splitCount = splitCount;
			root = CreateBranch(this, null, ref region);
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

		/// <summary>
		/// QuadTree internally keeps pools of Branches and Leaves. If you want to clear these to clean up memory,
		/// you can call this function. Most of the time you'll want to leave this alone, though.
		/// </summary>
		public static void ClearPools()
		{
			branchPool = new Stack<Branch>();
			leafPool = new Stack<Leaf>();
			Branch.tempPool = new Stack<List<Leaf>>();
		}

		/// <summary>
		/// Clear the QuadTree. This will remove all leaves and branches. If you have a lot of moving objects,
		/// you probably want to call Clear() every frame, and re-insert every object. Branches and leaves are pooled.
		/// </summary>
		public void Clear()
		{
			root.Clear();
			root.Tree = this;
			leafLookup.Clear();
		}
		/// <summary>
		/// Count how many branches are in the QuadTree.
		/// </summary>
		public int CountBranches()
		{
			int count = 0;
			CountBranches(root, ref count);
			return count;
		}

		/// <summary>
		/// Find all other values whose areas are overlapping the specified value.
		/// </summary>
		/// <returns>True if any collisions were found.</returns>
		/// <param name="value">The value to check collisions against.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public IEnumerable<T> FindCollisions(T value)
		{
			Leaf leaf;
			if (leafLookup.TryGetValue(value, out leaf))
			{
				var branch = leaf.Branch;

				//Add the leaf's siblings (prevent it from colliding with itself)
				if (branch.Leaves.Count > 0)
				{
					for (int i = 0; i < branch.Leaves.Count; ++i)
					{
						if (leaf != branch.Leaves[i] && leaf.Quad.Intersects(ref branch.Leaves[i].Quad))
						{
							yield return branch.Leaves[i].Value;
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
							foreach(var index in branch.Branches[i].SearchQuad(leaf.Quad))
							{
								yield return index;
							}
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
								yield return branch.Leaves[i].Value;
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
			Leaf leaf;
			if (!leafLookup.TryGetValue(value, out leaf))
			{
				leaf = CreateLeaf(value, ref quad);
				leafLookup.Add(value, leaf);
			}
			root.Insert(leaf);
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
		public void Insert(T value, long x, long y, long width, long height)
		{
			var quad = new Quad(x, y, x + width, y + height);
			Insert(value, ref quad);
		}

		/// <summary>
		/// Find all values contained in the specified area.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="quad">The area to search.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public IEnumerable<T> SearchArea(ref Quad quad)
		{
			return root.SearchQuad(quad);
		}

		/// <summary>
		/// Find all values contained in the specified area.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="quad">The area to search.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public IEnumerable<T> SearchArea(Quad quad)
		{
			return SearchArea(ref quad);
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
		public IEnumerable<T> SearchArea(long x, long y, long width, long height)
		{
			var quad = new Quad(x, y, x + width, y + height);
			return SearchArea(ref quad);
		}

		/// <summary>
		/// Find all values overlapping the specified point.
		/// </summary>
		/// <returns>True if any values were found.</returns>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="values">A list to populate with the results. If null, this function will create the list for you.</param>
		public IEnumerable<T> SearchPoint(long x, long y)
		{
			return root.SearchPoint(x, y);
		}

		static Branch CreateBranch(QuadTree<T> tree, Branch parent, ref Quad quad)
		{
			var branch = branchPool.Count > 0 ? branchPool.Pop() : new Branch();
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

		static Leaf CreateLeaf(T value, ref Quad quad)
		{
			var leaf = leafPool.Count > 0 ? leafPool.Pop() : new Leaf();
			leaf.Value = value;
			leaf.Quad = quad;
			return leaf;
		}

		void CountBranches(Branch branch, ref int count)
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

		internal class Branch
		{
			internal static Stack<List<Leaf>> tempPool = new Stack<List<Leaf>>();

			internal Branch[] Branches = new Branch[4];
			internal List<Leaf> Leaves = new List<Leaf>();
			internal Branch Parent;
			internal Quad[] Quads = new Quad[4];
			internal bool Split;
			internal QuadTree<T> Tree;
			internal void Clear()
			{
				Tree = null;
				Parent = null;
				Split = false;

				for (int i = 0; i < 4; ++i)
				{
					if (Branches[i] != null)
					{
						branchPool.Push(Branches[i]);
						Branches[i].Clear();
						Branches[i] = null;
					}
				}

				for (int i = 0; i < Leaves.Count; ++i)
				{
					leafPool.Push(Leaves[i]);
					Leaves[i].Branch = null;
					Leaves[i].Value = default(T);
				}

				Leaves.Clear();
			}

			internal void Insert(Leaf leaf)
			{
				//If this branch is already split
				if (Split)
				{
					for (int i = 0; i < 4; ++i)
					{
						if (Quads[i].Contains(ref leaf.Quad))
						{
							if (Branches[i] == null)
							{
								Branches[i] = CreateBranch(Tree, this, ref Quads[i]);
							}
							Branches[i].Insert(leaf);
							return;
						}
					}

					Leaves.Add(leaf);
					leaf.Branch = this;
				}
				else
				{
					//Add the leaf to this node
					Leaves.Add(leaf);
					leaf.Branch = this;

					//Once I have reached capacity, split the node
					if (Leaves.Count >= Tree.splitCount)
					{
						var temp = tempPool.Count > 0 ? tempPool.Pop() : new List<Leaf>();
						temp.AddRange(Leaves);
						Leaves.Clear();
						Split = true;
						for (int i = 0; i < temp.Count; ++i)
						{
							Insert(temp[i]);
						}
						temp.Clear();
						tempPool.Push(temp);
					}
				}
			}

			internal IEnumerable<T> SearchPoint(long x, long y)
			{
				if (Leaves.Count > 0)
				{
					for (int i = 0; i < Leaves.Count; ++i)
					{
						if (Leaves[i].Quad.Contains(x, y))
						{
							yield return Leaves[i].Value;
						}
					}
				}

				for (int i = 0; i < 4; ++i)
				{
					if (Branches[i] != null)
					{
						foreach(var index in Branches[i].SearchPoint(x, y))
						{
							yield return index;
						}
					}
				}
			}

			internal IEnumerable<T> SearchQuad(Quad quad)
			{
				if (Leaves.Count > 0)
				{
					for (int i = 0; i < Leaves.Count; ++i)
					{
						if (quad.Intersects(ref Leaves[i].Quad))
						{
							yield return Leaves[i].Value;
						}
					}
				}

				for (int i = 0; i < 4; ++i)
				{
					if (Branches[i] != null)
					{
						Branches[i].SearchQuad(quad);
					}
				}
			}
		}

		internal class Leaf
		{
			internal Branch Branch;
			internal Quad Quad;
			internal T Value;
		}
	}
}