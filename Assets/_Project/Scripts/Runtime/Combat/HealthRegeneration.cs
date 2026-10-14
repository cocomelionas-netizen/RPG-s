using UnityEngine;

namespace RPG.Combat
{
    /// <summary>Server: slowly heals a living unit once it has not taken damage for a while (out of combat).</summary>
    [RequireComponent(typeof(Health))]
    public class HealthRegeneration : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float delayAfterDamage = 5f;
        [Tooltip("Share of max health restored per second while regenerating.")]
        [SerializeField, Range(0f, 1f)] private float fractionPerSecond = 0.05f;

        private Health health;
        private float pending;

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
            bool canRegenerate = health.IsServer && health.IsSpawned && !health.IsDead
                                 && health.Current < health.Max
                                 && Time.time - health.LastDamageTime >= delayAfterDamage;
            if (!canRegenerate)
            {
                pending = 0f;
                return;
            }

            pending += health.Max * fractionPerSecond * Time.deltaTime;
            int amount = Mathf.FloorToInt(pending);
            if (amount <= 0) return;

            pending -= amount;
            health.Heal(amount);
        }
    }
}
