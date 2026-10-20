using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPG.Dungeons;
using RPG.Items;
using RPG.Monsters;
using RPG.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.EditorTools
{
    /// <summary>
    /// The source game's dungeons (DungeonData): their keys and treasures, the dungeon assets with their plans, monsters
    /// and containers, the entrances in the world, and the outdoor monsters that drop keys.
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const string DungeonsFolder = "Assets/_Project/Data/Dungeons";

        /// <summary>
        /// The source game's dungeon keys and treasures, sold back for the source game's price. The items clicked to put a recipe
        /// together (Dale Gem on a Depleted Key, an Inactive Demiplane Key with Magic Dust and Major Treasury Gold) combine.
        /// They go at the end of the catalog (item ids are saved with characters).
        /// </summary>
        private static List<ItemDefinition> CreateDungeonItems()
        {
            var created = new Dictionary<string, ItemDefinition>();
            foreach (var entry in DungeonData.Items)
            {
                var rarity = (ItemRarity)Enum.Parse(typeof(ItemRarity), entry.quality);
                // Vendors pay the source game's sell price and ask four times it.
                ItemDefinition item = CreateItem(entry.id, entry.name, ItemSlot.None, rarity, 0, 0, 0, DungeonItemColor(entry.id), entry.sellPrice * 4);
                item.sellValue = entry.sellPrice;
                item.maxStack = Mathf.Max(1, entry.stack);
                item.canHaveAffix = false;
                item.description = entry.description;
                created[entry.id] = item;
            }

            foreach (var recipe in DungeonData.Recipes)
            {
                foreach (var entry in DungeonData.Items)
                {
                    if (!entry.combines || Array.IndexOf(recipe.ingredients, entry.id) < 0) continue;
                    ItemDefinition item = created[entry.id];
                    item.use = ItemUse.Combine;
                    item.combineWith = recipe.ingredients.Where(other => other != entry.id).Select(other => created[other]).ToArray();
                    item.combineResult = created[recipe.result];
                    EditorUtility.SetDirty(item);
                }
            }
            return DungeonData.Items.Select(entry => created[entry.id]).ToList();
        }

        private static Color DungeonItemColor(string id)
        {
            if (id.Contains("Key") || id.Contains("Keys")) return new Color(0.85f, 0.65f, 0.25f);
            if (id.Contains("Weapon")) return new Color(0.85f, 0.3f, 0.25f);
            if (id.Contains("Armor")) return new Color(0.3f, 0.5f, 0.9f);
            if (id.Contains("Accessory")) return new Color(0.4f, 0.8f, 0.5f);
            if (id == "DaleGem") return new Color(0.55f, 0.85f, 0.9f);
            if (id == "MagicDust") return new Color(0.85f, 0.85f, 0.95f);
            if (id == "ArcaneEssence") return new Color(0.7f, 0.4f, 0.95f);
            return new Color(0.95f, 0.8f, 0.3f);
        }

        /// <summary>A source-game loot table (DungeonData.Loot) as loot entries (none for 0).</summary>
        private static LootEntry[] DungeonLoot(int lootId)
        {
            if (lootId <= 0 || !DungeonData.Loot.TryGetValue(lootId, out var rows)) return Array.Empty<LootEntry>();
            var entries = new List<LootEntry>();
            foreach (var row in rows)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{row.item}.asset");
                if (definition == null)
                {
                    Debug.LogError($"[TestScene] Dungeon loot item {row.item} not found.");
                    continue;
                }
                entries.Add(new LootEntry(definition, row.chance / 100f, row.min, row.max));
            }
            return entries.ToArray();
        }

        /// <summary>
        /// The source game's containers in the dungeons, one prefab per name ("Rosewood Chest"): crates and barrels hold gold;
        /// chests are locked and hold gear (green 100%, blue 12%, gold 5%, purple 1%), a potion and a gem (75%) and twice
        /// the gold; strongboxes two more pieces, a potion and a gem for sure and three times the gold.
        /// </summary>
        private static Dictionary<string, GameObject> CreateDungeonContainers(ChestPrefabs chests, ItemCatalog items, AffixCatalog affixes,
            GameObject lootBagPrefab)
        {
            ItemDefinition[] potions = Loot(items, ("HealingPotion", 1f), ("ManaPotion", 1f)).Select(entry => entry.item).ToArray();
            ItemDefinition[] gems = Loot(items, ("ChippedRuby", 1f), ("ChippedTopaz", 1f), ("ChippedSapphire", 1f), ("ChippedEmerald", 1f), ("RoughAmethyst", 1f))
                .Select(entry => entry.item).ToArray();
            var prefabs = new Dictionary<string, GameObject>();
            foreach (DungeonData.Dungeon data in DungeonData.All)
            {
                foreach (var spot in data.Chests)
                {
                    if (prefabs.ContainsKey(spot.name)) continue;
                    GameObject look = spot.kind == 2 ? chests.Strongbox : spot.kind == 1 ? chests.Chest : chests.Crate;
                    Sprite sprite = spot.kind == 0 && spot.name.IndexOf("Barrel", StringComparison.OrdinalIgnoreCase) >= 0
                        ? chests.Barrel : look.GetComponent<SpriteRenderer>().sprite;
                    Action<LootChest> configure = null;
                    if (spot.kind == 2) configure = chest => chest.ConfigureLocked(100f, 15f, 5f, 1f, 2, 100f, 100f, potions, gems);
                    else if (spot.kind == 1) configure = chest => chest.ConfigureLocked(100f, 12f, 5f, 1f, 0, 75f, 75f, potions, gems);
                    float goldRatio = spot.kind == 2 ? 3f : spot.kind == 1 ? 2f : 1f;
                    prefabs[spot.name] = CreateChestPrefab("Dungeon" + spot.name.Replace(" ", "").Replace("'", ""), spot.name,
                        Mathf.Max(1, spot.level), goldRatio, sprite, lootBagPrefab, affixes, items, configure);
                }
            }
            return prefabs;
        }

        /// <summary>
        /// One asset per source-game dungeon: its plan, arrival, exits (destinations are set when the world is placed),
        /// monsters (each with its Flare look over its level tier's numbers, like outdoors), containers and entry
        /// rules, and doors into the dungeons entered from inside it. In DungeonData' order.
        /// </summary>
        private static DungeonDefinition[] CreateDungeons(GameObject orc, WorldTiles tiles, Dictionary<string, GameObject> containers)
        {
            var instance = new GameObject("Dungeon Instance");
            instance.AddComponent<NetworkObject>();
            instance.AddComponent<DungeonInstance>();
            Directory.CreateDirectory(Path.GetDirectoryName(DungeonInstancePrefabPath));
            GameObject instancePrefab = PrefabUtility.SaveAsPrefabAsset(instance, DungeonInstancePrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            RegisterNetworkPrefab(instancePrefab);

            // Flare's paving and rock pillars.
            Tile floorTile = tiles.Path[0];
            Tile wallTile = tiles.Walls[0];
            Sprite portalSprite = PlaceholderArtGenerator.Portal("Portal");

            var byId = new Dictionary<string, DungeonDefinition>();
            var all = new List<DungeonDefinition>();
            foreach (DungeonData.Dungeon data in DungeonData.All)
            {
                var dungeon = LoadOrCreateAsset<DungeonDefinition>($"{DungeonsFolder}/{data.Id}.asset");
                dungeon.displayName = data.Name;
                dungeon.instancePrefab = instancePrefab;
                dungeon.floorTile = floorTile;
                dungeon.wallTile = wallTile;
                dungeon.portalSprite = portalSprite;
                dungeon.rows = data.Rows;
                dungeon.arrival = data.Arrival;
                dungeon.exits = data.Exits.Select(exit => new DungeonExit { cell = exit.cell, label = exit.name }).ToArray();
                dungeon.doors = Array.Empty<DungeonDoor>();
                dungeon.referenceMonster = orc;
                dungeon.monsters = data.Monsters.Select(m => new DungeonMonsterSpot
                {
                    prefab = MonsterPrefabFor(m.name.Trim(), m.level, m.boss),
                    cell = m.cell,
                    displayName = m.name,
                    controlImmune = Array.IndexOf(MonsterData.ControlImmune, m.name.Trim()) >= 0,
                    spells = MonsterSpellsFor(m.name.Trim()),
                    level = m.level,
                    elite = m.elite,
                    boss = m.boss,
                    healthScale = m.health,
                    damageScale = m.damage,
                    goldScale = m.gold,
                    loot = DungeonLoot(m.loot).Concat(JunkLoot(m.name.Trim(), m.level)).ToArray(),
                    blueChance = m.blue,
                    goldChance = m.goldQuality,
                    purpleChance = m.purple,
                }).ToArray();
                dungeon.chests = data.Chests.Select(c => new DungeonChestSpot { prefab = containers[c.name], cell = c.cell, level = c.level }).ToArray();
                dungeon.minLevel = data.MinLevel;
                dungeon.maxPlayers = data.MaxPlayers;
                dungeon.key = data.Key != null ? AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{data.Key}.asset") : null;
                if (data.Key != null && dungeon.key == null) Debug.LogError($"[TestScene] Dungeon key {data.Key} not found.");
                dungeon.monsterAggroRadius = 3.5f;
                byId[data.Id] = dungeon;
                all.Add(dungeon);
            }

            // Salmaster's Descent opens inside the Desecrated Altar, Telamont's Sanctuary inside the Demiplane.
            foreach (DungeonData.Dungeon data in DungeonData.All)
            {
                if (data.Parent == null) continue;
                DungeonDefinition parent = byId[data.Parent];
                parent.doors = parent.doors.Append(new DungeonDoor { cell = data.ParentCell, target = byId[data.Id] }).ToArray();
            }
            foreach (DungeonDefinition dungeon in all) EditorUtility.SetDirty(dungeon);
            return all.ToArray();
        }

        /// <summary>
        /// The dungeon manager, an entrance where the source game has each dungeon's (at the world's south edge for those
        /// further south), and where every exit leads in this world.
        /// </summary>
        private static void PlaceDungeons(Grid grid, Tilemap obstacles, DungeonDefinition[] dungeons)
        {
            new GameObject("Dungeon Manager").AddComponent<DungeonManager>().Configure(dungeons);
            var parent = new GameObject("Dungeon Entrances").transform;
            DungeonData.Dungeon[] all = DungeonData.All;
            for (int i = 0; i < all.Length; i++)
            {
                DungeonDefinition dungeon = dungeons[i];
                for (int e = 0; e < dungeon.exits.Length; e++)
                {
                    // Next to the entrance it comes back to, not on it.
                    Vector3Int world = all[i].Exits[e].world + new Vector3Int(-1, -1, 0);
                    dungeon.exits[e].destination = grid.GetCellCenterWorld(OpenCellNear(obstacles, world));
                }
                EditorUtility.SetDirty(dungeon);
                if (all[i].Parent != null) continue;

                var entrance = new GameObject($"Dungeon Entrance {dungeon.displayName}");
                entrance.transform.SetParent(parent, false);
                entrance.transform.position = grid.GetCellCenterWorld(OpenCellNear(obstacles, all[i].WorldEntrance));
                var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
                visual.transform.SetParent(entrance.transform, false);
                visual.sprite = dungeon.portalSprite;
                visual.spriteSortPoint = SpriteSortPoint.Pivot;
                var box = entrance.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.6f, 0.9f);
                box.offset = new Vector2(0f, 0.45f);
                entrance.AddComponent<Portal>().ConfigureEntrance(visual, null, dungeon);
            }
        }

        /// <summary>The source-game loot table of an outdoor monster spot that drops keys (Keys to the Barracks), or 0.</summary>
        private static int KeyDropperLoot(Vector3Int cell)
        {
            foreach (var dropper in DungeonData.KeyDroppers)
                if (dropper.cell == cell) return dropper.loot;
            return 0;
        }

        /// <summary>
        /// Outdoor key droppers the source game has beyond this world's edge (DungeonData.EdgeMonsters). The world is the whole
        /// island now, so there are none: the Zhendale camp, Tantorus and Fasthorn are ordinary spots (KeyDroppers).
        /// </summary>
        private static void PlaceEdgeMonsters(Grid grid, Tilemap obstacles)
        {
            var parent = new GameObject("Edge Monster Spots").transform;
            foreach (var edge in DungeonData.EdgeMonsters)
            {
                var spot = new GameObject($"Edge Spot {edge.name}");
                spot.transform.SetParent(parent, false);
                spot.transform.position = grid.GetCellCenterWorld(OpenCellNear(obstacles, edge.cell));
                var spawner = spot.AddComponent<MonsterSpawner>();
                spawner.ActivationRange = MonsterActivationRange;
                spawner.Configure(MonsterPrefabFor(edge.name, edge.level, false), 1, 0.6f, edge.respawn);
                spawner.EliteChance = edge.elite ? 1f : 0f;
                spawner.Level = edge.level;
                spawner.DisplayName = edge.name;
                spawner.ControlImmune = Array.IndexOf(MonsterData.ControlImmune, edge.name) >= 0;
                spawner.Spells = MonsterSpellsFor(edge.name);
                spawner.BonusLoot = DungeonLoot(edge.loot).Concat(JunkLoot(edge.name, edge.level)).ToArray();
            }
        }
    }
}
