"""The source game's island (map "fanadin") in this project's cells, and the generator of StartAreaData.cs.

    python3 -I Tools/GameData/island.py <path to the source data checkout>

The source game draws a map isometric with its x axis running down-right and its y axis down-left, so north is up.
Unity's isometric grid draws cell (x, y) higher on screen as x + y grows, so a source position (x, y) is the cell
(597 - y, 688 - x): the same picture, the same way up. The source map's 13 x 13 zone blocks stay 13 x 13 blocks of cells:
source block (bx, by) is cell block (45 - by, 52 - bx).

StartAreaData: the whole island: where new characters start, the town, every monster (npc + npc_template, all but the
friendly ones) and container (gameobject, type 1 with a level), the waypoints (named by the place they stand in), the
zone of every block (zone_template) and the labels of the source game's world map (mapquester_map.png: the zone names
and level ranges, placed where its icons are).
"""
import math
import os
import sqlite3
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import names  # noqa: E402
import map_file  # noqa: E402

WIDTH, HEIGHT = 569, 690
BLOCK = map_file.ZONE_BLOCK
BLOCK_COLUMNS = -(-WIDTH // BLOCK)
BLOCK_ROWS = -(-HEIGHT // BLOCK)
START = (17, 106)
TOWN = (524, 250)

# The labels of the source game's world map: name, the line under it, where its icon is in mapquester_map.png (pixels).
# That picture is the island isometric: pixel = (614 + 0.855 (x - y), 16 + 0.4275 (x + y)).
MAP_LABELS = [
    ("St. Revere", "", (840, 350), True),
    ("Howler's Valley", "Level 5+", (580, 138), False),
    ("Trog Cave", "Level 3+", (770, 145), False),
    ("Fechmar's Stand", "Level 11+", (436, 187), False),
    ("Wanderer's Forest", "Level 13+", (865, 207), False),
    ("Atmar's Fall", "Lv25 Dungeons (Four Players)", (258, 240), False),
    ("Ruins of Atmar", "Level 15+", (985, 268), False),
    ("Morgall's Camp", "Level 16+", (718, 262), False),
    ("Kandorys Forecourt", "Contested (PvP)", (560, 290), False),
    ("Hillsfar Dale", "Level 25+", (286, 317), False),
    ("Deserters", "Level 18+", (408, 353), False),
    ("Kandorys Castle", "Level 20-25", (585, 340), False),
    ("Kandorys Beach", "Level 6+", (533, 420), False),
    ("Hollow Fields", "Level 8+", (722, 440), False),
    ("Burial Grounds", "Level 10+", (658, 523), False),
    ("Fireshear", "Level 25 PvP (Boss)", (1133, 335), False),
    ("Wyrm Thicket", "Level 25+ (PvP)", (990, 398), False),
]


def cell_of(x, y):
    """A source position in this project's cells."""
    return 597 - int(math.floor(y)), 688 - int(math.floor(x))


def source_of(cx, cy):
    """The source cell of one of this project's cells."""
    return 688 - cy, 597 - cx


def on_island(x, y):
    cx, cy = cell_of(x, y)
    return 1 <= cx < WIDTH - 1 and 1 <= cy < HEIGHT - 1


def picture_to_source(px, py):
    d = (px - 614) / 0.855
    e = (py - 16) / 0.4275
    return (d + e) / 2, (e - d) / 2


def zone_name(name):
    """The source game's zone name as shown here: its "Kandoryrs" misspelling fixed; the island itself has none."""
    if name is None or name.endswith(' Island') or name == 'Unknown':
        return None
    return name.replace('Kandoryrs', 'Kandorys')


def plain(text):
    """A source name in this project's words (for comments)."""
    return names.rename(text)


def cs(text):
    # Every name and text written goes through this project's names (names.py).
    text = names.rename(text)
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def v3(cell):
    return f'new Vector3Int({cell[0]}, {cell[1]}, 0)'


def main(root):
    db = sqlite3.connect(os.path.join(root, 'game.db'))
    db.row_factory = sqlite3.Row
    zones = map_file.read_zones(os.path.join(root, 'maps', 'fanadin.map'))
    names = {row['id']: zone_name(row['name']) for row in db.execute('select id, name from zone_template')}

    def zone_at(x, y):
        return names.get(zones.get((int(x) // BLOCK, int(y) // BLOCK)))

    def nearest_zone(x, y):
        best = None
        for (bx, by), zone in zones.items():
            if names.get(zone) is None:
                continue
            d = (bx * BLOCK + 6 - x) ** 2 + (by * BLOCK + 6 - y) ** 2
            if best is None or d < best[0]:
                best = (d, names[zone])
        return best[1]

    monsters = []
    for n in db.execute('select n.position_x x, n.position_y y, n.respawn_time, t.* from npc n join npc_template t '
                        'on t.entry = n.entry where n.map = 1 and t.faction != 1 order by n.guid'):
        if not on_island(n['x'], n['y']):
            continue
        elite = bool(n['bool_elite']) or bool(n['bool_boss'])
        respawn = float(min(n['respawn_time'] or 0, 600))
        monsters.append((cell_of(n['x'], n['y']), n['min_level'], elite, respawn, n['name'].strip()))
    monsters.sort(key=lambda m: m[0])

    # The world map's boss markers: every elite or boss, one marker for each group of the same name standing together
    # (within 40 cells), at the group's middle, with the group's lowest level.
    groups = []
    for cell, level, elite, _, name in monsters:
        if not elite:
            continue
        group = next((g for g in groups if g['name'] == name and any(math.dist(cell, c) < 40 for c in g['cells'])), None)
        if group is None:
            groups.append({'name': name, 'level': level, 'cells': [cell]})
        else:
            group['cells'].append(cell)
            group['level'] = min(group['level'], level)
    bosses = []
    for g in groups:
        middle = (sum(c[0] for c in g['cells']) / len(g['cells']), sum(c[1] for c in g['cells']) / len(g['cells']))
        nearest = min(g['cells'], key=lambda c: math.dist(c, middle))
        bosses.append((g['name'], g['level'], len(g['cells']), nearest))

    chests = []
    for o in db.execute('select o.position_x x, o.position_y y, o.respawn, g.name, g.data3 level, g.data4 from gameobject o '
                        'join gameobject_template g on g.entry = o.entry where o.map = 1 and g.type = 1 order by o.guid'):
        if not o['level'] or not on_island(o['x'], o['y']):
            continue
        kind = 1 if o['data4'] == 100 else 0
        chests.append((cell_of(o['x'], o['y']), kind, int(o['level']), float(min(o['respawn'] or 0, 600)), o['name']))

    labels = []
    for title, detail, picture, town in MAP_LABELS:
        x, y = picture_to_source(*picture)
        labels.append((title, detail, (x, y), town))

    places = []
    for o in db.execute('select o.position_x x, o.position_y y from gameobject o join gameobject_template g '
                        'on g.entry = o.entry where o.map = 1 and g.type = 2 order by o.guid'):
        near = [(math.hypot(lx - o['x'], ly - o['y']), title) for title, _, (lx, ly), _ in labels]
        distance, title = min(near)
        name = title if distance < 45 else zone_at(o['x'], o['y']) or nearest_zone(o['x'], o['y'])
        places.append((name, cell_of(o['x'], o['y'])))

    zone_list = sorted({n for n in names.values() if n is not None and any(names.get(z) == n for z in zones.values())})
    letters = {name: chr(ord('A') + i) for i, name in enumerate(zone_list)}
    block_rows = []
    for k in range(BLOCK_ROWS):
        row = ''
        for j in range(BLOCK_COLUMNS):
            name = names.get(zones.get((52 - k, 45 - j)))
            row += letters[name] if name else '.'
        block_rows.append(row)

    out = []
    w = out.append
    w('// Generated by Tools/GameData/island.py from the source game\'s game.db and maps/fanadin.map: the whole island in')
    w('// this project\'s cells (the source position (x, y) is the cell (597 - y, 688 - x)). Do not edit by hand.')
    w('using UnityEngine;')
    w('')
    w('namespace RPG.EditorTools')
    w('{')
    w('    internal static class StartAreaData')
    w('    {')
    w(f'        public const int Width = {WIDTH};')
    w(f'        public const int Height = {HEIGHT};')
    w(f'        /// <summary>Where new characters start: the source game\'s "start" teleport in Pilgrim\'s Refuge {START}.</summary>')
    w(f'        public static readonly Vector3Int Start = {v3(cell_of(*START))};')
    w(f'        /// <summary>The main town, St. Revere: the middle of its market {TOWN}.</summary>')
    w(f'        public static readonly Vector3Int Town = {v3(cell_of(*TOWN))};')
    w('')
    w('        /// <summary>Monster spots: cell, source-game level, elite (or boss), respawn seconds (capped at 10 minutes).</summary>')
    w('        public static readonly (Vector3Int cell, int level, bool elite, float respawn)[] Monsters =')
    w('        {')
    for cell, level, elite, respawn, name in monsters:
        w(f'            ({v3(cell)}, {level}, {"true" if elite else "false"}, {respawn:g}f), // {plain(name)}')
    w('        };')
    w('')
    w('        /// <summary>Containers: cell, kind (0 crate or barrel, 1 chest), level, respawn seconds (capped at 10 minutes).</summary>')
    w('        public static readonly (Vector3Int cell, int kind, int level, float respawn)[] Chests =')
    w('        {')
    for cell, kind, level, respawn, name in chests:
        w(f'            ({v3(cell)}, {kind}, {level}, {respawn:g}f), // {plain(name)}')
    w('        };')
    w('')
    w('        /// <summary>')
    w('        /// Waystones where the source game has its waypoints, named by the place they stand in. Ids are their index:')
    w('        /// only add at the end.')
    w('        /// </summary>')
    w('        public static readonly (string name, Vector3Int cell)[] Places =')
    w('        {')
    for name, cell in places:
        w(f'            ({cs(name)}, {v3(cell)}),')
    w('        };')
    w('')
    w('        /// <summary>The source game\'s zones (named areas) of the island.</summary>')
    w('        public static readonly string[] ZoneNames =')
    w('        {')
    for name in zone_list:
        w(f'            {cs(name)},')
    w('        };')
    w('')
    w(f'        /// <summary>Zone blocks of {BLOCK} x {BLOCK} cells: ZoneBlocks[y / {BLOCK}][x / {BLOCK}], \'A\' the first of ZoneNames, \'.\' none.</summary>')
    w(f'        public const int ZoneBlock = {BLOCK};')
    w('        public static readonly string[] ZoneBlocks =')
    w('        {')
    for row in block_rows:
        w(f'            "{row}",')
    w('        };')
    w('')
    w('        /// <summary>The labels of the source game\'s world map: name, the line under it, cell, a town (written larger).</summary>')
    w('        public static readonly (string name, string detail, Vector3Int cell, bool town)[] MapLabels =')
    w('        {')
    for title, detail, (x, y), town in labels:
        w(f'            ({cs(title)}, {cs(detail)}, {v3(cell_of(x, y))}, {"true" if town else "false"}),')
    w('        };')
    w('')
    w('        /// <summary>The elites and bosses for the world map: name, level, how many stand there, cell (one of them).</summary>')
    w('        public static readonly (string name, int level, int count, Vector3Int cell)[] Bosses =')
    w('        {')
    for name, level, count, cell in bosses:
        w(f'            ({cs(name)}, {level}, {count}, {v3(cell)}),')
    w('        };')
    w('    }')
    w('}')

    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Scripts', 'Editor',
                        'StartAreaData.cs')
    with open(path, 'w', newline='\n') as f:
        f.write('\n'.join(out) + '\n')
    print(f'{len(monsters)} monsters, {len(chests)} containers, {len(places)} waypoints, {len(zone_list)} zones')
    for name, cell in places:
        print('  waypoint', name, cell)


if __name__ == '__main__':
    main(sys.argv[1])
