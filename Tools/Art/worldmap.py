"""Paints the world map (M) and its icons.

    python3 -I Tools/Art/worldmap.py <game-icons.net SVG folder>

Writes Assets/_Project/Resources/UI/Map/:
  World.png     the island painted from its ground (Scripts/Editor/WorldMapData.cs): meadows with a soft relief, woods,
                lakes with shores, walls and rocks, a ragged coast and the sea around it. It covers exactly the world
                rectangle of the island's ground (WorldNavigation.WorldBounds: the isometric cells 0..Width-1 x
                0..Height-1, cell (x, y) centred at ((x - y) / 2, (x + y + 1) / 4)), so the map and the minimap put it
                under their markers as it is.
  <Kind>.png    round badges for the map's markers: a coloured disc in a golden rim with a white glyph from
                game-icons.net (CC-BY 3.0, credits in Resources/UI/CREDITS.md); the glyphs are read from the folder of
                their SVGs (<author>-<name>.svg or <name>.svg, from github.com/game-icons/icons). Player.png is an arrow
                pointing up, Pin.png a marker pin.
Needs numpy, scipy, Pillow and cairosvg.
"""
import io
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'UI', 'Map')
EDITOR = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Editor')

PICTURE = (2048, 1024)
SEED = 7

# Colours (0-255 RGB).
SEA_DEEP = (22, 44, 66)
SEA_SHALLOW = (52, 104, 128)
FOAM = (196, 214, 206)
SAND = (198, 178, 128)
MEADOW = (112, 138, 70)
MEADOW_DRY = (150, 148, 84)
FOREST = (44, 76, 40)
FOREST_LIGHT = (70, 104, 52)
LAKE_DEEP = (34, 72, 108)
LAKE = (62, 112, 146)
STONE = (126, 118, 104)
STONE_DARK = (58, 52, 46)

# Marker badges: file, glyph, disc colour.
BADGES = [
    ('Town', 'castle', (178, 132, 40)),
    ('Dungeon', 'cave-entrance', (104, 62, 150)),
    ('Boss', 'crowned-skull', (160, 34, 30)),
    ('Waystone', 'crystal-cluster', (32, 132, 150)),
    ('Weapons', 'crossed-swords', (120, 84, 52)),
    ('Armor', 'visored-helm', (96, 96, 110)),
    ('Potions', 'potion-ball', (52, 128, 70)),
    ('Gems', 'gem-pendant', (40, 92, 160)),
    ('Food', 'beer-stein', (150, 96, 40)),
    ('Bank', 'two-coins', (170, 136, 30)),
    ('Transmute', 'cauldron', (60, 120, 104)),
    ('Trainer', 'open-book', (56, 76, 150)),
    ('Healer', 'ankh', (170, 150, 80)),
    ('Goods', 'swap-bag', (128, 90, 60)),
    ('Person', 'conversation', (84, 92, 100)),
    ('Place', 'compass', (110, 86, 54)),
]


def read_rows():
    text = open(os.path.join(EDITOR, 'WorldMapData.cs')).read()
    rows = re.findall(r'^\s*"([.#TW]*)",?\s*$', text, re.M)
    size = open(os.path.join(EDITOR, 'StartAreaData.cs')).read()
    width = int(re.search(r'const int Width = (\d+);', size).group(1))
    height = int(re.search(r'const int Height = (\d+);', size).group(1))
    return [r.ljust(width, '#')[:width] for r in rows[:height]], width, height


def noise(shape, scales, rng):
    """Smooth value noise in [0, 1]: random grids of several sizes scaled up and added."""
    total = np.zeros(shape)
    weight = 0.0
    for cells, amount in scales:
        grid = rng.random((max(2, shape[0] // cells), max(2, shape[1] // cells)))
        up = ndimage.zoom(grid, (shape[0] / grid.shape[0], shape[1] / grid.shape[1]), order=3)[:shape[0], :shape[1]]
        total[:up.shape[0], :up.shape[1]] += up * amount
        weight += amount
    total /= weight
    return (total - total.min()) / max(1e-6, total.max() - total.min())


def sample(field, u, v, outside=0.0):
    """Bilinear sample of a cell-space field (rows y, columns x) at fractional cells (u = x, v = y)."""
    return ndimage.map_coordinates(field, [v - 0.5, u - 0.5], order=1, mode='constant', cval=outside)


def mix(a, b, t):
    t = np.clip(t, 0, 1)[..., None]
    return a * (1 - t) + np.asarray(b, dtype=float) * t


def paint(rows, width, height):
    rng = np.random.default_rng(SEED)
    kinds = np.array([list(r) for r in rows])
    tree = (kinds == 'T').astype(float)
    wall = (kinds == '#').astype(float)
    wall[:1, :] = wall[-1:, :] = 0  # the ground's own frame is the coast, not a wall
    wall[:, :1] = wall[:, -1:] = 0
    water = (kinds == 'W').astype(float)

    # The world rectangle (WorldNavigation.WorldBounds) and every pixel's fractional cell.
    x_min, x_max = (0 - (height - 1)) * 0.5 - 0.5, (width - 1) * 0.5 + 0.5
    y_min, y_max = 0.0, (width - 1 + height - 1 + 1) * 0.25 + 0.25
    w, h = PICTURE
    px, py = np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)
    wx = x_min + px / w * (x_max - x_min)
    wy = y_max - py / h * (y_max - y_min)
    u = wx + 2 * wy
    v = 2 * wy - wx

    # A ragged coast a little outside the ground: land where the distance past the edge is under a noisy margin.
    past = np.maximum.reduce([-u, u - width, -v, v - height, np.zeros_like(u)])
    coast_noise = sample(noise((height, width), [(90, 1.0), (30, 0.6), (8, 0.3)], rng), np.clip(u, 1, width - 1),
                         np.clip(v, 1, height - 1), 0.5)
    margin = 5.0 + 22.0 * coast_noise ** 1.3
    land = np.clip((margin - past) / 1.5, 0, 1)

    # Fields in cell space, blurred so the shapes are soft, then sampled for every pixel.
    def field(mask, sigma):
        return sample(ndimage.gaussian_filter(mask, sigma), u, v)

    forest = field(tree, 1.2)
    forest_wide = field(tree, 4.0)
    lake = field(water, 1.0)
    lake_depth = field(water, 3.0)
    walls = field(wall, 0.6)
    walls_shadow = sample(ndimage.gaussian_filter(wall, 1.2), u + 1.2, v + 1.2)
    relief = noise((height, width), [(60, 1.0), (20, 0.6), (6, 0.25)], rng)
    height_field = sample(relief, u, v, 0.5)
    dry = sample(noise((height, width), [(90, 1.0), (30, 0.5)], rng), u, v, 0.5)
    grain = rng.random((h, w))

    # Sea: deep away from the coast, shallow by it, faint waves.
    coast_distance = ndimage.distance_transform_edt(land < 0.5) / (w / (x_max - x_min))
    sea = mix(np.full((h, w, 3), SEA_SHALLOW, dtype=float), SEA_DEEP, coast_distance / 10.0)
    waves = (np.sin(coast_distance * 2.4 + grain * 0.6) > 0.93) & (coast_distance > 1.5) & (coast_distance < 9)
    sea = mix(sea, (90, 140, 160), waves * 0.35 * np.clip(1 - coast_distance / 9, 0, 1))

    # Land: meadows from green to dry, a hillshade from the relief (light from the top left), sand by the sea.
    ground = mix(np.full((h, w, 3), MEADOW, dtype=float), MEADOW_DRY, (dry - 0.45) * 2.2)
    gy, gx = np.gradient(height_field)
    shade = np.clip((-gx - gy) * 40.0, -1, 1)
    ground = ground * (1 + 0.16 * shade[..., None])
    sand = np.clip(1.0 - (margin - past) / 2.5, 0, 1) * (past > 0)
    ground = mix(ground, SAND, sand)

    # Woods: a darker canopy over the wide tree field, stippled with tree tops on the dense parts.
    canopy = np.clip(forest * 1.6, 0, 1)
    ground = mix(ground, FOREST_LIGHT, np.clip(forest_wide * 2.2, 0, 0.55))
    ground = mix(ground, FOREST, canopy * 0.9)
    tops = (grain > 0.82) & (canopy > 0.35)
    ground = mix(ground, (96, 130, 66), tops * 0.6)
    ground = mix(ground, (24, 44, 22), ((grain < 0.12) & (canopy > 0.35)) * 0.5)

    # Lakes with a light shore line and deeper middles.
    shore = np.clip(1 - np.abs(lake - 0.42) / 0.08, 0, 1)
    ground = mix(ground, SAND, np.clip((lake - 0.22) / 0.15, 0, 1) * 0.7)
    lake_colour = mix(np.full((h, w, 3), LAKE, dtype=float), LAKE_DEEP, np.clip(lake_depth * 1.4 - 0.3, 0, 1))
    ground = mix(ground, lake_colour, np.clip((lake - 0.42) / 0.06, 0, 1))
    ground = mix(ground, FOAM, shore * 0.35)

    # Walls and rocks: a shadow to the lower right, the stone over it with a dark edge.
    ground = mix(ground, (30, 34, 24), np.clip(walls_shadow * 1.5, 0, 0.45))
    edge = np.clip(1 - np.abs(walls - 0.3) / 0.12, 0, 1)
    ground = mix(ground, STONE, np.clip((walls - 0.3) / 0.15, 0, 1))
    ground = mix(ground, STONE_DARK, edge * 0.8)

    picture = sea * (1 - land[..., None]) + ground * land[..., None]
    # The coast line itself, dark and thin.
    coast_line = np.clip(1 - np.abs(land - 0.5) / 0.35, 0, 1)
    picture = mix(picture, (40, 46, 34), coast_line * 0.55)

    # Paper: a fine grain, a warm grade and a soft vignette.
    picture = picture * (0.96 + 0.08 * grain[..., None])
    picture = mix(picture, (150, 128, 90), 0.08)
    vy, vx = np.meshgrid(np.linspace(-1, 1, h), np.linspace(-1, 1, w), indexing='ij')
    vignette = np.clip((vx ** 2 + vy ** 2 - 0.55) / 1.4, 0, 1)
    picture = mix(picture, SEA_DEEP, vignette * (1 - land) * 0.6)
    image = Image.fromarray(np.clip(picture, 0, 255).astype(np.uint8), 'RGB')
    return image.filter(ImageFilter.UnsharpMask(radius=1.2, percent=40, threshold=2))


def glyph(folder, name, size):
    import cairosvg
    candidates = [f for f in os.listdir(folder) if f == name + '.svg' or f.endswith('-' + name + '.svg')]
    if not candidates:
        raise SystemExit(f'missing glyph {name}.svg in {folder}')
    svg = open(os.path.join(folder, candidates[0])).read()
    svg = svg.replace('<path d="M0 0h512v512H0z"/>', '')  # the black square behind the glyph
    data = cairosvg.svg2png(bytestring=svg.encode(), output_width=size, output_height=size)
    image = Image.open(io.BytesIO(data)).convert('RGBA')
    alpha = image.convert('L')  # the glyph is white
    white = Image.new('RGBA', image.size, (255, 255, 255, 0))
    white.putalpha(alpha)
    return white


def badge(folder, glyph_name, colour, size=64):
    s = size * 4
    image = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    shadow = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((s * 0.06, s * 0.1, s * 0.98, s * 0.98), fill=(0, 0, 0, 170))
    image.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(s * 0.03)))
    # A dark edge, a golden rim and a bronze groove.
    draw.ellipse((s * 0.04, s * 0.04, s * 0.92, s * 0.92), fill=(70, 46, 16, 255))
    draw.ellipse((s * 0.06, s * 0.06, s * 0.90, s * 0.90), fill=(232, 196, 110, 255))
    draw.ellipse((s * 0.10, s * 0.10, s * 0.86, s * 0.86), fill=(120, 82, 26, 255))
    # The disc: a radial gradient of the kind's colour.
    disc = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    centre = np.array([s * 0.42, s * 0.40])
    yy, xx = np.mgrid[0:s, 0:s]
    d = np.sqrt((xx - centre[0]) ** 2 + (yy - centre[1]) ** 2) / (s * 0.45)
    base = np.array(colour, dtype=float)
    rgb = base[None, None, :] * (1.25 - 0.55 * np.clip(d, 0, 1))[..., None]
    disc_array = np.dstack([np.clip(rgb, 0, 255), np.full((s, s), 255.0)]).astype(np.uint8)
    disc = Image.fromarray(disc_array, 'RGBA')
    mask = Image.new('L', (s, s), 0)
    ImageDraw.Draw(mask).ellipse((s * 0.12, s * 0.12, s * 0.84, s * 0.84), fill=255)
    image.paste(disc, (0, 0), mask)
    # The glyph with a dark shadow.
    g = glyph(folder, glyph_name, int(s * 0.5))
    offset = (int(s * 0.23), int(s * 0.23))
    dark = Image.new('RGBA', g.size, (0, 0, 0, 0))
    dark.putalpha(g.getchannel('A').point(lambda a: a * 0.75))
    image.alpha_composite(dark.filter(ImageFilter.GaussianBlur(s * 0.012)), (offset[0] + int(s * 0.012), offset[1] + int(s * 0.02)))
    image.alpha_composite(g, offset)
    # A soft highlight on the top of the disc.
    shine = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(shine).ellipse((s * 0.2, s * 0.14, s * 0.76, s * 0.44), fill=(255, 255, 255, 38))
    image.alpha_composite(shine.filter(ImageFilter.GaussianBlur(s * 0.03)))
    return image.resize((size, size), Image.LANCZOS)


def arrow(size=64):
    s = size * 4
    image = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    points = [(s * 0.5, s * 0.06), (s * 0.86, s * 0.9), (s * 0.5, s * 0.7), (s * 0.14, s * 0.9)]
    shadow = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).polygon([(x + s * 0.03, y + s * 0.04) for x, y in points], fill=(0, 0, 0, 180))
    image.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(s * 0.03)))
    draw = ImageDraw.Draw(image)
    draw.polygon(points, fill=(255, 236, 160, 255), outline=(60, 36, 8, 255), width=int(s * 0.05))
    draw.polygon([points[0], points[1], points[2]], fill=(214, 160, 48, 255))
    draw.line(points + [points[0]], fill=(60, 36, 8, 255), width=int(s * 0.05), joint='curve')
    return image.resize((size, size), Image.LANCZOS)


def pin(size=64):
    s = size * 4
    image = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.polygon([(s * 0.26, s * 0.42), (s * 0.74, s * 0.42), (s * 0.5, s * 0.96)], fill=(60, 20, 10, 255))
    draw.ellipse((s * 0.2, s * 0.06, s * 0.8, s * 0.66), fill=(60, 20, 10, 255))
    draw.polygon([(s * 0.3, s * 0.42), (s * 0.7, s * 0.42), (s * 0.5, s * 0.9)], fill=(220, 70, 40, 255))
    draw.ellipse((s * 0.24, s * 0.1, s * 0.76, s * 0.62), fill=(220, 70, 40, 255))
    draw.ellipse((s * 0.38, s * 0.24, s * 0.62, s * 0.48), fill=(255, 236, 200, 255))
    return image.resize((size, size), Image.LANCZOS)


def main(glyphs):
    os.makedirs(OUT, exist_ok=True)
    rows, width, height = read_rows()
    paint(rows, width, height).save(os.path.join(OUT, 'World.png'), optimize=True)
    for name, glyph_name, colour in BADGES:
        badge(glyphs, glyph_name, colour).save(os.path.join(OUT, name + '.png'))
    arrow().save(os.path.join(OUT, 'Player.png'))
    pin().save(os.path.join(OUT, 'Pin.png'))
    print(f'world map {PICTURE[0]} x {PICTURE[1]}, {len(BADGES)} badges')


if __name__ == '__main__':
    main(sys.argv[1])
