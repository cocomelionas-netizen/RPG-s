using System;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// All items. Bags and loot store an item as its index here plus one (0 means an empty slot),
    /// so items must only be added at the end, never reordered or removed.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Items/Item Catalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        public ItemDefinition[] items = Array.Empty<ItemDefinition>();

        /// <summary>The item with this id (index plus one), or null for 0 and unknown ids.</summary>
        public ItemDefinition Get(int id)
        {
            return id >= 1 && id <= items.Length ? items[id - 1] : null;
        }

        /// <summary>The id of an item (index plus one), or 0 when it is not in the catalog.</summary>
        public int IdOf(ItemDefinition item)
        {
            return item != null ? Array.IndexOf(items, item) + 1 : 0;
        }
    }
}
