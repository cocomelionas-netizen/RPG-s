using System.Collections.Generic;
using RPG.Characters;
using RPG.Combat;
using RPG.Monsters;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    public enum UnitFrameRole
    {
        /// <summary>Bound to the local player when it spawns.</summary>
        LocalPlayer,
        /// <summary>Follows the local player's selected target.</summary>
        Target,
    }

    /// <summary>
    /// Screen frame of a unit: a round portrait (the unit's standing, front-facing sprite) with its level, the name, and health
    /// and mana bars. The player's frame has the portrait on the left, the target's on the right.
    /// Builds its own UI; place it on a RectTransform about 440 x 110 in the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UnitFrame : MonoBehaviour
    {
        [SerializeField] private UnitFrameRole role;
        [SerializeField, Min(40f)] private float portraitSize = 96f;
        [SerializeField, Min(100f)] private float barWidth = 300f;

        private static readonly Color HealthColor = new Color(0.62f, 0.1f, 0.08f);
        private static readonly Color ManaColor = new Color(0.12f, 0.3f, 0.85f);
        private static readonly Color BorderColor = new Color(0.55f, 0.42f, 0.22f);
        // The source game's name colours: enemies red, players light blue.
        private static readonly Color HostileName = new Color32(0xB6, 0x3E, 0x3D, 0xFF);
        private static readonly Color FriendlyName = new Color32(0x8E, 0xAC, 0xD2, 0xFF);
        /// <summary>Height of a character sprite shown in the portrait, in portrait diameters (shows head and shoulders).</summary>
        private const float FaceZoom = 2.3f;

        private Health bound;
        private GameObject panel;
        private Text nameText;
        private Text levelText;
        private RectTransform healthFill;
        private Text healthLabel;
        private GameObject manaBar;
        private RectTransform manaFill;
        private Text manaLabel;
        private Image face;
        // A hero drawn in layers (HeroAppearance) shows each layer in its portrait.
        private HeroAppearance hero;
        private Health heroOf;
        private readonly List<Sprite> heroLayers = new List<Sprite>();
        private readonly List<Sprite> shownLayers = new List<Sprite>();
        private readonly List<Image> layerImages = new List<Image>();

        public UnitFrameRole Role => role;

        private void Awake()
        {
            bool mirrored = role == UnitFrameRole.Target;
            panel = UIFactory.CreateRect("Panel", transform).gameObject;
            UIFactory.Stretch((RectTransform)panel.transform);

            // Portrait: a gold ring, a dark disc that masks the face, and a level badge.
            Vector2 side = mirrored ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            Image ring = UIFactory.CreateImage("Portrait", panel.transform, BorderColor);
            ring.sprite = UIFactory.CircleSprite;
            ring.raycastTarget = false;
            UIFactory.Place(ring.rectTransform, side, Vector2.zero, Vector2.one * (portraitSize + 8f));

            Image disc = UIFactory.CreateImage("Mask", ring.transform, new Color(0.1f, 0.09f, 0.08f));
            disc.sprite = UIFactory.CircleSprite;
            disc.raycastTarget = false;
            UIFactory.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * portraitSize);
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            face = UIFactory.CreateImage("Face", disc.transform, Color.white);
            face.raycastTarget = false;
            face.rectTransform.anchorMin = face.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            face.rectTransform.pivot = new Vector2(0.5f, 0.72f);
            face.rectTransform.anchoredPosition = Vector2.zero;

            Image badge = UIFactory.CreateImage("Level", ring.transform, new Color(0.12f, 0.08f, 0.05f));
            badge.sprite = UIFactory.CircleSprite;
            badge.raycastTarget = false;
            UIFactory.Place(badge.rectTransform, mirrored ? new Vector2(0f, 0f) : new Vector2(1f, 0f), Vector2.zero, new Vector2(32f, 32f));
            badge.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            levelText = UIFactory.CreateText("Value", badge.transform, 15, TextAnchor.MiddleCenter);
            UIFactory.Stretch(levelText.rectTransform);
            levelText.fontStyle = FontStyle.Bold;

            // Name and bars beside the portrait.
            float barsX = (portraitSize + 14f) * (mirrored ? -1f : 1f);
            Vector2 barAnchor = mirrored ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);

            nameText = UIFactory.CreateText("Name", panel.transform, 19, mirrored ? TextAnchor.LowerRight : TextAnchor.LowerLeft);
            UIFactory.Place(nameText.rectTransform, barAnchor, new Vector2(barsX, 34f), new Vector2(barWidth, 26f));
            nameText.fontStyle = FontStyle.Bold;

            Image healthBar = UIFactory.CreateBar("Health", panel.transform, HealthColor, 14, out healthFill, out healthLabel);
            UIFactory.Place(healthBar.rectTransform, barAnchor, new Vector2(barsX, 8f), new Vector2(barWidth, 22f));

            Image manaBackground = UIFactory.CreateBar("Mana", panel.transform, ManaColor, 13, out manaFill, out manaLabel);
            UIFactory.Place(manaBackground.rectTransform, barAnchor, new Vector2(barsX, -14f), new Vector2(barWidth, 16f));
            manaBar = manaBackground.gameObject;

            // Buffs and debuffs under the bars: an icon each, its seconds left and stacks; the pointer over one shows what it is.
            effectsRow = UIFactory.CreateRect("Effects", panel.transform);
            UIFactory.Place(effectsRow, barAnchor, new Vector2(barsX, -42f), new Vector2(barWidth, EffectIconSize));
            effectsMirrored = mirrored;

            panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (role != UnitFrameRole.Target) return;
            PlayerTargeting.LocalTargetChanged += Bind;
            Bind(PlayerTargeting.LocalTarget);
        }

        private void OnDisable()
        {
            if (role == UnitFrameRole.Target)
                PlayerTargeting.LocalTargetChanged -= Bind;
        }

        public void Bind(Health health)
        {
            bound = health;
        }

        private static int LevelOf(Health unit)
        {
            if (unit.TryGetComponent(out CharacterProgress progress)) return progress.Level;
            if (unit.TryGetComponent(out MonsterAI monster)) return monster.Level;
            return 0;
        }

        private static string Subtitle(Health unit)
        {
            if (unit.TryGetComponent(out CharacterProgress progress))
                return $"  <size=14>{progress.ClassName} · {LevelCurve.RankName(progress.Level)}</size>";
            return unit.TryGetComponent(out MonsterAI monster) && monster.IsElite ? "  <size=14>Elite</size>" : "";
        }

        private void LateUpdate()
        {
            bool show = bound != null && bound.IsSpawned;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show)
            {
                hoveredEffect = null;
                if (effectTooltip != null && effectTooltip.gameObject.activeSelf) effectTooltip.gameObject.SetActive(false);
                return;
            }

            // Texts are rewritten only when what they show changed (a new unit, its health or mana), the name and the
            // effect timers a few times a second.
            bool newUnit = bound != shownUnit;
            if (newUnit)
            {
                shownUnit = bound;
                shownMana = bound.GetComponent<Mana>();
                shownHealth = shownMaxHealth = shownManaValue = shownManaMax = int.MinValue;
            }
            if (newUnit || Time.unscaledTime >= nextNameRefresh)
            {
                nextNameRefresh = Time.unscaledTime + 0.5f;
                nameText.text = bound.DisplayName + Subtitle(bound);
                nameText.color = bound.Faction == Faction.Player ? FriendlyName : HostileName;
                int level = LevelOf(bound);
                levelText.text = level > 0 ? level.ToString() : "";
                levelText.transform.parent.gameObject.SetActive(level > 0);
            }

            healthFill.anchorMax = new Vector2(bound.Fraction, 1f);
            int current = bound.IsDead ? -1 : bound.Current;
            if (current != shownHealth || bound.Max != shownMaxHealth)
            {
                shownHealth = current;
                shownMaxHealth = bound.Max;
                healthLabel.text = bound.IsDead ? "Dead" : $"{bound.Current} / {bound.Max}   ({Mathf.RoundToInt(bound.Fraction * 100f)}%)";
            }

            Mana mana = shownMana;
            bool hasMana = mana != null && mana.IsSpawned && mana.Max > 0;
            if (manaBar.activeSelf != hasMana) manaBar.SetActive(hasMana);
            if (hasMana)
            {
                manaFill.anchorMax = new Vector2(mana.Fraction, 1f);
                if (mana.Current != shownManaValue || mana.Max != shownManaMax)
                {
                    shownManaValue = mana.Current;
                    shownManaMax = mana.Max;
                    manaLabel.text = $"{mana.Current} / {mana.Max}";
                }
            }

            UpdateFace();
            if (newUnit || Time.unscaledTime >= nextEffectsRefresh)
            {
                nextEffectsRefresh = Time.unscaledTime + 0.25f;
                UpdateEffects();
            }
            PlaceEffectTooltip();
        }

        private Health shownUnit;
        private Mana shownMana;
        private int shownHealth;
        private int shownMaxHealth;
        private int shownManaValue;
        private int shownManaMax;
        private float nextNameRefresh;
        private float nextEffectsRefresh;

        private const float EffectIconSize = 30f;
        private const int MaxEffectIcons = 10;
        private static readonly Color DebuffBorder = new Color(0.9f, 0.2f, 0.15f);
        private static readonly Color BuffBorder = new Color(0.3f, 0.85f, 0.35f);
        private RectTransform effectsRow;
        private bool effectsMirrored;

        /// <summary>One buff or debuff icon: its picture (or its colour), the time left and stacks, and what it shows.</summary>
        private class EffectIcon
        {
            public GameObject Root;
            public Image Background;
            public Image Picture;
            public Outline Border;
            public Text Time;
            public Text Stacks;
            public StatusView View;
        }

        private readonly List<EffectIcon> effectIcons = new List<EffectIcon>();
        private EffectIcon hoveredEffect;
        private RectTransform effectTooltip;
        private Text effectTooltipText;

        private EffectIcon CreateEffectIcon(int index)
        {
            var icon = new EffectIcon();
            icon.Background = UIFactory.CreateImage("Effect", effectsRow, new Color(0.05f, 0.05f, 0.05f, 0.95f));
            icon.Root = icon.Background.gameObject;
            Vector2 corner = effectsMirrored ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            float x = index * (EffectIconSize + 4f) * (effectsMirrored ? -1f : 1f);
            UIFactory.Place(icon.Background.rectTransform, corner, new Vector2(x, 0f), new Vector2(EffectIconSize, EffectIconSize));
            icon.Border = icon.Root.AddComponent<Outline>();
            icon.Border.effectDistance = new Vector2(2f, -2f);
            icon.Picture = UIFactory.CreateIcon(icon.Background.transform, 1f);
            icon.Time = UIFactory.CreateText("Time", icon.Background.transform, 11, TextAnchor.LowerCenter);
            UIFactory.Place(icon.Time.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -15f), new Vector2(EffectIconSize + 16f, 16f));
            icon.Stacks = UIFactory.CreateText("Stacks", icon.Background.transform, 11, TextAnchor.UpperRight);
            UIFactory.Stretch(icon.Stacks.rectTransform);
            icon.Stacks.rectTransform.offsetMax = new Vector2(-1f, 1f);
            // The pointer over it shows what it is.
            HoverArea hover = icon.Root.AddComponent<HoverArea>();
            hover.Entered += _ => hoveredEffect = icon;
            hover.Exited += _ =>
            {
                if (hoveredEffect == icon) hoveredEffect = null;
            };
            return icon;
        }

        private void UpdateEffects()
        {
            int shown = 0;
            StatusEffects effects = bound.Effects;
            double now = bound.NetworkManager != null ? bound.NetworkManager.ServerTime.Time : 0.0;
            if (effects != null)
            {
                foreach (StatusView view in effects.Views)
                {
                    if (shown >= MaxEffectIcons) break;
                    if (shown == effectIcons.Count) effectIcons.Add(CreateEffectIcon(shown));
                    EffectIcon icon = effectIcons[shown];
                    if (!icon.Root.activeSelf) icon.Root.SetActive(true);
                    icon.View = view;
                    // Its picture (a spell's own, else one for its kind); without one, its colour.
                    bool pictured = UIFactory.SetIcon(icon.Picture, StatusInfo.IconOf(view), Color.white);
                    Color color = view.Color;
                    icon.Background.color = pictured ? new Color(0.05f, 0.05f, 0.05f, 0.95f) : new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.95f);
                    // Red frame for debuffs, green for buffs.
                    icon.Border.effectColor = view.Harmful ? DebuffBorder : BuffBorder;
                    double left = view.EndsAt - now;
                    string time = left < 60 ? $"{System.Math.Ceiling(System.Math.Max(0, left)):0}" : left < 3600 ? $"{System.Math.Ceiling(left / 60):0}m" : "";
                    if (icon.Time.text != time) icon.Time.text = time;
                    string name = view.Name.ToString();
                    string stacks = view.Stacks > 1 ? view.Stacks.ToString() : pictured ? "" : name.Substring(0, Mathf.Min(2, name.Length));
                    if (icon.Stacks.text != stacks) icon.Stacks.text = stacks;
                    shown++;
                }
            }
            for (int i = shown; i < effectIcons.Count; i++)
                if (effectIcons[i].Root.activeSelf)
                {
                    effectIcons[i].Root.SetActive(false);
                    if (hoveredEffect == effectIcons[i]) hoveredEffect = null;
                }
            UpdateEffectTooltip(now);
        }

        /// <summary>The hovered effect's name, the source game's text for it, what it does and the time left, by the pointer.</summary>
        private void UpdateEffectTooltip(double now)
        {
            if (hoveredEffect == null || !hoveredEffect.Root.activeSelf)
            {
                if (effectTooltip != null && effectTooltip.gameObject.activeSelf) effectTooltip.gameObject.SetActive(false);
                return;
            }
            if (effectTooltip == null)
            {
                Canvas root = GetComponentInParent<Canvas>();
                Image background = UIFactory.CreateImage("Effect Tooltip", root != null ? root.rootCanvas.transform : transform, new Color(0f, 0f, 0f, 0.92f));
                UIFactory.StyleBox(background);
                UIFactory.MakeTopmost(background.gameObject);
                background.raycastTarget = false;
                effectTooltip = background.rectTransform;
                effectTooltip.anchorMin = effectTooltip.anchorMax = Vector2.zero;
                effectTooltip.pivot = new Vector2(0f, 1f);
                effectTooltipText = UIFactory.CreateText("Text", effectTooltip, 15, TextAnchor.UpperLeft);
                UIFactory.Stretch(effectTooltipText.rectTransform);
                effectTooltipText.rectTransform.offsetMin = new Vector2(12f, 8f);
                effectTooltipText.rectTransform.offsetMax = new Vector2(-12f, -8f);
            }
            if (!effectTooltip.gameObject.activeSelf) effectTooltip.gameObject.SetActive(true);
            string text = StatusInfo.Tooltip(hoveredEffect.View, now);
            if (effectTooltipText.text != text)
            {
                effectTooltipText.text = text;
                const float width = 300f;
                effectTooltip.sizeDelta = new Vector2(width, 40f);
                effectTooltip.sizeDelta = new Vector2(width, Mathf.Ceil(effectTooltipText.preferredHeight) + 18f);
            }
            PlaceEffectTooltip();
        }

        private void PlaceEffectTooltip()
        {
            if (effectTooltip == null || !effectTooltip.gameObject.activeSelf || hoveredEffect == null) return;
            Canvas root = effectTooltip.GetComponentInParent<Canvas>().rootCanvas;
            float scale = root != null ? root.scaleFactor : 1f;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : (Vector2)hoveredEffect.Root.transform.position;
            effectTooltip.anchoredPosition = UIFactory.KeepOnScreen(effectTooltip, pointer / scale + new Vector2(14f, -14f), scale);
        }

        /// <summary>Shows the unit standing and facing the viewer, zoomed on the head; it does not animate.</summary>
        private void UpdateFace()
        {
            if (heroOf != bound)
            {
                heroOf = bound;
                hero = bound.GetComponent<HeroAppearance>();
            }
            if (hero != null && hero.HasLayers)
            {
                UpdateLayeredFace();
                return;
            }
            ShowLayers(0);

            Sprite sprite = PortraitSprite(bound);
            face.enabled = sprite != null;
            if (sprite == null) return;

            if (face.sprite != sprite)
            {
                face.sprite = sprite;
                float height = portraitSize * FaceZoom;
                float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
                face.rectTransform.sizeDelta = new Vector2(height * aspect, height);
            }
            face.color = bound.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        }

        /// <summary>The hero's layers standing and facing the viewer, lined up at the feet and zoomed like a single sprite.</summary>
        private void UpdateLayeredFace()
        {
            face.enabled = false;
            hero.PortraitLayers(heroLayers);
            bool same = heroLayers.Count == shownLayers.Count;
            for (int i = 0; same && i < heroLayers.Count; i++) same = heroLayers[i] == shownLayers[i];
            if (!same)
            {
                shownLayers.Clear();
                shownLayers.AddRange(heroLayers);
                ShowLayers(heroLayers.Count);
                if (heroLayers.Count == 0) return;

                // Bounds of all layers around the feet, in units.
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (Sprite sprite in heroLayers)
                {
                    Bounds b = sprite.bounds;
                    minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x);
                    minY = Mathf.Min(minY, b.min.y); maxY = Mathf.Max(maxY, b.max.y);
                }
                float scale = portraitSize * FaceZoom / Mathf.Max(0.01f, maxY - minY);
                // The point 72% up the figure sits at the disc's centre, like a single sprite's (face pivot).
                var feet = new Vector2(-(minX + maxX) * 0.5f * scale, -(minY + (maxY - minY) * 0.72f) * scale);
                for (int i = 0; i < heroLayers.Count; i++)
                {
                    Sprite sprite = heroLayers[i];
                    RectTransform rect = layerImages[i].rectTransform;
                    layerImages[i].sprite = sprite;
                    rect.pivot = sprite.pivot / sprite.rect.size;
                    rect.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * scale;
                    rect.anchoredPosition = feet;
                }
            }
            Color color = bound.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            for (int i = 0; i < shownLayers.Count; i++) layerImages[i].color = color;
        }

        /// <summary>Shows the first <paramref name="count"/> layer images (made as needed), hides the rest.</summary>
        private void ShowLayers(int count)
        {
            if (count == 0) shownLayers.Clear();
            while (layerImages.Count < count)
            {
                Image image = UIFactory.CreateImage("Layer", face.transform.parent, Color.white);
                image.raycastTarget = false;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                layerImages.Add(image);
            }
            for (int i = 0; i < layerImages.Count; i++)
                if (layerImages[i].gameObject.activeSelf != i < count) layerImages[i].gameObject.SetActive(i < count);
        }

        /// <summary>The first idle frame facing south; units without animations show their sprite as it is.</summary>
        private static Sprite PortraitSprite(Health unit)
        {
            var animator = unit.GetComponent<CharacterAnimator>();
            CharacterAnimationSet set = animator != null ? animator.AnimationSet : null;
            if (set != null && set.TryGetFrames(CharacterAnimationSet.IdleClip, Direction8.South, out _, out Sprite[] frames, out _)
                && frames != null && frames.Length > 0 && frames[0] != null)
                return frames[0];

            var body = unit.GetComponent<SpriteRenderer>();
            return body != null ? body.sprite : null;
        }
    }
}
