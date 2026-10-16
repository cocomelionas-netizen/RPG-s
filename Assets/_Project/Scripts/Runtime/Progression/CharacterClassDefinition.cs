using System;
using RPG.Characters;
using UnityEngine;

namespace RPG.Progression
{
    /// <summary>A playable class: its look, stats at level 1, growth per level and abilities.</summary>
    [CreateAssetMenu(menuName = "RPG/Progression/Class", fileName = "Class")]
    public class CharacterClassDefinition : ScriptableObject
    {
        public string displayName = "Warrior";
        public CharacterAnimationSet animations;

        [Header("Attributes")]
        [Tooltip("Attributes at level 1, before spent points and items.")]
        public AttributeSet baseAttributes = new AttributeSet(10, 10, 10, 10);
        [Tooltip("Adds a quarter of itself to damage.")]
        public Attribute primaryAttribute = Attribute.Strength;
        [Tooltip("The weapon skills the class can train (the source game's paladin: maces, axes, swords, shields); empty: all.")]
        public Attribute[] weaponSkills = Array.Empty<Attribute>();

        /// <summary>Whether the class can buy points in a stat: every stat but the weapon skills of other classes' weapons.</summary>
        public bool CanTrain(Attribute attribute) =>
            attribute < Attribute.Staves || attribute > Attribute.Shields || weaponSkills == null || weaponSkills.Length == 0
            || Array.IndexOf(weaponSkills, attribute) >= 0;

        [Header("Health and mana (attributes add more)")]
        [Tooltip("Health at level 1; grows like the source game's (LevelCurve.HealthGrowth): times the level, half again from 21.")]
        [Min(1)] public int baseHealth = 100;
        [Tooltip("Mana at level 1; grows like health.")]
        [Min(0)] public int baseMana = 20;

        [Header("Auto-attack")]
        [Min(0)] public int minDamage = 8;
        [Min(0)] public int maxDamage = 12;
        [Tooltip("Added to min and max damage per level above 1.")]
        [Min(0f)] public float damagePerLevel = 1f;
        [Min(0.1f)] public float attackInterval = 1.5f;
        [Min(0.1f)] public float attackRange = 0.9f;
        [Range(0f, 1f)] public float critChance = 0.1f;
        [Tooltip("Ranged classes shoot a projectile of this color; leave alpha at 0 for melee.")]
        public Color projectileColor = Color.clear;

        [Header("Abilities (keys 1, 2, 3...)")]
        public AbilityDefinition[] abilities = Array.Empty<AbilityDefinition>();

        public int MaxHealthAt(int level) => Mathf.RoundToInt(baseHealth * LevelCurve.HealthGrowth(level));

        public int MaxManaAt(int level) => Mathf.RoundToInt(baseMana * LevelCurve.HealthGrowth(level));

        public int MinDamageAt(int level) => minDamage + Mathf.RoundToInt(damagePerLevel * Mathf.Max(0, level - 1));

        public int MaxDamageAt(int level) => maxDamage + Mathf.RoundToInt(damagePerLevel * Mathf.Max(0, level - 1));
    }
}
