using System;
using System.Collections.Generic;
using RPG.Controls;
using RPG.Player;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Local player's target selection and attack intent.
    /// - Left click on a unit or Tab (next hostile target nearby): select it, without attacking.
    /// - Key 1 (the Attack button) or F: walk into range of the selected enemy and auto-attack it.
    /// - Left click on the ground or WASD: stop attacking (the target stays selected). Esc: clear target.
    /// Runs before <see cref="PlayerMovement"/> so a click on a unit is not also a move click.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Health), typeof(AutoAttack))]
    public class PlayerTargeting : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float tabTargetRange = 10f;

        private PlayerInputReader input;
        private PlayerMovement movement;
        private AutoAttack autoAttack;
        private Health health;
        private Camera worldCamera;
        private bool attackIntent;
        private readonly List<Collider2D> pointerHits = new List<Collider2D>();

        /// <summary>The local player's selected target (null when nothing is selected).</summary>
        public static Health LocalTarget { get; private set; }

        public static event Action<Health> LocalTargetChanged;

        /// <summary>Owner only: Attack was pressed without a living enemy selected (message for the player).</summary>
        public event Action<string> AttackFailed;

        /// <summary>True while the local player wants to auto-attack its target.</summary>
        public bool IsAttacking => attackIntent;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            movement = GetComponent<PlayerMovement>();
            autoAttack = GetComponent<AutoAttack>();
            health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            worldCamera = Camera.main;
            autoAttack.AttackStopped += OnServerStoppedAttack;
            foreach (UnitFrame frame in FindObjectsByType<UnitFrame>(FindObjectsInactive.Include))
                if (frame.Role == UnitFrameRole.LocalPlayer)
                    frame.Bind(health);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            autoAttack.AttackStopped -= OnServerStoppedAttack;
            SetTarget(null);
        }

        /// <summary>Selects a target (null clears it). Does not start attacking.</summary>
        public void SetTarget(Health target)
        {
            if (LocalTarget == target) return;
            LocalTarget = target;
            LocalTargetChanged?.Invoke(target);
        }

        /// <summary>Starts walking to and auto-attacking the selected target. False when it is not a living enemy.</summary>
        public bool StartAttack()
        {
            if (LocalTarget == null || !health.IsHostileTo(LocalTarget) || LocalTarget.IsDead) return false;
            attackIntent = true;
            autoAttack.RequestAttack(LocalTarget);
            return true;
        }

        /// <summary>Owner: the Attack button (key 1, F, or a click on the action bar).</summary>
        public void AttackSelected()
        {
            if (!IsOwner || health.IsDead) return;
            if (!StartAttack())
                AttackFailed?.Invoke(LocalTarget != null && !LocalTarget.IsDead && !health.IsHostileTo(LocalTarget)
                    ? "You cannot attack that"
                    : "No enemy target");
        }

        public void StopAttack()
        {
            if (!attackIntent) return;
            attackIntent = false;
            autoAttack.RequestStop();
            movement.Stop();
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (health.IsDead)
            {
                StopAttack();
                return;
            }

            if (LocalTarget != null && !LocalTarget.IsSpawned)
                SetTarget(null);

            if (input.CancelPressed)
            {
                StopAttack();
                SetTarget(null);
            }

            if (input.TargetNextPressed)
                SetTarget(NextTarget());

            if (input.AttackPressed || input.AbilityPressed(0))
                AttackSelected();

            if (input.MoveClickPressed && !input.IsPointerOverUI)
            {
                Health clicked = UnitUnderPointer();
                if (clicked != null)
                {
                    movement.SuppressClickMoveUntilRelease();
                    // Selecting does not attack; that is the Attack button's job.
                    if (clicked != LocalTarget) StopAttack();
                    SetTarget(clicked);
                }
                else
                {
                    StopAttack();
                }
            }

            if (input.MoveInput.sqrMagnitude > 0.01f)
                StopAttack();

            if (attackIntent)
                ApproachTarget();
        }

        private void ApproachTarget()
        {
            if (LocalTarget == null || LocalTarget.IsDead)
            {
                StopAttack();
                return;
            }

            Vector2 self = transform.position;
            Vector2 target = LocalTarget.transform.position;
            if (Vector2.Distance(self, target) > autoAttack.Range * 0.9f)
            {
                movement.SetDestination(target);
            }
            else
            {
                if (movement.Destination.HasValue) movement.Stop();
                movement.FaceTowards(target - self);
            }
        }

        private void OnServerStoppedAttack()
        {
            attackIntent = false;
            if (movement.Destination.HasValue) movement.Stop();
        }

        private Health UnitUnderPointer()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return null;

            Vector2 point = worldCamera.ScreenToWorldPoint(input.PointerPosition);
            Physics2D.OverlapPoint(point, ContactFilter2D.noFilter, pointerHits);

            Health best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider2D hit in pointerHits)
            {
                Health unit = hit.GetComponentInParent<Health>();
                if (unit == null || unit == health || !unit.IsSpawned || unit.IsDead) continue;

                float distance = Vector2.Distance(point, unit.transform.position);
                if (distance < bestDistance)
                {
                    best = unit;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private Health NextTarget()
        {
            var candidates = new List<Health>();
            Vector2 self = transform.position;
            foreach (Health unit in FindObjectsByType<Health>())
            {
                if (!unit.IsSpawned || unit.IsDead || !health.IsHostileTo(unit)) continue;
                if (Vector2.Distance(self, unit.transform.position) <= tabTargetRange)
                    candidates.Add(unit);
            }

            if (candidates.Count == 0) return LocalTarget;

            candidates.Sort((a, b) => Vector2.Distance(self, a.transform.position).CompareTo(Vector2.Distance(self, b.transform.position)));
            int index = candidates.IndexOf(LocalTarget);
            return candidates[(index + 1) % candidates.Count];
        }
    }
}
