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
from tv2_earth import Art, _seed, branch, bundle, noise, ramp_of, trunk
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


def leaf(variant: int, tall: int | None = None, fine: int = 1) -> Image.Image:
    """A great curled leaf of the Waking Hollow, taller than a house: a stem that rises
    and bends over, a blade either side of it that narrows to a curled tip, lit on the
    half that faces the dawn, veins running out from its rib, a bite out of its edge here
    and there, a rim the dawn has turned to rose. `fine` draws it that many times the
    draft's size: the same leaf, with lesser veins between its veins and a finer rim."""
    r = rng("eden leaf", variant)
    f = fine
    draft = tall or (150, 190, 120, 220)[variant % 4]
    tall = draft * f
    W, H = (int(draft * 0.75) + 20) * f, (draft + 12) * f
    rib = max(2, draft // 70) * f
    apart = max(11, draft // 13) * f  # between two of its veins
    art = Art(W, H)
    mottle = noise(("eden leaf", variant, draft), W, H, max(5.0, draft / 26) * f)
    grain = noise(("eden leaf grain", variant, draft), W, H, 2.4) if f > 1 else None
    side = 1 if variant % 2 == 0 else -1
    x0 = W / 2 - side * W * 0.2
    spine = []
    for i in range(tall):
        t = i / tall
        bend = side * (t**2.2) * W * 0.42 + math.sin(t * 5 + variant) * 2 * f
        droop = (t**3) * tall * 0.16
        spine.append((x0 + bend, H - 6 * f - i + droop))
    bites = [(r.uniform(0.3, 0.85), r.choice([-1, 1]), r.uniform(4, 7) * tall / 150) for _ in range(2 + variant % 3)]
    for i, (sx, sy) in enumerate(spine):
        t = i / tall
        half = 4 * f + math.sin(min(t * 1.25, 1) * math.pi) ** 0.8 * W * 0.28 * (1 - 0.3 * t)
        for d in range(int(-half), int(half) + 1):
            x, y = sx + d, sy + abs(d) * 0.28
            off = d / max(half, 1)
            if any(off * bs > 0 and math.hypot((t - bt) * tall, (1 - abs(off)) * half) < br for bt, bs, br in bites):
                continue  # a bite out of its edge
            lit = -side * off
            xi, yi = min(max(int(x), 0), W - 1), min(max(int(y), 0), H - 1)
            shade = 4.2 + lit * 1.2 + (0.8 if t > 0.75 else 0) + mottle[yi, xi] * 0.4
            if lit < -0.78:
                shade -= 1  # the far edge curls under
            if abs(d) > rib:
                run = (i - abs(d) * 1.7) % apart
                shade += 1 if run < 1 else -1 if run < 2 else 0  # a vein, and the fold beside it
                if f > 1:  # a lesser vein half way between two, and the leaf's own grain
                    lesser = (i - abs(d) * 1.7 + apart / 2) % apart
                    shade += 0.6 if lesser < 1 and abs(off) < 0.7 else 0
                    shade += grain[yi, xi] * 0.45
            elif lit > 0:
                shade += 0.5
            colour = tone(LEAF, round(shade))
            if abs(off) > (0.86 if f == 1 else 0.9) and t > 0.25:
                colour = ROSE[1 if abs(off) > 0.95 else 2]
            art.put(x, y, colour)
            for under in range(1, f + 1):
                art.put(x, y + under, colour)
    for i, (sx, sy) in enumerate(spine[: int(tall * 0.92)]):
        for d in range(-rib, rib):
            art.put(sx + d, sy, tone(LEAF, 6 if d < 0 else 2))
    for _ in range((3 + draft // 60) * f):  # dew, where the light finds it
        i = r.randint(int(tall * 0.2), int(tall * 0.8))
        sx, sy = spine[i]
        x, y = int(sx - side * r.uniform(6 * f, tall * 0.12)), int(sy + r.uniform(2, 6) * f)
        if art.has(x, y) and art.has(x + 1, y + 1):
            art.put(x, y, (255, 255, 255))
            art.put(x + 1, y, LEAF[6])
            art.put(x, y + 1, LEAF[6])
    for _ in range(f):
        art.outline(LEAF[0])
    return art.image()


def ivy(tint: str, variant: int) -> Image.Image:
    """Ivy hanging from an edge: strands of several lengths, a leaf to either side all the
    way down each, the nearer strands with bigger leaves, a blossom here and there."""
    ramp = TINTS[tint]
    r = rng("eden ivy", tint, variant)
    wide, tall = (3, 4, 2)[variant % 3] * T, (4, 6, 3)[variant % 3] * T
    art = Art(wide, tall)
    strands = [(3 + n * 5 + r.uniform(-1.5, 1.5), int(tall * r.uniform(0.35, 1.0)), n % 3 == 1) for n in range(wide // 5)]
    for x, length, near in sorted(strands, key=lambda s: s[2]):
        s = r.choice([-1, 1])
        for i in range(length):
            x += r.uniform(-0.4, 0.4)
            art.put(x, i, tone(ramp, 1 if not near else 2))
            if i % (4 if near else 3) == 1 and i < length - 1:
                s = -s
                if near:  # a leaf of five: lit over, dark under
                    for dx, dy, k in ((1, -1, 6), (1, 0, 5), (2, 0, 5), (3, 0, 4), (1, 1, 4), (2, 1, 3)):
                        art.put(x + s * dx, i + dy, tone(ramp, k))
                else:
                    for dx, dy, k in ((1, 0, 4), (2, 0, 3), (1, 1, 2)):
                        art.put(x + s * dx, i + dy, tone(ramp, k))
            if i > 6 and r.random() < 0.035:
                for dx, dy, k in ((0, 0, 2), (-1, 0, 1), (1, 0, 1), (0, -1, 2), (0, 1, 1)):
                    art.put(x + dx, i + dy, (ROSE if tint in ("green", "pale") else CYAN)[k])
        art.put(x, length, tone(ramp, 5))  # the strand's tip, new and pale
    return art.image()


def teal_tree(variant: int, sway: int = 0, fine: int = 1) -> Image.Image:
    """A tree of the orchard under the canopy: the tree of Paradise in leaves of deep teal."""
    return tree(variant + 20, TEAL, tall=(150, 176, 132)[variant % 3], sway=sway, fine=fine)


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
    """Wheat, standing in rows that are counted: a close stand of stalks, the ears heavy
    along its top, one a little over the next, a stalk or two that has got above the
    rest; a tally post at the row's end."""
    r = rng("eden wheat", variant)
    W, H = 6 * T, 2 * T + 6
    art = Art(W, H)
    last = W - (8 if variant < 2 else 2)
    swell = (variant - 2) * 3 if variant >= 2 else 0
    tops = {}
    for x in range(2, last):
        tops[x] = H - 24 - swell - (int(3 * math.sin(x * 0.2)) if variant >= 2 else 0) - round(1.5 * math.sin(x * 0.9 + variant))
        for y in range(tops[x] + 5, H):  # the stand: stalk by stalk, darker toward the ground
            deep = (y - tops[x]) / 24
            art.put(x, y, tone(WHEAT, (3 if x % 3 else 2) - (1 if deep > 0.8 else 0) + (1 if x % 3 == 1 and deep < 0.5 else 0)))
    x = 3
    while x < last - 2:  # the ears: three wide, grains down them, a dark seam between two
        top = tops[x] - r.randint(0, 3)
        for i in range(8):
            wide = 0 if i == 0 else 1
            for d in range(-wide, wide + 1):
                k = 6 if (d < 0 or i < 2) and (i + d) % 2 == 0 else 5 if d <= 0 else 4
                art.put(x + d, top + i, tone(WHEAT, k if i < 7 else 3))
        art.put(x, top - 1, tone(WHEAT, 6))
        art.put(x + (1 if x % 2 else -1), top - 2, tone(WHEAT, 5))
        x += 4 if r.random() < 0.8 else 3
    for _ in range(3):  # one that has got above the rest
        x = r.randint(6, last - 6)
        top = tops[x] - r.randint(6, 9)
        lean = r.choice([-1, 1])
        for y in range(top, tops[x] + 2):
            art.put(x + lean * ((tops[x] - y) // 4), y, tone(WHEAT, 4) if y > top + 5 else tone(WHEAT, 6 if (y - top) % 2 == 0 else 5))
    if variant >= 2:  # the middle of a row: no post
        return art.image()
    for y in range(H - 22, H):  # the post, and the tally cut in it
        art.put(W - 5, y, MARBLE[5])
        art.put(W - 4, y, MARBLE[3])
    for n in range(4 + variant):
        art.put(W - 5, H - 20 + n * 3, MARBLE[1])
    return art.image()


def sundial(fine: int = 1) -> Image.Image:
    """The Sundial of the Tallied Fields: a great disc of stone leaning on its plinth, worn
    and stained, rings and the lines of the hours cut in its face, the hours themselves
    set round its rim in gold, a blade of gold for a gnomon whose shadow lies across it
    and never moves. `fine` draws it that many times the draft's size: its rim a true
    round, the grain of its stone, finer moss."""
    r = rng("eden sundial")
    f = fine
    W0, H0 = 14 * T, 10 * T
    plinth = Art(W0, H0)
    cx, cy, rad = W0 / 2, H0 - 5.2 * T, 4.6 * T
    ashlar(plinth, cx - 5.4 * T, H0 - 0.6 * T, cx + 5.4 * T, H0, MARBLE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (5.4 * T)))
    ashlar(plinth, cx - 5 * T, H0 - 1.5 * T, cx + 5 * T, H0 - 0.6 * T, MARBLE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (5 * T)))
    for x in range(int(cx - 5.3 * T), int(cx + 5.3 * T)):
        plinth.put(x, H0 - 1.5 * T, MARBLE[6])
        plinth.put(x, H0 - 1.5 * T + 1, MARBLE[3])
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    art.a = scale2x_rgba(plinth.a) if f == 2 else plinth.a.copy()
    grain = noise("eden sundial grain", W, H, 2.6 * f)
    stain = noise("eden sundial stain", W, H, 17.0 * f)
    pores = noise("eden sundial pores", W, H, 1.8) if f > 1 else None
    for y in range(int((cy - rad) * f), int((cy + rad) * f)):
        for x in range(int((cx - rad) * f), int((cx + rad) * f)):
            dx, dy = (x + 0.5) / f - cx, ((y + 0.5) / f - cy) * 1.06
            d = math.hypot(dx, dy)
            if d > rad:
                continue
            ang = math.atan2(dy, dx)
            light = -(dx + dy) / (max(d, 1) * 1.414)
            hour = (ang * 12 / math.pi) % 1
            if d > rad - 4:  # the rim's roll
                k = 4 + round(light * 2.3)
            elif d > rad - 11:  # the band the hours are set in
                k = 4 + round(light * 0.8 + grain[y, x] * 0.25)
                if abs(hour - 0.5) > 0.42:
                    art.put(x, y, GOLD[5 if light > 0 else 4] if d < rad - 5 else GOLD[3])
                    continue
                if f > 1 and abs(hour - 0.5) < 0.012 and rad - 9.5 < d < rad - 6:
                    k -= 1  # the half hour, a fine cut between two of gold
            else:
                k = 5 + round(light * 0.9 * d / rad + grain[y, x] * 0.3) - (1 if stain[y, x] > 0.36 else 0)
                if f > 1 and pores[y, x] > 0.8:
                    k -= 1  # the stone's own pores
                for ring in (0.36, 0.64, 0.84):  # rings cut in its face: dark in the cut, lit on its far lip
                    cut = d - rad * ring
                    k += -2 if 0 <= cut < 1.2 else 1 if 1.2 <= cut < 2.2 else 0
                if rad * 0.36 < d < rad * 0.84 and abs(hour - 0.5) > 0.5 - 0.6 / max(d * math.pi / 12, 1):
                    k -= 1  # an hour's line
                along = dx * 0.62 + dy * 0.5  # under the gnomon's shadow, a wedge from the middle
                across = abs(-dx * 0.5 + dy * 0.62)
                if 0 < along < rad * 0.82 and across < 1.5 + along * 0.05:
                    k -= 2
            art.put(x, y, tone(MARBLE, k))
    for _ in range(26 * f):  # moss, low on it where the rain runs
        ang = r.uniform(0.15, math.pi - 0.15)
        d = rad - r.uniform(0.5, 7)
        x, y = int((cx + math.cos(ang) * d) * f), int((cy + math.sin(ang) * d / 1.06) * f)
        for ox, oy in ((0, 0), (1, 0), (0, 1), (-1, 0)):
            if art.has(x + ox, y + oy):
                art.put(x + ox, y + oy, tone(LEAF, 3 + (ox == 0 and oy == 0)))
    tip = (cx - rad * 0.08, cy - rad * 0.86)  # where the gnomon's point is
    steps = int(rad * 0.86 * f)
    for i in range(steps):  # the gnomon: a blade, broad at its foot, lit on its left
        t = i / steps
        gx, gy = (cx + (tip[0] - cx) * t) * f, (cy + (tip[1] - cy) * t) * f
        half = (3.2 * (1 - t) + 0.6) * f
        for d in range(int(-half - 1), int(half + 2)):
            if abs(d) <= half:
                art.put(gx + d, gy, GOLD[6 if d < -half * 0.3 else 5 if d < half * 0.4 else 3])
    for oy in range(-5 * f, 5 * f + 1):
        for ox in range(-5 * f, 5 * f + 1):
            if ox * ox + oy * oy <= 25 * f * f:
                art.put(cx * f + ox, cy * f + oy, GOLD[6] if ox + oy < -3 * f else GOLD[4] if ox + oy < 3 * f else GOLD[2])
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def wheel(fine: int = 1) -> Image.Image:
    """The Wheel mural of the Terraces, the biggest single image there: on a wall of marble
    in shadow a wheel of gold with twelve spokes, its rim a roll of metal studded all
    round, figures in a line walking round it all one way, a hub like an eye that looks
    back; a border of gold in a key pattern over and under. `fine` draws the wheel and
    its eye that many times the draft's size: a true round, spokes with an edge."""
    r = rng("eden wheel")
    f = fine
    W0, H0 = 44 * T, 30 * T
    wall = Art(W0, H0)
    ashlar(wall, 0, 0, W0, H0, SHADOWED, r, course=8, shift=lambda x: 0)
    key = ("#########...", "#.......#...", "#..####.#...", "#..#....#...", "#..######...", "#...........")
    for x in range(W0):
        for y in (1, 2, 11, 12):
            wall.put(x, y, GOLD[5 if y in (1, 11) else 3])
            wall.put(x, H0 - 1 - y, GOLD[5 if y in (2, 12) else 3])
        for row, line in enumerate(key):
            if line[x % 12] == "#":
                wall.put(x, 4 + row, GOLD[4])
                wall.put(x, H0 - 5 - row, GOLD[4])
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    art.a = scale2x_rgba(wall.a) if f == 2 else wall.a.copy()
    if f > 1:  # the wall's own grain: a stone a shade down here and there, a pixel at a time
        pores = noise("eden wheel wall", W, H, 2.0)
        worn = pores > 0.78
        art.a[worn, :3] = (art.a[worn, :3].astype(np.float32) * 0.9).astype(np.uint8)
    cx, cy, rad = W0 / 2, H0 / 2, 12.5 * T
    for y in range(int((cy - rad - 3) * f), int((cy + rad + 4) * f)):
        for x in range(int((cx - rad - 3) * f), int((cx + rad + 4) * f)):
            dx, dy = (x + 0.5) / f - cx, (y + 0.5) / f - cy
            d = math.hypot(dx, dy)
            if d <= 21:  # the eye: an iris of blue, rayed, a pupil, a light in it
                ang = math.atan2(dy, dx)
                if d > 9:
                    art.put(x, y, tone(CLOTH, 4 + round(math.sin(ang * 22) * 0.7 - (d - 9) / 12 * 1.6 + 1.2)))
                else:
                    art.put(x, y, (12, 16, 40) if d > 3.5 or dx + dy > -1 else GOLD[6])
                if math.hypot(dx + 5, dy + 6) < 2.2:
                    art.put(x, y, (255, 255, 255))
                continue
            k = gold_wheel(dx, dy, rad, 14, 30)
            if k is not None:
                art.put(x, y, tone(GOLD, k))
            elif rad < d < rad + 3 and dx + dy > 0:
                art.put(x, y, SHADOWED[1])  # the shadow the rim throws on the wall
    walkers = Art(W0, H0)
    for n in range(30):  # the gathered, walking the rim
        ang = n / 30 * 2 * math.pi
        walker(walkers, cx + math.cos(ang) * (rad + 15), cy + math.sin(ang) * (rad + 15) + 6, MARBLE, SHADOWED[1], step=n % 2)
    figures = scale2x_rgba(walkers.a) if f == 2 else walkers.a
    shown = figures[..., 3] > 0
    art.a[shown] = figures[shown]
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


GLYPHS = (
    ("#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."),
    ("..#..", ".#.#.", "#...#", "..#..", "..#..", "#...#", ".###."),
    ("#####", "..#..", "..#..", ".###.", "..#..", "..#..", "#####"),
    ("#.#.#", "#.#.#", "#.#.#", ".###.", "..#..", "..#..", "..#.."),
    (".###.", "#...#", "#.#.#", "#.#.#", "#...#", ".###.", "..#.."),
    ("#....", "##...", "#.#..", "#..#.", "#.#..", "##...", "#...."),
    ("..#..", "..#..", "#####", "..#..", ".#.#.", "#...#", "#...#"),
)


def low_gate(fine: int = 1) -> Image.Image:
    """The Low Gate: a vast ring of silver stone set in the cliff, thirty-six stones round,
    a sign cut in each, a roll of stone along its outer edge and its inner, a cold gem set
    at each quarter; rime on it and ice hanging from its crown; and in it the dark that is
    under Paradise: deep on deep of it, cloud lit from nowhere, a few cold stars. `fine`
    draws it that many times the draft's size: a true round, the grain of its stone, finer
    ice, more and smaller stars, and wisps in the dark."""
    r = rng("eden low gate")
    f = fine
    W = H = 32 * T * f
    art = Art(W, H)
    c = 16.0 * T  # its middle, on the draft
    rad, rim, count = 15 * T, 40, 36
    inner = rad - rim
    grain = noise("eden low gate grain", W, H, 2.8 * f)
    stain = noise("eden low gate stain", W, H, 24.0 * f)
    cloud = noise("eden low gate deep", W, H, 44.0 * f)
    wisp = noise("eden low gate wisp", W, H, 13.0 * f)
    pores = noise("eden low gate pores", W, H, 2.0) if f > 1 else None
    veil = noise("eden low gate veil", W, H, 5.0) if f > 1 else None
    shades = [r.choice([-1, 0, 0, 0, 1]) for _ in range(count)]

    def carved(n: int, arc: float, band: float) -> bool:
        gx, gy = int(math.floor(arc / 2 + 2.5)), int(math.floor((band - 20) / 2 + 3.5))
        return 0 <= gx < 5 and 0 <= gy < 7 and GLYPHS[(n * 5 + 2) % len(GLYPHS)][gy][gx] == "#"

    for y in range(H):
        for x in range(W):
            dx, dy = (x + 0.5) / f - c, (y + 0.5) / f - c
            d = math.hypot(dx, dy)
            if d > rad:
                continue
            if d > inner:
                light = -(dx + dy) / (d * 1.414)
                turn = (math.atan2(dy, dx) / (2 * math.pi) * count + 0.5) % count
                n, frac = int(turn) % count, turn % 1
                band = rad - d
                quarter = n % 9 == 0
                if band < 5:
                    k = 4 + round(light * 2.4) + (1 if band < 2 else 0)
                elif band > rim - 6:
                    k = 4 - round(light * 2.4) + (1 if band > rim - 2 else 0)
                else:
                    k = 4 + round(light * 1.3 + grain[y, x] * 0.7) + shades[n] + (1 if quarter else 0) - (1 if stain[y, x] > 0.32 else 0)
                    if f > 1 and pores[y, x] > 0.8:
                        k -= 1
                    arc = (frac - 0.5) * 2 * math.pi * d / count
                    if frac < 0.03 or frac > 0.97:
                        k -= 3  # the joint between two stones
                    elif frac < 0.075:
                        k += 1
                    elif 7 <= band < 8.2 or rim - 9.2 <= band < rim - 8:
                        k -= 2  # a line cut along it
                    elif 8.2 <= band < 9.4 or rim - 8 <= band < rim - 6.8:
                        k += 1
                    elif not quarter and carved(n, arc, band):
                        k -= 2
                    elif not quarter and carved(n, arc - 1.0, band - 1.0):
                        k += 1
                art.put(x, y, tone(SILVER, k))
                continue
            deep = d / inner
            v = cloud[y, x] + 0.45 * wisp[y, x] + (0.18 * veil[y, x] if f > 1 else 0)
            red, green, blue = 8 + 18 * deep, 10 + 28 * deep, 26 + 58 * deep
            if v > 0.25:
                red, green, blue = red + 12, green + 8, blue + 26
            if v > 0.55:
                red, green, blue = red + 12, green + 10, blue + 22
            for ring in (0.3, 0.52, 0.72, 0.88):  # deep on deep: the rings of a way down
                if abs(deep - ring) < 0.007:
                    red, green, blue = red + 8, green + 12, blue + 22
            glow = stepped_alpha((d - (inner - 18)) / 18.0, 3 * f)
            red, green, blue = red + 10 * glow, green + 34 * glow, blue + 54 * glow
            art.put(x, y, (min(int(red), 255), min(int(green), 255), min(int(blue), 255)))
    for _ in range(120 * f * f):
        ang, d = r.uniform(0, 6.28), r.uniform(0, inner - 10) * r.uniform(0.3, 1.0)
        x, y = (c + math.cos(ang) * d) * f, (c + math.sin(ang) * d) * f
        bright = r.random()
        art.put(x, y, (200, 220, 255) if bright < 0.7 else (255, 255, 255))
        if bright > 0.93:
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                art.put(x + ox, y + oy, (120, 150, 220))
    for q in range(4):  # a gem at each quarter, cold, in a setting
        gx, gy = (c + math.cos(q * math.pi / 2) * (rad - 20)) * f, (c + math.sin(q * math.pi / 2) * (rad - 20)) * f
        for oy in range(-8 * f, 8 * f + 1):
            for ox in range(-8 * f, 8 * f + 1):
                d = math.hypot(ox, oy) / f
                if d <= 7.5:
                    lit = (ox + oy) / f
                    colour = SILVER[1] if d > 6 else CYAN[1] if d > 4.4 or lit > 3 else CYAN[2] if d > 2 else CYAN[3]
                    if f > 1 and math.hypot(ox / f + 2.2, oy / f + 2.2) < 0.9:
                        colour = (255, 255, 255)  # the light in it
                    art.put(gx + ox, gy + oy, colour)
    for x in range(int((c - rad * 0.8) * f), int((c + rad * 0.8) * f)):  # rime along its crown
        out = (c - math.sqrt(rad * rad - ((x + 0.5) / f - c) ** 2)) * f
        thick = int((2 + 2.5 * (0.5 + 0.5 * math.sin(x * 0.31 / f)) + (x % 7 == 0)) * f + (x % 3 == 0) * (f - 1))
        for i in range(thick):
            art.put(x, out + i, SILVER[6] if i < thick - f else FROST[5])
    x = (c - inner * 0.82) * f
    while x < (c + inner * 0.82) * f:  # ice hanging in it
        top = (c - math.sqrt(inner * inner - (x / f - c) ** 2)) * f
        long = r.randint(4, 22) * f
        for i in range(long):
            wide = 1 + (f - 1) * (i < long * 0.5) + (f - 1) * (i < long * 0.2)
            for d in range(wide):
                art.put(x + d, top + i, FROST[6] if i < long * 0.6 and d == 0 else FROST[5])
        x += r.randint(3, 13) * (f if f == 1 else 1.4)
    for _ in range(f):
        art.outline(SILVER[0])
    return art.image()


def crag(variant: int) -> Image.Image:
    """A spire of the silver cliffs by the Low Gate: split rock, facet by facet, a shoulder
    where a piece has gone, lit cold on its left, snow on every shoulder."""
    r = rng("eden crag", variant)
    tall = (11, 15, 8)[variant % 3] * T
    W = 5 * T
    art = Art(W, tall)
    light, crack = facets(W, tall, ("eden crag", variant), 15.0, 2.6)
    rough = noise(("eden crag edge", variant), W, tall, 13.0)
    fine = noise(("eden crag grain", variant), W, tall, 2.2)
    peak = W / 2 + r.uniform(-10, 10)
    shoulders = sorted(r.uniform(0.22, 0.85) for _ in range(3))
    for y in range(tall):
        t = y / tall
        left = 2 + t**0.8 * (W / 2 - 17) + sum(5 for s in shoulders[::2] if t > s) + rough[y, 3] * 6 * t
        right = 2 + t**0.8 * (W / 2 - 17) + sum(5 for s in shoulders[1::2] if t > s) + rough[y, W - 4] * 6 * t
        cx = peak + (W / 2 - peak) * t
        for x in range(max(0, int(cx - left)), min(W, int(cx + right))):
            off = (x + 0.5 - cx) / (left if x < cx else right)
            k = 4 + round_shift(off) + round(light[y, x] * 1.5 + fine[y, x] * 0.35) - (1 if crack[y, x] and light[y, x] < 0.2 else 0)
            art.put(x, y, tone(SILVER, k))
    snowed(art, SILVER[6], FROST[5])
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


KNIGHT_SHOULDER = 5.0 * T  # how far over its block a keeper's shoulders are


def knight() -> Image.Image:
    """A statue of one who kept this place: hooded, the face in its hood's shadow, a mantle
    that falls in folds from the shoulders to the ground, both arms brought forward to a
    great sword set point down before him, on a block of stone with a step to it. Three
    times the hero's height, and worn: the rain has streaked it and moss has it by the foot."""
    r = rng("eden knight")
    W, H = 5 * T, 9 * T
    art = Art(W, H)
    cx = W / 2
    grain = noise("eden knight grain", W, H, 2.4)
    streak = noise("eden knight streak", W * 3, H, 9.0)
    top = int(H - 1.5 * T)
    ashlar(art, cx - 2.2 * T, H - 0.6 * T, cx + 2.2 * T, H, STONE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (2.2 * T)))
    ashlar(art, cx - 1.8 * T, top, cx + 1.8 * T, H - 0.6 * T, STONE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (1.8 * T)))
    shoulder = int(top - KNIGHT_SHOULDER)
    for y in range(shoulder, top):  # the mantle: broad at the shoulders, in at the waist, out to its hem
        t = (y - shoulder) / (top - shoulder)
        half = 17 - 3 * math.sin(min(t / 0.5, 1) * math.pi / 2) + (5 * ((t - 0.5) / 0.5) ** 1.3 if t > 0.5 else 0)
        half *= min(1.0, 0.55 + t * 9)  # the shoulders are round
        half += 1.2 * math.sin(t * 9 + 1) * t
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            fold = math.sin(off * (6 + 3 * t) + math.sin(t * 3.1) * 1.4) * min(1.0, 0.3 + t * 1.4)
            k = 4 + round_shift(off) + (1 if fold > 0.55 else -1 if fold < -0.45 else 0) + round(grain[y, x] * 0.5)
            if abs(off) < 0.2 and t > 0.3:
                k -= 1  # where the mantle parts, the robe under it
            if streak[y, x * 3] > 0.45:
                k -= 1
            art.put(x, y, tone(STONE, k))
    hands = (cx, shoulder + 34)
    for side in (-1, 1):  # the arms, brought forward to the sword
        sx, sy = cx + side * 13, shoulder + 7
        for i in range(31):
            t = i / 30
            x = sx + (hands[0] + side * 3 - sx) * t**1.6
            y = sy + (hands[1] - sy) * t
            for d in range(-3, 4):
                art.put(x + d, y, tone(STONE, (6 if side < 0 else 4) - (1 if d > 1 else 0) - (1 if d == 3 else 0)))
            art.put(x + 4 * (1 if side > 0 else -1), y, tone(STONE, 2))
    for dy in range(-13, 11):  # the hood: a cowl with a peak, the face in its shadow
        for dx in range(-11, 12):
            if (dx / 10.5) ** 2 + (dy / 12.5) ** 2 <= 1 or (dy < -8 and abs(dx + 1) < 5 + dy + 13):
                face = (dx / 5.2) ** 2 + ((dy - 1.5) / 7.2) ** 2 <= 1
                lip = (dx / 6.6) ** 2 + ((dy - 1.5) / 8.6) ** 2 <= 1
                k = 1 if face and dy < 5 else 2 if face else 6 if lip and dx < 0 else 3 if lip else 5 if dx + dy < -5 else 4 if dx < 4 else 3
                art.put(cx + dx, shoulder - 5 + dy, STONE[0] if face and dy < 2 else tone(STONE, k))
    for dx in range(-15, 16):  # the hood's fall on the shoulders
        for dy in range(0, 5):
            if (dx / 15.5) ** 2 + ((dy - 0.5) / 4.6) ** 2 <= 1:
                art.put(cx + dx, shoulder + 3 + dy, tone(STONE, 5 if dx < -4 else 4 if dx < 6 else 3) if dy < 4 else tone(STONE, 2))
    for y in range(hands[1] + 4, top - 1):  # the sword: its blade, a fuller down it
        half = 2.6 - 1.4 * (y - hands[1]) / (top - hands[1])
        for d in range(-3, 4):
            if abs(d) <= half:
                art.put(cx + d, y, tone(STONE, 2 if d == 0 else 6 if d < 0 else 4))
    for d in range(-13, 14):  # its guard, a knob at either end
        for dy in range(3):
            art.put(cx + d, hands[1] + 4 + dy, tone(STONE, 6 if dy == 0 else 4 if dy == 1 else 2))
    for end in (-13, 13):
        for dy in range(-1, 4):
            art.put(cx + end, hands[1] + 4 + dy, tone(STONE, 5))
            art.put(cx + end + (1 if end > 0 else -1), hands[1] + 5 + (dy > 1), tone(STONE, 3))
    for dy in range(-4, 5):  # the hands, one over the other, on its grip; the pommel over them
        for dx in range(-5, 6):
            if (dx / 5.2) ** 2 + (dy / 4.2) ** 2 <= 1:
                art.put(hands[0] + dx, hands[1] + dy, tone(STONE, 6 if dx + dy < -3 else 5 if dx < 2 else 3))
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            if dx * dx + dy * dy <= 8:
                art.put(cx + dx, hands[1] - 8 + dy, tone(STONE, 6 if dx + dy < -1 else 4))
    for x in range(int(cx - 2.2 * T), int(cx + 2.2 * T)):
        art.put(x, int(H - 0.6 * T), tone(STONE, 6))
    for x in range(int(cx - 2 * T), int(cx + 2 * T)):
        art.put(x, top, tone(STONE, 6))
        art.put(x, top + 1, tone(STONE, 2))
    for _ in range(30):  # moss, by the foot and in the folds
        x, y = int(cx + r.uniform(-2.1, 2.1) * T), int(r.uniform(top - 10, H - 1))
        if art.has(x, y):
            for dx in range(r.randint(2, 4)):
                if art.has(x + dx, y):
                    art.put(x + dx, y, tone(LEAF, 2 + (dx % 2)))
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
    """Far off in the Tallied Fields: fields to the horizon, plot beside plot, each
    ploughed or sown or standing its own way, hedges between them, poplars along the
    furthest ridge, ricks on the nearest, a line of the gathered small on the middle one,
    all walking one way. Its ends thin out into the air: it has no edge."""
    r = rng("eden far fields")
    W, H = 44 * T, 9 * T
    art = Art(W, H)
    haze = (250, 226, 172)
    ridges = ((H * 0.3, 8, 0.011, 0.74), (H * 0.5, 11, 0.017, 0.58), (H * 0.74, 12, 0.008, 0.4))
    crest = {}
    for n, (top, swell, f, amount) in enumerate(ridges):
        plots, x = [], -r.randint(0, 40)
        while x < W:  # the plots of this ridge: where each ends, what grows in it, which way its rows run
            wide = r.randint(46, 110)
            plots.append((x + wide, r.choice(["wheat", "wheat", "stubble", "plough", "green"]), r.choice([-1.6, -0.8, 0.8, 1.6]), r.randint(4, 7)))
            x += wide
        for x in range(W):
            y0 = top + math.sin(x * f + n * 2.1) * swell + math.sin(x * f * 2.7 + n) * swell * 0.3
            crest[(n, x)] = y0
            end, kind, slant, pitch = next(p for p in plots if x < p[0])
            for y in range(int(y0), H):
                down = y - y0
                edge = end - x - down * 0.5  # px to the hedge at the plot's end, which runs away down the slope
                stripe = int((x + down * slant) / pitch) % 2
                if 0 <= edge < 2:
                    colour = mix(LEAF[2], WHEAT[2], 0.3)
                elif kind == "wheat":
                    colour = WHEAT[5 - stripe]
                elif kind == "stubble":
                    colour = WHEAT[5] if (x * 3 + y * 5) % 11 else WHEAT[3]
                elif kind == "plough":
                    colour = mix(WHEAT[3 - stripe], (120, 84, 60), 0.5)
                else:
                    colour = mix(LEAF[4 - stripe], WHEAT[5], 0.35)
                if down < 1:
                    colour = WHEAT[6]
                art.put(x, y, mix(colour, haze, amount))
    for i in range(16):  # poplars, on the furthest
        x = int(W * 0.05 + i * W * 0.06 + r.uniform(-12, 12))
        if not 0 <= x < W:
            continue
        y, tall = crest[(0, x)], r.randint(9, 15)
        for up in range(tall):
            half = 0.6 + 2.2 * math.sin(min(1.0, (up + 1) / tall) * math.pi) ** 0.7
            for d in range(int(-half), int(half) + 1):
                art.put(x + d, y - up, mix(LEAF[3 if d < 0 else 2], haze, 0.62))
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
    for x in range(W):  # no edge: its ends thin out
        show = stepped_alpha(min(x, W - 1 - x) / 96.0, 32)
        art.a[:, x, 3] = (art.a[:, x, 3] * show).astype(np.uint8)
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
    """The Making-house, seen from afar: a vast pale house on an arcade, a dome and its
    lantern over it, wings and stacks, pilasters up its front, a roof-walk along each of
    its storeys; its door a white slit with a line of gold down it, the wheel over the
    door, its windows lit. Three sizes: it comes nearer the whole way along its approach."""
    s = (0.45, 0.75, 1.15)[size]
    amount = (0.66, 0.5, 0.34)[size]
    haze = (168, 160, 190)
    u = T * s  # a tile of the house, in pixels of the picture
    W, H = int(36 * u), int(26 * u)
    art = Art(W, H)
    cx = W / 2
    grain = noise(("eden far house", size), W, H, 3.0)
    for y in range(H):
        for x in range(W):
            side = (x + 0.5 - cx) / u
            ax, up = abs(side), (H - y - 0.5) / u
            tooth = int(ax * 3) % 2 == 0  # the roof-walk's merlons
            hall = ax < 11 and (up < 14 or (up < 14.4 and tooth))
            wings = ax < 16.5 and (up < 9 or (up < 9.4 and tooth))
            drum = ax < 5 and (up < 17 or (up < 17.3 and tooth))
            dome = math.hypot(ax, (up - 17) * 1.1) < 5 and up >= 17
            lantern = (ax < 0.8 and 21.3 < up < 23) or (ax < 1.1 and 22.8 < up < 23.2) or (ax < 0.25 and 23 <= up < 24.6)
            stack_top = 15 if side < 0 else 18
            stack = (abs(ax - 13.6) < 0.9 and up < stack_top) or (abs(ax - 13.6) < 1.25 and stack_top - 0.7 < up < stack_top)
            base = ax < 17.2 and up < 0.6
            if not (hall or wings or drum or dome or lantern or stack or base):
                continue
            k = 5 + (1 if side < -6 else 0) - (1 if side > 4 else 0) - (1 if side > 11.5 else 0) + round(grain[y, x] * 0.45)
            colour = PEARL[max(1, min(6, k))]
            cornice = any(abs(up - line) < 0.22 for line in (14, 9, 17)) and not stack
            door = ax < 3 and (up < 7 or math.hypot(ax, up - 7) < 3)
            jamb = not door and ax < 3.5 and (up < 7 or math.hypot(ax, up - 7) < 3.5)
            lx = (ax - 3.6) % 1.3 - 0.65
            high = 3.6 < ax < 10.2 and abs(lx) < 0.34 and 9.9 < up and (up < 11.6 or math.hypot(lx, up - 11.6) < 0.34)
            lx = (ax - 11.9) % 1.4 - 0.7
            low = 11.9 < ax < 16.1 and abs(lx) < 0.36 and 4.4 < up and (up < 6.2 or math.hypot(lx, up - 6.2) < 0.36) and abs(ax - 13.6) > 1.0
            lx = (ax - 3.9) % 2.1 - 1.05
            arch = 3.9 < ax < 16.4 and abs(lx) < 0.7 and up > 0.6 and (up < 2.3 or math.hypot(lx, up - 2.3) < 0.7)
            ring = abs(math.hypot(side, up - 12.2) - 1.5) < 0.3 or (math.hypot(side, up - 12.2) < 1.5 and abs(math.sin(math.atan2(up - 12.2, side) * 4)) < 0.18)
            pilaster = hall and 9.4 < up < 13.8 and ax > 3.4
            less = 0.0
            if door:
                colour = GOLD[5] if ax < 0.16 else PEARL[6] if (up * 1.1) % 1 > 0.12 else PEARL[5]
                less = 0.12
            elif lantern:
                colour, less = (GOLD[5], 0.2) if up > 22.8 else (PEARL[6 if side < 0 else 4], 0.0)
            elif ring:
                colour, less = GOLD[5], 0.2
            elif high or low:
                colour, less = AMBER[3 if (up * 2.2) % 1 > 0.2 else 2], 0.3
            elif arch:
                colour = PEARL[2 if lx > -0.2 else 1]
            elif jamb or cornice or base:
                colour = PEARL[6]
            elif stack:
                colour = BRASS[(4 if side < 0 else 3) + (1 if int(up * 1.5) % 4 == 0 or up > stack_top - 0.7 else 0)]
            elif dome:
                rib = abs(math.sin(side * 1.3)) < 0.12
                colour = PEARL[max(1, (6 if side < -1.6 else 5 if side < 2 else 4) - (1 if rib else 0) + round(grain[y, x] * 0.4))]
            elif pilaster and (ax * 0.55) % 1 < 0.1:
                colour = PEARL[6]
            elif pilaster and (ax * 0.55) % 1 > 0.9:
                colour = PEARL[max(1, k - 2)]
            elif (up * 2) % 2 < 0.14:
                colour = PEARL[max(1, k - 1)]  # a course of its stone
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
    """The Wheel, seen from below on the way up the Terraces: its gold rim and its spokes over everything, pale with distance."""
    W = H = 13 * T
    art = Art(W, H)
    cx = cy = W / 2
    rad = 6.2 * T
    haze = (246, 216, 184)
    for y in range(H):
        for x in range(W):
            k = gold_wheel(x + 0.5 - cx, y + 0.5 - cy, rad, 9, 13)
            if k is not None:
                art.put(x, y, mix(tone(GOLD, k), haze, 0.5))
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


# Close to the eye: dark, and passed in front of. Each zone's own: its deep tone, the tone
# of what is turned a little to the light, and the rim the light catches.
FRONTS = (
    ("leaf", (12, 26, 16), (22, 48, 26), (48, 92, 44)),  # the Hollow and the Vines: blades of a great leaf
    ("fruit", (8, 24, 28), (14, 44, 48), (30, 84, 82)),  # the orchard: a low branch, a fruit on it
    ("wheat", (40, 28, 8), (74, 54, 14), (128, 98, 30)),  # the fields: ears of wheat
    ("stone", (30, 24, 30), (58, 48, 54), (108, 90, 92)),  # the terraces: a balustrade, broken
    ("brass", (30, 20, 10), (60, 42, 18), (116, 84, 34)),  # the Making-house: a pipe and its valve
    ("crag", (12, 16, 30), (26, 34, 58), (56, 72, 108)),  # the Gate: split rock
)


def front(kind: int, sway: int = 0) -> Image.Image:
    """A dark shape close to the eye, passed in front of, of a zone's own kind: in three
    tones of the zone's own colour, the darkest for its mass, a rim where the light
    catches it from the upper left, so it frames the view, has a form, and never reads
    as something to stand on. What of it goes under its floor line thins out to nothing
    over two tiles: its foot is nowhere."""
    name, ink, mid, rim = FRONTS[kind]
    r = rng("eden front", name)
    W, H = 7 * T, 6 * T
    art = Art(W, H)
    floor = H - 2 * T
    detail = np.zeros((H, W), dtype=bool)  # what is drawn on its mass in the middle tone

    def mark(x, y):
        x, y = int(x), int(y)
        if 0 <= x < W and 0 <= y < H:
            detail[y, x] = True

    if name == "leaf":
        for n, (x0, tall, lean) in enumerate(((20, 56, 0.9), (48, 40, -0.7), (78, 62, 0.4), (96, 30, -1.0))):
            for h in range(-2 * T, tall):
                t = max(h, 0) / tall
                half = 2 + math.sin(min(t * 1.1, 1) * math.pi) * 7
                x = x0 + lean * t * t * 30 + sway * t * t
                for d in range(int(-half), int(half) + 1):
                    art.put(x + d, floor - h, ink)
                if 0 < h < tall - 4:
                    mark(x, floor - h)  # its rib
                    if h % 7 == 0:
                        for d in range(1, int(half)):
                            mark(x - d, floor - h - d // 2)
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
                            if (dx + 3) ** 2 / 30 + (dy + 3) ** 2 / 14 <= 1 and (dx + dy) % 3 == 0:
                                mark(i + dx, y - 8 + dy + (i % 3))
        for fx in (30, 74):
            fy = floor - 26 - math.sin(fx / (W - 8) * 2.6) * 18 + 9
            for dy in range(-3, 4):
                for dx in range(-3, 4):
                    if dx * dx + dy * dy <= 10:
                        art.put(fx + dx, fy + dy, mix(CYAN[1], ink, 0.3 if dx + dy < 0 else 0.55))
            art.put(fx - 1, fy - 1, CYAN[2])
        art.rect(0, floor - 30, 4, H, ink)
    elif name == "wheat":
        for _ in range(22):
            x, tall, lean = r.randint(3, W - 4), r.randint(26, 54), r.uniform(-0.4, 0.4)
            for h in range(-2 * T, tall):
                t = max(h, 0) / tall
                xx = x + lean * t * t * 12 + sway * t * t
                art.put(xx, floor - h, mid)
                if h > tall - 9:
                    art.put(xx - 1, floor - h, mid if h % 2 else ink)
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
                if 0 < up < 1:
                    mark(cx - half + 2, y)
        art.rect(0, floor - 3, W, H, ink)
        for dy in range(-6, 7):  # a block fallen from it
            for dx in range(-9, 10):
                art.put(W * 0.82 + dx + dy * 0.3, floor - 7 + dy, ink)
    elif name == "brass":
        for x in range(W):
            for d in range(-6, 7):
                art.put(x, floor - 22 + d, ink)
            mark(x, floor - 26)
            if x % 38 < 4:
                for d in range(-8, 9):
                    art.put(x, floor - 22 + d, ink)
        for y in range(int(floor - 22), H):
            for d in range(-5, 6):
                art.put(26 + d, y, ink)
            mark(23, y)
        for dy in range(-11, 12):  # the wheel of a valve
            for dx in range(-11, 12):
                d = math.hypot(dx, dy)
                if 8 < d <= 11 or (d <= 8 and (abs(dx) < 1.2 or abs(dy) < 1.2)):
                    art.put(78 + dx, floor - 40 + dy, ink)
        art.rect(77, floor - 30, 80, floor - 22, ink)
    else:
        light, crack = facets(W, H, "eden front crag", 14.0, 2.2)
        for n, (cx, tall, half0) in enumerate(((22, 58, 16), (62, 36, 13), (92, 48, 11))):
            peak = cx + (n - 1) * 5
            for h in range(-2 * T, tall):
                t = 1 - max(h, 0) / tall
                half = 1 + t**0.8 * half0
                middle = peak + (cx - peak) * t
                for x in range(int(middle - half), int(middle + half) + 1):
                    art.put(x, floor - h, ink)
                    if 0 <= x < W and 0 <= floor - h < H and light[floor - h, x] > 0.35 and x < middle:
                        mark(x, floor - h)
    solid = art.a[..., 3] > 0
    over = floor + 3
    lit = solid & (~shift(solid, 1, 0, False) | ~shift(solid, 0, 1, False)) & shift(solid, -1, 0, False)  # the rim the light catches
    near = solid & ~lit & (~shift(solid, 2, 0, False) | ~shift(solid, 0, 2, False) | ~shift(solid, 1, 1, False)) & shift(solid, -2, 0, False)
    plain = np.all(art.a[..., :3] == np.array(ink, dtype=np.uint8), axis=-1) & solid
    for mask, colour in ((detail & plain, mid), (near & plain, mid), (lit & plain, rim)):
        mask = mask.copy()
        mask[over:] = False
        art.a[mask, :3] = colour
    for y in range(floor, H):  # its foot is nowhere
        art.a[y, :, 3] = (art.a[y, :, 3] * stepped_alpha(1 - (y - floor) / (2.0 * T), 5)).astype(np.uint8)
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


def great_leaf(fine: int = 1) -> Image.Image:
    """The Rising Leaf: one leaf as tall as a tower, behind the stair the hero goes up; the stair is its rib."""
    return leaf(1, tall=560, fine=fine)


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
    top = int(h - 1.5 * T - KNIGHT_SHOULDER)
    for y in range(0, top + 6):  # the head, off at the neck, aslant
        for x in range(w):
            if y < top + 2 + (x - w / 2) * 0.25:
                a[y, x, 3] = 0
    for y in range(top + 6, h - 2 * T):  # the crack
        x = int(w / 2 + 8 + math.sin(y * 0.4) * 2 + (y - top) * 0.12)
        if a[y, x, 3]:
            a[y, x, :3] = STONE[0]
            if a[y, x + 1, 3]:
                a[y, x + 1, :3] = STONE[6]
    art = Art(w, h)
    art.a = a
    art.outline(STONE[0])
    return art.image()


def facets(w: int, h: int, seed, cell: float = 13.0, tall: float = 1.7):
    """Split rock: for every pixel how lit the facet it lies on is (-1 .. 1), and whether
    it lies in the crack between two facets. Facets are `cell` px across and `tall` times
    that high: rock splits along its height."""
    salt = _seed(seed) & 0xFFFF
    ys, xs = np.mgrid[0:h, 0:w]
    xs, ys = xs.astype(np.float64), ys.astype(np.float64)
    cw, ch = cell, cell * tall
    gx, gy = np.floor(xs / cw).astype(np.int64), np.floor(ys / ch).astype(np.int64)
    best, second = np.full((h, w), 1e9), np.full((h, w), 1e9)
    owner = np.zeros((h, w), dtype=np.int64)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            cx, cy = gx + ox, gy + oy
            n = ((cx * 73856093) ^ (cy * 19349663) ^ (salt * 83492791)) & 0x7FFFFFFF
            n = (n * 1103515245 + 12345) & 0x7FFFFFFF
            px = (cx + (n % 997) / 997.0) * cw
            py = (cy + ((n // 997) % 991) / 991.0) * ch
            d = np.hypot(xs - px, (ys - py) / tall)
            closer = d < best
            second = np.where(closer, best, np.minimum(second, d))
            owner = np.where(closer, n, owner)
            best = np.where(closer, d, best)
    return ((owner // 7) % 201) / 100.0 - 1.0, (second - best) < 1.1


def snowed(art: Art, white, pale, deep: int = 3):
    """Snow on what faces up: along the top of everything drawn, where it is not steep."""
    solid = art.a[..., 3] > 0
    top = solid & ~shift(solid, 0, 1, False)
    level = top & (shift(top, 1, 0, False) | shift(top, -1, 0, False))
    for d in range(deep):
        layer = shift(level, 0, d, False) & solid
        art.a[layer, :3] = white if d < deep - 1 else pale


def stepped_alpha(share: float, steps: int = 6) -> float:
    """A share 0 .. 1 in even steps: how an edge thins out without a chequer."""
    return max(0.0, min(1.0, math.ceil(max(0.0, min(1.0, share)) * steps) / steps))


def gold_wheel(dx: float, dy: float, rad: float, rim: float, hub: float, spokes: int = 12):
    """The gold of a great wheel at a place off its middle: which tone of GOLD, or None
    where the wall shows. A rim that is a roll of metal, lit where it faces the light;
    spokes that are broad at the hub and fine at the rim, each with a lit edge; a hub."""
    d = math.hypot(dx, dy)
    if d > rad:
        return None
    light = -(dx + dy) / (max(d, 1) * 1.414)
    if d > rad - rim:
        band = (rad - d) / rim
        if band < 0.3:
            return 4 + round(light * 2.2)
        if band > 0.72:
            return 4 - round(light * 2.2)
        stud = abs((math.atan2(dy, dx) / (2 * math.pi) * spokes * 4) % 1 - 0.5) < 0.1 and rim > 10
        return 6 if stud else 4 + round(light * 0.9)
    if abs(d - (rad - rim * 1.7)) < max(1.0, rim * 0.1):
        return 4 + round(light * 1.6)  # a fine ring within the rim
    if d <= hub:
        return 4 + round(light * 2.0) if d > hub * 0.78 else 3 + round(light * 1.2)
    ang = math.atan2(dy, dx)
    turn = (ang / (2 * math.pi) * spokes + 0.5) % 1 - 0.5
    off = turn * 2 * math.pi * d / spokes  # px to the side of the nearest spoke's line
    half = 0.9 + rim * 0.22 * (1 - d / rad) ** 0.8
    if abs(off) <= half:
        lit = (-math.sin(ang) + math.cos(ang)) * (1 if off > 0 else -1) < 0
        edge = abs(off) > half - 1.1
        return (6 if lit else 3) if edge and half > 1.4 else 5
    if d < hub * 1.7:  # a petal between two spokes, round the hub
        between = (abs(turn) - 0.5) * 2 * math.pi * d / spokes
        if abs(between) < (hub * 1.7 - d) * 0.5:
            return 5 if light > 0 else 4
    return None


def walker(art: Art, x: float, y: float, ramp, shadow=None, alpha: int = 255, step: int = 0):
    """One of the gathered, small: a head, a robe to the ground, lit on the left; `step`
    0 or 1 is which foot is forward. (x, y) is where it stands."""
    x, y = int(round(x)), int(round(y))
    if shadow is not None:
        for i in range(12):
            for d in range(-2, 3):
                art.put(x + d + 2, y - 11 + i + 1, shadow, alpha)
    for dx, dy, k in ((-1, -13, 6), (0, -13, 5), (1, -13, 4), (-1, -12, 6), (0, -12, 5), (1, -12, 4), (0, -11, 3)):
        art.put(x + dx, y + dy, tone(ramp, k), alpha)
    for i in range(10):
        half = 1 + (i + 2) // 4
        for d in range(-half, half + 1):
            k = 6 if d == -half else 3 if d == half else 5 if d < 0 else 4
            art.put(x + d, y - 10 + i, tone(ramp, k - (1 if i == 9 else 0)), alpha)
    art.put(x - 2 + 3 * step, y, tone(ramp, 3), alpha)
    art.put(x + (2 if step else -3), y - 6, tone(ramp, 5), alpha)  # a hand, out of the sleeve


# --- What holds things up: nothing in Paradise is a bare bar or a plank on the air ---------

WOOD = ramp_of("1a100a 33200f 4a2f18 634021 7d532c 976a3a b88a54")
LANTERN_BARK = ramp_of("0a1618 16282a 22403e 315852 467466 66947e 98bca0")
POSTS = ("stem", "trunk", "vine", "timber")  # the Hollow's, the orchard's, the Vines', the Fields'
POST_ROWS = 10  # tiles from a floor to the underside of the platform over it


def post(kind: int) -> Image.Image:
    """What holds a platform up in a garden, from the floor to its underside: the stalk
    of a great leaf with its joints and a leaf off it; a slender trunk that forks under
    its load; two stems twisted about each other; a post of timber with its braces and
    a binding of rope. Each spreads at its foot and at its head: it grew there, or was set."""
    name = POSTS[kind]
    r = rng("eden post", name)
    W, H = 3 * T, POST_ROWS * T
    art = Art(W, H)
    cx = W / 2
    if name == "stem":
        for y in range(H):
            t = y / H
            mid = cx + math.sin(y / 31.0) * 3
            half = 3.6 + 3 * t**4 + 5 * (1 - t) ** 8
            for x in range(int(mid - half), int(mid + half) + 1):
                off = (x + 0.5 - mid) / half
                joint = y % 36
                art.put(x, y, tone(LEAF, 5 + round_shift(off) + (1 if joint == 0 else -1 if joint == 1 else 0)))
        for n, y in enumerate(range(36, H - 30, 36)):  # a leaf off each joint, to one side and the other
            side = 1 if n % 2 else -1
            mid = cx + math.sin(y / 31.0) * 3
            for i in range(15):
                half = 1 + 3 * math.sin(i / 14 * math.pi)
                for d in range(int(-half), int(half) + 1):
                    art.put(mid + side * (4 + i), y - i * 0.7 + d, tone(LEAF, 5 if d < 0 else 3))
                art.put(mid + side * (4 + i), y - i * 0.7, tone(LEAF, 6))
        for side in (-1, 1):  # roots, and the arms it holds its load with
            for i in range(12):
                art.put(cx + side * (5 + i), H - 3 + (i * i) // 40, tone(LEAF, 3))
                art.put(cx + side * (5 + i), H - 2 + (i * i) // 40, tone(LEAF, 2))
                art.put(cx + side * (3 + i), 5 - (i * i) // 28, tone(LEAF, 5 if side < 0 else 3))
                art.put(cx + side * (3 + i), 6 - (i * i) // 28, tone(LEAF, 3))
        art.outline(LEAF[0])
    elif name == "trunk":
        fork = 4 * T
        trunk(art, int(cx), H - 1, H - fork, 9, 5, BIRCH, r, lean=0.3)
        for side in (-1, 1):
            branch(art, cx + 2 * (side > 0), fork + 4, cx + side * 17, 0, 4, BIRCH)
        for y in range(fork + 14, H - 8, 9):  # the marks a pale bark has
            if r.random() < 0.7:
                x = int(cx + r.uniform(-3, 2))
                for d in range(r.randint(2, 4)):
                    if art.has(x + d, y):
                        art.put(x + d, y, BIRCH[1])
        for bx, by, rx, ry in ((cx - 14, 9, 9, 7), (cx + 15, 8, 8, 6), (cx + 1, fork - 2, 7, 5)):
            bundle(art, bx, by, rx, ry, TEAL, r, ("post", bx), gaps=False)
        for dy in range(-2, 3):  # one fruit under the fork, lit
            for dx in range(-2, 3):
                if dx * dx + dy * dy <= 5:
                    art.put(cx + 9 + dx, fork + 9 + dy, CYAN[3] if dx * dx + dy * dy < 2 else CYAN[2] if dx + dy < 2 else CYAN[1])
        art.put(cx + 9, fork + 5, TEAL[2])
        art.put(cx + 9, fork + 6, TEAL[2])
        art.outline(TEAL[0])
    elif name == "vine":
        for strand in range(2):
            phase = strand * math.pi
            for y in range(H):
                mid = cx + math.sin(y / 15.0 + phase) * 7
                depth = math.cos(y / 15.0 + phase)
                half = 4 + depth * 1.2 + 3 * (y / H) ** 5 + 3 * (1 - y / H) ** 7
                for x in range(int(mid - half), int(mid + half) + 1):
                    off = (x + 0.5 - mid) / half
                    k = 4 + round_shift(off) + (1 if depth > 0.3 else -1 if depth < -0.4 else 0) - (1 if (x * 2 + y // 2) % 9 == 0 else 0)
                    if depth > -0.2 or not art.has(x, y):
                        art.put(x, y, tone(BARK, k))
        for _ in range(5):
            y = r.randint(16, H - 20)
            bundle(art, cx + r.choice([-1, 1]) * r.uniform(8, 13), y, r.uniform(6, 9), r.uniform(5, 7), LEAF, r, ("post vine", y), gaps=False)
        art.outline(BARK[0])
    else:
        for y in range(8, H):
            for d in range(-5, 5):
                grain = (d * 5 + y // 5 + (y // 37) * 3) % 11 == 0
                art.put(cx + d, y, tone(WOOD, (5 if d < -2 else 4 if d < 2 else 3) - (1 if grain else 0)))
        for side in (-1, 1):  # the braces under its load
            for i in range(22):
                for d in range(-2, 2):
                    art.put(cx + side * i + d, 30 - i, tone(WOOD, 5 if d < 0 else 3))
        for x in range(int(cx - 22), int(cx + 22)):  # the head it carries on
            for y in range(4, 9):
                art.put(x, y, tone(WOOD, 6 if y == 4 else 4 if y < 8 else 2))
        for y in (H // 3, H // 3 + 1, H // 3 + 3, H // 3 + 4):  # a binding of rope
            for d in range(-6, 6):
                art.put(cx + d, y, tone(WHEAT, 5 if (d + y) % 3 else 3))
        for i in range(9):  # its end, hanging
            art.put(cx + 6, H // 3 + 4 + i, tone(WHEAT, 4))
        ashlar(art, cx - 9, H - 9, cx + 9, H, STONE, r, course=5)  # the stone it is set in
        art.outline(WOOD[0])
    return art.image()


def lantern_trunk() -> Image.Image:
    """A length of the Lantern Tree's trunk, a storey of it: six tiles through and never
    straight, bark in ridges that wander up it, a knot, a hollow with one of its fruits
    alight in it, the glow of its sap in a crack here and there. It repeats upward: the
    tree is as tall as its room."""
    r = rng("eden lantern trunk")
    W, H = 8 * T, 12 * T
    art = Art(W, H)
    cx = W / 2
    knots = [(cx - 22, H * 0.72, 7, 10), (cx + 14, H * 0.2, 5, 7)]
    hollow = (cx + 6, H * 0.46, 9, 15)
    for y in range(H):
        a = 2 * math.pi * y / H
        left = 3 * T + 5 * math.sin(a + 0.6) + 3 * math.sin(3 * a + 2.0)
        right = 3 * T + 5 * math.sin(a + 3.9) + 3 * math.sin(2 * a + 0.4)
        for x in range(int(cx - left), int(cx + right)):
            off = (x + 0.5 - cx) / (left if x < cx else right)
            wander = 5 * math.sin(a * 2 + x * 0.05) + 3 * math.sin(a * 5 + x * 0.11)
            ridge = math.sin((x + wander) * 0.42)
            k = 4 + round_shift(off) + (1 if ridge > 0.72 else -1 if ridge < -0.5 else 0)
            sap = ridge < -0.94 and math.sin(a * 3 + x * 0.3) > 0.86
            for kx, ky, rx, ry in knots:
                d = math.hypot((x - kx) / rx, (y - ky) / ry)
                if d < 1:
                    k = 4 + round_shift(off) + (-2 if 0.55 < d < 0.8 or d < 0.25 else 1 if d >= 0.8 else 0)
                    sap = False
            hx, hy, rx, ry = hollow
            d = math.hypot((x - hx) / rx, (y - hy) / ry)
            if d < 1:
                colour = LANTERN_BARK[0] if d < 0.82 else LANTERN_BARK[5 if (x - hx) + (y - hy) > 0 else 1]
                glow = math.hypot(x - hx, y - hy - 7)
                art.put(x, y, CYAN[3] if glow < 1.6 else CYAN[2] if glow < 3 else CYAN[1] if glow < 4.2 else mix(colour, CYAN[1], 0.25) if glow < 9 and d < 0.82 else colour)
                continue
            art.put(x, y, CYAN[1] if sap else tone(LANTERN_BARK, k))
    for _ in range(14):  # moss on the side the light is on
        x, y = int(cx - r.uniform(20, 44)), r.randint(6, H - 8)
        for dx in range(r.randint(2, 5)):
            if art.has(x + dx, y) and art.has(x + dx, y + 1):
                art.put(x + dx, y, tone(TEAL, 5))
                art.put(x + dx, y + 1, tone(TEAL, 3))
    art.outline(LANTERN_BARK[0])
    return art.image()


def lantern_root() -> Image.Image:
    """The Lantern Tree's foot: the trunk spreading into roots as thick as a man that go
    into the ground one by one."""
    W, H = 16 * T, 5 * T
    art = Art(W, H)
    cx = W / 2
    for y in range(H):
        t = y / (H - 1)
        half = 3 * T + 4 + t**2.3 * 4.6 * T
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            lane = off * 3 * T * (1 + t * 0.35)  # the bark's ridges fan out with the roots
            ridge = math.sin(lane * 0.42 + math.sin(y * 0.09) * 1.5)
            toe = abs(math.sin(off * 7.6))  # between two roots the ground shows, lower down
            if t > 0.62 and toe < (t - 0.62) * 2.2:
                continue
            k = 4 + round_shift(off * 0.9) + (1 if ridge > 0.72 else -1 if ridge < -0.5 else 0) - (1 if t > 0.62 and toe < (t - 0.62) * 2.2 + 0.2 else 0)
            art.put(x, y, tone(LANTERN_BARK, k))
    art.outline(LANTERN_BARK[0])
    art.a[0, :, 3] = np.where(art.a[1, :, 3] > 0, art.a[0, :, 3], 0)
    return art.image()


def crown_trunk() -> Image.Image:
    """A length of the Crown Vine, a storey of it: six stems as thick as a tower plaited
    about one another, the dark between them, leaves at the turns. It repeats upward."""
    r = rng("eden crown trunk")
    W, H = 16 * T, 12 * T
    art = Art(W, H)
    for y in range(H):  # the dark between the stems
        a = 2 * math.pi * y / H
        half = 44 + 9 * math.sin(2 * a + 1.0)
        for x in range(int(W / 2 - half), int(W / 2 + half)):
            art.put(x, y, BARK[1] if (x + y // 3) % 7 else BARK[0])
    for strand in range(6):
        phase = strand * math.pi / 3
        for y in range(H):
            a = 2 * math.pi * y / H
            mid = W / 2 + math.sin(a + phase) * 62 + math.sin(2 * a + phase * 2) * 12
            depth = math.cos(a + phase)
            half = 19 + depth * 5
            for x in range(int(mid - half), int(mid + half)):
                off = (x + 0.5 - mid) / half
                groove = (x * 2 + y // 2) % 12 == 0
                k = 4 + round_shift(off) + (1 if depth > 0.3 else -1 if depth < -0.4 else 0) - (1 if groove else 0)
                if depth > -0.2 or not art.has(x, y) or tuple(art.a[y, x, :3]) in (BARK[0], BARK[1]):
                    art.put(x, y, tone(BARK, k))
    for n in range(9):
        y = r.randint(22, H - 22)
        x = W / 2 + r.choice([-1, 1]) * r.uniform(40, 96)
        bundle(art, x, y, r.uniform(12, 18), r.uniform(9, 13), LEAF, r, ("crown trunk", n), gaps=False)
    art.outline(BARK[0])
    return art.image()


CORBELS = 2  # the kinds of what is under a ledge, for each make of ledge


def corbel(style: str, variant: int) -> Image.Image:
    """What a ledge sits on, hung under it: braces of timber to a post with a knot of root
    in the gardens; a bracket of marble, carved, on the Terraces; brackets of iron and a
    chain with its hook in the Making-house; a shard of rock and its ice by the Gate.
    Nothing a hero can touch: it is what makes a ledge a thing that was built or grew."""
    r = rng("eden corbel", style, variant)
    W, H = 2 * T, 2 * T
    art = Art(W, H)
    cx = W / 2
    if style == "plank":
        if variant == 0:
            for side in (-1, 1):
                for i in range(13):
                    for d in range(2):
                        art.put(cx + side * (13 - i) - (side > 0), i + d, tone(WOOD, 5 - d * 2 if side < 0 else 4 - d * 2))
            for y in range(0, 20):
                for d in range(-2, 2):
                    art.put(cx + d, y, tone(WOOD, 5 if d < 0 else 3))
            for d in range(-3, 3):
                art.put(cx + d, 11, tone(WHEAT, 5))
                art.put(cx + d, 12, tone(WHEAT, 3))
            for dy in range(-2, 3):
                for dx in range(-3, 3):
                    if dx * dx + dy * dy <= 7:
                        art.put(cx + dx, 21 + dy, tone(WOOD, 4 if dx + dy < 0 else 2))
        else:
            for x in range(4, W - 4):  # a clod of the earth it grew out of, and its roots
                deep = int(9 * math.sin((x - 4) / (W - 8) * math.pi) ** 0.8)
                for y in range(deep):
                    art.put(x, y, (70, 50, 32) if y < 2 else (58, 40, 26) if y < deep * 0.6 else (40, 28, 20))
            for _ in range(5):
                x, long = r.uniform(7, W - 7), r.randint(6, 16)
                y = 6.0
                for i in range(long):
                    x += r.uniform(-0.6, 0.6)
                    art.put(x, y + i, (120, 96, 64) if i < long - 3 else (150, 124, 86))
        art.outline(WOOD[0])
    elif style == "gilt":
        for row, (y0, half) in enumerate(((0, 14), (4, 11), (8, 7), (12, 4))):
            for y in range(y0, y0 + 4):
                for x in range(int(cx - half), int(cx + half)):
                    off = (x + 0.5 - cx) / half
                    art.put(x, y, GOLD[5] if row == 0 and y == 0 else tone(MARBLE, 5 + round_shift(off) - (2 if y == y0 + 3 else 0) + (1 if y == y0 else 0)))
        if variant:  # a leaf carved under it
            for i in range(9):
                half = 3 * math.sin((i + 1) / 10 * math.pi)
                for d in range(int(-half), int(half) + 1):
                    art.put(cx + d, 16 + i, tone(MARBLE, 5 if d < 0 else 3))
                art.put(cx, 16 + i, tone(MARBLE, 2))
        else:  # a scroll at either side of it
            for side in (-1, 1):
                for dy in range(-3, 4):
                    for dx in range(-3, 4):
                        d = math.hypot(dx, dy)
                        if d <= 3.4:
                            art.put(cx + side * 10 + dx, 6 + dy, tone(MARBLE, 2 if 1.2 < d < 2.2 else 5 if dx + dy < 0 else 4))
        art.outline(MARBLE[0])
    elif style == "grate":
        for side in (-1, 1):
            for i in range(11):
                art.put(cx + side * (12 - i), i, tone(STEEL, 4))
                art.put(cx + side * (12 - i), i + 1, tone(STEEL, 2))
            for y in range(0, 12):
                art.put(cx + side * 1.5 - 0.5, y, tone(STEEL, 5 if side < 0 else 3))
        for x in range(int(cx - 12), int(cx + 12), 4):
            art.put(x, 0, tone(STEEL, 6))  # rivets
        if variant == 0:
            for i in range(12, 28):  # a chain, a link at a time, and its hook
                link = (i - 12) % 6
                for d in ((-2, 1) if link in (1, 2, 3, 4) else (-1, 0)):
                    art.put(cx + d, i, tone(STEEL, 5 if d < 0 else 3))
            for dx, dy in ((0, 0), (1, 1), (2, 2), (2, 3), (1, 4), (0, 4), (-1, 3)):
                art.put(cx + dx, 27 + dy, tone(STEEL, 5))
        else:
            for y in range(12, 20):  # a pipe's elbow, and what drips from it
                for d in range(-3, 3):
                    art.put(cx + d, y, tone(BRASS, 5 if d < -1 else 4 if d < 1 else 2))
            for d in range(-4, 4):
                art.put(cx + d, 19, tone(BRASS, 6))
                art.put(cx + d, 20, tone(BRASS, 3))
            art.put(cx - 1, 23, CYAN[2])
            art.put(cx - 1, 24, CYAN[1])
        art.outline(STEEL[0])
    else:
        light, crack = facets(W, H, ("eden corbel", variant), 8.0, 1.6)
        for y in range(15):
            half = 13 * (1 - y / 15) ** (0.7 + 0.3 * variant) + 1
            for x in range(int(cx - half), int(cx + half)):
                off = (x + 0.5 - cx) / half
                art.put(x, y, tone(SILVER, 4 + round_shift(off) + round(light[y, x] * 1.2) - (1 if crack[y, x] else 0)))
        x = 5.0
        while x < W - 5:  # its ice
            top = 15 * (1 - (abs(x - cx) / 13) ** 1.4) if abs(x - cx) < 13 else 0
            long = r.randint(4, 13)
            for i in range(long):
                art.put(x, top + i, FROST[6] if i < long * 0.6 else FROST[5])
            x += r.randint(2, 5)
        art.outline(SILVER[0])
    return art.image()


# --- Bigger than a tile: what is set in a floor's own ground, and the dark under a roof ----

INLAYS = ("earth", "loam", "vinewood", "tilth", "marble", "pearl", "silver")  # the ground of zones 1 to 7
INLAY_KINDS = 3
ROOT = ((150, 124, 86), (120, 96, 64), (84, 64, 44))
TINTED_STONE = {"earth": (126, 92, 66), "tilth": (150, 120, 70), "loam": (40, 110, 110)}


def inlay(kind: str, variant: int) -> Image.Image:
    """Something set in the ground of a floor that is bigger than a tile, so a floor is
    not one tile said over: a boulder, roots, a seam of pebbles in the earth; the rings
    of its growth in the wood of the Vines; a frieze, a rosette, a tally cut in the
    marble; a plate, a pipe, a dial let into the pearl; a vein of ice in the rock."""
    r = rng("eden inlay", kind, variant)
    W, H = (4, 3, 5)[variant % 3] * T, 20
    art = Art(W, H)
    cx, cy = W / 2, H / 2
    if kind in TINTED_STONE:
        tint = TINTED_STONE[kind]
        stone = [mix(c, tint, 0.45) if tint else c for c in STONE]
        if variant == 0:  # a boulder the earth lies round
            light, crack = facets(W, H, ("eden inlay", kind), 9.0, 1.0)
            for y in range(H):
                for x in range(W):
                    d = math.hypot((x + 0.5 - cx) / (W * 0.42), (y + 0.5 - cy - 1) / (H * 0.4))
                    edge = 1 + 0.12 * math.sin(math.atan2(y - cy, x - cx) * 5 + 1)
                    if d < edge:
                        k = 4 - round((x - cx) / W * 2.2 + (y - cy) / H * 2.4) + round(light[y, x] * 0.8) - (1 if crack[y, x] else 0)
                        art.put(x, y, tone(stone, k))
            art.outline(stone[0])
        elif variant == 1:  # roots, through it
            for n in range(3):
                y = 5.0 + n * 6 + r.uniform(-1, 1)
                thick = 2.4 - n * 0.5
                for x in range(2, W - 2):
                    y += math.sin(x * 0.21 + n * 2) * 0.35 + r.uniform(-0.2, 0.2)
                    taper = min(1.0, (x - 1) / 9, (W - 2 - x) / 9)
                    for d in range(int(-thick * taper), int(thick * taper) + 1):
                        art.put(x, y + d, ROOT[0] if d < 0 else ROOT[1] if d == 0 else ROOT[2])
                    if x % 11 == 5 + n:
                        for i in range(1, 5):
                            art.put(x + i, y + (i if n % 2 else -i) * 0.8, ROOT[1])
        else:  # a seam of pebbles
            x = 3.0
            while x < W - 6:
                wide, y = r.randint(3, 7), cy + math.sin(x * 0.11) * 4 + r.uniform(-2, 2)
                for dy in range(-2, 3):
                    for dx in range(wide):
                        if (dx - wide / 2 + 0.5) ** 2 / (wide / 2) ** 2 + dy * dy / 5.5 <= 1:
                            art.put(x + dx, y + dy, tone(stone, 5 if dy < 0 and dx < wide - 1 else 4 if dy < 1 else 2))
                x += wide + r.randint(2, 6)
    elif kind == "vinewood":
        rings = 5 - variant
        for y in range(H):
            for x in range(W):
                d = math.hypot((x + 0.5 - cx) / (W * 0.4), (y + 0.5 - cy) / (H * 0.42))
                d *= 1 + 0.1 * math.sin(math.atan2(y - cy, x - cx) * 3 + variant)
                if d < 1:
                    ring = (d * rings) % 1
                    art.put(x, y, tone(BARK, 2 if ring < 0.22 else 5 if ring < 0.4 else 4 if d < 0.5 else 3))
    elif kind == "marble":
        if variant == 0:  # a frieze, in a key pattern
            key = ("#########...", "#.......#...", "#..####.#...", "#..#....#...", "#..######...", "#...........")
            for x in range(2, W - 2):
                for row, line in enumerate(key):
                    for scale in range(2):
                        y = 4 + row * 2 + scale
                        if line[(x // 2) % 12] == "#":
                            art.put(x, y, MARBLE[2])
                            art.put(x + 1, y + 1, MARBLE[6])
                for y in (2, H - 4):
                    art.put(x, y, MARBLE[2])
                    art.put(x, y + 1, MARBLE[6])
        elif variant == 1:  # a rosette
            for y in range(H):
                for x in range(W):
                    dx, dy = x + 0.5 - cx, y + 0.5 - cy
                    d, ang = math.hypot(dx, dy), math.atan2(dy, dx)
                    petal = 9 * (0.55 + 0.45 * abs(math.cos(ang * 4)))
                    if d < 2.5:
                        art.put(x, y, GOLD[5 if dx + dy < 0 else 3])
                    elif d < petal:
                        art.put(x, y, MARBLE[6 if dx + dy < -2 else 4 if abs(math.cos(ang * 4)) > 0.3 else 2])
                    elif d < petal + 1.2:
                        art.put(x, y, MARBLE[2])
        else:  # a tally, cut in rows: fives, and a few over
            for row in range(2):
                x = 5 + row * 3
                while x < W - 12:
                    for stroke in range(4):
                        for y in range(4 + row * 9, 10 + row * 9):
                            art.put(x + stroke * 2, y, MARBLE[2])
                            art.put(x + stroke * 2 + 1, y, MARBLE[6])
                    for i in range(8):
                        art.put(x - 1 + i, 9 + row * 9 - i * 0.7, MARBLE[1])
                    x += 13
    elif kind == "pearl":
        if variant == 0:  # a plate let into it, riveted
            for y in range(3, H - 3):
                for x in range(3, W - 3):
                    edge = y in (3, H - 4) or x in (3, W - 4)
                    art.put(x, y, BRASS[2] if edge else tone(PEARL, 4 + (1 if y < 6 else 0) - (1 if y > H - 7 else 0) + (1 if (x + y * 2) % 23 == 0 else 0)))
            for x in range(6, W - 5, 9):
                for y in (5, H - 6):
                    art.put(x, y, BRASS[6])
                    art.put(x + 1, y + 1, BRASS[2])
        elif variant == 1:  # a pipe, laid in it
            for x in range(W):
                for d in range(-4, 5):
                    flange = x % 21 in (9, 10, 11)
                    if abs(d) < 4 or flange:
                        art.put(x, cy + d, tone(BRASS, (5 if d < -1 else 4 if d < 2 else 2) + (1 if flange else 0)))
            art.outline(BRASS[0])
        else:  # a dial
            for y in range(H):
                for x in range(W):
                    dx, dy = x + 0.5 - cx, y + 0.5 - cy
                    d = math.hypot(dx, dy)
                    if d < 9.5:
                        art.put(x, y, BRASS[5 if dx + dy < 0 else 3] if d > 7.5 else PEARL[6] if abs(math.sin(math.atan2(dy, dx) * 6)) > 0.25 or d < 5 else PEARL[2])
            for i in range(7):
                art.put(cx + i * 0.6, cy - i * 0.8, (170, 40, 40))
            art.put(cx, cy, BRASS[1])
            art.outline(BRASS[0])
    else:  # a vein of ice in the rock
        for x in range(2, W - 2):
            y = cy + math.sin(x * 0.13 + variant * 2) * 5 + math.sin(x * 0.41) * 1.5
            thick = 1.2 + 1.8 * math.sin((x - 2) / (W - 4) * math.pi) + 0.6 * math.sin(x * 0.9)
            for d in range(int(-thick), int(thick) + 1):
                art.put(x, y + d, FROST[6] if d < 0 else FROST[5] if d == 0 else FROST[4])
            if x % 9 == 4:
                art.put(x, y - thick - 1, (255, 255, 255))
                for i in range(1, 5):  # a splinter off it
                    art.put(x + i, y + thick + i * (1 if x % 2 else -1) * 0.8, FROST[4])
    return art.image()


GLOOMS = (8, 4, 2, 1)  # tiles across


def gloom(wide: int) -> Image.Image:
    """The dark under a roof and under a floor in a roofed place: deepest against what
    it is under, gone in four tiles, in steps of see-through. Laid over the wall behind
    a room and what stands far off, under everything the hero is among."""
    W, H = wide * T, 4 * T
    art = Art(W, H)
    for y in range(H):
        alpha = int(stepped_alpha(1 - y / H, 7) * 112)
        for x in range(W):
            art.put(x, y, (6, 12, 22), alpha)
    return art.image()


# --- What bends as the hero goes through it (`life brush`) ---------------------------------

BRUSHES = ("grass", "fern", "frond", "wheat", "bloom", "frost")  # zones 1, 2, 3, 4, 5 and 7
ORDER_BRUSH = 26  # before the hero: he goes through it


def brush(kind: int) -> Image.Image:
    """Something that grows on a floor, as high as the hero's knee or his waist, that he
    goes through and that bends as he does: a tuft of long grass with a flower in it; a
    fern of the orchard, its tips alight; the broad fronds under the Vines; a stand of
    wheat that has seeded itself off its row; flowers on tall stalks along a terrace;
    grass with the frost on it by the Gate. It turns about its foot."""
    name = BRUSHES[kind]
    r = rng("eden brush", name)
    W, H = 2 * T + 8, T + 14
    art = Art(W, H)
    cx, foot = W / 2, H - 3
    ramp = {"grass": LEAF, "fern": TEAL, "frond": VINE, "wheat": WHEAT, "bloom": LEAF, "frost": FROST}[name]

    def blade(x0, tall, lean, wide=1.0, lit=5, dark=3):
        for i in range(int(tall)):
            t = i / tall
            x = x0 + lean * t * t * tall * 0.45
            half = wide * (1 - t) ** 0.7
            for d in range(int(-half), int(half) + 1):
                art.put(x + d, foot - i, tone(ramp, lit if d <= 0 else dark))
        return x0 + lean * tall * 0.45, foot - tall

    if name in ("grass", "frost"):
        for n in range(11):
            x0 = cx + (n - 5) * 2.2 + r.uniform(-1, 1)
            tip = blade(x0, r.uniform(9, 23), (n - 5) / 5 * r.uniform(0.6, 1.2), 1.4, 5 if n % 2 else 4, 3 if n % 2 else 2)
            if name == "frost" and n % 2:
                art.put(tip[0], tip[1], (255, 255, 255))
                art.put(tip[0], tip[1] + 2, FROST[6])
        if name == "grass":
            for fx, fy in ((cx - 6, foot - 19), (cx + 8, foot - 15)):
                for dx, dy, k in ((0, 0, 2), (-1, 0, 1), (1, 0, 1), (0, -1, 2), (0, 1, 1)):
                    art.put(fx + dx, fy + dy, ROSE[k])
    elif name in ("fern", "frond"):
        for n in range(5):  # fronds: a rib that arches over, leaflets down either side of it
            lean = (n - 2) / 2 * 1.3 + r.uniform(-0.15, 0.15)
            tall = r.uniform(15, 24) - abs(n - 2) * 2
            for i in range(int(tall)):
                t = i / tall
                x, y = cx + lean * t * tall * 0.5, foot - i * (1 - 0.35 * t * abs(lean))
                art.put(x, y, tone(ramp, 2))
                if i > 3 and i % (2 if name == "fern" else 3) == 0:
                    long = (2.5 if name == "fern" else 5.0) * math.sin(min(1.0, t * 1.2) * math.pi) + 1
                    for side in (-1, 1):
                        for d in range(1, int(long) + 1):
                            art.put(x + side * d, y + d * 0.5, tone(ramp, 5 if side < 0 else 4 if d < long - 1 else 3))
                            if name == "frond":
                                art.put(x + side * d, y + d * 0.5 + 1, tone(ramp, 3))
            if name == "fern":  # its tip, alight
                art.put(x, y, CYAN[2])
                art.put(x, y - 1, CYAN[3])
    elif name == "wheat":
        for n in range(9):
            x0 = cx + (n - 4) * 2.6 + r.uniform(-1, 1)
            tall = r.uniform(14, 22)
            lean = (n - 4) / 4 * 0.5 + r.uniform(-0.15, 0.15)
            for i in range(int(tall)):
                t = i / tall
                art.put(x0 + lean * t * t * tall * 0.4, foot - i, tone(WHEAT, 3 if n % 2 else 2))
            hx, hy = x0 + lean * tall * 0.4, foot - int(tall)
            for i in range(8):
                for d in (-1, 0, 1) if 0 < i < 7 else (0,):
                    art.put(hx + d, hy + i, tone(WHEAT, 6 if (i + d) % 2 == 0 and d <= 0 else 5 if d <= 0 else 4))
            art.put(hx + 1, hy - 2, tone(WHEAT, 5))
    else:  # flowers on tall stalks
        petals = ((154, 58, 102), (226, 122, 166), (255, 240, 246)), ((90, 58, 168), (168, 136, 240), (239, 230, 255)), (GOLD[2], GOLD[4], GOLD[6])
        for n in range(6):
            x0 = cx + (n - 2.5) * 5 + r.uniform(-1.5, 1.5)
            tall = r.uniform(11, 23)
            lean = (n - 2.5) / 2.5 * 0.5
            for i in range(int(tall)):
                t = i / tall
                art.put(x0 + lean * t * t * tall * 0.4, foot - i, tone(LEAF, 3))
                if i in (6, 13) and n % 2:
                    for d in range(1, 4):
                        art.put(x0 + lean * t * t * tall * 0.4 + (d if i == 6 else -d), foot - i - d // 2, tone(LEAF, 5 if d < 3 else 4))
            hx, hy = x0 + lean * tall * 0.4, foot - int(tall)
            dark, mid, lit = petals[n % 3]
            for dx, dy, colour in ((0, 0, GOLD[5]), (-1, 0, mid), (1, 0, dark), (0, -1, lit), (0, 1, dark), (-1, -1, lit), (1, -1, mid), (-2, 0, mid), (2, 0, dark), (0, -2, lit)):
                art.put(hx + dx, hy + dy, colour)
    art.outline(ramp[0] if name != "wheat" else WHEAT[0])
    return art.image()


# --- What there is, and where its floor line lies ------------------------------------------

LEAVES, IVIES, TEAL_TREES, VINES, CRAGS = 4, 3, 3, 3, 3
FRAMES = 4
GLOW, FRAME = "_glow", "_f"


def _plan():
    plan = []

    def add(key, paint, order, sink=0, lit=True):
        plan.append((key, paint, order, lit, sink))

    def hung(key, paint, order, lit=True):
        """A picture that hangs: its floor line is a tile under its top, so it is stood in the cell under what it hangs from."""
        plan.append((key, paint, order, lit, None))

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
    # What holds things up: a garden's posts, the two great trunks a storey at a time, what is under a ledge.
    for k in range(len(POSTS)):
        add(f"eden_post_{k}", lambda k=k: post(k), ORDER_STAND)
    add("eden_lantern_trunk", lantern_trunk, ORDER_TREE - 2)
    add("eden_lantern_root", lantern_root, ORDER_TREE - 1, sink=6)
    add("eden_crown_trunk", crown_trunk, ORDER_TREE - 2)
    for style in LEDGES:
        for v in range(CORBELS):
            hung(f"eden_corbel_{style}_{v}", lambda a=(style, v): corbel(*a), ORDER_HUNG)
    # Bigger than a tile: what is set in each zone's ground; and the dark under a roof.
    for kind in INLAYS:
        for v in range(INLAY_KINDS):
            add(f"eden_inlay_{kind}_{v}", lambda a=(kind, v): inlay(*a), ORDER_DECAL, sink=5)
    for wide in GLOOMS:
        hung(f"eden_gloom_{wide}", lambda wide=wide: gloom(wide), ORDER_GARDEN + 1, lit=False)
    # What bends as the hero goes through it (`life brush`).
    for k in range(len(BRUSHES)):
        add(f"eden_brush_{k}", lambda k=k: brush(k), ORDER_BRUSH, sink=3)
    return plan


PLAN = _plan()
# What is drawn at the size that ships itself (tv2_base.DENSITY), by key; the rest ships as
# its draft through Scale2x. A picture here is the draft's size times DENSITY exactly.
FINE = {
    "eden_great_leaf": lambda: great_leaf(DENSITY),
    "eden_low_gate": lambda: low_gate(DENSITY),
    "eden_wheel": lambda: wheel(DENSITY),
    "eden_sundial": lambda: sundial(DENSITY),
}
for _v in range(LEAVES):
    FINE[f"eden_leaf_{_v}"] = lambda v=_v: leaf(v, fine=DENSITY)
for _v in range(TEAL_TREES):
    for _frame in range(FRAMES):
        FINE[f"eden_teal_tree_{_v}" + (f"{FRAME}{_frame}" if _frame else "")] = lambda v=_v, s=_frame: teal_tree(v, s, fine=DENSITY)
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
