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
    }

    /// <summary>A value for each of the four attributes (from a class, spent points or items).</summary>
    [Serializable]
    public struct AttributeSet : IEquatable<AttributeSet>, INetworkSerializeByMemcpy
    {
        public int strength;
        public int agility;
        public int intelligence;
        public int vitality;

        public const int Count = 4;

        public AttributeSet(int strength, int agility, int intelligence, int vitality)
        {
            this.strength = strength;
            this.agility = agility;
            this.intelligence = intelligence;
            this.vitality = vitality;
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
                    default: vitality = value; break;
                }
            }
        }

        public bool IsZero => strength == 0 && agility == 0 && intelligence == 0 && vitality == 0;

        public static AttributeSet operator +(AttributeSet a, AttributeSet b)
        {
            return new AttributeSet(a.strength + b.strength, a.agility + b.agility, a.intelligence + b.intelligence, a.vitality + b.vitality);
        }

        public bool Equals(AttributeSet other) =>
            strength == other.strength && agility == other.agility && intelligence == other.intelligence && vitality == other.vitality;

        public override bool Equals(object obj) => obj is AttributeSet other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(strength, agility, intelligence, vitality);

        /// <summary>For tooltips, for example "+3 Strength, +2 Vitality" (empty when all are zero).</summary>
        public string Describe(string separator = ", ")
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Count; i++)
            {
                var attribute = (Attribute)i;
                if (this[attribute] != 0) parts.Add($"{(this[attribute] > 0 ? "+" : "")}{this[attribute]} {attribute}");
            }
            return string.Join(separator, parts);
        }
    }

    /// <summary>What attributes are worth. The same rules for every class; the class only picks its primary attribute.</summary>
    public static class AttributeRules
    {
        public const int PointsPerLevel = 5;
        public const int HealthPerVitality = 4;
        public const int HealthPerStrength = 1;
        public const int ManaPerIntelligence = 4;
        public const float CritPerAgility = 0.004f;

        /// <summary>Added to minimum and maximum damage: a quarter of the class's primary attribute.</summary>
        public static int DamageBonus(int primaryAttribute) => Mathf.Max(0, primaryAttribute) / 4;

        public static int HealthBonus(AttributeSet total) => total.vitality * HealthPerVitality + total.strength * HealthPerStrength;

        public static int ManaBonus(AttributeSet total) => total.intelligence * ManaPerIntelligence;

        public static float CritBonus(AttributeSet total) => total.agility * CritPerAgility;
    }
}
