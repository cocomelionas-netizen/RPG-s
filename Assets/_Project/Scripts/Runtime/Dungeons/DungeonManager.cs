using System.Collections.Generic;
using RPG.Combat;
using RPG.Items;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Networking;
using RPG.Progression;
using RPG.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.Dungeons
{
    /// <summary>
    /// Runs dungeon instances. One lives in the world scene on every peer.
    /// - Everyone: builds an instance's tilemaps, navigation, exits and doors when it spawns.
    /// - Server: when a player uses an entrance, every living player standing around it enters a new instance of its
    ///   dungeon in a free slot far from the world (each group gets its own copy). Fills it with the dungeon's monsters
    ///   and containers (once: nothing comes back), sends players out through the exits, and closes instances that
    ///   stayed empty for a while.
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        [SerializeField] private DungeonDefinition[] dungeons = System.Array.Empty<DungeonDefinition>();
        [Tooltip("Instances are placed this far from the world, one slot after another.")]
        [SerializeField] private Vector2 firstSlotOrigin = new Vector2(500f, 0f);
        [SerializeField] private float slotSpacing = 150f;
        [Tooltip("Players this close to the entrance enter together.")]
        [SerializeField, Min(0.5f)] private float groupRadius = 3f;
        [Tooltip("How close a player must be to use a portal (the server allows a little more).")]
        [SerializeField, Min(0.5f)] private float portalRange = 1.3f;
        [SerializeField, Min(0f)] private float closeAfterEmptySeconds = 20f;

        private const float CheckInterval = 0.5f;
        private const float RangeTolerance = 0.4f;

        private readonly List<DungeonInstance> instances = new List<DungeonInstance>();
        private float nextCheck;

        public static DungeonManager Instance { get; private set; }

        /// <summary>The first dungeon (the one entrances without a dungeon of their own open).</summary>
        public DungeonDefinition Definition => dungeons.Length > 0 ? dungeons[0] : null;
        public IReadOnlyList<DungeonDefinition> Dungeons => dungeons;
        public float PortalRange => portalRange;
        public IReadOnlyList<DungeonInstance> Instances => instances;

        public float CloseAfterEmptySeconds
        {
            get => closeAfterEmptySeconds;
            set => closeAfterEmptySeconds = Mathf.Max(0f, value);
        }

        /// <summary>Sets the dungeons (used by the scene builder and tests).</summary>
        public void Configure(params DungeonDefinition[] definitions)
        {
            dungeons = definitions ?? System.Array.Empty<DungeonDefinition>();
        }

        public DungeonDefinition DungeonAt(int index) => index >= 0 && index < dungeons.Length ? dungeons[index] : null;

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Everyone ----------

        /// <summary>Builds the tilemaps, navigation, exits and doors of an instance at its slot. Returns the root object.</summary>
        public GameObject BuildLayout(DungeonInstance instance, out Grid grid)
        {
            DungeonDefinition definition = instance.Definition;
            var root = new GameObject($"Dungeon {instance.Slot} {(definition != null ? definition.displayName : "")}");
            root.transform.position = firstSlotOrigin + Vector2.right * (slotSpacing * Mathf.Max(0, instance.Slot));

            grid = root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);

            Tilemap floor = CreateTilemap(grid, "Floor", -100, TilemapRenderer.Mode.Chunk);
            Tilemap walls = CreateTilemap(grid, "Walls", 0, TilemapRenderer.Mode.Individual);

            if (definition != null)
            {
                DungeonLayout layout = definition.Layout;
                List<Vector3Int> floorCells = layout.FloorCells();
                List<Vector3Int> wallCells = layout.WallCells();
                Fill(floor, floorCells, definition.floorTile);
                Fill(floor, wallCells, definition.floorTile);
                Fill(walls, wallCells, definition.wallTile);
            }
            walls.gameObject.AddComponent<TilemapCollider2D>();
            var navigation = root.AddComponent<WorldNavigation>();
            navigation.Configure(grid, floor, walls);
            if (definition == null) return root;
            navigation.ZoneName = definition.displayName;

            foreach (DungeonExit exit in definition.exits)
            {
                Portal portal = CreatePortal(root.transform, grid, exit.cell, definition, "Exit", out SpriteRenderer visual);
                portal.ConfigureExit(visual, instance, exit.destination, exit.label);
            }
            foreach (DungeonDoor door in definition.doors)
            {
                if (door.target == null) continue;
                Portal portal = CreatePortal(root.transform, grid, door.cell, definition, door.target.displayName, out SpriteRenderer visual);
                portal.ConfigureEntrance(visual, instance, door.target);
            }
            return root;
        }

        private static void Fill(Tilemap tilemap, List<Vector3Int> cells, TileBase tile)
        {
            var tiles = new TileBase[cells.Count];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            tilemap.SetTiles(cells.ToArray(), tiles);
        }

        private static Tilemap CreateTilemap(Grid grid, string tilemapName, int sortingOrder, TilemapRenderer.Mode mode)
        {
            var go = new GameObject(tilemapName);
            go.transform.SetParent(grid.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            var tilemapRenderer = go.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = sortingOrder;
            tilemapRenderer.mode = mode;
            return tilemap;
        }

        private static Portal CreatePortal(Transform parent, Grid grid, Vector3Int cell, DungeonDefinition definition, string portalName,
            out SpriteRenderer visual)
        {
            var portalObject = new GameObject(portalName);
            portalObject.transform.SetParent(parent, false);
            portalObject.transform.position = grid.GetCellCenterWorld(cell);

            visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(portalObject.transform, false);
            visual.sprite = definition.portalSprite;
            visual.spriteSortPoint = SpriteSortPoint.Pivot;

            var box = portalObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.6f, 0.9f);
            box.offset = new Vector2(0f, 0.45f);
            return portalObject.AddComponent<Portal>();
        }

        // ---------- Server ----------

        private NetworkManager Network => NetworkManager.Singleton;

        /// <summary>Server: uses the open portal the player stands next to. False when there is none.</summary>
        public bool ServerUsePortal(Health player)
        {
            if (Network == null || !Network.IsServer || player == null || player.IsDead) return false;

            Vector2 position = AutoAttack.ServerPosition(player);
            Portal nearest = null;
            float nearestDistance = portalRange + RangeTolerance;
            foreach (Portal portal in Portal.Portals)
            {
                float distance = Vector2.Distance(position, portal.transform.position);
                if (distance <= nearestDistance && portal.IsOpen)
                {
                    nearest = portal;
                    nearestDistance = distance;
                }
            }
            if (nearest == null) return false;

            if (nearest.Kind == PortalKind.DungeonEntrance)
                return ServerEnter(nearest.transform.position, player, nearest.Dungeon != null ? nearest.Dungeon : Definition) != null;
            return ServerLeave(player, nearest);
        }

        /// <summary>
        /// Why a player may not open or join a dungeon ("You must be level 25..."), or null when they may.
        /// <paramref name="needsKey"/>: whether they must also carry the key (only the one who opens it does).
        /// </summary>
        public string EntryRefusal(Health player, bool needsKey, DungeonDefinition dungeon = null)
        {
            dungeon = dungeon != null ? dungeon : Definition;
            if (dungeon == null || player == null) return null;
            int level = player.TryGetComponent(out CharacterProgress progress) ? progress.Level : 1;
            if (level < dungeon.minLevel)
                return $"You must be level {dungeon.minLevel} to enter {dungeon.displayName}";
            if (needsKey && dungeon.key != null && (!player.TryGetComponent(out Inventory inventory) || inventory.CountOf(dungeon.key) <= 0))
                return $"You need {Article(dungeon.key.displayName)} to enter {dungeon.displayName}";
            return null;
        }

        // "a Tower Key", but "Keys to the Barracks".
        private static string Article(string itemName) =>
            itemName.StartsWith("Keys ") ? itemName : ("AEIOU".IndexOf(itemName[0]) >= 0 ? "an " : "a ") + itemName;

        /// <summary>
        /// Server: <paramref name="opener"/> opens a new instance of <paramref name="dungeon"/> (the first one when
        /// null) at an entrance, if they meet the level and carry the key (used up). Living players around the
        /// entrance who meet the level go in with them, up to the limit. Returns the instance (null when refused).
        /// </summary>
        public DungeonInstance ServerEnter(Vector2 entrance, Health opener, DungeonDefinition dungeon = null)
        {
            dungeon = dungeon != null ? dungeon : Definition;
            if (dungeon == null || opener == null) return null;
            string refusal = EntryRefusal(opener, true, dungeon);
            if (refusal != null)
            {
                Announce(opener, refusal);
                return null;
            }

            var group = new List<Health> { opener };
            foreach (Health player in PlayersAround(entrance))
            {
                if (player == opener) continue;
                string reason = EntryRefusal(player, false, dungeon);
                if (reason != null) Announce(player, reason);
                else if (group.Count < dungeon.maxPlayers) group.Add(player);
            }

            if (dungeon.key != null && opener.TryGetComponent(out Inventory inventory) && !inventory.ServerRemoveItem(dungeon.key, 1))
                return null;
            return Open(dungeon, group);
        }

        /// <summary>Server: opens a new instance for the living players around an entrance, without entry checks (tests, tools).</summary>
        public DungeonInstance ServerEnter(Vector2 entrance)
        {
            return Open(Definition, PlayersAround(entrance));
        }

        private List<Health> PlayersAround(Vector2 entrance)
        {
            var players = new List<Health>();
            foreach (NetworkClient client in Network.ConnectedClientsList)
            {
                if (client.PlayerObject == null || !client.PlayerObject.TryGetComponent(out Health player)) continue;
                if (!player.IsDead && Vector2.Distance(AutoAttack.ServerPosition(player), entrance) <= groupRadius)
                    players.Add(player);
            }
            return players;
        }

        private DungeonInstance Open(DungeonDefinition dungeon, List<Health> group)
        {
            int index = System.Array.IndexOf(dungeons, dungeon);
            if (dungeon == null || index < 0 || dungeon.instancePrefab == null || group.Count == 0) return null;

            GameObject instanceObject = Instantiate(dungeon.instancePrefab);
            var instance = instanceObject.GetComponent<DungeonInstance>();
            instance.ServerPrepare(FreeSlot(), index);
            instanceObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            instances.Add(instance);

            SpawnMonsters(instance, dungeon);
            SpawnChests(instance, dungeon);
            Vector3Int arrivalCell = dungeon.Layout.NearestFloor(dungeon.arrival);
            for (int i = 0; i < group.Count; i++)
            {
                Vector2 arrival = instance.CellCenter(arrivalCell) + new Vector2((i % 3 - 1) * 0.35f, (i / 3) * 0.2f);
                Teleport(group[i], arrival, $"Entering {dungeon.displayName}");
            }
            Debug.Log($"[Dungeon] {dungeon.displayName} opened in slot {instance.Slot} for {group.Count} player(s).");
            return instance;
        }

        /// <summary>
        /// Server: sends a player in a dungeon out through <paramref name="exit"/> (to where it leads), or back to the
        /// dungeon's entrance in the world when no exit is given.
        /// </summary>
        public bool ServerLeave(Health player, Portal exit = null)
        {
            Vector2 position = AutoAttack.ServerPosition(player);
            DungeonInstance instance = InstanceAt(position);
            if (instance == null) return false;

            Vector2 destination;
            if (exit != null && exit.HasDestination) destination = exit.Destination;
            else
            {
                Portal entrance = WorldEntranceOf(instance.Definition);
                destination = entrance != null ? (Vector2)entrance.transform.position + new Vector2(0f, -0.8f) : Vector2.zero;
            }
            Teleport(player, destination, "Back in the world");
            return true;
        }

        /// <summary>
        /// Where a player inside a dungeon at <paramref name="position"/> would come back to in the world (its entrance);
        /// false when the position is in no dungeon. Saved characters use it, so nobody logs back into another group's run.
        /// </summary>
        public bool TryGetWorldReturn(Vector2 position, out Vector2 destination)
        {
            destination = position;
            DungeonInstance instance = InstanceAt(position);
            if (instance == null) return false;
            Portal entrance = WorldEntranceOf(instance.Definition);
            if (entrance == null) return false;
            destination = (Vector2)entrance.transform.position + new Vector2(0f, -0.8f);
            return true;
        }

        /// <summary>
        /// The entrance in the world that opens <paramref name="dungeon"/>; for a dungeon entered through a door inside
        /// another (Corvalis's Descent, Mirethane's Sanctuary) the outer dungeon's entrance; any world entrance only when
        /// there is none at all.
        /// </summary>
        private Portal WorldEntranceOf(DungeonDefinition dungeon)
        {
            for (int depth = 0; dungeon != null && depth < 8; depth++)
            {
                Portal own = DirectWorldEntrance(dungeon);
                if (own != null) return own;
                dungeon = OuterDungeonOf(dungeon);
            }
            foreach (Portal portal in Portal.Portals)
                if (portal.Kind == PortalKind.DungeonEntrance && portal.Instance == null && InstanceAt(portal.transform.position) == null)
                    return portal;
            return null;
        }

        private Portal DirectWorldEntrance(DungeonDefinition dungeon)
        {
            foreach (Portal portal in Portal.Portals)
            {
                if (portal.Kind != PortalKind.DungeonEntrance || portal.Instance != null || InstanceAt(portal.transform.position) != null) continue;
                if (portal.Dungeon == dungeon) return portal;
            }
            return null;
        }

        /// <summary>The dungeon with a door into <paramref name="dungeon"/>, or null.</summary>
        private DungeonDefinition OuterDungeonOf(DungeonDefinition dungeon)
        {
            foreach (DungeonDefinition outer in dungeons)
            {
                if (outer == null || outer.doors == null) continue;
                foreach (DungeonDoor door in outer.doors)
                    if (door.target == dungeon) return outer;
            }
            return null;
        }

        /// <summary>The instance whose area contains a position (null in the world).</summary>
        public DungeonInstance InstanceAt(Vector2 position)
        {
            foreach (DungeonInstance instance in instances)
                if (instance != null && instance.Contains(position)) return instance;
            return null;
        }

        private int FreeSlot()
        {
            int candidate = 0;
            while (instances.Exists(i => i != null && i.IsSpawned && i.Slot == candidate)) candidate++;
            return candidate;
        }

        private void SpawnMonsters(DungeonInstance instance, DungeonDefinition dungeon)
        {
            DungeonLayout layout = dungeon.Layout;
            foreach (DungeonMonsterSpot spot in dungeon.monsters)
            {
                if (spot.prefab == null) continue;
                NetworkObject monster = SpawnMonster(instance, dungeon, spot, instance.CellCenter(layout.NearestFloor(spot.cell)));
                if (!spot.boss) continue;
                instance.BossesLeft++;
                monster.GetComponent<Health>().Died += instance.ServerBossDied;
            }
        }

        /// <summary>The health of a normal monster of <paramref name="level"/> (what a spot's health scale multiplies).</summary>
        public static int NormalHealthAt(DungeonDefinition dungeon, GameObject fallback, int level)
        {
            GameObject reference = dungeon.referenceMonster != null ? dungeon.referenceMonster : fallback;
            if (reference == null || !reference.TryGetComponent(out Health health) || !reference.TryGetComponent(out MonsterAI ai)) return 1;
            return Mathf.Max(1, Mathf.RoundToInt(health.BaseMax * MonsterAI.HealthScaleBetween(ai.Level, level)));
        }

        private NetworkObject SpawnMonster(DungeonInstance instance, DungeonDefinition dungeon, DungeonMonsterSpot spot, Vector2 point)
        {
            GameObject monster = Instantiate(spot.prefab, point, Quaternion.identity);
            var ai = monster.GetComponent<MonsterAI>();
            var health = monster.GetComponent<Health>();
            var attack = monster.GetComponent<AutoAttack>();
            if (ai != null)
            {
                ai.Home = point;
                ai.AggroRadius = dungeon.monsterAggroRadius;
                ai.ServerOverrideLevel(spot.level);
                if (spot.loot != null && spot.loot.Length > 0) ai.ServerAddBonusLoot(spot.loot);
                if (spot.spells != null && spot.spells.Length > 0 && monster.TryGetComponent(out MonsterCaster caster)) caster.Spells = spot.spells;
                if (spot.blueChance > 0f || spot.goldChance > 0f || spot.purpleChance > 0f)
                    ai.ConfigureQualityDrops(spot.blueChance, spot.goldChance, spot.purpleChance);
                if (spot.goldScale > 0f)
                    ai.GoldReward = new Vector2Int(Mathf.RoundToInt(ai.GoldReward.x * spot.goldScale), Mathf.RoundToInt(ai.GoldReward.y * spot.goldScale));
            }
            var networkObject = monster.GetComponent<NetworkObject>();
            networkObject.Spawn(destroyWithScene: true);

            if (ai != null && spot.elite) ai.ServerMakeElite();
            if (health != null)
            {
                if (spot.controlImmune && health.Effects != null) health.Effects.ControlImmune = true;
                // The source game's own health for a monster: a multiple of a normal monster of its level.
                if (spot.healthScale > 0f)
                    health.ServerSetMaxHealth(Mathf.Max(1, Mathf.RoundToInt(NormalHealthAt(dungeon, spot.prefab, spot.level) * spot.healthScale)), true);
                if (!string.IsNullOrEmpty(spot.displayName)) health.ServerSetDisplayName(spot.displayName);
            }
            if (attack != null && spot.damageScale > 0f)
                attack.DamageScale *= spot.damageScale / (spot.elite ? MonsterAI.EliteDamageScale : 1f);
            instance.Monsters.Add(networkObject);
            return networkObject;
        }

        private void SpawnChests(DungeonInstance instance, DungeonDefinition dungeon)
        {
            DungeonLayout layout = dungeon.Layout;
            foreach (DungeonChestSpot spot in dungeon.chests)
            {
                if (spot.prefab == null) continue;
                GameObject chest = Instantiate(spot.prefab, instance.CellCenter(layout.NearestFloor(spot.cell)), Quaternion.identity);
                if (chest.TryGetComponent(out LootChest lootChest)) lootChest.ServerOverrideLevel(spot.level);
                var networkObject = chest.GetComponent<NetworkObject>();
                networkObject.Spawn(destroyWithScene: true);
                instance.Chests.Add(networkObject);
            }
        }

        private static void Teleport(Health player, Vector2 destination, string message)
        {
            if (player.TryGetComponent(out PlayerNetworkMovement movement)) movement.ServerTeleport(destination);
            Announce(player, message);
        }

        private static void Announce(Health player, string message)
        {
            if (player.TryGetComponent(out DungeonTraveller traveller)) traveller.ServerAnnounce(message);
        }

        private void Update()
        {
            if (Network == null || !Network.IsServer || !Network.IsListening)
            {
                instances.Clear();
                return;
            }
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckInterval;

            for (int i = instances.Count - 1; i >= 0; i--)
            {
                DungeonInstance instance = instances[i];
                if (instance == null || !instance.IsSpawned)
                {
                    instances.RemoveAt(i);
                    continue;
                }

                if (HasPlayersInside(instance))
                {
                    instance.EmptySince = -1f;
                    continue;
                }
                if (instance.EmptySince < 0f) instance.EmptySince = Time.time;
                if (Time.time - instance.EmptySince >= closeAfterEmptySeconds)
                {
                    Close(instance);
                    instances.RemoveAt(i);
                }
            }
        }

        private bool HasPlayersInside(DungeonInstance instance)
        {
            foreach (NetworkClient client in Network.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if (instance.Contains(AutoAttack.ServerPosition(client.PlayerObject))) return true;
            }
            return false;
        }

        private void Close(DungeonInstance instance)
        {
            foreach (NetworkObject monster in instance.Monsters)
                if (monster != null && monster.IsSpawned) monster.Despawn(true);
            foreach (NetworkObject chest in instance.Chests)
                if (chest != null && chest.IsSpawned) chest.Despawn(true);

            foreach (LootBag loot in FindObjectsByType<LootBag>())
                if (loot.IsSpawned && instance.Contains(loot.transform.position)) loot.NetworkObject.Despawn(true);

            Debug.Log($"[Dungeon] Slot {instance.Slot} closed (empty).");
            instance.NetworkObject.Despawn(true);
        }
    }
}
