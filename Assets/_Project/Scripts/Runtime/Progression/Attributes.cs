using System;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Progression
{
    public enum Attribute
    {
        /// <summary>Damage for warriors; a little health for everyone.</summary>
        Strength,
        /// <summary>Critical hit chance for everyone; damage for rangers.</summary>
        Agility,
        /// <summary>Mana for everyone; damage for mages.</summary>
        Intelligence,
        /// <summary>Health.</summary>
        Vitality,
        /// <summary>Points put straight into health (like Source lets you), +1 each.</summary>
        Health,
        /// <summary>Points put straight into mana, +1 each.</summary>
        Mana,
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

        public const int Count = 6;

        public AttributeSet(int strength, int agility, int intelligence, int vitality, int health = 0, int mana = 0)
        {
            this.strength = strength;
            this.agility = agility;
            this.intelligence = intelligence;
            this.vitality = vitality;
            this.health = health;
            this.mana = mana;
        }

        /// <summary>Sum of all values (for example every point spent).</summary>
        public int Total => strength + agility + intelligence + vitality + health + mana;

        public int this[Attribute attribute]
        {
            get
            {
                switch (attribute)
                {
                    case Attribute.Strength: return strength;
                    case Attribute.Agility: return agility;
                    case Attribute.Intelligence: return intelligence;
                    case Attribute.Health: return health;
                    case Attribute.Mana: return mana;
                    default: return vitality;
                }
            }
            set
            {
                switch (attribute)
                {
                    case Attribute.Strength: strength = value; break;
                    case Attribute.Agility: agility = value; break;
                    case Attribute.Intelligence: intelligence = value; break;
                    case Attribute.Health: health = value; break;
                    case Attribute.Mana: mana = value; break;
                    default: vitality = value; break;
                }
            }
        }

        public bool IsZero => strength == 0 && agility == 0 && intelligence == 0 && vitality == 0 && health == 0 && mana == 0;

        public static AttributeSet operator +(AttributeSet a, AttributeSet b)
        {
            return new AttributeSet(a.strength + b.strength, a.agility + b.agility, a.intelligence + b.intelligence, a.vitality + b.vitality,
                a.health + b.health, a.mana + b.mana);
        }

        public bool Equals(AttributeSet other) =>
            strength == other.strength && agility == other.agility && intelligence == other.intelligence && vitality == other.vitality
            && health == other.health && mana == other.mana;

        public override bool Equals(object obj) => obj is AttributeSet other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(strength, agility, intelligence, vitality, health, mana);

        /// <summary>For tooltips, for example "+3 Strength, +2 Vitality" (empty when all are zero).</summary>
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
                parts.Add($"{(shown > 0 ? "+" : "")}{shown} {attribute}");
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

        /// <summary>
        /// Experience the next point costs when <paramref name="invested"/> points are already in the attribute
        /// (Source's prices): health and mana x^(x/1500), at least 1, so they stay cheap for a long time;
        /// the other attributes x^1.5 + 5.
        /// </summary>
        public static int PointCost(Attribute attribute, int invested)
        {
            int x = Mathf.Max(0, invested);
            if (attribute == Attribute.Health || attribute == Attribute.Mana)
            {
                double growing = Math.Pow(x, x / 1500.0);
                return growing >= int.MaxValue ? int.MaxValue : Mathf.Max(1, (int)growing);
            }
            double steady = Math.Pow(x, 1.5);
            return steady >= int.MaxValue - 5 ? int.MaxValue : (int)steady + 5;
        }

        /// <summary>Added to minimum and maximum damage: a quarter of the class's primary attribute.</summary>
        public static int DamageBonus(int primaryAttribute) => Mathf.Max(0, primaryAttribute) / 4;

        public static int HealthBonus(AttributeSet total) =>
            total.vitality * HealthPerVitality + total.strength * HealthPerStrength + total.health * HealthPerPoint;

        public static int ManaBonus(AttributeSet total) => total.intelligence * ManaPerIntelligence + total.mana * ManaPerPoint;

        public static float CritBonus(AttributeSet total) => total.agility * CritPerAgility;
    }
}
