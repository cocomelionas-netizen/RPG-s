// Packages the editor scripts use that have no reference DLL here (URP, the Input System's UI module), and the one
// build API newer than the UnityEditor.dll reference.
namespace UnityEngine.Rendering.Universal
{
    public class Renderer2DData : UnityEngine.ScriptableObject {}
    public class UniversalRenderPipelineAsset : UnityEngine.ScriptableObject {}
    public class UniversalAdditionalCameraData : UnityEngine.MonoBehaviour {}
    public class Light2D : UnityEngine.MonoBehaviour { public enum LightType { Global } public LightType lightType; public float intensity; }
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
    }
}
