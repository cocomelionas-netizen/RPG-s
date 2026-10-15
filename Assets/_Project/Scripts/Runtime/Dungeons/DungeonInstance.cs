using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Dungeons
{
    /// <summary>
    /// One running copy of a dungeon for one group. The server spawns it in a free slot far from the world;
    /// every peer then builds the same tilemaps there (<see cref="DungeonManager.BuildLayout"/>) and removes them
    /// when it is despawned. The server also keeps track of the monsters and containers it put inside.
    /// </summary>
    public class DungeonInstance : NetworkBehaviour
    {
        private readonly NetworkVariable<int> slot = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> dungeonIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> bossDefeated = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private int pendingSlot = -1;
        private int pendingDungeon;
        private GameObject layout;
        private Grid grid;
        private readonly List<NetworkObject> monsters = new List<NetworkObject>();
        private readonly List<NetworkObject> chests = new List<NetworkObject>();

        public int Slot => slot.Value;
        public bool BossDefeated => bossDefeated.Value;
        public Grid Grid => grid;

        /// <summary>Which of the manager's dungeons this is a copy of.</summary>
        public int DungeonIndex => dungeonIndex.Value;

        public DungeonDefinition Definition =>
            DungeonManager.Instance != null ? DungeonManager.Instance.DungeonAt(dungeonIndex.Value) : null;

        /// <summary>Server only: monsters spawned in this instance.</summary>
        public List<NetworkObject> Monsters => monsters;

        /// <summary>Server only: containers spawned in this instance.</summary>
        public List<NetworkObject> Chests => chests;

        /// <summary>Server only: bosses still alive.</summary>
        public int BossesLeft { get; set; }

        /// <summary>Server only: time since the last player was inside (-1 while someone is).</summary>
        public float EmptySince { get; set; } = -1f;

        /// <summary>Server: chooses the slot and the dungeon before spawning.</summary>
        public void ServerPrepare(int freeSlot, int dungeon)
        {
            pendingSlot = freeSlot;
            pendingDungeon = dungeon;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                slot.Value = pendingSlot;
                dungeonIndex.Value = pendingDungeon;
            }

            DungeonManager manager = DungeonManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[Dungeon] No DungeonManager in the scene; cannot build the dungeon.");
                return;
            }
            layout = manager.BuildLayout(this, out grid);
        }

        public override void OnNetworkDespawn()
        {
            if (layout != null) Destroy(layout);
            layout = null;
            grid = null;
        }

        /// <summary>Server: one of its bosses died; the dungeon is beaten when none is left.</summary>
        public void ServerBossDied()
        {
            if (!IsServer) return;
            BossesLeft = Mathf.Max(0, BossesLeft - 1);
            if (BossesLeft == 0) bossDefeated.Value = true;
        }

        /// <summary>World position of a layout cell's center.</summary>
        public Vector2 CellCenter(Vector3Int cell)
        {
            return grid != null ? (Vector2)grid.GetCellCenterWorld(cell) : (Vector2)transform.position;
        }

        /// <summary>True when a position is inside this dungeon's area.</summary>
        public bool Contains(Vector2 worldPosition)
        {
            if (grid == null) return false;
            Vector3Int cell = grid.WorldToCell(worldPosition);
            BoundsInt bounds = DungeonLayout.For(Definition).Bounds();
            return cell.x >= bounds.xMin && cell.x < bounds.xMax && cell.y >= bounds.yMin && cell.y < bounds.yMax;
        }
    }
}
