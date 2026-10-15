using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPG.UI
{
    /// <summary>Reports when the pointer enters or leaves a UI element (for tooltips). Needs a raycast target graphic.</summary>
    public class HoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<HoverArea> Entered;
        public event Action<HoverArea> Exited;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke(this);
    }
}
