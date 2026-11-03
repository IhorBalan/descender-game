"""Celestial Garden architecture: white-gold marble columns, arches, decks,
balustrades, ashlar walls, banners, lanterns and ruins. Painted with soft
form and occlusion only (Unity 2D lights add the directional light)."""
from __future__ import annotations

import math

from pa_core import F32, Layer, MaskCanvas, gblur, hexc, lerp, np, smoothstep, wrapd
from pa_core import vnoise_fast as vnoise
from pa_garden_paint import (GOLD, GREEN, MARBLE, WHITE_FLOWER, YELLOW, Painter, cavity, gold_shade, green_at,
                             jitter, lathe_height, marble, relief)
from pa_shapes import tendril_pts

COL_W = 64           # shaft width (1 unit)
CAP_W = 102          # capital / base width (1.6 units)


def _lay(rgb, a):
    L = Layer(a.shape[1], a.shape[0])
    L.set_straight(np.clip(rgb, 0, 1), np.clip(a, 0, 1))
    return L


def _streaks(w, h, seed, wrap_x=False, wrap_y=False, amount=0.05):
    n = vnoise(w, h, 1, seed, 3, 0.5, wrap_x, wrap_y, sx=5, sy=max(60, h * 0.6))
    return 1 - amount * smoothstep(0.55, 0.95, n)


# ================================================================== columns

def column_shaft(seed=2001):
    w, h = COL_W, 256
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    cx, r = (w - 1) / 2, 27.0
    u = np.clip((x - cx) / r, -1, 1)
    c = np.sqrt(1 - u * u)
    ang = np.arcsin(u)
    groove = 0.5 + 0.5 * np.cos(ang * 14)
    height = r * c - 2.4 * groove * c
    a = np.clip(r + 0.5 - np.abs(x - cx), 0, 1)
    br = relief(height, 1.0, blur=0.6, wrap_y=True) * (1 - 0.18 * groove * c)
    tex = marble(w, h, seed, False, True, 0.5, 0.25)
    rgb = tex * br[..., None] * _streaks(w, h, seed + 5, False, True)[..., None]
    # a little edge darkening for roundness
    rgb = rgb * (0.86 + 0.14 * c ** 0.5)[..., None]
    return _lay(rgb, a)


def _base_profile(h=64):
    hw = np.zeros(h, F32)
    sq = np.zeros(h, F32)
    gold = np.zeros(h, F32)
    for yy in range(h):
        if yy < 4:
            hw[yy] = 28.5
            gold[yy] = 1.0 if 1 <= yy <= 3 else 0
        elif yy < 15:
            hw[yy] = 30 + 6 * math.sin(math.pi * (yy - 4) / 11)
        elif yy < 23:
            hw[yy] = 31 - 3 * math.sin(math.pi * (yy - 15) / 8)
        elif yy < 37:
            hw[yy] = 36 + 8.5 * math.sin(math.pi * (yy - 23) / 14)
        elif yy < 40:
            hw[yy] = 47 + (yy - 37)
            sq[yy] = 1
        else:
            hw[yy] = 50.5
            sq[yy] = 1
    return hw, sq, gold


def column_base(seed=2011):
    w, h = CAP_W, 64
    hw, sq, gold = _base_profile(h)
    height, a = lathe_height(w, h, hw, square=sq)
    height = height + np.where(sq[:, None] > 0, 0, 0)
    br = relief(height, 0.9, blur=0.7) * cavity(height, 3, 0.25)
    tex = marble(w, h, seed, scale=0.4, veins=0.2)
    rgb = tex * br[..., None]
    g = gold[:, None] * a
    rgb = lerp(rgb, gold_shade(br * 1.15 - 0.2), g)
    # grime and a hint of moss creeping on the plinth foot
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    moss = smoothstep(52, 63, y) * smoothstep(0.5, 0.8, vnoise(w, h, 10, seed + 3, 3))
    rgb = lerp(rgb, green_at(0.45), moss * 0.6)
    return _lay(rgb, a)


def column_capital(seed=2021):
    """Corinthian-ish capital: abacus on top, a bell of curled acanthus leaves,
    volutes at the corners, astragal ring at the bottom (fits the 64 px shaft)."""
    w, h = CAP_W, 64
    cx = (w - 1) / 2
    hw = np.zeros(h, F32)
    sq = np.zeros(h, F32)
    for yy in range(h):
        if yy < 11:
            hw[yy] = 50.5
            sq[yy] = 1
        elif yy < 57:
            t = (yy - 11) / 46
            hw[yy] = 29 + 16 * (1 - t) ** 1.6
        else:
            hw[yy] = 29.5 + 2.5 * math.sin(math.pi * (yy - 57) / 7)
    height, a = lathe_height(w, h, hw, square=sq)
    height = height * 0.5
    # acanthus leaves and volutes in relief
    lm = MaskCanvas(w, h, 4)
    for row, (yb, n, L, wid) in enumerate([(60, 5, 30, 9.5), (52, 4, 34, 9.0)]):
        for k in range(n):
            xb = cx + (k - (n - 1) / 2) * (56 / max(1, n - 1)) * (1.0 if row == 0 else 0.9)
            sgn = 1 if xb > cx else -1
            p = tendril_pts(L, -math.pi / 2 + sgn * 0.12, sgn * 0.55, 3.0, 0, 30) + np.array([xb, yb], F32)
            lm.taper_line(p, wid, 2.0)
    m_leaf = gblur(lm.get(), 0.8)
    vm = MaskCanvas(w, h, 4)
    for sgn in (-1, 1):
        p = tendril_pts(26, -math.pi / 2 + sgn * 0.6, sgn * 1.6, 2.2, 0, 60) + np.array([cx + sgn * 20, 30], F32)
        vm.taper_line(p, 5.0, 3.0)
        vm.circle(p[-1][0], p[-1][1], 3.2)
    m_vol = gblur(vm.get(), 0.6)
    a = np.clip(np.maximum(a, np.maximum(m_leaf, m_vol) * (np.mgrid[0:h, 0:w][0] > 10)), 0, 1)
    height = height + m_leaf * 7 + m_vol * 9
    br = relief(height, 1.0, blur=0.6) * cavity(height, 2.5, 0.45)
    tex = marble(w, h, seed, scale=0.4, veins=0.15)
    rgb = tex * br[..., None]
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    gold = ((y >= 8) & (y <= 10)).astype(F32) * a
    # gold eyes of the volutes and the leaf tips
    for sgn in (-1, 1):
        r = np.hypot(x - (cx + sgn * 30), y - 22)
        gold = np.maximum(gold, smoothstep(3.5, 2.0, r))
    rgb = lerp(rgb, gold_shade(br * 1.1 - 0.15), gold * a)
    return _lay(rgb, a)


# ================================================================== arches and decks

def _grass_lip(p, w, top, seed, density=1.0, flowers=0.5, wrap=False, lilies=0):
    """Paint a mossy, grassy lip along row `top` (walkable line) into Painter p."""
    rng = np.random.default_rng(seed)
    n = int(w * 0.32 * density)
    for i in range(n):  # back blades
        x = rng.uniform(0, w)
        L = rng.uniform(5, 15)
        sgn = 1 if rng.random() < 0.5 else -1
        pts = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.5, 0.5), sgn * rng.uniform(0, 0.5), 2.5, 0, 10) \
            + np.array([x, top + 2], F32)
        p.blade(pts, rng.uniform(2.0, 3.2), 0.6, green_at(0.35), green_at(0.62 + 0.15 * rng.random()))
    # moss cushion over the edge
    for i in range(int(w / 5)):
        x = rng.uniform(0, w)
        r = rng.uniform(3.0, 6.5)
        p.circle(x, top + rng.uniform(1.5, 4.5), r, green_at(0.38 + 0.12 * rng.random()))
    for i in range(int(w / 6)):
        x = rng.uniform(0, w)
        r = rng.uniform(2.5, 5.0)
        p.circle(x, top + rng.uniform(-0.5, 1.5), r, green_at(0.62 + 0.15 * rng.random()))
    for i in range(int(w / 10)):  # sunlit top highlights
        x = rng.uniform(0, w)
        p.circle(x, top - 0.5 + rng.uniform(-1, 1), rng.uniform(1.5, 3.0), green_at(0.85 + 0.12 * rng.random()))
    for i in range(int(w * 0.18 * density)):  # front blades
        x = rng.uniform(0, w)
        L = rng.uniform(4, 11)
        sgn = 1 if rng.random() < 0.5 else -1
        pts = tendril_pts(L, -math.pi / 2 + rng.uniform(-0.6, 0.6), sgn * rng.uniform(0, 0.7), 2.5, 0, 10) \
            + np.array([x, top + 4], F32)
        p.blade(pts, rng.uniform(1.8, 2.8), 0.5, green_at(0.5), green_at(0.8 + 0.2 * rng.random()))
    for i in range(int(w / 40 * flowers)):
        x = rng.uniform(0, w)
        p.blossom(x, top - rng.uniform(1, 6), rng.uniform(2.2, 3.4), WHITE_FLOWER,
                  YELLOW if rng.random() < 0.7 else hexc("f6c24a"), 5, rng.uniform(0, 6.28), hexc("e6e6dc"))
    for i in range(lilies):
        x = rng.uniform(10, w - 10)
        st = tendril_pts(rng.uniform(10, 16), -math.pi / 2 + rng.uniform(-0.3, 0.3), 0.1, 2, 0, 8) + np.array(
            [x, top + 3], F32)
        p.blade(st, 1.6, 1.2, green_at(0.4), green_at(0.6))
        p.lily(st[-1][0], st[-1][1], -math.pi / 2 + rng.uniform(-0.9, 0.9), rng.uniform(8, 11), rng)


def _overlay(rgb, a, prgb, pa):
    out = prgb * pa[..., None] + rgb * (a * (1 - pa))[..., None]
    na = pa + a * (1 - pa)
    return np.where(na[..., None] > 1e-4, out / np.maximum(na[..., None], 1e-4), 0), na


def _entablature_rows(h, top, dentils=True):
    """Height profile (per row) and a gold mask for a cornice/frieze/architrave stack
    starting at `top`. Returns (height_rows, gold_rows, bottom_row)."""
    hr = np.zeros(h, F32)
    gr = np.zeros(h, F32)
    yy = top
    seq = [  # (rows, height fn of t)
        (4, lambda t: 16.0),                       # crown fillet
        (6, lambda t: 16 - 4 * t * t),             # cyma
        (3, lambda t: 12.5),                       # fillet
        (5 if dentils else 0, None),               # dentil band (filled later)
        (2, lambda t: 11.0),
        (16, lambda t: 9.5),                       # frieze
        (5, lambda t: 10.5),                       # architrave fascia 1
        (6, lambda t: 11.5),                       # fascia 2
    ]
    rows = []
    for n, fn in seq:
        for i in range(n):
            if yy < h:
                hr[yy] = fn(i / max(1, n - 1)) if fn else -1  # -1 marks dentil rows
                rows.append(yy)
            yy += 1
    frieze0 = top + 4 + 6 + 3 + (5 if dentils else 0) + 2
    for g in (frieze0 + 7, frieze0 + 8):
        if g < h:
            gr[g] = 1
    return hr, gr, yy


def _apply_entablature(height, gold, hr, gr, top, bottom, w, wrap=False, dentil_step=8):
    x = np.arange(w)
    for yy in range(top, min(bottom, height.shape[0])):
        if hr[yy] < 0:
            den = ((x % dentil_step) < dentil_step * 0.6).astype(F32)
            height[yy] = 11.0 + 3.0 * den
        else:
            height[yy] = hr[yy]
        gold[yy] = np.maximum(gold[yy], gr[yy])


def arch_span(spacing_units=6, seed=2101):
    """A round arch between two column centres `spacing_units` apart, with
    spandrels and an entablature whose flat top is the walkable line."""
    sp = spacing_units * 64
    W = sp + CAP_W
    xl, xr = CAP_W / 2, W - CAP_W / 2
    cx = W / 2
    r_in = (xr - xl - CAP_W) / 2
    TOP = 16
    ent_h = 52
    H = 256 if spacing_units <= 6 else 384
    t_ring = H - (TOP + ent_h + 4) - r_in
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    d = np.hypot(x - cx, H - y)
    theta = np.arctan2(H - y, x - cx)  # 0..pi over the arch
    opening = smoothstep(r_in + 0.5, r_in - 0.5, d)
    a = (1 - opening) * (y >= TOP)
    height = np.full((H, W), 6.0, F32)
    gold = np.zeros((H, W), F32)
    # voussoir ring
    ring = smoothstep(r_in - 0.5, r_in + 1.5, d) * smoothstep(r_in + t_ring + 1, r_in + t_ring - 1, d)
    nv = 17
    seg = (theta / (math.pi / nv)) % 1.0
    joint = np.exp(-((np.minimum(seg, 1 - seg)) / 0.035) ** 2) * ring
    bevel = smoothstep(r_in, r_in + 3, d) * smoothstep(r_in + t_ring, r_in + t_ring - 3, d)
    height = height + ring * (5 + 2 * bevel) - joint * 3
    # inner archivolt fascia and an outer moulding line
    height = height + smoothstep(r_in + 6, r_in + 4, d) * ring * -1.5
    outer = np.exp(-((d - (r_in + t_ring + 2.5)) / 1.6) ** 2) * (y < H - 2)
    height = height + outer * 2.5
    # keystone
    kw = (math.pi / nv) * 0.6
    key = smoothstep(kw + 0.01, kw - 0.01, np.abs(theta - math.pi / 2)) * \
        smoothstep(r_in - 0.5, r_in + 1, d) * smoothstep(r_in + t_ring + 14, r_in + t_ring + 12, d)
    height = np.maximum(height, key * 15)
    kr = np.hypot(x - cx, y - (H - r_in - t_ring * 0.55))
    gold = np.maximum(gold, smoothstep(5.0, 3.5, kr) * key)
    # entablature
    hr, gr, bottom = _entablature_rows(H, TOP)
    _apply_entablature(height, gold, hr, gr, TOP, bottom, W)
    # piers above the capitals: slight pilaster bands
    for px in (xl, xr):
        pil = smoothstep(CAP_W / 2 - 4, CAP_W / 2 - 6, np.abs(x - px)) * (y >= bottom) * (1 - ring)
        height = height + pil * 1.5
    br = relief(height, 0.7, blur=0.7) * cavity(height, 3.5, 0.35)
    tex = marble(W, H, seed, scale=0.8)
    rgb = tex * br[..., None]
    # soft occlusion under the cornice and inside the arch soffit edge
    under = np.exp(-((y - bottom) / 4.0) ** 2) * (y >= bottom)
    rgb = rgb * (1 - 0.12 * under)[..., None]
    rgb = rgb * _streaks(W, H, seed + 9, amount=0.06)[..., None]
    # moss in the voussoir joints, a little
    mn = vnoise(W, H, 30, seed + 4, 3)
    rgb = lerp(rgb, green_at(0.4), joint * smoothstep(0.55, 0.8, mn) * 0.7)
    rgb = lerp(rgb, gold_shade(br * 1.15 - 0.2), gold * a)
    # grass lip on top
    p = Painter(W, H, 3)
    _grass_lip(p, W, TOP, seed + 20, 1.0, 0.7)
    pr, pa = p.get()
    rgb, a = _overlay(rgb, a, pr, pa)
    meta = {"walkableRow": TOP, "springingRow": H, "leftColumnCentreX": xl, "rightColumnCentreX": xr,
            "columnSpacingPx": sp, "archInnerRadiusPx": float(r_in), "keystoneCentre": [cx, H - r_in - t_ring]}
    return _lay(rgb, a), meta


def deck_top(seed=2201, w=512, h=77):
    """Walkway strip (bridges, aqueduct tops): grass lip, cornice, frieze with gold line."""
    TOP = 18
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    height = np.zeros((h, w), F32)
    gold = np.zeros((h, w), F32)
    hr, gr, bottom = _entablature_rows(h, TOP)
    _apply_entablature(height, gold, hr, gr, TOP, h, w)
    for yy in range(bottom, h):
        height[yy] = 12.0
    a = (y >= TOP).astype(F32)
    br = relief(height, 0.7, blur=0.7, wrap_x=True) * cavity(height, 3.5, 0.35, wrap_x=True)
    rgb = marble(w, h, seed, True, False, 0.8) * br[..., None]
    rgb = rgb * _streaks(w, h, seed + 9, True, False, 0.06)[..., None]
    rgb = lerp(rgb, gold_shade(br * 1.15 - 0.2), gold * a)
    # moss creeping over the cornice in places
    p = Painter(w, h, 3, wrap_x=True)
    rng = np.random.default_rng(seed)
    for i in range(10):
        x0 = rng.uniform(0, w)
        for k in range(int(rng.uniform(3, 8))):
            xx = x0 + rng.uniform(-14, 14)
            L = rng.uniform(4, 18)
            pts = tendril_pts(L, math.pi / 2 + rng.uniform(-0.2, 0.2), rng.uniform(-0.3, 0.3), 2, 0, 8) \
                + np.array([xx, TOP + 4], F32)
            p.blade(pts, rng.uniform(2.5, 4.5), 0.8, green_at(0.42), green_at(0.3))
    _grass_lip(p, w, TOP, seed + 20, 1.0, 0.8, True, lilies=1)
    pr, pa = p.get()
    rgb, a = _overlay(rgb, a, pr, pa)
    return _lay(rgb, a), {"walkableRow": TOP}


def balustrade(seed=2301, w=512, h=77):
    """Terrace-edge balustrade: top rail, vase balusters every 0.5 unit, plinth."""
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    height = np.zeros((h, w), F32)
    a = np.zeros((h, w), F32)
    rail = (y < 13).astype(F32)
    rail_prof = np.where(y < 3, 8 + 2 * y, np.where(y < 9, 14 - 0.3 * (y - 3), 11 - (y - 9) * 0.8))
    plinth = (y >= 64).astype(F32)
    plinth_prof = np.where(y < 67, 10 + (y - 64), 13.0)
    step = 32
    bx = (x % step) - (step - 1) / 2
    t = np.clip((y - 13) / 51, 0, 1)
    # vase: neck at top, belly low
    hwv = 4.5 + 6.0 * np.exp(-((t - 0.68) / 0.2) ** 2) + 2.0 * np.exp(-((t - 0.08) / 0.07) ** 2) \
        + 3.0 * np.exp(-((t - 0.97) / 0.05) ** 2)
    u = np.clip(np.abs(bx) / hwv, 0, 1)
    bal = np.clip(hwv + 0.5 - np.abs(bx), 0, 1) * (y >= 13) * (y < 64)
    height = np.where(bal > 0, hwv * np.sqrt(1 - u * u), height)
    height = np.where(rail > 0, rail_prof, height)
    height = np.where(plinth > 0, plinth_prof, height)
    a = np.clip(rail + plinth + bal, 0, 1)
    br = relief(height, 0.8, blur=0.6, wrap_x=True) * cavity(height, 2.5, 0.3, wrap_x=True)
    rgb = marble(w, h, seed, True, False, 0.6, 0.2) * br[..., None]
    rgb = rgb * (1 - 0.18 * np.exp(-((y - 13) / 3) ** 2) * bal)[..., None]   # shadow under the rail
    gold = ((y >= 10) & (y <= 11)).astype(F32) * rail
    rgb = lerp(rgb, gold_shade(br * 1.1 - 0.15), gold)
    # moss on the rail, ivy wandering along in two places
    p = Painter(w, h, 3, wrap_x=True)
    rng = np.random.default_rng(seed)
    for i in range(int(w / 7)):
        xx = rng.uniform(0, w)
        p.circle(xx, rng.uniform(0, 2.5), rng.uniform(1.5, 3.5), green_at(0.5 + 0.35 * rng.random()))
    for k in range(2):
        x0 = rng.uniform(0, w)
        pts = np.stack([x0 + np.linspace(0, 120, 30), 4 + 6 * np.sin(np.linspace(0, 5, 30)) ** 2 +
                        np.linspace(0, 40, 30) * (np.linspace(0, 1, 30) > 0.6)], 1)
        p.line(pts, 1.6, GREEN["dark"])
        for j in range(0, 30, 2):
            ang = rng.uniform(-math.pi, math.pi)
            tt = rng.random()
            p.ivy_leaf(pts[j][0], pts[j][1], ang, rng.uniform(5, 8), green_at(0.35 + 0.35 * tt),
                       green_at(0.6 + 0.35 * tt))
    pr, pa = p.get()
    rgb, a = _overlay(rgb, a, pr, pa)
    return _lay(rgb, a), {"railTopRow": 0, "footRow": h}


def wall_block(seed=2401, N=512):
    """Weathered marble ashlar, tileable both ways (8x8 units), moss in the joints."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:N, 0:N].astype(F32)
    course = 64
    ci = (y // course).astype(int)
    dy = np.minimum(y % course, course - 1 - (y % course))
    dx = np.full((N, N), 1e9, F32)
    block_id = np.zeros((N, N), F32)
    for c in range(N // course):
        rows = ci == c
        ncut = int(rng.integers(3, 5))
        while True:
            cuts = np.sort(rng.uniform(0, N, ncut))
            gaps = np.diff(np.concatenate([cuts, [cuts[0] + N]]))
            if gaps.min() > 90:
                break
        xr = x[rows]
        dd = np.min(np.abs(wrapd(xr[:, None] - cuts[None, :], N)), axis=1)
        dx[rows] = dd
        idx = np.searchsorted(cuts, xr % N) % ncut
        block_id[rows] = c * 10 + idx
    edge_n = vnoise(N, N, 6, seed + 1, 3, 0.5, True, True)
    dd = np.minimum(dx, dy) - 2.2 * edge_n
    joint = smoothstep(1.8, 0.6, dd)
    bevel = smoothstep(0.8, 7, dd)
    tint = (np.sin(block_id * 12.9898) * 43758.5453) % 1.0
    height = 4 + 5 * bevel + (tint - 0.5) * 0.8
    chips = smoothstep(0.78, 0.92, vnoise(N, N, 14, seed + 2, 3, 0.5, True, True)) * smoothstep(12, 2, dd)
    height = height - chips * 3
    br = relief(height, 0.7, blur=0.7, wrap_x=True, wrap_y=True) * cavity(height, 4, 0.35, True, True)
    rgb = marble(N, N, seed, True, True, 0.7, 0.25) * (0.95 + 0.07 * tint)[..., None] * br[..., None]
    rgb = rgb * 0.95
    # grime: rain streaks running down from the joints
    streak_src = gblur(joint, 1.0, True, True)
    streak = np.zeros_like(streak_src)
    acc = streak_src.copy()
    for k in range(1, 28, 3):
        streak = np.maximum(streak, np.roll(acc, k, axis=0) * (1 - k / 30))
    sn = vnoise(N, N, 1, seed + 3, 3, 0.5, True, True, sx=4, sy=60)
    rgb = rgb * (1 - 0.08 * streak * smoothstep(0.45, 0.85, sn))[..., None]
    jcol = hexc("9a917e")
    rgb = lerp(rgb, jcol, joint * 0.7)
    moss_n = vnoise(N, N, 60, seed + 4, 4, 0.55, True, True)
    moss = smoothstep(0.58, 0.85, moss_n)
    creep = gblur(joint, 3.0, True, True) * moss
    mfine = vnoise(N, N, 3, seed + 5, 2, 0.5, True, True)
    mcol = lerp(green_at(0.32), green_at(0.55), mfine)
    rgb = lerp(rgb, mcol, np.clip(joint * moss * 1.1 + creep * 0.5 * smoothstep(0.4, 0.75, mfine), 0, 0.9))
    return _lay(rgb, np.ones((N, N), F32))


# ================================================================== small pieces

def banner(seed=2501):
    w, h = 77, 224
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    cx = (w - 1) / 2
    lay = Layer(w, h)
    # cloth with a swallowtail
    cw = 30
    notch = 18
    cut = h - 4 - notch * (1 - np.abs(x - cx) / cw)
    cloth = smoothstep(cw + 0.5, cw - 0.5, np.abs(x - cx)) * (y >= 10) * smoothstep(cut + 0.5, cut - 0.5, y)
    folds = np.sin((x - cx) / cw * math.pi * 2.2 + 0.4) * 0.5 + 0.5
    folds = folds * (0.6 + 0.4 * smoothstep(10, h, y))
    shade = 0.80 + 0.22 * folds - 0.08 * smoothstep(h * 0.6, h, y)
    cream = lerp(hexc("efe3c6"), hexc("fbf4e2"), folds)
    rgb = cream * shade[..., None]
    border = cloth * ((np.abs(x - cx) > cw - 5) | (y < 16) | (y > cut - 6)).astype(F32)
    rgb = lerp(rgb, gold_shade(shade * 0.95), border)
    # sun emblem
    ey = 82
    r = np.hypot(x - cx, y - ey)
    ang = np.arctan2(y - ey, x - cx)
    rays = (0.5 + 0.5 * np.cos(ang * 12)) ** 3
    sun = smoothstep(10.5, 9.5, r) + smoothstep(19 * (0.55 + 0.45 * rays) + 0.5, 19 * (0.55 + 0.45 * rays) - 0.5, r) \
        * smoothstep(12, 13.5, r)
    sun = np.clip(sun, 0, 1)
    rgb = lerp(rgb, gold_shade(0.6 + 0.4 * smoothstep(12, 0, r) * shade), sun * cloth)
    ring = np.exp(-((r - 6.5) / 1.0) ** 2) * cloth
    rgb = lerp(rgb, GOLD["dark"], ring * 0.5)
    lay.over(np.clip(rgb, 0, 1), cloth)
    # rod with finials and rings
    rod = smoothstep(2.5, 1.5, np.abs(y - 6)) * smoothstep(w / 2 - 1, w / 2 - 3, np.abs(x - cx))
    fin = np.maximum(smoothstep(4.5, 3.0, np.hypot(x - 3.5, y - 6)), smoothstep(4.5, 3.0, np.hypot(x - (w - 4.5),
                                                                                                    y - 6)))
    metal = np.clip(rod + fin, 0, 1)
    lay.over(gold_shade(0.55 + 0.45 * smoothstep(8, 4, y)), metal)
    for rx in (cx - 22, cx, cx + 22):
        rr = np.hypot(x - rx, y - 9)
        lay.over(gold_shade(0.7), np.exp(-((rr - 2.6) / 0.8) ** 2) * (y > 7))
    return lay, (0.5, 1 - 6 / h)


def lantern(seed=2601):
    w, h = 38, 77
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    cx = (w - 1) / 2
    lay = Layer(w, h)
    glow = np.zeros((h, w), F32)
    # chain
    for k in range(4):
        yy = 2 + k * 3.2
        rr = np.hypot((x - cx) * (1.6 if k % 2 else 0.8), (y - yy) * (0.8 if k % 2 else 1.6))
        lay.over(gold_shade(0.6), np.exp(-((rr - 1.5) / 0.6) ** 2))
    hw = np.zeros(h, F32)
    for yy in range(h):
        if 14 <= yy < 18:
            hw[yy] = 3 + (yy - 14) * 2.5
        elif 18 <= yy < 25:
            hw[yy] = 12 + 2.5 * math.sin(math.pi * (yy - 18) / 7)
        elif 25 <= yy < 56:
            t = (yy - 25) / 31
            hw[yy] = 11.5 + 2.0 * math.sin(math.pi * t)
        elif 56 <= yy < 62:
            hw[yy] = 13 - (yy - 56) * 1.2
        elif 62 <= yy < 72:
            hw[yy] = max(1.0, 5 - (yy - 62) * 0.45)
    height, a = lathe_height(w, h, hw)
    br = relief(height, 1.0, blur=0.5)
    glass = ((y >= 26) & (y < 55)).astype(F32) * a
    bars = np.maximum(smoothstep(1.4, 0.4, np.abs(x - cx)), smoothstep(1.2, 0.3, np.abs(np.abs(x - cx) - 8)))
    bars = bars * glass
    metal = a * (1 - glass) + bars
    inner = np.exp(-((x - cx) / 7) ** 2 - ((y - 41) / 13) ** 2)
    gcol = lerp(hexc("ffbf5a"), hexc("fff6dc"), inner)
    lay.over(gcol, glass)
    lay.over(gold_shade(br * 1.1 - 0.1), np.clip(metal, 0, 1))
    glow = np.clip(glass * (0.5 + 0.5 * inner) * (1 - bars * 0.7) + gblur(glass, 4) * 0.4, 0, 1)
    g = Layer(w, h)
    g.set_straight(np.ones((h, w, 3), F32) * hexc("ffd07a"), glow)
    return lay, g, (0.5, 1.0)


def broken_column(seed=2701):
    w, h = CAP_W, 192
    lay = Layer(w, h)
    base = column_base(seed + 1)
    lay.blit(*base.straight(), 0, h - 64)
    shaft = column_shaft(seed + 2)
    srgb, sa = shaft.straight()
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    rng = np.random.default_rng(seed)
    # jagged break line across the shaft
    xs = np.arange(COL_W)
    jag = 38 + 10 * np.sin(xs * 0.21 + 1.0) + 6 * np.sin(xs * 0.53) + rng.uniform(-2, 2, COL_W)
    jag = jag - (xs - 32) * 0.35
    yy = np.arange(h - 64)[:, None]
    keep = smoothstep(jag[None, :] - 0.5, jag[None, :] + 0.5, yy).astype(F32)
    src_rgb = np.tile(srgb, (1, 1, 1))[: h - 64] if srgb.shape[0] >= h - 64 else np.concatenate([srgb, srgb])[: h - 64]
    src_a = (sa if sa.shape[0] >= h - 64 else np.concatenate([sa, sa]))[: h - 64]
    # the broken face: lighter, rough
    face = smoothstep(jag[None, :] + 4, jag[None, :] + 0.5, yy) * keep
    rough = vnoise(COL_W, h - 64, 3, seed + 3, 2)
    src_rgb = lerp(src_rgb, MARBLE["light"] * (0.9 + 0.1 * rough)[..., None], face * 0.6)
    lay.blit(src_rgb, src_a * keep, (w - COL_W) // 2, 0)
    # moss and a little ivy on the break and down one side
    p = Painter(w, h, 3)
    off = (w - COL_W) // 2
    for i in range(40):
        xx = rng.uniform(4, COL_W - 4)
        p.circle(off + xx, jag[int(xx)] + rng.uniform(-1, 3), rng.uniform(2, 4.5), green_at(0.45 + 0.4 * rng.random()))
    vine = np.stack([off + 50 + 6 * np.sin(np.linspace(0, 6, 40)), np.linspace(jag[50], h - 20, 40)], 1)
    p.line(vine, 1.5, GREEN["dark"])
    for j in range(0, 40, 2):
        tt = rng.random()
        p.ivy_leaf(vine[j][0], vine[j][1], rng.uniform(-math.pi, math.pi), rng.uniform(5, 8),
                   green_at(0.35 + 0.3 * tt), green_at(0.6 + 0.35 * tt))
    for i in range(30):
        xx = rng.uniform(0, w)
        p.blade(tendril_pts(rng.uniform(5, 12), -math.pi / 2 + rng.uniform(-0.5, 0.5), 0.2, 2, 0, 8)
                + np.array([xx, h - 2], F32), 2.4, 0.6, green_at(0.4), green_at(0.8))
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    return lay, (0.5, 0.0)


def fallen_arch_fragment(seed=2801):
    """A fallen curved piece of an arch ring lying on the grass."""
    w, h = 256, 128
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    # an annulus sector, rotated to rest on its outer curve
    cx, cy, r_in, t = w / 2, h + 92, 120, 34
    d = np.hypot(x - cx, y - cy)
    th = np.arctan2(y - cy, x - cx)
    sector = smoothstep(-2.2, -2.15, th) * smoothstep(-0.9, -0.95, th)
    rng = np.random.default_rng(seed)
    endn = vnoise(w, h, 6, seed, 3)
    ring = smoothstep(r_in - 0.5, r_in + 0.5, d) * smoothstep(r_in + t + 0.5, r_in + t - 0.5, d)
    a = ring * smoothstep(-2.25 + 0.06 * endn, -2.15 + 0.06 * endn, th) * smoothstep(-0.85 - 0.06 * endn,
                                                                                     -0.95 - 0.06 * endn, th)
    nv = 12
    seg = ((th + math.pi) / (math.pi / nv)) % 1.0
    joint = np.exp(-((np.minimum(seg, 1 - seg)) / 0.04) ** 2)
    height = 8 + 4 * smoothstep(r_in, r_in + 4, d) * smoothstep(r_in + t, r_in + t - 4, d) - joint * 3
    br = relief(height, 0.8, blur=0.6) * cavity(height, 3, 0.3)
    rgb = marble(w, h, seed, scale=0.5) * br[..., None]
    gold = np.exp(-((d - (r_in + t - 4)) / 1.2) ** 2) * a
    rgb = lerp(rgb, gold_shade(br), gold * 0.8)
    # it is old: moss over the top, grass growing round the foot
    rgb = lerp(rgb, green_at(0.5), smoothstep(r_in + t - 10, r_in + t, d) * smoothstep(0.4, 0.7,
               vnoise(w, h, 12, seed + 4, 3)) * 0.8)
    lay = _lay(rgb, a)
    p = Painter(w, h, 3)
    for i in range(140):
        xx = rng.uniform(10, w - 10)
        p.blade(tendril_pts(rng.uniform(6, 18), -math.pi / 2 + rng.uniform(-0.6, 0.6), rng.uniform(-0.4, 0.4), 2, 0,
                            10) + np.array([xx, h - 1], F32), 2.6, 0.6, green_at(0.35), green_at(0.7 + 0.25 *
                                                                                                    rng.random()))
    for i in range(5):
        xx = rng.uniform(20, w - 20)
        p.blossom(xx, h - rng.uniform(6, 14), 3, WHITE_FLOWER, YELLOW, 5, rng.uniform(0, 6))
    pr, pa = p.get()
    lay.blit(pr, pa, 0, 0)
    return lay, (0.5, 0.0)
