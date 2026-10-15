using System;
using RPG.Items;
using UnityEngine;

namespace RPG.Vendors
{
    /// <summary>What a vendor NPC is called, what it sells (at each item's price) and how many of each, and what it crafts.</summary>
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
        [Tooltip("A jeweler: combines three of the same gem into a better one.")]
        public bool combinesGems;
        [Tooltip("Gear this vendor crafts from materials (each piece's craftMaterial and craftCount).")]
        public ItemDefinition[] crafts = Array.Empty<ItemDefinition>();

        /// <summary>The most of a stock item the vendor holds (0 for unlimited).</summary>
        public int LimitOf(int index) => index >= 0 && index < stockLimits.Length ? Mathf.Max(0, stockLimits[index]) : 0;

        public bool Sells(ItemDefinition item) => item != null && Array.IndexOf(stock, item) >= 0;

        /// <summary>The craft list entry of a piece, or -1.</summary>
        public int CraftIndexOf(ItemDefinition item) => item != null ? Array.IndexOf(crafts, item) : -1;
    }
}
