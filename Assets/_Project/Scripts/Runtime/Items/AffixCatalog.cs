using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RPG.Items
{
    /// <summary>
    /// Every affix, and the rolling of one for a drop. Items store an affix as its index here plus one
    /// (0 means none), so affixes must only be added at the end.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Items/Affix Catalog", fileName = "AffixCatalog")]
    public class AffixCatalog : ScriptableObject
    {
        public AffixDefinition[] affixes = Array.Empty<AffixDefinition>();

        [Tooltip("Chance that a dropped weapon or armor piece rolls an affix at all.")]
        [Range(0f, 1f)] public float affixChance = 0.6f;

        private readonly List<AffixDefinition> candidates = new List<AffixDefinition>();

        public AffixDefinition Get(int id) => id >= 1 && id <= affixes.Length ? affixes[id - 1] : null;

        public int IdOf(AffixDefinition affix) => affix != null ? Array.IndexOf(affixes, affix) + 1 : 0;

        /// <summary>How often a quality is picked among the possible affixes: rarer qualities much less often.</summary>
        public static float Weight(ItemRarity quality)
        {
            switch (quality)
            {
                case ItemRarity.Uncommon: return 60f;
                case ItemRarity.Rare: return 28f;
                case ItemRarity.Epic: return 10f;
                case ItemRarity.Legendary: return 2f;
                default: return 0f;
            }
        }

        /// <summary>An affix id for an item dropped by a monster of <paramref name="monsterLevel"/> (0 for none).</summary>
        public int Roll(ItemDefinition item, int monsterLevel)
        {
            if (item == null || !item.IsEquippable || !item.canHaveAffix || Random.value >= affixChance) return 0;

            candidates.Clear();
            float total = 0f;
            foreach (AffixDefinition affix in affixes)
            {
                if (affix == null || affix.retired || monsterLevel < affix.minLevel || monsterLevel > affix.maxLevel) continue;
                candidates.Add(affix);
                total += Weight(affix.quality);
            }
            if (total <= 0f) return 0;

            float pick = Random.value * total;
            foreach (AffixDefinition affix in candidates)
            {
                pick -= Weight(affix.quality);
                if (pick <= 0f) return IdOf(affix);
            }
            return IdOf(candidates[candidates.Count - 1]);
        }

        /// <summary>
        /// An affix of exactly <paramref name="quality"/> for a guaranteed drop (the source game's bosses always drop a gold
        /// item, for example). Prefers affixes meant for <paramref name="monsterLevel"/>, otherwise those of that quality
        /// closest to the level, otherwise the best quality there is.
        /// </summary>
        public int RollQuality(ItemDefinition item, int monsterLevel, ItemRarity quality)
        {
            if (item == null || !item.IsEquippable || !item.canHaveAffix) return 0;

            candidates.Clear();
            int closest = int.MaxValue;
            foreach (AffixDefinition affix in affixes)
            {
                if (affix == null || affix.retired || affix.quality != quality) continue;
                int distance = monsterLevel < affix.minLevel ? affix.minLevel - monsterLevel
                    : monsterLevel > affix.maxLevel ? monsterLevel - affix.maxLevel : 0;
                if (distance > closest) continue;
                if (distance < closest) candidates.Clear();
                closest = distance;
                candidates.Add(affix);
            }
            if (candidates.Count == 0)
                foreach (AffixDefinition affix in affixes)
                    if (affix != null && !affix.retired && (candidates.Count == 0 || affix.quality > candidates[0].quality))
                    {
                        candidates.Clear();
                        candidates.Add(affix);
                    }
            return candidates.Count > 0 ? IdOf(candidates[Random.Range(0, candidates.Count)]) : 0;
        }
    }
}
