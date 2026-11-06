"""Celestial Garden nature, water and cloud pieces."""
from __future__ import annotations

import math

from pa_core import F32, Layer, MaskCanvas, gblur, grad3, hexc, lambert, lerp, np, pillow_normals, rim_light, \
    smoothstep, wrapd
from pa_core import vnoise_fast as vnoise
from pa_garden_paint import (GREEN, ROCK, SKY, WATER, WHITE_FLOWER, YELLOW, Painter, cavity, green_at, relief)
from pa_shapes import blob_cluster, tendril_pts


def _lay(rgb, a):
    L = Layer(a.shape[1], a.shape[0])
    L.set_straight(np.clip(rgb, 0, 1), np.clip(a, 0, 1))
    return L


def _over(lay, rgb, a):
    lay.blit(rgb, a, 0, 0)


# ================================================================== rock

def rock_fill(seed=3001, N=512):
    """Pale warm-grey layered cliff rock: strata with lit ledges and shaded
    undersides, sparse joints, moss on the ledges. Tiles both ways."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:N, 0:N].astype(F32)
    while True:
        cuts = np.sort(rng.uniform(0, N, 7))
        gaps = np.diff(np.concatenate([cuts, [cuts[0] + N]]))
        if gaps.min() > 42:
            break
    warp = vnoise(N, N, 1, seed + 1, 3, 0.5, True, True, sx=128, sy=64)
    yw = (y + (warp - 0.5) * 26) % N
    rel = (yw - cuts[0]) % N
    starts = (cuts - cuts[0]) % N
    bi = np.searchsorted(starts, rel, side="right") - 1
    ends = np.concatenate([starts[1:], [N]])
    t = (rel - starts[bi]) / (ends[bi] - starts[bi])
    tone = ((np.sin(bi * 12.9898 + seed) * 43758.5453) % 1.0)
    rough = vnoise(N, N, 14, seed + 2, 4, 0.55, True, True)
    br = 0.86 + 0.16 * np.exp(-t / 0.10) - 0.20 * smoothstep(0.72, 1.0, t)
    br = br * relief(rough * 10, 0.5, ambient=0.8, diffuse=0.22, blur=1.0, wrap_x=True, wrap_y=True)
    joint = np.zeros((N, N), F32)
    for b in range(len(cuts)):
        for j in range(int(rng.integers(1, 4))):
            jx = rng.uniform(0, N)
            slant = rng.uniform(-0.25, 0.25)
            d = np.abs(wrapd(x - jx - slant * rel, N))
            joint = np.maximum(joint, np.exp(-(d / 1.3) ** 2) * (bi == b) * smoothstep(0.0, 0.1, t))
    joint = joint * (0.6 + 0.4 * rough)
    base = lerp(ROCK["mid"], ROCK["light"], np.clip(0.35 + 0.35 * tone + 0.3 * (rough - 0.5), 0, 1))
    rgb = base * br[..., None]
    rgb = lerp(rgb, hexc("9aa0aa"), smoothstep(0.75, 1.0, t) * 0.35)
    rgb = lerp(rgb, ROCK["warm"], np.exp(-t / 0.08) * 0.35)
    rgb = lerp(rgb, ROCK["deep"], joint * 0.45)
    mx = vnoise(N, N, 1, seed + 3, 3, 0.5, True, True, sx=40, sy=200)
    mf = vnoise(N, N, 3, seed + 4, 2, 0.5, True, True)
    ledge_moss = np.exp(-t / 0.07) * smoothstep(0.45, 0.7, mx)
    patch = smoothstep(0.72, 0.9, vnoise(N, N, 90, seed + 5, 3, 0.5, True, True)) * 0.45
    moss = np.clip(ledge_moss + patch * smoothstep(0.3, 0.7, mf), 0, 0.85)
    mcol = lerp(green_at(0.42), green_at(0.66), mf) * (0.9 + 0.12 * br)[..., None]
    rgb = lerp(rgb, mcol, moss)
    return _lay(rgb, np.ones((N, N), F32))


# ================================================================== grass lip / island underside

def grass_lip(seed=3101, w=512, h=128, S=51):
    """Lush grass/moss top for cliffs and islands. Walkable line at row S."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    lay = Layer(w, h)
    # soft shadow the cushion throws on the rock below
    sh_m = MaskCanvas(w, h, 2)
    xs = np.linspace(0, w, 129)
    bot = S + 20 + 5 * np.sin(xs / w * 2 * math.pi * 3 + 1) + 3 * np.sin(xs / w * 2 * math.pi * 7)
    sh_m.poly([(a, S) for a in xs] + [(a, b + 8) for a, b in zip(xs[::-1], bot[::-1])])
    sh = gblur(sh_m.get(), 5, wrap_x=True) * smoothstep(S + 6, S + 20, y)
    lay.over(hexc("34403a"), sh * 0.45)
    p = Painter(w, h, 3, wrap_x=True)
    # hanging moss and roots under the lip
    for i in range(46):
        x0 = rng.uniform(0, w)
        y0 = float(np.interp(x0 % w, xs, bot)) - 6
        L = rng.uniform(6, 30) * (1.8 if rng.random() < 0.12 else 1)
        pts = tendril_pts(L, math.pi / 2 + rng.uniform(-0.25, 0.25), rng.uniform(-0.5, 0.5), 2.5, 0, 12) \
            + np.array([x0, y0], F32)
        if rng.random() < 0.3:
            p.blade(pts, 2.0, 0.8, hexc("6e5a44"), hexc("8c7458"))   # root
        else:
            p.blade(pts, rng.uniform(2.5, 4.5), 0.8, green_at(0.32), green_at(0.22))
    # back grass
    for i in range(170):
        x0 = rng.uniform(0, w)
        L = rng.uniform(10, 34)
        sgn = 1 if rng.random() < 0.5 else -1
        pts = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.45, 0.45), sgn * rng.uniform(0, 0.45), 2.6, 0, 12) \
            + np.array([x0, S + 3], F32)
        p.blade(pts, rng.uniform(2.8, 4.6), 0.7, green_at(0.3), green_at(0.55 + 0.25 * rng.random()))
    # moss cushion body (cool shadow low, sunlit top)
    for i in range(140):
        x0 = rng.uniform(0, w)
        yb = float(np.interp(x0 % w, xs, bot))
        r = rng.uniform(6, 12)
        p.circle(x0, yb - r * 0.7, r, green_at(0.2 + 0.1 * rng.random()))
    for i in range(160):
        x0 = rng.uniform(0, w)
        r = rng.uniform(5, 10)
        p.circle(x0, S + rng.uniform(5, 13), r, green_at(0.36 + 0.14 * rng.random()))
    for i in range(170):
        x0 = rng.uniform(0, w)
        r = rng.uniform(3.5, 7.5)
        p.circle(x0, S + rng.uniform(1, 5), r, green_at(0.55 + 0.15 * rng.random()))
    for i in range(110):
        x0 = rng.uniform(0, w)
        p.circle(x0, S + rng.uniform(-0.5, 2), rng.uniform(2, 4), green_at(0.78 + 0.2 * rng.random()))
    # front grass, brighter tips
    for i in range(110):
        x0 = rng.uniform(0, w)
        L = rng.uniform(7, 24)
        sgn = 1 if rng.random() < 0.5 else -1
        pts = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.6, 0.6), sgn * rng.uniform(0, 0.6), 2.6, 0, 12) \
            + np.array([x0, S + 5], F32)
        p.blade(pts, rng.uniform(2.4, 4.0), 0.6, green_at(0.45), green_at(0.8 + 0.2 * rng.random()))
    # sparse lilies and small blossoms
    for i in range(3):
        x0 = (i + rng.uniform(0.2, 0.8)) * w / 3
        st = tendril_pts(rng.uniform(18, 30), -math.pi / 2 + rng.uniform(-0.25, 0.25), 0.1, 2, 0, 10) \
            + np.array([x0, S + 3], F32)
        for side in (-1, 1):
            p.lance_leaf(x0, S + 3, -math.pi / 2 + side * rng.uniform(0.4, 0.8), rng.uniform(16, 24), 4.5,
                         green_at(0.45), green_at(0.7), curl=side * 0.4)
        p.blade(st, 2.0, 1.4, green_at(0.45), green_at(0.65))
        p.lily(st[-1][0], st[-1][1], -math.pi / 2 + rng.uniform(-1.0, 1.0), rng.uniform(10, 13), rng)
    for i in range(14):
        x0 = rng.uniform(0, w)
        p.blossom(x0, S - rng.uniform(2, 14), rng.uniform(2.4, 3.6), WHITE_FLOWER,
                  YELLOW if rng.random() < 0.7 else hexc("f2b640"), 5, rng.uniform(0, 6.28), hexc("e4e6dc"))
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    return lay, {"walkableRow": S}


def island_bottom(seed=3201, w=512, h=160, attach=8):
    """Rocky underside strip (for the flipped/bottom edge): rock body at the top,
    hanging rock lumps, roots and moss strands below. Attach row near the top."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    mc = MaskCanvas(w, h, 3)
    mc.poly([(0, -2), (w, -2), (w, attach + 16), (0, attach + 16)])
    teeth = []
    for i in range(14):
        cx = (i + rng.uniform(0.1, 0.9)) * w / 14
        depth = rng.uniform(24, 100) * (1.3 if i % 5 == 0 else 1.0)
        half = rng.uniform(16, 34)
        teeth.append((cx, depth, half))
        for off in (-w, 0, w):
            t = np.linspace(0, 1, 12)
            left = [(cx + off - half + half * 0.9 * tt ** 1.4 + rng.uniform(-2, 2), attach + 10 + depth * tt)
                    for tt in t]
            right = [(cx + off + half - half * 0.9 * tt ** 1.4 + rng.uniform(-2, 2), attach + 10 + depth * tt)
                     for tt in t[::-1]]
            mc.poly(left + right)
    m = mc.get()
    m = gblur(m, 0.6, wrap_x=True)
    nx, ny, nz = pillow_normals(m, 6, 1.0, True)
    facet = vnoise(w, h, 9, seed + 1, 3, 0.5, True, False)
    shade = 0.62 + 0.25 * lambert(nx, ny, nz, (0.3, -0.4, 0.8)) + 0.1 * facet
    rgb = lerp(ROCK["shade"], ROCK["mid"], np.clip(shade - 0.4, 0, 1))
    rgb = rgb * (1 - 0.25 * smoothstep(attach, h * 0.7, y))[..., None]
    # cool bounce from the cloud sea below along the lower edges
    bounce = rim_light(m, 3, (0.0, 1.0), True)
    rgb = lerp(rgb, hexc("c8dcf0"), bounce * 0.55)
    strata = vnoise(w, h, 1, seed + 2, 2, 0.5, True, False, sx=60, sy=3)
    rgb = rgb * (0.95 + 0.08 * strata)[..., None]
    lay = _lay(rgb, m)
    p = Painter(w, h, 3, wrap_x=True)
    for i in range(30):
        x0 = rng.uniform(0, w)
        col = int(np.argmax(m[:, int(x0) % w][::-1] > 0.5))
        yb = h - 1 - col
        L = rng.uniform(15, 60)
        pts = tendril_pts(L, math.pi / 2 + rng.uniform(-0.2, 0.2), rng.uniform(-0.8, 0.8), 2.6, 0, 20) \
            + np.array([x0, yb - 4], F32)
        if rng.random() < 0.5:
            p.blade(pts, rng.uniform(2.5, 4), 0.7, hexc("7a6650"), hexc("9a8466"))
        else:
            p.blade(pts, rng.uniform(3, 5), 0.8, green_at(0.3), green_at(0.18))
            for k in range(2, 18, 5):
                p.ivy_leaf(pts[k][0], pts[k][1], math.pi / 2 + rng.uniform(-1.2, 1.2), rng.uniform(4, 6),
                           green_at(0.25), green_at(0.4))
    # moss along the attach edge
    for i in range(70):
        x0 = rng.uniform(0, w)
        p.circle(x0, attach + rng.uniform(2, 10), rng.uniform(2, 4.5), green_at(0.25 + 0.25 * rng.random()))
    for i in range(60):
        x0 = rng.uniform(0, w)
        pts = tendril_pts(rng.uniform(5, 14), math.pi / 2 + rng.uniform(-0.3, 0.3), rng.uniform(-0.4, 0.4), 2, 0, 8)             + np.array([x0, attach + 6], F32)
        p.blade(pts, rng.uniform(2.5, 4), 0.7, green_at(0.35), green_at(0.22))
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    return lay, {"attachRow": attach}


# ================================================================== hanging greenery

def _ivy_strand(p, x0, y0, L, rng, sway=1.0, leaf=(5, 9), density=1.0, tone=0.0, blossoms=0):
    n = max(8, int(L / 3))
    t = np.linspace(0, 1, n)
    ph = rng.uniform(0, 6.28)
    xs = x0 + sway * (6 * np.sin(t * 5 + ph) + 3 * np.sin(t * 11 + ph * 2)) * t
    ys = y0 + t * L
    pts = np.stack([xs, ys], 1)
    p.line(pts, 1.4, green_at(0.25 + tone))
    k = 0
    for i in range(1, n, max(1, int(2 / density))):
        side = 1 if k % 2 else -1
        k += 1
        size = rng.uniform(*leaf) * (1 - 0.35 * t[i])
        ang = math.pi / 2 + side * rng.uniform(0.5, 1.4)
        lit = rng.random()
        c = green_at(0.28 + 0.3 * lit + tone)
        cl = green_at(0.5 + 0.4 * lit + tone)
        p.ivy_leaf(xs[i], ys[i], ang, size, c, cl, green_at(0.2 + tone))
    for b in range(blossoms):
        i = int(rng.uniform(0.2, 0.9) * n)
        p.blossom(xs[i] + rng.uniform(-4, 4), ys[i], rng.uniform(2.4, 3.2), WHITE_FLOWER, YELLOW, 5,
                  rng.uniform(0, 6))
    return pts


def ivy_drape(variant=0, w=128, h=256):
    rng = np.random.default_rng(3301 + variant * 17)
    p = Painter(w, h, 3)
    # top clump
    for layer, tone in ((0, -0.08), (1, 0.05)):
        for i in range(60):
            xx = rng.uniform(8, w - 8)
            yy = rng.uniform(0, 22 + 6 * layer)
            lit = rng.random()
            p.ivy_leaf(xx, yy, rng.uniform(0, 6.28), rng.uniform(6, 10), green_at(0.3 + 0.3 * lit + tone),
                       green_at(0.55 + 0.35 * lit + tone), green_at(0.2))
    nstr = [5, 4, 6][variant % 3]
    for back in (True, False):
        for i in range(nstr):
            xx = rng.uniform(14, w - 14)
            L = rng.uniform(0.35, 0.95) * (h - 20) * (0.8 if back else 1.0)
            _ivy_strand(p, xx, 10, L, rng, 1.0, (5, 9), 1.0, -0.1 if back else 0.03,
                        blossoms=(1 if (not back and rng.random() < 0.4) else 0))
    rgb, a = p.get()
    return _lay(rgb, a), (0.5, 1.0)


def ivy_curtain(seed=3401, w=384, h=192):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    for back in (True, False):
        for i in range(16):
            xx = (i + rng.uniform(0, 1)) * w / 16
            L = rng.uniform(0.3, 0.95) * (h - 12)
            _ivy_strand(p, xx, 6, L, rng, 0.8, (5, 9), 1.0, -0.12 if back else 0.03,
                        blossoms=(1 if (not back and rng.random() < 0.3) else 0))
    for i in range(170):
        xx = rng.uniform(0, w)
        yy = rng.uniform(0, 20)
        lit = rng.random()
        p.ivy_leaf(xx, yy, rng.uniform(0, 6.28), rng.uniform(6, 10), green_at(0.3 + 0.3 * lit),
                   green_at(0.55 + 0.35 * lit), green_at(0.2))
    rgb, a = p.get()
    return _lay(rgb, a), (0.5, 1.0)


def vine_hanging(seed=3501, w=51, h=320):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    _ivy_strand(p, w / 2, 0, h - 10, rng, 1.3, (5, 8), 0.8, 0.0, blossoms=2)
    rgb, a = p.get()
    return _lay(rgb, a), (0.5, 1.0)


# ================================================================== ground plants

def lily_cluster(variant=0, w=102, h=77):
    rng = np.random.default_rng(3601 + variant * 31)
    p = Painter(w, h, 3)
    base = (w / 2, h - 2)
    for i in range(9):  # strap leaves
        ang = -math.pi / 2 + rng.uniform(-1.25, 1.25)
        p.lance_leaf(base[0] + rng.uniform(-12, 12), base[1], ang, rng.uniform(22, 40), rng.uniform(4.5, 7),
                     green_at(0.35 + 0.15 * rng.random()), green_at(0.62 + 0.25 * rng.random()),
                     curl=(1 if ang > -math.pi / 2 else -1) * rng.uniform(0.2, 0.6))
    nfl = [3, 2, 4][variant % 3]
    for i in range(nfl):
        bx = base[0] + rng.uniform(-14, 14)
        L = rng.uniform(30, 56)
        st = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.3, 0.3), rng.uniform(-0.2, 0.2), 2, 0, 16) \
            + np.array([bx, base[1]], F32)
        p.blade(st, 2.4, 1.6, green_at(0.4), green_at(0.62))
        ang = -math.pi / 2 + (rng.uniform(0.5, 1.3) * (1 if st[-1][0] > base[0] else -1))
        p.lily(st[-1][0], st[-1][1], ang, rng.uniform(13, 17), rng)
    # a bud
    st = tendril_pts(28, -math.pi / 2 + 0.2, -0.1, 2, 0, 12) + np.array([base[0] + 6, base[1]], F32)
    p.blade(st, 2.0, 1.4, green_at(0.4), green_at(0.6))
    p.lance_leaf(st[-1][0], st[-1][1], -math.pi / 2 + 0.3, 12, 5, hexc("e8eccf"), WHITE_FLOWER)
    rgb, a = p.get()
    return _lay(rgb, a), (0.5, 0.0)


def flower_bush(seed=3701, w=160, h=102):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    cx, cy, rx, ry = w / 2, h * 0.58, w * 0.44, h * 0.5
    for layer in range(4):
        for i in range(160):
            a = rng.uniform(math.pi, 2 * math.pi) if rng.random() < 0.75 else rng.uniform(0, math.pi)
            rr = math.sqrt(rng.uniform(0, 1)) * (1.0 - 0.15 * layer)
            xx = cx + math.cos(a) * rx * rr
            yy = min(h - 4, cy + math.sin(a) * ry * rr * (0.9 if a > math.pi else 0.5))
            light = 0.5 - 0.5 * (yy - (cy - ry)) / (2 * ry) + 0.2 * (xx - cx) / rx + layer * 0.12
            p.lance_leaf(xx, yy, rng.uniform(0, 6.28), rng.uniform(7, 12), rng.uniform(3, 5),
                         green_at(0.25 + 0.5 * light), green_at(0.45 + 0.5 * light))
    for i in range(26):
        a = rng.uniform(math.pi * 1.05, math.pi * 1.95)
        rr = math.sqrt(rng.uniform(0.3, 1.0))
        xx = cx + math.cos(a) * rx * rr * 0.95
        yy = cy + math.sin(a) * ry * rr * 0.9
        p.blossom(xx, yy, rng.uniform(3.2, 4.8), WHITE_FLOWER, YELLOW if rng.random() < 0.75 else hexc("f6b84a"), 5,
                  rng.uniform(0, 6), hexc("e2e4dc"))
    rgb, a = p.get()
    return _lay(rgb, a), (0.5, 0.0)


def fern(seed=3801, w=128, h=128):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    for i in range(7):
        sgn = -1 if i % 2 else 1
        ang = -math.pi / 2 + sgn * (0.2 + 0.18 * i)
        L = rng.uniform(70, 110) * (1 - 0.06 * i)
        turns = sgn * rng.uniform(0.25, 0.5)
        pts = tendril_pts(L, ang, turns, 2.2, 0, 50) + np.array([w / 2 + rng.uniform(-4, 4), h - 2], F32)
        tone = rng.uniform(-0.05, 0.1)
        p.line(pts, 1.6, green_at(0.35 + tone))
        tang = np.gradient(pts, axis=0)
        for k in range(4, 46, 2):
            tt = k / 50
            ln = 15 * math.sin(math.pi * min(1, tt * 1.1)) * (1 - 0.5 * tt) + 2
            ta = math.atan2(tang[k][1], tang[k][0])
            for side in (-1, 1):
                lit = 0.5 + 0.3 * side * -sgn
                p.lance_leaf(pts[k][0], pts[k][1], ta + side * 1.1, ln, ln * 0.32, green_at(0.35 + 0.35 * lit + tone),
                             green_at(0.55 + 0.4 * lit + tone))
    rgb, a = p.get()
    return _lay(rgb, a), (0.5, 0.0)


def grass_tuft_strip(seed=3901, w=512, h=64):
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3, wrap_x=True)
    for layer in range(2):
        for i in range(160 if layer == 0 else 110):
            x0 = rng.uniform(0, w)
            L = rng.uniform(14, 56) * (0.6 + 0.4 * (0.5 + 0.5 * math.sin(x0 / w * 2 * math.pi * 5)))
            sgn = 1 if rng.random() < 0.5 else -1
            pts = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.5, 0.5), sgn * rng.uniform(0, 0.6), 2.6, 0, 12) \
                + np.array([x0, h - 1], F32)
            p.blade(pts, rng.uniform(2.6, 4.6), 0.6, green_at(0.3 + 0.15 * layer),
                    green_at(0.6 + 0.2 * layer + 0.2 * rng.random()))
    for i in range(7):
        p.blossom(rng.uniform(0, w), h - rng.uniform(16, 40), rng.uniform(2.4, 3.4), WHITE_FLOWER, YELLOW, 5,
                  rng.uniform(0, 6))
    rgb, a = p.get()
    return _lay(rgb, a), (0.5, 0.0)


# ================================================================== golden tree

def leafy_mass(w, h, blobs, ramp, seed, light=(0.5, -0.6, 0.6), edge_leaves=True):
    """A canopy made of sphere-shaded leaf clumps (x, y, r), drawn back (top) to
    front (bottom), with small leaves along each clump's edge. Returns (rgb, a, lam)."""
    rng = np.random.default_rng(seed)
    lay = Layer(w, h)
    lam_all = np.zeros((h, w), F32)
    for (bx, by, r) in sorted(blobs, key=lambda b: b[1]):
        pad = int(r * 1.5) + 4
        x0, y0 = int(max(0, bx - pad)), int(max(0, by - pad))
        x1, y1 = int(min(w, bx + pad)), int(min(h, by + pad))
        if x1 <= x0 or y1 <= y0:
            continue
        lw_, lh_ = x1 - x0, y1 - y0
        mc = MaskCanvas(lw_, lh_, 2)
        blob_cluster(mc, bx - x0, by - y0, r * 0.75, 6, int(rng.integers(0, 1 << 30)), 0.8, 0.85)
        m = mc.get()
        nx, ny, nz = pillow_normals(m, r * 0.35, 1.0)
        lam = lambert(nx, ny, nz, light)
        tex = vnoise(lw_, lh_, max(3, r / 9), int(rng.integers(0, 1 << 30)), 3)
        col = ramp(np.clip(lam * 0.9 + (tex - 0.5) * 0.25, 0, 1))
        sub = Layer(lw_, lh_)
        sub.set_straight(col, m)
        if edge_leaves:
            p = Painter(lw_, lh_, 2)
            ys_, xs_ = np.nonzero((m > 0.4) & (m < 0.9))
            if len(ys_):
                for k in rng.choice(len(ys_), min(len(ys_), int(r * 1.2)), replace=False):
                    l = float(lam[ys_[k], xs_[k]])
                    ang = math.atan2(ys_[k] - (by - y0), xs_[k] - (bx - x0)) + rng.uniform(-0.6, 0.6)
                    c = ramp(np.array([np.clip(l * 0.9 + 0.05, 0, 1)]))[0]
                    c2 = ramp(np.array([np.clip(l * 0.9 + 0.2, 0, 1)]))[0]
                    p.lance_leaf(xs_[k], ys_[k], ang, r * rng.uniform(0.16, 0.26), r * rng.uniform(0.06, 0.09), c, c2)
            pr, pa = p.get()
            sub.blit(pr, pa, 0, 0)
        srgb, sa = sub.straight()
        lay.blit(srgb, sa, x0, y0)
        cur = lam_all[y0:y1, x0:x1]
        lam_all[y0:y1, x0:x1] = cur * (1 - sa) + lam * sa
    rgb, a = lay.straight()
    return rgb, a, lam_all


def gold_ramp(t):
    return grad3([(0.0, hexc("94621e")), (0.35, hexc("d29e3c")), (0.68, hexc("f0d07c")), (0.9, hexc("fff0c4")),
                  (1.0, hexc("fffbec"))], t)


def green_ramp(t):
    return green_at(0.12 + 0.86 * np.asarray(t, F32))


def golden_tree(seed=4001, w=1536, h=1280):
    """The landmark: a huge tree with a white-gold glowing crown, rooted on its
    own floating island (grass top, rock underside), so it never hovers."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    lay = Layer(w, h)
    cx = w / 2
    top = 1010
    im = MaskCanvas(w, h, 2)
    xs = np.linspace(cx - 520, cx + 520, 80)
    topline = top + 8 * np.sin((xs - cx) / 90)
    under = []
    for xx in xs[::-1]:
        tt = abs(xx - cx) / 520
        under.append((xx, top + 30 + 240 * (1 - tt ** 1.6) + rng.uniform(-14, 14)))
    im.poly(list(zip(xs, topline)) + under)
    m_isl = im.get()
    nx, ny, nz = pillow_normals(m_isl, 24, 1.0)
    lam = lambert(nx, ny, nz, (0.45, -0.4, 0.75))
    strata = vnoise(w, h, 1, seed + 1, 3, 0.5, sx=160, sy=8)
    rcol = lerp(ROCK["shade"], ROCK["light"], np.clip(lam * 0.85 + strata * 0.25 - 0.1, 0, 1))
    rcol = lerp(rcol, hexc("c8dcf0"), rim_light(m_isl, 6, (0, 1)) * 0.5)
    lay.over(rcol, m_isl)
    tm = MaskCanvas(w, h, 2)
    trunk = tendril_pts(560, -math.pi / 2, 0.06, 1.5, 0, 80, 3.0, seed) + np.array([cx, top + 20], F32)
    tm.taper_line(trunk, 150, 62)
    for side in (-1, 1):
        for k, (L, a0, wd) in enumerate([(300, 0.15, 54), (230, 0.45, 40), (380, 0.02, 34)]):
            rp = tendril_pts(L, (0 if side > 0 else math.pi) + side * a0, side * 0.07, 3.0, 0, 50) \
                + np.array([cx + side * 30, top + 6 + k * 4], F32)
            tm.taper_line(rp, wd, 6)
        for k, (L, a0) in enumerate([(330, 0.9), (260, 0.45)]):
            bp = tendril_pts(L, -math.pi / 2 + side * a0, side * 0.12, 2.0, 0, 40) + trunk[30 + k * 18]
            tm.taper_line(bp, 46 - k * 10, 10)
    m_tr = tm.get()
    nx, ny, nz = pillow_normals(m_tr, 10, 1.0)
    lamt = lambert(nx, ny, nz, (0.5, -0.4, 0.75))
    bark_n = vnoise(w, h, 1, seed + 2, 3, 0.5, sx=10, sy=90)
    bcol = grad3([(0.0, hexc("8a7458")), (0.5, hexc("d8c8a4")), (1.0, hexc("fff4d8"))], lamt * (0.75 + 0.35 * bark_n))
    bcol = lerp(bcol, hexc("e8c060"), smoothstep(0.7, 0.9, bark_n) * 0.5)
    lay.over(bcol, m_tr)
    p = Painter(w, h, 2)
    for i in range(900):
        xx = rng.uniform(cx - 515, cx + 515)
        yy = top + 8 * math.sin((xx - cx) / 90)
        L = rng.uniform(10, 34)
        pts = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.6, 0.6), rng.uniform(-0.5, 0.5), 2.5, 0, 8) \
            + np.array([xx, yy + 8], F32)
        p.blade(pts, rng.uniform(4, 7), 1.0, green_at(0.35), green_at(0.65 + 0.3 * rng.random()))
    for i in range(260):
        xx = rng.uniform(cx - 515, cx + 515)
        yy = top + 8 * math.sin((xx - cx) / 90)
        p.circle(xx, yy + rng.uniform(4, 18), rng.uniform(8, 16), green_at(0.3 + 0.4 * rng.random()))
    for i in range(40):
        xx = rng.uniform(cx - 420, cx + 420)
        tt = abs(xx - cx) / 520
        yy = top + 30 + 240 * (1 - tt ** 1.6) - 20
        pts = tendril_pts(rng.uniform(40, 140), math.pi / 2, rng.uniform(-0.3, 0.3), 2.5, 0, 20) \
            + np.array([xx, yy], F32)
        p.blade(pts, rng.uniform(4, 7), 1.0, hexc("8c7a60"), hexc("a8966e"))
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    blobs = []
    for i in range(34):
        a = rng.uniform(0, 2 * math.pi)
        rr = math.sqrt(rng.uniform(0, 1))
        blobs.append((cx + math.cos(a) * 440 * rr, 380 + math.sin(a) * 250 * rr, rng.uniform(95, 165) * (1.15 - 0.4 * rr)))
    crgb, ca, lamc = leafy_mass(w, h, blobs, gold_ramp, seed + 5, (0.5, -0.65, 0.55))
    lay.blit(crgb, ca, 0, 0)
    glow = gblur(ca, 34) * 0.45 + ca * (0.2 + 0.55 * lamc ** 1.5)
    for i in range(60):
        gx, gy = cx + rng.normal(0, 300), 600 + abs(rng.normal(0, 200))
        r = np.hypot(x - gx, y - gy)
        dot = np.exp(-(r / 3.5) ** 2)
        lay.add_light(hexc("fff2c4"), dot * 0.9)
        glow = glow + dot + np.exp(-(r / 12) ** 2) * 0.3
    g = Layer(w, h)
    g.set_straight(np.ones((h, w, 3), F32) * hexc("fff0c0"), np.clip(glow, 0, 1))
    return lay, g, (0.5, 0.0)


# ================================================================== water

def waterfall_tex(seed=4101, w=256, h=512):
    """Clean, vertically tileable falling-water source for a scrolling shader."""
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    s1 = vnoise(w, h, 1, seed, 4, 0.55, True, True, sx=10, sy=200)
    s2 = vnoise(w, h, 1, seed + 1, 3, 0.5, True, True, sx=4, sy=90)
    s3 = vnoise(w, h, 1, seed + 2, 2, 0.5, True, True, sx=30, sy=512)
    streak = np.clip(0.45 * s1 + 0.3 * s2 + 0.35 * s3, 0, 1)
    u = np.abs(x - (w - 1) / 2) / (w / 2)
    side = smoothstep(1.0, 0.6, u)
    edge_n = vnoise(w, h, 1, seed + 3, 3, 0.5, True, True, sx=20, sy=120)
    side = side * smoothstep(1.0, 0.75, u + 0.15 * (edge_n - 0.5))
    rgb = grad3([(0.0, WATER["deep"]), (0.45, WATER["blue"]), (0.8, WATER["white"]), (1.0, hexc("ffffff"))],
                streak * (0.75 + 0.25 * (1 - u)))
    a = side * (0.55 + 0.45 * streak)
    return _lay(rgb, a)


def soft_puffs(w, h, seed, n, rmin, rmax, spread_y=0.4, shade=True):
    rng = np.random.default_rng(seed)
    mc = MaskCanvas(w, h, 2)
    for i in range(n):
        xx = w / 2 + rng.normal(0, w * 0.2)
        yy = h * 0.62 + rng.normal(0, h * spread_y * 0.3)
        r = rng.uniform(rmin, rmax)
        mc.circle(xx, yy, r)
    m = gblur(mc.get(), rmin * 0.6)
    yy, xx = np.mgrid[0:h, 0:w].astype(F32)
    edge = np.hypot((xx - w / 2) / (w / 2), (yy - h / 2) / (h / 2))
    m = m * (1 - smoothstep(0.75, 1.0, edge))
    nx, ny, nz = pillow_normals(m, rmin * 0.8, 1.0)
    lam = lambert(nx, ny, nz, (0.4, -0.7, 0.6))
    rgb = lerp(hexc("c4d8ee"), hexc("ffffff"), 0.35 + 0.65 * lam) if shade else np.ones((h, w, 3), F32)
    return rgb, m


def waterfall_foam(seed=4201):
    rgb, m = soft_puffs(192, 128, seed, 26, 10, 26)
    return _lay(rgb, m * 0.95), (0.5, 0.5)


def mist_soft(seed=4301, w=256, h=128):
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    r = np.hypot((x - w / 2) / (w * 0.46), (y - h / 2) / (h * 0.44))
    n = vnoise(w, h, 40, seed, 3)
    a = np.clip(1 - r, 0, 1) ** 1.6 * (0.7 + 0.5 * n)
    return _lay(np.ones((h, w, 3), F32), np.clip(a * 0.85, 0, 1)), (0.5, 0.5)


# ================================================================== cloud sea

def cloud_sea(layer=0, seed=4401, w=2048, h=512):
    """Volumetric cloud tops, tileable in x: several rows of billows, each row
    shaded on its own so the valleys between them read as depth.
    layer 0 = back (hazier), 2 = front (brightest)."""
    rng = np.random.default_rng(seed + layer * 13)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    lay = Layer(w, h)
    base_y = [150, 170, 190][layer]
    shadow = [hexc("aac0dc"), hexc("b4c8e2"), hexc("bccee6")][layer]
    lit = [hexc("eef3fa"), hexc("f8fafd"), hexc("ffffff")][layer]
    for r in range(4):
        mc = MaskCanvas(w, h, 2)
        by = base_y + r * 62
        xs = np.arange(0, w + 8, 8, dtype=F32)
        k = max(1, round(w / 2048))
        var = vnoise(len(xs), 4, 1, seed + layer * 7 + r, 3, 0.5, True, False, sx=60 / 8 * 4, sy=4)[1]
        line = by + 18 * np.sin(2 * math.pi * xs / w * (2 + r) * k + rng.uniform(0, 6)) + 46 * (var - 0.5) * (k > 1)
        mc.poly([(a, b + 30) for a, b in zip(xs, line)] + [(w, h + 4), (0, h + 4)])
        n = int([16, 20, 24, 28][r] * w / 2048)
        for i in range(n):
            cx = (i + rng.uniform(0, 1)) * w / n
            yy = float(np.interp(cx, xs, line))
            r0 = rng.uniform(55, 120) * [1.0, 0.92, 0.85, 0.8][r] * [0.85, 1.0, 1.15][layer]
            for off in (-w, 0, w):
                blob_cluster(mc, cx + off, yy + r0 * 0.3, r0, 6, seed + layer * 100 + r * 30 + i, 1.0, 0.6)
        m = gblur(mc.get(), [5, 4, 3.2][layer], wrap_x=True)
        nx, ny, nz = pillow_normals(m, 24, 1.3, True)
        lam = lambert(nx, ny, nz, (0.45, -0.75, 0.5))
        col = lerp(shadow, lit, np.clip(0.1 + 0.95 * lam, 0, 1))
        col = lerp(col, hexc("fff4dc"), rim_light(m, 7, (0.5, -0.85), True) * 0.55)
        col = lerp(col, shadow * 0.95, smoothstep(by + 10, by + 120, y) * 0.55)
        lay.over(col, m)
    rgb, a = lay.straight()
    rgb = lerp(rgb, [hexc("dbe8f6"), hexc("e4eef8"), hexc("ecf3fa")][layer], smoothstep(base_y + 250, h, y) * 0.5)
    if layer == 0:
        rgb = lerp(rgb, SKY["haze"], 0.3)
    return _lay(rgb, a), (0.5, 0.0)
