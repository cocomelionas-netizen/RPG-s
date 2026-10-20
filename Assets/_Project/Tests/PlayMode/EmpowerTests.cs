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
    }
}
