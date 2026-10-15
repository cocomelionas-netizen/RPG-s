#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Items;
using RPG.Monsters;
using RPG.Networking;
using RPG.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Chests in the world and elite monsters, on a real host.</summary>
    public class ChestTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string CratePrefabPath = "Assets/_Project/Prefabs/World/WoodenCrate.prefab";
        private const string LootBagPrefabPath = "Assets/_Project/Prefabs/World/LootBag.prefab";
        private const string GoblinPrefabPath = "Assets/_Project/Prefabs/Monsters/Goblin.prefab";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject spawnerObject;
        private NetworkManager network;
        private GameObject cratePrefab;
        private GameObject goblinPrefab;
        private NetworkObject player;
        private Inventory inventory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefabPath);
            goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
            Assert.IsNotNull(cratePrefab, "Run RPG > Build Test Scene first.");

            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = cratePrefab });
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = goblinPrefab });
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootBagPrefabPath) });
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
            inventory = player.GetComponent<Inventory>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (spawnerObject != null) Object.Destroy(spawnerObject);
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        private ChestSpawner PlaceCrate(Vector3 offset, float respawnSeconds)
        {
            spawnerObject = new GameObject("Test Chest Spot");
            spawnerObject.transform.position = player.transform.position + offset;
            var spawner = spawnerObject.AddComponent<ChestSpawner>();
            spawner.Configure(cratePrefab, respawnSeconds);
            return spawner;
        }

        [UnityTest]
        public IEnumerator OpeningAChest_GivesGoldAndRemovesIt()
        {
            ChestSpawner spawner = PlaceCrate(new Vector3(0.5f, 0f, 0f), 60f);
            yield return null;
            yield return null;
            Assert.IsNotNull(spawner.Current, "The spawner put a crate there.");
            var chest = spawner.Current.GetComponent<LootChest>();
            int goldBefore = inventory.Gold;

            Assert.IsTrue(chest.ServerOpen(player.GetComponent<Health>()));
            yield return null;

            Assert.Greater(inventory.Gold, goldBefore);
            Assert.IsNull(spawner.Current, "Opened chests disappear.");
        }

        [UnityTest]
        public IEnumerator Chest_FarAway_DoesNotOpen()
        {
            ChestSpawner spawner = PlaceCrate(new Vector3(6f, 0f, 0f), 60f);
            yield return null;
            yield return null;
            Assert.IsFalse(spawner.Current.GetComponent<LootChest>().ServerOpen(player.GetComponent<Health>()));
        }

        [UnityTest]
        public IEnumerator OpenedChest_ComesBack()
        {
            ChestSpawner spawner = PlaceCrate(new Vector3(0.5f, 0f, 0f), 0.2f);
            yield return null;
            yield return null;
            spawner.Current.GetComponent<LootChest>().ServerOpen(player.GetComponent<Health>());

            float end = Time.realtimeSinceStartup + 3f;
            while (spawner.Current == null && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.IsNotNull(spawner.Current);
        }

        [UnityTest]
        public IEnumerator EliteMonster_IsTougherAndNamedSo()
        {
            GameObject goblin = Object.Instantiate(goblinPrefab, player.transform.position + new Vector3(4f, 0f, 0f), Quaternion.identity);
            goblin.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            yield return null;
            var health = goblin.GetComponent<Health>();
            var ai = goblin.GetComponent<MonsterAI>();
            int normalMax = health.Max;
            Vector3 normalScale = goblin.transform.localScale;

            ai.ServerMakeElite();
            yield return null;

            Assert.IsTrue(ai.IsElite);
            Assert.AreEqual(Mathf.RoundToInt(normalMax * MonsterAI.EliteHealthScale), health.Max);
            Assert.AreEqual(health.Max, health.Current);
            StringAssert.StartsWith("Elite", health.DisplayName);
            Assert.Greater(goblin.transform.localScale.x, normalScale.x);
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
