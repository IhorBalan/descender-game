#!/usr/bin/env python3
"""Draws the Tiles V2 liquids: water, lava and the underworld's own (the Styx, blood,
tar, slime), each a sheet of animation frames for its surface row and for its
body. Not a script.

A liquid fills whole cells. Its top cell is drawn with the surface frames (the
liquid stands 3 px below the cell's top, with a travelling ripple), every cell
under it with the body frames. A `clear` liquid is see-through with glints that
come and go; a `thick` one is opaque with a crust and spots that swell. A lit
liquid takes the tile light; an unlit one is drawn as it is and gives light.
"""
from __future__ import annotations

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

LIQUID_FRAMES = 8  # frames of each animation, a loop
SURFACE_DROP = 3  # px of air above the liquid in its top cell


def _rgba(*values):
    return [tuple(int(v[i : i + 2], 16) for i in (0, 2, 4, 6)) for v in values]


# key, how it is drawn (clear: body, light, foam, glint; thick: crest, hot, body,
# deep, crust, vein), frames a second, and what it does: the share of light a
# tile of it passes on (red dies first in water), the light it gives, whether
# the hero swims in it and the hearts it takes from him.
LIQUIDS = [
    {"key": "water", "style": "clear", "colours": _rgba("2c68cc96", "5c9ce8ac", "c6e6fae0", "7cb8f0b0"),
     "fps": 7, "lit": True, "keeps": (0.74, 0.86, 0.93), "emits": (0.0, 0.0, 0.0), "swim": True, "hazard": 0, "order": 20},
    {"key": "lava", "style": "thick", "colours": _rgba("ffee96ff", "ffbc48ff", "ec681eff", "c64016ff", "842816ff", "ff9630ff"),
     "fps": 4, "lit": False, "keeps": (0.86, 0.80, 0.72), "emits": (1.5, 0.80, 0.30), "swim": False, "hazard": 1, "order": 21},
    {"key": "styx", "style": "clear", "colours": _rgba("143c3ac8", "2c5e56d0", "86aa98e0", "3a7068d0"),
     "fps": 5, "lit": True, "keeps": (0.60, 0.80, 0.76), "emits": (0.0, 0.0, 0.0), "swim": True, "hazard": 0, "order": 22},
    {"key": "blood", "style": "thick", "colours": _rgba("ff8a7cff", "e84438ff", "a8141aff", "780c14ff", "48060cff", "d42c2cff"),
     "fps": 4, "lit": False, "keeps": (0.84, 0.60, 0.60), "emits": (0.80, 0.14, 0.12), "swim": False, "hazard": 1, "order": 23},
    {"key": "tar", "style": "thick", "colours": _rgba("6a6a84ff", "44445aff", "181820ff", "101016ff", "08080cff", "30303eff"),
     "fps": 3, "lit": True, "keeps": (0.50, 0.50, 0.55), "emits": (0.0, 0.0, 0.0), "swim": False, "hazard": 1, "order": 24},
    {"key": "slime", "style": "clear", "colours": _rgba("4a8c22d2", "74b83cd8", "c4ee8ce6", "8ed050d8"),
     "fps": 4, "lit": True, "keeps": (0.70, 0.88, 0.60), "emits": (0.05, 0.12, 0.03), "swim": True, "hazard": 0, "order": 25},
    # Falling water (`fall`: body, light, foam, glint): streaks that run down. Nothing swims
    # in it and it hurts nobody; it is drawn as it is, so a fall shows from far off and at night.
    {"key": "falls", "style": "fall", "colours": _rgba("6aa8ec84", "9ccaf6b0", "ffffffe6", "d8ecfdcc"),
     "fps": 12, "lit": False, "keeps": (0.90, 0.94, 0.98), "emits": (0.10, 0.13, 0.17), "swim": False, "hazard": 0, "order": 26},
]


def ripple(x: int, frame: int, swing: float) -> int:
    """The surface's height (px below the cell's top) at column x: a wave one tile
    long that travels a quarter of a tile every two frames, so the loop is seamless."""
    phase = 2 * np.pi * (x + frame * TILE / LIQUID_FRAMES) / TILE
    return SURFACE_DROP + int(round(swing * np.sin(phase)))


def spots(key: str, count: int) -> list[tuple[int, int, int]]:
    """Fixed places in a body tile and the frame each one shows up at."""
    r = rng("liquid", key)
    return [(r.randint(1, TILE - 4), r.randint(1, TILE - 2), r.randrange(LIQUID_FRAMES)) for _ in range(count)]


def clear_frame(liquid: dict, part: str, frame: int) -> np.ndarray:
    body, light, foam, glint = liquid["colours"]
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    a[:, :] = body
    # Glints: short lighter dashes that come and go, each for three frames.
    for x, y, start in spots(liquid["key"], 5):
        if (frame - start) % LIQUID_FRAMES < 3:
            a[y, x : x + 2] = glint
    if part == "surface":
        for x in range(TILE):
            top = ripple(x, frame, 1.0)
            a[:top, x] = 0
            a[top, x] = foam
            a[top + 1, x] = light
    return a


def thick_frame(liquid: dict, part: str, frame: int) -> np.ndarray:
    crest, hot, body, deep, crust, vein = liquid["colours"]
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    a[:, :] = body
    r = rng(liquid["key"], "crust")
    # A dark crust in soft patches and darker depths, the same in every frame.
    for _ in range(5):
        cx, cy, rad = r.randrange(TILE), r.randrange(TILE), r.uniform(1.4, 2.6)
        for y in range(TILE):
            for x in range(TILE):
                dx, dy = min(abs(x - cx), TILE - abs(x - cx)), min(abs(y - cy), TILE - abs(y - cy))
                if dx * dx + dy * dy * 1.8 < rad * rad:
                    a[y, x] = deep if dx * dx + dy * dy * 1.8 > rad * rad * 0.45 else crust
    # Hot spots swell and fade, each on its own beat.
    for x, y, start in spots(liquid["key"], 4):
        beat = (frame - start) % LIQUID_FRAMES
        if beat < 4:
            a[y, x : x + 2] = vein
            if beat in (1, 2):
                a[y, x] = hot
                if y > 0:
                    a[y - 1, x] = vein
    if part == "surface":
        for x in range(TILE):
            top = ripple(x, frame, 0.7)
            a[:top, x] = 0
            a[top, x] = crest
            a[top + 1, x] = hot
            a[top + 2, x] = vein if (x + frame) % 3 else hot
    return a


def fall_frame(liquid: dict, part: str, frame: int) -> np.ndarray:
    """Falling water: a see-through sheet with streaks of light and flecks of foam that
    run down it two pixels a frame, so the loop is seamless; the same from top to foot."""
    body, light, foam, glint = liquid["colours"]
    a = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    a[:, :] = body
    r = rng(liquid["key"], "streaks")
    drop = frame * TILE // LIQUID_FRAMES
    for x in range(TILE):
        kind = r.choice(("light", "light", "glint", "body", "body", "foam"))
        start, length = r.randrange(TILE), r.randint(4, 11)
        if kind == "body":
            continue
        colour = {"light": light, "glint": glint, "foam": foam}[kind]
        for k in range(length if kind != "foam" else 2):
            a[(start + k + drop) % TILE, x] = colour
    return a


DRAW = {"clear": clear_frame, "thick": thick_frame, "fall": fall_frame}


def liquid_sheet(liquid: dict) -> tuple[Image.Image, list]:
    """A liquid's sheet: the surface frames in a row, the body frames under them."""
    key = liquid["key"]
    sheet = np.zeros((2 * TILE, LIQUID_FRAMES * TILE, 4), dtype=np.uint8)
    sprites = []
    for row, part in enumerate(("surface", "body")):
        for f in range(LIQUID_FRAMES):
            sheet[row * TILE : (row + 1) * TILE, f * TILE : (f + 1) * TILE] = DRAW[liquid["style"]](liquid, part, f)
            sprites.append((f"tv2_{key}_{part}_{f}", f, row))
    return Image.fromarray(sheet, "RGBA"), sprites
