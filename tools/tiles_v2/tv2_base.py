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
]
TERRAIN_CHARS = {c: key + (":" + slope if slope else "") for c, layer, key, slope in LEGEND if layer == "terrain"}
WALL_CHARS = {c: key for c, layer, key, _ in LEGEND if layer == "wall"}
LIQUID_CHARS = {c: key for c, layer, key, _ in LEGEND if layer == "liquid"}
TORCH_CHAR = "T"  # a torch on the wall at this cell; air for the terrain
SPAWN_CHAR = "P"  # where the hero starts in the playground; air


def read_map(path: str):
    """The map at `path`: (terrain rows, wall rows, properties). A property line
    `@view cave 31 20` is properties["view"]["cave"] = [31, 20]; any other
    `@key value` is properties[key] = value."""
    with open(path, encoding="utf-8") as handle:
        lines = handle.read().replace("\r", "").split("\n")
    properties: dict = {"view": {}}
    grids: list[list[str]] = [[]]
    for line in lines:
        if line.startswith("#"):
            continue
        if line.startswith("@"):
            parts = line[1:].split()
            if parts and parts[0] == "view" and len(parts) == 4:
                properties["view"][parts[1]] = [float(parts[2]), float(parts[3])]
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
