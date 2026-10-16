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
    /// The local player's character sheet (C, or the Character button), laid out like the source game's: the GENERAL,
    /// COMBAT and SKILLS tabs; the worn gear down both sides (click to take off, drag gear from the bag to put it on), the
    /// name, level, progression and experience between them; "Label: value" rows of the tab's stats, a small purple + on
    /// every stat while SPEND EXP is on, and a tooltip for every row and item.
    /// Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CharacterWindow : MonoBehaviour
    {
        private const float Width = 640f;
        private const float RowHeight = 24f;
        private const float LabelX = 340f;
        private const float ValueX = 470f;
        private const float PlusX = 604f;

        // Colours taken from the source game's character panel.
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
        private static readonly Color EmptySlotColor = new Color32(0x1E, 0x1C, 0x14, 0xF2);
        private const string TooltipHeaderHex = "d3c370";
        // The exact colour the source game's client draws the "5xp" price in (181, 0, 175).
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
                new Row { Label = "Damage:", Value = () => $"{attack.ScaledMinDamage} - {attack.ScaledMaxDamage}",
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

        private static readonly string[] PageNames = { "GENERAL", "COMBAT", "SKILLS" };

        // The source game's paperdoll: equipment indices (Inventory.SlotAt) down the left and the right of the sheet.
        private static readonly int[] LeftSlots = { 1, 4, 2, 7, 10, 0 };
        private static readonly int[] RightSlots = { 8, 9, 5, 6, 3 };
        private static readonly string[] SlotNames = { "Weapon", "Helm", "Chest", "Feet", "Necklace", "Belt", "Legs", "Hands", "Ring", "Ring", "Offhand" };
        private const float SlotSize = 50f;
        private const float SlotGap = 8f;
        private const float PaperdollWidth = 320f;

        private readonly List<Button> tabs = new List<Button>();
        private readonly ItemSlotView[] slotViews = new ItemSlotView[Inventory.EquipmentSlots];
        private readonly List<GameObject> plusButtons = new List<GameObject>();
        private Text nameText;
        private Text levelText;
        private Text progressionText;
        private Text experienceText;
        private bool spending;

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
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, 40f);

            title = AddText(rect, "CHARACTER", 20, TextAnchor.UpperCenter, new Vector2(0f, -10f), new Vector2(Width, 28f));
            title.color = TitleColor;
            Button close = UIFactory.CreateButton("Close", rect, new Color(0.35f, 0.12f, 0.1f, 0.95f), "X", 15);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(26f, 26f));
            close.onClick.AddListener(Toggle);

            // The tabs, like the source game's: GENERAL, COMBAT, SKILLS.
            for (int i = 0; i < PageNames.Length; i++)
            {
                int index = i;
                Button tab = UIFactory.CreateButton(PageNames[i], rect, Color.clear, PageNames[i], 14);
                UIFactory.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(40f + i * 100f, -44f), new Vector2(96f, 26f));
                tab.onClick.AddListener(() => ShowPage(index));
                tabs.Add(tab);
            }

            BuildPaperdoll(rect);

            BuildPages();
            float lowest = -86f - 6f * (SlotSize + SlotGap);
            foreach (List<Row> rows in pages)
            {
                RectTransform root = UIFactory.CreateRect("Page", rect);
                UIFactory.Stretch(root);
                pageRoots.Add(root.gameObject);
                float y = -86f;
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

            float bottom = lowest - 12f;
            Button spend = UIFactory.CreateButton("Spend Exp", rect, new Color32(0x4A, 0x1E, 0x14, 0xF2), "SPEND EXP", 14);
            UIFactory.Place((RectTransform)spend.transform, new Vector2(0f, 1f), new Vector2(14f, bottom), new Vector2(120f, 28f));
            spend.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            spend.onClick.AddListener(() => SetSpending(!spending));
            points = AddText(rect, "", 15, TextAnchor.MiddleLeft, new Vector2(146f, bottom), new Vector2(Width - 160f, 28f));
            points.color = TitleColor;
            rect.sizeDelta = new Vector2(Width, -bottom + 28f + 14f);

            BuildTooltip();
            ShowPage(0);
            SetSpending(false);
            window.SetActive(false);
        }

        /// <summary>The worn gear down both sides, and between them the name, level and experience, like the source game's.</summary>
        private void BuildPaperdoll(RectTransform rect)
        {
            Image divider = UIFactory.CreateImage("Divider", rect, BorderColor);
            UIFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(PaperdollWidth, -80f), new Vector2(2f, 6f * (SlotSize + SlotGap)));
            for (int column = 0; column < 2; column++)
            {
                int[] slots = column == 0 ? LeftSlots : RightSlots;
                float x = column == 0 ? 18f : PaperdollWidth - 18f - SlotSize;
                for (int i = 0; i < slots.Length; i++)
                    slotViews[slots[i]] = CreateEquipmentSlot(rect, slots[i], new Vector2(x, -86f - i * (SlotSize + SlotGap)));
            }

            float center = 18f + SlotSize + 8f;
            float centerWidth = PaperdollWidth - 2f * center;
            nameText = AddText(rect, "", 17, TextAnchor.UpperCenter, new Vector2(center, -90f), new Vector2(centerWidth, 24f));
            nameText.color = ValueColor;
            levelText = AddText(rect, "", 12, TextAnchor.UpperCenter, new Vector2(center, -114f), new Vector2(centerWidth, 34f));
            levelText.color = LabelColor;
            Text progressionLabel = AddText(rect, "PROGRESSION", 14, TextAnchor.UpperCenter, new Vector2(center, -170f), new Vector2(centerWidth, 20f));
            progressionLabel.color = TitleColor;
            progressionText = AddText(rect, "", 14, TextAnchor.UpperCenter, new Vector2(center, -190f), new Vector2(centerWidth, 20f));
            progressionText.color = ValueColor;
            Text experienceLabel = AddText(rect, "EXPERIENCE", 14, TextAnchor.UpperCenter, new Vector2(center, -224f), new Vector2(centerWidth, 20f));
            experienceLabel.color = TitleColor;
            experienceText = AddText(rect, "", 14, TextAnchor.UpperCenter, new Vector2(center, -244f), new Vector2(centerWidth, 20f));
            experienceText.color = ValueColor;
        }

        private ItemSlotView CreateEquipmentSlot(RectTransform parent, int equipIndex, Vector2 topLeft)
        {
            Button button = UIFactory.CreateButton(SlotNames[equipIndex], parent, EmptySlotColor, null, 0);
            var rect = (RectTransform)button.transform;
            UIFactory.Place(rect, new Vector2(0f, 1f), topLeft, new Vector2(SlotSize, SlotSize));
            ItemSlotView view = button.gameObject.AddComponent<ItemSlotView>();
            view.Background = (Image)button.targetGraphic;
            view.Border = button.gameObject.AddComponent<Outline>();
            view.Border.effectDistance = new Vector2(2f, -2f);
            view.Label = UIFactory.CreateText("Name", rect, 10, TextAnchor.MiddleCenter);
            UIFactory.Stretch(view.Label.rectTransform);
            view.Label.rectTransform.offsetMin = new Vector2(2f, 2f);
            view.Label.rectTransform.offsetMax = new Vector2(-2f, -2f);
            button.onClick.AddListener(() => OnSlotClicked(equipIndex));
            view.PointerEntered += _ => ShowTooltip(() => SlotTooltip(equipIndex));
            view.PointerExited += _ => HideTooltip();

            // Gear dragged from the bag onto its slot is put on.
            var drop = button.gameObject.AddComponent<DropTarget>();
            drop.Accepts = payload => payload.Kind == DragKind.Item && payload.BagIndex >= 0;
            drop.Dropped = payload =>
            {
                if (inventory != null) inventory.RequestEquip(payload.BagIndex);
            };
            return view;
        }

        private void OnSlotClicked(int equipIndex)
        {
            if (inventory == null) return;
            // A gem picked in the bag goes into the clicked worn item; otherwise the item comes off.
            int gem = InventoryWindow.PendingGem;
            if (gem >= 0)
            {
                inventory.RequestSocketGem(gem, true, equipIndex);
                InventoryWindow.ClearPendingGem();
                return;
            }
            inventory.RequestUnequipAt(equipIndex);
        }

        private string SlotTooltip(int equipIndex)
        {
            if (inventory == null) return SlotNames[equipIndex];
            ItemStack stack = inventory.GetEquippedAt(equipIndex);
            ItemDefinition item = inventory.Definition(stack);
            if (item == null) return $"<color=#{TooltipHeaderHex}>{SlotNames[equipIndex]}</color>\nEmpty. Drag gear here from the bag (I).";
            string text = inventory.Describe(stack, VendorWindow.ClassName(item, progress));
            return text.Replace("Click to equip", "Click to take off");
        }

        private void SetSpending(bool on)
        {
            spending = on;
            foreach (GameObject plus in plusButtons) plus.SetActive(on);
        }

        private void ShowEquipment()
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                ItemSlotView view = slotViews[i];
                if (view == null) continue;
                ItemStack stack = inventory != null ? inventory.GetEquippedAt(i) : ItemStack.Empty;
                ItemDefinition item = inventory != null ? inventory.Definition(stack) : null;
                if (item == null)
                {
                    view.Background.color = EmptySlotColor;
                    view.Border.effectColor = new Color(0f, 0f, 0f, 0.5f);
                    view.Label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(LabelColor)}>{SlotNames[i]}</color>";
                    continue;
                }
                Color rarity = ItemDefinition.RarityColor(inventory.Quality(stack));
                Color background = item.iconColor * 0.55f;
                background.a = 0.95f;
                view.Background.color = background;
                view.Border.effectColor = rarity;
                view.Label.text = inventory.DisplayName(stack);
                view.Label.color = rarity;
            }
        }

        private void ShowPage(int index)
        {
            page = (index % pages.Count + pages.Count) % pages.Count;
            for (int i = 0; i < pageRoots.Count; i++) pageRoots[i].SetActive(i == page);
            for (int i = 0; i < tabs.Count; i++)
                tabs[i].GetComponentInChildren<Text>().color = i == page ? new Color32(0xC0, 0x3A, 0x2A, 0xFF) : LabelColor;
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
            plusButtons.Add(plus.gameObject);

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
            nameText.text = health.DisplayName.ToUpperInvariant();
            levelText.text = $"Level {progress.Level} {progress.ClassName} ({LevelCurve.RankName(progress.Level)})";
            progressionText.text = progress.ExperienceToNext > 0 ? $"{progress.Experience} / {progress.ExperienceToNext}" : "Max level";
            experienceText.text = progress.ExperienceToSpend.ToString();
            ShowEquipment();

            total = progress.TotalAttributes;
            fromItems = inventory != null ? inventory.Attributes : default;
            foreach (Row row in pages[page])
                if (row != null) row.ValueText.text = row.Value();
            points.text = spending ? $"Click + to raise a stat. Experience to spend: <color=#{CostHex}>{progress.ExperienceToSpend}xp</color>" : "";

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
