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


# --- What grows low, and wood and stone on the ground ------------------------------------------


def _rings(s: Scene, cx: float, cy: float, rx: float, ry: float, base: float, mat: int, tilt: float = 1.4, step: float = 2.2):
    """The cut face of wood seen a little from above: an ellipse that faces up, in rings."""
    q = ((s.xs + 0.5 - cx) / rx) ** 2 + ((s.ys + 0.5 - cy) / ry) ** 2
    d = np.sqrt(q) * rx
    ring = (np.sin(d / step * math.pi) > 0.55) & (q < 0.85)
    s.paint(q <= 1, base + (s.ys + 0.5 - cy) * tilt, mat, var=np.where(ring, -1, np.where(q < 0.04, -1, 1)), force=True)


def bush(width: int, height: int, sink: int, seed, leaves, *, berries=None) -> tuple[Image.Image, Image.Image]:
    """A bush on its floor line: heaps of small leaves side by side, a few higher at the
    back; `berries` (a colour) dots it with berries."""
    r = random.Random(f"style bush {seed}")
    W, H = width, height
    s = Scene(W, H)
    LEAF, BERRY = 0, 1
    lightness = colorsys.rgb_to_hls(*(c / 255 for c in hex_rgb(leaves)))[1]
    ramps = {LEAF: make_ramp(leaves, 10, cool=0.4, warm=0.3, low=0.06 + 0.4 * max(0.0, lightness - 0.4), high=0.85)}
    if berries is not None:
        ramps[BERRY] = make_ramp(berries, 7, cool=0.3, warm=0.25, low=0.15, high=0.92)
    foot_y = H - sink
    tall = foot_y - 2
    hr = min(W * 0.2, tall * 0.42)
    n = max(2, int(round((W - 4) / (hr * 1.5))))
    leaf = max(2.4, hr * 0.13)
    heaps = []
    for i in range(n):  # the back row, higher
        x = 4 + hr + (W - 8 - 2 * hr) * (i + 0.5) / n + r.uniform(-2, 2)
        heaps.append((x, foot_y - tall + hr * 0.95 + r.uniform(0, 3), hr * r.uniform(0.75, 0.9), 1.0))
    for i in range(n + 1):  # the front row, on the ground
        x = 3 + hr * 0.9 + (W - 6 - 1.8 * hr) * i / max(n, 1) + r.uniform(-2, 2)
        heaps.append((min(max(x, hr), W - hr), foot_y - hr * 0.78, hr * r.uniform(0.85, 1.0), 4.0))
    for hx, hy, rr, hz in heaps:
        leafy_heap(s, r, hx, hy, rr, hz, leaf, LEAF)
    if berries is not None:
        br = max(1.6, hr * 0.08)
        for _ in range(int(W * 0.15)):
            hx, hy, rr, hz = heaps[r.randrange(len(heaps))]
            a, d = r.uniform(0, 2 * math.pi), rr * 0.8 * math.sqrt(r.random())
            bx, by = hx + math.cos(a) * d, hy + math.sin(a) * d * 0.8
            q = ((s.xs + 0.5 - bx) / br) ** 2 + ((s.ys + 0.5 - by) / br) ** 2
            s.paint(q <= 1, hz + rr + np.sqrt(np.clip(1 - q, 0, 1)) * br, BERRY, force=True)
    s.mat[foot_y + 1 :, :] = -1  # nothing under the ground
    return render(s, ramps, ao=0.04, ao_radius=3, shadow=0.3, shadow_reach=6, shadow_drop=0.7, gamma=1.3)


def stump(width: int, height: int, sink: int, seed, bark, *, axe: bool = False) -> tuple[Image.Image, Image.Image]:
    """A stump: a short round trunk in furrows, flaring into roots, cut flat on top with its
    rings showing; `axe` leaves an axe stuck in its top."""
    r = random.Random(f"style stump {seed}")
    W, H = width, height
    s = Scene(W, H)
    BARK, WOOD, IRON = 0, 1, 2
    ramps = {
        BARK: make_ramp(bark, 9, cool=0.3, warm=0.18, low=0.08, high=0.75),
        WOOD: make_ramp("b88a54", 8, cool=0.25, warm=0.2, low=0.2, high=0.85),
        IRON: make_ramp("6a6e78", 7, cool=0.2, warm=0.1, low=0.08, high=0.9),
    }
    foot_y = H - sink
    cx = W / 2
    rad = W * 0.34
    top = foot_y - (foot_y - 2) * 0.62
    grain = value_noise(W, H, 3.0, ("stump", seed))
    flare = np.clip((s.ys - (foot_y - rad * 0.8)) / (rad * 0.8), 0, 1) ** 2
    rr = rad * (1 + 0.3 * flare)
    u = (s.xs + 0.5 - cx) / rr
    furrow = np.abs(np.sin(u * math.pi * 2.2 + grain))
    s.paint((np.abs(u) < 1) & (s.ys >= top) & (s.ys < foot_y + 2), np.sqrt(np.clip(1 - u * u, 0, 1)) * rr - (1 - furrow) * 1.2, BARK, var=np.where(grain > 0.5, 1, 0))
    _rings(s, cx, top, rad, rad * 0.32, rad + 2, WOOD)
    if axe:  # its head bitten into the top, its haft up and out to one side
        hx = cx + rad * 0.2
        s.limb(hx, top - 1, hx + rad * 0.9, top - rad * 1.5, 1.6, 1.4, rad + 8, WOOD)
        blade = (np.abs(s.xs + 0.5 - (hx - 1)) < rad * 0.35) & (s.ys >= top - rad * 0.45) & (s.ys < top + 1)
        s.paint(blade, rad + 7 + (s.xs - hx) * -0.2, IRON, force=True)
    s.mat[foot_y + 2 :, :] = -1
    return render(s, ramps, ao=0.05, ao_radius=2, shadow=0.3, shadow_reach=5, shadow_drop=0.6, gamma=1.2)


def log(width: int, height: int, sink: int, seed, bark) -> tuple[Image.Image, Image.Image]:
    """A felled log lying along the ground: round, its bark in long furrows, its near end
    sawn with the rings showing, a shoot of green out of its top."""
    r = random.Random(f"style log {seed}")
    W, H = width, height
    s = Scene(W, H)
    BARK, WOOD, LEAF = 0, 1, 2
    ramps = {
        BARK: make_ramp(bark, 9, cool=0.3, warm=0.18, low=0.08, high=0.75),
        WOOD: make_ramp("c09060", 8, cool=0.25, warm=0.2, low=0.2, high=0.85),
        LEAF: make_ramp("5a9a3a", 7, cool=0.4, warm=0.3, low=0.1, high=0.85),
    }
    foot_y = H - sink
    rad = min((foot_y - 4) * 0.5, H * 0.4)
    cy = foot_y - rad
    x0, x1 = 4 + rad * 0.3, W - 4 - rad * 0.3
    grain = value_noise(W, H, 5.0, ("log", seed))
    v = (s.ys + 0.5 - cy) / rad
    furrow = np.abs(np.sin(v * math.pi * 2.6 + grain * 1.2))
    body = (np.abs(v) < 1) & (s.xs >= x0) & (s.xs < x1)
    s.paint(body, np.sqrt(np.clip(1 - v * v, 0, 1)) * rad - (1 - furrow) * 1.4, BARK, var=np.where(grain > 0.55, 1, 0))
    # The sawn end: the rings on a face turned to the eye and a little to the right.
    q = ((s.xs + 0.5 - x1) / (rad * 0.36)) ** 2 + v**2
    d = np.sqrt(q)
    s.paint(q <= 1, rad + 1 + (s.xs - x1) * 0.6, WOOD, var=np.where(np.sin(d * rad / 2.2 * math.pi) > 0.55, -1, 1), force=True)
    # A shoot.
    sx = x0 + (x1 - x0) * r.uniform(0.25, 0.45)
    s.limb(sx, cy - rad * 0.9, sx - 2, cy - rad * 1.5, 1.0, 0.8, rad + 2, LEAF)
    for side in (-1, 1):
        lx, ly = sx - 2 + side * 3, cy - rad * 1.5
        q = ((s.xs + 0.5 - lx) / 3.0) ** 2 + ((s.ys + 0.5 - ly) / 1.8) ** 2
        s.paint(q <= 1, rad + 3 + np.sqrt(np.clip(1 - q, 0, 1)), LEAF)
    s.mat[foot_y + 1 :, :] = -1
    return render(s, ramps, ao=0.05, ao_radius=2, shadow=0.3, shadow_reach=5, shadow_drop=0.6, gamma=1.2)


def hanging_roots(width: int, height: int, seed, bark, *, count: int = 4) -> tuple[Image.Image, Image.Image]:
    """Roots hanging out of a cave's roof: strands down from the top edge, thick at the top
    and thin to their tips, wandering a little, with rootlets off them."""
    r = random.Random(f"style roots {seed}")
    W, H = width, height
    s = Scene(W, H)
    ROOT = 0
    ramps = {ROOT: make_ramp(bark, 8, cool=0.3, warm=0.18, low=0.08, high=0.72)}
    for i in range(count):
        x = W * (i + 0.5) / count + r.uniform(-W * 0.08, W * 0.08)
        length = H * r.uniform(0.45, 0.95)
        thick = r.uniform(1.6, 3.2)
        y, steps = 0.0, max(4, int(length / 6))
        phase = r.uniform(0, 6)
        for k in range(steps):
            y1 = length * (k + 1) / steps
            x1 = x + math.sin(y1 / 13 + phase) * 1.5 + r.uniform(-0.8, 0.8)
            t0, t1 = k / steps, (k + 1) / steps
            s.limb(x, y, x1, y1, max(0.6, thick * (1 - t0 * 0.8)), max(0.6, thick * (1 - t1 * 0.8)), 1 + i * 0.3, ROOT)
            if r.random() < 0.18 and t1 < 0.8:
                side = r.choice([-1, 1])
                s.limb(x1, y1, x1 + side * r.uniform(4, 9), y1 + r.uniform(5, 12), max(0.6, thick * 0.4), 0.6, 0.5, ROOT)
            x, y = x1, y1
    return render(s, ramps, ao=0.03, ao_radius=2, shadow=0.2, shadow_reach=4, shadow_drop=0.6, gamma=1.2)


def toadstools(width: int, height: int, sink: int, seed, cap="c43a2c") -> tuple[Image.Image, Image.Image]:
    """A few toadstools: pale stems, round caps with white flecks, each with a gill shadow."""
    r = random.Random(f"style toadstools {seed}")
    W, H = width, height
    s = Scene(W, H)
    STEM, CAP, FLECK = 0, 1, 2
    ramps = {STEM: make_ramp("e2d6bc", 7, cool=0.3, warm=0.12, low=0.25, high=0.95), CAP: make_ramp(cap, 9, cool=0.32, warm=0.22, low=0.1, high=0.82), FLECK: make_ramp("f4f0e6", 5, cool=0.2, warm=0.05, low=0.5, high=0.98)}
    foot_y = H - sink
    n = 3
    for i in range(n):
        x = W * (i + 0.6) / (n + 0.4) + r.uniform(-2, 2)
        tall = (foot_y - 3) * r.uniform(0.55, 1.0)
        cr = max(4.0, W * r.uniform(0.11, 0.15))
        top = foot_y - tall + cr * 0.5
        s.limb(x, foot_y, x, top + cr * 0.3, cr * 0.32, cr * 0.26, 2 + i, STEM)
        q = ((s.xs + 0.5 - x) / cr) ** 2 + ((s.ys + 0.5 - top) / (cr * 0.7)) ** 2
        cap_mask = (q <= 1) & (s.ys < top + cr * 0.25)
        s.paint(cap_mask, 4 + i + np.sqrt(np.clip(1 - q, 0, 1)) * cr * 0.9, CAP, force=True)
        for _ in range(4):
            fx, fy = x + r.uniform(-0.6, 0.6) * cr, top - r.uniform(0.1, 0.55) * cr
            fq = ((s.xs + 0.5 - fx) / 1.3) ** 2 + ((s.ys + 0.5 - fy) / 1.1) ** 2
            s.paint((fq <= 1) & cap_mask, s.H + 0.4, FLECK, force=True)
    s.mat[foot_y + 1 :, :] = -1
    return render(s, ramps, ao=0.04, ao_radius=2, shadow=0.3, shadow_reach=4, shadow_drop=0.6, gamma=1.2)


def boulders(width: int, height: int, sink: int, seed, stone, *, plank=None, count: int = 4, glint: bool = False) -> tuple[Image.Image, Image.Image]:
    """Stones heaped on the ground, each a lump broken into a few faces, the big ones at the
    back; `plank` (a colour) lays a broken plank across them; `glint` lets the lumps shine
    (ore)."""
    r = random.Random(f"style boulders {seed}")
    W, H = width, height
    s = Scene(W, H)
    STONE, WOOD = 0, 1
    ramps = {STONE: make_ramp(stone, 10, cool=0.32, warm=0.16 if not glint else 0.3, low=0.08, high=0.86 if not glint else 0.97)}
    if plank is not None:
        ramps[WOOD] = make_ramp(plank, 8, cool=0.28, warm=0.2, low=0.1, high=0.8)
    foot_y = H - sink
    tall = foot_y - 2
    order = list(range(count))
    r.shuffle(order)  # where along the heap each stone lies, the big ones not all at one end
    for i in range(count):
        big = 1.0 - 0.4 * i / max(count - 1, 1)
        rx = min(W / (count + 0.6), tall * 0.75) * big * r.uniform(0.85, 1.0)
        ry = min(tall * 0.5, rx * 0.8)
        cx = 2 + rx + (W - 4 - 2 * rx) * (order[i] + 0.5) / count + r.uniform(-2, 2)
        cy = foot_y - ry * 0.85
        facets = value_noise(W, H, max(3.0, rx * 0.5), ("facets", seed, i))
        q = ((s.xs + 0.5 - cx) / rx) ** 2 + ((s.ys + 0.5 - cy) / ry) ** 2
        flat = np.round(facets * 3) / 3  # a few planes, not a smooth round
        s.paint(q <= 1, i * 3 + np.sqrt(np.clip(1 - q, 0, 1)) * ry * 0.9 + flat * ry * 0.25, STONE, var=np.where(facets > 0.5, 1, 0))
    if plank is not None:
        x0, y0 = W * 0.18, foot_y - tall * 0.25
        x1, y1 = W * 0.7, foot_y - tall * 0.95
        vx, vy = x1 - x0, y1 - y0
        L = math.hypot(vx, vy)
        along = ((s.xs - x0) * vx + (s.ys - y0) * vy) / L
        across = (-(s.xs - x0) * vy + (s.ys - y0) * vx) / L
        board = (along >= 0) & (along <= L) & (np.abs(across) < 2.6)
        s.paint(board, 3 * count + 4 - np.abs(across) * 0.3, WOOD, var=np.where(np.sin(along * 0.9) > 0.8, -1, 0), force=True)
    s.mat[foot_y + 1 :, :] = -1
    return render(s, ramps, ao=0.05, ao_radius=3, shadow=0.35, shadow_reach=6, shadow_drop=0.6, gamma=1.2)


def topiary(width: int, height: int, sink: int, seed, leaves, bark, *, balls: int = 1, box: bool = False, flowers=None) -> tuple[Image.Image, Image.Image]:
    """A clipped garden shape of small leaves: a box hedge (`box`), or `balls` balls one over
    another on a pale stem; `flowers` (a colour) dots it with blossom."""
    r = random.Random(f"style topiary {seed}")
    W, H = width, height
    s = Scene(W, H)
    LEAF, BARK, FLOWER = 0, 1, 2
    ramps = {LEAF: make_ramp(leaves, 10, cool=0.4, warm=0.3, low=0.07, high=0.86), BARK: make_ramp(bark, 8, cool=0.3, warm=0.1, anchor=0.6, low=0.2, high=0.95)}
    if flowers is not None:
        ramps[FLOWER] = make_ramp(flowers, 6, cool=0.25, warm=0.2, low=0.35, high=0.96)
    foot_y = H - sink
    leaf = 2.6
    if box:
        top, x0, x1 = 3, 3, W - 3
        rr = (foot_y - top) * 0.5
        # A box with rounded shoulders: its face stands out evenly, its top and sides round off.
        ex = np.clip(np.minimum(s.xs + 0.5 - x0, x1 - s.xs - 0.5) / rr, 0, 1)
        ey = np.clip((s.ys + 0.5 - top) / rr, 0, 1)
        inside = (s.xs >= x0) & (s.xs < x1) & (s.ys >= top) & (s.ys <= foot_y)
        shape = np.sqrt(np.clip(1 - (1 - np.minimum(ex, ey)) ** 2, 0, 1)) * rr
        s.paint(inside, shape, LEAF, var=-1)
        for _ in range(int((x1 - x0) * (foot_y - top) / (leaf * leaf * 0.9))):
            lx, ly = r.uniform(x0, x1), r.uniform(top, foot_y)
            ang = r.uniform(0, math.pi)
            ca, sa = math.cos(ang), math.sin(ang)
            a0, a1 = max(int(lx - 5), 0), min(int(lx + 6), W)
            b0, b1 = max(int(ly - 5), 0), min(int(ly + 6), H)
            px, py = s.xs[b0:b1, a0:a1] + 0.5 - lx, s.ys[b0:b1, a0:a1] + 0.5 - ly
            pu, pv = px * ca + py * sa, -px * sa + py * ca
            q = (pu / (leaf * 1.3)) ** 2 + (pv / (leaf * 0.75)) ** 2
            base = s.H[b0:b1, a0:a1]
            ok = (q <= 1) & (s.mat[b0:b1, a0:a1] == LEAF)
            s.paint(ok, np.where(ok, base + np.sqrt(np.clip(1 - q, 0, 1)) * 1.2, 0), LEAF, var=r.choice([-1, 0, 0, 1]), at=(b0, a0), force=True)
        heaps = [((x0 + x1) / 2, (top + foot_y) / 2, (x1 - x0) / 2, 0.0)]
    else:
        stem_top = 4
        rad = min(W * 0.4, (foot_y - stem_top) / (balls * 2.0 + 0.6))
        s.limb(W / 2, foot_y + 1, W / 2, stem_top + rad, max(2.0, rad * 0.14), max(1.6, rad * 0.1), 0, BARK)
        heaps = []
        for i in range(balls):
            cy = stem_top + rad * (1 + 2.1 * i) * (0.85 if balls > 1 else 1.0)
            rr = rad * (0.8 + 0.2 * i / max(balls - 1, 1)) if balls > 1 else rad
            heaps.append((W / 2, cy, rr, 3.0 + i))
        for hx, hy, rr, hz in heaps:
            leafy_heap(s, r, hx, hy, rr, hz, leaf, LEAF)
    if flowers is not None:
        for _ in range(int(W * 0.25)):
            hx, hy, rr, hz = heaps[r.randrange(len(heaps))]
            fx, fy = hx + r.uniform(-0.85, 0.85) * rr, hy + r.uniform(-0.75, 0.75) * rr * (0.8 if not box else 1.6)
            q = ((s.xs + 0.5 - fx) / 1.6) ** 2 + ((s.ys + 0.5 - fy) / 1.4) ** 2
            s.paint((q <= 1) & (s.mat == LEAF), s.H + 0.8, FLOWER, force=True)
    s.mat[foot_y + 1 :, :] = -1
    return render(s, ramps, ao=0.04, ao_radius=3, shadow=0.3, shadow_reach=6, shadow_drop=0.7, gamma=1.3)


# --- Made things: boards, blocks, iron ------------------------------------------------------


def board(s: Scene, x0, y0, x1, y1, z, mat: int, *, bevel: float = 1.6, grain=None, var=0, force: bool = False):
    """A board or block from (x0, y0) to (x1, y1): its face `z` out, its edges bevelled
    `bevel` px; `grain` (a noise field) runs along its long side in streaks."""
    xs, ys = s.xs, s.ys
    inside = (xs >= x0) & (xs < x1) & (ys >= y0) & (ys < y1)
    edge = np.minimum.reduce([xs + 0.5 - x0, x1 - xs - 0.5, ys + 0.5 - y0, y1 - ys - 0.5])
    height = z + np.clip(edge / max(bevel, 0.1), 0, 1) ** 0.8 * bevel
    v = np.broadcast_to(np.asarray(var), xs.shape).copy() if not np.isscalar(var) else np.full(xs.shape, var)
    if grain is not None:
        along_x = (x1 - x0) >= (y1 - y0)
        streak = np.sin((ys if along_x else xs) * 1.7 + grain * 3.0)
        height = height - (streak > 0.7) * 0.5
        v = v + np.where(streak > 0.7, -1, np.where(grain > 0.6, 1, 0))
    s.paint(inside, height, mat, var=v, force=force)


def post(s: Scene, x, y0, y1, r, z, mat: int, var=0):
    """An upright round post from y0 down to y1."""
    u = (s.xs + 0.5 - x) / r
    s.paint((np.abs(u) < 1) & (s.ys >= y0) & (s.ys < y1), z + np.sqrt(np.clip(1 - u * u, 0, 1)) * r, mat, var=var)


def ball(s: Scene, cx, cy, rx, ry, z, depth, mat: int, var=0, force: bool = False):
    q = ((s.xs + 0.5 - cx) / rx) ** 2 + ((s.ys + 0.5 - cy) / ry) ** 2
    s.paint(q <= 1, z + np.sqrt(np.clip(1 - q, 0, 1)) * depth, mat, var=var, force=force)


def nail(s: Scene, x, y, z, mat: int, r: float = 1.0):
    ball(s, x, y, r, r, z, r, mat, force=True)


def _made(W, H, ramps_spec):
    s = Scene(W, H)
    ramps = {}
    for i, (base, n, kw) in enumerate(ramps_spec):
        ramps[i] = make_ramp(base, n, **kw)
    return s, ramps


WOODEN = ("8a5a34", 9, dict(cool=0.28, warm=0.2, low=0.08, high=0.8))
GREYWOOD_ = ("7a7064", 9, dict(cool=0.3, warm=0.14, low=0.08, high=0.8))
IRON_ = ("5a5e68", 8, dict(cool=0.22, warm=0.1, low=0.05, high=0.88))
STONE_ = ("8a8890", 10, dict(cool=0.32, warm=0.14, low=0.08, high=0.88))
STRAW = ("c8a040", 9, dict(cool=0.3, warm=0.25, low=0.1, high=0.92))
ROOFRED = ("a8442c", 9, dict(cool=0.3, warm=0.22, low=0.08, high=0.82))


def _finish(s, ramps, foot_y, **kw):
    s.mat[foot_y + 1 :, :] = -1
    opts = dict(ao=0.05, ao_radius=3, shadow=0.32, shadow_reach=6, shadow_drop=0.6, gamma=1.2)
    opts.update(kw)
    return render(s, ramps, **opts)


def fence(width: int, height: int, sink: int, seed, kind: int) -> tuple[Image.Image, Image.Image]:
    """A length of wooden fence: pickets on two rails (0), a gap with a fallen rail (1), or
    pickets leaning over (2); each picket a board with a point, grain and a nail at each rail."""
    r = random.Random(f"style fence {seed}")
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN, IRON_])
    WOOD, IRON = 0, 1
    foot_y = H - sink
    grain = value_noise(W, H, 4.0, ("fence", seed))
    top = foot_y - (foot_y - 4) * 0.95
    rails = (top + (foot_y - top) * 0.28, foot_y - (foot_y - top) * 0.28)
    for ry in rails:  # the rails, behind the pickets
        board(s, 1, ry - 3, W - 1, ry + 3, 1, WOOD, grain=grain, var=-1)
    n = 9
    pitch = (W - 2) / n
    for i in range(n):
        if kind == 1 and 3 <= i <= 5:
            continue
        x0 = 1 + i * pitch + 1
        x1 = x0 + pitch - 4.5  # a gap between pickets, the rails behind it
        lean = (i - 4) * 2.2 if kind == 2 else 0.0
        tall = top + r.uniform(-2, 3)
        for y in range(int(tall), foot_y + 1):
            t = (foot_y - y) / max(foot_y - tall, 1)
            dx = lean * t
            point = max(0.0, (y - tall) / ((x1 - x0) * 0.5))  # the cut point at its top
            half = (x1 - x0) / 2 * min(1.0, point)
            mid = (x0 + x1) / 2 + dx
            row = slice(y, y + 1)
            xs = s.xs[row]
            inside = np.abs(xs + 0.5 - mid) < half
            edge = half - np.abs(xs + 0.5 - mid)
            s.paint(inside, 4 + np.clip(edge / 1.5, 0, 1) * 1.5 - (np.sin(xs * 2.1 + grain[row] * 3) > 0.75) * 0.5, WOOD, var=np.where(grain[row] > 0.55, 1, 0), at=(y, 0))
        for ry in rails:
            nail(s, (x0 + x1) / 2 + lean * (foot_y - ry) / max(foot_y - tall, 1), ry, 6, IRON, 0.9)
    if kind == 1:  # the rail that fell, across the gap
        gx0, gx1 = 1 + 3 * pitch, 1 + 6 * pitch
        vx, vy = gx1 - gx0, -(foot_y - rails[1]) * 0.6
        L = math.hypot(vx, vy)
        along = ((s.xs - gx0) * vx + (s.ys - (foot_y - 2)) * vy) / L
        across = (-(s.xs - gx0) * vy + (s.ys - (foot_y - 2)) * vx) / L
        s.paint((along >= 0) & (along <= L) & (np.abs(across) < 3), 5 - np.abs(across) * 0.4, WOOD)
    return _finish(s, ramps, foot_y)


def iron_fence(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """Railings of wrought iron: round bars topped with spear points, on two flat rails."""
    W, H = width, height
    s, ramps = _made(W, H, [IRON_])
    IRON = 0
    foot_y = H - sink
    top = 6
    for ry in (top + 10, foot_y - 8):
        board(s, 1, ry - 1.5, W - 1, ry + 1.5, 2, IRON, bevel=1.0)
    n = 12
    for i in range(n):
        x = 1 + (W - 2) * (i + 0.5) / n
        post(s, x, top + 3, foot_y + 1, 1.2, 3, IRON)
        tip = (s.ys >= top - 3) & (s.ys < top + 4) & (np.abs(s.xs + 0.5 - x) < (s.ys - (top - 3)) * 0.45)
        s.paint(tip, 4 - np.abs(s.xs + 0.5 - x) * 0.5, IRON)
    return _finish(s, ramps, foot_y, rim=False)


def crates(width: int, height: int, sink: int, seed, kind: int) -> tuple[Image.Image, Image.Image]:
    """What is left by a store: crates stacked with a barrel (0), sacks with a pick against
    them (1), or a ladder leant on nothing (2)."""
    r = random.Random(f"style crates {seed}")
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN, IRON_, ("b89a62", 9, dict(cool=0.3, warm=0.22, low=0.12, high=0.9))])
    WOOD, IRON, SACK = 0, 1, 2
    foot_y = H - sink
    grain = value_noise(W, H, 4.0, ("crates", seed))
    if kind == 0:
        for x0, y1, size in ((4, foot_y, 30), (34, foot_y, 26), (8, foot_y - 30, 24)):
            board(s, x0, y1 - size, x0 + size, y1, 3, WOOD, bevel=2.2, grain=grain)
            for k in (0.33, 0.66):  # its boards
                yy = y1 - size * k
                s.H[int(yy), int(x0 + 2) : int(x0 + size - 2)] -= 1.5
            s.limb(x0 + 3, y1 - 3, x0 + size - 3, y1 - size + 3, 1.6, 1.6, 5.5, WOOD)  # the brace
        cx, rx = 72, 10
        u = (s.xs + 0.5 - cx) / (rx * (1 + 0.12 * np.sin(np.clip((s.ys - (foot_y - 34)) / 34, 0, 1) * math.pi)))
        s.paint((np.abs(u) < 1) & (s.ys >= foot_y - 34) & (s.ys < foot_y), 2 + np.sqrt(np.clip(1 - u * u, 0, 1)) * rx - (np.sin(u * 9) > 0.8) * 0.6, WOOD, var=np.where(grain > 0.5, 1, 0))
        for hy in (foot_y - 28, foot_y - 6):
            s.paint((np.abs(u) < 1.02) & (np.abs(s.ys + 0.5 - hy) < 1.6), 3 + np.sqrt(np.clip(1 - u * u, 0, 1)) * rx * 1.05, IRON, force=True)
    elif kind == 1:
        for cx, cy, rx, ry in ((22, foot_y - 9, 15, 10), (46, foot_y - 9, 14, 10), (34, foot_y - 24, 13, 10)):
            ball(s, cx, cy, rx, ry, 2, ry * 0.9, SACK, var=np.where(value_noise(W, H, 2.0, ("sack", cx)) > 0.5, 1, 0))
            s.limb(cx - 3, cy - ry + 1, cx + 3, cy - ry + 1, 1.5, 1.5, 2 + ry, SACK)  # its tie
        s.limb(68, foot_y, 80, foot_y - 38, 1.6, 1.6, 4, WOOD)  # the pick's haft
        s.limb(70, foot_y - 42, 90, foot_y - 32, 2.0, 1.0, 6, IRON)
    else:
        x0, x1 = 30, 62
        for side in (0, 1):
            bx = x0 + side * 16
            s.limb(bx, foot_y, bx + 22, 6, 1.8, 1.6, 3, WOOD)
        for k in range(1, 8):
            t = k / 8
            y = foot_y - (foot_y - 6) * t
            s.limb(x0 + 22 * t, y, x0 + 16 + 22 * t, y, 1.4, 1.4, 4.5, WOOD)
    return _finish(s, ramps, foot_y)


def barrel(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A barrel: staves bellied out, iron hoops, its lid just showing."""
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN, IRON_])
    WOOD, IRON = 0, 1
    foot_y = H - sink
    top = 6
    t = np.clip((s.ys - top) / (foot_y - top), 0, 1)
    rx = (W / 2 - 3) * (0.86 + 0.14 * np.sin(t * math.pi))
    u = (s.xs + 0.5 - W / 2) / rx
    body = (np.abs(u) < 1) & (s.ys >= top) & (s.ys < foot_y)
    stave = np.abs(np.sin(np.arcsin(np.clip(u, -1, 1)) * 4.0))
    s.paint(body, np.sqrt(np.clip(1 - u * u, 0, 1)) * rx - (stave < 0.15) * 1.0, WOOD, var=np.where(stave < 0.15, -1, 0))
    for hy in (top + 5, top + (foot_y - top) * 0.36, foot_y - (foot_y - top) * 0.36, foot_y - 5):
        s.paint(body & (np.abs(s.ys + 0.5 - hy) < 1.7), np.sqrt(np.clip(1 - u * u, 0, 1)) * rx + 1, IRON, force=True)
    ball(s, W / 2, top + 1, W / 2 - 5, 3, 10, 2, WOOD, var=-1, force=True)
    return _finish(s, ramps, foot_y)


def bench(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A plank bench on two splayed legs."""
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN])
    WOOD = 0
    foot_y = H - sink
    grain = value_noise(W, H, 5.0, ("bench", seed))
    seat = foot_y - (foot_y - 4) * 0.7
    board(s, 2, seat, W - 2, seat + 6, 5, WOOD, bevel=2.0, grain=grain)
    for x in (12, W - 12):
        for side in (-1, 1):
            s.limb(x, seat + 5, x + side * 5, foot_y, 2.4, 2.2, 3, WOOD)
    return _finish(s, ramps, foot_y)


def kennel(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A dog's kennel: plank walls, a round door, a pitched roof of red boards, a chain out."""
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN, ROOFRED, IRON_, ("1a1418", 4, dict(cool=0.1, warm=0.0, low=0.02, high=0.12))])
    WOOD, ROOF, IRON, DARK = 0, 1, 2, 3
    foot_y = H - sink
    grain = value_noise(W, H, 4.0, ("kennel", seed))
    eave = foot_y - (foot_y - 4) * 0.55
    for i, x0 in enumerate(range(8, W - 8, 8)):
        board(s, x0, eave, min(x0 + 8, W - 8), foot_y, 3, WOOD, bevel=1.2, grain=grain)
    door = ((s.xs + 0.5 - W / 2) / 8) ** 2 + ((s.ys + 0.5 - (foot_y - 9)) / 11) ** 2
    s.paint((door <= 1) & (s.ys < foot_y), 0.5, DARK, force=True)
    peak = 4
    for side in (-1, 1):
        x_from, x_to = W / 2, W / 2 + side * (W / 2 - 1)
        vx, vy = x_to - x_from, eave + 3 - peak
        L = math.hypot(vx, vy)
        along = ((s.xs - x_from) * vx + (s.ys - peak) * vy) / L
        across = (-(s.xs - x_from) * vy + (s.ys - peak) * vx) / L * side
        slab = (along >= 0) & (along <= L) & (across > -9) & (across < 0.5)
        s.paint(slab, 8 + (across + 9) * 0.5 - (np.sin(along * 1.1) > 0.85) * 0.8, ROOF, var=np.where(np.sin(along * 1.1) > 0.85, -1, 0))
    for k in range(6):
        nail(s, W - 10 + k * 1.6, foot_y - 3 + (k % 2), 4, IRON, 1.0)
    return _finish(s, ramps, foot_y)


def cage(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """An iron cage that hangs: a ring at top and bottom, round bars between, a hook above."""
    W, H = width, height
    s, ramps = _made(W, H, [IRON_, ("1a1c24", 4, dict(cool=0.1, warm=0.0, low=0.03, high=0.12))])
    IRON, DARK = 0, 1
    foot_y = H - max(sink, 2)
    top = 14
    board(s, 6, top, W - 6, top + 5, 4, IRON, bevel=1.5)
    board(s, 6, foot_y - 6, W - 6, foot_y, 4, IRON, bevel=1.5)
    for i in range(9):
        x = 8 + (W - 16) * i / 8
        post(s, x, top + 5, foot_y - 6, 1.6, 3, IRON)
    s.limb(W / 2, 2, W / 2, top, 1.5, 1.5, 5, IRON)
    ball(s, W / 2, 4, 4, 3, 4, 2, IRON)
    return _finish(s, ramps, foot_y, outline=True)


def haystack(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A haystack: a tall dome of straw laid in strands that run down it, a pole through its
    top, the straw ragged at its foot."""
    r = random.Random(f"style hay {seed}")
    W, H = width, height
    s, ramps = _made(W, H, [STRAW, WOODEN])
    HAY, WOOD = 0, 1
    foot_y = H - sink
    top = 10
    t = np.clip((s.ys - top) / (foot_y - top), 0, 1)
    rx = (W / 2 - 2) * np.sqrt(np.clip(t * 1.25, 0, 1)) ** 0.9
    u = (s.xs + 0.5 - W / 2) / np.maximum(rx, 0.5)
    strand = np.sin(u * 14 + value_noise(W, H, 3.0, ("hay", seed)) * 2.5)
    body = (np.abs(u) < 1) & (s.ys >= top) & (s.ys < foot_y + 1)
    s.paint(body, np.sqrt(np.clip(1 - u * u, 0, 1)) * rx * 0.7 + strand * 0.6 + (1 - t) * 6, HAY, var=np.where(strand > 0.8, 1, np.where(strand < -0.85, -1, 0)))
    for band in (0.45, 0.75):  # where the straw is tied round
        by = top + (foot_y - top) * band
        s.H = np.where(body & (np.abs(s.ys + 0.5 - by) < 1.2), s.H - 1.5, s.H)
    s.limb(W / 2, top - 8, W / 2, top + 6, 1.6, 1.6, 10, WOOD)
    for _ in range(int(W * 0.6)):  # ragged straw at its foot
        x = r.uniform(W * 0.05, W * 0.95)
        s.limb(x, foot_y, x + r.uniform(-3, 3), foot_y - r.uniform(2, 6), 0.7, 0.6, 3, HAY, var=1)
    return _finish(s, ramps, foot_y, ao_radius=4)


def grave(width: int, height: int, sink: int, seed, kind: int) -> tuple[Image.Image, Image.Image]:
    """A grave marker of grey stone: a rounded headstone with a carved line (0), a cross (1),
    a slab leant over (2), a low flat slab (3); lichen in patches."""
    W, H = width, height
    s, ramps = _made(W, H, [STONE_, ("7a9a4a", 7, dict(cool=0.3, warm=0.25, low=0.15, high=0.75))])
    STONE, LICHEN = 0, 1
    foot_y = H - sink
    grit = value_noise(W, H, 2.0, ("grave grit", seed))
    if kind == 0:
        top = foot_y - (foot_y - 4) * 0.62
        x0, x1 = 8, W - 8
        q = ((s.xs + 0.5 - W / 2) / ((x1 - x0) / 2)) ** 2 + ((s.ys + 0.5 - (top + 8)) / 9) ** 2
        shape = ((s.xs >= x0) & (s.xs < x1) & (s.ys >= top + 8) & (s.ys < foot_y)) | ((q <= 1) & (s.ys < top + 8))
        edge = np.minimum(np.minimum(s.xs + 0.5 - x0, x1 - s.xs - 0.5), np.where(s.ys < top + 8, (1 - np.sqrt(np.clip(q, 0, 1))) * 9, 9))
        s.paint(shape, 3 + np.clip(edge / 2, 0, 1) * 2 + grit * 0.3, STONE)
        for ly in (top + 14, top + 19):
            s.H = np.where(shape & (np.abs(s.ys + 0.5 - ly) < 0.8) & (np.abs(s.xs + 0.5 - W / 2) < 8), s.H - 1.5, s.H)
    elif kind == 1:
        cx, top = W / 2, foot_y - (foot_y - 4) * 0.85
        board(s, cx - 3.5, top, cx + 3.5, foot_y, 3, STONE, bevel=1.6)
        board(s, cx - 13, top + 10, cx + 13, top + 17, 3.2, STONE, bevel=1.6)
    elif kind == 2:
        x0, y0, x1, y1 = 10, foot_y, 32, foot_y - 40
        vx, vy = x1 - x0, y1 - y0
        L = math.hypot(vx, vy)
        along = ((s.xs - x0) * vx + (s.ys - y0) * vy) / L
        across = (-(s.xs - x0) * vy + (s.ys - y0) * vx) / L
        slab = (along >= 0) & (along <= L) & (np.abs(across) < 6)
        s.paint(slab, 3 + np.clip((6 - np.abs(across)) / 1.6, 0, 1) * 1.6 + grit * 0.3, STONE)
    else:
        board(s, 4, foot_y - 12, W - 4, foot_y, 3, STONE, bevel=2.0)
    lichen = (value_noise(W, H, 3.0, ("lichen", seed)) > 0.55) & (s.mat == STONE)
    s.paint(lichen, s.H + 0.3, LICHEN, force=True)
    return _finish(s, ramps, foot_y)


def scarecrow(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A scarecrow on a pole: a sack head under a hat, a coat on a crossbar, straw at its
    cuffs and hem."""
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN, ("8a3a2c", 9, dict(cool=0.32, warm=0.2, low=0.08, high=0.75)), ("c8a868", 8, dict(cool=0.3, warm=0.2, low=0.15, high=0.9)), STRAW, ("3a2a20", 7, dict(cool=0.2, warm=0.15, low=0.05, high=0.45))])
    WOOD, COAT, SACK, HAY, HAT = 0, 1, 2, 3, 4
    foot_y = H - sink
    cx = W / 2
    post(s, cx, 20, foot_y, 2.4, 1, WOOD)
    arm_y = 40
    s.limb(4, arm_y, W - 4, arm_y, 2.2, 2.2, 3, WOOD)
    t = np.clip((s.ys - arm_y + 4) / 50, 0, 1)
    half = 10 + 6 * t
    u = (s.xs + 0.5 - cx) / half
    coat = (np.abs(u) < 1) & (s.ys >= arm_y - 4) & (s.ys < arm_y + 50)
    s.paint(coat, 5 + np.sqrt(np.clip(1 - u * u, 0, 1)) * half * 0.4 + np.sin(u * 7) * 0.6, COAT)
    for side in (-1, 1):
        s.limb(cx + side * 8, arm_y, cx + side * 26, arm_y + 2, 4.5, 3.5, 6, COAT)
        for k in range(4):
            s.limb(cx + side * 27, arm_y + 2, cx + side * (30 + k), arm_y + 6 + k * 2, 0.8, 0.6, 7, HAY)
    for k in range(9):
        x = cx - 14 + k * 3.5
        s.limb(x, arm_y + 48, x + (k - 4) * 0.4, arm_y + 56, 0.9, 0.7, 6, HAY)
    ball(s, cx, 28, 8, 9, 6, 7, SACK)
    board(s, cx - 13, 16, cx + 13, 19, 12, HAT, bevel=1.0)
    board(s, cx - 7, 6, cx + 7, 17, 12, HAT, bevel=2.0)
    return _finish(s, ramps, foot_y)


def wheel(s: Scene, cx, cy, rad, z, mat: int, iron: int, spokes: int = 8):
    d = np.hypot(s.xs + 0.5 - cx, s.ys + 0.5 - cy)
    rim = (d < rad) & (d > rad - 3)
    s.paint(rim, z + 2 + np.clip(1.5 - np.abs(d - (rad - 1.5)), 0, 1.5), mat)
    s.paint((d < rad + 0.5) & (d > rad - 1.2), z + 3.5, iron, force=True)
    for k in range(spokes):
        a = k * math.pi * 2 / spokes
        s.limb(cx, cy, cx + math.cos(a) * (rad - 2), cy + math.sin(a) * (rad - 2), 1.3, 1.1, z + 1, mat)
    ball(s, cx, cy, 3.2, 3.2, z + 2, 2.5, iron)


def cart(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A farm cart: a box of boards on two spoked wheels, its shafts down to the ground."""
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN, IRON_])
    WOOD, IRON = 0, 1
    foot_y = H - sink
    grain = value_noise(W, H, 5.0, ("cart", seed))
    rad = (foot_y - 4) * 0.3
    bed = foot_y - rad * 2 - 2
    for k in range(3):
        board(s, 6, bed - 22 + k * 8, W * 0.72, bed - 14 + k * 8, 3, WOOD, bevel=1.4, grain=grain)
    for x in (8, W * 0.36, W * 0.70):
        board(s, x - 2, bed - 25, x + 2, bed + 2, 5, WOOD, bevel=1.2)
    s.limb(W * 0.68, bed - 2, W - 4, foot_y - 1, 2.0, 1.8, 2, WOOD)
    for cx in (W * 0.18, W * 0.54):
        wheel(s, cx, foot_y - rad, rad, 6, WOOD, IRON)
    return _finish(s, ramps, foot_y)


def well(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A village well: a round wall of dressed stone, two posts carrying a pitched roof of red
    tiles, a winch between them with its rope and bucket, a crank at one side."""
    r = random.Random(f"style well {seed}")
    W, H = width, height
    s, ramps = _made(W, H, [STONE_, WOODEN, ROOFRED, IRON_])
    STONE, WOOD, ROOF, IRON = 0, 1, 2, 3
    foot_y = H - sink
    wall_top = foot_y - (foot_y - 4) * 0.36
    x0, x1 = 14, W - 14
    course = 8
    y = wall_top
    row = 0
    while y < foot_y:
        x = x0 - (row % 2) * 7
        while x < x1:
            wide = r.randint(12, 18)
            bx0, bx1 = max(x, x0), min(x + wide, x1)
            if bx1 - bx0 > 2:
                u = (bx0 + bx1) / 2 - W / 2
                board(s, bx0, y, bx1, min(y + course, foot_y), 4 + (1 - (u / (W / 2)) ** 2) * 3, STONE, bevel=1.8, var=r.choice([-1, 0, 0, 1]))
            x += wide
        y += course
        row += 1
    board(s, x0 - 3, wall_top - 4, x1 + 3, wall_top + 1, 9, STONE, bevel=1.8)
    grain = value_noise(W, H, 5.0, ("well", seed))
    eave = 28
    for px in (x0 + 4, x1 - 4):
        board(s, px - 3, eave, px + 3, wall_top - 3, 6, WOOD, bevel=1.4, grain=grain)
    s.limb(x0 + 4, eave + 14, x1 - 4, eave + 14, 3, 3, 8, WOOD)
    s.limb(W / 2 + 2, eave + 16, W / 2 + 2, wall_top - 14, 0.8, 0.8, 9, WOOD)
    board(s, W / 2 - 4, wall_top - 16, W / 2 + 8, wall_top - 6, 10, WOOD, bevel=1.5)
    peak = 4
    s.limb(x1 - 2, eave + 14, x1 + 6, eave + 20, 1.2, 1.2, 9, IRON)
    gable = (s.ys >= peak + 6) & (s.ys < eave + 2) & (np.abs(s.xs + 0.5 - W / 2) < (s.ys - peak) * (W / 2 - 1) / (eave + 4 - peak) - 4)
    s.paint(gable, 5 - (np.sin(s.xs * 0.8) > 0.85) * 0.6, WOOD, var=np.where(np.sin(s.xs * 0.8) > 0.85, -1, 0))
    peak = 4
    for side in (-1, 1):
        x_from, x_to = W / 2, W / 2 + side * (W / 2 - 1)
        vx, vy = x_to - x_from, eave + 4 - peak
        L = math.hypot(vx, vy)
        along = ((s.xs - x_from) * vx + (s.ys - peak) * vy) / L
        across = (-(s.xs - x_from) * vy + (s.ys - peak) * vx) / L * side
        tiles = np.sin(along * 0.75) > 0.8
        slab = (along >= 0) & (along <= L) & (across > -13) & (across < 0.5)
        s.paint(slab, 12 + (across + 13) * 0.5 - tiles * 0.8, ROOF, var=np.where(tiles, -1, 0))
    return _finish(s, ramps, foot_y, ao_radius=4)


def washing(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A washing line between two posts: a rope that sags, cloths of a few colours hung on it
    with their folds."""
    r = random.Random(f"style washing {seed}")
    W, H = width, height
    colours = ["e8e2d4", "9a3a4a", "4a6a9a", "d8c060", "e8e2d4"]
    s, ramps = _made(W, H, [WOODEN, ("c8b898", 6, dict(cool=0.2, warm=0.1, low=0.25, high=0.8))] + [(c, 8, dict(cool=0.3, warm=0.18, low=0.15, high=0.95)) for c in colours])
    WOOD, ROPE = 0, 1
    foot_y = H - sink
    top = 10
    for x in (6, W - 6):
        post(s, x, top, foot_y, 2.2, 2, WOOD)
    sag = lambda x: top + 4 + 10 * math.sin(math.pi * (x - 6) / (W - 12))
    for x in range(6, W - 6):
        s.paint(np.ones((1, 1), dtype=bool), 4, ROPE, at=(int(sag(x)), x))
    x = 18
    k = 0
    while x < W - 26:
        wide, long_ = r.randint(12, 20), r.randint(14, 30)
        y0 = sag(x + wide / 2)
        u = (s.xs + 0.5 - x) / wide
        cloth = (u >= 0) & (u < 1) & (s.ys >= y0) & (s.ys < y0 + long_ * (1 - 0.15 * np.abs(np.sin(u * 3.0))))
        s.paint(cloth, 5 + np.sin(u * math.pi * r.choice([2, 3])) * 1.5, 2 + k % len(colours))
        x += wide + r.randint(3, 8)
        k += 1
    return _finish(s, ramps, foot_y)


def garden(width: int, height: int, sink: int, seed) -> tuple[Image.Image, Image.Image]:
    """A kitchen garden: a bed of dark earth in a plank edging, rows of leafy greens on it and
    a stake of beans at one end."""
    r = random.Random(f"style garden {seed}")
    W, H = width, height
    s, ramps = _made(W, H, [WOODEN, ("4a3424", 8, dict(cool=0.3, warm=0.2, low=0.05, high=0.5)), ("5aa040", 9, dict(cool=0.4, warm=0.3, low=0.08, high=0.88))])
    WOOD, SOIL, LEAF = 0, 1, 2
    foot_y = H - sink
    bed = foot_y - 10
    board(s, 2, bed, W - 2, foot_y, 2, WOOD, bevel=1.4, grain=value_noise(W, H, 5.0, ("garden", seed)))
    board(s, 4, bed - 3, W - 4, bed + 1, 1, SOIL, bevel=1.5)
    for i in range(int((W - 30) / 13)):
        cx = 12 + i * 13
        for _ in range(5):
            a = r.uniform(-2.6, -0.5)
            lx, ly = cx + math.cos(a) * 5, bed - 3 + math.sin(a) * 7
            ball(s, lx, ly, 3, 4.5, 3, 2.5, LEAF, var=r.choice([-1, 0, 1]))
    sx = W - 14
    s.limb(sx, foot_y - 4, sx, 6, 1.3, 1.1, 3, WOOD)
    for k in range(10):
        y = 10 + k * (bed - 14) / 10
        ball(s, sx + (3 if k % 2 else -3), y, 2.6, 3.2, 4, 2, LEAF, var=r.choice([-1, 0, 1]))
    return _finish(s, ramps, foot_y)


# --- Relief: a drawing of the first style raised into the second -----------------------------


def _distance_inside(mask: np.ndarray, cap: int) -> np.ndarray:
    """How many px each pixel of `mask` lies from its edge (4-neighbour steps), up to `cap`."""
    d = np.zeros(mask.shape)
    cur = mask.copy()
    for k in range(1, cap + 1):
        shrunk = cur & np.roll(cur, 1, 0) & np.roll(cur, -1, 0) & np.roll(cur, 1, 1) & np.roll(cur, -1, 1)
        shrunk[0, :] = shrunk[-1, :] = False
        shrunk[:, 0] = shrunk[:, -1] = False
        d += cur
        cur = shrunk
        if not cur.any():
            break
    return d


def relief(picture: Image.Image, *, depth: float = 1.0, round_: int = 6, groove: float = 3.0, strength: float = 0.55) -> tuple[Image.Image, Image.Image]:
    """A picture drawn in the first style, raised into the second: its shapes and colours
    stay where they are, but it gets a height field (its silhouette rounded `round_` px in,
    each patch of one tone bevelled where it meets a darker line, the dark lines and joints
    sunk `groove`), is lit again by the one light on that field (`strength` of the change),
    its shadows turned cool and its lights warm, and gives its normal map."""
    a = np.array(picture.convert("RGBA")).astype(float)
    rgb, alpha = a[..., :3], a[..., 3]
    solid = alpha > 0
    lum = (rgb @ np.array([0.299, 0.587, 0.114])) / 255
    local = lum - blur(lum, 2)
    # A joint or line is a pixel darker than what is round it: sink it; a stone is bevelled up
    # from its joints.
    joint = solid & (local < -0.035)
    face = solid & ~joint
    bevel = np.clip(_distance_inside(face, 3) / 3, 0, 1) ** 0.7
    body = np.clip(_distance_inside(solid, round_) / round_, 0, 1)
    body = np.sqrt(1 - (1 - body) ** 2)
    H = (body * round_ * 0.8 + bevel * groove * 0.6 - joint * groove * 0.4 + local * 6) * depth
    N = normals(np.where(solid, H, 0), solid, clamp=2.0)
    d = np.clip(N @ LIGHT, 0, 1)
    flat = float(LIGHT[2])  # how a face square to the eye is lit: the old drawing's own light
    change = (d - flat) * strength
    # Hollows: darker where the field is lower than round it.
    Hf = np.where(solid, H, H[solid].min() if solid.any() else 0)
    change -= np.clip((blur(Hf, 3) - Hf) * 0.04, 0, 0.25)
    out = rgb.copy()
    lit = change > 0
    warm = np.array([255, 236, 190], dtype=float)
    cool = np.array([40, 44, 92], dtype=float)
    k = np.clip(change, -1, 1)[..., None]
    out = np.where(lit[..., None], out + (warm - out) * k * 0.9, out + (cool - out) * (-k) * 0.9)
    res = np.zeros_like(a)
    res[..., :3] = np.clip(np.round(out), 0, 255)
    res[..., 3] = alpha
    nm = np.zeros(a.shape, dtype=np.uint8)
    nm[..., 0] = np.round((N[..., 0] * 0.5 + 0.5) * 255)
    nm[..., 1] = np.round((-N[..., 1] * 0.5 + 0.5) * 255)
    nm[..., 2] = np.round((N[..., 2] * 0.5 + 0.5) * 255)
    nm[..., 3] = 255
    nm[~solid] = (128, 128, 255, 255)
    return Image.fromarray(res.astype(np.uint8), "RGBA"), Image.fromarray(nm, "RGBA")


# --- The ground: textures on a tile that repeats ---------------------------------------------


def _wrap_blur(a: np.ndarray, r: int) -> np.ndarray:
    if r <= 0:
        return a.astype(np.float64)
    p = np.pad(a, r, mode="wrap")
    return blur(p, r)[r:-r, r:-r]


def _wrap_normals(H: np.ndarray, clamp: float = 2.5) -> np.ndarray:
    gx = (np.roll(H, -1, 1) - np.roll(H, 1, 1)) / 2
    gy = (np.roll(H, -1, 0) - np.roll(H, 1, 0)) / 2
    gx, gy = np.clip(gx, -clamp, clamp), np.clip(gy, -clamp, clamp)
    n = np.stack([-gx, -gy, np.ones_like(gx)], axis=-1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def _wrap_noise(size: int, cells: int, seed) -> np.ndarray:
    r = random.Random(str(("tile noise", seed)))
    grid = np.array([[r.uniform(-1, 1) for _ in range(cells)] for _ in range(cells)])
    ys, xs = np.mgrid[0:size, 0:size]
    fx, fy = xs * cells / size, ys * cells / size
    ix, iy = fx.astype(int), fy.astype(int)
    tx, ty = fx - ix, fy - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a, b = grid[iy % cells, ix % cells], grid[iy % cells, (ix + 1) % cells]
    c, d = grid[(iy + 1) % cells, ix % cells], grid[(iy + 1) % cells, (ix + 1) % cells]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def _wrap_voronoi(size: int, points) -> tuple[np.ndarray, np.ndarray]:
    """For each pixel of a tile that repeats: which point is nearest, and how far it lies
    from the line where the next nearest takes over."""
    ys, xs = np.mgrid[0:size, 0:size] + 0.5
    best = np.full((size, size), 1e9)
    second = np.full((size, size), 1e9)
    owner = np.zeros((size, size), dtype=int)
    for i, (px, py, sx, sy) in enumerate(points):
        dx = np.abs(xs - px)
        dy = np.abs(ys - py)
        dx, dy = np.minimum(dx, size - dx), np.minimum(dy, size - dy)
        d = np.hypot(dx * sx, dy * sy)
        nearer = d < best
        second = np.where(nearer, best, np.minimum(second, d))
        owner = np.where(nearer, i, owner)
        best = np.minimum(best, d)
    return owner, second - best


def _wrap_dab(H: np.ndarray, cx: float, cy: float, rx: float, ry: float, height: float):
    """A round bump (or, with a negative height, a dent) on a tile that repeats."""
    size = H.shape[0]
    ys, xs = np.mgrid[0:size, 0:size] + 0.5
    dx = np.abs(xs - cx)
    dy = np.abs(ys - cy)
    dx, dy = np.minimum(dx, size - dx), np.minimum(dy, size - dy)
    q = (dx / rx) ** 2 + (dy / ry) ** 2
    H += np.sqrt(np.clip(1 - q, 0, 1)) * height


TILE_KINDS = ("dirt", "meadow", "stone", "rubble", "bricks", "ashlar", "sand", "clay", "planks", "leaves", "foliage", "cloud", "ice", "flutes")


def tile_texture(kind: str, variant: int, size: int = 32) -> tuple[np.ndarray, np.ndarray]:
    """The inside of a material on a tile that repeats, in the second style: a height field
    (returned too, for the normal map) lit by the one light into the material's ramp
    indices 1 .. 5, where a face square to the eye takes 3, a joint or crack 1; hollows a
    step darker. Stone breaks into chunks each tilted its own way and bevelled at its
    cracks; bricks and dressed stone are blocks bevelled at their sunk joints; earth has
    pebbles standing out of it and root hairs sunk in it; sand lies in ripples; clay in
    strata; boards along their grain; leaves and scales overlap; cloud is soft puffs; ice
    is big facets; flutes are columns side by side."""
    r = random.Random(f"tile texture {kind} {variant}")
    H = np.zeros((size, size))
    joint = np.zeros((size, size), dtype=bool)
    ys, xs = np.mgrid[0:size, 0:size] + 0.5
    noise = _wrap_noise(size, 4, (kind, variant))
    fine_noise = _wrap_noise(size, 16, (kind, variant, "fine"))
    if kind in ("dirt", "meadow"):
        H = noise * 1.0 + fine_noise * 0.2
        for _ in range(5 + variant % 2):  # pebbles standing out of it
            _wrap_dab(H, r.uniform(0, size), r.uniform(0, size), r.uniform(1.5, 3.2), r.uniform(1.2, 2.4), r.uniform(2.0, 3.0))
        for _ in range(2):  # root hairs, sunk
            x, y = r.uniform(0, size), r.uniform(0, size)
            for i in range(r.randint(5, 9)):
                x += r.choice([-1, 0, 0, 1])
                H[int(y + i) % size, int(x) % size] -= 1.2
    elif kind in ("stone", "rubble", "ice"):
        n = {"stone": 6, "rubble": 9, "ice": 4}[kind]
        pts = [(r.uniform(0, size), r.uniform(0, size), 1.0, r.uniform(1.0, 1.3)) for _ in range(n)]
        owner, edge = _wrap_voronoi(size, pts)
        tilt = [(r.uniform(-0.18, 0.18), r.uniform(-0.25, 0.1), r.uniform(0, 1.5)) for _ in range(n)]
        tx = np.array([t[0] for t in tilt])[owner]
        ty = np.array([t[1] for t in tilt])[owner]
        lift = np.array([t[2] for t in tilt])[owner]
        cx = np.array([p[0] for p in pts])[owner]
        cy = np.array([p[1] for p in pts])[owner]
        dx = (xs - cx + size / 2) % size - size / 2
        dy = (ys - cy + size / 2) % size - size / 2
        sharp = kind == "ice"
        bevel = np.clip(edge / (2.0 if sharp else 2.5), 0, 1) ** 0.7
        H = 2 + (lift + tx * dx + ty * dy + fine_noise * (0.1 if sharp else 0.35)) * bevel * (1.0 if kind != "rubble" else 0.7) + bevel * 1.5
        joint = edge < (0.4 if sharp else 0.9)
    elif kind in ("bricks", "ashlar"):
        course, long_ = 16, (16 if kind == "bricks" else 32)  # bricks square and in line, dressed stone long and laid half over
        row = (ys // course).astype(int)
        shift = (row % 2) * (long_ // 2) if kind == "ashlar" else 0
        col = ((xs + shift) // long_).astype(int)
        u = (xs + shift) % long_
        v = ys % course
        edge = np.minimum.reduce([u, long_ - u, v, course - v])
        block = (row * 7 + col * 3 + variant) % 5
        lift = np.array([0.0, 0.4, -0.3, 0.2, -0.1])[block]
        bevel = np.clip((edge - 0.5) / 2.0, 0, 1) ** 0.8
        H = 1 + bevel * 2.2 + lift * bevel + fine_noise * 0.25 * bevel
        joint = edge < 1.0
        if kind == "ashlar":  # a vein through the stone, sunk a little
            vein = np.abs(np.sin((xs + noise * 6) * 0.35 + variant) * 5 - (ys % course - course / 2)) < 0.6
            H -= vein * 0.4
    elif kind == "sand":
        H = np.sin((xs + ys * 0.35 + noise * 4) * 2 * math.pi / 8) * 0.9 + fine_noise * 0.5
    elif kind == "clay":
        bounds = [0]
        while bounds[-1] < size:
            bounds.append(bounds[-1] + r.choice([8, 10, 11]))
        bounds[-1] = size
        H = np.zeros((size, size))
        wave = np.sin(xs * 2 * math.pi / size + variant) * 2.5 + noise * 1.0  # the strata wander
        yy = (ys + wave) % size
        for i in range(len(bounds) - 1):
            y0, y1 = bounds[i], bounds[i + 1]
            band = (yy >= y0) & (yy < y1)
            e = np.minimum(yy - y0, y1 - yy)
            H = np.where(band, 1 + np.clip(e / 2.5, 0, 1) * 1.4 + (i % 2) * 0.3 + fine_noise * 0.25, H)
            joint |= band & (yy - y0 < 0.7)
    elif kind == "planks":
        board, length = 8, 16
        row = (ys // board).astype(int)
        u = (xs + (row * 5 + variant * 3) % length) % length  # boards end at different places
        v = ys % board
        e = np.minimum.reduce([v, board - v, u + 0.5, length - u])
        H = 1 + np.clip((e - 0.5) / 1.5, 0, 1) * 1.6 + (np.sin(xs * 0.4 + noise * 4 + row * 2) > 0.85) * -0.4
        joint = e < 1.0
        for k in range(size // board):  # a nail at each board's end
            _wrap_dab(H, (k * 11 + variant * 5) % size + 0.5, k * board + board / 2, 0.9, 0.9, 1.2)
    elif kind in ("leaves", "foliage"):
        H = np.zeros((size, size)) - 1
        if kind == "leaves":  # roof scales in rows, each rounded at its foot, its lip over the row under
            pitch = 8
            for row in range(size // pitch):
                for k in range(size // pitch):
                    cx = k * pitch + (row % 2) * pitch / 2 + pitch / 2
                    cy = row * pitch + pitch * 0.75
                    dx = (xs - cx + size / 2) % size - size / 2
                    dy = (ys - cy + size / 2) % size - size / 2
                    q = (dx / (pitch / 2 + 0.3)) ** 2 + (np.maximum(dy, 0) / (pitch / 2)) ** 2
                    scale = (q < 1) & (dy > -pitch)
                    h = np.sqrt(np.clip(1 - (dx / (pitch / 2 + 0.3)) ** 2, 0, 1)) * 1.4 + (dy + pitch) / pitch * 2.0
                    H = np.where(scale & (h > H), h, H)
        else:
            for _ in range(int(size * size / 14)):
                lx, ly = r.uniform(0, size), r.uniform(0, size)
                ang = r.uniform(0, math.pi)
                ca, sa = math.cos(ang), math.sin(ang)
                dx = (xs - lx + size / 2) % size - size / 2
                dy = (ys - ly + size / 2) % size - size / 2
                pu, pv = dx * ca + dy * sa, -dx * sa + dy * ca
                q = (pu / 3.6) ** 2 + (pv / 2.0) ** 2
                h = r.uniform(0, 2) + np.sqrt(np.clip(1 - q, 0, 1)) * 1.5
                H = np.where((q < 1) & (h > H), h, H)
        joint = H < 0
        H = np.maximum(H, 0)
    elif kind == "cloud":
        H = np.zeros((size, size))
        for _ in range(6):
            _wrap_dab(H, r.uniform(0, size), r.uniform(0, size), r.uniform(6, 10), r.uniform(5, 8), r.uniform(3, 5))
        H = _wrap_blur(H, 1)
    elif kind == "flutes":
        u = xs % 8
        H = np.sqrt(np.clip(1 - ((u - 4) / 4) ** 2, 0, 1)) * 2.5
        joint = (u < 0.8) | (u > 7.2)
    else:
        raise KeyError(kind)
    H = np.where(joint, H.min(), H)
    N = _wrap_normals(H)
    d = np.clip(N @ LIGHT, 0, 1)
    flat = float(LIGHT[2])
    idx = 3 + (d - flat) / (0.95 - flat) * 2.4
    idx = np.where(d < flat, 3 - (flat - d) / (flat - 0.15) * 2.0, idx)
    hollow = _wrap_blur(H, 2) - H > 0.8
    idx = np.round(idx) - hollow
    idx = np.where(joint, 1, idx)
    if kind in ("cloud", "ice"):  # smooth stuff: its light changes gently, and no dark in it
        idx = np.maximum(3 + np.round((idx - 3) * 0.6), 2)
    return np.clip(idx, 1, 5).astype(np.int16), H


# --- Repaint: a building drawn again over its own plan ----------------------------------------

# What a region of a building is made of, and how it stands: its height over the wall's face
# (what is further out shades what is under it), and how its surface is shaped.
REPAINT_KINDS = {
    "stone": dict(z=3.0, bevel=2.2, blocks=True, rough=0.15),
    "tiles": dict(z=9.0, bevel=1.4, blocks=True, rough=0.2, lip=0.35),
    "plaster": dict(z=2.0, bevel=1.5, blocks=False, rough=0.35),
    "timber": dict(z=4.5, bevel=2.6, blocks=True, rough=0.2, grain=True),
    "thatch": dict(z=9.0, bevel=2.0, blocks=False, rough=0.3, strands=True),
    "iron": dict(z=5.0, bevel=1.2, blocks=True, rough=0.05),
    "metal": dict(z=5.0, bevel=1.5, blocks=True, rough=0.05),
    "leaf": dict(z=8.0, bevel=1.0, blocks=False, rough=1.0),
    "cloth": dict(z=5.0, bevel=1.5, blocks=False, rough=0.15, folds=True),
}


def _rgb_list(ramp) -> list[tuple[int, int, int]]:
    return [tuple(int(c) for c in colour[:3]) for colour in ramp]


def repaint(picture: Image.Image, materials, *, seed="repaint", keep_far: float = 30.0) -> tuple[Image.Image, Image.Image]:
    """A building drawn again in the second style over its own plan: each pixel of its
    first-style drawing is read as the material whose colours it is nearest (`materials`:
    (old ramp, kind, base colour of its new ramp)); a joint or seam (that material's darkest
    tones) parts it into pieces (blocks, beams, shingles), each with its own lift and tilt,
    bevelled at its edges and shaped on its face as its kind is (stone pitted, timber in
    grain, thatch in strands, tiles with a lip over the row under, ivy in leaves); then the
    whole is lit by the one light into each material's new ramp, the eaves and what stands
    out casting shadows. What is no material's colour (a window's glass, a glow, a bell) is
    kept as drawn, set back a little. Every shape stays where the drawing has it."""
    a = np.array(picture.convert("RGBA"))
    h, w = a.shape[:2]
    rgb = a[..., :3].astype(float)
    solid = a[..., 3] > 0
    # Read each pixel as the nearest colour of the materials' old ramps.
    colours, owner, tone = [], [], []
    for m, (ramp, kind, base) in enumerate(materials):
        if kind == "keep":
            continue
        cols = _rgb_list(ramp)
        for i, c in enumerate(cols):
            colours.append(c)
            owner.append(m)
            tone.append(i / max(len(cols) - 1, 1))
    if not colours:  # nothing but lights: raised, not repainted
        return relief(picture)
    colours = np.array(colours, dtype=float)
    owner, tone = np.array(owner), np.array(tone)
    d = np.linalg.norm(rgb[..., None, :] - colours[None, None, :, :], axis=-1)
    near = d.argmin(-1)
    far = d.min(-1) > keep_far
    mat = np.where(solid & ~far, owner[near], -1)
    t = tone[near]
    # A dark opening (a window's glass, a doorway, an arch) is kept as drawn: dark and at
    # least three pixels thick, where a joint is one or two.
    lum = rgb @ np.array([0.299, 0.587, 0.114]) / 255
    dark = solid & (lum < 0.17)
    opening = _distance_inside(dark, 2) >= 2
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
        opening = opening | (np.roll(np.roll(opening, dy, 0), dx, 1) & dark)
    far = far | opening
    mat = np.where(far, -1, mat)
    keep = solid & far
    # Dither (a checker of two tones) is a shade, not a pattern: read it as its mean.
    def checker(v, axis):
        d0, d1 = v - np.roll(v, 1, axis), v - np.roll(v, -1, axis)
        return (d0 * d1 > 0) & (np.abs(d0) > 0.03)
    dither = checker(lum, 0) & checker(lum, 1) & solid
    if dither.any():
        smooth = blur(np.where(solid, lum, lum[solid].mean()), 1)
        grown = dither | np.roll(dither, 1, 0) | np.roll(dither, -1, 0) | np.roll(dither, 1, 1) | np.roll(dither, -1, 1)
        lum = np.where(grown & solid, smooth, lum)
    filled = np.where(solid, lum, lum[solid].mean() if solid.any() else 0)
    local = lum - blur(filled, 2)
    broad_lum = blur(filled, 6)
    s = Scene(w, h)
    xs, ys = s.xs, s.ys
    rough = value_noise(w, h, 2.0, (seed, "rough"))
    broad = value_noise(w, h, 7.0, (seed, "broad"))
    r = random.Random(f"repaint {seed}")
    ramps = {}
    for m, (ramp, kind, base) in enumerate(materials):
        sel = mat == m
        if kind == "keep" or not sel.any():
            continue
        k = REPAINT_KINDS[kind]
        n = 10
        # The new ramp keeps the family's own range, darkest to lightest: marble stays pale.
        ls = [colorsys.rgb_to_hls(*(v / 255 for v in c))[1] for c in _rgb_list(ramp)]
        lb = colorsys.rgb_to_hls(*(v / 255 for v in hex_rgb(base)))[1]
        lo_l, hi_l = max(0.03, min(ls) - 0.04), min(0.98, max(ls) + 0.04)
        anchor = float(np.clip((lb - lo_l) / max(hi_l - lo_l, 0.05), 0.3, 0.7))
        sb = colorsys.rgb_to_hls(*(v / 255 for v in hex_rgb(base)))[2]
        shift = float(np.clip(sb / 0.5, 0.15, 1.0))  # a weak colour turns only a little, or beige goes pink
        ramps[m] = make_ramp(base, n, cool=0.32 * shift, warm=(0.2 if kind != "leaf" else 0.28) * shift, anchor=anchor, low=lo_l, high=hi_l)
        if kind in ("stone", "tiles"):  # the drawing's own joints, its lines only: a dot of texture is no joint
            seam = sel & (local < -0.022)
            # Where two blocks meet with no dark line between, a change of tone parts them.
            step = (np.abs(lum - np.roll(lum, -1, 1)) > 0.022) | (np.abs(lum - np.roll(lum, -1, 0)) > 0.022)
            seam = seam | (sel & step & np.roll(sel, -1, 1) & np.roll(sel, -1, 0))
            pieces = _label(seam)
            sizes = np.bincount(pieces.ravel())
            seam = seam & (sizes[pieces] >= 4)
            labels, rise = None, (_distance_down(sel & ~seam) / 7.0 if kind == "tiles" else None)
        elif k["blocks"]:  # beams and plates: parted where the drawing parts them
            seam = sel & (local < -0.07)
            labels, rise = None, None
        else:
            seam, labels, rise = np.zeros_like(sel), None, None
        body = sel & ~seam
        dist = _distance_inside(body, 4)
        bevel = np.clip(dist / max(k["bevel"], 0.5), 0, 1) ** 0.7
        H = k["z"] + bevel * k["bevel"] + rough * k["rough"]
        var = np.zeros((h, w), dtype=int)
        if k["blocks"]:
            if labels is None:
                labels = _label(body)
            count = labels.max() + 1
            lift = np.array([r.uniform(-0.5, 0.6) for _ in range(count)])
            tx = np.array([r.uniform(-0.08, 0.08) for _ in range(count)])
            ty = np.array([r.uniform(-0.1, 0.04) for _ in range(count)])
            # Each piece the shade the drawing gave it, a step or two either way.
            sums = np.bincount(labels.ravel(), weights=np.where(body, lum, 0).ravel(), minlength=count)
            nums = np.maximum(np.bincount(labels.ravel(), weights=body.ravel().astype(float), minlength=count), 1)
            fam = lum[body].mean() if body.any() else 0
            per_step = max((hi_l - lo_l) / (n - 1), 0.02)  # a step of the new ramp, in lightness
            tone = np.clip(np.round((sums / nums - fam) / per_step * 0.8), -6, 4).astype(int)
            H = H + lift[labels] * bevel + (tx[labels] * (xs % 16 - 8) + ty[labels] * (ys % 16 - 8)) * bevel
            var = tone[labels] if kind in ("stone", "tiles") else var
            if rise is not None and k.get("lip"):  # a shingle stands out more at its foot, over the one under
                H = H + np.clip(rise, 0, 1) * k["lip"] * 5
        if k.get("grain"):
            H = H + (np.sin(ys * 1.6 + broad * 4) > 0.75) * -0.5 + (np.sin(xs * 1.6 + broad * 4) > 0.8) * -0.3
        if k.get("strands"):
            H = H + np.sin(xs * 1.4 + broad * 3 + ys * 0.15) * 0.6
        if k.get("folds"):
            H = H + np.sin(xs * 0.5 + broad * 2) * 1.2
        if kind == "leaf":
            H = H + value_noise(w, h, 1.6, (seed, "leaf")) * 1.4
        # The drawing's broad light and shade across a shape (a round tower lit on one side)
        # carries over, a step or two.
        family = lum[sel].mean()
        spread = max(float(np.ptp(lum[sel])), 0.08)
        if kind not in ("stone", "tiles"):
            var = var + np.clip(np.round((broad_lum - family) / spread * 3), -2, 2).astype(int)
        s.paint(body, H, m, var=var)
        s.paint(seam, np.full((h, w), k["z"] - 0.5), m, var=-3)
    KEEP = len(materials)
    s.paint(keep, np.full((h, w), 1.0), KEEP)
    ramps[KEEP] = [(0, 0, 0)] * 10
    # The silhouette rounds off a little where it meets the sky.
    edge = np.clip(_distance_inside(solid, 3) / 3, 0, 1)
    s.H = np.where(solid, s.H - (1 - edge) * 1.5, s.H)
    return _render_matched(s, ramps, keep, lum, a, KEEP)


def _render_matched(s: Scene, ramps: dict, keep: np.ndarray, lum: np.ndarray, a: np.ndarray, KEEP: int, **kwargs) -> tuple[Image.Image, Image.Image]:
    """Renders a scene drawn over a first-style picture, each material as bright overall as
    the picture had it (its tones shifted by the steps its new light took it off, and lit
    again), what is kept (`keep`) pasted back as drawn."""
    opts = dict(ao=0.05, ao_radius=3, shadow=0.34, shadow_reach=10, shadow_drop=0.55, gamma=1.2, outline=False, rim=True)
    opts.update(kwargs)
    out, nm = render(s, ramps, **opts)
    new_lum = np.array(out)[..., :3].astype(float) @ np.array([0.299, 0.587, 0.114]) / 255
    for m, ramp in ramps.items():
        sel = (s.mat == m) & ~keep
        if m == KEEP or not sel.any():
            continue
        lr = [sum(c) / 765 for c in ramp]
        step = max((max(lr) - min(lr)) / (len(ramp) - 1), 0.02)
        steps = int(np.clip(np.round((lum[sel].mean() - new_lum[sel].mean()) / step), -3, 3))
        s.var = np.where(sel, s.var + steps, s.var)
    out, nm = render(s, ramps, **opts)
    o = np.array(out)
    o[keep] = a[keep]
    return Image.fromarray(o, "RGBA"), nm


def _label(mask: np.ndarray) -> np.ndarray:
    """The 4-connected pieces of `mask`, numbered from 1 (0 where it is not)."""
    labels = np.zeros(mask.shape, dtype=int)
    h, w = mask.shape
    count = 0
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys, xs):
        if labels[y0, x0]:
            continue
        count += 1
        stack = [(y0, x0)]
        labels[y0, x0] = count
        while stack:
            y, x = stack.pop()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not labels[yy, xx]:
                    labels[yy, xx] = count
                    stack.append((yy, xx))
    return labels


def _distance_down(mask: np.ndarray) -> np.ndarray:
    """How far each pixel of `mask` lies below the top of its run in its column."""
    out = np.zeros(mask.shape)
    run = np.zeros(mask.shape[1])
    for y in range(mask.shape[0]):
        run = np.where(mask[y], run + 1, 0)
        out[y] = run
    return out


def guess_kind(h: float, l: float, s: float) -> str:
    """What a family of colours most likely is, from its middle colour (hue, lightness,
    saturation): grey is stone, green ivy, a strong red or blue roof tiles, gold metal, a dark
    brown timber, a straw yellow thatch, a pale warm colour dressed stone."""
    if s > 0.85 and l > 0.5 or s > 0.6 and l > 0.8:
        return "keep"  # a light: a lit window, a glow, fire
    if s < 0.14:
        return "stone"
    if 0.18 < h < 0.47 and s > 0.2:
        return "leaf"
    if s > 0.62 and 0.08 < h < 0.18:
        return "metal"
    if (h < 0.045 or h > 0.93) and s > 0.3 and l > 0.3:
        return "tiles"
    if 0.5 < h < 0.75 and s > 0.35:
        return "tiles"
    if 0.03 <= h < 0.17:
        if l < 0.3:
            return "timber"
        if s > 0.42 and l < 0.55:
            return "thatch"
    return "stone"


def palette_families(picture: Image.Image, least: int = 6, kinds: dict | None = None):
    """The families of colours a drawing is made of: its colours (used on at least `least`
    pixels) joined where they are near in hue and saturation (greys with greys), each family
    sorted dark to light. Returns [(colours, kind, base colour)]."""
    a = np.array(picture.convert("RGBA"))
    px = a[a[..., 3] > 0][:, :3]
    if not len(px):
        return []
    cols, counts = np.unique(px, axis=0, return_counts=True)
    cols = [tuple(int(v) for v in c) for c, n in zip(cols, counts) if n >= least]
    hls = [colorsys.rgb_to_hls(*(v / 255 for v in c)) for c in cols]
    parent = list(range(len(cols)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for i in range(len(cols)):
        hi, li, si = hls[i]
        for j in range(i + 1, len(cols)):
            hj, lj, sj = hls[j]
            grey_i, grey_j = si < 0.14 or li < 0.08, sj < 0.14 or lj < 0.08
            dh = min(abs(hi - hj), 1 - abs(hi - hj))
            if (grey_i and grey_j and abs(li - lj) < 0.2) or (not grey_i and not grey_j and dh < 0.016 and abs(si - sj) < 0.16 and abs(li - lj) < 0.22):
                parent[find(i)] = find(j)
    groups = {}
    for i in range(len(cols)):
        groups.setdefault(find(i), []).append(i)
    out = []
    for members in groups.values():
        members.sort(key=lambda i: hls[i][1])
        colours = [cols[i] for i in members]
        mid = members[len(members) // 2]
        h, l, s = hls[mid]
        base = "%02x%02x%02x" % cols[mid]
        kind = next((k for anchor, k in (kinds or {}).items() if hex_rgb(anchor) in colours), None) or guess_kind(h, l, s)
        if len(colours) <= 2 and s > 0.4 and kind in ("stone", "metal", "tiles"):
            kind = "keep"  # a small bright thing (a bell, a finial, a sign): as drawn
        out.append((colours, kind, base))
    return out


def repainted(picture: Image.Image, seed, kinds: dict | None = None) -> tuple[Image.Image, Image.Image]:
    """A building of the first style drawn again over its own plan (`repaint`), its materials
    read from its own colours (`palette_families`; `kinds` names a family's kind by one of its
    colours where the guess would be wrong)."""
    return repaint(picture, palette_families(picture, kinds=kinds), seed=seed, keep_far=18)


# --- Turned: columns and pillars drawn round ---------------------------------------------------


def turned(picture: Image.Image, seed, *, flutes: float = 4.0, block: float = 0.45) -> tuple[Image.Image, Image.Image]:
    """Columns and pillars drawn again as turned stone over their own outline: each row of
    each upright of the drawing becomes a true round section of it, its shaft cut in
    flutes (`flutes` px apart, at the size it ships), a moulding where the outline steps
    out or in (a base, a capital, a band), a broken top ragged where the drawing breaks it;
    a run wider than `block` of the picture (a lintel, a fallen block) is a dressed block,
    not a round. Lit by the one light into the marble's own ramp, as bright overall as the
    drawing; its gold and its lights kept as drawn."""
    a = np.array(picture.convert("RGBA"))
    h, w = a.shape[:2]
    families = palette_families(picture)
    solid = a[..., 3] > 0
    rgb = a[..., :3].astype(float)
    lum = rgb @ np.array([0.299, 0.587, 0.114]) / 255
    colours, owner = [], []
    for m, (ramp, kind, base) in enumerate(families):
        for c in _rgb_list(ramp):
            colours.append(c)
            owner.append(m)
    near = np.linalg.norm(rgb[..., None, :] - np.array(colours, dtype=float)[None, None], axis=-1).argmin(-1)
    fam = np.array(owner)[near]
    keep = solid & np.isin(fam, [m for m, f in enumerate(families) if f[1] == "keep"])
    stone = [m for m, f in enumerate(families) if f[1] != "keep"]
    # One stone for the whole, from all its colours: its range theirs, its base the colour
    # most of it is.
    ramp = [c for m in stone for c in _rgb_list(families[m][0])]
    used = solid & ~keep
    vals, counts = np.unique(a[used][:, :3], axis=0, return_counts=True)
    base = "%02x%02x%02x" % tuple(int(v) for v in vals[counts.argmax()]) if len(vals) else families[0][2]
    ls = [colorsys.rgb_to_hls(*(v / 255 for v in c))[1] for c in ramp]
    lo_l, hi_l = max(0.03, min(ls) - 0.04), min(0.98, max(ls) + 0.04)
    lb = colorsys.rgb_to_hls(*(v / 255 for v in hex_rgb(base)))[1]
    anchor = float(np.clip((lb - lo_l) / max(hi_l - lo_l, 0.05), 0.3, 0.7))
    sb = colorsys.rgb_to_hls(*(v / 255 for v in hex_rgb(base)))[2]
    shift = float(np.clip(sb / 0.5, 0.15, 1.0))
    STONE, KEEP = 0, 1
    ramps = {STONE: make_ramp(base, 11, cool=0.32 * shift, warm=0.18 * shift, anchor=anchor, low=lo_l, high=hi_l), KEEP: [(0, 0, 0)] * 11}
    s = Scene(w, h)
    H = np.full((h, w), -1e9)
    widths = np.zeros((h, w))
    for y in range(h):
        row = solid[y]
        x = 0
        while x < w:
            if not row[x]:
                x += 1
                continue
            x0 = x
            while x < w and row[x]:
                x += 1
            x1 = x  # the run [x0, x1)
            half = (x1 - x0) / 2
            cx = (x0 + x1) / 2
            u = (np.arange(x0, x1) + 0.5 - cx) / half
            widths[y, x0:x1] = x1 - x0
            if x1 - x0 > block * w:  # a block: dressed, bevelled at its ends
                edge = np.minimum(np.arange(x0, x1) + 0.5 - x0, x1 - np.arange(x0, x1) - 0.5)
                H[y, x0:x1] = 6 + np.clip(edge / 3, 0, 1) * 3
            else:
                round_ = np.sqrt(np.clip(1 - u * u, 0, 1)) * half * 0.9
                H[y, x0:x1] = round_
    # Flutes on the shaft: where a row is as wide as the rows round it (not a moulding).
    up, down = np.roll(widths, 1, 0), np.roll(widths, -1, 0)
    up2, down2 = np.roll(widths, 3, 0), np.roll(widths, -3, 0)
    shaft = solid & (np.abs(widths - up2) <= 1) & (np.abs(widths - down2) <= 1) & (widths <= block * w)
    xs = s.xs
    # The flutes follow the round: their spacing is even round the shaft, so they crowd at its sides.
    # The angle round the column at each pixel, from its run's middle.
    run_l = np.zeros((h, w))
    run_r = np.zeros((h, w))
    for y in range(h):
        x = 0
        while x < w:
            if not solid[y, x]:
                x += 1
                continue
            x0 = x
            while x < w and solid[y, x]:
                x += 1
            run_l[y, x0:x], run_r[y, x0:x] = x0, x
    half = np.maximum((run_r - run_l) / 2, 0.5)
    u = np.clip((xs + 0.5 - (run_l + run_r) / 2) / half, -1, 1)
    angle = np.arcsin(u)
    count = np.maximum(np.round(half * np.pi / flutes), 2)
    flute = 1 - np.abs(np.cos(angle * count))
    H = np.where(shaft, H - flute * 1.8, H)
    # A moulding: where the outline steps, a round ring stands out.
    step = solid & ((np.abs(widths - up) >= 2) | (np.abs(widths - down) >= 2))
    ring = step | np.roll(step, 1, 0) | np.roll(step, -1, 0)
    H = np.where(ring & ~shaft, H + 1.2, H)
    grit = value_noise(w, h, 2.0, (seed, "grit"))
    H = H + grit * 0.06
    s.paint(solid & ~keep, H, STONE)
    s.paint(keep, np.where(keep, H + 0.5, 0), KEEP)
    return _render_matched(s, ramps, keep, lum, a, KEEP, ao=0.04, ao_radius=2, shadow=0.25, shadow_reach=6)


# --- Statues ----------------------------------------------------------------------------------


def keeper_statue(width: int, height: int, seed, *, broken=None) -> tuple[Image.Image, Image.Image]:
    """A keeper in stone on a plinth of two courses of dressed blocks: hooded, a mantle
    falling from round shoulders over a robe in straight folds that open toward its hem, its
    forearms out of the mantle to the hands crossed on the pommel of a sword set point down
    (a ridged blade, a guard with knobs), the face deep in the hood's shadow; moss at its foot
    and in the lowest folds, the rain's streaks down it. `broken` (points x, y) is the line
    the statue is snapped off along: above it nothing, along it a fresh rough break.
    Drawn for a picture 160 by 288; another size is scaled to it."""
    r = random.Random(f"keeper {seed}")
    W, H = 160, 288
    s = Scene(W, H)
    xs, ys = s.xs, s.ys
    STONE, PLINTH, MOSS, DARK = 0, 1, 2, 3
    ramps = {
        STONE: make_ramp("8a8d9a", 11, cool=0.34, warm=0.16, anchor=0.55, low=0.08, high=0.86),
        PLINTH: make_ramp("a2a5b0", 10, cool=0.30, warm=0.16, anchor=0.55, low=0.14, high=0.9),
        MOSS: make_ramp("5a7c34", 9, cool=0.34, warm=0.24, anchor=0.55, low=0.08, high=0.70),
        DARK: make_ramp("2a2c38", 5, cool=0.2, warm=0.0, anchor=0.7, low=0.03, high=0.2),
    }
    cx = 80.0
    for top, bottom, half in ((248, 268, 58.5), (268, 288, 73)):  # the plinth
        x = cx - half
        while x < cx + half:
            wide = r.randint(18, 30)
            bx0, bx1 = x, min(x + wide, cx + half)
            inside = (xs >= bx0) & (xs < bx1) & (ys >= top) & (ys < bottom)
            edge = np.minimum.reduce([xs - bx0, bx1 - 1 - xs, ys - top, bottom - 1 - ys])
            s.paint(inside, 4 + np.clip(edge / 2.5, 0, 1) * 4 + value_noise(W, H, 3.0, ("plinth", x)) * 0.4, PLINTH, var=r.choice([-1, 0, 0, 1]))
            x += wide
        mould = (ys >= top) & (ys < top + 3) & (np.abs(xs + 0.5 - cx) < half + 2)
        s.paint(mould, 9 + (top + 3 - ys) * 0.8, PLINTH, force=True)
    top_y, hem = 78, 250  # the robe: broad shoulders, in at the waist, out to its hem
    t = np.clip((ys - top_y) / (hem - top_y), 0, 1)
    half = 33 - 6 * np.sin(np.minimum(t / 0.45, 1) * math.pi / 2) + 12 * np.clip((t - 0.45) / 0.55, 0, 1) ** 1.4
    half = half * np.minimum(1.0, 0.5 + t * 10)
    u = (xs + 0.5 - cx) / half
    fold = np.sin(u * math.pi * (3 + 2 * t) + 0.6) * (0.4 + 2.6 * t)
    s.paint((np.abs(u) < 1) & (ys >= top_y) & (ys < hem), 10 + np.sqrt(np.clip(1 - u * u, 0, 1)) * half * 0.5 + fold, STONE)
    mt = np.clip((ys - 80) / 74, 0, 1)  # the mantle, open in front
    shoulder = np.sqrt(np.clip((ys - 78) / 16, 0, 1))
    for side in (-1, 1):
        inner = cx + side * (10 + 8 * mt)
        outer = cx + side * ((half + 3) * shoulder)
        lo, hi = np.minimum(inner, outer), np.maximum(inner, outer)
        mu = (xs + 0.5 - (lo + hi) / 2) / ((hi - lo) / 2 + 0.01)
        cape = (xs >= lo) & (xs < hi) & (ys >= 78) & (ys < 150 + 10 * np.abs(xs + 0.5 - cx) / 36)
        s.paint(cape, 18 + np.sqrt(np.clip(1 - mu * mu, 0, 1)) * 8 + np.sin(mu * math.pi * 2.0 + side * 1.3) * 1.8 * mt, STONE)
    for side in (-1, 1):  # the forearms, a cuff at each wrist
        s.limb(cx + side * 17, 116, cx + side * 8, 144, 6.5, 5.5, 26, STONE)
        s.limb(cx + side * 10, 139, cx + side * 8.5, 145, 7, 7, 27, STONE)
    bt = np.clip((ys - 164) / (246 - 164), 0, 1)  # the sword
    bw = 9.0 - 6.0 * bt**1.6
    bu = (xs + 0.5 - cx) / bw
    s.paint((np.abs(bu) < 1) & (ys >= 164) & (ys < 246), 40 + (1 - np.abs(bu)) * 7, STONE, var=2)
    s.paint((np.abs(ys - 158) < 4) & (np.abs(xs + 0.5 - cx) < 28), 42 + np.sqrt(np.clip(1 - ((ys - 158) / 4) ** 2, 0, 1)) * 3, STONE, force=True)
    for kx in (cx - 28, cx + 28):
        ball(s, kx, 158, 5, 6, 42, 4, STONE)
    ball(s, cx, 148, 13, 11, 40, 8, STONE)  # the hands over the grip
    ball(s, cx, 132, 7, 7, 44, 5, STONE)  # the pommel
    hood = ((xs + 0.5 - cx) / 24) ** 2 + ((ys + 0.5 - 60) / 30) ** 2  # the hood and the face in it
    peak = (ys < 40) & (np.abs(xs + 0.5 - cx - (ys - 40) * 0.25) < (ys - 26) * 0.5)
    s.paint((hood <= 1) | peak, 22 + np.sqrt(np.clip(1 - hood, 0, 1)) * 16, STONE)
    face = ((xs + 0.5 - cx) / 12.5) ** 2 + ((ys + 0.5 - 68) / 17) ** 2
    s.paint(face <= 1, 22 + np.sqrt(np.clip(1 - face, 0, 1)) * 3, DARK, force=True)
    s.H = np.where((face > 1) & (face <= 1.5) & (hood <= 1), s.H + 3, s.H)
    if broken is not None:  # snapped off: nothing above the line, a rough fresh break along it
        bx, by = zip(*broken)
        line = np.interp(xs, bx, by) + value_noise(W, H, 2.5, ("break", seed)) * 3
        figure = s.mat != PLINTH
        gone = figure & (ys < line)
        s.mat[gone] = -1
        s.H[gone] = -1e9
        fresh = figure & ~gone & (ys < line + 4)
        s.H = np.where(fresh, s.H + value_noise(W, H, 1.5, ("chips", seed)) * 2.5, s.H)
        s.var = np.where(fresh, s.var + 1, s.var)
    damp = value_noise(W, H, 6.0, ("moss", seed))  # wear: moss low down, streaks of rain
    fine_damp = value_noise(W, H, 2.5, ("moss fine", seed))
    mossy = (s.mat == STONE) & (ys > 222) & (damp + fine_damp * 0.5 > 0.7 - (ys - 222) / 60)
    mossy |= (s.mat == PLINTH) & (ys < 254) & (damp + fine_damp * 0.5 > 0.45)
    s.paint(mossy, s.H + 0.6, MOSS, var=np.where(fine_damp > 0.5, 0, -1), force=True)
    streak = value_noise(W, H, 4.0, ("streak", seed)) + np.sin(xs * 1.7) * 0.2
    s.var = np.where((s.mat == STONE) & (streak > 0.62) & (ys > 100), s.var - 1, s.var)
    picture, normal = render(s, ramps, ao=0.05, ao_radius=5, shadow=0.38, shadow_reach=14, shadow_drop=0.6, lo=0.08, hi=0.95, gamma=1.2)
    if (width, height) != (W, H):
        picture, normal = picture.resize((width, height), Image.NEAREST), normal.resize((width, height), Image.NEAREST)
    return picture, normal


def angel_statue(old: Image.Image, seed, variant: int = 0) -> tuple[Image.Image, Image.Image]:
    """An angel in white marble over the plan of its first-style drawing (`old`, whose wing
    outline it keeps): a plinth of dressed blocks under a moulded cornice; a robe that falls
    round from the shoulders in folds that open toward the hem, a gold belt; sleeves from the
    shoulders to the hands joined at the breast; a veiled head bowed a little, a gold halo
    over it; and two wings raised over it, each a mass of feathers laid in rows, the small
    coverts near the shoulder over the long flight feathers that hang along its lower edge,
    each feather round across with its shaft down it."""
    a = np.array(old.convert("RGBA"))
    H_, W_ = a.shape[:2]
    r = random.Random(f"angel {seed} {variant}")
    s = Scene(W_, H_)
    xs, ys = s.xs, s.ys
    MARBLE, PLINTH, GOLD, SHADE = 0, 1, 2, 3
    ramps = {
        MARBLE: make_ramp("d4d2dc", 11, cool=0.30, warm=0.12, anchor=0.6, low=0.30, high=0.98),
        PLINTH: make_ramp("d0ceda", 10, cool=0.30, warm=0.12, anchor=0.6, low=0.4, high=0.98),
        GOLD: make_ramp("e0aa2c", 8, cool=0.25, warm=0.2, anchor=0.5, low=0.22, high=0.92),
        SHADE: make_ramp("6a6880", 5, cool=0.2, warm=0.0, anchor=0.6, low=0.2, high=0.45),
    }
    solid = a[..., 3] > 0
    rows = np.nonzero(solid.any(1))[0]
    cx = (np.nonzero(solid[rows[-1]])[0].min() + np.nonzero(solid[rows[-1]])[0].max() + 1) / 2
    # The plinth and its cornice, as the drawing has them.
    def span(y):
        x = np.nonzero(solid[min(max(int(y), 0), H_ - 1)])[0]
        return (x.min(), x.max() + 1) if len(x) else (cx, cx)
    plinth_top = next(y for y in range(H_ - 1, 0, -1) if span(y)[1] - span(y)[0] > span(H_ - 1)[1] - span(H_ - 1)[0] + 6)
    cornice_top = plinth_top
    while span(cornice_top - 1)[1] - span(cornice_top - 1)[0] > span(H_ - 1)[1] - span(H_ - 1)[0] + 6:
        cornice_top -= 1
    px0, px1 = span(H_ - 1)
    y = plinth_top + 1
    course = 0
    while y < H_:
        x = px0 - (course % 2) * 9
        while x < px1:
            wide = r.randint(16, 26)
            bx0, bx1 = max(x, px0), min(x + wide, px1)
            if bx1 - bx0 > 2:
                inside = (xs >= bx0) & (xs < bx1) & (ys >= y) & (ys < min(y + 12, H_))
                edge = np.minimum.reduce([xs - bx0, bx1 - 1 - xs, ys - y, min(y + 12, H_) - 1 - ys])
                s.paint(inside, 4 + np.clip(edge / 2, 0, 1) * 3, PLINTH, var=r.choice([-1, 0, 0, 1]))
            x += wide
        y += 12
        course += 1
    c0, c1 = span(cornice_top + 2)
    corn = (xs >= c0) & (xs < c1) & (ys >= cornice_top) & (ys <= plinth_top)
    s.paint(corn, 9 + np.sqrt(np.clip(1 - ((ys - (cornice_top + plinth_top) / 2) / max((plinth_top - cornice_top) / 2, 1)) ** 2, 0, 1)) * 3, PLINTH, force=True)
    # The robe: round, its folds opening toward the hem.
    shoulder_y, hem_y = 150, cornice_top
    t = np.clip((ys - shoulder_y) / (hem_y - shoulder_y), 0, 1)
    halfs = np.array([(span(yy)[1] - span(yy)[0]) / 2 if yy > 290 else 0 for yy in range(H_)])
    waist = halfs[300] if halfs[300] else 46
    hem = halfs[hem_y - 4] if halfs[hem_y - 4] else 78
    profile = np.array([halfs[yy] if halfs[yy] else 0 for yy in range(H_)])  # below the wings, the drawing's own outline
    profile = np.convolve(np.pad(profile, 6, mode="edge"), np.ones(13) / 13, mode="same")[6:-6]  # smoothed: a pixel's step is no ring
    half = np.where(ys < 292, 30 + (waist - 30) * np.clip((ys - shoulder_y) / 140, 0, 1), np.maximum(profile[ys.astype(int)], 1))
    u = (xs + 0.5 - cx) / half
    fold = np.sin(u * math.pi * (3 + 3 * t) + 0.5) * (0.5 + 3.0 * t)
    s.paint((np.abs(u) < 1) & (ys >= shoulder_y) & (ys < hem_y), 14 + np.sqrt(np.clip(1 - u * u, 0, 1)) * half * 0.45 + fold, MARBLE)
    belt = (np.abs(u) < 1.02) & (np.abs(ys - 300) < 2.5)
    s.paint(belt, 14 + np.sqrt(np.clip(1 - u * u, 0, 1)) * half * 0.45 + 1.5, GOLD, force=True)
    # The sleeves to the joined hands.
    for side in (-1, 1):
        s.limb(cx + side * 26, 166, cx + side * 9, 214, 10, 7, 26, MARBLE)
        s.limb(cx + side * 12, 205, cx + side * 6, 224, 9, 9, 27, MARBLE)
    ball(s, cx, 214, 8, 13, 36, 6, MARBLE)  # the hands, palm to palm
    # The head, a veil over it falling to the shoulders; the halo where the drawing has its gold.
    ball(s, cx, 132, 16, 19, 28, 13, MARBLE)
    ball(s, cx, 124, 17, 11, 31, 8, MARBLE)  # the veil over the crown of the head
    face = ((xs + 0.5 - cx) / 9) ** 2 + ((ys + 0.5 - 138) / 11) ** 2
    s.H = np.where(face <= 1, s.H - (1 - face) * 2.0, s.H)
    gold = solid & (a[..., 0] > 190) & (a[..., 2] < 140) & (ys < 150)
    ring = np.zeros((H_, W_))
    if gold.any():
        near = blur(gold.astype(float), 1)
        ring = near
    s.paint(gold, 48 + ring * 2, GOLD, force=True)
    # The wings: a fan of feathers from each shoulder, in bands, each band under the one
    # nearer the shoulder and its feathers longer; each feather pointing out from the
    # shoulder, round across, its shaft down it, over the next one along.
    body = (np.abs(xs + 0.5 - cx) < np.where(ys < 150, 22, 34)) & (ys >= 90)
    wing = solid & ~body & (ys < 300) & ~gold
    for side in (-1, 1):
        mask = wing & ((xs + 0.5 - cx) * side > 0)
        if not mask.any():
            continue
        sx, sy = cx + side * 28, 175.0  # the shoulder the wing grows from
        wy, wx = np.nonzero(mask)
        rho = np.hypot(wx - sx, wy - sy)
        reach = rho.max()
        band = 0
        start = 0.0
        while start < reach:
            depth = 16 + 14 * (start / reach)  # bands grow longer outward
            mid = start + depth * 0.55
            length = depth * 1.45
            wide = 5.0 + 4.0 * (start / reach)
            step = wide * 1.3 / max(mid, 1)
            theta = -math.pi
            while theta < math.pi:
                fx, fy = sx + math.cos(theta) * mid, sy + math.sin(theta) * mid
                ix, iy = int(fx), int(fy)
                if 0 <= ix < W_ and 0 <= iy < H_ and blur(mask.astype(float), 0)[iy, ix] if False else (0 <= ix < W_ and 0 <= iy < H_ and mask[iy, ix]):
                    ang = theta + r.uniform(-0.05, 0.05)
                    ca, sa = math.cos(ang), math.sin(ang)
                    x0, x1 = max(int(fx - length - 3), 0), min(int(fx + length + 3), W_)
                    y0, y1 = max(int(fy - length - 3), 0), min(int(fy + length + 3), H_)
                    px, py = xs[y0:y1, x0:x1] + 0.5 - fx, ys[y0:y1, x0:x1] + 0.5 - fy
                    along, across = px * ca + py * sa, -px * sa + py * ca
                    q = (np.maximum(along, 0) / (length * 0.6)) ** 2 + (np.minimum(along, 0) / (length * 0.45)) ** 2 + (across / wide) ** 2
                    feather = (q <= 1) & mask[y0:y1, x0:x1]
                    shaft = (np.abs(across) < 0.55) & (along > -length * 0.3)
                    z = 34 - band * 2.2 + np.sqrt(np.clip(1 - q, 0, 1)) * wide * 0.7 - shaft * 0.6 - np.clip(along / length, 0, 1) * 1.5
                    s.paint(feather, z, MARBLE, var=-1 if r.random() < 0.15 else 0, at=(y0, x0))
                theta += step
            start += depth * 0.62
            band += 1
        under = mask & (s.mat < 0)  # what no feather covers: the wing under them
        s.paint(under, np.full((H_, W_), 8.0), MARBLE, var=-1)
        # Its leading edge: a rounded band along the wing's top, over the feathers.
        top = np.where(mask.any(0), mask.argmax(0), H_)
        below = ys - top[None, :]
        edge = mask & (below >= 0) & (below < 11)
        s.paint(edge, 40 + np.sqrt(np.clip(1 - ((below - 5) / 6) ** 2, 0, 1)) * 4, MARBLE, force=True)
    keep = np.zeros((H_, W_), dtype=bool)
    s.mat[~solid] = -1  # inside the drawing's outline, always
    return render(s, ramps, ao=0.05, ao_radius=4, shadow=0.3, shadow_reach=10, shadow_drop=0.6, lo=0.1, hi=0.97, gamma=1.15, outline=False)
