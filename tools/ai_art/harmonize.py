"""Harmonize the painted kit so its pieces read as one painting, not a collage of stickers.

Every kit piece was generated on its own, so each has its own outline, detail and colour.
This pass, run after `scenario_gen.py apply`, reads each piece from out/ (the fitted
generation) and writes the harmonized version into Assets/Art/Paradise/Kit:

- edge-preserving smoothing: keeps the forms, drops the busy cartoon texture;
- outline softening: the dark rim the generator draws around every cut-out is blended
  into the piece's own colour, so it sits in the scene instead of on it;
- one palette: plants brought toward the marble (greens less saturated and a touch
  cooler), marble a little warmer, nothing more saturated than the key art;
- plants fade out at their foot, so they grow out of the grass lip instead of standing on
  a cut;
- the two big fills (wall blocks, rock) calmed: their mortar and cracks lighter and their
  local contrast lower, so a wall is a surface behind the walk, not a pattern.

  python tools/ai_art/harmonize.py            every kit piece
  python tools/ai_art/harmonize.py fern       only pieces whose name contains "fern"
"""
import json
import os
import sys

import numpy as np

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

HERE = os.path.dirname(__file__)
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "out")
MANIFEST = os.path.join(ROOT, "Assets", "Art", "Paradise", "manifest.json")
PREFIX = "Assets/Art/Paradise/"

# Plants that stand on the walk: their foot fades into the grass.
GROUND_PLANTS = ("fern", "flower_bush", "lily_cluster", "hedge_flowers", "blossom_tree")
FILLS = ("wall_block", "rock_fill")
SKIP = ("_glow", "fg_leaves", "waterfall", "mist")


def load(path: str) -> np.ndarray:
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img.ndim == 2:
        img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGRA)
    if img.shape[2] == 3:
        img = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
    return img


def bleed(bgr: np.ndarray, alpha: np.ndarray) -> np.ndarray:
    """Colour pushed out into the transparent pixels, so filters do not pull black in at edges."""
    mask = (alpha > 8).astype(np.float32)
    out = bgr.astype(np.float32)
    acc = out * mask[..., None]
    weight = mask.copy()
    for size in (5, 15, 41):
        b = cv2.blur(acc, (size, size))
        w = cv2.blur(weight, (size, size))
        fill = (weight < 0.5) & (w > 1e-4)
        acc[fill] = b[fill] / w[fill][..., None]
        weight[fill] = 1.0
    return np.clip(acc, 0, 255).astype(np.uint8)


def smooth(bgr: np.ndarray, strength: float, wrap: bool) -> np.ndarray:
    """Edge-preserving smoothing: flat areas lose their noise, forms keep their edges."""
    if strength <= 0:
        return bgr
    pad = 32 if wrap else 0
    src = cv2.copyMakeBorder(bgr, pad, pad, pad, pad, cv2.BORDER_WRAP) if wrap else bgr
    sm = cv2.edgePreservingFilter(src, flags=cv2.RECURS_FILTER, sigma_s=12 + 30 * strength, sigma_r=0.12 + 0.2 * strength)
    if wrap:
        sm = sm[pad:-pad, pad:-pad]
    return cv2.addWeighted(sm, strength, bgr, 1 - strength, 0)


def soften_outline(bgr: np.ndarray, alpha: np.ndarray, width: int, amount: float) -> np.ndarray:
    """The dark rim inside a cut-out's edge, blended toward the colour just inside it."""
    solid = (alpha > 200).astype(np.uint8)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * width + 1, 2 * width + 1))
    inner = cv2.erode(solid, kernel)
    band = (solid > 0) & (inner == 0)
    if not band.any():
        return bgr
    f = bgr.astype(np.float32)
    m = inner.astype(np.float32)
    k = 4 * width + 1
    interior = cv2.blur(f * m[..., None], (k, k)) / np.maximum(cv2.blur(m, (k, k)), 1e-4)[..., None]
    lum = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY).astype(np.float32)
    ilum = cv2.cvtColor(np.clip(interior, 0, 255).astype(np.uint8), cv2.COLOR_BGR2GRAY).astype(np.float32)
    # Only where the rim is darker than inside: that is the drawn outline, not the form.
    dark = np.clip((ilum - lum) / 60.0, 0, 1) * band
    t = (dark * amount)[..., None]
    out = f * (1 - t) + interior * t
    return np.clip(out, 0, 255).astype(np.uint8)


def palette(bgr: np.ndarray, plant: bool) -> np.ndarray:
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV).astype(np.float32)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    green = ((h > 25) & (h < 90)).astype(np.float32)  # yellow-green to green (OpenCV hue 0..180)
    if plant:
        # The acid yellow-greens toward a cooler, calmer leaf green, less saturated.
        h += green * np.clip((60 - h) * 0.25, -6, 6)
        s *= 1 - 0.24 * green
        v *= 1 - 0.05 * green
    else:
        # Marble and gold: the generator's cream is too yellow; toward a natural warm stone.
        yellow = ((h > 8) & (h < 38)).astype(np.float32)
        s *= 0.92 - 0.14 * yellow
    hsv = np.stack([np.clip(h, 0, 180), np.clip(s, 0, 255), np.clip(v, 0, 255)], -1).astype(np.uint8)
    return cv2.cvtColor(hsv, cv2.COLOR_HSV2BGR)


def calm_fill(bgr: np.ndarray) -> np.ndarray:
    """A wall or rock fill made quieter: mortar and cracks lighter, local contrast lower."""
    f = bgr.astype(np.float32)
    big = cv2.GaussianBlur(cv2.copyMakeBorder(f, 64, 64, 64, 64, cv2.BORDER_WRAP), (0, 0), 24)[64:-64, 64:-64]
    detail = f - big
    lum = detail.mean(axis=2, keepdims=True)
    # Dark lines (mortar, cracks) lifted most; light detail kept a little more.
    gain = np.where(lum < 0, 0.55, 0.85)
    out = big + detail * gain
    return np.clip(out, 0, 255).astype(np.uint8)


def foot_fade(alpha: np.ndarray, fraction: float) -> np.ndarray:
    rows = np.nonzero(alpha.max(axis=1) > 16)[0]
    if rows.size == 0:
        return alpha
    bottom = rows[-1]
    n = max(2, int((bottom - rows[0]) * fraction))
    ramp = np.ones(alpha.shape[0], np.float32)
    for i in range(n):
        ramp[bottom - i] = (i / n) ** 1.4
    ramp[bottom + 1 :] = 0
    return (alpha.astype(np.float32) * ramp[:, None]).astype(np.uint8)


def harmonize(entry: dict) -> bool:
    rel = entry["path"][len(PREFIX) :]
    name = os.path.basename(rel)[:-4]
    if not rel.startswith("Kit/") or any(s in name for s in SKIP):
        return False
    src = os.path.join(OUT, rel)
    if not os.path.exists(src):
        return False
    img = load(src)
    bgr, alpha = img[..., :3], img[..., 3]
    tile = entry.get("tileable") or []
    plant = rel.startswith("Kit/Nature/") and name != "rock_fill" and name != "island_bottom"
    bgr = bleed(bgr, alpha)
    if name in FILLS:
        bgr = calm_fill(bgr)
        bgr = smooth(bgr, 0.25, wrap=True)
    else:
        bgr = smooth(bgr, 0.4 if plant else 0.5, wrap=bool(tile))
        bgr = soften_outline(bgr, alpha, 3, 0.75)
    bgr = palette(bgr, plant)
    if any(name.startswith(p) for p in GROUND_PLANTS):
        alpha = foot_fade(alpha, 0.1)
    out = np.dstack([bgr, alpha])
    cv2.imwrite(os.path.join(ROOT, entry["path"]), out)
    return True


def main() -> None:
    if cv2 is None:
        raise SystemExit("OpenCV is needed to harmonize: pip install opencv-python-headless")
    only = sys.argv[1:]
    with open(MANIFEST, encoding="utf-8") as f:
        entries = json.load(f)["files"]
    done = 0
    for e in entries:
        if only and not any(o in e["path"] for o in only):
            continue
        if harmonize(e):
            done += 1
            print("harmonized", e["path"][len(PREFIX) :])
    print(f"{done} pieces")


if __name__ == "__main__":
    main()
