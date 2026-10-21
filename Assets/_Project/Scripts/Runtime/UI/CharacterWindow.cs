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
        // The source game's character panel (equipment.png): 603 x 571, the stats column right of x 360.
        private const float Width = 603f;
        private const float Height = 571f;
        private const float RowHeight = 26f;
        private const float RowsTop = 122f;
        private const float LabelX = 375f;
        private const float ValueX = 500f;
        private const float PlusX = 562f;

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
        private System.Func<string> shownTooltip;
        private CharacterProgress progress;
        private Health health;
        private Mana mana;
        private AutoAttack attack;
        private Inventory inventory;
        private AttributeSet total;
        private AttributeSet fromItems;

        private static string Percent(float chance) => $"{Mathf.RoundToInt(chance * 1000f) / 10f}%";

        /// <summary>An attribute's total, with what items add in green; while editing with the points picked in purple.</summary>
        private string AttributeValue(Attribute attribute, string extra = null)
        {
            int picked = PendingOf(attribute);
            bool untrainable = progress != null && !progress.CanTrain(attribute);
            string shown = picked > 0 ? $"<color=#{CostHex}>{total[attribute] + picked}</color>"
                : untrainable ? $"<color=#a04030>{total[attribute]}</color>"
                : fromItems[attribute] != 0 ? $"{total[attribute]} <size=13><color=#7fbf6a>(+{fromItems[attribute]})</color></size>"
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
                new Row { Label = "Health:", Value = () => PendingOf(Attribute.Health) > 0
                        ? $"<color=#{CostHex}>{health.Max + PendingOf(Attribute.Health) * AttributeRules.HealthPerPoint}</color>" : health.Max.ToString(),
                    Plus = Attribute.Health,
                    Description = "The damage you can take before you fall. Grows with points bought with experience (+1 each), Courage, Strength, levels and gear." },
                new Row { Label = "Mana:", Value = () => mana == null ? "0" : PendingOf(Attribute.Mana) > 0
                        ? $"<color=#{CostHex}>{mana.Max + PendingOf(Attribute.Mana) * AttributeRules.ManaPerPoint}</color>" : mana.Max.ToString(),
                    Plus = Attribute.Mana, Mana = true,
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
                new Row { Label = "Damage:", Value = () => $"{attack.ScaledMinDamage} - {attack.ScaledMaxDamage}",
                    Description = "The damage of your attacks; abilities multiply it. Your weapon's skill raises it." },
                new Row { Label = "Critical Chance:", Value = () => Percent(progress.CritChance),
                    Description = "The chance for an attack to deal double damage: your class, Agility and the melee or ranged critical rating." },
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
            // Like the source game's SKILLS tab: the weapon skills (only the class's own can be trained), then trading and locks.
            skills.Add(null);
            skills.Add(Stat(Attribute.Bartering, "Vendors charge 0.5% less and pay 0.5% more per point, up to 25%.", () => Percent(AttributeRules.BarteringDiscount(total))));
            skills.Add(Stat(Attribute.Lockpicking, "Opens locked chests: you need twice the chest's level."));
            pages.Add(skills);
        }

        private static readonly string[] PageNames = { "GENERAL", "COMBAT", "SKILLS" };

        // The source game's paperdoll: equipment indices (Inventory.SlotAt) down the left and the right of the sheet.
        // Left: Helm, Necklace, Chest, Ranged, Offhand, Weapon; right: two Rings, Hands, Belt, Legs, Feet. Ranged has no
        // index of its own here (bows are worn as the weapon): it shows a worn bow.
        private const int RangedSlot = -2;
        private static readonly int[] LeftSlots = { 1, 4, 2, RangedSlot, 10, 0 };
        private static readonly int[] RightSlots = { 8, 9, 7, 5, 6, 3 };
        private static readonly string[] SlotNames = { "Weapon", "Helm", "Chest", "Feet", "Necklace", "Belt", "Legs", "Hands", "Ring", "Ring", "Offhand" };
        private const float SlotSize = 40f;
        private const float SlotStep = 58f;
        private const float SlotsTop = 179f;
        private const float PaperdollWidth = 360f;
        private ItemSlotView rangedView;

        private readonly List<Button> tabs = new List<Button>();
        private readonly ItemSlotView[] slotViews = new ItemSlotView[Inventory.EquipmentSlots];
        private readonly List<(GameObject button, Attribute attribute)> plusButtons = new List<(GameObject, Attribute)>();
        // Points picked while editing (SPEND EXP), bought on CONFIRM.
        private readonly Dictionary<Attribute, int> pending = new Dictionary<Attribute, int>();
        private Button spendButton;
        private Button confirmButton;
        private Button cancelButton;
        private Text costLabel;
        private Text costText;
        private Text editNote;
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
            window.AddComponent<OpenWindow>();
            UIFactory.AddFrame(panel);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, 40f);
            rect.sizeDelta = new Vector2(Width, Height);

            // The header with the title and the tab bar under it, where the source game's panel has them.
            Image header = UIFactory.CreateImage("Header", rect, new Color32(0x4A, 0x16, 0x12, 0xF5));
            UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -14f), new Vector2(Width - 20f, 52f));
            Image tabBar = UIFactory.CreateImage("Tabs", rect, new Color32(0x14, 0x12, 0x11, 0xF5));
            UIFactory.Place(tabBar.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -68f), new Vector2(Width - 20f, 38f));
            title = AddText(rect, "CHARACTER", 21, TextAnchor.MiddleCenter, new Vector2(0f, -26f), new Vector2(Width, 30f));
            title.color = new Color32(0xE6, 0xD8, 0xC0, 0xFF);
            Button close = UIFactory.CreateButton("Close", rect, new Color(0.35f, 0.12f, 0.1f, 0.95f), "X", 15);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-14f, -26f), new Vector2(26f, 26f));
            close.onClick.AddListener(Toggle);

            // The tabs, like the source game's: GENERAL, COMBAT, SKILLS.
            float[] tabCenters = { 156f, 296f, 431f };
            for (int i = 0; i < PageNames.Length; i++)
            {
                int index = i;
                Button tab = UIFactory.CreateButton(PageNames[i], rect, Color.clear, PageNames[i], 14);
                UIFactory.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(tabCenters[i] - 55f, -74f), new Vector2(110f, 26f));
                tab.onClick.AddListener(() => ShowPage(index));
                tabs.Add(tab);
            }

            BuildPaperdoll(rect);

            BuildPages();
            foreach (List<Row> rows in pages)
            {
                RectTransform root = UIFactory.CreateRect("Page", rect);
                UIFactory.Stretch(root);
                pageRoots.Add(root.gameObject);
                float y = -RowsTop;
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
            }

            float bottom = -534f;
            spendButton = BottomButton(rect, "SPEND EXP", 14f, bottom, () => SetSpending(true));
            confirmButton = BottomButton(rect, "CONFIRM", 14f, bottom, Confirm);
            cancelButton = BottomButton(rect, "CANCEL", 158f, bottom, () => SetSpending(false));
            points = AddText(rect, "", 15, TextAnchor.MiddleLeft, new Vector2(PaperdollWidth + 14f, bottom), new Vector2(Width - PaperdollWidth - 28f, 28f));
            points.color = TitleColor;
            editNote = AddText(rect, "Only base values are shown\nwhile editing", 12, TextAnchor.MiddleCenter,
                new Vector2(PaperdollWidth + 10f, -462f), new Vector2(Width - PaperdollWidth - 30f, 34f));
            editNote.color = LabelColor;

            BuildTooltip();
            ShowPage(0);
            SetSpending(false);
            window.SetActive(false);
            VendorWindow.Opened += CloseForTrade;
            TransmuteWindow.Opened += CloseForTrade;
        }

        private void OnDestroy()
        {
            VendorWindow.Opened -= CloseForTrade;
            TransmuteWindow.Opened -= CloseForTrade;
        }

        /// <summary>A vendor's or the transmuter's window opens where the sheet stands: the sheet closes.</summary>
        private void CloseForTrade()
        {
            if (window.activeSelf) Toggle();
        }

        /// <summary>The worn gear down both sides, and between them the name, level and experience, like the source game's.</summary>
        private void BuildPaperdoll(RectTransform rect)
        {
            Image divider = UIFactory.CreateImage("Divider", rect, BorderColor);
            UIFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(PaperdollWidth, -110f), new Vector2(2f, 430f));
            for (int column = 0; column < 2; column++)
            {
                int[] slots = column == 0 ? LeftSlots : RightSlots;
                float x = column == 0 ? 46f : 288f;
                for (int i = 0; i < slots.Length; i++)
                {
                    ItemSlotView view = CreateEquipmentSlot(rect, slots[i], new Vector2(x, -SlotsTop - i * SlotStep));
                    if (slots[i] == RangedSlot) rangedView = view;
                    else slotViews[slots[i]] = view;
                }
            }

            // Between the columns, as on the source game's panel: name and level, PROGRESSION, EXPERIENCE (and COST while editing).
            const float center = 100f;
            const float centerWidth = 180f;
            nameText = AddText(rect, "", 16, TextAnchor.UpperCenter, new Vector2(center, -112f), new Vector2(centerWidth, 22f));
            nameText.color = ValueColor;
            levelText = AddText(rect, "", 12, TextAnchor.UpperCenter, new Vector2(center - 20f, -134f), new Vector2(centerWidth + 40f, 32f));
            levelText.color = LabelColor;
            Text progressionLabel = AddText(rect, "PROGRESSION", 14, TextAnchor.UpperCenter, new Vector2(center, -220f), new Vector2(centerWidth, 20f));
            progressionLabel.color = TitleColor;
            progressionText = AddText(rect, "", 14, TextAnchor.UpperCenter, new Vector2(center, -240f), new Vector2(centerWidth, 20f));
            progressionText.color = ValueColor;
            Text experienceLabel = AddText(rect, "EXPERIENCE", 14, TextAnchor.UpperCenter, new Vector2(center, -278f), new Vector2(centerWidth, 20f));
            experienceLabel.color = TitleColor;
            experienceText = AddText(rect, "", 14, TextAnchor.UpperCenter, new Vector2(center, -298f), new Vector2(centerWidth, 20f));
            experienceText.color = ValueColor;
            costLabel = AddText(rect, "COST", 14, TextAnchor.UpperCenter, new Vector2(center, -336f), new Vector2(centerWidth, 20f));
            costLabel.color = TitleColor;
            costText = AddText(rect, "", 14, TextAnchor.UpperCenter, new Vector2(center, -356f), new Vector2(centerWidth, 20f));
            costText.color = ValueColor;
        }

        /// <summary>The silhouette an empty equipment slot shows (UI art Slot&lt;Part&gt;).</summary>
        private static string SilhouetteOf(int slot)
        {
            switch (slot == RangedSlot ? "Ranged" : SlotNames[slot])
            {
                case "Helm": return "Head";
                case "Chest": return "Chest";
                case "Feet": return "Feet";
                case "Necklace": return "Neck";
                case "Belt": return "Shoulders";
                case "Legs": return "Legs";
                case "Hands": return "Hands";
                case "Ring": return "Wrist";
                case "Offhand": return "Shield";
                default: return "Weapon";
            }
        }

        private static string SlotName(int equipIndex) => equipIndex == RangedSlot ? "Ranged" : SlotNames[equipIndex];

        private ItemSlotView CreateEquipmentSlot(RectTransform parent, int slot, Vector2 topLeft)
        {
            // The Ranged slot shows the worn bow, which is the weapon here.
            int equipIndex = slot == RangedSlot ? 0 : slot;
            Button button = UIFactory.CreateButton(SlotName(slot), parent, EmptySlotColor, null, 0);
            Image placeholder = UIFactory.MakeSlot(button, SilhouetteOf(slot));
            var rect = (RectTransform)button.transform;
            UIFactory.Place(rect, new Vector2(0f, 1f), topLeft, new Vector2(SlotSize, SlotSize));
            ItemSlotView view = button.gameObject.AddComponent<ItemSlotView>();
            view.Background = (Image)button.targetGraphic;
            view.Placeholder = placeholder;
            view.Border = button.gameObject.AddComponent<Outline>();
            view.Border.effectDistance = new Vector2(2f, -2f);
            view.Icon = UIFactory.CreateIcon(rect, 4f);
            view.Label = UIFactory.CreateText("Name", rect, 8, TextAnchor.MiddleCenter);
            UIFactory.Stretch(view.Label.rectTransform);
            view.Label.rectTransform.offsetMin = new Vector2(2f, 2f);
            view.Label.rectTransform.offsetMax = new Vector2(-2f, -2f);
            button.onClick.AddListener(() => OnSlotClicked(equipIndex));
            view.PointerEntered += _ => ShowTooltip(() => SlotTooltip(slot));
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

        /// <summary>What a paperdoll slot shows: the worn item, but a bow only in Ranged and a melee weapon only in Weapon.</summary>
        private ItemStack Shown(int slot)
        {
            if (inventory == null) return ItemStack.Empty;
            ItemStack stack = inventory.GetEquippedAt(slot == RangedSlot ? 0 : slot);
            if (slot != RangedSlot && slot != 0) return stack;
            ItemDefinition item = inventory.Definition(stack);
            bool ranged = item != null && item.weaponType == WeaponType.Ranged;
            return (slot == RangedSlot) == ranged ? stack : ItemStack.Empty;
        }

        private string SlotTooltip(int slot)
        {
            if (inventory == null) return SlotName(slot);
            ItemStack stack = Shown(slot);
            ItemDefinition item = inventory.Definition(stack);
            if (item == null) return $"<color=#{TooltipHeaderHex}>{SlotName(slot)}</color>\nEmpty. Drag gear here from the bag (I).";
            string text = inventory.Describe(stack, VendorWindow.ClassName(item, progress));
            return text.Replace("Click to equip", "Click to take off");
        }

        private Button BottomButton(RectTransform rect, string label, float x, float y, UnityEngine.Events.UnityAction action)
        {
            Button button = UIFactory.CreateButton(label, rect, new Color32(0x4A, 0x1E, 0x14, 0xF2), label, 14);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(120f, 28f));
            button.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            button.onClick.AddListener(action);
            return button;
        }

        /// <summary>
        /// SPEND EXP: the + buttons of the stats the class can train, CONFIRM / CANCEL, the cost so far, and the abilities
        /// window next to the sheet, like the source game's. Turning it off (CANCEL) forgets the picked points.
        /// </summary>
        private void SetSpending(bool on)
        {
            spending = on;
            pending.Clear();
            RefreshEditing();
            if (on) SpellbookWindow.OpenBeside((RectTransform)window.transform);
        }

        private void RefreshEditing()
        {
            foreach ((GameObject button, Attribute attribute) in plusButtons)
                button.SetActive(spending && (progress == null || progress.CanTrain(attribute)));
            spendButton.gameObject.SetActive(!spending);
            confirmButton.gameObject.SetActive(spending);
            cancelButton.gameObject.SetActive(spending);
            costLabel.gameObject.SetActive(spending);
            costText.gameObject.SetActive(spending);
            editNote.gameObject.SetActive(spending);
        }

        private int PendingOf(Attribute attribute) => pending.TryGetValue(attribute, out int count) ? count : 0;

        /// <summary>What the picked points cost together.</summary>
        private int PendingCost()
        {
            int cost = 0;
            if (progress == null) return 0;
            foreach (KeyValuePair<Attribute, int> pick in pending) cost += progress.PointsCost(pick.Key, pick.Value);
            return cost;
        }

        private void Confirm()
        {
            if (progress != null)
                foreach (KeyValuePair<Attribute, int> pick in pending) progress.RequestSpendPoints(pick.Key, pick.Value);
            SetSpending(false);
        }

        private void ShowEquipment()
        {
            for (int i = -2; i < slotViews.Length; i++)
            {
                if (i == -1) continue;
                ItemSlotView view = i == RangedSlot ? rangedView : slotViews[i];
                if (view == null) continue;
                ItemStack stack = Shown(i);
                ItemDefinition item = inventory != null ? inventory.Definition(stack) : null;
                if (item == null)
                {
                    view.Show(null, "", Color.white, EmptySlotColor);
                    view.Label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(LabelColor)}>{SlotName(i)}</color>";
                    continue;
                }
                view.Show(item, inventory.DisplayName(stack), ItemDefinition.RarityColor(inventory.Quality(stack)), EmptySlotColor);
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
            plusButtons.Add((plus.gameObject, attribute));

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
            int cost = AttributeRules.PointCost(attribute, invested + PendingOf(attribute));
            bool affordable = progress != null && PendingCost() + cost <= progress.ExperienceToSpend;
            string header = $"<color=#{TooltipHeaderHex}>{gain}:</color> <color=#{(affordable ? CostHex : TooExpensiveHex)}>{cost}xp</color>";
            string spent = invested > 0 ? $"You have invested {invested} point{(invested == 1 ? "" : "s")}." : "You haven't invested anything.";
            return $"{header}\n{spent}";
        }

        private void BuildTooltip()
        {
            Image background = UIFactory.CreateImage("Stat Tooltip", transform, TooltipColor);
            UIFactory.StyleBox(background);
            UIFactory.MakeTopmost(background.gameObject);
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
            nextRefresh = 0f;
            if (window.activeSelf) return;
            HideTooltip();
            // Closing drops the points picked but not confirmed.
            if (spending) SetSpending(false);
        }

        // The texts are rewritten when what they show changed, or a few times a second (buffs), not every frame.
        private float nextRefresh;
        private int shownState;
        private const float RefreshSeconds = 0.25f;

        private int StateOf()
        {
            unchecked
            {
                int state = progress.Level;
                state = state * 31 + progress.Experience;
                state = state * 31 + progress.ExperienceToSpend;
                state = state * 31 + PendingCost();
                state = state * 31 + page;
                state = state * 31 + (spending ? 1 : 0);
                state = state * 31 + (inventory != null ? inventory.Gold : 0);
                return state;
            }
        }

        /// <summary>Picks one more point (bought on CONFIRM), when the experience covers everything picked.</summary>
        private void Spend(Attribute attribute)
        {
            if (progress == null) return;
            if (!progress.CanTrain(attribute))
            {
                MessageLog.Add($"Your class cannot train {AttributeRules.NameOf(attribute)}.", MessageLog.WarningColor);
                return;
            }
            int next = AttributeRules.PointCost(attribute, progress.SpentPoints[attribute] + PendingOf(attribute));
            if (PendingCost() + next > progress.ExperienceToSpend)
            {
                MessageLog.Add($"You need {next} more experience to raise {AttributeRules.NameOf(attribute)}.", MessageLog.WarningColor);
                return;
            }
            pending[attribute] = PendingOf(attribute) + 1;
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
            if (input != null && input.enabled && (input.CharacterPressed || OpenWindow.EscapeCloses(input) && window.activeSelf)) Toggle();
            if (!window.activeSelf)
            {
                tooltip.gameObject.SetActive(false);
                return;
            }

            health = progress.GetComponent<Health>();
            mana = progress.GetComponent<Mana>();
            attack = progress.GetComponent<AutoAttack>();
            inventory = progress.GetComponent<Inventory>();
            int state = StateOf();
            if (state == shownState && Time.unscaledTime < nextRefresh)
            {
                UpdateTooltip(false);
                return;
            }
            shownState = state;
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            nameText.text = health.DisplayName.ToUpperInvariant();
            levelText.text = $"Level {progress.Level} {progress.ClassName} ({LevelCurve.RankName(progress.Level)})";
            progressionText.text = progress.ExperienceToNext > 0 ? $"{progress.Experience} / {progress.ExperienceToNext}" : "Max level";
            experienceText.text = (progress.ExperienceToSpend - (spending ? PendingCost() : 0)).ToString();
            costText.text = PendingCost().ToString();
            ShowEquipment();

            total = progress.TotalAttributes;
            fromItems = inventory != null ? inventory.Attributes : default;
            foreach (Row row in pages[page])
                if (row != null) row.ValueText.text = row.Value();
            points.text = "";
            if (spending) RefreshEditing();

            UpdateTooltip(true);
        }

        /// <summary>
        /// Shows the hovered element's tooltip. Its text is built when the hovered element changes or the window redraws
        /// (<paramref name="refresh"/>), not every frame; only its position follows the pointer.
        /// </summary>
        private void UpdateTooltip(bool refresh)
        {
            bool show = tooltipContent != null;
            if (tooltip.gameObject.activeSelf != show) tooltip.gameObject.SetActive(show);
            if (!show)
            {
                shownTooltip = null;
                return;
            }
            if (refresh || tooltipContent != shownTooltip)
            {
                shownTooltip = tooltipContent;
                tooltipText.text = tooltipContent();
                tooltip.sizeDelta = new Vector2(280f, tooltipText.preferredHeight + 18f);
            }
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : Vector2.zero;
            tooltip.anchoredPosition = UIFactory.KeepOnScreen(tooltip, pointer / scale + new Vector2(16f, -12f), scale);
        }
    }
}
