using System;
using System.Collections.Generic;
using UnityEngine;
namespace Unity.Collections { public struct FixedString32Bytes { public FixedString32Bytes(string s){} public static implicit operator FixedString32Bytes(string s)=>default; public override string ToString()=>""; } }
namespace Unity.Netcode
{
    public interface IReaderWriter {}
    public class BufferSerializer<T> where T : IReaderWriter { public void SerializeValue<V>(ref V v){} }
    public interface INetworkSerializable { void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter; }
    public enum NetworkVariableReadPermission { Everyone, Owner }
    public enum NetworkVariableWritePermission { Server, Owner }
    public class NetworkVariable<T> { public delegate void OnValueChangedDelegate(T previousValue, T newValue); public OnValueChangedDelegate OnValueChanged; public NetworkVariable(T value = default, NetworkVariableReadPermission r = default, NetworkVariableWritePermission w = default){} public T Value { get; set; } }
    public enum SendTo { Server, Owner, Everyone }
    public class RpcAttribute : Attribute { public RpcAttribute(SendTo s){} }
    public struct RpcReceiveParams { public ulong SenderClientId; }
    public struct RpcParams { public RpcReceiveParams Receive; }
    public struct NetworkTime { public double Time => 0; }
    public class NetworkClient { public NetworkObject PlayerObject; }
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Singleton { get; private set; }
        public NetworkTime ServerTime => default;
        public bool IsServer => false; public bool IsListening => false;
        public IReadOnlyList<NetworkClient> ConnectedClientsList => null; public bool IsHost=>false; public bool IsConnectedClient=>false;
        public NetworkConfig NetworkConfig; public NetworkClient LocalClient => null;
        public class ConnectionApprovalResponse { public bool Approved; public bool CreatePlayerObject; public uint? PlayerPrefabHash; public Vector3? Position; public Quaternion? Rotation; public bool Pending; public string Reason; }
        public struct ConnectionApprovalRequest { public byte[] Payload; public ulong ClientNetworkId; }
        public System.Action<ConnectionApprovalRequest, ConnectionApprovalResponse> ConnectionApprovalCallback;
        public event System.Action<ulong> OnClientConnectedCallback; public event System.Action<ulong> OnClientDisconnectCallback; public event System.Action<bool> OnServerStopped;
        public bool StartHost()=>true; public bool StartClient()=>true; public bool StartServer()=>true;
        public IReadOnlyList<ulong> ConnectedClientsIds => null; public ulong LocalClientId => 0; public string DisconnectReason => ""; public void Shutdown(){} public bool ShutdownInProgress => false;
    }
    public class NetworkObject : MonoBehaviour
    {
        public bool IsSpawned { get; internal set; }
        public void Spawn(bool destroyWithScene = false){}
        public void Despawn(bool destroy = true){}
    }
    public struct NetworkObjectReference { public NetworkObjectReference(NetworkObject o){} public bool TryGet(out NetworkObject o, NetworkManager m = null){o=null;return false;} public static implicit operator NetworkObjectReference(NetworkObject o)=>default; }
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        public bool IsServer { get; private set; } public bool IsOwner { get; private set; } public bool IsSpawned => false;
        public ulong OwnerClientId => 0; public ulong NetworkObjectId { get; internal set; }
        public NetworkManager NetworkManager => null; public NetworkObject NetworkObject => null;
        public virtual void OnNetworkSpawn(){} public virtual void OnNetworkDespawn(){} protected virtual void OnNetworkPostSpawn(){}
    }
}
namespace RPG.Controls { public class PlayerInputReader : MonoBehaviour { public const int AbilityKeyCount = 8; public static readonly string[] AbilityKeys = { "1", "2", "3", "4", "7", "8", "9", "0" }; public static readonly string[] PotionKeys = { "5", "6" }; public const int PotionKeyCount = 2; public bool AbilityPressed(int s)=>false; public bool PotionPressed(int s)=>false; public float ZoomDelta => 0f; public bool CancelPressed, TargetNextPressed, AttackPressed, MoveClickPressed, InventoryPressed, CharacterPressed, MapPressed, SpellbookPressed; public Vector2 MoveInput; public bool MoveClickHeld; public bool IsPointerOverUI; public Vector2 PointerPosition; } }
