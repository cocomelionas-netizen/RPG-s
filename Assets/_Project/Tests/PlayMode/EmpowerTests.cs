using NUnit.Framework;
using RPG.Items;
using UnityEngine;

namespace RPG.Tests
{
    /// <summary>Orb empowerment bookkeeping: steps and points on the stack, which gear an orb fits, chances and points.</summary>
    public class EmpowerTests
    {
        [Test]
        public void Stack_KeepsStepsAndSpentPoints()
        {
            ItemStack stack = new ItemStack(5, 1, 7).WithEmpower(2, 3);
            Assert.AreEqual(2, stack.EmpowerSteps);
            Assert.AreEqual(3, stack.EmpowerSpent);
            Assert.AreEqual(2, stack.WithCount(1).EmpowerSteps, "Copies keep the empowerment.");
            Assert.IsFalse(stack.SameKind(new ItemStack(5, 1, 7)), "An empowered item is not the same as a plain one.");
        }

        [Test]
        public void Orb_FitsOnlyItsGear()
        {
            var orb = ScriptableObject.CreateInstance<ItemDefinition>();
            orb.use = ItemUse.Empower;
            orb.empowerTarget = EmpowerTarget.Armor;
            Assert.IsTrue(orb.CanEmpower(ItemSlot.Chest));
            Assert.IsTrue(orb.CanEmpower(ItemSlot.Hands));
            Assert.IsFalse(orb.CanEmpower(ItemSlot.Weapon));
            Assert.IsFalse(orb.CanEmpower(ItemSlot.Ring));
            orb.empowerTarget = EmpowerTarget.Weapon;
            Assert.IsTrue(orb.CanEmpower(ItemSlot.Shield));
            Object.DestroyImmediate(orb);
        }

        [Test]
        public void Points_GrowWithQuality_ChanceWithPower()
        {
            Assert.Less(ItemDefinition.EmpowerPoints(ItemRarity.Uncommon), ItemDefinition.EmpowerPoints(ItemRarity.Epic));
            Assert.Less(ItemDefinition.EmpowerChance(2), ItemDefinition.EmpowerChance(5));
            Assert.AreEqual(1f, ItemDefinition.EmpowerChance(6));
        }

        [Test]
        public void EmpoweredWeapon_GlowsFromPlusThree()
        {
            Assert.AreEqual(0f, RPG.Characters.HeroAppearance.GlowOf(3).a, "No glow below +4.");
            for (int steps = 5; steps <= RPG.Player.Cheats.MaxWeaponGlowSteps; steps++)
                if (steps != 7 && steps != 10)
                    Assert.Greater(RPG.Characters.HeroAppearance.GlowOf(steps).a, RPG.Characters.HeroAppearance.GlowOf(steps - 1).a,
                        $"+{steps} glows more than +{steps - 1}.");
            Color white = RPG.Characters.HeroAppearance.GlowOf(5), blue = RPG.Characters.HeroAppearance.GlowOf(8),
                orange = RPG.Characters.HeroAppearance.GlowOf(11);
            Assert.Greater(white.g, 0.9f, "+4 to +6: white.");
            Assert.Greater(blue.b, blue.r + 0.3f, "+7 to +9: blue.");
            Assert.Greater(orange.r, orange.b + 0.4f, "+10 to +12: orange.");
            Assert.AreEqual(12, ItemDefinition.MaxEmpowerSteps, "Up to +12.");
        }
    }
}
