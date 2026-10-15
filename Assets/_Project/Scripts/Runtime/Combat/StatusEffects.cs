using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>What a status effect does (Dreadmyst's aura types that this project has).</summary>
    public enum StatusKind : byte
    {
        /// <summary>Cannot move, attack or cast.</summary>
        Stun,
        /// <summary>Cannot move.</summary>
        Root,
        /// <summary>Moves slower by Value percent.</summary>
        Slow,
        /// <summary>Takes Value damage of its school in all, split into ticks.</summary>
        DamageOverTime,
        /// <summary>Regains Value health in all, split into ticks.</summary>
        HealOverTime,
        /// <summary>Takes Value percent more (or less, negative) damage of its school, or of every school.</summary>
        DamageTaken,
        /// <summary>Deals Value percent more damage of its school, or of every school.</summary>
        DamageDealt,
        /// <summary>Strength, Agility, Intelligence, Courage and Willpower raised by Value percent.</summary>
        Attributes,
        /// <summary>Intelligence raised by Value points.</summary>
        Intellect,
        /// <summary>Spell critical rating raised by Value.</summary>
        SpellCritical,
        /// <summary>Value percent more chance to dodge.</summary>
        Dodge,
        /// <summary>Dodges the next hit (then it is gone).</summary>
        Evade,
        /// <summary>Absorbs Value damage (then it is gone).</summary>
        Absorb,
        /// <summary>Cannot move, attack or cast; any damage ends it.</summary>
        Incapacitate,
        /// <summary>Monsters do not notice the unit; dealing or taking damage ends it.</summary>
        Stealth,
        /// <summary>Regains Value percent of max mana when it runs out.</summary>
        ManaOnExpire,
        /// <summary>Cannot be stunned.</summary>
        StunImmunity,
    }

    /// <summary>One effect as everyone sees it (for icons): the server keeps the rest.</summary>
    public struct StatusView : INetworkSerializeByMemcpy, IEquatable<StatusView>
    {
        public StatusKind Kind;
        public FixedString32Bytes Name;
        public Color32 Color;
        public float Value;
        public double StartedAt;
        public double EndsAt;
        public byte Stacks;
        public bool Harmful;
        public DamageSchool School;
        public bool AnySchool;

        public bool Equals(StatusView other) =>
            Kind == other.Kind && Name.Equals(other.Name) && Value.Equals(other.Value) && StartedAt.Equals(other.StartedAt)
            && EndsAt.Equals(other.EndsAt) && Stacks == other.Stacks;

        public override bool Equals(object obj) => obj is StatusView other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Kind, Name.GetHashCode(), EndsAt);

        /// <summary>The effect's total (Value times its stacks).</summary>
        public float Total => Value * Mathf.Max(1, (int)Stacks);

        public bool Matches(DamageSchool school) => AnySchool || School == school;
    }

    /// <summary>
    /// Buffs and debuffs on a unit: stuns, roots, slows, damage and healing over time, damage and attribute changes
    /// (Dreadmyst's auras). The server applies, ticks and removes them; everyone sees them (for icons, and the owner
    /// stops moving while stunned).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class StatusEffects : NetworkBehaviour
    {
        public const float TickInterval = 1f;

        private NetworkList<StatusView> views;
        private Health health;
        private Mana mana;

        private class Active
        {
            public StatusView View;
            public Health Source;
            public float Interval;
            public double NextTick;
            public int MaxStacks;
            /// <summary>Damage or health still to tick, or what an absorb has left.</summary>
            public float Remaining;
            public int TicksLeft;
        }

        private readonly List<Active> active = new List<Active>();

        /// <summary>Raised on every peer when the effects changed (attributes may need recounting).</summary>
        public event Action Changed;

        private void Awake()
        {
            health = GetComponent<Health>();
            mana = GetComponent<Mana>();
            views = new NetworkList<StatusView>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            views.OnListChanged += OnViewsChanged;
            if (IsServer)
            {
                views.Clear();
                active.Clear();
            }
        }

        public override void OnNetworkDespawn()
        {
            views.OnListChanged -= OnViewsChanged;
        }

        private void OnViewsChanged(NetworkListEvent<StatusView> change) => Changed?.Invoke();

        private double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

        /// <summary>The effects on the unit now (on every peer).</summary>
        public IEnumerable<StatusView> Views
        {
            get
            {
                if (views == null) yield break;
                double now = Now;
                for (int i = 0; i < views.Count; i++)
                    if (views[i].EndsAt > now) yield return views[i];
            }
        }

        public bool Has(StatusKind kind)
        {
            foreach (StatusView view in Views)
                if (view.Kind == kind) return true;
            return false;
        }

        /// <summary>Has an effect of this kind with this name (a spell's own effect).</summary>
        public bool Has(StatusKind kind, string effectName)
        {
            foreach (StatusView view in Views)
                if (view.Kind == kind && view.Name.ToString() == effectName) return true;
            return false;
        }

        /// <summary>Stunned or incapacitated: cannot move, attack or cast.</summary>
        public bool IsDisabled => Has(StatusKind.Stun) || Has(StatusKind.Incapacitate);

        /// <summary>Cannot move (also while disabled).</summary>
        public bool IsRooted => IsDisabled || Has(StatusKind.Root);

        public bool IsStealthed => Has(StatusKind.Stealth);

        /// <summary>Movement speed multiplier: 0 when rooted, less when slowed (the strongest slow counts).</summary>
        public float SpeedMultiplier
        {
            get
            {
                if (IsRooted) return 0f;
                float slow = 0f;
                foreach (StatusView view in Views)
                    if (view.Kind == StatusKind.Slow) slow = Mathf.Max(slow, view.Total);
                return Mathf.Clamp01(1f - slow / 100f);
            }
        }

        /// <summary>Sum of the values of one kind (for example every Dodge buff), for a school when it matters.</summary>
        public float Total(StatusKind kind, DamageSchool? school = null)
        {
            float total = 0f;
            foreach (StatusView view in Views)
                if (view.Kind == kind && (!school.HasValue || view.Matches(school.Value))) total += view.Total;
            return total;
        }

        /// <summary>Multiplier of damage this unit takes of a school.</summary>
        public float DamageTakenMultiplier(DamageSchool school) => Mathf.Max(0f, 1f + Total(StatusKind.DamageTaken, school) / 100f);

        /// <summary>Multiplier of damage this unit deals of a school.</summary>
        public float DamageDealtMultiplier(DamageSchool school) => Mathf.Max(0f, 1f + Total(StatusKind.DamageDealt, school) / 100f);

        // ---------- Server ----------

        /// <summary>
        /// Server: puts an effect on the unit. The same effect (name and kind) again refreshes it, adding a stack when
        /// it stacks. Over-time effects split <paramref name="value"/> into ticks every <paramref name="interval"/>.
        /// False when it did not take (stun immunity).
        /// </summary>
        public bool ServerApply(string effectName, Color color, StatusKind kind, float value, float duration, Health source = null,
            DamageSchool school = DamageSchool.Physical, bool anySchool = false, float interval = 0f, int maxStacks = 1, bool harmful = true)
        {
            if (!IsServer || health.IsDead || duration <= 0f) return false;
            if ((kind == StatusKind.Stun || kind == StatusKind.Incapacitate) && Has(StatusKind.StunImmunity)) return false;

            double now = Now;
            var name = new FixedString32Bytes(effectName.Length > 29 ? effectName.Substring(0, 29) : effectName);
            Active effect = active.Find(a => a.View.Kind == kind && a.View.Name.Equals(name));
            if (effect == null)
            {
                effect = new Active();
                active.Add(effect);
                effect.View.Stacks = 1;
            }
            else if (effect.View.Stacks < Mathf.Max(1, maxStacks))
            {
                effect.View.Stacks++;
            }

            effect.View.Kind = kind;
            effect.View.Name = name;
            effect.View.Color = color;
            effect.View.Value = value;
            effect.View.StartedAt = now;
            effect.View.EndsAt = now + duration;
            effect.View.Harmful = harmful;
            effect.View.School = school;
            effect.View.AnySchool = anySchool;
            effect.Source = source;
            effect.MaxStacks = Mathf.Max(1, maxStacks);
            effect.Remaining = value;
            if (kind == StatusKind.DamageOverTime || kind == StatusKind.HealOverTime)
            {
                effect.Interval = interval > 0f ? interval : TickInterval;
                effect.TicksLeft = Mathf.Max(1, Mathf.RoundToInt(duration / effect.Interval));
                effect.NextTick = now + effect.Interval;
            }
            Publish();
            return true;
        }

        /// <summary>Server: removes harmful effects (Cleanse); <paramref name="count"/> of them, or all with 0.</summary>
        public int ServerRemoveHarmful(int count = 0)
        {
            if (!IsServer) return 0;
            int removed = 0;
            for (int i = active.Count - 1; i >= 0 && (count <= 0 || removed < count); i--)
            {
                if (!active[i].View.Harmful) continue;
                active.RemoveAt(i);
                removed++;
            }
            if (removed > 0) Publish();
            return removed;
        }

        /// <summary>Server: removes every effect of a kind (Teleport frees from stuns and roots).</summary>
        public void ServerRemove(StatusKind kind)
        {
            if (IsServer && active.RemoveAll(a => a.View.Kind == kind) > 0) Publish();
        }

        /// <summary>Server: everything ends (death, respawn).</summary>
        public void ServerClear()
        {
            if (!IsServer || active.Count == 0) return;
            active.Clear();
            Publish();
        }

        /// <summary>Server: an Evade effect takes the hit (and is used up). True when the hit is dodged.</summary>
        public bool ServerConsumeEvade()
        {
            int index = active.FindIndex(a => a.View.Kind == StatusKind.Evade && a.View.EndsAt > Now);
            if (index < 0) return false;
            active.RemoveAt(index);
            Publish();
            return true;
        }

        /// <summary>Server: shields soak up the damage first; returns what gets through.</summary>
        public int ServerAbsorb(int amount)
        {
            bool changed = false;
            double now = Now;
            for (int i = active.Count - 1; i >= 0 && amount > 0; i--)
            {
                Active shield = active[i];
                if (shield.View.Kind != StatusKind.Absorb || shield.View.EndsAt <= now) continue;
                int soaked = Mathf.Min(amount, Mathf.CeilToInt(shield.Remaining));
                amount -= soaked;
                shield.Remaining -= soaked;
                if (shield.Remaining <= 0f) active.RemoveAt(i);
                changed = true;
            }
            if (changed) Publish();
            return amount;
        }

        /// <summary>Server: damage breaks incapacitation and stealth.</summary>
        public void ServerOnDamaged()
        {
            if (active.RemoveAll(a => a.View.Kind == StatusKind.Incapacitate || a.View.Kind == StatusKind.Stealth) > 0) Publish();
        }

        /// <summary>Server: attacking breaks stealth.</summary>
        public void ServerOnAttacked()
        {
            if (active.RemoveAll(a => a.View.Kind == StatusKind.Stealth) > 0) Publish();
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || active.Count == 0) return;
            if (health.IsDead)
            {
                ServerClear();
                return;
            }

            double now = Now;
            bool changed = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                Active effect = active[i];
                if (effect.TicksLeft > 0 && now >= effect.NextTick)
                {
                    int amount = Mathf.Max(1, Mathf.RoundToInt(effect.Remaining / effect.TicksLeft));
                    effect.Remaining -= amount;
                    effect.TicksLeft--;
                    effect.NextTick += effect.Interval;
                    if (effect.View.Kind == StatusKind.DamageOverTime)
                        health.ApplyDamage(amount, false, effect.Source, effect.View.School, periodic: true);
                    else
                        health.Heal(amount, true);
                    if (health.IsDead) return;
                }
                if (i < active.Count && active[i] == effect && effect.View.EndsAt <= now)
                {
                    active.RemoveAt(i);
                    changed = true;
                    if (effect.View.Kind == StatusKind.ManaOnExpire && mana != null)
                        mana.ServerRestore(Mathf.RoundToInt(mana.Max * effect.View.Value / 100f));
                }
            }
            if (changed) Publish();
        }

        private void Publish()
        {
            views.Clear();
            foreach (Active effect in active) views.Add(effect.View);
        }
    }
}
