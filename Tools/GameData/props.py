"""Generates Assets/_Project/Scripts/Editor/PropData.cs from the source game's client data.

    python3 -I Tools/GameData/props.py <path to the source data checkout>

The source game's island map (maps/fanadin.map) puts an object on some cells (the third layer of a cell: a texture
and a scale, negative when mirrored): buildings, tents, wells, fences, wall stones... and blocks the cells around them.
This writes where this project draws its own art for them (Tools/Art/buildings.py):

- Placements: each building or prop by its art key (BUILDING_KEYS, PROP_KEYS below), its cell (island.cell_of) and
  whether it is mirrored. Buildings whose free original the source game used (rubberduck's CC0 buildings, Bleed's
  tavern, timbered house, Galata tower, outpost and Medieval Building 06) keep the source game's own frame; the others
  get a stand-in of about the same size.
- WallStyles: how each blocked cell is drawn, so that every cell that blocks shows something. Wall cells ('#' in
  WorldMapData): under a building or prop (nothing: its picture is drawn there; only cells under its opaque pixels,
  read from Tools/Art/buildings.py's atlas, so run that first), a wooden or iron fence along x or y, low rubble (brick
  and stone wall pieces), a tall stone pillar (the source game's ruin walls, rock walls and cliffs) or a bush (blocked
  cells with nothing on them). Woods ('T'): 't' where the source game has a tree, ',' elsewhere (the builder fills
  them with trees and bushes).
- Lights: where the source game's map has a picture that gives light (sprite_light: campfires, street lights, torches,
  grave lanterns, fireflies), its cell, colour (RGBA), size and offset, for the lights the builder puts there at night.
- Sources: for every building key, the source game's image size and hotspot (sprite_hotspot: the pixel that stands on
  the cell), so the art is drawn as big and as placed as the source game draws it. Only sizes are read, never pixels.
"""
import glob
import io
import os
import re
import sqlite3
import struct
import sys
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from island import HEIGHT, WIDTH, cell_of  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Scripts', 'Editor',
                   'PropData.cs')

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Scripts', 'Editor',
                   'BuildingArtData.cs')

# Wall styles; woods with and without a source tree.
COVERED, FENCE_X, FENCE_Y, IRON_X, IRON_Y, RUBBLE, PILLAR, BUSH = 'x', 'f', 'g', 'i', 'j', 'r', 'p', 'b'
TREE, WOODS = 't', ','
TREE_WORDS = re.compile(r'tree|pine|oak|tilia|conifer|ulmus|carpinus|linden|poplar|alder|olive|guava|avicennia|'
                        r'eucalyptus|hornbeam|rosewood|almond|sakura|acacia|robinia|cherry|evergreen|cupress', re.I)


def building_key(texture):
    """The art key of a building texture of the source map, or None."""
    stem = texture[:-4] if texture.lower().endswith('.png') else texture
    if re.fullmatch(r'building[12]_128x64_0\d', stem) or re.fullmatch(r'b[123]_128x64_shaded_0\d', stem):
        return stem
    if re.fullmatch(r'medieval-tavern_\d0000', stem) or re.fullmatch(r'old_outpost_iso_\d+0000', stem):
        return stem
    match = re.fullmatch(r'timbered-house \((\d+)\)', stem)
    if match:
        return f'timbered-house_{match.group(1)}'
    if stem in ('Medieval_Building_06', 'galata_tower', 'church', 'house_green1', 'cabin_01', 'cabin_02',
                'medieval_house_01', 'medieval_house_03'):
        return stem
    return None


def prop_key(texture):
    """The art key of a small prop of the source map (Flare art), or None."""
    name = texture.lower()
    if name.startswith('tentroof') or 'floor' in name:
        return None
    for prefix, key in (('huntertent', 'tent'), ('tent', 'tent'), ('well_', 'well'), ('anvil', 'anvil'),
                        ('sign_post', 'signpost'), ('campfire', 'campfire'), ('woodpile', 'woodpile'),
                        ('stump_axe', 'stump'), ('stumpc', 'stump'), ('grave', 'grave'), ('statue', 'statue')):
        if name.startswith(prefix):
            return key
    return None


def wall_kind(texture):
    """What a wall cell's own object is: fence, iron, rubble, pillar or None."""
    name = texture.lower()
    if name.startswith('fencegrave'):
        return 'iron'
    if name.startswith(('fence_part', 'barricade', 'stakes')):
        return 'fence'
    if name.startswith(('brickwall', 'stonewall')):
        return 'rubble'
    if name.startswith(('ruintile', 'rockwall', 'cliff', 'spire', 'tileset_dungeon')):
        return 'pillar'
    return None


def read_map(path):
    """Cells as (x, y, flag, [(layer, texture name, scale)])."""
    data = open(path, 'rb').read()
    pos = 0

    def take(fmt):
        nonlocal pos
        value = struct.unpack_from(fmt, data, pos)
        pos += struct.calcsize(fmt)
        return value[0]

    def text():
        nonlocal pos
        end = data.index(b'\0', pos)
        value = data[pos:end].decode('latin1')
        pos = end + 1
        return value

    size = take('<i')
    textures = [text() for _ in range(take('<i'))]
    cells = []
    for _ in range(take('<i')):
        index = take('<i')
        flag = take('<B')
        layers = []
        for layer in range(3):
            if take('<B'):
                texture = take('<i')
                scale = struct.unpack('<f', struct.pack('<i', take('<i')))[0]
                if 0 <= texture < len(textures):
                    layers.append((layer, textures[texture], scale))
        cells.append((index % size, index // size, flag, layers))
    return cells


def source_images(root):
    """The size of every image in the source game's map texture archives (by file name)."""
    sizes = {}
    for archive in glob.glob(os.path.join(root, 'content', 'map', '*.zip')):
        try:
            zf = zipfile.ZipFile(archive)
        except zipfile.BadZipFile:
            continue
        for name in zf.namelist():
            base = name.split('/')[-1]
            if base.lower().endswith('.png') and base not in sizes:
                with zf.open(name) as f:
                    head = f.read(24)
                if head[:8] == b'\x89PNG\r\n\x1a\n':
                    sizes[base] = struct.unpack('>II', head[16:24])
    return sizes


def main(root):
    cells = read_map(os.path.join(root, 'maps', 'fanadin.map'))
    db = sqlite3.connect(os.path.join(root, 'game.db'))
    hotspots = {name: (x, y) for name, x, y in db.execute('select filename, x_offset, y_offset from sprite_hotspot')}
    sizes = source_images(root)
    glows = {name: (color, x, y, scale) for name, color, x, y, _radius, _ground, _top, scale
             in db.execute('select filename, color, x_offset, y_offset, intensity, bool_applyground, bool_applytop, scale '
                           'from sprite_light')}
    lights = []

    ours = {}
    walls = {}
    objects = {}
    placements = []
    sources = {}
    for x, y, flag, layers in cells:
        cx, cy = cell_of(x, y)
        if not (0 <= cx < WIDTH and 0 <= cy < HEIGHT):
            continue
        ours[(cx, cy)] = flag
        for layer, texture, scale in layers:
            if layer == 0:
                continue
            if layer == 2:
                objects.setdefault((cx, cy), []).append(texture)
            glow = glows.get(texture) or glows.get(texture + '.png')
            if glow:
                color, ox, oy, scale = glow
                rgba = tuple((int(color) >> shift) & 0xFF for shift in (24, 16, 8, 0))
                lights.append((cx, cy, rgba, float(scale or 1), int(ox or 0), int(oy or 0)))
            key = building_key(texture)
            if key:
                placements.append((key, cx, cy, scale < 0, True))
                w, h = sizes.get(texture, (0, 0))
                hx, hy = hotspots.get(texture, (w // 2, int(h * 0.85)))
                sources[key] = (texture, w, h, hx, hy)
                continue
            key = prop_key(texture)
            if key and layer == 2:
                placements.append((key, cx, cy, scale < 0, False))
                continue
            kind = wall_kind(texture)
            if kind and flag & 0x20 and not flag & 0x40 and (cx, cy) not in walls:
                walls[(cx, cy)] = kind

    def is_wall(c):
        # As WorldMapData's '#' (world.py): blocked, not a tree.
        flag = ours.get(c, 0)
        return bool(flag & 0x20) and not flag & 0x40

    covered = art_cover(placements)
    trees = {cell for cell, textures in objects.items() if any(TREE_WORDS.search(t) for t in textures)}

    def style(c):
        kind = walls.get(c)
        if c in covered and kind not in ('fence', 'iron'):
            return COVERED
        if kind in ('fence', 'iron'):
            same = lambda n: walls.get(n) == kind  # noqa: E731
            along_x = same((c[0] + 1, c[1])) or same((c[0] - 1, c[1]))
            along_y = same((c[0], c[1] + 1)) or same((c[0], c[1] - 1))
            x_line = along_x or not along_y
            return (FENCE_X if x_line else FENCE_Y) if kind == 'fence' else (IRON_X if x_line else IRON_Y)
        if kind == 'rubble':
            return RUBBLE
        if kind == 'pillar':
            return PILLAR
        return BUSH

    def is_woods(c):
        # As WorldMapData's 'T': blocked, a tree or another object.
        flag = ours.get(c, 0)
        return bool(flag & 0x20) and bool(flag & 0x40)

    rows = []
    for cy in range(HEIGHT):
        rows.append(''.join(style((cx, cy)) if is_wall((cx, cy)) else (TREE if (cx, cy) in trees else WOODS)
                            if is_woods((cx, cy)) else '.' for cx in range(WIDTH)))

    write(placements, rows, sources, lights)
    counts = {s: sum(r.count(s) for r in rows) for s in (COVERED, FENCE_X, FENCE_Y, IRON_X, IRON_Y, RUBBLE, PILLAR, BUSH, TREE, WOODS)}
    print(f'{len(lights)} lights')
    print(f'{sum(1 for p in placements if p[4])} buildings, {sum(1 for p in placements if not p[4])} props; walls {counts}')


def write(placements, rows, sources, lights):
    with open(OUT, 'w', newline='\n') as f:
        f.write('// Generated by Tools/GameData/props.py from the source game\'s maps/fanadin.map and game.db (sprite_hotspot):\n')
        f.write('// where its buildings, props and walls are, drawn with this project\'s own art (Tools/Art/buildings.py).\n')
        f.write('using UnityEngine;\n\nnamespace RPG.EditorTools\n{\n    internal static class PropData\n    {\n')
        f.write('        public const char Covered = \'x\', FenceX = \'f\', FenceY = \'g\', IronX = \'i\', IronY = \'j\', Rubble = \'r\', '
                'Pillar = \'p\', Bush = \'b\', Tree = \'t\', Woods = \',\';\n\n')
        f.write('        /// <summary>Buildings and props: art key (BuildingArtData), cell, mirrored, a building.</summary>\n')
        f.write('        public static readonly (string key, Vector3Int cell, bool flip, bool building)[] Placements =\n        {\n')
        for key, cx, cy, flip, building in sorted(placements, key=lambda p: (p[2], p[1])):
            f.write(f'            ("{key}", new Vector3Int({cx}, {cy}, 0), {str(flip).lower()}, {str(building).lower()}),\n')
        f.write('        };\n\n')
        f.write('        /// <summary>Lights: cell, colour (RGBA bytes), size (1: a campfire), offset in source pixels (64 a cell, y up).</summary>\n')
        f.write('        public static readonly (Vector3Int cell, Color32 color, float size, Vector2 offset)[] Lights =\n        {\n')
        for cx, cy, (cr, cg, cb, ca), scale, ox, oy in sorted(lights, key=lambda l: (l[1], l[0])):
            f.write(f'            (new Vector3Int({cx}, {cy}, 0), new Color32({cr}, {cg}, {cb}, {ca}), {scale:g}f, new Vector2({ox}, {oy})),\n')
        f.write('        };\n\n')
        f.write('        /// <summary>How each blocked cell is drawn: WallStyles[y][x]; woods: a source tree or not; \'.\' open ground.</summary>\n')
        f.write('        public static readonly string[] WallStyles =\n        {\n')
        for row in rows:
            f.write(f'            "{row}",\n')
        f.write('        };\n    }\n}\n')


def art_cover(placements):
    """The cells whose middle lies on an opaque pixel of a building or prop as the scene builder draws it
    (BuildingArtData: the same variant pick, pivot and mirroring as TestSceneBuilder.PlaceBuildings)."""
    from PIL import Image
    text = open(ART).read()
    sprites = re.findall(r'\("([^"]+)", (\d+), (\d+), (\d+), (\d+), (\d+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f\)', text)
    building_keys = {p[0] for p in placements if p[4]}
    groups = {}
    for name, *numbers in sprites:
        match = re.fullmatch(r'(.*)_(\d)', name)
        key = match.group(1) if match and name not in building_keys else name
        groups.setdefault(key, []).append([int(v) for v in numbers[:5]] + [float(v) for v in numbers[5:]])
    pages = {}
    world = lambda x, y: ((x - y) / 2, (x + y + 1) / 4)  # noqa: E731
    drawn = {}
    for key, cx, cy, flip, building in placements:
        if key in groups and ((cx, cy) not in drawn or building):
            drawn[(cx, cy)] = (key, flip)
    covered = set()
    for (cx, cy), (key, flip) in drawn.items():
        options = groups[key]
        page, x, y, w, h, pivot_x, pivot_y, ppu = options[abs(cx * 31 + cy * 17) % len(options)]
        if page not in pages:
            pages[page] = Image.open(os.path.join(os.path.dirname(ART), '..', '..', 'Art', 'Buildings',
                                                  f'Buildings{page}.png')).convert('RGBA')
        atlas = pages[page]
        bx, by = world(cx, cy)
        reach = int(max(w, h) / ppu * 4) + 2
        for dx in range(-reach, reach + 1):
            for dy in range(-reach, reach + 1):
                wx, wy = world(cx + dx, cy + dy)
                u = (wx - bx) * ppu * (-1 if flip else 1) + pivot_x * w
                v = (wy - by) * ppu + pivot_y * h
                if 0 <= u < w and 0 <= v < h and atlas.getpixel((x + int(u), atlas.height - 1 - (y + int(v))))[3] > 128:
                    covered.add((cx + dx, cy + dy))
    return covered


def building_sources(root):
    """For Tools/Art/buildings.py: every building key on the map with its source image (name, width, height, hotspot)."""
    cells = read_map(os.path.join(root, 'maps', 'fanadin.map'))
    db = sqlite3.connect(os.path.join(root, 'game.db'))
    hotspots = {name: (x, y) for name, x, y in db.execute('select filename, x_offset, y_offset from sprite_hotspot')}
    sizes = source_images(root)
    found = {}
    for _x, _y, _flag, layers in cells:
        for layer, texture, _scale in layers:
            key = building_key(texture) if layer else None
            if key and key not in found:
                w, h = sizes.get(texture, (0, 0))
                found[key] = (texture, w, h) + hotspots.get(texture, (w // 2, int(h * 0.85)))
    return found


if __name__ == '__main__':
    main(sys.argv[1])
