using System.Collections.Generic;
using RPG.Combat;
using RPG.Monsters;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Dungeons
{
    /// <summary>
    /// Server: a boss's special attack. While fighting, it regularly marks the ground around itself with a
    /// pulsing ring and, after a short warning, slams it: every player still inside takes heavy damage.
    /// Players who watch the ring and step out take nothing.
    /// </summary>
    [RequireComponent(typeof(MonsterAI), typeof(Health))]
    public class BossMechanics : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float slamInterval = 9f;
        [Tooltip("Time from the start of a fight to the first slam.")]
        [SerializeField, Min(0f)] private float firstSlamDelay = 4f;
        [SerializeField, Min(0.1f)] private float warningSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float slamRadius = 2.2f;
        [SerializeField, Min(0)] private int minDamage = 25;
        [SerializeField, Min(0)] private int maxDamage = 35;

        private static readonly Color WarningColor = new Color(1f, 0.15f, 0.1f, 0.9f);

        private MonsterAI ai;
        private Health health;
        private MonsterCaster caster;
        private float nextSlamAt = -1f;
        private float slamLandsAt = -1f;
        private Vector2 slamCenter;
        private readonly List<Health> victims = new List<Health>();

        public float SlamRadius => slamRadius;
        public float WarningSeconds => warningSeconds;

        private void Awake()
        {
            ai = GetComponent<MonsterAI>();
            health = GetComponent<Health>();
            caster = GetComponent<MonsterCaster>();
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            // A slam that was already announced still lands.
            if (slamLandsAt >= 0f && Time.time >= slamLandsAt) Land();

            // A boss with the source game's own spells casts those instead.
            if (caster != null && caster.HasSpells) return;
            if (health.IsDead || ai.State != MonsterState.Chase)
            {
                nextSlamAt = -1f;
                return;
            }

            if (nextSlamAt < 0f) nextSlamAt = Time.time + firstSlamDelay;
            if (slamLandsAt < 0f && Time.time >= nextSlamAt) ServerSlamNow();
        }

        /// <summary>Server: starts a slam right away (the warning, then the hit).</summary>
        public void ServerSlamNow()
        {
            if (!IsServer) return;
            slamCenter = transform.position;
            slamLandsAt = Time.time + warningSeconds;
            nextSlamAt = Time.time + slamInterval;
            WarningRpc(slamCenter, slamRadius, warningSeconds);
        }

        private void Land()
        {
            slamLandsAt = -1f;
            if (health.IsDead) return;

            victims.Clear();
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || !unit.IsSpawned || unit.IsDead || unit.Faction != Faction.Player) continue;
                if (Vector2.Distance(AutoAttack.ServerPosition(unit), slamCenter) <= slamRadius) victims.Add(unit);
            }
            foreach (Health victim in victims)
                victim.ApplyDamage(Random.Range(minDamage, maxDamage + 1), false, health);

            SlamRpc(slamCenter, slamRadius);
        }

        [Rpc(SendTo.Everyone)]
        private void WarningRpc(Vector2 center, float radius, float seconds)
        {
            CombatEffects.Telegraph(center, radius, WarningColor, seconds);
        }

        [Rpc(SendTo.Everyone)]
        private void SlamRpc(Vector2 center, float radius)
        {
            CombatEffects.Ring(center, radius, WarningColor);
            var animator = GetComponent<RPG.Characters.CharacterAnimator>();
            if (animator != null) animator.PlayOneShot(AutoAttack.AttackClip);
        }
    }
}
