"""Draws the settings screen's art into Assets/Art/UI/Settings, in the title
screen's gilded style (tools/title_textures.py, whose helpers it uses): the
sun-gear emblem by the heading, the long jewelled rule under it, the plain
hairlines, the faint rule between rows, the vertical rules between the columns,
the glowing underline of the chosen tab, the bar behind the chosen section and
the one behind the focused row, the gold toggle switch on and off, the slider's
track, fill and handle, the chevrons either side of a value, and a white line
icon for each section (tinted by the screen).

    python3 tools/settings_textures.py                    # Python 3 with Pillow and numpy
    py tools/settings_textures.py --out scratch/settings  # Windows; write somewhere else

Like the title's, the art is drawn at ART_SCALE times its size on the 1920x1080
stage and shown at 1/ART_SCALE, so it stays sharp up to 4K; sizes below are in
stage pixels. A new file under Assets/ gets its .meta (unity_meta: bilinear,
mipmapped, uncompressed); an existing .meta is never touched.
"""

import argparse
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "unity"))
import title_textures as tt  # noqa: E402
import unity_meta  # noqa: E402

OUT = os.path.join(unity_meta.ASSETS, "Art", "UI", "Settings")
S = tt.ART_SCALE
AMBER, GOLD, PALE = tt.AMBER, tt.GOLD, tt.PALE
flat, over, to_image, falloff, blur = tt.flat, tt.over, tt.to_image, tt.falloff, tt.blur


def save(im, name):
    path = os.path.join(OUT, name)
    im.save(path)
    unity_meta.ensure_meta(path)
    return path


def hairline(name, w, h, strength, solid, gem=0.0, dots=False):
    """A horizontal gold hairline fading at both ends, optionally jewelled."""
    old = tt.OUT
    tt.OUT = OUT
    try:
        tt.rule(name, w, h, gem, solid, strength, dots)
    finally:
        tt.OUT = old


def vertical_rule(name, w=6, h=620):
    """A vertical hairline that fades at its top and bottom."""
    W, H = w * S, h * S
    xx = np.arange(W, dtype=np.float32)[None, :]
    along = falloff(H, 0.55, 1.3)[:, None]
    line = np.exp(-(((xx - W / 2 + 0.5) / (0.6 * S)) ** 2)) * along * 0.45
    save(to_image(flat((H, W), GOLD), line), name)


def tab_glow(w=200, h=30):
    """The chosen tab's underline: a bright hairline that fades at its ends, a
    soft glow round it, and a small diamond hanging from its middle."""
    W, H = w * S, h * S
    yy = np.arange(H, dtype=np.float32)[:, None] / S
    across = falloff(W, 0.15, 1.4)[None, :]
    y0 = 9.0
    line = np.exp(-(((yy - y0) / 0.6) ** 2)) * across
    glow = np.exp(-(((yy - y0) / 5.0) ** 2)) * across * 0.45
    shape = (H, W)
    layer = (flat(shape, AMBER), np.clip(glow, 0, 1))
    layer = over(layer, (flat(shape, PALE), np.clip(line, 0, 1)))
    gem = tt.diamond_mask(W, H, W / 2, (y0 + 0.5) * S, 3.6 * S)
    layer = over(layer, (flat(shape, (250, 216, 150)), gem))
    save(to_image(*layer), "tab_glow.png")


def band(name, w, h, left_fade, right_fade, peak, gem_right=False):
    """A bar of warm light behind a chosen line: strongest at `peak` (0..1
    across), fading over `left_fade` and `right_fade` shares to each end, with
    gold hairlines along its top and bottom; optionally a diamond at its right end."""
    W, H = w * S, h * S
    x = np.linspace(0.0, 1.0, W, dtype=np.float32)
    rise = np.clip(x / max(1e-6, left_fade), 0, 1)
    fall = np.clip((1.0 - x) / max(1e-6, right_fade), 0, 1)
    across = (np.minimum(rise, fall) ** 1.2)[None, :]
    hot = np.exp(-(((x - peak) / 0.35) ** 2))[None, :]
    ys = np.linspace(-1.0, 1.0, H, dtype=np.float32)[:, None]
    body = (0.30 + 0.32 * hot) * across * (0.75 + 0.25 * np.exp(-((ys / 0.5) ** 2)))
    yy = np.arange(H, dtype=np.float32)[:, None] / S
    top, bottom = 1.5, h - 2.0
    lines = (np.exp(-(((yy - top) / 0.6) ** 2)) * 0.9 + np.exp(-(((yy - bottom) / 0.6) ** 2)) * 0.75) * across
    line_glow = (np.exp(-(((yy - top) / 3.5) ** 2)) + np.exp(-(((yy - bottom) / 3.5) ** 2))) * 0.2 * across
    shape = (H, W)
    layer = (flat(shape, AMBER * 0.7), np.clip(body, 0, 1))
    layer = over(layer, (flat(shape, (255, 210, 140)), np.clip(line_glow, 0, 1)))
    layer = over(layer, (flat(shape, (248, 222, 170)), np.clip(lines, 0, 1)))
    if gem_right:
        cx = W - 9 * S
        ring = tt.diamond_mask(W, H, cx, H / 2, 7 * S, 1.6 * S)
        core = tt.diamond_mask(W, H, cx, H / 2, 3 * S)
        layer = over(layer, (flat(shape, (246, 206, 140)), ring))
        layer = over(layer, (flat(shape, PALE), core))
    save(to_image(*layer), name)


def rounded(w, h, r, fill, k=4):
    """An antialiased rounded box mask (w, h, r in image pixels) of value `fill`, 0..1."""
    im = Image.new("L", (int(w * k), int(h * k)), 0)
    ImageDraw.Draw(im).rounded_rectangle([0, 0, w * k - 1, h * k - 1], radius=r * k, fill=int(255 * fill))
    return np.asarray(im.resize((int(w), int(h)), Image.LANCZOS), dtype=np.float32) / 255.0


def disc(w, h, cx, cy, r, k=4):
    im = Image.new("L", (int(w * k), int(h * k)), 0)
    ImageDraw.Draw(im).ellipse([(cx - r) * k, (cy - r) * k, (cx + r) * k, (cy + r) * k], fill=255)
    return np.asarray(im.resize((int(w), int(h)), Image.LANCZOS), dtype=np.float32) / 255.0


def toggle(name, on, w=56, h=28):
    """The switch: a rounded track (warm gold when on, dark when off) with a thin
    edge, and a knob at the right (on) or left (off) with a soft shadow."""
    pad = 6
    W, H = (w + pad * 2) * S, (h + pad * 2) * S
    p = pad * S
    tw, th = w * S, h * S
    track = np.zeros((H, W), dtype=np.float32)
    track[p : p + th, p : p + tw] = rounded(tw, th, th / 2, 1.0)
    inner = np.zeros((H, W), dtype=np.float32)
    e = 1.5 * S
    inner[int(p + e) : int(p + e) + int(th - 2 * e), int(p + e) : int(p + e) + int(tw - 2 * e)] = rounded(
        int(tw - 2 * e), int(th - 2 * e), (th - 2 * e) / 2, 1.0
    )
    shape = (H, W)
    yy = np.arange(H, dtype=np.float32)[:, None]
    t = np.clip((yy - p) / th, 0, 1)
    if on:
        fill = tt.ramp(np.repeat(t, W, axis=1), [(0, (238, 186, 104)), (0.5, (200, 132, 52)), (1, (150, 88, 32))])
        edge_color = (255, 226, 166)
    else:
        fill = tt.ramp(np.repeat(t, W, axis=1), [(0, (34, 29, 26)), (1, (22, 19, 17))])
        edge_color = (128, 112, 92)
    kx = p + (tw - th / 2) if on else p + th / 2
    ky = p + th / 2
    kr = th / 2 - 3.5 * S
    knob = disc(W, H, kx, ky, kr)
    shadow = np.roll(blur(disc(W, H, kx, ky, kr), 2.5 * S), int(1.5 * S), axis=0) * 0.6
    knob_color = tt.ramp(np.repeat(np.clip((yy - (ky - kr)) / (2 * kr), 0, 1), W, axis=1), [(0, (255, 248, 230)), (1, (226, 206, 170))])
    if not on:
        knob_color = knob_color * 0.62
    glow = blur(track, 5 * S) * (0.35 if on else 0.0)
    layer = (flat(shape, AMBER), glow)
    layer = over(layer, (flat(shape, edge_color), track * (0.9 if on else 0.7)))
    layer = over(layer, (fill, inner))
    layer = over(layer, (flat(shape, (0, 0, 0)), shadow))
    layer = over(layer, (knob_color, knob))
    save(to_image(*layer), name)


def slider(w=240, h=6):
    """The slider's track (dark with a faint edge), its fill (gold, shown in part
    by the screen) and its handle (a pale gold tab with a glow)."""
    W, H = w * S, h * S
    pad = 2 * S
    shape = (H + pad * 2, W + pad * 2)
    box = np.zeros(shape, dtype=np.float32)
    box[pad : pad + H, pad : pad + W] = rounded(W, H, H / 2, 1.0)
    inner = np.zeros(shape, dtype=np.float32)
    inner[pad + S : pad + H - S, pad + S : pad + W - S] = rounded(W - 2 * S, H - 2 * S, (H - 2 * S) / 2, 1.0)
    layer = (flat(shape, (110, 96, 80)), box * 0.8)
    layer = over(layer, (flat(shape, (24, 20, 18)), inner))
    save(to_image(*layer), "slider_track.png")
    yy = np.arange(shape[0], dtype=np.float32)[:, None]
    t = np.clip((yy - pad) / H, 0, 1)
    gold = tt.ramp(np.repeat(t, shape[1], axis=1), [(0, (255, 226, 160)), (0.5, (226, 166, 84)), (1, (168, 104, 40))])
    save(to_image(gold, box), "slider_fill.png")

    hw, hh, hp = 10 * S, 26 * S, 8 * S
    shape = (hh + hp * 2, hw + hp * 2)
    tab = np.zeros(shape, dtype=np.float32)
    tab[hp : hp + hh, hp : hp + hw] = rounded(hw, hh, 2.5 * S, 1.0)
    yy = np.arange(shape[0], dtype=np.float32)[:, None]
    color = tt.ramp(np.repeat(np.clip((yy - hp) / hh, 0, 1), shape[1], axis=1), [(0, (255, 244, 220)), (1, (214, 178, 120))])
    layer = (flat(shape, AMBER), blur(tab, 4 * S) * 0.6)
    layer = over(layer, (color, tab))
    save(to_image(*layer), "slider_handle.png")


def stroke_icon(name, size, draw, width=2.2, filled=None):
    """A white line icon `size` square: `draw(d, n, lw)` strokes on a canvas
    n pixels across with lines lw wide (both oversampled), then it is shrunk
    smooth. `filled` draws solid shapes the same way."""
    k = 4
    n = size * S * k
    lw = max(1, int(round(width * S * k)))
    im = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(im)
    draw(d, n, lw)
    if filled is not None:
        filled(d, n)
    im = im.resize((size * S, size * S), Image.LANCZOS)
    a = np.asarray(im, dtype=np.float32) / 255.0
    save(to_image(flat(a.shape, (255, 255, 255)), a), name)


def arc(d, n, box, start, end, lw):
    d.arc([box[0] * n, box[1] * n, box[2] * n, box[3] * n], start, end, fill=255, width=lw)


def line(d, n, points, lw):
    d.line([(x * n, y * n) for x, y in points], fill=255, width=lw, joint="curve")
    for x, y in (points[0], points[-1]):
        r = lw / 2
        d.ellipse([x * n - r, y * n - r, x * n + r, y * n + r], fill=255)


def gear(d, n, lw, teeth=8, outer=0.40, inner=0.30, hole=0.12, cut=True):
    c = n / 2
    pts = []
    for i in range(teeth * 4):
        a = math.tau * i / (teeth * 4)
        r = outer if (i % 4) in (1, 2) else inner
        pts.append((c + math.cos(a) * r * n, c + math.sin(a) * r * n))
    d.polygon(pts, fill=255)
    if cut:
        d.ellipse([c - (inner - 0.1) * n, c - (inner - 0.1) * n, c + (inner - 0.1) * n, c + (inner - 0.1) * n], fill=0)
        d.ellipse([c - hole * n, c - hole * n, c + hole * n, c + hole * n], fill=255)
        d.ellipse([c - (hole - 0.05) * n, c - (hole - 0.05) * n, c + (hole - 0.05) * n, c + (hole - 0.05) * n], fill=0)


def icons():
    """One white line icon per section, 44 px across."""
    size = 44

    def gameplay(d, n, lw):
        gear(d, n, lw)

    def monitor(d, n, lw):
        d.rounded_rectangle([0.1 * n, 0.2 * n, 0.9 * n, 0.7 * n], radius=0.05 * n, outline=255, width=lw)
        line(d, n, [(0.5, 0.71), (0.5, 0.82)], lw)
        line(d, n, [(0.32, 0.84), (0.68, 0.84)], lw)

    def interface(d, n, lw):
        for i, (y, k) in enumerate(((0.28, 0.32), (0.5, 0.66), (0.72, 0.45))):
            line(d, n, [(0.12, y), (0.88, y)], lw)
            r = 0.075 * n
            d.ellipse([k * n - r, y * n - r, k * n + r, y * n + r], fill=0, outline=255, width=lw)

    def movement(d, n, lw):
        line(d, n, [(0.5, 0.12), (0.5, 0.88)], lw)
        line(d, n, [(0.12, 0.5), (0.88, 0.5)], lw)
        for (x, y), (ax, ay) in (((0.5, 0.12), (0, 1)), ((0.5, 0.88), (0, -1)), ((0.12, 0.5), (1, 0)), ((0.88, 0.5), (-1, 0))):
            px, py = -ay, ax
            line(d, n, [(x + ax * 0.12 + px * 0.1, y + ay * 0.12 + py * 0.1), (x, y), (x + ax * 0.12 - px * 0.1, y + ay * 0.12 - py * 0.1)], lw)

    def actions(d, n, lw):
        line(d, n, [(0.22, 0.78), (0.8, 0.2)], lw)
        line(d, n, [(0.8, 0.2), (0.86, 0.14)], lw)
        line(d, n, [(0.2, 0.56), (0.44, 0.8)], lw)
        line(d, n, [(0.22, 0.78), (0.12, 0.88)], lw)

    def screen(d, n, lw):
        d.rounded_rectangle([0.12 * n, 0.24 * n, 0.88 * n, 0.76 * n], radius=0.04 * n, outline=255, width=lw)
        for (x, y), (dx, dy) in (((0.2, 0.32), (1, 1)), ((0.8, 0.32), (-1, 1)), ((0.2, 0.68), (1, -1)), ((0.8, 0.68), (-1, -1))):
            line(d, n, [(x, y + dy * 0.1), (x, y), (x + dx * 0.1, y)], lw)

    def effects(d, n, lw):
        def star(cx, cy, r, d=d):
            pts = []
            for i in range(8):
                a = math.tau * i / 8 - math.pi / 2
                rr = r if i % 2 == 0 else r * 0.28
                pts.append((cx * n + math.cos(a) * rr * n, cy * n + math.sin(a) * rr * n))
            d.polygon(pts, fill=255)

        star(0.42, 0.55, 0.34)
        star(0.78, 0.24, 0.14)

    def volume(d, n, lw):
        d.polygon([(0.14 * n, 0.4 * n), (0.3 * n, 0.4 * n), (0.5 * n, 0.22 * n), (0.5 * n, 0.78 * n), (0.3 * n, 0.6 * n), (0.14 * n, 0.6 * n)], fill=255)
        arc(d, n, (0.42, 0.32, 0.72, 0.68), -55, 55, lw)
        arc(d, n, (0.4, 0.18, 0.9, 0.82), -55, 55, lw)

    def voices(d, n, lw):
        d.rounded_rectangle([0.1 * n, 0.18 * n, 0.9 * n, 0.68 * n], radius=0.12 * n, outline=255, width=lw)
        line(d, n, [(0.3, 0.67), (0.24, 0.84), (0.46, 0.67)], lw)
        for x in (0.32, 0.5, 0.68):
            r = 0.045 * n
            d.ellipse([x * n - r, 0.43 * n - r, x * n + r, 0.43 * n + r], fill=255)

    for name, draw in (
        ("icon_gameplay", gameplay),
        ("icon_interface", interface),
        ("icon_movement", movement),
        ("icon_actions", actions),
        ("icon_screen", screen),
        ("icon_effects", effects),
        ("icon_volume", volume),
        ("icon_voices", voices),
        ("icon_monitor", monitor),
    ):
        stroke_icon(name + ".png", size, draw)


def emblem(size=76):
    """The sun-gear by the SETTINGS heading: a toothed gold ring with an inner
    ring and a gem, rays between the teeth, and a warm glow."""
    k = 4
    n = size * S * k
    c = n / 2
    im = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(im)
    gear(d, n, 0, teeth=12, outer=0.44, inner=0.36, cut=False)
    d.ellipse([c - 0.3 * n, c - 0.3 * n, c + 0.3 * n, c + 0.3 * n], fill=0)
    d.ellipse([c - 0.24 * n, c - 0.24 * n, c + 0.24 * n, c + 0.24 * n], fill=255)
    d.ellipse([c - 0.2 * n, c - 0.2 * n, c + 0.2 * n, c + 0.2 * n], fill=0)
    d.ellipse([c - 0.1 * n, c - 0.1 * n, c + 0.1 * n, c + 0.1 * n], fill=255)
    m = np.asarray(im.resize((size * S, size * S), Image.LANCZOS), dtype=np.float32) / 255.0
    pad = 14 * S
    H = W = size * S + pad * 2
    mask = np.zeros((H, W), dtype=np.float32)
    mask[pad : pad + size * S, pad : pad + size * S] = m
    yy = np.arange(H, dtype=np.float32)[:, None]
    metal = tt.ramp(
        np.repeat(np.clip((yy - pad) / (size * S), 0, 1), W, axis=1),
        [(0, (255, 240, 200)), (0.45, (232, 182, 100)), (0.55, (250, 216, 150)), (1, (150, 92, 36))],
    )
    layer = (flat((H, W), AMBER), blur(mask, 7 * S) * 0.6)
    layer = over(layer, (flat((H, W), (30, 16, 8)), np.clip(blur(mask, 0.9 * S) * 2.5, 0, 1)))
    layer = over(layer, (metal, mask))
    save(to_image(*layer), "settings_emblem.png")


def chevron(w=16, h=28):
    """A thin chevron pointing right; the screen mirrors it for the left one."""

    def draw(d, n, lw):
        line(d, n, [(0.3, 0.12), (0.72, 0.5), (0.3, 0.88)], lw)

    k = 4
    n = h * S * k
    im = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(im)
    draw(d, n, max(1, int(round(2.0 * S * k))))
    im = im.resize((h * S, h * S), Image.LANCZOS).crop(((h - w) * S // 2, 0, (h + w) * S // 2, h * S))
    a = np.asarray(im, dtype=np.float32) / 255.0
    save(to_image(flat(a.shape, (255, 255, 255)), a), "chevron.png")


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=OUT, help="the folder to write into (default: Assets/Art/UI/Settings)")
    OUT = os.path.abspath(parser.parse_args().out)
    os.makedirs(OUT, exist_ok=True)
    emblem()
    hairline("rule_long.png", 1500, 24, 0.75, 0.42, 7.0, dots=True)
    hairline("rule_plain.png", 1640, 6, 0.55, 0.45)
    hairline("rule_row.png", 820, 6, 0.32, 0.3)
    vertical_rule("rule_vertical.png")
    tab_glow()
    band("side_bar.png", 360, 62, 0.08, 0.02, 0.7, gem_right=True)
    band("row_bar.png", 840, 56, 0.03, 0.45, 0.25)
    toggle("toggle_on.png", True)
    toggle("toggle_off.png", False)
    slider()
    chevron()
    icons()
    print("drew", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))


if __name__ == "__main__":
    main()
