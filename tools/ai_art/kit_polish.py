"""Finishes the kit pieces whose cut-outs give them away. Run after harmonize.py, before
normals.py. Works on Assets/Art/Paradise directly and changes only what is still unfinished,
so running it twice is safe.

- plants cut off by the side of their image taper to a point, as a frond would;
- hanging plants lose the flat row they were cropped along, for a ragged leafy top;
- wisteria loses the pale band painted over its top, so the leaves are its top edge;
- the hedge's enclosed gaps are sealed, so the pale stone behind does not show as stars;
- the bounce pod is recoloured from mint to the garden's cream and gold, its glow calmed.

  python tools/ai_art/kit_polish.py
"""
import os

import numpy as np
from PIL import Image

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise")
NATURE = os.path.join(ROOT, "Kit", "Nature")
POD = os.path.join(ROOT, "Z2_Orchard", "Props", "z2_prop_bounce_pod.png")
POD_GLOW = os.path.join(ROOT, "Z2_Orchard", "Props", "z2_prop_bounce_pod_glow.png")

TAPER_MID = ("fern", "flower_bush")
TAPER_BOTTOM = ("hedge_flowers",)
TAPER_TOP = ("ivy_drape_a", "ivy_drape_b", "ivy_drape_c", "ivy_curtain", "wisteria_drape")
RAGGED = ("ivy_drape_a", "ivy_drape_b", "ivy_drape_c", "ivy_curtain", "vine_hanging", "wisteria_drape")


def load(path: str) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)


def save(path: str, rgba: np.ndarray) -> None:
    Image.fromarray(np.clip(rgba + 0.5, 0, 255).astype(np.uint8), "RGBA").save(path, optimize=True)


def smooth(values: np.ndarray, size: int) -> np.ndarray:
    return cv2.GaussianBlur(values.reshape(-1, 1).astype(np.float32), (1, size | 1), 0).ravel()


def taper_side(alpha: np.ndarray, from_left: bool, reach: int, anchor: str) -> bool:
    """Narrows what reaches an image side to a point at that side; False if nothing reaches it."""
    edge = alpha[:, 0] if from_left else alpha[:, -1]
    if edge.max() < 26:
        return False
    w = alpha.shape[1]
    columns = [i if from_left else w - 1 - i for i in range(reach)]
    first, last = np.zeros(reach), np.zeros(reach)
    for i, x in enumerate(columns):
        solid = np.flatnonzero(alpha[:, x] > 26)
        first[i], last[i] = (solid[0], solid[-1]) if solid.size else (0, 0)
    first, last = smooth(first, 9), smooth(last, 9)
    ys = np.arange(alpha.shape[0], dtype=np.float32)
    for i, x in enumerate(columns):
        # 0 at the side, 1 where the taper starts: a blade's curve, or a bush's round end.
        t = i / reach
        keep = np.sqrt(1.0 - (1.0 - t) ** 2) if anchor == "bottom" else t**0.7
        centre = {"top": first[i], "bottom": last[i]}.get(anchor, (first[i] + last[i]) * 0.5)
        half = (last[i] - first[i]) * (keep if anchor != "mid" else keep * 0.5)
        reach_y = np.abs(ys - centre)
        alpha[:, x] *= np.clip((half - reach_y) / 2.5, 0.0, 1.0)
    return True


def ragged_top(alpha: np.ndarray, seed: int) -> bool:
    """Replaces a flat cropped top with an outline of leaf clumps; False if the top is not flat."""
    if (alpha[0] > 200).mean() < 0.02:
        return False
    h, w = alpha.shape
    rng = np.random.default_rng(seed)
    depth = min(30.0, h * 0.09)
    outline = np.full(w, depth, dtype=np.float32)
    x = -20.0
    while x < w + 20:
        r = rng.uniform(depth * 0.7, depth * 1.5)
        lift = rng.uniform(0.0, depth * 0.5)
        span = np.arange(w, dtype=np.float32) - x
        dome = depth + lift - r - np.sqrt(np.clip(r * r - span * span, 0.0, None))
        inside = np.abs(span) < r
        outline[inside] = np.minimum(outline[inside], np.maximum(dome[inside], 0.0))
        x += rng.uniform(r * 0.8, r * 1.6)
    ys = np.arange(h, dtype=np.float32).reshape(-1, 1)
    alpha *= np.clip((ys - outline) / 2.0, 0.0, 1.0)
    return True


def strip_pale_band(rgba: np.ndarray, rows: int) -> None:
    """Clears the low-saturation paint over the top rows, leaving the leaves as the edge."""
    rgb = rgba[:rows, :, :3]
    top = rgb.max(axis=2)
    saturation = (top - rgb.min(axis=2)) / (top + 1.0)
    keep = cv2.GaussianBlur(np.clip((saturation - 0.24) * 14.0, 0.0, 1.0), (0, 0), 1.2)
    # Fades back in below, so the flowers further down are not touched.
    fade = np.linspace(0.0, 1.0, rows).reshape(-1, 1) ** 6
    rgba[:rows, :, 3] *= np.clip(keep + fade, 0.0, 1.0)


def polish_plants() -> None:
    for i, name in enumerate(sorted(set(TAPER_MID + TAPER_TOP + TAPER_BOTTOM + RAGGED))):
        path = os.path.join(NATURE, name + ".png")
        if not os.path.exists(path):
            continue
        rgba = load(path)
        alpha = rgba[..., 3]
        changed = False
        if name == "wisteria_drape" and (alpha[8:40] > 128).mean() > 0.6:
            strip_pale_band(rgba, 92)
            changed = True
        anchor = "top" if name in TAPER_TOP else "bottom" if name in TAPER_BOTTOM else "mid"
        if name not in RAGGED or name in TAPER_TOP:
            reach = 110 if anchor == "bottom" else min(alpha.shape[1] // 5, 56)
            for from_left in (True, False):
                changed |= taper_side(alpha, from_left, reach, anchor)
        if name in RAGGED:
            changed |= ragged_top(alpha, 17 + i)
        if changed:
            save(path, rgba)
            print(f"polished {name}")


def close_holes() -> None:
    """Seals the gaps a hedge's leaves were cut around, which read as white stars against pale stone."""
    path = os.path.join(NATURE, "hedge_flowers.png")
    rgba = load(path)
    solid = (rgba[..., 3] > 128).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(1 - solid, connectivity=4)
    h, w = solid.shape
    holes = np.zeros((h, w), np.uint8)
    for i in range(1, count):
        x, y, cw, ch, _ = stats[i]
        if x > 0 and y > 0 and x + cw < w and y + ch < h:
            holes[labels == i] = 1
    if not holes.any():
        return
    grown = cv2.dilate(holes, np.ones((3, 3), np.uint8)) > 0
    filled = cv2.inpaint(rgba[..., :3].astype(np.uint8), grown.astype(np.uint8), 5, cv2.INPAINT_TELEA)
    # Painted as the shade between leaves, the darkest thing in a gap.
    rgba[..., :3][grown] = filled[grown].astype(np.float32) * 0.55
    rgba[..., 3][grown] = 255.0
    save(path, rgba)
    print("closed the hedge's holes")


def recolour_pod() -> None:
    rgba = load(POD)
    rgb = rgba[..., :3].astype(np.uint8)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV_FULL).astype(np.float32)
    # Only the mint is moved: already warm means this has run.
    solid = rgba[..., 3] > 128
    if np.median(hsv[..., 0][solid]) < 90:
        return
    h, w = rgb.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    head = np.hypot(xs - w * 0.5, ys - h * 0.457)
    orb = head < w * 0.105
    stem = (ys > h * 0.54) & (hsv[..., 2] < 170) & (hsv[..., 1] > 90) & ~orb
    hue = np.full((h, w), 30.0, dtype=np.float32)  # cream-gold, of 256
    saturation = hsv[..., 1] * 0.42
    value = hsv[..., 2]
    hue[orb] = 24.0
    saturation[orb] = np.clip(hsv[..., 1][orb] * 1.1, 0, 170)
    value[orb] = value[orb] * 0.88
    hue[stem] = 62.0
    saturation[stem] = hsv[..., 1][stem] * 0.55
    moved = np.dstack((hue, saturation, value)).astype(np.uint8)
    rgba[..., :3] = cv2.cvtColor(moved, cv2.COLOR_HSV2RGB_FULL).astype(np.float32)
    save(POD, rgba)

    glow = load(POD_GLOW)
    # Additive: dimmer colour is a dimmer glow. The solid disc at its heart was the blown-out core.
    centre = np.hypot(xs - w * 0.5, ys - h * 0.457)
    calm = 0.34 - 0.27 * np.clip(1.0 - (centre - w * 0.09) / (w * 0.03), 0.0, 1.0)
    tint = np.array([1.0, 0.92, 0.78], dtype=np.float32)
    luma = glow[..., :3].mean(axis=2, keepdims=True)
    glow[..., :3] = luma * tint * calm[..., None]
    save(POD_GLOW, glow)
    print("recoloured the bounce pod")


if __name__ == "__main__":
    polish_plants()
    close_holes()
    recolour_pod()
