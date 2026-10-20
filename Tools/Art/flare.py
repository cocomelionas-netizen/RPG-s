"""Packs the art taken from Flare (flare-game, CC-BY-SA 3.0, see Assets/_Project/Art/Flare/CREDITS.md) into atlases.

    python3 -I Tools/Art/flare.py <path to a flare-game checkout>

Writes Assets/_Project/Art/Flare/Tiles.png (ground, paths, water, rocks, plants, trees), one atlas per character in
Assets/_Project/Art/Flare/Characters/ (monsters, and the three heroes put together from Flare's avatar layers) and
Assets/_Project/Scripts/Editor/FlareArtData.cs: where each sprite is in its atlas, its pivot (Flare's anchor point: a
tile's middle, a tree's or a character's feet) and the frames of every animation in this project's eight directions.

Flare draws a 192 x 96 pixel tile per map cell; here a cell is 1 x 0.5 units and the screen shows about 120 pixels per
unit, so everything is scaled to 2/3 (128 pixels per unit), or smaller when a character's frames do not fit 2048 x 2048.
"""
import math
import os
import re
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Flare')
DATA = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Editor', 'FlareArtData.cs')
SCALE = 2 / 3
PIXELS_PER_UNIT = 192 * SCALE
MAX_ATLAS = 2048
PAD = 2

# Flare's direction index: 0 west, 1 north-west, 2 north, 3 north-east, 4 east, 5 south-east, 6 south, 7 south-west.
FLARE_DIRECTION_NAMES = ['W', 'NW', 'N', 'NE', 'E', 'SE', 'S', 'SW']
# This project's DirectionalClip fields in order: east, northEast, north, northWest, west, southWest, south, southEast.
OUR_ORDER = [4, 3, 2, 1, 0, 7, 6, 5]

# Tiles of tileset_grassland: (name, image, ids).
TILES = [
    ('Grass', 'grassland', range(16, 32)),
    ('Path', 'grassland', range(32, 44)),
    ('Rock', 'grassland', range(128, 136)),
    ('Plant', 'grassland', list(range(112, 118)) + list(range(120, 128))),
    ('Tree', 'grassland', list(range(240, 244)) + list(range(248, 256))),
    ('Water', 'water', range(176, 192)),
]

# Monsters: our look name, Flare enemy, the animation used for attacks.
MONSTERS = [
    ('Goblin', 'goblin', 'swing'),
    ('GoblinElite', 'goblin_elite', 'swing'),
    ('Skeleton', 'skeleton', 'swing'),
    ('SkeletonMage', 'skeleton_mage', 'cast'),
    ('SkeletonArcher', 'skeleton_archer', 'shoot'),
    ('Zombie', 'zombie', 'swing'),
    ('Minotaur', 'minotaur', 'swing'),
    ('Antlion', 'antlion', 'swing'),
    ('AntlionSmall', 'antlion_small', 'swing'),
    ('FireAnt', 'fire_ant', 'swing'),
    ('IceAnt', 'ice_ant', 'swing'),
    ('Wyvern', 'wyvern', 'swing'),
    ('WyvernFire', 'wyvern_fire', 'swing'),
]

# Heroes from Flare's male avatar layers: our look name, the attack animation, part per layer.
HEROES = [
    ('HeroWarrior', 'swing', {'head': 'plate_helm', 'chest': 'plate_cuirass', 'legs': 'plate_greaves', 'feet': 'plate_boots',
                              'hands': 'plate_gauntlets', 'main': 'longsword', 'off': 'kite_shield'}),
    ('HeroRanger', 'shoot', {'head': 'leather_hood', 'chest': 'leather_chest', 'legs': 'leather_pants', 'feet': 'leather_boots',
                             'hands': 'leather_gloves', 'main': 'longbow'}),
    ('HeroMage', 'cast', {'head': 'mage_hood', 'chest': 'mage_vest', 'legs': 'mage_skirt', 'feet': 'mage_boots',
                          'hands': 'mage_sleeves', 'main': 'staff'}),
]


def read_tileset(path):
    """{image section: {id: (x, y, w, h, ox, oy)}} of a tileset definition."""
    sets, current = {}, None
    for line in open(path):
        line = line.strip()
        if line.startswith('img='):
            current = os.path.basename(line[4:])
            sets.setdefault(current, {})
        m = re.match(r'tile=(\d+),(\d+),(\d+),(\d+),(\d+),(-?\d+),(-?\d+)', line)
        if m and current:
            sets[current][int(m.group(1))] = tuple(map(int, m.groups()[1:]))
    return sets


def read_animation(path):
    """Images ({section or None: path}) and sections: {name: {'frames', 'duration', 'type', 'frame': {(i, dir): rect}}}."""
    images, sections, current = {}, {}, None
    for line in open(path):
        line = line.strip()
        if line.startswith('image='):
            parts = line[6:].split(',')
            images[parts[1] if len(parts) > 1 else None] = parts[0]
            continue
        m = re.match(r'\[(\w+)\]', line)
        if m:
            current = sections.setdefault(m.group(1), {'frame': {}})
            continue
        if current is None or '=' not in line:
            continue
        key, value = line.split('=', 1)
        if key == 'frame':
            # index, direction, x, y, w, h, anchor x, anchor y[, the image it is in]
            parts = value.split(',')
            v = list(map(int, parts[:8]))
            current['frame'][(v[0], v[1])] = tuple(v[2:]) + ((parts[8] if len(parts) > 8 else None),)
        elif key == 'frames':
            current['frames'] = int(value)
        elif key == 'duration':
            current['duration'] = int(re.sub(r'\D', '', value)) / (1 if value.endswith('ms') else 0.001)
        elif key == 'type':
            current['type'] = value
    return images, sections


def frame_order(section):
    """The frame indices played, back_forth expanded (0 1 2 3 2 1)."""
    count = section.get('frames', 1)
    order = list(range(count))
    if section.get('type') == 'back_forth' and count > 2:
        order += list(range(count - 2, 0, -1))
    return order


def fps_of(section):
    count = section.get('frames', 1)
    duration = section.get('duration', 400)
    return max(1.0, count / (duration / 1000.0))


class Atlas:
    """Shelf-packs images; rects are returned in Unity's coordinates (origin bottom left)."""

    def __init__(self):
        self.items = []

    def add(self, image, pivot):
        self.items.append((image, pivot))
        return len(self.items) - 1

    def pack(self, limit=MAX_ATLAS):
        order = sorted(range(len(self.items)), key=lambda i: -self.items[i][0].height)
        width = 256
        while True:
            placed, x, y, shelf, ok = {}, PAD, PAD, 0, True
            for i in order:
                im = self.items[i][0]
                if x + im.width + PAD > width:
                    x, y, shelf = PAD, y + shelf + PAD, 0
                if x + im.width + PAD > width:
                    ok = False
                    break
                placed[i] = (x, y)
                x += im.width + PAD
                shelf = max(shelf, im.height)
            height = y + shelf + PAD
            if ok and height <= width:
                break
            if width >= limit:
                return None
            width *= 2
        height = 1 << max(0, math.ceil(math.log2(height)))
        sheet = Image.new('RGBA', (width, height), (0, 0, 0, 0))
        rects = [None] * len(self.items)
        for i, (x, y) in placed.items():
            im, (px, py) = self.items[i]
            sheet.alpha_composite(im, (x, y))
            rects[i] = (x, height - y - im.height, im.width, im.height, px, py)
        return sheet, rects


def scaled(image, scale):
    w = max(1, round(image.width * scale))
    h = max(1, round(image.height * scale))
    return image.resize((w, h), Image.LANCZOS)


def pivot_of(w, h, ox, oy):
    """Flare's anchor (pixels from the top left) as a Unity pivot (0..1 from the bottom left)."""
    return ox / w, 1 - oy / h


def pack_tiles(flare):
    base = os.path.join(flare, 'mods', 'fantasycore')
    sets = read_tileset(os.path.join(base, 'tilesetdefs', 'tileset_grassland.txt'))
    sources = {
        'grassland': (Image.open(os.path.join(base, 'images', 'tilesets', 'tileset_grassland.png')).convert('RGBA'),
                      sets['tileset_grassland.png']),
        'water': (Image.open(os.path.join(base, 'images', 'tilesets', 'tileset_grassland_water.png')).convert('RGBA'),
                  sets['tileset_grassland_water.png']),
    }
    atlas, names = Atlas(), []
    for name, source, ids in TILES:
        image, rects = sources[source]
        for k, tile in enumerate(ids):
            x, y, w, h, ox, oy = rects[tile]
            crop = image.crop((x, y, x + w, y + h))
            # Water lies a half tile lower in Flare; here it is level with the ground.
            pivot = (0.5, 0.5) if source == 'water' else pivot_of(w, h, ox, oy)
            atlas.add(scaled(crop, SCALE), pivot)
            names.append(f'{name}{k}')
    sheet, rects = atlas.pack(4096)
    sheet.save(os.path.join(OUT, 'Tiles.png'), optimize=True)
    return list(zip(names, rects))


def character_frames(images, sections, base, clips, compose=None):
    """[(clip id, flare section, loop)] → frames {(section, i, dir): (image, ox, oy)} (unscaled)."""
    cache = {}

    def sheet(key):
        path = images.get(key) or images.get(None)
        if path not in cache:
            cache[path] = Image.open(os.path.join(base, path)).convert('RGBA')
        return cache[path]

    frames = {}
    for _clip, section, _loop in clips:
        data = sections[section]
        for i in range(data.get('frames', 1)):
            for d in range(8):
                rect = data['frame'].get((i, d))
                if rect is None:
                    continue
                x, y, w, h, ox, oy, key = rect
                frames[(section, i, d)] = (sheet(key or section).crop((x, y, x + w, y + h)), ox, oy)
    return frames


def compose_hero(base, parts, clips):
    """Each frame of the hero: Flare's avatar layers drawn in its order for that direction (engine/hero_layers.txt)."""
    layers = {}
    for line in open(os.path.join(base, 'engine', 'hero_layers.txt')):
        if line.startswith('layer='):
            name, *order = line.strip()[6:].split(',')
            layers[FLARE_DIRECTION_NAMES.index(name)] = order
    part_frames = {}
    for layer, part in parts.items():
        images, sections = read_animation(os.path.join(base, 'animations', 'avatar', 'male', part + '.txt'))
        part_frames[layer] = character_frames(images, sections, base, clips)
    _images, sections = read_animation(os.path.join(base, 'animations', 'avatar', 'male', parts['chest'] + '.txt'))
    frames = {}
    for _clip, section, _loop in clips:
        for i in range(sections[section].get('frames', 1)):
            for d in range(8):
                pieces = [part_frames[layer].get((section, i, d)) for layer in layers[d] if layer in part_frames]
                pieces = [p for p in pieces if p is not None]
                if not pieces:
                    continue
                left = min(-ox for _, ox, _ in pieces)
                top = min(-oy for _, _, oy in pieces)
                right = max(im.width - ox for im, ox, _ in pieces)
                bottom = max(im.height - oy for im, _, oy in pieces)
                canvas = Image.new('RGBA', (right - left, bottom - top), (0, 0, 0, 0))
                for im, ox, oy in pieces:
                    canvas.alpha_composite(im, (-ox - left, -oy - top))
                frames[(section, i, d)] = (canvas, -left, -top)
    return frames, sections


def pack_character(name, frames, sections, clips):
    """Packs the frames, smaller when they do not fit; returns the look's data for FlareArtData."""
    scale = SCALE
    while True:
        atlas, index = Atlas(), {}
        for key, (image, ox, oy) in frames.items():
            index[key] = atlas.add(scaled(image, scale), pivot_of(image.width, image.height, ox, oy))
        packed = atlas.pack()
        if packed:
            break
        scale *= 0.9
    sheet, rects = packed
    sheet.save(os.path.join(OUT, 'Characters', name + '.png'), optimize=True)
    clip_data = []
    for clip_id, section, loop in clips:
        data = sections[section]
        directions = []
        for d in OUR_ORDER:
            directions.append([index[(section, i, d)] for i in frame_order(data) if (section, i, d) in index])
        clip_data.append((clip_id, fps_of(data), loop, directions))
    return {'name': name, 'ppu': 192 * scale, 'rects': rects, 'clips': clip_data, 'size': sheet.size}


def clips_for(attack):
    return [('Idle', 'stance', True), ('Walk', 'run', True), ('Attack', attack, False)]


def cs_float(value):
    text = f'{value:.4f}'.rstrip('0').rstrip('.')
    return (text or '0') + 'f'


def write_data(tiles, looks):
    w = []
    w.append('// Generated by Tools/Art/flare.py from flare-game (CC-BY-SA 3.0, see Assets/_Project/Art/Flare/CREDITS.md).')
    w.append('// Do not edit by hand.')
    w.append('namespace RPG.EditorTools')
    w.append('{')
    w.append('    internal static class FlareArtData')
    w.append('    {')
    w.append('        public const string Folder = "Assets/_Project/Art/Flare";')
    w.append(f'        public const float TilePixelsPerUnit = {cs_float(PIXELS_PER_UNIT)};')
    w.append('')
    w.append('        /// <summary>Sprites of Tiles.png: name, rect in pixels from the bottom left, pivot (0..1).</summary>')
    w.append('        public static readonly (string name, int x, int y, int w, int h, float pivotX, float pivotY)[] Tiles =')
    w.append('        {')
    for name, (x, y, wd, h, px, py) in tiles:
        w.append(f'            ("{name}", {x}, {y}, {wd}, {h}, {cs_float(px)}, {cs_float(py)}),')
    w.append('        };')
    w.append('')
    w.append('        internal sealed class Look')
    w.append('        {')
    w.append('            public string Name;')
    w.append('            public float PixelsPerUnit;')
    w.append('            /// <summary>Frame rects: x, y (from the bottom left), width, height in pixels, then pivot x, y in thousandths.</summary>')
    w.append('            public int[] Rects;')
    w.append('            public Clip[] Clips;')
    w.append('        }')
    w.append('')
    w.append('        internal sealed class Clip')
    w.append('        {')
    w.append('            public string Id;')
    w.append('            public float FramesPerSecond;')
    w.append('            public bool Loop;')
    w.append('            /// <summary>Frame numbers (into Rects) for east, north-east, north, north-west, west, south-west, south, south-east.</summary>')
    w.append('            public int[][] Directions;')
    w.append('        }')
    w.append('')
    w.append('        public static readonly Look[] Looks =')
    w.append('        {')
    for look in looks:
        rects = []
        for x, y, wd, h, px, py in look['rects']:
            rects += [x, y, wd, h, round(px * 1000), round(py * 1000)]
        w.append('            new Look')
        w.append('            {')
        w.append(f'                Name = "{look["name"]}", PixelsPerUnit = {cs_float(look["ppu"])},')
        w.append('                Rects = new[] { ' + ', '.join(map(str, rects)) + ' },')
        w.append('                Clips = new[]')
        w.append('                {')
        for clip_id, fps, loop, directions in look['clips']:
            dirs = ', '.join('new[] { ' + ', '.join(map(str, d)) + ' }' for d in directions)
            w.append(f'                    new Clip {{ Id = "{clip_id}", FramesPerSecond = {cs_float(fps)}, Loop = {"true" if loop else "false"}, '
                     f'Directions = new[] {{ {dirs} }} }},')
        w.append('                },')
        w.append('            },')
    w.append('        };')
    w.append('    }')
    w.append('}')
    with open(DATA, 'w', newline='\n') as f:
        f.write('\n'.join(w) + '\n')


def main(flare):
    os.makedirs(os.path.join(OUT, 'Characters'), exist_ok=True)
    base = os.path.join(flare, 'mods', 'fantasycore')
    tiles = pack_tiles(flare)
    looks = []
    for name, enemy, attack in MONSTERS:
        clips = clips_for(attack)
        images, sections = read_animation(os.path.join(base, 'animations', 'enemies', enemy + '.txt'))
        frames = character_frames(images, sections, base, clips)
        looks.append(pack_character(name, frames, sections, clips))
    for name, attack, parts in HEROES:
        clips = clips_for(attack)
        frames, sections = compose_hero(base, parts, clips)
        looks.append(pack_character(name, frames, sections, clips))
    write_data(tiles, looks)
    for look in looks:
        print(f'{look["name"]}: {look["size"][0]}x{look["size"][1]}, {len(look["rects"])} frames, {look["ppu"]:.0f} px/unit')


if __name__ == '__main__':
    main(sys.argv[1])
