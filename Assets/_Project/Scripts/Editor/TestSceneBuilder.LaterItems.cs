using System.Collections.Generic;
using RPG.Items;
using RPG.Progression;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// The source game's items added after the catalog's first order (players keep item ids, so these go at its end):
    /// the other orb tiers, the attribute scrolls, Flawless and Draconic gems, Draconic Treasure, and the level 1 Tomes.
    /// Made with the other items (the dungeon loot refers to them by file, Data/Items/Later), put into the catalog after
    /// the Tomes (CreateClasses).
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private static readonly List<ItemDefinition> LaterItems = new List<ItemDefinition>();

        /// <summary>The source game's quality numbers (2 green ... 6 purple) as this project's rarities.</summary>
        private static ItemRarity SourceQuality(int quality) =>
            quality <= 2 ? ItemRarity.Common : quality == 3 ? ItemRarity.Uncommon : quality == 4 ? ItemRarity.Rare
            : quality == 5 ? ItemRarity.Epic : ItemRarity.Legendary;

        private static void CreateLaterItems()
        {
            LaterItems.Clear();

            // Orbs (item_template 924-935, 1140-1142): sell price and buy cost ratio; Major and Divine orbs already exist.
            foreach ((string kind, Color color) in new[] { ("Accessory", new Color(0.4f, 0.8f, 0.5f)), ("Armor", new Color(0.3f, 0.5f, 0.9f)),
                         ("Weapon", new Color(0.85f, 0.3f, 0.25f)) })
            {
                foreach ((string tier, int quality, int sell, int ratio) in new[] { ("Minor", 2, 62, 6), ("Lesser", 3, 260, 6), ("Greater", 4, 975, 6) })
                    LaterItems.Add(Orb(tier, kind, quality, sell, ratio, color));
            }
            LaterItems.Add(Orb("Major", "Weapon", 5, 2532, 50, new Color(0.85f, 0.3f, 0.25f)));
            foreach ((string kind, Color color) in new[] { ("Accessory", new Color(0.4f, 0.8f, 0.5f)), ("Armor", new Color(0.3f, 0.5f, 0.9f)),
                         ("Weapon", new Color(0.85f, 0.3f, 0.25f)) })
            {
                ItemDefinition arcane = Orb("Arcane", kind, 6, 6000, 75, color);
                arcane.description = "Takes an item's level beyond its normal limit.";
                LaterItems.Add(arcane);
            }

            // Attribute scrolls I-V (1085-1109, spells 246-270): +4/7/11/14/21 of the source game's attribute for 5 minutes,
            // twice that here (this project's attribute scale, like the gems).
            int[] levels = { 4, 8, 14, 18, 23 };
            int[] sells = { 55, 82, 123, 185, 278 };
            int[] amounts = { 4, 7, 11, 14, 21 };
            string[] numerals = { "I", "II", "III", "IV", "V" };
            foreach ((string stat, Attribute attribute) in new[] { ("Intellect", Attribute.Intelligence), ("Willpower", Attribute.Willpower),
                         ("Courage", Attribute.Vitality), ("Agility", Attribute.Agility), ("Strength", Attribute.Strength) })
            {
                for (int tier = 0; tier < numerals.Length; tier++)
                {
                    ItemDefinition scroll = CreateItem($"Later/ScrollOf{stat}{numerals[tier]}", $"Scroll of {stat} {numerals[tier]}", ItemSlot.None,
                        ItemRarity.Uncommon, 0, 0, 0, new Color(0.85f, 0.8f, 0.6f), sells[tier] * 3, -1, levels[tier]);
                    scroll.sellValue = sells[tier];
                    scroll.use = ItemUse.Buff;
                    scroll.buffAttribute = attribute;
                    scroll.buffAmount = amounts[tier] * 2;
                    scroll.buffSeconds = 300f;
                    scroll.maxStack = 5;
                    scroll.canHaveAffix = false;
                    scroll.dropWeight = 0f;
                    LaterItems.Add(scroll);
                }
            }

            // Flawless (+4 in the source game) and Draconic (+5) gems, twice that here; made by transmutation (and dropped).
            foreach ((string tier, ItemRarity rarity, int amount, int sell) in new[] { ("Flawless", ItemRarity.Epic, 8, 750), ("Draconic", ItemRarity.Legendary, 10, 1000) })
            {
                foreach ((string gemName, Attribute attribute, Color color) in GemKinds)
                {
                    ItemDefinition gem = CreateGem($"Later/{tier}{gemName}", $"{tier} {gemName}", rarity, attribute, amount,
                        color * (tier == "Flawless" ? 1.25f : 1.4f), sell * 6, null);
                    gem.sellValue = sell;
                    gem.maxStack = 5;
                    LaterItems.Add(gem);
                }
            }

            // Draconic Treasure (1237): the wyrms' rare drop, Jipples's ware and a Draconic gem ingredient.
            ItemDefinition treasure = CreateItem("Later/DraconicTreasure", "Draconic Treasure", ItemSlot.None, ItemRarity.Rare, 0, 0, 0,
                new Color(0.95f, 0.45f, 0.2f), 1500 * 1000, -1, 25);
            treasure.sellValue = 1500;
            treasure.maxStack = 5;
            treasure.canHaveAffix = false;
            treasure.dropWeight = 0f;
            treasure.description = "Gleaming gold and gems, a taste of true wyrm wealth.";
            LaterItems.Add(treasure);
        }

        private static ItemDefinition Orb(string tier, string kind, int quality, int sell, int ratio, Color color)
        {
            ItemDefinition orb = CreateItem($"Later/{tier}{kind}Orb", $"{tier} {kind} Orb", ItemSlot.None, SourceQuality(quality), 0, 0, 0,
                color, sell * ratio);
            orb.sellValue = sell;
            orb.maxStack = 5;
            orb.canHaveAffix = false;
            orb.dropWeight = 0f;
            return orb;
        }
    }
}
