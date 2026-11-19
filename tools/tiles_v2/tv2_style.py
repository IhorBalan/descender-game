"""The second style of Tiles V2: a thing is drawn as a height field with a material at each
pixel (how far each pixel stands out, and what it is made of); its shade is the one light
on the normals of that field, stepped into a ramp of the material's own whose tones shift
in hue (cool in shadow, warm in light), darker in hollows and where something stands
between it and the light; its outline is selective; and it gives its own normal map, so
the lamps of the world light its relief (docs/art/tiles-v2.md, "The second style").

A painter here returns (picture, normal map), both at the size the art ships. A module
names its painters in its STYLE (key -> callable), which the generator prefers to FINE.
"""
from __future__ import annotations

import colorsys
import math
import random

import numpy as np
from PIL import Image

LIGHT = np.array([-0.5, -0.6, 0.62])
LIGHT = LIGHT / np.linalg.norm(LIGHT)  # the one light: from the upper left, a little toward the eye
SKY_RIM = np.array([168, 200, 240], dtype=float)  # the cool rim where a shape away from the light meets the sky


def hex_rgb(value) -> tuple[int, int, int]:
    if isinstance(value, str):
        return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))
    return tuple(int(c) for c in value[:3])


def _toward(h: float, target: float, amount: float) -> float:
    d = ((target - h + 0.5) % 1.0) - 0.5
    return (h + d * amount) % 1.0


def make_ramp(base, n: int = 10, cool: float = 0.32, warm: float = 0.22, anchor: float = 0.55, low: float = 0.07, high: float = 0.9) -> list[tuple[int, int, int]]:
    """A ramp of `n` tones round a base colour (at `anchor` of the way up): the darker tones
    turn toward blue-violet and gain a little saturation, the lighter toward warm yellow and
    lose some. A dark is never the base colour made black."""
    r, g, b = (c / 255 for c in hex_rgb(base))
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    out = []
    for i in range(n):
        t = i / (n - 1)
        if t <= anchor:
            k = t / anchor
            light = low + (l - low) * k**0.9
            hue = _toward(h, 0.70, cool * (1 - k) ** 1.3)
            sat = min(1.0, s * (1.0 + 0.25 * (1 - k)))
        else:
            k = (t - anchor) / (1 - anchor)
            light = l + (high - l) * k**1.05
            hue = _toward(h, 0.14, warm * k)
            sat = max(0.0, s * (1.0 - 0.45 * k))
        rr, gg, bb = colorsys.hls_to_rgb(hue, light, sat)
        out.append((int(round(rr * 255)), int(round(gg * 255)), int(round(bb * 255))))
    return out


def blur(a: np.ndarray, r: int) -> np.ndarray:
    """A box blur of radius r (edges clamped), by summed areas."""
    if r <= 0:
        return a.astype(np.float64)
    p = np.pad(a, r + 1, mode="edge").astype(np.float64)
    c = p.cumsum(0).cumsum(1)
    h, w = a.shape
    k = 2 * r + 1
    y0, x0 = np.arange(h), np.arange(w)
    s = c[y0[:, None] + k, x0[None, :] + k] - c[y0[:, None], x0[None, :] + k] - c[y0[:, None] + k, x0[None, :]] + c[y0[:, None], x0[None, :]]
    return s / (k * k)


def value_noise(w: int, h: int, scale: float, seed) -> np.ndarray:
    """Smooth noise -1 .. 1 with features about `scale` px across, the same for the same seed."""
    r = random.Random(str(seed))
    gw, gh = int(w / scale) + 3, int(h / scale) + 3
    grid = np.array([[r.uniform(-1, 1) for _ in range(gw)] for _ in range(gh)])
    ys, xs = np.mgrid[0:h, 0:w] / scale
    x0, y0 = xs.astype(int), ys.astype(int)
    fx, fy = xs - x0, ys - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = grid[y0, x0] * (1 - fx) + grid[y0, x0 + 1] * fx
    b = grid[y0 + 1, x0] * (1 - fx) + grid[y0 + 1, x0 + 1] * fx
    return a * (1 - fy) + b * fy


class Scene:
    """A height field with a material and a tone offset at each pixel, painted primitive by
    primitive with a depth test: what stands further out is in front."""

    def __init__(self, w: int, h: int):
        self.w, self.h = w, h
        self.H = np.full((h, w), -1e9)
        self.mat = np.full((h, w), -1, dtype=int)
        self.var = np.zeros((h, w), dtype=int)
        self.ys, self.xs = np.mgrid[0:h, 0:w].astype(float)

    def paint(self, mask: np.ndarray, height, mat: int, var=0, force: bool = False, at: tuple[int, int] = (0, 0)):
        """Paints `mask` (the whole scene, or a block of it whose top left is `at`, y then x)."""
        y0, x0 = at
        win = (slice(y0, y0 + mask.shape[0]), slice(x0, x0 + mask.shape[1]))
        H, M, V = self.H[win], self.mat[win], self.var[win]
        height = np.broadcast_to(np.asarray(height, dtype=float), mask.shape)
        put = mask & ((height > H) | force)
        H[put] = height[put]
        M[put] = mat
        V[put] = np.broadcast_to(np.asarray(var), mask.shape)[put]

    @property
    def solid(self) -> np.ndarray:
        return self.mat >= 0

    def limb(self, x0, y0, x1, y1, r0, r1, z, mat: int, var=0):
        """A round limb from one point to another, its radius going from r0 to r1."""
        vx, vy = x1 - x0, y1 - y0
        L2 = max(vx * vx + vy * vy, 1e-6)
        k = np.clip(((self.xs - x0) * vx + (self.ys - y0) * vy) / L2, 0, 1)
        d = np.hypot(self.xs - (x0 + vx * k), self.ys - (y0 + vy * k))
        rr = r0 + (r1 - r0) * k
        self.paint(d < rr, np.sqrt(np.clip(rr * rr - d * d, 0, None)) + z, mat, var)


def normals(H: np.ndarray, solid: np.ndarray, clamp: float = 3.0) -> np.ndarray:
    fill = H[solid].min() if solid.any() else 0.0
    Hf = np.where(solid, H, fill)
    gy, gx = np.gradient(Hf)
    gx, gy = np.clip(gx, -clamp, clamp), np.clip(gy, -clamp, clamp)
    n = np.stack([-gx, -gy, np.ones_like(gx)], axis=-1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def cast_shadow(H: np.ndarray, solid: np.ndarray, reach: int = 10, drop: float = 0.9, soft: int = 1) -> np.ndarray:
    """How much each pixel lies in the shadow of what stands further out up and to the left
    of it (toward the light): 0 .. 1."""
    Hf = np.where(solid, H, -1e9)
    shadow = np.zeros(H.shape)
    for k in range(1, reach + 1):
        dx, dy = int(round(k * 0.62)), int(round(k * 0.78))
        src = np.roll(np.roll(Hf, dy, 0), dx, 1)
        src[:dy, :] = -1e9
        src[:, :dx] = -1e9
        shadow = np.maximum(shadow, ((src - k * drop) > Hf + 0.5).astype(float))
    return np.where(solid, blur(shadow, soft), 0)


def render(scene: Scene, ramps: dict, *, ao: float = 0.04, ao_radius: int = 5, rim: bool = True, outline: bool = True, lo: float = 0.08, hi: float = 0.95, gamma: float = 1.25, shadow: float = 0.3, shadow_reach: int = 12, shadow_drop: float = 0.7, extra_shadow=None) -> tuple[Image.Image, Image.Image]:
    """The picture baked in the one light, and its normal map (Unity's: green is up). A face
    square to the eye sits a little over the middle of its ramp."""
    solid = scene.solid
    N = normals(scene.H, solid)
    d = np.clip(N @ LIGHT, 0, 1)
    v = lo + (hi - lo) * d**gamma
    if shadow:
        v -= shadow * cast_shadow(scene.H, solid, shadow_reach, shadow_drop)
    base = scene.H[solid].min() if solid.any() else 0.0
    Hf = np.where(solid, scene.H, base)
    v -= np.clip((blur(Hf, ao_radius) - Hf) * ao, 0, 0.45)
    if extra_shadow is not None:
        v -= extra_shadow
    v = np.clip(v, 0, 1)
    out = np.zeros((scene.h, scene.w, 4), dtype=np.uint8)
    for m, ramp in ramps.items():
        sel = scene.mat == m
        if not sel.any():
            continue
        n = len(ramp)
        idx = np.clip(np.round(v * (n - 1)).astype(int) + scene.var, 0, n - 1)
        cols = np.array(ramp, dtype=np.uint8)[idx]
        out[sel, :3] = cols[sel]
        out[sel, 3] = 255
    if rim:
        edge = solid & ~(np.roll(solid, -1, 1) & np.roll(solid, -1, 0) & np.roll(solid, 1, 1))
        sel = edge & (N[..., 0] > 0.25) & (d < 0.45)
        out[sel, :3] = (out[sel, :3] * 0.55 + SKY_RIM * 0.45).astype(np.uint8)
    if outline:
        grow = solid.copy()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            grow |= np.roll(np.roll(solid, dy, 0), dx, 1)
        ring = grow & ~solid
        for y, x in zip(*np.nonzero(ring)):
            lit, mat = 0.0, -1
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < scene.h and 0 <= xx < scene.w and solid[yy, xx]:
                    lit = max(lit, d[yy, xx] if dy != -1 else d[yy, xx] * 0.6)  # what is under it is in shadow
                    mat = scene.mat[yy, xx]
            if mat < 0 or lit > 0.86:
                continue  # a lit edge against the sky has no line
            ramp = ramps[mat]
            out[y, x, :3] = ramp[0] if lit < 0.5 else ramp[1] if lit < 0.72 else ramp[2]
            out[y, x, 3] = 255
    nm = np.zeros((scene.h, scene.w, 4), dtype=np.uint8)
    nm[..., 0] = np.round((N[..., 0] * 0.5 + 0.5) * 255)
    nm[..., 1] = np.round((-N[..., 1] * 0.5 + 0.5) * 255)
    nm[..., 2] = np.round((N[..., 2] * 0.5 + 0.5) * 255)
    nm[..., 3] = 255
    flat = out[..., 3] == 0  # where nothing is, the normal faces the eye
    nm[flat, 0], nm[flat, 1], nm[flat, 2] = 128, 128, 255
    return Image.fromarray(out, "RGBA"), Image.fromarray(nm, "RGBA")




# --- Trees -----------------------------------------------------------------------------------

FRAMES = 4  # the frames of a crown that stirs


def leafy_heap(s: Scene, r: random.Random, hx: float, hy: float, hr: float, hz: float, leaf: float, mat: int):
    """A heap of leaves at (hx, hy), `hr` across, standing `hz` out: a heart under them, then
    single leaves, low bumps on its round, each turned out from its middle."""
    xs, ys, W, H, LEAF = s.xs, s.ys, s.w, s.h, mat
    core = ((xs + 0.5 - hx) / (hr * 0.95)) ** 2 + ((ys + 0.5 - hy) / (hr * 0.76)) ** 2
    hd = np.clip(1 - ((xs + 0.5 - hx) / (hr * 1.08)) ** 2 - ((ys + 0.5 - hy) / (hr * 0.86)) ** 2, 0, 1)
    s.paint(core <= 1, hz + np.sqrt(hd) * hr * 0.9 - 0.5, LEAF, var=-1)
    for _ in range(int(hr * hr / (leaf * leaf * 0.55))):
        a, d = r.uniform(0, 2 * math.pi), hr * math.sqrt(r.random())
        lx, ly = hx + math.cos(a) * d * 1.08, hy + math.sin(a) * d * 0.86
        rl = leaf * r.uniform(0.8, 1.25)
        ang = math.atan2(ly - hy, lx - hx) + r.uniform(-0.6, 0.6)
        var = r.choice([-1, 0, 0, 0, 0, 1])
        ca, sa = math.cos(ang), math.sin(ang)
        x0, x1 = max(int(lx - rl * 1.3 - 2), 0), min(int(lx + rl * 1.3 + 3), W)
        y0, y1 = max(int(ly - rl * 1.3 - 2), 0), min(int(ly + rl * 1.3 + 3), H)
        if x1 <= x0 or y1 <= y0:
            continue
        sx, sy = xs[y0:y1, x0:x1], ys[y0:y1, x0:x1]
        du, dv = sx + 0.5 - lx, sy + 0.5 - ly
        pu, pv = du * ca + dv * sa, -du * sa + dv * ca
        shape = (pu / (rl * 1.3)) ** 2 + (pv / (rl * 0.75)) ** 2
        hd = np.clip(1 - ((sx + 0.5 - hx) / (hr * 1.08)) ** 2 - ((sy + 0.5 - hy) / (hr * 0.86)) ** 2, 0, 1)
        s.paint(shape <= 1, hz + np.sqrt(hd) * hr * 0.9 + np.sqrt(np.clip(1 - shape, 0, 1)) * 1.6, LEAF, var=var, at=(y0, x0))


def tree(*args, **kwargs) -> tuple[Image.Image, Image.Image]:
    """A broad tree (tree_scene), baked in the one light: the picture and its normal map."""
    s, ramps, unit = tree_scene(*args, **kwargs)
    return render(s, ramps, ao=0.03, ao_radius=max(3, int(unit * 0.07)), shadow=0.32, shadow_reach=max(6, int(unit * 0.14)), shadow_drop=0.7, gamma=1.3)


def tree_scene(width: int, height: int, sink: int, seed, leaves, bark, *, frame: int | None = None, pale: bool = False, fruit=None, petals: bool = False, crown: float = 1.0, lean: float = 0.0, spread: float = 0.4, girth: float = 1.0, roots: bool = False, leaf: float | None = None):
    """A broad tree on its floor line (`sink` px over the picture's foot), filling a picture
    of `width` by `height`: a round trunk that flares at its foot, limbs that part under the
    crown and show between its heaps, and a crown of heaps of leaves, each a round mass the
    light shapes with single leaves on it, the upper heaps shading the lower. Its bark is in
    furrows, or `pale` with short dark marks across it (a birch's). `frame` is the frame of
    a crown that stirs: each heap goes round a little in its own time, the trunk still.
    `fruit` (a colour) hangs fruit on the crown; `petals` lets a few leaves lie on the ground
    by its foot; `crown` scales how much of its height the crown takes; `lean` leans it;
    `spread` is the crown's half width over the picture's; `girth` thickens the trunk;
    `roots` spreads roots over the ground from its foot; `leaf` caps a leaf's size. Returns the scene, its ramps and
    the crown's unit of size."""
    r = random.Random(f"style tree {seed}")
    W, H = width, height
    s = Scene(W, H)
    xs, ys = s.xs, s.ys
    BARK, LEAF, FRUIT = 0, 1, 2
    if pale:
        bark_ramp = make_ramp(bark, 10, cool=0.30, warm=0.10, anchor=0.6, low=0.16, high=0.95)
    else:
        bark_ramp = make_ramp(bark, 10, cool=0.30, warm=0.18, low=0.08, high=0.78)
    # A light crown (blossom, gold) keeps its shadows lighter: its darks are not a green's.
    lightness = colorsys.rgb_to_hls(*(c / 255 for c in hex_rgb(leaves)))[1]
    ramps = {BARK: bark_ramp, LEAF: make_ramp(leaves, 11, cool=0.40, warm=0.30, low=0.06 + 0.4 * max(0.0, lightness - 0.4), high=0.88)}
    if fruit is not None:
        ramps[FRUIT] = make_ramp(fruit, 8, cool=0.3, warm=0.25, low=0.12, high=0.92)
    foot_y = H - sink
    middle = W / 2
    foot_x = middle + r.uniform(-0.03, 0.03) * W
    crown_top = 3
    crown_bottom = crown_top + (foot_y - crown_top) * 0.62 * crown
    crown_cy = (crown_top + crown_bottom) / 2
    crown_rx = W * spread
    crown_ry = (crown_bottom - crown_top) / 2
    unit = min(crown_rx, crown_ry * 1.3)

    def swung(k):
        if frame is None:
            return 0.0, 0.0
        a = frame * 2 * math.pi / FRAMES + k * 1.9
        return round(1.3 * math.sin(a)) * 2.0, round(0.7 * math.cos(a + 0.8)) * 2.0

    # The trunk: a round column that tapers and leans, its foot flaring into roots.
    grain = value_noise(W, H, 6.0, ("bark", seed))
    top_y = crown_cy + crown_ry * 0.25
    t = np.clip((foot_y - ys) / max(foot_y - top_y, 1), 0, 1)
    rad0 = max(5.0, unit * 0.15) * girth
    top_x = middle + lean * unit * 0.3
    cx = foot_x + (top_x - foot_x) * t * t
    flare = np.clip((ys - (foot_y - rad0 * 1.8)) / (rad0 * 1.8), 0, 1) ** 2
    rad = rad0 * (1 - 0.4 * t) + rad0 * 0.9 * flare
    u = (xs + 0.5 - cx) / rad
    inside = (np.abs(u) < 1) & (ys < foot_y + sink * 0.45) & (ys >= top_y)  # into the ground a little
    if pale:  # a birch's bark: smooth, with short dark marks across it
        marks = (np.sin(ys * 0.55 + grain * 2.0) > 0.86) & (np.abs(u + 0.3 * np.sin(ys * 0.13)) < 0.55)
        relief = np.where(marks, -rad0 * 0.12, 0.0)
        var = np.where(marks, -5, np.where(grain > 0.55, 1, 0))
    else:
        furrow = np.abs(np.sin(u * math.pi * 2.4 + grain * 1.1))
        relief = -(1 - furrow) * rad0 * 0.17
        var = np.where(grain > 0.6, 1, 0)
    s.paint(inside, np.sqrt(np.clip(1 - u * u, 0, 1)) * rad + relief, BARK, var=var)
    # The heaps of the crown: a big one in the middle, a ring round it, low ones at its sides,
    # one side of the crown lower than the other.
    low_side = r.choice([-1, 1])
    heaps = [(middle, crown_cy, unit * 0.5, unit * 0.55)]
    ring = r.randint(6, 8)
    for i in range(ring):
        a = -math.pi * (0.05 + 0.9 * i / (ring - 1)) + r.uniform(-0.12, 0.12)
        d = r.uniform(0.55, 0.8)
        hx = middle + math.cos(a) * crown_rx * d
        hy = crown_cy + math.sin(a) * crown_ry * d * 0.95 + crown_ry * 0.15
        heaps.append((hx, hy, unit * r.uniform(0.3, 0.4), unit * r.uniform(0.38, 0.5)))
    for side in (-1, 1):
        drop = 0.75 if side == low_side else 0.5
        heaps.append((middle + side * crown_rx * r.uniform(0.62, 0.78), crown_cy + crown_ry * drop, unit * r.uniform(0.24, 0.3), unit * 0.3))
    heaps.append((middle + r.uniform(-0.1, 0.1) * crown_rx, crown_cy + crown_ry * 0.58, unit * 0.32, unit * 0.2))
    # The limbs: from the trunk's top out to under the heaps, a branch off each.
    for k, (hx, hy, hr, _) in enumerate(heaps):
        if k == 0 or (hy < crown_cy - crown_ry * 0.2 and abs(hx - middle) < crown_rx * 0.3):
            continue
        dx, dy = swung(k)
        x0, y0 = top_x, top_y + 6 + r.uniform(0, rad0)
        x1, y1 = hx + dx, hy + dy + hr * 0.45
        s.limb(x0, y0, x1, y1, rad0 * 0.6, rad0 * 0.25, 2, BARK)
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        bx, by = mx + (x1 - x0) * 0.25, my - unit * 0.2
        if any(((bx - qx) / qr) ** 2 + ((by - qy) / (qr * 0.8)) ** 2 < 0.8 for qx, qy, qr, _ in heaps):  # a branch off it, hidden in the leaves at its tip
            s.limb(mx, my, bx, by, rad0 * 0.25, rad0 * 0.14, 1, BARK)
    # The leaves: on each heap a heart under them, then single leaves, low bumps on its round.
    leaf = min(max(3.0, unit * 0.075), leaf or 1e9)
    for k, (hx, hy, hr, hz) in enumerate(heaps):
        dx, dy = swung(k)
        hx, hy = hx + dx, hy + dy
        leafy_heap(s, r, hx, hy, hr, hz, leaf, LEAF)
    if fruit is not None:
        fr = max(2.4, unit * 0.05)
        for _ in range(int(unit * 0.3)):
            k = r.randrange(1, len(heaps))
            hx, hy, hr, hz = heaps[k]
            dx, dy = swung(k)
            a, d = r.uniform(0, 2 * math.pi), hr * 0.8 * math.sqrt(r.random())
            fx, fy = hx + dx + math.cos(a) * d, hy + dy + math.sin(a) * d * 0.8 + hr * 0.15
            q = ((xs + 0.5 - fx) / fr) ** 2 + ((ys + 0.5 - fy) / fr) ** 2
            s.paint(q <= 1, hz + hr * 0.95 + np.sqrt(np.clip(1 - q, 0, 1)) * fr * 1.2, FRUIT, force=True)
    if petals:  # a few leaves already let go, lying on the ground by its foot
        for _ in range(int(unit * 0.5)):
            px = int(foot_x + r.uniform(-1, 1) * crown_rx)
            py = int(foot_y - r.randint(0, 2))
            if 0 <= px < W - 2 and 0 <= py < H:
                s.paint(np.ones((1, 2), dtype=bool), 0.5, LEAF, var=r.choice([0, 1, 2]), at=(py, px))
    if roots:  # roots like walls, out over the ground from its foot
        for side in (-1, 1):
            for k in range(3):
                x0, y0 = foot_x + side * rad0 * 0.5, foot_y - rad0 * (1.4 - 0.3 * k)
                x1, y1 = foot_x + side * rad0 * (1.7 + 0.8 * k), foot_y + 2 - k
                s.limb(x0, y0, x1, y1, rad0 * (0.5 - 0.1 * k), rad0 * 0.12, 1 + k, BARK)
    return s, ramps, unit


def pine(width: int, height: int, sink: int, seed, needles, bark) -> tuple[Image.Image, Image.Image]:
    """A fir on its floor line: tiers of drooping boughs, narrow at the top and broad at the
    foot, each tier a skirt round the trunk whose needles run out and down in short strokes,
    its hem ragged; each tier shades the top of the one under it; a bit of trunk under the
    lowest; a leader at the top."""
    r = random.Random(f"style pine {seed}")
    W, H = width, height
    s = Scene(W, H)
    xs, ys = s.xs, s.ys
    BARK, NEEDLE = 0, 1
    ramps = {BARK: make_ramp(bark, 9, cool=0.3, warm=0.18, low=0.08, high=0.72), NEEDLE: make_ramp(needles, 11, cool=0.42, warm=0.26, low=0.05, high=0.7)}
    foot_y = H - sink
    cx = W / 2
    top = 6
    skirt_foot = foot_y - max(10, (foot_y - top) * 0.08)
    trunk_r = max(4.0, W * 0.05)
    u = (xs + 0.5 - cx) / trunk_r
    grain = value_noise(W, H, 4.0, ("pine bark", seed))
    s.paint((np.abs(u) < 1) & (ys >= top + 20) & (ys < foot_y + sink * 0.45), np.sqrt(np.clip(1 - u * u, 0, 1)) * trunk_r - (grain > 0.4) * 0.8, BARK, var=np.where(grain > 0.6, 1, 0))
    tiers = r.randint(7, 9)
    span = skirt_foot - top
    jag = value_noise(W, H, 2.2, ("pine hem", seed))
    for i in range(tiers):
        k = (i + 1) / tiers
        y1 = top + span * (k ** 0.92)  # the tier's hem
        y0 = top + span * (max(0.0, i - 0.9) / tiers) ** 0.92 if i else top  # its top, under the tier above
        half = W * 0.46 * (0.18 + 0.82 * k) * r.uniform(0.94, 1.0)
        rows = np.clip((ys - y0) / max(y1 - y0, 1), 0, 1)
        reach = half * (0.25 + 0.75 * rows ** 0.75)
        dx = xs + 0.5 - cx
        ragged = jag * 2.5 + np.sin(dx * 0.9 + i) * 1.5
        inside = (ys >= y0) & (ys < y1 + ragged) & (np.abs(dx) < reach)
        q = np.clip(np.abs(dx) / np.maximum(reach, 1), 0, 1)
        needle = np.sin((np.abs(dx) * 0.8 - (ys - y0) * 1.1) * 1.3 + jag * 2.0)  # strokes out and down
        # A bough comes out toward the eye down its length, so its hem stands over the top of
        # the tier under it and shades it.
        z = 4 + (tiers - i) * 2.0 + np.sqrt(np.clip(1 - q * q, 0, 1)) * half * 0.4 + rows ** 1.5 * half * 0.3 + needle * 0.7
        s.paint(inside, z, NEEDLE, var=np.where(needle > 0.75, 1, 0), force=False)
    # The leader: a last shoot at the very top.
    lead = (np.abs(xs + 0.5 - cx) < 1.6) & (ys >= top - 5) & (ys < top + 8)
    s.paint(lead, 12 + tiers, NEEDLE)
    return render(s, ramps, ao=0.05, ao_radius=4, shadow=0.42, shadow_reach=10, shadow_drop=0.5, gamma=1.35)


def willow(width: int, height: int, sink: int, seed, leaves, bark) -> tuple[Image.Image, Image.Image]:
    """A weeping willow: a broad tree whose trunk bends, its crown low and flat, and from
    under it strands of leaves that fall nearly to the ground, each a string of small
    leaves that sways a little down its length, in front of the trunk and behind it."""
    s, ramps, unit = tree_scene(width, height, sink, seed, leaves, bark, crown=0.62, lean=1.6, spread=0.36, girth=0.9)
    r = random.Random(f"style willow {seed}")
    LEAF = 1
    W, H = width, height
    foot_y = H - sink
    solid = s.mat == LEAF
    cols = np.nonzero(solid.any(0))[0]
    x0, x1 = cols.min(), cols.max()
    leaf = max(1.8, unit * 0.032)
    for x in range(x0 + 2, x1 - 1, 3):
        rows = np.nonzero(solid[:, x])[0]
        if not len(rows):
            continue
        start = rows.max() - r.randint(4, 14)
        end = int(start + (foot_y - start) * r.uniform(0.55, 0.92))
        z = r.uniform(4, 16) if r.random() < 0.6 else r.uniform(-4, 2)
        phase = r.uniform(0, 6.3)
        y = start
        while y < end:
            sx = x + math.sin((y - start) / 11.0 + phase) * 2.2
            rl = leaf * r.uniform(0.8, 1.2)
            a0, a1 = max(int(sx - rl - 2), 0), min(int(sx + rl + 3), W)
            b0, b1 = max(int(y - rl * 1.6 - 2), 0), min(int(y + rl * 1.6 + 3), H)
            px, py = s.xs[b0:b1, a0:a1] + 0.5 - sx, s.ys[b0:b1, a0:a1] + 0.5 - y
            q = (px / (rl * 0.75)) ** 2 + (py / (rl * 1.6)) ** 2
            s.paint(q <= 1, z + 2 + np.sqrt(np.clip(1 - q, 0, 1)) * 1.4, LEAF, var=r.choice([-1, 0, 0, 1]), at=(b0, a0))
            y += rl * 1.7
    return render(s, ramps, ao=0.03, ao_radius=max(3, int(unit * 0.07)), shadow=0.3, shadow_reach=max(6, int(unit * 0.12)), shadow_drop=0.7, gamma=1.3)


def bare_tree(width: int, height: int, sink: int, seed, wood) -> tuple[Image.Image, Image.Image]:
    """A dead tree: a grey trunk, split and weathered, that forks and forks again into bare
    limbs, each thinner than the one it grew from, out to twigs a pixel or two wide, a few
    of them broken off short."""
    r = random.Random(f"style bare tree {seed}")
    W, H = width, height
    s = Scene(W, H)
    WOOD = 0
    ramps = {WOOD: make_ramp(wood, 9, cool=0.3, warm=0.14, low=0.08, high=0.8)}
    foot_y = H - sink
    cx = W / 2 + r.uniform(-0.04, 0.04) * W
    grain = value_noise(W, H, 3.0, ("dead grain", seed))
    trunk_r = max(5.0, W * 0.05)
    fork_y = foot_y - (foot_y - 8) * r.uniform(0.38, 0.48)
    t = np.clip((foot_y - s.ys) / max(foot_y - fork_y, 1), 0, 1)
    rad = trunk_r * (1 - 0.25 * t) + trunk_r * 0.6 * np.clip((s.ys - (foot_y - trunk_r * 1.5)) / (trunk_r * 1.5), 0, 1) ** 2
    u = (s.xs + 0.5 - cx) / rad
    split = np.abs(np.sin(u * math.pi * 1.8 + grain * 0.8))
    s.paint((np.abs(u) < 1) & (s.ys >= fork_y - 2) & (s.ys < foot_y + sink * 0.45), np.sqrt(np.clip(1 - u * u, 0, 1)) * rad - (1 - split) * trunk_r * 0.2, WOOD, var=np.where(grain > 0.5, 1, 0))

    def grow(x, y, angle, length, radius, depth, z):
        x1, y1 = x + math.cos(angle) * length, y + math.sin(angle) * length
        x1 = min(max(x1, 4), W - 5)
        y1 = max(y1, 4)
        s.limb(x, y, x1, y1, radius, max(0.7, radius * 0.62), z, WOOD)
        if depth == 0 or radius < 1.0:
            return
        if depth < 3 and r.random() < 0.2:
            return  # broken off short
        for turn in (-1, 1):
            grow(x1, y1, angle + turn * r.uniform(0.3, 0.6), length * r.uniform(0.6, 0.78), radius * 0.62, depth - 1, z + r.uniform(-0.5, 1))

    grow(cx, fork_y + 2, -math.pi / 2 - r.uniform(0.35, 0.55), (fork_y - 8) * 0.38, trunk_r * 0.72, 4, 2)
    grow(cx, fork_y + 2, -math.pi / 2 + r.uniform(0.35, 0.55), (fork_y - 8) * 0.36, trunk_r * 0.7, 4, 2.5)
    grow(cx, fork_y + (foot_y - fork_y) * 0.3, -math.pi / 2 + r.choice([-1, 1]) * 1.0, (fork_y - 8) * 0.24, trunk_r * 0.4, 3, 1)
    return render(s, ramps, ao=0.04, ao_radius=3, shadow=0.25, shadow_reach=6, shadow_drop=0.6, gamma=1.2)
