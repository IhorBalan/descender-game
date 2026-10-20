#!/usr/bin/env python3
"""Generate the placeholder tiles, checkpoint, goal gate, ability feather,
light barrier and story relic.

Everything is drawn from code so it can be regenerated or replaced without
any tooling beyond Python + Pillow:

    python3 tools/gen_placeholders.py

Characters (hero, imp, watcher, angel guard) are the prototype strips from
the art thread, under assets/sprites/<name>/ with sprites.json.
"""
from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "sprites")

# Palette (RGBA)
CLEAR = (0, 0, 0, 0)
OUTLINE = (34, 24, 56, 255)


STONE = (186, 198, 236, 255)
STONE_DARK = (150, 166, 220, 255)
STONE_LIGHT = (222, 230, 250, 255)
CLOUD = (244, 247, 255, 255)
CLOUD_EDGE = (255, 255, 255, 255)
SPIKE = (255, 204, 84, 255)
SPIKE_DARK = (176, 112, 30, 255)

PEDESTAL = (130, 128, 170, 255)
PEDESTAL_DARK = (84, 80, 120, 255)
ORB_OFF = (120, 130, 170, 255)
ORB_ON = (255, 222, 90, 255)
ORB_GLOW = (255, 245, 180, 140)
GATE = (58, 44, 96, 255)
GATE_INNER = (150, 226, 255, 255)
GATE_FRAME = (255, 206, 96, 255)
FEATHER = (250, 250, 255, 255)
FEATHER_SHADE = (190, 200, 240, 255)
FEATHER_SHAFT = (255, 206, 96, 255)
FEATHER_GLOW = (255, 245, 180, 110)
LIGHT = (255, 240, 190, 230)
LIGHT_CORE = (255, 255, 240, 255)
LIGHT_HAZE = (255, 230, 150, 90)
EARTH = (96, 78, 70, 255)
EARTH_DARK = (70, 54, 50, 255)
EARTH_LIGHT = (122, 102, 90, 255)
RUBBLE = (138, 130, 126, 255)
RUBBLE_EDGE = (166, 158, 150, 255)
ASH = (176, 110, 60, 255)
ASH_DARK = (120, 70, 36, 255)
RELIC = (226, 206, 150, 255)
RELIC_SHADE = (170, 140, 90, 255)
RELIC_MARK = (90, 60, 40, 255)
RELIC_GLOW = (255, 245, 180, 90)


def new(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), CLEAR)


def rect(d: ImageDraw.ImageDraw, x0: int, y0: int, x1: int, y1: int, color) -> None:
    """Inclusive pixel rectangle."""
    d.rectangle((x0, y0, x1, y1), fill=color)


def save(img: Image.Image, name: str) -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name)
    img.save(path)
    print("wrote", path)


# --- Tiles --------------------------------------------------------------

def gen_tiles() -> None:
    # 0 = stone fill, 1 = stone top (cloud edge), 2 = spikes
    img = new(48, 16)
    d = ImageDraw.Draw(img)
    speckles = [(2, 3), (9, 5), (13, 11), (5, 12), (11, 1), (7, 8), (1, 14), (14, 7)]
    for t in range(2):
        ox = t * 16
        rect(d, ox, 0, ox + 15, 15, STONE)
        for sx, sy in speckles:
            rect(d, ox + sx, sy, ox + sx + 1, sy, STONE_DARK)
        rect(d, ox + 3, 6, ox + 4, 6, STONE_LIGHT)
        rect(d, ox + 10, 13, ox + 12, 13, STONE_LIGHT)
    # Top variant: cloud-like edge in Heaven's colours
    ox = 16
    rect(d, ox, 0, ox + 15, 3, CLOUD)
    rect(d, ox, 0, ox + 15, 0, CLOUD_EDGE)
    rect(d, ox + 2, 1, ox + 4, 1, CLOUD_EDGE)
    rect(d, ox + 9, 1, ox + 12, 1, CLOUD_EDGE)
    rect(d, ox, 4, ox + 15, 4, STONE_DARK)
    # Spikes
    ox = 32
    for cx in (4, 12):
        for i in range(8):
            y = 15 - i
            half = 3 - i // 2 if i < 6 else 0
            rect(d, ox + cx - half, y, ox + cx + half, y, SPIKE)
            if half > 0:
                rect(d, ox + cx + half, y, ox + cx + half, y, SPIKE_DARK)
    save(img, "tiles.png")


def gen_tiles_earth() -> None:
    """The ruined Earth's tiles, same layout as tiles.png: fill, top, spikes.
    Dark packed soil, a rubble edge on top and rusted spikes."""
    img = new(48, 16)
    d = ImageDraw.Draw(img)
    speckles = [(3, 2), (10, 6), (14, 10), (6, 13), (12, 1), (8, 8), (1, 15), (13, 5)]
    for t in range(2):
        ox = t * 16
        rect(d, ox, 0, ox + 15, 15, EARTH)
        for sx, sy in speckles:
            rect(d, ox + sx, sy, ox + sx + 1, sy, EARTH_DARK)
        rect(d, ox + 4, 9, ox + 5, 9, EARTH_LIGHT)
        rect(d, ox + 11, 3, ox + 12, 3, EARTH_LIGHT)
    ox = 16
    rect(d, ox, 0, ox + 15, 3, RUBBLE)
    rect(d, ox, 0, ox + 15, 0, RUBBLE_EDGE)
    rect(d, ox + 3, 1, ox + 5, 2, RUBBLE_EDGE)
    rect(d, ox + 10, 1, ox + 11, 1, RUBBLE_EDGE)
    rect(d, ox + 7, 2, ox + 8, 3, EARTH_DARK)
    rect(d, ox, 4, ox + 15, 4, EARTH_DARK)
    ox = 32
    for cx in (4, 12):
        for i in range(8):
            y = 15 - i
            half = 3 - i // 2 if i < 6 else 0
            rect(d, ox + cx - half, y, ox + cx + half, y, ASH)
            if half > 0:
                rect(d, ox + cx + half, y, ox + cx + half, y, ASH_DARK)
    save(img, "tiles_earth.png")


def gen_checkpoint() -> None:
    img = new(32, 32)
    d = ImageDraw.Draw(img)
    for f in range(2):
        ox = f * 16
        if f == 1:
            d.ellipse((ox + 2, 4, ox + 13, 20), fill=ORB_GLOW)
        rect(d, ox + 3, 22, ox + 12, 31, PEDESTAL_DARK)
        rect(d, ox + 4, 23, ox + 11, 30, PEDESTAL)
        rect(d, ox + 2, 21, ox + 13, 22, PEDESTAL)
        orb = ORB_ON if f == 1 else ORB_OFF
        rect(d, ox + 5, 9, ox + 10, 20, OUTLINE)
        rect(d, ox + 6, 10, ox + 9, 19, orb)
        rect(d, ox + 7, 11, ox + 7, 13, CLOUD_EDGE if f == 1 else STONE_LIGHT)
    save(img, "checkpoint.png")


def gen_goal() -> None:
    img = new(16, 32)
    d = ImageDraw.Draw(img)
    rect(d, 1, 6, 14, 31, GATE)
    d.ellipse((1, 2, 14, 12), fill=GATE)
    rect(d, 3, 10, 12, 31, GATE_INNER)
    d.ellipse((3, 5, 12, 15), fill=GATE_INNER)
    rect(d, 1, 6, 1, 31, GATE_FRAME)
    rect(d, 14, 6, 14, 31, GATE_FRAME)
    rect(d, 5, 3, 10, 3, GATE_FRAME)
    rect(d, 0, 30, 15, 31, PEDESTAL_DARK)
    save(img, "goal.png")


def gen_feather() -> None:
    """The ability pickup: a white feather with a gold shaft, leaning right,
    with a soft glow behind it."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((1, 1, 14, 14), fill=FEATHER_GLOW)
    # Vane: a leaf from the quill at the bottom-left to the tip top-right.
    vane = [(2, 11, 12), (3, 9, 12), (4, 8, 12), (5, 7, 12), (6, 6, 11), (7, 5, 10),
            (8, 4, 9), (9, 3, 8), (10, 2, 7), (11, 2, 5), (12, 2, 3)]
    for y, x0, x1 in vane:
        rect(d, x0, y, x1, y, FEATHER)
        rect(d, x1, y, x1, y, FEATHER_SHADE)
    # Shaft along the middle of the vane, then the bare quill.
    for i in range(10):
        rect(d, 12 - i, 2 + i, 12 - i, 2 + i, FEATHER_SHAFT)
    rect(d, 2, 12, 2, 12, FEATHER_SHAFT)
    rect(d, 1, 13, 1, 14, OUTLINE)
    save(img, "feather.png")


def gen_light_barrier() -> None:
    """One tile of a wall of angel light: bright vertical shafts in a haze.
    Tiles stack vertically, so the pattern repeats top to bottom."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    rect(d, 2, 0, 13, 15, LIGHT_HAZE)
    for x, core in ((4, False), (7, True), (11, False)):
        rect(d, x, 0, x, 15, LIGHT)
        if core:
            rect(d, x, 0, x + 1, 15, LIGHT_CORE)
    # Breaks in the shafts so they read as streaming light, not a pole.
    for x, y in ((4, 5), (4, 6), (11, 11), (11, 12), (8, 2), (7, 14)):
        rect(d, x, y, x, y, LIGHT_HAZE)
    save(img, "light_barrier.png")


def gen_relic() -> None:
    """A story relic the hero can examine: a small tablet with a tally cut
    into it, in a soft glow. One sprite stands in for every relic."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((0, 0, 15, 15), fill=RELIC_GLOW)
    rect(d, 3, 2, 12, 13, OUTLINE)
    rect(d, 4, 3, 11, 12, RELIC)
    rect(d, 11, 3, 11, 12, RELIC_SHADE)
    rect(d, 4, 12, 11, 12, RELIC_SHADE)
    # The tally: four strokes and a bar.
    for x in (5, 7, 9):
        rect(d, x, 5, x, 9, RELIC_MARK)
    rect(d, 4, 7, 10, 7, RELIC_MARK)
    save(img, "relic.png")


if __name__ == "__main__":
    gen_tiles()
    gen_tiles_earth()
    gen_checkpoint()
    gen_goal()
    gen_feather()
    gen_light_barrier()
    gen_relic()
