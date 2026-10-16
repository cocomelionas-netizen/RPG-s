using RPG.Combat;
using RPG.Items;
using RPG.Networking;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// On the player: which waystones they have discovered (by walking near them) and travel between them.
    /// The server checks everything: the player stands at a stone, the destination is discovered, and they can pay.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class WaystoneTraveller : NetworkBehaviour
    {
        [Tooltip("Walking this close to a waystone discovers it.")]
        [SerializeField, Min(0.5f)] private float discoverRadius = 3f;
        [Tooltip("Gold a trip costs; the source game's waypoints are free.")]
        [SerializeField, Min(0)] private int travelCost;

        private const float CheckInterval = 0.25f;
        private const float RangeTolerance = 0.6f;
        private static readonly Color MessageColor = new Color(0.55f, 0.85f, 1f);

        private readonly NetworkVariable<int> discovered = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Health health;
        private Inventory inventory;
        private PlayerNetworkMovement networkMovement;
        private float nextCheck;

        public int TravelCost => travelCost;

        /// <summary>Sets what a trip costs (0: free, like the source game).</summary>
        public void SetTravelCost(int cost) => travelCost = Mathf.Max(0, cost);

        /// <summary>Discovered waystones as bits by id (for saving).</summary>
        public int DiscoveredMask => discovered.Value;

        public bool IsDiscovered(int id) => id >= 0 && id < Waystone.MaxWaystones && (discovered.Value & (1 << id)) != 0;

        private void Awake()
        {
            health = GetComponent<Health>();
            inventory = GetComponent<Inventory>();
            networkMovement = GetComponent<PlayerNetworkMovement>();
        }

        /// <summary>Server: restores saved discoveries.</summary>
        public void ServerLoad(int mask)
        {
            if (IsServer) discovered.Value = mask;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || health.IsDead || Time.time < nextCheck) return;
            nextCheck = Time.time + CheckInterval;

            Vector2 position = AutoAttack.ServerPosition(health);
            foreach (Waystone stone in Waystone.All)
            {
                if (IsDiscovered(stone.Id) || Vector2.Distance(position, stone.transform.position) > discoverRadius) continue;
                ServerDiscover(stone.Id);
                AnnounceRpc($"You've discovered a new waypoint! {stone.DisplayName}");
            }
        }

        /// <summary>Server: marks a waystone as discovered.</summary>
        public void ServerDiscover(int id)
        {
            if (IsServer && id >= 0 && id < Waystone.MaxWaystones) discovered.Value |= 1 << id;
        }

        public void RequestTravel(int id)
        {
            if (IsOwner) TravelRpc(id);
        }

        [Rpc(SendTo.Server)]
        private void TravelRpc(int id, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerTravel(id);
        }

        /// <summary>Server: travels from the waystone the player stands at to a discovered one. False when refused.</summary>
        public bool ServerTravel(int id)
        {
            if (!IsServer || health.IsDead) return false;
            Waystone from = Waystone.Near(AutoAttack.ServerPosition(health), RangeTolerance);
            Waystone to = Waystone.Get(id);
            if (from == null || to == null || from == to) return false;
            if (!IsDiscovered(id))
            {
                AnnounceRpc("You have not discovered that waypoint yet");
                return false;
            }
            if (travelCost > 0 && inventory != null && inventory.Gold < travelCost)
            {
                AnnounceRpc($"You need {travelCost} gold to travel");
                return false;
            }

            if (travelCost > 0 && inventory != null) inventory.ServerAddGold(-travelCost, false);
            if (networkMovement != null) networkMovement.ServerTeleport(to.ArrivalPoint);
            AnnounceRpc($"Travelled to {to.DisplayName}");
            return true;
        }

        [Rpc(SendTo.Owner)]
        private void AnnounceRpc(string message)
        {
            CombatTextLayer.ShowText(transform.position + Vector3.up * 1.2f, message, MessageColor, 1.1f);
            MessageLog.Add(message + ".", MessageColor);
        }
    }
}
