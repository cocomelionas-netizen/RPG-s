"""Packs Flare's hero layers (flare-game, CC-BY-SA 3.0) so a hero is drawn from the gear it wears.

    python3 -I Tools/Art/avatar.py <path to a flare-game checkout>

Flare draws its heroes in layers: a head (hair, or the helmet over it), chest, legs, feet, hands, the main-hand weapon and
the off-hand shield, each its own animation sheet with the same frames. This keeps them apart, for the male and the
female hero: one atlas per gender and slot in Assets/_Project/Art/Flare/Avatar/ (each part's frames in it) and
Assets/_Project/Scripts/Editor/FlareAvatarData.cs (each part's frames: rect, pivot, clips in eight directions, and the
order the slots are drawn in per direction, Flare's engine/hero_layers.txt).

Armor has Idle, Walk and the three attacks (Swing, Shoot, Cast: the one played is the weapon's); a weapon has Idle,
Walk and its own attack.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image  # noqa: E402
from flare import (FLARE_DIRECTION_NAMES, OUR_ORDER, PAD, ROOT, SCALE, character_frames, cs_float, fps_of,  # noqa: E402
                   frame_order, pivot_of, read_animation, scaled)

OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Flare', 'Avatar')
DATA = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Editor', 'FlareAvatarData.cs')

GENDERS = [('Male', 'male', 'head_short'), ('Female', 'female', 'head_long')]
ARMOR_CLIPS = [('Idle', 'stance', True), ('Walk', 'run', True), ('Swing', 'swing', False), ('Shoot', 'shoot', False),
               ('Cast', 'cast', False)]
# Slot: the parts kept ('hair' is the gender's own head). The default_* parts are the bare body (nothing worn).
ARMOR = {
    'head': ['hair', 'leather_hood', 'chain_coif', 'plate_helm', 'mage_hood'],
    'chest': ['default_chest', 'cloth_shirt', 'leather_chest', 'chain_cuirass', 'plate_cuirass', 'mage_vest'],
    'legs': ['default_legs', 'cloth_pants', 'leather_pants', 'chain_greaves', 'plate_greaves', 'mage_skirt'],
    'feet': ['default_feet', 'cloth_sandals', 'leather_boots', 'chain_boots', 'plate_boots', 'mage_boots'],
    'hands': ['default_hands', 'cloth_gloves', 'leather_gloves', 'chain_gloves', 'plate_gauntlets', 'mage_sleeves'],
    'off': ['buckler', 'kite_shield'],
}
# Weapons and the attack they play.
WEAPONS = [('dagger', 'swing'), ('shortsword', 'swing'), ('longsword', 'swing'), ('battle_axe', 'swing'), ('mace', 'swing'),
           ('shortbow', 'shoot'), ('longbow', 'shoot'), ('wand', 'cast'), ('staff', 'cast')]
CLIP_OF = {'swing': 'Swing', 'shoot': 'Shoot', 'cast': 'Cast'}
SLOTS = ['head', 'chest', 'legs', 'feet', 'hands', 'main', 'off']


def parts():
    """(slot, part, clips) for every part kept."""
    out = []
    for slot, names in ARMOR.items():
        for name in names:
            out.append((slot, name, ARMOR_CLIPS))
    for name, attack in WEAPONS:
        out.append(('main', name, [('Idle', 'stance', True), ('Walk', 'run', True), (CLIP_OF[attack], attack, False)]))
    return out


def layer_order(base):
    """Slots in drawing order (first drawn first) for each of this project's directions (OUR_ORDER)."""
    layers = {}
    for line in open(os.path.join(base, 'engine', 'hero_layers.txt')):
        if line.startswith('layer='):
            name, *order = line.strip()[6:].split(',')
            layers[FLARE_DIRECTION_NAMES.index(name)] = order
    return [layers[d] for d in OUR_ORDER]


def pack_tight(items):
    """Shelf-packs (image, pivot) items into the smallest sheet up to 4096 wide and high (sides multiples of 4, not
    powers of two: the atlases are compressed in 4 x 4 blocks); rects in Unity's coordinates (origin bottom left)."""
    order = sorted(range(len(items)), key=lambda i: -items[i][0].height)
    best = None
    for width in range(1024, 4097, 256):
        placed, x, y, shelf = {}, PAD, PAD, 0
        for i in order:
            im = items[i][0]
            if x + im.width + PAD > width:
                x, y, shelf = PAD, y + shelf + PAD, 0
            placed[i] = (x, y)
            x += im.width + PAD
            shelf = max(shelf, im.height)
        height = (y + shelf + PAD + 3) // 4 * 4
        if height <= 4096 and (best is None or width * height < best[0] * best[1]):
            best = (width, height, placed)
    if best is None:
        return None
    width, height, placed = best
    sheet = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    rects = [None] * len(items)
    for i, (x, y) in placed.items():
        im, (px, py) = items[i]
        sheet.alpha_composite(im, (x, y))
        rects[i] = (x, height - y - im.height, im.width, im.height, px, py)
    return sheet, rects


def pack_slot(gender, slot, entries):
    """One atlas for a gender's slot; every part's frames in it at the world's 2/3 scale."""
    items, looks = [], []
    for part, file_name, images, sections, frames, clips in entries:
        index = {}
        for key, (image, ox, oy) in frames.items():
            index[key] = len(items)
            items.append((scaled(image, SCALE), pivot_of(image.width, image.height, ox, oy)))
        looks.append((part, index, sections, clips))
    packed = pack_tight(items)
    if not packed:
        raise SystemExit(f'{gender} {slot}: frames do not fit 4096 x 4096')
    sheet, rects = packed
    name = f'{gender}_{slot}'
    sheet.save(os.path.join(OUT, name + '.png'), optimize=True)
    result = []
    for part, index, sections, clips in looks:
        clip_data = []
        for clip_id, section, loop in clips:
            data = sections[section]
            directions = [[index[(section, i, d)] for i in frame_order(data) if (section, i, d) in index] for d in OUR_ORDER]
            clip_data.append((clip_id, fps_of(data), loop, directions))
        result.append({'part': part, 'clips': clip_data})
    return {'atlas': name, 'size': sheet.size, 'rects': rects, 'parts': result}


def write_data(order, genders):
    w = ['// Generated by Tools/Art/avatar.py from flare-game (CC-BY-SA 3.0, see Assets/_Project/Art/Flare/CREDITS.md).',
         '// Do not edit by hand.',
         'namespace RPG.EditorTools',
         '{',
         '    internal static class FlareAvatarData',
         '    {',
         '        public const string Folder = "Assets/_Project/Art/Flare/Avatar";',
         f'        public const float PixelsPerUnit = {cs_float(192 * SCALE)};',
         '',
         '        /// <summary>Slots in drawing order (first drawn first) for east, north-east, north, north-west, west, south-west,',
         '        /// south, south-east.</summary>',
         '        public static readonly string[][] LayerOrder =',
         '        {']
    for slots in order:
        w.append('            new[] { ' + ', '.join(f'"{s}"' for s in slots) + ' },')
    w += ['        };',
          '',
          '        internal sealed class Sheet',
          '        {',
          '            public string Gender;',
          '            public string Slot;',
          '            /// <summary>Atlas file name in <see cref="Folder"/> (without .png).</summary>',
          '            public string Atlas;',
          '            /// <summary>Frame rects: x, y (from the bottom left), width, height in pixels, then pivot x, y in thousandths.</summary>',
          '            public int[] Rects;',
          '            public Part[] Parts;',
          '        }',
          '',
          '        internal sealed class Part',
          '        {',
          '            public string Name;',
          '            public FlareArtData.Clip[] Clips;',
          '        }',
          '',
          '        public static readonly Sheet[] Sheets =',
          '        {']
    for gender, sheets in genders:
        for slot, sheet in sheets:
            rects = []
            for x, y, wd, h, px, py in sheet['rects']:
                rects += [x, y, wd, h, round(px * 1000), round(py * 1000)]
            w.append('            new Sheet')
            w.append('            {')
            w.append(f'                Gender = "{gender}", Slot = "{slot}", Atlas = "{sheet["atlas"]}",')
            w.append('                Rects = new[] { ' + ', '.join(map(str, rects)) + ' },')
            w.append('                Parts = new[]')
            w.append('                {')
            for part in sheet['parts']:
                w.append(f'                    new Part {{ Name = "{part["part"]}", Clips = new[] {{')
                for clip_id, fps, loop, directions in part['clips']:
                    dirs = ', '.join('new[] { ' + ', '.join(map(str, d)) + ' }' for d in directions)
                    w.append(f'                        new FlareArtData.Clip {{ Id = "{clip_id}", FramesPerSecond = {cs_float(fps)}, '
                             f'Loop = {"true" if loop else "false"}, Directions = new[] {{ {dirs} }} }},')
                w.append('                    } },')
            w.append('                },')
            w.append('            },')
    w += ['        };', '    }', '}']
    with open(DATA, 'w', newline='\n') as f:
        f.write('\n'.join(w) + '\n')


def main(flare):
    os.makedirs(OUT, exist_ok=True)
    base = os.path.join(flare, 'mods', 'fantasycore')
    genders = []
    for gender, folder, hair in GENDERS:
        by_slot = {}
        for slot, part, clips in parts():
            file_name = hair if part == 'hair' else part
            images, sections = read_animation(os.path.join(base, 'animations', 'avatar', folder, file_name + '.txt'))
            frames = character_frames(images, sections, base, clips)
            by_slot.setdefault(slot, []).append((part, file_name, images, sections, frames, clips))
        sheets = []
        for slot in SLOTS:
            sheet = pack_slot(gender, slot, by_slot[slot])
            sheets.append((slot, sheet))
            print(f'{gender} {slot}: {sheet["size"][0]}x{sheet["size"][1]}, {len(sheet["rects"])} frames')
        genders.append((gender, sheets))
    write_data(layer_order(base), genders)


if __name__ == '__main__':
    main(sys.argv[1])
