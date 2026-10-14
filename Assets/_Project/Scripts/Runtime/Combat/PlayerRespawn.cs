using RPG.Networking;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>Server: a dead player comes back at the spawn point with full health after a delay.</summary>
    [RequireComponent(typeof(Health), typeof(PlayerNetworkMovement))]
    public class PlayerRespawn : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float respawnDelay = 5f;

        private Health health;
        private PlayerNetworkMovement networkMovement;
        private AutoAttack autoAttack;
        private double respawnAt = -1.0;

        public float RespawnDelay
        {
            get => respawnDelay;
            set => respawnDelay = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            networkMovement = GetComponent<PlayerNetworkMovement>();
            autoAttack = GetComponent<AutoAttack>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) health.Died -= OnDied;
        }

        private void OnDied()
        {
            respawnAt = NetworkManager.ServerTime.Time + respawnDelay;
            if (autoAttack != null) autoAttack.ServerStopAttack(true);
            Debug.Log($"[Server] {health.DisplayName} died.");
        }

        private void Update()
        {
            if (!IsServer || respawnAt < 0.0 || NetworkManager.ServerTime.Time < respawnAt) return;

            respawnAt = -1.0;
            var bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            Vector2 spawn = bootstrap != null ? (Vector2)bootstrap.GetSpawnPosition() : Vector2.zero;
            networkMovement.ServerTeleport(spawn);
            health.RestoreFull();
            Debug.Log($"[Server] {health.DisplayName} respawned.");
        }
    }
}
