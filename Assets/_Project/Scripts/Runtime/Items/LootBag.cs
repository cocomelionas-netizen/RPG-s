using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// Items a monster left behind. Only the server knows what is inside; a player who walks up and clicks it
    /// gets everything that fits in their bag. Disappears when emptied or after a while.
    /// </summary>
    public class LootBag : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float lifetime = 90f;
        [SerializeField] private float bobHeight = 0.04f;
        [SerializeField] private float bobSpeed = 3f;

        private readonly List<ItemDefinition> contents = new List<ItemDefinition>();
        private float despawnAt;
        private Transform visual;
        private Vector3 visualBase;

        /// <summary>Server only: what is still inside.</summary>
        public IReadOnlyList<ItemDefinition> Contents => contents;

        private void Awake()
        {
            var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                visual = spriteRenderer.transform;
                visualBase = visual.localPosition;
            }
        }

        /// <summary>Server: fills the bag (before or right after spawning it).</summary>
        public void ServerSetContents(IEnumerable<ItemDefinition> items)
        {
            contents.Clear();
            foreach (ItemDefinition item in items)
                if (item != null) contents.Add(item);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) despawnAt = Time.time + lifetime;
        }

        /// <summary>Server: moves every item that fits into the player's bag. True when anything was taken.</summary>
        public bool ServerGiveTo(Inventory inventory)
        {
            if (!IsServer || inventory == null) return false;

            bool tookAny = false;
            for (int i = contents.Count - 1; i >= 0; i--)
            {
                if (inventory.ServerAddItem(contents[i]) <= 0) continue;
                contents.RemoveAt(i);
                tookAny = true;
            }

            if (contents.Count == 0) NetworkObject.Despawn(true);
            return tookAny;
        }

        private void Update()
        {
            if (visual != null)
                visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * 0.5f + 0.5f) * bobHeight;

            if (IsServer && IsSpawned && (Time.time >= despawnAt || contents.Count == 0))
                NetworkObject.Despawn(true);
        }
    }
}
