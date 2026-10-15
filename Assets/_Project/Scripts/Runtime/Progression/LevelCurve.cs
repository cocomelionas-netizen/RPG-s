using UnityEngine;

namespace RPG.Progression
{
    /// <summary>
    /// How much experience each level needs, what gaining experience does to level and progress,
    /// and the rank that goes with each level.
    /// </summary>
    public static class LevelCurve
    {
        public const int MaxLevel = 25;

        private static readonly string[] Ranks =
        {
            "Recruit", "Private", "Lance Corporal", "Corporal", "Sergeant", "Staff Sergeant", "Master Sergeant",
            "First Sergeant", "Sergeant Major", "Second Lieutenant", "First Lieutenant", "Captain", "Major",
            "Lieutenant Colonel", "Colonel", "Brigadier", "Major General", "Lieutenant General", "General",
            "Field Marshal", "Knight", "Baron", "Count", "Earl", "Champion",
        };

        /// <summary>The rank title of a level, from Recruit (1) to Champion (25).</summary>
        public static string RankName(int level) => Ranks[Mathf.Clamp(level, 1, MaxLevel) - 1];

        /// <summary>Experience needed to go from <paramref name="level"/> to the next one (0 at max level).</summary>
        public static int ExperienceToNext(int level)
        {
            if (level >= MaxLevel) return 0;
            return Mathf.RoundToInt(40f * Mathf.Pow(Mathf.Max(1, level), 1.5f) + 20f);
        }

        /// <summary>
        /// Adds experience, levelling up as many times as it covers. Experience is the progress into the current
        /// level; at max level it stays 0.
        /// </summary>
        public static void Gain(ref int level, ref int experience, int amount)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            if (level >= MaxLevel || amount <= 0)
            {
                if (level >= MaxLevel) experience = 0;
                return;
            }

            experience += amount;
            while (level < MaxLevel && experience >= ExperienceToNext(level))
            {
                experience -= ExperienceToNext(level);
                level++;
            }
            if (level >= MaxLevel) experience = 0;
        }

        /// <summary>
        /// Experience for killing a monster: full for monsters at or above your level, less the more you outlevel it
        /// (20 % less per level, at least 10 %).
        /// </summary>
        public static int KillExperience(int baseReward, int monsterLevel, int playerLevel)
        {
            int levelsAbove = Mathf.Max(0, playerLevel - monsterLevel);
            float scale = Mathf.Max(0.1f, 1f - 0.2f * levelsAbove);
            return Mathf.Max(1, Mathf.RoundToInt(baseReward * scale));
        }
    }
}
