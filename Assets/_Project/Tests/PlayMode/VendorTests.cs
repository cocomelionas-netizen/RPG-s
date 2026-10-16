#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Items;
using RPG.Monsters;
using RPG.Networking;
using RPG.Vendors;
using RPG.World;
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
        // The source game's vendors: the paladin (warrior) trainer crafts for now, the innkeeper sells potions, food and
        // the Scroll of Returning, the gem trader has limited stock, the heavy armor merchant random qualities.
        private const string WarriorVendorPath = "Assets/_Project/Data/Vendors/Halfrid.asset";
        private const string InnkeeperPath = "Assets/_Project/Data/Vendors/Aedis.asset";
        private const string GemTraderPath = "Assets/_Project/Data/Vendors/Calder.asset";
        private const string ArmorMerchantPath = "Assets/_Project/Data/Vendors/Karl.asset";
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

        private void UseVendor(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<VendorDefinition>(path);
            Assert.IsNotNull(definition, path);
            vendor.Configure(definition, AssetDatabase.LoadAssetAtPath<AffixCatalog>("Assets/_Project/Data/Items/Affixes/AffixCatalog.asset"));
        }

        [UnityTest]
        public IEnumerator Buying_CostsThePrice_AndPutsTheItemInTheBag()
        {
            UseVendor(InnkeeperPath);
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
            UseVendor(InnkeeperPath);
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
            UseVendor(GemTraderPath);
            int limited = -1;
            for (int i = 0; i < vendor.Definition.stock.Length; i++)
                if (vendor.Definition.LimitOf(i) > 0) limited = i;
            Assert.GreaterOrEqual(limited, 0, "The gem trader holds only a few of each gem.");
            inventory.ServerAddGold(50000, false);
            ItemDefinition item = vendor.Definition.stock[limited];
            int limit = vendor.Definition.LimitOf(limited);

            for (int i = 0; i < limit; i++) Assert.IsTrue(inventory.ServerBuy(vendor, limited));
            int goldAfter = inventory.Gold;
            Assert.AreEqual(0, vendor.ServerRemaining(limited));
            Assert.IsFalse(inventory.ServerBuy(vendor, limited), "Sold out.");
            yield return null;

            Assert.AreEqual(goldAfter, inventory.Gold);
            Assert.AreEqual(limit, inventory.CountOf(item));
        }

        [Test]
        public void Innkeeper_SellsPotions_ThatNeverRunOut_AndTheScrollOfReturning()
        {
            UseVendor(InnkeeperPath);
            ItemDefinition[] stock = vendor.Definition.stock;
            int potion = System.Array.FindIndex(stock, item => item.use == ItemUse.Heal);
            Assert.GreaterOrEqual(potion, 0);
            Assert.AreEqual(Vendor.Unlimited, vendor.ServerRemaining(potion), "Potions never run out.");
            Assert.IsTrue(System.Array.Exists(stock, item => item.use == ItemUse.Return && item.displayName == "Scroll of Returning"));
            Assert.IsTrue(System.Array.Exists(stock, item => item.use == ItemUse.Food), "Fresh Apple Pie.");
        }

        [UnityTest]
        public IEnumerator ArmorMerchant_SellsGear_WithTheQualityShown()
        {
            UseVendor(ArmorMerchantPath);
            Assert.IsTrue(vendor.Definition.randomQuality);
            int piece = System.Array.FindIndex(vendor.Definition.stock, item => item.IsEquippable);
            Assert.GreaterOrEqual(piece, 0, "The heavy armor merchant sells armor.");
            int affixId = vendor.ServerAffix(piece);
            AffixDefinition affix = inventory.Affixes.Get(affixId);
            Assert.IsNotNull(affix, "Its stock is at least green (the source game: green 100 %).");
            inventory.ServerAddGold(100000, false);
            int goldBefore = inventory.Gold;
            int price = inventory.BuyPrice(vendor.Definition.stock[piece], affix);
            Assert.GreaterOrEqual(price, vendor.Definition.stock[piece].price);

            Assert.IsTrue(inventory.ServerBuy(vendor, piece));
            yield return null;
            Assert.AreEqual(goldBefore - price, inventory.Gold);
            ItemStack bought = inventory.GetBagSlot(FindInBag(vendor.Definition.stock[piece]));
            Assert.AreEqual(affixId, bought.AffixId, "The piece comes with the quality the vendor showed.");
        }

        [UnityTest]
        public IEnumerator ScrollOfReturning_TakesYouToTown_AfterItsCast()
        {
            var town = new GameObject("Test Town Waystone");
            try
            {
                town.transform.position = player.transform.position + new Vector3(30f, 10f, 0f);
                town.AddComponent<Waystone>().Configure(30, Waystone.MainTownName);
                UseVendor(InnkeeperPath);
                ItemDefinition scroll = System.Array.Find(vendor.Definition.stock, item => item.use == ItemUse.Return);
                Assert.AreEqual(2f, scroll.castSeconds);
                Assert.AreEqual(300f, scroll.useCooldown);
                inventory.ServerAddItem(scroll, 2, false);
                yield return null;

                Assert.IsTrue(inventory.ServerUse(FindInBag(scroll)));
                Assert.IsTrue(inventory.IsReturning);
                Assert.AreEqual(2, inventory.CountOf(scroll), "Used up only when it works.");
                float end = Time.realtimeSinceStartup + 4f;
                while (inventory.IsReturning && Time.realtimeSinceStartup < end) yield return null;
                yield return null;
                yield return null;

                Assert.Less(Vector2.Distance(player.transform.position, town.GetComponent<Waystone>().ArrivalPoint), 0.5f);
                Assert.AreEqual(1, inventory.CountOf(scroll));
                Assert.Greater(inventory.ReturnCooldownRemaining, 250f, "5 minutes until the next one.");
                Assert.IsFalse(inventory.ServerUse(FindInBag(scroll)), "Still cooling down.");
            }
            finally
            {
                Object.Destroy(town);
            }
        }

        [UnityTest]
        public IEnumerator ScrollOfReturning_MovingBreaksItOff()
        {
            var town = new GameObject("Test Town Waystone");
            try
            {
                town.transform.position = player.transform.position + new Vector3(30f, 10f, 0f);
                town.AddComponent<Waystone>().Configure(30, Waystone.MainTownName);
                UseVendor(InnkeeperPath);
                ItemDefinition scroll = System.Array.Find(vendor.Definition.stock, item => item.use == ItemUse.Return);
                inventory.ServerAddItem(scroll, 1, false);
                yield return null;

                Assert.IsTrue(inventory.ServerUse(FindInBag(scroll)));
                player.GetComponent<PlayerNetworkMovement>().ServerTeleport((Vector2)player.transform.position + new Vector2(2f, 0f));
                float end = Time.realtimeSinceStartup + 4f;
                while (inventory.IsReturning && Time.realtimeSinceStartup < end) yield return null;

                Assert.Greater(Vector2.Distance(player.transform.position, town.GetComponent<Waystone>().ArrivalPoint), 5f);
                Assert.AreEqual(1, inventory.CountOf(scroll), "Not used up.");
                Assert.AreEqual(0f, inventory.ReturnCooldownRemaining);
            }
            finally
            {
                Object.Destroy(town);
            }
        }

        [UnityTest]
        public IEnumerator Food_OutOfCombat_IsEaten_AndHasACooldown()
        {
            UseVendor(InnkeeperPath);
            ItemDefinition food = System.Array.Find(vendor.Definition.stock, item => item.use == ItemUse.Food);
            Assert.AreEqual(30f, food.useCooldown);
            Assert.Greater(food.restoreAmount, 0);
            inventory.ServerAddItem(food, 2, false);
            yield return null;

            Assert.IsTrue(inventory.ServerUse(FindInBag(food)));
            Assert.AreEqual(1, inventory.CountOf(food));
            Assert.IsFalse(inventory.ServerUse(FindInBag(food)), "30 seconds until the next.");
            Assert.Greater(inventory.FoodCooldownRemaining, 25f);
        }

        [UnityTest]
        public IEnumerator Food_RestoresHealthAndMana_ButNotInCombat()
        {
            UseVendor(InnkeeperPath);
            ItemDefinition food = System.Array.Find(vendor.Definition.stock, item => item.use == ItemUse.Food);
            inventory.ServerAddItem(food, 2, false);
            var health = player.GetComponent<Health>();
            health.ApplyDamage(health.Max / 2, false, null);
            yield return null;

            Assert.IsFalse(inventory.ServerUse(FindInBag(food)), "Just hit: in combat.");
            Assert.AreEqual(2, inventory.CountOf(food));
        }

        [UnityTest]
        public IEnumerator FivePotions_CombineIntoTheNextTier()
        {
            ItemDefinition minor = Item("HealingPotion");
            Assert.IsNotNull(minor.combinesInto, "Minor Life Potion combines into Lesser Life Potion.");
            int before = inventory.CountOf(minor);
            inventory.ServerAddItem(minor, 5 - before % 5, false);
            int expected = inventory.CountOf(minor) / 5;

            Assert.AreEqual(expected, inventory.ServerCombinePotions(vendor));
            yield return null;
            Assert.AreEqual(0, inventory.CountOf(minor));
            Assert.AreEqual(expected, inventory.CountOf(minor.combinesInto));
        }

        [UnityTest]
        public IEnumerator PotionAboveYourLevel_CannotBeUsed()
        {
            ItemDefinition lesser = Item("HealingPotion").combinesInto;
            Assert.Greater(lesser.itemLevel, 1);
            inventory.ServerAddItem(lesser, 1, false);
            yield return null;

            Assert.IsFalse(inventory.ServerUse(FindInBag(lesser)));
            Assert.AreEqual(1, inventory.CountOf(lesser));
            Assert.AreNotEqual(FindInBag(lesser), inventory.FindUsable(ItemUse.Heal), "Keys 5 and 6 skip it.");
        }

        [Test]
        public void PotionDrops_FollowTheDroppersLevel()
        {
            ItemDefinition minor = Item("HealingPotion");
            for (int i = 0; i < 50; i++)
            {
                ItemDefinition low = LootDrop.PotionForLevel(minor, 1);
                Assert.AreEqual(1, low.itemLevel, low.displayName);
                ItemDefinition high = LootDrop.PotionForLevel(minor, 25);
                Assert.AreEqual(24, high.itemLevel, high.displayName);
                StringAssert.StartsWith("Major Life", high.displayName);
            }
        }

        private int FirstLevelCraft()
        {
            ItemDefinition[] crafts = vendor.Definition.crafts;
            for (int i = 0; i < crafts.Length; i++)
                if (crafts[i] != null && crafts[i].itemLevel == 1 && crafts[i].IsCraftable) return i;
            return -1;
        }

        [UnityTest]
        public IEnumerator Crafting_TakesTheMaterials_AndGivesThePiece()
        {
            int index = FirstLevelCraft();
            Assert.GreaterOrEqual(index, 0, "The warrior vendor crafts level 1 gear.");
            ItemDefinition piece = vendor.Definition.crafts[index];
            inventory.ServerAddItem(piece.craftMaterial, piece.craftCount + 1, false);

            Assert.IsTrue(inventory.ServerCraft(vendor, index));
            yield return null;
            Assert.AreEqual(1, inventory.CountOf(piece.craftMaterial), "Only the recipe's materials are used.");
            Assert.AreEqual(1, inventory.CountOf(piece));
        }

        [UnityTest]
        public IEnumerator Crafting_WithTooFewMaterials_Fails()
        {
            int index = FirstLevelCraft();
            ItemDefinition piece = vendor.Definition.crafts[index];
            inventory.ServerAddItem(piece.craftMaterial, piece.craftCount - 1, false);

            Assert.IsFalse(inventory.ServerCraft(vendor, index));
            yield return null;
            Assert.AreEqual(piece.craftCount - 1, inventory.CountOf(piece.craftMaterial));
            Assert.AreEqual(0, inventory.CountOf(piece));
        }

        [UnityTest]
        public IEnumerator Crafting_SeveralAtOnce_TakesTheMaterialsAndTheFee()
        {
            int index = FirstLevelCraft();
            ItemDefinition piece = vendor.Definition.crafts[index];
            inventory.ServerAddItem(piece.craftMaterial, piece.craftCount * 3, false);
            inventory.ServerSetGold(1000);

            Assert.IsTrue(inventory.ServerCraft(vendor, index, 3));
            yield return null;
            Assert.AreEqual(0, inventory.CountOf(piece.craftMaterial));
            Assert.AreEqual(3, inventory.CountOf(piece), "Three pieces (each may have its own affix).");
            Assert.AreEqual(1000 - Inventory.CraftFee(piece) * 3, inventory.Gold, "The vendor takes its fee for each.");
        }

        [UnityTest]
        public IEnumerator Crafting_WithoutTheFee_Fails()
        {
            int index = FirstLevelCraft();
            ItemDefinition piece = vendor.Definition.crafts[index];
            inventory.ServerAddItem(piece.craftMaterial, piece.craftCount, false);
            inventory.ServerSetGold(0);

            Assert.IsFalse(inventory.ServerCraft(vendor, index));
            yield return null;
            Assert.AreEqual(piece.craftCount, inventory.CountOf(piece.craftMaterial), "Nothing is used up.");
        }

        [UnityTest]
        public IEnumerator Recipes_MakePotionTiers_LikeTheSourceGame()
        {
            Recipe[] recipes = vendor.Definition.recipes;
            int index = System.Array.FindIndex(recipes, recipe => recipe.result != null && recipe.result.displayName == "Lesser Life Potion");
            Assert.GreaterOrEqual(index, 0, "The crafting vendor makes Lesser Life Potions from Minor ones.");
            RecipeIngredient minor = recipes[index].ingredients[0];
            Assert.AreEqual("Minor Life Potion", minor.item.displayName);
            Assert.AreEqual(5, minor.count);

            int before = inventory.CountOf(minor.item);
            inventory.ServerAddItem(minor.item, minor.count * 2, false);
            inventory.ServerSetGold(1000);
            Assert.IsTrue(inventory.ServerCraftRecipe(vendor, index, 2));
            yield return null;
            Assert.AreEqual(before, inventory.CountOf(minor.item));
            Assert.AreEqual(2, inventory.CountOf(recipes[index].result));

            Assert.IsFalse(inventory.ServerCraftRecipe(vendor, index, 1), "Without the materials nothing is made.");
        }

        [UnityTest]
        public IEnumerator Crafting_AboveYourLevel_Fails()
        {
            ItemDefinition[] crafts = vendor.Definition.crafts;
            int index = System.Array.FindIndex(crafts, item => item != null && item.itemLevel >= 10);
            Assert.GreaterOrEqual(index, 0);
            inventory.ServerAddItem(crafts[index].craftMaterial, crafts[index].craftCount, false);

            Assert.IsFalse(inventory.ServerCraft(vendor, index));
            yield return null;
            Assert.AreEqual(0, inventory.CountOf(crafts[index]));
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
