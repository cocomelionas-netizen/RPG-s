using RPG.Progression;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// A random bonus a dropped weapon or armor piece can roll, named as a prefix ("Sturdy Iron Helm") or a
    /// suffix ("Iron Sword of the Bear"). Raises the item's quality to its own and makes it worth more.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Items/Affix", fileName = "Affix")]
    public class AffixDefinition : ScriptableObject
    {
        public string displayName = "of Testing";
        public bool isPrefix;
        public ItemRarity quality = ItemRarity.Uncommon;
        [Tooltip("Monsters from this level up can drop it.")]
        [Min(1)] public int minLevel = 1;
        [Tooltip("Monsters up to this level can drop it.")]
        [Min(1)] public int maxLevel = 99;

        public AttributeSet attributes;
        [Min(0)] public int damageBonus;
        [Min(0)] public int healthBonus;
        [Min(0)] public int armor;
        [Min(0)] public int manaBonus;
        [Min(1f)] public float priceMultiplier = 1.5f;
    }
}
