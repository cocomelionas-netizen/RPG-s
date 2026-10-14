using RPG.Controls;
using UnityEngine;

namespace RPG.CameraSystem
{
    /// <summary>
    /// Smoothly follows a target with an orthographic camera and zooms with the mouse wheel.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Follow")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [SerializeField] private Vector2 offset = new Vector2(0f, 0.3f);

        [Header("Zoom")]
        [SerializeField, Min(0.1f)] private float zoomStep = 0.75f;
        [SerializeField, Min(0.1f)] private float minZoom = 2.5f;
        [SerializeField, Min(0.1f)] private float maxZoom = 8f;
        [SerializeField, Min(0f)] private float zoomSpeed = 10f;

        private Camera cam;
        private PlayerInputReader input;
        private Vector3 velocity;
        private float targetZoom;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            targetZoom = cam.orthographicSize;
            CacheInput();
        }

        /// <summary>Sets what the camera follows (for example a newly spawned player).</summary>
        public void Follow(Transform newTarget, bool snap = false)
        {
            target = newTarget;
            CacheInput();
            if (snap && target != null)
                transform.position = DesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);

            if (input != null && input.ZoomDelta != 0f && !input.IsPointerOverUI)
                targetZoom = Mathf.Clamp(targetZoom - input.ZoomDelta * zoomStep, minZoom, maxZoom);

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));
        }

        private Vector3 DesiredPosition()
        {
            Vector3 position = target.position + (Vector3)offset;
            position.z = transform.position.z;
            return position;
        }

        private void CacheInput()
        {
            input = target != null ? target.GetComponent<PlayerInputReader>() : null;
        }
    }
}
