#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Monsters;
using RPG.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>
    /// Monster AI on a real host: aggro, chasing, threat from damage, evading home, death and respawn,
    /// plus out-of-combat regeneration of players. Needs the prefabs from RPG > Build Test Scene.
    /// The test scene has no navigation, so monsters walk in straight lines.
    /// </summary>
    public class MonsterTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string GoblinPrefabPath = "Assets/_Project/Prefabs/Monsters/Goblin.prefab";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject spawnerObject;
        private NetworkManager network;
        private GameObject goblinPrefab;

        private NetworkObject player;
        private Health playerHealth;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
            Assert.IsNotNull(goblinPrefab, "Run RPG > Build Test Scene first.");

            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = goblinPrefab });
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

        [UnityTest]
        public IEnumerator Monster_AttacksPlayerInAggroRange()
        {
            MonsterAI goblin = SpawnGoblin(new Vector2(0.5f, 0f));
            goblin.AggroRadius = 3f;

            float end = Time.time + 3f;
            while (playerHealth.Current == playerHealth.Max && Time.time < end)
                yield return null;

            Assert.AreEqual(MonsterState.Chase, goblin.State);
            Assert.AreEqual(playerHealth, goblin.CurrentTarget);
            Assert.Less(playerHealth.Current, playerHealth.Max, "The goblin hit the player.");
        }

        [UnityTest]
        public IEnumerator Monster_IgnoresPlayerOutsideAggroRange()
        {
            MonsterAI goblin = SpawnGoblin(new Vector2(6f, 0f));
            goblin.AggroRadius = 2f;

            yield return new WaitForSeconds(1f);

            Assert.AreEqual(MonsterState.Idle, goblin.State);
            Assert.IsNull(goblin.CurrentTarget);
            Assert.AreEqual(playerHealth.Max, playerHealth.Current);
        }

        [UnityTest]
        public IEnumerator Monster_ChasesPlayerIntoRange()
        {
            MonsterAI goblin = SpawnGoblin(new Vector2(2.5f, 0f));
            goblin.AggroRadius = 3f;

            yield return new WaitForSeconds(2f);

            float distance = Vector2.Distance(goblin.transform.position, player.transform.position);
            Assert.Less(distance, 1f, "The goblin walked up to the player.");
        }

        [UnityTest]
        public IEnumerator Monster_TurnsOnWhoeverHitsIt()
        {
            MonsterAI goblin = SpawnGoblin(new Vector2(6f, 0f));
            goblin.AggroRadius = 0f;
            yield return null;

            goblin.GetComponent<Health>().ApplyDamage(5, false, playerHealth);
            yield return null;

            Assert.AreEqual(MonsterState.Chase, goblin.State);
            Assert.AreEqual(playerHealth, goblin.CurrentTarget);
        }

        [UnityTest]
        public IEnumerator Monster_PulledTooFar_WalksHomeInvulnerableAndHeals()
        {
            MonsterAI goblin = SpawnGoblin(new Vector2(3f, 0f));
            goblin.AggroRadius = 0f;
            var goblinHealth = goblin.GetComponent<Health>();
            yield return null;

            goblinHealth.ApplyDamage(10, false, playerHealth);
            Assert.Less(goblinHealth.Current, goblinHealth.Max);

            // Pretend it was pulled far away from home.
            goblin.LeashRadius = 1f;
            Vector2 home = (Vector2)goblin.transform.position + new Vector2(1.5f, 0f);
            goblin.Home = home;
            yield return null;
            yield return null;

            Assert.AreEqual(MonsterState.Evade, goblin.State);
            int beforeHit = goblinHealth.Current;
            goblinHealth.ApplyDamage(10, false, playerHealth);
            Assert.AreEqual(beforeHit, goblinHealth.Current, "Evading monsters ignore damage.");

            float end = Time.time + 3f;
            while (goblin.State == MonsterState.Evade && Time.time < end)
                yield return null;

            Assert.AreEqual(MonsterState.Idle, goblin.State);
            Assert.AreEqual(goblinHealth.Max, goblinHealth.Current, "Back to full health at home.");
            Assert.Less(Vector2.Distance(goblin.transform.position, home), 0.2f);
        }

        [UnityTest]
        public IEnumerator Monster_GivingUpMidChase_StillReachesHomeAndHeals()
        {
            // Chasing: it has just planned a route towards the player when it has to give up.
            MonsterAI goblin = SpawnGoblin(new Vector2(2.5f, 0f));
            goblin.AggroRadius = 3f;
            var goblinHealth = goblin.GetComponent<Health>();
            yield return null;
            goblinHealth.ApplyDamage(10, false, playerHealth);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(MonsterState.Chase, goblin.State);

            Vector2 home = (Vector2)goblin.transform.position + new Vector2(2f, 0f);
            goblin.LeashRadius = 1f;
            goblin.Home = home;

            float end = Time.time + 4f;
            while (goblin.State != MonsterState.Idle && Time.time < end)
                yield return null;

            Assert.AreEqual(MonsterState.Idle, goblin.State, "Reached home instead of stopping on the old route.");
            Assert.Less(Vector2.Distance(goblin.transform.position, home), 0.2f);
            Assert.AreEqual(goblinHealth.Max, goblinHealth.Current);
        }

        [UnityTest]
        public IEnumerator Monster_HittingTheTarget_KeepsTheLeashAtHome()
        {
            MonsterAI goblin = SpawnGoblin(new Vector2(1.6f, 0f));
            goblin.AggroRadius = 3f;
            Vector2 home = goblin.transform.position;

            float end = Time.time + 3f;
            while (playerHealth.Current == playerHealth.Max && Time.time < end)
                yield return null;

            Assert.Less(playerHealth.Current, playerHealth.Max, "The goblin walked up and hit.");
            Assert.AreEqual(MonsterState.Chase, goblin.State);
            Assert.Less(Vector2.Distance(goblin.LeashAnchor, home), 0.01f, "Like the source game: the leash is measured from home.");
        }

        [UnityTest]
        public IEnumerator Monster_GivesUpWhenItCannotHitForAWhile()
        {
            // Far from the player: it chases but cannot land a hit before its patience runs out.
            MonsterAI goblin = SpawnGoblin(new Vector2(6f, 0f));
            goblin.AggroRadius = 0f;
            goblin.ChaseTimeout = 1f;
            var goblinHealth = goblin.GetComponent<Health>();
            yield return null;

            goblinHealth.ApplyDamage(5, false, playerHealth);
            yield return null;
            Assert.AreEqual(MonsterState.Chase, goblin.State);

            float end = Time.time + 2.5f;
            while (goblin.State == MonsterState.Chase && Time.time < end)
                yield return null;

            Assert.AreNotEqual(MonsterState.Chase, goblin.State, "Gave up and went home.");
            Assert.AreEqual(playerHealth.Max, playerHealth.Current);
        }

        [UnityTest]
        public IEnumerator Monster_DiesAndIsRemoved()
        {
            MonsterAI goblin = SpawnGoblin(new Vector2(5f, 0f));
            goblin.CorpseSeconds = 0.2f;
            GameObject goblinObject = goblin.gameObject;
            yield return null;

            goblin.GetComponent<Health>().ApplyDamage(1000, false, playerHealth);
            Assert.AreEqual(MonsterState.Dead, goblin.State);

            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(goblinObject == null, "The corpse was despawned.");
        }

        [UnityTest]
        public IEnumerator Spawner_ReplacesDeadMonster()
        {
            spawnerObject = new GameObject("Test Spawner");
            spawnerObject.transform.position = player.transform.position + new Vector3(6f, 0f, 0f);
            var spawner = spawnerObject.AddComponent<MonsterSpawner>();
            spawner.Configure(goblinPrefab, 1, 0f, 0.2f);
            yield return null;
            yield return null;

            Assert.AreEqual(1, spawner.Alive.Count);
            NetworkObject first = spawner.Alive[0];
            first.GetComponent<MonsterAI>().CorpseSeconds = 0.1f;
            first.GetComponent<Health>().ApplyDamage(1000, false, playerHealth);

            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(1, spawner.Alive.Count);
            Assert.AreNotEqual(first, spawner.Alive[0], "A new monster took its place.");
            Assert.IsTrue(spawner.Alive[0].IsSpawned);
        }

        [UnityTest]
        public IEnumerator Spawner_WithActivationRange_OnlyWhilePlayerNear()
        {
            spawnerObject = new GameObject("Test Spawner");
            spawnerObject.transform.position = player.transform.position + new Vector3(60f, 0f, 0f);
            var spawner = spawnerObject.AddComponent<MonsterSpawner>();
            spawner.Configure(goblinPrefab, 2, 0f, 0.2f);
            spawner.ActivationRange = 20f;
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0, spawner.Alive.Count, "No player near: nothing spawned.");

            spawnerObject.transform.position = player.transform.position + new Vector3(10f, 0f, 0f);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(2, spawner.Alive.Count, "A player came near.");

            player.GetComponent<PlayerNetworkMovement>().ServerTeleport((Vector2)player.transform.position - new Vector2(50f, 0f));
            yield return new WaitForSeconds(7f);
            Assert.AreEqual(0, spawner.Alive.Count, "Every player is far: the idle monsters went away.");
        }

        [UnityTest]
        public IEnumerator Player_RegeneratesOutOfCombat()
        {
            var regeneration = player.GetComponent<HealthRegeneration>();
            Assert.IsNotNull(regeneration);
            regeneration.DelayAfterDamage = 0.2f;

            playerHealth.ApplyDamage(30, false);
            int hurt = playerHealth.Current;
            yield return new WaitForSeconds(1f);

            Assert.Greater(playerHealth.Current, hurt);
        }

        private MonsterAI SpawnGoblin(Vector2 offset)
        {
            Vector3 position = player.transform.position + (Vector3)offset;
            GameObject goblin = Object.Instantiate(goblinPrefab, position, Quaternion.identity);
            goblin.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            return goblin.GetComponent<MonsterAI>();
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
