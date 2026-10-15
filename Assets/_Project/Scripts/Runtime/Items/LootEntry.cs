using System;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>One possible drop: an item and the chance (0..1) that a kill drops it.</summary>
    [Serializable]
    public struct LootEntry
    {
        public ItemDefinition item;
        [Range(0f, 1f)] public float chance;

        public LootEntry(ItemDefinition item, float chance)
        {
            this.item = item;
            this.chance = chance;
        }
    }
}
