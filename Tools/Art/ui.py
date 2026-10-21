"""Cuts the UI art out of "RPG UI 1" by System G6 / Qoma (CC0, https://opengameart.org/content/rpg-ui-1).

    python3 -I Tools/Art/ui.py <path to the extracted rpg_ui folder>

Writes Assets/_Project/Resources/UI/Rpg/*.png (white or full colour, nine-sliced by the scene builder, ImportUiFrames):
  Window.png        grey stone window with a steel frame (window_stash), the notches in the middle of its edges
                    smoothed away so it stretches
  Parchment.png     parchment window with the same frame (window_dialogue), for what NPCs say
  Tooltip.png       dark box with a thin steel frame (window_message_box), for tooltips and list rows
  Button.png        steel button; ButtonHover.png the golden one (button_rectangle_set)
  Close/Left/Right/Plus/Minus(.png, Hover.png)   square steel buttons and their golden hover (button_square_set)
  Slot.png          empty item slot: dark well with a steel frame (slot_default_set + slot_border_set); SlotFrame.png
                    the frame alone, drawn over a slot's own colour
  SlotHead.png ...  the empty equipment slots' silhouettes (slot_default_set)
  BarFrame.png      a bar's steel frame; BarFill.png a white, shaded fill tinted in code (bar_rectangle_set)
"""
import os
import sys

from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Resources', 'UI', 'Rpg')
CELL = 64


def smooth_edges(image, border):
    """Rebuilds the four edges from one column/row near the corners, so the nine-sliced edges stretch evenly."""
    w, h = image.size
    out = image.copy()
    column = image.crop((border * 2, 0, border * 2 + 1, h))
    for x in range(border, w - border):
        top = column.crop((0, 0, 1, border))
        bottom = column.crop((0, h - border, 1, h))
        out.paste(top, (x, 0))
        out.paste(bottom, (x, h - border))
    row = image.crop((0, border * 2, w, border * 2 + 1))
    for y in range(border, h - border):
        out.paste(row.crop((0, 0, border, 1)), (0, y))
        out.paste(row.crop((w - border, 0, w, 1)), (w - border, y))
    return out


def cell(image, column, row, size=CELL):
    return image.crop((column * size, row * size, column * size + size, row * size + size))


def save(image, name):
    image.save(os.path.join(OUT, name + '.png'))
    print(name, image.size)


def main(root):
    elements = os.path.join(root, 'elements')
    load = lambda name: Image.open(os.path.join(elements, name)).convert('RGBA')
    os.makedirs(OUT, exist_ok=True)

    save(smooth_edges(load('window_stash.png'), 26), 'Window')
    save(smooth_edges(load('window_dialogue.png'), 26), 'Parchment')
    save(smooth_edges(load('window_message_box.png'), 10), 'Tooltip')

    buttons = load('button_rectangle_set.png')
    save(buttons.crop((0, 0, 256, 32)), 'Button')
    save(buttons.crop((256, 0, 512, 32)), 'ButtonHover')

    squares = load('button_square_set.png')
    for name, normal, hover in [('Close', (4, 0), (1, 1)), ('Left', (5, 0), (2, 1)), ('Right', (6, 0), (3, 1)),
                                ('Plus', (7, 0), (4, 1)), ('Minus', (8, 0), (5, 1))]:
        save(cell(squares, *normal), name)
        save(cell(squares, *hover), name + 'Hover')

    wells = load('slot_default_set.png')
    frames = load('slot_border_set.png')
    slot = cell(wells, 0, 0)
    slot.alpha_composite(cell(frames, 0, 0))
    save(slot, 'Slot')
    save(cell(frames, 0, 0), 'SlotFrame')
    for index, name in enumerate(['Head', 'Chest', 'Shoulders', 'Neck', 'Hands', 'Wrist', 'Feet', 'Legs', 'Weapon', 'Shield'], 1):
        silhouette = cell(wells, index, 0)
        silhouette.alpha_composite(cell(frames, 0, 0))
        save(silhouette, 'Slot' + name)

    bars = load('bar_rectangle_set.png')
    save(smooth_edges(bars.crop((0, 128, 512, 192)), 12), 'BarFrame')
    # The bright red fill, made white (its shading kept) so code can tint it any colour.
    fill = bars.crop((0, 384, 512, 448))
    grey = fill.convert('LA').convert('RGBA')
    pixels = grey.load()
    for y in range(grey.height):
        for x in range(grey.width):
            r, g, b, a = pixels[x, y]
            v = min(255, int(r * 1.6 + 40))
            pixels[x, y] = (v, v, v, fill.getpixel((x, y))[3])
    save(grey, 'BarFill')


if __name__ == '__main__':
    main(sys.argv[1])
