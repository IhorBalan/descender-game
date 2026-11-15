#!/usr/bin/env python3
"""Draws the Tiles V2 backdrops: the sky's pieces (clouds, stars, sun, moon), the
parallax ridges behind the map and the rock behind the caves. Not a script.

The sky itself is not a picture: it is two colours that change with the hour
(tv2_base.DAY_KEYS), drawn in seven dithered bands by `sky_image` here and by
SkyPainter.cs in Unity, the same sums.
"""
from __future__ import annotations

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

BACKDROP_W, BACKDROP_H = VIEW  # a backdrop picture is the size of the view and repeats sideways
SKY_BANDS = 7
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]

# Back to front. `kind` says how Unity places a layer: sky pieces are pinned to
# the view, a ridge stands on the surface line and scrolls sideways, the cave
# rock fills the ground under the surface line and scrolls both ways.
BACKDROPS = [
    {"key": "backdrop_stars", "kind": "stars", "scroll": 0.0, "order": -1008, "lit": False},
    {"key": "backdrop_sun", "kind": "sun", "scroll": 0.0, "order": -1006, "lit": False},
    {"key": "backdrop_moon", "kind": "moon", "scroll": 0.0, "order": -1006, "lit": False},
    {"key": "backdrop_clouds", "kind": "clouds", "scroll": 0.04, "order": -1004, "lit": False},
    {"key": "backdrop_far", "kind": "ridge", "scroll": 0.1, "order": -990, "lit": False},
    {"key": "backdrop_mid", "kind": "ridge", "scroll": 0.25, "order": -980, "lit": False},
    {"key": "backdrop_near", "kind": "ridge", "scroll": 0.45, "order": -970, "lit": False},
]
# The rock behind the caves is a region's own (REGIONS in tv2_base.py): each region that
# has one gets `backdrop_cave_<region>` and its ragged top, `backdrop_cave_top_<region>`.
CAVE_SCROLL = 0.55
CAVE_ORDER = -960
SKY_ORDER = -1010
CLOUD_DRIFT = 0.25  # units a second the clouds move to the right


def periodic(x, waves, seed, width=BACKDROP_W):
    """A sum of sines that repeats every `width` pixels."""
    r = rng("ridge", seed)
    total = 0.0
    for k, amp in waves:
        phase = r.uniform(0, 6.283)
        total += amp * np.sin(2 * np.pi * k * x / width + phase)
    return total


def dither_mix(a, b, t, x, y):
    """a or b at pixel (x, y) by an ordered 4 x 4 dither of the share t of b."""
    return b if t * 16 > BAYER[y % 4][x % 4] + 0.5 else a


def sky_band(x: int, y: int, height: int) -> int:
    """Which of the SKY_BANDS colours pixel (x, y) of a sky `height` px tall shows
    (0 at the top): the bands meet in an ordered dither."""
    t = y / (height - 1) * (SKY_BANDS - 1)
    i = int(t)
    return min(i + (1 if (t - i) * 16 > BAYER[y % 4][x % 4] + 0.5 else 0), SKY_BANDS - 1)


def sky_image(width: int, height: int, top, low) -> Image.Image:
    """The sky between two colours (0 .. 1 floats), top to bottom."""
    im = Image.new("RGBA", (width, height))
    px = im.load()
    colours = [
        tuple(int(round(255 * (top[j] + (low[j] - top[j]) * k / (SKY_BANDS - 1)))) for j in range(3)) + (255,)
        for k in range(SKY_BANDS)
    ]
    for y in range(height):
        for x in range(width):
            px[x, y] = colours[sky_band(x, y, height)]
    return im


def backdrop_clouds() -> Image.Image:
    """Flat puffs, lit on top, a shade on the underside; the row repeats sideways."""
    im = Image.new("RGBA", (BACKDROP_W, 160), (0, 0, 0, 0))
    px = im.load()
    r = rng("clouds")
    white, under = hexes("f2f7fb")[0], hexes("c9dcea")[0]
    # A few big banks and some small puffs between them.
    for big in (True, True, True, False, False, False, False, True, False):
        cx, cy = r.randrange(BACKDROP_W), r.randint(34, 124)
        w = r.randint(64, 110) if big else r.randint(26, 48)
        size = (9, 17) if big else (5, 9)
        puffs = [(cx + r.randint(-w // 2, w // 2), cy + r.randint(-4, 4), r.randint(*size)) for _ in range(7 if big else 4)]
        for y in range(cy - 26, cy + 18):
            for x in range(cx - w, cx + w):
                inside = any((x - px0) ** 2 + ((y - py0) * 1.6) ** 2 <= rr * rr for px0, py0, rr in puffs)
                if inside and 0 <= y < im.height:
                    px[x % BACKDROP_W, y] = (under if y > cy + (4 if big else 2) else white) + (255,)
    return im


def backdrop_stars() -> Image.Image:
    """A night sky's stars on nothing: single pixels of three brightnesses and a few crosses."""
    im = Image.new("RGBA", (BACKDROP_W, BACKDROP_H), (0, 0, 0, 0))
    px = im.load()
    r = rng("stars")
    tints = hexes("ffffff", "cfe0ff", "fff0c8")
    for _ in range(520):
        x, y = r.randrange(BACKDROP_W), r.randrange(BACKDROP_H - 60)
        px[x, y] = r.choice(tints) + (r.choice([110, 160, 220]),)
    for _ in range(30):
        x, y = r.randrange(2, BACKDROP_W - 2), r.randrange(2, BACKDROP_H - 90)
        px[x, y] = tints[0] + (255,)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            px[x + dx, y + dy] = tints[1] + (130,)
    return im


def disc(size: int, radius: float, colours, shadow=None) -> Image.Image:
    """A round body: rings of colour from the rim in, and for the moon a darker side."""
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = im.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if d > radius:
                continue
            ring = min(int((radius - d) / radius * len(colours) * 1.4), len(colours) - 1)
            colour = colours[ring]
            if shadow and ((x - c - radius * 0.55) ** 2 + (y - c + radius * 0.25) ** 2) ** 0.5 < radius * 0.95:
                colour = shadow
            px[x, y] = colour + (255,)
    return im


def backdrop_sun() -> Image.Image:
    return disc(32, 13.2, hexes("f6c453", "ffd76e", "ffe9a0", "fff7d6"))


def backdrop_moon() -> Image.Image:
    im = disc(32, 11.2, hexes("c9d2e4", "e9edf5", "f6f8fc"), shadow=hexes("9aa6c2")[0])
    px = im.load()
    crater = hexes("b4bfd6")[0] + (255,)
    for x, y in ((10, 12), (12, 20), (8, 16)):
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            px[x + dx, y + dy] = crater
    return im


def backdrop_ridge(name: str, colour, light, base: int, waves, seed, trees=None) -> Image.Image:
    """A silhouette band that repeats sideways: its left-facing slopes lit (the
    light comes from the top left), optional round-crowned trees on top."""
    im = Image.new("RGBA", (BACKDROP_W, BACKDROP_H), (0, 0, 0, 0))
    px = im.load()
    xs = np.arange(BACKDROP_W)
    height = base + periodic(xs, waves, seed)
    if trees:
        r = rng(name, "trees")
        bumps = np.zeros(BACKDROP_W)
        x = 0
        while x < BACKDROP_W:
            rad = r.randint(*trees)
            for dx in range(-rad, rad + 1):
                xx = (x + dx) % BACKDROP_W
                bumps[xx] = max(bumps[xx], (rad * rad - dx * dx) ** 0.5 + rad * 0.6)
            x += r.randint(rad, rad * 2)
        height = height + bumps
    top = (BACKDROP_H - np.round(height)).astype(int)
    for x in range(BACKDROP_W):
        slope = top[x] - top[(x - 1) % BACKDROP_W]
        for y in range(max(top[x], 0), BACKDROP_H):
            lit = slope < 0 and y - top[x] < 3 or (y == top[x])
            px[x, y] = (light if lit else colour) + (255,)
    return im


CAVE_SIZE = 128
CAVE_TOP_H = 32


def cave_rock(width: int, height: int, seed: str, cells: int) -> np.ndarray:
    """Big rough boulders with dark cracks between them, as ramp indices; wraps
    both ways so the picture tiles."""
    r = rng("cave", seed)
    points = [(r.uniform(0, width), r.uniform(0, height), r.choice((1, 2, 2, 3))) for _ in range(cells)]

    def wrapped(ax, ay, bx, by):
        dx, dy = abs(ax - bx), abs(ay - by)
        return (min(dx, width - dx) ** 2 + min(dy, height - dy) ** 2) ** 0.5

    owner = np.zeros((height, width), dtype=np.int16)
    crack = np.zeros((height, width), dtype=bool)
    for y in range(height):
        for x in range(width):
            d = sorted((wrapped(x + 0.5, y + 0.5, px0, py0), i) for i, (px0, py0, _) in enumerate(points))
            owner[y, x] = d[0][1]
            crack[y, x] = d[1][0] - d[0][0] < 1.5
    t = np.zeros((height, width), dtype=np.int16)
    for y in range(height):
        for x in range(width):
            if crack[y, x]:
                continue
            v = points[owner[y, x]][2]
            if crack[(y - 2) % height, x] or crack[y, (x - 2) % width]:
                v += 1
            elif crack[(y + 2) % height, x] or crack[y, (x + 2) % width]:
                v -= 1
            t[y, x] = max(1, min(4, v))
    for _ in range(width * height // 90):
        x, y = r.randrange(width), r.randrange(height)
        if t[y, x] > 1:
            t[y, x] -= 1
    return t


def backdrop_cave(region: dict) -> Image.Image:
    """The rock behind a region's caves: dark, so a wall in front of it still reads; tiles both ways."""
    CAVE_RAMP = hexes(*region["cave"].split())
    t = cave_rock(CAVE_SIZE, CAVE_SIZE, "body " + region["key"], 26)
    rgba = np.zeros((CAVE_SIZE, CAVE_SIZE, 4), dtype=np.uint8)
    rgba[..., :3] = np.array(CAVE_RAMP, dtype=np.uint8)[t]
    rgba[..., 3] = 255
    return Image.fromarray(rgba, "RGBA")


def backdrop_cave_top(region: dict) -> Image.Image:
    """The top of the cave rock, where it meets the sky backdrop in an opening: a
    ragged edge; repeats sideways."""
    CAVE_RAMP = hexes(*region["cave"].split())
    t = cave_rock(CAVE_SIZE, CAVE_TOP_H, "top " + region["key"], 9)
    xs = np.arange(CAVE_SIZE)
    edge = 9 + periodic(xs, [(2, 3.5), (5, 2.0), (11, 1.2)], "cave top", CAVE_SIZE)
    rgba = np.zeros((CAVE_TOP_H, CAVE_SIZE, 4), dtype=np.uint8)
    rgba[..., :3] = np.array(CAVE_RAMP, dtype=np.uint8)[t]
    for x in range(CAVE_SIZE):
        top = int(round(CAVE_TOP_H - 14 - edge[x] * 0.9))
        rgba[max(top, 0) :, x, 3] = 255
        if 0 <= top < CAVE_TOP_H:
            rgba[top, x, :3] = CAVE_RAMP[4]
    rgba[CAVE_TOP_H - 1, :, :3] = CAVE_RAMP[0]
    return Image.fromarray(rgba, "RGBA")


FAR_W, FAR_H = 640, 360


def backdrop_far(region: dict) -> Image.Image:
    """What stands far off in a region's caves: shapes a little lighter than the rock
    behind them, standing from the foot of the picture and hanging from its head; it
    repeats both ways. Columns, spires, fangs, heaps, bare trees, towers with lit
    windows, a viaduct, shards: FAR in tv2_base.py says which."""
    kind = FAR[region["key"]]
    ramp = hexes(*region["cave"].split())
    lit = tuple(min(255, int(c * 1.55) + 10) for c in ramp[4])
    mid = tuple(min(255, int(c * 1.3) + 6) for c in ramp[4])
    dark = tuple(min(255, int(c * 1.08) + 3) for c in ramp[4])
    a = np.zeros((FAR_H, FAR_W, 4), dtype=np.uint8)
    r = rng("far", region["key"])

    def put(x, y, colour):
        if 0 <= y < FAR_H:
            a[y, x % FAR_W] = (*colour, 255)

    def block(x0, x1, y0, y1):
        """A shape's body: lit on its left edge, dark on its right."""
        for x in range(x0, x1):
            colour = lit if x - x0 < max(1, (x1 - x0) // 5) else dark if x1 - x <= max(1, (x1 - x0) // 4) else mid
            for y in range(y0, y1):
                put(x, y, colour)

    def needle(cx, base, tall, half, up=True):
        """A spike from a base row: standing (up) or hanging."""
        for i in range(tall):
            wide = max(0, int(round(half * (1 - i / tall) ** 0.8)))
            y = base - i if up else base + i
            for x in range(cx - wide, cx + wide + 1):
                put(x, y, lit if x < cx - wide * 0.3 else dark if x > cx + wide * 0.4 else mid)

    x = r.randint(0, 30)
    while x < FAR_W - 20:
        if kind == "columns":
            wide, tall = r.randint(12, 20), r.randint(90, 210)
            block(x, x + wide, FAR_H - tall, FAR_H)
            if r.random() < 0.6:
                block(x - 3, x + wide + 3, FAR_H - tall - 6, FAR_H - tall)
            if r.random() < 0.5:
                needle(x + wide // 2 + r.randint(20, 40), 0, r.randint(20, 70), r.randint(3, 6), up=False)
            x += wide + r.randint(34, 80)
        elif kind in ("spires", "shards"):
            tall = r.randint(70, 230)
            needle(x, FAR_H - 1, tall, r.randint(5, 13) if kind == "spires" else r.randint(3, 8))
            if kind == "shards" and r.random() < 0.6:
                needle(x + r.randint(8, 16), FAR_H - 1, tall // 2, r.randint(3, 6))
            needle(x + r.randint(18, 40), 0, r.randint(30, 120), r.randint(4, 9), up=False)
            x += r.randint(40, 86)
        elif kind == "teeth":
            needle(x, 0, r.randint(60, 190), r.randint(7, 15), up=False)
            needle(x + r.randint(14, 30), FAR_H - 1, r.randint(30, 110), r.randint(8, 16))
            x += r.randint(36, 70)
        elif kind == "heaps":
            wide, tall = r.randint(50, 110), r.randint(30, 80)
            for dx in range(wide):
                top = int(tall * np.sin(np.pi * (dx + 0.5) / wide) ** 0.7)
                for y in range(FAR_H - top, FAR_H):
                    put(x + dx, y, lit if y - (FAR_H - top) < 2 else mid if dx < wide * 0.6 else dark)
            if r.random() < 0.7:
                pillar = x + wide + r.randint(6, 20)
                block(pillar, pillar + r.randint(8, 14), 0, FAR_H)
            x += wide + r.randint(40, 80)
        elif kind == "trees":
            tall, wide = r.randint(120, 240), r.randint(5, 9)
            block(x, x + wide, FAR_H - tall, FAR_H)
            for _ in range(r.randint(3, 6)):
                at, side, length, slope = r.randint(tall // 3, tall - 8), r.choice([-1, 1]), r.randint(14, 44), r.uniform(0.5, 0.9)
                for i in range(length):
                    bx = x + (wide if side > 0 else 0) + side * i
                    by = FAR_H - at - int(i * slope)
                    put(bx, by, mid)
                    put(bx, by + 1, dark)
            x += r.randint(46, 100)
        elif kind == "towers":
            wide, tall = r.randint(28, 48), r.randint(120, 270)
            block(x, x + wide, FAR_H - tall, FAR_H)
            for cx in range(x, x + wide, 8):
                block(cx, cx + 4, FAR_H - tall - 5, FAR_H - tall)
            for wy in range(FAR_H - tall + 14, FAR_H - 8, 22):
                for wx in range(x + 6, x + wide - 6, 11):
                    if r.random() < 0.6:
                        for dy in range(5):
                            put(wx, wy + dy, (255, 150, 60) if r.random() < 0.5 else ramp[0])
                            put(wx + 1, wy + dy, (230, 110, 40) if r.random() < 0.5 else ramp[0])
            x += wide + r.randint(36, 90)
        else:  # arches: a viaduct right across
            break
    if kind == "arches":
        deck, span = FAR_H - 150, 80
        for x in range(FAR_W):
            along = x % span
            rise = int(52 * np.sin(np.pi * along / span) ** 0.6) if 8 <= along < span - 8 else 0
            for y in range(deck, FAR_H):
                if y < deck + 14 or y - deck - 14 >= rise or along < 8 or along >= span - 8:
                    put(x, y, lit if y < deck + 2 else dark if along >= span - 11 else mid)
        for x in range(30, FAR_W, 110):
            needle(x, 0, r.randint(40, 100), r.randint(4, 8), up=False)
    return Image.fromarray(a, "RGBA")


def backdrops() -> dict[str, Image.Image]:
    caves = {}
    for region in REGIONS:
        if region["cave"]:
            caves["backdrop_cave_" + region["key"]] = backdrop_cave(region)
            caves["backdrop_cave_top_" + region["key"]] = backdrop_cave_top(region)
            caves["backdrop_far_" + region["key"]] = backdrop_far(region)
    return {
        **caves,
        "backdrop_stars": backdrop_stars(),
        "backdrop_sun": backdrop_sun(),
        "backdrop_moon": backdrop_moon(),
        "backdrop_clouds": backdrop_clouds(),
        "backdrop_far": backdrop_ridge(
            "far", hexes("7fa3c4")[0], hexes("a3c2dc")[0], 118, [(1, 30), (2, 20), (3, 12), (7, 6), (13, 3), (23, 2)], 1
        ),
        "backdrop_mid": backdrop_ridge(
            "mid", hexes("4f8a7c")[0], hexes("69a48f")[0], 72, [(1, 12), (3, 10), (5, 6), (11, 3)], 2, trees=(4, 8)
        ),
        "backdrop_near": backdrop_ridge(
            "near", hexes("2f5e4c")[0], hexes("427a60")[0], 40, [(2, 8), (5, 5), (9, 3)], 3, trees=(6, 12)
        ),
    }
