using System.Text;
using RPG.Progression;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>Where an item is worn. None: not equippable (potions, materials).</summary>
    public enum ItemSlot
    {
        None,
        Weapon,
        Head,
        Chest,
        Feet,
    }

    /// <summary>Quality, from vendor junk to the rarest finds. Random affixes can raise an item's quality.</summary>
    public enum ItemRarity
    {
        Poor,
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>What happens when the item is used from the bag.</summary>
    public enum ItemUse
    {
        None,
        /// <summary>Restores a share of max health and is used up.</summary>
        Heal,
        /// <summary>Restores a share of max mana and is used up.</summary>
        RestoreMana,
    }

    /// <summary>One kind of item: its slot, rarity, stats and use. Players' bags refer to it by catalog index.</summary>
    [CreateAssetMenu(menuName = "RPG/Items/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Item";
        public ItemSlot slot = ItemSlot.None;
        public ItemRarity rarity = ItemRarity.Common;

        [Header("Stats while equipped")]
        [Tooltip("Added to auto-attack (and so ability) damage.")]
        [Min(0)] public int damageBonus;
        [Min(0)] public int healthBonus;
        [Tooltip("Reduces damage taken: 100 armor halves it.")]
        [Min(0)] public int armor;
        [Min(0)] public int manaBonus;
        public AttributeSet attributes;
        [Tooltip("Random affixes can be rolled on it when it drops (weapons and armor).")]
        public bool canHaveAffix = true;

        [Header("Trade and restrictions")]
        [Tooltip("Price at vendors in gold; vendors buy it back for a quarter of this.")]
        [Min(0)] public int price = 10;
        [Tooltip("Index of the class (in the class catalog) that may equip it; -1 for every class.")]
        public int requiredClass = -1;
        [Tooltip("The item's level: monsters drop gear around their own level, and a player needs this level to wear it.")]
        [Range(1, 25)] public int itemLevel = 1;

        [Header("Use")]
        public ItemUse use = ItemUse.None;
        [Range(0f, 1f)] public float healFraction = 0.35f;
        [Min(1)] public int maxStack = 1;

        [Header("Gem")]
        [Tooltip("A gem: clicking it sets it into an empty socket of worn gear, where its attributes count.")]
        public bool isGem;
        [Tooltip("A jeweler turns three of this gem into one of these.")]
        public ItemDefinition combinesInto;

        [Header("Look")]
        [Tooltip("Color of the item's square in the bag until it has an icon.")]
        public Color iconColor = new Color(0.5f, 0.5f, 0.5f);

        public bool IsEquippable => slot != ItemSlot.None;

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Poor: return new Color(0.6f, 0.6f, 0.6f);
                case ItemRarity.Uncommon: return new Color(0.35f, 0.9f, 0.35f);
                case ItemRarity.Rare: return new Color(0.35f, 0.6f, 1f);
                // Source's order: green, blue, gold, then purple at the top.
                case ItemRarity.Epic: return new Color(1f, 0.78f, 0.2f);
                case ItemRarity.Legendary: return new Color(0.75f, 0.4f, 1f);
                default: return new Color(0.92f, 0.92f, 0.92f);
            }
        }

        /// <summary>What a vendor pays for one (a quarter of the price, at least 1 for anything with a price).</summary>
        public int SellPrice => price > 0 ? Mathf.Max(1, price / 4) : 0;

        /// <summary>Sell price with an affix: better affixes make an item worth more.</summary>
        public int SellPriceWith(AffixDefinition affix)
        {
            if (affix == null) return SellPrice;
            return Mathf.Max(1, Mathf.RoundToInt(SellPrice * affix.priceMultiplier));
        }

        /// <summary>"Sturdy Iron Helm" or "Iron Sword of the Bear".</summary>
        public string NameWith(AffixDefinition affix)
        {
            if (affix == null) return displayName;
            return affix.isPrefix ? $"{affix.displayName} {displayName}" : $"{displayName} {affix.displayName}";
        }

        /// <summary>The better of the item's own quality and its affix's.</summary>
        public ItemRarity QualityWith(AffixDefinition affix)
        {
            return affix != null && affix.quality > rarity ? affix.quality : rarity;
        }

        public bool CanBeUsedByClass(int classIndex) => requiredClass < 0 || requiredClass == classIndex;

        /// <summary>Whether a player of <paramref name="playerLevel"/> may wear it (only gear has a level requirement).</summary>
        public bool CanBeWornAtLevel(int playerLevel) => !IsEquippable || playerLevel >= itemLevel;

        /// <summary>Tooltip text: name, quality and slot, stats and use, with an affix when the item has one.</summary>
        /// <param name="className">Name of the required class to show (null when anyone may use it).</param>
        /// <param name="socketLine">"Socket: empty" or the gem in it (null when the item has no socket).</param>
        /// <param name="sellPrice">What a vendor pays, when it differs from the item's own (a gem in its socket); -1 to work it out.</param>
        public string Describe(string className = null, AffixDefinition affix = null, string socketLine = null, int sellPrice = -1)
        {
            var text = new StringBuilder();
            text.AppendLine(NameWith(affix));
            text.Append(QualityWith(affix));
            if (IsEquippable) text.Append(' ').Append(slot);
            text.AppendLine();

            int damage = damageBonus + (affix != null ? affix.damageBonus : 0);
            int health = healthBonus + (affix != null ? affix.healthBonus : 0);
            int totalArmor = armor + (affix != null ? affix.armor : 0);
            int mana = manaBonus + (affix != null ? affix.manaBonus : 0);
            AttributeSet totalAttributes = attributes + (affix != null ? affix.attributes : default);
            if (damage > 0) text.AppendLine($"+{damage} damage");
            if (health > 0) text.AppendLine($"+{health} health");
            if (mana > 0) text.AppendLine($"+{mana} mana");
            if (totalArmor > 0) text.AppendLine($"+{totalArmor} armor");
            if (!totalAttributes.IsZero) text.AppendLine(totalAttributes.Describe("\n"));
            if (!string.IsNullOrEmpty(socketLine)) text.AppendLine(socketLine);
            if (isGem) text.AppendLine("Set into gear with an empty socket");
            if (isGem && combinesInto != null) text.AppendLine($"3 combine into {combinesInto.displayName} at a jeweler");
            if (use == ItemUse.Heal) text.AppendLine($"Use: restores {Mathf.RoundToInt(healFraction * 100f)}% health");
            if (use == ItemUse.RestoreMana) text.AppendLine($"Use: restores {Mathf.RoundToInt(healFraction * 100f)}% mana");
            if (maxStack > 1) text.AppendLine($"Stacks up to {maxStack}");
            if (!string.IsNullOrEmpty(className)) text.AppendLine($"{className} only");
            if (IsEquippable) text.AppendLine($"Requires level {itemLevel}");
            if (price > 0) text.AppendLine($"Sells for {(sellPrice >= 0 ? sellPrice : SellPriceWith(affix))} gold");
            text.Append(IsEquippable ? "Click to equip" : use != ItemUse.None ? "Click to use" : isGem ? "Click, then click the item to set it into" : "");
            return text.ToString().TrimEnd();
        }
    }
}
