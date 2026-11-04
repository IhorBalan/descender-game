"""Shared particle sprites and the preview (mock screenshot) composer."""
from __future__ import annotations

import math
import os

from PIL import Image

from pa_core import F32, OUT, PREVIEW_DIR, Layer, from_image, gblur, np, smoothstep, to_image, wrapd
from pa_shapes import (particle_firefly, particle_light_shaft, particle_mote, particle_petals, particle_pollen)

CDIR = "Common/Particles"


def build(reg):
    for name, fn, desc, blend in [
        ("mote", particle_mote, "Soft round glow mote (tint per zone)", "additive"),
        ("firefly", particle_firefly, "Bright core + halo", "additive"),
        ("pollen", particle_pollen, "Tiny fuzzy pollen speck", "additive"),
        ("petal_strip", particle_petals, "Three petal variants, 32x32 frames in a 96x32 strip (light, tint pink/gold)",
         "normal"),
        ("light_shaft", particle_light_shaft, "Soft vertical light ray, widens downward (additive, tint)", "additive"),
    ]:
        rgb, a = fn()
        extra = {"frames": 3, "frameSize": [32, 32]} if name == "petal_strip" else None
        piv = (0.5, 1.0) if name == "light_shaft" else (0.5, 0.5)
        reg.save(to_image(rgb, a), f"{CDIR}/{name}.png", desc, "common", pivot=piv, blend=blend, extra=extra)


# ------------------------------------------------------------------ preview

PW, PH = 1280, 720


def load(rel):
    return from_image(Image.open(os.path.join(OUT, rel)))


def scaled(rel, s):
    img = Image.open(os.path.join(OUT, rel)).convert("RGBA")
    if s != 1:
        # premultiplied resize
        arr = np.asarray(img).astype(F32) / 255
        pm = arr.copy()
        pm[..., :3] *= pm[..., 3:4]
        chans = [np.asarray(Image.fromarray(pm[..., c], "F").resize((max(1, int(img.width * s)),
                                                                     max(1, int(img.height * s))), Image.LANCZOS))
                 for c in range(4)]
        pm = np.clip(np.stack(chans, -1), 0, 1)
        a = pm[..., 3]
        rgb = np.where(a[..., None] > 1e-3, pm[..., :3] / np.maximum(a[..., None], 1e-3), 0)
        return np.clip(rgb, 0, 1), a
    return from_image(img)


def put(lay, rel, x, y_bottom, s=1.0, mode="over", tint=None, amount=1.0, flip=False):
    rgb, a = scaled(rel, s)
    if flip:
        rgb, a = rgb[:, ::-1], a[:, ::-1]
    if tint is not None:
        rgb = rgb * np.asarray(tint, F32)
    h, w = a.shape
    if mode == "add":
        lay.blit(rgb * a[..., None] * amount, a * amount, x - w / 2, y_bottom - h, premult=True, mode="add")
    else:
        lay.blit(rgb, a * amount, x - w / 2, y_bottom - h)


def backdrop(lay, rel, shift, s=0.625):
    rgb, a = scaled(rel, s)
    rgb = np.roll(rgb, -int(shift), axis=1)[:PH, :PW]
    a = np.roll(a, -int(shift), axis=1)[:PH, :PW]
    lay.blit(rgb, a, 0, 0)


def ground(lay, fill_rel, top_rel, S, ys, bot_rel=None, R=None, yb=None, x0=0, x1=PW, fill_off=(0, 0)):
    frgb, fa = load(fill_rel)
    trgb, ta = load(top_rel)
    Y, X = np.mgrid[0:PH, 0:PW]
    ysx = ys[X]
    inx = (X >= x0) & (X < x1)
    endfade = np.clip(np.minimum(X - x0, x1 - 1 - X) / 10.0, 0, 1)
    body = (Y >= ysx + 6) & inx
    if yb is not None:
        body &= Y <= yb[X]
    fx, fy = (X + fill_off[0]) % 512, (Y + fill_off[1]) % 512
    lay.blit(frgb[fy, fx] * 1.0, body.astype(F32), 0, 0)
    if bot_rel is not None:
        brgb, ba = load(bot_rel)
        r = Y - yb[X].astype(int) + R
        ok = (r >= 0) & (r < ba.shape[0]) & inx
        rr = np.clip(r, 0, ba.shape[0] - 1)
        lay.blit(brgb[rr, X % 512], ba[rr, X % 512] * ok * endfade, 0, 0)
    r = Y - ysx.astype(int) + S
    ok = (r >= 0) & (r < ta.shape[0]) & inx
    rr = np.clip(r, 0, ta.shape[0] - 1)
    lay.blit(trgb[rr, X % 512], ta[rr, X % 512] * ok * endfade, 0, 0)


def hero(lay, x, y_bottom):
    """A 1 x 1.5 unit placeholder figure (64 x 96 px) for scale."""
    Y, X = np.mgrid[0:PH, 0:PW].astype(F32)
    cx, top = x, y_bottom - 96
    body = (np.abs(X - cx) < 18 - np.clip((Y - (top + 40)) * -0.1, 0, 6)) & (Y > top + 30) & (Y < y_bottom)
    head = np.hypot(X - cx, Y - (top + 20)) < 15
    m = gblur((body | head).astype(F32), 0.6)
    lay.over(np.asarray([0.10, 0.08, 0.12], F32), m)
    rim = np.clip(m - np.roll(m, -3, axis=1), 0, 1) * 0.0 + np.clip(m - np.roll(np.roll(m, 2, axis=0), -2, axis=1), 0, 1)
    lay.add_light(np.asarray([1.0, 0.9, 0.7], F32), rim * 0.6)


def particles(lay, rel, centers, n_each, spread, size_range, tint, amount, seed, mode="add", frames=None):
    rng = np.random.default_rng(seed)
    rgb0, a0 = load(rel)
    for cx, cy in centers:
        for _ in range(n_each):
            x = cx + rng.normal() * spread
            y = cy + rng.normal() * spread * 0.6
            s = rng.uniform(*size_range)
            rgb, a = rgb0, a0
            if frames:
                f = rng.integers(0, frames)
                fw = a0.shape[1] // frames
                rgb, a = rgb0[:, f * fw:(f + 1) * fw], a0[:, f * fw:(f + 1) * fw]
            w = max(2, int(a.shape[1] * s))
            im = to_image(rgb, a).resize((w, w), Image.LANCZOS)
            r2, a2 = from_image(im)
            k = amount * rng.uniform(0.4, 1.0)
            if mode == "add":
                lay.blit(r2 * np.asarray(tint, F32) * a2[..., None] * k, a2 * k, x - w / 2, y - w / 2, premult=True,
                         mode="add")
            else:
                lay.blit(r2 * np.asarray(tint, F32), a2 * k, x - w / 2, y - w / 2)


def bloom(lay, glow_rgb, strength=0.8):
    """Simulate camera bloom from an additive glow buffer."""
    b = gblur(glow_rgb, 6) * 0.6 + gblur(glow_rgb, 22) * 0.5
    lay.rgb = lay.rgb + glow_rgb + b * strength


def save_preview(lay, name):
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    rgb, a = lay.straight()
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
    path = os.path.join(PREVIEW_DIR, name)
    img.save(path)
    print("  preview", path)
    return path
