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
import tv2_style
from tv2_earth import Art, _seed, branch, bundle, noise, ramp_of, trunk
from tv2_heaven import (
    AMBER, BIRCH, BLOSSOM, CLOTH, GOLD, MARBLE, ORDER_GARDEN, ORDER_HUNG, ORDER_STAND, ORDER_TREE, ORDER_WALL, SHADOWED,
    T, VINE, WATER, ashlar, column, fine_stones, finial, host_wings, pillar, round_shift, stones, tone, tree,
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


def doubled(a: np.ndarray, fine: int) -> np.ndarray:
    """A part of a picture that is still drawn on the draft (a wall of dressed stone, a
    plinth), at the size the picture is being drawn at: itself at 1, through Scale2x at 2.
    No other size is a size the art ships at."""
    if fine == 1:
        return a.copy()
    if fine == 2:
        return scale2x_rgba(a)
    raise ValueError(f"a draft part can be drawn at 1 or 2 times its size, not {fine}")


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


def ivy(tint: str, variant: int, fine: int = 1) -> Image.Image:
    """Ivy hanging from an edge: strands of several lengths, a leaf to either side all the
    way down each, the nearer strands with bigger leaves, a blossom here and there. `fine`
    draws it that many times the draft's size: thinner stems, more of them, and each leaf
    a leaf, lit over and dark under, with a stalk."""
    ramp = TINTS[tint]
    r = rng("eden ivy", tint, variant)
    f = fine
    wide, tall = (3, 4, 2)[variant % 3] * T * f, (4, 6, 2 + 1)[variant % 3] * T * f
    art = Art(wide, tall)
    apart = 5 if f == 1 else 8
    count = wide // apart
    strands = [(3 * f + n * apart + r.uniform(-1.5, 1.5), int(tall * r.uniform(0.35, 1.0)) - 3 * f, n % 3 == 1) for n in range(count)]
    petal = ROSE if tint in ("green", "pale") else CYAN
    for x, length, near in sorted(strands, key=lambda s: s[2]):
        s = r.choice([-1, 1])
        every = (4 if near else 3) if f == 1 else (7 if near else 5)
        for i in range(max(length, 4)):
            x += r.uniform(-0.4, 0.4)
            art.put(x, i, tone(ramp, 1 if not near else 2))
            if i % every == 1 and i < length - 1:
                s = -s
                if f > 1 and near:  # a leaf of the nearer strand: a heart, its rib dark, its top edge lit
                    for dx, dy, k in (
                        (2, -2, 6), (3, -2, 6), (4, -2, 5),
                        (1, -1, 6), (2, -1, 5), (3, -1, 5), (4, -1, 5), (5, -1, 4),
                        (1, 0, 3), (2, 0, 3), (3, 0, 4), (4, 0, 4), (5, 0, 4), (6, 0, 3),
                        (2, 1, 4), (3, 1, 3), (4, 1, 3), (5, 1, 2),
                        (3, 2, 2), (4, 2, 2),
                    ):  # fmt: skip
                        art.put(x + s * dx, i + dy, tone(ramp, k))
                elif f > 1:
                    for dx, dy, k in ((1, -1, 5), (2, -1, 5), (3, -1, 4), (1, 0, 3), (2, 0, 4), (3, 0, 4), (4, 0, 3), (2, 1, 2), (3, 1, 2)):
                        art.put(x + s * dx, i + dy, tone(ramp, k))
                elif near:
                    for dx, dy, k in ((1, -1, 6), (1, 0, 5), (2, 0, 5), (3, 0, 4), (1, 1, 4), (2, 1, 3)):
                        art.put(x + s * dx, i + dy, tone(ramp, k))
                else:
                    for dx, dy, k in ((1, 0, 4), (2, 0, 3), (1, 1, 2)):
                        art.put(x + s * dx, i + dy, tone(ramp, k))
            if i > 6 * f and r.random() < 0.035 / f:
                for dx, dy, k in ((0, 0, 2), (-1, 0, 1), (1, 0, 1), (0, -1, 2), (0, 1, 1)) + (((-1, -1, 2), (1, -1, 2), (0, -2, 2)) if f > 1 else ()):
                    art.put(x + dx, i + dy, petal[k])
        art.put(x, max(length, 4), tone(ramp, 5))  # the strand's tip, new and pale
    return art.image()


def teal_tree(variant: int, sway: int = 0, fine: int = 1) -> Image.Image:
    """A tree of the orchard under the canopy: the tree of Paradise in leaves of deep teal."""
    return tree(variant + 20, TEAL, tall=(150, 176, 132)[variant % 3], sway=sway, fine=fine)


def vine_trunk(variant: int, fine: int = 1) -> Image.Image:
    """A trunk of the Great Vines: three stems as thick as a man twisted about one another,
    lit on the left, leaves at the turns, taller than two screens. `fine` draws it that
    many times the draft's size: a line of light down each stem, finer grooves, a ring
    where it has grown a length."""
    r = rng("eden vine", variant)
    f = fine
    tall0 = (22, 28, 18)[variant % 3] * T
    W0 = 6 * T
    W, tall = W0 * f, tall0 * f
    art = Art(W, tall)
    for strand in range(3):
        phase = strand * 2.1 + variant
        for y in range(tall):
            yd = y / f
            mid = (W0 / 2 + math.sin(yd / 23.0 + phase) * 22) * f
            depth = math.cos(yd / 23.0 + phase)  # behind or before the others
            half = (9 + depth * 2.5) * f
            ring = f > 1 and int(yd + strand * 9) % 31 == 0
            for x in range(int(mid - half), int(mid + half)):
                off = (x + 0.5 - mid) / half
                groove = (x * 2 + y // 2) % 9 == 0
                k = 4 + round_shift(off) + (1 if depth > 0.3 else -1 if depth < -0.4 else 0) - (1 if groove else 0)
                if f > 1 and -0.5 < off < -0.38 and not groove:
                    k += 1  # the line of light down it
                if ring:
                    k -= 1
                if depth > -0.2 or not art.has(x, y):
                    art.put(x, y, tone(BARK, k))
    for _ in range(tall0 // 26):
        y = r.randint(20, tall0 - 20)
        x = W0 / 2 + r.choice([-1, 1]) * r.uniform(14, 30)
        bundle(art, x * f, y * f, r.uniform(9, 14) * f, r.uniform(7, 10) * f, LEAF, r, ("vine", variant, y), gaps=False)
    for _ in range(f):
        art.outline(BARK[0])
    return art.image()


def wheat(variant: int, fine: int = 1) -> Image.Image:
    """Wheat, standing in rows that are counted: a close stand of stalks, the ears heavy
    along its top, one a little over the next, a stalk or two that has got above the
    rest; a tally post at the row's end. `fine` draws it that many times the draft's
    size: stalk by stalk, each ear with its grains to either side and its beard."""
    r = rng("eden wheat", variant)
    f = fine
    W0, H0 = 6 * T, 2 * T + 6
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    last = (W0 - (8 if variant < 2 else 2)) * f
    swell = (variant - 2) * 3 if variant >= 2 else 0

    def top_at(x: int) -> int:
        xd = x / f
        rise = 24 + swell + (3 * math.sin(xd * 0.2) if variant >= 2 else 0) + 1.5 * math.sin(xd * 0.9 + variant)
        return int(round((H0 - rise) * f))

    for x in range(2 * f, last):
        top = top_at(x)
        for y in range(top + 5 * f, H):  # the stand: stalk by stalk, darker toward the ground
            deep = (y - top) / (24 * f)
            art.put(x, y, tone(WHEAT, (3 if x % 3 else 2) - (1 if deep > 0.8 else 0) + (1 if x % 3 == 1 and deep < 0.5 else 0)))
    x = 3 * f
    while x < last - 2 * f:  # the ears: grains down them to either side, a dark seam between two
        top = top_at(x) - r.randint(0, 3 * f)
        if f == 1:
            for i in range(8):
                wide = 0 if i == 0 else 1
                for d in range(-wide, wide + 1):
                    k = 6 if (d < 0 or i < 2) and (i + d) % 2 == 0 else 5 if d <= 0 else 4
                    art.put(x + d, top + i, tone(WHEAT, k if i < 7 else 3))
            art.put(x, top - 1, tone(WHEAT, 6))
            art.put(x + (1 if x % 2 else -1), top - 2, tone(WHEAT, 5))
            x += 4 if r.random() < 0.8 else 3
            continue
        lean = r.choice([-1, 0, 0, 1])
        for i in range(14):
            wide = (0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 0)[i]
            cx = x + (lean * (13 - i)) // 7
            for d in range(-wide, wide + 1):
                grain = (i + abs(d)) % 3  # a grain, the cleft under it, the next
                k = (6 if d < 0 else 5) if grain == 0 else 5 if grain == 1 and d <= 0 else 4 if grain == 1 else 3
                art.put(cx + d, top + i, tone(WHEAT, k if i < 13 else 2))
            if i % 3 == 0 and 0 < i < 12:  # its beard, a hair to either side
                art.put(cx - wide - 1, top + i - 1, tone(WHEAT, 6))
                art.put(cx + wide + 1, top + i - 1, tone(WHEAT, 5))
        for i in range(1, 4):
            art.put(x + (lean * 13) // 7 + (i // 2) * (1 if x % 2 else -1), top - i, tone(WHEAT, 6))
        x += r.choice([6, 6, 7, 5])
    for _ in range(3 * f):  # one that has got above the rest
        x = r.randint(6 * f, last - 6 * f)
        base = top_at(x)
        top = base - r.randint(6, 9) * f
        lean = r.choice([-1, 1])
        for y in range(top, base + 2 * f):
            art.put(x + lean * ((base - y) // (4 * f)), y, tone(WHEAT, 4) if y > top + 5 * f else tone(WHEAT, 6 if (y - top) % 2 == 0 else 5))
            if f > 1 and y <= top + 5 * f and (y - top) % 2 == 0:
                art.put(x + lean * ((base - y) // (4 * f)) + 1, y, tone(WHEAT, 5))
    if variant >= 2:  # the middle of a row: no post
        return art.image()
    for y in range(H - 22 * f, H):  # the post, and the tally cut in it
        for d in range(f):
            art.put(W - 5 * f + d, y, MARBLE[5] if d == 0 or f == 1 else MARBLE[4])
            art.put(W - 4 * f + d, y, MARBLE[3])
    for n in range(4 + variant):
        for d in range(f):
            art.put(W - 5 * f + d, H - 20 * f + n * 3 * f, MARBLE[1])
            if f > 1:
                art.put(W - 5 * f + d, H - 20 * f + n * 3 * f + 1, MARBLE[6])
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
    art.a = doubled(plinth.a, f)
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
    back; a border of gold in a key pattern over and under. `fine` draws the wheel, its
    eye and the figures that many times the draft's size: a true round, spokes with an
    edge, each figure with a round head and a robe that falls in a curve."""
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
    art.a = doubled(wall.a, f)
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
    walkers = Art(W, H)
    for n in range(30):  # the gathered, walking the rim
        ang = n / 30 * 2 * math.pi
        walker(walkers, cx + math.cos(ang) * (rad + 15), cy + math.sin(ang) * (rad + 15) + 6, MARBLE, SHADOWED[1], step=n % 2, fine=f)
    shown = walkers.a[..., 3] > 0
    art.a[shown] = walkers.a[shown]
    return art.image()


def pipe(across: bool, long: int, fine: int = 1) -> Image.Image:
    """A pipe of the Making-house: brass, warm, banded where its lengths are joined, a line
    of light along its top; `long` tiles of it, lying or standing. `fine` draws it that
    many times the draft's size: a collar at each joint with its bolts, the light along
    it a narrow line, a dull band where it is in its own shade."""
    f = fine
    W, H = (long * T * f, T * f) if across else (T * f, long * T * f)
    art = Art(W, H)
    for A in range(long * T * f):
        a = A / f
        band = a % 40 < 4
        for D in range(2 * f, (T - 2) * f):
            d = (D + 0.5) / f - 0.5
            off = (d - T / 2) / (T / 2 - 2)
            k = 4 - round(off * 2.2) + (1 if band else 0)
            if abs(off) < (0.2 if f == 1 else 0.12):
                k += 1
            if f > 1 and -0.42 < off < -0.3:
                k += 1  # the light along it
            if f > 1 and band and a % 40 in (1, 2) and abs(off) < 0.75 and int(A) % 2 == 0 and D % 7 == 3:
                k = 6  # a bolt in the collar
            x, y = (A, D) if across else (D, A)
            art.put(x, y, tone(BRASS, k))
        if band:
            for d in (1, T - 2):
                for s in range(f):
                    x, y = (A, d * f + s) if across else (d * f + s, A)
                    art.put(x, y, tone(BRASS, 3))
    for _ in range(f):
        art.outline(BRASS[0])
    return art.image()


def gear(frame: int, fine: int = 1) -> Image.Image:
    """A wheel of the Making-house's machinery, turning: brass teeth, spokes, a hub. `fine`
    draws it that many times the draft's size: each tooth with a lit face and a dark one,
    a groove inside the rim, a bolt on the hub."""
    f = fine
    W = H = 5 * T * f
    art = Art(W, H)
    cx = cy = 5 * T / 2
    turn = frame * math.pi / 18
    for Y in range(H):
        for X in range(W):
            dx, dy = (X + 0.5) / f - cx, (Y + 0.5) / f - cy
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx) + turn
            tooth = math.sin(ang * 12) > 0.2
            lit = 1 if dx + dy < 0 else -1
            if d < (36 if tooth else 31) and d > 24:
                k = 4 + lit
                if f > 1 and d > 31 and tooth:
                    k += 1 if math.cos(ang * 12) > 0 else -1  # a tooth's two faces
                if f > 1 and 26.5 < d < 27.5:
                    k -= 1  # the groove inside the rim
                art.put(X, Y, tone(BRASS, k))
            elif d <= 24 and (abs(math.sin(ang * 3)) < 5.5 / max(d, 5) or d < 8):
                k = 5 + lit if d > 3 else 2
                if f > 1 and 1.2 < d < 2.2:
                    k = 6  # the bolt on the hub
                art.put(X, Y, tone(BRASS, k))
            elif 21 < d <= 24:
                art.put(X, Y, tone(BRASS, 3))
    for _ in range(f):
        art.outline(BRASS[0])
    return art.image()


def making_door(fine: int = 1) -> Image.Image:
    """The door of the Making-house: vast, white, shut, a line of gold down its middle; a
    frame of pearl with a lintel, pipes running into the wall either side, and over it the
    wheel. The gathered go in by it. `fine` draws it that many times the draft's size:
    the door's panels moulded, a sheen down the gold, the wheel's spokes and rim true."""
    r = rng("eden making door")
    f = fine
    W0, H0 = 16 * T, 20 * T
    wall = Art(W0, H0)
    cx = W0 / 2
    ashlar(wall, 0, 2 * T, W0, H0, PEARL, r, course=8, shift=lambda x: round_shift((x + 0.5 - cx) / (W0 / 2)))
    art = Art(W0 * f, H0 * f)
    art.a = doubled(wall.a, f)
    for Y in range(4 * T * f, H0 * f):
        y = (Y + 0.5) / f - 0.5
        for X in range(int(cx - 5 * T) * f, int(cx + 5 * T) * f):
            x = (X + 0.5) / f - 0.5
            up = 4 * T + 5 * T - y
            if up > 0 and math.hypot(x + 0.5 - cx, up) > 5 * T:
                continue
            off = (x + 0.5 - cx) / (5 * T)
            cell = Y // f % 28
            panel = (Y // f // 28) % 2 == 0 and abs(abs(off) - 0.5) < 0.32 and cell > 3
            k = 6 - (1 if panel else 0) - (1 if off > 0.6 else 0)
            if f > 1 and panel and (cell == 4 or abs(abs(off) - 0.5) > 0.3):
                k -= 1  # the panel's moulding, in shade at its head and its sides
            art.put(X, Y, tone(PEARL, k))
        for d in range(-f, f):
            art.put(cx * f + d, Y, GOLD[6 if f > 1 and d == -f else 5 if d < 0 else 3])
    for X in range(W0 * f):
        for Y in range(2 * T * f, (2 * T + 6) * f):
            art.put(X, Y, tone(PEARL, 6 if Y < 2 * T * f + f else 4 if f == 1 or Y < (2 * T + 5) * f else 3))
        for s in range(f):
            art.put(X, (2 * T + 6) * f + s, GOLD[4] if s == 0 else GOLD[3])
    c = cx * f, (2 * T - 2) * f
    for DY in range(-22 * f, 23 * f):
        for DX in range(-22 * f, 23 * f):
            dx, dy = DX / f, DY / f
            d = math.hypot(dx, dy)
            if 17 < d <= 22 or (d <= 17 and abs(math.sin(math.atan2(dy, dx) * 4)) < (0.2 if f == 1 else 0.16)) or d < 5:
                k = 5 if dx + dy < 0 else 3
                if f > 1 and (20.6 < d <= 22 or 3.6 < d < 5):
                    k += 1 if dx + dy < 0 else -1
                art.put(c[0] + DX, c[1] + DY, GOLD[k])
    for _ in range(f):
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


def crag(variant: int, fine: int = 1) -> Image.Image:
    """A spire of the silver cliffs by the Low Gate: split rock, facet by facet, a shoulder
    where a piece has gone, lit cold on its left, snow on every shoulder. `fine` draws it
    that many times the draft's size: the same facets, with the grain of the rock on
    each and snow that lies as snow does."""
    r = rng("eden crag", variant)
    f = fine
    tall0 = (11, 15, 8)[variant % 3] * T
    W0 = 5 * T
    W, tall = W0 * f, tall0 * f
    art = Art(W, tall)
    light, crack = facets(W, tall, ("eden crag", variant), 15.0 * f, 2.6)
    rough = noise(("eden crag edge", variant), W, tall, 13.0 * f)
    grain = noise(("eden crag grain", variant), W, tall, 2.2 if f == 1 else 3.4)
    peak = (W0 / 2 + r.uniform(-10, 10)) * f
    shoulders = sorted(r.uniform(0.22, 0.85) for _ in range(3))
    for y in range(tall):
        t = y / tall
        left = (2 + t**0.8 * (W0 / 2 - 17) + sum(5 for s in shoulders[::2] if t > s) + rough[y, 3 * f] * 6 * t) * f
        right = (2 + t**0.8 * (W0 / 2 - 17) + sum(5 for s in shoulders[1::2] if t > s) + rough[y, W - 4 * f] * 6 * t) * f
        cx = peak + (W / 2 - peak) * t
        for x in range(max(0, int(cx - left)), min(W, int(cx + right))):
            off = (x + 0.5 - cx) / (left if x < cx else right)
            k = 4 + round_shift(off) + round(light[y, x] * 1.5 + grain[y, x] * (0.35 if f == 1 else 0.42)) - (1 if crack[y, x] and light[y, x] < 0.2 else 0)
            art.put(x, y, tone(SILVER, k))
    snowed(art, SILVER[6], FROST[5], deep=3 * f)
    for _ in range(f):
        art.outline(SILVER[0])
    return art.image()


def figure(who: str, fine: int = 1) -> Image.Image:
    """One of the story's people, standing by the road: Mereth who tends the orchard, with
    a basket; Ithuriel the guard who sat down, his spear across his knees, wings folded.
    `fine` draws them that many times the draft's size: a face with its shade, the robe's
    folds, the feathers of the wings, the weave of the basket."""
    f = fine
    W0, H0 = 3 * T, 3 * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2
    sits = who == "ithuriel"
    top = H0 - (30 if sits else 40)
    if sits:
        for I in range(24 * f):  # folded wings behind, feather on feather
            i = I / f
            for side in (-1, 1):
                for D in range(5 * f):
                    d = D / f
                    k = 6 - int(d) // 2
                    if f > 1 and (I + D * 2) % 7 == 0:
                        k -= 1
                    art.put((cx + side * (8 + d + i * 0.15)) * f, (top + 4) * f + I, tone(MARBLE, k))
    for Y in range((top + 9) * f, (H0 - 1) * f):
        y = Y / f
        t = (y - top - 9) / (H0 - top - 10)
        half = 5 + t * (9 if sits else 5)
        for X in range(int((cx - half) * f), int((cx + half) * f)):
            x = X / f
            off = (x + 0.5 / f - cx) / half
            fold = math.sin(off * 7 + t * 3)
            k = 2 + (1 if off < -0.2 and fold > 0 else -1 if off > 0.5 else 0)
            if f > 1 and abs(fold) < 0.1 and t > 0.2:
                k = max(1, k - 1)  # the line a fold turns on
            art.put(X, Y, ROBE[k])
    skin, skin_shade = (236, 200, 170), (204, 160, 132)
    for DY in range(-5 * f, 5 * f):
        for DX in range(-4 * f, 5 * f):
            dx, dy = DX / f, DY / f
            if dx * dx + dy * dy <= 20:
                if dy > -2:
                    colour = skin_shade if f > 1 and dx > 2 else skin
                else:
                    colour = (GOLD[5] if f > 1 and dx < -1 else GOLD[4]) if not sits else MARBLE[6]
                art.put(cx * f + DX, (top + 5) * f + DY, colour)
    if f > 1:  # the eyes, cast down
        for dx in (-2, 1):
            art.put(cx * f + dx * f, (top + 5) * f + f, skin_shade)
            art.put(cx * f + dx * f + 1, (top + 5) * f + f, skin_shade)
    if sits:
        for X in range(int((cx - 20) * f), int((cx + 20) * f)):
            for s in range(f):
                art.put(X, (H0 - 13) * f + s, GOLD[6] if f > 1 and s == 0 else GOLD[5])
                art.put(X, (H0 - 12) * f + s, GOLD[2])
    else:
        for DY in range(6 * f):
            dy = DY / f
            for DX in range(-5 * f, 6 * f):
                dx = DX / f
                if abs(dx) <= 5 - int(dy) // 2:
                    k = 5 if dx < 0 else 3
                    if f > 1 and (DX + DY) % 4 == 0:
                        k -= 1  # the weave
                    art.put((cx + 9) * f + DX, (H0 - 14) * f + DY, tone(BARK, k))
        for dx, dy in ((7, -16), (10, -17), (12, -15)):
            for s in range(f):
                for t in range(f):
                    art.put((cx + dx) * f + s, (H0 + dy) * f + t, CYAN[3] if f > 1 and s == t == 0 else CYAN[2])
    for _ in range(f):
        art.outline(ROBE[0])
    return art.image()


def bough(mirrored: bool, leaves=None, fine: int = 1) -> Image.Image:
    """A bough of the Lantern Tree: a limb as thick as a trunk reaching out from it, lesser
    limbs up from that, leaves of teal in heaps along it. `fine` draws it that many times
    the draft's size: the bark's furrows along the limb, leaf by leaf in its heaps."""
    r = rng("eden bough")
    leaves = leaves or TEAL
    f = fine
    W0, H0 = 16 * T, 8 * T
    art = Art(W0 * f, H0 * f)
    for i in range((W0 - 10) * f):
        t = i / ((W0 - 10) * f)
        y = (H0 - 22 - math.sin(t * 2.4) * 34) * f
        half = (9 - t * 6) * f
        for d in range(int(-half), int(half) + 1):
            k = 5 - round(d / max(half, 1) * 2)
            if f > 1 and (i + d * 3) % 13 == 0 and abs(d) < half - 1:
                k -= 1  # a furrow of the bark, along it
            art.put(i, y + d, tone(BARK, k))
    for n in range(5):
        x = 40 + n * 42
        y = H0 - 22 - math.sin(x / (W0 - 10) * 2.4) * 34
        branch(art, x * f, y * f, (x + 14) * f, (y - 30) * f, 3 * f, BARK)
    for n in range(9):
        x = 26 + n * 26 + r.uniform(-6, 6)
        y = H0 - 40 - math.sin(x / (W0 - 10) * 2.4) * 34 + r.uniform(-18, 6)
        bundle(art, x * f, y * f, r.uniform(16, 24) * f, r.uniform(12, 17) * f, leaves, r, ("bough", n), gaps=False)
    for _ in range(f):
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


def gate_arch(kind: str, fine: int = 1) -> Image.Image:
    """The arch a doorway stands in, behind whoever goes through: two piers and a pointed
    arch of cut stone with a ring of tracery in its head, weathered, a line of old gold
    under its spring; or (`vine`) two trunks grown together overhead. Eight tiles wide,
    twelve high, open in the middle. `fine` draws it that many times the draft's size:
    the joints of the stone a pixel wide, the arch and its ring true rounds."""
    r = rng("eden gate arch", kind)
    f = fine
    W0, H0 = 8 * T, 12 * T
    art = Art(W0 * f, H0 * f)
    cx, spring = W0 / 2, 5 * T
    if kind == "vine":
        for side in (-1, 1):
            for I in range((H0 - 6) * f):
                i = I / f
                t = i / (H0 - 6)
                x = cx + side * (2.6 * T - (t**2.4) * 2.4 * T) + math.sin(i / 13 + side) * 3
                half = (8 - t * 4) * f
                for D in range(int(-half), int(half) + 1):
                    k = 4 - round(D / max(half, 1) * 2) + (1 if (I + D) % (9 * f) else 0)
                    art.put(x * f + D, H0 * f - 1 - I, tone(BARK, k))
        for n in range(11):
            a = math.pi * n / 10
            bx, by = cx - math.cos(a) * 2.2 * T, 2.2 * T - math.sin(a) * 1.4 * T + r.uniform(-4, 4)
            bundle(art, bx * f, by * f, r.uniform(10, 15) * f, r.uniform(8, 11) * f, LEAF, r, ("arch", n), gaps=False)
        for _ in range(f):
            art.outline(BARK[0])
        return art.image()
    piers = Art(W0, H0)
    for side in (-1, 1):
        px = cx + side * 3 * T
        ashlar(piers, px - T, spring, px + T, H0, STONE, r, course=8, shift=lambda x, px=px: round_shift((x + 0.5 - px) / T))
    art.a = doubled(piers.a, f)
    for side in (-1, 1):
        px = cx + side * 3 * T
        for X in range(int(px - T - 3) * f, int(px + T + 3) * f):  # a base, and a capital with old gold under it
            for y, k in ((H0 - 6, 6), (H0 - 5, 4), (H0 - 1, 2), (spring, 6), (spring + 1, 5), (spring + 2, 2)):
                for s in range(f):
                    art.put(X, y * f + s, tone(STONE, k + (1 if f > 1 and s == 0 and k in (4, 5) else 0)))
            for s in range(f):
                art.put(X, (spring + 3) * f + s, GOLD[4] if f > 1 and s == 0 else GOLD[3])
    for Y in range(4 * f, (spring + 1) * f):
        y = Y / f
        for X in range(4 * f, (W0 - 4) * f):
            x = X / f
            # A pointed arch: two arcs struck from the far springing points.
            outer = max(math.hypot(x + 0.5 / f - (cx - 2 * T), y - spring), math.hypot(x + 0.5 / f - (cx + 2 * T), y - spring))
            if 4 * T < outer <= 6 * T - 6:
                ring = outer > 6 * T - 12 or outer < 4 * T + 6
                art.put(X, Y, tone(STONE, (6 if ring else fine_stones(X, Y, f) - 1) + (1 if x < cx else -1)))
    for DY in range(-13 * f, 14 * f):  # the ring in its head
        for DX in range(-13 * f, 14 * f):
            dx, dy = DX / f, DY / f
            d = math.hypot(dx, dy)
            if 9 < d <= 13 or (d <= 9 and abs(math.sin(math.atan2(dy, dx) * 3)) < 0.18):
                art.put(cx * f + DX, 2.2 * T * f + DY, tone(STONE, 6 if dx + dy < 0 else 3))
    for _ in range(f):
        art.outline(STONE[0])
    return art.image()


def gothic_window(fine: int = 1) -> Image.Image:
    """A window of an old hall: two lancets and a round light over them in a pointed frame
    of stone, glass in lead with a light behind it, dim and warm. `fine` draws it that
    many times the draft's size: lead a pixel wide, a pane that is lit at its heart."""
    f = fine
    W0, H0 = 5 * T, 10 * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2
    for Y in range(H0 * f):
        y = Y / f
        for X in range(W0 * f):
            x = X / f
            px = x + 0.5 / f
            outer = max(math.hypot(px - (cx - 1.2 * T), y - 4 * T), math.hypot(px - (cx + 1.2 * T), y - 4 * T)) if y < 4 * T else 0
            inside = (outer < 3.6 * T) if y < 4 * T else abs(px - cx) < 2.3 * T
            if not inside:
                continue
            frame = (outer > 3.6 * T - 6) if y < 4 * T else abs(px - cx) > 2.3 * T - 6
            mullion = abs(px - cx) < 3 and y > 3 * T
            rose = math.hypot(px - cx, y - 2.6 * T)
            if frame or mullion or 13 < rose <= 17 or y > H0 - 5:
                art.put(X, Y, tone(STONE, 5 if x < cx else 3))
                continue
            if f == 1:
                lead = (X + Y) % 6 == 0 or (X - Y) % 6 == 0
                pane = ((X + Y) // 6 * 3 + (X - Y) // 6 * 5) % 7
                art.put(X, Y, STONE[0] if lead else AMBER[1 + pane % 3] if pane % 3 else GOLD[3])
                continue
            u, v = X + Y, X - Y
            lead = u % (6 * f) == 0 or v % (6 * f) == 0
            pane = (u // (6 * f) * 3 + v // (6 * f) * 5) % 7
            heart = 2 * f <= u % (6 * f) <= 4 * f and 2 * f <= v % (6 * f) <= 4 * f
            colour = AMBER[min(4, 1 + pane % 3 + (1 if heart else 0))] if pane % 3 else (GOLD[4] if heart else GOLD[3])
            art.put(X, Y, STONE[0] if lead else colour)
    for _ in range(f):
        art.outline(STONE[0])
    return art.image()


def buttress(mirrored: bool, fine: int = 1) -> Image.Image:
    """A flying buttress: a pier of stone with a pinnacle on it, and an arm of stone flung
    from it to the wall it holds, an arch under the arm. `fine` draws it that many times
    the draft's size: the arm's courses with joints a pixel wide."""
    r = rng("eden buttress")
    f = fine
    W0, H0 = 8 * T, 13 * T
    pier = Art(W0, H0)
    px = 1.5 * T
    ashlar(pier, px - T, 3 * T, px + T, H0, STONE, r, course=8, shift=lambda x: round_shift((x + 0.5 - px) / T))
    art = Art(W0 * f, H0 * f)
    art.a = doubled(pier.a, f)
    for I in range(3 * T * f):  # the pinnacle
        i = I / f
        half = max(1, (3 * T - i) / (3 * T) * T * 0.8) * f
        for X in range(int(px * f - half), int(px * f + half) + 1):
            art.put(X, 3 * T * f - 1 - I, tone(STONE, 5 if X < px * f else 3))
    for X in range(int(px * f), W0 * f):
        x = X / f
        t = (x - px) / (W0 - px)
        top = (4.2 * T + t * 3.6 * T) * f
        under = top + (14 + math.sin(t * math.pi) * 22) * f
        for Y in range(int(top), int(top) + 9 * f):
            art.put(X, Y, tone(STONE, 6 if Y < int(top) + f else fine_stones(X, Y, f) - 1))
        for Y in range(int(under), int(under) + 5 * f):
            art.put(X, Y, tone(STONE, 4 if Y < int(under) + f else 3))
    for X in range(int(px - T - 3) * f, int(px + T + 3) * f):
        for s in range(f):
            art.put(X, 3 * T * f + s, tone(STONE, 6))
            art.put(X, (3 * T + 1) * f + s, tone(STONE, 2))
            art.put(X, (H0 - 5) * f + s, tone(STONE, 6))
    for _ in range(f):
        art.outline(STONE[0])
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if mirrored else image


def ruin(variant: int, fine: int = 1) -> Image.Image:
    """What is left of a column: a stump on its base, broken off aslant, and a drum of it
    lying by; or its capital fallen and half in the ground. `fine` draws it that many
    times the draft's size: the flutes with a lit edge each, the break rough."""
    f = fine
    W0, H0 = 5 * T, 5 * T
    art = Art(W0 * f, H0 * f)
    rough = noise(("eden ruin", variant), W0 * f, H0 * f, 2.0) if f > 1 else None
    if variant % 2 == 0:
        cx, tall = 1.6 * T, (3.4, 2.2)[variant // 2 % 2] * T
        for Y in range(int((H0 - tall) * f), H0 * f):
            y = Y / f
            cut = (y - (H0 - tall)) * 0.9
            for X in range(int((cx - 11) * f), int((cx + 11) * f)):
                x = X / f
                edge = cut + 2 + (rough[Y, X] * 1.2 if f > 1 else 0)
                if y < H0 - tall + 14 and x - (cx - 11) > edge:
                    continue
                off = (x + 0.5 / f - cx) / 11
                flutes = (off + 1) * 5
                flute = int(flutes) % 2
                k = 5 + round_shift(off) - flute
                if f > 1 and flute == 0 and flutes % 1 < 0.2:
                    k += 1  # the lit edge of a flute
                art.put(X, Y, tone(STONE, k))
        for X in range(int((cx - 15) * f), int((cx + 15) * f)):
            for y, k in ((H0 - 6, 6), (H0 - 5, 5), (H0 - 4, 4), (H0 - 1, 2)):
                for s in range(f):
                    art.put(X, y * f + s, tone(STONE, k))
        for Y in range((H0 - 20) * f, (H0 - 2) * f):  # a drum fallen by it
            y = Y / f
            for X in range(int(3.1 * T * f), int(4.6 * T * f)):
                x = X / f
                if math.hypot((x - 3.85 * T) / 12, (y - (H0 - 11)) / 9) <= 1:
                    k = 5 if y < H0 - 13 else 3 if x > 3.9 * T else 4
                    if f > 1 and math.hypot((x - 3.85 * T) / 12, (y - (H0 - 11)) / 9) < 0.45 and y < H0 - 11:
                        k = 6
                    art.put(X, Y, tone(STONE, k))
    else:
        for Y in range((H0 - 22) * f, H0 * f):
            y = Y / f
            lean = (y - (H0 - 22)) * 0.25
            for X in range(int((T + lean) * f), int((3.8 * T + lean) * f)):
                x = X / f
                leaf = (int(x) + int(y)) % 7 < 2 and y < H0 - 8
                k = 6 if y < H0 - 22 + 1 else 3 if leaf else 5 if x < 2.2 * T else 4
                if f > 1 and leaf and (X + Y) % 2 == 0:
                    k = 4  # the curl of a leaf of its capital
                art.put(X, Y, tone(STONE, k))
        for X in range((T - 4) * f, 4 * T * f):
            for s in range(f):
                art.put(X, (H0 - 1) * f + s, tone(STONE, 2))
    for _ in range(f):
        art.outline(STONE[0])
    return art.image()


def sword(coiled: bool = False, fine: int = 1) -> Image.Image:
    """A sword left standing in the ground; or (`coiled`) the twisted sword a fire is lit
    round, where the lost rest. `fine` draws it that many times the draft's size: a
    fuller down the blade, its edge lit, a grip bound in cord."""
    f = fine
    W0, H0 = 2 * T, 4 * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2 * f
    metal = BRASS if coiled else STEEL
    tall = 40 if coiled else 46
    for I in range(tall * f):
        i = I / f
        Y = H0 * f - 1 - I
        wob = math.sin(i / 3.5) * 1.6 * f if coiled else 0
        half = (1 if coiled or i >= tall - 6 else 2) * f
        if f > 1 and not coiled and i >= tall - 6:
            half = round(2 * f * (tall - i) / 6)  # the point
        for D in range(-half, half + 1):
            k = 6 if D < 0 else 4 if D == 0 else 2
            if f > 1 and D == -half:
                k = 5  # its edge
            art.put(cx + round(wob) + D, Y, tone(metal, k))
    hilt = H0 * f - 1 - tall * f
    for D in range(-7 * f, 8 * f):
        for s in range(f):
            art.put(cx + D, hilt + s, tone(metal, (5 if D < 0 else 3) + (1 if f > 1 and s == 0 else 0)))
            art.put(cx + D, hilt + f + s, tone(metal, 2))
    grip = BARK if not coiled else BRASS
    for I in range(1, 9 * f):
        for D in range(-f, f):
            k = 5 if D < 0 else 3
            if f > 1 and I % 3 == 0:
                k -= 1  # the cord
            art.put(cx + D, hilt - I, tone(grip, k))
    for dx, dy in ((-1, -10), (0, -10), (1, -10), (-1, -9), (0, -11)):
        for s in range(f):
            for t in range(f):
                art.put(cx + dx * f + s, hilt + dy * f + t, tone(metal, 6 if f > 1 and s == t == 0 else 5))
    for _ in range(f):
        art.outline(metal[0])
    return art.image()


KNIGHT_SHOULDER = 5.0 * T  # how far over its block a keeper's shoulders are


def knight(fine: int = 1) -> Image.Image:
    """A statue of one who kept this place: hooded, the face in its hood's shadow, a mantle
    that falls in folds from the shoulders to the ground, both arms brought forward to a
    great sword set point down before him, on a block of stone with a step to it. Three
    times the hero's height, and worn: the rain has streaked it and moss has it by the foot.
    `fine` draws it that many times the draft's size: rounder, the folds finer, the pits
    the weather has made in it."""
    r = rng("eden knight")
    f = fine
    W0, H0 = 5 * T, 9 * T
    W, H = W0 * f, H0 * f
    block = Art(W0, H0)
    cx0 = W0 / 2
    top0 = int(H0 - 1.5 * T)
    ashlar(block, cx0 - 2.2 * T, H0 - 0.6 * T, cx0 + 2.2 * T, H0, STONE, r, shift=lambda x: round_shift((x + 0.5 - cx0) / (2.2 * T)))
    ashlar(block, cx0 - 1.8 * T, top0, cx0 + 1.8 * T, H0 - 0.6 * T, STONE, r, shift=lambda x: round_shift((x + 0.5 - cx0) / (1.8 * T)))
    for x in range(int(cx0 - 2.2 * T), int(cx0 + 2.2 * T)):
        block.put(x, int(H0 - 0.6 * T), tone(STONE, 6))
    for x in range(int(cx0 - 2 * T), int(cx0 + 2 * T)):
        block.put(x, top0, tone(STONE, 6))
        block.put(x, top0 + 1, tone(STONE, 2))
    art = Art(W, H)
    art.a = doubled(block.a, f)
    cx, top = cx0 * f, top0 * f
    grain = noise("eden knight grain", W, H, 2.4 * f)
    pits = noise("eden knight pits", W, H, 1.8) if f > 1 else None
    streak = noise("eden knight streak", W * 3, H, 9.0 * f)
    shoulder = int(top0 - KNIGHT_SHOULDER) * f
    for y in range(shoulder, top):  # the mantle: broad at the shoulders, in at the waist, out to its hem
        t = (y - shoulder) / (top - shoulder)
        half = 17 - 3 * math.sin(min(t / 0.5, 1) * math.pi / 2) + (5 * ((t - 0.5) / 0.5) ** 1.3 if t > 0.5 else 0)
        half *= min(1.0, 0.55 + t * 9)  # the shoulders are round
        half = (half + 1.2 * math.sin(t * 9 + 1) * t) * f
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            fold = math.sin(off * (6 + 3 * t) + math.sin(t * 3.1) * 1.4) * min(1.0, 0.3 + t * 1.4)
            k = 4 + round_shift(off) + (1 if fold > 0.55 else -1 if fold < -0.45 else 0) + round(grain[y, x] * 0.5)
            if f > 1 and abs(fold) < 0.08 and t > 0.25:
                k -= 1  # the line a fold turns on
            if abs(off) < 0.2 and t > 0.3:
                k -= 1  # where the mantle parts, the robe under it
            if streak[y, min(x * 3, W * 3 - 1)] > 0.45:
                k -= 1
            if f > 1 and pits[y, x] > 0.82:
                k -= 1
            art.put(x, y, tone(STONE, k))
    hands = (cx, shoulder + 34 * f)
    for side in (-1, 1):  # the arms, brought forward to the sword
        sx, sy = cx + side * 13 * f, shoulder + 7 * f
        for i in range(31 * f):
            t = i / (31 * f - 1)
            x = sx + (hands[0] + side * 3 * f - sx) * t**1.6
            y = sy + (hands[1] - sy) * t
            for d in range(-3 * f, 3 * f + 1):
                art.put(x + d, y, tone(STONE, (6 if side < 0 else 4) - (1 if d > f else 0) - (1 if d >= 3 * f - (f - 1) else 0)))
            for d in range(f):
                art.put(x + (4 * f + d) * (1 if side > 0 else -1), y, tone(STONE, 2))
    for dy in range(-13 * f, 10 * f + 1):  # the hood: a cowl with a peak, the face in its shadow
        for dx in range(-11 * f, 11 * f + 1):
            u, v = dx / f, dy / f
            if (u / 10.5) ** 2 + (v / 12.5) ** 2 <= 1 or (v < -8 and abs(u + 1) < 5 + v + 13):
                face = (u / 5.2) ** 2 + ((v - 1.5) / 7.2) ** 2 <= 1
                lip = (u / 6.6) ** 2 + ((v - 1.5) / 8.6) ** 2 <= 1
                k = 1 if face and v < 5 else 2 if face else 6 if lip and u < 0 else 3 if lip else 5 if u + v < -5 else 4 if u < 4 else 3
                art.put(cx + dx, shoulder - 5 * f + dy, STONE[0] if face and v < 2 else tone(STONE, k))
    for dx in range(-15 * f, 15 * f + 1):  # the hood's fall on the shoulders
        for dy in range(0, 5 * f):
            u, v = dx / f, dy / f
            if (u / 15.5) ** 2 + ((v - 0.5) / 4.6) ** 2 <= 1:
                art.put(cx + dx, shoulder + 3 * f + dy, tone(STONE, 5 if u < -4 else 4 if u < 6 else 3) if v < 4 else tone(STONE, 2))
    for y in range(hands[1] + 4 * f, top - f):  # the sword: its blade, a fuller down it
        half = (2.6 - 1.4 * (y - hands[1]) / (top - hands[1])) * f
        for d in range(-3 * f, 3 * f + 1):
            if abs(d) <= half:
                art.put(cx + d, y, tone(STONE, 2 if abs(d) < f / 2 + 0.5 else 6 if d < 0 else 4))
    for d in range(-13 * f, 13 * f + 1):  # its guard, a knob at either end
        for dy in range(3 * f):
            art.put(cx + d, hands[1] + 4 * f + dy, tone(STONE, 6 if dy < f else 4 if dy < 2 * f else 2))
    for end in (-13, 13):
        for dy in range(-f, 4 * f):
            for d in range(f):
                art.put(cx + end * f + d * (1 if end > 0 else -1), hands[1] + 4 * f + dy, tone(STONE, 5))
                art.put(cx + (end + (1 if end > 0 else -1)) * f + d * (1 if end > 0 else -1), hands[1] + 5 * f + min(max(0, dy - f), 2 * f - 1), tone(STONE, 3))
    for dy in range(-4 * f, 4 * f + 1):  # the hands, one over the other, on its grip; the pommel over them
        for dx in range(-5 * f, 5 * f + 1):
            if (dx / (5.2 * f)) ** 2 + (dy / (4.2 * f)) ** 2 <= 1:
                art.put(hands[0] + dx, hands[1] + dy, tone(STONE, 6 if dx + dy < -3 * f else 5 if dx < 2 * f else 3))
    if f > 1:  # the fingers of the upper hand
        for n in range(-3, 4, 2):
            for dy in range(-2 * f, 0):
                art.put(hands[0] + n * f, hands[1] + dy, tone(STONE, 3))
    for dy in range(-3 * f, 3 * f + 1):
        for dx in range(-3 * f, 3 * f + 1):
            if dx * dx + dy * dy <= 8 * f * f:
                art.put(cx + dx, hands[1] - 8 * f + dy, tone(STONE, 6 if dx + dy < -f else 4))
    for _ in range(30 * f):  # moss, by the foot and in the folds
        x, y = int(cx + r.uniform(-2.1, 2.1) * T * f), int(r.uniform(top - 10 * f, H - 1))
        if art.has(x, y):
            for dx in range(r.randint(2, 4)):
                if art.has(x + dx, y):
                    art.put(x + dx, y, tone(LEAF, 2 + (dx % 2)))
    for _ in range(f):
        art.outline(STONE[0])
    return art.image()


def fog(frame: int, fine: int = 1) -> Image.Image:
    """The fog that stands in a great door: a veil of pale mist from sill to lintel, threads
    of it drifting up, frame by frame. `fine` draws it that many times the draft's size:
    the same veil asked at every half pixel, so its threads are finer and its edge soft."""
    f = fine
    W0, H0 = 4 * T, 8 * T
    art = Art(W0 * f, H0 * f)
    for Y in range(H0 * f):
        y = Y / f
        for X in range(W0 * f):
            x = X / f
            off = abs(x + 0.5 / f - W0 / 2) / (W0 / 2)
            wave = math.sin(x * 0.55 + (y + frame * 5) * 0.21) + math.sin(x * 0.23 - (y + frame * 7) * 0.13 + 1.3)
            alpha = (1 - off**1.6) * (150 + 45 * wave) * min(1.0, (H0 - y) / 10 + 0.3) * min(1.0, y / 6 + 0.4)
            if alpha > 18:
                art.put(X, Y, FOG[3 if wave > 0.9 else 2 if wave > -0.2 else 1], int(min(alpha, 235)))
    return art.image()


def chain(variant: int, fine: int = 1) -> Image.Image:
    """A chain hung from a roof, a link at a time, long or short; the long one has a hook.
    `fine` draws it that many times the draft's size: each link a ring seen edge on and
    the next one face on, lit on its left, and a hook that curls."""
    f = fine
    W0, H0 = T, (5, 8)[variant % 2] * T
    art = Art(W0 * f, H0 * f)
    long = H0 - (6 if variant % 2 else 0)
    if f == 1:
        for i in range(long):
            link = i % 6
            for d in ((-2, 2) if link in (1, 2, 3, 4) else (-1, 0, 1)):
                art.put(8 + d, i, tone(STEEL, 5 if d < 0 else 3))
    else:
        cx = 8 * f
        for parity in (0, 1):  # the links face on, then the links edge on through them
            for n in range(parity, long // 6 + 1, 2):
                mid = n * 6 * f + 3 * f
                rx, ry = (2.8 * f, 4.6 * f) if parity == 0 else (1.4 * f, 4.4 * f)
                for DY in range(int(-ry) - 1, int(ry) + 2):
                    Y = mid + DY
                    if not 0 <= Y < long * f:
                        continue
                    for DX in range(int(-rx) - 1, int(rx) + 2):
                        u, v = (DX + 0.5) / rx, (DY + 0.5) / ry
                        d = u * u + v * v
                        if d > 1:
                            continue
                        if parity == 0 and d < 0.34:
                            continue  # its hole
                        if parity == 1 and abs(u) < 0.36 and abs(v) < 0.62:
                            art.put(cx + DX, Y, STEEL[1])  # its slot, in shadow
                            continue
                        k = 6 if u + v < -0.7 else 5 if u < -0.1 else 3 if u < 0.6 else 2
                        art.put(cx + DX, Y, tone(STEEL, k))
    if variant % 2:
        for dx, dy in ((0, 0), (1, 1), (2, 2), (2, 3), (1, 4), (0, 5), (-1, 5), (-2, 4)):
            for s in range(f):
                for t in range(f):
                    art.put((8 + dx) * f + s, (H0 - 6 + dy) * f + t, tone(STEEL, 6 if f > 1 and s == 0 and dx <= 0 else 5))
    if f > 1:
        for _ in range(f - 1):
            art.outline(STEEL[0])
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


def far_hollow(fine: int = 1) -> Image.Image:
    """Far off in the Waking Hollow: more of the great leaves and the trees in blossom,
    small, in the rose of the dawn. `fine` stands them there as they are drawn at that
    many times the draft's size."""
    r = rng("eden far hollow")
    f = fine
    W0, H0 = 36 * T, 12 * T
    canvas = Image.new("RGBA", (W0 * f, H0 * f))
    haze = (238, 206, 196)
    for depth, (scale, amount, n) in enumerate(((0.4, 0.74, 9), (0.6, 0.56, 6))):
        for i in range(n):
            x = (i + 0.5) / n * W0 + r.uniform(-22, 22)
            what = tree(r.randint(0, 3), BLOSSOM, fine=f) if (i + depth) % 3 == 0 else leaf(r.randint(0, 3), fine=f)
            stood(canvas, hazed(what, haze, amount, scale), x * f, (H0 + 3) * f)
    return canvas


def far_orchard(fine: int = 1) -> Image.Image:
    """Far off in the orchard: the rows going on, tree behind tree, dark under the canopy,
    their fruit small lights. `fine` stands the trees as they are drawn at that many
    times the draft's size, and gives each fruit a lit side and a dim one."""
    r = rng("eden far orchard")
    f = fine
    W0, H0 = 36 * T, 10 * T
    canvas = Image.new("RGBA", (W0 * f, H0 * f))
    haze = (8, 40, 46)
    for depth, (scale, amount, n) in enumerate(((0.45, 0.8, 13), (0.65, 0.62, 9))):
        for i in range(n):
            x = (i + 0.5) / n * W0  # exact rows: every one is counted
            stood(canvas, hazed(teal_tree(i % 3, fine=f), haze, amount, scale), x * f, (H0 + 2) * f)
            for _ in range(2):
                fx, fy = int(x + r.uniform(-16, 16) * scale * 2), int(H0 - (70 + r.uniform(0, 60)) * scale)
                X, Y = fx * f, fy * f
                if 0 <= fx < W0 and 0 <= fy < H0 and canvas.getpixel((X, Y))[3]:
                    if f == 1:
                        canvas.putpixel((fx, fy), CYAN[2] + (255,))
                        canvas.putpixel((min(fx + 1, W0 - 1), fy), mix(CYAN[1], haze, 0.3) + (255,))
                        continue
                    for dx, dy, colour in ((0, 0, CYAN[3]), (1, 0, CYAN[2]), (0, 1, CYAN[2]), (1, 1, mix(CYAN[1], haze, 0.3)), (2, 1, mix(CYAN[1], haze, 0.5))):
                        if X + dx < W0 * f and Y + dy < H0 * f:
                            canvas.putpixel((X + dx, Y + dy), colour + (255,))
    return canvas


def far_vines(fine: int = 1) -> Image.Image:
    """Far off among the Great Vines: more trunks, as great, going up out of sight, pale
    in the mist that lies at their feet. `fine` stands the trunks as they are drawn at
    that many times the draft's size."""
    r = rng("eden far vines")
    f = fine
    W0, H0 = 30 * T, 26 * T
    canvas = Image.new("RGBA", (W0 * f, H0 * f))
    haze = (172, 204, 184)
    for scale, amount, places in ((0.5, 0.82, (0.08, 0.36, 0.62, 0.9)), (0.85, 0.66, (0.22, 0.5, 0.8))):
        for n, at in enumerate(places):
            trunk_ = vine_trunk(n + (1 if scale < 0.6 else 0), f)
            if scale < 0.6:  # the furthest go up out of the picture: one trunk on another
                tall = Image.new("RGBA", (trunk_.width, trunk_.height * 2))
                tall.paste(trunk_, (0, 0))
                tall.paste(trunk_, (0, trunk_.height))
                trunk_ = tall
            far_ = np.array(hazed(trunk_, haze, amount, scale, foot=0.2))
            for y in range(min(72 * f, far_.shape[0])):  # its top thins into the air, in steps, as pixels do
                far_[y, :, 3] = (far_[y, :, 3] * (int(y / (12 * f)) + 0.5) / 6).astype(np.uint8)
            stood(canvas, Image.fromarray(far_, "RGBA"), (at * W0 + r.uniform(-8, 8)) * f, H0 * f)
    return canvas


def far_fields(fine: int = 1) -> Image.Image:
    """Far off in the Tallied Fields: fields to the horizon, plot beside plot, each
    ploughed or sown or standing its own way, hedges between them, poplars along the
    furthest ridge, ricks on the nearest, a line of the gathered small on the middle one,
    all walking one way. Its ends thin out into the air: it has no edge. `fine` draws it
    that many times the draft's size: finer rows, a stubble finer than its plot, a head
    on each of the gathered."""
    r = rng("eden far fields")
    f = fine
    W0, H0 = 44 * T, 9 * T
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    haze = (250, 226, 172)
    ridges = ((H0 * 0.3, 8, 0.011, 0.74), (H0 * 0.5, 11, 0.017, 0.58), (H0 * 0.74, 12, 0.008, 0.4))
    crest = {}
    for n, (top, swell, freq, amount) in enumerate(ridges):
        plots, x = [], -r.randint(0, 40)
        while x < W0:  # the plots of this ridge: where each ends, what grows in it, which way its rows run
            wide = r.randint(46, 110)
            plots.append((x + wide, r.choice(["wheat", "wheat", "stubble", "plough", "green"]), r.choice([-1.6, -0.8, 0.8, 1.6]), r.randint(4, 7)))
            x += wide
        for X in range(W):
            x = X / f
            y0 = top + math.sin(x * freq + n * 2.1) * swell + math.sin(x * freq * 2.7 + n) * swell * 0.3
            if X % f == 0:
                crest[(n, X // f)] = y0
            end, kind, slant, pitch = next(p for p in plots if x < p[0])
            for Y in range(int(y0 * f), H):
                y = Y / f
                down = y - y0
                edge = end - x - down * 0.5  # px to the hedge at the plot's end, which runs away down the slope
                stripe = int((x + down * slant) / pitch) % 2
                if 0 <= edge < (2 if f == 1 else 1.5):
                    colour = mix(LEAF[2], WHEAT[2], 0.3)
                elif kind == "wheat":
                    colour = WHEAT[5 - stripe]
                elif kind == "stubble":
                    colour = WHEAT[5] if (X * 3 + Y * 5) % (11 if f == 1 else 13) else WHEAT[3]
                elif kind == "plough":
                    colour = mix(WHEAT[3 - stripe], (120, 84, 60), 0.5)
                else:
                    colour = mix(LEAF[4 - stripe], WHEAT[5], 0.35)
                if down < 1 / f:
                    colour = WHEAT[6]
                art.put(X, Y, mix(colour, haze, amount))
    for i in range(16):  # poplars, on the furthest
        x = int(W0 * 0.05 + i * W0 * 0.06 + r.uniform(-12, 12))
        if not 0 <= x < W0:
            continue
        y, tall = crest[(0, x)], r.randint(9, 15)
        for UP in range(tall * f):
            up = UP / f
            half = (0.6 + 2.2 * math.sin(min(1.0, (up + 1) / tall) * math.pi) ** 0.7) * f
            for D in range(int(-half), int(half) + 1):
                art.put(x * f + D, y * f - UP, mix(LEAF[3 if D < 0 else 2], haze, 0.62))
    for i in range(34):  # the gathered, on the middle ridge
        x = int(W0 * 0.18 + i * 9)
        y = crest[(1, x)]
        for UP in range(f, 8 * f):
            up = UP / f
            for D in range(2 * f) if up < 6 else range(f // 2, f // 2 + f):
                art.put(x * f + D, y * f - UP, mix((214, 214, 232), haze, 0.45))
    for i in range(7):  # ricks, on the near one
        x = int(W0 * (0.06 + i * 0.14) + r.uniform(-10, 10))
        y = crest[(2, min(max(x, 0), W0 - 1))]
        for UP in range(9 * f):
            up = UP / f
            half = (6 - up * 0.6) * f
            for D in range(int(-half), int(half) + 1):
                art.put(x * f + D, y * f - UP, mix(WHEAT[5 if D < 0 else 3], haze, 0.36))
    for X in range(W):  # no edge: its ends thin out
        show = stepped_alpha(min(X, W - 1 - X) / (96.0 * f), 32)
        art.a[:, X, 3] = (art.a[:, X, 3] * show).astype(np.uint8)
    return art.image()


def far_terraces(fine: int = 1) -> Image.Image:
    """Far off on the Terraces: more terraces, arcade over arcade, going up and back, with
    clipped trees along them. `fine` draws it that many times the draft's size: true
    round arches, a cornice a pixel high."""
    r = rng("eden far terraces")
    f = fine
    W0, H0 = 40 * T, 12 * T
    art = Art(W0 * f, H0 * f)
    haze = (244, 214, 190)
    step = 3.6 * T
    for n in (2, 1, 0):
        amount = 0.46 + n * 0.16
        x0, x1 = int(n * 4.5 * T + (2 - n) * T), int(W0 - n * 3 * T - (2 - n) * 2 * T)
        top = int(H0 - (n + 1) * step)
        for X in range(x0 * f, x1 * f):
            bay = (X / f - x0) % 22
            for Y in range(top * f, H0 * f):
                down = Y / f - top
                arch = 5 <= bay <= 17 and down > 10 and (down > 17 or math.hypot(bay - 11, down - 17) <= 6.5)
                k = 6 if down < 1 / f else 2 if down < 2 / f else 1 if arch else 5 - (1 if bay < 3 else 0) - (1 if down > step - 6 else 0)
                art.put(X, Y, mix(MARBLE[k], haze, amount + (0.1 if arch else 0)))
        for i in range(int((x1 - x0) / 44)):  # clipped green along it
            cx = x0 + 22 + i * 44 + r.randint(-3, 3)
            for UP in range(f, 15 * f):
                up = UP / f
                half = (1 if up < 4 else 4.5 * math.sin((up - 3) / 12 * math.pi) ** 0.6) * (1 if f == 1 else f * 0.75)
                for D in range(int(-half), int(half) + 1):
                    art.put(cx * f + D, top * f - UP, mix(LEAF[3 if D < 0 else 2], haze, amount))
    return art.image()


def far_house(size: int, fine: int = 1) -> Image.Image:
    """The Making-house, seen from afar: a vast pale house on an arcade, a dome and its
    lantern over it, wings and stacks, pilasters up its front, a roof-walk along each of
    its storeys; its door a white slit with a line of gold down it, the wheel over the
    door, its windows lit. Three sizes: it comes nearer the whole way along its approach.
    `fine` draws it that many times the draft's size, each shape asked at every half pixel."""
    f = fine
    s = (0.45, 0.75, 1.15)[size]
    amount = (0.66, 0.5, 0.34)[size]
    haze = (168, 160, 190)
    W, H = int(36 * T * s) * f, int(26 * T * s) * f
    u = T * s * f  # a tile of the house, in pixels of the picture
    art = Art(W, H)
    cx = W / 2
    grain = noise(("eden far house", size), W, H, 3.0 * f)
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


def ridge(art: Art, peaks, haze, amount: float, seed: float = 0.0, fine: int = 1):
    """A ridge of the silver cliffs far off: spires of split rock, each (middle, height,
    half its foot), lit cold on the left, snow on what faces up. The peaks are in the
    draft's pixels; `art` is `fine` times the draft's size, and at more than one the
    cracks in the rock are a pixel wide."""
    f = fine
    w0, h0 = art.w // f, art.h // f

    def top_of(x):
        high = 0.0
        for cx, tall, half in peaks:
            off = abs(x - cx) / half
            if off < 1:
                high = max(high, tall * (1 - off) ** 1.5 + tall * 0.12 * (1 - off))
        ends = max(0.0, min(1.0, x / 48, (w0 - 1 - x) / 48))
        return h0 - (high + 6 + math.sin(x * 0.21 + seed) * 2.5 + math.sin(x * 0.07 + seed * 3) * 4) * ends

    for X in range(art.w):
        x = X / f
        top = top_of(x)
        slope = top_of(x + 2) - top_of(x - 2)  # over zero: it falls away to the right
        for Y in range(int(top * f), art.h):
            down = Y / f - top
            crack = (X * 3 + Y) % (19 * f) == 0 or (X * 2 - Y) % (31 * f) == 0
            k = 4 + (1 if slope < -1 else -1 if slope > 1 else 0) - (1 if crack else 0) - (1 if down > 70 else 0)
            snow = down < 2 + (int(x) % 5 == 0) * 2 + (2 if abs(slope) < 1.5 else 0)
            art.put(X, Y, mix(SILVER[6] if snow else tone(SILVER, k), haze, amount))


def far_gate(size: int, fine: int = 1) -> Image.Image:
    """The Low Gate, seen from afar: the ring in its cliff, cold, small; nearer the second
    time. `fine` stands the Gate as it is drawn at that many times the draft's size."""
    f = fine
    scale, amount = ((0.25, 0.62), (0.5, 0.46))[size]
    haze = (98, 118, 162)
    ring = hazed(low_gate(f), haze, amount, scale)
    wide = max(1, round(32 * T * scale))  # the ring's width on the draft
    W0, H0 = wide + 14 * T, wide + 7 * T
    art = Art(W0 * f, H0 * f)
    ridge(art, ((W0 * 0.5, H0 * 0.92, wide * 1.15), (W0 * 0.2, H0 * 0.6, wide * 0.5), (W0 * 0.82, H0 * 0.7, wide * 0.55), (W0 * 0.36, H0 * 0.8, wide * 0.4), (W0 * 0.66, H0 * 0.84, wide * 0.36)), haze, amount + 0.12, size, f)
    canvas = art.image()
    stood(canvas, ring, W0 * f / 2, (H0 - T) * f)
    return canvas


def far_wheel(fine: int = 1) -> Image.Image:
    """The Wheel, seen from below on the way up the Terraces: its gold rim and its spokes
    over everything, pale with distance. `fine` asks the wheel at every half pixel."""
    f = fine
    W = H = 13 * T * f
    art = Art(W, H)
    cx = cy = 13 * T / 2
    rad = 6.2 * T
    haze = (246, 216, 184)
    for Y in range(H):
        for X in range(W):
            k = gold_wheel((X + 0.5) / f - cx, (Y + 0.5) / f - cy, rad, 9, 13)
            if k is not None:
                art.put(X, Y, mix(tone(GOLD, k), haze, 0.5))
    return art.image()


def far_cliffs(fine: int = 1) -> Image.Image:
    """Far off by the Low Gate: more of the silver cliffs, spire behind spire, cold and blue."""
    r = rng("eden far cliffs")
    f = fine
    W0, H0 = 40 * T, 17 * T
    canvas = Image.new("RGBA", (W0 * f, H0 * f))
    haze = (112, 132, 176)
    for depth, (amount, n, high) in enumerate(((0.8, 9, 0.95), (0.6, 6, 0.62))):
        art = Art(W0 * f, H0 * f)
        peaks = [((i + 0.5) / n * W0 + r.uniform(-20, 20), H0 * high * r.uniform(0.55, 1.0), r.uniform(44, 86)) for i in range(n)]
        ridge(art, peaks, haze, amount, depth * 1.7, f)
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


def front(kind: int, sway: int = 0, fine: int = 1) -> Image.Image:
    """A dark shape close to the eye, passed in front of, of a zone's own kind: in three
    tones of the zone's own colour, the darkest for its mass, a rim where the light
    catches it from the upper left, so it frames the view, has a form, and never reads
    as something to stand on. What of it goes under its floor line thins out to nothing
    over two tiles: its foot is nowhere. `fine` draws it that many times the draft's
    size: the same shapes asked at every half pixel, its marks and stalks finer."""
    name, ink, mid, rim = FRONTS[kind]
    r = rng("eden front", name)
    f = fine
    W0, H0 = 7 * T, 6 * T
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    floor0 = H0 - 2 * T
    floor = floor0 * f
    detail = np.zeros((H, W), dtype=bool)  # what is drawn on its mass in the middle tone

    def mark(x, y):
        x, y = int(x), int(y)
        if 0 <= x < W and 0 <= y < H:
            detail[y, x] = True

    if name == "leaf":
        for n, (x0, tall, lean) in enumerate(((20, 56, 0.9), (48, 40, -0.7), (78, 62, 0.4), (96, 30, -1.0))):
            for HH in range(-2 * T * f, tall * f):
                h = HH / f
                t = max(h, 0) / tall
                half = (2 + math.sin(min(t * 1.1, 1) * math.pi) * 7) * f
                x = (x0 + lean * t * t * 30 + sway * t * t) * f
                for D in range(int(-half), int(half) + 1):
                    art.put(x + D, floor - HH, ink)
                if 0 < h < tall - 4:
                    mark(x, floor - HH)  # its rib
                    if HH % (7 * f) == 0:
                        for D in range(1, int(half)):
                            mark(x - D, floor - HH - D // 2)
    elif name == "fruit":
        for I in range((W0 - 8) * f):
            i = I / f
            t = i / (W0 - 8)
            y = (floor0 - 26 - math.sin(t * 2.6) * 18 + sway * t) * f
            for D in range(-2 * f, (3 - int(t * 2)) * f):
                art.put(I, y + D, ink)
            if I % (13 * f) == 6 * f:
                for DY in range(-7 * f, 8 * f):
                    for DX in range(-9 * f, 10 * f):
                        dx, dy = DX / f, DY / f
                        if (dx / 9) ** 2 + (dy / 7) ** 2 <= 1:
                            at = (I + DX, y - 8 * f + DY + (int(i) % 3) * f)
                            art.put(*at, ink)
                            if (dx + 3) ** 2 / 30 + (dy + 3) ** 2 / 14 <= 1 and (DX + DY) % (3 * f) == 0:
                                mark(*at)
        for fx in (30, 74):
            fy = floor0 - 26 - math.sin(fx / (W0 - 8) * 2.6) * 18 + 9
            for DY in range(-3 * f, 3 * f + 1):
                for DX in range(-3 * f, 3 * f + 1):
                    dx, dy = DX / f, DY / f
                    if dx * dx + dy * dy <= 10:
                        art.put(fx * f + DX, fy * f + DY, mix(CYAN[1], ink, 0.3 if dx + dy < 0 else 0.55))
            for s in range(f):
                for t in range(f):
                    art.put((fx - 1) * f + s, (fy - 1) * f + t, CYAN[2])
        art.rect(0, (floor0 - 30) * f, 4 * f, H, ink)
    elif name == "wheat":
        for _ in range(22):
            x, tall, lean = r.randint(3, W0 - 4), r.randint(26, 54), r.uniform(-0.4, 0.4)
            for HH in range(-2 * T * f, tall * f):
                h = HH / f
                t = max(h, 0) / tall
                xx = (x + lean * t * t * 12 + sway * t * t) * f
                for s in range(f):  # a stalk as wide as a draft pixel: it is close to the eye
                    art.put(xx + s, floor - HH, mid)
                if h > tall - 9:
                    for s in range(f):
                        art.put(xx - f + s, floor - HH, mid if (HH // f) % 2 else ink)
                        art.put(xx + f + s, floor - HH, ink)
    elif name == "stone":
        art.rect(0, (floor0 - 30) * f, W, (floor0 - 24) * f, ink)  # the rail, broken off
        for X in range(W):
            x = X / f
            if x > W0 * 0.62 + math.sin(x) * 2:
                for Y in range(int((floor0 - 30) * f), int((floor0 - 24) * f)):
                    art.a[Y, X, 3] = 0
        for n in range(5):
            cx = 9 + n * 21
            if cx > W0 * 0.6:
                break
            for Y in range(int((floor0 - 24) * f), H):
                up = (floor0 - Y / f) / 24
                half = (3 + (3.5 * math.sin(up * math.pi) if 0 < up < 1 else 3)) * f
                for D in range(int(-half), int(half) + 1):
                    art.put(cx * f + D, Y, ink)
                if 0 < up < 1:
                    mark(cx * f - half + 2 * f, Y)
        art.rect(0, (floor0 - 3) * f, W, H, ink)
        for DY in range(-6 * f, 7 * f):  # a block fallen from it
            for DX in range(-9 * f, 10 * f):
                art.put(W0 * 0.82 * f + DX + DY * 0.3, (floor0 - 7) * f + DY, ink)
    elif name == "brass":
        for X in range(W):
            for D in range(-6 * f, 7 * f):
                art.put(X, (floor0 - 22) * f + D, ink)
            mark(X, (floor0 - 26) * f)
            if (X // f) % 38 < 4:
                for D in range(-8 * f, 9 * f):
                    art.put(X, (floor0 - 22) * f + D, ink)
        for Y in range(int((floor0 - 22) * f), H):
            for D in range(-5 * f, 6 * f):
                art.put(26 * f + D, Y, ink)
            mark(23 * f, Y)
        for DY in range(-11 * f, 12 * f):  # the wheel of a valve
            for DX in range(-11 * f, 12 * f):
                dx, dy = DX / f, DY / f
                d = math.hypot(dx, dy)
                if 8 < d <= 11 or (d <= 8 and (abs(dx) < 1.2 or abs(dy) < 1.2)):
                    art.put(78 * f + DX, (floor0 - 40) * f + DY, ink)
        art.rect(77 * f, (floor0 - 30) * f, 80 * f, (floor0 - 22) * f, ink)
    else:
        light, crack = facets(W, H, "eden front crag", 14.0 * f, 2.2)
        for n, (cx, tall, half0) in enumerate(((22, 58, 16), (62, 36, 13), (92, 48, 11))):
            peak = cx + (n - 1) * 5
            for HH in range(-2 * T * f, tall * f):
                t = 1 - max(HH / f, 0) / tall
                half = (1 + t**0.8 * half0) * f
                middle = (peak + (cx - peak) * t) * f
                for X in range(int(middle - half), int(middle + half) + 1):
                    art.put(X, floor - HH, ink)
                    if 0 <= X < W and 0 <= floor - HH < H and light[floor - HH, X] > 0.35 and X < middle:
                        mark(X, floor - HH)
    solid = art.a[..., 3] > 0

    def away(dx, dy, n):
        """Where there is nothing within `n` steps of (dx, dy)."""
        out = np.zeros_like(solid)
        for k in range(1, n + 1):
            out |= ~shift(solid, dx * k, dy * k, False)
        return out

    over = floor + 3 * f
    lit = solid & (away(1, 0, f) | away(0, 1, f)) & shift(solid, -f, 0, False)  # the rim the light catches
    near = solid & ~lit & (away(2, 0, f) | away(0, 2, f) | away(1, 1, f)) & shift(solid, -2 * f, 0, False)
    plain = np.all(art.a[..., :3] == np.array(ink, dtype=np.uint8), axis=-1) & solid
    for mask, colour in ((detail & plain, mid), (near & plain, mid), (lit & plain, rim)):
        mask = mask.copy()
        mask[over:] = False
        art.a[mask, :3] = colour
    for Y in range(floor, H):  # its foot is nowhere
        art.a[Y, :, 3] = (art.a[Y, :, 3] * stepped_alpha(1 - (Y - floor) / (2.0 * T * f), 5)).astype(np.uint8)
    return art.image()


def crack(variant: int, fine: int = 1) -> Image.Image:
    """A crack in a floor of cut stone: down from its top, forked, a chip gone at the lip.
    `fine` draws it that many times the draft's size: as narrow as a crack is, wider at the
    lip."""
    r = rng("eden crack", variant)
    f = fine
    W0, H0 = 3 * T, 2 * T
    art = Art(W0 * f, H0 * f)
    dark = (30, 30, 44)
    x = W0 / 2 + r.uniform(-8, 8)
    for y in range(H0 - r.randint(2, 10)):
        step = r.choice((-1, 0, 0, 1))
        for s in range(f):  # down a draft pixel, a fine pixel at a time
            X = (x + step * (s + 1) / f) * f
            art.put(X, y * f + s, dark, 215 if y < 14 else 150)
            if f > 1 and y < 8:
                art.put(X + 1, y * f + s, dark, 170)  # wider at the lip
        x += step
        if y < 5:
            for s in range(f):
                art.put(x * f + f, y * f + s, dark, 190)
        if y in (7, 15) and r.random() < 0.8:
            fx = x
            for i in range(r.randint(5, 11)):
                fx += r.choice((-1, 1)) if i == 0 else (1 if fx > x else -1)
                for s in range(f):
                    art.put(fx * f + s * (1 if fx > x else -1), (y + i // 2) * f + s // 2, dark, 150)
    for dx in range(-3, 4):  # the chip at the lip
        for dy in range(0, 3 - abs(dx) // 2):
            at = W0 / 2 + r.uniform(-1, 1) + dx + (variant - 1) * 9
            for s in range(f):
                for t in range(f):
                    art.put(at * f + s, dy * f + t, dark, 200)
    return art.image()


def moss(tint: str, variant: int, fine: int = 1) -> Image.Image:
    """What has grown over a floor of cut stone: moss in the gardens' green, pale lichen in
    the Making-house, rime by the Gate. It lies along the top, deeper here and there, a
    strand of it down the face. `fine` draws it that many times the draft's size: a lumpy
    top, tufts a pixel wide, a darker edge where it ends on the stone."""
    ramp = TINTS[tint]
    r = rng("eden moss", tint, variant)
    f = fine
    W0, H0 = (4, 6, 3)[variant % 3] * T, T + 10
    art = Art(W0 * f, H0 * f)
    top = 4  # the floor's top row in the picture
    lump = noise(("eden moss", tint, variant), W0 * f, 1, 3.0) if f > 1 else None
    for x in range(W0):
        edge = min(x, W0 - 1 - x)
        deep = (2.5 + 2.5 * math.sin(x * 0.31 + variant) + r.uniform(0, 2)) * min(1.0, edge / 6)
        tuft = edge > 2 and r.random() < 0.3
        tall = r.randint(2, 4) if tuft else 0
        strand = edge > 3 and r.random() < 0.09
        long = r.randint(4, 12) if strand else 0
        for s in range(f):
            X = x * f + s
            low = int((deep + 1) * f + (lump[0, X] * 1.5 if f > 1 else 0))
            for D in range(max(low, 1)):
                k = 5 if D < f else 4 if D < 2 * f else 3
                if f > 1 and D == low - 1 and low > 2:
                    k = 2  # where it ends on the stone
                art.put(X, top * f + D, tone(ramp, k))
            if tuft:
                for up in range(1, (tall - 1) * f + 1 - s):
                    art.put(X, top * f - up, tone(ramp, (6 if tint != "green" else 5) + (0 if f == 1 or up < tall else 0)))
            if strand and (f == 1 or s == 0):
                for D in range(int(deep) * f, (int(deep) + long) * f):
                    art.put(X, top * f + D, tone(ramp, 3))
    return art.image()


UNDERS = {"root": None, "marble": MARBLE, "pearl": PEARL, "silver": SILVER}


def under(kind: str, variant: int, fine: int = 1) -> Image.Image:
    """What a floor in the air has under it: the earth it was lifted with and the roots out
    of it, in the gardens; a mass of its own stone, broken off, after. In shadow, as the
    underside of a thing is. `fine` draws it that many times the draft's size: stones in
    the earth, roots that fork, a thick one among the thin; in the stone, the grain of
    each block and a line of light under each joint."""
    r = rng("eden under", kind, variant)
    f = fine
    wide, tall = (6, 9, 4)[variant % 3], (3, 4, 2)[variant % 3]
    W, H = wide * T * f, (tall * T + T) * f
    art = Art(W, H)
    ramp = UNDERS[kind]
    grain = noise(("eden under", kind, variant), W, H, 2.2) if f > 1 else None

    def belly_at(x: float) -> float:
        t = (x + 0.5) / (W / f)
        deep = math.sin(min(max(t, 0.0), 1.0) * math.pi) ** 0.7 * (tall * T - 6) * (0.8 + 0.2 * math.sin(x * 0.19 + variant * 2))
        return deep + math.sin(x * 0.6) * 1.5

    for x in range(W):
        xd = x / f
        belly = belly_at(xd) * f + (math.sin(x * 0.9) * 1.2 if f > 1 else 0)
        for y in range(int(belly)):
            yd = y / f
            down = y / max(belly, 1)
            if ramp is None:
                band = min(2, int(down * 3 + (grain[y, x] * 0.5 if f > 1 else 0)))
                colour = ((58, 40, 26), (48, 32, 22), (38, 26, 18))[max(band, 0)]
                if (x * 7 + y * 3) % (31 if f == 1 else 53) < 3:
                    colour = (86, 62, 40)  # a stone in it
                if f > 1 and grain[y, x] > 0.72:
                    colour = (70, 50, 32)
                if y < 2 * f:
                    colour = (70, 50, 32)
            else:
                block = (int(xd) // 11 + int(yd) // 7) % 3 == 0
                joint = y % (7 * f) < f or (x + (int(yd) // 7) * 5 * f) % (11 * f) < f
                k = 3 - int(down * 2) + (1 if block else 0) - (1 if joint else 0)
                if f > 1 and not joint:
                    under_joint = (y - 1) % (7 * f) < f
                    k += (1 if under_joint else 0) + (-1 if grain[y, x] > 0.75 else 0)
                colour = tone(ramp, k)
            art.put(x, y, colour)
    if ramp is None:
        for n in range(wide * 2 * f):  # roots out of it
            x = r.uniform(6, W / f - 6) * f
            y = max(belly_at(x / f), 2) * f * 0.75
            length = r.randint(6, 18) * f
            thick = f > 1 and n % 5 == 0
            for i in range(length):
                x += r.uniform(-0.6, 0.6)
                colour = (120, 96, 64) if i < length - 3 * f else (150, 124, 86)
                art.put(x, y + i, colour)
                if thick and i < length * 0.6:
                    art.put(x + 1, y + i, (96, 74, 50))
                if f > 1 and i == length // 2 and n % 3 == 0:  # it forks
                    side = r.choice([-1, 1])
                    for j in range(1, r.randint(4, 9)):
                        art.put(x + side * j * 0.7, y + i + j, (120, 96, 64))
    for _ in range(f):
        art.outline((26, 20, 22) if ramp is None else ramp[0])
    return art.image()


def mist(frame: int, fine: int = 1) -> Image.Image:
    """Mist lying along a floor, before the hero: thin, pale, drifting, frame by frame.
    `fine` asks the same mist at every half pixel."""
    f = fine
    W0, H0 = 18 * T, 3 * T
    art = Art(W0 * f, H0 * f)
    for Y in range(H0 * f):
        y = Y / f
        for X in range(W0 * f):
            x = X / f
            ends = min(1.0, x / 40, (W0 - 1 - x) / 40)
            height = (y / H0) ** 1.5
            wave = math.sin((x + frame * 9) * 0.045 + y * 0.12) + math.sin((x - frame * 6) * 0.021 - y * 0.07 + 1.9)
            alpha = ends * height * (62 + 30 * wave)
            if alpha > 9:
                art.put(X, Y, FOG[3 if wave > 0.7 else 2], int(min(alpha, 120)))
    return art.image()


def rays(variant: int, fine: int = 1) -> Image.Image:
    """Shafts of low sun down through leaves or between pillars: slanted, warm, thin as
    breath. `fine` asks the same light at every half pixel, so its slant is smooth."""
    r = rng("eden rays", variant)
    f = fine
    W0, H0 = 9 * T, 11 * T
    art = Art(W0 * f, H0 * f)
    shafts = [(r.uniform(0.1, 0.9) * W0, r.uniform(7, 16), r.uniform(0.6, 1.0)) for _ in range(4 + variant)]
    for Y in range(H0 * f):
        y = Y / f
        for X in range(W0 * f):
            x = X / f
            fall = y / H0
            alpha = 0.0
            for x0, wide, strength in shafts:
                off = abs(x - (x0 - y * 0.34)) / wide
                if off < 1:
                    alpha += (1 - off) ** 1.5 * strength
            alpha *= 44 * (1 - fall**2 * 0.75) * min(1.0, y / 12)
            if alpha > 5:
                art.put(X, Y, (255, 240, 196), int(min(alpha, 70)))
    return art.image()


def great_leaf(fine: int = 1) -> Image.Image:
    """The Rising Leaf: one leaf as tall as a tower, behind the stair the hero goes up; the stair is its rib."""
    return leaf(1, tall=560, fine=fine)


def lantern_crown(fine: int = 1) -> Image.Image:
    """The crown of the Lantern Tree: a whole screen of teal leaves heaped on boughs, and
    the fruit that gives the orchard its light all through it. `fine` draws it that many
    times the draft's size: leaf on leaf in each heap, a light on each fruit."""
    r = rng("eden lantern crown")
    f = fine
    W0, H0 = 40 * T, 22 * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2
    for n in range(7):  # the limbs
        ang = math.pi * (0.12 + 0.76 * n / 6)
        branch(art, cx * f, (H0 - 4) * f, (cx - math.cos(ang) * W0 * 0.4) * f, (H0 - 30 - math.sin(ang) * H0 * 0.62) * f, (9 - abs(n - 3)) * f, BARK)
    heaps = []
    for n in range(170):
        ang = r.uniform(0, math.pi)
        d = r.uniform(0.02, 1.0) ** 0.5
        x, y = cx - math.cos(ang) * d * (W0 / 2 - 40), H0 - 60 - math.sin(ang) * d * (H0 - 108)
        heaps.append((y, x))
    for y, x in sorted(heaps):
        bundle(art, x * f, y * f, r.uniform(30, 46) * f, r.uniform(21, 30) * f, TEAL, r, ("crown", int(x), int(y)), gaps=False)
    for n in range(46):  # the fruit
        ang, d = r.uniform(0, math.pi), r.uniform(0.15, 0.95)
        x, y = int(cx - math.cos(ang) * d * (W0 / 2 - 40)), int(H0 - 40 - math.sin(ang) * d * (H0 - 100))
        if art.has(x * f, y * f):
            for DY in range(-2 * f, 2 * f + 1):
                for DX in range(-2 * f, 2 * f + 1):
                    dx, dy = DX / f, DY / f
                    dd = dx * dx + dy * dy
                    if dd <= 5:
                        colour = CYAN[3] if dd < 2 else CYAN[2] if dx + dy < 2 else CYAN[1]
                        if f > 1 and dd < 0.6 and dx < 0 and dy < 0:
                            colour = (255, 255, 255)  # a light on it
                        art.put(x * f + DX, y * f + DY, colour)
    for _ in range(f):
        art.outline(TEAL[0])
    return art.image()


def piston(frame: int, fine: int = 1) -> Image.Image:
    """A piston of the Making-house, working: a cylinder of brass on the floor and its rod
    going up and down in it, a weight on the rod. `fine` draws it that many times the
    draft's size: the rod's polish, the weight's bevel, the cylinder's bands with rivets."""
    f = fine
    W0, H0 = 3 * T, 6 * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2 * f
    rise = (0, 10, 18, 8)[frame % 4]
    for Y in range(int(H0 - 3 * T - rise - 6) * f, (H0 - 2 * T) * f):  # the rod
        for D in range(-2 * f, 3 * f):
            d = (D + 0.5) / f - 0.5 if f > 1 else D
            k = 5 - round(abs(d)) + (1 if d < 0 else 0)
            if f > 1 and -1.4 < d < -0.8:
                k = 6
            art.put(cx + D, Y, tone(STEEL, k))
    top = (H0 - 3 * T - rise - 12) * f
    for DY in range(-7 * f, 8 * f):  # the weight
        for DX in range(-12 * f, 13 * f):
            dy, dx = DY / f, DX / f
            k = 5 if dy < -4 else 4 - round(dx / 6)
            if f > 1 and (dy < -6.4 or abs(dx) > 12.4):
                k = 6 if dy < -6.4 and dx < 6 else 3  # its bevel
            art.put(cx + DX, top + DY, tone(BRASS, k))
    foot = (H0 - 2 * T - 6) * f
    for Y in range(foot, H0 * f):  # the cylinder
        cell = (Y - foot) / f % 14
        band = cell < 2
        for DX in range(-16 * f, 17 * f):
            off = DX / (16 * f)
            k = 4 - round(off * 2.2) + (1 if band else 0)
            if f > 1 and band and DX % (6 * f) == 0 and cell >= 0.5 and cell < 1.5:
                k = 6  # a rivet in the band
            if f > 1 and -0.62 < off < -0.5:
                k += 1  # the light down it
            art.put(cx + DX, Y, tone(BRASS, k))
    for _ in range(f):
        art.outline(BRASS[0])
    return art.image()


WALKERS = 8


def gathered(frame: int = 0, fine: int = 1) -> Image.Image:
    """The gathered, far off: a line of small pale figures walking calmly, all one way: a
    step on with every frame, so the line never ends and never arrives. `fine` draws them
    that many times the draft's size: a head, a robe that sways, a step."""
    f = fine
    W0, H0 = 12 * T, 2 * T
    art = Art(W0 * f, H0 * f)
    apart = 16
    lit, shade, dark = (226, 226, 238), (188, 190, 212), (160, 162, 190)
    for n in range(-1, W0 // apart + 2):
        x = n * apart + frame * apart / WALKERS
        bob = 1 if (frame + n) % 4 < 2 else 0
        ends = min(1.0, x / 26, (W0 - x) / 26)
        if ends <= 0.05:
            continue
        alpha = int(215 * ends)
        if f == 1:
            for i in range(16 - bob):
                for d in range(-2 if i > 4 else -1, 3 if i > 4 else 2):
                    art.put(x + d, H0 - 17 + bob + i, lit if d < 0 else shade, alpha)
            continue
        X, top = x * f, (H0 - 17 + bob) * f
        tall = (16 - bob) * f
        stride = ((frame + n) % 4) - 1.5
        for I in range(tall):
            i = I / f
            if i < 4.2:  # the head
                half = 1.6 * math.sin(min(1.0, (i + 0.4) / 4.6) * math.pi) + 0.4
            elif i < 5:  # the neck
                half = 1.0
            else:  # the robe, wider to its hem, swaying with the step
                half = 2.0 + (i - 5) / 11 * 1.2
            sway = stride * 0.5 * max(0.0, (i - 9) / 7) * f
            for D in range(int(-half * f), int(half * f) + 1):
                colour = lit if D < 0 else shade if D < half * f - 1 else dark
                art.put(X + D + sway, top + I, colour, alpha)
    return art.image()


def broken_knight(fine: int = 1) -> Image.Image:
    """A keeper's statue that has not been kept: the head gone, a crack down it, half its sword."""
    f = fine
    image = knight(f).transpose(Image.FLIP_LEFT_RIGHT)
    a = np.array(image)
    h, w = a.shape[:2]
    top = int(h - (1.5 * T + KNIGHT_SHOULDER) * f)
    for y in range(0, top + 6 * f):  # the head, off at the neck, aslant
        for x in range(w):
            if y < top + 2 * f + (x - w / 2) * 0.25:
                a[y, x, 3] = 0
    for y in range(top + 6 * f, h - 2 * T * f):  # the crack
        x = int(w / 2 + (8 + math.sin(y / f * 0.4) * 2) * f + (y - top) * 0.12)
        for d in range(f):
            if a[y, x + d, 3]:
                a[y, x + d, :3] = STONE[0]
        if a[y, x + f, 3]:
            a[y, x + f, :3] = STONE[6]
    art = Art(w, h)
    art.a = a
    for _ in range(f):
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


def walker(art: Art, x: float, y: float, ramp, shadow=None, alpha: int = 255, step: int = 0, fine: int = 1):
    """One of the gathered, small: a head, a robe to the ground, lit on the left; `step`
    0 or 1 is which foot is forward. (x, y) is where it stands, in the draft's pixels;
    `art` is `fine` times the draft's size, and at more than one the head is round, the
    robe falls in a curve with a fold down it, and a hand and a foot show."""
    x, y = int(round(x)), int(round(y))
    f = fine
    if f == 1:
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
        return
    X, Y = x * f + f // 2, y * f
    if shadow is not None:  # its shadow on the wall, the robe's shape a little to the right
        for I in range(12 * f):
            half = (1.4 + I / (12 * f) * 2.2) * f
            for D in range(int(-half), int(half) + 1):
                art.put(X + D + 2 * f, Y - 11 * f + I + f, shadow, alpha)
    for DY in range(-3 * f, 1):  # the head
        for DX in range(-2 * f, 2 * f):
            u, v = (DX + 0.5) / (1.8 * f), (DY + 1.5 * f) / (1.7 * f)
            if u * u + v * v <= 1:
                k = 6 if u < -0.3 else 5 if u < 0.35 else 4
                art.put(X + DX, Y - 11 * f + DY, tone(ramp, k), alpha)
    for I in range(10 * f):  # the robe: narrow at the shoulders, out to its hem
        t = I / (10 * f)
        half = (1.7 + 2.3 * t**0.7) * f  # its shoulders, then the fall of it
        for D in range(int(-half), int(math.ceil(half))):
            off = (D + 0.5) / half
            k = 6 if off < -0.7 else 3 if off > 0.7 else 5 if off < 0 else 4
            if abs(off - 0.15) < 0.12 and t > 0.3:
                k -= 1  # a fold down it
            art.put(X + D, Y - 10 * f + I, tone(ramp, k - (1 if I >= 9 * f else 0)), alpha)
    for s in range(f):  # a foot forward, a hand out of the sleeve
        art.put(X + (-2 + 3 * step) * f + s, Y, tone(ramp, 3), alpha)
        art.put(X + (2 if step else -3) * f + s, Y - 6 * f, tone(ramp, 6 if s == 0 else 5), alpha)


# --- What holds things up: nothing in Paradise is a bare bar or a plank on the air ---------

WOOD = ramp_of("1a100a 33200f 4a2f18 634021 7d532c 976a3a b88a54")
LANTERN_BARK = ramp_of("0a1618 16282a 22403e 315852 467466 66947e 98bca0")
POSTS = ("stem", "trunk", "vine", "timber")  # the Hollow's, the orchard's, the Vines', the Fields'
POST_ROWS = 10  # tiles from a floor to the underside of the platform over it


def post(kind: int, fine: int = 1) -> Image.Image:
    """What holds a platform up in a garden, from the floor to its underside: the stalk
    of a great leaf with its joints and a leaf off it; a slender trunk that forks under
    its load; two stems twisted about each other; a post of timber with its braces and
    a binding of rope. Each spreads at its foot and at its head: it grew there, or was set.
    `fine` draws it that many times the draft's size: the stalk's fibres and the sheath at
    each joint, the bark's marks, the timber's grain and the lay of its rope."""
    name = POSTS[kind]
    r = rng("eden post", name)
    f = fine
    W0, H0 = 3 * T, POST_ROWS * T
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    cx = W / 2
    if name == "stem":
        for y in range(H):
            yd = y / f
            t = yd / H0
            mid = cx + math.sin(yd / 31.0) * 3 * f
            half = (3.6 + 3 * t**4 + 5 * (1 - t) ** 8) * f
            joint = y % (36 * f)
            for x in range(int(mid - half), int(mid + half) + 1):
                off = (x + 0.5 - mid) / half
                k = 5 + round_shift(off) + (1 if joint < f else -1 if joint < 2 * f else 0)
                if f > 1 and joint >= 2 * f and (x * 3 + int(mid)) % 7 == 0 and abs(off) < 0.7:
                    k -= 1  # a fibre down it
                if f > 1 and 2 * f <= joint < 6 * f and abs(off) > 0.55:
                    k += 1  # the sheath that wraps each joint
                art.put(x, y, tone(LEAF, k))
        for n, yd in enumerate(range(36, H0 - 30, 36)):  # a leaf off each joint, to one side and the other
            side = 1 if n % 2 else -1
            mid = cx + math.sin(yd / 31.0) * 3 * f
            for i in range(15 * f):
                half = (1 + 3 * math.sin(i / (15 * f - 1) * math.pi)) * f
                for d in range(int(-half), int(half) + 1):
                    art.put(mid + side * (4 * f + i), yd * f - i * 0.7 + d, tone(LEAF, 5 if d < 0 else 3))
                art.put(mid + side * (4 * f + i), yd * f - i * 0.7, tone(LEAF, 6))
                if f > 1 and i % 5 == 2:  # its veins
                    for d in range(1, int(half)):
                        art.put(mid + side * (4 * f + i), yd * f - i * 0.7 - d, tone(LEAF, 4))
        for side in (-1, 1):  # roots, and the arms it holds its load with
            for i in range(12 * f):
                for d in range(f):
                    art.put(cx + side * (5 * f + i), H - 3 * f + (i * i) // (40 * f) + d, tone(LEAF, 3))
                    art.put(cx + side * (5 * f + i), H - 2 * f + (i * i) // (40 * f) + d, tone(LEAF, 2))
                    art.put(cx + side * (3 * f + i), 5 * f - (i * i) // (28 * f) + d, tone(LEAF, 5 if side < 0 else 3))
                    art.put(cx + side * (3 * f + i), 6 * f - (i * i) // (28 * f) + d, tone(LEAF, 3))
        outline = LEAF[0]
    elif name == "trunk":
        fork = 4 * T * f
        trunk(art, int(cx), H - 1, H - fork, 9 * f, 5 * f, BIRCH, r, lean=0.3, roots=f == 1)
        if f > 1:
            for side in (-1, 1):
                for k in range(5 * f):
                    for d in range(max(1, 2 * f - k // 2)):
                        art.put(cx + side * (9 * f // 2 + k), H - 2 - d + k // 3, BIRCH[3 if side > 0 else 4])
        for side in (-1, 1):
            branch(art, cx + 2 * f * (side > 0), fork + 4 * f, cx + side * 17 * f, 0, 4 * f, BIRCH)
        for y in range(fork + 14 * f, H - 8 * f, 9 if f == 1 else 7):  # the marks a pale bark has
            if r.random() < 0.7:
                x = int(cx + r.uniform(-3, 2) * f)
                for d in range(r.randint(2, 4 * f)):
                    if art.has(x + d, y):
                        art.put(x + d, y, BIRCH[1])
        for bx, by, rx, ry in ((W0 / 2 - 14, 9, 9, 7), (W0 / 2 + 15, 8, 8, 6), (W0 / 2 + 1, 4 * T - 2, 7, 5)):
            bundle(art, bx * f, by * f, rx * f, ry * f, TEAL, r, ("post", bx), gaps=False)
        for dy in range(-2 * f, 2 * f + 1):  # one fruit under the fork, lit
            for dx in range(-2 * f, 2 * f + 1):
                d = dx * dx + dy * dy
                if d <= 5 * f * f:
                    art.put(cx + 9 * f + dx, fork + 9 * f + dy, CYAN[3] if d < 2 * f * f else CYAN[2] if dx + dy < 2 * f else CYAN[1])
        for d in range(2 * f):
            art.put(cx + 9 * f, fork + 5 * f + d, TEAL[2])
        outline = TEAL[0]
    elif name == "vine":
        for strand in range(2):
            phase = strand * math.pi
            for y in range(H):
                yd = y / f
                mid = cx + math.sin(yd / 15.0 + phase) * 7 * f
                depth = math.cos(yd / 15.0 + phase)
                half = (4 + depth * 1.2 + 3 * (yd / H0) ** 5 + 3 * (1 - yd / H0) ** 7) * f
                for x in range(int(mid - half), int(mid + half) + 1):
                    off = (x + 0.5 - mid) / half
                    k = 4 + round_shift(off) + (1 if depth > 0.3 else -1 if depth < -0.4 else 0) - (1 if (x * 2 + y // 2) % 9 == 0 else 0)
                    if depth > -0.2 or not art.has(x, y):
                        art.put(x, y, tone(BARK, k))
        for _ in range(5):
            y = r.randint(16, H0 - 20)
            bundle(art, (W0 / 2 + r.choice([-1, 1]) * r.uniform(8, 13)) * f, y * f, r.uniform(6, 9) * f, r.uniform(5, 7) * f, LEAF, r, ("post vine", y), gaps=False)
        outline = BARK[0]
    else:
        base = Art(W0, H0)
        ashlar(base, W0 / 2 - 9, H0 - 9, W0 / 2 + 9, H0, STONE, r, course=5)  # the stone it is set in
        for y in range(8 * f, H):
            for d in range(-5 * f, 5 * f):
                grain_ = (d * 5 + y // 5 + (y // (37 * f)) * 3) % 11 == 0
                art.put(cx + d, y, tone(WOOD, (5 if d < -2 * f else 4 if d < 2 * f else 3) - (1 if grain_ else 0)))
        for side in (-1, 1):  # the braces under its load
            for i in range(22 * f):
                for d in range(-2 * f, 2 * f):
                    art.put(cx + side * i + d, 30 * f - i, tone(WOOD, 5 if d < 0 else 3))
        for x in range(int(cx - 22 * f), int(cx + 22 * f)):  # the head it carries on
            for y in range(4 * f, 9 * f):
                art.put(x, y, tone(WOOD, 6 if y < 5 * f else 4 if y < 8 * f else 2) if (x + y // (2 * f)) % 13 or f == 1 else tone(WOOD, 3))
        top = H0 // 3 * f
        for y in range(top, top + 5 * f):  # a binding of rope: its turns, and the lay of each
            turn = (y - top) // (f if f > 1 else 1)
            if f == 1 and y - top == 2:
                continue
            for d in range(-6 * f, 6 * f):
                art.put(cx + d, y, tone(WHEAT, 5 if (d + y) % 3 else 3) if turn % 3 != 2 or f == 1 else tone(WHEAT, 2))
        for i in range(9 * f):  # its end, hanging
            art.put(cx + 6 * f, top + 4 * f + i, tone(WHEAT, 4))
            if f > 1:
                art.put(cx + 6 * f + 1, top + 4 * f + i, tone(WHEAT, 3 if i % 3 else 5))
        stone = doubled(base.a, f)
        shown = stone[..., 3] > 0
        art.a[shown] = stone[shown]
        outline = WOOD[0]
    for _ in range(f):
        art.outline(outline)
    return art.image()


def lantern_trunk(fine: int = 1) -> Image.Image:
    """A length of the Lantern Tree's trunk, a storey of it: six tiles through and never
    straight, bark in ridges that wander up it, a knot, a hollow with one of its fruits
    alight in it, the glow of its sap in a crack here and there. It repeats upward: the
    tree is as tall as its room. `fine` draws it that many times the draft's size: a
    hairline furrow between two ridges, the rings of a knot, finer moss."""
    r = rng("eden lantern trunk")
    f = fine
    W0, H0 = 8 * T, 12 * T
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    cx = W0 / 2
    knots = [(cx - 22, H0 * 0.72, 7, 10), (cx + 14, H0 * 0.2, 5, 7)]
    hx, hy, hrx, hry = cx + 6, H0 * 0.46, 9, 15
    for y in range(H):
        yd = y / f
        a = 2 * math.pi * yd / H0
        left = 3 * T + 5 * math.sin(a + 0.6) + 3 * math.sin(3 * a + 2.0)
        right = 3 * T + 5 * math.sin(a + 3.9) + 3 * math.sin(2 * a + 0.4)
        for x in range(int((cx - left) * f), int((cx + right) * f)):
            xd = (x + 0.5) / f
            off = (xd - cx) / (left if xd < cx else right)
            wander = 5 * math.sin(a * 2 + xd * 0.05) + 3 * math.sin(a * 5 + xd * 0.11)
            ridge = math.sin((xd + wander) * 0.42)
            k = 4 + round_shift(off) + (1 if ridge > 0.72 else -1 if ridge < -0.5 else 0)
            if f > 1 and abs(ridge) < 0.5 and math.sin((xd + wander) * 1.26 + 1.0) > 0.93:
                k -= 1  # a hairline furrow between two ridges
            sap = ridge < -0.94 and math.sin(a * 3 + xd * 0.3) > 0.86
            for kx, ky, rx, ry in knots:
                d = math.hypot((xd - kx) / rx, (yd - ky) / ry)
                if d < 1:
                    rings = f > 1 and int(d * 9) % 2 == 0 and d > 0.25
                    k = 4 + round_shift(off) + (-2 if 0.55 < d < 0.8 or d < 0.25 else 1 if d >= 0.8 else 0) - (1 if rings and not 0.55 < d < 0.8 else 0)
                    sap = False
            d = math.hypot((xd - hx) / hrx, (yd - hy) / hry)
            if d < 1:
                colour = LANTERN_BARK[0] if d < 0.82 else LANTERN_BARK[5 if (xd - hx) + (yd - hy) > 0 else 1]
                glow = math.hypot(xd - hx, yd - hy - 7)
                art.put(x, y, CYAN[3] if glow < 1.6 else CYAN[2] if glow < 3 else CYAN[1] if glow < 4.2 else mix(colour, CYAN[1], 0.25) if glow < 9 and d < 0.82 else colour)
                continue
            art.put(x, y, CYAN[1] if sap else tone(LANTERN_BARK, k))
    for _ in range(14 * f):  # moss on the side the light is on
        x, y = int((cx - r.uniform(20, 44)) * f), r.randint(6 * f, H - 8 * f)
        for dx in range(r.randint(2, 5)):
            if art.has(x + dx, y) and art.has(x + dx, y + 1):
                art.put(x + dx, y, tone(TEAL, 5))
                art.put(x + dx, y + 1, tone(TEAL, 3))
    for _ in range(f):
        art.outline(LANTERN_BARK[0])
    return art.image()


def lantern_root(fine: int = 1) -> Image.Image:
    """The Lantern Tree's foot: the trunk spreading into roots as thick as a man that go
    into the ground one by one. `fine` draws it that many times the draft's size."""
    f = fine
    W0, H0 = 16 * T, 5 * T
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    cx = W0 / 2
    for y in range(H):
        yd = y / f
        t = yd / (H0 - 1)
        half = 3 * T + 4 + t**2.3 * 4.6 * T
        for x in range(int((cx - half) * f), int((cx + half) * f)):
            xd = (x + 0.5) / f
            off = (xd - cx) / half
            lane = off * 3 * T * (1 + t * 0.35)  # the bark's ridges fan out with the roots
            ridge = math.sin(lane * 0.42 + math.sin(yd * 0.09) * 1.5)
            toe = abs(math.sin(off * 7.6))  # between two roots the ground shows, lower down
            if t > 0.62 and toe < (t - 0.62) * 2.2:
                continue
            k = 4 + round_shift(off * 0.9) + (1 if ridge > 0.72 else -1 if ridge < -0.5 else 0) - (1 if t > 0.62 and toe < (t - 0.62) * 2.2 + 0.2 else 0)
            if f > 1 and abs(ridge) < 0.5 and math.sin(lane * 1.26 + 1.0) > 0.93:
                k -= 1
            art.put(x, y, tone(LANTERN_BARK, k))
    for _ in range(f):
        art.outline(LANTERN_BARK[0])
    for row in range(f):  # its top is the trunk's: no edge there
        art.a[row, :, 3] = np.where(art.a[f, :, 3] > 0, art.a[row, :, 3], 0)
    return art.image()


def crown_trunk(fine: int = 1) -> Image.Image:
    """A length of the Crown Vine, a storey of it: six stems as thick as a tower plaited
    about one another, the dark between them, leaves at the turns. It repeats upward.
    `fine` draws it that many times the draft's size: a line of light down each stem,
    finer grooves."""
    r = rng("eden crown trunk")
    f = fine
    W0, H0 = 16 * T, 12 * T
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    for y in range(H):  # the dark between the stems
        a = 2 * math.pi * y / H
        half = (44 + 9 * math.sin(2 * a + 1.0)) * f
        for x in range(int(W / 2 - half), int(W / 2 + half)):
            art.put(x, y, BARK[1] if (x + y // 3) % 7 else BARK[0])
    for strand in range(6):
        phase = strand * math.pi / 3
        for y in range(H):
            a = 2 * math.pi * y / H
            mid = W / 2 + (math.sin(a + phase) * 62 + math.sin(2 * a + phase * 2) * 12) * f
            depth = math.cos(a + phase)
            half = (19 + depth * 5) * f
            for x in range(int(mid - half), int(mid + half)):
                off = (x + 0.5 - mid) / half
                groove = (x * 2 + y // 2) % 12 == 0
                k = 4 + round_shift(off) + (1 if depth > 0.3 else -1 if depth < -0.4 else 0) - (1 if groove else 0)
                if f > 1 and -0.5 < off < -0.42 and not groove:
                    k += 1  # the line of light down it
                if depth > -0.2 or not art.has(x, y) or tuple(art.a[y, x, :3]) in (BARK[0], BARK[1]):
                    art.put(x, y, tone(BARK, k))
    for n in range(9):
        y = r.randint(22, H0 - 22)
        x = W0 / 2 + r.choice([-1, 1]) * r.uniform(40, 96)
        bundle(art, x * f, y * f, r.uniform(12, 18) * f, r.uniform(9, 13) * f, LEAF, r, ("crown trunk", n), gaps=False)
    for _ in range(f):
        art.outline(BARK[0])
    return art.image()


CORBELS = 2  # the kinds of what is under a ledge, for each make of ledge


def corbel(style: str, variant: int, fine: int = 1) -> Image.Image:
    """What a ledge sits on, hung under it: braces of timber to a post with a knot of root
    in the gardens; a bracket of marble, carved, on the Terraces; brackets of iron and a
    chain with its hook in the Making-house; a shard of rock and its ice by the Gate.
    Nothing a hero can touch: it is what makes a ledge a thing that was built or grew.
    `fine` draws it that many times the draft's size: the timber's grain, a moulding on
    each course of the marble, each link of the chain, the facets of the rock."""
    r = rng("eden corbel", style, variant)
    f = fine
    W0 = H0 = 2 * T
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    cx = W / 2
    if style == "plank":
        if variant == 0:
            for side in (-1, 1):
                for i in range(13 * f):
                    for d in range(2 * f):
                        art.put(cx + side * (13 * f - i) - (side > 0), i + d, tone(WOOD, (5 if side < 0 else 4) - (2 if d >= f else 0)))
            for y in range(0, 20 * f):
                for d in range(-2 * f, 2 * f):
                    art.put(cx + d, y, tone(WOOD, (5 if d < 0 else 3) - (1 if f > 1 and (d * 3 + y // 3) % 7 == 0 else 0)))
            for d in range(-3 * f, 3 * f):
                for row in range(2 * f):
                    art.put(cx + d, 11 * f + row, tone(WHEAT, (5 if row < f else 3) - (1 if f > 1 and (d + row) % 3 == 0 else 0)))
            for dy in range(-2 * f, 3 * f):
                for dx in range(-3 * f, 3 * f):
                    if dx * dx + dy * dy <= 7 * f * f:
                        art.put(cx + dx, 21 * f + dy, tone(WOOD, 4 if dx + dy < 0 else 2))
        else:
            grain = noise(("eden corbel clod", f), W, H, 2.2)
            for x in range(4 * f, W - 4 * f):  # a clod of the earth it grew out of, and its roots
                deep = int(9 * f * math.sin((x - 4 * f) / (W - 8 * f) * math.pi) ** 0.8)
                for y in range(deep):
                    colour = (70, 50, 32) if y < 2 * f else (58, 40, 26) if y < deep * 0.6 else (40, 28, 20)
                    if f > 1 and grain[y, x] > 0.7:
                        colour = (86, 62, 40)
                    art.put(x, y, colour)
            for n in range(5 * f):
                x, long = r.uniform(7, W0 - 7) * f, r.randint(6, 16) * f
                y = 6.0 * f
                for i in range(long):
                    x += r.uniform(-0.6, 0.6)
                    art.put(x, y + i, (120, 96, 64) if i < long - 3 * f else (150, 124, 86))
                    if f > 1 and n % 3 == 0 and i < long // 2:
                        art.put(x + 1, y + i, (96, 74, 50))
        outline = WOOD[0]
    elif style == "gilt":
        for row, (y0, half) in enumerate(((0, 14), (4, 11), (8, 7), (12, 4))):
            for y in range(y0 * f, (y0 + 4) * f):
                for x in range(int(cx - half * f), int(cx + half * f)):
                    off = (x + 0.5 - cx) / (half * f)
                    step = y - y0 * f
                    k = 5 + round_shift(off) - (2 if step >= 4 * f - f else 0) + (1 if step < f else 0)
                    if f > 1 and step == f:
                        k -= 1  # the moulding under each course's lip
                    art.put(x, y, GOLD[5] if row == 0 and step < f else tone(MARBLE, k))
        if variant:  # a leaf carved under it
            for i in range(9 * f):
                half = 3 * f * math.sin((i + 1) / (9 * f + 1) * math.pi)
                for d in range(int(-half), int(half) + 1):
                    art.put(cx + d, 16 * f + i, tone(MARBLE, 5 if d < 0 else 3))
                art.put(cx, 16 * f + i, tone(MARBLE, 2))
                if f > 1 and i % 4 == 1:
                    for d in range(1, int(half)):
                        art.put(cx - d, 16 * f + i - d // 2, tone(MARBLE, 4))
        else:  # a scroll at either side of it
            for side in (-1, 1):
                for dy in range(-3 * f, 3 * f + 1):
                    for dx in range(-3 * f, 3 * f + 1):
                        d = math.hypot(dx, dy) / f
                        if d <= 3.4:
                            turn = math.atan2(dy, dx * side) + d * 1.6
                            spiral = f > 1 and math.sin(turn * 1.0) > 0.55 and d > 0.8
                            art.put(cx + side * 10 * f + dx, 6 * f + dy, tone(MARBLE, 2 if (1.2 < d < 2.2 and f == 1) or spiral else 5 if dx + dy < 0 else 4))
        outline = MARBLE[0]
    elif style == "grate":
        for side in (-1, 1):
            for i in range(11 * f):
                for d in range(f):
                    art.put(cx + side * (12 * f - i), i + d, tone(STEEL, 4))
                    art.put(cx + side * (12 * f - i), i + f + d, tone(STEEL, 2))
            for y in range(0, 12 * f):
                for d in range(f):
                    art.put(cx + side * 1.5 * f - 0.5 * f + d, y, tone(STEEL, 5 if side < 0 else 3))
        for x in range(int(cx - 12 * f), int(cx + 12 * f), 4 * f):
            art.put(x, 0, tone(STEEL, 6))  # rivets
            if f > 1:
                art.put(x + 1, 0, tone(STEEL, 6))
                art.put(x, 1, tone(STEEL, 4))
                art.put(x + 1, 1, tone(STEEL, 2))
        if variant == 0:
            for i in range(12 * f, 28 * f):  # a chain, a link at a time, and its hook
                link = (i - 12 * f) % (6 * f)
                open_ = f <= link < 5 * f
                for d in ((-2 * f, 2 * f - 1) if open_ else range(-f, f)):
                    art.put(cx + d, i, tone(STEEL, 5 if d < 0 else 3))
                if f > 1 and open_:
                    art.put(cx - 2 * f + 1, i, tone(STEEL, 3))
            for dx, dy in ((0, 0), (1, 1), (2, 2), (2, 3), (1, 4), (0, 4), (-1, 3)):
                for ox in range(f):
                    for oy in range(f):
                        art.put(cx + dx * f + ox, 27 * f + dy * f + oy, tone(STEEL, 5 if ox + oy == 0 else 4))
        else:
            for y in range(12 * f, 20 * f):  # a pipe's elbow, and what drips from it
                for d in range(-3 * f, 3 * f):
                    art.put(cx + d, y, tone(BRASS, 5 if d < -f else 4 if d < f else 2))
            for d in range(-4 * f, 4 * f):
                for row in range(2 * f):
                    art.put(cx + d, 19 * f + row, tone(BRASS, 6 if row < f else 3))
            for i in range(2 * f):
                art.put(cx - f, 23 * f + i, CYAN[2] if i < f else CYAN[1])
        outline = STEEL[0]
    else:
        light, crack = facets(W, H, ("eden corbel", variant, f), 8.0 * f, 1.6)
        fine_ = noise(("eden corbel rime", variant), W, H, 2.2) if f > 1 else None
        for y in range(15 * f):
            half = (13 * (1 - y / (15 * f)) ** (0.7 + 0.3 * variant) + 1) * f
            for x in range(int(cx - half), int(cx + half)):
                off = (x + 0.5 - cx) / half
                k = 4 + round_shift(off) + round(light[y, x] * 1.2 + (fine_[y, x] * 0.4 if f > 1 else 0)) - (1 if crack[y, x] else 0)
                art.put(x, y, tone(SILVER, k))
        x = 5.0 * f
        while x < W - 5 * f:  # its ice
            away = abs(x - cx) / f
            top = 15 * f * (1 - (away / 13) ** 1.4) if away < 13 else 0
            long = r.randint(4, 13) * f
            for i in range(long):
                art.put(x, top + i, FROST[6] if i < long * 0.6 else FROST[5])
                if f > 1 and i < long * 0.35:
                    art.put(x + 1, top + i, FROST[5])
            x += r.randint(2, 5) * (1 if f == 1 else 1.5)
        outline = SILVER[0]
    for _ in range(f):
        art.outline(outline)
    return art.image()


# --- Bigger than a tile: what is set in a floor's own ground, and the dark under a roof ----

INLAYS = ("earth", "loam", "vinewood", "tilth", "marble", "pearl", "silver")  # the ground of zones 1 to 7
INLAY_KINDS = 3
ROOT = ((150, 124, 86), (120, 96, 64), (84, 64, 44))
TINTED_STONE = {"earth": (126, 92, 66), "tilth": (150, 120, 70), "loam": (40, 110, 110)}


def inlay(kind: str, variant: int, fine: int = 1) -> Image.Image:
    """Something set in the ground of a floor that is bigger than a tile, so a floor is
    not one tile said over: a boulder, roots, a seam of pebbles in the earth; the rings
    of its growth in the wood of the Vines; a frieze, a rosette, a tally cut in the
    marble; a plate, a pipe, a dial let into the pearl; a vein of ice in the rock.
    `fine` draws it that many times the draft's size, each thing in its own finer hand."""
    r = rng("eden inlay", kind, variant)
    f = fine
    W0, H0 = (4, 3, 5)[variant % 3] * T, 20
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    cx, cy = W0 / 2, H0 / 2  # on the draft

    def each():
        """Every pixel, and the place on the draft it is at."""
        for Y in range(H):
            for X in range(W):
                yield X, Y, (X + 0.5) / f - 0.5, (Y + 0.5) / f - 0.5

    def thirds(d, half, colours):
        if f == 1:
            return colours[0] if d < 0 else colours[1] if d == 0 else colours[2]
        return colours[0] if d < -half / 3 else colours[1] if d <= half / 3 else colours[2]

    if kind in TINTED_STONE:
        tint = TINTED_STONE[kind]
        stone = [mix(c, tint, 0.45) if tint else c for c in STONE]
        if variant == 0:  # a boulder the earth lies round
            light, crack = facets(W, H, ("eden inlay", kind), 9.0 * f, 1.0)
            grain = noise(("eden inlay grain", kind), W, H, 2.0) if f > 1 else None
            for X, Y, x, y in each():
                d = math.hypot((x + 0.5 - cx) / (W0 * 0.42), (y + 0.5 - cy - 1) / (H0 * 0.4))
                edge = 1 + 0.12 * math.sin(math.atan2(y - cy, x - cx) * 5 + 1)
                if d < edge:
                    k = 4 - round((x - cx) / W0 * 2.2 + (y - cy) / H0 * 2.4) + round(light[Y, X] * 0.8) - (1 if crack[Y, X] else 0)
                    if f > 1:
                        k += round(grain[Y, X] * 0.6)
                    art.put(X, Y, tone(stone, k))
            for _ in range(f):
                art.outline(stone[0])
        elif variant == 1:  # roots, through it
            for n in range(3):
                y = 5.0 + n * 6 + r.uniform(-1, 1)
                thick = 2.4 - n * 0.5
                path = {}
                for x in range(2, W0 - 2):
                    y += math.sin(x * 0.21 + n * 2) * 0.35 + r.uniform(-0.2, 0.2)
                    path[x] = y
                for X in range(2 * f, (W0 - 2) * f):
                    x = X / f
                    x0 = min(int(x), W0 - 3)
                    y = path[x0] + (path[min(x0 + 1, W0 - 3)] - path[x0]) * (x - x0)
                    half = thick * min(1.0, (x - 1) / 9, (W0 - 2 - x) / 9) * f
                    for d in range(int(-half), int(half) + 1):
                        art.put(X, y * f + d, thirds(d, half, ROOT))
                    if X % (11 * f) == (5 + n) * f:
                        for i in range(1, 5 * f):
                            art.put(X + i, y * f + (i if n % 2 else -i) * 0.8, ROOT[1])
                            if f > 1:
                                art.put(X + i, y * f + (i if n % 2 else -i) * 0.8 + 1, ROOT[2])
        else:  # a seam of pebbles
            x = 3.0
            while x < W0 - 6:
                wide, y = r.randint(3, 7), cy + math.sin(x * 0.11) * 4 + r.uniform(-2, 2)
                for DY in range(-2 * f, 3 * f):
                    for DX in range(wide * f):
                        dx, dy = (DX + 0.5) / f - 0.5, (DY + 0.5) / f - 0.5
                        if (dx - wide / 2 + 0.5) ** 2 / (wide / 2) ** 2 + dy * dy / 5.5 <= 1:
                            k = 5 if dy < 0 and dx < wide - 1 else 4 if dy < 1 else 2
                            if f > 1 and dy < -1 and dx < wide / 3:
                                k = 6  # the light on it
                            art.put(x * f + DX, y * f + DY, tone(stone, k))
                x += wide + r.randint(2, 6)
    elif kind == "vinewood":
        rings = 5 - variant
        for X, Y, x, y in each():
            d = math.hypot((x + 0.5 - cx) / (W0 * 0.4), (y + 0.5 - cy) / (H0 * 0.42))
            ang = math.atan2(y - cy, x - cx)
            d *= 1 + 0.1 * math.sin(ang * 3 + variant)
            if d < 1:
                ring = (d * rings) % 1
                k = 2 if ring < 0.22 else 5 if ring < 0.4 else 4 if d < 0.5 else 3
                if f > 1 and d > 0.3 and abs(math.sin(ang * 2.5 + variant)) < 0.05:
                    k = 1  # where the wood has checked as it dried
                art.put(X, Y, tone(BARK, k))
    elif kind == "marble":
        if variant == 0:  # a frieze, in a key pattern
            key = ("#########...", "#.......#...", "#..####.#...", "#..#....#...", "#..######...", "#...........")
            for X in range(2 * f, (W0 - 2) * f):
                for row, line in enumerate(key):
                    for scale in range(2 * f):
                        Y = (4 + row * 2) * f + scale
                        if line[(X // f // 2) % 12] == "#":
                            art.put(X, Y, MARBLE[2])
                            art.put(X + 1, Y + 1, MARBLE[6])
                for y in (2, H0 - 4):
                    for s in range(f):
                        art.put(X, y * f + s, MARBLE[2])
                        art.put(X, y * f + f + s, MARBLE[6])
        elif variant == 1:  # a rosette
            for X, Y, x, y in each():
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                d, ang = math.hypot(dx, dy), math.atan2(dy, dx)
                petal = 9 * (0.55 + 0.45 * abs(math.cos(ang * 4)))
                if d < 2.5:
                    art.put(X, Y, GOLD[5 if dx + dy < 0 else 3])
                elif d < petal:
                    art.put(X, Y, MARBLE[6 if dx + dy < -2 else 4 if abs(math.cos(ang * 4)) > 0.3 else 2])
                elif d < petal + 1.2:
                    art.put(X, Y, MARBLE[2])
        else:  # a tally, cut in rows: fives, and a few over
            for row in range(2):
                x = 5 + row * 3
                while x < W0 - 12:
                    for stroke in range(4):
                        for Y in range((4 + row * 9) * f, (10 + row * 9) * f):
                            for s in range(f):
                                art.put((x + stroke * 2) * f + s, Y, MARBLE[2])
                            art.put((x + stroke * 2) * f + f, Y, MARBLE[6])
                    for i in range(8 * f):
                        art.put((x - 1) * f + i, (9 + row * 9) * f - i * 0.7, MARBLE[1])
                        if f > 1:
                            art.put((x - 1) * f + i, (9 + row * 9) * f - i * 0.7 + 1, MARBLE[1])
                    x += 13
    elif kind == "pearl":
        if variant == 0:  # a plate let into it, riveted
            for Y in range(3 * f, (H0 - 3) * f):
                for X in range(3 * f, (W0 - 3) * f):
                    x, y = X // f, Y // f
                    edge = y in (3, H0 - 4) or x in (3, W0 - 4)
                    art.put(X, Y, BRASS[2] if edge else tone(PEARL, 4 + (1 if y < 6 else 0) - (1 if y > H0 - 7 else 0) + (1 if (X + Y * 2) % (23 * f) == 0 else 0)))
            for x in range(6, W0 - 5, 9):
                for y in (5, H0 - 6):
                    if f == 1:
                        art.put(x, y, BRASS[6])
                        art.put(x + 1, y + 1, BRASS[2])
                        continue
                    for dx, dy, colour in ((0, 0, BRASS[6]), (1, 0, BRASS[6]), (0, 1, BRASS[6]), (1, 1, BRASS[4]), (2, 1, BRASS[2]), (2, 2, BRASS[2]), (1, 2, BRASS[2])):
                        art.put(x * f + dx, y * f + dy, colour)
        elif variant == 1:  # a pipe, laid in it
            for X in range(W):
                flange = (X // f) % 21 in (9, 10, 11)
                for row in range(9 * f):
                    d = row // f - 4
                    if abs(d) < 4 or flange:
                        k = (5 if d < -1 else 4 if d < 2 else 2) + (1 if flange else 0)
                        if f > 1 and row == 2 * f + 1:
                            k += 1  # the light along it
                        art.put(X, int(cy - 4) * f + row, tone(BRASS, k))
            for _ in range(f):
                art.outline(BRASS[0])
        else:  # a dial
            for X, Y, x, y in each():
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                d = math.hypot(dx, dy)
                if d < 9.5:
                    tick = abs(math.sin(math.atan2(dy, dx) * 6)) > (0.25 if f == 1 else 0.16)
                    art.put(X, Y, BRASS[5 if dx + dy < 0 else 3] if d > 7.5 else PEARL[6] if tick or d < 5 else PEARL[2])
            for i in range(7 * f):
                art.put(cx * f + i * 0.6, cy * f - i * 0.8, (170, 40, 40))
            for dy in range(f):
                for dx in range(f):
                    art.put(cx * f + dx, cy * f + dy, BRASS[1])
            for _ in range(f):
                art.outline(BRASS[0])
    else:  # a vein of ice in the rock
        for X in range(2 * f, (W0 - 2) * f):
            x = X / f
            y = cy + math.sin(x * 0.13 + variant * 2) * 5 + math.sin(x * 0.41) * 1.5
            thick = 1.2 + 1.8 * math.sin((x - 2) / (W0 - 4) * math.pi) + 0.6 * math.sin(x * 0.9)
            half = thick * f
            for d in range(int(-half), int(half) + 1):
                art.put(X, y * f + d, thirds(d, half, (FROST[6], FROST[5], FROST[4])))
            if X % (9 * f) == 4 * f:
                art.put(X, y * f - half - 1, (255, 255, 255))
                if f > 1:
                    art.put(X + 1, y * f - half - 2, (255, 255, 255))
                for i in range(1, 5 * f):  # a splinter off it
                    art.put(X + i, y * f + half + i * (1 if (X // f) % 2 else -1) * 0.8, FROST[4])
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


def brush(kind: int, fine: int = 1) -> Image.Image:
    """Something that grows on a floor, as high as the hero's knee or his waist, that he
    goes through and that bends as he does: a tuft of long grass with a flower in it; a
    fern of the orchard, its tips alight; the broad fronds under the Vines; a stand of
    wheat that has seeded itself off its row; flowers on tall stalks along a terrace;
    grass with the frost on it by the Gate. It turns about its foot. `fine` draws it that
    many times the draft's size: more blades and finer, a rib down each, petals on a flower."""
    name = BRUSHES[kind]
    r = rng("eden brush", name)
    f = fine
    W0, H0 = 2 * T + 8, T + 14
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    cx, foot = W / 2, H - 3 * f
    ramp = {"grass": LEAF, "fern": TEAL, "frond": VINE, "wheat": WHEAT, "bloom": LEAF, "frost": FROST}[name]

    def blade(x0, tall, lean, wide=1.0, lit=5, dark=3):
        tall *= f
        for i in range(int(tall)):
            t = i / tall
            x = x0 + lean * t * t * tall * 0.45
            half = wide * f * (1 - t) ** 0.7
            for d in range(int(-half), int(half) + 1):
                art.put(x + d, foot - i, tone(ramp, lit if d <= 0 else dark))
            if f > 1 and half >= 1.5:
                art.put(x, foot - i, tone(ramp, lit + 1))  # its rib, lit
        return x0 + lean * tall * 0.45, foot - tall

    def flower(x, y, petals):
        dark, mid, lit = petals
        if f == 1:
            for dx, dy, colour in ((0, 0, lit), (-1, 0, mid), (1, 0, mid), (0, -1, lit), (0, 1, mid)):
                art.put(x + dx, y + dy, colour)
            return
        for dx, dy, colour in (
            (0, 0, GOLD[5]), (1, 0, GOLD[4]),
            (-1, -1, lit), (0, -2, lit), (1, -2, lit), (2, -1, mid),
            (-2, 0, lit), (-2, 1, mid), (3, 0, mid), (3, 1, dark),
            (-1, 2, mid), (0, 3, dark), (1, 3, dark), (2, 2, dark),
        ):  # fmt: skip
            art.put(x + dx, y + dy, colour)

    if name in ("grass", "frost"):
        count = 11 if f == 1 else 17
        for n in range(count):
            spread = (n - (count - 1) / 2) / ((count - 1) / 2)
            x0 = cx + spread * 11 * f + r.uniform(-1, 1)
            tip = blade(x0, r.uniform(9, 23), spread * r.uniform(0.6, 1.2), 1.4 if f == 1 else 1.1, 5 if n % 2 else 4, 3 if n % 2 else 2)
            if name == "frost" and n % 2:
                art.put(tip[0], tip[1], (255, 255, 255))
                art.put(tip[0], tip[1] + 2, FROST[6])
                if f > 1:
                    art.put(tip[0] + 1, tip[1] + 1, (255, 255, 255))
        if name == "grass":
            for fx, fy in ((cx - 6 * f, foot - 19 * f), (cx + 8 * f, foot - 15 * f)):
                flower(fx, fy, ROSE)
    elif name in ("fern", "frond"):
        for n in range(5):  # fronds: a rib that arches over, leaflets down either side of it
            lean = (n - 2) / 2 * 1.3 + r.uniform(-0.15, 0.15)
            tall = (r.uniform(15, 24) - abs(n - 2) * 2) * f
            every = (2 if name == "fern" else 3) if f == 1 else (3 if name == "fern" else 4)
            for i in range(int(tall)):
                t = i / tall
                x, y = cx + lean * t * tall * 0.5, foot - i * (1 - 0.35 * t * abs(lean))
                art.put(x, y, tone(ramp, 2))
                if i > 3 * f and i % every == 0:
                    long = ((2.5 if name == "fern" else 5.0) * math.sin(min(1.0, t * 1.2) * math.pi) + 1) * f
                    for side in (-1, 1):
                        for d in range(1, int(long) + 1):
                            art.put(x + side * d, y + d * 0.5, tone(ramp, 5 if side < 0 else 4 if d < long - 1 else 3))
                            if name == "frond" or f > 1:
                                art.put(x + side * d, y + d * 0.5 + 1, tone(ramp, 3))
            if name == "fern":  # its tip, alight
                art.put(x, y, CYAN[2])
                art.put(x, y - 1, CYAN[3])
                if f > 1:
                    art.put(x + 1, y, CYAN[2])
                    art.put(x - 1, y, CYAN[1])
    elif name == "wheat":
        count = 9 if f == 1 else 10
        for n in range(count):
            spread = (n - (count - 1) / 2) / ((count - 1) / 2)
            x0 = cx + spread * 10.4 * f + r.uniform(-1, 1)
            tall = r.uniform(14, 22) * f
            lean = spread * 0.5 + r.uniform(-0.15, 0.15)
            for i in range(int(tall)):
                t = i / tall
                art.put(x0 + lean * t * t * tall * 0.4, foot - i, tone(WHEAT, 3 if n % 2 else 2))
            hx, hy = x0 + lean * tall * 0.4, foot - int(tall)
            long = 8 if f == 1 else 13
            for i in range(long):
                wide = 1 if 0 < i < long - 1 else 0
                if f > 1 and 2 < i < long - 3:
                    wide = 2
                for d in range(-wide, wide + 1):
                    art.put(hx + d, hy + i, tone(WHEAT, 6 if (i + d) % 2 == 0 and d <= 0 else 5 if d <= 0 else 4))
                if f > 1 and i % 3 == 1:
                    art.put(hx - wide - 1, hy + i - 1, tone(WHEAT, 6))
            art.put(hx + 1, hy - 2, tone(WHEAT, 5))
    else:  # flowers on tall stalks
        petals = ((154, 58, 102), (226, 122, 166), (255, 240, 246)), ((90, 58, 168), (168, 136, 240), (239, 230, 255)), (GOLD[2], GOLD[4], GOLD[6])
        for n in range(6):
            x0 = cx + (n - 2.5) * 5 * f + r.uniform(-1.5, 1.5)
            tall = r.uniform(11, 23) * f
            lean = (n - 2.5) / 2.5 * 0.5
            for i in range(int(tall)):
                t = i / tall
                art.put(x0 + lean * t * t * tall * 0.4, foot - i, tone(LEAF, 3))
                if i in (6 * f, 13 * f) and n % 2:
                    for d in range(1, 4 * f):
                        art.put(x0 + lean * t * t * tall * 0.4 + (d if i == 6 * f else -d), foot - i - d // 2, tone(LEAF, 5 if d < 3 * f else 4))
                        if f > 1:
                            art.put(x0 + lean * t * t * tall * 0.4 + (d if i == 6 * f else -d), foot - i - d // 2 + 1, tone(LEAF, 3))
            hx, hy = x0 + lean * tall * 0.4, foot - int(tall)
            dark, mid, lit = petals[n % 3]
            if f == 1:
                for dx, dy, colour in ((0, 0, GOLD[5]), (-1, 0, mid), (1, 0, dark), (0, -1, lit), (0, 1, dark), (-1, -1, lit), (1, -1, mid), (-2, 0, mid), (2, 0, dark), (0, -2, lit)):
                    art.put(hx + dx, hy + dy, colour)
            else:
                flower(hx, hy, petals[n % 3])
    for _ in range(f):
        art.outline(ramp[0] if name != "wheat" else WHEAT[0])
    return art.image()


def fine_glowfruit(frame: int) -> np.ndarray:
    """The fruit that gives the orchard its light, at the size that ships: a round fruit on
    a stem under two leaves, lit from within, a light on its skin, its glow swelling and
    fading over the frames."""
    size = PIXELS
    a = np.zeros((size, size, 4), dtype=np.uint8)

    def put(x, y, colour):
        x, y = int(x), int(y)
        if 0 <= x < size and 0 <= y < size:
            a[y, x, :3] = colour
            a[y, x, 3] = 255

    for y in range(0, 9):
        put(15, y, TEAL[3])
        put(16, y, TEAL[2])
    for side, (lx, ly) in ((-1, (11, 5)), (1, (21, 3))):  # its two leaves
        for dy in range(-2, 3):
            for dx in range(-4, 5):
                if (dx / 4.4) ** 2 + (dy / 2.2) ** 2 <= 1:
                    put(lx + dx, ly + dy + (dx * side) // 3, TEAL[6] if dy < 0 else TEAL[5] if dy == 0 else TEAL[4])
    bright = (0, 1, 2)[frame % 3]
    for dy in range(-10, 11):
        for dx in range(-10, 11):
            d = math.hypot(dx + 0.5, dy + 0.5)
            if d <= 9.4:
                colour = CYAN[3] if d < 3.6 + bright * 1.6 else CYAN[2] if dx + dy < 5 else CYAN[1]
                if d > 8.2 and dx + dy > 2:
                    colour = CYAN[0]  # its skin, in its own shade
                put(16 + dx, 19 + dy, colour)
    for dx, dy in ((-4, -5), (-3, -5), (-4, -4)):  # a light on its skin
        put(16 + dx, 19 + dy, (255, 255, 255))
    return a


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
# What holds things up and what hangs from them: posts, what is under a ledge, what is under a floor, ivy.
for _k in range(len(POSTS)):
    FINE[f"eden_post_{_k}"] = lambda k=_k: post(k, DENSITY)
for _style in LEDGES:
    for _v in range(CORBELS):
        FINE[f"eden_corbel_{_style}_{_v}"] = lambda a=(_style, _v): corbel(*a, fine=DENSITY)
for _v in range(3):
    for _kind in UNDERS:
        FINE[f"eden_under_{_kind}_{_v}"] = lambda a=(_kind, _v): under(*a, fine=DENSITY)
for _tint in TINTS:
    for _v in range(IVIES):
        FINE[f"eden_ivy_{_tint}_{_v}"] = lambda a=(_tint, _v): ivy(*a, fine=DENSITY)
# The two great trunks a storey at a time, the vines, the boughs, the wheat.
FINE["eden_lantern_trunk"] = lambda: lantern_trunk(DENSITY)
FINE["eden_lantern_root"] = lambda: lantern_root(DENSITY)
FINE["eden_crown_trunk"] = lambda: crown_trunk(DENSITY)
for _v in range(VINES):
    FINE[f"eden_vine_{_v}"] = lambda v=_v: vine_trunk(v, DENSITY)
for _v in range(2):
    FINE[f"eden_bough_{_v}"] = lambda v=_v: bough(v == 1, fine=DENSITY)
    FINE[f"eden_vine_bough_{_v}"] = lambda v=_v: bough(v == 1, LEAF, fine=DENSITY)
for _v in range(4):
    FINE[f"eden_wheat_{_v}"] = lambda v=_v: wheat(v, DENSITY)
# What he goes through, what is set in the ground, the crags and the keepers by the Gate.
for _k in range(len(BRUSHES)):
    FINE[f"eden_brush_{_k}"] = lambda k=_k: brush(k, DENSITY)
for _kind in INLAYS:
    for _v in range(INLAY_KINDS):
        FINE[f"eden_inlay_{_kind}_{_v}"] = lambda a=(_kind, _v): inlay(*a, fine=DENSITY)
for _v in range(CRAGS):
    FINE[f"eden_crag_{_v}"] = lambda v=_v: crag(v, DENSITY)
FINE["eden_knight"] = lambda: knight(DENSITY)
FINE["eden_knight_1"] = lambda: broken_knight(DENSITY)
# The Making-house: its pipes, gears, pistons and door, and the gathered who go in.
for _long in (6, 12):
    FINE[f"eden_pipe_h_{_long}"] = lambda n=_long: pipe(True, n, DENSITY)
    FINE[f"eden_pipe_v_{_long}"] = lambda n=_long: pipe(False, n, DENSITY)
for _frame in range(3):
    FINE["eden_gear" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: gear(s, DENSITY)
for _frame in range(FRAMES):
    FINE["eden_piston" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: piston(s, DENSITY)
for _frame in range(WALKERS):
    FINE["eden_gathered" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: gathered(s, DENSITY)
FINE["eden_making_door"] = lambda: making_door(DENSITY)
# What makes it an old, great place: arches, a window, buttresses, ruins, swords, its people.
for _kind in ("stone", "vine"):
    FINE[f"eden_arch_{_kind}"] = lambda k=_kind: gate_arch(k, DENSITY)
FINE["eden_window"] = lambda: gothic_window(DENSITY)
for _v in range(2):
    FINE[f"eden_buttress_{_v}"] = lambda v=_v: buttress(v == 1, DENSITY)
for _v in range(3):
    FINE[f"eden_ruin_{_v}"] = lambda v=_v: ruin(v, DENSITY)
FINE["eden_sword"] = lambda: sword(False, DENSITY)
FINE["eden_bonfire"] = lambda: sword(True, DENSITY)
for _who in ("mereth", "ithuriel"):
    FINE[f"eden_{_who}"] = lambda w=_who: figure(w, DENSITY)
for _v in range(2):
    FINE[f"eden_chain_{_v}"] = lambda v=_v: chain(v, DENSITY)
    FINE[f"eden_rays_{_v}"] = lambda v=_v: rays(v, DENSITY)
# The fog in the door, the mist along a floor; the wear on the stone.
for _frame in range(FRAMES):
    FINE["eden_fog" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: fog(s, DENSITY)
    FINE["eden_mist" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: mist(s, DENSITY)
for _v in range(3):
    FINE[f"eden_crack_{_v}"] = lambda v=_v: crack(v, DENSITY)
    for _tint in ("green", "pale", "frost"):
        FINE[f"eden_moss_{_tint}_{_v}"] = lambda a=(_tint, _v): moss(*a, fine=DENSITY)
# What stands far off behind a place, the Lantern Tree's crown, and what passes before the eye.
for _name, _paint in (("hollow", far_hollow), ("orchard", far_orchard), ("vines", far_vines), ("fields", far_fields), ("terraces", far_terraces), ("cliffs", far_cliffs)):
    FINE[f"eden_far_{_name}"] = lambda p=_paint: p(DENSITY)
for _v in range(3):
    FINE[f"eden_far_house_{_v}"] = lambda v=_v: far_house(v, DENSITY)
for _v in range(2):
    FINE[f"eden_far_gate_{_v}"] = lambda v=_v: far_gate(v, DENSITY)
FINE["eden_far_wheel"] = lambda: far_wheel(DENSITY)
for _rows, _wide in ((10, 2), (10, 1)):
    FINE[f"eden_pillar_{_rows}x{_wide}"] = lambda a=(_rows, _wide): pillar(*a, fine=DENSITY)
FINE["eden_lantern_crown"] = lambda: lantern_crown(DENSITY)
for _k in range(len(FRONTS)):
    FINE[f"eden_front_{_k}"] = lambda k=_k: front(k, fine=DENSITY)
    if _k < 3:
        for _s, _lean in ((1, 2), (2, 4)):
            FINE[f"eden_front_{_k}_s{_s}"] = lambda a=(_k, _lean): front(*a, fine=DENSITY)
# What is drawn in the second style (tv2_style), by key: given the size it ships at, the
# picture and its normal map. The generator prefers it to FINE.
STYLE = {}
for _v in range(TEAL_TREES):
    for _frame in range(FRAMES):
        _key = f"eden_teal_tree_{_v}" + (f"{FRAME}{_frame}" if _frame else "")
        STYLE[_key] = lambda w, h, k=_key, v=_v, s=_frame: tv2_style.tree(w, h, SINK_OF[k] * DENSITY, ("eden teal", v), "1c8c84", "b4c6be", frame=s, pale=True, petals=True, crown=1.1, lean=(-0.3, 0.4, 0.2)[v % 3])

# Raised from their drawings in the first style, so what stands where (a door, a ledge) stays.
for _key in ("eden_leaf_0", "eden_leaf_1", "eden_leaf_2", "eden_leaf_3", "eden_ivy_green_0", "eden_ivy_green_1", "eden_ivy_green_2", "eden_ivy_teal_0", "eden_ivy_teal_1", "eden_ivy_teal_2", "eden_ivy_pale_0", "eden_ivy_pale_1", "eden_ivy_pale_2", "eden_ivy_frost_0", "eden_ivy_frost_1", "eden_ivy_frost_2", "eden_vine_0", "eden_vine_1", "eden_vine_2", "eden_wheat_0", "eden_wheat_1", "eden_wheat_2", "eden_wheat_3", "eden_bough_0", "eden_vine_bough_0", "eden_bough_1", "eden_vine_bough_1", "eden_sundial", "eden_wheel", "eden_pipe_h_6", "eden_pipe_v_6", "eden_pipe_h_12", "eden_pipe_v_12", "eden_gear", "eden_gear_f1", "eden_gear_f2", "eden_making_door", "eden_low_gate", "eden_crag_0", "eden_crag_1", "eden_crag_2", "eden_pillar_10x2", "eden_pillar_10x1", "eden_arch_stone", "eden_arch_vine", "eden_window", "eden_buttress_0", "eden_chain_0", "eden_buttress_1", "eden_chain_1", "eden_ruin_0", "eden_ruin_1", "eden_ruin_2", "eden_sword", "eden_bonfire", "eden_knight", "eden_crack_0", "eden_moss_green_0", "eden_moss_pale_0", "eden_moss_frost_0", "eden_under_root_0", "eden_under_marble_0", "eden_under_pearl_0", "eden_under_silver_0", "eden_crack_1", "eden_moss_green_1", "eden_moss_pale_1", "eden_moss_frost_1", "eden_under_root_1", "eden_under_marble_1", "eden_under_pearl_1", "eden_under_silver_1", "eden_crack_2", "eden_moss_green_2", "eden_moss_pale_2", "eden_moss_frost_2", "eden_under_root_2", "eden_under_marble_2", "eden_under_pearl_2", "eden_under_silver_2", "eden_great_leaf", "eden_lantern_crown", "eden_piston", "eden_piston_f1", "eden_piston_f2", "eden_piston_f3", "eden_knight_1", "eden_post_0", "eden_post_1", "eden_post_2", "eden_post_3", "eden_lantern_trunk", "eden_lantern_root", "eden_crown_trunk", "eden_corbel_plank_0", "eden_corbel_plank_1", "eden_corbel_grate_0", "eden_corbel_grate_1", "eden_corbel_gilt_0", "eden_corbel_gilt_1", "eden_corbel_rime_0", "eden_corbel_rime_1", "eden_inlay_earth_0", "eden_inlay_earth_1", "eden_inlay_earth_2", "eden_inlay_loam_0", "eden_inlay_loam_1", "eden_inlay_loam_2", "eden_inlay_vinewood_0", "eden_inlay_vinewood_1", "eden_inlay_vinewood_2", "eden_inlay_tilth_0", "eden_inlay_tilth_1", "eden_inlay_tilth_2", "eden_inlay_marble_0", "eden_inlay_marble_1", "eden_inlay_marble_2", "eden_inlay_pearl_0", "eden_inlay_pearl_1", "eden_inlay_pearl_2", "eden_inlay_silver_0", "eden_inlay_silver_1", "eden_inlay_silver_2", "eden_brush_0", "eden_brush_1", "eden_brush_2", "eden_brush_3", "eden_brush_4", "eden_brush_5"):
    STYLE[_key] = lambda w, h, k=_key: tv2_style.relief(FINE[k]())

# The buildings drawn again over their own plans (tv2_style.repaint): each region read from the
# first-style drawing as the material it is and painted anew in it, every opening kept.
for _key in ("eden_making_door", "eden_low_gate", "eden_arch_stone", "eden_arch_vine", "eden_window", "eden_buttress_0", "eden_buttress_1", "eden_ruin_0", "eden_ruin_1", "eden_ruin_2"):
    STYLE[_key] = lambda w, h, k=_key: tv2_style.repainted(FINE[k](), k)

# The keeper statues carved anew (tv2_style.keeper_statue), the broken one snapped off along
# the line its first-style drawing breaks it; the pillars turned round over their outlines.
KEEPER_BREAK = [(20, 100), (40, 94), (50, 84), (60, 77), (70, 80), (80, 82), (95, 86), (110, 90), (125, 97), (140, 104)]
STYLE["eden_knight"] = lambda w, h: tv2_style.keeper_statue(w, h, "eden knight")
STYLE["eden_knight_1"] = lambda w, h: tv2_style.keeper_statue(w, h, "eden knight 1", broken=KEEPER_BREAK)
for _key in ("eden_pillar_10x2", "eden_pillar_10x1"):
    STYLE[_key] = lambda w, h, k=_key: tv2_style.turned(FINE[k](), k)
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
