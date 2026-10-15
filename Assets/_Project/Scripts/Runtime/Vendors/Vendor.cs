using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Vendors
{
    /// <summary>
    /// A vendor NPC standing in the world (the same on every peer). Players click it to trade; the trade itself
    /// is checked by the server in <see cref="RPG.Items.Inventory"/>. Vendors are told apart over the network by
    /// their index in <see cref="All"/>, which is sorted by name so it is the same everywhere.
    /// </summary>
    public class Vendor : MonoBehaviour
    {
        [SerializeField] private VendorDefinition definition;
        [Tooltip("How close a player must be to trade (the server allows a little more).")]
        [SerializeField, Min(0.5f)] private float tradeRange = 1.6f;

        private static readonly List<Vendor> Vendors = new List<Vendor>();

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
        }

        private void OnEnable()
        {
            Vendors.Add(this);
            Vendors.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        }

        private void OnDisable() => Vendors.Remove(this);
    }
}
