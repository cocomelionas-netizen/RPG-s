namespace RPG.EditorTools
{
    /// <summary>
    /// How Dreadmyst makes its gear, taken from its client data (<c>game.db</c>: <c>item_template</c>,
    /// <c>material_chance_weapon</c>, <c>material_chance_armor</c>) and the material names in <c>Dreadmyst.exe</c>.
    /// Every base item exists for every level 1-25 in each material; a drop of some level picks the material by the
    /// chance tables below, so better materials only appear from their own levels on.
    /// </summary>
    internal static class DreadmystGear
    {
        /// <summary>Weapon materials by tier (Dreadmyst weapon_material 1-7): swords, axes, maces, daggers.</summary>
        public static readonly string[] Metals = { "Bronze", "Steel", "Mythril", "Emerald", "Crystal", "Diamond", "Titanium" };

        /// <summary>Wood by tier (weapon_material 8-14): bows, staves and rods.</summary>
        public static readonly string[] Woods = { "Aspen", "Elm", "Cherry", "Black Walnut", "White Oak", "Hard Maple", "Hickory" };

        /// <summary>Armor materials by armor_type 1-15 (index 0 is unused).</summary>
        public static readonly string[] ArmorMaterials =
        {
            null, "Cotton", "Leather", "Brigandine", "Gambeson", "Bronze", "Steel", "Mythril", "Emerald",
            "Crystal", "Diamond", "Titanium", "Linen", "Wool", "Silk", "Spiritsilk",
        };

        /// <summary>The kind of armor each armor_type is.</summary>
        public enum ArmorKind { Cloth, Leather, Chain, Plate, Robe }

        public static ArmorKind KindOf(int armorType)
        {
            if (armorType <= 1) return ArmorKind.Cloth;
            if (armorType <= 4) return ArmorKind.Leather;
            if (armorType <= 8) return ArmorKind.Chain;
            if (armorType <= 11) return ArmorKind.Plate;
            return ArmorKind.Robe;
        }

        /// <summary>
        /// How strong an armor_type is, from its sell price (50 + 2.5 x tier x level for a chest): cloth 1, leather 2-4,
        /// chain 5-8, plate 9-11; robes cost like leather and chain (2, 4, 6, 8) but give mana instead of armor.
        /// </summary>
        public static int TierOf(int armorType) => armorType >= 12 ? 2 * (armorType - 11) : armorType;

        /// <summary>Base item names for head, chest and feet of each armor kind (Dreadmyst's own).</summary>
        public static (string head, string chest, string feet) NamesOf(ArmorKind kind)
        {
            switch (kind)
            {
                case ArmorKind.Leather: return ("Cap", "Jerkin", "Boots");
                case ArmorKind.Chain: return ("Coif", "Chainmail", "Greaves");
                case ArmorKind.Plate: return ("Helmet", "Breastplate", "Sabatons");
                case ArmorKind.Robe: return ("Hood", "Vestments", "Slippers");
                default: return ("Hat", "Shirt", "Shoes");
            }
        }

        /// <summary>
        /// material_chance_weapon: (tier 1-7, level, chance in %). Wood uses the same table for its tiers 8-14.
        /// A drop of a level picks one of the tiers listed for that level, weighted by chance.
        /// </summary>
        public static readonly (int tier, int level, float chance)[] WeaponChances =
        {
            (1, 1, 50f), (1, 2, 50f), (1, 3, 100f), (1, 4, 66.7f), (1, 5, 33.3f),
            (2, 2, 25f), (2, 3, 25f), (2, 4, 50f), (2, 5, 75f), (2, 6, 100f), (2, 7, 80f), (2, 8, 60f), (2, 9, 40f), (2, 10, 20f),
            (3, 6, 25f), (3, 7, 25f), (3, 8, 50f), (3, 9, 75f), (3, 10, 100f), (3, 11, 75f), (3, 12, 50f), (3, 13, 25f),
            (4, 9, 25f), (4, 10, 25f), (4, 11, 50f), (4, 12, 75f), (4, 13, 100f), (4, 14, 80f), (4, 15, 60f), (4, 16, 40f), (4, 17, 20f),
            (5, 13, 20f), (5, 14, 20f), (5, 15, 40f), (5, 16, 60f), (5, 17, 80f), (5, 18, 100f), (5, 19, 80f), (5, 20, 60f), (5, 21, 40f), (5, 22, 20f),
            (6, 19, 33.3f), (6, 20, 33.3f), (6, 21, 66.7f), (6, 22, 100f), (6, 23, 75f), (6, 24, 50f), (6, 25, 25f),
            (7, 25, 100f),
        };

        /// <summary>material_chance_armor: (armor_type, level, chance in %). Cotton (1) is never dropped.</summary>
        public static readonly (int type, int level, float chance)[] ArmorChances =
        {
            (2, 1, 33.3f), (2, 2, 33.3f), (2, 3, 66.7f), (2, 4, 100f), (2, 5, 75f), (2, 6, 50f), (2, 7, 25f),
            (3, 3, 33.3f), (3, 4, 33.3f), (3, 5, 66.7f), (3, 6, 100f), (3, 7, 66.7f), (3, 8, 33.3f),
            (4, 6, 50f), (4, 7, 50f), (4, 8, 100f), (4, 9, 50f),
            (5, 1, 10f), (5, 2, 10f), (5, 3, 20f), (5, 4, 30f), (5, 5, 40f), (5, 6, 50f), (5, 7, 60f), (5, 8, 70f), (5, 9, 80f), (5, 10, 90f),
            (5, 11, 100f), (5, 12, 90f), (5, 13, 80f), (5, 14, 70f), (5, 15, 60f), (5, 16, 50f), (5, 17, 40f), (5, 18, 30f), (5, 19, 20f), (5, 20, 10f),
            (6, 5, 10f), (6, 6, 10f), (6, 7, 20f), (6, 8, 30f), (6, 9, 40f), (6, 10, 50f), (6, 11, 60f), (6, 12, 70f), (6, 13, 80f), (6, 14, 90f),
            (6, 15, 100f), (6, 16, 90.9f), (6, 17, 81.8f), (6, 18, 72.7f), (6, 19, 63.6f), (6, 20, 54.5f), (6, 21, 45.5f), (6, 22, 36.4f),
            (6, 23, 27.3f), (6, 24, 18.2f), (6, 25, 9.1f),
            (7, 10, 12.5f), (7, 11, 12.5f), (7, 12, 25f), (7, 13, 37.5f), (7, 14, 50f), (7, 15, 62.5f), (7, 16, 75f), (7, 17, 87.5f), (7, 18, 100f),
            (7, 19, 87.5f), (7, 20, 75f), (7, 21, 62.5f), (7, 22, 50f), (7, 23, 37.5f), (7, 24, 25f), (7, 25, 12.5f),
            (8, 15, 20f), (8, 16, 20f), (8, 17, 40f), (8, 18, 60f), (8, 19, 80f), (8, 20, 100f), (8, 21, 83.3f), (8, 22, 66.7f), (8, 23, 50f),
            (8, 24, 33.3f), (8, 25, 16.7f),
            (9, 10, 12.5f), (9, 11, 12.5f), (9, 12, 25f), (9, 13, 37.5f), (9, 14, 50f), (9, 15, 62.5f), (9, 16, 75f), (9, 17, 87.5f), (9, 18, 100f),
            (9, 19, 87.5f), (9, 20, 75f), (9, 21, 62.5f), (9, 22, 50f), (9, 23, 37.5f), (9, 24, 25f), (9, 25, 12.5f),
            (10, 15, 20f), (10, 16, 20f), (10, 17, 40f), (10, 18, 60f), (10, 19, 80f), (10, 20, 100f), (10, 21, 83.3f), (10, 22, 66.7f),
            (10, 23, 50f), (10, 24, 33.3f), (10, 25, 16.7f),
            (11, 20, 33.3f), (11, 21, 33.3f), (11, 22, 66.7f), (11, 23, 100f), (11, 24, 66.7f), (11, 25, 33.3f),
            (12, 1, 20f), (12, 2, 20f), (12, 3, 40f), (12, 4, 60f), (12, 5, 80f), (12, 6, 100f), (12, 7, 80f), (12, 8, 60f), (12, 9, 40f), (12, 10, 20f),
            (13, 3, 11.1f), (13, 4, 11.1f), (13, 5, 22.2f), (13, 6, 33.3f), (13, 7, 44.4f), (13, 8, 55.6f), (13, 9, 66.7f), (13, 10, 77.8f),
            (13, 11, 88.9f), (13, 12, 100f), (13, 13, 88.9f), (13, 14, 77.8f), (13, 15, 66.7f), (13, 16, 55.6f), (13, 17, 44.4f), (13, 18, 33.3f),
            (13, 19, 22.2f), (13, 20, 11.1f),
            (14, 10, 12.5f), (14, 11, 12.5f), (14, 12, 25f), (14, 13, 37.5f), (14, 14, 50f), (14, 15, 62.5f), (14, 16, 75f), (14, 17, 87.5f),
            (14, 18, 100f), (14, 19, 87.5f), (14, 20, 75f), (14, 21, 62.5f), (14, 22, 50f), (14, 23, 37.5f), (14, 24, 25f), (14, 25, 12.5f),
            (15, 15, 20f), (15, 16, 20f), (15, 17, 40f), (15, 18, 60f), (15, 19, 80f), (15, 20, 100f), (15, 21, 83.3f), (15, 22, 66.7f),
            (15, 23, 50f), (15, 24, 33.3f), (15, 25, 16.7f),
        };

        /// <summary>Dreadmyst's sell price of a plain (white) weapon: 50 + 10 x level + 2 x level x (tier - 1).</summary>
        public static int WeaponSellPrice(int tier, int level) => 50 + 10 * level + 2 * level * (tier - 1);

        /// <summary>Dreadmyst's sell price of a plain chest piece: 50 + 2.5 x tier x level.</summary>
        public static float ArmorSellPrice(int armorType, int level) => 50f + 2.5f * TierOf(armorType) * level;
    }
}
