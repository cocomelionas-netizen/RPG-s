using System;
using RPG.Items;
using UnityEngine;

namespace RPG.Vendors
{
    /// <summary>What a vendor NPC is called, what it sells (at each item's price) and how many of each, and what it transmutes.</summary>
    [CreateAssetMenu(menuName = "RPG/Vendors/Vendor", fileName = "Vendor")]
    public class VendorDefinition : ScriptableObject
    {
        public string displayName = "Vendor";
        [Tooltip("Shown under the name, for example \"Mage supplies\".")]
        public string title = "";
        public ItemDefinition[] stock = Array.Empty<ItemDefinition>();
        [Tooltip("How many of each stock item the vendor has, in the same order as Stock. 0 (or missing) means unlimited.")]
        public int[] stockLimits = Array.Empty<int>();
        [Tooltip("Seconds between restocks: limited items are refilled to their limit.")]
        [Min(10f)] public float restockSeconds = 300f;
        [Tooltip("A transmuter (the source game's Reginald): items put into its grid together make these recipes' results.")]
        public Recipe[] recipes = Array.Empty<Recipe>();

        [Header("Random quality (the source game's merchants)")]
        [Tooltip("Gear in stock gets a random quality at every restock, rolled best first with the chances below.")]
        public bool randomQuality;
        [Range(0f, 100f)] public float greenChance = 100f;
        [Range(0f, 100f)] public float blueChance = 50f;
        [Range(0f, 100f)] public float goldChance = 5f;
        [Range(0f, 100f)] public float purpleChance = 1f;

        /// <summary>A quality for a piece in stock: purple, gold, blue or green at their chances (best first), else plain.</summary>
        public ItemRarity RollQuality(float roll0, float roll1, float roll2, float roll3)
        {
            if (roll0 * 100f < purpleChance) return ItemRarity.Legendary;
            if (roll1 * 100f < goldChance) return ItemRarity.Epic;
            if (roll2 * 100f < blueChance) return ItemRarity.Rare;
            if (roll3 * 100f < greenChance) return ItemRarity.Uncommon;
            return ItemRarity.Common;
        }

        /// <summary>Whether it transmutes (has a transmute window).</summary>
        public bool Transmutes => recipes != null && recipes.Length > 0;

        /// <summary>The most of a stock item the vendor holds (0 for unlimited).</summary>
        public int LimitOf(int index) => index >= 0 && index < stockLimits.Length ? Mathf.Max(0, stockLimits[index]) : 0;

        public bool Sells(ItemDefinition item) => item != null && Array.IndexOf(stock, item) >= 0;
    }
}
