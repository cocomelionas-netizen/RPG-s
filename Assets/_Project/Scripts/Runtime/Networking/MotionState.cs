using System;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Networking
{
    /// <summary>A character's replicated movement at one moment of server time.</summary>
    public struct MotionState : INetworkSerializable, IEquatable<MotionState>
    {
        public Vector2 Position;
        public Vector2 Facing;
        public bool IsMoving;
        /// <summary>Server time when the server accepted this state; used to interpolate remote characters.</summary>
        public double ServerTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Facing);
            serializer.SerializeValue(ref IsMoving);
            serializer.SerializeValue(ref ServerTime);
        }

        public bool Equals(MotionState other)
        {
            return Position == other.Position && Facing == other.Facing && IsMoving == other.IsMoving && ServerTime.Equals(other.ServerTime);
        }

        public override bool Equals(object obj) => obj is MotionState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Position, Facing, IsMoving, ServerTime);
    }
}
