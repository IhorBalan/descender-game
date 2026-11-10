"""Splits the painted sky into the part that stays and the part that moves.

The garden's sky was painted as one picture with its clouds in it, and a sky is pinned to the
view, so the same clouds sat in the same place for the whole walk. This separates it:

  garden_bg0_sky_clear.png   the sky without clouds, sun and rays kept: stays on screen
  garden_bg0_clouds.png      the clouds alone, two screens wide, tiling: drifts and scrolls

The source stays garden_bg0_sky.png. Clouds are found by the fine detail painted in them, which
the smooth sun and rays do not have. Where a cloud hid the sky, the sky is filled from around it.
Each cloud is then lifted out on its own: the ones the painting's frame sliced are dropped, so
nothing in the strip is ever cut, and the whole ones fill both screens, in their painted places on
the first and rearranged on the second. The soft vertical stripe in the painting's left half goes.

  python tools/ai_art/sky_split.py
"""
import os
import random

import numpy as np
from PIL import Image

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

FOLDER = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Backdrop_Garden")
SOURCE = os.path.join(FOLDER, "garden_bg0_sky.png")
CLEAR = os.path.join(FOLDER, "garden_bg0_sky_clear.png")
CLOUDS = os.path.join(FOLDER, "garden_bg0_clouds.png")

# Everything left of this column is free of the sun: there a sky varies only from top to bottom.
SUN_STARTS = 1000
# On the sun's side nothing above this row is cloud: only rays, which must stay in the sky.
SUN_SKY_ENDS = 235
# Clouds painted over the sun's rainbow, right of this column, do not survive being lifted out.
RAINBOW_STARTS = 1700
# A smooth blue smudge of cloud on the sun's side, with no painted detail to be found by.
FAINT_CLOUD = (1720, 560, 2048, 900)
# A gap inside a cloud smaller than this many pixels is cloud; a bigger one is sky seen through.
SMALL_HOLE = 2500
# Scraps smaller than this many pixels are not worth moving.
SMALLEST_CLOUD = 900


def blur(img: np.ndarray, sigma: float) -> np.ndarray:
    return cv2.GaussianBlur(img, (0, 0), sigma, borderType=cv2.BORDER_REPLICATE)


def smoothstep(a: float, b: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def round_kernel(n: int) -> np.ndarray:
    return cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (n, n))


def fill(img: np.ndarray, known: np.ndarray) -> np.ndarray:
    """Fills where `known` is 0 from around it, coarse to fine, by normalised blurs."""
    k = known[..., None]
    result = None
    for sigma in (320, 160, 80, 40, 20, 10, 5):
        weight = blur(known, sigma)[..., None]
        guess = blur(img * k, sigma) / (weight + 1e-4)
        if result is None:
            result = guess
        else:
            sure = smoothstep(0.08, 0.5, weight)
            result = result * (1.0 - sure) + guess * sure
    return img * k + result * (1.0 - k)


def fill_holes(mask: np.ndarray, smaller_than: int = 0) -> np.ndarray:
    """Fills the holes in a mask: all of them, or only those smaller than a number of pixels."""
    count, labels, stats, _ = cv2.connectedComponentsWithStats(1 - mask, connectivity=4)
    h, w = mask.shape
    out = mask.copy()
    for i in range(1, count):
        x, y, cw, ch, area = stats[i]
        touches_edge = x == 0 or y == 0 or x + cw == w or y + ch == h
        if not touches_edge and (smaller_than == 0 or area < smaller_than):
            out[labels == i] = 1
    return out


def cloud_masks(rgb: np.ndarray):
    """Broadly, closed over the gaps, for what the sky must fill in; tightly, for what moves with a cloud."""
    smooth = blur(rgb, 1.6)
    fine = np.linalg.norm(smooth - blur(smooth, 9), axis=2)
    detail = (fine > 0.017).astype(np.uint8)
    detail[: SUN_SKY_ENDS, SUN_STARTS:] = 0
    broad = fill_holes(cv2.morphologyEx(detail, cv2.MORPH_CLOSE, round_kernel(31)))
    broad = cv2.morphologyEx(broad, cv2.MORPH_OPEN, round_kernel(9))
    tight = fill_holes(cv2.morphologyEx(detail, cv2.MORPH_CLOSE, round_kernel(9)), SMALL_HOLE)
    return broad, cv2.morphologyEx(tight, cv2.MORPH_OPEN, round_kernel(5))


def clear_sky(rgb: np.ndarray, mask: np.ndarray) -> np.ndarray:
    h, w = rgb.shape[:2]
    grown = cv2.dilate(mask, round_kernel(41))
    x0, y0, x1, y1 = FAINT_CLOUD
    grown[y0:y1, x0:x1] = 1
    sky = fill(rgb, 1.0 - grown.astype(np.float32))
    # Where clouds used to end the sun's glow the fill leaves a flat edge: spread it downward.
    spread = cv2.GaussianBlur(sky, (0, 0), sigmaX=36, sigmaY=120, borderType=cv2.BORDER_REPLICATE)
    ys = np.arange(h, dtype=np.float32)
    xs = np.arange(w, dtype=np.float32)
    down = smoothstep(SUN_SKY_ENDS * 0.7, SUN_SKY_ENDS * 1.2, ys) * (
        1.0 - smoothstep(SUN_SKY_ENDS * 3.0, SUN_SKY_ENDS * 4.2, ys)
    )
    side = smoothstep(SUN_STARTS * 0.7, SUN_STARTS * 1.1, xs)
    amount = (down[:, None] * side[None, :])[..., None]
    sky = sky * (1.0 - amount) + spread * amount
    # Inside the filled clouds keep only the broad change of colour, and blur the left half
    # sideways alone: that keeps the top-to-bottom gradient and removes the vertical stripe.
    where = blur(grown.astype(np.float32), 26)[..., None]
    sky = sky * (1.0 - where) + blur(sky, 48) * where
    sideways = cv2.GaussianBlur(sky, (0, 0), sigmaX=200, sigmaY=0.01, borderType=cv2.BORDER_REPLICATE)
    keep = smoothstep(SUN_STARTS * 0.72, SUN_STARTS * 1.08, xs)[None, :, None]
    return sky * keep + sideways * (1.0 - keep)


def matte(rgb: np.ndarray, sky: np.ndarray, broad: np.ndarray, tight: np.ndarray):
    """A cloud's opacity, and a colour for it that holds out to its rim."""
    # The painter's pale glow around a cloud is still sky: it is as blue as the sky, only
    # lighter. A cloud is far paler in colour than the sky behind it, or simply far from it, and
    # never strongly blue; only deep inside one is anything taken as cloud regardless.
    paler = (sky.max(axis=2) - sky.min(axis=2)) - (rgb.max(axis=2) - rgb.min(axis=2))
    distance = np.linalg.norm(rgb - sky, axis=2)
    blue = rgb[..., 2] - rgb[..., 0]
    alpha = np.maximum(smoothstep(0.14, 0.3, paler), smoothstep(0.13, 0.24, distance))
    alpha *= blur(cv2.dilate(broad, np.ones((25, 25), np.uint8)).astype(np.float32), 8)
    alpha *= 1.0 - smoothstep(0.16, 0.3, blue)
    deep = cv2.erode(tight, round_kernel(31)).astype(np.float32)
    alpha = np.maximum(alpha, blur(deep, 4))
    a = alpha[..., None]
    colour = np.clip((rgb - (1.0 - a) * sky) / np.maximum(a, 0.04), 0.0, 1.0)
    # A rim takes the colour of the cloud inside it. Left as solved it would carry the blue of
    # the sky it was painted on, and show as a dark outline over the warm light near the sun.
    return fill(colour, (alpha > 0.85).astype(np.float32)), alpha


def whole_clouds(rgba: np.ndarray) -> list:
    """Each cloud on its own, biggest first, as (x, y, picture): none the painting's frame sliced."""
    h, w = rgba.shape[:2]
    alpha = rgba[..., 3]
    cores = (alpha > 0.45).astype(np.uint8)
    count, core_labels = cv2.connectedComponents(cores, connectivity=8)
    # Every soft pixel belongs to the core nearest it, so clouds separate where they are thinnest.
    _, nearest = cv2.distanceTransformWithLabels(1 - cores, cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_CCOMP)
    seen = core_labels > 0
    by_seed = np.zeros(int(nearest.max()) + 1, np.int32)
    by_seed[nearest[seen]] = core_labels[seen]
    labels = by_seed[nearest]
    clouds = []
    for i in range(1, count):
        ys, xs = np.nonzero((labels == i) & (alpha > 0.04))
        if xs.size < SMALLEST_CLOUD:
            continue
        x0, x1, y0, y1 = int(xs.min()), int(xs.max()) + 1, int(ys.min()), int(ys.max()) + 1
        if x0 == 0 or x1 == w or y0 == 0 or y1 == h or xs.mean() > RAINBOW_STARTS:
            continue
        piece = rgba[y0:y1, x0:x1].copy()
        piece[..., 3] *= labels[y0:y1, x0:x1] == i
        clouds.append((x0, y0, piece))
    clouds.sort(key=lambda c: -c[2].shape[0] * c[2].shape[1])
    return clouds


def stamp(canvas: np.ndarray, piece: np.ndarray, x: int, y: int) -> None:
    """Lays a cloud over the canvas."""
    ch, cw = piece.shape[:2]
    a = piece[..., 3:]
    under = canvas[y : y + ch, x : x + cw]
    out_a = a + under[..., 3:] * (1.0 - a)
    under[..., :3] = (piece[..., :3] * a + under[..., :3] * under[..., 3:] * (1.0 - a)) / np.maximum(out_a, 1e-4)
    under[..., 3:] = out_a


def rearranged(clouds: list, shape: tuple, seed: int) -> np.ndarray:
    """A screen of the same clouds elsewhere, each moved, resized a little and sometimes turned around."""
    h, w = shape[:2]
    rng = random.Random(seed)
    canvas = np.zeros(shape, np.float32)
    taken = np.zeros((h, w), bool)
    for _, y, piece in clouds + clouds[: len(clouds) // 3]:
        scale = rng.uniform(0.86, 1.14)
        piece = cv2.resize(piece, None, fx=scale, fy=scale, interpolation=cv2.INTER_LINEAR)
        if rng.random() < 0.5:
            piece = piece[:, ::-1]
        ch, cw = piece.shape[:2]
        solid = piece[..., 3] > 0.05
        best = None
        for _ in range(120):
            nx = rng.randint(24, max(24, w - cw - 24))
            ny = min(max(0, y + rng.randint(-46, 46)), h - ch)
            overlap = int((taken[ny : ny + ch, nx : nx + cw] & solid).sum())
            if best is None or overlap < best[0]:
                best = (overlap, nx, ny)
            if overlap == 0:
                break
        _, nx, ny = best
        stamp(canvas, piece, nx, ny)
        taken[ny : ny + ch, nx : nx + cw] |= solid
    return canvas


def strip(colour: np.ndarray, alpha: np.ndarray) -> np.ndarray:
    """Two screens that tile: the clouds where they were painted, in the middle, and rearranged around them."""
    rgba = np.dstack((colour, alpha)).astype(np.float32)
    clouds = whole_clouds(rgba)
    own = np.zeros_like(rgba)
    for x, y, piece in reversed(clouds):
        stamp(own, piece, x, y)
    both = np.concatenate((own, rearranged(clouds, rgba.shape, 8)), axis=1)
    print("clouds", len(clouds))
    return np.roll(both, own.shape[1] // 2, axis=1)


def save(rgba: np.ndarray, path: str) -> None:
    Image.fromarray((np.clip(rgba, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGBA").save(path, optimize=True)


def main() -> None:
    rgb = np.asarray(Image.open(SOURCE).convert("RGB"), dtype=np.float32) / 255.0
    broad, tight = cloud_masks(rgb)
    sky = clear_sky(rgb, broad)
    colour, alpha = matte(rgb, sky, broad, tight)
    # A breath of noise, or a smooth gradient bands in eight bits.
    rng = np.random.default_rng(5)
    dithered = sky + rng.uniform(-0.5, 0.5, sky.shape[:2])[..., None] / 255.0
    save(np.dstack((dithered, np.ones(sky.shape[:2], np.float32))), CLEAR)
    save(strip(colour, alpha), CLOUDS)


if __name__ == "__main__":
    main()
