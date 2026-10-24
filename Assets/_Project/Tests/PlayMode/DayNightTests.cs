using NUnit.Framework;
using RPG.World;

namespace RPG.Tests
{
    /// <summary>The day and night cycle: when it is night, and how dark a zone gets.</summary>
    public class DayNightTests
    {
        [Test]
        public void Cycle_HasADayAndANight()
        {
            double cycle = DayNight.CycleMinutes * 60.0;
            Assert.AreEqual(0f, DayNight.NightAt(0), "A cycle begins with the day.");
            Assert.AreEqual(1f, DayNight.NightAt(cycle * 0.8), "And has its night after the dusk.");
            Assert.AreEqual(DayNight.NightAt(cycle * 0.3), DayNight.NightAt(cycle * 1.3), "Every day is the same.");
            float dusk = DayNight.NightAt(cycle * 0.64);
            Assert.That(dusk, Is.GreaterThan(0f).And.LessThan(1f), "Dusk falls slowly.");
        }

        [Test]
        public void Zones_AreAsDarkAsTheirNight()
        {
            Assert.AreEqual(0f, DayNight.DarknessOf(0f, 0.45f, false), "No darkness by day outdoors.");
            Assert.Greater(DayNight.DarknessOf(1f, 0.45f, false), DayNight.DarknessOf(1f, 0.05f, false), "Graveyards are darker than roads.");
            Assert.Greater(DayNight.DarknessOf(0f, 0.4f, true), 0.5f, "Dungeons are dark by day too.");
            Assert.LessOrEqual(DayNight.DarknessOf(1f, 1f, true), 0.8f, "Never quite black.");
        }

        [Test]
        public void ToggleNight_JumpsToTheNightAndBack()
        {
            DayNight.ToggleNight();
            double now = System.DateTime.UtcNow.Subtract(System.DateTime.UnixEpoch).TotalSeconds;
            Assert.AreEqual(1f, DayNight.NightAt(now + DayNight.ClockShift), "The middle of the night.");
            DayNight.ToggleNight();
            Assert.AreEqual(0.0, DayNight.ClockShift);
        }
    }
}
