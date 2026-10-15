using Attribute = RPG.Progression.Attribute;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Controls;
using RPG.Items;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's character sheet (C, or the Character button), laid out like Source's: rows of
    /// "Label: value", attributes with a small purple + to spend a point, and a tooltip for every row
    /// (what the stat does; on a + what a point costs and how many are already invested).
    /// Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CharacterWindow : MonoBehaviour
    {
        private const float Width = 330f;
        private const float RowHeight = 24f;
        private const float LabelX = 22f;
        private const float ValueX = 170f;
        private const float PlusX = 262f;

        // Colours taken from Source's character panel.
        private static readonly Color PanelColor = new Color32(0x12, 0x13, 0x06, 0xF2);
        private static readonly Color BorderColor = new Color32(0x4A, 0x3E, 0x26, 0xFF);
        private static readonly Color LabelColor = new Color32(0x8C, 0x86, 0x76, 0xFF);
        private static readonly Color ValueColor = new Color32(0xC8, 0xC4, 0xB6, 0xFF);
        private static readonly Color ManaColor = new Color32(0x6E, 0x8C, 0xB4, 0xFF);
        private static readonly Color TitleColor = new Color32(0xD3, 0xC3, 0x70, 0xFF);
        private static readonly Color PlusColor = new Color32(0x6B, 0x2C, 0x8F, 0xFF);
        private static readonly Color PlusBorder = new Color32(0x2F, 0x04, 0x47, 0xFF);
        private static readonly Color TooltipColor = new Color32(0x0D, 0x15, 0x1A, 0xF5);
        private static readonly Color TooltipBorder = new Color32(0x2B, 0x3A, 0x40, 0xFF);
        private const string TooltipHeaderHex = "d3c370";
        private const string CostHex = "c040c8";

        private enum Stat { Health, Mana, Armor, Strength, Agility, Intelligence, Vitality, Damage, Critical }

        private static readonly Dictionary<Stat, string> Descriptions = new Dictionary<Stat, string>
        {
            { Stat.Health, "The damage you can take before you fall. Grows with points put into it (+5 each), Vitality, Strength, levels and gear." },
            { Stat.Mana, "Spent on abilities and slowly regained. Grows with points put into it (+5 each), Intelligence, levels and gear." },
            { Stat.Armor, "Reduces the damage of every hit you take. Comes from gear." },
            { Stat.Strength, "+1 health per point. Warriors deal a quarter of their Strength as extra damage." },
            { Stat.Agility, "+0.4% critical chance per point. Rangers deal a quarter of their Agility as extra damage." },
            { Stat.Intelligence, "+4 mana per point. Mages deal a quarter of their Intelligence as extra damage." },
            { Stat.Vitality, "+4 health per point." },
            { Stat.Damage, "The damage of your attacks; abilities multiply it." },
            { Stat.Critical, "The chance for a hit to deal double damage." },
        };

        private GameObject window;
        private GameObject toggleButton;
        private Text title;
        private Text points;
        private readonly Dictionary<Stat, Text> values = new Dictionary<Stat, Text>();
        private readonly GameObject[] plusButtons = new GameObject[AttributeSet.Count];
        private RectTransform tooltip;
        private Text tooltipText;
        private System.Func<string> tooltipContent;
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
            panel.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 60f);

            title = AddText(rect, "", 19, TextAnchor.UpperLeft, new Vector2(LabelX, -12f), new Vector2(Width - 40f, 48f));
            title.color = TitleColor;
            Button close = UIFactory.CreateButton("Close", rect, new Color(0.35f, 0.12f, 0.1f, 0.95f), "X", 15);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(26f, 26f));
            close.onClick.AddListener(Toggle);

            float y = -66f;
            plusButtons[(int)Attribute.Health] = AddPlus(rect, Attribute.Health, y);
            AddRow(rect, Stat.Health, "Health:", ref y);
            plusButtons[(int)Attribute.Mana] = AddPlus(rect, Attribute.Mana, y);
            AddRow(rect, Stat.Mana, "Mana:", ref y);
            AddRow(rect, Stat.Armor, "Armor Value:", ref y);
            y -= 12f;
            for (int i = 0; i <= (int)Attribute.Vitality; i++)
            {
                var attribute = (Attribute)i;
                float rowY = y;
                AddRow(rect, Stat.Strength + i, $"{attribute}:", ref y);
                plusButtons[i] = AddPlus(rect, attribute, rowY);
            }
            y -= 12f;
            AddRow(rect, Stat.Damage, "Damage:", ref y);
            AddRow(rect, Stat.Critical, "Critical Chance:", ref y);
            y -= 8f;
            points = AddText(rect, "", 15, TextAnchor.UpperLeft, new Vector2(LabelX, y), new Vector2(Width - 40f, 22f));
            points.color = TitleColor;
            rect.sizeDelta = new Vector2(Width, -y + 34f);

            BuildTooltip();
            window.SetActive(false);
        }

        private void AddRow(RectTransform parent, Stat stat, string label, ref float y)
        {
            // A see-through strip under the row catches the pointer for the tooltip.
            Image strip = UIFactory.CreateImage(label, parent, Color.clear);
            UIFactory.Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(LabelX - 6f, y + 2f), new Vector2(PlusX - LabelX, RowHeight));
            HoverArea hover = strip.gameObject.AddComponent<HoverArea>();
            hover.Entered += _ => ShowTooltip(() => Descriptions[stat]);
            hover.Exited += _ => HideTooltip();

            Text labelText = AddText(parent, label, 16, TextAnchor.MiddleLeft, new Vector2(LabelX, y), new Vector2(ValueX - LabelX, RowHeight));
            labelText.color = LabelColor;
            Text value = AddText(parent, "", 16, TextAnchor.MiddleLeft, new Vector2(ValueX, y), new Vector2(PlusX - ValueX - 6f, RowHeight));
            value.color = stat == Stat.Mana ? ManaColor : ValueColor;
            values[stat] = value;
            y -= RowHeight;
        }

        private GameObject AddPlus(RectTransform parent, Attribute attribute, float y)
        {
            Button plus = UIFactory.CreateButton("Plus", parent, PlusColor, "+", 15);
            UIFactory.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(PlusX, y - 4f), new Vector2(17f, 17f));
            plus.gameObject.AddComponent<Outline>().effectColor = PlusBorder;
            plus.GetComponentInChildren<Text>().color = new Color32(0xE6, 0xD6, 0xF2, 0xFF);
            plus.onClick.AddListener(() => Spend(attribute));

            HoverArea hover = plus.gameObject.AddComponent<HoverArea>();
            hover.Entered += _ => ShowTooltip(() => PointTooltip(attribute));
            hover.Exited += _ => HideTooltip();
            return plus.gameObject;
        }

        private string PointTooltip(Attribute attribute)
        {
            int invested = progress != null ? progress.SpentPoints[attribute] : 0;
            string gain = attribute == Attribute.Health ? $"+{AttributeRules.HealthPerPoint} Health"
                : attribute == Attribute.Mana ? $"+{AttributeRules.ManaPerPoint} Mana"
                : $"+1 {attribute}";
            string header = $"<color=#{TooltipHeaderHex}>{gain}:</color> <color=#{CostHex}>1 point</color>";
            string spent = invested > 0 ? $"You have invested {invested} point{(invested == 1 ? "" : "s")}." : "You haven't invested anything.";
            return $"{header}\n{spent}";
        }

        private void BuildTooltip()
        {
            Image background = UIFactory.CreateImage("Stat Tooltip", transform, TooltipColor);
            background.raycastTarget = false;
            background.gameObject.AddComponent<Outline>().effectColor = TooltipBorder;
            tooltip = background.rectTransform;
            tooltip.anchorMin = tooltip.anchorMax = Vector2.zero;
            tooltip.pivot = new Vector2(0f, 1f);
            tooltip.sizeDelta = new Vector2(280f, 60f);

            tooltipText = UIFactory.CreateText("Text", tooltip, 15, TextAnchor.UpperLeft);
            UIFactory.Stretch(tooltipText.rectTransform);
            tooltipText.rectTransform.offsetMin = new Vector2(10f, 8f);
            tooltipText.rectTransform.offsetMax = new Vector2(-10f, -8f);
            tooltipText.color = new Color32(0xFB, 0xFE, 0xFE, 0xFF);
            tooltip.gameObject.SetActive(false);
        }

        private void ShowTooltip(System.Func<string> content) => tooltipContent = content;

        private void HideTooltip() => tooltipContent = null;

        private Text AddText(RectTransform parent, string content, int size, TextAnchor alignment, Vector2 topLeft, Vector2 boxSize)
        {
            Text text = UIFactory.CreateText("Text", parent, size, alignment);
            UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), topLeft, boxSize);
            text.text = content;
            return text;
        }

        private void Toggle()
        {
            window.SetActive(!window.activeSelf);
            if (!window.activeSelf) HideTooltip();
        }

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
                tooltip.gameObject.SetActive(false);
                return;
            }

            var input = progress.GetComponent<PlayerInputReader>();
            if (input != null && input.enabled && input.CharacterPressed) Toggle();
            if (!window.activeSelf)
            {
                tooltip.gameObject.SetActive(false);
                return;
            }

            var health = progress.GetComponent<Health>();
            var mana = progress.GetComponent<Mana>();
            var attack = progress.GetComponent<AutoAttack>();
            var inventory = progress.GetComponent<Inventory>();
            title.text = $"{health.DisplayName}\n<size=15><color=#{ColorUtility.ToHtmlStringRGB(LabelColor)}>Level {progress.Level} {progress.ClassName}, " +
                         $"{LevelCurve.RankName(progress.Level)}</color></size>";

            values[Stat.Health].text = health.Max.ToString();
            values[Stat.Mana].text = mana != null ? mana.Max.ToString() : "0";
            values[Stat.Armor].text = (inventory != null ? inventory.Armor : 0).ToString();

            AttributeSet total = progress.TotalAttributes;
            AttributeSet fromItems = inventory != null ? inventory.Attributes : default;
            for (int i = 0; i <= (int)Attribute.Vitality; i++)
            {
                var attribute = (Attribute)i;
                values[Stat.Strength + i].text = fromItems[attribute] != 0
                    ? $"{total[attribute]} <size=13><color=#7fbf6a>(+{fromItems[attribute]})</color></size>"
                    : total[attribute].ToString();
            }
            foreach (GameObject plus in plusButtons)
                if (plus != null) plus.SetActive(progress.UnspentPoints > 0);
            values[Stat.Damage].text = $"{attack.MinDamage} - {attack.MaxDamage}";
            values[Stat.Critical].text = $"{Mathf.RoundToInt(progress.CritChance * 1000f) / 10f}%";
            points.text = progress.UnspentPoints > 0 ? $"Unspent points: {progress.UnspentPoints}" : "Level up to earn attribute points.";

            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            bool show = tooltipContent != null;
            if (tooltip.gameObject.activeSelf != show) tooltip.gameObject.SetActive(show);
            if (!show) return;

            tooltipText.text = tooltipContent();
            tooltip.sizeDelta = new Vector2(280f, tooltipText.preferredHeight + 18f);
            tooltip.SetAsLastSibling();
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : Vector2.zero;
            tooltip.anchoredPosition = pointer / scale + new Vector2(16f, -12f);
        }
    }
}
