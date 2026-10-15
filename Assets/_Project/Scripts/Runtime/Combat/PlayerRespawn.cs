using RPG.Networking;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>Server: a dead player comes back at the spawn point with full health and mana after a delay.</summary>
    [RequireComponent(typeof(Health), typeof(PlayerNetworkMovement))]
    public class PlayerRespawn : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float respawnDelay = 5f;

        private Health health;
        private PlayerNetworkMovement networkMovement;
        private AutoAttack autoAttack;
        private Mana mana;
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
            mana = GetComponent<Mana>();
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

        /// <summary>Server: brought back where it fell (Redemption) with shares of health and mana; no respawn then.</summary>
        public void ServerRevive(float healthShare, float manaShare)
        {
            if (!IsServer || !health.IsDead) return;
            respawnAt = -1.0;
            health.ServerRevive(Mathf.RoundToInt(health.Max * healthShare));
            if (mana != null) mana.ServerRestore(Mathf.RoundToInt(mana.Max * manaShare) - mana.Current);
            Debug.Log($"[Server] {health.DisplayName} was revived.");
        }

        private void Update()
        {
            if (!IsServer || respawnAt < 0.0 || NetworkManager.ServerTime.Time < respawnAt) return;

            respawnAt = -1.0;
            var bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            Vector2 spawn = bootstrap != null ? (Vector2)bootstrap.GetSpawnPosition() : Vector2.zero;
            networkMovement.ServerTeleport(spawn);
            health.RestoreFull();
            if (mana != null) mana.ServerRestore(mana.Max);
            Debug.Log($"[Server] {health.DisplayName} respawned.");
        }
    }
}
