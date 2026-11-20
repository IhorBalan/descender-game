#!/usr/bin/env python3
"""Cut the crowned knight into the parts the run lab's skeleton wears.

From his first standing frame (source/idle_00.png, 362 x 362, soles on row 341)
each part is cut by an outline drawn over the frame. Where one part hid another
(his near arm over his chest, his fist over his thigh) the hidden place is filled
with the armour plates beside it, a little darker. The parts:

    cloak     the red cloak behind him, from the shoulders     (Chest)
    torso     chest, belt and scarf, without arms or head      (Hips: pelvis to neck)
    tabard    the red strip that hangs from the belt          (Hips, hanging)
    head      head, crown, beard and the long hair            (Head)
    upperarm  pauldron and upper arm                           (UpperArm.L / .R)
    forearm   forearm and gauntleted fist                      (LowerArm.L / .R)
    thigh     knee plates                                      (Thigh.L / .R)
    shin      greave                                           (Shin.L / .R)
    foot      sabaton                                          (Foot.L / .R)
    sword     the greatsword, from its grip                    (Hand.R)

The near (left) side's limbs are cut and worn on both sides, the far side's
darker. Each part is written to Assets/Art/Sprites/hero/parts/<part>.png with
its pivot at the joint it hangs from, and parts.json beside them gives each
part's joint and end in its own pixels (Unity's: from the bottom left), so a
part can be laid along its bone:

    python tools/hero_knight/parts.py
    python tools/hero_knight/parts.py --preview scratch/parts.png   # the parts side by side

Needs Python 3 with Pillow, NumPy and OpenCV (cv2).
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

SOURCE = os.path.join(HERE, "source", "idle_00.png")
OUT = os.path.join(unity_meta.ASSETS, "Art", "Sprites", "hero", "parts")
PPU = 125  # his 313 px from crown to soles stand 2.5 units

# Outlines in the source frame's pixels (x right, y down).
NEAR_ARM_UPPER = [(133, 96), (168, 88), (203, 96), (206, 122), (196, 140), (190, 166), (146, 168), (134, 140)]
NEAR_ARM_LOWER = [(140, 150), (190, 150), (196, 196), (192, 240), (148, 240), (137, 198)]
FAR_ARM = [(224, 160), (248, 166), (252, 205), (247, 238), (230, 238), (224, 205)]
TABARD = [(203, 176), (229, 176), (231, 240), (228, 300), (204, 300), (202, 240)]
HEAD = [(196, 22), (230, 22), (236, 60), (234, 92), (200, 96), (192, 60)]
TORSO = [(176, 78), (238, 84), (244, 120), (236, 168), (234, 200), (205, 206), (178, 204), (172, 120)]
THIGH = [(138, 186), (196, 186), (200, 230), (200, 262), (194, 290), (138, 290), (132, 262), (134, 230)]
SHIN = [(138, 270), (190, 270), (188, 300), (184, 318), (140, 318), (136, 300)]
FOOT = [(130, 300), (186, 300), (196, 330), (196, 342), (130, 342)]
SWORD = [(238, 188), (276, 186), (300, 230), (362, 330), (362, 342), (330, 342), (236, 250)]
CLOAK = [(56, 70), (180, 80), (176, 210), (140, 240), (132, 330), (56, 340)]

# Each part: (outline, joint, along, bone, rigid, order, sway), joint and along in source
# pixels. A limb (not rigid) is laid from its joint to `along` on its bone, stretched to the
# bone's length; a rigid part keeps its size and turns with its bone, `along` being the
# bone's direction in the standing frame (the torso's line for what hangs from it, the
# fist's line for the sword). A bone named without a side (Thigh) is worn on both: the
# near (left) at `order`, the far (right) darker and behind. `sway` is the degrees a
# hanging part swings each step. The sword is carried low: STEADY gives it a world angle
# (0 forward, 90 up) for the line from its grip to its tip, and how much of the hand's
# turning it follows.
STEADY = {"sword": (-150.0, 0.25)}
UP = (8, -96)  # his torso's line in the standing frame, pelvis to neck
PARTS = {
    "cloak": (CLOAK, (178, 104), (178 + UP[0], 104 + UP[1]), "Chest", True, -4, 4.0),
    "torso": (TORSO, (200, 188), (208, 92), "Hips", True, 5, 0.0),
    "tabard": (TABARD, (216, 178), (216 + UP[0], 178 + UP[1]), "Hips", True, 6, 6.0),
    "head": (HEAD, (210, 92), (214, 34), "Head", True, 8, 0.0),
    "upperarm": (NEAR_ARM_UPPER, (172, 108), (167, 158), "UpperArm", False, 9, 0.0),
    "forearm": (NEAR_ARM_LOWER, (167, 158), (168, 222), "LowerArm", False, 10, 0.0),
    "thigh": (THIGH, (166, 192), (163, 266), "Thigh", False, 7, 0.0),
    "shin": (SHIN, (163, 274), (161, 314), "Shin", False, 7, 0.0),
    "foot": (FOOT, (160, 316), (194, 336), "Foot", False, 7, 0.0),
    "sword": (SWORD, (252, 222), (352, 336), "Hand.R", True, -1, 0.0),
}


def mask_of(size, outline) -> np.ndarray:
    image = Image.new("L", size, 0)
    ImageDraw.Draw(image).polygon(outline, fill=255)
    return np.asarray(image) > 0


def hair_mask(rgba: np.ndarray) -> np.ndarray:
    """His long grey hair: pale and unsaturated, behind and above his shoulders."""
    rgb = rgba[..., :3].astype(float) / 255
    high, low = rgb.max(-1), rgb.min(-1)
    saturation = np.where(high > 0, (high - low) / np.maximum(high, 1e-6), 0)
    h, w = rgba.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    return (rgba[..., 3] > 0) & (saturation < 0.28) & (high > 0.38) & (xs < 214) & (ys < 150)


def joined_to(mask: np.ndarray, seed: np.ndarray) -> np.ndarray:
    """The pieces of `mask` that touch `seed` (stray pixels elsewhere dropped)."""
    import cv2  # only when cutting: the tools' import check runs without OpenCV

    count, labels = cv2.connectedComponents((mask | seed).astype(np.uint8), connectivity=8)
    keep = np.unique(labels[seed & (labels > 0)])
    return np.isin(labels, keep) & mask


def cloth(rgba: np.ndarray) -> np.ndarray:
    """The red of the cloak and tabard, and the dark of their folds; not grey hair."""
    r, g, b = (rgba[..., i].astype(int) for i in range(3))
    return (rgba[..., 3] > 0) & ((r > g + 18) | (np.maximum(np.maximum(r, g), b) < 60))


def plates(rgba: np.ndarray, holes: np.ndarray, period: int = 26, shade: float = 0.72) -> np.ndarray:
    """Fills holes in armour with the armour beside them: each hole pixel takes the colour
    `period` pixels to its right (and further, until one is not a hole), a little darker,
    as the side of the chest the arm hid is in its shadow."""
    out = rgba.copy()
    ys, xs = np.nonzero(holes)
    w = rgba.shape[1]
    for y, x in zip(ys, xs):
        sx = x + period
        while sx < w and holes[y, sx]:
            sx += period
        if sx < w and rgba[y, sx, 3] > 0:
            out[y, x, :3] = (rgba[y, sx, :3] * shade).astype(np.uint8)
            out[y, x, 3] = 255
    return out


def cut(rgba: np.ndarray, keep: np.ndarray):
    """The pixels of `keep`, cropped to them: (image, left, top)."""
    alpha = keep & (rgba[..., 3] > 0)
    ys, xs = np.nonzero(alpha)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    part = rgba[y0:y1, x0:x1].copy()
    part[..., 3] = np.where(alpha[y0:y1, x0:x1], part[..., 3], 0)
    return Image.fromarray(part), int(x0), int(y0)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=OUT, help="where to write the parts (default: Assets/Art/Sprites/hero/parts)")
    parser.add_argument("--preview", help="also write the parts side by side to this PNG")
    args = parser.parse_args()
    rgba = np.asarray(Image.open(SOURCE).convert("RGBA")).copy()
    size = (rgba.shape[1], rgba.shape[0])
    masks = {name: mask_of(size, spec[0]) for name, spec in PARTS.items()}
    hair = hair_mask(rgba)
    masks["head"] = joined_to(masks["head"] | hair, masks["head"])
    far_arm = mask_of(size, FAR_ARM)
    # What each part may not take: what lies over it in the frame.
    arm = masks["upperarm"] | masks["forearm"]
    masks["torso"] &= ~masks["head"] & ~far_arm & ~masks["tabard"] & ~hair
    masks["cloak"] &= cloth(rgba) & ~hair & ~arm & ~masks["thigh"] & ~masks["shin"] & ~masks["foot"] & ~masks["torso"]
    masks["cloak"] = joined_to(masks["cloak"], mask_of(size, [(60, 200), (130, 200), (130, 320), (60, 320)]))
    masks["sword"] &= ~far_arm | (mask_of(size, [(246, 210), (262, 210), (262, 236), (246, 236)]))
    # Painted in: the chest behind the near arm, the thighs behind the near fist.
    painted = plates(rgba, arm & mask_of(size, TORSO))
    # The thigh behind the near fist: the plates under it, from below.
    legs = plates(np.rot90(rgba, -1).copy(), np.rot90(masks["forearm"] & masks["thigh"], -1).copy(), period=34, shade=0.9)
    legs = np.rot90(legs, 1).copy()
    os.makedirs(args.out, exist_ok=True)
    spec = {"ppu": PPU, "parts": {}}
    previews = []
    for name, (_, joint, end, bone, rigid, order, sway) in PARTS.items():
        source = painted if name == "torso" else legs if name == "thigh" else rgba
        image, left, top = cut(source, masks[name])
        path = os.path.join(args.out, name + ".png")
        image.save(path)
        height = image.height
        # In the part's own pixels, Unity's way: from its bottom left.
        local = lambda p: [round(p[0] - left, 2), round(height - (p[1] - top), 2)]  # noqa: E731
        spec["parts"][name] = {
            "joint": local(joint),
            "end": local(end),
            "bone": bone,
            "rigid": rigid,
            "order": order,
            "sway": sway,
            "steady": name in STEADY,
            "worldAngle": STEADY.get(name, (0.0, 0.0))[0],
            "follow": STEADY.get(name, (0.0, 0.0))[1],
            "size": [image.width, image.height],
        }
        if unity_meta.inside_assets(path):
            pivot = (local(joint)[0] / image.width, local(joint)[1] / image.height)
            if os.path.exists(path + ".meta"):
                os.remove(path + ".meta")
            unity_meta.ensure_meta(path)
            meta = unity_meta.texture_meta(unity_meta.guid_for(unity_meta.rel(path)), ppu=PPU, point_filter=True, pivot=pivot)
            with open(path + ".meta", "w", newline="\n") as handle:
                handle.write(meta)
        previews.append(image)
    spec_path = os.path.join(args.out, "parts.json")
    with open(spec_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(spec, handle, indent=2)
        handle.write("\n")
    unity_meta.ensure_meta(spec_path)
    if args.preview:
        width = sum(p.width for p in previews) + 10 * len(previews)
        sheet = Image.new("RGBA", (width, max(p.height for p in previews)), (60, 64, 80, 255))
        x = 0
        for p in previews:
            sheet.alpha_composite(p, (x, 0))
            x += p.width + 10
        sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(args.preview)
    print(f"knight parts: {len(PARTS)} -> {args.out}")


if __name__ == "__main__":
    main()
