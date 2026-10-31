#!/usr/bin/env python3
"""Generate the gamepad button prompts: one small PNG per button, in an
Xbox set and a PlayStation set, under godot_assets/ui/prompts/.

The Prompts autoload (scripts/prompts.gd) shows them in hints and HUD
messages for whichever gamepad the player last touched. Everything is
drawn from code so it can be regenerated or replaced without any tooling
beyond Python + Pillow:

    python3 tools/gen_prompts.py

Face buttons, the sticks, the d-pad and the menu buttons are 10x10; the
shoulder buttons and triggers are 14x10, so a prompt sits on a line of
the 8px UI font.
"""
from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "godot_assets", "ui", "prompts")

CLEAR = (0, 0, 0, 0)
OUTLINE = (24, 18, 40, 255)
WHITE = (255, 255, 255, 255)
INK = (34, 28, 52, 255)
GREY = (92, 88, 112, 255)
GREY_LIGHT = (150, 146, 176, 255)
GREY_LIT = (255, 242, 179, 255)
XBOX_GREEN = (88, 184, 76, 255)
XBOX_RED = (224, 72, 72, 255)
XBOX_BLUE = (72, 128, 230, 255)
XBOX_YELLOW = (240, 196, 48, 255)
PS_BODY = (58, 54, 74, 255)
PS_CROSS = (120, 170, 255, 255)
PS_CIRCLE = (255, 96, 96, 255)
PS_SQUARE = (255, 140, 200, 255)
PS_TRIANGLE = (96, 220, 140, 255)

# A 3x5 pixel font for the letters the prompts need.
GLYPHS = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"],
    "B": ["##.", "#.#", "##.", "#.#", "##."],
    "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "L": ["#..", "#..", "#..", "#..", "###"],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["###", "..#", "###", "#..", "###"],
    "3": ["###", "..#", ".##", "..#", "###"],
}


def glyph(draw: ImageDraw.ImageDraw, text: str, x: int, y: int, color) -> None:
    """Writes `text` with its top left corner at (x, y), 4px per letter."""
    for letter in text:
        rows = GLYPHS[letter]
        for row, line in enumerate(rows):
            for col, cell in enumerate(line):
                if cell == "#":
                    draw.point((x + col, y + row), color)
        x += 4


DISC_ROWS = (
    "..######..",
    ".########.",
    "##########",
    "##########",
    "##########",
    "##########",
    "##########",
    "##########",
    ".########.",
    "..######..",
)


def disc(draw: ImageDraw.ImageDraw, fill, outline=OUTLINE) -> None:
    """A 10x10 circle: the pixels of DISC_ROWS, the ones on its edge in
    the outline colour."""
    inside = {(x, y) for y, row in enumerate(DISC_ROWS) for x, cell in enumerate(row) if cell == "#"}
    for x, y in inside:
        edge = any((x + dx, y + dy) not in inside for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        draw.point((x, y), outline if edge else fill)


def face(fill, letter: str, ink) -> Image.Image:
    image = Image.new("RGBA", (10, 10), CLEAR)
    draw = ImageDraw.Draw(image)
    disc(draw, fill)
    glyph(draw, letter, 4, 3, ink)
    return image


def ps_face(shape: str) -> Image.Image:
    image = Image.new("RGBA", (10, 10), CLEAR)
    draw = ImageDraw.Draw(image)
    disc(draw, PS_BODY)
    if shape == "cross":
        for i in range(5):
            draw.point((3 + i, 3 + i), PS_CROSS)
            draw.point((7 - i, 3 + i), PS_CROSS)
    elif shape == "circle":
        draw.rectangle((3, 3, 7, 7), outline=PS_CIRCLE)
        for corner in ((3, 3), (7, 3), (3, 7), (7, 7)):
            draw.point(corner, PS_BODY)
    elif shape == "square":
        draw.rectangle((3, 3, 7, 7), outline=PS_SQUARE)
    elif shape == "triangle":
        draw.point((5, 3), PS_TRIANGLE)
        draw.point((4, 4), PS_TRIANGLE)
        draw.point((6, 4), PS_TRIANGLE)
        draw.point((3, 5), PS_TRIANGLE)
        draw.point((7, 5), PS_TRIANGLE)
        draw.line((2, 6, 8, 6), PS_TRIANGLE)
    return image


def shoulder(text: str, trigger: bool) -> Image.Image:
    """A 14x10 rounded button; a trigger has a flat top and a round
    bottom, a bumper the other way round."""
    image = Image.new("RGBA", (14, 10), CLEAR)
    draw = ImageDraw.Draw(image)
    top, bottom = (0, 9)
    draw.rectangle((0, top, 13, bottom), fill=GREY, outline=OUTLINE)
    corners = ((0, top), (13, top)) if not trigger else ((0, bottom), (13, bottom))
    for corner in corners:
        draw.point(corner, CLEAR)
    inner = ((1, top), (12, top), (0, top + 1), (13, top + 1)) if not trigger else (
        (1, bottom), (12, bottom), (0, bottom - 1), (13, bottom - 1))
    for pixel in inner:
        draw.point(pixel, OUTLINE)
    glyph(draw, text, 4, 3, WHITE)
    return image


def menu(kind: str) -> Image.Image:
    """The small buttons in the middle: three lines for start/options,
    two overlapping squares for back/share."""
    image = Image.new("RGBA", (10, 10), CLEAR)
    draw = ImageDraw.Draw(image)
    disc(draw, GREY)
    if kind == "start":
        for y in (3, 5, 7):
            draw.line((3, y, 6, y), WHITE)
    else:
        draw.rectangle((3, 3, 5, 5), outline=WHITE)
        draw.rectangle((5, 5, 7, 7), outline=WHITE)
    return image


def dpad(direction: str | None) -> Image.Image:
    image = Image.new("RGBA", (10, 10), CLEAR)
    draw = ImageDraw.Draw(image)
    draw.rectangle((3, 0, 6, 9), fill=GREY, outline=OUTLINE)
    draw.rectangle((0, 3, 9, 6), fill=GREY, outline=OUTLINE)
    draw.rectangle((4, 1, 5, 8), fill=GREY)
    draw.rectangle((1, 4, 8, 5), fill=GREY)
    arms = {
        "up": (4, 1, 5, 3),
        "down": (4, 6, 5, 8),
        "left": (1, 4, 3, 5),
        "right": (6, 4, 8, 5),
    }
    if direction:
        draw.rectangle(arms[direction], fill=GREY_LIT)
    return image


def stick(direction: str | None, letter: str | None = None) -> Image.Image:
    """A stick seen from above: a ring with the cap inside, pushed to one
    side when there is a direction, lettered when it is pressed in."""
    image = Image.new("RGBA", (10, 10), CLEAR)
    draw = ImageDraw.Draw(image)
    disc(draw, GREY_LIGHT)
    offsets = {None: (0, 0), "up": (0, -2), "down": (0, 2), "left": (-2, 0), "right": (2, 0)}
    dx, dy = offsets[direction]
    if letter:
        glyph(draw, letter, 4, 3, INK)
    else:
        draw.rectangle((3 + dx, 3 + dy, 6 + dx, 6 + dy), fill=GREY_LIT if direction else INK)
    return image


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    icons = {
        "xbox_a": face(XBOX_GREEN, "A", WHITE),
        "xbox_b": face(XBOX_RED, "B", WHITE),
        "xbox_x": face(XBOX_BLUE, "X", WHITE),
        "xbox_y": face(XBOX_YELLOW, "Y", INK),
        "xbox_lb": shoulder("LB", False),
        "xbox_rb": shoulder("RB", False),
        "xbox_lt": shoulder("LT", True),
        "xbox_rt": shoulder("RT", True),
        "xbox_start": menu("start"),
        "xbox_back": menu("back"),
        "xbox_ls": stick(None, "L"),
        "xbox_rs": stick(None, "R"),
        "ps_cross": ps_face("cross"),
        "ps_circle": ps_face("circle"),
        "ps_square": ps_face("square"),
        "ps_triangle": ps_face("triangle"),
        "ps_l1": shoulder("L1", False),
        "ps_r1": shoulder("R1", False),
        "ps_l2": shoulder("L2", True),
        "ps_r2": shoulder("R2", True),
        "ps_options": menu("start"),
        "ps_share": menu("back"),
        "ps_l3": stick(None, "L"),
        "ps_r3": stick(None, "R"),
        "dpad": dpad(None),
        "stick": stick(None),
    }
    for direction in ("up", "down", "left", "right"):
        icons["dpad_%s" % direction] = dpad(direction)
        icons["stick_%s" % direction] = stick(direction)
    for name, image in icons.items():
        image.save(os.path.join(OUT_DIR, "%s.png" % name))
    print("wrote %d prompts to %s" % (len(icons), OUT_DIR))


if __name__ == "__main__":
    main()
