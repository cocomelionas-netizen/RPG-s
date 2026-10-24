using System.Collections.Generic;
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
        private static readonly Dictionary<string, Sprite> Art = new Dictionary<string, Sprite>();

        /// <summary>
        /// A piece of the UI art (Resources/UI/Rpg, cut from "RPG UI 1" by System G6 / Qoma, CC0, by Tools/Art/ui.py):
        /// Window, Parchment, Tooltip, Button(Hover), Close/Left/Right/Plus/Minus(Hover), Slot, SlotFrame, Slot&lt;Part&gt;,
        /// BarFrame, BarFill. Null when it is missing (the plain look is used then).
        /// </summary>
        public static Sprite UiArt(string name)
        {
            if (Art.TryGetValue(name, out Sprite sprite)) return sprite;
            sprite = Resources.Load<Sprite>("UI/Rpg/" + name);
            Art[name] = sprite;
            return sprite;
        }

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
            Sprite frame = UiArt("BarFrame");
            Sprite shaded = UiArt("BarFill");

            Image fillImage = CreateImage("Fill", background.transform, fillColor);
            fillImage.raycastTarget = false;
            fill = fillImage.rectTransform;
            Stretch(fill);

            if (frame != null && shaded != null)
            {
                // A steel frame around a shaded fill of the bar's colour.
                fillImage.sprite = shaded;
                var framed = CreateImage("Frame", background.transform, Color.white);
                framed.raycastTarget = false;
                framed.sprite = frame;
                framed.type = Image.Type.Sliced;
                Stretch(framed.rectTransform);
                fill.offsetMin = new Vector2(2f, 2f);
                fill.offsetMax = new Vector2(-2f, -2f);
            }
            else
            {
                background.gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.27f, 0.16f, 0.9f);
                // A lighter top half gives the bar some depth.
                Image shine = CreateImage("Shine", fillImage.transform, new Color(1f, 1f, 1f, 0.12f));
                shine.raycastTarget = false;
                shine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                shine.rectTransform.anchorMax = Vector2.one;
                shine.rectTransform.offsetMin = shine.rectTransform.offsetMax = Vector2.zero;
            }

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

        /// <summary>
        /// An item or spell icon filling <paramref name="parent"/> less <paramref name="inset"/> on each side; hidden until
        /// <see cref="SetIcon"/> gives it a sprite. Never catches the pointer.
        /// </summary>
        public static Image CreateIcon(Transform parent, float inset)
        {
            Image icon = CreateImage("Icon", parent, Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(inset, inset);
            icon.rectTransform.offsetMax = new Vector2(-inset, -inset);
            icon.enabled = false;
            return icon;
        }

        /// <summary>Shows a sprite in an icon (or hides it for none); true when there is a sprite to show.</summary>
        public static bool SetIcon(Image icon, Sprite sprite, Color tint)
        {
            if (icon == null) return false;
            bool shown = sprite != null;
            if (icon.sprite != sprite) icon.sprite = sprite;
            if (icon.color != tint) icon.color = tint;
            if (icon.enabled != shown) icon.enabled = shown;
            return shown;
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
            button.onClick.AddListener(() => RPG.World.GameAudio.Play(RPG.World.Sfx.Click, null, 0.5f));
            // X, +, -, < and > are the square steel buttons; other coloured buttons with a label the long steel one
            // (golden under the pointer); slots and list rows (no label) and clear tabs stay plain.
            string square = label == "X" ? "Close" : label == "+" ? "Plus" : label == "-" ? "Minus" : label == "<" ? "Left"
                : label == ">" ? "Right" : null;
            bool pictured = square != null && UiArt(square) != null;
            if (pictured)
                Swap(button, background, UiArt(square), UiArt(square + "Hover"), false);
            else if (color.a > 0.01f && !string.IsNullOrEmpty(label) && UiArt("Button") != null)
            {
                Swap(button, background, UiArt("Button"), UiArt("ButtonHover"), true);
                // A hint of the button's own colour over the steel.
                background.color = Color.Lerp(Color.white, new Color(color.r, color.g, color.b, 1f), 0.3f);
            }
            else if (color.a > 0.01f && !string.IsNullOrEmpty(label) && ButtonSprite != null)
            {
                background.sprite = ButtonSprite;
                background.type = Image.Type.Sliced;
            }
            if (!string.IsNullOrEmpty(label))
            {
                Text text = CreateText("Label", background.transform, fontSize, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
                // The square buttons draw their sign; the label stays (empty) for code that styles it.
                text.text = pictured ? "" : label;
            }
            return button;
        }

        /// <summary>
        /// Draws the ornate gold frame around a window or panel, a little outside its edge and over its contents
        /// (it moves to the top when the window first shows). Without the frame sprite the panel keeps an outline.
        /// </summary>
        public static Image AddFrame(Image panel, float outset = 6f, Color? color = null)
        {
            if (UiArt("Window") != null)
            {
                // The stone window with its steel frame.
                panel.sprite = UiArt("Window");
                panel.type = Image.Type.Sliced;
                panel.color = Color.white;
                return null;
            }
            if (WindowFrameSprite == null)
            {
                panel.gameObject.AddComponent<Outline>().effectColor = color ?? FrameColor;
                return null;
            }
            Image frame = CreateImage("Frame", panel.transform, color ?? FrameColor);
            frame.sprite = WindowFrameSprite;
            frame.type = Image.Type.Sliced;
            // Only the border is drawn, not a transparent quad over the whole window.
            frame.fillCenter = false;
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

        /// <summary>
        /// Draws a popup (a tooltip) over every window, whatever window it belongs to: a canvas of its own sorted on top.
        /// </summary>
        public static void MakeTopmost(GameObject popup)
        {
            Canvas canvas = popup.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
        }

        /// <summary>
        /// <paramref name="position"/> (canvas units, from the bottom left of the screen) moved just enough that a popup of
        /// its size and pivot stays on the screen.
        /// </summary>
        public static Vector2 KeepOnScreen(RectTransform popup, Vector2 position, float scale)
        {
            Vector2 screen = new Vector2(Screen.width, Screen.height) / Mathf.Max(0.0001f, scale);
            Vector2 size = popup.sizeDelta;
            Vector2 min = position - Vector2.Scale(popup.pivot, size);
            Vector2 max = min + size;
            if (max.x > screen.x) position.x -= max.x - screen.x;
            if (min.x < 0f) position.x -= min.x;
            if (max.y > screen.y) position.y -= max.y - screen.y;
            if (min.y < 0f) position.y -= min.y;
            return position;
        }

        /// <summary>A sprite button: its hover and pressed look are another sprite.</summary>
        private static void Swap(Button button, Image background, Sprite normal, Sprite hover, bool sliced)
        {
            background.sprite = normal;
            background.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            background.color = Color.white;
            if (hover == null) return;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = hover, pressedSprite = hover };
        }

        /// <summary>A window on parchment (what NPCs say): the same frame as <see cref="AddFrame"/>; its text should be dark.</summary>
        public static bool AddParchment(Image panel)
        {
            Sprite parchment = UiArt("Parchment");
            if (parchment == null)
            {
                AddFrame(panel);
                return false;
            }
            panel.sprite = parchment;
            panel.type = Image.Type.Sliced;
            panel.color = Color.white;
            return true;
        }

        /// <summary>A tooltip's or list row's dark box with a thin steel frame (keeps its colour's alpha).</summary>
        public static void StyleBox(Image box)
        {
            Sprite sprite = UiArt("Tooltip");
            if (sprite == null) return;
            box.sprite = sprite;
            box.type = Image.Type.Sliced;
            box.color = new Color(1f, 1f, 1f, Mathf.Max(0.85f, box.color.a));
        }

        /// <summary>
        /// Draws the steel slot frame over an item slot (under its icon); the slot's own colour (empty, quality) shows
        /// inside it. <paramref name="placeholder"/> (Head, Chest, ... from Resources/UI/Rpg/Slot&lt;Part&gt;) is the
        /// silhouette of an empty equipment slot; returned so it can be hidden while the slot holds something.
        /// </summary>
        public static Image MakeSlot(Button slot, string placeholder = null)
        {
            Image hint = null;
            Sprite silhouette = placeholder != null ? UiArt("Slot" + placeholder) : null;
            if (silhouette != null)
            {
                hint = CreateImage("Placeholder", slot.transform, new Color(1f, 1f, 1f, 0.55f));
                hint.raycastTarget = false;
                hint.sprite = silhouette;
                Stretch(hint.rectTransform);
                hint.transform.SetAsFirstSibling();
            }
            Sprite frame = UiArt("SlotFrame");
            if (frame != null)
            {
                Image border = CreateImage("Frame", slot.transform, Color.white);
                border.raycastTarget = false;
                border.sprite = frame;
                Stretch(border.rectTransform);
                border.transform.SetSiblingIndex(hint != null ? 1 : 0);
            }
            return hint;
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
