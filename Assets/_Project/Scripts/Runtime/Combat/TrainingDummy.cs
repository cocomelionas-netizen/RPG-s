using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Server: a target for testing combat. Never fights back, returns to full health when left alone
    /// and comes back a few seconds after it "dies".
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float respawnDelay = 4f;
        [SerializeField, Min(0f)] private float resetAfterSeconds = 6f;

        private Health health;
        private float deadSince = -1f;

        public float RespawnDelay
        {
            get => respawnDelay;
            set => respawnDelay = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            if (health.IsDead)
            {
                if (deadSince < 0f) deadSince = Time.time;
                if (Time.time - deadSince >= respawnDelay)
                {
                    deadSince = -1f;
                    health.RestoreFull();
                }
                return;
            }

            if (health.Current < health.Max && Time.time - health.LastDamageTime >= resetAfterSeconds)
                health.RestoreFull();
        }
    }
}
