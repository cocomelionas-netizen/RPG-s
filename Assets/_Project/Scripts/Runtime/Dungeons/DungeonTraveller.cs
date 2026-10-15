using RPG.Combat;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Dungeons
{
    /// <summary>The player's side of portals: asks the server to use the portal it stands at, and shows where it went.</summary>
    [RequireComponent(typeof(Health))]
    public class DungeonTraveller : NetworkBehaviour
    {
        private static readonly Color MessageColor = new Color(0.8f, 0.6f, 1f);

        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        /// <summary>Owner: use the portal next to the player (the server checks the distance).</summary>
        public void RequestUsePortal()
        {
            if (IsOwner) UsePortalRpc();
        }

        [Rpc(SendTo.Server)]
        private void UsePortalRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            DungeonManager manager = DungeonManager.Instance;
            if (manager != null) manager.ServerUsePortal(health);
        }

        /// <summary>Server: shows the owner a short message (for example the dungeon's name on entering).</summary>
        public void ServerAnnounce(string message)
        {
            if (IsServer && !string.IsNullOrEmpty(message)) AnnounceRpc(message);
        }

        [Rpc(SendTo.Owner)]
        private void AnnounceRpc(string message)
        {
            CombatTextLayer.ShowText(transform.position + Vector3.up * 1.2f, message, MessageColor, 1.1f);
        }
    }
}
