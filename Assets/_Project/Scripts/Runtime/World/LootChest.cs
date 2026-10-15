using System.Collections.Generic;
using RPG.Combat;
using RPG.Items;
using Unity.Netcode;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// A crate, chest or strongbox in the world. A player who walks up and clicks it gets its gold, and its loot
    /// is left in a loot bag; the chest then disappears until its <see cref="ChestSpawner"/> brings it back.
    /// The server rolls everything.
    /// </summary>
    public class LootChest : NetworkBehaviour
    {
        [SerializeField] private string displayName = "Wooden Crate";
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField] private LootEntry[] loot = System.Array.Empty<LootEntry>();
        [SerializeField] private Vector2Int gold = new Vector2Int(2, 6);
        [SerializeField] private GameObject lootBagPrefab;
        [SerializeField] private AffixCatalog affixes;
        [Tooltip("Gear dropped by level, around the chest's level.")]
        [SerializeField] private ItemCatalog gear;
        [Tooltip("Times the gear chance is rolled when it is opened.")]
        [SerializeField, Min(0)] private int gearRolls = 2;
        [SerializeField, Range(0f, 1f)] private float socketChance = 0.25f;
        [SerializeField, Min(0.3f)] private float openRange = 1.2f;

        private static readonly List<LootChest> Chests = new List<LootChest>();
        private readonly NetworkVariable<int> networkLevel = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private bool opened;

        /// <summary>Every spawned chest (on every peer).</summary>
        public static IReadOnlyList<LootChest> All => Chests;

        public string DisplayName => displayName;
        public int Level => IsSpawned && networkLevel.Value > 0 ? networkLevel.Value : level;
        public float OpenRange => openRange;

        /// <summary>"Oak Chest Lv4", as shown above it.</summary>
        public string Label => $"{displayName} Lv{Level}";

        /// <summary>Sets what the chest holds (used by the scene builder).</summary>
        public void Configure(string chestName, int chestLevel, LootEntry[] entries, Vector2Int goldRange, GameObject bagPrefab,
            AffixCatalog affixCatalog, ItemCatalog gearCatalog = null, int gearRollCount = 2)
        {
            displayName = chestName;
            level = Mathf.Max(1, chestLevel);
            loot = entries ?? System.Array.Empty<LootEntry>();
            gold = new Vector2Int(Mathf.Max(0, goldRange.x), Mathf.Max(goldRange.x, goldRange.y));
            lootBagPrefab = bagPrefab;
            affixes = affixCatalog;
            gear = gearCatalog;
            gearRolls = Mathf.Max(0, gearRollCount);
        }

        /// <summary>Server, before spawning: the level of this spot's chest (its gear drops come from around it).</summary>
        public void ServerOverrideLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, 25);
        }

        public override void OnNetworkSpawn()
        {
            Chests.Add(this);
            if (IsServer) networkLevel.Value = level;
        }

        public override void OnNetworkDespawn() => Chests.Remove(this);

        /// <summary>Server: opens the chest for a player standing next to it. False when it is already open or too far.</summary>
        public bool ServerOpen(Health opener)
        {
            if (!IsServer || opened || opener == null || opener.IsDead) return false;
            float distance = Vector2.Distance(AutoAttack.ServerPosition(opener), transform.position);
            if (distance > openRange + 0.4f) return false;

            opened = true;
            if (gold.y > 0 && opener.TryGetComponent(out Inventory inventory))
                inventory.ServerAddGold(Random.Range(gold.x, gold.y + 1));
            // The table once (junk, potions, gems), the gear chance several times.
            LootDrop.Spawn(lootBagPrefab, transform.position + new Vector3(0f, -0.25f, 0f), loot, gear, affixes, level, socketChance,
                1, Mathf.Max(0, gearRolls - 1));
            NetworkObject.Despawn(true);
            return true;
        }
    }
}
