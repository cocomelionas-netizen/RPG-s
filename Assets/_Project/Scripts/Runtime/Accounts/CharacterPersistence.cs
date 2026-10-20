using System.Collections.Generic;
using RPG.Characters;
using RPG.Combat;
using RPG.Items;
using RPG.Navigation;
using RPG.Networking;
using RPG.Progression;
using RPG.World;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Accounts
{
    /// <summary>
    /// Server: loads the player's saved character when it spawns and saves it again: shortly after anything
    /// important changes (level, items), every few seconds while playing (position), and when the player leaves.
    /// Players without an account (tests, servers without <see cref="AccountService"/>) are not saved.
    /// </summary>
    [RequireComponent(typeof(CharacterProgress), typeof(Inventory))]
    public class CharacterPersistence : NetworkBehaviour
    {
        [Tooltip("Saves at least this often while playing (position).")]
        [SerializeField, Min(1f)] private float autosaveSeconds = 15f;
        [Tooltip("Waits this long after a change before saving, so many quick changes are saved once.")]
        [SerializeField, Min(0f)] private float changeSaveDelay = 2f;

        private CharacterProgress progress;
        private Inventory inventory;
        private WaystoneTraveller waystones;
        private Spellbook spellbook;
        private Health health;
        private HeroAppearance appearance;
        private PlayerNetworkMovement networkMovement;
        private AccountService accounts;
        private AccountRecord account;
        private float nextAutosave;
        private float saveChangesAt = -1f;

        /// <summary>Server only: the account this character belongs to (null for guests).</summary>
        public AccountRecord Account => account;

        private void Awake()
        {
            progress = GetComponent<CharacterProgress>();
            inventory = GetComponent<Inventory>();
            waystones = GetComponent<WaystoneTraveller>();
            spellbook = GetComponent<Spellbook>();
            health = GetComponent<Health>();
            appearance = GetComponent<HeroAppearance>();
            networkMovement = GetComponent<PlayerNetworkMovement>();
        }

        protected override void OnNetworkPostSpawn()
        {
            // After the other components spawned and set their defaults.
            if (!IsServer) return;

            accounts = NetworkManager.GetComponent<AccountService>();
            if (accounts == null || !accounts.TryGetAccount(OwnerClientId, out account)) return;

            Load(account.character);
            progress.Changed += OnCharacterChanged;
            inventory.Changed += OnCharacterChanged;
            if (spellbook != null) spellbook.Changed += OnCharacterChanged;
            nextAutosave = Time.time + autosaveSeconds;
            ServerSave();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer || account == null) return;
            progress.Changed -= OnCharacterChanged;
            inventory.Changed -= OnCharacterChanged;
            if (spellbook != null) spellbook.Changed -= OnCharacterChanged;
            ServerSave();
            accounts.LogOut(OwnerClientId);
        }

        private void OnCharacterChanged()
        {
            if (saveChangesAt < 0f) saveChangesAt = Time.time + changeSaveDelay;
        }

        private void Update()
        {
            if (!IsServer || account == null) return;
            bool changesDue = saveChangesAt >= 0f && Time.time >= saveChangesAt;
            if (changesDue || Time.time >= nextAutosave) ServerSave();
        }

        private void Load(CharacterRecord saved)
        {
            health.ServerSetDisplayName(account.username);
            if (appearance != null) appearance.ServerSetFemale(saved.female);
            progress.ServerLoad(saved.classIndex, saved.level, saved.experience, saved.spentPoints, saved.experienceToSpend);
            if (!saved.hasBeenSaved) return;

            if (spellbook != null) spellbook.ServerLoad(saved.spellRanks, saved.spellBar);
            inventory.ServerLoad(ToStacks(saved.bag), ToStacks(saved.equipment));
            inventory.ServerSetGold(saved.gold);
            if (waystones != null) waystones.ServerLoad(saved.waystones);
            var position = new Vector2(saved.x, saved.y);
            // A spot on no map, or inside a dungeon (its slot may belong to another group by now), keeps the spawn point.
            WorldNavigation navigation = WorldNavigation.At(position);
            bool inDungeon = RPG.Dungeons.DungeonManager.Instance != null && RPG.Dungeons.DungeonManager.Instance.InstanceAt(position) != null;
            bool safe = navigation != null && navigation.IsWalkable(position) && !inDungeon;
            if (safe && networkMovement != null) networkMovement.ServerTeleport(position);
            Debug.Log($"[Accounts] Loaded {account.username}: level {saved.level} {progress.ClassName}.");
        }

        /// <summary>Server: writes the character to its account now.</summary>
        public void ServerSave()
        {
            if (!IsServer || account == null) return;

            CharacterRecord record = account.character;
            record.classIndex = progress.ClassIndex;
            if (appearance != null) record.female = appearance.IsFemale;
            record.level = progress.Level;
            record.experience = progress.Experience;
            record.gold = inventory.Gold;
            record.spentPoints = progress.SpentPoints;
            record.experienceToSpend = progress.ExperienceToSpend;
            record.bag = ToRecords(inventory.BagSnapshot());
            record.equipment = ToRecords(inventory.EquipmentSnapshot());
            if (waystones != null) record.waystones = waystones.DiscoveredMask;
            if (spellbook != null)
            {
                record.spellRanks = spellbook.RanksSnapshot();
                record.spellBar = spellbook.BarSnapshot();
            }
            Vector2 position = AutoAttack.ServerPosition(this);
            // Inside a dungeon the character is saved at its entrance in the world.
            if (RPG.Dungeons.DungeonManager.Instance != null && RPG.Dungeons.DungeonManager.Instance.TryGetWorldReturn(position, out Vector2 outside))
                position = outside;
            record.x = position.x;
            record.y = position.y;
            record.hasBeenSaved = true;
            accounts.Save(account);

            saveChangesAt = -1f;
            nextAutosave = Time.time + autosaveSeconds;
        }

        private static List<ItemStack> ToStacks(ItemRecord[] records)
        {
            var stacks = new List<ItemStack>();
            if (records == null) return stacks;
            foreach (ItemRecord record in records)
                stacks.Add(new ItemStack(record.id, record.count, record.affix, record.socket, record.socket2, record.socket3, record.socket4)
                    { Empower = record.empower });
            return stacks;
        }

        private static ItemRecord[] ToRecords(ItemStack[] stacks)
        {
            var records = new ItemRecord[stacks.Length];
            for (int i = 0; i < stacks.Length; i++)
                records[i] = new ItemRecord(stacks[i].ItemId, stacks[i].Count, stacks[i].AffixId, stacks[i].Socket, stacks[i].Socket2,
                    stacks[i].Socket3, stacks[i].Socket4) { empower = stacks[i].Empower };
            return records;
        }
    }
}
