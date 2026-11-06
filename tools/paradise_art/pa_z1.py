"""Zone 1, The Waking Hollow: dawn gold and rose, giant curled leaves."""
from __future__ import annotations

import math

from PIL import Image

from pa_backdrop import (curve_at, fill_below, leaf_shape, mask_ribbon, periodic_curve, sky_ramp, stem_shape,
                         wrapped)
from pa_core import (F32, Layer, MaskCanvas, RibbonCanvas, at_ts, c3, euler_spine, gblur, glow_disc, grad3, hexc,
                     inner_edge, lambert, lerp, np, pillow_normals, radial, rim_light, smoothstep, turns_k1, vnoise,
                     wrapd)
from pa_shapes import blob_cluster, fern_frond, leaf_geometry, render_leaf, tendril_pts

ZONE = "z1"
DIR = "Z1_WakingHollow"
W, H = 2048, 1152
SUN = (0.60 * W, 0.56 * H)
LIGHT2 = (0.55, -0.83)          # 2D light direction (towards the light), y down
LIGHT3 = (0.45, -0.7, 0.55)

PAL = {
    "sky": [(0.00, hexc("86609e")), (0.16, hexc("c4789e")), (0.32, hexc("ec8e98")), (0.46, hexc("ffb894")),
            (0.58, hexc("ffd6a0")), (0.70, hexc("ffe8bc")), (1.00, hexc("fbcaa6"))],
    "haze": hexc("ffd9b8"),
    "gold": hexc("ffd98a"),
    "gold_hi": hexc("fff3cf"),
    "rose": hexc("e7799a"),
    "plum": hexc("5a2346"),
    "plum_dark": hexc("2c0f26"),
}

LEAF_PAL = {  # play-adjacent / prop leaves: rose with gold light
    "base": hexc("7c2c55"), "mid": hexc("d8678a"), "tip": hexc("ffb592"), "under": hexc("6a2350"),
    "rim": hexc("fff0bc"), "vein": hexc("ffd590"), "midrib": hexc("ffe2a8"), "dark": hexc("3e0f30"),
}


# ================================================================== ground

GP1 = {
    "c2a1124": hexc("2a1124"),
    "c5a2640": hexc("5a2640"),
    "c643048": hexc("643048"),
    "c70384c": hexc("70384c"),
    "c562a40": hexc("562a40"),
    "c7a4252": hexc("7a4252"),
    "c4a1a34": hexc("4a1a34"),
    "ca45a68": hexc("a45a68"),
    "c4e4a26": hexc("4e4a26"),
    "c7a7038": hexc("7a7038"),
    "c1c0818": hexc("1c0818"),
    "c8a7a3a": hexc("5a7640"),
    "c5a3c34": hexc("5a3c34"),
    "c3a1c2c": hexc("3a1c2c"),
    "cffe9a0": hexc("eae4a4"),
    "ccdb85a": hexc("86ae5c"),
    "c6c6a2c": hexc("3c5a32"),
    "cf2e08a": hexc("dfe8a6"),
    "cc4ae4e": hexc("88ae5e"),
    "c8a7234": hexc("4c6c3a"),
    "c5c3a30": hexc("3a2c30"),
    "cfff4c4": hexc("f8eec2"),
    "cf0d870": hexc("a6c872"),
    "c9c9038": hexc("587c42"),
    "cfff0b8": hexc("f2e8b2"),
    "cb8a23e": hexc("74a052"),
    "cff9db4": hexc("ff9db4"),
    "cffe0e6": hexc("ffe0e6"),
    "c5a2a40": hexc("5a2a40"),
    "c3a1630": hexc("3a1630"),
    "c4a1e38": hexc("4a1e38"),
    "c341228": hexc("341228"),
    "gold": PAL["gold"], "gold_hi": PAL["gold_hi"],
    "fill_dark": hexc("2a1824"), "fill_mid": hexc("3a2230"), "fill_light": hexc("43293a"),
    "fill_moss": hexc("3e4030"),
    "n_back": 64, "n_front": 30, "n_flowers": 8, "n_curls": 5, "tex": 0.10,
}


def ground_fill(seed=101, gp=None):
    """Calm, dark, low-contrast ground body: large soft blotches, faint bark grain,
    a couple of barely-there spiral knots. Tiles both ways at 512."""
    gp = gp or GP1
    N = 512
    y, x = np.mgrid[0:N, 0:N].astype(F32)
    rng = np.random.default_rng(seed)
    big = vnoise(N, N, 256, seed, 3, 0.5, True, True)
    med = vnoise(N, N, 90, seed + 1, 3, 0.5, True, True)
    warp = vnoise(N, N, 128, seed + 2, 2, 0.5, True, True)
    # soft blotches between the darkest and a slightly lifted body colour
    t = np.clip(0.55 * big + 0.45 * med, 0, 1)
    rgb = grad3([(0.0, gp["fill_dark"]), (0.6, gp["fill_mid"]), (1.0, gp["fill_light"])], t)
    # faint bark grain: long, gently warped streaks (anisotropic noise)
    grain = vnoise(N, N, 1, seed + 3, 3, 0.55, True, True, sx=170, sy=9)
    yy = y + (warp - 0.5) * 40
    streak = 0.5 + 0.5 * np.sin(2 * math.pi * 7 * yy / N + grain * 3.0)
    rgb = rgb * (0.965 + 0.05 * streak * grain)[..., None]
    # fine earthy tooth
    fine = vnoise(N, N, 5, seed + 4, 2, 0.5, True, True)
    rgb = rgb * (0.975 + 0.05 * fine)[..., None]
    # soft moss bloom, very low strength
    moss = smoothstep(0.55, 0.95, vnoise(N, N, 140, seed + 5, 3, 0.5, True, True))
    rgb = lerp(rgb, gp["fill_moss"], moss * 0.16)
    # two barely-there spiral knots
    for k in range(2):
        cx, cy = rng.uniform(0, N, 2)
        R = rng.uniform(26, 38)
        dx, dy = wrapd(x - cx, N), wrapd(y - cy, N)
        r = np.hypot(dx, dy)
        th = np.arctan2(dy, dx)
        spiral = 0.5 + 0.5 * np.cos(r / R * 12 - th * 2)
        disc = smoothstep(R, R * 0.4, r)
        rgb = rgb * (1 - 0.06 * disc * spiral)[..., None]
    # a handful of dim specks
    for _ in range(10):
        cx, cy = rng.uniform(0, N, 2)
        r = np.hypot(wrapd(x - cx, N), wrapd(y - cy, N))
        rgb = rgb + gp["gold"] * (np.exp(-(r / 1.3) ** 2) * 0.18 + np.exp(-(r / 5) ** 2) * 0.03)[..., None]
    return Layer_from(np.clip(rgb, 0, 1), np.ones((N, N), F32))


def Layer_from(rgb, a):
    L = Layer(rgb.shape[1], rgb.shape[0])
    L.set_straight(rgb, a)
    return L


def _blades(mc, n, seed, ybase, lmin, lmax, wmin, wmax, Wd=512, lean=0.55, curl=0.7):
    rng = np.random.default_rng(seed)
    for i in range(n):
        x = rng.uniform(0, Wd)
        L = rng.uniform(lmin, lmax)
        th = -math.pi / 2 + rng.uniform(-lean, lean)
        turns = rng.uniform(0.0, curl) * (1 if rng.random() < 0.5 else -1)
        pts = tendril_pts(L, th, turns, 2.6, 0.0, 30) + np.array([x, ybase + rng.uniform(-2, 3)], F32)
        w0 = rng.uniform(wmin, wmax)
        wrapped(Wd, x, lambda off: mc.taper_line(pts + np.array([off, 0], F32), w0, 0.7))


def _flower(mp, mcc, x, y, r, n=5, rot=0.0, Wd=None):
    for k in range(n):
        a = rot + k * 2 * math.pi / n
        pts = euler_spine(r, a, 0, 0, 1, 12) + np.array([x, y], F32)
        t = np.linspace(0, 1, 12)
        hw = np.sin(np.pi * np.clip(t * 0.95 + 0.05, 0, 1)) ** 0.8 * r * 0.42
        if Wd:
            wrapped(Wd, x, lambda off: mask_ribbon(mp, pts, hw, hw, off))
        else:
            mask_ribbon(mp, pts, hw, hw)
    if Wd:
        wrapped(Wd, x, lambda off: mcc.circle(x + off, y, r * 0.32))
    else:
        mcc.circle(x, y, r * 0.32)


def ground_top(seed=111, S=51, gp=None, flowers=True):
    gp = gp or GP1
    """Top edge strip 512x128. Walkable line at row S."""
    Wd, Hd = 512, 128
    y, x = np.mgrid[0:Hd, 0:Wd].astype(F32)
    rng = np.random.default_rng(seed)
    lay = Layer(Wd, Hd)
    # --- lip mask: flat top at S, scalloped moss cushion hanging below
    lip = MaskCanvas(Wd, Hd, 3)
    xs, top = periodic_curve(Wd, S, [(4, 0.8), (9, 0.5)], seed, 2)
    _, bot = periodic_curve(Wd, S + 17, [(2, 3.0), (5, 2.5), (11, 1.5)], seed + 1, 2)
    pts = [(a, b) for a, b in zip(xs, top)] + [(a, b) for a, b in zip(xs[::-1], bot[::-1])]
    lip.poly(pts)
    for i in range(26):
        cx = (i + rng.uniform(-0.3, 0.3)) * Wd / 26
        r = rng.uniform(7, 13)
        cy = curve_at(xs, bot, cx, Wd) - r * 0.35
        wrapped(Wd, cx, lambda off: lip.circle(cx + off, cy, r))
    m_lip = lip.get()
    # --- under-lip shadow on the fill
    sh = gblur(np.roll(m_lip, 5, axis=0), 5, wrap_x=True) * (1 - m_lip)
    sh = np.clip(sh * 1.4, 0, 1) * smoothstep(S, S + 8, y)
    lay.over(gp["c1c0818"], sh * 0.6)
    # --- hanging moss drips (behind the lip)
    dr = MaskCanvas(Wd, Hd, 3)
    tips = []
    for i in range(46):
        cx = rng.uniform(0, Wd)
        y0 = curve_at(xs, bot, cx, Wd) - 4
        L = rng.uniform(5, 26) * (1.6 if rng.random() < 0.15 else 1.0)
        pts_d = tendril_pts(L, math.pi / 2 + rng.uniform(-0.2, 0.2), rng.uniform(-0.5, 0.5), 2.5, 0, 16) \
            + np.array([cx, y0], F32)
        w0 = rng.uniform(2.0, 4.0)
        wrapped(Wd, cx, lambda off: dr.taper_line(pts_d + np.array([off, 0], F32), w0, 0.8))
        if rng.random() < 0.3:
            tips.append(pts_d[-1])
    m_dr = dr.get()
    dcol = grad3([(S + 10, gp["c8a7a3a"]), (S + 30, gp["c5a3c34"]), (Hd, gp["c3a1c2c"])], y)
    lay.over(dcol, m_dr)
    for tx, ty in tips:
        for off in (-Wd, 0, Wd):
            r = np.hypot(x - tx - off, y - ty - 1.5)
            lay.add_light(gp["gold"], np.exp(-(r / 1.4) ** 2) * 0.9 + np.exp(-(r / 4.5) ** 2) * 0.25)
    # --- back tufts (behind the lip)
    bk = MaskCanvas(Wd, Hd, 3)
    _blades(bk, gp.get("n_back", 120), seed + 2, S + 4, 10, 34, 3.0, 5.0, Wd)
    m_bk = bk.get()
    bcol = grad3([(S - 42, gp["cffe9a0"]), (S - 20, gp["ccdb85a"]), (S + 4, gp["c6c6a2c"])], y)
    lay.over(bcol * 0.92, m_bk)
    # --- lip body, pillow-shaded
    nx, ny, nz = pillow_normals(m_lip, 5.0, 1.0, wrap_x=True)
    lam = lambert(nx, ny, nz, (0.35, -0.85, 0.45))
    depth = np.clip((y - S) / 26, 0, 1)
    tex = vnoise(Wd, Hd, 4, seed + 3, 3, 0.5, True, False)
    base = grad3([(0.0, gp["cf2e08a"]), (0.25, gp["cc4ae4e"]), (0.6, gp["c8a7234"]), (1.0, gp["c5c3a30"])],
                 depth)
    tx_ = gp.get("tex", 0.22)
    col = base * (0.62 + 0.5 * lam)[..., None] * (1 - tx_ / 2 + tx_ * tex)[..., None]
    toplight = np.exp(-((y - S - 1.2) / 1.8) ** 2)
    col = lerp(col, gp["gold_hi"], toplight * 0.75 * m_lip)
    lay.over(np.clip(col, 0, 1), m_lip)
    # --- front tufts, brighter, over the lip edge
    fr = MaskCanvas(Wd, Hd, 3)
    _blades(fr, gp.get("n_front", 70), seed + 4, S + 6, 6, 22, 2.5, 4.2, Wd, lean=0.7, curl=0.9)
    m_fr = fr.get()
    fcol = grad3([(S - 30, gp["cfff4c4"]), (S - 10, gp["cf0d870"]), (S + 6, gp["c9c9038"])], y)
    lay.over(fcol, m_fr)
    # --- fiddlehead curls
    cu = MaskCanvas(Wd, Hd, 3)
    nc = gp.get("n_curls", 7)
    for i in range(nc):
        cx = (i + rng.uniform(0, 0.8)) * Wd / nc
        L = rng.uniform(22, 38)
        sgn = 1 if rng.random() < 0.5 else -1
        p = tendril_pts(L, -math.pi / 2 + sgn * 0.2, sgn * 1.25, 2.2, 0, 60) + np.array([cx, S + 2], F32)
        wrapped(Wd, cx, lambda off: cu.taper_line(p + np.array([off, 0], F32), 3.6, 1.6))
    m_cu = cu.get()
    lay.over(grad3([(S - 38, gp["cfff0b8"]), (S + 2, gp["cb8a23e"])], y), m_cu)
    # --- little rose flowers on the lip
    fp, fc = MaskCanvas(Wd, Hd, 4), MaskCanvas(Wd, Hd, 4)
    for i in range(gp.get("n_flowers", 16) if flowers else 0):
        cx = rng.uniform(0, Wd)
        cy = S - rng.uniform(0, 14)
        _flower(fp, fc, cx, cy, rng.uniform(3.2, 5.0), 5, rng.uniform(0, 6.28), Wd)
    m_fp, m_fc = fp.get(), fc.get()
    lay.over(lerp(gp["cff9db4"], gp["cffe0e6"], smoothstep(S + 2, S - 14, y)), m_fp)
    lay.over(gp["gold"], m_fc)
    lay.add_light(gp["gold"], gblur(m_fc, 2.0, wrap_x=True) * 0.35)
    return lay


def ground_bottom(seed=121, row=6, gp=None):
    gp = gp or GP1
    """Underside strip 512x64: dark body band on top, hanging roots below."""
    Wd, Hd = 512, 64
    y, x = np.mgrid[0:Hd, 0:Wd].astype(F32)
    rng = np.random.default_rng(seed)
    lay = Layer(Wd, Hd)
    body = MaskCanvas(Wd, Hd, 3)
    xs, bot = periodic_curve(Wd, row + 8, [(3, 2.5), (7, 1.5), (13, 1.0)], seed, 2)
    body.poly([(a, -2) for a in xs] + [(a, b) for a, b in zip(xs[::-1], bot[::-1])])
    for i in range(18):
        cx = rng.uniform(0, Wd)
        r = rng.uniform(4, 9)
        cy = curve_at(xs, bot, cx, Wd) - r * 0.3
        wrapped(Wd, cx, lambda off: body.circle(cx + off, cy, r))
    m_body = body.get()
    roots = MaskCanvas(Wd, Hd, 3)
    tips = []
    for i in range(16):
        cx = rng.uniform(0, Wd)
        L = rng.uniform(16, 54)
        sgn = 1 if rng.random() < 0.5 else -1
        p = tendril_pts(L, math.pi / 2 + rng.uniform(-0.25, 0.25), sgn * rng.uniform(0.2, 0.9), 2.8, 0, 50) \
            + np.array([cx, row + 4], F32)
        w0 = rng.uniform(4, 8)
        wrapped(Wd, cx, lambda off: roots.taper_line(p + np.array([off, 0], F32), w0, 1.0))
        # root hairs
        for _ in range(2):
            k = int(rng.uniform(0.3, 0.8) * len(p))
            hp = tendril_pts(rng.uniform(5, 11), math.pi / 2 + rng.uniform(-1.0, 1.0), 0, 1, 0, 8) + p[k]
            wrapped(Wd, cx, lambda off: roots.taper_line(hp + np.array([off, 0], F32), 1.4, 0.5))
        if rng.random() < 0.4:
            tips.append(p[-1])
    m_roots = roots.get()
    nx, ny, nz = pillow_normals(m_roots, 2.0, 1.0, True)
    lam = lambert(nx, ny, nz, (-0.4, 0.5, 0.7))  # bounce light from below
    rcol = grad3([(0, gp["c5a2a40"]), (Hd, gp["c3a1630"])], y) * (0.8 + 0.5 * lam)[..., None]
    lay.over(rcol, m_roots)
    nx, ny, nz = pillow_normals(m_body, 4.0, 1.0, True)
    lam = lambert(nx, ny, nz, (0.0, 0.8, 0.6))
    bcol = grad3([(0, gp["c4a1e38"]), (row + 14, gp["c341228"])], y) * (0.85 + 0.35 * lam)[..., None]
    lay.over(bcol, m_body)
    for tx, ty in tips:
        for off in (-Wd, 0, Wd):
            r = np.hypot(x - tx - off, y - ty - 1)
            lay.add_light(gp["gold"], np.exp(-(r / 1.5) ** 2) * 0.9 + np.exp(-(r / 5) ** 2) * 0.2)
    return lay


# ================================================================== backdrops

def sky(seed=201):
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    ramp = sky_ramp(PAL["sky"], H)
    rgb = np.broadcast_to(ramp[:, None, :], (H, W, 3)).copy()
    sx, sy = SUN
    dx = wrapd(x - sx, W)
    dy = y - sy
    r = np.hypot(dx, dy)
    # wide warm bloom, halo and core
    rgb = rgb + hexc("ffb58a") * (np.exp(-(r / 900) ** 1.6) * 0.22)[..., None]
    rgb = lerp(rgb, hexc("ffe2b0"), np.exp(-(r / 330) ** 1.5) * 0.55)
    rgb = lerp(rgb, hexc("fff8e6"), np.exp(-(r / 70) ** 2) * 0.95)
    # clouds: long dawn streaks lit from below by the sun
    clouds = np.zeros((H, W), F32)
    for k, (cy, hh, sxs, thr) in enumerate([(230, 140, 520, 0.56), (420, 120, 380, 0.58), (600, 80, 300, 0.6)]):
        n = vnoise(W, H, 1, seed + k, 5, 0.5, True, False, sx=sxs, sy=hh * 0.35)
        band = np.exp(-((y - cy) / hh) ** 2)
        clouds = np.maximum(clouds, smoothstep(thr, thr + 0.22, n * band + 0.25 * band))
    clouds = gblur(clouds, 2.0, wrap_x=True)
    nx, ny, nz = pillow_normals(clouds, 10, 1.6, True)
    ln = np.hypot(dx, dy) + 1e-3
    lx, ly = -dx / ln, -dy / ln
    lam = np.clip(nx * lx + ny * ly, 0, 1)
    near = np.exp(-(r / 700) ** 1.3)
    ccol = lerp(lerp(hexc("8e5c9c"), hexc("d78aa0"), smoothstep(100, 650, y)), hexc("ffd8a4"),
                np.clip(lam * 0.9 + near * 0.5, 0, 1))
    ccol = lerp(ccol, hexc("fff2d0"), inner_edge(clouds, 4, True) * np.clip(lam + near, 0, 1) * 0.7)
    rgb = lerp(rgb, ccol, clouds * 0.82)
    # sun rays
    th = np.arctan2(dy, dx)
    streak = (0.5 + 0.5 * np.cos(th * 9 + 1.0)) * (0.5 + 0.5 * np.cos(th * 23 + 2.0)) \
        * (0.6 + 0.4 * np.cos(th * 5 + 0.3))
    rays = streak * np.exp(-(r / 520) ** 2) * smoothstep(40, 160, r)
    rgb = rgb + hexc("ffe6b8") * (rays * 0.16)[..., None]
    return Layer_from(np.clip(rgb, 0, 1), np.ones((H, W), F32))


def _sky_at(yrow):
    return grad3(PAL["sky"], np.clip(np.asarray(yrow, F32) / (H - 1), 0, 1))


def _sun_glow(blur_r=600):
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    r = np.hypot(wrapd(x - SUN[0], W), y - SUN[1])
    return np.exp(-(r / blur_r) ** 1.4), y


def far(seed=301):
    mc = MaskCanvas(W, H, 2)
    xs, hy = periodic_curve(W, 660, [(1, 34), (2, 24), (3, 14), (6, 6)], seed)
    wrapped(W, 0, lambda off: fill_below(mc, xs, hy, H, W, off))
    rng = np.random.default_rng(seed)
    for i in range(7):
        cx = (i + rng.uniform(0, 1)) * W / 7
        s = rng.integers(0, 1000)
        for off in (-W, 0, W):
            blob_cluster(mc, cx + off, curve_at(xs, hy, cx, W) - 40, 110 + (s % 80), 7, seed + i, 1.0, 0.6)
    for i in range(13):
        cx = (i + rng.uniform(0.1, 0.9)) * W / 13
        L = rng.uniform(340, 720)
        sgn = 1 if i % 2 else -1
        leaf_shape(mc, W, cx, curve_at(xs, hy, cx, W) + 40, L, -math.pi / 2 + sgn * rng.uniform(0.05, 0.4),
                   rng.uniform(26, 56), sgn * rng.uniform(0.9, 1.4), 0.0, 3.0, seed + 50 + i, 0.25, 0.25)
    m = gblur(mc.get(), 1.6, wrap_x=True)
    glow, y = _sun_glow(520)
    base = _sky_at(y[:, :1].repeat(W, 1) - 90)
    col = lerp(base, hexc("c97a98"), 0.30)
    col = lerp(col, hexc("ffd3a8"), glow * 0.55)
    col = lerp(col, PAL["haze"], smoothstep(560, H, y) * 0.65)
    rim = rim_light(m, 3.0, (0.3, -1.0), True)
    col = lerp(col, hexc("ffe8c4"), rim * 0.45)
    lay = Layer(W, H)
    lay.set_straight(np.clip(col, 0, 1), m)
    return lay


def midfar(seed=401):
    mc = MaskCanvas(W, H, 2)
    rng = np.random.default_rng(seed)
    xs, hy = periodic_curve(W, 780, [(1, 36), (2, 26), (4, 12), (7, 6)], seed)
    wrapped(W, 0, lambda off: fill_below(mc, xs, hy, H, W, off))
    for i in range(5):
        cx = (i + rng.uniform(0, 1)) * W / 5
        for off in (-W, 0, W):
            blob_cluster(mc, cx + off, curve_at(xs, hy, cx, W) - 10, 110, 7, seed + i, 1.1, 0.55)
    # giant curled leaves rising and arching
    for i in range(8):
        cx = (i + rng.uniform(0.1, 0.9)) * W / 8
        sgn = 1 if i % 2 == 0 else -1
        L = rng.uniform(520, 900)
        leaf_shape(mc, W, cx, curve_at(xs, hy, cx, W) + 50, L, -math.pi / 2 + sgn * rng.uniform(0.2, 0.55),
                   rng.uniform(50, 95), sgn * rng.uniform(0.9, 1.3), sgn * 0.0004, 3.2, seed + 70 + i, 0.3, 0.12)
    # thin curling stems
    for i in range(10):
        cx = rng.uniform(0, W)
        sgn = 1 if rng.random() < 0.5 else -1
        stem_shape(mc, W, cx, curve_at(xs, hy, cx, W) + 30, rng.uniform(200, 420),
                   -math.pi / 2 + sgn * rng.uniform(0, 0.3), 9, 3, sgn * rng.uniform(1.0, 1.8), 2.6, 0, seed + i)
    m = gblur(mc.get(), 1.0, wrap_x=True)
    glow, y = _sun_glow(560)
    nx, ny, nz = pillow_normals(m, 14, 0.8, True)
    lam = lambert(nx, ny, nz, (0.4, -0.75, 0.6))
    col = lerp(hexc("a8547e"), hexc("d98a96"), lam * 0.7)
    col = lerp(col, _sky_at(y - 120), 0.18)
    col = lerp(col, hexc("ffc7a0"), glow * 0.4)
    col = lerp(col, PAL["haze"] * 0.95, smoothstep(700, H, y) * 0.6)
    rim = rim_light(m, 4.0, LIGHT2, True)
    col = lerp(col, hexc("ffe2a6"), rim * 0.75)
    lay = Layer(W, H)
    lay.set_straight(np.clip(col, 0, 1), m)
    return lay


MID_LEAF = {
    "base": hexc("2c0c28"), "mid": hexc("521c44"), "tip": hexc("80345a"), "under": hexc("3a1034"),
    "rim": hexc("ffcf96"), "vein": hexc("a85e6e"), "midrib": hexc("b06c70"), "dark": hexc("180614"),
}


def mid(seed=501):
    lay = Layer(W, H)
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    # mounds at the base
    mc = MaskCanvas(W, H, 2)
    xs, hy = periodic_curve(W, 930, [(1, 26), (3, 16), (5, 10), (9, 5)], seed)
    wrapped(W, 0, lambda off: fill_below(mc, xs, hy, H, W, off))
    for i in range(9):
        cx = (i + rng.uniform(0, 1)) * W / 9
        for off in (-W, 0, W):
            blob_cluster(mc, cx + off, curve_at(xs, hy, cx, W) + 10, 70 + 30 * ((i * 7) % 3), 5, seed + i, 1.2, 0.5)
    stems = MaskCanvas(W, H, 2)
    pods = []
    for i in range(9):
        cx = rng.uniform(0, W)
        sgn = 1 if rng.random() < 0.5 else -1
        L = rng.uniform(260, 520)
        p = stem_shape(stems, W, cx, curve_at(xs, hy, cx, W) + 20, L, -math.pi / 2 + sgn * rng.uniform(0.05, 0.35),
                       12, 4, sgn * rng.uniform(0.25, 0.6), 2.0, 0, seed + 30 + i)
        if rng.random() < 0.6:
            pods.append(p[int(len(p) * 0.995) - 1])
    # back leaves (silhouette-shaded through the mound pass)
    for i in range(6):
        cx = (i + rng.uniform(0, 1)) * W / 6
        sgn = 1 if rng.random() < 0.5 else -1
        leaf_shape(mc, W, cx, curve_at(xs, hy, cx, W) + 30, rng.uniform(300, 520),
                   -math.pi / 2 + sgn * rng.uniform(0.3, 0.6), rng.uniform(40, 70), sgn * 1.1, 0, 3.0, seed + 90 + i)
    m = gblur(np.maximum(mc.get(), 0), 0.8, wrap_x=True)
    nx, ny, nz = pillow_normals(m, 12, 0.9, True)
    lam = lambert(nx, ny, nz, LIGHT3)
    col = lerp(hexc("34112e"), hexc("6a2c4e"), lam)
    col = lerp(col, PAL["haze"] * 0.8, smoothstep(880, H, y) * 0.4)
    col = lerp(col, hexc("ffc890"), rim_light(m, 3.5, LIGHT2, True) * 0.8)
    lay.over(np.clip(col, 0, 1), m)
    ms = stems.get()
    nx, ny, nz = pillow_normals(ms, 3, 1.0, True)
    lam = lambert(nx, ny, nz, LIGHT3)
    scol = lerp(hexc("3a1232"), hexc("8c4060"), lam)
    scol = lerp(scol, hexc("ffd09a"), rim_light(ms, 2.0, LIGHT2, True) * 0.8)
    lay.over(scol, ms)
    # big shaded leaves in front
    for i in range(4):
        cx = (i + rng.uniform(0.1, 0.9)) * W / 4
        sgn = 1 if i % 2 == 0 else -1
        L = rng.uniform(700, 980)
        th = -math.pi / 2 + sgn * rng.uniform(0.35, 0.7)
        rgb, a, (ox, oy), _ = render_leaf(L, th, rng.uniform(120, 170), MID_LEAF, turns=sgn * rng.uniform(1.0, 1.3),
                                          k0=sgn * 0.0006, p=3.2, seed=seed + 200 + i, light=LIGHT2, vein_w=2.5,
                                          veins=8, tex=0.05)
        by = curve_at(xs, hy, cx, W) + 40
        lay.blit(rgb, a, cx - ox, by - oy, wrap_x=True)
    # glowing seed pods on the stems
    for (px, py) in pods:
        for off in (-W, 0, W):
            r = np.hypot(x - px - off, y - py)
            core = smoothstep(9, 6, r)
            lay.over(lerp(hexc("ffd38a"), hexc("fff6dc"), smoothstep(7, 0, r)), core)
            lay.add_light(hexc("ffc070"), np.exp(-(r / 26) ** 2) * 0.45)
    # warm haze over the whole layer so it sits behind the play layer
    rgb, a = lay.straight()
    rgb = lerp(rgb, hexc("e8a0a0"), 0.08) * 0.82
    lay.set_straight(rgb, gblur(a, 0.6, wrap_x=True))
    lay.rgb = gblur(lay.rgb, 0.6, wrap_x=True)
    return lay


# ================================================================== foreground silhouettes

FG_COL = hexc("2a0c24")
FG_RIM = hexc("b8607a")
FG_MID = hexc("4a1a3c")


def _fg_finish(m, blur=3.0, rim_dir=(0.4, -0.9), rim_amt=0.35):
    m = gblur(m, blur)
    nx, ny, nz = pillow_normals(m, 18, 0.6)
    lam = lambert(nx, ny, nz, (0.3, -0.8, 0.6))
    col = lerp(FG_COL, FG_MID, lam * 0.7)
    col = lerp(col, FG_RIM, rim_light(m, 6, rim_dir) * rim_amt)
    lay = Layer(m.shape[1], m.shape[0])
    lay.set_straight(col, m)
    return lay


def fg_fern(seed=601):
    w, h = 640, 1024
    mc = MaskCanvas(w, h, 2)
    fern_frond(mc, 300, h + 20, 1180, -math.pi / 2 + 0.12, turns=1.3, k0=-0.0002, stem_w=22, pinnae=20,
               seed=seed, ribbon=False, pinna_len=230)
    return _fg_finish(mc.get(), 3.0)


def fg_leaf_big(seed=611):
    w, h = 1024, 768
    mc = MaskCanvas(w, h, 2)
    pts, hwl, hwr = leaf_geometry(1250, 0.35, 230, 1.15, 0.0003, 3.0, 0.3, 0.1, 0.0, 1.0, 1.0, 300, 0, seed)
    pts = pts + np.array([-60, 110], F32)
    mask_ribbon(mc, pts, hwl, hwr)
    stem_pts = euler_spine(200, 0.35 + math.pi, 0, 0, 1, 20)[::-1] + np.array([-60, 110], F32)
    mc.taper_line(stem_pts, 10, 20)
    m = mc.get()
    # leave thin vein gaps for life
    vm = MaskCanvas(w, h, 2)
    for j in range(10):
        t0 = 0.06 + 0.7 * j / 10
        for side in (-1, 1):
            u = np.linspace(0, 1, 20)
            q = at_ts(pts, hwl, hwr, np.clip(t0 + 0.14 * u ** 1.4, 0, 1), side * (0.06 + 0.8 * u))
            vm.taper_line(q, 4, 1)
    m = m * (1 - 0.35 * vm.get())
    return _fg_finish(m, 3.5, (0.2, -1.0), 0.4)


def fg_vines(seed=621):
    w, h = 384, 1024
    mc = MaskCanvas(w, h, 2)
    rng = np.random.default_rng(seed)
    for i in range(3):
        x0 = 70 + i * 120 + rng.uniform(-20, 20)
        L = rng.uniform(700, 980)
        sgn = 1 if i % 2 else -1
        p = tendril_pts(L, math.pi / 2 + sgn * 0.05, sgn * 1.4, 4.0, 0, 300, 3.0, seed + i) + np.array([x0, -10], F32)
        mc.taper_line(p, 14, 4)
        for k in range(8, 260, 24):
            side = 1 if (k // 24) % 2 else -1
            lp, lw, _ = leaf_geometry(rng.uniform(40, 80), math.pi / 2 + side * 0.9, 16, 0.3 * side, 0, 2.0, 0.35,
                                      0.15, 0, 1, 1, 30)
            mask_ribbon(mc, lp + p[k], lw, lw)
    return _fg_finish(mc.get(), 3.0, (0.6, -0.4), 0.3)


def fg_grass(seed=631):
    w, h = 1024, 512
    mc = MaskCanvas(w, h, 2)
    rng = np.random.default_rng(seed)
    for i in range(34):
        x = rng.uniform(80, w - 80)
        L = rng.uniform(160, 460) * (1 - abs(x - w / 2) / w)
        sgn = 1 if x > w / 2 else -1
        p = tendril_pts(L, -math.pi / 2 + sgn * rng.uniform(0.1, 0.6), sgn * rng.uniform(0.3, 1.2), 2.6, 0, 80) \
            + np.array([x, h + 10], F32)
        mc.taper_line(p, rng.uniform(14, 26), 3)
    return _fg_finish(mc.get(), 3.0)


def fg_curl(seed=641):
    w, h = 512, 768
    mc = MaskCanvas(w, h, 2)
    rng = np.random.default_rng(seed)
    for i, (x, L, sgn) in enumerate([(220, 700, 1), (300, 520, -1), (160, 420, -1), (360, 360, 1)]):
        p = tendril_pts(L, -math.pi / 2 + sgn * 0.12, sgn * 1.6, 3.0, 0, 260, 1.5, seed + i) + np.array([x, h + 5],
                                                                                                         F32)
        mc.taper_line(p, 26 - i * 3, 6)
        for k in range(30, 180, 40):
            side = -sgn
            lp, lw, _ = leaf_geometry(rng.uniform(60, 110), math.atan2(p[k + 1][1] - p[k][1], p[k + 1][0] - p[k][0])
                                      + side * 0.9, 24, 0.4 * side, 0, 2.0, 0.35, 0.15, 0, 1, 1, 30)
            mask_ribbon(mc, lp + p[k], lw, lw)
    return _fg_finish(mc.get(), 3.0)


# ================================================================== props

def giant_leaf(seed=701):
    """The Rising Leaf family: a huge rose leaf curling at the tip (1024 px)."""
    rgb, a, (ox, oy), vm = render_leaf(1500, -0.95, 280, LEAF_PAL, turns=1.35, k0=0.00035, p=3.3, ss=2, K=12,
                                       veins=13, vein_w=3.0, seed=seed, light=LIGHT2, tex=0.05, stem=110,
                                       stem_w=16)
    lay = Layer(a.shape[1], a.shape[0])
    # soft dew drops along the veins
    lay.set_straight(rgb, a)
    rng = np.random.default_rng(seed)
    h, w = a.shape
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    glow = np.zeros((h, w), F32)
    if vm is not None:
        glow += gblur(vm, 1.0) * 0.55
    ys, xs_ = np.nonzero((vm if vm is not None else a) > 0.5)
    for _ in range(14):
        k = rng.integers(0, len(ys))
        r = np.hypot(x - xs_[k], y - ys[k])
        drop = smoothstep(5.5, 3.5, r)
        lay.over(lerp(hexc("ffe3b0"), hexc("ffffff"), smoothstep(3, 0, r)), drop * a)
        glow += np.exp(-(r / 3.5) ** 2) + 0.3 * np.exp(-(r / 12) ** 2)
    glow = np.clip(glow * a, 0, 1)
    img, gimg, (sc, fx, fy) = _fit(lay, glow, 1024)
    return img, gimg, ((ox * sc + fx) / 1024, 1 - (oy * sc + fy) / 1024)


def _fit(lay, glow, size):
    """Scale a layer (and its glow mask) down to fit a size x size canvas, centred at the bottom."""
    rgb, a = lay.straight()
    h, w = a.shape
    s = min(1.0, size / max(w, h))
    nw, nh = int(round(w * s)), int(round(h * s))
    prem = rgb * a[..., None]
    from pa_core import to_image
    img = to_image(prem, a).resize((nw, nh), Image.LANCZOS)  # premultiplied resize
    arr = np.asarray(img).astype(F32) / 255
    pa, pr = arr[..., 3], arr[..., :3]
    rgb2 = np.where(pa[..., None] > 1e-3, pr / np.maximum(pa[..., None], 1e-3), 0)
    canvas = Layer(size, size)
    ox, oy = (size - nw) // 2, size - nh
    canvas.blit(np.clip(rgb2, 0, 1), np.clip(pa, 0, 1), ox, oy)
    g = Image.fromarray(np.clip(glow, 0, 1).astype(F32), "F").resize((nw, nh), Image.BILINEAR)
    gl = Layer(size, size)
    garr = np.clip(np.asarray(g), 0, 1)
    gl.blit(np.ones((nh, nw, 3), F32) * hexc("ffd590"), garr, ox, oy)
    return canvas, gl, (s, ox, oy)


def seed_pod(seed=711, size=128):
    """A closed seed bud: three wrapped petals with light peeking through the
    seams at the tip, curled sepals at the base, on a curled stalk. It bursts
    into light when hit."""
    s = size
    y, x = np.mgrid[0:s, 0:s].astype(F32)
    cx, cy = s * 0.5, s * 0.47
    ry = s * 0.34
    bx, by = cx, cy + ry * 0.82            # bud base, where the stalk starts
    L3 = (0.45, -0.7, 0.6)

    def petal(L, th, width, k0):
        mc = MaskCanvas(s, s, 4)
        pts, hwl, hwr = leaf_geometry(L, th, width, 0.0, k0, 1.0, 0.42, 0.45, 0.0, 1.25, 1.0, 120)
        mask_ribbon(mc, pts + np.array([bx, by], F32), hwl, hwr)
        vm = MaskCanvas(s, s, 4)
        for sv in (-0.45, 0.0, 0.45):
            q = at_ts(pts + np.array([bx, by], F32), hwl, hwr, np.linspace(0.08, 0.85, 30), np.full(30, sv))
            vm.taper_line(q, 1.2, 0.4, 200, 30)
        return mc.get(), vm.get()

    m_c, v_c = petal(80, -math.pi / 2, 25, 0.0)
    m_l, v_l = petal(74, -math.pi / 2 - 0.30, 23, 0.0075)
    m_r, v_r = petal(74, -math.pi / 2 + 0.30, 23, -0.0075)
    union = np.clip(m_c + m_l + m_r, 0, 1)
    lay = Layer(s, s)
    # stalk curling to the ground (unchanged geometry: the pivot is its foot)
    st = MaskCanvas(s, s, 4)
    p = tendril_pts(40, math.pi / 2 + 0.3, -0.6, 2, 0, 40) + np.array([cx, cy + ry * 0.85], F32)
    st.taper_line(p, 8, 4)
    ms = st.get()
    nx2, ny2, nz2 = pillow_normals(ms, 2, 1.0)
    lay.over(lerp(hexc("33402a"), hexc("8aa45e"), lambert(nx2, ny2, nz2, L3)), ms)
    # light inside the bud, seen between the petal tips
    inner_light = np.exp(-(np.hypot(x - cx, (y - (by - 62)) * 0.8) / 11) ** 2)
    tipmask = np.clip(gblur(union, 1.5) * 1.6, 0, 1)
    lay.over(lerp(hexc("ffd88e"), hexc("fff4d8"), inner_light),
             np.clip(inner_light * 1.4, 0, 1) * smoothstep(by - 40, by - 55, y) * tipmask)

    def shade(m, v, dark, mid, lit, back=False):
        nx, ny, nz = pillow_normals(m, 7, 1.0)
        lam = lambert(nx, ny, nz, L3)
        col = grad3([(0.0, dark), (0.6, mid), (1.0, lit)], lam)
        # warmer, lighter towards the tip
        col = lerp(col, hexc("ffc8a8"), smoothstep(by - 30, by - 78, y) * 0.35)
        col = lerp(col, hexc("ffe2c4"), v * 0.35)
        col = lerp(col, hexc("fff0d0"), rim_light(m, 2.0, (0.6, -0.8)) * 0.55)
        if back:
            col = col * 0.8
        return np.clip(col, 0, 1)

    lay.over(shade(m_c, v_c, hexc("5a1e44"), hexc("b45478"), hexc("f29a9c"), True), m_c)
    for m, v in ((m_l, v_l), (m_r, v_r)):
        # thin dark gap where a petal overlaps the one behind
        gap = gblur(m, 1.2) * (1 - m) * m_c
        lay.over(hexc("2e0c24"), np.clip(gap * 2.0, 0, 1) * 0.6)
        lay.over(shade(m, v, hexc("6a2450"), hexc("c8607e"), hexc("ffb0a2")), m)
    # seams: the inner edges of the front petals, glowing near the tip
    seams = (inner_edge(m_l, 1.0) + inner_edge(m_r, 1.0)) * (1 - inner_edge(union, 1.6) * 1.2)
    seams = np.clip(seams, 0, 1) * union * smoothstep(by - 18, by - 60, y)
    lay.over(hexc("ffd88e"), np.clip(seams * 1.2, 0, 1) * 0.85)
    # sepals: curled green leaves cupping the base
    sp = MaskCanvas(s, s, 4)
    for ang, L, turn in ((-math.pi / 2 - 0.95, 30, -0.35), (-math.pi / 2 + 0.95, 30, 0.35),
                         (-math.pi / 2 - 1.55, 24, -0.45), (-math.pi / 2 + 1.55, 24, 0.45),
                         (-math.pi / 2 - 0.35, 22, -0.2)):
        lp, lw, _ = leaf_geometry(L, ang, 6.5, turn, 0, 2.0, 0.3, 0.5, 0, 1.0, 1.0, 40)
        mask_ribbon(sp, lp + np.array([bx, by + 1], F32), lw, lw)
    m_s = sp.get()
    nx, ny, nz = pillow_normals(m_s, 2.5, 1.0)
    scol = lerp(hexc("3e5630"), hexc("a6c272"), lambert(nx, ny, nz, L3))
    scol = lerp(scol, hexc("f2e6b0"), rim_light(m_s, 1.5, (0.6, -0.8)) * 0.6)
    lay.over(scol, m_s)
    glow = Layer(s, s)
    g = np.clip(seams * 1.3 + gblur(seams, 2.5) * 0.7, 0, 1)
    g = np.maximum(g, inner_light * smoothstep(by - 40, by - 55, y) * 0.9)
    glow.set_straight(np.ones((s, s, 3), F32) * hexc("ffd88e"), g)
    # pivot at the stalk foot
    return lay, glow, (p[-1][0] / s, 1 - p[-1][1] / s)


def flower_cluster(seed=721, w=256, h=192, tint="rose"):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    lay = Layer(w, h)
    glow = np.zeros((h, w), F32)
    petal_cols = {"rose": (hexc("e8668e"), hexc("ffd0dc")), "gold": (hexc("f0a040"), hexc("fff0b0")),
                  "white": (hexc("e6c6d8"), hexc("ffffff"))}[tint]
    stems = MaskCanvas(w, h, 3)
    leaves = MaskCanvas(w, h, 3)
    heads = []
    for i in range(7):
        bx = w / 2 + rng.uniform(-50, 50)
        L = rng.uniform(70, 150)
        sgn = 1 if bx > w / 2 else -1
        p = tendril_pts(L, -math.pi / 2 + sgn * rng.uniform(0.1, 0.5), sgn * rng.uniform(0.0, 0.25), 2, 0, 60) \
            + np.array([bx, h - 4], F32)
        stems.taper_line(p, 4.5, 2.5)
        heads.append((p[-1], rng.uniform(9, 15)))
        for k in (12, 28):
            side = 1 if rng.random() < 0.5 else -1
            lp, lw, _ = leaf_geometry(rng.uniform(20, 36), -math.pi / 2 + side * 1.0, 8, side * 0.35, 0, 2, 0.35, 0.1,
                                      0, 1, 1, 24)
            mask_ribbon(leaves, lp + p[k], lw, lw)
    # curled buds
    for i in range(3):
        bx = w / 2 + rng.uniform(-70, 70)
        sgn = 1 if rng.random() < 0.5 else -1
        p = tendril_pts(rng.uniform(60, 100), -math.pi / 2 + sgn * 0.2, sgn * 1.2, 2.4, 0, 60) + np.array([bx, h - 4],
                                                                                                          F32)
        stems.taper_line(p, 4, 2)
    ms, ml = stems.get(), leaves.get()
    gcol = grad3([(h, hexc("6a5a2a")), (h * 0.3, hexc("c8b860"))], y)
    lay.over(gcol, ml)
    lay.over(gcol * 0.85, ms)
    for (hx, hy), r in heads:
        fp, fc = MaskCanvas(w, h, 4), MaskCanvas(w, h, 4)
        _flower(fp, fc, hx, hy, r, 5, rng.uniform(0, 6.28))
        _flower(fp, fc, hx, hy, r * 0.6, 5, rng.uniform(0, 6.28))
        mp_, mcen = fp.get(), fc.get()
        rr = np.hypot(x - hx, y - hy) / r
        lay.over(lerp(petal_cols[0], petal_cols[1], np.clip(1 - rr + 0.2, 0, 1)), mp_)
        lay.over(hexc("ffd070"), mcen)
        glow += mcen + np.exp(-(rr / 0.5) ** 2) * 0.35 * mp_
    g = Layer(w, h)
    g.set_straight(np.ones((h, w, 3), F32) * hexc("ffd890"), np.clip(glow + gblur(glow, 4) * 0.6, 0, 1))
    return lay, g


# ================================================================== build

def build(reg):
    z = ZONE
    reg.save(ground_fill(), f"{DIR}/Ground/z1_ground_fill.png", "SpriteShape fill: living rose-wood with moss", z,
             tile=("x", "y"))
    S = 51
    reg.save(ground_top(S=S), f"{DIR}/Ground/z1_ground_top.png",
             "SpriteShape top edge: lit gold moss lip with tufts, fiddleheads and rose flowers", z,
             pivot=(0.5, 1 - S / 128), tile=("x",), extra={"surfaceRow": S, "surfaceFromTopUnits": S / 64})
    R = 6
    reg.save(ground_bottom(row=R), f"{DIR}/Ground/z1_ground_bottom.png",
             "SpriteShape side/bottom edge: dark underside with hanging roots and gold drops", z,
             pivot=(0.5, 1 - R / 64), tile=("x",), extra={"surfaceRow": R})
    reg.save(sky(), f"{DIR}/Backdrop/z1_bg0_sky.png", "Parallax 0 (farthest): dawn sky, rose to gold, sun glow", z,
             tile=("x",), extra={"layer": 0, "suggestedParallax": 0.98})
    reg.save(far(), f"{DIR}/Backdrop/z1_bg1_far.png", "Parallax 1: hazy curled-leaf skyline", z, tile=("x",),
             extra={"layer": 1, "suggestedParallax": 0.9})
    reg.save(midfar(), f"{DIR}/Backdrop/z1_bg2_midfar.png", "Parallax 2: rose curled leaves and stems, gold rims", z,
             tile=("x",), extra={"layer": 2, "suggestedParallax": 0.75})
    reg.save(mid(), f"{DIR}/Backdrop/z1_bg3_mid.png", "Parallax 3: dark plum leaves, stems, glowing seed pods", z,
             tile=("x",), extra={"layer": 3, "suggestedParallax": 0.5})
    for name, fn, piv, desc in [
        ("fern", fg_fern, (0.47, 0.0), "curled fern frond rising from the bottom"),
        ("leaf_big", fg_leaf_big, (0.0, 1.0), "huge leaf reaching in from the top-left"),
        ("vines", fg_vines, (0.5, 1.0), "hanging vines with small leaves, curled ends"),
        ("grass", fg_grass, (0.5, 0.0), "clump of curling blades"),
        ("curl", fg_curl, (0.45, 0.0), "spiral tendrils with leaves"),
    ]:
        reg.save(fn(), f"{DIR}/Foreground/z1_fg_{name}.png", f"Foreground silhouette: {desc}", z, pivot=piv)
    img, glow, piv = giant_leaf()
    reg.save(img, f"{DIR}/Props/z1_prop_giant_leaf.png", "Giant curled rose leaf (16 units)", z, pivot=piv)
    reg.save(glow, f"{DIR}/Props/z1_prop_giant_leaf_glow.png", "Emissive veins and dew of the giant leaf", z,
             pivot=piv, blend="additive")
    pod, pglow, piv = seed_pod()
    reg.save(pod, f"{DIR}/Props/z1_prop_seed_pod.png", "Seed pod (attack target that bursts into light)", z,
             pivot=piv)
    reg.save(pglow, f"{DIR}/Props/z1_prop_seed_pod_glow.png", "Emissive seams of the seed pod", z, pivot=piv,
             blend="additive")
    for i, tint in enumerate(["rose", "gold", "white"]):
        fl, fg = flower_cluster(721 + i, tint=tint)
        nm = "abc"[i]
        reg.save(fl, f"{DIR}/Props/z1_prop_flowers_{nm}.png", f"Flower cluster ({tint})", z, pivot=(0.5, 0.0))
        reg.save(fg, f"{DIR}/Props/z1_prop_flowers_{nm}_glow.png", f"Emissive flower hearts ({tint})", z,
                 pivot=(0.5, 0.0), blend="additive")


# ================================================================== preview

def preview():
    from pa_common import PH, PW, backdrop, bloom, ground, hero, particles, put, save_preview
    lay = Layer(PW, PH)
    glow = np.zeros((PH, PW, 3), F32)
    gl = Layer(PW, PH)
    D = DIR + "/"
    backdrop(lay, D + "Backdrop/z1_bg0_sky.png", 180)
    backdrop(lay, D + "Backdrop/z1_bg1_far.png", 220)
    backdrop(lay, D + "Backdrop/z1_bg2_midfar.png", 520)
    backdrop(lay, D + "Backdrop/z1_bg3_mid.png", 900)
    xs = np.arange(PW, dtype=F32)
    ys = 575 + 26 * np.sin(xs / PW * 2 * math.pi * 0.9 + 0.4) + 10 * np.sin(xs / PW * 2 * math.pi * 2.3 + 1.0)
    # giant leaf behind the ground on the left
    put(lay, D + "Props/z1_prop_giant_leaf.png", 230, ys[230] + 40, 0.72)
    put(gl, D + "Props/z1_prop_giant_leaf_glow.png", 230, ys[230] + 40, 0.72, mode="add", amount=0.6)
    ground(lay, D + "Ground/z1_ground_fill.png", D + "Ground/z1_ground_top.png", 51, ys)
    # floating branch platform
    px0, px1 = 780, 1120
    t = np.clip((xs - px0) / (px1 - px0), 0, 1)
    pys = 360 - 14 * np.sin(t * math.pi)
    pyb = pys + 20 + 70 * np.clip(np.sin(t * math.pi), 0, 1) ** 0.6
    ground(lay, D + "Ground/z1_ground_fill.png", D + "Ground/z1_ground_top.png", 51, pys,
           D + "Ground/z1_ground_bottom.png", 6, pyb, px0, px1)
    for x, s in [(470, 1.0), (560, 0.8)]:
        put(lay, D + "Props/z1_prop_seed_pod.png", x, ys[x] + 6, s)
        put(gl, D + "Props/z1_prop_seed_pod_glow.png", x, ys[x] + 6, s, mode="add")
    for x, nm, s in [(130, "a", 1.0), (1000, "b", 0.9), (900, "c", 0.7), (1180, "a", 0.8)]:
        yb = (pys[x] if px0 <= x < px1 else ys[x]) + 6
        put(lay, D + f"Props/z1_prop_flowers_{nm}.png", x, yb, s)
        put(gl, D + f"Props/z1_prop_flowers_{nm}_glow.png", x, yb, s, mode="add", amount=0.7)
    hero(lay, 660, int(ys[660]) + 2)
    particles(gl, "Common/Particles/mote.png", [(930, 250), (380, 380), (700, 470)], 26, 70, (0.3, 0.9),
              PAL["gold"], 0.55, 7)
    particles(gl, "Common/Particles/pollen.png", [(640, 300), (200, 250)], 30, 160, (0.6, 1.2), hexc("fff0d0"), 0.6,
              8)
    particles(lay, "Common/Particles/petal_strip.png", [(820, 200), (300, 300)], 7, 140, (0.6, 1.0),
              hexc("ffb0c4"), 1.0, 9, mode="over", frames=3)
    shaft = "Common/Particles/light_shaft.png"
    put(gl, shaft, 640, 640, 0.8, mode="add", tint=hexc("ffe0a8"), amount=0.12)
    bloom(lay, gl.rgb, 0.7)
    put(lay, D + "Foreground/z1_fg_fern.png", 40, 900, 0.75)
    put(lay, D + "Foreground/z1_fg_vines.png", 1180, 420, 0.6)
    put(lay, D + "Foreground/z1_fg_grass.png", 1150, 790, 0.6)
    return save_preview(lay, "z1_preview.png")
