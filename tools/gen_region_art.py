#!/usr/bin/env python3
"""Generate placeholder region art for the painted look: edge pieces (tufts,
hanging growth, side moss) and parallax layers, at 64 pixels per tile.

    python3 tools/gen_region_art.py [paradise ...]

Everything is drawn from code (needs Pillow) so it can be regenerated or
replaced piece by piece: drop painted PNGs with the same names into
assets/regions/<key>/ and the JSON next to them keeps working. See
scripts/edge_decor.gd and scripts/backdrop.gd for what the JSON means.
"""
from __future__ import annotations

import json
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = 64  # pixels per tile

# Per region: palette for the growth, the hills and the fog.
REGIONS = {
    "paradise": {
        "leaf": [(214, 226, 236), (186, 204, 222), (236, 242, 248)],
        "stem": (150, 170, 196),
        "moss": (196, 212, 230),
        "hill_far": (118, 132, 178),
        "hill_near": (92, 104, 150),
        "fog": (210, 222, 240),
        "sky": (150, 170, 210),
    },
}


def save(img: Image.Image, key: str, name: str) -> None:
    path = os.path.join(ROOT, "assets", "regions", key, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def tuft(pal: dict, seed: int) -> Image.Image:
    """Grass blades standing on the cell's top edge, 1 tile wide, overhanging upward."""
    rng = random.Random(seed)
    w, h = ART, ART * 3 // 4
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(rng.randint(14, 22)):
        x = rng.randint(4, w - 5)
        height = rng.randint(h // 3, h - 6)
        lean = rng.uniform(-0.45, 0.45)
        color = rng.choice(pal["leaf"])
        pts = [(x, h), (x + lean * height * 0.5, h - height * 0.6), (x + lean * height, h - height)]
        d.line(pts, fill=color, width=rng.randint(2, 4), joint="curve")
    # a low cushion of leaves along the edge
    for _ in range(rng.randint(18, 26)):
        x = rng.randint(0, w - 1)
        r = rng.randint(3, 6)
        d.ellipse((x - r, h - r - rng.randint(0, 6), x + r, h + r), fill=rng.choice(pal["leaf"]))
    return img


def vine(pal: dict, seed: int) -> Image.Image:
    """Hanging growth under a cell's underside, 1 tile wide, up to 1.5 tiles long."""
    rng = random.Random(seed)
    w, h = ART, ART * 3 // 2
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(rng.randint(3, 6)):
        x = rng.randint(6, w - 7)
        length = rng.randint(h // 3, h - 4)
        sway = rng.uniform(-0.25, 0.25)
        pts = [(x + sway * t * length * math.sin(t * 3.0), t * length) for t in [i / 12 for i in range(13)]]
        d.line(pts, fill=pal["stem"], width=2, joint="curve")
        for px, py in pts[2::3]:
            r = rng.randint(2, 4)
            d.ellipse((px - r, py - r, px + r, py + r), fill=rng.choice(pal["leaf"]))
    return img


def moss(pal: dict, seed: int, right: bool) -> Image.Image:
    """A soft fringe down a side, a quarter tile wide, 1 tile tall."""
    rng = random.Random(seed)
    w, h = ART // 4, ART
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(rng.randint(16, 24)):
        y = rng.randint(0, h - 1)
        r = rng.randint(2, 5)
        x = (w - rng.randint(0, 5)) if not right else rng.randint(0, 5)
        d.ellipse((x - r, y - r, x + r, y + r), fill=rng.choice([pal["moss"], *pal["leaf"]]))
    return img


def hills(pal: dict, seed: int, color: tuple, w: int, h: int, bumps: int, base: float) -> Image.Image:
    """A rolling silhouette on a transparent sheet; the lower part is filled."""
    rng = random.Random(seed)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    centres = [(rng.uniform(0, w), rng.uniform(h * 0.25, h * 0.7), rng.uniform(w * 0.12, w * 0.3)) for _ in range(bumps)]
    pts = []
    for x in range(w + 1):
        y = h * base
        for cx, cy, r in centres:
            for ox in (-w, 0, w):  # wrap, so the layer repeats seamlessly
                dx = x - (cx + ox)
                if abs(dx) < r:
                    y = min(y, cy + (1 - math.cos(dx / r * math.pi / 2)) * (h * base - cy))
        pts.append((x, y))
    d.polygon([(0, h), *pts, (w, h)], fill=color)
    return img


def fog(pal: dict, seed: int, w: int, h: int) -> Image.Image:
    rng = random.Random(seed)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(40):
        x, y = rng.uniform(0, w), rng.uniform(h * 0.3, h)
        rx, ry = rng.uniform(w * 0.1, w * 0.3), rng.uniform(h * 0.08, h * 0.2)
        for ox in (-w, 0, w):
            d.ellipse((x + ox - rx, y - ry, x + ox + rx, y + ry), fill=(*pal["fog"], 40))
    return img.filter(ImageFilter.GaussianBlur(18))


def gen(key: str) -> None:
    pal = REGIONS[key]
    tops, bottoms, lefts, rights = [], [], [], []
    for i in range(3):
        save(tuft(pal, 100 + i), key, f"tuft_{i}.png")
        tops.append(f"tuft_{i}.png")
    for i in range(2):
        save(vine(pal, 200 + i), key, f"vine_{i}.png")
        bottoms.append(f"vine_{i}.png")
    save(moss(pal, 300, False), key, "moss_l.png")
    save(moss(pal, 301, True), key, "moss_r.png")
    lefts.append("moss_l.png")
    rights.append("moss_r.png")
    with open(os.path.join(ROOT, "assets", "regions", key, "edges.json"), "w") as f:
        json.dump({"art_tile": ART, "top": tops, "bottom": bottoms, "left": lefts, "right": rights,
                   "chance": 1.0, "bottom_chance": 0.5, "left_chance": 0.6, "right_chance": 0.6}, f, indent=2)
        f.write("\n")
    # Parallax: one screen wide at the art scale (30 tiles), hills and fog.
    w, h = 30 * ART, 17 * ART
    save(hills(pal, 1, pal["hill_far"], w, h, 5, 0.75), key, "hills_far.png")
    save(hills(pal, 2, pal["hill_near"], w, h, 4, 0.9), key, "hills_near.png")
    save(fog(pal, 3, w, h), key, "fog.png")
    with open(os.path.join(ROOT, "assets", "regions", key, "parallax.json"), "w") as f:
        json.dump({"art_tile": ART, "layers": [
            {"file": "hills_far.png", "scroll": [0.25, 0.85], "y": -60},
            {"file": "hills_near.png", "scroll": [0.5, 0.92], "y": 20},
            {"file": "fog.png", "scroll": [0.7, 0.97], "y": 60},
        ]}, f, indent=2)
        f.write("\n")
    print(f"{key}: edges and parallax written to assets/regions/{key}/")


if __name__ == "__main__":
    for key in sys.argv[1:] or list(REGIONS):
        gen(key)
