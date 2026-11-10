"""The orchard's roof of leaves, seen from under it: two tiling bands for the top of the view.

Zone 2 is the Orchard Under the Canopy (docs/design/paradise.md): the sun reaches it only in
shafts and flecks. These bands are the canopy itself, pinned to the top of the view (so their
top edge never shows, at any height) and scrolled sideways at their own depth by ParadiseGarden:

  canopy_back.png    just behind the play layer: the orchard's blossom crowns seen from below,
                     their undersides in the shade, glowing yellow-green where the sun comes
                     through the leaves, open in places to the sky, strands of ivy and wisteria
                     hanging from their lowest lobes. A little soft.
  canopy_front.png   between the eye and the walk: the same growth near and out of focus, dark,
                     only along the very top, passing faster than the world.

Both are built from the kit's own plants, like the framing (fg_framing.py), and tile sideways:
every plant is drawn again a band's width to either side. Written to Kit/Nature.

  python tools/ai_art/canopy.py
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
KIT = 128  # pixels per unit of the kit's plants
WORK = 96  # pixels per unit while building
GAMMA = 2.2

# The bands, in units of the painting (ParadiseGarden pins each to one screen per 32 units).
BACK = dict(width=64.0, height=8.0, ppu=64)
FRONT = dict(width=64.0, height=5.0, ppu=40)

# Linear-light shade of the canopy's underside, of its blossoms (they keep more light), and the
# glow of leaves with the sun behind them.
SHADE = np.array([0.06, 0.105, 0.1])
SHADE_BLOSSOM = np.array([0.17, 0.22, 0.22])
THROUGH = np.array([0.34, 0.42, 0.14])
SHADE_FRONT = np.array([0.04, 0.065, 0.065])
# How much of its light the roof keeps at its top, where the most leaves stack overhead.
DEEP = 0.45
# The far crowns: shaded less, and hazed toward the sky behind them.
SHADE_FAR = np.array([0.16, 0.22, 0.23])
SKY_FAR = np.array([0.32, 0.5, 0.72])


def plant(name: str, scale: float, flip_x: bool = False, flip_y: bool = False, crop_top: float = 1.0) -> Image.Image:
    """A kit plant at `scale` times its size in the world, in working pixels; `crop_top` keeps that share from its top."""
    img = Image.open(os.path.join(NATURE, f"{name}.png")).convert("RGBA")
    if crop_top < 1.0:
        img = img.crop((0, 0, img.width, round(img.height * crop_top)))
    factor = scale * WORK / KIT
    img = img.resize((max(1, round(img.width * factor)), max(1, round(img.height * factor))), Image.LANCZOS)
    if flip_x:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    if flip_y:
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    return img


def over_tiled(canvas: Image.Image, img: Image.Image, x: float, y: float) -> None:
    """Pastes at x, and a canvas width to either side, so the band tiles sideways."""
    for dx in (-canvas.width, 0, canvas.width):
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        layer.paste(img, (round(x + dx), round(y)))
        canvas.alpha_composite(layer)


def linear(img: Image.Image) -> np.ndarray:
    """Premultiplied linear RGBA."""
    p = np.asarray(img).astype(np.float32) / 255.0
    a = p[..., 3:]
    return np.concatenate((p[..., :3] ** GAMMA * a, a), axis=2)


def disc(radius: float) -> np.ndarray:
    n = int(np.ceil(radius))
    ys, xs = np.mgrid[-n : n + 1, -n : n + 1]
    kernel = np.clip(radius + 0.5 - np.sqrt(xs * xs + ys * ys), 0.0, 1.0).astype(np.float32)
    return kernel / kernel.sum()


def defocus(image: np.ndarray, radius: float) -> np.ndarray:
    """Out of focus, wrapping sideways so the band still tiles."""
    pad = int(np.ceil(radius)) + 1
    wrapped = np.concatenate((image[:, -pad:], image, image[:, :pad]), axis=1)
    return cv2.filter2D(wrapped, -1, disc(radius), borderType=cv2.BORDER_CONSTANT)[:, pad:-pad]


def write(image: np.ndarray, ppu: int, name: str) -> None:
    size = (round(image.shape[1] * ppu / WORK), round(image.shape[0] * ppu / WORK))
    small = cv2.resize(image, size, interpolation=cv2.INTER_AREA)
    alpha = np.clip(small[..., 3:], 0.0, 1.0)
    rgb = (small[..., :3] / np.maximum(alpha, 1e-4)).clip(0.0, 1.0) ** (1.0 / GAMMA)
    out = np.concatenate((rgb, alpha), axis=2)
    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(NATURE, name), optimize=True)
    print(f"{name}: {size[0]}x{size[1]}")


def crown_row(rng: random.Random, w: int, h: int, scales: tuple, hang: tuple, step: tuple) -> Image.Image:
    """Crowns turned over, side by side: seen from below, a crown's rounded top is the lobe
    that hangs lowest. Each hangs its own depth (a share of its height below the band's top)."""
    row = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    x = 0.0
    while x < w:
        img = plant("blossom_tree", rng.uniform(*scales), rng.random() < 0.5, True, 0.7)
        over_tiled(row, img, x - img.width / 2, rng.uniform(*hang) * img.height - img.height)
        x += img.width * rng.uniform(*step)
    return row


def back() -> None:
    rng = random.Random(7)
    w, h = round(BACK["width"] * WORK), round(BACK["height"] * WORK)
    # Behind, higher crowns further off: paler, toward the sky they stand against.
    far = linear(crown_row(rng, w, h, (0.7, 0.95), (0.35, 0.6), (0.35, 0.5)))
    far[..., :3] = far[..., :3] * SHADE_FAR + SKY_FAR * far[..., 3:] * 0.55
    # The near crowns, close together, a few apart so the sky shows between them.
    crowns = crown_row(rng, w, h, (1.0, 1.4), (0.55, 0.9), (0.4, 0.58))
    # Growth along the very top, so the band is leaves edge to edge where the view begins.
    x = 0.0
    while x < w:
        img = plant(rng.choice(("flower_bush", "hedge_flowers")), rng.uniform(1.0, 1.5), rng.random() < 0.5, True)
        over_tiled(crowns, img, x - img.width / 2, -img.height * rng.uniform(0.45, 0.7))
        x += img.width * 0.55
    mass = np.asarray(crowns)[..., 3].astype(np.float32) / 255.0
    # Strands from the lowest lobes: each from a point on the underside of the mass.
    strands = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for _ in range(int(BACK["width"] / 2.2)):
        sx = rng.uniform(0, w)
        column = mass[:, int(sx) % w]
        filled = np.flatnonzero(column > 0.9)
        if filled.size == 0:
            continue
        low = filled.max()
        name = rng.choice(("ivy_curtain", "ivy_drape_a", "ivy_drape_b", "ivy_drape_c", "ivy_drape_a", "wisteria_drape" if rng.random() < 0.3 else "ivy_drape_b"))
        img = plant(name, rng.uniform(0.6, 0.95), rng.random() < 0.5)
        over_tiled(strands, img, sx - img.width / 2, low - img.height * 0.4)
    # Shade the underside, the blossoms keeping more of their light, and light the leaves the
    # sun comes through: along the outline of each mass of leaves (its smoothed coverage
    # halfway), not round every little hole in it.
    # The strands hang from behind the lobes, so where each begins is hidden in the leaves.
    picture = linear_over(linear(crowns), linear(strands))
    a = picture[..., 3]
    rgb = picture[..., :3] / np.maximum(a[..., None], 1e-4)
    whiteness = np.clip((rgb.mean(axis=2) - 0.35) / 0.4, 0.0, 1.0)[..., None]
    shade = SHADE * (1.0 - whiteness) + SHADE_BLOSSOM * whiteness
    coverage = cv2.GaussianBlur(a, (0, 0), 0.45 * WORK)
    rim = np.clip(1.0 - np.abs(coverage - 0.45) / 0.3, 0.0, 1.0)[..., None] ** 2
    lit = rgb * shade + THROUGH * rim * (0.4 + 0.6 * whiteness)
    # Deepest where the leaves stack most, at the top, lightest in the lowest lobes the sun reaches.
    down = np.linspace(0.0, 1.0, h, dtype=np.float32)[:, None, None]
    lit *= DEEP + (1.0 - DEEP) * np.clip(down / 0.7, 0.0, 1.0) ** 0.8
    near = np.concatenate((lit * a[..., None], a[..., None]), axis=2)
    picture = linear_over(defocus(near, 2.0), defocus(far, 4.0))
    write(picture, BACK["ppu"], "canopy_back.png")


def linear_over(top: np.ndarray, under: np.ndarray) -> np.ndarray:
    return top + under * (1.0 - top[..., 3:])


def front() -> None:
    rng = random.Random(19)
    w, h = round(FRONT["width"] * WORK), round(FRONT["height"] * WORK)
    near = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # Near, so large: clumps hanging from above the view, in a ragged line, now and then a gap.
    x = 0.0
    while x < w:
        name = rng.choice(("flower_bush", "hedge_flowers", "ivy_curtain", "ivy_drape_a"))
        img = plant(name, rng.uniform(2.2, 3.2), rng.random() < 0.5, name in ("flower_bush", "hedge_flowers"))
        reach = rng.uniform(0.35, 0.95) * h
        over_tiled(near, img, x - img.width / 2, reach - img.height)
        x += img.width * rng.uniform(0.3, 0.65)
    picture = linear(near)
    picture[..., :3] *= SHADE_FRONT
    write(defocus(picture, 14.0), FRONT["ppu"], "canopy_front.png")


def main() -> None:
    back()
    front()


if __name__ == "__main__":
    main()
