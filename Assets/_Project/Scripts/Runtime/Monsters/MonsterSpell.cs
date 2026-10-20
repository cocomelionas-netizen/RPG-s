using System;
using RPG.Combat;
using UnityEngine;

namespace RPG.Monsters
{
    /// <summary>What a monster spell does (the source game's spell effects as this project has them).</summary>
    public enum MonsterSpellKind
    {
        /// <summary>A bolt of its school at the target.</summary>
        Bolt,
        /// <summary>Hits every enemy around the caster (or around the target when it has a range).</summary>
        Area,
        /// <summary>A melee strike of Weapon Percent of the monster's hit.</summary>
        Strike,
        /// <summary>A shot with the ranged weapon (Weapon Percent of the monster's hit).</summary>
        Shoot,
        /// <summary>Heals the most hurt friendly monster in range (or itself).</summary>
        Heal,
        /// <summary>Rushes at the target.</summary>
        Charge,
        /// <summary>Only its effects, on the caster (Enrage, Determination).</summary>
        Self,
        /// <summary>Only its effects, on the target (Rend, Blight, Silence).</summary>
        Hex,
    }

    /// <summary>A buff or debuff a monster spell puts on its target (or on the caster).</summary>
    [Serializable]
    public class MonsterSpellEffect
    {
        public StatusKind kind;
        [Tooltip("Percent for changes, total for damage or mana burn; with Scaled, a multiple of the monster's hit.")]
        public float value;
        public bool scaled;
        [Min(0f)] public float seconds = 5f;
        [Tooltip("Ticks of damage over time or mana burn (0: once a second).")]
        [Min(0f)] public float tick;
        [Min(1)] public int stacks = 1;
        public bool onCaster;
    }

    /// <summary>
    /// One spell of a monster (the source game's npc_template spells): tried every Interval seconds with Chance % while
    /// fighting, then resting Cooldown seconds. A primary spell is cast instead of the weapon swing (casters, archers).
    /// </summary>
    [Serializable]
    public class MonsterSpell
    {
        public string name = "Spell";
        public MonsterSpellKind kind;
        public DamageSchool school;
        [Min(0f)] public float castSeconds;
        [Tooltip("World units (0: melee or around itself).")]
        [Min(0f)] public float range;
        [Min(0f)] public float radius;
        [Tooltip("Damage as a multiple of the monster's own hit (0: none).")]
        [Min(0f)] public float damageMultiplier;
        [Range(0f, 100f)] public float chance = 100f;
        [Min(0f)] public float interval = 1.5f;
        [Min(0f)] public float cooldown;
        public bool primary;
        public Color color = Color.white;
        public MonsterSpellEffect[] effects = Array.Empty<MonsterSpellEffect>();

        /// <summary>Whether it reaches over a distance (bolts, shots, hexes, ranged heals and areas).</summary>
        public bool IsRanged => range > 0f && kind != MonsterSpellKind.Strike && kind != MonsterSpellKind.Self;
    }
}
