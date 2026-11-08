"""Cliff and wall pieces derived from the two fill paintings, so terrain stops reading as a tile.

- widen: rock_fill and wall_block at twice the width. The second half is the first mirrored
  and shifted, and the two are joined along the darkest line between them (a crack in the
  rock, the mortar in the wall), so the join reads as one more crack or joint.
- pier: the quoined pier built into a terrace wall, cut block by block out of the wall itself.
  Its courses are the wall's courses at the same height, so it bonds into the wall when the
  mesh maps it by world height (ParadiseGarden.Piers). Each block carries its soft shadow on
  the side away from the sun.

Run after harmonize.py, then normals.py for the pieces it wrote:

  python tools/ai_art/cliff_kit.py
"""
import os

import numpy as np
from PIL import Image, ImageFilter

KIT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Kit")
ROCK = os.path.join(KIT, "Nature", "rock_fill.png")
WALL = os.path.join(KIT, "Architecture", "wall_block.png")
PIER = os.path.join(KIT, "Architecture", "wall_pier.png")

# How far the join may wander either side of the half-way line, in pixels.
SEAM_REACH = 140
# The original tile's width: anything wider has been through widen() already.
TILE = 1024

PIER_WIDTH = 384
PIER_BLOCKS = (240, 312)
PIER_SHADOW = np.array([38.0, 40.0, 54.0])


def load(path: str) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)


def save(path: str, pixels: np.ndarray) -> None:
    Image.fromarray(np.clip(pixels + 0.5, 0, 255).astype(np.uint8), "RGBA").save(path, optimize=True)
    print(f"{os.path.basename(path)}: {pixels.shape[1]}x{pixels.shape[0]}")


def luma(rgba: np.ndarray) -> np.ndarray:
    return rgba[..., 0] * 0.3 + rgba[..., 1] * 0.59 + rgba[..., 2] * 0.11


def soften(field: np.ndarray, radius: float) -> np.ndarray:
    """A float field blurred; it wraps around every side, as the tiles do."""
    n = int(radius * 3) + 2
    padded = np.pad(field, n, mode="wrap")
    scaled = Image.fromarray(np.clip(padded, 0, 255).astype(np.uint8), "L")
    return np.asarray(scaled.filter(ImageFilter.GaussianBlur(radius))).astype(np.float32)[n:-n, n:-n]


def mortar_rows(pixels: np.ndarray) -> list:
    """The rows of a wall's horizontal joints: the local minima of each row's brightness."""
    rows = luma(pixels).mean(axis=1)
    threshold = rows.mean() - 22.0
    found = []
    for y, value in enumerate(rows):
        window = np.take(rows, range(y - 20, y + 21), mode="wrap")
        if value < threshold and value == window.min():
            found.append(y)
    return found


def closed_seam(cost: np.ndarray, sideways: np.ndarray, starts: int = 16) -> np.ndarray:
    """The cheapest path down the rows that ends where it began, so the tile still repeats.

    cost[y, i] is what it costs to pass column i at row y; sideways[y] is the price of each
    column moved between rows y - 1 and y.
    """
    rows, columns = cost.shape
    index = np.arange(columns)
    steps = np.abs(index[None, :] - index[:, None]).astype(np.float32)
    best, path = np.inf, None
    # Spread the starts over the row, cheapest first, so they are not all one crack.
    order = np.argsort(cost[0])
    chosen = []
    for candidate in order:
        if all(abs(candidate - c) > columns // (starts * 2) for c in chosen):
            chosen.append(candidate)
        if len(chosen) == starts:
            break
    for start in chosen:
        total = np.full(columns, 1e9, dtype=np.float32)
        total[start] = cost[0, start]
        back = np.zeros((rows, columns), dtype=np.int32)
        for y in range(1, rows):
            through = total[None, :] + steps * sideways[y]
            back[y] = through.argmin(axis=1)
            total = through[index, back[y]] + cost[y]
        closing = total + steps[start] * sideways[0]
        end = int(closing.argmin())
        if closing[end] < best:
            best = closing[end]
            found = np.zeros(rows, dtype=np.int32)
            found[-1] = end
            for y in range(rows - 1, 0, -1):
                found[y - 1] = back[y, found[y]]
            path = found
    return path


def widen(path: str, shift: int, drop: int, course_joints: bool) -> None:
    """Writes the tile back at twice its width (see the module's docstring)."""
    first = load(path)
    height, width = first.shape[:2]
    if width != TILE:
        print(f"{os.path.basename(path)}: already wide")
        return
    second = np.roll(np.roll(first[:, ::-1], shift, axis=1), drop, axis=0)
    columns = (np.arange(-SEAM_REACH, SEAM_REACH + 1)) % width
    dark_a = soften(luma(first), 1.2)[:, columns]
    dark_b = soften(luma(second), 1.2)[:, columns]
    difference = np.abs(first[:, columns, :3] - second[:, columns, :3]).mean(axis=2)
    # Dark in either painting is a place the eye accepts an edge; alike in both is a bonus.
    cost = (np.minimum(dark_a, dark_b) / 255.0) ** 2 + 0.25 * (difference / 255.0)
    if course_joints:
        # Sideways only along a horizontal joint: the seam climbs the wall course by course.
        sideways = np.full(height, 1.0, dtype=np.float32)
        for row in mortar_rows(first):
            for y in range(row - 2, row + 3):
                sideways[y % height] = 2e-5
    else:
        sideways = np.full(height, 0.06, dtype=np.float32)
    seam = closed_seam(cost, sideways) - SEAM_REACH

    # 1 where the first painting shows; each half is one tile long, starting at the seam.
    weight = np.zeros((height, width * 2), dtype=np.float32)
    for y in range(height):
        weight[y, np.arange(seam[y], seam[y] + width) % (width * 2)] = 255.0
    weight = soften(weight, 1.2)[..., None] / 255.0
    tiled_a = np.concatenate((first, first), axis=1)
    tiled_b = np.concatenate((second, second), axis=1)
    save(path, tiled_a * weight + tiled_b * (1.0 - weight))


def clear_span(band: np.ndarray, width: int, low: int, high: int) -> int:
    """Where a block of this width fits in one course: no joint across it, and little moss."""
    light = luma(band).mean(axis=0)
    moss = (band[..., 1] - band[..., 0]).mean(axis=0)
    best, where = -np.inf, low
    for x in range(low, high - width, 4):
        score = light[x:x + width].min() - 2.0 * moss[x:x + width].mean()
        if score > best:
            best, where = score, x
    return where


def rounded(width: int, height: int, radius: int) -> np.ndarray:
    """A block's coverage: square along the joints, its sides' corners rounded off."""
    alpha = np.ones((height, width), dtype=np.float32)
    for y in range(height):
        inset = max(0.0, radius - min(y, height - 1 - y))
        if inset > 0.0:
            cut = radius - np.sqrt(max(0.0, radius * radius - inset * inset))
            whole = int(cut)
            alpha[y, :whole] = 0.0
            alpha[y, width - whole:] = 0.0
            alpha[y, whole] = 1.0 - (cut - whole)
            alpha[y, width - 1 - whole] = 1.0 - (cut - whole)
    return alpha


def pier() -> None:
    wall = load(WALL)
    joints = mortar_rows(wall)
    start = joints[0]
    joints = [j - start for j in joints] + [wall.shape[0]]
    # A joint at row 0 for the work; rolled back at the end so the courses line up again.
    rolled = np.roll(wall, -start, axis=0)
    height = wall.shape[0]
    centre = PIER_WIDTH // 2 + 24
    stone = rolled[:, SEAM_REACH:TILE - SEAM_REACH, :3].reshape(-1, 3).mean(axis=0)
    colour = np.zeros((height, PIER_WIDTH, 3), dtype=np.float32)
    cover = np.zeros((height, PIER_WIDTH), dtype=np.float32)
    shade = np.zeros((height, PIER_WIDTH), dtype=np.float32)
    for i in range(len(joints) - 1):
        top, bottom = joints[i], joints[i + 1]
        width = PIER_BLOCKS[i % 2]
        # Inside the first tile's untouched middle, whichever width the wall now has.
        x = clear_span(rolled[top + 10:bottom - 10], width, SEAM_REACH + 20, TILE - SEAM_REACH - 20)
        block = rolled[top:bottom, x:x + width, :3].copy()
        # The wall's own colour, a step brighter: it stands forward and catches more sun.
        block *= stone / block[20:-20, 20:-20].mean(axis=(0, 1)) * 1.14
        # The sides turn: toward the sun on the right, away on the left.
        for k in range(12):
            t = 1.0 - k / 12.0
            block[:, k] *= 1.0 - 0.26 * t
            block[:, width - 1 - k] *= 1.0 + 0.1 * t
        left = centre - width // 2
        alpha = rounded(width, bottom - top, 14)
        colour[top:bottom, left:left + width] = block
        cover[top:bottom, left:left + width] = alpha
        shade[top:bottom, max(0, left - 46):left - 46 + width] = 255.0
    shade = soften(shade, 18.0) / 255.0 * 0.49
    alpha = cover + shade * (1.0 - cover)
    rgb = colour * cover[..., None] + PIER_SHADOW * (shade * (1.0 - cover))[..., None]
    rgb = rgb / np.maximum(alpha, 1e-4)[..., None]
    out = np.dstack((rgb, alpha * 255.0))
    save(PIER, np.roll(out, start, axis=0))


def main() -> None:
    widen(ROCK, shift=397, drop=311, course_joints=False)
    widen(WALL, shift=331, drop=0, course_joints=True)
    pier()


if __name__ == "__main__":
    main()
