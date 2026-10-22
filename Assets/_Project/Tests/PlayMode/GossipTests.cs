#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RPG.Combat;
using RPG.Networking;
using RPG.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>The source game's gossip: what townsfolk say, which replies show, and the scripts replies run.</summary>
    public class GossipTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";

        /// <summary>A quest log that knows a fixed set of quests.</summary>
        private class FakeQuests : MonoBehaviour, IQuestFacts
        {
            public readonly HashSet<int> Taken = new HashSet<int>();
            public readonly HashSet<int> Finished = new HashSet<int>();
            public readonly HashSet<int> TurnedIn = new HashSet<int>();
            public bool HasQuest(int quest) => Taken.Contains(quest);
            public bool HasFinishedQuest(int quest) => Finished.Contains(quest);
            public bool TurnedInQuest(int quest) => TurnedIn.Contains(quest);
        }

        /// <summary>Halvard' menu (1) and the beer menu (26) as the source game has them, and a bank reply.</summary>
        private static GossipCatalog Catalog()
        {
            var catalog = ScriptableObject.CreateInstance<GossipCatalog>();
            catalog.texts = new[]
            {
                new GossipText(1, 1, "Greetings, stranger.", new GossipCondition(GossipCondition.HasOrDidQuest, 20, false)),
                new GossipText(25, 1, "Head down the road.", new GossipCondition(GossipCondition.HasQuest, 20, true)),
                new GossipText(143, 26, "Do you want some beer?", new GossipCondition(GossipCondition.HasAura, 277, false, "Drunk")),
                new GossipText(270, 26, "You've had enough, $PLAYER$.", new GossipCondition(GossipCondition.HasAura, 277, true, "Drunk")),
                new GossipText(221, 57, "<You take a big sip.>"),
            };
            catalog.options = new[]
            {
                new GossipOption(16, 26, "Sure.", 57, 6, new GossipCondition(GossipCondition.HasAura, 277, false, "Drunk")),
                new GossipOption(17, 26, "No Thanks.", GossipOption.Close, 0),
                new GossipOption(71, 26, "I'd like to make a deposit.", 0, 30),
                new GossipOption(80, 26, "I require item transmutation", GossipOption.Transmute, 0),
            };
            catalog.scripts = new[]
            {
                new GossipScriptStep(6, 0, GossipScriptStep.CastSpell, 277, "Drunk", 10f),
                new GossipScriptStep(30, 0, GossipScriptStep.OpenBank, 0, "", 0f),
            };
            return catalog;
        }

        [Test]
        public void BuiltCatalog_LoadsWithTheSourceGossip()
        {
            // A ScriptableObject whose class is not in a file of its own name loads as null in a build.
            var catalog = AssetDatabase.LoadAssetAtPath<GossipCatalog>("Assets/_Project/Data/World/GossipCatalog.asset");
            Assert.IsNotNull(catalog, "Run RPG > Build Test Scene first.");
            Assert.Greater(catalog.texts.Length, 100);
            Assert.Greater(catalog.options.Length, 50);
            Assert.IsNotNull(catalog.TextFor(1, null), "Halvard greets a stranger");
        }

        [Test]
        public void Format_FillsTheSourcePlaceholders()
        {
            Assert.AreEqual("Well met, Ana. A fine Mage, my girl. Ana the mage, says Aldric.",
                GossipCatalog.Format("Well met, $PLAYER$. A fine $CLASS$, my $G=boy:girl$. $N the $class$, says $NPC$.", "Ana", "Aldric", "Mage", true));
            Assert.AreEqual("my good sir", GossipCatalog.Format("$G=my good sir:my lady$", "Bo", "Pennywhit", "Warrior", false));
        }

        [Test]
        public void Text_IsTheFirstWhoseConditionsAreMet()
        {
            GossipCatalog catalog = Catalog();
            var player = new GameObject("Player");
            try
            {
                FakeQuests quests = player.AddComponent<FakeQuests>();
                Assert.AreEqual(1, catalog.TextFor(1, player).entry, "A stranger is greeted");
                quests.Taken.Add(20);
                Assert.AreEqual(25, catalog.TextFor(1, player).entry, "With the quest, Halvard points down the road");
                quests.Taken.Clear();
                quests.TurnedIn.Add(20);
                Assert.IsNull(catalog.TextFor(1, player), "Done with it: neither text");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Options_HideWhatThisProjectCannotDo_AndCheckTheMenus()
        {
            GossipCatalog catalog = Catalog();
            var player = new GameObject("Player");
            try
            {
                var options = new List<GossipOption>();
                catalog.OptionsFor(26, player, options);
                CollectionAssert.AreEqual(new[] { 16, 17 }, options.ConvertAll(o => o.entry), "No bank and no transmuting from gossip");

                Assert.IsTrue(catalog.Reaches(26, catalog.Option(16)));
                Assert.IsFalse(catalog.Reaches(1, catalog.Option(16)), "Halvard' menu does not lead to the beer");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(catalog);
            }
        }

        [UnityTest]
        public IEnumerator Beer_MakesThePlayerDrunk_OnlyNearTheBrewer()
        {
            var cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;
            var networkObject = new GameObject("Test NetworkManager");
            var network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            var bootstrap = networkObject.AddComponent<NetworkBootstrap>();
            GossipCatalog catalog = Catalog();
            var brewerObject = new GameObject("Brewer");
            try
            {
                yield return null;
                Assert.IsTrue(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
                float end = Time.realtimeSinceStartup + 5f;
                while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < end)
                    yield return null;
                NetworkObject player = network.LocalClient.PlayerObject;
                var conversation = player.GetComponent<Conversation>();
                Assert.IsNotNull(conversation, "Run RPG > Build Test Scene first.");
                var effects = player.GetComponent<StatusEffects>();

                brewerObject.transform.position = player.transform.position + new Vector3(30f, 0f, 0f);
                Townsperson brewer = brewerObject.AddComponent<Townsperson>();
                brewer.Configure(162, "Breda", "Brewer", 26, catalog);
                yield return null;
                Assert.IsFalse(conversation.ServerChoose(brewer, 16), "Too far away");

                brewerObject.transform.position = player.transform.position + new Vector3(0.5f, 0f, 0f);
                yield return null;
                Assert.IsFalse(conversation.ServerChoose(brewer, 71), "The bank is not here yet");
                Assert.IsTrue(conversation.ServerChoose(brewer, 16));
                yield return null;
                Assert.IsTrue(effects.Has(StatusKind.Flavor, "Drunk"));
                Assert.AreEqual(270, catalog.TextFor(26, player.gameObject).entry, "Drunk: \"You've had enough\"");
                Assert.IsFalse(conversation.ServerChoose(brewer, 16), "No second beer while drunk");
            }
            finally
            {
                Object.Destroy(brewerObject);
                if (network.IsListening) network.Shutdown();
                Object.DestroyImmediate(catalog);
            }
            float until = Time.realtimeSinceStartup + 5f;
            while ((network.IsListening || network.ShutdownInProgress) && Time.realtimeSinceStartup < until)
                yield return null;
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            yield return null;
        }
    }
}
#endif
