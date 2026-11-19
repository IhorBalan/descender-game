#!/usr/bin/env python3
"""Draws what the Earth of the Tiles V2 world is dressed with: its trees, what is left of
the village by the start, the pithead over the old shaft, the timber and rails of the old
mine, the reeds and lilies of the pond, the graves before the Gate, the ridges behind the
surface and the rock and the old workings behind the mine. Not a script.

Everything here is a picture (`picture` in BACKDROPS): a sprite stood in a cell by the
map (`@mark picture column row key`, its foot the bottom middle of the cell), drawn
at one art pixel a pixel and lit by the tile light like the ground. None of it is solid
and none of it is in the way: the ground, the ledges and the lamps are the map's own.
`tv2_earth_map.py` says where each stands.

The art follows the research notes (earth-art-research.md in the session notes): the
light from the top left, hue-shifted ramps (the shadows cool, the lit side warm), crowns
built of a few rounded bundles and never one ball on a stick, everything further off
paler, bluer and plainer, the props of the background darker and less saturated than
the ground the hero walks on.
"""
from __future__ import annotations

import math

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

SINK = TILE  # px of a picture stood on the ground that go on down behind the ground (roots, the foot of a post)
PLINTH = 3 * TILE  # the stone a building stands on, down into the ground: it stands on a slope too

# --- Colours ------------------------------------------------------------------------


def ramp_of(text: str) -> list:
    return hexes(*text.split())


OAK = ramp_of("10241c 173a2a 1f5232 2e6e36 4a8c3a 72ab42 a6cc58")  # outline, then darkest .. brightest
AUTUMN = ramp_of("2a120a 5a2a14 8a3e18 b25a1e d27e2a eaa840 f6d070")
APPLE = ramp_of("10241c 18402a 23592f 357636 529440 7cb24a b2d264")
PINE = ramp_of("0a1a18 10302a 174236 1f5640 2c6b48 3f8352 5c9c5e")
WILLOW = ramp_of("12281a 1f4a30 2f6a3a 4c8a3e 74a848 a2c45e c8dc78")
BARK = ramp_of("1a110c 2e1f17 4a3122 64432c 7e5838 9a7048")
GREYWOOD = ramp_of("1c1a1a 2a2624 3e3833 564e46 70675c 8a8174")
STONEWORK = ramp_of("26222c 48434e 5f5964 787079 918890 aca3a7 c8bfbf")
PLASTER = ramp_of("3a3028 7a6a54 968466 b09e7c c8b896 dcd0b0")
TIMBER = ramp_of("1a100a 33200f 4a2f18 634021 7d532c 976a3a")
ROOF = ramp_of("2a0e0a 4a1c16 6e2a1e 8e3a26 ae5232 c86e42")
THATCH = ramp_of("2a1e0e 5a4220 7a5c2c 9a7838 ba9648 d4b45c")
IRON = ramp_of("101014 24262c 3a3d46 565a64 7a7f8a a4a8b0")
COPPER = ramp_of("2a160a 6e3a1c 9a5428 c06e34 de8c44 f0ac62")
GLOW = ramp_of("ff9a3a ffc65a fff0b0")
DARK = hexes("120e12")[0]


# --- A canvas -----------------------------------------------------------------------


class Art:
    """A picture being drawn: x to the right, y down, RGBA."""

    def __init__(self, width: int, height: int):
        self.w, self.h = width, height
        self.a = np.zeros((height, width, 4), dtype=np.uint8)

    def put(self, x, y, colour, alpha: int = 255):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            self.a[y, x, :3] = colour[:3]
            self.a[y, x, 3] = alpha

    def has(self, x, y) -> bool:
        x, y = int(x), int(y)
        return 0 <= x < self.w and 0 <= y < self.h and self.a[y, x, 3] > 0

    def rect(self, x0, y0, x1, y1, colour):
        for y in range(int(y0), int(y1)):
            for x in range(int(x0), int(x1)):
                self.put(x, y, colour)

    def fill(self, mask: np.ndarray, colour):
        self.a[mask, :3] = colour[:3]
        self.a[mask, 3] = 255

    def line(self, x0, y0, x1, y1, colour, width: float = 1.0):
        steps = int(max(abs(x1 - x0), abs(y1 - y0), 1) * 2)
        half = (width - 1) / 2
        for i in range(steps + 1):
            t = i / steps
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for dx in range(-int(math.ceil(half)), int(math.ceil(half)) + 1):
                for dy in range(-int(math.ceil(half)), int(math.ceil(half)) + 1):
                    if dx * dx + dy * dy <= half * half + 0.6:
                        self.put(round(x + dx), round(y + dy), colour)

    def outline(self, colour, alpha: int = 255, diagonal: bool = False):
        """A 1 px edge round everything drawn so far."""
        solid = self.a[..., 3] > 0
        edge = np.zeros_like(solid)
        steps = [(1, 0), (-1, 0), (0, 1), (0, -1)] + ([(1, 1), (-1, -1), (1, -1), (-1, 1)] if diagonal else [])
        for dx, dy in steps:
            edge |= shift(solid, dx, dy, False)
        edge &= ~solid
        self.a[edge, :3] = colour[:3]
        self.a[edge, 3] = alpha

    def under(self, other: "Art", x0: int = 0, y0: int = 0):
        """Lays this picture over `other` at (x0, y0) (only where this one has something)."""
        h, w = self.h, self.w
        sub = other.a[y0 : y0 + h, x0 : x0 + w]
        mask = self.a[: sub.shape[0], : sub.shape[1], 3] > 0
        sub[mask] = self.a[: sub.shape[0], : sub.shape[1]][mask]

    def image(self) -> Image.Image:
        return Image.fromarray(self.a, "RGBA")


def noise(seed, w: int, h: int, scale: float) -> np.ndarray:
    """Smooth value noise, -1 .. 1, the size of a picture."""
    r = np.random.default_rng(_seed(seed))
    gw, gh = int(w / scale) + 2, int(h / scale) + 2
    grid = r.uniform(-1, 1, (gh, gw))
    ys, xs = np.mgrid[0:h, 0:w]
    fx, fy = xs / scale, ys / scale
    ix, iy = fx.astype(int), fy.astype(int)
    tx, ty = fx - ix, fy - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    top = grid[iy, ix] * (1 - tx) + grid[iy, ix + 1] * tx
    low = grid[iy + 1, ix] * (1 - tx) + grid[iy + 1, ix + 1] * tx
    return top * (1 - ty) + low * ty


def _seed(seed) -> int:
    h = 2166136261
    for ch in str(seed):
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


# --- Trees --------------------------------------------------------------------------


def bundle(art: Art, cx, cy, rx, ry, ramp, r, seed, gaps: bool = True):
    """One rounded bundle of leaves, shaded as a ball lit from the top left, its edge
    lumpy, a dark rim where it lies over a bundle drawn before it, and small clumps of
    lighter and darker leaves on it so it is foliage and not a disc."""
    h, w = art.h, art.w
    ys, xs = np.mgrid[0:h, 0:w]
    nx, ny = (xs + 0.5 - cx) / rx, (ys + 0.5 - cy) / ry
    ang = np.arctan2(ny, nx)
    lump = 1 + 0.10 * np.sin(ang * 5 + r.uniform(0, 6.3)) + 0.07 * np.sin(ang * 11 + r.uniform(0, 6.3))
    d = np.sqrt(nx * nx + ny * ny) / lump
    inside = d <= 1.0
    jitter = noise((seed, "leaf"), w, h, 2.2) * 0.35
    light = -(nx * 0.55 + ny * 0.85) * 0.75 + (1 - d) * 0.35 + jitter
    index = np.clip(np.round(light * 2.0 + 3.0), 1, len(ramp) - 1).astype(int)
    index[(ny > 0.45) & (index > 2)] -= 1  # the underside is in its own shade
    covered = art.a[..., 3] > 0
    rim = inside & (d > 0.86) & covered & (ny > -0.6)
    colours = np.array(ramp, dtype=np.uint8)[index]
    art.a[inside, :3] = colours[inside]
    art.a[inside, 3] = 255
    art.a[rim, :3] = ramp[1]
    # Clumps of leaves: a lit one up and to the left, a dark one under it.
    for _ in range(int(rx * ry / 9)):
        x, y = int(cx + r.uniform(-rx, rx) * 0.85), int(cy + r.uniform(-ry, ry) * 0.85)
        if not (0 <= x < w - 2 and 1 <= y < h - 1) or not inside[y, x]:
            continue
        k = index[y, x]
        lit = ramp[min(k + 1, len(ramp) - 1)]
        art.put(x, y, lit)
        art.put(x + 1, y, lit)
        art.put(x, y + 1, ramp[max(k - 1, 1)])
        art.put(x + 1, y + 1, ramp[max(k - 1, 1)])
    if gaps:
        # A hole or two near the bottom where the branches show.
        for _ in range(r.randint(0, 2)):
            x, y = int(cx + r.uniform(-rx, rx) * 0.5), int(cy + ry * r.uniform(0.2, 0.55))
            for dx in range(-1, 2):
                for dy in range(0, 2):
                    if 0 <= x + dx < w and 0 <= y + dy < h and inside[y + dy, x + dx]:
                        art.put(x + dx, y + dy, ramp[0])


def trunk(art: Art, x_foot, y_foot, height, base, top, ramp, r, lean: float = 0.0, roots: bool = True):
    """A trunk from its foot up, wider at the root, lit on its left; bark streaks and a knot."""
    for i in range(height):
        t = i / max(height - 1, 1)
        half = (base + (top - base) * t) / 2
        cx = x_foot + lean * t * t * height * 0.15
        y = y_foot - i
        for x in range(int(math.floor(cx - half)), int(math.ceil(cx + half))):
            off = (x + 0.5 - cx) / max(half, 0.5)
            k = 4 if off < -0.45 else 2 if off > 0.35 else 3
            if (x * 3 + i // 3 + int(cx)) % 7 == 0 and -0.4 < off < 0.4:
                k = 2
            art.put(x, y, ramp[k])
    if roots:
        for side in (-1, 1):
            for k in range(4):
                art.put(x_foot + side * (base // 2 + k), y_foot - 1 + (k // 2), ramp[2 if side > 0 else 3])
                art.put(x_foot + side * (base // 2 + k), y_foot + (k // 2), ramp[1])
    if height > 12:
        ky = y_foot - int(height * r.uniform(0.35, 0.6))
        art.put(x_foot, ky, ramp[1])
        art.put(x_foot + 1, ky, ramp[1])
        art.put(x_foot, ky - 1, ramp[5])


def branch(art: Art, x0, y0, x1, y1, width, ramp):
    art.line(x0, y0, x1, y1, ramp[2], width)
    art.line(x0 - 0.6, y0 - 0.6, x1 - 0.6, y1 - 0.6, ramp[4], max(width - 1.5, 1))


def dot(art: Art, x, y, colour, f: int, lit=None):
    """A draft pixel at `f` times the draft's size: a block of f by f, its top left
    corner in `lit` if given."""
    for s in range(f):
        for u in range(f):
            art.put(int(x * f) + s, int(y * f) + u, lit if lit is not None and s == u == 0 else colour)


def oak(variant: int, leaves=OAK, tall: int | None = None, fruit=None, turn: int = 6, fine: int = 1) -> Image.Image:
    """A broad tree of a few bundles on branches that part from the trunk well over the
    hero's head; asymmetric, with a low bundle on one side. `turn` is one more number in
    what its shape is drawn from, so trees in other leaves are other trees: always the
    same number for the same picture, or the picture would change each time it is drawn.
    `fine` draws it that many times the draft's size: leaf by leaf, a light on each fruit."""
    r = rng("oak", variant, turn)
    f = fine
    tall = tall or r.randint(118, 160)
    wide = int(tall * r.uniform(0.62, 0.78))
    W, H = wide + 16, tall + SINK + 4
    art = Art(W * f, H * f)
    foot_x, foot_y = W // 2 + r.randint(-4, 4), H - SINK + 6
    split = int(tall * r.uniform(0.38, 0.5))  # the height the branches part at
    crown_cy = H - SINK - tall + int(tall * 0.36)
    trunk(art, foot_x * f, foot_y * f, (split + 12) * f, r.randint(9, 12) * f, 6 * f, BARK, r)
    # Where the bundles sit, and a branch to each.
    bundles = []
    count = r.randint(6, 8)
    for i in range(count):
        a = -math.pi * (0.12 + 0.76 * i / (count - 1)) + r.uniform(-0.15, 0.15)
        dist = r.uniform(0.45, 0.85)
        bx = W / 2 + math.cos(a) * wide * 0.36 * dist * 1.25
        by = crown_cy + math.sin(a) * tall * 0.24 * dist + r.uniform(-4, 6)
        bundles.append((bx, by, r.uniform(0.17, 0.25) * wide, r.uniform(0.15, 0.2) * tall))
    # A low bundle to one side, and a top one.
    side = r.choice([-1, 1])
    bundles.append((W / 2 + side * wide * 0.33, crown_cy + tall * 0.17, wide * 0.17, tall * 0.12))
    bundles.append((W / 2 + r.uniform(-6, 6), crown_cy - tall * 0.2, wide * 0.24, tall * 0.17))
    top_y = foot_y - split - 10
    for bx, by, _, _ in bundles:
        branch(art, foot_x * f, (top_y + 8) * f, bx * f, (by + 6) * f, (4 if abs(bx - foot_x) > wide * 0.2 else 5) * f, BARK)
    # Back to front: the lowest and darkest first, so the upper ones lie over them.
    for k, (bx, by, rx, ry) in enumerate(sorted(bundles, key=lambda b: -b[1])):
        bundle(art, bx * f, by * f, rx * f, ry * f, leaves, r, (variant, k))
    if fruit:
        for _ in range(16):
            x, y = r.randint(8, W - 8), r.randint(H - SINK - tall + 10, H - SINK - split)
            if art.has(x * f, y * f) and art.has((x + 1) * f, (y + 1) * f):
                if f == 1:
                    art.put(x, y, fruit[1])
                    art.put(x + 1, y, fruit[0])
                    art.put(x, y + 1, fruit[0])
                    art.put(x + 1, y + 1, fruit[0])
                    continue
                for dy in range(2 * f):
                    for dx in range(2 * f):
                        if (dx - f + 0.5) ** 2 + (dy - f + 0.5) ** 2 <= f * f + 0.5:
                            art.put(x * f + dx, y * f + dy, fruit[1] if dx + dy < f else fruit[0])
                art.put(x * f + 1, y * f + 1, (255, 255, 255))
    for _ in range(f):
        art.outline(leaves[0])
    return art.image()


def pine(variant: int, fine: int = 1) -> Image.Image:
    """A fir: tiers of drooping boughs on a straight trunk, a sharp top, lit on the left.
    `fine` draws it that many times the draft's size: needles in finer droops, a sharp
    leader."""
    r = rng("pine", variant)
    f = fine
    tall = r.randint(130, 190)
    wide = int(tall * r.uniform(0.34, 0.42))
    W, H = wide + 12, tall + SINK + 4
    art = Art(W * f, H * f)
    cx, foot = W // 2, H - SINK + 6
    trunk(art, cx * f, foot * f, (tall - 10) * f, 7 * f, 3 * f, BARK, r, roots=True)
    tiers = r.randint(7, 9)
    top = H - SINK - tall
    for i in range(tiers):
        t = (i + 1) / tiers
        y_low = top + int(tall * 0.12 + (tall * 0.8) * t)
        half = 4 + (wide / 2 - 4) * t**0.85
        height = int(tall * 0.8 / tiers * 1.7)
        for Y in range((y_low - height) * f, (y_low + 1) * f):
            y = Y / f
            s = (y - (y_low - height)) / height
            span = half * s**0.9
            for X in range(int((cx - span) * f), int((cx + span) * f) + 1 if f == 1 else int((cx + span) * f) + f):
                x = X / f
                off = (x + 0.5 / f - cx) / max(half, 1)
                droop = (X * 5 + Y) % 4 == 0 and y > y_low - 2
                k = 5 if off < -0.35 and s < 0.7 else 4 if off < 0 else 3 if off < 0.4 else 2
                if s > 0.82:
                    k = max(k - 2, 1)
                if f > 1 and k > 2 and (X * 3 + Y * 7) % 11 == 0:
                    k -= 1  # a needle in shade
                if not droop:
                    art.put(X, Y, PINE[k])
            if Y % f == 0:  # the ragged foot of the tier, as each draft row of it would have it
                for x in range(int(cx - span), int(cx + span) + 1, 3):
                    for s_ in range(f):
                        art.put(x * f, (y_low + 1) * f + s_, PINE[1])
    for Y in range(top * f, (top + 6) * f):
        art.put(cx * f, Y, PINE[4])
    for _ in range(f):
        art.outline(PINE[0])
    return art.image()


def willow(variant: int, fine: int = 1) -> Image.Image:
    """A willow over the water: a round head of bundles and long strands that hang to the
    ground, the light catching their left side. `fine` draws it that many times the
    draft's size: strands a pixel wide that sway smoothly, leaf by leaf in its head."""
    r = rng("willow", variant)
    f = fine
    tall, wide = r.randint(120, 140), r.randint(110, 130)
    W, H = wide + 16, tall + SINK + 4
    art = Art(W * f, H * f)
    cx, foot = W // 2, H - SINK + 6
    trunk(art, (cx - 4) * f, foot * f, int(tall * 0.55) * f, 12 * f, 7 * f, BARK, r, lean=1.5)
    head = H - SINK - tall
    for k in range(5):
        bundle(art, (cx + r.uniform(-wide * 0.3, wide * 0.3)) * f, (head + 22 + r.uniform(-6, 10)) * f, wide * 0.24 * f, 22 * f, WILLOW, r, ("willow", variant, k), gaps=False)
    # The strands, from the head's edge down.
    for x in range(8, W - 8):
        off = (x - cx) / (wide / 2)
        if abs(off) > 1:
            continue
        start = head + 22 + int(18 * (1 - off * off) ** 0.5) - 6
        length = int((tall * 0.55) * (0.55 + 0.45 * abs(off)) * r.uniform(0.7, 1.0))
        if f == 1:
            if (x + variant) % 2:
                continue
            for y in range(start, min(start + length, H - SINK + 2)):
                sway = int(round(math.sin((y - start) / 9.0 + x * 0.3)))
                k = 5 if off < -0.3 else 3 if off < 0.4 else 2
                art.put(x + sway, y, WILLOW[k if (y - start) % 5 else k - 1])
            continue
        if (x + variant) % 2:
            continue
        for Y in range(start * f, min(start + length, H - SINK + 2) * f):  # a strand that sways smoothly, lit on its left
            y = Y / f
            sway = round(math.sin((y - start) / 9.0 + x * 0.3) * f)
            k = 5 if off < -0.3 else 3 if off < 0.4 else 2
            k = k if int(y - start) % 5 else k - 1
            art.put(x * f + sway, Y, WILLOW[min(k + 1, len(WILLOW) - 1)])
            art.put(x * f + sway + 1, Y, WILLOW[k])
    for _ in range(f):
        art.outline(WILLOW[0])
    return art.image()


def dead_tree(variant: int, fine: int = 1) -> Image.Image:
    """A tree long dead: grey wood, a split trunk, branches that fork and thin out. `fine`
    draws it that many times the draft's size, its twigs as thin as twigs are."""
    r = rng("deadtree", variant)
    f = fine
    tall = r.randint(90, 130)
    W0, H0 = int(tall * 0.8) + 10, tall + SINK + 4
    art = Art(W0 * f, H0 * f)
    cx, foot = W0 // 2, H0 - SINK + 6
    trunk(art, cx * f, foot * f, int(tall * 0.55) * f, 9 * f, 5 * f, GREYWOOD, r)

    def grow(x, y, angle, length, width, depth):
        x1, y1 = x + math.cos(angle) * length, y + math.sin(angle) * length
        branch(art, x * f, y * f, x1 * f, y1 * f, width * f if f == 1 or width > 1 else 1.5, GREYWOOD)
        if depth > 0:
            for turn in (-0.45, 0.4) if depth > 1 or r.random() < 0.7 else (r.choice([-0.4, 0.4]),):
                grow(x1, y1, angle + turn + r.uniform(-0.2, 0.2), length * r.uniform(0.6, 0.75), max(width - 1, 1), depth - 1)

    top = foot - int(tall * 0.55)
    grow(cx, top + 4, -math.pi / 2 - 0.5, tall * 0.28, 3, 3)
    grow(cx, top + 4, -math.pi / 2 + 0.45, tall * 0.3, 3, 3)
    grow(cx, top + 14, -math.pi / 2 + 1.0, tall * 0.18, 2, 2)
    for _ in range(f):
        art.outline(GREYWOOD[0])
    return art.image()


def shrub(variant: int, fine: int = 1) -> Image.Image:
    """A thicket two or three tiles wide: a few low bundles, a berry or two. `fine` draws
    it that many times the draft's size: leaf by leaf, a light on each berry."""
    r = rng("shrub", variant)
    f = fine
    wide, tall = r.randint(34, 52), r.randint(22, 30)
    W0, H0 = wide + 8, tall + SINK
    art = Art(W0 * f, H0 * f)
    ground = H0 - SINK + 4
    for k in range(4):
        bx = 4 + wide * (0.2 + 0.6 * k / 3) + r.uniform(-3, 3)
        bundle(art, bx * f, (ground - tall * r.uniform(0.35, 0.55)) * f, wide * 0.22 * f, tall * 0.42 * f, OAK, r, ("shrub", variant, k), gaps=False)
    if variant % 2:
        red, lit = hexes("c02a3a", "ff8a8a")
        for _ in range(6):
            x, y = r.randint(6, W0 - 6), r.randint(ground - tall + 6, ground - 4)
            if art.has(x * f, y * f):
                for dx in range(f):
                    for dy in range(f):
                        art.put(x * f + dx, y * f + dy, lit if f > 1 and dx == dy == 0 else red)
    art.a[(ground + 2) * f :, :, 3] = 0
    for _ in range(f):
        art.outline(OAK[0])
    return art.image()


# --- What people built ------------------------------------------------------------


def masonry(art: Art, x0, y0, x1, y1, ramp, r, course: int = 5, dark: bool = False, fine: int = 1):
    """Stone in courses, each stone its own shade, the mortar dark, the top of each stone lit.
    The places are in the draft's pixels; `art` is `fine` times the draft's size, and at
    more than one the same stones have mortar a pixel wide and a pit here and there."""
    f = fine
    shift_k = -1 if dark else 0
    for row, y in enumerate(range(int(y0), int(y1), course)):
        x = int(x0) - (row % 2) * r.randint(3, 6)
        while x < x1:
            length = r.randint(6, 11)
            tone = r.choice([2, 3, 3, 4]) + shift_k
            for yy in range(y * f, min(y + course, int(y1)) * f):
                for xx in range(max(x, int(x0)) * f, min(x + length, int(x1)) * f):
                    edge = yy == (y + course) * f - 1 or xx == (x + length) * f - 1
                    k = 1 if edge else tone + 1 if yy == y * f else tone
                    if f > 1 and not edge and (xx * 11 + yy * 7) % 37 == 0:
                        k -= 1  # a pit in the stone
                    art.put(xx, yy, ramp[max(min(k, len(ramp) - 1), 1)])
            x += length


def roof_tiles(art: Art, pts, ramp, r, rows: int = 4, fine: int = 1):
    """A roof's face between its eaves and its ridge, laid as rows of tiles, lit along each row's
    lower lip. `pts` is a function y -> (x0, x1) of the face at a row, in the draft's pixels;
    at `fine` over one the face is drawn at that size, its seams and lips a pixel wide."""
    f = fine
    for y, (x0, x1) in pts:
        for Y in range(y * f, (y + 1) * f):
            for X in range(int(x0 * f) if f > 1 else int(x0), int(x1 * f) if f > 1 else int(x1)):
                row = (Y // (rows * f)) % 2
                lip = Y % (rows * f) == rows * f - 1
                seam = (X + row * 3 * f) % (6 * f) == 0
                k = 1 if seam else 4 if lip else 3 if (X * 7 + Y) % 9 else 2
                art.put(X, Y, ramp[k])


def window(art: Art, x, y, w, h, glow=None, broken=False, r=None, fine: int = 1):
    """A window: a deep dark opening (or a lit one), a sill of stone, a cross of timber.
    The places are in the draft's pixels; at `fine` over one its cross and its sill are a
    pixel finer and lit along their edge."""
    f = fine
    for Y in range(y * f, (y + h) * f):
        for X in range(x * f, (x + w) * f):
            art.put(X, Y, glow[1] if glow else DARK)
    if glow:
        for Y in range((y + 1) * f, (y + h - 1) * f):
            for s in range(f):
                art.put((x + 1) * f + s, Y, glow[2])
    mid_x, mid_y = x + w // 2, y + h // 2
    for Y in range(y * f, (y + h) * f):
        for s in range(f):
            art.put(mid_x * f + s, Y, TIMBER[3] if f > 1 and s == 0 else TIMBER[2])
    if not broken:
        for X in range(x * f, (x + w) * f):
            for s in range(f):
                art.put(X, mid_y * f + s, TIMBER[3] if f > 1 and s == 0 else TIMBER[2])
    for X in range((x - 1) * f, (x + w + 1) * f):
        for s in range(f):
            art.put(X, (y + h) * f + s, STONEWORK[6] if f > 1 and s == 0 else STONEWORK[5])
            art.put(X, (y + h + 1) * f + s, STONEWORK[2])
    if broken and r:
        for _ in range(3):
            dot(art, x + r.randrange(w), y + r.randrange(h), PLASTER[4], f)


def ivy(art: Art, x0, y0, x1, y1, r, amount: float, fine: int = 1):
    """Ivy over a wall: clumps of leaves that climb from the ground and thin out upward.
    At `fine` over one each clump is a leaf of a few pixels, lit on its top."""
    f = fine
    for _ in range(int((x1 - x0) * (y1 - y0) * amount / 4)):
        x = r.randint(int(x0), int(x1) - 1)
        y = int(y1 - (y1 - y0) * r.random() ** 1.8)
        if art.has(x * f, y * f):
            k = r.choice([2, 3, 3, 4, 5])
            if f == 1:
                art.put(x, y, OAK[k])
                art.put(x + 1, y, OAK[k - 1])
                art.put(x, y - 1, OAK[min(k + 1, 6)])
                continue
            X, Y = x * f, y * f
            for dx, dy, kk in ((0, 0, k), (1, 0, k), (2, 0, k - 1), (3, 1, k - 1), (0, -1, min(k + 1, 6)), (1, -1, min(k + 1, 6)), (1, 1, k - 1), (2, 1, k - 1)):
                art.put(X + dx, Y + dy, OAK[kk])


def plinth(art: Art, x0, x1, top, r, fine: int = 1):
    """The stone the house stands on, down into the ground."""
    masonry(art, x0, top, x1, art.h // fine, STONEWORK, r, course=6, dark=True, fine=fine)


def cottage(variant: int, fine: int = 1) -> Image.Image:
    """A cottage of stone below and timber and plaster above, under a roof of red tiles,
    a chimney at one end. Variant 0 is a ruin: its roof fell in at the east end (the
    rafters stand bare), its plaster is gone in places, its windows are black, ivy has
    it. Variant 1 still has a roof of thatch, someone keeps a lamp in its window. `fine`
    draws it that many times the draft's size: mortar and joints a pixel wide, thatch in
    finer straws."""
    r = rng("cottage", variant)
    f = fine
    ruin = variant == 0
    W0, wall_h = 150, 54
    roof_h = 46
    H0 = wall_h + roof_h + 22 + 3 * TILE
    art = Art(W0 * f, H0 * f)
    floor = H0 - 3 * TILE  # the floor line: the plinth goes down under it into the ground
    x0, x1 = 14, W0 - 14
    plinth(art, x0 - 2, x1 + 2, floor, r, fine=f)
    top = floor - wall_h
    # The ground floor of stone, the upper of timber and plaster.
    masonry(art, x0, floor - 22, x1, floor, STONEWORK, r, fine=f)
    for Y in range(top * f, (floor - 22) * f):
        for X in range(x0 * f, x1 * f):
            x = X // f
            k = 4 if x < x0 + 3 else 3 if (X * 13 + Y * 7) % 23 else 2
            art.put(X, Y, PLASTER[k])
    for x in list(range(x0, x1, 22)) + [x1 - 3]:
        for Y in range(top * f, (floor - 22) * f):
            for DX in range(3 * f):
                art.put(x * f + DX, Y, TIMBER[3 if DX < f and (f == 1 or DX == 0) else 2 if f == 1 or DX < 3 * f - 1 else 1])
    for y in (top, floor - 24):
        for X in range((x0 - 2) * f, (x1 + 2) * f):
            for d, k in enumerate((3, 2, 1)):
                for s in range(f):
                    art.put(X, (y + d) * f + s, TIMBER[k + (1 if f > 1 and d == 0 and s == 0 else 0)])
    # Braces in the panels.
    for x in range(x0 + 3, x1 - 22, 44):
        art.line(x * f, (floor - 26) * f, (x + 18) * f, (top + 3) * f, TIMBER[2], 2 * f)
    # Door and windows.
    door_x = x0 + 52
    for Y in range((floor - 30) * f, floor * f):
        for X in range(door_x * f, (door_x + 14) * f):
            x = X // f
            if ruin:
                art.put(X, Y, DARK)
            elif x < door_x + 5:
                # The door stands ajar: the fire inside lights the gap.
                art.put(X, Y, GLOW[1] if x > door_x + 1 else GLOW[2])
            else:
                art.put(X, Y, TIMBER[2 if (x - door_x) % 4 or (f > 1 and X % f) else 1])
    for X in range((door_x - 2) * f, (door_x + 16) * f):
        for s in range(f):
            art.put(X, (floor - 31) * f + s, STONEWORK[5])
            art.put(X, (floor - 32) * f + s, STONEWORK[3])
    if ruin:
        # The door hangs off one hinge, leaning in the opening.
        art.line((door_x + 2) * f, (floor - 1) * f, (door_x + 9) * f, (floor - 28) * f, TIMBER[3], 4 * f)
    glow = GLOW if not ruin else None
    window(art, x0 + 14, floor - 18, 12, 11, glow=None, broken=ruin, r=r, fine=f)
    window(art, x0 + 86, floor - 18, 12, 11, glow=glow, broken=ruin, r=r, fine=f)
    window(art, x0 + 30, top + 8, 10, 10, glow=None, broken=ruin, r=r, fine=f)
    window(art, x0 + 92, top + 8, 10, 10, glow=None, broken=ruin, r=r, fine=f)
    # The roof: a pitched one from eave to eave, the ridge in the middle.
    ridge_x, ridge_y = W0 // 2, top - roof_h
    face = []
    for y in range(ridge_y, top + 4):
        t = (y - ridge_y) / (top + 4 - ridge_y)
        half = 6 + (W0 / 2 - 4) * t
        face.append((y, (ridge_x - half, ridge_x + half)))
    if ruin:
        # The west half keeps its tiles; the east has fallen, its rafters bare against the sky.
        roof_tiles(art, [(y, (a, min(b, ridge_x + 6 + (y - ridge_y) * 0.2))) for y, (a, b) in face], ROOF, r, fine=f)
        for k in range(5):
            x = ridge_x + 10 + k * 13
            art.line((ridge_x + 2) * f, (ridge_y + 2) * f, min(x + 18, W0 - 4) * f, (top + 2 - k * 2 + (6 if k == 4 else 0)) * f, TIMBER[2 if k % 2 else 3], 2 * f)
        for x in range(ridge_x, W0 - 18, 7):
            dot(art, x, top - 6, TIMBER[1], f)
        # The plaster has come away from the east of the upper floor, showing the dark inside.
        for Y in range((top + 3) * f, (floor - 25) * f):
            y = Y / f
            for X in range((x0 + 104) * f, (x1 - 3) * f):
                x = X / f
                if (x - (x0 + 104)) + (y - top) * 0.6 > 6 + 8 * math.sin((int(y) if f == 1 else y) * 0.3):
                    art.put(X, Y, DARK if (X + Y) % 5 else PLASTER[1])
        # Inside, through the fallen east end: a table on its side, a chair, a broken pot.
        art.rect((x0 + 108) * f, (floor - 12) * f, (x0 + 112) * f, (floor - 1) * f, TIMBER[3])
        art.rect((x0 + 108) * f, (floor - 12) * f, (x0 + 126) * f, (floor - 9) * f, TIMBER[4])
        art.line((x0 + 112) * f, (floor - 9) * f, (x0 + 120) * f, (floor - 1) * f, TIMBER[2], 2 * f)
        art.line((x0 + 98) * f, (floor - 1) * f, (x0 + 102) * f, (floor - 10) * f, TIMBER[3], f)
        art.line((x0 + 102) * f, (floor - 10) * f, (x0 + 106) * f, (floor - 8) * f, TIMBER[3], f)
        art.rect((x0 + 92) * f, (floor - 4) * f, (x0 + 97) * f, (floor - 1) * f, ROOF[2])
        ivy(art, x0, top, x0 + 70, floor, r, 0.55, fine=f)
        ivy(art, x1 - 30, floor - 30, x1, floor, r, 0.4, fine=f)
    else:
        for y, (a, b) in face:
            for Y in range(y * f, (y + 1) * f):
                for X in range(int(a) * f if f == 1 else int(a * f), int(b) * f if f == 1 else int(b * f)):
                    x = X / f
                    straw = (X * 5 + Y * 3) % 7 == 0 if f == 1 else (X * 5 + Y * 3) % 11 == 0
                    k = 5 if (x - a) < 4 else 2 if b - x < 4 else 4 if straw else 3
                    if (y - ridge_y) % 7 == 6 and (f == 1 or Y % f == f - 1):
                        k = max(k - 1, 1)
                    art.put(X, Y, THATCH[k])
        for X in range((ridge_x - 8) * f, (ridge_x + 9) * f):
            for s in range(f):
                art.put(X, ridge_y * f + s, THATCH[2])
    # The chimney, at the west end, through the roof.
    cx = x0 + 18
    masonry(art, cx, ridge_y - 4, cx + 12, top - 10, STONEWORK, r, course=4, fine=f)
    for X in range((cx - 1) * f, (cx + 13) * f):
        for s in range(f):
            art.put(X, (ridge_y - 5) * f + s, STONEWORK[6] if f > 1 and s == 0 else STONEWORK[5])
    for _ in range(f):
        art.outline(STONEWORK[0])
    return art.image()


def cottage_glow(fine: int = 1) -> Image.Image:
    """The light of the kept cottage's door and window by itself, shown unlit over it, so the
    fire in it shows at dusk and at night. `fine`: over the cottage drawn at that size."""
    base = cottage(1, fine)
    art = Art(base.width, base.height)
    a = np.array(base)
    lit = (a[..., 0] == GLOW[1][0]) & (a[..., 1] == GLOW[1][1]) & (a[..., 2] == GLOW[1][2])
    lit |= (a[..., 0] == GLOW[2][0]) & (a[..., 1] == GLOW[2][1]) & (a[..., 2] == GLOW[2][2])
    art.a[lit] = a[lit]
    return art.image()


def chapel(variant: int = 0, fine: int = 1) -> Image.Image:
    """A chapel of stone, roofless: its west gable stands with a tall pointed window and a
    bell-cote on top (the bell still in it), its long wall broken down to the sills, a door
    under a pointed arch; ivy and the sky through all of it. `fine` draws it that many
    times the draft's size: mortar a pixel wide, true pointed heads, a round bell."""
    r = rng("chapel", variant)
    f = fine
    W0 = 170
    H0 = 150 + 3 * TILE
    art = Art(W0 * f, H0 * f)
    floor = H0 - 3 * TILE
    plinth(art, 8, W0 - 8, floor, r, fine=f)
    # The gable end, west.
    gx0, gx1 = 10, 62
    gtop = floor - 104
    masonry(art, gx0, gtop, gx1, floor, STONEWORK, r, fine=f)
    for y in range(gtop - 30, gtop):
        t = (y - (gtop - 30)) / 30
        half = 4 + (gx1 - gx0) / 2 * t
        masonry(art, (gx0 + gx1) / 2 - half, y, (gx0 + gx1) / 2 + half, y + 1, STONEWORK, r, course=5, fine=f)
    # The bell-cote on the gable's top.
    bx = (gx0 + gx1) // 2
    masonry(art, bx - 9, gtop - 52, bx + 9, gtop - 26, STONEWORK, r, fine=f)
    for Y in range((gtop - 48) * f, (gtop - 32) * f):
        y = Y / f
        for X in range((bx - 5) * f, (bx + 5) * f):
            x = X / f + (0.5 / f - 0.5 if f > 1 else 0)
            if (x - bx) ** 2 / 25 + ((y - (gtop - 44)) / 6) ** 2 <= 1 or y > gtop - 44:
                art.put(X, Y, DARK)
    for Y in range((gtop - 44) * f, (gtop - 36) * f):
        y = Y / f
        half = 1 + (int(y) - (gtop - 44)) // 2 if f == 1 else 1 + (y - (gtop - 44)) / 2
        for X in range(int((bx - half) * f), int((bx + half + 1) * f)):
            art.put(X, Y, COPPER[4] if X < bx * f else COPPER[2])
    for Y in range((gtop - 62) * f, (gtop - 52) * f):
        half = (Y / f - (gtop - 62)) * 0.9
        for X in range(int((bx - half) * f), int((bx + half) * f) + (1 if f == 1 else f)):
            art.put(X, Y, STONEWORK[5] if X < bx * f else STONEWORK[3])
    art.line(bx * f, (gtop - 70) * f, bx * f, (gtop - 62) * f, IRON[3], f)
    art.line((bx - 3) * f, (gtop - 67) * f, (bx + 3) * f, (gtop - 67) * f, IRON[3], f)
    # The tall pointed window of the gable, its tracery gone.
    wx = bx
    for Y in range((gtop + 6) * f, (floor - 34) * f):
        y = Y / f
        half = 7 if y > gtop + 16 else 7 * math.sqrt(max(0.0, 1 - ((gtop + 16 - y) / 10) ** 2))
        for X in range(int((wx - half) * f), int((wx + half) * f) + (1 if f == 1 else f)):
            art.put(X, Y, DARK)
    art.line(wx * f, (gtop + 12) * f, wx * f, (floor - 34) * f, STONEWORK[2], 1 if f == 1 else 1.5)
    # The long wall, east of the gable, broken.
    lx0, lx1 = gx1, W0 - 10
    for x in range(lx0, lx1):
        height = 60 - (x - lx0) * 0.35 + 8 * math.sin(x * 0.21) + 5 * math.sin(x * 0.53)
        height = max(height, 10)
        masonry(art, x, floor - int(height), x + 1, floor, STONEWORK, r, fine=f)
    # Its windows (the broken tops of them), and a door under a pointed arch.
    for wx2 in (lx0 + 24, lx0 + 62):
        for Y in range((floor - 44) * f, (floor - 14) * f):
            y = Y / f
            half = 5 if y > floor - 38 else 5 * math.sqrt(max(0.0, 1 - ((floor - 38 - y) / 6) ** 2))
            for X in range(int((wx2 - half) * f), int((wx2 + half) * f) + (1 if f == 1 else f)):
                if art.has(X, Y):
                    art.put(X, Y, DARK)
    dx = lx0 + 88
    for Y in range((floor - 30) * f, floor * f):
        y = Y / f
        half = 7 if y > floor - 22 else 7 * math.sqrt(max(0.0, 1 - ((floor - 22 - y) / 8) ** 2))
        for X in range(int((dx - half) * f), int((dx + half) * f) + (1 if f == 1 else f)):
            if art.has(X, Y):
                art.put(X, Y, DARK)
    ivy(art, gx0, gtop - 10, gx1, floor, r, 0.35, fine=f)
    ivy(art, lx0, floor - 50, lx1, floor, r, 0.5, fine=f)
    for _ in range(f):
        art.outline(STONEWORK[0])
    return art.image()


def well(fine: int = 1) -> Image.Image:
    """A well: a ring of stone, two posts and a little roof, the crank and the rope, the
    bucket on the rim. `fine` draws it that many times the draft's size."""
    r = rng("well")
    f = fine
    W0, H0 = 64, 58 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    masonry(art, 12, floor - 16, W0 - 12, floor + 6, STONEWORK, r, course=4, fine=f)
    for X in range(10 * f, (W0 - 10) * f):
        for s in range(f):
            art.put(X, (floor - 17) * f + s, STONEWORK[6] if f > 1 and s == 0 else STONEWORK[6] if f == 1 else STONEWORK[5])
            art.put(X, (floor - 16) * f + s, STONEWORK[4])
    for px in (16, W0 - 20):
        for Y in range((floor - 44) * f, (floor - 16) * f):
            for DX in range(4 * f):
                art.put(px * f + DX, Y, TIMBER[4 if DX < f else 2 if DX < 4 * f - 1 or f == 1 else 1])
    for Y in range((floor - 56) * f, (floor - 42) * f):
        y = Y / f
        t = (y - (floor - 56)) / 14
        half = 6 + 22 * t
        for X in range(int((W0 / 2 - half) * f), int((W0 / 2 + half) * f)):
            x = X / f
            k = 5 if x < W0 / 2 - half + 3 else 2 if x > W0 / 2 + half - 3 else 3
            course = (int(y) % 3) if f == 1 else (Y % (3 * f))
            art.put(X, Y, ROOF[k] if course else ROOF[1])
    for X in range(16 * f, (W0 - 16) * f):
        for s in range(f):
            art.put(X, (floor - 36) * f + s, TIMBER[3])
            art.put(X, (floor - 35) * f + s, TIMBER[1])
    art.line((W0 - 16) * f, (floor - 36) * f, (W0 - 10) * f, (floor - 30) * f, IRON[3], f)
    for Y in range((floor - 35) * f, (floor - 24) * f):
        art.put(W0 // 2 * f, Y, THATCH[4])
        if f > 1:
            art.put(W0 // 2 * f + 1, Y, THATCH[3])
    for Y in range((floor - 23) * f, (floor - 18) * f):
        y = Y // f
        for X in range((W0 // 2 - 3) * f, (W0 // 2 + 4) * f):
            art.put(X, Y, TIMBER[3] if y != floor - 21 else IRON[3])
    for _ in range(f):
        art.outline(STONEWORK[0])
    return art.image()


def windmill(sails: bool = True, fine: int = 1) -> Image.Image:
    """A windmill on its hill: a tower of stone that narrows upward, a cap of boards, four
    sails of lattice of which two are broken, a door and a small window. Without `sails` the
    tower alone: the world's mill turns its own sails (`sails`, TilesV2Life). `fine` draws
    it that many times the draft's size: a tower that narrows smoothly, boards a pixel apart."""
    r = rng("windmill")
    f = fine
    W0, H0 = 190, 200 + 2 * TILE
    art = Art(W0 * f, H0 * f)
    floor = H0 - 2 * TILE
    cx = W0 // 2
    base_half, top_half, tower_h = 26, 17, 118
    for y in range(floor - tower_h, H0):
        t = min(max((floor - y) / tower_h, 0), 1)
        half = base_half + (top_half - base_half) * t
        masonry(art, cx - half, y, cx + half, y + 1, PLASTER, r, course=6, fine=f)
    for Y in range((floor - tower_h) * f, floor * f):
        y = Y / f
        t = (floor - y) / tower_h
        half = base_half + (top_half - base_half) * t
        if f == 1:
            for x in range(int(cx - half), int(cx - half) + 3):
                art.put(x, Y, PLASTER[5])
            for x in range(int(cx + half) - 5, int(cx + half)):
                art.put(x, Y, PLASTER[1] if x > cx + half - 3 else PLASTER[2])
            continue
        left, right = int((cx - half) * f), int((cx + half) * f)
        for X in range(left, left + 3 * f):
            art.put(X, Y, PLASTER[6] if X == left + 1 and len(PLASTER) > 6 else PLASTER[5])
        for X in range(right - 5 * f, right):
            art.put(X, Y, PLASTER[1] if X > (cx + half - 3) * f else PLASTER[2])
    # Door and window.
    for Y in range((floor - 26) * f, floor * f):
        for X in range((cx - 7) * f, (cx + 6) * f):
            art.put(X, Y, DARK)
    window(art, cx - 4, floor - 74, 8, 10, fine=f)
    # The cap.
    cap_y = floor - tower_h
    for Y in range((cap_y - 22) * f, (cap_y + 2) * f):
        y = Y / f
        t = (y - (cap_y - 22)) / 24
        half = 6 + (top_half + 4) * math.sqrt(t)
        for X in range(int((cx - half) * f) if f > 1 else int(cx - half), int((cx + half) * f) if f > 1 else int(cx + half)):
            x = X / f
            k = 4 if x < cx - half * 0.4 else 2 if x > cx + half * 0.5 else 3
            board = (X % 4) if f == 1 else (X % (4 * f))
            art.put(X, Y, TIMBER[k] if board else TIMBER[1])
    # The sails, from the hub: lattice arms, two of them broken short.
    hub = (cx + 2, cap_y - 6)
    w1 = 1 if f == 1 else 1.5
    for k, (angle, length, broken) in enumerate(((-0.95, 82, False), (0.62, 80, True), (2.2, 82, False), (3.76, 46, True)) if sails else ()):
        ex, ey = hub[0] + math.cos(angle) * length, hub[1] + math.sin(angle) * length
        art.line(hub[0] * f, hub[1] * f, ex * f, ey * f, TIMBER[2], 3 * f)
        nx, ny = -math.sin(angle), math.cos(angle)
        start = 18
        for d in range(start, int(length) - 2, 7):
            px, py = hub[0] + math.cos(angle) * d, hub[1] + math.sin(angle) * d
            art.line(px * f, py * f, (px + nx * 14) * f, (py + ny * 14) * f, TIMBER[3], w1)
        for side in (0.0, 14.0):
            art.line(
                (hub[0] + math.cos(angle) * start + nx * side) * f,
                (hub[1] + math.sin(angle) * start + ny * side) * f,
                (ex + nx * side) * f,
                (ey + ny * side) * f,
                TIMBER[3 if side else 4],
                w1,
            )
        if broken:
            art.line(ex * f, ey * f, (ex + nx * 9 + math.cos(angle + 1.2) * 8) * f, (ey + ny * 9 + math.sin(angle + 1.2) * 8) * f, TIMBER[2], 2 * f)
    for DX in range(-3 * f, 3 * f + 1):
        for DY in range(-3 * f, 3 * f + 1):
            dx, dy = DX / f, DY / f
            if dx * dx + dy * dy <= 9:
                art.put(hub[0] * f + DX, hub[1] * f + DY, IRON[2 if dx + dy > 0 else 4])
    ivy(art, cx - base_half, floor - 50, cx - 6, floor, r, 0.4, fine=f)
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def fence(variant: int, fine: int = 1) -> Image.Image:
    """Three tiles of a picket fence: whole, with a gap and a picket fallen, or leaning.
    `fine` draws it that many times the draft's size: each picket and rail lit along its
    edge, a pointed head on each picket."""
    r = rng("fence", variant)
    f = fine
    W0, H0 = 3 * TILE, 22 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    lean = [0.0, 0.0, 0.18][variant % 3]
    for x in range(1, W0 - 1, 6):
        if variant % 3 == 1 and 18 <= x < 30:
            continue
        tall = r.randint(16, 19)
        dx = lean * tall
        art.line(x * f, (floor + 4) * f, (x + dx) * f, (floor - tall) * f, TIMBER[3], 3 * f)
        if f > 1:
            art.line(x * f - 2, (floor + 4) * f, (x + dx) * f - 2, (floor - tall) * f + 2, TIMBER[4], 1)
            for d in range(-2, 3):  # its head, to a point
                art.put((x + dx) * f + d, (floor - tall) * f - 1 - (2 - abs(d)), TIMBER[4] if d <= 0 else TIMBER[3])
        else:
            art.put(int(x + dx), floor - tall - 1, TIMBER[4])
    for y in (floor - 6, floor - 14):
        art.line(0, y * f, W0 * f - 1, (y - (lean * 6 if lean else 0)) * f, TIMBER[2], 2 * f)
        if f > 1:
            art.line(0, y * f - 1, W0 * f - 1, (y - (lean * 6 if lean else 0)) * f - 1, TIMBER[3], 1)
    if variant % 3 == 1:
        art.line(18 * f, (floor - 1) * f, 31 * f, (floor - 3) * f, TIMBER[2], 2 * f)
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def cart(fine: int = 1) -> Image.Image:
    """A farm cart left where it stopped: a box of boards on one wheel, the other wheel off and
    leaning against it, its shafts down in the grass. `fine` draws it that many times the
    draft's size: true wheels, boards a pixel apart."""
    f = fine
    W0, H0 = 72, 34 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for Y in range((floor - 26) * f, (floor - 12) * f):
        y = Y // f
        for X in range(10 * f, 54 * f):
            x = X // f
            seam = (y - floor) % 5 == 0 if f == 1 else (Y - floor * f) % (5 * f) == 0
            art.put(X, Y, TIMBER[4 if (Y == (floor - 26) * f) else 1 if seam else 3 if x > 12 else 4])
    art.line(54 * f, (floor - 14) * f, 70 * f, (floor - 1) * f, TIMBER[2], 2 * f)

    def wheel(cx, cy, rad):
        if f == 1:
            for a in range(0, 360, 4):
                art.put(cx + math.cos(math.radians(a)) * rad, cy + math.sin(math.radians(a)) * rad, TIMBER[2])
                art.put(cx + math.cos(math.radians(a)) * (rad - 1), cy + math.sin(math.radians(a)) * (rad - 1), TIMBER[3])
        else:
            for DY in range(-(rad + 1) * f, (rad + 1) * f):
                for DX in range(-(rad + 1) * f, (rad + 1) * f):
                    d = math.hypot((DX + 0.5) / f, (DY + 0.5) / f)
                    if rad - 1.5 < d <= rad + 0.5:
                        art.put(cx * f + DX, cy * f + DY, TIMBER[2] if d > rad - 0.5 else TIMBER[3])
        for a in range(0, 180, 45):
            art.line((cx - math.cos(math.radians(a)) * rad) * f, (cy - math.sin(math.radians(a)) * rad) * f, (cx + math.cos(math.radians(a)) * rad) * f, (cy + math.sin(math.radians(a)) * rad) * f, TIMBER[2], 1 if f == 1 else 1.5)
        dot(art, cx, cy, IRON[3], f)

    wheel(22, floor - 10, 10)
    wheel(46, floor - 9, 9)
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def haystack(variant: int, fine: int = 1) -> Image.Image:
    """A stack of old hay gone grey-gold, a pole through its top. `fine` draws it that
    many times the draft's size: a round profile, the straw in finer strands."""
    r = rng("hay", variant)
    f = fine
    W0, H0 = 46, 40 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for Y in range((floor - 34) * f, (floor + 4) * f):
        y = Y / f
        t = (y - (floor - 34)) / 38
        half = 6 + 17 * math.sin(min(t * 1.4, 1) * math.pi / 2)
        for X in range(int((W0 / 2 - half) * f), int((W0 / 2 + half) * f)):
            x = X / f
            off = (x - W0 / 2) / half
            k = 5 if off < -0.5 else 4 if off < 0 else 3 if off < 0.5 else 2
            if (X * 3 + Y * 5) % 11 == 0:
                k = max(k - 1, 1)
            if f > 1 and (X * 7 + Y * 2) % 23 == 0:
                k = min(k + 1, len(THATCH) - 1)  # a strand that catches the light
            art.put(X, Y, THATCH[k])
    art.line(W0 // 2 * f, (floor - 42) * f, W0 // 2 * f, (floor - 30) * f, TIMBER[3], 2 * f)
    for _ in range(f):
        art.outline(THATCH[0])
    return art.image()


def scarecrow(fine: int = 1) -> Image.Image:
    """A scarecrow in a field nobody sows: a cross of poles, a sack head, a coat in rags.
    `fine` draws it that many times the draft's size: a round sack head, a hat's brim."""
    f = fine
    W0, H0 = 34, 52 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    art.line(17 * f, (floor + 4) * f, 17 * f, (floor - 44) * f, TIMBER[3], 2 * f)
    art.line(3 * f, (floor - 34) * f, 31 * f, (floor - 36) * f, TIMBER[3], 2 * f)
    for Y in range((floor - 34) * f, (floor - 14) * f):
        for X in range(10 * f, 25 * f):
            patch_ = (X + Y) % 7 if f == 1 else (X // f + Y // f) % 7 and (X * 3 + Y) % 11
            art.put(X, Y, ROOF[2] if patch_ else ROOF[1])
    for X in range(3 * f, 10 * f):
        for s in range(f):
            art.put(X, (floor - 33) * f + s, ROOF[3])
            art.put(X, (floor - 32) * f + s, ROOF[2])
    for X in range(25 * f, 31 * f):
        for s in range(f):
            art.put(X, (floor - 35) * f + s, ROOF[3])
    for Y in range((floor - 46) * f, (floor - 35) * f):
        y = Y / f
        for X in range(12 * f, 22 * f):
            x = X / f + (0.5 / f - 0.5 if f > 1 else 0)
            if (x - 17) ** 2 / 25 + ((y - (floor - 40)) / 5.5) ** 2 <= 1:
                art.put(X, Y, THATCH[4] if x < 17 else THATCH[3])
    for Y in range((floor - 50) * f, (floor - 45) * f):
        y = Y // f
        for X in range(9 * f, 26 * f):
            x = X // f
            if y > floor - 47 or 13 <= x <= 21:
                art.put(X, Y, TIMBER[1])
    dot(art, 15, floor - 41, DARK, f)
    dot(art, 19, floor - 41, DARK, f)
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def grave(variant: int, fine: int = 1) -> Image.Image:
    """A grave: a round-topped headstone, a cross, a leaning slab or a low ledger, with
    lichen on it. `fine` draws it that many times the draft's size: a true round head,
    its cut letters a pixel wide."""
    r = rng("tomb", variant)
    f = fine
    W0, H0 = 24, 30 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    kind = variant % 4
    if kind == 0:
        for Y in range((floor - 22) * f, (floor + 3) * f):
            y = Y / f
            for X in range(5 * f, 19 * f):
                x = X / f
                cxp = x + (0.5 / f - 0.5 if f > 1 else 0)
                if y > floor - 16 or (cxp - 12) ** 2 + ((y - (floor - 16)) * 1.2) ** 2 <= 49:
                    art.put(X, Y, STONEWORK[5 if x < 8 else 2 if x > 16 else 4])
        for x in (9, 10, 11, 12, 13, 14):
            art.put(x * f, (floor - 12) * f, STONEWORK[2]) if f > 1 else art.put(x, floor - 12, STONEWORK[2])
        for x in (10, 13):
            if f == 1:
                art.put(x, floor - 9, STONEWORK[2])
            else:
                art.put(x * f, (floor - 9) * f, STONEWORK[2])
                art.put(x * f + 1, (floor - 9) * f, STONEWORK[2])
        if f > 1:
            for x in range(9 * f, 15 * f):
                art.put(x, (floor - 12) * f, STONEWORK[2])
                art.put(x, (floor - 12) * f + 1, STONEWORK[5])  # the cut's lit lower edge
    elif kind == 1:
        art.rect(10 * f, (floor - 26) * f, 14 * f, (floor + 3) * f, STONEWORK[4])
        art.rect(5 * f, (floor - 20) * f, 19 * f, (floor - 16) * f, STONEWORK[4])
        art.rect(10 * f, (floor - 26) * f, 11 * f, (floor + 3) * f, STONEWORK[5])
        art.rect(13 * f, (floor - 20) * f, 14 * f, (floor + 3) * f, STONEWORK[2])
        if f > 1:
            art.rect(5 * f, (floor - 20) * f, 19 * f, (floor - 20) * f + 1, STONEWORK[5])
            art.rect(5 * f, (floor - 16) * f - 1, 19 * f, (floor - 16) * f, STONEWORK[2])
    elif kind == 2:
        for I in range(18 * f):
            i = I / f
            Y = (floor - 2) * f + (f - 1) - I
            x0 = 6 + (int(i * 0.5) if f == 1 else i * 0.5)
            for X in range(int(x0 * f), int((x0 + 11) * f)):
                x = X / f
                art.put(X, Y, STONEWORK[4 if x < x0 + 3 else 2 if x > x0 + 8 else 3])
    else:
        art.rect(3 * f, (floor - 8) * f, 21 * f, (floor + 3) * f, STONEWORK[3])
        art.rect(3 * f, (floor - 8) * f, 21 * f, (floor - 6) * f, STONEWORK[5])
        if f > 1:
            art.rect(3 * f, (floor - 8) * f, 21 * f, (floor - 8) * f + 1, STONEWORK[6] if len(STONEWORK) > 6 else STONEWORK[5])
    for _ in range(5):
        x, y = r.randint(4, 19), r.randint(floor - 20, floor)
        if art.has(x * f, y * f):
            lichen = hexes("7a8a4a")[0]
            if f == 1:
                art.put(x, y, lichen)
            else:
                art.put(x * f, y * f, lichen)
                art.put(x * f + 1, y * f, hexes("9aaa5a")[0])
                art.put(x * f, y * f + 1, lichen)
    for _ in range(f):
        art.outline(STONEWORK[0])
    return art.image()


def gateway(fine: int = 1) -> Image.Image:
    """The Gate on the surface: a great arch of dark stone over the mouth of the way down,
    pointed, its voussoirs picked out, a band of letters nobody reads over the arch, two
    buttresses, a crack, and the warm glow of what is below inside it. `fine` draws it
    that many times the draft's size: mortar a pixel wide, a true pointed arch, a finer
    glow, letters cut a pixel deep."""
    r = rng("gateway")
    f = fine
    W0, H0 = 18 * TILE, 13 * TILE + 3 * TILE
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    floor = H0 - 3 * TILE
    dark = ramp_of("140f14 241c24 33282f 433640 54464e 66575c 7a6a6c")
    masonry(art, 0, floor - 150, W0, H0, dark, r, course=7, fine=f)
    # Cut the outline: buttresses that step in, a gable over the arch.
    for Y in range(0, H):
        y = Y / f
        for X in range(W):
            x = X / f
            outside = False
            if y < floor - 150:
                outside = True
            elif x < 14 or x >= W0 - 14:
                outside = y < floor - 96 + (0 if x < 7 or x >= W0 - 7 else -24)
            else:
                peak = floor - 150 + abs(x + (0.5 / f - 0.5 if f > 1 else 0) - W0 / 2) * 0.32 - 16
                outside = y < peak
            if outside:
                art.a[Y, X, 3] = 0
    for Y in range((floor - 166) * f, (floor - 150) * f):
        y = Y / f
        for X in range(14 * f, (W0 - 14) * f):
            x = X / f
            peak = floor - 150 + abs(x + (0.5 / f - 0.5 if f > 1 else 0) - W0 / 2) * 0.32 - 16
            if y >= peak:
                art.put(X, Y, dark[3] if x < W0 / 2 else dark[2])
    # The opening: a pointed arch, black inside, a red glow at its foot.
    cx, half, spring = W0 // 2, 44, floor - 70
    for Y in range((floor - 124) * f, floor * f):
        y = Y / f
        if y >= spring:
            span = half
        else:
            # two arcs that meet at the top
            rise = (spring - y) / (spring - (floor - 124))
            span = half * math.sqrt(max(0.0, 1 - rise**1.6))
        for X in range(int((cx - span) * f), int((cx + span) * f) + (1 if f == 1 else f)):
            ember_glow(art, X, Y, max(0.0, (y - (floor - 56)) / 56))
        # The voussoirs round the opening.
        for side in (-1, 1):
            for W_ in range(6 * f):
                w_ = W_ / f
                X = int((cx + side * (span + 1 + w_)) * f) if f > 1 else int(cx + side * (span + 1 + W_))
                if art.has(X, Y):
                    k = 5 if ((int(y) // 6) + int(w_) // 3) % 2 else 4
                    if f > 1 and int(y) % 6 == 5 and Y % f == f - 1:
                        k -= 1  # the joint between two voussoirs
                    art.put(X, Y, dark[k if side < 0 else k - 2])
    # Nothing under its floor line: where the ground was dug for the way down the stone
    # would stand in front of the tunnel as steps; the piers go behind the ground anyway.
    art.a[(floor + 2) * f :, :, 3] = 0
    # A band of letters over the arch.
    for X in range((cx - 52) * f, (cx + 53) * f):
        x = X // f
        for s in range(f):
            art.put(X, (floor - 136) * f + s, dark[6])
            art.put(X, (floor - 129) * f + s, dark[1])
        if x % 5 and (x * 7) % 11 > 3:
            if f == 1:
                art.put(X, floor - 132 - (x % 2), dark[1])
            else:
                art.put(X, (floor - 132 - (x % 2)) * f, dark[1])
                art.put(X, (floor - 132 - (x % 2)) * f + 1, dark[5])  # the letter's lit lower edge
    # A crack down the east pier.
    x, y = cx + 64, floor - 120
    while y < floor:
        if f == 1:
            art.put(x, y, DARK)
            art.put(x + 1, y, dark[4])
        else:
            for s in range(f):
                art.put(x * f, y * f + s, DARK)
                art.put(x * f + 1, y * f + s, dark[4])
        x += r.choice([-1, 0, 0, 1])
        y += 1
    ivy(art, 0, floor - 90, 40, floor, r, 0.25, fine=f)
    for _ in range(f):
        art.outline(dark[0])
    return art.image()


def ember_glow(art: Art, x: int, y: int, t: float):
    """Paints the dark of an opening at (x, y), warmer toward its foot by t (0 .. 1), in an
    ordered dither between three colours."""
    reds = hexes("120a0c", "2a0e0a", "4a160c")
    level = t * 2
    i = min(int(level), 1)
    frac = level - i
    colour = reds[i + 1] if frac * 16 > BAYER_4[y % 4][x % 4] + 0.5 else reds[i]
    art.put(x, y, colour)


def gate_arch(fine: int = 1) -> Image.Image:
    """The Gate itself, in its hall under the ground: two piers of dark red stone with a
    figure cut in a niche in each, a round arch of voussoirs between them with a keystone
    carved as a face, a band of letters over it; the dark inside, warm at its foot where
    the fire of what is below shows. `fine` draws it that many times the draft's size."""
    r = rng("gate arch")
    f = fine
    W0, H0 = 15 * TILE, 12 * TILE + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    stone = ramp_of("0e0606 1c0c0a 28110e 341713 42201a 50281f 60302a")
    pier = 3 * TILE
    top = floor - 11 * TILE
    masonry(art, 0, top + 18, pier, H0, stone, r, course=7, fine=f)
    masonry(art, W0 - pier, top + 18, W0, H0, stone, r, course=7, fine=f)
    masonry(art, 0, top, W0, top + 30, stone, r, course=6, fine=f)
    # The opening: a round arch springing from the piers.
    cx, half = W0 / 2, (W0 - 2 * pier) / 2
    spring = top + 30 + half * 0.6
    for Y in range(int(top + 26) * f, floor * f):
        y = Y / f
        if y >= spring:
            span = half
        else:
            k = (spring - y) / (half * 0.9)
            span = half * math.sqrt(max(0.0, 1 - k * k))
        for X in range(int((cx - span) * f), int((cx + span) * f) + (1 if f == 1 else f)):
            ember_glow(art, X, Y, max(0.0, (y - (floor - 48)) / 48))
        # Voussoirs round it.
        if y < spring + 4:
            for side in (-1, 1):
                for W_ in range(f, 8 * f):
                    w_ = W_ / f
                    X = int((cx + side * (span + w_)) * f) if f > 1 else int(cx + side * (span + W_))
                    if 0 <= X < W0 * f:
                        k = 6 if ((int(y) // 5) + (int(w_) // 4)) % 2 else 5
                        art.put(X, Y, stone[k if side < 0 else k - 2])
    # The keystone, a face: two dark eyes and a mouth.
    ky = int(top + 24)
    art.rect((cx - 7) * f, (ky - 2) * f, (cx + 7) * f, (ky + 12) * f, stone[5])
    art.rect((cx - 7) * f, (ky - 2) * f, (cx - 5) * f, (ky + 12) * f, stone[6])
    art.rect((cx + 5) * f, (ky - 2) * f, (cx + 7) * f, (ky + 12) * f, stone[2])
    for ex in (cx - 4, cx + 2):
        art.rect(ex * f, (ky + 3) * f, (ex + 2) * f, (ky + 5) * f, hexes("ff7a2a")[0])
        if f > 1:
            art.put(ex * f, (ky + 3) * f, hexes("ffc080")[0])
    art.rect((cx - 3) * f, (ky + 8) * f, (cx + 4) * f, (ky + 9) * f, DARK)
    # The band of letters.
    for X in range(8 * f, (W0 - 8) * f):
        x = X // f
        for s in range(f):
            art.put(X, (top + 6) * f + s, stone[6])
            art.put(X, (top + 13) * f + s, stone[1])
        if x % 4 and (x * 7) % 9 > 2:
            if f == 1:
                art.put(X, top + 9 + (x % 2), stone[1])
            else:
                art.put(X, (top + 9 + (x % 2)) * f, stone[1])
                art.put(X, (top + 9 + (x % 2)) * f + 1, stone[4])
    # A figure in a niche in each pier: hooded, its hands together.
    for nx in (pier // 2, W0 - pier // 2):
        ny = floor - 78
        for Y in range(ny * f, (ny + 52) * f):
            y = Y / f
            for X in range((nx - 9) * f, (nx + 10) * f):
                x = X / f + (0.5 / f - 0.5 if f > 1 else 0)
                if y > ny + 8 or (x - nx) ** 2 + ((y - ny - 8) * 1.1) ** 2 <= 81:
                    art.put(X, Y, stone[1])
        for Y in range((ny + 6) * f, (ny + 50) * f):
            y = Y / f
            t = (y - ny - 6) / 44
            hw = 4 + 4 * t
            for X in range(int((nx - hw) * f), int((nx + hw) * f) + (1 if f == 1 else f)):
                x = X / f
                art.put(X, Y, stone[5] if x < nx - hw * 0.3 else stone[3] if x > nx + hw * 0.4 else stone[4])
        art.rect((nx - 3) * f, (ny + 9) * f, (nx + 4) * f, (ny + 14) * f, stone[1])
    for _ in range(f):
        art.outline(stone[0])
    return art.image()


def pithead(fine: int = 1) -> Image.Image:
    """The head of the old shaft: a frame of timber two legs wide over the hole, braced,
    with the sheave wheel at its top and the rope down into the dark; a hut for the winch.
    `fine` draws it that many times the draft's size: a true wheel with its spokes, a rope
    that is laid."""
    r = rng("pithead")
    f = fine
    W0, H0 = 7 * TILE, 9 * TILE + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    left, right = 10, W0 - 14
    top = floor - 118
    for x0, x1 in ((left, left + 14), (right, right - 14)):
        art.line(x0 * f, (floor + 6) * f, ((left + right) / 2 + (x1 - x0) * 0.1) * f, (top + 10) * f, TIMBER[3], 4 * f)
    for xa in (left + 4, right - 4):
        art.line(xa * f, (floor + 6) * f, xa * f, (top + 26) * f, TIMBER[2], 4 * f)
    for y in range(top + 30, floor, 26):
        art.line((left + 4) * f, y * f, (right - 4) * f, y * f, TIMBER[2], 2 * f)
        art.line((left + 4) * f, y * f, (right - 4) * f, (y + 24) * f, TIMBER[1], f)
    # The head and the wheel.
    art.rect((left - 2) * f, (top + 20) * f, (right + 2) * f, (top + 26) * f, TIMBER[3])
    art.rect((left - 2) * f, (top + 20) * f, (right + 2) * f, (top + 21) * f, TIMBER[5])
    wcx, wcy, rad = (left + right) // 2, top + 10, 15
    if f == 1:
        for a in range(0, 360, 2):
            for k, (rr, colour) in enumerate(((rad, IRON[2]), (rad - 1, IRON[4]), (rad - 2, IRON[3]))):
                art.put(wcx + math.cos(math.radians(a)) * rr, wcy + math.sin(math.radians(a)) * rr, colour)
    else:
        for DY in range(-(rad + 1) * f, (rad + 1) * f):
            for DX in range(-(rad + 1) * f, (rad + 1) * f):
                d = math.hypot((DX + 0.5) / f, (DY + 0.5) / f)
                if rad - 2.5 < d <= rad + 0.5:
                    art.put(wcx * f + DX, wcy * f + DY, IRON[2] if d > rad - 0.3 else IRON[4] if d > rad - 1.3 and DX + DY < 0 else IRON[3])
    for a in range(0, 180, 30):
        art.line((wcx - math.cos(math.radians(a)) * rad) * f, (wcy - math.sin(math.radians(a)) * rad) * f, (wcx + math.cos(math.radians(a)) * rad) * f, (wcy + math.sin(math.radians(a)) * rad) * f, IRON[3], 1 if f == 1 else 1.5)
    art.rect((wcx - 2) * f, (wcy - 2) * f, (wcx + 3) * f, (wcy + 3) * f, IRON[1])
    # The rope, down the middle into the shaft.
    for Y in range(wcy * f, (floor + SINK) * f):
        y = Y // f
        if f == 1:
            art.put(wcx + rad - 1, Y, THATCH[3] if y % 4 else THATCH[2])
        else:
            lay = (Y // 2) % 3
            art.put((wcx + rad - 1) * f, Y, THATCH[3] if lay else THATCH[2])
            art.put((wcx + rad - 1) * f + 1, Y, THATCH[2] if lay else THATCH[3])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def winch_hut(fine: int = 1) -> Image.Image:
    """The hut the winch stood in, of boards, its roof fallen half in. `fine` draws it that
    many times the draft's size: boards a pixel apart, the roof's courses."""
    r = rng("hut")
    f = fine
    W0, H0 = 4 * TILE, 3 * TILE + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for Y in range((floor - 30) * f, (floor + 4) * f):
        for X in range(4 * f, (W0 - 4) * f):
            x = X // f
            gap = (x % 5 == 0) if f == 1 else (X % (5 * f) == 0)
            art.put(X, Y, TIMBER[1] if gap else TIMBER[3 if (x // 5) % 2 else 2])
    for Y in range((floor - 22) * f, (floor - 12) * f):
        for X in range(12 * f, 22 * f):
            art.put(X, Y, DARK)
    for Y in range((floor - 42) * f, (floor - 29) * f):
        y = Y / f
        t = (y - (floor - 42)) / 13
        for X in range(int((W0 / 2 - 6 - 26 * t) * f), int((W0 / 2 + 6 + 26 * t) * f)):
            x = X / f
            if x > W0 / 2 + 4 and ((X + Y) % 3 == 0 if f == 1 else (X // f + Y // f) % 3 == 0):
                continue
            k = 3 if x < W0 / 2 else 2
            if f > 1 and Y % (3 * f) == 0:
                k -= 1
            art.put(X, Y, ROOF[k])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def colonnade_ruin(fine: int = 1) -> Image.Image:
    """Behind the marble stubs by the start: what stood there, a temple of the old world;
    four broken shafts of fluted marble of different heights, a piece of the architrave
    still across two of them, a drum fallen in the grass. Their feet go on down into the
    ground, wherever it rises under them. `fine` draws it that many times the draft's
    size: each flute with a lit edge, a ragged break."""
    r = rng("colonnade")
    f = fine
    W0, H0 = 26 * TILE, 7 * TILE + 3 * TILE
    art = Art(W0 * f, H0 * f)
    floor = H0 - 3 * TILE
    marble = ramp_of("4a4852 7c7a86 98969f b4b2bb ccc9d1 e2dfe6 f2f0f4")
    shafts = ((24, 88), (112, 46), (288, 100), (384, 64))
    for cx, tall in shafts:
        for Y in range((floor - tall) * f, H0 * f):
            for X in range((cx - 8) * f, (cx + 8) * f):
                x = X // f
                flute = (x - cx + 8) % 4
                k = 5 if x < cx - 4 else 2 if x > cx + 4 else 4 if flute == 0 else 3
                if f > 1 and flute == 1 and X % f == 0 and cx - 4 <= x <= cx + 4:
                    k = 4  # the lit edge of a flute
                art.put(X, Y, marble[k])
        for X in range((cx - 11) * f, (cx + 11) * f):
            for Y in range((floor - 4) * f, (floor + 2) * f):
                art.put(X, Y, marble[5 if Y < (floor - 4) * f + (f if f == 1 else 1) else 3])
        # The broken top, ragged.
        for x in range(cx - 8, cx + 8):
            gone = r.randint(0, 7)
            for Y in range((floor - tall - gone) * f, (floor - tall) * f):
                for s in range(f):
                    art.put(x * f + s, Y, marble[4 if x < cx else 3])
            if f > 1 and gone:
                art.put(x * f + (x % 2), (floor - tall - gone) * f - 1, marble[4 if x < cx else 3])  # a chip of the break
    # The architrave still across the two tallest... that stand apart: a piece of it on the
    # tallest, broken off over the gap.
    cx, tall = shafts[2]
    for X in range((cx - 12) * f, (cx + 12) * f):
        for Y in range((floor - tall - 6) * f, (floor - tall) * f):
            art.put(X, Y, marble[5 if Y < (floor - tall - 6) * f + (1 if f > 1 else 1) else 3])
    for X in range((cx - 34) * f, (cx + 14) * f):
        x = X / f
        for Y in range((floor - tall - 17) * f, (floor - tall - 6) * f):
            y = Y / f
            k = 4 if y < floor - tall - 14 else 3 if (int(x) // 8) % 2 else 2
            if x < cx - 26 and y < floor - tall - 17 + (cx - 26 - x):
                continue
            art.put(X, Y, marble[k])
    # A drum fallen in the grass.
    for Y in range((floor - 15) * f, (floor + 2) * f):
        y = Y / f
        for X in range(186 * f, 214 * f):
            art.put(X, Y, marble[5 if y < floor - 11 else 3 if y < floor - 3 else 2])
    for Y in range((floor - 15) * f, (floor + 2) * f):
        for s in range(f):
            art.put(213 * f + s, Y, marble[1])
            art.put(212 * f + s, Y, marble[2])
    for _ in range(f):
        art.outline(marble[0])
    return art.image()


# --- The pond -----------------------------------------------------------------------


def reeds(variant: int, sway: int = 0, fine: int = 1) -> Image.Image:
    """A clump of reeds at the water's edge: blades of different heights leaning a little,
    two or three bulrush heads. `sway` bends their tops that many px with the wind (the
    frames TilesV2Life turns through). `fine` draws it that many times the draft's size:
    blades a pixel wide that bend smoothly, round bulrush heads."""
    r = rng("reeds", variant)
    f = fine
    W0, H0 = 2 * TILE, 30 + SINK
    art = Art(W0 * f, H0 * f)
    floor = (H0 - SINK) * f
    for i in range(r.randint(7, 10)):
        x = r.randint(2, W0 - 3)
        tall = r.randint(12, 28)
        lean = r.choice([-1, 0, 0, 1])
        for HH in range(-4 * f, tall * f):
            h = HH / f
            bend = (lean * 3 + sway) * (max(h, 0) / tall) ** 2
            dx = int(round(bend)) if f == 1 else round(bend * f)
            colour = WILLOW[2] if h < tall * 0.4 else WILLOW[3] if i % 2 else WILLOW[4]
            art.put(x * f + dx, floor - HH, colour)
            if f > 1 and h < tall * 0.6:
                art.put(x * f + dx + 1, floor - HH, WILLOW[2])
        if i % 3 == 0:
            for HH in range((tall - 6) * f, (tall - 1) * f):
                h = HH / f
                bend = (lean * 3 + sway) * (h / tall) ** 2
                dx = int(round(bend)) if f == 1 else round(bend * f)
                if f == 1:
                    art.put(x + dx, floor - HH, BARK[2])
                    art.put(x + dx + 1, floor - HH, BARK[1])
                    continue
                round_ = 1 if HH in ((tall - 6) * f, (tall - 1) * f - 1) else 0  # the head's rounded ends
                for d in range(-1 + round_, 3 - round_):
                    art.put(x * f + dx + d, floor - HH, BARK[3] if d < 0 else BARK[2] if d < 2 else BARK[1])
    return art.image()


def lilies(variant: int, fine: int = 1) -> Image.Image:
    """Water lilies on the pond: flat round pads, each with its cleft, and on one a flower.
    `fine` draws them that many times the draft's size: a vein on each pad, a flower of
    petals round a gold heart."""
    r = rng("lilies", variant)
    f = fine
    W0, H0 = 3 * TILE, TILE
    art = Art(W0 * f, H0 * f)
    y0 = 3  # the water stands 3 px under the top of its cell
    x = r.randint(2, 6)
    while x < W0 - 8:
        wide = r.randint(5, 8)
        for dx in range(wide):
            for d, colour in ((-1, OAK[5] if dx < wide - 3 else OAK[4]), (0, OAK[4] if dx < wide - 2 else OAK[3]), (1, OAK[2])):
                dot(art, x + dx, y0 + d, colour, f, OAK[6] if f > 1 and d == -1 and dx < wide - 4 and dx > 0 else None)
        dot(art, x + wide // 2, y0 - 1, OAK[2], f)
        dot(art, x + wide // 2, y0, OAK[1], f)
        x += wide + r.randint(3, 9)
    if variant % 2 == 0:
        for dx, dy, c in ((10, -2, "f6e8f0"), (11, -3, "ffffff"), (12, -2, "f0b8d0"), (11, -1, "f6d23a")):
            dot(art, dx, y0 + dy, hexes(c)[0], f)
        if f > 1:
            art.put(11 * f, (y0 - 2) * f, hexes("f6d23a")[0])  # its gold heart
            art.put(11 * f + 1, (y0 - 2) * f, hexes("f6d23a")[0])
    return art.image()


def jetty(fine: int = 1) -> Image.Image:
    """A landing stage of boards out over the water on two piles, a board missing. `fine`
    draws it that many times the draft's size: the boards' ends, round piles."""
    f = fine
    W0, H0 = 4 * TILE, 2 * TILE + SINK
    art = Art(W0 * f, H0 * f)
    deck = H0 - SINK - 10
    for X in range(0, (W0 - 4) * f):
        x = X // f
        if 30 <= x < 36:
            continue
        for s in range(f):
            art.put(X, deck * f + s, TIMBER[5])
            art.put(X, (deck + 1) * f + s, TIMBER[3] if (x % 8 if f == 1 else X % (8 * f)) else TIMBER[1])
            art.put(X, (deck + 2) * f + s, TIMBER[2])
    for px in (20, W0 - 10):
        for Y in range((deck + 3) * f, H0 * f):
            for d, k in enumerate((3, 2, 1)):
                for s in range(f):
                    art.put((px + d) * f + s, Y, TIMBER[k + (1 if f > 1 and d == 0 and s == 0 else 0)])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


# --- The old mine -------------------------------------------------------------------

POST_HEIGHTS = range(3, 13)  # the tunnels' heights (cells of air) a set of timber is drawn for


def mine_post(rows: int, fine: int = 1) -> Image.Image:
    """A set of mine timber for a tunnel `rows` cells high: a post on a sill, a cap that
    carries the roof, two knee braces under the cap. It reaches into the roof and the floor
    a little, behind the ground. `fine` draws it that many times the draft's size: the
    timber's grain a pixel wide, a nail head on each brace."""
    f = fine
    W0 = 3 * TILE
    H0 = rows * TILE + 4 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    cx = W0 // 2
    for Y in range(4 * f, (floor + 4) * f):
        for X in range((cx - 3) * f, (cx + 3) * f):
            x = X // f
            k = 4 if x == cx - 3 else 2 if x >= cx + 1 else 3
            if f > 1 and X == (cx - 3) * f + 1:
                k = 3  # the post's lit edge is a pixel, then its face
            if (Y * 3 + X) % (17 if f == 1 else 23) == 0:
                k = 1
            art.put(X, Y, TIMBER[k])
    for Y in range(0, 7 * f):
        y = Y // f
        for X in range(4 * f, (W0 - 4) * f):
            art.put(X, Y, TIMBER[4 if y == 4 else 1 if y == 6 else 3])
    for side in (-1, 1):
        art.line((cx + side * 3) * f, 18 * f, (cx + side * 14) * f, 7 * f, TIMBER[2 if side > 0 else 3], 2 * f)
        if f > 1:
            art.line((cx + side * 3) * f, 18 * f - 2, (cx + side * 14) * f, 7 * f - 2, TIMBER[4 if side < 0 else 3], 1)
    art.rect((cx - 5) * f, (floor - 2) * f, (cx + 6) * f, (floor + 2) * f, TIMBER[2])
    for y in (10, floor - 10):
        for s in range(f):
            for u in range(f):
                art.put((cx - 1) * f + s, y * f + u, IRON[5] if f > 1 and s == u == 0 else IRON[4])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def rail(piece: str, fine: int = 1) -> Image.Image:
    """A tile of the mine's track, the floor's top line its foot: sleepers and two rails:
    `mid`, or `end` (the last tile, with a stop of timber). `fine` draws it that many times
    the draft's size: a rail with a lit head and a web, sleepers with their grain."""
    f = fine
    W0, H0 = TILE, TILE + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK  # the floor's top line
    for x in (2, 9):
        for dx in range(4):
            for d, k in ((-1, 3), (0, 2), (1, 1)):
                for s in range(f):
                    for u in range(f):
                        grain = f > 1 and (s + (x + dx) * f + u * 3) % 5 == 0 and k == 2
                        art.put((x + dx) * f + s, (floor + d) * f + u, TIMBER[k - (1 if grain else 0)])
    for X in range(W0 * f):
        if f == 1:
            art.put(X, floor - 3, IRON[5])
            art.put(X, floor - 2, IRON[2])
        else:
            for u, k in enumerate((6 if len(IRON) > 6 else 5, 5, 3, 2)):
                art.put(X, (floor - 3) * f + u, IRON[k])
    if piece == "end":
        art.rect(10 * f, (floor - 9) * f, 16 * f, (floor - 1) * f, TIMBER[3])
        art.rect(10 * f, (floor - 9) * f, 16 * f, (floor - 8) * f, TIMBER[5])
        art.rect(15 * f, (floor - 9) * f, 16 * f, (floor - 1) * f, TIMBER[1])
    return art.image()


def mine_cart(variant: int, fine: int = 1) -> Image.Image:
    """An ore cart on the track: an iron tub, ribbed, two wheels; full of copper ore (or
    tipped over and empty). `fine` draws it that many times the draft's size: a tub whose
    sides slope smoothly, round wheels, lumps of ore with a glint."""
    r = rng("minecart", variant)
    f = fine
    W0, H0 = 2 * TILE, 26 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK - 3  # on the rail
    tipped = variant == 1

    def wheel(wx, wy):
        for DX in range(-3 * f, 3 * f + 1):
            for DY in range(-3 * f, 3 * f + 1):
                dx, dy = DX / f, DY / f
                if dx * dx + dy * dy <= 9:
                    art.put(wx * f + DX, wy * f + DY, IRON[1 if dx * dx + dy * dy > 4 else 3 if f == 1 or dx * dx + dy * dy > 0.6 else 4])

    if not tipped:
        for Y in range((floor - 18) * f, (floor - 4) * f):
            y = Y / f
            t = (y - (floor - 18)) / 14
            inset = int(t * 3) if f == 1 else t * 3
            for X in range(int((2 + inset) * f), int((W0 - 2 - inset) * f)):
                x = X / f
                k = 4 if x < 5 + inset else 1 if x > W0 - 6 - inset else 3
                if (int(x) - 2) % 7 == 0 and (f == 1 or X % f == 0):
                    k = 2
                art.put(X, Y, IRON[k])
        for X in range(f, (W0 - 1) * f):
            for s in range(f):
                art.put(X, (floor - 19) * f + s, IRON[5])
        for _ in range(16):
            x, y = r.randint(4, W0 - 5), r.randint(floor - 24, floor - 19)
            if y >= floor - 22 + abs(x - W0 // 2) // 4:
                colour = COPPER[r.randint(2, 5)]
                dot(art, x, y, colour, f, COPPER[5] if f > 1 else None)
                dot(art, x + 1, y, COPPER[2], f)
        for wx in (8, W0 - 9):
            wheel(wx, floor - 2)
    else:
        for Y in range((floor - 14) * f, (floor + 2) * f):
            y = Y / f
            for X in range(4 * f, (W0 - 2) * f):
                x = X / f
                if (x - 4) + (floor + 2 - y) * 0.7 < 22:
                    art.put(X, Y, IRON[3 if x < 10 else 2])
        wheel(24, floor - 12)
        for _ in range(10):
            dot(art, r.randint(0, 10), floor + r.randint(-1, 1), COPPER[r.randint(2, 5)], f)
    for _ in range(f):
        art.outline(IRON[0])
    return art.image()


def crates(variant: int, fine: int = 1) -> Image.Image:
    """What is stacked in the mine: crates and a barrel (variant 0), sacks with a
    pick against them (variant 1), a ladder lying against the wall (variant 2). `fine`
    draws it that many times the draft's size: boards a pixel apart, round sacks."""
    r = rng("crates", variant)
    f = fine
    W0, H0 = 3 * TILE, 30 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    if variant == 0:
        for x0, y0, s in ((2, floor - 16, 16), (20, floor - 14, 14), (8, floor - 30, 14)):
            for Y in range(y0 * f, (y0 + s) * f):
                y = Y // f
                for X in range(x0 * f, (x0 + s) * f):
                    x = X // f
                    if f == 1:
                        edge = x in (x0, x0 + s - 1) or y in (y0, y0 + s - 1)
                        art.put(X, Y, TIMBER[4 if edge and (x == x0 or y == y0) else 2 if edge else 3 if (y - y0) % 5 else 1])
                        continue
                    lo, hi = X < x0 * f + f or Y < y0 * f + f, X >= (x0 + s - 1) * f or Y >= (y0 + s - 1) * f
                    seam = (Y - y0 * f) % (5 * f) == 0
                    art.put(X, Y, TIMBER[4 if lo and not hi else 2 if hi else 1 if seam else 3])
            art.line((x0 + 1) * f, (y0 + 1) * f, (x0 + s - 2) * f, (y0 + s - 2) * f, TIMBER[2], 1 if f == 1 else 1.5)
        for Y in range((floor - 18) * f, floor * f):
            y = Y / f
            for X in range(36 * f, 46 * f):
                x = X / f
                bulge = abs(y - (floor - 9)) < 6
                k = 4 if x < 38 else 2 if x > 43 else 3
                hoop = int(y) in (floor - 15, floor - 4) if f == 1 else Y in ((floor - 15) * f, (floor - 4) * f, (floor - 15) * f + 1, (floor - 4) * f + 1)
                art.put(X, Y, IRON[3] if hoop else TIMBER[k + (1 if bulge and k < 4 else 0)])
    elif variant == 1:
        for cx, cy, rx, ry in ((12, floor - 6, 9, 6), (26, floor - 6, 9, 6), (19, floor - 15, 8, 6)):
            for Y in range((cy - ry) * f, (cy + ry) * f):
                y = Y / f + (0.5 / f - 0.5 if f > 1 else 0)
                for X in range((cx - rx) * f, (cx + rx) * f):
                    x = X / f + (0.5 / f - 0.5 if f > 1 else 0)
                    if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1:
                        art.put(X, Y, THATCH[4 if x < cx - 2 and y < cy else 2 if y > cy + 2 else 3])
        art.line(34 * f, floor * f, 40 * f, (floor - 24) * f, TIMBER[3], 2 * f)
        art.line(34 * f, (floor - 25) * f, 46 * f, (floor - 21) * f, IRON[4], 2 * f)
    else:
        for side in (0, 9):
            art.line((6 + side) * f, floor * f, (18 + side) * f, (floor - 28) * f, TIMBER[3], 2 * f)
        for k in range(1, 6):
            t = k / 6
            art.line((6 + 12 * t) * f, (floor - 28 * t) * f, (15 + 12 * t) * f, (floor - 28 * t) * f, TIMBER[2], f)
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


# --- Behind the surface: the ridges -------------------------------------------------
BACKDROP_W, BACKDROP_H = VIEW


def _profile(seed, waves, width=BACKDROP_W, fine: int = 1):
    """A sum of sines that repeats every `width` draft pixels, asked `fine` times a pixel."""
    r = rng("earth ridge", seed)
    xs = np.arange(width * fine) / fine
    total = np.zeros(width * fine)
    for k, amp in waves:
        total += amp * np.sin(2 * np.pi * k * xs / width + r.uniform(0, 6.283))
    return total


def ridge_mountains(width: int = 1280, fine: int = 1) -> Image.Image:
    """The mountains far off: peaks of rock with snow on them, each lit on its face to the
    left of the ridge that runs down from its top and in the blue of the low sky on the
    other, and a band of haze along their foot. `fine` draws them that many times the
    draft's size: the gullies a pixel wide, the haze in a finer dither."""
    f = fine
    H, W = BACKDROP_H * f, width * f
    a = np.zeros((H, W, 4), dtype=np.uint8)
    body, shade, lit, deep = hexes("8eaecb", "7d9bc0", "a9c4dc", "7290b6")
    snow, snow_shade = hexes("eef3f8", "c4d2e6")
    r = rng("mountains")
    peaks = []
    count = width // 92
    for k in range(count):
        peaks.append((width * (k + r.uniform(0.1, 0.9)) / count, r.uniform(120, 245), r.uniform(0.6, 0.95), r.uniform(-0.35, 0.35)))
    base = 66 + _profile("m base", [(6, 8), (14, 4)], width, f)
    rough = _profile("m rough", [(58, 2.0), (94, 1.4), (166, 0.8)], width, f)
    height = base.copy()
    owner = np.full(W, -1)
    for i, (px, ph, steep, _) in enumerate(peaks):
        for X in range(W):
            d = X / f - px
            d = (d + width / 2) % width - width / 2
            hgt = ph - abs(d) * steep * (1.15 if d > 0 else 1.0)
            if hgt > height[X]:
                height[X], owner[X] = hgt, i
    height += rough
    top = (H - np.round(height * f)).astype(int)
    edge = noise("snowline", W, 1, 7.0 * f)[0]
    for X in range(W):
        x = X / f
        for Y in range(max(top[X], 0), H):
            y = Y / f
            colour = body
            if owner[X] >= 0:
                px, ph, _, lean = peaks[owner[X]]
                peak_y = BACKDROP_H - ph
                ridge = px + (y - peak_y) * (0.25 + lean) + 3 * math.sin((y - peak_y) * 0.12)
                d = (x - ridge + width / 2) % width - width / 2
                colour = lit if d < 0 else shade
                gully = (int(d) + int(y) // 3) % 23 == 0 if f == 1 else (int(d * f) + int(y) // 3 * f) % (23 * f) == 0
                if d >= 0 and gully:
                    colour = deep  # a gully down the shaded face
                if BACKDROP_H - y > 168 + 10 * edge[X] and (Y - top[X]) / f < 34:
                    colour = snow if d < 0 else snow_shade
            a[Y, X, :3] = colour
            a[Y, X, 3] = 255
    # Haze along the foot: the mountains fade into the low sky colour, dithered.
    haze = np.array(hexes("b4d0e6")[0], dtype=float)
    for Y in range((BACKDROP_H - 110) * f, H):
        t = (Y / f - (BACKDROP_H - 110)) / 110
        for X in range(W):
            if a[Y, X, 3] and t * 16 > BAYER_4[Y % 4][X % 4] + 0.5:
                a[Y, X, :3] = (a[Y, X, :3] * 0.45 + haze * 0.55).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


BAYER_4 = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def _crowns(a, top, colours, r, x0, x1, base, size, kinds=("round", "pine"), fine: int = 1):
    """A row of tree crowns standing on `base` (a row per column) from x0 to x1, in the given
    colours (dark, mid, lit): round ones and pointed ones mixed. `a`, `top` and `base` are
    `fine` times the draft's size; x0, x1 and the crowns' sizes are in the draft's pixels."""
    f = fine
    width = a.shape[1]
    height = a.shape[0]
    x = x0
    while x < x1:
        kind = r.choice(kinds)
        rad = r.randint(*size)
        foot = int(base[(x * f) % width])
        if kind == "round":
            for DX in range(-rad * f, rad * f + 1):
                for DY in range(-rad * f, rad * f + 1):
                    dx, dy = DX / f, DY / f
                    if dx * dx + dy * dy * 1.2 <= rad * rad:
                        X, Y = (x * f + DX) % width, foot - rad * f + DY
                        if 0 <= Y < height:
                            lit = dx + dy < -rad * 0.4
                            dark = dx + dy > rad * 0.5
                            a[Y, X, :3] = colours[2] if lit else colours[0] if dark else colours[1]
                            a[Y, X, 3] = 255
        else:
            tall = int(rad * 3.2)
            for I in range(tall * f):
                i = I / f
                half = rad * 0.75 * i / tall
                for DX in range(-int(half * f), int(half * f) + 1):
                    dx = DX / f
                    X, Y = (x * f + DX) % width, foot - tall * f + I
                    if 0 <= Y < height:
                        a[Y, X, :3] = colours[2] if dx < -half * 0.3 else colours[0] if dx > half * 0.4 else colours[1]
                        a[Y, X, 3] = 255
        x += r.randint(max(rad // 2, 2), rad + 2)


def ridge_hills(width: int = 1920, fine: int = 1) -> Image.Image:
    """The hills between the mountains and the woods, blue with distance, with the old
    world on them: a castle on a crag, a church tower, a few roofs, clumps of wood. `fine`
    draws them that many times the draft's size."""
    f = fine
    H, W = BACKDROP_H * f, width * f
    a = np.zeros((H, W, 4), dtype=np.uint8)
    body, shade, lit = hexes("6f9aa6", "5f8a9a", "88b2b8")
    dark_tree, mid_tree, lit_tree = hexes("5a8494", "66919c", "7ea6ae")
    r = rng("hills")
    height = 92 + _profile("hills", [(6, 16), (15, 9), (33, 4)], width, f)
    top = (H - np.round(height * f)).astype(int)
    for X in range(W):
        rising = height[X] >= height[(X - 1) % W]
        for Y in range(max(top[X], 0), H):
            a[Y, X, :3] = lit if rising and Y - top[X] < 3 * f else body if Y - top[X] > 2 * f + (f - 1) or rising else shade
            a[Y, X, 3] = 255
    # A castle on the highest hill of the middle third, and woods everywhere else.
    draft_height = height[::f]
    best = int(np.argmax(draft_height[width // 3 : width // 3 + 300])) + width // 3
    _crowns(a, top, (dark_tree, mid_tree, lit_tree), r, 0, best - 70, top + 2 * f, (3, 6), fine=f)
    _crowns(a, top, (dark_tree, mid_tree, lit_tree), r, best + 70, width, top + 2 * f, (3, 6), fine=f)

    def tower(x, wide, tall, cap="flat"):
        foot = top[x * f] + 3 * f
        for X in range(x * f, (x + wide) * f):
            for Y in range(foot - tall * f, foot):
                a[Y, X % W, :3] = lit if X < (x + 2) * f - (f - 1 if f > 1 else 0) else shade if X > (x + wide - 3) * f + (f - 1) else body
                a[Y, X % W, 3] = 255
        if cap == "flat":
            for x_ in range(x, x + wide, 3):
                for Y in range(foot - (tall + 3) * f, foot - tall * f):
                    for s in range(f):
                        a[Y, (x_ * f + s) % W, :3] = body
                        a[Y, (x_ * f + s) % W, 3] = 255
        else:
            for I in range((tall // 2) * f):
                i = I / f
                half = wide / 2 * i / (tall // 2)
                for X in range(int((x + wide / 2 - half) * f), int((x + wide / 2 + half) * f) + (1 if f == 1 else f)):
                    a[foot - (tall + tall // 2) * f + I, X % W, :3] = shade if X > (x + wide / 2) * f else lit
                    a[foot - (tall + tall // 2) * f + I, X % W, 3] = 255
        return foot

    tower(best - 22, 44, 22)
    tower(best - 30, 10, 38)
    tower(best + 18, 9, 46, cap="spire")
    tower(best - 4, 12, 30)
    for wx in (best - 12, best + 4, best + 30):
        for s in range(f):
            a[top[best * f] - 14 * f : top[best * f] - 9 * f, (wx * f + s) % W, :3] = hexes("4c7080")[0]
    # A village far off with its church, and another; a lone tower on a hill.
    for church, roofs in ((150, (126, 162, 176)), (1330, (1306, 1344, 1360, 1376))):
        tower(church, 7, 26, cap="spire")
        for rx in roofs:
            tower(rx, 12, 7, cap="spire")
    tower(1700, 10, 34)
    # A band of haze along their foot, between them and the woods: they stand further off.
    haze = np.array(hexes("a6c6d4")[0], dtype=float)
    for Y in range((BACKDROP_H - 70) * f, H):
        t = (Y / f - (BACKDROP_H - 70)) / 70
        for X in range(W):
            if a[Y, X, 3] and t * 16 > BAYER_4[Y % 4][X % 4] + 0.5:
                a[Y, X, :3] = (a[Y, X, :3] * 0.5 + haze * 0.5).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


def ridge_wood(seed: str, base: float, waves, colours, ground, size, density_kinds, width: int = VIEW[0], fine: int = 1) -> Image.Image:
    """A band of wood on a ridge that repeats sideways: the ground's line lit where it
    rises, two rows of crowns on it. `fine` draws it that many times the draft's size."""
    f = fine
    H, W = BACKDROP_H * f, width * f
    a = np.zeros((H, W, 4), dtype=np.uint8)
    r = rng("earth wood", seed)
    height = base + _profile(seed, waves, width, f)
    top = (H - np.round(height * f)).astype(int)
    for X in range(W):
        rising = height[X] >= height[(X - 1) % W]
        for Y in range(max(top[X], 0), H):
            a[Y, X, :3] = ground[2] if rising and Y - top[X] < 2 * f else ground[1] if Y - top[X] < 4 * f else ground[0]
            a[Y, X, 3] = 255
    _crowns(a, top, colours, r, 0, width, top + 3 * f, size, density_kinds, fine=f)
    _crowns(a, top, colours, r, 7, width, top + 8 * f, size, density_kinds, fine=f)
    return Image.fromarray(a, "RGBA")


def ridge_mid(fine: int = 1) -> Image.Image:
    return ridge_wood(
        "mid",
        64,
        [(1, 10), (3, 9), (5, 5), (11, 2)],
        hexes("3e7466", "4b8470", "62a07e"),
        hexes("4a8670", "56937a", "6eaa86"),
        (5, 10),
        ("round", "round", "pine"),
        fine=fine,
    )


def ridge_near(fine: int = 1) -> Image.Image:
    return ridge_wood(
        "near",
        34,
        [(2, 7), (5, 5), (9, 3)],
        hexes("234a3a", "2d5a42", "3e744e"),
        hexes("2a5240", "305c46", "3a6c50"),
        (7, 14),
        ("round", "pine", "round"),
        fine=fine,
    )


# --- Behind the mine ------------------------------------------------------------------


def cave_earth(region: dict) -> Image.Image:
    """The rock behind the Earth's caves: beds of earth and stone that lie in long gentle
    waves, of different thickness and shade, with boulders in them; dark, so a wall still
    reads in front of it; tiles both ways."""
    size = 128
    ramp = hexes(*region["cave"].split())
    r = rng("cave earth")
    t = np.full((size, size), 2, dtype=np.int16)
    ys, xs = np.mgrid[0:size, 0:size]
    # The beds: (where it starts in the 128 rows, how thick, its shade); a seam under each.
    beds = [(0, 22, 2), (22, 9, 3), (31, 26, 2), (57, 6, 1), (63, 30, 2), (93, 12, 3), (105, 23, 2)]
    wob = (3.0 * np.sin(2 * np.pi * xs / size + 0.7) + 1.6 * np.sin(2 * np.pi * xs * 2 / size + 2.1) + 0.8 * np.sin(2 * np.pi * xs * 5 / size + 4.0))
    yy = (ys + np.round(wob).astype(int)) % size
    for y0, thick, tone in beds:
        inside = (yy >= y0) & (yy < y0 + thick)
        t[inside] = tone
        t[yy == (y0 + thick - 1) % size] = 1
        t[(yy == y0) & ((xs * 7 + ys) % 5 != 0)] = min(tone + 1, 4)
    for _ in range(11):
        cx, cy, rx, ry = r.randrange(size), r.randrange(size), r.randint(4, 10), r.randint(3, 6)
        for dy in range(-ry, ry + 1):
            for dx in range(-rx, rx + 1):
                d = (dx / rx) ** 2 + (dy / ry) ** 2
                if d <= 1:
                    X, Y = (cx + dx) % size, (cy + dy) % size
                    t[Y, X] = 4 if (dx / rx + dy / ry) < -0.5 and d > 0.35 else 1 if d > 0.8 else 3
    for _ in range(size * size // 60):
        x, y = r.randrange(size), r.randrange(size)
        t[y, x] = max(1, t[y, x] - 1)
    rgba = np.zeros((size, size, 4), dtype=np.uint8)
    rgba[..., :3] = np.array(ramp, dtype=np.uint8)[np.clip(t, 0, 4)]
    rgba[..., 3] = 255
    return Image.fromarray(rgba, "RGBA")


def far_mine(region: dict) -> Image.Image:
    """What stands far off in the Earth's caves: the old workings, a little lighter than the
    rock: trestles of timber one over another, ladders between their decks, a wheel on a
    head frame, ropes; and hanging from the roof, roots and the odd stalactite."""
    W, H = 640, 360
    ramp = hexes(*region["cave"].split())
    lit = tuple(min(255, int(c * 1.7) + 12) for c in ramp[4])
    mid = tuple(min(255, int(c * 1.35) + 7) for c in ramp[4])
    dark = tuple(min(255, int(c * 1.1) + 3) for c in ramp[4])
    a = np.zeros((H, W, 4), dtype=np.uint8)
    r = rng("far mine")

    def put(x, y, colour):
        y = int(round(y))
        if 0 <= y < H:
            a[y, int(round(x)) % W] = (*colour, 255)

    def beam(x0, y0, x1, y1, colour, width=2):
        steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(steps + 1):
            t = i / steps
            for k in range(width):
                put(x0 + (x1 - x0) * t + (k if abs(y1 - y0) > abs(x1 - x0) else 0), y0 + (y1 - y0) * t + (0 if abs(y1 - y0) > abs(x1 - x0) else k), colour)

    x = r.randint(10, 40)
    while x < W - 120:
        wide = r.randint(80, 150)
        decks = r.randint(2, 4)
        foot = H - 1
        deck_h = r.randint(44, 64)
        for d in range(decks):
            y = foot - (d + 1) * deck_h
            beam(x, y, x + wide, y, lit, 3)
            for px in range(x, x + wide + 1, max(wide // 3, 20)):
                beam(px, y, px, y + deck_h, mid, 3)
            for px in range(x, x + wide - 20, max(wide // 3, 20)):
                beam(px, y + deck_h, px + max(wide // 3, 20), y + 3, dark, 1)
            if r.random() < 0.7:
                lx = x + r.randint(10, wide - 14)
                beam(lx, y, lx, y + deck_h, mid, 1)
                beam(lx + 6, y, lx + 6, y + deck_h, mid, 1)
                for ry in range(y + 4, y + deck_h, 6):
                    beam(lx, ry, lx + 6, ry, dark, 1)
        if r.random() < 0.6:
            top = foot - decks * deck_h
            cx = x + wide // 2
            beam(cx - 16, top, cx, top - 40, mid, 2)
            beam(cx + 16, top, cx, top - 40, mid, 2)
            for ang in range(0, 360, 6):
                put(cx + 10 * math.cos(math.radians(ang)), top - 40 + 10 * math.sin(math.radians(ang)), lit)
            beam(cx + 10, top - 40, cx + 10, foot, dark, 1)
        x += wide + r.randint(60, 120)
    # Roots and drips from the roof.
    for k in range(18):
        rx = r.randrange(W)
        length = r.randint(14, 70)
        for y in range(length):
            put(rx + int(2 * math.sin(y * 0.2 + k)), y, dark if y > length * 0.6 else mid)
    return Image.fromarray(a, "RGBA")


# --- The second round: more trees, a village that is lived in, a mine that was worked,
# water that shows the sky, and what comes and goes with the hour ------------------------

BIRCH_BARK = ramp_of("2a2a2c 6a6a6c b8b6b0 d6d4cc ecebe4 fafaf6")
BIRCH_LEAF = ramp_of("1a3416 2c5a22 4a7e2c 6e9e36 98bc46 c4d866 e2ec90")
GIANT = ramp_of("0e2016 15331f 1d4828 2a6230 427e36 64a03e 98c454")
MOSS = hexes("4a6a2a", "6a8a34")


def birch(variant: int, fine: int = 1) -> Image.Image:
    """A birch: a slim white trunk with black marks, leaning a little, an airy crown of
    small light bundles that lets the sky through. `fine` draws it that many times the
    draft's size: a round trunk, marks in finer flecks, twigs a pixel wide."""
    r = rng("birch", variant)
    f = fine
    tall = r.randint(110, 140)
    wide = int(tall * 0.5)
    W, H = wide + 16, tall + SINK + 4
    art = Art(W * f, H * f)
    cx, foot = W // 2, H - SINK + 6
    lean = r.choice([-1, 1]) * r.uniform(0.6, 1.4)
    for I in range((tall - 18) * f):
        i = I / f
        t = i / tall
        half = 3.2 - 1.6 * t
        x0 = cx + lean * t * t * 12
        for X in range(int((x0 - half) * f), int((x0 + half) * f) + 1):
            x = X / f
            off = (x + 0.5 / f - x0) / half
            k = 5 if off < -0.3 else 2 if off > 0.45 else 4
            if f == 1:
                if (I * 7 + X * 3) % 13 == 0 or (I % 11 in (0, 1) and off > -0.2):
                    k = 0
            elif (I * 7 + X * 13) % 47 == 0 or (int(i) % 11 in (0, 1) and off > -0.2 + (0.25 if int(i) % 11 == 1 and I % f else 0)):
                k = 0  # a fleck, or a band that thins at its edge
            art.put(X, foot * f - I, BIRCH_BARK[k])
    top = foot - tall + 18
    for k in range(7):
        a = -math.pi * (0.1 + 0.8 * k / 6)
        bx = cx + lean * 10 + math.cos(a) * wide * 0.28
        by = top + 22 + math.sin(a) * tall * 0.16 + r.uniform(-4, 8)
        art.line((cx + lean * 8) * f, (top + 40) * f, bx * f, (by + 4) * f, BIRCH_BARK[1], 1 if f == 1 else 1.5)
        bundle(art, bx * f, by * f, wide * r.uniform(0.13, 0.19) * f, tall * r.uniform(0.09, 0.13) * f, BIRCH_LEAF, r, ("birch", variant, k))
    for _ in range(f):
        art.outline(BIRCH_LEAF[0])
    return art.image()


def giant_oak(fine: int = 1) -> Image.Image:
    """The old oak on the hill: a landmark, fifteen tiles high, its trunk four of the hero
    wide, roots like walls, a crown of many bundles with deep gaps, moss on its north side.
    `fine` draws it that many times the draft's size: leaf by leaf, bark in finer
    streaks, the moss in tufts."""
    r = rng("giant oak")
    f = fine
    tall, wide = 240, 230
    W, H = wide + 20, tall + SINK + 4
    art = Art(W * f, H * f)
    foot_x, foot_y = W // 2, H - SINK + 8
    trunk(art, foot_x * f, foot_y * f, 120 * f, 30 * f, 16 * f, BARK, r)
    for side in (-1, 1):
        for k in range(3):
            x0 = foot_x + side * (12 + k * 6)
            art.line(x0 * f, (foot_y - 14 + k * 3) * f, (x0 + side * (14 + k * 6)) * f, (foot_y + 2) * f, BARK[2 + (side < 0)], (5 - k) * f)
    limbs = []
    for k in range(9):
        a = -math.pi * (0.08 + 0.84 * k / 8) + r.uniform(-0.1, 0.1)
        d = r.uniform(0.55, 0.95)
        bx = W / 2 + math.cos(a) * wide * 0.42 * d
        by = foot_y - 150 + math.sin(a) * tall * 0.3 * d
        limbs.append((bx, by))
        branch(art, foot_x * f, (foot_y - 104) * f, bx * f, (by + 10) * f, (7 if abs(bx - foot_x) > 40 else 9) * f, BARK)
    for k, (bx, by) in enumerate(sorted(limbs, key=lambda b: -b[1])):
        bundle(art, bx * f, by * f, r.uniform(30, 44) * f, r.uniform(26, 34) * f, GIANT, r, ("giant", k))
    for k in range(5):
        bundle(art, (W / 2 + r.uniform(-60, 60)) * f, (foot_y - 200 + r.uniform(-10, 20)) * f, r.uniform(28, 38) * f, r.uniform(22, 28) * f, GIANT, r, ("giant top", k))
    for _ in range(140):
        x, y = foot_x - r.randint(4, 15), foot_y - r.randint(0, 110)
        if art.has(x * f, y * f):
            art.put(x * f, y * f, MOSS[r.randrange(2)])
            if f > 1:
                art.put(x * f + 1, y * f, MOSS[1])
                art.put(x * f, y * f - 1, MOSS[0])
    for _ in range(f):
        art.outline(GIANT[0])
    return art.image()


def log(fine: int = 1) -> Image.Image:
    """A fallen log, its cut end pale, moss along its top, a fungus on its side. `fine`
    draws it that many times the draft's size: a round body, the rings of its end."""
    f = fine
    W0, H0 = 3 * TILE + 6, 18 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for X in range(2 * f, (W0 - 4) * f):
        for Y in range((floor - 12) * f, (floor + 2) * f):
            y = Y / f
            if f == 1:
                k = 4 if y < floor - 9 else 2 if y > floor - 3 else 3
                if (X * 5 + Y) % 9 == 0:
                    k = 1
            else:
                round_ = (y - (floor - 5)) / 7  # -1 its top, 1 its underside
                k = 4 if round_ < -0.55 else 2 if round_ > 0.55 else 3
                if (X * 5 + Y * 2) % 13 == 0:
                    k = 1
            art.put(X, Y, BARK[k])
    pale, dark = hexes("c8a878", "a88858")
    for Y in range((floor - 12) * f, (floor + 2) * f):
        for X in range((W0 - 6) * f, (W0 - 2) * f):
            if f == 1:
                art.put(X, Y, (pale, dark)[(X + Y) % 2])
            else:
                d = math.hypot((X / f - (W0 - 4)) / 2, (Y / f - (floor - 5)) / 7)
                art.put(X, Y, dark if (d * 4) % 1 < 0.3 else pale)  # its rings
    for x in (14, 15, 16, 17):
        dot(art, x, floor - 13, hexes("e8d8a0")[0], f)
        dot(art, x, floor - 14, hexes("c8b880")[0] if x in (15, 16) else hexes("e8d8a0")[0], f)
    for x in range(6, 30, 3):
        dot(art, x, floor - 12, MOSS[x % 2], f, MOSS[1] if f > 1 else None)
    for _ in range(f):
        art.outline(BARK[0])
    return art.image()


def stump(variant: int, fine: int = 1) -> Image.Image:
    """A stump of a felled tree, its cut face pale with rings; one has an axe left in it.
    `fine` draws it that many times the draft's size: the rings of its face."""
    f = fine
    W0, H0 = 26, 16 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for Y in range((floor - 10) * f, (floor + 3) * f):
        y = Y / f
        widen = (1 if y > floor - 2 else 0) if f == 1 else max(0.0, min(1.0, (y - (floor - 3)) / 2))
        for X in range(int((6 - widen) * f), int((20 + widen) * f)):
            x = X / f
            k = 4 if x < 9 else 2 if x > 17 else 3
            if f > 1 and (X * 3 + Y) % 13 == 0:
                k -= 1  # a furrow of the bark
            art.put(X, Y, BARK[k])
    pale, light, dark = hexes("c8a878", "e0c898", "a88858")
    for X in range(6 * f, 20 * f):
        x = X / f
        for s in range(f):
            art.put(X, (floor - 11) * f + s, pale)
            if f == 1:
                art.put(X, floor - 12, light if 8 < x < 18 else pale)
            else:
                ring = abs(math.hypot((x - 12.5) / 6, (s + 0.5) / f - 0.5) * 3 % 1 - 0.5) < 0.12
                art.put(X, (floor - 12) * f + s, dark if ring else light if 8 < x < 18 else pale)
    dot(art, 12, floor - 12, dark, f)
    if variant % 2:
        art.line(14 * f, (floor - 12) * f, 21 * f, (floor - 22) * f, TIMBER[3], 2 * f)
        art.rect(10 * f, (floor - 15) * f, 16 * f, (floor - 11) * f, IRON[4])
        if f > 1:
            art.rect(10 * f, (floor - 15) * f, 16 * f, (floor - 15) * f + 1, IRON[5])
    for _ in range(f):
        art.outline(BARK[0])
    return art.image()


def toadstools(fine: int = 1) -> Image.Image:
    """A ring of red toadstools on white stems, a fleck of light on each cap. `fine` draws
    them that many times the draft's size: round caps with white spots."""
    f = fine
    W0, H0 = 2 * TILE, 12 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    stem, red, lit, edge = hexes("efe4cc", "d23a2a", "f05a40", "f07a5a")
    for cx, tall, half in ((5, 6, 3), (12, 9, 4), (19, 5, 2), (25, 7, 3)):
        for h in range(-2, tall):
            dot(art, cx, floor - h, stem, f)
        if f == 1:
            for k in range(2):
                for x in range(cx - half, cx + half + 1):
                    art.put(x, floor - tall - k, (red, lit)[k] if x > cx - half else edge)
            art.put(cx - 1, floor - tall - 1, hexes("ffffff")[0])
            continue
        for DY in range(-2 * f, 0):  # a cap, domed
            for DX in range(-half * f, (half + 1) * f):
                u, v = (DX + 0.5 - f / 2) / ((half + 0.5) * f), (DY + 0.5) / (2 * f)
                if u * u + v * v <= 1:
                    art.put(cx * f + DX, (floor - tall + 1) * f + DY, edge if u < -0.6 else lit if v < -0.5 else red)
        for dx in (-1, 1):
            art.put((cx + dx) * f, (floor - tall) * f - 1 - (dx > 0), (255, 255, 255))
    for _ in range(f):
        art.outline(hexes("33201a")[0])
    return art.image()


def washing_line(fine: int = 1) -> Image.Image:
    """A washing line between two posts, a few things pegged out on it. `fine` draws it
    that many times the draft's size: the line a pixel thin, the cloth's folds."""
    f = fine
    W0, H0 = 5 * TILE, 30 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for px in (3, W0 - 5):
        art.rect(px * f, (floor - 28) * f, (px + 2) * f, (floor + 4) * f, TIMBER[3])
        if f > 1:
            art.rect(px * f, (floor - 28) * f, px * f + 1, (floor + 4) * f, TIMBER[4])
    sag = lambda x: floor - 26 + int(4 * math.sin(math.pi * (x - 4) / (W0 - 8)))  # noqa: E731
    smooth = lambda x: floor - 26 + 4 * math.sin(math.pi * (x - 4) / (W0 - 8))  # noqa: E731
    for X in range(4 * f, (W0 - 4) * f):
        if f == 1:
            art.put(X, sag(X), THATCH[1])
        else:
            art.put(X, int(round(smooth(X / f) * f)), THATCH[1])
    for x0, w, h, colour in ((12, 9, 11, "d8d8e0"), (24, 12, 14, "8a4a5a"), (40, 8, 9, "5a7aa8"), (52, 5, 6, "d8c070"), (60, 8, 10, "e0e0e8")):
        c = hexes(colour)[0]
        dark = tuple(int(v * 0.75) for v in c)
        for X in range(x0 * f, (x0 + w) * f):
            x = X / f
            top = (sag(int(x)) + 1) * f if f == 1 else int(round((smooth(x) + 1) * f))
            for Y in range(top, top + h * f):
                fold = f > 1 and (X - x0 * f) % (3 * f) == 0 and Y > top + f
                art.put(X, Y, dark if x > x0 + w - 3 or Y >= top + (h - 2) * f or fold else c)
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def garden(fine: int = 1) -> Image.Image:
    """A kitchen garden behind a low fence: rows of cabbages, beans climbing their canes,
    a scarecrow's hat on a stick. `fine` draws it that many times the draft's size: round
    cabbages with their leaves, the fence's laths a pixel thinner."""
    r = rng("garden")
    f = fine
    W0, H0 = 5 * TILE, 22 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for X in range(2 * f, (W0 - 2) * f):
        x = X // f
        for Y in range((floor - 3) * f, (floor + 3) * f):
            art.put(X, Y, BARK[1] if (x // 6) % 2 else BARK[2])
    for cx in range(6, W0 - 20, 7):
        for DX in range(-3 * f, 4 * f):
            for DY in range(-3 * f, f):
                dx, dy = DX / f, DY / f
                if dx * dx + dy * dy * 2 <= 9:
                    k = 5 if dx < 0 and dy < -1 else 4 if dx < 1 else 3
                    if f > 1 and abs(math.atan2(dy, dx + 0.01) * 3 % 1 - 0.5) < 0.08:
                        k -= 1  # a cabbage's leaves
                    art.put(cx * f + DX, (floor - 3) * f + DY, OAK[k])
    for cx in range(W0 - 18, W0 - 4, 4):
        art.line(cx * f, (floor - 2) * f, cx * f, (floor - 20) * f, TIMBER[4], 1 if f == 1 else 1.5)
        for y in range(floor - 18, floor - 2, 2):
            colour = OAK[4 if r.random() < 0.5 else 3]
            side = 1 if (y // 2) % 2 else -1
            if f == 1:
                art.put(cx + side, y, colour)
            else:
                art.put(cx * f + side * 2, y * f, colour)
                art.put(cx * f + side * 3, y * f + 1, colour)
                art.put(cx * f + side, y * f + 1, OAK[5])
    for X in range(f, (W0 - 1) * f):
        x = X // f
        if x % 3 == 0 and X % f == 0:
            art.line(X, (floor + 2) * f, X, (floor - 6) * f, TIMBER[3], 1 if f == 1 else 1.5)
        for s in range(f):
            art.put(X, (floor - 4) * f + s, TIMBER[2] if f == 1 or s else TIMBER[3])
            art.put(X, (floor - 2) * f + s, TIMBER[2] if f == 1 or s else TIMBER[3])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def bench(fine: int = 1) -> Image.Image:
    """A plank bench on two legs. `fine` draws it that many times the draft's size: the
    plank's lit edge and grain."""
    f = fine
    W0, H0 = 2 * TILE + 4, 12 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    art.rect(2 * f, (floor - 7) * f, (W0 - 2) * f, (floor - 5) * f, TIMBER[4])
    art.rect(2 * f, (floor - 5) * f, (W0 - 2) * f, (floor - 4) * f, TIMBER[2])
    if f > 1:
        art.rect(2 * f, (floor - 7) * f, (W0 - 2) * f, (floor - 7) * f + 1, TIMBER[5])
        for X in range(2 * f, (W0 - 2) * f, 7):
            art.put(X, (floor - 6) * f, TIMBER[3])
    for x in (4, W0 - 6):
        art.rect(x * f, (floor - 5) * f, (x + 2) * f, (floor + 3) * f, TIMBER[2])
        if f > 1:
            art.rect(x * f, (floor - 5) * f, x * f + 1, (floor + 3) * f, TIMBER[3])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def barrel(fine: int = 1) -> Image.Image:
    """A water butt: staves bound with iron, rain standing in it. `fine` draws it that many
    times the draft's size: a curved belly, the staves' joints."""
    f = fine
    W0, H0 = TILE + 4, 20 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for Y in range((floor - 18) * f, (floor + 2) * f):
        y = Y / f
        bulge = (1 if floor - 13 < y < floor - 4 else 0) if f == 1 else math.sin(max(0.0, min(1.0, (y - (floor - 18)) / 20)) * math.pi) * 1.2
        for X in range(int((3 - bulge) * f), int((W0 - 3 + bulge) * f)):
            x = X / f
            k = 4 if x < 6 else 2 if x > W0 - 6 else 3
            hoop = int(y) in (floor - 15, floor - 4) if f == 1 else int(y) in (floor - 15, floor - 4)
            if f > 1 and not hoop and (X - 3 * f) % (3 * f) == 0:
                k -= 1  # a joint between the staves
            art.put(X, Y, IRON[3 if not (f > 1 and Y % f == 0) else 4] if hoop else TIMBER[k])
    for X in range(4 * f, (W0 - 4) * f):
        x = X / f
        for s in range(f):
            art.put(X, (floor - 18) * f + s, hexes("8ab4d8")[0] if x < 10 else hexes("5a84a8")[0])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def kennel(fine: int = 1) -> Image.Image:
    """A dog's kennel of boards under a roof, its door dark, a chain out of it. `fine` draws
    it that many times the draft's size: boards a pixel apart, a round door, the chain's links."""
    f = fine
    W0, H0 = 2 * TILE + 4, 22 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for Y in range((floor - 12) * f, (floor + 2) * f):
        for X in range(5 * f, (W0 - 5) * f):
            x = X // f
            k = 3 if (x // 4) % 2 else 2
            if f > 1 and X % (4 * f) == 0:
                k = 1  # the gap between two boards
            art.put(X, Y, TIMBER[k])
    for Y in range((floor - 21) * f, (floor - 11) * f):
        y = Y / f
        t = (y - (floor - 21)) / 10
        for X in range(int((W0 / 2 - 3 - 13 * t) * f), int((W0 / 2 + 3 + 13 * t) * f)):
            k = 4 if X < W0 / 2 * f else 2
            if f > 1 and Y % (3 * f) == 0:
                k -= 1  # a course of its shingles
            art.put(X, Y, ROOF[k])
    for Y in range((floor - 9) * f, (floor + 1) * f):
        y = Y / f
        for X in range(13 * f, 21 * f):
            x = X / f + (0.5 / f - 0.5 if f > 1 else 0)
            if (x - 17) ** 2 + ((y - (floor - 5)) * 0.8) ** 2 <= 20 or y > floor - 5:
                art.put(X, Y, DARK)
    for X in range(20 * f, W0 * f):
        x = X // f
        if f == 1:
            art.put(X, floor - 1 + (x % 2), IRON[4])
        else:
            art.put(X, (floor - 1) * f + (1 if (X // 3) % 2 else 0) + 1, IRON[4 if X % 3 else 2])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def mill_tower(fine: int = 1) -> Image.Image:
    """The windmill without its sails, which turn (the sails are their own picture)."""
    return windmill(sails=False, fine=fine)


SAIL_FRAMES = 4  # the sails turn a quarter of a circle in this many frames: four arms look the same every quarter


def sails(frame: int, fine: int = 1) -> Image.Image:
    """The mill's sails at one step of a turn: four lattice arms on the hub, their cloth
    torn in the same place on each arm so every quarter turn looks the same. `fine` draws
    them that many times the draft's size: the lattice a pixel wide, the cloth's tears
    finer."""
    f = fine
    size = 2 * 84 + 4
    art = Art(size * f, size * f)
    hub = (size // 2, size // 2)
    for k in range(4):
        angle = math.radians(k * 90 + frame * 90 / SAIL_FRAMES - 52)
        ex, ey = hub[0] + math.cos(angle) * 82, hub[1] + math.sin(angle) * 82
        art.line(hub[0] * f, hub[1] * f, ex * f, ey * f, TIMBER[2], 3 * f)
        nx, ny = -math.sin(angle), math.cos(angle)
        for d in range(18, 80, 7):
            px, py = hub[0] + math.cos(angle) * d, hub[1] + math.sin(angle) * d
            art.line(px * f, py * f, (px + nx * 14) * f, (py + ny * 14) * f, TIMBER[3], 1 if f == 1 else 1.5)
        for side in (0.0, 14.0):
            art.line((hub[0] + math.cos(angle) * 18 + nx * side) * f, (hub[1] + math.sin(angle) * 18 + ny * side) * f, (ex + nx * side) * f, (ey + ny * side) * f, TIMBER[3 if side else 4], 1 if f == 1 else 1.5)
        # Cloth still on the inner half of the lattice, torn on the outer.
        for D in range(20 * f, 50 * f):
            d = D / f
            for S in range(f, 14 * f):
                s = S / f
                if (int(d) + int(s)) % 5 == 0:
                    continue
                px, py = hub[0] + math.cos(angle) * d + nx * s, hub[1] + math.sin(angle) * d + ny * s
                if not art.has(px * f, py * f):
                    art.put(px * f, py * f, PLASTER[4] if s < 8 else PLASTER[3])
    for DX in range(-3 * f, 3 * f + 1):
        for DY in range(-3 * f, 3 * f + 1):
            dx, dy = DX / f, DY / f
            if dx * dx + dy * dy <= 9:
                art.put(hub[0] * f + DX, hub[1] * f + DY, IRON[2 if dx + dy > 0 else 4])
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def rubble(variant: int, fine: int = 1) -> Image.Image:
    """A heap of fallen rock and a broken beam: stones of four shades, lit on their tops.
    `fine` draws it that many times the draft's size: each stone a true round."""
    r = rng("rubble", variant)
    f = fine
    W0, H0 = 4 * TILE, 26 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for _ in range(26):
        cx, rad = r.randint(6, W0 - 6), r.randint(2, 6)
        cy = floor - int(16 * math.sin(math.pi * cx / W0)) + r.randint(-2, 3)
        shade = r.choice([2, 3, 3, 4])
        for DX in range(-rad * f, rad * f + 1):
            for DY in range(-rad * f, rad * f + 1):
                dx, dy = DX / f, DY / f
                if dx * dx + dy * dy <= rad * rad:
                    k = shade + 1 if dx + dy < -rad * 0.6 else shade - 1 if dx + dy > rad * 0.5 else shade
                    art.put(cx * f + DX, cy * f + DY, STONEWORK[max(1, min(k, 5))])
    art.line((W0 // 2 - 14) * f, (floor - 4) * f, (W0 // 2 + 12) * f, (floor - 22) * f, TIMBER[3], 4 * f)
    art.line((W0 // 2 - 14) * f, (floor - 6) * f, (W0 // 2 + 12) * f, (floor - 24) * f, TIMBER[4], f)
    for X in range(0, W0 * f):
        for Y in range((floor + 1) * f, H0 * f):
            art.a[Y, X, 3] = 0 if not art.has(X, Y) else art.a[Y, X, 3]
    for _ in range(f):
        art.outline(STONEWORK[0])
    return art.image()


def puddle(fine: int = 1) -> Image.Image:
    """A puddle on the track, the sky in it. `fine` draws it that many times the draft's
    size, its glints a pixel each."""
    f = fine
    W0, H0 = 3 * TILE, 4 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    sky, deep, under, glint = hexes("6a8aa8", "4a6a88", "3a5470", "c8e0f0")
    for X in range(3 * f, (W0 - 3) * f):
        x = X / f
        for s in range(f):
            art.put(X, (floor - 1) * f + s, sky if x < 14 else deep)
            art.put(X, floor * f + s, under)
    for x in (6, 7, 18, 19, 30):
        if f == 1:
            art.put(x, floor - 1, glint)
        else:
            art.put(x * f + (x % 2), (floor - 1) * f, glint)
    return art.image()


def cage(fine: int = 1) -> Image.Image:
    """A cage of iron hung from the roof on a rope. `fine` draws it that many times the
    draft's size: bars a pixel wide, each lit on one side."""
    f = fine
    W0, H0 = 3 * TILE, 3 * TILE
    art = Art(W0 * f, H0 * f)
    art.line(W0 // 2 * f, 0, W0 // 2 * f, 8 * f, THATCH[3], 1 if f == 1 else 1.5)
    art.rect(6 * f, 8 * f, (W0 - 6) * f, 11 * f, IRON[3])
    art.rect(6 * f, (H0 - 4) * f, (W0 - 6) * f, (H0 - 1) * f, IRON[2])
    if f > 1:
        art.rect(6 * f, 8 * f, (W0 - 6) * f, 8 * f + 1, IRON[5])
    for x in range(6, W0 - 6, 5):
        art.line(x * f, 11 * f, x * f, (H0 - 4) * f, IRON[4 if x < 20 else 2], 1)
        if f > 1:
            art.line(x * f + 1, 11 * f, x * f + 1, (H0 - 4) * f, IRON[2 if x < 20 else 1], 1)
    art.line((W0 - 7) * f, 11 * f, (W0 - 7) * f, (H0 - 4) * f, IRON[1], 1)
    for _ in range(f):
        art.outline(IRON[0])
    return art.image()


def roots(variant: int, fine: int = 1) -> Image.Image:
    """Roots hanging from a roof under a tree: a few thick ones that fork, thin, hair roots
    at their ends. The picture hangs: its pivot is the roof line. `fine` draws them that
    many times the draft's size: each root a pixel narrower toward its end."""
    r = rng("roots", variant)
    f = fine
    W0, H0 = 3 * TILE, 3 * TILE + 4
    art = Art(W0 * f, H0 * f)

    def grow(x, y, length, width):
        for i in range(length):
            if f == 1:
                art.put(x, y + i, BARK[2] if width > 1 else BARK[1])
                if width > 1:
                    art.put(x + 1, y + i, BARK[1])
            else:
                thick = (2 * f if width > 1 else f) * (1 - 0.5 * i / max(length, 1))
                for u in range(f):
                    for s in range(max(1, int(round(thick)))):
                        art.put(x * f + s, (y + i) * f + u, BARK[3 if s == 0 and width > 1 else 2 if width > 1 else 1])
            if i and i % 5 == 0:
                x += r.choice([-1, 0, 1])
            if width > 1 and i > 6 and r.random() < 0.08:
                grow(x + r.choice([-2, 2]), y + i, length - i, 1)

    for k in range(r.randint(3, 5)):
        grow(r.randint(4, W0 - 6), 0, r.randint(20, H0 - 2), 2 if k < 2 else 1)
    return art.image()


def nugget(variant: int, fine: int = 1) -> Image.Image:
    """A lump of copper ore in the mine floor, green where it has weathered. `fine` draws
    it that many times the draft's size: each lump round, with a glint."""
    r = rng("nugget", variant)
    f = fine
    W0, H0 = TILE + 6, 12 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for _ in range(7):
        cx, cy, rad = r.randint(5, W0 - 6), floor - r.randint(2, 7), r.randint(2, 4)
        for DX in range(-rad * f, rad * f + 1):
            for DY in range(-rad * f, rad * f + 1):
                dx, dy = DX / f, DY / f
                if dx * dx + dy * dy <= rad * rad:
                    k = 5 if dx + dy < -rad * 0.5 else 2 if dx + dy > rad * 0.4 else 3
                    if f > 1 and abs(dx + rad * 0.45) < 0.3 and abs(dy + rad * 0.45) < 0.3:
                        k = len(COPPER) - 1  # a glint on it
                    art.put(cx * f + DX, cy * f + DY, COPPER[k])
    for _ in range(9):
        x, y = r.randint(2, W0 - 3), floor - r.randint(0, 10)
        if art.has(x * f, y * f) and not art.has((x + 1) * f, (y + 1) * f):
            dot(art, x, y, hexes("4aa888")[0], f, hexes("7ad8b8")[0] if f > 1 else None)
    for _ in range(f):
        art.outline(COPPER[0])
    return art.image()


def reflection(fine: int = 1) -> Image.Image:
    """The willow and the bank in the pond, upside down and broken by the ripples: a dark
    green shape under the surface, a few lines of light across it. `fine`: the willow drawn
    at that size, rippled a pixel at a time."""
    f = fine
    base = willow(0, f)
    a = np.array(base.transpose(Image.FLIP_TOP_BOTTOM))[SINK * f :, :]
    h = min(a.shape[0], 3 * TILE * f)
    a = a[:h].copy()
    water = np.array(hexes("1e4a5a")[0], dtype=float)
    out = np.zeros_like(a)
    for Y in range(h):
        y = Y / f
        if (Y // f) % 4 == 3:
            continue
        sway = int(round(1.5 * f * math.sin(y * 0.9)))
        row = np.roll(a[Y], sway, axis=0)
        mask = row[:, 3] > 0
        mixed = (row[:, :3] * 0.55 + water * 0.45).astype(np.uint8)
        out[Y, mask, :3] = mixed[mask]
        out[Y, mask, 3] = int(200 * (1 - Y / h))
    return Image.fromarray(out, "RGBA")


def depth(width_tiles: int, rows: int, fine: int = 1) -> Image.Image:
    """The pond's depth: darker the deeper, in an ordered dither. `fine`: the dither at that
    many times the draft's size, so it is finer."""
    f = fine
    W, H = width_tiles * TILE * f, rows * TILE * f
    art = Art(W, H)
    deep = hexes("0c2a3a")[0]
    for Y in range(4 * f, H):
        t = (Y / f - 4) / (H / f - 4)
        for X in range(W):
            if t * 16 > BAYER_4[Y % 4][X % 4] + 0.5:
                art.put(X, Y, deep, int(40 + 110 * t))
    return art.image()


def gate_glow(fine: int = 1) -> Image.Image:
    """The glow of what is below in the Gate's opening by itself, shown unlit over it.
    `fine`: over the Gate drawn at that size, pixel for pixel."""
    base = np.array(gateway(fine))
    art = Art(base.shape[1], base.shape[0])
    reds = {hexes("2a0e0a")[0], hexes("4a160c")[0]}
    for y in range(base.shape[0]):
        for x in range(base.shape[1]):
            if tuple(base[y, x, :3]) in reds and base[y, x, 3]:
                art.put(x, y, hexes("ff7a2a")[0] if tuple(base[y, x, :3]) == hexes("4a160c")[0] else hexes("a83a14")[0], 200)
    return art.image()


def rays(fine: int = 1) -> Image.Image:
    """Shafts of low sun through the wood's edge, at dawn and at sunset (TilesV2Life fades
    them with the hour). `fine`: the same shafts at that many times the draft's size, their
    streaks a pixel apart."""
    f = fine
    W0, H0 = 20 * TILE, 12 * TILE
    art = Art(W0 * f, H0 * f)
    warm = hexes("fff0c0")[0]
    r = rng("rays")
    for k in range(6):
        x0 = r.randint(20, W0 - 60)
        wide = r.randint(6, 16)
        for Y in range(H0 * f):
            y = Y / f
            x = x0 + y * 0.45
            fade = 1 - y / H0
            for DX in range(wide * f):
                dx = DX / f
                if (int(dx) + int(y)) % 3 if f == 1 else (DX + Y) % 3:
                    art.put(int(x * f + DX), Y, warm, int(70 * fade * (1 - abs(dx - wide / 2) / wide)))
    return art.image()


def front(variant: int, sway: int = 0, fine: int = 1) -> Image.Image:
    """A dark shape close to the eye, passed in front of: tall grass, a fern, a thistle, the
    end of a bough. Near black, so it frames the view and never reads as something to stand
    on. `fine` draws it that many times the draft's size: blades that taper, a rim of light
    a pixel wide."""
    r = rng("front", variant)
    f = fine
    W0, H0 = 4 * TILE, 3 * TILE + 2 * TILE
    W, H = W0 * f, H0 * f
    art = Art(W, H)
    floor = (H0 - 2 * TILE) * f
    ink, rim = hexes("0a120e", "1a2a1e")
    if variant % 2 == 0:
        for _ in range(16):
            x, tall, lean = r.randint(2, W0 - 3), r.randint(18, 44), r.uniform(-0.5, 0.5)
            for HH in range(-2 * TILE * f, tall * f):
                t = max(HH / f, 0) / tall
                if f == 1:
                    xx = int(x + lean * t * t * 14 + sway * t * t)
                    art.put(xx, floor - HH, ink)
                    if t < 0.7:
                        art.put(xx + 1, floor - HH, ink)
                    continue
                xx = (x + lean * t * t * 14 + sway * t * t) * f
                wide = max(1, round((1 - t) * 3.2 * f / 2 + 0.5))  # a blade that tapers to its point
                for d in range(wide):
                    art.put(xx + d, floor - HH, ink)
    else:
        cx = W0 // 2
        for side in (-1, 1):
            for I in range(30 * f):
                i = I / f
                t = i / 30
                x, y = cx + side * i, floor / f - (36 * (1 - t * t) if f > 1 else int(36 * (1 - t * t))) + (sway * t * t * 0.5 if f > 1 else int(round(sway * t * t * 0.5)))
                for K in range(-2 * f, 3 * f if f == 1 else int((3 - t * 1.5) * f)):
                    art.put(x * f, y * f + K, ink)
                if I % (3 * f) == 0:
                    art.line(x * f, y * f, (x + side * 3) * f, (y + 6) * f, ink, 1 if f == 1 else 1.5)
        art.rect((cx - 2) * f, floor - 36 * f, (cx + 2) * f, H, ink)
    for y in range(H):
        for x in range(W - 1):
            if art.has(x, y) and not art.has(x - 1, y) and y < floor:
                art.put(x, y, rim)
    return art.image()


def windows(fine: int = 1) -> Image.Image:
    """The windows of the far villages and the castle, lit: a layer of its own that comes
    on at night (the hills themselves are tinted dark then): drawn where ridge_hills put their
    houses, at the hills' own size. `fine`: over the hills drawn at that many times the
    draft's size."""
    f = fine
    hills = np.array(ridge_hills(fine=f))
    a = np.zeros_like(hills)
    window = hexes("4c7080")[0]
    lit = hexes("ffd27a")[0]
    for y in range(hills.shape[0]):
        for x in range(hills.shape[1]):
            if hills[y, x, 3] and tuple(hills[y, x, :3]) == window:
                a[y, x, :3] = lit
                a[y, x, 3] = 255
    r = rng("windows")
    # A few more, in the roofs of the villages.
    width = hills.shape[1]
    for church in (150, 1330):
        for _ in range(5):
            x = church + r.randint(-26, 50)
            col = np.nonzero(hills[:, (x * f) % width, 3])[0]
            if len(col):
                y = col[0] + r.randint(3, 6) * (f if f > 1 else 1)
                for s in range(f):
                    for u in range(f):
                        if y + u < hills.shape[0]:
                            a[y + u, (x * f + s) % width, :3] = lit
                            a[y + u, (x * f + s) % width, 3] = 255
    return Image.fromarray(a, "RGBA")


# --- The third round: the graveyard, animals, a fire by the start, a boat, lights in the
# damp, what sways in the wind --------------------------------------------------------------

IRONWORK = ramp_of("0e0e12 1e2026 2e3038 44474f 5e626b 7c8088")
WOOL = ramp_of("2a2622 9a948a b8b2a6 d2ccc0 e6e0d4 f6f2ea")
SHROOM = ramp_of("0c2a26 1a6a5e 2aa08a 54d8bc 9cf6e2 e2fff8")


def shine(fine: int = 1) -> Image.Image:
    """Lines of light on the pond's surface, for the willow's shape to lie under. `fine`:
    each line a pixel high at that many times the draft's size, so they are finer."""
    f = fine
    W0, H0 = 14 * TILE, 3 * TILE
    art = Art(W0 * f, H0 * f)
    r = rng("shine")
    for y in range(4, H0, 3):
        t = (y - 4) / (H0 - 4)
        x = r.randint(0, 20)
        while x < W0 - 4:
            length = r.randint(4, 18)
            for DX in range(length * f):
                art.put(x * f + DX, y * f, hexes("cfe8f6")[0], int(170 * (1 - t) ** 1.5))
            x += length + r.randint(8, 30)
    return art.image()


def crypt(fine: int = 1) -> Image.Image:
    """A family's crypt in the graveyard: a little house of stone with a pitched roof, an
    iron door, a cross on the gable; lichen on it. `fine` draws it that many times the
    draft's size: mortar a pixel wide, the door's iron in a finer lattice."""
    r = rng("crypt")
    f = fine
    W0, H0 = 3 * TILE + 8, 3 * TILE + 10 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    masonry(art, 4, floor - 34, W0 - 4, floor + 4, STONEWORK, r, course=5, fine=f)
    for Y in range((floor - 50) * f, (floor - 34) * f):
        y = Y / f
        t = (y - (floor - 50)) / 16
        half = 4 + (W0 / 2 - 2) * t
        for X in range(int((W0 / 2 - half) * f), int((W0 / 2 + half) * f)):
            art.put(X, Y, STONEWORK[5] if X < W0 / 2 * f else STONEWORK[2])
    art.rect((W0 // 2 - 1) * f, (floor - 60) * f, (W0 // 2 + 1) * f, (floor - 49) * f, STONEWORK[5])
    art.rect((W0 // 2 - 4) * f, (floor - 57) * f, (W0 // 2 + 4) * f, (floor - 55) * f, STONEWORK[5])
    for Y in range((floor - 24) * f, floor * f):
        for X in range((W0 // 2 - 6) * f, (W0 // 2 + 6) * f):
            art.put(X, Y, IRONWORK[3] if (X + Y) % (4 if f == 1 else 5) else IRONWORK[1])
    for _ in range(14):
        x, y = r.randint(5, W0 - 6), r.randint(floor - 34, floor)
        if art.has(x * f, y * f):
            dot(art, x, y, hexes("7a8a4a")[0], f, hexes("9aaa5a")[0] if f > 1 else None)
    for _ in range(f):
        art.outline(STONEWORK[0])
    return art.image()


def iron_fence(variant: int, fine: int = 1) -> Image.Image:
    """Three tiles of the graveyard's iron railing, spear-headed, one bar bent. `fine`
    draws it that many times the draft's size: round bars lit on one side, spear heads
    with a point, rails with an edge."""
    f = fine
    W0, H0 = 3 * TILE, 20 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    for x in range(2, W0 - 1, 4):
        bend = 2 if variant and x == 18 else 0
        if f == 1:
            art.line(x, floor + 3, x + bend, floor - 16, IRONWORK[3], 1)
            art.put(x + bend, floor - 17, IRONWORK[5])
            art.put(x + bend - 1, floor - 16, IRONWORK[4])
            art.put(x + bend + 1, floor - 16, IRONWORK[4])
            continue
        art.line(x * f, (floor + 3) * f, (x + bend) * f, (floor - 16) * f, IRONWORK[4], 1)
        art.line(x * f + 1, (floor + 3) * f, (x + bend) * f + 1, (floor - 16) * f, IRONWORK[2], 1)
        top = (floor - 16) * f
        for dy, half in ((-4, 0), (-3, 1), (-2, 2), (-1, 2), (0, 1)):  # the spear head
            for d in range(-half, half + 2):
                art.put((x + bend) * f + d, top + dy, IRONWORK[5] if d <= 0 else IRONWORK[3])
    for y in (floor - 13, floor - 3):
        if f == 1:
            art.line(0, y, W0 - 1, y, IRONWORK[2], 1)
            continue
        for X in range(W0 * f):
            art.put(X, y * f, IRONWORK[4])
            art.put(X, y * f + 1, IRONWORK[2])
    return art.image()


def crow(variant: int, fine: int = 1) -> Image.Image:
    """A crow on a headstone: a black body, a head turned to one side, a pale eye, a beak.
    `fine` draws it that many times the draft's size: a round body, a feathered edge."""
    f = fine
    W0, H0 = TILE, TILE
    art = Art(W0 * f, H0 * f)
    body = hexes("14141a", "2a2a36", "4a4a5a")
    side = 1 if variant % 2 else -1
    for Y in range(8 * f, 14 * f):
        y = Y / f + (0.5 / f - 0.5 if f > 1 else 0)
        for X in range(5 * f, 11 * f):
            x = X / f + (0.5 / f - 0.5 if f > 1 else 0)
            if (x - 8) ** 2 + ((y - 11) * 1.4) ** 2 <= 9:
                art.put(X, Y, body[1] if y > 10 else body[2])
    for Y in range(5 * f, 9 * f):
        for X in range((7 + side) * f, (10 + side) * f):
            art.put(X, Y, body[1])
    dot(art, 9 + 2 * side if side > 0 else 6 + side, 6, hexes("8a7a4a")[0], f)
    dot(art, 8 + side, 6, hexes("e0d0a0")[0], f, (255, 255, 255) if f > 1 else None)
    art.line((8 - side * 3) * f, 12 * f, (8 - side * 6) * f, 14 * f, body[0], f)
    dot(art, 7, 15, body[0], f)
    dot(art, 9, 15, body[0], f)
    return art.image()


def sheep(variant: int, fine: int = 1) -> Image.Image:
    """A sheep grazing: a round body of wool, a dark head down to the grass, four legs.
    `fine` draws it that many times the draft's size: the wool in curls."""
    f = fine
    W0, H0 = 2 * TILE, 14 + SINK
    art = Art(W0 * f, H0 * f)
    floor = H0 - SINK
    side = 1 if variant % 2 else -1
    cx = W0 // 2
    for Y in range((floor - 13) * f, (floor - 3) * f):
        y = Y / f
        for X in range((cx - 9) * f, (cx + 9) * f):
            x = X / f
            if ((x - cx) / 9) ** 2 + ((y - (floor - 8)) / 5.2) ** 2 <= 1:
                curl = (X + Y) % 3 if f == 1 else (X // 2 + Y // 2 + (X * Y) % 3) % 3
                k = 5 if x < cx - 3 and y < floor - 9 else 2 if y > floor - 5 else 4 if curl else 3
                art.put(X, Y, WOOL[k])
    hx = cx + side * 9
    for Y in range((floor - 9) * f, (floor - 3) * f):
        y = Y / f
        for X in range((hx - 2) * f, (hx + 3) * f):
            art.put(X, Y, WOOL[0] if y > floor - 8 else WOOL[1])
    if f > 1:
        art.put((hx + side) * f, (floor - 7) * f, (230, 230, 210))  # an eye
    for lx in (cx - 5, cx - 2, cx + 3, cx + 6):
        art.rect(lx * f, (floor - 3) * f, (lx + 1) * f, (floor + 1) * f, WOOL[0])
    for _ in range(f):
        art.outline(hexes("1e1a16")[0])
    return art.image()


def cat(fine: int = 1) -> Image.Image:
    """A cat sitting on a fence post, its tail hanging down. `fine` draws it that many
    times the draft's size: a round body, pointed ears, a tail that curls."""
    f = fine
    W0, H0 = TILE, TILE + 6
    art = Art(W0 * f, H0 * f)
    fur = hexes("2a1e16", "5a3a22", "8a5a32", "b07a46")
    for Y in range(6 * f, 14 * f):
        y = Y / f + (0.5 / f - 0.5 if f > 1 else 0)
        for X in range(4 * f, 12 * f):
            x = X / f + (0.5 / f - 0.5 if f > 1 else 0)
            if ((x - 8) / 4) ** 2 + ((y - 10) / 4) ** 2 <= 1:
                art.put(X, Y, fur[2] if x < 8 else fur[1])
    for Y in range(2 * f, 7 * f):
        for X in range(4 * f, 9 * f):
            x = X / f
            art.put(X, Y, fur[3] if x < 6 else fur[2])
    if f == 1:
        art.put(4, 1, fur[2])
        art.put(8, 1, fur[2])
    else:
        for ex in (4, 8):  # its ears, to a point
            for dy in range(f + 1):
                for dx in range(f - dy // 2 if f > 1 else 1):
                    art.put(ex * f + dx + (dy // 2 if ex == 8 else 0), 2 * f - 1 - dy, fur[2])
    eye = hexes("d8e060")[0]
    for ex in (5, 7):
        dot(art, ex, 4, eye, f)
        if f > 1:
            art.put(ex * f + 1, 4 * f + 1, fur[0])  # its pupil
    for Y in range(13 * f, H0 * f):
        y = Y / f
        if f == 1:
            art.put(11 + (Y // 4) % 2, Y, fur[1])
        else:
            art.put(int(round((11.5 + 0.5 * math.sin(y * math.pi / 4)) * f)), Y, fur[1])
            art.put(int(round((11.5 + 0.5 * math.sin(y * math.pi / 4)) * f)) + 1, Y, fur[1])
    for _ in range(f):
        art.outline(fur[0])
    return art.image()


BIRD_FRAMES = 2


def bird(frame: int, fine: int = 1) -> Image.Image:
    """A bird far off, a stroke of two wings: up, or down. `fine` draws the stroke that
    many times the draft's size, a pixel wide and curved."""
    f = fine
    art = Art(7 * f, 4 * f)
    ink = hexes("2a2e3a")[0]
    points = ((0, 0), (1, 1), (2, 2), (3, 3), (4, 2), (5, 1), (6, 0)) if frame % 2 == 0 else ((0, 2), (1, 1), (2, 1), (3, 2), (4, 1), (5, 1), (6, 2))
    if f == 1:
        for x, y in points:
            art.put(x, y, ink)
        return art.image()
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        art.line(x0 * f + 0.5, y0 * f + 0.5, x1 * f + 0.5, y1 * f + 0.5, ink, 1.5)
    return art.image()


def boat(fine: int = 1) -> Image.Image:
    """A rowing boat pulled up on the bank, an oar in it. `fine` draws it that many times
    the draft's size: a hull that curves, its strakes a pixel apart."""
    f = fine
    W0, H0 = 3 * TILE, 12
    art = Art(W0 * f, H0 * f)
    for Y in range(3 * f, 12 * f):
        y = Y / f
        inset = (int(y) - 3) // 2 if f == 1 else (y - 3) ** 1.3 / 2.4
        for X in range(int((2 + inset) * f), int((W0 - 2 - inset) * f)):
            k = 4 if (int(y) == 3 if f == 1 else Y < 3 * f + 1) else 3 if y < 7 else 2
            if f > 1 and Y % (3 * f) == 0 and Y > 3 * f:
                k -= 1  # a strake
            art.put(X, Y, TIMBER[k])
    for X in range(6 * f, (W0 - 6) * f):
        for s in range(f):
            art.put(X, 5 * f + s, hexes("3a5470")[0])
    art.line(10 * f, 0, 30 * f, 6 * f, TIMBER[4], f)
    for _ in range(f):
        art.outline(TIMBER[0])
    return art.image()


def campfire_frame(frame: int) -> np.ndarray:
    """A campfire: a ring of stones, crossed logs, a flame that changes its shape each frame."""
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)

    def put(x, y, colour):
        if 0 <= x < TILE and 0 <= y < TILE:
            a[y, x, :3] = colour
            a[y, x, 3] = 255

    stones = hexes("3e3a40", "6a6470", "8e8894")
    for x in (1, 3, 12, 14):
        put(x, 15, stones[1])
        put(x, 14, stones[2] if x < 8 else stones[1])
    for x in range(2, 14):
        put(x, 15, stones[0] if x % 3 == 0 else stones[1])
    for i in range(10):
        put(3 + i, 13 - i // 4, TIMBER[3])
        put(12 - i, 13 - i // 4, TIMBER[2])
    flame = [hexes("ff6a1e")[0], hexes("ffb43a")[0], hexes("fff0a0")[0]]
    shapes = [
        [(7, 12, 5), (8, 11, 6), (6, 10, 4), (9, 9, 4), (7, 7, 3)],
        [(7, 12, 5), (8, 11, 5), (7, 10, 5), (8, 8, 3), (6, 6, 2)],
        [(8, 12, 5), (7, 11, 6), (8, 9, 4), (7, 8, 3), (9, 6, 2)],
    ][frame % 3]
    for x, y, h in shapes:
        for k in range(h):
            put(x, y - k, flame[0] if k > h - 2 else flame[1] if k > 1 else flame[2])
    return a


def glowshroom_frame(frame: int) -> np.ndarray:
    """Mushrooms that give a cold light in the damp: three caps on pale stems, the glow
    swelling and fading over the frames."""
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    lift = (0, 1, 2)[frame % 3]

    def put(x, y, colour):
        if 0 <= x < TILE and 0 <= y < TILE:
            a[y, x, :3] = colour
            a[y, x, 3] = 255

    for cx, tall, half in ((4, 5, 2), (9, 8, 3), (13, 4, 1)):
        for h in range(tall):
            put(cx, 15 - h, SHROOM[1])
        for x in range(cx - half, cx + half + 1):
            put(x, 15 - tall, SHROOM[2 + lift])
            put(x, 14 - tall, SHROOM[3 + lift] if abs(x - cx) < half else SHROOM[2 + lift])
        put(cx, 13 - tall, SHROOM[3 + lift])
    return a


# --- What the generator writes --------------------------------------------------------

OAKS, PINES, APPLES, DEADS, SHRUBS = 4, 3, 2, 2, 3
ORDER_BEHIND = -287  # in front of the walls and their shade, behind the trees of the foliage, the decor and the ground
ORDER_TREES = -288  # the trees: behind what people built, so a crown never covers a house
ORDER_FIRS = -289  # the firs: behind the other trees, so where two crowns cross the order is fixed
ORDER_GLOW = -286  # the lit window over its cottage
ORDER_TRACK = -99  # rails and carts: in front of the ground they lie on (it is under them), behind the ledges
ORDER_WATER = 27  # on the water's surface, which is drawn in front of the hero
ORDER_RAYS = -284  # shafts of low sun: in front of the trees, behind the decor and the ground
ORDER_FRONT = 40  # what passes close to the eye: in front of everything but the motes in the air


def _picture(key: str, order: int = ORDER_BEHIND, lit: bool = True) -> dict:
    return {"key": key, "kind": "picture", "scroll": 1.0, "scrollUp": 1.0, "rise": 0, "drift": 0.0, "order": order, "lit": lit, "tinted": False}


def _plan() -> list[tuple[str, object, int, bool, int]]:
    """Every picture of the Earth: (key, what draws it, sorting order, lit, sink). The sink
    is how many px of it lie under its floor line, behind the ground: its sprite's pivot
    is there, so the map marks the cell of air the picture stands in and its roots, its
    plinth or the foot of its post go down into the ground under that cell."""
    plan = []

    def add(key, paint, order=ORDER_BEHIND, lit=True, sink=SINK):
        plan.append((key, paint, order, lit, sink))

    for v in range(OAKS):
        add(f"earth_oak_{v}", lambda v=v: oak(v, AUTUMN if v == 3 else OAK, turn=0 if v == 3 else 6), ORDER_TREES)
    for v in range(PINES):
        add(f"earth_pine_{v}", lambda v=v: pine(v), ORDER_FIRS)
    for v in range(APPLES):
        add(f"earth_apple_{v}", lambda v=v: oak(10 + v, APPLE, tall=96 + 8 * v, fruit=hexes("c8322c", "f07a5a"), turn=2), ORDER_TREES)
    for v in range(DEADS):
        add(f"earth_dead_tree_{v}", lambda v=v: dead_tree(v), ORDER_TREES)
    for v in range(SHRUBS):
        add(f"earth_shrub_{v}", lambda v=v: shrub(v))
    add("earth_willow", lambda: willow(0), ORDER_TREES)
    add("earth_cottage_ruin", lambda: cottage(0), sink=PLINTH)
    add("earth_cottage", lambda: cottage(1), sink=PLINTH)
    add("earth_cottage_glow", cottage_glow, ORDER_GLOW, lit=False, sink=PLINTH)
    add("earth_chapel", chapel, sink=PLINTH)
    add("earth_well", well)
    add("earth_windmill", mill_tower, sink=2 * TILE)
    for v in range(3):
        add(f"earth_fence_{v}", lambda v=v: fence(v))
    add("earth_cart", cart)
    for v in range(2):
        add(f"earth_haystack_{v}", lambda v=v: haystack(v))
    add("earth_scarecrow", scarecrow)
    for v in range(4):
        add(f"earth_grave_{v}", lambda v=v: grave(v))
    add("earth_gateway", gateway, sink=PLINTH)
    add("earth_gate_arch", gate_arch)
    add("earth_pithead", pithead)
    add("earth_winch_hut", winch_hut)
    add("earth_colonnade", colonnade_ruin, sink=PLINTH)
    for v in range(3):
        add(f"earth_reeds_{v}", lambda v=v: reeds(v))
        for f, lean in ((1, 2), (2, 4)):
            add(f"earth_reeds_{v}_s{f}", lambda v=v, lean=lean: reeds(v, lean))
    for v in range(2):
        add(f"earth_lilies_{v}", lambda v=v: lilies(v), ORDER_WATER + 2, sink=0)
    add("earth_jetty", jetty)
    for rows in POST_HEIGHTS:
        add(f"earth_post_{rows}", lambda rows=rows: mine_post(rows))
    add("earth_rail_mid", lambda: rail("mid"), ORDER_TRACK)
    add("earth_rail_end", lambda: rail("end"), ORDER_TRACK)
    add("earth_rail_start", lambda: rail("end").transpose(Image.FLIP_LEFT_RIGHT), ORDER_TRACK)
    for v in range(2):
        add(f"earth_minecart_{v}", lambda v=v: mine_cart(v), ORDER_TRACK)
    for v in range(3):
        add(f"earth_crates_{v}", lambda v=v: crates(v))
    # The second round.
    for v in range(2):
        add(f"earth_birch_{v}", lambda v=v: birch(v), ORDER_TREES)
    add("earth_giant_oak", giant_oak, ORDER_TREES)
    add("earth_log", log)
    for v in range(2):
        add(f"earth_stump_{v}", lambda v=v: stump(v))
    add("earth_toadstools", toadstools)
    add("earth_washing", washing_line)
    add("earth_garden", garden)
    add("earth_bench", bench)
    add("earth_barrel", barrel)
    add("earth_kennel", kennel)
    for f in range(SAIL_FRAMES):
        add(f"earth_sails_{f}", lambda f=f: sails(f), ORDER_GLOW, sink=86)
    for v in range(2):
        add(f"earth_rubble_{v}", lambda v=v: rubble(v))
    add("earth_puddle", puddle, ORDER_TRACK)
    add("earth_cage", cage, sink=0)
    for v in range(3):
        add(f"earth_roots_{v}", lambda v=v: roots(v), sink=3 * TILE + 4 - 20)
    for v in range(2):
        add(f"earth_nugget_{v}", lambda v=v: nugget(v))
    add("earth_reflection", reflection, ORDER_WATER + 1, sink=3 * TILE - 13)
    add("earth_shine", shine, ORDER_WATER + 1, sink=3 * TILE - 16)
    add("earth_depth", lambda: depth(14, 3), ORDER_WATER, sink=3 * TILE - 16)
    add("earth_gate_glow", gate_glow, ORDER_GLOW, lit=False, sink=PLINTH)
    add("earth_rays", rays, ORDER_RAYS, lit=False, sink=0)
    for v in range(4):
        add(f"earth_front_{v}", lambda v=v: front(v), ORDER_FRONT, lit=False, sink=2 * TILE)
        for f, lean in ((1, 2), (2, 4)):
            add(f"earth_front_{v}_s{f}", lambda v=v, lean=lean: front(v, lean), ORDER_FRONT, lit=False, sink=2 * TILE)
    # The third round.
    add("earth_crypt", crypt)
    for v in range(2):
        add(f"earth_iron_fence_{v}", lambda v=v: iron_fence(v))
        # A crow on a headstone: the picture is as tall as the stone, the bird on its top.
        add(f"earth_crow_{v}", lambda v=v: padded(crow(v), 22), sink=0)
        add(f"earth_sheep_{v}", lambda v=v: sheep(v))
    add("earth_cat", lambda: padded(cat(), 14), sink=0)
    for f in range(BIRD_FRAMES):
        add(f"earth_bird_{f}", lambda f=f: bird(f), ORDER_FRONT - 1000, lit=False, sink=0)
    add("earth_boat", boat, ORDER_WATER + 2, sink=10)
    return plan


PLAN = _plan()
PICTURE_KEYS = [key for key, *_ in PLAN]
ENTRIES = [_picture(key, order, lit) for key, _, order, lit, _ in PLAN]
SINK_OF = {key: sink for key, *_, sink in PLAN}
# What is drawn at the size that ships itself (tv2_base.DENSITY), by key; the rest ships as
# its draft through Scale2x. A picture here is the draft's size times DENSITY exactly.
FINE = {}
# The trees.
for _v in range(OAKS):
    FINE[f"earth_oak_{_v}"] = lambda v=_v: oak(v, AUTUMN if v == 3 else OAK, turn=0 if v == 3 else 6, fine=DENSITY)
for _v in range(PINES):
    FINE[f"earth_pine_{_v}"] = lambda v=_v: pine(v, DENSITY)
for _v in range(APPLES):
    FINE[f"earth_apple_{_v}"] = lambda v=_v: oak(10 + v, APPLE, tall=96 + 8 * v, fruit=hexes("c8322c", "f07a5a"), turn=2, fine=DENSITY)
for _v in range(2):
    FINE[f"earth_birch_{_v}"] = lambda v=_v: birch(v, DENSITY)
FINE["earth_willow"] = lambda: willow(0, DENSITY)
FINE["earth_giant_oak"] = lambda: giant_oak(DENSITY)
# What passes before the eye, the reeds by the water, the mine's timber, the mill's sails.
for _v in range(4):
    FINE[f"earth_front_{_v}"] = lambda v=_v: front(v, fine=DENSITY)
    for _s, _lean in ((1, 2), (2, 4)):
        FINE[f"earth_front_{_v}_s{_s}"] = lambda a=(_v, _lean): front(*a, fine=DENSITY)
for _v in range(3):
    FINE[f"earth_reeds_{_v}"] = lambda v=_v: reeds(v, fine=DENSITY)
    for _s, _lean in ((1, 2), (2, 4)):
        FINE[f"earth_reeds_{_v}_s{_s}"] = lambda a=(_v, _lean): reeds(*a, fine=DENSITY)
for _rows in POST_HEIGHTS:
    FINE[f"earth_post_{_rows}"] = lambda n=_rows: mine_post(n, DENSITY)
for _frame in range(SAIL_FRAMES):
    FINE[f"earth_sails_{_frame}"] = lambda s=_frame: sails(s, DENSITY)
# Graves, the mine's track and its stores, roots, lilies, stumps, rubble, ore; crows, sheep, birds.
for _v in range(4):
    FINE[f"earth_grave_{_v}"] = lambda v=_v: grave(v, DENSITY)
FINE["earth_rail_mid"] = lambda: rail("mid", DENSITY)
FINE["earth_rail_end"] = lambda: rail("end", DENSITY)
FINE["earth_rail_start"] = lambda: rail("end", DENSITY).transpose(Image.FLIP_LEFT_RIGHT)
for _v in range(3):
    FINE[f"earth_crates_{_v}"] = lambda v=_v: crates(v, DENSITY)
    FINE[f"earth_roots_{_v}"] = lambda v=_v: roots(v, DENSITY)
for _v in range(2):
    FINE[f"earth_lilies_{_v}"] = lambda v=_v: lilies(v, DENSITY)
    FINE[f"earth_stump_{_v}"] = lambda v=_v: stump(v, DENSITY)
    FINE[f"earth_rubble_{_v}"] = lambda v=_v: rubble(v, DENSITY)
    FINE[f"earth_nugget_{_v}"] = lambda v=_v: nugget(v, DENSITY)
    FINE[f"earth_crow_{_v}"] = lambda v=_v: padded(crow(v, DENSITY), 22 * DENSITY)
    FINE[f"earth_sheep_{_v}"] = lambda v=_v: sheep(v, DENSITY)
for _frame in range(BIRD_FRAMES):
    FINE[f"earth_bird_{_frame}"] = lambda s=_frame: bird(s, DENSITY)
# What a village and its fields are dressed with.
for _key, _paint in (("log", log), ("toadstools", toadstools), ("washing", washing_line), ("garden", garden), ("bench", bench), ("barrel", barrel), ("kennel", kennel), ("puddle", puddle), ("cage", cage), ("boat", boat)):
    FINE[f"earth_{_key}"] = lambda p=_paint: p(DENSITY)
FINE["earth_cat"] = lambda: padded(cat(DENSITY), 14 * DENSITY)
# The village's houses, its chapel and mill, the Gate above and the Gate below, the mine's head.
FINE["earth_cottage_ruin"] = lambda: cottage(0, DENSITY)
FINE["earth_cottage"] = lambda: cottage(1, DENSITY)
for _key, _paint in (("chapel", chapel), ("well", well), ("windmill", mill_tower), ("cart", cart), ("scarecrow", scarecrow), ("gateway", gateway), ("gate_arch", gate_arch), ("pithead", pithead), ("winch_hut", winch_hut), ("colonnade", colonnade_ruin), ("jetty", jetty), ("crypt", crypt)):
    FINE[f"earth_{_key}"] = lambda p=_paint: p(fine=DENSITY)
for _v in range(2):
    FINE[f"earth_minecart_{_v}"] = lambda v=_v: mine_cart(v, DENSITY)
# The glow of the kept cottage and of the Gate, over them as they are drawn at 32; the pond.
FINE["earth_cottage_glow"] = lambda: cottage_glow(DENSITY)
FINE["earth_gate_glow"] = lambda: gate_glow(DENSITY)
FINE["earth_reflection"] = lambda: reflection(DENSITY)
FINE["earth_shine"] = lambda: shine(DENSITY)
FINE["earth_depth"] = lambda: depth(14, 3, DENSITY)
FINE["earth_rays"] = lambda: rays(DENSITY)
# What stands far off behind the surface: the mountains, the hills and their lit windows, the woods.
FINE["backdrop_far"] = lambda: ridge_mountains(fine=DENSITY)
FINE["backdrop_earth_hills"] = lambda: ridge_hills(fine=DENSITY)
FINE["backdrop_earth_windows"] = lambda: windows(DENSITY)
FINE["backdrop_mid"] = lambda: ridge_mid(DENSITY)
FINE["backdrop_near"] = lambda: ridge_near(DENSITY)
for _v in range(SHRUBS):
    FINE[f"earth_shrub_{_v}"] = lambda v=_v: shrub(v, DENSITY)
for _v in range(DEADS):
    FINE[f"earth_dead_tree_{_v}"] = lambda v=_v: dead_tree(v, DENSITY)
for _v in range(3):
    FINE[f"earth_fence_{_v}"] = lambda v=_v: fence(v, DENSITY)
for _v in range(2):
    FINE[f"earth_haystack_{_v}"] = lambda v=_v: haystack(v, DENSITY)
    FINE[f"earth_iron_fence_{_v}"] = lambda v=_v: iron_fence(v, DENSITY)
_DRAWN: dict[str, Image.Image] = {}


def pictures() -> dict[str, Image.Image]:
    """Every picture of the Earth, drawn (once), by key."""
    if not _DRAWN:
        for key, paint, *_ in PLAN:
            _DRAWN[key] = paint()
    return dict(_DRAWN)


def padded(image: Image.Image, under: int) -> Image.Image:
    """A picture with `under` px of nothing beneath it: something that sits on the top of
    what stands in its cell (a crow on a headstone, a cat on a post)."""
    out = Image.new("RGBA", (image.width, image.height + under), (0, 0, 0, 0))
    out.paste(image, (0, 0))
    return out


def pivot_of(key: str) -> tuple[float, float]:
    """A picture's pivot (x, y from the bottom, 0 .. 1): the middle of its floor line."""
    height = pictures()[key].height
    return (0.5, round(SINK_OF[key] / height, 6))


def size_of(key: str) -> tuple[int, int]:
    return pictures()[key].size


# The Earth's ridges: (key, scroll, order, painter). Back to front; the three the world had
# keep their keys and orders, two are new between them.
RIDGES = [
    ("backdrop_far", 0.1, -990, ridge_mountains),
    ("backdrop_earth_hills", 0.17, -985, ridge_hills),
    ("backdrop_mid", 0.25, -980, ridge_mid),
    ("backdrop_near", 0.45, -970, ridge_near),
]
NEW_RIDGES = [
    {"key": "backdrop_earth_hills", "kind": "ridge", "scroll": 0.17, "order": -985, "lit": False},
    # The lights of the far villages: placed as the hills are (a layer of heaven at their
    # scroll, standing 2 tiles over the surface line) but not tinted, so they can shine at
    # night; TilesV2Life shows them only then.
    {"key": "backdrop_earth_windows", "kind": "heaven", "scroll": 0.17, "scrollUp": 1.0, "rise": 2, "drift": 0.0, "order": -984, "lit": False, "tinted": False},
]
LAYERS = {"backdrop_earth_windows": lambda: windows()}
