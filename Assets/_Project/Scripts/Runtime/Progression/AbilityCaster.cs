using System;
using System.Collections.Generic;
using RPG.Characters;
using RPG.Combat;
using RPG.Controls;
using RPG.Player;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Progression
{
    public enum CastFailure : byte
    {
        None,
        Dead,
        NotReady,
        NoTarget,
        OutOfRange,
        NoMana,
    }

    /// <summary>
    /// Uses the abilities of the player's class (keys 2, 3, 4; key 1 is Attack, see <see cref="PlayerTargeting"/>).
    /// When the target is too far, the owner first walks into range and then uses the ability.
    /// The owner asks; the server checks that the caster
    /// is alive, the ability is ready (its own cooldown and a short global cooldown), and the target is a living
    /// enemy in range, then applies damage or healing and tells everyone to show it.
    /// Cooldowns shown to the owner start when the server confirms the cast.
    /// </summary>
    // Before PlayerMovement, so cancelling a walk into range never cancels the click that cancelled it.
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CharacterProgress), typeof(Health), typeof(AutoAttack))]
    public class AbilityCaster : NetworkBehaviour
    {
        public const float GlobalCooldown = 1f;
        /// <summary>Key 1 is Attack; abilities start at key 2.</summary>
        public const int FirstAbilityKey = 1;
        private const int MaxSlots = 8;
        private const float ApproachRangeFactor = 0.9f;

        private CharacterProgress progress;
        private Health health;
        private Mana mana;
        private AutoAttack autoAttack;
        private CharacterAnimator characterAnimator;
        private PlayerMovement movement;
        private PlayerInputReader input;

        // Server time when each slot is ready again (server: authoritative, owner: from confirmations).
        private readonly double[] readyAt = new double[MaxSlots];
        private readonly double[] cooldownStartedAt = new double[MaxSlots];
        private double globalReadyAt;
        private readonly List<Health> areaTargets = new List<Health>();

        // Owner: an ability waiting until the player has walked into range.
        private int pendingSlot = -1;
        private Health pendingTarget;

        /// <summary>Owner only: the slot of the ability the player is walking into range for (-1 when none).</summary>
        public int PendingSlot => pendingSlot;

        /// <summary>Owner only: the server refused a cast or the client could not ask: (slot, reason).</summary>
        public event Action<int, CastFailure> CastFailed;

        /// <summary>Raised on every peer when an ability was used: slot.</summary>
        public event Action<int> Cast;

        public IReadOnlyList<AbilityDefinition> Abilities =>
            progress.Class != null ? progress.Class.abilities : Array.Empty<AbilityDefinition>();

        private double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0.0;

        private void Awake()
        {
            progress = GetComponent<CharacterProgress>();
            health = GetComponent<Health>();
            mana = GetComponent<Mana>();
            autoAttack = GetComponent<AutoAttack>();
            characterAnimator = GetComponent<CharacterAnimator>();
            movement = GetComponent<PlayerMovement>();
            input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || input == null || !input.enabled) return;
            for (int key = FirstAbilityKey; key < PlayerInputReader.AbilityKeyCount; key++)
                if (input.AbilityPressed(key)) TryCast(key - FirstAbilityKey);

            if (pendingSlot >= 0) UpdateApproach();
        }

        private void UpdateApproach()
        {
            AbilityDefinition ability = GetAbility(pendingSlot);
            bool cancelled = ability == null || health.IsDead
                             || pendingTarget == null || !pendingTarget.IsSpawned || pendingTarget.IsDead
                             || PlayerTargeting.LocalTarget != pendingTarget
                             || input.MoveInput.sqrMagnitude > 0.01f || input.CancelPressed
                             || (input.MoveClickPressed && !input.IsPointerOverUI);
            if (cancelled)
            {
                CancelApproach();
                return;
            }

            Vector2 target = pendingTarget.transform.position;
            if (Vector2.Distance(transform.position, target) > ability.range * ApproachRangeFactor)
            {
                if (movement != null) movement.SetDestination(target);
                return;
            }

            int slot = pendingSlot;
            CancelApproach();
            TryCast(slot);
        }

        private void CancelApproach()
        {
            if (pendingSlot >= 0 && movement != null && movement.Destination.HasValue) movement.Stop();
            pendingSlot = -1;
            pendingTarget = null;
        }

        public AbilityDefinition GetAbility(int slot)
        {
            IReadOnlyList<AbilityDefinition> abilities = Abilities;
            return slot >= 0 && slot < abilities.Count && slot < MaxSlots ? abilities[slot] : null;
        }

        /// <summary>True when there is enough mana for the ability (always without a mana pool).</summary>
        public bool HasManaFor(AbilityDefinition ability) => ability == null || mana == null || mana.Has(ability.manaCost);

        /// <summary>Seconds until the slot can be used again (its own cooldown or the global one).</summary>
        public float CooldownRemaining(int slot)
        {
            if (slot < 0 || slot >= MaxSlots || !IsSpawned) return 0f;
            return (float)Math.Max(0.0, Math.Max(readyAt[slot], globalReadyAt) - Now);
        }

        /// <summary>0..1 share of the slot's current cooldown still left (for the cooldown overlay).</summary>
        public float CooldownFraction(int slot)
        {
            if (slot < 0 || slot >= MaxSlots || !IsSpawned) return 0f;
            double now = Now;
            double end = Math.Max(readyAt[slot], globalReadyAt);
            if (end <= now) return 0f;

            double start = readyAt[slot] >= globalReadyAt ? cooldownStartedAt[slot] : globalReadyAt - GlobalCooldown;
            double length = end - start;
            return length > 0.0 ? Mathf.Clamp01((float)((end - now) / length)) : 0f;
        }

        // ---------- Owner ----------

        /// <summary>Owner: uses the ability in a slot on the selected target (when it needs one).</summary>
        public void TryCast(int slot)
        {
            if (!IsOwner) return;

            AbilityDefinition ability = GetAbility(slot);
            if (ability == null) return;
            if (pendingSlot >= 0 && pendingSlot != slot) CancelApproach();
            if (health.IsDead)
            {
                CastFailed?.Invoke(slot, CastFailure.Dead);
                return;
            }
            if (CooldownRemaining(slot) > 0f)
            {
                CastFailed?.Invoke(slot, CastFailure.NotReady);
                return;
            }
            if (!HasManaFor(ability))
            {
                CastFailed?.Invoke(slot, CastFailure.NoMana);
                return;
            }

            if (!ability.NeedsTarget)
            {
                RequestCastRpc(slot, false, default);
                return;
            }

            Health target = PlayerTargeting.LocalTarget;
            if (target == null || !target.IsSpawned || target.IsDead || !health.IsHostileTo(target))
            {
                CastFailed?.Invoke(slot, CastFailure.NoTarget);
                return;
            }
            if (Vector2.Distance(transform.position, target.transform.position) > ability.range)
            {
                if (movement == null)
                {
                    CastFailed?.Invoke(slot, CastFailure.OutOfRange);
                    return;
                }
                // Walk into range first; UpdateApproach uses the ability on arrival.
                pendingSlot = slot;
                pendingTarget = target;
                movement.SetDestination(target.transform.position);
                return;
            }

            RequestCastRpc(slot, true, target.NetworkObject);
            if (TryGetComponent(out PlayerTargeting targeting))
                targeting.StartAttack();
        }

        [Rpc(SendTo.Server)]
        private void RequestCastRpc(int slot, bool hasTarget, NetworkObjectReference targetReference, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            Health target = null;
            if (hasTarget && targetReference.TryGet(out NetworkObject targetObject))
                targetObject.TryGetComponent(out target);

            if (!ServerTryCast(slot, target, out CastFailure failure) && failure != CastFailure.None)
                CastFailedRpc(slot, failure);
        }

        [Rpc(SendTo.Owner)]
        private void CastFailedRpc(int slot, CastFailure reason)
        {
            CastFailed?.Invoke(slot, reason);
        }

        /// <summary>A message for the player about a failed cast.</summary>
        public static string Describe(CastFailure failure, AbilityDefinition ability)
        {
            switch (failure)
            {
                case CastFailure.Dead: return "You are dead";
                case CastFailure.NotReady: return ability != null ? $"{ability.displayName} is not ready" : "Not ready";
                case CastFailure.NoTarget: return "No enemy target";
                case CastFailure.OutOfRange: return "Out of range";
                case CastFailure.NoMana: return "Not enough mana";
                default: return "";
            }
        }

        // ---------- Server ----------

        /// <summary>Server: uses an ability if every check passes; otherwise returns false with a reason for the player.</summary>
        public bool ServerTryCast(int slot, Health target, out CastFailure failure)
        {
            failure = CastFailure.None;
            if (!IsServer) return false;

            AbilityDefinition ability = GetAbility(slot);
            if (ability == null) return false;
            if (health.IsDead)
            {
                failure = CastFailure.Dead;
                return false;
            }

            double now = Now;
            if (now < readyAt[slot] || now < globalReadyAt)
            {
                failure = CastFailure.NotReady;
                return false;
            }

            Vector2 self = AutoAttack.ServerPosition(this);
            Vector2 effectTarget = self;
            if (ability.NeedsTarget)
            {
                if (target == null || !target.IsSpawned || target.IsDead || !health.IsHostileTo(target))
                {
                    failure = CastFailure.NoTarget;
                    return false;
                }

                effectTarget = AutoAttack.ServerPosition(target);
                if (Vector2.Distance(self, effectTarget) > ability.range + autoAttack.RangeTolerance)
                {
                    failure = CastFailure.OutOfRange;
                    return false;
                }
            }

            if (mana != null && !mana.ServerSpend(ability.manaCost))
            {
                failure = CastFailure.NoMana;
                return false;
            }

            switch (ability.kind)
            {
                case AbilityKind.TargetDamage:
                    Hit(target, ability.damageMultiplier);
                    break;
                case AbilityKind.TargetArea:
                    HitArea(effectTarget, ability.radius, ability.damageMultiplier);
                    break;
                case AbilityKind.SelfArea:
                    HitArea(self, ability.radius, ability.damageMultiplier);
                    break;
                case AbilityKind.SelfHeal:
                    health.Heal(Mathf.Max(1, Mathf.RoundToInt(health.Max * ability.healFraction)), true);
                    break;
            }

            cooldownStartedAt[slot] = now;
            readyAt[slot] = now + ability.cooldown;
            globalReadyAt = now + GlobalCooldown;
            CastRpc(slot, now, effectTarget - self);
            return true;
        }

        private void Hit(Health target, float multiplier)
        {
            int damage = autoAttack.RollDamage(multiplier, out bool critical);
            target.ApplyDamage(damage, critical, health);
        }

        private void HitArea(Vector2 center, float radius, float multiplier)
        {
            // Copy first: damage can kill units and change the list.
            areaTargets.Clear();
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || !unit.IsSpawned || unit.IsDead || !health.IsHostileTo(unit)) continue;
                if (Vector2.Distance(center, AutoAttack.ServerPosition(unit)) <= radius)
                    areaTargets.Add(unit);
            }

            foreach (Health unit in areaTargets)
                Hit(unit, multiplier);
        }

        // ---------- Everyone ----------

        [Rpc(SendTo.Everyone)]
        private void CastRpc(int slot, double castTime, Vector2 offsetToTarget)
        {
            AbilityDefinition ability = GetAbility(slot);
            if (ability == null) return;

            if (!IsServer)
            {
                // The owner shows the cooldown from the server's cast time.
                cooldownStartedAt[slot] = castTime;
                readyAt[slot] = castTime + ability.cooldown;
                globalReadyAt = castTime + GlobalCooldown;
            }

            Vector2 self = transform.position;
            Vector2 target = self + offsetToTarget;
            if (offsetToTarget.sqrMagnitude > 0.0001f && IsOwner && movement != null)
                movement.FaceTowards(offsetToTarget);
            if (ability.kind != AbilityKind.SelfHeal && characterAnimator != null)
                characterAnimator.PlayOneShot(AutoAttack.AttackClip);

            switch (ability.kind)
            {
                case AbilityKind.TargetDamage:
                    if (ability.range > 1.5f) CombatEffects.Projectile(self, target, ability.effectColor);
                    else CombatEffects.Ring(target, 0.35f, ability.effectColor);
                    break;
                case AbilityKind.TargetArea:
                    CombatEffects.Projectile(self, target, ability.effectColor);
                    CombatEffects.Ring(target, ability.radius, ability.effectColor);
                    break;
                case AbilityKind.SelfArea:
                    CombatEffects.Ring(self, ability.radius, ability.effectColor);
                    break;
                case AbilityKind.SelfHeal:
                    CombatEffects.Ring(self, 0.5f, ability.effectColor);
                    break;
            }

            Cast?.Invoke(slot);
        }

        public override void OnNetworkSpawn()
        {
            Array.Clear(readyAt, 0, readyAt.Length);
            Array.Clear(cooldownStartedAt, 0, cooldownStartedAt.Length);
            globalReadyAt = 0.0;
        }
    }
}
