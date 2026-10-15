using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>One item square in the inventory window: shows an item and reports hover and clicks.</summary>
    public class ItemSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Background;
        public Outline Border;
        public Text Label;
        public Text Count;

        public event Action<ItemSlotView> PointerEntered;
        public event Action<ItemSlotView> PointerExited;

        public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke(this);
    }
}
