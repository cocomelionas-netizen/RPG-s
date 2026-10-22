"""Paints the lakes' water tiles: Assets/_Project/Art/Water/Water.png.

    python3 -I Tools/Art/water.py

256 isometric tiles of 128 x 64 pixels (one cell, 16 x 16 in a 2048 x 1024 atlas), drawn over the grass on a tilemap
of their own: water that is deeper and darker away from the shore, soft ripples, and where a neighbour cell
is land a bank of wet sand and a line of foam that fade into the grass. Tile index = the land edges (1 north-east =
cell x + 1, 2 north-west = y + 1, 4 south-west = x - 1, 8 south-east = y - 1) + the land corners whose two edges are
water (16 top = x + 1, y + 1; 32 left = x - 1, y + 1; 64 bottom = x - 1, y - 1; 128 right = x + 1, y - 1).
The ripples and the shore's wobble repeat with the cell lattice, so neighbouring tiles meet without a seam.
"""
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Water')
W, H = 128, 64
SS = 3  # supersampling

DEEP = np.array([22, 60, 92], float)
SHALLOW = np.array([52, 118, 140], float)
FOAM = np.array([214, 232, 228], float)
SAND = np.array([176, 156, 108], float)
WET = np.array([120, 112, 78], float)

# Waves periodic on the cell lattice: (frequency in u, frequency in v, amplitude, phase).
WAVES = [(1, 0, 0.35, 0.3), (0, 1, 0.35, 1.7), (1, 1, 0.25, 2.9), (2, -1, 0.2, 0.8), (-1, 2, 0.2, 4.1), (3, 1, 0.12, 2.2),
         (1, -3, 0.12, 5.0), (2, 3, 0.08, 1.1)]
WOBBLE = [(2, 1, 1.0, 0.4), (1, 2, 1.0, 2.0), (3, -2, 0.6, 3.3), (-2, 3, 0.6, 5.5)]


def lattice(f, u, v):
    total = np.zeros_like(u)
    for m, n, amp, phase in f:
        total += amp * np.sin(2 * math.pi * (m * u + n * v) + phase)
    return total


def tile(mask):
    px, py = np.meshgrid((np.arange(W * SS) + 0.5) / SS, (np.arange(H * SS) + 0.5) / SS)
    a = (px - W / 2) / (W / 2)
    b = (py - H / 2) / (H / 2)
    # A little past the diamond, so neighbouring tiles overlap (their water is the same there) and no seam shows.
    inside = np.abs(a) + np.abs(b) <= 1.0 + 3.0 / 32
    # Cell coordinates of the pixel (u along x, v along y of the grid), for the lattice-periodic noise.
    u = a / 2 - b / 2
    v = -a / 2 - b / 2

    # Distance (0 at the edge, 0.5 in the middle) to every land edge and land corner.
    edges = {1: (1 - (a - b)) / 2, 2: (1 - (-a - b)) / 2, 4: (1 - (b - a)) / 2, 8: (1 - (a + b)) / 2}
    corners = {16: (0.0, -1.0), 32: (-1.0, 0.0), 64: (0.0, 1.0), 128: (1.0, 0.0)}
    near = []
    for bit, distance in edges.items():
        if mask & bit:
            near.append(distance)
    for bit, (ca, cb) in corners.items():
        if mask & bit:
            # In the same units as the edges: half the diamond's own half-diagonal.
            near.append(np.hypot(a - ca, (b - cb)) / 2)
    # A soft minimum: where two land edges meet, the shore rounds the corner off.
    soft = 0.07
    land = -soft * np.log(sum(np.exp(-d / soft) for d in near)) if near else np.full(a.shape, 9.0)
    shore = land + 0.035 * lattice(WOBBLE, u, v) / 2.2

    ripples = lattice(WAVES, u, v)
    depth = np.clip((shore - 0.12) / 0.4, 0, 1)
    colour = SHALLOW[None, None, :] * (1 - depth[..., None]) + DEEP[None, None, :] * depth[..., None]
    colour = colour * (1 + 0.04 * ripples[..., None])

    # The shore: grass fading into wet sand, sand, then a line of foam at the water's edge.
    sand = np.clip((0.16 - shore) / 0.03, 0, 1)
    wet = np.clip((0.1 - shore) / 0.04, 0, 1)
    colour = colour * (1 - sand[..., None]) + SAND[None, None, :] * sand[..., None]
    colour = colour * (1 - wet[..., None] * 0.6) + WET[None, None, :] * wet[..., None] * 0.6
    # The water just off the shore is lighter.
    light = np.clip(1 - (shore - 0.16) / 0.07, 0, 1) * (shore > 0.16)
    colour = colour * (1 - light[..., None] * 0.18) + np.array([110, 170, 170], float)[None, None, :] * light[..., None] * 0.18
    foam = np.clip(1 - np.abs(shore - 0.16) / 0.022, 0, 1) * (0.7 + 0.3 * np.clip(ripples, 0, 1))
    colour = colour * (1 - foam[..., None] * 0.8) + FOAM[None, None, :] * foam[..., None] * 0.8
    alpha = np.clip((shore - 0.01) / 0.07, 0, 1) ** 1.5
    alpha[~inside] = 0

    rgba = np.dstack([np.clip(colour, 0, 255), alpha * 255]).astype(np.uint8)
    return Image.fromarray(rgba, 'RGBA').resize((W, H), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    atlas = Image.new('RGBA', (16 * W, 16 * H), (0, 0, 0, 0))
    for mask in range(256):
        # From the bottom left, as Unity counts rects: tile 0 at the bottom left.
        x, row = mask % 16, mask // 16
        atlas.paste(tile(mask), (x * W, (15 - row) * H))
    atlas.save(os.path.join(OUT, 'Water.png'), optimize=True)
    print('water: 256 tiles')


if __name__ == '__main__':
    main()
