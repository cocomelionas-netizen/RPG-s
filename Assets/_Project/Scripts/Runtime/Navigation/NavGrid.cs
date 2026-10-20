using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Navigation
{
    /// <summary>
    /// Walkable cells of a tile grid and A* path finding over them (8 neighbours, no cutting corners
    /// past blocked cells). Costs are world distances between cell centers, so it works for isometric
    /// grids as well as square ones. Pure C#: <see cref="WorldNavigation"/> builds it from the tilemaps.
    /// </summary>
    public class NavGrid
    {
        private static readonly int[] NeighbourX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] NeighbourY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private readonly BoundsInt bounds;
        private readonly int width;
        private readonly int height;
        private readonly bool[] walkable;
        private readonly bool[] sightBlocked;
        private readonly Vector2[] centers;

        // A* buffers, reused between searches. A stamp marks which entries belong to the current search.
        private readonly float[] costFromStart;
        private readonly int[] parent;
        private readonly int[] openStamp;
        private readonly int[] closedStamp;
        private readonly MinHeap open = new MinHeap();
        private int stamp;

        /// <param name="bounds">Cells covered by the grid (z is ignored).</param>
        /// <param name="isWalkable">Whether a cell can be walked on.</param>
        /// <param name="cellCenter">World position of a cell's center.</param>
        /// <param name="blocksSight">Whether a cell blocks sight (a wall); null: nothing does.</param>
        public NavGrid(BoundsInt bounds, Func<Vector3Int, bool> isWalkable, Func<Vector3Int, Vector2> cellCenter,
            Func<Vector3Int, bool> blocksSight = null)
        {
            this.bounds = bounds;
            width = Mathf.Max(0, bounds.size.x);
            height = Mathf.Max(0, bounds.size.y);

            int count = width * height;
            walkable = new bool[count];
            sightBlocked = new bool[count];
            centers = new Vector2[count];
            costFromStart = new float[count];
            parent = new int[count];
            openStamp = new int[count];
            closedStamp = new int[count];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                int index = y * width + x;
                walkable[index] = isWalkable(cell);
                sightBlocked[index] = blocksSight != null && blocksSight(cell);
                centers[index] = cellCenter(cell);
            }
        }

        public BoundsInt Bounds => bounds;

        public bool Contains(Vector3Int cell)
        {
            return cell.x >= bounds.xMin && cell.x < bounds.xMin + width
                && cell.y >= bounds.yMin && cell.y < bounds.yMin + height;
        }

        /// <summary>False outside the grid.</summary>
        public bool IsWalkable(Vector3Int cell)
        {
            return Contains(cell) && walkable[IndexOf(cell)];
        }

        /// <summary>True for a wall cell (nothing can be seen, shot or cast through it). False outside the grid.</summary>
        public bool BlocksSight(Vector3Int cell)
        {
            return Contains(cell) && sightBlocked[IndexOf(cell)];
        }

        public void SetWalkable(Vector3Int cell, bool value)
        {
            if (Contains(cell)) walkable[IndexOf(cell)] = value;
        }

        /// <summary>
        /// Finds the shortest path from <paramref name="start"/> to <paramref name="goal"/>.
        /// <paramref name="path"/> receives the cells after the start up to and including the goal
        /// (empty when start equals goal). The start cell itself may be blocked; the goal must be walkable.
        /// </summary>
        /// <param name="maxExpanded">Gives up after visiting this many cells.</param>
        public bool FindPath(Vector3Int start, Vector3Int goal, List<Vector3Int> path, int maxExpanded = 10000)
        {
            path.Clear();
            if (!Contains(start) || !IsWalkable(goal)) return false;
            if (start == goal) return true;

            NextStamp();
            open.Clear();

            int startIndex = IndexOf(start);
            int goalIndex = IndexOf(goal);
            costFromStart[startIndex] = 0f;
            parent[startIndex] = -1;
            openStamp[startIndex] = stamp;
            open.Push(startIndex, Heuristic(startIndex, goalIndex));

            int expanded = 0;
            while (open.Count > 0)
            {
                int current = open.Pop();
                // The heap may hold outdated duplicates of a cell; only the first (cheapest) one counts.
                if (closedStamp[current] == stamp) continue;
                closedStamp[current] = stamp;

                if (current == goalIndex)
                {
                    BuildPath(goalIndex, path);
                    return true;
                }

                if (++expanded > maxExpanded) return false;

                int currentX = current % width;
                int currentY = current / width;
                for (int i = 0; i < NeighbourX.Length; i++)
                {
                    int x = currentX + NeighbourX[i];
                    int y = currentY + NeighbourY[i];
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;

                    int neighbour = y * width + x;
                    if (!walkable[neighbour] || closedStamp[neighbour] == stamp) continue;

                    // Diagonal steps must not squeeze between two blocked cells or clip a blocked corner.
                    bool diagonal = NeighbourX[i] != 0 && NeighbourY[i] != 0;
                    if (diagonal && (!walkable[currentY * width + x] || !walkable[y * width + currentX])) continue;

                    float cost = costFromStart[current] + Vector2.Distance(centers[current], centers[neighbour]);
                    if (openStamp[neighbour] == stamp && cost >= costFromStart[neighbour]) continue;

                    openStamp[neighbour] = stamp;
                    costFromStart[neighbour] = cost;
                    parent[neighbour] = current;
                    open.Push(neighbour, cost + Heuristic(neighbour, goalIndex));
                }
            }

            return false;
        }

        /// <summary>
        /// The walkable cell nearest (in world distance) to <paramref name="cell"/>, searching rings up to
        /// <paramref name="maxRadius"/> cells away. Returns the cell itself when it is walkable.
        /// </summary>
        public bool TryFindNearestWalkable(Vector3Int cell, int maxRadius, out Vector3Int result)
        {
            result = cell;
            if (IsWalkable(cell)) return true;

            Vector2 origin = CellCenterUnclamped(cell);
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                bool found = false;
                float bestDistance = float.MaxValue;
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Only the ring at this radius; the inside was searched already.
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

                    var candidate = new Vector3Int(cell.x + dx, cell.y + dy, 0);
                    if (!IsWalkable(candidate)) continue;

                    float distance = Vector2.Distance(origin, centers[IndexOf(candidate)]);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        result = candidate;
                        found = true;
                    }
                }
                if (found) return true;
            }
            return false;
        }

        /// <summary>World center of a cell inside the grid.</summary>
        public Vector2 CellCenter(Vector3Int cell)
        {
            return centers[IndexOf(cell)];
        }

        private Vector2 CellCenterUnclamped(Vector3Int cell)
        {
            if (Contains(cell)) return centers[IndexOf(cell)];
            var clamped = new Vector3Int(
                Mathf.Clamp(cell.x, bounds.xMin, bounds.xMin + width - 1),
                Mathf.Clamp(cell.y, bounds.yMin, bounds.yMin + height - 1), 0);
            return width > 0 && height > 0 ? centers[IndexOf(clamped)] : Vector2.zero;
        }

        private int IndexOf(Vector3Int cell)
        {
            return (cell.y - bounds.yMin) * width + (cell.x - bounds.xMin);
        }

        private float Heuristic(int from, int to)
        {
            return Vector2.Distance(centers[from], centers[to]);
        }

        private void BuildPath(int goalIndex, List<Vector3Int> path)
        {
            for (int index = goalIndex; parent[index] >= 0; index = parent[index])
                path.Add(new Vector3Int(bounds.xMin + index % width, bounds.yMin + index / width, 0));
            path.Reverse();
        }

        private void NextStamp()
        {
            if (stamp == int.MaxValue)
            {
                Array.Clear(openStamp, 0, openStamp.Length);
                Array.Clear(closedStamp, 0, closedStamp.Length);
                stamp = 0;
            }
            stamp++;
        }

        /// <summary>Binary min-heap of cell indices by priority. Duplicates are allowed.</summary>
        private class MinHeap
        {
            private readonly List<int> items = new List<int>();
            private readonly List<float> priorities = new List<float>();

            public int Count => items.Count;

            public void Clear()
            {
                items.Clear();
                priorities.Clear();
            }

            public void Push(int item, float priority)
            {
                items.Add(item);
                priorities.Add(priority);

                int child = items.Count - 1;
                while (child > 0)
                {
                    int parentIndex = (child - 1) / 2;
                    if (priorities[parentIndex] <= priorities[child]) break;
                    Swap(child, parentIndex);
                    child = parentIndex;
                }
            }

            public int Pop()
            {
                int top = items[0];
                int last = items.Count - 1;
                items[0] = items[last];
                priorities[0] = priorities[last];
                items.RemoveAt(last);
                priorities.RemoveAt(last);

                int index = 0;
                while (true)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;
                    int smallest = index;
                    if (left < items.Count && priorities[left] < priorities[smallest]) smallest = left;
                    if (right < items.Count && priorities[right] < priorities[smallest]) smallest = right;
                    if (smallest == index) break;
                    Swap(index, smallest);
                    index = smallest;
                }
                return top;
            }

            private void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (priorities[a], priorities[b]) = (priorities[b], priorities[a]);
            }
        }
    }
}
