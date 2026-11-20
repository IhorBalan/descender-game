#!/usr/bin/env python3
"""Cut the crowned knight's parts sheet into the parts the run lab's skeleton wears.

The source is the owner's parts sheet (source/parts_sheet.webp, 1254 x 1254, a
transparent background): the knight drawn apart, every part whole, all at one
scale, side view facing right. Each part is found as one piece of the sheet
(what lies within a few pixels of each other belongs together), cropped and
written to Assets/Art/Sprites/hero/parts/<part>.png with its pivot at the joint
it hangs from; parts.json beside them gives each part's joint, its line and how
it is worn. The upper arm is cut in two: the pauldron, which turns with the
shoulders, and the sleeve under it, which swings with the arm.

    python tools/hero_knight/parts.py
    python tools/hero_knight/parts.py --preview scratch/parts.png   # the parts side by side

A part is laid along its bone from its joint toward `along` (both in sheet
pixels): a limb is stretched from joint to `along` to its bone's length; a rigid
part keeps its size and turns with its bone, `along` naming the direction that
lies on the bone (straight up for what hangs from the body). `scale` sizes a
rigid part against the rest (the greatsword is drawn larger than he could carry).
A limb's bone named without a side (Thigh) is worn on both sides. Needs Python 3
with Pillow and NumPy.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "unity"))
import unity_meta  # noqa: E402

SOURCE = os.path.join(HERE, "source", "parts_sheet.webp")
OUT = os.path.join(unity_meta.ASSETS, "Art", "Sprites", "hero", "parts")
PPU = 400  # the knight stands about 1040 sheet pixels: 2.6 units

# Each part: its box on the sheet (x0, y0, x1, y1), and how it is worn.
#   joint, along: sheet pixels; bone; rigid; order (the near side's); sway (degrees each
#   step); scale; steady (a world angle in degrees for the line joint -> along, and the
#   share of the bone's swing it follows) for what is carried rather than worn.
UP = (0, -100)


def up_from(point):
    return (point[0] + UP[0], point[1] + UP[1])


PARTS = {
    "cloak": dict(box=(795, 0, 1222, 640), joint=(1170, 157), along=up_from((1170, 157)), bone="Neck", rigid=True, order=-12, sway=3.0),
    # The sheet draws him short in the body for his legs: the rows from under the chest
    # plate down to the belt are drawn LENGTHEN pixels longer (RunBody.Knight's spine).
    "torso": dict(box=(366, 30, 662, 566), joint=(505, 330), along=up_from((505, 330)), bone="Hips", rigid=True, order=4, lengthen=(150, 268, 80)),
    "tabard": dict(box=(668, 52, 792, 582), joint=(665, 120), along=up_from((665, 120)), bone="Hips", rigid=True, order=5, sway=5.0),
    "head": dict(box=(12, 78, 356, 360), joint=(255, 268), along=up_from((255, 268)), bone="Head", rigid=True, order=9),
    "pauldron": dict(box=(40, 550, 334, 792), joint=(200, 690), along=(100, 690), bone="Shoulder", rigid=True, order=12),
    # The sleeve as split_arm moved it (SLEEVE_SHIFT rows down).
    "sleeve": dict(box=(40, 1254, 334, 1854), joint=(200, 690), along=(180, 920), bone="UpperArm", rigid=False, order=10, moved=True),
    "forearm": dict(box=(328, 672, 508, 978), joint=(405, 715), along=(425, 880), bone="LowerArm", rigid=False, order=11),
    # Under the torso's tassets (order 4), as plate thighs are: the tassets hide its top as it lifts.
    "thigh": dict(box=(540, 626, 732, 964), joint=(628, 670), along=(612, 915), bone="Thigh", rigid=False, order=3),
    "shin": dict(box=(760, 634, 930, 980), joint=(845, 690), along=(835, 935), bone="Shin", rigid=False, order=7),
    "foot": dict(box=(956, 786, 1226, 954), joint=(1020, 860), along=(1205, 935), bone="Foot", rigid=False, order=7),
    "sword": dict(box=(72, 955, 1124, 1224), joint=(205, 1088), along=(1100, 1086), bone="Hand.R", rigid=True, order=-5, scale=0.5, steady=(-150.0, 0.25)),
}


# The pauldron's lower edge on the sheet: above it the pauldron, below it the sleeve.
PAULDRON = [(40, 550), (334, 550), (334, 778), (292, 772), (262, 742), (226, 756), (192, 770), (150, 762), (112, 766), (78, 786), (40, 790)]
SHOULDER = (200, 690)  # the shoulder joint on the sheet
SHOULDER_TOP = 622  # the sleeve is carried up under the pauldron toward here
# How far round the shoulder joint the carried-up sleeve reaches: a disc about the joint
# turns onto itself, so the pauldron (which covers it) hides it at every swing.
UNDER_PAULDRON = 66


def polygon(size, outline) -> np.ndarray:
    image = Image.new("L", size, 0)
    ImageDraw.Draw(image).polygon(outline, fill=255)
    return np.asarray(image) > 0


def pieces(mask: np.ndarray) -> list[np.ndarray]:
    """The 8-connected pieces of a mask, each as its own mask, biggest first."""
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
        one = np.zeros(mask.shape, bool)
        ys, xs = zip(*piece)
        one[list(ys), list(xs)] = True
        found.append(one)
    return sorted(found, key=lambda m: -m.sum())


def largest(mask: np.ndarray) -> np.ndarray:
    """The biggest piece of a mask (crumbs of the pauldron's edge dropped)."""
    found = pieces(mask)
    return found[0] if found else mask


def without_crumbs(image: np.ndarray, share: float = 0.01) -> np.ndarray:
    """A part with every loose speck taken away: pieces under `share` of its biggest."""
    found = pieces(image[..., 3] > 8)
    keep = np.zeros(image.shape[:2], bool)
    for piece in found:
        if piece.sum() >= share * found[0].sum():
            keep |= piece
    out = image.copy()
    out[~keep, 3] = 0
    return out


def split_arm(rgba: np.ndarray) -> np.ndarray:
    """The sheet with the upper arm parted: the pauldron where it was, the sleeve moved to
    a free place under the sheet's drawings and carried up under the pauldron, darker
    there (the pauldron shades it), so it can turn without showing a cut edge."""
    out = rgba.copy()
    h, w = rgba.shape[:2]
    arm = np.zeros((h, w), bool)
    arm[550:965, 40:334] = rgba[550:965, 40:334, 3] > 0
    pauldron = polygon((w, h), PAULDRON)
    sleeve = largest(arm & ~pauldron)
    out[arm & ~pauldron, 3] = 0  # the pauldron stays alone where it is drawn
    canvas = np.zeros((h + 600, w, 4), np.uint8)
    canvas[:h] = out
    shift = h + 120 - SHOULDER_TOP
    ys, xs = np.nonzero(sleeve)
    canvas[ys + shift, xs] = rgba[ys, xs]
    # The first row the sleeve is whole across, and the rows above it made from it.
    widths = sleeve.sum(1)
    full = int(np.nonzero(widths >= 0.8 * widths.max())[0][0])
    row = rgba[full + 4].copy()
    columns = sleeve[full + 4]
    row[..., :3] = (row[..., :3] * 0.5).astype(np.uint8)
    xs_all = np.arange(w)
    half = max(abs(int(xs_all[columns].min()) - SHOULDER[0]), abs(int(xs_all[columns].max()) - SHOULDER[0]))
    for y in range(SHOULDER_TOP, full + 4):
        target = canvas[y + shift]
        if y <= SHOULDER[1]:
            reach = np.sqrt(max(UNDER_PAULDRON**2 - (y - SHOULDER[1]) ** 2, 0))
        else:  # widening from the disc about the joint to the sleeve's own width
            t = (y - SHOULDER[1]) / max(full + 4 - SHOULDER[1], 1)
            reach = UNDER_PAULDRON + (half - UNDER_PAULDRON) * t
        fill = columns & (np.abs(xs_all - SHOULDER[0]) <= reach) & (target[..., 3] < 255)
        target[fill] = row[fill]
    return canvas


def cut(rgba: np.ndarray, box) -> tuple[Image.Image, int, int]:
    """The sheet's pixels inside `box`, cropped to what is drawn there: (image, left, top)."""
    x0, y0, x1, y1 = box
    window = rgba[y0:y1, x0:x1]
    ys, xs = np.nonzero(window[..., 3] > 0)
    left, top = x0 + xs.min(), y0 + ys.min()
    right, bottom = x0 + xs.max() + 1, y0 + ys.max() + 1
    part = without_crumbs(rgba[top:bottom, left:right].copy())
    return Image.fromarray(part), int(left), int(top)


def lengthened(image: Image.Image, top: int, rows) -> Image.Image:
    """A part with the sheet's rows y0..y1 drawn `extra` pixels longer, what is above and
    below them unchanged (the chest plate and the belt keep their shapes)."""
    y0, y1, extra = rows
    a, b = y0 - top, y1 - top
    w, h = image.size
    band = image.crop((0, a, w, b)).resize((w, b - a + extra), Image.LANCZOS)
    out = Image.new("RGBA", (w, h + extra))
    out.paste(image.crop((0, 0, w, a)), (0, 0))
    out.paste(band, (0, a))
    out.paste(image.crop((0, b, w, h)), (0, b + extra))
    return out


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=OUT, help="where to write the parts (default: Assets/Art/Sprites/hero/parts)")
    parser.add_argument("--preview", help="also write the parts side by side to this PNG")
    args = parser.parse_args()
    rgba = split_arm(np.asarray(Image.open(SOURCE).convert("RGBA")).copy())
    sleeve_shift = 1254 + 120 - SHOULDER_TOP
    os.makedirs(args.out, exist_ok=True)
    spec = {"ppu": PPU, "parts": {}}
    previews = []
    for name, part in PARTS.items():
        if part.get("moved"):
            part = dict(part, joint=(part["joint"][0], part["joint"][1] + sleeve_shift), along=(part["along"][0], part["along"][1] + sleeve_shift))
        image, left, top = cut(rgba, part["box"])
        rows = part.get("lengthen")
        if rows:
            image = lengthened(image, top, rows)
        height = image.height

        def local(point):
            # In the part's own pixels, Unity's way: from its bottom left.
            y = point[1] + (rows[2] if rows and point[1] >= rows[1] else 0)
            return [round(point[0] - left, 2), round(height - (y - top), 2)]

        path = os.path.join(args.out, name + ".png")
        image.save(path, optimize=True)
        steady = part.get("steady")
        spec["parts"][name] = {
            "joint": local(part["joint"]),
            "end": local(part["along"]),
            "bone": part["bone"],
            "rigid": part["rigid"],
            "order": part["order"],
            "sway": part.get("sway", 0.0),
            "scale": part.get("scale", 1.0),
            "steady": steady is not None,
            "worldAngle": steady[0] if steady else 0.0,
            "follow": steady[1] if steady else 0.0,
            "size": [image.width, image.height],
        }
        if unity_meta.inside_assets(path):
            joint = local(part["joint"])
            pivot = (joint[0] / image.width, joint[1] / image.height)
            guid = unity_meta.guid_for(unity_meta.rel(path))
            # Painted art, drawn small: smoothed, with mipmaps so it stays clean shrunk.
            meta = unity_meta.texture_meta(guid, ppu=PPU, point_filter=False, mipmaps=True, pivot=pivot)
            unity_meta.ensure_meta(path)
            with open(path + ".meta", "w", newline="\n") as handle:
                handle.write(meta)
        previews.append(image)
    # Parts the sheet no longer has (an earlier cut) are taken away with their .meta.
    for stale in os.listdir(args.out):
        stem, extension = os.path.splitext(stale)
        if extension == ".png" and stem not in PARTS:
            os.remove(os.path.join(args.out, stale))
            if os.path.exists(os.path.join(args.out, stale + ".meta")):
                os.remove(os.path.join(args.out, stale + ".meta"))
    spec_path = os.path.join(args.out, "parts.json")
    with open(spec_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(spec, handle, indent=2)
        handle.write("\n")
    unity_meta.ensure_meta(spec_path)
    if args.preview:
        width = sum(p.width for p in previews) + 10 * len(previews)
        sheet = Image.new("RGBA", (width, max(p.height for p in previews)), (40, 44, 58, 255))
        x = 0
        for p in previews:
            sheet.alpha_composite(p, (x, 0))
            x += p.width + 10
        sheet.save(args.preview)
    print(f"knight parts: {len(PARTS)} -> {args.out}")


if __name__ == "__main__":
    main()
