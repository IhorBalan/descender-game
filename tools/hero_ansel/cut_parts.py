"""Cuts the parts sheet of the new Ansel into one transparent PNG per part.

The sheet (parts_sheet.png, next to this file) holds his parts on a flat grey ground:
head, hood, torso, arm, leg, cloak and sword. The grey is keyed out where it touches the
sheet's edge or fills a sizeable hole (the cloak's rips), the grey fringe is unmixed from
the edges, and each part is written under its name. reference.png is the approved full
figure that fit_parts.py measures the parts against.

    python tools/hero_ansel/cut_parts.py [sheet] [out folder]
    python tools/hero_ansel/fit_parts.py [out folder]

Needs numpy, Pillow and OpenCV.
"""
import os
import sys

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_SHEET = os.path.join(HERE, "parts_sheet.png")
DEFAULT_OUT = os.path.join(HERE, "..", "ai_art", "out", "hero_ansel_parts")
MIN_PART = 1500  # pixels: anything smaller is a speck
MIN_HOLE = 60  # pixels: a smaller grey island inside a part is paint, not a hole


def name_for(x, y, w, h):
    """Which part a box on the sheet is, from where the sheet lays each one out."""
    cx, cy = x + w / 2, y + h / 2
    if cx > 950:
        return "cloak"
    if cy > 840:
        return "sword"
    if cy < 200 and cx < 350:
        return "head"
    if cy < 300:
        return "hood"
    if cx < 350:
        return "torso"
    if cx < 620:
        return "arm"
    return "leg"


def cut(sheet_path, out):
    rgb = np.asarray(Image.open(sheet_path).convert("RGB"))
    h, w = rgb.shape[:2]
    border = np.concatenate([rgb[:12].reshape(-1, 3), rgb[-12:].reshape(-1, 3), rgb[:, :12].reshape(-1, 3), rgb[:, -12:].reshape(-1, 3)])
    bg = np.median(border, axis=0)

    lab = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).astype(np.float32)
    bg_lab = cv2.cvtColor(np.uint8([[bg]]), cv2.COLOR_RGB2LAB).astype(np.float32)[0, 0]
    dist = np.sqrt((((lab - bg_lab) * np.array([0.7, 1.0, 1.0])) ** 2).sum(axis=2))

    near = (dist < 9).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(near, connectivity=4)
    edge = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]])))
    keep = np.zeros(n, bool)
    for i in range(1, n):
        keep[i] = i in edge or stats[i, cv2.CC_STAT_AREA] >= MIN_HOLE
    fg = (~keep[labels]).astype(np.uint8)

    alpha = np.clip(cv2.distanceTransform(fg, cv2.DIST_L2, 3) / 1.6, 0, 1)
    a = alpha[..., None]
    unmixed = np.where(a > 0.05, (rgb.astype(np.float32) - bg * (1 - a)) / np.maximum(a, 0.05), rgb)
    unmixed = np.clip(unmixed, 0, 255).astype(np.uint8)
    alpha = (alpha * 255).astype(np.uint8)

    os.makedirs(out, exist_ok=True)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(fg, connectivity=8)
    written = []
    for i in range(1, n):
        x, y, bw, bh, area = stats[i]
        if area <= MIN_PART:
            continue
        name = name_for(x, y, bw, bh)
        x0, y0, x1, y1 = max(x - 4, 0), max(y - 4, 0), min(x + bw + 4, w), min(y + bh + 4, h)
        part_alpha = np.where(labels[y0:y1, x0:x1] == i, alpha[y0:y1, x0:x1], 0).astype(np.uint8)
        Image.fromarray(np.dstack([unmixed[y0:y1, x0:x1], part_alpha]), "RGBA").save(os.path.join(out, name + ".png"))
        written.append(name)
        print(f"{name:6} at ({x},{y}) size ({bw},{bh})")
    missing = {"head", "hood", "torso", "arm", "leg", "cloak", "sword"} - set(written)
    if missing:
        sys.exit("The sheet is missing parts: " + ", ".join(sorted(missing)))


def main():
    if cv2 is None:
        sys.exit("cut_parts.py needs OpenCV.")
    cut(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SHEET, sys.argv[2] if len(sys.argv) > 2 else DEFAULT_OUT)


if __name__ == "__main__":
    main()
