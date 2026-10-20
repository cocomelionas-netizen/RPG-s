using System;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Monsters;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// The source game's monster spells (MonsterSpellData, Tools/GameData/monster_spells.py) as <see cref="MonsterSpell"/>s:
    /// ranges in its yards (Yards), effect radii in cells (half a unit each, at most 6 units), damage as a multiple of the
    /// monster's own hit (its fixed amounts measured against an ordinary dungeon elite's hit, 400).
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const float MonsterSpellReferenceHit = 400f;

        private static readonly Dictionary<string, Color> SchoolColors = new Dictionary<string, Color>
        {
            { "Physical", new Color(0.9f, 0.85f, 0.75f) }, { "Frost", new Color(0.5f, 0.8f, 1f) }, { "Fire", new Color(1f, 0.5f, 0.15f) },
            { "Shadow", new Color(0.65f, 0.35f, 0.95f) }, { "Holy", new Color(1f, 0.92f, 0.5f) },
        };

        /// <summary>The spells a monster of this source name casts (none for most).</summary>
        private static MonsterSpell[] MonsterSpellsFor(string monsterName)
        {
            if (string.IsNullOrEmpty(monsterName) || !MonsterSpellData.ByMonster.TryGetValue(monsterName, out var entries))
                return Array.Empty<MonsterSpell>();
            var spells = new List<MonsterSpell>();
            foreach (var entry in entries)
            {
                if (!MonsterSpellData.Spells.TryGetValue(entry.spell, out var data)) continue;
                var kind = (MonsterSpellKind)Enum.Parse(typeof(MonsterSpellKind), data.kind);
                var school = (DamageSchool)Enum.Parse(typeof(DamageSchool), data.school);
                float multiplier = data.damage > 0f ? data.damage / MonsterSpellReferenceHit
                    : data.damage < 0f ? (kind == MonsterSpellKind.Heal ? 0.15f : 1.2f)
                    : data.weapon > 0f ? data.weapon / 100f : 0f;
                var effects = new List<MonsterSpellEffect>();
                foreach (var e in data.effects)
                {
                    var status = (StatusKind)Enum.Parse(typeof(StatusKind), e.status);
                    bool amount = status == StatusKind.DamageOverTime || status == StatusKind.ManaBurn;
                    effects.Add(new MonsterSpellEffect
                    {
                        kind = status,
                        // Damage over time and mana burn: a multiple of the monster's hit (its own amount, or one hit).
                        value = amount ? (e.value > 0f ? e.value / MonsterSpellReferenceHit : 1f) : e.value,
                        scaled = amount,
                        seconds = Mathf.Min(e.seconds, 60f),
                        tick = e.tick,
                        stacks = Mathf.Max(1, e.stacks),
                        onCaster = e.self,
                    });
                }
                spells.Add(new MonsterSpell
                {
                    name = data.name.Replace(" +1", ""),
                    kind = kind,
                    school = school,
                    castSeconds = data.cast,
                    range = data.range * Yards,
                    radius = Mathf.Min(data.radius * 0.5f, 6f),
                    damageMultiplier = multiplier,
                    chance = entry.chance,
                    interval = entry.interval,
                    cooldown = entry.cooldown,
                    primary = entry.primary,
                    color = SchoolColors.TryGetValue(data.school, out Color color) ? color : Color.white,
                    effects = effects.ToArray(),
                });
            }
            return spells.ToArray();
        }
    }
}
