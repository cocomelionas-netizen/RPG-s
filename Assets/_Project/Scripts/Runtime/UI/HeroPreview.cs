using System;
using System.Collections.Generic;
using RPG.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// A hero drawn on the UI in its layers (<see cref="AvatarCatalog"/>), breathing in its idle animation and turned three
    /// quarters to the viewer: the character screens show each character with the looks of the gear it wears. Its feet stand at the bottom
    /// middle of its RectTransform; the figure is as tall as the rect.
    /// </summary>
    public class HeroPreview : MonoBehaviour
    {
        private static readonly AvatarSlot[] Slots = (AvatarSlot[])Enum.GetValues(typeof(AvatarSlot));

        /// <summary>
        /// Turned a little aside (three quarters): facing the viewer straight, the hero holds a bow or staff behind the body.
        /// </summary>
        private const Direction8 Facing = Direction8.SouthWest;

        private AvatarCatalog catalog;
        private readonly List<Image> images = new List<Image>();
        private readonly List<Sprite[]> frames = new List<Sprite[]>();
        private readonly List<bool> flips = new List<bool>();
        private float framesPerSecond = 6f;
        private float scale;
        private Vector2 feet;
        private int shownFrame = -1;
        private Color tint = Color.white;

        /// <summary>Makes a preview filling <paramref name="parent"/>.</summary>
        public static HeroPreview Create(Transform parent, AvatarCatalog avatars)
        {
            RectTransform rect = UIFactory.CreateRect("Hero", parent);
            UIFactory.Stretch(rect);
            var preview = rect.gameObject.AddComponent<HeroPreview>();
            preview.catalog = avatars;
            return preview;
        }

        /// <summary>The worn looks of a class's starting gear, or of saved equipment, by avatar slot ("" for bare).</summary>
        public static string[] LooksOf(Func<RPG.Items.ItemSlot, RPG.Items.ItemDefinition> worn)
        {
            var looks = new string[Slots.Length];
            foreach (AvatarSlot slot in Slots)
            {
                RPG.Items.ItemDefinition item = worn(HeroAppearance.ItemSlotOf(slot));
                looks[(int)slot] = item != null ? item.appearance ?? "" : "";
            }
            return looks;
        }

        /// <summary>Shows a hero of a gender wearing <paramref name="looks"/> (by avatar slot; empty or missing for bare).</summary>
        public void Show(bool female, string[] looks)
        {
            frames.Clear();
            flips.Clear();
            shownFrame = -1;
            if (catalog != null)
            {
                foreach (AvatarSlot slot in catalog.OrderFor(Facing))
                {
                    string look = looks != null && (int)slot < looks.Length ? looks[(int)slot] : null;
                    CharacterAnimationSet set = HeroAppearance.LookOf(catalog, female, slot, look);
                    if (set == null || !set.TryGetFrames(CharacterAnimationSet.IdleClip, Facing, out DirectionalClip clip,
                            out Sprite[] clipFrames, out bool flipX) || clipFrames.Length == 0)
                        continue;
                    frames.Add(clipFrames);
                    flips.Add(flipX);
                    if (clip != null) framesPerSecond = Mathf.Max(1f, clip.framesPerSecond);
                }
            }
            while (images.Count < frames.Count)
            {
                Image image = UIFactory.CreateImage("Layer", transform, Color.white);
                image.raycastTarget = false;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                images.Add(image);
            }
            for (int i = 0; i < images.Count; i++) images[i].gameObject.SetActive(i < frames.Count);
            Fit();
            Draw(0);
        }

        /// <summary>Greys the hero out (an empty slot, a character being deleted) or brings its colours back.</summary>
        public void SetTint(Color color)
        {
            tint = color;
            for (int i = 0; i < images.Count; i++) images[i].color = tint;
        }

        /// <summary>Scale and place from the first frame of every layer: the figure fills the rect's height.</summary>
        private void Fit()
        {
            if (frames.Count == 0) return;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Sprite[] layer in frames)
            {
                Bounds b = layer[0].bounds;
                minX = Mathf.Min(minX, b.min.x);
                maxX = Mathf.Max(maxX, b.max.x);
                minY = Mathf.Min(minY, b.min.y);
                maxY = Mathf.Max(maxY, b.max.y);
            }
            float height = ((RectTransform)transform).rect.height;
            scale = Mathf.Max(1f, height) / Mathf.Max(0.01f, maxY - minY);
            feet = new Vector2(-(minX + maxX) * 0.5f * scale, -minY * scale);
        }

        private void Update()
        {
            if (frames.Count == 0) return;
            Draw(Mathf.FloorToInt(Time.unscaledTime * framesPerSecond));
        }

        private void Draw(int frame)
        {
            if (frame == shownFrame) return;
            shownFrame = frame;
            for (int i = 0; i < frames.Count; i++)
            {
                Sprite[] layer = frames[i];
                Sprite sprite = layer[frame % layer.Length];
                Image image = images[i];
                RectTransform rect = image.rectTransform;
                image.sprite = sprite;
                image.color = tint;
                // Mirrored around its own pivot (the feet), like a flipped SpriteRenderer.
                rect.pivot = sprite.pivot / sprite.rect.size;
                rect.localScale = new Vector3(flips[i] ? -1f : 1f, 1f, 1f);
                rect.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * scale;
                rect.anchoredPosition = feet;
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (frames.Count == 0) return;
            Fit();
            shownFrame = -1;
        }
    }
}
