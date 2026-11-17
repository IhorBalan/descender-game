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


def leaf(variant: int) -> Image.Image:
    """A great curled leaf of the Waking Hollow, taller than a house: a stem that rises
    and bends over, a blade either side of it that narrows to a curled tip, veins, a rim
    the dawn has turned to rose."""
    r = rng("eden leaf", variant)
    tall = (150, 190, 120, 220)[variant % 4]
    W, H = int(tall * 0.75) + 20, tall + 12
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
        for d in range(-2, 2):
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
    for x in range(2, W - 8, 2):
        top = H - 26 - (x % 4) - r.randint(0, 2)
        for y in range(top, H):
            art.put(x, y, tone(WHEAT, 3 if (x // 2) % 2 else 2))
        for i in range(7):
            art.put(x + (1 if i % 2 else 0), top - 1 + i, tone(WHEAT, 6 if i < 2 else 5 if i < 5 else 4))
            art.put(x - (1 if i % 3 == 0 else 0), top + i, tone(WHEAT, 4))
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
    W = H = 26 * T
    art = Art(W, H)
    cx = cy = W / 2
    rad = 12 * T
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > rad:
                continue
            ang = math.atan2(dy, dx)
            if d > rad - 26:
                light = -(dx + dy) / rad * 0.55
                mark = rad - 18 < d < rad - 8 and abs((ang * 20 / math.pi) % 1 - 0.5) > 0.4
                edge = d > rad - 3 or d < rad - 23
                art.put(x, y, tone(SILVER, 4 + round(light * 3) + (1 if edge else 0) - (2 if mark else 0)))
            else:
                deep = d / (rad - 26)
                art.put(x, y, (10 + int(18 * deep), 14 + int(26 * deep), 34 + int(48 * deep)))
    for _ in range(46):
        ang, d = r.uniform(0, 6.28), r.uniform(0, rad - 34)
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


def gathered() -> Image.Image:
    """The gathered, far off: a line of small pale figures walking calmly, all one way."""
    W, H = 10 * T, 2 * T
    art = Art(W, H)
    for n in range(9):
        x = 8 + n * 17
        for i in range(16):
            for d in range(-2 if i > 4 else -1, 3 if i > 4 else 2):
                art.put(x + d, H - 17 + i, (226, 226, 238) if d < 0 else (188, 190, 212), 215)
    return art.image()


def bough(mirrored: bool) -> Image.Image:
    """A bough of the Lantern Tree: a limb as thick as a trunk reaching out from it, lesser
    limbs up from that, leaves of teal in heaps along it."""
    r = rng("eden bough")
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
        bundle(art, x, y, r.uniform(16, 24), r.uniform(12, 17), TEAL, r, ("bough", n), gaps=False)
    art.outline(TEAL[0])
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
    for v in range(2):
        add(f"eden_wheat_{v}", lambda v=v: wheat(v), ORDER_GARDEN, sink=2)
        add(f"eden_bough_{v}", lambda v=v: bough(v == 1), ORDER_TREE)
    add("eden_sundial", sundial, ORDER_STAND)
    add("eden_wheel", wheel, ORDER_WALL)
    for long in (6, 12):
        add(f"eden_pipe_h_{long}", lambda long=long: pipe(True, long), ORDER_STAND)
        add(f"eden_pipe_v_{long}", lambda long=long: pipe(False, long), ORDER_STAND)
    add("eden_gear", lambda: gear(0), ORDER_STAND)
    add(f"eden_gear{FRAME}1", lambda: gear(1), ORDER_STAND)
    add(f"eden_gear{FRAME}2", lambda: gear(2), ORDER_STAND)
    add("eden_making_door", making_door, ORDER_WALL)
    add("eden_low_gate", low_gate, ORDER_WALL)
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
STOOD_KEYS = [key for key in PICTURE_KEYS if FRAME + "1" not in key and FRAME + "2" not in key and FRAME + "3" not in key]


def pivot_of(key: str) -> tuple[float, float]:
    """A picture's pivot (x, y from the bottom, 0 .. 1): the middle of its floor line."""
    return (0.5, round(SINK_OF[key] / pictures()[key].height, 6))


def size_of(key: str) -> tuple[int, int]:
    return pictures()[key].size
