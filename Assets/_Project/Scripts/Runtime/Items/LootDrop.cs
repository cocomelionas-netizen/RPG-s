using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// Server: rolls loot and leaves it on the ground in a loot bag (monsters, chests). The loot table gives the
    /// fixed drops (junk, potions, keys, gems); gear comes by level, the way Dreadmyst does it: a piece of the
    /// quality that rolls, picked from the gear around the dropper's level.
    /// </summary>
    public static class LootDrop
    {
        /// <summary>
        /// Chance per roll of a gear drop of each quality, best first (stand-ins for Dreadmyst's server values,
        /// tiered like its bosses: gold rarely, blue sometimes, green more often, plain white gear now and then).
        /// </summary>
        public static readonly (ItemRarity quality, float chance)[] GearChances =
        {
            (ItemRarity.Epic, 0.003f),
            (ItemRarity.Rare, 0.02f),
            (ItemRarity.Uncommon, 0.08f),
            (ItemRarity.Common, 0.06f),
        };

        /// <summary>Gear from this many levels below the dropper up to <see cref="LevelsAbove"/> above it can drop.</summary>
        public const int LevelsBelow = 3;
        public const int LevelsAbove = 1;

        private static readonly List<LootItem> Drops = new List<LootItem>();
        private static readonly List<ItemDefinition> Pool = new List<ItemDefinition>();
        private static readonly List<float> Weights = new List<float>();

        /// <summary>
        /// How often gear of <paramref name="itemLevel"/> drops from a dropper of <paramref name="level"/>
        /// (like Dreadmyst's material tables: most often at its level, less the further off, none outside the window).
        /// </summary>
        public static float LevelWeight(int itemLevel, int level)
        {
            int difference = itemLevel - level;
            if (difference > LevelsAbove || difference < -LevelsBelow) return 0f;
            return difference >= 0
                ? 1f - difference * 0.5f / LevelsAbove
                : 1f + difference * 0.75f / LevelsBelow;
        }

        /// <summary>The quality of a gear drop for one roll, or null when no gear drops.</summary>
        public static ItemRarity? RollGearQuality()
        {
            float roll = Random.value;
            foreach ((ItemRarity quality, float chance) in GearChances)
            {
                if (roll < chance) return quality;
                roll -= chance;
            }
            return null;
        }

        /// <summary>
        /// A piece of gear of <paramref name="quality"/> around <paramref name="level"/>: an item no better than that
        /// quality, weighted by <see cref="LevelWeight"/>, raised to the quality with an affix when it is plainer.
        /// When nothing fits the level window, the highest-level gear below it is used. False when there is no gear.
        /// </summary>
        public static bool PickGear(ItemCatalog catalog, AffixCatalog affixes, int level, ItemRarity quality, float socketChance,
            out LootItem drop)
        {
            drop = default;
            if (catalog == null) return false;

            Pool.Clear();
            Weights.Clear();
            float total = 0f;
            int bestFallbackLevel = 0;
            foreach (ItemDefinition item in catalog.items)
            {
                if (item == null || !item.IsEquippable || item.rarity > quality) continue;
                float weight = LevelWeight(item.itemLevel, level);
                if (weight > 0f)
                {
                    Pool.Add(item);
                    Weights.Add(weight);
                    total += weight;
                }
                else if (item.itemLevel <= level) bestFallbackLevel = Mathf.Max(bestFallbackLevel, item.itemLevel);
            }
            if (total <= 0f)
            {
                // Higher than any gear made yet: the best gear there is.
                foreach (ItemDefinition item in catalog.items)
                {
                    if (item == null || !item.IsEquippable || item.rarity > quality || item.itemLevel != bestFallbackLevel) continue;
                    Pool.Add(item);
                    Weights.Add(1f);
                    total += 1f;
                }
                if (total <= 0f) return false;
            }

            float pick = Random.value * total;
            ItemDefinition chosen = Pool[Pool.Count - 1];
            for (int i = 0; i < Pool.Count; i++)
            {
                pick -= Weights[i];
                if (pick > 0f) continue;
                chosen = Pool[i];
                break;
            }

            int affix = 0;
            if (affixes != null)
                affix = chosen.rarity < quality && quality > ItemRarity.Common ? affixes.RollQuality(chosen, level, quality) : affixes.Roll(chosen, level);
            int socket = Random.value < socketChance ? ItemStack.EmptySocket : 0;
            drop = new LootItem(chosen, affix, socket);
            return true;
        }

        /// <summary>
        /// Rolls every table entry and the gear chance <paramref name="rolls"/> times (the gear chance
        /// <paramref name="extraGearRolls"/> more), adds one piece of gear for each of <paramref name="guaranteed"/>
        /// (at that quality), and spawns a bag with what dropped. Gear listed in the table itself can get an affix and
        /// a socket too. False when nothing dropped.
        /// </summary>
        public static bool Spawn(GameObject bagPrefab, Vector2 position, LootEntry[] entries, ItemCatalog gear, AffixCatalog affixes,
            int level, float socketChance, int rolls = 1, int extraGearRolls = 0, params ItemRarity[] guaranteed)
        {
            if (bagPrefab == null) return false;
            Drops.Clear();
            for (int roll = 0; roll < rolls; roll++)
            {
                if (entries != null)
                {
                    foreach (LootEntry entry in entries)
                    {
                        if (entry.item == null || Random.value >= entry.chance) continue;
                        int affix = affixes != null ? affixes.Roll(entry.item, level) : 0;
                        int socket = entry.item.IsEquippable && Random.value < socketChance ? ItemStack.EmptySocket : 0;
                        Drops.Add(new LootItem(entry.item, affix, socket));
                    }
                }

                ItemRarity? quality = RollGearQuality();
                if (quality.HasValue && PickGear(gear, affixes, level, quality.Value, socketChance, out LootItem drop))
                    Drops.Add(drop);
            }

            for (int roll = 0; roll < extraGearRolls; roll++)
            {
                ItemRarity? quality = RollGearQuality();
                if (quality.HasValue && PickGear(gear, affixes, level, quality.Value, socketChance, out LootItem drop))
                    Drops.Add(drop);
            }

            if (guaranteed != null)
                foreach (ItemRarity quality in guaranteed)
                    if (PickGear(gear, affixes, level, quality, socketChance, out LootItem drop))
                        Drops.Add(drop);
            if (Drops.Count == 0) return false;

            GameObject bag = Object.Instantiate(bagPrefab, position, Quaternion.identity);
            bag.GetComponent<LootBag>().ServerSetContents(Drops);
            bag.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            return true;
        }
    }
}
