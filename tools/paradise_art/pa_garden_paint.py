"""Painting helpers for the Celestial Garden kit: palette, relief shading of
height fields (architecture), marble texture, and a supersampled painter for
foliage (ivy leaves, grass blades, blossoms, lilies)."""
from __future__ import annotations

import math

from PIL import Image, ImageDraw

from pa_core import F32, gblur, grad3, hexc, lerp, np, smoothstep, wrapd
from pa_core import vnoise_fast as vnoise

# ------------------------------------------------------------------ palette

MARBLE = {
    "light": hexc("fff7e6"), "base": hexc("f2e5cb"), "shade": hexc("d6c5a6"), "deep": hexc("a8957a"),
    "cool": hexc("b9c2c8"), "vein": hexc("cbbca2"), "grime": hexc("8f8a72"),
}
GOLD = {"dark": hexc("8a5c18"), "mid": hexc("cf9c3c"), "lit": hexc("ffe39a"), "hi": hexc("fff6d6")}
GREEN = {
    "deep": hexc("16443e"), "shadow": hexc("235f50"), "dark": hexc("2f7a43"), "mid": hexc("4f9d3a"),
    "lit": hexc("8cc63c"), "tip": hexc("d2ea78"),
}
SKY = {"top": hexc("2a6cc4"), "upper": hexc("4e94dc"), "mid": hexc("8cc0ec"), "low": hexc("cbe3f6"),
       "horizon": hexc("e6f1fa"), "haze": hexc("d4e6f6"), "sun": hexc("fff3d2")}
ROCK = {"light": hexc("d8cfc2"), "mid": hexc("b3a99c"), "shade": hexc("8a8382"), "deep": hexc("5f5f6a"),
        "warm": hexc("e8dccb")}
WHITE_FLOWER = hexc("fbfaf4")
YELLOW = hexc("ffd84a")
WATER = {"white": hexc("f6fbff"), "blue": hexc("b8dcf2"), "deep": hexc("7eb6e0")}

LIGHT3 = (0.42, -0.55, 0.72)   # soft, from the upper right


# ------------------------------------------------------------------ relief shading

def relief(height, strength=1.0, L=LIGHT3, ambient=0.70, diffuse=0.36, blur=0.8, wrap_x=False, wrap_y=False):
    """Brightness from a height field (pixel units): soft so 2D lights can do the rest."""
    h = gblur(height, blur, wrap_x, wrap_y) if blur else height
    if wrap_x or wrap_y:
        gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 if wrap_x else np.gradient(h, axis=1)
        gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 if wrap_y else np.gradient(h, axis=0)
    else:
        gy, gx = np.gradient(h)
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    Lv = np.asarray(L, F32)
    Lv = Lv / np.linalg.norm(Lv)
    d = np.clip((nx * Lv[0] + ny * Lv[1] + nz * Lv[2]) / ln, 0, 1)
    return ambient + diffuse * d


def cavity(height, r=4.0, amount=0.5, wrap_x=False, wrap_y=False):
    """Ambient occlusion-ish darkening where the surface is lower than its surroundings."""
    b = gblur(height, r, wrap_x, wrap_y)
    return 1 - amount * np.clip((b - height) / max(1e-3, r * 0.6), 0, 1)


def marble(w, h, seed, wrap_x=False, wrap_y=False, scale=1.0, veins=0.35):
    """Cream marble colour field (h,w,3)."""
    n1 = vnoise(w, h, 160 * scale, seed, 4, 0.5, wrap_x, wrap_y)
    n2 = vnoise(w, h, 40 * scale, seed + 1, 3, 0.5, wrap_x, wrap_y)
    rgb = grad3([(0.0, MARBLE["shade"]), (0.5, MARBLE["base"]), (1.0, MARBLE["light"])], 0.25 + 0.6 * n1)
    warp = vnoise(w, h, 90 * scale, seed + 2, 3, 0.5, wrap_x, wrap_y)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    kx = round(w / (260 * scale)) or 1
    ky = round(h / (380 * scale)) or 1
    f = np.sin(2 * math.pi * (kx * x / w + ky * y / h) + warp * 9.0)
    v = np.exp(-(f / 0.07) ** 2) * (0.3 + 0.7 * n2) * veins
    rgb = lerp(rgb, MARBLE["vein"], v)
    fine = vnoise(w, h, 3, seed + 3, 2, 0.5, wrap_x, wrap_y)
    rgb = rgb * (0.985 + 0.03 * fine)[..., None]
    return rgb.astype(F32)


def gold_shade(bright):
    return grad3([(0.0, GOLD["dark"]), (0.55, GOLD["mid"]), (0.85, GOLD["lit"]), (1.0, GOLD["hi"])],
                 np.clip(bright, 0, 1))


def lathe_height(w, h, hw_rows, cx=None, square=None):
    """Height field of a turned (round) form: per-row half widths hw_rows (len h).
    `square` (per row, 0..1) flattens the front for box-like plinths."""
    cx = (w - 1) / 2 if cx is None else cx
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    hw = np.asarray(hw_rows, F32)[:, None]
    u = np.clip(np.abs(x - cx) / np.maximum(hw, 1e-3), 0, 1)
    if square is not None:
        p = 2 + 10 * np.asarray(square, F32)[:, None]
        prof = (1 - u ** p) ** (1 / p)
    else:
        prof = np.sqrt(1 - u * u)
    height = hw * prof
    mask = np.clip(hw + 0.5 - np.abs(x - cx), 0, 1)
    return height.astype(F32), mask.astype(F32)


# ------------------------------------------------------------------ painter

def rgba(c, a=1.0):
    c = np.clip(np.asarray(c, F32), 0, 1)
    return (int(c[0] * 255 + 0.5), int(c[1] * 255 + 0.5), int(c[2] * 255 + 0.5), int(a * 255 + 0.5))


class Painter:
    """Supersampled RGBA painting with flat-coloured polygons, for foliage."""

    def __init__(self, w, h, ss=3, wrap_x=False, wrap_y=False):
        self.w, self.h, self.ss, self.wrap, self.wrap_y = w, h, ss, wrap_x, wrap_y
        self.img = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def _o1(self, vs, size, on):
        if not on:
            return [0]
        lo, hi = min(vs), max(vs)
        o = [0]
        if lo < 0:
            o.append(size)
        if hi > size:
            o.append(-size)
        return o

    def _offs2(self, xs, ys):
        return [(ox, oy) for ox in self._o1(xs, self.w, self.wrap) for oy in self._o1(ys, self.h, self.wrap_y)]

    def _offs(self, xs):
        return self._o1(xs, self.w, self.wrap)

    def poly(self, pts, col, a=1.0):
        pts = np.asarray(pts, F32)
        for ox, oy in self._offs2(pts[:, 0], pts[:, 1]):
            q = [((p[0] + ox) * self.ss, (p[1] + oy) * self.ss) for p in pts]
            self.d.polygon(q, fill=rgba(col, a))

    def circle(self, x, y, r, col, a=1.0):
        s = self.ss
        for ox, oy in self._offs2([x - r, x + r], [y - r, y + r]):
            self.d.ellipse([(x + ox - r) * s, (y + oy - r) * s, (x + ox + r) * s, (y + oy + r) * s], fill=rgba(col, a))

    def line(self, pts, w, col, a=1.0):
        pts = np.asarray(pts, F32)
        for ox, oy in self._offs2(pts[:, 0], pts[:, 1]):
            q = [((p[0] + ox) * self.ss, (p[1] + oy) * self.ss) for p in pts]
            self.d.line(q, fill=rgba(col, a), width=max(1, int(w * self.ss)), joint="curve")

    def blade(self, pts, w0, w1, c0, c1):
        """A tapered stroke whose colour runs from c0 (base) to c1 (tip)."""
        pts = np.asarray(pts, F32)
        n = len(pts)
        tang = np.gradient(pts, axis=0)
        tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-6)
        nrm = np.stack([-tang[:, 1], tang[:, 0]], 1)
        hw = np.linspace(w0, w1, n)[:, None] / 2
        L, R = pts + nrm * hw, pts - nrm * hw
        for i in range(n - 1):
            t = i / max(1, n - 2)
            self.poly([L[i], L[i + 1], R[i + 1], R[i]], lerp(np.asarray(c0, F32), np.asarray(c1, F32), t))

    def ivy_leaf(self, x, y, ang, size, col, lit, vein=None):
        """Three-lobed ivy leaf; stem at (x,y), pointing along ang. Lit half lighter."""
        phi = np.linspace(0, 2 * math.pi, 26, endpoint=False)
        r = 0.5 * (1 + 0.24 * np.cos(3 * phi))
        lx = 0.52 + r * np.cos(phi)
        ly = r * np.sin(phi) * 0.95
        ca, sa = math.cos(ang), math.sin(ang)
        X = x + (lx * ca - ly * sa) * size
        Y = y + (lx * sa + ly * ca) * size
        pts = np.stack([X, Y], 1)
        self.poly(pts, col)
        half = pts[(np.sin(phi) <= 0.02)]
        tip = np.array([[x + ca * size * 1.0, y + sa * size * 1.0]])
        base = np.array([[x + ca * size * 0.1, y + sa * size * 0.1]])
        self.poly(np.concatenate([base, half, tip]), lit)
        if vein is not None and size > 5:
            self.line([(x, y), (x + ca * size * 0.85, y + sa * size * 0.85)], max(0.6, size * 0.06), vein, 0.7)

    def lance_leaf(self, x, y, ang, L, W, col, lit=None, curl=0.0):
        t = np.linspace(0, 1, 16)
        w = np.sin(np.pi * np.clip(t, 0, 1) ** 0.75) * W * 0.5
        bend = curl * t * t * L
        ca, sa = math.cos(ang), math.sin(ang)
        cxs, cys = t * L, bend
        up = np.stack([cxs, cys - w], 1)
        dn = np.stack([cxs, cys + w], 1)
        def tf(p):
            return np.stack([x + p[:, 0] * ca - p[:, 1] * sa, y + p[:, 0] * sa + p[:, 1] * ca], 1)
        self.poly(np.concatenate([tf(up), tf(dn[::-1])]), col)
        if lit is not None:
            mid = np.stack([cxs, cys], 1)
            self.poly(np.concatenate([tf(up), tf(mid[::-1])]), lit)

    def blossom(self, x, y, r, petal, centre, n=5, rot=0.0, shade=None):
        for k in range(n):
            a = rot + k * 2 * math.pi / n
            self.circle(x + math.cos(a) * r * 0.55, y + math.sin(a) * r * 0.55, r * 0.5,
                        shade if (shade is not None and math.sin(a) > 0.3) else petal)
        self.circle(x, y, r * 0.28, centre)

    def lily(self, x, y, ang, size, rng):
        """A white trumpet lily seen from the side; (x,y) is where it meets the stem."""
        ca, sa = math.cos(ang), math.sin(ang)

        def P(u, v):
            return (x + (u * ca - v * sa) * size, y + (u * sa + v * ca) * size)
        throat = hexc("e9efc4")
        white = WHITE_FLOWER
        shade = hexc("dfe3da")
        # back petals
        for side in (-1, 1):
            pts = [P(0.05, 0.0), P(0.55, side * 0.18), P(0.95, side * 0.42), P(1.08, side * 0.62),
                   P(0.98, side * 0.40), P(0.6, side * 0.06)]
            self.poly(pts, shade)
        # tube
        self.poly([P(0.0, -0.08), P(0.62, -0.2), P(0.62, 0.2), P(0.0, 0.08)], lerp(throat, white, 0.4))
        # front petals, recurved tips
        for side, col in ((-1, white), (1, lerp(white, shade, 0.5)), (0, white)):
            off = side * 0.3
            pts = [P(0.45, off * 0.5), P(0.85, off + 0.0), P(1.12, off * 1.6 + (0.1 if side == 0 else 0)),
                   P(1.0, off * 1.2 - 0.06), P(0.6, off * 0.4)]
            self.poly(pts, col)
        # stamens
        for k in (-1, 0, 1):
            tip = P(1.08, k * 0.12)
            self.line([P(0.5, k * 0.04), tip], max(0.6, size * 0.03), hexc("c8d890"))
            self.circle(tip[0], tip[1], max(0.6, size * 0.04), hexc("e0902a"))

    def get(self):
        im = self.img.convert("RGBa").resize((self.w, self.h), Image.LANCZOS).convert("RGBA")
        arr = np.asarray(im).astype(F32) / 255
        return arr[..., :3], arr[..., 3]


def green_at(t, warm=0.0):
    """Foliage colour ramp: 0 = deep cool shadow, 1 = sunlit tip."""
    c = grad3([(0.0, GREEN["deep"]), (0.25, GREEN["shadow"]), (0.45, GREEN["dark"]), (0.65, GREEN["mid"]),
               (0.85, GREEN["lit"]), (1.0, GREEN["tip"])], np.clip(t, 0, 1))
    return c


def jitter(c, rng, amt=0.04):
    return np.clip(np.asarray(c, F32) * (1 + rng.uniform(-amt, amt, 3)), 0, 1)
