using System.Collections.Generic;
using RPG.Controls;
using RPG.Items;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The small window of a loot bag the local player opened (by clicking the sparkles a monster left), like
    /// Source's loot window: every item in it as a square with its tooltip; click one to take it, or take all.
    /// Closes on Esc, the X, walking away, or when the bag is empty. Builds its own UI; place it on a
    /// RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LootWindow : MonoBehaviour
    {
        private const int Columns = 4;
        private const float SlotSize = 64f;
        private const float Spacing = 6f;
        private const float Top = 46f;
        private const float WalkAwayDistance = 1.5f;

        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.95f);
        private static readonly Color BorderColor = new Color(0.45f, 0.34f, 0.18f, 0.95f);
        private static readonly Color EmptySlot = new Color(0.16f, 0.14f, 0.12f, 0.95f);

        private GameObject window;
        private RectTransform windowRect;
        private RectTransform grid;
        private Button takeAll;
        private RectTransform tooltip;
        private Text tooltipText;
        private readonly List<ItemSlotView> slots = new List<ItemSlotView>();
        private ItemSlotView hovered;

        private ulong lootId;
        private ItemStack[] contents = new ItemStack[0];

        private void Awake()
        {
            Image panel = UIFactory.CreateImage("Loot", transform, PanelColor);
            window = panel.gameObject;
            panel.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            windowRect = panel.rectTransform;
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0f, 0.5f);
            windowRect.anchoredPosition = new Vector2(80f, 40f);

            float width = Columns * (SlotSize + Spacing) - Spacing + 24f;
            Text title = UIFactory.CreateText("Title", windowRect, 19, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(width - 60f, 30f));
            title.text = "Loot";
            title.color = new Color(1f, 0.88f, 0.6f);
            title.fontStyle = FontStyle.Bold;

            Button close = UIFactory.CreateButton("Close", windowRect, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 16);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(28f, 28f));
            close.onClick.AddListener(Hide);

            grid = UIFactory.CreateRect("Items", windowRect);
            UIFactory.Place(grid, new Vector2(0f, 1f), new Vector2(12f, -Top), new Vector2(width - 24f, 10f));

            takeAll = UIFactory.CreateButton("Take All", windowRect, new Color(0.25f, 0.35f, 0.18f, 0.95f), "Take all", 16);
            var takeRect = (RectTransform)takeAll.transform;
            takeRect.anchorMin = takeRect.anchorMax = takeRect.pivot = new Vector2(0.5f, 0f);
            takeRect.anchoredPosition = new Vector2(0f, 10f);
            takeRect.sizeDelta = new Vector2(width - 24f, 32f);
            takeAll.onClick.AddListener(TakeAll);
            windowRect.sizeDelta = new Vector2(width, 120f);

            Image background = UIFactory.CreateImage("Tooltip", transform, new Color(0f, 0f, 0f, 0.92f));
            background.raycastTarget = false;
            tooltip = background.rectTransform;
            tooltip.anchorMin = tooltip.anchorMax = Vector2.zero;
            tooltip.pivot = new Vector2(0f, 0f);
            tooltipText = UIFactory.CreateText("Text", tooltip, 15, TextAnchor.UpperLeft);
            UIFactory.Stretch(tooltipText.rectTransform);
            tooltipText.rectTransform.offsetMin = new Vector2(8f, 6f);
            tooltipText.rectTransform.offsetMax = new Vector2(-8f, -6f);
            tooltip.gameObject.SetActive(false);

            window.SetActive(false);
            Inventory.LootContentsReceived += OnContents;
        }

        private void OnDestroy()
        {
            Inventory.LootContentsReceived -= OnContents;
        }

        private void OnContents(ulong id, ItemStack[] stacks)
        {
            // An answer about another bag than the open one (or none open) opens that one.
            if (stacks.Length == 0)
            {
                if (id == lootId) Hide();
                return;
            }
            lootId = id;
            contents = stacks;
            Layout();
            window.SetActive(true);
        }

        private void Layout()
        {
            while (slots.Count < contents.Length) slots.Add(CreateSlot(slots.Count));
            for (int i = 0; i < slots.Count; i++) slots[i].gameObject.SetActive(i < contents.Length);
            int rows = Mathf.Max(1, (contents.Length + Columns - 1) / Columns);
            windowRect.sizeDelta = new Vector2(windowRect.sizeDelta.x, Top + rows * (SlotSize + Spacing) + 52f);
        }

        private ItemSlotView CreateSlot(int index)
        {
            Button button = UIFactory.CreateButton("Slot", grid, EmptySlot, null, 0);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);
            rect.anchoredPosition = new Vector2(index % Columns * (SlotSize + Spacing), -(index / Columns) * (SlotSize + Spacing));
            button.onClick.AddListener(() => Take(index));

            ItemSlotView view = button.gameObject.AddComponent<ItemSlotView>();
            view.Background = (Image)button.targetGraphic;
            view.Border = button.gameObject.AddComponent<Outline>();
            view.Border.effectDistance = new Vector2(2f, -2f);
            view.Label = UIFactory.CreateText("Name", rect, 11, TextAnchor.MiddleCenter);
            UIFactory.Stretch(view.Label.rectTransform);
            view.Label.rectTransform.offsetMin = new Vector2(3f, 3f);
            view.Label.rectTransform.offsetMax = new Vector2(-3f, -3f);
            view.PointerEntered += v => hovered = v;
            view.PointerExited += v => { if (hovered == v) hovered = null; };
            return view;
        }

        private LootBag Bag() => window.activeSelf ? LootBag.Find(lootId) : null;

        private void Take(int index)
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            LootBag bag = Bag();
            if (inventory == null || bag == null || index >= contents.Length) return;
            inventory.RequestTakeLoot(bag, index, contents[index].ItemId);
        }

        private void TakeAll()
        {
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            LootBag bag = Bag();
            if (inventory != null && bag != null) inventory.RequestPickUp(bag);
        }

        private void Hide()
        {
            window.SetActive(false);
            tooltip.gameObject.SetActive(false);
            hovered = null;
            lootId = 0;
        }

        private void Update()
        {
            if (!window.activeSelf) return;
            Inventory inventory = UIFactory.LocalPlayer<Inventory>();
            LootBag bag = Bag();
            PlayerInputReader input = inventory != null ? inventory.GetComponent<PlayerInputReader>() : null;
            bool gone = inventory == null || bag == null
                        || Vector2.Distance(inventory.transform.position, bag.transform.position) > inventory.PickupRange + WalkAwayDistance;
            if (gone || (input != null && input.CancelPressed))
            {
                Hide();
                return;
            }

            for (int i = 0; i < contents.Length; i++)
            {
                ItemSlotView view = slots[i];
                ItemDefinition item = inventory.Definition(contents[i]);
                if (item == null)
                {
                    view.Background.color = EmptySlot;
                    view.Label.text = "";
                    continue;
                }
                Color rarity = ItemDefinition.RarityColor(inventory.Quality(contents[i]));
                Color background = item.iconColor * 0.55f;
                background.a = 0.95f;
                view.Background.color = background;
                view.Border.effectColor = rarity;
                view.Label.text = contents[i].Count > 1 ? $"{inventory.DisplayName(contents[i])} x{contents[i].Count}" : inventory.DisplayName(contents[i]);
                view.Label.color = rarity;
            }
            UpdateTooltip(inventory);
        }

        private void UpdateTooltip(Inventory inventory)
        {
            int index = hovered != null ? slots.IndexOf(hovered) : -1;
            ItemDefinition item = index >= 0 && index < contents.Length ? inventory.Definition(contents[index]) : null;
            tooltip.gameObject.SetActive(item != null);
            if (item == null) return;

            string className = VendorWindow.ClassName(item, inventory.GetComponent<CharacterProgress>());
            tooltipText.text = inventory.Describe(contents[index], className)
                .Replace("Click to equip", "").Replace("Click to use", "").Replace("Click, then click the item to set it into", "").TrimEnd()
                + "\nClick to take";
            tooltipText.color = ItemDefinition.RarityColor(inventory.Quality(contents[index]));
            tooltip.sizeDelta = new Vector2(260f, tooltipText.preferredHeight + 14f);
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : (Vector2)hovered.transform.position;
            tooltip.anchoredPosition = pointer / scale + new Vector2(14f, 10f);
            tooltip.SetAsLastSibling();
        }
    }
}
