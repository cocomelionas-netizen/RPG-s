using System;
using RPG.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// One item square in the inventory window: shows an item and reports hover and clicks. Hiding it while it is
    /// hovered counts as the pointer leaving it.
    /// </summary>
    public class ItemSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Background;
        /// <summary>An empty equipment slot's silhouette (optional): shown only while the slot is empty.</summary>
        public Image Placeholder;
        /// <summary>The item's picture (hidden for none: the name shows instead).</summary>
        public Image Icon;
        public Outline Border;
        public Text Label;
        public Text Count;

        /// <summary>
        /// Shows an item: its icon on a dark square tinted by its quality, or (an item without an icon) its name on its
        /// color. The border is the quality color. Null shows an empty slot of <paramref name="empty"/>.
        /// </summary>
        public void Show(ItemDefinition item, string displayName, Color rarity, Color empty)
        {
            if (Placeholder != null && Placeholder.enabled != (item == null)) Placeholder.enabled = item == null;
            if (item == null)
            {
                Background.color = empty;
                Border.effectColor = new Color(0f, 0f, 0f, 0.5f);
                Label.text = "";
                UIFactory.SetIcon(Icon, null, Color.white);
                return;
            }
            bool pictured = UIFactory.SetIcon(Icon, item.icon, item.iconTint);
            Color background = pictured ? Color.Lerp(FilledColor, rarity, 0.15f) : item.iconColor * 0.55f;
            background.a = 0.95f;
            Background.color = background;
            Border.effectColor = rarity;
            Label.text = pictured ? "" : displayName;
            Label.color = rarity;
        }

        private static readonly Color FilledColor = new Color(0.07f, 0.065f, 0.06f);

        public event Action<ItemSlotView> PointerEntered;
        public event Action<ItemSlotView> PointerExited;

        private bool hovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            PointerEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            PointerExited?.Invoke(this);
        }

        private void OnDisable()
        {
            if (!hovered) return;
            hovered = false;
            PointerExited?.Invoke(this);
        }
    }
}
