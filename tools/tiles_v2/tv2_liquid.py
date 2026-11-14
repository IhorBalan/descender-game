#!/usr/bin/env python3
"""Draws the Tiles V2 liquids: water and lava, each a sheet of animation frames for
its surface row and for its body. Not a script.

A liquid fills whole cells. Its top cell is drawn with the surface frames (the
liquid stands 3 px below the cell's top, with a travelling ripple), every cell
under it with the body frames. Water is see-through and takes the light; lava
is opaque, drawn unlit, and gives light.
"""
from __future__ import annotations

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

LIQUID_FRAMES = 8  # frames of each animation, a loop
SURFACE_DROP = 3  # px of air above the liquid in its top cell

# key, frames a second, how it is drawn and what it does to the light: the share
# of light a tile of it passes on (red dies first in water), the light it gives.
LIQUIDS = [
    {
        "key": "water",
        "fps": 7,
        "lit": True,
        "keeps": (0.74, 0.86, 0.93),
        "emits": (0.0, 0.0, 0.0),
        "swim": True,
        "hazard": 0,
        "order": 20,
    },
    {
        "key": "lava",
        "fps": 4,
        "lit": False,
        "keeps": (0.86, 0.80, 0.72),
        "emits": (1.5, 0.80, 0.30),
        "swim": False,
        "hazard": 1,
        "order": 21,
    },
]

WATER = {
    "body": (44, 104, 204, 150),
    "light": (92, 156, 232, 172),
    "foam": (198, 230, 250, 224),
    "glint": (124, 184, 240, 176),
}
LAVA = {
    "crest": (255, 238, 150, 255),
    "hot": (255, 188, 72, 255),
    "body": (236, 104, 30, 255),
    "deep": (198, 64, 22, 255),
    "crust": (132, 40, 22, 255),
    "vein": (255, 150, 48, 255),
}


def ripple(x: int, frame: int, swing: float) -> int:
    """The surface's height (px below the cell's top) at column x: a wave one tile
    long that travels a quarter of a tile every two frames, so the loop is seamless."""
    phase = 2 * np.pi * (x + frame * TILE / LIQUID_FRAMES) / TILE
    return SURFACE_DROP + int(round(swing * np.sin(phase)))


def spots(key: str, count: int) -> list[tuple[int, int, int]]:
    """Fixed places in a body tile and the frame each one shows up at."""
    r = rng("liquid", key)
    return [(r.randint(1, TILE - 4), r.randint(1, TILE - 2), r.randrange(LIQUID_FRAMES)) for _ in range(count)]


def water_frame(part: str, frame: int) -> np.ndarray:
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    a[:, :] = WATER["body"]
    # Glints: short lighter dashes that come and go, each for three frames.
    for x, y, start in spots("water", 5):
        if (frame - start) % LIQUID_FRAMES < 3:
            a[y, x : x + 2] = WATER["glint"]
    if part == "surface":
        for x in range(TILE):
            top = ripple(x, frame, 1.0)
            a[:top, x] = 0
            a[top, x] = WATER["foam"]
            a[top + 1, x] = WATER["light"]
    return a


def lava_frame(part: str, frame: int) -> np.ndarray:
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    a[:, :] = LAVA["body"]
    r = rng("lava", "crust")
    # A dark crust in soft patches and darker depths, the same in every frame.
    for _ in range(5):
        cx, cy, rad = r.randrange(TILE), r.randrange(TILE), r.uniform(1.4, 2.6)
        for y in range(TILE):
            for x in range(TILE):
                dx, dy = min(abs(x - cx), TILE - abs(x - cx)), min(abs(y - cy), TILE - abs(y - cy))
                if dx * dx + dy * dy * 1.8 < rad * rad:
                    a[y, x] = LAVA["deep"] if dx * dx + dy * dy * 1.8 > rad * rad * 0.45 else LAVA["crust"]
    # Hot spots swell and fade, each on its own beat.
    for x, y, start in spots("lava", 4):
        beat = (frame - start) % LIQUID_FRAMES
        if beat < 4:
            a[y, x : x + 2] = LAVA["vein"]
            if beat in (1, 2):
                a[y, x] = LAVA["hot"]
                if y > 0:
                    a[y - 1, x] = LAVA["vein"]
    if part == "surface":
        for x in range(TILE):
            top = ripple(x, frame, 0.7)
            a[:top, x] = 0
            a[top, x] = LAVA["crest"]
            a[top + 1, x] = LAVA["hot"]
            a[top + 2, x] = LAVA["vein"] if (x + frame) % 3 else LAVA["hot"]
    return a


DRAW = {"water": water_frame, "lava": lava_frame}


def liquid_sheet(liquid: dict) -> tuple[Image.Image, list]:
    """A liquid's sheet: the surface frames in a row, the body frames under them."""
    key = liquid["key"]
    sheet = np.zeros((2 * TILE, LIQUID_FRAMES * TILE, 4), dtype=np.uint8)
    sprites = []
    for row, part in enumerate(("surface", "body")):
        for f in range(LIQUID_FRAMES):
            sheet[row * TILE : (row + 1) * TILE, f * TILE : (f + 1) * TILE] = DRAW[key](part, f)
            sprites.append((f"tv2_{key}_{part}_{f}", f, row))
    return Image.fromarray(sheet, "RGBA"), sprites
