"""Celestial Garden v2 wide backdrops: 8192 x 1152 (four screens), seamless in
x, with content varied along the width (nothing repeats inside the 4 screens).

In game each PNG is scaled to 0.625 (2048 px = one 20-unit screen), pinned
vertically, and scrolls at: far 0.08, midfar 0.2, mid 0.4. The visible band of
distant scenery is roughly the top 55% of the image."""
from __future__ import annotations

import math

from pa_core import (F32, Layer, MaskCanvas, gblur, grad3, hexc, lambert, lerp, np, pillow_normals, rim_light,
                     smoothstep, wrapd)
from pa_core import vnoise_fast as vnoise
from pa_garden_paint import GOLD, SKY, Painter, green_at
from pa_shapes import tendril_pts

WW, H = 8192, 1152
HAZE = hexc("d2e4f4")
HAZE_FAR = hexc("c6dcf2")


def _lay(rgb, a):
    L = Layer(a.shape[1], a.shape[0])
    L.set_straight(np.clip(rgb, 0, 1), np.clip(a, 0, 1))
    return L


def _noise1(n, scale, seed):
    return vnoise(n, 4, 1, seed, 3, 0.5, False, False, sx=scale, sy=4)[1]


# ================================================================== mountains

def peak(hl, hr, ht, seed, base_haze, haze_col=HAZE_FAR):
    """One mountain with lit (right) and shadow (left) planes, gullies, ridges and
    snowfields. Returns rgb, a, summit_x (sprite coords); sprite bottom = mountain foot."""
    rng = np.random.default_rng(seed)
    w, h = int(hl + hr + 40), int(ht + 10)
    px, top, bot = hl + 20, 8.0, float(h)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    t = np.clip((y - top) / (bot - top), 0, 1)
    ys = np.arange(h)
    nl = _noise1(h, 14, seed)[:, None]
    nr = _noise1(h, 14, seed + 1)[:, None]
    sh_l = 1 + 0.14 * np.sin(t * rng.uniform(5, 9) + rng.uniform(0, 6)) * t
    sh_r = 1 + 0.14 * np.sin(t * rng.uniform(5, 9) + rng.uniform(0, 6)) * t
    le = px - hl * t ** rng.uniform(0.75, 1.05) * sh_l * (1 + 0.22 * (nl - 0.5))
    re = px + hr * t ** rng.uniform(0.75, 1.05) * sh_r * (1 + 0.22 * (nr - 0.5))
    a = smoothstep(le - 1, le + 1, x) * smoothstep(re + 1, re - 1, x) * (y >= top)
    curve = rng.uniform(-0.25, 0.25)
    ridge = px + curve * (y - top) + 0.12 * hr * np.sin(t * 3 + rng.uniform(0, 6)) * t
    face = smoothstep(-1.5, 1.5, x - ridge)
    # ridged fractal noise sheared along each face's downslope: branching ridges and gullies
    n = vnoise(w, h, 1, seed + 2, 4, 0.55, False, False, sx=rng.uniform(9, 14), sy=rng.uniform(45, 70))
    k = rng.uniform(0.9, 1.3)
    nr_ = np.stack([np.roll(n[r], int(k * r)) for r in range(h)])
    nl_ = np.stack([np.roll(n[r], -int(k * r)) for r in range(h)])
    R = 1 - np.abs(2 * lerp(nl_, nr_, face) - 1)
    hf = gblur(R, 1.0) * 14
    gy, gx = np.gradient(hf)
    det = np.clip(0.5 + (-gx * 0.55 + gy * -0.45) * 0.9, 0, 1)
    v = R
    across = np.where(x > ridge, (x - ridge) / np.maximum(re - ridge, 1), (ridge - x) / np.maximum(ridge - le, 1))
    br_lit = 0.74 + 0.3 * (det - 0.5) + 0.12 * R - 0.06 * across
    br_sh = 0.40 + 0.2 * (det - 0.5) + 0.08 * R + 0.05 * across
    br = lerp(br_sh, br_lit, face)
    ts = rng.uniform(0.22, 0.4)
    sn = vnoise(w, h, 24, seed + 3, 3)
    snow = smoothstep(ts + 0.06, ts - 0.04, t - 0.22 * (v - 0.5) + 0.12 * (sn - 0.5))
    snow = np.maximum(snow, smoothstep(0.7, 0.9, v) * smoothstep(ts + 0.3, ts + 0.04, t) * 0.8)
    rock = lerp(hexc("485a82"), hexc("9ca4b8"), np.clip((br - 0.35) / 0.65, 0, 1))
    snowc = lerp(hexc("9cb4da"), hexc("fffaf0"), np.clip((br - 0.35) / 0.65, 0, 1))
    rgb = lerp(rock, snowc, snow)
    # sunlit snow along the right-hand silhouette
    rgb = lerp(rgb, hexc("fffaf0"), smoothstep(re - 4, re - 1, x) * smoothstep(re + 1, re - 1, x) * snow * 0.5)
    amt = base_haze + (1 - base_haze) * smoothstep(0.3, 1.0, t) * 0.95
    rgb = lerp(rgb, haze_col, amt)
    return rgb.astype(F32), a.astype(F32), px


def far_wide(seed=6101):
    lay = Layer(WW, H)
    y = np.arange(H, dtype=F32)[:, None]
    rng = np.random.default_rng(seed)
    rows = [  # (count, foot y, height range, haze)
        (30, 800, (300, 470), 0.52),
        (13, 860, (380, 600), 0.22),
    ]
    giants = {2, 7, 11}
    for r, (n, foot, (hmin, hmax), hz) in enumerate(rows):
        specs = []
        for i in range(n):
            ht = rng.uniform(hmin, hmax)
            if r == 1 and i in giants:
                ht = rng.uniform(620, 680)
            hl, hr = ht * rng.uniform(0.9, 1.5), ht * rng.uniform(0.9, 1.6)
            specs.append(((i + rng.uniform(0.15, 0.85)) * WW / n, ht, hl, hr))
        subs = []
        for (cx, ht, hl, hr) in specs:   # shoulders: lower sub-peaks beside each mountain
            for side in rng.choice([-1, 1], int(rng.integers(1, 3)), replace=False):
                sh = ht * rng.uniform(0.5, 0.78)
                subs.append((cx + side * rng.uniform(0.35, 0.65) * (hr if side > 0 else hl), sh, sh * rng.uniform(0.9, 1.4),
                             sh * rng.uniform(0.9, 1.4)))
        specs = subs + specs
        specs.sort(key=lambda s: s[1])
        for k, (cx, ht, hl, hr) in enumerate(specs):
            rgb, a, px = peak(hl, hr, ht, seed + r * 100 + k, hz)
            lay.blit(rgb, a, cx - px, foot - a.shape[0], wrap_x=True)
    # mist pooling at the feet, opaque below
    rgb, a = lay.straight()
    mist = smoothstep(600, 860, y)
    rgb = lerp(rgb, HAZE, mist * 0.85)
    a = np.maximum(a, smoothstep(780, 880, y) * np.ones_like(a))
    rgb = np.where(a[..., None] > 1e-3, rgb, HAZE)
    lay.set_straight(rgb, gblur(a, 0.6, wrap_x=True))
    return lay


# ================================================================== distant architecture

MLIT, MSH, MDARK = hexc("fbf6ea"), hexc("cfc6b8"), hexc("8e8c96")


def draw_tholos(p, x, y, s):
    """Round temple: steps, colonnade, entablature, dome, gold lantern. (x,y) = ground centre, s = scale px."""
    for i, wd in enumerate((2.7, 2.5, 2.3)):
        yy = y - i * 0.12 * s
        p.poly([(x - wd / 2 * s, yy), (x + wd / 2 * s, yy), (x + wd / 2 * s, yy - 0.12 * s), (x - wd / 2 * s, yy - 0.12 * s)],
               MSH if i % 2 else MLIT)
    yb = y - 0.36 * s
    ch = 1.15 * s
    p.poly([(x - 1.0 * s, yb), (x + 1.0 * s, yb), (x + 1.0 * s, yb - ch), (x - 1.0 * s, yb - ch)], MDARK)
    for k in range(6):
        u = -1 + 2 * (k + 0.5) / 6
        cx = x + math.sin(u * 1.2) / math.sin(1.2) * 1.0 * s
        cw = 0.15 * s * (0.6 + 0.4 * math.cos(u * 1.2))
        p.poly([(cx - cw / 2, yb), (cx + cw / 2, yb), (cx + cw / 2, yb - ch), (cx - cw / 2, yb - ch)], MSH)
        p.poly([(cx, yb), (cx + cw / 2, yb), (cx + cw / 2, yb - ch), (cx, yb - ch)], MLIT)
    ye = yb - ch
    p.poly([(x - 1.15 * s, ye), (x + 1.15 * s, ye), (x + 1.15 * s, ye - 0.24 * s), (x - 1.15 * s, ye - 0.24 * s)], MLIT)
    p.line([(x - 1.15 * s, ye - 0.08 * s), (x + 1.15 * s, ye - 0.08 * s)], max(1, 0.05 * s), GOLD["mid"])
    yd = ye - 0.24 * s
    p.poly([(x - 0.9 * s, yd), (x + 0.9 * s, yd), (x + 0.9 * s, yd - 0.22 * s), (x - 0.9 * s, yd - 0.22 * s)], MSH)
    yd -= 0.22 * s
    th = np.linspace(0, math.pi, 30)
    dome = [(x + math.cos(t_) * 0.92 * s, yd - math.sin(t_) * 0.78 * s) for t_ in th]
    p.poly(dome, MSH)
    lit = [(x + 0.08 * s + math.cos(t_) * 0.8 * s * (0.4 + 0.6 * (t_ < math.pi / 2)), yd - math.sin(t_) * 0.72 * s)
           for t_ in th[:18]] + [(x + 0.1 * s, yd)]
    p.poly(lit, MLIT)
    p.poly([(x - 0.1 * s, yd - 0.76 * s), (x + 0.1 * s, yd - 0.76 * s), (x + 0.1 * s, yd - 0.98 * s), (x - 0.1 * s, yd - 0.98 * s)],
           MLIT)
    p.poly([(x - 0.12 * s, yd - 0.98 * s), (x + 0.12 * s, yd - 0.98 * s), (x, yd - 1.25 * s)], GOLD["mid"])


def draw_temple(p, x, y, s):
    """Rectangular temple: steps, five columns, architrave, pediment roof (lit right half)."""
    for i, wd in enumerate((3.0, 2.8, 2.6)):
        yy = y - i * 0.12 * s
        p.poly([(x - wd / 2 * s, yy), (x + wd / 2 * s, yy), (x + wd / 2 * s, yy - 0.12 * s), (x - wd / 2 * s, yy - 0.12 * s)],
               MSH if i % 2 else MLIT)
    yb = y - 0.36 * s
    ch = 1.1 * s
    p.poly([(x - 1.15 * s, yb), (x + 1.15 * s, yb), (x + 1.15 * s, yb - ch), (x - 1.15 * s, yb - ch)], MDARK)
    for k in range(5):
        cx = x - 1.05 * s + k * 2.1 * s / 4
        cw = 0.17 * s
        p.poly([(cx - cw / 2, yb), (cx + cw / 2, yb), (cx + cw / 2, yb - ch), (cx - cw / 2, yb - ch)], MSH)
        p.poly([(cx, yb), (cx + cw / 2, yb), (cx + cw / 2, yb - ch), (cx, yb - ch)], MLIT)
    ye = yb - ch
    p.poly([(x - 1.3 * s, ye), (x + 1.3 * s, ye), (x + 1.3 * s, ye - 0.26 * s), (x - 1.3 * s, ye - 0.26 * s)], MLIT)
    p.line([(x - 1.3 * s, ye - 0.1 * s), (x + 1.3 * s, ye - 0.1 * s)], max(1, 0.05 * s), GOLD["mid"])
    yr = ye - 0.26 * s
    p.poly([(x - 1.4 * s, yr), (x, yr - 0.62 * s), (x, yr)], MSH)
    p.poly([(x, yr), (x, yr - 0.62 * s), (x + 1.4 * s, yr)], MLIT)
    p.poly([(x - 1.0 * s, yr - 0.04 * s), (x, yr - 0.46 * s), (x + 1.0 * s, yr - 0.04 * s)], hexc("e4dccb"))
    for ax in (x - 1.4 * s, x, x + 1.4 * s):
        p.circle(ax, (yr - 0.62 * s - 0.05 * s) if ax == x else yr - 0.05 * s, 0.07 * s, GOLD["mid"])


def draw_spire(p, x, y, s):
    """A tall white tower: base with arched door, tapering shaft with bands and
    windows, open belfry, pointed roof, gold finial."""
    def block(x0, x1, y0, y1, split=0.55):
        p.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], MSH)
        xm = x0 + (x1 - x0) * split
        p.poly([(xm, y0), (x1, y0), (x1, y1), (xm, y1)], MLIT)
    block(x - 0.6 * s, x + 0.6 * s, y, y - 1.3 * s)
    p.poly([(x - 0.18 * s, y), (x + 0.18 * s, y), (x + 0.18 * s, y - 0.45 * s), (x, y - 0.6 * s), (x - 0.18 * s, y - 0.45 * s)],
           MDARK)
    yy = y - 1.3 * s
    for k in range(3):
        w0 = (0.5 - k * 0.06) * s
        block(x - w0, x + w0, yy, yy - 1.1 * s)
        p.line([(x - w0 - 0.04 * s, yy), (x + w0 + 0.04 * s, yy)], max(1, 0.07 * s), MLIT)
        p.poly([(x + 0.05 * s, yy - 0.35 * s), (x + 0.17 * s, yy - 0.35 * s), (x + 0.17 * s, yy - 0.7 * s),
                (x + 0.11 * s, yy - 0.78 * s), (x + 0.05 * s, yy - 0.7 * s)], MDARK)
        yy -= 1.1 * s
    w0 = 0.36 * s
    block(x - w0, x + w0, yy, yy - 0.7 * s)
    for ax in (x - 0.17 * s, x + 0.12 * s):
        p.poly([(ax - 0.08 * s, yy - 0.1 * s), (ax + 0.08 * s, yy - 0.1 * s), (ax + 0.08 * s, yy - 0.5 * s),
                (ax, yy - 0.58 * s), (ax - 0.08 * s, yy - 0.5 * s)], MDARK)
    yy -= 0.7 * s
    p.line([(x - w0 - 0.06 * s, yy), (x + w0 + 0.06 * s, yy)], max(1, 0.08 * s), GOLD["mid"])
    p.poly([(x - 0.42 * s, yy), (x, yy - 1.9 * s), (x, yy)], MSH)
    p.poly([(x, yy), (x, yy - 1.9 * s), (x + 0.42 * s, yy)], MLIT)
    p.circle(x, yy - 1.95 * s, 0.09 * s, GOLD["lit"])
    p.line([(x, yy - 1.95 * s), (x, yy - 2.25 * s)], max(1, 0.04 * s), GOLD["mid"])


def draw_ring_gate(p, x, y, R):
    """The ring gate monument standing on a stepped pedestal."""
    for i, (wd, hh) in enumerate([(1.0, 0.1), (0.85, 0.1), (0.5, 0.42)]):
        y0 = y - sum(h_ for _, h_ in [(1.0, 0.1), (0.85, 0.1), (0.5, 0.42)][:i]) * R
        p.poly([(x - wd * R / 2, y0), (x + wd * R / 2, y0), (x + wd * R / 2 * 0.9, y0 - hh * R), (x - wd * R / 2 * 0.9, y0 - hh * R)],
               MSH)
        p.poly([(x, y0), (x + wd * R / 2, y0), (x + wd * R / 2 * 0.9, y0 - hh * R), (x, y0 - hh * R)], MLIT)
    cy = y - 0.62 * R - R
    p.circle(cx := x, cy, R, MSH)
    p.circle(cx + R * 0.05, cy - R * 0.03, R * 0.97, MLIT)
    p.circle(cx, cy, R * 0.84, GOLD["mid"])
    p.circle(cx - R * 0.02, cy + R * 0.01, R * 0.8, hexc("d9cfbd"))
    return cx, cy, R * 0.74


def small_tree(p, x, y, s, rng):
    p.line([(x, y), (x + rng.uniform(-0.1, 0.1) * s, y - 0.9 * s)], max(1, 0.12 * s), hexc("7e6e5c"))
    for k in range(5):
        cx = x + rng.uniform(-0.45, 0.45) * s
        cy = y - 1.1 * s + rng.uniform(-0.35, 0.25) * s
        r = rng.uniform(0.35, 0.55) * s
        p.circle(cx, cy, r, green_at(0.3))
        p.circle(cx + r * 0.22, cy - r * 0.2, r * 0.75, green_at(0.6))
        p.circle(cx + r * 0.38, cy - r * 0.36, r * 0.35, green_at(0.85))


# ================================================================== islands

ROCK_TEX = {}


def set_rock_texture(rgb):
    ROCK_TEX["rgb"] = rgb


def _rock_tex(w, h, scale, seed):
    from PIL import Image
    key = round(scale, 3)
    if key not in ROCK_TEX:
        src = ROCK_TEX["rgb"]
        s = max(16, int(src.shape[0] * scale))
        ch = [np.asarray(Image.fromarray(src[..., c].astype(F32), "F").resize((s, s), Image.BILINEAR)) for c in range(3)]
        ROCK_TEX[key] = np.stack(ch, -1)
    t = ROCK_TEX[key]
    rng = np.random.default_rng(seed)
    t = np.roll(t, (int(rng.integers(0, t.shape[0])), int(rng.integers(0, t.shape[1]))), (0, 1))
    reps = (h // t.shape[0] + 1, w // t.shape[1] + 1, 1)
    return np.tile(t, reps)[:h, :w]


def island(width, seed, feats):
    """A floating island sprite: rock underside with lit and shadow planes,
    grass cap, trees, buildings, waterfalls with mist. Returns rgb, a, top_y."""
    rng = np.random.default_rng(seed)
    extra = 120
    if feats.get("ring"):
        extra = max(extra, width * 0.75)
    if feats.get("spire"):
        extra = max(extra, 360)
    if feats.get("temples"):
        extra = max(extra, 190)
    under = width * feats.get("under", 0.7)
    falls = feats.get("falls", 0)
    fall_len = width * 1.3 if falls else 0
    w = int(width + 80)
    h = int(extra + under + fall_len + 40)
    top = extra
    cx = w / 2
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    mc = MaskCanvas(w, h, 2)
    xs = np.linspace(cx - width / 2, cx + width / 2, 60)
    tl = top + 3 * np.sin(xs / 31 + seed)
    bot = []
    pw = rng.uniform(1.1, 3.2)
    off = rng.uniform(-0.25, 0.25)
    teeth = rng.uniform(0, 1, 60) ** 3
    for j, xx in enumerate(xs[::-1]):
        t = np.clip(abs((xx - cx) / (width / 2) - off) / (1 + abs(off)), 0, 1)
        d = under * (1 - t ** pw) * (0.85 + 0.2 * rng.random()) + teeth[j] * under * 0.3 * (1 - t)
        bot.append((xx, top + 12 + d))
    mc.poly(list(zip(xs, tl)) + bot)
    m = mc.get()
    u = (x - cx) / (width / 2)
    streak = vnoise(w, h, 1, seed + 1, 3, 0.5, sx=max(6, width / 14), sy=max(20, width / 3))
    plane = smoothstep(-0.25, 0.35, u + 0.35 * (streak - 0.5))
    depth = np.clip((y - top) / max(1, under), 0, 1)
    br = 0.55 + 0.45 * plane + 0.1 * (streak - 0.5) - 0.22 * depth
    tex = _rock_tex(w, h, feats.get("tex_scale", float(np.clip(width / 2400, 0.12, 0.5))), seed)
    rcol = lerp(lerp(hexc("8e94a6"), hexc("ece0cc"), np.clip(br, 0, 1)), tex * br[..., None] * 1.05, 0.45)
    rcol = lerp(rcol, hexc("7c86a0"), smoothstep(0.6, 0.35, br) * 0.35)
    rcol = lerp(rcol, hexc("cfe0f2"), rim_light(m, 3, (0, 1)) * 0.5)
    mossy = smoothstep(top + max(8, width * 0.12), top + 2, y) * smoothstep(0.35, 0.7, streak)
    rcol = lerp(rcol, green_at(0.35) * (0.8 + 0.3 * plane)[..., None], mossy * 0.7)
    lay = Layer(w, h)
    lay.over(rcol, m)
    p = Painter(w, h, 3)
    for k in range(falls):     # waterfalls with mist at their ends
        fx = cx + rng.uniform(-0.38, 0.38) * width
        fw = max(3.0, rng.uniform(5, 9) * width / 300)
        L = fall_len * rng.uniform(0.6, 1.0)
        for j in range(5):
            xx = fx + rng.uniform(-fw * 0.4, fw * 0.4)
            pts = np.stack([np.full(20, xx), np.linspace(top + 2, top + L * (0.85 + 0.15 * rng.random()), 20)], 1)
            p.blade(pts, fw * rng.uniform(0.5, 0.9), fw * 1.8, hexc("ffffff"), hexc("e4eef8"))
    pr, pa = p.get()
    fl_ = max(fall_len, 10.0)
    lay.blit(pr, pa * smoothstep(top + fl_ * 1.02, top + fl_ * 0.3, y), 0, 0)
    g = Painter(w, h, 3)
    s = max(8.0, width / 18)
    for i in range(int(width / 3)):
        xx = rng.uniform(cx - width / 2 + 3, cx + width / 2 - 3)
        yy = top + 3 * math.sin(xx / 31 + seed)
        g.circle(xx, yy + rng.uniform(1, 6), rng.uniform(2.5, 6) * width / 300 + 1.5, green_at(0.3 + 0.5 * rng.random()))
    blds = []
    for kind in feats.get("temples", []):
        blds.append(kind)
    for i in range(feats.get("trees", 0)):
        xx = cx + rng.uniform(-0.42, 0.42) * width
        small_tree(g, xx, top + 3 * math.sin(xx / 31 + seed) + 2, s * rng.uniform(0.9, 1.4), rng)
    hole = None
    nb = len(blds)
    for i, kind in enumerate(blds):
        xx = cx + ((i - (nb - 1) / 2) * 0.32 + rng.uniform(-0.04, 0.04)) * width
        yy = top + 3 * math.sin(xx / 31 + seed) + 3
        sc = feats.get("bscale", s * 1.3) * (1.0 if kind == "templum" else 0.9)
        (draw_temple if kind == "templum" else draw_tholos)(g, xx, yy, sc)
    if feats.get("spire"):
        xx = cx + width * 0.08
        draw_spire(g, xx, top + 3, feats.get("bscale", s * 1.25))
    if feats.get("ring"):
        hole = draw_ring_gate(g, cx, top + 3, width * 0.32)
    for i in range(int(width / 40)):     # bushes and flowers in front
        xx = cx + rng.uniform(-0.45, 0.45) * width
        yy = top + 3 * math.sin(xx / 31 + seed) + 4
        r = rng.uniform(0.25, 0.45) * s
        g.circle(xx, yy - r * 0.5, r, green_at(0.35))
        g.circle(xx + r * 0.25, yy - r * 0.75, r * 0.65, green_at(0.7))
        if rng.random() < 0.6:
            g.circle(xx + r * 0.1, yy - r * 1.1, max(1.0, r * 0.18), hexc("ffffff"))
    gr, ga = g.get()
    if hole is not None:
        hx, hy, hr = hole
        ga = ga * smoothstep(hr - 0.8, hr + 0.8, np.hypot(x - hx, y - hy))
    lay.blit(gr, ga, 0, 0)
    rgb, a = lay.straight()
    return rgb, a, top


ISLANDS = [  # x, top, width, features, haze
    (300, 520, 260, {"trees": 3, "falls": 1}, 0.5),
    (840, 380, 150, {"trees": 1, "falls": 1}, 0.58),
    (1380, 600, 420, {"temples": ["templum"], "falls": 2, "trees": 4, "under": 0.5}, 0.4),
    (1930, 470, 200, {"trees": 2}, 0.52),
    (2620, 520, 470, {"ring": True, "falls": 2, "trees": 3, "under": 0.55}, 0.4),
    (3250, 600, 240, {"trees": 2, "falls": 1}, 0.48),
    (3700, 330, 120, {"falls": 1, "trees": 1}, 0.6),
    (4220, 560, 330, {"temples": ["tholos"], "falls": 1, "trees": 2}, 0.44),
    (4820, 450, 190, {"trees": 2, "falls": 1}, 0.52),
    (5420, 520, 600, {"temples": ["tholos", "templum", "tholos"], "falls": 3, "trees": 5, "under": 0.6}, 0.38),
    (6080, 640, 220, {"trees": 3}, 0.5),
    (6560, 400, 160, {"falls": 1, "trees": 1}, 0.56),
    (7060, 560, 300, {"spire": True, "falls": 1, "trees": 2}, 0.42),
    (7600, 470, 200, {"trees": 2, "falls": 1}, 0.5),
    (7990, 620, 140, {"falls": 1}, 0.55),
]


def midfar_wide(seed=6201):
    lay = Layer(WW, H)
    for i, (cx, top, wd, feats, hz) in enumerate(ISLANDS):
        rgb, a, ty = island(wd, seed + i * 7, feats)
        rgb = lerp(rgb, HAZE, hz)
        lay.blit(rgb, a, cx - a.shape[1] / 2, top - ty, wrap_x=True)
    r, a = lay.straight()
    lay.set_straight(r, gblur(a, 0.5, wrap_x=True))
    return lay


# ================================================================== mid

def mid_wide(kit, seed=6301):
    """Nearer cliffs, arcades and trees with breaks of open sky; built from the
    kit at half scale (columns stand on rock or run down into the clouds)."""
    from pa_garden_env import garden_tree, scaled
    lay = Layer(WW, H)
    rng = np.random.default_rng(seed)
    y = np.arange(H, dtype=F32)[:, None]
    s = 0.5
    arch_rgb, arch_a = scaled(kit["arch_span"], s)
    shaft_rgb, shaft_a = scaled(kit["column_shaft"], s)
    cap_rgb, cap_a = scaled(kit["column_capital"], s)
    base_rgb, base_a = scaled(kit["column_base"], s)
    bush_rgb, bush_a = scaled(kit["flower_bush"], 0.8)
    ivy_rgb, ivy_a = scaled(kit["ivy_curtain"], 0.6)
    wf_rgb, wf_a = scaled(kit["waterfall_tex"], 0.2)
    broken_rgb, broken_a = scaled(kit["broken_column"], 0.55)
    bt_rgb, bt_a = scaled(kit["blossom_tree"], 0.42)
    sp = int(6 * 64 * s)

    def arcade(x0, n_arches, walk, on_ground=None):
        cols = [x0 + k * sp for k in range(n_arches + 1)]
        spring = walk - 8 + arch_a.shape[0]
        for cx in cols:
            yy = spring + cap_a.shape[0]
            stop = on_ground if on_ground is not None else H
            while yy < stop:
                hh = int(min(shaft_a.shape[0], stop - yy))
                lay.blit(shaft_rgb[:hh], shaft_a[:hh], cx - shaft_a.shape[1] / 2, yy, wrap_x=True)
                yy += hh
            if on_ground is not None:
                lay.blit(base_rgb, base_a, cx - base_a.shape[1] / 2, on_ground - base_a.shape[0] + 4, wrap_x=True)
            lay.blit(cap_rgb, cap_a, cx - cap_a.shape[1] / 2, spring, wrap_x=True)
        for cx in cols[:-1]:
            lay.blit(arch_rgb, arch_a, cx - 51 * s, walk - 8, wrap_x=True)
        return cols

    def waterfall(fx, y0):
        for yy in range(int(y0), H, wf_a.shape[0]):
            lay.blit(wf_rgb, wf_a * 0.9, fx - wf_a.shape[1] / 2, yy, wrap_x=True)

    def dress(cx, top, wd, n_trees, blossom=0, ruins=0):
        for k in range(n_trees):
            tx = cx + ((k + 0.5) / n_trees - 0.5) * wd * 0.85 + rng.uniform(-25, 25)
            trgb, ta, (fx_, fy_) = garden_tree(rng.uniform(170, 260), seed + int(tx))
            lay.blit(trgb, ta, tx - fx_, top + 6 - fy_, wrap_x=True)
        for k in range(blossom):
            tx = cx + rng.uniform(-0.3, 0.3) * wd
            lay.blit(bt_rgb, bt_a, tx - bt_a.shape[1] / 2, top + 4 - bt_a.shape[0], wrap_x=True)
        for k in range(ruins):
            tx = cx + rng.uniform(-0.35, 0.35) * wd
            lay.blit(broken_rgb, broken_a, tx - broken_a.shape[1] / 2, top + 4 - broken_a.shape[0], wrap_x=True)
        for k in range(max(2, int(wd / 260))):
            lay.blit(bush_rgb, bush_a, cx + rng.uniform(-0.45, 0.35) * wd, top - bush_a.shape[0] + 8, wrap_x=True)
        for ex in (cx - wd / 2 + 8, cx + wd / 2 - ivy_a.shape[1] + 8):
            lay.blit(ivy_rgb, ivy_a, ex, top + 3, wrap_x=True)

    def cliff(cx, top, wd, sd, feats=None):
        rgb, a, ty = island(wd, sd, feats or {"under": 0.9})
        lay.blit(rgb, a, cx - a.shape[1] / 2, top - ty, wrap_x=True)

    # A: big cliff, arcade to B, waterfall
    cliff(700, 520, 1000, seed + 1)
    cliff(2050, 545, 560, seed + 2)
    arcade(1215, 3, 532)
    waterfall(1215 + sp * 1.5, 540)
    dress(700, 520, 1000, 4, blossom=1, ruins=1)
    dress(2050, 545, 560, 2)
    # (2400-3300: open sky)
    # C: high cliff with a small arcade standing on it, waterfall off its right edge
    cliff(4100, 470, 1300, seed + 3)
    arcade(3700, 2, 380, on_ground=474)
    dress(4300, 470, 900, 3, blossom=1)
    waterfall(4100 + 600, 476)
    # lone ruined arch rising out of the clouds
    arcade(5150, 1, 600)
    # D: cliff with trees and ruins
    cliff(6500, 530, 900, seed + 4)
    dress(6500, 530, 900, 4, ruins=1)
    waterfall(6500 - 380, 536)
    # E: small island
    cliff(7620, 600, 320, seed + 5)
    dress(7620, 600, 320, 1, blossom=1)
    rgb, a = lay.straight()
    rgb = lerp(rgb, lerp(HAZE, SKY["mid"], 0.2), 0.26 + 0.3 * smoothstep(640, H, y))
    lay.set_straight(rgb, a)
    return lay
