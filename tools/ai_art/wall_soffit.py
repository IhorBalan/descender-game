"""The underside of a built terrace: a moulded fascia with a row of dentils, from the coping's marble.

Where a garden terrace overhangs (the ceiling of the root hollow under Mereth's grove), its underside
had the floating islands' rock fringe, so cut marble ended in rock teeth. A mason finishes the edge
of a slab: a plain fascia, a bead and the gold line, and a row of dentils under them. This builds
that band out of the coping painting itself, so the stone matches the wall it hangs from: its plain
lower marble for the fascia and the dentils, its gold line, all shaded as stone that faces down, out
of the sun. Tileable sideways. Written to Kit/Architecture/wall_soffit.png, drawn as it is seen
(the slab's edge at the top); ParadiseGarden derives the flipped edge the SpriteShape takes.

  python tools/ai_art/wall_soffit.py
"""
import os

import numpy as np
from PIL import Image

FOLDER = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Kit", "Architecture")
SOURCE = os.path.join(FOLDER, "wall_cap.png")
OUT = os.path.join(FOLDER, "wall_soffit.png")

# The coping's plain marble fascia, and its gold line, in the painting's rows.
FASCIA = (142, 190)
GOLD = (119, 123)
# Rows of fascia hidden under the slab's edge (the edge's pivot), in pixels at 128 a unit.
TUCK = 10
# A dentil, the gap after it (their sum divides the width, so the band tiles), and its height.
DENTIL = 20
GAP = 12
DENTIL_HEIGHT = 18
# Facing down, out of the sun: the fascia a little darker than the coping, darker still at its foot.
SHADE = 0.86


def main() -> None:
    src = np.asarray(Image.open(SOURCE).convert("RGBA"), dtype=np.float32) / 255.0
    width = src.shape[1]
    assert width % (DENTIL + GAP) == 0, width
    fascia = src[FASCIA[0] : FASCIA[1], :, :3]
    stone = fascia.reshape(-1, 3).mean(axis=0)
    # The marble's own grain (its brightness about its mean), carried into the plain bands.
    grain = fascia.mean(axis=2) / fascia.mean()
    gold = src[GOLD[0] : GOLD[1], :, :3].mean(axis=0)
    rows = []

    def plain(height: int, shade: float) -> None:
        for _ in range(height):
            g = grain[len(rows) % grain.shape[0]][:, None]
            rows.append(stone[None, :] * shade * g)

    for y in range(TUCK):
        rows.append(fascia[y] * SHADE)
    for y in range(fascia.shape[0]):
        rows.append(fascia[y] * SHADE * (1.0 - 0.1 * (y / (fascia.shape[0] - 1)) ** 1.5))
    plain(2, 0.5)  # the groove under the fascia
    plain(4, 1.02)  # the bead, catching the light thrown up from the clouds
    for y in range(GOLD[1] - GOLD[0]):
        rows.append(gold * (0.9 + 0.1 * grain[y][:, None]))
    plain(4, 0.78)  # the moulding under the gold, turning away
    plain(2, 0.48)  # its shadow
    top = len(rows)
    out = np.zeros((top + DENTIL_HEIGHT, width, 4), np.float32)
    out[:top, :, :3] = np.stack(rows)
    out[:top, :, 3] = 1.0
    # The dentils: blocks standing out of the shadowed fascia behind them, lit along their left
    # edge (under a slab the light comes back from the clouds, away from the sun), shaded along
    # their right and their foot. The gaps show the fascia behind, then open onto air.
    x = np.arange(width) % (DENTIL + GAP)
    block = x < DENTIL
    across = np.where(x < 2, 1.08, np.where(x >= DENTIL - 3, 0.72, 0.9))
    for y in range(DENTIL_HEIGHT):
        g = grain[(top + y) % grain.shape[0]]
        foot = 0.74 if y >= DENTIL_HEIGHT - 3 else 1.0
        face = stone[None, :] * (across * foot * g * SHADE)[:, None]
        behind = stone[None, :] * (0.42 * g)[:, None]
        out[top + y, :, :3] = np.where(block[:, None], face, behind)
        out[top + y, :, 3] = np.where(block | (y < DENTIL_HEIGHT - 6), 1.0, 0.0)
    out[-1, :, 3] *= 0.6  # a soft last row, so the foot does not stair-step
    Image.fromarray((np.clip(out, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGBA").save(OUT, optimize=True)
    print(f"wall soffit {width}x{out.shape[0]}, slab edge {TUCK} px down")


if __name__ == "__main__":
    main()
