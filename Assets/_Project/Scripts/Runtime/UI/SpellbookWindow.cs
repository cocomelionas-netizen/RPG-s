using System.Collections.Generic;
using RPG.Controls;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's ABILITIES window (P, the Spells button, or SPEND EXP on the character sheet), laid out like the
    /// source game's (474 x 592, the SPELLS and ACTIONS tabs, a scrolling list of 401 x 70 rows): every spell of the
    /// player's class with its icon, NAME and rank in Roman numerals and what it does. Known spells have a purple + on the
    /// icon that ranks them up with experience (the price in its tooltip); clicking a known spell picks it and clicking a
    /// slot of the action bar puts it there, or it is dragged there. Spells not learned yet are dim with the level of their
    /// Tome. ACTIONS holds Attack. Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SpellbookWindow : MonoBehaviour
    {
        private const float Width = 474f;
        private const float Height = 592f;
        private const float RowWidth = 401f;
        private const float RowHeight = 70f;
        private const float RowGap = 10f;
        private const float ListTop = 112f;
        private const float IconSize = 44f;

        private static readonly Color PanelColor = new Color32(0x24, 0x22, 0x20, 0xF5);
        private static readonly Color HeaderColor = new Color32(0x4A, 0x16, 0x12, 0xF5);
        private static readonly Color TabBarColor = new Color32(0x14, 0x12, 0x11, 0xF5);
        private static readonly Color BorderColor = new Color32(0x4A, 0x3E, 0x26, 0xFF);
        private static readonly Color TitleColor = new Color32(0xE6, 0xD8, 0xC0, 0xFF);
        private static readonly Color TabColor = new Color32(0xC8, 0xB8, 0x9E, 0xFF);
        private static readonly Color ActiveTabColor = new Color32(0xC0, 0x3A, 0x2A, 0xFF);
        private static readonly Color KnownRow = new Color32(0x3C, 0x3A, 0x37, 0xF0);
        private static readonly Color UnknownRow = new Color32(0x2A, 0x29, 0x27, 0xE0);
        private static readonly Color PickedRow = new Color32(0x4C, 0x36, 0x5E, 0xF0);
        private static readonly Color PlusColor = new Color32(0x6B, 0x2C, 0x8F, 0xFF);
        private static readonly Color TooltipColor = new Color32(0x0D, 0x15, 0x1A, 0xF5);
        private const string CostHex = "b500af";

        private static SpellbookWindow instance;

        /// <summary>The spell picked to put on the bar (-1 when none).</summary>
        public static int PendingSpell { get; private set; } = -1;

        public static void ClearPending() => PendingSpell = -1;

        /// <summary>Opens the window just right of <paramref name="beside"/> (the character sheet's SPEND EXP).</summary>
        public static void OpenBeside(RectTransform beside)
        {
            if (instance == null) return;
            instance.window.SetActive(true);
            if (beside != null)
            {
                Vector3[] corners = new Vector3[4];
                beside.GetWorldCorners(corners);
                instance.windowRect.position = new Vector3(corners[2].x + 10f, (corners[1].y + corners[0].y) / 2f, 0f);
            }
        }

        private GameObject window;
        private GameObject toggleButton;
        private RectTransform windowRect;
        private RectTransform content;
        private ScrollRect scroll;
        private Text footer;
        private readonly List<Button> tabs = new List<Button>();
        private readonly List<Row> rows = new List<Row>();
        private int tab;
        private Spellbook shownFor;
        private int shownCount = -1;
        private int shownTab = -1;
        private RectTransform tooltip;
        private Text tooltipText;
        private System.Func<string> tooltipContent;

        private class Row
        {
            public int Spell;
            public Image Background;
            public Image Icon;
            public Text Name;
            public Text Details;
            public GameObject Plus;
        }

        private void Awake()
        {
            instance = this;
            Button toggle = UIFactory.CreateButton("Spellbook Button", transform, new Color(0.3f, 0.18f, 0.35f, 0.95f), "Spells (P)", 16);
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(1f, 0f);
            toggleRect.sizeDelta = new Vector2(120f, 44f);
            toggleRect.anchoredPosition = new Vector2(-280f, 20f);
            toggle.onClick.AddListener(Toggle);
            toggleButton = toggle.gameObject;

            Image panel = UIFactory.CreateImage("Abilities", transform, PanelColor);
            window = panel.gameObject;
            panel.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            windowRect = panel.rectTransform;
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0f, 0.5f);
            windowRect.pivot = new Vector2(0f, 0.5f);
            windowRect.anchoredPosition = new Vector2(633f, 40f);
            windowRect.sizeDelta = new Vector2(Width, Height);

            // The source game's header with the title and the tab bar under it.
            Image header = UIFactory.CreateImage("Header", windowRect, HeaderColor);
            UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -14f), new Vector2(Width - 20f, 52f));
            Text title = AddText(windowRect, "ABILITIES", 21, TextAnchor.MiddleCenter, new Vector2(0f, -26f), new Vector2(Width, 30f));
            title.color = TitleColor;
            Image tabBar = UIFactory.CreateImage("Tabs", windowRect, TabBarColor);
            UIFactory.Place(tabBar.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -68f), new Vector2(Width - 20f, 38f));
            string[] names = { "SPELLS", "ACTIONS" };
            float[] centers = { 182f, 288f };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Button button = UIFactory.CreateButton(names[i], windowRect, Color.clear, names[i], 14);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(centers[i] - 50f, -74f), new Vector2(100f, 26f));
                button.onClick.AddListener(() => tab = index);
                tabs.Add(button);
            }
            Button close = UIFactory.CreateButton("Close", windowRect, new Color(0.35f, 0.12f, 0.1f, 0.95f), "X", 15);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-14f, -26f), new Vector2(26f, 26f));
            close.onClick.AddListener(Toggle);

            // The scrolling list.
            RectTransform viewport = UIFactory.CreateRect("List", windowRect);
            UIFactory.Place(viewport, new Vector2(0f, 1f), new Vector2(26f, -ListTop), new Vector2(RowWidth + 20f, Height - ListTop - 46f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            content = UIFactory.CreateRect("Rows", viewport);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            footer = AddText(windowRect, "", 13, TextAnchor.MiddleCenter, new Vector2(0f, -Height + 40f), new Vector2(Width, 24f));
            footer.color = TabColor;

            BuildTooltip();
            window.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            PendingSpell = -1;
        }

        private static Text AddText(RectTransform parent, string content, int size, TextAnchor alignment, Vector2 topLeft, Vector2 boxSize)
        {
            Text text = UIFactory.CreateText("Text", parent, size, alignment);
            UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), topLeft, boxSize);
            text.text = content;
            return text;
        }

        private void Toggle()
        {
            window.SetActive(!window.activeSelf);
            if (!window.activeSelf)
            {
                PendingSpell = -1;
                tooltipContent = null;
            }
        }

        /// <summary>The list of the shown tab: the class's spells, or Attack (spell index <see cref="Spellbook.AttackEntry"/>).</summary>
        private void Build(Spellbook spellbook)
        {
            foreach (Row row in rows) Destroy(row.Background.gameObject);
            rows.Clear();
            shownFor = spellbook;
            shownCount = spellbook.Spells.Count;
            shownTab = tab;
            var entries = new List<int>();
            if (tab == 0)
                for (int i = 0; i < shownCount; i++) entries.Add(i);
            else
                entries.Add(Spellbook.AttackEntry);
            for (int i = 0; i < entries.Count; i++) rows.Add(CreateRow(entries[i], i));
            content.sizeDelta = new Vector2(RowWidth, entries.Count * (RowHeight + RowGap));
            content.anchoredPosition = Vector2.zero;
        }

        private Row CreateRow(int spell, int position)
        {
            Button button = UIFactory.CreateButton("Ability", content, KnownRow, null, 0);
            var rect = (RectTransform)button.transform;
            UIFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, -position * (RowHeight + RowGap)), new Vector2(RowWidth, RowHeight));
            button.onClick.AddListener(() => Pick(spell));
            var source = button.gameObject.AddComponent<DragSource>();
            source.Payload = () => Payload(spell);
            var row = new Row { Spell = spell, Background = (Image)button.targetGraphic };

            Image frame = UIFactory.CreateImage("Icon Frame", rect, Color.black);
            UIFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -13f), new Vector2(IconSize, IconSize));
            frame.raycastTarget = false;
            row.Icon = UIFactory.CreateImage("Icon", frame.rectTransform, Color.gray);
            UIFactory.Stretch(row.Icon.rectTransform);
            row.Icon.rectTransform.offsetMin = new Vector2(3f, 3f);
            row.Icon.rectTransform.offsetMax = new Vector2(-3f, -3f);
            row.Icon.raycastTarget = false;

            row.Name = AddText(rect, "", 15, TextAnchor.UpperLeft, new Vector2(70f, -10f), new Vector2(RowWidth - 80f, 20f));
            row.Name.fontStyle = FontStyle.Bold;
            row.Details = AddText(rect, "", 12, TextAnchor.UpperLeft, new Vector2(70f, -32f), new Vector2(RowWidth - 80f, 34f));
            row.Details.color = new Color32(0xA8, 0xA0, 0x92, 0xFF);

            // The purple + on the icon's corner ranks the spell up with experience.
            Button plus = UIFactory.CreateButton("Rank Up", rect, PlusColor, "+", 13);
            UIFactory.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(8f, -7f), new Vector2(16f, 16f));
            plus.onClick.AddListener(() => RankUp(spell));
            var hover = plus.gameObject.AddComponent<HoverArea>();
            hover.Entered += _ => tooltipContent = () => RankTooltip(spell);
            hover.Exited += _ => tooltipContent = null;
            row.Plus = plus.gameObject;
            return row;
        }

        private DragPayload? Payload(int spell)
        {
            if (shownFor == null) return null;
            if (spell == Spellbook.AttackEntry)
                return new DragPayload { Kind = DragKind.Spell, Value = Spellbook.AttackEntry, Label = "Attack", Color = new Color(0.45f, 0.2f, 0.15f) };
            if (!shownFor.Knows(spell)) return null;
            AbilityDefinition ability = shownFor.Get(spell);
            Color color = ability.effectColor * 0.45f;
            color.a = 1f;
            return new DragPayload { Kind = DragKind.Spell, Value = spell, Label = ability.displayName, Color = color };
        }

        private void Pick(int spell)
        {
            if (shownFor == null || (spell != Spellbook.AttackEntry && !shownFor.Knows(spell))) return;
            PendingSpell = PendingSpell == spell ? -1 : spell;
            if (PendingSpell >= 0 || PendingSpell == Spellbook.AttackEntry)
            {
                string name = spell == Spellbook.AttackEntry ? "Attack" : shownFor.Get(spell).displayName;
                MessageLog.Add($"Click a slot on the action bar to put {name} there (or drag it there).", new Color(0.75f, 0.55f, 1f));
            }
        }

        private void RankUp(int spell)
        {
            if (shownFor == null || spell < 0) return;
            if (shownFor.CanRankUp(spell)) shownFor.RequestRankUp(spell);
            else if (shownFor.NextRankCost(spell) > 0)
                MessageLog.Add($"You need {shownFor.NextRankCost(spell)} experience to raise {shownFor.Get(spell).displayName}.", MessageLog.WarningColor);
        }

        private string RankTooltip(int spell)
        {
            if (shownFor == null || spell < 0) return "";
            AbilityDefinition ability = shownFor.Get(spell);
            int cost = shownFor.NextRankCost(spell);
            string price = $"<color=#{(shownFor.CanRankUp(spell) ? CostHex : "7a6a7a")}>{cost}xp</color>";
            return $"<color=#d3c370>{ability.displayName} {Roman(shownFor.RankOf(spell) + 1)}:</color> {price}\nRank {shownFor.RankOf(spell)} of {ability.maxRank}.";
        }

        /// <summary>A rank in Roman numerals, as the source game shows it ("V"); rank 1 shows none.</summary>
        public static string Roman(int rank)
        {
            if (rank <= 1) return "";
            string[] numerals = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < values.Length; i++)
                while (rank >= values[i])
                {
                    text.Append(numerals[i]);
                    rank -= values[i];
                }
            return text.ToString();
        }

        private void BuildTooltip()
        {
            Image background = UIFactory.CreateImage("Rank Tooltip", transform, TooltipColor);
            background.raycastTarget = false;
            tooltip = background.rectTransform;
            tooltip.anchorMin = tooltip.anchorMax = Vector2.zero;
            tooltip.pivot = new Vector2(0f, 1f);
            tooltip.sizeDelta = new Vector2(260f, 50f);
            tooltipText = UIFactory.CreateText("Text", tooltip, 15, TextAnchor.UpperLeft);
            UIFactory.Stretch(tooltipText.rectTransform);
            tooltipText.rectTransform.offsetMin = new Vector2(10f, 6f);
            tooltipText.rectTransform.offsetMax = new Vector2(-10f, -6f);
            tooltip.gameObject.SetActive(false);
        }

        private void Update()
        {
            Spellbook spellbook = UIFactory.LocalPlayer<Spellbook>();
            bool hasPlayer = spellbook != null && spellbook.IsSpawned;
            if (toggleButton.activeSelf != hasPlayer) toggleButton.SetActive(hasPlayer);
            if (!hasPlayer)
            {
                if (window.activeSelf) window.SetActive(false);
                tooltip.gameObject.SetActive(false);
                return;
            }

            var input = spellbook.GetComponent<PlayerInputReader>();
            if (input != null && input.enabled)
            {
                if (input.SpellbookPressed) Toggle();
                else if (input.CancelPressed && PendingSpell != -1) PendingSpell = -1;
            }
            if (!window.activeSelf)
            {
                tooltip.gameObject.SetActive(false);
                return;
            }
            if (spellbook != shownFor || spellbook.Spells.Count != shownCount || tab != shownTab) Build(spellbook);
            for (int i = 0; i < tabs.Count; i++) tabs[i].GetComponentInChildren<Text>().color = i == tab ? ActiveTabColor : TabColor;

            var progress = spellbook.GetComponent<CharacterProgress>();
            var caster = spellbook.GetComponent<AbilityCaster>();
            foreach (Row row in rows)
            {
                if (row.Spell == Spellbook.AttackEntry)
                {
                    row.Background.color = PendingSpell == Spellbook.AttackEntry ? PickedRow : KnownRow;
                    row.Icon.color = new Color(0.6f, 0.25f, 0.2f);
                    row.Name.text = "ATTACK";
                    row.Name.color = TitleColor;
                    row.Details.text = "Attack the selected enemy with your weapon (F). Drag it onto the action bar.";
                    row.Plus.SetActive(false);
                    continue;
                }
                AbilityDefinition ability = spellbook.Get(row.Spell);
                if (ability == null) continue;
                int rank = spellbook.RankOf(row.Spell);
                bool known = rank > 0;
                row.Background.color = PendingSpell == row.Spell ? PickedRow : known ? KnownRow : UnknownRow;
                Color icon = ability.effectColor;
                icon.a = 1f;
                row.Icon.color = known ? icon : icon * 0.35f;
                string numeral = Roman(rank);
                row.Name.text = (ability.displayName.ToUpperInvariant() + (numeral.Length > 0 ? " " + numeral : ""));
                row.Name.color = known ? TitleColor : new Color32(0x80, 0x78, 0x6C, 0xFF);
                if (known)
                {
                    int mana = caster != null ? caster.ManaCost(ability) : ability.manaCost;
                    row.Details.text = string.IsNullOrEmpty(ability.description) ? $"{mana} mana, {ability.cooldown:0.#}s cooldown" : ability.description;
                }
                else
                {
                    string level = progress.Level >= ability.learnLevel ? $"level {ability.learnLevel}" : $"<color=#a04030>level {ability.learnLevel}</color>";
                    row.Details.text = $"Not learned: its Tome is sold by your class trainer ({level}).";
                }
                row.Plus.SetActive(known && spellbook.NextRankCost(row.Spell) > 0);
            }
            footer.text = $"Experience to spend: <color=#{CostHex}>{progress.ExperienceToSpend}xp</color>";
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            bool show = tooltipContent != null;
            if (tooltip.gameObject.activeSelf != show) tooltip.gameObject.SetActive(show);
            if (!show) return;
            tooltipText.text = tooltipContent();
            tooltip.sizeDelta = new Vector2(260f, tooltipText.preferredHeight + 14f);
            tooltip.SetAsLastSibling();
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
            tooltip.anchoredPosition = pointer / scale + new Vector2(16f, -12f);
        }
    }
}
