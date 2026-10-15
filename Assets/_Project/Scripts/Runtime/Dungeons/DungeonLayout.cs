using System.Collections.Generic;
using UnityEngine;

namespace RPG.Dungeons
{
    /// <summary>
    /// The plan of a dungeon in grid cells: rooms in a row joined by corridors, the last room the boss's.
    /// Every peer builds the same tilemaps from it, so only the dungeon's slot needs to be sent over the network.
    /// </summary>
    public static class DungeonLayout
    {
        /// <summary>Rooms from the entrance to the boss room.</summary>
        public static readonly RectInt[] Rooms =
        {
            new RectInt(0, 0, 9, 9),
            new RectInt(13, 0, 9, 9),
            new RectInt(26, 0, 9, 9),
            new RectInt(39, -2, 13, 13),
        };

        /// <summary>Three cells wide, joining each room to the next.</summary>
        public static readonly RectInt[] Corridors =
        {
            new RectInt(9, 3, 4, 3),
            new RectInt(22, 3, 4, 3),
            new RectInt(35, 3, 4, 3),
        };

        public static int BossRoom => Rooms.Length - 1;

        /// <summary>Where players arrive.</summary>
        public static Vector3Int Arrival => new Vector3Int(5, 4, 0);

        /// <summary>The exit next to the arrival point (always open).</summary>
        public static Vector3Int EntranceExit => new Vector3Int(1, 4, 0);

        /// <summary>The exit at the far end of the boss room (opens when the boss is dead).</summary>
        public static Vector3Int BossExit => new Vector3Int(50, 4, 0);

        public static Vector3Int RoomCenter(int room)
        {
            RectInt rect = Rooms[Mathf.Clamp(room, 0, Rooms.Length - 1)];
            return new Vector3Int(rect.x + rect.width / 2, rect.y + rect.height / 2, 0);
        }

        public static bool IsFloor(Vector3Int cell)
        {
            var point = new Vector2Int(cell.x, cell.y);
            foreach (RectInt room in Rooms)
                if (room.Contains(point)) return true;
            foreach (RectInt corridor in Corridors)
                if (corridor.Contains(point)) return true;
            return false;
        }

        /// <summary>Every floor cell.</summary>
        public static List<Vector3Int> FloorCells()
        {
            var cells = new List<Vector3Int>();
            foreach (Vector3Int cell in Bounds().allPositionsWithin)
                if (IsFloor(cell)) cells.Add(cell);
            return cells;
        }

        /// <summary>Cells next to the floor (including diagonally) that are not floor: the walls.</summary>
        public static List<Vector3Int> WallCells()
        {
            var cells = new List<Vector3Int>();
            foreach (Vector3Int cell in Bounds().allPositionsWithin)
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

        /// <summary>All cells of the layout including a one-cell wall border.</summary>
        public static BoundsInt Bounds()
        {
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
            foreach (RectInt room in Rooms)
            {
                xMin = Mathf.Min(xMin, room.xMin);
                yMin = Mathf.Min(yMin, room.yMin);
                xMax = Mathf.Max(xMax, room.xMax);
                yMax = Mathf.Max(yMax, room.yMax);
            }
            return new BoundsInt(xMin - 1, yMin - 1, 0, xMax - xMin + 2, yMax - yMin + 2, 1);
        }
    }
}
