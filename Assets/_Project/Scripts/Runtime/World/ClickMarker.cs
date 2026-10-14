using RPG.Player;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Shows a pulsing ring on the ground where the player is walking to with click-to-move.
    /// Hidden while there is no click destination.
    /// </summary>
    public class ClickMarker : MonoBehaviour
    {
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private SpriteRenderer markerRenderer;

        [Header("Pulse")]
        [SerializeField, Min(0f)] private float pulseSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.15f;

        private Vector3 baseScale;

        public bool IsVisible => markerRenderer != null && markerRenderer.enabled;

        private void Awake()
        {
            if (markerRenderer == null) markerRenderer = GetComponentInChildren<SpriteRenderer>();
            baseScale = transform.localScale;
            SetVisible(false);
        }

        /// <summary>Sets which player the marker follows (for example a newly spawned local player).</summary>
        public void Bind(PlayerMovement playerMovement)
        {
            movement = playerMovement;
        }

        private void LateUpdate()
        {
            Vector2? destination = movement != null ? movement.Destination : null;
            if (!destination.HasValue)
            {
                SetVisible(false);
                return;
            }

            transform.position = new Vector3(destination.Value.x, destination.Value.y, transform.position.z);
            transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (markerRenderer != null)
                markerRenderer.enabled = visible;
        }
    }
}
