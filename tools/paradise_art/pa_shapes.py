"""Reusable organic shapes: shaded leaves with curled tips, fern fronds,
spiral tendrils, round canopies, and the particle sprites."""
from __future__ import annotations

import math

from pa_core import (F32, Image, Layer, MaskCanvas, RibbonCanvas, at_ts, euler_spine, frame, gblur, glow_disc,
                     grad3, inner_edge, lambert, leaf_width, lerp, np, pillow_normals, smoothstep, turns_k1, vnoise)


# ------------------------------------------------------------------ geometry helpers

def leaf_geometry(L, theta0, width, turns=0.0, k0=0.0, p=3.0, peak=0.3, base=0.12, tipw=0.0, fall=1.0,
                  asym=1.0, n=360, wobble=0.0, seed=0, curl_narrow=0.55):
    """Spine + half widths of a leaf whose tip may coil into a spiral."""
    k1 = turns_k1(turns, L, p) if turns else 0.0
    pts = euler_spine(L, theta0, k0, k1, p, n, wobble, seed)
    t = np.linspace(0, 1, n, dtype=F32)
    w = leaf_width(t, base, peak, tipw, fall) * width
    if turns:
        # narrow the coil so it does not swallow itself
        w *= 1 - curl_narrow * smoothstep(0.55, 1.0, t)
    return pts, w * asym, w


def bbox_of(pts, hwl, hwr, pad=8):
    _, nrm = frame(pts)
    L = pts + nrm * hwl[:, None]
    R = pts - nrm * hwr[:, None]
    allp = np.concatenate([L, R, pts], 0)
    x0, y0 = allp.min(0) - pad
    x1, y1 = allp.max(0) + pad
    return float(x0), float(y0), float(x1), float(y1)


# ------------------------------------------------------------------ leaf

def shade_leaf(mp, pal, light3=(0.5, -0.65, 0.6), curl_from=None, seed=0, veins=None, glow_veins=0.0,
               tex=0.08):
    """Colour a leaf from its ribbon maps. pal keys: base, mid, tip, under, rim, vein, midrib, dark."""
    m, t, s, f = mp["m"], mp["t"], mp["s"], mp["f"]
    h, w = m.shape
    a_s = np.abs(s)
    col = grad3([(0.0, pal["base"]), (0.45, pal["mid"]), (1.0, pal["tip"])], t)
    # V-fold: the half facing the light is brighter, the far edge of the shadow half darker
    fold = smoothstep(0.0, 0.3, a_s)
    shade = 0.84 + 0.26 * f * fold
    shade *= 1 - 0.28 * np.clip(-f, 0, 1) * a_s ** 2
    # cupping: a soft darker valley near the midrib on both halves
    shade *= 1 - 0.10 * np.exp(-((a_s - 0.12) / 0.08) ** 2)
    col = col * shade[..., None]
    if curl_from is not None:
        u = smoothstep(curl_from, curl_from + 0.12, t)
        col = lerp(col, pal["under"] * (0.8 + 0.25 * np.clip(f, -1, 1))[..., None], u * 0.85)
    # midrib
    rib = np.exp(-(s / 0.045) ** 2) * (1 - t) ** 0.6
    col = lerp(col, pal["midrib"], rib * 0.7)
    # texture
    if tex:
        nz = vnoise(w, h, max(6, w / 40), seed + 7, 3)
        col = col * (1 - tex + 2 * tex * nz)[..., None]
    # veins (drawn lines in leaf space)
    if veins is not None:
        col = lerp(col, pal["vein"], np.clip(veins, 0, 1) * 0.45)
    # rim light on the edge that faces the light, dark lip on the other
    edge = smoothstep(0.80, 0.98, a_s)
    edge = np.maximum(edge, inner_edge(m, 2.0) * 0.8)
    col = lerp(col, pal["rim"], edge * smoothstep(0.0, 0.6, f) * 0.9)
    col = lerp(col, pal["dark"], edge * smoothstep(0.0, 0.6, -f) * 0.5)
    return np.clip(col, 0, 1)


def vein_mask(w, h, pts, hwl, hwr, ox, oy, count=9, slope=0.16, width=3.0, ss=2, seed=0, t_end=0.9):
    """Secondary veins: curves from the midrib towards the margin and tip."""
    mc = MaskCanvas(w, h, ss)
    p = pts + np.array([ox, oy], F32)
    rng = np.random.default_rng(seed)
    # midrib
    tt = np.linspace(0.0, t_end, 80)
    mid = at_ts(p, hwl, hwr, tt, np.zeros_like(tt))
    mc.taper_line(mid, width * 1.6, width * 0.4)
    for j in range(count):
        t0 = 0.06 + (t_end - 0.12) * (j + rng.random() * 0.4) / count
        for side in (-1, 1):
            u = np.linspace(0, 1, 24)
            tv = t0 + slope * (u ** 1.4) * (1 + 0.3 * rng.random())
            sv = side * (0.05 + 0.85 * u)
            q = at_ts(p, hwl, hwr, np.clip(tv, 0, 1), sv)
            mc.taper_line(q, width, 0.5, 235, 40)
    return mc.get()


def render_leaf(L, theta0, width, pal, turns=0.0, k0=0.0, p=3.0, peak=0.3, base=0.12, tipw=0.0, fall=1.0,
                ss=2, K=10, veins=9, vein_w=3.0, seed=0, wobble=0.0, light=(0.55, -0.83), curl_narrow=0.55,
                tex=0.08, stem=0.0, stem_w=None):
    """A complete shaded leaf sprite. Returns (rgb, alpha, anchor_xy) where
    anchor is the leaf base in sprite pixels."""
    pts, hwl, hwr = leaf_geometry(L, theta0, width, turns, k0, p, peak, base, tipw, fall, 1.0, 360, wobble, seed,
                                  curl_narrow)
    if stem:
        # a stem extends backwards from the base
        sp = euler_spine(stem, theta0 + math.pi, 0.0, 0.0, 1, 40)
        stem_pts = sp[::-1]
    x0, y0, x1, y1 = bbox_of(pts, hwl, hwr, 10)
    if stem:
        x0 = min(x0, stem_pts[:, 0].min() - 10)
        y0 = min(y0, stem_pts[:, 1].min() - 10)
        x1 = max(x1, stem_pts[:, 0].max() + 10)
        y1 = max(y1, stem_pts[:, 1].max() + 10)
    w, h = int(math.ceil(x1 - x0)), int(math.ceil(y1 - y0))
    ox, oy = -x0, -y0
    rc = RibbonCanvas(w, h, ss, light)
    if stem:
        sw = stem_w or max(3.0, width * 0.08)
        q = stem_pts + np.array([ox, oy], F32)
        rc.ribbon(q, sw, sw, K=4, part=1)
    rc.ribbon(pts + np.array([ox, oy], F32), hwl, hwr, K=K, part=0)
    mp = rc.maps(1.5)
    vm = vein_mask(w, h, pts, hwl, hwr, ox, oy, veins, 0.16, vein_w, ss, seed) * mp["m"] if veins else None
    curl_from = 0.62 if turns else None
    col = shade_leaf(mp, pal, curl_from=curl_from, seed=seed, veins=vm, tex=tex)
    if stem:
        col = np.where((mp["part"] > 0.5)[..., None], lerp(pal["base"] * 0.8, pal["rim"], np.clip(mp["f"], 0, 1) * 0.4), col)
    return col.astype(F32), mp["m"], (ox, oy), vm


def render_silhouette_leaf(L, theta0, width, turns=0.0, k0=0.0, p=3.0, peak=0.3, base=0.1, ss=2, seed=0, wobble=0.0,
                           curl_narrow=0.55, fall=1.0, tipw=0.0):
    """Mask-only leaf (plus its maps) for silhouettes."""
    pts, hwl, hwr = leaf_geometry(L, theta0, width, turns, k0, p, peak, base, tipw, fall, 1.0, 300, wobble, seed,
                                  curl_narrow)
    return pts, hwl, hwr


def tendril_pts(L, theta0, turns=1.6, p=2.5, k0=0.0, n=200, wobble=0.0, seed=0):
    return euler_spine(L, theta0, k0, turns_k1(turns, L, p), p, n, wobble, seed)


def fern_frond(rc_or_mc, x, y, L, theta0, turns=1.2, k0=0.0, stem_w=10.0, pinna_len=None, pinnae=22, seed=0,
               p=3.0, wobble=0.0, part=0.0, ribbon=True, pinna_curl=0.6):
    """A fern frond with a coiled fiddlehead tip, pinnae down both sides.
    Draws into a RibbonCanvas (ribbon=True) or a MaskCanvas."""
    rng = np.random.default_rng(seed)
    pts = euler_spine(L, theta0, k0, turns_k1(turns, L, p), p, 300, wobble, seed) + np.array([x, y], F32)
    tang, nrm = frame(pts)
    t = np.linspace(0, 1, len(pts))
    sw = stem_w * (1 - 0.8 * t)
    pl = pinna_len or L * 0.22
    drawn = []
    for i in range(pinnae):
        tt = 0.08 + 0.72 * i / pinnae
        k = int(tt * (len(pts) - 1))
        scale = math.sin(math.pi * min(1.0, (tt - 0.02) / 0.85)) ** 0.7 * (1 - 0.55 * tt)
        for side in (1, -1):
            ang_base = math.atan2(nrm[k][1] * side, nrm[k][0] * side)
            ta = math.atan2(tang[k][1], tang[k][0])
            # sweep pinnae forward toward the tip
            d = (ta - ang_base + math.pi) % (2 * math.pi) - math.pi
            ang = ang_base + d * 0.38
            ln = pl * scale * (0.85 + 0.3 * rng.random())
            if ln < 6:
                continue
            curl = side * -1 * pinna_curl * (1 if d > 0 else -1)
            pp = euler_spine(ln, ang, 0.0, turns_k1(abs(curl) * 0.25, ln, 2.0) * np.sign(curl), 2.0, 40)
            pp = pp + pts[k]
            pw = leaf_width(np.linspace(0, 1, 40), 0.35, 0.25, 0.0, 0.9) * ln * 0.16
            drawn.append((pp, pw))
    if ribbon:
        for pp, pw in drawn:
            rc_or_mc.ribbon(pp, pw, pw, K=4, part=part + 1, step=1)
        rc_or_mc.ribbon(pts, sw, sw, K=4, part=part)
    else:
        for pp, pw in drawn:
            q = np.concatenate([at_ts(pp, pw, pw, np.linspace(0, 1, 30), np.ones(30)),
                                at_ts(pp, pw, pw, np.linspace(1, 0, 30), -np.ones(30))])
            rc_or_mc.poly(q)
        rc_or_mc.taper_line(pts, stem_w, stem_w * 0.2)
    return pts


def blob_cluster(mc, cx, cy, r, count, seed, spread=1.0, flat=0.75):
    """A round cumulus of discs (canopy, moss mound, cloud)."""
    rng = np.random.default_rng(seed)
    mc.ellipse(cx, cy, r, r * flat)
    for _ in range(count):
        a = rng.random() * math.pi * 2
        d = r * spread * (0.35 + 0.55 * rng.random())
        rr = r * (0.35 + 0.35 * rng.random())
        x = cx + math.cos(a) * d
        y = cy + math.sin(a) * d * flat * 0.8 - rr * 0.15
        mc.ellipse(x, y, rr, rr * 0.92)


# ------------------------------------------------------------------ particles

def particle_mote():
    a = glow_disc(32, 2.4)
    a = np.clip(a * 1.0 + 0.5 * glow_disc(32, 8.0), 0, 1)
    rgb = np.ones((32, 32, 3), F32)
    return rgb, a


def particle_firefly():
    y, x = np.mgrid[0:32, 0:32].astype(F32)
    r = np.hypot(x - 15.5, y - 15.5) / 16
    core = np.exp(-(r / 0.13) ** 2)
    halo = np.clip(1 - r, 0, 1) ** 2.6 * 0.55
    a = np.clip(core + halo, 0, 1)
    rgb = np.ones((32, 32, 3), F32)
    rgb[..., 2] -= 0.06 * (1 - core)  # the faintest warm halo
    return rgb, a


def particle_pollen():
    y, x = np.mgrid[0:16, 0:16].astype(F32)
    dx, dy = x - 7.5, y - 7.5
    r = np.hypot(dx, dy) / 8
    ang = np.arctan2(dy, dx)
    spikes = 0.5 + 0.5 * np.cos(ang * 6)
    a = np.clip(1 - r, 0, 1) ** 2.2 + 0.25 * np.clip(1 - r * 1.3, 0, 1) * spikes
    a = np.clip(np.maximum(a, np.exp(-(r / 0.28) ** 2)), 0, 1)
    rgb = np.ones((16, 16, 3), F32)
    return rgb, a


def particle_petals():
    """Three petal variants side by side, 32x32 each (96x32)."""
    W = 96
    out_rgb = np.zeros((32, W, 3), F32)
    out_a = np.zeros((32, W), F32)
    specs = [(0.0, 0.0, 1.0), (0.6, 0.35, 0.8), (-0.5, -0.2, 0.9)]
    for i, (rot, bend, size) in enumerate(specs):
        ss = 4
        mc = MaskCanvas(32, 32, ss)
        n = 40
        t = np.linspace(0, 1, n)
        # teardrop with a small notch at the wide end
        wv = np.sin(np.pi * t ** 0.8) ** 0.9 * 7.5 * size
        L = 24 * size
        pts = euler_spine(L, rot - math.pi / 2 + 0.2, bend / L * 3, 0, 1, n)
        pts = pts - pts.mean(0) + np.array([16, 16], F32)
        tang, nrm = frame(pts)
        left = pts + nrm * wv[:, None]
        right = pts - nrm * wv[:, None]
        poly = np.concatenate([left, right[::-1]])
        mc.poly(poly)
        # notch
        tip = pts[-1]
        mc.circle(tip[0], tip[1], 1.6 * size, 0)
        m = mc.get()
        m = gblur(m, 0.4)
        # shading: lighter centre line, slightly grey toward the base, faint vein
        rc = RibbonCanvas(32, 32, ss)
        rc.ribbon(pts, wv, wv, K=8)
        mp = rc.maps(0.8)
        sh = 0.86 + 0.14 * (1 - np.abs(mp["s"]) ** 2) + 0.06 * mp["f"]
        sh = sh * (0.9 + 0.1 * mp["t"])
        rgb = np.clip(np.stack([sh, sh, sh], -1), 0, 1)
        out_rgb[:, i * 32:(i + 1) * 32] = rgb
        out_a[:, i * 32:(i + 1) * 32] = m
    return out_rgb, out_a


def particle_light_shaft(w=256, h=1024, seed=5):
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    v = y / (h - 1)
    # the ray widens downward
    half = (0.18 + 0.20 * v) * w
    d = np.abs(x - (w - 1) / 2) / half
    prof = np.clip(1 - d, 0, 1)
    prof = prof * prof * (3 - 2 * prof)
    # internal streaks: vertically stretched noise, tileable in x not needed
    st = vnoise(w, h, 1, seed, 3, 0.5, sx=w / 10, sy=h * 0.6)
    st2 = vnoise(w, h, 1, seed + 1, 2, 0.5, sx=w / 24, sy=h * 0.9)
    streak = 0.65 + 0.25 * st + 0.15 * st2
    along = smoothstep(0.0, 0.12, v) * (1 - smoothstep(0.45, 1.0, v)) ** 1.2
    a = np.clip(prof * streak * along, 0, 1)
    a = gblur(a, 2.0)
    rgb = np.ones((h, w, 3), F32)
    return rgb, a * 0.95
