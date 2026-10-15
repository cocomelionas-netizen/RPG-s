using System;
using Unity.Netcode;

namespace RPG.Items
{
    /// <summary>Some items of one kind in a bag or equipment slot. Id 0 means the slot is empty.</summary>
    public struct ItemStack : IEquatable<ItemStack>, INetworkSerializeByMemcpy
    {
        public int ItemId;
        public int Count;

        public static readonly ItemStack Empty = default;

        public ItemStack(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public bool IsEmpty => ItemId == 0 || Count <= 0;

        public bool Equals(ItemStack other) => ItemId == other.ItemId && Count == other.Count;

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ItemId, Count);
    }
}
