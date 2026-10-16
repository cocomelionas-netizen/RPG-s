using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// Server: rolls loot and leaves it on the ground in a loot bag (monsters, chests). The loot table gives the
    /// fixed drops (junk, potions, keys, gems). Gear itself is crafted: what drops are crafting materials, picked
    /// the way the source game picks the material of its gear drops (by the dropper's level and its material chances).
    /// Only guaranteed pieces (bosses) drop as finished gear.
    /// </summary>
    public static class LootDrop
    {
        /// <summary>Chance per roll that crafting materials drop.</summary>
        public const float MaterialChance = 0.4f;

        /// <summary>Chance that one more piece of material drops with them.</summary>
        public const float ExtraMaterialChance = 0.35f;

        /// <summary>
        /// The quality a crafted piece comes out at, best first (the rest are plain): gold rarely, blue sometimes,
        /// green often. A plainer piece is raised to the quality with an affix.
        /// </summary>
        public static readonly (ItemRarity quality, float chance)[] CraftChances =
        {
            (ItemRarity.Epic, 0.02f),
            (ItemRarity.Rare, 0.08f),
            (ItemRarity.Uncommon, 0.25f),
        };

        /// <summary>Gear from this many levels below the dropper up to <see cref="LevelsAbove"/> above it can drop.</summary>
        public const int LevelsBelow = 3;
        public const int LevelsAbove = 1;

        private static readonly List<LootItem> Drops = new List<LootItem>();
        private static readonly List<ItemDefinition> Pool = new List<ItemDefinition>();
        private static readonly List<float> Weights = new List<float>();

        /// <summary>
        /// How often gear of <paramref name="itemLevel"/> drops from a dropper of <paramref name="level"/>
        /// (like the source game's material tables: most often at its level, less the further off, none outside the window).
        /// </summary>
        public static float LevelWeight(int itemLevel, int level)
        {
            int difference = itemLevel - level;
            if (difference > LevelsAbove || difference < -LevelsBelow) return 0f;
            return difference >= 0
                ? 1f - difference * 0.5f / LevelsAbove
                : 1f + difference * 0.75f / LevelsBelow;
        }

        /// <summary>A quality from a table of chances (best first), or null when none of them rolls.</summary>
        public static ItemRarity? RollQuality((ItemRarity quality, float chance)[] chances)
        {
            float roll = Random.value;
            foreach ((ItemRarity quality, float chance) in chances)
            {
                if (roll < chance) return quality;
                roll -= chance;
            }
            return null;
        }

        /// <summary>
        /// A crafting material dropped by a dropper of <paramref name="level"/>: the material of a craftable piece
        /// picked like a gear drop (<see cref="PickGear"/>), so better materials only drop from their own levels on.
        /// </summary>
        public static bool PickMaterial(ItemCatalog catalog, int level, out ItemDefinition material)
        {
            material = null;
            if (!PickGear(catalog, null, level, ItemRarity.Legendary, 0f, out LootItem piece)) return false;
            material = piece.Item.craftMaterial;
            return material != null;
        }

        /// <summary>
        /// A piece of gear of <paramref name="quality"/> around <paramref name="level"/>: an item no better than that
        /// quality, weighted by <see cref="LevelWeight"/> and its <see cref="ItemDefinition.dropWeight"/>, raised to the quality with an affix when it is plainer.
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
                if (item == null || !item.IsEquippable || item.rarity > quality || item.dropWeight <= 0f) continue;
                float weight = LevelWeight(item.itemLevel, level) * item.dropWeight;
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
                    if (item == null || !item.IsEquippable || item.rarity > quality || item.dropWeight <= 0f
                        || item.itemLevel != bestFallbackLevel) continue;
                    Pool.Add(item);
                    Weights.Add(item.dropWeight);
                    total += item.dropWeight;
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
        /// Rolls every table entry once into <paramref name="into"/>: each drops at its chance, as many as it rolls.
        /// Gear can get an affix and a socket; a potion drops as the tier of <paramref name="level"/>.
        /// </summary>
        public static void RollEntries(LootEntry[] entries, int level, AffixCatalog affixes, float socketChance, List<LootItem> into)
        {
            if (entries == null) return;
            foreach (LootEntry entry in entries)
            {
                if (entry.item == null || Random.value >= entry.chance) continue;
                ItemDefinition item = PotionForLevel(entry.item, level);
                int count = entry.RollCount();
                if (item.IsEquippable)
                {
                    // Each piece of gear rolls its own affix and socket.
                    for (int i = 0; i < count; i++)
                    {
                        int affix = affixes != null ? affixes.Roll(item, level) : 0;
                        into.Add(new LootItem(item, affix, Random.value < socketChance ? ItemStack.EmptySocket : 0));
                    }
                }
                else
                {
                    into.Add(new LootItem(item, affixes != null ? affixes.Roll(item, level) : 0, 0, count));
                }
            }
        }

        /// <summary>
        /// Rolls every table entry and the material chance <paramref name="rolls"/> times (the material chance
        /// <paramref name="extraMaterialRolls"/> more), adds <paramref name="extra"/> (already rolled drops) and one
        /// finished piece of gear for each of <paramref name="guaranteed"/> (at that quality), and spawns a bag with
        /// what dropped. Gear listed in the table itself can get an affix and a socket too. False when nothing dropped.
        /// </summary>
        public static bool Spawn(GameObject bagPrefab, Vector2 position, LootEntry[] entries, ItemCatalog gear, AffixCatalog affixes,
            int level, float socketChance, int rolls = 1, int extraMaterialRolls = 0, List<LootItem> extra = null,
            params ItemRarity[] guaranteed)
        {
            if (bagPrefab == null) return false;
            Drops.Clear();
            for (int roll = 0; roll < rolls; roll++)
            {
                RollEntries(entries, level, affixes, socketChance, Drops);
                RollMaterials(gear, level);
            }
            if (extra != null) Drops.AddRange(extra);

            for (int roll = 0; roll < extraMaterialRolls; roll++)
                RollMaterials(gear, level);

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

        /// <summary>Chance that a potion drops as its serum (the stronger, instant version).</summary>
        public const float SerumChance = 0.15f;

        /// <summary>
        /// Like the source game's world loot: a potion in a loot table drops as the highest tier the dropper's level allows
        /// (Minor, Lesser, plain, Greater, Major), sometimes as its serum. Anything else is returned as it is.
        /// </summary>
        public static ItemDefinition PotionForLevel(ItemDefinition item, int level)
        {
            if (item == null || item.use == ItemUse.None) return item;
            while (item.combinesInto != null && item.combinesInto.use == item.use && item.combinesInto.rarity == item.rarity
                   && item.combinesInto.itemLevel <= level)
                item = item.combinesInto;
            return item.strongerVariant != null && Random.value < SerumChance ? item.strongerVariant : item;
        }

        /// <summary>Server: spawns a loot bag with these items (null when there is no prefab or nothing to put in).</summary>
        public static LootBag SpawnBag(GameObject bagPrefab, Vector2 position, List<LootItem> items)
        {
            if (bagPrefab == null || items == null || items.Count == 0) return null;
            GameObject bag = Object.Instantiate(bagPrefab, position, Quaternion.identity);
            var loot = bag.GetComponent<LootBag>();
            loot.ServerSetContents(items);
            bag.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            return loot;
        }

        private static void RollMaterials(ItemCatalog gear, int level)
        {
            if (Random.value >= MaterialChance || !PickMaterial(gear, level, out ItemDefinition material)) return;
            Drops.Add(new LootItem(material, 0));
            if (Random.value < ExtraMaterialChance) Drops.Add(new LootItem(material, 0));
        }
    }
}
