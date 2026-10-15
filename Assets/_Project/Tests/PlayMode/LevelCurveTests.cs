using NUnit.Framework;
using RPG.Progression;

namespace RPG.Tests
{
    /// <summary>Experience needed per level and experience from kills.</summary>
    public class LevelCurveTests
    {
        [Test]
        public void LevelCurve_NeedsMoreExperienceEachLevel()
        {
            for (int level = 1; level < LevelCurve.MaxLevel - 1; level++)
                Assert.Less(LevelCurve.ExperienceToNext(level), LevelCurve.ExperienceToNext(level + 1));
            Assert.AreEqual(0, LevelCurve.ExperienceToNext(LevelCurve.MaxLevel));
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
