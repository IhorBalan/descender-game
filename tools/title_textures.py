"""Draws the title screen's pixel art into Assets/Art/UI/Title: the gilded
DESCENDER logo, the halo and star flare behind it, the gold focus bar (and a wide one
for the save slots) with its diamond emblem, the diamond rules between the menu items and under the tagline,
the line that frames the key hints, and the 9 px pixel font sheet the
screen's text is drawn from (pixel_font.png and its metrics, pixel_font.json).

    python3 tools/title_textures.py                 # Python 3 with Pillow
    py tools/title_textures.py --out scratch/title  # Windows; write somewhere else
    py tools/title_textures.py --preview mock.png   # also a 320x180 mock of the screen

Everything is drawn at the 320x180 game resolution with hard pixels: no
antialiasing, gradients in a few flat bands, glows as a handful of alpha steps.
The screen behind it is a flat dark colour (TitleScreen.Background), so there is
no backdrop image. A new file under Assets/ gets its .meta (unity_meta); an
existing .meta is never touched.
"""

import argparse
import json
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "unity"))
import unity_meta  # noqa: E402

OUT = os.path.join(unity_meta.ASSETS, "Art", "UI", "Title")
FONT = os.path.join(unity_meta.ASSETS, "Art", "Fonts", "PirataOne-Regular.ttf")

CLEAR = (0, 0, 0, 0)
# The gold ramp, light to dark, and the ink that outlines it.
GOLD = [(255, 243, 214), (252, 222, 160), (240, 190, 104), (214, 150, 66), (168, 104, 40), (118, 68, 26)]
INK = (34, 18, 10, 255)
GLOW = (255, 178, 84)
# Matches TitleScreen.Background, for the preview only.
BACKGROUND = (15, 12, 12)


def rgba(color, alpha=255):
    return (color[0], color[1], color[2], alpha)


def logo():
    """DESCENDER in Pirata One, the first and last letters taller, filled with
    the gold ramp in bands from top to bottom, a light top edge, a dark ink
    outline and a one-pixel drop shadow."""
    big = ImageFont.truetype(FONT, 40)
    small = ImageFont.truetype(FONT, 31)
    parts = [("D", big), ("ESCENDE", small), ("R", big)]
    mask = Image.new("L", (260, 60), 0)
    d = ImageDraw.Draw(mask)
    d.fontmode = "1"
    baseline = 48
    x = 0
    for text, font in parts:
        d.text((x, baseline), text, font=font, fill=255, anchor="ls")
        x += int(d.textlength(text, font=font)) + 1
    box = mask.getbbox()
    mask = mask.crop(box)
    w, h = mask.size
    im = Image.new("RGBA", (w + 4, h + 4), CLEAR)
    m = mask.load()
    px = im.load()

    def inside(x, y):
        return 0 <= x < w and 0 <= y < h and m[x, y] > 0

    # Shadow, then the ink outline, then the fill.
    for y in range(h):
        for x in range(w):
            if inside(x, y):
                px[x + 2, y + 3] = (8, 4, 3, 200)
    for y in range(-1, h + 1):
        for x in range(-1, w + 1):
            if inside(x, y):
                continue
            if any(inside(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[x + 2, y + 2] = INK
    for y in range(h):
        # Bands from a pale crown down to a deep foot.
        t = y / max(1, h - 1)
        band = 1 if t < 0.3 else (2 if t < 0.52 else (3 if t < 0.74 else 4))
        for x in range(w):
            if inside(x, y):
                color = GOLD[band]
                if not inside(x, y - 1):
                    color = GOLD[0]
                elif not inside(x, y + 1):
                    color = GOLD[5]
                px[x + 2, y + 2] = rgba(color)
    save(im, "title_logo.png")
    return im


def halo(w=200, h=96):
    """The glow behind the logo: a thin ring broken by the logo, a soft warm
    bloom in three alpha steps, and a star flare on the vertical axis."""
    im = Image.new("RGBA", (w, h), CLEAR)
    cx, cy = w // 2, h // 2 + 4
    px = im.load()
    for y in range(h):
        for x in range(w):
            dx = (x - cx) / 92.0
            dy = (y - cy) / 40.0
            r = dx * dx + dy * dy
            a = 0
            if r < 1.0:
                # Eight flat steps that fall to nothing at the edge.
                a = int(round((1.0 - r) ** 2 * 8)) * 3
            if a:
                px[x, y] = rgba(GLOW, a)
    # The ring: a 1 px circle, brightest at the top, fading towards the sides.
    radius = 38
    for i in range(720):
        angle = math.radians(i / 2)
        x = int(round(cx + math.cos(angle) * radius * 1.25))
        y = int(round(cy - 2 - math.sin(angle) * radius))
        up = math.sin(angle)
        if up < -0.2:
            continue
        a = int(70 + 150 * max(0.0, up))
        if 0 <= x < w and 0 <= y < h:
            px[x, y] = rgba(GOLD[1], min(255, a))
    # The flare: a vertical spear of light with a four-pointed star at its head.
    # It stops above the logo, which the halo sits behind.
    for y in range(0, 30):
        dist = abs(y - 10)
        a = max(0, 255 - dist * 4)
        if a > 0:
            px[cx, y] = rgba(GOLD[0], a)
            if dist < 18:
                side = max(0, a // 3)
                px[cx - 1, y] = rgba(GOLD[1], side)
                px[cx + 1, y] = rgba(GOLD[1], side)
    for i in range(1, 9):
        a = 255 - i * 28
        for sx, sy in ((i, 0), (-i, 0)):
            px[cx + sx, 10 + sy] = rgba(GOLD[0], a)
    for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        px[cx + dx, 10 + dy] = rgba(GOLD[0], 200)
    save(im, "title_halo.png")
    return im


def fade(x, w, solid):
    """How strong a horizontal line is at x: full in the middle `solid` share,
    then falling off in steps to nothing at the ends."""
    c = abs(x - (w - 1) / 2) / ((w - 1) / 2)
    if c <= solid:
        return 1.0
    t = (c - solid) / (1 - solid)
    # Eight flat steps, so the fall-off stays hard-edged pixel art.
    return max(0.0, round((1.0 - t) * 8) / 8)


def diamond(px, cx, cy, r, fill, edge):
    for y in range(-r, r + 1):
        for x in range(-r, r + 1):
            d = abs(x) + abs(y)
            if d == r:
                px[cx + x, cy + y] = edge
            elif d < r:
                px[cx + x, cy + y] = fill


def rule(name, w, r, solid):
    """A thin gold line that fades at both ends, with a diamond in the middle."""
    h = r * 2 + 1
    im = Image.new("RGBA", (w, h), CLEAR)
    px = im.load()
    cy = h // 2
    for x in range(w):
        a = int(150 * fade(x, w, solid))
        px[x, cy] = rgba(GOLD[3], a)
    diamond(px, w // 2, cy, r, rgba(GOLD[2]), rgba(GOLD[0]))
    if r >= 3:
        diamond(px, w // 2, cy, r - 2, rgba(GOLD[4]), rgba(GOLD[1]))
        for side in (-1, 1):
            px[w // 2 + side * (r + 2), cy] = rgba(GOLD[1])
    save(im, name)
    return im


def hint_line(w=40):
    """The short line either side of the key hints, bright at its inner end."""
    im = Image.new("RGBA", (w, 1), CLEAR)
    px = im.load()
    for x in range(w):
        px[x, 0] = rgba(GOLD[3], int(140 * round(x / (w - 1) * 8) / 8))
    save(im, "rule_hints.png")
    return im


def focus_bar(name, w, h=15, emblem=True):
    """The bar behind the focused item: a band of warm light that fades at both
    ends, gold lines along its top and bottom edges, and a diamond emblem in a
    ring at its left that points the way, as a sword guard would."""
    im = Image.new("RGBA", (w, h), CLEAR)
    px = im.load()
    for x in range(w):
        f = fade(x, w, 0.35)
        for y in range(1, h - 1):
            # Brighter at the middle row, in three steps.
            mid = abs(y - h // 2)
            base = 120 if mid <= 1 else (96 if mid <= 3 else 72)
            px[x, y] = rgba((150, 88, 30), int(base * f))
        px[x, 0] = rgba(GOLD[1], int(230 * f))
        px[x, h - 1] = rgba(GOLD[2], int(200 * f))
    save(im, name)
    if not emblem:
        return im, None

    # The emblem, its own sprite so it can pulse: a diamond inside a diamond
    # ring with a short blade of light either side.
    size = 15
    em = Image.new("RGBA", (size + 16, size), CLEAR)
    ep = em.load()
    c = size // 2
    ox = 8
    for i in range(1, 9):
        a = 230 - i * 24
        ep[ox + c - 7 - i, c] = rgba(GOLD[0], a) if ox + c - 7 - i >= 0 else (0, 0, 0, 0)
        ep[ox + c + 7 + i, c] = rgba(GOLD[0], a) if ox + c + 7 + i < size + 16 else (0, 0, 0, 0)
    diamond(ep, ox + c, c, 7, rgba((40, 20, 8), 220), rgba(GOLD[1]))
    diamond(ep, ox + c, c, 5, CLEAR, rgba(GOLD[3]))
    diamond(ep, ox + c, c, 3, rgba(GOLD[1]), rgba(GOLD[0]))
    ep[ox + c, c] = rgba((255, 255, 240))
    save(em, "focus_emblem.png")
    return im, em


def pixel_font(size=9, sheet_width=256):
    """Pirata One rasterised at its pixel size with no antialiasing, every
    printable ASCII character in a row of cells as tall as a line, white on
    clear for the UI to tint, and the metrics beside it as JSON: for each
    character its cell (x, y, width) in the sheet, where its ink starts relative
    to the pen (often left of it, for the swashed capitals) and how far the pen,
    in fractions of a pixel,
    then moves. PixelText draws from the pair; TextMeshPro's 9 px atlas clipped
    the swashes off the capitals."""
    font = ImageFont.truetype(FONT, size)
    ascent, descent = font.getmetrics()
    line = ascent + descent
    glyphs = {}
    cells = []
    x, y = 1, 1
    for code in range(32, 127):
        c = chr(code)
        left, _, right, _ = font.getbbox(c, anchor="ls")
        # A pixel of slack either side: the hard-edged raster can reach past the smooth box.
        left -= 1
        right += 1
        width = max(0, right - left) if c != " " else 0
        if x + width + 1 > sheet_width:
            x, y = 1, y + line + 1
        cells.append((c, x, y, left))
        glyphs[str(code)] = [x, y, width, left, round(font.getlength(c), 3)]
        x += width + 1
    im = Image.new("RGBA", (sheet_width, y + line + 1), CLEAR)
    d = ImageDraw.Draw(im)
    d.fontmode = "1"
    for c, cx, cy, left in cells:
        d.text((cx - left, cy + ascent), c, font=font, fill=(255, 255, 255, 255), anchor="ls")
    save(im, "pixel_font.png")
    path = os.path.join(OUT, "pixel_font.json")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"size": size, "line": line, "ascent": ascent, "glyphs": glyphs}, f, indent=1, sort_keys=True)
        f.write("\n")
    unity_meta.ensure_meta(path)


def save(im, name):
    path = os.path.join(OUT, name)
    im.save(path)
    unity_meta.ensure_meta(path)


def preview(path, parts):
    """A mock of the whole screen, to judge the art without opening Unity. The
    text is drawn with the same font at the sizes the screen uses."""
    W, H = 320, 180
    im = Image.new("RGBA", (W, H), rgba(BACKGROUND))
    logo_im, halo_im, rule_small, rule_big, bar, emblem, line = parts
    im.alpha_composite(halo_im, ((W - halo_im.width) // 2, -4))
    im.alpha_composite(logo_im, ((W - logo_im.width) // 2, 18))
    d = ImageDraw.Draw(im)
    d.fontmode = "1"
    f8 = ImageFont.truetype(FONT, 8)
    f9 = ImageFont.truetype(FONT, 9)
    tag = "A man with half-finished angel powers descends from Heaven"
    d.text((W // 2, 60), tag, font=f8, fill=(170, 150, 128), anchor="mm")
    im.alpha_composite(rule_big, ((W - rule_big.width) // 2, 64))
    items = ["New Game", "Continue", "Load Game", "Settings", "Credits", "Quit"]
    top, step = 84, 14
    for i, text in enumerate(items):
        y = top + i * step
        if i == 0:
            im.alpha_composite(bar, ((W - bar.width) // 2, y - bar.height // 2))
            im.alpha_composite(emblem, ((W - bar.width) // 2 - 4, y - emblem.height // 2))
        elif i < len(items):
            im.alpha_composite(rule_small, ((W - rule_small.width) // 2, y - step // 2 - 1))
        color = (255, 240, 212) if i == 0 else ((110, 98, 90) if i in (1, 2) else (196, 178, 156))
        d.text((W // 2, y), " ".join(text.upper()), font=f9, fill=color, anchor="mm")
    hints = "Arrows  Navigate     Enter  Confirm     Esc  Back"
    half = int(d.textlength(hints, font=f8)) // 2
    im.alpha_composite(line, (W // 2 - half - 6 - line.width, 171))
    im.alpha_composite(line.transpose(Image.FLIP_LEFT_RIGHT), (W // 2 + half + 6, 171))
    d.text((W // 2, 171), hints, font=f8, fill=(150, 134, 118), anchor="mm")
    im.convert("RGB").resize((W * 4, H * 4), Image.NEAREST).save(path)


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=OUT, help="the folder to write into (default: Assets/Art/UI/Title)")
    parser.add_argument("--preview", help="also write a 4x mock of the screen to this path")
    args = parser.parse_args()
    OUT = os.path.abspath(args.out)
    os.makedirs(OUT, exist_ok=True)
    parts = draw_all()
    if args.preview:
        preview(args.preview, parts)


def draw_all():
    logo_im = logo()
    halo_im = halo()
    rule_small = rule("rule_item.png", 120, 1, 0.3)
    rule_big = rule("rule_tagline.png", 168, 3, 0.4)
    line = hint_line()
    bar, emblem = focus_bar("focus_bar.png", 184)
    focus_bar("focus_bar_wide.png", 300, emblem=False)
    pixel_font()
    print("drew", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))
    return logo_im, halo_im, rule_small, rule_big, bar, emblem, line


if __name__ == "__main__":
    main()
