using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The message box in the bottom left: loot, gold, experience, level ups and other news for the local player,
    /// newest at the bottom. Anything can call <see cref="Add"/>. Builds its own UI; place it on a RectTransform
    /// about 480 x 180 in the bottom left of the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MessageLog : MonoBehaviour
    {
        [SerializeField, Min(1)] private int visibleLines = 9;

        private const int KeptLines = 50;
        private static readonly List<string> Lines = new List<string>();
        private static int version;

        private Text text;
        private int shownVersion = -1;

        public static readonly Color InfoColor = new Color(0.85f, 0.85f, 0.8f);
        public static readonly Color WarningColor = new Color(1f, 0.45f, 0.35f);

        /// <summary>Adds a line (kept even before the box exists, so nothing from the login is lost).</summary>
        public static void Add(string message, Color color)
        {
            if (string.IsNullOrEmpty(message)) return;
            Lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>");
            if (Lines.Count > KeptLines) Lines.RemoveAt(0);
            version++;
        }

        public static void Add(string message) => Add(message, InfoColor);

        private void Awake()
        {
            Image panel = UIFactory.CreateImage("Panel", transform, new Color(0.04f, 0.03f, 0.03f, 0.6f));
            panel.raycastTarget = false;
            UIFactory.Stretch(panel.rectTransform);
            UIFactory.AddFrame(panel);

            text = UIFactory.CreateText("Lines", panel.transform, 15, TextAnchor.LowerLeft);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10f, 8f);
            text.rectTransform.offsetMax = new Vector2(-10f, -6f);
            text.verticalOverflow = VerticalWrapMode.Truncate;

            if (Lines.Count == 0)
                Add("Welcome! C: character, P: spells, I: bag, M: map, 1: attack, 2-4 and 7-0: spells, 5-6: potions, F9: cheats.", new Color(1f, 0.85f, 0.45f));
        }

        private void LateUpdate()
        {
            if (shownVersion == version) return;
            shownVersion = version;
            int first = Mathf.Max(0, Lines.Count - visibleLines);
            text.text = string.Join("\n", Lines.GetRange(first, Lines.Count - first));
        }
    }
}
