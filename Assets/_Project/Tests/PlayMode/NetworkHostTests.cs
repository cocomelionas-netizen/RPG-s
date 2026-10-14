#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Networking;
using RPG.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Starts a real host with the real player prefab and checks spawning and replicated movement.</summary>
    public class NetworkHostTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";

        private GameObject cameraObject;
        private GameObject networkObject;
        private NetworkManager network;
        private NetworkBootstrap bootstrap;

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
            bootstrap = networkObject.AddComponent<NetworkBootstrap>();

            // Let Start run; a batch mode test run would auto-start a server, which these tests replace.
            yield return null;
            if (network.IsListening)
                yield return ShutdownNetwork();
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
        public IEnumerator Host_SpawnsOwnedPlayerNearSpawnPoint()
        {
            Assert.IsTrue(bootstrap.StartHost(RandomPort()));
            yield return WaitForLocalPlayer();

            NetworkObject player = network.LocalClient.PlayerObject;
            Assert.IsNotNull(player, "Host should get a player object.");
            Assert.IsTrue(player.IsOwner);
            Assert.IsNotNull(player.GetComponent<PlayerNetworkMovement>());
            Assert.Less(((Vector2)player.transform.position).magnitude, 1f, "Spawned within the spawn radius of the origin.");
        }

        [UnityTest]
        public IEnumerator Host_Movement_UpdatesAuthoritativeState()
        {
            Assert.IsTrue(bootstrap.StartHost(RandomPort()));
            yield return WaitForLocalPlayer();

            NetworkObject player = network.LocalClient.PlayerObject;
            var movement = player.GetComponent<PlayerMovement>();
            var networkMovement = player.GetComponent<PlayerNetworkMovement>();
            Vector2 target = (Vector2)player.transform.position + new Vector2(1.5f, 0f);

            movement.SetDestination(target);
            float end = Time.time + 3f;
            while (Vector2.Distance(networkMovement.AuthoritativePosition, target) > 0.1f && Time.time < end)
                yield return null;

            Assert.Less(Vector2.Distance(networkMovement.AuthoritativePosition, target), 0.1f);
            Assert.AreEqual(0, networkMovement.CorrectionCount, "The host's own movement is never corrected.");
        }

        private IEnumerator WaitForLocalPlayer()
        {
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < end)
                yield return null;
        }

        private IEnumerator ShutdownNetwork()
        {
            if (network == null) yield break;
            if (network.IsListening) network.Shutdown();
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.IsListening || network.ShutdownInProgress) && Time.realtimeSinceStartup < end)
                yield return null;
        }

        private static ushort RandomPort() => (ushort)Random.Range(20000, 40000);
    }
}
#endif
