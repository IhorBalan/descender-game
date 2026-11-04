"""Helpers for horizontally tileable backdrop layers and edge strips."""
from __future__ import annotations

import math

from pa_core import F32, Layer, MaskCanvas, frame, gblur, grad3, lerp, np, smoothstep, vnoise, wrapd
from pa_shapes import leaf_geometry, tendril_pts


def periodic_curve(W, base, terms, seed, step=4):
    """y(x) = base + sum a*sin(2*pi*f*x/W + phase), f integer -> tiles in x."""
    rng = np.random.default_rng(seed)
    xs = np.arange(0, W + step, step, dtype=F32)
    y = np.full_like(xs, base)
    for f, a in terms:
        y += a * np.sin(2 * math.pi * f * xs / W + rng.random() * 6.283)
    return xs, y


def curve_at(xs, ys, x, W):
    return float(np.interp(x % W, xs, ys))


def fill_below(mc, xs, ys, H, W, ox=0):
    pts = [(x + ox, y) for x, y in zip(xs, ys)] + [(xs[-1] + ox, H + 4), (xs[0] + ox, H + 4)]
    mc.poly(pts)


def mask_ribbon(mc, pts, hwl, hwr, x_off=0.0):
    """A ribbon into a MaskCanvas (one quad per segment)."""
    _, nrm = frame(pts)
    L = pts + nrm * np.asarray(hwl)[:, None]
    R = pts - nrm * np.asarray(hwr)[:, None]
    for i in range(len(pts) - 1):
        mc.poly([(L[i][0] + x_off, L[i][1]), (L[i + 1][0] + x_off, L[i + 1][1]),
                 (R[i + 1][0] + x_off, R[i + 1][1]), (R[i][0] + x_off, R[i][1])])


def wrapped(W, x, fn):
    """Call fn(offset) for the copies needed so a shape near x tiles in W."""
    for off in (-W, 0, W):
        fn(off)


def leaf_shape(mc, W, x, y, L, theta0, width, turns=1.2, k0=0.0, p=3.0, seed=0, peak=0.3, base=0.1, wobble=0.0,
               curl_narrow=0.55, n=240, fall=1.0, tipw=0.0):
    pts, hwl, hwr = leaf_geometry(L, theta0, width, turns, k0, p, peak, base, tipw, fall, 1.0, n, wobble, seed,
                                  curl_narrow)
    pts = pts + np.array([x, y], F32)
    wrapped(W, x, lambda off: mask_ribbon(mc, pts, hwl, hwr, off))
    return pts, hwl, hwr


def stem_shape(mc, W, x, y, L, theta0, w0, w1, turns=0.0, p=2.5, k0=0.0, seed=0, wobble=0.0, n=160):
    pts = tendril_pts(L, theta0, turns, p, k0, n, wobble, seed) + np.array([x, y], F32)
    hw = np.linspace(w0, w1, len(pts)) / 2
    wrapped(W, x, lambda off: mask_ribbon(mc, pts, hw, hw, off))
    return pts


def vertical_fog(H, y0, y1, amount):
    y = np.arange(H, dtype=F32)
    return (smoothstep(y0, y1, y) * amount)[:, None]


def tint_layer(m, rgb, wrap_x=True, blur_px=0.0):
    lay = Layer(m.shape[1], m.shape[0])
    if blur_px:
        m = gblur(m, blur_px, wrap_x=wrap_x)
    lay.set_straight(rgb, m)
    return lay


def sky_ramp(stops, H):
    y = np.arange(H, dtype=F32) / (H - 1)
    return grad3(stops, y)  # (H,3)
