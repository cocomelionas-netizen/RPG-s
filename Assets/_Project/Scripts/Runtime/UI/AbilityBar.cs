using RPG.Combat;
using RPG.Controls;
using RPG.Items;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's action bar, like the source game's: 12 slots on keys 1 to 9, 0, - and =. Each holds Attack, a
    /// spell or a usable item (potions, with how many are left), with a cooldown overlay and countdown; a cast bar shows
    /// while casting, and a line shows messages such as "No enemy target". Clicking a slot does the same as its key.
    /// Spells are dragged onto it from the spellbook, items from the bag; slots are dragged to move them, and off the bar
    /// to empty them. (Picking a spell in the spellbook and clicking a slot also puts it there.)
    /// Builds its own UI; place it on an empty RectTransform in the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AbilityBar : MonoBehaviour
    {
        [SerializeField, Min(16f)] private float slotSize = 58f;
        [SerializeField, Min(0f)] private float spacing = 5f;
        [SerializeField, Min(0.1f)] private float messageSeconds = 1.6f;

        private class Slot
        {
            public GameObject Root;
            public Image Background;
            /// <summary>The spell's or item's picture (with one, only the mana cost or count is written).</summary>
            public Image Icon;
            public RectTransform Cooldown;
            public Text Key;
            public Text Name;
            public Text Countdown;
            public Outline Border;
            // What the texts show now, so they are rewritten only when it changes (the bar is always on screen).
            public int ShownKind = -1;
            public int ShownId;
            public int ShownNumber;
            public bool ShownFlag;
            public int ShownCountdown;
        }

        /// <summary>True (and remembers it) when the slot's name text must be rewritten for this content.</summary>
        private static bool NameChanged(Slot slot, int kind, int id, int number, bool flag)
        {
            if (slot.ShownKind == kind && slot.ShownId == id && slot.ShownNumber == number && slot.ShownFlag == flag) return false;
            slot.ShownKind = kind;
            slot.ShownId = id;
            slot.ShownNumber = number;
            slot.ShownFlag = flag;
            return true;
        }

        /// <summary>Shows whole seconds (positive) or minutes (negative, "5m"); 0 shows nothing.</summary>
        private static void SetCountdown(Slot slot, int value)
        {
            if (slot.ShownCountdown == value) return;
            slot.ShownCountdown = value;
            slot.Countdown.text = value > 0 ? value.ToString() : value < 0 ? $"{-value}m" : "";
        }

        private Slot[] slots;
        private Inventory inventory;
        private Spellbook spellbook;
        private Text message;
        private GameObject castBar;
        private RectTransform castFill;
        private Text castText;
        private float messageTimer;
        private AbilityCaster caster;
        private PlayerTargeting targeting;

        private static readonly Color AttackColor = new Color(0.55f, 0.18f, 0.12f, 0.92f);
        private static readonly Color ActiveOutline = new Color(1f, 0.85f, 0.3f);
        private static readonly Color IdleOutline = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color EmptyColor = new Color(0.08f, 0.07f, 0.06f, 0.6f);
        private static readonly Color DropColor = new Color(0.25f, 0.18f, 0.35f, 0.9f);

        private int SlotCount => Spellbook.BarSlots;

        private void Awake()
        {
            int count = SlotCount;
            float width = count * slotSize + (count - 1) * spacing;

            // A dark frame behind all buttons.
            Image frame = UIFactory.CreateImage("Frame", transform, new Color(0.06f, 0.05f, 0.04f, 0.88f));
            frame.raycastTarget = false;
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0f);
            frame.rectTransform.sizeDelta = new Vector2(width + 20f, slotSize + 20f);
            frame.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            frame.gameObject.AddComponent<Outline>().effectColor = new Color(0.45f, 0.34f, 0.18f, 0.95f);

            slots = new Slot[count];
            for (int i = 0; i < count; i++)
                slots[i] = CreateSlot(i, PlayerInputReader.AbilityKeys[i], -width * 0.5f + i * (slotSize + spacing));

            Image castBack = UIFactory.CreateImage("Cast Bar", transform, new Color(0.05f, 0.04f, 0.03f, 0.9f));
            castBack.raycastTarget = false;
            castBack.rectTransform.anchorMin = castBack.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            castBack.rectTransform.pivot = new Vector2(0.5f, 0f);
            castBack.rectTransform.sizeDelta = new Vector2(300f, 20f);
            castBack.rectTransform.anchoredPosition = new Vector2(0f, slotSize + 50f);
            castBack.gameObject.AddComponent<Outline>().effectColor = new Color(0.45f, 0.34f, 0.18f, 0.95f);
            castBar = castBack.gameObject;
            Image fill = UIFactory.CreateImage("Fill", castBack.rectTransform, new Color(0.85f, 0.7f, 0.25f, 0.95f));
            fill.raycastTarget = false;
            castFill = fill.rectTransform;
            castFill.anchorMin = Vector2.zero;
            castFill.anchorMax = new Vector2(0f, 1f);
            castFill.offsetMin = castFill.offsetMax = Vector2.zero;
            castText = UIFactory.CreateText("Name", castBack.rectTransform, 13, TextAnchor.MiddleCenter);
            UIFactory.Stretch(castText.rectTransform);
            castBar.SetActive(false);

            message = UIFactory.CreateText("Message", transform, 22, TextAnchor.MiddleCenter);
            message.rectTransform.anchorMin = message.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            message.rectTransform.pivot = new Vector2(0.5f, 0f);
            message.rectTransform.sizeDelta = new Vector2(600f, 30f);
            message.rectTransform.anchoredPosition = new Vector2(0f, slotSize + 16f);
            message.color = new Color(1f, 0.45f, 0.35f);
            message.text = "";
        }

        private Slot CreateSlot(int index, string key, float x)
        {
            Button button = UIFactory.CreateButton($"Slot {index + 1}", transform, EmptyColor, null, 0);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = new Vector2(x, 0f);
            int slotIndex = index;
            button.onClick.AddListener(() => OnSlotClicked(slotIndex));

            // Dragged to move it to another slot, or off the bar to empty it.
            var source = button.gameObject.AddComponent<DragSource>();
            source.Payload = () => SlotPayload(slotIndex);
            source.Ended = dropped =>
            {
                if (!dropped && spellbook != null && spellbook.EntryInSlot(slotIndex) != -1) spellbook.RequestAssign(slotIndex, -1);
            };
            var target = button.gameObject.AddComponent<DropTarget>();
            target.Accepts = payload => payload.Kind != DragKind.Item || IsUsable(payload.Value);
            target.Dropped = payload => OnDropped(slotIndex, payload);

            var slot = new Slot { Root = button.gameObject, Background = (Image)button.targetGraphic };

            slot.Icon = UIFactory.CreateIcon(rect, 3f);
            Image cooldown = UIFactory.CreateImage("Cooldown", rect, new Color(0f, 0f, 0f, 0.65f));
            cooldown.raycastTarget = false;
            slot.Cooldown = cooldown.rectTransform;
            slot.Cooldown.anchorMin = Vector2.zero;
            slot.Cooldown.anchorMax = new Vector2(1f, 0f);
            slot.Cooldown.offsetMin = slot.Cooldown.offsetMax = Vector2.zero;

            slot.Key = UIFactory.CreateText("Key", rect, 15, TextAnchor.UpperLeft);
            UIFactory.Stretch(slot.Key.rectTransform);
            slot.Key.rectTransform.offsetMin = new Vector2(5f, 0f);
            slot.Key.rectTransform.offsetMax = new Vector2(0f, -3f);
            slot.Key.text = key;
            slot.Border = button.gameObject.AddComponent<Outline>();
            slot.Border.effectDistance = new Vector2(3f, -3f);

            slot.Name = UIFactory.CreateText("Name", rect, 11, TextAnchor.LowerCenter);
            UIFactory.Stretch(slot.Name.rectTransform);
            slot.Name.rectTransform.offsetMin = new Vector2(3f, 4f);
            slot.Name.rectTransform.offsetMax = new Vector2(-3f, 0f);

            slot.Countdown = UIFactory.CreateText("Countdown", rect, 24, TextAnchor.MiddleCenter);
            UIFactory.Stretch(slot.Countdown.rectTransform);
            return slot;
        }

        private bool IsUsable(int itemId)
        {
            ItemDefinition item = inventory != null && inventory.Catalog != null ? inventory.Catalog.Get(itemId) : null;
            return item != null && item.use != ItemUse.None;
        }

        private DragPayload? SlotPayload(int slot)
        {
            if (spellbook == null || spellbook.EntryInSlot(slot) == -1) return null;
            return new DragPayload
            {
                Kind = DragKind.BarSlot,
                Value = slot,
                Label = slots[slot].Name.text,
                Color = slots[slot].Background.color,
                Icon = slots[slot].Icon.enabled ? slots[slot].Icon.sprite : null,
                IconTint = slots[slot].Icon.color,
            };
        }

        private void OnDropped(int slot, DragPayload payload)
        {
            if (spellbook == null) return;
            switch (payload.Kind)
            {
                case DragKind.Spell:
                    spellbook.RequestAssign(slot, payload.Value);
                    break;
                case DragKind.Item:
                    spellbook.RequestAssignItem(slot, payload.Value);
                    break;
                case DragKind.BarSlot:
                    if (payload.Value != slot) spellbook.RequestMove(payload.Value, slot);
                    break;
            }
        }

        private void OnSlotClicked(int slot)
        {
            if (caster == null) return;
            if (SpellbookWindow.PendingSpell != -1 && spellbook != null)
            {
                // A spell picked in the spellbook goes into this slot.
                spellbook.RequestAssign(slot, SpellbookWindow.PendingSpell);
                SpellbookWindow.ClearPending();
                return;
            }
            caster.UseSlot(slot);
        }

        private void OnCastFailed(int slot, CastFailure failure)
        {
            ShowMessage(AbilityCaster.Describe(failure, caster != null ? caster.GetAbility(slot) : null));
        }

        private void ShowMessage(string text)
        {
            message.text = text;
            messageTimer = messageSeconds;
        }

        private void Update()
        {
            AbilityCaster local = UIFactory.LocalPlayer<AbilityCaster>();
            if (local != caster)
            {
                if (caster != null) caster.CastFailed -= OnCastFailed;
                if (targeting != null) targeting.AttackFailed -= ShowMessage;
                caster = local;
                targeting = caster != null ? caster.GetComponent<PlayerTargeting>() : null;
                spellbook = caster != null ? caster.GetComponent<Spellbook>() : null;
                inventory = caster != null ? caster.GetComponent<Inventory>() : null;
                if (caster != null) caster.CastFailed += OnCastFailed;
                if (targeting != null) targeting.AttackFailed += ShowMessage;
            }

            bool dropping = DragSource.IsDragging || SpellbookWindow.PendingSpell != -1;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                slot.Root.SetActive(caster != null);
                if (caster == null) continue;

                if (spellbook != null && spellbook.IsAttackSlot(i)) ShowAttack(slot);
                else if (spellbook != null && spellbook.ItemInSlot(i) > 0) ShowItem(slot, spellbook.ItemInSlot(i));
                else if (caster.GetAbility(i) != null) ShowSpell(slot, i, caster.GetAbility(i));
                else ShowEmpty(slot, dropping);
            }

            AbilityDefinition casting = caster != null ? caster.CastingAbility : null;
            bool returning = inventory != null && inventory.IsReturning;
            castBar.SetActive(casting != null || returning);
            if (casting != null)
            {
                castFill.anchorMax = new Vector2(caster.CastProgress, 1f);
                castText.text = casting.displayName;
            }
            else if (returning)
            {
                castFill.anchorMax = new Vector2(inventory.ReturnProgress, 1f);
                castText.text = "Returning";
            }

            messageTimer -= Time.deltaTime;
            Color color = message.color;
            color.a = Mathf.Clamp01(messageTimer / 0.4f);
            message.color = color;
        }

        private void ShowAttack(Slot slot)
        {
            slot.Background.color = AttackColor;
            bool pictured = UIFactory.SetIcon(slot.Icon, IconSet.Attack, Color.white);
            if (NameChanged(slot, 0, 0, 0, pictured)) slot.Name.text = pictured ? "" : "Attack";
            slot.Cooldown.anchorMax = new Vector2(1f, 0f);
            SetCountdown(slot, 0);
            SetHighlighted(slot, targeting != null && targeting.IsAttacking);
        }

        private void ShowSpell(Slot slot, int index, AbilityDefinition ability)
        {
            Color tint = ability.effectColor * 0.45f;
            tint.a = 0.92f;
            slot.Background.color = tint;
            bool enoughMana = caster.HasManaFor(ability);
            // Not enough mana: the picture dims.
            bool pictured = UIFactory.SetIcon(slot.Icon, ability.icon, enoughMana ? Color.white : new Color(0.45f, 0.45f, 0.6f));
            int cost = caster.ManaCost(ability);
            if (NameChanged(slot, pictured ? 4 : 1, ability.GetHashCode(), cost, enoughMana))
            {
                string costText = cost > 0 ? $"<color={(enoughMana ? "#8fb3ff" : "#ff6655")}>{cost}</color>" : "";
                slot.Name.text = pictured ? costText
                    : cost > 0 ? $"{ability.displayName}\n<color={(enoughMana ? "#8fb3ff" : "#ff6655")}>{cost} mana</color>" : ability.displayName;
            }
            SetHighlighted(slot, caster.PendingSlot == index);

            float remaining = caster.CooldownRemaining(index);
            slot.Cooldown.anchorMax = new Vector2(1f, caster.CooldownFraction(index));
            SetCountdown(slot, remaining >= 1f ? Mathf.CeilToInt(remaining) : 0);
        }

        private void ShowItem(Slot slot, int itemId)
        {
            ItemDefinition item = inventory != null && inventory.Catalog != null ? inventory.Catalog.Get(itemId) : null;
            if (item == null)
            {
                ShowEmpty(slot, false);
                return;
            }
            int count = inventory.CountForBarItem(item);
            Color tint = count > 0 ? item.iconColor * 0.7f : item.iconColor * 0.3f;
            tint.a = 0.92f;
            // None left: the picture dims.
            bool pictured = UIFactory.SetIcon(slot.Icon, item.icon, count > 0 ? item.iconTint : item.iconTint * 0.35f);
            slot.Background.color = pictured ? EmptyColor : tint;
            if (NameChanged(slot, 2, itemId, count, pictured)) slot.Name.text = pictured ? count.ToString() : $"{item.displayName}\nx{count}";
            bool potion = item.use == ItemUse.Heal || item.use == ItemUse.RestoreMana;
            bool scroll = item.use == ItemUse.Return;
            bool food = item.use == ItemUse.Food;
            // Potions share one cooldown; Scrolls of Returning and food have their own.
            float remaining = potion ? inventory.PotionCooldownRemaining : scroll ? inventory.ReturnCooldownRemaining
                : food ? inventory.FoodCooldownRemaining : 0f;
            float fraction = potion ? inventory.PotionCooldownFraction : scroll ? inventory.ReturnCooldownFraction(item.useCooldown)
                : food && item.useCooldown > 0f ? Mathf.Clamp01(remaining / item.useCooldown) : 0f;
            slot.Cooldown.anchorMax = new Vector2(1f, count > 0 ? fraction : 0f);
            SetCountdown(slot, count > 0 && remaining >= 0.05f ? CountdownValue(remaining) : 0);
            SetHighlighted(slot, false);
        }

        /// <summary>45 seconds, or -5 for "5m" on long cooldowns (see <see cref="SetCountdown"/>).</summary>
        private static int CountdownValue(float seconds) =>
            seconds >= 60f ? -Mathf.CeilToInt(seconds / 60f) : Mathf.CeilToInt(seconds);

        private void ShowEmpty(Slot slot, bool dropping)
        {
            // Empty slots stay visible (dim) so things can be put there.
            slot.Background.color = dropping ? DropColor : EmptyColor;
            UIFactory.SetIcon(slot.Icon, null, Color.white);
            if (NameChanged(slot, 3, 0, 0, false)) slot.Name.text = "";
            SetCountdown(slot, 0);
            slot.Cooldown.anchorMax = new Vector2(1f, 0f);
            SetHighlighted(slot, false);
        }

        private static void SetHighlighted(Slot slot, bool highlighted)
        {
            Color wanted = highlighted ? ActiveOutline : IdleOutline;
            if (slot.Border != null && slot.Border.effectColor != wanted) slot.Border.effectColor = wanted;
        }

        private void OnDestroy()
        {
            if (caster != null) caster.CastFailed -= OnCastFailed;
            if (targeting != null) targeting.AttackFailed -= ShowMessage;
        }
    }
}
