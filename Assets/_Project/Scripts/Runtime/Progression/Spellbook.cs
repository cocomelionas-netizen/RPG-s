using System;
using System.Collections.Generic;
using RPG.Items;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Progression
{
    /// <summary>
    /// The spells a player knows (the source game's spellbook): a rank for every spell of the class (0 while not learned),
    /// and what sits in each of the 12 slots of the action bar (the source game's ActionBar): Attack, a spell or a usable
    /// item (potions). A new character knows the level 1 spells; the rest are learned from Tomes once the player has the
    /// spell's level, and every spell is ranked up with experience (<see cref="AbilityDefinition.RankCost"/>). The server
    /// owns it; the owner asks to rank up and to arrange the bar (dragging spells and items onto it, moving and removing them).
    /// </summary>
    [RequireComponent(typeof(CharacterProgress))]
    public class Spellbook : NetworkBehaviour
    {
        /// <summary>Action bar slots (keys 1 to 9, 0, - and =), like the source game's 12-slot bar.</summary>
        public const int BarSlots = 12;

        /// <summary>The bar entry of the auto-attack (a new bar has it on key 1).</summary>
        public const int AttackEntry = -2;

        /// <summary>Bar entries from here up are items: the entry minus this is the item's catalog id.</summary>
        public const int ItemEntryBase = 100000;

        /// <summary>Where a new bar puts Attack (key 1) and the first healing and mana potions (keys 5 and 6).</summary>
        public const int AttackSlot = 0;
        public const int HealingPotionSlot = 4;
        public const int ManaPotionSlot = 5;

        /// <summary>The bar entry of an item.</summary>
        public static int ItemEntry(int itemId) => ItemEntryBase + itemId;

        private NetworkList<int> ranks;
        private NetworkList<int> bar;
        private CharacterProgress progress;

        /// <summary>Raised on every peer when ranks or the bar changed.</summary>
        public event Action Changed;

        /// <summary>Every spell of the class, known or not, in their order.</summary>
        public IReadOnlyList<AbilityDefinition> Spells =>
            progress != null && progress.Class != null ? progress.Class.abilities : Array.Empty<AbilityDefinition>();

        private void Awake()
        {
            progress = GetComponent<CharacterProgress>();
            ranks = new NetworkList<int>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            bar = new NetworkList<int>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            ranks.OnListChanged += OnListChanged;
            bar.OnListChanged += OnListChanged;
            progress.ClassChanged += OnClassChanged;
            if (IsServer) ServerReset();
        }

        public override void OnNetworkDespawn()
        {
            ranks.OnListChanged -= OnListChanged;
            bar.OnListChanged -= OnListChanged;
            progress.ClassChanged -= OnClassChanged;
        }

        private void OnListChanged(NetworkListEvent<int> change) => Changed?.Invoke();

        private void OnClassChanged()
        {
            if (IsServer) ServerReset();
        }

        public AbilityDefinition Get(int spell) => spell >= 0 && spell < Spells.Count ? Spells[spell] : null;

        public int IndexOf(AbilityDefinition spell)
        {
            IReadOnlyList<AbilityDefinition> spells = Spells;
            for (int i = 0; i < spells.Count; i++)
                if (spells[i] == spell) return i;
            return -1;
        }

        /// <summary>The spell's rank (0 while not learned).</summary>
        public int RankOf(int spell) => ranks != null && spell >= 0 && spell < ranks.Count ? ranks[spell] : 0;

        public bool Knows(int spell) => RankOf(spell) > 0;

        /// <summary>What a bar slot holds: -1 empty, <see cref="AttackEntry"/>, a spell index, or an item entry (<see cref="ItemEntry"/>).</summary>
        public int EntryInSlot(int slot) => bar != null && slot >= 0 && slot < bar.Count ? bar[slot] : -1;

        /// <summary>The spell in a bar slot (-1 when empty, Attack or an item).</summary>
        public int SpellInSlot(int slot)
        {
            int entry = EntryInSlot(slot);
            return entry >= ItemEntryBase || entry < 0 ? -1 : entry;
        }

        /// <summary>Whether a bar slot holds Attack.</summary>
        public bool IsAttackSlot(int slot) => EntryInSlot(slot) == AttackEntry;

        /// <summary>The catalog id of the item in a bar slot (0 when empty or a spell).</summary>
        public int ItemInSlot(int slot)
        {
            int entry = EntryInSlot(slot);
            return entry >= ItemEntryBase ? entry - ItemEntryBase : 0;
        }

        /// <summary>The bar slot of a spell (-1 when it is not on the bar).</summary>
        public int SlotOf(int spell) => SlotOfEntry(spell);

        private int SlotOfEntry(int entry)
        {
            for (int i = 0; bar != null && entry != -1 && i < bar.Count; i++)
                if (bar[i] == entry) return i;
            return -1;
        }

        /// <summary>Experience the spell's next rank costs (0 when it cannot rank up).</summary>
        public int NextRankCost(int spell)
        {
            AbilityDefinition ability = Get(spell);
            int rank = RankOf(spell);
            return ability != null && rank > 0 && rank < ability.maxRank ? ability.RankCost(rank) : 0;
        }

        public bool CanRankUp(int spell)
        {
            int cost = NextRankCost(spell);
            return cost > 0 && progress.ExperienceToSpend >= cost;
        }

        public int[] RanksSnapshot()
        {
            var copy = new int[ranks.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = ranks[i];
            return copy;
        }

        public int[] BarSnapshot()
        {
            var copy = new int[bar.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = bar[i];
            return copy;
        }

        // ---------- Owner ----------

        public void RequestRankUp(int spell)
        {
            if (IsOwner) RankUpRpc(spell);
        }

        /// <summary>Owner: puts a known spell (or <see cref="AttackEntry"/>) into a bar slot (-1 empties it); it leaves the slot it was in.</summary>
        public void RequestAssign(int slot, int spell)
        {
            if (IsOwner) AssignRpc(slot, spell);
        }

        /// <summary>Owner: puts a usable item (by catalog id) into a bar slot; it leaves the slot it was in.</summary>
        public void RequestAssignItem(int slot, int itemId)
        {
            if (IsOwner && itemId > 0) AssignRpc(slot, ItemEntry(itemId));
        }

        /// <summary>Owner: moves what is in one bar slot to another (they swap).</summary>
        public void RequestMove(int from, int to)
        {
            if (IsOwner) MoveRpc(from, to);
        }

        [Rpc(SendTo.Server)]
        private void MoveRpc(int from, int to, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerMove(from, to);
        }

        [Rpc(SendTo.Server)]
        private void RankUpRpc(int spell, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerRankUp(spell);
        }

        [Rpc(SendTo.Server)]
        private void AssignRpc(int slot, int spell, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerAssign(slot, spell);
        }

        // ---------- Server ----------

        /// <summary>
        /// Server: a new character's spellbook: Attack on key 1, the level 1 spells on the bar in order, the potions on keys 5 and 6.
        /// </summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            ranks.Clear();
            bar.Clear();
            IReadOnlyList<AbilityDefinition> spells = Spells;
            for (int i = 0; i < spells.Count; i++)
                ranks.Add(spells[i] != null && spells[i].learnLevel <= 1 ? 1 : 0);
            for (int i = 0; i < BarSlots; i++) bar.Add(-1);
            bar[AttackSlot] = AttackEntry;
            bar[HealingPotionSlot] = DefaultPotionEntry(ItemUse.Heal);
            bar[ManaPotionSlot] = DefaultPotionEntry(ItemUse.RestoreMana);
            for (int i = 0; i < spells.Count; i++)
                if (ranks[i] > 0) PutOnFreeSlot(i);
        }

        /// <summary>
        /// Server: restores saved ranks and bar (unknown entries are dropped; null keeps a new spellbook). A bar saved
        /// before Attack and items could go on it (7 spell slots: keys 2, 3, 4, 7, 8, 9, 0) keeps its spells on those keys.
        /// </summary>
        public void ServerLoad(int[] savedRanks, int[] savedBar)
        {
            if (!IsServer) return;
            ServerReset();
            if (savedRanks == null || savedRanks.Length == 0) return;
            IReadOnlyList<AbilityDefinition> spells = Spells;
            for (int i = 0; i < ranks.Count && i < savedRanks.Length; i++)
                ranks[i] = spells[i] != null ? Mathf.Clamp(savedRanks[i], ranks[i], spells[i].maxRank) : 0;
            if (savedBar == null || savedBar.Length == 0) return;

            const int oldSlots = 7;
            if (savedBar.Length == oldSlots)
            {
                // Old keys 2, 3, 4 are slots 1-3 now, old keys 7, 8, 9, 0 slots 6-9; Attack and the potions stay.
                for (int i = 0; i < oldSlots; i++)
                    bar[i < 3 ? i + 1 : i + 3] = Knows(savedBar[i]) ? savedBar[i] : -1;
                return;
            }
            for (int i = 0; i < bar.Count; i++)
            {
                int entry = i < savedBar.Length ? savedBar[i] : -1;
                bar[i] = IsValidEntry(entry) ? entry : -1;
            }
        }

        private Inventory inventory;

        private ItemCatalog Catalog
        {
            get
            {
                if (inventory == null) inventory = GetComponent<Inventory>();
                return inventory != null ? inventory.Catalog : null;
            }
        }

        /// <summary>Whether an entry may sit on the bar: Attack, a known spell or a usable item.</summary>
        public bool IsValidEntry(int entry)
        {
            if (entry == AttackEntry) return true;
            if (entry < ItemEntryBase) return Knows(entry);
            ItemDefinition item = Catalog != null ? Catalog.Get(entry - ItemEntryBase) : null;
            return item != null && item.use != ItemUse.None;
        }

        /// <summary>The bar entry of the catalog's first potion of a kind (-1 when there is none).</summary>
        private int DefaultPotionEntry(ItemUse use)
        {
            ItemCatalog catalog = Catalog;
            if (catalog == null || catalog.items == null) return -1;
            for (int i = 0; i < catalog.items.Length; i++)
            {
                ItemDefinition item = catalog.items[i];
                if (item != null && item.use == use && item.itemLevel <= 1) return ItemEntry(catalog.IdOf(item));
            }
            return -1;
        }

        /// <summary>Server: learns a spell of the class (from a Tome) at rank 1 and puts it on a free slot. False when known or not of the class.</summary>
        public bool ServerLearn(AbilityDefinition spell)
        {
            int index = IndexOf(spell);
            if (!IsServer || index < 0 || Knows(index)) return false;
            ranks[index] = 1;
            PutOnFreeSlot(index);
            return true;
        }

        /// <summary>Server: raises a known spell's rank with experience. False at its last rank or without the experience.</summary>
        public bool ServerRankUp(int spell)
        {
            int cost = NextRankCost(spell);
            if (!IsServer || cost <= 0 || !progress.ServerTrySpendExperience(cost)) return false;
            ranks[spell] = RankOf(spell) + 1;
            return true;
        }

        /// <summary>
        /// Server: puts Attack, a known spell or a usable item (<see cref="ItemEntry"/>) into a slot, or empties it with -1.
        /// When it already was on another slot, the two slots swap.
        /// </summary>
        public bool ServerAssign(int slot, int entry)
        {
            if (!IsServer || slot < 0 || slot >= bar.Count || (entry != -1 && !IsValidEntry(entry))) return false;
            int previous = SlotOfEntry(entry);
            if (entry != -1 && previous >= 0 && previous != slot) bar[previous] = bar[slot];
            bar[slot] = entry;
            return true;
        }

        /// <summary>Server: swaps two bar slots.</summary>
        public bool ServerMove(int from, int to)
        {
            if (!IsServer || from < 0 || to < 0 || from >= bar.Count || to >= bar.Count) return false;
            if (from == to) return true;
            int moved = bar[from];
            bar[from] = bar[to];
            bar[to] = moved;
            return true;
        }

        private void PutOnFreeSlot(int spell)
        {
            if (SlotOf(spell) >= 0) return;
            for (int i = 0; i < bar.Count; i++)
            {
                if (bar[i] >= 0) continue;
                bar[i] = spell;
                return;
            }
        }
    }
}
