using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// The one place marked on the local player's minimap and world map (a guard's directions to an NPC). It is taken
    /// off when the player gets there or another place is marked.
    /// </summary>
    public static class MapPin
    {
        /// <summary>Getting this close takes the pin off.</summary>
        public const float ReachedDistance = 2f;
        public static readonly Color PinColor = new Color(1f, 0.35f, 0.9f);

        public static bool Active { get; private set; }
        public static Vector2 Position { get; private set; }
        public static string Label { get; private set; } = "";

        public static void Set(Vector2 position, string label)
        {
            Active = true;
            Position = position;
            Label = label ?? "";
        }

        public static void Clear() => Active = false;

        /// <summary>Takes the pin off once the player stands near it.</summary>
        public static void CheckReached(Vector2 player)
        {
            if (Active && Vector2.Distance(player, Position) <= ReachedDistance) Active = false;
        }
    }
}
