"""The orchard's fruit glows: an emissive layer for the blossom tree, a soft light on each golden fruit.

The Orchard is lit by the orchard itself (docs/design/paradise.md, zone 2): its golden fruit
gives off a little light of its own. The fruit is found in the tree's painting by its colour
(saturated gold, opaque), and each gets a small bright core and a wide soft halo, warm gold,
on black, which adds nothing. ParadiseGarden.Place lays any `<name>_glow` sprite over the piece
with the additive glow material, so every orchard tree picks this up.

  python tools/ai_art/fruit_glow.py
"""
import os

import numpy as np
from PIL import Image

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

NATURE = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Kit", "Nature")
TREE = os.path.join(NATURE, "blossom_tree.png")
GLOW = os.path.join(NATURE, "blossom_tree_glow.png")

GOLD = np.array([1.0, 0.74, 0.32], dtype=np.float32)
# A fruit is a disc of saturated gold at least this many pixels in the painting.
SMALLEST_FRUIT = 120
CORE = 16.0
HALO = 70.0
# At the bloom threshold where the fruit is (the glow material doubles it): a warmth, not a lamp.
CORE_LIGHT = 0.4
HALO_LIGHT = 0.2


def fruits(rgba: np.ndarray) -> list:
    hsv = cv2.cvtColor(rgba[..., :3], cv2.COLOR_RGB2HSV_FULL)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    gold = ((h > 18) & (h < 45) & (s > 110) & (v > 150) & (rgba[..., 3] > 200)).astype(np.uint8)
    gold = cv2.morphologyEx(gold, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    count, _, stats, centres = cv2.connectedComponentsWithStats(gold)
    return [(float(centres[i][0]), float(centres[i][1])) for i in range(1, count) if stats[i, 4] >= SMALLEST_FRUIT]


def main() -> None:
    rgba = np.asarray(Image.open(TREE).convert("RGBA"))
    h, w = rgba.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    light = np.zeros((h, w), np.float32)
    found = fruits(rgba)
    for x, y in found:
        d2 = (xs - x) ** 2 + (ys - y) ** 2
        light = np.maximum(light, CORE_LIGHT * np.exp(-d2 / (2 * CORE * CORE)) + HALO_LIGHT * np.exp(-d2 / (2 * HALO * HALO)))
    light = np.clip(light, 0.0, 1.0)
    out = np.dstack((GOLD[None, None, :] * light[..., None], light))
    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), "RGBA").save(GLOW, optimize=True)
    print(f"fruit glow: {len(found)} fruit")


if __name__ == "__main__":
    main()
