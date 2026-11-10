"""Draws the title screen's art into Assets/Art/UI/Title: the gilded DESCENDER
logo, the halo and star flare behind it, the gold focus bar (and a wide one for
the save slots) with its diamond emblem, the hairline rules between the menu
items and under the tagline, the short lines either side of the tagline and the
key hints, the soft mote the rising embers are drawn with, the key cap and the
D-pad icon.

    python3 tools/title_textures.py                 # Python 3 with Pillow and numpy
    py tools/title_textures.py --out scratch/title  # Windows; write somewhere else
    py tools/title_textures.py --preview mock.png   # also a 1920x1080 mock of the screen

The screen is laid out on a 1920x1080 stage; every piece is drawn at ART_SCALE
times its size there (3840x2160 detail), and the screen shows it at 1/ART_SCALE
(TitleKit.ArtScale), so it stays sharp on 1440p and 4K screens; mipmaps keep it
smooth on smaller ones. All sizes below are in stage pixels and multiplied by S
where they are drawn. The look is a modern dark-fantasy title: antialiased,
soft glows over a flat dark ground (TitleScreen.Background), no backdrop image.
The logo is set in Cinzel Bold (Assets/Art/Fonts, OFL). A new file under
Assets/ gets its .meta (unity_meta: bilinear, mipmapped, uncompressed); an
existing .meta is never touched, except that the key cap's nine-slice border is
written into it.
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

# Matches TitleKit.ArtScale: the art is drawn this many times its stage size.
ART_SCALE = 2
S = ART_SCALE

# Matches TitleScreen.Background, for the preview only.
BACKGROUND = (14, 12, 12)
GOLD = np.array([236, 190, 112], dtype=np.float32)
PALE = np.array([255, 238, 200], dtype=np.float32)
AMBER = np.array([206, 122, 40], dtype=np.float32)
# The key cap's nine-slice margin, in stage pixels; the .meta holds it times S.
KEY_BORDER = 12


def to_image(rgb, alpha):
    """An RGBA image from float arrays: rgb 0..255 (h, w, 3), alpha 0..1 (h, w)."""
    a = np.clip(alpha, 0.0, 1.0)
    out = np.zeros(a.shape + (4,), dtype=np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = (a * 255.0 + 0.5).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def over(base, top):
    """Composites one (rgb, alpha) float layer over another."""
    rgb_b, a_b = base
    rgb_t, a_t = top
    a = a_t + a_b * (1.0 - a_t)
    safe = np.where(a > 1e-6, a, 1.0)
    rgb = (rgb_t * a_t[..., None] + rgb_b * a_b[..., None] * (1.0 - a_t[..., None])) / safe[..., None]
    return rgb, a


def blur(mask, radius):
    """A gaussian blur of a 0..1 float mask, radius in image pixels."""
    if radius <= 0:
        return mask
    # Separable: one 1D kernel along the rows, then down the columns, edges padded with zero.
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


def falloff(n, solid, power=1.6):
    """A 0..1 profile across n samples: full over the middle `solid` share, then
    a smooth fall to nothing at both ends."""
    x = np.abs(np.linspace(-1.0, 1.0, n, dtype=np.float32))
    t = np.clip((x - solid) / max(1e-6, 1.0 - solid), 0, 1)
    return (0.5 + 0.5 * np.cos(np.pi * t)) ** power


def logo():
    """DESCENDER in Cinzel Bold, the first and last letters taller, cast in
    gold: a metal ramp from a pale crown through amber to a bright rim and a
    deep foot, a bevel lit from above, fine grain, a dark edge, a soft drop
    shadow and a warm outer glow."""
    over_sample = 2
    big = ImageFont.truetype(BOLD, 157 * S * over_sample)
    small = ImageFont.truetype(BOLD, 120 * S * over_sample)
    parts = [("D", big), ("ESCENDE", small), ("R", big)]
    tracking = 8 * S * over_sample
    mask = Image.new("L", (1700 * S * over_sample, 300 * S * over_sample), 0)
    d = ImageDraw.Draw(mask)
    x, baseline = 30 * S * over_sample, 230 * S * over_sample
    for text, font in parts:
        for c in text:
            d.text((x, baseline), c, font=font, fill=255, anchor="ls")
            x += d.textlength(c, font=font) + tracking
    mask = mask.crop(mask.getbbox())
    mask = mask.resize((mask.width // over_sample, mask.height // over_sample), Image.LANCZOS)
    pad = 56 * S
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
    soft = blur(m, 2.4 * S)
    slope = np.zeros_like(soft)
    slope[S:-S, :] = soft[: -2 * S, :] - soft[2 * S :, :]
    light = np.clip(-slope * 9.0, 0, 1)
    shade = np.clip(slope * 9.0, 0, 1)
    metal = metal + light[..., None] * 70.0 - shade[..., None] * 60.0
    rng = np.random.default_rng(11)
    grain = rng.normal(0.0, 1.0, (h, w)).astype(np.float32)
    metal = metal * (1.0 + 0.05 * blur(grain, 0.6 * S)[..., None])

    edge = np.clip(blur(m, 1.3 * S) * 3.0, 0, 1)
    shadow = np.roll(blur(m, 7.0 * S), 6 * S, axis=0) * 0.85
    glow = blur(m, 21.0 * S) * 0.55

    layer = (flat((h, w), (0, 0, 0)), np.zeros((h, w), dtype=np.float32))
    layer = over(layer, (flat((h, w), AMBER), glow))
    layer = over(layer, (flat((h, w), (4, 2, 1)), shadow))
    layer = over(layer, (flat((h, w), (36, 18, 8)), edge))
    layer = over(layer, (metal, m))
    save(to_image(*layer), "title_logo.png")


# Where the halo's ring centre sits in its image, as a share of the height;
# TitleScreen places the image from it.
HALO_RING = 0.62


def halo(w=1100, h=640):
    """The light behind the logo: a warm bloom, a thin ring that is brightest
    at its crown and fades behind the letters, and a spear of light falling
    from a four-pointed star down the middle. Nothing reaches the image's edge."""
    W, H = w * S, h * S
    cx, cy = W / 2, H * HALO_RING
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    dx, dy = (xx - cx) / S, (yy - cy) / S
    r = np.sqrt(dx * dx + dy * dy)

    bloom = np.exp(-((dx / 340) ** 2 + (dy / 160) ** 2)) * 0.2
    radius = 205.0
    ring_line = np.exp(-(((r - radius) / 1.0) ** 2))
    ring_glow = np.exp(-(((r - radius) / 7.0) ** 2)) * 0.35
    up = np.clip(-dy / radius, -1, 1)
    crown = np.clip(0.25 + 0.75 * up, 0, 1) ** 1.4
    ring = (ring_line * 0.95 + ring_glow) * crown

    star = -radius - 8.0
    sx, sy = np.abs(dx), dy - star
    above = sy < 0
    spear = np.exp(-((sx / 0.9) ** 2)) * np.clip(1.0 - np.abs(sy) / np.where(above, 34.0, 270.0), 0, 1) ** 1.5
    spear_glow = (
        np.exp(-((sx / 5.0) ** 2)) * np.clip(1.0 - np.abs(sy) / np.where(above, 40.0, 310.0), 0, 1) ** 2 * 0.5
    )
    arms = np.exp(-((sy / 0.9) ** 2)) * np.clip(1.0 - sx / 56.0, 0, 1) ** 2
    core = np.exp(-((sx * sx + sy * sy) / (2 * 4.5**2)))
    star_glow = np.exp(-((sx * sx + sy * sy) / (2 * 21.0**2))) * 0.6
    light = np.clip(ring + spear + spear_glow + arms + core + star_glow, 0, 1)
    window = falloff(H, 0.6, 1.0)[:, None] * falloff(W, 0.6, 1.0)[None, :]
    bloom = bloom * window
    light = light * window

    layer = (flat((H, W), AMBER), bloom)
    color = PALE * light[..., None] + GOLD * (1 - light[..., None])
    layer = over(layer, (color, light))
    save(to_image(*layer), "title_halo.png")


def focus_bar(name, w, h=72):
    """The light behind the focused item: a warm band that fades at both ends
    and towards its top and bottom, a brighter core, and fine gold hairlines
    along both edges."""
    W, H = w * S, h * S
    across = falloff(W, 0.25)[None, :]
    ys = np.linspace(-1.0, 1.0, H, dtype=np.float32)[:, None]
    band = np.exp(-((ys / 0.55) ** 2)) * 0.62 * across
    core = np.exp(-((ys / 0.22) ** 2)) * 0.30 * falloff(W, 0.1, 2.5)[None, :]
    yy = np.arange(H, dtype=np.float32)[:, None] / S
    top, bottom = 5.0, h - 6.0
    lines = (np.exp(-(((yy - top) / 0.6) ** 2)) + np.exp(-(((yy - bottom) / 0.6) ** 2)) * 0.8) * falloff(
        W, 0.3, 1.2
    )[None, :]
    line_glow = (np.exp(-(((yy - top) / 4.0) ** 2)) + np.exp(-(((yy - bottom) / 4.0) ** 2))) * 0.25 * across
    shape = (H, W)
    layer = (flat(shape, AMBER * 0.75), np.clip(band, 0, 1))
    layer = over(layer, (flat(shape, (238, 160, 70)), np.clip(core, 0, 1)))
    layer = over(layer, (flat(shape, (255, 214, 140)), np.clip(line_glow, 0, 1)))
    layer = over(layer, (flat(shape, PALE), np.clip(lines, 0, 1)))
    save(to_image(*layer), name)


def diamond_mask(w, h, cx, cy, r, stroke=None, over_sample=4):
    """An antialiased diamond (filled, or a ring `stroke` wide) as a 0..1 mask,
    all in image pixels."""
    k = over_sample
    im = Image.new("L", (int(w * k), int(h * k)), 0)
    d = ImageDraw.Draw(im)

    def points(rr):
        return [(cx * k, (cy - rr) * k), ((cx + rr) * k, cy * k), (cx * k, (cy + rr) * k), ((cx - rr) * k, cy * k)]

    d.polygon(points(r), fill=255)
    if stroke is not None:
        d.polygon(points(r - stroke), fill=0)
    im = im.resize((int(w), int(h)), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def emblem(w=170, h=72):
    """The diamond at the focus bar's left end: a gold ring, a dark inner
    diamond, a bright gem in it, blades of light either side and a glow."""
    W, H = w * S, h * S
    cx, cy = W / 2, H / 2
    ring = diamond_mask(W, H, cx, cy, 25 * S, 3.4 * S)
    inner_ring = diamond_mask(W, H, cx, cy, 18 * S, 1.7 * S)
    fill = diamond_mask(W, H, cx, cy, 22 * S)
    gem = diamond_mask(W, H, cx, cy, 9.5 * S)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    sx, sy = np.abs(xx - cx) / S, np.abs(yy - cy) / S
    blades = np.exp(-((sy / 0.7) ** 2)) * np.clip(1.0 - (sx - 25) / 60.0, 0, 1) ** 1.5 * (sx > 24)
    glow = np.exp(-((sx * sx + sy * sy) / (2 * 20.0**2))) * 0.55
    shape = (H, W)
    gem_color = ramp(
        np.clip((yy / S - (h / 2 - 9.5)) / 19.0, 0, 1), [(0, (255, 248, 226)), (0.5, (250, 210, 140)), (1, (190, 120, 50))]
    )
    layer = (flat(shape, AMBER), glow)
    layer = over(layer, (flat(shape, (30, 16, 8)), fill * 0.9))
    layer = over(layer, (flat(shape, (214, 150, 70)), inner_ring))
    layer = over(layer, (flat(shape, (246, 206, 140)), ring))
    layer = over(layer, (flat(shape, PALE), np.clip(blades, 0, 1)))
    layer = over(layer, (gem_color, gem))
    save(to_image(*layer), "focus_emblem.png")


def rule(name, w, h, gem, solid, strength=0.75, dots=False):
    """A gold hairline that fades at both ends, with a small diamond (and
    optionally two dots) in the middle, and a faint glow."""
    W, H = w * S, h * S
    cy = H / 2
    yy = np.arange(H, dtype=np.float32)[:, None]
    across = falloff(W, solid, 1.3)[None, :]
    line = np.exp(-(((yy - cy + 0.5) / (0.6 * S)) ** 2)) * across * strength
    glow = np.exp(-(((yy - cy + 0.5) / (2.6 * S)) ** 2)) * across * 0.18
    shape = (H, W)
    mid = W / 2
    layer = (flat(shape, AMBER), np.clip(glow, 0, 1))
    layer = over(layer, (flat(shape, GOLD), np.clip(line, 0, 1)))
    if gem:
        g = gem * S
        ring = diamond_mask(W, H, mid, cy, g, max(1.3 * S, g * 0.3))
        core = diamond_mask(W, H, mid, cy, g * 0.45)
        gap = diamond_mask(W, H, mid, cy, g + 2.5 * S)
        layer = (layer[0], layer[1] * (1 - gap))
        layer = over(layer, (flat(shape, (246, 206, 140)), ring))
        layer = over(layer, (flat(shape, PALE), core))
        if dots:
            for side in (-1, 1):
                dot = diamond_mask(W, H, mid + side * (g + 7.5 * S), cy, 1.9 * S)
                layer = over(layer, (flat(shape, (246, 206, 140)), dot))
    save(to_image(*layer), name)


def side_line(name, w=170, h=6):
    """A short hairline, bright at its right end and fading to the left; the
    screen mirrors it for the right-hand side."""
    W, H = w * S, h * S
    yy = np.arange(H, dtype=np.float32)[:, None]
    along = np.linspace(0.0, 1.0, W, dtype=np.float32)[None, :] ** 1.6
    line = np.exp(-(((yy - H / 2 + 0.5) / (0.6 * S)) ** 2)) * along * 0.7
    save(to_image(flat((H, W), GOLD), line), name)


def mote(size=32):
    """One rising ember: a soft round glow with a bright centre, white so the screen can tint it."""
    n = size * S
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    c = (n - 1) / 2
    d2 = (xx - c) ** 2 + (yy - c) ** 2
    a = np.exp(-d2 / (2 * (n / 9) ** 2)) * 0.9 + np.exp(-d2 / (2 * (n / 4) ** 2)) * 0.3
    save(to_image(flat((n, n), (255, 255, 255)), np.clip(a, 0, 1)), "mote.png")


def key_cap(size=40, radius=7, stroke=1.5):
    """A key cap: a dark rounded box with a thin light edge, nine-sliced with a
    KEY_BORDER margin so it stretches to any key name."""
    k = 4
    n = size * S
    im = Image.new("RGBA", (n * k, n * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, n * k - 1, n * k - 1], radius=radius * S * k, fill=(196, 172, 132, 210))
    e = stroke * S * k
    d.rounded_rectangle([e, e, n * k - 1 - e, n * k - 1 - e], radius=(radius - stroke) * S * k, fill=(22, 18, 16, 230))
    im = im.resize((n, n), Image.LANCZOS)
    path = save(im, "key_cap.png")
    set_border(path, KEY_BORDER * S)


def dpad_icon(size=38):
    """A D-pad: a plus-shaped outline with a small arrow in each arm, in the key cap's light edge colour."""
    k = 4
    n = size * S
    N = n * k
    c = N / 2
    arm, r, m, e = 0.30 * N, 0.06 * N, 0.04 * N, 0.045 * N
    outer = Image.new("L", (N, N), 0)
    do = ImageDraw.Draw(outer)
    do.rounded_rectangle([c - arm / 2, m, c + arm / 2, N - m], radius=r, fill=255)
    do.rounded_rectangle([m, c - arm / 2, N - m, c + arm / 2], radius=r, fill=255)
    inner = Image.new("L", (N, N), 0)
    di = ImageDraw.Draw(inner)
    di.rounded_rectangle([c - arm / 2 + e, m + e, c + arm / 2 - e, N - m - e], radius=r, fill=255)
    di.rounded_rectangle([m + e, c - arm / 2 + e, N - m - e, c + arm / 2 - e], radius=r, fill=255)
    outline = np.asarray(outer, dtype=np.float32) - np.asarray(inner, dtype=np.float32)
    arrows = Image.new("L", (N, N), 0)
    da = ImageDraw.Draw(arrows)
    t = 0.07 * N
    for ax, ay, vx, vy in ((0, -1, 1, 0), (0, 1, 1, 0), (-1, 0, 0, 1), (1, 0, 0, 1)):
        tip = (c + ax * 0.36 * N, c + ay * 0.36 * N)
        base = (c + ax * 0.26 * N, c + ay * 0.26 * N)
        da.polygon([tip, (base[0] + vx * t, base[1] + vy * t), (base[0] - vx * t, base[1] - vy * t)], fill=255)
    mask = np.clip(outline + np.asarray(arrows, dtype=np.float32), 0, 255)
    im = Image.fromarray(mask.astype(np.uint8), "L").resize((n, n), Image.LANCZOS)
    a = np.asarray(im, dtype=np.float32) / 255.0
    save(to_image(flat((n, n), (214, 192, 156)), a), "key_dpad.png")


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
    """A mock of the screen at 1920x1080 from the art just drawn (shown at 1/S,
    as the screen shows it), to judge the frame without opening Unity."""
    W, H = 1920, 1080
    im = Image.new("RGBA", (W, H), BACKGROUND + (255,))

    def put(name, cx, cy):
        part = Image.open(os.path.join(OUT, name)).convert("RGBA")
        part = part.resize((part.width // S, part.height // S), Image.LANCZOS)
        im.alpha_composite(part, (int(cx - part.width / 2), int(cy - part.height / 2)))

    # Matches TitleScreen: the ring's centre 30 px under the logo's middle.
    logo_y = 245
    put("title_halo.png", W / 2, logo_y + 30 - (HALO_RING - 0.5) * 640)
    put("title_logo.png", W / 2, logo_y)
    im.convert("RGB").save(path)


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=OUT, help="the folder to write into (default: Assets/Art/UI/Title)")
    parser.add_argument("--preview", help="also write a 1920x1080 mock of the screen's header to this path")
    args = parser.parse_args()
    OUT = os.path.abspath(args.out)
    os.makedirs(OUT, exist_ok=True)
    draw_all()
    if args.preview:
        preview(args.preview)


def draw_all():
    logo()
    halo()
    focus_bar("focus_bar.png", 820)
    focus_bar("focus_bar_wide.png", 1300)
    emblem()
    rule("rule_item.png", 400, 14, 4.0, 0.25, 0.6)
    rule("rule_tagline.png", 700, 28, 8.0, 0.35, 0.8, dots=True)
    side_line("rule_side.png")
    mote()
    key_cap()
    dpad_icon()
    print("drew", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))


if __name__ == "__main__":
    main()
