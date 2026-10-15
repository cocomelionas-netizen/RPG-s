using System;
using Unity.Netcode;

namespace RPG.Items
{
    /// <summary>
    /// Some items of one kind in a bag or equipment slot. Id 0 means the slot is empty. Items with a random
    /// affix (affix id above 0) only stack with the same item and affix.
    /// </summary>
    public struct ItemStack : IEquatable<ItemStack>, INetworkSerializeByMemcpy
    {
        public int ItemId;
        public int Count;
        public int AffixId;

        public static readonly ItemStack Empty = default;

        public ItemStack(int itemId, int count, int affixId = 0)
        {
            ItemId = itemId;
            Count = count;
            AffixId = affixId;
        }

        /// <summary>The same stack with another count.</summary>
        public ItemStack WithCount(int count) => new ItemStack(ItemId, count, AffixId);

        public bool SameKind(ItemStack other) => ItemId == other.ItemId && AffixId == other.AffixId;

        public bool IsEmpty => ItemId == 0 || Count <= 0;

        public bool Equals(ItemStack other) => ItemId == other.ItemId && Count == other.Count && AffixId == other.AffixId;

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ItemId, Count, AffixId);
    }
}
