using RPG.Networking;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Server: a dead player comes back at the spawn point with full health and mana: after a delay, or (Wait For Choice,
    /// like the source game's "You have died. Respawn?") when the player presses Respawn (<see cref="RequestRespawn"/>),
    /// which a Deliverance can beat.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(PlayerNetworkMovement))]
    public class PlayerRespawn : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float respawnDelay = 5f;
        [Tooltip("Respawn only when the player asks (the death panel's Respawn button), not after the delay.")]
        [SerializeField] private bool waitForChoice;

        /// <summary>The shortest time dead before Respawn works (so a stray click does not skip a revive).</summary>
        public const float MinimumDeadSeconds = 1.5f;

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

        /// <summary>Respawn only when the player asks.</summary>
        public bool WaitForChoice
        {
            get => waitForChoice;
            set => waitForChoice = value;
        }

        private double diedAt = -1.0;

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
            diedAt = NetworkManager.ServerTime.Time;
            respawnAt = waitForChoice ? -1.0 : diedAt + respawnDelay;
            if (autoAttack != null) autoAttack.ServerStopAttack(true);
            Debug.Log($"[Server] {health.DisplayName} died.");
        }

        /// <summary>Server: brought back where it fell (Deliverance) with shares of health and mana; no respawn then.</summary>
        public void ServerRevive(float healthShare, float manaShare)
        {
            if (!IsServer || !health.IsDead) return;
            respawnAt = -1.0;
            health.ServerRevive(Mathf.RoundToInt(health.Max * healthShare));
            if (mana != null) mana.ServerRestore(Mathf.RoundToInt(mana.Max * manaShare) - mana.Current);
            Debug.Log($"[Server] {health.DisplayName} was revived.");
        }

        /// <summary>Owner: asks to come back at the spawn point now (the death panel's Respawn).</summary>
        public void RequestRespawn()
        {
            if (IsOwner) RespawnRpc();
        }

        [Rpc(SendTo.Server)]
        private void RespawnRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !health.IsDead) return;
            if (NetworkManager.ServerTime.Time - diedAt < MinimumDeadSeconds) return;
            ServerRespawnNow();
        }

        private void Update()
        {
            if (!IsServer || respawnAt < 0.0 || NetworkManager.ServerTime.Time < respawnAt) return;
            ServerRespawnNow();
        }

        private void ServerRespawnNow()
        {
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
