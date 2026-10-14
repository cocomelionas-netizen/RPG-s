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
    /// Melee auto-attack resolved on the server. The owner only asks to attack a target;
    /// the server checks hostility, range and the attack interval, rolls the damage
    /// and tells everyone to play the attack animation.
    /// Stats are fixed for now and will come from classes and items later.
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
        [Tooltip("Extra range the server allows, because positions it sees are slightly out of date.")]
        [SerializeField, Min(0f)] private float rangeTolerance = 0.35f;

        private Health health;
        private CharacterAnimator characterAnimator;
        private PlayerMovement movement;
        private Health serverTarget;
        private double nextAttackTime;

        public float Range => range;
        public float AttackInterval => attackInterval;

        /// <summary>Server only: what this unit is attacking.</summary>
        public Health ServerTarget => serverTarget;

        /// <summary>Owner only: the server stopped the attack (target died, became invalid, or we died).</summary>
        public event Action AttackStopped;

        private void Awake()
        {
            health = GetComponent<Health>();
            characterAnimator = GetComponent<CharacterAnimator>();
            movement = GetComponent<PlayerMovement>();
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

            Vector2 from = ServerPosition(this);
            Vector2 to = ServerPosition(serverTarget);
            if (Vector2.Distance(from, to) > range + rangeTolerance) return;

            bool critical = Random.value < critChance;
            int damage = Random.Range(minDamage, maxDamage + 1) * (critical ? 2 : 1);
            nextAttackTime = now + attackInterval;

            serverTarget.ApplyDamage(damage, critical);
            PlayAttackRpc(to - from);
        }

        [Rpc(SendTo.Everyone)]
        private void PlayAttackRpc(Vector2 direction)
        {
            if (IsOwner && movement != null) movement.FaceTowards(direction);
            if (characterAnimator != null) characterAnimator.PlayOneShot(AttackClip);
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
