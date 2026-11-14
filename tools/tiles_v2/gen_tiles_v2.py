#!/usr/bin/env python3
"""Draws the Tiles V2 art into Assets/Art/TilesV2: one sheet a material with its
full autotile frame set, the blend overlays between materials, the parallax
backdrop, and tiles_v2.json, the manifest the Unity builder reads to make the
Rule Tiles. The standard this code encodes is written down in
docs/art/tiles-v2.md; change both together.

    python3 -I tools/tiles_v2/gen_tiles_v2.py                    # Python 3 with Pillow and numpy
    py -I tools/tiles_v2/gen_tiles_v2.py --preview scratch/v2.png  # also lay out the sample map

The standard, in short:
  * a tile is 16 x 16 px, imported at 16 PPU: one tile is one Unity unit and one
    collision cell; nothing is scaled, rotated or drawn at another density;
  * every terrain has the same 47 frames (the 8-neighbour "blob" set: edges,
    outer corners, inner corners) in 3 variants, plus two slopes; walls have
    the 47 frames too;
  * each material has one ramp of six colours plus an outline colour, and
    draws with nothing else;
  * light comes from the top left: an exposed top edge gets a highlight, a
    left edge a lighter rim, bottom and right edges a shade;
  * every edge open to air gets a 1 px outline in the material's outline
    colour; edges between joined materials get none (a blend overlay hides
    the seam).

Everything is drawn from fixed seeds, so a run reproduces the same pixels. A
new file under Assets/ gets its .meta (unity_meta); an existing .meta is never
touched.
"""
from __future__ import annotations

import argparse
import json
import os
import random
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "unity"))
import unity_meta  # noqa: E402

OUT = os.path.join(unity_meta.ASSETS, "Art", "TilesV2")
SAMPLE_MAP = os.path.join(OUT, "sample_map.txt")

TILE = 16
PPU = 16
VARIANTS = 3
COLUMNS = 8

# Neighbour bits, Unity's way up (y up). Image rows run down, so N is row - 1.
N, NE, E, SE, S, SW, W, NW = 1, 2, 4, 8, 16, 32, 64, 128
BITS = [(N, 0, -1), (NE, 1, -1), (E, 1, 0), (SE, 1, 1), (S, 0, 1), (SW, -1, 1), (W, -1, 0), (NW, -1, -1)]
# Each corner counts only when both edges beside it are solid.
CORNERS = {NE: (N, E), SE: (S, E), SW: (S, W), NW: (N, W)}


def canonical(mask: int) -> int:
    """The frame a neighbour mask shows: a corner bit is kept only when both of
    its edges are set, since otherwise the corner cannot be seen."""
    out = mask & (N | E | S | W)
    for corner, (a, b) in CORNERS.items():
        if mask & corner and mask & a and mask & b:
            out |= corner
    return out


FRAMES = sorted({canonical(m) for m in range(256)})
assert len(FRAMES) == 47


# --- Palettes -----------------------------------------------------------------
# A ramp is six colours, darkest to brightest; `outline` is the edge to air.


def hexes(*values):
    return [tuple(int(v[i : i + 2], 16) for i in (0, 2, 4)) for v in values]


PALETTES = {
    "dirt": {
        "outline": hexes("2a1810")[0],
        "ramp": hexes("452a1c", "5e3a26", "7a4c31", "93603d", "ad774c", "c6915e"),
    },
    "grass": {
        "outline": hexes("173418")[0],
        "ramp": hexes("1f4a22", "2e6a2c", "3f8a35", "56a83f", "79c24f", "a6dc6a"),
    },
    "stone": {
        "outline": hexes("1c1d26")[0],
        "ramp": hexes("33353f", "464955", "5b5e6b", "717482", "8a8d9a", "a8abb6"),
    },
    "dirt_wall": {
        "outline": hexes("1e120c")[0],
        "ramp": hexes("2c1b12", "362218", "40291d", "4a3022", "553828", "60402e"),
    },
    "stone_wall": {
        "outline": hexes("16171d")[0],
        "ramp": hexes("23242c", "2b2d36", "343641", "3d3f4b", "474a56", "525562"),
    },
    "sand": {
        "outline": hexes("4a3a1c")[0],
        "ramp": hexes("8c7440", "a88c4e", "c2a660", "d6bc74", "e6cf8a", "f3e2a8"),
    },
    "clay": {
        "outline": hexes("3a1a14")[0],
        "ramp": hexes("6a3024", "84402c", "9c5236", "b4643f", "c97a4e", "dc9466"),
    },
    "copper": {
        "outline": hexes("3a1e10")[0],
        "ramp": hexes("6e3a1c", "9a5428", "c06e34", "de8c44", "f0ac62", "ffd08a"),
    },
}

# Materials: the sheet, how its inside is drawn, and what it joins (no edge
# between joined tiles). `group` is the Rule Tile's join group in Unity.
MATERIALS = [
    {"key": "dirt", "kind": "terrain", "texture": "dirt", "palette": "dirt", "group": "earth", "slopes": True},
    {"key": "grass", "kind": "terrain", "texture": "dirt", "palette": "dirt", "grass": "grass", "group": "earth", "slopes": True},
    {"key": "stone", "kind": "terrain", "texture": "stone", "palette": "stone", "group": "earth", "slopes": True},
    {"key": "dirt_wall", "kind": "wall", "texture": "dirt", "palette": "dirt_wall", "group": "walls", "slopes": False},
    {"key": "stone_wall", "kind": "wall", "texture": "rubble", "palette": "stone_wall", "group": "walls", "slopes": False},
    {"key": "sand", "kind": "terrain", "texture": "sand", "palette": "sand", "group": "earth", "slopes": True},
    {"key": "clay", "kind": "terrain", "texture": "clay", "palette": "clay", "group": "earth", "slopes": False},
    {"key": "copper_ore", "kind": "terrain", "texture": "stone", "palette": "stone", "ore": "copper", "group": "earth", "slopes": False},
]

# Blends: `from` laid over the edge of an `onto` tile that touches it.
BLENDS = [
    {"key": "dirt_on_stone", "onto": "stone", "from": ["dirt", "grass"], "texture": "dirt", "palette": "dirt"},
    {"key": "dirt_on_sand", "onto": "sand", "from": ["dirt", "grass"], "texture": "dirt", "palette": "dirt"},
    {"key": "dirt_on_clay", "onto": "clay", "from": ["dirt", "grass"], "texture": "dirt", "palette": "dirt"},
    {"key": "sand_on_stone", "onto": "stone", "from": ["sand"], "texture": "sand", "palette": "sand"},
]

SLOPES = ["up_right", "up_left"]  # rising to the right / to the left; solid below the diagonal


# --- Helpers ------------------------------------------------------------------


def rng(*parts) -> random.Random:
    return random.Random("|".join(str(p) for p in parts))


def seam_safe(r: random.Random, count: int, lo: int = 3, hi: int = TILE - 4) -> list[int]:
    """Positions along a tile edge, kept off the two pixels at each end so an
    edge runs on into the next tile at its base line."""
    return [r.randint(lo, hi) for _ in range(count)]


def shift(a: np.ndarray, dx: int, dy: int, fill) -> np.ndarray:
    """a moved by (dx, dy) in image space, with `fill` coming in."""
    out = np.full_like(a, fill)
    h, w = a.shape
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    out[yd, xd] = a[ys, xs]
    return out


DISC = [(dx, dy) for dx in range(-3, 4) for dy in range(-3, 4) if dx * dx + dy * dy <= 10]


def opened(solid: np.ndarray) -> np.ndarray:
    """Morphological opening by a small disc: rounds every outer corner by a
    pixel or two and leaves straight edges where they are."""
    eroded = solid.copy()
    for dx, dy in DISC:
        eroded &= shift(solid, dx, dy, True)
    grown = np.zeros_like(solid)
    for dx, dy in DISC:
        grown |= shift(eroded, dx, dy, False)
    return grown & solid


def reach(solid: np.ndarray, dx: int, dy: int, cap: int = 8) -> np.ndarray:
    """For each solid pixel, how many steps toward (dx, dy) reach air (1: the
    next pixel is air); `cap` + 1 where none does within the cap."""
    out = np.full(solid.shape, cap + 1, dtype=np.int16)
    probe = solid.copy()
    for step in range(1, cap + 1):
        ahead = shift(solid, -dx * step, -dy * step, True)
        hit = probe & ~ahead
        out[hit] = step
        probe &= ahead
    out[~solid] = 0
    return out


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


# --- Backdrop -----------------------------------------------------------------

BACKDROP_W, BACKDROP_H = 320, 180


def periodic(x, waves, seed):
    """A sum of sines that repeats every BACKDROP_W pixels."""
    r = rng("ridge", seed)
    total = 0.0
    for k, amp in waves:
        phase = r.uniform(0, 6.283)
        total += amp * np.sin(2 * np.pi * k * x / BACKDROP_W + phase)
    return total


def dither_mix(a, b, t, x, y):
    """a or b at pixel (x, y) by an ordered 4 x 4 dither of the share t of b."""
    bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
    return b if t * 16 > bayer[y % 4][x % 4] + 0.5 else a


def backdrop_sky() -> Image.Image:
    top, low = hexes("3b6fc0")[0], hexes("a9d6ef")[0]
    bands = 7
    im = Image.new("RGBA", (BACKDROP_W, BACKDROP_H))
    px = im.load()
    for y in range(BACKDROP_H):
        t = y / (BACKDROP_H - 1) * (bands - 1)
        i = int(t)
        f = t - i
        for x in range(BACKDROP_W):
            k = i + (1 if dither_mix(0, 1, f, x, y) else 0)
            k = min(k, bands - 1)
            u = k / (bands - 1)
            px[x, y] = tuple(int(top[j] + (low[j] - top[j]) * u) for j in range(3)) + (255,)
    # Clouds: flat puffs, lit on top, shade on the underside.
    r = rng("clouds")
    white, under = hexes("f2f7fb")[0], hexes("c9dcea")[0]
    for _ in range(6):
        cx, cy, w = r.randrange(BACKDROP_W), r.randint(18, 70), r.randint(26, 54)
        puffs = [(cx + r.randint(-w // 2, w // 2), cy + r.randint(-3, 3), r.randint(5, 10)) for _ in range(5)]
        for y in range(cy - 14, cy + 10):
            for x in range(cx - w, cx + w):
                inside = any((x - px0) ** 2 + ((y - py0) * 1.6) ** 2 <= rr * rr for px0, py0, rr in puffs)
                if inside and 0 <= y < BACKDROP_H:
                    flat = y > cy + 2
                    px[x % BACKDROP_W, y] = (under if flat else white) + (255,)
    return im


def backdrop_ridge(name: str, colour, light, base: int, waves, seed, trees=None) -> Image.Image:
    """A silhouette band that repeats sideways: its left-facing slopes lit (the
    light comes from the top left), optional round-crowned trees on top."""
    im = Image.new("RGBA", (BACKDROP_W, BACKDROP_H), (0, 0, 0, 0))
    px = im.load()
    xs = np.arange(BACKDROP_W)
    height = base + periodic(xs, waves, seed)
    if trees:
        r = rng(name, "trees")
        bumps = np.zeros(BACKDROP_W)
        x = 0
        while x < BACKDROP_W:
            rad = r.randint(*trees)
            for dx in range(-rad, rad + 1):
                xx = (x + dx) % BACKDROP_W
                bumps[xx] = max(bumps[xx], (rad * rad - dx * dx) ** 0.5 + rad * 0.6)
            x += r.randint(rad, rad * 2)
        height = height + bumps
    top = (BACKDROP_H - np.round(height)).astype(int)
    for x in range(BACKDROP_W):
        slope = top[x] - top[(x - 1) % BACKDROP_W]
        for y in range(max(top[x], 0), BACKDROP_H):
            lit = slope < 0 and y - top[x] < 3 or (y == top[x])
            px[x, y] = (light if lit else colour) + (255,)
    return im


def backdrops() -> dict[str, Image.Image]:
    return {
        "backdrop_sky": backdrop_sky(),
        "backdrop_far": backdrop_ridge(
            "far", hexes("7fa3c4")[0], hexes("a3c2dc")[0], 70, [(1, 16), (2, 9), (5, 4), (11, 2)], 1
        ),
        "backdrop_mid": backdrop_ridge(
            "mid", hexes("4f8a7c")[0], hexes("69a48f")[0], 46, [(1, 8), (3, 6), (7, 2)], 2, trees=(4, 8)
        ),
        "backdrop_near": backdrop_ridge(
            "near", hexes("2f5e4c")[0], hexes("427a60")[0], 26, [(2, 6), (5, 3)], 3, trees=(6, 12)
        ),
    }


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


# --- Sheets and manifest ------------------------------------------------------


def frame_cell(i: int) -> tuple[int, int]:
    return i % COLUMNS, i // COLUMNS


def sheet_for(material: dict) -> tuple[Image.Image, list]:
    """A material's sheet: the 47 frames in variant blocks of 6 rows, then a row
    of slopes (each slope's variants side by side)."""
    rows_per_variant = (len(FRAMES) + COLUMNS - 1) // COLUMNS
    slope_rows = 1 if material["slopes"] else 0
    rows = rows_per_variant * VARIANTS + slope_rows
    sheet = np.zeros((rows * TILE, COLUMNS * TILE, 4), dtype=np.uint8)
    sprites = []
    for v in range(VARIANTS):
        for i, mask in enumerate(FRAMES):
            cx, cy = frame_cell(i)
            cy += v * rows_per_variant
            sheet[cy * TILE : (cy + 1) * TILE, cx * TILE : (cx + 1) * TILE] = draw_frame(material, mask, v)
            sprites.append((f"tv2_{material['key']}_f{mask}_v{v}", cx, cy))
    if material["slopes"]:
        cy = rows_per_variant * VARIANTS
        for s, kind in enumerate(SLOPES):
            for v in range(VARIANTS):
                cx = s * VARIANTS + v
                sheet[cy * TILE : (cy + 1) * TILE, cx * TILE : (cx + 1) * TILE] = draw_slope(material, kind, v)
                sprites.append((f"tv2_{material['key']}_{kind}_v{v}", cx, cy))
    return Image.fromarray(sheet, "RGBA"), sprites


def blend_sheet(blend: dict) -> tuple[Image.Image, list]:
    rows_per_variant = (len(BLEND_MASKS) + COLUMNS - 1) // COLUMNS
    sheet = np.zeros((rows_per_variant * VARIANTS * TILE, COLUMNS * TILE, 4), dtype=np.uint8)
    sprites = []
    for v in range(VARIANTS):
        for i, sides in enumerate(BLEND_MASKS):
            cx, cy = frame_cell(i)
            cy += v * rows_per_variant
            sheet[cy * TILE : (cy + 1) * TILE, cx * TILE : (cx + 1) * TILE] = draw_blend(blend, sides, v)
            sprites.append((f"tv2_{blend['key']}_s{sides}_v{v}", cx, cy))
    return Image.fromarray(sheet, "RGBA"), sprites


def sheet_meta(path: str, image: Image.Image, sprites: list) -> str:
    h = image.size[1]
    entries = [(name, cx * TILE, h - (cy + 1) * TILE, TILE, TILE, (0.5, 0.5)) for name, cx, cy in sprites]
    return unity_meta.texture_meta(unity_meta.guid_for(unity_meta.rel(path)), ppu=PPU, point_filter=True, sprites=entries)


def backdrop_meta(path: str) -> str:
    # Full Rect, so the sprite can be drawn tiled sideways.
    text = unity_meta.texture_meta(unity_meta.guid_for(unity_meta.rel(path)), ppu=PPU, point_filter=True, pivot=(0.5, 0.0))
    return text.replace("spriteMeshType: 1", "spriteMeshType: 0").replace("wrapU: 1", "wrapU: 0")


def manifest() -> dict:
    return {
        "tile": TILE,
        "ppu": PPU,
        "variants": VARIANTS,
        "bits": {"N": N, "NE": NE, "E": E, "SE": SE, "S": S, "SW": SW, "W": W, "NW": NW},
        "frames": FRAMES,
        "slopes": SLOPES,
        "materials": [
            {
                "key": m["key"],
                "kind": m["kind"],
                "group": m["group"],
                "sheet": m["key"] + ".png",
                "slopes": m["slopes"],
            }
            for m in MATERIALS
        ],
        "blends": [
            {"key": b["key"], "onto": b["onto"], "from": b["from"], "sheet": "blend_" + b["key"] + ".png", "masks": BLEND_MASKS}
            for b in BLENDS
        ],
        "props": [{"key": "torch", "sheet": "torch.png", "frames": TORCH_FRAMES}],
        "backdrops": [
            {"key": "backdrop_sky", "scroll": 0.0, "order": -1000},
            {"key": "backdrop_far", "scroll": 0.1, "order": -990},
            {"key": "backdrop_mid", "scroll": 0.25, "order": -980},
            {"key": "backdrop_near", "scroll": 0.45, "order": -970},
        ],
    }


def write(path: str, image: Image.Image, meta: str | None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    if unity_meta.inside_assets(path):
        unity_meta.ensure_meta(os.path.dirname(path))  # the folders
        if meta is not None:
            unity_meta.write_if_missing(path + ".meta", meta)
        else:
            unity_meta.ensure_meta(path)


def generate(out: str) -> dict[str, Image.Image]:
    images = {}
    for material in MATERIALS:
        image, sprites = sheet_for(material)
        path = os.path.join(out, material["key"] + ".png")
        write(path, image, sheet_meta(path, image, sprites))
        images[material["key"]] = image
    for blend in BLENDS:
        image, sprites = blend_sheet(blend)
        path = os.path.join(out, "blend_" + blend["key"] + ".png")
        write(path, image, sheet_meta(path, image, sprites))
        images["blend_" + blend["key"]] = image
    image, sprites = torch_sheet()
    path = os.path.join(out, "torch.png")
    write(path, image, sheet_meta(path, image, sprites))
    images["torch"] = image
    for name, image in backdrops().items():
        path = os.path.join(out, name + ".png")
        write(path, image, backdrop_meta(path))
        images[name] = image
    path = os.path.join(out, "tiles_v2.json")
    with open(path, "w", newline="\n") as handle:
        json.dump(manifest(), handle, indent=2)
        handle.write("\n")
    if unity_meta.inside_assets(path):
        unity_meta.ensure_meta(path)
    return images


# --- Preview: the sample map laid out the way the Rule Tiles will pick ---------


def read_map(path: str) -> tuple[list[str], list[str]]:
    """The sample map: a terrain grid, a blank line, a wall grid (same size)."""
    with open(path) as handle:
        text = handle.read().replace("\r", "")
    blocks = [b for b in text.split("\n\n") if b.strip()]
    grids = [[line for line in b.split("\n") if line and not line.startswith("#")] for b in blocks]
    return grids[0], grids[1]


TERRAIN_CHARS = {
    "G": "grass",
    "D": "dirt",
    "S": "stone",
    "A": "sand",
    "C": "clay",
    "O": "copper_ore",
    "/": "grass:up_right",
    "\\": "grass:up_left",
    "(": "sand:up_right",
    ")": "sand:up_left",
}
TORCH_CHAR = "T"  # a torch on the wall at this cell; air for the terrain
WALL_CHARS = {"d": "dirt_wall", "s": "stone_wall"}


# The lighting standard (docs/art/tiles-v2.md), shared with TilesV2Sample.cs and
# TileLight.cs: light per tile, from the open sky and the torches, spread tile by tile.
AMBIENT = 0.04  # the light everywhere, deep underground included
AIR_KEEPS = 0.9  # the share of light a tile of air (or air before a wall) passes on
SOLID_KEEPS = 0.7  # the share a solid tile passes on: light dies a few tiles into rock
LIGHT_PASSES = 3  # sweeps of the map: enough for light to turn a corner twice
TORCH_COLOUR = (1.0, 0.72, 0.42)
TORCH_INTENSITY = 1.6  # the light a torch gives its tile
TORCH_GLOW = 0.35  # the small flickering point light on top of it
TORCH_GLOW_RADIUS = 3.0  # tiles


def tile_light(solid: np.ndarray, wall: np.ndarray, torches) -> np.ndarray:
    """The light per tile, rows top first, as TileLight.cs works it out: daylight
    down each column to its first solid tile or wall, each torch's light on its
    tile, then the brightest of each tile's own light and its neighbours' light
    times the share it keeps."""
    h, w = solid.shape
    light = np.zeros((h, w, 3), dtype=np.float32)
    for x in range(w):
        for y in range(h):
            if solid[y, x] or wall[y, x]:
                break
            light[y, x] = 1.0
    for tx, ty in torches:
        light[ty, tx] = np.maximum(light[ty, tx], np.array(TORCH_COLOUR) * TORCH_INTENSITY)
    keep = np.where(solid, SOLID_KEEPS, AIR_KEEPS).astype(np.float32)[..., None]
    for _ in range(LIGHT_PASSES):
        for x in range(1, w):
            light[:, x] = np.maximum(light[:, x], light[:, x - 1] * keep[:, x])
        for x in range(w - 2, -1, -1):
            light[:, x] = np.maximum(light[:, x], light[:, x + 1] * keep[:, x])
        for y in range(1, h):
            light[y] = np.maximum(light[y], light[y - 1] * keep[y])
        for y in range(h - 2, -1, -1):
            light[y] = np.maximum(light[y], light[y + 1] * keep[y])
    return light


def light_preview(canvas: Image.Image, terrain: list[str], walls: list[str], torches) -> Image.Image:
    """The Unity lighting for the preview: the tile light, filtered smooth between
    tile centres, on a faint ambient."""
    h, w = len(terrain), canvas.width // TILE

    def grid(rows, chars):
        return np.array([[x < len(r) and r[x] in chars for x in range(w)] for r in rows[:h]], dtype=bool)

    light = tile_light(grid(terrain, TERRAIN_CHARS), grid(walls, WALL_CHARS), torches)
    smooth = np.stack(
        [np.asarray(Image.fromarray(light[..., c], "F").resize(canvas.size, Image.BILINEAR)) for c in range(3)], -1
    )
    smooth += AMBIENT
    rgb = np.asarray(canvas.convert("RGB"), dtype=np.float32) * np.clip(smooth, 0, 1.6)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def preview(images: dict[str, Image.Image], path: str, lit: bool = True):
    terrain, walls = read_map(SAMPLE_MAP)
    h, w = len(terrain), max(len(row) for row in terrain)
    group = {m["key"]: m["group"] for m in MATERIALS}

    def at(grid, chars, x, y):
        if 0 <= y < len(grid) and 0 <= x < len(grid[y]):
            return chars.get(grid[y][x])
        return None

    def joins(a, b):
        return a is not None and b is not None and group[a.split(":")[0]] == group[b.split(":")[0]]

    rows_per_variant = (len(FRAMES) + COLUMNS - 1) // COLUMNS
    canvas = Image.new("RGBA", (w * TILE, h * TILE))
    sky = images["backdrop_sky"].resize((w * TILE, h * TILE), Image.NEAREST)
    canvas.alpha_composite(sky)
    for key in ("backdrop_far", "backdrop_mid", "backdrop_near"):
        layer = images[key]
        for x0 in range(0, w * TILE, BACKDROP_W):
            canvas.alpha_composite(layer, (x0, 13 * TILE - BACKDROP_H))

    def place(grid, chars, kind_filter):
        for y in range(h):
            for x in range(w):
                name = at(grid, chars, x, y)
                if name is None:
                    continue
                key, _, slope = name.partition(":")
                v = (x * 7 + y * 13) % VARIANTS
                sheet = images[key]
                if slope:
                    cx, cy = SLOPES.index(slope) * VARIANTS + v, rows_per_variant * VARIANTS
                else:
                    mask = 0
                    for bit, dx, dy in BITS:
                        if joins(name, at(grid, chars, x + dx, y + dy)):
                            mask |= bit
                    cx, cy = frame_cell(FRAMES.index(canonical(mask)))
                    cy += v * rows_per_variant
                tile = sheet.crop((cx * TILE, cy * TILE, (cx + 1) * TILE, (cy + 1) * TILE))
                canvas.alpha_composite(tile, (x * TILE, y * TILE))

    place(walls, WALL_CHARS, "wall")
    place(terrain, TERRAIN_CHARS, "terrain")
    for blend in BLENDS:
        blend_img = images["blend_" + blend["key"]]
        brows = (len(BLEND_MASKS) + COLUMNS - 1) // COLUMNS
        for y in range(h):
            for x in range(w):
                if at(terrain, TERRAIN_CHARS, x, y) != blend["onto"]:
                    continue
                sides = 0
                for bit, side in BLEND_SIDES:
                    _, dx, dy = next(b for b in BITS if b[0] == side)
                    other = at(terrain, TERRAIN_CHARS, x + dx, y + dy)
                    if other is not None and other.split(":")[0] in blend["from"]:
                        sides |= bit
                if sides:
                    v = (x * 7 + y * 13) % VARIANTS
                    cx, cy = frame_cell(BLEND_MASKS.index(sides))
                    cy += v * brows
                    tile = blend_img.crop((cx * TILE, cy * TILE, (cx + 1) * TILE, (cy + 1) * TILE))
                    canvas.alpha_composite(tile, (x * TILE, y * TILE))
    torches = [(x, y) for y in range(h) for x in range(len(terrain[y])) if terrain[y][x] == TORCH_CHAR]
    if lit:
        canvas = light_preview(canvas, terrain, walls, torches)
    for x, y in torches:
        canvas.alpha_composite(images["torch"].crop((0, 0, TILE, TILE)), (x * TILE, y * TILE))
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    canvas.resize((canvas.width * 3, canvas.height * 3), Image.NEAREST).save(path)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=OUT, help="where to write (default: Assets/Art/TilesV2)")
    parser.add_argument("--preview", help="also write the sample map laid out, at 3x, to this PNG")
    args = parser.parse_args(argv)
    images = generate(args.out)
    if args.preview:
        preview(images, args.preview)
    print(f"Tiles V2: {len(MATERIALS)} materials x {len(FRAMES)} frames x {VARIANTS} variants -> {args.out}")


if __name__ == "__main__":
    main()
