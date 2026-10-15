using RPG.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>The local player's mana. Builds its own UI.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class ManaBar : MonoBehaviour
    {
        private static readonly Color FillColor = new Color(0.2f, 0.45f, 0.95f);

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

            label = UIFactory.CreateText("Label", background.transform, 13, TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void LateUpdate()
        {
            Mana mana = UIFactory.LocalPlayer<Mana>();
            bool show = mana != null && mana.IsSpawned && mana.Max > 0;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;

            fill.anchorMax = new Vector2(mana.Fraction, 1f);
            label.text = $"Mana {mana.Current} / {mana.Max}";
        }
    }
}
