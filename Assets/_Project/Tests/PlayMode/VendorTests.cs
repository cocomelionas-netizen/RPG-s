#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Items;
using RPG.Monsters;
using RPG.Networking;
using RPG.Vendors;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Gold, buying and selling at vendors, and class-only gear, on a real host.</summary>
    public class VendorTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string GoblinPrefabPath = "Assets/_Project/Prefabs/Monsters/Goblin.prefab";
        private const string WarriorVendorPath = "Assets/_Project/Data/Vendors/WarriorVendor.asset";
        private const string ItemsFolder = "Assets/_Project/Data/Items";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject vendorObject;
        private NetworkManager network;
        private GameObject goblinPrefab;

        private NetworkObject player;
        private Inventory inventory;
        private Vendor vendor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
            var definition = AssetDatabase.LoadAssetAtPath<VendorDefinition>(WarriorVendorPath);
            Assert.IsNotNull(definition, "Run RPG > Build Test Scene first.");

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
            inventory = player.GetComponent<Inventory>();

            vendorObject = new GameObject("Test Vendor");
            vendorObject.transform.position = player.transform.position + new Vector3(0.6f, 0f, 0f);
            vendor = vendorObject.AddComponent<Vendor>();
            vendor.Configure(definition);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(vendorObject);
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Buying_CostsThePrice_AndPutsTheItemInTheBag()
        {
            inventory.ServerAddGold(500, false);
            int goldBefore = inventory.Gold;
            ItemDefinition item = vendor.Definition.stock[0];

            Assert.IsTrue(inventory.ServerBuy(vendor, 0));
            yield return null;
            Assert.AreEqual(goldBefore - item.price, inventory.Gold);
            Assert.AreEqual(1, inventory.CountOf(item));
        }

        [UnityTest]
        public IEnumerator Buying_WithoutEnoughGold_Fails()
        {
            int expensive = -1;
            for (int i = 0; i < vendor.Definition.stock.Length; i++)
                if (vendor.Definition.stock[i].price > inventory.Gold) expensive = i;
            Assert.GreaterOrEqual(expensive, 0);
            int goldBefore = inventory.Gold;

            Assert.IsFalse(inventory.ServerBuy(vendor, expensive));
            yield return null;
            Assert.AreEqual(goldBefore, inventory.Gold);
            Assert.AreEqual(0, inventory.CountOf(vendor.Definition.stock[expensive]));
        }

        [UnityTest]
        public IEnumerator Selling_GivesAQuarterOfThePrice()
        {
            ItemDefinition sword = Item("IronSword");
            inventory.ServerAddItem(sword, 1, false);
            int goldBefore = inventory.Gold;

            Assert.IsTrue(inventory.ServerSell(vendor, FindInBag(sword)));
            yield return null;
            Assert.AreEqual(goldBefore + sword.SellPrice, inventory.Gold);
            Assert.AreEqual(sword.price / 4, sword.SellPrice);
            Assert.AreEqual(0, inventory.CountOf(sword));
        }

        [UnityTest]
        public IEnumerator LimitedItem_SellsOut()
        {
            int limited = -1;
            for (int i = 0; i < vendor.Definition.stock.Length; i++)
                if (vendor.Definition.LimitOf(i) == 1) limited = i;
            Assert.GreaterOrEqual(limited, 0, "The warrior vendor has a one-of-a-kind item.");
            inventory.ServerAddGold(5000, false);
            ItemDefinition item = vendor.Definition.stock[limited];

            Assert.IsTrue(inventory.ServerBuy(vendor, limited));
            int goldAfterFirst = inventory.Gold;
            Assert.AreEqual(0, vendor.ServerRemaining(limited));
            Assert.IsFalse(inventory.ServerBuy(vendor, limited), "Sold out.");
            yield return null;

            Assert.AreEqual(goldAfterFirst, inventory.Gold);
            Assert.AreEqual(1, inventory.CountOf(item));
            Assert.AreEqual(Vendor.Unlimited, vendor.ServerRemaining(vendor.Definition.stock.Length - 1), "Potions never run out.");
        }

        [UnityTest]
        public IEnumerator Trading_FarFromTheVendor_Fails()
        {
            vendorObject.transform.position = player.transform.position + new Vector3(6f, 0f, 0f);
            inventory.ServerAddGold(500, false);
            yield return null;

            Assert.IsFalse(inventory.ServerBuy(vendor, 0));
            Assert.IsFalse(inventory.ServerSell(vendor, FindInBag(Item("HealingPotion"))));
        }

        [UnityTest]
        public IEnumerator ClassGear_OnlyFitsItsClass()
        {
            // New characters are warriors.
            ItemDefinition bow = Item("HuntingBow");
            ItemDefinition sword = Item("MilitiaSword");
            inventory.ServerAddItem(bow, 1, false);
            inventory.ServerAddItem(sword, 1, false);
            yield return null;

            Assert.IsFalse(inventory.ServerEquip(FindInBag(bow)), "A warrior cannot use a ranger's bow.");
            Assert.IsTrue(inventory.ServerEquip(FindInBag(sword)));
            Assert.AreEqual(sword, inventory.Definition(inventory.GetEquipped(ItemSlot.Weapon)));
        }

        [UnityTest]
        public IEnumerator KillingAMonster_GivesGold()
        {
            int goldBefore = inventory.Gold;
            GameObject goblin = Object.Instantiate(goblinPrefab, player.transform.position + new Vector3(4f, 0f, 0f), Quaternion.identity);
            var ai = goblin.GetComponent<MonsterAI>();
            ai.GoldReward = new Vector2Int(5, 5);
            goblin.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            yield return null;

            goblin.GetComponent<Health>().ApplyDamage(10000, false, player.GetComponent<Health>());
            yield return null;
            Assert.AreEqual(goldBefore + 5, inventory.Gold);
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
