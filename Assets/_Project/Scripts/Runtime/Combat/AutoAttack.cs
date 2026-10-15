using System;
using RPG.Characters;
using RPG.Networking;
using RPG.Player;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RPG.Combat
{
    /// <summary>
    /// Auto-attack resolved on the server. The owner only asks to attack a target;
    /// the server checks hostility, range and the attack interval, rolls the damage
    /// and tells everyone to play the attack animation (and a projectile for ranged attackers).
    /// Players get their stats from their class and level (<see cref="SetStats"/>), monsters from the prefab.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class AutoAttack : NetworkBehaviour
    {
        public const string AttackClip = "Attack";

        [SerializeField, Min(0)] private int minDamage = 8;
        [SerializeField, Min(0)] private int maxDamage = 12;
        [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;
        [SerializeField, Min(0.1f)] private float range = 0.9f;
        [SerializeField, Range(0f, 1f)] private float critChance = 0.1f;
        [Tooltip("The school of this unit's hits: a magic school is reduced by the target's resist of it.")]
        [SerializeField] private DamageSchool damageSchool = DamageSchool.Physical;
        [Tooltip("Extra range the server allows, because positions it sees are slightly out of date.")]
        [SerializeField, Min(0f)] private float rangeTolerance = 0.35f;
        [Tooltip("Ranged attackers shoot a projectile of this color; alpha 0 means melee.")]
        [SerializeField] private Color projectileColor = Color.clear;

        private Health health;
        private CharacterAnimator characterAnimator;
        private PlayerMovement movement;
        private Health serverTarget;
        private double nextAttackTime;

        public float Range => range;
        public float AttackInterval => attackInterval;
        public float RangeTolerance => rangeTolerance;
        public int MinDamage => minDamage;
        public int MaxDamage => maxDamage;
        public float CritChance => critChance;

        /// <summary>Server only: what this unit is attacking.</summary>
        public Health ServerTarget => serverTarget;

        /// <summary>Server only: an auto-attack landed: (target, damage).</summary>
        public event Action<Health, int> ServerHitLanded;

        /// <summary>Owner only: the server stopped the attack (target died, became invalid, or we died).</summary>
        public event Action AttackStopped;

        private void Awake()
        {
            health = GetComponent<Health>();
            characterAnimator = GetComponent<CharacterAnimator>();
            movement = GetComponent<PlayerMovement>();
        }

        /// <summary>
        /// Sets the attack stats (from class and level). Call it on every peer so the owner walks into the right range.
        /// </summary>
        public void SetStats(int newMinDamage, int newMaxDamage, float newInterval, float newRange, float newCritChance, Color newProjectileColor)
        {
            minDamage = Mathf.Max(0, newMinDamage);
            maxDamage = Mathf.Max(minDamage, newMaxDamage);
            attackInterval = Mathf.Max(0.1f, newInterval);
            range = Mathf.Max(0.1f, newRange);
            critChance = Mathf.Clamp01(newCritChance);
            projectileColor = newProjectileColor;
        }

        /// <summary>Server: scales all damage this unit deals (elite monsters hit harder).</summary>
        public float DamageScale { get; set; } = 1f;

        /// <summary>The school of this unit's auto-attacks (monsters may hit with frost, fire, shadow or holy).</summary>
        public DamageSchool School
        {
            get => damageSchool;
            set => damageSchool = value;
        }

        /// <summary>Server: critical chance of abilities (Spell Critical); the attack's own when not set.</summary>
        public float SpellCritChance { get; set; } = -1f;

        /// <summary>Server: multiplier of ability damage and heals (Willpower).</summary>
        public float AbilityPower { get; set; } = 1f;

        /// <summary>Server: one damage roll (auto-attack damage times <paramref name="multiplier"/>, critical hits double it).</summary>
        public int RollDamage(float multiplier, out bool critical)
        {
            return RollDamage(multiplier, critChance, out critical);
        }

        /// <summary>Server: one ability's damage roll: with Willpower and the spell critical chance.</summary>
        public int RollAbilityDamage(float multiplier, out bool critical)
        {
            return RollDamage(multiplier * AbilityPower, SpellCritChance >= 0f ? SpellCritChance : critChance, out critical);
        }

        private int RollDamage(float multiplier, float chance, out bool critical)
        {
            critical = Random.value < chance;
            int damage = Mathf.RoundToInt(Random.Range(minDamage, maxDamage + 1) * multiplier * DamageScale);
            return Mathf.Max(1, damage) * (critical ? 2 : 1);
        }

        // ---------- Owner ----------

        public void RequestAttack(Health target)
        {
            if (!IsOwner || target == null || !target.IsSpawned) return;
            RequestAttackRpc(target.NetworkObject);
        }

        public void RequestStop()
        {
            if (!IsOwner) return;
            RequestStopRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestAttackRpc(NetworkObjectReference target, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (target.TryGet(out NetworkObject targetObject) && targetObject.TryGetComponent(out Health targetHealth))
                ServerStartAttack(targetHealth);
        }

        [Rpc(SendTo.Server)]
        private void RequestStopRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            ServerStopAttack(false);
        }

        [Rpc(SendTo.Owner)]
        private void AttackStoppedRpc()
        {
            AttackStopped?.Invoke();
        }

        // ---------- Server ----------

        /// <summary>Server only. Returns false when the target cannot be attacked.</summary>
        public bool ServerStartAttack(Health target)
        {
            if (!IsServer || !CanAttack(target)) return false;
            serverTarget = target;
            return true;
        }

        public void ServerStopAttack(bool notifyOwner)
        {
            if (!IsServer || serverTarget == null) return;
            serverTarget = null;
            if (notifyOwner) AttackStoppedRpc();
        }

        private bool CanAttack(Health target)
        {
            return target != null && target.IsSpawned && !target.IsDead && !health.IsDead && health.IsHostileTo(target);
        }

        private void Update()
        {
            if (!IsServer || serverTarget == null) return;

            if (!CanAttack(serverTarget))
            {
                ServerStopAttack(true);
                return;
            }

            double now = NetworkManager.ServerTime.Time;
            if (now < nextAttackTime) return;
            // Stunned or incapacitated units cannot swing.
            if (health.Effects != null && health.Effects.IsPacified) return;

            Vector2 from = ServerPosition(this);
            Vector2 to = ServerPosition(serverTarget);
            if (Vector2.Distance(from, to) > range + rangeTolerance) return;

            int damage = RollDamage(1f, out bool critical);
            nextAttackTime = now + attackInterval * (health.Effects != null ? health.Effects.AttackIntervalMultiplier : 1f);

            Health hitTarget = serverTarget;
            hitTarget.ApplyDamage(damage, critical, health, damageSchool);
            ServerHitLanded?.Invoke(hitTarget, damage);
            PlayAttackRpc(to - from);
        }

        [Rpc(SendTo.Everyone)]
        private void PlayAttackRpc(Vector2 direction)
        {
            if (IsOwner && movement != null) movement.FaceTowards(direction);
            if (characterAnimator != null) characterAnimator.PlayOneShot(AttackClip);
            if (projectileColor.a > 0f)
                CombatEffects.Projectile(transform.position, (Vector2)transform.position + direction, projectileColor);
        }

        /// <summary>Where the server believes a unit stands (players: last validated position).</summary>
        public static Vector2 ServerPosition(Component unit)
        {
            return unit.TryGetComponent(out PlayerNetworkMovement networkMovement)
                ? networkMovement.AuthoritativePosition
                : (Vector2)unit.transform.position;
        }
    }
}
