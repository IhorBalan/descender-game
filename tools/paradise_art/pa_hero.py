"""Ansel, the hero: a painted 2D cutout character for a bone rig.

    python tools/paradise_art/gen_paradise_art.py hero      (or: python pa_hero.py)

Every part is painted in one shared "world" frame (pixels at 128 PPU, y down,
x = 0 at the hip joint, soles at y = 205, top of hair at y = 0), each into its
own rectangle, so the rest-pose joints line up by construction. Parts are
painted at SS x resolution and box-filtered down (crisp anti-aliasing).

Writes Assets/Art/Paradise/Hero/*.png + hero_rig.json and
tools/paradise_art/previews/hero_preview.png. Deterministic.
"""
from __future__ import annotations

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from pa_core import (F32, OUT, PREVIEW_DIR, Image, ImageDraw, gblur, grad3, hexc, lerp, np,  # noqa: E402
                     pillow_normals, smoothstep, vnoise)

SS = 4                     # supersampling while painting
PPU = 128
HERO_DIR = os.path.join(OUT, "Hero")
LIGHT3 = (0.48, -0.62, 0.62)   # warm sun from the upper right (y down)
LIGHT2 = np.array([0.6, -0.8], F32)

# ------------------------------------------------------------------ skeleton (world px, y down)
HIP = (0.0, 100.0)
KNEE = (0.0, 149.0)
ANKLE = (0.0, 192.0)
HEEL = (-7.0, 205.0)
TOE = (18.0, 204.0)
SOLE_Y = 205.0
NECK = (3.0, 38.0)
SH_F = (1.0, 47.0)
SH_B = (-2.0, 46.0)
ELBOW = (1.0, 83.0)
WRIST = (1.5, 111.0)
GRIP = (2.2, 120.5)
WING_ROOT = (-9.0, 56.0)
TUFT_ROOT = (-9.0, 7.5)
DELTA_ARM = (ELBOW[0] - SH_F[0], ELBOW[1] - SH_F[1])

# ------------------------------------------------------------------ palette (deep, shade, base, light, hi)
def ramp(*hs):
    return [hexc(h) for h in hs]


SKIN = ramp("8e5148", "c98b74", "efc3a2", "fde0c8", "fff4e8")
HAIR = ramp("160d0a", "2b1912", "3f281c", "68432c", "a27552")
CLOTH = ramp("6f6a78", "b4aa98", "e8dec8", "faf4e6", "fffdf6")
NAVY = ramp("10142a", "1d2642", "2d3a5e", "48598a", "7c93c4")
LEATHER = ramp("24130b", "472817", "6c4027", "98633a", "cf9a62")
SOLE = ramp("140c08", "24170f", "33231a", "4a3527", "6a5040")
STEEL = ramp("3e4757", "79859a", "b5c0cf", "e2e9f2", "ffffff")
GOLDR = ramp("6e4612", "b07f2c", "d9a948", "ffe39a", "fff6d6")
RIM = hexc("fff0c4")
OUTLINE_DARK = hexc("1c1426")


def shade_col(rmp, v):
    return grad3([(0.0, rmp[0]), (0.33, rmp[1]), (0.62, rmp[2]), (0.86, rmp[3]), (1.0, rmp[4])],
                 np.clip(v, 0, 1))


# ------------------------------------------------------------------ geometry helpers

def cr(pts, closed=False, n=8):
    """Catmull-Rom through pts -> dense polyline."""
    P = np.asarray(pts, np.float64)
    if closed:
        P = np.concatenate([P[-1:], P, P[:2]])
    else:
        P = np.concatenate([P[:1] * 2 - P[1:2], P, P[-1:] * 2 - P[-2:-1]])
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for t in np.linspace(0, 1, n, endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        out.append(P[-2])
    return np.array(out)


def band(mask, w):
    """Inner band of a mask, about w (final) px wide."""
    b = gblur(mask, w * SS)
    return np.clip(mask * (1 - smoothstep(0.5, 0.97, b)), 0, 1)


class Part:
    """A hi-res canvas covering world rect [x0,x1) x [y0,y1) (final px)."""

    def __init__(self, name, x0, y0, x1, y1):
        self.name = name
        self.x0, self.y0 = x0, y0
        self.w, self.h = int(x1 - x0), int(y1 - y0)
        self.W, self.H = self.w * SS, self.h * SS
        self.prem = np.zeros((self.H, self.W, 3), F32)
        self.a = np.zeros((self.H, self.W), F32)
        yy, xx = np.mgrid[0:self.H, 0:self.W].astype(F32)
        self.X = self.x0 + (xx + 0.5) / SS
        self.Y = self.y0 + (yy + 0.5) / SS
        self.joints = {}

    # -- masks -----------------------------------------------------------
    def _new(self):
        im = Image.new("L", (self.W, self.H), 0)
        return im, ImageDraw.Draw(im)

    def _p(self, pts):
        return [((float(x) - self.x0) * SS, (float(y) - self.y0) * SS) for x, y in pts]

    @staticmethod
    def _arr(im):
        return np.asarray(im).astype(F32) / 255

    def poly(self, pts, smooth=False, n=8):
        if smooth:
            pts = cr(pts, True, n)
        im, d = self._new()
        d.polygon(self._p(pts), fill=255)
        return self._arr(im)

    def circ(self, cx, cy, r):
        return self.ell(cx, cy, r, r)

    def ell(self, cx, cy, rx, ry, rot=0.0):
        t = np.linspace(0, 2 * math.pi, 72, endpoint=False)
        ca, sa = math.cos(rot), math.sin(rot)
        x, y = np.cos(t) * rx, np.sin(t) * ry
        return self.poly(np.stack([cx + x * ca - y * sa, cy + x * sa + y * ca], 1))

    def stroke(self, pts, w0, w1=None, smooth=True, cap=True):
        """Tapered stroke along a (smoothed) polyline."""
        w1 = w0 if w1 is None else w1
        P = cr(pts, False, 10) if smooth and len(pts) > 2 else np.asarray(pts, np.float64)
        if len(P) == 2:
            P = np.stack([np.linspace(P[0][0], P[1][0], 8), np.linspace(P[0][1], P[1][1], 8)], 1)
        tang = np.gradient(P, axis=0)
        tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-9)
        nrm = np.stack([-tang[:, 1], tang[:, 0]], 1)
        hw = np.linspace(w0, w1, len(P))[:, None] / 2
        L, R = P + nrm * hw, P - nrm * hw
        im, d = self._new()
        d.polygon(self._p(np.concatenate([L, R[::-1]])), fill=255)
        if cap:
            for (x, y), r in ((P[0], w0 / 2), (P[-1], w1 / 2)):
                d.ellipse([(x - r - self.x0) * SS, (y - r - self.y0) * SS,
                           (x + r - self.x0) * SS, (y + r - self.y0) * SS], fill=255)
        return self._arr(im)

    def soft(self, m, s):
        return gblur(m, s * SS)

    # -- painting --------------------------------------------------------
    def paint(self, mask, rgb, alpha=1.0):
        a = np.clip(mask * alpha, 0, 1).astype(F32)
        rgb = np.asarray(rgb, F32)
        self.prem = rgb * a[..., None] + self.prem * (1 - a[..., None])
        self.a = a + self.a * (1 - a)

    def tint(self, mask, rgb, amount):
        """Mix existing colour towards rgb (keeps alpha)."""
        t = np.clip(mask * amount, 0, 1)[..., None]
        self.prem = self.prem * (1 - t) + np.asarray(rgb, F32) * self.a[..., None] * t

    def mul(self, mask, f):
        self.prem = self.prem * (1 - np.clip(mask, 0, 1)[..., None] * (1 - np.asarray(f, F32)))

    def add(self, mask, rgb, amount=1.0):
        self.prem = self.prem + np.asarray(rgb, F32) * (np.clip(mask * amount, 0, None) * self.a)[..., None]

    def lit(self, mask, sigma, strength=1.0, L=LIGHT3):
        """0..1 diffuse term for an inflated version of mask."""
        nx, ny, nz = pillow_normals(mask, sigma * SS, strength)
        Lv = np.asarray(L, F32)
        Lv = Lv / np.linalg.norm(Lv)
        d = np.clip(nx * Lv[0] + ny * Lv[1] + nz * Lv[2], 0, 1)
        return d, nx, ny

    def material(self, mask, rmp, sigma, strength=1.0, amb=0.22, gain=0.86, ao=None, edge=0.55,
                 edge_w=0.9, rim=0.55, rim_w=1.1, tex=None, vbias=0.0, paint=True, clip=None):
        d, nx, ny = self.lit(mask, sigma, strength)
        v = amb + gain * d + vbias
        if tex is not None:
            v = v + tex
        if ao is not None:
            v = v * ao
        rgb = shade_col(rmp, v)
        face = np.clip(nx * LIGHT2[0] + ny * LIGHT2[1], -1, 1)
        if rim:
            rb = band(mask, rim_w) * smoothstep(0.05, 0.6, face)
            rgb = lerp(rgb, RIM, np.clip(rb * rim, 0, 1))
        if edge:
            eb = band(mask, edge_w) * (0.35 + 0.65 * smoothstep(0.3, -0.5, face))
            rgb = lerp(rgb, rmp[0] * 0.75 + OUTLINE_DARK * 0.25, np.clip(eb * edge, 0, 1))
        m = mask if clip is None else mask * clip
        if paint:
            self.paint(m, rgb)
        return rgb

    def fold(self, pts, w0, w1, col, alpha, blur=0.5, clip=None):
        m = self.soft(self.stroke(pts, w0, w1), blur)
        if clip is not None:
            m = m * clip
        self.paint(m, col, alpha)

    def shadow(self, mask, amount, blur, clip=None, col=None):
        m = self.soft(mask, blur)
        if clip is not None:
            m = m * clip
        col = hexc("2a1e3a") if col is None else col
        self.tint(m, col, amount)

    # -- output ------------------------------------------------------------
    def export(self, soften=0.42, outline=True):
        prem, a = self.prem.copy(), self.a.copy()
        if outline:  # soft silhouette line: darker on the shadow side
            nx, ny, _ = pillow_normals(a, 1.6 * SS, 3.0)
            face = np.clip(nx * LIGHT2[0] + ny * LIGHT2[1], -1, 1)
            eb = band(a, 0.8) * (0.25 + 0.6 * smoothstep(0.25, -0.6, face))
            prem = prem * (1 - eb[..., None] * 0.5)
        h, w = self.h, self.w
        pa = a.reshape(h, SS, w, SS).mean(axis=(1, 3))
        pp = prem.reshape(h, SS, w, SS, 3).mean(axis=(1, 3))
        rgb = pp / np.maximum(pa, 1e-4)[..., None]
        # bleed colour outward so the softened edge keeps the right hue
        pb, ab = gblur(pp, 1.5), gblur(pa, 1.5)
        bled = pb / np.maximum(ab, 1e-4)[..., None]
        rgb = np.where((pa > 0.35)[..., None], rgb, bled)
        if soften:
            pa = np.maximum(gblur(pa, soften), pa * smoothstep(0.6, 1.0, pa))
        rgb = np.clip(rgb, 0, 1)
        pa = np.clip(pa, 0, 1)
        rgb = np.where(pa[..., None] > 0.003, rgb, 0)
        arr = np.concatenate([rgb, pa[..., None]], 2)
        return Image.fromarray((arr * 255 + 0.5).astype(np.uint8), "RGBA")

    def local(self, p):
        return (round(p[0] - self.x0, 3), round(p[1] - self.y0, 3))


def noise(p, scale, seed, amp, octaves=3):
    n = vnoise(p.W, p.H, scale * SS, seed, octaves)
    return (n - 0.5) * amp


# ================================================================== parts

def paint_torso():
    p = Part("torso", -20, 32, 22, 120)
    body = p.poly([(4, 38.2), (8.6, 39.8), (11.8, 43.6), (13.8, 50.5), (14.4, 58.5), (13.8, 67), (12.6, 75),
                   (12.4, 81.5), (10.4, 85.5), (10.8, 90.5), (12.4, 97), (13.8, 104.5), (15.4, 112.2),
                   (8, 113.6), (1, 114.6), (-7, 113.8), (-14.2, 112.6), (-13.6, 104.5), (-12.4, 97),
                   (-10.8, 90.5), (-9.8, 85.5), (-11.6, 80.5), (-11.8, 72), (-12.8, 62.5), (-13.0, 54),
                   (-11.6, 47), (-8.2, 42.4), (-3.6, 39.6), (0.4, 38.4)], smooth=True)
    tex = noise(p, 9, 11, 0.06) + noise(p, 2.5, 12, 0.025)
    upper = smoothstep(91, 87, p.Y)          # 1 above the belt
    # blouse overhang and skirt shading: darker just above / below the belt
    ao = 1 - 0.22 * smoothstep(77, 85, p.Y) * upper - 0.18 * smoothstep(96, 90, p.Y) * (1 - upper)
    ao = ao - 0.10 * smoothstep(-4, -13, p.X)    # far side rolls away from the light
    p.material(body, CLOTH, 5.5, 1.1, ao=ao, tex=tex, rim=0.65)
    clip = body
    dark = CLOTH[1] * 0.8
    litc = CLOTH[4]
    # skirt folds below the belt
    for x0, x1, w in ((-8.5, -10.5, 1.6), (-3.5, -4.5, 1.8), (2.0, 2.6, 1.6), (7.5, 10.0, 1.5)):
        p.fold([(x0, 92), ((x0 + x1) / 2, 102), (x1, 113)], 0.4, w, dark, 0.6, 0.7, clip)
        p.fold([(x0 + 1.4, 93), ((x0 + x1) / 2 + 1.6, 102), (x1 + 1.5, 112)], 0.2, 0.9, litc, 0.35, 0.6, clip)
    # blousing above the belt
    for x0, x1 in ((-8, -6.5), (-2.5, -1.0), (3.5, 4.5), (9, 9.5)):
        p.fold([(x0 - 1.5, 76), (x0, 80.5), (x1, 85)], 0.3, 1.3, dark, 0.55, 0.6, clip)
    # chest drape from the shoulder
    p.fold([(9.5, 48), (10.5, 60), (9.0, 72)], 0.3, 1.4, dark, 0.35, 1.0, clip)
    p.fold([(-9, 50), (-7.5, 62), (-8.5, 74)], 0.3, 1.2, dark, 0.35, 1.0, clip)
    p.fold([(11.0, 62), (8.0, 70), (6.5, 80)], 0.3, 1.0, litc, 0.3, 0.8, clip)
    # collar: v-neck with laces and a gold trim
    vneck = p.poly([(6.4, 39.4), (10.4, 40.4), (9.4, 46.8)])
    p.material(vneck, SKIN, 1.6, 0.8, edge=0.2, rim=0.0, vbias=-0.12)
    p.shadow(p.stroke([(6.6, 40.2), (10.2, 41.0)], 1.2), 0.35, 0.5, vneck)
    for y in (42.4, 44.6):
        p.fold([(7.6, y - 0.8), (9.8, y + 0.6)], 0.5, 0.5, LEATHER[1], 0.85, 0.2)
        p.fold([(7.8, y + 0.6), (9.6, y - 0.9)], 0.5, 0.5, LEATHER[2], 0.85, 0.2)
    collar = p.stroke([(-4.0, 39.8), (0.5, 38.9), (4.5, 39.0), (6.6, 39.6), (9.0, 46.6)], 1.4, 1.0) * body
    collar += p.stroke([(10.6, 40.6), (10.0, 44), (9.2, 46.8)], 1.1, 0.9) * body
    p.material(np.clip(collar, 0, 1), GOLDR, 0.8, 1.0, edge=0.3, rim=0.3, vbias=0.05)
    # hem trim
    below = p.poly([(-20, 120), (-20, 113.2 - 1.7), (-7, 112.2 - 1.7), (1, 113.0 - 1.7), (8, 112.2 - 1.7),
                    (22, 110.8 - 1.7), (22, 120)], smooth=False)
    hem = body * p.soft(below, 0.3) * smoothstep(104, 108, p.Y)
    # follow the actual hem: pixels whose point 1.6px lower is outside the body
    sh = int(1.6 * SS)
    lowered = np.zeros_like(body)
    lowered[:-sh] = body[sh:]
    hem = np.clip(body * (1 - lowered) * smoothstep(104, 108, p.Y), 0, 1)
    p.material(p.soft(hem, 0.15), GOLDR, 0.7, 1.0, edge=0.25, rim=0.2, vbias=0.0)
    # belt
    beltband = p.poly([(-20, 83.6), (22, 85.6), (22, 91.8), (-20, 89.8)])
    grown = np.clip(p.soft(body, 0.55) * 2.2, 0, 1)
    belt = beltband * grown
    p.shadow(beltband * body * 0 + p.poly([(-20, 89.6), (22, 91.6), (22, 93.4), (-20, 91.4)]) * body,
             0.35, 0.8)
    p.material(belt, LEATHER, 1.6, 0.9, edge=0.6, rim=0.6)
    p.fold([(-14, 85.0), (14, 87.0)], 0.35, 0.35, LEATHER[3], 0.55, 0.1, belt)
    p.fold([(-14, 89.3), (14, 91.2)], 0.35, 0.35, LEATHER[0], 0.55, 0.1, belt)
    # belt end hanging from the buckle
    tail = p.stroke([(10.0, 90.5), (10.4, 95.0), (9.8, 99.5)], 2.0, 1.8)
    p.material(tail, LEATHER, 1.0, 0.8, edge=0.6, rim=0.4)
    # buckle (seen side-on at the front)
    buckle = p.poly([(10.4, 84.6), (14.0, 85.0), (14.0, 92.0), (10.4, 91.6)], smooth=False)
    p.material(buckle, GOLDR, 1.0, 1.0, edge=0.6, rim=0.5)
    p.paint(p.poly([(11.4, 86.4), (13.0, 86.6), (13.0, 90.4), (11.4, 90.2)]), GOLDR[1], 0.9)
    # pouch on the back hip
    pouch = p.poly([(-14.6, 89.5), (-8.0, 90.2), (-7.8, 97.5), (-9.2, 100.2), (-13.6, 100.0), (-15.0, 97.0)],
                   smooth=True)
    p.shadow(p.poly([(-15, 99), (-8, 99.5), (-8, 102), (-15, 101.5)]), 0.35, 0.9, body)
    p.material(pouch, LEATHER, 2.2, 1.0, edge=0.6, rim=0.5, vbias=0.03)
    flap = p.poly([(-14.8, 89.6), (-8.0, 90.3), (-8.2, 94.0), (-11.2, 95.4), (-14.7, 93.8)], smooth=True)
    p.material(flap, LEATHER, 1.2, 1.0, edge=0.7, rim=0.6, vbias=0.06)
    p.paint(p.circ(-11.2, 94.0, 0.75), GOLDR[3])
    # the torn back where the wing of light breaks through
    tear = p.poly([(-14.6, 49.5), (-11.8, 50.8), (-10.6, 53.2), (-11.6, 55.6), (-10.2, 58.0),
                   (-11.6, 61.0), (-14.6, 62.4)])
    glow_spill = np.clip(1 - np.hypot(p.X - -12.5, (p.Y - 56) * 0.8) / 9.0, 0, 1) ** 1.6
    p.tint(body, hexc("fff2c8"), 0.55 * glow_spill)
    p.paint(tear * body, hexc("fffbea"))
    p.add(p.soft(tear, 0.8) * body, hexc("ffd98a"), 0.35)
    p.fold([(-11.8, 50.8), (-10.6, 53.2), (-11.6, 55.6), (-10.2, 58.0), (-11.6, 61.0)], 0.45, 0.45,
           GOLDR[3], 0.6, 0.25, body)
    p.joints = {"hip": HIP, "neck": NECK, "shoulder_front": SH_F, "shoulder_back": SH_B,
                "wing_root": WING_ROOT}
    p.pivot = HIP
    return p


def paint_head():
    p = Part("head", -21, -7, 22, 52)
    tex = noise(p, 5, 21, 0.04)
    neck = p.poly([(-3.6, 48), (-4.0, 34), (-2.6, 27.5), (3.0, 29.0), (7.9, 31.5), (8.3, 37), (9.0, 48)],
                  smooth=True, n=5)
    nao = 1 - 0.32 * smoothstep(38, 30.5, p.Y)          # shadow under the jaw
    p.material(neck, SKIN, 3.0, 0.9, ao=nao, tex=tex, rim=0.5, edge=0.4)
    cranium = p.ell(0.3, 15.5, 12.8, 13.6)
    face = p.poly([(4, 3.5), (10, 6.6), (13.2, 11), (13.9, 14.6), (13.3, 17.1), (14.4, 19.4), (16.3, 21.9),
                   (15.7, 23.1), (14.3, 23.5), (14.5, 24.9), (13.9, 26.1), (14.2, 27.3), (13.1, 28.5),
                   (13.9, 30.8), (12.9, 32.9), (8.6, 33.6), (4.2, 32.0), (0.8, 28.8), (-1.2, 24), (-2, 17),
                   (0, 8)], smooth=True, n=6)
    head = np.clip(cranium + face, 0, 1)
    p.material(head, SKIN, 4.0, 1.0, tex=tex, rim=0.6, edge=0.45)
    # planes of the face: eye socket, cheek, nose underside, lip
    p.shadow(p.ell(10.2, 17.6, 2.8, 2.0), 0.18, 0.9, head, SKIN[1])
    p.shadow(p.ell(14.2, 23.6, 1.6, 0.7), 0.35, 0.5, head, SKIN[0])
    p.shadow(p.ell(12.6, 28.4, 1.4, 0.6), 0.25, 0.5, head, SKIN[0])
    p.shadow(p.ell(5.0, 28.0, 5.5, 3.2, 0.5), 0.14, 1.6, head, SKIN[0])        # jaw plane
    p.add(p.soft(p.ell(9.6, 23.2, 2.6, 1.8), 1.0), hexc("f08a7a") - SKIN[2], 0.4)    # blush
    p.add(p.soft(p.ell(14.6, 20.6, 0.9, 1.2), 0.5), hexc("ffffff") - SKIN[3], 0.5)  # nose highlight
    p.add(p.soft(p.ell(10.5, 12.0, 2.0, 1.4), 0.8), RIM - SKIN[3], 0.3)             # brow highlight
    # mouth: small, set, a little downturned
    p.fold([(12.0, 26.4), (13.2, 26.05), (14.2, 26.1)], 0.45, 0.75, hexc("7a3a34"), 0.85, 0.15)
    p.fold([(12.6, 27.3), (13.8, 27.2)], 0.3, 0.6, hexc("ffffff"), 0.18, 0.4)
    # nostril
    p.paint(p.soft(p.ell(14.4, 22.6, 0.7, 0.45, -0.3), 0.15), hexc("8e4e44"), 0.8)
    # eye (3/4 profile, looking ahead and slightly up)
    white = p.poly([(7.9, 18.7), (9.6, 17.3), (11.9, 17.7), (12.5, 18.6), (11.4, 19.7), (9.3, 20.0)], smooth=True)
    p.paint(white, hexc("f4efe9"))
    p.shadow(p.stroke([(8, 17.8), (12.4, 17.7)], 1.0), 0.3, 0.4, white)
    iris = p.ell(11.1, 18.55, 1.25, 1.45) * white
    p.paint(iris, hexc("5d3b26"))
    p.paint(p.ell(11.2, 18.9, 0.8, 0.8) * white, hexc("8a6238"), 0.7)
    p.paint(p.ell(11.3, 18.5, 0.62, 0.72) * white, hexc("1b1010"))
    p.paint(p.circ(11.65, 18.05, 0.38), hexc("ffffff"), 0.95)
    p.fold([(7.7, 18.6), (9.4, 17.1), (11.7, 17.2), (12.9, 18.3)], 0.45, 0.75, hexc("2a1612"), 0.95, 0.12)
    p.fold([(12.4, 17.9), (13.4, 17.4)], 0.6, 0.3, hexc("2a1612"), 0.9, 0.1)      # lash flick
    p.fold([(8.8, 20.2), (10.8, 20.4), (12.0, 19.9)], 0.3, 0.4, SKIN[0], 0.45, 0.2)
    p.fold([(8.2, 16.2), (10.6, 15.6), (12.5, 15.9)], 0.3, 0.4, SKIN[1], 0.4, 0.4)  # lid crease
    # eyebrow: low and straight, front end pulled down (resolve), centre lifted (doubt)
    p.fold([(7.4, 15.0), (9.6, 14.0), (11.6, 14.1), (13.1, 15.0)], 1.15, 0.6, HAIR[1], 0.9, 0.2)
    # ---- hair
    cap = p.poly([(12.6, 9.6), (14.2, 6.2), (11, 1.2), (4, -1.6), (-5, -0.8), (-12, 3.6), (-15.2, 11),
                  (-14.8, 19), (-12.2, 25.4), (-8.2, 30.0), (-4.4, 28.6), (-3.2, 24), (-2.2, 18.6),
                  (1.0, 15.0), (2.8, 15.2), (3.5, 19.8), (4.4, 19.6), (4.8, 14.2), (7.8, 11.6),
                  (10.6, 11.8)], smooth=True, n=6)
    locks = [  # (points, width) messy locks for the silhouette
        ([(4, 4), (10, 7.5), (14.0, 12.4)], 4.6), ([(8, 2), (12.6, 5.2), (15.4, 9.4)], 3.8),
        ([(3, 6), (6.5, 9.8), (8.8, 13.4)], 3.6),
        ([(3, 3), (5.2, -0.4), (8.6, -1.4)], 4.4), ([(-2, 2.6), (-2.2, -0.8), (-0.4, -2.4)], 4.2),
        ([(-6, 3.8), (-9.2, 0.6), (-11.8, -0.2)], 4.2),
        ([(-8.5, 7), (-13.5, 5.4), (-17.0, 6.6)], 4.2), ([(-10, 13), (-15.0, 15.0), (-17.4, 18.2)], 4.0),
        ([(-9.5, 19.5), (-12.8, 23.8), (-14.4, 27.4)], 3.6), ([(-6.5, 24.5), (-7.8, 28.6), (-8.6, 31.4)], 3.0),
    ]
    hair = cap.copy()
    for pts, w in locks:
        hair = np.maximum(hair, p.stroke(pts, w, 1.1))
    htex = noise(p, 3, 23, 0.10)
    hao = 1 - 0.25 * smoothstep(14, 26, p.Y) * smoothstep(0, -10, p.X)
    p.shadow(p.soft(hair, 0.8) * (1 - hair), 0.25, 0.6, head, SKIN[0])    # contact shadow on the skin
    p.material(hair, HAIR, 4.5, 1.2, ao=hao, tex=htex, rim=0.5, rim_w=1.1, edge=0.55)
    # strands: dark grooves + warm sheen following each lock
    rng = np.random.default_rng(7)
    for pts, w in locks:
        P = np.asarray(pts, float)
        for k in range(2):
            off = rng.uniform(-0.25, 0.25, 2) * w
            q = [(x + off[0] * 0.3, y + off[1] * 0.3) for x, y in P]
            p.fold(q, 0.5, 0.15, HAIR[0], 0.35, 0.2, hair)
    for (cx, cy), (dx, dy) in (((7.0, 3.5), (5.5, 4.0)), ((1.5, 0.6), (5.0, -1.0)), ((-4.5, 1.0), (3.8, -2.4)),
                               ((10.0, 6.4), (3.6, 4.8))):
        p.fold([(cx - dx * 0.5, cy - dy * 0.5), (cx, cy - 0.3), (cx + dx * 0.5, cy + dy * 0.5)], 0.4, 0.9,
               HAIR[4], 0.55, 0.35, hair)
    sheen = p.soft(p.ell(4.0, 3.0, 8.0, 3.2, -0.25), 1.0) * hair
    p.add(sheen, HAIR[3] - HAIR[2], 0.5)
    # ear
    ear = p.ell(0.6, 20.6, 2.7, 3.9, 0.12)
    p.material(ear, SKIN, 1.5, 1.0, edge=0.6, rim=0.5, vbias=-0.04)
    p.fold([(1.4, 18.4), (0.3, 19.6), (0.7, 22.0), (1.6, 23.0)], 0.7, 0.5, SKIN[1], 0.7, 0.3, ear)
    p.add(p.soft(p.ell(1.2, 20.2, 1.6, 2.8), 0.6) * ear, hexc("f08a7a") - SKIN[2], 0.25)
    # a few fringe strands over the forehead, catching the light
    p.fold([(10.5, 8.5), (12.6, 11.0), (13.8, 13.4)], 0.9, 0.2, HAIR[2], 1.0, 0.1)
    p.fold([(9.0, 9.4), (10.4, 11.8), (11.0, 14.2)], 0.8, 0.2, HAIR[1], 1.0, 0.1)
    p.joints = {"neck": NECK, "hair_tuft": TUFT_ROOT, "eye": (11.1, 18.6)}
    p.pivot = NECK
    return p


def paint_tuft():
    p = Part("hair_tuft", -26, -6, -2, 14)
    lock = np.maximum(p.stroke([(-8.0, 7.6), (-13.0, 5.2), (-17.2, 2.0), (-21.0, -1.6)], 4.6, 0.3),
                      p.stroke([(-8.5, 8.6), (-13.5, 8.0), (-18.8, 6.0)], 3.6, 0.2))
    p.material(lock, HAIR, 2.2, 1.1, tex=noise(p, 3, 31, 0.08), rim=0.7, edge=0.55)
    p.fold([(-9, 7.4), (-14, 4.8), (-19.5, 0.2)], 0.5, 0.2, HAIR[0], 0.6, 0.15, lock)
    p.fold([(-10, 6.2), (-14.6, 3.6), (-18, 0.8)], 0.6, 0.2, HAIR[4], 0.5, 0.3, lock)
    p.joints = {"root": TUFT_ROOT, "tip": (-21.0, -1.6)}
    p.pivot = TUFT_ROOT
    return p


def paint_upper_arm():
    p = Part("upper_arm", -10, 37, 12, 93)
    cap = p.circ(SH_F[0], SH_F[1], 6.9)
    sleeve = p.poly([(-5.9, 47), (-6.2, 60), (-6.0, 72), (-5.8, 82), (-4.0, 88.4), (1.0, 89.4),
                     (6.0, 88.2), (7.6, 82), (7.2, 72), (7.4, 60), (7.8, 47)], smooth=True, n=6)
    arm = np.clip(cap + sleeve, 0, 1)
    tex = noise(p, 7, 41, 0.06)
    p.material(arm, CLOTH, 4.0, 1.1, tex=tex, rim=0.65, ao=1 - 0.08 * smoothstep(0, -6, p.X))
    dark = CLOTH[1] * 0.8
    p.fold([(-4.5, 50.5), (0.5, 53.5), (6.6, 51.5)], 0.3, 1.0, dark, 0.3, 0.6, arm)     # shoulder seam
    p.fold([(-5.5, 66), (-1.5, 70), (1.5, 74)], 1.2, 0.2, dark, 0.5, 0.6, arm)
    p.fold([(6.8, 70), (3.0, 75), (0.5, 79)], 1.3, 0.2, dark, 0.5, 0.6, arm)
    p.fold([(-5.0, 78), (-1.0, 81), (2.6, 84.5)], 1.2, 0.3, dark, 0.5, 0.6, arm)
    p.fold([(6.5, 80.5), (3.0, 84.5)], 1.0, 0.3, dark, 0.3, 0.6, arm)
    p.fold([(-3.8, 74.5), (0.4, 76.8), (4.0, 80)], 0.3, 0.8, CLOTH[4], 0.4, 0.5, arm)
    # gold band at the cuff
    cuff = p.stroke([(-5.4, 85.0), (-3.0, 87.6), (1.0, 88.6), (5.0, 87.6), (6.8, 84.6)], 1.3, 1.3) * arm
    p.material(cuff, GOLDR, 0.8, 1.0, edge=0.3, rim=0.3)
    p.joints = {"shoulder": SH_F, "elbow": ELBOW}
    p.pivot = SH_F
    return p


def paint_fore_arm():
    p = Part("fore_arm", -9, 74, 13, 130)
    tex = noise(p, 6, 51, 0.05)
    cap = p.circ(ELBOW[0], ELBOW[1], 5.7)
    sleeve = p.poly([(-5.2, 83), (-5.5, 90), (-4.8, 98), (6.0, 98), (6.9, 90), (6.7, 83)], smooth=False)
    sl = np.clip(cap + sleeve, 0, 1)
    p.material(sl, CLOTH, 3.5, 1.0, tex=tex, rim=0.6)
    p.fold([(-4.5, 88), (0, 91), (5, 89)], 0.3, 1.0, CLOTH[1] * 0.8, 0.35, 0.6, sl)
    p.fold([(-4.2, 93.5), (1, 96.2), (5.6, 94.4)], 0.3, 1.0, CLOTH[1] * 0.8, 0.35, 0.6, sl)
    # skin: wrist, back of the hand, curled fingers, thumb
    wrist = p.poly([(-3.0, 106), (-3.2, 113), (5.4, 113), (5.2, 106)], smooth=False)
    handb = p.poly([(-3.3, 110.0), (-3.9, 115.5), (-3.6, 118.6), (6.9, 118.6), (7.6, 114.2), (5.8, 110.0)],
                   smooth=True, n=6)
    fingers = np.zeros_like(handb)
    fcs = (-2.15, 0.55, 3.25, 5.85)
    for i, fx in enumerate(fcs):
        fingers = np.maximum(fingers, p.ell(fx, 121.0 - 0.25 * i, 1.48, 3.7))
    hand = np.clip(wrist + handb + fingers, 0, 1)
    p.material(hand, SKIN, 3.2, 1.0, tex=tex, rim=0.55, edge=0.5)
    for i, fx in enumerate(fcs[:-1]):      # finger separations
        x = fx + 1.35
        p.fold([(x, 118.8), (x - 0.1, 121.5), (x, 124.0)], 0.5, 0.35, SKIN[0], 0.6, 0.2, hand)
    p.fold([(-3.4, 118.5), (2, 118.8), (7.0, 118.2)], 0.5, 0.5, SKIN[1], 0.45, 0.5, hand)   # knuckle line
    for fx in fcs:
        p.add(p.soft(p.ell(fx + 0.2, 118.3, 0.8, 0.6), 0.4), hexc("ffffff") - SKIN[3], 0.35)  # knuckles
    thumb = p.stroke([(5.0, 112.4), (7.6, 115.2), (8.8, 118.6)], 3.4, 2.5)
    p.material(thumb, SKIN, 1.4, 1.0, rim=0.6, edge=0.6, vbias=0.04)
    p.paint(p.soft(p.ell(8.9, 119.2, 0.8, 0.6), 0.2), hexc("f6d6c4"), 0.7)   # thumbnail
    # leather bracer
    bracer = p.poly([(-4.4, 96.2), (-4.0, 103), (-3.6, 109.8), (5.4, 109.8), (5.9, 103), (6.3, 96.2)],
                    smooth=False)
    p.shadow(p.poly([(-4, 109.5), (5.5, 109.5), (5.5, 111.5), (-4, 111.5)]), 0.35, 0.7, hand)
    p.material(bracer, LEATHER, 2.4, 1.0, tex=noise(p, 3, 53, 0.08), rim=0.6, edge=0.6)
    for y in (99.4, 106.0):
        strap = p.poly([(-4.6, y - 1.0), (6.3, y - 1.0), (6.1, y + 1.0), (-4.3, y + 1.0)]) * np.clip(
            p.soft(bracer, 0.4) * 3, 0, 1)
        p.material(strap, LEATHER, 0.6, 1.0, rim=0.4, edge=0.7, vbias=-0.1)
        p.paint(p.ell(4.6, y, 0.9, 0.95), GOLDR[3], 0.9)
    p.fold([(-4.3, 96.6), (6.2, 96.6)], 0.5, 0.5, LEATHER[3], 0.6, 0.15, bracer)
    p.joints = {"elbow": ELBOW, "wrist": WRIST, "grip": GRIP}
    p.pivot = ELBOW
    return p


def paint_thigh():
    p = Part("thigh", -12, 88, 13, 160)
    cap = p.circ(HIP[0], HIP[1], 9.7)
    leg = p.poly([(-9.7, 100), (-9.2, 116), (-7.8, 134), (-6.6, 149), (6.9, 149), (8.6, 133), (9.9, 116),
                  (9.7, 100)], smooth=False)
    knee = p.circ(KNEE[0] + 0.2, KNEE[1], 6.9)
    m = np.clip(p.soft(np.clip(cap + leg + knee, 0, 1), 0.6) * 1.9 - 0.45, 0, 1)
    tex = noise(p, 8, 61, 0.06)
    p.material(m, NAVY, 5.0, 1.1, tex=tex, rim=0.75, rim_w=1.3, vbias=0.04)
    lit = NAVY[3]
    p.fold([(5.5, 104), (6.6, 122), (4.8, 140)], 0.3, 1.2, lit, 0.35, 0.8, m)        # front plane light
    p.fold([(-6.5, 138), (-3.0, 141), (0.5, 145.5)], 1.4, 0.3, NAVY[0], 0.5, 0.5, m)  # knee creases
    p.fold([(-6.8, 143.5), (-3.5, 146.5)], 1.1, 0.3, NAVY[0], 0.45, 0.5, m)
    p.fold([(-6.0, 112), (-3, 118), (-4.0, 126)], 0.3, 1.0, NAVY[0], 0.3, 0.7, m)
    p.add(p.soft(p.ell(4.0, 147, 2.4, 3.4), 1.0) * m, NAVY[4] - NAVY[2], 0.35)       # knee light
    p.joints = {"hip": HIP, "knee": KNEE}
    p.pivot = HIP
    return p


def paint_shin():
    p = Part("shin", -10, 140, 11, 200)
    knee = p.circ(KNEE[0], KNEE[1], 6.6)
    tr = p.poly([(-6.6, 149), (-7.5, 158), (-6.8, 167), (-6.6, 174.5), (-3, 175.5), (2, 175.6), (6.2, 174.5),
                 (6.4, 166), (6.6, 157), (6.6, 149)], smooth=True, n=6)
    trousers = np.clip(knee + tr, 0, 1)
    p.material(trousers, NAVY, 4.5, 1.1, tex=noise(p, 8, 71, 0.06), rim=0.75, rim_w=1.3, vbias=0.04)
    p.fold([(-6.5, 165), (-2, 169), (2.5, 172.5)], 1.2, 0.3, NAVY[0], 0.45, 0.5, trousers)
    p.fold([(6.2, 163), (2.5, 167.5)], 1.0, 0.3, NAVY[0], 0.4, 0.5, trousers)
    p.fold([(4.6, 152), (5.2, 160), (4.6, 166)], 0.3, 1.0, NAVY[3], 0.35, 0.7, trousers)
    # boot shaft with a folded cuff (part of the shin so it follows the leg)
    shaft = np.clip(p.poly([(-6.4, 173), (-6.0, 182), (-5.5, 192), (5.6, 192), (5.8, 182), (6.3, 173)],
                           smooth=False) + p.circ(ANKLE[0], ANKLE[1], 5.4), 0, 1)
    p.shadow(p.poly([(-8, 175), (8, 175), (8, 178.5), (-8, 178.5)]), 0.0, 0.5)
    p.material(shaft, LEATHER, 3.0, 1.0, tex=noise(p, 4, 73, 0.07), rim=0.65, edge=0.6)
    p.fold([(-5.5, 186), (-2.5, 189.5), (0.5, 191.5)], 1.0, 0.3, LEATHER[0], 0.45, 0.5, shaft)
    p.fold([(3.6, 178), (4.4, 186)], 0.3, 1.0, LEATHER[3], 0.45, 0.6, shaft)
    cuff = p.poly([(-7.4, 170.2), (7.2, 170.2), (6.9, 176.8), (-7.0, 176.8)], smooth=False)
    p.shadow(p.poly([(-7, 176.5), (7, 176.5), (7, 178.6), (-7, 178.6)]), 0.4, 0.6, shaft)
    p.material(cuff, LEATHER, 1.8, 1.0, rim=0.7, edge=0.65, vbias=0.07)
    p.fold([(-7.0, 171.0), (7.0, 171.0)], 0.6, 0.6, LEATHER[4], 0.45, 0.2, cuff)
    p.joints = {"knee": KNEE, "ankle": ANKLE}
    p.pivot = KNEE
    return p


def paint_boot():
    p = Part("boot", -11, 182, 22, 208)
    ank = p.circ(ANKLE[0], ANKLE[1], 5.9)
    foot = p.poly([(-6.2, 190), (-7.2, 197), (-7.3, 203.2), (-6.4, 205), (14.5, 205), (17.6, 203.8),
                   (18.4, 200.8), (16.2, 197.8), (10.5, 195.4), (5.6, 191.0), (3, 187.5)], smooth=True, n=6)
    boot = np.clip(ank + foot, 0, 1)
    p.material(boot, LEATHER, 3.0, 1.0, tex=noise(p, 4, 81, 0.07), rim=0.7, edge=0.6)
    sole = boot * smoothstep(202.9, 203.5, p.Y)
    heel = boot * smoothstep(-1.2, -2.0, p.X) * smoothstep(200.2, 200.8, p.Y)
    p.material(np.clip(sole + heel, 0, 1), SOLE, 1.0, 1.0, rim=0.25, edge=0.4, vbias=0.05)
    p.fold([(10, 196.6), (14.5, 198.2), (17.2, 200.4)], 0.3, 1.0, LEATHER[4], 0.5, 0.5, boot)   # toe shine
    p.fold([(2.6, 189.6), (5.6, 195.6)], 1.3, 1.3, LEATHER[0], 0.6, 0.2, boot)                  # strap
    p.fold([(2.8, 189.2), (5.4, 194.8)], 0.5, 0.5, LEATHER[3], 0.6, 0.2, boot)
    p.paint(p.ell(4.3, 192.6, 0.8, 0.8), GOLDR[3], 0.9)
    p.joints = {"ankle": ANKLE, "heel": HEEL, "toe": TOE}
    p.pivot = ANKLE
    return p


def paint_sword():
    p = Part("sword", -16, -13, 88, 13)
    # local frame: grip centre at (0,0), blade along +x
    pom = p.ell(-11.2, 0, 2.6, 3.1)
    grip = p.poly([(-9.2, -2.0), (6.2, -2.0), (6.2, 2.0), (-9.2, 2.0)])
    guard = p.stroke([(7.6, -8.4), (7.6, 8.4)], 2.8, 2.8)
    blade = p.poly([(9, -2.6), (72, -2.4), (85.0, 0), (72, 2.4), (9, 2.6)])
    p.material(grip, LEATHER, 1.2, 1.0, rim=0.4, edge=0.6, amb=0.18)
    for x in np.arange(-8.4, 6.0, 1.6):
        p.fold([(x, -2.1), (x + 1.2, 2.1)], 0.45, 0.45, LEATHER[0], 0.7, 0.1, grip)
    # blade: lit upper bevel, darker lower bevel, fuller groove
    yb = p.Y
    v = np.where(yb < 0, 0.92 - 0.12 * (-yb / 2.6), 0.5 + 0.1 * (yb / 2.6))
    v = v + 0.06 * np.sin(p.X * 0.08)
    rgb = shade_col(STEEL, v)
    p.paint(blade, rgb)
    fuller = p.poly([(11, -0.55), (60, -0.4), (63, 0), (60, 0.4), (11, 0.55)])
    p.paint(fuller, STEEL[1], 0.8)
    p.paint(p.poly([(11, -0.55), (60, -0.4), (60, -0.1), (11, -0.15)]), STEEL[0], 0.6)
    p.paint(band(blade, 0.6) * (yb < 0), STEEL[4], 0.7)
    p.paint(band(blade, 0.6) * (yb > 0), STEEL[0], 0.5)
    p.add(p.soft(p.ell(38, -1.4, 10, 0.5), 0.6) * blade, hexc("ffffff"), 0.6)
    p.material(guard, STEEL, 1.2, 1.0, rim=0.6, edge=0.6, vbias=-0.15)
    p.material(pom, STEEL, 1.2, 1.0, rim=0.6, edge=0.6, vbias=-0.08)
    p.paint(p.circ(-11.6, -0.8, 0.8), STEEL[4], 0.8)
    p.joints = {"grip": (0.0, 0.0), "tip": (85.0, 0.0), "pommel": (-13.8, 0.0)}
    p.pivot = (0.0, 0.0)
    return p


# ------------------------------------------------------------------ the wing of light

WING_SPINE = [WING_ROOT, (-14.5, 42), (-22.5, 27), (-33.5, 12.5), (-45.5, 1.5), (-56.5, -4.5), (-63, -6.5)]


def _wing_feathers():
    """(t, base pt, angle deg (screen, y down), length, width, formed 0..1) back to front."""
    sp = cr(WING_SPINE, False, 20)
    seg = np.r_[0, np.cumsum(np.linalg.norm(np.diff(sp, axis=0), axis=1))]
    total = seg[-1]
    rng = np.random.default_rng(5)
    out = []
    n = 10
    for i in range(n):
        t = 0.10 + 0.9 * (i / (n - 1)) ** 0.9
        j = int(np.searchsorted(seg, t * total).clip(1, len(sp) - 1))
        b = sp[j]
        ang = 96 + 72 * t ** 1.1 + rng.uniform(-3, 3)
        L = 14 + 26 * math.sin(math.pi * min(1.0, t * 0.92) ** 1.1 * 0.62) ** 1.0
        if t > 0.9:
            L *= 1 - (t - 0.9) * 2.2
        formed = float(np.clip(1.25 - 1.35 * t, 0.0, 1.0))
        out.append((t, b, ang, L, 0.34 * L ** 0.9 + 2.0, formed))
    return out[::-1], sp


def feather_poly(b, ang, L, W, curve=0.12):
    t = np.linspace(0, 1, 18)
    w = W * 0.5 * smoothstep(0, 0.3, t) ** 0.6 * np.sqrt(np.clip(1 - t ** 2.6, 0, 1))
    bend = curve * L * t * t
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    cx, cy = t * L, bend
    up = np.stack([cx, cy - w], 1)
    dn = np.stack([cx, cy + w], 1)
    outl = np.concatenate([up, dn[::-1]])
    spine = np.stack([cx, cy], 1)

    def tf(q):
        return np.stack([b[0] + q[:, 0] * ca - q[:, 1] * sa, b[1] + q[:, 0] * sa + q[:, 1] * ca], 1)
    return tf(outl), tf(spine)


def paint_wing():
    p = Part("wing", -108, -40, 2, 70)
    feathers, sp = _wing_feathers()
    white = hexc("fffdf2")
    gold = hexc("ffd47a")
    deep = hexc("e8b65a")
    rng = np.random.default_rng(9)
    acc = np.zeros_like(p.a)
    for t, b, ang, L, W, f in feathers:
        outl, spine = feather_poly(b, ang, L, W, 0.10 + 0.08 * t)
        m = p.poly(outl)
        if f > 0.02:
            along = np.clip(np.hypot(p.X - b[0], p.Y - b[1]) / L, 0, 1)
            col = lerp(white, gold, np.clip(along * 0.8 + (1 - f) * 0.3, 0, 1))
            col = lerp(col, deep, 0.25 * band(m, 1.4))
            p.paint(m, col, 0.2 + 0.6 * f * (1 - 0.4 * along))
        # vane edge: bright line, broken up where the feather is unformed
        edge = band(m, 0.55)
        if f < 0.6:
            dash = smoothstep(0.35, 0.6, noise(p, 2.2, int(rng.integers(1e6)), 1.0) + 0.5)
            edge = edge * (dash * (1 - f) + f)
        p.paint(edge, lerp(hexc("f2b84c"), white, 0.25 + 0.35 * f), 0.6 + 0.35 * f)
        q = p.stroke(spine[: int(len(spine) * (0.7 if f > 0.3 else 0.9))], 0.8, 0.2)
        p.paint(q, white, 0.35 + 0.3 * (1 - f))
        acc = np.maximum(acc, m)
    # coverts: small rounded feathers hugging the arm of the wing (most formed near the root)
    for k in range(9):
        t = 0.04 + 0.5 * k / 8
        j = int(t * (len(sp) - 1))
        b = sp[j]
        f = float(np.clip(1.1 - 1.2 * t, 0.15, 1))
        for row, (dl, da, sc) in enumerate(((4.0, 0, 1.0), (1.2, 10, 0.7))):
            ang = 110 + 60 * t + da + rng.uniform(-6, 6)
            L = (7.5 + 6 * t) * sc
            outl, spine = feather_poly((b[0] - dl * 0.3, b[1] + dl * 0.2), ang, L, L * 0.55, 0.05)
            m = p.poly(outl)
            p.paint(m, lerp(white, hexc("fff0c8"), 0.3 * row), 0.3 + 0.45 * f)
            p.paint(band(m, 0.4) * smoothstep(0, 1, (p.X - b[0]) * 0 + 1), gold, 0.3)
    # the arm of the wing: a bright tapering bone of light
    arm = p.stroke(WING_SPINE, 4.6, 1.2)
    p.paint(p.soft(arm, 0.6), hexc("fff6dc"), 0.95)
    p.paint(p.stroke(WING_SPINE, 2.0, 0.5), hexc("ffffff"), 1.0)
    # motes where the wing has not formed yet
    for _ in range(26):
        t = rng.uniform(0.45, 1.0)
        j = int(np.searchsorted(np.linspace(0, 1, len(sp)), t).clip(0, len(sp) - 1))
        b = sp[j]
        ang = math.radians(110 + 60 * t + rng.uniform(-20, 20))
        r = rng.uniform(4, 40)
        x, y = b[0] + math.cos(ang) * r, b[1] + math.sin(ang) * r
        rr = rng.uniform(0.5, 1.1)
        p.paint(p.soft(p.circ(x, y, rr), 0.3), hexc("fffbe8"), 0.9)
    p.joints = {"root": WING_ROOT, "wrist": WING_SPINE[3], "tip": WING_SPINE[-1]}
    p.pivot = WING_ROOT
    return p


def paint_wing_glow(wing_img, wing_part):
    """Additive halo: blurred wing alpha + a hot core along the arm."""
    pad = 16
    x0, y0 = wing_part.x0 - pad, wing_part.y0 - pad
    w, h = wing_part.w + 2 * pad, wing_part.h + 2 * pad
    a = np.zeros((h, w), F32)
    wa = np.asarray(wing_img).astype(F32)[..., 3] / 255
    a[pad:pad + wing_part.h, pad:pad + wing_part.w] = wa
    g = 0.6 * gblur(a, 5.0) + 0.3 * gblur(a, 1.8)
    im = Image.new("L", (w * 2, h * 2), 0)
    d = ImageDraw.Draw(im)
    P = cr(WING_SPINE, False, 10)
    d.line([((x - x0) * 2, (y - y0) * 2) for x, y in P], fill=255, width=8, joint="curve")
    core = np.asarray(im.resize((w, h), Image.BOX)).astype(F32) / 255
    g = g + 0.6 * gblur(core, 3.0)
    yy, xx = np.mgrid[0:h, 0:w].astype(F32)
    root = np.exp(-(((xx + x0 - WING_ROOT[0]) ** 2 + (yy + y0 - WING_ROOT[1]) ** 2) / (2 * 7.0 ** 2)))
    g = np.clip(g + 0.5 * root, 0, 1)
    g = g / max(1e-6, g.max())
    rgb = grad3([(0, hexc("ffb648")), (0.5, hexc("ffd88a")), (1.0, hexc("fff6dc"))], g)
    alpha = np.clip(g * 0.6, 0, 1)
    rgb = np.where(alpha[..., None] > 0.003, rgb, 0)
    img = Image.fromarray((np.concatenate([rgb, alpha[..., None]], 2) * 255 + 0.5).astype(np.uint8), "RGBA")
    return img, (x0, y0)


# ================================================================== build + rig json

PART_FNS = [paint_torso, paint_head, paint_tuft, paint_upper_arm, paint_fore_arm, paint_thigh, paint_shin,
            paint_boot, paint_sword, paint_wing]

DRAW_ORDER = [  # back to front; "_b" = far-side limb (reuse sprite, tint darker)
    "wing_glow(additive, behind or above everything)", "wing", "upper_arm_b", "fore_arm_b", "thigh_b",
    "shin_b", "boot_b", "boot", "shin", "thigh", "hair_tuft", "head", "torso", "sword", "fore_arm", "upper_arm"]


def _dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def build(reg=None):
    os.makedirs(HERO_DIR, exist_ok=True)
    parts, imgs = {}, {}
    for fn in PART_FNS:
        prt = fn()
        img = prt.export(outline=prt.name not in ("wing",), soften=0.0 if prt.name == "wing" else 0.42)
        parts[prt.name] = prt
        imgs[prt.name] = img
    gimg, (gx0, gy0) = paint_wing_glow(imgs["wing"], parts["wing"])
    ha = np.asarray(imgs["head"])[..., 3]
    hair_top = parts["head"].y0 + int(np.nonzero((ha > 127).any(1))[0][0])   # world y of the top of the hair
    rig = {"generator": "tools/paradise_art/pa_hero.py", "character": "Ansel", "pixelsPerUnit": PPU,
           "heightUnits": round((SOLE_Y - hair_top) / PPU, 4),
           "conventions": [
               "pivot_px and joints: pixels, x from the LEFT, y from the TOP of that part's image.",
               "pivot_normalized: Unity sprite pivot, (0,0) = bottom-left.",
               "All parts are drawn in rest pose (rotation 0): limbs hang straight down, sword blade along +x.",
               "Faces RIGHT. Positive angle = counter-clockwise (Unity z); a hanging limb with +angle swings forward.",
               "Child pivot sits at the parent's named joint: torso.neck->head, torso.shoulder_front->upper_arm, "
               "upper_arm.elbow->fore_arm, fore_arm.grip->sword, torso.hip->thigh, thigh.knee->shin, "
               "shin.ankle->boot, torso.wing_root->wing & wing_glow, head.hair_tuft->hair_tuft.",
               "Far-side limbs reuse upper_arm/fore_arm/thigh/shin/boot at torso.shoulder_back / torso.hip, "
               "tinted ~ (0.58, 0.58, 0.70) and sorted behind the torso.",
               "PNG RGBA straight alpha. wing_glow is for additive blending.",
           ],
           "drawOrder": DRAW_ORDER,
           "restPoseWorldPx": {"note": "shared painting frame: y down, x=0 at hip, soles at y=205",
                               "hip": HIP, "knee": KNEE, "ankle": ANKLE, "heel": HEEL, "toe": TOE,
                               "neck": NECK, "shoulder_front": SH_F, "shoulder_back": SH_B, "elbow": ELBOW,
                               "wrist": WRIST, "grip": GRIP, "wing_root": WING_ROOT, "hair_tuft": TUFT_ROOT},
           "parts": {}}
    lengths = {
        "torso": {"hip_to_neck": _dist(HIP, NECK), "hip_to_shoulder_front": _dist(HIP, SH_F)},
        "head": {"neck_to_top_of_hair": NECK[1] - hair_top, "neck_to_eye": _dist(NECK, (11.1, 18.6))},
        "upper_arm": {"shoulder_to_elbow": _dist(SH_F, ELBOW)},
        "fore_arm": {"elbow_to_grip": _dist(ELBOW, GRIP), "elbow_to_wrist": _dist(ELBOW, WRIST)},
        "thigh": {"hip_to_knee": _dist(HIP, KNEE)},
        "shin": {"knee_to_ankle": _dist(KNEE, ANKLE)},
        "boot": {"ankle_to_toe": _dist(ANKLE, TOE), "ankle_to_heel": _dist(ANKLE, HEEL),
                 "ankle_height_above_sole": SOLE_Y - ANKLE[1]},
        "sword": {"grip_to_tip": 85.0, "grip_to_pommel": 13.8},
        "wing": {"root_to_tip": _dist(WING_ROOT, WING_SPINE[-1])},
        "hair_tuft": {"root_to_tip": _dist(TUFT_ROOT, (-21.0, -1.6))},
    }
    for name, prt in parts.items():
        img = imgs[name]
        fn = f"hero_{name}.png"
        img.save(os.path.join(HERO_DIR, fn), optimize=True)
        pv = prt.local(prt.pivot)
        rig["parts"][name] = {
            "file": f"Assets/Art/Paradise/Hero/{fn}", "size_px": [img.width, img.height],
            "pivot_px": list(pv),
            "pivot_normalized": [round(pv[0] / img.width, 5), round(1 - pv[1] / img.height, 5)],
            "joints_px": {k: list(prt.local(v)) for k, v in prt.joints.items()},
            "lengths_units": {k: round(v / PPU, 4) for k, v in lengths.get(name, {}).items()},
            "lengths_px": {k: round(v, 2) for k, v in lengths.get(name, {}).items()},
            "blend": "normal",
        }
        print(f"  wrote {fn} {img.width}x{img.height}")
    gfn = "hero_wing_glow.png"
    gimg.save(os.path.join(HERO_DIR, gfn), optimize=True)
    gpv = (round(WING_ROOT[0] - gx0, 3), round(WING_ROOT[1] - gy0, 3))
    rig["parts"]["wing_glow"] = {
        "file": f"Assets/Art/Paradise/Hero/{gfn}", "size_px": [gimg.width, gimg.height], "pivot_px": list(gpv),
        "pivot_normalized": [round(gpv[0] / gimg.width, 5), round(1 - gpv[1] / gimg.height, 5)],
        "joints_px": {"root": list(gpv)}, "lengths_units": {}, "lengths_px": {}, "blend": "additive"}
    print(f"  wrote {gfn} {gimg.width}x{gimg.height}")
    with open(os.path.join(HERO_DIR, "hero_rig.json"), "w") as f:
        json.dump(rig, f, indent=2)
        f.write("\n")
    print("  wrote hero_rig.json")
    return rig


# ================================================================== preview (FK assembly)

def _rot(v, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return (v[0] * c + v[1] * s, -v[0] * s + v[1] * c)


class Sprite:
    def __init__(self, img, pivot, joints):
        arr = np.asarray(img.convert("RGBA")).astype(F32) / 255
        self.a = arr[..., 3]
        self.prem = arr[..., :3] * self.a[..., None]
        self.pivot = pivot
        self.joints = joints

    def joint(self, name, at, deg, scale):
        j = self.joints[name]
        v = ((j[0] - self.pivot[0]) * scale, (j[1] - self.pivot[1]) * scale)
        r = _rot(v, deg)
        return (at[0] + r[0], at[1] + r[1])


def _place(canvas, spr, at, deg, scale, tint=None, additive=False):
    """Composite sprite so its pivot lands on `at` (canvas px), rotated deg CCW, scaled."""
    H, W = canvas["a"].shape
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    # canvas -> sprite: v = R(-deg) (P - at) / scale + pivot
    A = c / scale
    B = -s / scale
    D = s / scale
    E = c / scale
    C = spr.pivot[0] - (A * at[0] + B * at[1])
    F = spr.pivot[1] - (D * at[0] + E * at[1])
    coeffs = (A, B, C, D, E, F)
    chans = []
    for ch in [spr.prem[..., 0], spr.prem[..., 1], spr.prem[..., 2], spr.a]:
        im = Image.fromarray(ch.astype(np.float32), "F")
        chans.append(np.asarray(im.transform((W, H), Image.AFFINE, coeffs, resample=Image.BILINEAR)))
    prem = np.stack(chans[:3], 2)
    al = np.clip(chans[3], 0, 1)
    if tint is not None:
        prem = prem * np.asarray(tint, F32)
    if additive:
        canvas["rgb"] = canvas["rgb"] + prem
    else:
        canvas["rgb"] = prem + canvas["rgb"] * (1 - al[..., None])


POSES = {
    "idle": dict(torso=-2, head=3, wing=0, tuft=0,
                 ua_f=5, fa_f=16, sw=-40, ua_b=-6, fa_b=8,
                 th_f=7, sh_f=3, bt_f=0, th_b=-7, sh_b=-6, bt_b=0, air=0),
    "run": dict(torso=-12, head=10, wing=8, tuft=18,
                ua_f=-42, fa_f=24, sw=205, ua_b=48, fa_b=112,
                th_f=42, sh_f=-2, bt_f=14, th_b=-28, sh_b=-92, bt_b=-70, air=6),
    "jump": dict(torso=-6, head=4, wing=-18, tuft=28,
                 ua_f=128, fa_f=158, sw=62, ua_b=-55, fa_b=-25,
                 th_f=82, sh_f=-14, bt_f=6, th_b=48, sh_b=-38, bt_b=-24, air=34),
    "slash": dict(torso=-16, head=10, wing=12, tuft=14,
                  ua_f=104, fa_f=96, sw=22, ua_b=-48, fa_b=-6,
                  th_f=52, sh_f=4, bt_f=0, th_b=-40, sh_b=-48, bt_b=-20, air=0),
}
BACK_TINT = (0.58, 0.58, 0.70)


def _pose_points(sp, pose, hip, scale):
    """Forward kinematics; returns placement list (name, sprite, at, deg, tint, additive) in draw order."""
    P = pose
    t = sp["torso"]
    out = {}
    neck = t.joint("neck", hip, P["torso"], scale)
    shf = t.joint("shoulder_front", hip, P["torso"], scale)
    shb = t.joint("shoulder_back", hip, P["torso"], scale)
    wr = t.joint("wing_root", hip, P["torso"], scale)
    head_deg = P["torso"] + P["head"]
    tuft = sp["head"].joint("hair_tuft", neck, head_deg, scale)

    def arm(sh, ua, fa):
        el = sp["upper_arm"].joint("elbow", sh, ua, scale)
        gr = sp["fore_arm"].joint("grip", el, fa, scale)
        return el, gr

    el_f, gr_f = arm(shf, P["ua_f"], P["fa_f"])
    el_b, gr_b = arm(shb, P["ua_b"], P["fa_b"])

    def leg(th, sh):
        kn = sp["thigh"].joint("knee", hip, th, scale)
        an = sp["shin"].joint("ankle", kn, sh, scale)
        return kn, an
    kn_f, an_f = leg(P["th_f"], P["sh_f"])
    kn_b, an_b = leg(P["th_b"], P["sh_b"])
    wd = P["torso"] + P["wing"]
    L = [("wing_glow", wr, wd, None, True), ("wing", wr, wd, None, False),
         ("fore_arm", el_b, P["fa_b"], BACK_TINT, False),
         ("upper_arm", shb, P["ua_b"], BACK_TINT, False),
         ("boot", an_b, P["bt_b"], BACK_TINT, False), ("shin", kn_b, P["sh_b"], BACK_TINT, False),
         ("thigh", hip, P["th_b"], BACK_TINT, False),
         ("boot", an_f, P["bt_f"], None, False), ("shin", kn_f, P["sh_f"], None, False),
         ("thigh", hip, P["th_f"], None, False),
         ("hair_tuft", tuft, head_deg + P["tuft"], None, False), ("head", neck, head_deg, None, False),
         ("torso", hip, P["torso"], None, False),
         ("sword", gr_f, P["sw"], None, False), ("fore_arm", el_f, P["fa_f"], None, False),
         ("upper_arm", shf, P["ua_f"], None, False)]
    # lowest point of the feet (for grounding)
    feet = []
    for an, bd in ((an_f, P["bt_f"]), (an_b, P["bt_b"])):
        for j in ("heel", "toe"):
            feet.append(sp["boot"].joint(j, an, bd, scale)[1])
    return L, max(feet)


def _render_pose(sp, pose, W, H, ground, scale, bg_rgb):
    canvas = {"rgb": bg_rgb.copy(), "a": np.ones((H, W), F32)}
    _, low = _pose_points(sp, pose, (0, 0), scale)
    hip = (W * 0.52, ground - low - pose["air"] * scale)
    L, _ = _pose_points(sp, pose, hip, scale)
    # a soft contact shadow on the ground
    sh = np.exp(-(((np.mgrid[0:H, 0:W][1] - hip[0]) / (26 * scale)) ** 2 +
                  ((np.mgrid[0:H, 0:W][0] - ground) / (3.2 * scale)) ** 2))
    k = 0.28 / (1 + pose["air"] / 15)
    canvas["rgb"] = canvas["rgb"] * (1 - k * sh[..., None])
    for name, at, deg, tint, add in L:
        _place(canvas, sp[name], at, deg, scale, tint, add)
    return np.clip(canvas["rgb"], 0, 1)


def preview():
    rig_path = os.path.join(HERO_DIR, "hero_rig.json")
    with open(rig_path) as f:
        rig = json.load(f)
    sp = {}
    root = os.path.dirname(os.path.dirname(os.path.dirname(OUT)))
    for name, rec in rig["parts"].items():
        img = Image.open(os.path.join(root, rec["file"]))
        sp[name] = Sprite(img, tuple(rec["pivot_px"]), {k: tuple(v) for k, v in rec["joints_px"].items()})
    CW, CH, scale = 440, 640, 2.0
    ground = 560
    names = list(POSES)
    rows = []
    # row 1: light background
    yy = np.linspace(0, 1, CH, dtype=F32)[:, None, None]
    light_bg = (hexc("f6f3ec") * (1 - yy) + hexc("dfe6ee") * yy) * np.ones((CH, CW, 3), F32)
    light_bg[ground:] = hexc("cfc6b4")
    # row 2: garden crop
    gp = os.path.join(PREVIEW_DIR, "garden_preview.png")
    garden = Image.open(gp).convert("RGB") if os.path.exists(gp) else None
    cells = []
    for row in range(2):
        cols = []
        for i, n in enumerate(names):
            if row == 0 or garden is None:
                bg = light_bg
            else:
                cx = [150, 290, 800, 1010][i]
                gy = 402  # walkway top in garden_preview.png
                crop = garden.crop((cx, gy - ground // 2, cx + CW // 2, gy - ground // 2 + CH // 2))
                bg = np.asarray(crop.resize((CW, CH), Image.BICUBIC)).astype(F32) / 255
            cols.append(_render_pose(sp, POSES[n], CW, CH, ground, scale, bg))
        cells.append(np.concatenate(cols, 1))
    top = np.concatenate(cells, 0)
    # row 3: actual size (1x) on both backgrounds + parts sheet with pivots
    small_h = 300
    strip = np.ones((small_h, top.shape[1], 3), F32) * hexc("ebe7de")
    x = 10
    for n in names:
        bg = np.ones((small_h, 120, 3), F32) * hexc("f6f3ec")
        strip[:, x:x + 120] = _render_pose(sp, POSES[n], 120, small_h, 270, 1.0, bg)
        x += 125
    if garden is not None:
        for i, n in enumerate(names):
            crop = garden.crop((140 + i * 290, 402 - 270, 140 + i * 290 + 120, 402 - 270 + small_h))
            bg = np.asarray(crop).astype(F32) / 255
            strip[:, x:x + 120] = _render_pose(sp, POSES[n], 120, small_h, 270, 1.0, bg)
            x += 125
    out = Image.fromarray((np.concatenate([top, strip], 0) * 255 + 0.5).astype(np.uint8), "RGB")
    # parts sheet (2x) with pivots (red) and joints (blue)
    d = ImageDraw.Draw(out)
    px = x + 10
    py = top.shape[0] + 8
    for name in ["head", "hair_tuft", "torso", "upper_arm", "fore_arm", "thigh", "shin", "boot"]:
        rec = rig["parts"][name]
        img = Image.open(os.path.join(root, rec["file"]))
        im2 = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
        if px + im2.width > out.width - 4:
            break
        bgbox = Image.new("RGB", im2.size, (205, 205, 215))
        bgbox.paste(im2, (0, 0), im2)
        out.paste(bgbox, (px, py))
        for jn, (jx, jy) in rec["joints_px"].items():
            cx, cy = px + jx * 2, py + jy * 2
            d.ellipse([cx - 2.5, cy - 2.5, cx + 2.5, cy + 2.5], outline=(30, 80, 220))
        cx, cy = px + rec["pivot_px"][0] * 2, py + rec["pivot_px"][1] * 2
        d.line([cx - 5, cy, cx + 5, cy], fill=(230, 30, 30))
        d.line([cx, cy - 5, cx, cy + 5], fill=(230, 30, 30))
        px += im2.width + 8
    path = os.path.join(PREVIEW_DIR, "hero_preview.png")
    out.save(path, optimize=True)
    print(f"  preview {path}")
    return out


if __name__ == "__main__":
    build()
    if "--no-preview" not in sys.argv:
        preview()
