#!/usr/bin/env python3
"""Draw a region's art from code: the parallax layers, the edge pieces, the
particle sprites, and the tile textures for the map editor.

    python3 tools/gen_region_art.py [paradise ruined_earth limbo ...] [--textures DIR]

Needs Pillow and numpy. Everything is generated so it can be regenerated or
replaced piece by piece: drop painted PNGs with the same names into
assets/regions/<key>/ and the JSON next to them keeps working. The tile
textures go to DIR (default ../editor/art/<key>/) as .lpx.json files the
editor imports, or that scripts/upload-textures.ts in the editor writes over
the region's existing textures.

See scripts/edge_decor.gd, scripts/backdrop.gd and scripts/region_fx.gd for
what the JSON files mean.
"""
from __future__ import annotations

import base64
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = 64  # pixels per tile

# ---------------------------------------------------------------- helpers


def fbm(w: int, h: int, scale: float, seed: int, octaves: int = 5, gain: float = 0.5, tileable: bool = False) -> np.ndarray:
    """Smooth value noise in 0..1, fractal over `octaves`."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), dtype=np.float32)
    amp, total, s = 1.0, 0.0, scale
    for _ in range(octaves):
        gw, gh = max(2, int(round(w / s))), max(2, int(round(h / s)))
        g = rng.random((gh, gw), dtype=np.float32)
        if tileable:
            g3 = np.tile(g, (3, 3))
            big = Image.fromarray(g3, mode="F").resize((w * 3, h * 3), Image.BICUBIC)
            layer = np.asarray(big)[h:2 * h, w:2 * w]
        else:
            layer = np.asarray(Image.fromarray(g, mode="F").resize((w, h), Image.BICUBIC))
        out += layer * amp
        total += amp
        amp *= gain
        s = max(1.0, s / 2)
    out /= total
    return np.clip(out, 0, 1)


def blur(v: np.ndarray, radius: float) -> np.ndarray:
    """A gaussian blur of a 0..1 field (through an 8-bit image, which is plenty here)."""
    img = Image.fromarray((np.clip(v, 0, 1) * 255 + 0.5).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(radius))
    return np.asarray(img).astype(np.float32) / 255


def smoothstep(e0: float, e1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def col(c: tuple, shape) -> np.ndarray:
    return np.ones((*shape, 3), dtype=np.float32) * (np.array(c, dtype=np.float32) / 255)


def lerp(a: np.ndarray, b: np.ndarray, t: np.ndarray) -> np.ndarray:
    return a + (b - a) * t[..., None]


def to_image(rgb: np.ndarray, alpha: np.ndarray) -> Image.Image:
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)[..., None]], axis=2)
    return Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA")


def gray_image(v: np.ndarray) -> Image.Image:
    g = (np.clip(v, 0, 1) * 255 + 0.5).astype(np.uint8)
    return Image.fromarray(np.stack([g, g, g, np.full_like(g, 255)], axis=2), "RGBA")


def save_png(img: Image.Image, key: str, name: str) -> None:
    path = os.path.join(ROOT, "assets", "regions", key, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def write_json(key: str, name: str, data) -> None:
    with open(os.path.join(ROOT, "assets", "regions", key, name), "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


def lpx_doc(name: str, albedo: Image.Image, height: np.ndarray, specular: np.ndarray, emissive: Image.Image | None,
            tileable: bool, strength: float, tags: list[str]) -> dict:
    """A map-editor texture document (src/doc.ts): four same-sized RGBA layers, base64."""
    w, h = albedo.size
    if emissive is None:
        emissive = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    layers = {
        "albedo": albedo, "height": gray_image(height), "specular": gray_image(specular), "emissive": emissive,
    }
    return {
        "format": "lit-pixel-doc", "version": 1, "name": name, "tags": tags, "w": w, "h": h,
        "tileable": tileable, "normalStrength": strength,
        "layers": {k: base64.b64encode(v.tobytes()).decode("ascii") for k, v in layers.items()},
    }


def stroke(d: ImageDraw.ImageDraw, pts, color, width: int) -> None:
    d.line(pts, fill=color, width=width, joint="curve")


def soft_disc(size: int, color: tuple, power: float = 2.0) -> Image.Image:
    y, x = np.mgrid[0:size, 0:size]
    r = np.hypot(x - size / 2 + 0.5, y - size / 2 + 0.5) / (size / 2)
    a = np.clip(1 - r, 0, 1) ** power
    return to_image(col(color, (size, size)), a)


# ---------------------------------------------------------------- Paradise

P = {
    "sky_top": (58, 120, 212), "sky_bot": (214, 236, 250), "haze": (236, 244, 252),
    "cloud": (255, 255, 255), "cloud_shadow": (178, 198, 236), "cloud_deep": (146, 166, 216),
    "marble": (240, 232, 214), "marble2": (224, 212, 184), "marble_dark": (196, 176, 136), "mortar": (178, 160, 124),
    "gold": (226, 178, 70), "gold_light": (255, 241, 184), "gold_dark": (158, 116, 38),
    "grass": (150, 206, 98), "grass_light": (212, 240, 150), "grass_dark": (92, 154, 64), "moss": (62, 120, 58),
    "blossom": (255, 182, 220), "blossom2": (255, 140, 200), "white": (255, 255, 255),
    "rock": (142, 136, 124), "rock_dark": (96, 100, 128), "rock_light": (190, 184, 170), "root": (118, 88, 58),
    "water": (176, 232, 255), "wall": (186, 200, 230), "wall2": (168, 184, 220), "wall_dark": (138, 154, 196),
}


def marble_field(w: int, h: int, seed: int, tileable: bool) -> tuple[np.ndarray, np.ndarray]:
    """Marble colour (0..1 rgb) and a vein mask, seamless when `tileable`."""
    base = fbm(w, h, 48, seed, 4, 0.55, tileable)
    warp = fbm(w, h, 24, seed + 1, 4, 0.5, tileable)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    field = np.sin((xx / w * 2 + yy / h * 1.3) * math.pi * 2 + warp * 7.0) * 0.5 + 0.5
    veins = 1 - smoothstep(0.0, 0.06, np.abs(field - 0.5)) * 1.0
    veins = np.clip(veins, 0, 1) * (0.4 + 0.6 * fbm(w, h, 12, seed + 2, 3, 0.5, tileable))
    rgb = lerp(col(P["marble"], (h, w)), col(P["marble2"], (h, w)), base)
    rgb = lerp(rgb, col(P["marble_dark"], (h, w)), veins * 0.6)
    gold_veins = veins * smoothstep(0.55, 0.8, fbm(w, h, 30, seed + 3, 2, 0.5, tileable))
    rgb = lerp(rgb, col(P["gold"], (h, w)), gold_veins * 0.7)
    return rgb, veins


def block_grid(w: int, h: int, block: int, mortar: int, offset_rows: bool) -> tuple[np.ndarray, np.ndarray]:
    """Mortar mask (1 in the joints) and a bevel height for a running-bond block pattern."""
    yy, xx = np.mgrid[0:h, 0:w]
    row = yy // block
    x = (xx + (row % 2) * (block // 2 if offset_rows else 0)) % block
    y = yy % block
    dist = np.minimum(np.minimum(x, block - 1 - x), np.minimum(y, block - 1 - y)).astype(np.float32)
    joint = (dist < mortar).astype(np.float32)
    bevel = smoothstep(mortar, mortar + 4, dist)
    return joint, bevel


def tex_terrain(seed: int = 11) -> dict:
    w = h = 128
    rgb, veins = marble_field(w, h, seed, True)
    joint, bevel = block_grid(w, h, 64, 1, True)
    soft = blur(joint, 1.2)
    rgb = lerp(rgb, col(P["mortar"], (h, w)), np.clip(soft * 1.2, 0, 1) * 0.55)
    rgb = lerp(rgb, col(P["white"], (h, w)), (1 - bevel) * (1 - joint) * 0.08)
    # weathering: a faint mossy tint pooling in the lower part of each slab
    moss = fbm(w, h, 20, seed + 5, 3, 0.5, True) * smoothstep(0.55, 1.0, (np.mgrid[0:h, 0:w][0] % 64) / 64)
    rgb = lerp(rgb, col(P["grass_dark"], (h, w)), np.clip(moss - 0.35, 0, 1) * 0.5)
    height = 0.5 + 0.22 * bevel - 0.3 * np.clip(soft * 1.5, 0, 1) + (fbm(w, h, 8, seed + 9, 3, 0.5, True) - 0.5) * 0.08
    spec = 0.38 * (1 - joint) * (1 - veins * 0.5) + 0.05
    return lpx_doc("paradise_terrain", to_image(rgb, np.ones((h, w))), height, spec, None, True, 1.8, ["paradise", "terrain", "marble", "world tiles"])


def tex_wall(seed: int = 21) -> dict:
    w = h = 128
    base = fbm(w, h, 40, seed, 4, 0.5, True)
    rgb = lerp(col(P["wall"], (h, w)), col(P["wall2"], (h, w)), base)
    joint, bevel = block_grid(w, h, 64, 1, False)
    soft = blur(joint, 1.0)
    rgb = lerp(rgb, col(P["wall_dark"], (h, w)), np.clip(soft * 1.3, 0, 1) * 0.5)
    # a pilaster up the middle of every other column and a cornice line, so it reads as architecture
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    pil = 1 - smoothstep(4, 7, np.abs((xx % 128) - 64))
    pil_edge = smoothstep(3, 4.5, np.abs((xx % 128) - 64)) * pil
    rgb = lerp(rgb, col(P["wall_dark"], (h, w)), pil_edge * 0.45)
    rgb = lerp(rgb, col(P["wall"], (h, w)), (pil - pil_edge) * 0.5)
    cornice = 1 - smoothstep(1, 2.5, np.abs((yy % 128) - 30))
    rgb = lerp(rgb, col(P["wall_dark"], (h, w)), cornice * 0.35)
    height = 0.5 + 0.08 * bevel - 0.12 * np.clip(soft * 1.5, 0, 1) + 0.1 * pil - 0.05 * cornice
    spec = np.full((h, w), 0.1, dtype=np.float32)
    return lpx_doc("paradise_wall", to_image(rgb, np.ones((h, w))), height, spec, None, True, 0.8, ["paradise", "wall", "marble", "world tiles"])


def tex_platform(seed: int = 31) -> dict:
    """Four 64-px variants as quadrants: a gold-rimmed marble ledge in the top 28 px of each."""
    w = h = 128
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    alpha = np.zeros((h, w), dtype=np.float32)
    height = np.zeros((h, w), dtype=np.float32)
    spec = np.zeros((h, w), dtype=np.float32)
    marble, veins = marble_field(w, h, seed, False)
    rng = random.Random(seed)
    for qy in range(2):
        for qx in range(2):
            x0, y0 = qx * 64, qy * 64
            slab = 28
            yy, xx = np.mgrid[0:slab, 0:64].astype(np.float32)
            # body: marble, darker toward the underside
            body = marble[y0:y0 + slab, x0:x0 + 64].copy()
            body = lerp(body, col(P["marble_dark"], (slab, 64)), smoothstep(16, 27, yy) * 0.6)
            # gold rim on top with a bright line and a dark seam
            rim = (yy < 7).astype(np.float32)
            body = lerp(body, col(P["gold"], (slab, 64)), rim)
            body = lerp(body, col(P["gold_light"], (slab, 64)), ((yy >= 1) & (yy < 3)).astype(np.float32) * 0.8)
            body = lerp(body, col(P["gold_dark"], (slab, 64)), ((yy >= 6) & (yy < 8)).astype(np.float32) * 0.9)
            # ornament: small studs along the rim, different per variant
            for sx in range(8 + rng.randint(0, 6), 64, 16):
                stud = np.hypot(xx - sx, yy - 4) < 2.2
                body[stud] = np.array(P["gold_light"]) / 255
            # rounded ends: fade the corners
            end = np.minimum(xx, 63 - xx)
            a = np.ones((slab, 64), dtype=np.float32)
            a *= smoothstep(-1, 2, end - np.maximum(yy - 20, 0) * 0.6)
            rgb[y0:y0 + slab, x0:x0 + 64] = body
            alpha[y0:y0 + slab, x0:x0 + 64] = a
            height[y0:y0 + slab, x0:x0 + 64] = (0.85 - smoothstep(18, 27, yy) * 0.4) * a
            spec[y0:y0 + slab, x0:x0 + 64] = (0.7 * rim + 0.35 * (1 - rim)) * a
    return lpx_doc("paradise_platform", to_image(rgb, alpha), height, spec, None, False, 2.0, ["paradise", "platform", "world tiles"])


def tex_hazard() -> dict:
    w = h = 128
    return lpx_doc("paradise_hazard_none", Image.new("RGBA", (w, h), (0, 0, 0, 0)), np.zeros((h, w)), np.zeros((h, w)), None, True, 1.0, ["paradise", "hazard", "world tiles"])


def deco_doc(name: str, img: Image.Image, emissive: Image.Image | None, strength: float = 2.0, spec_value: float = 0.25) -> dict:
    a = np.asarray(img).astype(np.float32) / 255
    alpha = a[..., 3]
    # height from a blurred alpha: puffy toward the middle of each shape
    blurred = np.asarray(img.split()[3].filter(ImageFilter.GaussianBlur(2))).astype(np.float32) / 255
    height = np.clip(0.3 + 0.6 * blurred, 0, 1) * (alpha > 0.5)
    spec = np.full(alpha.shape, spec_value, dtype=np.float32) * (alpha > 0.5)
    return lpx_doc(name, img, height, spec, emissive, False, strength, ["paradise", "deco", "world tiles"])


def deco_grass(seed: int = 41) -> dict:
    rng = random.Random(seed)
    img = Image.new("RGBA", (ART, ART), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(26):
        x = rng.randint(6, ART - 7)
        hgt = rng.randint(14, 34)
        lean = rng.uniform(-0.5, 0.5)
        color = rng.choice([P["grass"], P["grass_light"], P["grass_dark"]])
        stroke(d, [(x, ART), (x + lean * hgt * 0.4, ART - hgt * 0.55), (x + lean * hgt, ART - hgt)], color, rng.randint(2, 3))
    for _ in range(3):
        x, y = rng.randint(10, ART - 10), rng.randint(ART - 30, ART - 12)
        for k in range(5):
            ang = k / 5 * math.tau
            d.ellipse((x + math.cos(ang) * 3 - 2.5, y + math.sin(ang) * 3 - 2.5, x + math.cos(ang) * 3 + 2.5, y + math.sin(ang) * 3 + 2.5), fill=P["blossom"])
        d.ellipse((x - 1.5, y - 1.5, x + 1.5, y + 1.5), fill=P["gold_light"])
    return deco_doc("paradise_grass", img, None)


def deco_vines(seed: int = 42) -> dict:
    rng = random.Random(seed)
    img = Image.new("RGBA", (ART, ART), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(4):
        x = rng.randint(8, ART - 9)
        length = rng.randint(34, ART - 2)
        sway = rng.uniform(-0.3, 0.3)
        pts = [(x + sway * t * length * math.sin(t * 2.6), t * length) for t in [i / 10 for i in range(11)]]
        stroke(d, pts, P["moss"], 2)
        for px, py in pts[1::2]:
            r = rng.randint(3, 4)
            d.ellipse((px - r, py - r * 0.7, px + r, py + r * 0.7), fill=rng.choice([P["grass"], P["grass_dark"]]))
        if rng.random() < 0.6:
            px, py = pts[-1]
            d.ellipse((px - 3, py - 3, px + 3, py + 3), fill=P["blossom2"])
    return deco_doc("paradise_vines", img, None)


def deco_lantern(seed: int = 43) -> tuple[dict, None]:
    img = Image.new("RGBA", (ART, ART), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = ART // 2
    d.line([(cx, 0), (cx, 14)], fill=P["gold_dark"], width=2)
    d.rectangle((cx - 9, 14, cx + 9, 18), fill=P["gold"], outline=P["gold_dark"])
    d.rectangle((cx - 11, 18, cx + 11, 46), fill=P["gold_dark"])
    d.rectangle((cx - 8, 21, cx + 8, 43), fill=P["gold_light"])
    d.rectangle((cx - 11, 46, cx + 11, 50), fill=P["gold"], outline=P["gold_dark"])
    d.polygon([(cx - 6, 50), (cx + 6, 50), (cx, 58)], fill=P["gold_dark"])
    for y in (28, 36):
        d.line([(cx - 8, y), (cx + 8, y)], fill=P["gold"], width=1)
    em = Image.new("RGBA", (ART, ART), (0, 0, 0, 0))
    e = ImageDraw.Draw(em)
    e.rectangle((cx - 8, 21, cx + 8, 43), fill=(255, 228, 160, 230))
    return deco_doc("paradise_lantern", img, em, 2.0, 0.6), None


def deco_window() -> dict:
    img = Image.new("RGBA", (ART, ART), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x0, x1, top, bot = 14, 50, 4, 60
    d.rounded_rectangle((x0, top, x1, bot), radius=18, fill=P["gold_dark"])
    d.rounded_rectangle((x0 + 3, top + 3, x1 - 3, bot - 3), radius=15, fill=P["gold"])
    d.rounded_rectangle((x0 + 6, top + 6, x1 - 6, bot - 6), radius=12, fill=P["haze"])
    d.line([(ART // 2, top + 6), (ART // 2, bot - 6)], fill=P["gold"], width=2)
    d.line([(x0 + 6, 30), (x1 - 6, 30)], fill=P["gold"], width=2)
    em = Image.new("RGBA", (ART, ART), (0, 0, 0, 0))
    ImageDraw.Draw(em).rounded_rectangle((x0 + 6, top + 6, x1 - 6, bot - 6), radius=12, fill=(255, 241, 184, 150))
    return deco_doc("paradise_window", img, em, 2.0, 0.5)


# ---- edges (game) ----


def edge_top(seed: int) -> Image.Image:
    """A grass and moss fringe standing on a cell's top edge: 1.5 tiles wide, overhanging neighbours."""
    rng = random.Random(seed)
    w, h = ART * 3 // 2, ART * 7 // 8
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # a low cushion of moss along the edge, fading at the ends
    for _ in range(40):
        x = rng.randint(0, w - 1)
        r = rng.randint(4, 8)
        y = h - rng.randint(0, 8)
        d.ellipse((x - r, y - r * 0.7, x + r, y + r * 0.7), fill=rng.choice([P["moss"], P["grass_dark"], P["grass"]]))
    for _ in range(46):
        x = rng.randint(4, w - 5)
        hgt = rng.randint(h // 4, h - 8)
        lean = rng.uniform(-0.5, 0.5)
        color = rng.choice([P["grass"], P["grass"], P["grass_light"], P["grass_dark"]])
        stroke(d, [(x, h), (x + lean * hgt * 0.4, h - hgt * 0.55), (x + lean * hgt, h - hgt)], color, rng.randint(2, 4))
    for _ in range(rng.randint(2, 5)):
        x, y = rng.randint(8, w - 8), rng.randint(h - 26, h - 10)
        for k in range(5):
            ang = k / 5 * math.tau + rng.random()
            d.ellipse((x + math.cos(ang) * 3 - 2.5, y + math.sin(ang) * 3 - 2.5, x + math.cos(ang) * 3 + 2.5, y + math.sin(ang) * 3 + 2.5), fill=rng.choice([P["blossom"], P["white"]]))
        d.ellipse((x - 1.5, y - 1.5, x + 1.5, y + 1.5), fill=P["gold_light"])
    for _ in range(3):
        x, y = rng.randint(4, w - 4), rng.randint(h // 3, h - 4)
        d.ellipse((x - 1, y - 1, x + 1, y + 1), fill=P["gold_light"])
    # fade the ends so overlapping neighbours blend
    a = np.asarray(img).astype(np.float32)
    xx = np.arange(w, dtype=np.float32)
    fade = np.minimum(1, np.minimum(xx, w - 1 - xx) / 18)
    a[..., 3] *= fade[None, :]
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def edge_bottom(seed: int) -> Image.Image:
    """The rocky underside of a floating island hanging from a cell, with roots: 1.5 tiles wide."""
    rng = random.Random(seed)
    w, h = ART * 3 // 2, ART * 5 // 4
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rock_h = h - 24
    pts = [(0, 0)]
    x = 0
    while x < w:
        x += rng.randint(10, 18)
        depth = rock_h * rng.uniform(0.35, 1.0) * (1 - abs(x / w - 0.5) * 0.9)
        pts.append((min(x, w), depth))
    pts.append((w, 0))
    d.polygon(pts, fill=P["rock"])
    rgb = np.asarray(img).astype(np.float32) / 255
    mask = rgb[..., 3]
    # mottled, darker toward the bottom and toward the edges
    n = fbm(w, h, 10, seed, 3, 0.5)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    shade = smoothstep(0, rock_h, yy) * 0.75 + n * 0.3
    body = lerp(col(P["rock_light"], (h, w)), col(P["rock"], (h, w)), np.clip(n * 1.4, 0, 1))
    body = lerp(body, col(P["rock_dark"], (h, w)), np.clip(shade, 0, 1))
    rim = (yy < 4).astype(np.float32) * mask
    body = lerp(body, col(P["marble_dark"], (h, w)), rim * 0.8)
    out = to_image(body, mask)
    d = ImageDraw.Draw(out)
    # moss clinging to the top corners and roots hanging off the lowest points
    for _ in range(10):
        x = rng.randint(0, w - 1)
        r = rng.randint(3, 6)
        d.ellipse((x - r, -r + 2, x + r, r + 2), fill=rng.choice([P["moss"], P["grass_dark"]]))
    low = sorted(pts[1:-1], key=lambda p: -p[1])[:3]
    for px, py in low:
        if rng.random() < 0.75:
            length = rng.randint(14, 24)
            sway = rng.uniform(-0.4, 0.4)
            rp = [(px + sway * t * length * math.sin(t * 2.2), py + t * length) for t in [i / 8 for i in range(9)]]
            stroke(d, rp, P["root"], 2)
    a = np.asarray(out).astype(np.float32)
    xx = np.arange(w, dtype=np.float32)
    a[..., 3] *= np.minimum(1, np.minimum(xx, w - 1 - xx) / 14)[None, :]
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def edge_side(seed: int, right: bool) -> Image.Image:
    rng = random.Random(seed)
    w, h = ART * 7 // 16, ART
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    edge = 0 if right else w - 1
    for _ in range(18):
        y = rng.randint(0, h - 1)
        r = rng.randint(3, 6)
        x = edge + (rng.randint(0, w // 2) * (1 if right else -1))
        d.ellipse((x - r, y - r * 0.8, x + r, y + r * 0.8), fill=rng.choice([P["moss"], P["grass_dark"], P["grass"]]))
    for _ in range(2):
        y0 = rng.randint(0, h // 2)
        x0 = edge + (rng.randint(2, 8) * (1 if right else -1))
        pts = [(x0 + (3 if right else -3) * math.sin(t * 3), y0 + t * (h - y0)) for t in [i / 8 for i in range(9)]]
        stroke(d, pts, P["moss"], 2)
    return img


# ---- parallax (game) ----


def cloud_layer(w: int, h: int, seed: int, scale: float, cover: float, soft: float) -> Image.Image:
    n = fbm(w, h, scale, seed, 5, 0.55, tileable=True)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    # masses sit in the middle band of the strip
    band = 1 - smoothstep(0.0, 0.5, np.abs(yy - 0.5)) * 0.8
    m = smoothstep(cover, cover + soft, n * band)
    lit = np.roll(m, -int(h * 0.06), axis=0) * m  # the top of every mass is lit
    rgb = lerp(col(P["cloud_shadow"], (h, w)), col(P["cloud"], (h, w)), np.clip(lit * 1.4, 0, 1))
    rgb = lerp(rgb, col(P["cloud_deep"], (h, w)), (1 - lit) * smoothstep(0.55, 1.0, yy) * 0.5)
    return to_image(rgb, m)


def sky_layer(w: int, h: int) -> Image.Image:
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    rgb = lerp(col(P["sky_top"], (h, w)), col(P["sky_bot"], (h, w)), smoothstep(0.05, 0.95, yy))
    haze = fbm(w, h, 220, 7, 3, 0.5) * smoothstep(0.3, 1.0, yy) * 0.35
    rgb = lerp(rgb, col(P["haze"], (h, w)), haze)
    return to_image(rgb, np.ones((h, w)))


def islands_layer(w: int, h: int, seed: int) -> Image.Image:
    """Distant floating islands with towers and waterfalls, washed toward the sky."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    far = (196, 210, 240)
    mid = (170, 186, 224)
    deep = (150, 164, 206)
    grass = (178, 212, 190)
    for _ in range(7):
        iw = rng.randint(w // 14, w // 8)
        ix = rng.randint(0, w - iw)
        iy = rng.randint(h // 5, h * 2 // 3)
        ih = rng.randint(h // 10, h // 6)
        # top slab and jagged underside
        d.rounded_rectangle((ix, iy, ix + iw, iy + 18), radius=9, fill=grass)
        pts = [(ix, iy + 12)]
        x = ix
        while x < ix + iw:
            x += rng.randint(14, 30)
            pts.append((min(x, ix + iw), iy + 12 + ih * rng.uniform(0.3, 1.0) * (1 - abs((x - ix) / iw - 0.5) * 1.2)))
        pts.append((ix + iw, iy + 12))
        d.polygon(pts, fill=mid)
        # a tower or two with spires
        for _ in range(rng.randint(1, 3)):
            tw = rng.randint(10, 22)
            tx = rng.randint(ix + 6, max(ix + 6, ix + iw - tw - 6))
            th = rng.randint(40, 110)
            d.rectangle((tx, iy - th, tx + tw, iy + 2), fill=far)
            d.polygon([(tx - 2, iy - th), (tx + tw + 2, iy - th), (tx + tw / 2, iy - th - tw * 1.6)], fill=deep)
            d.rectangle((tx + tw / 2 - 1, iy - th + 12, tx + tw / 2 + 1, iy - th + 22), fill=(236, 230, 200))
        # waterfalls off the edge
        for _ in range(rng.randint(1, 2)):
            fx = rng.randint(ix + 4, ix + iw - 4)
            fl = rng.randint(h // 4, h // 2)
            for k in range(fl):
                a = int(200 * (1 - k / fl))
                d.line([(fx - 1, iy + 10 + k), (fx + 1, iy + 10 + k)], fill=(*P["water"], a))
    # wash everything toward the sky for depth
    a = np.asarray(img).astype(np.float32) / 255
    rgb = lerp(a[..., :3], col(P["sky_bot"], (h, w)), np.full((h, w), 0.5, dtype=np.float32))
    return to_image(rgb, a[..., 3] * 0.75)


def rays_layer(w: int, h: int, seed: int) -> Image.Image:
    rng = random.Random(seed)
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    for _ in range(9):
        x = rng.randint(-w // 4, w)
        width = rng.randint(30, 90)
        d.polygon([(x, 0), (x + width, 0), (x + width + h * 0.45, h), (x + h * 0.45, h)], fill=rng.randint(70, 130))
    img = img.filter(ImageFilter.GaussianBlur(22))
    a = np.asarray(img).astype(np.float32) / 255
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    a *= (1 - smoothstep(0.2, 1.0, yy)) * 0.55
    return to_image(col(P["gold_light"], (h, w)), a)


def fog_layer(w: int, h: int, seed: int, color: tuple, strength: float) -> Image.Image:
    n = fbm(w, h, 160, seed, 4, 0.5, tileable=True)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    a = (0.35 + 0.65 * n) * smoothstep(0.0, 0.7, yy) * strength
    return to_image(col(color, (h, w)), a)


def wisps_layer(w: int, h: int, seed: int, color: tuple | None = None, strength: float = 0.4) -> Image.Image:
    n = fbm(w, h, 120, seed, 4, 0.6, tileable=True)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    a = smoothstep(0.58, 0.8, n) * (1 - smoothstep(0, 0.5, np.abs(yy - 0.5)) * 0.9) * strength
    return to_image(col(color or P["cloud"], (h, w)), a)


# ---- particles (game) ----


def petal(size: int = 32) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((4, 10, size - 4, size - 10), fill=P["blossom"])
    d.ellipse((10, 12, size - 6, size - 14), fill=P["white"] + (110,))
    return img.rotate(30, resample=Image.BICUBIC)


def paradise(texture_dir: str) -> None:
    key = "paradise"
    # edges
    tops = []
    for i in range(4):
        save_png(edge_top(100 + i), key, f"tuft_{i}.png")
        tops.append(f"tuft_{i}.png")
    bottoms = []
    for i in range(3):
        save_png(edge_bottom(200 + i), key, f"underside_{i}.png")
        bottoms.append(f"underside_{i}.png")
    lefts, rights = [], []
    for i in range(2):
        save_png(edge_side(300 + i, False), key, f"ivy_l{i}.png")
        save_png(edge_side(310 + i, True), key, f"ivy_r{i}.png")
        lefts.append(f"ivy_l{i}.png")
        rights.append(f"ivy_r{i}.png")
    for old in ("vine_0.png", "vine_1.png", "moss_l.png", "moss_r.png", "hills_far.png", "hills_near.png"):
        path = os.path.join(ROOT, "assets", "regions", key, old)
        if os.path.exists(path):
            os.remove(path)
    write_json(key, "edges.json", {
        "art_tile": ART, "top": tops, "bottom": bottoms, "left": lefts, "right": rights,
        "chance": 1.0, "top_chance": 0.9, "bottom_chance": 0.7, "left_chance": 0.55, "right_chance": 0.55,
    })
    # parallax, one screen wide (30 tiles) at the art scale, repeated sideways
    w = 30 * ART
    save_png(sky_layer(w // 2, 17 * ART // 2), key, "sky.png")
    save_png(cloud_layer(w, 9 * ART, 1, 210, 0.44, 0.14), key, "clouds_far.png")
    save_png(islands_layer(w, 11 * ART, 2), key, "islands.png")
    save_png(rays_layer(w, 12 * ART, 3), key, "rays.png")
    save_png(cloud_layer(w, 7 * ART, 4, 150, 0.5, 0.12), key, "clouds_mid.png")
    save_png(fog_layer(w, 6 * ART, 5, P["haze"], 0.6), key, "fog.png")
    save_png(wisps_layer(w, 5 * ART, 6), key, "wisps.png")
    write_json(key, "parallax.json", {"art_tile": ART, "layers": [
        {"file": "sky.png", "cover": True},
        {"file": "clouds_far.png", "scroll": [0.12, 0.4], "y": -120},
        {"file": "islands.png", "scroll": [0.25, 0.55], "y": 120},
        {"file": "rays.png", "scroll": [0.15, 0.25], "y": -300},
        {"file": "clouds_mid.png", "scroll": [0.5, 0.78], "y": 330},
        {"file": "fog.png", "scroll": [0.75, 0.95], "y": 430},
        {"file": "wisps.png", "scroll": [1.18, 1.04], "y": 460, "front": True},
    ]})
    # particles
    save_png(petal(), key, "petal.png")
    save_png(soft_disc(16, P["gold_light"], 2.5), key, "mote.png")
    write_json(key, "fx.json", {"art_tile": ART, "particles": [
        {"texture": "petal.png", "amount": 36, "color": "#ffffff", "lifetime": 14, "direction": [-0.35, 1.0],
         "spread": 20, "speed": [8, 18], "gravity": [0, 2], "scale": [0.5, 0.9], "spin": 0.4, "sway": 4},
        {"texture": "mote.png", "amount": 28, "color": "#fff1b8", "lifetime": 12, "direction": [0.1, -1.0],
         "spread": 40, "speed": [2, 6], "scale": [0.4, 0.9], "sway": 2},
    ]})
    # tile textures for the editor
    os.makedirs(texture_dir, exist_ok=True)
    docs = [tex_terrain(), tex_wall(), tex_platform(), tex_hazard(), deco_grass(), deco_vines(), deco_lantern()[0], deco_window()]
    for doc in docs:
        with open(os.path.join(texture_dir, f"{doc['name']}.lpx.json"), "w") as f:
            json.dump(doc, f)
    print(f"{key}: edges, parallax and particles in assets/regions/{key}/; {len(docs)} textures in {texture_dir}")


# ---------------------------------------------------------------- the regions below Paradise
#
# Every region under the orchard is drawn from one table of colours and a
# few style words (STYLES), so a new circle is a dozen lines, not a module.
# The pieces are the same three kinds Paradise has: a parallax set (a cover
# sky, a far silhouette, a mid layer, fog and a front haze), edge pieces on
# the terrain's exposed sides, and the particles in the air.


def fade_ends(img: Image.Image, margin: int) -> Image.Image:
    """Fade a piece's alpha toward its left and right ends so overlapping neighbours blend."""
    a = np.asarray(img).astype(np.float32)
    w = a.shape[1]
    xx = np.arange(w, dtype=np.float32)
    a[..., 3] *= np.minimum(1, np.minimum(xx, w - 1 - xx) / margin)[None, :]
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def tint(c: tuple, k: float) -> tuple:
    """A colour scaled toward black (k < 1) or white (k > 1)."""
    if k <= 1:
        return tuple(int(v * k) for v in c[:3])
    return tuple(int(v + (255 - v) * (k - 1)) for v in c[:3])


# ---- edges ----


def fringe_top(seed: int, style: str, colors: list, accent: tuple | None) -> Image.Image:
    """Something standing on a ledge: dead grass, a moss or slime cushion, cinders, frost."""
    rng = random.Random(seed)
    w, h = ART * 3 // 2, ART * 3 // 4
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if style in ("grass", "reeds"):
        for _ in range(26):
            x = rng.randint(0, w - 1)
            r = rng.randint(3, 6)
            d.ellipse((x - r, h - 6 - r * 0.6, x + r, h - 6 + r * 0.6), fill=rng.choice(colors))
        count = 40 if style == "grass" else 22
        for _ in range(count):
            x = rng.randint(4, w - 5)
            hgt = rng.randint(h // 4, h - 6)
            lean = rng.uniform(-0.7, 0.7)
            stroke(d, [(x, h), (x + lean * hgt * 0.3, h - hgt * 0.5), (x + lean * hgt, h - hgt)], rng.choice(colors), rng.randint(1, 3))
    elif style in ("moss", "slime", "frost", "ash"):
        for _ in range(48):
            x = rng.randint(0, w - 1)
            r = rng.randint(3, 9)
            y = h - rng.randint(0, 6)
            d.ellipse((x - r, y - r * 0.55, x + r, y + r * 0.55), fill=rng.choice(colors))
        if style == "slime":
            for _ in range(5):
                x = rng.randint(6, w - 6)
                r = rng.randint(2, 4)
                d.ellipse((x - r, h - 14 - r, x + r, h - 14 + r), fill=tint(colors[0], 1.3))
        if style == "frost":
            for _ in range(10):
                x = rng.randint(4, w - 5)
                hgt = rng.randint(6, h - 10)
                d.polygon([(x - 2, h), (x + 2, h), (x + rng.uniform(-2, 2), h - hgt)], fill=rng.choice(colors))
    elif style == "cinders":
        for _ in range(30):
            x = rng.randint(0, w - 1)
            r = rng.randint(2, 6)
            d.ellipse((x - r, h - 4 - r * 0.6, x + r, h - 4 + r * 0.6), fill=rng.choice(colors))
        for _ in range(8):
            x = rng.randint(4, w - 5)
            r = rng.randint(1, 3)
            d.ellipse((x - r, h - 7 - r, x + r, h - 7 + r), fill=accent or colors[-1])
    elif style == "bones":
        for _ in range(22):
            x = rng.randint(0, w - 1)
            r = rng.randint(3, 7)
            d.ellipse((x - r, h - 5 - r * 0.5, x + r, h - 5 + r * 0.5), fill=rng.choice(colors))
        for _ in range(3):
            x = rng.randint(8, w - 12)
            length = rng.randint(8, 16)
            ang = rng.uniform(-0.5, 0.5)
            x2, y2 = x + math.cos(ang) * length, h - 8 - math.sin(ang) * length
            stroke(d, [(x, h - 6), (x2, y2)], accent or (210, 200, 180), 2)
            d.ellipse((x2 - 2, y2 - 2, x2 + 2, y2 + 2), fill=accent or (210, 200, 180))
    if accent and style not in ("cinders", "bones"):
        for _ in range(4):
            x, y = rng.randint(4, w - 4), rng.randint(h // 2, h - 4)
            d.ellipse((x - 1, y - 1, x + 1, y + 1), fill=accent)
    return fade_ends(img, 18)


def hang_bottom(seed: int, style: str, colors: list, accent: tuple | None) -> Image.Image:
    """Something hanging under a ledge: roots, stalactites, drips, icicles, chains, vines."""
    rng = random.Random(seed)
    w, h = ART * 3 // 2, ART
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if style in ("stalactites", "icicles"):
        x = rng.randint(4, 14)
        while x < w - 6:
            bw = rng.randint(5, 12)
            depth = rng.randint(h // 4, h - 4) * (1 - abs(x / w - 0.5) * 0.8)
            c = rng.choice(colors)
            d.polygon([(x - bw, 0), (x + bw, 0), (x + rng.uniform(-2, 2), depth)], fill=c)
            d.polygon([(x - bw, 0), (x - bw * 0.3, 0), (x - 1, depth * 0.7)], fill=tint(c, 1.25))
            if style == "icicles" and rng.random() < 0.6:
                d.ellipse((x - 2, depth - 1, x + 2, depth + 3), fill=accent or tint(c, 1.4))
            x += bw + rng.randint(6, 18)
    elif style == "drips":
        for _ in range(rng.randint(4, 7)):
            x = rng.randint(4, w - 5)
            length = rng.randint(h // 5, h - 8)
            c = rng.choice(colors)
            d.ellipse((x - 5, -4, x + 5, 6), fill=c)
            stroke(d, [(x, 0), (x + rng.uniform(-1, 1), length)], c, rng.randint(2, 3))
            d.ellipse((x - 3, length - 2, x + 3, length + 5), fill=accent or tint(c, 1.3))
    elif style == "chains":
        for _ in range(rng.randint(1, 3)):
            x = rng.randint(8, w - 8)
            length = rng.randint(h // 2, h - 4)
            c = rng.choice(colors)
            y = 0
            while y < length:
                d.ellipse((x - 3, y, x + 3, y + 8), outline=c, width=2)
                y += 7
            if rng.random() < 0.5:
                d.polygon([(x - 5, length), (x + 5, length), (x, length + 8)], fill=accent or c)
    elif style == "cloth":
        for _ in range(rng.randint(2, 4)):
            x = rng.randint(6, w - 14)
            tw = rng.randint(6, 12)
            length = rng.randint(h // 3, h - 6)
            c = rng.choice(colors)
            pts = [(x, 0), (x + tw, 0)]
            for k in range(3):
                pts.append((x + tw - k * tw / 3 + rng.uniform(-2, 2), length * (1 - k * 0.25)))
            pts.append((x, length * 0.5))
            d.polygon(pts, fill=c)
    else:  # roots and vines
        for _ in range(rng.randint(3, 6)):
            x = rng.randint(4, w - 5)
            length = rng.randint(h // 3, h - 4)
            sway = rng.uniform(-0.5, 0.5)
            pts = [(x + sway * t * length * math.sin(t * 2.4), t * length) for t in [i / 8 for i in range(9)]]
            c = rng.choice(colors)
            stroke(d, pts, c, rng.randint(2, 3))
            if style == "vines" and accent:
                for px, py in pts[2::2]:
                    d.ellipse((px - 2, py - 1, px + 2, py + 3), fill=accent)
    return fade_ends(img, 14)


def side_piece(seed: int, right: bool, style: str, colors: list) -> Image.Image:
    """Growth, frost or soot down a wall."""
    rng = random.Random(seed)
    w, h = ART * 7 // 16, ART
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    edge = 0 if right else w - 1
    sgn = 1 if right else -1
    for _ in range(16):
        y = rng.randint(0, h - 1)
        r = rng.randint(2, 6)
        x = edge + rng.randint(0, w // 2) * sgn
        d.ellipse((x - r, y - r * 0.8, x + r, y + r * 0.8), fill=rng.choice(colors))
    if style == "vines":
        for _ in range(2):
            y0 = rng.randint(0, h // 2)
            x0 = edge + rng.randint(2, 8) * sgn
            stroke(d, [(x0 + 3 * sgn * math.sin(t * 3), y0 + t * (h - y0)) for t in [i / 8 for i in range(9)]], colors[0], 2)
    elif style == "frost":
        for _ in range(4):
            y = rng.randint(4, h - 4)
            d.polygon([(edge, y - 3), (edge, y + 3), (edge + rng.randint(6, 14) * sgn, y)], fill=rng.choice(colors))
    return img


# ---- parallax ----


def dark_sky(w: int, h: int, top: tuple, bottom: tuple, seed: int, hole: tuple | None = None) -> Image.Image:
    """A cover layer: a gradient with a little cloudy noise, and for Earth the hole in the sky."""
    yy, xx = np.mgrid[0:h, 0:w]
    yy = yy.astype(np.float32) / h
    xx = xx.astype(np.float32) / w
    rgb = lerp(col(top, (h, w)), col(bottom, (h, w)), smoothstep(0.0, 1.0, yy))
    n = fbm(w, h, 120, seed, 3, 0.5)
    rgb = lerp(rgb, col(tint(bottom, 1.15), (h, w)), (n - 0.5) * 0.3 + 0.15)
    if hole:
        r = np.hypot((xx - 0.5) * (w / h), yy - 0.05)
        glow = np.clip(1 - r / 0.55, 0, 1) ** 2.2
        rgb = lerp(rgb, col(hole, (h, w)), glow * 0.85)
    return to_image(rgb, np.ones((h, w)))


def silhouette_layer(w: int, h: int, seed: int, kind: str, color: tuple, lit: tuple | None, alpha: float) -> Image.Image:
    """A far shape: a ruined skyline, a cavern's teeth, a hall of pillars, ice spires or a wall of names."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    dark = tint(color, 0.75)
    if kind == "skyline":
        x = 0
        while x < w:
            bw = rng.randint(w // 20, w // 9)
            bh = rng.randint(h // 4, h * 3 // 4)
            top = h - bh
            # a broken top: a slanted rubble line and a sagging roof
            pts = [(x, h), (x, top + rng.randint(0, 20)), (x + bw * 0.3, top), (x + bw * 0.6, top + rng.randint(0, 30)), (x + bw, top + rng.randint(0, 24)), (x + bw, h)]
            c = rng.choice([color, dark])
            d.polygon(pts, fill=c)
            for wy in range(top + 20, h - 10, 18):
                for wx in range(x + 8, x + bw - 10, 16):
                    if rng.random() < 0.6:
                        win = lit if (lit and rng.random() < 0.08) else tint(c, 0.6)
                        d.rectangle((wx, wy, wx + 6, wy + 10), fill=win)
            if rng.random() < 0.3:  # a chimney or a spire
                sx = x + rng.randint(4, max(5, bw - 10))
                d.rectangle((sx, top - rng.randint(20, 60), sx + 6, top), fill=c)
            x += bw + rng.randint(-6, 24)
    elif kind == "cavern":
        for side in (0, 1):  # teeth from the ceiling and the floor
            x = -10
            while x < w + 10:
                bw = rng.randint(10, 40)
                depth = rng.randint(h // 6, h // 2)
                c = rng.choice([color, dark])
                if side == 0:
                    d.polygon([(x - bw, 0), (x + bw, 0), (x + rng.uniform(-6, 6), depth)], fill=c)
                else:
                    d.polygon([(x - bw, h), (x + bw, h), (x + rng.uniform(-6, 6), h - depth)], fill=c)
                x += bw + rng.randint(4, 30)
    elif kind == "pillars":
        x = rng.randint(10, 60)
        while x < w:
            pw = rng.randint(14, 30)
            c = rng.choice([color, dark])
            d.rectangle((x, 0, x + pw, h), fill=c)
            d.rectangle((x - 6, 0, x + pw + 6, 14), fill=c)
            d.rectangle((x - 6, h - 14, x + pw + 6, h), fill=c)
            d.rectangle((x + pw * 0.3, 0, x + pw * 0.45, h), fill=tint(c, 1.2))
            nx = x + pw + rng.randint(60, 160)
            # an arch between this pillar and the next
            d.chord((x + pw - 10, 10, nx + 10, 10 + (nx - x) * 0.9), 180, 360, fill=c)
            d.chord((x + pw + 6, 24, nx - 6, 24 + (nx - x) * 0.82), 180, 360, fill=(0, 0, 0, 0))
            x = nx
    elif kind == "spires":
        x = 0
        while x < w:
            bw = rng.randint(14, 50)
            top = rng.randint(h // 8, h * 2 // 3)
            c = rng.choice([color, dark])
            d.polygon([(x - bw, h), (x + bw, h), (x + rng.uniform(-4, 4), top)], fill=c)
            d.polygon([(x - bw, h), (x - bw * 0.2, h), (x - 1, top + (h - top) * 0.15)], fill=tint(c, 1.25))
            x += bw + rng.randint(0, 40)
    elif kind == "wall":
        d.rectangle((0, 0, w, h), fill=color)
        for y in range(0, h, 20):
            d.line([(0, y), (w, y)], fill=dark, width=2)
            off = 20 if (y // 20) % 2 else 0
            for x in range(off, w, 40):
                d.line([(x, y), (x, y + 20)], fill=dark, width=2)
        # names scratched into the stone: short marks in rows
        for _ in range(w // 6):
            x, y = rng.randint(0, w - 24), rng.randint(0, h - 8)
            for k in range(rng.randint(3, 7)):
                d.line([(x + k * 4, y + rng.randint(2, 6)), (x + k * 4, y + 6)], fill=tint(color, 1.3), width=1)
    elif kind == "cages":
        x = rng.randint(0, 80)
        while x < w:
            cw, ch = rng.randint(24, 40), rng.randint(40, 70)
            top = rng.randint(0, h - ch - 10)
            c = rng.choice([color, dark])
            d.line([(x + cw / 2, 0), (x + cw / 2, top)], fill=c, width=2)
            d.rectangle((x, top, x + cw, top + ch), outline=c, width=3)
            for k in range(1, 4):
                d.line([(x + cw * k / 4, top), (x + cw * k / 4, top + ch)], fill=c, width=2)
            x += cw + rng.randint(50, 160)
    a = np.asarray(img).astype(np.float32) / 255
    return to_image(a[..., :3], a[..., 3] * alpha)


def glow_band(w: int, h: int, color: tuple, seed: int) -> Image.Image:
    """Light coming up from below: a lava field or a furnace under the floor."""
    n = fbm(w, h, 90, seed, 3, 0.5, tileable=True)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    a = smoothstep(0.1, 1.0, yy) * (0.5 + 0.5 * n) * 0.7
    return to_image(col(color, (h, w)), a)


# ---- particles ----


def flake(size: int, color: tuple, seed: int) -> Image.Image:
    """An irregular scrap: ash, a petal of soot, a snowflake's blur."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = [(size / 2 + math.cos(a) * rng.uniform(size * 0.25, size * 0.45), size / 2 + math.sin(a) * rng.uniform(size * 0.25, size * 0.45)) for a in [k / 6 * math.tau for k in range(6)]]
    d.polygon(pts, fill=color)
    return img


def streak(size: int, color: tuple) -> Image.Image:
    """A falling drop drawn as a short vertical streak."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([(size / 2, 2), (size / 2, size - 6)], fill=color + (160,), width=2)
    d.ellipse((size / 2 - 2, size - 8, size / 2 + 2, size - 2), fill=color)
    return img


PARTICLES = {
    "ash": {"amount": 40, "lifetime": 14, "direction": [-0.3, 1.0], "spread": 25, "speed": [6, 14], "gravity": [0, 2], "scale": [0.3, 0.6], "spin": 0.3, "sway": 3},
    "snow": {"amount": 60, "lifetime": 12, "direction": [-0.2, 1.0], "spread": 20, "speed": [10, 22], "gravity": [0, 4], "scale": [0.3, 0.7], "sway": 4},
    "embers": {"amount": 30, "lifetime": 8, "direction": [0.1, -1.0], "spread": 35, "speed": [10, 26], "gravity": [0, -4], "scale": [0.3, 0.7], "sway": 5},
    "motes": {"amount": 24, "lifetime": 12, "direction": [0.0, -1.0], "spread": 50, "speed": [2, 5], "scale": [0.3, 0.7], "sway": 2},
    "dust": {"amount": 30, "lifetime": 16, "direction": [1.0, 0.0], "spread": 30, "speed": [2, 6], "scale": [0.2, 0.5], "sway": 2},
    "flies": {"amount": 22, "lifetime": 6, "direction": [1.0, 0.0], "spread": 180, "speed": [14, 30], "scale": [0.2, 0.35], "sway": 12},
    "drops": {"amount": 14, "lifetime": 3, "direction": [0.0, 1.0], "spread": 0, "speed": [60, 90], "gravity": [0, 60], "scale": [0.4, 0.7]},
    "smoke": {"amount": 10, "lifetime": 16, "direction": [0.2, -1.0], "spread": 20, "speed": [3, 7], "scale": [1.5, 3.0], "sway": 3},
    "sparks": {"amount": 18, "lifetime": 5, "direction": [0.0, -1.0], "spread": 60, "speed": [16, 40], "gravity": [0, 10], "scale": [0.2, 0.4], "sway": 6},
}


def particle_texture(kind: str, color: tuple, seed: int) -> Image.Image:
    if kind in ("ash", "snow"):
        return flake(24, color, seed)
    if kind == "drops":
        return streak(24, color)
    if kind == "smoke":
        return soft_disc(48, color, 1.6)
    if kind == "flies":
        return soft_disc(8, color, 1.0)
    return soft_disc(16, color, 2.5)


# ---- the table ----
#
# sky: cover gradient (top, bottom) [, hole colour]; far/mid: (kind, colour, lit colour, alpha);
# fog: (colour, strength); glow: colour or None; front: (colour, strength);
# edges: (top style, top colours, bottom style, bottom colours, side style, side colours, accent)
# with per-side chances; air: [(kind, colour, overrides)].

STYLES = {
    "ruined_earth": {
        "sky": ((26, 24, 36), (88, 78, 92), (214, 206, 190)),
        "far": ("skyline", (52, 46, 60), (210, 170, 90), 0.9),
        "mid": ("skyline", (38, 34, 46), (240, 180, 90), 1.0),
        "fog": ((96, 88, 98), 0.35),
        "front": ((120, 110, 112), 0.3),
        "edges": ("grass", [(104, 92, 58), (128, 112, 70), (76, 70, 46)], "roots", [(70, 54, 38), (54, 42, 30)], "moss", [(72, 76, 48), (58, 60, 40)], None),
        "chances": (0.8, 0.5, 0.4),
        "air": [("ash", (150, 142, 136), {}), ("smoke", (70, 64, 70), {"amount": 6})],
    },
    "purgatory": {
        "sky": ((40, 40, 46), (92, 92, 100)),
        "far": ("wall", (58, 58, 64), None, 0.8),
        "mid": ("pillars", (46, 46, 52), None, 0.9),
        "fog": ((150, 150, 158), 0.3),
        "front": ((170, 170, 180), 0.35),
        "edges": ("moss", [(84, 92, 84), (70, 78, 72), (98, 104, 92)], "drips", [(120, 126, 132)], "moss", [(80, 86, 80), (66, 72, 68)], (200, 204, 210)),
        "chances": (0.6, 0.3, 0.4),
        "air": [("motes", (210, 212, 220), {"amount": 18}), ("dust", (160, 160, 170), {})],
    },
    "limbo": {
        "sky": ((22, 26, 40), (56, 66, 100)),
        "far": ("cavern", (40, 48, 76), None, 0.85),
        "mid": ("pillars", (34, 40, 64), None, 0.9),
        "fog": ((110, 124, 170), 0.45),
        "front": ((140, 154, 200), 0.3),
        "edges": ("moss", [(60, 74, 96), (74, 88, 112), (48, 60, 84)], "stalactites", [(70, 80, 108), (56, 64, 92)], "moss", [(56, 68, 90), (66, 80, 104)], (170, 190, 230)),
        "chances": (0.6, 0.5, 0.4),
        "air": [("dust", (170, 186, 220), {"amount": 36}), ("motes", (200, 214, 250), {"amount": 12})],
    },
    "lust": {
        "sky": ((40, 14, 34), (112, 44, 92)),
        "far": ("pillars", (78, 30, 66), (255, 170, 210), 0.85),
        "mid": ("cages", (60, 22, 52), None, 0.9),
        "fog": ((190, 90, 160), 0.4),
        "front": ((230, 130, 200), 0.3),
        "edges": ("reeds", [(150, 60, 120), (120, 44, 96), (186, 90, 150)], "cloth", [(170, 60, 130), (120, 40, 96), (210, 110, 170)], "vines", [(120, 48, 100), (150, 66, 124)], (255, 200, 230)),
        "chances": (0.5, 0.45, 0.35),
        "air": [("motes", (255, 190, 230), {"amount": 26, "spread": 80}), ("ash", (220, 150, 200), {"amount": 14, "scale": [0.25, 0.45]})],
    },
    "gluttony": {
        "sky": ((14, 30, 14), (40, 84, 36)),
        "far": ("cavern", (34, 68, 30), None, 0.85),
        "mid": ("cavern", (26, 54, 24), None, 0.9),
        "fog": ((100, 170, 80), 0.4),
        "front": ((140, 210, 100), 0.3),
        "edges": ("slime", [(90, 170, 60), (70, 140, 50), (120, 200, 80)], "drips", [(110, 190, 70), (90, 160, 60)], "moss", [(70, 130, 50), (90, 160, 60)], (200, 255, 150)),
        "chances": (0.7, 0.55, 0.45),
        "air": [("flies", (40, 60, 30), {}), ("drops", (120, 200, 80), {"amount": 8})],
    },
    "greed": {
        "sky": ((30, 24, 12), (84, 66, 32)),
        "far": ("wall", (70, 56, 28), None, 0.75),
        "mid": ("pillars", (56, 44, 22), (255, 220, 120), 0.9),
        "fog": ((170, 140, 70), 0.4),
        "front": ((220, 190, 110), 0.25),
        "edges": ("bones", [(96, 80, 44), (120, 100, 56), (78, 64, 36)], "chains", [(110, 96, 60), (84, 72, 44)], "moss", [(90, 78, 42), (110, 94, 52)], (230, 210, 150)),
        "chances": (0.5, 0.4, 0.3),
        "air": [("motes", (255, 220, 140), {"amount": 30, "scale": [0.2, 0.5]}), ("dust", (200, 170, 100), {"amount": 20})],
    },
    "wrath": {
        "sky": ((30, 8, 8), (96, 26, 20)),
        "far": ("cavern", (72, 20, 16), None, 0.9),
        "mid": ("spires", (56, 16, 14), None, 0.9),
        "glow": (255, 110, 30),
        "fog": ((160, 60, 30), 0.4),
        "front": ((80, 30, 20), 0.45),
        "edges": ("cinders", [(80, 30, 20), (60, 22, 16), (110, 40, 24)], "stalactites", [(90, 34, 26), (70, 26, 20)], "moss", [(90, 36, 24), (70, 28, 20)], (255, 150, 60)),
        "chances": (0.6, 0.5, 0.3),
        "air": [("embers", (255, 150, 60), {}), ("smoke", (60, 20, 16), {"amount": 8}), ("sparks", (255, 220, 120), {"amount": 10})],
    },
    "heresy": {
        "sky": ((34, 14, 6), (110, 50, 18)),
        "far": ("skyline", (80, 36, 14), (255, 190, 80), 0.85),
        "mid": ("cages", (60, 28, 12), None, 0.9),
        "glow": (255, 150, 50),
        "fog": ((180, 90, 40), 0.4),
        "front": ((110, 50, 20), 0.4),
        "edges": ("ash", [(110, 76, 50), (90, 60, 40), (140, 100, 64)], "chains", [(110, 80, 50), (90, 64, 40)], "moss", [(96, 66, 42), (120, 84, 54)], (255, 200, 110)),
        "chances": (0.6, 0.4, 0.3),
        "air": [("ash", (190, 150, 110), {"amount": 44}), ("embers", (255, 180, 80), {"amount": 16})],
    },
    "violence": {
        "sky": ((26, 6, 10), (80, 18, 30)),
        "far": ("cavern", (64, 16, 26), None, 0.9),
        "mid": ("cages", (50, 12, 22), None, 0.9),
        "fog": ((140, 40, 60), 0.45),
        "front": ((90, 20, 36), 0.45),
        "edges": ("bones", [(80, 28, 36), (100, 36, 44), (60, 20, 28)], "drips", [(150, 30, 40), (120, 24, 34)], "moss", [(90, 30, 40), (110, 40, 50)], (220, 200, 190)),
        "chances": (0.6, 0.5, 0.35),
        "air": [("drops", (170, 30, 40), {"amount": 10}), ("ash", (120, 60, 70), {"amount": 24}), ("flies", (30, 10, 14), {"amount": 10})],
    },
    "fraud": {
        "sky": ((22, 14, 40), (70, 48, 120)),
        "far": ("pillars", (56, 38, 100), (230, 200, 255), 0.85),
        "mid": ("cloth", (44, 30, 80), None, 0.9),
        "fog": ((150, 110, 210), 0.45),
        "front": ((190, 150, 240), 0.3),
        "edges": ("moss", [(80, 60, 130), (100, 76, 160), (64, 48, 110)], "cloth", [(110, 70, 170), (80, 50, 140), (150, 100, 200)], "vines", [(84, 60, 136), (104, 76, 160)], (240, 210, 255)),
        "chances": (0.6, 0.5, 0.35),
        "air": [("motes", (220, 190, 255), {"amount": 28}), ("dust", (160, 130, 220), {"amount": 24})],
    },
    "treachery": {
        "sky": ((12, 24, 44), (50, 90, 136)),
        "far": ("spires", (40, 76, 120), None, 0.85),
        "mid": ("spires", (30, 60, 100), None, 0.9),
        "fog": ((150, 190, 230), 0.5),
        "front": ((200, 225, 250), 0.4),
        "edges": ("frost", [(170, 210, 240), (200, 230, 250), (140, 180, 220)], "icicles", [(150, 200, 240), (120, 170, 220)], "frost", [(160, 200, 236), (190, 220, 246)], (240, 250, 255)),
        "chances": (0.8, 0.6, 0.5),
        "air": [("snow", (235, 245, 255), {}), ("dust", (200, 225, 250), {"amount": 16})],
    },
}


def region_height(key: str) -> float:
    """A region's height in level pixels, from the world export (a circle's when unknown)."""
    try:
        with open(os.path.join(ROOT, "levels", "world", "world.json")) as f:
            for region in json.load(f).get("regions", []):
                if region_key(str(region.get("name", ""))) == key:
                    return float(region.get("y1", 0)) - float(region.get("y0", 0))
    except (OSError, ValueError):
        pass
    return 544.0


def region_key(name: str) -> str:
    """The folder name of a region, as EdgeDecor.region_key: lowercased, without the circle's number."""
    key = name.lower().strip()
    dot = key.find(". ")
    if dot > 0 and key[:dot].isdigit():
        key = key[dot + 2:]
    return key.replace(" ", "_")


def layer_anchor(height: float, layer: float, scroll_y: float, offset: float) -> float:
    """The `y` of a parallax layer (its top edge relative to the region's top, in level pixels)
    that puts its centre `offset` below the middle of the view when the camera is halfway
    down the region. Backdrop draws it at anchor + (camera - anchor) * (1 - scroll), so on
    screen it moves by `scroll` of the camera's travel and drifts scroll * height / 2 either
    way over the region: the vertical scroll of a layer that should stay in view is small."""
    return height / 2 + (offset - layer / 2) / scroll_y


def styled_region(key: str, _texture_dir: str) -> None:
    """Draws a region's set from its STYLES entry."""
    st = STYLES[key]
    top_style, top_colors, bottom_style, bottom_colors, side_style, side_colors, accent = st["edges"]
    top_chance, bottom_chance, side_chance = st["chances"]
    tops, bottoms, lefts, rights = [], [], [], []
    for i in range(4):
        save_png(fringe_top(100 + i, top_style, top_colors, accent), key, f"top_{i}.png")
        tops.append(f"top_{i}.png")
    for i in range(3):
        save_png(hang_bottom(200 + i, bottom_style, bottom_colors, accent), key, f"hang_{i}.png")
        bottoms.append(f"hang_{i}.png")
    for i in range(2):
        save_png(side_piece(300 + i, False, side_style, side_colors), key, f"side_l{i}.png")
        save_png(side_piece(310 + i, True, side_style, side_colors), key, f"side_r{i}.png")
        lefts.append(f"side_l{i}.png")
        rights.append(f"side_r{i}.png")
    write_json(key, "edges.json", {
        "art_tile": ART, "top": tops, "bottom": bottoms, "left": lefts, "right": rights,
        "chance": 1.0, "top_chance": top_chance, "bottom_chance": bottom_chance,
        "left_chance": side_chance, "right_chance": side_chance,
    })
    # parallax, one screen wide, repeated sideways
    w = 30 * ART
    seed = sum(ord(c) for c in key)
    sky = st["sky"]
    save_png(dark_sky(w // 2, 17 * ART // 2, sky[0], sky[1], seed, sky[2] if len(sky) > 2 else None), key, "sky.png")
    layers = [{"file": "sky.png", "cover": True}]
    height = region_height(key)

    def place(name: str, tiles: int, scroll: list, offset: float, front: bool = False) -> None:
        # The region is a few screens tall and the view one, so a layer that
        # follows the camera by less than all of its travel drifts across the
        # region; it is anchored so that halfway down it sits `offset` level
        # pixels below the middle of the view.
        entry = {"file": f"{name}.png", "scroll": scroll, "y": int(round(layer_anchor(height, tiles * 16, scroll[1], offset)))}
        if front:
            entry["front"] = True
        layers.append(entry)

    mid_kind = st["mid"][0]
    for name, spec, tiles, scroll, offset in (("far", st["far"], 14, [0.2, 0.08], 0), ("mid", st["mid"], 11, [0.45, 0.16], 10)):
        kind, color, lit, alpha = spec
        if kind == "cloth":
            # a mid layer of hanging veils is drawn as hangings on a dark bar
            img = Image.new("RGBA", (w, tiles * ART), (0, 0, 0, 0))
            rng = random.Random(seed + 7)
            d = ImageDraw.Draw(img)
            x = 0
            while x < w:
                tw, length = rng.randint(30, 70), rng.randint(ART * 2, tiles * ART - 20)
                d.polygon([(x, 0), (x + tw, 0), (x + tw * 0.7, length), (x + tw * 0.3, length * 0.9)], fill=rng.choice([color, tint(color, 0.7)]))
                x += tw + rng.randint(40, 140)
            a = np.asarray(img).astype(np.float32) / 255
            save_png(to_image(a[..., :3], a[..., 3] * alpha), key, f"{name}.png")
        else:
            save_png(silhouette_layer(w, tiles * ART, seed + len(name), kind, color, lit, alpha), key, f"{name}.png")
        place(name, tiles, scroll, offset)
    if st.get("glow"):
        save_png(glow_band(w, 6 * ART, st["glow"], seed + 3), key, "glow.png")
        place("glow", 6, [0.6, 0.12], 70)
    fog_color, fog_strength = st["fog"]
    save_png(fog_layer(w, 7 * ART, seed + 5, fog_color, fog_strength), key, "fog.png")
    place("fog", 7, [0.75, 0.25], 50)
    front_color, front_strength = st["front"]
    save_png(wisps_layer(w, 6 * ART, seed + 6, front_color, front_strength), key, "haze.png")
    place("haze", 6, [1.18, 0.4], 10, front=True)
    write_json(key, "parallax.json", {"art_tile": ART, "layers": layers})
    # particles
    particles = []
    for kind, color, overrides in st["air"]:
        save_png(particle_texture(kind, color, seed), key, f"{kind}.png")
        entry = {"texture": f"{kind}.png", "color": "#%02x%02x%02x" % color[:3]}
        entry.update(PARTICLES[kind])
        entry.update(overrides)
        particles.append(entry)
    write_json(key, "fx.json", {"art_tile": ART, "particles": particles})
    print(f"{key}: edges, parallax and particles in assets/regions/{key}/ ({mid_kind} mid layer)")


REGIONS = {"paradise": paradise}
for _key in STYLES:
    REGIONS[_key] = (lambda k: lambda texture_dir: styled_region(k, texture_dir))(_key)

if __name__ == "__main__":
    args = sys.argv[1:]
    texture_dir = None
    if "--textures" in args:
        i = args.index("--textures")
        texture_dir = args[i + 1]
        del args[i:i + 2]
    for key in args or list(REGIONS):
        REGIONS[key](texture_dir or os.path.join(ROOT, "..", "editor", "art", key))
