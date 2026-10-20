using System.Collections.Generic;
using RPG.Characters;
using RPG.Navigation;
using RPG.Networking;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Monsters
{
    /// <summary>
    /// Server-driven movement of a monster. The server walks it along paths from <see cref="WorldNavigation"/>
    /// (straight towards the goal when the scene has no navigation) and replicates position, facing and
    /// whether it moves; clients show it slightly in the past, blended between server states, like remote players.
    /// </summary>
    public class MonsterMotion : NetworkBehaviour, ICharacterMotionSource
    {
        [SerializeField, Min(0f)] private float moveSpeed = 1.8f;
        [Tooltip("State updates per second sent to clients while moving.")]
        [SerializeField, Min(1f)] private float sendRate = 15f;
        [Tooltip("Clients show the monster this many seconds in the past so there are always two states to blend.")]
        [SerializeField, Min(0f)] private float interpolationDelay = 0.15f;
        [Tooltip("A moving goal (a fleeing player) is re-planned at most this often.")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.4f;

        private const float RepathDistance = 0.25f;
        // A goal this far from the planned one (for example home instead of a player) is planned at once.
        private const float NewGoalDistance = 1f;

        private readonly NetworkVariable<MotionState> state = new NetworkVariable<MotionState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Server.
        private readonly List<Vector2> path = new List<Vector2>();
        private int pathIndex;
        private Vector2? destination;
        private Vector2 pathGoal;
        private float speedScale = 1f;
        private float repathTimer;
        private float sendTimer;
        private bool sentMoving;

        // Remote.
        private readonly List<MotionState> snapshots = new List<MotionState>();

        private Vector2 facing = Vector2.down;
        private bool moving;

        public Vector2 FacingDirection => facing;
        public bool IsMoving => moving;
        public float MoveSpeed => moveSpeed;

        /// <summary>Server: the share of its speed it can use (0 rooted or stunned, less slowed).</summary>
        public float StatusSpeed => effects != null ? effects.SpeedMultiplier : 1f;

        private RPG.Combat.StatusEffects effects;

        /// <summary>Server only: where the monster is walking to (null when standing).</summary>
        public Vector2? Destination => destination;

        /// <summary>Server only: the last attempt to plan a route found none.</summary>
        public bool PathFailed { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                WriteState();
                return;
            }

            snapshots.Clear();
            snapshots.Add(state.Value);
            ShowRemote(state.Value.Position, state.Value.Facing, state.Value.IsMoving);
        }

        // ---------- Server ----------

        /// <summary>
        /// Server only: walks towards a world position. Calling it every frame with a moving goal is fine:
        /// small goal changes are re-planned at most every repath interval, a new goal far from the old one at once.
        /// </summary>
        public void ServerMoveTo(Vector2 goal, float speedMultiplier = 1f)
        {
            if (!IsServer) return;

            speedScale = Mathf.Max(0.01f, speedMultiplier);
            bool pathUsedUp = pathIndex >= path.Count;
            float goalShift = (goal - pathGoal).sqrMagnitude;
            bool goalMoved = goalShift > RepathDistance * RepathDistance;
            bool newGoal = goalShift > NewGoalDistance * NewGoalDistance;
            destination = goal;

            // A failed search is not retried every frame: it may have searched the whole map.
            bool mayRepath = repathTimer <= 0f || !PathFailed;
            if ((pathUsedUp && mayRepath) || (goalMoved && repathTimer <= 0f) || (newGoal && !PathFailed))
                Repath();
        }

        /// <summary>Server only: stops walking.</summary>
        public void ServerStop()
        {
            if (!IsServer) return;
            destination = null;
            path.Clear();
            pathIndex = 0;
            PathFailed = false;
        }

        /// <summary>Server only: turns to face a direction (for example its target) without moving.</summary>
        public void ServerFace(Vector2 direction)
        {
            if (!IsServer || direction.sqrMagnitude < 0.0001f) return;
            facing = direction.normalized;
        }

        /// <summary>Server only: moves by a small offset if the new spot is walkable (used to spread out around a target).</summary>
        public void ServerNudge(Vector2 offset)
        {
            if (!IsServer || offset.sqrMagnitude < 0.000001f) return;
            Vector2 next = (Vector2)transform.position + offset;
            WorldNavigation navigation = WorldNavigation.At(next);
            if (navigation != null && !navigation.IsWalkable(next)) return;
            SetPosition(next);
            // Clients see the step too (sent at most at the send rate).
            nudged = true;
        }

        private bool nudged;

        /// <summary>Server only: instantly places the monster somewhere (for example back home when it is stuck).</summary>
        public void ServerTeleport(Vector2 position)
        {
            if (!IsServer) return;
            ServerStop();
            SetPosition(position);
            WriteState();
        }

        private void Repath()
        {
            path.Clear();
            pathIndex = 0;
            pathGoal = destination.Value;
            repathTimer = repathInterval;

            WorldNavigation navigation = WorldNavigation.At(transform.position);
            if (navigation == null)
            {
                path.Add(pathGoal);
                PathFailed = false;
                return;
            }

            PathFailed = !navigation.FindPath(transform.position, pathGoal, path);
            if (PathFailed) path.Clear();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
                UpdateServer();
            else
                UpdateRemote();
        }

        private void UpdateServer()
        {
            if (effects == null) effects = GetComponent<RPG.Combat.StatusEffects>();
            repathTimer -= Time.deltaTime;
            moving = false;

            if (destination.HasValue && pathIndex < path.Count)
            {
                Vector2 position = transform.position;
                // Slows and roots (status effects) hold it back.
                float step = moveSpeed * speedScale * StatusSpeed * Time.deltaTime;
                while (step > 0f && pathIndex < path.Count)
                {
                    Vector2 toWaypoint = path[pathIndex] - position;
                    float distance = toWaypoint.magnitude;
                    if (distance > 0.0001f) facing = toWaypoint / distance;

                    if (distance <= step)
                    {
                        position = path[pathIndex];
                        step -= distance;
                        pathIndex++;
                    }
                    else
                    {
                        position += toWaypoint / distance * step;
                        step = 0f;
                    }
                }

                SetPosition(position);
                moving = true;

                // Arrived at the goal of the current route.
                if (pathIndex >= path.Count && (destination.Value - pathGoal).sqrMagnitude <= RepathDistance * RepathDistance)
                    destination = null;
            }

            sendTimer += Time.deltaTime;
            bool stoppedOrStarted = moving != sentMoving;
            if (stoppedOrStarted || ((moving || nudged) && sendTimer >= 1f / sendRate) || Vector2.Dot(facing, state.Value.Facing) < 0.999f)
            {
                nudged = false;
                WriteState();
            }
        }

        private void SetPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void WriteState()
        {
            sendTimer = 0f;
            sentMoving = moving;
            state.Value = new MotionState
            {
                Position = transform.position,
                Facing = facing,
                IsMoving = moving,
                ServerTime = NetworkManager.ServerTime.Time,
            };
        }

        // ---------- Remote ----------

        private void UpdateRemote()
        {
            MotionState latest = state.Value;
            if (snapshots.Count == 0 || latest.ServerTime > snapshots[snapshots.Count - 1].ServerTime)
                snapshots.Add(latest);

            double renderTime = NetworkManager.ServerTime.Time - interpolationDelay;
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

        private void ShowRemote(Vector2 position, Vector2 remoteFacing, bool isMoving)
        {
            SetPosition(position);
            if (remoteFacing.sqrMagnitude > 0.0001f) facing = remoteFacing;
            moving = isMoving;
        }
    }
}
