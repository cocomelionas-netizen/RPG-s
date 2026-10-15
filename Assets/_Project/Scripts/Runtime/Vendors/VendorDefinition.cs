using System;
using RPG.Items;
using UnityEngine;

namespace RPG.Vendors
{
    /// <summary>What a vendor NPC is called and what it sells (at each item's price).</summary>
    [CreateAssetMenu(menuName = "RPG/Vendors/Vendor", fileName = "Vendor")]
    public class VendorDefinition : ScriptableObject
    {
        public string displayName = "Vendor";
        [Tooltip("Shown under the name, for example \"Mage supplies\".")]
        public string title = "";
        public ItemDefinition[] stock = Array.Empty<ItemDefinition>();

        public bool Sells(ItemDefinition item) => item != null && Array.IndexOf(stock, item) >= 0;
    }
}
