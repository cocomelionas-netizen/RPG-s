using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>What is being dragged with the mouse (the source game's draggable nodes).</summary>
    public enum DragKind
    {
        None,
        /// <summary>A spell from the spellbook; Value is its index.</summary>
        Spell,
        /// <summary>An item from the bag; Value is its catalog id, BagIndex its bag slot.</summary>
        Item,
        /// <summary>An action bar slot; Value is the slot.</summary>
        BarSlot,
    }

    /// <summary>One drag in progress: what it carries and how it looks under the pointer.</summary>
    public struct DragPayload
    {
        public DragKind Kind;
        public int Value;
        public int BagIndex;
        public string Label;
        public Color Color;
    }

    /// <summary>
    /// Makes a UI element draggable: <see cref="Payload"/> says what it carries (null: nothing to drag right now). A small
    /// square with the label follows the pointer; a <see cref="DropTarget"/> under it on release receives the payload.
    /// <see cref="Ended"/> tells whether it was dropped on a target (a bar slot dragged off the bar is removed).
    /// </summary>
    public class DragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Func<DragPayload?> Payload;
        public Action<bool> Ended;

        /// <summary>The drag in progress (Kind None when nothing is dragged).</summary>
        public static DragPayload Current { get; private set; }

        public static bool IsDragging => Current.Kind != DragKind.None;

        private static bool dropped;
        private RectTransform ghost;
        private Canvas canvas;

        /// <summary>Called by the target the payload was dropped on.</summary>
        internal static void MarkDropped() => dropped = true;

        public void OnBeginDrag(PointerEventData eventData)
        {
            DragPayload? payload = Payload?.Invoke();
            if (!payload.HasValue || payload.Value.Kind == DragKind.None)
            {
                eventData.pointerDrag = null;
                return;
            }
            Current = payload.Value;
            dropped = false;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            ghost = CreateGhost(payload.Value);
            Move(eventData);
        }

        public void OnDrag(PointerEventData eventData) => Move(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            bool wasDropped = dropped;
            Current = default;
            dropped = false;
            Ended?.Invoke(wasDropped);
        }

        private void OnDisable()
        {
            if (ghost == null) return;
            Destroy(ghost.gameObject);
            ghost = null;
            Current = default;
        }

        private RectTransform CreateGhost(DragPayload payload)
        {
            Transform parent = canvas != null ? canvas.transform : transform;
            Color color = payload.Color;
            color.a = 0.85f;
            Image image = UIFactory.CreateImage("Dragging", parent, color);
            image.raycastTarget = false;
            image.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.85f, 0.3f, 0.9f);
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(58f, 58f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Text label = UIFactory.CreateText("Label", rect, 11, TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform);
            label.text = payload.Label;
            rect.SetAsLastSibling();
            return rect;
        }

        private void Move(PointerEventData eventData)
        {
            if (ghost == null) return;
            var parent = (RectTransform)ghost.parent;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, eventData.position, cam, out Vector3 point))
                ghost.position = point;
        }
    }

    /// <summary>A UI element that takes drops: <see cref="Accepts"/> filters them, <see cref="Dropped"/> handles them.</summary>
    public class DropTarget : MonoBehaviour, IDropHandler
    {
        public Func<DragPayload, bool> Accepts;
        public Action<DragPayload> Dropped;

        public void OnDrop(PointerEventData eventData)
        {
            DragPayload payload = DragSource.Current;
            if (payload.Kind == DragKind.None || (Accepts != null && !Accepts(payload))) return;
            DragSource.MarkDropped();
            Dropped?.Invoke(payload);
        }
    }
}
