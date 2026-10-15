using RPG.Combat;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's action bar: Attack (key 1) followed by the class abilities (keys 2, 3, 4...)
    /// with a cooldown overlay and countdown, plus a line for messages such as "No enemy target".
    /// Clicking a button does the same as its key. Attack and an ability being walked into range are highlighted.
    /// Builds its own UI; place it on an empty RectTransform in the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AbilityBar : MonoBehaviour
    {
        [Tooltip("Buttons in total: Attack plus this many minus one abilities.")]
        [SerializeField, Min(1)] private int slotCount = 4;
        [SerializeField, Min(16f)] private float slotSize = 76f;
        [SerializeField, Min(0f)] private float spacing = 10f;
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
        private Text message;
        private float messageTimer;
        private AbilityCaster caster;
        private PlayerTargeting targeting;

        private static readonly Color AttackColor = new Color(0.55f, 0.18f, 0.12f, 0.92f);
        private static readonly Color ActiveOutline = new Color(1f, 0.85f, 0.3f);

        private void Awake()
        {
            slots = new Slot[slotCount];
            float width = slotCount * slotSize + (slotCount - 1) * spacing;
            for (int i = 0; i < slotCount; i++)
                slots[i] = CreateSlot(i, -width * 0.5f + i * (slotSize + spacing));

            message = UIFactory.CreateText("Message", transform, 22, TextAnchor.MiddleCenter);
            message.rectTransform.anchorMin = message.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            message.rectTransform.pivot = new Vector2(0.5f, 0f);
            message.rectTransform.sizeDelta = new Vector2(600f, 30f);
            message.rectTransform.anchoredPosition = new Vector2(0f, slotSize + 8f);
            message.color = new Color(1f, 0.45f, 0.35f);
            message.text = "";
        }

        private Slot CreateSlot(int index, float x)
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

            slot.Key = UIFactory.CreateText("Key", rect, 18, TextAnchor.UpperLeft);
            UIFactory.Stretch(slot.Key.rectTransform);
            slot.Key.rectTransform.offsetMin = new Vector2(5f, 0f);
            slot.Key.rectTransform.offsetMax = new Vector2(0f, -3f);
            slot.Key.text = (index + 1).ToString();
            button.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);

            slot.Name = UIFactory.CreateText("Name", rect, 13, TextAnchor.LowerCenter);
            UIFactory.Stretch(slot.Name.rectTransform);
            slot.Name.rectTransform.offsetMin = new Vector2(3f, 4f);
            slot.Name.rectTransform.offsetMax = new Vector2(-3f, 0f);

            slot.Countdown = UIFactory.CreateText("Countdown", rect, 26, TextAnchor.MiddleCenter);
            UIFactory.Stretch(slot.Countdown.rectTransform);
            return slot;
        }

        private void OnSlotClicked(int button)
        {
            if (button == 0)
            {
                if (targeting != null) targeting.AttackSelected();
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
                slot.Root.SetActive(ability != null);
                if (ability == null) continue;

                Color tint = ability.effectColor * 0.45f;
                tint.a = 0.92f;
                slot.Background.color = tint;
                bool enoughMana = caster.HasManaFor(ability);
                slot.Name.text = ability.manaCost > 0
                    ? $"{ability.displayName}\n<color={(enoughMana ? "#8fb3ff" : "#ff6655")}>{ability.manaCost} mana</color>"
                    : ability.displayName;
                SetHighlighted(slot, caster.PendingSlot == abilitySlot);

                float remaining = caster.CooldownRemaining(abilitySlot);
                slot.Cooldown.anchorMax = new Vector2(1f, caster.CooldownFraction(abilitySlot));
                slot.Countdown.text = remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : "";
            }

            messageTimer -= Time.deltaTime;
            Color color = message.color;
            color.a = Mathf.Clamp01(messageTimer / 0.4f);
            message.color = color;
        }

        private static void SetHighlighted(Slot slot, bool highlighted)
        {
            var outline = slot.Root.GetComponent<Outline>();
            outline.effectColor = highlighted ? ActiveOutline : new Color(0f, 0f, 0f, 0.6f);
        }

        private void OnDestroy()
        {
            if (caster != null) caster.CastFailed -= OnCastFailed;
            if (targeting != null) targeting.AttackFailed -= ShowMessage;
        }
    }
}
