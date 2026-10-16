using System.Text;
using RPG.Progression;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>Where an item is worn (the source game's equipment slots). None: not equippable (potions, materials).</summary>
    public enum ItemSlot
    {
        None,
        Weapon,
        Head,
        Chest,
        Feet,
        /// <summary>The source game's Necklace.</summary>
        Neck,
        /// <summary>The source game's Belt.</summary>
        Waist,
        Legs,
        Hands,
        /// <summary>Worn on either of the two ring slots.</summary>
        Ring,
        /// <summary>The source game's Offhand.</summary>
        Shield,
    }

    /// <summary>What kind of weapon a weapon is: its source-game weapon skill raises its damage.</summary>
    public enum WeaponType
    {
        None,
        Staff,
        Mace,
        Axe,
        Sword,
        Ranged,
        Dagger,
        Wand,
        Shield,
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
        /// <summary>A Tome: teaches its spell and is used up.</summary>
        LearnSpell,
        /// <summary>Put together with the items it combines with into another (the source game's key recipes); all are used up.</summary>
        Combine,
        /// <summary>A Scroll of Returning: after its cast (standing still) takes the player to the main town; used up.</summary>
        Return,
        /// <summary>Food: restores Restore Amount of health and of mana at once, not in combat; used up.</summary>
        Food,
    }

    /// <summary>One kind of item: its slot, rarity, stats and use. Players' bags refer to it by catalog index.</summary>
    [CreateAssetMenu(menuName = "RPG/Items/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Item";
        [Tooltip("Flavor text shown in the tooltip.")]
        [TextArea] public string description;
        public ItemSlot slot = ItemSlot.None;
        public ItemRarity rarity = ItemRarity.Common;
        [Tooltip("For weapons: which weapon skill raises its damage.")]
        public WeaponType weaponType = WeaponType.None;

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
        [Tooltip("Always rolls an affix when it drops (the source game's necklaces, belts and rings are worth their stats).")]
        public bool alwaysAffix;

        [Header("Trade and restrictions")]
        [Tooltip("Price at vendors in gold (the source game's: its sell price times its buy cost ratio).")]
        [Min(0)] public int price = 10;
        [Tooltip("What vendors pay for it (the source game's sell price); 0: a quarter of the price.")]
        [Min(0)] public int sellValue;
        [Tooltip("Index of the class (in the class catalog) that may equip it; -1 for every class.")]
        public int requiredClass = -1;
        [Tooltip("Classes that may wear it, one bit per class index (the source game's class armor); 0: every class.")]
        public int classMask;
        [Tooltip("The item's level: monsters drop gear around their own level, and a player needs this level to wear (or drink) it.")]
        [Range(1, 25)] public int itemLevel = 1;
        [Tooltip("How often it drops among the gear of its level, like the source game's material chance; 0: never drops by level (only from loot tables and vendors).")]
        [Min(0f)] public float dropWeight = 1f;

        [Header("Use")]
        public ItemUse use = ItemUse.None;
        [Tooltip("Share of max health or mana restored, when Restore Amount is 0.")]
        [Range(0f, 1f)] public float healFraction = 0.35f;
        [Tooltip("Health or mana restored in all (the source game's potions and serums); 0 uses Heal Fraction instead.")]
        [Min(0)] public int restoreAmount;
        [Tooltip("Seconds the restoring takes, a share every 2 seconds (the source game's potions: 20); 0: all at once (serums).")]
        [Min(0f)] public float restoreSeconds;
        [Min(1)] public int maxStack = 1;
        [Tooltip("A stronger version that sometimes drops in its place (a serum for a potion).")]
        public ItemDefinition strongerVariant;
        [Tooltip("Return scrolls: seconds of standing still before it works (the source game: 2).")]
        [Min(0f)] public float castSeconds;
        [Tooltip("Return scrolls and food: seconds before another can be used (the source game: 300 and 30).")]
        [Min(0f)] public float useCooldown;
        [Tooltip("Tomes: the spell it teaches.")]
        public AbilityDefinition teachesSpell;

        [Tooltip("Combine: the other items it takes (one of each), and what they make.")]
        public ItemDefinition[] combineWith = System.Array.Empty<ItemDefinition>();
        public ItemDefinition combineResult;

        [Header("Gem")]
        [Tooltip("A gem: clicking it sets it into an empty socket of worn gear, where its attributes count.")]
        public bool isGem;
        [Tooltip("The next tier (Combine Count of this make one at the transmuter). For potions also the next tier that drops at its level.")]
        public ItemDefinition combinesInto;
        [Min(2)] public int combineCount = 3;

        [Header("Look")]
        [Tooltip("Color of the item's square in the bag until it has an icon.")]
        public Color iconColor = new Color(0.5f, 0.5f, 0.5f);

        public bool IsEquippable => slot != ItemSlot.None;

        /// <summary>The weapon skill of a weapon type (null for none).</summary>
        public static Attribute? SkillOf(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Staff: return Attribute.Staves;
                case WeaponType.Mace: return Attribute.Maces;
                case WeaponType.Axe: return Attribute.Axes;
                case WeaponType.Sword: return Attribute.Swords;
                case WeaponType.Ranged: return Attribute.Ranged;
                case WeaponType.Dagger: return Attribute.Daggers;
                case WeaponType.Wand: return Attribute.Wands;
                case WeaponType.Shield: return Attribute.Shields;
                default: return null;
            }
        }

        /// <summary>Whether a player of <paramref name="playerLevel"/> may drink it (potions have a level like gear).</summary>
        public bool CanBeUsedAtLevel(int playerLevel) => use == ItemUse.None || use == ItemUse.Combine || use == ItemUse.Return || use == ItemUse.Food || playerLevel >= itemLevel;

        /// <summary>Seconds between restoring ticks of potions that work over time.</summary>
        public const float RestoreInterval = 2f;

        /// <summary>How many ticks a potion restores over (1 when it works at once).</summary>
        public int RestoreTicks => restoreSeconds > 0f ? Mathf.Max(1, Mathf.RoundToInt(restoreSeconds / RestoreInterval)) : 1;

        /// <summary>What one use restores of a pool of <paramref name="max"/>: the fixed amount, or the share of max.</summary>
        public int RestoreTotal(int max) => restoreAmount > 0 ? restoreAmount : Mathf.Max(1, Mathf.RoundToInt(max * healFraction));

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Poor: return new Color(0.6f, 0.6f, 0.6f);
                case ItemRarity.Uncommon: return new Color(0.35f, 0.9f, 0.35f);
                case ItemRarity.Rare: return new Color(0.35f, 0.6f, 1f);
                // The source game's order: green, blue, gold, then purple at the top.
                case ItemRarity.Epic: return new Color(1f, 0.78f, 0.2f);
                case ItemRarity.Legendary: return new Color(0.75f, 0.4f, 1f);
                default: return new Color(0.92f, 0.92f, 0.92f);
            }
        }

        /// <summary>What a vendor pays for one: its sell value, or a quarter of the price (at least 1 for anything with a price).</summary>
        public int SellPrice => sellValue > 0 ? sellValue : price > 0 ? Mathf.Max(1, price / 4) : 0;

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

        public bool CanBeUsedByClass(int classIndex) =>
            requiredClass >= 0 ? requiredClass == classIndex : classMask == 0 || (classIndex >= 0 && (classMask & (1 << classIndex)) != 0);

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
            if (IsEquippable) text.Append(' ').Append(weaponType != WeaponType.None ? weaponType.ToString() : slot.ToString());
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
            if (isGem && combinesInto != null) text.AppendLine($"{combineCount} transmute into {combinesInto.displayName}");
            if (use == ItemUse.LearnSpell && teachesSpell != null)
            {
                text.AppendLine($"Use: learn {teachesSpell.displayName}");
                if (!string.IsNullOrEmpty(teachesSpell.description)) text.AppendLine(teachesSpell.description);
                if (itemLevel > 1) text.AppendLine($"Requires level {itemLevel}");
            }
            else if (use == ItemUse.Combine && combineResult != null)
            {
                var others = new StringBuilder();
                foreach (ItemDefinition other in combineWith)
                    if (other != null) others.Append(others.Length > 0 ? " and " : "").Append(other.displayName);
                text.AppendLine(others.Length > 0 ? $"Use: combine with {others} into {combineResult.displayName}" : $"Use: turn into {combineResult.displayName}");
            }
            else if (use == ItemUse.Return)
            {
                text.AppendLine("Use: returns you to St. Revere");
                if (castSeconds > 0f) text.AppendLine($"{castSeconds:0.#} sec cast, {Mathf.RoundToInt(useCooldown / 60f)} min cooldown");
            }
            else if (use == ItemUse.Food)
            {
                text.AppendLine($"Use: restores {restoreAmount} health and {restoreAmount} mana");
                text.AppendLine("Cannot be used in combat");
                if (useCooldown > 0f) text.AppendLine($"{useCooldown:0} sec cooldown");
            }
            else if (use != ItemUse.None && use != ItemUse.Combine)
            {
                string pool = use == ItemUse.Heal ? "health" : "mana";
                string amount = restoreAmount > 0 ? $"{restoreAmount} {pool}" : $"{Mathf.RoundToInt(healFraction * 100f)}% {pool}";
                text.AppendLine(restoreSeconds > 0f ? $"Use: restores {amount} over {restoreSeconds:0} sec" : $"Use: restores {amount}");
                if (itemLevel > 1) text.AppendLine($"Requires level {itemLevel}");
                if (combinesInto != null) text.AppendLine($"{combineCount} transmute into {combinesInto.displayName}");
            }
            if (!string.IsNullOrEmpty(description)) text.AppendLine(description);
            if (maxStack > 1) text.AppendLine($"Stacks up to {maxStack}");
            if (!string.IsNullOrEmpty(className)) text.AppendLine($"{className} only");
            if (IsEquippable) text.AppendLine($"Requires level {itemLevel}");
            if (price > 0) text.AppendLine($"Sells for {(sellPrice >= 0 ? sellPrice : SellPriceWith(affix))} gold");
            text.Append(IsEquippable ? "Click to equip" : use != ItemUse.None ? "Click to use" : isGem ? "Click, then click the item to set it into" : "");
            return text.ToString().TrimEnd();
        }
    }
}
