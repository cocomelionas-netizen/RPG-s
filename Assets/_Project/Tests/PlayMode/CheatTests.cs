#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Items;
using RPG.Networking;
using RPG.Player;
using RPG.Progression;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>The F9 cheats on the host's own character.</summary>
    public class CheatTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";

        [UnityTest]
        public IEnumerator HostCheats_GiveGoldLevelsAndGodMode()
        {
            var cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;
            var networkObject = new GameObject("Test NetworkManager");
            var network = networkObject.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkObject.AddComponent<UnityTransport>(),
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            var bootstrap = networkObject.AddComponent<NetworkBootstrap>();
            try
            {
                yield return null;
                Assert.IsTrue(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
                float end = Time.realtimeSinceStartup + 5f;
                while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < end)
                    yield return null;
                NetworkObject player = network.LocalClient.PlayerObject;
                var cheats = player.GetComponent<Cheats>();
                Assert.IsNotNull(cheats, "Run RPG > Build Test Scene first.");
                Assert.IsTrue(cheats.Allowed, "The host's own character may cheat");

                var inventory = player.GetComponent<Inventory>();
                int gold = inventory.Gold;
                Assert.IsTrue(cheats.ServerCheat(CheatCommand.Gold));
                Assert.AreEqual(gold + Cheats.GoldGift, inventory.Gold);

                var progress = player.GetComponent<CharacterProgress>();
                int level = progress.Level;
                Assert.IsTrue(cheats.ServerCheat(CheatCommand.LevelUp));
                Assert.AreEqual(level + 1, progress.Level);
                Assert.IsTrue(cheats.ServerCheat(CheatCommand.MaxLevel));
                Assert.AreEqual(LevelCurve.MaxLevel, progress.Level);

                var health = player.GetComponent<Health>();
                Assert.IsTrue(cheats.ServerCheat(CheatCommand.God));
                int before = health.Current;
                health.ApplyDamage(health.Max, false);
                Assert.AreEqual(before, health.Current, "God mode takes no damage");
                Assert.IsTrue(cheats.ServerCheat(CheatCommand.God));
                Assert.IsFalse(cheats.God);
            }
            finally
            {
                if (network.IsListening) network.Shutdown();
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
