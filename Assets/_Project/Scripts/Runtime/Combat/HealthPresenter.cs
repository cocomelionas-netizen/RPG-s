using RPG.UI;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Client-side look of a <see cref="Health"/>: an overhead health bar (shown when hurt or targeted),
    /// a red flash and a damage number on every hit, and a faded grey body while dead.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HealthPresenter : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [Tooltip("A white sprite with its pivot on the left edge, 1 unit wide.")]
        [SerializeField] private Sprite barSprite;
        [SerializeField] private float barHeightAboveFeet = 0.85f;
        [SerializeField] private Vector2 barSize = new Vector2(0.5f, 0.06f);
        [SerializeField] private float damageNumberHeight = 0.9f;

        private static readonly Color BarBackground = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color FullColor = new Color(0.3f, 0.85f, 0.3f);
        private static readonly Color EmptyColor = new Color(0.9f, 0.2f, 0.15f);
        private static readonly Color FlashColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color DeadColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
        private const float FlashSeconds = 0.15f;
        private const int BarSortingOrder = 500;

        private Health health;
        private Transform barRoot;
        private SpriteRenderer barFill;
        private float flashTimer;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (barSprite != null) CreateBar();
        }

        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable() => health.Damaged -= OnDamaged;

        private void CreateBar()
        {
            barRoot = new GameObject("Health Bar").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(-barSize.x * 0.5f, barHeightAboveFeet, 0f);

            const float border = 0.015f;
            SpriteRenderer background = CreateBarPart("Background", BarBackground, BarSortingOrder);
            background.transform.localPosition = new Vector3(-border, 0f, 0f);
            background.transform.localScale = new Vector3(barSize.x + border * 2f, barSize.y + border * 2f, 1f);

            barFill = CreateBarPart("Fill", FullColor, BarSortingOrder + 1);
        }

        private SpriteRenderer CreateBarPart(string partName, Color color, int sortingOrder)
        {
            var part = new GameObject(partName).AddComponent<SpriteRenderer>();
            part.transform.SetParent(barRoot, false);
            part.sprite = barSprite;
            part.color = color;
            part.sortingOrder = sortingOrder;
            return part;
        }

        private void LateUpdate()
        {
            bool spawned = health.IsSpawned;
            bool dead = health.IsDead;
            float fraction = health.Fraction;

            if (barRoot != null)
            {
                bool show = spawned && !dead && (fraction < 1f || PlayerTargeting.LocalTarget == health);
                barRoot.gameObject.SetActive(show);
                if (show)
                {
                    barFill.transform.localScale = new Vector3(barSize.x * fraction, barSize.y, 1f);
                    barFill.color = Color.Lerp(EmptyColor, FullColor, fraction);
                }
            }

            if (body == null) return;
            flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
            if (dead) body.color = DeadColor;
            else if (flashTimer > 0f) body.color = FlashColor;
            else body.color = Color.white;
        }

        private void OnDamaged(int amount, bool critical)
        {
            flashTimer = FlashSeconds;
            bool hurtsLocalPlayer = health.IsOwner && health.GetComponent<PlayerTargeting>() != null;
            CombatTextLayer.Show(transform.position + Vector3.up * damageNumberHeight, amount, critical, hurtsLocalPlayer);
        }
    }
}
