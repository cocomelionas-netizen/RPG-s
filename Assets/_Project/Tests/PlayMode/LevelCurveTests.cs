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

    /// <summary>Experience needed per level and experience from kills.</summary>
    public class LevelCurveTests
    {
        [Test]
        public void LevelCurve_NeedsMoreExperienceEachLevel_LikeDreadmyst()
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
