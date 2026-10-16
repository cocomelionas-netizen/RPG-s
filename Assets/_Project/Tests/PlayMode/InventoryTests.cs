#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Items;
using RPG.Monsters;
using RPG.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Bag, equipment, potions and loot on a real host. Needs the assets from RPG > Build Test Scene.</summary>
    public class InventoryTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string GoblinPrefabPath = "Assets/_Project/Prefabs/Monsters/Goblin.prefab";
        private const string LootBagPrefabPath = "Assets/_Project/Prefabs/World/LootBag.prefab";
        private const string ItemsFolder = "Assets/_Project/Data/Items";

        private GameObject cameraObject;
        private GameObject networkObject;
        private NetworkManager network;
        private GameObject goblinPrefab;
        private GameObject lootBagPrefab;

        private NetworkObject player;
        private Health playerHealth;
        private Inventory inventory;
        private AutoAttack autoAttack;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
            lootBagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootBagPrefabPath);
            Assert.IsNotNull(lootBagPrefab, "Run RPG > Build Test Scene first.");

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
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = lootBagPrefab });
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
            autoAttack = player.GetComponent<AutoAttack>();
            yield return null;
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
        public IEnumerator NewCharacter_HasStartingPotions()
        {
            yield return null;
            Assert.AreEqual(3, inventory.CountOf(Item("HealingPotion")));
        }

        [UnityTest]
        public IEnumerator Gear_AboveYourLevel_CannotBeWorn()
        {
            ItemDefinition sword = Item("IronSword");
            Assert.Greater(sword.itemLevel, 1);
            inventory.ServerAddItem(sword, 1, false);
            yield return null;

            Assert.IsFalse(inventory.ServerEquip(FindInBag(sword)), "A level 1 character cannot wear it yet.");
            SetLevel(sword.itemLevel);
            yield return null;
            Assert.IsTrue(inventory.ServerEquip(FindInBag(sword)));
        }

        [UnityTest]
        public IEnumerator EquippingWeapon_AddsDamage_AndTakingItOffRemovesIt()
        {
            SetLevel(10);
            yield return null;
            int minBefore = autoAttack.MinDamage;
            ItemDefinition sword = Item("IronSword");
            Assert.AreEqual(1, inventory.ServerAddItem(sword, 1, false));
            int index = FindInBag(sword);

            Assert.IsTrue(inventory.ServerEquip(index));
            yield return null;
            Assert.AreEqual(sword, inventory.Definition(inventory.GetEquipped(ItemSlot.Weapon)));
            Assert.IsTrue(inventory.GetBagSlot(index).IsEmpty);
            Assert.AreEqual(minBefore + sword.damageBonus, autoAttack.MinDamage);

            Assert.IsTrue(inventory.ServerUnequip(ItemSlot.Weapon));
            yield return null;
            Assert.IsTrue(inventory.GetEquipped(ItemSlot.Weapon).IsEmpty);
            Assert.AreEqual(1, inventory.CountOf(sword));
            Assert.AreEqual(minBefore, autoAttack.MinDamage);
        }

        [UnityTest]
        public IEnumerator Armor_RaisesHealthAndReducesDamage()
        {
            SetLevel(10);
            yield return null;
            int maxBefore = playerHealth.Max;
            ItemDefinition chainmail = Item("Chainmail");
            inventory.ServerAddItem(chainmail, 1, false);
            Assert.IsTrue(inventory.ServerEquip(FindInBag(chainmail)));
            yield return null;

            Assert.AreEqual(maxBefore + chainmail.healthBonus, playerHealth.Max);
            Assert.AreEqual(chainmail.armor, playerHealth.ServerArmor);

            int before = playerHealth.Current;
            playerHealth.ApplyDamage(100, false);
            Assert.AreEqual(before - Health.ReduceByArmor(100, chainmail.armor), playerHealth.Current);
            Assert.Less(Health.ReduceByArmor(100, chainmail.armor), 100);
        }

        [UnityTest]
        public IEnumerator Potion_HealsIsUsedUp_AndHasACooldown()
        {
            ItemDefinition potion = Item("HealingPotion");
            int index = FindInBag(potion);
            playerHealth.ApplyDamage(60, false);
            int hurt = playerHealth.Current;
            yield return null;

            Assert.IsTrue(inventory.ServerUse(index));
            Assert.Greater(playerHealth.Current, hurt);
            Assert.AreEqual(2, inventory.CountOf(potion));

            Assert.IsFalse(inventory.ServerUse(index), "Potion cooldown.");
            Assert.AreEqual(2, inventory.CountOf(potion));
        }

        [UnityTest]
        public IEnumerator FullBag_RefusesMoreItems()
        {
            ItemDefinition sword = Item("RustySword");
            int free = 0;
            for (int i = 0; i < inventory.BagSize; i++)
                if (inventory.GetBagSlot(i).IsEmpty) free++;

            Assert.AreEqual(free, inventory.ServerAddItem(sword, free, false));
            Assert.AreEqual(0, inventory.ServerAddItem(sword, 1, false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator KilledMonster_DropsLoot_ThatCanBePickedUpNearby()
        {
            ItemDefinition helm = Item("IronHelm");
            GameObject goblin = Object.Instantiate(goblinPrefab, player.transform.position + new Vector3(0.6f, 0f, 0f), Quaternion.identity);
            var ai = goblin.GetComponent<MonsterAI>();
            ai.ConfigureLoot(lootBagPrefab, new[] { new LootEntry(helm, 1f) });
            ai.AggroRadius = 0f;
            goblin.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            yield return null;

            goblin.GetComponent<Health>().ApplyDamage(1000, false, playerHealth);
            yield return null;

            LootBag loot = Object.FindAnyObjectByType<LootBag>();
            Assert.IsNotNull(loot, "The kill left a loot bag.");
            Assert.AreEqual(1, loot.Contents.Count);

            Assert.IsTrue(inventory.ServerPickUp(loot));
            Assert.AreEqual(1, inventory.CountOf(helm));
            yield return null;
            Assert.IsTrue(loot == null || !loot.IsSpawned, "An emptied bag disappears.");
        }

        [UnityTest]
        public IEnumerator Loot_TooFarAway_CannotBePickedUp()
        {
            GameObject bag = Object.Instantiate(lootBagPrefab, player.transform.position + new Vector3(5f, 0f, 0f), Quaternion.identity);
            var loot = bag.GetComponent<LootBag>();
            loot.ServerSetContents(new[] { Item("IronBoots") });
            bag.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            yield return null;

            Assert.IsFalse(inventory.ServerPickUp(loot));
            Assert.AreEqual(0, inventory.CountOf(Item("IronBoots")));
        }

        [UnityTest]
        public IEnumerator LootWindow_TakesOneItemAtATime()
        {
            GameObject bag = Object.Instantiate(lootBagPrefab, player.transform.position, Quaternion.identity);
            var loot = bag.GetComponent<LootBag>();
            loot.ServerSetContents(new[] { Item("IronBoots"), Item("GoblinEar") });
            bag.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            yield return null;
            Assert.AreSame(loot, LootBag.Find(loot.NetworkObjectId));

            int earId = inventory.Catalog.IdOf(Item("GoblinEar"));
            Assert.IsFalse(loot.ServerGiveOne(inventory, 0, earId), "Slot 0 holds the boots, not the ear.");
            Assert.IsTrue(loot.ServerGiveOne(inventory, 1, earId));
            Assert.AreEqual(1, inventory.CountOf(Item("GoblinEar")));
            Assert.AreEqual(0, inventory.CountOf(Item("IronBoots")), "Only the clicked item is taken.");
            Assert.AreEqual(1, loot.Contents.Count);

            Assert.IsTrue(loot.ServerGiveOne(inventory, 0, inventory.Catalog.IdOf(Item("IronBoots"))));
            yield return null;
            Assert.IsFalse(loot.IsSpawned, "An emptied bag disappears.");
        }

        /// <summary>Makes the player a warrior of <paramref name="level"/>, high enough to wear the gear a test needs.</summary>
        private void SetLevel(int level)
        {
            player.GetComponent<RPG.Progression.CharacterProgress>().ServerLoad(0, level, 0, default, 0);
        }

        [Test]
        public void Equipment_HasTheSourceGamesSlots()
        {
            Assert.AreEqual(11, Inventory.EquipmentSlots);
            Assert.AreEqual(ItemSlot.Weapon, Inventory.SlotAt(0), "The first four keep their places (saves).");
            Assert.AreEqual(ItemSlot.Feet, Inventory.SlotAt(3));
            Assert.AreEqual(ItemSlot.Ring, Inventory.SlotAt(8));
            Assert.AreEqual(ItemSlot.Ring, Inventory.SlotAt(9));
            Assert.AreEqual(ItemSlot.Shield, Inventory.SlotAt(10));
        }

        [UnityTest]
        public IEnumerator TwoRings_GoOnBothHands_AndLegsHaveArmor()
        {
            ItemDefinition ring = Item("Generated/Ring_L01");
            ItemDefinition legs = Item("Generated/LeatherTrousers_L01");
            Assert.AreEqual(ItemSlot.Ring, ring.slot);
            Assert.IsTrue(ring.alwaysAffix, "Rings are worth their affix.");
            Assert.AreEqual(ItemSlot.Legs, legs.slot);
            Assert.Greater(legs.armor, 0);
            inventory.ServerAddItem(ring, 2, false);
            inventory.ServerAddItem(legs, 1, false);
            yield return null;

            Assert.IsTrue(inventory.ServerEquip(FindInBag(ring)));
            Assert.IsTrue(inventory.ServerEquip(FindInBag(ring)));
            Assert.IsTrue(inventory.ServerEquip(FindInBag(legs)));
            yield return null;
            Assert.AreEqual(ring, inventory.Definition(inventory.GetEquippedAt(8)));
            Assert.AreEqual(ring, inventory.Definition(inventory.GetEquippedAt(9)));
            Assert.AreEqual(legs, inventory.Definition(inventory.GetEquipped(ItemSlot.Legs)));
            Assert.AreEqual(legs.armor, inventory.Armor);

            Assert.IsTrue(inventory.ServerUnequipAt(9));
            Assert.AreEqual(1, inventory.CountOf(ring));
        }

        [UnityTest]
        public IEnumerator Shield_IsForTheWarrior()
        {
            ItemDefinition shield = Item("Generated/Barricade_L01");
            Assert.AreEqual(ItemSlot.Shield, shield.slot);
            Assert.Greater(shield.armor, 0);
            inventory.ServerAddItem(shield, 1, false);
            yield return null;
            // New characters are warriors.
            Assert.IsTrue(inventory.ServerEquip(FindInBag(shield)));
            Assert.AreEqual(shield, inventory.Definition(inventory.GetEquipped(ItemSlot.Shield)));
        }

        [Test]
        public void Sockets_FollowTheQuality_LikeTheSourceGame()
        {
            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(0, LootDrop.RollSockets(ItemRarity.Common));
                Assert.That(LootDrop.RollSockets(ItemRarity.Uncommon), Is.InRange(0, 1));
                Assert.That(LootDrop.RollSockets(ItemRarity.Rare), Is.InRange(0, 2));
                Assert.That(LootDrop.RollSockets(ItemRarity.Epic), Is.InRange(1, 3));
                Assert.That(LootDrop.RollSockets(ItemRarity.Legendary), Is.InRange(2, 4));
            }
        }

        [UnityTest]
        public IEnumerator Gems_FillEverySocket_AndAllCount()
        {
            ItemDefinition legs = Item("Generated/LeatherTrousers_L01");
            ItemDefinition ruby = Item("Ruby");
            Assert.AreEqual(1, inventory.ServerAddItemWithSockets(legs, 1, false, 0, 3));
            inventory.ServerAddItem(ruby, 3, false);
            yield return null;
            int legsIndex = FindInBag(legs);
            Assert.AreEqual(3, inventory.GetBagSlot(legsIndex).SocketCount);
            for (int i = 0; i < 3; i++) Assert.IsTrue(inventory.ServerSocketGem(FindInBag(ruby), false, legsIndex));
            Assert.IsFalse(inventory.ServerSocketGem(FindInBag(Item("HealingPotion")), false, legsIndex), "A potion is no gem.");
            ItemStack socketed = inventory.GetBagSlot(legsIndex);
            Assert.AreEqual(-1, socketed.FirstEmptySocket, "All three are full.");
            Assert.AreEqual(legs.SellPrice + 3 * ruby.SellPrice, inventory.SellPrice(socketed));

            Assert.IsTrue(inventory.ServerEquip(legsIndex));
            yield return null;
            Assert.AreEqual(3 * ruby.attributes.strength, inventory.Attributes.strength);
        }

        private static ItemDefinition Item(string fileName)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{fileName}.asset");
            Assert.IsNotNull(item, fileName);
            return item;
        }

        private int FindInBag(ItemDefinition item)
        {
            for (int i = 0; i < inventory.BagSize; i++)
                if (inventory.Definition(inventory.GetBagSlot(i)) == item) return i;
            Assert.Fail($"{item.displayName} is not in the bag.");
            return -1;
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
