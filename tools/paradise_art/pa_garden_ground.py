"""Celestial Garden v2 ground and built pieces: natural mossy cliff rock,
noble marble ashlar, wall coping, waterfall spout, foreground loggia frame."""
from __future__ import annotations

import math

from pa_core import F32, Layer, MaskCanvas, gblur, grad3, hexc, lerp, np, smoothstep, wrapd
from pa_core import vnoise_fast as vnoise
from pa_garden_paint import (GOLD, MARBLE, ROCK, WHITE_FLOWER, YELLOW, Painter, cavity, gold_shade, green_at, marble,
                             relief)
from pa_shapes import tendril_pts


def _lay(rgb, a):
    L = Layer(a.shape[1], a.shape[0])
    L.set_straight(np.clip(rgb, 0, 1), np.clip(a, 0, 1))
    return L


def _overlay(rgb, a, prgb, pa):
    out = prgb * pa[..., None] + rgb * (a * (1 - pa))[..., None]
    na = pa + a * (1 - pa)
    return np.where(na[..., None] > 1e-4, out / np.maximum(na[..., None], 1e-4), 0), na


def soften(rgb, a, r=0.6):
    """A touch of painterly softness: blend in a slightly blurred copy."""
    pm = rgb * a[..., None]
    pb = gblur(pm, r)
    ab = gblur(a, r)
    pm = pm * 0.55 + pb * 0.45
    a2 = a * 0.55 + ab * 0.45
    return np.where(a2[..., None] > 1e-4, pm / np.maximum(a2[..., None], 1e-4), 0), a2


# ------------------------------------------------------------------ small plant helpers (wrap aware)

def tuft(p, x, y, rng, size=1.0, flowers=0, up=-math.pi / 2):
    for i in range(int(10 * size) + 4):
        L = rng.uniform(5, 14) * size
        pts = tendril_pts(L, up + rng.uniform(-0.7, 0.7), rng.uniform(-0.5, 0.5), 2.5, 0, 8) + np.array([x, y], F32)
        p.blade(pts, rng.uniform(1.8, 3.0) * size ** 0.5, 0.5, green_at(0.35), green_at(0.6 + 0.35 * rng.random()))
    for i in range(flowers):
        p.blossom(x + rng.uniform(-6, 6) * size, y - rng.uniform(4, 11) * size, rng.uniform(1.8, 2.6), WHITE_FLOWER,
                  YELLOW, 5, rng.uniform(0, 6), hexc("e2e4da"))


def mini_fern(p, x, y, rng, size=1.0):
    for k in range(5):
        ang = -math.pi / 2 + (k - 2) * 0.45 + rng.uniform(-0.15, 0.15)
        L = rng.uniform(14, 22) * size
        pts = tendril_pts(L, ang, (1 if ang > -math.pi / 2 else -1) * 0.25, 2.2, 0, 14) + np.array([x, y], F32)
        p.line(pts, 1.0, green_at(0.35))
        tang = np.gradient(pts, axis=0)
        for j in range(2, 13, 2):
            ta = math.atan2(tang[j][1], tang[j][0])
            ln = (4.5 - j * 0.25) * size
            for side in (-1, 1):
                p.lance_leaf(pts[j][0], pts[j][1], ta + side * 1.1, ln, ln * 0.35, green_at(0.45 + 0.1 * side),
                             green_at(0.7 + 0.1 * side))


def ivy_runner(p, x0, y0, length, rng, heading=0.0, leaf=(4, 7)):
    """A meandering ivy stem with leaves, drawn into a (wrapping) painter."""
    pts = [(x0, y0)]
    ang = heading
    for i in range(int(length / 4)):
        ang += rng.normal(0, 0.25)
        ang = ang * 0.9 + heading * 0.1
        x, y = pts[-1]
        pts.append((x + math.cos(ang) * 4, y + math.sin(ang) * 4))
    pts = np.array(pts, F32)
    p.line(pts, 1.2, green_at(0.25))
    for i in range(1, len(pts), 2):
        lit = rng.random()
        p.ivy_leaf(pts[i][0], pts[i][1], rng.uniform(0, 6.28), rng.uniform(*leaf), green_at(0.3 + 0.3 * lit),
                   green_at(0.55 + 0.35 * lit), green_at(0.22))


# ================================================================== rock

def rock_fill(seed=3011, N=512):
    """Natural mossy cliff rock: irregular blocky fractured faces of varied size.
    Each block is a tilted plane at its own depth, so block borders read as lit
    or shadowed fracture steps (not mortar lines); borders are domain-warped so
    they wander; a few true crevices hold moss, ferns and grass; ivy runners.
    Pale warm grey with cool shadows, no regular bands. Tiles both ways."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:N, 0:N].astype(F32)
    wx = (vnoise(N, N, 70, seed + 11, 3, 0.5, True, True) - 0.5) * 40
    wy = (vnoise(N, N, 70, seed + 12, 3, 0.5, True, True) - 0.5) * 40
    n_big, n_small = 9, 26
    P = np.concatenate([rng.uniform(0, N, (n_big, 2)), rng.uniform(0, N, (n_small, 2))]).astype(F32)
    wts = np.concatenate([rng.uniform(30, 52, n_big), rng.uniform(0, 14, n_small)]).astype(F32)
    dx = wrapd((x + wx)[..., None] - P[:, 0], N)
    dy = wrapd((y + wy)[..., None] - P[:, 1], N) * 0.8
    d = np.hypot(dx, dy) - wts
    order = np.argsort(d, axis=2)
    F1 = np.take_along_axis(d, order[..., :1], 2)[..., 0]
    F2 = np.take_along_axis(d, order[..., 1:2], 2)[..., 0]
    cell, cell2 = order[..., 0], order[..., 1]
    cdx = np.take_along_axis(dx, order[..., :1], 2)[..., 0]
    cdy = np.take_along_axis(dy, order[..., :1], 2)[..., 0]
    del dx, dy, d, order
    n = len(P)
    off = rng.uniform(0, 22, n).astype(F32)
    tilt = np.stack([rng.uniform(-0.45, 0.55, n), rng.uniform(-0.55, 0.3, n)], 1).astype(F32)
    border = F2 - F1
    height = off[cell] - (tilt[cell, 0] * cdx + tilt[cell, 1] * cdy) * 0.35
    rough = vnoise(N, N, 10, seed + 2, 4, 0.55, True, True)
    height = height + rough * 3.5 + vnoise(N, N, 40, seed + 13, 3, 0.5, True, True) * 5
    lo, hi = np.minimum(cell, cell2), np.maximum(cell, cell2)
    deep = (((np.sin(lo * 12.9898 + hi * 78.233) * 43758.5453) % 1.0) > 0.62).astype(F32)
    crev = smoothstep(2.2, 0.3, border) * deep
    height = height - crev * 6
    br = relief(height, 0.55, ambient=0.66, diffuse=0.42, blur=1.4, wrap_x=True, wrap_y=True)
    br = br * cavity(height, 6, 0.28, True, True)
    tone = vnoise(N, N, 200, seed + 3, 2, 0.5, True, True)
    ftone = (np.sin(cell * 7.77 + 1.3) * 43758.5453) % 1.0
    rgb = lerp(ROCK["mid"], ROCK["light"], np.clip(0.38 + 0.32 * tone + 0.18 * (ftone - 0.5), 0, 1)) * br[..., None]
    rgb = lerp(rgb, hexc("8a94a8"), smoothstep(0.88, 0.68, br) * 0.5)
    rgb = lerp(rgb, ROCK["warm"], smoothstep(1.0, 1.08, br) * 0.4)
    rgb = lerp(rgb, hexc("666c7a"), crev * 0.5)
    # moss: in the crevices and as a few soft patches on up-facing blocks
    mf = vnoise(N, N, 3, seed + 4, 2, 0.5, True, True)
    mp = vnoise(N, N, 90, seed + 5, 3, 0.5, True, True)
    up = smoothstep(0.0, 0.3, -tilt[cell, 1])
    moss = np.clip(gblur(crev, 2.0, True, True) * smoothstep(0.45, 0.7, mp) * 1.1 +
                   smoothstep(0.7, 0.88, mp) * up * smoothstep(0.35, 0.7, mf) * 0.55, 0, 0.8)
    mcol = lerp(green_at(0.33), green_at(0.58), mf) * (0.8 + 0.2 * br)[..., None]
    rgb = lerp(rgb, mcol, moss)
    lay = _lay(rgb, np.ones((N, N), F32))
    p = Painter(N, N, 3, True, True)
    cand = np.argwhere(crev[::3, ::3] > 0.6)
    for k_ in rng.choice(len(cand), min(len(cand), 13), replace=False):
        cy, cx = cand[k_] * 3
        if rng.random() < 0.4:
            mini_fern(p, cx, cy, rng, rng.uniform(0.7, 1.0))
        else:
            tuft(p, cx, cy, rng, rng.uniform(0.6, 1.0), int(rng.integers(0, 3)))
    for k_ in range(3):
        ivy_runner(p, rng.uniform(0, N), rng.uniform(0, N), rng.uniform(140, 260), rng,
                   math.pi / 2 + rng.uniform(-0.6, 0.6))
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    return lay


# ================================================================== marble ashlar

def wall_block(seed=2411, N=512):
    """Noble weathered marble ashlar: blocks 1-3 units long in irregularly
    staggered courses, softened chipped edges, moss and flowers in some joints."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:N, 0:N].astype(F32)
    courses = [72, 56, 80, 64, 72, 56, 48, 64]
    starts = np.concatenate([[0], np.cumsum(courses)[:-1]])
    dx = np.full((N, N), 1e9, F32)
    dy = np.zeros((N, N), F32)
    bid = np.zeros((N, N), F32)
    joints_pts = []
    for c, (y0, ch) in enumerate(zip(starts, courses)):
        rows = (y >= y0) & (y < y0 + ch)
        cuts = []
        xx = rng.uniform(0, N)
        total = 0
        while True:
            L = rng.choice([64, 96, 128, 160, 192], p=[0.12, 0.22, 0.3, 0.2, 0.16])
            if total + L > N - 56:
                break
            cuts.append((xx + total) % N)
            total += L
        cuts.append((xx + total) % N)
        cuts = np.sort(np.array(cuts))
        xr = x[rows]
        dd = np.min(np.abs(wrapd(xr[:, None] - cuts[None, :], N)), axis=1)
        dx[rows] = dd
        dy[rows] = np.minimum(y[rows] - y0, y0 + ch - 1 - y[rows])
        bid[rows] = c * 20 + np.searchsorted(cuts, xr % N)
        for cx in cuts:
            joints_pts.append((cx, y0))
    en = vnoise(N, N, 5, seed + 1, 3, 0.5, True, True)
    dd = np.minimum(dx, dy) - 2.5 * en * smoothstep(14, 2, np.minimum(dx, dy))
    joint = smoothstep(1.6, 0.5, dd)
    bevel = smoothstep(0.5, 8.0, dd)
    tint = (np.sin(bid * 12.9898) * 43758.5453) % 1.0
    height = 4 + 6 * bevel ** 0.7 + (tint - 0.5) * 0.6
    chips = smoothstep(0.8, 0.93, vnoise(N, N, 12, seed + 2, 3, 0.5, True, True)) * smoothstep(14, 3, dd)
    height = height - chips * 3.5
    br = relief(height, 0.6, ambient=0.72, diffuse=0.34, blur=0.8, wrap_x=True, wrap_y=True)
    br = br * cavity(height, 4, 0.3, True, True)
    rgb = marble(N, N, seed, True, True, 0.9, 0.3) * (0.96 + 0.06 * tint)[..., None] * br[..., None]
    # soft weathering: faint grime pooling under the bed joints, warm stains
    stain = vnoise(N, N, 1, seed + 3, 3, 0.5, True, True, sx=6, sy=90)
    rgb = rgb * (1 - 0.05 * smoothstep(0.6, 0.95, stain))[..., None]
    rgb = lerp(rgb, hexc("c4b391"), smoothstep(0.75, 0.95, vnoise(N, N, 60, seed + 6, 3, 0.5, True, True)) * 0.18)
    rgb = lerp(rgb, hexc("b3a68c"), joint * 0.75)
    mn = vnoise(N, N, 70, seed + 4, 3, 0.5, True, True)
    mf = vnoise(N, N, 3, seed + 5, 2, 0.5, True, True)
    moss = joint * smoothstep(0.62, 0.85, mn) + gblur(joint, 2.5, True, True) * smoothstep(0.7, 0.9, mn) * 0.6
    rgb = lerp(rgb, lerp(green_at(0.35), green_at(0.6), mf), np.clip(moss, 0, 0.85))
    lay = _lay(rgb, np.ones((N, N), F32))
    p = Painter(N, N, 3, True, True)
    picks = rng.choice(len(joints_pts), 7, replace=False)
    for k in picks:
        jx, jy = joints_pts[k]
        tuft(p, jx, jy + 2, rng, rng.uniform(0.6, 0.9), int(rng.integers(1, 4)))
    for k in range(2):
        ivy_runner(p, rng.uniform(0, N), rng.uniform(0, N), rng.uniform(160, 240), rng, math.pi / 2 + rng.uniform(-0.4, 0.4))
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    return lay


def wall_cap(seed=2421, w=512, h=96, S=34):
    """Marble coping that tops a terrace wall, grass and flowers spilling over.
    Walkable line at row S."""
    from pa_garden_arch import _grass_lip
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    hr = np.zeros(h, F32)
    gold = np.zeros(h, F32)
    for yy in range(h):
        if yy < S:
            continue
        r = yy - S
        if r < 6:
            hr[yy] = 14 + math.sqrt(max(0, 36 - (6 - r) ** 2)) * 0.6       # rounded nose
        elif r < 18:
            hr[yy] = 17.0                                                      # coping face
        elif r < 22:
            hr[yy] = 12.0                                                      # shadow groove
        elif r < 34:
            t = (r - 22) / 12
            hr[yy] = 12 + 4 * math.sin(math.pi * t) * (1 - t * 0.3)            # cyma moulding
            gold[yy] = 1.0 if r in (27,) else 0
        else:
            hr[yy] = 11.0                                                      # plain band into the wall
    height = np.broadcast_to(hr[:, None], (h, w)).astype(F32).copy()
    # coping stones: vertical joints every 2-3 units, irregular
    cuts = np.cumsum(rng.choice([128, 160, 192], 6))
    cuts = cuts[cuts < w]
    cuts = np.concatenate([[0], cuts]) + rng.uniform(0, 40)
    dj = np.min(np.abs(wrapd(x[..., None] - cuts[None, None, :], w)), axis=2)
    joint = smoothstep(1.4, 0.4, dj) * (y >= S) * (y < S + 22)
    height = height - joint * 3
    a = (y >= S).astype(F32)
    br = relief(height, 0.7, blur=0.7, wrap_x=True) * cavity(height, 3, 0.35, wrap_x=True)
    rgb = marble(w, h, seed, True, False, 0.8, 0.25) * br[..., None]
    rgb = lerp(rgb, gold_shade(br * 1.1 - 0.15), (gold[:, None] * a) * 0.9)
    rgb = lerp(rgb, hexc("b3a68c"), joint * 0.6)
    p = Painter(w, h, 3, wrap_x=True)
    # grass and flowers spilling over the front of the coping
    for i in range(44):
        x0 = rng.uniform(0, w)
        L = rng.uniform(10, 46) * (1.5 if rng.random() < 0.15 else 1.0)
        pts = tendril_pts(L, math.pi / 2 + rng.uniform(-0.35, 0.35), rng.uniform(-0.4, 0.4), 2.5, 0, 14) \
            + np.array([x0, S + 2], F32)
        p.blade(pts, rng.uniform(3, 5), 0.8, green_at(0.55), green_at(0.32))
        if rng.random() < 0.5:
            for j in range(3, 14, 4):
                p.ivy_leaf(pts[j][0], pts[j][1], math.pi / 2 + rng.uniform(-1.2, 1.2), rng.uniform(3.5, 5.5),
                           green_at(0.35), green_at(0.6))
        if rng.random() < 0.35:
            p.blossom(pts[-1][0], pts[-1][1], rng.uniform(2.0, 2.8), WHITE_FLOWER, YELLOW, 5, rng.uniform(0, 6))
    _grass_lip(p, w, S, seed + 7, 1.2, 1.4, True, lilies=1)
    for i in range(6):   # little flower clumps sitting on top
        x0 = rng.uniform(0, w)
        for k in range(8):
            p.lance_leaf(x0 + rng.uniform(-8, 8), S + 2, -math.pi / 2 + rng.uniform(-1.3, 1.3), rng.uniform(6, 11), 3,
                         green_at(0.4), green_at(0.7))
        for k in range(5):
            p.blossom(x0 + rng.uniform(-9, 9), S - rng.uniform(4, 13), rng.uniform(2.2, 3.0), WHITE_FLOWER, YELLOW, 5,
                      rng.uniform(0, 6), hexc("e2e4da"))
    pr, pa = p.get()
    rgb, a = _overlay(rgb, a, pr, pa)
    return _lay(rgb, a), {"walkableRow": S}


# ================================================================== waterfall spout

def waterfall_spout(seed=2431, w=96, h=64):
    """A carved marble channel mouth set into a wall; water runs out over its lip.
    Pivot: left edge middle (where it enters the wall). Lip point documented."""
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    height = np.zeros((h, w), F32)
    a = np.zeros((h, w), F32)
    # wall plaque (back) with a moulded frame
    plaque = ((x < 40) & (y >= 6) & (y < 60)).astype(F32)
    frame = plaque * ((x < 4) | (x > 35) | (y < 10) | (y > 55)).astype(F32)
    height += plaque * 6 + frame * 3
    # the channel: a trough projecting right, its lip rounded and slightly lower at the mouth
    lip_y = 30
    tx0, tx1 = 26, 92
    taper = (x - tx0) / (tx1 - tx0)
    top = lip_y - 6 + 2 * taper
    bot = lip_y + 14 - 4 * taper
    trough = ((x >= tx0) & (x <= tx1) & (y >= top) & (y <= bot)).astype(F32)
    tprof = np.clip((y - top) / np.maximum(bot - top, 1), 0, 1)
    height = np.where(trough > 0, 14 + 6 * np.sin(math.pi * tprof) - 3 * (tprof < 0.25), height)
    # a carved scroll under the trough
    scroll = (np.hypot(x - 46, y - (lip_y + 16)) < 7).astype(F32) * (1 - trough)
    height += scroll * 9
    a = np.clip(plaque + trough + scroll, 0, 1)
    a = gblur(a, 0.5)
    br = relief(height, 0.9, blur=0.6) * cavity(height, 2.5, 0.35)
    rgb = marble(w, h, seed, scale=0.3, veins=0.15) * br[..., None]
    g = np.exp(-((y - (lip_y + 13 - 4 * taper)) / 0.9) ** 2) * trough * (x > 30)
    rgb = lerp(rgb, gold_shade(br), np.clip(g, 0, 1) * 0.8)
    # water in the channel, running to the lip and starting to fall
    water = ((x >= 30) & (x <= tx1 + 3) & (np.abs(y - (top + 3)) < 2.4)).astype(F32)
    fall = ((x > tx1 - 2) & (x < tx1 + 4) & (y > top + 1)).astype(F32) * smoothstep(h, top, y)
    wm = np.clip(gblur(np.maximum(water, fall), 0.7) * 1.4, 0, 1)
    wcol = lerp(hexc("b8dcf2"), hexc("ffffff"), 0.5 + 0.5 * np.sin(x * 0.6) * 0.5)
    rgb, a = _overlay(rgb, a, wcol, wm * 0.9)
    # moss in the frame
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    for i in range(14):
        p.circle(rng.uniform(0, 38), rng.uniform(52, 60), rng.uniform(1.5, 3.2), green_at(0.4 + 0.3 * rng.random()))
    tuft(p, 8, 8, rng, 0.5, 1)
    pr, pa = p.get()
    rgb, a = _overlay(rgb, a, pr, pa)
    return _lay(rgb, a), {"lipPoint": [float(tx1), float(lip_y - 6 + 2)], "pivotNote": "pivot = left edge middle"}


# ================================================================== loggia frame (foreground)

def loggia_frame(kit, seed=2441):
    """Foreground framing piece for the screen's left edge: a column on its base
    and capital carrying a big arch that runs out of the left of the frame,
    entablature on top, wisteria and ivy hanging. ~6 x 11 units, base pivot."""
    from pa_garden_arch import arch_span, column_base, column_capital, column_shaft
    w, h = 384, 704
    arch, meta = arch_span(6, seed)
    argb, aa = arch.straight()
    x0 = 102
    argb, aa = argb[:, x0:x0 + w], aa[:, x0:x0 + w]
    lay = Layer(w, h)
    col_cx = meta["rightColumnCentreX"] - x0      # 333
    spring = aa.shape[0]
    base = column_base(seed + 1)
    shaft = column_shaft(seed + 2)
    cap = column_capital(seed + 3)
    srgb, sa = shaft.straight()
    yy = spring + 64
    while yy < h - 64:
        hh = min(256, h - 64 - yy)
        lay.blit(srgb[:hh], sa[:hh], col_cx - 32, yy)
        yy += hh
    brgb, ba = base.straight()
    lay.blit(brgb, ba, col_cx - 51, h - 64)
    crgb, ca = cap.straight()
    lay.blit(crgb, ca, col_cx - 51, spring)
    lay.blit(argb, aa, 0, 0)
    rng = np.random.default_rng(seed)
    p = Painter(w, h, 3)
    for i in range(10):     # ivy pouring off the entablature
        xx = rng.uniform(0, w - 30)
        L = rng.uniform(60, 220)
        pts = np.stack([xx + 5 * np.sin(np.linspace(0, 5, 40) + i), np.linspace(20, 20 + L, 40)], 1)
        p.line(pts, 1.4, green_at(0.25))
        for j in range(1, 40, 2):
            lit = rng.random()
            p.ivy_leaf(pts[j][0], pts[j][1], math.pi / 2 + rng.uniform(-1.4, 1.4), rng.uniform(5, 9) * (1 - j / 80),
                       green_at(0.28 + 0.3 * lit), green_at(0.5 + 0.4 * lit), green_at(0.2))
    for i in range(16):     # wisteria racemes in the arch
        xx = rng.uniform(20, 300)
        L = rng.uniform(40, 110)
        for j in range(int(L / 2.2)):
            t = j / (L / 2.2)
            r = 4.0 * (1 - 0.7 * t) + 0.8
            cx_ = xx + rng.uniform(-r, r) + 3 * math.sin(t * 3 + i)
            cy_ = 60 + j * 2.2
            c = lerp(hexc("fbf8ee"), hexc("f3dfa0"), t * 0.8)
            p.circle(cx_, cy_, r * 0.75, c * (0.86 if rng.random() < 0.4 else 1.0))
    for i in range(3):      # plants round the column foot
        tuft(p, col_cx + rng.uniform(-60, 60), h - 2, rng, 1.6, 3)
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    return lay, {"columnCentreX": float(col_cx), "springingRow": int(spring), "pivotNote": "base pivot (bottom centre)"}
