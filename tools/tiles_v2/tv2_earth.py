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


def oak(variant: int, leaves=OAK, tall: int | None = None, fruit=None, turn: int = 6) -> Image.Image:
    """A broad tree of a few bundles on branches that part from the trunk well over the
    hero's head; asymmetric, with a low bundle on one side. `turn` is one more number in
    what its shape is drawn from, so trees in other leaves are other trees: always the
    same number for the same picture, or the picture would change each time it is drawn."""
    r = rng("oak", variant, turn)
    tall = tall or r.randint(118, 160)
    wide = int(tall * r.uniform(0.62, 0.78))
    W, H = wide + 16, tall + SINK + 4
    art = Art(W, H)
    foot_x, foot_y = W // 2 + r.randint(-4, 4), H - SINK + 6
    split = int(tall * r.uniform(0.38, 0.5))  # the height the branches part at
    crown_cy = H - SINK - tall + int(tall * 0.36)
    trunk(art, foot_x, foot_y, split + 12, r.randint(9, 12), 6, BARK, r)
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
        branch(art, foot_x, top_y + 8, bx, by + 6, 4 if abs(bx - foot_x) > wide * 0.2 else 5, BARK)
    # Back to front: the lowest and darkest first, so the upper ones lie over them.
    for k, (bx, by, rx, ry) in enumerate(sorted(bundles, key=lambda b: -b[1])):
        bundle(art, bx, by, rx, ry, leaves, r, (variant, k))
    if fruit:
        for _ in range(16):
            x, y = r.randint(8, W - 8), r.randint(H - SINK - tall + 10, H - SINK - split)
            if art.has(x, y) and art.has(x + 1, y + 1):
                art.put(x, y, fruit[1])
                art.put(x + 1, y, fruit[0])
                art.put(x, y + 1, fruit[0])
                art.put(x + 1, y + 1, fruit[0])
    art.outline(leaves[0])
    return art.image()


def pine(variant: int) -> Image.Image:
    """A fir: tiers of drooping boughs on a straight trunk, a sharp top, lit on the left."""
    r = rng("pine", variant)
    tall = r.randint(130, 190)
    wide = int(tall * r.uniform(0.34, 0.42))
    W, H = wide + 12, tall + SINK + 4
    art = Art(W, H)
    cx, foot = W // 2, H - SINK + 6
    trunk(art, cx, foot, tall - 10, 7, 3, BARK, r, roots=True)
    tiers = r.randint(7, 9)
    top = H - SINK - tall
    for i in range(tiers):
        t = (i + 1) / tiers
        y_low = top + int(tall * 0.12 + (tall * 0.8) * t)
        half = 4 + (wide / 2 - 4) * t ** 0.85
        height = int(tall * 0.8 / tiers * 1.7)
        for y in range(y_low - height, y_low + 1):
            s = (y - (y_low - height)) / height
            span = half * s ** 0.9
            for x in range(int(cx - span), int(cx + span) + 1):
                off = (x + 0.5 - cx) / max(half, 1)
                droop = (x * 5 + y) % 4 == 0 and y > y_low - 2
                k = 5 if off < -0.35 and s < 0.7 else 4 if off < 0 else 3 if off < 0.4 else 2
                if s > 0.82:
                    k = max(k - 2, 1)
                if not droop:
                    art.put(x, y, PINE[k])
            # The ragged foot of the tier.
            for x in range(int(cx - span), int(cx + span) + 1, 3):
                art.put(x, y_low + 1, PINE[1])
    for y in range(top, top + 6):
        art.put(cx, y, PINE[4])
    art.outline(PINE[0])
    return art.image()


def willow(variant: int) -> Image.Image:
    """A willow over the water: a round head of bundles and long strands that hang to the
    ground, the light catching their left side."""
    r = rng("willow", variant)
    tall, wide = r.randint(120, 140), r.randint(110, 130)
    W, H = wide + 16, tall + SINK + 4
    art = Art(W, H)
    cx, foot = W // 2, H - SINK + 6
    trunk(art, cx - 4, foot, int(tall * 0.55), 12, 7, BARK, r, lean=1.5)
    head = H - SINK - tall
    for k in range(5):
        bundle(art, cx + r.uniform(-wide * 0.3, wide * 0.3), head + 22 + r.uniform(-6, 10), wide * 0.24, 22, WILLOW, r, ("willow", variant, k), gaps=False)
    # The strands, from the head's edge down.
    for x in range(8, W - 8):
        off = (x - cx) / (wide / 2)
        if abs(off) > 1:
            continue
        start = head + 22 + int(18 * (1 - off * off) ** 0.5) - 6
        length = int((tall * 0.55) * (0.55 + 0.45 * abs(off)) * r.uniform(0.7, 1.0))
        if (x + variant) % 2:
            continue
        for y in range(start, min(start + length, H - SINK + 2)):
            sway = int(round(math.sin((y - start) / 9.0 + x * 0.3)))
            k = 5 if off < -0.3 else 3 if off < 0.4 else 2
            art.put(x + sway, y, WILLOW[k if (y - start) % 5 else k - 1])
    art.outline(WILLOW[0])
    return art.image()


def dead_tree(variant: int) -> Image.Image:
    """A tree long dead: grey wood, a split trunk, branches that fork and thin out."""
    r = rng("deadtree", variant)
    tall = r.randint(90, 130)
    W, H = int(tall * 0.8) + 10, tall + SINK + 4
    art = Art(W, H)
    cx, foot = W // 2, H - SINK + 6
    trunk(art, cx, foot, int(tall * 0.55), 9, 5, GREYWOOD, r)

    def grow(x, y, angle, length, width, depth):
        x1, y1 = x + math.cos(angle) * length, y + math.sin(angle) * length
        branch(art, x, y, x1, y1, width, GREYWOOD)
        if depth > 0:
            for turn in (-0.45, 0.4) if depth > 1 or r.random() < 0.7 else (r.choice([-0.4, 0.4]),):
                grow(x1, y1, angle + turn + r.uniform(-0.2, 0.2), length * r.uniform(0.6, 0.75), max(width - 1, 1), depth - 1)

    top = foot - int(tall * 0.55)
    grow(cx, top + 4, -math.pi / 2 - 0.5, tall * 0.28, 3, 3)
    grow(cx, top + 4, -math.pi / 2 + 0.45, tall * 0.3, 3, 3)
    grow(cx, top + 14, -math.pi / 2 + 1.0, tall * 0.18, 2, 2)
    art.outline(GREYWOOD[0])
    return art.image()


def shrub(variant: int) -> Image.Image:
    """A thicket two or three tiles wide: a few low bundles, a berry or two."""
    r = rng("shrub", variant)
    wide, tall = r.randint(34, 52), r.randint(22, 30)
    W, H = wide + 8, tall + SINK
    art = Art(W, H)
    ground = H - SINK + 4
    for k in range(4):
        bx = 4 + wide * (0.2 + 0.6 * k / 3) + r.uniform(-3, 3)
        bundle(art, bx, ground - tall * r.uniform(0.35, 0.55), wide * 0.22, tall * 0.42, OAK, r, ("shrub", variant, k), gaps=False)
    if variant % 2:
        for _ in range(6):
            x, y = r.randint(6, W - 6), r.randint(ground - tall + 6, ground - 4)
            if art.has(x, y):
                art.put(x, y, hexes("c02a3a")[0])
    art.a[ground + 2 :, :, 3] = 0
    art.outline(OAK[0])
    return art.image()


# --- What people built ------------------------------------------------------------


def masonry(art: Art, x0, y0, x1, y1, ramp, r, course: int = 5, dark: bool = False):
    """Stone in courses, each stone its own shade, the mortar dark, the top of each stone lit."""
    shift_k = -1 if dark else 0
    for row, y in enumerate(range(int(y0), int(y1), course)):
        x = int(x0) - (row % 2) * r.randint(3, 6)
        while x < x1:
            length = r.randint(6, 11)
            tone = r.choice([2, 3, 3, 4]) + shift_k
            for yy in range(y, min(y + course, int(y1))):
                for xx in range(max(x, int(x0)), min(x + length, int(x1))):
                    edge = yy == y + course - 1 or xx == x + length - 1
                    k = 1 if edge else tone + 1 if yy == y else tone
                    art.put(xx, yy, ramp[max(min(k, len(ramp) - 1), 1)])
            x += length


def roof_tiles(art: Art, pts, ramp, r, rows: int = 4):
    """A roof's face between its eaves and its ridge, laid as rows of tiles, lit along each row's
    lower lip. `pts` is a function y -> (x0, x1) of the face at a row."""
    for y, (x0, x1) in pts:
        for x in range(int(x0), int(x1)):
            row = (y // rows) % 2
            lip = y % rows == rows - 1
            seam = (x + row * 3) % 6 == 0
            k = 1 if seam else 4 if lip else 3 if (x * 7 + y) % 9 else 2
            art.put(x, y, ramp[k])


def window(art: Art, x, y, w, h, glow=None, broken=False, r=None):
    """A window: a deep dark opening (or a lit one), a sill of stone, a cross of timber."""
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            art.put(xx, yy, glow[1] if glow else DARK)
    if glow:
        for yy in range(y + 1, y + h - 1):
            art.put(x + 1, yy, glow[2])
    mid_x, mid_y = x + w // 2, y + h // 2
    for yy in range(y, y + h):
        art.put(mid_x, yy, TIMBER[2])
    if not broken:
        for xx in range(x, x + w):
            art.put(xx, mid_y, TIMBER[2])
    for xx in range(x - 1, x + w + 1):
        art.put(xx, y + h, STONEWORK[5])
        art.put(xx, y + h + 1, STONEWORK[2])
    if broken and r:
        for _ in range(3):
            art.put(x + r.randrange(w), y + r.randrange(h), PLASTER[4])


def ivy(art: Art, x0, y0, x1, y1, r, amount: float):
    """Ivy over a wall: clumps of leaves that climb from the ground and thin out upward."""
    for _ in range(int((x1 - x0) * (y1 - y0) * amount / 4)):
        x = r.randint(int(x0), int(x1) - 1)
        y = int(y1 - (y1 - y0) * r.random() ** 1.8)
        if art.has(x, y):
            k = r.choice([2, 3, 3, 4, 5])
            art.put(x, y, OAK[k])
            art.put(x + 1, y, OAK[k - 1])
            art.put(x, y - 1, OAK[min(k + 1, 6)])


def plinth(art: Art, x0, x1, top, r):
    """The stone the house stands on, down into the ground."""
    masonry(art, x0, top, x1, art.h, STONEWORK, r, course=6, dark=True)


def cottage(variant: int) -> Image.Image:
    """A cottage of stone below and timber and plaster above, under a roof of red tiles,
    a chimney at one end. Variant 0 is a ruin: its roof fell in at the east end (the
    rafters stand bare), its plaster is gone in places, its windows are black, ivy has
    it. Variant 1 still has a roof of thatch, someone keeps a lamp in its window."""
    r = rng("cottage", variant)
    ruin = variant == 0
    W, wall_h = 150, 54
    roof_h = 46
    H = wall_h + roof_h + 22 + 3 * TILE
    art = Art(W, H)
    floor = H - 3 * TILE  # the floor line: the plinth goes down under it into the ground
    x0, x1 = 14, W - 14
    plinth(art, x0 - 2, x1 + 2, floor, r)
    top = floor - wall_h
    # The ground floor of stone, the upper of timber and plaster.
    masonry(art, x0, floor - 22, x1, floor, STONEWORK, r)
    for y in range(top, floor - 22):
        for x in range(x0, x1):
            k = 4 if x < x0 + 3 else 3 if (x * 13 + y * 7) % 23 else 2
            art.put(x, y, PLASTER[k])
    for x in list(range(x0, x1, 22)) + [x1 - 3]:
        for y in range(top, floor - 22):
            for dx in range(3):
                art.put(x + dx, y, TIMBER[3 if dx == 0 else 2])
    for y in (top, floor - 24):
        for x in range(x0 - 2, x1 + 2):
            art.put(x, y, TIMBER[3])
            art.put(x, y + 1, TIMBER[2])
            art.put(x, y + 2, TIMBER[1])
    # Braces in the panels.
    for x in range(x0 + 3, x1 - 22, 44):
        art.line(x, floor - 26, x + 18, top + 3, TIMBER[2], 2)
    # Door and windows.
    door_x = x0 + 52
    for y in range(floor - 30, floor):
        for x in range(door_x, door_x + 14):
            art.put(x, y, DARK if ruin else TIMBER[2 if (x - door_x) % 4 else 1])
    for x in range(door_x - 2, door_x + 16):
        art.put(x, floor - 31, STONEWORK[5])
        art.put(x, floor - 32, STONEWORK[3])
    if ruin:
        # The door hangs off one hinge, leaning in the opening.
        art.line(door_x + 2, floor - 1, door_x + 9, floor - 28, TIMBER[3], 4)
    glow = GLOW if not ruin else None
    window(art, x0 + 14, floor - 18, 12, 11, glow=None, broken=ruin, r=r)
    window(art, x0 + 86, floor - 18, 12, 11, glow=glow, broken=ruin, r=r)
    window(art, x0 + 30, top + 8, 10, 10, glow=None, broken=ruin, r=r)
    window(art, x0 + 92, top + 8, 10, 10, glow=None, broken=ruin, r=r)
    # The roof: a pitched one from eave to eave, the ridge in the middle.
    ridge_x, ridge_y = W // 2, top - roof_h
    face = []
    for y in range(ridge_y, top + 4):
        t = (y - ridge_y) / (top + 4 - ridge_y)
        half = 6 + (W / 2 - 4) * t
        face.append((y, (ridge_x - half, ridge_x + half)))
    if ruin:
        # The west half keeps its tiles; the east has fallen, its rafters bare against the sky.
        roof_tiles(art, [(y, (a, min(b, ridge_x + 6 + (y - ridge_y) * 0.2))) for y, (a, b) in face], ROOF, r)
        for k in range(5):
            x = ridge_x + 10 + k * 13
            art.line(ridge_x + 2, ridge_y + 2, min(x + 18, W - 4), top + 2 - k * 2 + (6 if k == 4 else 0), TIMBER[2 if k % 2 else 3], 2)
        for x in range(ridge_x, W - 18, 7):
            art.put(x, top - 6, TIMBER[1])
        # The plaster has come away from the east of the upper floor, showing the dark inside.
        for y in range(top + 3, floor - 25):
            for x in range(x0 + 104, x1 - 3):
                if (x - (x0 + 104)) + (y - top) * 0.6 > 6 + 8 * math.sin(y * 0.3):
                    art.put(x, y, DARK if (x + y) % 5 else PLASTER[1])
        ivy(art, x0, top, x0 + 70, floor, r, 0.55)
        ivy(art, x1 - 30, floor - 30, x1, floor, r, 0.4)
    else:
        for y, (a, b) in face:
            for x in range(int(a), int(b)):
                k = 5 if (x - a) < 4 else 2 if b - x < 4 else 4 if (x * 5 + y * 3) % 7 == 0 else 3
                if (y - ridge_y) % 7 == 6:
                    k = max(k - 1, 1)
                art.put(x, y, THATCH[k])
        for x in range(ridge_x - 8, ridge_x + 9):
            art.put(x, ridge_y, THATCH[2])
    # The chimney, at the west end, through the roof.
    cx = x0 + 18
    masonry(art, cx, ridge_y - 4, cx + 12, top - 10, STONEWORK, r, course=4)
    for x in range(cx - 1, cx + 13):
        art.put(x, ridge_y - 5, STONEWORK[5])
    art.outline(STONEWORK[0])
    return art.image()


def cottage_glow() -> Image.Image:
    """The lit window of the cottage that still has a roof, drawn unlit over it: the lamp
    in it shows at dusk and at night."""
    base = cottage(1)
    art = Art(base.width, base.height)
    a = np.array(base)
    lit = (a[..., 0] == GLOW[1][0]) & (a[..., 1] == GLOW[1][1]) & (a[..., 2] == GLOW[1][2])
    lit |= (a[..., 0] == GLOW[2][0]) & (a[..., 1] == GLOW[2][1]) & (a[..., 2] == GLOW[2][2])
    art.a[lit] = a[lit]
    return art.image()


def chapel(variant: int = 0) -> Image.Image:
    """A chapel of stone, roofless: its west gable stands with a tall pointed window and a
    bell-cote on top (the bell still in it), its long wall broken down to the sills, a door
    under a pointed arch; ivy and the sky through all of it."""
    r = rng("chapel", variant)
    W = 170
    H = 150 + 3 * TILE
    art = Art(W, H)
    floor = H - 3 * TILE
    plinth(art, 8, W - 8, floor, r)
    # The gable end, west.
    gx0, gx1 = 10, 62
    gtop = floor - 104
    masonry(art, gx0, gtop, gx1, floor, STONEWORK, r)
    for y in range(gtop - 30, gtop):
        t = (y - (gtop - 30)) / 30
        half = 4 + (gx1 - gx0) / 2 * t
        masonry(art, (gx0 + gx1) / 2 - half, y, (gx0 + gx1) / 2 + half, y + 1, STONEWORK, r, course=5)
    # The bell-cote on the gable's top.
    bx = (gx0 + gx1) // 2
    masonry(art, bx - 9, gtop - 52, bx + 9, gtop - 26, STONEWORK, r)
    for y in range(gtop - 48, gtop - 32):
        for x in range(bx - 5, bx + 5):
            if (x - bx) ** 2 / 25 + ((y - (gtop - 44)) / 6) ** 2 <= 1 or y > gtop - 44:
                art.put(x, y, DARK)
    for y in range(gtop - 44, gtop - 36):
        half = 1 + (y - (gtop - 44)) // 2
        for x in range(bx - half, bx + half + 1):
            art.put(x, y, COPPER[4] if x < bx else COPPER[2])
    for y in range(gtop - 62, gtop - 52):
        half = (y - (gtop - 62)) * 0.9
        for x in range(int(bx - half), int(bx + half) + 1):
            art.put(x, y, STONEWORK[5] if x < bx else STONEWORK[3])
    art.line(bx, gtop - 70, bx, gtop - 62, IRON[3])
    art.line(bx - 3, gtop - 67, bx + 3, gtop - 67, IRON[3])
    # The tall pointed window of the gable, its tracery gone.
    wx = bx
    for y in range(gtop + 6, floor - 34):
        half = 7 if y > gtop + 16 else 7 * math.sqrt(max(0.0, 1 - ((gtop + 16 - y) / 10) ** 2))
        for x in range(int(wx - half), int(wx + half) + 1):
            art.put(x, y, DARK)
    art.line(wx, gtop + 12, wx, floor - 34, STONEWORK[2], 1)
    # The long wall, east of the gable, broken.
    lx0, lx1 = gx1, W - 10
    for x in range(lx0, lx1):
        height = 60 - (x - lx0) * 0.35 + 8 * math.sin(x * 0.21) + 5 * math.sin(x * 0.53)
        height = max(height, 10)
        masonry(art, x, floor - int(height), x + 1, floor, STONEWORK, r)
    # Its windows (the broken tops of them), and a door under a pointed arch.
    for wx2 in (lx0 + 24, lx0 + 62):
        for y in range(floor - 44, floor - 14):
            half = 5 if y > floor - 38 else 5 * math.sqrt(max(0.0, 1 - ((floor - 38 - y) / 6) ** 2))
            for x in range(int(wx2 - half), int(wx2 + half) + 1):
                if art.has(x, y):
                    art.put(x, y, DARK)
    dx = lx0 + 88
    for y in range(floor - 30, floor):
        half = 7 if y > floor - 22 else 7 * math.sqrt(max(0.0, 1 - ((floor - 22 - y) / 8) ** 2))
        for x in range(int(dx - half), int(dx + half) + 1):
            if art.has(x, y):
                art.put(x, y, DARK)
    ivy(art, gx0, gtop - 10, gx1, floor, r, 0.35)
    ivy(art, lx0, floor - 50, lx1, floor, r, 0.5)
    art.outline(STONEWORK[0])
    return art.image()


def well() -> Image.Image:
    """A well: a ring of stone, two posts and a little roof, the crank and the rope, the
    bucket on the rim."""
    r = rng("well")
    W, H = 64, 58 + SINK
    art = Art(W, H)
    floor = H - SINK
    masonry(art, 12, floor - 16, W - 12, floor + 6, STONEWORK, r, course=4)
    for x in range(10, W - 10):
        art.put(x, floor - 17, STONEWORK[6])
        art.put(x, floor - 16, STONEWORK[4])
    for px in (16, W - 20):
        for y in range(floor - 44, floor - 16):
            for dx in range(4):
                art.put(px + dx, y, TIMBER[4 if dx == 0 else 2])
    for y in range(floor - 56, floor - 42):
        t = (y - (floor - 56)) / 14
        half = 6 + 22 * t
        for x in range(int(W / 2 - half), int(W / 2 + half)):
            k = 5 if x < W / 2 - half + 3 else 2 if x > W / 2 + half - 3 else 3
            art.put(x, y, ROOF[k] if (y % 3) else ROOF[1])
    for x in range(16, W - 16):
        art.put(x, floor - 36, TIMBER[3])
        art.put(x, floor - 35, TIMBER[1])
    art.line(W - 16, floor - 36, W - 10, floor - 30, IRON[3])
    for y in range(floor - 35, floor - 24):
        art.put(W // 2, y, THATCH[4])
    for y in range(floor - 23, floor - 18):
        for x in range(W // 2 - 3, W // 2 + 4):
            art.put(x, y, TIMBER[3] if y != floor - 21 else IRON[3])
    art.outline(STONEWORK[0])
    return art.image()


def windmill() -> Image.Image:
    """A windmill on its hill, long still: a tower of stone that narrows upward, a cap of
    boards, four sails of lattice of which two are broken, a door and a small window."""
    r = rng("windmill")
    W, H = 190, 200 + 2 * TILE
    art = Art(W, H)
    floor = H - 2 * TILE
    cx = W // 2
    base_half, top_half, tower_h = 26, 17, 118
    for y in range(floor - tower_h, H):
        t = min(max((floor - y) / tower_h, 0), 1)
        half = base_half + (top_half - base_half) * t
        masonry(art, cx - half, y, cx + half, y + 1, PLASTER, r, course=6)
    for y in range(floor - tower_h, floor):
        t = (floor - y) / tower_h
        half = base_half + (top_half - base_half) * t
        for x in range(int(cx - half), int(cx - half) + 3):
            art.put(x, y, PLASTER[5])
        for x in range(int(cx + half) - 5, int(cx + half)):
            art.put(x, y, PLASTER[1] if x > cx + half - 3 else PLASTER[2])
    # Door and window.
    for y in range(floor - 26, floor):
        for x in range(cx - 7, cx + 6):
            art.put(x, y, DARK)
    window(art, cx - 4, floor - 74, 8, 10)
    # The cap.
    cap_y = floor - tower_h
    for y in range(cap_y - 22, cap_y + 2):
        t = (y - (cap_y - 22)) / 24
        half = 6 + (top_half + 4) * math.sqrt(t)
        for x in range(int(cx - half), int(cx + half)):
            k = 4 if x < cx - half * 0.4 else 2 if x > cx + half * 0.5 else 3
            art.put(x, y, TIMBER[k] if (x % 4) else TIMBER[1])
    # The sails, from the hub: lattice arms, two of them broken short.
    hub = (cx + 2, cap_y - 6)
    for k, (angle, length, broken) in enumerate(((-0.95, 82, False), (0.62, 80, True), (2.2, 82, False), (3.76, 46, True))):
        ex, ey = hub[0] + math.cos(angle) * length, hub[1] + math.sin(angle) * length
        art.line(hub[0], hub[1], ex, ey, TIMBER[2], 3)
        nx, ny = -math.sin(angle), math.cos(angle)
        start = 18
        for d in range(start, int(length) - 2, 7):
            px, py = hub[0] + math.cos(angle) * d, hub[1] + math.sin(angle) * d
            art.line(px, py, px + nx * 14, py + ny * 14, TIMBER[3], 1)
        for side in (0.0, 14.0):
            art.line(
                hub[0] + math.cos(angle) * start + nx * side,
                hub[1] + math.sin(angle) * start + ny * side,
                ex + nx * side,
                ey + ny * side,
                TIMBER[3 if side else 4],
                1,
            )
        if broken:
            art.line(ex, ey, ex + nx * 9 + math.cos(angle + 1.2) * 8, ey + ny * 9 + math.sin(angle + 1.2) * 8, TIMBER[2], 2)
    for dx in range(-3, 4):
        for dy in range(-3, 4):
            if dx * dx + dy * dy <= 9:
                art.put(hub[0] + dx, hub[1] + dy, IRON[2 if dx + dy > 0 else 4])
    ivy(art, cx - base_half, floor - 50, cx - 6, floor, r, 0.4)
    art.outline(TIMBER[0])
    return art.image()


def fence(variant: int) -> Image.Image:
    """Three tiles of a picket fence: whole, with a gap and a picket fallen, or leaning."""
    r = rng("fence", variant)
    W, H = 3 * TILE, 22 + SINK
    art = Art(W, H)
    floor = H - SINK
    lean = [0.0, 0.0, 0.18][variant % 3]
    for x in range(1, W - 1, 6):
        if variant % 3 == 1 and 18 <= x < 30:
            continue
        tall = r.randint(16, 19)
        dx = lean * tall
        art.line(x, floor + 4, x + dx, floor - tall, TIMBER[3], 3)
        art.put(int(x + dx), floor - tall - 1, TIMBER[4])
    for y in (floor - 6, floor - 14):
        art.line(0, y, W - 1, y - (lean * 6 if lean else 0), TIMBER[2], 2)
    if variant % 3 == 1:
        art.line(18, floor - 1, 31, floor - 3, TIMBER[2], 2)
    art.outline(TIMBER[0])
    return art.image()


def cart() -> Image.Image:
    """A farm cart left where it stopped: a box of boards on one wheel, the other wheel off and
    leaning against it, its shafts down in the grass."""
    W, H = 72, 34 + SINK
    art = Art(W, H)
    floor = H - SINK
    for y in range(floor - 26, floor - 12):
        for x in range(10, 54):
            art.put(x, y, TIMBER[4 if y == floor - 26 else 1 if (y - floor) % 5 == 0 else 3 if x > 12 else 4])
    art.line(54, floor - 14, 70, floor - 1, TIMBER[2], 2)

    def wheel(cx, cy, rad):
        for a in range(0, 360, 4):
            art.put(cx + math.cos(math.radians(a)) * rad, cy + math.sin(math.radians(a)) * rad, TIMBER[2])
            art.put(cx + math.cos(math.radians(a)) * (rad - 1), cy + math.sin(math.radians(a)) * (rad - 1), TIMBER[3])
        for a in range(0, 180, 45):
            art.line(cx - math.cos(math.radians(a)) * rad, cy - math.sin(math.radians(a)) * rad, cx + math.cos(math.radians(a)) * rad, cy + math.sin(math.radians(a)) * rad, TIMBER[2])
        art.put(cx, cy, IRON[3])

    wheel(22, floor - 10, 10)
    wheel(46, floor - 9, 9)
    art.outline(TIMBER[0])
    return art.image()


def haystack(variant: int) -> Image.Image:
    """A stack of old hay gone grey-gold, a pole through its top."""
    r = rng("hay", variant)
    W, H = 46, 40 + SINK
    art = Art(W, H)
    floor = H - SINK
    for y in range(floor - 34, floor + 4):
        t = (y - (floor - 34)) / 38
        half = 6 + 17 * math.sin(min(t * 1.4, 1) * math.pi / 2)
        for x in range(int(W / 2 - half), int(W / 2 + half)):
            off = (x - W / 2) / half
            k = 5 if off < -0.5 else 4 if off < 0 else 3 if off < 0.5 else 2
            if (x * 3 + y * 5) % 11 == 0:
                k = max(k - 1, 1)
            art.put(x, y, THATCH[k])
    art.line(W // 2, floor - 42, W // 2, floor - 30, TIMBER[3], 2)
    art.outline(THATCH[0])
    return art.image()


def scarecrow() -> Image.Image:
    """A scarecrow in a field nobody sows: a cross of poles, a sack head, a coat in rags."""
    W, H = 34, 52 + SINK
    art = Art(W, H)
    floor = H - SINK
    art.line(17, floor + 4, 17, floor - 44, TIMBER[3], 2)
    art.line(3, floor - 34, 31, floor - 36, TIMBER[3], 2)
    for y in range(floor - 34, floor - 14):
        for x in range(10, 25):
            art.put(x, y, ROOF[2] if (x + y) % 7 else ROOF[1])
    for x in range(3, 10):
        art.put(x, floor - 33, ROOF[3])
        art.put(x, floor - 32, ROOF[2])
    for x in range(25, 31):
        art.put(x, floor - 35, ROOF[3])
    for y in range(floor - 46, floor - 35):
        for x in range(12, 22):
            if (x - 17) ** 2 / 25 + ((y - (floor - 40)) / 5.5) ** 2 <= 1:
                art.put(x, y, THATCH[4] if x < 17 else THATCH[3])
    for y in range(floor - 50, floor - 45):
        for x in range(9, 26):
            if y > floor - 47 or 13 <= x <= 21:
                art.put(x, y, TIMBER[1])
    art.put(15, floor - 41, DARK)
    art.put(19, floor - 41, DARK)
    art.outline(TIMBER[0])
    return art.image()


def grave(variant: int) -> Image.Image:
    """A grave before the Gate: a headstone (round-topped, a cross, or a slab gone over), lichen."""
    r = rng("tomb", variant)
    W, H = 24, 30 + SINK
    art = Art(W, H)
    floor = H - SINK
    kind = variant % 4
    if kind == 0:
        for y in range(floor - 22, floor + 3):
            for x in range(5, 19):
                if y > floor - 16 or (x - 12) ** 2 + ((y - (floor - 16)) * 1.2) ** 2 <= 49:
                    art.put(x, y, STONEWORK[5 if x < 8 else 2 if x > 16 else 4])
        for x in (9, 10, 11, 12, 13, 14):
            art.put(x, floor - 12, STONEWORK[2])
        art.put(10, floor - 9, STONEWORK[2])
        art.put(13, floor - 9, STONEWORK[2])
    elif kind == 1:
        art.rect(10, floor - 26, 14, floor + 3, STONEWORK[4])
        art.rect(5, floor - 20, 19, floor - 16, STONEWORK[4])
        art.rect(10, floor - 26, 11, floor + 3, STONEWORK[5])
        art.rect(13, floor - 20, 14, floor + 3, STONEWORK[2])
    elif kind == 2:
        for i in range(18):
            y = floor - 2 - i
            x0 = 6 + int(i * 0.5)
            for x in range(x0, x0 + 11):
                art.put(x, y, STONEWORK[4 if x < x0 + 3 else 2 if x > x0 + 8 else 3])
    else:
        art.rect(3, floor - 8, 21, floor + 3, STONEWORK[3])
        art.rect(3, floor - 8, 21, floor - 6, STONEWORK[5])
    for _ in range(5):
        x, y = r.randint(4, 19), r.randint(floor - 20, floor)
        if art.has(x, y):
            art.put(x, y, hexes("7a8a4a")[0])
    art.outline(STONEWORK[0])
    return art.image()


def gateway() -> Image.Image:
    """The Gate on the surface: a great arch of dark stone over the mouth of the way down,
    pointed, its voussoirs picked out, a band of letters nobody reads over the arch, two
    buttresses, a crack, and the warm glow of what is below inside it."""
    r = rng("gateway")
    W, H = 18 * TILE, 13 * TILE + 3 * TILE
    art = Art(W, H)
    floor = H - 3 * TILE
    dark = ramp_of("140f14 241c24 33282f 433640 54464e 66575c 7a6a6c")
    masonry(art, 0, floor - 150, W, H, dark, r, course=7)
    # Cut the outline: buttresses that step in, a gable over the arch.
    for y in range(0, H):
        for x in range(W):
            outside = False
            if y < floor - 150:
                outside = True
            elif x < 14 or x >= W - 14:
                outside = y < floor - 96 + (0 if x < 7 or x >= W - 7 else -24)
            else:
                peak = floor - 150 + abs(x - W / 2) * 0.32 - 16
                outside = y < peak
            if outside:
                art.a[y, x, 3] = 0
    for y in range(floor - 166, floor - 150):
        for x in range(14, W - 14):
            peak = floor - 150 + abs(x - W / 2) * 0.32 - 16
            if y >= peak:
                art.put(x, y, dark[3] if x < W / 2 else dark[2])
    # The opening: a pointed arch, black inside, a red glow at its foot.
    cx, half, spring = W // 2, 44, floor - 70
    for y in range(floor - 124, floor):
        if y >= spring:
            span = half
        else:
            # two arcs that meet at the top
            rise = (spring - y) / (spring - (floor - 124))
            span = half * math.sqrt(max(0.0, 1 - rise ** 1.6))
        for x in range(int(cx - span), int(cx + span) + 1):
            ember_glow(art, x, y, max(0.0, (y - (floor - 56)) / 56))
        # The voussoirs round the opening.
        for side in (-1, 1):
            for w_ in range(6):
                x = int(cx + side * (span + 1 + w_))
                if art.has(x, y):
                    k = 5 if ((y // 6) + w_ // 3) % 2 else 4
                    art.put(x, y, dark[k if side < 0 else k - 2])
    # Under the floor line the opening is open: the way down goes on through it.
    for y in range(floor, H):
        for x in range(cx - half, cx + half + 1):
            art.a[y, x, 3] = 0
    # A band of letters over the arch.
    for x in range(cx - 52, cx + 53):
        art.put(x, floor - 136, dark[6])
        art.put(x, floor - 129, dark[1])
        if x % 5 and (x * 7) % 11 > 3:
            art.put(x, floor - 132 - (x % 2), dark[1])
    # A crack down the east pier.
    x, y = cx + 64, floor - 120
    while y < floor:
        art.put(x, y, DARK)
        art.put(x + 1, y, dark[4])
        x += r.choice([-1, 0, 0, 1])
        y += 1
    ivy(art, 0, floor - 90, 40, floor, r, 0.25)
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


def gate_arch() -> Image.Image:
    """The Gate itself, in its hall under the ground: two piers of dark red stone with a
    figure cut in a niche in each, a round arch of voussoirs between them with a keystone
    carved as a face, a band of letters over it; the dark inside, warm at its foot where
    the fire of what is below shows."""
    r = rng("gate arch")
    W, H = 15 * TILE, 12 * TILE + SINK
    art = Art(W, H)
    floor = H - SINK
    stone = ramp_of("0e0606 1c0c0a 28110e 341713 42201a 50281f 60302a")
    pier = 3 * TILE
    top = floor - 11 * TILE
    masonry(art, 0, top + 18, pier, H, stone, r, course=7)
    masonry(art, W - pier, top + 18, W, H, stone, r, course=7)
    masonry(art, 0, top, W, top + 30, stone, r, course=6)
    # The opening: a round arch springing from the piers.
    cx, half = W / 2, (W - 2 * pier) / 2
    spring = top + 30 + half * 0.6
    for y in range(int(top + 26), floor):
        if y >= spring:
            span = half
        else:
            k = (spring - y) / (half * 0.9)
            span = half * math.sqrt(max(0.0, 1 - k * k))
        for x in range(int(cx - span), int(cx + span) + 1):
            ember_glow(art, x, y, max(0.0, (y - (floor - 48)) / 48))
        # Voussoirs round it.
        if y < spring + 4:
            for side in (-1, 1):
                for w_ in range(1, 8):
                    x = int(cx + side * (span + w_))
                    if 0 <= x < W:
                        k = 6 if ((y // 5) + (w_ // 4)) % 2 else 5
                        art.put(x, y, stone[k if side < 0 else k - 2])
    # The keystone, a face: two dark eyes and a mouth.
    ky = int(top + 24)
    art.rect(cx - 7, ky - 2, cx + 7, ky + 12, stone[5])
    art.rect(cx - 7, ky - 2, cx - 5, ky + 12, stone[6])
    art.rect(cx + 5, ky - 2, cx + 7, ky + 12, stone[2])
    for ex in (cx - 4, cx + 2):
        art.rect(ex, ky + 3, ex + 2, ky + 5, hexes("ff7a2a")[0])
    art.rect(cx - 3, ky + 8, cx + 4, ky + 9, DARK)
    # The band of letters.
    for x in range(8, W - 8):
        art.put(x, top + 6, stone[6])
        art.put(x, top + 13, stone[1])
        if x % 4 and (x * 7) % 9 > 2:
            art.put(x, top + 9 + (x % 2), stone[1])
    # A figure in a niche in each pier: hooded, its hands together.
    for nx in (pier // 2, W - pier // 2):
        ny = floor - 78
        for y in range(ny, ny + 52):
            for x in range(nx - 9, nx + 10):
                if y > ny + 8 or (x - nx) ** 2 + ((y - ny - 8) * 1.1) ** 2 <= 81:
                    art.put(x, y, stone[1])
        for y in range(ny + 6, ny + 50):
            t = (y - ny - 6) / 44
            hw = 4 + 4 * t
            for x in range(int(nx - hw), int(nx + hw) + 1):
                art.put(x, y, stone[5] if x < nx - hw * 0.3 else stone[3] if x > nx + hw * 0.4 else stone[4])
        art.rect(nx - 3, ny + 9, nx + 4, ny + 14, stone[1])
    art.outline(stone[0])
    return art.image()


def pithead() -> Image.Image:
    """The head of the old shaft: a frame of timber two legs wide over the hole, braced,
    with the sheave wheel at its top and the rope down into the dark; a hut for the winch."""
    r = rng("pithead")
    W, H = 7 * TILE, 9 * TILE + SINK
    art = Art(W, H)
    floor = H - SINK
    left, right = 10, W - 14
    top = floor - 118
    for x0, x1 in ((left, left + 14), (right, right - 14)):
        art.line(x0, floor + 6, (left + right) / 2 + (x1 - x0) * 0.1, top + 10, TIMBER[3], 4)
    for xa in (left + 4, right - 4):
        art.line(xa, floor + 6, xa, top + 26, TIMBER[2], 4)
    for y in range(top + 30, floor, 26):
        art.line(left + 4, y, right - 4, y, TIMBER[2], 2)
        art.line(left + 4, y, right - 4, y + 24, TIMBER[1], 1)
    # The head and the wheel.
    art.rect(left - 2, top + 20, right + 2, top + 26, TIMBER[3])
    art.rect(left - 2, top + 20, right + 2, top + 21, TIMBER[5])
    wcx, wcy, rad = (left + right) // 2, top + 10, 15
    for a in range(0, 360, 2):
        for k, (rr, colour) in enumerate(((rad, IRON[2]), (rad - 1, IRON[4]), (rad - 2, IRON[3]))):
            art.put(wcx + math.cos(math.radians(a)) * rr, wcy + math.sin(math.radians(a)) * rr, colour)
    for a in range(0, 180, 30):
        art.line(wcx - math.cos(math.radians(a)) * rad, wcy - math.sin(math.radians(a)) * rad, wcx + math.cos(math.radians(a)) * rad, wcy + math.sin(math.radians(a)) * rad, IRON[3])
    art.rect(wcx - 2, wcy - 2, wcx + 3, wcy + 3, IRON[1])
    # The rope, down the middle into the shaft.
    for y in range(wcy, floor + SINK):
        art.put(wcx + rad - 1, y, THATCH[3] if y % 4 else THATCH[2])
    art.outline(TIMBER[0])
    return art.image()


def winch_hut() -> Image.Image:
    """The hut the winch stood in, of boards, its roof fallen half in."""
    r = rng("hut")
    W, H = 4 * TILE, 3 * TILE + SINK
    art = Art(W, H)
    floor = H - SINK
    for y in range(floor - 30, floor + 4):
        for x in range(4, W - 4):
            art.put(x, y, TIMBER[3 if (x // 5) % 2 else 2] if (x % 5) else TIMBER[1])
    for y in range(floor - 22, floor - 12):
        for x in range(12, 22):
            art.put(x, y, DARK)
    for y in range(floor - 42, floor - 29):
        t = (y - (floor - 42)) / 13
        for x in range(int(W / 2 - 6 - 26 * t), int(W / 2 + 6 + 26 * t)):
            if x > W / 2 + 4 and (x + y) % 3 == 0:
                continue
            art.put(x, y, ROOF[3 if x < W / 2 else 2])
    art.outline(TIMBER[0])
    return art.image()


def colonnade_ruin() -> Image.Image:
    """Behind the marble stubs by the start: what stood there, a temple of the old world;
    four broken shafts of fluted marble of different heights, a piece of the architrave
    still across two of them, a drum fallen in the grass. Their feet go on down into the
    ground, wherever it rises under them."""
    r = rng("colonnade")
    W, H = 26 * TILE, 7 * TILE + 3 * TILE
    art = Art(W, H)
    floor = H - 3 * TILE
    marble = ramp_of("4a4852 7c7a86 98969f b4b2bb ccc9d1 e2dfe6 f2f0f4")
    shafts = ((24, 88), (112, 46), (288, 100), (384, 64))
    for cx, tall in shafts:
        for y in range(floor - tall, H):
            for x in range(cx - 8, cx + 8):
                flute = (x - cx + 8) % 4
                k = 5 if x < cx - 4 else 2 if x > cx + 4 else 4 if flute == 0 else 3
                art.put(x, y, marble[k])
        for x in range(cx - 11, cx + 11):
            for y in range(floor - 4, floor + 2):
                art.put(x, y, marble[5 if y == floor - 4 else 3])
        # The broken top, ragged.
        for x in range(cx - 8, cx + 8):
            for y in range(floor - tall - r.randint(0, 7), floor - tall):
                art.put(x, y, marble[4 if x < cx else 3])
    # The architrave still across the two tallest... that stand apart: a piece of it on the
    # tallest, broken off over the gap.
    cx, tall = shafts[2]
    for x in range(cx - 12, cx + 12):
        for y in range(floor - tall - 6, floor - tall):
            art.put(x, y, marble[5 if y == floor - tall - 6 else 3])
    for x in range(cx - 34, cx + 14):
        for y in range(floor - tall - 17, floor - tall - 6):
            k = 4 if y < floor - tall - 14 else 3 if (x // 8) % 2 else 2
            if x < cx - 26 and y < floor - tall - 17 + (cx - 26 - x):
                continue
            art.put(x, y, marble[k])
    # A drum fallen in the grass.
    for y in range(floor - 15, floor + 2):
        for x in range(186, 214):
            art.put(x, y, marble[5 if y < floor - 11 else 3 if y < floor - 3 else 2])
    for y in range(floor - 15, floor + 2):
        art.put(213, y, marble[1])
        art.put(212, y, marble[2])
    art.outline(marble[0])
    return art.image()


# --- The pond -----------------------------------------------------------------------


def reeds(variant: int) -> Image.Image:
    """A clump of reeds at the water's edge: blades of different heights leaning a little,
    two or three bulrush heads."""
    r = rng("reeds", variant)
    W, H = 2 * TILE, 30 + SINK
    art = Art(W, H)
    floor = H - SINK
    for i in range(r.randint(7, 10)):
        x = r.randint(2, W - 3)
        tall = r.randint(12, 28)
        lean = r.choice([-1, 0, 0, 1])
        for h in range(-4, tall):
            dx = int(lean * (h / tall) ** 2 * 3)
            art.put(x + dx, floor - h, WILLOW[2] if h < tall * 0.4 else WILLOW[3] if i % 2 else WILLOW[4])
        if i % 3 == 0:
            for h in range(tall - 6, tall - 1):
                dx = int(lean * (h / tall) ** 2 * 3)
                art.put(x + dx, floor - h, BARK[2])
                art.put(x + dx + 1, floor - h, BARK[1])
    return art.image()


def lilies(variant: int) -> Image.Image:
    """Lily pads on the water, a flower on one; drawn at the water's surface."""
    r = rng("lilies", variant)
    W, H = 3 * TILE, TILE
    art = Art(W, H)
    y0 = 3  # the water stands 3 px under the top of its cell
    x = r.randint(2, 6)
    while x < W - 8:
        wide = r.randint(5, 8)
        for dx in range(wide):
            art.put(x + dx, y0 - 1, OAK[5] if dx < wide - 3 else OAK[4])
            art.put(x + dx, y0, OAK[4] if dx < wide - 2 else OAK[3])
            art.put(x + dx, y0 + 1, OAK[2])
        art.put(x + wide // 2, y0 - 1, OAK[2])
        art.put(x + wide // 2, y0, OAK[1])
        x += wide + r.randint(3, 9)
    if variant % 2 == 0:
        for dx, dy, c in ((10, -2, "f6e8f0"), (11, -3, "ffffff"), (12, -2, "f0b8d0"), (11, -1, "f6d23a")):
            art.put(dx, y0 + dy, hexes(c)[0])
    return art.image()


def jetty() -> Image.Image:
    """A landing stage of boards out over the water on two piles, a board missing."""
    W, H = 4 * TILE, 2 * TILE + SINK
    art = Art(W, H)
    deck = H - SINK - 10
    for x in range(0, W - 4):
        if 30 <= x < 36:
            continue
        art.put(x, deck, TIMBER[5])
        art.put(x, deck + 1, TIMBER[3] if x % 8 else TIMBER[1])
        art.put(x, deck + 2, TIMBER[2])
    for px in (20, W - 10):
        for y in range(deck + 3, H):
            art.put(px, y, TIMBER[3])
            art.put(px + 1, y, TIMBER[2])
            art.put(px + 2, y, TIMBER[1])
    art.outline(TIMBER[0])
    return art.image()


# --- The old mine -------------------------------------------------------------------

POST_HEIGHTS = range(3, 13)  # the tunnels' heights (cells of air) a set of timber is drawn for


def mine_post(rows: int) -> Image.Image:
    """A set of mine timber for a tunnel `rows` cells high: a post on a sill, a cap that
    carries the roof, two knee braces under the cap. It reaches into the roof and the floor
    a little, behind the ground."""
    W = 3 * TILE
    H = rows * TILE + 4 + SINK
    art = Art(W, H)
    floor = H - SINK
    cx = W // 2
    for y in range(4, floor + 4):
        for x in range(cx - 3, cx + 3):
            k = 4 if x == cx - 3 else 2 if x >= cx + 1 else 3
            if (y * 3 + x) % 17 == 0:
                k = 1
            art.put(x, y, TIMBER[k])
    for y in range(0, 7):
        for x in range(4, W - 4):
            art.put(x, y, TIMBER[4 if y == 4 else 1 if y == 6 else 3])
    for side in (-1, 1):
        art.line(cx + side * 3, 18, cx + side * 14, 7, TIMBER[2 if side > 0 else 3], 2)
    art.rect(cx - 5, floor - 2, cx + 6, floor + 2, TIMBER[2])
    for y in (10, floor - 10):
        art.put(cx - 1, y, IRON[4])
    art.outline(TIMBER[0])
    return art.image()


def rail(piece: str) -> Image.Image:
    """A tile of track: sleepers half sunk in the floor and the rail over them; `piece` is
    `mid`, or `end` (the last tile, with a stop of timber)."""
    W, H = TILE, TILE + SINK
    art = Art(W, H)
    floor = H - SINK  # the floor's top line
    for x in (2, 9):
        for dx in range(4):
            art.put(x + dx, floor - 1, TIMBER[3])
            art.put(x + dx, floor, TIMBER[2])
            art.put(x + dx, floor + 1, TIMBER[1])
    for x in range(W):
        art.put(x, floor - 3, IRON[5])
        art.put(x, floor - 2, IRON[2])
    if piece == "end":
        art.rect(10, floor - 9, 16, floor - 1, TIMBER[3])
        art.rect(10, floor - 9, 16, floor - 8, TIMBER[5])
        art.rect(15, floor - 9, 16, floor - 1, TIMBER[1])
    return art.image()


def mine_cart(variant: int) -> Image.Image:
    """An ore cart on the track: an iron tub, ribbed, two wheels; full of copper ore (or
    tipped over and empty)."""
    r = rng("minecart", variant)
    W, H = 2 * TILE, 26 + SINK
    art = Art(W, H)
    floor = H - SINK - 3  # on the rail
    tipped = variant == 1
    if not tipped:
        for y in range(floor - 18, floor - 4):
            t = (y - (floor - 18)) / 14
            inset = int(t * 3)
            for x in range(2 + inset, W - 2 - inset):
                k = 4 if x < 5 + inset else 1 if x > W - 6 - inset else 3
                if (x - 2) % 7 == 0:
                    k = 2
                art.put(x, y, IRON[k])
        for x in range(1, W - 1):
            art.put(x, floor - 19, IRON[5])
        for _ in range(16):
            x, y = r.randint(4, W - 5), r.randint(floor - 24, floor - 19)
            if y >= floor - 22 + abs(x - W // 2) // 4:
                art.put(x, y, COPPER[r.randint(2, 5)])
                art.put(x + 1, y, COPPER[2])
        for wx in (8, W - 9):
            for dx in range(-3, 4):
                for dy in range(-3, 4):
                    if dx * dx + dy * dy <= 9:
                        art.put(wx + dx, floor - 2 + dy, IRON[1 if dx * dx + dy * dy > 4 else 3])
    else:
        for y in range(floor - 14, floor + 2):
            for x in range(4, W - 2):
                if (x - 4) + (floor + 2 - y) * 0.7 < 22:
                    art.put(x, y, IRON[3 if x < 10 else 2])
        for dx in range(-3, 4):
            for dy in range(-3, 4):
                if dx * dx + dy * dy <= 9:
                    art.put(24 + dx, floor - 12 + dy, IRON[1 if dx * dx + dy * dy > 4 else 3])
        for _ in range(10):
            art.put(r.randint(0, 10), floor + r.randint(-1, 1), COPPER[r.randint(2, 5)])
    art.outline(IRON[0])
    return art.image()


def crates(variant: int) -> Image.Image:
    """What the miners left: crates of boards and a barrel (variant 0), a heap of sacks and a
    pick against them (variant 1), a ladder lying against the wall (variant 2)."""
    r = rng("crates", variant)
    W, H = 3 * TILE, 30 + SINK
    art = Art(W, H)
    floor = H - SINK
    if variant == 0:
        for x0, y0, s in ((2, floor - 16, 16), (20, floor - 14, 14), (8, floor - 30, 14)):
            for y in range(y0, y0 + s):
                for x in range(x0, x0 + s):
                    edge = x in (x0, x0 + s - 1) or y in (y0, y0 + s - 1)
                    art.put(x, y, TIMBER[4 if edge and (x == x0 or y == y0) else 2 if edge else 3 if (y - y0) % 5 else 1])
            art.line(x0 + 1, y0 + 1, x0 + s - 2, y0 + s - 2, TIMBER[2])
        for y in range(floor - 18, floor):
            for x in range(36, 46):
                bulge = abs(y - (floor - 9)) < 6
                k = 4 if x < 38 else 2 if x > 43 else 3
                art.put(x, y, IRON[3] if y in (floor - 15, floor - 4) else TIMBER[k + (1 if bulge and k < 4 else 0)])
    elif variant == 1:
        for cx, cy, rx, ry in ((12, floor - 6, 9, 6), (26, floor - 6, 9, 6), (19, floor - 15, 8, 6)):
            for y in range(cy - ry, cy + ry):
                for x in range(cx - rx, cx + rx):
                    if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1:
                        art.put(x, y, THATCH[4 if x < cx - 2 and y < cy else 2 if y > cy + 2 else 3])
        art.line(34, floor, 40, floor - 24, TIMBER[3], 2)
        art.line(34, floor - 25, 46, floor - 21, IRON[4], 2)
    else:
        for side in (0, 9):
            art.line(6 + side, floor, 18 + side, floor - 28, TIMBER[3], 2)
        for k in range(1, 6):
            t = k / 6
            art.line(6 + 12 * t, floor - 28 * t, 15 + 12 * t, floor - 28 * t, TIMBER[2], 1)
    art.outline(TIMBER[0])
    return art.image()


# --- Behind the surface: the ridges -------------------------------------------------
BACKDROP_W, BACKDROP_H = VIEW


def _profile(seed, waves, width=BACKDROP_W):
    r = rng("earth ridge", seed)
    xs = np.arange(width)
    total = np.zeros(width)
    for k, amp in waves:
        total += amp * np.sin(2 * np.pi * k * xs / width + r.uniform(0, 6.283))
    return total


def ridge_mountains(width: int = 1280) -> Image.Image:
    """The furthest: a range of mountains in the haze, a few great peaks with snow on them,
    each lit on its face to the left of the ridge that runs down from its top and in the
    blue of the low sky on the other, and a band of haze along their foot."""
    a = np.zeros((BACKDROP_H, width, 4), dtype=np.uint8)
    body, shade, lit, deep = hexes("8eaecb", "7d9bc0", "a9c4dc", "7290b6")
    snow, snow_shade = hexes("eef3f8", "c4d2e6")
    r = rng("mountains")
    peaks = []
    count = width // 92
    for k in range(count):
        peaks.append((width * (k + r.uniform(0.1, 0.9)) / count, r.uniform(120, 245), r.uniform(0.6, 0.95), r.uniform(-0.35, 0.35)))
    base = 66 + _profile("m base", [(6, 8), (14, 4)], width)
    rough = _profile("m rough", [(58, 2.0), (94, 1.4), (166, 0.8)], width)
    height = base.copy()
    owner = np.full(width, -1)
    for i, (px, ph, steep, _) in enumerate(peaks):
        for x in range(width):
            d = x - px
            d = (d + width / 2) % width - width / 2
            hgt = ph - abs(d) * steep * (1.15 if d > 0 else 1.0)
            if hgt > height[x]:
                height[x], owner[x] = hgt, i
    height += rough
    top = (BACKDROP_H - np.round(height)).astype(int)
    edge = noise("snowline", width, 1, 7.0)[0]
    for x in range(width):
        for y in range(max(top[x], 0), BACKDROP_H):
            colour = body
            if owner[x] >= 0:
                px, ph, _, lean = peaks[owner[x]]
                peak_y = BACKDROP_H - ph
                ridge = px + (y - peak_y) * (0.25 + lean) + 3 * math.sin((y - peak_y) * 0.12)
                d = (x - ridge + width / 2) % width - width / 2
                colour = lit if d < 0 else shade
                if d >= 0 and (int(d) + y // 3) % 23 == 0:
                    colour = deep  # a gully down the shaded face
                if BACKDROP_H - y > 168 + 10 * edge[x] and y - top[x] < 34:
                    colour = snow if d < 0 else snow_shade
            a[y, x, :3] = colour
            a[y, x, 3] = 255
    # Haze along the foot: the mountains fade into the low sky colour, dithered.
    haze = np.array(hexes("b4d0e6")[0], dtype=float)
    for y in range(BACKDROP_H - 110, BACKDROP_H):
        t = (y - (BACKDROP_H - 110)) / 110
        for x in range(width):
            if a[y, x, 3] and t * 16 > BAYER_4[y % 4][x % 4] + 0.5:
                a[y, x, :3] = (a[y, x, :3] * 0.45 + haze * 0.55).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


BAYER_4 = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def _crowns(a, top, colours, r, x0, x1, base, size, kinds=("round", "pine")):
    width = a.shape[1]
    """A row of tree crowns standing on `base` (a row per column) from x0 to x1, in the given
    colours (dark, mid, lit): round ones and pointed ones mixed."""
    x = x0
    while x < x1:
        kind = r.choice(kinds)
        rad = r.randint(*size)
        foot = int(base[x % width])
        if kind == "round":
            for dx in range(-rad, rad + 1):
                for dy in range(-rad, rad + 1):
                    if dx * dx + dy * dy * 1.2 <= rad * rad:
                        X, Y = (x + dx) % width, foot - rad + dy
                        if 0 <= Y < BACKDROP_H:
                            lit = dx + dy < -rad * 0.4
                            dark = dx + dy > rad * 0.5
                            a[Y, X, :3] = colours[2] if lit else colours[0] if dark else colours[1]
                            a[Y, X, 3] = 255
        else:
            tall = int(rad * 3.2)
            for i in range(tall):
                half = rad * 0.75 * i / tall
                for dx in range(-int(half), int(half) + 1):
                    X, Y = (x + dx) % width, foot - tall + i
                    if 0 <= Y < BACKDROP_H:
                        a[Y, X, :3] = colours[2] if dx < -half * 0.3 else colours[0] if dx > half * 0.4 else colours[1]
                        a[Y, X, 3] = 255
        x += r.randint(max(rad // 2, 2), rad + 2)


def ridge_hills(width: int = 1920) -> Image.Image:
    """Hills nearer than the mountains, blue-green in the haze, with what is left of the
    world on them: a castle on a crag, a church tower, a few roofs, clumps of wood."""
    a = np.zeros((BACKDROP_H, width, 4), dtype=np.uint8)
    body, shade, lit = hexes("6f9aa6", "5f8a9a", "88b2b8")
    dark_tree, mid_tree, lit_tree = hexes("5a8494", "66919c", "7ea6ae")
    r = rng("hills")
    height = 92 + _profile("hills", [(6, 16), (15, 9), (33, 4)], width)
    top = (BACKDROP_H - np.round(height)).astype(int)
    for x in range(width):
        rising = height[x] >= height[(x - 1) % width]
        for y in range(max(top[x], 0), BACKDROP_H):
            a[y, x, :3] = lit if rising and y - top[x] < 3 else body if y - top[x] > 2 or rising else shade
            a[y, x, 3] = 255
    # A castle on the highest hill of the middle third, and woods everywhere else.
    best = int(np.argmax(height[width // 3 : width // 3 + 300])) + width // 3
    _crowns(a, top, (dark_tree, mid_tree, lit_tree), r, 0, best - 70, top + 2, (3, 6))
    _crowns(a, top, (dark_tree, mid_tree, lit_tree), r, best + 70, width, top + 2, (3, 6))
    def tower(x, wide, tall, cap="flat"):
        foot = top[x] + 3
        for X in range(x, x + wide):
            for Y in range(foot - tall, foot):
                a[Y, X % width, :3] = lit if X < x + 2 else shade if X > x + wide - 3 else body
                a[Y, X % width, 3] = 255
        if cap == "flat":
            for X in range(x, x + wide, 3):
                for Y in range(foot - tall - 3, foot - tall):
                    a[Y, X % width, :3] = body
                    a[Y, X % width, 3] = 255
        else:
            for i in range(tall // 2):
                half = wide / 2 * i / (tall // 2)
                for X in range(int(x + wide / 2 - half), int(x + wide / 2 + half) + 1):
                    a[foot - tall - tall // 2 + i, X % width, :3] = shade if X > x + wide / 2 else lit
                    a[foot - tall - tall // 2 + i, X % width, 3] = 255
        return foot
    tower(best - 22, 44, 22)
    tower(best - 30, 10, 38)
    tower(best + 18, 9, 46, cap="spire")
    tower(best - 4, 12, 30)
    for wx in (best - 12, best + 4, best + 30):
        a[top[best] - 14 : top[best] - 9, wx % width, :3] = hexes("4c7080")[0]
    # A village far off with its church, and another; a lone tower on a hill.
    for church, roofs in ((150, (126, 162, 176)), (1330, (1306, 1344, 1360, 1376))):
        tower(church, 7, 26, cap="spire")
        for rx in roofs:
            tower(rx, 12, 7, cap="spire")
    tower(1700, 10, 34)
    return Image.fromarray(a, "RGBA")


def ridge_wood(seed: str, base: float, waves, colours, ground, size, density_kinds, width: int = VIEW[0]) -> Image.Image:
    a = np.zeros((BACKDROP_H, width, 4), dtype=np.uint8)
    r = rng("earth wood", seed)
    height = base + _profile(seed, waves, width)
    top = (BACKDROP_H - np.round(height)).astype(int)
    for x in range(width):
        rising = height[x] >= height[(x - 1) % width]
        for y in range(max(top[x], 0), BACKDROP_H):
            a[y, x, :3] = ground[2] if rising and y - top[x] < 2 else ground[1] if y - top[x] < 4 else ground[0]
            a[y, x, 3] = 255
    _crowns(a, top, colours, r, 0, width, top + 3, size, density_kinds)
    _crowns(a, top, colours, r, 7, width, top + 8, size, density_kinds)
    return Image.fromarray(a, "RGBA")


def ridge_mid() -> Image.Image:
    """Rolling hills with woods, greener and nearer, round trees and firs mixed."""
    return ridge_wood(
        "mid",
        64,
        [(1, 10), (3, 9), (5, 5), (11, 2)],
        hexes("3e7466", "4b8470", "62a07e"),
        hexes("4a8670", "56937a", "6eaa86"),
        (5, 10),
        ("round", "round", "pine"),
    )


def ridge_near() -> Image.Image:
    """The edge of the near wood: dark crowns, lit on the left, firs among them."""
    return ridge_wood(
        "near",
        34,
        [(2, 7), (5, 5), (9, 3)],
        hexes("234a3a", "2d5a42", "3e744e"),
        hexes("2a5240", "305c46", "3a6c50"),
        (7, 14),
        ("round", "pine", "round"),
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


# --- What the generator writes --------------------------------------------------------

OAKS, PINES, APPLES, DEADS, SHRUBS = 4, 3, 2, 2, 3
ORDER_BEHIND = -287  # in front of the walls and their shade, behind the trees of the foliage, the decor and the ground
ORDER_GLOW = -286  # the lit window over its cottage
ORDER_TRACK = -99  # rails and carts: in front of the ground they lie on (it is under them), behind the ledges
ORDER_WATER = 27  # on the water's surface, which is drawn in front of the hero


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
        add(f"earth_oak_{v}", lambda v=v: oak(v, AUTUMN if v == 3 else OAK, turn=0 if v == 3 else 6))
    for v in range(PINES):
        add(f"earth_pine_{v}", lambda v=v: pine(v))
    for v in range(APPLES):
        add(f"earth_apple_{v}", lambda v=v: oak(10 + v, APPLE, tall=96 + 8 * v, fruit=hexes("c8322c", "f07a5a"), turn=2))
    for v in range(DEADS):
        add(f"earth_dead_tree_{v}", lambda v=v: dead_tree(v))
    for v in range(SHRUBS):
        add(f"earth_shrub_{v}", lambda v=v: shrub(v))
    add("earth_willow", lambda: willow(0))
    add("earth_cottage_ruin", lambda: cottage(0), sink=PLINTH)
    add("earth_cottage", lambda: cottage(1), sink=PLINTH)
    add("earth_cottage_glow", cottage_glow, ORDER_GLOW, lit=False, sink=PLINTH)
    add("earth_chapel", chapel, sink=PLINTH)
    add("earth_well", well)
    add("earth_windmill", windmill, sink=2 * TILE)
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
    for v in range(2):
        add(f"earth_lilies_{v}", lambda v=v: lilies(v), ORDER_WATER, sink=0)
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
    return plan


PLAN = _plan()
PICTURE_KEYS = [key for key, *_ in PLAN]
ENTRIES = [_picture(key, order, lit) for key, _, order, lit, _ in PLAN]
SINK_OF = {key: sink for key, *_, sink in PLAN}
_DRAWN: dict[str, Image.Image] = {}


def pictures() -> dict[str, Image.Image]:
    """Every picture of the Earth, drawn (once), by key."""
    if not _DRAWN:
        for key, paint, *_ in PLAN:
            _DRAWN[key] = paint()
    return dict(_DRAWN)


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
]
