using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>One item (or a stack of the same) in a loot bag, with the random affix it rolled (0 for none).</summary>
    public struct LootItem
    {
        public ItemDefinition Item;
        public int AffixId;
        /// <summary>0 or <see cref="ItemStack.EmptySocket"/>.</summary>
        public int Socket;
        private int count;

        /// <summary>How many (at least 1).</summary>
        public int Count => Mathf.Max(1, count);

        public LootItem(ItemDefinition item, int affixId, int socket = 0, int count = 1)
        {
            Item = item;
            AffixId = affixId;
            Socket = socket;
            this.count = count;
        }

        public LootItem WithCount(int newCount) => new LootItem(Item, AffixId, Socket, newCount);
    }

    /// <summary>
    /// Items a monster left behind. Only the server knows what is inside; a player who walks up and clicks it
    /// gets everything that fits in their bag. Disappears when emptied or after a while.
    /// </summary>
    public class LootBag : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float lifetime = 90f;
        [SerializeField] private float bobHeight = 0.04f;
        [SerializeField] private float bobSpeed = 3f;

        private readonly List<LootItem> contents = new List<LootItem>();
        private float despawnAt;
        private Transform visual;
        private Vector3 visualBase;

        /// <summary>Server only: what is still inside.</summary>
        public IReadOnlyList<LootItem> Contents => contents;

        private void Awake()
        {
            var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                visual = spriteRenderer.transform;
                visualBase = visual.localPosition;
            }
            // Sparkles show there is loot to take.
            LootSparkles.AddTo(transform, spriteRenderer);
        }

        /// <summary>Server: fills the bag (before or right after spawning it).</summary>
        public void ServerSetContents(IEnumerable<LootItem> items)
        {
            contents.Clear();
            foreach (LootItem item in items)
                if (item.Item != null) contents.Add(item);
        }

        /// <summary>Server: fills the bag with items without affixes.</summary>
        public void ServerSetContents(IEnumerable<ItemDefinition> items)
        {
            contents.Clear();
            foreach (ItemDefinition item in items)
                if (item != null) contents.Add(new LootItem(item, 0));
        }

        private static readonly List<LootBag> Bags = new List<LootBag>();

        /// <summary>The spawned loot bag with this network id on this peer (null when it is gone).</summary>
        public static LootBag Find(ulong networkId)
        {
            foreach (LootBag bag in Bags)
                if (bag != null && bag.IsSpawned && bag.NetworkObjectId == networkId) return bag;
            return null;
        }

        public override void OnNetworkSpawn()
        {
            Bags.Add(this);
            if (IsServer) despawnAt = Time.time + lifetime;
        }

        public override void OnNetworkDespawn() => Bags.Remove(this);

        /// <summary>
        /// Server: gives the player the one item at <paramref name="index"/> when it still is <paramref name="itemId"/>
        /// (the loot window shows what was inside a moment ago). True when it was taken.
        /// </summary>
        public bool ServerGiveOne(Inventory inventory, int index, int itemId)
        {
            if (!IsServer || inventory == null || inventory.Catalog == null || index < 0 || index >= contents.Count) return false;
            LootItem item = contents[index];
            if (inventory.Catalog.IdOf(item.Item) != itemId) return false;
            if (!GiveStack(inventory, index)) return false;
            if (contents.Count == 0) NetworkObject.Despawn(true);
            return true;
        }

        /// <summary>Server: moves every item that fits into the player's bag. True when anything was taken.</summary>
        public bool ServerGiveTo(Inventory inventory)
        {
            if (!IsServer || inventory == null) return false;

            bool tookAny = false;
            for (int i = contents.Count - 1; i >= 0; i--)
                tookAny |= GiveStack(inventory, i);

            if (contents.Count == 0) NetworkObject.Despawn(true);
            return tookAny;
        }

        /// <summary>Moves what fits of one entry into the bag; what does not fit stays. True when any was taken.</summary>
        private bool GiveStack(Inventory inventory, int index)
        {
            LootItem item = contents[index];
            int taken = inventory.ServerAddItem(item.Item, item.Count, true, item.AffixId, item.Socket);
            if (taken <= 0) return false;
            if (taken >= item.Count) contents.RemoveAt(index);
            else contents[index] = item.WithCount(item.Count - taken);
            return true;
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
