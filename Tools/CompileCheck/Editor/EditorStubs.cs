// Packages the editor scripts use that have no reference DLL here (URP, the Input System's UI module), and the one
// build API newer than the UnityEditor.dll reference.
namespace UnityEngine.Rendering.Universal
{
    public class Renderer2DData : UnityEngine.ScriptableObject {}
    public class UniversalAdditionalCameraData : UnityEngine.MonoBehaviour {}
}
namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { public override void Process() {} }
}
namespace UnityEditor
{
    public enum StandaloneBuildSubtarget { Player, Server }
}
namespace Unity.Netcode
{
    public class NetworkPrefabsList : UnityEngine.ScriptableObject
    {
        public bool Contains(UnityEngine.GameObject prefab) => false;
        public void Add(NetworkPrefab prefab) {}
        public void Remove(NetworkPrefab prefab) {}
        public System.Collections.Generic.IReadOnlyList<NetworkPrefab> PrefabList => new System.Collections.Generic.List<NetworkPrefab>();
    }
}
