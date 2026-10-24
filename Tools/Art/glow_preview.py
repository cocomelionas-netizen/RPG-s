"""Previews the weapon glow (HeroAppearance.GlowOf, the RPG/SpriteGlow shader) on every weapon look of the male hero.

    python3 -I Tools/Art/glow_preview.py [out.png]

Draws each weapon (dagger ... staff) in the hero's hand facing south-west at +0, +6, +9 and +12, the way the game does:
the rings of additive copies behind the weapon, the weapon, a sheen over it, then the body. Keep the numbers below in step
with HeroAppearance when the glow changes.
"""
import os
import re
import sys

from PIL import Image, ImageChops, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
DATA = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Editor', 'FlareAvatarData.cs')
ATLASES = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Flare', 'Avatar')
PPU = 128  # FlareAvatarData.PixelsPerUnit

# HeroAppearance: GlowOf (colour, strength), the rings (radius in units, strength, whiteness), the sheen.
GLOW = {6: (1, 1, 1, .65), 9: (.25, .4, 1, .9), 12: (1, .45, .06, 1)}
RINGS = [(0.009, .45, .6), (0.02, .28, .1), (0.042, .14, 0)]
OFFSETS = [(1, 0), (-1, 0), (0, 1), (0, -1), (.7, .7), (-.7, .7), (.7, -.7), (-.7, -.7)]
SHEEN = 0.3
WEAPONS = ['dagger', 'shortsword', 'longsword', 'battle_axe', 'mace', 'shortbow', 'longbow', 'wand', 'staff']
BODY = [('feet', 'leather_boots'), ('legs', 'leather_pants'), ('hands', 'leather_gloves'), ('chest', 'leather_chest'),
        ('head', 'hair')]
SOUTH_WEST = 5  # FlareAvatarData direction index; the weapon is drawn first (behind the body) facing it
W, H, FEET_X, FEET_Y = 150, 170, 75, 130


def read_sheets():
    """(gender, slot) -> (atlas, frame rects, {part: idle frames by direction})."""
    text = open(DATA).read()
    sheets = {}
    pattern = r'Gender = "(\w+)", Slot = "(\w+)", Atlas = "(\w+)",\s*Rects = new\[\] \{([^}]*)\}'
    for match in re.finditer(pattern, text):
        gender, slot, atlas, rects = match.groups()
        numbers = [int(v) for v in rects.split(',') if v.strip()]
        end = text.find('new Sheet', match.end())
        body = text[match.end():end if end > 0 else len(text)]
        parts = {}
        for part in re.finditer(r'Name = "(\w+)", Clips = new\[\] \{\s*new FlareArtData.Clip \{ Id = "Idle"[^D]*'
                                r'Directions = new\[\] \{ (.*?) \} \}', body):
            directions = re.findall(r'new\[\] \{ ([\d, ]+) \}', part.group(2))
            parts[part.group(1)] = [[int(v) for v in d.split(',')] for d in directions]
        sheets[(gender, slot)] = (atlas, [numbers[i:i + 6] for i in range(0, len(numbers), 6)], parts)
    return sheets


def main(out):
    sheets = read_sheets()
    atlases = {}

    def frame(slot, part):
        atlas, rects, parts = sheets[('Male', slot)]
        if atlas not in atlases:
            atlases[atlas] = Image.open(os.path.join(ATLASES, atlas + '.png')).convert('RGBA')
        image = atlases[atlas]
        x, y, w, h, px, py = rects[parts[part][SOUTH_WEST][0]]
        return image.crop((x, image.height - y - h, x + w, image.height - y)), px / 1000 * w, (1 - py / 1000) * h

    def solid(sprite, dx=0, dy=0):
        # The shader's smoothstep(0.5, 0.85, alpha): only solid pixels glow.
        mask = Image.new('L', (W, H), 0)
        image, px, py = sprite
        mask.paste(image.getchannel('A').point(lambda v: int(255 * min(1, max(0, (v / 255 - 0.5) / 0.35)))),
                   (int(FEET_X - px + dx), int(FEET_Y - py - dy)))
        return mask

    def light(mask, colour, alpha):
        return Image.merge('RGB', [mask.point(lambda v, k=k: int(v * alpha * k)) for k in colour])

    body = [frame(slot, part) for slot, part in BODY]
    columns = [0, 6, 9, 12]
    sheet = Image.new('RGB', (W * 2 * len(columns), H * len(WEAPONS)))
    draw = ImageDraw.Draw(sheet)
    for row, name in enumerate(WEAPONS):
        weapon = frame('main', name)
        for column, steps in enumerate(columns):
            picture = Image.new('RGB', (W, H), (58, 70, 40))
            if steps:
                r, g, b, a = GLOW[steps]
                for radius, strength, whiteness in RINGS:
                    tint = [c + (1 - c) * whiteness for c in (r, g, b)]
                    for ox, oy in OFFSETS:
                        picture = ImageChops.add(picture, light(solid(weapon, ox * radius * PPU, oy * radius * PPU),
                                                                tint, a * strength))
            picture = picture.convert('RGBA')
            for image, px, py in [weapon]:
                picture.alpha_composite(image, (int(FEET_X - px), int(FEET_Y - py)))
            if steps:
                picture = ImageChops.add(picture.convert('RGB'), light(solid(weapon), (r, g, b), a * SHEEN)).convert('RGBA')
            for image, px, py in body:
                picture.alpha_composite(image, (int(FEET_X - px), int(FEET_Y - py)))
            big = picture.convert('RGB').resize((W * 2, H * 2), Image.NEAREST)
            sheet.paste(big.crop((0, 40, W * 2, 40 + H)), (column * W * 2, row * H))
        draw.text((4, row * H + 4), name, fill=(255, 255, 255))
    for column, steps in enumerate(columns):
        draw.text((column * W * 2 + W - 10, 2), f'+{steps}', fill=(255, 255, 0))
    sheet.save(out)
    print(out)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'glow_preview.png')
