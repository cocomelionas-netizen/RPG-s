using RPG.Combat;
using RPG.Controls;
using RPG.Items;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's character sheet (C, or the Character button): attributes with buttons to spend
    /// the points earned by levelling up, and the stats they make. Builds its own UI; place it on a
    /// RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CharacterWindow : MonoBehaviour
    {
        private const float Width = 330f;
        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.94f);
        private static readonly Color PointsColor = new Color(1f, 0.85f, 0.3f);

        private static readonly string[] Hints =
        {
            "+1 health; damage for warriors",
            "+0.4% critical chance; damage for rangers",
            "+4 mana; damage for mages",
            "+4 health",
        };

        private GameObject window;
        private GameObject toggleButton;
        private Text title;
        private Text points;
        private Text stats;
        private readonly Text[] values = new Text[AttributeSet.Count];
        private readonly GameObject[] plusButtons = new GameObject[AttributeSet.Count];
        private CharacterProgress progress;

        private void Awake()
        {
            Button toggle = UIFactory.CreateButton("Character Button", transform, new Color(0.3f, 0.22f, 0.12f, 0.95f), "Character (C)", 16);
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(1f, 0f);
            toggleRect.sizeDelta = new Vector2(130f, 44f);
            toggleRect.anchoredPosition = new Vector2(-140f, 20f);
            toggle.onClick.AddListener(Toggle);
            toggleButton = toggle.gameObject;

            Image panel = UIFactory.CreateImage("Character", transform, PanelColor);
            window = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(Width, 420f);

            title = AddText(rect, "", 20, new Vector2(14f, -10f), new Vector2(Width - 28f, 28f));
            title.fontStyle = FontStyle.Bold;
            points = AddText(rect, "", 15, new Vector2(14f, -40f), new Vector2(Width - 28f, 20f));
            points.color = PointsColor;

            for (int i = 0; i < AttributeSet.Count; i++)
            {
                float y = -72f - i * 52f;
                AddText(rect, ((Attribute)i).ToString(), 17, new Vector2(14f, y), new Vector2(140f, 22f));
                values[i] = AddText(rect, "", 17, new Vector2(150f, y), new Vector2(80f, 22f));
                Text hint = AddText(rect, Hints[i], 12, new Vector2(14f, y - 22f), new Vector2(Width - 28f, 18f));
                hint.color = new Color(0.75f, 0.75f, 0.75f);

                Button plus = UIFactory.CreateButton("Plus", rect, new Color(0.25f, 0.45f, 0.2f, 0.95f), "+", 20);
                var plusRect = (RectTransform)plus.transform;
                plusRect.anchorMin = plusRect.anchorMax = plusRect.pivot = new Vector2(0f, 1f);
                plusRect.anchoredPosition = new Vector2(Width - 54f, y + 2f);
                plusRect.sizeDelta = new Vector2(36f, 30f);
                var attribute = (Attribute)i;
                plus.onClick.AddListener(() => Spend(attribute));
                plusButtons[i] = plus.gameObject;
            }

            stats = AddText(rect, "", 15, new Vector2(14f, -290f), new Vector2(Width - 28f, 120f));
            window.SetActive(false);
        }

        private Text AddText(RectTransform parent, string content, int size, Vector2 topLeft, Vector2 boxSize)
        {
            Text text = UIFactory.CreateText("Text", parent, size, TextAnchor.UpperLeft);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = boxSize;
            text.text = content;
            return text;
        }

        private void Toggle() => window.SetActive(!window.activeSelf);

        private void Spend(Attribute attribute)
        {
            if (progress != null) progress.RequestSpendPoint(attribute);
        }

        private void Update()
        {
            progress = UIFactory.LocalPlayer<CharacterProgress>();
            bool hasPlayer = progress != null && progress.IsSpawned;
            if (toggleButton.activeSelf != hasPlayer) toggleButton.SetActive(hasPlayer);
            if (!hasPlayer)
            {
                if (window.activeSelf) window.SetActive(false);
                return;
            }

            var input = progress.GetComponent<PlayerInputReader>();
            if (input != null && input.enabled && input.CharacterPressed) Toggle();
            if (!window.activeSelf) return;

            title.text = $"Level {progress.Level} {progress.ClassName}  <size=15><color=#ffd973>{LevelCurve.RankName(progress.Level)}</color></size>";
            points.text = progress.UnspentPoints > 0 ? $"{progress.UnspentPoints} points to spend" : "Level up to earn attribute points";

            AttributeSet total = progress.TotalAttributes;
            AttributeSet spent = progress.SpentPoints;
            var inventory = progress.GetComponent<Inventory>();
            AttributeSet fromItems = inventory != null ? inventory.Attributes : default;
            for (int i = 0; i < AttributeSet.Count; i++)
            {
                var attribute = (Attribute)i;
                values[i].text = fromItems[attribute] != 0 ? $"{total[attribute]} <color=#7fd97f>(+{fromItems[attribute]})</color>" : total[attribute].ToString();
                plusButtons[i].SetActive(progress.UnspentPoints > 0);
            }

            var attack = progress.GetComponent<AutoAttack>();
            var health = progress.GetComponent<Health>();
            var mana = progress.GetComponent<Mana>();
            CharacterClassDefinition definition = progress.Class;
            stats.text =
                $"Damage {attack.MinDamage}-{attack.MaxDamage}   (primary: {(definition != null ? definition.primaryAttribute.ToString() : "-")})\n" +
                $"Critical chance {Mathf.RoundToInt(progress.CritChance * 1000f) / 10f}%\n" +
                $"Health {health.Max}   Mana {(mana != null ? mana.Max : 0)}\n" +
                $"Armor {(inventory != null ? inventory.Armor : 0)}   Spent points: {spent.strength + spent.agility + spent.intelligence + spent.vitality}";
        }
    }
}
