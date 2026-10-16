using NUnit.Framework;
using RPG.Progression;

namespace RPG.Tests
{
    /// <summary>Every level has its own rank.</summary>
    public class RankTests
    {
        [Test]
        public void EveryLevelHasARank_FromRecruitToChampion()
        {
            Assert.AreEqual(25, LevelCurve.MaxLevel);
            Assert.AreEqual("Recruit", LevelCurve.RankName(1));
            Assert.AreEqual("Champion", LevelCurve.RankName(LevelCurve.MaxLevel));
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int level = 1; level <= LevelCurve.MaxLevel; level++)
                Assert.IsTrue(seen.Add(LevelCurve.RankName(level)), $"Rank of level {level} repeats.");
        }
    }

    /// <summary>The source game's health and the damage growth that keeps fights as long.</summary>
    public class SourceBalanceTests
    {
        [Test]
        public void MonsterHealth_FollowsTheSourceGamesNormalMonsters()
        {
            Assert.AreEqual(60f, RPG.Monsters.MonsterAI.SourceHealthAt(1), 0.5f, "Antling");
            Assert.AreEqual(7340f, RPG.Monsters.MonsterAI.SourceHealthAt(25), 1f, "Zhendale Brute");
            Assert.AreEqual(3f, RPG.Monsters.MonsterAI.EliteHealthScale, "Zhendale Elite 22025 / Brute 7340");
            for (int level = 2; level <= 30; level++)
                Assert.Greater(RPG.Monsters.MonsterAI.SourceHealthAt(level), RPG.Monsters.MonsterAI.SourceHealthAt(level - 1));
        }

        [Test]
        public void PlayerDamage_GrowsWithLevel()
        {
            Assert.AreEqual(1.54f, LevelCurve.DamageGrowth(1), 0.01f);
            Assert.AreEqual(12.7f, LevelCurve.DamageGrowth(25), 0.1f);
            Assert.AreEqual(2812, UnityEngine.Mathf.RoundToInt(75 * LevelCurve.HealthGrowth(25)), "A warrior's health at 25 in the source game.");
        }
    }

    /// <summary>Experience needed per level and experience from kills.</summary>
    public class LevelCurveTests
    {
        [Test]
        public void LevelCurve_NeedsMoreExperienceEachLevel_LikeTheSourceGame()
        {
            for (int level = 1; level < LevelCurve.MaxLevel - 1; level++)
                Assert.LessOrEqual(LevelCurve.ExperienceToNext(level), LevelCurve.ExperienceToNext(level + 1));
            Assert.AreEqual(100, LevelCurve.ExperienceToNext(1));
            Assert.AreEqual(1087, LevelCurve.ExperienceToNext(10));
            Assert.AreEqual(0, LevelCurve.ExperienceToNext(LevelCurve.MaxLevel));
            Assert.AreEqual(20, LevelCurve.KillExperienceFor(1));
            Assert.AreEqual(2389, LevelCurve.KillExperienceFor(25));
        }

        [Test]
        public void HealthGrowth_IsTheLevel_AndHalfAgainFrom21_LikeTheSourceGame()
        {
            // The source game's warrior: 75, 150 ... 1500 at 20, 2362.5 at 21, 2812.5 at 25.
            Assert.AreEqual(1f, LevelCurve.HealthGrowth(1));
            Assert.AreEqual(20f, LevelCurve.HealthGrowth(20));
            Assert.AreEqual(31.5f, LevelCurve.HealthGrowth(21));
            Assert.AreEqual(2812.5f, 75f * LevelCurve.HealthGrowth(25));
        }

        [Test]
        public void LevelCurve_GainCanLevelUpSeveralTimes()
        {
            int level = 1;
            int experience = 0;
            int amount = LevelCurve.ExperienceToNext(1) + LevelCurve.ExperienceToNext(2) + 5;
            LevelCurve.Gain(ref level, ref experience, amount);
            Assert.AreEqual(3, level);
            Assert.AreEqual(5, experience);

            level = LevelCurve.MaxLevel;
            experience = 0;
            LevelCurve.Gain(ref level, ref experience, 1000);
            Assert.AreEqual(LevelCurve.MaxLevel, level);
            Assert.AreEqual(0, experience);
        }

        [Test]
        public void LevelCurve_LessExperienceForLowerLevelMonsters()
        {
            Assert.AreEqual(20, LevelCurve.KillExperience(20, 2, 1));
            Assert.AreEqual(20, LevelCurve.KillExperience(20, 2, 2));
            Assert.AreEqual(16, LevelCurve.KillExperience(20, 2, 3));
            Assert.AreEqual(2, LevelCurve.KillExperience(20, 2, 15));
        }
    }
}
