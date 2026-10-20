"""Packs the icons of items and spells into one atlas (credits in Assets/_Project/Art/Icons/CREDITS.md).

    python3 -I Tools/Art/icons.py <flare-game checkout> <painterly spell icons folder> <game-icons.net png folder>

- Flare (flare-game, CC-BY-SA 3.0): its item icons (mods/fantasycore/images/icons/icons.png, 64 x 64, by index):
  weapons, armor sets, jewelry, gems, potions, scrolls, books, keys, coins; and a few of its crafting icons
  (mods/empyrean_campaign/images/icons/icons_crafting.png and icons_empyrean.png, "emp/<n>").
- Painterly Spell Icons by J. W. Bjerk (eleazzaar), CC-BY-SA 3.0: the spells ("spell/<name>"; the folder holds the
  unpacked parts 1-4, 256 x 256 each, scaled to 64).
- game-icons.net (CC-BY 3.0): white glyphs ("glyph/<author>/<name>", from its PNG archive, ffffff/transparent/1x1)
  drawn on Flare's blank stone icon like Flare's own skill icons: junk, materials and food Flare has no icon for.

Writes Assets/_Project/Art/Icons/Icons.png and Assets/_Project/Scripts/Editor/IconArtData.cs (where each named icon is).
The builder (TestSceneBuilder.Icons.cs) picks the icons by these names.
"""
import glob
import os
import sys

from PIL import Image, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Icons')
DATA = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Editor', 'IconArtData.cs')
SIZE = 64
PAD = 2
ATLAS = 1024

# Flare's icons.png by index (8 per row).
FLARE = (
    [1]  # Attack: crossed swords
    + [64, 65, 68, 69, 70, 71, 72, 80, 81, 82, 83, 84, 85, 86, 87, 88]  # potions, gems, a rune scroll, coins
    + list(range(96, 108)) + list(range(109, 116)) + list(range(117, 124))  # weapons and shields
    + list(range(129, 133)) + list(range(136, 141)) + list(range(144, 149)) + list(range(152, 157))
    + list(range(160, 165)) + [176] + list(range(184, 189)) + list(range(192, 197))  # armor sets
    + list(range(198, 209)) + list(range(212, 220)) + [224, 225, 226]  # rings, belt, necklaces, figurines, scroll, book, key
)
# Flare's empyrean crafting icons: icons_crafting.png 0-4, then icons_empyrean.png as 5-11.
EMPYREAN = list(range(12))

SPELLS = [
    # Paladin
    'slice-orange-2', 'light-royal-3', 'light-air-fire-3', 'rock-orange-3', 'haste-fire-2', 'enchant-orange-3',
    'beam-royal-3', 'light-sky-2', 'runes-orange-3', 'protect-orange-3', 'heal-royal-3', 'heal-jade-3',
    'explosion-orange-2', 'lighting-royal-3', 'protect-sky-3', 'haste-royal-3', 'protect-red-3', 'link-spirit-3',
    'shielding-spirit-3', 'protect-jade-3',
    # Ranger
    'slice-spirit-2', 'vines-plain-2', 'evil-eye-red-2', 'needles-sky-2', 'needles-royal-2', 'wind-sky-2',
    'slice-acid-2', 'rip-sky-2', 'fog-sky-2', 'explosion-red-1', 'needles-fire-2', 'enchant-jade-3',
    'fire-arrows-royal-2', 'rip-acid-2', 'wind-grasp-air-2', 'fire-arrows-jade-2', 'fire-arrows-2',
    'explosion-orange-3', 'evil-eye-eerie-2', 'fire-arrows-sky-3',
    # Mage
    'fireball-red-2', 'fog-orange-2', 'ice-sky-1', 'beam-red-2', 'ice-blue-2', 'light-blue-2', 'explosion-sky-3',
    'enchant-blue-3', 'horror-eerie-2', 'protect-blue-3', 'light-air-fire-2', 'air-burst-sky-2', 'ice-sky-3',
    'runes-blue-3', 'link-blue-3', 'air-burst-magenta-3', 'heal-sky-3', 'runes-magenta-2', 'beam-magenta-2',
    'wind-red-3',
    # Orbs and essences (items)
    'light-air-fire-2', 'light-blue-3', 'light-jade-2', 'light-royal-2', 'light-magenta-2', 'light-jade-3',
]

GLYPHS = [
    'lorc/ammonite-fossil', 'delapouite/hair-strands', 'lorc/chicken-leg', 'lorc/broken-bone', 'lorc/crab-claw',
    'lorc/crossed-bones', 'delapouite/rusty-sword', 'lorc/tooth', 'delapouite/melting-metal', 'lorc/metal-scales',
    'lorc/gem-necklace', 'delapouite/wooden-crate', 'lorc/skull-crack', 'lorc/bird-claw', 'lorc/hoof',
    'lorc/insect-jaws', 'lorc/dorsal-scales', 'delapouite/claws', 'delapouite/wolverine-claws', 'skoll/fangs',
    'lorc/pretty-fangs', 'faithtoken/minerals', 'lorc/crystal-cluster', 'lorc/crystal-growth', 'lorc/gems',
    'delapouite/animal-hide', 'lorc/crown-coin', 'lorc/metal-bar', 'delapouite/log', 'delapouite/rolled-cloth',
    'delapouite/bread', 'lorc/pie-slice', 'delapouite/sandwich', 'lorc/roast-chicken', 'lorc/meat',
    'delapouite/ivory-tusks', 'delapouite/elf-ear', 'lorc/shard-sword', 'delapouite/praying-mantis',
]


def flare_icons(flare):
    sheet = Image.open(os.path.join(flare, 'mods', 'fantasycore', 'images', 'icons', 'icons.png')).convert('RGBA')
    columns = sheet.width // SIZE
    icons = {}
    for index in FLARE:
        x, y = index % columns * SIZE, index // columns * SIZE
        icons[f'flare/{index}'] = sheet.crop((x, y, x + SIZE, y + SIZE))
    folder = os.path.join(flare, 'mods', 'empyrean_campaign', 'images', 'icons')
    strip = []
    for name in ('icons_crafting.png', 'icons_empyrean.png'):
        image = Image.open(os.path.join(folder, name)).convert('RGBA')
        strip += [image.crop((x, 0, x + SIZE, SIZE)) for x in range(0, image.width, SIZE)
                  if image.crop((x, 0, x + SIZE, SIZE)).getbbox()]
    for index in EMPYREAN:
        icons[f'emp/{index}'] = strip[index]
    # Flare's blank stone (the background of its skill icons) for the glyphs.
    stone = sheet.crop((48 % columns * SIZE, 48 // columns * SIZE, 48 % columns * SIZE + SIZE, 48 // columns * SIZE + SIZE))
    return icons, stone


def spell_icons(folder):
    files = {os.path.basename(f)[:-4]: f for f in glob.glob(os.path.join(folder, '**', '*.png'), recursive=True)
             if '__MACOSX' not in f}
    return {f'spell/{name}': Image.open(files[name]).convert('RGBA').resize((SIZE, SIZE), Image.LANCZOS)
            for name in dict.fromkeys(SPELLS)}


def glyph_icons(folder, stone):
    base = os.path.join(folder, 'icons', 'ffffff', 'transparent', '1x1')
    icons = {}
    for name in GLYPHS:
        glyph = Image.open(os.path.join(base, name + '.png')).convert('RGBA').resize((44, 44), Image.LANCZOS)
        # A dark shadow under the white glyph, as on Flare's skill icons.
        shadow = Image.new('RGBA', glyph.size, (0, 0, 0, 0))
        shadow.putalpha(glyph.getchannel('A').point(lambda a: a * 0.75))
        shadow = shadow.filter(ImageFilter.GaussianBlur(1.5))
        icon = stone.copy()
        icon.alpha_composite(shadow, (12, 12))
        icon.alpha_composite(glyph, (10, 10))
        icons[f'glyph/{name}'] = icon
    return icons


def write_data(rects):
    w = ['// Generated by Tools/Art/icons.py (Flare, Painterly Spell Icons, game-icons.net; see Assets/_Project/Art/Icons/CREDITS.md).',
         '// Do not edit by hand.',
         'namespace RPG.EditorTools',
         '{',
         '    internal static class IconArtData',
         '    {',
         '        public const string Atlas = "Assets/_Project/Art/Icons/Icons.png";',
         f'        public const int Size = {SIZE};',
         '',
         '        /// <summary>Each icon: name and its lower left corner in the atlas (pixels from the bottom left).</summary>',
         '        public static readonly (string name, int x, int y)[] Icons =',
         '        {']
    for name, (x, y) in rects:
        w.append(f'            ("{name}", {x}, {y}),')
    w += ['        };', '    }', '}']
    with open(DATA, 'w', newline='\n') as f:
        f.write('\n'.join(w) + '\n')


def main(flare, painterly, game_icons):
    icons, stone = flare_icons(flare)
    icons.update(spell_icons(painterly))
    icons.update(glyph_icons(game_icons, stone))
    cell = SIZE + 2 * PAD
    per_row = ATLAS // cell
    assert len(icons) <= per_row * per_row, 'too many icons for the atlas'
    sheet = Image.new('RGBA', (ATLAS, ATLAS), (0, 0, 0, 0))
    rects = []
    for i, (name, image) in enumerate(icons.items()):
        x = i % per_row * cell + PAD
        y = i // per_row * cell + PAD
        # The edge pixels repeated into the padding, so filtering never pulls in a neighbour.
        sheet.paste(image.resize((SIZE + 2 * PAD, SIZE + 2 * PAD), Image.NEAREST), (x - PAD, y - PAD))
        sheet.paste(image, (x, y))
        rects.append((name, (x, ATLAS - y - SIZE)))
    os.makedirs(OUT, exist_ok=True)
    sheet.save(os.path.join(OUT, 'Icons.png'), optimize=True)
    write_data(rects)
    print(f'{len(icons)} icons')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3])
