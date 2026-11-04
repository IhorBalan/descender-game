"""Zone 2, The Orchard Under the Canopy: deep teal and cyan, light from the
fruit and fungi, shafts from above. The trees are a little too orderly:
identical, evenly spaced, numbered, fruit in exact rows."""
from __future__ import annotations

import math

from PIL import Image, ImageDraw

import pa_z1
from pa_backdrop import curve_at, fill_below, leaf_shape, mask_ribbon, periodic_curve, sky_ramp, stem_shape, wrapped
from pa_core import (F32, Layer, MaskCanvas, euler_spine, font, gblur, grad3, hexc, inner_edge, lambert, lerp, np,
                     pillow_normals, rim_light, smoothstep, to_image, vnoise, wrapd)
from pa_shapes import blob_cluster, fern_frond, leaf_geometry, tendril_pts

ZONE = "z2"
DIR = "Z2_Orchard"
W, H = 2048, 1152
LIGHT2 = (0.15, -1.0)
LIGHT3 = (0.15, -0.85, 0.5)

PAL = {
    "sky": [(0.00, hexc("1a6670")), (0.14, hexc("125460")), (0.40, hexc("0c3c4a")), (0.70, hexc("0a2f3c")),
            (1.00, hexc("114654"))],
    "haze": hexc("1e6470"),
    "glow": hexc("a8fff0"),
    "glow_hi": hexc("eafffb"),
    "fruit": hexc("9ff7e4"),
}

GP2 = {
    "c2a1124": hexc("06181e"), "c5a2640": hexc("12363e"), "c643048": hexc("163e44"), "c70384c": hexc("1a464a"),
    "c562a40": hexc("10323a"), "c7a4252": hexc("1e4e50"), "c4a1a34": hexc("0c2a32"), "ca45a68": hexc("1a4446"),
    "c4e4a26": hexc("1e4a3a"), "c7a7038": hexc("2e7a60"), "c1c0818": hexc("02100f"), "c8a7a3a": hexc("2e7a6a"),
    "c5a3c34": hexc("1a4a48"), "c3a1c2c": hexc("0c2a30"), "cffe9a0": hexc("bafff0"), "ccdb85a": hexc("4cb4a0"),
    "c6c6a2c": hexc("1e5a54"), "cf2e08a": hexc("b4fff0"), "cc4ae4e": hexc("4ab8a4"), "c8a7234": hexc("20706a"),
    "c5c3a30": hexc("0e3a3e"), "cfff4c4": hexc("e0fff8"), "cf0d870": hexc("7ae0c8"), "c9c9038": hexc("2a8478"),
    "cfff0b8": hexc("d0fff4"), "cb8a23e": hexc("3a9c8c"), "cff9db4": hexc("8eeaff"), "cffe0e6": hexc("eaffff"),
    "c5a2a40": hexc("14404a"), "c3a1630": hexc("0a2a32"), "c4a1e38": hexc("0e3038"), "c341228": hexc("082228"),
    "gold": hexc("9ffff0"), "gold_hi": hexc("e6fffa"),
    "fill_dark": hexc("0a1c22"), "fill_mid": hexc("10282e"), "fill_light": hexc("173438"),
    "fill_moss": hexc("143a34"),
    "n_back": 85, "n_front": 44, "n_flowers": 10, "n_curls": 6, "tex": 0.16,
}


def Layer_from(rgb, a):
    L = Layer(rgb.shape[1], rgb.shape[0])
    L.set_straight(rgb, a)
    return L


# ================================================================== sky

def sky(seed=1201):
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    rgb = np.broadcast_to(sky_ramp(PAL["sky"], H)[:, None, :], (H, W, 3)).copy()
    # light from above through gaps in the canopy: broad glows along the top
    lights = np.zeros((H, W), F32)
    rng = np.random.default_rng(seed)
    for i in range(5):
        cx = (i + rng.uniform(0.2, 0.8)) * W / 5
        r = np.hypot(wrapd(x - cx, W) * 0.8, (y + 60) * 1.0)
        lights += np.exp(-(r / rng.uniform(260, 420)) ** 1.5) * rng.uniform(0.6, 1.0)
    rgb = lerp(rgb, hexc("5fd2c8"), np.clip(lights, 0, 1) * 0.55)
    # soft vertical shafts falling from the canopy
    sh = vnoise(W, H, 1, seed + 2, 3, 0.5, True, False, sx=70, sy=4000)
    shafts = smoothstep(0.55, 0.9, sh) * np.exp(-(y / 760) ** 1.4) * (0.4 + 0.6 * np.clip(lights * 1.6, 0, 1))
    rgb = rgb + hexc("8ff0e2") * (gblur(shafts, 6, wrap_x=True) * 0.18)[..., None]
    # the canopy itself: a dark lace with glowing holes, densest at the top
    n = vnoise(W, H, 1, seed + 3, 5, 0.55, True, False, sx=150, sy=70)
    dens = smoothstep(380, 0, y)
    canopy = smoothstep(0.5, 0.58, n * 0.6 + dens * 0.6) * smoothstep(0.02, 0.25, dens)
    canopy = gblur(canopy, 1.5, wrap_x=True)
    nx, ny, nz = pillow_normals(canopy, 10, 1.4, True)
    lam = lambert(nx, ny, nz, (0.0, 0.8, 0.6))  # underside lit by the fruit-glow below
    ccol = lerp(hexc("051c24"), hexc("1b5a60"), lam * 0.8)
    ccol = lerp(ccol, hexc("aefff0"), inner_edge(canopy, 3, True) * np.clip(lights, 0, 1) * 0.6)
    rgb = lerp(rgb, ccol, canopy * 0.92)
    # small leaves hanging off the lace edge
    holes = (1 - canopy) * dens
    rgb = rgb + PAL["glow"] * (holes * np.clip(lights, 0, 1) * 0.35)[..., None]
    # cool mist low down
    rgb = lerp(rgb, hexc("1c5e68"), smoothstep(700, H, y) * 0.5)
    return Layer_from(np.clip(rgb, 0, 1), np.ones((H, W), F32))


def _sky_at(yy):
    return grad3(PAL["sky"], np.clip(np.asarray(yy, F32) / (H - 1), 0, 1))


# ================================================================== orchard tree sprite

def tree_sprite(h, seed=1501, number=None, pal=None, fruit=True, ss=2, detail=1.0):
    """An orchard tree: straight trunk, round canopy, fruit in exact rows.
    Returns (rgb, a, glow, (anchor_x, anchor_y)) with anchor at the trunk foot."""
    pal = pal or {"dark": hexc("08262e"), "mid": hexc("17565c"), "lit": hexc("3e9c94"), "rim": hexc("a6f6e8"),
                  "bark": hexc("143a40"), "bark_lit": hexc("3a8478")}
    w = int(h * 0.82)
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    cx, cy, R = w / 2, h * 0.36, h * 0.30
    # trunk + roots + branches
    tm = MaskCanvas(w, h, ss)
    trunk = euler_spine(h * 0.62, -math.pi / 2, 0.0, 0.0, 1, 60, 0.6, seed) + np.array([cx, h * 0.985], F32)
    tm.taper_line(trunk, h * 0.075, h * 0.04)
    for side in (-1, 1):
        rp = tendril_pts(h * 0.12, math.pi / 2 + side * 1.15, side * -0.15, 2, 0, 30) + np.array(
            [cx + side * h * 0.015, h * 0.94], F32)
        tm.taper_line(rp, h * 0.04, h * 0.008)
        bp = tendril_pts(h * 0.2, -math.pi / 2 + side * 0.7, side * 0.1, 2, 0, 30) + trunk[38]
        tm.taper_line(bp, h * 0.03, h * 0.01)
    m_tr = tm.get()
    # canopy: one big round crown of overlapping lobes
    cm = MaskCanvas(w, h, ss)
    blob_cluster(cm, cx, cy, R * 0.8, 10, seed + 1, 0.9, 0.82)
    for k in range(28):  # scalloped leafy edge
        a = k / 28 * 2 * math.pi
        rr = R * (0.86 + 0.06 * math.sin(a * 3))
        cm.circle(cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.82, R * 0.16)
    # pointed leaves fringing the crown, curling at the tips
    for k in range(int(40 * detail) + 10):
        a = rng.uniform(0, 2 * math.pi)
        rr = R * rng.uniform(0.86, 1.0)
        bx, by = cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.82
        sgn = 1 if rng.random() < 0.5 else -1
        lp, lw, _ = leaf_geometry(R * rng.uniform(0.11, 0.17), a + rng.uniform(-0.4, 0.4), R * 0.065, sgn * 0.2, 0, 2.0, 0.35, 0.2, 0, 1, 1, 24)
        mask_ribbon(cm, lp + np.array([bx, by], F32), lw, lw)
    m_c = cm.get()
    # leaf texture inside the crown: little lobes catching light
    lobes = MaskCanvas(w, h, ss)
    for k in range(int(140 * detail)):
        a, d = rng.uniform(0, 2 * math.pi), R * math.sqrt(rng.uniform(0, 0.9))
        lobes.ellipse(cx + math.cos(a) * d, cy + math.sin(a) * d * 0.8, R * 0.09, R * 0.06)
    m_lobe = gblur(lobes.get(), 1.0) * m_c
    lay = Layer(w, h)
    # trunk shading: lit from above, bark strands
    nx, ny, nz = pillow_normals(m_tr, h * 0.012, 1.0)
    lam = lambert(nx, ny, nz, (0.3, -0.6, 0.7))
    strands = 0.5 + 0.5 * np.sin((x - cx) / (h * 0.006) + vnoise(w, h, h * 0.05, seed + 2, 3) * 6)
    bark = lerp(pal["bark"], pal["bark_lit"], lam * (0.75 + 0.25 * strands))
    lay.over(bark, m_tr)
    # canopy shading
    nx, ny, nz = pillow_normals(m_c, R * 0.25, 1.0)
    lam = lambert(nx, ny, nz, LIGHT3)
    ccol = grad3([(0.0, pal["dark"]), (0.55, pal["mid"]), (1.0, pal["lit"])], lam)
    ccol = lerp(ccol, pal["lit"] * 1.05, m_lobe * lam * 0.16)
    ccol = lerp(ccol, pal["dark"] * 0.8, m_lobe * (1 - lam) * 0.12)
    ccol = lerp(ccol, pal["rim"], rim_light(m_c, R * 0.05, LIGHT2) * 0.7)
    shadow = gblur(m_c, R * 0.12) * (1 - m_c) * m_tr
    lay.rgb *= (1 - 0.5 * shadow)[..., None]
    lay.over(np.clip(ccol, 0, 1), m_c)
    glow = np.zeros((h, w), F32)
    if fruit:
        # fruit in exact rows (the orchard is tended) but soft: small, varied
        # brightness and size, soft halos, some half hidden by leaves
        frng = np.random.default_rng(seed + 77)
        step = h * 0.085
        rf0 = h * 0.0085
        inner = gblur(m_c, R * 0.1)
        f_a = np.zeros((h, w), F32)
        f_col = np.zeros((h, w, 3), F32)
        glow = np.zeros((h, w), F32)
        halo_light = np.zeros((h, w), F32)
        cover = MaskCanvas(w, h, ss)
        for row in range(0, 4):
            fy = cy - step * 0.3 + row * step
            for col in range(-3, 4):
                fx = cx + col * step
                if inner[int(min(h - 1, fy)), int(min(w - 1, fx))] <= 0.9:
                    continue
                b = 0.22 + 0.78 * frng.random() ** 2.4          # mostly dim, a few bright
                rf = rf0 * frng.uniform(0.75, 1.15)
                r = np.hypot(x - fx, y - fy)
                body = smoothstep(rf * 1.05, rf * 0.55, r)          # soft-edged, not a hard disc
                core = np.exp(-(r / (rf * 0.55)) ** 2)
                c = lerp(lerp(hexc("1d6a68"), hexc("6fe6d2"), b), PAL["glow_hi"], core * b)
                f_col = f_col * (1 - body[..., None]) + c * body[..., None]
                f_a = np.maximum(f_a, body)
                halo = np.exp(-(r / (rf * (2.2 + 1.6 * b))) ** 2) * b
                halo_light = np.maximum(halo_light, halo)
                glow = np.maximum(glow, body * (0.25 + 0.75 * b) * (0.5 + 0.5 * core) + halo * 0.35)
                if frng.random() < 0.4:   # a leaf partly over this fruit
                    sgn = 1 if frng.random() < 0.5 else -1
                    ang = frng.uniform(-0.6, 0.6) + (math.pi / 2 if frng.random() < 0.5 else -math.pi / 2)
                    lp, lw, _ = leaf_geometry(rf * frng.uniform(2.3, 2.9), ang, rf * 0.9, sgn * 0.15, 0, 2.0,
                                              0.35, 0.2, 0, 1, 1, 24)
                    off = np.array([fx - math.cos(ang) * rf * 2.1, fy - math.sin(ang) * rf * 2.1], F32)
                    mask_ribbon(cover, lp + off, lw, lw)
        m_cov = cover.get() * m_c
        lay.rgb = lay.rgb + (PAL["glow"] * (halo_light * m_c * 0.14)[..., None]) * lay.a[..., None]
        f_rgb = np.where(f_a[..., None] > 1e-3, f_col / np.maximum(f_a[..., None], 1e-3), 0)
        lay.over(np.clip(f_rgb, 0, 1), f_a * m_c)
        # covering leaves, shaded like the crown
        nx, ny, nz = pillow_normals(m_cov, 2.0, 1.0)
        lcol = lerp(pal["mid"] * 0.9, pal["lit"] * 0.95, lambert(nx, ny, nz, LIGHT3) * 0.8)
        lcol = lerp(lcol, PAL["glow"], 0.12)  # lit from the fruit underneath
        lay.over(lcol, m_cov)
        glow = np.clip(glow * (1 - gblur(m_cov, 1.5) * 0.6) * m_c, 0, 1)
    if number is not None:
        # a small numbered plate nailed to the trunk
        ph, pw = int(h * 0.04), int(h * 0.06)
        px, py = int(cx - pw / 2), int(h * 0.74)
        plate = Image.new("L", (pw * 4, ph * 4), 0)
        d = ImageDraw.Draw(plate)
        d.rounded_rectangle([0, 0, pw * 4 - 1, ph * 4 - 1], radius=ph, fill=255)
        txt = Image.new("L", (pw * 4, ph * 4), 0)
        dt = ImageDraw.Draw(txt)
        f = font(int(ph * 3.0))
        s = str(number)
        bb = dt.textbbox((0, 0), s, font=f)
        dt.text(((pw * 4 - (bb[2] - bb[0])) / 2 - bb[0], (ph * 4 - (bb[3] - bb[1])) / 2 - bb[1]), s, fill=255, font=f)
        pm = np.asarray(plate.resize((pw, ph), Image.BOX)).astype(F32) / 255
        tm_ = np.asarray(txt.resize((pw, ph), Image.BOX)).astype(F32) / 255
        pl = np.zeros((h, w), F32)
        tl = np.zeros((h, w), F32)
        pl[py:py + ph, px:px + pw] = pm
        tl[py:py + ph, px:px + pw] = tm_
        lay.over(hexc("4e7a74"), pl * 0.85)
        lay.over(hexc("0c2624"), tl * pl * 0.9)
    rgb, a = lay.straight()
    return rgb, a, glow, (cx, h * 0.985)


# ================================================================== backdrops

def _fogged(rgb, a, color, amount):
    return lerp(rgb, color, amount), a


def far(seed=1301):
    lay = Layer(W, H)
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    # back row: 16 identical small trees, then a hedge line
    for row, (n, th, base, col, fog) in enumerate([(16, 230, 700, hexc("1a5e6a"), 0.55),
                                                   (10, 340, 800, hexc("15505c"), 0.38)]):
        rgb, a, glow, (ax, ay) = tree_sprite(th, seed + row, None, fruit=True, detail=0.4)
        rgb = lerp(rgb, PAL["haze"], fog)
        rgb = lerp(rgb, col, 0.35)
        for i in range(n):
            tx = (i + 0.5 + 0.5 * row) * W / n
            lay.blit(rgb, a, tx - ax, base - ay, wrap_x=True)
            lay.blit(np.ones_like(rgb) * PAL["glow"] * glow[..., None] * 0.25, glow * 0.25, tx - ax, base - ay,
                     wrap_x=True, premult=True, mode="add")
        mc = MaskCanvas(W, H, 2)
        xs, hy = periodic_curve(W, base - 10, [(n, 6), (2 * n, 3)], seed + 9 + row)
        wrapped(W, 0, lambda off: fill_below(mc, xs, hy, H, W, off))
        hm = mc.get()
        lay.over(lerp(lerp(col, PAL["haze"], fog), PAL["haze"], smoothstep(base, H, y) * 0.5), hm)
    rgb, a = lay.straight()
    rgb = lerp(rgb, PAL["haze"], smoothstep(500, H, y) * 0.3)
    lay.set_straight(rgb, gblur(a, 0.8, wrap_x=True))
    return lay


def midfar(seed=1401):
    lay = Layer(W, H)
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    rgb, a, glow, (ax, ay) = tree_sprite(720, seed, None, fruit=True, detail=0.8)
    rgb = lerp(rgb * 0.85, PAL["haze"], 0.42)
    for i in range(6):
        tx = (i + 0.5) * W / 6
        lay.blit(rgb, a, tx - ax, 940 - ay, wrap_x=True)
        lay.blit(PAL["glow"] * glow[..., None] * 0.3, glow * 0.3, tx - ax, 940 - ay, wrap_x=True, premult=True,
                 mode="add")
    mc = MaskCanvas(W, H, 2)
    xs, hy = periodic_curve(W, 920, [(6, 10), (12, 4), (1, 8)], seed + 3)
    wrapped(W, 0, lambda off: fill_below(mc, xs, hy, H, W, off))
    hm = mc.get()
    nx, ny, nz = pillow_normals(hm, 12, 1.0, True)
    gcol = lerp(hexc("0f3c46"), hexc("2a7c7a"), lambert(nx, ny, nz, LIGHT3))
    lay.over(gcol, hm)
    # hanging vines from the canopy above
    vm = MaskCanvas(W, H, 2)
    rng = np.random.default_rng(seed)
    beads = []
    for i in range(18):
        vx = rng.uniform(0, W)
        L = rng.uniform(120, 420)
        sgn = 1 if rng.random() < 0.5 else -1
        p = tendril_pts(L, math.pi / 2, sgn * rng.uniform(0.3, 1.0), 4, 0, 90, 1.0, seed + i) + np.array([vx, -5], F32)
        wrapped(W, vx, lambda off: vm.taper_line(p + np.array([off, 0], F32), 6, 2))
        beads.append(p[-12])
    m_v = vm.get()
    lay.over(lerp(hexc("0c3038"), hexc("2a6e6e"), smoothstep(0, 400, y)), m_v)
    for bx, by in beads:
        for off in (-W, 0, W):
            r = np.hypot(x - bx - off, y - by)
            lay.add_light(PAL["glow"], np.exp(-(r / 3) ** 2) * 0.9 + np.exp(-(r / 14) ** 2) * 0.25)
    rgb2, a2 = lay.straight()
    rgb2 = lerp(rgb2, PAL["haze"], smoothstep(820, H, y) * 0.45)
    lay.set_straight(rgb2, a2)
    lay.rgb = gblur(lay.rgb, 0.7, wrap_x=True)
    lay.a = gblur(lay.a, 0.7, wrap_x=True)
    return lay


def fungus_cluster(seed, scale=1.0, ss=3):
    """Luminous mushroom cluster. Returns rgb, a, glow (w=h=256*scale), foot at bottom centre."""
    s = int(256 * scale)
    y, x = np.mgrid[0:s, 0:s].astype(F32)
    rng = np.random.default_rng(seed)
    lay = Layer(s, s)
    glow = np.zeros((s, s), F32)
    shrooms = sorted([(rng.uniform(0.25, 0.75) * s, rng.uniform(0.35, 1.0)) for _ in range(5)], key=lambda t: -t[1])
    for i, (fx, k) in enumerate(shrooms):
        hgt = s * (0.25 + 0.5 * k)
        cr = s * (0.07 + 0.11 * k)
        sgn = 1 if fx > s / 2 else -1
        st = tendril_pts(hgt, -math.pi / 2 + sgn * 0.15, -sgn * 0.08, 2, 0, 40) + np.array([fx, s - 2], F32)
        sm = MaskCanvas(s, s, ss)
        sm.taper_line(st, cr * 0.32, cr * 0.22)
        m_s = sm.get()
        cx, cy = st[-1]
        cap = MaskCanvas(s, s, ss)
        t = np.linspace(0, math.pi, 60)
        tilt = sgn * 0.12
        pts = [(cx + math.cos(a + tilt) * cr * 1.0, cy - math.sin(a + tilt) * cr * 0.75) for a in t]
        pts += [(cx - cr * 0.95, cy + cr * 0.12), (cx + cr * 0.95, cy + cr * 0.12)][::-1]
        cap.poly(pts)
        m_c = cap.get()
        gill = MaskCanvas(s, s, ss)
        gill.ellipse(cx, cy + cr * 0.06, cr * 0.9, cr * 0.16)
        m_g = gill.get() * (1 - smoothstep(cy - cr * 0.05, cy - cr * 0.2, y))
        nx, ny, nz = pillow_normals(m_s, 2, 1.0)
        lay.over(lerp(hexc("1e5a60"), hexc("8ae0d6"), lambert(nx, ny, nz, (0.3, -0.2, 0.8)) *
                      smoothstep(s, cy, y)), m_s)
        nx, ny, nz = pillow_normals(m_c, cr * 0.3, 1.0)
        lam = lambert(nx, ny, nz, (0.2, -0.8, 0.5))
        ccol = lerp(hexc("0f4a58"), hexc("3fb8bc"), lam)
        # glowing spots on the cap
        spots = np.zeros((s, s), F32)
        for _ in range(6):
            a = rng.uniform(0.15, math.pi - 0.15)
            d = rng.uniform(0.3, 0.8)
            sx, sy = cx + math.cos(a) * cr * d, cy - math.sin(a) * cr * 0.65 * d
            spots += smoothstep(cr * 0.11, cr * 0.04, np.hypot(x - sx, y - sy))
        spots = np.clip(spots, 0, 1) * m_c
        ccol = lerp(ccol, PAL["glow_hi"], spots)
        ccol = lerp(ccol, hexc("c8fff4"), rim_light(m_c, cr * 0.08, (0.0, 1.0)) * 0.7)
        lay.over(np.clip(ccol, 0, 1), m_c)
        lay.over(lerp(hexc("7ff0e4"), PAL["glow_hi"], 0.5), m_g)
        glow = np.maximum(glow, np.clip(spots + m_g + 0.25 * m_c * (1 - lam), 0, 1))
        glow = np.maximum(glow, np.exp(-(np.hypot(x - cx, (y - cy) * 1.4) / (cr * 1.4)) ** 2) * 0.35)
    # moss tuft at the foot
    mm = MaskCanvas(s, s, ss)
    blob_cluster(mm, s / 2, s - 4, s * 0.18, 6, seed + 3, 1.3, 0.35)
    m_m = mm.get() * smoothstep(s * 0.7, s * 0.85, y)
    nx, ny, nz = pillow_normals(m_m, 4, 1.0)
    lay.over(lerp(hexc("0c3a3a"), hexc("4ab09a"), lambert(nx, ny, nz, LIGHT3)), m_m)
    rgb, a = lay.straight()
    return rgb, a, glow


def mid(seed=1601):
    lay = Layer(W, H)
    y, x = np.mgrid[0:H, 0:W].astype(F32)
    rng = np.random.default_rng(seed)
    # ground mound
    mc = MaskCanvas(W, H, 2)
    xs, hy = periodic_curve(W, 1000, [(1, 18), (3, 14), (6, 8)], seed)
    wrapped(W, 0, lambda off: fill_below(mc, xs, hy, H, W, off))
    for i in range(8):
        cx = (i + rng.uniform(0, 1)) * W / 8
        for off in (-W, 0, W):
            blob_cluster(mc, cx + off, curve_at(xs, hy, cx, W) + 10, 60 + 20 * (i % 3), 5, seed + i, 1.2, 0.5)
    for i in range(6):
        cx = rng.uniform(0, W)
        fern_frond(mc, cx, curve_at(xs, hy, cx, W) + 20, rng.uniform(220, 340),
                   -math.pi / 2 + rng.uniform(-0.4, 0.4), turns=1.2 * (1 if i % 2 else -1), stem_w=7, pinnae=14,
                   seed=seed + 40 + i, ribbon=False, pinna_len=60)
    m = mc.get()
    # three big trunks, evenly spaced, numbered in sequence
    tm = MaskCanvas(W, H, 2)
    plates = []
    for i in range(3):
        tx = (i + 0.5) * W / 3
        base = curve_at(xs, hy, tx, W) + 30
        tp = euler_spine(base + 60, -math.pi / 2, 0.0, 0.0, 1, 80, 0.8, seed + i) + np.array([tx, base], F32)
        wrapped(W, tx, lambda off: tm.taper_line(tp + np.array([off, 0], F32), 150, 96))
        for side in (-1, 1):
            for k, (L, ang) in enumerate([(200, 1.05), (140, 0.6)]):
                rp = tendril_pts(L, math.pi / 2 + side * ang, side * -0.25, 2, 0, 40) + np.array(
                    [tx + side * 40, base - 70 - k * 30], F32)
                wrapped(W, tx, lambda off: tm.taper_line(rp + np.array([off, 0], F32), 54 - k * 12, 8))
        plates.append((tx, base - 230, 17 + i))
    m_t = tm.get()
    # shade ground
    nx, ny, nz = pillow_normals(m, 10, 1.0, True)
    lam = lambert(nx, ny, nz, LIGHT3)
    gcol = lerp(hexc("06212a"), hexc("1a5658"), lam)
    gcol = lerp(gcol, hexc("7fe8dc"), rim_light(m, 3, LIGHT2, True) * 0.6)
    # trunks: bark strands, cyan rim from above
    nx, ny, nz = pillow_normals(m_t, 22, 1.0, True)
    lam_t = lambert(nx, ny, nz, (0.45, -0.3, 0.8))
    bark_n = vnoise(W, H, 1, seed + 5, 4, 0.5, True, False, sx=14, sy=260)
    tcol = lerp(hexc("051a20"), hexc("1d5254"), lam_t * (0.7 + 0.5 * bark_n))
    tcol = lerp(tcol, hexc("6ad8cc"), rim_light(m_t, 5, (1.0, -0.2), True) * 0.45)
    tcol = lerp(tcol, PAL["haze"], smoothstep(200, -100, y) * 0.0)
    lay.over(tcol, m_t)
    lay.over(gcol, m)
    # numbered plates on the trunks
    for tx, py, num in plates:
        ph, pw = 40, 64
        plate = Image.new("L", (pw * 4, ph * 4), 0)
        d = ImageDraw.Draw(plate)
        d.rounded_rectangle([0, 0, pw * 4 - 1, ph * 4 - 1], radius=40, fill=255)
        txt = Image.new("L", (pw * 4, ph * 4), 0)
        dt = ImageDraw.Draw(txt)
        f = font(110)
        s = str(num)
        bb = dt.textbbox((0, 0), s, font=f)
        dt.text(((pw * 4 - (bb[2] - bb[0])) / 2 - bb[0], (ph * 4 - (bb[3] - bb[1])) / 2 - bb[1]), s, fill=255, font=f)
        pm = np.asarray(plate.resize((pw, ph), Image.BOX)).astype(F32) / 255
        tmm = np.asarray(txt.resize((pw, ph), Image.BOX)).astype(F32) / 255
        rgbp = np.ones((ph, pw, 3), F32) * hexc("355a58")
        rgbp = lerp(rgbp, hexc("081c1e"), tmm * 0.85)
        lay.blit(rgbp, pm * 0.9, tx - pw / 2, py, wrap_x=True)
    # glowing fungi at the trunk feet
    for i in range(3):
        tx = (i + 0.5) * W / 3 + (90 if i % 2 else -100)
        frgb, fa, fg = fungus_cluster(seed + 70 + i, 0.8)
        fy = curve_at(xs, hy, tx, W) + 24
        s = fa.shape[0]
        lay.blit(frgb, fa, tx - s / 2, fy - s, wrap_x=True)
        lay.blit(PAL["glow"] * fg[..., None] * 0.6, fg * 0.6, tx - s / 2, fy - s, wrap_x=True, premult=True,
                 mode="add")
        r = np.hypot(wrapd(x - tx, W), (y - fy + s * 0.4) * 1.3)
        lay.add_light(PAL["glow"], np.exp(-(r / 160) ** 2) * 0.22 * lay.a)
    # hanging moss strands from above
    vm = MaskCanvas(W, H, 2)
    for i in range(14):
        vx = rng.uniform(0, W)
        L = rng.uniform(180, 520)
        sgn = 1 if rng.random() < 0.5 else -1
        p = tendril_pts(L, math.pi / 2, sgn * rng.uniform(0.2, 0.8), 4, 0, 90, 1.5, seed + 90 + i) \
            + np.array([vx, -5], F32)
        wrapped(W, vx, lambda off: vm.taper_line(p + np.array([off, 0], F32), 10, 2))
    lay.over(lerp(hexc("06242a"), hexc("1e5a5c"), smoothstep(0, 500, y)), vm.get())
    rgb, a = lay.straight()
    rgb = lerp(rgb, PAL["haze"], smoothstep(920, H, y) * 0.35)
    lay.set_straight(rgb, a)
    lay.rgb = gblur(lay.rgb, 0.6, wrap_x=True)
    lay.a = gblur(lay.a, 0.6, wrap_x=True)
    return lay


# ================================================================== foreground

FG_COL = hexc("03141a")
FG_RIM = hexc("2f8e88")


def _fg(fn, rim_dir=(0.2, -1.0)):
    """Reuse a zone-1 silhouette shape with the zone-2 colours."""
    old = pa_z1.FG_COL, pa_z1.FG_RIM, pa_z1.FG_MID
    pa_z1.FG_COL, pa_z1.FG_RIM, pa_z1.FG_MID = FG_COL, FG_RIM, hexc("0c2e34")
    try:
        lay = fn()
    finally:
        pa_z1.FG_COL, pa_z1.FG_RIM, pa_z1.FG_MID = old
    return lay


def fg_roots(seed=1701):
    w, h = 1024, 512
    mc = MaskCanvas(w, h, 2)
    rng = np.random.default_rng(seed)
    for i in range(7):
        x0 = rng.uniform(100, w - 100)
        sgn = 1 if x0 < w / 2 else -1
        L = rng.uniform(500, 900)
        p = euler_spine(L, -math.pi / 2 + sgn * 0.75, sgn * 0.0024, 0, 1, 120, 2, seed + i) + np.array([x0, h + 20],
                                                                                                    F32)
        mc.taper_line(p, rng.uniform(40, 70), 16)
    return _fg_lay(mc.get())


def _fg_lay(m, blur=3.0):
    m = gblur(m, blur)
    nx, ny, nz = pillow_normals(m, 18, 0.6)
    lam = lambert(nx, ny, nz, (0.1, -0.9, 0.5))
    col = lerp(FG_COL, hexc("0c2e34"), lam * 0.7)
    col = lerp(col, FG_RIM, rim_light(m, 6, (0.1, -1.0)) * 0.5)
    lay = Layer(m.shape[1], m.shape[0])
    lay.set_straight(col, m)
    return lay


def fg_vines_glow(seed=1711):
    """Hanging vines with glowing beads (beads carry their own light)."""
    w, h = 384, 1024
    mc = MaskCanvas(w, h, 2)
    rng = np.random.default_rng(seed)
    beads = []
    for i in range(4):
        x0 = 50 + i * 95 + rng.uniform(-15, 15)
        L = rng.uniform(600, 980)
        sgn = 1 if i % 2 else -1
        p = tendril_pts(L, math.pi / 2 + sgn * 0.04, sgn * 1.3, 4.0, 0, 300, 3.0, seed + i) + np.array([x0, -10], F32)
        mc.taper_line(p, 12, 3)
        for k in range(20, 280, 22):
            side = 1 if (k // 22) % 2 else -1
            lp, lw, _ = leaf_geometry(rng.uniform(30, 60), math.pi / 2 + side * 0.9, 12, 0.3 * side, 0, 2.0, 0.35,
                                      0.15, 0, 1, 1, 30)
            mask_ribbon(mc, lp + p[k], lw, lw)
            if rng.random() < 0.35:
                beads.append(p[k])
    lay = _fg_lay(mc.get(), 2.5)
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    for bx, by in beads:
        r = np.hypot(x - bx, y - by - 8)
        lay.over(PAL["glow_hi"], smoothstep(5, 3, r))
        lay.add_light(PAL["glow"], np.exp(-(r / 14) ** 2) * 0.35)
    return lay


def fg_canopy(seed=1721):
    """A mass of leaves hanging into the top of the frame."""
    w, h = 1024, 640
    mc = MaskCanvas(w, h, 2)
    rng = np.random.default_rng(seed)
    blob_cluster(mc, w / 2, -60, 360, 10, seed, 1.2, 0.5)
    for i in range(16):
        x0 = rng.uniform(80, w - 80)
        sgn = 1 if rng.random() < 0.5 else -1
        pts, hwl, hwr = leaf_geometry(rng.uniform(180, 380), math.pi / 2 + sgn * rng.uniform(0.2, 0.7),
                                      rng.uniform(40, 70), sgn * rng.uniform(0.5, 1.0), 0, 3.0, 0.3, 0.1, 0, 1, 1,
                                      160)
        mask_ribbon(mc, pts + np.array([x0, rng.uniform(20, 140)], F32), hwl, hwr)
    return _fg_lay(mc.get(), 3.5)


# ================================================================== props

def glowing_fruit(seed=1801, s=64):
    """Orchard fruit: a soft glowing pear-round with a tally scratched in."""
    y, x = np.mgrid[0:s, 0:s].astype(F32)
    ss = 4
    mc = MaskCanvas(s, s, ss)
    cx, cy = s * 0.5, s * 0.56
    t = np.linspace(0, 2 * math.pi, 120)
    r = s * 0.33
    px = cx + np.sin(t) * r * (1 - 0.12 * np.cos(t))
    py = cy - np.cos(t) * r * 1.02 + 0.08 * r * np.cos(t) ** 2
    mc.poly(list(zip(px, py)))
    m = mc.get()
    st = MaskCanvas(s, s, ss)
    sp = tendril_pts(s * 0.2, -math.pi / 2 - 0.2, 0.15, 2, 0, 20) + np.array([cx, cy - r * 0.9], F32)
    st.taper_line(sp, 2.6, 1.6)
    lp, lw, _ = leaf_geometry(s * 0.3, -0.35, s * 0.07, 0.2, 0, 2, 0.35, 0.1, 0, 1, 1, 30)
    mask_ribbon(st, lp + sp[10], lw, lw)
    m_st = st.get() * (1 - m)
    nx, ny, nz = pillow_normals(m, s * 0.12, 1.0)
    lam = lambert(nx, ny, nz, (0.3, -0.6, 0.7))
    rr = np.hypot(x - cx, y - cy) / r
    inner = np.exp(-(rr / 0.7) ** 2)
    col = lerp(hexc("1c8c88"), hexc("7ff2e0"), lam * 0.6 + inner * 0.5)
    col = lerp(col, PAL["glow_hi"], inner ** 2 * 0.6)
    # tally: four strokes and a slash, scratched in
    tal = MaskCanvas(s, s, ss)
    for k in range(4):
        xx = cx - 6 + k * 4
        tal.line([(xx, cy - 5), (xx + 0.6, cy + 5)], 1.0)
    tal.line([(cx - 8, cy + 3), (cx + 8, cy - 3)], 1.0)
    m_t = tal.get() * m
    col = lerp(col, hexc("0e5a5a"), m_t * 0.75)
    col = lerp(col, hexc("ffffff"), rim_light(m, 2, (0.5, -0.8)) * 0.6)
    lay = Layer(s, s)
    nx2, ny2, nz2 = pillow_normals(m_st, 1.5, 1.0)
    lay.over(lerp(hexc("0e3a3c"), hexc("3a9a88"), lambert(nx2, ny2, nz2, LIGHT3)), m_st)
    lay.over(np.clip(col, 0, 1), m)
    gl = np.clip(m * (0.35 + 0.65 * inner) * (1 - m_t * 0.8) + gblur(m, 5) * 0.4, 0, 1)
    glow = Layer(s, s)
    glow.set_straight(np.ones((s, s, 3), F32) * PAL["glow"], gl)
    # pivot at the top of the stalk (fruit hangs)
    return lay, glow, (sp[-1][0] / s, 1 - sp[-1][1] / s)


def bounce_pod(seed=1811, s=256):
    """A giant soft seed head (bounce pod): a fluffy sphere of radiating fibres."""
    y, x = np.mgrid[0:s, 0:s].astype(F32)
    rng = np.random.default_rng(seed)
    cx, cy, R = s / 2, s * 0.44, s * 0.36
    fib = MaskCanvas(s, s, 3)
    tips = []
    for k in range(260):
        a = rng.uniform(0, 2 * math.pi)
        L = R * rng.uniform(0.8, 1.0)
        bend = rng.uniform(-0.3, 0.3)
        p = tendril_pts(L, a, bend * 0.2, 2, 0, 14) + np.array([cx, cy], F32)
        fib.taper_line(p, 2.0, 0.6, 120, 220)
        tips.append(p[-1])
    m_f = fib.get()
    puff = MaskCanvas(s, s, 3)
    for tx, ty in tips:
        puff.circle(tx, ty, R * 0.06)
    m_p = gblur(puff.get(), 2.0)
    core = MaskCanvas(s, s, 3)
    core.circle(cx, cy, R * 0.28)
    m_c = core.get()
    st = MaskCanvas(s, s, 3)
    sp = tendril_pts(s - cy - R * 0.2, math.pi / 2 + 0.1, 0.15, 2, 0, 30) + np.array([cx, cy + R * 0.25], F32)
    st.taper_line(sp, 14, 9)
    lay = Layer(s, s)
    m_st = st.get()
    nx, ny, nz = pillow_normals(m_st, 3, 1.0)
    lay.over(lerp(hexc("0e3a3c"), hexc("3a9a88"), lambert(nx, ny, nz, LIGHT3)), m_st)
    rr = np.hypot(x - cx, y - cy) / R
    lay.over(lerp(hexc("4ab6b0"), hexc("d8fff8"), smoothstep(0.3, 1.0, rr)), np.clip(m_f * 0.85, 0, 1))
    lay.over(lerp(hexc("7fd8d0"), hexc("d8fff8"), smoothstep(0.7, 1.0, rr)), np.clip(m_p * 0.55, 0, 1))
    nx, ny, nz = pillow_normals(m_c, 6, 1.0)
    lay.over(lerp(hexc("1e7470"), hexc("8ff0e0"), lambert(nx, ny, nz, LIGHT3)), m_c)
    gl = np.clip(m_p * 0.18 + m_c * 0.5 + np.exp(-(rr / 0.55) ** 2) * 0.3, 0, 1)
    glow = Layer(s, s)
    glow.set_straight(np.ones((s, s, 3), F32) * PAL["glow"], gl)
    return lay, glow, (sp[-1][0] / s, 1 - min(s - 1, sp[-1][1]) / s)


def orchard_tree_prop(seed=1821):
    h = 1280
    rgb, a, glow, (ax, ay) = tree_sprite(h, seed, number=41, fruit=True, detail=1.2)
    w = a.shape[1]
    pad = (1024 - w) // 2
    full = Layer(1024, h)
    full.blit(rgb, a, pad, 0)
    g = Layer(1024, h)
    g.blit(np.ones_like(rgb) * PAL["glow"], glow, pad, 0)
    return full, g, ((ax + pad) / 1024, 1 - ay / h)


# ================================================================== build

def build(reg):
    z = ZONE
    reg.save(pa_z1.ground_fill(1101, GP2), f"{DIR}/Ground/z2_ground_fill.png",
             "SpriteShape fill: dark teal mossy root-wood with cyan specks", z, tile=("x", "y"))
    S = 51
    reg.save(pa_z1.ground_top(1111, S, GP2), f"{DIR}/Ground/z2_ground_top.png",
             "SpriteShape top edge: cyan-lit moss lip, tufts, fiddleheads, pale glow-blossoms", z,
             pivot=(0.5, 1 - S / 128), tile=("x",), extra={"surfaceRow": S, "surfaceFromTopUnits": S / 64})
    R = 6
    reg.save(pa_z1.ground_bottom(1121, R, GP2), f"{DIR}/Ground/z2_ground_bottom.png",
             "SpriteShape side/bottom edge: dark roots with cyan drops", z, pivot=(0.5, 1 - R / 64), tile=("x",),
             extra={"surfaceRow": R})
    reg.save(sky(), f"{DIR}/Backdrop/z2_bg0_sky.png", "Parallax 0: teal canopy gloom, light from above", z,
             tile=("x",), extra={"layer": 0, "suggestedParallax": 0.98})
    reg.save(far(), f"{DIR}/Backdrop/z2_bg1_far.png", "Parallax 1: two hazy rows of identical orchard trees", z,
             tile=("x",), extra={"layer": 1, "suggestedParallax": 0.9})
    reg.save(midfar(), f"{DIR}/Backdrop/z2_bg2_midfar.png",
             "Parallax 2: evenly spaced orchard trees, fruit in rows, hanging vines", z, tile=("x",),
             extra={"layer": 2, "suggestedParallax": 0.75})
    reg.save(mid(), f"{DIR}/Backdrop/z2_bg3_mid.png",
             "Parallax 3: dark numbered trunks (17,18,19), roots, glowing fungi, hanging moss", z, tile=("x",),
             extra={"layer": 3, "suggestedParallax": 0.5})
    for name, fn, piv, desc in [
        ("fern", lambda: _fg(pa_z1.fg_fern), (0.47, 0.0), "curled fern frond rising from the bottom"),
        ("leaf_big", lambda: _fg(pa_z1.fg_leaf_big), (0.0, 1.0), "huge leaf reaching in from the top-left"),
        ("vines", fg_vines_glow, (0.5, 1.0), "hanging vines with small glowing beads"),
        ("roots", fg_roots, (0.5, 0.0), "arching roots from the bottom"),
        ("canopy", fg_canopy, (0.5, 1.0), "leaf mass hanging into the top of the frame"),
    ]:
        reg.save(fn(), f"{DIR}/Foreground/z2_fg_{name}.png", f"Foreground silhouette: {desc}", z, pivot=piv)
    fr, fg, piv = glowing_fruit()
    reg.save(fr, f"{DIR}/Props/z2_prop_fruit.png", "Glowing orchard fruit with a tally scratched in (1 unit)", z,
             pivot=piv)
    reg.save(fg, f"{DIR}/Props/z2_prop_fruit_glow.png", "Emissive fruit", z, pivot=piv, blend="additive")
    rgb, a, g = fungus_cluster(1831, 1.0)
    reg.save(to_image(rgb, a), f"{DIR}/Props/z2_prop_mushrooms.png", "Luminous mushroom cluster (4 units)", z,
             pivot=(0.5, 0.0))
    reg.save(to_image(np.ones_like(rgb) * PAL["glow"], g), f"{DIR}/Props/z2_prop_mushrooms_glow.png",
             "Emissive caps, spots and gills", z, pivot=(0.5, 0.0), blend="additive")
    tr, tg, piv = orchard_tree_prop()
    reg.save(tr, f"{DIR}/Props/z2_prop_orchard_tree.png",
             "Orchard tree for the mid layer: numbered plate (41), fruit in exact rows (16x20 units)", z, pivot=piv)
    reg.save(tg, f"{DIR}/Props/z2_prop_orchard_tree_glow.png", "Emissive fruit of the orchard tree", z, pivot=piv,
             blend="additive")
    bp, bg, piv = bounce_pod()
    reg.save(bp, f"{DIR}/Props/z2_prop_bounce_pod.png", "Bounce pod: giant soft seed head (4 units)", z, pivot=piv)
    reg.save(bg, f"{DIR}/Props/z2_prop_bounce_pod_glow.png", "Emissive bounce pod", z, pivot=piv, blend="additive")


# ================================================================== preview

def preview():
    from pa_common import PH, PW, backdrop, bloom, ground, hero, particles, put, save_preview
    lay = Layer(PW, PH)
    gl = Layer(PW, PH)
    D = DIR + "/"
    backdrop(lay, D + "Backdrop/z2_bg0_sky.png", 0)
    backdrop(lay, D + "Backdrop/z2_bg1_far.png", 160)
    backdrop(lay, D + "Backdrop/z2_bg2_midfar.png", 400)
    shaft = "Common/Particles/light_shaft.png"
    for sx, sc, am in [(330, 1.0, 0.35), (700, 0.8, 0.25), (1050, 1.1, 0.3)]:
        put(gl, shaft, sx, 760, sc, mode="add", tint=hexc("9ff5e8"), amount=am)
    backdrop(lay, D + "Backdrop/z2_bg3_mid.png", 700)
    xs = np.arange(PW, dtype=F32)
    ys = 590 + 22 * np.sin(xs / PW * 2 * math.pi * 1.1 + 2.0) + 8 * np.sin(xs / PW * 2 * math.pi * 2.7)
    put(lay, D + "Props/z2_prop_orchard_tree.png", 1000, ys[1000] + 40, 0.55)
    put(gl, D + "Props/z2_prop_orchard_tree_glow.png", 1000, ys[1000] + 40, 0.55, mode="add", tint=PAL["glow"],
        amount=0.8)
    ground(lay, D + "Ground/z2_ground_fill.png", D + "Ground/z2_ground_top.png", 51, ys)
    px0, px1 = 300, 640
    t = np.clip((xs - px0) / (px1 - px0), 0, 1)
    pys = 330 - 12 * np.sin(t * math.pi)
    pyb = pys + 20 + 70 * np.clip(np.sin(t * math.pi), 0, 1) ** 0.6
    ground(lay, D + "Ground/z2_ground_fill.png", D + "Ground/z2_ground_top.png", 51, pys,
           D + "Ground/z2_ground_bottom.png", 6, pyb, px0, px1)
    put(lay, D + "Props/z2_prop_mushrooms.png", 180, ys[180] + 8, 0.7)
    put(gl, D + "Props/z2_prop_mushrooms_glow.png", 180, ys[180] + 8, 0.7, mode="add", tint=PAL["glow"])
    put(lay, D + "Props/z2_prop_bounce_pod.png", 780, ys[780] + 8, 0.75)
    put(gl, D + "Props/z2_prop_bounce_pod_glow.png", 780, ys[780] + 8, 0.75, mode="add", tint=PAL["glow"], amount=0.6)
    for fx, fy in [(420, 140), (470, 170), (520, 140), (570, 170)]:
        put(lay, D + "Props/z2_prop_fruit.png", fx, fy, 1.0)
        put(gl, D + "Props/z2_prop_fruit_glow.png", fx, fy, 1.0, mode="add", tint=PAL["glow"])
    hero(lay, 560, int(ys[560]) + 2)
    particles(gl, "Common/Particles/firefly.png", [(250, 420), (900, 300), (620, 200)], 16, 80, (0.4, 0.9),
              PAL["glow"], 0.8, 17)
    particles(gl, "Common/Particles/mote.png", [(800, 450), (420, 250), (1100, 200)], 30, 90, (0.25, 0.7),
              hexc("7ff0e0"), 0.5, 18)
    bloom(lay, gl.rgb, 0.8)
    put(lay, D + "Foreground/z2_fg_vines.png", 60, 520, 0.7)
    put(lay, D + "Foreground/z2_fg_roots.png", 1150, 800, 0.6)
    put(lay, D + "Foreground/z2_fg_canopy.png", 700, 120, 0.6)
    return save_preview(lay, "z2_preview.png")
