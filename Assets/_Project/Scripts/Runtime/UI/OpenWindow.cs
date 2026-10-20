using RPG.Controls;
using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// Marks a window (on its panel): while one is open, or in the frame one closed, Esc belongs to the windows — it
    /// closes them and does not also drop the target or cancel a cast. An Esc that only cancelled something in a window
    /// (a gem being set, a spell being placed on the bar: <see cref="HandleEscape"/>) closes nothing. The windows that can
    /// cancel such a thing run first (<c>DefaultExecutionOrder(-20)</c>), the ones that close on Esc after them.
    /// </summary>
    public sealed class OpenWindow : MonoBehaviour
    {
        private static int open;
        private static int lastClosedFrame = -1;
        private static int escapeHandledFrame = -1;

        /// <summary>True when this frame's Esc is for a window (one is open, or one just closed).</summary>
        public static bool EscapeUsedByWindow => open > 0 || lastClosedFrame == Time.frameCount || EscapeHandled;

        /// <summary>True when this frame's Esc already cancelled something and must close nothing else.</summary>
        public static bool EscapeHandled => escapeHandledFrame == Time.frameCount;

        /// <summary>This frame's Esc was used to cancel something: no window closes on it.</summary>
        public static void HandleEscape() => escapeHandledFrame = Time.frameCount;

        /// <summary>True when Esc was pressed this frame and should close a window (it cancelled nothing first).</summary>
        public static bool EscapeCloses(PlayerInputReader input) => input != null && input.CancelPressed && !EscapeHandled;

        private void OnEnable() => open++;

        private void OnDisable()
        {
            open = Mathf.Max(0, open - 1);
            lastClosedFrame = Time.frameCount;
        }
    }
}
