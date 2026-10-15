using System;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>One possible drop: an item, the chance (0..1) that a kill drops it, and how many (1 when not set).</summary>
    [Serializable]
    public struct LootEntry
    {
        public ItemDefinition item;
        [Range(0f, 1f)] public float chance;
        [Tooltip("How many drop: from min to max (0 counts as 1).")]
        [Min(0)] public int minCount;
        [Min(0)] public int maxCount;

        public LootEntry(ItemDefinition item, float chance, int minCount = 1, int maxCount = 1)
        {
            this.item = item;
            this.chance = chance;
            this.minCount = minCount;
            this.maxCount = maxCount;
        }

        /// <summary>A count between min and max (at least 1).</summary>
        public int RollCount()
        {
            int low = Mathf.Max(1, minCount);
            return UnityEngine.Random.Range(low, Mathf.Max(low, maxCount) + 1);
        }
    }
}
