using System;
using Unity.Netcode;

namespace RPG.Items
{
    /// <summary>
    /// Some items of one kind in a bag or equipment slot. Id 0 means the slot is empty. Items with a random
    /// affix (affix id above 0) or sockets only stack with the same item, affix and sockets. Gear has up to
    /// <see cref="MaxSockets"/> sockets, like the source game's (the better the quality, the more), filled from the first.
    /// </summary>
    public struct ItemStack : IEquatable<ItemStack>, INetworkSerializeByMemcpy
    {
        /// <summary>A socket's value when it holds no gem yet.</summary>
        public const int EmptySocket = -1;

        /// <summary>The most sockets an item can have (the source game's purple gear: 2 to 4).</summary>
        public const int MaxSockets = 4;

        public int ItemId;
        public int Count;
        public int AffixId;
        /// <summary>Each socket: 0 none, <see cref="EmptySocket"/> empty, above 0 the item id of the gem set in it.</summary>
        public int Socket;
        public int Socket2;
        public int Socket3;
        public int Socket4;

        public static readonly ItemStack Empty = default;

        public ItemStack(int itemId, int count, int affixId = 0, int socket = 0, int socket2 = 0, int socket3 = 0, int socket4 = 0)
        {
            ItemId = itemId;
            Count = count;
            AffixId = affixId;
            Socket = socket;
            Socket2 = socket2;
            Socket3 = socket3;
            Socket4 = socket4;
        }

        /// <summary>One item with <paramref name="sockets"/> empty sockets.</summary>
        public static ItemStack WithEmptySockets(int itemId, int count, int affixId, int sockets)
        {
            var stack = new ItemStack(itemId, count, affixId);
            for (int i = 0; i < Math.Min(sockets, MaxSockets); i++) stack = stack.WithSocketAt(i, EmptySocket);
            return stack;
        }

        /// <summary>The same stack with another count.</summary>
        public ItemStack WithCount(int count) => new ItemStack(ItemId, count, AffixId, Socket, Socket2, Socket3, Socket4);

        /// <summary>The same stack with another first socket content.</summary>
        public ItemStack WithSocket(int socket) => WithSocketAt(0, socket);

        /// <summary>What socket <paramref name="index"/> holds (0 when the item has no such socket).</summary>
        public int SocketAt(int index)
        {
            switch (index)
            {
                case 0: return Socket;
                case 1: return Socket2;
                case 2: return Socket3;
                case 3: return Socket4;
                default: return 0;
            }
        }

        public ItemStack WithSocketAt(int index, int value)
        {
            ItemStack copy = this;
            switch (index)
            {
                case 0: copy.Socket = value; break;
                case 1: copy.Socket2 = value; break;
                case 2: copy.Socket3 = value; break;
                case 3: copy.Socket4 = value; break;
            }
            return copy;
        }

        /// <summary>How many sockets the item has.</summary>
        public int SocketCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxSockets; i++)
                    if (SocketAt(i) != 0) count++;
                return count;
            }
        }

        /// <summary>The first empty socket (-1 when none).</summary>
        public int FirstEmptySocket
        {
            get
            {
                for (int i = 0; i < MaxSockets; i++)
                    if (SocketAt(i) == EmptySocket) return i;
                return -1;
            }
        }

        public bool HasSocket => SocketCount > 0;

        public bool HasGem => Socket > 0 || Socket2 > 0 || Socket3 > 0 || Socket4 > 0;

        public bool SameKind(ItemStack other) => ItemId == other.ItemId && AffixId == other.AffixId && Socket == other.Socket
                                                 && Socket2 == other.Socket2 && Socket3 == other.Socket3 && Socket4 == other.Socket4;

        public bool IsEmpty => ItemId == 0 || Count <= 0;

        public bool Equals(ItemStack other) => SameKind(other) && Count == other.Count;

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ItemId, Count, AffixId, Socket, Socket2, Socket3, Socket4);
    }
}
