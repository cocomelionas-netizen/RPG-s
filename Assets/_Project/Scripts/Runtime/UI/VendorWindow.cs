using System;
using System.Collections.Generic;
using RPG.Controls;
using RPG.Items;
using RPG.Progression;
using RPG.Vendors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The window of the vendor the local player is talking to: its goods with prices (click to buy), a Transmute button
    /// that opens its <see cref="TransmuteWindow"/> when it transmutes, and the player's gold. While it is open,
    /// clicking items in the bag sells them. The pointer over a row shows the item's tooltip, gear compared with what is
    /// worn (<see cref="ItemTooltip"/>). Closes on Esc, the X button, or when the player walks away. Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VendorWindow : MonoBehaviour
    {
        [SerializeField, Min(20f)] private float rowHeight = 56f;
        [SerializeField, Min(100f)] private float width = 470f;

        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.95f);
        private static readonly Color RowColor = new Color(0.16f, 0.14f, 0.12f, 0.95f);
        private static readonly Color Unaffordable = new Color(1f, 0.4f, 0.3f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);
        private const float WalkAwayDistance = 1.5f;

        private static VendorWindow instance;
        private Vendor requested;

        private GameObject window;
        private Text title;
        private Text goldText;
        private RectTransform list;
        private readonly List<Row> rows = new List<Row>();
        private Button craftButton;
        private Vendor shown;
        private int stockColumns = 1;
        private int stockPerColumn = 1;
        private int StockRows => rows.Count > 0 ? stockPerColumn : 0;
        // Long stock lists (the merchants' gear for every level) are shown a page at a time.
        private const int RowsPerColumn = 10;
        private const int PageSize = RowsPerColumn * 2;
        private int page;
        // The rows are rewritten only when something they show may have changed (gold, level, page) or a few times a
        // second (stock left, restocks), not every frame.
        private float nextRefresh;
        private int lastGold = -1;
        private int lastLevel = -1;
        private const float RefreshSeconds = 0.25f;
        /// <summary>Where the goods start, under the title and the gold.</summary>
        private const float ListTop = -104f;
        private int pageCount = 1;
        private RectTransform pager;
        private Text pageText;
        private ItemTooltip itemTooltip;
        // What the tooltip was built for: rebuilt only when the row, its affix, the gold or the worn gear changes.
        private Row tooltipRow;
        private int tooltipSignature;

        private class Row
        {
            public GameObject Root;
            public Text Name;
            public Text Details;
            public Text Price;
            public Image Icon;
            public int StockIndex;
        }

        /// <summary>The vendor whose window is open (null when closed).</summary>
        public static Vendor CurrentVendor => instance != null && instance.window.activeSelf ? instance.shown : null;

        /// <summary>Raised when a vendor window opens (the inventory opens next to it).</summary>
        public static event Action Opened;

        public static void Open(Vendor vendor)
        {
            if (instance != null) instance.requested = vendor;
        }

        /// <summary>Opens the transmute window of the vendor whose window is open.</summary>
        private void OpenTransmute()
        {
            if (shown == null) return;
            TransmuteWindow.Open(shown);
            Hide();
        }

        public static void Close()
        {
            if (instance != null) instance.Hide();
        }

        private void Awake()
        {
            instance = this;
            Image panel = UIFactory.CreateImage("Vendor", transform, PanelColor);
            window = panel.gameObject;
            window.AddComponent<OpenWindow>();
            UIFactory.AddFrame(panel);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, 30f);
            rect.sizeDelta = new Vector2(width, 200f);

            title = AddText(rect, 22, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(width - 60f, 54f));
            title.fontStyle = FontStyle.Bold;
            goldText = AddText(rect, 16, TextAnchor.UpperLeft, new Vector2(16f, -60f), new Vector2(width - 32f, 40f));
            goldText.color = GoldColor;

            Button close = UIFactory.CreateButton("Close", rect, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 18);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            closeRect.sizeDelta = new Vector2(34f, 34f);
            close.onClick.AddListener(Hide);

            craftButton = UIFactory.CreateButton("Transmute", rect, new Color(0.35f, 0.26f, 0.45f, 0.95f), "Transmute", 16);
            var craftRect = (RectTransform)craftButton.transform;
            craftRect.anchorMin = craftRect.anchorMax = craftRect.pivot = new Vector2(0.5f, 0f);
            craftRect.anchoredPosition = new Vector2(0f, 12f);
            craftRect.sizeDelta = new Vector2(width - 28f, 38f);
            craftButton.onClick.AddListener(OpenTransmute);

            pager = UIFactory.CreateRect("Pager", rect);
            pager.anchorMin = pager.anchorMax = pager.pivot = new Vector2(0f, 1f);
            pager.sizeDelta = new Vector2(width - 28f, 32f);
            Button previous = UIFactory.CreateButton("Previous", pager, RowColor, "<", 18);
            UIFactory.Place((RectTransform)previous.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(60f, 30f));
            previous.onClick.AddListener(() => TurnPage(-1));
            Button next = UIFactory.CreateButton("Next", pager, RowColor, ">", 18);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(60f, 30f));
            next.onClick.AddListener(() => TurnPage(1));
            pageText = UIFactory.CreateText("Page", pager, 15, TextAnchor.MiddleCenter);
            UIFactory.Place(pageText.rectTransform, new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(132f, 30f));

            list = UIFactory.CreateRect("Goods", rect);
            list.anchorMin = list.anchorMax = list.pivot = new Vector2(0f, 1f);
            list.anchoredPosition = new Vector2(16f, ListTop);
            list.sizeDelta = new Vector2(width - 28f, 10f);

            itemTooltip = new ItemTooltip(transform);
            window.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private Text AddText(RectTransform parent, int size, TextAnchor alignment, Vector2 topLeft, Vector2 boxSize)
        {
            Text text = UIFactory.CreateText("Text", parent, size, alignment);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = boxSize;
            return text;
        }

        private void Show(Vendor vendor)
        {
            bool fresh = vendor != shown || !window.activeSelf;
            if (fresh) page = 0;
            shown = vendor;
            VendorDefinition definition = vendor.Definition;
            title.text = definition != null && !string.IsNullOrEmpty(definition.title)
                ? $"{vendor.DisplayName}\n<size=15>{definition.title}</size>"
                : vendor.DisplayName;

            foreach (Row row in rows) Destroy(row.Root);
            rows.Clear();
            HideTooltip();
            ItemDefinition[] stock = definition != null ? definition.stock : Array.Empty<ItemDefinition>();
            pageCount = Mathf.Max(1, (stock.Length + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pageCount - 1);
            int first = page * PageSize;
            int onPage = Mathf.Clamp(stock.Length - first, 0, PageSize);
            stockColumns = onPage > RowsPerColumn ? 2 : 1;
            stockPerColumn = Mathf.Max(1, Mathf.Min(onPage, RowsPerColumn));
            for (int i = 0; i < onPage; i++)
                rows.Add(CreateRow(i, first + i));

            bool paged = pageCount > 1;
            pager.gameObject.SetActive(paged);
            pager.anchoredPosition = new Vector2(16f, ListTop - StockRows * (rowHeight + 4f) - 4f);
            pageText.text = $"Page {page + 1} / {pageCount}";

            bool transmutes = definition != null && definition.Transmutes;
            // A transmuter with nothing to sell (the source game's Mortimer) opens its transmute window at once.
            if (transmutes && stock.Length == 0)
            {
                TransmuteWindow.Open(vendor);
                shown = null;
                window.SetActive(false);
                return;
            }
            craftButton.gameObject.SetActive(transmutes);
            float height = -ListTop + StockRows * (rowHeight + 4f) + 16f + (paged ? 38f : 0f) + (transmutes ? 46f : 0f);
            ((RectTransform)window.transform).sizeDelta = new Vector2(width * stockColumns, height);
            window.SetActive(true);
            if (!fresh) return;
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory != null) inventory.RequestVendorStock(vendor);
            Opened?.Invoke();
        }

        private void TurnPage(int step)
        {
            if (shown == null) return;
            page = Mathf.Clamp(page + step, 0, pageCount - 1);
            Show(shown);
        }

        private void Hide()
        {
            window.SetActive(false);
            shown = null;
            HideTooltip();
        }

        private void HideTooltip()
        {
            tooltipRow = null;
            if (itemTooltip != null) itemTooltip.Hide();
        }

        /// <summary>The tooltip of the row under the pointer, compared with the worn gear.</summary>
        private void UpdateTooltip(Inventory inventory)
        {
            Row over = null;
            Mouse mouse = Mouse.current;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (mouse != null)
            {
                Vector2 pointer = mouse.position.ReadValue();
                Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                foreach (Row row in rows)
                    if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)row.Root.transform, pointer, eventCamera))
                    {
                        over = row;
                        break;
                    }
            }
            ItemDefinition[] stock = shown.Definition != null ? shown.Definition.stock : Array.Empty<ItemDefinition>();
            ItemDefinition item = over != null && over.StockIndex < stock.Length ? stock[over.StockIndex] : null;
            if (item == null || inventory.Catalog == null)
            {
                HideTooltip();
                return;
            }

            int affixId = shown.KnownAffix(over.StockIndex);
            var stack = new ItemStack(inventory.Catalog.IdOf(item), 1, affixId);
            int signature = affixId * 7919 + inventory.Gold;
            int wornIndex = Inventory.IndexOf(item.slot);
            for (int i = wornIndex; i >= 0 && i < wornIndex + 2; i++)
            {
                ItemStack worn = inventory.GetEquippedAt(i);
                signature = signature * 31 + worn.ItemId * 17 + worn.AffixId + worn.EmpowerSteps;
            }
            if (over != tooltipRow || signature != tooltipSignature)
            {
                tooltipRow = over;
                tooltipSignature = signature;
                int price = inventory.BuyPrice(item, inventory.Affixes != null ? inventory.Affixes.Get(affixId) : null);
                string hint = shown.KnownRemaining(over.StockIndex) == 0 ? "Sold out"
                    : inventory.Gold < price ? $"Costs {price} gold: not enough gold" : $"Click to buy for {price} gold";
                itemTooltip.Show(inventory, stack, hint, compare: true);
            }
            itemTooltip.Follow(canvas, new Vector2(0f, 1f), new Vector2(16f, -12f));
        }

        private Row CreateRow(int position, int stockIndex)
        {
            // Long stock lists (a trainer's Tomes) in two columns.
            Row row = CreateRowAt(-(position % stockPerColumn) * (rowHeight + 4f), RowColor, out Button button, position / stockPerColumn * width);
            row.StockIndex = stockIndex;
            nextRefresh = 0f;
            button.onClick.AddListener(() => Buy(stockIndex));
            return row;
        }

        private const float PriceWidth = 84f;

        private Row CreateRowAt(float top, Color color, out Button button, float left = 0f)
        {
            button = UIFactory.CreateButton("Item", list, color, null, 0);
            UIFactory.StyleBox((Image)button.targetGraphic);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, top);
            rect.sizeDelta = new Vector2(width - 32f, rowHeight);

            var row = new Row { Root = button.gameObject };
            // The item's picture on the left, the texts beside it.
            RectTransform iconBox = UIFactory.CreateRect("Icon Box", rect);
            UIFactory.Place(iconBox, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(rowHeight - 12f, rowHeight - 12f));
            row.Icon = UIFactory.CreateIcon(iconBox, 0f);
            // Name on one line, the details on up to two under it, the price (and how many are left) on the right.
            float textLeft = rowHeight;
            float textWidth = width - 32f - PriceWidth - textLeft - 8f;
            row.Name = AddText(rect, 15, TextAnchor.UpperLeft, new Vector2(textLeft, -5f), new Vector2(textWidth, 20f));
            row.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.Details = AddText(rect, 12, TextAnchor.UpperLeft, new Vector2(textLeft, -24f), new Vector2(textWidth, 30f));
            row.Details.lineSpacing = 0.9f;
            row.Price = AddText(rect, 16, TextAnchor.MiddleRight, new Vector2(width - 32f - PriceWidth - 8f, 0f), new Vector2(PriceWidth, rowHeight));
            return row;
        }

        private void Buy(int index)
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory != null && shown != null) inventory.RequestBuy(shown, index, shown.KnownAffix(index));
        }

        private void Update()
        {
            if (requested != null)
            {
                Show(requested);
                requested = null;
            }
            if (!window.activeSelf) return;

            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            PlayerInputReader input = inventory != null ? inventory.GetComponent<PlayerInputReader>() : null;
            bool walkedAway = inventory == null || shown == null
                              || Vector2.Distance(inventory.transform.position, shown.transform.position) > shown.TradeRange + WalkAwayDistance;
            if (walkedAway || OpenWindow.EscapeCloses(input))
            {
                Hide();
                return;
            }

            UpdateTooltip(inventory);
            var progress = inventory.GetComponent<CharacterProgress>();
            int level = progress != null ? progress.Level : 0;
            if (inventory.Gold == lastGold && level == lastLevel && Time.unscaledTime < nextRefresh) return;
            lastGold = inventory.Gold;
            lastLevel = level;
            nextRefresh = Time.unscaledTime + RefreshSeconds;

            goldText.text = $"Your gold: {inventory.Gold}\n<size=13><color=#c8c0b0>Click an item to buy. Click items in your bag to sell them.</color></size>";
            ItemDefinition[] stock = shown.Definition.stock;
            AffixCatalog affixCatalog = inventory.Affixes;
            foreach (Row row in rows)
            {
                int i = row.StockIndex;
                ItemDefinition item = i >= 0 && i < stock.Length ? stock[i] : null;
                if (item == null) continue;
                AffixDefinition affix = affixCatalog != null ? affixCatalog.Get(shown.KnownAffix(i)) : null;
                string itemName = item.NameWith(affix);
                int left = shown.KnownRemaining(i);
                row.Name.text = itemName;
                row.Name.color = ItemDefinition.RarityColor(item.QualityWith(affix));
                UIFactory.SetIcon(row.Icon, item.icon, left == 0 ? item.iconTint * 0.4f : item.iconTint);
                row.Details.text = Summary(item, progress, affix);
                int price = inventory.BuyPrice(item, affix);
                row.Price.text = left == Vendor.Unlimited ? $"{price} g"
                    : left > 0 ? $"{price} g\n<size=12><color=#bbbbbb>{left} left</color></size>"
                    : $"{price} g\n<size=12><color=#ff6655>sold out</color></size>";
                row.Price.color = inventory.Gold >= price && left != 0 ? GoldColor : Unaffordable;
            }
        }

        /// <summary>One line: slot, bonuses (with an affix's) and which class may use it.</summary>
        public static string Summary(ItemDefinition item, CharacterProgress progress, AffixDefinition affix = null)
        {
            var parts = new List<string>();
            if (item.IsEquippable)
            {
                parts.Add(item.weaponType != WeaponType.None ? item.weaponType.ToString() : item.slot.ToString());
                bool wearable = progress == null || item.CanBeWornAtLevel(progress.Level);
                parts.Add(wearable ? $"level {item.itemLevel}" : $"<color=#ff6655>level {item.itemLevel}</color>");
            }
            int damage = item.damageBonus + (affix != null ? affix.damageBonus : 0);
            int health = item.healthBonus + (affix != null ? affix.healthBonus : 0);
            int armor = item.armor + (affix != null ? affix.armor : 0);
            int mana = item.manaBonus + (affix != null ? affix.manaBonus : 0);
            AttributeSet attributes = item.attributes + (affix != null ? affix.attributes : default);
            if (damage > 0) parts.Add($"+{damage} dmg");
            if (health > 0) parts.Add($"+{health} HP");
            if (armor > 0) parts.Add($"{armor} armor");
            if (mana > 0) parts.Add($"+{mana} mana");
            if (!attributes.IsZero) parts.Add(attributes.Describe("  "));
            if (item.isGem) parts.Add("gem: set into a socket");
            switch (item.use)
            {
                case ItemUse.Heal:
                case ItemUse.RestoreMana:
                    string pool = item.use == ItemUse.Heal ? "health" : "mana";
                    string amount = item.restoreAmount > 0 ? $"{item.restoreAmount} {pool}" : $"{Mathf.RoundToInt(item.healFraction * 100f)}% {pool}";
                    parts.Add(item.restoreSeconds > 0f ? $"{amount} over {item.restoreSeconds:0}s" : amount);
                    break;
                case ItemUse.Food:
                    parts.Add($"{item.restoreAmount} health and mana, out of combat");
                    break;
                case ItemUse.Return:
                    parts.Add("returns you to St. Aurelis");
                    break;
                case ItemUse.Buff:
                    parts.Add($"+{item.buffAmount} {ItemDefinition.AttributeName(item.buffAttribute)} for {Mathf.RoundToInt(item.buffSeconds / 60f)} min");
                    break;
                case ItemUse.LearnSpell:
                    if (item.teachesSpell != null) parts.Add($"teaches {item.teachesSpell.displayName}");
                    break;
            }
            if (!item.IsEquippable && item.use != ItemUse.None && item.itemLevel > 1)
            {
                bool usable = progress == null || item.CanBeUsedAtLevel(progress.Level);
                parts.Add(usable ? $"level {item.itemLevel}" : $"<color=#ff6655>level {item.itemLevel}</color>");
            }
            string className = ClassName(item, progress);
            if (className != null)
            {
                bool usable = progress == null || item.CanBeUsedByClass(progress.ClassIndex);
                parts.Add(usable ? className : $"<color=#ff6655>{className} only</color>");
            }
            return string.Join("   ", parts);
        }

        /// <summary>The names of the classes an item is limited to, "Warrior" or "Warrior/Ranger" (null when anyone may use it).</summary>
        public static string ClassName(ItemDefinition item, CharacterProgress progress)
        {
            if (item == null || (item.requiredClass < 0 && item.classMask == 0)) return null;
            ClassCatalog catalog = progress != null ? progress.Catalog : null;
            if (item.requiredClass >= 0)
            {
                CharacterClassDefinition definition = catalog != null ? catalog.Get(item.requiredClass) : null;
                return definition != null ? definition.displayName : "Class";
            }
            var names = new List<string>();
            for (int i = 0; i < 31; i++)
            {
                if ((item.classMask & (1 << i)) == 0) continue;
                CharacterClassDefinition definition = catalog != null ? catalog.Get(i) : null;
                names.Add(definition != null ? definition.displayName : $"Class {i + 1}");
            }
            return string.Join("/", names);
        }
    }
}
