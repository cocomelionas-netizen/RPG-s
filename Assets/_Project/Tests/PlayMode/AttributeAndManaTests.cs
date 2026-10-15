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
    /// <summary>Mana and ability costs, attribute points, random affixes and junk loot, on a real host.</summary>
    public class AttributeAndManaTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string WarriorVendorPath = "Assets/_Project/Data/Vendors/WarriorVendor.asset";
        private const string ItemsFolder = "Assets/_Project/Data/Items";
        private const string AffixCatalogPath = "Assets/_Project/Data/Items/Affixes/AffixCatalog.asset";
        private const string DummyPrefabPath = "Assets/_Project/Prefabs/World/TrainingDummy.prefab";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject vendorObject;
        private NetworkManager network;
        private GameObject dummyPrefab;

        private NetworkObject player;
        private Health playerHealth;
        private Mana mana;
        private CharacterProgress progress;
        private AbilityCaster caster;
        private Inventory inventory;
        private Vendor vendor;
        private AffixCatalog affixes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            affixes = AssetDatabase.LoadAssetAtPath<AffixCatalog>(AffixCatalogPath);
            Assert.IsNotNull(affixes, "Run RPG > Build Test Scene first.");

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
            mana = player.GetComponent<Mana>();
            progress = player.GetComponent<CharacterProgress>();
            caster = player.GetComponent<AbilityCaster>();
            inventory = player.GetComponent<Inventory>();

            vendorObject = new GameObject("Test Vendor");
            vendorObject.transform.position = player.transform.position + new Vector3(0.6f, 0f, 0f);
            vendor = vendorObject.AddComponent<Vendor>();
            vendor.Configure(AssetDatabase.LoadAssetAtPath<VendorDefinition>(WarriorVendorPath));
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

        // ---------- Mana ----------

        [UnityTest]
        public IEnumerator Player_StartsWithFullMana()
        {
            yield return null;
            Assert.IsNotNull(mana);
            int expected = progress.Class.MaxManaAt(1) + AttributeRules.ManaBonus(progress.TotalAttributes);
            Assert.AreEqual(expected, mana.Max);
            Assert.AreEqual(mana.Max, mana.Current);
        }

        [UnityTest]
        public IEnumerator Ability_CostsMana_AndFailsWithoutEnough()
        {
            // Second Wind (warrior slot 2) needs no target.
            AbilityDefinition secondWind = progress.Class.abilities[2];
            Assert.Greater(secondWind.manaCost, 0);
            int before = mana.Current;

            Assert.IsTrue(caster.ServerTryCast(2, null, out CastFailure failure), failure.ToString());
            Assert.AreEqual(before - secondWind.manaCost, mana.Current);
            yield return null;

            Assert.IsTrue(mana.ServerSpend(mana.Current));
            Assert.IsFalse(caster.ServerTryCast(1, null, out failure));
            Assert.AreEqual(CastFailure.NoMana, failure);
        }

        [UnityTest]
        public IEnumerator ManaPotion_RestoresMana()
        {
            ItemDefinition potion = Item("ManaPotion");
            inventory.ServerAddItem(potion, 1, false);
            mana.ServerSpend(mana.Current);
            yield return null;

            Assert.IsTrue(inventory.ServerUse(FindInBag(potion)));
            Assert.Greater(mana.Current, 0);
        }

        // ---------- Attributes ----------

        [UnityTest]
        public IEnumerator LevelUp_GivesPoints_ThatRaiseStats()
        {
            Assert.AreEqual(0, progress.UnspentPoints);
            progress.ServerAddExperience(LevelCurve.ExperienceToNext(1));
            yield return null;
            Assert.AreEqual(AttributeRules.PointsPerLevel, progress.UnspentPoints);

            int healthBefore = playerHealth.Max;
            for (int i = 0; i < AttributeRules.PointsPerLevel; i++)
                Assert.IsTrue(progress.ServerSpendPoint(Attribute.Vitality));
            Assert.IsFalse(progress.ServerSpendPoint(Attribute.Vitality), "No points left.");
            yield return null;

            Assert.AreEqual(0, progress.UnspentPoints);
            Assert.AreEqual(AttributeRules.PointsPerLevel, progress.SpentPoints.vitality);
            Assert.AreEqual(healthBefore + AttributeRules.PointsPerLevel * AttributeRules.HealthPerVitality, playerHealth.Max);
        }

        // ---------- Affixes and junk ----------

        [UnityTest]
        public IEnumerator AffixItem_HasItsNameBonusesAndPrice()
        {
            ItemDefinition sword = Item("IronSword");
            AffixDefinition bear = Affix("OfTheBear");
            inventory.ServerAddItem(sword, 1, false, affixes.IdOf(bear));
            yield return null;

            int index = FindInBag(sword);
            ItemStack stack = inventory.GetBagSlot(index);
            Assert.AreEqual(bear, inventory.Affix(stack));
            Assert.AreEqual("Iron Sword of the Bear", inventory.DisplayName(stack));
            Assert.AreEqual(bear.quality, inventory.Quality(stack));
            Assert.Greater(sword.SellPriceWith(bear), sword.SellPrice);

            int healthBefore = playerHealth.Max;
            Assert.IsTrue(inventory.ServerEquip(index));
            yield return null;
            int expected = AttributeRules.HealthBonus(bear.attributes) + bear.healthBonus;
            Assert.AreEqual(healthBefore + expected, playerHealth.Max);
        }

        [UnityTest]
        public IEnumerator AffixItems_DoNotStackWithPlainOnes()
        {
            ItemDefinition sword = Item("IronSword");
            int usedBefore = UsedBagSlots();
            inventory.ServerAddItem(sword, 1, false);
            inventory.ServerAddItem(sword, 1, false, affixes.IdOf(Affix("Sturdy")));
            yield return null;
            Assert.AreEqual(usedBefore + 2, UsedBagSlots());
        }

        [Test]
        public void AffixRoll_NeverForPotionsOrJunk()
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(0, affixes.Roll(Item("HealingPotion"), 8));
                Assert.AreEqual(0, affixes.Roll(Item("GoblinEar"), 8));
            }
        }

        [UnityTest]
        public IEnumerator Junk_SellsForGold()
        {
            ItemDefinition ear = Item("GoblinEar");
            Assert.AreEqual(ItemRarity.Poor, ear.rarity);
            inventory.ServerAddItem(ear, 3, false);
            int goldBefore = inventory.Gold;
            yield return null;

            Assert.IsTrue(inventory.ServerSell(vendor, FindInBag(ear)));
            yield return null;
            Assert.Greater(inventory.Gold, goldBefore);
        }

        private static ItemDefinition Item(string fileName)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{fileName}.asset");
            Assert.IsNotNull(item, fileName);
            return item;
        }

        private static AffixDefinition Affix(string fileName)
        {
            var affix = AssetDatabase.LoadAssetAtPath<AffixDefinition>($"{ItemsFolder}/Affixes/{fileName}.asset");
            Assert.IsNotNull(affix, fileName);
            return affix;
        }

        private int FindInBag(ItemDefinition item)
        {
            for (int i = 0; i < inventory.BagSize; i++)
                if (inventory.Definition(inventory.GetBagSlot(i)) == item) return i;
            Assert.Fail($"{item.displayName} is not in the bag.");
            return -1;
        }

        private int UsedBagSlots()
        {
            int used = 0;
            for (int i = 0; i < inventory.BagSize; i++)
                if (!inventory.GetBagSlot(i).IsEmpty) used++;
            return used;
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
