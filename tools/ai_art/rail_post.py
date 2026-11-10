"""The end post of a balustrade, made from the balustrade's own marble.

A balustrade never just stops: it ends in a solid square post. The kit has none, so this builds
one out of the balustrade painting itself, so the stone matches exactly: its plinth for the base,
the same marble drawn up into a square shaft with its edges turned from the light, and the plinth
again, turned over, as the cap. Written to Kit/Architecture/balustrade_post.png; run normals.py
for its normal map.

  python tools/ai_art/rail_post.py
"""
import os

import numpy as np
from PIL import Image

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

FOLDER = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Kit", "Architecture")
SOURCE = os.path.join(FOLDER, "balustrade.png")
OUT = os.path.join(FOLDER, "balustrade_post.png")

# The balustrade's plinth: the clean marble band along its foot, in the painting's rows.
PLINTH = (134, 154)
# Where along the painting the marble is taken from (a stretch with no ivy on the plinth).
TAKE = (200, 300)
SHAFT_WIDTH = 64
BASE_WIDTH = 80
CAP_WIDTH = 84
HEIGHT = 166


def resized(img: np.ndarray, w: int, h: int) -> np.ndarray:
    return cv2.resize(img, (w, h), interpolation=cv2.INTER_AREA if w < img.shape[1] else cv2.INTER_CUBIC)


def main() -> None:
    src = np.asarray(Image.open(SOURCE).convert("RGBA"), dtype=np.float32) / 255.0
    plinth = src[PLINTH[0] : PLINTH[1], TAKE[0] : TAKE[1]]
    base_h = PLINTH[1] - PLINTH[0]
    cap_h = 20
    shaft_h = HEIGHT - base_h - cap_h
    out = np.zeros((HEIGHT, CAP_WIDTH, 4), np.float32)

    def place(img: np.ndarray, top: int) -> None:
        h, w = img.shape[:2]
        left = (CAP_WIDTH - w) // 2
        a = img[..., 3:]
        under = out[top : top + h, left : left + w]
        under[..., :3] = img[..., :3] * a + under[..., :3] * (1.0 - a)
        under[..., 3:] = a + under[..., 3:] * (1.0 - a)

    # The shaft: the same marble drawn up, its sides turning away from the light (the sun is
    # high on the right, so the left edge is the darker), a soft shade under the cap.
    # Turned on its side, so the marble's veins run up the post rather than smearing across it.
    shaft = resized(np.transpose(plinth, (1, 0, 2)), SHAFT_WIDTH, shaft_h).copy()
    shaft[..., 3] = 1.0
    across = np.linspace(-1.0, 1.0, SHAFT_WIDTH)[None, :, None]
    shade = 1.0 - 0.16 * np.clip(-across - 0.55, 0.0, 1.0) / 0.45 - 0.07 * np.clip(across - 0.75, 0.0, 1.0) / 0.25
    down = np.linspace(0.0, 1.0, shaft_h)[:, None, None]
    shade = shade * (1.0 - 0.12 * np.exp(-down / 0.08))
    shaft[..., :3] *= shade
    place(shaft, cap_h)
    base = resized(plinth, BASE_WIDTH, base_h).copy()
    base[..., 3] = 1.0
    place(base, HEIGHT - base_h)
    cap = resized(plinth[::-1], CAP_WIDTH, cap_h).copy()
    cap[..., 3] = 1.0
    cap[:2, :, :3] = np.minimum(1.0, cap[:2, :, :3] * 1.12)
    place(cap, 0)

    Image.fromarray((np.clip(out, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGBA").save(OUT, optimize=True)
    print(f"balustrade post {CAP_WIDTH}x{HEIGHT}")


if __name__ == "__main__":
    main()
