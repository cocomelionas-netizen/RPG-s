using System;
using System.Collections.Generic;
using System.Text;
using RPG.Controls;
using RPG.Items;
using RPG.Vendors;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The source game's TRANSMUTE window at a transmuter (Reginald): a 4 x 3 grid and Confirm. Items are put in by
    /// dragging them from the bag or clicking them in the bag, and taken out by clicking the grid slot. When what is in the
    /// grid makes a recipe, its result is shown and Confirm makes it (the server checks it again,
    /// <see cref="Inventory.ServerTransmute"/>). "Recipes" lists what the transmuter knows. Closes on Esc, the X button, or
    /// when the player walks away. Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TransmuteWindow : MonoBehaviour
    {
        private const int Columns = 4;
        private const int Rows = 3;
        private const float SlotSize = 78f;
        private const float Spacing = 8f;
        private const float Width = Columns * (SlotSize + Spacing) + 28f;
        private const float WalkAwayDistance = 1.5f;
        private const int RecipesPerPage = 14;

        private static readonly Color PanelColor = new Color(0.07f, 0.065f, 0.09f, 0.97f);
        private static readonly Color SlotColor = new Color(0.14f, 0.13f, 0.17f, 0.95f);
        private static readonly Color ConfirmColor = new Color(0.35f, 0.3f, 0.2f, 0.98f);
        private static readonly Color ReadyColor = new Color(0.55f, 0.9f, 0.5f);
        private static readonly Color Missing = new Color(1f, 0.45f, 0.4f);

        private static TransmuteWindow instance;
        private Vendor requested;
        private Vendor shown;

        private GameObject window;
        private Text title;
        private readonly Image[] slotImages = new Image[Inventory.TransmuteSlots];
        private readonly Text[] slotTexts = new Text[Inventory.TransmuteSlots];
        private readonly Image[] slotIcons = new Image[Inventory.TransmuteSlots];
        // The bag slot in each grid slot (-1: empty) and the item it held then (a changed bag slot falls out).
        private readonly int[] bagSlots = new int[Inventory.TransmuteSlots];
        private readonly int[] itemIds = new int[Inventory.TransmuteSlots];
        private Text resultText;
        private Button confirmButton;
        private GameObject recipePanel;
        private Text recipeText;
        private Text recipePageText;
        private int recipePage;
        // The grid and the recipe list are redrawn when the bag or the grid changed (or the page), not every frame.
        private bool dirty;
        private Inventory watched;
        private readonly List<int> placedSlots = new List<int>();

        /// <summary>Whether the transmute window is open (clicking bag items then puts them in).</summary>
        public static bool IsOpen => instance != null && instance.window.activeSelf;

        /// <summary>Raised when the window opens (the inventory opens next to it).</summary>
        public static event Action Opened;

        public static void Open(Vendor vendor)
        {
            if (instance != null) instance.requested = vendor;
        }

        /// <summary>Puts a bag slot's items into the first empty grid slot (nothing when it is already in or the grid is full).</summary>
        public static void Add(int bagIndex)
        {
            if (IsOpen) instance.Put(bagIndex, -1);
        }

        private void Awake()
        {
            instance = this;
            for (int i = 0; i < bagSlots.Length; i++) bagSlots[i] = -1;

            Image panel = UIFactory.CreateImage("Transmute", transform, PanelColor);
            window = panel.gameObject;
            window.AddComponent<OpenWindow>();
            RectTransform rect = panel.rectTransform;
            float height = 96f + Rows * (SlotSize + Spacing) + 104f;
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(-220f, 30f), new Vector2(Width, height));
            UIFactory.AddFrame(panel);

            title = Label(rect, 24, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(Width - 60f, 60f));
            title.fontStyle = FontStyle.Bold;
            Button close = UIFactory.CreateButton("Close", rect, new Color(0.45f, 0.12f, 0.1f, 0.95f), "X", 18);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(34f, 34f));
            close.onClick.AddListener(Hide);

            for (int i = 0; i < Inventory.TransmuteSlots; i++)
            {
                int index = i;
                Button slot = UIFactory.CreateButton("Slot", rect, SlotColor, null, 0);
                UIFactory.MakeSlot(slot);
                var slotRect = (RectTransform)slot.transform;
                UIFactory.Place(slotRect, new Vector2(0f, 1f),
                    new Vector2(14f + i % Columns * (SlotSize + Spacing), -80f - i / Columns * (SlotSize + Spacing)), new Vector2(SlotSize, SlotSize));
                slot.onClick.AddListener(() => Clear(index));
                slotImages[i] = slot.GetComponent<Image>();
                slotIcons[i] = UIFactory.CreateIcon(slot.transform, 8f);
                slotTexts[i] = UIFactory.CreateText("Item", slot.transform, 12, TextAnchor.MiddleCenter);
                UIFactory.Stretch(slotTexts[i].rectTransform);
                var drop = slot.gameObject.AddComponent<DropTarget>();
                drop.Accepts = payload => payload.Kind == DragKind.Item && payload.BagIndex >= 0;
                drop.Dropped = payload => Put(payload.BagIndex, index);
            }

            float below = -80f - Rows * (SlotSize + Spacing) - 4f;
            resultText = Label(rect, 16, TextAnchor.UpperLeft, new Vector2(14f, below), new Vector2(Width - 28f, 44f));
            confirmButton = UIFactory.CreateButton("Confirm", rect, ConfirmColor, "Confirm", 18);
            UIFactory.Place((RectTransform)confirmButton.transform, new Vector2(0f, 1f), new Vector2(14f, below - 48f), new Vector2(Width - 150f, 42f));
            confirmButton.onClick.AddListener(Confirm);
            Button recipes = UIFactory.CreateButton("Recipes", rect, SlotColor, "Recipes", 15);
            UIFactory.Place((RectTransform)recipes.transform, new Vector2(1f, 1f), new Vector2(-14f, below - 48f), new Vector2(110f, 42f));
            recipes.onClick.AddListener(() => { recipePanel.SetActive(!recipePanel.activeSelf); dirty = true; });

            // The recipe list, beside the grid.
            Image list = UIFactory.CreateImage("Recipe List", rect, PanelColor);
            recipePanel = list.gameObject;
            UIFactory.Place(list.rectTransform, new Vector2(1f, 1f), new Vector2(360f + 8f, 0f), new Vector2(360f, height));
            list.rectTransform.pivot = new Vector2(1f, 1f);
            list.gameObject.AddComponent<Outline>().effectColor = new Color(0.45f, 0.36f, 0.6f, 0.9f);
            recipeText = Label(list.rectTransform, 13, TextAnchor.UpperLeft, new Vector2(12f, -12f), new Vector2(336f, height - 70f));
            Button previous = UIFactory.CreateButton("Previous", list.rectTransform, SlotColor, "<", 18);
            UIFactory.Place((RectTransform)previous.transform, new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(60f, 34f));
            previous.onClick.AddListener(() => { recipePage = Mathf.Max(0, recipePage - 1); dirty = true; });
            Button next = UIFactory.CreateButton("Next", list.rectTransform, SlotColor, ">", 18);
            UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(60f, 34f));
            next.onClick.AddListener(() => { recipePage++; dirty = true; });
            recipePageText = Label(list.rectTransform, 14, TextAnchor.MiddleCenter, new Vector2(80f, -height + 46f), new Vector2(200f, 34f));
            recipePanel.SetActive(false);

            window.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (watched != null) watched.Changed -= MarkDirty;
        }

        private void MarkDirty() => dirty = true;

        private static Text Label(RectTransform parent, int size, TextAnchor alignment, Vector2 topLeft, Vector2 boxSize)
        {
            Text text = UIFactory.CreateText("Text", parent, size, alignment);
            UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), topLeft, boxSize);
            return text;
        }

        private void Show(Vendor vendor)
        {
            shown = vendor;
            string subtitle = vendor.Definition != null && !string.IsNullOrEmpty(vendor.Definition.title) ? vendor.Definition.title : "Transmutation";
            title.text = $"TRANSMUTE\n<size=14>{vendor.DisplayName}, {subtitle}: put items in, then Confirm.</size>";
            for (int i = 0; i < bagSlots.Length; i++) bagSlots[i] = -1;
            recipePage = 0;
            dirty = true;
            window.SetActive(true);
            Opened?.Invoke();
        }

        private void Hide()
        {
            window.SetActive(false);
            shown = null;
        }

        /// <summary>Puts a bag slot into a grid slot (-1: the first empty one).</summary>
        private void Put(int bagIndex, int gridIndex)
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory == null || bagIndex < 0 || bagIndex >= inventory.BagSize) return;
            ItemStack stack = inventory.GetBagSlot(bagIndex);
            if (stack.IsEmpty) return;
            if (Array.IndexOf(bagSlots, bagIndex) >= 0) return;
            if (gridIndex < 0) gridIndex = Array.IndexOf(bagSlots, -1);
            if (gridIndex < 0) return;
            bagSlots[gridIndex] = bagIndex;
            itemIds[gridIndex] = stack.ItemId;
            dirty = true;
        }

        private void Clear(int gridIndex)
        {
            bagSlots[gridIndex] = -1;
            dirty = true;
        }

        private List<int> PlacedSlots()
        {
            placedSlots.Clear();
            foreach (int slot in bagSlots)
                if (slot >= 0) placedSlots.Add(slot);
            return placedSlots;
        }

        private void Confirm()
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            if (inventory == null || shown == null) return;
            inventory.RequestTransmute(shown, PlacedSlots().ToArray());
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

            if (inventory != watched)
            {
                if (watched != null) watched.Changed -= MarkDirty;
                watched = inventory;
                watched.Changed += MarkDirty;
                dirty = true;
            }
            if (!dirty) return;
            dirty = false;

            for (int i = 0; i < bagSlots.Length; i++)
            {
                ItemStack stack = bagSlots[i] >= 0 ? inventory.GetBagSlot(bagSlots[i]) : ItemStack.Empty;
                // A slot whose bag slot was used up or now holds something else falls out of the grid.
                if (bagSlots[i] >= 0 && (stack.IsEmpty || stack.ItemId != itemIds[i]))
                {
                    bagSlots[i] = -1;
                    stack = ItemStack.Empty;
                }
                ItemDefinition item = inventory.Definition(stack);
                bool pictured = UIFactory.SetIcon(slotIcons[i], item != null ? item.icon : null, item != null ? item.iconTint : Color.white);
                slotImages[i].color = item != null && !pictured ? Color.Lerp(SlotColor, item.iconColor, 0.55f) : SlotColor;
                // With a picture only the count shows (lower right); without one the name.
                slotTexts[i].alignment = pictured ? TextAnchor.LowerRight : TextAnchor.MiddleCenter;
                slotTexts[i].text = item == null ? "" : pictured ? (stack.Count > 1 ? stack.Count.ToString() : "")
                    : stack.Count > 1 ? $"{item.displayName}\nx{stack.Count}" : item.displayName;
                slotTexts[i].color = item != null ? ItemDefinition.RarityColor(inventory.Quality(stack)) : Color.white;
            }

            Dictionary<ItemDefinition, int> placed = inventory.CountItems(PlacedSlots());
            Recipe? match = Recipe.Match(shown.Definition.recipes, placed);
            if (placed.Count == 0)
            {
                resultText.text = "Drag items here from your bag, or click them in the bag.";
                resultText.color = Color.white;
            }
            else if (match != null)
            {
                Recipe recipe = match.Value;
                resultText.text = $"Makes: {(recipe.resultCount > 1 ? recipe.resultCount + " x " : "")}{recipe.result.displayName}";
                resultText.color = ReadyColor;
            }
            else
            {
                resultText.text = "These items make nothing together.";
                resultText.color = Missing;
            }
            confirmButton.interactable = match != null;
            if (recipePanel.activeSelf) ShowRecipes(shown.Definition.recipes);
        }

        private void ShowRecipes(Recipe[] recipes)
        {
            int pages = Mathf.Max(1, (recipes.Length + RecipesPerPage - 1) / RecipesPerPage);
            recipePage = Mathf.Clamp(recipePage, 0, pages - 1);
            var text = new StringBuilder();
            for (int i = recipePage * RecipesPerPage; i < Mathf.Min(recipes.Length, (recipePage + 1) * RecipesPerPage); i++)
            {
                Recipe recipe = recipes[i];
                if (!recipe.IsValid) continue;
                text.Append("<b>").Append(recipe.result.displayName).Append("</b>: ");
                for (int k = 0; k < recipe.ingredients.Length; k++)
                {
                    if (k > 0) text.Append(" + ");
                    text.Append(recipe.ingredients[k].count).Append(' ').Append(recipe.ingredients[k].item.displayName);
                }
                text.AppendLine();
            }
            recipeText.text = text.ToString();
            recipePageText.text = $"Page {recipePage + 1} / {pages}";
        }
    }
}
