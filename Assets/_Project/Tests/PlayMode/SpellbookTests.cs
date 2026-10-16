#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Items;
using RPG.Networking;
using RPG.Progression;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>The spellbook (the source game's spells, Tomes and ranks bought with experience) and status effects, on a real host.</summary>
    public class SpellbookTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string TomePath = "Assets/_Project/Data/Items/Tomes/TomeOfHammerOfMight.asset";

        private GameObject cameraObject;
        private GameObject networkObject;
        private NetworkManager network;
        private NetworkObject player;
        private CharacterProgress progress;
        private Spellbook spellbook;
        private Inventory inventory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            var bootstrap = networkObject.AddComponent<NetworkBootstrap>();

            yield return null;
            if (network.IsListening)
                yield return ShutdownNetwork();

            Assert.IsTrue(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < end)
                yield return null;

            player = network.LocalClient.PlayerObject;
            Assert.IsNotNull(player);
            progress = player.GetComponent<CharacterProgress>();
            spellbook = player.GetComponent<Spellbook>();
            inventory = player.GetComponent<Inventory>();
            Assert.IsNotNull(spellbook, "Run RPG > Build Test Scene first.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [Test]
        public void NewCharacter_KnowsTheLevelOneSpells_OnTheBar()
        {
            int known = 0;
            for (int i = 0; i < spellbook.Spells.Count; i++)
            {
                bool levelOne = spellbook.Get(i).learnLevel <= 1;
                Assert.AreEqual(levelOne, spellbook.Knows(i), spellbook.Get(i).displayName);
                if (!levelOne) continue;
                known++;
                Assert.GreaterOrEqual(spellbook.SlotOf(i), 0, $"{spellbook.Get(i).displayName} is on the bar.");
            }
            Assert.AreEqual(3, known, "The source game's classes start with three spells.");
        }

        [UnityTest]
        public IEnumerator RankUp_CostsExperience()
        {
            int cost = spellbook.NextRankCost(0);
            Assert.Greater(cost, 0);
            Assert.IsFalse(spellbook.ServerRankUp(0), "No experience to spend yet.");

            progress.ServerAddExperience(cost);
            yield return null;
            int toSpend = progress.ExperienceToSpend;
            Assert.IsTrue(spellbook.ServerRankUp(0));
            yield return null;

            Assert.AreEqual(2, spellbook.RankOf(0));
            Assert.AreEqual(toSpend - cost, progress.ExperienceToSpend);
            Assert.Greater(spellbook.NextRankCost(0), cost, "Every rank costs more.");
        }

        [UnityTest]
        public IEnumerator Tome_TeachesItsSpell_FromItsLevel()
        {
            var tome = AssetDatabase.LoadAssetAtPath<ItemDefinition>(TomePath);
            Assert.IsNotNull(tome);
            int spell = spellbook.IndexOf(tome.teachesSpell);
            Assert.GreaterOrEqual(spell, 0);
            inventory.ServerAddItem(tome, 1, false);
            yield return null;

            Assert.IsFalse(inventory.ServerUse(FindInBag(tome)), "Hammer of Might is a level 2 spell.");
            Assert.IsFalse(spellbook.Knows(spell));

            progress.ServerLoad(0, tome.itemLevel, 0, default, 0);
            yield return null;
            Assert.IsTrue(inventory.ServerUse(FindInBag(tome)));
            yield return null;

            Assert.IsTrue(spellbook.Knows(spell));
            Assert.GreaterOrEqual(spellbook.SlotOf(spell), 0, "A learned spell goes on a free slot.");
            Assert.AreEqual(0, inventory.CountOf(tome), "The Tome is used up.");
        }

        [UnityTest]
        public IEnumerator Assigning_MovesASpellBetweenSlots()
        {
            int spell = spellbook.SpellInSlot(0);
            Assert.GreaterOrEqual(spell, 0);
            Assert.IsTrue(spellbook.ServerAssign(5, spell));
            yield return null;
            Assert.AreEqual(spell, spellbook.SpellInSlot(5));
            Assert.AreNotEqual(spell, spellbook.SpellInSlot(0), "A spell sits in one slot only.");
            Assert.IsFalse(spellbook.ServerAssign(1, spellbook.Spells.Count - 1), "Unknown spells cannot go on the bar.");
        }

        [UnityTest]
        public IEnumerator Effects_SlowAndExpire()
        {
            StatusEffects effects = player.GetComponent<Health>().Effects;
            Assert.IsTrue(effects.ServerApply("Test Slow", Color.white, StatusKind.Slow, 40f, 0.5f));
            yield return null;
            Assert.AreEqual(0.6f, effects.SpeedMultiplier, 0.001f);
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(1f, effects.SpeedMultiplier, 0.001f, "Effects run out.");
        }

        [UnityTest]
        public IEnumerator Effects_ImmuneSilenceAndLessHealing()
        {
            Health health = player.GetComponent<Health>();
            StatusEffects effects = health.Effects;
            int full = health.Current;

            Assert.IsTrue(effects.ServerApply("Test Immune", Color.white, StatusKind.Immune, 1f, 2f, harmful: false));
            health.ApplyDamage(30, false);
            Assert.AreEqual(full, health.Current, "Immune: no damage.");
            Assert.IsFalse(effects.ServerApply("Test Stun", Color.white, StatusKind.Stun, 0f, 2f), "Immune: no harmful effects.");
            effects.ServerClear();

            health.ApplyDamage(40, false);
            int hurt = health.Current;
            Assert.IsTrue(effects.ServerApply("Test Sting", Color.white, StatusKind.HealingTaken, -50f, 2f));
            health.Heal(20);
            Assert.AreEqual(hurt + 10, health.Current, "Half the healing.");

            Assert.IsTrue(effects.ServerApply("Test Silence", Color.white, StatusKind.Silence, 0f, 2f));
            yield return null;
            Assert.IsTrue(effects.IsSilenced);
            Assert.IsFalse(effects.IsDisabled, "Silenced units can still move.");
        }

        private int FindInBag(ItemDefinition item)
        {
            int id = inventory.Catalog.IdOf(item);
            for (int i = 0; i < inventory.BagSize; i++)
                if (inventory.GetBagSlot(i).ItemId == id) return i;
            return -1;
        }

        private IEnumerator ShutdownNetwork()
        {
            if (network == null) yield break;
            if (network.IsListening) network.Shutdown();
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.IsListening || network.ShutdownInProgress) && Time.realtimeSinceStartup < end)
                yield return null;
        }
    }
}
#endif
