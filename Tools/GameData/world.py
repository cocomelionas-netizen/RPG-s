"""Generates Assets/_Project/Scripts/Editor/WorldMapData.cs and VendorData.cs from the source game's client data.

    python3 Tools/GameData/world.py <path to the source data checkout>

WorldMapData: the ground of the start region (map "fanadin", x 0-560, y 30-340) cell by cell, from maps/fanadin.map:
'.' walkable, '#' a wall, fence or rock (flag 0x20), 'T' a tree or another blocked object (flags 0x60), the edge
of the region blocked. Cells the .map file does not list are plain walkable ground.

VendorData: every vendor NPC of the region where the source game stands it (npc), with its name, subname and what it
sells (npc_vendor: item names, limits, restock; npc_vendor_random: the kinds of gear and the quality chances).

Cells: (floor(x) + 2, floor(y) - 28), like StartAreaData.
"""
import collections
import os
import sqlite3
import sys

sys.path.insert(0, os.path.dirname(__file__))
import map_file  # noqa: E402

X0, X1, Y0, Y1 = 0, 560, 30, 340
WIDTH, HEIGHT = 568, 318
TOWN = (524, 250)


def cell_of(x, y):
    return int(x) + 2, int(y) - 28


def source_of(cx, cy):
    return cx - 2, cy + 28


def main(root):
    out_dir = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Scripts', 'Editor')
    size, _textures, flags = map_file.read(os.path.join(root, 'maps', 'fanadin.map'))
    db = sqlite3.connect(os.path.join(root, 'game.db'))

    rows = []
    for cy in range(HEIGHT):
        row = []
        for cx in range(WIDTH):
            if cx == 0 or cy == 0 or cx == WIDTH - 1 or cy == HEIGHT - 1:
                row.append('#')
                continue
            flag = flags.get(source_of(cx, cy), 0)
            row.append('.' if not flag & 0x20 else 'T' if flag & 0x40 else '#')
        rows.append(row)

    vendors = read_vendors(db)
    check_reachable(db, rows, vendors)
    write_map(os.path.join(out_dir, 'WorldMapData.cs'), rows)
    write_vendors(os.path.join(out_dir, 'VendorData.cs'), vendors)


def read_vendors(db):
    q = lambda sql, *a: db.execute(sql, a).fetchall()
    fixed = collections.defaultdict(list)
    for npc, name, limit, restock, sell, ratio in q('select v.npc_entry, i.name, v.max_count, v.restock_cooldown, i.sell_price, '
                                                    'i.buy_cost_ratio from npc_vendor v join item_template i on i.entry = v.item '
                                                    'order by v.entry'):
        number = lambda v: int(v) if str(v).strip() not in ('', 'None') else 0
        if name not in [item[0] for item in fixed[npc]]:
            fixed[npc].append((name, number(limit), number(restock), number(sell), number(ratio) or 3))
    random = collections.defaultdict(list)
    for row in q('select npc_entry, equip_type, weapon_type, armor_preference, green_chance, blue_chance, gold_chance, '
                 'purple_chance, min_level, max_level from npc_vendor_random order by entry'):
        npc, *kind = [int(v) if str(v).strip() not in ('', 'None') else 0 for v in row]
        random[npc].append(tuple(kind))

    vendors = []
    for entry, name, subname, x, y in q('select t.entry, t.name, t.subname, n.position_x, n.position_y from npc n '
                                        'join npc_template t on t.entry = n.entry where n.map = 1 order by n.guid'):
        if not (X0 <= x <= X1 and Y0 <= y <= Y1) or (entry not in fixed and entry not in random):
            continue
        kinds = sorted(set(random.get(entry, [])))
        vendors.append({
            'entry': entry, 'name': name, 'title': (subname or '').strip(), 'cell': cell_of(x, y),
            'items': fixed.get(entry, []), 'gear': kinds,
        })
    return vendors


def check_reachable(db, rows, vendors):
    """Warns about spots that cannot be walked to from the start (4-way steps over walkable cells)."""
    start = cell_of(17, 106)
    seen = {start}
    queue = collections.deque([start])
    while queue:
        cx, cy = queue.popleft()
        for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
            if 0 <= nx < WIDTH and 0 <= ny < HEIGHT and (nx, ny) not in seen and rows[ny][nx] == '.':
                seen.add((nx, ny))
                queue.append((nx, ny))

    def near(cell):
        return any((cell[0] + dx, cell[1] + dy) in seen for dx in range(-2, 3) for dy in range(-2, 3))

    spots = [('vendor ' + v['name'], v['cell']) for v in vendors]
    for name, x, y in db.execute('select t.name, n.position_x, n.position_y from npc n join npc_template t on t.entry = n.entry '
                                 'where n.map = 1'):
        if X0 <= x <= X1 and Y0 <= y <= Y1:
            spots.append((name, cell_of(x, y)))
    for name, x, y in db.execute('select t.name, g.position_x, g.position_y from gameobject g join gameobject_template t '
                                 'on t.entry = g.entry where g.map = 1'):
        if X0 <= x <= X1 and Y0 <= y <= Y1:
            spots.append((name, cell_of(x, y)))
    lost = [(name, cell) for name, cell in spots if not near(cell)]
    print(f'walkable reached from the start: {len(seen)} cells; spots out of reach: {len(lost)} of {len(spots)}')
    for name, cell in lost[:40]:
        print('  ', name, cell)


def write_map(path, rows):
    with open(path, 'w', newline='\n') as f:
        f.write('// Generated by Tools/GameData/world.py from the source game\'s maps/fanadin.map (x 0-560, y 30-340).\n')
        f.write('namespace RPG.EditorTools\n{\n    internal static class WorldMapData\n    {\n')
        f.write('        public const char Walkable = \'.\';\n        public const char Wall = \'#\';\n        public const char Tree = \'T\';\n\n')
        f.write('        /// <summary>The ground cell by cell: Rows[y][x], y and x in this project\'s cells.</summary>\n')
        f.write('        public static readonly string[] Rows =\n        {\n')
        for row in rows:
            f.write('            "' + ''.join(row) + '",\n')
        f.write('        };\n    }\n}\n')


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def write_vendors(path, vendors):
    with open(path, 'w', newline='\n') as f:
        f.write('// Generated by Tools/GameData/world.py from the source game\'s game.db (npc, npc_template, npc_vendor,\n')
        f.write('// npc_vendor_random): every vendor of the start region, where it stands and what it sells.\n')
        f.write('using UnityEngine;\n\nnamespace RPG.EditorTools\n{\n    internal static class VendorData\n    {\n')
        f.write('        /// <summary>A kind of gear a merchant sells (npc_vendor_random): equip type, weapon type, armor preference\n')
        f.write('        /// (1 light, 2 heavy, 0 any), the chances of green, blue, gold and purple in percent and the levels (0: any).</summary>\n')
        f.write('        public struct GearKind\n        {\n            public int equip, weapon, armor, green, blue, gold, purple, minLevel, maxLevel;\n\n')
        f.write('            public GearKind(int equip, int weapon, int armor, int green, int blue, int gold, int purple, int minLevel, int maxLevel)\n            {\n')
        f.write('                this.equip = equip; this.weapon = weapon; this.armor = armor;\n')
        f.write('                this.green = green; this.blue = blue; this.gold = gold; this.purple = purple;\n')
        f.write('                this.minLevel = minLevel; this.maxLevel = maxLevel;\n            }\n        }\n\n')
        f.write('        public struct Vendor\n        {\n            public int entry;\n            public string name, title;\n')
        f.write('            public Vector3Int cell;\n            /// <summary>Item names, how many it holds (0: unlimited), restock seconds (0: none given), the source game\'s\n')
        f.write('            /// sell price and buy price (sell price times its buy cost ratio).</summary>\n')
        f.write('            public (string item, int limit, int restock, int sell, int buy)[] items;\n            public GearKind[] gear;\n        }\n\n')
        f.write('        public static readonly Vendor[] Vendors =\n        {\n')
        for v in vendors:
            f.write('            new Vendor\n            {\n')
            f.write(f'                entry = {v["entry"]}, name = {cs(v["name"])}, title = {cs(v["title"])},\n')
            f.write(f'                cell = new Vector3Int({v["cell"][0]}, {v["cell"][1]}, 0),\n')
            if v['items']:
                items = ', '.join(f'({cs(n)}, {limit}, {restock // 1000}, {sell}, {sell * ratio})'
                                  for n, limit, restock, sell, ratio in v['items'])
                f.write(f'                items = new (string, int, int, int, int)[] {{ {items} }},\n')
            else:
                f.write('                items = System.Array.Empty<(string, int, int, int, int)>(),\n')
            if v['gear']:
                gear = ', '.join('new GearKind(' + ', '.join(str(x) for x in g) + ')' for g in v['gear'])
                f.write(f'                gear = new[] {{ {gear} }},\n')
            else:
                f.write('                gear = System.Array.Empty<GearKind>(),\n')
            f.write('            },\n')
        f.write('        };\n    }\n}\n')


if __name__ == '__main__':
    main(sys.argv[1])
