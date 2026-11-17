#!/usr/bin/env python3
"""Draws what the Paradise level (make_paradise_map.py) is dressed with, zone by zone,
with the brushes of the Earth's pictures and of the castles of Paradise (tv2_earth.py,
tv2_heaven.py): the great leaves of the Waking Hollow, the orchard's teal trees and the
boughs of its Lantern Tree, the twisting trunks of the Great Vines, the wheat and the
sundial of the Tallied Fields, the Wheel of the Terraces, the pipes, the gears and the
door of the Making-house, the crags and the ring of the Low Gate, the people of the
story, and ivy to hang from everything. Not a script.

Each is a picture a map stands in a cell, lit by the tile light, as the Earth's and the
castles' are. The light is from the top left.
"""
from __future__ import annotations

import math
import re

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403
from tv2_earth import Art, branch, bundle, ramp_of, trunk
from tv2_heaven import (
    AMBER, BIRCH, BLOSSOM, CLOTH, GOLD, MARBLE, ORDER_GARDEN, ORDER_HUNG, ORDER_STAND, ORDER_TREE, ORDER_WALL, SHADOWED,
    T, VINE, WATER, ashlar, column, finial, host_wings, pillar, round_shift, stones, tone, tree,
)  # fmt: skip

LEAF = ramp_of("1c3a14 2c6a22 3e8c2c 5aac38 86c84a b8e06a ecf7a8")
ROSE = ramp_of("5a1f3e e27aa6 fbc4da")
TEAL = ramp_of("06262a 0c4a4c 126a68 1c8c84 34b0a0 6cd8c4 c0f8e8")
PALE = ramp_of("5a6a66 7c908a 98aca4 b4c6be cedcd4 e4eee8 f8fcfa")
FROST = ramp_of("1c2a44 3e5478 546c92 6c86aa 88a2c2 a8c0da e8f2fc")
BARK = ramp_of("14200c 26401a 345624 446e2e 58883a 70a44a 9ccc6c")
WHEAT = ramp_of("5a4208 8a6a10 b08a18 d0a824 e8c63c f6de68 fff3a4")
BRASS = ramp_of("3a2a10 6a4c1c 8c682a a8843a c4a250 e0c474 fbe8a8")
PEARL = ramp_of("5a5866 8e8a96 a39ea8 b8b3bc cbc7cf e2dfe5 f8f7f9")
SILVER = ramp_of("1c2a44 3e5478 546c92 6c86aa 88a2c2 a8c0da cfe0f0")
CYAN = ramp_of("0a4a5a 2ab4c8 8cf0f4 e8ffff")
ROBE = ramp_of("4a4a66 9a98b0 c8c6d8 f0eff6")
TINTS = {"green": LEAF, "teal": TEAL, "pale": PALE, "frost": FROST}


def leaf(variant: int, tall: int | None = None) -> Image.Image:
    """A great curled leaf of the Waking Hollow, taller than a house: a stem that rises
    and bends over, a blade either side of it that narrows to a curled tip, veins, a rim
    the dawn has turned to rose."""
    r = rng("eden leaf", variant)
    tall = tall or (150, 190, 120, 220)[variant % 4]
    W, H = int(tall * 0.75) + 20, tall + 12
    rib = max(2, tall // 70)
    art = Art(W, H)
    side = 1 if variant % 2 == 0 else -1
    x0 = W / 2 - side * W * 0.2
    spine = []
    for i in range(tall):
        t = i / tall
        bend = side * (t**2.2) * W * 0.42 + math.sin(t * 5 + variant) * 2
        droop = (t**3) * tall * 0.16
        spine.append((x0 + bend, H - 6 - i + droop))
    for i, (sx, sy) in enumerate(spine):
        t = i / tall
        half = 4 + math.sin(min(t * 1.25, 1) * math.pi) ** 0.8 * W * 0.28 * (1 - 0.3 * t)
        for d in range(int(-half), int(half) + 1):
            x, y = sx + d, sy + abs(d) * 0.28
            off = d / max(half, 1)
            vein = abs(d) > 2 and (i + abs(d)) % 11 < 2
            k = 4 + (1 if side * off < -0.25 else -1 if side * off > 0.45 else 0) + (1 if t > 0.75 else 0) - (1 if vein else 0)
            colour = ROSE[1 if abs(off) > 0.97 else 2] if abs(off) > 0.9 and t > 0.25 else tone(LEAF, k)
            art.put(x, y, colour)
            art.put(x, y + 1, colour)
    for i, (sx, sy) in enumerate(spine[: int(tall * 0.92)]):
        for d in range(-rib, rib):
            art.put(sx + d, sy, tone(LEAF, 6 if d < 0 else 2))
    art.outline(LEAF[0])
    return art.image()


def ivy(tint: str, variant: int) -> Image.Image:
    """Ivy hanging from an edge: strands of several lengths, leaves down them, a blossom here and there."""
    ramp = TINTS[tint]
    r = rng("eden ivy", tint, variant)
    wide, tall = (3, 4, 2)[variant % 3] * T, (4, 6, 3)[variant % 3] * T
    art = Art(wide, tall)
    for n in range(wide // 5):
        x = 3 + n * 5 + r.uniform(-1.5, 1.5)
        length = int(tall * r.uniform(0.35, 1.0))
        for i in range(length):
            x += r.uniform(-0.35, 0.35)
            art.put(x, i, tone(ramp, 2))
            if i % 3 == n % 3:
                s = r.choice([-1, 1])
                for d in (1, 2):
                    art.put(x + s * d, i + d - 1, tone(ramp, 5 if d == 1 else 4))
                art.put(x + s, i - 1, tone(ramp, 3))
            if i > 6 and r.random() < 0.035:
                for dx, dy, k in ((0, 0, 2), (-1, 0, 1), (1, 0, 1), (0, -1, 2), (0, 1, 1)):
                    art.put(x + dx, i + dy, (ROSE if tint in ("green", "pale") else CYAN)[k])
    return art.image()


def teal_tree(variant: int, sway: int = 0) -> Image.Image:
    """A tree of the orchard under the canopy: the tree of Paradise in leaves of deep teal."""
    return tree(variant + 20, TEAL, tall=(150, 176, 132)[variant % 3], sway=sway)


def vine_trunk(variant: int) -> Image.Image:
    """A trunk of the Great Vines: three stems as thick as a man twisted about one another,
    lit on the left, leaves at the turns, taller than two screens."""
    r = rng("eden vine", variant)
    tall = (22, 28, 18)[variant % 3] * T
    W = 6 * T
    art = Art(W, tall)
    for strand in range(3):
        phase = strand * 2.1 + variant
        for y in range(tall):
            cx = W / 2 + math.sin(y / 23.0 + phase) * 22
            depth = math.cos(y / 23.0 + phase)  # behind or before the others
            half = 9 + depth * 2.5
            for x in range(int(cx - half), int(cx + half)):
                off = (x + 0.5 - cx) / half
                groove = (x * 2 + y // 2) % 9 == 0
                k = 4 + round_shift(off) + (1 if depth > 0.3 else -1 if depth < -0.4 else 0) - (1 if groove else 0)
                if depth > -0.2 or not art.has(x, y):
                    art.put(x, y, tone(BARK, k))
    for _ in range(tall // 26):
        y = r.randint(20, tall - 20)
        x = W / 2 + r.choice([-1, 1]) * r.uniform(14, 30)
        bundle(art, x, y, r.uniform(9, 14), r.uniform(7, 10), LEAF, r, ("vine", variant, y), gaps=False)
    art.outline(BARK[0])
    return art.image()


def wheat(variant: int) -> Image.Image:
    """Wheat, standing in rows that are counted: stalks of one height, heavy heads, a tally post at the row's end."""
    r = rng("eden wheat", variant)
    W, H = 6 * T, 2 * T + 6
    art = Art(W, H)
    for x in range(2, W - (8 if variant < 2 else 2), 2):
        top = H - 26 - (x % 4) - r.randint(0, 2) + (variant - 2) * 3 * (variant >= 2) + (int(3 * math.sin(x * 0.2)) if variant >= 2 else 0)
        for y in range(top, H):
            art.put(x, y, tone(WHEAT, 3 if (x // 2) % 2 else 2))
        for i in range(7):
            art.put(x + (1 if i % 2 else 0), top - 1 + i, tone(WHEAT, 6 if i < 2 else 5 if i < 5 else 4))
            art.put(x - (1 if i % 3 == 0 else 0), top + i, tone(WHEAT, 4))
    if variant >= 2:  # the middle of a row: no post
        return art.image()
    for y in range(H - 22, H):  # the post, and the tally cut in it
        art.put(W - 5, y, MARBLE[5])
        art.put(W - 4, y, MARBLE[3])
    for n in range(4 + variant):
        art.put(W - 5, H - 20 + n * 3, MARBLE[1])
    return art.image()


def sundial() -> Image.Image:
    """The Sundial of the Tallied Fields: a great disc of stone leaning on its plinth, the
    hours cut round its rim in gold, a gnomon of gold whose shadow lies across it and
    never moves."""
    r = rng("eden sundial")
    W, H = 14 * T, 10 * T
    art = Art(W, H)
    cx, cy, rad = W / 2, H - 5.2 * T, 4.6 * T
    ashlar(art, cx - 5 * T, H - 1.5 * T, cx + 5 * T, H, MARBLE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (5 * T)))
    for x in range(int(cx - 5.3 * T), int(cx + 5.3 * T)):
        art.put(x, H - 1.5 * T, MARBLE[6])
        art.put(x, H - 1.5 * T + 1, MARBLE[3])
    for y in range(int(cy - rad), int(cy + rad)):
        for x in range(int(cx - rad), int(cx + rad)):
            d = math.hypot(x + 0.5 - cx, (y + 0.5 - cy) * 1.06)
            if d > rad:
                continue
            ang = math.atan2(y - cy, x - cx)
            rim = d > rad - 9
            tick = rim and abs((ang * 12 / math.pi) % 1 - 0.5) > 0.42
            light = -(x - cx) / rad * 0.5 - (y - cy) / rad * 0.5
            k = 4 + round(light * 2)
            art.put(x, y, GOLD[5] if tick else tone(MARBLE, k + (1 if rim else 0)) if d > rad - 11 else tone(MARBLE, k))
    for i in range(int(rad * 0.95)):  # the shadow that never moves
        for d in range(-2, 3):
            art.put(cx + i * 0.62 + d, cy + i * 0.5, MARBLE[2])
    for i in range(int(rad * 0.9)):  # the gnomon
        for d in range(max(1, 5 - i // 12)):
            art.put(cx + d - 2, cy - i * 0.55, GOLD[6 if d == 0 else 4 if d < 3 else 2])
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            if dx * dx + dy * dy <= 16:
                art.put(cx + dx, cy + dy, GOLD[6] if dx + dy < -2 else GOLD[4] if dx + dy < 2 else GOLD[2])
    art.outline(MARBLE[0])
    return art.image()


def wheel() -> Image.Image:
    """The Wheel mural of the Terraces, the biggest single image there: on a wall of marble
    a wheel of gold with twelve spokes, figures in a line walking round its rim all one
    way, a hub like an eye; a border of gold."""
    r = rng("eden wheel")
    W, H = 44 * T, 30 * T
    art = Art(W, H)
    ashlar(art, 0, 0, W, H, SHADOWED, r, course=8, shift=lambda x: 0)
    for x in range(W):
        for y in (2, 3, H - 4, H - 3):
            art.put(x, y, GOLD[5 if y in (2, H - 4) else 3])
    cx, cy, rad = W / 2, H / 2, 12.5 * T
    for y in range(int(cy - rad - 8), int(cy + rad + 8)):
        for x in range(int(cx - rad - 8), int(cx + rad + 8)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)
            lit = 1 if dx + dy < 0 else 0
            if rad - 10 < d <= rad:
                art.put(x, y, GOLD[5 + lit if d > rad - 3 or d < rad - 7 else 3 + lit])
            elif d <= rad - 10 and abs(math.sin(ang * 6)) < 4.5 / max(d, 6) and d > 26:
                art.put(x, y, GOLD[4 + lit])
            elif d <= 26:
                art.put(x, y, GOLD[6] if d > 22 else tone(CLOTH, 5 if d > 10 else 2) if d > 5 else GOLD[6])
    for n in range(30):  # the gathered, walking the rim
        ang = n / 30 * 2 * math.pi
        px, py = cx + math.cos(ang) * (rad + 14), cy + math.sin(ang) * (rad + 14)
        for i in range(12):
            for d in range(-2 if i > 3 else -1, 3 if i > 3 else 2):
                art.put(px + d, py - 6 + i, tone(MARBLE, 6 if d < 0 else 4))
    return art.image()


def pipe(across: bool, long: int) -> Image.Image:
    """A pipe of the Making-house: brass, warm, banded where its lengths are joined, a line
    of light along its top; `long` tiles of it, lying or standing."""
    W, H = (long * T, T) if across else (T, long * T)
    art = Art(W, H)
    for a in range(long * T):
        band = a % 40 < 4
        for d in range(2, T - 2):
            off = (d - T / 2) / (T / 2 - 2)
            k = 4 - round(off * 2.2) + (1 if band else 0)
            if abs(off) < 0.2:
                k += 1
            x, y = (a, d) if across else (d, a)
            art.put(x, y, tone(BRASS, k))
        if band:
            for d in (1, T - 2):
                x, y = (a, d) if across else (d, a)
                art.put(x, y, tone(BRASS, 3))
    art.outline(BRASS[0])
    return art.image()


def gear(frame: int) -> Image.Image:
    """A wheel of the Making-house's machinery, turning: brass teeth, spokes, a hub."""
    W = H = 5 * T
    art = Art(W, H)
    cx = cy = W / 2
    turn = frame * math.pi / 18
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx) + turn
            tooth = math.sin(ang * 12) > 0.2
            lit = 1 if dx + dy < 0 else -1
            if d < (36 if tooth else 31) and d > 24:
                art.put(x, y, tone(BRASS, 4 + lit))
            elif d <= 24 and (abs(math.sin(ang * 3)) < 5.5 / max(d, 5) or d < 8):
                art.put(x, y, tone(BRASS, 5 + lit if d > 3 else 2))
            elif 21 < d <= 24:
                art.put(x, y, tone(BRASS, 3))
    art.outline(BRASS[0])
    return art.image()


def making_door() -> Image.Image:
    """The door of the Making-house: vast, white, shut, a line of gold down its middle; a
    frame of pearl with a lintel, pipes running into the wall either side, and over it the
    wheel. The gathered go in by it."""
    r = rng("eden making door")
    W, H = 16 * T, 20 * T
    art = Art(W, H)
    cx = W / 2
    ashlar(art, 0, 2 * T, W, H, PEARL, r, course=8, shift=lambda x: round_shift((x + 0.5 - cx) / (W / 2)))
    for y in range(4 * T, H):
        for x in range(int(cx - 5 * T), int(cx + 5 * T)):
            up = 4 * T + 5 * T - y
            if up > 0 and math.hypot(x + 0.5 - cx, up) > 5 * T:
                continue
            off = (x + 0.5 - cx) / (5 * T)
            panel = (y // 28) % 2 == 0 and abs(abs(off) - 0.5) < 0.32 and y % 28 > 3
            art.put(x, y, tone(PEARL, 6 - (1 if panel else 0) - (1 if off > 0.6 else 0)))
        for d in (-1, 0):
            art.put(cx + d, y, GOLD[5 if d < 0 else 3])
    for x in range(W):
        for y in range(2 * T, 2 * T + 6):
            art.put(x, y, tone(PEARL, 6 if y == 2 * T else 4))
        art.put(x, 2 * T + 6, GOLD[4])
    for dy in range(-22, 23):
        for dx in range(-22, 23):
            d = math.hypot(dx, dy)
            if 17 < d <= 22 or (d <= 17 and abs(math.sin(math.atan2(dy, dx) * 4)) < 0.2) or d < 5:
                art.put(cx + dx, 2 * T - 2 + dy * 0.0 + dy, GOLD[5 if dx + dy < 0 else 3])
    art.outline(PEARL[0])
    return art.image()


def low_gate() -> Image.Image:
    """The Low Gate: a vast ring of silver set in the cliff, cut with marks round its
    rim, and in it the dark that is under Paradise, with a few cold stars."""
    r = rng("eden low gate")
    W = H = 32 * T
    art = Art(W, H)
    cx = cy = W / 2
    rad = 15 * T
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > rad:
                continue
            ang = math.atan2(dy, dx)
            if d > rad - 40:
                light = -(dx + dy) / rad * 0.55
                mark = rad - 29 < d < rad - 12 and abs((ang * 18 / math.pi) % 1 - 0.5) > 0.38
                edge = d > rad - 4 or d < rad - 36
                art.put(x, y, tone(SILVER, 4 + round(light * 3) + (1 if edge else 0) - (2 if mark else 0)))
            else:
                deep = d / (rad - 40)
                art.put(x, y, (10 + int(18 * deep), 14 + int(26 * deep), 34 + int(48 * deep)))
    for _ in range(80):
        ang, d = r.uniform(0, 6.28), r.uniform(0, rad - 48)
        art.put(cx + math.cos(ang) * d, cy + math.sin(ang) * d, (200, 220, 255) if r.random() < 0.7 else (255, 255, 255))
    art.outline(SILVER[0])
    return art.image()


def crag(variant: int) -> Image.Image:
    """A spire of the silver cliffs by the Low Gate: split rock, lit cold on its left, snow in its cracks."""
    r = rng("eden crag", variant)
    tall = (11, 15, 8)[variant % 3] * T
    W = 5 * T
    art = Art(W, tall)
    peak = W / 2 + r.uniform(-10, 10)
    for y in range(tall):
        t = y / tall
        half = 3 + t**0.8 * (W / 2 - 4) + math.sin(y / 9 + variant) * 2
        cx = peak + (W / 2 - peak) * t
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            crack = (x * 3 + y) % 17 == 0 or (x - y // 3) % 23 == 0
            k = 4 + round_shift(off) - (2 if crack else 0)
            art.put(x, y, SILVER[6] if crack and off < -0.2 and y % 5 == 0 else tone(SILVER, k))
    art.outline(SILVER[0])
    return art.image()


def figure(who: str) -> Image.Image:
    """One of the story's people, standing by the road: Mereth who tends the orchard, with
    a basket; Ithuriel the guard who sat down, his spear across his knees, wings folded."""
    W, H = 3 * T, 3 * T
    art = Art(W, H)
    cx = W / 2
    sits = who == "ithuriel"
    top = H - (30 if sits else 40)
    if sits:
        for i in range(24):  # folded wings behind
            for side in (-1, 1):
                for d in range(5):
                    art.put(cx + side * (8 + d + i * 0.15), top + 4 + i, tone(MARBLE, 6 - d // 2))
    for y in range(top + 9, H - 1):
        t = (y - top - 9) / (H - top - 10)
        half = 5 + t * (9 if sits else 5)
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            fold = math.sin(off * 7 + t * 3)
            art.put(x, y, ROBE[2 + (1 if off < -0.2 and fold > 0 else -1 if off > 0.5 else 0)])
    for dy in range(-5, 5):
        for dx in range(-4, 5):
            if dx * dx + dy * dy <= 20:
                art.put(cx + dx, top + 5 + dy, (236, 200, 170) if dy > -2 else GOLD[4] if not sits else MARBLE[6])
    if sits:
        for x in range(int(cx - 20), int(cx + 20)):
            art.put(x, H - 13, GOLD[5])
            art.put(x, H - 12, GOLD[2])
    else:
        for dy in range(6):
            for dx in range(-5, 6):
                if abs(dx) <= 5 - dy // 2:
                    art.put(cx + 9 + dx, H - 14 + dy, tone(BARK, 5 if dx < 0 else 3))
        for dx, dy in ((7, -16), (10, -17), (12, -15)):
            art.put(cx + dx, H + dy, CYAN[2])
    art.outline(ROBE[0])
    return art.image()


def bough(mirrored: bool, leaves=None) -> Image.Image:
    """A bough of the Lantern Tree: a limb as thick as a trunk reaching out from it, lesser
    limbs up from that, leaves of teal in heaps along it."""
    r = rng("eden bough")
    leaves = leaves or TEAL
    W, H = 16 * T, 8 * T
    art = Art(W, H)
    for i in range(W - 10):
        t = i / (W - 10)
        y = H - 22 - math.sin(t * 2.4) * 34
        half = 9 - t * 6
        for d in range(int(-half), int(half) + 1):
            art.put(i, y + d, tone(BARK, 5 - round(d / max(half, 1) * 2)))
    for n in range(5):
        x = 40 + n * 42
        y = H - 22 - math.sin(x / (W - 10) * 2.4) * 34
        branch(art, x, y, x + 14, y - 30, 3, BARK)
    for n in range(9):
        x = 26 + n * 26 + r.uniform(-6, 6)
        y = H - 40 - math.sin(x / (W - 10) * 2.4) * 34 + r.uniform(-18, 6)
        bundle(art, x, y, r.uniform(16, 24), r.uniform(12, 17), leaves, r, ("bough", n), gaps=False)
    art.outline(leaves[0])
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if mirrored else image


def glowfruit_frame(frame: int) -> np.ndarray:
    """A fruit of the orchard that gives light: a round fruit on a stem under two leaves, its
    glow swelling and fading over the frames. A lamp, hung where a tree or a bough would bear it."""
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        if 0 <= x < TILE and 0 <= y < TILE:
            a[y, x, :3] = colour
            a[y, x, 3] = 255

    for y in range(0, 4):
        put(8, y, TEAL[2])
    for dx, dy in ((-1, 2), (-2, 3), (1, 1), (2, 2), (3, 2)):
        put(8 + dx, dy, TEAL[5])
    bright = (0, 1, 2)[frame % 3]
    for dy in range(-5, 6):
        for dx in range(-5, 6):
            d = dx * dx + dy * dy
            if d <= 22:
                put(8 + dx, 9 + dy, CYAN[3] if d < 4 + bright * 3 else CYAN[2] if dx + dy < 3 else CYAN[1])
    return a


# --- An old, great place: what makes the level read as one --------------------------------

STONE = ramp_of("1c1d26 464955 5b5e6b 717482 8a8d9a a8abb6 c8cad2")
STEEL = ramp_of("14161c 3c424e 5a6270 808a98 aab4c0 d4dce4 f4f8fc")
FOG = ramp_of("7088b0 a8c0e0 d0e2f6 f2f8ff")


def gate_arch(kind: str) -> Image.Image:
    """The arch a doorway stands in, behind whoever goes through: two piers and a pointed
    arch of cut stone with a ring of tracery in its head, weathered, a line of old gold
    under its spring; or (`vine`) two trunks grown together overhead. Eight tiles wide,
    twelve high, open in the middle."""
    r = rng("eden gate arch", kind)
    W, H = 8 * T, 12 * T
    art = Art(W, H)
    cx, spring = W / 2, 5 * T
    if kind == "vine":
        for side in (-1, 1):
            for i in range(H - 6):
                t = i / (H - 6)
                x = cx + side * (2.6 * T - (t**2.4) * 2.4 * T) + math.sin(i / 13 + side) * 3
                half = 8 - t * 4
                for d in range(int(-half), int(half) + 1):
                    art.put(x + d, H - 1 - i, tone(BARK, 4 - round(d / max(half, 1) * 2) + (1 if (i + d) % 9 else 0)))
        for n in range(11):
            a = math.pi * n / 10
            bundle(art, cx - math.cos(a) * 2.2 * T, 2.2 * T - math.sin(a) * 1.4 * T + r.uniform(-4, 4), r.uniform(10, 15), r.uniform(8, 11), LEAF, r, ("arch", n), gaps=False)
        art.outline(BARK[0])
        return art.image()
    for side in (-1, 1):
        px = cx + side * 3 * T
        ashlar(art, px - T, spring, px + T, H, STONE, r, course=8, shift=lambda x, px=px: round_shift((x + 0.5 - px) / T))
        for x in range(int(px - T - 3), int(px + T + 3)):  # a base, and a capital with old gold under it
            for y, k in ((H - 6, 6), (H - 5, 4), (H - 1, 2), (spring, 6), (spring + 1, 5), (spring + 2, 2)):
                art.put(x, y, tone(STONE, k))
            art.put(x, spring + 3, GOLD[3])
    for y in range(4, spring + 1):
        for x in range(4, W - 4):
            # A pointed arch: two arcs struck from the far springing points.
            outer = max(math.hypot(x + 0.5 - (cx - 2 * T), y - spring), math.hypot(x + 0.5 - (cx + 2 * T), y - spring))
            if 4 * T < outer <= 6 * T - 6:
                ring = outer > 6 * T - 12 or outer < 4 * T + 6
                art.put(x, y, tone(STONE, (6 if ring else stones(x, y) - 1) + (1 if x < cx else -1)))
    for dy in range(-13, 14):  # the ring in its head
        for dx in range(-13, 14):
            d = math.hypot(dx, dy)
            if 9 < d <= 13 or (d <= 9 and abs(math.sin(math.atan2(dy, dx) * 3)) < 0.18):
                art.put(cx + dx, 2.2 * T + dy, tone(STONE, 6 if dx + dy < 0 else 3))
    art.outline(STONE[0])
    return art.image()


def gothic_window() -> Image.Image:
    """A window of an old hall: two lancets and a round light over them in a pointed frame
    of stone, glass in lead with a light behind it, dim and warm."""
    W, H = 5 * T, 10 * T
    art = Art(W, H)
    cx = W / 2
    for y in range(H):
        for x in range(W):
            outer = max(math.hypot(x + 0.5 - (cx - 1.2 * T), y - 4 * T), math.hypot(x + 0.5 - (cx + 1.2 * T), y - 4 * T)) if y < 4 * T else 0
            inside = (outer < 3.6 * T) if y < 4 * T else abs(x + 0.5 - cx) < 2.3 * T
            if not inside:
                continue
            frame = (outer > 3.6 * T - 6) if y < 4 * T else abs(x + 0.5 - cx) > 2.3 * T - 6
            mullion = abs(x + 0.5 - cx) < 3 and y > 3 * T
            rose = math.hypot(x + 0.5 - cx, y - 2.6 * T)
            if frame or mullion or 13 < rose <= 17 or y > H - 5:
                art.put(x, y, tone(STONE, 5 if x < cx else 3))
            else:
                lead = (x + y) % 6 == 0 or (x - y) % 6 == 0
                pane = ((x + y) // 6 * 3 + (x - y) // 6 * 5) % 7
                art.put(x, y, STONE[0] if lead else AMBER[1 + pane % 3] if pane % 3 else GOLD[3])
    art.outline(STONE[0])
    return art.image()


def buttress(mirrored: bool) -> Image.Image:
    """A flying buttress: a pier of stone with a pinnacle on it, and an arm of stone flung
    from it to the wall it holds, an arch under the arm."""
    r = rng("eden buttress")
    W, H = 8 * T, 13 * T
    art = Art(W, H)
    px = 1.5 * T
    ashlar(art, px - T, 3 * T, px + T, H, STONE, r, course=8, shift=lambda x: round_shift((x + 0.5 - px) / T))
    for i in range(3 * T):  # the pinnacle
        half = max(1, (3 * T - i) / (3 * T) * T * 0.8)
        for x in range(int(px - half), int(px + half) + 1):
            art.put(x, 3 * T - 1 - i, tone(STONE, 5 if x < px else 3))
    for x in range(int(px), W):
        t = (x - px) / (W - px)
        top = 4.2 * T + t * 3.6 * T
        under = top + 14 + math.sin(t * math.pi) * 22
        for y in range(int(top), int(top) + 9):
            art.put(x, y, tone(STONE, 6 if y == int(top) else stones(x, y) - 1))
        for y in range(int(under), int(under) + 5):
            art.put(x, y, tone(STONE, 4 if y == int(under) else 3))
    for x in range(int(px - T - 3), int(px + T + 3)):
        art.put(x, 3 * T, tone(STONE, 6))
        art.put(x, 3 * T + 1, tone(STONE, 2))
        art.put(x, H - 5, tone(STONE, 6))
    art.outline(STONE[0])
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if mirrored else image


def ruin(variant: int) -> Image.Image:
    """What is left of a column: a stump on its base, broken off aslant, and a drum of it lying by; or its capital fallen and half in the ground."""
    W, H = 5 * T, 5 * T
    art = Art(W, H)
    if variant % 2 == 0:
        cx, tall = 1.6 * T, (3.4, 2.2)[variant // 2 % 2] * T
        for y in range(int(H - tall), H):
            cut = (y - (H - tall)) * 0.9
            for x in range(int(cx - 11), int(cx + 11)):
                if y < H - tall + 14 and x - (cx - 11) > cut + 2:
                    continue
                off = (x + 0.5 - cx) / 11
                flute = int((off + 1) * 5) % 2
                art.put(x, y, tone(STONE, 5 + round_shift(off) - flute))
        for x in range(int(cx - 15), int(cx + 15)):
            for y, k in ((H - 6, 6), (H - 5, 5), (H - 4, 4), (H - 1, 2)):
                art.put(x, y, tone(STONE, k))
        for y in range(H - 20, H - 2):  # a drum fallen by it
            for x in range(int(3.1 * T), int(4.6 * T)):
                if math.hypot((x - 3.85 * T) / 12, (y - (H - 11)) / 9) <= 1:
                    art.put(x, y, tone(STONE, 5 if y < H - 13 else 3 if x > 3.9 * T else 4))
    else:
        for y in range(H - 22, H):
            lean = (y - (H - 22)) * 0.25
            for x in range(int(T + lean), int(3.8 * T + lean)):
                leaf = (x + y) % 7 < 2 and y < H - 8
                art.put(x, y, tone(STONE, 6 if y == H - 22 else 3 if leaf else 5 if x < 2.2 * T else 4))
        for x in range(int(T - 4), int(4 * T)):
            art.put(x, H - 1, tone(STONE, 2))
    art.outline(STONE[0])
    return art.image()


def sword(coiled: bool = False) -> Image.Image:
    """A sword left standing in the ground; or (`coiled`) the twisted sword a fire is lit round, where the lost rest."""
    W, H = 2 * T, 4 * T
    art = Art(W, H)
    cx = W / 2
    metal = BRASS if coiled else STEEL
    tall = 40 if coiled else 46
    for i in range(tall):
        y = H - 1 - i
        wob = round(math.sin(i / 3.5) * 1.6) if coiled else 0
        half = 1 if coiled or i >= tall - 6 else 2
        for d in range(-half, half + 1):
            art.put(cx + wob + d, y, tone(metal, 6 if d < 0 else 4 if d == 0 else 2))
    hilt = H - 1 - tall
    for d in range(-7, 8):
        art.put(cx + d, hilt, tone(metal, 5 if d < 0 else 3))
        art.put(cx + d, hilt + 1, tone(metal, 2))
    for i in range(1, 9):
        art.put(cx, hilt - i, tone(BARK if not coiled else BRASS, 3))
        art.put(cx - 1, hilt - i, tone(BARK if not coiled else BRASS, 5))
    for dx, dy in ((-1, -10), (0, -10), (1, -10), (-1, -9), (0, -11)):
        art.put(cx + dx, hilt + dy, tone(metal, 5))
    art.outline(metal[0])
    return art.image()


def knight() -> Image.Image:
    """A statue of one who kept this place: hooded, in a long mantle, both hands on a great
    sword set point down before him, on a block of stone. Three times the hero's height."""
    r = rng("eden knight")
    W, H = 5 * T, 9 * T
    art = Art(W, H)
    cx = W / 2
    ashlar(art, cx - 1.8 * T, H - 1.5 * T, cx + 1.8 * T, H, STONE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (1.8 * T)))
    top = H - 1.5 * T
    shoulder = top - 5.4 * T
    for y in range(int(shoulder), int(top)):
        t = (y - shoulder) / (top - shoulder)
        half = 13 + t * 14 + 2 * math.sin(t * 8)
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            fold = math.sin(off * 8 + t * 2)
            art.put(x, y, tone(STONE, 4 + round_shift(off) + (1 if fold > 0.6 else -1 if fold < -0.5 else 0)))
    for dy in range(-13, 10):  # the hood
        for dx in range(-11, 12):
            if dx * dx + (dy * 0.85) ** 2 <= 121:
                face = dy > -3 and abs(dx) < 6
                art.put(cx + dx, shoulder - 6 + dy, STONE[0] if face else tone(STONE, 5 if dx + dy < -4 else 3))
    for y in range(int(shoulder + 1.6 * T), int(top - 3)):  # the sword
        for d in (-2, -1, 0, 1):
            art.put(cx + d, y, tone(STONE, 6 if d < 0 else 3))
    for d in range(-12, 13):
        art.put(cx + d, shoulder + 1.6 * T, tone(STONE, 6))
        art.put(cx + d, shoulder + 1.6 * T + 1, tone(STONE, 2))
    for x in range(int(cx - 2 * T), int(cx + 2 * T)):
        art.put(x, int(top), tone(STONE, 6))
        art.put(x, int(top) + 1, tone(STONE, 2))
    art.outline(STONE[0])
    return art.image()


def fog(frame: int) -> Image.Image:
    """The fog that stands in a great door: a veil of pale mist from sill to lintel, threads of it drifting up, frame by frame."""
    W, H = 4 * T, 8 * T
    art = Art(W, H)
    for y in range(H):
        for x in range(W):
            off = abs(x + 0.5 - W / 2) / (W / 2)
            wave = math.sin(x * 0.55 + (y + frame * 5) * 0.21) + math.sin(x * 0.23 - (y + frame * 7) * 0.13 + 1.3)
            alpha = (1 - off**1.6) * (150 + 45 * wave) * min(1.0, (H - y) / 10 + 0.3) * min(1.0, y / 6 + 0.4)
            if alpha > 18:
                art.put(x, y, FOG[3 if wave > 0.9 else 2 if wave > -0.2 else 1], int(min(alpha, 235)))
    return art.image()


def chain(variant: int) -> Image.Image:
    """A chain hung from a roof, a link at a time, long or short; the long one has a hook."""
    W, H = T, (5, 8)[variant % 2] * T
    art = Art(W, H)
    for i in range(H - (6 if variant % 2 else 0)):
        link = i % 6
        for d in ((-2, 2) if link in (1, 2, 3, 4) else (-1, 0, 1)):
            art.put(8 + d, i, tone(STEEL, 5 if d < 0 else 3))
    if variant % 2:
        for dx, dy in ((0, 0), (1, 1), (2, 2), (2, 3), (1, 4), (0, 5), (-1, 5), (-2, 4)):
            art.put(8 + dx, H - 6 + dy, tone(STEEL, 5))
    return art.image()


# --- Depth, air and wear: what stands far off, what passes close to the eye, what time has done ---

ORDER_FAR = -298  # far off behind a place: over the wall at its back, behind everything in it
ORDER_DECAL = -99  # on a floor's own stone: over the tiles, behind the ledges and the hero
ORDER_NEAR = 28  # mist and light in the air before the hero


def mix(colour, haze, amount: float):
    """A colour gone toward the colour of the air by `amount`."""
    return tuple(int(round(c * (1 - amount) + h * amount)) for c, h in zip(colour[:3], haze))


def hazed(image: Image.Image, haze, amount: float, scale: float = 1.0, foot: float = 0.0) -> Image.Image:
    """A picture as it is seen far off: smaller, and gone toward the colour of the air; more
    so toward its foot by `foot`, where the mist lies."""
    if scale != 1.0:
        image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.NEAREST)
    a = np.array(image.convert("RGBA")).astype(np.float32)
    rows = np.linspace(0, 1, a.shape[0]).reshape(-1, 1, 1)
    amounts = np.clip(amount + foot * rows**2, 0, 0.97)
    a[..., :3] = a[..., :3] * (1 - amounts) + np.array(haze, dtype=np.float32) * amounts
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def stood(canvas: Image.Image, image: Image.Image, middle: float, foot: float):
    """Stands a picture on a canvas: its middle at `middle`, its foot at `foot`."""
    canvas.paste(image, (int(middle - image.width / 2), int(foot - image.height)), image)


def far_hollow() -> Image.Image:
    """Far off in the Waking Hollow: more of the great leaves and the trees in blossom, small, in the rose of the dawn."""
    r = rng("eden far hollow")
    W, H = 36 * T, 12 * T
    canvas = Image.new("RGBA", (W, H))
    haze = (238, 206, 196)
    for depth, (scale, amount, n) in enumerate(((0.4, 0.74, 9), (0.6, 0.56, 6))):
        for i in range(n):
            x = (i + 0.5) / n * W + r.uniform(-22, 22)
            what = tree(r.randint(0, 3), BLOSSOM) if (i + depth) % 3 == 0 else leaf(r.randint(0, 3))
            stood(canvas, hazed(what, haze, amount, scale), x, H + 3)
    return canvas


def far_orchard() -> Image.Image:
    """Far off in the orchard: the rows going on, tree behind tree, dark under the canopy, their fruit small lights."""
    r = rng("eden far orchard")
    W, H = 36 * T, 10 * T
    canvas = Image.new("RGBA", (W, H))
    haze = (8, 40, 46)
    for depth, (scale, amount, n) in enumerate(((0.45, 0.8, 13), (0.65, 0.62, 9))):
        for i in range(n):
            x = (i + 0.5) / n * W  # exact rows: every one is counted
            stood(canvas, hazed(teal_tree(i % 3), haze, amount, scale), x, H + 2)
            for _ in range(2):
                fx, fy = int(x + r.uniform(-16, 16) * scale * 2), int(H - (70 + r.uniform(0, 60)) * scale)
                if 0 <= fx < W and 0 <= fy < H and canvas.getpixel((fx, fy))[3]:
                    canvas.putpixel((fx, fy), CYAN[2] + (255,))
                    canvas.putpixel((min(fx + 1, W - 1), fy), mix(CYAN[1], haze, 0.3) + (255,))
    return canvas


def far_vines() -> Image.Image:
    """Far off among the Great Vines: more trunks, as great, going up out of sight, pale in the mist that lies at their feet."""
    r = rng("eden far vines")
    W, H = 30 * T, 26 * T
    canvas = Image.new("RGBA", (W, H))
    haze = (172, 204, 184)
    for scale, amount, places in ((0.5, 0.82, (0.08, 0.36, 0.62, 0.9)), (0.85, 0.66, (0.22, 0.5, 0.8))):
        for n, at in enumerate(places):
            trunk_ = vine_trunk(n + (1 if scale < 0.6 else 0))
            if scale < 0.6:  # the furthest go up out of the picture: one trunk on another
                tall = Image.new("RGBA", (trunk_.width, trunk_.height * 2))
                tall.paste(trunk_, (0, 0))
                tall.paste(trunk_, (0, trunk_.height))
                trunk_ = tall
            far_ = np.array(hazed(trunk_, haze, amount, scale, foot=0.2))
            for y in range(min(72, far_.shape[0])):  # its top thins into the air, in steps, as pixels do
                far_[y, :, 3] = (far_[y, :, 3] * (int(y / 12) + 0.5) / 6).astype(np.uint8)
            stood(canvas, Image.fromarray(far_, "RGBA"), at * W + r.uniform(-8, 8), H)
    return canvas


def far_fields() -> Image.Image:
    """Far off in the Tallied Fields: fields to the horizon, their rows running away, a
    line of the gathered small on a ridge, all walking one way."""
    r = rng("eden far fields")
    W, H = 44 * T, 9 * T
    art = Art(W, H)
    haze = (250, 226, 172)
    ridges = ((H * 0.3, 8, 0.011, 0.8), (H * 0.5, 11, 0.017, 0.62), (H * 0.74, 12, 0.008, 0.42))
    crest = {}
    for n, (top, swell, f, amount) in enumerate(ridges):
        for x in range(W):
            y0 = top + math.sin(x * f + n * 2.1) * swell + math.sin(x * f * 2.7 + n) * swell * 0.3
            ends = min(1.0, x / (70 + n * 40), (W - 1 - x) / (70 + n * 40))
            y0 = H - (H - y0) * (ends * ends * (3 - 2 * ends))
            crest[(n, x)] = y0
            for y in range(int(y0), H):
                down = y - y0
                stripe = int((x + down * (1.6 if n % 2 else -1.6)) / (5 + n * 2)) % 2
                k = 6 if down < 1 else 5 - stripe
                art.put(x, y, mix(WHEAT[k], haze, amount))
    for i in range(34):  # the gathered, on the middle ridge
        x = int(W * 0.18 + i * 9)
        y = crest[(1, x)]
        for up in range(1, 8):
            for d in (0, 1) if up < 6 else (0,):
                art.put(x + d, y - up, mix((214, 214, 232), haze, 0.45))
    for i in range(7):  # ricks, on the near one
        x = int(W * (0.06 + i * 0.14) + r.uniform(-10, 10))
        y = crest[(2, min(max(x, 0), W - 1))]
        for up in range(9):
            half = 6 - up * 0.6
            for d in range(int(-half), int(half) + 1):
                art.put(x + d, y - up, mix(WHEAT[5 if d < 0 else 3], haze, 0.36))
    return art.image()


def far_terraces() -> Image.Image:
    """Far off on the Terraces: more terraces, arcade over arcade, going up and back, with clipped trees along them."""
    r = rng("eden far terraces")
    W, H = 40 * T, 12 * T
    art = Art(W, H)
    haze = (244, 214, 190)
    step = 3.6 * T
    for n in (2, 1, 0):
        amount = 0.46 + n * 0.16
        x0, x1 = int(n * 4.5 * T + (2 - n) * T), int(W - n * 3 * T - (2 - n) * 2 * T)
        top = int(H - (n + 1) * step)
        for x in range(x0, x1):
            bay = (x - x0) % 22
            for y in range(top, H):
                down = y - top
                arch = 5 <= bay <= 17 and down > 10 and (down > 17 or math.hypot(bay - 11, down - 17) <= 6.5)
                k = 6 if down == 0 else 2 if down == 1 else 1 if arch else 5 - (1 if bay < 3 else 0) - (1 if down > step - 6 else 0)
                art.put(x, y, mix(MARBLE[k], haze, amount + (0.1 if arch else 0)))
        for i in range(int((x1 - x0) / 44)):  # clipped green along it
            cx = x0 + 22 + i * 44 + r.randint(-3, 3)
            for up in range(1, 15):
                half = 1 if up < 4 else 4.5 * math.sin((up - 3) / 12 * math.pi) ** 0.6
                for d in range(int(-half), int(half) + 1):
                    art.put(cx + d, top - up, mix(LEAF[3 if d < 0 else 2], haze, amount))
    return art.image()


def far_house(size: int) -> Image.Image:
    """The Making-house, seen from afar: a vast pale house with a dome, wings and stacks,
    its door a white slit with a line of gold down it, the wheel over the door, its
    windows lit. Three sizes: it comes nearer the whole way along its approach."""
    s = (0.45, 0.75, 1.15)[size]
    amount = (0.66, 0.5, 0.34)[size]
    haze = (168, 160, 190)
    u = T * s  # a tile of the house, in pixels of the picture
    W, H = int(36 * u), int(24 * u)
    art = Art(W, H)
    cx = W / 2
    for y in range(H):
        for x in range(W):
            side = (x + 0.5 - cx) / u
            ax, up = abs(side), (H - y - 0.5) / u
            hall, wings, drum = ax < 11 and up < 14, ax < 16.5 and up < 9, ax < 5 and up < 17
            dome = math.hypot(ax, (up - 17) * 1.1) < 5 and up >= 17
            stack = abs(ax - 13.6) < 0.9 and up < (15 if side < 0 else 18)
            if not (hall or wings or drum or dome or stack):
                continue
            k = 5 + (1 if side < -6 else 0) - (1 if side > 4 else 0) - (1 if side > 11.5 else 0)
            colour = PEARL[max(1, min(6, k))]
            cornice = any(abs(up - line) < 0.22 for line in (14, 9, 17)) and not stack
            door = ax < 3 and (up < 7 or math.hypot(ax, up - 7) < 3)
            window = (9.8 < up < 11.6 and ax > 3.6 and ax < 10.2 and int(ax * 1.5) % 2 == 0) or (4.5 < up < 6.6 and ax > 11.6 and ax < 16 and int(ax * 1.5) % 2 == 0)
            ring = abs(math.hypot(side, up - 12.2) - 1.5) < 0.3 or (math.hypot(side, up - 12.2) < 1.5 and abs(math.sin(math.atan2(up - 12.2, side) * 4)) < 0.18)
            less = 0.0
            if door:
                colour = GOLD[5] if ax < 0.16 else PEARL[6]
                less = 0.12
            elif ring:
                colour, less = GOLD[5], 0.2
            elif window:
                colour, less = AMBER[3], 0.3
            elif cornice:
                colour = PEARL[6]
            elif stack:
                colour = BRASS[4 if side < 0 else 3]
            elif dome:
                colour = PEARL[(6 if side < -1.6 else 5 if side < 2 else 4) - (1 if abs(math.sin(side * 1.3)) < 0.12 else 0)]
            elif (up * 2) % 2 < 0.14 or (hall and up > 9 and abs((ax * 0.5) % 1 - 0.5) > 0.44):
                colour = PEARL[max(1, k - 1)]  # a course of its stone; a pilaster's edge
            if stack and int(up * 1.5) % 4 == 0:
                colour = BRASS[5]
            art.put(x, y, mix(colour, haze, max(0.0, amount - less)))
    return art.image()


def ridge(art: Art, peaks, haze, amount: float, seed: float = 0.0):
    """A ridge of the silver cliffs far off: spires of split rock, each (middle, height,
    half its foot), lit cold on the left, snow on what faces up."""

    def top_of(x):
        high = 0.0
        for cx, tall, half in peaks:
            off = abs(x - cx) / half
            if off < 1:
                high = max(high, tall * (1 - off) ** 1.5 + tall * 0.12 * (1 - off))
        ends = max(0.0, min(1.0, x / 48, (art.w - 1 - x) / 48))
        return art.h - (high + 6 + math.sin(x * 0.21 + seed) * 2.5 + math.sin(x * 0.07 + seed * 3) * 4) * ends

    for x in range(art.w):
        top = top_of(x)
        slope = top_of(x + 2) - top_of(x - 2)  # over zero: it falls away to the right
        for y in range(int(top), art.h):
            down = y - top
            crack = (x * 3 + y) % 19 == 0 or (x * 2 - y) % 31 == 0
            k = 4 + (1 if slope < -1 else -1 if slope > 1 else 0) - (1 if crack else 0) - (1 if down > 70 else 0)
            snow = down < 2 + (x % 5 == 0) * 2 + (2 if abs(slope) < 1.5 else 0)
            art.put(x, y, mix(SILVER[6] if snow else tone(SILVER, k), haze, amount))


def far_gate(size: int) -> Image.Image:
    """The Low Gate, seen from afar: the ring in its cliff, cold, small; nearer the second time."""
    scale, amount = ((0.25, 0.62), (0.5, 0.46))[size]
    haze = (98, 118, 162)
    ring = hazed(low_gate(), haze, amount, scale)
    W, H = ring.width + 14 * T, ring.height + 7 * T
    art = Art(W, H)
    wide = ring.width
    ridge(art, ((W * 0.5, H * 0.92, wide * 1.15), (W * 0.2, H * 0.6, wide * 0.5), (W * 0.82, H * 0.7, wide * 0.55), (W * 0.36, H * 0.8, wide * 0.4), (W * 0.66, H * 0.84, wide * 0.36)), haze, amount + 0.12, size)
    canvas = art.image()
    stood(canvas, ring, W / 2, H - T)
    return canvas


def far_wheel() -> Image.Image:
    """The Wheel, seen from below on the way up the Terraces: its gold ring and its spokes over everything, pale with distance."""
    W = H = 13 * T
    art = Art(W, H)
    cx = cy = W / 2
    rad = 6.2 * T
    haze = (246, 216, 184)
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)
            lit = 1 if dx + dy < 0 else 0
            if rad - 9 < d <= rad:
                art.put(x, y, mix(GOLD[5 + lit if d > rad - 3 or d < rad - 6 else 3 + lit], haze, 0.5))
            elif d <= rad - 9 and (abs(math.sin(ang * 6)) < 3.2 / max(d, 5) and d > 14 or d <= 14):
                art.put(x, y, mix(GOLD[4 + lit], haze, 0.56))
    return art.image()


def far_cliffs() -> Image.Image:
    """Far off by the Low Gate: more of the silver cliffs, spire behind spire, cold and blue."""
    r = rng("eden far cliffs")
    W, H = 40 * T, 17 * T
    canvas = Image.new("RGBA", (W, H))
    haze = (112, 132, 176)
    for depth, (amount, n, high) in enumerate(((0.8, 9, 0.95), (0.6, 6, 0.62))):
        art = Art(W, H)
        peaks = [((i + 0.5) / n * W + r.uniform(-20, 20), H * high * r.uniform(0.55, 1.0), r.uniform(44, 86)) for i in range(n)]
        ridge(art, peaks, haze, amount, depth * 1.7)
        image = art.image()
        canvas.paste(image, (0, 0), image)
    return canvas


# Close to the eye: dark, and passed in front of. Each zone's own.
FRONTS = (
    ("leaf", (10, 20, 12), (24, 44, 22)),  # the Hollow and the Vines: blades of a great leaf
    ("fruit", (4, 18, 20), (10, 40, 42)),  # the orchard: a low branch, a fruit on it
    ("wheat", (30, 22, 6), (58, 44, 12)),  # the fields: ears of wheat
    ("stone", (24, 20, 24), (52, 44, 48)),  # the terraces: a balustrade, broken
    ("brass", (24, 16, 8), (50, 36, 16)),  # the Making-house: a pipe and its valve
    ("crag", (10, 14, 26), (26, 36, 58)),  # the Gate: split rock
)


def front(kind: int, sway: int = 0) -> Image.Image:
    """A dark shape close to the eye, passed in front of, of a zone's own kind. Near black,
    with a rim of the zone's colour where the light catches it, so it frames the view and
    never reads as something to stand on. Two tiles of it go under its floor line."""
    name, ink, rim = FRONTS[kind]
    r = rng("eden front", name)
    W, H = 7 * T, 6 * T
    art = Art(W, H)
    floor = H - 2 * T
    if name == "leaf":
        for n, (x0, tall, lean) in enumerate(((20, 56, 0.9), (48, 40, -0.7), (78, 62, 0.4), (96, 30, -1.0))):
            for h in range(-2 * T, tall):
                t = max(h, 0) / tall
                half = 2 + math.sin(min(t * 1.1, 1) * math.pi) * 7
                x = x0 + lean * t * t * 30 + sway * t * t
                for d in range(int(-half), int(half) + 1):
                    art.put(x + d, floor - h, ink)
    elif name == "fruit":
        for i in range(W - 8):
            t = i / (W - 8)
            y = floor - 26 - math.sin(t * 2.6) * 18 + sway * t
            for d in range(-2, 3 - int(t * 2)):
                art.put(i, y + d, ink)
            if i % 13 == 6:
                for dy in range(-7, 8):
                    for dx in range(-9, 10):
                        if (dx / 9) ** 2 + (dy / 7) ** 2 <= 1:
                            art.put(i + dx, y - 8 + dy + (i % 3), ink)
        for fx in (30, 74):
            fy = floor - 26 - math.sin(fx / (W - 8) * 2.6) * 18 + 9
            for dy in range(-3, 4):
                for dx in range(-3, 4):
                    if dx * dx + dy * dy <= 10:
                        art.put(fx + dx, fy + dy, mix(CYAN[1], ink, 0.45))
        art.rect(0, floor - 30, 4, H, ink)
    elif name == "wheat":
        for _ in range(22):
            x, tall, lean = r.randint(3, W - 4), r.randint(26, 54), r.uniform(-0.4, 0.4)
            for h in range(-2 * T, tall):
                t = max(h, 0) / tall
                xx = x + lean * t * t * 12 + sway * t * t
                art.put(xx, floor - h, ink)
                if h > tall - 9:
                    art.put(xx - 1, floor - h, ink)
                    art.put(xx + 1, floor - h, ink)
    elif name == "stone":
        art.rect(0, floor - 30, W, floor - 24, ink)  # the rail, broken off
        for x in range(W):
            if x > W * 0.62 + math.sin(x) * 2:
                for y in range(int(floor - 30), int(floor - 24)):
                    art.a[y, x, 3] = 0
        for n in range(5):
            cx = 9 + n * 21
            if cx > W * 0.6:
                break
            for y in range(int(floor - 24), H):
                up = (floor - y) / 24
                half = 3 + (3.5 * math.sin(up * math.pi) if 0 < up < 1 else 3)
                for d in range(int(-half), int(half) + 1):
                    art.put(cx + d, y, ink)
        art.rect(0, floor - 3, W, H, ink)
        for dy in range(-6, 7):  # a block fallen from it
            for dx in range(-9, 10):
                art.put(W * 0.82 + dx + dy * 0.3, floor - 7 + dy, ink)
    elif name == "brass":
        for x in range(W):
            for d in range(-6, 7):
                art.put(x, floor - 22 + d, ink)
            if x % 38 < 4:
                for d in range(-8, 9):
                    art.put(x, floor - 22 + d, ink)
        for y in range(int(floor - 22), H):
            for d in range(-5, 6):
                art.put(26 + d, y, ink)
        for dy in range(-11, 12):  # the wheel of a valve
            for dx in range(-11, 12):
                d = math.hypot(dx, dy)
                if 8 < d <= 11 or (d <= 8 and (abs(dx) < 1.2 or abs(dy) < 1.2)):
                    art.put(78 + dx, floor - 40 + dy, ink)
        art.rect(77, floor - 30, 80, floor - 22, ink)
    else:
        for n, (cx, tall, half0) in enumerate(((22, 58, 16), (62, 36, 13), (92, 48, 11))):
            peak = cx + (n - 1) * 5
            for h in range(-2 * T, tall):
                t = 1 - max(h, 0) / tall
                half = 1 + t**0.8 * half0
                mid = peak + (cx - peak) * t
                for x in range(int(mid - half), int(mid + half) + 1):
                    art.put(x, floor - h, ink)
    for y in range(H):  # the rim the light catches
        for x in range(W - 1):
            if art.has(x, y) and not art.has(x - 1, y) and y < floor:
                art.put(x, y, rim)
    return art.image()


def crack(variant: int) -> Image.Image:
    """A crack in a floor of cut stone: down from its top, forked, a chip gone at the lip."""
    r = rng("eden crack", variant)
    W, H = 3 * T, 2 * T
    art = Art(W, H)
    dark = (30, 30, 44)
    x = W / 2 + r.uniform(-8, 8)
    for y in range(H - r.randint(2, 10)):
        x += r.choice((-1, 0, 0, 1))
        art.put(x, y, dark, 215 if y < 14 else 150)
        if y < 5:
            art.put(x + 1, y, dark, 190)
        if y in (7, 15) and r.random() < 0.8:
            fx = x
            for i in range(r.randint(5, 11)):
                fx += r.choice((-1, 1)) if i == 0 else (1 if fx > x else -1)
                art.put(fx, y + i // 2, dark, 150)
    for dx in range(-3, 4):  # the chip at the lip
        for dy in range(0, 3 - abs(dx) // 2):
            art.put(W / 2 + r.uniform(-1, 1) + dx + (variant - 1) * 9, dy, dark, 200)
    return art.image()


def moss(tint: str, variant: int) -> Image.Image:
    """What has grown over a floor of cut stone: moss in the gardens' green, pale lichen in
    the Making-house, rime by the Gate. It lies along the top, deeper here and there, a
    strand of it down the face."""
    ramp = TINTS[tint]
    r = rng("eden moss", tint, variant)
    W, H = (4, 6, 3)[variant % 3] * T, T + 10
    art = Art(W, H)
    top = 4  # the floor's top row in the picture
    for x in range(W):
        edge = min(x, W - 1 - x)
        deep = (2.5 + 2.5 * math.sin(x * 0.31 + variant) + r.uniform(0, 2)) * min(1.0, edge / 6)
        for d in range(int(deep) + 1):
            art.put(x, top + d, tone(ramp, 5 if d == 0 else 4 if d < 2 else 3))
        if edge > 2 and r.random() < 0.3:
            for up in range(1, r.randint(2, 4)):
                art.put(x, top - up, tone(ramp, 6 if tint != "green" else 5))
        if edge > 3 and r.random() < 0.09:
            for d in range(int(deep), int(deep) + r.randint(4, 12)):
                art.put(x, top + d, tone(ramp, 3))
    return art.image()


UNDERS = {"root": None, "marble": MARBLE, "pearl": PEARL, "silver": SILVER}


def under(kind: str, variant: int) -> Image.Image:
    """What a floor in the air has under it: the earth it was lifted with and the roots out
    of it, in the gardens; a mass of its own stone, broken off, after. In shadow, as the
    underside of a thing is."""
    r = rng("eden under", kind, variant)
    wide, tall = (6, 9, 4)[variant % 3], (3, 4, 2)[variant % 3]
    W, H = wide * T, tall * T + T
    art = Art(W, H)
    ramp = UNDERS[kind]
    for x in range(W):
        t = (x + 0.5) / W
        belly = math.sin(t * math.pi) ** 0.7 * (tall * T - 6) * (0.8 + 0.2 * math.sin(x * 0.19 + variant * 2))
        belly += math.sin(x * 0.6) * 1.5
        for y in range(int(belly)):
            down = y / max(belly, 1)
            if ramp is None:
                stone_ = (x * 7 + y * 3) % 31 < 3
                colour = (86, 62, 40) if stone_ else ((58, 40, 26), (48, 32, 22), (38, 26, 18))[min(2, int(down * 3))]
                if y < 2:
                    colour = (70, 50, 32)
            else:
                k = 3 - int(down * 2) + (1 if (x // 11 + y // 7) % 3 == 0 else 0) - (1 if y % 7 == 0 or (x + (y // 7) * 5) % 11 == 0 else 0)
                colour = tone(ramp, k)
            art.put(x, y, colour)
    if ramp is None:
        for n in range(wide * 2):  # roots out of it
            x = r.uniform(6, W - 6)
            t = x / W
            y = math.sin(t * math.pi) ** 0.7 * (tall * T - 6) * 0.75
            length = r.randint(6, 18)
            for i in range(length):
                x += r.uniform(-0.6, 0.6)
                art.put(x, y + i, (120, 96, 64) if i < length - 3 else (150, 124, 86))
    art.outline((26, 20, 22) if ramp is None else ramp[0])
    return art.image()


def mist(frame: int) -> Image.Image:
    """Mist lying along a floor, before the hero: thin, pale, drifting, frame by frame."""
    W, H = 18 * T, 3 * T
    art = Art(W, H)
    for y in range(H):
        for x in range(W):
            ends = min(1.0, x / 40, (W - 1 - x) / 40)
            height = (y / H) ** 1.5
            wave = math.sin((x + frame * 9) * 0.045 + y * 0.12) + math.sin((x - frame * 6) * 0.021 - y * 0.07 + 1.9)
            alpha = ends * height * (62 + 30 * wave)
            if alpha > 9:
                art.put(x, y, FOG[3 if wave > 0.7 else 2], int(min(alpha, 120)))
    return art.image()


def rays(variant: int) -> Image.Image:
    """Shafts of low sun down through leaves or between pillars: slanted, warm, thin as breath."""
    r = rng("eden rays", variant)
    W, H = 9 * T, 11 * T
    art = Art(W, H)
    shafts = [(r.uniform(0.1, 0.9) * W, r.uniform(7, 16), r.uniform(0.6, 1.0)) for _ in range(4 + variant)]
    for y in range(H):
        for x in range(W):
            fall = (y / H)
            alpha = 0.0
            for x0, wide, strength in shafts:
                off = abs(x - (x0 - y * 0.34)) / wide
                if off < 1:
                    alpha += (1 - off) ** 1.5 * strength
            alpha *= 44 * (1 - fall**2 * 0.75) * min(1.0, y / 12)
            if alpha > 5:
                art.put(x, y, (255, 240, 196), int(min(alpha, 70)))
    return art.image()


def great_leaf() -> Image.Image:
    """The Rising Leaf: one leaf as tall as a tower, behind the stair the hero goes up; the stair is its rib."""
    return leaf(1, tall=560)


def lantern_crown() -> Image.Image:
    """The crown of the Lantern Tree: a whole screen of teal leaves heaped on boughs, and
    the fruit that gives the orchard its light all through it."""
    r = rng("eden lantern crown")
    W, H = 40 * T, 22 * T
    art = Art(W, H)
    cx = W / 2
    for n in range(7):  # the limbs
        ang = math.pi * (0.12 + 0.76 * n / 6)
        branch(art, cx, H - 4, cx - math.cos(ang) * W * 0.4, H - 30 - math.sin(ang) * H * 0.62, 9 - abs(n - 3), BARK)
    heaps = []
    for n in range(170):
        ang = r.uniform(0, math.pi)
        d = r.uniform(0.02, 1.0) ** 0.5
        x, y = cx - math.cos(ang) * d * (W / 2 - 40), H - 60 - math.sin(ang) * d * (H - 108)
        heaps.append((y, x))
    for y, x in sorted(heaps):
        bundle(art, x, y, r.uniform(30, 46), r.uniform(21, 30), TEAL, r, ("crown", int(x), int(y)), gaps=False)
    for n in range(46):  # the fruit
        ang, d = r.uniform(0, math.pi), r.uniform(0.15, 0.95)
        x, y = int(cx - math.cos(ang) * d * (W / 2 - 40)), int(H - 40 - math.sin(ang) * d * (H - 100))
        if art.has(x, y):
            for dy in range(-2, 3):
                for dx in range(-2, 3):
                    if dx * dx + dy * dy <= 5:
                        art.put(x + dx, y + dy, CYAN[3] if dx * dx + dy * dy < 2 else CYAN[2] if dx + dy < 2 else CYAN[1])
    art.outline(TEAL[0])
    return art.image()


def piston(frame: int) -> Image.Image:
    """A piston of the Making-house, working: a cylinder of brass on the floor and its rod going up and down in it, a weight on the rod."""
    W, H = 3 * T, 6 * T
    art = Art(W, H)
    cx = W / 2
    rise = (0, 10, 18, 8)[frame % 4]
    for y in range(int(H - 3 * T - rise - 6), H - 2 * T):  # the rod
        for d in range(-2, 3):
            art.put(cx + d, y, tone(STEEL, 5 - abs(d) + (1 if d < 0 else 0)))
    for dy in range(-7, 8):  # the weight
        for dx in range(-12, 13):
            art.put(cx + dx, H - 3 * T - rise - 12 + dy, tone(BRASS, 5 if dy < -4 else 4 - round(dx / 6)))
    for y in range(H - 2 * T - 6, H):  # the cylinder
        for dx in range(-16, 17):
            off = dx / 16
            band = (y - (H - 2 * T - 6)) % 14 < 2
            art.put(cx + dx, y, tone(BRASS, 4 - round(off * 2.2) + (1 if band else 0)))
    art.outline(BRASS[0])
    return art.image()


WALKERS = 8


def gathered(frame: int = 0) -> Image.Image:
    """The gathered, far off: a line of small pale figures walking calmly, all one way: a
    step on with every frame, so the line never ends and never arrives."""
    W, H = 12 * T, 2 * T
    art = Art(W, H)
    apart = 16
    for n in range(-1, W // apart + 2):
        x = n * apart + frame * apart / WALKERS
        bob = 1 if (frame + n) % 4 < 2 else 0
        ends = min(1.0, x / 26, (W - x) / 26)
        if ends <= 0.05:
            continue
        for i in range(16 - bob):
            for d in range(-2 if i > 4 else -1, 3 if i > 4 else 2):
                art.put(x + d, H - 17 + bob + i, (226, 226, 238) if d < 0 else (188, 190, 212), int(215 * ends))
    return art.image()


def broken_knight() -> Image.Image:
    """A keeper's statue that has not been kept: the head gone, a crack down it, half its sword."""
    image = knight().transpose(Image.FLIP_LEFT_RIGHT)
    a = np.array(image)
    h, w = a.shape[:2]
    top = int(h - 1.5 * T - 5.4 * T)
    for y in range(0, top + 4):  # the head, off at the neck, aslant
        for x in range(w):
            if y < top - 2 + (x - w / 2) * 0.25:
                a[y, x, 3] = 0
    for y in range(top + 6, h - 2 * T):  # the crack
        x = int(w / 2 + 8 + math.sin(y * 0.4) * 2 + (y - top) * 0.12)
        if a[y, x, 3]:
            a[y, x, :3] = STONE[0]
    art = Art(w, h)
    art.a = a
    art.outline(STONE[0])
    return art.image()


# --- What there is, and where its floor line lies ------------------------------------------

LEAVES, IVIES, TEAL_TREES, VINES, CRAGS = 4, 3, 3, 3, 3
FRAMES = 4
GLOW, FRAME = "_glow", "_f"


def _plan():
    plan = []

    def add(key, paint, order, sink=0, lit=True):
        plan.append((key, paint, order, lit, sink))

    def hung(key, paint, order):
        """A picture that hangs: its floor line is a tile under its top, so it is stood in the cell under what it hangs from."""
        plan.append((key, paint, order, True, None))

    for v in range(LEAVES):
        add(f"eden_leaf_{v}", lambda v=v: leaf(v), ORDER_TREE - 1, sink=6)
    for tint in TINTS:
        for v in range(IVIES):
            hung(f"eden_ivy_{tint}_{v}", lambda a=(tint, v): ivy(*a), ORDER_HUNG)
    for v in range(TEAL_TREES):
        add(f"eden_teal_tree_{v}", lambda v=v: teal_tree(v), ORDER_TREE, sink=T)
        for frame in range(1, FRAMES):
            add(f"eden_teal_tree_{v}{FRAME}{frame}", lambda a=(v, frame): teal_tree(*a), ORDER_TREE, sink=T)
    for v in range(VINES):
        add(f"eden_vine_{v}", lambda v=v: vine_trunk(v), ORDER_TREE - 1, sink=T)
    for v in range(4):
        add(f"eden_wheat_{v}", lambda v=v: wheat(v), ORDER_GARDEN, sink=2)
    for v in range(2):
        add(f"eden_bough_{v}", lambda v=v: bough(v == 1), ORDER_TREE)
        add(f"eden_vine_bough_{v}", lambda v=v: bough(v == 1, LEAF), ORDER_TREE)
    add("eden_sundial", sundial, ORDER_STAND)
    add("eden_wheel", wheel, ORDER_WALL)
    for long in (6, 12):
        add(f"eden_pipe_h_{long}", lambda long=long: pipe(True, long), ORDER_STAND)
        add(f"eden_pipe_v_{long}", lambda long=long: pipe(False, long), ORDER_STAND)
    add("eden_gear", lambda: gear(0), ORDER_STAND)
    add(f"eden_gear{FRAME}1", lambda: gear(1), ORDER_STAND)
    add(f"eden_gear{FRAME}2", lambda: gear(2), ORDER_STAND)
    add("eden_making_door", making_door, ORDER_WALL)
    add("eden_low_gate", low_gate, ORDER_WALL, sink=3 * T)  # half sunk: it rises out of the ground it is set in
    for v in range(CRAGS):
        add(f"eden_crag_{v}", lambda v=v: crag(v), ORDER_TREE - 1, sink=4)
    for rows, wide in ((10, 2), (10, 1)):
        add(f"eden_pillar_{rows}x{wide}", lambda a=(rows, wide): pillar(*a), ORDER_STAND)
    # What makes it an old, great place: arches to go through, windows, buttresses, what is broken, who kept it.
    for kind in ("stone", "vine"):
        add(f"eden_arch_{kind}", lambda kind=kind: gate_arch(kind), ORDER_STAND)
    add("eden_window", gothic_window, ORDER_WALL + 1)
    for v in range(2):
        add(f"eden_buttress_{v}", lambda v=v: buttress(v == 1), ORDER_TREE - 2)
        hung(f"eden_chain_{v}", lambda v=v: chain(v), ORDER_HUNG)
    for v in range(3):
        add(f"eden_ruin_{v}", lambda v=v: ruin(v), ORDER_GARDEN)
    add("eden_sword", lambda: sword(False), ORDER_GARDEN)
    add("eden_bonfire", lambda: sword(True), ORDER_STAND)
    add("eden_knight", knight, ORDER_STAND)
    add("eden_fog", lambda: fog(0), ORDER_HUNG + 2, lit=False)
    for frame in range(1, FRAMES):
        add(f"eden_fog{FRAME}{frame}", lambda frame=frame: fog(frame), ORDER_HUNG + 2, lit=False)
    add("eden_mereth", lambda: figure("mereth"), ORDER_STAND)
    add("eden_ithuriel", lambda: figure("ithuriel"), ORDER_STAND)
    add("eden_gathered", gathered, ORDER_TREE - 2)
    for frame in range(1, WALKERS):
        add(f"eden_gathered{FRAME}{frame}", lambda frame=frame: gathered(frame), ORDER_TREE - 2)
    # Depth: what stands far off behind a place (a map's `life far` marks).
    for name, paint in (("hollow", far_hollow), ("orchard", far_orchard), ("vines", far_vines), ("fields", far_fields), ("terraces", far_terraces), ("cliffs", far_cliffs)):
        add(f"eden_far_{name}", paint, ORDER_FAR, sink=T)
    for v in range(3):
        add(f"eden_far_house_{v}", lambda v=v: far_house(v), ORDER_FAR + 1, sink=T)
    for v in range(2):
        add(f"eden_far_gate_{v}", lambda v=v: far_gate(v), ORDER_FAR + 1, sink=T)
    add("eden_far_wheel", far_wheel, ORDER_FAR + 1)
    # What passes close to the eye (`life front`), each zone's own; the first three sway.
    for k in range(len(FRONTS)):
        add(f"eden_front_{k}", lambda k=k: front(k), 40, sink=2 * T, lit=False)
        if k < 3:
            for f, lean in ((1, 2), (2, 4)):
                add(f"eden_front_{k}_s{f}", lambda a=(k, lean): front(*a), 40, sink=2 * T, lit=False)
    # Wear: cracks in the stone and what has grown on it; and what a floor in the air has under it.
    for v in range(3):
        add(f"eden_crack_{v}", lambda v=v: crack(v), ORDER_DECAL, sink=2 * T)
        for tint in ("green", "pale", "frost"):
            add(f"eden_moss_{tint}_{v}", lambda a=(tint, v): moss(*a), ORDER_DECAL, sink=T + 6)
        for kind in UNDERS:
            hung(f"eden_under_{kind}_{v}", lambda a=(kind, v): under(*a), ORDER_HUNG)
    # Air: mist along a floor, shafts of low sun.
    add("eden_mist", lambda: mist(0), ORDER_NEAR, lit=False)
    for frame in range(1, FRAMES):
        add(f"eden_mist{FRAME}{frame}", lambda frame=frame: mist(frame), ORDER_NEAR, lit=False)
    for v in range(2):
        add(f"eden_rays_{v}", lambda v=v: rays(v), ORDER_NEAR, lit=False)
    # The great things, greater.
    add("eden_great_leaf", great_leaf, ORDER_TREE - 2, sink=6)
    add("eden_lantern_crown", lantern_crown, ORDER_TREE)
    add("eden_piston", lambda: piston(0), ORDER_STAND)
    for frame in range(1, FRAMES):
        add(f"eden_piston{FRAME}{frame}", lambda frame=frame: piston(frame), ORDER_STAND)
    add("eden_knight_1", broken_knight, ORDER_STAND)
    return plan


PLAN = _plan()
PICTURE_KEYS = [key for key, *_ in PLAN]
ENTRIES = [
    {"key": key, "kind": "picture", "scroll": 1.0, "scrollUp": 1.0, "rise": 0, "drift": 0.0, "order": order, "lit": lit, "tinted": False}
    for key, _, order, lit, _ in PLAN
]
_DRAWN: dict[str, Image.Image] = {}


def pictures() -> dict[str, Image.Image]:
    """Every picture of the level, drawn (once), by key."""
    if not _DRAWN:
        for key, paint, *_ in PLAN:
            _DRAWN[key] = paint()
    return dict(_DRAWN)


def _sink(key: str, sink):
    return pictures()[key].height - T if sink is None else sink


class _Sinks(dict):
    """How far under its floor line each picture goes on: a hung one's is worked out from its height when asked."""

    def __missing__(self, key):
        raise KeyError(key)


SINK_OF = _Sinks()
for _key, *_rest, _s in PLAN:
    SINK_OF[_key] = _sink(_key, _s)
STOOD_KEYS = [key for key in PICTURE_KEYS if not re.search(r"_[fs]\d+$", key)]  # what a map stands; frames come with them


def pivot_of(key: str) -> tuple[float, float]:
    """A picture's pivot (x, y from the bottom, 0 .. 1): the middle of its floor line."""
    return (0.5, round(SINK_OF[key] / pictures()[key].height, 6))


def size_of(key: str) -> tuple[int, int]:
    return pictures()[key].size
