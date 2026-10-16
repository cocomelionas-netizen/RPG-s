#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Dungeons;
using RPG.Items;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Networking;
using RPG.Progression;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>
    /// The source game's dungeons on a real host: entering (level, key), its monsters and containers, the bosses and their
    /// keys, leaving through the exits, closing, the boss slam, and the key recipes.
    /// </summary>
    public class DungeonTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string LootBagPrefabPath = "Assets/_Project/Prefabs/World/LootBag.prefab";
        private const string DungeonsFolder = "Assets/_Project/Data/Dungeons";
        private const string ItemsFolder = "Assets/_Project/Data/Items";
        // The source game's Decrepit Tower: level 25, a Tower Key, two bosses, many containers.
        private const string TowerPath = DungeonsFolder + "/DecrepitTower.asset";
        private const string GarrisonPath = DungeonsFolder + "/TheLowerGarrison.asset";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject managerObject;
        private GameObject entranceObject;
        private NetworkManager network;
        private DungeonDefinition dungeon;
        private DungeonDefinition garrison;
        private DungeonManager manager;
        private NetworkObject player;
        private Health playerHealth;
        private Inventory inventory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(TowerPath);
            garrison = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(GarrisonPath);
            Assert.IsNotNull(dungeon, "Run RPG > Build Test Scene first.");
            Assert.IsNotNull(garrison);

            managerObject = new GameObject("Dungeon Manager");
            manager = managerObject.AddComponent<DungeonManager>();
            manager.Configure(dungeon, garrison);

            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            AddPrefab(dungeon.instancePrefab);
            AddPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LootBagPrefabPath));
            foreach (DungeonDefinition definition in new[] { dungeon, garrison })
            {
                foreach (DungeonMonsterSpot spot in definition.monsters) AddPrefab(spot.prefab);
                foreach (DungeonChestSpot spot in definition.chests) AddPrefab(spot.prefab);
            }
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
            inventory = player.GetComponent<Inventory>();

            // Meet the entry requirements: the minimum level and keys.
            var progress = player.GetComponent<CharacterProgress>();
            while (progress.Level < dungeon.minLevel)
                progress.ServerAddExperience(LevelCurve.ExperienceToNext(progress.Level));
            Assert.IsNotNull(dungeon.key, "The tower needs a key.");
            inventory.ServerAddItem(dungeon.key, 3, false);

            // An entrance into the tower right next to the player.
            entranceObject = new GameObject("Test Entrance");
            entranceObject.transform.position = player.transform.position + new Vector3(0.5f, 0f, 0f);
            entranceObject.AddComponent<Portal>().ConfigureEntrance(null, null, dungeon);
            yield return null;
        }

        private void AddPrefab(GameObject prefab)
        {
            if (prefab != null && !network.NetworkConfig.Prefabs.Contains(prefab))
                network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
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

        [Test]
        public void Plan_IsTheSourceMap_WithEverythingOnTheFloor()
        {
            DungeonLayout layout = dungeon.Layout;
            Assert.AreEqual(80, layout.Width, "fort_amir.map is 80 x 80.");
            Assert.AreEqual(80, layout.Height);
            Assert.Greater(layout.FloorCells().Count, 1000);
            Assert.IsTrue(layout.IsFloor(dungeon.arrival), "Players arrive on the floor.");
            foreach (DungeonMonsterSpot spot in dungeon.monsters)
                Assert.IsTrue(layout.IsFloor(spot.cell), $"{spot.displayName} stands on the floor.");
            foreach (DungeonChestSpot spot in dungeon.chests)
                Assert.IsTrue(layout.IsFloor(spot.cell), "Every container stands on the floor.");
            Assert.Greater(dungeon.exits.Length, 0, "There is a way out.");
        }

        [Test]
        public void Rules_AreTheSourceGames()
        {
            Assert.AreEqual(25, dungeon.minLevel);
            Assert.AreEqual(4, dungeon.maxPlayers);
            Assert.AreEqual("Tower Key", dungeon.key.displayName);
            Assert.IsNull(garrison.key, "The Lower Garrison needs no key.");

            var barracks = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(DungeonsFolder + "/AbandonedBarracks.asset");
            Assert.IsNotNull(barracks);
            Assert.AreEqual(1, barracks.maxPlayers, "The barracks are entered alone.");
            Assert.AreEqual("Keys to the Barracks", barracks.key.displayName);
        }

        [UnityTest]
        public IEnumerator UsingTheEntrance_OpensAnInstanceWithItsMonstersAndContainers()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            yield return null;

            Assert.AreEqual(1, manager.Instances.Count);
            DungeonInstance instance = manager.Instances[0];
            Assert.AreEqual(dungeon, instance.Definition);
            Vector2 position = player.transform.position;
            Assert.IsTrue(instance.Contains(position), "The player is inside the dungeon.");

            WorldNavigation map = WorldNavigation.At(position);
            Assert.IsNotNull(map, "The dungeon has its own navigation.");
            Assert.IsTrue(map.IsWalkable(position));

            Assert.AreEqual(dungeon.monsters.Length, instance.Monsters.Count, "Every monster of the plan.");
            Assert.AreEqual(dungeon.chests.Length, instance.Chests.Count, "Every container of the plan.");
            foreach (NetworkObject monster in instance.Monsters)
                Assert.IsTrue(instance.Contains(monster.transform.position));
            Assert.AreEqual(2, instance.BossesLeft, "Lord Zargan and Tchazzar.");
        }

        [UnityTest]
        public IEnumerator Bosses_HaveTheirNamesAndLevels()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            DungeonInstance instance = manager.Instances[0];

            NetworkObject zargan = instance.Monsters.Find(m => m.GetComponent<Health>().DisplayName == "Lord Zargan");
            Assert.IsNotNull(zargan);
            Assert.AreEqual(30, zargan.GetComponent<MonsterAI>().Level);
            NetworkObject horror = instance.Monsters.Find(m => m.GetComponent<Health>().DisplayName == "Undead Horror");
            Assert.IsNotNull(horror);
            Assert.Greater(zargan.GetComponent<Health>().Max, horror.GetComponent<Health>().Max * 3, "A boss is far tougher than its elites.");
        }

        [UnityTest]
        public IEnumerator Entering_UsesUpOneKey()
        {
            int keys = inventory.CountOf(dungeon.key);
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            Assert.AreEqual(keys - 1, inventory.CountOf(dungeon.key));
        }

        [UnityTest]
        public IEnumerator Entering_WithoutAKey_IsRefused()
        {
            Assert.IsTrue(inventory.ServerRemoveItem(dungeon.key, inventory.CountOf(dungeon.key)));
            yield return null;

            Assert.IsFalse(manager.ServerUsePortal(playerHealth));
            Assert.AreEqual(0, manager.Instances.Count);
            Assert.IsNotNull(manager.EntryRefusal(playerHealth, true, dungeon));
        }

        [UnityTest]
        public IEnumerator Entering_BelowTheMinimumLevel_IsRefused()
        {
            player.GetComponent<CharacterProgress>().ServerLoad(0, 1, 0, default, 0);
            yield return null;

            int keys = inventory.CountOf(dungeon.key);
            Assert.IsFalse(manager.ServerUsePortal(playerHealth));
            Assert.AreEqual(0, manager.Instances.Count);
            Assert.AreEqual(keys, inventory.CountOf(dungeon.key), "No key is used when refused.");
        }

        [UnityTest]
        public IEnumerator Leaving_ThroughAnExit_GoesWhereItLeads()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            DungeonInstance instance = manager.Instances[0];

            Portal exit = null;
            foreach (Portal portal in Portal.Portals)
                if (portal.Kind == PortalKind.DungeonExit && portal.Instance == instance) exit = portal;
            Assert.IsNotNull(exit, "The tower has an exit.");
            Assert.IsTrue(exit.IsOpen, "Exits are always open, like the source game's.");

            Assert.IsTrue(manager.ServerLeave(playerHealth, exit));
            yield return null;
            Assert.Less(Vector2.Distance(player.transform.position, exit.Destination), 0.5f);
            Assert.IsNull(manager.InstanceAt(player.transform.position));
        }

        [UnityTest]
        public IEnumerator Leaving_WithoutAnExit_ReturnsToTheEntrance()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;

            Assert.IsTrue(manager.ServerLeave(playerHealth));
            yield return null;
            Assert.Less(Vector2.Distance(player.transform.position, entranceObject.transform.position), 1.5f);
        }

        [UnityTest]
        public IEnumerator KillingEveryBoss_BeatsTheDungeon()
        {
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            DungeonInstance instance = manager.Instances[0];
            Assert.IsFalse(instance.BossDefeated);

            foreach (NetworkObject monster in instance.Monsters.ToArray())
                if (monster.GetComponent<BossMechanics>() != null)
                    monster.GetComponent<Health>().ApplyDamage(10000000, false, playerHealth);
            yield return null;

            Assert.IsTrue(instance.BossDefeated);
        }

        [UnityTest]
        public IEnumerator Melchon_DropsTowerKeys()
        {
            entranceObject.GetComponent<Portal>().ConfigureEntrance(null, null, garrison);
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            DungeonInstance instance = manager.Instances[0];
            Assert.AreEqual(garrison, instance.Definition);

            NetworkObject melchon = instance.Monsters.Find(m => m.GetComponent<Health>().DisplayName == "Melchon");
            Assert.IsNotNull(melchon);
            Vector2 at = melchon.transform.position;
            melchon.GetComponent<Health>().ApplyDamage(10000000, false, playerHealth);
            yield return null;

            int towerKeys = 0;
            foreach (LootBag bag in Object.FindObjectsByType<LootBag>())
                if (Vector2.Distance(bag.transform.position, at) < 1f)
                    foreach (LootItem item in bag.Contents)
                        if (item.Item == dungeon.key) towerKeys += item.Count;
            Assert.AreEqual(10, towerKeys, "Melchon drops 10 Tower Keys, like in the source game.");
        }

        [UnityTest]
        public IEnumerator EmptyInstance_IsClosedWithItsMonstersAndContainers()
        {
            manager.CloseAfterEmptySeconds = 0.2f;
            Assert.IsTrue(manager.ServerUsePortal(playerHealth));
            yield return null;
            DungeonInstance instance = manager.Instances[0];
            NetworkObject monster = instance.Monsters[0];
            NetworkObject chest = instance.Chests[0];

            Assert.IsTrue(manager.ServerLeave(playerHealth));
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(0, manager.Instances.Count);
            Assert.IsTrue(instance == null || !instance.IsSpawned, "The instance was despawned.");
            Assert.IsTrue(monster == null || !monster.IsSpawned, "Its monsters too.");
            Assert.IsTrue(chest == null || !chest.IsSpawned, "And its containers.");
        }

        [UnityTest]
        public IEnumerator BossSlam_HitsPlayersInsideTheRing()
        {
            GameObject bossPrefab = null;
            foreach (DungeonMonsterSpot spot in dungeon.monsters)
                if (spot.boss) bossPrefab = spot.prefab;
            Assert.IsNotNull(bossPrefab);
            GameObject bossObject = Object.Instantiate(bossPrefab, player.transform.position + new Vector3(1f, 0f, 0f), Quaternion.identity);
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

        [UnityTest]
        public IEnumerator DaleGem_OnADepletedKey_MakesAnUndercourseKey()
        {
            var gem = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "/DaleGem.asset");
            var depleted = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "/DepletedKey.asset");
            var undercourse = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "/UndercourseKey.asset");
            Assert.IsNotNull(gem);

            inventory.ServerAddItem(gem, 1, false);
            Assert.IsFalse(inventory.ServerCombineFrom(gem), "Nothing to combine it with yet.");
            Assert.AreEqual(1, inventory.CountOf(gem));

            inventory.ServerAddItem(depleted, 1, false);
            Assert.IsTrue(inventory.ServerCombineFrom(gem));
            yield return null;
            Assert.AreEqual(0, inventory.CountOf(gem));
            Assert.AreEqual(0, inventory.CountOf(depleted));
            Assert.AreEqual(1, inventory.CountOf(undercourse));
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
