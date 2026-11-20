#!/usr/bin/env python3
"""Export the crowned knight, the hero's look, into Assets/Art/Sprites/hero/ at game scale.

The source is the owner's art: twelve 362 x 362 frames of the knight standing
(source/idle_00.png .. idle_11.png, his feet on row 341). Each frame is shrunk
to the Tiles V2 pixel (32 a tile) so he stands about two and a half tiles high,
the height of his collision box, with a dark outline where his edge meets the
air. Until the rest of his moves are drawn, every other strip the player script
plays holds his first standing frame, as many frames as it had, so the timings
the script reads from the strips stay as they were.

    python tools/hero_knight/export.py
    python tools/hero_knight/export.py --out scratch/sprites   # write somewhere else

It rewrites the hero entry in sprites.json (frame size, origin, `ppu` 32 and the
strips) and the strips' .meta files (unity_meta slices them from that entry);
run tools/unity/build_prefabs.py after it so Player.prefab holds the new frames.
Needs Python 3 with Pillow and NumPy.
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "unity"))
import unity_meta  # noqa: E402

SPRITES = os.path.join(unity_meta.ASSETS, "Art", "Sprites")
SOURCE = os.path.join(HERE, "source")

PPU = 32  # the Tiles V2 pixel: one tile is 32 of his pixels
TALL = 80  # px from the top of his crown to his soles: 2.5 tiles
SOURCE_TOP, SOURCE_FOOT = 29, 341  # his crown's top row and his soles' row in a source frame
SOURCE_MIDDLE = 193  # the column between his feet in a source frame: he turns about it
SCALE = TALL / (SOURCE_FOOT - SOURCE_TOP)
FRAME = 96  # a frame's width and height
FOOT = 92  # the row his soles stand on in a frame
OUTLINE = (22, 14, 20, 255)
IDLE_FPS = 8
# What the player script plays besides standing, and how many frames and how fast each
# was: kept, so the attack's timing and the one-shots' ends stay where the script has them.
OTHERS = {
    "run": (8, 12, True),
    "skid": (1, 1, False),
    "jump": (3, 8, False),
    "fall": (2, 6, True),
    "land": (2, 15, False),
    "dash": (2, 12, True),
    "wall": (2, 6, True),
    "glide": (2, 8, True),
    "double_jump": (4, 20, False),
    "attack": (6, 21, False),
    "cast": (3, 14, False),
    "slam": (2, 1, False),
    "hurt": (2, 10, False),
    "heal": (4, 8, True),
    "death": (5, 8, False),
}


def shrink(img: Image.Image) -> Image.Image:
    """A source frame at game scale on its frame: area-averaged with premultiplied alpha,
    the alpha made hard, a dark outline where he meets the air."""
    size = round(img.width * SCALE)
    a = np.asarray(img.convert("RGBA")).astype(float) / 255
    a[..., :3] *= a[..., 3:4]
    small = np.asarray(Image.fromarray((a * 255).round().astype(np.uint8)).resize((size, size), Image.BOX)).astype(float) / 255
    alpha = small[..., 3:4]
    rgb = np.where(alpha > 0, small[..., :3] / np.maximum(alpha, 1e-6), 0)
    rgb = np.clip((rgb - 0.5) * 1.08 + 0.5, 0, 1)  # what the averaging washed out
    solid = alpha[..., 0] > 0.42
    body = np.zeros((size, size, 4), np.uint8)
    body[solid, :3] = (rgb[solid] * 255).round().astype(np.uint8)
    body[solid, 3] = 255
    frame = Image.new("RGBA", (FRAME, FRAME))
    left = FRAME // 2 - round(SOURCE_MIDDLE * SCALE)
    top = FOOT - round(SOURCE_FOOT * SCALE)
    frame.alpha_composite(Image.fromarray(body), (left, top))
    out = np.asarray(frame).copy()
    solid = out[..., 3] > 0
    edge = np.zeros_like(solid)
    edge[1:] |= solid[:-1]
    edge[:-1] |= solid[1:]
    edge[:, 1:] |= solid[:, :-1]
    edge[:, :-1] |= solid[:, 1:]
    out[edge & ~solid] = OUTLINE
    return Image.fromarray(out)


def strip(frames: list[Image.Image]) -> Image.Image:
    sheet = Image.new("RGBA", (FRAME * len(frames), FRAME))
    for i, frame in enumerate(frames):
        sheet.alpha_composite(frame, (i * FRAME, 0))
    return sheet


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=SPRITES, help="the sprites folder to write into (default: Assets/Art/Sprites)")
    out_root = os.path.abspath(parser.parse_args().out)
    out_dir = os.path.join(out_root, "hero")
    os.makedirs(out_dir, exist_ok=True)
    idle = [shrink(Image.open(path)) for path in sorted(glob.glob(os.path.join(SOURCE, "idle_*.png")))]
    assert len(idle) == 12, "the twelve standing frames are under tools/hero_knight/source"
    strips = {"idle": (idle, IDLE_FPS, True)}
    for name, (count, fps, loop) in OTHERS.items():
        strips[name] = ([idle[0]] * count, fps, loop)
    entry = {
        "frame_size": [FRAME, FRAME],
        "origin": [FRAME // 2, FOOT],
        "anchor": "feet",
        "ppu": PPU,
        "animations": {},
    }
    for name, (frames, fps, loop) in strips.items():
        file_name = f"hero_{name}.png"
        path = os.path.join(out_dir, file_name)
        strip(frames).save(path)
        entry["animations"][name] = {"file": file_name, "frames": len(frames), "fps": fps, "loop": loop}
    spec_path = os.path.join(out_root, "sprites.json")
    source = spec_path if os.path.exists(spec_path) else os.path.join(SPRITES, "sprites.json")
    with open(source, encoding="utf-8") as handle:
        spec = json.load(handle)
    spec["hero"] = entry
    with open(spec_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(spec, handle, indent=2)
        handle.write("\n")
    # The strips' slicing follows the entry: their .meta files are written again (same GUIDs).
    for name in strips:
        path = os.path.join(out_dir, f"hero_{name}.png")
        if unity_meta.inside_assets(path) and os.path.exists(path + ".meta"):
            os.remove(path + ".meta")
        unity_meta.ensure_meta(path)
    unity_meta.ensure_meta(spec_path)
    print(f"hero: {FRAME}x{FRAME} frames at {PPU} px a unit, {len(idle)} standing frames")


if __name__ == "__main__":
    main()
