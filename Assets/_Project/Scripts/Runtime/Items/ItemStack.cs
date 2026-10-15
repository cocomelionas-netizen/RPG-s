using System;
using Unity.Netcode;

namespace RPG.Items
{
    /// <summary>
    /// Some items of one kind in a bag or equipment slot. Id 0 means the slot is empty. Items with a random
    /// affix (affix id above 0) or a socket only stack with the same item, affix and socket.
    /// </summary>
    public struct ItemStack : IEquatable<ItemStack>, INetworkSerializeByMemcpy
    {
        /// <summary><see cref="Socket"/> value of an item with a socket that holds no gem yet.</summary>
        public const int EmptySocket = -1;

        public int ItemId;
        public int Count;
        public int AffixId;
        /// <summary>0: no socket, <see cref="EmptySocket"/>: an empty socket, above 0: the item id of the gem set in it.</summary>
        public int Socket;

        public static readonly ItemStack Empty = default;

        public ItemStack(int itemId, int count, int affixId = 0, int socket = 0)
        {
            ItemId = itemId;
            Count = count;
            AffixId = affixId;
            Socket = socket;
        }

        /// <summary>The same stack with another count.</summary>
        public ItemStack WithCount(int count) => new ItemStack(ItemId, count, AffixId, Socket);

        /// <summary>The same stack with another socket content.</summary>
        public ItemStack WithSocket(int socket) => new ItemStack(ItemId, Count, AffixId, socket);

        public bool HasSocket => Socket != 0;

        public bool HasGem => Socket > 0;

        public bool SameKind(ItemStack other) => ItemId == other.ItemId && AffixId == other.AffixId && Socket == other.Socket;

        public bool IsEmpty => ItemId == 0 || Count <= 0;

        public bool Equals(ItemStack other) =>
            ItemId == other.ItemId && Count == other.Count && AffixId == other.AffixId && Socket == other.Socket;

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ItemId, Count, AffixId, Socket);
    }
}
