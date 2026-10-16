using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Progression
{
    /// <summary>
    /// The spells a player knows (the source game's spellbook): a rank for every spell of the class (0 while not learned),
    /// and which spell sits in each slot of the action bar. A new character knows the level 1 spells; the rest are
    /// learned from Tomes once the player has the spell's level, and every spell is ranked up with experience
    /// (<see cref="AbilityDefinition.RankCost"/>). The server owns it; the owner asks to rank up and to arrange the bar.
    /// </summary>
    [RequireComponent(typeof(CharacterProgress))]
    public class Spellbook : NetworkBehaviour
    {
        /// <summary>Action bar slots for spells (keys 2, 3, 4, 7, 8, 9 and 0; key 1 is Attack).</summary>
        public const int BarSlots = 7;

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

        /// <summary>The spell in a bar slot (-1 when empty).</summary>
        public int SpellInSlot(int slot) => bar != null && slot >= 0 && slot < bar.Count ? bar[slot] : -1;

        /// <summary>The bar slot of a spell (-1 when it is not on the bar).</summary>
        public int SlotOf(int spell)
        {
            for (int i = 0; bar != null && i < bar.Count; i++)
                if (bar[i] == spell) return i;
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

        /// <summary>Owner: puts a known spell into a bar slot (-1 empties the slot); it leaves the slot it was in.</summary>
        public void RequestAssign(int slot, int spell)
        {
            if (IsOwner) AssignRpc(slot, spell);
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

        /// <summary>Server: a new character's spellbook: the level 1 spells, on the bar in order.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            ranks.Clear();
            bar.Clear();
            IReadOnlyList<AbilityDefinition> spells = Spells;
            for (int i = 0; i < spells.Count; i++)
                ranks.Add(spells[i] != null && spells[i].learnLevel <= 1 ? 1 : 0);
            for (int i = 0; i < BarSlots; i++) bar.Add(-1);
            for (int i = 0; i < spells.Count; i++)
                if (ranks[i] > 0) PutOnFreeSlot(i);
        }

        /// <summary>Server: restores saved ranks and bar (unknown entries are dropped; null keeps a new spellbook).</summary>
        public void ServerLoad(int[] savedRanks, int[] savedBar)
        {
            if (!IsServer) return;
            ServerReset();
            if (savedRanks == null || savedRanks.Length == 0) return;
            IReadOnlyList<AbilityDefinition> spells = Spells;
            for (int i = 0; i < ranks.Count && i < savedRanks.Length; i++)
                ranks[i] = spells[i] != null ? Mathf.Clamp(savedRanks[i], ranks[i], spells[i].maxRank) : 0;
            if (savedBar == null) return;
            for (int i = 0; i < bar.Count; i++)
            {
                int spell = i < savedBar.Length ? savedBar[i] : -1;
                bar[i] = Knows(spell) ? spell : -1;
            }
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

        /// <summary>Server: puts a known spell into a slot (or empties it with -1).</summary>
        public bool ServerAssign(int slot, int spell)
        {
            if (!IsServer || slot < 0 || slot >= bar.Count || (spell >= 0 && !Knows(spell))) return false;
            int previous = SlotOf(spell);
            if (spell >= 0 && previous >= 0 && previous != slot) bar[previous] = bar[slot];
            bar[slot] = spell < 0 ? -1 : spell;
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
