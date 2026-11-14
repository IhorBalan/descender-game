#!/usr/bin/env python3
"""Draws the Tiles V2 terrain: each material's texture, its 47 autotile frames and
slopes, the blend overlays between materials and the wall torch. Not a script.
"""
from __future__ import annotations

import random

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

# --- Textures: the inside of a material as ramp indices (0..5) -----------------


def torus_d(ax, ay, bx, by):
    dx = abs(ax - bx)
    dy = abs(ay - by)
    dx = min(dx, TILE - dx)
    dy = min(dy, TILE - dy)
    return (dx * dx + dy * dy) ** 0.5


def blob(t, r, value, count, size):
    """Soft rounded patches of one ramp index, wrapped round the tile."""
    for _ in range(count):
        cx, cy, rad = r.randrange(TILE), r.randrange(TILE), r.uniform(*size)
        for y in range(TILE):
            for x in range(TILE):
                if torus_d(x, y, cx, cy) < rad:
                    t[y, x] = value


def texture_dirt(seed) -> np.ndarray:
    """Packed earth: a mid brown with a couple of soft darker blotches, a few
    single lit grains with a shade under them and dark specks, kept sparse so
    a field of dirt reads as one surface, not a grid. Index 5 is kept for the
    rims."""
    r = rng("dirt", seed)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    blob(t, r, 2, 2, (1.5, 2.3))
    for _ in range(4):
        x, y = r.randrange(TILE), r.randrange(TILE)
        t[y, x] = 4
        t[(y + 1) % TILE, x] = 2
    for _ in range(4):
        x, y = r.randrange(TILE), r.randrange(TILE)
        if t[y, x] == 3:
            t[y, x] = 1 if r.random() < 0.5 else 2
    return t


def texture_stone(seed, cells: int = 4) -> np.ndarray:
    """Rock chunks: cells of a wrapped Voronoi, dark cracks between them, each
    chunk lit on its top-left rim and shaded on its bottom-right. Index 5 is
    kept for the rims."""
    r = rng("stone", seed, cells)
    points = [(r.uniform(0, TILE), r.uniform(0, TILE), r.choice((2, 3, 3))) for _ in range(cells)]
    owner = np.zeros((TILE, TILE), dtype=np.int16)
    crack = np.zeros((TILE, TILE), dtype=bool)
    for y in range(TILE):
        for x in range(TILE):
            d = sorted((torus_d(x + 0.5, y + 0.5, px, py), i) for i, (px, py, _) in enumerate(points))
            owner[y, x] = d[0][1]
            crack[y, x] = d[1][0] - d[0][0] < 0.8
    t = np.zeros((TILE, TILE), dtype=np.int16)
    for y in range(TILE):
        for x in range(TILE):
            if crack[y, x]:
                t[y, x] = 1
                continue
            v = points[owner[y, x]][2]
            if crack[(y - 1) % TILE, x]:
                v += 1
            elif crack[(y + 1) % TILE, x] or crack[y, (x + 1) % TILE]:
                v -= 1
            t[y, x] = max(1, min(4, v))
    for _ in range(3):
        x, y = r.randrange(TILE), r.randrange(TILE)
        if t[y, x] > 2:
            t[y, x] -= 1
    return t


def texture_bricks(seed) -> np.ndarray:
    """Rough stone blocks for a wall: two courses a tile, offset, each block lit
    on top and shaded below, its own shade, worn by a few chips."""
    r = rng("bricks", seed)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    shades = {}
    for y in range(TILE):
        course = y // 8
        row = y % 8
        for x in range(TILE):
            xx = (x + (8 if course % 2 else 0)) % TILE
            col = xx % 8
            block = (course, xx // 8)
            base = shades.setdefault(block, r.choice([2, 3, 3, 4]))
            if row == 7 or col == 7:
                t[y, x] = 0
            elif row == 0 or col == 0:
                t[y, x] = min(base + 1, 5)
            elif row == 6 or col == 6:
                t[y, x] = base - 1
            else:
                t[y, x] = base
    for _ in range(5):
        x, y = r.randrange(TILE), r.randrange(TILE)
        if t[y, x] >= 2:
            t[y, x] -= 1
    return t


def texture_rubble(seed) -> np.ndarray:
    """Big rough blocks for a stone wall: the stone chunks, fewer and larger."""
    return texture_stone(seed + 11, cells=3)


def texture_sand(seed) -> np.ndarray:
    """Loose sand: a light field of single grains a step darker or lighter,
    and a couple of short wind ripples, shaded under."""
    r = rng("sand", seed)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    for y in range(TILE):
        for x in range(TILE):
            roll = r.random()
            if roll < 0.14:
                t[y, x] = 2
            elif roll < 0.21:
                t[y, x] = 4
    for _ in range(2):
        x0, y0, n = r.randrange(TILE), r.randrange(TILE), r.randint(3, 5)
        for k in range(n):
            t[y0, (x0 + k) % TILE] = 4
            t[(y0 + 1) % TILE, (x0 + k) % TILE] = 2
    return t


def texture_clay(seed) -> np.ndarray:
    """Layered clay: wavy darker strata twice a tile (so it wraps), a few lit
    pebbles with a shade under them."""
    r = rng("clay", seed)
    phase = r.uniform(0, 6.283)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    for y in range(TILE):
        for x in range(TILE):
            wob = int(round(1.2 * np.sin(2 * np.pi * x / TILE + phase)))
            band = (y + wob) % 8
            if band < 2:
                t[y, x] = 2
            elif band == 2:
                t[y, x] = 4 if (x + y) % 3 == 0 else 3
    for _ in range(3):
        x, y = r.randrange(TILE), r.randrange(TILE)
        t[y, x] = 4
        t[(y + 1) % TILE, x] = 1
    return t


TEXTURES = {
    "dirt": texture_dirt,
    "stone": texture_stone,
    "bricks": texture_bricks,
    "rubble": texture_rubble,
    "sand": texture_sand,
    "clay": texture_clay,
}


def ore_mask(seed) -> np.ndarray:
    """Where an ore tile's nuggets are: two or three clusters of a few pixels
    each, wrapped round the tile."""
    r = rng("ore", seed)
    mask = np.zeros((TILE, TILE), dtype=bool)
    for _ in range(r.choice([3, 3, 4])):
        x, y = r.randrange(TILE), r.randrange(TILE)
        for _ in range(r.randint(6, 11)):
            mask[y % TILE, x % TILE] = True
            mask[(y + 1) % TILE, x % TILE] = True
            mask[y % TILE, (x + 1) % TILE] = True
            x += r.choice([-1, 0, 1, 1])
            y += r.choice([-1, 0, 1])
    return mask


# --- Frames -------------------------------------------------------------------


def neighbourhood(mask: int, center: np.ndarray | None = None) -> np.ndarray:
    """A 3 x 3 tile canvas: the middle tile (full, or `center`) and its eight
    neighbours, each full where the mask says it is solid."""
    solid = np.zeros((TILE * 3, TILE * 3), dtype=bool)
    solid[TILE : 2 * TILE, TILE : 2 * TILE] = True if center is None else center
    for bit, dx, dy in BITS:
        if mask & bit:
            x0, y0 = (dx + 1) * TILE, (dy + 1) * TILE
            solid[y0 : y0 + TILE, x0 : x0 + TILE] = True
    return solid


def notch(solid: np.ndarray, mask: int, r: random.Random, per_edge: int):
    """Cuts 1 px nicks into the middle tile's edges that face air, away from the
    seams, so a long edge is not ruler-straight."""
    c = slice(TILE, 2 * TILE)
    middle = solid[c, c]
    for bit, axis, outer in ((N, 0, 0), (S, 0, TILE - 1), (W, 1, 0), (E, 1, TILE - 1)):
        if mask & bit:
            continue
        for at in seam_safe(r, per_edge):
            for i in range(r.choice([1, 1, 2, 3])):
                p = min(at + i, TILE - 4)
                if axis == 0:
                    # the outermost solid pixel of this column on that side
                    col = middle[:, p]
                    rows = np.nonzero(col)[0]
                    if len(rows):
                        middle[rows[0] if outer == 0 else rows[-1], p] = False
                else:
                    row = middle[p, :]
                    cols = np.nonzero(row)[0]
                    if len(cols):
                        middle[p, cols[0] if outer == 0 else cols[-1]] = False
    solid[c, c] = middle


def shade(solid: np.ndarray, tex: np.ndarray, palette: dict, wall: bool) -> np.ndarray:
    """Colours the middle tile of a canvas: the texture, an outline where it
    meets air, a lit top-left rim and a shaded bottom-right rim."""
    c = slice(TILE, 2 * TILE)
    up, down = reach(solid, 0, -1)[c, c], reach(solid, 0, 1)[c, c]
    left, right = reach(solid, -1, 0)[c, c], reach(solid, 1, 0)[c, c]
    edge = np.zeros((TILE, TILE), dtype=bool)
    for dx, dy in [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]:
        edge |= ~shift(solid, dx, dy, True)[c, c]
    mid = solid[c, c]
    idx = tex.copy()
    checker = (np.add.outer(np.arange(TILE), np.arange(TILE)) % 2) == 0
    if wall:
        # Walls stay low and flat: no highlight, a two-pixel shade at every open edge.
        near = np.minimum(np.minimum(up, down), np.minimum(left, right))
        idx = np.where(near == 2, np.maximum(idx - 2, 0), idx)
        idx = np.where((near == 3) & checker, np.maximum(idx - 1, 0), idx)
    else:
        idx = np.where(right == 2, np.maximum(idx - 1, 1), idx)
        idx = np.where((down == 3) & checker, np.maximum(idx - 1, 1), idx)
        idx = np.where(down == 2, 1, idx)
        idx = np.where(left == 2, np.minimum(idx + 1, 5), idx)
        idx = np.where((up == 3) & checker, np.minimum(idx + 1, 5), idx)
        idx = np.where(up == 2, 5, idx)
    rgba = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    ramp = np.array(palette["ramp"], dtype=np.uint8)
    rgba[..., :3] = ramp[np.clip(idx, 0, 5)]
    rgba[..., 3] = 255
    rgba[edge & mid, :3] = palette["outline"]
    rgba[~mid] = 0
    return rgba


def grass_over(rgba: np.ndarray, solid: np.ndarray, palette: dict, r: random.Random) -> np.ndarray:
    """Grass on the faces open to air: a deep turf on top with blades and drips,
    a thin rim down the sides."""
    c = slice(TILE, 2 * TILE)
    mid = solid[c, c]
    up = reach(solid, 0, -1)[c, c]
    left, right = reach(solid, -1, 0)[c, c], reach(solid, 1, 0)[c, c]
    edge = np.zeros((TILE, TILE), dtype=bool)
    for dx, dy in [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]:
        edge |= ~shift(solid, dx, dy, True)[c, c]
    ramp = palette["ramp"]
    depth = np.full(TILE, 6)
    for x in seam_safe(r, 6, 2, TILE - 3):
        depth[x] = r.choice([7, 8, 8, 9])
    out = rgba.copy()
    for y in range(TILE):
        for x in range(TILE):
            if not mid[y, x]:
                continue
            top = up[y, x]
            side = min(left[y, x], right[y, x])
            if top <= depth[x]:
                if edge[y, x]:
                    colour = palette["outline"]
                elif top == 2:
                    colour = ramp[5]
                elif top == 3:
                    colour = ramp[4] if (x + y) % 2 else ramp[3]
                elif top == depth[x]:
                    colour = ramp[1]
                else:
                    colour = ramp[3] if left[y, x] > 2 else ramp[4]
                out[y, x, :3] = colour
            elif side <= 3:
                if edge[y, x]:
                    out[y, x, :3] = palette["outline"]
                elif side == 3:
                    out[y, x, :3] = ramp[1]
                else:
                    out[y, x, :3] = ramp[4] if left[y, x] == 2 else ramp[2]
    return out


def blade_tips(solid: np.ndarray, mask: int, r: random.Random):
    """Tufts along a top edge open to air: each column of the middle tile is cut
    down by 0 to 2 pixels (1 at the seams, so the next tile's top lines up), so
    blades stand up between dips."""
    if mask & N:
        return
    c = slice(TILE, 2 * TILE)
    middle = solid[c, c]
    cut = [1] * TILE
    x = 2
    while x < TILE - 2:
        cut[x] = 0
        if x + 1 < TILE - 2:
            cut[x + 1] = r.choice([0, 1])
        if x + 2 < TILE - 2:
            cut[x + 2] = 2
        x += r.choice([3, 4, 4, 5])
    for x in range(TILE):
        rows = np.nonzero(middle[:, x])[0]
        for k in range(min(cut[x], len(rows))):
            middle[rows[k], x] = False
    solid[c, c] = middle


def ore_over(rgba: np.ndarray, solid: np.ndarray, palette: dict, variant: int) -> np.ndarray:
    """Nuggets of ore set in the tile: lit on their top-left, shaded on their
    bottom-right, a dark rim cut into the rock under them; never on the outline."""
    c = slice(TILE, 2 * TILE)
    mid = solid[c, c]
    edge = np.zeros((TILE, TILE), dtype=bool)
    for dx, dy in [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]:
        edge |= ~shift(solid, dx, dy, True)[c, c]
    ore = ore_mask(variant) & mid & ~edge
    ramp = palette["ramp"]
    out = rgba.copy()
    for y in range(TILE):
        for x in range(TILE):
            if ore[y, x]:
                above = y > 0 and ore[y - 1, x]
                left = x > 0 and ore[y, x - 1]
                below = y < TILE - 1 and ore[y + 1, x]
                colour = ramp[3]
                if not above:
                    colour = ramp[5]
                elif not left:
                    colour = ramp[4]
                elif not below:
                    colour = ramp[1]
                out[y, x, :3] = colour
            elif mid[y, x] and not edge[y, x] and y > 0 and ore[y - 1, x]:
                out[y, x, :3] = palette["outline"]
    return out


def draw_frame(material: dict, mask: int, variant: int) -> np.ndarray:
    r = rng(material["key"], mask, variant)
    solid = neighbourhood(mask)
    wall = material["kind"] == "wall"
    solid = opened(solid) if not wall else solid
    if not wall and material.get("grass"):
        blade_tips(solid, mask, r)
    tex = TEXTURES[material["texture"]](variant)
    rgba = shade(solid, tex, PALETTES[material["palette"]], wall)
    if material.get("grass"):
        rgba = grass_over(rgba, solid, PALETTES[material["grass"]], r)
    if material.get("ore"):
        rgba = ore_over(rgba, solid, PALETTES[material["ore"]], variant)
    return rgba


def slope_shape(kind: str) -> tuple[np.ndarray, int]:
    """The middle tile of a slope (solid on and below the diagonal) and the mask
    of the neighbours a slope sits among: ground below, the high side solid."""
    ys, xs = np.mgrid[0:TILE, 0:TILE]
    if kind == "up_right":
        center = xs + ys >= TILE - 1
        mask = E | SE | S | SW
    else:
        center = xs <= ys
        mask = W | SW | S | SE
    return center, mask


def draw_slope(material: dict, kind: str, variant: int) -> np.ndarray:
    r = rng(material["key"], kind, variant)
    center, mask = slope_shape(kind)
    solid = opened(neighbourhood(mask, center))
    if material.get("grass"):
        blade_tips(solid, mask, r)
    tex = TEXTURES[material["texture"]](variant)
    rgba = shade(solid, tex, PALETTES[material["palette"]], False)
    if material.get("grass"):
        rgba = grass_over(rgba, solid, PALETTES[material["grass"]], r)
    return rgba


# --- Blends -------------------------------------------------------------------

BLEND_SIDES = [(1, N), (2, E), (4, S), (8, W)]  # overlay bit, neighbour side
BLEND_MASKS = list(range(1, 16))


def draw_blend(blend: dict, sides: int, variant: int) -> np.ndarray:
    """The `from` material reaching a few pixels over each side of the `onto`
    tile that touches it, with a ragged, shaded front and a few loose clumps."""
    r = rng(blend["key"], sides, variant)
    palette = PALETTES[blend["palette"]]
    tex = TEXTURES[blend["texture"]](variant + 7)
    cover = np.zeros((TILE, TILE), dtype=np.int16)  # 0 none, 1 body, 2 front
    for bit, side in BLEND_SIDES:
        if not sides & bit:
            continue
        depth = [3] * TILE
        for at in seam_safe(r, 6, 2, TILE - 3):
            depth[at] = r.choice([4, 5, 5, 6])
        for along in range(TILE):
            for d in range(depth[along] + 1):
                x, y = {N: (along, d), S: (along, TILE - 1 - d), W: (d, along), E: (TILE - 1 - d, along)}[side]
                cover[y, x] = max(cover[y, x], 2 if d == depth[along] else 1)
        for _ in range(3):
            along, d = r.randint(2, TILE - 3), r.randint(6, 8)
            x, y = {N: (along, d), S: (along, TILE - 1 - d), W: (d, along), E: (TILE - 1 - d, along)}[side]
            cover[y, x] = max(cover[y, x], 2)
    rgba = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    ramp = np.array(palette["ramp"], dtype=np.uint8)
    body = cover == 1
    rgba[body, :3] = ramp[np.clip(tex[body], 1, 5)]
    rgba[cover == 2, :3] = ramp[1]
    rgba[cover > 0, 3] = 255
    return rgba


# --- Props --------------------------------------------------------------------

TORCH_FRAMES = 3


def torch_frame(frame: int) -> np.ndarray:
    """A wall torch in one cell: a wrapped stick, outlined, lit on its left,
    and a flame whose shape sways from frame to frame (drawn unlit in Unity:
    it is the light)."""
    wood, wood_lit, wood_dark, rim = hexes("6b4226", "8f5c34", "4a2c18", "24140a")
    wrap = hexes("9a9aa6")[0]
    outer, mid, core, tip = hexes("d8521a", "ff9a2e", "ffe07a", "fff6d0")
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        if 0 <= x < TILE and 0 <= y < TILE:
            a[y, x, :3] = colour
            a[y, x, 3] = 255

    for y in range(8, 15):
        put(6, y, rim)
        put(9, y, rim)
        put(7, y, wood_lit)
        put(8, y, wood if y < 13 else wood_dark)
    put(7, 15, rim)
    put(8, 15, rim)
    for x in (6, 7, 8, 9):
        put(x, 9, wrap if x in (7, 8) else rim)
    sway = [0, -1, 1][frame % 3]
    height = [6, 7, 5][frame % 3]
    for k in range(height):
        y = 7 - k
        half = 2 if k < 2 else (1 if k < height - 1 else 0)
        cx = 7 + (sway if k >= height // 2 else 0)
        for x in range(cx - half, cx + 2 + half):
            put(x, y, outer)
        if half >= 1:
            for x in range(cx - half + 1, cx + 1 + half):
                put(x, y, mid)
        if k < height - 2:
            put(cx, y, core)
            put(cx + 1, y, core if k > 0 else mid)
    if frame == 1:
        put(7 + sway, 8 - height, tip)
    return a


def torch_sheet() -> tuple[Image.Image, list]:
    sheet = np.zeros((TILE, TORCH_FRAMES * TILE, 4), dtype=np.uint8)
    sprites = []
    for f in range(TORCH_FRAMES):
        sheet[:, f * TILE : (f + 1) * TILE] = torch_frame(f)
        sprites.append((f"tv2_torch_{f}", f, 0))
    return Image.fromarray(sheet, "RGBA"), sprites


