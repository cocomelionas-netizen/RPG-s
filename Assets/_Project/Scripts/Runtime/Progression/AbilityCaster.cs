using System;
using System.Collections.Generic;
using RPG.Characters;
using RPG.Combat;
using RPG.Controls;
using RPG.Items;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Networking;
using RPG.Player;
using RPG.World;
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
        /// <summary>Stunned or incapacitated.</summary>
        Disabled,
        /// <summary>The target lacks what the spell needs (Ice Shard: Chains of Ice; Kidney Shot: incapacitated).</summary>
        Requirement,
        /// <summary>Already casting something.</summary>
        Casting,
        /// <summary>Moved or was stunned while casting.</summary>
        Interrupted,
        /// <summary>Teleport: nowhere to stand there.</summary>
        BadPlace,
    }

    /// <summary>
    /// Casts the spells on the player's action bar (<see cref="Spellbook"/>; keys 2, 3, 4, 7, 8, 9, 0; key 1 is
    /// Attack, see <see cref="PlayerTargeting"/>). When the target is too far, the owner first walks into range. The
    /// owner asks; the server checks that the caster is alive and free, the spell is ready (its own cooldown and a
    /// short global cooldown), there is mana and the target is right, then casts it: at once, or after its cast time
    /// if the caster stands still. It deals weapon or spell damage, heals and puts on its effects at the spell's rank.
    /// Cooldowns shown to the owner start when the server confirms the cast.
    /// </summary>
    // Before PlayerMovement, so cancelling a walk into range never cancels the click that cancelled it.
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CharacterProgress), typeof(Health), typeof(AutoAttack))]
    public class AbilityCaster : NetworkBehaviour
    {
        public const float GlobalCooldown = 1f;
        private const int MaxSpells = 64;
        private const float ApproachRangeFactor = 0.9f;
        /// <summary>Moving further than this while casting breaks the cast off.</summary>
        private const float CastMoveTolerance = 0.2f;
        private const float CriticalMultiplier = 2f;

        private CharacterProgress progress;
        private Spellbook spellbook;
        private Health health;
        private Mana mana;
        private AutoAttack autoAttack;
        private CharacterAnimator characterAnimator;
        private PlayerMovement movement;
        private PlayerInputReader input;

        // Server time when each spell is ready again (server: authoritative, owner: from confirmations).
        private readonly double[] readyAt = new double[MaxSpells];
        private readonly double[] cooldownStartedAt = new double[MaxSpells];
        private double globalReadyAt;
        private readonly List<Health> areaTargets = new List<Health>();

        // Owner: a spell waiting until the player has walked into range.
        private int pendingSlot = -1;
        private Health pendingTarget;

        // Server: the spell being cast.
        private int castingSpell = -1;
        private Health castTarget;
        private Vector2 castPoint;
        private Vector2 castStartPosition;
        private double castEndsAt;

        /// <summary>Everyone: the spell being cast and when the cast started and ends (for the cast bar).</summary>
        public AbilityDefinition CastingAbility { get; private set; }
        private double castShownStart;
        private double castShownEnd;

        /// <summary>Owner only: the slot of the spell the player is walking into range for (-1 when none).</summary>
        public int PendingSlot => pendingSlot;

        /// <summary>Owner only: the server refused a cast or the client could not ask: (slot, reason).</summary>
        public event Action<int, CastFailure> CastFailed;

        /// <summary>Raised on every peer when a spell landed: its bar slot (-1 when it is not on the bar).</summary>
        public event Action<int> Cast;

        /// <summary>The spells of the class (all, known or not).</summary>
        public IReadOnlyList<AbilityDefinition> Abilities =>
            progress.Class != null ? progress.Class.abilities : Array.Empty<AbilityDefinition>();

        public Spellbook Spellbook => spellbook;

        private double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0.0;

        /// <summary>0..1 of the current cast (0 when not casting).</summary>
        public float CastProgress
        {
            get
            {
                if (CastingAbility == null || castShownEnd <= castShownStart) return 0f;
                return Mathf.Clamp01((float)((Now - castShownStart) / (castShownEnd - castShownStart)));
            }
        }

        private void Awake()
        {
            progress = GetComponent<CharacterProgress>();
            spellbook = GetComponent<Spellbook>();
            health = GetComponent<Health>();
            mana = GetComponent<Mana>();
            autoAttack = GetComponent<AutoAttack>();
            characterAnimator = GetComponent<CharacterAnimator>();
            movement = GetComponent<PlayerMovement>();
            input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (IsServer && IsSpawned) UpdateCasting();
            if (!IsSpawned || !IsOwner || input == null || !input.enabled) return;
            for (int slot = 0; slot < PlayerInputReader.AbilityKeyCount; slot++)
                if (input.AbilityPressed(slot)) UseSlot(slot);

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

        /// <summary>The spell index in a bar slot (-1 when empty).</summary>
        public int SpellInSlot(int slot) => spellbook != null ? spellbook.SpellInSlot(slot) : (slot >= 0 && slot < Abilities.Count ? slot : -1);

        /// <summary>The spell on a bar slot (null when empty).</summary>
        public AbilityDefinition GetAbility(int slot) => GetSpell(SpellInSlot(slot));

        private AbilityDefinition GetSpell(int spell) => spell >= 0 && spell < Abilities.Count && spell < MaxSpells ? Abilities[spell] : null;

        private int RankOf(int spell) => spellbook != null ? Mathf.Max(1, spellbook.RankOf(spell)) : 1;

        /// <summary>Mana a spell costs the caster now.</summary>
        public int ManaCost(AbilityDefinition ability) => ability != null ? ability.ManaCostAt(progress.Level) : 0;

        /// <summary>True when there is enough mana for the spell (always without a mana pool).</summary>
        public bool HasManaFor(AbilityDefinition ability) => ability == null || mana == null || mana.Has(ManaCost(ability));

        /// <summary>Seconds until the slot's spell can be used again (its own cooldown or the global one).</summary>
        public float CooldownRemaining(int slot) => SpellCooldownRemaining(SpellInSlot(slot));

        public float SpellCooldownRemaining(int spell)
        {
            if (spell < 0 || spell >= MaxSpells || !IsSpawned) return 0f;
            return (float)Math.Max(0.0, Math.Max(readyAt[spell], globalReadyAt) - Now);
        }

        /// <summary>0..1 share of the slot's current cooldown still left (for the cooldown overlay).</summary>
        public float CooldownFraction(int slot)
        {
            int spell = SpellInSlot(slot);
            if (spell < 0 || spell >= MaxSpells || !IsSpawned) return 0f;
            double now = Now;
            double end = Math.Max(readyAt[spell], globalReadyAt);
            if (end <= now) return 0f;

            double start = readyAt[spell] >= globalReadyAt ? cooldownStartedAt[spell] : globalReadyAt - GlobalCooldown;
            double length = end - start;
            return length > 0.0 ? Mathf.Clamp01((float)((end - now) / length)) : 0f;
        }

        // ---------- Owner ----------

        /// <summary>
        /// Owner: does what a bar slot holds, as its key or a click on it: Attack attacks the selected target, an item is
        /// used from the bag, a spell is cast.
        /// </summary>
        public void UseSlot(int slot)
        {
            if (!IsOwner) return;
            if (spellbook != null && spellbook.IsAttackSlot(slot))
            {
                if (TryGetComponent(out PlayerTargeting targeting)) targeting.AttackSelected();
                return;
            }
            int itemId = spellbook != null ? spellbook.ItemInSlot(slot) : 0;
            if (itemId > 0)
            {
                if (TryGetComponent(out Inventory inventory)) inventory.RequestUseItem(itemId);
                return;
            }
            TryCast(slot);
        }

        /// <summary>Owner: casts the spell in a bar slot on the selected target (when it needs one).</summary>
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
            if (health.Effects != null && health.Effects.IsSilenced)
            {
                CastFailed?.Invoke(slot, CastFailure.Disabled);
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

            Vector2 point = PointerWorldPosition();
            Health target = PlayerTargeting.LocalTarget;
            if (!ability.NeedsTarget)
            {
                bool friendly = target != null && target.IsSpawned && !target.IsDead && !health.IsHostileTo(target);
                RequestCastRpc(slot, friendly, friendly ? target.NetworkObject : default, point);
                if (ability.castTime > 0f && movement != null && movement.Destination.HasValue) movement.Stop();
                return;
            }

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
                // Walk into range first; UpdateApproach uses the spell on arrival.
                pendingSlot = slot;
                pendingTarget = target;
                movement.SetDestination(target.transform.position);
                return;
            }

            // Casting needs standing still.
            if (ability.castTime > 0f && movement != null && movement.Destination.HasValue) movement.Stop();
            RequestCastRpc(slot, true, target.NetworkObject, point);
            if (TryGetComponent(out PlayerTargeting targeting))
                targeting.StartAttack();
        }

        private Vector2 PointerWorldPosition()
        {
            Camera camera = Camera.main;
            if (camera == null || input == null) return transform.position;
            return camera.ScreenToWorldPoint(input.PointerPosition);
        }

        [Rpc(SendTo.Server)]
        private void RequestCastRpc(int slot, bool hasTarget, NetworkObjectReference targetReference, Vector2 point, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            Health target = null;
            if (hasTarget && targetReference.TryGet(out NetworkObject targetObject))
                targetObject.TryGetComponent(out target);

            if (!ServerTryCast(slot, target, point, out CastFailure failure) && failure != CastFailure.None)
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
                case CastFailure.Disabled: return "You can't do that right now";
                case CastFailure.Requirement:
                    if (ability != null && ability.requiresTargetOutOfCombat) return "The target is in combat";
                    if (ability != null && !string.IsNullOrEmpty(ability.requiredEffectName)) return $"The target needs {ability.requiredEffectName} first";
                    return ability != null && ability.requiredEffect == StatusKind.Slow ? "The target must be slowed (Chains of Ice)"
                        : ability != null && ability.requiredEffect == StatusKind.Incapacitate ? "The target must be incapacitated"
                        : "Invalid target";
                case CastFailure.Casting: return "You are already casting";
                case CastFailure.Interrupted: return "Interrupted";
                case CastFailure.BadPlace: return "You can't teleport there";
                default: return "";
            }
        }

        // ---------- Server ----------

        /// <summary>Server: casts the spell in a bar slot if every check passes (on the pointer's point for Teleport).</summary>
        public bool ServerTryCast(int slot, Health target, out CastFailure failure) =>
            ServerTryCast(slot, target, AutoAttack.ServerPosition(this), out failure);

        /// <summary>Server: casts the spell in a bar slot if every check passes; otherwise returns false with a reason.</summary>
        public bool ServerTryCast(int slot, Health target, Vector2 point, out CastFailure failure)
        {
            failure = CastFailure.None;
            if (!IsServer) return false;

            int spell = SpellInSlot(slot);
            AbilityDefinition ability = GetSpell(spell);
            if (ability == null) return false;
            if (health.IsDead)
            {
                failure = CastFailure.Dead;
                return false;
            }
            if (health.Effects != null && health.Effects.IsSilenced)
            {
                failure = CastFailure.Disabled;
                return false;
            }
            if (castingSpell >= 0)
            {
                failure = CastFailure.Casting;
                return false;
            }

            double now = Now;
            if (now < readyAt[spell] || now < globalReadyAt)
            {
                failure = CastFailure.NotReady;
                return false;
            }
            if (!CheckTarget(ability, ref target, point, out failure)) return false;
            if (mana != null && !mana.Has(ManaCost(ability)))
            {
                failure = CastFailure.NoMana;
                return false;
            }

            globalReadyAt = now + GlobalCooldown;
            if (ability.castTime > 0f)
            {
                castingSpell = spell;
                castTarget = target;
                castPoint = point;
                castStartPosition = AutoAttack.ServerPosition(this);
                castEndsAt = now + ability.castTime;
                CastStartedRpc(spell, now, castEndsAt);
                return true;
            }
            return Resolve(spell, target, point, out failure);
        }

        /// <summary>Server: whether the target fits the spell (friendly spells fall back on the caster).</summary>
        private bool CheckTarget(AbilityDefinition ability, ref Health target, Vector2 point, out CastFailure failure)
        {
            failure = CastFailure.None;
            Vector2 self = AutoAttack.ServerPosition(this);
            if (ability.NeedsTarget)
            {
                if (target == null || !target.IsSpawned || target.IsDead || !health.IsHostileTo(target))
                {
                    failure = CastFailure.NoTarget;
                    return false;
                }
                if (Vector2.Distance(self, AutoAttack.ServerPosition(target)) > ability.range + autoAttack.RangeTolerance)
                {
                    failure = CastFailure.OutOfRange;
                    return false;
                }
                bool missingEffect = ability.requiresTargetEffect && (target.Effects == null
                    || (string.IsNullOrEmpty(ability.requiredEffectName)
                        ? !target.Effects.Has(ability.requiredEffect)
                        : !target.Effects.Has(ability.requiredEffect, ability.requiredEffectName)));
                bool fighting = ability.requiresTargetOutOfCombat && target.TryGetComponent(out MonsterAI ai) && ai.CurrentTarget != null;
                if (missingEffect || fighting)
                {
                    failure = CastFailure.Requirement;
                    return false;
                }
                return true;
            }

            if (ability.kind == AbilityKind.FriendlyTarget)
            {
                bool usable = target != null && target.IsSpawned && !target.IsDead && !health.IsHostileTo(target)
                              && Vector2.Distance(self, AutoAttack.ServerPosition(target)) <= ability.range + autoAttack.RangeTolerance;
                if (!usable) target = health;
                return true;
            }

            if (ability.kind == AbilityKind.Teleport)
            {
                Vector2 to = self + Vector2.ClampMagnitude(point - self, ability.range);
                // Only onto walkable ground of a map: never past a dungeon's or the world's edge.
                WorldNavigation map = WorldNavigation.At(to);
                if (map == null || !map.IsWalkable(to))
                {
                    failure = CastFailure.BadPlace;
                    return false;
                }
            }
            target = null;
            return true;
        }

        private void UpdateCasting()
        {
            if (castingSpell < 0) return;
            bool moved = Vector2.Distance(AutoAttack.ServerPosition(this), castStartPosition) > CastMoveTolerance;
            bool stopped = health.IsDead || (health.Effects != null && health.Effects.IsSilenced);
            if (moved || stopped)
            {
                int interrupted = castingSpell;
                castingSpell = -1;
                CastEndedRpc();
                CastFailedRpc(spellbook != null ? spellbook.SlotOf(interrupted) : interrupted, CastFailure.Interrupted);
                return;
            }
            if (Now < castEndsAt) return;

            int spell = castingSpell;
            castingSpell = -1;
            CastEndedRpc();
            Health target = castTarget;
            if (!CheckTarget(GetSpell(spell), ref target, castPoint, out CastFailure failure)
                || !Resolve(spell, target, castPoint, out failure))
                CastFailedRpc(spellbook != null ? spellbook.SlotOf(spell) : spell, failure);
        }

        /// <summary>Server: the spell lands: mana, effects, cooldown.</summary>
        private bool Resolve(int spell, Health target, Vector2 point, out CastFailure failure)
        {
            failure = CastFailure.None;
            AbilityDefinition ability = GetSpell(spell);
            if (ability == null) return false;
            if (mana != null && !mana.ServerSpend(ManaCost(ability)))
            {
                failure = CastFailure.NoMana;
                return false;
            }

            int rank = RankOf(spell);
            AttributeSet attributes = progress.TotalAttributes;
            Vector2 self = AutoAttack.ServerPosition(this);
            Vector2 effectTarget = target != null ? AutoAttack.ServerPosition(target) : self;

            switch (ability.kind)
            {
                case AbilityKind.TargetDamage:
                    Strike(ability, rank, attributes, target);
                    break;
                case AbilityKind.Charge:
                    if (TryGetComponent(out PlayerNetworkMovement network))
                    {
                        // Rush to the target, stopping just short of it.
                        Vector2 away = self - effectTarget;
                        network.ServerTeleport(effectTarget + (away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.down) * 0.6f);
                    }
                    Strike(ability, rank, attributes, target);
                    break;
                case AbilityKind.TargetArea:
                    HitArea(ability, rank, attributes, effectTarget);
                    break;
                case AbilityKind.SelfArea:
                    HitArea(ability, rank, attributes, self);
                    break;
                case AbilityKind.SelfHeal:
                    health.Heal(Mathf.Max(1, Mathf.RoundToInt(health.Max * ability.healFraction * autoAttack.AbilityPower)), true);
                    ApplyEffects(ability, rank, attributes, health);
                    break;
                case AbilityKind.FriendlyTarget:
                case AbilityKind.Self:
                    Health friend = ability.kind == AbilityKind.Self || target == null ? health : target;
                    Help(ability, rank, attributes, friend);
                    effectTarget = AutoAttack.ServerPosition(friend);
                    break;
                case AbilityKind.FriendlyArea:
                    foreach (Health unit in Health.Spawned)
                        if (unit != null && unit.IsSpawned && !unit.IsDead && !health.IsHostileTo(unit)
                            && Vector2.Distance(self, AutoAttack.ServerPosition(unit)) <= ability.radius)
                            Help(ability, rank, attributes, unit);
                    break;
                case AbilityKind.Teleport:
                    Vector2 to = self + Vector2.ClampMagnitude(point - self, ability.range);
                    if (TryGetComponent(out PlayerNetworkMovement mover)) mover.ServerTeleport(to);
                    effectTarget = to;
                    ApplyEffects(ability, rank, attributes, health);
                    break;
                case AbilityKind.Retreat:
                    effectTarget = Leap(self, ability.leapDistance);
                    Help(ability, rank, attributes, health);
                    break;
                case AbilityKind.Recall:
                    Waystone town = Waystone.MainTown;
                    if (town != null && TryGetComponent(out PlayerNetworkMovement traveller))
                    {
                        traveller.ServerTeleport(town.ArrivalPoint);
                        effectTarget = town.ArrivalPoint;
                    }
                    break;
                case AbilityKind.Revive:
                    Health fallen = NearestFallenFriend(self, ability.range);
                    if (fallen != null && fallen.TryGetComponent(out PlayerRespawn respawn))
                    {
                        respawn.ServerRevive(ability.reviveHealth, ability.reviveMana);
                        effectTarget = AutoAttack.ServerPosition(fallen);
                    }
                    break;
            }
            // Teleport frees the caster; Wings of Freedom (a friendly spell) frees its target.
            if (ability.breaksControl && ability.kind != AbilityKind.FriendlyTarget && health.Effects != null)
                health.Effects.ServerFreeFromControl();

            double now = Now;
            cooldownStartedAt[spell] = now;
            readyAt[spell] = now + ability.cooldown;
            CastRpc(spell, now, effectTarget - self);
            return true;
        }

        /// <summary>Server: the spell's damage on one enemy (every hit), then its effects.</summary>
        private void Strike(AbilityDefinition ability, int rank, AttributeSet attributes, Health target)
        {
            if (target == null) return;
            for (int hit = 0; hit < ability.hits && !target.IsDead; hit++)
            {
                int damage = RollDamage(ability, rank, attributes, out bool critical);
                if (damage > 0) target.ApplyDamage(damage, critical, health, ability.school, ignoreDefenses: ability.ignoreDefenses);
            }
            if (!target.IsDead) ApplyEffects(ability, rank, attributes, target);
        }

        private int RollDamage(AbilityDefinition ability, int rank, AttributeSet attributes, out bool critical)
        {
            critical = false;
            if (ability.DealsWeaponDamage) return autoAttack.RollAbilityDamage(ability.WeaponPercentAt(rank) / 100f, out critical);
            if (ability.UsesSpellPower)
            {
                critical = UnityEngine.Random.value < Mathf.Max(0f, autoAttack.SpellCritChance);
                float amount = ability.SpellPowerAt(rank, attributes, UnityEngine.Random.value) * autoAttack.AbilityPower
                               * AbilityDefinition.SpellDamageScale * autoAttack.DamageScale;
                return Mathf.Max(1, Mathf.RoundToInt(amount * (critical ? CriticalMultiplier : 1f)));
            }
            // Old-style ability: auto-attack damage times its multiplier.
            if (ability.damageMultiplier > 0f && ability.effects.Length == 0) return autoAttack.RollAbilityDamage(ability.damageMultiplier, out critical);
            return 0;
        }

        /// <summary>Server: leaps away from the nearest enemy (or backwards), as far as there is ground; where it landed.</summary>
        private Vector2 Leap(Vector2 self, float distance)
        {
            Health from = autoAttack.ServerTarget;
            if (from == null || from.IsDead)
            {
                float nearest = float.MaxValue;
                foreach (Health unit in Health.Spawned)
                {
                    if (unit == null || !unit.IsSpawned || unit.IsDead || !health.IsHostileTo(unit)) continue;
                    float d = Vector2.Distance(self, AutoAttack.ServerPosition(unit));
                    if (d >= nearest) continue;
                    nearest = d;
                    from = unit;
                }
            }
            Vector2 away = from != null ? self - AutoAttack.ServerPosition(from) : Vector2.down;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.down;
            away.Normalize();
            WorldNavigation map = WorldNavigation.At(self);
            for (float step = distance; step > 0.2f; step -= 0.5f)
            {
                Vector2 to = self + away * step;
                if (map != null && !map.IsWalkable(to)) continue;
                if (TryGetComponent(out PlayerNetworkMovement mover)) mover.ServerTeleport(to);
                return to;
            }
            return self;
        }

        private Health NearestFallenFriend(Vector2 self, float range)
        {
            Health best = null;
            float bestDistance = range + autoAttack.RangeTolerance;
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || unit == health || !unit.IsSpawned || !unit.IsDead || health.IsHostileTo(unit)) continue;
                float d = Vector2.Distance(self, AutoAttack.ServerPosition(unit));
                if (d > bestDistance) continue;
                bestDistance = d;
                best = unit;
            }
            return best;
        }

        /// <summary>Server: heals and buffs a friendly unit.</summary>
        private void Help(AbilityDefinition ability, int rank, AttributeSet attributes, Health friend)
        {
            if (ability.removesHarmful && friend.Effects != null) friend.Effects.ServerRemoveHarmful(1);
            if (ability.breaksControl && ability.kind == AbilityKind.FriendlyTarget && friend.Effects != null) friend.Effects.ServerFreeFromControl();
            if (ability.restoreManaPercent > 0f && friend.TryGetComponent(out Mana friendMana))
                friendMana.ServerRestore(Mathf.RoundToInt(friendMana.Max * (ability.restoreManaPercent + ability.restoreManaPerRank * (rank - 1)) / 100f));
            int heal = 0;
            if (ability.healFraction > 0f) heal += Mathf.RoundToInt(friend.Max * ability.healFraction);
            if (ability.isHeal && ability.UsesSpellPower)
            {
                bool critical = UnityEngine.Random.value < Mathf.Max(0f, autoAttack.SpellCritChance);
                heal += Mathf.RoundToInt(ability.SpellPowerAt(rank, attributes, UnityEngine.Random.value) * autoAttack.AbilityPower
                                         * (critical ? CriticalMultiplier : 1f));
            }
            if (heal > 0) friend.Heal(heal, true);
            ApplyEffects(ability, rank, attributes, friend);
        }

        private void HitArea(AbilityDefinition ability, int rank, AttributeSet attributes, Vector2 center)
        {
            // Copy first: damage can kill units and change the list. Nearest first when the spell has a limit.
            areaTargets.Clear();
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || !unit.IsSpawned || unit.IsDead || !health.IsHostileTo(unit)) continue;
                if (Vector2.Distance(center, AutoAttack.ServerPosition(unit)) <= ability.radius)
                    areaTargets.Add(unit);
            }
            if (ability.maxTargets > 0 && areaTargets.Count > ability.maxTargets)
            {
                areaTargets.Sort((a, b) => Vector2.Distance(center, AutoAttack.ServerPosition(a))
                    .CompareTo(Vector2.Distance(center, AutoAttack.ServerPosition(b))));
                areaTargets.RemoveRange(ability.maxTargets, areaTargets.Count - ability.maxTargets);
            }

            foreach (Health unit in areaTargets)
                Strike(ability, rank, attributes, unit);
        }

        /// <summary>Server: puts the spell's effects on the target (or on the caster for caster effects).</summary>
        private void ApplyEffects(AbilityDefinition ability, int rank, AttributeSet attributes, Health target)
        {
            foreach (SpellEffect effect in ability.effects)
            {
                if (effect == null) continue;
                Health on = effect.onCaster ? health : target;
                if (on == null || on.Effects == null || on.IsDead) continue;
                float value = effect.ValueAt(rank, attributes);
                // Damage over time grows like the spell's own damage; healing, shields and mana over time are the source's.
                if (effect.kind == StatusKind.DamageOverTime)
                    value *= AbilityDefinition.SpellDamageScale * autoAttack.AbilityPower * autoAttack.DamageScale;
                else if (effect.kind == StatusKind.HealOverTime || effect.kind == StatusKind.Absorb
                         || effect.kind == StatusKind.ManaBurn || effect.kind == StatusKind.ManaOverTime)
                    value *= autoAttack.AbilityPower;
                bool harmful = health.IsHostileTo(on);
                on.Effects.ServerApply(ability.displayName, ability.effectColor, effect.kind, value, effect.duration, health,
                    effect.school, effect.anySchool && !effect.magicOnly, effect.interval, effect.maxStacks, harmful, effect.magicOnly);
            }
        }

        // ---------- Everyone ----------

        [Rpc(SendTo.Everyone)]
        private void CastStartedRpc(int spell, double start, double end)
        {
            CastingAbility = GetSpell(spell);
            castShownStart = start;
            castShownEnd = end;
            if (!IsServer) globalReadyAt = Math.Max(globalReadyAt, start + GlobalCooldown);
        }

        [Rpc(SendTo.Everyone)]
        private void CastEndedRpc()
        {
            CastingAbility = null;
        }

        [Rpc(SendTo.Everyone)]
        private void CastRpc(int spell, double castTime, Vector2 offsetToTarget)
        {
            AbilityDefinition ability = GetSpell(spell);
            if (ability == null) return;

            if (!IsServer)
            {
                // The owner shows the cooldown from the server's cast time.
                cooldownStartedAt[spell] = castTime;
                readyAt[spell] = castTime + ability.cooldown;
                if (ability.castTime <= 0f) globalReadyAt = Math.Max(globalReadyAt, castTime + GlobalCooldown);
            }

            Vector2 self = transform.position;
            Vector2 target = self + offsetToTarget;
            if (offsetToTarget.sqrMagnitude > 0.0001f && IsOwner && movement != null)
                movement.FaceTowards(offsetToTarget);
            if (!ability.IsFriendly && characterAnimator != null)
                characterAnimator.PlayOneShot(AutoAttack.AttackClip);

            switch (ability.kind)
            {
                case AbilityKind.TargetDamage:
                case AbilityKind.Charge:
                    if (ability.range > 1.5f && ability.kind != AbilityKind.Charge) CombatEffects.Projectile(self, target, ability.effectColor);
                    else CombatEffects.Ring(target, 0.35f, ability.effectColor);
                    break;
                case AbilityKind.TargetArea:
                    CombatEffects.Projectile(self, target, ability.effectColor);
                    CombatEffects.Ring(target, ability.radius, ability.effectColor);
                    break;
                case AbilityKind.SelfArea:
                case AbilityKind.FriendlyArea:
                    CombatEffects.Ring(self, ability.radius, ability.effectColor);
                    break;
                case AbilityKind.Teleport:
                    CombatEffects.Ring(target, 0.5f, ability.effectColor);
                    break;
                default:
                    CombatEffects.Ring(target, 0.5f, ability.effectColor);
                    break;
            }

            Cast?.Invoke(spellbook != null ? spellbook.SlotOf(spell) : spell);
        }

        public override void OnNetworkSpawn()
        {
            Array.Clear(readyAt, 0, readyAt.Length);
            Array.Clear(cooldownStartedAt, 0, cooldownStartedAt.Length);
            globalReadyAt = 0.0;
            castingSpell = -1;
            CastingAbility = null;
        }
    }
}
