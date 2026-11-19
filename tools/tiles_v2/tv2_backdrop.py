#!/usr/bin/env python3
"""Draws the Tiles V2 backdrops: the sky's pieces (clouds, stars, sun, moon), the
parallax ridges behind the map and the rock behind the caves. Not a script.

The sky itself is not a picture: it is two colours that change with the hour
(tv2_base.DAY_KEYS), drawn in clean bands by `sky_image` here and by Sky.cs in
Unity, the same sums.

Nothing here is dithered: a chequer of two colours is a screen door at the size
the game is seen at. Bands meet in a line, and what thins out (a cloud's foot,
a shaft of light) does it in a few steps of see-through.
"""
from __future__ import annotations

import math

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_earth
import tv2_eden
import tv2_heaven

BACKDROP_W, BACKDROP_H = VIEW  # a backdrop picture is the size of the view and repeats sideways
SKY_BANDS = 24
FADE_STEPS = 8  # how many steps of see-through a thinning edge goes down in
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
    # Heaven, over a world that has a Paradise: `rise` is how many tiles over the surface line a
    # layer's foot stands for a camera on that line, `scrollUp` the share of the camera's way up
    # it moves with the world (under 1 it keeps the camera company, as what is far does),
    # `drift` the units a second it moves by itself. Islands far off, shafts of light, a sea
    # of cloud the islands stand in, and wisps of it that pass in front of the hero.
    # Furthest: a country of cloud, towers of it one behind another, that fills most of the
    # sky of Paradise; then castles on banks of cloud, far off and pale. Both are tall
    # pictures that follow the camera only a little, set so that a view on the Earth's
    # surface is under their feet (their thinned-out foot is a grey veil against a clear
    # sky) and a view anywhere in Paradise, the Garden to the top of the citadel, is in them.
    {"key": "backdrop_heaven_clouds", "kind": "heaven", "scroll": 0.05, "scrollUp": 0.66, "rise": 31, "drift": 0.0, "order": -1003, "lit": False, "tinted": True},
    {"key": "backdrop_heaven_city", "kind": "heaven", "scroll": 0.1, "scrollUp": 0.7, "rise": 32, "drift": 0.0, "order": -1001, "lit": False, "tinted": True},
    {"key": "backdrop_heaven_far", "kind": "heaven", "scroll": 0.06, "scrollUp": 0.25, "rise": 6, "drift": 0.0, "order": -1000, "lit": False, "tinted": True},
    {"key": "backdrop_heaven_near", "kind": "heaven", "scroll": 0.16, "scrollUp": 0.4, "rise": 10, "drift": 0.0, "order": -996, "lit": False, "tinted": True},
    # A second sea of cloud, the one the Temple's island stands in, and thin cloud high over all.
    {"key": "backdrop_cirrus", "kind": "heaven", "scroll": 0.35, "scrollUp": 1.0, "rise": 138, "drift": 0.2, "order": -968, "lit": False, "tinted": True},
    {"key": "backdrop_cloudmid", "kind": "heaven", "scroll": 0.6, "scrollUp": 1.0, "rise": 77, "drift": 0.08, "order": -967, "lit": False, "tinted": True},
    {"key": "backdrop_cloudsea", "kind": "heaven", "scroll": 0.5, "scrollUp": 1.0, "rise": 26, "drift": 0.12, "order": -966, "lit": False, "tinted": True},
    {"key": "backdrop_rays", "kind": "heaven", "scroll": 0.3, "scrollUp": 0.6, "rise": 8, "drift": 0.0, "order": -964, "lit": False, "tinted": False},
    {"key": "backdrop_cloudfront", "kind": "heaven", "scroll": 1.0, "scrollUp": 1.0, "rise": 24, "drift": 0.4, "order": 30, "lit": False, "tinted": True},
    # A picture a map stands somewhere itself (`@mark picture column row key`): not laid by kind.
    {"key": "backdrop_rainbow", "kind": "picture", "scroll": 1.0, "scrollUp": 1.0, "rise": 0, "drift": 0.0, "order": -962, "lit": False, "tinted": False},
    {"key": "backdrop_glory", "kind": "picture", "scroll": 1.0, "scrollUp": 1.0, "rise": 0, "drift": 0.0, "order": -963, "lit": False, "tinted": False},
]
# The Earth's (tv2_earth.py): a ridge of hills between the mountains and the woods, and the
# pictures its surface and its mine are dressed with.
BACKDROPS += tv2_earth.NEW_RIDGES + tv2_earth.ENTRIES
# Paradise's (tv2_heaven.py): its trees, the towers and roofs of its castles, its halls.
BACKDROPS += tv2_heaven.ENTRIES
# The Paradise level's (tv2_eden.py): what its seven zones are dressed with.
BACKDROPS += tv2_eden.ENTRIES
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


def stepped(share, steps: int = FADE_STEPS):
    """A share 0 .. 1 (a number or an array) in `steps` even steps: how a thinning edge fades."""
    return np.clip(np.ceil(np.clip(share, 0, 1) * steps) / steps, 0, 1)


def sky_band(x: int, y: int, height: int) -> int:
    """Which of the SKY_BANDS colours pixel (x, y) of a sky `height` px tall shows
    (0 at the top): even bands that meet in a line."""
    return min(y * SKY_BANDS // height, SKY_BANDS - 1)


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


def backdrop_clouds(fine: int = 1) -> Image.Image:
    """Flat puffs, lit on top, a shade on the underside; the row repeats sideways. `fine`
    draws them that many times the draft's size, each puff a true round."""
    f = fine
    W0 = BACKDROP_W
    im = Image.new("RGBA", (W0 * f, 160 * f), (0, 0, 0, 0))
    px = im.load()
    r = rng("clouds")
    white, under = hexes("f2f7fb")[0], hexes("c9dcea")[0]
    # A few big banks and some small puffs between them.
    for big in (True, True, True, False, False, False, False, True, False):
        cx, cy = r.randrange(W0), r.randint(34, 124)
        w = r.randint(64, 110) if big else r.randint(26, 48)
        size = (9, 17) if big else (5, 9)
        puffs = [(cx + r.randint(-w // 2, w // 2), cy + r.randint(-4, 4), r.randint(*size)) for _ in range(7 if big else 4)]
        for Y in range((cy - 26) * f, (cy + 18) * f):
            y = Y / f
            for X in range((cx - w) * f, (cx + w) * f):
                x = X / f
                inside = any((x - px0) ** 2 + ((y - py0) * 1.6) ** 2 <= rr * rr for px0, py0, rr in puffs)
                if inside and 0 <= Y < im.height:
                    px[X % (W0 * f), Y] = (under if y > cy + (4 if big else 2) else white) + (255,)
    return im


def backdrop_stars(fine: int = 1) -> Image.Image:
    """A night sky's stars on nothing: single pixels of three brightnesses and a few
    crosses. `fine` puts them in the same places at that many times the draft's size: a
    star is a pixel still, so they are finer; a cross has a bright heart."""
    f = fine
    im = Image.new("RGBA", (BACKDROP_W * f, BACKDROP_H * f), (0, 0, 0, 0))
    px = im.load()
    r = rng("stars")
    tints = hexes("ffffff", "cfe0ff", "fff0c8")
    for _ in range(520):
        x, y = r.randrange(BACKDROP_W), r.randrange(BACKDROP_H - 60)
        px[x * f, y * f] = r.choice(tints) + (r.choice([110, 160, 220]),)
    for _ in range(30):
        x, y = r.randrange(2, BACKDROP_W - 2), r.randrange(2, BACKDROP_H - 90)
        X, Y = x * f, y * f
        px[X, Y] = tints[0] + (255,)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            px[X + dx, Y + dy] = tints[1] + (130 if f == 1 else 170,)
            if f > 1:
                px[X + dx * 2, Y + dy * 2] = tints[1] + (90,)
    return im


def disc(size: int, radius: float, colours, shadow=None, fine: int = 1) -> Image.Image:
    """A round body: rings of colour from the rim in, and for the moon a darker side.
    `fine` draws it that many times the draft's size, its rings true rounds."""
    f = fine
    im = Image.new("RGBA", (size * f, size * f), (0, 0, 0, 0))
    px = im.load()
    c = (size - 1) / 2
    for Y in range(size * f):
        for X in range(size * f):
            x, y = (X + 0.5) / f - 0.5, (Y + 0.5) / f - 0.5
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if d > radius:
                continue
            ring = min(int((radius - d) / radius * len(colours) * 1.4), len(colours) - 1)
            colour = colours[ring]
            if shadow and ((x - c - radius * 0.55) ** 2 + (y - c + radius * 0.25) ** 2) ** 0.5 < radius * 0.95:
                colour = shadow
            px[X, Y] = colour + (255,)
    return im


def backdrop_sun(fine: int = 1) -> Image.Image:
    return disc(32, 13.2, hexes("f6c453", "ffd76e", "ffe9a0", "fff7d6"), fine=fine)


def backdrop_moon(fine: int = 1) -> Image.Image:
    f = fine
    im = disc(32, 11.2, hexes("c9d2e4", "e9edf5", "f6f8fc"), shadow=hexes("9aa6c2")[0], fine=f)
    px = im.load()
    crater, rim = hexes("b4bfd6", "dde3ef")
    for x, y in ((10, 12), (12, 20), (8, 16)):
        for dx in range(2 * f):
            for dy in range(2 * f):
                if f > 1 and (dx, dy) in ((0, 0), (2 * f - 1, 0), (0, 2 * f - 1), (2 * f - 1, 2 * f - 1)):
                    continue  # a round crater
                px[x * f + dx, y * f + dy] = (rim if f > 1 and dy == 2 * f - 1 else crater) + (255,)
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


def cave_rock(width: int, height: int, seed: str, cells: int, fine: int = 1) -> np.ndarray:
    """Big rough boulders with dark cracks between them, as ramp indices; wraps
    both ways so the picture tiles. `fine` makes it that many times the draft's size:
    the same boulders, the cracks between them narrower, the grit on them finer."""
    f = fine
    r = rng("cave", seed)
    points = [(r.uniform(0, width), r.uniform(0, height), r.choice((1, 2, 2, 3))) for _ in range(cells)]
    W, H = width * f, height * f
    ys, xs = np.mgrid[0:H, 0:W]
    xs, ys = (xs + 0.5) / f, (ys + 0.5) / f
    best = np.full((H, W), 1e9)
    second = np.full((H, W), 1e9)
    owner = np.zeros((H, W), dtype=np.int16)
    for i, (px0, py0, _) in enumerate(points):
        dx, dy = np.abs(xs - px0), np.abs(ys - py0)
        d = np.sqrt(np.minimum(dx, width - dx) ** 2 + np.minimum(dy, height - dy) ** 2)
        nearer = d < best
        second = np.where(nearer, best, np.minimum(second, d))
        owner = np.where(nearer, i, owner)
        best = np.minimum(best, d)
    crack = second - best < (1.5 if f == 1 else 1.0)
    shade = np.array([p[2] for p in points], dtype=np.int16)[owner]
    reach = 2 * f
    above = np.roll(crack, reach, axis=0) | np.roll(crack, reach, axis=1)  # a crack two draft pixels up or to the left
    below = np.roll(crack, -reach, axis=0) | np.roll(crack, -reach, axis=1)
    t = np.where(above, shade + 1, np.where(below, shade - 1, shade))
    t = np.clip(t, 1, 4).astype(np.int16)
    t[crack] = 0
    for _ in range(width * height // 90):
        x, y = r.randrange(width), r.randrange(height)
        X, Y = x * f, y * f
        if t[Y, X] > 1:
            t[Y, X] -= 1
    return t


def backdrop_cave(region: dict, fine: int = 1) -> Image.Image:
    """The rock behind a region's caves: dark, so a wall in front of it still reads; tiles both ways."""
    f = fine
    CAVE_RAMP = hexes(*region["cave"].split())
    t = cave_rock(CAVE_SIZE, CAVE_SIZE, "body " + region["key"], 26, f)
    rgba = np.zeros((CAVE_SIZE * f, CAVE_SIZE * f, 4), dtype=np.uint8)
    rgba[..., :3] = np.array(CAVE_RAMP, dtype=np.uint8)[t]
    rgba[..., 3] = 255
    return Image.fromarray(rgba, "RGBA")


def backdrop_cave_top(region: dict, fine: int = 1) -> Image.Image:
    """The top of the cave rock, where it meets the sky backdrop in an opening: a
    ragged edge; repeats sideways. `fine` asks the edge at every half pixel."""
    f = fine
    CAVE_RAMP = hexes(*region["cave"].split())
    t = cave_rock(CAVE_SIZE, CAVE_TOP_H, "top " + region["key"], 9, f)
    xs = np.arange(CAVE_SIZE * f) / f
    edge = 9 + periodic(xs, [(2, 3.5), (5, 2.0), (11, 1.2)], "cave top", CAVE_SIZE)
    rgba = np.zeros((CAVE_TOP_H * f, CAVE_SIZE * f, 4), dtype=np.uint8)
    rgba[..., :3] = np.array(CAVE_RAMP, dtype=np.uint8)[t]
    for X in range(CAVE_SIZE * f):
        top = int(round((CAVE_TOP_H - 14 - edge[X] * 0.9) * f))
        rgba[max(top, 0) :, X, 3] = 255
        if 0 <= top < CAVE_TOP_H * f:
            rgba[top, X, :3] = CAVE_RAMP[4]
    rgba[CAVE_TOP_H * f - 1, :, :3] = CAVE_RAMP[0]
    return Image.fromarray(rgba, "RGBA")


FAR_W, FAR_H = 640, 360


def backdrop_far(region: dict, fine: int = 1) -> Image.Image:
    """What stands far off in a region's caves: shapes a little lighter than the rock
    behind them, standing from the foot of the picture and hanging from its head; it
    repeats both ways. Columns, spires, fangs, heaps, bare trees, towers with lit
    windows, a viaduct, shards: FAR in tv2_base.py says which. `fine` draws them that many
    times the draft's size: points that are sharp, edges a pixel finer."""
    f = fine
    kind = FAR[region["key"]]
    ramp = hexes(*region["cave"].split())
    lit = tuple(min(255, int(c * 1.55) + 10) for c in ramp[4])
    mid = tuple(min(255, int(c * 1.3) + 6) for c in ramp[4])
    dark = tuple(min(255, int(c * 1.08) + 3) for c in ramp[4])
    FW, FH = FAR_W * f, FAR_H * f
    a = np.zeros((FH, FW, 4), dtype=np.uint8)
    r = rng("far", region["key"])

    def put(X, Y, colour):
        """A pixel of the picture, at a place in its own pixels."""
        if 0 <= Y < FH:
            a[Y, X % FW] = (*colour, 255)

    def block(x0, x1, y0, y1):
        """A shape's body: lit on its left edge, dark on its right (places in the draft's pixels)."""
        for X in range(x0 * f, x1 * f):
            x = X / f
            colour = lit if x - x0 < max(1, (x1 - x0) // 5) else dark if x1 - x <= max(1, (x1 - x0) // 4) else mid
            for Y in range(y0 * f, y1 * f):
                put(X, Y, colour)

    def needle(cx, base, tall, half, up=True):
        """A spike from a base row: standing (up) or hanging."""
        for I in range(tall * f):
            i = I / f
            if f == 1:
                wide = max(0, int(round(half * (1 - i / tall) ** 0.8)))
            else:
                wide = half * (1 - i / tall) ** 0.8
            Y = base * f + (f - 1 if up else 0) - I if up else base * f + I
            for X in range(int(math.floor((cx - wide) * f)) if f > 1 else cx - wide, int(math.ceil((cx + wide) * f)) + 1 if f > 1 else cx + wide + 1):
                x = X / f
                put(X, Y, lit if x < cx - wide * 0.3 else dark if x > cx + wide * 0.4 else mid)

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
            for DX in range(wide * f):
                dx = DX / f
                top = tall * np.sin(np.pi * (dx + 0.5 / f) / wide) ** 0.7
                top = int(top) if f == 1 else top
                for Y in range(int((FAR_H - top) * f), FH):
                    y = Y / f
                    put(x * f + DX, Y, lit if y - (FAR_H - top) < 2 else mid if dx < wide * 0.6 else dark)
            if r.random() < 0.7:
                pillar = x + wide + r.randint(6, 20)
                block(pillar, pillar + r.randint(8, 14), 0, FAR_H)
            x += wide + r.randint(40, 80)
        elif kind == "trees":
            tall, wide = r.randint(120, 240), r.randint(5, 9)
            block(x, x + wide, FAR_H - tall, FAR_H)
            for _ in range(r.randint(3, 6)):
                at, side, length, slope = r.randint(tall // 3, tall - 8), r.choice([-1, 1]), r.randint(14, 44), r.uniform(0.5, 0.9)
                for I in range(length * f):
                    i = I / f
                    bx = (x + (wide if side > 0 else 0)) * f + side * I
                    by = (FAR_H - at) * f - (int(i * slope) * f if f == 1 else int(i * slope * f))
                    put(bx, by, mid)
                    put(bx, by + 1, dark)
                    if f > 1 and i < length * 0.6:
                        put(bx, by + 2, dark)
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
                            left = (255, 150, 60) if r.random() < 0.5 else ramp[0]
                            right = (230, 110, 40) if r.random() < 0.5 else ramp[0]
                            for s in range(f):
                                for u in range(f):
                                    put(wx * f + s, (wy + dy) * f + u, left)
                                    put((wx + 1) * f + s, (wy + dy) * f + u, right)
            x += wide + r.randint(36, 90)
        else:  # arches: a viaduct right across
            break
    if kind == "arches":
        deck, span = FAR_H - 150, 80
        for X in range(FW):
            x = X / f
            along = x % span
            rise = (int(52 * np.sin(np.pi * along / span) ** 0.6) if f == 1 else 52 * np.sin(np.pi * along / span) ** 0.6) if 8 <= along < span - 8 else 0
            for Y in range(deck * f, FH):
                y = Y / f
                if y < deck + 14 or y - deck - 14 >= rise or along < 8 or along >= span - 8:
                    put(X, Y, lit if y < deck + 2 else dark if along >= span - 11 else mid)
        for x in range(30, FAR_W, 110):
            needle(x, 0, r.randint(40, 100), r.randint(4, 8), up=False)
    return Image.fromarray(a, "RGBA")


def backdrop_isles(name: str, height: int, count: int, wide, colours, falls: float, built: float, fine: int = 1) -> Image.Image:
    """Islands hanging in the sky far off, on nothing: a flat top with a lit rim, a body that
    narrows to a ragged point, on some a row of columns, from some a thread of falling
    water. `colours`: rim, top, body, under, water. The row repeats sideways. `fine` draws
    them that many times the draft's size: a rim a pixel high, a ragged point that is
    smooth between its jags, a thread of water a pixel wide."""
    f = fine
    W = BACKDROP_W * f
    im = Image.new("RGBA", (W, height * f), (0, 0, 0, 0))
    px = im.load()
    r = rng("isles", name)
    rim, top, body, under, water = colours
    slot = BACKDROP_W / count
    for i in range(count):
        half = r.randint(*wide)
        cx = int(i * slot + r.uniform(0.2, 0.8) * slot)
        cy = r.randint(26, height - 150)
        deep = int(half * r.uniform(0.95, 1.4))
        jag = [r.uniform(0.75, 1.15) for _ in range(half * 2 + 1)]
        foot = cy
        for DX in range(-half * f, half * f + 1):
            dx = DX / f
            n = int(math.floor(dx)) + half
            j = jag[n] if f == 1 or n >= 2 * half else jag[n] + (jag[n + 1] - jag[n]) * (dx - math.floor(dx))
            t = abs(dx) / (half + 0.5)
            down = max(1, int(deep * (1 - t**1.4) * j)) * f if f == 1 else max(f, int(deep * f * (1 - t**1.4) * j))
            x = (cx * f + DX) % W
            for k in range(down):
                kd = k / f
                colour = rim if k == 0 else top if kd < 3 else under if kd > down / f * 0.62 else body
                px[x, cy * f + k] = colour + (255,)
            if DX == 0:
                foot = cy + down / f
        if r.random() < built and half >= 18:
            # A row of columns under a lintel, off the middle.
            first = cx + r.randint(-half // 2, 0)
            for k in range(5):
                for Y in range((cy - 9) * f, cy * f):
                    for s in range(f):
                        px[((first + k * 4) * f + s) % W, Y] = (top if f > 1 and s == 0 else body) + (255,)
            for X in range((first - 1) * f, (first + 18) * f):
                for s in range(f):
                    px[X % W, (cy - 10) * f + s] = top + (255,)
        if r.random() < falls:
            x = (cx + r.randint(-half // 3, half // 3)) % BACKDROP_W
            long = r.randint(50, 120)
            for K in range(long * f):
                k = K // f if f == 1 else K / f
                y = foot * f + K - 2 * f
                if 0 <= y < height * f:
                    px[x * f, int(y)] = water + (max(0, 220 - int(200 * k) // long),)
    return im


def backdrop_rays(fine: int = 1) -> Image.Image:
    """Shafts of light slanting down from the left: bands of pale gold in three strengths,
    stepped at their edges, fading out at the top and the foot. Repeats sideways. `fine`
    asks the same light at every half pixel, so its slant is smooth."""
    f = fine
    width, height = BACKDROP_W, 1024
    ys, xs = np.mgrid[0 : height * f, 0 : width * f] / f
    along = (xs + ys * 0.42) % width
    strength = np.zeros(xs.shape)
    r = rng("rays")
    for _ in range(7):
        centre, half, power = r.uniform(0, width), r.uniform(10, 34), r.uniform(0.5, 1.0)
        away = np.minimum(np.abs(along - centre), width - np.abs(along - centre))
        strength = np.maximum(strength, power * np.clip(1 - away / half, 0, 1))
    fade = np.clip(np.minimum(ys / 220.0, (height - ys) / 300.0), 0, 1)
    level = strength * fade * 3.0
    a = np.zeros((height * f, width * f, 4), dtype=np.uint8)
    a[:, :, :3] = (255, 246, 204)
    a[:, :, 3] = np.round(stepped(level / 3.0, 6) * 39)
    return Image.fromarray(a, "RGBA")


def backdrop_cloud_band(name: str, height: int, rows, alpha: int, solid_under: bool, fine: int = 1) -> Image.Image:
    """A bank of cloud that repeats sideways: rows of round heads, back to front (`rows`:
    how many heads, the line their middles lie on, their sizes, their colours lit and in
    shade). With `solid_under` it is a sea, filled under the heads and thinning to nothing
    at its foot in steps of see-through; without, the heads float apart. `fine` draws it
    that many times the draft's size: each head a true round."""
    f = fine
    W = BACKDROP_W * f
    im = Image.new("RGBA", (W, height * f), (0, 0, 0, 0))
    px = im.load()
    r = rng("cloud band", name)
    for count, line, sizes, lit, shade_ in rows:
        heads = [(r.randrange(BACKDROP_W), line + r.randint(-10, 10), r.randint(*sizes)) for _ in range(count)]
        for X in range(W):
            x = X / f
            crest = None
            for hx, hy, rad in heads:
                dx = min(abs(x - hx), BACKDROP_W - abs(x - hx))
                if dx <= rad:
                    y = hy - (rad * rad - dx * dx) ** 0.5 * 0.7
                    crest = y if crest is None else min(crest, y)
            flat = crest is None
            if flat:
                if not solid_under:
                    continue
                crest = line + 14  # between two heads the sea lies flat, with no lit rim
            crest = int(crest) if f == 1 else crest
            floor = height if solid_under else int(crest + max(sizes) * 0.55)
            for Y in range(max(int(math.ceil(crest * f)) if f > 1 else crest, 0), min(floor * f, height * f)):
                y = Y / f
                colour = lit if y - crest < 4 and not flat else shade_
                show = alpha
                if solid_under:
                    show = int(round(alpha * float(stepped((height - y) / 70.0))))
                elif floor - y < 3:
                    show = int(alpha * (floor - y)) // 4
                px[X, Y] = colour + (show,)
    return im


def _stamp(canvas: np.ndarray, mask: np.ndarray, x0: int, y0: int, colours: np.ndarray):
    """Lays a patch of colour indices (0 for nothing) on a canvas, wrapping sideways."""
    h, w = mask.shape
    top, bottom = max(y0, 0), min(y0 + h, canvas.shape[0])
    if top >= bottom:
        return
    xs = np.arange(x0, x0 + w) % canvas.shape[1]
    part = mask[top - y0 : bottom - y0]
    block = canvas[top:bottom][:, xs]
    block[part > 0] = colours[part[part > 0]]
    canvas[top:bottom, xs] = block


def _mass(r, wide: int, tall: int, flat_top: int = 0, fine: int = 1) -> np.ndarray:
    """A heap of cloud as a patch of tones: 1 its lit crown, 2 its body, 3 the shade of
    its underside. Round heads piled highest in the middle on a level foot; with
    `flat_top` the heap is cut level that many pixels over its foot, to be built on.
    `fine` makes the patch that many times the draft's size, each head a true round."""
    f = fine
    pad = int(wide * 0.22) + 2
    h, w = tall + pad, wide + 2 * pad
    ys, xs = np.mgrid[0 : h * f, 0 : w * f]
    if f > 1:
        ys, xs = (ys + 0.5) / f - 0.5, (xs + 0.5) / f - 0.5
    inside = np.zeros(ys.shape, dtype=bool)
    middle, foot = w / 2, h - 1
    for _ in range(max(6, wide // 11)):
        hx = middle + r.uniform(-wide / 2, wide / 2)
        room = tall * (1 - (abs(hx - middle) / (wide / 2)) ** 2)
        rad = r.uniform(wide * 0.07, wide * 0.2)
        hy = foot - r.uniform(0.1, 1.0) * max(room - rad, rad * 0.4)
        inside |= (xs - hx) ** 2 + ((ys - hy) * 1.15) ** 2 <= rad * rad
    inside |= ((xs - middle) / (wide / 2)) ** 2 + ((ys - foot) / (tall * 0.28)) ** 2 <= 1
    if flat_top:
        inside[: (h - flat_top) * f] = False
    crest = np.where(inside.any(axis=0), inside.argmax(axis=0), h * f)
    depth = (np.mgrid[0 : h * f, 0 : w * f][0] - crest[None, :]) / f
    tones = np.zeros(ys.shape, dtype=np.uint8)
    tones[inside] = 2
    tones[inside & (depth < 5)] = 1
    # The shade of its underside: a band along its foot, its upper edge a slow wave.
    under = foot - ys
    tones[inside & (depth >= 5) & (under < 9 + 3 * np.sin(xs / 9.0))] = 3
    return tones


def _fade_foot(a: np.ndarray, rows: int):
    """Thins a picture out to nothing over its last rows, in steps of see-through."""
    h, w = a.shape[:2]
    ys, _ = np.mgrid[0:h, 0:w]
    a[:, :, 3] = np.round(a[:, :, 3] * stepped((h - ys) / float(rows), 12))


def backdrop_heaven_clouds(fine: int = 1) -> Image.Image:
    """A country of cloud far off: towers of it one behind another, the further the paler,
    with the sky between them. Repeats sideways; thins out toward the Earth. `fine` heaps
    it at that many times the draft's size."""
    f = fine
    width, height = BACKDROP_W, 1280
    canvas = np.zeros((height * f, width * f), dtype=np.uint8)
    r = rng("heaven clouds")
    palette = np.array([(0, 0, 0)] + list(hexes("b9cfee", "a7c1e8", "9ab5e0", "d0e0f5", "bcd1ef", "abc3e6", "e9f1fb", "d3e2f5", "bfd2ee")), dtype=np.uint8)
    masses = []
    for depth in range(3):
        for _ in range((10, 9, 7)[depth]):
            masses.append((depth, r.randrange(width), r.randint(170 + depth * 60, height - 40), r.randint(150, 250) + depth * 40, r.randint(90, 170) + depth * 30))
    for depth, cx, foot, wide, tall in sorted(masses, key=lambda m: (m[0], m[2])):
        tones = _mass(r, wide, tall, fine=f)
        colours = np.array([0, 1 + depth * 3, 2 + depth * 3, 3 + depth * 3], dtype=np.uint8)
        _stamp(canvas, tones, cx * f - tones.shape[1] // 2, foot * f - tones.shape[0], colours)
    a = np.zeros((height * f, width * f, 4), dtype=np.uint8)
    a[:, :, :3] = palette[canvas]
    a[:, :, 3] = np.where(canvas > 0, 255, 0)
    _fade_foot(a, 300 * f)
    return Image.fromarray(a, "RGBA")


def _building(r, kind: str, wide: int, tall: int, fine: int = 1) -> np.ndarray:
    """One building of the far castles as a patch of tones: 1 lit, 2 its body, 3 a dark
    opening, 4 a roof. `fine` makes it that many times the draft's size: its lit edge and
    its openings a pixel finer, its spires and roofs smooth to their points."""
    f = fine
    cap = wide // 2 + 6
    h, w = tall + 84, wide + 4
    t = np.zeros((h * f, w * f), dtype=np.uint8)

    def fill(y0, y1, x0_, x1_, v):
        t[int(y0 * f) : int(y1 * f), int(x0_ * f) : int(x1_ * f)] = v

    def taper(row0, steps, middle, reach_of, v_of):
        """A roof up from row0: `steps` draft rows, each as wide as reach_of(k) either side."""
        for K in range(steps * f):
            k = K / f
            reach = reach_of(k)
            v = v_of(int(k))
            Y = row0 * f - 1 - K
            if f == 1:
                t[Y, int(round(middle - reach)) : int(round(middle + reach)) + 1] = v
            else:
                t[Y, int(round((middle - reach) * f)) : int(round((middle + reach) * f)) + f] = v

    x0, x1, foot = 2, 2 + wide, h
    top = foot - tall
    lit = 2 if f == 1 else 1.5
    if kind == "tower":
        fill(top, foot, x0, x1, 2)
        fill(top, foot, x0, x0 + lit, 1)
        for y in range(top + 8, foot - 10, 14):
            fill(y, y + 6, (x0 + x1) // 2 - 1, (x0 + x1) // 2 + 1, 3)
        taper(top, cap, (x0 + x1) / 2, lambda k: max((wide / 2) * (1 - k / cap), 0.6), lambda k: 4 if k < cap - 3 else 1)
    elif kind == "keep":
        # A great block under battlements, a taller tower with a pointed roof out of its middle.
        fill(top, foot, x0, x1, 2)
        fill(top, foot, x0, x0 + lit, 1)
        for x in range(x0, x1, 6):
            fill(top - 4, top, x, x + 3, 2)
        for x in range(x0 + 5, x1 - 4, 9):
            fill(top + 10, foot - 6, x, x + 3, 3)
            fill(top + 9, top + 10, x + 1, x + 2, 3)
        middle, half = (x0 + x1) // 2, wide // 6 + 2
        fill(top - 30, top, middle - half, middle + half, 2)
        fill(top - 30, top, middle - half, middle - half + lit, 1)
        taper(top - 30, half * 2 + 4, middle, lambda k: max(half * (1 - k / (half * 2 + 4)), 0.6), lambda k: 4)
    elif kind == "wall":
        # A curtain wall on round arches, battlements along it.
        fill(top, top + 8, x0 - 1, x1 + 1, 2)
        fill(top, top + 1, x0 - 1, x1 + 1, 1)
        for x in range(x0, x1, 6):
            fill(top - 4, top, x, x + 3, 2)
        for x in range(x0, x1 - 1, 12):
            fill(top + 8, foot, x, x + 4, 2)
            fill(top + 8, foot, x, x + (1 if f == 1 else 0.5), 1)
    else:  # a slender tower
        fill(top, foot, x0 + wide // 2 - 3, x0 + wide // 2 + 3, 2)
        fill(top, foot, x0 + wide // 2 - 3, x0 + wide // 2 - 3 + (1 if f == 1 else 0.5), 1)
        taper(top, 12, x0 + wide / 2, lambda k: max(3.5 * (1 - k / 12), 0.6), lambda k: 4)
    return t


def backdrop_heaven_city(fine: int = 1) -> Image.Image:
    """Castles far off, on banks of cloud one over another: towers under pointed roofs of
    blue, keeps, walls on arches, a thread of water falling from a bank's edge here and
    there. Pale: they are a long way off. Repeats sideways. `fine` builds them at that
    many times the draft's size."""
    f = fine
    width, height = BACKDROP_W, 1344
    canvas = np.zeros((height * f, width * f), dtype=np.uint8)
    r = rng("heaven city")
    # Tones per shelf, far to near: cloud lit, body, shade; stone lit, body, dark; gold.
    ramps = [
        hexes("d3e1f5", "c0d2ee", "b0c5e8", "cfdcf2", "b6c8e8", "9fb4dc", "8ea8de"),
        hexes("dfe9f8", "c9d9f1", "b5c9ea", "d9e3f5", "bccdea", "a0b5dc", "7f9fde"),
        hexes("ecf2fb", "d4e1f4", "bccfec", "e6ecf8", "c3d1ec", "a2b6dc", "6f93dc"),
    ]
    palette = np.array([(0, 0, 0)] + [c for ramp in ramps for c in ramp] + [hexes("f2f7fd")[0]], dtype=np.uint8)
    water = len(palette) - 1
    shelves = [(k % 3, (k * 250 + r.randint(-60, 60)) % width, 170 + k * 100 + r.randint(-24, 24), r.randint(240, 400)) for k in range(12)]
    for depth, cx, foot, wide in sorted(shelves, key=lambda s: s[0]):
        base = 1 + depth * 7
        # The buildings first, their feet a little down in the cloud, then the cloud over their feet.
        x = cx - wide // 2 + 34
        while x < cx + wide // 2 - 44:
            kind = r.choice(["tower", "keep", "wall", "tower", "slender", "keep", "slender"])
            bw, bt = {"tower": (r.randint(12, 18), r.randint(70, 140)), "keep": (r.randint(44, 70), r.randint(44, 64)), "wall": (r.randint(50, 84), r.randint(34, 46)), "slender": (10, r.randint(56, 110))}[kind]
            piece = _building(r, kind, bw, bt, fine=f)
            _stamp(canvas, piece, x * f, (foot - 30) * f - piece.shape[0], np.array([0, base + 3, base + 4, base + 5, base + 6], dtype=np.uint8))
            x += bw + r.randint(4, 22)
        shelf = _mass(r, wide, 44, fine=f)
        _stamp(canvas, shelf, cx * f - shelf.shape[1] // 2, foot * f - shelf.shape[0], np.array([0, base, base + 1, base + 2], dtype=np.uint8))
        for _ in range(2):
            fx, long = cx + r.randint(-wide // 2 + 20, wide // 2 - 20), r.randint(70, 150)
            for K in range(long * f):
                k = K / f
                Y = (foot - 6) * f + K
                if Y < height * f and (k < long * 0.6 or int(k) % 2 == 0):
                    canvas[Y, (fx * f) % (width * f)] = water
    a = np.zeros((height * f, width * f, 4), dtype=np.uint8)
    a[:, :, :3] = palette[canvas]
    a[:, :, 3] = np.where(canvas > 0, 255, 0)
    _fade_foot(a, 260 * f)
    return Image.fromarray(a, "RGBA")


def backdrop_cirrus(fine: int = 1) -> Image.Image:
    """Thin cloud high up: long streaks, their ends thinning out. Repeats sideways. `fine`
    asks the same streaks at every half pixel."""
    f = fine
    width, height = BACKDROP_W, 128
    a = np.zeros((height * f, width * f, 4), dtype=np.uint8)
    r = rng("cirrus")
    ys, xs = np.mgrid[0 : height * f, 0 : width * f] / f
    for _ in range(16):
        cx, cy, long, thick = r.randrange(width), r.randint(12, height - 12), r.randint(70, 210), r.uniform(2.5, 7)
        dx = np.minimum(np.abs(xs - cx), width - np.abs(xs - cx))
        d = (dx / (long / 2)) ** 2 + ((ys - cy - dx * 0.04) / thick) ** 2
        show = np.round(stepped((1 - d) * 1.6, 4) * 235).astype(np.uint8)
        more = show > a[:, :, 3]
        a[more] = (244, 248, 253, 0)
        a[:, :, 3] = np.where(more, show, a[:, :, 3])
    return Image.fromarray(a, "RGBA")


def backdrop_glory(fine: int = 1) -> Image.Image:
    """A glory: rings and rays of pale gold round a middle, see-through, in a few
    strengths. Stood behind what is holiest. `fine` asks it at every half pixel."""
    f = fine
    size = 640
    ys, xs = np.mgrid[0 : size * f, 0 : size * f] / f
    dx, dy = xs - size / 2 + 0.5, ys - size / 2 + 0.5
    d = np.hypot(dx, dy)
    angle = np.arctan2(dy, dx)
    rays = (np.cos(angle * 16) > 0.55) | (np.cos(angle * 8 + 0.4) > 0.9)
    rings = (np.abs(d - 96) < 3) | (np.abs(d - 150) < 2) | (np.abs(d - 214) < 1.5)
    strength = np.clip(1 - d / (size / 2), 0, 1) * np.where(rays | rings, 1.0, 0.32)
    a = np.zeros((size * f, size * f, 4), dtype=np.uint8)
    a[:, :, :3] = (255, 240, 190)
    a[:, :, 3] = np.round(stepped(strength * 0.8, 8) * 110)
    return Image.fromarray(a, "RGBA")


def backdrop_rainbow(fine: int = 1) -> Image.Image:
    """Half a rainbow's ring, standing on its two feet: seven thin bands, see-through, their
    feet thinning out in steps. `fine` draws its bands as true rounds."""
    f = fine
    width, height = 416, 208
    im = Image.new("RGBA", (width * f, height * f), (0, 0, 0, 0))
    px = im.load()
    bands = hexes("ff6b6b", "ffa24d", "ffe066", "8ce08a", "66c8f0", "6f8cf0", "b07ce8")
    outer, thick = 204.0, 2.6
    for Y in range(height * f):
        y = Y / f
        for X in range(width * f):
            x = X / f
            d = ((x - width / 2 + 0.5) ** 2 + (height - y) ** 2) ** 0.5
            k = (outer - d) / thick
            if 0 <= k < len(bands):
                fade = float(stepped((height - y) / 46.0, 6))
                px[X, Y] = bands[int(k)] + (int(round(112 * fade)),)
    return im


def skies(fine: int = 1) -> dict:
    """The sky's own layers, by key, each a painter at `fine` times the draft's size."""
    f = fine
    return {
        "backdrop_stars": lambda: backdrop_stars(f),
        "backdrop_sun": lambda: backdrop_sun(f),
        "backdrop_moon": lambda: backdrop_moon(f),
        "backdrop_clouds": lambda: backdrop_clouds(f),
        "backdrop_heaven_far": lambda: backdrop_isles("far", 360, 6, (14, 30), hexes("eef5fd", "d4e3f5", "bcd2ee", "a9c3e6", "f4f9ff"), 0.6, 0.4, f),
        "backdrop_heaven_near": lambda: backdrop_isles("near", 480, 3, (34, 60), hexes("ffffff", "c6e2b4", "a3bbe2", "869fd0", "eaf4ff"), 0.8, 0.7, f),
        "backdrop_rays": lambda: backdrop_rays(f),
        "backdrop_heaven_clouds": lambda: backdrop_heaven_clouds(f),
        "backdrop_heaven_city": lambda: backdrop_heaven_city(f),
        "backdrop_cirrus": lambda: backdrop_cirrus(f),
        "backdrop_glory": lambda: backdrop_glory(f),
        "backdrop_cloudmid": lambda: backdrop_cloud_band(
            "mid",
            240,
            [
                (14, 84, (24, 44), hexes("dde7f6")[0], hexes("c6d6ef")[0]),
                (13, 116, (20, 36), hexes("eff4fc")[0], hexes("d8e3f5")[0]),
                (12, 146, (16, 30), hexes("ffffff")[0], hexes("e8effa")[0]),
            ],
            255,
            True,
            f,
        ),
        "backdrop_rainbow": lambda: backdrop_rainbow(f),
        "backdrop_cloudsea": lambda: backdrop_cloud_band(
            "sea",
            208,
            [
                (16, 78, (26, 46), hexes("dbe6f6")[0], hexes("c2d3ee")[0]),
                (15, 104, (22, 40), hexes("f0f5fc")[0], hexes("d5e1f4")[0]),
                (14, 132, (18, 34), hexes("ffffff")[0], hexes("e6eefa")[0]),
            ],
            255,
            True,
            f,
        ),
        "backdrop_cloudfront": lambda: backdrop_cloud_band(
            "front", 192, [(5, 70, (16, 30), hexes("ffffff")[0], hexes("e8effa")[0]), (4, 130, (12, 24), hexes("ffffff")[0], hexes("e8effa")[0])], 120, False, f
        ),
    }


def backdrops() -> dict[str, Image.Image]:
    caves = {}
    for region in REGIONS:
        if region["cave"]:
            caves["backdrop_cave_" + region["key"]] = tv2_earth.cave_earth(region) if region["key"] == "earth" else backdrop_cave(region)
            caves["backdrop_cave_top_" + region["key"]] = backdrop_cave_top(region)
            caves["backdrop_far_" + region["key"]] = tv2_earth.far_mine(region) if FAR[region["key"]] == "mine" else backdrop_far(region)
    earth = tv2_earth.pictures()
    earth.update(tv2_heaven.pictures())
    earth.update(tv2_eden.pictures())
    earth.update({key: paint() for key, _, _, paint in tv2_earth.RIDGES})
    earth.update({key: paint() for key, paint in tv2_earth.LAYERS.items()})
    return {
        **caves,
        **{key: paint() for key, paint in skies().items()},
        "backdrop_far": backdrop_ridge(
            "far", hexes("7fa3c4")[0], hexes("a3c2dc")[0], 118, [(1, 30), (2, 20), (3, 12), (7, 6), (13, 3), (23, 2)], 1
        ),
        "backdrop_mid": backdrop_ridge(
            "mid", hexes("4f8a7c")[0], hexes("69a48f")[0], 72, [(1, 12), (3, 10), (5, 6), (11, 3)], 2, trees=(4, 8)
        ),
        "backdrop_near": backdrop_ridge(
            "near", hexes("2f5e4c")[0], hexes("427a60")[0], 40, [(2, 8), (5, 5), (9, 3)], 3, trees=(6, 12)
        ),
        **earth,
    }


# What is drawn at the size that ships itself (tv2_base.DENSITY), by key; the rest ships as
# its draft through Scale2x. A picture here is the draft's size times DENSITY exactly.
FINE = skies(DENSITY)
for _region in REGIONS:  # the rock behind the caves, and its ragged top
    if _region["cave"]:
        if _region["key"] != "earth":
            FINE["backdrop_cave_" + _region["key"]] = lambda region=_region: backdrop_cave(region, DENSITY)
        FINE["backdrop_cave_top_" + _region["key"]] = lambda region=_region: backdrop_cave_top(region, DENSITY)
        if _region["key"] == "earth":
            FINE["backdrop_cave_earth"] = lambda region=_region: tv2_earth.cave_earth(region, DENSITY)
        far = tv2_earth.far_mine if FAR[_region["key"]] == "mine" else backdrop_far
        FINE["backdrop_far_" + _region["key"]] = lambda region=_region, paint=far: paint(region, DENSITY)
