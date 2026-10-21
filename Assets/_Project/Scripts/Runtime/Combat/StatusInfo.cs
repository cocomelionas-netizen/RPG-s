using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// What the buffs and debuffs are, for the unit frames: each effect's icon and the source game's text for it (by the
    /// effect's name: a spell's, a monster spell's or an item's), an icon for each kind of effect and for damage over time
    /// of each school for the effects that have none of their own, and a line saying what the effect does with its
    /// numbers. Made by the scene builder (Resources/StatusInfo.asset).
    /// </summary>
    public class StatusInfo : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            [TextArea] public string description;
            public Sprite icon;
        }

        public Entry[] entries = Array.Empty<Entry>();
        [Tooltip("An icon for each StatusKind (by its number), for effects without one of their own.")]
        public Sprite[] kindIcons = Array.Empty<Sprite>();
        [Tooltip("An icon for damage over time of each DamageSchool (by its number).")]
        public Sprite[] damageIcons = Array.Empty<Sprite>();

        private static StatusInfo loaded;
        private static bool tried;
        private Dictionary<string, Entry> byName;

        /// <summary>The project's effect info, or null when there is none (tests, a scene built without it).</summary>
        public static StatusInfo Instance
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    loaded = Resources.Load<StatusInfo>("StatusInfo");
                }
                return loaded;
            }
        }

        private Entry Find(string effectName)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, Entry>();
                foreach (Entry entry in entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.name) && !byName.ContainsKey(entry.name))
                        byName[entry.name] = entry;
            }
            return effectName != null && byName.TryGetValue(effectName, out Entry found) ? found : null;
        }

        /// <summary>The effect's icon: its own, else its kind's (damage over time: its school's); null when there is none.</summary>
        public static Sprite IconOf(StatusView view)
        {
            StatusInfo info = Instance;
            if (info == null) return null;
            Entry entry = info.Find(view.Name.ToString());
            if (entry != null && entry.icon != null) return entry.icon;
            if (view.Kind == StatusKind.DamageOverTime && (int)view.School < info.damageIcons.Length && info.damageIcons[(int)view.School] != null)
                return info.damageIcons[(int)view.School];
            int kind = (int)view.Kind;
            return kind < info.kindIcons.Length ? info.kindIcons[kind] : null;
        }

        /// <summary>The source game's text for the effect ("Stunned.", "Reduces fire resistance."); empty when there is none.</summary>
        public static string DescriptionOf(string effectName)
        {
            Entry entry = Instance != null ? Instance.Find(effectName) : null;
            return entry != null && entry.description != null ? entry.description : "";
        }

        /// <summary>What the effect does, with its numbers ("Moves 30% slower.", "Takes 45 fire damage over its duration.").</summary>
        public static string Effect(StatusView view)
        {
            float value = view.Total;
            string amount = Mathf.Abs(value).ToString("0");
            string school = view.AnySchool ? "" : view.MagicOnly ? "magic " : view.School == DamageSchool.Physical ? "physical " : view.School.ToString().ToLowerInvariant() + " ";
            string more = value >= 0f ? "more" : "less";
            switch (view.Kind)
            {
                case StatusKind.Stun: return "Stunned: cannot move, attack or cast.";
                case StatusKind.Root: return "Rooted: cannot move.";
                case StatusKind.Slow: return $"Moves {amount}% slower.";
                case StatusKind.DamageOverTime: return $"Takes {amount} {school}damage over its duration.";
                case StatusKind.HealOverTime: return $"Regains {amount} health over its duration.";
                case StatusKind.DamageTaken: return $"Takes {amount}% {more} {school}damage.";
                case StatusKind.DamageDealt: return $"Deals {amount}% {more} {school}damage.";
                case StatusKind.Attributes: return value >= 0f ? $"All attributes +{amount}%." : $"All attributes -{amount}%.";
                case StatusKind.Intellect: return $"Intelligence {Signed(value)}.";
                case StatusKind.SpellCritical: return $"Spell critical rating {Signed(value)}.";
                case StatusKind.Dodge: return $"{Signed(value)}% chance to dodge.";
                case StatusKind.Evade: return "Dodges the next hit.";
                case StatusKind.Absorb: return $"Absorbs the next {amount} damage.";
                case StatusKind.Incapacitate: return "Incapacitated: cannot move, attack or cast; any damage ends it.";
                case StatusKind.Stealth: return "Unseen by monsters; dealing or taking damage ends it.";
                case StatusKind.ManaOnExpire: return $"Regains {amount}% of max mana when it runs out.";
                case StatusKind.StunImmunity: return "Cannot be stunned.";
                case StatusKind.Pacify: return "Cannot swing a weapon.";
                case StatusKind.Haste: return $"Moves {amount}% faster.";
                case StatusKind.Immune: return "Takes no damage and no harmful effects.";
                case StatusKind.ManaBurn: return $"Loses {amount} mana over its duration.";
                case StatusKind.HealingTaken: return $"Receives {amount}% {more} healing.";
                case StatusKind.AttackSpeed: return $"Attacks {amount}% faster.";
                case StatusKind.ManaOverTime: return $"Regains {amount} mana over its duration.";
                case StatusKind.BlockRating: return $"Block rating {Signed(value)}.";
                case StatusKind.Silence: return "Silenced: cannot cast spells.";
                case StatusKind.StrengthBonus: return $"Strength {Signed(value)}.";
                case StatusKind.AgilityBonus: return $"Agility {Signed(value)}.";
                case StatusKind.WillpowerBonus: return $"Willpower {Signed(value)}.";
                case StatusKind.CourageBonus: return $"Courage {Signed(value)}.";
                default: return "";
            }
        }

        private static string Signed(float value) => value >= 0f ? $"+{value:0}" : $"{value:0}";

        /// <summary>The whole tooltip: name, buff or debuff, the source text, what it does, stacks and time left.</summary>
        public static string Tooltip(StatusView view, double now)
        {
            string name = view.Name.ToString();
            string color = view.Harmful ? "#ff6a55" : "#7ee07a";
            var text = new System.Text.StringBuilder();
            text.Append($"<b>{name}</b>  <size=12><color={color}>{(view.Harmful ? "Debuff" : "Buff")}</color></size>");
            string description = DescriptionOf(name);
            if (!string.IsNullOrEmpty(description)) text.Append($"\n<color=#e6dcc0>{description}</color>");
            string effect = Effect(view);
            if (!string.IsNullOrEmpty(effect) && effect != description) text.Append($"\n<color=#b8c8d8>{effect}</color>");
            if (view.Stacks > 1) text.Append($"\n<color=#b8b0a0>Stacks: {view.Stacks}</color>");
            double left = view.EndsAt - now;
            if (left < 36000) text.Append($"\n<color=#b8b0a0>{TimeLeft(left)} left</color>");
            return text.ToString();
        }

        public static string TimeLeft(double seconds)
        {
            if (seconds < 0) seconds = 0;
            if (seconds < 60) return $"{Math.Ceiling(seconds):0} s";
            if (seconds < 3600) return $"{Math.Floor(seconds / 60):0} min {Math.Ceiling(seconds % 60):0} s";
            return $"{Math.Floor(seconds / 3600):0} h {Math.Floor(seconds % 3600 / 60):0} min";
        }
    }
}
