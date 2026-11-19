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
from PIL import Image, ImageFilter

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_style
from tv2_earth import Art, bundle, branch, lilies, noise, ramp_of, trunk

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
VINE = ramp_of("143a24 1f6a3a 2c8c4a 43ac5c 6ccc74 9ee69a d8fbd0")
WATER = ramp_of("2a5ea8 4a8ad8 7ab6f0 b4dcfc eef8ff")
FLOWERS = [ramp_of(h) for h in ("9a3a66 e27aa6 fff0f6", "8a8aa8 e6e6f2 ffffff", "8a5c10 f2c648 fff6c4", "2450ae 76aaf4 dceaff", "5a3aa8 a888f0 efe6ff")]
SWAN = ramp_of("5a668a a6b1cd d2d9e9 f2f4fa ffffff")

FRAMES = 4  # the frames of what stirs: a tree's crown, a banner
WAVES = 6  # the frames of a pennant's wave
_LAMPS: list = []  # the windows with a light behind them of the picture being drawn: (middle, top, width, height)

T = TILE


def tone(ramp, k):
    return ramp[max(1, min(int(k), len(ramp) - 1))]


# --- Trees -----------------------------------------------------------------------------


def tree(variant: int, leaves, tall: int | None = None, sway: int = 0, fine: int = 1) -> Image.Image:
    """A tree of Paradise: a pale trunk that parts into branches well over the hero's head,
    a crown of a few rounded bundles of blossom or of gold leaf, lower on one side, with
    a few petals already let go under it. `sway` is the frame of its stirring: each
    bundle of its crown goes round a pixel's breadth in its own time, the trunk still.
    `fine` draws it that many times the draft's size, the same tree with what a finer
    pixel can say: leaf by leaf in its crown, the marks of its bark, twice the flecks."""
    f = fine

    def swung(k):
        a = sway * 2 * math.pi / FRAMES + k * 1.9
        return round(1.3 * math.sin(a)) * f, round(0.7 * math.cos(a + 0.8)) * f

    r = rng("heaven tree", variant, leaves[3])
    tall = tall or r.randint(150, 208)
    wide = int(tall * r.uniform(0.66, 0.82))
    W, H = (wide + 18) * f, (tall + T + 4) * f
    art = Art(W, H)
    foot_x, foot_y = (wide + 18) // 2 + r.randint(-4, 4), tall + T + 4 - T + 6
    split = int(tall * r.uniform(0.36, 0.46))
    crown_cy = tall + 4 - tall + int(tall * 0.36)
    base = max(9, int(tall * 0.066)) + r.randint(0, 2)
    trunk(art, foot_x * f, foot_y * f, (split + 12) * f, base * f, 5 * f, BIRCH, r, lean=r.uniform(-0.6, 0.6), roots=f == 1)
    if f > 1:  # its roots, where it goes into the ground
        for side in (-1, 1):
            for k in range(5 * f):
                for d in range(max(1, 2 * f - k // 2)):
                    art.put(foot_x * f + side * (base * f // 2 + k), foot_y * f - d - 1 + k // 3, BIRCH[3 if side > 0 else 4])
    # The marks of a pale bark: short dark dashes across the trunk.
    step = r.randint(7, 10)
    for i in range(6 * f, (split + 6) * f, step):
        reach = r.randint(2, 2 * f + 1) if f > 1 else 3
        start = r.randint(-2 * f, 0) if f > 1 else -2
        for dx in range(start, start + reach):
            if art.has(foot_x * f + dx, foot_y * f - i):
                art.put(foot_x * f + dx, foot_y * f - i, BIRCH[1])
    bundles = []
    count = r.randint(6, 8)
    middle = (wide + 18) / 2
    for i in range(count):
        a = -math.pi * (0.1 + 0.8 * i / (count - 1)) + r.uniform(-0.15, 0.15)
        dist = r.uniform(0.45, 0.85)
        bx = middle + math.cos(a) * wide * 0.36 * dist * 1.25
        by = crown_cy + math.sin(a) * tall * 0.24 * dist + r.uniform(-4, 6)
        bundles.append((bx, by, r.uniform(0.17, 0.25) * wide, r.uniform(0.15, 0.2) * tall))
    side = r.choice([-1, 1])
    bundles.append((middle + side * wide * 0.33, crown_cy + tall * 0.17, wide * 0.17, tall * 0.12))
    bundles.append((middle + r.uniform(-6, 6), crown_cy - tall * 0.2, wide * 0.24, tall * 0.17))
    top_y = foot_y - split - 10
    for k, (bx, by, _, _) in enumerate(bundles):
        dx, dy = swung(k)
        branch(art, foot_x * f, (top_y + 8) * f, bx * f + dx, (by + 6) * f + dy, (3 if abs(bx - foot_x) > wide * 0.2 else 4) * f, BIRCH)
    for n, (k, (bx, by, rx, ry)) in enumerate(sorted(enumerate(bundles), key=lambda b: -b[1][1])):
        dx, dy = swung(k)
        bundle(art, bx * f + dx, by * f + dy, rx * f, ry * f, leaves, r, (variant, n, leaves[3]))
    # Brighter flecks: a tree in flower catches the light all over.
    for _ in range(wide * 2 * f * f):
        x, y = r.randint(4 * f, W - 5 * f), r.randint((tall + 4 - tall) * f, (tall + 4 - split + 8) * f)
        if art.has(x, y) and art.has(x, y + 2 * f) and tuple(art.a[y, x, :3]) not in (tuple(BIRCH[k]) for k in range(6)):
            art.put(x, y, leaves[6])
    for _ in range(f):
        art.outline(leaves[0])
    # Fallen petals on the ground by its foot.
    for _ in range(14 * f):
        x = foot_x * f + r.randint(-wide * f // 2, wide * f // 2)
        art.put(x, foot_y * f - r.randint(0, f), leaves[r.choice([4, 5, 6])])
    return art.image()


# --- Stone and roofs -------------------------------------------------------------------


def round_shift(off: float) -> int:
    """How a round tower's stone is lit at a place across it (-1 its left edge, 1 its right)."""
    return 1 if off < -0.6 else 0 if off < -0.05 else -1 if off < 0.4 else -2 if off < 0.75 else -3


def ashlar(art: Art, x0, y0, x1, y1, ramp, r, course: int = 6, shift=None, flat: int = 0, fine: int = 1):
    """Dressed stone in even courses, each block its own shade, the joint dark, the top of
    each block lit. `shift(x)` adds to a block's shade at a column (a round tower's light).
    The places are in the draft's pixels; `art` is `fine` times the draft's size, and at
    more than one the same blocks have a joint a pixel wide and a pore here and there."""
    f = fine
    x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
    for row, y in enumerate(range(y0, y1, course)):
        x = x0 - (row % 2) * 6
        while x < x1:
            length = r.randint(9, 14)
            base = r.choice([4, 5, 5, 5, 6])
            for yy in range(y * f, min(y + course, y1) * f):
                for xx in range(max(x, x0) * f, min(x + length, x1) * f):
                    joint = yy == (y + course) * f - 1 or xx == (x + length) * f - 1
                    k = base + flat + (shift(xx / f if f > 1 else xx) if shift else 0)
                    k = k - 2 if joint else k + 1 if yy == y * f else k
                    if f > 1 and not joint and (xx * 7 + yy * 13) % 31 == 0:
                        k -= 1  # a pore in the stone
                    art.put(xx, yy, tone(ramp, k))
            x += length


def shingles(art: Art, rows, ramp, cx: float, lit_far: int = 0, fine: int = 1):
    """A roof's face laid in rows of round-footed tiles, the side toward the sun lighter, a
    bright streak down it, the far side darker. `rows`: (y, x0, x1) of the face at each row,
    in the picture's pixels; `cx` in the draft's. At `fine` over one the tiles are the
    same size, their seams and lips a pixel wide."""
    f = fine
    for y, x0, x1 in rows:
        half = max((x1 - x0) / 2, 1)
        for x in range(int(x0), int(x1) + 1):
            off = (x - cx * f) / half
            band = (y // (4 * f)) % 2
            lip = y % (4 * f) == 4 * f - 1
            seam = (x + band * 3 * f) % (6 * f) == 0
            k = 4 + (1 if off < -0.35 else 0 if off < 0.1 else -1 if off < 0.6 else -2) + lit_far
            if -0.5 < off < -0.38:
                k += 1
            k = k - 2 if seam else k - 1 if lip else k
            art.put(x, y, tone(ramp, k))


def lit_window(art: Art, cx, top, wide, tall, frame=None, cool: bool = False, fine: int = 1):
    """A window with a round head and a light behind it (or, `cool`, glass in a lattice of
    lead), in a frame of stone a pixel proud of the wall, with a sill. The places are in
    the draft's pixels; `art` is `fine` times the draft's size, and at more than one the
    head is a true round and the lead and the frame are a pixel wide."""
    frame = frame or MARBLE
    f = fine
    half = wide / 2
    if not cool and f == 1:
        _LAMPS.append((cx, top, wide, tall))
    for Y in range(int(top * f), int((top + tall) * f)):
        y = Y / f
        up = top + half - y
        reach = half if up <= 0 else math.sqrt(max(half * half - up * up, 0))
        for X in range(int(math.floor((cx - reach) * f)), int(math.ceil((cx + reach) * f))):
            if cool:
                if f == 1:
                    pane = ((X + Y) // 4 + (X - Y) // 4) % len(GLASS)
                    lead = (X + Y) % 4 == 0 or (X - Y) % 4 == 0
                else:
                    pane = ((X + Y) // (4 * f) + (X - Y) // (4 * f)) % len(GLASS)
                    lead = (X + Y) % (4 * f) == 0 or (X - Y) % (4 * f) == 0
                art.put(X, Y, LEAD if lead else GLASS[pane])
            else:
                d = abs(X / f + 0.5 / f - cx) / max(half, 1)
                k = 4 if d < 0.35 else 3 if d < 0.7 else 2
                art.put(X, Y, AMBER[max(k - (1 if y > top + tall * 0.7 else 0), 1)])
        if f == 1:
            for side in (-1, 1):
                art.put(int(cx + side * (reach + 0.5)), Y, frame[1])
        else:
            art.put(int(math.floor((cx - reach) * f)) - 1, Y, frame[1])
            art.put(int(math.ceil((cx + reach) * f)), Y, frame[1])
    if not cool and wide >= 8:
        for Y in range(int((top + half * 0.6) * f), int((top + tall) * f)):
            art.put(int(cx * f), Y, AMBER[0])
        for X in range(int((cx - half) * f), int((cx + half) * f)):
            art.put(X, int(top + tall * 0.55) * f, AMBER[0])
    for X in range(int((cx - half - 2) * f), int((cx + half + 2) * f)):
        for s in range(f):
            art.put(X, int(top + tall) * f + s, frame[6])
            art.put(X, (int(top + tall) + 1) * f + s, frame[2] if f == 1 or s else frame[4])


def finial(art: Art, cx, foot, tall: int = 3 * T, fine: int = 1):
    """A rod of gold on a roof's point, a ball at its foot."""
    f = fine
    c = int(cx)
    for Y in range(int(foot - tall) * f, int(foot) * f):
        if f == 1:
            art.put(c - 1, Y, GOLD[5])
            art.put(c, Y, GOLD[3])
        else:
            for s, k in enumerate((6, 5, 4, 2)):
                art.put((c - 1) * f + s, Y, GOLD[k])
    for DY in range(-3 * f, 3 * f + 1):
        for DX in range(-3 * f, 3 * f + 1):
            dx, dy = DX / f, DY / f
            if dx * dx + dy * dy <= 9:
                art.put(c * f + DX, (int(foot) - 2) * f + DY, GOLD[6] if dx + dy < -2 else GOLD[4] if dx + dy < 2 else GOLD[2])


def cone_rows(cx: float, eaves: float, wide: int, fine: int = 1):
    """The rows of a tower's roof over a tower `wide` tiles across whose eaves' tiles lie on
    the row that ends at y = `eaves`: a flared lip, then two up for one across to a point.
    The rows are in the pixels of a picture `fine` times the draft's size."""
    f = fine
    rows = []
    half = wide / 2 * T
    for I in range(T * f):  # the eaves: a cell wider at either end, flaring out to its lip
        i = I / f
        y = eaves * f - 1 - I
        reach = (half + T + (T - i) * 0.9) * f
        rows.append((int(y), cx * f - reach, cx * f + reach - 1))
    for I in range(wide * T * f):  # two up for one across
        i = I / f
        y = (eaves - T) * f - 1 - I
        reach = (half - i / 2 + 2) * f
        if reach < 0.5 * f:
            break
        rows.append((int(y), cx * f - reach, cx * f + reach - 1))
    return rows


def tower(wide: int, tall: int, back: bool = False, hang: bool = False, style: int = 0, fine: int = 1) -> Image.Image:
    """A round tower of a castle of Paradise, `wide` tiles across (an even number) and
    `tall` tiles to the foot of its head: dressed marble lit on its left and in shade on
    its right, a wider foot (or, hung on a wall, a foot that narrows to a point), bands
    of stone round it, windows with light in them, a head on corbels under battlements,
    and a pointed roof of blue tiles with a rod of gold. `back`: one that stands further
    off, paler and plainer. Its floor line is its foot; it is drawn half a tile right of
    its middle, so it stands in the cell left of its middle line. `style` is what else
    it has, so two towers are not one tower twice: 1 a balcony on brackets with a door
    onto it, 2 a round window of gold tracery and gilded bands, 3 a climbing rose up
    its sunny side and flowers under its windows, 4 a banner down its face. `fine` draws
    it that many times the draft's size: joints, lead and seams a pixel wide, true
    rounds, the rose leaf by leaf."""
    r = rng("heaven tower", wide, tall, back, hang)
    f = fine
    stone = MARBLE_FAR if back else MARBLE
    blue = AZURE_FAR if back else AZURE
    W0 = (wide + 5) * T
    roof_tiles_tall = wide + 1
    H0 = (tall + 2 + roof_tiles_tall + 4) * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2 + T / 2
    half = wide * T / 2
    foot = H0
    top = foot - tall * T  # the top of the shaft

    def shift(x):
        return round_shift((x + 0.5 - cx) / half)

    def at(X):
        """The draft's column a column of the picture is at."""
        return X / f if f > 1 else X

    def row(y, x0_, x1_, colour_of):
        """A draft row from x0_ to x1_, `colour_of(x)` its colour at a draft column."""
        for X in range(int(x0_) * f, int(x1_) * f):
            for s in range(f):
                art.put(X, int(y) * f + s, colour_of(at(X)))

    ashlar(art, cx - half, top, cx + half, foot, stone, r, shift=shift, fine=f)
    if hang:
        # Hung on a wall: its foot narrows to a point in steps of stone.
        for I in range(2 * T * f):
            i = I / f
            Y = foot * f - 1 - I
            reach = half * (i / (2 * T)) ** 0.7
            for X in range(int(cx - half) * f, int(cx + half) * f):
                if abs(at(X) + 0.5 / f - cx) > reach:
                    art.a[Y, X] = 0
    else:
        # A wider foot: two courses standing proud, a lit moulding over them.
        ashlar(art, cx - half - 5, foot - 2 * T, cx + half + 5, foot, stone, r, shift=shift, flat=0, fine=f)
        row(foot - 2 * T - 1, cx - half - 6, cx + half + 6, lambda x: tone(stone, 6 + shift(x)))
        row(foot - 2 * T, cx - half - 6, cx + half + 6, lambda x: tone(stone, 2 + shift(x)))
    # Bands of stone round it.
    for y in range(int(top + 5 * T), int(foot - 3 * T), 7 * T):
        for d, k in enumerate((6, 5, 2)):
            row(y - 4 * T + d, cx - half - 1, cx + half + 1, lambda x, k=k: tone(stone, k + shift(x)))
    # Windows, where the map's own lit cells are: every seven rows from four under the top.
    for wrow in range(4, tall - 4, 7):
        y = top + wrow * T - T // 2
        if wide >= 6 and not back:
            for dx in (-6, 6):
                lit_window(art, cx + dx, y, 8, 30, stone, fine=f)
        else:
            lit_window(art, cx, y, 8 if not back else 6, 28 if not back else 22, stone, fine=f)
    if style == 1:
        # A balcony round it between two rows of windows, on brackets, and a door onto it.
        by = int(top + (9 if tall < 21 else 16) * T)
        lit_window(art, cx, by - 30, 10, 29, stone, fine=f)
        x0 = int(cx - half - 7)
        for X in range(x0 * f, int(cx + half + 7) * f):
            x = at(X)
            xi = X // f
            sh = round_shift((x + 0.5 - cx) / (half + 7))
            for d, k in enumerate((6, 5, 5, 2)):
                for s in range(f):
                    art.put(X, (by + d) * f + s, tone(stone, k + sh))
            if (xi - x0) % 6 < 3:
                for d in range(3):
                    if abs(x + 0.5 - cx) < half + 7 - d * 2:
                        for s in range(f):
                            art.put(X, (by + 4 + d) * f + s, tone(stone, 3 + sh))
            for s in range(f):
                art.put(X, (by - 9) * f + s, tone(stone, 6 + sh))
                art.put(X, (by - 8) * f + s, tone(stone, 3 + sh))
            if (xi - x0) % 4 == 0 and X % f == 0:
                for Y in range((by - 7) * f, by * f):
                    art.put(X, Y, tone(stone, 5 + sh))
                    if f > 1:
                        art.put(X + 1, Y, tone(stone, 3 + sh))
    elif style == 2:
        # A round window of gold tracery under its head, and its bands gilded.
        ry, rad = int(top + 2.3 * T), 8 if wide >= 6 else 6
        if f == 1:
            _LAMPS.append((cx, ry - rad, 2 * rad, 2 * rad))
        for DY in range((-rad - 2) * f, (rad + 3) * f):
            for DX in range((-rad - 2) * f, (rad + 3) * f):
                dx, dy = DX / f, DY / f
                d = math.hypot(dx + 0.5 / f, dy + 0.5 / f)
                if d > rad + 2:
                    continue
                spoke = abs(math.sin(math.atan2(dy + 0.5 / f, dx + 0.5 / f) * 4)) < (0.22 if f == 1 else 0.14)
                colour = GOLD[5 if dx + dy < 0 else 3] if d > rad or spoke or d < 2 else AMBER[4 if d < rad * 0.5 else 3 if d < rad * 0.8 else 2]
                art.put(cx * f + DX, ry * f + DY, colour)
        for y in range(int(top + 5 * T), int(foot - 3 * T), 7 * T):
            row(y - 4 * T, cx - half - 1, cx + half + 1, lambda x: GOLD[6 if x < cx - half * 0.3 else 4])
            row(y - 4 * T + 1, cx - half - 1, cx + half + 1, lambda x: GOLD[4 if x < cx else 2])
    elif style == 3:
        # A climbing rose up its sunny side, and flowers in a box under every window.
        x = cx - half * 0.55
        for i in range(int((tall - 3) * T)):
            y = foot - 1 - i
            x = min(max(x + r.uniform(-0.5, 0.5), cx - half + 2), cx - 3)
            if f == 1:
                art.put(x, y, VINE[1])
                art.put(x + 1, y, VINE[3])
            else:
                for s in range(f):
                    art.put(x * f + 1, y * f + s, VINE[1])
                    art.put(x * f + 2, y * f + s, VINE[3])
            if i % 4 == 0:
                side = r.choice([-1, 1])
                for d in (1, 2, 3):
                    if f == 1:
                        art.put(x + side * d + (side > 0), y - d // 2, VINE[4 if d > 1 else 2])
                    else:
                        lx, ly = (x + side * d + (side > 0)) * f, (y - d / 2) * f
                        art.put(lx, ly, VINE[5 if d == 3 else 4 if d > 1 else 2])
                        art.put(lx + side, ly, VINE[4 if d > 1 else 2])
                        art.put(lx, ly + 1, VINE[3])
            if i % 11 == 5:
                bx, by = int(x) + r.randint(-3, 3), y
                if f == 1:
                    for dx, dy, k in ((0, 0, 6), (-1, 0, 4), (1, 0, 4), (0, -1, 5), (0, 1, 3)):
                        art.put(bx + dx, by + dy, BLOSSOM[k])
                else:
                    petals = ((0, -2, 5), (1, -2, 5), (-2, -1, 5), (-1, -1, 6), (2, -1, 4), (-2, 0, 4), (-1, 0, 6), (0, 0, 6), (1, 0, 5), (2, 0, 4), (-1, 1, 4), (0, 1, 3), (1, 1, 3), (0, -1, 6), (1, -1, 5))
                    for dx, dy, k in petals:
                        art.put(bx * f + dx, by * f + dy, BLOSSOM[k])
        for wrow in range(4, tall - 4, 7):
            for wx in (cx - 6, cx + 6) if wide >= 6 else (cx,):
                sill = int(top + wrow * T - T // 2 + (30 if wide >= 6 else 28)) + 2
                for dx in range(-6, 6):
                    box = (MARBLE[3], VINE[3 if dx % 2 else 2], FLOWERS[(dx // 2) % 3][1 + (dx % 2)] if dx % 3 else VINE[4])
                    if f == 1:
                        art.put(wx + dx, sill + 2, box[0])
                        art.put(wx + dx, sill + 1, box[1])
                        art.put(wx + dx, sill, box[2])
                        continue
                    for s in range(f):
                        for t in range(f):
                            X = int((wx + dx) * f) + s
                            art.put(X, (sill + 2) * f + t, box[0])
                            art.put(X, (sill + 1) * f + t, box[1])
                            art.put(X, sill * f + t, box[2] if (s + t) % 2 == 0 or not dx % 3 else box[1])
    elif style == 4:
        # A banner down its face, under the first row of windows.
        banner(art, cx, top + 6.2 * T, 14 if wide <= 4 else 18, 4 * T, r, fine=f)
    # The head: corbels with shadow between them, a walk a cell wider, battlements.
    head_top = top - 2 * T
    x_left = int(cx - half - T)
    for X in range(x_left * f, int(cx + half + T) * f):
        x = at(X)
        xi = X // f
        off = (x + 0.5 - cx) / (half + T)
        sh = round_shift(off)
        for Y in range(int(head_top) * f, int(top) * f):
            k = 4 + sh + (1 if Y == int(head_top) * f else 0)
            art.put(X, Y, tone(stone, k))
        # Corbels: every eight pixels a bracket, dark between them.
        for Y in range(int(top) * f, (int(top) + 6) * f):
            y = at(Y)
            proud = (xi - x_left) % 8 < 4
            if proud and abs(x + 0.5 - cx) < half + T - (y - top) * 1.6:
                lit = 1 if f > 1 and (xi - x_left) % 8 == 0 and X % f == 0 else 0
                art.put(X, Y, tone(stone, 3 + sh + lit))
            elif abs(x + 0.5 - cx) < half:
                art.put(X, Y, tone(stone, 1))
    for X in range(x_left * f, int(cx + half + T) * f):
        xi = X // f
        # Joints and a line of shadow under the walk's lip.
        if (xi - x_left) % 10 == 9 and X % f == f - 1:
            for Y in range((int(head_top) + 2) * f, int(top) * f):
                art.put(X, Y, tone(stone, 2 + round_shift((at(X) + 0.5 - cx) / (half + T))))
        for s in range(f):
            art.put(X, (int(top) - 1) * f + s, tone(stone, 2))
    # The shadow the head casts on the shaft.
    for Y in range((int(top) + 6) * f, (int(top) + 12) * f):
        y = at(Y)
        for X in range(int(cx - half) * f, int(cx + half) * f):
            x = at(X)
            if f == 1:
                if art.has(X, Y) and (X + Y) % 2 == 0 or y < top + 9:
                    art.put(X, Y, tone(stone, 2 + shift(x)))
            elif y < top + 9:
                art.put(X, Y, tone(stone, 2 + shift(x)))
            elif art.has(X, Y) and y < top + 10.5:
                art.put(X, Y, tone(stone, 3 + shift(x)))  # its edge, a softer shade than a chequer
    # The roof.
    eaves = head_top
    rows = cone_rows(cx, eaves, wide, fine=f)
    shingles(art, rows, blue, cx, fine=f)
    lip = min(rows, key=lambda r_: -r_[0])
    for X in range(int(lip[1]), int(lip[2]) + 1):
        art.put(X, lip[0], GOLD[4] if X < cx * f else GOLD[2])
        art.put(X, lip[0] - 1, GOLD[6] if X < cx * f else GOLD[3])
        if f > 1:
            art.put(X, lip[0] - 2, GOLD[5] if X < cx * f else GOLD[3])
    apex = min(r_[0] for r_ in rows) / f
    finial(art, cx, apex + 2, fine=f)
    for _ in range(f):
        art.outline(stone[0] if not back else stone[1])
    return art.image()


def roof_over(wide: int, fine: int = 1) -> Image.Image:
    """The tiles of a great roof, drawn over the map's own plain ones: a roof at 45 degrees
    over `wide` tiles, laid in rows of blue tiles, its side from the sun darker, a gabled
    window with a light in it on either face, a crest of gold along its eaves, a ball at
    its point. Its floor line is its point. `fine` draws it that many times the draft's
    size, its tiles' seams a pixel wide."""
    f = fine
    W0 = (wide + 2 + (wide + 1) % 2) * T
    H0 = ((wide + 1) // 2 + 4) * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2 + (T / 2 if wide % 2 == 0 else 0)  # over an even number of tiles it stands in the cell left of its middle
    rows = []
    for I in range((H0 - 3 * T - 4) * f):
        i = I / f
        y = H0 * f - 1 - I
        reach = wide * T / 2 + T - i
        if reach < 0.5:
            break
        rows.append((y, (cx - reach) * f, (cx + reach) * f - 1))
    shingles(art, rows, AZURE, cx, fine=f)
    # A gabled window on either face.
    for side in (-1, 1):
        wx = cx + side * wide * T * 0.22
        wy = H0 - int(wide * T * 0.16) - 30
        for I in range(22 * f):
            reach = I / f * 0.75
            for X in range(int((wx - reach) * f), int((wx + reach) * f) + 1):
                art.put(X, (wy - 14) * f + I, tone(AZURE, 6 if X < wx * f else 3))
        for Y in range(int(wy + 8) * f, int(wy + 30) * f):
            for X in range(int(wx - 11) * f if f == 1 else int((wx - 11) * f), int(wx + 11) * f if f == 1 else int((wx + 11) * f)):
                x = X / f
                art.put(X, Y, tone(MARBLE, 5 if x < wx - 6 else 4 if x < wx + 6 else 3))
        lit_window(art, wx, wy + 11, 10, 15, MARBLE, fine=f)
    for X in range(W0 * f):
        if art.has(X, H0 * f - 1):
            x = X // f
            for s in range(f):
                art.put(X, H0 * f - 1 - s, GOLD[2] if s == 0 else GOLD[3])
                art.put(X, (H0 - 2) * f + s, GOLD[(5 if X < cx * f else 3) + (1 if f > 1 and s == 0 and X < cx * f else 0)])
                if x % 6 == 0:  # the crest
                    art.put(X, (H0 - 3) * f + s, GOLD[6])
                    art.put(X, (H0 - 4) * f + s, GOLD[5])
    finial(art, cx, min(row[0] for row in rows) / f + 2, fine=f)
    for _ in range(f):
        art.outline(AZURE[0])
    return art.image()


# --- The hall --------------------------------------------------------------------------


def column(art: Art, cx, top, foot, wide: float, stone=None, fine: int = 1):
    """A column of bright marble: a fluted shaft lit on its left, a base on a block, a
    capital of leaves under a slab. Its places are in the draft's pixels; `art` is `fine`
    times the draft's size, and at more than one each flute has a lit edge, the leaves of
    the capital curl, and the gold under it has a sheen."""
    stone = stone or MARBLE
    f = fine
    half = wide / 2
    for Y in range(int(top + 12) * f, int(foot - 10) * f):
        for X in range(int((cx - half) * f), int((cx + half) * f)):
            off = (X / f + 0.5 / f - cx) / half
            fluted = (off + 1) * wide / 2.67
            flute = int(fluted) % 3
            k = 5 + round_shift(off) + (1 if flute == 0 else -1 if flute == 2 else 0)
            if f > 1 and flute == 1 and fluted % 1 < 0.18:
                k += 1  # the lit edge of a flute
            art.put(X, Y, tone(stone, k))
    for i, (dy, grow, k) in enumerate(((0, 5, 4), (3, 3, 6), (5, 4, 3), (8, 2, 5))):
        for Y in range(int(foot - 10 + dy) * f, int(foot - 10 + dy + (3 if i < 3 else 2)) * f):
            for X in range(int((cx - half - grow) * f), int((cx + half + grow) * f)):
                lit = 1 if f > 1 and Y == int(foot - 10 + dy) * f and k < 6 else 0
                art.put(X, Y, tone(stone, k + lit + round_shift((X / f + 0.5 / f - cx) / (half + grow))))
    # The capital: a slab, a bell of leaves under it.
    for Y in range(int(top) * f, int(top + 4) * f):
        for X in range(int((cx - half - 6) * f), int((cx + half + 6) * f)):
            art.put(X, Y, tone(stone, (6 if Y / f == top else 5) + round_shift((X / f + 0.5 / f - cx) / (half + 6))))
    for I in range(8 * f):
        i = I / f
        grow = 5 - (I // f if f == 1 else i) * 0.6
        for X in range(int((cx - half - grow) * f), int((cx + half + grow) * f)):
            leaf = (X + I // 2) % (4 * f) == 0 if f > 1 else (X + I // 2) % 4 == 0
            art.put(X, int(top + 4) * f + I, tone(stone, (3 if leaf else 5) + round_shift((X / f + 0.5 / f - cx) / (half + grow))))
    for X in range(int((cx - half - 4) * f), int((cx + half + 4) * f)):
        for s in range(f):
            art.put(X, int(top + 4) * f + s, GOLD[5] if f > 1 and s == 0 else GOLD[4])


def pillar(rows: int, wide: int = 2, fine: int = 1) -> Image.Image:
    """A column standing by itself, `rows` tiles high."""
    f = fine
    W0, H0 = (wide + 2) * T, rows * T
    art = Art(W0 * f, H0 * f)
    column(art, W0 / 2 + (T / 2 if wide % 2 == 0 else 0), 0, H0, wide * T * 0.62, fine=f)
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def banner(art: Art, cx, top, wide: int, tall: int, r, fine: int = 1):
    """A banner of blue cloth on a rod of gold, the wings of the host on it, its foot cut to
    two points. At `fine` over one the folds and the wings are drawn finer."""
    f = fine
    for X in range(int(cx - wide / 2 - 2) * f, int(cx + wide / 2 + 2) * f):
        for s in range(f):
            art.put(X, int(top) * f + s, GOLD[6] if f > 1 and s == 0 else GOLD[5])
            art.put(X, (int(top) + 1) * f + s, GOLD[2])
    for I in range(tall * f):
        i = I / f
        Y = (int(top) + 2) * f + I
        for X in range(int((cx - wide / 2) * f), int((cx + wide / 2) * f)):
            off = (X / f + 0.5 / f - cx) / (wide / 2)
            notch = i > tall - 7 and abs(off) < (i - (tall - 7)) / 7
            if notch:
                continue
            fold = math.sin(off * 4.2 + 0.6)
            k = 4 + (1 if fold > 0.5 else -1 if fold < -0.4 else 0)
            edge = abs(off) > 0.82
            art.put(X, Y, GOLD[4] if edge else tone(CLOTH, k))
    # The wings, in gold.
    wy = int(top) + tall // 3
    c = int(cx)
    for dx in range(1, wide // 2 - 2):
        for dy in range(max(1, 5 - dx)):
            for s in range(f):
                for t in range(f):
                    art.put((c - dx) * f + s, (wy - dx // 2 + dy) * f + t, GOLD[6] if f > 1 and t == 0 else GOLD[5])
                    art.put((c + dx - 1) * f + s, (wy - dx // 2 + dy) * f + t, GOLD[4])
    for DY in range(-f, 5 * f):
        for s in range(f):
            art.put((c - 1) * f + s, wy * f + DY, GOLD[6])
            art.put(c * f + s, wy * f + DY, GOLD[4])


def glass_window(art: Art, cx, top, foot, wide: int, r, warm: bool = True, fine: int = 1):
    """A tall window with a pointed head: panes of coloured glass in a lattice of lead, a
    stone frame with a mullion up its middle and a round light in its head. The places
    are in the draft's pixels; at `fine` over one the head is a true curve and the lead,
    the frame and the mullion are a pixel finer."""
    f = fine
    half = wide / 2
    for Y in range(int(top) * f, int(foot) * f):
        y = Y / f
        up = top + wide * 0.9 - y
        reach = half if up <= 0 else half * math.sqrt(max(1 - (up / (wide * 0.9)) ** 2, 0))
        for X in range(int(math.floor((cx - reach) * f)), int(math.ceil((cx + reach) * f))):
            p = 6 * f
            lead = (X + Y) % p == 0 or (X - Y) % p == 0
            pane = ((X + Y) // p * 3 + (X - Y) // p * 5) % 7
            if warm:
                colour = AMBER[1 + pane % 4] if pane % 3 else GOLD[5]
                depth = abs(X / f + 0.5 / f - cx) / max(half, 1)
                if depth < 0.3 and pane % 3:
                    colour = AMBER[4]
            else:
                colour = GLASS[pane % len(GLASS)]
            art.put(X, Y, LEAD if lead else colour)
        for side in (-1, 1):
            for k in range(2):
                if f == 1:
                    art.put(int(cx + side * (reach + 0.5 + k)), Y, MARBLE[5 - k * 2])
                else:
                    edge = int(math.floor((cx - reach) * f)) - 1 - k * f if side < 0 else int(math.ceil((cx + reach) * f)) + k * f
                    for s in range(f):
                        art.put(edge + side * s, Y, MARBLE[5 - k * 2])
    for Y in range(int(top + wide * 0.7) * f, int(foot) * f):
        if f == 1:
            art.put(int(cx), Y, MARBLE[5])
            art.put(int(cx) + 1, Y, MARBLE[2])
        else:
            for s, k in enumerate((6, 5, 3, 2)):
                art.put(int(cx) * f - 1 + s, Y, MARBLE[k])
    for X in range(int(cx - half - 3) * f, int(cx + half + 3) * f):
        for s in range(f):
            art.put(X, int(foot) * f + s, MARBLE[6])
            art.put(X, (int(foot) + 1) * f + s, MARBLE[4])
            art.put(X, (int(foot) + 2) * f + s, MARBLE[2])


def wall(art: Art, x0, y0, x1, y1, r, fine: int = 1):
    """The wall at the back of a hall: marble out of the sun, darker toward its top. The
    places are in the draft's pixels; `art` is `fine` times the draft's size."""
    f = fine
    span = max(y1 - y0, 1)
    ashlar(art, x0, y0, x1, y1, SHADOWED, r, course=8, shift=lambda x: 0, fine=f)
    for Y in range(int(y0) * f, int(y0 + span * 0.35) * f):
        for X in range(int(x0) * f, int(x1) * f):
            if (X + Y) % 2 == 0 and art.has(X, Y):
                art.put(X, Y, SHADOWED[1])


def bay(rows: int, mirrored: bool = False, fine: int = 1) -> Image.Image:
    """One bay of a hall's arcade, nine tiles wide and `rows` tiles high: the wall at the
    back with a tall window of warm glass in it, and in front a column at the bay's west
    end with a banner on it, half of a round arch springing from it either way, and a
    band with a line of gold over all. Stood side by side they are an arcade. `fine`
    draws it that many times the draft's size."""
    r = rng("heaven bay", rows)
    f = fine
    W0, H0 = 9 * T, rows * T
    art = Art(W0 * f, H0 * f)
    wall(art, 0, 0, W0, H0, r, fine=f)
    opening = 2 * T + 3.5 * T  # the middle of the bay's opening
    glass_window(art, opening, H0 - 15 * T if rows >= 20 else T * 1.5, H0 - 3 * T, 30 if rows >= 20 else 22, r, fine=f)
    # The band over the arcade, and the arches under it.
    band = T
    spring = (5 if rows >= 20 else 3) * T  # the row the arches spring from, from the top
    for Y in range((band + 2) * f, (spring + 2) * f):
        y = Y / f
        for X in range(W0 * f):
            x = X / f
            # The opening's arch is a half round of radius 3.5 tiles about its springing.
            d = math.hypot(x + 0.5 / f - opening, y - spring)
            if d > 3.5 * T:
                # The wall over the arch: stone in courses, a joint every dozen pixels.
                art.put(X, Y, tone(MARBLE, fine_stones(X, Y, f)))
            elif d > 3.5 * T - 5:
                # The arch's ring of stones, lit from below by the glass.
                ang = math.atan2(y - spring, x + 0.5 / f - opening)
                art.put(X, Y, tone(MARBLE, 6 if int(ang * 9) % 2 else 4) if d > 3.5 * T - 4 else MARBLE[2])
    for X in range(W0 * f):
        x = X // f
        for Y in range(0, (band + 2) * f):
            y = Y // f
            first = Y == 0
            art.put(X, Y, tone(MARBLE, 6 if first or (f == 1 and y == 0) else 5 if y < band - 3 else 3 if y < band else 4))
        for s in range(f):
            art.put(X, (band - 4) * f + s, GOLD[6] if f > 1 and s == 0 else GOLD[5])
            art.put(X, (band - 3) * f + s, GOLD[2])
            if x % 8 < 4:
                art.put(X, (band + 1) * f + s, MARBLE[2])
    for Y in range((int(1.5 * T) + 2) * f, 4 * T * f):  # the chain the bay's lanterns hang on
        y = Y // f
        if f == 1:
            art.put(int(opening), Y, GOLD[5] if y % 3 else GOLD[2])
        else:
            link = Y % (3 * f)
            art.put(int(opening) * f, Y, GOLD[5] if link else GOLD[2])
            art.put(int(opening) * f + 1, Y, GOLD[3] if link else GOLD[2])
    column(art, T, spring, H0, 1.25 * T, fine=f)
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if mirrored else image


def bay_end(rows: int, fine: int = 1) -> Image.Image:
    """Where a hall's arcade ends toward the high seat, five tiles wide: its last column
    with a banner on it, and over the way through to the seat a wall of cut stone under
    the same band."""
    r = rng("heaven bay end", rows)
    f = fine
    W0, H0 = 5 * T, rows * T
    art = Art(W0 * f, H0 * f)
    wall(art, 0, 0, W0, H0, r, fine=f)
    band = T
    spring = (5 if rows >= 20 else 3) * T
    for Y in range((band + 2) * f, (spring + 2) * f):
        for X in range(W0 * f):
            art.put(X, Y, tone(MARBLE, fine_stones(X, Y, f)))
    for X in range(W0 * f):  # the lintel's underside, in its own shadow
        x = X // f
        for s in range(f):
            art.put(X, (spring + 2) * f + s, MARBLE[3])
            art.put(X, (spring + 3) * f + s, MARBLE[1])
        for Y in range(0, (band + 2) * f):
            y = Y // f
            art.put(X, Y, tone(MARBLE, 6 if Y == 0 else 5 if y < band - 3 else 3 if y < band else 4))
        for s in range(f):
            art.put(X, (band - 4) * f + s, GOLD[6] if f > 1 and s == 0 else GOLD[5])
            art.put(X, (band - 3) * f + s, GOLD[2])
            if x % 8 < 4:
                art.put(X, (band + 1) * f + s, MARBLE[2])
    column(art, T, spring, H0, 1.25 * T, fine=f)
    return art.image()


def hung_banner(frame: int, fine: int = 1) -> Image.Image:
    """A banner hung on a column of the hall and stirring: the cloth, the wings of the
    host on it, a slow wave going down it frame by frame, its rod still. Three tiles
    wide, the cloth about the line two tiles from its left: it is stood in the west
    cell of a column two cells wide, and hangs on the column's middle."""
    r = rng("heaven hung banner")
    f = fine
    W0, H0 = 3 * T, 5 * T + 4
    art = Art(W0 * f, H0 * f)
    banner(art, 2 * T, 0, 16, H0 - 6, r, fine=f)
    for Y in range(4 * f, H0 * f):
        y = Y / f
        dx = round(1.7 * f * (y / H0) ** 1.2 * math.sin(frame * 2 * math.pi / FRAMES - y / 17))
        art.a[Y] = np.roll(art.a[Y], dx, axis=0)
    return art.image()


def pennant(frame: int, fine: int = 1) -> Image.Image:
    """A long pennant of blue bordered with gold, its end cut to two points, flying to the
    right of the rod it is hoisted on, which is the picture's middle: a wave runs along
    it frame by frame, wider toward its end. `fine` draws it that many times the draft's
    size: the wave smooth along it, its border a pixel wide."""
    f = fine
    long = 54
    W0, H0 = 2 * long + 2, 30
    art = Art(W0 * f, H0 * f)
    for I in range(long * f):
        i = I / f
        t = i / long
        phase = i / 6.5 - frame * 2 * math.pi / WAVES
        lift = 3.4 * math.sin(phase) * (0.2 + 0.8 * t)
        thick = round(15 - 5 * t) if f == 1 else (15 - 5 * t)
        rows = thick if f == 1 else int(round(thick * f))
        for K in range(rows):
            k = K / f
            if i > long - 13 and abs(k - (thick - 1) / 2 - (0.5 - 0.5 / f if f > 1 else 0)) < (i - (long - 13)) * 0.42:
                continue  # the cut between its two points
            edge = (K in (0, rows - 1) or I == 0) if f == 1 else (K == 0 or K == rows - 1 or I < 1)
            fold = math.cos(phase)
            colour = GOLD[5 if K == 0 else 3] if edge else tone(CLOTH, 4 + (1 if fold > 0.45 else -1 if fold < -0.45 else 0) - (1 if k > thick * 0.7 else 0))
            Y = 7 + round(lift) + K if f == 1 else int(round((7 + lift) * f)) + K
            art.put(W0 // 2 * f + I, Y, colour)
    for dx, dy in ((6, 0), (5, -1), (7, -1), (4, -2), (8, -2), (6, 1), (6, 2)):  # the wings, small, by the rod
        lift = 3.4 * math.sin(dx / 6.5 - frame * 2 * math.pi / WAVES) * (0.2 + 0.8 * dx / long)
        if f == 1:
            art.put(W0 // 2 + dx, 14 + round(lift) + dy, GOLD[6])
            continue
        for s in range(f):
            for u in range(f):
                art.put((W0 // 2 + dx) * f + s, int(round((14 + lift + dy) * f)) + u, GOLD[6] if s + u < 2 else GOLD[5])
    return art.image()


def glint(frame: int, fine: int = 1) -> Image.Image:
    """A glint of light on gold: a star of four points that comes, is whole, and goes.
    `fine` draws it that many times the draft's size: its arms thin to their points."""
    f = fine
    art = Art(T * f, T * f)
    reach = (2, 5, 3)[frame]
    c = 8 * f
    for D in range(-reach * f, reach * f + 1):
        d = abs(D) / f
        bright = (255, 255, 255) if d < reach - 1 else (255, 244, 190)
        alpha = 255 if d < reach else 150
        if f > 1:
            alpha = int(255 - 130 * d / reach)
        art.put(c + D, c, bright, alpha)
        art.put(c, c + D, bright, alpha)
    if frame == 1:
        for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            art.put(c + dx, c + dy, (255, 250, 220), 200)
    return art.image()


def glow(image: Image.Image, lamps, fine: int = 1) -> Image.Image:
    """The light of a picture's windows by itself, and its halo: shown unlit over the
    picture as the day goes, so a castle's windows burn at dusk and at night. `lamps` are
    in the draft's pixels; at `fine` over one `image` is that many times the draft's size
    and the halo spreads as far in the draft's terms."""
    f = fine
    a = np.array(image.convert("RGBA"))
    out = np.zeros_like(a)
    mask = np.zeros(a.shape[:2], dtype=np.uint8)
    for cx, top, wide, tall in lamps:
        x0, x1 = max(int((cx - wide / 2) * f) - 2 * f, 0), min(int((cx + wide / 2) * f) + 2 * f, a.shape[1])
        y0, y1 = max(int(top * f) - f, 0), min(int((top + tall) * f) + f, a.shape[0])
        part = a[y0:y1, x0:x1]
        light = np.zeros(part.shape[:2], dtype=bool)
        for colour in AMBER[1:]:
            light |= (part[..., :3] == np.array(colour, dtype=np.uint8)).all(axis=-1) & (part[..., 3] > 0)
        out[y0:y1, x0:x1][light] = part[light]
        mask[y0:y1, x0:x1][light] = 255
    halo = np.array(Image.fromarray(mask).filter(ImageFilter.GaussianBlur(4.5 * f))).astype(float) / 255
    out[..., 0], out[..., 1], out[..., 2] = np.where(mask > 0, out[..., 0], 255), np.where(mask > 0, out[..., 1], 206), np.where(mask > 0, out[..., 2], 110)
    out[..., 3] = np.where(mask > 0, 255, np.clip(halo * 2.4, 0, 1) * 135).astype(np.uint8)
    return Image.fromarray(out)


def rays(rows: int, mirrored: bool = False, fine: int = 1) -> Image.Image:
    """The light of a hall's windows in its air: from each tall window of a side a shaft
    that slants down into the hall toward its middle, pale gold, fainter as it goes.
    Unlit, and the sun's: gone by night. The size of the side it is laid over. `fine`
    asks the same light at every half pixel."""
    f = fine
    H, W0 = rows * T, 24 * T
    ys, xs = np.mgrid[0 : H * f, 0 : W0 * f].astype(float)
    ys, xs = ys / f, xs / f
    alpha = np.zeros(ys.shape)
    slant, far = 0.62, 10.0 * T
    top, foot = H - 15 * T + 14, H - 3 * T
    for mid in (T + 5.5 * T, 10 * T + 5.5 * T):
        # A pixel is in the shaft if, going back up the slant, it comes to the window.
        low = np.maximum(np.maximum(ys - foot, (xs - (mid + 14)) / slant), 0)
        high = np.minimum(np.minimum(ys - top, (xs - (mid - 14)) / slant), far)
        inside = high >= low
        streak = 0.8 + 0.2 * np.sin((xs - slant * ys) / 3.1)
        alpha = np.maximum(alpha, np.where(inside, (1 - low / far) * streak * 58, 0))
    side = np.zeros(ys.shape + (4,), dtype=np.uint8)
    side[..., 0], side[..., 1], side[..., 2] = 255, 238, 186
    side[..., 3] = alpha.astype(np.uint8)
    picture = Image.fromarray(side)
    if mirrored:
        picture = picture.transpose(Image.FLIP_LEFT_RIGHT)
    image = Image.new("RGBA", (25 * T * f, H * f), (0, 0, 0, 0))
    image.paste(picture, (T * f, 0))
    return image


def hall_side(rows: int, mirrored: bool = False, fine: int = 1) -> Image.Image:
    """One side of the citadel's hall, 24 tiles of it from its end wall to the way through
    to the high seat: a tile of wall, two bays of the arcade, and the arcade's end. The
    east side is the west one turned about. It stands in the cell left of its middle."""
    r = rng("heaven hall side", rows)
    f = fine
    side = Image.new("RGBA", (24 * T * f, rows * T * f), (0, 0, 0, 0))
    strip = Art(T * f, rows * T * f)
    wall(strip, 0, 0, T, rows * T, r, fine=f)
    for Y in range(0, (T + 2) * f):
        y = Y // f
        for X in range(T * f):
            strip.put(X, Y, tone(MARBLE, 6 if Y == 0 else 5 if y < T - 3 else 3 if y < T else 4))
    for X in range(T * f):
        for s in range(f):
            strip.put(X, (T - 4) * f + s, GOLD[6] if f > 1 and s == 0 else GOLD[5])
            strip.put(X, (T - 3) * f + s, GOLD[2])
    side.paste(strip.image(), (0, 0))
    one = bay(rows, fine=f)
    side.paste(one, (T * f, 0))
    side.paste(one, (10 * T * f, 0))
    side.paste(bay_end(rows, fine=f), (19 * T * f, 0))
    if mirrored:
        side = side.transpose(Image.FLIP_LEFT_RIGHT)
    image = Image.new("RGBA", (25 * T * f, rows * T * f), (0, 0, 0, 0))
    image.paste(side, (T * f, 0))
    return image


def high_seat(rows: int, fine: int = 1) -> Image.Image:
    """The wall behind the high seat, 25 tiles wide and `rows` tiles high, from the hall's
    floor up through the great tower: marble out of the sun between pilasters, a throne
    of gold with a high back, over it the wings of the host and a ring of light, a tall
    window of cool glass at either hand, and up the tower three ranks of small windows
    with light in them. `fine` draws it that many times the draft's size: a true ring,
    feathers and rays a pixel finer, the throne's posts turned."""
    r = rng("heaven high seat", rows)
    f = fine
    W0, H0 = 25 * T, rows * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2

    def at(V):
        return V / f if f > 1 else V

    wall(art, 0, 0, W0, H0, r, fine=f)
    for px in (2 * T, W0 - 2 * T):
        for Y in range(0, H0 * f):
            for X in range(int(px - 7) * f, int(px + 7) * f):
                x = at(X)
                art.put(X, Y, tone(SHADOWED, 5 if x < px - 3 else 4 if x < px + 3 else 3))
    # The ring of light and the wings over the seat.
    wy = H0 - 16 * T
    for a in range(0, 3600 * f):
        ang = a / (3600 * f) * 2 * math.pi
        for rad, k in ((58, 4), (59, 6), (60, 5), (61, 2)) if f == 1 else ((58, 4), (58.5, 6), (59, 6), (59.5, 6), (60, 5), (60.5, 5), (61, 2), (61.5, 2)):
            art.put((cx + math.cos(ang) * rad) * f, (wy + math.sin(ang) * rad) * f, GOLD[k])
    for ray in range(24):
        ang = ray / 24 * 2 * math.pi
        for RAD in range(64 * f, (92 if ray % 2 else 80) * f):
            rad = RAD / f
            art.put((cx + math.cos(ang) * rad) * f, (wy + math.sin(ang) * rad) * f, GOLD[4 if int(rad) % 3 else 6])
    for side in (-1, 1):
        for feather in range(11):
            # A wing: feathers fanned up and out, the longest at its tip.
            ang = math.radians(8 + feather * 6.5)
            length = 44 + feather * 9
            root_x, root_y = cx + side * (8 + feather * 3.0), wy + 34 - feather * 2.2
            for I in range(length * f):
                i = I / f
                x = root_x + side * math.cos(ang) * i
                y = root_y - math.sin(ang) * i
                width = 5 if i < length * 0.75 else 3
                for D in range(width * f):
                    k = (6 if D == 0 else 4 if D < width - 1 else 2) if f == 1 else (6 if D == 0 else 5 if D < f else 4 if D < (width - 1) * f else 2)
                    art.put(x * f, y * f + D, GOLD[k])
    for DY in range(-16 * f, 40 * f):
        dy = DY / f
        reach = 9 if dy < 0 else 13 - dy * 0.12
        for DX in range(int(-reach * f), int(reach * f) + 1):
            dx = DX / f
            if dy < 0 and dx * dx + (dy + 8) ** 2 > 81:
                continue
            art.put(cx * f + DX, wy * f + DY, GOLD[6 if dx < -3 else 5 if dx < 4 else 3])
    # The throne, on the map's own steps of gold (three tiles of them): a high back that
    # ends in a point with a cushion of blue up it, a seat, arms on turned posts with a
    # ball on each, and at either hand a tall candlestick of gold with a flame.
    seat = H0 - 3 * T
    back_top = seat - 132
    for Y in range(int(back_top) * f, int(seat) * f):
        y = at(Y)
        yi = Y // f
        half = 27 if y > back_top + 24 else 6 + (y - back_top) * 0.9
        for X in range(int((cx - half) * f), int((cx + half) * f)):
            x = at(X)
            off = (x + 0.5 / f - cx) / half
            cushion = abs(x + 0.5 / f - cx) < 18 and back_top + 28 < y < seat - 40
            tuft = cushion and (int(math.floor(x - cx)) % 9 == 4) and (yi % 9 == 4) if f > 1 else cushion and (int(x - cx) % 9 == 4) and (y % 9 == 4)
            stud = not cushion and abs(abs(x + 0.5 / f - cx) - 22) < 1.5 and yi % 10 == 5 and y > back_top + 30
            art.put(X, Y, GOLD[6] if tuft or stud else tone(CLOTH, 4 + (1 if off < -0.2 else -1 if off > 0.5 else 0)) if cushion else tone(GOLD, 5 + round_shift(off)))
    for DY in range(-7 * f, 8 * f):  # a boss of blue in the back's point
        for DX in range(-7 * f, 8 * f):
            dx, dy = DX / f, DY / f
            if dx * dx + dy * dy <= 40:
                art.put(cx * f + DX, (back_top + 20) * f + DY, GOLD[6] if dx * dx + dy * dy > 26 else tone(CLOTH, 5 if dx + dy < 0 else 3))
    for side in (-1, 1):
        post = cx + side * 40
        for Y in range(int(seat - 52) * f, int(seat) * f):  # the arm's post
            y = at(Y)
            for X in range((int(post) - 6) * f, (int(post) + 6) * f):
                x = at(X)
                if f == 1:
                    swell = 1 if (seat - y) % 14 < 4 else 0
                    reach = 4 + swell * 2
                else:
                    phase = (seat - y) % 14
                    reach = 4 + 2 * max(0.0, math.sin(phase / 4 * math.pi)) if phase < 4 else 4  # a turned knop
                if abs(x + 0.5 / f - post) < reach:
                    art.put(X, Y, tone(GOLD, 5 + round_shift((x + 0.5 / f - post) / 6)))
        for X in range(int(cx + min(side * 26, side * 46)) * f, int(cx + max(side * 26, side * 46)) * f):
            for Y in range(int(seat - 47) * f, int(seat - 39) * f):
                y = at(Y)
                art.put(X, Y, GOLD[6 if (Y == int(seat - 47) * f if f > 1 else y == seat - 47) else 4 if y < seat - 41 else 2])
        for DY in range(-7 * f, 8 * f):
            for DX in range(-7 * f, 8 * f):
                dx, dy = DX / f, DY / f
                if dx * dx + dy * dy <= 49:
                    art.put(post * f + DX, (seat - 58) * f + DY, GOLD[6] if dx + dy < -4 else GOLD[4] if dx + dy < 4 else GOLD[2])
        # A candlestick: a foot, a stem with knops, a bowl, a flame.
        sx = cx + side * 74
        for Y in range(int(seat - 84) * f, int(seat) * f):
            y = at(Y)
            knop = (seat - y) % 22 < 5
            for X in range((int(sx) - (3 if knop else 1)) * f, (int(sx) + (3 if knop else 2)) * f):
                art.put(X, Y, GOLD[6 if X < sx * f else 3])
        for I in range(4 * f):  # its foot
            i = I / f
            for X in range(int((sx - 8 + i * 1.5) * f), int((sx + 8 - i * 1.5) * f)):
                art.put(X, (seat - 1) * f + (f - 1) - I, GOLD[5 if X < sx * f else 3])
        for I in range(8 * f):  # its bowl
            i = I / f
            for X in range(int((sx - 9 + i * 0.8) * f), int((sx + 9 - i * 0.8) * f)):
                x = at(X)
                art.put(X, (seat - 92) * f + I, GOLD[6 if x < sx - 2 else 4 if x < sx + 3 else 2])
        for DY in range(-12 * f, f):
            dy = DY / f
            reach = 3.2 * math.sin((dy + 12) / 12 * math.pi * 0.9) + 0.6
            for X in range(int((sx - reach) * f), int((sx + reach) * f) + 1):
                art.put(X, (seat - 93) * f + DY, AMBER[4 if abs(at(X) + 0.5 / f - sx) < 1.2 else 3 if dy > -9 else 2])
        if f == 1:
            _LAMPS.append((sx, seat - 106, 10, 14))
    for Y in range(int(seat - 36) * f, int(seat) * f):  # the seat and its front
        y = at(Y)
        for X in range(int(cx - 36) * f, int(cx + 36) * f):
            x = at(X)
            off = (x + 0.5 / f - cx) / 36
            k = 6 if y < seat - 32 else 5 + round_shift(off)
            panel = seat - 22 < y < seat - 5 and abs(off) < 0.86 and abs(abs(off) - 0.43) > (0.05 if f == 1 else 0.03)
            art.put(X, Y, tone(CLOTH, 5 if off < 0 else 4) if seat - 32 <= y < seat - 25 and abs(off) < 0.9 else tone(GOLD, k - (1 if panel else 0)))
    finial(art, cx, back_top + 2, 14, fine=f)
    # A tall window of cool glass at either hand.
    for side in (-1, 1):
        glass_window(art, cx + side * 8.6 * T, H0 - 18 * T, H0 - 4 * T, 34, r, warm=False, fine=f)
    # Up the tower: ranks of small windows with light in them, a band under each rank.
    for rank, y in enumerate(range(int(H0 - 24 * T), 3 * T, -7 * T)):
        for X in range(W0 * f):
            for d, k in enumerate((6, 5, 1)):
                for s in range(f):
                    art.put(X, (y + 3 * T + d) * f + s, SHADOWED[k])
        for k in range(-3, 4):
            if k == 0 and rank == 0:
                continue
            lit_window(art, cx + k * 3 * T, y, 12, 34, SHADOWED, fine=f)
    return art.image()


def gate(fine: int = 1) -> Image.Image:
    """The gate between a gatehouse's two towers, six tiles wide: a round arch of cut
    stones, a grate of gold drawn up into it, the wings of the host over it on a wall
    walk under battlements. `fine` draws it that many times the draft's size: a true
    arch, bars a pixel wide each side lit, joints a pixel wide."""
    r = rng("heaven gate")
    f = fine
    W0, H0 = 6 * T, 8 * T
    art = Art(W0 * f, H0 * f)
    cx, spring = W0 / 2, H0 - 3 * T
    ashlar(art, 0, 2 * T, W0, spring + 4, MARBLE, r, fine=f)
    for Y in range(int(spring - 3 * T) * f, H0 * f):
        y = Y / f
        for X in range(W0 * f):
            x = X / f
            d = math.hypot(x + 0.5 / f - cx, min(y - spring, 0))
            if d < 2.5 * T - 5:
                art.a[Y, X] = 0
            elif d < 2.5 * T and y <= spring:
                ang = math.atan2(y - spring, x + 0.5 / f - cx)
                art.put(X, Y, tone(MARBLE, 6 if int(ang * 7) % 2 else 4))
            elif y > spring and abs(x + 0.5 / f - cx) > 2.5 * T - 5:
                art.put(X, Y, tone(MARBLE, 5 if x < cx else 3))
    for x in range(int(cx - 2.5 * T + 6), int(cx + 2.5 * T - 6), 6):  # the grate, drawn up
        for Y in range(int(spring - 2.2 * T) * f, int(spring - 0.9 * T) * f):
            y = Y / f
            if math.hypot(x + 0.5 - cx, y - spring) < 2.5 * T - 5:
                if f == 1:
                    art.put(x, Y, GOLD[5])
                    art.put(x + 1, Y, GOLD[2])
                else:
                    for s, k in enumerate((6, 5, 3, 2)):
                        art.put(x * f + s, Y, GOLD[k])
    for X in range(W0 * f):
        x = X // f
        for Y in range(T * f, 2 * T * f):
            art.put(X, Y, tone(MARBLE, 6 if Y < T * f + (1 if f > 1 else 1) else 5))
        if (x // 8) % 2 == 0:
            for Y in range(4 * f, T * f):
                art.put(X, Y, tone(MARBLE, 5 if (x % 8 if f == 1 else X % (8 * f)) else 6))
        for s in range(f):
            art.put(X, 2 * T * f + s, MARBLE[2])
    for dx in range(2, 20):
        for dy in range(max(1, 6 - dx // 3)):
            for s in range(f):
                for u in range(f):
                    art.put((cx - dx) * f + s, (2.6 * T - dx // 2 + dy) * f + u, GOLD[6] if f > 1 and u == 0 else GOLD[5])
                    art.put((cx + dx - 1) * f + s, (2.6 * T - dx // 2 + dy) * f + u, GOLD[3])
    for DY in range(-2 * f, 8 * f):
        for s in range(f):
            art.put((cx - 1) * f + s, 2.6 * T * f + DY, GOLD[6])
            art.put(cx * f + s, 2.6 * T * f + DY, GOLD[4])
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


# --- The Orchard: a round temple, and the host in marble --------------------------------


def rotunda(fine: int = 1) -> Image.Image:
    """The round temple of the Orchard, 27 tiles wide: six columns in a ring on three
    steps, a band over them, and a dome of gold with ribs, a ring of small windows under
    it, a rod on its top. The sky shows between its columns. `fine` draws it that many
    times the draft's size: a true dome, its ribs a pixel wide, finer columns."""
    f = fine
    W0, H0 = 29 * T, 30 * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2
    floor = H0
    col_top = floor - 12 * T

    def at(V):
        return V / f if f > 1 else V

    # Three steps.
    for i, grow in enumerate((13.5, 12.8, 12.1)):
        for Y in range(int(floor - (i + 1) * 5) * f, int(floor - i * 5) * f):
            lit = Y == int(floor - (i + 1) * 5) * f
            for X in range(int((cx - grow * T) * f), int((cx + grow * T) * f)):
                art.put(X, Y, tone(MARBLE, (6 if lit else 4) + round_shift((at(X) + 0.5 / f - cx) / (grow * T))))
    # The far columns, in shade, then the near ones.
    for off in (-4.2, 4.2):
        column(art, cx + off * T, col_top + 6, floor - 15, 1.0 * T, MARBLE_FAR, fine=f)
    for off in (-10.5, -7.0, 7.0, 10.5):
        column(art, cx + off * T, col_top, floor - 15, 1.3 * T, fine=f)
    # The band: an architrave, a frieze with a line of gold, a cornice that stands out.
    for Y in range(int(col_top - 2 * T) * f, int(col_top) * f):
        row = at(Y) - (col_top - 2 * T)
        for X in range(int((cx - 12.6 * T) * f), int((cx + 12.6 * T) * f)):
            sh = round_shift((at(X) + 0.5 / f - cx) / (12.6 * T))
            k = 6 if row < 2 else 5 if row < 12 else 3 if row < 14 else 5 if row < 26 else 2
            art.put(X, Y, tone(MARBLE, k + sh))
            if 15 < row < 24 and (X // (4 * f)) % 3 == 0:
                art.put(X, Y, tone(MARBLE, 3 + sh))
        art.put(int((cx - 12.6 * T) * f), Y, MARBLE[1])
    for X in range(int((cx - 12.6 * T) * f), int((cx + 12.6 * T) * f)):
        for s in range(f):
            art.put(X, int(col_top - T) * f + s, GOLD[6] if f > 1 and s == 0 else GOLD[5])
    # The drum with its windows, and the dome.
    drum_top = col_top - 4 * T
    for Y in range(int(drum_top) * f, int(col_top - 2 * T) * f):
        for X in range(int((cx - 11 * T) * f), int((cx + 11 * T) * f)):
            art.put(X, Y, tone(MARBLE, 5 + round_shift((at(X) + 0.5 / f - cx) / (11 * T))))
    for k in range(-4, 5):
        lit_window(art, cx + k * 2.3 * T, drum_top + 6, 8, 18, MARBLE, fine=f)
    radius = 11 * T
    for Y in range(int(drum_top - radius) * f, int(drum_top) * f):
        y = at(Y)
        reach = math.sqrt(max(radius * radius - (drum_top - y) ** 2, 0))
        for X in range(int((cx - reach) * f), int((cx + reach) * f)):
            x = at(X)
            off = (x + 0.5 / f - cx) / max(reach, 1)
            # Lit like a ball: brightest up and to the left.
            lightness = -off * 0.55 + (drum_top - y) / radius * 0.5
            k = 3 + round(lightness * 3.2)
            rib = abs(((x - cx) / max(reach, 1)) * 5 % 1 - 0.5) > (0.44 if f == 1 else 0.465)
            art.put(X, Y, tone(GOLD, k - (2 if rib else 0)))
    for X in range(int(cx - radius - 2) * f, int(cx + radius + 2) * f):
        for s in range(f):
            art.put(X, int(drum_top) * f + s, GOLD[6 if X < cx * f else 3])
            art.put(X, (int(drum_top) + 1) * f + s, GOLD[2])
    finial(art, cx, drum_top - radius + 3, 4 * T, fine=f)
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def angel(variant: int = 0, fine: int = 1) -> Image.Image:
    """One of the host in white marble on a block, seven times the hero's height: a long
    robe in folds, hands on a sword point down before it, a bowed head, great wings
    raised, a ring of gold over its head. `variant` 1 faces the other way. `fine` draws
    it that many times the draft's size: each feather's edge a pixel wide, the folds finer,
    a ring of gold that is a true ring."""
    r = rng("heaven angel")
    f = fine
    W0, H0 = 12 * T, 17 * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2
    # The block it stands on.
    block_top = H0 - 3 * T
    block = Art(W0, H0)
    ashlar(block, cx - 2.6 * T, block_top, cx + 2.6 * T, H0, MARBLE, r, shift=lambda x: round_shift((x + 0.5 - cx) / (2.6 * T)))
    art.a = np.kron(block.a, np.ones((f, f, 1), dtype=np.uint8)) if f > 1 else block.a
    for X in range(int((cx - 2.9 * T) * f), int((cx + 2.9 * T) * f)):
        for row, k in enumerate((6, 5, 2)):
            for s in range(f):
                art.put(X, (int(block_top) + row) * f + s, MARBLE[k])
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
                for I in range(int(length * f)):
                    i = I / f
                    x = root_x + side * i * lean
                    y = root_y + i
                    width = 6 if i < length - 6 else max(1, int(length - i))
                    for D in range(width * f):
                        d = D / f
                        if f == 1:
                            k = 6 if D == (0 if side < 0 else width - 1) else 5 if I % 9 else 4
                        else:
                            edge = D == (0 if side < 0 else width * f - 1)
                            k = 6 if edge else 4 if I % (9 * f) < 1 else 5
                        art.put((x - width // 2) * f + D, y * f, tone(MARBLE, k - shade_k - (1 if side > 0 else 0)))
        # The wing's arm: the bone the feathers hang from.
        for a in range(0, 100 * f):
            t = a / (100 * f - 1)
            x = cx + side * (9 + t * 58)
            y = shoulder - 6 - math.sin(t * math.pi * 0.62) * 62
            for D in range(-3 * f, 4 * f):
                art.put(x * f, y * f + D, tone(MARBLE, 6 if D < 0 else 4 if f == 1 or D < 3 * f else 3))
    # The robe: a bell of folds from the shoulders to the block.
    for Y in range(int(shoulder * f), int(block_top * f)):
        y = Y / f
        t = (y - shoulder) / (block_top - shoulder)
        half = 15 + t * 22 + 3 * math.sin(t * 9)
        for X in range(int((cx - half) * f), int((cx + half) * f)):
            off = (X / f + 0.5 / f - cx) / half
            fold = math.sin(off * 9 + t * 2.5)
            k = 5 + round_shift(off) + (1 if fold > 0.6 else -1 if fold < -0.5 else 0)
            if f > 1 and abs(fold) < 0.06:
                k -= 1  # the line a fold turns on
            art.put(X, Y, tone(MARBLE, k))
    # Arms down to the hilt, the sword point down.
    hilt = shoulder + 3.4 * T
    for side in (-1, 1):
        for I in range(int(3.2 * T * f)):
            t = I / (3.2 * T * f)
            x = cx + side * (15 - t * 13)
            y = shoulder + 6 + I / f
            for D in range(-3 * f, 4 * f):
                d = D / f
                art.put(x * f + D, y * f, tone(MARBLE, 6 if d < -1 else 5 if d < 2 else 3))
    for Y in range(int(hilt * f), int((block_top - 4) * f)):
        for s, k in enumerate((6, 4, 2) if f == 1 else (6, 6, 5, 4, 3, 2)):
            art.put((cx - 1) * f + s, Y, MARBLE[k])
    for X in range(int((cx - 9) * f), int((cx + 9) * f)):
        for s in range(f):
            art.put(X, int(hilt) * f + s, GOLD[6] if f > 1 and s == 0 else GOLD[5])
            art.put(X, (int(hilt) + 1) * f + s, GOLD[3])
    # The head, bowed, and its hair; the ring over it.
    head_y = shoulder - 11
    for DY in range(-10 * f, 11 * f):
        for DX in range(-9 * f, 10 * f):
            dx, dy = DX / f, DY / f
            if dx * dx + dy * dy <= 86:
                art.put(cx * f + DX, head_y * f + DY, tone(MARBLE, 6 if dx + dy < -5 else 5 if dx < 4 else 3))
    for DX in range(-10 * f, 11 * f):
        for DY in range(-11 * f, -2 * f):
            dx, dy = DX / f, DY / f
            if dx * dx + (dy + 2) ** 2 <= 100 and dy < -4 + abs(dx) * 0.3:
                k = 4 if dx < 2 else 2
                if f > 1 and (DX + DY // 2) % 4 == 0:
                    k -= 1  # a lock of its hair
                art.put(cx * f + DX, head_y * f + DY, tone(MARBLE, k))
    for a in range(720 * f):
        ang = a / (720 * f) * 2 * math.pi
        for rad, k in ((13, 6), (14, 4)) if f == 1 else ((12.8, 6), (13.4, 6), (14.0, 4), (14.6, 4)):
            art.put((cx + math.cos(ang) * rad) * f, (head_y - 19 + math.sin(ang) * rad * 0.36) * f, GOLD[k])
    for _ in range(f):
        art.outline(MARBLE[0])
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if variant else image


# --- What stands about a castle: gates, bridges, battlements, rooms ---------------------------


def host_wings(art: Art, cx, wy, size: float = 1.0, feathers: int = 9, fine: int = 1):
    """The wings of the host in gold: feathers fanned up and out from a middle, the
    longest at the tip. The places are in the draft's pixels; `art` is `fine` times the
    draft's size, and at more than one each feather is lit along its upper edge."""
    f = fine
    for side in (-1, 1):
        for feather in range(feathers):
            ang = math.radians(8 + feather * 58 / max(feathers - 1, 1))
            length = (16 + feather * 5) * size
            root_x, root_y = cx + side * (3 + feather * 1.6) * size, wy + (12 - feather * 1.1) * size
            for I in range(int(length * f)):
                i = I / f
                x = root_x + side * math.cos(ang) * i
                y = root_y - math.sin(ang) * i
                width = 3 if i < length * 0.75 else 2
                for D in range(width * f):
                    k = (6 if D == 0 else 4 if D < width - 1 else 2) if f == 1 else (6 if D == 0 else 5 if D < f else 4 if D < (width - 1) * f else 2)
                    art.put(x * f, y * f + D, GOLD[k])
    for DY in range(int(-4 * size) * f, int(14 * size) * f):
        for s in range(f):
            art.put((cx - 1) * f + s, wy * f + DY, GOLD[6])
            art.put(cx * f + s, wy * f + DY, GOLD[4])


def stones(x: int, y: int, light: int = 0) -> int:
    """The shade of a wall of cut stone at a pixel: courses eight high, a joint under and after each block."""
    joint = y % 8 == 7 or (x + (y // 8 % 2) * 6) % 12 == 11
    return (3 if joint else 6 if y % 8 == 0 else 5) + light


def fine_stones(X: int, Y: int, f: int, light: int = 0) -> int:
    """`tv2_heaven.stones` at `f` times the draft's size: the same courses and blocks, the
    joint a pixel wide and the lit edge of each course a pixel high, as cut stone is."""
    if f == 1:
        return stones(X, Y, light)
    course, block = 8 * f, 12 * f
    joint = Y % course == course - 1 or (X + (Y // course % 2) * 6 * f) % block == block - 1
    return (3 if joint else 6 if Y % course == 0 else 5) + light


def portal(apart: int, rows: int, wide: int = 2, fine: int = 1) -> Image.Image:
    """A gate that stands free: two columns `apart` tiles from middle to middle and `rows`
    tiles high, a round arch of cut stones sprung from them, a band with a line of gold
    and the wings of the host over it, a rod on its top. `fine` draws it that many times
    the draft's size: a true arch, its joints a pixel wide."""
    f = fine
    col = wide * T * 0.62
    radius = apart * T / 2
    inner, outer = radius - col / 2, radius + col / 2 + 2
    W0 = (apart + wide + 3) * T
    H0 = int(rows * T + outer + 1.5 * T) + 3 * T + 4
    art = Art(W0 * f, H0 * f)
    cx, spring = W0 / 2, H0 - rows * T
    top = int(spring - outer - 1.5 * T)
    for Y in range((top + int(1.5 * T)) * f, int(spring) * f):
        y = Y / f
        for X in range(int(cx - outer) * f, int(cx + outer) * f):
            x = X / f
            d = math.hypot(x + 0.5 / f - cx, y - spring)
            if d < inner:
                continue
            if d < inner + 6:
                ang = math.atan2(y - spring, x + 0.5 / f - cx)
                art.put(X, Y, MARBLE[2] if d < inner + 1 / f else tone(MARBLE, 6 if int(ang * (apart + 2)) % 2 else 4))
            else:
                art.put(X, Y, tone(MARBLE, fine_stones(X, Y, f, round_shift((x + 0.5 / f - cx) / outer))))
    for Y in range(top * f, (top + int(1.5 * T)) * f):
        row = Y / f - top
        for X in range(int(cx - outer - 5) * f, int(cx + outer + 5) * f):
            k = 6 if row < 2 else 2 if row > 1.5 * T - 3 else 5
            gold = GOLD[5] if 5 <= row < 6 else GOLD[2] if 6 <= row < 7 else None
            if f > 1 and gold is not None and Y % f == 0 and row < 6:
                gold = GOLD[6]
            art.put(X, Y, gold if gold is not None else tone(MARBLE, k + round_shift((X / f + 0.5 / f - cx) / (outer + 5))))
    host_wings(art, cx, top + 8, 0.55 if apart < 6 else 0.8, 6, fine=f)
    for side in (-1, 1):
        column(art, cx + side * radius, spring, H0, col, fine=f)
    finial(art, cx, top + 1, fine=f)
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def bridge(wide: int, fine: int = 1) -> Image.Image:
    """A bridge high between two towers, `wide` tiles long: a walk under battlements, on a
    round arch of cut stones with a boss of gold at its crown. `fine` draws it that many
    times the draft's size: a true arch, joints a pixel wide."""
    r = rng("heaven bridge", wide)
    f = fine
    rise = wide // 2
    W0, H0 = (wide + 2) * T, (4 + rise) * T
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2 + (T / 2 if wide % 2 == 0 else 0)
    x0, x1 = int(cx - wide * T / 2), int(cx + wide * T / 2)
    radius = wide * T / 2 - 3
    ashlar(art, x0, T, x1, H0, MARBLE, r, shift=lambda x: -1, fine=f)
    for Y in range(T * f, H0 * f):
        y = Y / f
        for X in range(x0 * f, x1 * f):
            x = X / f
            d = math.hypot(x + 0.5 / f - cx, y - H0)
            if d < radius:
                art.a[Y, X] = 0
            elif d < radius + 5:
                ang = math.atan2(y - H0, x + 0.5 / f - cx)
                art.put(X, Y, MARBLE[1] if d < radius + 1 / f else tone(MARBLE, 5 if int(ang * (wide + 3)) % 2 else 3))
    for X in range((x0 - 2) * f, (x1 + 2) * f):  # the walk's lip, and its shadow
        xi = X // f
        for s in range(f):
            for d, k in enumerate((6, 5, 5, 2)):
                art.put(X, (T + d) * f + s, MARBLE[k])
            if (xi - x0) % 6 < 3:  # brackets under the lip
                art.put(X, (T + 4) * f + s, MARBLE[4])
                art.put(X, (T + 5) * f + s, MARBLE[3])
    for X in range(x0 * f, x1 * f):  # merlons
        xi = X // f
        k = (xi - x0) % 8
        if (xi - x0) // 8 % 2 == 0:
            for Y in range(5 * f, T * f):
                y = Y // f
                art.put(X, Y, tone(MARBLE, 6 if (Y == 5 * f if f > 1 else y == 5) or (k == 0 and X % f == 0) else 3 if k == 7 else 5))
        else:
            for Y in range((T - 4) * f, T * f):
                art.put(X, Y, tone(MARBLE, 5 if Y < (T - 4) * f + 1 else 4))
    for DY in range(-3 * f, 3 * f + 1):  # the boss at the crown
        for DX in range(-3 * f, 3 * f + 1):
            dx, dy = DX / f, DY / f
            if dx * dx + dy * dy <= 9:
                art.put(cx * f + DX, (H0 - radius - 4) * f + DY, GOLD[6] if dx + dy < -2 else GOLD[4] if dx + dy < 2 else GOLD[2])
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def parapet(cols: int, fine: int = 1) -> Image.Image:
    """The battlements along a wall walk, `cols` tiles long and a tile high: merlons of cut
    stone, each with a lit top and a side in shade, a low wall between them. `fine` draws
    them that many times the draft's size, their edges a pixel wide."""
    r = rng("heaven parapet", cols)
    f = fine
    W0, H0 = cols * T, T
    art = Art(W0 * f, H0 * f)
    block = 5
    for x in range(W0):
        k = x % 16
        if k < 9:
            block = r.choice([5, 5, 6]) if k == 0 else block  # each merlon its own shade
            for Y in range(3 * f, H0 * f):
                y = Y // f
                for s in range(f):
                    first, last = s == 0, s == f - 1
                    top_row = Y == 3 * f if f > 1 else y == 3
                    edge = 6 if top_row else 6 if k == 0 and (f == 1 or first) else 3 if k == 8 and (f == 1 or last) else block if y != 10 or (f > 1 and Y % f) else 3
                    art.put(x * f + s, Y, tone(MARBLE, edge))
        else:
            for Y in range((H0 - 6) * f, H0 * f):
                y = Y // f
                for s in range(f):
                    art.put(x * f + s, Y, tone(MARBLE, 5 if (Y == (H0 - 6) * f if f > 1 else y == H0 - 6) else 3 if (Y == H0 * f - 1 if f > 1 else y == H0 - 1) else 4))
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def span(fine: int = 1) -> Image.Image:
    """The bridge of marble from the Garden east, eighteen tiles long: a rail of balusters
    over its deck (the map's own tiles, which this is drawn behind), and under the deck two
    round arches of cut stones on three piers that end in the air, the middle one longest.
    `fine` draws it that many times the draft's size: true arches, balusters that swell."""
    r = rng("heaven span")
    f = fine
    W0, H0 = 20 * T, 11 * T
    art = Art(W0 * f, H0 * f)
    x0 = T + T // 2  # the deck's west end (an even number of tiles: it stands in the cell left of its middle)
    under = 2 * T  # the deck's underside
    spring = under + 4 * T
    for X in range(x0 * f, (x0 + 18 * T) * f):
        rel = (X / f - x0) / T
        for Y in range(under * f, (spring + 1) * f):
            y = Y / f
            # The wall over the arches, the arches left open.
            open_ = any(math.hypot(rel - mid, (y - spring) / T) < 3.5 for mid in (4.5, 13.5))
            near = any(3.5 <= math.hypot(rel - mid, (y - spring) / T) < 3.5 + 5 / T for mid in (4.5, 13.5))
            if open_:
                continue
            if near:
                mid = 4.5 if rel < 9 else 13.5
                ang = math.atan2(y - spring, rel - mid)
                art.put(X, Y, tone(MARBLE, 5 if int(ang * 10) % 2 else 3))
            else:
                art.put(X, Y, tone(MARBLE, fine_stones(X, Y, f, -1)))
    for west, east, prow in ((0, 1, 6), (8, 10, 9), (17, 18, 6)):
        px0, px1 = x0 + west * T, x0 + east * T
        pcx, half = (px0 + px1) / 2, (px1 - px0) / 2
        foot = under + prow * T
        ashlar(art, px0, spring - T, px1, foot, MARBLE, r, shift=lambda x, pcx=pcx, half=half: round_shift((x + 0.5 - pcx) / half), fine=f)
        for I in range(T * f):  # its foot narrows to a point
            i = I / f
            Y = foot * f - 1 - I
            reach = half * (i / T) ** 0.7
            for X in range(px0 * f, px1 * f):
                if abs(X / f + 0.5 / f - pcx) > reach:
                    art.a[Y, X] = 0
        for X in range((px0 - 2) * f, (px1 + 2) * f):  # an impost where the arches spring
            for d, k in enumerate((6, 4, 2)):
                for s in range(f):
                    art.put(X, (spring - T + d) * f + s, MARBLE[k])
    for X in range(x0 * f, (x0 + 18 * T) * f):  # the shadow the deck casts
        for Y in range(under * f, (under + 3) * f):
            if art.has(X, Y):
                art.put(X, Y, MARBLE[2 if Y < (under + 2) * f else 3])
    # The rail: posts with a cap, a handrail, balusters between.
    for X in range(x0 * f, (x0 + 18 * T) * f):
        x = X // f
        k = (x - x0) % (3 * T)
        post = k < 5 or x >= x0 + 18 * T - 5
        for d, shade in enumerate((6, 5, 3)):
            for s in range(f):
                art.put(X, (4 + d) * f + s, MARBLE[shade])
        if post:
            for Y in range(f, T * f):
                y = Y // f
                lit = k % 5 == 0 and (f == 1 or X % f == 0)
                art.put(X, Y, tone(MARBLE, 6 if lit or (Y == f if f > 1 else y == 1) else 3 if k == 4 and (f == 1 or X % f == f - 1) else 5))
        elif f == 1 and (x - x0) % 5 in (1, 2):
            for y in range(7, T - 2):
                art.put(X, y, MARBLE[5 if (x - x0) % 5 == 1 else 3])
        elif f > 1:
            u = (X / f - x0) % 5  # where in a baluster's five pixels this column is
            for Y in range(7 * f, (T - 2) * f):
                t = (Y / f - 7) / (T - 9)
                swell = 1.0 + 0.45 * math.sin(t * math.pi)  # a baluster swells to its middle
                if abs(u - 2.0) < swell:
                    art.put(X, Y, MARBLE[5 if u < 2.0 else 3])
        for Y in range((T - 2) * f, T * f):
            art.put(X, Y, MARBLE[5 if Y < (T - 2) * f + (1 if f > 1 else 1) else 4])
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def chamber(name: str, cols: int, rows: int, windows, wings=None, fine: int = 1) -> Image.Image:
    """The wall at the back of a room, `cols` by `rows` tiles: marble out of the sun in
    courses, a cornice under its ceiling, a rail and a skirting by its floor, and its
    windows, each (column from the west, rows from the top to its head, rows to its sill,
    kind): `sky` an opening the sky shows through, in a frame the sun reaches; `warm`
    and `cool` glass in lead; `lamp` a small window with a light behind it. `wings`: where
    the wings of the host hang in gold (column, rows from the top). `fine` draws it that
    many times the draft's size."""
    r = rng("heaven chamber", name)
    f = fine
    W0, H0 = cols * T, rows * T
    art = Art(W0 * f, H0 * f)
    wall(art, 0, 0, W0, H0, r, fine=f)
    for X in range(W0 * f):
        x = X // f
        for y, k in ((0, 6), (1, 5), (2, 5), (3, 2), (4, 3)):
            for s in range(f):
                plain = x % 8 if f == 1 else X % (8 * f)
                art.put(X, y * f + s, SHADOWED[k] if plain or y > 2 else SHADOWED[3])
        for d, k in enumerate((6, 5, 1)):
            for s in range(f):
                art.put(X, (H0 - 2 * T - 8 + d) * f + s, SHADOWED[k])
        for Y in range((H0 - 6) * f, H0 * f):
            art.put(X, Y, SHADOWED[6 if Y < (H0 - 6) * f + 1 else 4 if (x % 24 if f == 1 else X % (24 * f)) else 2])
    # Panels between the rail and the skirting.
    for x in range(4, W0 - 4, 2 * T):
        for Y in range((H0 - 2 * T - 2) * f, (H0 - 9) * f):
            for s in range(f):
                art.put(x * f + s, Y, SHADOWED[2])
                art.put((x + 1) * f + s, Y, SHADOWED[5] if f == 1 or s else SHADOWED[6])
    for col, head, sill, kind in windows:
        cx, top, foot = col * T + T / 2, head * T + 2, sill * T
        if kind == "lamp":
            lit_window(art, cx, top, 10, foot - top - 4, SHADOWED, fine=f)
        elif kind == "sky":
            wide = 3 * T - 10
            half = wide / 2
            for Y in range(int(top) * f, int(foot) * f):
                y = Y / f
                up = top + wide * 0.9 - y
                reach = half if up <= 0 else half * math.sqrt(max(1 - (up / (wide * 0.9)) ** 2, 0))
                x0, x1 = int(math.floor((cx - reach) * f)), int(math.ceil((cx + reach) * f))
                for X in range(x0, x1):
                    art.a[Y, X] = 0
                for side in (-1, 1):  # the frame, and the depth of the wall the sun lights on one side
                    for k, shade in enumerate((6, 5, 3) if side > 0 else (3, 5, 6)):
                        if f == 1:
                            art.put(int(cx + side * (reach + 0.5 + k)), Y, MARBLE[shade])
                        else:
                            for s in range(f):
                                art.put((x1 + k * f + s) if side > 0 else (x0 - 1 - k * f - s), Y, MARBLE[shade])
            for X in range(int(cx - half - 4) * f, int(cx + half + 4) * f):
                for d, k in enumerate((6, 5, 3, 2)):
                    for s in range(f):
                        art.put(X, (int(foot) + d) * f + s, MARBLE[k])
        else:
            glass_window(art, cx, top, foot, 3 * T - 10, r, warm=kind == "warm", fine=f)
    if wings:
        host_wings(art, wings[0] * T + T / 2, wings[1] * T, 1.5, 11, fine=f)
    return art.image()


# --- What grows and stands in a garden --------------------------------------------------------


def flowers(variant: int, fine: int = 1) -> Image.Image:
    """A bed of flowers, a tile high and three to five long: stems and leaves, and heads of
    pink, white, gold, blue and violet, no two beds the same. `fine` draws it that many
    times the draft's size: leaves that are lit at their tips, thin stems, and each head
    a flower of five petals round its eye."""
    r = rng("heaven flowers", variant)
    f = fine
    wide = (4, 3, 5, 4)[variant % 4]
    W0, H0 = wide * T, T + 6
    art = Art(W0 * f, H0 * f)

    def block(x, y, colour, lit=None):
        for s in range(f):
            for t in range(f):
                art.put(x * f + s, y * f + t, lit if lit is not None and s == 0 and t == 0 else colour)

    for x in range(2, W0 - 2):
        tall = r.randint(3, 6)
        for y in range(H0 - 2 - tall, H0):
            if r.random() < 0.8:
                k = r.choice([2, 3, 3, 4])
                block(x, y, VINE[k], VINE[min(k + 1, 6)] if f > 1 and y == H0 - 2 - tall else None)
    for _ in range(wide * 5):
        x, y = r.randint(3, W0 - 4), r.randint(3, H0 - 8)
        ramp = FLOWERS[r.randrange(len(FLOWERS)) if variant % 4 else r.choice([0, 0, 1, 2])]
        for yy in range((y + 2) * f, (H0 - 3) * f):
            art.put(x * f, yy, VINE[2])
        if r.random() < 0.6:
            side = r.choice([-1, 1])
            if f == 1:
                art.put(x + side, y + 4, VINE[4])
            else:
                for i in range(1, 2 * f + 1):
                    art.put(x * f + side * i, (y + 4) * f - i // 2, VINE[4] if i < 2 * f else VINE[5])
        eye = GOLD[5] if ramp is not FLOWERS[2] else GOLD[2]
        if f == 1:
            for dx, dy, k in ((0, 0, 3), (-1, 0, 2), (1, 0, 2), (0, -1, 3), (0, 1, 1)):
                art.put(x + dx, y + dy, ramp[k - 1])
            art.put(x, y, eye)
            continue
        X, Y = x * f, y * f
        for dx, dy, k in (
            (0, -2, 3), (1, -2, 3), (-2, -1, 3), (-1, -1, 3), (2, -1, 2), (3, -1, 2),
            (-2, 0, 2), (-1, 0, 3), (2, 0, 2), (3, 0, 1), (-1, 1, 2), (0, 2, 1), (1, 2, 1), (2, 1, 1),
        ):  # fmt: skip
            art.put(X + dx, Y + dy, ramp[k - 1])
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            art.put(X + dx, Y + dy, eye)
    return art.image()


def hedge(variant: int, fine: int = 1) -> Image.Image:
    """Green that is kept: 0 a low hedge clipped square with blossom in it, 1 a ball on a
    stem, 2 a tree clipped to three rounds one over another. `fine` draws it that many
    times the draft's size: leaf by leaf, and blossom with a lit petal."""
    r = rng("heaven hedge", variant)
    f = fine
    if variant == 0:
        W0, H0 = 5 * T, 2 * T
        art = Art(W0 * f, H0 * f)
        for i in range(5):
            bundle(art, (12 + i * 14) * f, (H0 - 12 + r.uniform(-1, 1)) * f, 11 * f, 11 * f, VINE, r, ("hedge", i), gaps=False)
        for Y in range((H0 - 3) * f, H0 * f):
            for X in range(6 * f, (W0 - 6) * f):
                art.put(X, Y, VINE[1])
        for _ in range(12):
            x, y = r.randint(6, W0 - 7), r.randint(H0 - 20, H0 - 6)
            if art.has(x * f, y * f):
                if f == 1:
                    art.put(x, y, BLOSSOM[6])
                    art.put(x + 1, y, BLOSSOM[4])
                    continue
                for dx, dy, k in ((0, 0, 6), (1, 0, 6), (2, 0, 5), (0, 1, 5), (1, 1, 4), (2, 1, 4), (1, -1, 5)):
                    art.put(x * f + dx, y * f + dy, BLOSSOM[k])
    elif variant == 1:
        W0, H0 = 3 * T, 3 * T + 4
        art = Art(W0 * f, H0 * f)
        trunk(art, (W0 // 2) * f, H0 * f - 1, (T + 10) * f, 5 * f, 4 * f, BIRCH, r, roots=False)
        bundle(art, W0 / 2 * f, (H0 - T - 22) * f, 15 * f, 15 * f, VINE, r, ("ball",), gaps=False)
    else:
        W0, H0 = 3 * T, 5 * T
        art = Art(W0 * f, H0 * f)
        trunk(art, (W0 // 2) * f, H0 * f - 1, T * f, 5 * f, 4 * f, BIRCH, r, roots=False)
        for cy, rad in ((H0 - T - 10, 17), (H0 - T - 32, 13), (H0 - T - 50, 9)):
            bundle(art, W0 / 2 * f, cy * f, rad * f, rad * 0.8 * f, VINE, r, ("cone", rad), gaps=False)
    for _ in range(f):
        art.outline(VINE[0])
    return art.image()


def bench(fine: int = 1) -> Image.Image:
    """A bench of marble: a slab on two legs that curl, a line of gold along its edge.
    `fine` draws it that many times the draft's size: the slab's moulding, a sheen on the
    gold, legs that swell in a curve."""
    f = fine
    W0, H0 = 3 * T + 8, T + 2
    art = Art(W0 * f, H0 * f)
    rows = ((4, MARBLE[6], MARBLE[6]), (5, GOLD[6], GOLD[4]), (6, MARBLE[5], MARBLE[6]), (7, MARBLE[4], MARBLE[4]), (8, MARBLE[2], MARBLE[3]))
    for X in range(2 * f, (W0 - 2) * f):
        for y, first, second in rows:
            for s in range(f):
                art.put(X, y * f + s, (first if s == 0 else second) if f > 1 else (GOLD[5] if y == 5 else first))
    for lx in (9, W0 - 10):
        for Y in range(9 * f, H0 * f):
            y = Y / f
            if f == 1:
                swell = 3 + (2 if y > H0 - 4 else 1 if y < 12 else 0)
            else:
                swell = 3 + 2 * max(0.0, (y - (H0 - 5)) / 4) ** 1.5 + 1.0 * max(0.0, (12 - y) / 3)
            for X in range(int((lx - swell) * f), int((lx + swell) * f)):
                x = X / f
                art.put(X, Y, tone(MARBLE, 5 if x < lx - 1 else 4 if x < lx + 1 else 3))
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def urn(fine: int = 1) -> Image.Image:
    """An urn of marble banded with gold on a block of stone, flowers spilling out of it.
    `fine` draws it that many times the draft's size: a smooth belly, bands with a
    sheen, leaf by leaf in what spills out of it."""
    r = rng("heaven urn")
    f = fine
    W0, H0 = 2 * T, 3 * T
    block = Art(W0, H0)
    cx = W0 / 2
    ashlar(block, cx - 9, H0 - T, cx + 9, H0, MARBLE, r, shift=lambda x: round_shift((x + 0.5 - cx) / 9))
    art = Art(W0 * f, H0 * f)
    art.a = np.kron(block.a, np.ones((f, f, 1), dtype=np.uint8)) if f > 1 else block.a
    for X in range(int(cx - 11) * f, int(cx + 11) * f):
        for s in range(f):
            art.put(X, (H0 - T) * f + s, MARBLE[6])
            art.put(X, (H0 - T + 1) * f + s, MARBLE[3])
    profile = (4, 3, 3, 5, 7, 8, 9, 9, 9, 8, 7, 6, 5, 4, 4, 5, 7, 8)
    for I in range(18 * f):  # the urn: a foot, a belly, a neck, a lip
        i = I / f
        n = int(i)
        half = profile[n] if f == 1 else profile[n] + (profile[min(n + 1, 17)] - profile[n]) * (i - n)
        Y = (H0 - T) * f - 1 - I
        band = n in (9, 16)
        for X in range(int((cx - half) * f), int((cx + half) * f)):
            x = X / f
            if band:
                colour = GOLD[(6 if f > 1 and I % f == f - 1 and x < cx else 5) if x < cx else 3]
            else:
                colour = tone(MARBLE, 5 + round_shift((x + 0.5 / f - cx) / half))
            art.put(X, Y, colour)
    for k, (dx, dy, rad) in enumerate(((0, -6, 8), (-6, -2, 6), (6, -2, 6))):
        bundle(art, (cx + dx) * f, (H0 - T - 20 + dy) * f, rad * f, rad * f, VINE, r, ("urn", k), gaps=False)
    for _ in range(16):
        x, y = int(cx) + r.randint(-11, 11), H0 - T - 20 + r.randint(-12, 4)
        if art.has(x * f, y * f):
            ramp = FLOWERS[r.randrange(3)]
            if f == 1:
                art.put(x, y, ramp[2])
                art.put(x + 1, y, ramp[1])
                continue
            for dx, dy, k in ((0, 0, 2), (1, 0, 2), (2, 0, 1), (0, 1, 1), (1, 1, 1), (1, -1, 2)):
                art.put(x * f + dx, y * f + dy, ramp[k])
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def fountain(frame: int, fine: int = 1) -> Image.Image:
    """A fountain of marble: a wide basin, a stem, a bowl, a smaller bowl, a jet out of the
    top, and water running over the lip of each bowl into the one under it, which the
    frames move. `fine` draws it that many times the draft's size: bowls that curve, the
    basin's joints a pixel wide, threads of water a pixel wide."""
    r = rng("heaven fountain")
    f = fine
    W0, H0 = 8 * T, 6 * T + 8
    art = Art(W0 * f, H0 * f)
    cx = W0 / 2

    def bowl(y, half, deep):
        for I in range(deep * f):
            i = I / f
            reach = half - (i / deep) ** 2 * half * 0.55
            for X in range(int((cx - reach) * f), int((cx + reach) * f)):
                art.put(X, y * f + I, tone(MARBLE, (6 if I < f and (f == 1 or I == 0) else 5) + round_shift((X / f + 0.5 / f - cx) / reach)))
        for X in range(int((cx - half + 2) * f), int((cx + half - 2) * f)):
            x = X // f
            for s in range(f):
                art.put(X, (y - 1) * f + s, WATER[3 if (x + frame * 2) % 5 else 4])

    def falls(y0, y1, half):
        for side in (-1, 1):
            for k in range(3):
                x = int(cx + side * (half - 1 - k * 3))
                for Y in range(int(y0) * f, int(y1) * f):
                    y = Y // f
                    if (y + frame * 3 + k * 2) % 7 < 5:
                        colour = WATER[4 if (y + frame * 3) % 7 < 2 else 3 if k else 2]
                        art.put(x * f, Y, colour, 235)
                        if f > 1 and k == 0:
                            art.put(x * f + 1, Y, WATER[2], 200)

    for Y in range(int(H0 - 14) * f, H0 * f):  # the basin, of cut stone, with a lip
        for X in range(int(cx - 60) * f, int(cx + 60) * f):
            art.put(X, Y, tone(MARBLE, fine_stones(X, Y, f, round_shift((X / f + 0.5 / f - cx) / 60)) if f > 1 else stones(X, Y, round_shift((X + 0.5 - cx) / 60))))
    for X in range(int(cx - 62) * f, int(cx + 62) * f):
        x = X // f
        for s in range(f):
            art.put(X, (H0 - 16) * f + s, MARBLE[6])
            art.put(X, (H0 - 15) * f + s, GOLD[5] if (x % 2 if f == 1 else s == 0) else MARBLE[5] if f == 1 else GOLD[3])
            art.put(X, (H0 - 14) * f + s, MARBLE[3])
            if abs(x - cx) < 58:
                art.put(X, (H0 - 17) * f + s, WATER[3 if (x + frame * 2) % 6 else 4])
    for Y in range(int(H0 - 74) * f, int(H0 - 16) * f):  # the stem
        y = Y // f
        half = 5 + (2 if (H0 - y) % 20 < 4 else 0)
        for X in range(int((cx - half) * f), int((cx + half) * f)):
            art.put(X, Y, tone(MARBLE, 5 + round_shift((X / f + 0.5 / f - cx) / half)))
    falls(H0 - 50, H0 - 17, 34)
    bowl(H0 - 52, 36, 9)
    falls(H0 - 78, H0 - 53, 20)
    bowl(H0 - 80, 22, 7)
    for I in range(22 * f):  # the jet, and what falls back from it
        i = I / f
        Y = (H0 - 82) * f - I
        spread = (i / 22) ** 2 * 6
        for side in (-1, 0, 1):
            if (int(i) + frame * 2 + side) % 4:
                art.put((cx + side * spread) * f, Y, WATER[4 if side == 0 else 3], 240)
    for _ in range(10):
        x, y = cx + r.randint(-58, 58), H0 - 18 - (frame + r.randint(0, 2)) % 3
        for s in range(f):
            art.put(x * f + s, y * f, WATER[4], 220)
    for _ in range(f):
        art.outline(MARBLE[0])
    return art.image()


def swan(variant: int, fine: int = 1) -> Image.Image:
    """A swan on the water: a white body, a neck in a curve, a beak of gold. `variant` 1
    looks the other way. `fine` draws it that many times the draft's size: a smooth
    body and neck, an eye, the water it parts in a pixel's ripple."""
    f = fine
    W0, H0 = 28, 24
    art = Art(W0 * f, H0 * f)
    for Y in range((H0 - 9) * f, (H0 - 1) * f):
        y = Y / f
        for X in range(3 * f, 22 * f):
            x = X / f
            if ((x - 12) / 9.5) ** 2 + ((y - (H0 - 5)) / 4.2) ** 2 <= 1:
                art.put(X, Y, SWAN[4 if y < H0 - 5 and x < 14 else 3 if y < H0 - 3 else 2])
    for x, y in ((6, H0 - 10), (5, H0 - 11), (4, H0 - 12), (7, H0 - 11)):  # a wing lifted a little
        for s in range(f):
            for u in range(f):
                art.put(x * f + s, y * f + u, SWAN[4])
    for I in range(12 * f):  # the neck
        i = I / f
        x = 19 + 2.2 * math.sin(i / 11 * math.pi)
        x = round(x) if f == 1 else x
        for s in range(f):
            art.put(x * f + s, (H0 - 8) * f - I + (f - 1), SWAN[4])
            art.put((x + 1) * f + s, (H0 - 8) * f - I + (f - 1), SWAN[3])
    for dx, dy in ((0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)):
        for s in range(f):
            for u in range(f):
                art.put((19 + dx) * f + s, (H0 - 21 + dy) * f + u, SWAN[4])
    for (x, y), colour in (((22, H0 - 20), GOLD[4]), ((23, H0 - 20), GOLD[3]), ((21, H0 - 21), LEAD)):
        for s in range(f):
            for u in range(f):
                art.put(x * f + s, y * f + u, colour)
    if f > 1:
        art.put(21 * f, (H0 - 21) * f, (255, 255, 255))  # a light in its eye
    for X in range(f, 25 * f):  # the water it parts
        x = X // f
        if (x % 3 if f == 1 else X % 3):
            for s in range(f):
                art.put(X, (H0 - 1) * f + s, WATER[3] if f == 1 or s == 0 else WATER[2], 200)
    for _ in range(f):
        art.outline(SWAN[0])
    image = art.image()
    return image.transpose(Image.FLIP_LEFT_RIGHT) if variant else image


# --- Cloud ----------------------------------------------------------------------------------

CLOUD = ramp_of("8fa3c8 a9bbdc b9c8e6 cfdaf0 e0e8f6 eef3fb ffffff")


def puff(variant: int, fine: int = 1) -> Image.Image:
    """A billow of cloud for the underside and the ends of an island, whose tiles end in
    steps: a few round lumps side by side, each lit from over it and in its own shade
    under, soft at its rim. It is stood in the cell of air under a cloud's tile and hangs
    about the line between them, so a row of them is a cloud's own ragged edge. `fine`
    draws it that many times the draft's size: each lump a true round, its shade in
    even bands, its rim thinning in a ring rather than a chequer."""
    r = rng("heaven puff", variant)
    f = fine
    wide = (4, 5, 6, 3, 5)[variant % 5]
    W0, H0 = wide * T, 3 * T
    art = Art(W0 * f, H0 * f)
    lumps = []
    count = wide + 1
    for i in range(count):
        cx = W0 * (i + 0.5) / count + r.uniform(-3, 3)
        rad = r.uniform(9, 15) * (0.75 if i in (0, count - 1) else 1.0)
        lumps.append((cx, H0 * 0.42 + r.uniform(-3, 5), rad))
    for cx, cy, rad in sorted(lumps, key=lambda lump: lump[2]):
        for Y in range(int(cy - rad) * f, (int(cy + rad) + 1) * f):
            for X in range(int(cx - rad) * f, (int(cx + rad) + 1) * f):
                x, y = (X + 0.5) / f - 0.5, (Y + 0.5) / f - 0.5
                d = math.hypot(x + 0.5 - cx, (y + 0.5 - cy) * 1.15) / rad
                if d > 1:
                    continue
                down = (y + 0.5 - cy) / rad  # -1 its top, 1 its underside
                side = (x + 0.5 - cx) / rad
                k = 5 - round(max(down, 0) * 2.6 + max(side, 0) * 0.9 + (0.9 if d > 0.86 and down > -0.2 else 0))
                if f == 1 and d > 0.9 and (x + y) % 2 and down > 0.3:
                    continue  # a rim that thins out
                if f > 1 and d > 0.95 and down > 0.3:
                    continue  # a rim that thins out, in a ring
                if f > 1 and down < -0.55 and side < -0.1 and d < 0.8:
                    k += 1  # the top of a lump, where the light is
                art.put(X, Y, tone(CLOUD, max(k, 2)))
    return art.image()


PUFFS = 5


# --- What there is, and where its floor line lies ------------------------------------------

# Each kind has its own order, so where two overlap which is in front is never left to chance.
ORDER_BACK = -289  # a tower further back: in front of the walls, behind everything else of a castle
ORDER_TREE = -287  # a tree: behind what is built
ORDER_WALL = -284  # the wall at the back of a room: over the walls and the open walls laid there
ORDER_STAND = -283  # what stands before a wall or the sky: columns, gates, bridges, battlements, the host
ORDER_GARDEN = -286  # what a garden is dressed with: before its trees, behind what is built
ORDER_HUNG = -282  # what hangs on a column: a banner
ORDER_LIGHT = -281  # light in the air of a hall
ORDER_OVER = -99  # a great roof: over the map's own roof tiles, behind the ledges and the hero
ORDER_TOWER = -97  # a near tower: before the roofs and the walls it stands at
ORDER_GLINT = -94  # a glint on gold, and a pennant on a rod: before the towers
ORDER_LILIES = 27  # on the water, before the hero as the water is (the Earth's lilies' own order)

BLOSSOMS, GOLDS = 4, 4
# The towers the castles are built of: (tiles across, tiles to the foot of the head, further back, hung on a wall).
# What it has besides is its style (tv2_heaven.tower), so two towers of a size need not be one tower twice.
TOWERS = [
    (8, 24, False, False, 2), (6, 13, False, False, 3), (6, 16, False, False, 1), (8, 19, False, False, 4),
    (4, 15, False, False, 0), (4, 15, False, False, 4), (4, 9, False, True, 0),
    (8, 25, False, False, 1), (6, 11, False, False, 0), (4, 15, False, False, 3), (4, 9, False, False, 0), (6, 14, False, False, 2), (8, 20, False, False, 3),
    (4, 31, True, False, 0), (6, 22, True, False, 0), (4, 27, True, False, 0), (4, 24, True, False, 0),
    (4, 19, True, False, 0), (4, 21, True, False, 0), (4, 15, True, False, 0),
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


def tower_key(wide: int, tall: int, back: bool = False, hang: bool = False, style: int = 0) -> str:
    return f"heaven_tower_{wide}x{tall}" + ("_back" if back else "") + ("_hung" if hang else "") + (f"_s{style}" if style else "")


def tower_rise(wide: int) -> int:
    """How many tiles a tower's head, roof and rod stand over the top of its shaft."""
    return 2 + wide + 1 + 3


FLOWER_BEDS, HEDGES, SWANS = 4, 3, 2
GLOW, FRAME = "_glow", "_f"  # what a picture's companions are named: its windows' light; its further frames


def _plan():
    plan = []

    def add(key, paint, order=ORDER_WALL, sink=0, lit=True):
        plan.append((key, paint, order, lit, sink))

    def moving(key, paint, count, order, sink=0, lit=True):
        """A picture of several frames: its own key is the first, the rest are its key and `_f1` ..."""
        add(key, lambda: paint(0), order, sink, lit)
        for frame in range(1, count):
            add(f"{key}{FRAME}{frame}", lambda frame=frame: paint(frame), order, sink, lit)

    def burning(key, order, sink=0):
        """The light of a picture's windows, to show unlit over it when the day goes."""
        add(key + GLOW, lambda: glow(_DRAWN[key], _LAMPS_OF[key]), order + 1, sink, lit=False)

    for v in range(BLOSSOMS):
        moving(f"heaven_blossom_{v}", lambda sway, v=v: tree(v, BLOSSOM, sway=sway), FRAMES, ORDER_TREE, sink=T)
    for v in range(GOLDS):
        moving(f"heaven_gold_{v}", lambda sway, v=v: tree(v, GOLDLEAF, sway=sway), FRAMES, ORDER_TREE, sink=T)
    seen = set()
    for wide, tall, back, hang, style in TOWERS:
        key = tower_key(wide, tall, back, hang, style)
        if key not in seen:
            seen.add(key)
            order = ORDER_BACK if back else ORDER_TOWER
            add(key, lambda a=(wide, tall, back, hang, style): tower(*a), order)
            burning(key, order)
    for wide in ROOFS:
        # A roof's floor line is its point: it is stood in the cell of air over its point.
        add(f"heaven_roof_{wide}", lambda wide=wide: roof_over(wide), ORDER_OVER, sink=((wide + 1) // 2) * T)
        burning(f"heaven_roof_{wide}", ORDER_OVER, sink=((wide + 1) // 2) * T)
    for rows, wide in PILLARS:
        add(f"heaven_pillar_{rows}x{wide}", lambda a=(rows, wide): pillar(*a), ORDER_STAND)
    add("heaven_hall_west", lambda: hall_side(HALL))
    add("heaven_hall_east", lambda: hall_side(HALL, True))
    add("heaven_rays_west", lambda: rays(HALL), ORDER_LIGHT, lit=False)
    add("heaven_rays_east", lambda: rays(HALL, True), ORDER_LIGHT, lit=False)
    moving("heaven_banner", hung_banner, FRAMES, ORDER_HUNG, sink=T)
    # The wall behind the high seat stands on the hall's floor, three steps of gold under its mark.
    add("heaven_high_seat", lambda: high_seat(SEAT), sink=3 * T)
    burning("heaven_high_seat", ORDER_WALL, sink=3 * T)
    add("heaven_gate", gate, ORDER_STAND)
    add("heaven_rotunda", rotunda, ORDER_STAND)
    burning("heaven_rotunda", ORDER_STAND)
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
    for v in range(PUFFS):
        # A billow hangs about the top of its cell: before the cloud's tiles, behind the ledges and the hero.
        add(f"heaven_puff_{v}", lambda v=v: puff(v), ORDER_OVER, sink=T // 2 + 2)
    for name, cols, rows, windows, wings in CHAMBERS:
        add(f"heaven_room_{name}", lambda a=(name, cols, rows, windows, wings): chamber(*a))
    burning("heaven_room_keep", ORDER_WALL)
    # A garden's own: beds of flowers, clipped green, a fountain that runs, benches, urns, swans.
    for v in range(FLOWER_BEDS):
        add(f"heaven_flowers_{v}", lambda v=v: flowers(v), ORDER_GARDEN, sink=2)
    for v in range(HEDGES):
        add(f"heaven_hedge_{v}", lambda v=v: hedge(v), ORDER_GARDEN, sink=1)
    add("heaven_bench", bench, ORDER_GARDEN)
    add("heaven_urn", urn, ORDER_GARDEN)
    moving("heaven_fountain", fountain, 3, ORDER_GARDEN)
    for v in range(SWANS):
        add(f"heaven_swan_{v}", lambda v=v: swan(v), ORDER_STAND, sink=2)
        # Lilies, the Earth's own drawing of them, on the water and before the hero as the water is.
        add(f"heaven_lilies_{v}", lambda v=v: lilies(v), ORDER_LILIES)
    # What flies and what glints: before the towers.
    moving("heaven_pennant", pennant, WAVES, ORDER_GLINT)
    moving("heaven_glint", glint, 3, ORDER_GLINT + 1, lit=False)
    return plan


PLAN = _plan()
# What is drawn at the size that ships itself (tv2_base.DENSITY), by key; the rest ships as
# its draft through Scale2x. A picture here is the draft's size times DENSITY exactly.
# What is drawn in the second style (tv2_style), by key: given the size it ships at, the
# picture and its normal map. The generator prefers it to FINE.
STYLE = {}
for _kind, _count, _leaves in (("blossom", BLOSSOMS, "dc6c9e"), ("gold", GOLDS, "d8a02a")):
    for _v in range(_count):
        for _frame in range(FRAMES):
            _key = f"heaven_{_kind}_{_v}" + (f"{FRAME}{_frame}" if _frame else "")
            STYLE[_key] = lambda w, h, k=_key, a=(_kind, _v), c=_leaves, s=_frame: tv2_style.tree(w, h, SINK_OF[k] * DENSITY, ("heaven tree",) + a, c, "c4c4d2", frame=s, pale=True, petals=True, crown=1.15, lean=(0.4, -0.3, 0.2, -0.5)[a[1] % 4])

STYLE["heaven_hedge_0"] = lambda w, h: tv2_style.topiary(w, h, SINK_OF["heaven_hedge_0"] * DENSITY, "heaven hedge 0", "43ac5c", "c4c4d2", box=True, flowers="f29cc0")
STYLE["heaven_hedge_1"] = lambda w, h: tv2_style.topiary(w, h, SINK_OF["heaven_hedge_1"] * DENSITY, "heaven hedge 1", "43ac5c", "c4c4d2", balls=1)
STYLE["heaven_hedge_2"] = lambda w, h: tv2_style.topiary(w, h, SINK_OF["heaven_hedge_2"] * DENSITY, "heaven hedge 2", "43ac5c", "c4c4d2", balls=3)

# Raised from their drawings in the first style, so what stands where (a door, a stair) stays.
for _key in ("heaven_tower_8x24_s2", "heaven_tower_6x13_s3", "heaven_tower_6x16_s1", "heaven_tower_8x19_s4", "heaven_tower_4x15", "heaven_tower_4x15_s4", "heaven_tower_4x9_hung", "heaven_tower_8x25_s1", "heaven_tower_6x11", "heaven_tower_4x15_s3", "heaven_tower_4x9", "heaven_tower_6x14_s2", "heaven_tower_8x20_s3", "heaven_tower_4x31_back", "heaven_tower_6x22_back", "heaven_tower_4x27_back", "heaven_tower_4x24_back", "heaven_tower_4x19_back", "heaven_tower_4x21_back", "heaven_tower_4x15_back", "heaven_roof_29", "heaven_roof_21", "heaven_roof_14", "heaven_pillar_18x2", "heaven_pillar_8x2", "heaven_pillar_12x1", "heaven_hall_west", "heaven_hall_east", "heaven_banner", "heaven_banner_f1", "heaven_banner_f2", "heaven_banner_f3", "heaven_high_seat", "heaven_gate", "heaven_rotunda", "heaven_angel_0", "heaven_angel_1", "heaven_bridge_6", "heaven_bridge_7", "heaven_bridge_8", "heaven_bridge_9", "heaven_parapet_27", "heaven_parapet_33", "heaven_portal_9x9", "heaven_portal_4x6", "heaven_span", "heaven_puff_0", "heaven_puff_1", "heaven_puff_2", "heaven_puff_3", "heaven_puff_4", "heaven_room_lower", "heaven_room_upper", "heaven_room_keep", "heaven_flowers_0", "heaven_flowers_1", "heaven_flowers_2", "heaven_flowers_3", "heaven_bench", "heaven_urn", "heaven_fountain", "heaven_fountain_f1", "heaven_fountain_f2", "heaven_swan_0", "heaven_lilies_0", "heaven_swan_1", "heaven_lilies_1", "heaven_pennant", "heaven_pennant_f1", "heaven_pennant_f2", "heaven_pennant_f3", "heaven_pennant_f4", "heaven_pennant_f5"):
    STYLE[_key] = lambda w, h, k=_key: tv2_style.relief(FINE[k]())

# The buildings drawn again over their own plans (tv2_style.repaint): each region read from the
# first-style drawing as the material it is and painted anew in it, every opening kept. The
# towers far behind keep the relief: their haze is the distance.
for _key in ("heaven_tower_8x24_s2", "heaven_tower_6x13_s3", "heaven_tower_6x16_s1", "heaven_tower_8x19_s4", "heaven_tower_4x15", "heaven_tower_4x15_s4", "heaven_tower_4x9_hung", "heaven_tower_8x25_s1", "heaven_tower_6x11", "heaven_tower_4x15_s3", "heaven_tower_4x9", "heaven_tower_6x14_s2", "heaven_tower_8x20_s3", "heaven_hall_west", "heaven_hall_east", "heaven_high_seat", "heaven_gate", "heaven_rotunda", "heaven_bridge_6", "heaven_bridge_7", "heaven_bridge_8", "heaven_bridge_9", "heaven_parapet_27", "heaven_parapet_33", "heaven_span", "heaven_room_lower", "heaven_room_upper", "heaven_room_keep"):
    STYLE[_key] = lambda w, h, k=_key: tv2_style.repainted(FINE[k](), k)

# The statues and columns drawn again: the angels carved anew over their own plans
# (tv2_style.angel_statue), the pillars turned round over their outlines (tv2_style.turned).
for _key in ("heaven_angel_0", "heaven_angel_1"):
    STYLE[_key] = lambda w, h, k=_key: tv2_style.angel_statue(FINE[k](), k, int(k[-1]))
for _key in ("heaven_pillar_18x2", "heaven_pillar_8x2", "heaven_pillar_12x1"):
    STYLE[_key] = lambda w, h, k=_key: tv2_style.turned(FINE[k](), k)

FINE = {}
for _v in range(BLOSSOMS):
    for _frame in range(FRAMES):
        FINE[f"heaven_blossom_{_v}" + (f"{FRAME}{_frame}" if _frame else "")] = lambda v=_v, s=_frame: tree(v, BLOSSOM, sway=s, fine=DENSITY)
for _v in range(GOLDS):
    for _frame in range(FRAMES):
        FINE[f"heaven_gold_{_v}" + (f"{FRAME}{_frame}" if _frame else "")] = lambda v=_v, s=_frame: tree(v, GOLDLEAF, sway=s, fine=DENSITY)
# The billows under a cloud, and what a garden is dressed with.
for _v in range(PUFFS):
    FINE[f"heaven_puff_{_v}"] = lambda v=_v: puff(v, DENSITY)
for _v in range(FLOWER_BEDS):
    FINE[f"heaven_flowers_{_v}"] = lambda v=_v: flowers(v, DENSITY)
for _v in range(HEDGES):
    FINE[f"heaven_hedge_{_v}"] = lambda v=_v: hedge(v, DENSITY)
FINE["heaven_bench"] = lambda: bench(DENSITY)
FINE["heaven_urn"] = lambda: urn(DENSITY)
for _rows, _wide in PILLARS:
    FINE[f"heaven_pillar_{_rows}x{_wide}"] = lambda a=(_rows, _wide): pillar(*a, fine=DENSITY)
# The castles' towers.
for _a in TOWERS:
    FINE[tower_key(*_a)] = lambda a=_a: tower(*a, fine=DENSITY)
# The great roofs, the hall's banners and its light, the gates, the bridges, the battlements.
for _wide in ROOFS:
    FINE[f"heaven_roof_{_wide}"] = lambda w=_wide: roof_over(w, DENSITY)
for _frame in range(FRAMES):
    FINE["heaven_banner" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: hung_banner(s, DENSITY)
for _frame in range(WAVES):
    FINE["heaven_pennant" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: pennant(s, DENSITY)
for _frame in range(3):
    FINE["heaven_glint" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: glint(s, DENSITY)
FINE["heaven_rays_west"] = lambda: rays(HALL, fine=DENSITY)
FINE["heaven_rays_east"] = lambda: rays(HALL, True, DENSITY)
for _wide in BRIDGES:
    FINE[f"heaven_bridge_{_wide}"] = lambda w=_wide: bridge(w, DENSITY)
for _cols in PARAPETS:
    FINE[f"heaven_parapet_{_cols}"] = lambda c=_cols: parapet(c, DENSITY)
for _apart, _rows, _wide in PORTALS:
    FINE[f"heaven_portal_{_apart}x{_rows}"] = lambda a=(_apart, _rows, _wide): portal(*a, fine=DENSITY)
# The gatehouse's gate, the Orchard's temple, the bridge from the Garden, the hall, its high
# seat and the rooms of the House; the swans.
FINE["heaven_gate"] = lambda: gate(DENSITY)
FINE["heaven_rotunda"] = lambda: rotunda(DENSITY)
FINE["heaven_span"] = lambda: span(DENSITY)
FINE["heaven_hall_west"] = lambda: hall_side(HALL, fine=DENSITY)
FINE["heaven_hall_east"] = lambda: hall_side(HALL, True, DENSITY)
FINE["heaven_high_seat"] = lambda: high_seat(SEAT, DENSITY)
for _a in CHAMBERS:
    FINE[f"heaven_room_{_a[0]}"] = lambda a=_a: chamber(*a, fine=DENSITY)
for _v in range(SWANS):
    FINE[f"heaven_swan_{_v}"] = lambda v=_v: swan(v, DENSITY)
# The host in marble, and the fountain that runs.
for _v in range(2):
    FINE[f"heaven_angel_{_v}"] = lambda v=_v: angel(v, DENSITY)
for _frame in range(3):
    FINE["heaven_fountain" + (f"{FRAME}{_frame}" if _frame else "")] = lambda s=_frame: fountain(s, DENSITY)
# The light of every picture's windows, over it as it is drawn at the size that ships; the lilies.
for _key in [k for k in FINE]:
    if _key + GLOW in [key for key, *_ in PLAN]:
        FINE[_key + GLOW] = lambda k=_key: (pictures(), glow(FINE[k](), _LAMPS_OF[k], DENSITY))[1]
for _v in range(SWANS):
    FINE[f"heaven_lilies_{_v}"] = lambda v=_v: lilies(v, DENSITY)
PICTURE_KEYS = [key for key, *_ in PLAN]
ENTRIES = [
    {"key": key, "kind": "picture", "scroll": 1.0, "scrollUp": 1.0, "rise": 0, "drift": 0.0, "order": order, "lit": lit, "tinted": False}
    for key, _, order, lit, _ in PLAN
]
SINK_OF = {key: sink for key, *_, sink in PLAN}
_DRAWN: dict[str, Image.Image] = {}
_LAMPS_OF: dict[str, list] = {}


def base_of(key: str) -> str:
    """The picture a companion goes with (its windows' light, a further frame); a picture itself is its own."""
    if key.endswith(GLOW):
        return key[: -len(GLOW)]
    head, _, tail = key.rpartition(FRAME)
    return head if head and tail.isdigit() else key


STOOD_KEYS = [key for key in PICTURE_KEYS if base_of(key) == key]  # what a map stands; companions come with them


def pictures() -> dict[str, Image.Image]:
    """Every picture of Paradise, drawn (once), by key."""
    if not _DRAWN:
        for key, paint, *_ in PLAN:
            _LAMPS.clear()
            _DRAWN[key] = paint()
            _LAMPS_OF[key] = list(_LAMPS)
    return dict(_DRAWN)


def pivot_of(key: str) -> tuple[float, float]:
    """A picture's pivot (x, y from the bottom, 0 .. 1): the middle of its floor line."""
    return (0.5, round(SINK_OF[key] / pictures()[key].height, 6))


def size_of(key: str) -> tuple[int, int]:
    return pictures()[key].size
