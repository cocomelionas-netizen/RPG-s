using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>Small helpers for HUD elements that build their own UI at runtime.</summary>
    public static class UIFactory
    {
        private static Font font;
        private static Sprite circle;
        private static Sprite windowFrame;
        private static Sprite buttonShape;
        private static Sprite divider;

        /// <summary>The gold of the window frames (Kenney's Fantasy UI Borders, CC0, tinted).</summary>
        public static readonly Color FrameColor = new Color32(0xC8, 0xA2, 0x62, 0xFF);

        /// <summary>A window's ornate border (nine-sliced; Resources/UI/WindowFrame.png).</summary>
        public static Sprite WindowFrameSprite => windowFrame != null ? windowFrame : windowFrame = Resources.Load<Sprite>("UI/WindowFrame");

        /// <summary>A button's shape with cut corners (nine-sliced; Resources/UI/Button.png), tinted with the button's colour.</summary>
        public static Sprite ButtonSprite => buttonShape != null ? buttonShape : buttonShape = Resources.Load<Sprite>("UI/Button");

        /// <summary>A fading divider line (Resources/UI/Divider.png).</summary>
        public static Sprite DividerSprite => divider != null ? divider : divider = Resources.Load<Sprite>("UI/Divider");

        public static Font DefaultFont => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>A white, smooth-edged disc (for round portraits, masks and map dots).</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (circle != null) return circle;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                float radius = size * 0.5f;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                return circle;
            }
        }

        /// <summary>A rect anchored and pivoted at one point of its parent.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>A bar: dark background, a fill that grows from the left (set its anchorMax.x) and a centered label.</summary>
        public static Image CreateBar(string name, Transform parent, Color fillColor, int fontSize, out RectTransform fill, out Text label)
        {
            Image background = CreateImage(name, parent, new Color(0.03f, 0.02f, 0.02f, 0.85f));
            background.raycastTarget = false;
            background.gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.27f, 0.16f, 0.9f);

            Image fillImage = CreateImage("Fill", background.transform, fillColor);
            fillImage.raycastTarget = false;
            fill = fillImage.rectTransform;
            Stretch(fill);

            // A lighter top half gives the bar some depth.
            Image shine = CreateImage("Shine", fillImage.transform, new Color(1f, 1f, 1f, 0.12f));
            shine.raycastTarget = false;
            shine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.offsetMin = shine.rectTransform.offsetMax = Vector2.zero;

            label = CreateText("Label", background.transform, fontSize, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return background;
        }

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
            // Coloured buttons get the cut-corner shape; clear ones (tabs, slots) stay as they are.
            if (color.a > 0.01f && ButtonSprite != null)
            {
                background.sprite = ButtonSprite;
                background.type = Image.Type.Sliced;
            }
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

        /// <summary>
        /// Draws the ornate gold frame around a window or panel, a little outside its edge and over its contents
        /// (it moves to the top when the window first shows). Without the frame sprite the panel keeps an outline.
        /// </summary>
        public static Image AddFrame(Image panel, float outset = 6f, Color? color = null)
        {
            if (WindowFrameSprite == null)
            {
                panel.gameObject.AddComponent<Outline>().effectColor = color ?? FrameColor;
                return null;
            }
            Image frame = CreateImage("Frame", panel.transform, color ?? FrameColor);
            frame.sprite = WindowFrameSprite;
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            Stretch(frame.rectTransform);
            frame.rectTransform.offsetMin = new Vector2(-outset, -outset);
            frame.rectTransform.offsetMax = new Vector2(outset, outset);
            frame.gameObject.AddComponent<DrawnLast>();
            return frame;
        }

        /// <summary>A fading gold divider line across a panel, centred at <paramref name="y"/> from its top.</summary>
        public static Image AddDivider(RectTransform parent, float y, float width)
        {
            Image line = CreateImage("Divider", parent, FrameColor);
            line.raycastTarget = false;
            if (DividerSprite != null) line.sprite = DividerSprite;
            Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(width, DividerSprite != null ? 22f : 2f));
            return line;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Moves its object to the end of its parent's children when it first shows, so it is drawn over them.</summary>
        private sealed class DrawnLast : MonoBehaviour
        {
            private void Start() => transform.SetAsLastSibling();
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
