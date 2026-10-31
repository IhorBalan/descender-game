"""Draws the menu theme's textures into godot_assets/ui/theme: the 9-slice button
boxes and panel (12x12, 4 px margins), the slider grabber, the chain divider
under headings and the title screen's cavern backdrop.

    python3 tools/menu_textures.py

The style is "hellforged stone": bevelled dark slabs, an ember-orange glow on
the focused one, sparks rising over a cavern floor.
"""

import os
import random

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "godot_assets", "ui", "theme")
W, H = 320, 180

LEAD = (13, 8, 9, 255)
EMBER = (255, 122, 42, 255)


def box(name, fill, border, bevel, size=12):
    """A 9-slice box: a 1 px border, then a lit top-left and a shaded
    bottom-right edge inside it."""
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, size - 1, size - 1], fill=fill, outline=border)
    light, dark = bevel
    d.line([1, 1, size - 2, 1], fill=light)
    d.line([1, 1, 1, size - 2], fill=light)
    d.line([1, size - 2, size - 2, size - 2], fill=dark)
    d.line([size - 2, 1, size - 2, size - 2], fill=dark)
    im.save(os.path.join(OUT, name))


def grabber(name, fill, border, r=4):
    im = Image.new("RGBA", (r * 2, r * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.polygon([(r, 0), (r * 2 - 1, r), (r, r * 2 - 1), (0, r)], fill=fill, outline=border)
    im.save(os.path.join(OUT, name))


def divider(name, color, w=120, h=5):
    """A row of small chain links."""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for x in range(2, w - 4, 4):
        d.rectangle([x, 1, x + 2, 3], outline=color)
    im.save(os.path.join(OUT, name))


def toggle(name, track, knob, on):
    """A small switch: a rounded track with the knob at the right when on."""
    im = Image.new("RGBA", (16, 9), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, 15, 8], radius=4, fill=track, outline=LEAD)
    x = 9 if on else 1
    d.rounded_rectangle([x, 1, x + 5, 7], radius=3, fill=knob, outline=(60, 18, 10, 255) if on else LEAD)
    im.save(os.path.join(OUT, name))


def backdrop(name):
    random.seed(7)
    im = Image.new("RGBA", (W, H))
    px = im.load()
    top, bottom = (22, 12, 12), (70, 24, 12)
    for y in range(H):
        t = y / (H - 1)
        col = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        for x in range(W):
            px[x, y] = col
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([-40, 140, W + 40, 260], fill=(200, 80, 20, 120))
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(18)))
    d = ImageDraw.Draw(im)
    x = -6
    while x < W + 10:
        w, h = random.randint(8, 22), random.randint(12, 44)
        d.polygon([(x, 0), (x + w, 0), (x + w // 2, h)], fill=(14, 8, 8, 255))
        x += random.randint(6, 16)
    x = -6
    while x < W + 10:
        w, h = random.randint(6, 18), random.randint(6, 26)
        d.polygon([(x, H), (x + w, H), (x + w // 2, H - h)], fill=(28, 12, 10, 255))
        x += random.randint(8, 18)
    for _ in range(60):
        c = random.choice([(255, 138, 58, 255), (255, 200, 90, 255), (200, 60, 20, 255)])
        d.point((random.randrange(W), random.randrange(90, H)), fill=c)
    im.save(os.path.join(OUT, name))


os.makedirs(OUT, exist_ok=True)
box("button_normal.png", (58, 48, 50, 255), LEAD, ((96, 82, 82, 255), (24, 17, 18, 255)))
box("button_focus.png", (104, 36, 20, 255), EMBER, ((255, 176, 112, 255), (60, 18, 10, 255)))
box("button_disabled.png", (42, 34, 36, 255), LEAD, ((60, 50, 50, 255), (20, 14, 14, 255)))
box("panel.png", (28, 20, 20, 235), LEAD, ((80, 66, 66, 255), (18, 12, 12, 255)))
grabber("grabber.png", EMBER, (60, 18, 10, 255))
toggle("toggle_on.png", (150, 48, 20, 255), (255, 206, 150, 255), True)
toggle("toggle_off.png", (40, 30, 32, 255), (120, 104, 100, 255), False)
divider("divider.png", EMBER)
backdrop("title_backdrop.png")
print("drew", sorted(os.listdir(OUT)))
