#if UNITY_EDITOR
using NUnit.Framework;
using RPG.Vendors;
using UnityEditor;
using UnityEngine;

namespace RPG.Tests
{
    /// <summary>Every stock item restocks on its own time: the source game's, 5 minutes for orbs, gems and merchants' gear.</summary>
    public class VendorRestockTests
    {
        private const string GemTraderPath = "Assets/_Project/Data/Vendors/Tamsin.asset";
        private const string ArmorMerchantPath = "Assets/_Project/Data/Vendors/Otto.asset";
        private const string TravelingTraderPath = "Assets/_Project/Data/Vendors/Nibbets.asset";

        [Test]
        public void OrbsAndGems_RestockEveryFiveMinutes()
        {
            var gems = AssetDatabase.LoadAssetAtPath<VendorDefinition>(GemTraderPath);
            Assert.IsNotNull(gems, "Run RPG > Build Test Scene first.");
            int divine = System.Array.FindIndex(gems.stock, item => item != null && item.displayName == "Divine Weapon Orb");
            Assert.GreaterOrEqual(divine, 0, "The gem trader sells Divine orbs.");
            Assert.AreEqual(300f, gems.RestockOf(divine));
            for (int i = 0; i < gems.stock.Length; i++)
                Assert.AreEqual(300f, gems.RestockOf(i), gems.stock[i].displayName);
        }

        [Test]
        public void MerchantGear_RestocksEveryFiveMinutes_TheTravelingTraderOnItsOwnTimes()
        {
            var armor = AssetDatabase.LoadAssetAtPath<VendorDefinition>(ArmorMerchantPath);
            Assert.IsTrue(armor.randomQuality);
            for (int i = 0; i < armor.stock.Length; i++)
                if (armor.stock[i].IsEquippable) Assert.AreEqual(300f, armor.RestockOf(i));

            var trader = AssetDatabase.LoadAssetAtPath<VendorDefinition>(TravelingTraderPath);
            Assert.AreEqual(trader.stock.Length, trader.restockTimes.Length);
            Assert.That(trader.restockTimes, Has.Some.EqualTo(30f), "The source game's 30 s items keep their time.");
            Assert.That(trader.restockTimes, Has.Some.EqualTo(300f));
        }

        [Test]
        public void RestockOf_FallsBackToTheVendorsTime()
        {
            var vendor = ScriptableObject.CreateInstance<VendorDefinition>();
            vendor.restockSeconds = 120f;
            vendor.restockTimes = new[] { 0f, 45f };
            Assert.AreEqual(120f, vendor.RestockOf(0));
            Assert.AreEqual(45f, vendor.RestockOf(1));
            Assert.AreEqual(120f, vendor.RestockOf(5));
            Object.DestroyImmediate(vendor);
        }
    }
}
#endif
