using System;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Mana that abilities cost. The server owns the value and refills it slowly; clients only read it.
    /// Max mana comes from the class, level, Intelligence and items (set by <see cref="RPG.Progression.CharacterProgress"/>).
    /// </summary>
    public class Mana : NetworkBehaviour
    {
        [Tooltip("Share of max mana restored per second.")]
        [SerializeField, Range(0f, 1f)] private float regenFractionPerSecond = 0.03f;

        private readonly NetworkVariable<int> current = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> max = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Health health;
        private float pending;

        public int Current => current.Value;
        public int Max => max.Value;
        public float Fraction => max.Value > 0 ? (float)current.Value / max.Value : 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        public bool Has(int amount) => current.Value >= amount;

        /// <summary>Server: mana regained every second on top of the usual (Meditate).</summary>
        public float ServerBonusPerSecond { get; set; }

        /// <summary>Server: spends mana. False (and nothing spent) when there is not enough.</summary>
        public bool ServerSpend(int amount)
        {
            if (!IsServer) return false;
            if (amount <= 0) return true;
            if (current.Value < amount) return false;
            current.Value -= amount;
            return true;
        }

        /// <summary>Server: restores mana (potions).</summary>
        public void ServerRestore(int amount)
        {
            if (!IsServer || amount <= 0) return;
            current.Value = Math.Min(max.Value, current.Value + amount);
        }

        /// <summary>Server: changes max mana; <paramref name="fill"/> also refills it.</summary>
        public void ServerSetMax(int value, bool fill)
        {
            if (!IsServer) return;
            max.Value = Mathf.Max(0, value);
            current.Value = fill ? max.Value : Mathf.Min(current.Value, max.Value);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || current.Value >= max.Value || (health != null && health.IsDead))
            {
                pending = 0f;
                return;
            }

            pending += (max.Value * regenFractionPerSecond + ServerBonusPerSecond) * Time.deltaTime;
            int amount = Mathf.FloorToInt(pending);
            if (amount <= 0) return;
            pending -= amount;
            current.Value = Math.Min(max.Value, current.Value + amount);
        }
    }
}
