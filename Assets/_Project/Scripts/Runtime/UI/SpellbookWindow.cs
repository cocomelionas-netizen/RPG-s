using System.Collections.Generic;
using RPG.Controls;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's spellbook (P, or the Spellbook button), like Dreadmyst's: every spell of the class with its
    /// rank, level, mana, cooldown and what it does. Known spells have a purple + that ranks them up with experience
    /// (the price in the tooltip); clicking a known spell picks it, and clicking a slot of the action bar then puts it
    /// there. Spells not learned yet show the level of their Tome. Builds its own UI; place it on a RectTransform that
    /// fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SpellbookWindow : MonoBehaviour
    {
        private const float Width = 520f;
        private const float RowHeight = 48f;
        private const float Top = 70f;

        private static readonly Color PanelColor = new Color32(0x12, 0x13, 0x06, 0xF2);
        private static readonly Color BorderColor = new Color32(0x4A, 0x3E, 0x26, 0xFF);
        private static readonly Color TitleColor = new Color32(0xD3, 0xC3, 0x70, 0xFF);
        private static readonly Color KnownRow = new Color(0.16f, 0.14f, 0.12f, 0.95f);
        private static readonly Color UnknownRow = new Color(0.09f, 0.08f, 0.07f, 0.9f);
        private static readonly Color PickedRow = new Color(0.3f, 0.2f, 0.42f, 0.95f);
        private static readonly Color PlusColor = new Color32(0x6B, 0x2C, 0x8F, 0xFF);
        private const string CostHex = "b500af";

        private static SpellbookWindow instance;

        /// <summary>The spell picked to put on the bar (-1 when none).</summary>
        public static int PendingSpell { get; private set; } = -1;

        public static void ClearPending() => PendingSpell = -1;

        private GameObject window;
        private GameObject toggleButton;
        private RectTransform windowRect;
        private Text title;
        private Text footer;
        private readonly List<Row> rows = new List<Row>();
        private Spellbook shownFor;
        private int shownCount = -1;
        private int columns = 1;
        private int perColumn = 1;

        private class Row
        {
            public Image Background;
            public Text Name;
            public Text Details;
            public Button Plus;
            public Text PlusText;
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

            Image panel = UIFactory.CreateImage("Spellbook", transform, PanelColor);
            window = panel.gameObject;
            panel.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            windowRect = panel.rectTransform;
            windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = new Vector2(0f, 40f);
            windowRect.sizeDelta = new Vector2(Width, 200f);

            title = UIFactory.CreateText("Title", windowRect, 20, TextAnchor.UpperLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(Width - 60f, 54f));
            title.color = TitleColor;
            Button close = UIFactory.CreateButton("Close", windowRect, new Color(0.35f, 0.12f, 0.1f, 0.95f), "X", 15);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(26f, 26f));
            close.onClick.AddListener(Toggle);

            footer = UIFactory.CreateText("Footer", windowRect, 14, TextAnchor.LowerLeft);
            UIFactory.Place(footer.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 34f), new Vector2(Width - 32f, 26f));
            footer.color = TitleColor;
            window.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            PendingSpell = -1;
        }

        private void Toggle()
        {
            window.SetActive(!window.activeSelf);
            if (!window.activeSelf) PendingSpell = -1;
        }

        private void Build(Spellbook spellbook)
        {
            foreach (Row row in rows) Destroy(row.Background.gameObject);
            rows.Clear();
            shownFor = spellbook;
            shownCount = spellbook.Spells.Count;
            // Long spellbooks (Dreadmyst classes have 20+ spells) in two columns.
            columns = shownCount > 12 ? 2 : 1;
            perColumn = Mathf.Max(1, (shownCount + columns - 1) / columns);
            for (int i = 0; i < shownCount; i++) rows.Add(CreateRow(i));
            windowRect.sizeDelta = new Vector2(Width * columns, Top + perColumn * (RowHeight + 4f) + 50f);
        }

        private Row CreateRow(int spell)
        {
            Button button = UIFactory.CreateButton("Spell", windowRect, KnownRow, null, 0);
            var rect = (RectTransform)button.transform;
            UIFactory.Place(rect, new Vector2(0f, 1f), new Vector2(14f + spell / perColumn * Width, -Top - spell % perColumn * (RowHeight + 4f)),
                new Vector2(Width - 28f, RowHeight));
            button.onClick.AddListener(() => Pick(spell));
            var row = new Row { Background = (Image)button.targetGraphic };

            row.Name = UIFactory.CreateText("Name", rect, 16, TextAnchor.UpperLeft);
            UIFactory.Place(row.Name.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -3f), new Vector2(Width - 120f, 22f));
            row.Details = UIFactory.CreateText("Details", rect, 12, TextAnchor.UpperLeft);
            UIFactory.Place(row.Details.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -24f), new Vector2(Width - 120f, 22f));
            row.Details.color = new Color(0.75f, 0.72f, 0.65f);

            row.Plus = UIFactory.CreateButton("Rank Up", rect, PlusColor, "+", 16);
            UIFactory.Place((RectTransform)row.Plus.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 12f), new Vector2(24f, 24f));
            row.Plus.onClick.AddListener(() => RankUp(spell));
            row.PlusText = UIFactory.CreateText("Cost", rect, 12, TextAnchor.MiddleRight);
            UIFactory.Place(row.PlusText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-38f, 12f), new Vector2(70f, 24f));
            return row;
        }

        private void Pick(int spell)
        {
            if (shownFor == null || !shownFor.Knows(spell)) return;
            PendingSpell = PendingSpell == spell ? -1 : spell;
            if (PendingSpell >= 0)
                MessageLog.Add($"Click a slot on the action bar to put {shownFor.Get(spell).displayName} there.", new Color(0.75f, 0.55f, 1f));
        }

        private void RankUp(int spell)
        {
            if (shownFor == null) return;
            if (shownFor.CanRankUp(spell)) shownFor.RequestRankUp(spell);
            else if (shownFor.NextRankCost(spell) > 0)
                MessageLog.Add($"You need {shownFor.NextRankCost(spell)} experience to raise {shownFor.Get(spell).displayName}.", MessageLog.WarningColor);
        }

        private void Update()
        {
            Spellbook spellbook = UIFactory.LocalPlayer<Spellbook>();
            bool hasPlayer = spellbook != null && spellbook.IsSpawned;
            if (toggleButton.activeSelf != hasPlayer) toggleButton.SetActive(hasPlayer);
            if (!hasPlayer)
            {
                if (window.activeSelf) window.SetActive(false);
                return;
            }

            var input = spellbook.GetComponent<PlayerInputReader>();
            if (input != null && input.enabled)
            {
                if (input.SpellbookPressed) Toggle();
                else if (input.CancelPressed && PendingSpell >= 0) PendingSpell = -1;
            }
            if (!window.activeSelf) return;
            if (spellbook != shownFor || spellbook.Spells.Count != shownCount) Build(spellbook);

            var progress = spellbook.GetComponent<CharacterProgress>();
            var caster = spellbook.GetComponent<AbilityCaster>();
            title.text = $"Spellbook\n<size=14><color=#8c8676>{progress.ClassName} spells. Click a known spell, then a slot on the bar.</color></size>";
            for (int i = 0; i < rows.Count; i++)
            {
                AbilityDefinition ability = spellbook.Get(i);
                Row row = rows[i];
                if (ability == null) continue;
                int rank = spellbook.RankOf(i);
                bool known = rank > 0;
                row.Background.color = PendingSpell == i ? PickedRow : known ? KnownRow : UnknownRow;
                string level = progress.Level >= ability.learnLevel ? $"level {ability.learnLevel}" : $"<color=#ff6655>level {ability.learnLevel}</color>";
                row.Name.text = known
                    ? $"{ability.displayName}  <size=13><color=#bbbbbb>Rank {rank}/{ability.maxRank}</color></size>"
                    : $"<color=#8a8478>{ability.displayName}</color>  <size=13><color=#8a8478>Tome from the trainer, {level}</color></size>";
                row.Name.color = known ? new Color(0.95f, 0.92f, 0.85f) : Color.white;
                int mana = caster != null ? caster.ManaCost(ability) : ability.manaCost;
                string cast = ability.castTime > 0f ? $"{ability.castTime:0.#}s cast" : "instant";
                row.Details.text = $"{mana} mana   {ability.cooldown:0.#}s cooldown   {cast}   {Shorten(ability.description)}";

                int cost = spellbook.NextRankCost(i);
                row.Plus.gameObject.SetActive(known && cost > 0);
                row.PlusText.text = known && cost > 0
                    ? $"<color=#{(spellbook.CanRankUp(i) ? CostHex : "7a6a7a")}>{cost}xp</color>"
                    : known ? "<color=#8a8478>max</color>" : "";
            }
            footer.text = $"Experience to spend: <color=#{CostHex}>{progress.ExperienceToSpend}xp</color>   (also spent on attributes, C)";
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > 70 ? text.Substring(0, 67) + "..." : text;
        }
    }
}
