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
    /// Screen frame of a unit: a round portrait (the unit's current sprite) with its level, the name, and health
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
        private static readonly Color HostileName = new Color(0.95f, 0.3f, 0.22f);
        private static readonly Color FriendlyName = new Color(0.55f, 0.85f, 1f);
        private static readonly Color NeutralName = new Color(1f, 0.85f, 0.45f);
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
            return unit.TryGetComponent(out CharacterProgress progress) ? $"  <size=14>{progress.ClassName}</size>" : "";
        }

        private void LateUpdate()
        {
            bool show = bound != null && bound.IsSpawned;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;

            nameText.text = bound.DisplayName + Subtitle(bound);
            nameText.color = role == UnitFrameRole.LocalPlayer ? NeutralName
                : bound.Faction == Faction.Player ? FriendlyName : HostileName;
            int level = LevelOf(bound);
            levelText.text = level > 0 ? level.ToString() : "";
            levelText.transform.parent.gameObject.SetActive(level > 0);

            healthFill.anchorMax = new Vector2(bound.Fraction, 1f);
            healthLabel.text = bound.IsDead ? "Dead" : $"{bound.Current} / {bound.Max}   ({Mathf.RoundToInt(bound.Fraction * 100f)}%)";

            bool hasMana = bound.TryGetComponent(out Mana mana) && mana.IsSpawned && mana.Max > 0;
            if (manaBar.activeSelf != hasMana) manaBar.SetActive(hasMana);
            if (hasMana)
            {
                manaFill.anchorMax = new Vector2(mana.Fraction, 1f);
                manaLabel.text = $"{mana.Current} / {mana.Max}";
            }

            UpdateFace();
        }

        /// <summary>Shows the unit's current animation frame, zoomed on the head.</summary>
        private void UpdateFace()
        {
            var body = bound.GetComponent<SpriteRenderer>();
            Sprite sprite = body != null ? body.sprite : null;
            face.enabled = sprite != null;
            if (sprite == null) return;

            face.sprite = sprite;
            float height = portraitSize * FaceZoom;
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            face.rectTransform.sizeDelta = new Vector2(height * aspect, height);
            // Face the middle of the screen, like the bars.
            bool facingLeft = body.flipX;
            bool wantLeft = role == UnitFrameRole.Target;
            face.rectTransform.localScale = new Vector3(facingLeft == wantLeft ? 1f : -1f, 1f, 1f);
            face.color = bound.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        }
    }
}
