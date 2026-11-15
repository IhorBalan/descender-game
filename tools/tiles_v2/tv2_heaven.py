#!/usr/bin/env python3
"""Draws what the Paradise of the Tiles V2 world is dressed with, at the detail the Earth is
drawn at (tv2_earth.py, whose canvas and brushes these use): its trees in blossom and with
leaves of gold, the towers of its castles under their pointed roofs, the tiles of their
great roofs, the columns, bays and high seat of the citadel's hall, a gate, the round
temple of the Orchard and the host that stand by it in marble. Not a script.

Everything here is a picture (`picture` in BACKDROPS), stood in a cell by the map
(`@mark picture column row key`) and lit by the tile light. None of it is solid: what the
hero stands on and is stopped by is the map's own tiles, which these are drawn over or
behind. `make_world_map.paradise` says where each stands.

The light comes from the top left, as everywhere: a tower is round, so its stone goes from
lit on its left through its face to shade on its right, and a roof has a side in the sun
and a side out of it. What stands further back is paler, bluer and plainer.
"""
from __future__ import annotations

import math

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
from tv2_earth import Art, bundle, branch, noise, ramp_of, trunk

# --- Colours: outline first, then darkest .. brightest ---------------------------------

BLOSSOM = ramp_of("5a1f3e 9a3a66 c8588a e27aa6 f29cc0 fbc4da fff0f6")
GOLDLEAF = ramp_of("4a3006 8a5c10 b8821c dca62c f2c648 ffe07c fff6c4")
BIRCH = ramp_of("2a2a34 6e6e7c 9a9aa8 c4c4d0 e2e2ea f6f6fa")
MARBLE = ramp_of("3a3a4c 86849a a3a1b4 bebccb d6d4de eae9ef ffffff")
SHADOWED = ramp_of("26263a 4c4b62 5c5b72 6c6b82 7c7b92 8e8da2 a2a1b4")  # marble the sun does not reach
MARBLE_FAR = ramp_of("5a668a 8794b6 97a3c2 a6b1cd b5bfd7 c4cce0 d2d9e9")
AZURE = ramp_of("0e1c48 1a3684 2450ae 3068cc 4a86e2 76aaf4 b0d0fc")
AZURE_FAR = ramp_of("38508a 4a639e 5872ac 6682ba 7692c8 8aa4d4 a2b8e0")
GOLD = ramp_of("4a3206 8a5e0c b88314 dca922 f2c93c fde06a fff3a8")
CLOTH = ramp_of("0c1848 16297a 2140a2 2f5ac8 4c7ee6 82a8f6 c4d8ff")
AMBER = ramp_of("7a3c08 e08a18 ffb830 ffd866 fff2b8")
GLASS = [ramp_of("1c1a3a 2a4cc0 3f8ee8 7cc4f6")[k] for k in range(4)] + ramp_of("8a5ce0 e0407a ffd45a")
LEAD = ramp_of("1c1a3a")[0]

T = TILE


def tone(ramp, k):
    return ramp[max(1, min(int(k), len(ramp) - 1))]


# --- Trees -----------------------------------------------------------------------------


def tree(variant: int, leaves, tall: int | None = None) -> Image.Image:
    """A tree of Paradise: a pale trunk that parts into branches well over the hero's head,
    a crown of a few rounded bundles of blossom or of gold leaf, lower on one side, with
    a few petals already let go under it."""
    r = rng("heaven tree", variant, leaves[3])
    tall = tall or r.randint(150, 208)
    wide = int(tall * r.uniform(0.66, 0.82))
    W, H = wide + 18, tall + T + 4
    art = Art(W, H)
    foot_x, foot_y = W // 2 + r.randint(-4, 4), H - T + 6
    split = int(tall * r.uniform(0.36, 0.46))
    crown_cy = H - T - tall + int(tall * 0.36)
    trunk(art, foot_x, foot_y, split + 12, max(9, int(tall * 0.066)) + r.randint(0, 2), 5, BIRCH, r, lean=r.uniform(-0.6, 0.6))
    # The marks of a pale bark: short dark dashes across the trunk.
    for i in range(6, split + 6, r.randint(7, 10)):
        for dx in range(-2, 1):
            if art.has(foot_x + dx, foot_y - i):
                art.put(foot_x + dx, foot_y - i, BIRCH[1])
    bundles = []
    count = r.randint(6, 8)
    for i in range(count):
        a = -math.pi * (0.1 + 0.8 * i / (count - 1)) + r.uniform(-0.15, 0.15)
        dist = r.uniform(0.45, 0.85)
        bx = W / 2 + math.cos(a) * wide * 0.36 * dist * 1.25
        by = crown_cy + math.sin(a) * tall * 0.24 * dist + r.uniform(-4, 6)
        bundles.append((bx, by, r.uniform(0.17, 0.25) * wide, r.uniform(0.15, 0.2) * tall))
    side = r.choice([-1, 1])
    bundles.append((W / 2 + side * wide * 0.33, crown_cy + tall * 0.17, wide * 0.17, tall * 0.12))
    bundles.append((W / 2 + r.uniform(-6, 6), crown_cy - tall * 0.2, wide * 0.24, tall * 0.17))
    top_y = foot_y - split - 10
    for bx, by, _, _ in bundles:
        branch(art, foot_x, top_y + 8, bx, by + 6, 3 if abs(bx - foot_x) > wide * 0.2 else 4, BIRCH)
    for k, (bx, by, rx, ry) in enumerate(sorted(bundles, key=lambda b: -b[1])):
        bundle(art, bx, by, rx, ry, leaves, r, (variant, k, leaves[3]))
    # Brighter flecks: a tree in flower catches the light all over.
    for _ in range(wide * 2):
        x, y = r.randint(4, W - 5), r.randint(H - T - tall, H - T - split + 8)
        if art.has(x, y) and art.has(x, y + 2) and tuple(art.a[y, x, :3]) not in (tuple(BIRCH[k]) for k in range(6)):
            art.put(x, y, leaves[6])
    art.outline(leaves[0])
    # Fallen petals on the ground by its foot.
    for _ in range(14):
        x = foot_x + r.randint(-wide // 2, wide // 2)
        art.put(x, foot_y - r.randint(0, 1), leaves[r.choice([4, 5, 6])])
    return art.image()


# --- Stone and roofs -------------------------------------------------------------------


def round_shift(off: float) -> int:
    """How a round tower's stone is lit at a place across it (-1 its left edge, 1 its right)."""
    return 1 if off < -0.6 else 0 if off < -0.05 else -1 if off < 0.4 else -2 if off < 0.75 else -3


def ashlar(art: Art, x0, y0, x1, y1, ramp, r, course: int = 6, shift=None, flat: int = 0):
    """Dressed stone in even courses, each block its own shade, the joint dark, the top of
    each block lit. `shift(x)` adds to a block's shade at a column (a round tower's light)."""
    x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
    for row, y in enumerate(range(y0, y1, course)):
        x = x0 - (row % 2) * 6
        while x < x1:
            length = r.randint(9, 14)
            base = r.choice([4, 5, 5, 5, 6])
            for yy in range(y, min(y + course, y1)):
                for xx in range(max(x, x0), min(x + length, x1)):
                    joint = yy == y + course - 1 or xx == x + length - 1
                    k = base + flat + (shift(xx) if shift else 0)
                    k = k - 2 if joint else k + 1 if yy == y else k
                    art.put(xx, yy, tone(ramp, k))
            x += length


def shingles(art: Art, rows, ramp, cx: float, lit_far: int = 0):
    """A roof's face laid in rows of round-footed tiles, the side toward the sun lighter, a
    bright streak down it, the far side darker. `rows`: (y, x0, x1) of the face at each row."""
    for y, x0, x1 in rows:
        half = max((x1 - x0) / 2, 1)
        for x in range(int(x0), int(x1) + 1):
            off = (x - cx) / half
            band = (y // 4) % 2
            lip = y % 4 == 3
            seam = (x + band * 3) % 6 == 0
            k = 4 + (1 if off < -0.35 else 0 if off < 0.1 else -1 if off < 0.6 else -2) + lit_far
            if -0.5 < off < -0.38:
                k += 1
            k = k - 2 if seam else k - 1 if lip else k
            art.put(x, y, tone(ramp, k))


def lit_window(art: Art, cx, top, wide, tall, frame=None, cool: bool = False):
    """A window with a round head and a light behind it (or, `cool`, glass in a lattice of
    lead), in a frame of stone a pixel proud of the wall, with a sill."""
    frame = frame or MARBLE
    half = wide / 2
    for y in range(int(top), int(top + tall)):
        up = top + half - y
        reach = half if up <= 0 else math.sqrt(max(half * half - up * up, 0))
        for x in range(int(math.floor(cx - reach)), int(math.ceil(cx + reach))):
            if cool:
                pane = ((x + y) // 4 + (x - y) // 4) % len(GLASS)
                lead = (x + y) % 4 == 0 or (x - y) % 4 == 0
                art.put(x, y, LEAD if lead else GLASS[pane])
            else:
                d = abs(x + 0.5 - cx) / max(half, 1)
                k = 4 if d < 0.35 else 3 if d < 0.7 else 2
                art.put(x, y, AMBER[max(k - (1 if y > top + tall * 0.7 else 0), 1)])
        for side in (-1, 1):
            art.put(int(cx + side * (reach + 0.5)), y, frame[1])
    if not cool and wide >= 8:
        for y in range(int(top + half * 0.6), int(top + tall)):
            art.put(int(cx), y, AMBER[0])
        for x in range(int(cx - half), int(cx + half)):
            art.put(x, int(top + tall * 0.55), AMBER[0])
    for x in range(int(cx - half - 2), int(cx + half + 2)):
        art.put(x, int(top + tall), frame[6])
        art.put(x, int(top + tall) + 1, frame[2])


def finial(art: Art, cx, foot, tall: int = 3 * T):
    """A rod of gold on a roof's point, a ball at its foot."""
    for y in range(int(foot - tall), int(foot)):
        art.put(int(cx) - 1, y, GOLD[5])
        art.put(int(cx), y, GOLD[3])
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            if dx * dx + dy * dy <= 9:
                art.put(int(cx) + dx, int(foot) - 2 + dy, GOLD[6] if dx + dy < -2 else GOLD[4] if dx + dy < 2 else GOLD[2])


def cone_rows(cx: float, eaves: float, wide: int):
    """The rows of a tower's roof over a tower `wide` tiles across whose eaves' tiles lie on
    the row that ends at y = `eaves`: a flared lip, then two up for one across to a point."""
    rows = []
    half = wide / 2 * T
    for i in range(T):  # the eaves: a cell wider at either end, flaring out to its lip
        y = eaves - 1 - i
        reach = half + T + (T - i) * 0.9
        rows.append((int(y), cx - reach, cx + reach - 1))
    for i in range(wide * T):  # two up for one across
        y = eaves - T - 1 - i
        reach = half - i / 2 + 2
        if reach < 0.5:
            break
        rows.append((int(y), cx - reach, cx + reach - 1))
    return rows


def tower(wide: int, tall: int, back: bool = False, hang: bool = False) -> Image.Image:
    """A round tower of a castle of Paradise, `wide` tiles across (an even number) and
    `tall` tiles to the foot of its head: dressed marble lit on its left and in shade on
    its right, a wider foot (or, hung on a wall, a foot that narrows to a point), bands
    of stone round it, windows with light in them, a head on corbels under battlements,
    and a pointed roof of blue tiles with a rod of gold. `back`: one that stands further
    off, paler and plainer. Its floor line is its foot; it is drawn half a tile right of
    its middle, so it stands in the cell left of its middle line."""
    r = rng("heaven tower", wide, tall, back, hang)
    stone = MARBLE_FAR if back else MARBLE
    blue = AZURE_FAR if back else AZURE
    W = (wide + 5) * T
    roof_tiles_tall = wide + 1
    H = (tall + 2 + roof_tiles_tall + 4) * T
    art = Art(W, H)
    cx = W / 2 + T / 2
    half = wide * T / 2
    foot = H
    top = foot - tall * T  # the top of the shaft

    def shift(x):
        return round_shift((x + 0.5 - cx) / half)

    ashlar(art, cx - half, top, cx + half, foot, stone, r, shift=shift)
    if hang:
        # Hung on a wall: its foot narrows to a point in steps of stone.
        for i in range(2 * T):
            y = foot - 1 - i
            reach = half * (i / (2 * T)) ** 0.7
            for x in range(int(cx - half), int(cx + half)):
                if abs(x + 0.5 - cx) > reach:
                    art.a[y, x] = 0
    else:
        # A wider foot: two courses standing proud, a lit moulding over them.
        ashlar(art, cx - half - 5, foot - 2 * T, cx + half + 5, foot, stone, r, shift=shift, flat=0)
        for x in range(int(cx - half - 6), int(cx + half + 6)):
            art.put(x, foot - 2 * T - 1, tone(stone, 6 + shift(x)))
            art.put(x, foot - 2 * T, tone(stone, 2 + shift(x)))
    # Bands of stone round it.
    for y in range(int(top + 5 * T), int(foot - 3 * T), 7 * T):
        for x in range(int(cx - half - 1), int(cx + half + 1)):
            art.put(x, y - 4 * T, tone(stone, 6 + shift(x)))
            art.put(x, y - 4 * T + 1, tone(stone, 5 + shift(x)))
            art.put(x, y - 4 * T + 2, tone(stone, 2 + shift(x)))
    # Windows, where the map's own lit cells are: every seven rows from four under the top.
    for row in range(4, tall - 4, 7):
        y = top + row * T - T // 2
        if wide >= 6 and not back:
            for dx in (-6, 6):
                lit_window(art, cx + dx, y, 8, 30, stone)
        else:
            lit_window(art, cx, y, 8 if not back else 6, 28 if not back else 22, stone)
    # The head: corbels with shadow between them, a walk a cell wider, battlements.
    head_top = top - 2 * T
    for x in range(int(cx - half - T), int(cx + half + T)):
        off = (x + 0.5 - cx) / (half + T)
        sh = round_shift(off)
        for y in range(int(head_top), int(top)):
            k = 4 + sh + (1 if y == head_top else 0)
            art.put(x, y, tone(stone, k))
        # Corbels: every eight pixels a bracket, dark between them.
        for y in range(int(top), int(top) + 6):
            proud = (x - int(cx - half - T)) % 8 < 4
            if proud and abs(x + 0.5 - cx) < half + T - (y - top) * 1.6:
                art.put(x, y, tone(stone, 3 + sh))
            elif abs(x + 0.5 - cx) < half:
                art.put(x, y, tone(stone, 1))
    for x in range(int(cx - half - T), int(cx + half + T)):
        # Joints and a line of shadow under the walk's lip.
        if (x - int(cx - half - T)) % 10 == 9:
            for y in range(int(head_top) + 2, int(top)):
                art.put(x, y, tone(stone, 2 + round_shift((x + 0.5 - cx) / (half + T))))
        art.put(x, int(top) - 1, tone(stone, 2))
    # The shadow the head casts on the shaft.
    for y in range(int(top) + 6, int(top) + 12):
        for x in range(int(cx - half), int(cx + half)):
            if art.has(x, y) and (x + y) % 2 == 0 or y < top + 9:
                art.put(x, y, tone(stone, 2 + shift(x)))
    # The roof.
    eaves = head_top
    rows = cone_rows(cx, eaves, wide)
    shingles(art, rows, blue, cx)
    lip = min(rows, key=lambda row: -row[0])
    for x in range(int(lip[1]), int(lip[2]) + 1):
        art.put(x, lip[0], GOLD[4] if x < cx else GOLD[2])
        art.put(x, lip[0] - 1, GOLD[6] if x < cx else GOLD[3])
    apex = min(row[0] for row in rows)
    finial(art, cx, apex + 2)
    art.outline(stone[0] if not back else stone[1])
    return art.image()


def roof_over(wide: int) -> Image.Image:
    """The tiles of a great roof, drawn over the map's own plain ones: a roof at 45 degrees
    over `wide` tiles, laid in rows of blue tiles, its side from the sun darker, a gabled
    window with a light in it on either face, a crest of gold along its eaves, a ball at
    its point. Its floor line is its point."""
    W = (wide + 2 + (wide + 1) % 2) * T
    H = ((wide + 1) // 2 + 4) * T
    art = Art(W, H)
    cx = W / 2 + (T / 2 if wide % 2 == 0 else 0)  # over an even number of tiles it stands in the cell left of its middle
    rows = []
    for i in range(H - 3 * T - 4):
        y = H - 1 - i
        reach = wide * T / 2 + T - i
        if reach < 0.5:
            break
        rows.append((y, cx - reach, cx + reach - 1))
    shingles(art, rows, AZURE, cx)
    # A gabled window on either face.
    for side in (-1, 1):
        wx = cx + side * wide * T * 0.22
        wy = H - int(wide * T * 0.16) - 30
        for i in range(22):
            reach = i * 0.75
            for x in range(int(wx - reach), int(wx + reach) + 1):
                art.put(x, wy - 14 + i, tone(AZURE, 6 if x < wx else 3))
        for y in range(int(wy + 8), int(wy + 30)):
            for x in range(int(wx - 11), int(wx + 11)):
                art.put(x, y, tone(MARBLE, 5 if x < wx - 6 else 4 if x < wx + 6 else 3))
        lit_window(art, wx, wy + 11, 10, 15, MARBLE)
    for x in range(W):
        if art.has(x, H - 1):
            art.put(x, H - 1, GOLD[2])
            art.put(x, H - 2, GOLD[5] if x < cx else GOLD[3])
            if x % 6 == 0:
                art.put(x, H - 3, GOLD[6])
                art.put(x, H - 4, GOLD[5])
    finial(art, cx, min(row[0] for row in rows) + 2)
    art.outline(AZURE[0])
    return art.image()


# --- The hall --------------------------------------------------------------------------


def column(art: Art, cx, top, foot, wide: float, stone=None):
    """A column of bright marble: a fluted shaft lit on its left, a base on a block, a
    capital of leaves under a slab."""
    stone = stone or MARBLE
    half = wide / 2
    for y in range(int(top + 12), int(foot - 10)):
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            flute = int((off + 1) * wide / 2.67) % 3
            k = 5 + round_shift(off) + (1 if flute == 0 else -1 if flute == 2 else 0)
            art.put(x, y, tone(stone, k))
    for i, (dy, grow, k) in enumerate(((0, 5, 4), (3, 3, 6), (5, 4, 3), (8, 2, 5))):
        for y in range(int(foot - 10 + dy), int(foot - 10 + dy + (3 if i < 3 else 2))):
            for x in range(int(cx - half - grow), int(cx + half + grow)):
                art.put(x, y, tone(stone, k + round_shift((x + 0.5 - cx) / (half + grow))))
    # The capital: a slab, a bell of leaves under it.
    for y in range(int(top), int(top + 4)):
        for x in range(int(cx - half - 6), int(cx + half + 6)):
            art.put(x, y, tone(stone, (6 if y == top else 5) + round_shift((x + 0.5 - cx) / (half + 6))))
    for i in range(8):
        grow = 5 - i * 0.6
        for x in range(int(cx - half - grow), int(cx + half + grow)):
            leaf = (x + i // 2) % 4 == 0
            art.put(x, int(top + 4 + i), tone(stone, (3 if leaf else 5) + round_shift((x + 0.5 - cx) / (half + grow))))
    for x in range(int(cx - half - 4), int(cx + half + 4)):
        art.put(x, int(top + 4), GOLD[4])


def pillar(rows: int, wide: int = 2) -> Image.Image:
    """A column standing by itself, `rows` tiles high."""
    W, H = (wide + 2) * T, rows * T
    art = Art(W, H)
    column(art, W / 2 + (T / 2 if wide % 2 == 0 else 0), 0, H, wide * T * 0.62)
    art.outline(MARBLE[0])
    return art.image()


def banner(art: Art, cx, top, wide: int, tall: int, r):
    """A banner of blue cloth on a rod of gold, the wings of the host on it, its foot cut to two points."""
    for x in range(int(cx - wide / 2 - 2), int(cx + wide / 2 + 2)):
        art.put(x, int(top), GOLD[5])
        art.put(x, int(top) + 1, GOLD[2])
    for i in range(tall):
        y = int(top) + 2 + i
        for x in range(int(cx - wide / 2), int(cx + wide / 2)):
            off = (x + 0.5 - cx) / (wide / 2)
            notch = i > tall - 7 and abs(off) < (i - (tall - 7)) / 7
            if notch:
                continue
            fold = math.sin(off * 4.2 + 0.6)
            k = 4 + (1 if fold > 0.5 else -1 if fold < -0.4 else 0)
            edge = abs(off) > 0.82
            art.put(x, y, GOLD[4] if edge else tone(CLOTH, k))
    # The wings, in gold.
    wy = int(top) + tall // 3
    for dx in range(1, wide // 2 - 2):
        for dy in range(max(1, 5 - dx)):
            art.put(int(cx) - dx, wy - dx // 2 + dy, GOLD[5])
            art.put(int(cx) + dx - 1, wy - dx // 2 + dy, GOLD[4])
    for dy in range(-1, 5):
        art.put(int(cx) - 1, wy + dy, GOLD[6])
        art.put(int(cx), wy + dy, GOLD[4])


def glass_window(art: Art, cx, top, foot, wide: int, r, warm: bool = True):
    """A tall window with a pointed head: panes of coloured glass in a lattice of lead, a
    stone frame with a mullion up its middle and a round light in its head."""
    half = wide / 2
    for y in range(int(top), int(foot)):
        up = top + wide * 0.9 - y
        reach = half if up <= 0 else half * math.sqrt(max(1 - (up / (wide * 0.9)) ** 2, 0))
        for x in range(int(math.floor(cx - reach)), int(math.ceil(cx + reach))):
            lead = (x + y) % 6 == 0 or (x - y) % 6 == 0
            pane = ((x + y) // 6 * 3 + (x - y) // 6 * 5) % 7
            if warm:
                colour = AMBER[1 + pane % 4] if pane % 3 else GOLD[5]
                depth = abs(x + 0.5 - cx) / max(half, 1)
                if depth < 0.3 and pane % 3:
                    colour = AMBER[4]
            else:
                colour = GLASS[pane % len(GLASS)]
            art.put(x, y, LEAD if lead else colour)
        for side in (-1, 1):
            for k in range(2):
                art.put(int(cx + side * (reach + 0.5 + k)), y, MARBLE[5 - k * 2])
    for y in range(int(top + wide * 0.7), int(foot)):
        art.put(int(cx), y, MARBLE[5])
        art.put(int(cx) + 1, y, MARBLE[2])
    for x in range(int(cx - half - 3), int(cx + half + 3)):
        art.put(x, int(foot), MARBLE[6])
        art.put(x, int(foot) + 1, MARBLE[4])
        art.put(x, int(foot) + 2, MARBLE[2])


def wall(art: Art, x0, y0, x1, y1, r):
    """The wall at the back of a hall: marble out of the sun, darker toward its top."""
    span = max(y1 - y0, 1)
    ashlar(art, x0, y0, x1, y1, SHADOWED, r, course=8, shift=lambda x: 0)
    for y in range(int(y0), int(y0 + span * 0.35)):
        for x in range(int(x0), int(x1)):
            if (x + y) % 2 == 0 and art.has(x, y):
                art.put(x, y, SHADOWED[1])


def bay(rows: int, mirrored: bool = False) -> Image.Image:
    """One bay of a hall's arcade, nine tiles wide and `rows` tiles high: the wall at the
    back with a tall window of warm glass in it, and in front a column at the bay's west
    end with a banner on it, half of a round arch springing from it either way, and a
    band with a line of gold over all. Stood side by side they are an arcade."""
    r = rng("heaven bay", rows)
    W, H = 9 * T, rows * T
    art = Art(W, H)
    wall(art, 0, 0, W, H, r)
    opening = 2 * T + 3.5 * T  # the middle of the bay's opening
    glass_window(art, opening, H - 15 * T if rows >= 20 else T * 1.5, H - 3 * T, 30 if rows >= 20 else 22, r)
    # The band over the arcade, and the arches under it.
    band = T
    spring = (5 if rows >= 20 else 3) * T  # the row the arches spring from, from the top
    for y in range(band + 2, spring + 2):
        for x in range(W):
            # The opening's arch is a half round of radius 3.5 tiles about its springing.
            d = math.hypot(x + 0.5 - opening, y - spring)
            if d > 3.5 * T:
                # The wall over the arch: stone in courses, a joint every dozen pixels.
                joint = y % 8 == 7 or (x + (y // 8 % 2) * 6) % 12 == 11
                art.put(x, y, tone(MARBLE, 3 if joint else 6 if y % 8 == 0 else 5))
            elif d > 3.5 * T - 5:
                # The arch's ring of stones, lit from below by the glass.
                ang = math.atan2(y - spring, x + 0.5 - opening)
                art.put(x, y, tone(MARBLE, 6 if int(ang * 9) % 2 else 4) if d > 3.5 * T - 4 else MARBLE[2])
    for x in range(W):
        for y in range(0, band + 2):
            art.put(x, y, tone(MARBLE, 6 if y == 0 else 5 if y < band - 3 else 3 if y < band else 4))
        art.put(x, band - 4, GOLD[5])
        art.put(x, band - 3, GOLD[2])
        if x % 8 < 4:
            art.put(x, band + 1, MARBLE[2])
    for y in range(int(1.5 * T) + 2, 4 * T):  # the chain the bay's lanterns hang on
        art.put(int(opening), y, GOLD[5] if y % 3 else GOLD[2])
    column(art, T, spring, H, 1.25 * T)
    banner(art, T, spring + 22, 16, 76 if rows >= 20 else 44, r)
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if mirrored else image


def bay_end(rows: int) -> Image.Image:
    """Where a hall's arcade ends toward the high seat, five tiles wide: its last column
    with a banner on it, and over the way through to the seat a wall of cut stone under
    the same band."""
    r = rng("heaven bay end", rows)
    W, H = 5 * T, rows * T
    art = Art(W, H)
    wall(art, 0, 0, W, H, r)
    band = T
    spring = (5 if rows >= 20 else 3) * T
    for y in range(band + 2, spring + 2):
        for x in range(W):
            art.put(x, y, tone(MARBLE, stones(x, y)))
    for x in range(W):  # the lintel's underside, in its own shadow
        art.put(x, spring + 2, MARBLE[3])
        art.put(x, spring + 3, MARBLE[1])
        for y in range(0, band + 2):
            art.put(x, y, tone(MARBLE, 6 if y == 0 else 5 if y < band - 3 else 3 if y < band else 4))
        art.put(x, band - 4, GOLD[5])
        art.put(x, band - 3, GOLD[2])
        if x % 8 < 4:
            art.put(x, band + 1, MARBLE[2])
    column(art, T, spring, H, 1.25 * T)
    banner(art, T, spring + 22, 16, 76 if rows >= 20 else 44, r)
    return art.image()


def hall_side(rows: int, mirrored: bool = False) -> Image.Image:
    """One side of the citadel's hall, 24 tiles of it from its end wall to the way through
    to the high seat: a tile of wall, two bays of the arcade, and the arcade's end. The
    east side is the west one turned about. It stands in the cell left of its middle."""
    r = rng("heaven hall side", rows)
    side = Image.new("RGBA", (24 * T, rows * T), (0, 0, 0, 0))
    strip = Art(T, rows * T)
    wall(strip, 0, 0, T, rows * T, r)
    for y in range(0, T + 2):
        for x in range(T):
            strip.put(x, y, tone(MARBLE, 6 if y == 0 else 5 if y < T - 3 else 3 if y < T else 4))
    for x in range(T):
        strip.put(x, T - 4, GOLD[5])
        strip.put(x, T - 3, GOLD[2])
    side.paste(strip.image(), (0, 0))
    one = bay(rows)
    side.paste(one, (T, 0))
    side.paste(one, (10 * T, 0))
    side.paste(bay_end(rows), (19 * T, 0))
    if mirrored:
        side = side.transpose(Image.FLIP_LEFT_RIGHT)
    image = Image.new("RGBA", (25 * T, rows * T), (0, 0, 0, 0))
    image.paste(side, (T, 0))
    return image


def high_seat(rows: int) -> Image.Image:
    """The wall behind the high seat, 25 tiles wide and `rows` tiles high, from the hall's
    floor up through the great tower: marble out of the sun between pilasters, a throne
    of gold with a high back, over it the wings of the host and a ring of light, a tall
    window of cool glass at either hand, and up the tower three ranks of small windows
    with light in them."""
    r = rng("heaven high seat", rows)
    W, H = 25 * T, rows * T
    art = Art(W, H)
    cx = W / 2
    wall(art, 0, 0, W, H, r)
    for px in (2 * T, W - 2 * T):
        for y in range(0, H):
            for x in range(int(px - 7), int(px + 7)):
                art.put(x, y, tone(SHADOWED, 5 if x < px - 3 else 4 if x < px + 3 else 3))
    # The ring of light and the wings over the seat.
    wy = H - 13 * T
    for a in range(0, 3600):
        ang = a / 3600 * 2 * math.pi
        for rad, k in ((58, 4), (59, 6), (60, 5), (61, 2)):
            art.put(cx + math.cos(ang) * rad, wy + math.sin(ang) * rad, GOLD[k])
    for ray in range(24):
        ang = ray / 24 * 2 * math.pi
        for rad in range(64, 92 if ray % 2 else 80):
            art.put(cx + math.cos(ang) * rad, wy + math.sin(ang) * rad, GOLD[4 if rad % 3 else 6])
    for side in (-1, 1):
        for feather in range(11):
            # A wing: feathers fanned up and out, the longest at its tip.
            ang = math.radians(8 + feather * 6.5)
            length = 44 + feather * 9
            root_x, root_y = cx + side * (8 + feather * 3.0), wy + 34 - feather * 2.2
            for i in range(length):
                x = root_x + side * math.cos(ang) * i
                y = root_y - math.sin(ang) * i
                width = 5 if i < length * 0.75 else 3
                for d in range(width):
                    art.put(x, y + d, GOLD[6 if d == 0 else 4 if d < width - 1 else 2])
    for dy in range(-16, 40):
        reach = 9 if dy < 0 else 13 - dy * 0.12
        for dx in range(int(-reach), int(reach) + 1):
            if dy < 0 and dx * dx + (dy + 8) ** 2 > 81:
                continue
            art.put(cx + dx, wy + dy, GOLD[6 if dx < -3 else 5 if dx < 4 else 3])
    # The throne, on the map's own steps of gold (two tiles of them): a high back that ends
    # in a point with a cushion of blue up it, a seat, arms on turned posts, a footstool.
    seat = H - 2 * T
    back_top = seat - 78
    for y in range(int(back_top), int(seat)):
        t = (y - back_top) / 78
        half = 17 if y > back_top + 14 else 5 + (y - back_top) * 0.86
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            cushion = abs(x + 0.5 - cx) < 11 and back_top + 16 < y < seat - 26
            tuft = cushion and (int(x - cx) % 8 == 4) and (y % 8 == 4)
            art.put(x, y, GOLD[5] if tuft else tone(CLOTH, 4 + (1 if off < -0.2 else -1 if off > 0.5 else 0)) if cushion else tone(GOLD, 5 + round_shift(off)))
    for side in (-1, 1):  # the arms, on posts, each with a ball
        for y in range(int(seat - 34), int(seat)):
            for x in range(int(cx + side * 26) - 4, int(cx + side * 26) + 4):
                art.put(x, y, tone(GOLD, 5 + round_shift((x + 0.5 - cx - side * 26) / 4)))
        for x in range(int(cx + min(side * 17, side * 30)), int(cx + max(side * 17, side * 30))):
            for y in range(int(seat - 30), int(seat - 25)):
                art.put(x, y, GOLD[6 if y == seat - 30 else 4 if y < seat - 26 else 2])
        for dy in range(-5, 6):
            for dx in range(-5, 6):
                if dx * dx + dy * dy <= 25:
                    art.put(cx + side * 26 + dx, seat - 38 + dy, GOLD[6] if dx + dy < -3 else GOLD[4] if dx + dy < 3 else GOLD[2])
    for y in range(int(seat - 22), int(seat)):  # the seat and its front
        for x in range(int(cx - 22), int(cx + 22)):
            off = (x + 0.5 - cx) / 22
            k = 6 if y < seat - 19 else 5 + round_shift(off)
            art.put(x, y, tone(CLOTH, 5 if off < 0 else 4) if seat - 19 <= y < seat - 14 and abs(off) < 0.85 else tone(GOLD, k))
    finial(art, cx, back_top + 2, 12)
    # A tall window of cool glass at either hand.
    for side in (-1, 1):
        glass_window(art, cx + side * 8.6 * T, H - 18 * T, H - 4 * T, 34, r, warm=False)
    # Up the tower: ranks of small windows with light in them, a band under each rank.
    for rank, y in enumerate(range(int(H - 24 * T), 3 * T, -7 * T)):
        for x in range(W):
            art.put(x, y + 3 * T, SHADOWED[6])
            art.put(x, y + 3 * T + 1, SHADOWED[5])
            art.put(x, y + 3 * T + 2, SHADOWED[1])
        for k in range(-3, 4):
            if k == 0 and rank == 0:
                continue
            lit_window(art, cx + k * 3 * T, y, 12, 34, SHADOWED)
    return art.image()


def gate() -> Image.Image:
    """The gate between a gatehouse's two towers, six tiles wide: a round arch of cut
    stones, a grate of gold drawn up into it, the wings of the host over it on a wall
    walk under battlements."""
    r = rng("heaven gate")
    W, H = 6 * T, 8 * T
    art = Art(W, H)
    cx, spring = W / 2, H - 3 * T
    ashlar(art, 0, 2 * T, W, spring + 4, MARBLE, r)
    for y in range(int(spring - 3 * T), H):
        for x in range(W):
            d = math.hypot(x + 0.5 - cx, min(y - spring, 0))
            if d < 2.5 * T - 5:
                art.a[y, x] = 0
            elif d < 2.5 * T and y <= spring:
                ang = math.atan2(y - spring, x + 0.5 - cx)
                art.put(x, y, tone(MARBLE, 6 if int(ang * 7) % 2 else 4))
            elif y > spring and abs(x + 0.5 - cx) > 2.5 * T - 5:
                art.put(x, y, tone(MARBLE, 5 if x < cx else 3))
    for x in range(int(cx - 2.5 * T + 6), int(cx + 2.5 * T - 6), 6):  # the grate, drawn up
        for y in range(int(spring - 2.2 * T), int(spring - 0.9 * T)):
            if math.hypot(x + 0.5 - cx, y - spring) < 2.5 * T - 5:
                art.put(x, y, GOLD[5])
                art.put(x + 1, y, GOLD[2])
    for x in range(W):
        for y in range(T, 2 * T):
            art.put(x, y, tone(MARBLE, 6 if y == T else 5))
        if (x // 8) % 2 == 0:
            for y in range(4, T):
                art.put(x, y, tone(MARBLE, 5 if x % 8 else 6))
        art.put(x, 2 * T, MARBLE[2])
    for dx in range(2, 20):
        for dy in range(max(1, 6 - dx // 3)):
            art.put(cx - dx, 2.6 * T - dx // 2 + dy, GOLD[5])
            art.put(cx + dx - 1, 2.6 * T - dx // 2 + dy, GOLD[3])
    for dy in range(-2, 8):
        art.put(cx - 1, 2.6 * T + dy, GOLD[6])
        art.put(cx, 2.6 * T + dy, GOLD[4])
    art.outline(MARBLE[0])
    return art.image()


# --- The Orchard: a round temple, and the host in marble --------------------------------


def rotunda() -> Image.Image:
    """The round temple of the Orchard, 27 tiles wide: six columns in a ring on three
    steps, a band over them, and a dome of gold with ribs, a ring of small windows under
    it, a rod on its top. The sky shows between its columns."""
    r = rng("heaven rotunda")
    W, H = 29 * T, 30 * T
    art = Art(W, H)
    cx = W / 2
    floor = H
    col_top = floor - 12 * T
    # Three steps.
    for i, grow in enumerate((13.5, 12.8, 12.1)):
        for y in range(int(floor - (i + 1) * 5), int(floor - i * 5)):
            for x in range(int(cx - grow * T), int(cx + grow * T)):
                art.put(x, y, tone(MARBLE, (6 if y == floor - (i + 1) * 5 else 4) + round_shift((x + 0.5 - cx) / (grow * T))))
    # The far columns, in shade, then the near ones.
    for off in (-4.2, 4.2):
        column(art, cx + off * T, col_top + 6, floor - 15, 1.0 * T, MARBLE_FAR)
    for off in (-10.5, -7.0, 7.0, 10.5):
        column(art, cx + off * T, col_top, floor - 15, 1.3 * T)
    # The band: an architrave, a frieze with a line of gold, a cornice that stands out.
    for y in range(int(col_top - 2 * T), int(col_top)):
        for x in range(int(cx - 12.6 * T), int(cx + 12.6 * T)):
            sh = round_shift((x + 0.5 - cx) / (12.6 * T))
            row = y - (col_top - 2 * T)
            k = 6 if row < 2 else 5 if row < 12 else 3 if row < 14 else 5 if row < 26 else 2
            art.put(x, y, tone(MARBLE, k + sh))
            if 15 < row < 24 and (x // 4) % 3 == 0:
                art.put(x, y, tone(MARBLE, 3 + sh))
        art.put(int(cx - 12.6 * T), y, MARBLE[1])
    for x in range(int(cx - 12.6 * T), int(cx + 12.6 * T)):
        art.put(x, int(col_top - T), GOLD[5])
    # The drum with its windows, and the dome.
    drum_top = col_top - 4 * T
    for y in range(int(drum_top), int(col_top - 2 * T)):
        for x in range(int(cx - 11 * T), int(cx + 11 * T)):
            art.put(x, y, tone(MARBLE, 5 + round_shift((x + 0.5 - cx) / (11 * T))))
    for k in range(-4, 5):
        lit_window(art, cx + k * 2.3 * T, drum_top + 6, 8, 18, MARBLE)
    radius = 11 * T
    for y in range(int(drum_top - radius), int(drum_top)):
        reach = math.sqrt(max(radius * radius - (drum_top - y) ** 2, 0))
        for x in range(int(cx - reach), int(cx + reach)):
            off = (x + 0.5 - cx) / max(reach, 1)
            # Lit like a ball: brightest up and to the left.
            lightness = -off * 0.55 + (drum_top - y) / radius * 0.5
            k = 3 + round(lightness * 3.2)
            rib = abs(((x - cx) / max(reach, 1)) * 5 % 1 - 0.5) > 0.44
            art.put(x, y, tone(GOLD, k - (2 if rib else 0)))
    for x in range(int(cx - radius - 2), int(cx + radius + 2)):
        art.put(x, int(drum_top), GOLD[6 if x < cx else 3])
        art.put(x, int(drum_top) + 1, GOLD[2])
    finial(art, cx, drum_top - radius + 3, 4 * T)
    art.outline(MARBLE[0])
    return art.image()


def angel(variant: int = 0) -> Image.Image:
    """One of the host in white marble on a block, seven times the hero's height: a long
    robe in folds, hands on a sword point down before it, a bowed head, great wings
    raised, a ring of gold over its head. `variant` 1 faces the other way."""
    r = rng("heaven angel")
    W, H = 12 * T, 17 * T
    art = Art(W, H)
    cx = W / 2
    # The block it stands on.
    block_top = H - 3 * T
    ashlar(art, cx - 2.6 * T, block_top, cx + 2.6 * T, H, MARBLE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (2.6 * T)))
    for x in range(int(cx - 2.9 * T), int(cx + 2.9 * T)):
        art.put(x, int(block_top), MARBLE[6])
        art.put(x, int(block_top) + 1, MARBLE[5])
        art.put(x, int(block_top) + 2, MARBLE[2])
    # The wings, behind it: each rises from the shoulder in an arch well over the head and
    # comes down outside it in three ranks of feathers, the longest reaching the knees.
    shoulder = block_top - 8.6 * T
    for side in (-1, 1):
        for rank, (reach, drop, shade_k) in enumerate(((1.0, 1.0, 0), (0.78, 0.7, 1), (0.56, 0.42, 1))):
            for feather in range(15):
                t = feather / 14
                # Where the feather leaves the wing's arm: along an arch out from the shoulder.
                root_x = cx + side * (9 + t * 58 * reach)
                root_y = shoulder - 6 - math.sin(t * math.pi * 0.62) * 62 * reach + rank * 9
                length = (34 + math.sin(t * math.pi * 0.85) * 84) * drop
                lean = 0.08 + t * 0.42  # the outer ones lean out
                for i in range(int(length)):
                    x = root_x + side * i * lean
                    y = root_y + i
                    width = 6 if i < length - 6 else max(1, int(length - i))
                    for d in range(width):
                        k = 6 if d == (0 if side < 0 else width - 1) else 5 if i % 9 else 4
                        art.put(x + d - width // 2, y, tone(MARBLE, k - shade_k - (1 if side > 0 else 0)))
        # The wing's arm: the bone the feathers hang from.
        for a in range(0, 100):
            t = a / 99
            x = cx + side * (9 + t * 58)
            y = shoulder - 6 - math.sin(t * math.pi * 0.62) * 62
            for d in range(-3, 4):
                art.put(x, y + d, tone(MARBLE, 6 if d < 0 else 4))
    # The robe: a bell of folds from the shoulders to the block.
    for y in range(int(shoulder), int(block_top)):
        t = (y - shoulder) / (block_top - shoulder)
        half = 15 + t * 22 + 3 * math.sin(t * 9)
        for x in range(int(cx - half), int(cx + half)):
            off = (x + 0.5 - cx) / half
            fold = math.sin(off * 9 + t * 2.5)
            k = 5 + round_shift(off) + (1 if fold > 0.6 else -1 if fold < -0.5 else 0)
            art.put(x, y, tone(MARBLE, k))
    # Arms down to the hilt, the sword point down.
    hilt = shoulder + 3.4 * T
    for side in (-1, 1):
        for i in range(int(3.2 * T)):
            t = i / (3.2 * T)
            x = cx + side * (15 - t * 13)
            y = shoulder + 6 + i
            for d in range(-3, 4):
                art.put(x + d, y, tone(MARBLE, 6 if d < -1 else 5 if d < 2 else 3))
    for y in range(int(hilt), int(block_top - 4)):
        art.put(cx - 1, y, MARBLE[6])
        art.put(cx, y, MARBLE[4])
        art.put(cx + 1, y, MARBLE[2])
    for x in range(int(cx - 9), int(cx + 9)):
        art.put(x, int(hilt), GOLD[5])
        art.put(x, int(hilt) + 1, GOLD[3])
    # The head, bowed, and its hair; the ring over it.
    head_y = shoulder - 11
    for dy in range(-10, 11):
        for dx in range(-9, 10):
            if dx * dx + dy * dy <= 86:
                art.put(cx + dx, head_y + dy, tone(MARBLE, 6 if dx + dy < -5 else 5 if dx < 4 else 3))
    for dx in range(-10, 11):
        for dy in range(-11, -2):
            if dx * dx + (dy + 2) ** 2 <= 100 and dy < -4 + abs(dx) * 0.3:
                art.put(cx + dx, head_y + dy, tone(MARBLE, 4 if dx < 2 else 2))
    for a in range(720):
        ang = a / 720 * 2 * math.pi
        for rad, k in ((13, 6), (14, 4)):
            art.put(cx + math.cos(ang) * rad, head_y - 19 + math.sin(ang) * rad * 0.36, GOLD[k])
    art.outline(MARBLE[0])
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if variant else image


# --- What stands about a castle: gates, bridges, battlements, rooms ---------------------------


def host_wings(art: Art, cx, wy, size: float = 1.0, feathers: int = 9):
    """The wings of the host in gold: feathers fanned up and out from a middle, the longest at the tip."""
    for side in (-1, 1):
        for feather in range(feathers):
            ang = math.radians(8 + feather * 58 / max(feathers - 1, 1))
            length = (16 + feather * 5) * size
            root_x, root_y = cx + side * (3 + feather * 1.6) * size, wy + (12 - feather * 1.1) * size
            for i in range(int(length)):
                x = root_x + side * math.cos(ang) * i
                y = root_y - math.sin(ang) * i
                width = 3 if i < length * 0.75 else 2
                for d in range(width):
                    art.put(x, y + d, GOLD[6 if d == 0 else 4 if d < width - 1 else 2])
    for dy in range(int(-4 * size), int(14 * size)):
        art.put(cx - 1, wy + dy, GOLD[6])
        art.put(cx, wy + dy, GOLD[4])


def stones(x: int, y: int, light: int = 0) -> int:
    """The shade of a wall of cut stone at a pixel: courses eight high, a joint under and after each block."""
    joint = y % 8 == 7 or (x + (y // 8 % 2) * 6) % 12 == 11
    return (3 if joint else 6 if y % 8 == 0 else 5) + light


def portal(apart: int, rows: int, wide: int = 2) -> Image.Image:
    """A gate that stands free: two columns `apart` tiles from middle to middle and `rows`
    tiles high, a round arch of cut stones sprung from them, a band with a line of gold
    and the wings of the host over it, a rod on its top."""
    col = wide * T * 0.62
    radius = apart * T / 2
    inner, outer = radius - col / 2, radius + col / 2 + 2
    W = (apart + wide + 3) * T
    H = int(rows * T + outer + 1.5 * T) + 3 * T + 4
    art = Art(W, H)
    cx, spring = W / 2, H - rows * T
    top = int(spring - outer - 1.5 * T)
    for y in range(top + int(1.5 * T), int(spring)):
        for x in range(int(cx - outer), int(cx + outer)):
            d = math.hypot(x + 0.5 - cx, y - spring)
            if d < inner:
                continue
            if d < inner + 6:
                ang = math.atan2(y - spring, x + 0.5 - cx)
                art.put(x, y, MARBLE[2] if d < inner + 1 else tone(MARBLE, 6 if int(ang * (apart + 2)) % 2 else 4))
            else:
                art.put(x, y, tone(MARBLE, stones(x, y, round_shift((x + 0.5 - cx) / outer))))
    for y in range(top, top + int(1.5 * T)):
        row = y - top
        for x in range(int(cx - outer - 5), int(cx + outer + 5)):
            k = 6 if row < 2 else 2 if row > 1.5 * T - 3 else 5
            art.put(x, y, GOLD[5] if row == 5 else GOLD[2] if row == 6 else tone(MARBLE, k + round_shift((x + 0.5 - cx) / (outer + 5))))
    host_wings(art, cx, top + 8, 0.55 if apart < 6 else 0.8, 6)
    for side in (-1, 1):
        column(art, cx + side * radius, spring, H, col)
    finial(art, cx, top + 1)
    art.outline(MARBLE[0])
    return art.image()


def bridge(wide: int) -> Image.Image:
    """A bridge high between two towers, `wide` tiles long: a walk under battlements, on a
    round arch of cut stones with a boss of gold at its crown."""
    r = rng("heaven bridge", wide)
    rise = wide // 2
    W, H = (wide + 2) * T, (4 + rise) * T
    art = Art(W, H)
    cx = W / 2 + (T / 2 if wide % 2 == 0 else 0)
    x0, x1 = int(cx - wide * T / 2), int(cx + wide * T / 2)
    radius = wide * T / 2 - 3
    ashlar(art, x0, T, x1, H, MARBLE, r, shift=lambda x: -1)
    for y in range(T, H):
        for x in range(x0, x1):
            d = math.hypot(x + 0.5 - cx, y - H)
            if d < radius:
                art.a[y, x] = 0
            elif d < radius + 5:
                ang = math.atan2(y - H, x + 0.5 - cx)
                art.put(x, y, MARBLE[1] if d < radius + 1 else tone(MARBLE, 5 if int(ang * (wide + 3)) % 2 else 3))
    for x in range(x0 - 2, x1 + 2):  # the walk's lip, and its shadow
        art.put(x, T, MARBLE[6])
        art.put(x, T + 1, MARBLE[5])
        art.put(x, T + 2, MARBLE[5])
        art.put(x, T + 3, MARBLE[2])
        if (x - x0) % 6 < 3:  # brackets under the lip
            art.put(x, T + 4, MARBLE[4])
            art.put(x, T + 5, MARBLE[3])
    for x in range(x0, x1):  # merlons
        k = (x - x0) % 8
        if (x - x0) // 8 % 2 == 0:
            for y in range(5, T):
                art.put(x, y, tone(MARBLE, 6 if y == 5 or k == 0 else 3 if k == 7 else 5))
        else:
            for y in range(T - 4, T):
                art.put(x, y, tone(MARBLE, 5 if y == T - 4 else 4))
    for dy in range(-3, 4):  # the boss at the crown
        for dx in range(-3, 4):
            if dx * dx + dy * dy <= 9:
                art.put(cx + dx, H - radius - 4 + dy, GOLD[6] if dx + dy < -2 else GOLD[4] if dx + dy < 2 else GOLD[2])
    art.outline(MARBLE[0])
    return art.image()


def parapet(cols: int) -> Image.Image:
    """The battlements along a wall walk, `cols` tiles long and a tile high: merlons of cut
    stone, each with a lit top and a side in shade, a low wall between them."""
    r = rng("heaven parapet", cols)
    W, H = cols * T, T
    art = Art(W, H)
    block = 5
    for x in range(W):
        k = x % 16
        if k < 9:
            block = r.choice([5, 5, 6]) if k == 0 else block  # each merlon its own shade
            for y in range(3, H):
                edge = 6 if y == 3 else 6 if k == 0 else 3 if k == 8 else block if y != 10 else 3
                art.put(x, y, tone(MARBLE, edge))
        else:
            for y in range(H - 6, H):
                art.put(x, y, tone(MARBLE, 5 if y == H - 6 else 3 if y == H - 1 else 4))
    art.outline(MARBLE[0])
    return art.image()


def span() -> Image.Image:
    """The bridge of marble from the Garden east, eighteen tiles long: a rail of balusters
    over its deck (the map's own tiles, which this is drawn behind), and under the deck two
    round arches of cut stones on three piers that end in the air, the middle one longest."""
    r = rng("heaven span")
    W, H = 20 * T, 11 * T
    art = Art(W, H)
    x0 = T + T // 2  # the deck's west end (an even number of tiles: it stands in the cell left of its middle)
    under = 2 * T  # the deck's underside
    spring = under + 4 * T
    for x in range(x0, x0 + 18 * T):
        rel = (x - x0) / T
        for y in range(under, spring + 1):
            # The wall over the arches, the arches left open.
            open_ = any(math.hypot(rel - mid, (y - spring) / T) < 3.5 for mid in (4.5, 13.5))
            near = any(3.5 <= math.hypot(rel - mid, (y - spring) / T) < 3.5 + 5 / T for mid in (4.5, 13.5))
            if open_:
                continue
            if near:
                mid = 4.5 if rel < 9 else 13.5
                ang = math.atan2(y - spring, rel - mid)
                art.put(x, y, tone(MARBLE, 5 if int(ang * 10) % 2 else 3))
            else:
                art.put(x, y, tone(MARBLE, stones(x, y, -1)))
    for west, east, rows in ((0, 1, 6), (8, 10, 9), (17, 18, 6)):
        px0, px1 = x0 + west * T, x0 + east * T
        pcx, half = (px0 + px1) / 2, (px1 - px0) / 2
        foot = under + rows * T
        ashlar(art, px0, spring - T, px1, foot, MARBLE, r, shift=lambda x, pcx=pcx, half=half: round_shift((x + 0.5 - pcx) / half))
        for i in range(T):  # its foot narrows to a point
            y = foot - 1 - i
            reach = half * (i / T) ** 0.7
            for x in range(px0, px1):
                if abs(x + 0.5 - pcx) > reach:
                    art.a[y, x] = 0
        for x in range(px0 - 2, px1 + 2):  # an impost where the arches spring
            art.put(x, spring - T, MARBLE[6])
            art.put(x, spring - T + 1, MARBLE[4])
            art.put(x, spring - T + 2, MARBLE[2])
    for x in range(x0, x0 + 18 * T):  # the shadow the deck casts
        for y in range(under, under + 3):
            if art.has(x, y):
                art.put(x, y, MARBLE[2 if y < under + 2 else 3])
    # The rail: posts with a cap, a handrail, balusters between.
    for x in range(x0, x0 + 18 * T):
        k = (x - x0) % (3 * T)
        post = k < 5 or x >= x0 + 18 * T - 5
        art.put(x, 4, MARBLE[6])
        art.put(x, 5, MARBLE[5])
        art.put(x, 6, MARBLE[3])
        if post:
            for y in range(1, T):
                art.put(x, y, tone(MARBLE, 6 if k % 5 == 0 or y == 1 else 3 if k == 4 else 5))
        elif (x - x0) % 5 in (1, 2):
            for y in range(7, T - 2):
                art.put(x, y, MARBLE[5 if (x - x0) % 5 == 1 else 3])
        for y in range(T - 2, T):
            art.put(x, y, MARBLE[5 if y == T - 2 else 4])
    art.outline(MARBLE[0])
    return art.image()


def chamber(name: str, cols: int, rows: int, windows, wings=None) -> Image.Image:
    """The wall at the back of a room, `cols` by `rows` tiles: marble out of the sun in
    courses, a cornice under its ceiling, a rail and a skirting by its floor, and its
    windows, each (column from the west, rows from the top to its head, rows to its sill,
    kind): `sky` an opening the sky shows through, in a frame the sun reaches; `warm`
    and `cool` glass in lead; `lamp` a small window with a light behind it. `wings`: where
    the wings of the host hang in gold (column, rows from the top)."""
    r = rng("heaven chamber", name)
    W, H = cols * T, rows * T
    art = Art(W, H)
    wall(art, 0, 0, W, H, r)
    for x in range(W):
        for y, k in ((0, 6), (1, 5), (2, 5), (3, 2), (4, 3)):
            art.put(x, y, SHADOWED[k] if x % 8 or y > 2 else SHADOWED[3])
        art.put(x, H - 2 * T - 8, SHADOWED[6])
        art.put(x, H - 2 * T - 7, SHADOWED[5])
        art.put(x, H - 2 * T - 6, SHADOWED[1])
        for y in range(H - 6, H):
            art.put(x, y, SHADOWED[6 if y == H - 6 else 4 if x % 24 else 2])
    # Panels between the rail and the skirting.
    for x in range(4, W - 4, 2 * T):
        for y in range(H - 2 * T - 2, H - 9):
            art.put(x, y, SHADOWED[2])
            art.put(x + 1, y, SHADOWED[5])
    for col, head, sill, kind in windows:
        cx, top, foot = col * T + T / 2, head * T + 2, sill * T
        if kind == "lamp":
            lit_window(art, cx, top, 10, foot - top - 4, SHADOWED)
        elif kind == "sky":
            wide = 3 * T - 10
            half = wide / 2
            for y in range(int(top), int(foot)):
                up = top + wide * 0.9 - y
                reach = half if up <= 0 else half * math.sqrt(max(1 - (up / (wide * 0.9)) ** 2, 0))
                for x in range(int(math.floor(cx - reach)), int(math.ceil(cx + reach))):
                    art.a[y, x] = 0
                for side in (-1, 1):  # the frame, and the depth of the wall the sun lights on one side
                    for k, shade in enumerate((6, 5, 3) if side > 0 else (3, 5, 6)):
                        art.put(int(cx + side * (reach + 0.5 + k)), y, MARBLE[shade])
            for x in range(int(cx - half - 4), int(cx + half + 4)):
                art.put(x, int(foot), MARBLE[6])
                art.put(x, int(foot) + 1, MARBLE[5])
                art.put(x, int(foot) + 2, MARBLE[3])
                art.put(x, int(foot) + 3, MARBLE[2])
        else:
            glass_window(art, cx, top, foot, 3 * T - 10, r, warm=kind == "warm")
    if wings:
        host_wings(art, wings[0] * T + T / 2, wings[1] * T, 1.5, 11)
    return art.image()


# --- What there is, and where its floor line lies ------------------------------------------

# Each kind has its own order, so where two overlap which is in front is never left to chance.
ORDER_BACK = -289  # a tower further back: in front of the walls, behind everything else of a castle
ORDER_TREE = -287  # a tree: behind what is built
ORDER_WALL = -284  # the wall at the back of a room: over the walls and the open walls laid there
ORDER_STAND = -283  # what stands before a wall or the sky: columns, gates, bridges, battlements, the host
ORDER_OVER = -99  # a great roof: over the map's own roof tiles, behind the ledges and the hero
ORDER_TOWER = -97  # a near tower: before the roofs and the walls it stands at

BLOSSOMS, GOLDS = 4, 4
# The towers the castles are built of: (tiles across, tiles to the foot of the head, further back, hung on a wall).
TOWERS = [
    (8, 24, False, False), (6, 13, False, False), (6, 16, False, False), (8, 19, False, False),
    (4, 15, False, False), (4, 9, False, True),
    (8, 25, False, False), (6, 11, False, False), (4, 15, False, False), (4, 9, False, False), (6, 14, False, False), (8, 20, False, False),
    (4, 31, True, False), (6, 22, True, False), (4, 27, True, False), (4, 24, True, False),
    (4, 19, True, False), (4, 21, True, False), (4, 15, True, False),
]  # fmt: skip
ROOFS = [29, 21, 14]  # the great roofs: tiles across
PILLARS = [(18, 2), (8, 2), (12, 1)]  # columns standing alone: (tiles high, tiles across)
HALL, SEAT = 22, 41  # the citadel's hall, and the wall behind its high seat up through the tower, in tiles
BRIDGES = [6, 7, 8, 9]  # bridges between towers: tiles long
PARAPETS = [27, 33]  # battlements along the wall walks: tiles long
PORTALS = [(9, 9, 2), (4, 6, 1)]  # gates standing free: (tiles between its columns' middles, tiles high, a column's tiles)
# The House of Trials' rooms: (name, tiles across, tiles high, windows, where the wings hang).
SKY_WINDOWS, WARM_WINDOWS = (9, 31, 45, 69, 79), (9, 27, 46, 65, 75)  # columns from the House's west wall
CHAMBERS = [
    ("lower", 81, 12, [(off - 2, 1, 9, "sky") for off in SKY_WINDOWS], None),
    ("upper", 81, 12, [(off - 2, 1, 9, "warm") for off in WARM_WINDOWS], None),
    ("keep", 15, 16, [(2, 10, 14, "lamp"), (12, 10, 14, "lamp")], (7, 4)),
]


def tower_key(wide: int, tall: int, back: bool = False, hang: bool = False) -> str:
    return f"heaven_tower_{wide}x{tall}" + ("_back" if back else "") + ("_hung" if hang else "")


def tower_rise(wide: int) -> int:
    """How many tiles a tower's head, roof and rod stand over the top of its shaft."""
    return 2 + wide + 1 + 3


def _plan():
    plan = []

    def add(key, paint, order=ORDER_WALL, sink=0):
        plan.append((key, paint, order, True, sink))

    for v in range(BLOSSOMS):
        add(f"heaven_blossom_{v}", lambda v=v: tree(v, BLOSSOM), ORDER_TREE, sink=T)
    for v in range(GOLDS):
        add(f"heaven_gold_{v}", lambda v=v: tree(v, GOLDLEAF), ORDER_TREE, sink=T)
    seen = set()
    for wide, tall, back, hang in TOWERS:
        key = tower_key(wide, tall, back, hang)
        if key not in seen:
            seen.add(key)
            add(key, lambda a=(wide, tall, back, hang): tower(*a), ORDER_BACK if back else ORDER_TOWER)
    for wide in ROOFS:
        # A roof's floor line is its point: it is stood in the cell of air over its point.
        add(f"heaven_roof_{wide}", lambda wide=wide: roof_over(wide), ORDER_OVER, sink=((wide + 1) // 2) * T)
    for rows, wide in PILLARS:
        add(f"heaven_pillar_{rows}x{wide}", lambda a=(rows, wide): pillar(*a), ORDER_STAND)
    add("heaven_hall_west", lambda: hall_side(HALL))
    add("heaven_hall_east", lambda: hall_side(HALL, True))
    # The wall behind the high seat stands on the hall's floor, two steps of gold under its mark.
    add("heaven_high_seat", lambda: high_seat(SEAT), sink=2 * T)
    add("heaven_gate", gate, ORDER_STAND)
    add("heaven_rotunda", rotunda, ORDER_STAND)
    add("heaven_angel_0", lambda: angel(0), ORDER_STAND)
    add("heaven_angel_1", lambda: angel(1), ORDER_STAND)
    for wide in BRIDGES:
        add(f"heaven_bridge_{wide}", lambda wide=wide: bridge(wide), ORDER_STAND)
    for cols in PARAPETS:
        add(f"heaven_parapet_{cols}", lambda cols=cols: parapet(cols), ORDER_STAND)
    for apart, rows, wide in PORTALS:
        add(f"heaven_portal_{apart}x{rows}", lambda a=(apart, rows, wide): portal(*a), ORDER_STAND)
    # The Garden's bridge is marked on its deck: a tile of deck and nine of arches and piers hang under the mark.
    add("heaven_span", span, ORDER_STAND, sink=10 * T)
    for name, cols, rows, windows, wings in CHAMBERS:
        add(f"heaven_room_{name}", lambda a=(name, cols, rows, windows, wings): chamber(*a))
    return plan


PLAN = _plan()
PICTURE_KEYS = [key for key, *_ in PLAN]
ENTRIES = [
    {"key": key, "kind": "picture", "scroll": 1.0, "scrollUp": 1.0, "rise": 0, "drift": 0.0, "order": order, "lit": lit, "tinted": False}
    for key, _, order, lit, _ in PLAN
]
SINK_OF = {key: sink for key, *_, sink in PLAN}
_DRAWN: dict[str, Image.Image] = {}


def pictures() -> dict[str, Image.Image]:
    """Every picture of Paradise, drawn (once), by key."""
    if not _DRAWN:
        for key, paint, *_ in PLAN:
            _DRAWN[key] = paint()
    return dict(_DRAWN)


def pivot_of(key: str) -> tuple[float, float]:
    """A picture's pivot (x, y from the bottom, 0 .. 1): the middle of its floor line."""
    return (0.5, round(SINK_OF[key] / pictures()[key].height, 6))


def size_of(key: str) -> tuple[int, int]:
    return pictures()[key].size
