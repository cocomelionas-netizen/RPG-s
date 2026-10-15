using System;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Progression
{
    /// <summary>
    /// Every stat a character has, Source's list (its ids, names and prices come from Source.exe). Players buy
    /// points in all of them with experience; gear, affixes and gems add more. Only ever append: values are saved.
    /// </summary>
    public enum Attribute
    {
        /// <summary>Damage for warriors; a little health for everyone.</summary>
        Strength,
        /// <summary>Critical hit chance for everyone; damage for rangers.</summary>
        Agility,
        /// <summary>Mana for everyone; damage for mages.</summary>
        Intelligence,
        /// <summary>Health (Source's Courage).</summary>
        Vitality,
        /// <summary>Points put straight into health (like Source lets you), +1 each.</summary>
        Health,
        /// <summary>Points put straight into mana, +1 each.</summary>
        Mana,
        /// <summary>Stronger abilities and heals.</summary>
        Willpower,
        /// <summary>Health regained every second, in and out of combat.</summary>
        Regeneration,
        /// <summary>Mana regained every second.</summary>
        Meditate,
        /// <summary>Critical chance of melee attacks.</summary>
        MeleeCritical,
        /// <summary>Critical chance of ranged attacks.</summary>
        RangedCritical,
        /// <summary>Critical chance of abilities.</summary>
        SpellCritical,
        /// <summary>Chance to avoid a hit completely.</summary>
        Dodge,
        /// <summary>Chance to block half of a hit.</summary>
        Block,
        /// <summary>Less frost damage taken.</summary>
        ResistFrost,
        /// <summary>Less fire damage taken.</summary>
        ResistFire,
        /// <summary>Less shadow damage taken.</summary>
        ResistShadow,
        /// <summary>Less holy damage taken.</summary>
        ResistHoly,
        /// <summary>Better prices at vendors.</summary>
        Bartering,
        /// <summary>Opens locked chests.</summary>
        Lockpicking,
        /// <summary>Damage with staves.</summary>
        Staves,
        /// <summary>Damage with maces.</summary>
        Maces,
        /// <summary>Damage with axes.</summary>
        Axes,
        /// <summary>Damage with swords.</summary>
        Swords,
        /// <summary>Damage with bows.</summary>
        Ranged,
        /// <summary>Damage with daggers.</summary>
        Daggers,
        /// <summary>Damage with wands.</summary>
        Wands,
        /// <summary>Block chance with a shield.</summary>
        Shields,
    }

    /// <summary>A value for each attribute (from a class, spent points or items).</summary>
    [Serializable]
    public struct AttributeSet : IEquatable<AttributeSet>, INetworkSerializeByMemcpy
    {
        public int strength;
        public int agility;
        public int intelligence;
        public int vitality;
        public int health;
        public int mana;
        public int willpower;
        public int regeneration;
        public int meditate;
        public int meleeCritical;
        public int rangedCritical;
        public int spellCritical;
        public int dodge;
        public int block;
        public int resistFrost;
        public int resistFire;
        public int resistShadow;
        public int resistHoly;
        public int bartering;
        public int lockpicking;
        public int staves;
        public int maces;
        public int axes;
        public int swords;
        public int ranged;
        public int daggers;
        public int wands;
        public int shields;

        public const int Count = 28;

        public AttributeSet(int strength, int agility, int intelligence, int vitality, int health = 0, int mana = 0) : this()
        {
            this.strength = strength;
            this.agility = agility;
            this.intelligence = intelligence;
            this.vitality = vitality;
            this.health = health;
            this.mana = mana;
        }

        /// <summary>Sum of all values (for example every point spent).</summary>
        public int Total
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Count; i++) total += this[(Attribute)i];
                return total;
            }
        }

        public int this[Attribute attribute]
        {
            get
            {
                switch (attribute)
                {
                    case Attribute.Strength: return strength;
                    case Attribute.Agility: return agility;
                    case Attribute.Intelligence: return intelligence;
                    case Attribute.Vitality: return vitality;
                    case Attribute.Health: return health;
                    case Attribute.Mana: return mana;
                    case Attribute.Willpower: return willpower;
                    case Attribute.Regeneration: return regeneration;
                    case Attribute.Meditate: return meditate;
                    case Attribute.MeleeCritical: return meleeCritical;
                    case Attribute.RangedCritical: return rangedCritical;
                    case Attribute.SpellCritical: return spellCritical;
                    case Attribute.Dodge: return dodge;
                    case Attribute.Block: return block;
                    case Attribute.ResistFrost: return resistFrost;
                    case Attribute.ResistFire: return resistFire;
                    case Attribute.ResistShadow: return resistShadow;
                    case Attribute.ResistHoly: return resistHoly;
                    case Attribute.Bartering: return bartering;
                    case Attribute.Lockpicking: return lockpicking;
                    case Attribute.Staves: return staves;
                    case Attribute.Maces: return maces;
                    case Attribute.Axes: return axes;
                    case Attribute.Swords: return swords;
                    case Attribute.Ranged: return ranged;
                    case Attribute.Daggers: return daggers;
                    case Attribute.Wands: return wands;
                    case Attribute.Shields: return shields;
                    default: return 0;
                }
            }
            set
            {
                switch (attribute)
                {
                    case Attribute.Strength: strength = value; break;
                    case Attribute.Agility: agility = value; break;
                    case Attribute.Intelligence: intelligence = value; break;
                    case Attribute.Vitality: vitality = value; break;
                    case Attribute.Health: health = value; break;
                    case Attribute.Mana: mana = value; break;
                    case Attribute.Willpower: willpower = value; break;
                    case Attribute.Regeneration: regeneration = value; break;
                    case Attribute.Meditate: meditate = value; break;
                    case Attribute.MeleeCritical: meleeCritical = value; break;
                    case Attribute.RangedCritical: rangedCritical = value; break;
                    case Attribute.SpellCritical: spellCritical = value; break;
                    case Attribute.Dodge: dodge = value; break;
                    case Attribute.Block: block = value; break;
                    case Attribute.ResistFrost: resistFrost = value; break;
                    case Attribute.ResistFire: resistFire = value; break;
                    case Attribute.ResistShadow: resistShadow = value; break;
                    case Attribute.ResistHoly: resistHoly = value; break;
                    case Attribute.Bartering: bartering = value; break;
                    case Attribute.Lockpicking: lockpicking = value; break;
                    case Attribute.Staves: staves = value; break;
                    case Attribute.Maces: maces = value; break;
                    case Attribute.Axes: axes = value; break;
                    case Attribute.Swords: swords = value; break;
                    case Attribute.Ranged: ranged = value; break;
                    case Attribute.Daggers: daggers = value; break;
                    case Attribute.Wands: wands = value; break;
                    case Attribute.Shields: shields = value; break;
                }
            }
        }

        public bool IsZero => Equals(default);

        public static AttributeSet operator +(AttributeSet a, AttributeSet b)
        {
            for (int i = 0; i < Count; i++) a[(Attribute)i] += b[(Attribute)i];
            return a;
        }

        public bool Equals(AttributeSet other)
        {
            for (int i = 0; i < Count; i++)
                if (this[(Attribute)i] != other[(Attribute)i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is AttributeSet other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 17;
            for (int i = 0; i < Count; i++) hash = hash * 31 + this[(Attribute)i];
            return hash;
        }

        /// <summary>For tooltips, for example "+3 Strength, +2 Courage" (empty when all are zero).</summary>
        public string Describe(string separator = ", ")
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Count; i++)
            {
                var attribute = (Attribute)i;
                if (this[attribute] == 0) continue;
                // Health and mana points show what they add, the others their own value.
                int shown = attribute == Attribute.Health ? AttributeRules.HealthBonus(new AttributeSet { health = this[attribute] })
                    : attribute == Attribute.Mana ? AttributeRules.ManaBonus(new AttributeSet { mana = this[attribute] })
                    : this[attribute];
                parts.Add($"{(shown > 0 ? "+" : "")}{shown} {AttributeRules.NameOf(attribute)}");
            }
            return string.Join(separator, parts);
        }
    }

    /// <summary>What attributes are worth. The same rules for every class; the class only picks its primary attribute.</summary>
    public static class AttributeRules
    {
        public const int HealthPerVitality = 4;
        public const int HealthPerStrength = 1;
        public const int ManaPerIntelligence = 4;
        public const float CritPerAgility = 0.004f;
        /// <summary>What one point put straight into health or mana adds.</summary>
        public const int HealthPerPoint = 1;
        public const int ManaPerPoint = 1;
        /// <summary>Critical chance per point of a critical rating (melee, ranged, spell).</summary>
        public const float CritPerRating = 0.002f;
        /// <summary>Dodge and block chance per point of rating, and their caps.</summary>
        public const float AvoidPerRating = 0.002f;
        public const float MaxDodge = 0.4f;
        public const float MaxBlock = 0.5f;
        /// <summary>Block chance per point of the Shields skill.</summary>
        public const float BlockPerShieldSkill = 0.001f;
        /// <summary>Ability damage and heals per point of Willpower.</summary>
        public const float AbilityPowerPerWillpower = 0.005f;
        /// <summary>Damage with a weapon per point of its skill.</summary>
        public const float DamagePerWeaponSkill = 0.005f;
        /// <summary>Health or mana regained per second per point of Regeneration or Meditate.</summary>
        public const float RegenPerPoint = 0.2f;
        /// <summary>Price change per point of Bartering, and its cap.</summary>
        public const float PricePerBartering = 0.005f;
        public const float MaxBartering = 0.25f;

        private static readonly string[] Names =
        {
            "Strength",
            "Agility",
            "Intelligence",
            "Courage",
            "Health",
            "Mana",
            "Willpower",
            "Regeneration",
            "Meditate",
            "Melee Critical",
            "Ranged Critical",
            "Spell Critical",
            "Dodge Rating",
            "Block Rating",
            "Resist Frost",
            "Resist Fire",
            "Resist Shadow",
            "Resist Holy",
            "Bartering",
            "Lockpicking",
            "Staves",
            "Maces",
            "Axes",
            "Swords",
            "Ranged",
            "Daggers",
            "Wands",
            "Shields",
        };

        /// <summary>The name players see (Source's: Vitality shows as Courage).</summary>
        public static string NameOf(Attribute attribute)
        {
            int index = (int)attribute;
            return index >= 0 && index < Names.Length ? Names[index] : attribute.ToString();
        }

        /// <summary>
        /// Experience the next point costs when <paramref name="invested"/> points are already in the attribute,
        /// Source's prices (its stat cost function): health and mana x^(x/1500), at least 1; Strength, Agility,
        /// Willpower, Intelligence and Courage x^1.5 + 5; Regeneration and Meditate x^1.4 + 4; melee and spell
        /// critical x/2 + 1; ranged critical and resists x/3 + 1; dodge and block x + 1; Bartering and Lockpicking
        /// 2x + 1; weapon skills three times Strength's.
        /// </summary>
        public static int PointCost(Attribute attribute, int invested)
        {
            int x = Mathf.Max(0, invested);
            switch (attribute)
            {
                case Attribute.Health:
                case Attribute.Mana:
                    double growing = Math.Pow(x, x / 1500.0);
                    return growing >= int.MaxValue ? int.MaxValue : Mathf.Max(1, (int)growing);
                case Attribute.Regeneration:
                case Attribute.Meditate:
                    return Clamp(Math.Pow(x, 1.4) + 4);
                case Attribute.MeleeCritical:
                case Attribute.SpellCritical:
                    return x / 2 + 1;
                case Attribute.RangedCritical:
                case Attribute.ResistFrost:
                case Attribute.ResistFire:
                case Attribute.ResistShadow:
                case Attribute.ResistHoly:
                    return x / 3 + 1;
                case Attribute.Dodge:
                case Attribute.Block:
                    return x + 1;
                case Attribute.Bartering:
                case Attribute.Lockpicking:
                    return Clamp(2.0 * x + 1);
                case Attribute.Staves:
                case Attribute.Maces:
                case Attribute.Axes:
                case Attribute.Swords:
                case Attribute.Ranged:
                case Attribute.Daggers:
                case Attribute.Wands:
                case Attribute.Shields:
                    return Clamp(3.0 * PointCost(Attribute.Strength, x));
                default:
                    return Clamp(Math.Pow(x, 1.5) + 5);
            }
        }

        private static int Clamp(double value) => value >= int.MaxValue ? int.MaxValue : (int)value;

        /// <summary>Added to minimum and maximum damage: a quarter of the class's primary attribute.</summary>
        public static int DamageBonus(int primaryAttribute) => Mathf.Max(0, primaryAttribute) / 4;

        public static int HealthBonus(AttributeSet total) =>
            total.vitality * HealthPerVitality + total.strength * HealthPerStrength + total.health * HealthPerPoint;

        public static int ManaBonus(AttributeSet total) => total.intelligence * ManaPerIntelligence + total.mana * ManaPerPoint;

        public static float CritBonus(AttributeSet total) => total.agility * CritPerAgility;

        /// <summary>Extra critical chance of melee or ranged attacks from their rating.</summary>
        public static float AttackCritBonus(AttributeSet total, bool ranged) =>
            (ranged ? total.rangedCritical : total.meleeCritical) * CritPerRating;

        public static float SpellCritBonus(AttributeSet total) => total.spellCritical * CritPerRating;

        public static float DodgeChance(AttributeSet total) => Mathf.Clamp(total.dodge * AvoidPerRating, 0f, MaxDodge);

        public static float BlockChance(AttributeSet total) =>
            Mathf.Clamp(total.block * AvoidPerRating + total.shields * BlockPerShieldSkill, 0f, MaxBlock);

        /// <summary>Multiplier of ability damage and heals.</summary>
        public static float AbilityPower(AttributeSet total) => 1f + Mathf.Max(0, total.willpower) * AbilityPowerPerWillpower;

        /// <summary>Multiplier of damage with a weapon whose skill is <paramref name="skill"/> (none: 1).</summary>
        public static float WeaponSkillBonus(AttributeSet total, Attribute? skill) =>
            skill.HasValue ? 1f + Mathf.Max(0, total[skill.Value]) * DamagePerWeaponSkill : 1f;

        public static float HealthRegenPerSecond(AttributeSet total) => Mathf.Max(0, total.regeneration) * RegenPerPoint;

        public static float ManaRegenPerSecond(AttributeSet total) => Mathf.Max(0, total.meditate) * RegenPerPoint;

        /// <summary>Share off vendor prices (and added to what they pay).</summary>
        public static float BarteringDiscount(AttributeSet total) => Mathf.Clamp(total.bartering * PricePerBartering, 0f, MaxBartering);

        /// <summary>Share of damage of a magic school a resist rating stops: r / (r + 100).</summary>
        public static float ResistReduction(int rating) => rating <= 0 ? 0f : rating / (rating + 100f);
    }
}
