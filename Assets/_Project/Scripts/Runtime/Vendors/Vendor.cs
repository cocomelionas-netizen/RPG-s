using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Vendors
{
    /// <summary>
    /// A vendor NPC standing in the world (the same on every peer). Players click it to trade; the trade itself
    /// is checked by the server in <see cref="RPG.Items.Inventory"/>. Vendors are told apart over the network by
    /// their index in <see cref="All"/>, which is sorted by name so it is the same everywhere.
    /// The server keeps how many of each limited item are left and restocks them; clients learn the counts
    /// when they open the window and after buying (<see cref="KnownRemaining"/>).
    /// </summary>
    public class Vendor : MonoBehaviour
    {
        [SerializeField] private VendorDefinition definition;
        [Tooltip("How close a player must be to trade (the server allows a little more).")]
        [SerializeField, Min(0.5f)] private float tradeRange = 1.6f;

        private static readonly List<Vendor> Vendors = new List<Vendor>();

        /// <summary>Unlimited, in stock counts.</summary>
        public const int Unlimited = -1;

        private int[] remaining;
        private float nextRestock;
        private int[] known;

        public VendorDefinition Definition => definition;
        public float TradeRange => tradeRange;
        public string DisplayName => definition != null ? definition.displayName : name;

        /// <summary>Every vendor in the scene, sorted by name.</summary>
        public static IReadOnlyList<Vendor> All => Vendors;

        public int Index => Vendors.IndexOf(this);

        public static Vendor Get(int index) => index >= 0 && index < Vendors.Count ? Vendors[index] : null;

        /// <summary>Sets what it sells (used by the scene builder).</summary>
        public void Configure(VendorDefinition vendorDefinition)
        {
            definition = vendorDefinition;
            remaining = null;
            known = null;
        }

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
            return true;
        }

        /// <summary>Server: every stock count, for sending to a client.</summary>
        public int[] ServerStockSnapshot()
        {
            EnsureStock();
            return (int[])remaining.Clone();
        }

        /// <summary>Client: stores the counts the server sent.</summary>
        public void SetKnownStock(int[] counts)
        {
            known = counts;
        }

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
            Restock();
        }

        private void Restock()
        {
            for (int i = 0; i < remaining.Length; i++)
            {
                int limit = definition.LimitOf(i);
                remaining[i] = limit > 0 ? limit : Unlimited;
            }
            nextRestock = Time.time + (definition != null ? definition.restockSeconds : 300f);
        }

        private void Update()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsServer || definition == null) return;
            EnsureStock();
            if (Time.time >= nextRestock) Restock();
        }

        private void OnEnable()
        {
            Vendors.Add(this);
            Vendors.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        }

        private void OnDisable() => Vendors.Remove(this);
    }
}
