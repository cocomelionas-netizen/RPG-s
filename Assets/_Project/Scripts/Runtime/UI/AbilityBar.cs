using RPG.Combat;
using RPG.Controls;
using RPG.Items;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's action bar: Attack (key 1), spells on keys 2, 3, 4, the healing and mana potions (keys 5, 6)
    /// with how many are left, and more spells on keys 7, 8, 9, 0; each with a cooldown overlay and countdown, a cast
    /// bar while casting, and a line for messages such as "No enemy target". Clicking a button does the same as its
    /// key; while a spell is picked in the spellbook, clicking a spell slot puts it there.
    /// Attack and a spell being walked into range are highlighted.
    /// Builds its own UI; place it on an empty RectTransform in the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AbilityBar : MonoBehaviour
    {
        [Tooltip("Buttons in total: Attack plus this many minus one spells.")]
        [SerializeField, Min(1)] private int slotCount = PlayerInputReader.AbilityKeyCount;
        [SerializeField, Min(16f)] private float slotSize = 62f;
        [SerializeField, Min(0f)] private float spacing = 6f;
        [Tooltip("Extra space between the abilities and the potions.")]
        [SerializeField, Min(0f)] private float potionGap = 22f;
        [SerializeField, Min(0.1f)] private float messageSeconds = 1.6f;

        private class Slot
        {
            public GameObject Root;
            public Image Background;
            public RectTransform Cooldown;
            public Text Key;
            public Text Name;
            public Text Countdown;
        }

        private Slot[] slots;
        private Slot[] potions;
        private Inventory inventory;
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
        private static readonly Color EmptyPotion = new Color(0.12f, 0.1f, 0.09f, 0.9f);

        private void Awake()
        {
            int potionCount = PlayerInputReader.PotionKeyCount;
            int buttons = slotCount + potionCount;
            float width = buttons * slotSize + (buttons - 1) * spacing + potionGap;

            // A dark frame behind all buttons.
            Image frame = UIFactory.CreateImage("Frame", transform, new Color(0.06f, 0.05f, 0.04f, 0.88f));
            frame.raycastTarget = false;
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0f);
            frame.rectTransform.sizeDelta = new Vector2(width + 20f, slotSize + 20f);
            frame.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            frame.gameObject.AddComponent<Outline>().effectColor = new Color(0.45f, 0.34f, 0.18f, 0.95f);

            // Keys in order on the bar: 1 2 3 4, the potions 5 6, then 7 8 9 0.
            const int beforePotions = 4;
            slots = new Slot[slotCount];
            potions = new Slot[potionCount];
            float x = -width * 0.5f;
            for (int i = 0; i < slotCount + potionCount; i++)
            {
                if (i == beforePotions) x += potionGap * 0.5f;
                if (i == beforePotions + potionCount) x += potionGap * 0.5f;
                bool potion = i >= beforePotions && i < beforePotions + potionCount;
                int index = potion ? slotCount + (i - beforePotions) : i < beforePotions ? i : i - potionCount;
                string key = potion ? PlayerInputReader.PotionKeys[i - beforePotions] : PlayerInputReader.AbilityKeys[index];
                Slot slot = CreateSlot(index, key, x);
                if (potion) potions[i - beforePotions] = slot;
                else slots[index] = slot;
                x += slotSize + spacing;
            }

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
            Button button = UIFactory.CreateButton($"Ability {index + 1}", transform, new Color(0.12f, 0.1f, 0.09f, 0.9f), null, 0);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = new Vector2(x, 0f);
            int slotIndex = index;
            button.onClick.AddListener(() => OnSlotClicked(slotIndex));

            var slot = new Slot { Root = button.gameObject, Background = (Image)button.targetGraphic };

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
            button.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);

            slot.Name = UIFactory.CreateText("Name", rect, 11, TextAnchor.LowerCenter);
            UIFactory.Stretch(slot.Name.rectTransform);
            slot.Name.rectTransform.offsetMin = new Vector2(3f, 4f);
            slot.Name.rectTransform.offsetMax = new Vector2(-3f, 0f);

            slot.Countdown = UIFactory.CreateText("Countdown", rect, 24, TextAnchor.MiddleCenter);
            UIFactory.Stretch(slot.Countdown.rectTransform);
            return slot;
        }

        private void OnSlotClicked(int button)
        {
            if (button >= slotCount)
            {
                int bagSlot = inventory != null ? inventory.FindUsable(Inventory.PotionKeyUse(button - slotCount)) : -1;
                if (bagSlot >= 0) inventory.RequestUse(bagSlot);
            }
            else if (button == 0)
            {
                if (targeting != null) targeting.AttackSelected();
            }
            else if (caster != null && SpellbookWindow.PendingSpell >= 0 && caster.Spellbook != null)
            {
                // A spell picked in the spellbook goes into this slot.
                caster.Spellbook.RequestAssign(button - 1, SpellbookWindow.PendingSpell);
                SpellbookWindow.ClearPending();
            }
            else if (caster != null)
            {
                caster.TryCast(button - 1);
            }
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
                if (caster != null) caster.CastFailed += OnCastFailed;
                if (targeting != null) targeting.AttackFailed += ShowMessage;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (i == 0)
                {
                    slot.Root.SetActive(targeting != null);
                    if (targeting == null) continue;
                    slot.Background.color = AttackColor;
                    slot.Name.text = "Attack";
                    slot.Cooldown.anchorMax = new Vector2(1f, 0f);
                    slot.Countdown.text = "";
                    SetHighlighted(slot, targeting.IsAttacking);
                    continue;
                }

                int abilitySlot = i - 1;
                AbilityDefinition ability = caster != null ? caster.GetAbility(abilitySlot) : null;
                // Empty slots stay visible (dim) so spells can be put there.
                slot.Root.SetActive(caster != null);
                if (ability == null)
                {
                    slot.Background.color = SpellbookWindow.PendingSpell >= 0 ? new Color(0.25f, 0.18f, 0.35f, 0.9f) : new Color(0.08f, 0.07f, 0.06f, 0.6f);
                    slot.Name.text = "";
                    slot.Countdown.text = "";
                    slot.Cooldown.anchorMax = new Vector2(1f, 0f);
                    SetHighlighted(slot, false);
                    continue;
                }

                Color tint = ability.effectColor * 0.45f;
                tint.a = 0.92f;
                slot.Background.color = tint;
                bool enoughMana = caster.HasManaFor(ability);
                int cost = caster.ManaCost(ability);
                slot.Name.text = cost > 0
                    ? $"{ability.displayName}\n<color={(enoughMana ? "#8fb3ff" : "#ff6655")}>{cost} mana</color>"
                    : ability.displayName;
                SetHighlighted(slot, caster.PendingSlot == abilitySlot);

                float remaining = caster.CooldownRemaining(abilitySlot);
                slot.Cooldown.anchorMax = new Vector2(1f, caster.CooldownFraction(abilitySlot));
                slot.Countdown.text = remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : "";
            }

            AbilityDefinition casting = caster != null ? caster.CastingAbility : null;
            castBar.SetActive(casting != null);
            if (casting != null)
            {
                castFill.anchorMax = new Vector2(caster.CastProgress, 1f);
                castText.text = casting.displayName;
            }

            inventory = caster != null ? caster.GetComponent<Inventory>() : null;
            for (int i = 0; i < potions.Length; i++)
                UpdatePotion(potions[i], Inventory.PotionKeyUse(i));

            messageTimer -= Time.deltaTime;
            Color color = message.color;
            color.a = Mathf.Clamp01(messageTimer / 0.4f);
            message.color = color;
        }

        private void UpdatePotion(Slot slot, ItemUse use)
        {
            slot.Root.SetActive(inventory != null);
            if (inventory == null) return;

            int bagSlot = inventory.FindUsable(use);
            ItemDefinition potion = bagSlot >= 0 ? inventory.Definition(inventory.GetBagSlot(bagSlot)) : null;
            int count = potion != null ? inventory.CountOf(potion) : 0;
            Color tint = potion != null ? potion.iconColor * 0.7f : EmptyPotion;
            tint.a = 0.92f;
            slot.Background.color = tint;
            string label = use == ItemUse.Heal ? "Health" : "Mana";
            slot.Name.text = count > 0 ? $"{label}  x{count}" : label;
            // Potions share one cooldown.
            float remaining = inventory.PotionCooldownRemaining;
            slot.Cooldown.anchorMax = new Vector2(1f, count > 0 ? inventory.PotionCooldownFraction : 0f);
            slot.Countdown.text = count > 0 && remaining >= 0.05f ? Mathf.CeilToInt(remaining).ToString() : "";
            SetHighlighted(slot, false);
        }

        private static void SetHighlighted(Slot slot, bool highlighted)
        {
            var outline = slot.Root.GetComponent<Outline>();
            outline.effectColor = highlighted ? ActiveOutline : IdleOutline;
        }

        private void OnDestroy()
        {
            if (caster != null) caster.CastFailed -= OnCastFailed;
            if (targeting != null) targeting.AttackFailed -= ShowMessage;
        }
    }
}
