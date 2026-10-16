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
    /// The crafting window of a crafting vendor, laid out like Throne and Liberty's: category tabs on the left (weapon,
    /// head, chest, feet, potions, other), the list of what can be made in the middle, and on the right the chosen
    /// piece with its level, its materials (have / need), the quantity (- / +, 10, 30, 50, 100), the vendor's fee and the
    /// Craft button, which says what is missing ("Insufficient crafting materials"). Closes on Esc, the X button, or
    /// when the player walks away. Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CraftingWindow : MonoBehaviour
    {
        private const float Width = 960f;
        private const float Height = 600f;
        private const float TabWidth = 120f;
        private const float ListWidth = 380f;
        private const float RowHeight = 44f;
        private const int RowsPerPage = 10;
        private const int MaxQuantity = 100;
        private const float WalkAwayDistance = 1.5f;

        private static readonly Color PanelColor = new Color(0.07f, 0.065f, 0.09f, 0.97f);
        private static readonly Color SectionColor = new Color(0.12f, 0.11f, 0.15f, 0.95f);
        private static readonly Color RowColor = new Color(0.14f, 0.13f, 0.17f, 0.95f);
        private static readonly Color SelectedColor = new Color(0.3f, 0.25f, 0.42f, 0.98f);
        private static readonly Color TabColor = new Color(0.16f, 0.15f, 0.2f, 0.95f);
        private static readonly Color ActiveTabColor = new Color(0.42f, 0.3f, 0.55f, 0.98f);
        private static readonly Color CraftColor = new Color(0.35f, 0.3f, 0.2f, 0.98f);
        private static readonly Color Missing = new Color(1f, 0.4f, 0.35f);
        private static readonly Color Enough = new Color(0.85f, 0.85f, 0.8f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);
        private static readonly Color HeaderColor = new Color(0.85f, 0.8f, 0.65f);

        private enum Category { Weapon, Head, Chest, Feet, Potions, Other }

        private static readonly string[] CategoryNames = { "Weapons", "Head", "Chest", "Feet", "Potions", "Other" };

        /// <summary>One line of the list: a piece of gear (at every level it is made in) or one other recipe.</summary>
        private class Entry
        {
            public string Name;
            public ItemDefinition Shown;
            /// <summary>Gear: indices into the vendor's craft list, lowest level first.</summary>
            public readonly List<int> CraftIndices = new List<int>();
            /// <summary>Other recipes: index into the vendor's recipes (-1 for gear).</summary>
            public int RecipeIndex = -1;
        }

        private class Row
        {
            public Button Button;
            public Image Background;
            public Text Name;
            public Text Details;
        }

        private static CraftingWindow instance;
        private Vendor requested;
        private Vendor shown;

        private GameObject window;
        private Text title;
        private Text goldText;
        private readonly List<Button> tabs = new List<Button>();
        private readonly List<Row> rows = new List<Row>();
        private Text pageText;
        private Text emptyText;
        private Text selectedName;
        private Text selectedDetails;
        private Text levelText;
        private GameObject levelPicker;
        private readonly List<Text> materialNames = new List<Text>();
        private readonly List<Text> materialCounts = new List<Text>();
        private Text quantityText;
        private Text feeText;
        private Text statusText;
        private Button craftButton;
        private GameObject details;

        private readonly List<Entry>[] entries = new List<Entry>[CategoryNames.Length];
        private Category category;
        private int page;
        private Entry selected;
        private int selectedLevelIndex;
        private int quantity = 1;

        /// <summary>Whether the crafting window is open.</summary>
        public static bool IsOpen => instance != null && instance.window.activeSelf;

        public static void Open(Vendor vendor)
        {
            if (instance != null) instance.requested = vendor;
        }

        private void Awake()
        {
            instance = this;
            for (int i = 0; i < entries.Length; i++) entries[i] = new List<Entry>();

            Image panel = UIFactory.CreateImage("Crafting", transform, PanelColor);
            window = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(-120f, 30f), new Vector2(Width, Height));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.45f, 0.36f, 0.6f, 0.9f);

            title = Label(rect, 28, TextAnchor.UpperLeft, new Vector2(18f, -12f), new Vector2(500f, 40f));
            title.fontStyle = FontStyle.Bold;
            goldText = Label(rect, 18, TextAnchor.UpperRight, new Vector2(Width - 320f, -18f), new Vector2(250f, 26f));
            goldText.color = GoldColor;
            Button close = UIFactory.CreateButton("Close", rect, new Color(0.45f, 0.12f, 0.1f, 0.95f), "X", 20);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(38f, 38f));
            close.onClick.AddListener(Hide);

            // Category tabs on the left.
            for (int i = 0; i < CategoryNames.Length; i++)
            {
                int index = i;
                Button tab = UIFactory.CreateButton(CategoryNames[i], rect, TabColor, CategoryNames[i], 15);
                UIFactory.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(18f, -70f - i * 58f), new Vector2(TabWidth - 18f, 52f));
                tab.onClick.AddListener(() => SelectCategory((Category)index));
                tabs.Add(tab);
            }

            // The list in the middle.
            float listLeft = TabWidth + 14f;
            for (int i = 0; i < RowsPerPage; i++)
            {
                int index = i;
                Button button = UIFactory.CreateButton("Recipe", rect, RowColor, null, 0);
                var rowRect = (RectTransform)button.transform;
                UIFactory.Place(rowRect, new Vector2(0f, 1f), new Vector2(listLeft, -70f - i * (RowHeight + 4f)), new Vector2(ListWidth, RowHeight));
                button.onClick.AddListener(() => SelectRow(index));
                var row = new Row { Button = button, Background = (Image)button.targetGraphic };
                row.Name = Label(rowRect, 15, TextAnchor.UpperLeft, new Vector2(10f, -4f), new Vector2(ListWidth - 20f, 20f));
                row.Details = Label(rowRect, 12, TextAnchor.UpperLeft, new Vector2(10f, -24f), new Vector2(ListWidth - 20f, 18f));
                row.Details.color = new Color(0.72f, 0.7f, 0.66f);
                rows.Add(row);
            }
            emptyText = Label(rect, 15, TextAnchor.UpperLeft, new Vector2(listLeft + 10f, -80f), new Vector2(ListWidth - 20f, 80f));
            emptyText.color = HeaderColor;
            float pagerTop = -70f - RowsPerPage * (RowHeight + 4f) - 4f;
            Button previous = UIFactory.CreateButton("Previous", rect, TabColor, "<", 18);
            UIFactory.Place((RectTransform)previous.transform, new Vector2(0f, 1f), new Vector2(listLeft, pagerTop), new Vector2(44f, 30f));
            previous.onClick.AddListener(() => ChangePage(-1));
            Button next = UIFactory.CreateButton("Next", rect, TabColor, ">", 18);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0f, 1f), new Vector2(listLeft + ListWidth - 44f, pagerTop), new Vector2(44f, 30f));
            next.onClick.AddListener(() => ChangePage(1));
            pageText = Label(rect, 14, TextAnchor.MiddleCenter, new Vector2(listLeft + 50f, pagerTop), new Vector2(ListWidth - 100f, 30f));

            BuildDetails(rect, listLeft + ListWidth + 16f);
            window.SetActive(false);
        }

        private void BuildDetails(RectTransform parent, float left)
        {
            float width = Width - left - 18f;
            Image section = UIFactory.CreateImage("Details", parent, SectionColor);
            UIFactory.Place(section.rectTransform, new Vector2(0f, 1f), new Vector2(left, -70f), new Vector2(width, Height - 88f));
            details = section.gameObject;
            RectTransform rect = section.rectTransform;

            selectedName = Label(rect, 20, TextAnchor.UpperCenter, new Vector2(10f, -10f), new Vector2(width - 20f, 28f));
            selectedDetails = Label(rect, 13, TextAnchor.UpperCenter, new Vector2(10f, -42f), new Vector2(width - 20f, 40f));
            selectedDetails.color = new Color(0.78f, 0.76f, 0.7f);

            // The level of a piece of gear: < level N >.
            var picker = UIFactory.CreateRect("Level", rect);
            UIFactory.Place(picker, new Vector2(0f, 1f), new Vector2(10f, -88f), new Vector2(width - 20f, 30f));
            levelPicker = picker.gameObject;
            Button lower = UIFactory.CreateButton("Lower", picker, TabColor, "<", 16);
            UIFactory.Place((RectTransform)lower.transform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(34f, 28f));
            lower.onClick.AddListener(() => ChangeLevel(-1));
            Button higher = UIFactory.CreateButton("Higher", picker, TabColor, ">", 16);
            UIFactory.Place((RectTransform)higher.transform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(34f, 28f));
            higher.onClick.AddListener(() => ChangeLevel(1));
            levelText = Label(picker, 15, TextAnchor.MiddleCenter, new Vector2(80f, 0f), new Vector2(width - 180f, 30f));
            levelText.rectTransform.anchorMin = levelText.rectTransform.anchorMax = levelText.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text header = Label(rect, 16, TextAnchor.UpperLeft, new Vector2(14f, -128f), new Vector2(width - 28f, 24f));
            header.text = "Crafting Material";
            header.color = HeaderColor;
            header.fontStyle = FontStyle.Bold;
            for (int i = 0; i < 4; i++)
            {
                Image line = UIFactory.CreateImage("Material", rect, RowColor);
                line.raycastTarget = false;
                UIFactory.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -156f - i * 40f), new Vector2(width - 28f, 36f));
                Text name = Label(line.rectTransform, 14, TextAnchor.MiddleLeft, new Vector2(10f, 0f), new Vector2(width - 140f, 36f));
                Text count = Label(line.rectTransform, 15, TextAnchor.MiddleRight, new Vector2(width - 138f, 0f), new Vector2(100f, 36f));
                materialNames.Add(name);
                materialCounts.Add(count);
            }

            // Quantity: 10 30 50 100, then - N +.
            float quantityTop = -330f;
            int[] amounts = { 10, 30, 50, 100 };
            float buttonWidth = (width - 28f - 18f) / amounts.Length;
            for (int i = 0; i < amounts.Length; i++)
            {
                int amount = amounts[i];
                Button button = UIFactory.CreateButton($"x{amount}", rect, TabColor, amount.ToString(), 15);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(14f + i * (buttonWidth + 6f), quantityTop),
                    new Vector2(buttonWidth, 30f));
                button.onClick.AddListener(() => SetQuantity(amount));
            }
            Button minus = UIFactory.CreateButton("Minus", rect, TabColor, "-", 20);
            UIFactory.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(14f, quantityTop - 38f), new Vector2(44f, 32f));
            minus.onClick.AddListener(() => SetQuantity(quantity - 1));
            Button plus = UIFactory.CreateButton("Plus", rect, TabColor, "+", 20);
            UIFactory.Place((RectTransform)plus.transform, new Vector2(1f, 1f), new Vector2(-14f, quantityTop - 38f), new Vector2(44f, 32f));
            plus.onClick.AddListener(() => SetQuantity(quantity + 1));
            quantityText = Label(rect, 18, TextAnchor.MiddleCenter, new Vector2(64f, quantityTop - 38f), new Vector2(width - 128f, 32f));

            statusText = Label(rect, 14, TextAnchor.MiddleCenter, new Vector2(10f, quantityTop - 80f), new Vector2(width - 20f, 24f));
            statusText.color = Missing;

            craftButton = UIFactory.CreateButton("Craft", rect, CraftColor, null, 0);
            UIFactory.Place((RectTransform)craftButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(width - 28f, 50f));
            craftButton.onClick.AddListener(Craft);
            feeText = Label((RectTransform)craftButton.transform, 20, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(width - 28f, 50f));
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private static Text Label(RectTransform parent, int size, TextAnchor alignment, Vector2 topLeft, Vector2 boxSize)
        {
            Text text = UIFactory.CreateText("Text", parent, size, alignment);
            UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), topLeft, boxSize);
            return text;
        }

        // ---------- Showing ----------

        private void Show(Vendor vendor)
        {
            shown = vendor;
            title.text = $"Craft  <size=16><color=#b5a07a>{vendor.DisplayName}</color></size>";
            BuildEntries(vendor.Definition, UIFactory.LocalPlayer<CharacterProgress>());
            window.SetActive(true);
            // The first tab that has something.
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Count == 0) continue;
                SelectCategory((Category)i);
                break;
            }
        }

        private void Hide()
        {
            window.SetActive(false);
            shown = null;
            selected = null;
        }

        /// <summary>Sorts the vendor's craft list and recipes into the categories, one entry per piece of gear.</summary>
        private void BuildEntries(VendorDefinition definition, CharacterProgress progress)
        {
            foreach (List<Entry> list in entries) list.Clear();
            if (definition == null) return;

            var byName = new Dictionary<string, Entry>();
            ItemDefinition[] crafts = definition.crafts ?? Array.Empty<ItemDefinition>();
            for (int i = 0; i < crafts.Length; i++)
            {
                ItemDefinition item = crafts[i];
                if (item == null || !item.IsCraftable) continue;
                if (!byName.TryGetValue(item.displayName, out Entry entry))
                {
                    entry = new Entry { Name = item.displayName, Shown = item };
                    byName[item.displayName] = entry;
                    entries[(int)CategoryOf(item)].Add(entry);
                }
                entry.CraftIndices.Add(i);
            }
            foreach (Entry entry in byName.Values)
                entry.CraftIndices.Sort((a, b) => crafts[a].itemLevel.CompareTo(crafts[b].itemLevel));

            Recipe[] recipes = definition.recipes ?? Array.Empty<Recipe>();
            for (int i = 0; i < recipes.Length; i++)
            {
                if (!recipes[i].IsValid) continue;
                ItemDefinition result = recipes[i].result;
                var entry = new Entry { Name = result.displayName, Shown = result, RecipeIndex = i };
                bool potion = result.use == ItemUse.Heal || result.use == ItemUse.RestoreMana;
                entries[(int)(potion ? Category.Potions : Category.Other)].Add(entry);
            }

            foreach (List<Entry> list in entries)
                list.Sort((a, b) => FirstLevel(a, definition) != FirstLevel(b, definition)
                    ? FirstLevel(a, definition).CompareTo(FirstLevel(b, definition))
                    : string.CompareOrdinal(a.Name, b.Name));
        }

        private static int FirstLevel(Entry entry, VendorDefinition definition) =>
            entry.CraftIndices.Count > 0 ? definition.crafts[entry.CraftIndices[0]].itemLevel : entry.Shown.itemLevel;

        private static Category CategoryOf(ItemDefinition item)
        {
            switch (item.slot)
            {
                case ItemSlot.Weapon: return Category.Weapon;
                case ItemSlot.Head: return Category.Head;
                case ItemSlot.Chest: return Category.Chest;
                case ItemSlot.Feet: return Category.Feet;
                default: return Category.Other;
            }
        }

        private void SelectCategory(Category newCategory)
        {
            category = newCategory;
            page = 0;
            List<Entry> list = entries[(int)category];
            Select(list.Count > 0 ? list[0] : null);
        }

        private void ChangePage(int change)
        {
            int pages = Mathf.Max(1, (entries[(int)category].Count + RowsPerPage - 1) / RowsPerPage);
            page = Mathf.Clamp(page + change, 0, pages - 1);
        }

        private void SelectRow(int row)
        {
            List<Entry> list = entries[(int)category];
            int index = page * RowsPerPage + row;
            if (index < list.Count) Select(list[index]);
        }

        private void Select(Entry entry)
        {
            selected = entry;
            quantity = 1;
            selectedLevelIndex = 0;
            if (entry == null || entry.CraftIndices.Count == 0 || shown == null) return;
            // The best level the player can wear.
            var progress = UIFactory.LocalPlayer<CharacterProgress>();
            int level = progress != null ? progress.Level : 1;
            ItemDefinition[] crafts = shown.Definition.crafts;
            for (int i = 0; i < entry.CraftIndices.Count; i++)
                if (crafts[entry.CraftIndices[i]].itemLevel <= level) selectedLevelIndex = i;
        }

        private void ChangeLevel(int change)
        {
            if (selected == null || selected.CraftIndices.Count == 0) return;
            selectedLevelIndex = Mathf.Clamp(selectedLevelIndex + change, 0, selected.CraftIndices.Count - 1);
        }

        private void SetQuantity(int amount)
        {
            quantity = Mathf.Clamp(amount, 1, MaxQuantity);
        }

        /// <summary>The item the selection makes (the gear piece at the chosen level, or the recipe's result).</summary>
        private ItemDefinition SelectedItem()
        {
            if (selected == null || shown == null) return null;
            if (selected.RecipeIndex >= 0) return shown.Definition.recipes[selected.RecipeIndex].result;
            return shown.Definition.crafts[selected.CraftIndices[selectedLevelIndex]];
        }

        /// <summary>The materials of the selection for one craft.</summary>
        private void SelectedMaterials(List<RecipeIngredient> into)
        {
            into.Clear();
            if (selected == null || shown == null) return;
            if (selected.RecipeIndex >= 0)
            {
                foreach (RecipeIngredient ingredient in shown.Definition.recipes[selected.RecipeIndex].ingredients)
                    if (ingredient.item != null) into.Add(ingredient);
                return;
            }
            ItemDefinition piece = SelectedItem();
            into.Add(new RecipeIngredient(piece.craftMaterial, piece.craftCount));
        }

        private readonly List<RecipeIngredient> materials = new List<RecipeIngredient>();

        private void Craft()
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory == null || shown == null || selected == null) return;
            if (selected.RecipeIndex >= 0) inventory.RequestCraftRecipe(shown, selected.RecipeIndex, quantity);
            else inventory.RequestCraft(shown, selected.CraftIndices[selectedLevelIndex], quantity);
        }

        // ---------- Every frame ----------

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

            var progress = inventory.GetComponent<CharacterProgress>();
            int level = progress != null ? progress.Level : 1;
            goldText.text = $"{inventory.Gold:N0} gold";
            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].gameObject.SetActive(entries[i].Count > 0);
                ((Image)tabs[i].targetGraphic).color = (int)category == i ? ActiveTabColor : TabColor;
            }

            UpdateList(inventory, level);
            UpdateDetails(inventory, level, progress);
        }

        private void UpdateList(Inventory inventory, int level)
        {
            List<Entry> list = entries[(int)category];
            int pages = Mathf.Max(1, (list.Count + RowsPerPage - 1) / RowsPerPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            pageText.text = pages > 1 ? $"{page + 1} / {pages}" : "";
            emptyText.text = list.Count == 0 ? "Nothing to craft here." : "";
            for (int i = 0; i < rows.Count; i++)
            {
                int index = page * RowsPerPage + i;
                Row row = rows[i];
                bool visible = index < list.Count;
                row.Button.gameObject.SetActive(visible);
                if (!visible) continue;

                Entry entry = list[index];
                row.Background.color = entry == selected ? SelectedColor : RowColor;
                ItemDefinition item = entry.Shown;
                row.Name.text = entry.Name;
                row.Name.color = ItemDefinition.RarityColor(item.rarity);
                row.Details.text = RowDetails(entry, inventory, level);
            }
        }

        private string RowDetails(Entry entry, Inventory inventory, int level)
        {
            if (entry.RecipeIndex >= 0)
            {
                bool can = CanMake(inventory, shown.Definition.recipes[entry.RecipeIndex].ingredients, 1);
                return can ? "<color=#9be28f>Materials ready</color>" : "Missing materials";
            }
            ItemDefinition[] crafts = shown.Definition.crafts;
            int lowest = crafts[entry.CraftIndices[0]].itemLevel;
            int highest = crafts[entry.CraftIndices[entry.CraftIndices.Count - 1]].itemLevel;
            string levels = lowest == highest ? $"Level {lowest}" : $"Levels {lowest}-{highest}";
            if (lowest > level) return $"<color=#ff6655>{levels}</color>";
            bool ready = false;
            foreach (int craft in entry.CraftIndices)
                if (crafts[craft].itemLevel <= level && inventory.CountOf(crafts[craft].craftMaterial) >= crafts[craft].craftCount) ready = true;
            return ready ? $"{levels}   <color=#9be28f>Materials ready</color>" : levels;
        }

        private static bool CanMake(Inventory inventory, RecipeIngredient[] ingredients, int times)
        {
            foreach (RecipeIngredient ingredient in ingredients)
                if (ingredient.item != null && inventory.CountOf(ingredient.item) < ingredient.count * times) return false;
            return true;
        }

        private void UpdateDetails(Inventory inventory, int level, CharacterProgress progress)
        {
            ItemDefinition item = SelectedItem();
            details.SetActive(item != null);
            if (item == null) return;

            selectedName.text = item.displayName;
            selectedName.color = ItemDefinition.RarityColor(item.rarity);
            selectedDetails.text = VendorWindow.Summary(item, progress);
            bool gear = selected.RecipeIndex < 0;
            levelPicker.SetActive(gear);
            if (gear)
            {
                levelText.text = selected.CraftIndices.Count > 1
                    ? $"Level {item.itemLevel}  <size=12>({selectedLevelIndex + 1} of {selected.CraftIndices.Count})</size>"
                    : $"Level {item.itemLevel}";
                levelText.color = item.itemLevel <= level ? Enough : Missing;
            }

            SelectedMaterials(materials);
            bool enough = true;
            for (int i = 0; i < materialNames.Count; i++)
            {
                bool shownLine = i < materials.Count;
                materialNames[i].transform.parent.gameObject.SetActive(shownLine);
                if (!shownLine) continue;
                RecipeIngredient ingredient = materials[i];
                int have = inventory.CountOf(ingredient.item);
                int need = ingredient.count * quantity;
                materialNames[i].text = ingredient.item.displayName;
                materialNames[i].color = ItemDefinition.RarityColor(ingredient.item.rarity);
                materialCounts[i].text = $"{have} / {need}";
                materialCounts[i].color = have >= need ? Enough : Missing;
                if (have < need) enough = false;
            }

            quantityText.text = quantity.ToString();
            int fee = Inventory.CraftFee(item) * quantity;
            feeText.text = $"<color=#ffd040>{fee:N0} gold</color>     Craft";
            bool levelOk = !gear || item.itemLevel <= level;
            bool goldOk = inventory.Gold >= fee;
            statusText.text = !levelOk ? $"Requires level {item.itemLevel}"
                : !enough ? "Insufficient crafting materials"
                : !goldOk ? "Not enough gold"
                : "";
            craftButton.interactable = levelOk && enough && goldOk;
        }
    }
}
