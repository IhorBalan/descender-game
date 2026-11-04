"""Celestial Garden backdrops: sky, snowy far mountains, floating islands
(midfar) and nearer cliffs with arcades (mid). 2048x1152, tileable in x."""
from __future__ import annotations

import math

from PIL import Image

from pa_core import (F32, Layer, MaskCanvas, gblur, grad3, hexc, inner_edge, lambert, lerp, np, pillow_normals,
                     rim_light, smoothstep, wrapd)
from pa_core import vnoise_fast as vnoise
from pa_garden_paint import GOLD, MARBLE, ROCK, SKY, WHITE_FLOWER, Painter, gold_shade, green_at
from pa_shapes import blob_cluster, tendril_pts

W, H = 2048, 1152
SUN = (0.80 * W, 0.11 * H)
SKY_STOPS = [(0.0, SKY["top"]), (0.30, SKY["upper"]), (0.56, SKY["mid"]), (0.80, SKY["low"]), (1.0, SKY["horizon"])]


def sky_at(yy):
    return grad3(SKY_STOPS, np.clip(np.asarray(yy, F32) / (H - 1), 0, 1))


def scaled(lay, s):
    """Premultiplied-correct resize of a Layer -> (rgb, a)."""
    rgb, a = lay.straight()
    h, w = a.shape
    nw, nh = max(1, int(round(w * s))), max(1, int(round(h * s)))
    pm = np.concatenate([rgb * a[..., None], a[..., None]], 2)
    ch = [np.asarray(Image.fromarray(pm[..., c].astype(F32), "F").resize((nw, nh), Image.LANCZOS)) for c in range(4)]
    pm = np.clip(np.stack(ch, -1), 0, 1)
    na = pm[..., 3]
    out = np.where(na[..., None] > 1e-3, pm[..., :3] / np.maximum(na[..., None], 1e-3), 0)
    return np.clip(out, 0, 1).astype(F32), na.astype(F32)


def haze(rgb, a, color, amt):
    return lerp(rgb, color, amt), a


# ================================================================== sky

def sky(seed=5001):
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    rgb = np.broadcast_to(sky_at(np.arange(H))[:, None, :], (H, W, 3)).copy()
    sx, sy = SUN
    dx, dy = wrapd(x - sx, W), y - sy
    r = np.hypot(dx, dy)
    rgb = rgb + hexc("ffe8c0") * (np.exp(-(r / 950) ** 1.4) * 0.22)[..., None]
    rgb = lerp(rgb, hexc("fff2d6"), np.exp(-(r / 260) ** 1.6) * 0.6)
    rgb = lerp(rgb, hexc("ffffff"), np.exp(-(r / 46) ** 2) * 0.95)
    # cumulus
    rng = np.random.default_rng(seed)
    mc = MaskCanvas(W, H, 2)
    specs = []
    for i in range(9):
        cx = (i + rng.uniform(0.1, 0.9)) * W / 9
        cy = rng.uniform(300, 760)
        rr = rng.uniform(60, 120) * (0.7 + 0.6 * (cy - 300) / 460)
        specs.append((cx, cy, rr))
        for off in (-W, 0, W):
            blob_cluster(mc, cx + off, cy, rr, 9, seed + i, 1.6, 0.55)
    m = mc.get()
    # flat-ish bottoms
    bottoms = np.zeros((H, W), F32)
    for cx, cy, rr in specs:
        d = np.abs(wrapd(x - cx, W))
        bottoms = np.maximum(bottoms, (d < rr * 2.2) * smoothstep(cy + rr * 0.5, cy + rr * 0.2, y))
    m = m * np.clip(bottoms + (m * 0), 0, 1)
    n = vnoise(W, H, 30, seed + 1, 3, 0.5, True, False)
    m = gblur(np.clip(m * (0.8 + 0.4 * n), 0, 1), 4, wrap_x=True)
    nx, ny, nz = pillow_normals(m, 28, 1.3, True)
    ln = r + 1e-3
    lx, ly = -dx / ln, -dy / ln
    lam = np.clip(nx * (0.6 * lx + 0.2) + ny * (0.6 * ly - 0.6) + nz * 0.45, 0, 1)
    ccol = lerp(hexc("b4c8e4"), hexc("ffffff"), np.clip(0.15 + lam, 0, 1))
    ccol = lerp(ccol, hexc("fff6e0"), rim_light(m, 8, (0.6, -0.8), True) * 0.6)
    ccol = lerp(ccol, sky_at(y), smoothstep(400, 900, y) * 0.35)
    rgb = lerp(rgb, ccol, m * 0.95)
    # god-rays from the sun, falling down-left
    th = np.arctan2(dy, dx)
    streak = (0.5 + 0.5 * np.cos(th * 13 + 0.7)) * (0.5 + 0.5 * np.cos(th * 29 + 2.1)) * \
        (0.6 + 0.4 * np.cos(th * 7))
    down = smoothstep(0.2, 1.2, th) * smoothstep(3.0, 2.0, th)
    rays = streak * down * np.exp(-(r / 820) ** 1.5) * smoothstep(50, 200, r)
    rgb = rgb + hexc("fff0cc") * (rays * 0.16)[..., None]
    L = Layer(W, H)
    L.set_straight(np.clip(rgb, 0, 1), np.ones((H, W), F32))
    return L


# ================================================================== far mountains

def _range(seed, base, amps, haze_amt, snow_off, gully_scale=1.0):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    xs = np.arange(W, dtype=F32)
    ridge = np.full(W, base, F32)
    for f, a in amps:
        ridge -= a * (1 - np.abs(np.sin(math.pi * f * xs / W + rng.uniform(0, 3.14)))) ** 1.6
    ridge += (vnoise(W, 8, 1, seed + 1, 3, 0.5, True, False, sx=40, sy=8)[4] - 0.5) * 18
    slope = (np.roll(ridge, -3) - np.roll(ridge, 3)) / 6
    face = gblur(smoothstep(-0.2, 0.3, slope)[None, :].repeat(4, 0), 6, wrap_x=True)[0]
    m = smoothstep(ridge[None, :] - 0.8, ridge[None, :] + 0.8, y)
    n = vnoise(W, H, 1, seed + 2, 4, 0.55, True, False, sx=22 * gully_scale, sy=120 * gully_scale)
    nA = np.stack([np.roll(n[r], int(0.6 * r)) for r in range(H)])
    nB = np.stack([np.roll(n[r], -int(0.6 * r)) for r in range(H)])
    g = gblur(nB + (nA - nB) * face[None, :], 2.0, wrap_x=True)
    ridged = 1 - np.abs(2 * g - 1)
    depth = y - ridge[None, :]
    shade = np.clip(0.28 + 0.55 * face[None, :] + 0.3 * (ridged - 0.5) * smoothstep(420, 60, depth), 0, 1)
    shade = gblur(shade, 1.2, wrap_x=True)
    rock = lerp(hexc("60789e"), hexc("a8b8d0"), shade)
    sl = snow_off * (0.6 + 0.8 * vnoise(W, H, 1, seed + 3, 3, 0.5, True, False, sx=90, sy=200)) + 45 * (ridged - 0.5)
    snow = smoothstep(sl + 8, sl - 8, depth)
    scol = lerp(hexc("a6bede"), hexc("fbfdff"), np.clip(shade * 1.25, 0, 1))
    rgb = lerp(rock, scol, snow)
    rgb = lerp(rgb, SKY["haze"], haze_amt + (1 - haze_amt) * smoothstep(60, 420, depth) * 0.9)
    return rgb, m


def far(seed=5101):
    lay = Layer(W, H)
    rgb, m = _range(seed, 660, [(3, 230), (5, 140), (11, 50)], 0.5, 80)
    lay.over(rgb, m)
    rgb, m = _range(seed + 10, 780, [(4, 170), (7, 90), (13, 30)], 0.36, 60, 0.8)
    lay.over(rgb, m)
    r, a = lay.straight()
    lay.set_straight(r, gblur(a, 0.7, wrap_x=True))
    return lay


# ================================================================== floating islands

def island_sprite(width, seed, temples=0, falls=1, ring=False, spire=False, tree=False):
    """A floating island: grassy top, rocky cone underside, waterfalls, small
    white buildings standing on the grass. Returns (rgb, a, top_y_in_sprite)."""
    rng = np.random.default_rng(seed)
    extra_top = 260 if ring else (190 if spire else (120 if temples else 60))
    under = width * 0.75
    fall_len = width * 1.1 if falls else 0
    w = int(width + 40)
    h = int(extra_top + under + max(fall_len, 0) + 40)
    top = extra_top
    cx = w / 2
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    lay = Layer(w, h)
    mc = MaskCanvas(w, h, 2)
    xs = np.linspace(cx - width / 2, cx + width / 2, 40)
    tl = top + 4 * np.sin(xs / 37 + seed)
    bot = []
    for xx in xs[::-1]:
        t = abs(xx - cx) / (width / 2)
        bot.append((xx, top + 16 + under * (1 - t ** 1.5) * (0.85 + 0.3 * rng.random()) * (1 - 0.15 * t)))
    mc.poly(list(zip(xs, tl)) + bot)
    m = mc.get()
    nx, ny, nz = pillow_normals(m, max(4, width * 0.05), 1.0)
    lam = lambert(nx, ny, nz, (0.5, -0.4, 0.75))
    fac = vnoise(w, h, max(4, width / 18), seed + 1, 3)
    rcol = lerp(ROCK["shade"], ROCK["light"], np.clip(lam * 0.8 + fac * 0.3 - 0.1, 0, 1))
    rcol = lerp(rcol, hexc("cfe0f2"), rim_light(m, 3, (0, 1)) * 0.5)
    strata = vnoise(w, h, 1, seed + 2, 2, 0.5, sx=width / 5, sy=3)
    rcol = rcol * (0.94 + 0.1 * strata)[..., None]
    p = Painter(w, h, 2)
    # waterfalls pour off the rim
    for k in range(falls):
        fx = cx + (rng.uniform(-0.38, 0.38)) * width
        fw = rng.uniform(5, 10) * width / 300
        L = fall_len * rng.uniform(0.7, 1.0)
        fy = top + 3
        for j in range(5):
            xx = fx + rng.uniform(-fw * 0.4, fw * 0.4)
            pts = np.stack([np.full(20, xx), np.linspace(fy, fy + L * (0.85 + 0.15 * rng.random()), 20)], 1)
            p.blade(pts, fw * rng.uniform(0.5, 0.9), fw * 1.6, hexc("ffffff"), hexc("e8f2fb"))
    lay.over(rcol, m)
    pr, pa = p.get()
    fl_ = max(fall_len, 10.0)
    fade = smoothstep(top + fl_ * 1.05, top + fl_ * 0.35, y)
    lay.blit(pr, pa * fade, 0, 0)
    # grass cap
    g = Painter(w, h, 2)
    for i in range(int(width / 3)):
        xx = rng.uniform(cx - width / 2 + 4, cx + width / 2 - 4)
        yy = top + 4 * math.sin(xx / 37 + seed)
        g.circle(xx, yy + rng.uniform(2, 8), rng.uniform(3, 7) * width / 300, green_at(0.35 + 0.45 * rng.random()))
    for i in range(int(width / 50) + 1):
        xx = rng.uniform(cx - width * 0.45, cx + width * 0.45)
        yy = top + 4 * math.sin(xx / 37 + seed)
        rr = rng.uniform(10, 22) * width / 300
        g.circle(xx, yy - rr * 0.6, rr, green_at(0.45))
        g.circle(xx + rr * 0.3, yy - rr * 0.9, rr * 0.7, green_at(0.7))
    gr, ga = g.get()
    lay.blit(gr, ga, 0, 0)
    # buildings
    b = Painter(w, h, 3)
    marble_l, marble_s = hexc("f8f3e8"), hexc("d6cdbd")
    for k in range(temples):
        tw = rng.uniform(36, 60) * width / 300
        tx = cx + rng.uniform(-0.25, 0.25) * width
        ty = top + 4 * math.sin(tx / 37 + seed) + 2
        th = tw * 0.75
        b.poly([(tx - tw / 2 - 3, ty), (tx + tw / 2 + 3, ty), (tx + tw / 2 + 3, ty - 4), (tx - tw / 2 - 3, ty - 4)],
               marble_s)
        nc = 4
        for c in range(nc):
            ccx = tx - tw / 2 + 3 + c * (tw - 6) / (nc - 1)
            b.poly([(ccx - 1.8, ty - 4), (ccx + 1.8, ty - 4), (ccx + 1.8, ty - th), (ccx - 1.8, ty - th)],
                   marble_l if c > 0 else marble_s)
        b.poly([(tx - tw / 2 - 3, ty - th), (tx + tw / 2 + 3, ty - th), (tx + tw / 2 + 3, ty - th - 5),
                (tx - tw / 2 - 3, ty - th - 5)], marble_l)
        if rng.random() < 0.5:
            b.poly([(tx - tw / 2 - 4, ty - th - 5), (tx + tw / 2 + 4, ty - th - 5), (tx, ty - th - 5 - tw * 0.3)],
                   marble_l)
        else:
            b.circle(tx, ty - th - 5, tw * 0.35, marble_l)
            b.poly([(tx - 1, ty - th - 5 - tw * 0.33), (tx + 1, ty - th - 5 - tw * 0.33), (tx, ty - th - tw * 0.6)],
                   GOLD["mid"])
    if spire:
        sx = cx + rng.uniform(-0.2, 0.2) * width
        sy = top + 2
        sw = 16 * width / 300
        b.poly([(sx - sw, sy), (sx + sw, sy), (sx + sw * 0.6, sy - 60), (sx - sw * 0.6, sy - 60)], marble_l)
        b.poly([(sx - sw * 0.75, sy - 60), (sx + sw * 0.75, sy - 60), (sx, sy - 175)], marble_l)
        b.poly([(sx, sy - 60), (sx + sw * 0.75, sy - 60), (sx, sy - 175)], marble_s)
        b.circle(sx, sy - 178, 3, GOLD["lit"])
    if ring:
        rx, ry = cx, top - 120
        R1, R0 = 110, 84
        b.poly([(rx - 26, top + 2), (rx + 26, top + 2), (rx + 18, ry + R1 - 6), (rx - 18, ry + R1 - 6)], marble_s)
        b.poly([(rx - 34, top + 2), (rx + 34, top + 2), (rx + 34, top - 8), (rx - 34, top - 8)], marble_l)
        b.circle(rx, ry, R1, marble_l)
        b.circle(rx, ry, R1 - 5, hexc("e8dcc4"))
        b.circle(rx, ry, R0 + 5, GOLD["mid"])
        b.circle(rx, ry, R0, (0, 0, 0))
    br, ba = b.get()
    if ring:
        # punch the ring's hole
        hole = np.hypot(x - cx, y - (top - 120)) < 84
        ba = np.where(hole, 0, ba)
    lay.blit(br, ba, 0, 0)
    rgb, a = lay.straight()
    return rgb, a, top


def midfar(seed=5201):
    lay = Layer(W, H)
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    rng = np.random.default_rng(seed)
    specs = [  # (x centre, top y, width, temples, falls, ring, spire, haze)
        (180, 620, 230, 1, 1, False, False, 0.55),
        (520, 470, 360, 0, 2, True, False, 0.42),
        (900, 700, 200, 0, 1, False, True, 0.55),
        (1220, 560, 300, 2, 1, False, False, 0.45),
        (1560, 760, 170, 1, 1, False, False, 0.6),
        (1820, 520, 260, 0, 2, False, True, 0.48),
        (1040, 380, 120, 0, 0, False, False, 0.62),
        (330, 330, 100, 0, 1, False, False, 0.66),
    ]
    for i, (cx, top, wd, tp, fl, ring, spire, hz) in enumerate(specs):
        rgb, a, ty = island_sprite(wd, seed + i * 7, tp, fl, ring, spire)
        rgb = lerp(rgb, SKY["haze"], hz)
        rgb = lerp(rgb, sky_at(top), 0.15)
        lay.blit(rgb, a, cx - a.shape[1] / 2, top - ty, wrap_x=True)
    r, a = lay.straight()
    lay.set_straight(r, gblur(a, 0.6, wrap_x=True))
    return lay


# ================================================================== mid: nearer cliffs with arcades

def garden_tree(height, seed):
    """A lush round garden tree (trunk + sphere-shaded leaf clumps). Returns rgb, a, foot (x, y)."""
    from pa_garden_nature import green_ramp, leafy_mass
    rng = np.random.default_rng(seed)
    h = int(height)
    w = int(height * 0.95)
    cx = w / 2
    lay = Layer(w, h)
    tm = MaskCanvas(w, h, 2)
    trunk = tendril_pts(h * 0.55, -math.pi / 2, rng.uniform(-0.05, 0.05), 1.5, 0, 30, 2.0, seed) + np.array([cx, h - 2],
                                                                                                              F32)
    tm.taper_line(trunk, h * 0.07, h * 0.035)
    for side in (-1, 1):
        bp = tendril_pts(h * 0.2, -math.pi / 2 + side * 0.7, side * 0.08, 2, 0, 20) + trunk[16]
        tm.taper_line(bp, h * 0.035, h * 0.012)
    mt = tm.get()
    nx, ny, nz = pillow_normals(mt, max(2, h * 0.012), 1.0)
    lay.over(lerp(hexc("6a5e50"), hexc("c8b89c"), lambert(nx, ny, nz, (0.5, -0.4, 0.75))), mt)
    blobs = []
    for i in range(10):
        a = rng.uniform(0, 2 * math.pi)
        rr = math.sqrt(rng.uniform(0, 1))
        blobs.append((cx + math.cos(a) * w * 0.27 * rr, h * 0.33 + math.sin(a) * h * 0.17 * rr,
                      h * rng.uniform(0.13, 0.2)))
    crgb, ca, _ = leafy_mass(w, h, blobs, green_ramp, seed + 3)
    lay.blit(crgb, ca, 0, 0)
    rgb, a = lay.straight()
    return rgb, a, (cx, h - 2)


def mid(kit, seed=5301):
    """Nearer floating cliffs joined by an aqueduct arcade, with trees, bushes,
    ivy and a waterfall. Built from the kit pieces at half scale so everything
    matches the play layer; every column stands on rock or runs down into the clouds."""
    lay = Layer(W, H)
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    s = 0.5
    islands = [(380, 640, 700, 0), (1500, 620, 760, 1)]
    tops = {}
    for i, (cx, top, wd, tp) in enumerate(islands):
        rgb, a, ty = island_sprite(wd, seed + i * 11, tp, 1, False, False)
        lay.blit(rgb, a, cx - a.shape[1] / 2, top - ty, wrap_x=True)
        tops[i] = (cx, top, wd)
    # aqueduct arcade bridging the gap
    arch_rgb, arch_a = scaled(kit["arch_span"], s)
    shaft_rgb, shaft_a = scaled(kit["column_shaft"], s)
    cap_rgb, cap_a = scaled(kit["column_capital"], s)
    walk = 628
    sp = int(6 * 64 * s)
    cols = [748, 748 + sp, 748 + 2 * sp]
    spring = walk - 8 + arch_a.shape[0]
    for cx in cols:
        for yy in range(int(spring + cap_a.shape[0]), H, shaft_a.shape[0]):
            lay.blit(shaft_rgb, shaft_a, cx - shaft_a.shape[1] / 2, yy, wrap_x=True)
        lay.blit(cap_rgb, cap_a, cx - cap_a.shape[1] / 2, spring, wrap_x=True)
    for cx in cols[:-1]:
        lay.blit(arch_rgb, arch_a, cx - 51 * s, walk - 8, wrap_x=True)
    # water spilling over the aqueduct's cornice
    wf_rgb, wf_a = scaled(kit["waterfall_tex"], 0.2)
    fx = cols[1] + sp * 0.5
    for yy in range(walk + 6, H, wf_a.shape[0]):
        lay.blit(wf_rgb, wf_a * 0.9, fx - wf_a.shape[1] / 2, yy, wrap_x=True)
    # trees, bushes and ivy on the cliffs
    bush_rgb, bush_a = scaled(kit["flower_bush"], 0.8)
    ivy_rgb, ivy_a = scaled(kit["ivy_curtain"], 0.6)
    for i, (cx, top, wd) in tops.items():
        for k in range(3):
            tx = cx + (k - 1) * wd * 0.28 + rng.uniform(-30, 30)
            th = rng.uniform(170, 250)
            trgb, ta, (fx_, fy_) = garden_tree(th, seed + 40 + i * 5 + k)
            lay.blit(trgb, ta, tx - fx_, top + 6 - fy_, wrap_x=True)
        for k in range(3):
            lay.blit(bush_rgb, bush_a, cx + rng.uniform(-0.42, 0.3) * wd, top - bush_a.shape[0] + 8, wrap_x=True)
        for ex in (cx - wd / 2 + 10, cx + wd / 2 - ivy_a.shape[1] + 10):
            lay.blit(ivy_rgb, ivy_a, ex, top + 4, wrap_x=True)
    rgb, a = lay.straight()
    rgb = lerp(rgb, lerp(SKY["haze"], SKY["mid"], 0.25), 0.40 + 0.3 * smoothstep(700, H, y))
    lay.set_straight(rgb, a)
    return lay
