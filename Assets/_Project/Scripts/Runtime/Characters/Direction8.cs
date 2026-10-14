using UnityEngine;

namespace RPG.Characters
{
    /// <summary>Eight screen-space facing directions, counter-clockwise from east.</summary>
    public enum Direction8
    {
        East,
        NorthEast,
        North,
        NorthWest,
        West,
        SouthWest,
        South,
        SouthEast,
    }

    public static class Direction8Util
    {
        public const int Count = 8;

        /// <summary>Snaps a screen-space vector to the nearest of the eight directions.</summary>
        public static Direction8 FromVector(Vector2 vector)
        {
            if (vector.sqrMagnitude < 0.0001f)
                return Direction8.South;

            float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(angle / 45f);
            return (Direction8)(((index % Count) + Count) % Count);
        }

        /// <summary>True for the directions that can be drawn by mirroring an east-facing sprite.</summary>
        public static bool IsWestSide(Direction8 direction)
        {
            return direction == Direction8.NorthWest || direction == Direction8.West || direction == Direction8.SouthWest;
        }

        /// <summary>The horizontally mirrored direction (west becomes east and back).</summary>
        public static Direction8 MirrorHorizontally(Direction8 direction)
        {
            switch (direction)
            {
                case Direction8.East: return Direction8.West;
                case Direction8.NorthEast: return Direction8.NorthWest;
                case Direction8.NorthWest: return Direction8.NorthEast;
                case Direction8.West: return Direction8.East;
                case Direction8.SouthWest: return Direction8.SouthEast;
                case Direction8.SouthEast: return Direction8.SouthWest;
                default: return direction;
            }
        }
    }
}
