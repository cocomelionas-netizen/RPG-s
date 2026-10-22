using NUnit.Framework;
using RPG.Dungeons;
using RPG.UI;
using RPG.Vendors;
using UnityEngine;

namespace RPG.Tests
{
    /// <summary>The world map's badges, which badge a merchant gets and what a dungeon's tooltip says.</summary>
    public class WorldMapTests
    {
        [Test]
        public void EveryBadge_AndThePaintedIsland_AreThere()
        {
            foreach (string badge in new[]
            {
                "Town", "Dungeon", "Boss", "Waystone", "Weapons", "Armor", "Potions", "Gems", "Food", "Bank", "Transmute", "Trainer",
                "Healer", "Goods", "Person", "Place", "Player", "Pin",
            })
                Assert.IsNotNull(WorldMap.Icon(badge), $"No map badge {badge}.");
            Assert.IsNotNull(Resources.Load<Texture2D>("UI/Map/World"), "No painted island.");
        }

        [Test]
        public void Merchants_GetTheBadgeOfWhatTheySell()
        {
            VendorDefinition Vendor(string title)
            {
                var vendor = ScriptableObject.CreateInstance<VendorDefinition>();
                vendor.title = title;
                return vendor;
            }

            Assert.AreEqual("Armor", WorldMap.VendorIcon(Vendor("Heavy Armor Trader")));
            Assert.AreEqual("Weapons", WorldMap.VendorIcon(Vendor("Blades Trader")));
            Assert.AreEqual("Potions", WorldMap.VendorIcon(Vendor("Alchemist")));
            Assert.AreEqual("Gems", WorldMap.VendorIcon(Vendor("Jeweler")));
            Assert.AreEqual("Food", WorldMap.VendorIcon(Vendor("Innkeeper")));
            Assert.AreEqual("Bank", WorldMap.VendorIcon(Vendor("Banker")));
            Assert.AreEqual("Trainer", WorldMap.VendorIcon(Vendor("Mage")));
            Assert.AreEqual("Goods", WorldMap.VendorIcon(Vendor("Traveling Trader")));
            Assert.AreEqual("Goods", WorldMap.VendorIcon(null));
        }

        [Test]
        public void DungeonTooltip_TellsWhoMayEnter_AndItsBosses()
        {
            var dungeon = ScriptableObject.CreateInstance<DungeonDefinition>();
            dungeon.displayName = "Crumbling Tower";
            dungeon.minLevel = 25;
            dungeon.maxPlayers = 4;
            dungeon.monsters = new[]
            {
                new DungeonMonsterSpot { displayName = "Lord Valdrek", level = 30, boss = true },
                new DungeonMonsterSpot { displayName = "Skeleton", level = 25 },
                new DungeonMonsterSpot { displayName = "Lord Valdrek", level = 30, boss = true },
            };
            string tooltip = WorldMap.DungeonTooltip(dungeon, "?");
            StringAssert.StartsWith("Crumbling Tower", tooltip);
            StringAssert.Contains("level 25+, up to 4 players", tooltip);
            StringAssert.Contains("Boss: Lord Valdrek (30)", tooltip);
            StringAssert.DoesNotContain("Skeleton", tooltip);
        }
    }
}
