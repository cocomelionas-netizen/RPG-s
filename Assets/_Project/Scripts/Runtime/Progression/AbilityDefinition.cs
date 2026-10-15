using System;
using RPG.Combat;
using UnityEngine;

namespace RPG.Progression
{
    public enum AbilityKind
    {
        /// <summary>Damages the selected hostile target.</summary>
        TargetDamage,
        /// <summary>Damages hostile units within a radius around the selected target.</summary>
        TargetArea,
        /// <summary>Damages every hostile unit within a radius around the caster.</summary>
        SelfArea,
        /// <summary>Heals the caster by a share of its max health.</summary>
        SelfHeal,
        /// <summary>Heals or buffs the selected friendly target, or the caster when none is selected.</summary>
        FriendlyTarget,
        /// <summary>Works on the caster only (buffs).</summary>
        Self,
        /// <summary>Buffs every friendly unit within a radius around the caster (auras).</summary>
        FriendlyArea,
        /// <summary>Rushes to the selected hostile target, then hits it.</summary>
        Charge,
        /// <summary>Moves the caster to the point under the pointer.</summary>
        Teleport,
        /// <summary>Leaps the caster away from its target (Tactical Retreat).</summary>
        Retreat,
        /// <summary>Takes the caster to St. Revere (Illusion Gate).</summary>
        Recall,
        /// <summary>Brings the nearest fallen friendly player in range back (Redemption).</summary>
        Revive,
    }

    /// <summary>A buff or debuff a spell puts on its target (or on the caster), Source's "apply aura".</summary>
    [Serializable]
    public class SpellEffect
    {
        public StatusKind kind;
        [Tooltip("Percent for slows and damage changes, points for others, the total for damage or healing over time.")]
        public float value;
        [Tooltip("Added for every rank above the first.")]
        public float valuePerRank;
        [Tooltip("Adds an attribute of the caster times Stat Factor (Boon of Clairvoyance: Willpower x 5).")]
        public bool scalesWithStat;
        public Attribute stat;
        public float statFactor;
        [Min(0f)] public float duration = 5f;
        [Tooltip("Seconds between ticks of damage or healing over time.")]
        [Min(0f)] public float interval;
        [Min(1)] public int maxStacks = 1;
        public DamageSchool school;
        [Tooltip("Damage changes apply to every school (otherwise only to School).")]
        public bool anySchool = true;
        [Tooltip("Goes on the caster instead of the target.")]
        public bool onCaster;
        [Tooltip("Damage changes apply to every magic school, not physical (Salvation Aura).")]
        public bool magicOnly;

        public float ValueAt(int rank, AttributeSet casterAttributes) =>
            value + valuePerRank * Mathf.Max(0, rank - 1) + (scalesWithStat ? casterAttributes[stat] * statFactor : 0f);
    }

    /// <summary>
    /// One spell (ability) of a class, a Source spell: its mana cost (base + per level), cooldown, cast time and
    /// range; damage as a share of the weapon's or from Source's spell formula (rank + attributes x coefficient /
    /// (105 - rank x divisor)), healing, and the effects it puts on. Spells are learned from Tomes from their level
    /// and ranked up with experience. The server resolves it with <see cref="AbilityCaster"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Progression/Ability", fileName = "Ability")]
    public class AbilityDefinition : ScriptableObject
    {
        /// <summary>Source's spell numbers are for its health; ours is half of it, so its amounts are halved too.</summary>
        public const float PowerScale = 0.5f;

        public string displayName = "Ability";
        [TextArea] public string description = "";
        public AbilityKind kind = AbilityKind.TargetDamage;
        [Tooltip("The level from which it can be learned (its Tome's level).")]
        [Range(1, 25)] public int learnLevel = 1;
        [Min(1)] public int maxRank = 10;
        public DamageSchool school = DamageSchool.Physical;

        [Header("Cost and timing")]
        [Min(0f)] public float cooldown = 6f;
        [Tooltip("Mana at level 0; Mana Per Level is added for every character level (Source: 2 + level).")]
        [Min(0)] public int manaCost = 10;
        [Min(0f)] public float manaPerLevel;
        [Tooltip("Seconds of casting before it lands; moving breaks it off.")]
        [Min(0f)] public float castTime;

        [Header("Reach")]
        [Tooltip("Max distance to the target (target abilities).")]
        [Min(0f)] public float range = 1f;
        [Tooltip("Area radius (area abilities).")]
        [Min(0f)] public float radius = 1f;
        [Tooltip("Most units an area hits (0: all).")]
        [Min(0)] public int maxTargets;

        [Header("Weapon damage")]
        [Tooltip("Old abilities: auto-attack damage times this (used when Weapon Percent and Spell Amount are 0).")]
        [Min(0f)] public float damageMultiplier = 2f;
        [Tooltip("Percent of the weapon's (auto-attack) damage (Mighty Blow: 134%).")]
        [Min(0f)] public float weaponPercent;
        [Min(0f)] public float weaponPercentPerRank;
        [Tooltip("Hits in a row (Multi-Shot: 3).")]
        [Min(1)] public int hits = 1;
        [Tooltip("Cannot be dodged or blocked and ignores armor (Aimed Shot).")]
        public bool ignoreDefenses;

        [Header("Spell power (damage, or healing when Is Heal)")]
        public bool isHeal;
        [Min(0f)] public float spellAmount;
        [Min(0f)] public float spellSpread;
        [Tooltip("Source's coefficient: (Stat + Stat 2 x Factor) x this / (105 - rank x Divisor).")]
        [Min(0f)] public float spellCoefficient;
        public Attribute spellStat = Attribute.Intelligence;
        public Attribute spellStat2 = Attribute.Vitality;
        public float spellStat2Factor = 1f;
        [Min(0f)] public float rankDivisor = 5f;
        [Tooltip("Heals: share of max health restored (Touch of Salvation: all of it).")]
        [Range(0f, 1f)] public float healFraction;

        [Header("Effects")]
        public SpellEffect[] effects = Array.Empty<SpellEffect>();
        [Tooltip("Removes one harmful effect from the target (Cleanse).")]
        public bool removesHarmful;
        [Tooltip("Frees the caster from stuns and roots (Teleport).")]
        public bool breaksControl;
        [Tooltip("Only on targets that have this effect (Ice Shard: slowed by Chains of Ice; Kidney Shot: incapacitated).")]
        public bool requiresTargetEffect;
        public StatusKind requiredEffect;
        [Tooltip("And with this name (Explosive Arrow: Flame Arrow's mark); empty: any of the kind.")]
        public string requiredEffectName = "";
        [Tooltip("Only on monsters not fighting yet (Sabotage).")]
        public bool requiresTargetOutOfCombat;
        [Tooltip("Percent of max mana restored at once (Wisdom of Lazarus), plus this per rank.")]
        [Min(0f)] public float restoreManaPercent;
        [Min(0f)] public float restoreManaPerRank;
        [Tooltip("How far a Retreat leaps; how much health and mana (shares) a Revive brings back.")]
        [Min(0f)] public float leapDistance = 2.5f;
        [Range(0f, 1f)] public float reviveHealth = 0.4f;
        [Range(0f, 1f)] public float reviveMana = 0.2f;

        [Header("Look")]
        [Tooltip("Color of the projectile, ring or glow shown when it is used.")]
        public Color effectColor = Color.white;

        public bool NeedsTarget => kind == AbilityKind.TargetDamage || kind == AbilityKind.TargetArea || kind == AbilityKind.Charge;

        public bool IsFriendly => kind == AbilityKind.FriendlyTarget || kind == AbilityKind.Self || kind == AbilityKind.SelfHeal
                                  || kind == AbilityKind.FriendlyArea || kind == AbilityKind.Recall || kind == AbilityKind.Revive;

        public bool DealsWeaponDamage => weaponPercent > 0f;

        public bool UsesSpellPower => spellAmount > 0f || spellSpread > 0f || spellCoefficient > 0f;

        /// <summary>Mana it costs a caster of <paramref name="level"/>.</summary>
        public int ManaCostAt(int level) => manaCost + Mathf.RoundToInt(manaPerLevel * Mathf.Max(1, level));

        public float WeaponPercentAt(int rank) => weaponPercent + weaponPercentPerRank * Mathf.Max(0, rank - 1);

        /// <summary>
        /// Source's spell formula: rank + (stat + stat 2 x factor) x coefficient / (105 - rank x divisor), plus the
        /// base amount and a roll up to the spread; halved for our health (<see cref="PowerScale"/>).
        /// </summary>
        public float SpellPowerAt(int rank, AttributeSet attributes, float roll01)
        {
            rank = Mathf.Max(1, rank);
            float stats = attributes[spellStat] + attributes[spellStat2] * spellStat2Factor;
            float divisor = Mathf.Max(5f, 105f - rank * rankDivisor);
            float scaled = spellCoefficient > 0f ? rank + stats * spellCoefficient / divisor : 0f;
            return (scaled + spellAmount + spellSpread * Mathf.Clamp01(roll01)) * PowerScale;
        }

        /// <summary>Experience the next rank costs (from <paramref name="rank"/> to the next): 10 x its level + 10 x rank^1.5.</summary>
        public int RankCost(int rank) => 10 * learnLevel + Mathf.RoundToInt(10f * Mathf.Pow(Mathf.Max(1, rank), 1.5f));
    }
}
