using RPG.Characters;
using RPG.Controls;
using UnityEngine;

namespace RPG.Player
{
    /// <summary>
    /// Moves the player on the isometric map with WASD (screen-relative)
    /// or click-to-move (click or hold the left mouse button).
    /// Keyboard input always cancels the current click destination.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour, ICharacterMotionSource
    {
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Camera used to convert mouse clicks to world positions. Defaults to Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [Tooltip("Click-to-move stops when the player is this close to the destination.")]
        [SerializeField, Min(0.01f)] private float stopDistance = 0.05f;
        [Tooltip("Click-to-move is cancelled when the player is blocked for this many seconds.")]
        [SerializeField, Min(0f)] private float blockedTimeout = 0.25f;

        private Rigidbody2D body;
        private Vector2 moveDirection;
        private Vector2? destination;
        private Vector2 lastFixedPosition;
        private Vector2 lastRequestedVelocity;
        private float blockedTimer;

        /// <summary>Last non-zero movement direction. Drives 8-direction animations.</summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.down;

        public bool IsMoving => moveDirection.sqrMagnitude > 0f;

        /// <summary>Current click-to-move target, or null when moving with the keyboard or idle.</summary>
        public Vector2? Destination => destination;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void Update()
        {
            Vector2 keyboard = input.MoveInput;
            if (keyboard.sqrMagnitude > 0.01f)
            {
                destination = null;
                moveDirection = Vector2.ClampMagnitude(keyboard, 1f);
            }
            else
            {
                if (input.MoveClickHeld && !input.IsPointerOverUI)
                    SetDestination(ScreenToWorld(input.PointerPosition));

                moveDirection = destination.HasValue ? DirectionToDestination() : Vector2.zero;
            }

            if (IsMoving)
                FacingDirection = moveDirection.normalized;
        }

        private void FixedUpdate()
        {
            // Several physics steps can run before the next Update after a frame hitch,
            // so click-to-move steers here too instead of waiting for Update.
            if (destination.HasValue)
                moveDirection = DirectionToDestination();
            if (IsMoving)
                FacingDirection = moveDirection.normalized;

            Vector2 velocity = moveDirection * moveSpeed;

            if (destination.HasValue)
            {
                // Do not overshoot the destination on the last physics step.
                Vector2 toDestination = destination.Value - body.position;
                if (toDestination.magnitude <= moveSpeed * Time.fixedDeltaTime)
                    velocity = toDestination / Time.fixedDeltaTime;

                UpdateBlockedCheck();
            }

            body.linearVelocity = velocity;
            lastFixedPosition = body.position;
            lastRequestedVelocity = velocity;
        }

        /// <summary>Starts click-to-move towards a world position.</summary>
        public void SetDestination(Vector2 worldPosition)
        {
            if (!destination.HasValue)
                blockedTimer = 0f;
            destination = worldPosition;
        }

        public void Stop()
        {
            destination = null;
            moveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
        }

        private Vector2 DirectionToDestination()
        {
            Vector2 toDestination = destination.Value - body.position;
            if (toDestination.magnitude <= stopDistance)
            {
                destination = null;
                return Vector2.zero;
            }
            return toDestination.normalized;
        }

        private void UpdateBlockedCheck()
        {
            // Compare against what the previous step asked for: a step that requested no movement
            // (for example the first step after a new click) must not count as being blocked.
            float expectedStep = lastRequestedVelocity.magnitude * Time.fixedDeltaTime;
            if (expectedStep <= 0.0001f) return;

            float actualStep = (body.position - lastFixedPosition).magnitude;

            blockedTimer = actualStep < expectedStep * 0.1f ? blockedTimer + Time.fixedDeltaTime : 0f;
            if (blockedTimer >= blockedTimeout && !input.MoveClickHeld)
                destination = null;
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return worldCamera.ScreenToWorldPoint(screenPosition);
        }

        private void OnDrawGizmosSelected()
        {
            if (!destination.HasValue) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, destination.Value);
            Gizmos.DrawWireSphere(destination.Value, 0.1f);
        }
    }
}
