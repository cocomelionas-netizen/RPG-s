"""Generates Assets/_Project/Scripts/Editor/SourceDungeons.cs from Source's client data.

Usage: python3 -I Tools/SourceData/dungeons.py <path to the Source steam clone> > Assets/_Project/Scripts/Editor/SourceDungeons.cs

Takes every dungeon whose entrance is in the start region (and the dungeons entered from inside those): its plan
(walkable cells of maps/<map>.map), where players arrive, its exits, its monsters (npc + npc_template) and containers
(gameobject + gameobject_template), entry rules (dungeon_template: level, key, group size) and the bosses' loot tables
(loot), plus the keys and treasures those drop, the two key recipes, and the outdoor monsters that drop keys.
"""
import math
import os
import re
import sqlite3
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import Source_map  # noqa: E402

STEAM = sys.argv[1]
db = sqlite3.connect(os.path.join(STEAM, 'game.db'))
db.row_factory = sqlite3.Row

# This project's world is Source's "fanadin" from x 0-560, y 30-340, shifted like SourceStartArea.
WORLD_DX, WORLD_DY = 2, -28
WORLD_MAX_X, WORLD_MAX_Y = 564, 309


def world_cell(x, y):
    """A fanadin position in this project's world cells, pulled inside its edge when it lies beyond it."""
    cx = min(max(int(math.floor(x)) + WORLD_DX, 3), WORLD_MAX_X)
    cy = min(max(int(math.floor(y)) + WORLD_DY, 3), WORLD_MAX_Y)
    return cx, cy


def in_world(x, y):
    return 0 <= x <= 560 and 30 <= y <= 340


# Source items the dungeons need, under this project's asset names. Gems are this project's own (Refined: the
# middle tier, Flawless: the top); Source's amethysts and stat scrolls have nothing here and are left out.
ITEM_IDS = {
    1127: 'TowerKey', 1131: 'KeysToTheBarracks', 1134: 'UndercourseKey', 1135: 'DepletedKey', 1136: 'DaleGem',
    1139: 'DemiplaneKey', 1147: 'SanctumKey', 1153: 'InactiveDemiplaneKey', 1155: 'SalmastersKey',
    1156: 'KeysToStRevere', 1128: 'DivineAccessoryOrb', 1129: 'DivineArmorOrb', 1130: 'DivineWeaponOrb',
    927: 'MajorAccessoryOrb', 931: 'MajorArmorOrb', 1154: 'MagicDust', 1144: 'ArcaneEssence', 1143: 'MajorTreasuryGold',
}
GEMS = {
    919: 'Ruby', 920: 'Emerald', 921: 'Topaz', 922: 'Sapphire',
    1148: 'FlawlessRuby', 1149: 'FlawlessEmerald', 1150: 'FlawlessTopaz', 1151: 'FlawlessSapphire',
}
# Items a player clicks to put a recipe together (the others are only ingredients).
ACTIVATORS = {1135, 1136, 1153}

# Source's quality numbers: 3 green, 4 blue, 5 gold, 6 purple.
QUALITY = {0: 'Common', 1: 'Common', 2: 'Common', 3: 'Uncommon', 4: 'Rare', 5: 'Epic', 6: 'Legendary'}

# A normal level 25 monster (Zhendale Brute) has 7340 health; bosses are measured against it. Health then grows
# with level like this project's monsters (MonsterAI.HealthScaleBetween).
NORMAL_HEALTH_25 = 7340.0
# Weapon value of Source's ordinary dungeon elites (400); damage is measured against an elite's.
ELITE_WEAPON_VALUE = 400.0
ELITE_DAMAGE_SCALE = 1.5


def health_scale_between(a, b):
    return (1 + 0.15 * (b - 1)) / (1 + 0.15 * (a - 1))


def ident(name):
    return re.sub(r'[^A-Za-z0-9]', '', name.replace("'", ''))


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"').replace('\n', ' ') + '"'


def flt(value):
    text = f'{value:.3f}'.rstrip('0').rstrip('.')
    return (text if text else '0') + 'f'


def v3(cell):
    return f'new Vector3Int({cell[0]}, {cell[1]}, 0)'


maps = {row['id']: row for row in db.execute('select * from map')}
rules = {row['map']: row for row in db.execute('select * from dungeon_template')}
portals = list(db.execute(
    'select o.map, o.position_x x, o.position_y y, g.entry, g.name, g.required_item, g.data1 target, g.data3 tx, g.data4 ty '
    'from gameobject o join gameobject_template g on g.entry = o.entry where g.type = 4 order by o.guid'))

# Dungeons with an entrance in the start region, then those entered from inside them.
dungeon_maps = []
entrance_of = {}
for p in portals:
    if p['map'] == 1 and p['target'] not in (None, 1) and p['target'] in maps and p['target'] not in entrance_of:
        if p['entry'] == 51 or maps[p['target']]['name'] == 'arena':
            continue
        entrance_of[p['target']] = p
        dungeon_maps.append(p['target'])
changed = True
while changed:
    changed = False
    for p in portals:
        if p['map'] in dungeon_maps and p['target'] not in (None, 1) and p['target'] not in entrance_of \
                and p['required_item']:
            entrance_of[p['target']] = p
            dungeon_maps.append(p['target'])
            changed = True

used_loot = set()
dungeons = []
for map_id in dungeon_maps:
    entrance = entrance_of[map_id]
    info = maps[map_id]
    rule = rules[map_id] if map_id in rules else None
    size, _, flags = Source_map.read(os.path.join(STEAM, 'maps', info['name'] + '.map'))
    rows = [''.join('.' if Source_map.walkable(flags, x, y) else '#' for x in range(size)) for y in range(size)]
    name = entrance['name'].replace(' (Group)', '')

    def floor_cell(x, y):
        """The cell of a position, moved to the nearest walkable cell when it is not walkable."""
        cx, cy = int(math.floor(x)), int(math.floor(y))
        if Source_map.walkable(flags, cx, cy):
            return cx, cy
        best = None
        for r in range(1, 6):
            for dx in range(-r, r + 1):
                for dy in range(-r, r + 1):
                    if Source_map.walkable(flags, cx + dx, cy + dy):
                        d = dx * dx + dy * dy
                        if best is None or d < best[0]:
                            best = (d, (cx + dx, cy + dy))
            if best:
                return best[1]
        print(f'// warning: no floor near {x},{y} in {info["name"]}', file=sys.stderr)
        return cx, cy

    key = rule['required_item'] if rule and rule['required_item'] else entrance['required_item'] or None
    dungeon = {
        'id': ident(name), 'name': name, 'map': info['name'], 'map_id': map_id,
        'min_level': rule['required_level'] if rule else 1,
        'max_players': rule['max_players'] if rule else 4,
        'key': ITEM_IDS[key] if key else None,
        'rows': rows, 'size': size,
        'arrival': floor_cell(entrance['tx'], entrance['ty']),
        'entrance': entrance,
    }

    monsters = []
    for n in db.execute('select n.position_x x, n.position_y y, t.* from npc n join npc_template t on t.entry = n.entry '
                        'where n.map = ? order by n.guid', (map_id,)):
        level = n['min_level']
        loot = n['custom_loot'] if n['custom_loot'] and n['custom_loot'] > 0 else 0
        boss = bool(n['bool_boss']) and loot > 0
        elite = bool(n['bool_elite']) or bool(n['bool_boss'])
        health = 0.0
        if n['health'] and n['health'] > 0:
            health = n['health'] / (NORMAL_HEALTH_25 * health_scale_between(25, level))
        damage = 0.0
        if n['weapon_value'] and n['weapon_value'] > 0:
            damage = ELITE_DAMAGE_SCALE * n['weapon_value'] / ELITE_WEAPON_VALUE
        gold = n['custom_gold_ratio'] / 100.0 if n['custom_gold_ratio'] and n['custom_gold_ratio'] > 0 else 1.0

        def chance(column):
            return max(0.0, float(n[column] or 0)) / 100.0

        if loot:
            used_loot.add(loot)
        monsters.append((floor_cell(n['x'], n['y']), n['name'].strip(), level, elite, boss, health, damage, gold, loot,
                         chance('loot_blue_chance'), chance('loot_gold_chance'), chance('loot_purple_chance')))
    dungeon['monsters'] = monsters

    chests = []
    for o in db.execute('select o.position_x x, o.position_y y, g.name, g.flags, g.data3 level from gameobject o '
                        'join gameobject_template g on g.entry = o.entry where o.map = ? and g.type = 1 order by o.guid',
                        (map_id,)):
        # Source's container flags: 1 crate or barrel (gold only), 9 chest (locked), 25 strongbox (locked, more).
        kind = 2 if o['flags'] == 25 else 1 if o['flags'] == 9 else 0
        chests.append((floor_cell(o['x'], o['y']), kind, o['level'], re.sub(r'\s+[Ll]v\d+$', '', o['name'])))
    dungeon['chests'] = chests
    dungeons.append(dungeon)

by_map = {d['map_id']: d for d in dungeons}
for d in dungeons:
    entrance = d['entrance']
    d['parent'] = None if entrance['map'] == 1 else by_map[entrance['map']]['id']
    if entrance['map'] == 1:
        d['world'] = world_cell(entrance['x'], entrance['y'])
        d['moved'] = not in_world(entrance['x'], entrance['y'])
    else:
        d['parent_cell'] = (int(math.floor(entrance['x'])), int(math.floor(entrance['y'])))

for d in dungeons:
    # Where an exit leads, in the world: back to the entrance (or the outer dungeon's), or to its own target.
    def home(dungeon):
        while dungeon['parent']:
            dungeon = next(o for o in dungeons if o['id'] == dungeon['parent'])
        return dungeon

    exits = []
    for p in portals:
        if p['map'] != d['map_id'] or p['target'] is None or p['target'] in by_map and p['required_item']:
            continue
        cell = (int(math.floor(p['x'])), int(math.floor(p['y'])))
        # A row of portal cells is one portal.
        if any(abs(cell[0] - e[0][0]) <= 4 and abs(cell[1] - e[0][1]) <= 4 for e in exits):
            continue
        outer = home(d)
        if p['target'] == 1:
            original = outer['entrance']
            # Back at the entrance, also when Source's target lies beyond this world.
            near = abs(p['tx'] - original['x']) <= 8 and abs(p['ty'] - original['y']) <= 8 or not in_world(p['tx'], p['ty'])
            world = outer['world'] if near else world_cell(p['tx'], p['ty'])
            label = outer['name'] if near else None
        else:
            world, label = home(by_map[p['target']])['world'], None
        if label is None:
            label = p['name'] if p['name'] != 'Source Island' else 'Leave'
        exits.append((cell, world, label))
    d['exits'] = exits

# Loot tables of the bosses (and of the outdoor key droppers); identical rows count once.
outdoor = list(db.execute('select n.position_x x, n.position_y y, t.name, t.min_level, t.bool_elite, t.bool_boss, t.custom_loot, '
                          'n.respawn_time from npc n join npc_template t on t.entry = n.entry where n.map = 1 and '
                          't.custom_loot > 0 order by n.guid'))
KEY_LOOT = set()
for row in db.execute('select lootId from loot where item in (1131, 1136, 1153)'):
    KEY_LOOT.add(row['lootId'])
used_loot |= KEY_LOOT

loot_tables = {}
for loot_id in sorted(used_loot):
    rows, seen = [], set()
    for row in db.execute('select item, chance, count_min, count_max from loot where lootId = ? order by entry', (loot_id,)):
        item = ITEM_IDS.get(row['item']) or GEMS.get(row['item'])
        if item is None:
            continue
        low = row['count_min'] or 1
        high = row['count_max'] or low
        key = (item, row['chance'], low, high)
        if key in seen:
            continue
        seen.add(key)
        rows.append(key)
    loot_tables[loot_id] = rows

key_spots, extra_monsters = [], []
for n in outdoor:
    if n['custom_loot'] not in KEY_LOOT:
        continue
    if in_world(n['x'], n['y']):
        key_spots.append((world_cell(n['x'], n['y']), n['custom_loot'], n['name']))
    else:
        extra_monsters.append((n['x'], n['y'], n))

# Outdoor key droppers beyond the edge of this world come to its edge, each group keeping its shape, when nothing in
# this world drops their key (Source's far east, past St. Revere, stays out).
groups = {}
for x, y, n in extra_monsters:
    if x > 560 or any(loot_id == n['custom_loot'] for _, loot_id, _ in key_spots):
        continue
    groups.setdefault(n['custom_loot'], []).append((x, y, n))
moved = []
for loot_id, members in groups.items():
    if loot_id == 13:
        # The roaming bosses stand alone: each straight up to the edge.
        for x, y, n in members:
            moved.append((world_cell(x, y), n))
        continue
    lowest = max(y for _, y, _ in members)
    for x, y, n in members:
        cell = world_cell(x, y - (lowest - 336))
        moved.append((cell, n))

items = []
for entry in list(ITEM_IDS):
    row = db.execute('select * from item_template where entry = ?', (entry,)).fetchone()
    items.append((ITEM_IDS[entry], row['name'], QUALITY[row['quality'] or 0], row['sell_price'] or 0,
                  row['stack_count'] or 1, row['description'] or '', entry in ACTIVATORS))

recipes = []
for row in db.execute('select * from combine_items'):
    recipes.append((ITEM_IDS[row['output_item']], [ITEM_IDS[row['item_id']], ITEM_IDS[row['item_id_2']]]))
for row in db.execute('select * from crafting_recipes where result_item in (%s)' % ','.join(map(str, ITEM_IDS))):
    ingredients = [ITEM_IDS[row[f'item{i}']] for i in range(1, 6) if row[f'item{i}'] in ITEM_IDS]
    if row['result_item'] in (1139,):
        recipes.append((ITEM_IDS[row['result_item']], ingredients))

out = []
w = out.append
w('// Generated by Tools/SourceData/dungeons.py from Source\'s game.db and maps/*.map: the dungeons entered from')
w('// the start region (and from inside them), their plans, monsters, containers and entry rules, the bosses\' loot,')
w('// the keys and treasures, and the outdoor monsters that drop keys. Do not edit by hand.')
w('using System.Collections.Generic;')
w('using UnityEngine;')
w('')
w('namespace RPG.EditorTools')
w('{')
w('    internal static class SourceDungeons')
w('    {')
w('        internal sealed class Dungeon')
w('        {')
w('            /// <summary>Asset name.</summary>')
w('            public string Id;')
w('            public string Name;')
w('            /// <summary>Source\'s map file.</summary>')
w('            public string Map;')
w('            public int MinLevel;')
w('            public int MaxPlayers;')
w('            /// <summary>The key one player uses up to open it (an id in <see cref="Items"/>), or null.</summary>')
w('            public string Key;')
w('            /// <summary>Where its entrance stands in the world (when <see cref="Parent"/> is null).</summary>')
w('            public Vector3Int WorldEntrance;')
w('            /// <summary>Source has the entrance further south than this world reaches: it stands at the edge.</summary>')
w('            public bool MovedToEdge;')
w('            /// <summary>The dungeon whose plan holds the entrance (null: the world), and the entrance\'s cell there.</summary>')
w('            public string Parent;')
w('            public Vector3Int ParentCell;')
w('            /// <summary>Where players arrive.</summary>')
w('            public Vector3Int Arrival;')
w('            /// <summary>The plan, row y = index, column x: \'.\' walkable, \'#\' not.</summary>')
w('            public string[] Rows;')
w('            /// <summary>Portals out: cell, the world cell they lead to, name.</summary>')
w('            public (Vector3Int cell, Vector3Int world, string name)[] Exits;')
w('            /// <summary>')
w('            /// Monsters: cell, name, level, elite (Source elites and boss-flagged monsters), boss (with a loot table),')
w('            /// health as a multiple of a normal monster of its level (0: the usual), damage as a multiple of a normal')
w('            /// monster\'s (0: the usual), gold multiple, loot table (0: none), chances of a blue, gold and purple piece.')
w('            /// </summary>')
w('            public (Vector3Int cell, string name, int level, bool elite, bool boss, float health, float damage, float gold, int loot,')
w('                float blue, float goldQuality, float purple)[] Monsters;')
w('            /// <summary>Containers: cell, kind (0 crate or barrel, 1 chest, 2 strongbox), level, name.</summary>')
w('            public (Vector3Int cell, int kind, int level, string name)[] Chests;')
w('        }')
w('')
w('        /// <summary>Keys and treasures: id, name, quality, Source sell price, stack size, description, clicked to combine.</summary>')
w('        public static readonly (string id, string name, string quality, int sellPrice, int stack, string description, bool combines)[] Items =')
w('        {')
for i in items:
    w(f'            ({cs(i[0])}, {cs(i[1])}, {cs(i[2])}, {i[3]}, {i[4]}, {cs(i[5])}, {"true" if i[6] else "false"}),')
w('        };')
w('')
w('        /// <summary>Recipes: the result and every item it takes (one of each).</summary>')
w('        public static readonly (string result, string[] ingredients)[] Recipes =')
w('        {')
for result, ingredients in recipes:
    w(f'            ({cs(result)}, new[] {{ {", ".join(cs(x) for x in ingredients)} }}),')
w('        };')
w('')
w('        /// <summary>Loot tables by Source loot id: item id (in <see cref="Items"/> or a gem), chance in %, count.</summary>')
w('        public static readonly Dictionary<int, (string item, float chance, int min, int max)[]> Loot =')
w('            new Dictionary<int, (string item, float chance, int min, int max)[]>')
w('        {')
for loot_id, rows in loot_tables.items():
    w(f'            [{loot_id}] = new[]')
    w('            {')
    for item, chance, low, high in rows:
        w(f'                ({cs(item)}, {flt(chance)}, {low}, {high}),')
    w('            },')
w('        };')
w('')
w('        /// <summary>Outdoor monster spots (cells of SourceStartArea.Monsters) whose monster drops keys: loot table.</summary>')
w('        public static readonly (Vector3Int cell, int loot)[] KeyDroppers =')
w('        {')
for cell, loot_id, name in key_spots:
    w(f'            ({v3(cell)}, {loot_id}), // {name}')
w('        };')
w('')
w('        /// <summary>')
w('        /// Outdoor key droppers that Source has further south than this world reaches, moved up to its edge (a camp')
w('        /// keeps its shape): cell, name, level, elite, loot table, respawn seconds (capped at 10 minutes).')
w('        /// </summary>')
w('        public static readonly (Vector3Int cell, string name, int level, bool elite, int loot, float respawn)[] EdgeMonsters =')
w('        {')
for cell, n in moved:
    respawn = min(600.0, float(n['respawn_time'] or 60))
    elite = bool(n['bool_elite']) or bool(n['bool_boss'])
    w(f'            ({v3(cell)}, {cs(n["name"].strip())}, {n["min_level"]}, {"true" if elite else "false"}, {n["custom_loot"]}, {flt(respawn)}),')
w('        };')
w('')
w('        public static readonly Dungeon[] All =')
w('        {')
for d in dungeons:
    w('            new Dungeon')
    w('            {')
    w(f'                Id = {cs(d["id"])}, Name = {cs(d["name"])}, Map = {cs(d["map"])},')
    w(f'                MinLevel = {d["min_level"]}, MaxPlayers = {d["max_players"]}, Key = {cs(d["key"]) if d["key"] else "null"},')
    if d['parent']:
        w(f'                Parent = {cs(d["parent"])}, ParentCell = {v3(d["parent_cell"])},')
    else:
        w(f'                WorldEntrance = {v3(d["world"])}, MovedToEdge = {"true" if d["moved"] else "false"},')
    w(f'                Arrival = {v3(d["arrival"])},')
    exit_type = '(Vector3Int cell, Vector3Int world, string name)'
    monster_type = ('(Vector3Int cell, string name, int level, bool elite, bool boss, float health, float damage, float gold, '
                    'int loot, float blue, float goldQuality, float purple)')
    chest_type = '(Vector3Int cell, int kind, int level, string name)'

    def array(name, kind, lines):
        if not lines:
            w(f'                {name} = System.Array.Empty<{kind}>(),')
            return
        w(f'                {name} = new[]')
        w('                {')
        for line in lines:
            w('                    ' + line)
        w('                },')

    array('Exits', exit_type, [f'({v3(cell)}, {v3(world)}, {cs(label)}),' for cell, world, label in d['exits']])
    array('Monsters', monster_type, [
        f'({v3(m[0])}, {cs(m[1])}, {m[2]}, {"true" if m[3] else "false"}, {"true" if m[4] else "false"}, '
        f'{flt(m[5])}, {flt(m[6])}, {flt(m[7])}, {m[8]}, {flt(m[9])}, {flt(m[10])}, {flt(m[11])}),' for m in d['monsters']])
    array('Chests', chest_type, [f'({v3(c[0])}, {c[1]}, {c[2]}, {cs(c[3])}),' for c in d['chests']])
    w('                Rows = new[]')
    w('                {')
    for row in d['rows']:
        w(f'                    "{row}",')
    w('                },')
    w('            },')
w('        };')
w('    }')
w('}')
print('\n'.join(out))
