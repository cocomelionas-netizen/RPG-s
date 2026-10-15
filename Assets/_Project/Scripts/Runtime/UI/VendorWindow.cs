using System;
using System.Collections.Generic;
using RPG.Controls;
using RPG.Items;
using RPG.Progression;
using RPG.Vendors;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The window of the vendor the local player is talking to: its goods with prices (click to buy) and the
    /// player's gold. While it is open, clicking items in the bag sells them. Closes on Esc, the X button,
    /// or when the player walks away. Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VendorWindow : MonoBehaviour
    {
        [SerializeField, Min(20f)] private float rowHeight = 46f;
        [SerializeField, Min(100f)] private float width = 420f;

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
        private Vendor shown;

        private class Row
        {
            public GameObject Root;
            public Text Name;
            public Text Details;
            public Text Price;
        }

        /// <summary>The vendor whose window is open (null when closed).</summary>
        public static Vendor CurrentVendor => instance != null && instance.window.activeSelf ? instance.shown : null;

        /// <summary>Raised when a vendor window opens (the inventory opens next to it).</summary>
        public static event Action Opened;

        public static void Open(Vendor vendor)
        {
            if (instance != null) instance.requested = vendor;
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
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, 30f);
            rect.sizeDelta = new Vector2(width, 200f);

            title = AddText(rect, 22, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(width - 60f, 54f));
            title.fontStyle = FontStyle.Bold;
            goldText = AddText(rect, 16, TextAnchor.UpperLeft, new Vector2(14f, -62f), new Vector2(width - 28f, 22f));
            goldText.color = GoldColor;

            Button close = UIFactory.CreateButton("Close", rect, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 18);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            closeRect.sizeDelta = new Vector2(34f, 34f);
            close.onClick.AddListener(Hide);

            list = UIFactory.CreateRect("Goods", rect);
            list.anchorMin = list.anchorMax = list.pivot = new Vector2(0f, 1f);
            list.anchoredPosition = new Vector2(14f, -92f);
            list.sizeDelta = new Vector2(width - 28f, 10f);

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
            shown = vendor;
            VendorDefinition definition = vendor.Definition;
            title.text = definition != null && !string.IsNullOrEmpty(definition.title)
                ? $"{vendor.DisplayName}\n<size=15>{definition.title}</size>"
                : vendor.DisplayName;

            foreach (Row row in rows) Destroy(row.Root);
            rows.Clear();
            ItemDefinition[] stock = definition != null ? definition.stock : Array.Empty<ItemDefinition>();
            for (int i = 0; i < stock.Length; i++)
                rows.Add(CreateRow(i));

            float height = 92f + stock.Length * (rowHeight + 4f) + 40f;
            ((RectTransform)window.transform).sizeDelta = new Vector2(width, height);
            window.SetActive(true);
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory != null) inventory.RequestVendorStock(vendor);
            Opened?.Invoke();
        }

        private void Hide()
        {
            window.SetActive(false);
            shown = null;
        }

        private Row CreateRow(int index)
        {
            Button button = UIFactory.CreateButton("Item", list, RowColor, null, 0);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * (rowHeight + 4f));
            rect.sizeDelta = new Vector2(width - 28f, rowHeight);
            button.onClick.AddListener(() => Buy(index));

            var row = new Row { Root = button.gameObject };
            row.Name = AddText(rect, 16, TextAnchor.UpperLeft, new Vector2(8f, -3f), new Vector2(width - 140f, 22f));
            row.Details = AddText(rect, 12, TextAnchor.UpperLeft, new Vector2(8f, -24f), new Vector2(width - 140f, 20f));
            row.Price = AddText(rect, 16, TextAnchor.MiddleRight, new Vector2(width - 140f, 0f), new Vector2(104f, rowHeight));
            return row;
        }

        private void Buy(int index)
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory != null && shown != null) inventory.RequestBuy(shown, index);
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
            if (walkedAway || (input != null && input.CancelPressed))
            {
                Hide();
                return;
            }

            goldText.text = $"Your gold: {inventory.Gold}      Click an item to buy. While this is open, click items in your bag to sell them.";
            var progress = inventory.GetComponent<CharacterProgress>();
            ItemDefinition[] stock = shown.Definition.stock;
            for (int i = 0; i < rows.Count && i < stock.Length; i++)
            {
                ItemDefinition item = stock[i];
                Row row = rows[i];
                if (item == null) continue;
                int left = shown.KnownRemaining(i);
                row.Name.text = left == Vendor.Unlimited ? item.displayName
                    : left > 0 ? $"{item.displayName}  <size=13><color=#bbbbbb>({left} left)</color></size>"
                    : $"{item.displayName}  <size=13><color=#ff6655>(sold out)</color></size>";
                row.Name.color = ItemDefinition.RarityColor(item.rarity);
                row.Details.text = Summary(item, progress);
                row.Price.text = $"{item.price} g";
                row.Price.color = inventory.Gold >= item.price && left != 0 ? GoldColor : Unaffordable;
            }
        }

        /// <summary>One line: slot, bonuses and which class may use it.</summary>
        public static string Summary(ItemDefinition item, CharacterProgress progress)
        {
            var parts = new List<string>();
            if (item.IsEquippable) parts.Add(item.slot.ToString());
            if (item.damageBonus > 0) parts.Add($"+{item.damageBonus} dmg");
            if (item.healthBonus > 0) parts.Add($"+{item.healthBonus} HP");
            if (item.armor > 0) parts.Add($"{item.armor} armor");
            if (item.manaBonus > 0) parts.Add($"+{item.manaBonus} mana");
            if (!item.attributes.IsZero) parts.Add(item.attributes.Describe("  "));
            if (item.use == ItemUse.Heal) parts.Add($"heals {Mathf.RoundToInt(item.healFraction * 100f)}%");
            if (item.use == ItemUse.RestoreMana) parts.Add($"restores {Mathf.RoundToInt(item.healFraction * 100f)}% mana");
            string className = ClassName(item, progress);
            if (className != null)
            {
                bool usable = progress == null || item.CanBeUsedByClass(progress.ClassIndex);
                parts.Add(usable ? className : $"<color=#ff6655>{className} only</color>");
            }
            return string.Join("   ", parts);
        }

        /// <summary>The name of the class an item is limited to (null when anyone may use it).</summary>
        public static string ClassName(ItemDefinition item, CharacterProgress progress)
        {
            if (item == null || item.requiredClass < 0) return null;
            CharacterClassDefinition definition = progress != null && progress.Catalog != null ? progress.Catalog.Get(item.requiredClass) : null;
            return definition != null ? definition.displayName : "Class";
        }
    }
}
