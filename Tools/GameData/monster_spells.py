"""Generates Assets/_Project/Scripts/Editor/MonsterSpellData.cs: the spells the source game's monsters cast.

    python3 -I Tools/GameData/monster_spells.py <path to the client data clone>

npc_template gives each monster a primary spell (cast instead of its weapon swing: casters and archers) and up to four
others, each tried every `interval` ms with `chance` % while fighting, then resting `cooldown` ms. spell_template's effects
are decoded into this project's terms:

- effect 1: damage of a school (data1: 1 physical, 2 frost, 3 fire, 4 shadow, 5 holy; data2: the amount, empty: scaled
  from the monster's own hit), around the caster when it has a radius;
- effect 14: a weapon strike of data2 % of the monster's hit; effect 31: a shot with the ranged weapon;
- effect 6: a heal (data1: the amount, empty: a share of max health); effect 37: a charge at the target;
- effect 3: an aura, by its type (data1): 1 damage over time (data2 school), 3 a mechanic (data2: 3 fear, 5 silence,
  7 slow by -data3 %, 8 stun), 5 attributes by data3 %, 11 slower by -data2 %, 14 damage dealt and 15 damage taken by
  data3 %, 20 mana burn, 23 healing taken by data2 %. Positive auras go on the caster.
Summons, dispels, pulls and immunities have nothing here and are left out.
"""
import os
import sqlite3
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Editor', 'MonsterSpellData.cs')

# The source game's school numbers (1-5) as this project's DamageSchool names.
SCHOOLS = {1: 'Physical', 2: 'Frost', 3: 'Fire', 4: 'Shadow', 5: 'Holy'}
MECHANICS = {3: 'Incapacitate', 5: 'Silence', 7: 'Slow', 8: 'Stun'}


def num(value, default=0.0):
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def flt(value):
    text = f'{value:.3f}'.rstrip('0').rstrip('.')
    return (text or '0') + 'f'


def decode(spell):
    """(kind, school, damage, weapon %, radius, effects) of a spell, or None when nothing of it can be used here."""
    kind, school, damage, weapon, radius, effects = None, SCHOOLS.get(int(num(spell['cast_school'], 1)), 'Physical'), 0.0, 0.0, 0.0, []
    duration = num(spell['duration']) / 1000.0
    tick = num(spell['interval']) / 1000.0
    stacks = max(1, int(num(spell['stack_amount'], 1)))
    for i in (1, 2, 3):
        effect = int(num(spell[f'effect{i}']))
        d1, d2, d3 = num(spell[f'effect{i}_data1']), num(spell[f'effect{i}_data2']), num(spell[f'effect{i}_data3'])
        r = num(spell[f'effect{i}_radius'])
        positive = int(num(spell[f'effect{i}_positive'])) == 1
        if effect == 1:
            kind = kind or ('Area' if r > 0 else 'Bolt')
            school = SCHOOLS.get(int(d1), school)
            damage = d2 if d2 > 0 else -1.0
            radius = max(radius, r)
        elif effect == 14:
            kind = kind or 'Strike'
            weapon = d2
        elif effect == 31:
            kind = kind or 'Shoot'
            weapon = 100.0
        elif effect == 6:
            kind = 'Heal'
            damage = d1 if d1 > 0 else -1.0
        elif effect == 37:
            kind = 'Charge'
        elif effect == 3:
            aura = int(d1)
            status, value = None, 0.0
            if aura == 1:
                status, value = 'DamageOverTime', d3 if d3 > 0 else -1.0
                school = SCHOOLS.get(int(d2), school)
            elif aura == 3 and int(d2) in MECHANICS:
                status = MECHANICS[int(d2)]
                value = -d3 if status == 'Slow' else 0.0
            elif aura == 5:
                status, value = 'Attributes', d3
            elif aura == 11:
                status, value = 'Slow', -d2
            elif aura == 14:
                status, value = 'DamageDealt', d3
            elif aura == 15:
                status, value = 'DamageTaken', d3
            elif aura == 20:
                status, value = 'ManaBurn', -1.0
            elif aura == 23:
                status, value = 'HealingTaken', d2
            if status is None or duration <= 0:
                continue
            if status == 'Slow' and any(e[0] == 'Slow' for e in effects):
                continue
            effects.append((status, value, duration, tick if status in ('DamageOverTime', 'ManaBurn') else 0.0, stacks, positive))
            if r > 0 and kind is None:
                kind, radius = 'Area', max(radius, r)
    if kind is None and effects:
        kind = 'Self' if all(e[5] for e in effects) else 'Hex'
    return None if kind is None else (kind, school, damage, weapon, radius, effects)


def main(steam):
    db = sqlite3.connect(os.path.join(steam, 'game.db'))
    db.row_factory = sqlite3.Row
    monsters = {}
    used = set()
    for t in db.execute('select * from npc_template where faction <> 1 order by entry'):
        name = (t['name'] or '').strip()
        if not name or name in monsters:
            continue
        entries = []
        primary = int(num(t['spell_primary']))
        if primary:
            entries.append((primary, 100.0, 0.0, 0.0, True))
        for i in (1, 2, 3, 4):
            spell = int(num(t[f'spell_{i}_id']))
            if spell:
                entries.append((spell, num(t[f'spell_{i}_chance'], 100), num(t[f'spell_{i}_interval'], 1500) / 1000.0,
                                num(t[f'spell_{i}_cooldown']) / 1000.0, False))
        if entries:
            monsters[name] = entries
            used.update(e[0] for e in entries)

    spells = {}
    for spell_id in sorted(used):
        row = db.execute('select * from spell_template where entry = ?', (spell_id,)).fetchone()
        decoded = decode(row) if row else None
        if decoded:
            spells[spell_id] = (row['name'].strip(), num(row['cast_time']) / 1000.0, num(row['range']), decoded)

    w = ['// Generated by Tools/GameData/monster_spells.py from the source game\'s game.db (npc_template spells, spell_template).',
         '// Do not edit by hand.',
         'using System.Collections.Generic;',
         '',
         'namespace RPG.EditorTools',
         '{',
         '    internal static class MonsterSpellData',
         '    {',
         '        /// <summary>',
         '        /// The spells monsters cast: name, kind (Bolt, Area, Strike, Shoot, Heal, Charge, Self, Hex), school, cast',
         '        /// seconds, range and radius in the source game\'s yards, damage (-1: scaled from the monster\'s hit),',
         '        /// weapon %, and its effects: status, value (-1: scaled), seconds, tick seconds, stacks, on the caster.',
         '        /// </summary>',
         '        public static readonly Dictionary<int, (string name, string kind, string school, float cast, float range, float radius,',
         '            float damage, float weapon, (string status, float value, float seconds, float tick, int stacks, bool self)[] effects)> Spells =',
         '            new Dictionary<int, (string, string, string, float, float, float, float, float, (string, float, float, float, int, bool)[])>',
         '        {']
    for spell_id, (name, cast, rng, (kind, school, damage, weapon, radius, effects)) in spells.items():
        fx = ', '.join(f'({cs(s)}, {flt(v)}, {flt(d)}, {flt(t)}, {n}, {"true" if p else "false"})' for s, v, d, t, n, p in effects)
        w.append(f'            [{spell_id}] = ({cs(name)}, {cs(kind)}, {cs(school)}, {flt(cast)}, {flt(rng)}, {flt(radius)}, {flt(damage)}, '
                 f'{flt(weapon)}, new (string, float, float, float, int, bool)[] {{ {fx} }}),')
    w += ['        };',
          '',
          '        /// <summary>Each monster\'s spells by its name: spell, chance %, try interval and cooldown in seconds, primary.</summary>',
          '        public static readonly Dictionary<string, (int spell, float chance, float interval, float cooldown, bool primary)[]> ByMonster =',
          '            new Dictionary<string, (int, float, float, float, bool)[]>',
          '        {']
    for name, entries in sorted(monsters.items()):
        kept = [e for e in entries if e[0] in spells]
        if not kept:
            continue
        items = ', '.join(f'({s}, {flt(c)}, {flt(i)}, {flt(cd)}, {"true" if p else "false"})' for s, c, i, cd, p in kept)
        w.append(f'            [{cs(name)}] = new[] {{ {items} }},')
    w += ['        };', '    }', '}']
    with open(OUT, 'w', newline='\n') as f:
        f.write('\n'.join(w) + '\n')
    print(f'{len(spells)} spells, {len(monsters)} monsters')


if __name__ == '__main__':
    main(sys.argv[1])
