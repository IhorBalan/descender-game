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

from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(__file__)
CUTS = os.path.join(HERE, "out", "keyart_cuts")
OUT = os.path.join(HERE, "out", "Backdrop_Garden")
W, H = 8192, 1152

# The haze at the horizon, sampled from the sky painting where the cloud sea meets it.
HAZE = (196, 214, 232)
HAZE_DEEP = (179, 194, 207)


def piece(name: str, erase=(), crop=None, left: int = 0, right: int = 0, bottom: int = 0, top: int = 0) -> Image.Image:
    """
    A cut-out, cleaned: stray bits erased, then cropped, then the edges where the crop
    (or the key art's frame) cut through the painting faded out over that many pixels,
    so no straight edge shows against the sky.
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
    img.putalpha(ImageChops.multiply(img.getchannel("A"), ramp))
    return img.crop(img.getchannel("A").getbbox())


def haze(img: Image.Image, amount: float, colour=HAZE) -> Image.Image:
    tint = Image.new("RGBA", img.size, colour + (255,))
    out = Image.blend(img, tint, amount)
    out.putalpha(img.getchannel("A"))
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
        place(canvas, m, x, 880, rng.uniform(330, 400), flip=i % 2 == 1, amount=0.5)
    front = [k1, k3, k2, k1, k3]
    for i, m in enumerate(front):
        x = (i + 0.5) * W / len(front) + rng.uniform(-200, 200)
        place(canvas, m, x, 900, rng.uniform(520, 640), flip=i % 2 == 0, amount=0.28)
    horizon(canvas, 640, 880)
    return canvas


def islands():
    k1 = piece("isle_k1_temple", erase=[(0, 0, 210, 25), (370, 520, 762, 578), (0, 470, 140, 578)], right=170, bottom=70)
    k4 = piece("isle_k4_temple", erase=[(0, 0, 55, 90), (130, 730, 215, 781)], left=110, right=150, bottom=90)
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
        place(canvas, img, x, base, height, flip, amount=0.42)
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
        place(canvas, img, x, base, height, flip, amount=0.2)
    return canvas


def golden_tree() -> Image.Image:
    # The whole crown (the key art's frame cuts its top: faded like the sides) and the
    # island under it, with the neighbouring islands' edges faded away.
    tree = piece("tree_k2", crop=(0, 0, 820, 800), left=150, right=130, bottom=120, top=140)
    canvas = Image.new("RGBA", (1536, 1280), (0, 0, 0, 0))
    scale = min(1400 / tree.width, 1240 / tree.height)
    t = tree.resize((round(tree.width * scale), round(tree.height * scale)), Image.LANCZOS)
    canvas.paste(t, ((1536 - t.width) // 2, 1280 - t.height), t)
    return canvas


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    for name, make in (
        ("garden_bg1_far", far),
        ("garden_bg2_midfar", midfar),
        ("garden_bg3_mid", mid),
        ("golden_tree", golden_tree),
    ):
        img = make()
        img.save(os.path.join(OUT, f"{name}.png"))
        print(f"built {name} {img.size}")


if __name__ == "__main__":
    main()
