using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPG.CameraSystem
{
    /// <summary>
    /// Keeps the world from being drawn at more than about 1080 pixels high: on a big or high-DPI screen (a browser in full
    /// screen draws at the screen's real pixels, 4K and more) the world is drawn smaller and stretched (URP render scale),
    /// while the HUD stays sharp at the full size. HDR is turned off (a 2D game without bloom does not need it, and it
    /// costs much in a browser). Builds only: in the editor it would change the pipeline asset itself.
    /// </summary>
    public class RenderResolution : MonoBehaviour
    {
        /// <summary>The most pixels high the world is drawn at.</summary>
        public const int MaxWorldHeight = 1080;
        private const float CheckInterval = 0.5f;

        private int checkedHeight = -1;
        private float nextCheck;

        /// <summary>The share of the screen's pixels the world is drawn at now (for the F8 overlay).</summary>
        public static float CurrentScale { get; private set; } = 1f;

        /// <summary>The render scale for a screen this many pixels high: 1 up to the cap, less above it.</summary>
        public static float ScaleFor(int screenHeight) =>
            screenHeight <= MaxWorldHeight ? 1f : Mathf.Clamp((float)MaxWorldHeight / screenHeight, 0.25f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            var go = new GameObject("Render Resolution");
            DontDestroyOnLoad(go);
            go.AddComponent<RenderResolution>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckInterval;
            // Entering or leaving full screen changes the height.
            if (Screen.height == checkedHeight) return;
            checkedHeight = Screen.height;

            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)) return;
            pipeline.supportsHDR = false;
            CurrentScale = ScaleFor(Screen.height);
            pipeline.renderScale = CurrentScale;
        }
    }
}
