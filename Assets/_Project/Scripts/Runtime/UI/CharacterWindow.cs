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
    /// The local player's character sheet (C, or the Character button), laid out like Source's: three pages
    /// (attributes, combat ratings, weapon skills) of "Label: value" rows, a small purple + to spend a point on every
    /// stat, and a tooltip for every row (what the stat does; on a + what a point costs and how many are invested).
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
        // The exact colour Source's client draws the "5xp" price in (181, 0, 175).
        private const string CostHex = "b500af";
        private const string TooExpensiveHex = "7a6a7a";

        /// <summary>One line of the sheet: its label, how to show its value, the attribute its + buys (if any) and what it does.</summary>
        private class Row
        {
            public string Label;
            public System.Func<string> Value;
            public Attribute? Plus;
            public string Description;
            public Text ValueText;
            public bool Mana;
        }

        private GameObject window;
        private GameObject toggleButton;
        private Text title;
        private Text points;
        private Text pageText;
        private readonly List<List<Row>> pages = new List<List<Row>>();
        private readonly List<GameObject> pageRoots = new List<GameObject>();
        private int page;
        private RectTransform tooltip;
        private Text tooltipText;
        private System.Func<string> tooltipContent;
        private CharacterProgress progress;
        private Health health;
        private Mana mana;
        private AutoAttack attack;
        private Inventory inventory;
        private AttributeSet total;
        private AttributeSet fromItems;

        private static string Percent(float chance) => $"{Mathf.RoundToInt(chance * 1000f) / 10f}%";

        /// <summary>An attribute's total, with what items add in green.</summary>
        private string AttributeValue(Attribute attribute, string extra = null)
        {
            string shown = fromItems[attribute] != 0
                ? $"{total[attribute]} <size=13><color=#7fbf6a>(+{fromItems[attribute]})</color></size>"
                : total[attribute].ToString();
            return extra != null ? $"{shown} <size=13><color=#{ColorUtility.ToHtmlStringRGB(LabelColor)}>{extra}</color></size>" : shown;
        }

        private Row Stat(Attribute attribute, string description, System.Func<string> extra = null) => new Row
        {
            Label = AttributeRules.NameOf(attribute) + ":",
            Value = () => AttributeValue(attribute, extra?.Invoke()),
            Plus = attribute,
            Description = description,
        };

        private void BuildPages()
        {
            pages.Add(new List<Row>
            {
                new Row { Label = "Health:", Value = () => health.Max.ToString(), Plus = Attribute.Health,
                    Description = "The damage you can take before you fall. Grows with points bought with experience (+1 each), Courage, Strength, levels and gear." },
                new Row { Label = "Mana:", Value = () => mana != null ? mana.Max.ToString() : "0", Plus = Attribute.Mana, Mana = true,
                    Description = "Spent on abilities and slowly regained. Grows with points bought with experience (+1 each), Intelligence, levels and gear." },
                new Row { Label = "Armor Value:", Value = () => (inventory != null ? inventory.Armor : 0).ToString(),
                    Description = "Reduces the damage of every hit you take. Comes from gear." },
                null,
                Stat(Attribute.Strength, "+1 health per point. Warriors deal a quarter of their Strength as extra damage."),
                Stat(Attribute.Agility, "+0.4% critical chance per point. Rangers deal a quarter of their Agility as extra damage."),
                Stat(Attribute.Willpower, "+0.5% ability damage and heals per point.",
                    () => $"+{Mathf.RoundToInt((AttributeRules.AbilityPower(total) - 1f) * 100f)}%"),
                Stat(Attribute.Intelligence, "+4 mana per point. Mages deal a quarter of their Intelligence as extra damage."),
                Stat(Attribute.Vitality, "+4 health per point."),
                Stat(Attribute.Regeneration, "Health regained every second, even in combat: 1 every 5 seconds per point.",
                    () => $"{AttributeRules.HealthRegenPerSecond(total):0.#}/s"),
                Stat(Attribute.Meditate, "Mana regained every second: 1 every 5 seconds per point.",
                    () => $"{AttributeRules.ManaRegenPerSecond(total):0.#}/s"),
                null,
                new Row { Label = "Damage:", Value = () => $"{attack.MinDamage} - {attack.MaxDamage}",
                    Description = "The damage of your attacks; abilities multiply it. Your weapon's skill raises it." },
                new Row { Label = "Critical Chance:", Value = () => Percent(progress.CritChance),
                    Description = "The chance for an attack to deal double damage: your class, Agility and the melee or ranged critical rating." },
            });
            pages.Add(new List<Row>
            {
                Stat(Attribute.MeleeCritical, "Critical chance of melee attacks: +0.2% per point.", () => Percent(AttributeRules.AttackCritBonus(total, false))),
                Stat(Attribute.RangedCritical, "Critical chance of ranged attacks: +0.2% per point.", () => Percent(AttributeRules.AttackCritBonus(total, true))),
                Stat(Attribute.SpellCritical, "Critical chance of abilities: +0.2% per point.", () => Percent(progress.SpellCritChance)),
                null,
                Stat(Attribute.Dodge, "Chance to avoid a hit completely: +0.2% per point, at most 40%.", () => Percent(AttributeRules.DodgeChance(total))),
                Stat(Attribute.Block, "Chance to block half of a hit: +0.2% per point, at most 50%.", () => Percent(AttributeRules.BlockChance(total))),
                null,
                Stat(Attribute.ResistFrost, "Less frost damage taken: rating / (rating + 100).", () => Percent(AttributeRules.ResistReduction(total.resistFrost))),
                Stat(Attribute.ResistFire, "Less fire damage taken: rating / (rating + 100).", () => Percent(AttributeRules.ResistReduction(total.resistFire))),
                Stat(Attribute.ResistShadow, "Less shadow damage taken: rating / (rating + 100).", () => Percent(AttributeRules.ResistReduction(total.resistShadow))),
                Stat(Attribute.ResistHoly, "Less holy damage taken: rating / (rating + 100).", () => Percent(AttributeRules.ResistReduction(total.resistHoly))),
                null,
                Stat(Attribute.Bartering, "Vendors charge 0.5% less and pay 0.5% more per point, up to 25%.", () => Percent(AttributeRules.BarteringDiscount(total))),
                Stat(Attribute.Lockpicking, "Opens locked chests: you need twice the chest's level."),
            });
            var skills = new List<Row>();
            for (Attribute skill = Attribute.Staves; skill <= Attribute.Shields; skill++)
            {
                Attribute current = skill;
                string what = current == Attribute.Shields ? "+0.1% block chance per point." : "+0.5% damage per point with this kind of weapon.";
                skills.Add(Stat(current, what, () =>
                {
                    if (current == Attribute.Shields) return null;
                    bool worn = progress.WeaponSkill == current;
                    string bonus = $"+{Mathf.RoundToInt((AttributeRules.WeaponSkillBonus(total, current) - 1f) * 100f)}%";
                    return worn ? $"{bonus} (your weapon)" : bonus;
                }));
            }
            pages.Add(skills);
        }

        private static readonly string[] PageNames = { "Attributes", "Combat", "Weapon Skills" };

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

            BuildPages();
            float lowest = 0f;
            foreach (List<Row> rows in pages)
            {
                RectTransform root = UIFactory.CreateRect("Page", rect);
                UIFactory.Stretch(root);
                pageRoots.Add(root.gameObject);
                float y = -66f;
                foreach (Row row in rows)
                {
                    if (row == null)
                    {
                        y -= 12f;
                        continue;
                    }
                    if (row.Plus.HasValue) AddPlus(root, row.Plus.Value, y);
                    AddRow(root, row, ref y);
                }
                lowest = Mathf.Min(lowest, y);
            }

            float bottom = lowest - 8f;
            Button previous = UIFactory.CreateButton("Previous", rect, PlusColor, "<", 15);
            UIFactory.Place((RectTransform)previous.transform, new Vector2(0f, 1f), new Vector2(LabelX, bottom), new Vector2(26f, 22f));
            previous.onClick.AddListener(() => ShowPage(page - 1));
            Button next = UIFactory.CreateButton("Next", rect, PlusColor, ">", 15);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0f, 1f), new Vector2(Width - LabelX - 26f, bottom), new Vector2(26f, 22f));
            next.onClick.AddListener(() => ShowPage(page + 1));
            pageText = AddText(rect, "", 15, TextAnchor.MiddleCenter, new Vector2(LabelX + 30f, bottom), new Vector2(Width - 2f * LabelX - 60f, 22f));
            pageText.color = LabelColor;

            points = AddText(rect, "", 15, TextAnchor.UpperLeft, new Vector2(LabelX, bottom - 30f), new Vector2(Width - 40f, 22f));
            points.color = TitleColor;
            rect.sizeDelta = new Vector2(Width, -bottom + 30f + 34f);

            BuildTooltip();
            ShowPage(0);
            window.SetActive(false);
        }

        private void ShowPage(int index)
        {
            page = (index % pages.Count + pages.Count) % pages.Count;
            for (int i = 0; i < pageRoots.Count; i++) pageRoots[i].SetActive(i == page);
            pageText.text = $"{PageNames[page]}  ({page + 1}/{pages.Count})";
            HideTooltip();
        }

        private void AddRow(RectTransform parent, Row row, ref float y)
        {
            // A see-through strip under the row catches the pointer for the tooltip.
            Image strip = UIFactory.CreateImage(row.Label, parent, Color.clear);
            UIFactory.Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(LabelX - 6f, y + 2f), new Vector2(PlusX - LabelX, RowHeight));
            HoverArea hover = strip.gameObject.AddComponent<HoverArea>();
            hover.Entered += _ => ShowTooltip(() => row.Description);
            hover.Exited += _ => HideTooltip();

            Text labelText = AddText(parent, row.Label, 16, TextAnchor.MiddleLeft, new Vector2(LabelX, y), new Vector2(ValueX - LabelX, RowHeight));
            labelText.color = LabelColor;
            row.ValueText = AddText(parent, "", 16, TextAnchor.MiddleLeft, new Vector2(ValueX, y), new Vector2(PlusX - ValueX - 6f, RowHeight));
            row.ValueText.color = row.Mana ? ManaColor : ValueColor;
            row.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            y -= RowHeight;
        }

        private void AddPlus(RectTransform parent, Attribute attribute, float y)
        {
            Button plus = UIFactory.CreateButton("Plus", parent, PlusColor, "+", 15);
            UIFactory.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(PlusX, y - 4f), new Vector2(17f, 17f));
            plus.gameObject.AddComponent<Outline>().effectColor = PlusBorder;
            plus.GetComponentInChildren<Text>().color = new Color32(0xE6, 0xD6, 0xF2, 0xFF);
            plus.onClick.AddListener(() => Spend(attribute));

            HoverArea hover = plus.gameObject.AddComponent<HoverArea>();
            hover.Entered += _ => ShowTooltip(() => PointTooltip(attribute));
            hover.Exited += _ => HideTooltip();
        }

        private string PointTooltip(Attribute attribute)
        {
            int invested = progress != null ? progress.SpentPoints[attribute] : 0;
            string gain = attribute == Attribute.Health ? $"+{AttributeRules.HealthPerPoint} Health"
                : attribute == Attribute.Mana ? $"+{AttributeRules.ManaPerPoint} Mana"
                : $"+1 {AttributeRules.NameOf(attribute)}";
            int cost = progress != null ? progress.PointCost(attribute) : AttributeRules.PointCost(attribute, 0);
            bool affordable = progress != null && progress.CanAfford(attribute);
            string header = $"<color=#{TooltipHeaderHex}>{gain}:</color> <color=#{(affordable ? CostHex : TooExpensiveHex)}>{cost}xp</color>";
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
            if (progress == null) return;
            if (progress.CanAfford(attribute)) progress.RequestSpendPoint(attribute);
            else MessageLog.Add($"You need {progress.PointCost(attribute)} experience to raise {AttributeRules.NameOf(attribute)}.", MessageLog.WarningColor);
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

            health = progress.GetComponent<Health>();
            mana = progress.GetComponent<Mana>();
            attack = progress.GetComponent<AutoAttack>();
            inventory = progress.GetComponent<Inventory>();
            title.text = $"{health.DisplayName}\n<size=15><color=#{ColorUtility.ToHtmlStringRGB(LabelColor)}>Level {progress.Level} {progress.ClassName}, " +
                         $"{LevelCurve.RankName(progress.Level)}</color></size>";

            total = progress.TotalAttributes;
            fromItems = inventory != null ? inventory.Attributes : default;
            foreach (Row row in pages[page])
                if (row != null) row.ValueText.text = row.Value();
            points.text = $"Experience to spend: <color=#{CostHex}>{progress.ExperienceToSpend}xp</color>";

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
