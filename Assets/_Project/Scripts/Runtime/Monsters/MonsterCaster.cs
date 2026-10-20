using System.Collections.Generic;
using RPG.Combat;
using RPG.Navigation;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Monsters
{
    /// <summary>
    /// Server: casts a monster's spells (<see cref="MonsterSpell"/>, the source game's npc_template spells) while it fights.
    /// Each spell is tried every its interval with its chance, then rests its cooldown; casting stands still and is cut off
    /// by stuns and silences. A primary spell (casters, archers) is cast whenever it is ready, from its range, instead of
    /// the weapon swing (<see cref="PreferredRange"/> keeps the monster that far). Everyone sees the cast's name and its
    /// bolt or ring.
    /// </summary>
    [RequireComponent(typeof(MonsterAI), typeof(Health), typeof(AutoAttack))]
    public class MonsterCaster : NetworkBehaviour
    {
        private const float MinTry = 0.4f;
        /// <summary>Source game amounts are measured against an ordinary dungeon elite's hit (its weapon value).</summary>
        private const float ReferenceHit = 400f;
        private const float MaxHealShare = 0.3f;

        [SerializeField] private MonsterSpell[] spells = System.Array.Empty<MonsterSpell>();

        private MonsterAI ai;
        private Health health;
        private AutoAttack attack;
        private MonsterMotion motion;
        private float[] nextTry = System.Array.Empty<float>();
        private float[] readyAt = System.Array.Empty<float>();
        private int casting = -1;
        private float castEndsAt;
        private Health castTarget;
        private readonly List<Health> hits = new List<Health>();

        /// <summary>The monster's spells (set by its spawner).</summary>
        public MonsterSpell[] Spells
        {
            get => spells;
            set
            {
                spells = value ?? System.Array.Empty<MonsterSpell>();
                nextTry = new float[spells.Length];
                readyAt = new float[spells.Length];
                casting = -1;
            }
        }

        public bool HasSpells => spells.Length > 0;

        /// <summary>Server: true while it stands casting.</summary>
        public bool IsCasting => casting >= 0;

        /// <summary>How far from its target it stays: its primary spell's range, or 0 (melee as usual).</summary>
        public float PreferredRange
        {
            get
            {
                foreach (MonsterSpell spell in spells)
                    if (spell != null && spell.primary && spell.IsRanged) return spell.range * 0.85f;
                return 0f;
            }
        }

        private void Awake()
        {
            ai = GetComponent<MonsterAI>();
            health = GetComponent<Health>();
            attack = GetComponent<AutoAttack>();
            motion = GetComponent<MonsterMotion>();
            if (nextTry.Length != spells.Length) Spells = spells;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || spells.Length == 0) return;
            Health target = ai.CurrentTarget;
            bool fighting = ai.State == MonsterState.Chase && target != null && !target.IsDead && !health.IsDead;
            bool silenced = health.Effects != null && health.Effects.IsSilenced;
            if (casting >= 0)
            {
                if (!fighting || silenced || castTarget == null || castTarget.IsDead)
                {
                    casting = -1;
                    return;
                }
                if (Time.time >= castEndsAt) Resolve();
                return;
            }
            if (!fighting || silenced) return;

            for (int i = 0; i < spells.Length; i++)
            {
                MonsterSpell spell = spells[i];
                if (spell == null || Time.time < nextTry[i] || Time.time < readyAt[i]) continue;
                nextTry[i] = Time.time + Mathf.Max(MinTry, spell.interval);
                if (Random.value * 100f >= spell.chance || !CanReach(spell, target)) continue;
                Begin(i, target);
                return;
            }
        }

        private bool CanReach(MonsterSpell spell, Health target)
        {
            Vector2 self = transform.position;
            Vector2 at = AutoAttack.ServerPosition(target);
            float reach;
            switch (spell.kind)
            {
                case MonsterSpellKind.Self:
                case MonsterSpellKind.Heal:
                    return true;
                case MonsterSpellKind.Area:
                    reach = spell.range > 0f ? spell.range : spell.radius;
                    break;
                case MonsterSpellKind.Strike:
                    reach = attack.Range + attack.RangeTolerance;
                    break;
                default:
                    reach = Mathf.Max(spell.range, attack.Range) + attack.RangeTolerance;
                    break;
            }
            return Vector2.Distance(self, at) <= reach && WorldNavigation.LineOfSight(self, at);
        }

        private void Begin(int index, Health target)
        {
            MonsterSpell spell = spells[index];
            castTarget = target;
            casting = index;
            castEndsAt = Time.time + spell.castSeconds;
            if (spell.castSeconds > 0.35f)
            {
                if (motion != null) motion.ServerStop();
                CastRpc(spell.name, spell.color, spell.castSeconds);
            }
            if (spell.castSeconds <= 0f) Resolve();
        }

        private void Resolve()
        {
            int index = casting;
            casting = -1;
            MonsterSpell spell = spells[index];
            readyAt[index] = Time.time + Mathf.Max(spell.cooldown, spell.primary ? 0.5f : 0f);
            Health target = castTarget;
            Vector2 self = transform.position;
            Vector2 at = target != null ? AutoAttack.ServerPosition(target) : self;
            if (spell.kind != MonsterSpellKind.Self && spell.kind != MonsterSpellKind.Heal && (target == null || !CanReach(spell, target))) return;
            if (spell.kind != MonsterSpellKind.Self && spell.kind != MonsterSpellKind.Heal) ai.ServerSpellLanded();

            switch (spell.kind)
            {
                case MonsterSpellKind.Self:
                    Apply(spell, health, true);
                    GlowRpc(self, spell.color);
                    break;
                case MonsterSpellKind.Heal:
                    Health friend = MostHurtFriend(spell.range);
                    int amount = Mathf.RoundToInt(friend.Max * Mathf.Min(MaxHealShare, spell.damageMultiplier > 0f ? spell.damageMultiplier : 0.15f));
                    friend.Heal(Mathf.Max(1, amount), true);
                    GlowRpc(friend.transform.position, spell.color);
                    break;
                case MonsterSpellKind.Area:
                    Vector2 center = spell.range > 0f ? at : self;
                    hits.Clear();
                    foreach (Health unit in Health.Spawned)
                    {
                        if (unit == null || !unit.IsSpawned || unit.IsDead || !health.IsHostileTo(unit)) continue;
                        Vector2 position = AutoAttack.ServerPosition(unit);
                        if (Vector2.Distance(center, position) <= spell.radius && WorldNavigation.LineOfSight(center, position)) hits.Add(unit);
                    }
                    foreach (Health unit in hits) Hit(spell, unit);
                    RingRpc(center, spell.radius, spell.color);
                    break;
                case MonsterSpellKind.Charge:
                    // Rushes to the target's side (as far as there is ground), then strikes it.
                    Vector2 to = at + (self - at).normalized * Mathf.Min(0.6f, attack.Range * 0.8f);
                    WorldNavigation map = WorldNavigation.At(self);
                    if ((map == null || map.IsWalkable(to)) && motion != null) motion.ServerTeleport(to);
                    Hit(spell, target);
                    ProjectileRpc(self, to, spell.color);
                    break;
                default:
                    Hit(spell, target);
                    if (spell.kind != MonsterSpellKind.Strike) ProjectileRpc(self, at, spell.color);
                    break;
            }
        }

        /// <summary>Its damage (if any) and its effects on one enemy.</summary>
        private void Hit(MonsterSpell spell, Health unit)
        {
            float multiplier = spell.kind == MonsterSpellKind.Strike || spell.kind == MonsterSpellKind.Shoot
                ? spell.damageMultiplier > 0f ? spell.damageMultiplier : 1f
                : spell.damageMultiplier;
            if (multiplier > 0f)
            {
                int damage = attack.RollDamage(multiplier, out bool critical);
                unit.ApplyDamage(damage, critical, health, spell.school);
            }
            Apply(spell, unit, false);
        }

        private void Apply(MonsterSpell spell, Health unit, bool onSelf)
        {
            foreach (MonsterSpellEffect effect in spell.effects)
            {
                if (effect == null) continue;
                Health on = effect.onCaster || onSelf ? health : unit;
                if (on == null || on.Effects == null || on.IsDead) continue;
                float value = effect.value;
                if (effect.scaled) value = attack.RollDamage(Mathf.Max(0.1f, effect.value), out _);
                bool harmful = on != health && health.IsHostileTo(on);
                on.Effects.ServerApply(spell.name, spell.color, effect.kind, value, effect.seconds, health, spell.school, true,
                    effect.tick > 0f ? effect.tick : 1f, effect.stacks, harmful);
            }
        }

        private Health MostHurtFriend(float range)
        {
            Health best = health;
            float lowest = health.Max > 0 ? (float)health.Current / health.Max : 1f;
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || unit == health || !unit.IsSpawned || unit.IsDead || health.IsHostileTo(unit) || unit.Faction != health.Faction) continue;
                if (Vector2.Distance(transform.position, unit.transform.position) > range) continue;
                float share = unit.Max > 0 ? (float)unit.Current / unit.Max : 1f;
                if (share >= lowest) continue;
                lowest = share;
                best = unit;
            }
            return best;
        }

        // ---------- Everyone ----------

        [Rpc(SendTo.Everyone)]
        private void CastRpc(string spellName, Color color, float seconds)
        {
            CombatTextLayer.ShowText(transform.position + Vector3.up * 1.1f, spellName, color, 0.75f);
        }

        [Rpc(SendTo.Everyone)]
        private void ProjectileRpc(Vector2 from, Vector2 to, Color color)
        {
            CombatEffects.Projectile(from, to, color);
        }

        [Rpc(SendTo.Everyone)]
        private void RingRpc(Vector2 center, float radius, Color color)
        {
            CombatEffects.Ring(center, radius, color);
        }

        [Rpc(SendTo.Everyone)]
        private void GlowRpc(Vector2 center, Color color)
        {
            CombatEffects.Ring(center, 0.6f, color);
        }
    }
}
