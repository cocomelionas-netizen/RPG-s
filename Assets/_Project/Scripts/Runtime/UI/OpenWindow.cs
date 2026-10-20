using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// Marks a window (on its panel): while one is open, or in the frame one closed, Esc belongs to the windows — it
    /// closes them and does not also drop the target or cancel a cast.
    /// </summary>
    public sealed class OpenWindow : MonoBehaviour
    {
        private static int open;
        private static int lastClosedFrame = -1;

        /// <summary>True when this frame's Esc is for a window (one is open, or one just closed).</summary>
        public static bool EscapeUsedByWindow => open > 0 || lastClosedFrame == Time.frameCount;

        private void OnEnable() => open++;

        private void OnDisable()
        {
            open = Mathf.Max(0, open - 1);
            lastClosedFrame = Time.frameCount;
        }
    }
}
