"""Out-of-focus foreground framing for the garden, baked from the kit.

URP's 2D renderer has no depth of field, so the blur lives in the art. Each piece is built
from the kit's own plants at several times their size, in two planes: the back one a little
out of focus, the front one a lot. The blur is a disc in linear light, which is what a lens
does: a white blossom spreads into a soft bright coin instead of a grey smear. The plants are
then taken down into shade, and the upper edge catches the sky, so they read as the dark
garden the view is looking through.

Written to Assets/Art/Paradise/Kit/Nature as fg_soft_rise_*.png (pivot at the bottom edge)
and fg_soft_hang_*.png (pivot at the top edge). Run after kit_polish.py:

  python tools/ai_art/fg_framing.py
"""
import os
import random

import numpy as np
from PIL import Image

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

NATURE = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Kit", "Nature")

PPU = 48  # of the written art: a blurred picture has no detail to keep.
WORK = 96  # pixels per unit while building, so the disc blur stays round.
KIT = 128  # pixels per unit of the kit's plants.

GAMMA = 2.2
# Linear-light multiplier per plane: the front plane is the darkest, the nearest to the eye.
SHADE_BACK = np.array([0.12, 0.17, 0.165])
SHADE_FRONT = np.array([0.045, 0.07, 0.07])
SKY_EDGE = np.array([0.72, 0.68, 0.42])

# Where the pieces sit, in units from the walk (ParadiseGarden.Framing uses the same numbers).
RISE_FOOT = 8.0  # the foot of a rising piece, under the walk
HANG_TOP = 11.0  # the top of a hanging piece, over the walk


def plant(name: str, scale: float, flip: bool = False, turn: float = 0.0) -> Image.Image:
    """A kit plant at `scale` times its size in the world, in working pixels."""
    img = Image.open(os.path.join(NATURE, f"{name}.png")).convert("RGBA")
    factor = scale * WORK / KIT
    img = img.resize((max(1, round(img.width * factor)), max(1, round(img.height * factor))), Image.LANCZOS)
    if flip:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    if turn:
        img = img.rotate(turn, Image.BICUBIC, expand=True)
    return img


def over(canvas: Image.Image, img: Image.Image, x: float, y: float) -> None:
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    layer.paste(img, (round(x), round(y)))
    canvas.alpha_composite(layer)


def disc(radius: float) -> np.ndarray:
    n = int(np.ceil(radius))
    ys, xs = np.mgrid[-n : n + 1, -n : n + 1]
    kernel = np.clip(radius + 0.5 - np.sqrt(xs * xs + ys * ys), 0.0, 1.0).astype(np.float32)
    return kernel / kernel.sum()


def defocus(layer: Image.Image, radius: float) -> np.ndarray:
    """Premultiplied linear RGBA of a layer seen out of focus."""
    pixels = np.asarray(layer).astype(np.float32) / 255.0
    alpha = pixels[..., 3:]
    linear = np.concatenate((pixels[..., :3] ** GAMMA * alpha, alpha), axis=2)
    return cv2.filter2D(linear, -1, disc(radius), borderType=cv2.BORDER_REPLICATE)


def shaded(premultiplied: np.ndarray, shade: np.ndarray) -> np.ndarray:
    out = premultiplied.copy()
    out[..., :3] *= shade
    return out


def composite(under: np.ndarray, top: np.ndarray) -> np.ndarray:
    return top + under * (1.0 - top[..., 3:])


def sky_edge(image: np.ndarray, radius: float) -> np.ndarray:
    """Light on whatever faces the sky: the part of the coverage with nothing above it."""
    alpha = image[..., 3]
    n = int(radius)
    above = np.roll(alpha, n, axis=0)
    above[:n] = 0.0
    edge = np.clip(alpha - above, 0.0, 1.0)
    edge = cv2.GaussianBlur(edge, (0, 0), radius * 0.5) * alpha
    out = image.copy()
    out[..., :3] += edge[..., None] * SKY_EDGE * 0.32
    return out


def peak(u: float, at: float = 0.32) -> float:
    """0 at the piece's tallest point, 1 at either end, so it runs out of view on both sides."""
    side = at if u < at else 1.0 - at
    return (abs(u - at) / side) ** 1.4


def inside(centre: float, piece: int, width: int) -> float:
    """The left edge that keeps a piece clear of the canvas sides: a plant is never cut off."""
    margin = 40
    return max(margin, min(width - margin - piece, centre - piece / 2))


def rising_bank(layer: Image.Image, edge, names: tuple, scales: tuple, step: float, rng: random.Random) -> None:
    """Plants packed under a line, row on row to the foot, so the outline is leaves everywhere."""
    width, height = layer.size
    gap = round(step * WORK)
    for row in range(14):
        for x in range(-gap, width + gap, gap):
            at = x + rng.uniform(-0.4, 0.4) * gap
            y = edge(max(0.0, min(1.0, at / width))) + row * gap * 0.75 + rng.uniform(-0.25, 0.25) * gap
            if y < height:
                img = plant(rng.choice(names), rng.uniform(*scales), rng.random() < 0.5, rng.uniform(-14.0, 14.0))
                over(layer, img, inside(at, img.width, width), y)


def hanging_bank(layer: Image.Image, edge, names: tuple, scales: tuple, step: float, rng: random.Random) -> None:
    """Growth packed above a line, row on row to the top, every strand ending at or above it."""
    width = layer.size[0]
    gap = round(step * WORK)
    for row in range(14):
        for x in range(-gap, width + gap, gap):
            at = x + rng.uniform(-0.4, 0.4) * gap
            y = edge(max(0.0, min(1.0, at / width))) - row * gap * 0.75 + rng.uniform(-0.25, 0.25) * gap
            img = plant(rng.choice(names), rng.uniform(*scales), rng.random() < 0.5)
            if y > 0:
                over(layer, img, inside(at, img.width, width), y - img.height)


def finish(back: np.ndarray, front: np.ndarray, path: str) -> None:
    image = sky_edge(composite(shaded(back, SHADE_BACK), shaded(front, SHADE_FRONT)), 7.0)
    size = (round(image.shape[1] * PPU / WORK), round(image.shape[0] * PPU / WORK))
    small = cv2.resize(image, size, interpolation=cv2.INTER_AREA)
    alpha = np.clip(small[..., 3:], 0.0, 1.0)
    rgb = (small[..., :3] / np.maximum(alpha, 1e-4)).clip(0.0, 1.0) ** (1.0 / GAMMA)
    out = np.concatenate((rgb, alpha), axis=2)
    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), "RGBA").save(path, optimize=True)
    print(f"{os.path.basename(path)}: {size[0]}x{size[1]}")


def rise(index: int, seed: int) -> None:
    """A bank of plants rising from below the frame. The foot is RISE_FOOT units under the
    walk; the tallest point just passes the walk, and both ends sink out of the view."""
    rng = random.Random(seed)
    width, height = 14 * WORK, round((RISE_FOOT + 1.6) * WORK)
    size = (width, height)

    def top(u: float, lower: float = 0.0) -> float:
        walk_depth = -0.4 + 5.0 * peak(u) + lower  # units under the walk
        return height - (RISE_FOOT - walk_depth) * WORK

    back = Image.new("RGBA", size, (0, 0, 0, 0))
    rising_bank(back, top, ("flower_bush", "fern", "fern"), (1.4, 1.9), 1.1, rng)
    front = Image.new("RGBA", size, (0, 0, 0, 0))
    rising_bank(front, lambda u: top(u, 0.9), ("fern", "fern", "lily_cluster_a", "lily_cluster_b"), (2.2, 3.0), 1.5, rng)
    finish(defocus(back, 9.0), defocus(front, 18.0), os.path.join(NATURE, f"fg_soft_rise_{index}.png"))


def hang(index: int, seed: int) -> None:
    """Growth hanging into the top of the frame. The top edge is HANG_TOP units over the walk,
    past the top of the view; the longest strands stop above the hero, and both ends rise
    out of the view."""
    rng = random.Random(seed)
    width, height = 13 * WORK, round((HANG_TOP - 3.4) * WORK)
    size = (width, height)

    def tip(u: float, shorter: float = 0.0) -> float:
        over_walk = 4.2 + 5.0 * peak(u) + shorter  # units over the walk
        return (HANG_TOP - over_walk) * WORK

    back = Image.new("RGBA", size, (0, 0, 0, 0))
    hanging_bank(back, tip, ("ivy_curtain", "wisteria_drape", "ivy_drape_a"), (0.8, 1.1), 1.1, rng)
    front = Image.new("RGBA", size, (0, 0, 0, 0))
    hanging_bank(front, lambda u: tip(u, 0.7), ("ivy_drape_a", "ivy_drape_b", "ivy_drape_c"), (1.0, 1.3), 1.5, rng)
    finish(defocus(back, 8.0), defocus(front, 16.0), os.path.join(NATURE, f"fg_soft_hang_{index}.png"))


def main() -> None:
    for index, seed in enumerate((11, 29, 47)):
        rise(index, seed)
    for index, seed in enumerate((13, 31, 53)):
        hang(index, seed)


if __name__ == "__main__":
    main()
