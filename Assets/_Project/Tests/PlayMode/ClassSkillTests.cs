#if UNITY_EDITOR
using NUnit.Framework;
using RPG.Progression;
using UnityEditor;

namespace RPG.Tests
{
    /// <summary>Each class trains only its own weapon skills, like the source game's SKILLS tab.</summary>
    public class ClassSkillTests
    {
        private static CharacterClassDefinition Class(string name)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterClassDefinition>($"Assets/_Project/Data/Classes/{name}.asset");
            Assert.IsNotNull(definition, name);
            return definition;
        }

        [Test]
        public void Warrior_TrainsMeleeAndShieldsOnly()
        {
            CharacterClassDefinition warrior = Class("Warrior");
            Assert.IsTrue(warrior.CanTrain(Attribute.Swords));
            Assert.IsTrue(warrior.CanTrain(Attribute.Shields));
            Assert.IsFalse(warrior.CanTrain(Attribute.Ranged));
            Assert.IsFalse(warrior.CanTrain(Attribute.Staves));
            Assert.IsTrue(warrior.CanTrain(Attribute.Strength));
            Assert.IsTrue(warrior.CanTrain(Attribute.Lockpicking));
        }

        [Test]
        public void RangerAndMage_TrainTheirWeaponsOnly()
        {
            CharacterClassDefinition ranger = Class("Ranger");
            Assert.IsTrue(ranger.CanTrain(Attribute.Ranged));
            Assert.IsTrue(ranger.CanTrain(Attribute.Daggers));
            Assert.IsFalse(ranger.CanTrain(Attribute.Swords));
            CharacterClassDefinition mage = Class("Mage");
            Assert.IsTrue(mage.CanTrain(Attribute.Staves));
            Assert.IsTrue(mage.CanTrain(Attribute.Wands));
            Assert.IsFalse(mage.CanTrain(Attribute.Shields));
        }
    }
}
#endif
