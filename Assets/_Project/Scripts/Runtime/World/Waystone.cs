using System.Collections.Generic;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// A teleport stone in the world (the same on every peer). Walking near one discovers it; clicking one opens
    /// the list of discovered stones to travel to (<see cref="WaystoneTraveller"/>). Stones are told apart over the
    /// network and in saves by <see cref="Id"/> (0 to 31), so ids must never change.
    /// </summary>
    public class Waystone : MonoBehaviour
    {
        public const int MaxWaystones = 32;

        [SerializeField, Range(0, MaxWaystones - 1)] private int id;
        [SerializeField] private string displayName = "Waystone";
        [Tooltip("How close a player must be to use it (the server allows a little more).")]
        [SerializeField, Min(0.5f)] private float useRange = 1.6f;

        private static readonly List<Waystone> Stones = new List<Waystone>();

        public int Id => id;
        public string DisplayName => displayName;
        public float UseRange => useRange;

        /// <summary>Every waystone in the scene, by id.</summary>
        public static IReadOnlyList<Waystone> All => Stones;

        public static Waystone Get(int stoneId)
        {
            foreach (Waystone stone in Stones)
                if (stone.id == stoneId) return stone;
            return null;
        }

        /// <summary>The name of the main town, where Scrolls of Returning and Illusion Gate lead.</summary>
        public const string MainTownName = "St. Revere";

        /// <summary>The main town's stone (the first stone when there is none of that name; null without stones).</summary>
        public static Waystone MainTown
        {
            get
            {
                foreach (Waystone stone in Stones)
                    if (stone != null && stone.displayName == MainTownName) return stone;
                return Stones.Count > 0 ? Stones[0] : null;
            }
        }

        /// <summary>The stone within reach of a position (null when none).</summary>
        public static Waystone Near(Vector2 position, float extraRange)
        {
            Waystone nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (Waystone stone in Stones)
            {
                float distance = Vector2.Distance(position, stone.transform.position);
                if (distance <= stone.useRange + extraRange && distance < nearestDistance)
                {
                    nearest = stone;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        /// <summary>Where travellers arrive: just in front of the stone.</summary>
        public Vector2 ArrivalPoint => (Vector2)transform.position + new Vector2(0f, -0.6f);

        public void Configure(int stoneId, string stoneName)
        {
            id = stoneId;
            displayName = stoneName;
        }

        private void OnEnable()
        {
            Stones.Add(this);
            Stones.Sort((a, b) => a.id.CompareTo(b.id));
        }

        private void OnDisable() => Stones.Remove(this);
    }
}
