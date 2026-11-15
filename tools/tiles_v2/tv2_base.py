#!/usr/bin/env python3
"""The Tiles V2 standard's shared vocabulary: the tile size, the neighbour bits and
the 47 autotile frames, the palettes, the materials and blends, and the small
drawing helpers every Tiles V2 module uses. docs/art/tiles-v2.md is the standard
this encodes; change both together. Not a script: gen_tiles_v2.py is the entry.
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
# The camera's view in art pixels: 40 x 22.5 tiles, twice as far back as the game's
# own 320 x 180, so more of the world shows. The sky and the backdrops are drawn
# for it and the Unity cameras take it as their reference resolution.
VIEW = (640, 360)
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


def frame_cell(i: int) -> tuple[int, int]:
    """Where the i-th frame of a sheet sits: (column, row) in tiles."""
    return i % COLUMNS, i // COLUMNS


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
        # Hue-shifted: the shadows lean blue, the lit turf yellow (the Earth's own look).
        "outline": hexes("15301a")[0],
        "ramp": hexes("1b4426", "28652e", "3c8634", "58a53c", "82c04a", "b4da64"),
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
        # Muted, earth and not fire: the old orange read as lava in the dark of the mine.
        "outline": hexes("2e1712")[0],
        "ramp": hexes("4e2c22", "62382a", "764634", "8a5640", "9e684e", "b07c60"),
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
    # An ore gives a little light of its own (`emits`, in the tile light's units).
    {"key": "copper_ore", "kind": "terrain", "texture": "stone", "palette": "stone", "ore": "copper", "group": "earth", "slopes": False, "emits": (0.30, 0.17, 0.07)},
]

# Blends: `from` laid over the edge of an `onto` tile that touches it.
BLENDS = [
    {"key": "dirt_on_stone", "onto": "stone", "from": ["dirt", "grass"], "texture": "dirt", "palette": "dirt"},
    {"key": "dirt_on_sand", "onto": "sand", "from": ["dirt", "grass"], "texture": "dirt", "palette": "dirt"},
    {"key": "dirt_on_clay", "onto": "clay", "from": ["dirt", "grass"], "texture": "dirt", "palette": "dirt"},
    {"key": "sand_on_stone", "onto": "stone", "from": ["sand"], "texture": "sand", "palette": "sand"},
]

SLOPES = ["up_right", "up_left"]  # rising to the right / to the left; solid below the diagonal

# --- The world's biomes: Paradise, and the nine circles under the Earth ------------
# Each adds its ground, its wall and what it is dressed with to the lists above.
# (key, outline, ramp of six) for the palettes; the materials follow.
_BIOME_PALETTES = [
    ("cloud", "8fa3c8", "b9c8e6 cfdaf0 e0e8f6 eef3fb f8fbff ffffff"),
    ("marble", "5b5a66", "8c8a96 a6a4ae bfbdc6 d5d3da e8e6ec f7f6fa"),
    ("marble_wall", "5b5a66", "9a98a6 a8a6b2 b6b4be c4c2ca d0ced6 dcdae2"),
    ("ashstone", "22212b", "3d3b4a 514f60 676577 7e7c8e 9896a6 b5b3c0"),
    ("ash_wall", "17161d", "25242e 2d2c38 363542 3f3e4c 494857 545362"),
    ("rose_rock", "2b1226", "4a1f43 662b5c 833a74 a04c8c bd66a6 d98ac2"),
    ("rose_wall", "1c0c19", "2c1328 371934 431f40 4f264c 5c2d59 693566"),
    ("mud", "1c1a0c", "33301a 454222 595530 6e6a3e 86824f a19c66"),
    ("mud_wall", "121108", "1f1d10 272515 302d1a 393620 433f26 4d492d"),
    ("darkstone", "1a130c", "30241a 433225 574231 6c543f 836950 9c8164"),
    ("dark_wall", "100b07", "1c150f 241b14 2d2219 36291f 403125 4a392c"),
    ("gold", "4a3206", "8a5e0c b88314 dca922 f2c93c fde06a fff3a8"),
    ("swamp_rock", "0f1c1c", "1d3333 284545 355959 446e6c 578682 6fa19a"),
    ("swamp_wall", "091212", "101f1f 152828 1b3131 213b3a 284544 30504e"),
    ("dis_brick", "220d0b", "3f1a16 58251e 733228 8f4233 ab5642 c66f55"),
    ("brick_wall", "150807", "25100d 301511 3c1a15 48201a 55271f 622e25"),
    ("bloodstone", "1f0707", "3a0f10 521618 6c1f21 88292b a53739 c14b4c"),
    ("blood_wall", "140404", "220809 2d0b0d 380f11 441316 50171a 5d1c1f"),
    ("ember_sand", "3a1608", "7a3a16 9a4e1e b86428 d27e36 e69a4c f4b96a"),
    ("slate", "12161c", "242c38 323c4a 424e5e 546274 6a788c 8492a6"),
    ("slate_wall", "0b0e12", "161b23 1c222c 232a36 2a3240 323b4a 3a4455"),
    ("ice", "1c4a6a", "4f8fb8 6fadd0 8fc8e4 b0dff2 d0f0fb f0fcff"),
    ("frost_rock", "0e1a2c", "1c2e48 273d5c 344e72 44628a 5a7aa4 7696be"),
    ("ice_wall", "0c2334", "163a54 1c4762 235470 2a617e 326f8d 3a7d9c"),
    # What stands on the wall behind under the open sky: a tree's trunk and crown.
    ("wood_wall", "2a1810", "5a3a24 6b462c 7c5335 8e6240 a07450 b48860"),
    ("leaf_wall", "173418", "356e2e 428438 529a44 66b054 80c66a 9ad880"),
    # Paradise's own: the pale trunks of its trees, their blossom and their gold leaf (walls
    # are multiplied back by the wall tint, so these are drawn bright), and the inside of a cloud.
    ("birch_wall", "8a8a9a", "d6d6de dedee6 e8e8ee f0f0f4 f8f8fb ffffff"),
    ("blossom_wall", "a84a78", "d86a9a e482ae ee9cc0 f6b4d0 fccce0 ffe6f0"),
    ("gold_leaf_wall", "9a6a10", "c88e1c dca62c ecbc40 f8d05c ffe284 fff3b4"),
    ("pillar_wall", "8c8a9a", "b8b6c4 cccad6 dcdae4 eae8f0 f6f5fa ffffff"),
    # Stained glass: the first colour is the lead, the rest the panes' hues (cool, and warm for a rose).
    ("glass_wall", "1c1a3a", "24224a 2f6fd8 49b4f0 8a5ce0 ffd45a fff6d0"),
    ("rose_glass_wall", "3a1228", "4a1830 e0407a ff7aa8 f0a030 ffe070 fff6d0"),
    # Gold worked thin, for what is hung and set on a building; and marble in deep shadow.
    ("gilt_wall", "8a5e0c", "b88314 d09a1c e6b430 f6cc4c ffe27a fff3b0"),
    ("shadow_wall", "3a3a4c", "55546a 5f5e74 69687e 737288 7d7c92 87869c"),
    ("mist_wall", "7f93b8", "a9b9d6 b5c3de c1cde6 cdd7ec d9e1f2 e5ebf7"),
    # The Earth's: a meadow's turf, warmer and more yellow than the grass; the mine's planks.
    ("meadow_grass", "1c3214", "2a4a1c 3e6a26 5a8a2e 7ea83a a8c44a d2dc6a"),
    ("timber_wall", "120c08", "1e150e 251a12 2c1f16 33251a 3a2a1e 423024"),
    # A path: earth packed by feet, lighter and warmer than the dirt.
    ("path_dirt", "2e2018", "5a4030 6e4e38 84603f 9a7048 ae8254 c2966a"),
]
for _key, _outline, _ramp in _BIOME_PALETTES:
    PALETTES[_key] = {"outline": hexes(_outline)[0], "ramp": hexes(*_ramp.split())}


def _ground(key, texture, slopes, group="earth", **more):
    return dict({"key": key, "kind": "terrain", "texture": texture, "palette": key, "group": group, "slopes": slopes}, **more)


def _wall(key, texture):
    return {"key": key, "kind": "wall", "texture": texture, "palette": key, "group": "walls", "slopes": False}


MATERIALS += [
    # A cloud is bright all through: it gives a soft light of its own, so its underside is not a shadow.
    _ground("cloud", "cloud", True, group="sky", emits=(0.5, 0.55, 0.65)),
    _ground("marble", "bricks", True),
    _wall("marble_wall", "bricks"),
    _ground("ashstone", "stone", True),
    _wall("ash_wall", "rubble"),
    _ground("rose_rock", "stone", True),
    _wall("rose_wall", "rubble"),
    _ground("mud", "clay", True),
    _wall("mud_wall", "dirt"),
    _ground("darkstone", "stone", True),
    {"key": "gold_ore", "kind": "terrain", "texture": "stone", "palette": "darkstone", "ore": "gold", "group": "earth", "slopes": False, "emits": (0.36, 0.28, 0.08)},
    _wall("dark_wall", "rubble"),
    _ground("swamp_rock", "stone", True),
    _wall("swamp_wall", "dirt"),
    _ground("dis_brick", "bricks", False),
    _wall("brick_wall", "bricks"),
    _ground("bloodstone", "stone", True),
    _ground("ember_sand", "sand", True),
    _wall("blood_wall", "rubble"),
    _ground("slate", "rubble", True),
    _wall("slate_wall", "bricks"),
    _ground("ice", "ice", True, emits=(0.10, 0.16, 0.22)),
    _ground("frost_rock", "stone", True),
    _wall("ice_wall", "rubble"),
    # These stand under the open sky and do not shut it out (`open`): the cells behind
    # which they are drawn are as if they had no wall, for the light and for what grows,
    # and they are drawn in front of the other walls, not tinted back as those are.
    dict(_wall("wood_wall", "clay"), open=True),
    dict(_wall("leaf_wall", "leaves"), open=True),
    dict(_wall("birch_wall", "clay"), open=True),
    dict(_wall("blossom_wall", "leaves"), open=True),
    dict(_wall("gold_leaf_wall", "leaves"), open=True),
    # A column standing in the open, of bright marble: it shuts out no sky either.
    dict(_wall("pillar_wall", "flutes"), open=True),
    # Glass lets the light through, as a window does; gilt work hangs in front of the wall.
    dict(_wall("glass_wall", "glass"), open=True),
    dict(_wall("rose_glass_wall", "glass"), open=True),
    dict(_wall("gilt_wall", "plain"), open=True),
    # A recess, a niche, the inside of a dome: marble the sun does not reach.
    _wall("shadow_wall", "bricks"),
    # The inside of a cloud: a wall like any other, it shuts out the sky.
    _wall("mist_wall", "cloud"),
    # Gold in blocks, for what Paradise builds its roofs of: it gives a warm light.
    dict(_ground("gold_brick", "bricks", True, emits=(0.42, 0.33, 0.10)), palette="gold"),
    # The Earth's: a meadow is dirt under a warmer turf, where the wild flowers grow
    # (tv2_decor.py); the old mine's walls are boarded with planks in places.
    {"key": "meadow", "kind": "terrain", "texture": "dirt", "palette": "dirt", "grass": "meadow_grass", "group": "earth", "slopes": True},
    {"key": "path", "kind": "terrain", "texture": "dirt", "palette": "path_dirt", "group": "earth", "slopes": False},
    {"key": "column_stone", "kind": "terrain", "texture": "flutes", "palette": "marble", "group": "column", "slopes": False},
    _wall("timber_wall", "planks"),
]


def _blend(upper, lower):
    material = next(m for m in MATERIALS if m["key"] == upper)
    return {"key": f"{upper}_on_{lower}", "onto": lower, "from": [upper], "texture": material["texture"], "palette": material["palette"]}


# Where one biome's ground lies on the next one's, and a biome's two grounds on each other.
BLENDS += [
    _blend("dirt", "ashstone"),
    _blend("stone", "ashstone"),
    _blend("ashstone", "rose_rock"),
    _blend("rose_rock", "mud"),
    _blend("mud", "darkstone"),
    _blend("darkstone", "swamp_rock"),
    _blend("swamp_rock", "dis_brick"),
    _blend("dis_brick", "bloodstone"),
    _blend("ember_sand", "bloodstone"),
    _blend("bloodstone", "slate"),
    _blend("slate", "frost_rock"),
    _blend("ice", "frost_rock"),
    _blend("dirt", "marble"),
]

# The world's regions, top to bottom: what each is called, the faint light that hangs
# in its air (the tile light's units; none where the sky or torches do the work), the
# colours of the rock behind its caves (none for open sky) and what drifts in its air:
# (how many a second, colour, alpha, speed across from/to, speed up from/to, seconds
# a mote lives, whether it takes the tile light).
REGIONS = [
    {"key": "paradise", "name": "Paradise", "subtitle": "Celestial Gardens", "ambient": (0.0, 0.0, 0.0), "cave": None,
     "air": (5, "fff2c0", 0.8, (-0.4, 0.4), (-0.7, -0.2), 9.0, False)},
    {"key": "earth", "name": "Ruined Earth", "subtitle": "The Hub", "ambient": (0.0, 0.0, 0.0),
     "cave": "0d0a09 15110e 1d1814 262019 302820", "air": (4, "f4f0c8", 0.7, (-0.5, 0.3), (-0.15, 0.25), 9.0, True)},
    {"key": "limbo", "name": "1. Limbo", "subtitle": "The Forgotten", "ambient": (0.20, 0.21, 0.28),
     "cave": "0c0c12 14141d 1b1b27 232331 2c2c3c", "air": (7, "c8c8de", 0.5, (-0.3, 0.3), (-0.1, 0.1), 10.0, True)},
    {"key": "lust", "name": "2. Lust", "subtitle": "The Desire", "ambient": (0.26, 0.13, 0.24),
     "cave": "120812 1c0d1c 251226 2f1830 3a1f3c", "air": (26, "f0a0d8", 0.6, (5.0, 9.0), (-0.6, 0.6), 4.0, False)},
    {"key": "gluttony", "name": "3. Gluttony", "subtitle": "The Devourers", "ambient": (0.17, 0.19, 0.10),
     "cave": "0b0b06 12120a 19190e 212113 2a2a19", "air": (40, "9fb0c0", 0.5, (-1.0, -0.6), (-13.0, -10.0), 2.4, False)},
    {"key": "greed", "name": "4. Greed", "subtitle": "The Hoarders", "ambient": (0.24, 0.19, 0.09),
     "cave": "0c0906 140f0a 1c150e 251c13 2e2418", "air": (6, "ffd860", 0.9, (-0.2, 0.2), (-0.3, 0.1), 7.0, False)},
    {"key": "wrath", "name": "5. Wrath", "subtitle": "The Raging", "ambient": (0.10, 0.19, 0.18),
     "cave": "060c0c 0a1414 0e1b1b 132323 182c2c", "air": (7, "a0c8b8", 0.4, (-0.4, 0.4), (0.0, 0.2), 9.0, True)},
    {"key": "heresy", "name": "6. Heresy", "subtitle": "The Burned", "ambient": (0.27, 0.12, 0.07),
     "cave": "100605 1a0a08 230e0b 2d130f 381813", "air": (16, "ff9a3a", 0.9, (-0.5, 0.5), (0.8, 2.2), 4.0, False)},
    {"key": "violence", "name": "7. Violence", "subtitle": "The Wounded", "ambient": (0.25, 0.08, 0.08),
     "cave": "0e0304 170506 200709 2a0a0c 340d10", "air": (14, "b08080", 0.6, (-0.8, 0.2), (-1.4, -0.6), 7.0, True)},
    {"key": "fraud", "name": "8. Fraud", "subtitle": "The Deceivers", "ambient": (0.14, 0.17, 0.22),
     "cave": "06080b 0b0e13 10141b 161b24 1c222d", "air": (5, "8090a8", 0.5, (-0.2, 0.2), (-0.4, -0.1), 9.0, True)},
    {"key": "treachery", "name": "9. Treachery", "subtitle": "The Frozen", "ambient": (0.16, 0.24, 0.32),
     "cave": "050c14 0a1420 0f1c2c 152538 1b2e45", "air": (30, "e8f6ff", 0.9, (-1.2, 0.4), (-2.2, -1.0), 8.0, False)},
]


# What stands far off behind each region's caves, between the rock and the walls: the
# kind of shape (tv2_backdrop.py draws each) for every region that has rock behind it.
FAR = {
    "earth": "mine",
    "limbo": "columns",
    "lust": "spires",
    "gluttony": "teeth",
    "greed": "heaps",
    "wrath": "trees",
    "heresy": "towers",
    "violence": "trees",
    "fraud": "arches",
    "treachery": "shards",
}
FAR_SCROLL = 0.78  # the share of the camera's movement the far shapes scroll by: nearer than the rock


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


# --- The map: its legend and how it is read -------------------------------------
# A map is text: `@key value` lines (properties), then the terrain grid, a blank
# line and the wall grid (same size), row 0 on top; `#` starts a comment. The
# first grid holds what stands in a cell (ground, a liquid, a torch, the hero's
# start), the second the wall behind it.

LEGEND = [
    # character, layer, key, slope
    ("G", "terrain", "grass", ""),
    ("D", "terrain", "dirt", ""),
    ("S", "terrain", "stone", ""),
    ("A", "terrain", "sand", ""),
    ("C", "terrain", "clay", ""),
    ("O", "terrain", "copper_ore", ""),
    ("/", "terrain", "grass", "up_right"),
    ("\\", "terrain", "grass", "up_left"),
    ("(", "terrain", "sand", "up_right"),
    (")", "terrain", "sand", "up_left"),
    ("{", "terrain", "dirt", "up_right"),
    ("}", "terrain", "dirt", "up_left"),
    ("[", "terrain", "stone", "up_right"),
    ("]", "terrain", "stone", "up_left"),
    ("W", "liquid", "water", ""),
    ("L", "liquid", "lava", ""),
    ("T", "prop", "torch", ""),
    ("P", "mark", "spawn", ""),
    ("d", "wall", "dirt_wall", ""),
    ("s", "wall", "stone_wall", ""),
    # The biomes of the world.
    ("Q", "terrain", "cloud", ""),
    ("1", "terrain", "cloud", "up_right"),
    ("2", "terrain", "cloud", "up_left"),
    ("M", "terrain", "marble", ""),
    ("H", "terrain", "ashstone", ""),
    ("3", "terrain", "ashstone", "up_right"),
    ("4", "terrain", "ashstone", "up_left"),
    ("R", "terrain", "rose_rock", ""),
    ("5", "terrain", "rose_rock", "up_right"),
    ("6", "terrain", "rose_rock", "up_left"),
    ("U", "terrain", "mud", ""),
    ("7", "terrain", "mud", "up_right"),
    ("8", "terrain", "mud", "up_left"),
    ("N", "terrain", "darkstone", ""),
    ("9", "terrain", "darkstone", "up_right"),
    ("0", "terrain", "darkstone", "up_left"),
    ("$", "terrain", "gold_ore", ""),
    ("Y", "terrain", "swamp_rock", ""),
    ("<", "terrain", "swamp_rock", "up_right"),
    (">", "terrain", "swamp_rock", "up_left"),
    ("B", "terrain", "dis_brick", ""),
    ("V", "terrain", "bloodstone", ""),
    (":", "terrain", "bloodstone", "up_right"),
    (";", "terrain", "bloodstone", "up_left"),
    ("E", "terrain", "ember_sand", ""),
    ("F", "terrain", "slate", ""),
    ("+", "terrain", "slate", "up_right"),
    ("-", "terrain", "slate", "up_left"),
    ("I", "terrain", "ice", ""),
    ("Z", "terrain", "frost_rock", ""),
    ("=", "terrain", "frost_rock", "up_right"),
    ("_", "terrain", "frost_rock", "up_left"),
    ("~", "liquid", "styx", ""),
    ("!", "liquid", "blood", ""),
    ("%", "liquid", "tar", ""),
    ("&", "liquid", "slime", ""),
    ("*", "prop", "brazier", ""),
    ("^", "prop", "crystal", ""),
    ("J", "prop", "lantern", ""),
    # Ledges: thin one-way platforms, jumped up through and dropped down through.
    ('"', "ledge", "plank", ""),
    ("'", "ledge", "grate", ""),
    (",", "ledge", "gilt", ""),
    ("?", "ledge", "rime", ""),
    ("m", "wall", "marble_wall", ""),
    ("h", "wall", "ash_wall", ""),
    ("r", "wall", "rose_wall", ""),
    ("u", "wall", "mud_wall", ""),
    ("n", "wall", "dark_wall", ""),
    ("y", "wall", "swamp_wall", ""),
    ("b", "wall", "brick_wall", ""),
    ("v", "wall", "blood_wall", ""),
    ("f", "wall", "slate_wall", ""),
    ("i", "wall", "ice_wall", ""),
    ("w", "wall", "wood_wall", ""),
    ("l", "wall", "leaf_wall", ""),
    ("t", "wall", "birch_wall", ""),
    ("p", "wall", "blossom_wall", ""),
    ("g", "wall", "gold_leaf_wall", ""),
    ("c", "wall", "mist_wall", ""),
    ("x", "wall", "pillar_wall", ""),
    ("o", "wall", "glass_wall", ""),
    ("q", "wall", "rose_glass_wall", ""),
    ("X", "wall", "gilt_wall", ""),
    ("z", "wall", "shadow_wall", ""),
    # What Paradise builds with is cut on the slant too: a roof is a line, not a stair.
    ("a", "terrain", "marble", "up_right"),
    ("e", "terrain", "marble", "up_left"),
    ("j", "terrain", "gold_brick", "up_right"),
    ("k", "terrain", "gold_brick", "up_left"),
    ("K", "terrain", "gold_brick", ""),
    # Falling water: seen and walked through, neither swum in nor stood on.
    ("|", "liquid", "falls", ""),
    # The Earth's: a meadow in flower (grass the wild flowers grow thick on) and its slopes,
    # and the boarded wall of the old mine.
    ("Ñ", "terrain", "meadow", ""),
    ("ñ", "terrain", "meadow", "up_right"),
    ("Ö", "terrain", "meadow", "up_left"),
    ("Ø", "wall", "timber_wall", ""),
    ("ö", "prop", "mine_lamp", ""),
    ("Å", "prop", "campfire", ""),
    ("å", "prop", "glowshroom", ""),
    # A trodden path (packed earth, no turf), and the drums of the old columns by the start.
    ("ü", "terrain", "path", ""),
    ("Ü", "terrain", "column_stone", ""),
]
TERRAIN_CHARS = {c: key + (":" + slope if slope else "") for c, layer, key, slope in LEGEND if layer == "terrain"}
WALL_CHARS = {c: key for c, layer, key, _ in LEGEND if layer == "wall"}
OPEN_WALLS = {m["key"] for m in MATERIALS if m.get("open")}  # walls that do not shut out the sky
SHUT_WALL_CHARS = {c: key for c, key in WALL_CHARS.items() if key not in OPEN_WALLS}
LIQUID_CHARS = {c: key for c, layer, key, _ in LEGEND if layer == "liquid"}
PROP_CHARS = {c: key for c, layer, key, _ in LEGEND if layer == "prop"}
LEDGE_CHARS = {c: key for c, layer, key, _ in LEGEND if layer == "ledge"}
# A ledge's sheet has four pieces: by whether the same ledge goes on to its left and right.
LEDGES = ["plank", "grate", "gilt", "rime"]
LEDGE_PIECES = ["single", "left", "middle", "right"]
LEDGE_THICK = 5  # px of a ledge that carry: the strip at the top of its cell
TORCH_CHAR = "T"  # a torch on the wall at this cell; air for the terrain
SPAWN_CHAR = "P"  # where the hero starts in the playground; air


def read_map(path: str):
    """The map at `path`: (terrain rows, wall rows, properties). A property line
    `@view cave 31 20` is properties["view"]["cave"] = [31, 20]; `@region key row`
    and `@mark kind column row ...` add (key, row) and (kind, column, row, the
    rest) to properties["region"] and properties["mark"]; any other `@key value`
    is properties[key] = value."""
    with open(path, encoding="utf-8") as handle:
        lines = handle.read().replace("\r", "").split("\n")
    properties: dict = {"view": {}, "region": [], "mark": []}
    grids: list[list[str]] = [[]]
    for line in lines:
        if line.startswith("#"):
            continue
        if line.startswith("@"):
            parts = line[1:].split()
            if parts and parts[0] == "view" and len(parts) == 4:
                properties["view"][parts[1]] = [float(parts[2]), float(parts[3])]
            elif parts and parts[0] == "region" and len(parts) == 3:
                properties["region"].append((parts[1], int(parts[2])))
            elif parts and parts[0] == "mark" and len(parts) >= 4:
                properties["mark"].append((parts[1], int(parts[2]), int(parts[3]), parts[4:]))
            elif len(parts) >= 2:
                properties[parts[0]] = " ".join(parts[1:])
            continue
        if not line:
            if grids[-1]:
                grids.append([])
            continue
        grids[-1].append(line)
    grids = [g for g in grids if g]
    return grids[0], grids[1], properties


def cell_hash(x: int, y: int, salt: int = 0) -> int:
    """A hash of a cell (x, y up, Unity's cell) and a salt, 0 .. 2^31 - 1, the same
    sum TilesV2Rules.Hash does in 32-bit integers: what scatters the decor."""
    h = ((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) & 0xFFFFFFFF
    h ^= h >> 13
    h = (h * 0x5BD1E995) & 0xFFFFFFFF
    h ^= h >> 15
    return h & 0x7FFFFFFF


def variant_of(x: int, y: int, count: int) -> int:
    """The variant a terrain cell (x, y up) shows, as TerrainTile.Variant picks it
    (signed 32-bit sums)."""
    if count <= 1:
        return 0

    def signed(v):
        v &= 0xFFFFFFFF
        return v - (1 << 32) if v & 0x80000000 else v

    h = signed(signed(x * 73856093) ^ signed(y * 19349663))
    h = signed(h ^ (h >> 13))
    h = signed(h * 0x5BD1E995)
    h = signed(h ^ (h >> 15))
    return (h & 0x7FFFFFFF) % count


REGION_BLEND = 6  # rows either side of a region's edge over which its air's light turns into the next one's


def region_bands(regions: list, height: int) -> list[tuple[dict, int, int]]:
    """A map's regions as (region, first row, row after its last), top to bottom. A map
    that names none is all Earth."""
    by_key = {r["key"]: r for r in REGIONS}
    named = sorted(regions, key=lambda entry: entry[1]) or [("earth", 0)]
    tops = [top for _, top in named] + [height]
    return [(by_key[key], max(top, 0), min(tops[i + 1], height)) for i, (key, top) in enumerate(named)]


def ambient_rows(regions: list, height: int) -> list[tuple[float, float, float]]:
    """The light that hangs in the air of each row: its region's, turning into the
    next region's over REGION_BLEND rows either side of the edge between them."""
    bands = region_bands(regions, height)
    out = []
    for row in range(height):
        index = next((i for i, (_, top, end) in enumerate(bands) if top <= row < end), 0)
        region, top, end = bands[index]
        colour = region["ambient"]
        if index > 0 and row - top < REGION_BLEND:
            t = 0.5 + (row - top + 0.5) / (2 * REGION_BLEND)
            colour = tuple(a + (b - a) * t for a, b in zip(bands[index - 1][0]["ambient"], colour))
        elif index < len(bands) - 1 and end - row <= REGION_BLEND:
            t = 0.5 - (end - row - 0.5) / (2 * REGION_BLEND)
            colour = tuple(a + (b - a) * t for a, b in zip(colour, bands[index + 1][0]["ambient"]))
        out.append(tuple(colour))
    return out


# --- Light ----------------------------------------------------------------------
# The lighting standard (docs/art/tiles-v2.md), the numbers TileLight.cs and the
# preview share; the Unity builder copies them from the manifest into the tile set.
# Light is worked out per tile: 1 is full daylight.
LIGHT = {
    "ambient": 0.04,  # the light everywhere, deep underground included
    "ambient_colour": (0.55, 0.62, 0.85),
    "air_keeps": 0.91,  # the share of its light a tile of air passes on to the next tile
    "solid_keeps": 0.56,  # the share a solid tile passes on: light dies a few tiles into rock
    "torch_colour": (1.0, 0.72, 0.42),
    "torch_intensity": 1.6,  # the light a torch gives its tile
    "torch_glow": 0.35,  # the small flickering point light on top of it
    "torch_glow_radius": 3.0,  # tiles
    "wall_tint": (0.68, 0.70, 0.80),  # what the wall layer is multiplied by: a wall is further back than the ground
}


# The things that give light where they stand, each a sheet of frames: the light a prop
# gives its tile is its colour times its intensity; `embers` rise from a flame.
PROPS = [
    {"key": "torch", "frames": 3, "colour": LIGHT["torch_colour"], "intensity": LIGHT["torch_intensity"], "embers": True},
    {"key": "brazier", "frames": 3, "colour": (1.0, 0.60, 0.32), "intensity": 2.0, "embers": True},
    {"key": "crystal", "frames": 3, "colour": (0.55, 0.82, 1.0), "intensity": 1.3, "embers": False},
    {"key": "lantern", "frames": 3, "colour": (1.0, 0.93, 0.72), "intensity": 1.2, "embers": False},
    # The Earth's: a miner's lamp hung from a nail in the timber, a warmer, smaller light than a torch.
    {"key": "mine_lamp", "frames": 3, "colour": (1.0, 0.78, 0.48), "intensity": 1.5, "embers": False},
    # A fire by the start, and mushrooms that give a cold light in the damp of the Earth's caves.
    {"key": "campfire", "frames": 3, "colour": (1.0, 0.62, 0.30), "intensity": 1.8, "embers": True},
    {"key": "glowshroom", "frames": 3, "colour": (0.35, 1.0, 0.85), "intensity": 1.0, "embers": False},
]


# --- The day ----------------------------------------------------------------------
# What the hour does: the colour of the daylight (what the tile light's sky half is
# multiplied by), the sky's colour at the top and low down, the tint of the far
# backdrop (ridges, clouds) and how much the stars show. Linear between the keys,
# round the clock.
DAY_LENGTH = 480.0  # seconds of play a whole day takes
DAY_START = 10.0  # the hour a scene starts at


def _key(hour, light, top, low, tint, stars):
    return {
        "hour": hour,
        "light": light,
        "top": tuple(round(v / 255, 4) for v in hexes(top)[0]),
        "low": tuple(round(v / 255, 4) for v in hexes(low)[0]),
        "tint": tint,
        "stars": stars,
    }


DAY_KEYS = [
    _key(0.0, (0.30, 0.36, 0.60), "04060f", "101a36", (0.20, 0.25, 0.44), 1.0),
    _key(4.5, (0.30, 0.36, 0.60), "04060f", "101a36", (0.20, 0.25, 0.44), 1.0),
    _key(5.75, (0.42, 0.36, 0.44), "1a2250", "c9705a", (0.50, 0.42, 0.52), 0.5),
    _key(6.75, (0.86, 0.66, 0.52), "2e4a96", "f2a86c", (0.95, 0.74, 0.64), 0.0),
    _key(8.5, (1.0, 0.96, 0.90), "3868bc", "b6dcf0", (1.0, 0.97, 0.93), 0.0),
    _key(12.0, (1.0, 1.0, 1.0), "3b6fc0", "a9d6ef", (1.0, 1.0, 1.0), 0.0),
    _key(16.5, (1.0, 0.97, 0.90), "3a69b6", "b9d8ea", (1.0, 0.96, 0.90), 0.0),
    _key(18.5, (0.95, 0.64, 0.44), "2c2f78", "f08c4e", (0.96, 0.66, 0.52), 0.0),
    _key(19.75, (0.44, 0.34, 0.46), "141440", "7a3f5c", (0.48, 0.38, 0.52), 0.5),
    _key(21.0, (0.30, 0.36, 0.60), "04060f", "101a36", (0.20, 0.25, 0.44), 1.0),
]
SUN_HOURS = (5.5, 19.5)  # the sun is up between these; the moon the rest of the clock


def day_at(hour: float) -> dict:
    """The day's values at an hour (0 .. 24), mixed between the keys either side."""
    hour %= 24.0
    keys = DAY_KEYS + [dict(DAY_KEYS[0], hour=24.0)]
    for a, b in zip(keys, keys[1:]):
        if a["hour"] <= hour <= b["hour"]:
            t = 0.0 if b["hour"] == a["hour"] else (hour - a["hour"]) / (b["hour"] - a["hour"])
            mix = lambda u, v: tuple(x + (y - x) * t for x, y in zip(u, v))  # noqa: E731
            return {
                "hour": hour,
                "light": mix(a["light"], b["light"]),
                "top": mix(a["top"], b["top"]),
                "low": mix(a["low"], b["low"]),
                "tint": mix(a["tint"], b["tint"]),
                "stars": a["stars"] + (b["stars"] - a["stars"]) * t,
            }
    return dict(DAY_KEYS[0], hour=hour)


def sky_arc(hour: float) -> tuple[str, float]:
    """Which body is up at an hour and how far along its arc it is, 0 (rising) .. 1 (setting)."""
    hour %= 24.0
    rise, set_ = SUN_HOURS
    if rise <= hour <= set_:
        return "sun", (hour - rise) / (set_ - rise)
    return "moon", ((hour - set_) % 24.0) / (24.0 - (set_ - rise))
