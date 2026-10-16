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
            // Mighty Blow (bar slot 1, key 2; Attack is on key 1): the source game's mana, base plus per level.
            AbilityDefinition mightyBlow = caster.GetAbility(1);
            int cost = caster.ManaCost(mightyBlow);
            Assert.Greater(cost, 0);
            Assert.AreEqual(mightyBlow.ManaCostAt(progress.Level), cost);
            GameObject dummy = Object.Instantiate(dummyPrefab, player.transform.position + new Vector3(0.5f, 0f, 0f), Quaternion.identity);
            dummy.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            yield return null;
            int before = mana.Current;

            Assert.IsTrue(caster.ServerTryCast(1, dummy.GetComponent<Health>(), out CastFailure failure), failure.ToString());
            Assert.AreEqual(before - cost, mana.Current);
            yield return new WaitForSeconds(mightyBlow.cooldown + 0.1f);

            Assert.IsTrue(mana.ServerSpend(mana.Current));
            Assert.IsFalse(caster.ServerTryCast(1, dummy.GetComponent<Health>(), out failure));
            Assert.AreEqual(CastFailure.NoMana, failure);
        }

        // ---------- Attributes ----------

        [Test]
        public void PointCosts_GrowWithEveryPoint_LikeTheSourceGame()
        {
            // Attributes: x^1.5 + 5 for the next point after x.
            Assert.AreEqual(5, AttributeRules.PointCost(Attribute.Strength, 0));
            Assert.AreEqual(6, AttributeRules.PointCost(Attribute.Strength, 1));
            Assert.AreEqual(36, AttributeRules.PointCost(Attribute.Strength, 10));
            // Health and mana: x^(x/1500), at least 1, cheap for a long time.
            Assert.AreEqual(1, AttributeRules.PointCost(Attribute.Health, 0));
            Assert.AreEqual(1, AttributeRules.PointCost(Attribute.Mana, 100));
            Assert.AreEqual(1500, AttributeRules.PointCost(Attribute.Health, 1500));
        }

        [UnityTest]
        public IEnumerator Experience_BuysPoints_ThatRaiseStats()
        {
            Assert.AreEqual(0, progress.ExperienceToSpend);
            int gained = LevelCurve.ExperienceToNext(1);
            progress.ServerAddExperience(gained);
            yield return null;
            Assert.AreEqual(2, progress.Level, "Spending experience does not take levels away.");
            Assert.AreEqual(gained, progress.ExperienceToSpend);

            int healthBefore = playerHealth.Max;
            int expected = gained;
            int bought = 0;
            while (progress.CanAfford(Attribute.Vitality))
            {
                expected -= progress.PointCost(Attribute.Vitality);
                Assert.IsTrue(progress.ServerSpendPoint(Attribute.Vitality));
                bought++;
            }
            Assert.IsFalse(progress.ServerSpendPoint(Attribute.Vitality), "Not enough experience left.");
            yield return null;

            Assert.Greater(bought, 0);
            Assert.AreEqual(expected, progress.ExperienceToSpend);
            Assert.AreEqual(bought, progress.SpentPoints.vitality);
            Assert.AreEqual(healthBefore + bought * AttributeRules.HealthPerVitality, playerHealth.Max);
        }

        [UnityTest]
        public IEnumerator PointsIntoHealthAndMana_RaiseThemDirectly()
        {
            progress.ServerAddExperience(LevelCurve.ExperienceToNext(1));
            yield return null;
            int healthBefore = playerHealth.Max;
            int manaBefore = mana.Max;

            Assert.IsTrue(progress.ServerSpendPoint(Attribute.Health));
            Assert.IsTrue(progress.ServerSpendPoint(Attribute.Mana));
            yield return null;

            Assert.AreEqual(healthBefore + AttributeRules.HealthPerPoint, playerHealth.Max);
            Assert.AreEqual(manaBefore + AttributeRules.ManaPerPoint, mana.Max);
            Assert.AreEqual(1, progress.SpentPoints.health);
        }

        [Test]
        public void StatCosts_FromTheClientData()
        {
            Assert.AreEqual(6, AttributeRules.PointCost(Attribute.MeleeCritical, 10), "x/2 + 1");
            Assert.AreEqual(4, AttributeRules.PointCost(Attribute.RangedCritical, 9), "x/3 + 1");
            Assert.AreEqual(4, AttributeRules.PointCost(Attribute.ResistFire, 9), "x/3 + 1");
            Assert.AreEqual(6, AttributeRules.PointCost(Attribute.Dodge, 5), "x + 1");
            Assert.AreEqual(7, AttributeRules.PointCost(Attribute.Bartering, 3), "2x + 1");
            Assert.AreEqual(4, AttributeRules.PointCost(Attribute.Regeneration, 0), "x^1.4 + 4");
            Assert.AreEqual(3 * AttributeRules.PointCost(Attribute.Strength, 4), AttributeRules.PointCost(Attribute.Swords, 4), "3x Strength's");
            Assert.AreEqual("Courage", AttributeRules.NameOf(Attribute.Vitality));
        }

        [UnityTest]
        public IEnumerator DodgeBlockAndResists_ProtectThePlayer()
        {
            playerHealth.ServerDodgeChance = 1f;
            int full = playerHealth.Current;
            playerHealth.ApplyDamage(20, false);
            Assert.AreEqual(full, playerHealth.Current, "A dodged hit does nothing.");

            playerHealth.ServerDodgeChance = 0f;
            playerHealth.ServerArmor = 0;
            playerHealth.ServerBlockChance = 1f;
            playerHealth.ApplyDamage(20, false);
            Assert.AreEqual(full - 10, playerHealth.Current, "A blocked hit does half.");

            playerHealth.ServerBlockChance = 0f;
            playerHealth.ServerSetResist(DamageSchool.Fire, 100);
            int before = playerHealth.Current;
            playerHealth.ApplyDamage(20, false, null, DamageSchool.Fire);
            Assert.AreEqual(before - 10, playerHealth.Current, "100 fire resist halves fire damage.");
            before = playerHealth.Current;
            playerHealth.ApplyDamage(20, false, null, DamageSchool.Frost);
            Assert.AreEqual(before - 20, playerHealth.Current, "Not frost damage.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointsInCombatStats_ReachTheFight()
        {
            progress.ServerAddExperience(LevelCurve.ExperienceToNext(1));
            yield return null;
            Assert.IsTrue(progress.ServerSpendPoint(Attribute.Dodge));
            Assert.IsTrue(progress.ServerSpendPoint(Attribute.ResistShadow));
            Assert.IsTrue(progress.ServerSpendPoint(Attribute.SpellCritical));
            yield return null;

            Assert.AreEqual(AttributeRules.AvoidPerRating, playerHealth.ServerDodgeChance, 1e-6f);
            Assert.AreEqual(1, playerHealth.ServerResist(DamageSchool.Shadow));
            Assert.Greater(progress.SpellCritChance, progress.CritChance - 0.0001f);
        }

        // ---------- Affixes and junk ----------

        [UnityTest]
        public IEnumerator AffixItem_HasItsNameBonusesAndPrice()
        {
            ItemDefinition sword = Item("IronSword");
            AffixDefinition bear = Affix("OfTheBear");
            inventory.ServerAddItem(sword, 1, false, affixes.IdOf(bear));
            // High enough to wear the sword.
            progress.ServerLoad(0, sword.itemLevel, 0, default, 0);
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
        public void AffixRoll_UsesAffixDataOfTheLevel()
        {
            ItemDefinition sword = Item("Generated/BronzeSword_L03");
            int rolled = 0;
            for (int i = 0; i < 200; i++)
            {
                AffixDefinition affix = affixes.Get(affixes.Roll(sword, 3));
                if (affix == null) continue;
                rolled++;
                Assert.IsFalse(affix.retired, affix.displayName);
                Assert.LessOrEqual(affix.minLevel, 3, affix.displayName);
                Assert.GreaterOrEqual(affix.maxLevel, 3, affix.displayName);
                StringAssert.StartsWith("of ", affix.displayName);
            }
            Assert.Greater(rolled, 0);
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
