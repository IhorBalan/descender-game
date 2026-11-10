"""Build the garden's painted backdrops from pieces cut out of the key art
(keyart_cuts.py): the far mountain range, the mid-far chain of temple islands, the
nearer mid layer and the golden tree landmark. The key art is what the kit was painted
to match, so these layers share its brushwork and palette, and cost no generations.

Each layer is 8192 x 1152 (four screens of 2048 x 1152, one screen tall: see
ParadiseGarden.Pinned) and wraps seamlessly sideways. Output goes to
out/Backdrop_Garden, where `scenario_gen.py derive` and `apply` pick it up.

  python tools/ai_art/keyart_backdrop.py
"""
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

HERE = os.path.dirname(__file__)
CUTS = os.path.join(HERE, "out", "keyart_cuts")
OUT = os.path.join(HERE, "out", "Backdrop_Garden")
W, H = 8192, 1152

# The haze at the horizon, sampled from the sky painting where the cloud sea meets it.
HAZE = (196, 214, 232)
HAZE_DEEP = (179, 194, 207)


def piece(
    name: str, erase=(), crop=None, left: int = 0, right: int = 0, bottom: int = 0, top: int = 0, misty: bool = False
) -> Image.Image:
    """
    A cut-out, cleaned: stray bits erased, then cropped, then the edges where the crop
    (or the key art's frame) cut through the painting faded out over that many pixels,
    so no straight edge shows against the sky. `misty` makes the side fades wander down the
    piece, as mist eats into a shape, so they do not read as the sides of a rectangle.
    """
    img = Image.open(os.path.join(CUTS, f"{name}_cut.png")).convert("RGBA")
    # The remover leaves a faint veil over the sky it took away: clear it, keep soft edges.
    alpha = img.getchannel("A").point(lambda a: 0 if a < 40 else min(255, round((a - 40) * 255 / 170)))
    draw = ImageDraw.Draw(alpha)
    for box in erase:
        draw.rectangle(box, fill=0)
    img.putalpha(alpha)
    if crop:
        img = img.crop(crop)
    ramp = Image.new("L", img.size, 255)
    rd = ImageDraw.Draw(ramp)
    for side, n in (("l", left), ("r", right), ("b", bottom), ("t", top)):
        for i in range(n):
            t = i / n
            v = round(255 * t * t * (3 - 2 * t))
            if side in "lr" and misty:
                continue
            if side == "l":
                rd.line([(i, 0), (i, img.height)], fill=v)
            elif side == "r":
                rd.line([(img.width - 1 - i, 0), (img.width - 1 - i, img.height)], fill=v)
            elif side == "t":
                row = ramp.crop((0, i, img.width, i + 1)).point(lambda p, v=v: min(p, v))
                ramp.paste(row, (0, i))
            else:
                y = img.height - 1 - i
                row = ramp.crop((0, y, img.width, y + 1)).point(lambda p, v=v: min(p, v))
                ramp.paste(row, (0, y))
    if misty:
        ramp = ImageChops.multiply(ramp, wandering_sides(img.size, left, right, name))
    img.putalpha(ImageChops.multiply(img.getchannel("A"), ramp))
    return img.crop(img.getchannel("A").getbbox())


def wandering_sides(size, left: int, right: int, name: str) -> Image.Image:
    """255 inside; toward each given side it fades over a band whose depth wanders from row to row."""
    w, h = size
    rng = random.Random(name)
    out = Image.new("L", size, 255)

    def wander(depth: int):
        """How far in the fade reaches on each row: slow swells with quicker ones on top."""
        slow = [rng.random() for _ in range(h // 90 + 3)]
        quick = [rng.random() for _ in range(h // 26 + 3)]

        def at(knots, y, step):
            i = y // step
            f = (y % step) / step
            f = f * f * (3 - 2 * f)
            return knots[i] * (1 - f) + knots[i + 1] * f

        return [depth * (0.25 + 0.75 * at(slow, y, 90)) + depth * 0.3 * at(quick, y, 26) for y in range(h)]

    px = out.load()
    for depth, is_left in ((left, True), (right, False)):
        if depth <= 0:
            continue
        reach = wander(depth)
        for y in range(h):
            for i in range(min(w, round(reach[y]) + 1)):
                t = i / max(1.0, reach[y])
                v = round(255 * t * t * (3 - 2 * t))
                x = i if is_left else w - 1 - i
                px[x, y] = min(px[x, y], v)
    return out


def haze(img: Image.Image, amount: float, colour=HAZE) -> Image.Image:
    """
    Atmospheric perspective: toward the air colour by `amount`, and greyer and softer with
    it (saturation down about a third and a slight blur at the far end), so only the play
    layer keeps full contrast and crisp edges.
    """
    alpha = img.getchannel("A")
    rgb = ImageEnhance.Color(img.convert("RGB")).enhance(1.0 - 0.6 * amount)
    tint = Image.new("RGB", img.size, colour)
    out = Image.blend(rgb, tint, amount).convert("RGBA")
    if amount >= 0.3:
        out = out.filter(ImageFilter.GaussianBlur(amount * 2.2))
        alpha = alpha.filter(ImageFilter.GaussianBlur(amount * 1.2))
    out.putalpha(alpha)
    return out


def place(canvas: Image.Image, img: Image.Image, x: float, base: float, height: float, flip=False, amount=0.0, colour=HAZE):
    """Stand a piece on `base` (its bottom edge) centred at x, `height` pixels tall, wrapping."""
    scale = height / img.height
    p = img.resize((max(1, round(img.width * scale)), round(height)), Image.LANCZOS)
    if flip:
        p = p.transpose(Image.FLIP_LEFT_RIGHT)
    if amount:
        p = haze(p, amount, colour)
    left = round(x - p.width / 2)
    top = round(base - p.height)
    for dx in (-W, 0, W):
        if left + dx < W and left + dx + p.width > 0:
            layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
            layer.paste(p, (left + dx, top))
            canvas.alpha_composite(layer)


def horizon(canvas: Image.Image, top: int, solid: int, colour=HAZE, below=HAZE_DEEP) -> None:
    """Haze rising from the cloud sea: clear above `top`, opaque from `solid` down."""
    fog = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(fog)
    for y in range(top, H):
        t = min(1.0, (y - top) / max(1, solid - top))
        c = tuple(round(a + (b - a) * max(0.0, (y - solid) / (H - solid))) for a, b in zip(colour, below)) if y > solid else colour
        d.line([(0, y), (W, y)], fill=c + (round(255 * t * t * (3 - 2 * t)),))
    canvas.alpha_composite(fog)


def far() -> Image.Image:
    k1 = piece("mount_k1", erase=[(0, 0, 18, 438), (690, 140, 813, 215)], crop=(0, 0, 813, 300), right=140)
    k2 = piece("mount_k2", left=90, right=120)
    k3 = piece("mount_k3", erase=[(0, 0, 45, 100)], crop=(0, 0, 585, 256), left=60, right=140)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rng = random.Random(7)
    # Back row: low, hazy ranges; front row: the tall peaks, a little clearer.
    back = [k3, k2, k3, k1, k2, k3, k2, k1]
    for i, m in enumerate(back):
        x = i * W / len(back) + rng.uniform(-120, 120)
        place(canvas, m, x, 880, rng.uniform(330, 400), flip=i % 2 == 1, amount=0.62)
    front = [k1, k3, k2, k1, k3]
    for i, m in enumerate(front):
        x = (i + 0.5) * W / len(front) + rng.uniform(-200, 200)
        place(canvas, m, x, 900, rng.uniform(520, 640), flip=i % 2 == 0, amount=0.45)
    horizon(canvas, 640, 880)
    return canvas


def islands():
    k1 = piece(
        "isle_k1_temple", erase=[(0, 0, 210, 25), (370, 520, 762, 578), (0, 470, 140, 578)], right=170, bottom=70, misty=True
    )
    k4 = piece("isle_k4_temple", erase=[(0, 0, 55, 90), (130, 730, 215, 781)], left=110, right=150, bottom=90, misty=True)
    left = piece("tree_k2", crop=(0, 400, 220, 800), left=60, right=50, bottom=80)
    right = piece("tree_k2", crop=(700, 330, 960, 800), left=70, right=90, bottom=80)
    return k1, k4, left, right


def midfar() -> Image.Image:
    k1, k4, left, right = islands()
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # A loose chain: each island once in four screens, mirrored ones far from their twin.
    plan = [
        (k1, 700, 760, 430, False),
        (right, 2050, 700, 330, False),
        (k4, 3300, 820, 560, True),
        (left, 4500, 720, 300, True),
        (k1, 5650, 780, 400, True),
        (k4, 7150, 760, 470, False),
    ]
    for img, x, base, height, flip in plan:
        place(canvas, img, x, base, height, flip, amount=0.62)
    horizon(canvas, 760, 1000)
    return canvas


def mid() -> Image.Image:
    k1, k4, left, right = islands()
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Nearer and bigger, few and far apart, so the play layer keeps open sky behind it.
    plan = [
        (k4, 1300, 1000, 760, False),
        (right, 3900, 980, 560, True),
        (k1, 6200, 1010, 640, False),
    ]
    for img, x, base, height, flip in plan:
        place(canvas, img, x, base, height, flip, amount=0.5)
    return canvas


def crown_top(img: Image.Image, rows: int, seed: int = 11) -> Image.Image:
    """
    Finishes a crown the key art's frame cut flat: adds `rows` above the piece, its own
    leaves mirrored up into them, shaped by an outline of overlapping leaf clumps that is
    tallest in the middle of the cut.
    """
    w, h = img.size
    out = Image.new("RGBA", (w, h + rows), (0, 0, 0, 0))
    out.paste(img, (0, rows))
    out.paste(img.crop((0, 0, w, rows)).transpose(Image.FLIP_TOP_BOTTOM), (0, 0))
    edge = [x for x in range(w) if img.getpixel((x, 1))[3] > 128]
    x0, x1 = edge[0], edge[-1]
    rng = random.Random(seed)
    mask = Image.new("L", (w * 2, rows * 2), 0)
    draw = ImageDraw.Draw(mask)
    x = float(x0)
    while x < x1:
        along = (x - x0) / max(1, x1 - x0)
        tall = (rows - 6) * (0.4 + 0.6 * (4.0 * along * (1.0 - along)) ** 0.5)
        r = rng.uniform(0.55, 1.0) * tall
        top = rows - rng.uniform(0.72, 1.0) * tall
        draw.ellipse([2 * (x - r * 1.35), 2 * top, 2 * (x + r * 1.35), 2 * (top + 2 * r + 40)], fill=255)
        x += rng.uniform(26, 58)
    mask = mask.resize((w, rows), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.8))
    alpha = out.getchannel("A")
    rows_alpha = ImageChops.multiply(alpha.crop((0, 0, w, rows)), mask)
    alpha.paste(rows_alpha, (0, 0))
    out.putalpha(alpha)
    return out


def golden_tree() -> Image.Image:
    # The whole crown, with its natural leafy outline: the sides are faded only below it,
    # where the neighbouring islands stand, and the bottom where the cloud sea begins. The
    # key art's frame cuts the very top flat, so that is finished by hand.
    tree = piece("tree_k2", crop=(0, 0, 820, 800), bottom=120)
    crown_ends = 350
    w, h = tree.size
    ramp = Image.new("L", (w, h), 255)
    rd = ImageDraw.Draw(ramp)
    for y in range(crown_ends, h):
        strength = min(1.0, (y - crown_ends) / 40.0)
        for x in range(w):
            left = min(1.0, x / 150.0)
            right = min(1.0, (w - 1 - x) / 130.0)
            side = (left * left * (3 - 2 * left)) * (right * right * (3 - 2 * right))
            rd.point((x, y), fill=round(255 * (1.0 - strength * (1.0 - side))))
    tree.putalpha(ImageChops.multiply(tree.getchannel("A"), ramp))
    tree = crown_top(tree, 50)
    canvas = Image.new("RGBA", (1536, 1280), (0, 0, 0, 0))
    scale = min(1400 / tree.width, 1270 / tree.height)
    t = tree.resize((round(tree.width * scale), round(tree.height * scale)), Image.LANCZOS)
    canvas.paste(t, ((1536 - t.width) // 2, 1280 - t.height), t)
    return canvas


# The focus is on the walk: how far each layer is out of it, as a blur radius in pixels.
FOCUS = {"garden_bg1_far": 2.6, "garden_bg2_midfar": 2.2, "garden_bg3_mid": 1.6}


def out_of_focus(img: Image.Image, radius: float) -> Image.Image:
    """A layer blurred with its alpha, wrapping around the sides, as the layer tiles."""
    pad = int(radius * 4)
    wide = Image.new("RGBA", (img.width + pad * 2, img.height), (0, 0, 0, 0))
    wide.paste(img.crop((img.width - pad, 0, img.width, img.height)), (0, 0))
    wide.paste(img, (pad, 0))
    wide.paste(img.crop((0, 0, pad, img.height)), (img.width + pad, 0))
    soft = wide.convert("RGBa").filter(ImageFilter.GaussianBlur(radius)).convert("RGBA")
    return soft.crop((pad, 0, img.width + pad, img.height))


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    for name, make in (
        ("garden_bg1_far", far),
        ("garden_bg2_midfar", midfar),
        ("garden_bg3_mid", mid),
        ("golden_tree", golden_tree),
    ):
        img = make()
        if name in FOCUS:
            img = out_of_focus(img, FOCUS[name])
        img.save(os.path.join(OUT, f"{name}.png"))
        print(f"built {name} {img.size}")


if __name__ == "__main__":
    main()
