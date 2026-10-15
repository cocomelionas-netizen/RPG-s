#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Dungeons;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Dungeon instances on a real host: entering, monsters and boss, leaving, closing, the boss slam.</summary>
    public class DungeonTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string LootBagPrefabPath = "Assets/_Project/Prefabs/World/LootBag.prefab";
        private const string DungeonPath = "Assets/_Project/Data/Dungeons/GoblinDen.asset";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject managerObject;
        private GameObject entranceObject;
        private NetworkManager network;
        private DungeonDefinition dungeon;
        private DungeonManager manager;
        private NetworkObject player;
        private Health playerHealth;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(DungeonPath);
            Assert.IsNotNull(dungeon, "Run RPG > Build Test Scene first.");

            managerObject = new GameObject("Dungeon Manager");
            manager = managerObject.AddComponent<DungeonManager>();
            manager.Configure(dungeon);

            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = dungeon.instancePrefab });
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = dungeon.bossPrefab });
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootBagPrefabPath) });
            foreach (MonsterPack pack in dungeon.packs)
                if (!network.NetworkConfig.Prefabs.Contains(pack.prefab))
                    network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = pack.prefab });
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
            playerHealth = player.GetComponent<Health>();

            // An entrance right next to the player.
            entranceObject = new GameObject("Test Entrance");
            entranceObject.transform.position = player.transform.position + new Vector3(0.5f, 0f, 0f);
            entranceObject.AddComponent<Portal>().Configure(PortalKind.DungeonEntrance, null, null, false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(managerObject);
            Object.Destroy(entranceObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UsingTheEntrance_OpensAnInstanceWithMonstersAndMovesThePlayerIn()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            yield return null;

            Assert.AreEqual(1, manager.Instances.Count);
            DungeonInstance instance = manager.Instances[0];
            Vector2 position = player.transform.position;
            Assert.IsTrue(instance.Contains(position), "The player is inside the dungeon.");

            WorldNavigation map = WorldNavigation.At(position);
            Assert.IsNotNull(map, "The dungeon has its own navigation.");
            Assert.IsTrue(map.IsWalkable(position));

            int expected = 1;
            foreach (MonsterPack pack in dungeon.packs) expected += pack.count;
            Assert.AreEqual(expected, instance.Monsters.Count, "Packs and the boss.");
            Assert.IsTrue(instance.Monsters.Exists(m => m.GetComponent<BossMechanics>() != null));
            foreach (NetworkObject monster in instance.Monsters)
                Assert.IsTrue(instance.Contains(monster.transform.position));
        }

        [UnityTest]
        public IEnumerator Leaving_ReturnsToTheEntrance()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;

            Assert.IsTrue(manager.ServerLeave(playerHealth));
            yield return null;
            Assert.Less(Vector2.Distance(player.transform.position, entranceObject.transform.position), 1.5f);
            Assert.IsNull(manager.InstanceAt(player.transform.position));
        }

        [UnityTest]
        public IEnumerator BossExit_IsClosedUntilTheBossDies()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            DungeonInstance instance = manager.Instances[0];
            Portal bossExit = null;
            foreach (Portal portal in Portal.Portals)
                if (portal.Kind == PortalKind.DungeonExit && portal.Instance == instance && portal.name == "Boss Exit") bossExit = portal;
            Assert.IsNotNull(bossExit);
            Assert.IsFalse(bossExit.IsOpen);

            NetworkObject boss = instance.Monsters.Find(m => m.GetComponent<BossMechanics>() != null);
            boss.GetComponent<Health>().ApplyDamage(100000, false, playerHealth);
            yield return null;

            Assert.IsTrue(instance.BossDefeated);
            Assert.IsTrue(bossExit.IsOpen);
        }

        [UnityTest]
        public IEnumerator EmptyInstance_IsClosedWithItsMonsters()
        {
            manager.CloseAfterEmptySeconds = 0.2f;
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            DungeonInstance instance = manager.Instances[0];
            NetworkObject monster = instance.Monsters[0];

            Assert.IsTrue(manager.ServerLeave(playerHealth));
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(0, manager.Instances.Count);
            Assert.IsTrue(instance == null || !instance.IsSpawned, "The instance was despawned.");
            Assert.IsTrue(monster == null || !monster.IsSpawned, "Its monsters too.");
        }

        [UnityTest]
        public IEnumerator BossSlam_HitsPlayersInsideTheRing()
        {
            GameObject bossObject = Object.Instantiate(dungeon.bossPrefab, player.transform.position + new Vector3(1f, 0f, 0f), Quaternion.identity);
            bossObject.GetComponent<MonsterAI>().AggroRadius = 0f;
            bossObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            var mechanics = bossObject.GetComponent<BossMechanics>();
            yield return null;

            int before = playerHealth.Current;
            mechanics.ServerSlamNow();
            Assert.AreEqual(before, playerHealth.Current, "Nothing happens during the warning.");

            yield return new WaitForSeconds(mechanics.WarningSeconds + 0.2f);
            Assert.Less(playerHealth.Current, before, "The slam landed on the player inside the ring.");
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
