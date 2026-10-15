using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>Server: rolls a loot table and leaves the result on the ground in a loot bag (monsters, chests).</summary>
    public static class LootDrop
    {
        private static readonly List<LootItem> Drops = new List<LootItem>();

        /// <summary>
        /// Rolls every entry <paramref name="rolls"/> times; gear can get an affix (for <paramref name="level"/>) and a socket.
        /// Each of <paramref name="guaranteed"/> adds one piece of gear from the table with an affix of that quality.
        /// Spawns a bag with what dropped. False when nothing dropped.
        /// </summary>
        public static bool Spawn(GameObject bagPrefab, Vector2 position, LootEntry[] entries, AffixCatalog affixes, int level,
            float socketChance, int rolls = 1, params ItemRarity[] guaranteed)
        {
            if (bagPrefab == null || entries == null) return false;
            Drops.Clear();
            for (int roll = 0; roll < rolls; roll++)
            {
                foreach (LootEntry entry in entries)
                {
                    if (entry.item == null || Random.value >= entry.chance) continue;
                    int affix = affixes != null ? affixes.Roll(entry.item, level) : 0;
                    int socket = entry.item.IsEquippable && Random.value < socketChance ? ItemStack.EmptySocket : 0;
                    Drops.Add(new LootItem(entry.item, affix, socket));
                }
            }

            if (guaranteed != null && guaranteed.Length > 0 && affixes != null)
            {
                var gear = new List<ItemDefinition>();
                foreach (LootEntry entry in entries)
                    if (entry.item != null && entry.item.IsEquippable && entry.item.canHaveAffix) gear.Add(entry.item);
                foreach (ItemRarity quality in guaranteed)
                {
                    if (gear.Count == 0) break;
                    ItemDefinition item = gear[Random.Range(0, gear.Count)];
                    int socket = Random.value < socketChance ? ItemStack.EmptySocket : 0;
                    Drops.Add(new LootItem(item, affixes.RollQuality(item, level, quality), socket));
                }
            }
            if (Drops.Count == 0) return false;

            GameObject bag = Object.Instantiate(bagPrefab, position, Quaternion.identity);
            bag.GetComponent<LootBag>().ServerSetContents(Drops);
            bag.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            return true;
        }
    }
}
