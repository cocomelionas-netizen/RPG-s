#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Accounts;
using RPG.Combat;
using RPG.Items;
using RPG.Networking;
using RPG.Progression;
using RPG.World;
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
            Assert.AreNotEqual(hash, PasswordHasher.Hash("secret", PasswordHasher.NewSalt()), "Distinct salts, different hashes.");
        }

        [Test]
        public void AccountService_CreateLogInAndRefusals()
        {
            var go = new GameObject("Accounts");
            try
            {
                var service = go.AddComponent<AccountService>();
                service.UseStore(new MemoryAccountStore());

                Assert.IsNull(service.LogIn(Request("Hero", "secret", true), 1), "Quick start: account and character at once.");
                Assert.IsTrue(service.IsOnline("hero"));
                Assert.IsNotNull(service.Check(Play("Hero", "secret", "Hero")), "Already in the game.");

                service.LogOut(1);
                Assert.IsNull(service.Check(Play("hero", "secret", "HERO")), "Names are not case sensitive.");
                Assert.IsNotNull(service.Check(Play("Hero", "wrong", "Hero")), "Wrong password.");
                Assert.IsNotNull(service.Check(Play("Nobody", "secret", "Nobody")), "No such account.");
                Assert.IsNotNull(service.Check(Play("Hero", "secret", "Nobody")), "No such character.");
                Assert.IsNotNull(service.Handle(Action("Hero", "secret", AccountAction.Register)).error, "Name taken.");
                Assert.IsNotNull(service.Handle(Action("ab", "secret", AccountAction.Register)).error, "Name too short.");
                Assert.IsNotNull(service.Handle(Action("bad name!", "secret", AccountAction.Register)).error, "Invalid characters.");
                Assert.IsNotNull(service.Handle(Action("Other", "123", AccountAction.Register)).error, "Password too short.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Account_HoldsSeveralCharacters_CreatedAndDeleted()
        {
            var go = new GameObject("Accounts");
            try
            {
                var service = go.AddComponent<AccountService>();
                service.UseStore(new MemoryAccountStore());

                AccountReply reply = service.Handle(Action("Player", "secret", AccountAction.Register));
                Assert.IsNull(reply.error);
                Assert.AreEqual(0, reply.characters.Length);
                Assert.IsNotNull(service.Check(Play("Player", "secret", "")), "No character to play yet.");

                LoginRequest create = Action("Player", "secret", AccountAction.CreateCharacter);
                create.character = "Aldric";
                create.classIndex = 2;
                create.female = true;
                reply = service.Handle(create);
                Assert.IsNull(reply.error);
                create.character = "Brakka";
                create.classIndex = 0;
                create.female = false;
                reply = service.Handle(create);
                Assert.AreEqual(2, reply.characters.Length);
                Assert.AreEqual("Aldric", reply.characters[0].name);
                Assert.AreEqual(2, reply.characters[0].classIndex);
                Assert.IsTrue(reply.characters[0].female);
                Assert.AreEqual(1, reply.characters[1].level);
                Assert.IsNotNull(service.Handle(create).error, "Names are unique in an account.");

                var wrong = Action("Player", "wrong", AccountAction.List);
                Assert.IsNotNull(service.Handle(wrong).error, "The list needs the password.");

                Assert.IsNull(service.LogIn(Play("Player", "secret", "brakka"), 7));
                Assert.IsTrue(service.TryGetCharacter(7, out _, out CharacterRecord playing));
                Assert.AreEqual("Brakka", playing.name);
                service.LogOut(7);

                LoginRequest delete = Action("Player", "secret", AccountAction.DeleteCharacter);
                delete.character = "Aldric";
                reply = service.Handle(delete);
                Assert.IsNull(reply.error);
                Assert.AreEqual(1, reply.characters.Length);
                Assert.AreEqual("Brakka", reply.characters[0].name);
                Assert.AreEqual(1, service.Handle(Action("Player", "secret", AccountAction.List)).characters.Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OldAccount_ItsCharacterJoinsTheList()
        {
            var go = new GameObject("Accounts");
            try
            {
                var service = go.AddComponent<AccountService>();
                var memory = new MemoryAccountStore();
                service.UseStore(memory);
                string salt = PasswordHasher.NewSalt();
                memory.Save(new AccountRecord
                {
                    username = "Veteran", salt = salt, passwordHash = PasswordHasher.Hash("secret", salt),
                    character = new CharacterRecord { classIndex = 1, level = 9, hasBeenSaved = true },
                });

                AccountReply reply = service.Handle(Action("Veteran", "secret", AccountAction.List));
                Assert.AreEqual(1, reply.characters.Length);
                Assert.AreEqual("Veteran", reply.characters[0].name);
                Assert.AreEqual(9, reply.characters[0].level);
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
                // Saved before accounts held several characters: it becomes the account's character "Hero".
                character = new CharacterRecord
                {
                    classIndex = 1,
                    level = 3,
                    experience = 10,
                    gold = 77,
                    spentPoints = new AttributeSet(1, 0, 0, 2),
                    experienceToSpend = 40,
                    waystones = 0b101,
                    bag = new[] { new ItemRecord(catalog.IdOf(sword), 1, 3) },
                    hasBeenSaved = true,
                },
            });

            bootstrap.SetLogin(Play("Hero", "secret", "Hero"));
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
            Assert.AreEqual(new AttributeSet(1, 0, 0, 2), progress.SpentPoints);
            Assert.AreEqual(40, progress.ExperienceToSpend);
            Assert.AreEqual(0b101, player.GetComponent<WaystoneTraveller>().DiscoveredMask);

            progress.ServerAddExperience(LevelCurve.ExperienceToNext(3));
            player.GetComponent<CharacterPersistence>().ServerSave();

            Assert.IsTrue(store.TryLoad("hero", out AccountRecord saved));
            CharacterRecord savedHero = saved.Find("Hero");
            Assert.AreEqual(4, savedHero.level);
            Assert.AreEqual(1, savedHero.classIndex);
            Assert.AreEqual(40 + LevelCurve.ExperienceToNext(3), savedHero.experienceToSpend, "Earned experience is also there to spend.");
            Assert.AreEqual(3, savedHero.bag[0].affix, "The affix is saved with the item.");
            Assert.AreEqual(0b101, savedHero.waystones);
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
            var inventory = player.GetComponent<Inventory>();
            Assert.Greater(inventory.BagSnapshot().Length, 0);
            ItemDefinition weapon = inventory.Definition(inventory.GetEquipped(ItemSlot.Weapon));
            Assert.IsNotNull(weapon, "A new character starts with its class's weapon in hand.");
            Assert.AreEqual(WeaponType.Staff, weapon.weaponType);
            Assert.AreEqual(1, weapon.itemLevel);
            Assert.IsTrue(inventory.GetEquipped(ItemSlot.Shield).IsEmpty, "Only the warrior starts with a shield.");
            foreach ((ItemSlot slot, string name) in new[] { (ItemSlot.Chest, "Shirt"), (ItemSlot.Legs, "Pants"), (ItemSlot.Feet, "Shoes"), (ItemSlot.Hands, "Handwraps") })
                Assert.AreEqual(name, inventory.Definition(inventory.GetEquipped(slot))?.displayName, $"A new character wears {name}.");
            Assert.AreEqual(4, inventory.Armor, "The clothes give 1 armor each.");

            Assert.IsTrue(store.TryLoad("Newbie", out AccountRecord saved));
            CharacterRecord newbie = saved.Find("Newbie");
            Assert.IsTrue(newbie.hasBeenSaved);
            Assert.AreEqual(2, newbie.classIndex);
            Assert.IsTrue(System.Array.Exists(newbie.equipment, record => record.id == inventory.Catalog.IdOf(weapon)),
                "The starting weapon is saved with the character.");
        }

        [Test]
        public void EveryClass_HasAStartingWeapon_TheWarriorAShieldToo()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponent<Inventory>();
            for (int classIndex = 0; classIndex < 3; classIndex++)
            {
                Inventory.ClassGear weapon = System.Array.Find(inventory.StartingGear,
                    gear => gear.classIndex == classIndex && gear.item != null && gear.item.slot == ItemSlot.Weapon);
                Assert.IsNotNull(weapon.item, $"Class {classIndex} has no starting weapon.");
                Assert.IsTrue(weapon.item.CanBeUsedByClass(classIndex));
                Assert.AreEqual(1, weapon.item.itemLevel);
            }
            Assert.IsTrue(System.Array.Exists(inventory.StartingGear, gear => gear.classIndex == 0 && gear.item != null && gear.item.slot == ItemSlot.Shield));
            for (int classIndex = 0; classIndex < 3; classIndex++)
                foreach (ItemSlot slot in new[] { ItemSlot.Chest, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Hands })
                {
                    Inventory.ClassGear clothes = System.Array.Find(inventory.StartingGear,
                        gear => gear.classIndex == classIndex && gear.item != null && gear.item.slot == slot);
                    Assert.IsNotNull(clothes.item, $"Class {classIndex} starts without {slot} clothes.");
                    Assert.IsTrue(clothes.item.CanBeUsedByClass(classIndex), "Every class may wear the starting clothes.");
                }
        }

        [UnityTest]
        public IEnumerator WrongPassword_DoesNotStartTheGame()
        {
            yield return StartNetwork();
            string salt = PasswordHasher.NewSalt();
            store.Save(new AccountRecord { username = "Hero", salt = salt, passwordHash = PasswordHasher.Hash("secret", salt) });

            bootstrap.SetLogin(Play("Hero", "wrong", "Hero"));
            Assert.IsFalse(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
            Assert.IsFalse(network.IsListening);
            Assert.IsFalse(string.IsNullOrEmpty(bootstrap.LastDisconnectReason));
        }

        private static LoginRequest Request(string name, string password, bool create)
        {
            return new LoginRequest { username = name, password = password, createCharacter = create };
        }

        private static LoginRequest Play(string name, string password, string character) =>
            new LoginRequest { username = name, password = password, character = character };

        private static LoginRequest Action(string name, string password, AccountAction action) =>
            new LoginRequest { username = name, password = password, action = action };

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
