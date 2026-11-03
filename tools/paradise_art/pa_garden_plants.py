"""Celestial Garden v2 foliage: lusher, painterly clumps with many small white
blossoms; new blossom tree, flowering hedge and wisteria."""
from __future__ import annotations

import math

from pa_core import F32, Layer, MaskCanvas, gblur, grad3, hexc, lerp, np, smoothstep
from pa_garden_ground import soften, tuft
from pa_garden_paint import GOLD, WHITE_FLOWER, YELLOW, Painter, green_at
from pa_shapes import tendril_pts

LIGHT = np.array([0.5, -0.62, 0.6], F32) / np.linalg.norm([0.5, -0.62, 0.6])
PETAL_SHADE = hexc("dfe2da")


def _lay(rgb, a):
    L = Layer(a.shape[1], a.shape[0])
    L.set_straight(np.clip(rgb, 0, 1), np.clip(a, 0, 1))
    return L


def _finish(p, soft=0.55):
    rgb, a = p.get()
    rgb, a = soften(rgb, a, soft)
    return _lay(rgb, a)


def clump(p, cx, cy, rx, ry, n, rng, kind="lance", size=(6, 10), t0=0.12, t1=0.98, blossoms=0, bsize=(1.8, 3.0),
          flat_bottom=None, petal=WHITE_FLOWER, centre=YELLOW):
    """Paint a rounded mass of leaves lit like a sphere from the upper right.
    Leaves near the front/top are drawn last. Optional blossoms on the lit side."""
    leaves = []
    for i in range(n):
        a = rng.uniform(0, 2 * math.pi)
        r = math.sqrt(rng.uniform(0, 1))
        u, v = math.cos(a) * r, math.sin(a) * r
        if flat_bottom is not None and v > flat_bottom:
            v = flat_bottom - rng.uniform(0, 0.1)
        nz = math.sqrt(max(0.0, 1 - u * u - v * v))
        lam = max(0.0, u * LIGHT[0] + v * LIGHT[1] + nz * LIGHT[2])
        leaves.append((nz + rng.uniform(-0.15, 0.15), cx + u * rx, cy + v * ry, lam))
    leaves.sort()
    for nz, x, y, lam in leaves:
        t = t0 + (t1 - t0) * np.clip(lam + rng.uniform(-0.12, 0.12), 0, 1)
        c, cl = green_at(t), green_at(min(1.0, t + 0.16))
        ang = rng.uniform(0, 2 * math.pi)
        s = rng.uniform(*size)
        if kind == "ivy":
            p.ivy_leaf(x, y, ang, s, c, cl, green_at(max(0.0, t - 0.15)))
        else:
            p.lance_leaf(x, y, ang, s, s * 0.42, c, cl)
    for i in range(blossoms):
        a = rng.uniform(math.pi * 1.0, math.pi * 2.0) if rng.random() < 0.8 else rng.uniform(0, math.pi)
        r = math.sqrt(rng.uniform(0.1, 1.0))
        u, v = math.cos(a) * r, math.sin(a) * r * 0.95
        lam = max(0.0, u * LIGHT[0] + v * LIGHT[1] + math.sqrt(max(0, 1 - u * u - v * v)) * LIGHT[2])
        pc = lerp(PETAL_SHADE, petal, 0.35 + 0.65 * lam)
        p.blossom(cx + u * rx, cy + v * ry, rng.uniform(*bsize), pc, centre, 5, rng.uniform(0, 6),
                  lerp(PETAL_SHADE, pc, 0.4))


# ================================================================== ground plants

def flower_bush(seed=3711, w=160, h=102):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    clump(p, 46, 66, 40, 32, 220, rng, size=(6, 10), t0=0.08, t1=0.9, blossoms=20, flat_bottom=0.95)
    clump(p, 116, 64, 40, 34, 220, rng, size=(6, 10), t0=0.08, t1=0.9, blossoms=20, flat_bottom=0.95)
    clump(p, 80, 50, 50, 40, 340, rng, size=(6, 11), t0=0.12, t1=1.0, blossoms=46, flat_bottom=0.95)
    return _finish(p), (0.5, 0.0)


def fern(seed=3811, w=128, h=128):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    fronds = sorted([(rng.uniform(-1.2, 1.2), rng.uniform(70, 112)) for _ in range(11)], key=lambda f: -abs(f[0]))
    for k, (spread, L) in enumerate(fronds):
        sgn = 1 if spread > 0 else -1
        ang = -math.pi / 2 + spread
        pts = tendril_pts(L, ang, sgn * rng.uniform(0.25, 0.45), 2.2, 0, 50) + np.array([w / 2 + rng.uniform(-5, 5),
                                                                                        h - 2], F32)
        depth = 1 - abs(spread) / 1.3
        tone = -0.08 + 0.18 * depth
        p.line(pts, 1.6, green_at(0.3 + tone))
        tang = np.gradient(pts, axis=0)
        for j in range(3, 47, 2):
            tt = j / 50
            ln = 16 * math.sin(math.pi * min(1, tt * 1.05)) * (1 - 0.45 * tt) + 2
            ta = math.atan2(tang[j][1], tang[j][0])
            for side in (-1, 1):
                lit = 0.5 + 0.35 * side * sgn + 0.15 * (1 - tt)
                p.lance_leaf(pts[j][0], pts[j][1], ta + side * 1.05, ln, ln * 0.34,
                             green_at(0.25 + 0.45 * lit + tone), green_at(0.45 + 0.45 * lit + tone))
    return _finish(p, 0.5), (0.5, 0.0)


def lily_cluster(variant=0, w=153, h=116):
    rng = np.random.default_rng(3611 + variant * 31)
    p = Painter(w, h, 3)
    bx, by = w / 2, h - 2
    for i in range(18):  # strap leaves, back to front
        ang = -math.pi / 2 + rng.uniform(-1.3, 1.3)
        lit = rng.random()
        p.lance_leaf(bx + rng.uniform(-22, 22), by, ang, rng.uniform(30, 58), rng.uniform(6, 9),
                     green_at(0.2 + 0.35 * lit), green_at(0.5 + 0.4 * lit),
                     curl=(1 if ang > -math.pi / 2 else -1) * rng.uniform(0.2, 0.7))
    clump(p, bx, by - 8, 40, 10, 60, rng, size=(5, 8), t0=0.15, t1=0.8, blossoms=8, flat_bottom=0.6)
    nfl = [4, 3, 5][variant % 3]
    for i in range(nfl):
        x0 = bx + rng.uniform(-22, 22)
        L = rng.uniform(46, 86)
        st = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.3, 0.3), rng.uniform(-0.2, 0.2), 2, 0, 16) \
            + np.array([x0, by], F32)
        p.blade(st, 3.0, 2.0, green_at(0.35), green_at(0.62))
        ang = -math.pi / 2 + rng.uniform(0.5, 1.3) * (1 if st[-1][0] > bx else -1)
        p.lily(st[-1][0], st[-1][1], ang, rng.uniform(19, 25), rng)
    for i in range(2):  # buds
        x0 = bx + rng.uniform(-15, 15)
        st = tendril_pts(rng.uniform(36, 60), -math.pi / 2 + rng.uniform(-0.25, 0.25), 0, 2, 0, 12) \
            + np.array([x0, by], F32)
        p.blade(st, 2.6, 1.8, green_at(0.35), green_at(0.6))
        p.lance_leaf(st[-1][0], st[-1][1], -math.pi / 2 + rng.uniform(-0.3, 0.3), 16, 7, hexc("e4ead0"), WHITE_FLOWER)
    return _finish(p, 0.45), (0.5, 0.0)


# ================================================================== hanging greenery

def _strand(p, x0, y0, L, rng, sway=1.0, leaf=(6, 10), tone=0.0, blossoms=0, cluster=2):
    n = max(8, int(L / 3.2))
    t = np.linspace(0, 1, n)
    ph = rng.uniform(0, 6.28)
    xs = x0 + sway * (6 * np.sin(t * 5 + ph) + 3 * np.sin(t * 11 + ph * 2)) * t
    ys = y0 + t * L
    p.line(np.stack([xs, ys], 1), 1.5, green_at(0.22 + tone))
    for i in range(1, n, 1):
        for c in range(cluster if i % 2 == 0 else 1):
            size = rng.uniform(*leaf) * (1 - 0.4 * t[i])
            side = rng.choice([-1, 1])
            ang = math.pi / 2 + side * rng.uniform(0.4, 1.6)
            lit = np.clip(rng.random() * 0.7 + 0.3 * (side > 0), 0, 1)
            p.ivy_leaf(xs[i] + rng.uniform(-2, 2), ys[i], ang, size, green_at(0.24 + 0.34 * lit + tone),
                       green_at(0.46 + 0.42 * lit + tone), green_at(0.18 + tone))
    for b in range(blossoms):
        i = int(rng.uniform(0.1, 0.95) * n)
        p.blossom(xs[i] + rng.uniform(-5, 5), ys[i] + rng.uniform(-3, 3), rng.uniform(2.0, 2.9),
                  WHITE_FLOWER, YELLOW, 5, rng.uniform(0, 6), PETAL_SHADE)


def ivy_drape(variant=0, w=128, h=256):
    rng = np.random.default_rng(3311 + variant * 17)
    p = Painter(w, h, 3)
    nstr = [6, 5, 7][variant % 3]
    for back in (True, False):
        for i in range(nstr):
            xx = rng.uniform(12, w - 12)
            L = rng.uniform(0.35, 0.95) * (h - 16) * (0.8 if back else 1.0)
            _strand(p, xx, 14, L, rng, 1.0, (6, 10), -0.1 if back else 0.03, blossoms=0 if back else 3)
    clump(p, w / 2, 14, w * 0.48, 16, 150, rng, kind="ivy", size=(7, 11), t0=0.15, t1=0.95, blossoms=10)
    return _finish(p), (0.5, 1.0)


def ivy_curtain(seed=3411, w=384, h=192):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    for back in (True, False):
        for i in range(18):
            xx = (i + rng.uniform(0, 1)) * w / 18
            L = rng.uniform(0.3, 0.95) * (h - 14)
            _strand(p, xx, 10, L, rng, 0.8, (6, 10), -0.12 if back else 0.03, blossoms=0 if back else 2)
    for k in range(4):
        clump(p, (k + 0.5) * w / 4, 10, w / 6.5, 14, 130, rng, kind="ivy", size=(7, 11), t0=0.15, t1=0.95,
              blossoms=8)
    return _finish(p), (0.5, 1.0)


def vine_hanging(seed=3511, w=51, h=320):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    _strand(p, w / 2 - 4, 0, h - 12, rng, 1.2, (5, 8), -0.05, blossoms=0, cluster=1)
    _strand(p, w / 2 + 3, 0, h - 20, rng, 1.4, (6, 9), 0.03, blossoms=7, cluster=2)
    return _finish(p, 0.45), (0.5, 1.0)


# ================================================================== new plants

def blossom_tree(seed=3911, w=512, h=640):
    """White-blossom fruit tree with a few small golden fruit (8 x 10 units)."""
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 2)
    cx = w / 2
    # trunk and branches (bark: pale silver-brown, lit on the right)
    trunk = tendril_pts(300, -math.pi / 2 + 0.05, -0.06, 1.6, 0, 50, 2.0, seed) + np.array([cx - 10, h - 6], F32)
    p.blade(trunk, 46, 20, hexc("6e5c4c"), hexc("8a7662"))
    p.blade(trunk + np.array([7, 0], F32), 22, 8, hexc("a89478"), hexc("c4b296"))
    tips = []
    for k, (ti, ang, L, wd) in enumerate([(18, -2.3, 170, 18), (24, -0.9, 190, 18), (32, -1.9, 140, 13),
                                          (36, -1.1, 150, 13), (44, -1.6, 110, 10), (28, -2.8, 120, 10),
                                          (30, -0.3, 130, 10)]):
        sgn = 1 if ang > -math.pi / 2 else -1
        bp = tendril_pts(L, ang, sgn * 0.08, 2, 0, 24, 1.5, seed + k) + trunk[ti]
        p.blade(bp, wd, 3, hexc("6e5c4c"), hexc("8a7662"))
        p.blade(bp + np.array([wd * 0.25, 0], F32), wd * 0.45, 1.5, hexc("a89478"), hexc("c0ae92"))
        tips.append(bp[-1])
    # roots and grass at the foot
    for side in (-1, 1):
        rp = tendril_pts(46, math.pi / 2 - side * 1.3, side * 0.05, 2, 0, 12) + np.array([cx - 10, h - 14], F32)
        p.blade(rp, 16, 3, hexc("6e5c4c"), hexc("8a7662"))
    for i in range(5):
        tuft(p, cx + rng.uniform(-70, 70), h - 3, rng, 1.4, 2)
    # crown: blossom clumps with leaves underneath
    blobs = [(cx, 190, 150, 120)]
    for t_ in tips:
        blobs.append((t_[0], t_[1], rng.uniform(70, 100), rng.uniform(55, 75)))
    for i in range(5):
        a = rng.uniform(math.pi, 2 * math.pi)
        blobs.append((cx + math.cos(a) * 140, 190 + math.sin(a) * 90, rng.uniform(70, 95), rng.uniform(50, 70)))
    blobs.sort(key=lambda b: b[1])
    for (bx, by, rx, ry) in blobs:
        clump(p, bx, by, rx, ry, int(rx * 2.2), rng, size=(8, 13), t0=0.1, t1=0.8)
        # dense white blossom over the leaves
        for i in range(int(rx * 3.2)):
            a = rng.uniform(0, 2 * math.pi)
            r = math.sqrt(rng.uniform(0, 1))
            u, v = math.cos(a) * r, math.sin(a) * r
            if v > 0.55 and rng.random() < 0.6:
                continue
            nz = math.sqrt(max(0, 1 - u * u - v * v))
            lam = max(0.0, u * LIGHT[0] + v * LIGHT[1] + nz * LIGHT[2])
            pc = lerp(hexc("cfd2d8"), hexc("fffdf6"), 0.25 + 0.75 * lam)
            pc = lerp(pc, hexc("ffe6ea"), 0.15 * rng.random())
            p.blossom(bx + u * rx * 0.95, by + v * ry * 0.95, rng.uniform(3.2, 5.2), pc,
                      lerp(YELLOW, hexc("f0c060"), rng.random()), 5, rng.uniform(0, 6), lerp(pc, hexc("c8ccd2"), 0.4))
    # a few golden fruit hanging under the crown
    for i in range(9):
        bx, by, rx, ry = blobs[int(rng.integers(0, len(blobs)))]
        fx, fy = bx + rng.uniform(-0.6, 0.6) * rx, by + ry * rng.uniform(0.45, 0.85)
        p.line([(fx, fy - 8), (fx, fy - 2)], 1.2, hexc("6e5c4c"))
        p.circle(fx, fy + 3, 6.5, GOLD["dark"])
        p.circle(fx + 0.8, fy + 2.2, 5.6, GOLD["mid"])
        p.circle(fx + 2.0, fy + 0.8, 2.6, GOLD["lit"])
        p.circle(fx + 2.6, fy + 0.2, 1.0, GOLD["hi"])
    return _finish(p, 0.6), (0.5, 0.0)


def hedge_flowers(seed=4011, w=512, h=128):
    """Low flowering hedge, tileable in x, base pivot."""
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3, wrap_x=True)
    for k in range(9):
        cx = (k + rng.uniform(0.2, 0.8)) * w / 9
        clump(p, cx, h - 34, rng.uniform(42, 56), rng.uniform(28, 36), 260, rng, size=(6, 10), t0=0.06, t1=0.9,
              blossoms=40, flat_bottom=0.95)
    for k in range(9):
        cx = (k + rng.uniform(0.0, 1.0)) * w / 9
        clump(p, cx, h - 26, rng.uniform(36, 48), rng.uniform(20, 26), 170, rng, size=(6, 9), t0=0.12, t1=0.98,
              blossoms=30, flat_bottom=1.0)
    for i in range(30):
        tuft(p, rng.uniform(0, w), h - 2, rng, 0.9, 0)
    return _finish(p, 0.5), (0.5, 0.0)


def wisteria_drape(seed=4111, w=256, h=256):
    """White / pale-gold flowering vine curtain to hang from arches (top pivot)."""
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    for i in range(10):    # woody vines along the top
        pts = np.stack([np.linspace(-10, w + 10, 30), 8 + 6 * np.sin(np.linspace(0, 6, 30) + i) + i * 0.6], 1)
        p.line(pts, 2.0, hexc("7a6650"))
    racemes = sorted([(rng.uniform(8, w - 8), rng.uniform(60, h - 20), rng.random()) for _ in range(34)],
                     key=lambda r: r[2])
    for (x0, L, depth) in racemes:
        n = int(L / 1.6)
        sway = rng.uniform(-6, 6)
        rtop = rng.uniform(6.5, 9.5)
        for j in range(n):
            t = j / n
            r = rtop * (1 - t) ** 0.8 + 1.2
            for k in range(3):
                cx_ = x0 + sway * t * t + rng.uniform(-r, r)
                cy_ = 10 + j * 1.6 + rng.uniform(-1.2, 1.2)
                lit = np.clip(0.5 + (cx_ - x0) / (2 * r) * 0.6 + rng.uniform(-0.2, 0.2), 0, 1)
                c = lerp(lerp(hexc("cfd0d6"), hexc("fffcf2"), lit), hexc("f2dc9c"), t * 0.75)
                c = c * (0.8 + 0.2 * depth)
                p.circle(cx_, cy_, r * rng.uniform(0.45, 0.65), c)
    for i in range(70):    # pinnate leaves between the racemes
        x0 = rng.uniform(0, w)
        y0 = rng.uniform(6, 60)
        ang = math.pi / 2 + rng.uniform(-0.9, 0.9)
        for k in range(4):
            xx = x0 + math.cos(ang) * k * 6
            yy = y0 + math.sin(ang) * k * 6
            for side in (-1, 1):
                lit = rng.random()
                p.lance_leaf(xx, yy, ang + side * 0.9, 9, 3.6, green_at(0.3 + 0.35 * lit), green_at(0.55 + 0.4 * lit))
    return _finish(p, 0.5), (0.5, 1.0)
