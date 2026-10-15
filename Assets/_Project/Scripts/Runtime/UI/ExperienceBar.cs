using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>The local player's level, class and experience towards the next level. Builds its own UI.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class ExperienceBar : MonoBehaviour
    {
        private static readonly Color FillColor = new Color(0.55f, 0.35f, 0.95f);

        private GameObject panel;
        private RectTransform fill;
        private Text label;

        private void Awake()
        {
            Image background = UIFactory.CreateImage("Background", transform, new Color(0f, 0f, 0f, 0.75f));
            background.raycastTarget = false;
            UIFactory.Stretch(background.rectTransform);
            panel = background.gameObject;

            Image fillImage = UIFactory.CreateImage("Fill", background.transform, FillColor);
            fillImage.raycastTarget = false;
            fill = fillImage.rectTransform;
            UIFactory.Stretch(fill);

            label = UIFactory.CreateText("Label", background.transform, 11, TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void LateUpdate()
        {
            CharacterProgress progress = UIFactory.LocalPlayer<CharacterProgress>();
            bool show = progress != null && progress.IsSpawned;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;

            int needed = progress.ExperienceToNext;
            float fraction = needed > 0 ? Mathf.Clamp01((float)progress.Experience / needed) : 1f;
            fill.anchorMax = new Vector2(fraction, 1f);
            label.text = needed > 0
                ? $"Level {progress.Level} {progress.ClassName}  -  {progress.Experience} / {needed} XP"
                : $"Level {progress.Level} {progress.ClassName}  -  max level";
        }
    }
}
