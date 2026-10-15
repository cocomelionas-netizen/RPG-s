using UnityEngine;

namespace RPG.Combat
{
    /// <summary>What kind of damage a hit is: armor works on all of it, resists on their own school (Dreadmyst's schools).</summary>
    public enum DamageSchool : byte
    {
        Physical,
        Frost,
        Fire,
        Shadow,
        Holy,
    }

    public static class DamageSchools
    {
        /// <summary>Colour of a school's damage numbers (white for physical).</summary>
        public static Color ColorOf(DamageSchool school)
        {
            switch (school)
            {
                case DamageSchool.Frost: return new Color(0.55f, 0.85f, 1f);
                case DamageSchool.Fire: return new Color(1f, 0.55f, 0.2f);
                case DamageSchool.Shadow: return new Color(0.75f, 0.45f, 1f);
                case DamageSchool.Holy: return new Color(1f, 0.92f, 0.45f);
                default: return Color.white;
            }
        }
    }
}
