#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Networking;
using RPG.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Combat on a real host: server-side auto-attack, death and respawn.</summary>
    public class CombatTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string DummyPrefabPath = "Assets/_Project/Prefabs/World/TrainingDummy.prefab";

        private GameObject cameraObject;
        private GameObject networkObject;
        private NetworkManager network;
        private NetworkBootstrap bootstrap;
        private GameObject dummyPrefab;

        private NetworkObject player;
        private AutoAttack autoAttack;
        private Health playerHealth;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = dummyPrefab });
            bootstrap = networkObject.AddComponent<NetworkBootstrap>();

            yield return null;
            if (network.IsListening)
                yield return ShutdownNetwork();

            Assert.IsTrue(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < end)
                yield return null;

            player = network.LocalClient.PlayerObject;
            Assert.IsNotNull(player);
            autoAttack = player.GetComponent<AutoAttack>();
            playerHealth = player.GetComponent<Health>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_HasFullHealthAndName()
        {
            yield return null;
            Assert.AreEqual(100, playerHealth.Max);
            Assert.AreEqual(100, playerHealth.Current);
            Assert.AreEqual(Faction.Player, playerHealth.Faction);
            StringAssert.StartsWith("Player ", playerHealth.DisplayName);
        }

        [UnityTest]
        public IEnumerator AutoAttack_DamagesHostileTargetInRange_OncePerInterval()
        {
            Health dummy = SpawnDummy(new Vector2(0.5f, 0f));
            yield return null;

            Assert.IsTrue(autoAttack.ServerStartAttack(dummy));
            yield return new WaitForSeconds(0.2f);

            int afterFirstHit = dummy.Current;
            Assert.Less(afterFirstHit, dummy.Max, "The first hit should land immediately.");

            yield return new WaitForSeconds(autoAttack.AttackInterval * 0.5f);
            Assert.AreEqual(afterFirstHit, dummy.Current, "No second hit before the attack interval.");

            yield return new WaitForSeconds(autoAttack.AttackInterval * 0.7f);
            Assert.Less(dummy.Current, afterFirstHit, "Second hit after the interval.");
        }

        [UnityTest]
        public IEnumerator AutoAttack_OutOfRange_DoesNoDamage()
        {
            Health dummy = SpawnDummy(new Vector2(5f, 0f));
            yield return null;

            Assert.IsTrue(autoAttack.ServerStartAttack(dummy));
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(dummy.Max, dummy.Current);
        }

        [UnityTest]
        public IEnumerator AutoAttack_RefusesFriendlyTarget()
        {
            Health friendly = SpawnDummy(new Vector2(0.5f, 0f), Faction.Player);
            yield return null;

            Assert.IsFalse(autoAttack.ServerStartAttack(friendly));
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(friendly.Max, friendly.Current);
        }

        [UnityTest]
        public IEnumerator AutoAttack_StopsWhenTargetDies()
        {
            Health dummy = SpawnDummy(new Vector2(0.5f, 0f));
            yield return null;

            Assert.IsTrue(autoAttack.ServerStartAttack(dummy));
            dummy.ApplyDamage(10000, false);
            yield return null;
            yield return null;

            Assert.IsTrue(dummy.IsDead);
            Assert.IsNull(autoAttack.ServerTarget);
        }

        [UnityTest]
        public IEnumerator Dummy_ComesBackAfterDying()
        {
            Health dummy = SpawnDummy(new Vector2(3f, 0f), respawnDelay: 0.3f);
            yield return null;

            dummy.ApplyDamage(10000, false);
            Assert.IsTrue(dummy.IsDead);

            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(dummy.IsDead);
            Assert.AreEqual(dummy.Max, dummy.Current);
        }

        [UnityTest]
        public IEnumerator Player_DiesCannotMove_ThenRespawnsAtSpawn()
        {
            var respawn = player.GetComponent<PlayerRespawn>();
            var movement = player.GetComponent<PlayerMovement>();
            respawn.RespawnDelay = 0.5f;

            // Walk away from the spawn point first.
            player.GetComponent<PlayerMovement>().Teleport(new Vector2(3f, 0f));
            yield return new WaitForSeconds(0.2f);

            playerHealth.ApplyDamage(10000, false);
            yield return null;
            yield return null;
            Assert.IsTrue(playerHealth.IsDead);
            Assert.IsFalse(movement.enabled, "Dead players cannot move.");

            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(playerHealth.IsDead);
            Assert.AreEqual(playerHealth.Max, playerHealth.Current);
            Assert.Less(((Vector2)player.transform.position).magnitude, 1f, "Respawned at the spawn point (origin in tests).");

            yield return null;
            Assert.IsTrue(movement.enabled);
        }

        private Health SpawnDummy(Vector2 offset, Faction faction = Faction.Monster, float respawnDelay = 4f)
        {
            Vector3 position = player.transform.position + (Vector3)offset;
            GameObject dummy = Object.Instantiate(dummyPrefab, position, Quaternion.identity);

            var healthObject = new SerializedObject(dummy.GetComponent<Health>());
            healthObject.FindProperty("faction").enumValueIndex = (int)faction;
            healthObject.ApplyModifiedPropertiesWithoutUndo();
            dummy.GetComponent<TrainingDummy>().RespawnDelay = respawnDelay;

            dummy.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            return dummy.GetComponent<Health>();
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
