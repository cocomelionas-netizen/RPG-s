using System.Text;
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

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
    }

    /// <summary>What happens when the item is used from the bag.</summary>
    public enum ItemUse
    {
        None,
        /// <summary>Restores a share of max health and is used up.</summary>
        Heal,
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

        [Header("Trade and restrictions")]
        [Tooltip("Price at vendors in gold; vendors buy it back for a quarter of this.")]
        [Min(0)] public int price = 10;
        [Tooltip("Index of the class (in the class catalog) that may equip it; -1 for every class.")]
        public int requiredClass = -1;

        [Header("Use")]
        public ItemUse use = ItemUse.None;
        [Range(0f, 1f)] public float healFraction = 0.35f;
        [Min(1)] public int maxStack = 1;

        [Header("Look")]
        [Tooltip("Color of the item's square in the bag until it has an icon.")]
        public Color iconColor = new Color(0.5f, 0.5f, 0.5f);

        public bool IsEquippable => slot != ItemSlot.None;

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color(0.35f, 0.9f, 0.35f);
                case ItemRarity.Rare: return new Color(0.35f, 0.6f, 1f);
                case ItemRarity.Epic: return new Color(0.75f, 0.4f, 1f);
                default: return new Color(0.92f, 0.92f, 0.92f);
            }
        }

        /// <summary>What a vendor pays for one (a quarter of the price, at least 1 for anything with a price).</summary>
        public int SellPrice => price > 0 ? Mathf.Max(1, price / 4) : 0;

        public bool CanBeUsedByClass(int classIndex) => requiredClass < 0 || requiredClass == classIndex;

        /// <summary>Tooltip text: name, rarity and slot, stats and use.</summary>
        /// <param name="className">Name of the required class to show (null when anyone may use it).</param>
        public string Describe(string className = null)
        {
            var text = new StringBuilder();
            text.AppendLine(displayName);
            text.Append(rarity);
            if (IsEquippable) text.Append(' ').Append(slot);
            text.AppendLine();
            if (damageBonus > 0) text.AppendLine($"+{damageBonus} damage");
            if (healthBonus > 0) text.AppendLine($"+{healthBonus} health");
            if (armor > 0) text.AppendLine($"+{armor} armor");
            if (use == ItemUse.Heal) text.AppendLine($"Use: restores {Mathf.RoundToInt(healFraction * 100f)}% health");
            if (maxStack > 1) text.AppendLine($"Stacks up to {maxStack}");
            if (!string.IsNullOrEmpty(className)) text.AppendLine($"{className} only");
            if (price > 0) text.AppendLine($"Sells for {SellPrice} gold");
            text.Append(IsEquippable ? "Click to equip" : use != ItemUse.None ? "Click to use" : "");
            return text.ToString().TrimEnd();
        }
    }
}
