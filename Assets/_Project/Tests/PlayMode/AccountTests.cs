#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Accounts;
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
    /// <summary>Logins, accounts and saving/loading characters on a real host (accounts kept in memory).</summary>
    public class AccountTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string ItemsFolder = "Assets/_Project/Data/Items";

        private GameObject cameraObject;
        private GameObject networkObject;
        private NetworkManager network;
        private NetworkBootstrap bootstrap;
        private AccountService accounts;
        private MemoryAccountStore store;

        // ---------- Without a network ----------

        [Test]
        public void PasswordHasher_AcceptsOnlyTheRightPassword()
        {
            string salt = PasswordHasher.NewSalt();
            string hash = PasswordHasher.Hash("secret", salt);
            Assert.IsTrue(PasswordHasher.Verify("secret", salt, hash));
            Assert.IsFalse(PasswordHasher.Verify("Secret", salt, hash));
            Assert.AreNotEqual(hash, PasswordHasher.Hash("secret", PasswordHasher.NewSalt()), "Different salts, different hashes.");
        }

        [Test]
        public void AccountService_CreateLogInAndRefusals()
        {
            var go = new GameObject("Accounts");
            try
            {
                var service = go.AddComponent<AccountService>();
                service.UseStore(new MemoryAccountStore());

                Assert.IsNull(service.LogIn(Request("Hero", "secret", true), 1));
                Assert.IsTrue(service.IsOnline("hero"));
                Assert.IsNotNull(service.Check(Request("Hero", "secret", false)), "Already in the game.");

                service.LogOut(1);
                Assert.IsNull(service.Check(Request("hero", "secret", false)), "Names are not case sensitive.");
                Assert.IsNotNull(service.Check(Request("Hero", "wrong", false)), "Wrong password.");
                Assert.IsNotNull(service.Check(Request("Nobody", "secret", false)), "No such account.");
                Assert.IsNotNull(service.Check(Request("Hero", "secret", true)), "Name taken.");
                Assert.IsNotNull(service.Check(Request("ab", "secret", true)), "Name too short.");
                Assert.IsNotNull(service.Check(Request("bad name!", "secret", true)), "Invalid characters.");
                Assert.IsNotNull(service.Check(Request("Other", "123", true)), "Password too short.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- Host ----------

        private IEnumerator StartNetwork()
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
            accounts = networkObject.AddComponent<AccountService>();
            store = new MemoryAccountStore();
            accounts.UseStore(store);
            bootstrap = networkObject.AddComponent<NetworkBootstrap>();

            yield return null;
            if (network.IsListening)
                yield return ShutdownNetwork();
        }

        private IEnumerator WaitForPlayer()
        {
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.IsNotNull(network.LocalClient?.PlayerObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (networkObject == null) yield break;
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            networkObject = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedCharacter_IsRestoredOnLogin_AndSavedAgain()
        {
            yield return StartNetwork();
            var sword = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/IronSword.asset");
            var catalog = network.NetworkConfig.PlayerPrefab.GetComponent<Inventory>().Catalog;
            string salt = PasswordHasher.NewSalt();
            store.Save(new AccountRecord
            {
                username = "Hero",
                salt = salt,
                passwordHash = PasswordHasher.Hash("secret", salt),
                character = new CharacterRecord
                {
                    classIndex = 1,
                    level = 3,
                    experience = 10,
                    gold = 77,
                    bag = new[] { new ItemRecord(catalog.IdOf(sword), 1) },
                    hasBeenSaved = true,
                },
            });

            bootstrap.SetLogin(Request("Hero", "secret", false));
            Assert.IsTrue(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
            yield return WaitForPlayer();

            NetworkObject player = network.LocalClient.PlayerObject;
            var progress = player.GetComponent<CharacterProgress>();
            Assert.AreEqual(3, progress.Level);
            Assert.AreEqual(10, progress.Experience);
            Assert.AreEqual("Ranger", progress.ClassName);
            Assert.AreEqual("Hero", player.GetComponent<Health>().DisplayName);
            Assert.AreEqual(1, player.GetComponent<Inventory>().CountOf(sword));
            Assert.AreEqual(77, player.GetComponent<Inventory>().Gold);

            progress.ServerAddExperience(LevelCurve.ExperienceToNext(3));
            player.GetComponent<CharacterPersistence>().ServerSave();

            Assert.IsTrue(store.TryLoad("hero", out AccountRecord saved));
            Assert.AreEqual(4, saved.character.level);
            Assert.AreEqual(1, saved.character.classIndex);
        }

        [UnityTest]
        public IEnumerator NewCharacter_GetsChosenClassAndIsSaved()
        {
            yield return StartNetwork();
            bootstrap.SetLogin(new LoginRequest { username = "Newbie", password = "secret", createCharacter = true, classIndex = 2 });
            Assert.IsTrue(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
            yield return WaitForPlayer();

            NetworkObject player = network.LocalClient.PlayerObject;
            Assert.AreEqual("Mage", player.GetComponent<CharacterProgress>().ClassName);
            Assert.Greater(player.GetComponent<Inventory>().BagSnapshot().Length, 0);

            Assert.IsTrue(store.TryLoad("Newbie", out AccountRecord saved));
            Assert.IsTrue(saved.character.hasBeenSaved);
            Assert.AreEqual(2, saved.character.classIndex);
        }

        [UnityTest]
        public IEnumerator WrongPassword_DoesNotStartTheGame()
        {
            yield return StartNetwork();
            string salt = PasswordHasher.NewSalt();
            store.Save(new AccountRecord { username = "Hero", salt = salt, passwordHash = PasswordHasher.Hash("secret", salt) });

            bootstrap.SetLogin(Request("Hero", "wrong", false));
            Assert.IsFalse(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
            Assert.IsFalse(network.IsListening);
            Assert.IsFalse(string.IsNullOrEmpty(bootstrap.LastDisconnectReason));
        }

        private static LoginRequest Request(string name, string password, bool create)
        {
            return new LoginRequest { username = name, password = password, createCharacter = create };
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
