"""Normal maps for the painted garden kit, so stone, bark and leaves answer the sun.

The kit is flat paintings. The normal map of each piece is built from what the painting
already says about its form, at half the art's resolution (soft on purpose: it reads as
paint, not as noise, and costs a quarter of the memory):

- relief: the painting's light and dark, split into bands so mortar, cracks and carved
  ornament stand out while the broad light painted into the piece is ignored;
- bevel: the silhouette, rounded inward, so every edge turns away like a real one. With the
  sun low on one side this is what gives a rim of light on one edge and shade on the other;
- lathe (columns): each row is a cylinder between its left and right edge;
- dome (foliage): a clump is a ball of leaves, lit on its sunny side as one mass.

Written to Assets/Art/Paradise/Normals as <name>_n.png; ParadiseNormals.cs assigns them.

  python tools/ai_art/normals.py            every kit piece
  python tools/ai_art/normals.py fern       only pieces whose name contains "fern"
"""
import glob
import os
import sys

import numpy as np
from PIL import Image

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise")
SOURCES = [os.path.join(ROOT, "Kit"), os.path.join(ROOT, "Derived")]
OUT = os.path.join(ROOT, "Normals")
SCALE = 0.5

# Never lit from a direction: light itself, water and the soft masks.
SKIP = ("waterfall", "mist", "foam", "glow", "petal", "contact_shadow", "depth_shade", "fg_leaves")

# (relief bands fine/mid/broad in height per unit of luma, bevel radius, dome radius): all in
# pixels of the normal map. wrap is (x, y): the sides that tile.
STONE = dict(relief=(3, 10, 8), bevel=14, dome=0, wrap=(False, False))
PRESETS = [
    ("rock_fill", dict(relief=(1, 3, 26), bevel=0, dome=0, wrap=(True, True))),
    ("wall_block", dict(relief=(1, 9, 6), bevel=0, dome=0, wrap=(True, True))),
    ("island_bottom", dict(relief=(3, 10, 10), bevel=6, dome=0, wrap=(True, False))),
    ("grass_lip", dict(relief=(2, 6, 3), bevel=5, dome=10, wrap=(True, False))),
    ("deck_top", dict(relief=(3, 10, 5), bevel=7, dome=0, wrap=(True, False))),
    ("wall_cap", dict(relief=(3, 10, 5), bevel=7, dome=0, wrap=(True, False))),
    ("_cap_", dict(relief=(3, 10, 5), bevel=7, dome=0, wrap=(False, False))),
    ("column_shaft", dict(relief=(2, 9, 0), bevel=0, dome=0, wrap=(False, True), lathe=True)),
    ("column_base", dict(relief=(2, 9, 0), bevel=4, dome=0, wrap=(False, False), lathe=True)),
    ("column_capital", dict(relief=(4, 12, 0), bevel=4, dome=0, wrap=(False, False), lathe=True)),
    ("broken_column", dict(relief=(2, 9, 0), bevel=4, dome=0, wrap=(False, False), lathe=True)),
    ("grass_tuft", dict(relief=(1, 3, 0), bevel=3, dome=0, wrap=(True, False))),
    ("hedge", dict(relief=(2, 4, 0), bevel=6, dome=26, wrap=(True, False))),
    ("flower_bush", dict(relief=(2, 4, 0), bevel=6, dome=26, wrap=(False, False))),
    ("blossom_tree", dict(relief=(2, 4, 0), bevel=6, dome=40, wrap=(False, False))),
    ("lily", dict(relief=(1, 4, 0), bevel=3, dome=0, wrap=(False, False))),
    ("fern", dict(relief=(1, 4, 0), bevel=3, dome=0, wrap=(False, False))),
    ("ivy", dict(relief=(2, 4, 0), bevel=4, dome=9, wrap=(False, False))),
    ("vine", dict(relief=(2, 4, 0), bevel=4, dome=5, wrap=(False, False))),
    ("wisteria", dict(relief=(2, 4, 0), bevel=4, dome=9, wrap=(False, False))),
]


def preset(name: str) -> dict:
    for key, value in PRESETS:
        if key in name:
            return value
    return STONE


def pad(img: np.ndarray, n: int, wrap) -> np.ndarray:
    """Extends an image by n on every side: around the tiling sides, by its edge on the others."""
    widths = [(n, n), (0, 0)] + [(0, 0)] * (img.ndim - 2)
    img = np.pad(img, widths, mode="wrap" if wrap[1] else "edge")
    widths = [(0, 0), (n, n)] + [(0, 0)] * (img.ndim - 2)
    return np.pad(img, widths, mode="wrap" if wrap[0] else "edge")


def blur(img: np.ndarray, sigma: float, wrap) -> np.ndarray:
    n = int(sigma * 3) + 2
    return cv2.GaussianBlur(pad(img, n, wrap), (0, 0), sigma)[n:-n, n:-n]


def gradients(height: np.ndarray, wrap):
    """The slope of a height field along x and along the image's rows."""
    padded = pad(height, 1, wrap)
    dx = (padded[1:-1, 2:] - padded[1:-1, :-2]) * 0.5
    dy = (padded[2:, 1:-1] - padded[:-2, 1:-1]) * 0.5
    return dx, dy


def luma_filled(rgb: np.ndarray, alpha: np.ndarray, wrap) -> np.ndarray:
    """Brightness, with the holes filled from nearby paint so the silhouette makes no false relief."""
    luma = rgb[..., 0] * 0.3 + rgb[..., 1] * 0.59 + rgb[..., 2] * 0.11
    weight = blur(alpha, 6.0, wrap) + 1e-4
    around = blur(luma * alpha, 6.0, wrap) / weight
    return luma * alpha + around * (1.0 - alpha)


def bevel_height(alpha: np.ndarray, radius: float, wrap) -> np.ndarray:
    """Height rising inward from the silhouette in a quarter circle of that radius."""
    n = int(radius) + 2
    solid = (alpha > 0.5).astype(np.uint8)
    # Past a tiling side the shape continues; past any other side there is nothing.
    padded = np.pad(solid, [(n, n), (0, 0)], mode="wrap" if wrap[1] else "constant")
    padded = np.pad(padded, [(0, 0), (n, n)], mode="wrap" if wrap[0] else "constant")
    distance = cv2.distanceTransform(padded, cv2.DIST_L2, 5)[n:-n, n:-n]
    t = np.clip(distance / radius, 0.0, 1.0)
    return radius * np.sqrt(np.clip(1.0 - (1.0 - t) ** 2, 0.0, 1.0))


def lathe_normals(alpha: np.ndarray):
    """Each row a cylinder between its edges: x of the normal runs -1..1 across the row."""
    h, w = alpha.shape
    nx = np.zeros_like(alpha)
    left = np.full(h, -1.0)
    right = np.full(h, -1.0)
    for y in range(h):
        solid = np.flatnonzero(alpha[y] > 0.5)
        if solid.size > 1:
            left[y], right[y] = solid[0], solid[-1]
    known = np.flatnonzero(left >= 0)
    if known.size == 0:
        return nx
    left = np.interp(np.arange(h), known, left[known])
    right = np.interp(np.arange(h), known, right[known])
    # Steadied down the column, so a chip in the silhouette does not bend the whole row.
    k = max(3, h // 40) | 1
    left = cv2.GaussianBlur(left.reshape(-1, 1), (1, k), 0).ravel()
    right = cv2.GaussianBlur(right.reshape(-1, 1), (1, k), 0).ravel()
    xs = np.arange(w, dtype=np.float32)
    for y in range(h):
        centre = (left[y] + right[y]) * 0.5
        half = max(1.0, (right[y] - left[y]) * 0.5)
        nx[y] = np.clip((xs - centre) / half, -1.0, 1.0)
    return nx * 0.92


def normal_map(rgba: np.ndarray, p: dict) -> np.ndarray:
    wrap = p["wrap"]
    rgb = rgba[..., :3].astype(np.float32) / 255.0
    alpha = rgba[..., 3].astype(np.float32) / 255.0
    luma = luma_filled(rgb, alpha, wrap)
    # Three bands between four blurs: nothing broader than the last, which is painted light.
    blurs = [luma] + [blur(luma, s, wrap) for s in (1.2, 4.0, 14.0)]
    fine, mid, broad = p["relief"]
    height = (
        fine * (blurs[0] - blurs[1])
        + mid * (blurs[1] - blurs[2]) * 2.0
        + broad * (blurs[2] - blurs[3]) * 3.0
    )
    if p["bevel"] > 0:
        height = height + bevel_height(alpha, p["bevel"], wrap)
    if p["dome"] > 0:
        height = height + p["dome"] * 2.2 * blur(alpha, p["dome"], wrap)
    height = blur(height, 1.0, wrap)
    dx, dy = gradients(height, wrap)
    # A surface faces against its slope; rows run down the image while Unity's y runs up.
    nx, ny, nz = -dx, dy, np.ones_like(dx)
    if p.get("lathe"):
        # Laid over the cylinder, keeping the carving on top of the curve.
        cx = lathe_normals(alpha)
        cz = np.sqrt(np.clip(1.0 - cx * cx, 0.02, 1.0))
        nx, nz = nx + cx / cz, nz
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.dstack((nx / length, ny / length, nz / length))
    return (np.clip(normal * 0.5 + 0.5, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def main() -> None:
    only = sys.argv[1] if len(sys.argv) > 1 else None
    os.makedirs(OUT, exist_ok=True)
    paths = []
    for folder in SOURCES:
        paths += glob.glob(os.path.join(folder, "**", "*.png"), recursive=True)
    for path in sorted(paths):
        name = os.path.splitext(os.path.basename(path))[0]
        if any(s in name for s in SKIP) or (only and only not in name):
            continue
        img = Image.open(path).convert("RGBA")
        size = (max(4, round(img.width * SCALE)), max(4, round(img.height * SCALE)))
        small = np.asarray(img.resize(size, Image.LANCZOS))
        out = normal_map(small, preset(name))
        Image.fromarray(out, "RGB").save(os.path.join(OUT, f"{name}_n.png"), optimize=True)
        print(f"{name}: {size[0]}x{size[1]}")


if __name__ == "__main__":
    main()
