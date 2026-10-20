using System.Collections.Generic;
using RPG.CameraSystem;
using RPG.Characters;
using RPG.Combat;
using RPG.Controls;
using RPG.Player;
using RPG.World;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Networking
{
    /// <summary>
    /// Networked player movement: client-predicted, server-validated.
    /// - Owner: moves instantly with the normal <see cref="PlayerMovement"/> and reports its position to the server.
    /// - Server: checks every report (speed budget + collision cast). Accepted moves become the authoritative state;
    ///   rejected ones send the owner back to the last accepted position.
    /// - Everyone else: input and physics are off; the character is interpolated between server states.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
    public class PlayerNetworkMovement : NetworkBehaviour, ICharacterMotionSource
    {
        [Header("Owner")]
        [Tooltip("Position reports per second while moving.")]
        [SerializeField, Min(1f)] private float sendRate = 20f;

        [Header("Remote")]
        [Tooltip("Remote characters are shown this many seconds in the past so there are always two states to blend.")]
        [SerializeField, Min(0f)] private float interpolationDelay = 0.15f;

        [Header("Server validation")]
        [SerializeField, Min(1f)] private float speedTolerance = 1.25f;
        [Tooltip("Collision checks use a slightly smaller circle than the real feet so sliding along walls is not rejected.")]
        [SerializeField, Range(0.1f, 1f)] private float collisionRadiusScale = 0.8f;

        private readonly NetworkVariable<MotionState> state = new NetworkVariable<MotionState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerMovement movement;
        private PlayerInputReader input;
        private Rigidbody2D body;
        private CircleCollider2D feet;
        private CharacterAnimator characterAnimator;
        private Health health;

        // Owner.
        private float sendTimer;
        private MotionState lastSent;
        private int appliedCorrection;

        // Server.
        private MovementValidator validator;
        private Vector2 serverPosition;
        private int correctionCount;
        private readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>();

        // Remote.
        private readonly List<MotionState> snapshots = new List<MotionState>();
        private Vector2 remoteFacing = Vector2.down;
        private bool remoteMoving;

        public Vector2 FacingDirection => remoteFacing;
        public bool IsMoving => remoteMoving;

        /// <summary>The last position the server accepted.</summary>
        public Vector2 AuthoritativePosition => state.Value.Position;

        /// <summary>How many times the server has corrected this player.</summary>
        public int CorrectionCount => IsServer ? correctionCount : appliedCorrection;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            input = GetComponent<PlayerInputReader>();
            body = GetComponent<Rigidbody2D>();
            feet = GetComponent<CircleCollider2D>();
            characterAnimator = GetComponent<CharacterAnimator>();
            health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                serverPosition = transform.position;
                validator = new MovementValidator(movement.MoveSpeed, Now, speedTolerance);
                WriteState(serverPosition, Vector2.down, false);
            }

            if (IsOwner)
                SetUpOwner();
            else
                SetUpRemote();
        }

        private void SetUpOwner()
        {
            if (characterAnimator != null) characterAnimator.MotionSource = movement;

            // The local player is the one the camera follows and the click marker belongs to.
            var cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null) cameraFollow.Follow(transform, snap: true);
            var clickMarker = FindAnyObjectByType<ClickMarker>();
            if (clickMarker != null) clickMarker.Bind(movement);
        }

        private void SetUpRemote()
        {
            movement.enabled = false;
            if (input != null) input.enabled = false;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.linearVelocity = Vector2.zero;
            // Players walk through each other; the server checks collisions with casts instead.
            if (feet != null) feet.enabled = false;
            if (characterAnimator != null) characterAnimator.MotionSource = this;

            snapshots.Clear();
            snapshots.Add(state.Value);
            SetRemotePosition(state.Value.Position);
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
                UpdateOwner();
            else
                UpdateRemote();
        }

        private double Now => NetworkManager.ServerTime.Time;

        // ---------- Owner ----------

        private void UpdateOwner()
        {
            // Dead players cannot move until the server respawns them.
            bool dead = health != null && health.IsDead;
            if (dead && movement.enabled)
            {
                movement.Stop();
                movement.enabled = false;
            }
            else if (!dead && !movement.enabled)
            {
                movement.enabled = true;
            }

            sendTimer += Time.deltaTime;
            if (sendTimer < 1f / sendRate) return;
            sendTimer = 0f;

            var current = new MotionState
            {
                Position = transform.position,
                Facing = movement.FacingDirection,
                IsMoving = movement.IsMoving,
            };

            bool changed = (current.Position - lastSent.Position).sqrMagnitude > 0.000001f
                           || current.IsMoving != lastSent.IsMoving
                           || Vector2.Dot(current.Facing, lastSent.Facing) < 0.999f;
            if (!changed) return;
            lastSent = current;

            if (IsServer)
            {
                // Host: the server's own player is trusted.
                serverPosition = current.Position;
                WriteState(current.Position, current.Facing, current.IsMoving);
            }
            else
            {
                SubmitMotionRpc(current.Position, current.Facing, current.IsMoving, appliedCorrection);
            }
        }

        [Rpc(SendTo.Owner)]
        private void ApplyCorrectionRpc(Vector2 position, int correctionId)
        {
            appliedCorrection = correctionId;
            Vector2 previous = transform.position;
            movement.Teleport(position);
            SnapCameraIfFar(previous);
            lastSent = new MotionState { Position = position, Facing = movement.FacingDirection };
        }

        // ---------- Server ----------

        [Rpc(SendTo.Server)]
        private void SubmitMotionRpc(Vector2 position, Vector2 facing, bool isMoving, int correctionId, RpcParams rpcParams = default)
        {
            // Only the owner may move its player, and reports sent before the last correction are stale.
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (correctionId != correctionCount) return;
            if (health != null && health.IsDead) return;

            if (!IsFinite(position) || !IsFinite(facing))
            {
                Correct("invalid values");
                return;
            }

            float distance = Vector2.Distance(serverPosition, position);
            // Stunned or rooted players do not move; hasted ones may move as much faster as their haste allows.
            var effects = health != null ? health.Effects : null;
            if (effects != null && effects.IsRooted && distance > 0.05f)
            {
                Correct("rooted");
                return;
            }
            float speedScale = effects != null ? effects.SpeedMultiplier : 1f;
            if (!validator.TryMove(distance, Now, speedScale))
            {
                Correct($"too fast ({distance:0.00} units)");
                return;
            }

            if (IsPathBlocked(serverPosition, position))
            {
                Correct("path blocked");
                return;
            }

            serverPosition = position;
            WriteState(position, facing.sqrMagnitude > 0.0001f ? facing.normalized : state.Value.Facing, isMoving);
        }

        /// <summary>Server only: moves the player somewhere new (for example respawning) and tells its owner.</summary>
        public void ServerTeleport(Vector2 position)
        {
            if (!IsServer) return;

            serverPosition = position;
            validator.Reset(Now);
            WriteState(position, state.Value.Facing, false);

            if (IsOwner)
            {
                Vector2 previous = transform.position;
                movement.Teleport(position);
                SnapCameraIfFar(previous);
                lastSent = new MotionState { Position = position, Facing = movement.FacingDirection };
            }
            else
            {
                correctionCount++;
                ApplyCorrectionRpc(position, correctionCount);
            }
        }

        /// <summary>Owner: after a long jump (respawn, portal) the camera jumps too instead of gliding across the map.</summary>
        private void SnapCameraIfFar(Vector2 previousPosition)
        {
            if (Vector2.Distance(transform.position, previousPosition) < 3f) return;
            var cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null) cameraFollow.Follow(transform, snap: true);
        }

        private void Correct(string reason)
        {
            correctionCount++;
            validator.Reset(Now);
            Debug.LogWarning($"[Server] Rejected movement of client {OwnerClientId}: {reason}. Corrected to {serverPosition}.");
            ApplyCorrectionRpc(serverPosition, correctionCount);
        }

        private bool IsPathBlocked(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return false;

            Vector2 offset = feet != null ? feet.offset : Vector2.zero;
            float radius = (feet != null ? feet.radius : 0.1f) * collisionRadiusScale;
            Physics2D.CircleCast(from + offset, radius, delta / distance, ContactFilter2D.noFilter, castHits, distance);

            foreach (RaycastHit2D hit in castHits)
            {
                if (hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<PlayerNetworkMovement>() != null) continue;
                return true;
            }
            return false;
        }

        private void WriteState(Vector2 position, Vector2 facing, bool isMoving)
        {
            state.Value = new MotionState
            {
                Position = position,
                Facing = facing,
                IsMoving = isMoving,
                ServerTime = Now,
            };
        }

        private static bool IsFinite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

        // ---------- Remote ----------

        private void UpdateRemote()
        {
            MotionState latest = state.Value;
            if (snapshots.Count == 0 || latest.ServerTime > snapshots[snapshots.Count - 1].ServerTime)
                snapshots.Add(latest);

            double renderTime = Now - interpolationDelay;
            while (snapshots.Count > 2 && snapshots[1].ServerTime <= renderTime)
                snapshots.RemoveAt(0);

            MotionState from = snapshots[0];
            if (snapshots.Count == 1 || renderTime <= from.ServerTime)
            {
                ShowRemote(from.Position, from.Facing, from.IsMoving);
                return;
            }

            MotionState to = snapshots[1];
            double span = to.ServerTime - from.ServerTime;
            float t = span > 0.0 ? Mathf.Clamp01((float)((renderTime - from.ServerTime) / span)) : 1f;
            ShowRemote(Vector2.Lerp(from.Position, to.Position, t), to.Facing, t < 1f ? to.IsMoving || from.IsMoving : to.IsMoving);
        }

        private void ShowRemote(Vector2 position, Vector2 facing, bool isMoving)
        {
            SetRemotePosition(position);
            if (facing.sqrMagnitude > 0.0001f) remoteFacing = facing;
            remoteMoving = isMoving;
        }

        private void SetRemotePosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
