"""Packs the buildings and props of the island into atlases (credits in Assets/_Project/Art/Buildings/CREDITS.md).

    python3 -I Tools/Art/buildings.py <downloads folder> <flare-game checkout> <source data checkout>

The downloads folder holds the free originals, as downloaded from OpenGameArt.org:
  building_pack_02_single.zip, building_pack_03_single.zip   rubberduck's isometric medieval buildings 1 and 2 (CC0)
  Medieval-Tavern.zip, Timbered%20House_16.zip, Isometric.zip (Old Outpost), Galata-Tower.zip, Medieval_Building_06.zip
                                                             Bleed's Game Art (CC-BY 3.0 / 4.0)

Buildings (the keys of Tools/GameData/props.py): the source game's map uses these same free pictures; each is trimmed
to its content and drawn as big as the source game draws it (its image size there, 64 pixels a cell), standing on its
cell at the source game's hotspot, at most 128 pixels a cell (this project's tiles). Pictures the source game took from
elsewhere get a stand-in of the same size: rubberduck's buildings for the church, houses and cabins.
Props (tents, wells, anvils, signposts, campfires, wood piles, stumps, graves, statues, fences, iron fences and rubble)
are Flare's (flare-game: fantasycore tileset_grassland, empyrean_campaign tileset_ruins; CC-BY-SA 3.0), at Flare's
own size (192 pixels a cell), standing on their cell at Flare's tile offset.

Writes Assets/_Project/Art/Buildings/Buildings<N>.png (4096 x 2048) and Assets/_Project/Scripts/Editor/BuildingArtData.cs (every
sprite: atlas, rect, pivot, pixels per unit).
"""
import io
import os
import re
import subprocess
import sys
import zipfile

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Buildings')
DATA = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Editor', 'BuildingArtData.cs')
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'GameData'))
import props  # noqa: E402

SOURCE_CELL = 64.0   # source pixels a cell
MAX_CELL = 96.0      # most pixels a cell kept here (this project's tiles have 128: buildings a little softer, half the memory)
FLARE_CELL = 192.0   # Flare's pixels a cell
ATLAS = 4096
PAD = 2

# Stand-ins for pictures the source game has from elsewhere: key -> (archive, file).
STAND_INS = {
    'church': ('building_pack_03_single.zip', 'building_2/128x64_shaded/b2_128x64_shaded_00.png'),
    'house_green1': ('building_pack_03_single.zip', 'building_1/128x64_shaded/b1_128x64_shaded_00.png'),
    'medieval_house_01': ('building_pack_02_single.zip', 'building_1/128x64_shaded/building1_128x64_01.png'),
    'medieval_house_03': ('building_pack_02_single.zip', 'building_2/128x64_shaded/building2_128x64_00.png'),
    'cabin_01': ('building_pack_03_single.zip', 'building_3/128x64_shaded/b3_128x64_shaded_00.png'),
    'cabin_02': ('building_pack_03_single.zip', 'building_3/128x64_shaded/b3_128x64_shaded_01.png'),
}

# Flare props: key -> [(tileset, tile index)], one sprite per entry (variants).
FLARE_PROPS = {
    'tent': [('grassland', 72), ('grassland', 75)],
    'campfire': [('grassland', 102)],
    'anvil': [('grassland', 103)],
    'woodpile': [('grassland', 100), ('grassland', 101)],
    'stump': [('grassland', 136), ('grassland', 137)],
    'signpost': [('grassland', 138), ('grassland', 139)],
    'grave': [('grassland', 140), ('grassland', 141), ('grassland', 142), ('grassland', 143)],
    'fence_x': [('grassland', 104)],
    'fence_y': [('grassland', 107)],
    'well': [('ruins', 289), ('ruins', 290)],
    'statue': [('ruins', 241), ('ruins', 243)],
    'iron_x': [('ruins', 368)],
    'iron_y': [('ruins', 371)],
    'rubble': [('ruins', 187)],
}
FLARE_SETS = {
    'grassland': ('mods/fantasycore/images/tilesets/tileset_grassland.png', 'mods/fantasycore/tilesetdefs/tileset_grassland.txt'),
    'ruins': ('mods/empyrean_campaign/images/tilesets/tileset_ruins.png', 'mods/empyrean_campaign/tilesetdefs/tileset_ruins.txt'),
}


def trimmed(image):
    image = image.convert('RGBA')
    return image.crop(image.getchannel('A').getbbox())


def original(downloads, key):
    """The free original of a building key: (trimmed picture)."""
    def read(archive, name):
        return trimmed(Image.open(io.BytesIO(zipfile.ZipFile(os.path.join(downloads, archive)).read(name))))

    if key in STAND_INS:
        return read(*STAND_INS[key])
    match = re.fullmatch(r'building([12])_128x64_(0\d)', key)
    if match:
        return read('building_pack_02_single.zip', f'building_{match.group(1)}/128x64_shaded/{key}.png')
    match = re.fullmatch(r'b([123])_128x64_shaded_(0\d)', key)
    if match:
        return read('building_pack_03_single.zip', f'building_{match.group(1)}/128x64_shaded/{key}.png')
    if key.startswith('medieval-tavern_'):
        return read('Medieval-Tavern.zip', key + '.png')
    if key.startswith('old_outpost_iso_'):
        return read('Isometric.zip', key + '.png')
    if key == 'Medieval_Building_06':
        return read('Medieval_Building_06.zip', 'Medieval_Building_06.png')
    return None


def best_frame(downloads, archive, pattern, width, height):
    """The frame of a turning picture whose trimmed shape is closest to the source game's (width x height)."""
    zf = zipfile.ZipFile(os.path.join(downloads, archive))
    best = None
    for name in zf.namelist():
        if not re.fullmatch(pattern, name):
            continue
        frame = trimmed(Image.open(io.BytesIO(zf.read(name))))
        error = abs(frame.width / frame.height - width / height)
        if best is None or error < best[0]:
            best = (error, frame)
    return best[1]


def flare_tiles(flare, tileset):
    image_path, defs_path = FLARE_SETS[tileset]
    show = lambda path: subprocess.run(['git', '-C', flare, 'show', f'HEAD:{path}'], capture_output=True, check=True).stdout  # noqa: E731
    image = Image.open(io.BytesIO(show(image_path))).convert('RGBA')
    tiles = {}
    for m in re.finditer(r'tile=(\d+),(\d+),(\d+),(\d+),(\d+),(-?\d+),(-?\d+)', show(defs_path).decode()):
        index, x, y, w, h, ox, oy = map(int, m.groups())
        tiles[index] = (image.crop((x, y, x + w, y + h)), ox, oy)
    return tiles


def main(downloads, flare, source):
    sprites = []  # (name, image, pivot x, pivot y (from the bottom), pixels per unit)
    for key, (texture, w, h, hx, hy) in sorted(props.building_sources(source).items()):
        if key.startswith('timbered-house_'):
            picture = best_frame(downloads, 'Timbered%20House_16.zip', r'rem_\d+\.png', w, h)
        elif key == 'galata_tower':
            picture = best_frame(downloads, 'Galata-Tower.zip', r'Galata-Tower_\d+\.png', w, h)
        else:
            picture = original(downloads, key)
        if picture is None or not w:
            print(f'  no art for {key} ({texture})')
            continue
        # As wide as the source game's picture; the source's hotspot (stand-ins: the same spot of their own shape).
        cells_wide = w / SOURCE_CELL
        target_w = int(min(picture.width, cells_wide * MAX_CELL))
        target_h = max(1, round(picture.height * target_w / picture.width))
        picture = picture.resize((target_w, target_h), Image.LANCZOS)
        pivot_x = hx / w
        pivot_y = 1 - hy / h
        sprites.append((key, picture, pivot_x, pivot_y, target_w / cells_wide))

    sets = {name: flare_tiles(flare, name) for name in FLARE_SETS}
    for key, entries in FLARE_PROPS.items():
        for n, (tileset, index) in enumerate(entries):
            picture, ox, oy = sets[tileset][index]
            name = key if len(entries) == 1 else f'{key}_{n}'
            sprites.append((name, picture, ox / picture.width, 1 - oy / picture.height, FLARE_CELL))

    pages = pack(sprites)
    os.makedirs(OUT, exist_ok=True)
    for i, (page, _) in enumerate(pages):
        page.save(os.path.join(OUT, f'Buildings{i}.png'), optimize=True)
    write(pages)
    print(f'{len(sprites)} sprites in {len(pages)} atlases')


def pack(sprites):
    """MaxRects (best short side fit), biggest first, into as many ATLAS x ATLAS / 2 pages as needed."""
    width, height = ATLAS, ATLAS // 2
    pages = []
    for sprite in sorted(sprites, key=lambda s: -s[1].width * s[1].height):
        w, h = sprite[1].width + PAD, sprite[1].height + PAD
        for page in pages:
            spot = place(page[2], w, h)
            if spot:
                break
        else:
            page = [Image.new('RGBA', (width, height), (0, 0, 0, 0)), [], [(0, 0, width, height)]]
            pages.append(page)
            spot = place(page[2], w, h)
        page[0].paste(sprite[1], spot)
        page[1].append((sprite, spot[0], spot[1]))
    return [(page[0], page[1]) for page in pages]


def place(free, w, h):
    """Puts a w x h rect into the free rects (changed in place); its top left corner, or None."""
    best = None
    for fx, fy, fw, fh in free:
        if w <= fw and h <= fh:
            score = min(fw - w, fh - h)
            if best is None or score < best[0]:
                best = (score, fx, fy)
    if best is None:
        return None
    _, x, y = best
    used = (x, y, w, h)
    result = []
    for rect in free:
        result.extend(split(rect, used))
    # Drop free rects inside others.
    free[:] = [r for i, r in enumerate(result)
               if not any(j != i and contains(o, r) and (o != r or j < i) for j, o in enumerate(result))]
    return x, y


def split(rect, used):
    fx, fy, fw, fh = rect
    ux, uy, uw, uh = used
    if ux >= fx + fw or ux + uw <= fx or uy >= fy + fh or uy + uh <= fy:
        return [rect]
    parts = []
    if ux > fx:
        parts.append((fx, fy, ux - fx, fh))
    if ux + uw < fx + fw:
        parts.append((ux + uw, fy, fx + fw - ux - uw, fh))
    if uy > fy:
        parts.append((fx, fy, fw, uy - fy))
    if uy + uh < fy + fh:
        parts.append((fx, uy + uh, fw, fy + fh - uy - uh))
    return parts


def contains(outer, inner):
    return (outer[0] <= inner[0] and outer[1] <= inner[1] and outer[0] + outer[2] >= inner[0] + inner[2]
            and outer[1] + outer[3] >= inner[1] + inner[3])


def write(pages):
    lines = ['// Generated by Tools/Art/buildings.py (rubberduck CC0, Bleed CC-BY, Flare CC-BY-SA: see',
             '// Assets/_Project/Art/Buildings/CREDITS.md). Do not edit by hand.',
             'namespace RPG.EditorTools', '{', '    internal static class BuildingArtData', '    {',
             '        public const string Folder = "Assets/_Project/Art/Buildings";', '',
             '        /// <summary>Sprites: name, atlas page (Buildings&lt;page&gt;.png), rect from the bottom left, pivot, pixels a unit.</summary>',
             '        public static readonly (string name, int page, int x, int y, int w, int h, float pivotX, float pivotY, float pixelsPerUnit)[] Sprites =',
             '        {']
    for i, (page, placed) in enumerate(pages):
        for (name, image, px, py, ppu), x, y in sorted(placed, key=lambda p: p[0][0]):
            bottom = page.height - y - image.height
            lines.append(f'            ("{name}", {i}, {x}, {bottom}, {image.width}, {image.height}, {px:.4f}f, {py:.4f}f, {ppu:.2f}f),')
    lines += ['        };', '    }', '}']
    with open(DATA, 'w', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main(*sys.argv[1:4])
