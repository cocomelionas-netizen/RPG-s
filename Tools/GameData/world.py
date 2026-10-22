"""Generates Assets/_Project/Scripts/Editor/WorldMapData.cs and VendorData.cs from the source game's client data.

    python3 Tools/GameData/world.py <path to the source data checkout>

WorldMapData: the ground of the whole island (map "fanadin") cell by cell, from maps/fanadin.map:
'.' walkable, '#' a wall, fence or rock (flag 0x20), 'T' a tree or another blocked object (flags 0x60), 'W' water,
the edge of the region blocked. Cells the .map file does not list are plain walkable ground, except the lakes: the .map
file has no water, so it is read from the source game's world map picture (content/interface.zip,
mapquester_map.png, pixel = (614 + 0.855 (x - y), 16 + 0.4275 (x + y))): blue there is water here, as long as the
.map file does not make the cell walkable; specks smaller than 30 cells are left out.

VendorData: every vendor NPC of the island where the source game stands it (npc), with its name, subname and what it
sells (npc_vendor: item names, limits, restock; npc_vendor_random: the kinds of gear and the quality chances), its model
(npc_models: how it is dressed here), and the transmuter (subname "Transmutation").

Cells: island.cell_of, like StartAreaData.
"""
import collections
import io
import os
import sqlite3
import sys
import zipfile

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import names  # noqa: E402
import map_file  # noqa: E402
from island import HEIGHT, WIDTH, cell_of, on_island, source_of  # noqa: E402


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
    # Fords may only open water that the .map file left as open ground.
    open_ground = set()
    for cx, cy in read_water(root, flags):
        if rows[cy][cx] == '.':
            open_ground.add((cx, cy))
        rows[cy][cx] = 'W'

    vendors = read_vendors(db)
    carve_fords(rows, spots_of(db, vendors), open_ground)
    check_reachable(db, rows, vendors)
    write_map(os.path.join(out_dir, 'WorldMapData.cs'), rows)
    write_vendors(os.path.join(out_dir, 'VendorData.cs'), vendors)


def read_water(root, flags):
    """The lake cells: blue on the source game's world map, not walkable in the .map file, in groups of 30 or more."""
    with zipfile.ZipFile(os.path.join(root, 'content', 'interface.zip')) as archive:
        name = next(n for n in archive.namelist() if n.endswith('mapquester_map.png'))
        picture = Image.open(io.BytesIO(archive.read(name))).convert('RGBA')
    pixels = picture.load()

    def blue(x, y):
        sx, sy = 614 + 0.855 * (x - y), 16 + 0.4275 * (x + y)
        seen = hits = 0
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                px, py = int(round(sx + dx)), int(round(sy + dy))
                if 0 <= px < picture.width and 0 <= py < picture.height:
                    r, g, b, a = pixels[px, py]
                    seen += 1
                    hits += a > 100 and b > r + 10 and b >= g - 5
        return seen > 0 and hits * 2 > seen

    water = set()
    for cy in range(1, HEIGHT - 1):
        for cx in range(1, WIDTH - 1):
            x, y = source_of(cx, cy)
            if flags.get((x, y)) != 0 and blue(x + 0.5, y + 0.5):
                water.add((cx, cy))
    # Specks are left out: only groups of 30 or more cells are lakes.
    lakes, seen = set(), set()
    for start in water:
        if start in seen:
            continue
        group, queue = [], collections.deque([start])
        seen.add(start)
        while queue:
            cx, cy = queue.popleft()
            group.append((cx, cy))
            for nxt in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                if nxt in water and nxt not in seen:
                    seen.add(nxt)
                    queue.append(nxt)
        if len(group) >= 30:
            lakes.update(group)
    print(f'water: {len(lakes)} cells')
    return lakes


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
    for entry, name, subname, model, x, y in q('select t.entry, t.name, t.subname, m.name, n.position_x, n.position_y from npc n '
                                               'join npc_template t on t.entry = n.entry left join npc_models m on m.id = t.model_id '
                                               'where n.map = 1 order by n.guid'):
        # Vendors, and the transmuter (Reginald, "Transmutation"), who sells nothing but makes the crafting recipes.
        transmuter = (subname or '').strip() == 'Transmutation'
        if not on_island(x, y) or (entry not in fixed and entry not in random and not transmuter):
            continue
        kinds = sorted(set(random.get(entry, [])))
        vendors.append({
            'entry': entry, 'name': name, 'title': (subname or '').strip(), 'model': model or '', 'cell': cell_of(x, y),
            'items': fixed.get(entry, []), 'gear': kinds,
        })
    return vendors


def reached(rows):
    """The walkable cells reached from the start (4-way steps)."""
    start = cell_of(17, 106)
    seen = {start}
    queue = collections.deque([start])
    while queue:
        cx, cy = queue.popleft()
        for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
            if 0 <= nx < WIDTH and 0 <= ny < HEIGHT and (nx, ny) not in seen and rows[ny][nx] == '.':
                seen.add((nx, ny))
                queue.append((nx, ny))
    return seen


def near(seen, cell):
    return any((cell[0] + dx, cell[1] + dy) in seen for dx in range(-2, 3) for dy in range(-2, 3))


def carve_fords(rows, spots, open_ground):
    """
    Where the lakes cut a spot off that the .map file lets players reach (the source game's fords and shallows), the
    fewest water cells between it and the reached ground become walkable again, three cells wide (only water that the
    .map file left as open ground, never its walls or blocked cells).
    """
    carved = 0
    # The walk from the start is redone only after a ford opened something.
    seen = reached(rows)
    for name, cell in spots:
        cx, cy = cell
        if near(seen, cell) or not (0 <= cx < WIDTH and 0 <= cy < HEIGHT) or rows[cy][cx] == 'W':
            continue
        # Fewest water cells from the spot to reached ground (0-1 search: ground costs 0, water 1).
        cost = {cell: 0}
        parent = {}
        queue = collections.deque([cell])
        goal = None
        while queue:
            current = queue.popleft()
            if current in seen:
                goal = current
                break
            x, y = current
            for nxt in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                nx, ny = nxt
                if not (0 <= nx < WIDTH and 0 <= ny < HEIGHT) or rows[ny][nx] not in '.W':
                    continue
                if rows[ny][nx] == 'W' and nxt not in open_ground:
                    continue
                step = 1 if rows[ny][nx] == 'W' else 0
                if nxt not in cost or cost[current] + step < cost[nxt]:
                    cost[nxt] = cost[current] + step
                    parent[nxt] = current
                    (queue.append if step else queue.appendleft)(nxt)
        if goal is None:
            continue
        before = carved
        while goal in parent:
            x, y = goal
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if (x + dx, y + dy) in open_ground and rows[y + dy][x + dx] == 'W':
                        rows[y + dy][x + dx] = '.'
                        carved += 1
            goal = parent[goal]
        if carved != before:
            seen = reached(rows)
    print(f'fords: {carved} water cells made walkable')


def spots_of(db, vendors):
    """Every NPC, vendor and object of the island: (name, cell)."""
    spots = [('vendor ' + v['name'], v['cell']) for v in vendors]
    for name, x, y in db.execute('select t.name, n.position_x, n.position_y from npc n join npc_template t on t.entry = n.entry '
                                 'where n.map = 1'):
        if on_island(x, y):
            spots.append((name, cell_of(x, y)))
    for name, x, y in db.execute('select t.name, g.position_x, g.position_y from gameobject g join gameobject_template t '
                                 'on t.entry = g.entry where g.map = 1'):
        if on_island(x, y):
            spots.append((name, cell_of(x, y)))
    return spots


def check_reachable(db, rows, vendors):
    """Warns about spots that cannot be walked to from the start (4-way steps over walkable cells)."""
    seen = reached(rows)
    spots = spots_of(db, vendors)
    lost = [(name, cell) for name, cell in spots if not near(seen, cell)]
    print(f'walkable reached from the start: {len(seen)} cells; spots out of reach: {len(lost)} of {len(spots)}')
    for name, cell in lost[:40]:
        print('  ', name, cell)


def write_map(path, rows):
    with open(path, 'w', newline='\n') as f:
        f.write('// Generated by Tools/GameData/world.py from the source game\'s maps/fanadin.map (the whole island).\n')
        f.write('namespace RPG.EditorTools\n{\n    internal static class WorldMapData\n    {\n')
        f.write('        public const char Walkable = \'.\';\n        public const char Wall = \'#\';\n        public const char Tree = \'T\';\n'
                '        public const char Water = \'W\';\n\n')
        f.write('        /// <summary>The ground cell by cell: Rows[y][x], y and x in this project\'s cells.</summary>\n')
        f.write('        public static readonly string[] Rows =\n        {\n')
        for row in rows:
            f.write('            "' + ''.join(row) + '",\n')
        f.write('        };\n    }\n}\n')


def cs(text):
    # Every name and text written goes through this project's names (names.py).
    text = names.rename(text)
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def write_vendors(path, vendors):
    with open(path, 'w', newline='\n') as f:
        f.write('// Generated by Tools/GameData/world.py from the source game\'s game.db (npc, npc_template, npc_vendor,\n')
        f.write('// npc_vendor_random): every vendor of the island, where it stands and what it sells.\n')
        f.write('using UnityEngine;\n\nnamespace RPG.EditorTools\n{\n    internal static class VendorData\n    {\n')
        f.write('        /// <summary>A kind of gear a merchant sells (npc_vendor_random): equip type, weapon type, armor preference\n')
        f.write('        /// (1 light, 2 heavy, 0 any), the chances of green, blue, gold and purple in percent and the levels (0: any).</summary>\n')
        f.write('        public struct GearKind\n        {\n            public int equip, weapon, armor, green, blue, gold, purple, minLevel, maxLevel;\n\n')
        f.write('            public GearKind(int equip, int weapon, int armor, int green, int blue, int gold, int purple, int minLevel, int maxLevel)\n            {\n')
        f.write('                this.equip = equip; this.weapon = weapon; this.armor = armor;\n')
        f.write('                this.green = green; this.blue = blue; this.gold = gold; this.purple = purple;\n')
        f.write('                this.minLevel = minLevel; this.maxLevel = maxLevel;\n            }\n        }\n\n')
        f.write('        public struct Vendor\n        {\n            public int entry;\n            public string name, title, model;\n')
        f.write('            public Vector3Int cell;\n            /// <summary>Item names, how many it holds (0: unlimited), restock seconds (0: none given), the source game\'s\n')
        f.write('            /// sell price and buy price (sell price times its buy cost ratio).</summary>\n')
        f.write('            public (string item, int limit, int restock, int sell, int buy)[] items;\n            public GearKind[] gear;\n        }\n\n')
        f.write('        public static readonly Vendor[] Vendors =\n        {\n')
        for v in vendors:
            f.write('            new Vendor\n            {\n')
            f.write(f'                entry = {v["entry"]}, name = {cs(v["name"])}, title = {cs(v["title"])}, model = {cs(v["model"])},\n')
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
