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

        [Header("Health")]
        [Min(1)] public int baseHealth = 100;
        [Min(0)] public int healthPerLevel = 10;

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

        public int MaxHealthAt(int level) => baseHealth + healthPerLevel * Mathf.Max(0, level - 1);

        public int MinDamageAt(int level) => minDamage + Mathf.RoundToInt(damagePerLevel * Mathf.Max(0, level - 1));

        public int MaxDamageAt(int level) => maxDamage + Mathf.RoundToInt(damagePerLevel * Mathf.Max(0, level - 1));
    }
}
