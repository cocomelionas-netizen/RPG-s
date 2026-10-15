using System.Collections.Generic;
using RPG.Characters;
using RPG.Controls;
using RPG.Navigation;
using UnityEngine;

namespace RPG.Player
{
    /// <summary>
    /// Moves the player on the isometric map with WASD (screen-relative)
    /// or click-to-move (click or hold the left mouse button).
    /// Keyboard input always cancels the current click destination.
    /// Click-to-move walks around rocks and trees when the scene has a <see cref="WorldNavigation"/>.
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
        [Tooltip("While the destination keeps changing (holding the mouse button, following a target), the route is planned at most this often.")]
        [SerializeField, Min(0f)] private float repathInterval = 0.15f;

        private const float WaypointReachedDistance = 0.1f;
        private const float ReplanImmediatelyDistance = 1f;

        private Rigidbody2D body;
        private Vector2 moveDirection;
        private Vector2? destination;
        private Vector2 lastFixedPosition;
        private Vector2 lastRequestedVelocity;
        private float blockedTimer;
        private bool clickMoveSuppressed;

        // Route to the destination; empty when walking straight to it.
        private readonly List<Vector2> waypoints = new List<Vector2>();
        private int waypointIndex;
        private Vector2 plannedDestination;
        private bool routeOutdated;
        private bool routeEndsAtDestination;
        private float repathTimer;

        /// <summary>Last non-zero movement direction. Drives 8-direction animations.</summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.down;

        public bool IsMoving => moveDirection.sqrMagnitude > 0f;

        public float MoveSpeed => moveSpeed;

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
            repathTimer -= Time.deltaTime;
            if (routeOutdated && destination.HasValue && repathTimer <= 0f)
                PlanRoute();

            Vector2 keyboard = input.MoveInput;
            if (keyboard.sqrMagnitude > 0.01f)
            {
                destination = null;
                moveDirection = Vector2.ClampMagnitude(keyboard, 1f);
            }
            else
            {
                if (clickMoveSuppressed && !input.MoveClickHeld)
                    clickMoveSuppressed = false;

                if (input.MoveClickHeld && !clickMoveSuppressed && !input.IsPointerOverUI)
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
                // Do not overshoot the end of the route on the last physics step.
                if (IsOnLastLeg)
                {
                    Vector2 toTarget = SteeringTarget - body.position;
                    if (toTarget.magnitude <= moveSpeed * Time.fixedDeltaTime)
                        velocity = toTarget / Time.fixedDeltaTime;
                }

                UpdateBlockedCheck();
            }

            body.linearVelocity = velocity;
            lastFixedPosition = body.position;
            lastRequestedVelocity = velocity;
        }

        /// <summary>Starts click-to-move towards a world position.</summary>
        public void SetDestination(Vector2 worldPosition)
        {
            bool isNew = !destination.HasValue;
            if (isNew)
                blockedTimer = 0f;
            destination = worldPosition;

            if (isNew || (worldPosition - plannedDestination).sqrMagnitude > ReplanImmediatelyDistance * ReplanImmediatelyDistance)
                PlanRoute();
            else if (worldPosition != plannedDestination)
                routeOutdated = true;
        }

        /// <summary>
        /// The current mouse press was used for something else (for example selecting a target),
        /// so it must not also move the player until the button is released.
        /// </summary>
        public void SuppressClickMoveUntilRelease()
        {
            clickMoveSuppressed = true;
        }

        /// <summary>Turns the player to face a direction without moving (for example towards a target).</summary>
        public void FaceTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
                FacingDirection = direction.normalized;
        }

        public void Stop()
        {
            destination = null;
            ClearRoute();
            moveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
        }

        /// <summary>Stops and instantly moves the player (for example when the server corrects its position).</summary>
        public void Teleport(Vector2 worldPosition)
        {
            Stop();
            body.position = worldPosition;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            lastFixedPosition = worldPosition;
        }

        private Vector2 DirectionToDestination()
        {
            // Pass intermediate waypoints without slowing down.
            while (!IsOnLastLeg && (SteeringTarget - body.position).magnitude <= Mathf.Max(stopDistance, WaypointReachedDistance))
                waypointIndex++;

            Vector2 toTarget = SteeringTarget - body.position;
            if (toTarget.magnitude <= stopDistance)
            {
                destination = null;
                ClearRoute();
                return Vector2.zero;
            }
            return toTarget.normalized;
        }

        /// <summary>
        /// The next point to walk to: a waypoint of the route, or the destination itself on the last leg
        /// (so a destination that keeps moving is followed between re-plans).
        /// </summary>
        private Vector2 SteeringTarget =>
            waypointIndex < waypoints.Count && (!IsOnLastLeg || !routeEndsAtDestination) ? waypoints[waypointIndex] : destination.Value;

        /// <summary>
        /// True when walking to the final point. A route around obstacles ends at its last waypoint
        /// (the nearest walkable spot when the destination itself is blocked).
        /// </summary>
        private bool IsOnLastLeg => waypoints.Count == 0 || waypointIndex >= waypoints.Count - 1;

        private void PlanRoute()
        {
            ClearRoute();
            plannedDestination = destination.Value;
            repathTimer = repathInterval;

            // Without navigation (or without a route) walk straight; being blocked then cancels the move.
            WorldNavigation navigation = WorldNavigation.At(body.position);
            if (navigation != null && !navigation.FindPath(body.position, destination.Value, waypoints))
                waypoints.Clear();
            routeEndsAtDestination = waypoints.Count == 0 || waypoints[waypoints.Count - 1] == plannedDestination;
        }

        private void ClearRoute()
        {
            waypoints.Clear();
            waypointIndex = 0;
            routeOutdated = false;
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
