using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Server: slowly heals a living unit once it has not taken damage for a while (out of combat), and all the time by
    /// <see cref="ServerBonusPerSecond"/> (a player's Regeneration).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HealthRegeneration : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float delayAfterDamage = 5f;
        [Tooltip("Share of max health restored per second while regenerating.")]
        [SerializeField, Range(0f, 1f)] private float fractionPerSecond = 0.05f;

        private Health health;
        private float pending;

        /// <summary>Server: health regained every second even in combat (Regeneration).</summary>
        public float ServerBonusPerSecond { get; set; }

        public float DelayAfterDamage
        {
            get => delayAfterDamage;
            set => delayAfterDamage = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void Update()
        {
            bool canRegenerate = health.IsServer && health.IsSpawned && !health.IsDead && health.Current < health.Max;
            if (!canRegenerate)
            {
                pending = 0f;
                return;
            }

            bool outOfCombat = Time.time - health.LastDamageTime >= delayAfterDamage;
            pending += ((outOfCombat ? health.Max * fractionPerSecond : 0f) + ServerBonusPerSecond) * Time.deltaTime;
            int amount = Mathf.FloorToInt(pending);
            if (amount <= 0) return;

            pending -= amount;
            health.Heal(amount);
        }
    }
}
