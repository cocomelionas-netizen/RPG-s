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
    /// The local player's inventory (I or B, or the Bag button): worn equipment, the bag and the resulting stats.
    /// Click a bag item to equip or use it, click worn equipment to take it off; hover for details.
    /// A gem is set by clicking it and then the item (worn or in the bag) to put it into.
    /// Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(-20)]
    public class InventoryWindow : MonoBehaviour
    {
        [SerializeField, Min(20f)] private float slotSize = 62f;
        [SerializeField, Min(0f)] private float spacing = 6f;
        [SerializeField, Min(1)] private int columns = 5;

        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.94f);
        private static readonly Color EmptySlot = new Color(0.16f, 0.14f, 0.12f, 0.95f);

        private GameObject window;
        private GameObject toggleButton;
        private ItemTooltip itemTooltip;
        private Text statsText;
        private readonly List<ItemSlotView> bagSlots = new List<ItemSlotView>();
        private readonly List<ItemSlotView> equipmentSlots = new List<ItemSlotView>();
        private ItemSlotView hovered;
        private Inventory inventory;
        // Bag slot of the gem waiting for the item to go into (-1 when none).
        private int pendingGem = -1;
        private static InventoryWindow instance;

        /// <summary>The bag slot of a gem waiting for the item to go into (-1 when none); the character sheet uses it for worn gear.</summary>
        public static int PendingGem => instance != null ? instance.pendingGem : -1;

        public static void ClearPendingGem()
        {
            if (instance != null) instance.pendingGem = -1;
        }
        private static readonly Color PendingGemBorder = new Color(1f, 1f, 0.4f);

        private void Awake()
        {
            Button toggle = UIFactory.CreateButton("Bag Button", transform, new Color(0.3f, 0.22f, 0.12f, 0.95f), "Bag (I)", 18);
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(1f, 0f);
            toggleRect.sizeDelta = new Vector2(110f, 44f);
            toggleRect.anchoredPosition = new Vector2(-20f, 20f);
            toggle.onClick.AddListener(Toggle);
            toggleButton = toggle.gameObject;

            instance = this;
            BuildWindow();
            BuildTooltip();
            window.SetActive(false);
            VendorWindow.Opened += OnVendorOpened;
            TransmuteWindow.Opened += OnVendorOpened;
        }

        private void OnDestroy()
        {
            VendorWindow.Opened -= OnVendorOpened;
            TransmuteWindow.Opened -= OnVendorOpened;
            if (watched != null) watched.Changed -= MarkDirty;
        }

        private void OnVendorOpened()
        {
            // Selling (and putting items into the transmuter) happens by clicking the bag, so show it next to the vendor.
            if (!window.activeSelf) Toggle();
        }

        private void BuildWindow()
        {
            float gridWidth = columns * slotSize + (columns - 1) * spacing;
            Image panel = UIFactory.CreateImage("Inventory", transform, PanelColor);
            window = panel.gameObject;
            window.AddComponent<OpenWindow>();
            UIFactory.AddFrame(panel);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-20f, 30f);

            float y = -12f;
            Text title = AddText(rect, "Inventory (I)", 22, TextAnchor.UpperLeft, new Vector2(14f, y), new Vector2(gridWidth, 28f));
            title.fontStyle = FontStyle.Bold;
            y -= 34f;

            // Worn gear is on the character sheet (C), as in the source game.

            statsText = AddText(rect, "", 14, TextAnchor.UpperLeft, new Vector2(14f, y), new Vector2(gridWidth, 20f));
            y -= 28f;

            AddText(rect, "Bag", 15, TextAnchor.UpperLeft, new Vector2(14f, y), new Vector2(gridWidth, 20f));
            y -= 22f;
            rect.sizeDelta = new Vector2(gridWidth + 28f, -y + 14f);
            // The bag grid is filled once the size of the player's bag is known.
            bagGridTop = y;
            windowRect = rect;
        }

        private float bagGridTop;
        private RectTransform windowRect;

        private void EnsureBagSlots(int count)
        {
            if (bagSlots.Count == count) return;
            foreach (ItemSlotView view in bagSlots) Destroy(view.gameObject);
            bagSlots.Clear();

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                ItemSlotView view = CreateSlot(windowRect, new Vector2(14f + column * (slotSize + spacing), bagGridTop - row * (slotSize + spacing)));
                int index = i;
                view.GetComponent<Button>().onClick.AddListener(() => OnBagClicked(index));
                // Usable items (potions) are dragged onto the action bar.
                var source = view.gameObject.AddComponent<DragSource>();
                source.Payload = () => BagPayload(index);
                bagSlots.Add(view);
            }

            int rows = (count + columns - 1) / columns;
            float height = -bagGridTop + rows * (slotSize + spacing) + 14f;
            windowRect.sizeDelta = new Vector2(windowRect.sizeDelta.x, height);
        }

        private DragPayload? BagPayload(int index)
        {
            if (inventory == null) return null;
            ItemStack stack = inventory.GetBagSlot(index);
            ItemDefinition item = inventory.Definition(stack);
            if (item == null) return null;
            return new DragPayload
            {
                Kind = DragKind.Item,
                Value = stack.ItemId,
                BagIndex = index,
                Label = item.displayName,
                Color = item.iconColor * 0.7f,
                Icon = item.icon,
                IconTint = item.iconTint,
            };
        }

        private ItemSlotView CreateSlot(RectTransform parent, Vector2 topLeft)
        {
            Button button = UIFactory.CreateButton("Slot", parent, EmptySlot, null, 0);
            UIFactory.MakeSlot(button);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = topLeft;

            ItemSlotView view = button.gameObject.AddComponent<ItemSlotView>();
            view.Background = (Image)button.targetGraphic;
            view.Border = button.gameObject.AddComponent<Outline>();
            view.Border.effectDistance = new Vector2(2f, -2f);
            view.Icon = UIFactory.CreateIcon(rect, 4f);

            view.Label = UIFactory.CreateText("Name", rect, 11, TextAnchor.MiddleCenter);
            UIFactory.Stretch(view.Label.rectTransform);
            view.Label.rectTransform.offsetMin = new Vector2(3f, 3f);
            view.Label.rectTransform.offsetMax = new Vector2(-3f, -3f);

            view.Count = UIFactory.CreateText("Count", rect, 14, TextAnchor.LowerRight);
            UIFactory.Stretch(view.Count.rectTransform);
            view.Count.rectTransform.offsetMax = new Vector2(-4f, 0f);

            view.PointerEntered += v => hovered = v;
            view.PointerExited += v => { if (hovered == v) hovered = null; };
            return view;
        }

        private Text AddText(RectTransform parent, string content, int size, TextAnchor alignment, Vector2 topLeft, Vector2 boxSize)
        {
            Text text = UIFactory.CreateText("Text", parent, size, alignment);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = boxSize;
            text.text = content;
            return text;
        }

        private void BuildTooltip()
        {
            itemTooltip = new ItemTooltip(transform);
        }

        private void Toggle()
        {
            window.SetActive(!window.activeSelf);
            if (!window.activeSelf)
            {
                hovered = null;
                pendingGem = -1;
            }
        }

        private void OnBagClicked(int index)
        {
            if (inventory == null) return;
            if (TransmuteWindow.IsOpen)
            {
                pendingGem = -1;
                TransmuteWindow.Add(index);
                return;
            }
            if (VendorWindow.CurrentVendor != null)
            {
                pendingGem = -1;
                inventory.RequestSell(VendorWindow.CurrentVendor, index);
                return;
            }
            if (pendingGem >= 0)
            {
                // Clicking the gem again cancels; anything else is where it should go.
                if (index != pendingGem) inventory.RequestSocketGem(pendingGem, false, index);
                pendingGem = -1;
                return;
            }

            ItemDefinition item = inventory.Definition(inventory.GetBagSlot(index));
            if (item != null && (item.isGem || item.use == ItemUse.Empower))
            {
                // Gems and orbs both wait for the item they go on.
                pendingGem = index;
                MessageLog.Add(item.isGem
                    ? $"Click the item to set [{item.displayName}] into, in the bag or on the character sheet (C) (Esc to cancel)."
                    : $"Click the item to empower with [{item.displayName}], in the bag or on the character sheet (C) (Esc to cancel).",
                    ItemDefinition.RarityColor(item.rarity));
                return;
            }
            inventory.RequestActivate(index);
        }

        private Inventory watched;
        private bool dirty = true;
        // The slot the tooltip text was built for (rebuilt when the hovered slot changes or the window redraws).
        private ItemSlotView tooltipFor;
        private int shownGem = int.MinValue;
        private int shownGold = -1;
        private float nextRefresh;

        private void MarkDirty() => dirty = true;

        private void Update()
        {
            inventory = UIFactory.LocalPlayer<Inventory>();
            bool hasPlayer = inventory != null && inventory.IsSpawned;
            if (toggleButton.activeSelf != hasPlayer) toggleButton.SetActive(hasPlayer);
            if (!hasPlayer)
            {
                if (window.activeSelf) window.SetActive(false);
                itemTooltip.Hide();
                return;
            }

            PlayerInputReader input = inventory.GetComponent<PlayerInputReader>();
            // Esc closes the bag, unless it is only cancelling a gem being set (then no other window closes on it either).
            if (pendingGem >= 0 && input != null && input.enabled && input.CancelPressed)
            {
                pendingGem = -1;
                OpenWindow.HandleEscape();
            }
            else if (input != null && input.enabled && (input.InventoryPressed || OpenWindow.EscapeCloses(input) && window.activeSelf)) Toggle();
            if (!window.activeSelf)
            {
                itemTooltip.Hide();
                return;
            }

            // The chosen gem moved or was used up: stop waiting for a target.
            if (pendingGem >= 0)
            {
                ItemDefinition gem = inventory.Definition(inventory.GetBagSlot(pendingGem));
                if (gem == null || (!gem.isGem && gem.use != ItemUse.Empower)) pendingGem = -1;
            }

            // The slots and stats are redrawn when the bag changed (or the gem being set, or gold), and now and then
            // for buffs, not every frame.
            if (inventory != watched)
            {
                if (watched != null) watched.Changed -= MarkDirty;
                watched = inventory;
                watched.Changed += MarkDirty;
                dirty = true;
            }
            if (dirty || pendingGem != shownGem || inventory.Gold != shownGold || Time.unscaledTime >= nextRefresh)
            {
                dirty = false;
                shownGem = pendingGem;
                shownGold = inventory.Gold;
                nextRefresh = Time.unscaledTime + 0.5f;
                EnsureBagSlots(inventory.BagSize);
                for (int i = 0; i < bagSlots.Count; i++)
                {
                    Show(bagSlots[i], inventory.GetBagSlot(i));
                    if (i == pendingGem) bagSlots[i].Border.effectColor = PendingGemBorder;
                }
                ShowStats();
                tooltipFor = null;
            }
            ShowTooltip();
        }

        private void Show(ItemSlotView view, ItemStack stack)
        {
            ItemDefinition item = inventory.Definition(stack);
            if (item == null)
            {
                view.Show(null, "", Color.white, EmptySlot);
                view.Count.text = "";
                return;
            }

            view.Show(item, inventory.DisplayName(stack), ItemDefinition.RarityColor(inventory.Quality(stack)), EmptySlot);
            if (view.Count != null && bagSlots.Contains(view))
                view.Count.text = stack.Count > 1 ? stack.Count.ToString() : "";
        }

        private void ShowStats()
        {
            var attack = inventory.GetComponent<AutoAttack>();
            var health = inventory.GetComponent<Health>();
            string damage = attack != null ? $"{attack.ScaledMinDamage}-{attack.ScaledMaxDamage}" : "-";
            string maxHealth = health != null ? health.Max.ToString() : "-";
            ItemDefinition gem = pendingGem >= 0 ? inventory.Definition(inventory.GetBagSlot(pendingGem)) : null;
            statsText.text = gem != null
                ? $"<color=#ffff66>Click the item to {(gem.isGem ? "set" : "empower with")} {gem.displayName}{(gem.isGem ? " into" : "")} (Esc to cancel)</color>"
                : $"Damage {damage}   Health {maxHealth}   Armor {inventory.Armor}   Gold {inventory.Gold}";
        }

        private void ShowTooltip()
        {
            if (hovered == null)
            {
                tooltipFor = null;
                itemTooltip.Hide();
                return;
            }
            if (hovered != tooltipFor) DrawTooltip();
            itemTooltip.Follow(GetComponentInParent<Canvas>(), new Vector2(1f, 0f), new Vector2(-12f, 12f));
        }

        private void DrawTooltip()
        {
            tooltipFor = hovered;
            int bagIndex = bagSlots.IndexOf(hovered);
            int equipIndex = equipmentSlots.IndexOf(hovered);
            ItemStack stack = bagIndex >= 0 ? inventory.GetBagSlot(bagIndex)
                : equipIndex >= 0 ? inventory.GetEquippedAt(equipIndex) : ItemStack.Empty;
            ItemDefinition item = inventory.Definition(stack);
            string hint = item == null ? null
                : equipIndex >= 0 ? "Click to take off"
                : TransmuteWindow.IsOpen ? "Click to put into the transmuter"
                : VendorWindow.CurrentVendor != null ? $"Click to sell for {inventory.SellPrice(stack)} gold"
                : ClickHint(item);
            itemTooltip.Show(inventory, stack, hint);
        }

        /// <summary>What clicking an item in the bag does.</summary>
        public static string ClickHint(ItemDefinition item) =>
            item.IsEquippable ? "Click to equip" : item.use != ItemUse.None ? "Click to use" : item.isGem ? "Click, then click the item to set it into" : null;
    }
}
