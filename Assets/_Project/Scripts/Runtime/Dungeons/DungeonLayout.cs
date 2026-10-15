using System.Collections.Generic;
using UnityEngine;

namespace RPG.Dungeons
{
    /// <summary>
    /// The plan of a dungeon in grid cells, read from its definition's rows (Source's map: '.' walkable).
    /// Every peer builds the same tilemaps from it, so only the dungeon and its slot need to be sent over the network.
    /// </summary>
    public sealed class DungeonLayout
    {
        private static readonly Dictionary<DungeonDefinition, DungeonLayout> Cache = new Dictionary<DungeonDefinition, DungeonLayout>();

        private readonly string[] source;
        private readonly bool[,] floor;

        public int Width { get; }
        public int Height { get; }

        private DungeonLayout(string[] rows)
        {
            source = rows;
            Height = rows.Length;
            foreach (string row in rows)
                Width = Mathf.Max(Width, row != null ? row.Length : 0);
            floor = new bool[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                string row = rows[y];
                if (row == null) continue;
                for (int x = 0; x < row.Length; x++)
                    floor[x, y] = row[x] == '.';
            }
        }

        /// <summary>The plan of a dungeon (built once, again when its rows change).</summary>
        public static DungeonLayout For(DungeonDefinition dungeon)
        {
            string[] rows = dungeon != null && dungeon.rows != null ? dungeon.rows : System.Array.Empty<string>();
            if (dungeon != null && Cache.TryGetValue(dungeon, out DungeonLayout cached) && cached.source == rows) return cached;
            var layout = new DungeonLayout(rows);
            if (dungeon != null) Cache[dungeon] = layout;
            return layout;
        }

        public bool IsFloor(Vector3Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height && floor[cell.x, cell.y];

        /// <summary>Every floor cell.</summary>
        public List<Vector3Int> FloorCells()
        {
            var cells = new List<Vector3Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (floor[x, y]) cells.Add(new Vector3Int(x, y, 0));
            return cells;
        }

        /// <summary>Cells next to the floor (including diagonally) that are not floor: the walls.</summary>
        public List<Vector3Int> WallCells()
        {
            var cells = new List<Vector3Int>();
            BoundsInt bounds = Bounds();
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (IsFloor(cell)) continue;
                bool touchesFloor = false;
                for (int dx = -1; dx <= 1 && !touchesFloor; dx++)
                for (int dy = -1; dy <= 1 && !touchesFloor; dy++)
                    touchesFloor = IsFloor(new Vector3Int(cell.x + dx, cell.y + dy, 0));
                if (touchesFloor) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>All cells of the plan including a one-cell wall border.</summary>
        public BoundsInt Bounds() => new BoundsInt(-1, -1, 0, Width + 2, Height + 2, 1);

        /// <summary>The floor cell nearest to <paramref name="cell"/> (itself when it is floor).</summary>
        public Vector3Int NearestFloor(Vector3Int cell)
        {
            if (IsFloor(cell)) return cell;
            for (int radius = 1; radius < Mathf.Max(Width, Height); radius++)
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;
                var candidate = new Vector3Int(cell.x + dx, cell.y + dy, 0);
                if (IsFloor(candidate)) return candidate;
            }
            return cell;
        }
    }
}
