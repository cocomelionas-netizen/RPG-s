using System.Collections.Generic;
using RPG.Combat;
using RPG.Items;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.Dungeons
{
    /// <summary>
    /// Runs dungeon instances. One lives in the world scene on every peer.
    /// - Everyone: builds an instance's tilemaps, navigation and exit portals when it spawns.
    /// - Server: when a player uses the entrance, every living player standing around it enters a new instance
    ///   in a free slot far from the world (each group gets its own copy). Fills it with monster packs and the boss,
    ///   sends players back to the entrance through the exits, and closes instances that stayed empty for a while.
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        [SerializeField] private DungeonDefinition definition;
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

        public DungeonDefinition Definition => definition;
        public float PortalRange => portalRange;
        public IReadOnlyList<DungeonInstance> Instances => instances;

        public float CloseAfterEmptySeconds
        {
            get => closeAfterEmptySeconds;
            set => closeAfterEmptySeconds = Mathf.Max(0f, value);
        }

        /// <summary>Sets the dungeon (used by the scene builder and tests).</summary>
        public void Configure(DungeonDefinition dungeon)
        {
            definition = dungeon;
        }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Everyone ----------

        /// <summary>Builds the tilemaps, navigation and exits of an instance at its slot. Returns the root object.</summary>
        public GameObject BuildLayout(DungeonInstance instance, out Grid grid)
        {
            var root = new GameObject($"Dungeon {instance.Slot}");
            root.transform.position = firstSlotOrigin + Vector2.right * (slotSpacing * Mathf.Max(0, instance.Slot));

            grid = root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);

            Tilemap floor = CreateTilemap(grid, "Floor", -100, TilemapRenderer.Mode.Chunk);
            Tilemap walls = CreateTilemap(grid, "Walls", 0, TilemapRenderer.Mode.Individual);

            if (definition != null)
            {
                foreach (Vector3Int cell in DungeonLayout.FloorCells())
                    floor.SetTile(cell, definition.floorTile);
                foreach (Vector3Int cell in DungeonLayout.WallCells())
                {
                    floor.SetTile(cell, definition.floorTile);
                    walls.SetTile(cell, definition.wallTile);
                }
            }
            walls.gameObject.AddComponent<TilemapCollider2D>();
            root.AddComponent<WorldNavigation>().Configure(grid, floor, walls);

            CreateExit(root.transform, grid, DungeonLayout.EntranceExit, instance, false);
            CreateExit(root.transform, grid, DungeonLayout.BossExit, instance, true);
            return root;
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

        private void CreateExit(Transform parent, Grid grid, Vector3Int cell, DungeonInstance instance, bool needsBoss)
        {
            var exit = new GameObject(needsBoss ? "Boss Exit" : "Exit");
            exit.transform.SetParent(parent, false);
            exit.transform.position = grid.GetCellCenterWorld(cell);

            var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(exit.transform, false);
            visual.sprite = definition != null ? definition.portalSprite : null;
            visual.spriteSortPoint = SpriteSortPoint.Pivot;

            var box = exit.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.6f, 0.9f);
            box.offset = new Vector2(0f, 0.45f);

            exit.AddComponent<Portal>().Configure(PortalKind.DungeonExit, visual, instance, needsBoss);
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
                return ServerEnter(nearest.transform.position) != null;
            return ServerLeave(player);
        }

        /// <summary>Server: opens a new instance for the living players around an entrance. Returns it (null if nobody).</summary>
        public DungeonInstance ServerEnter(Vector2 entrance)
        {
            if (definition == null || definition.instancePrefab == null) return null;

            var group = new List<Health>();
            foreach (NetworkClient client in Network.ConnectedClientsList)
            {
                if (client.PlayerObject == null || !client.PlayerObject.TryGetComponent(out Health player)) continue;
                if (!player.IsDead && Vector2.Distance(AutoAttack.ServerPosition(player), entrance) <= groupRadius)
                    group.Add(player);
            }
            if (group.Count == 0) return null;

            GameObject instanceObject = Instantiate(definition.instancePrefab);
            var instance = instanceObject.GetComponent<DungeonInstance>();
            instance.ServerPrepare(FreeSlot());
            instanceObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            instances.Add(instance);

            SpawnMonsters(instance);
            for (int i = 0; i < group.Count; i++)
            {
                Vector2 arrival = instance.CellCenter(DungeonLayout.Arrival) + new Vector2((i % 3 - 1) * 0.35f, (i / 3) * 0.2f);
                Teleport(group[i], arrival, $"Entering {definition.displayName}");
            }
            Debug.Log($"[Dungeon] {definition.displayName} opened in slot {instance.Slot} for {group.Count} player(s).");
            return instance;
        }

        /// <summary>Server: sends a player in a dungeon back to the entrance.</summary>
        public bool ServerLeave(Health player)
        {
            Vector2 position = AutoAttack.ServerPosition(player);
            if (InstanceAt(position) == null) return false;

            Portal entrance = null;
            foreach (Portal portal in Portal.Portals)
                if (portal.Kind == PortalKind.DungeonEntrance) entrance = portal;

            Vector2 destination = entrance != null ? (Vector2)entrance.transform.position + new Vector2(0f, -0.8f) : Vector2.zero;
            Teleport(player, destination, "Back in the world");
            return true;
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

        private void SpawnMonsters(DungeonInstance instance)
        {
            foreach (MonsterPack pack in definition.packs)
            {
                if (pack.prefab == null) continue;
                Vector2 center = instance.CellCenter(DungeonLayout.RoomCenter(pack.room));
                for (int i = 0; i < pack.count; i++)
                {
                    float angle = i * Mathf.PI * 2f / Mathf.Max(1, pack.count);
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f) * (pack.count > 1 ? 0.8f : 0f);
                    SpawnMonster(instance, pack.prefab, center + offset);
                }
            }

            if (definition.bossPrefab != null)
            {
                Vector2 bossPoint = instance.CellCenter(DungeonLayout.RoomCenter(DungeonLayout.BossRoom));
                NetworkObject boss = SpawnMonster(instance, definition.bossPrefab, bossPoint);
                boss.GetComponent<Health>().Died += instance.ServerSetBossDefeated;
            }
        }

        private NetworkObject SpawnMonster(DungeonInstance instance, GameObject prefab, Vector2 point)
        {
            GameObject monster = Instantiate(prefab, point, Quaternion.identity);
            if (monster.TryGetComponent(out MonsterAI ai))
            {
                ai.Home = point;
                ai.AggroRadius = definition.monsterAggroRadius;
            }
            var networkObject = monster.GetComponent<NetworkObject>();
            networkObject.Spawn(destroyWithScene: true);
            instance.Monsters.Add(networkObject);
            return networkObject;
        }

        private static void Teleport(Health player, Vector2 destination, string message)
        {
            if (player.TryGetComponent(out PlayerNetworkMovement movement)) movement.ServerTeleport(destination);
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

            foreach (LootBag loot in FindObjectsByType<LootBag>())
                if (loot.IsSpawned && instance.Contains(loot.transform.position)) loot.NetworkObject.Despawn(true);

            Debug.Log($"[Dungeon] Slot {instance.Slot} closed (empty).");
            instance.NetworkObject.Despawn(true);
        }
    }
}
