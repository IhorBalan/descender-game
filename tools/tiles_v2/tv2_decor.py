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
    {"key": "bones", "anchor": "floor", "on": ["sand", "mud", "dis_brick", "ember_sand", "bloodstone", "frost_rock", "ashstone"], "where": "any", "chance": 0.02, "variants": 1},
    {"key": "mushroom", "anchor": "floor", "on": ["dirt", "clay", "stone", "mud"], "where": "cave", "chance": 0.06, "variants": 3},
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
    {"key": "root", "anchor": "ceiling", "on": ["dirt", "mud"], "where": "cave", "chance": 0.22, "variants": 2},
    {"key": "stalactite", "anchor": "ceiling", "on": ["stone", "copper_ore"], "where": "cave", "chance": 0.18, "variants": 3},
]


def _rule(key, anchor, on, chance, variants, **more):
    return dict({"key": key, "anchor": anchor, "on": on, "where": "any", "chance": chance, "variants": variants}, **more)


# What the world's biomes are dressed with (tv2_base.py lists their grounds). A rock's
# spikes and pebbles are the caves' own, drawn in that rock's colours: SPIKED.
SPIKED = {
    "ash": ("ashstone", ["ashstone"], 0.08, 0.20, 0.10),
    "rose": ("rose_rock", ["rose_rock"], 0.10, 0.15, 0.08),
    "dark": ("darkstone", ["darkstone", "gold_ore"], 0.08, 0.18, 0.12),
    "swamp": ("swamp_rock", ["swamp_rock"], 0.0, 0.10, 0.08),
    "blood": ("bloodstone", ["bloodstone"], 0.08, 0.22, 0.0),
    "slate": ("slate", ["slate"], 0.10, 0.16, 0.12),
    "frost": ("frost_rock", ["frost_rock"], 0.0, 0.0, 0.08),
}
DECOR += [
    _rule("cloud_puff", "floor", ["cloud"], 0.30, 3),
    _rule("lily", "floor", ["cloud"], 0.08, 2),
    _rule("grave", "floor", ["ashstone"], 0.05, 3),
    _rule("dead_grass", "floor", ["ashstone"], 0.25, 3),
    _rule("thorn_rose", "floor", ["rose_rock"], 0.12, 2),
    _rule("rose_vine", "ceiling", ["rose_rock"], 0.35, 2, tall=[1, 3], first="rose_vine_mid", middle="rose_vine_mid", last="rose_vine_tip"),
    _rule("reed", "floor", ["mud", "swamp_rock"], 0.18, 3),
    _rule("drip", "ceiling", ["mud"], 0.30, 3),
    _rule("coins", "floor", ["darkstone", "gold_ore"], 0.10, 3),
    _rule("dead_tree", "floor", ["swamp_rock", "bloodstone"], 0.06, 2, tall=[2, 4], first="dead_tree_base", middle="dead_tree_mid", last="dead_tree_top"),
    _rule("moss", "ceiling", ["swamp_rock"], 0.45, 2, tall=[1, 2], first="moss_mid", middle="moss_mid", last="moss_tip"),
    _rule("tomb", "floor", ["dis_brick"], 0.05, 2),
    _rule("chain", "ceiling", ["dis_brick", "slate"], 0.09, 2, tall=[1, 4], first="chain_mid", middle="chain_mid", last="chain_tip"),
    _rule("thorn", "floor", ["bloodstone", "ember_sand"], 0.10, 2),
    _rule("ice_spike", "floor", ["ice", "frost_rock"], 0.10, 3),
    _rule("snow", "floor", ["ice", "frost_rock"], 0.25, 3),
    _rule("icicle", "ceiling", ["ice", "frost_rock"], 0.38, 3),
]
for _name, (_palette, _on, _floor, _ceiling, _pebble) in SPIKED.items():
    if _floor:
        DECOR.append(_rule("stalagmite_" + _name, "floor", _on, _floor, 3))
    if _pebble:
        DECOR.append(_rule("pebble_" + _name, "floor", _on, _pebble, 3))
    if _ceiling:
        DECOR.append(_rule("stalactite_" + _name, "ceiling", _on, _ceiling, 3))
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


def pebble(variant: int, rock: dict = STONE) -> np.ndarray:
    pad = Pad()
    ramp = rock["ramp"]
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
    pad.outline(rock["outline"])
    return pad.a


def spike(pad: Pad, put, cx: float, base: float, height: int, r, rock: dict = STONE):
    """A stone spike of a height, widest at its root; `put` draws a pixel a
    distance from the root."""
    ramp = rock["ramp"]
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


def stalagmite(variant: int, rock: dict = STONE) -> np.ndarray:
    r = rng("stalagmite", variant)
    pad = Pad()
    height, base = [(5, 5), (8, 6), (12, 7)][variant % 3]
    spike(pad, pad.up, 7.5 + r.choice([-1, 0, 1]), base, height, r, rock)
    if variant % 3:
        spike(pad, pad.up, 12.5 if variant % 3 == 1 else 3.5, 3, height // 2, r, rock)
    pad.outline(rock["outline"])
    return pad.a


def stalactite(variant: int, rock: dict = STONE) -> np.ndarray:
    r = rng("stalactite", variant)
    pad = Pad()
    height, base = [(5, 5), (9, 6), (13, 7)][variant % 3]
    spike(pad, pad.down, 7.5 + r.choice([-1, 0, 1]), base, height, r, rock)
    if variant % 3:
        spike(pad, pad.down, 3.5 if variant % 3 == 1 else 12.5, 3, height // 2, r, rock)
    pad.outline(rock["outline"])
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


# --- The biomes: Paradise and the circles ---------------------------------------


def cloud_puff(variant: int) -> np.ndarray:
    """A soft lump of cloud standing on a cloud."""
    pad = Pad()
    white, soft, shade_ = col("ffffff"), col("eef3fb"), col("cfdaf0")
    plan = [[(7, 5, 4)], [(4, 3, 3), (11, 4, 3)], [(3, 2, 2), (8, 4, 4), (13, 2, 2)]][variant % 3]
    for cx, half, height in plan:
        for h in range(-BLEED, height):
            width = half if h < height - 1 else max(half - 2, 1)
            for x in range(cx - width, cx + width + 1):
                pad.up(x, h, white if h >= height - 2 and x <= cx else shade_ if x > cx + width - 2 else soft)
    return pad.a


def lily(variant: int) -> np.ndarray:
    """A white flower with a gold heart, the gardens' own."""
    pad = Pad()
    stem, petal, heart = col("7fbf75"), col("ffffff"), col("ffd24a")
    for cx, height in [[(7, 7)], [(4, 5), (11, 8)]][variant % 2]:
        for h in range(-BLEED, height):
            pad.up(cx, h, stem)
        for dx, dy in ((-1, 0), (1, 0), (0, 1), (-1, 1), (1, 1), (0, 2)):
            pad.up(cx + dx, height + dy, petal)
        pad.up(cx, height + 1, heart)
        pad.up(cx + 1, height // 2, stem)
    return pad.a


def grave(variant: int) -> np.ndarray:
    """A leaning headstone, a cross or a broken slab."""
    pad = Pad()
    ramp, rim = PALETTES["ashstone"]["ramp"], PALETTES["ashstone"]["outline"]
    kind = variant % 3
    if kind == 1:
        for h in range(-BLEED, 10):
            pad.up(7, h, ramp[4])
            pad.up(8, h, ramp[2])
        for x in range(4, 12):
            pad.up(x, 6, ramp[4] if x < 8 else ramp[3])
            pad.up(x, 7, ramp[5] if x < 8 else ramp[3])
    else:
        top = 9 if kind == 0 else 5
        for h in range(-BLEED, top):
            for x in range(4, 12):
                if h >= top - 2 and x in (4, 11) and kind == 0:
                    continue
                if kind == 2 and h == top - 1 and x % 3 == 0:
                    continue
                pad.up(x, h, ramp[4] if x == 4 else ramp[2] if x == 11 else ramp[3])
        if kind == 0:
            for x in range(6, 10):
                pad.up(x, 9, ramp[4])
            for h in range(3, 7):
                pad.up(7, h, ramp[1])
            pad.up(6, 5, ramp[1])
            pad.up(8, 5, ramp[1])
    pad.outline(rim)
    return pad.a


def dead_grass(variant: int) -> np.ndarray:
    """Pale dry blades."""
    r = rng("dead_grass", variant)
    pad = Pad()
    pale = hexes("5e5c68", "7e7c8e", "9896a6", "b5b3c0")
    x = r.randint(2, 4)
    while x < TILE - 2:
        height = r.randint(2, 5)
        for h in range(-BLEED, height):
            pad.up(x + (r.choice([-1, 1]) if h == height - 1 and height > 3 else 0), h, pale[min(h + 1, 3) if h >= 0 else 0])
        x += r.choice([2, 2, 3, 4])
    return pad.a


def bramble(variant: int, stems, dots, name: str) -> np.ndarray:
    """A low tangle of stems with a few dots (thorns, roses) on it."""
    r = rng(name, variant)
    pad = Pad()
    dark, mid = stems
    for start in ((3, 9, 13) if variant % 2 else (5, 11)):
        x, height = start, r.randint(5, 9)
        for h in range(-BLEED, height):
            pad.up(x, h, dark if h < 2 else mid)
            if h > 1 and r.random() < 0.5:
                side = r.choice([-1, 1])
                pad.up(x + side, h, dark)
                if r.random() < 0.6:
                    pad.up(x + 2 * side, h + 1, dots[r.randrange(len(dots))])
            if h > 0 and r.random() < 0.4:
                x = max(2, min(TILE - 3, x + r.choice([-1, 1])))
        pad.up(x, height, dots[0])
    return pad.a


def thorn_rose(variant: int) -> np.ndarray:
    return bramble(variant, hexes("3a1632", "5a2a4e"), hexes("ff7ab8", "ffc0dc", "d8408a"), "thorn_rose")


def thorn(variant: int) -> np.ndarray:
    return bramble(variant, hexes("1a0c0a", "33181a"), hexes("5a2a22", "7a3a2a"), "thorn")


def hanging(variant: int, tip: bool, stems, dots, name: str) -> np.ndarray:
    """A strand that hangs through a cell (or ends in it), with dots along it."""
    r = rng(name, variant, tip)
    pad = Pad()
    dark, mid = stems
    for start in (5, 10) if variant % 2 else (7,):
        x = start
        length = r.randint(7, 12) if tip else DECOR_H - CEILING
        for d in range(-BLEED, length):
            pad.down(x, d, mid if d % 3 else dark)
            if dots and r.random() < 0.28:
                side = r.choice([-1, 1])
                pad.down(x + side, d, dots[r.randrange(len(dots))])
            if d >= 0 and d % 4 == 3:
                x = max(3, min(TILE - 4, x + r.choice([-1, 0, 1])))
        if tip and dots:
            pad.down(x, length, dots[0])
    return pad.a


def rose_vine_mid(variant: int) -> np.ndarray:
    return hanging(variant, False, hexes("3a1632", "6a2a56"), hexes("ff7ab8", "ffc0dc"), "rose_vine")


def rose_vine_tip(variant: int) -> np.ndarray:
    return hanging(variant, True, hexes("3a1632", "6a2a56"), hexes("ff7ab8", "ffc0dc"), "rose_vine")


def moss_mid(variant: int) -> np.ndarray:
    return hanging(variant, False, hexes("1f4a40", "2f6e5c"), hexes("58a088"), "moss")


def moss_tip(variant: int) -> np.ndarray:
    return hanging(variant, True, hexes("1f4a40", "2f6e5c"), hexes("58a088"), "moss")


def chain_links(variant: int, tip: bool) -> np.ndarray:
    """An iron chain: links one over the other, ending in a hook or a weight."""
    pad = Pad()
    iron, lit, rim = col("4a4a56"), col("8a8a98"), col("15151b")
    x = 7 if variant % 2 == 0 else 8
    length = 10 if tip else DECOR_H - CEILING
    for d in range(-BLEED, length):
        if d % 4 in (0, 1):
            pad.down(x, d, lit)
            pad.down(x + 1, d, iron)
        else:
            pad.down(x - 1, d, lit)
            pad.down(x + 2, d, iron)
    if tip:
        if variant % 2 == 0:
            for dx, dd in ((0, 10), (0, 11), (1, 12), (2, 12), (3, 11), (3, 10)):
                pad.down(x + dx - 1, dd, lit if dx == 0 else iron)
        else:
            for dd in range(10, 14):
                for dx in range(-1, 3):
                    pad.down(x + dx, dd, lit if dx == -1 else iron)
    pad.outline(rim)
    return pad.a


def chain_mid(variant: int) -> np.ndarray:
    return chain_links(variant, False)


def chain_tip(variant: int) -> np.ndarray:
    return chain_links(variant, True)


def reed(variant: int) -> np.ndarray:
    """Marsh reeds: thin stalks with dark heads."""
    r = rng("reed", variant)
    pad = Pad()
    stalk, pale, head = col("56733a"), col("8aa058"), col("4a3020")
    for x in [(4, 9, 12), (6, 11), (3, 7, 10, 13)][variant % 3]:
        height = r.randint(8, 15)
        for h in range(-BLEED, height):
            pad.up(x, h, pale if h > height * 0.6 else stalk)
        if r.random() < 0.7:
            for h in range(height - 4, height - 1):
                pad.up(x, h, head)
                pad.up(x + 1, h, head)
    return pad.a


def drip(variant: int) -> np.ndarray:
    """Slime hanging from a ceiling in drops."""
    r = rng("drip", variant)
    pad = Pad()
    dark, mid, lit = col("4a6a1c"), col("74a030"), col("b8e070")
    for x in [(5, 11), (7,), (3, 8, 12)][variant % 3]:
        length = r.randint(4, 11)
        for d in range(-BLEED, length):
            pad.down(x, d, mid)
            if d < 2 or d >= length - 2:
                pad.down(x + 1, d, dark)
        pad.down(x, length - 1, lit)
        pad.down(x, length, mid)
    return pad.a


def coins(variant: int) -> np.ndarray:
    """A heap of gold with a glint on it."""
    r = rng("coins", variant)
    pad = Pad()
    ramp = PALETTES["gold"]["ramp"]
    half, height = [(5, 4), (3, 3), (6, 6)][variant % 3]
    cx = 7 + r.choice([-1, 0, 1])
    for h in range(-1, height):
        width = int(round(half * (1 - max(h, 0) / (height + 0.5))))
        for x in range(cx - width, cx + width + 1):
            pad.up(x, h, ramp[4] if (x + h) % 3 == 0 else ramp[2] if x > cx + width - 2 else ramp[3])
    pad.up(cx - 1, height - 1, ramp[5])
    pad.up(cx + half - 2, 1, ramp[5])
    pad.outline(PALETTES["gold"]["outline"])
    return pad.a


WOOD = hexes("140c0a", "2a1814", "40261e", "5a382a")


def dead_tree_part(variant: int, part: str) -> np.ndarray:
    """A bare black tree, a cell at a time: its foot, its trunk, its crown of branches."""
    r = rng("dead_tree", variant, part)
    pad = Pad()
    cx = 7
    top = DECOR_H if part != "top" else 9
    for h in range(-BLEED, top - BLEED):
        wide = 2 if part == "base" and h < 3 else 1
        for x in range(cx - wide + 1, cx + wide + 1):
            pad.up(x, h, WOOD[2] if x <= cx else WOOD[1])
        if part == "base" and h < 1:
            pad.up(cx - 2, h, WOOD[1])
            pad.up(cx + 3, h, WOOD[0])
    for side in (-1, 1):
        if part == "base" or r.random() < 0.25:
            continue
        at = r.randint(2, 7)
        x, h = cx + (0 if side < 0 else 1), at
        for k in range(r.randint(3, 6)):
            x += side
            h += 1 if k % 2 == 0 or part == "top" else 0
            pad.up(x, h, WOOD[3] if side < 0 else WOOD[1])
            if k == 2 and r.random() < 0.6:
                pad.up(x, h + 1, WOOD[1])
                pad.up(x - side, h + 2, WOOD[0])
    if part == "top":
        for k in range(4):
            pad.up(cx + (k % 2), top - BLEED + k, WOOD[2 if k < 2 else 1])
    return pad.a


def dead_tree_base(variant: int) -> np.ndarray:
    return dead_tree_part(variant, "base")


def dead_tree_mid(variant: int) -> np.ndarray:
    return dead_tree_part(variant, "mid")


def dead_tree_top(variant: int) -> np.ndarray:
    return dead_tree_part(variant, "top")


def tomb(variant: int) -> np.ndarray:
    """An open stone tomb with fire inside it."""
    pad = Pad()
    ramp, rim = PALETTES["dis_brick"]["ramp"], PALETTES["dis_brick"]["outline"]
    fire = hexes("d8521a", "ff9a2e", "ffe07a")
    for h in range(-BLEED, 6):
        for x in range(2, 14):
            pad.up(x, h, ramp[4] if x == 2 else ramp[1] if x == 13 else ramp[5] if h == 5 else ramp[3] if h == 4 else ramp[2])
    for x in range(4, 12):
        pad.up(x, 5, fire[1] if (x + variant) % 3 else fire[2])
        pad.up(x, 4, fire[0])
    for x in ((5, 9) if variant % 2 else (6, 10)):
        pad.up(x, 6, fire[1])
        pad.up(x, 7, fire[0])
    pad.outline(rim)
    return pad.a


def snow(variant: int) -> np.ndarray:
    """A drift of snow lying on the ground."""
    pad = Pad()
    white, soft, shade_ = col("ffffff"), col("d8ecf8"), col("a8c8e0")
    plan = [[(0, 16, 2)], [(1, 9, 3), (9, 7, 2)], [(3, 11, 3)]][variant % 3]
    for x0, width, height in plan:
        for x in range(x0, x0 + width):
            edge = min(x - x0, x0 + width - 1 - x)
            top = min(height, 1 + edge)
            for h in range(-1, top):
                pad.up(x, h, white if h == top - 1 else shade_ if h <= 0 else soft)
    return pad.a


def _spiked(kind: str, rock: str):
    drawer = {"stalagmite": stalagmite, "stalactite": stalactite, "pebble": pebble}[kind]
    return lambda variant: drawer(variant, PALETTES[rock])


DRAWERS = {
    "cloud_puff": cloud_puff,
    "lily": lily,
    "grave": grave,
    "dead_grass": dead_grass,
    "thorn_rose": thorn_rose,
    "thorn": thorn,
    "rose_vine_mid": rose_vine_mid,
    "rose_vine_tip": rose_vine_tip,
    "moss_mid": moss_mid,
    "moss_tip": moss_tip,
    "chain_mid": chain_mid,
    "chain_tip": chain_tip,
    "reed": reed,
    "drip": drip,
    "coins": coins,
    "dead_tree_base": dead_tree_base,
    "dead_tree_mid": dead_tree_mid,
    "dead_tree_top": dead_tree_top,
    "tomb": tomb,
    "snow": snow,
    "ice_spike": _spiked("stalagmite", "ice"),
    "icicle": _spiked("stalactite", "ice"),
    **{kind + "_" + name: _spiked(kind, SPIKED[name][0]) for name in SPIKED for kind in ("stalagmite", "stalactite", "pebble")},
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
