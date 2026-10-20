using System;
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
        public Outline Border;
        public Text Label;
        public Text Count;

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
