"""Core helpers for the Paradise art generator: noise, blur, layers, ribbons
(leaves, stems, fronds), pillow shading and the asset registry.

Everything works on float32 numpy arrays in 0..1. Layers keep premultiplied
colour internally and are written out as straight-alpha RGBA PNGs.
"""
from __future__ import annotations

import glob
import json
import math
import os
import sys

try:  # numpy may live outside this interpreter's site-packages on this machine
    import numpy as np
except ImportError:  # pragma: no cover - environment fallback
    _cands = [os.environ.get("PARADISE_NUMPY_PATH", "")]
    _cands += sorted(glob.glob(r"C:\Program Files\Blender Foundation\Blender*\*\python\lib\site-packages"), reverse=True)
    for _p in _cands:
        if _p and os.path.isdir(os.path.join(_p, "numpy")):
            sys.path.append(_p)
            break
    import numpy as np  # noqa: E402

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOL_DIR))
OUT = os.path.join(ROOT, "Assets", "Art", "Paradise")
PREVIEW_DIR = os.path.join(TOOL_DIR, "previews")
PPU = 64

F32 = np.float32


# ------------------------------------------------------------------ basics

def c3(rgb) -> np.ndarray:
    """(r,g,b) 0..255 -> float32 array 0..1 (shape 3)."""
    return np.asarray(rgb, dtype=F32) / 255.0


def hexc(h: str) -> np.ndarray:
    h = h.lstrip("#")
    return c3((int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)))


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    t = np.asarray(t, dtype=F32)
    if t.ndim and np.ndim(a) and np.shape(a)[-1] == 3 and t.shape[-1:] != (3,):
        t = t[..., None]
    return a + (b - a) * t


def grad3(stops, t):
    """Piecewise-linear colour ramp. stops: [(pos, rgb01), ...] sorted."""
    t = np.asarray(t, dtype=F32)
    out = np.zeros(t.shape + (3,), dtype=F32)
    pos = [p for p, _ in stops]
    cols = [np.asarray(c, dtype=F32) for _, c in stops]
    for ch in range(3):
        out[..., ch] = np.interp(t, pos, [c[ch] for c in cols])
    return out


def wrapd(d, period):
    """Signed shortest distance on a circle of `period`."""
    return (d + period / 2) % period - period / 2


# ------------------------------------------------------------------ blur

def _blur_axis(a, s, axis, wrap):
    if s <= 0.05:
        return a
    n = a.shape[axis]
    p = 0
    if not wrap:
        p = int(3.2 * s) + 2
        padw = [(0, 0)] * a.ndim
        padw[axis] = (p, p)
        a = np.pad(a, padw, mode="edge")
    m = a.shape[axis]
    f = np.fft.rfftfreq(m).astype(F32)
    g = np.exp(-2 * (math.pi * s * f) ** 2).astype(F32)
    shape = [1] * a.ndim
    shape[axis] = len(f)
    r = np.fft.irfft(np.fft.rfft(a, axis=axis) * g.reshape(shape), n=m, axis=axis).astype(F32)
    if p:
        sl = [slice(None)] * a.ndim
        sl[axis] = slice(p, p + n)
        r = r[tuple(sl)]
    return r


def gblur(a, sigma, wrap_x=False, wrap_y=False, sigma_y=None):
    """Gaussian blur (sigma in px) of an (H,W) or (H,W,C) float array; edges
    either wrap (for tileable images) or clamp."""
    a = np.asarray(a, dtype=F32)
    sy = sigma if sigma_y is None else sigma_y
    a = _blur_axis(a, sy, 0, wrap_y)
    a = _blur_axis(a, sigma, 1, wrap_x)
    return a


# ------------------------------------------------------------------ noise

def vnoise(w, h, scale, seed, octaves=4, gain=0.5, wrap_x=False, wrap_y=False, sx=None, sy=None):
    """Fractal value noise 0..1. `scale` = feature size in px. Periodic on
    wrapped axes (grid cells chosen to divide the size)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), dtype=F32)
    amp, total = 1.0, 0.0
    scx = scale if sx is None else sx
    scy = scale if sy is None else sy
    for _ in range(octaves):
        gw = max(2, int(round(w / scx)))
        gh = max(2, int(round(h / scy)))
        g = rng.random((gh, gw), dtype=F32)
        g3 = np.tile(g, (3, 3))
        big = Image.fromarray(g3, mode="F").resize((w * 3, h * 3), Image.BICUBIC)
        layer = np.asarray(big)[h:2 * h, w:2 * w]
        out += layer * amp
        total += amp
        amp *= gain
        scx = max(1.0, scx / 2)
        scy = max(1.0, scy / 2)
    out /= total
    # stretch to roughly 0..1
    lo, hi = np.percentile(out, 1), np.percentile(out, 99)
    return np.clip((out - lo) / max(1e-6, hi - lo), 0, 1).astype(F32)


# ------------------------------------------------------------------ layer

class Layer:
    """Premultiplied RGBA float canvas."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgb = np.zeros((h, w, 3), dtype=F32)
        self.a = np.zeros((h, w), dtype=F32)

    def over(self, rgb, alpha):
        """Paint straight colour `rgb` (3 or HxWx3) with coverage `alpha` over everything."""
        alpha = np.clip(np.asarray(alpha, dtype=F32), 0, 1)
        rgb = np.asarray(rgb, dtype=F32)
        self.rgb = rgb * alpha[..., None] + self.rgb * (1 - alpha[..., None])
        self.a = alpha + self.a * (1 - alpha)

    def under(self, rgb, alpha):
        alpha = np.clip(np.asarray(alpha, dtype=F32), 0, 1)
        rgb = np.asarray(rgb, dtype=F32)
        self.rgb = self.rgb + rgb * alpha[..., None] * (1 - self.a[..., None])
        self.a = self.a + alpha * (1 - self.a)

    def add_light(self, rgb, amount):
        """Additive light on top (also raises alpha so it shows over transparency)."""
        amount = np.clip(np.asarray(amount, dtype=F32), 0, None)
        self.rgb = self.rgb + np.asarray(rgb, dtype=F32) * amount[..., None]
        self.a = np.maximum(self.a, np.clip(amount, 0, 1))

    def blit(self, s_rgb, s_a, x0, y0, wrap_x=False, premult=False, mode="over"):
        """Composite a sprite (straight rgb unless premult) at integer x0,y0."""
        sh, sw = s_a.shape
        x0, y0 = int(round(x0)), int(round(y0))
        offsets = [0]
        if wrap_x:
            offsets = [0, -self.w, self.w]
        for off in offsets:
            x = x0 + off
            ax0, ay0 = max(0, x), max(0, y0)
            ax1, ay1 = min(self.w, x + sw), min(self.h, y0 + sh)
            if ax0 >= ax1 or ay0 >= ay1:
                continue
            sx0, sy0 = ax0 - x, ay0 - y0
            sa = s_a[sy0:sy0 + ay1 - ay0, sx0:sx0 + ax1 - ax0]
            sr = s_rgb[sy0:sy0 + ay1 - ay0, sx0:sx0 + ax1 - ax0]
            if not premult:
                sr = sr * sa[..., None]
            dr = self.rgb[ay0:ay1, ax0:ax1]
            da = self.a[ay0:ay1, ax0:ax1]
            if mode == "add":
                self.rgb[ay0:ay1, ax0:ax1] = dr + sr
                self.a[ay0:ay1, ax0:ax1] = np.maximum(da, np.clip(sa, 0, 1))
            else:
                self.rgb[ay0:ay1, ax0:ax1] = sr + dr * (1 - sa[..., None])
                self.a[ay0:ay1, ax0:ax1] = sa + da * (1 - sa)

    def straight(self):
        a = self.a[..., None]
        rgb = np.where(a > 1e-4, self.rgb / np.maximum(a, 1e-4), 0)
        return np.clip(rgb, 0, 1), np.clip(self.a, 0, 1)

    def set_straight(self, rgb, a=None):
        if a is not None:
            self.a = np.clip(a, 0, 1).astype(F32)
        self.rgb = (np.asarray(rgb, dtype=F32) * self.a[..., None]).astype(F32)

    def image(self):
        rgb, a = self.straight()
        return to_image(rgb, a)


def to_image(rgb, a):
    rgb = np.clip(rgb, 0, 1)
    a = np.clip(a, 0, 1)
    # straight alpha; clear colour where fully transparent (keeps PNGs small)
    rgb = np.where(a[..., None] > 0.002, rgb, 0)
    arr = np.concatenate([rgb, a[..., None]], axis=2)
    return Image.fromarray((arr * 255 + 0.5).astype(np.uint8), "RGBA")


def from_image(img):
    arr = np.asarray(img.convert("RGBA")).astype(F32) / 255
    return arr[..., :3], arr[..., 3]


# ------------------------------------------------------------------ masks

class MaskCanvas:
    """Supersampled PIL drawing of a coverage mask."""

    def __init__(self, w, h, ss=2):
        self.w, self.h, self.ss = w, h, ss
        self.img = Image.new("L", (w * ss, h * ss), 0)
        self.d = ImageDraw.Draw(self.img)

    def _p(self, pts):
        s = self.ss
        return [(float(x) * s, float(y) * s) for x, y in pts]

    def poly(self, pts, v=255):
        self.d.polygon(self._p(pts), fill=v, outline=v)

    def circle(self, x, y, r, v=255):
        s = self.ss
        self.d.ellipse([(x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s], fill=v)

    def ellipse(self, x, y, rx, ry, v=255):
        s = self.ss
        self.d.ellipse([(x - rx) * s, (y - ry) * s, (x + rx) * s, (y + ry) * s], fill=v)

    def line(self, pts, width, v=255):
        self.d.line(self._p(pts), fill=v, width=max(1, int(round(width * self.ss))), joint="curve")

    def taper_line(self, pts, w0, w1, v=255, v1=None):
        """Polyline with width tapering w0 -> w1, drawn as a quad strip."""
        pts = np.asarray(pts, dtype=F32)
        n = len(pts)
        if n < 2:
            return
        hw = np.linspace(w0, w1, n) / 2
        tang = np.gradient(pts, axis=0)
        tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-6)
        nrm = np.stack([-tang[:, 1], tang[:, 0]], 1)
        L = pts + nrm * hw[:, None]
        R = pts - nrm * hw[:, None]
        vv = np.linspace(v, v if v1 is None else v1, n)
        for i in range(n - 1):
            self.poly([L[i], L[i + 1], R[i + 1], R[i]], int(vv[i]))
        if v1 is None:
            for i in range(0, n, max(1, n // 6)):
                if hw[i] * self.ss > 1.5:
                    self.circle(pts[i][0], pts[i][1], hw[i], v)

    def get(self):
        im = self.img.resize((self.w, self.h), Image.BOX) if self.ss > 1 else self.img
        return np.asarray(im).astype(F32) / 255


# ------------------------------------------------------------------ spines

def euler_spine(L, theta0, k0=0.0, k1=0.0, p=3.0, n=400, wobble=0.0, seed=0):
    """A curve of length L whose curvature ramps from k0 to k0+k1 (power p):
    the tip coils into a natural spiral. Returns (n,2) points starting at 0,0.
    Angles in radians, y down (positive k turns clockwise on screen)."""
    s = np.linspace(0, L, n, dtype=F32)
    t = s / L
    kap = k0 + k1 * t ** p
    if wobble:
        rng = np.random.default_rng(seed)
        ph = rng.random(3) * 6.28
        kap = kap + wobble * (np.sin(t * 7.1 + ph[0]) + 0.6 * np.sin(t * 13.7 + ph[1])) / L
    ds = L / (n - 1)
    th = theta0 + np.concatenate([[0], np.cumsum((kap[1:] + kap[:-1]) * 0.5 * ds)])
    d = np.stack([np.cos(th), np.sin(th)], 1) * ds
    pts = np.concatenate([[[0, 0]], np.cumsum((d[1:] + d[:-1]) * 0.5, 0)], 0)
    return pts.astype(F32)


def turns_k1(turns, L, p):
    """k1 so the curl adds `turns` full turns over length L."""
    return turns * 2 * math.pi * (p + 1) / L


def leaf_width(t, base=0.08, peak=0.35, tipw=0.0, power=1.0):
    """A leaf-like half-width profile 0..1 along t."""
    t = np.asarray(t, dtype=F32)
    rise = smoothstep(0, peak, t) ** 0.7
    fall = np.clip((1 - t) / (1 - peak), 0, 1) ** power
    w = np.where(t < peak, base + (1 - base) * rise, tipw + (1 - tipw) * fall)
    return w.astype(F32)


def frame(pts):
    tang = np.gradient(pts, axis=0)
    tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-6)
    nrm = np.stack([-tang[:, 1], tang[:, 0]], 1)
    return tang.astype(F32), nrm.astype(F32)


def at_ts(pts, hwl, hwr, t, s):
    """Position at (t along, s across in -1..1; s>0 is the 'left' normal side)."""
    n = len(pts)
    tang, nrm = frame(pts)
    f = np.clip(np.asarray(t, dtype=F32), 0, 1) * (n - 1)
    i0 = np.clip(np.floor(f).astype(int), 0, n - 2)
    u = (f - i0)[..., None]
    p = pts[i0] * (1 - u) + pts[i0 + 1] * u
    nn = nrm[i0] * (1 - u) + nrm[i0 + 1] * u
    s = np.asarray(s, dtype=F32)
    wl = hwl[i0] * (1 - u[..., 0]) + hwl[i0 + 1] * u[..., 0]
    wr = hwr[i0] * (1 - u[..., 0]) + hwr[i0 + 1] * u[..., 0]
    w = np.where(s > 0, wl, wr) * s
    return p + nn * w[..., None]


class RibbonCanvas:
    """Rasterises ribbons (a spine with a half-width on each side) into a mask
    plus smooth attribute maps: t (along), s (across, -1..1), face (how much
    that half of the ribbon faces the light) and part id."""

    def __init__(self, w, h, ss=2, light=(0.55, -0.83)):
        self.w, self.h, self.ss = w, h, ss
        W, H = w * ss, h * ss
        self.m = Image.new("L", (W, H), 0)
        self.t = Image.new("F", (W, H), 0)
        self.s = Image.new("F", (W, H), 0)
        self.f = Image.new("F", (W, H), 0)
        self.pid = Image.new("F", (W, H), 0)
        self.dm, self.dt, self.ds = ImageDraw.Draw(self.m), ImageDraw.Draw(self.t), ImageDraw.Draw(self.s)
        self.df, self.dp = ImageDraw.Draw(self.f), ImageDraw.Draw(self.pid)
        L = np.asarray(light, dtype=F32)
        self.light = L / np.linalg.norm(L)

    def ribbon(self, pts, hwl, hwr, K=8, part=0.0, t0=0.0, t1=1.0, step=1):
        pts = np.asarray(pts, dtype=F32)
        hwl = np.broadcast_to(np.asarray(hwl, dtype=F32), (len(pts),))
        hwr = np.broadcast_to(np.asarray(hwr, dtype=F32), (len(pts),))
        _, nrm = frame(pts)
        ss = self.ss
        ks = np.linspace(-1, 1, K + 1)
        n = len(pts)
        idx = list(range(0, n, step))
        if idx[-1] != n - 1:
            idx.append(n - 1)
        for a, b in zip(idx[:-1], idx[1:]):
            tv = t0 + (t1 - t0) * (a + b) * 0.5 / (n - 1)
            for k in range(K):
                s0, s1 = ks[k], ks[k + 1]
                sm = (s0 + s1) / 2
                side = 1.0 if sm > 0 else -1.0
                fv = float(np.dot(nrm[a] * side, self.light))

                def c(i, s):
                    w = hwl[i] if s > 0 else hwr[i]
                    p = pts[i] + nrm[i] * (w * s)
                    return (float(p[0]) * ss, float(p[1]) * ss)

                poly = [c(a, s0), c(a, s1), c(b, s1), c(b, s0)]
                self.dm.polygon(poly, fill=255, outline=255)
                self.dt.polygon(poly, fill=float(tv), outline=float(tv))
                self.ds.polygon(poly, fill=float(sm), outline=float(sm))
                self.df.polygon(poly, fill=fv, outline=fv)
                self.dp.polygon(poly, fill=float(part), outline=float(part))

    def blob(self, x, y, r, part=0.0, face=0.0):
        """A filled disc (for knobs, fruit, round joins)."""
        ss = self.ss
        bb = [(x - r) * ss, (y - r) * ss, (x + r) * ss, (y + r) * ss]
        self.dm.ellipse(bb, fill=255)
        self.dt.ellipse(bb, fill=0.0)
        self.ds.ellipse(bb, fill=0.0)
        self.df.ellipse(bb, fill=float(face))
        self.dp.ellipse(bb, fill=float(part))

    def maps(self, smooth=1.2):
        size = (self.w, self.h)
        mss = np.asarray(self.m).astype(F32) / 255
        m = np.asarray(self.m.resize(size, Image.BOX)).astype(F32) / 255
        out = {"m": m}
        for key, im in (("t", self.t), ("s", self.s), ("f", self.f)):
            arr = np.asarray(im).astype(F32) * mss
            a = np.asarray(Image.fromarray(arr, "F").resize(size, Image.BOX))
            v = a / np.maximum(m, 1e-3)
            if smooth:
                num = gblur(v * m, smooth)
                den = gblur(m, smooth)
                v = np.where(den > 1e-3, num / np.maximum(den, 1e-3), v)
            out[key] = v.astype(F32)
        out["part"] = np.asarray(self.pid.resize(size, Image.NEAREST)).astype(F32)
        return out


# ------------------------------------------------------------------ shading

def pillow_normals(mask, sigma, strength=1.0, wrap_x=False):
    """Soft 'inflated' normals from a blurred silhouette: round, pillowy shading."""
    h = gblur(mask, sigma, wrap_x=wrap_x)
    gy, gx = np.gradient(h)
    k = strength * sigma * 2.2
    nx, ny = -gx * k, -gy * k
    nz = np.ones_like(nx)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    return nx / ln, ny / ln, nz / ln


def lambert(nx, ny, nz, L):
    L = np.asarray(L, dtype=F32)
    L = L / np.linalg.norm(L)
    return np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)


def inner_edge(mask, r, wrap_x=False):
    """1 just inside the silhouette edge, fading inward over ~r px."""
    b = gblur(mask, r, wrap_x=wrap_x)
    return np.clip(mask * (1 - b) * 2.2, 0, 1)


def rim_light(mask, r, L, wrap_x=False):
    """Edge light on the side of a silhouette that faces 2D light dir L=(lx,ly)."""
    nx, ny, _ = pillow_normals(mask, r, 3.0, wrap_x)
    L = np.asarray(L, dtype=F32)
    L = L / np.linalg.norm(L)
    facing = np.clip(nx * L[0] + ny * L[1], 0, 1)
    return inner_edge(mask, r * 0.8, wrap_x) * smoothstep(0.1, 0.7, facing)


def glow_disc(size, power=2.0, core=0.0):
    y, x = np.mgrid[0:size, 0:size].astype(F32)
    r = np.hypot(x - (size - 1) / 2, y - (size - 1) / 2) / (size / 2)
    a = np.clip(1 - r, 0, 1) ** power
    if core:
        a = np.maximum(a, np.exp(-(r / core) ** 2))
    return a


def radial(w, h, cx, cy, r, wrap_x=False):
    y, x = np.mgrid[0:h, 0:w].astype(F32)
    dx = x - cx
    if wrap_x:
        dx = wrapd(dx, w)
    return np.hypot(dx, y - cy) / r


# ------------------------------------------------------------------ text

def font(size):
    return ImageFont.load_default(size=size)


# ------------------------------------------------------------------ registry

class Registry:
    def __init__(self):
        self.items = []

    def save(self, img, rel, purpose, zone, pivot=(0.5, 0.5), blend="normal", tile=(), extra=None, ppu=PPU):
        path = os.path.join(OUT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        if not isinstance(img, Image.Image):
            img = img.image()
        img.save(path, optimize=True)
        rec = {
            "path": "Assets/Art/Paradise/" + rel.replace("\\", "/"),
            "purpose": purpose,
            "zone": zone,
            "size": [img.width, img.height],
            "sizeUnits": [round(img.width / ppu, 3), round(img.height / ppu, 3)],
            "pivot": [round(float(pivot[0]), 4), round(float(pivot[1]), 4)],
            "ppu": ppu,
            "blend": blend,
            "tileable": list(tile),
        }
        if extra:
            rec.update(extra)
        self.items = [i for i in self.items if i["path"] != rec["path"]]
        self.items.append(rec)
        print(f"  wrote {rec['path']} {img.width}x{img.height}")
        return img

    def write_manifest(self):
        path = os.path.join(OUT, "manifest.json")
        old = []
        if os.path.exists(path):
            try:
                with open(path) as f:
                    old = json.load(f).get("files", [])
            except Exception:
                old = []
        mine = {i["path"] for i in self.items}
        files = [o for o in old if o["path"] not in mine and os.path.exists(os.path.join(ROOT, o["path"]))] + self.items
        files.sort(key=lambda r: r["path"])
        doc = {
            "generator": "tools/paradise_art/gen_paradise_art.py",
            "pixelsPerUnit": PPU,
            "notes": [
                "PNG RGBA, straight (non-premultiplied) alpha.",
                "pivot is normalised (0,0 = bottom-left, Unity convention).",
                "blend 'additive' files are emissive: glowing parts only on black/transparent.",
                "Edge strips: surfaceRow is the pixel row (0 = top of the image) where the walkable line sits.",
                "Backdrops are 2048x1152 = 32x18 units at 64 PPU; tileable ones repeat seamlessly in x.",
            ],
            "files": files,
        }
        with open(path, "w") as f:
            json.dump(doc, f, indent=2)
            f.write("\n")
        print(f"manifest: {len(files)} files")


def _cr_axis(g, n_out, axis, wrap):
    """Catmull-Rom upsample of grid g along axis to n_out samples (periodic if wrap)."""
    n = g.shape[axis]
    pos = (np.arange(n_out, dtype=np.float64) + 0.5) * n / n_out - 0.5
    i1 = np.floor(pos).astype(int)
    t = (pos - i1).astype(F32)
    idx = [i1 - 1, i1, i1 + 1, i1 + 2]
    idx = [(i % n) if wrap else np.clip(i, 0, n - 1) for i in idx]
    p = [np.take(g, i, axis=axis) for i in idx]
    shape = [1] * g.ndim
    shape[axis] = n_out
    t = t.reshape(shape)
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p[1]) + (-p[0] + p[2]) * t + (2 * p[0] - 5 * p[1] + 4 * p[2] - p[3]) * t2 +
                  (-p[0] + 3 * p[1] - 3 * p[2] + p[3]) * t3)


def vnoise_fast(w, h, scale, seed, octaves=4, gain=0.5, wrap_x=False, wrap_y=False, sx=None, sy=None):
    """Same contract as vnoise, but separable Catmull-Rom (cheap for very wide images)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), dtype=F32)
    amp, total = 1.0, 0.0
    scx = scale if sx is None else sx
    scy = scale if sy is None else sy
    for _ in range(octaves):
        gw = max(2, int(round(w / scx)))
        gh = max(2, int(round(h / scy)))
        g = rng.random((gh, gw), dtype=F32)
        a = _cr_axis(g, w, 1, wrap_x)
        a = _cr_axis(a, h, 0, wrap_y)
        out += a.astype(F32) * amp
        total += amp
        amp *= gain
        scx = max(1.0, scx / 2)
        scy = max(1.0, scy / 2)
    out /= total
    sub = out[::4, ::4] if out.size > 4_000_000 else out
    lo, hi = np.percentile(sub, 1), np.percentile(sub, 99)
    return np.clip((out - lo) / max(1e-6, hi - lo), 0, 1).astype(F32)
