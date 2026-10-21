#!/usr/bin/env python3
"""Generate the placeholder checkpoint, goal gate, ability feather, light
barrier, story relic, upgrades and power-ups. (The world's tiles come from
the map tool, under levels/world/.)

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


STONE_LIGHT = (222, 230, 250, 255)
CLOUD_EDGE = (255, 255, 255, 255)
SPIKE = (255, 204, 84, 255)

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
RELIC = (226, 206, 150, 255)
RELIC_SHADE = (170, 140, 90, 255)
RELIC_MARK = (90, 60, 40, 255)
RELIC_GLOW = (255, 245, 180, 90)
HEART = (236, 70, 96, 255)
HEART_DARK = (150, 30, 56, 255)
HEART_CORE = (255, 236, 190, 255)
GLASS = (200, 230, 255, 255)
GLASS_EDGE = (120, 160, 220, 255)
BASIN_WATER = (255, 214, 110, 255)
CORK = (150, 110, 70, 255)
HALO = (255, 214, 90, 255)
HALO_DARK = (180, 130, 40, 255)
HALO_GLOW = (255, 240, 170, 110)
BLADE = (240, 244, 255, 255)
BLADE_FIRE = (255, 170, 60, 255)
HILT = (120, 90, 60, 255)
ZEAL = (140, 220, 255, 255)
ZEAL_CORE = (240, 252, 255, 255)
ZEAL_GLOW = (150, 210, 255, 90)


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


def gen_upgrade_heart() -> None:
    """Kept Light: a heart of light, warm red around a white-gold core, in a
    soft glow. The permanent heart upgrade."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((0, 0, 15, 15), fill=FEATHER_GLOW)
    rows = [(3, 3, 6), (3, 9, 12), (4, 2, 13), (5, 2, 13), (6, 2, 13), (7, 3, 12),
            (8, 4, 11), (9, 5, 10), (10, 6, 9), (11, 7, 8)]
    for y, x0, x1 in rows:
        rect(d, x0 - 1, y, x1 + 1, y, OUTLINE)
    rect(d, 4, 2, 5, 2, OUTLINE)
    rect(d, 10, 2, 11, 2, OUTLINE)
    rect(d, 7, 12, 8, 12, OUTLINE)
    for y, x0, x1 in rows:
        rect(d, x0, y, x1, y, HEART)
        rect(d, x1, y, x1, y, HEART_DARK)
    for y, x0, x1 in rows[5:]:
        rect(d, x0, y, x0, y, HEART)
        rect(d, x1 - 1, y, x1, y, HEART_DARK)
    # The light inside.
    rect(d, 6, 5, 9, 7, HEART_CORE)
    rect(d, 7, 4, 8, 8, HEART_CORE)
    rect(d, 7, 5, 8, 6, CLOUD_EDGE)
    save(img, "upgrade_heart.png")


def gen_upgrade_vial() -> None:
    """Vial of the Basin: a stoppered glass vial of gold Basin water. The
    permanent healing-charge upgrade."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((1, 1, 14, 14), fill=FEATHER_GLOW)
    # Cork and neck.
    rect(d, 6, 1, 9, 3, CORK)
    rect(d, 5, 2, 5, 3, OUTLINE)
    rect(d, 10, 2, 10, 3, OUTLINE)
    rect(d, 5, 4, 10, 5, OUTLINE)
    rect(d, 6, 4, 9, 5, GLASS)
    # Body.
    rect(d, 3, 6, 12, 14, OUTLINE)
    rect(d, 4, 6, 11, 13, GLASS)
    rect(d, 4, 8, 11, 13, BASIN_WATER)
    rect(d, 5, 8, 10, 8, CLOUD_EDGE)
    rect(d, 4, 9, 4, 13, GLASS_EDGE)
    rect(d, 11, 9, 11, 13, GLASS_EDGE)
    rect(d, 5, 10, 5, 11, CLOUD_EDGE)
    rect(d, 4, 14, 11, 14, OUTLINE)
    save(img, "upgrade_vial.png")


def gen_power_halo() -> None:
    """Halo: a Warden's halo, a tilted gold ring with a crack in it. The
    one-hit shield power-up."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((0, 2, 15, 13), fill=HALO_GLOW)
    d.ellipse((1, 4, 14, 11), fill=OUTLINE)
    d.ellipse((2, 5, 13, 10), fill=HALO)
    d.ellipse((4, 7, 11, 9), fill=OUTLINE)
    d.ellipse((4, 6, 11, 8), fill=CLEAR)
    d.ellipse((4, 6, 11, 8), fill=CLEAR)
    # Shade the lower rim, light the upper.
    rect(d, 4, 10, 11, 10, HALO_DARK)
    rect(d, 5, 5, 10, 5, CLOUD_EDGE)
    # The crack.
    rect(d, 11, 6, 11, 6, OUTLINE)
    rect(d, 12, 7, 12, 7, OUTLINE)
    rect(d, 11, 8, 11, 8, OUTLINE)
    save(img, "power_halo.png")


def gen_power_blade() -> None:
    """Lit Blade: a short sword, point up and right, with Choir fire along
    its edge. The damage power-up."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((1, 1, 14, 14), fill=(255, 200, 120, 90))
    # Blade from the hilt (bottom-left) to the point (top-right).
    for i in range(8):
        x, y = 5 + i, 10 - i
        rect(d, x - 1, y, x + 1, y, OUTLINE)
    for i in range(8):
        x, y = 5 + i, 10 - i
        rect(d, x, y, x, y, BLADE)
    rect(d, 12, 3, 12, 3, BLADE)
    # Fire licking off the edge.
    for x, y in ((7, 6), (9, 4), (11, 2), (8, 8), (10, 6), (13, 2)):
        rect(d, x, y, x, y, BLADE_FIRE)
    for x, y in ((8, 5), (10, 3)):
        rect(d, x, y, x, y, SPIKE)
    # Crossguard and grip.
    rect(d, 3, 9, 7, 13, OUTLINE)
    rect(d, 4, 10, 4, 12, HALO)
    rect(d, 6, 10, 6, 12, HALO)
    rect(d, 3, 11, 7, 11, HALO)
    rect(d, 2, 13, 3, 14, OUTLINE)
    rect(d, 3, 13, 3, 13, HILT)
    rect(d, 2, 14, 2, 14, HILT)
    save(img, "power_blade.png")


def gen_power_zeal() -> None:
    """Zeal: three chevrons of wind pointing right, the back ones fading.
    The speed power-up."""
    img = new(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((0, 1, 15, 14), fill=ZEAL_GLOW)
    for n, (ox, color) in enumerate(((2, ZEAL_GLOW), (5, ZEAL), (8, ZEAL_CORE))):
        for i in range(4):
            x = ox + i
            rect(d, x, 4 + i, x, 4 + i, OUTLINE if n else ZEAL)
            rect(d, x, 11 - i, x, 11 - i, OUTLINE if n else ZEAL)
        for i in range(4):
            x = ox + i + 1
            rect(d, x, 4 + i, x, 4 + i, color)
            rect(d, x, 11 - i, x, 11 - i, color)
    save(img, "power_zeal.png")


if __name__ == "__main__":
    gen_checkpoint()
    gen_goal()
    gen_feather()
    gen_light_barrier()
    gen_relic()
    gen_upgrade_heart()
    gen_upgrade_vial()
    gen_power_halo()
    gen_power_blade()
    gen_power_zeal()
