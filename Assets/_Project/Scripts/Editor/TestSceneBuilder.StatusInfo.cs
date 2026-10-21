using System.Collections.Generic;
using RPG.Combat;
using RPG.Items;
using RPG.Progression;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// What the buffs and debuffs are (Resources/StatusInfo.asset, shown by the unit frames): the source game's text for
    /// each effect (<see cref="AuraData"/>), the icon of the spell or item it comes from, and an icon for each kind of effect
    /// (and for damage over time of each school) for the monsters' effects, which have none of their own.
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const string StatusInfoPath = "Assets/_Project/Resources/StatusInfo.asset";

        /// <summary>The icon (TestSceneBuilder.Icons.cs) of each kind of effect, by StatusKind.</summary>
        private static readonly Dictionary<StatusKind, string> KindIcons = new Dictionary<StatusKind, string>
        {
            { StatusKind.Stun, "rock-orange-3" }, { StatusKind.Root, "vines-plain-2" }, { StatusKind.Slow, "ice-sky-1" },
            { StatusKind.DamageOverTime, "fireball-red-2" }, { StatusKind.HealOverTime, "heal-jade-3" },
            { StatusKind.DamageTaken, "evil-eye-red-2" }, { StatusKind.DamageDealt, "runes-orange-3" },
            { StatusKind.Attributes, "enchant-orange-3" }, { StatusKind.Intellect, "enchant-blue-3" },
            { StatusKind.SpellCritical, "light-blue-2" }, { StatusKind.Dodge, "wind-sky-2" }, { StatusKind.Evade, "wind-sky-2" },
            { StatusKind.Absorb, "protect-blue-3" }, { StatusKind.Incapacitate, "fog-blue-2" }, { StatusKind.Stealth, "fog-sky-2" },
            { StatusKind.ManaOnExpire, "link-blue-3" }, { StatusKind.StunImmunity, "haste-royal-3" },
            { StatusKind.Pacify, "explosion-orange-2" }, { StatusKind.Haste, "haste-fire-2" }, { StatusKind.Immune, "protect-jade-3" },
            { StatusKind.ManaBurn, "fire-arrows-jade-2" }, { StatusKind.HealingTaken, "heal-royal-3" },
            { StatusKind.AttackSpeed, "fire-arrows-sky-3" }, { StatusKind.ManaOverTime, "link-blue-3" },
            { StatusKind.BlockRating, "shielding-spirit-3" }, { StatusKind.Silence, "beam-magenta-2" },
            { StatusKind.StrengthBonus, "enchant-orange-3" }, { StatusKind.AgilityBonus, "wind-grasp-air-2" },
            { StatusKind.WillpowerBonus, "enchant-jade-3" }, { StatusKind.CourageBonus, "protect-red-3" },
            { StatusKind.Flavor, "light-air-fire-2" },
        };

        /// <summary>Damage over time by school: bleeding and poison, frost, fire, shadow, holy.</summary>
        private static readonly Dictionary<DamageSchool, string> DamageIcons = new Dictionary<DamageSchool, string>
        {
            { DamageSchool.Physical, "rip-acid-2" }, { DamageSchool.Frost, "ice-blue-2" }, { DamageSchool.Fire, "fireball-red-2" },
            { DamageSchool.Shadow, "horror-eerie-2" }, { DamageSchool.Holy, "light-royal-3" },
        };

        private static void CreateStatusInfo(ClassCatalog classes, ItemCatalog items)
        {
            var info = LoadOrCreateAsset<StatusInfo>(StatusInfoPath);
            var icons = new Dictionary<string, Sprite>();
            var descriptions = new Dictionary<string, string>();
            foreach (CharacterClassDefinition definition in classes.classes)
                if (definition != null)
                    foreach (AbilityDefinition spell in definition.abilities)
                        if (spell != null && spell.icon != null && !icons.ContainsKey(spell.displayName))
                            icons[spell.displayName] = spell.icon;
            // Attribute scrolls (and any other buff from an item) are named after the item.
            foreach (ItemDefinition item in items.items)
                if (item != null && item.use == ItemUse.Buff && !icons.ContainsKey(item.displayName))
                {
                    if (item.icon != null) icons[item.displayName] = item.icon;
                    if (!string.IsNullOrEmpty(item.description)) descriptions[item.displayName] = item.description;
                }
            foreach (KeyValuePair<string, string> aura in AuraData.Descriptions)
                descriptions[aura.Key] = aura.Value;

            var names = new SortedSet<string>(icons.Keys);
            names.UnionWith(descriptions.Keys);
            var entries = new List<StatusInfo.Entry>();
            foreach (string name in names)
                entries.Add(new StatusInfo.Entry
                {
                    name = name,
                    description = descriptions.TryGetValue(name, out string text) ? text : "",
                    icon = icons.TryGetValue(name, out Sprite icon) ? icon : null,
                });
            info.entries = entries.ToArray();

            var kinds = (StatusKind[])System.Enum.GetValues(typeof(StatusKind));
            info.kindIcons = new Sprite[kinds.Length];
            foreach (StatusKind kind in kinds)
                info.kindIcons[(int)kind] = KindIcons.TryGetValue(kind, out string icon) ? Icon($"spell/{icon}") : null;
            var schools = (DamageSchool[])System.Enum.GetValues(typeof(DamageSchool));
            info.damageIcons = new Sprite[schools.Length];
            foreach (DamageSchool school in schools)
                info.damageIcons[(int)school] = DamageIcons.TryGetValue(school, out string icon) ? Icon($"spell/{icon}") : null;
            EditorUtility.SetDirty(info);
        }
    }
}
