#!/usr/bin/env python3
"""Cut the crowned knight's painted run cycle into the frames the run lab plays.

The source is source/run_sheet.png (1536 x 1024, transparent): eight frames of
the run, drawn by ChatGPT image generation from the owner's knight and the
lab's eight key poses, in two rows of four: contact, down, passing and flight,
twice. Each frame is found as the figures on the sheet (rows and columns of
empty space between them), then put on one canvas so that every frame's face
is at the same place and its ground line on the canvas's bottom row.

The sheet's background was taken away by the image tool, but it left patches
of the grey background where it showed between the legs, and a light rim of
it round the figure. Every grey patch that is flat (the background had no
texture, the hair and the steel do) is taken away whole, and the pixels on the
figure's edge that are grey, or lighter than the figure just inside them, are
made mostly see-through.

A painted frame does not place the planted foot where an even run would: the
foot moves back by different amounts from frame to frame. Each stance frame is
moved forward or back so the planted heel moves back evenly (the line that fits
its heel's places, least squares), and each flight frame takes the mean of the
moves on either side. At PPU 148 a cycle covers 592 sheet pixels, the lab's
stride of 4 units, so the feet keep to the ground at the lab's speed.

    python tools/hero_knight/run_frames.py
    python tools/hero_knight/run_frames.py --preview scratch/run.png

Writes Assets/Art/Sprites/hero/run/knight_run_<n>.png, each with its pivot on the
ground under the hips, and run.json. Needs Python 3 with Pillow and NumPy.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "unity"))
import unity_meta  # noqa: E402

SOURCE = os.path.join(HERE, "source", "run_sheet.png")
OUT = os.path.join(unity_meta.ASSETS, "Art", "Sprites", "hero", "run")
PPU = 148  # 592 sheet pixels a cycle: a stride of 4 units
SOLID = 16  # alpha above which a pixel is the figure
HEAD = 70  # rows from the top of a figure that hold the head
FACE = 360  # where every frame's face is on the canvas
ROOT = FACE - 70  # the hips, over the pivot
WIDTH, HEIGHT = 520, 440  # the canvas; its bottom row is the ground
GROUND = HEIGHT - 1
GREY = 14  # a pixel this close to grey in its channels, and light, may be background
LIGHT = 150
FLAT = 6.0  # the background's spread of light about a pixel, at most
RIM = 0.35  # what is kept of a rim pixel's alpha on the figure's edge
BRIGHTER = 25  # a rim pixel this much lighter than the figure inside it is the old background
# The frames that have a foot down, each half's in order, and the flight frame after them.
STANCE = ((0, 1, 2), (4, 5, 6))
FLIGHT = (3, 7)


def runs(counts) -> list[tuple[int, int]]:
    """The spans of non-zero counts: (start, end)."""
    spans, start = [], None
    for i, count in enumerate(counts):
        if count and start is None:
            start = i
        if not count and start is not None:
            spans.append((start, i))
            start = None
    if start is not None:
        spans.append((start, len(counts)))
    return spans


def neighbours(mask: np.ndarray, reach: int) -> np.ndarray:
    """Pixels within `reach` (a square) of the mask."""
    out = mask.copy()
    h, w = mask.shape
    padded = np.pad(mask, reach)
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            out |= padded[reach + dy : reach + dy + h, reach + dx : reach + dx + w]
    return out


def pieces(mask: np.ndarray) -> list[np.ndarray]:
    """The 8-connected pieces of a mask, as arrays of (y, x)."""
    from collections import deque

    seen = np.zeros(mask.shape, bool)
    found = []
    h, w = mask.shape
    for sy, sx in zip(*np.nonzero(mask)):
        if seen[sy, sx]:
            continue
        piece, queue = [], deque([(sy, sx)])
        seen[sy, sx] = True
        while queue:
            y, x = queue.popleft()
            piece.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
        found.append(np.array(piece))
    return found


def cleaned(sheet: np.ndarray) -> np.ndarray:
    """The sheet without the background's grey patches, and its grey rim made faint."""
    out = sheet.copy()
    rgb = sheet[..., :3].astype(int)
    alpha = sheet[..., 3]
    grey = (rgb.max(2) - rgb.min(2) < GREY) & (rgb.min(2) > LIGHT) & (alpha > SOLID)
    light = rgb.mean(2)
    h, w = light.shape
    padded = np.pad(light, 2, mode="edge")
    spread = np.stack([padded[2 + dy : 2 + dy + h, 2 + dx : 2 + dx + w] for dy in range(-2, 3) for dx in range(-2, 3)]).std(0)
    flat = grey & (spread < FLAT)
    for piece in pieces(grey):
        if flat[piece[:, 0], piece[:, 1]].any():
            out[piece[:, 0], piece[:, 1], 3] = 0
    edge = neighbours(out[..., 3] <= SOLID, 1) & (out[..., 3] > 0)
    inside = (out[..., 3] > SOLID) & ~edge
    # The light of the figure just inside each edge pixel: the mean of the inside pixels about it.
    total = np.zeros((h, w))
    count = np.zeros((h, w))
    padded_light = np.pad(light * inside, 2)
    padded_inside = np.pad(inside.astype(float), 2)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            total += padded_light[2 + dy : 2 + dy + h, 2 + dx : 2 + dx + w]
            count += padded_inside[2 + dy : 2 + dy + h, 2 + dx : 2 + dx + w]
    mean = np.where(count > 0, total / np.maximum(count, 1), light)
    rim = edge & (grey | (light > mean + BRIGHTER))
    out[rim, 3] = (out[rim, 3] * RIM).astype(np.uint8)
    return out


def frames(sheet: np.ndarray) -> list[np.ndarray]:
    """The eight frames, each on the canvas with its face at FACE and the ground at the bottom."""
    solid = sheet[..., 3] > SOLID
    found = []
    for top, bottom in runs(solid.sum(1)):
        # A row's ground: its lowest foot (the frames with a foot down share it).
        ground = bottom - 1
        for left, right in runs(solid[top:bottom].sum(0)):
            part = sheet[top:bottom, left:right]
            mask = part[..., 3] > SOLID
            ys = np.nonzero(mask.any(1))[0]
            face = int(np.nonzero(mask[ys[0] : ys[0] + HEAD].any(0))[0].max())
            canvas = np.zeros((HEIGHT, WIDTH, 4), np.uint8)
            x, y = FACE - face, GROUND - (ground - top)
            canvas[max(y, 0) : y + part.shape[0], x : x + part.shape[1]] = part[max(-y, 0) :]
            found.append(canvas)
    if len(found) != 8:
        raise SystemExit(f"expected 8 frames on the sheet, found {len(found)}")
    return found


def heel(frame: np.ndarray) -> int:
    """The back of the foot on the ground: the leftmost solid pixel of the bottom rows."""
    rows = frame[GROUND - 12 :, :, 3] > SOLID
    return int(np.nonzero(rows.any(0))[0].min())


def moves(found: list[np.ndarray]) -> list[int]:
    """How far each frame is moved so the planted heel goes back evenly."""
    shift = [0.0] * len(found)
    for half in STANCE:
        steps = np.arange(len(half))
        heels = np.array([heel(found[i]) for i in half], float)
        slope, start = np.polyfit(steps, heels, 1)
        for step, i in zip(steps, half):
            shift[i] = start + slope * step - heels[step]
    for i in FLIGHT:
        shift[i] = (shift[i - 1] + shift[(i + 1) % len(found)]) / 2
    return [round(s) for s in shift]


def moved(frame: np.ndarray, dx: int) -> np.ndarray:
    out = np.zeros_like(frame)
    if dx >= 0:
        out[:, dx:] = frame[:, : frame.shape[1] - dx]
    else:
        out[:, :dx] = frame[:, -dx:]
    return out


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=OUT, help="where to write the frames")
    parser.add_argument("--preview", help="also write the frames side by side to this PNG")
    args = parser.parse_args()
    sheet = cleaned(np.asarray(Image.open(SOURCE).convert("RGBA")))
    found = frames(sheet)
    shift = moves(found)
    os.makedirs(args.out, exist_ok=True)
    pivot = (ROOT / WIDTH, 0.0)
    images = []
    for i, (frame, dx) in enumerate(zip(found, shift)):
        image = Image.fromarray(moved(frame, dx))
        path = os.path.join(args.out, f"knight_run_{i}.png")
        image.save(path, optimize=True)
        if unity_meta.inside_assets(path):
            guid = unity_meta.guid_for(unity_meta.rel(path))
            meta = unity_meta.texture_meta(guid, ppu=PPU, point_filter=False, mipmaps=True, pivot=pivot)
            unity_meta.ensure_meta(path)
            with open(path + ".meta", "w", newline="\n") as handle:
                handle.write(meta)
        images.append(image)
    spec = {"ppu": PPU, "frames": len(found), "moves": shift}
    spec_path = os.path.join(args.out, "run.json")
    with open(spec_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(spec, handle, indent=2)
        handle.write("\n")
    unity_meta.ensure_meta(spec_path)
    if args.preview:
        strip = Image.new("RGBA", (WIDTH * len(images), HEIGHT), (92, 102, 120, 255))
        for i, image in enumerate(images):
            strip.alpha_composite(image, (i * WIDTH, 0))
        strip.save(args.preview)
    print(f"knight run: {len(images)} frames, moved {shift} -> {args.out}")


if __name__ == "__main__":
    main()
