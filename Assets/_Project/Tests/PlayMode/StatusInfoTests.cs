using NUnit.Framework;
using RPG.Combat;
using Unity.Collections;

namespace RPG.Tests
{
    /// <summary>What a buff or debuff's tooltip says.</summary>
    public class StatusInfoTests
    {
        private static StatusView View(StatusKind kind, string name, float value, bool harmful, DamageSchool school = DamageSchool.Physical,
            byte stacks = 1) =>
            new StatusView
            {
                Kind = kind, Name = new FixedString32Bytes(name), Value = value, Harmful = harmful, School = school, Stacks = stacks,
                StartedAt = 0, EndsAt = 12,
            };

        [Test]
        public void Effect_SaysWhatItDoes_WithItsNumbers()
        {
            Assert.AreEqual("Moves 30% slower.", StatusInfo.Effect(View(StatusKind.Slow, "Chains of Ice", 30f, true)));
            Assert.AreEqual("Takes 45 fire damage over its duration.", StatusInfo.Effect(View(StatusKind.DamageOverTime, "Ignite", 45f, true, DamageSchool.Fire)));
            Assert.AreEqual("Takes 20% more physical damage.", StatusInfo.Effect(View(StatusKind.DamageTaken, "Divine Strike", 10f, true, stacks: 2)));
            Assert.AreEqual("Stunned: cannot move, attack or cast.", StatusInfo.Effect(View(StatusKind.Stun, "Charge", 0f, true)));
        }

        [Test]
        public void Tooltip_NamesIt_SaysBuffOrDebuff_AndTheTimeLeft()
        {
            string tooltip = StatusInfo.Tooltip(View(StatusKind.Haste, "Pinions", 50f, false), 2.0);
            StringAssert.Contains("Pinions", tooltip);
            StringAssert.Contains("Buff", tooltip);
            StringAssert.Contains("Moves 50% faster.", tooltip);
            StringAssert.Contains("10 s left", tooltip);
        }
    }
}
