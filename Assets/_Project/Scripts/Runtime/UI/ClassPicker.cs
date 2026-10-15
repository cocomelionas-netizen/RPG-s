using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// Buttons to switch the local player's class (for testing; with accounts the class is chosen once
    /// when creating a character). Level and experience are kept. Builds its own UI.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ClassPicker : MonoBehaviour
    {
        [SerializeField, Min(16f)] private float buttonHeight = 36f;
        [SerializeField, Min(16f)] private float buttonWidth = 120f;

        private static readonly Color Normal = new Color(0.15f, 0.13f, 0.12f, 0.9f);
        private static readonly Color Selected = new Color(0.55f, 0.42f, 0.15f, 0.95f);

        private GameObject panel;
        private Button[] buttons;
        private CharacterProgress progress;

        private void Awake()
        {
            panel = UIFactory.CreateRect("Panel", transform).gameObject;
            UIFactory.Stretch((RectTransform)panel.transform);

            Text title = UIFactory.CreateText("Title", panel.transform, 16, TextAnchor.LowerLeft);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 0f);
            title.rectTransform.sizeDelta = new Vector2(buttonWidth * 2f, 24f);
            title.rectTransform.anchoredPosition = Vector2.zero;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = "Class (testing)";
        }

        private void Build(ClassCatalog catalog)
        {
            if (buttons != null)
                foreach (Button old in buttons) Destroy(old.gameObject);

            buttons = new Button[catalog.Count];
            for (int i = 0; i < catalog.Count; i++)
            {
                CharacterClassDefinition definition = catalog.Get(i);
                Button button = UIFactory.CreateButton(definition.displayName, panel.transform, Normal, definition.displayName, 17);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
                rect.anchoredPosition = new Vector2(0f, -i * (buttonHeight + 6f) - 4f);
                int index = i;
                button.onClick.AddListener(() => OnClicked(index));
                buttons[i] = button;
            }
        }

        private void OnClicked(int index)
        {
            if (progress != null) progress.RequestClass(index);
        }

        private void LateUpdate()
        {
            CharacterProgress local = UIFactory.LocalPlayer<CharacterProgress>();
            if (local != progress)
            {
                progress = local;
                if (progress != null && progress.Catalog != null) Build(progress.Catalog);
            }

            bool show = progress != null && progress.IsSpawned && buttons != null;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;

            for (int i = 0; i < buttons.Length; i++)
                ((Image)buttons[i].targetGraphic).color = i == progress.ClassIndex ? Selected : Normal;
        }
    }
}
