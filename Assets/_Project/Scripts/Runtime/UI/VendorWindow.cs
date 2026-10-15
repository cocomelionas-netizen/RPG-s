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
    /// The window of the vendor the local player is talking to: its goods with prices (click to buy), the gear it
    /// crafts from the materials in the player's bag (click to craft) and the player's gold. While it is open,
    /// clicking items in the bag sells them. Closes on Esc, the X button, or when the player walks away. Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VendorWindow : MonoBehaviour
    {
        [SerializeField, Min(20f)] private float rowHeight = 46f;
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
        private Button combineButton;
        private readonly List<Row> rows = new List<Row>();
        private readonly List<Row> craftRows = new List<Row>();
        private Text craftHeader;
        private Vendor shown;
        // Craft list entries shown, and the ones found this frame.
        private readonly List<int> shownRecipes = new List<int>();
        private readonly List<int> recipes = new List<int>();
        private const int MaxCraftRows = 6;
        private static readonly Color CraftRowColor = new Color(0.12f, 0.15f, 0.12f, 0.95f);
        private static readonly Color EnoughColor = new Color(0.55f, 0.95f, 0.5f);

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

            combineButton = UIFactory.CreateButton("Combine", rect, new Color(0.2f, 0.35f, 0.45f, 0.95f), "Combine gems (3 of a kind = 1 better)", 15);
            var combineRect = (RectTransform)combineButton.transform;
            combineRect.anchorMin = combineRect.anchorMax = combineRect.pivot = new Vector2(0.5f, 0f);
            combineRect.anchoredPosition = new Vector2(0f, 12f);
            combineRect.sizeDelta = new Vector2(width - 28f, 34f);
            combineButton.onClick.AddListener(CombineGems);

            list = UIFactory.CreateRect("Goods", rect);
            list.anchorMin = list.anchorMax = list.pivot = new Vector2(0f, 1f);
            list.anchoredPosition = new Vector2(14f, -92f);
            list.sizeDelta = new Vector2(width - 28f, 10f);

            craftHeader = AddText(list, 15, TextAnchor.LowerLeft, Vector2.zero, new Vector2(width - 28f, 40f));
            craftHeader.color = new Color(0.85f, 0.8f, 0.65f);

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

            bool combines = definition != null && (definition.combinesGems || definition.combinesPotions);
            combineButton.gameObject.SetActive(combines);
            if (combines)
                combineButton.GetComponentInChildren<Text>().text = definition.combinesGems
                    ? "Combine gems (3 of a kind = 1 better)"
                    : "Combine potions (5 of a kind = 1 stronger)";
            shownRecipes.Clear();
            shownRecipes.Add(-1); // Forces the craft rows to be laid out.
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
            Row row = CreateRowAt(-index * (rowHeight + 4f), RowColor, out Button button);
            button.onClick.AddListener(() => Buy(index));
            return row;
        }

        private Row CreateRowAt(float top, Color color, out Button button)
        {
            button = UIFactory.CreateButton("Item", list, color, null, 0);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = new Vector2(width - 28f, rowHeight);

            var row = new Row { Root = button.gameObject };
            row.Name = AddText(rect, 16, TextAnchor.UpperLeft, new Vector2(8f, -3f), new Vector2(width - 176f, 22f));
            row.Details = AddText(rect, 12, TextAnchor.UpperLeft, new Vector2(8f, -24f), new Vector2(width - 176f, 20f));
            row.Price = AddText(rect, 16, TextAnchor.MiddleRight, new Vector2(width - 172f, 0f), new Vector2(136f, rowHeight));
            return row;
        }

        /// <summary>
        /// The craft list entries to offer: for each piece the vendor crafts, its best level the player can wear,
        /// when the player has some of its material. Highest level first, at most <see cref="MaxCraftRows"/>.
        /// </summary>
        public static void FindRecipes(VendorDefinition definition, Inventory inventory, int playerLevel, List<int> into)
        {
            into.Clear();
            if (definition == null || inventory == null || definition.crafts == null) return;
            ItemDefinition[] crafts = definition.crafts;
            for (int i = 0; i < crafts.Length; i++)
            {
                ItemDefinition item = crafts[i];
                if (item == null || !item.IsCraftable || item.itemLevel > playerLevel) continue;
                // Only the best level of each piece.
                int same = into.FindIndex(other => crafts[other].displayName == item.displayName);
                if (same >= 0)
                {
                    if (crafts[into[same]].itemLevel < item.itemLevel) into[same] = i;
                    continue;
                }
                if (inventory.CountOf(item.craftMaterial) > 0) into.Add(i);
            }
            into.Sort((a, b) => crafts[b].itemLevel != crafts[a].itemLevel
                ? crafts[b].itemLevel.CompareTo(crafts[a].itemLevel)
                : string.CompareOrdinal(crafts[a].displayName, crafts[b].displayName));
            if (into.Count > MaxCraftRows) into.RemoveRange(MaxCraftRows, into.Count - MaxCraftRows);
        }

        private void LayOutCrafts()
        {
            foreach (Row row in craftRows) Destroy(row.Root);
            craftRows.Clear();
            shownRecipes.Clear();
            shownRecipes.AddRange(recipes);

            float top = -rows.Count * (rowHeight + 4f) - 4f;
            craftHeader.rectTransform.anchoredPosition = new Vector2(0f, top);
            top -= 44f;
            for (int i = 0; i < recipes.Count; i++)
            {
                int craftIndex = recipes[i];
                craftRows.Add(CreateRowAt(top - i * (rowHeight + 4f), CraftRowColor, out Button button));
                button.onClick.AddListener(() => Craft(craftIndex));
            }

            bool jeweler = combineButton.gameObject.activeSelf;
            bool crafts = shown.Definition.crafts != null && shown.Definition.crafts.Length > 0;
            float height = 92f + rows.Count * (rowHeight + 4f) + (crafts ? 48f : 0f) + recipes.Count * (rowHeight + 4f) + 40f + (jeweler ? 46f : 0f);
            ((RectTransform)window.transform).sizeDelta = new Vector2(width, height);
        }

        private void Craft(int craftIndex)
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory != null && shown != null) inventory.RequestCraft(shown, craftIndex);
        }

        private void CombineGems()
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory == null || shown == null) return;
            if (shown.Definition.combinesGems) inventory.RequestCombineGems(shown);
            else inventory.RequestCombinePotions(shown);
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
            UpdateCrafts(inventory, progress);
        }

        private void UpdateCrafts(Inventory inventory, CharacterProgress progress)
        {
            VendorDefinition definition = shown.Definition;
            bool crafts = definition.crafts != null && definition.crafts.Length > 0;
            FindRecipes(definition, inventory, progress != null ? progress.Level : 1, recipes);
            bool changed = recipes.Count != shownRecipes.Count;
            for (int i = 0; !changed && i < recipes.Count; i++) changed = recipes[i] != shownRecipes[i];
            if (changed) LayOutCrafts();

            craftHeader.gameObject.SetActive(crafts);
            craftHeader.text = recipes.Count > 0
                ? "Craft from your materials (click to craft):"
                : "Craft: monsters, elites and bosses drop materials.\nBring them here and I will make gear from them.";
            for (int i = 0; i < craftRows.Count; i++)
            {
                ItemDefinition item = definition.crafts[shownRecipes[i]];
                Row row = craftRows[i];
                int have = inventory.CountOf(item.craftMaterial);
                row.Name.text = $"{item.displayName}  <size=13><color=#bbbbbb>(level {item.itemLevel})</color></size>";
                row.Name.color = ItemDefinition.RarityColor(item.rarity);
                row.Details.text = Summary(item, progress);
                row.Price.text = $"<size=13>{have}/{item.craftCount}\n{item.craftMaterial.displayName}</size>";
                row.Price.color = have >= item.craftCount ? EnoughColor : Unaffordable;
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
            if (item.isGem) parts.Add("gem: set into a socket");
            if (item.use != ItemUse.None)
            {
                string pool = item.use == ItemUse.Heal ? "health" : "mana";
                string amount = item.restoreAmount > 0 ? $"{item.restoreAmount} {pool}" : $"{Mathf.RoundToInt(item.healFraction * 100f)}% {pool}";
                parts.Add(item.restoreSeconds > 0f ? $"{amount} over {item.restoreSeconds:0}s" : amount);
                if (item.itemLevel > 1)
                {
                    bool usable = progress == null || item.CanBeUsedAtLevel(progress.Level);
                    parts.Add(usable ? $"level {item.itemLevel}" : $"<color=#ff6655>level {item.itemLevel}</color>");
                }
            }
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
