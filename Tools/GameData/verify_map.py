"""Checks the generated world against the source game's own world map picture, zone by zone.

    python3 -I Tools/GameData/verify_map.py <path to the source data checkout> [output.png]

For every cell of WorldMapData the source position is found (island.source_of) and the source world map picture
(content/interface.zip, mapquester_map.png, pixel = (614 + 0.855 (x - y), 16 + 0.4275 (x + y))) is sampled there:
blue is water. Each zone gets a water agreement score (cells where both say water or both say land). The map labels
(StartAreaData.MapLabels) are checked to stand in the zone they name (or the zone listed in LABEL_ZONES), and the
waypoints on walkable ground. Optionally writes a side-by-side picture: the source map and ours drawn the same way.
Exits 1 when a zone is under 95 %, a label is in the wrong zone or a waypoint is on a blocked cell. Needs Pillow.
"""
import io
import os
import re
import sys
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import island  # noqa: E402

EDITOR = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Scripts', 'Editor')
THRESHOLD = 95.0

# Map labels that are not zones of their own: the zone each one stands in (a dungeon entrance, a camp, a small area).
LABEL_ZONES = {
    'Trog Cave': 'Fields of Dreams',
    "Atmar's Fall": None,
    "Morgall's Camp": 'Kandorys Castle',
    'Deserters': 'Kandorys Beach',
    'Wyrm Thicket': 'Thicket of Wyrms',
    'Hollow Fields': 'The Hollow Fields',
}


def to_pixel(x, y):
    """A source position on the source world map picture (mapquester_map.png)."""
    return int(round(614 + 0.855 * (x - y))), int(round(16 + 0.4275 * (x + y)))


def load_rows():
    text = open(os.path.join(EDITOR, 'WorldMapData.cs')).read()
    return re.findall(r'^\s+"([.#TW]+)",$', text, re.M)


def load_area():
    text = open(os.path.join(EDITOR, 'StartAreaData.cs')).read()
    names = re.findall(r'^            "([^"]+)",$', text[text.index('ZoneNames'):text.index('ZoneBlocks')], re.M)
    blocks = re.findall(r'^            "([A-Z.]+)",$', text[text.index('ZoneBlocks ='):text.index('MapLabels')], re.M)
    labels = re.findall(r'\("([^"]+)", "([^"]*)", new Vector3Int\((\d+), (\d+), 0\), (true|false)\)', text)
    places = re.findall(r'\("([^"]+)", new Vector3Int\((\d+), (\d+), 0\)\),', text)
    block = int(re.search(r'ZoneBlock = (\d+);', text).group(1))
    return names, blocks, labels, places, block


def picture(root):
    from PIL import Image  # Pillow, only needed here
    with zipfile.ZipFile(os.path.join(root, 'content', 'interface.zip')) as archive:
        name = next(n for n in archive.namelist() if n.endswith('mapquester_map.png'))
        return Image.open(io.BytesIO(archive.read(name))).convert('RGBA')


def main(root, out=None):
    rows = load_rows()
    names, blocks, labels, places, block = load_area()
    pic = picture(root)
    px = pic.load()

    def zone_of(cx, cy):
        row, column = cy // block, cx // block
        if row >= len(blocks) or column >= len(blocks[row]) or blocks[row][column] == '.':
            return None
        return names[ord(blocks[row][column]) - ord('A')]

    def blue(x, y):
        sx, sy = to_pixel(x, y)
        if not (0 <= sx < pic.width and 0 <= sy < pic.height):
            return None
        r, g, b, a = px[sx, sy]
        if a <= 100:
            return None
        return b > r + 10 and b >= g - 5

    stats = {}
    for cy, row in enumerate(rows):
        for cx, kind in enumerate(row):
            x, y = island.source_of(cx, cy)
            water = blue(x + 0.5, y + 0.5)
            if water is None:
                continue
            zone = zone_of(cx, cy) or '(no zone)'
            same, total = stats.get(zone, (0, 0))
            stats[zone] = (same + ((kind == 'W') == water), total + 1)

    failed = False
    print(f'{"zone":28s} {"cells":>7s} {"water agrees":>13s}')
    for zone, (same, total) in sorted(stats.items(), key=lambda item: item[1][0] / max(1, item[1][1])):
        score = 100.0 * same / max(1, total)
        # Cells outside every zone (the coast, the island's edge) are shown but do not fail the check.
        counted = zone != '(no zone)'
        mark = '' if score >= THRESHOLD else ('   <-- check' if counted else '   (outside the zones, not counted)')
        failed |= score < THRESHOLD and counted
        print(f'{zone:28s} {total:7d} {score:12.1f}%{mark}')

    print()
    for title, _detail, cx, cy, _town in labels:
        zone = zone_of(int(cx), int(cy))
        expected = LABEL_ZONES[title] if title in LABEL_ZONES else title
        ok = zone == expected
        failed |= not ok
        print(f'label {title:22s} in zone {zone or "-":26s} {"ok" if ok else "WRONG ZONE (expected " + str(expected) + ")"}')
    for name, cx, cy in places:
        kind = rows[int(cy)][int(cx)]
        failed |= kind != '.'
        print(f'waypoint {name:24s} on {"walkable ground" if kind == "." else "BLOCKED cell " + kind}')

    if out:
        draw_side_by_side(pic, rows, out)
        print(f'\nwrote {out}')
    return 1 if failed else 0


def draw_side_by_side(pic, rows, out):
    """The source map next to ours, drawn with the same isometric projection (cell centres, like the score)."""
    from PIL import Image, ImageDraw
    ours = Image.new('RGBA', pic.size, (0, 0, 0, 0))
    o = ours.load()
    colors = {'.': (104, 128, 64, 255), '#': (96, 84, 70, 255), 'T': (40, 70, 34, 255), 'W': (50, 80, 140, 255)}
    for cy, row in enumerate(rows):
        for cx, kind in enumerate(row):
            x, y = island.source_of(cx, cy)
            sx, sy = to_pixel(x + 0.5, y + 0.5)
            if 0 <= sx < ours.width and 0 <= sy < ours.height:
                o[sx, sy] = colors[kind]
    side = Image.new('RGBA', (pic.width * 2, pic.height), (16, 16, 16, 255))
    side.alpha_composite(pic, (0, 0))
    side.alpha_composite(ours, (pic.width, 0))
    d = ImageDraw.Draw(side)
    d.text((10, 10), 'source world map', fill=(255, 255, 255, 255))
    d.text((pic.width + 10, 10), 'this project (WorldMapData)', fill=(255, 255, 255, 255))
    side.convert('RGB').save(out)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None))
