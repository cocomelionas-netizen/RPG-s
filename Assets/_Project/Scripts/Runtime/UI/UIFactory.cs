using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>Small helpers for HUD elements that build their own UI at runtime.</summary>
    public static class UIFactory
    {
        private static Font font;

        public static Font DefaultFont => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
        {
            Text text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }

        public static Button CreateButton(string name, Transform parent, Color color, string label, int fontSize)
        {
            Image background = CreateImage(name, parent, color);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            if (!string.IsNullOrEmpty(label))
            {
                Text text = CreateText("Label", background.transform, fontSize, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
                text.text = label;
            }
            return button;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>A component on the local player's character, or null before it has spawned.</summary>
        public static T LocalPlayer<T>() where T : Component
        {
            NetworkManager network = NetworkManager.Singleton;
            NetworkObject player = network != null && network.IsListening && network.LocalClient != null ? network.LocalClient.PlayerObject : null;
            return player != null ? player.GetComponent<T>() : null;
        }
    }
}
