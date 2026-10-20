using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPG.UI
{
    /// <summary>
    /// Reports when the pointer enters or leaves a UI element (for tooltips). Needs a raycast target graphic. Hiding the
    /// element while it is hovered counts as leaving it (the event system sends no exit then), so its tooltip goes too.
    /// </summary>
    public class HoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<HoverArea> Entered;
        public event Action<HoverArea> Exited;

        private bool hovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Entered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Exited?.Invoke(this);
        }

        private void OnDisable()
        {
            if (!hovered) return;
            hovered = false;
            Exited?.Invoke(this);
        }
    }
}
