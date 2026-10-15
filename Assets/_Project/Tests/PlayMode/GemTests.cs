#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Items;
using RPG.Networking;
using RPG.Progression;
using RPG.Vendors;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Sockets, setting gems and combining them at the jeweler, on a real host.</summary>
    public class GemTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string JewelerPath = "Assets/_Project/Data/Vendors/JewelerVendor.asset";
        private const string ItemsFolder = "Assets/_Project/Data/Items";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject vendorObject;
        private NetworkManager network;
        private NetworkObject player;
        private Inventory inventory;
        private Health playerHealth;
        private Vendor jeweler;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            var definition = AssetDatabase.LoadAssetAtPath<VendorDefinition>(JewelerPath);
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
            playerHealth = player.GetComponent<Health>();

            vendorObject = new GameObject("Test Jeweler");
            vendorObject.transform.position = player.transform.position + new Vector3(0.6f, 0f, 0f);
            jeweler = vendorObject.AddComponent<Vendor>();
            jeweler.Configure(definition);
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
        public IEnumerator Gem_SetIntoAnEmptySocket_AddsItsAttributes()
        {
            ItemDefinition sword = Item("IronSword");
            ItemDefinition emerald = Item("ChippedEmerald");
            inventory.ServerAddItem(sword, 1, false, 0, ItemStack.EmptySocket);
            yield return null;
            Assert.IsTrue(inventory.ServerEquip(FindInBag(sword)));
            Assert.AreEqual(ItemStack.EmptySocket, inventory.GetEquipped(ItemSlot.Weapon).Socket);
            yield return null;

            int healthBefore = playerHealth.Max;
            inventory.ServerAddItem(emerald, 1, false);
            Assert.IsTrue(inventory.ServerSocketGem(FindInBag(emerald), true, (int)ItemSlot.Weapon - 1));
            yield return null;

            Assert.AreEqual(emerald, inventory.Gem(inventory.GetEquipped(ItemSlot.Weapon)));
            Assert.AreEqual(0, inventory.CountOf(emerald), "The gem left the bag.");
            Assert.AreEqual(healthBefore + AttributeRules.HealthBonus(emerald.attributes), playerHealth.Max);
        }

        [UnityTest]
        public IEnumerator Gem_IntoAnItemWithoutAnEmptySocket_StaysInTheBag()
        {
            ItemDefinition ruby = Item("ChippedRuby");
            ItemDefinition helm = Item("IronHelm");
            inventory.ServerAddItem(helm, 1, false);
            inventory.ServerAddItem(ruby, 1, false);
            yield return null;

            Assert.IsFalse(inventory.ServerSocketGem(FindInBag(ruby), false, FindInBag(helm)), "The helm has no socket.");
            Assert.IsFalse(inventory.ServerSocketGem(FindInBag(ruby), true, (int)ItemSlot.Weapon - 1), "Nothing is worn there.");
            Assert.AreEqual(1, inventory.CountOf(ruby));
        }

        [UnityTest]
        public IEnumerator Gem_IntoTheChosenBagItem()
        {
            ItemDefinition topaz = Item("ChippedTopaz");
            ItemDefinition boots = Item("IronBoots");
            inventory.ServerAddItem(boots, 1, false, 0, ItemStack.EmptySocket);
            inventory.ServerAddItem(topaz, 1, false);
            yield return null;

            int bootsIndex = FindInBag(boots);
            Assert.IsTrue(inventory.ServerSocketGem(FindInBag(topaz), false, bootsIndex));
            Assert.AreEqual(topaz, inventory.Gem(inventory.GetBagSlot(bootsIndex)));
            Assert.AreEqual(0, inventory.CountOf(topaz));
        }

        [UnityTest]
        public IEnumerator Jeweler_CombinesThreeGemsIntoABetterOne()
        {
            ItemDefinition chipped = Item("ChippedRuby");
            Assert.IsNotNull(chipped.combinesInto);
            inventory.ServerAddItem(chipped, 7, false);
            yield return null;

            Assert.AreEqual(2, inventory.ServerCombineGems(jeweler));
            Assert.AreEqual(1, inventory.CountOf(chipped));
            Assert.AreEqual(2, inventory.CountOf(chipped.combinesInto));
        }

        [UnityTest]
        public IEnumerator SocketedItem_SellsForMore()
        {
            ItemDefinition sword = Item("IronSword");
            ItemDefinition ruby = Item("Ruby");
            int id = inventory.Catalog.IdOf(ruby);
            var plain = new ItemStack(inventory.Catalog.IdOf(sword), 1);
            var socketed = new ItemStack(inventory.Catalog.IdOf(sword), 1, 0, id);
            yield return null;

            Assert.AreEqual(inventory.SellPrice(plain) + ruby.SellPrice, inventory.SellPrice(socketed));
            StringAssert.Contains("Ruby", inventory.SocketText(socketed));
            Assert.IsNull(inventory.SocketText(plain));
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
