"""A blind arch set into a garden wall, for a lantern to hang in: the wall's own arch, in miniature.

The long terrace walls under the walk are a plane of ashlar. A mason breaks such a wall with niches,
and a lantern hung in one lights its recess. This builds the niche out of the kit itself, so it is
the same stone: the voussoirs and gold keystone of the wide arch (arch_span_wide), scaled down; jambs
and imposts of the coping's marble; and, behind, the wall's own blocks set back into shade, darkest
up under the arch where the recess is deepest. The lantern's light (ParadiseGarden.LanternLight)
lights the recess in the game. Written to Kit/Architecture/wall_niche.png, its pivot at the top of
its keystone, where it meets the coping.

  python tools/ai_art/wall_niche.py
"""
import os

import numpy as np
from PIL import Image

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

FOLDER = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Kit", "Architecture")
ARCH = os.path.join(FOLDER, "arch_span_wide.png")
WALL = os.path.join(FOLDER, "wall_block.png")
CAP = os.path.join(FOLDER, "wall_cap.png")
OUT = os.path.join(FOLDER, "wall_niche.png")

# The wide arch's ring, in its own pixels: its centre on the springing line, the intrados and the
# extrados (the line outside the voussoirs), and how far the keystone stands above the extrados.
CENTRE = (743, 768)
INNER = 538
OUTER = 637
KEYSTONE = 12
# The niche's scale against the wide arch, the height of its jambs below the springing, its sill.
SCALE = 0.42
JAMBS = 280
SILL = 22
# The coping's plain marble and its moulding, in the painting's rows (as wall_soffit.py takes them).
FASCIA = (142, 190)
MOULDING = (110, 134)


def resized(img: np.ndarray, w: int, h: int) -> np.ndarray:
    return cv2.resize(img, (w, h), interpolation=cv2.INTER_AREA if w < img.shape[1] else cv2.INTER_CUBIC)


def over(out: np.ndarray, img: np.ndarray, x: int, y: int) -> None:
    h, w = img.shape[:2]
    a = img[..., 3:]
    under = out[y : y + h, x : x + w]
    under[..., :3] = img[..., :3] * a + under[..., :3] * (1.0 - a)
    under[..., 3:] = a + under[..., 3:] * (1.0 - a)


def bare(img: np.ndarray) -> np.ndarray:
    """The coping's marble without the ivy strands that cross it: green turned back to stone."""
    out = img.copy()
    rgb = out[..., :3]
    green = rgb[..., 1] - (rgb[..., 0] + rgb[..., 2]) / 2.0 > 0.03
    stone = rgb[~green].mean(axis=0)
    lum = rgb.mean(axis=2, keepdims=True) / stone.mean()
    rgb[green] = (stone[None, :] * np.clip(lum[green], 0.85, 1.1))
    return out


def main() -> None:
    arch = np.asarray(Image.open(ARCH).convert("RGBA"), dtype=np.float32) / 255.0
    wall = np.asarray(Image.open(WALL).convert("RGBA"), dtype=np.float32) / 255.0
    cap = np.asarray(Image.open(CAP).convert("RGBA"), dtype=np.float32) / 255.0

    # The ring alone: everything between the intrados and the extrados, the keystone with it.
    cx, cy = CENTRE
    reach = OUTER + KEYSTONE + 4
    ring = arch[cy - reach : cy, cx - reach : cx + reach].copy()
    ys, xs = np.mgrid[0 : ring.shape[0], 0 : ring.shape[1]].astype(np.float32)
    r = np.hypot(xs - reach, ys - reach)
    gold = (ring[..., 0] - ring[..., 2]) > 0.2
    keep = np.clip(r - INNER + 1.0, 0.0, 1.0) * np.clip(OUTER + 1.5 - r, 0.0, 1.0)
    keep = np.maximum(keep, (gold & (r >= INNER - 1) & (r <= OUTER + KEYSTONE)).astype(np.float32))
    ring[..., 3] *= keep
    ring = resized(ring, round(ring.shape[1] * SCALE), round(ring.shape[0] * SCALE))
    inner, outer = INNER * SCALE, OUTER * SCALE
    band = outer - inner

    width = ring.shape[1] + 10
    height = ring.shape[0] + JAMBS + SILL
    mid = width / 2.0
    spring = ring.shape[0]
    out = np.zeros((height, width, 4), np.float32)

    # The recess: the wall's blocks, set back into the shade, darkest up under the arch and along
    # the jambs, where the recess is deepest; a little light reaches its foot.
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float32)
    above = ys < spring
    r = np.hypot(xs - mid, ys - spring)
    opening = np.where(above, r < inner + 1.0, np.abs(xs - mid) < inner + 1.0) & (ys < height - SILL)
    back = wall[:height, :width, :3]
    # How far in from the arch and the jambs, and from the right jamb alone (the sun is on the
    # right: that side of the recess is in the shadow of its own jamb).
    edge = np.where(above, inner - r, inner - np.abs(xs - mid))
    from_right = np.where(above, inner - r, mid + inner - xs)
    down = np.clip((ys - spring * 0.2) / (height - SILL - spring * 0.2), 0.0, 1.0)
    shade = 0.3 + 0.3 * down**0.8
    shade *= 1.0 - 0.3 * np.exp(-np.clip(edge, 0.0, None) / 22.0)
    shade *= 1.0 - 0.25 * np.exp(-np.clip(from_right, 0.0, None) / 40.0)
    # Set back, the blocks are a little cooler than the sunlit face around them.
    out[..., :3] = back * shade[..., None] * np.array([0.94, 0.97, 1.03], np.float32)
    out[..., 3] = opening.astype(np.float32)

    # The jambs and their imposts: the coping's marble turned on its side, the moulding as the
    # impost the arch springs from.
    cap = bare(cap)
    fascia = cap[FASCIA[0] : FASCIA[1], :, :].copy()
    fascia[..., 3] = 1.0
    jamb = resized(np.transpose(fascia, (1, 0, 2)), round(band), JAMBS)
    across = np.linspace(-1.0, 1.0, jamb.shape[1])[None, :, None]
    jamb[..., :3] *= 1.0 - 0.12 * np.clip(-across, 0.0, 1.0)
    left = round(mid - outer)
    right = round(mid + inner)
    over(out, jamb, left, spring)
    over(out, jamb[:, ::-1], right, spring)
    moulding = cap[MOULDING[0] : MOULDING[1], :, :].copy()
    moulding[..., 3] = 1.0
    impost = resized(moulding, round(band) + 10, 14)
    over(out, impost, left - 5, spring - 4)
    over(out, impost, right - 5, spring - 4)
    over(out, ring, (width - ring.shape[1]) // 2, 0)

    # The sill the jambs stand on: the moulding again, turned over, a touch wider than the niche.
    sill = resized(moulding[::-1], round(2 * outer) + 8, SILL)
    over(out, sill, round(mid - outer) - 4, height - SILL)
    Image.fromarray((np.clip(out, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGBA").save(OUT, optimize=True)
    print(f"wall niche {width}x{height}, opening {2 * inner:.0f} px, springing {spring} px down")


if __name__ == "__main__":
    main()
