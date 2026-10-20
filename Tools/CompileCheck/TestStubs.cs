using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine.TestTools { public class UnityTestAttribute : Attribute {} public class UnitySetUpAttribute : Attribute {} public class UnityTearDownAttribute : Attribute {} }
namespace UnityEditor { public static class AssetDatabase { public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object => null; } }
namespace Unity.Netcode.Transports.UTP { public class UnityTransport : Unity.Netcode.NetworkTransport { public bool UseWebSockets; public void SetConnectionData(bool a, string ip, ushort port, string listen){} } }
namespace Unity.Netcode {
  public class NetworkTransport : MonoBehaviour {}
  public class NetworkPrefab { public GameObject Prefab; }
  public class NetworkPrefabs { public bool Add(NetworkPrefab p)=>true; public bool Contains(GameObject p)=>false; }
  public class NetworkConfig { public byte[] ConnectionData = new byte[0]; public NetworkTransport NetworkTransport; public GameObject PlayerPrefab; public bool ConnectionApproval; public NetworkPrefabs Prefabs = new NetworkPrefabs(); }
  public partial class NetworkManagerExtra {}
}
namespace RPG.Networking { public class OfflineTransport : Unity.Netcode.NetworkTransport {} }
namespace Unity.Netcode {
  public interface INetworkSerializeByMemcpy {}
  public struct NetworkListEvent<T> { public T Value; public int Index; }
  public class NetworkList<T> where T : unmanaged, System.IEquatable<T> {
    public NetworkList(System.Collections.Generic.IEnumerable<T> values = default, NetworkVariableReadPermission r = default, NetworkVariableWritePermission w = default) {}
    public delegate void OnListChangedDelegate(NetworkListEvent<T> e); public event OnListChangedDelegate OnListChanged;
    public void Add(T v){} public void Clear(){} public void RemoveAt(int i){} public int Count => 0; public T this[int i] { get => default; set {} }
  }
}
namespace UnityEngine.InputSystem { public class Mouse { public static Mouse current; public PosCtl position; } public class PosCtl { public Vector2 ReadValue()=>default; } public class Keyboard { public static Keyboard current; public KeyCtl f3Key; } public class KeyCtl { public bool wasPressedThisFrame; } }
