using System.Collections;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Items;
using RPG.Progression;
using Unity.Netcode;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// A crate, barrel or chest in the world, a source-game container: crates only hold gold; chests are locked (pick
    /// the lock with Lockpicking of at least twice the chest's level, which takes <see cref="PickSeconds"/>) and hold
    /// a piece of gear that may come out blue, gold or purple, often a potion and a gem, and more gold. The player
    /// who opens it gets the gold and a loot window with the rest; the chest then disappears until its
    /// <see cref="ChestSpawner"/> brings it back. The server rolls everything.
    /// </summary>
    public class LootChest : NetworkBehaviour
    {
        /// <summary>How long picking a lock takes (the source game's Pick Lock: 2 seconds).</summary>
        public const float PickSeconds = 2f;

        /// <summary>Lockpicking needed per level of a locked chest (the source game's "Lv2xForLockpick").</summary>
        public const int LockpickingPerLevel = 2;

        [SerializeField] private string displayName = "Wooden Crate";
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField] private GameObject lootBagPrefab;
        [SerializeField] private AffixCatalog affixes;
        [Tooltip("The gear a chest's piece is picked from, around the chest's level.")]
        [SerializeField] private ItemCatalog gear;
        [SerializeField, Min(0.3f)] private float openRange = 1.2f;

        [Header("Source-game container")]
        [Tooltip("Needs Lockpicking of twice its level to open.")]
        [SerializeField] private bool locked;
        [Tooltip("Chance (%) of a piece of gear (green), and of it being blue, gold or purple instead.")]
        [SerializeField, Range(0f, 100f)] private float greenChance;
        [SerializeField, Range(0f, 100f)] private float blueChance;
        [SerializeField, Range(0f, 100f)] private float goldChance;
        [SerializeField, Range(0f, 100f)] private float purpleChance;
        [Tooltip("Pieces of gear it always holds on top.")]
        [SerializeField, Min(0)] private int assuredGear;
        [SerializeField, Range(0f, 100f)] private float potionChance;
        [SerializeField, Range(0f, 100f)] private float gemChance;
        [Tooltip("Gold: level x 2-4, times this.")]
        [SerializeField, Min(0f)] private float goldRatio = 1f;
        [Tooltip("The first potions (life and mana); a chest gives the tier of its level.")]
        [SerializeField] private ItemDefinition[] potions = System.Array.Empty<ItemDefinition>();
        [SerializeField] private ItemDefinition[] gems = System.Array.Empty<ItemDefinition>();

        private static readonly List<LootChest> Chests = new List<LootChest>();
        private readonly NetworkVariable<int> networkLevel = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly List<LootItem> rolled = new List<LootItem>();
        private bool opened;
        private Health picker;

        /// <summary>Every spawned chest (on every peer).</summary>
        public static ListView<LootChest> All => new ListView<LootChest>(Chests);

        public string DisplayName => displayName;
        public int Level => IsSpawned && networkLevel.Value > 0 ? networkLevel.Value : level;
        public float OpenRange => openRange;
        public bool Locked => locked;

        /// <summary>The Lockpicking a player needs to open it (0 when it is not locked).</summary>
        public int RequiredLockpicking => locked ? Level * LockpickingPerLevel : 0;

        /// <summary>"Oak Chest Lv4", as shown above it.</summary>
        public string Label => $"{displayName} Lv{Level}";

        /// <summary>Sets what the chest is (used by the scene builder): a crate with gold only unless locked with more.</summary>
        public void Configure(string chestName, int chestLevel, GameObject bagPrefab, AffixCatalog affixCatalog, ItemCatalog gearCatalog,
            float gold = 1f)
        {
            displayName = chestName;
            level = Mathf.Max(1, chestLevel);
            lootBagPrefab = bagPrefab;
            affixes = affixCatalog;
            gear = gearCatalog;
            goldRatio = Mathf.Max(0f, gold);
        }

        /// <summary>Makes it a locked source-game chest with these chances (in %), potions and gems.</summary>
        public void ConfigureLocked(float green, float blue, float goldQuality, float purple, int assured, float potion, float gem,
            ItemDefinition[] potionItems, ItemDefinition[] gemItems)
        {
            locked = true;
            greenChance = green;
            blueChance = blue;
            goldChance = goldQuality;
            purpleChance = purple;
            assuredGear = Mathf.Max(0, assured);
            potionChance = potion;
            gemChance = gem;
            potions = potionItems ?? System.Array.Empty<ItemDefinition>();
            gems = gemItems ?? System.Array.Empty<ItemDefinition>();
        }

        /// <summary>Server, before spawning: the level of this spot's chest (its gear comes from around it).</summary>
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

        private bool InReach(Health opener) =>
            opener != null && !opener.IsDead && Vector2.Distance(AutoAttack.ServerPosition(opener), transform.position) <= openRange + 0.4f;

        /// <summary>
        /// Server: a player standing next to it opens it. A locked chest first needs their Lockpicking and
        /// <see cref="PickSeconds"/> of picking (it opens on its own then, if they stayed). False when it is already
        /// open, too far, or their Lockpicking is too low.
        /// </summary>
        public bool ServerOpen(Health opener)
        {
            if (!IsServer || opened || !InReach(opener)) return false;
            opener.TryGetComponent(out Inventory inventory);
            if (!locked)
            {
                Open(inventory);
                return true;
            }

            int lockpicking = opener.TryGetComponent(out CharacterProgress progress) ? progress.TotalAttributes.lockpicking : 0;
            if (lockpicking < RequiredLockpicking)
            {
                if (inventory != null) inventory.ServerLockMessage(false, RequiredLockpicking, lockpicking);
                return false;
            }
            if (picker != null) return picker == opener;
            picker = opener;
            if (inventory != null) inventory.ServerLockMessage(true, RequiredLockpicking, lockpicking);
            StartCoroutine(PickLock(opener, inventory));
            return true;
        }

        private IEnumerator PickLock(Health opener, Inventory inventory)
        {
            yield return new WaitForSeconds(PickSeconds);
            // Walking away or dying breaks it off.
            if (!opened && IsSpawned && InReach(opener)) Open(inventory);
            picker = null;
        }

        private void Open(Inventory inventory)
        {
            opened = true;
            int gold = Mathf.RoundToInt(Random.Range(level * 2, level * 4 + 1) * goldRatio);
            if (gold > 0 && inventory != null) inventory.ServerAddGold(gold);

            RollContents();
            LootBag bag = LootDrop.SpawnBag(lootBagPrefab, transform.position + new Vector3(0f, -0.25f, 0f), rolled);
            // The opener sees what is inside right away.
            if (bag != null && inventory != null) inventory.ServerShowLoot(bag);
            NetworkObject.Despawn(true);
        }

        /// <summary>Server: what the chest holds this time.</summary>
        private void RollContents()
        {
            rolled.Clear();
            int pieces = assuredGear + (Random.value * 100f < greenChance ? 1 : 0);
            for (int i = 0; i < pieces; i++)
            {
                // Like the source game's container chances: green, sometimes blue, gold or (rarely) purple instead.
                float roll = Random.value * 100f;
                ItemRarity quality = roll < purpleChance ? ItemRarity.Legendary
                    : roll < purpleChance + goldChance ? ItemRarity.Epic
                    : roll < purpleChance + goldChance + blueChance ? ItemRarity.Rare
                    : ItemRarity.Uncommon;
                if (LootDrop.PickGear(gear, affixes, level, quality, out LootItem piece)) rolled.Add(piece);
            }
            if (potions.Length > 0 && Random.value * 100f < potionChance)
                rolled.Add(new LootItem(LootDrop.PotionForLevel(potions[Random.Range(0, potions.Length)], level), 0));
            if (gems.Length > 0 && Random.value * 100f < gemChance)
            {
                ItemDefinition gem = gems[Random.Range(0, gems.Length)];
                // Higher chests hold better gems.
                if (gem != null && level >= 15 && gem.combinesInto != null) gem = gem.combinesInto;
                if (gem != null) rolled.Add(new LootItem(gem, 0));
            }
        }
    }
}
