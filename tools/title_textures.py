"""Draws the title screen's art into Assets/Art/UI/Title, at the 1920x1080
the screen is laid out in: the gilded DESCENDER logo, the halo and star flare
behind it, the gold focus bar (and a wide one for the save slots) with its
diamond emblem, the hairline rules between the menu items and under the
tagline, the short lines either side of the tagline and the key hints, the
soft mote the rising embers are drawn with, the key cap and the D-pad icon.

    python3 tools/title_textures.py                 # Python 3 with Pillow and numpy
    py tools/title_textures.py --out scratch/title  # Windows; write somewhere else
    py tools/title_textures.py --preview mock.png   # also a mock of the whole screen

The look is a modern dark-fantasy title: smooth, antialiased, soft glows over
a flat dark ground (TitleScreen.Background), so there is no backdrop image.
The logo is set in Cinzel Bold (Assets/Art/Fonts, OFL). A new file under
Assets/ gets its .meta (unity_meta: bilinear, uncompressed, no mipmaps); an
existing .meta is never touched, except that the key cap's nine-slice border
is written into it.
"""

import argparse
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "unity"))
import unity_meta  # noqa: E402

OUT = os.path.join(unity_meta.ASSETS, "Art", "UI", "Title")
FONTS = os.path.join(unity_meta.ASSETS, "Art", "Fonts")
BOLD = os.path.join(FONTS, "Cinzel-Bold.ttf")
REGULAR = os.path.join(FONTS, "Cinzel-Regular.ttf")

# Matches TitleScreen.Background, for the preview only.
BACKGROUND = (14, 12, 12)
GOLD = np.array([236, 190, 112], dtype=np.float32)
PALE = np.array([255, 238, 200], dtype=np.float32)
AMBER = np.array([206, 122, 40], dtype=np.float32)
KEY_BORDER = 14


def to_image(rgb, alpha):
    """An RGBA image from float arrays: rgb 0..255 (h, w, 3), alpha 0..1 (h, w)."""
    a = np.clip(alpha, 0.0, 1.0)
    out = np.zeros(a.shape + (4,), dtype=np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = (a * 255.0 + 0.5).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def over(base, top):
    """Composites one premultiplied-free RGBA float layer pair over another: (rgb, a) tuples."""
    rgb_b, a_b = base
    rgb_t, a_t = top
    a = a_t + a_b * (1.0 - a_t)
    safe = np.where(a > 1e-6, a, 1.0)
    rgb = (rgb_t * a_t[..., None] + rgb_b * a_b[..., None] * (1.0 - a_t[..., None])) / safe[..., None]
    return rgb, a


def blur(mask, radius):
    """A gaussian blur of a 0..1 float mask."""
    if radius <= 0:
        return mask
    # Separable: one 1D kernel down the rows, then along the columns, edges padded with zero.
    reach = int(math.ceil(radius * 3))
    xs = np.arange(-reach, reach + 1, dtype=np.float32)
    kernel = np.exp(-(xs * xs) / (2 * radius * radius))
    kernel /= kernel.sum()
    out = np.apply_along_axis(lambda row: np.convolve(row, kernel, mode="same"), 1, mask.astype(np.float32))
    return np.apply_along_axis(lambda col: np.convolve(col, kernel, mode="same"), 0, out)


def flat(shape, color):
    rgb = np.zeros(shape + (3,), dtype=np.float32)
    rgb[...] = color
    return rgb


def ramp(t, stops):
    """Colour along `t` (any shape, 0..1) from (position, colour) stops."""
    t = np.clip(t, 0.0, 1.0)
    out = np.zeros(t.shape + (3,), dtype=np.float32)
    for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
        inside = (t >= p0) & (t <= p1)
        k = ((t - p0) / max(1e-6, p1 - p0))[..., None]
        out = np.where(inside[..., None], np.array(c0) * (1 - k) + np.array(c1) * k, out)
    return out


def logo():
    """DESCENDER in Cinzel Bold, the first and last letters taller, cast in
    gold: a metal ramp from a pale crown through amber to a bright rim and a
    deep foot, a bevel lit from above, fine grain, a dark edge, a soft drop
    shadow and a warm outer glow."""
    scale = 2
    big = ImageFont.truetype(BOLD, 196 * scale)
    small = ImageFont.truetype(BOLD, 150 * scale)
    parts = [("D", big), ("ESCENDE", small), ("R", big)]
    tracking = 10 * scale
    w0, h0 = 2000 * scale, 360 * scale
    mask = Image.new("L", (w0, h0), 0)
    d = ImageDraw.Draw(mask)
    x, baseline = 40 * scale, 280 * scale
    for text, font in parts:
        for c in text:
            d.text((x, baseline), c, font=font, fill=255, anchor="ls")
            x += d.textlength(c, font=font) + tracking
    mask = mask.crop(mask.getbbox())
    mask = mask.resize((mask.width // scale, mask.height // scale), Image.LANCZOS)
    pad = 70
    w, h = mask.width + pad * 2, mask.height + pad * 2
    m = np.zeros((h, w), dtype=np.float32)
    m[pad : pad + mask.height, pad : pad + mask.width] = np.asarray(mask, dtype=np.float32) / 255.0

    ys = (np.arange(h, dtype=np.float32) - pad) / mask.height
    t = np.repeat(ys[:, None], w, axis=1)
    metal = ramp(
        t,
        [
            (0.0, (255, 244, 214)),
            (0.30, (240, 200, 128)),
            (0.52, (196, 132, 58)),
            (0.60, (255, 226, 160)),
            (0.66, (226, 170, 90)),
            (0.86, (150, 90, 34)),
            (1.0, (92, 52, 20)),
        ],
    )
    # The bevel: the blurred letter shape's slope, light where it faces up.
    soft = blur(m, 3.0)
    slope = np.zeros_like(soft)
    slope[1:-1, :] = soft[:-2, :] - soft[2:, :]
    light = np.clip(-slope * 9.0, 0, 1)
    shade = np.clip(slope * 9.0, 0, 1)
    metal = metal + light[..., None] * 70.0 - shade[..., None] * 60.0
    rng = np.random.default_rng(11)
    grain = rng.normal(0.0, 1.0, (h, w)).astype(np.float32)
    metal = metal * (1.0 + 0.05 * blur(grain, 0.6)[..., None])

    edge = np.clip(blur(m, 1.6) * 3.0, 0, 1)
    shadow = np.roll(blur(m, 9.0), 8, axis=0) * 0.85
    glow = blur(m, 26.0) * 0.55

    layer = (flat((h, w), (0, 0, 0)), np.zeros((h, w), dtype=np.float32))
    layer = over(layer, (flat((h, w), AMBER), glow))
    layer = over(layer, (flat((h, w), (4, 2, 1)), shadow))
    layer = over(layer, (flat((h, w), (36, 18, 8)), edge))
    layer = over(layer, (metal, m))
    save(to_image(*layer), "title_logo.png")
    return to_image(*layer)


def halo(w=1300, h=820):
    """The light behind the logo: a warm bloom, a thin ring that is brightest
    at its crown and fades behind the letters, and a spear of light falling
    from a four-pointed star down the middle."""
    cx, cy = w / 2, h * 0.56
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dx, dy = (xx - cx), (yy - cy)
    r = np.sqrt(dx * dx + dy * dy)

    bloom = np.exp(-((dx / 420) ** 2 + (dy / 200) ** 2)) * 0.2
    radius = 255.0
    ring_line = np.exp(-(((r - radius) / 1.6) ** 2))
    ring_glow = np.exp(-(((r - radius) / 9.0) ** 2)) * 0.35
    up = np.clip(-dy / radius, -1, 1)
    crown = np.clip(0.25 + 0.75 * up, 0, 1) ** 1.4
    ring = (ring_line * 0.95 + ring_glow) * crown

    star_y = cy - radius - 10
    sx, sy = np.abs(xx - cx), (yy - star_y)
    spear = np.exp(-((sx / 1.4) ** 2)) * np.clip(1.0 - np.abs(sy) / np.where(sy < 0, 120.0, 330.0), 0, 1) ** 1.5
    spear_glow = np.exp(-((sx / 7.0) ** 2)) * np.clip(1.0 - np.abs(sy) / np.where(sy < 0, 160.0, 380.0), 0, 1) ** 2 * 0.5
    arms = np.exp(-((sy / 1.3) ** 2)) * np.clip(1.0 - sx / 70.0, 0, 1) ** 2
    core = np.exp(-((sx * sx + sy * sy) / (2 * 6.0**2)))
    star_glow = np.exp(-((sx * sx + sy * sy) / (2 * 26.0**2))) * 0.6
    light = np.clip(ring + spear + spear_glow + arms + core + star_glow, 0, 1)
    # Nothing reaches the image's edges, so no box shows around the glow.
    window = falloff(h, 0.55, 1.0)[:, None] * falloff(w, 0.55, 1.0)[None, :]
    bloom = bloom * window
    light = light * window

    layer = (flat((h, w), AMBER), bloom)
    color = PALE * light[..., None] + GOLD * (1 - light[..., None])
    layer = over(layer, (color, light))
    save(to_image(*layer), "title_halo.png")
    return to_image(*layer)


def falloff(n, solid, power=1.6):
    """A 0..1 profile across n samples: full over the middle `solid` share, then
    a smooth fall to nothing at both ends."""
    x = np.abs(np.linspace(-1.0, 1.0, n, dtype=np.float32))
    t = np.clip((x - solid) / max(1e-6, 1.0 - solid), 0, 1)
    return (0.5 + 0.5 * np.cos(np.pi * t)) ** power


def focus_bar(name, w, h=84):
    """The light behind the focused item: a warm band that fades at both ends
    and towards its top and bottom, a brighter core, and fine gold hairlines
    along both edges."""
    across = falloff(w, 0.25)[None, :]
    ys = np.linspace(-1.0, 1.0, h, dtype=np.float32)[:, None]
    band = np.exp(-((ys / 0.55) ** 2)) * 0.62 * across
    core = np.exp(-((ys / 0.22) ** 2)) * 0.30 * falloff(w, 0.1, 2.5)[None, :]
    yy = np.arange(h, dtype=np.float32)[:, None]
    top, bottom = 6.0, h - 7.0
    lines = (np.exp(-(((yy - top) / 0.9) ** 2)) + np.exp(-(((yy - bottom) / 0.9) ** 2)) * 0.8) * falloff(w, 0.3, 1.2)[None, :]
    line_glow = (np.exp(-(((yy - top) / 5.0) ** 2)) + np.exp(-(((yy - bottom) / 5.0) ** 2))) * 0.25 * across
    shape = (h, w)
    layer = (flat(shape, AMBER * 0.75), np.clip(band, 0, 1))
    layer = over(layer, (flat(shape, (238, 160, 70)), np.clip(core, 0, 1)))
    layer = over(layer, (flat(shape, (255, 214, 140)), np.clip(line_glow, 0, 1)))
    layer = over(layer, (flat(shape, PALE), np.clip(lines, 0, 1)))
    save(to_image(*layer), name)
    return to_image(*layer)


def diamond_mask(w, h, cx, cy, r, stroke=None, scale=4):
    """An antialiased diamond (filled, or a ring `stroke` wide) as a 0..1 mask."""
    im = Image.new("L", (w * scale, h * scale), 0)
    d = ImageDraw.Draw(im)

    def points(rr):
        return [(cx * scale, (cy - rr) * scale), ((cx + rr) * scale, cy * scale), (cx * scale, (cy + rr) * scale), ((cx - rr) * scale, cy * scale)]

    d.polygon(points(r), fill=255)
    if stroke is not None:
        d.polygon(points(r - stroke), fill=0)
    im = im.resize((w, h), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def emblem(w=200, h=84):
    """The diamond at the focus bar's left end: a gold ring, a dark inner
    diamond, a bright gem in it, blades of light either side and a glow."""
    cx, cy = w / 2, h / 2
    ring = diamond_mask(w, h, cx, cy, 30, 4)
    inner_ring = diamond_mask(w, h, cx, cy, 21, 2)
    fill = diamond_mask(w, h, cx, cy, 26)
    gem = diamond_mask(w, h, cx, cy, 11)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    sx, sy = np.abs(xx - cx), np.abs(yy - cy)
    blades = np.exp(-((sy / 1.0) ** 2)) * np.clip(1.0 - (sx - 30) / 70.0, 0, 1) ** 1.5 * (sx > 28)
    glow = np.exp(-((sx * sx + sy * sy) / (2 * 24.0**2))) * 0.55
    shape = (h, w)
    gem_color = ramp(np.clip((yy - (cy - 11)) / 22.0, 0, 1), [(0, (255, 248, 226)), (0.5, (250, 210, 140)), (1, (190, 120, 50))])
    layer = (flat(shape, AMBER), glow)
    layer = over(layer, (flat(shape, (30, 16, 8)), fill * 0.9))
    layer = over(layer, (flat(shape, (214, 150, 70)), inner_ring))
    layer = over(layer, (flat(shape, (246, 206, 140)), ring))
    layer = over(layer, (flat(shape, PALE), np.clip(blades, 0, 1)))
    layer = over(layer, (gem_color, gem))
    save(to_image(*layer), "focus_emblem.png")
    return to_image(*layer)


def rule(name, w, h, gem, solid, strength=0.75, dots=False):
    """A gold hairline that fades at both ends, with a small diamond (and
    optionally two dots) in the middle, and a faint glow."""
    cy = h / 2
    yy = np.arange(h, dtype=np.float32)[:, None]
    across = falloff(w, solid, 1.3)[None, :]
    line = np.exp(-(((yy - cy + 0.5) / 0.7) ** 2)) * across * strength
    glow = np.exp(-(((yy - cy + 0.5) / 3.0) ** 2)) * across * 0.18
    shape = (h, w)
    mid = w / 2
    layer = (flat(shape, AMBER), np.clip(glow, 0, 1))
    layer = over(layer, (flat(shape, GOLD), np.clip(line, 0, 1)))
    if gem:
        ring = diamond_mask(w, h, mid, cy, gem, max(1.5, gem * 0.3))
        core = diamond_mask(w, h, mid, cy, gem * 0.45)
        gap = diamond_mask(w, h, mid, cy, gem + 3)
        layer = (layer[0], layer[1] * (1 - gap))
        layer = over(layer, (flat(shape, (246, 206, 140)), ring))
        layer = over(layer, (flat(shape, PALE), core))
        if dots:
            for side in (-1, 1):
                dot = diamond_mask(w, h, mid + side * (gem + 9), cy, 2.2)
                layer = over(layer, (flat(shape, (246, 206, 140)), dot))
    save(to_image(*layer), name)
    return to_image(*layer)


def side_line(name, w=200, h=6):
    """A short hairline, bright at its right end and fading to the left; the
    screen mirrors it for the right-hand side."""
    yy = np.arange(h, dtype=np.float32)[:, None]
    along = np.linspace(0.0, 1.0, w, dtype=np.float32)[None, :] ** 1.6
    line = np.exp(-(((yy - h / 2 + 0.5) / 0.7) ** 2)) * along * 0.7
    save(to_image(flat((h, w), GOLD), line), name)


def mote(size=32):
    """One rising ember: a soft round glow with a bright centre, white so the screen can tint it."""
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2
    d2 = (xx - c) ** 2 + (yy - c) ** 2
    a = np.exp(-d2 / (2 * (size / 9) ** 2)) * 0.9 + np.exp(-d2 / (2 * (size / 4) ** 2)) * 0.3
    save(to_image(flat((size, size), (255, 255, 255)), np.clip(a, 0, 1)), "mote.png")


def key_cap(size=48, radius=8, stroke=2):
    """A key cap: a dark rounded box with a thin light edge, nine-sliced with a
    KEY_BORDER margin so it stretches to any key name."""
    s = 4
    im = Image.new("RGBA", (size * s, size * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, size * s - 1, size * s - 1], radius=radius * s, fill=(196, 172, 132, 210))
    d.rounded_rectangle(
        [stroke * s, stroke * s, (size - stroke) * s - 1, (size - stroke) * s - 1],
        radius=(radius - stroke) * s,
        fill=(22, 18, 16, 230),
    )
    im = im.resize((size, size), Image.LANCZOS)
    path = save(im, "key_cap.png")
    set_border(path, KEY_BORDER)


def dpad_icon(size=48):
    """A D-pad: a plus-shaped outline with a small arrow in each arm, in the key cap's light edge colour."""
    s = 4
    S = size * s
    im = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(im)
    arm, r = 0.30 * S, 0.06 * S
    c = S / 2
    d.rounded_rectangle([c - arm / 2, 2 * s, c + arm / 2, S - 2 * s], radius=r, fill=255)
    d.rounded_rectangle([2 * s, c - arm / 2, S - 2 * s, c + arm / 2], radius=r, fill=255)
    inner = Image.new("L", (S, S), 0)
    di = ImageDraw.Draw(inner)
    e = 2 * s
    di.rounded_rectangle([c - arm / 2 + e, 2 * s + e, c + arm / 2 - e, S - 2 * s - e], radius=r, fill=255)
    di.rounded_rectangle([2 * s + e, c - arm / 2 + e, S - 2 * s - e, c + arm / 2 - e], radius=r, fill=255)
    outline = np.asarray(im, dtype=np.float32) - np.asarray(inner, dtype=np.float32)
    arrows = Image.new("L", (S, S), 0)
    da = ImageDraw.Draw(arrows)
    t = 0.07 * S
    for ax, ay, vx, vy in ((0, -1, 1, 0), (0, 1, 1, 0), (-1, 0, 0, 1), (1, 0, 0, 1)):
        tip = (c + ax * 0.36 * S, c + ay * 0.36 * S)
        base = (c + ax * 0.26 * S, c + ay * 0.26 * S)
        da.polygon([tip, (base[0] + vx * t, base[1] + vy * t), (base[0] - vx * t, base[1] - vy * t)], fill=255)
    mask = np.clip(outline + np.asarray(arrows, dtype=np.float32), 0, 255)
    im = Image.fromarray(mask.astype(np.uint8), "L").resize((size, size), Image.LANCZOS)
    a = np.asarray(im, dtype=np.float32) / 255.0
    save(to_image(flat((size, size), (214, 192, 156)), a), "key_dpad.png")


def save(im, name):
    path = os.path.join(OUT, name)
    im.save(path)
    unity_meta.ensure_meta(path)
    return path


def set_border(path, border):
    """Writes a nine-slice border into a sprite's .meta (left, bottom, right, top)."""
    meta = path + ".meta"
    if not os.path.exists(meta):
        return
    with open(meta, encoding="utf-8", newline="") as f:
        text = f.read()
    want = f"spriteBorder: {{x: {border}, y: {border}, z: {border}, w: {border}}}"
    text = text.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}", want)
    with open(meta, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def preview(path):
    """A mock of the whole screen at 1920x1080 from the art just drawn, with the
    text in Cinzel, to judge the look without opening Unity."""
    W, H = 1920, 1080
    im = Image.new("RGBA", (W, H), BACKGROUND + (255,))

    def put(name, cx, cy, flip=False):
        part = Image.open(os.path.join(OUT, name)).convert("RGBA")
        if flip:
            part = part.transpose(Image.FLIP_LEFT_RIGHT)
        im.alpha_composite(part, (int(cx - part.width / 2), int(cy - part.height / 2)))

    put("title_halo.png", W / 2, 205)
    put("title_logo.png", W / 2, 215)
    d = ImageDraw.Draw(im)
    tag = ImageFont.truetype(REGULAR, 22)
    item = ImageFont.truetype(REGULAR, 34)

    def spaced(y, text, font, color, spacing):
        total = sum(d.textlength(c, font=font) + spacing for c in text) - spacing
        x = W / 2 - total / 2
        for c in text:
            d.text((x, y), c, font=font, fill=color, anchor="lm")
            x += d.textlength(c, font=font) + spacing
        return total

    spaced(330, "A MAN WITH HALF-FINISHED ANGEL POWERS", tag, (186, 170, 150), 7)
    half = spaced(358, "DESCENDS FROM HEAVEN", tag, (186, 170, 150), 7) / 2
    put("rule_side.png", W / 2 - half - 130, 358)
    put("rule_side.png", W / 2 + half + 130, 358, flip=True)
    put("rule_tagline.png", W / 2, 398)
    items = ["NEW GAME", "CONTINUE", "LOAD GAME", "SETTINGS", "CREDITS", "QUIT"]
    top, step = 470, 70
    for i, text in enumerate(items):
        y = top + i * step
        if i > 0:
            put("rule_item.png", W / 2, y - step / 2)
    put("focus_bar.png", W / 2, top)
    put("focus_emblem.png", W / 2 - 300, top)
    for i, text in enumerate(items):
        color = (255, 240, 214) if i == 0 else ((120, 108, 98) if i in (1, 2) else (196, 180, 160))
        spaced(top + i * step, text, item, color, 9)
    put("key_dpad.png", 700, 1005)
    put("key_cap.png", 900, 1005)
    put("key_cap.png", 1080, 1005)
    im.convert("RGB").save(path)


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=OUT, help="the folder to write into (default: Assets/Art/UI/Title)")
    parser.add_argument("--preview", help="also write a 1920x1080 mock of the screen to this path")
    args = parser.parse_args()
    OUT = os.path.abspath(args.out)
    os.makedirs(OUT, exist_ok=True)
    draw_all()
    if args.preview:
        preview(args.preview)


def draw_all():
    logo()
    halo()
    focus_bar("focus_bar.png", 900)
    focus_bar("focus_bar_wide.png", 1400)
    emblem()
    rule("rule_item.png", 460, 16, 4.5, 0.25, 0.6)
    rule("rule_tagline.png", 820, 32, 9.0, 0.35, 0.8, dots=True)
    side_line("rule_side.png")
    mote()
    key_cap()
    dpad_icon()
    print("drew", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))


if __name__ == "__main__":
    main()
