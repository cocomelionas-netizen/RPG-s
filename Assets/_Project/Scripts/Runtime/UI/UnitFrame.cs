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

    /// <summary>Screen frame with a unit's name and health bar.</summary>
    public class UnitFrame : MonoBehaviour
    {
        [SerializeField] private UnitFrameRole role;
        [Tooltip("The visible panel; hidden while nothing is bound. Must not be this object, so the frame keeps updating.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text nameText;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;

        private static readonly Color HostileColor = new Color(0.8f, 0.2f, 0.15f);
        private static readonly Color FriendlyColor = new Color(0.25f, 0.7f, 0.3f);

        private Health bound;

        public UnitFrameRole Role => role;

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

        private static string LevelText(Health unit)
        {
            if (unit.TryGetComponent(out CharacterProgress progress)) return $"Lv {progress.Level} {progress.ClassName}";
            if (unit.TryGetComponent(out MonsterAI monster)) return $"Lv {monster.Level}";
            return "";
        }

        private void LateUpdate()
        {
            bool show = bound != null && bound.IsSpawned;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;

            nameText.text = $"{bound.DisplayName}   {LevelText(bound)}";
            fill.anchorMax = new Vector2(bound.Fraction, 1f);
            fillImage.color = bound.Faction == Faction.Player ? FriendlyColor : HostileColor;
            valueText.text = bound.IsDead ? "Dead" : $"{bound.Current} / {bound.Max}";
        }
    }
}
