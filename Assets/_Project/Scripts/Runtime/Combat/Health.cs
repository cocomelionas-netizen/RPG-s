using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Hit points of anything that can be targeted and damaged. The server owns the value;
    /// clients only read it. Damage is applied on the server and announced to everyone
    /// (for damage numbers and hit flashes).
    /// </summary>
    public class Health : NetworkBehaviour
    {
        [SerializeField] private string defaultName = "Unit";
        [Tooltip("Adds the owner's client id to the name, e.g. \"Player 2\".")]
        [SerializeField] private bool appendOwnerId;
        [SerializeField] private Faction faction = Faction.Monster;
        [SerializeField, Min(1)] private int maxHealth = 100;

        private readonly NetworkVariable<int> current = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> max = new NetworkVariable<int>(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString32Bytes> displayName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static readonly List<Health> SpawnedUnits = new List<Health>();

        /// <summary>Raised on every peer when damage lands: (amount, critical).</summary>
        public event Action<int, bool> Damaged;

        /// <summary>Raised on every peer with the school of the damage that just landed (after <see cref="Damaged"/>).</summary>
        public DamageSchool LastDamageSchool { get; private set; }

        /// <summary>Raised on every peer when a hit was dodged ("Dodge") or partly blocked ("Block").</summary>
        public event Action<string> Avoided;

        /// <summary>Raised on every peer when a noticeable heal lands (abilities, not regeneration): amount.</summary>
        public event Action<int> Healed;

        /// <summary>Server only: health reached zero.</summary>
        public event Action Died;

        /// <summary>Server only: damage landed: (attacker, or null when unknown; amount).</summary>
        public event Action<Health, int> DamageTaken;

        public int Current => current.Value;
        public int Max => max.Value;
        public float Fraction => max.Value > 0 ? (float)current.Value / max.Value : 0f;
        public bool IsDead => IsSpawned && current.Value <= 0;
        public string DisplayName => displayName.Value.ToString();
        public Faction Faction => faction;

        /// <summary>Server only: <see cref="Time.time"/> of the last damage taken.</summary>
        public float LastDamageTime { get; private set; } = float.NegativeInfinity;

        /// <summary>Server only: ignores all damage while set (for example a monster walking back home).</summary>
        public bool ServerInvulnerable { get; set; }

        /// <summary>Server only: reduces damage taken; 100 armor halves it.</summary>
        public int ServerArmor { get; set; }

        /// <summary>Server only: chance (0..1) to avoid a hit completely.</summary>
        public float ServerDodgeChance { get; set; }

        /// <summary>Server only: chance (0..1) to block half of a hit.</summary>
        public float ServerBlockChance { get; set; }

        private readonly int[] resists = new int[5];

        /// <summary>Server only: resist rating against a magic school (physical damage has none).</summary>
        public void ServerSetResist(DamageSchool school, int rating)
        {
            if (school != DamageSchool.Physical) resists[(int)school] = Mathf.Max(0, rating);
        }

        public int ServerResist(DamageSchool school) => resists[(int)school];

        /// <summary>Damage left after armor: amount * 100 / (100 + armor), at least 1.</summary>
        public static int ReduceByArmor(int amount, int armor)
        {
            if (amount <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(amount * 100f / (100f + Mathf.Max(0, armor))));
        }

        /// <summary>Every spawned unit on this peer (for area effects and target searches).</summary>
        public static IReadOnlyList<Health> Spawned => SpawnedUnits;

        public override void OnNetworkSpawn()
        {
            SpawnedUnits.Add(this);
            if (!IsServer) return;

            max.Value = maxHealth;
            current.Value = maxHealth;
            displayName.Value = appendOwnerId ? $"{defaultName} {OwnerClientId}" : defaultName;
        }

        public override void OnNetworkDespawn()
        {
            SpawnedUnits.Remove(this);
        }

        public bool IsHostileTo(Health other)
        {
            return other != null && other != this && other.faction != faction;
        }

        /// <summary>Server only. Ignored for dead or invulnerable units and non-positive amounts.</summary>
        /// <param name="attacker">Who dealt the damage (monsters turn on their attackers); null when unknown.</param>
        /// <param name="school">Physical, or a magic school this unit's resist of that school reduces.</param>
        public void ApplyDamage(int amount, bool critical, Health attacker = null, DamageSchool school = DamageSchool.Physical)
        {
            if (!IsServer || IsDead || ServerInvulnerable || amount <= 0) return;
            LastDamageTime = Time.time;
            if (ServerDodgeChance > 0f && UnityEngine.Random.value < ServerDodgeChance)
            {
                AvoidedRpc(AvoidKind.Dodge);
                // Still counts as being attacked: monsters keep their threat.
                DamageTaken?.Invoke(attacker, 0);
                return;
            }
            bool blocked = ServerBlockChance > 0f && UnityEngine.Random.value < ServerBlockChance;
            if (blocked) amount = Mathf.Max(1, amount / 2);
            amount = ReduceByArmor(amount, ServerArmor);
            if (school != DamageSchool.Physical)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - RPG.Progression.AttributeRules.ResistReduction(resists[(int)school]))));

            current.Value = Mathf.Max(0, current.Value - amount);
            if (blocked) AvoidedRpc(AvoidKind.Block);
            DamagedRpc(amount, critical, school);
            DamageTaken?.Invoke(attacker, amount);

            if (current.Value == 0)
                Died?.Invoke();
        }

        /// <summary>Server only: the name shown on frames (for example the account name of a player).</summary>
        public void ServerSetDisplayName(string unitName)
        {
            if (!IsServer || string.IsNullOrEmpty(unitName)) return;
            displayName.Value = unitName.Length > 29 ? unitName.Substring(0, 29) : unitName;
        }

        /// <summary>Server only: restores some health. Ignored for dead units.</summary>
        /// <param name="announce">Show the heal to everyone (a green number); off for quiet regeneration.</param>
        public void Heal(int amount, bool announce = false)
        {
            if (!IsServer || IsDead || amount <= 0) return;
            int healed = Mathf.Min(max.Value, current.Value + amount) - current.Value;
            current.Value += healed;
            if (announce && healed > 0) HealedRpc(healed);
        }

        /// <summary>Server only: changes max health (class and level). <paramref name="fill"/> also restores full health.</summary>
        public void ServerSetMaxHealth(int value, bool fill)
        {
            if (!IsServer) return;
            max.Value = Mathf.Max(1, value);
            if (fill && !IsDead) current.Value = max.Value;
            else current.Value = Mathf.Min(current.Value, max.Value);
        }

        /// <summary>Server only: back to full health (respawn, out-of-combat reset).</summary>
        public void RestoreFull()
        {
            if (!IsServer) return;
            current.Value = max.Value;
        }

        private enum AvoidKind : byte { Dodge, Block }

        [Rpc(SendTo.Everyone)]
        private void DamagedRpc(int amount, bool critical, DamageSchool school)
        {
            LastDamageSchool = school;
            Damaged?.Invoke(amount, critical);
        }

        [Rpc(SendTo.Everyone)]
        private void AvoidedRpc(AvoidKind kind)
        {
            Avoided?.Invoke(kind == AvoidKind.Dodge ? "Dodge" : "Block");
        }

        [Rpc(SendTo.Everyone)]
        private void HealedRpc(int amount)
        {
            Healed?.Invoke(amount);
        }
    }
}
