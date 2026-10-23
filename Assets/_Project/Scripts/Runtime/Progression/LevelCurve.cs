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

        // The source game's tables (player_exp_levels): experience to reach the next level, and what a kill of a monster
        // of each level is worth, for levels 1 to 25.
        private static readonly int[] ExperienceTable =
        {
            100, 100, 123, 150, 195, 267, 373, 531, 760, 1087, 1548, 2194, 3091,
            4331, 6031, 8352, 11510, 15788, 21564, 29339, 39780, 53762, 72451, 97380, 129293,
        };

        private static readonly int[] KillExperienceTable =
        {
            20, 20, 24, 30, 39, 44, 62, 88, 126, 155, 186, 223, 268,
            322, 386, 463, 556, 667, 800, 960, 1152, 1382, 1659, 1991, 2389,
        };

        /// <summary>Experience needed to go from <paramref name="level"/> to the next one (0 at max level).</summary>
        public static int ExperienceToNext(int level)
        {
            if (level >= MaxLevel) return 0;
            return ExperienceTable[Mathf.Max(1, level) - 1];
        }

        /// <summary>What killing a monster of <paramref name="monsterLevel"/> is worth (before the outlevelling cut).</summary>
        /// <summary>
        /// How a class's health and mana grow, from the source game's <c>player_class_stats</c>: the level-1 value times the
        /// level, and half again from level 21 (a warrior: 75, 150 ... 1500 at 20, then 2362.5 at 21, 2812.5 at 25).
        /// </summary>
        public static float HealthGrowth(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            return level > 20 ? level * 1.5f : level;
        }

        /// <summary>
        /// How much harder players hit at a level (all their damage: weapons, abilities, spells, damage over time). The
        /// source game's monsters have far more health at high levels (60 at level 1, 7340 at 25) than ours had, and its
        /// server keeps the formulas that make players hit that hard; this makes a fight with a monster of your level last
        /// about 10 seconds of auto-attacks with gear of your level (Docs/Balance.md): 2 times at level 1, about 16.5 at 25.
        /// </summary>
        public static float DamageGrowth(int level) => 2f * Mathf.Pow(Mathf.Clamp(level, 1, MaxLevel), 0.656f);

        public static int KillExperienceFor(int monsterLevel) => KillExperienceTable[Mathf.Clamp(monsterLevel, 1, MaxLevel) - 1];

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
