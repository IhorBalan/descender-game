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


def backdrops() -> dict[str, Image.Image]:
    caves = {}
    for region in REGIONS:
        if region["cave"]:
            caves["backdrop_cave_" + region["key"]] = backdrop_cave(region)
            caves["backdrop_cave_top_" + region["key"]] = backdrop_cave_top(region)
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
