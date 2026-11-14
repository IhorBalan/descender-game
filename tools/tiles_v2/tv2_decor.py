#!/usr/bin/env python3
"""Draws the Tiles V2 dressing and says where it goes: the decor that stands on or
hangs from the ground (tufts, flowers, mushrooms, stalactites, cacti...), the
shade a tile casts on the wall behind it, and the glint of ore. Not a script.

Decor is scattered by rule, never painted by hand: `scatter` picks, for each
open face of the ground, at most one decor from a hash of the cell, so the same
map always gets the same dressing, in the preview and in Unity
(TilesV2Decor.cs does the same sums).
"""
from __future__ import annotations

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
from tv2_terrain import ore_mask

# A decor sprite is a tile wide and 2 px taller at each end, centred on its cell:
# the extra rows tuck behind the ground it stands on (or hangs from), whose edge
# is ragged.
BLEED = 2
DECOR_H = TILE + 2 * BLEED
FLOOR = DECOR_H - 1 - BLEED  # the cell's bottom row
CEILING = BLEED  # the cell's top row

GRASS = PALETTES["grass"]
STONE = PALETTES["stone"]
DIRT = PALETTES["dirt"]

# Where decor goes. `on`: the materials it grows on; `anchor`: floor (it stands in
# the cell above) or ceiling (it hangs in the cell below); `where`: sky (the cell
# is open to the sky), cave (it is not) or any; `chance`: the share of fitting
# cells that get it; `tall`: how many cells it may stack (first, middle, last).
DECOR = [
    {"key": "flower", "anchor": "floor", "on": ["grass"], "where": "sky", "chance": 0.10, "variants": 4},
    {"key": "bush", "anchor": "floor", "on": ["grass"], "where": "sky", "chance": 0.05, "variants": 2},
    {"key": "grass_tall", "anchor": "floor", "on": ["grass"], "where": "sky", "chance": 0.20, "variants": 2},
    {"key": "tuft", "anchor": "floor", "on": ["grass"], "where": "sky", "chance": 0.38, "variants": 3},
    {
        "key": "cactus",
        "anchor": "floor",
        "on": ["sand"],
        "where": "sky",
        "chance": 0.11,
        "variants": 2,
        "tall": [2, 3],
        "first": "cactus_base",
        "middle": "cactus_mid",
        "last": "cactus_top",
    },
    {"key": "dry_bush", "anchor": "floor", "on": ["sand"], "where": "sky", "chance": 0.08, "variants": 2},
    {"key": "bones", "anchor": "floor", "on": ["sand"], "where": "any", "chance": 0.02, "variants": 1},
    {"key": "mushroom", "anchor": "floor", "on": ["dirt", "clay", "stone"], "where": "cave", "chance": 0.06, "variants": 3},
    {"key": "stalagmite", "anchor": "floor", "on": ["stone", "copper_ore"], "where": "cave", "chance": 0.10, "variants": 3},
    {"key": "pebble", "anchor": "floor", "on": ["stone", "dirt", "clay", "copper_ore"], "where": "cave", "chance": 0.12, "variants": 3},
    {
        "key": "vine",
        "anchor": "ceiling",
        "on": ["grass"],
        "where": "any",
        "chance": 0.55,
        "variants": 2,
        "tall": [1, 3],
        "first": "vine_mid",
        "middle": "vine_mid",
        "last": "vine_tip",
    },
    {"key": "root", "anchor": "ceiling", "on": ["dirt"], "where": "cave", "chance": 0.22, "variants": 2},
    {"key": "stalactite", "anchor": "ceiling", "on": ["stone", "copper_ore"], "where": "cave", "chance": 0.18, "variants": 3},
]
FLOOR_SALT, CEILING_SALT = 101, 211


def rule_parts(rule: dict) -> list[str]:
    """The sprite sets a rule draws with, in sheet order."""
    names = [rule.get("first", rule["key"]), rule.get("middle", rule["key"]), rule.get("last", rule["key"])]
    return list(dict.fromkeys(names))


class Pad:
    """One decor sprite being drawn: 16 x 20, see BLEED."""

    def __init__(self):
        self.a = np.zeros((DECOR_H, TILE, 4), dtype=np.uint8)

    def put(self, x: int, y: int, colour, alpha: int = 255):
        if 0 <= x < TILE and 0 <= y < DECOR_H:
            self.a[y, x, :3] = colour
            self.a[y, x, 3] = alpha

    def up(self, x: int, height: int, colour):
        """A pixel `height` px above the floor (0 is the cell's bottom row, -1 and -2 tuck under)."""
        self.put(x, FLOOR - height, colour)

    def down(self, x: int, depth: int, colour):
        """A pixel `depth` px below the ceiling (0 is the cell's top row, -1 and -2 tuck over)."""
        self.put(x, CEILING + depth, colour)

    def outline(self, colour):
        """A 1 px outline round everything drawn so far (not past the cell's sides)."""
        solid = self.a[..., 3] > 0
        edge = np.zeros_like(solid)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            edge |= shift(solid, dx, dy, False)
        edge &= ~solid
        self.a[edge, :3] = colour
        self.a[edge, 3] = 255


def col(value: str):
    return hexes(value)[0]


# --- The plants of the surface --------------------------------------------------


def blade(pad: Pad, r, x: int, height: int, lean: int, tip=None):
    """One blade of grass from under the floor line up, its top leaning a pixel."""
    ramp = GRASS["ramp"]
    for h in range(-BLEED, height):
        dx = lean if h >= height - 2 and height > 3 else 0
        colour = ramp[1] if h < 1 else ramp[2] if h < height // 2 else ramp[3]
        if h == height - 1:
            colour = tip or (ramp[5] if r.random() < 0.4 else ramp[4])
        pad.up(x + dx, h, colour)


def tuft(variant: int) -> np.ndarray:
    r = rng("tuft", variant)
    pad = Pad()
    x = r.randint(2, 4)
    while x < TILE - 2:
        blade(pad, r, x, r.randint(3, 6), r.choice([-1, 0, 0, 1]))
        x += r.choice([1, 2, 2, 3])
        if r.random() < 0.3:
            x += 3
    return pad.a


def grass_tall(variant: int) -> np.ndarray:
    r = rng("grass_tall", variant)
    pad = Pad()
    straw = col("d9c873")
    x = r.randint(2, 3)
    while x < TILE - 2:
        seeded = r.random() < 0.35
        height = r.randint(7, 11)
        blade(pad, r, x, height, r.choice([-1, 0, 1]), straw if seeded else None)
        if seeded:
            pad.up(x, height - 2, straw)
        x += r.choice([2, 2, 3])
    return pad.a


FLOWERS = [("e04a3a", "ffd94a"), ("f6f2e6", "ffc93a"), ("6c8cf0", "fff0a0"), ("f6cc3a", "e8862a")]


def flower(variant: int) -> np.ndarray:
    r = rng("flower", variant)
    pad = Pad()
    ramp = GRASS["ramp"]
    petal, heart = (col(v) for v in FLOWERS[variant % len(FLOWERS)])
    x, height = r.randint(6, 9), r.randint(6, 9)
    for h in range(-BLEED, height):
        pad.up(x, h, ramp[1] if h < 1 else ramp[2])
    side = r.choice([-1, 1])
    pad.up(x + side, height // 2, ramp[3])
    pad.up(x + 2 * side, height // 2 + 1, ramp[4])
    pad.up(x - side, height // 2 - 1, ramp[3])
    top = height
    for dx, dy in ((0, 1), (-1, 0), (1, 0), (0, -1)):
        pad.up(x + dx, top + dy, petal)
    if variant % 2 == 0:
        pad.up(x - 1, top + 1, petal)
        pad.up(x + 1, top + 1, petal)
    pad.up(x, top, heart)
    # A few blades at its foot.
    blade(pad, r, x - 3, 3, 0)
    blade(pad, r, x + 3, 4, 1)
    return pad.a


def bush(variant: int) -> np.ndarray:
    r = rng("bush", variant)
    pad = Pad()
    ramp = GRASS["ramp"]
    cx, half, height = 7.5, 5.2, 7
    for h in range(-BLEED, height):
        for x in range(TILE):
            t = max(h, 0) / height
            width = half * (1 - t * t) ** 0.5
            if abs(x - cx) <= width:
                lit = (x - cx) < -width * 0.2 and h > height * 0.35
                dark = (x - cx) > width * 0.45 or h < 1
                pad.up(x, h, ramp[4] if lit else ramp[1] if dark else ramp[2])
    for _ in range(9):
        x, h = r.randint(3, 12), r.randint(1, height - 1)
        if pad.a[FLOOR - h, x, 3]:
            pad.up(x, h, ramp[5] if x < 8 else ramp[3])
    if variant % 2:
        for _ in range(4):
            x, h = r.randint(3, 12), r.randint(1, height - 2)
            if pad.a[FLOOR - h, x, 3]:
                pad.up(x, h, col("d6323c"))
    pad.outline(GRASS["outline"])
    return pad.a


# --- The desert -----------------------------------------------------------------

CACTUS = {"outline": col("1c3a1c"), "lit": col("7fbf55"), "mid": col("549a40"), "dark": col("3a7433"), "spine": col("dcecae")}


def cactus_trunk(pad: Pad, lo: int, hi: int):
    """The trunk between two heights above the floor, six px wide, ribbed."""
    for h in range(lo, hi):
        for x in range(5, 11):
            colour = CACTUS["lit"] if x == 5 else CACTUS["dark"] if x >= 9 else CACTUS["mid"]
            if x == 7:
                colour = CACTUS["dark"] if h % 2 else CACTUS["mid"]
            pad.up(x, h, colour)


def cactus_arm(pad: Pad, side: int, at: int, r):
    """An arm leaving the trunk sideways at a height and turning up."""
    reach = r.randint(3, 4)
    x0 = 4 if side < 0 else 11
    for k in range(reach):
        for h in (at, at + 1, at + 2):
            pad.up(x0 + side * k, h, CACTUS["mid"] if h > at else CACTUS["dark"])
    tip = x0 + side * (reach - 1)
    for h in range(at + 3, at + r.randint(6, 8)):
        for x in (tip - 1, tip, tip + 1) if side > 0 else (tip - 1, tip, tip + 1):
            pad.up(x, h, CACTUS["lit"] if x == tip - 1 else CACTUS["mid"])


def cactus_base(variant: int) -> np.ndarray:
    pad = Pad()
    cactus_trunk(pad, -BLEED, TILE + BLEED)
    pad.outline(CACTUS["outline"])
    for h in (3, 9, 13):
        pad.up(4 if (h + variant) % 2 else 11, h, CACTUS["spine"])
    return pad.a


def cactus_mid(variant: int) -> np.ndarray:
    r = rng("cactus_mid", variant)
    pad = Pad()
    cactus_trunk(pad, -BLEED, TILE + BLEED)
    cactus_arm(pad, -1 if variant % 2 == 0 else 1, r.randint(2, 4), r)
    pad.outline(CACTUS["outline"])
    for h in (1, 7, 12):
        pad.up(11 if variant % 2 == 0 else 4, h, CACTUS["spine"])
    return pad.a


def cactus_top(variant: int) -> np.ndarray:
    pad = Pad()
    top = 10 if variant % 2 == 0 else 8
    cactus_trunk(pad, -BLEED, top)
    for x in range(6, 10):
        pad.up(x, top, CACTUS["lit"] if x < 8 else CACTUS["mid"])
    pad.outline(CACTUS["outline"])
    if variant % 2:
        for dx, dh, colour in ((0, 2, "f08ab0"), (-1, 1, "e2609a"), (1, 1, "e2609a"), (0, 1, "ffd94a")):
            pad.up(8 + dx, top + dh, col(colour))
    for h in (2, 6):
        pad.up(4 if (h + variant) % 2 else 11, h, CACTUS["spine"])
    return pad.a


def dry_bush(variant: int) -> np.ndarray:
    r = rng("dry_bush", variant)
    pad = Pad()
    wood, light = col("7a5a36"), col("a9824e")
    root = r.randint(6, 9)
    for h in range(-BLEED, 2):
        pad.up(root, h, wood)
    for lean in (-1, 0, 1) if variant % 2 == 0 else (-1, 1):
        x, h = root, 2
        for step in range(r.randint(5, 8)):
            pad.up(x, h, light if step > 3 else wood)
            h += 1
            if r.random() < 0.6:
                x += lean if lean else r.choice([-1, 1])
            if step == 2 and r.random() < 0.7:
                pad.up(x - lean if lean else x + 1, h, wood)
                pad.up(x - 2 * lean if lean else x + 2, h + 1, light)
    return pad.a


def bones(variant: int) -> np.ndarray:
    pad = Pad()
    bone, shade_, hole = col("ece8d6"), col("c3bca4"), col("3a352c")
    for x in range(3, 8):
        for h in range(0, 4):
            if (x, h) in ((3, 3), (7, 3)):
                continue
            pad.up(x, h, shade_ if h == 0 or x == 7 else bone)
    pad.up(4, 1, hole)
    pad.up(6, 1, hole)
    for k in range(5):
        pad.up(9 + k, 0 if k < 3 else 1, bone if k % 4 else shade_)
    pad.up(9, 1, bone)
    pad.up(13, 0, bone)
    pad.outline(col("5a543f"))
    return pad.a


# --- The caves ------------------------------------------------------------------


def mushroom(variant: int) -> np.ndarray:
    pad = Pad()
    stem, stem_dark, rim = col("ebdfc2"), col("c2b392"), col("33201a")
    caps = [("b4432f", "dd7050", "f4e8d8"), ("8a6a4a", "ab8962", "c9ad84"), ("58b8b0", "9fe4dc", "e4fbf8")]
    cap, cap_lit, spot = (col(v) for v in caps[variant % 3])
    plan = [[(7, 4, 3)], [(5, 3, 2), (10, 5, 3)], [(4, 2, 1), (8, 4, 2), (12, 3, 1)]][variant % 3]
    for cx, height, half in plan:
        for h in range(-BLEED, height):
            pad.up(cx, h, stem)
            if half > 1:
                pad.up(cx + 1, h, stem_dark)
        for k in range(2 if half < 3 else 3):
            width = half - (1 if k == (1 if half < 3 else 2) else 0)
            for x in range(cx - width, cx + width + (2 if half > 1 else 1)):
                pad.up(x, height + k, cap_lit if x <= cx - width + 1 and k > 0 else cap)
        pad.up(cx - half + 1, height + 1, spot)
        if half > 2:
            pad.up(cx + 2, height + 1, spot)
    pad.outline(rim)
    return pad.a


def pebble(variant: int) -> np.ndarray:
    pad = Pad()
    ramp = STONE["ramp"]
    plan = [[(5, 5, 3)], [(2, 3, 2), (8, 5, 3)], [(4, 4, 2), (11, 2, 1)]][variant % 3]
    for x0, width, height in plan:
        for h in range(-1, height):
            for x in range(x0, x0 + width):
                rounded = h == height - 1 and x in (x0, x0 + width - 1) and width > 2
                if rounded:
                    continue
                colour = ramp[5] if h == height - 1 else ramp[2] if x == x0 + width - 1 or h <= 0 else ramp[3]
                if x == x0 and h < height - 1:
                    colour = ramp[4]
                pad.up(x, h, colour)
    pad.outline(STONE["outline"])
    return pad.a


def spike(pad: Pad, put, cx: float, base: float, height: int, r):
    """A stone spike of a height, widest at its root; `put` draws a pixel a
    distance from the root."""
    ramp = STONE["ramp"]
    for k in range(-BLEED, height):
        t = max(k, 0) / height
        half = max(base * (1 - t) ** 0.85 / 2, 0.5)
        lean = (r.random() - 0.5) * 0.3
        for x in range(TILE):
            off = x + 0.5 - cx - lean
            if abs(off) <= half:
                colour = ramp[4] if off < -half * 0.35 else ramp[2] if off > half * 0.3 else ramp[3]
                put(x, k, colour)
    put(int(cx), height - 1, ramp[5])


def stalagmite(variant: int) -> np.ndarray:
    r = rng("stalagmite", variant)
    pad = Pad()
    height, base = [(5, 5), (8, 6), (12, 7)][variant % 3]
    spike(pad, pad.up, 7.5 + r.choice([-1, 0, 1]), base, height, r)
    if variant % 3:
        spike(pad, pad.up, 12.5 if variant % 3 == 1 else 3.5, 3, height // 2, r)
    pad.outline(STONE["outline"])
    return pad.a


def stalactite(variant: int) -> np.ndarray:
    r = rng("stalactite", variant)
    pad = Pad()
    height, base = [(5, 5), (9, 6), (13, 7)][variant % 3]
    spike(pad, pad.down, 7.5 + r.choice([-1, 0, 1]), base, height, r)
    if variant % 3:
        spike(pad, pad.down, 3.5 if variant % 3 == 1 else 12.5, 3, height // 2, r)
    pad.outline(STONE["outline"])
    return pad.a


def root(variant: int) -> np.ndarray:
    r = rng("root", variant)
    pad = Pad()
    ramp = DIRT["ramp"]
    for start in (r.randint(3, 6), r.randint(9, 12)) if variant % 2 == 0 else (r.randint(5, 10),):
        x = start
        length = r.randint(6, 12)
        for d in range(-BLEED, length):
            pad.down(x, d, ramp[0] if d > length - 3 else ramp[1])
            if d < 2:
                pad.down(x + 1, d, ramp[0])
            if 1 < d < length - 2 and r.random() < 0.25:
                side = r.choice([-1, 1])
                pad.down(x + side, d + 1, ramp[1])
                pad.down(x + 2 * side, d + 2, ramp[0])
            if d >= 0 and r.random() < 0.45:
                x += r.choice([-1, 1])
                x = max(2, min(TILE - 3, x))
    return pad.a


def vine_stem(d: int, variant: int) -> int:
    """The vine's column at a depth: it sways and is back where it began every tile."""
    return 7 + int(round(1.2 * np.sin(2 * np.pi * d / TILE + (0.0 if variant % 2 == 0 else np.pi))))


def vine_leaves(pad: Pad, variant: int, lo: int, hi: int):
    ramp = GRASS["ramp"]
    for d in range(lo, hi):
        if (d + variant) % 4 == 1:
            side = -1 if (d // 4 + variant) % 2 == 0 else 1
            x = vine_stem(d, variant)
            pad.down(x + side, d, ramp[3])
            pad.down(x + 2 * side, d, ramp[4])
            pad.down(x + side, d + 1, ramp[2])


def vine_mid(variant: int) -> np.ndarray:
    pad = Pad()
    ramp = GRASS["ramp"]
    for d in range(-BLEED, TILE + BLEED):
        pad.down(vine_stem(d, variant), d, ramp[1] if d % 5 else ramp[2])
    vine_leaves(pad, variant, 0, TILE)
    return pad.a


def vine_tip(variant: int) -> np.ndarray:
    pad = Pad()
    ramp = GRASS["ramp"]
    length = 8 if variant % 2 == 0 else 12
    for d in range(-BLEED, length):
        pad.down(vine_stem(d, variant), d, ramp[1] if d % 5 else ramp[2])
    vine_leaves(pad, variant, 0, length - 2)
    x = vine_stem(length - 1, variant)
    pad.down(x, length, ramp[3])
    pad.down(x - 1, length - 1, ramp[4])
    pad.down(x + 1, length - 1, ramp[3])
    pad.down(x, length + 1, ramp[5])
    return pad.a


DRAWERS = {
    "tuft": tuft,
    "grass_tall": grass_tall,
    "flower": flower,
    "bush": bush,
    "cactus_base": cactus_base,
    "cactus_mid": cactus_mid,
    "cactus_top": cactus_top,
    "dry_bush": dry_bush,
    "bones": bones,
    "mushroom": mushroom,
    "pebble": pebble,
    "stalagmite": stalagmite,
    "stalactite": stalactite,
    "root": root,
    "vine_mid": vine_mid,
    "vine_tip": vine_tip,
}


def decor_sprite(part: str, variant: int) -> str:
    return f"tv2_decor_{part}_{variant}"


def decor_sheet() -> tuple[Image.Image, list]:
    """Every decor sprite: a row for each part, its variants side by side. The
    entries are (name, x, y from the bottom, width, height, pivot)."""
    rows = []
    for rule in DECOR:
        for part in rule_parts(rule):
            if part not in [p for p, _ in rows]:
                rows.append((part, rule["variants"]))
    columns = max(count for _, count in rows)
    sheet = np.zeros((len(rows) * DECOR_H, columns * TILE, 4), dtype=np.uint8)
    entries = []
    for row, (part, count) in enumerate(rows):
        for v in range(count):
            sheet[row * DECOR_H : (row + 1) * DECOR_H, v * TILE : (v + 1) * TILE] = DRAWERS[part](v)
            y = sheet.shape[0] - (row + 1) * DECOR_H
            entries.append((decor_sprite(part, v), v * TILE, y, TILE, DECOR_H, (0.5, 0.5)))
    return Image.fromarray(sheet, "RGBA"), entries


# --- Scatter: which decor stands where ------------------------------------------


def scatter(solid_key, free, sky, width: int, height: int) -> list[tuple[int, int, str]]:
    """The decor of a map: (column, row, sprite name) for every cell that gets one.

    `solid_key(x, row)` is the material of a full ground tile at a cell (None for
    air, a slope, anything else), `free(x, row)` whether decor may stand in a cell
    (in the map, no ground, no liquid, no torch), `sky(x, row)` whether the cell
    is open to the sky. Rows run top first; the hash takes Unity's cell (y up).
    """
    placed: dict[tuple[int, int], str] = {}
    for anchor, step, salt in (("floor", -1, FLOOR_SALT), ("ceiling", 1, CEILING_SALT)):
        for row in range(height):
            for x in range(width):
                key = solid_key(x, row)
                if key is None:
                    continue
                first = row + step
                if not free(x, first) or (x, first) in placed:
                    continue
                room = 0
                while room < 4 and free(x, first + step * room) and (x, first + step * room) not in placed:
                    room += 1
                y_up = height - 1 - row
                open_sky = sky(x, first)
                fitting = [
                    rule
                    for rule in DECOR
                    if rule["anchor"] == anchor
                    and key in rule["on"]
                    and rule["where"] in ("any", "sky" if open_sky else "cave")
                    and rule.get("tall", [1, 1])[0] <= room
                ]
                roll = cell_hash(x, y_up, salt) / float(1 << 31)
                chosen = None
                for rule in fitting:
                    if roll < rule["chance"]:
                        chosen = rule
                        break
                    roll -= rule["chance"]
                if chosen is None:
                    continue
                lo, hi = chosen.get("tall", [1, 1])
                count = min(lo + cell_hash(x, y_up, salt + 1) % (hi - lo + 1), room)
                for i in range(count):
                    part = chosen.get("last", chosen["key"])
                    if i < count - 1:
                        part = chosen.get("first", chosen["key"]) if i == 0 else chosen.get("middle", chosen["key"])
                    variant = cell_hash(x, y_up, salt + 2 + i) % chosen["variants"]
                    placed[(x, first + step * i)] = decor_sprite(part, variant)
    return [(x, row, name) for (x, row), name in sorted(placed.items())]


# --- Shade: what a tile casts on the wall behind it ------------------------------

# The alpha of the shade by distance (px) from the tile that casts it. The light
# comes from the top left, so a tile above or to the left casts further.
SHADE_REACH = {
    N: [0.50, 0.42, 0.34, 0.26, 0.18, 0.11, 0.05],
    W: [0.44, 0.35, 0.26, 0.18, 0.11, 0.05],
    S: [0.32, 0.20, 0.11, 0.05],
    E: [0.32, 0.20, 0.11, 0.05],
    NW: [0.40, 0.30, 0.21, 0.13, 0.06],
    NE: [0.30, 0.19, 0.10, 0.04],
    SW: [0.30, 0.19, 0.10, 0.04],
    SE: [0.22, 0.12, 0.05],
}
SHADE_FULL = 0.52  # behind a ground tile itself (seen through its rounded corners)


def shade_canonical(mask: int) -> int:
    """The shade a wall cell shows for the ground around it: every side, and a
    corner only when neither side beside it has ground (else the sides cover it)."""
    out = mask & (N | E | S | W)
    for corner, (a, b) in CORNERS.items():
        if mask & corner and not mask & a and not mask & b:
            out |= corner
    return out


SHADE_MASKS = sorted({shade_canonical(m) for m in range(256)} - {0})
assert len(SHADE_MASKS) == 46


def shade_alpha(mask: int) -> np.ndarray:
    """The shade's alpha over a cell, 0 .. 1."""
    light = np.ones((TILE, TILE), dtype=np.float32)  # what gets through
    ys, xs = np.mgrid[0:TILE, 0:TILE]
    distance = {
        N: ys,
        S: TILE - 1 - ys,
        W: xs,
        E: TILE - 1 - xs,
        NW: np.hypot(xs, ys),
        NE: np.hypot(TILE - 1 - xs, ys),
        SW: np.hypot(xs, TILE - 1 - ys),
        SE: np.hypot(TILE - 1 - xs, TILE - 1 - ys),
    }
    for bit, steps in SHADE_REACH.items():
        if not mask & bit:
            continue
        d = np.floor(distance[bit]).astype(int)
        alpha = np.where(d < len(steps), np.array(steps + [0.0], dtype=np.float32)[np.minimum(d, len(steps))], 0.0)
        light *= 1 - alpha
    return np.minimum(1 - light, 0.62)


def shade_tile(mask: int | None) -> np.ndarray:
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    alpha = np.full((TILE, TILE), SHADE_FULL) if mask is None else shade_alpha(mask)
    a[..., 3] = np.round(alpha * 255).astype(np.uint8)
    return a


def shade_sprite(mask: int | None) -> str:
    return "tv2_shade_full" if mask is None else f"tv2_shade_m{mask}"


def shade_sheet() -> tuple[Image.Image, list]:
    """The shade tiles: the full one, then one for each mask."""
    masks = [None] + SHADE_MASKS
    rows = (len(masks) + COLUMNS - 1) // COLUMNS
    sheet = np.zeros((rows * TILE, COLUMNS * TILE, 4), dtype=np.uint8)
    sprites = []
    for i, mask in enumerate(masks):
        cx, cy = frame_cell(i)
        sheet[cy * TILE : (cy + 1) * TILE, cx * TILE : (cx + 1) * TILE] = shade_tile(mask)
        sprites.append((shade_sprite(mask), cx, cy))
    return Image.fromarray(sheet, "RGBA"), sprites


# --- Glint: ore twinkles in the dark ----------------------------------------------

GLINT_FPS = 10
# What a glinting tile shows, frame by frame: -1 nothing, else one of its four
# frames (a dot and a cross at each of two nuggets). Each cell starts the loop at
# its own moment.
GLINT_LOOP = [-1] * 9 + [0, 1, 0] + [-1] * 7 + [2, 3, 2] + [-1] * 4
GLINT_DOT, GLINT_ARM = (255, 246, 208, 235), (255, 214, 140, 150)


def glint_spots(variant: int) -> list[tuple[int, int]]:
    """Two nugget pixels of an ore tile's variant, away from its edges, lit ones first."""
    mask = ore_mask(variant)
    r = rng("glint", variant)
    inner = [(x, y) for y in range(3, TILE - 3) for x in range(3, TILE - 3) if mask[y, x]]
    lit = [(x, y) for x, y in inner if not mask[y - 1, x]] or inner
    first = r.choice(lit)
    far = sorted(lit, key=lambda p: -abs(p[0] - first[0]) - abs(p[1] - first[1]))
    return [first, far[0]]


def glint_frame(variant: int, frame: int) -> np.ndarray:
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    x, y = glint_spots(variant)[frame // 2]
    a[y, x] = GLINT_DOT
    if frame % 2:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            a[y + dy, x + dx] = GLINT_ARM
    return a


def glint_sprite(variant: int, frame: int) -> str:
    return "tv2_glint_none" if frame < 0 else f"tv2_glint_v{variant}_{frame}"


def glint_sheet() -> tuple[Image.Image, list]:
    """A row for each ore variant: its four frames; the last cell of the sheet is empty."""
    sheet = np.zeros((VARIANTS * TILE, 5 * TILE, 4), dtype=np.uint8)
    sprites = [("tv2_glint_none", 4, 0)]
    for v in range(VARIANTS):
        for f in range(4):
            sheet[v * TILE : (v + 1) * TILE, f * TILE : (f + 1) * TILE] = glint_frame(v, f)
            sprites.append((glint_sprite(v, f), f, v))
    return Image.fromarray(sheet, "RGBA"), sprites
