using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The local player's ability buttons (keys 1, 2, 3...) with a cooldown overlay and countdown,
    /// plus a line for messages such as "Out of range". Clicking a button uses the ability too.
    /// Builds its own UI; place it on an empty RectTransform in the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AbilityBar : MonoBehaviour
    {
        [SerializeField, Min(1)] private int slotCount = 3;
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

            slot.Name = UIFactory.CreateText("Name", rect, 13, TextAnchor.LowerCenter);
            UIFactory.Stretch(slot.Name.rectTransform);
            slot.Name.rectTransform.offsetMin = new Vector2(3f, 4f);
            slot.Name.rectTransform.offsetMax = new Vector2(-3f, 0f);

            slot.Countdown = UIFactory.CreateText("Countdown", rect, 26, TextAnchor.MiddleCenter);
            UIFactory.Stretch(slot.Countdown.rectTransform);
            return slot;
        }

        private void OnSlotClicked(int slot)
        {
            if (caster != null) caster.TryCast(slot);
        }

        private void OnCastFailed(int slot, CastFailure failure)
        {
            message.text = AbilityCaster.Describe(failure, caster != null ? caster.GetAbility(slot) : null);
            messageTimer = messageSeconds;
        }

        private void Update()
        {
            AbilityCaster local = UIFactory.LocalPlayer<AbilityCaster>();
            if (local != caster)
            {
                if (caster != null) caster.CastFailed -= OnCastFailed;
                caster = local;
                if (caster != null) caster.CastFailed += OnCastFailed;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                AbilityDefinition ability = caster != null ? caster.GetAbility(i) : null;
                Slot slot = slots[i];
                slot.Root.SetActive(ability != null);
                if (ability == null) continue;

                Color tint = ability.effectColor * 0.45f;
                tint.a = 0.92f;
                slot.Background.color = tint;
                slot.Name.text = ability.displayName;

                float remaining = caster.CooldownRemaining(i);
                slot.Cooldown.anchorMax = new Vector2(1f, caster.CooldownFraction(i));
                slot.Countdown.text = remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : "";
            }

            messageTimer -= Time.deltaTime;
            Color color = message.color;
            color.a = Mathf.Clamp01(messageTimer / 0.4f);
            message.color = color;
        }

        private void OnDestroy()
        {
            if (caster != null) caster.CastFailed -= OnCastFailed;
        }
    }
}
