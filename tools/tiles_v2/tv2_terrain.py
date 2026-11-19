#!/usr/bin/env python3
"""Draws the Tiles V2 terrain: each material's texture, its 47 autotile frames and
slopes, the blend overlays between materials and the wall torch. Not a script.
"""
from __future__ import annotations

import math
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
    # A stone or two set in it: lit on its top-left, a shade under its foot; no two tiles' in one place.
    for _ in range(1 + seed % 2):
        x, y, wide = r.randrange(TILE), r.randrange(TILE), r.choice([2, 3])
        for dy in range(2):
            for dx in range(wide):
                t[(y + dy) % TILE, (x + dx) % TILE] = 4 if dy == 0 and dx < wide - 1 else 3 if dy == 0 else 2
        for dx in range(wide):
            t[(y + 2) % TILE, (x + dx + 1) % TILE] = 1
    # A lighter seam of the earth, lying a little aslant: it runs on into the next tile.
    lift = r.randrange(TILE)
    for x in range(TILE):
        y = (lift + (x * (1 + seed % 2)) // 8) % TILE
        if t[y, x] == 3 and (x + seed) % 5:
            t[y, x] = 4
    for _ in range(3):
        x, y = r.randrange(TILE), r.randrange(TILE)
        if t[y, x] == 3:
            t[y, x] = 2
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


def texture_ashlar(seed) -> np.ndarray:
    """Dressed stone, as the castles of Paradise are drawn: two even courses a tile, each
    block a tile long and its own shade, the courses half a block apart, a block lit along
    its top, the joint under and after it dark but not black."""
    r = rng("ashlar", seed)
    t = np.zeros((TILE, TILE), dtype=np.int16)
    for course in (0, 1):
        base = r.choice([3, 4, 4, 4])
        for row in range(8):
            for x in range(TILE):
                xx = (x + (8 if course else 0)) % TILE
                t[course * 8 + row, x] = base - 2 if row == 7 or xx == TILE - 1 else min(base + 1, 5) if row == 0 else base
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


def texture_cloud(seed) -> np.ndarray:
    """A cloud's inside: bright, with a few soft rounder shadows and lit puffs."""
    r = rng("cloud", seed)
    t = np.full((TILE, TILE), 4, dtype=np.int16)
    blob(t, r, 3, 3, (2.0, 3.2))
    blob(t, r, 2, 1, (1.2, 1.8))
    for _ in range(3):
        x, y = r.randrange(TILE), r.randrange(TILE)
        t[y, x] = 4
        t[y, (x + 1) % TILE] = 4
    return t


def texture_ice(seed) -> np.ndarray:
    """Ice: a clear field with slanting glints and a few fine dark cracks."""
    r = rng("ice", seed)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    for _ in range(3):
        x, y, n = r.randrange(TILE), r.randrange(TILE), r.randint(3, 6)
        for k in range(n):
            t[(y + k) % TILE, (x - k) % TILE] = 4
    for _ in range(2):
        x, y, n = r.randrange(TILE), r.randrange(TILE), r.randint(3, 5)
        for k in range(n):
            t[(y + k) % TILE, (x + k // 2) % TILE] = 2
    blob(t, r, 2, 1, (1.2, 2.0))
    return t


def texture_leaves(seed) -> np.ndarray:
    """Leaves, or petals: rows of small rounds, every other row half a round along, each lit
    on its top, dark at its foot and between it and the next, some lighter and some darker."""
    r = rng("leaves", seed)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    for row in range(4):
        for col in range(4):
            tone = r.choice([0, 0, 0, 1, -1])
            for dy in range(4):
                for dx in range(4):
                    x, y = (col * 4 + (2 if row % 2 else 0) + dx) % TILE, row * 4 + dy
                    if dy == 0:
                        t[y, x] = 5 if dx in (1, 2) else 2
                    elif dy == 3:
                        t[y, x] = 2 if dx in (1, 2) else 3 + tone
                    else:
                        t[y, x] = (4 if dy == 1 else 3) + tone
    return t


def texture_foliage(seed) -> np.ndarray:
    """Leaves in shade, as a wall behind a place: soft leaf shapes lying over one another
    in two tones a step apart, a lit edge on one in three; nothing small enough to be a
    speck. What stands before it is what is looked at."""
    r = rng("foliage", seed)
    t = np.full((TILE, TILE), 2, dtype=np.int16)
    for n in range(6):
        cx, cy = r.uniform(0, TILE), r.uniform(0, TILE)
        turn = r.uniform(-0.9, 0.9)
        rx, ry = r.uniform(3.2, 4.4), r.uniform(1.7, 2.3)
        tone = r.choice([3, 3, 3, 4]) if n > 1 else 3
        for y in range(TILE):
            for x in range(TILE):
                dx = ((x + 0.5 - cx + TILE / 2) % TILE) - TILE / 2
                dy = ((y + 0.5 - cy + TILE / 2) % TILE) - TILE / 2
                along = dx * np.cos(turn) + dy * np.sin(turn)
                across = -dx * np.sin(turn) + dy * np.cos(turn)
                d = (along / rx) ** 2 + (across / ry) ** 2
                if d <= 1:
                    t[y, x] = tone + (1 if n % 3 == 0 and across < -ry * 0.55 else 0)
                elif d <= 1.5 and across > 0 and t[y, x] == 2:
                    t[y, x] = 1  # the dark it lies over
    return np.clip(t, 0, 5)


def texture_flutes(seed) -> np.ndarray:
    """The shaft of a column: round flutes two to a tile, lit on the left."""
    t = np.zeros((TILE, TILE), dtype=np.int16)
    t[:, :] = np.array([1, 3, 4, 5, 5, 4, 3, 2] * 2, dtype=np.int16)[None, :]
    return t


def texture_glass(seed) -> np.ndarray:
    """Stained glass: panes in a lattice of lead, each pane one of the palette's colours
    (the ramp of a glass is hues, not shades: 0 is the lead), a glint in some."""
    r = rng("glass", seed)
    t = np.zeros((TILE, TILE), dtype=np.int16)
    for row in range(2):
        for col in range(2):
            hue = r.choice([1, 1, 2, 2, 3, 4])
            x0, y0 = col * 8, row * 8
            for dy in range(8):
                for dx in range(8):
                    # A pane is a diamond in its 8 x 8; what is left between four of them is one more.
                    inside = abs(dx - 3.5) + abs(dy - 3.5) <= 3.6
                    edge = abs(abs(dx - 3.5) + abs(dy - 3.5) - 4.1) < 0.6
                    t[y0 + dy, x0 + dx] = 0 if edge else hue if inside else 5 if (row + col) % 2 else 2
            if r.random() < 0.5:
                t[y0 + 2, x0 + 3] = 5
    return t


def texture_plain(seed) -> np.ndarray:
    """A smooth face with a soft sheen across it: polished metal, deep shadow."""
    r = rng("plain", seed)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    for y in range(TILE):
        for x in range(TILE):
            if (x + y + r.randrange(2)) % 16 in (3, 4):
                t[y, x] = 4
            elif (x + y) % 16 in (11, 12):
                t[y, x] = 2
    return t


def texture_planks(seed) -> np.ndarray:
    """Boards laid across, four to a tile, each with its own shade, a lit top edge, a dark
    seam under it, a joint here and there and the odd nail: a mine's boarded wall."""
    r = rng("planks", seed)
    t = np.full((TILE, TILE), 3, dtype=np.int16)
    for board in range(4):
        y0 = board * 4
        tone = r.choice([2, 3, 3, 4])
        joint = r.randrange(TILE)
        for x in range(TILE):
            t[y0, x] = min(tone + 1, 4)
            t[y0 + 1, x] = tone
            t[y0 + 2, x] = tone if (x * 7 + board * 3 + seed) % 5 else tone - 1
            t[y0 + 3, x] = 1
        for y in range(y0, y0 + 3):
            t[y, joint] = 1
        if r.random() < 0.5:
            t[y0 + 1, (joint + 3) % TILE] = 4
    return t


TEXTURES = {
    "planks": texture_planks,
    "glass": texture_glass,
    "plain": texture_plain,
    "leaves": texture_leaves,
    "foliage": texture_foliage,
    "flutes": texture_flutes,
    "cloud": texture_cloud,
    "ice": texture_ice,
    "dirt": texture_dirt,
    "stone": texture_stone,
    "bricks": texture_bricks,
    "ashlar": texture_ashlar,
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
    depth = np.full(TILE, 7)
    for x in seam_safe(r, 6, 2, TILE - 3):
        depth[x] = r.choice([8, 9, 9, 11])
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


def patina_over(rgba: np.ndarray, solid: np.ndarray, variant: int) -> np.ndarray:
    """Copper's green: a few pixels of patina on the rock beside its nuggets, and the
    nuggets made bigger by a pixel here and there."""
    c = slice(TILE, 2 * TILE)
    mid = solid[c, c]
    edge = np.zeros((TILE, TILE), dtype=bool)
    for dx, dy in [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]:
        edge |= ~shift(solid, dx, dy, True)[c, c]
    ore = ore_mask(variant) & mid & ~edge
    green, green_dark = hexes("4aa888", "2e7a62")
    out = rgba.copy()
    r = rng("patina", variant)
    for y in range(1, TILE - 1):
        for x in range(1, TILE - 1):
            if ore[y, x] or not mid[y, x] or edge[y, x]:
                continue
            beside = ore[y, x - 1] or ore[y, x + 1] or ore[y - 1, x]
            if beside and r.random() < 0.35:
                out[y, x, :3] = green if r.random() < 0.6 else green_dark
    return out


def draw_frame(material: dict, mask: int, variant: int, flat: bool = False) -> np.ndarray:
    """A frame on the draft. With `flat` its inside is one tone (ramp 3) all through, so
    what is plain inside can be told from what is rim, turf or ore."""
    r = rng(material["key"], mask, variant)
    solid = neighbourhood(mask)
    wall = material["kind"] == "wall"
    solid = opened(solid) if not wall else solid
    if not wall and material.get("grass"):
        blade_tips(solid, mask, r)
    tex = np.full((TILE, TILE), 3, dtype=np.int16) if flat else TEXTURES[material["texture"]](variant)
    rgba = shade(solid, tex, PALETTES[material["palette"]], wall)
    if material.get("grass"):
        rgba = grass_over(rgba, solid, PALETTES[material["grass"]], r)
    if material.get("ore"):
        rgba = ore_over(rgba, solid, PALETTES[material["ore"]], variant)
    if material.get("ore") == "copper":
        rgba = patina_over(rgba, solid, variant)
    return rgba


def slope_shape(kind: str) -> tuple[np.ndarray, int]:
    """The middle tile of a slope (solid on and below the diagonal) and the mask
    of the neighbours a slope sits among: ground below, the high side solid."""
    ys, xs = np.mgrid[0:TILE, 0:TILE]
    back = TILE - 1 - xs  # a column counted from the right
    if kind == "up_right":
        center = xs + ys >= TILE - 1
        mask = E | SE | S | SW
    elif kind == "up_left":
        center = xs <= ys
        mask = W | SW | S | SE
    # Steep: two cells up for one across. The lower cell is cut from its low corner to the
    # middle of its top, the upper from the middle of its foot to its high corner.
    elif kind == "steep_right_low":
        center = 2 * xs + ys >= TILE - 1
        mask = N | NE | E | SE | S | SW
    elif kind == "steep_right_high":
        center = 2 * xs + ys >= 2 * TILE - 1
        mask = E | SE | S
    elif kind == "steep_left_low":
        center = 2 * back + ys >= TILE - 1
        mask = N | NW | W | SW | S | SE
    else:
        center = 2 * back + ys >= 2 * TILE - 1
        mask = W | SW | S
    return center, mask


def draw_slope(material: dict, kind: str, variant: int, flat: bool = False) -> np.ndarray:
    r = rng(material["key"], kind, variant)
    center, mask = slope_shape(kind)
    solid = opened(neighbourhood(mask, center))
    if material.get("grass"):
        blade_tips(solid, mask, r)
    tex = np.full((TILE, TILE), 3, dtype=np.int16) if flat else TEXTURES[material["texture"]](variant)
    rgba = shade(solid, tex, PALETTES[material["palette"]], False)
    if material.get("grass"):
        rgba = grass_over(rgba, solid, PALETTES[material["grass"]], r)
    return rgba


# --- The art that ships: twice as fine as the draft ---------------------------
# A frame keeps the shape the draft gives it (its outline, its rims, its turf, its ore,
# through Scale2x), and its plain inside is laid with the material's texture drawn at the
# finer size: the draft's own, smoothed, with what a pixel half the size can say put in.


def _wrapped(t: np.ndarray, dx: int, dy: int) -> np.ndarray:
    return np.roll(np.roll(t, dy, axis=0), dx, axis=1)


def _tile_noise(seed, size: int, cells: int) -> np.ndarray:
    """Smooth noise -1 .. 1 on a tile that repeats: a grid of `cells` values, wrapped."""
    r = np.random.default_rng(rng("tile noise", seed).getrandbits(32))
    grid = r.uniform(-1, 1, (cells, cells))
    ys, xs = np.mgrid[0:size, 0:size]
    fx, fy = xs * cells / size, ys * cells / size
    ix, iy = fx.astype(int), fy.astype(int)
    tx, ty = fx - ix, fy - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a, b = grid[iy % cells, ix % cells], grid[iy % cells, (ix + 1) % cells]
    c, d = grid[(iy + 1) % cells, ix % cells], grid[(iy + 1) % cells, (ix + 1) % cells]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def fine_texture(name: str, variant: int) -> np.ndarray:
    """A material's inside at the size that ships, as ramp indices on a tile that repeats:
    the draft's texture through Scale2x, then what the finer pixel can say. Earth gets
    pebbles, each lit on its top left with its shade under it, and a grain; rock, hairline
    cracks and a glint on a chunk here and there; dressed stone, a vein, a chip at a
    block's corner and a line of light under its joint; clay and boards, a grain along
    them; leaves, a rib; sand and ice, a sparkle. Cloud, glass and the plain are left
    as they are: they are smooth."""
    size = PIXELS
    t = scale2x_array(TEXTURES[name](variant).astype(np.int16), wrap=True).astype(np.int16)
    r = rng("fine", name, variant)
    kind = {"rubble": "stone", "bricks": "ashlar", "meadow": "dirt"}.get(name, name)
    coarse = _tile_noise(("fine coarse", name, variant), size, 4)
    grain = _tile_noise(("fine grain", name, variant), size, 16)

    def put(x, y, value):
        t[y % size, x % size] = value

    if kind == "dirt":
        t[(coarse > 0.45) & (t == 3)] = 4  # a paler patch of it, soft
        for _ in range(4 + variant % 2):  # pebbles
            x, y, wide = r.randrange(size), r.randrange(size), r.choice([2, 3, 3, 4])
            for dx in range(wide):
                put(x + dx, y, 5 if dx < wide - 1 else 4)
                put(x + dx, y + 1, 4 if dx == 0 else 2)
                put(x + dx + 1, y + 2, 1)
        for _ in range(2):  # a root hair, pale, wandering down
            x, y = r.randrange(size), r.randrange(size)
            for i in range(r.randint(4, 8)):
                x += r.choice([-1, 0, 0, 1])
                put(x, y + i, 4 if i % 3 else 5)
        t[(grain > 0.84) & (t == 3)] = 2  # a grain, sparse: earth is not sand
        t[(grain < -0.88) & (t == 3)] = 4
    elif kind == "stone":
        for _ in range(3):  # hairline cracks, aslant
            x, y, lean = r.randrange(size), r.randrange(size), r.choice([-1, 1])
            for i in range(r.randint(5, 10)):
                if t[(y + i) % size, (x + lean * (i // 2)) % size] >= 2:
                    put(x + lean * (i // 2), y + i, 1)
                    if t[(y + i) % size, (x + lean * (i // 2) + 1) % size] >= 2:
                        put(x + lean * (i // 2) + 1, y + i, min(t[(y + i) % size, (x + lean * (i // 2) + 1) % size] + 1, 4))
        t[(grain > 0.74) & (t >= 2) & (t <= 3)] += 1  # a glint on a chunk
        t[(grain < -0.8) & (t >= 3)] -= 1
    elif kind == "ashlar":
        for _ in range(2):  # a vein through the stone
            x, y = r.randrange(size), r.randrange(size)
            for i in range(r.randint(8, 14)):
                y += r.choice([0, 0, 1, -1])
                if t[y % size, (x + i) % size] >= 3:
                    put(x + i, y, t[y % size, (x + i) % size] - 1)
        dark = t <= 1  # the joints
        under = _wrapped(dark, 0, 1) & ~dark  # the line of light under a joint
        t[under & (t < 5) & (grain > -0.5)] += 1
        for _ in range(3):  # a chip out of a block
            x, y = r.randrange(size), r.randrange(size)
            if t[y, x] >= 3:
                put(x, y, 2)
                put(x + 1, y, 2)
                put(x, y + 1, 1)
    elif kind in ("clay", "planks"):
        t[(grain > 0.55) & (t == 3) & (np.arange(size)[:, None] % 2 == 0)] = 4  # a grain along it
        t[(grain < -0.6) & (t == 3) & (np.arange(size)[:, None] % 2 == 1)] = 2
    elif kind in ("foliage", "leaves"):
        lit = (t >= 3) & (_wrapped(t, 1, 1) < t)  # the edge of a leaf that the light is on
        t[lit & (grain > 0.1) & (t < 5)] += 1
        t[(grain < -0.75) & (t >= 3)] -= 1
    elif kind == "sand":
        t[(grain > 0.7) & (t >= 2) & (t < 5)] += 1
        t[(grain < -0.72) & (t >= 2)] -= 1
    elif kind == "ice":
        t[(grain > 0.78) & (t < 5)] += 1
    return np.clip(t, 0, 5)


def _fine(material: dict, drawn: np.ndarray, flat: np.ndarray, variant: int) -> np.ndarray:
    """A frame at the size that ships, from the frame on the draft and the same frame with
    a flat inside: where the flat one is plain, the texture drawn at the finer size."""
    palette = PALETTES[material["palette"]]
    out = scale2x_rgba(drawn)
    plain = scale2x_rgba(flat)
    inside = np.all(plain[..., :3] == np.array(palette["ramp"][3], dtype=np.uint8), axis=-1) & (plain[..., 3] == 255)
    tex = fine_texture(material["texture"], variant)
    if material["kind"] == "wall":
        tex = np.clip(tex, 0, 4)  # a wall keeps low: no light on it
    out[inside, :3] = np.array(palette["ramp"], dtype=np.uint8)[tex][inside]
    if material.get("grass") and material["kind"] != "wall":
        _fine_turf(out, PALETTES[material["grass"]], (material["key"], variant))
    return out


def _fine_turf(out: np.ndarray, grass: dict, seed):
    """The turf of a frame at the size that ships: the doubled draft's, with what a finer
    pixel can say. Along a top that is open to the air a blade stands a pixel over its
    neighbours here and one sits a pixel under there (never at a tile's seam, so the next
    tile's top lines up); and in the turf itself single blades are a tone lighter or
    darker than the grass they stand in."""
    size = out.shape[0]
    ramp = [tuple(c) for c in grass["ramp"]]
    edge = tuple(grass["outline"])
    deep = 16  # how far down a column is moved with its top: the turf's depth, and a little
    for x in range(size):
        column = out[:, x]
        solid = np.nonzero(column[:, 3])[0]
        if not len(solid):
            continue
        top = int(solid[0])
        chance = rng("fine turf", seed, x).random()
        if tuple(column[top, :3]) == edge and 2 <= x < size - 2 and top + 4 < size and tuple(column[top + 4, :3]) in ramp:
            reach = min(deep, size - top - 1)
            if chance < 0.22 and top >= 1:
                column[top - 1 : top - 1 + reach] = column[top : top + reach].copy()
            elif chance > 0.8:
                column[top + 1 : top + 1 + reach] = column[top : top + reach].copy()
                column[top] = 0
        # Blades in the turf: a column of it a tone up, or down.
        blade = rng("fine blade", seed, x).random()
        for y in range(size):
            if column[y, 3] == 0:
                continue
            colour = tuple(column[y, :3])
            if colour == ramp[3] and blade < 0.22:
                column[y, :3] = ramp[4]
            elif colour == ramp[3] and blade > 0.84:
                column[y, :3] = ramp[2]
            elif colour == ramp[4] and blade > 0.9:
                column[y, :3] = ramp[5]


def fine_frame(material: dict, mask: int, variant: int) -> np.ndarray:
    return _fine(material, draw_frame(material, mask, variant), draw_frame(material, mask, variant, flat=True), variant)


def fine_slope(material: dict, kind: str, variant: int) -> np.ndarray:
    return _fine(material, draw_slope(material, kind, variant), draw_slope(material, kind, variant, flat=True), variant)


def fine_blend(blend: dict, sides: int, variant: int) -> np.ndarray:
    """A blend overlay at the size that ships: the draft's, through Scale2x."""
    return scale2x_rgba(draw_blend(blend, sides, variant))


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


def brazier_frame(frame: int) -> np.ndarray:
    """A fire bowl on a stand, a bigger flame than a torch's that sways."""
    iron, iron_lit, rim = hexes("3a3a44", "5c5c68", "16161c")
    outer, mid, core = hexes("d8521a", "ff9a2e", "ffe07a")
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        if 0 <= x < TILE and 0 <= y < TILE:
            a[y, x, :3] = colour
            a[y, x, 3] = 255

    for y in range(11, 16):
        put(7, y, iron_lit)
        put(8, y, iron)
    for x in range(5, 11):
        put(x, 15, rim)
    for x in range(3, 13):
        put(x, 9, rim if x in (3, 12) else iron_lit)
        put(x, 10, rim if x in (3, 4, 11, 12) else iron)
    sway = [0, 1, -1][frame % 3]
    height = [7, 8, 6][frame % 3]
    for k in range(height):
        y = 8 - k
        half = 3 if k < 2 else 2 if k < height - 2 else 1 if k < height - 1 else 0
        cx = 7 + (sway if k >= height // 2 else 0)
        for x in range(cx - half, cx + 2 + half):
            put(x, y, outer)
        for x in range(cx - half + 1, cx + 1 + half):
            put(x, y, mid)
        if k < height - 3:
            put(cx, y, core)
            put(cx + 1, y, core)
    return a


def crystal_frame(frame: int) -> np.ndarray:
    """A cluster of shards growing from the floor, with a glint that moves over it."""
    dark, mid, lit, white, rim = hexes("2f6f9e", "58a8d8", "9adcf6", "f2fdff", "163a5a")
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    for cx, top, half in ((7, 3, 2), (4, 8, 1), (11, 6, 1)):
        for y in range(top, TILE):
            width = min(half, (y - top) // 2 + (0 if half > 1 else 0))
            for x in range(cx - width, cx + width + 1):
                colour = lit if x < cx else dark if x > cx else mid
                a[y, x, :3] = colour
                a[y, x, 3] = 255
    solid = a[..., 3] > 0
    edge = np.zeros_like(solid)
    for dx, dy in ((1, 0), (-1, 0), (0, -1)):
        edge |= shift(solid, dx, dy, False)
    edge &= ~solid
    a[edge, :3] = rim
    a[edge, 3] = 255
    for x, y in [((7, 5), (4, 10), (11, 8))[frame % 3]]:
        a[y, x, :3] = white
        a[y + 1, x, :3] = white
    return a


def lantern_frame(frame: int) -> np.ndarray:
    """A gilded lamp on a short post, its glass a little brighter every other frame."""
    gold, gold_lit, rim = hexes("b8860e", "f2c93c", "4a3206")
    glass = hexes("fff0b0", "fffbe6", "ffe890")[frame % 3]
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        a[y, x, :3] = colour
        a[y, x, 3] = 255

    for y in range(10, 16):
        put(7, y, gold_lit)
        put(8, y, gold)
    for x in range(5, 11):
        put(x, 15, rim)
        put(x, 9, gold if x > 7 else gold_lit)
        put(x, 3, gold if x > 7 else gold_lit)
    for y in range(4, 9):
        put(5, y, rim)
        put(10, y, rim)
        for x in range(6, 10):
            put(x, y, glass)
    put(7, 2, gold_lit)
    put(8, 2, gold)
    put(7, 1, rim)
    put(8, 1, rim)
    return a


LEDGE_COLOURS = {
    # top (lit), board, underside, bracket, rim
    "plank": hexes("c6915e", "93603d", "5e3a26", "452a1c", "2a1810"),
    "grate": hexes("8a8a98", "5c5c68", "3a3a44", "2a2a32", "15151b"),
    "gilt": hexes("fff3a8", "f2c93c", "b88314", "e8e6ec", "4a3206"),
    "rime": hexes("f0fcff", "b0dff2", "6fadd0", "4f8fb8", "1c4a6a"),
}


def ledge_piece(style: str, piece: str) -> np.ndarray:
    """One cell of a ledge: a board at the top of the cell, with a bracket under each end
    it has. A grate is bars with gaps; the others are whole boards."""
    top, board, under, bracket, rim = LEDGE_COLOURS[style]
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        a[y, x, :3] = colour
        a[y, x, 3] = 255

    ends_left, ends_right = piece in ("single", "left"), piece in ("single", "right")
    for x in range(TILE):
        if style == "grate" and x % 4 == 3 and not (ends_left and x < 2) and not (ends_right and x > 13):
            put(x, 0, rim)
            put(x, 3, rim)
            continue
        put(x, 0, top)
        put(x, 1, board if (x * 7 + 3) % 11 else top)
        put(x, 2, board)
        put(x, 3, under)
        put(x, 4, rim)
    if style == "plank":
        for x in (5, 11):
            put(x, 1, under)
            put(x, 2, under)
    for end, x0, step in ((ends_left, 0, 1), (ends_right, TILE - 1, -1)):
        if not end:
            continue
        for y in range(0, 5):
            put(x0, y, rim)
        for k in range(4):
            put(x0 + step * (1 + k // 2), 5 + k, bracket)
            put(x0 + step * (2 + k // 2), 5 + k, rim)
    return a


def ledge_sheet() -> tuple[Image.Image, list]:
    """Every ledge: a row for each, its four pieces side by side."""
    sheet = np.zeros((len(LEDGES) * TILE, len(LEDGE_PIECES) * TILE, 4), dtype=np.uint8)
    sprites = []
    for row, style in enumerate(LEDGES):
        for column, piece in enumerate(LEDGE_PIECES):
            sheet[row * TILE : (row + 1) * TILE, column * TILE : (column + 1) * TILE] = ledge_piece(style, piece)
            sprites.append((f"tv2_ledge_{style}_{piece}", column, row))
    return Image.fromarray(sheet, "RGBA"), sprites


# The things of the game a map marks (`@mark`), each a tile-sized sprite the world's
# objects are drawn with: a light barrier's veil, the seal a bolt breaks, the slab a
# slam breaks, a relic.
THINGS = [
    "barrier_0", "barrier_1", "seal_bolt", "seal_slam", "relic_0", "relic_1", "crack", "crumble", "blink", "wind",
    "post", "tablet", "shade", "soul", "beacon_0", "beacon_1", "beacon_2", "sanctum",
    "flag_a_0", "flag_a_1", "flag_a_2", "flag_a_3", "flag_b_0", "flag_b_1", "flag_b_2", "flag_b_3", "bird_0", "bird_1",
]  # fmt: skip


def thing(name: str) -> np.ndarray:
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    r = rng("thing", name)
    if name.startswith("barrier"):
        # A veil of light: bright threads that shift between the two frames; tiles up and down.
        phase = int(name[-1])
        for x in range(TILE):
            strength = (0.55, 0.9, 0.4, 1.0, 0.6, 0.85, 0.45, 0.95)[(x + 3 * phase) % 8]
            for y in range(TILE):
                wave = 0.75 + 0.25 * np.sin((y + phase * 4 + x * 3) * np.pi / 4)
                a[y, x] = (255, 244, 190, int(200 * strength * wave) if 2 <= x <= 13 else 0)
        a[:, 1] = (255, 255, 235, 235)
        a[:, 14] = (255, 255, 235, 235)
    elif name == "seal_bolt":
        # A slab of night with a pale rune on it: light is what it gives way to.
        dark, mid, rim, rune = hexes("14101f", "241c38", "0a0812", "b9a6ff")
        a[:, :, :3] = mid
        a[:, :, 3] = 255
        for _ in range(26):
            a[r.randrange(TILE), r.randrange(TILE), :3] = dark
        a[:, 0, :3] = rim
        a[:, 15, :3] = rim
        for x, y in ((7, 3), (8, 3), (6, 4), (9, 4), (6, 5), (9, 5), (7, 6), (8, 6), (7, 7), (8, 7), (7, 9), (8, 9), (6, 11), (7, 11), (8, 11), (9, 11)):
            a[y, x, :3] = rune
    elif name == "seal_slam":
        # A stone slab cracked through: a blow from above is what it gives way to.
        ramp = PALETTES["stone"]["ramp"]
        a[:, :, :3] = ramp[3]
        a[:, :, 3] = 255
        a[0, :, :3] = ramp[5]
        a[15, :, :3] = ramp[1]
        for _ in range(18):
            a[r.randrange(TILE), r.randrange(TILE), :3] = ramp[2]
        x = 7
        for y in range(TILE):
            a[y, x, :3] = PALETTES["stone"]["outline"]
            if y % 3 == 1:
                a[y, x + r.choice([-1, 1]), :3] = ramp[1]
            x = max(3, min(12, x + r.choice([-1, 0, 0, 1])))
    elif name == "crack":
        # What tells a wall that is not one: a few fine cracks, laid over the rock.
        x = 6
        for y in range(TILE):
            a[y, x] = (0, 0, 0, 120)
            if y % 4 == 2:
                a[y, x + r.choice([-1, 1])] = (0, 0, 0, 90)
            x = max(2, min(13, x + r.choice([-1, 0, 0, 1])))
        for k in range(5):
            a[9 + k // 2, 9 + k] = (0, 0, 0, 100)
    elif name in ("crumble", "blink"):
        # A platform that does not stay: a slab that gives way (cracked stone), or one
        # that comes and goes (dark, with a pale rune that says so).
        ramp = PALETTES["stone" if name == "crumble" else "slate"]["ramp"]
        rim = PALETTES["stone" if name == "crumble" else "slate"]["outline"]
        for y in range(1, 11):
            for x in range(TILE):
                a[y, x] = (*(ramp[5] if y == 1 else ramp[2] if y >= 9 else ramp[3 if (x * 5 + y * 3) % 7 else 4]), 255)
        a[0, :] = (*rim, 255)
        a[11, :] = (*rim, 255)
        if name == "crumble":
            for x, y in ((4, 2), (5, 3), (5, 4), (6, 5), (6, 6), (7, 7), (11, 3), (10, 4), (10, 5), (11, 6), (12, 8)):
                a[y, x] = (*rim, 255)
        else:
            for x, y in ((7, 3), (8, 3), (6, 4), (9, 4), (6, 5), (9, 5), (7, 6), (8, 6), (7, 7), (8, 7)):
                a[y, x] = (200, 190, 255, 255)
    elif name == "post":
        # A signpost: a board on a stake.
        wood, lit, rim, ink = hexes("93603d", "c6915e", "2a1810", "452a1c")
        for y in range(8, 16):
            a[y, 7] = (*lit, 255)
            a[y, 8] = (*wood, 255)
        for y in range(2, 8):
            for x in range(2, 14):
                a[y, x] = (*(rim if y in (2, 7) or x in (2, 13) else lit if y == 3 else wood), 255)
        for x in (4, 5, 6, 8, 9, 11):
            a[4, x] = (*ink, 255)
        for x in (4, 5, 7, 8, 9, 10):
            a[6, x] = (*ink, 255)
    elif name == "tablet":
        # A standing stone with lines cut in it.
        ramp, rim = PALETTES["stone"]["ramp"], PALETTES["stone"]["outline"]
        for y in range(2, 16):
            for x in range(4, 12):
                if y == 2 and x in (4, 11):
                    continue
                a[y, x] = (*(ramp[5] if x == 4 else ramp[2] if x == 11 else ramp[4] if y == 2 else ramp[3]), 255)
        for y in (5, 7, 9, 11):
            for x in range(6, 10 if y != 11 else 8):
                a[y, x] = (*rim, 255)
    elif name == "shade":
        # A shade: a pale figure with no feet, two dark eyes.
        pale, mid, eye = (214, 224, 240), (170, 184, 210), (40, 44, 70)
        for y in range(2, 16):
            half = 3 if y < 6 else 4 if y < 12 else 4 - (y - 11) // 2
            for x in range(8 - half, 8 + half):
                if y >= 13 and (x + y) % 3 == 0:
                    continue
                a[y, x] = (*(pale if x < 8 else mid), 235 if y < 12 else 235 - (y - 11) * 40)
        a[5, 6] = a[5, 9] = (*eye, 255)
        a[6, 6] = a[6, 9] = (*eye, 255)
    elif name.startswith("flag"):
        # A pennant two cells long, flying to the right of its rod: `a` the half at the rod, `b`
        # its tail; four frames of a wave that runs along it.
        deep, mid, lit, trim = hexes("2648a8", "3b6fdc", "86b0f8", "ffe27a")
        half, frame = name[5], int(name[-1])
        for x in range(TILE):
            along = x + (0 if half == "a" else TILE)
            lift = int(round(2.2 * np.sin((along / 5.0) - frame * np.pi / 2) * along / (2 * TILE)))
            thick = max(2, 9 - along * 7 // (2 * TILE))  # it narrows to its point
            for k in range(thick):
                y = 3 + lift + k
                if 0 <= y < TILE:
                    a[y, x] = (*(trim if k in (0, thick - 1) and along < 26 else lit if (along + frame * 2) % 8 < 3 else mid if k % 3 else deep), 255)
    elif name.startswith("bird"):
        # A bird far off: two strokes of wing, up and down.
        white, grey = (255, 255, 255), (214, 224, 240)
        up = name.endswith("0")
        for dx in range(1, 6):
            y = 8 - (dx * 3 // 5 if up else -dx // 3) + (0 if up else 1)
            a[y, 7 - dx] = a[y, 8 + dx] = (*white, 255)
            if dx < 5:
                a[y + 1, 7 - dx] = a[y + 1, 8 + dx] = (*grey, 200)
        a[8 + (0 if up else 1), 7] = a[8 + (0 if up else 1), 8] = (*white, 255)
    elif name == "soul":
        # One of the blessed: a figure of light, a ring over its head.
        white, gold, warm, eye = (255, 252, 236), (255, 220, 120), (255, 238, 180), (150, 110, 40)
        for y in range(4, 16):
            half = 2 if y < 7 else 4 if y < 13 else 4 - (y - 12) // 2
            for x in range(8 - half, 8 + half):
                if y >= 14 and (x + y) % 3 == 0:
                    continue
                a[y, x] = (*(white if x < 8 else warm), 240 if y < 13 else 240 - (y - 12) * 45)
        for x in range(5, 11):
            a[1, x] = (*gold, 255)
        a[2, 4] = a[2, 11] = (*gold, 255)
        a[5, 6] = a[5, 9] = (*eye, 255)
    elif name.startswith("beacon"):
        # A bowl of gold on a stem: empty, or with a flame of white light in it (two frames).
        gold, lit, dark = hexes("dca922", "fff3a8", "8a5e0c")
        for y in range(11, 16):
            a[y, 7] = (*gold, 255)
            a[y, 8] = (*dark, 255)
        for x in range(5, 11):
            a[15, x] = (*(gold if x < 9 else dark), 255)
        for x in range(3, 13):
            a[9, x] = (*lit, 255)
            a[10, x] = (*(gold if x < 9 else dark), 255)
        for x in range(4, 12):
            a[11, x] = (*(gold if x < 8 else dark), 255)
        frame = int(name[-1])
        if frame:
            flame = [(8, 1), (7, 2), (8, 2), (7, 3), (8, 3), (9, 3), (6, 4), (7, 4), (8, 4), (9, 4)] if frame == 1 else [(7, 1), (7, 2), (8, 2), (6, 3), (7, 3), (8, 3), (6, 4), (7, 4), (8, 4), (9, 4)]
            for y in range(5, 9):
                for x in range(5, 11):
                    if abs(x - 7.5) <= 3 - (8 - y) * 0.4:
                        a[y, x] = (255, 250, 214, 255) if abs(x - 7.5) < 1.6 else (255, 226, 130, 255)
            for x, y in flame:
                a[y, x] = (255, 255, 240, 255)
    elif name == "sanctum":
        # The door of the temple: bars of gold with light between them; tiles up and down.
        gold, lit, dark = hexes("dca922", "fff3a8", "8a5e0c")
        for y in range(TILE):
            for x in range(TILE):
                a[y, x] = (255, 244, 200, 70 + 30 * ((x + y) % 4 == 0))
        for x in (1, 2, 7, 8, 13, 14):
            a[:, x] = (*(lit if x in (1, 7, 13) else gold), 255)
        for y in (0, 8):
            a[y, :] = (*gold, 255)
            a[y + 1, :] = (*dark, 255)
    elif name == "wind":
        # A streak the wind carries: a short pale dash.
        for x in range(3, 13):
            a[8, x] = (235, 240, 255, 60 + 16 * min(x - 3, 12 - x))
    else:
        # A relic: a small gold star that turns its glint.
        gold, lit, white, rim = hexes("f2c93c", "fde06a", "ffffff", "8a5e0c")
        for x, y in ((7, 3), (8, 3), (7, 12), (8, 12), (3, 7), (3, 8), (12, 7), (12, 8)):
            a[y, x] = (*rim, 255)
        for y in range(4, 12):
            for x in range(4, 12):
                if abs(x - 7.5) + abs(y - 7.5) <= 4.5:
                    a[y, x] = (*(lit if x + y < 15 else gold), 255)
        glint = (6, 6) if name.endswith("0") else (9, 6)
        a[glint[1], glint[0]] = (*white, 255)
        a[glint[1] + 1, glint[0]] = (*white, 255)
    return a


def things_sheet() -> tuple[Image.Image, list]:
    sheet = np.zeros((TILE, len(THINGS) * TILE, 4), dtype=np.uint8)
    sprites = []
    for i, name in enumerate(THINGS):
        sheet[:, i * TILE : (i + 1) * TILE] = thing(name)
        sprites.append((f"tv2_thing_{name}", i, 0))
    return Image.fromarray(sheet, "RGBA"), sprites


def mine_lamp_frame(frame: int) -> np.ndarray:
    """A miner's lamp hung from a hook at the top of its cell: a hood of iron, a glass with
    the flame in it a little brighter every other frame, a ring under it."""
    iron, iron_lit, rim = hexes("3a3d46", "7a7f8a", "16161a")
    glass = hexes("ffd890", "fff0c0", "ffc870")[frame % 3]
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        a[y, x, :3] = colour
        a[y, x, 3] = 255

    for y in range(0, 3):
        put(8, y, iron)
    for x in range(5, 11):
        put(x, 3, iron_lit if x < 8 else iron)
        put(x, 10, iron)
    put(6, 2, iron_lit)
    put(9, 2, iron)
    for y in range(4, 10):
        put(5, y, rim)
        put(10, y, rim)
        for x in range(6, 10):
            put(x, y, glass)
    put(7, 11, rim)
    put(8, 11, rim)
    return a


from tv2_earth import SHROOM, TIMBER, campfire_frame, glowshroom_frame  # noqa: E402  (the Earth's props)
from tv2_eden import fine_glowfruit, glowfruit_frame  # noqa: E402  (the Paradise level's)

PROP_FRAMES = {"glowfruit": glowfruit_frame, "campfire": campfire_frame, "glowshroom": glowshroom_frame, "torch": torch_frame, "brazier": brazier_frame, "crystal": crystal_frame, "lantern": lantern_frame, "mine_lamp": mine_lamp_frame}


def prop_sheet(prop: dict) -> tuple[Image.Image, list]:
    """A light prop's sheet: its frames side by side."""
    key, count = prop["key"], prop["frames"]
    sheet = np.zeros((TILE, count * TILE, 4), dtype=np.uint8)
    sprites = []
    for f in range(count):
        sheet[:, f * TILE : (f + 1) * TILE] = PROP_FRAMES[key](f)
        sprites.append((f"tv2_{key}_{f}", f, 0))
    return Image.fromarray(sheet, "RGBA"), sprites


# --- The lamps at the size that ships ---------------------------------------------------
# A lamp is in every view and is what the eye goes to: each is drawn at 32 x 32 itself,
# in the place its draft has in its cell (a draft pixel is two of these).

FINE_TILE = PIXELS


def _fine_cell() -> tuple[np.ndarray, callable]:
    a = np.zeros((FINE_TILE, FINE_TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        x, y = int(x), int(y)
        if 0 <= x < FINE_TILE and 0 <= y < FINE_TILE:
            a[y, x, :3] = colour
            a[y, x, 3] = 255

    return a, put


def _fine_flame(put, cx: float, foot: int, tall: int, wide: float, sway: float, colours, tip=None):
    """A flame: a tear that swells from its foot, narrows to a point and leans as it
    rises, in three heats."""
    outer, mid, core = colours
    for k in range(tall):
        t = k / tall
        y = foot - k
        lean = sway * t * t
        swell = 0.62 + 0.38 * (t / 0.22) ** 0.7 if t < 0.22 else ((1 - t) / 0.78) ** 0.85
        half = wide * swell * (0.9 + 0.1 * math.sin(t * 9 + sway))
        for x in range(int(math.floor(cx + lean - half)), int(math.ceil(cx + lean + half)) + 1):
            away = abs(x + 0.5 - (cx + lean))
            if away > half + 0.5:
                continue
            colour = outer
            if away < half - 1.6 and t < 0.8:
                colour = mid
            if away < half - 3.2 and t < 0.5:
                colour = core
            if tip is not None and away < 1.2 and t < 0.22:
                colour = tip
            put(x, y, colour)


def fine_torch(frame: int) -> np.ndarray:
    """A wall torch: a wrapped stick with its grain, and a flame that sways (unlit: it is the light)."""
    wood, wood_lit, wood_dark, rim = hexes("6b4226", "8f5c34", "4a2c18", "24140a")
    wrap, wrap_lit = hexes("9a9aa6", "c8c8d2")
    a, put = _fine_cell()
    for y in range(16, 30):
        for x in (12, 13, 18, 19):
            put(x, y, rim)
        put(14, y, wood_lit)
        put(15, y, wood_lit if y % 5 else wood)
        put(16, y, wood if y < 26 else wood_dark)
        put(17, y, wood_dark if y % 4 == 1 or y >= 26 else wood)
    for x in range(14, 18):
        put(x, 30, rim)
        put(x, 31, rim)
    for y in (18, 19, 20):
        for x in range(12, 20):
            put(x, y, rim if x in (12, 19) else wrap_lit if y == 18 and x < 16 else wrap)
    _fine_flame(put, 16.0, 15, (14, 16, 12)[frame % 3], 6.0, (0.0, -3.0, 3.0)[frame % 3], hexes("d8521a", "ff9a2e", "ffe07a"), hexes("fff6d0")[0])
    return a


def fine_brazier(frame: int) -> np.ndarray:
    """A fire bowl on a stand: the bowl's curve and its lit lip, a bigger flame than a torch's."""
    iron, iron_lit, rim = hexes("3a3a44", "5c5c68", "16161c")
    a, put = _fine_cell()
    for y in range(22, 30):
        put(14, y, iron_lit)
        put(15, y, iron_lit if y % 3 else iron)
        put(16, y, iron)
        put(17, y, iron)
    for x in range(10, 22):
        put(x, 30, iron_lit if 11 <= x <= 20 else rim)
        put(x, 31, rim)
    for row, (x0, x1) in enumerate(((4, 27), (4, 27), (5, 26), (6, 25), (8, 23), (10, 21))):
        for x in range(x0, x1 + 1):
            edge = x - x0 < 2 or x1 - x < 2 or row == 5
            put(x, 16 + row, rim if edge else iron_lit if row == 0 or x < 12 else iron)
    for x in range(10, 24, 4):  # the bowl's rivets
        put(x, 19, iron_lit)
    _fine_flame(put, 16.0, 15, (14, 16, 12)[frame % 3], 9.0, (0.0, 3.0, -3.0)[frame % 3], hexes("d8521a", "ff9a2e", "ffe07a"))
    return a


def fine_crystal(frame: int) -> np.ndarray:
    """A cluster of shards growing from the floor: each a point, a lit face and a dark one
    with the ridge between them, and a glint that moves from shard to shard."""
    dark, mid, lit, white, rim = hexes("2f6f9e", "58a8d8", "9adcf6", "f2fdff", "163a5a")
    a, put = _fine_cell()
    shards = ((15, 5, 5), (8, 16, 3), (23, 12, 3), (19, 21, 2))
    for cx, top, half in shards:
        for y in range(top, FINE_TILE):
            width = min(half, (y - top) // 2)
            for x in range(cx - width, cx + width + 1):
                colour = mid if x == cx else lit if x < cx else dark
                if x == cx - max(width - 1, 1) and width > 1 and (y - top) % 6 < 4:
                    colour = white if (y - top) < 8 else lit  # the light along its edge
                put(x, y, colour)
    solid = a[..., 3] > 0
    for _ in range(2):
        edge = np.zeros_like(solid)
        for dx, dy in ((1, 0), (-1, 0), (0, -1)):
            edge |= shift(solid, dx, dy, False)
        edge &= ~solid
        a[edge, :3] = rim
        a[edge, 3] = 255
        solid = a[..., 3] > 0
    gx, gy = ((15, 10), (8, 20), (23, 16))[frame % 3]
    for dx, dy in ((0, 0), (0, 1), (0, 2), (-1, 1), (1, 1)):
        put(gx + dx, gy + dy, white)
    return a


def fine_lantern(frame: int) -> np.ndarray:
    """A gilded lamp on a short post: a collar on the post, a roof and a knob on the lamp,
    and glass that is brightest in its middle and a little brighter every other frame."""
    gold, gold_lit, gold_dark, rim = hexes("b8860e", "f2c93c", "8a6208", "4a3206")
    glow = hexes("fff0b0", "fffbe6", "ffe890")[frame % 3]
    core, edge = hexes("ffffff", "ffd96a")
    a, put = _fine_cell()
    for y in range(20, 30):
        put(14, y, gold_lit)
        put(15, y, gold_lit if y % 4 else gold)
        put(16, y, gold)
        put(17, y, gold_dark)
    for x in range(13, 19):  # the collar
        put(x, 24, gold_lit if x < 16 else gold)
        put(x, 25, gold_dark)
    for x in range(10, 22):
        put(x, 30, gold_lit if 11 <= x <= 15 else gold if x <= 20 else rim)
        put(x, 31, rim)
        put(x, 18, gold_lit if x < 16 else gold)
        put(x, 19, gold if x < 16 else gold_dark)
        put(x, 6, gold_lit if x < 16 else gold)
        put(x, 7, gold if x < 16 else gold_dark)
    for y in range(8, 18):
        for x in (10, 11, 20, 21):
            put(x, y, rim)
        for x in range(12, 20):
            away = math.hypot(x + 0.5 - 16, (y + 0.5 - 13) * 0.8)
            put(x, y, core if away < 2.0 + (frame % 3 == 1) else glow if away < 4.2 else edge)
    for x in range(12, 20):  # its roof, and the knob on it
        put(x, 4, gold_lit if x < 16 else gold)
        put(x, 5, gold if x < 16 else gold_dark)
    for x in range(14, 18):
        put(x, 2, gold_lit if x < 16 else gold)
        put(x, 3, gold if x < 16 else gold_dark)
    for x in (15, 16):
        put(x, 0, rim)
        put(x, 1, rim)
    return a


def fine_campfire(frame: int) -> np.ndarray:
    """A campfire at the size that ships: a ring of round stones, two logs crossed with their
    bark and cut ends, a flame in three heats that changes its shape each frame."""
    stones = hexes("3e3a40", "6a6470", "8e8894", "aea8b4")
    wood, wood_lit, wood_dark, cut = TIMBER[3], TIMBER[4], TIMBER[1], hexes("c8a878")[0]
    a, put = _fine_cell()
    for i in range(20):  # the logs, crossed
        for d in range(3):
            put(6 + i, 27 - i // 2.5 + d, wood_dark if d == 2 else wood_lit if d == 0 else wood)
            put(25 - i, 27 - i // 2.5 + d, wood_dark if d == 2 else wood if d == 0 else TIMBER[2])
    for x, y in ((6, 27), (25, 27)):
        put(x, y, cut)
        put(x, y + 1, cut)
    for cx in (3, 8, 14, 20, 26, 30):  # the ring of stones
        for dy in range(-2, 2):
            for dx in range(-2, 3):
                if dx * dx + (dy + 0.5) ** 2 <= 5:
                    put(cx + dx, 30 + dy, stones[3] if dx + dy < -1 else stones[2] if dx < 1 else stones[1])
    for x in range(1, 32):
        put(x, 31, stones[0])
    tall, wide, sway = ((16, 6.0, 0.0), (14, 6.5, -2.5), (17, 5.5, 2.5))[frame % 3]
    _fine_flame(put, 16.0, 25, tall, wide, sway, hexes("ff6a1e", "ffb43a", "fff0a0"), hexes("fffbe0")[0])
    _fine_flame(put, 11.0 + frame % 2, 25, tall // 2, 3.0, -1.5, hexes("ff6a1e", "ffb43a", "fff0a0"))
    _fine_flame(put, 21.0 - frame % 2, 25, tall // 2 + 1, 3.0, 1.5, hexes("ff6a1e", "ffb43a", "fff0a0"))
    return a


def fine_glowshroom(frame: int) -> np.ndarray:
    """Mushrooms that give a cold light in the damp, at the size that ships: three domed caps
    on pale stems, gills under them, spots on top, the glow swelling and fading."""
    lift = (0, 1, 2)[frame % 3]
    a, put = _fine_cell()
    for cx, tall, half in ((8, 10, 4.5), (18, 16, 6.5), (27, 8, 3.0)):
        for y in range(31 - tall, 32):  # the stem
            put(cx - 1, y, SHROOM[2])
            put(cx, y, SHROOM[1])
        cap = 31 - tall
        for dy in range(-int(half) - 1, 2):  # the cap, domed
            for dx in range(-int(half * 1.2) - 1, int(half * 1.2) + 2):
                u, v = (dx + 0.5) / (half * 1.25), (dy + 0.5) / half
                if v <= 0.35 and u * u + v * v <= 1:
                    k = 4 + lift if u + v < -0.6 else 3 + lift if u < 0.4 else 2 + lift
                    if v > 0.1:
                        k = 1 + lift  # its gills, underneath
                    put(cx + dx, cap + dy, SHROOM[min(k, len(SHROOM) - 1)])
        put(cx - 2, cap - int(half * 0.6), SHROOM[len(SHROOM) - 1])  # a light spot on it
        put(cx + 1, cap - int(half * 0.4), SHROOM[len(SHROOM) - 1])
    return a


def fine_mine_lamp(frame: int) -> np.ndarray:
    """A miner's lamp at the size that ships, hung from a hook at the top of its cell: a hood
    of iron with its rivets, a glass with the flame in it, a ring under it."""
    iron, iron_lit, rim = hexes("3a3d46", "7a7f8a", "16161a")
    glass = hexes("ffd890", "fff0c0", "ffc870")[frame % 3]
    a, put = _fine_cell()
    for y in range(0, 6):  # the hook
        put(16, y, iron)
        put(17, y, rim)
    for x in range(13, 20):
        put(x, 0, iron if 14 < x < 18 else rim)
    for x in range(10, 22):  # the hood and the base
        put(x, 6, iron_lit if x < 16 else iron)
        put(x, 7, iron)
        put(x, 20, iron_lit if x < 15 else iron)
        put(x, 21, rim)
    for x in (12, 16, 20):
        put(x, 6, (200, 204, 212))  # its rivets
    for y in range(8, 20):
        for x in (10, 11, 20, 21):
            put(x, y, rim if x in (10, 21) else iron)
        for x in range(12, 20):
            away = math.hypot(x + 0.5 - 16, (y + 0.5 - 14) * 0.7)
            put(x, y, (255, 255, 240) if away < 1.6 + (frame % 3 == 1) else glass if away < 3.6 else hexes("ffb850")[0])
    for x in range(14, 18):  # the ring under it
        put(x, 22, rim)
        put(x, 23, iron)
    return a


FINE_PROPS = {"campfire": fine_campfire, "glowshroom": fine_glowshroom, "mine_lamp": fine_mine_lamp, "glowfruit": fine_glowfruit, "torch": fine_torch, "brazier": fine_brazier, "crystal": fine_crystal, "lantern": fine_lantern}


def fine_prop_sheet(prop: dict) -> tuple[Image.Image, list]:
    """A light prop's sheet at the size that ships: its frames side by side, each drawn at
    that size where the prop has a painter for it (FINE_PROPS), else its draft through Scale2x."""
    key, count = prop["key"], prop["frames"]
    paint = FINE_PROPS.get(key)
    sheet = np.zeros((FINE_TILE, count * FINE_TILE, 4), dtype=np.uint8)
    sprites = []
    for f in range(count):
        sheet[:, f * FINE_TILE : (f + 1) * FINE_TILE] = paint(f) if paint else scale2x_rgba(PROP_FRAMES[key](f))
        sprites.append((f"tv2_{key}_{f}", f, 0))
    return Image.fromarray(sheet, "RGBA"), sprites


def torch_sheet() -> tuple[Image.Image, list]:
    return prop_sheet(next(p for p in PROPS if p["key"] == "torch"))
