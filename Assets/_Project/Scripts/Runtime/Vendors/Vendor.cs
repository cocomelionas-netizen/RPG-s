using System;
using System.Collections.Generic;
using RPG.Items;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Vendors
{
    /// <summary>
    /// A vendor NPC standing in the world (the same on every peer). Players click it to trade; the trade itself
    /// is checked by the server in <see cref="RPG.Items.Inventory"/>. Vendors are told apart over the network by
    /// their index in <see cref="All"/>, which is sorted by name so it is the same everywhere.
    /// The server keeps how many of each limited item are left and restocks each on its own time
    /// (<see cref="VendorDefinition.RestockOf"/>): refilled to its limit, gear with a new random quality. Clients learn the
    /// counts and the time to each restock when they open the window, while it is open and after buying
    /// (<see cref="KnownRemaining"/>, <see cref="KnownSecondsLeft"/>). What is left and the restock times are kept between
    /// sessions (<see cref="VendorSaves"/>), as real time: logging out does not stop the clocks.
    /// </summary>
    public class Vendor : MonoBehaviour
    {
        [SerializeField] private VendorDefinition definition;
        [Tooltip("Where random qualities of gear in stock come from (vendors with Random Quality).")]
        [SerializeField] private AffixCatalog affixes;
        [Tooltip("How close a player must be to trade (the server allows a little more).")]
        [SerializeField, Min(0.5f)] private float tradeRange = 1.6f;

        private static readonly List<Vendor> Vendors = new List<Vendor>();

        /// <summary>Unlimited, in stock counts.</summary>
        public const int Unlimited = -1;

        private int[] remaining;
        private int[] affixIds;
        /// <summary>When each stock item restocks next (Time.time).</summary>
        private float[] nextRestock;
        private int[] known;
        private int[] knownAffixes;
        private float[] knownSecondsLeft;
        private float knownAt;

        public VendorDefinition Definition => definition;
        public float TradeRange => tradeRange;
        public string DisplayName => definition != null ? definition.displayName : name;

        /// <summary>Every vendor in the scene, sorted by name.</summary>
        public static ListView<Vendor> All => new ListView<Vendor>(Vendors);

        public int Index => Vendors.IndexOf(this);

        public static Vendor Get(int index) => index >= 0 && index < Vendors.Count ? Vendors[index] : null;

        /// <summary>Sets what it sells (used by the scene builder).</summary>
        public void Configure(VendorDefinition vendorDefinition, AffixCatalog affixCatalog = null)
        {
            definition = vendorDefinition;
            affixes = affixCatalog;
            remaining = null;
            known = null;
            knownAffixes = null;
        }

        public AffixCatalog Affixes => affixes;

        /// <summary>Server: the random affix the stock item has now (0 for none).</summary>
        public int ServerAffix(int index)
        {
            EnsureStock();
            return index >= 0 && index < affixIds.Length ? affixIds[index] : 0;
        }

        /// <summary>Server: every stock item's affix, for sending to a client.</summary>
        public int[] ServerAffixSnapshot()
        {
            EnsureStock();
            return (int[])affixIds.Clone();
        }

        /// <summary>Client: the affix of a stock item as last heard from the server (0 for none or unknown).</summary>
        public int KnownAffix(int index) => knownAffixes != null && index >= 0 && index < knownAffixes.Length ? knownAffixes[index] : 0;

        /// <summary>Server: how many of a stock item are left (<see cref="Unlimited"/> when there is no limit).</summary>
        public int ServerRemaining(int index)
        {
            EnsureStock();
            return index >= 0 && index < remaining.Length ? remaining[index] : 0;
        }

        /// <summary>Server: takes one of a stock item. False when it is sold out.</summary>
        public bool ServerTake(int index)
        {
            int left = ServerRemaining(index);
            if (left == Unlimited) return true;
            if (left <= 0) return false;
            remaining[index] = left - 1;
            Save();
            return true;
        }

        /// <summary>Server: seconds until each stock item restocks, for sending to a client.</summary>
        public float[] ServerSecondsLeftSnapshot()
        {
            EnsureStock();
            var left = new float[nextRestock.Length];
            for (int i = 0; i < left.Length; i++) left[i] = Mathf.Max(0f, nextRestock[i] - Time.time);
            return left;
        }

        /// <summary>Server: every stock count, for sending to a client.</summary>
        public int[] ServerStockSnapshot()
        {
            EnsureStock();
            return (int[])remaining.Clone();
        }

        /// <summary>Client: stores the counts the server sent.</summary>
        public void SetKnownStock(int[] counts, int[] affixesInStock = null, float[] secondsLeft = null)
        {
            known = counts;
            knownAffixes = affixesInStock;
            knownSecondsLeft = secondsLeft;
            knownAt = Time.unscaledTime;
        }

        /// <summary>Client: seconds until a stock item restocks, as last heard from the server (negative when unknown).</summary>
        public float KnownSecondsLeft(int index) =>
            knownSecondsLeft != null && index >= 0 && index < knownSecondsLeft.Length
                ? Mathf.Max(0f, knownSecondsLeft[index] - (Time.unscaledTime - knownAt))
                : -1f;

        /// <summary>Client: how many are left as last heard from the server (<see cref="Unlimited"/> when unlimited or unknown).</summary>
        public int KnownRemaining(int index)
        {
            if (known != null && index >= 0 && index < known.Length) return known[index];
            return definition != null && definition.LimitOf(index) > 0 ? definition.LimitOf(index) : Unlimited;
        }

        private void EnsureStock()
        {
            int count = definition != null ? definition.stock.Length : 0;
            if (remaining != null && remaining.Length == count) return;
            remaining = new int[count];
            affixIds = new int[count];
            nextRestock = new float[count];
            for (int i = 0; i < count; i++)
            {
                RestockItem(i);
                // The items do not all restock in the same frame: each first restock comes after a random part of its time.
                nextRestock[i] = Time.time + UnityEngine.Random.Range(0.3f, 1f) * definition.RestockOf(i);
            }
            Load();
        }

        /// <summary>Refills one stock item to its limit (gear gets a new random quality) and sets its next restock.</summary>
        private void RestockItem(int index)
        {
            int limit = definition.LimitOf(index);
            remaining[index] = limit > 0 ? limit : Unlimited;
            affixIds[index] = RollAffix(definition.stock[index]);
            nextRestock[index] = Time.time + definition.RestockOf(index);
        }

        /// <summary>The key this vendor is saved under: its definition's asset name (the same in every build).</summary>
        private string SaveKey => definition != null ? definition.name : name;

        /// <summary>Server: writes what is left and when each item restocks (as real time) to <see cref="VendorSaves"/>.</summary>
        public void Save()
        {
            if (!VendorSaves.Enabled || remaining == null) return;
            double now = VendorSaves.Now;
            var state = new VendorSaves.State
            {
                remaining = (int[])remaining.Clone(),
                affixes = (int[])affixIds.Clone(),
                restockAt = new double[nextRestock.Length],
            };
            for (int i = 0; i < nextRestock.Length; i++) state.restockAt[i] = now + (nextRestock[i] - Time.time);
            VendorSaves.Save(SaveKey, state);
        }

        /// <summary>
        /// Takes back what was left last time. An item whose restock came while nobody played restocks now, and its next
        /// restock keeps the rhythm (the first one due after now).
        /// </summary>
        private void Load()
        {
            if (!VendorSaves.Enabled || !VendorSaves.TryLoad(SaveKey, out VendorSaves.State state)) return;
            int count = remaining.Length;
            if (state.remaining == null || state.remaining.Length != count || state.affixes == null || state.affixes.Length != count
                || state.restockAt == null || state.restockAt.Length != count)
                return;
            double now = VendorSaves.Now;
            for (int i = 0; i < count; i++)
            {
                int limit = definition.LimitOf(i);
                remaining[i] = limit > 0 ? Mathf.Clamp(state.remaining[i], 0, limit) : Unlimited;
                affixIds[i] = state.affixes[i];
                double due = state.restockAt[i];
                float interval = definition.RestockOf(i);
                if (due <= now)
                {
                    RestockItem(i);
                    double missed = Math.Floor((now - due) / interval) + 1;
                    due += missed * interval;
                }
                nextRestock[i] = Time.time + (float)Math.Min(due - now, interval);
            }
        }

        /// <summary>A random quality for a piece in stock (0 for plain or when the vendor has none).</summary>
        private int RollAffix(ItemDefinition item)
        {
            if (definition == null || !definition.randomQuality || affixes == null || item == null || !item.IsEquippable || !item.canHaveAffix)
                return 0;
            ItemRarity quality = definition.RollQuality(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value,
                UnityEngine.Random.value);
            return quality > item.rarity ? affixes.RollQuality(item, item.itemLevel, quality) : 0;
        }

        private void Update()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsServer || definition == null) return;
            EnsureStock();
            for (int i = 0; i < nextRestock.Length; i++)
                if (Time.time >= nextRestock[i]) RestockItem(i);
        }

        private void OnEnable()
        {
            Vendors.Add(this);
            Vendors.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        }

        private void OnDisable() => Vendors.Remove(this);
    }
}
