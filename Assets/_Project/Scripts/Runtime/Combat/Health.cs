using System;
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

        /// <summary>Raised on every peer when damage lands: (amount, critical).</summary>
        public event Action<int, bool> Damaged;

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

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            max.Value = maxHealth;
            current.Value = maxHealth;
            displayName.Value = appendOwnerId ? $"{defaultName} {OwnerClientId}" : defaultName;
        }

        public bool IsHostileTo(Health other)
        {
            return other != null && other != this && other.faction != faction;
        }

        /// <summary>Server only. Ignored for dead or invulnerable units and non-positive amounts.</summary>
        /// <param name="attacker">Who dealt the damage (monsters turn on their attackers); null when unknown.</param>
        public void ApplyDamage(int amount, bool critical, Health attacker = null)
        {
            if (!IsServer || IsDead || ServerInvulnerable || amount <= 0) return;

            current.Value = Mathf.Max(0, current.Value - amount);
            LastDamageTime = Time.time;
            DamagedRpc(amount, critical);
            DamageTaken?.Invoke(attacker, amount);

            if (current.Value == 0)
                Died?.Invoke();
        }

        /// <summary>Server only: restores some health. Ignored for dead units.</summary>
        public void Heal(int amount)
        {
            if (!IsServer || IsDead || amount <= 0) return;
            current.Value = Mathf.Min(max.Value, current.Value + amount);
        }

        /// <summary>Server only: back to full health (respawn, out-of-combat reset).</summary>
        public void RestoreFull()
        {
            if (!IsServer) return;
            current.Value = max.Value;
        }

        [Rpc(SendTo.Everyone)]
        private void DamagedRpc(int amount, bool critical)
        {
            Damaged?.Invoke(amount, critical);
        }
    }
}
