#!/usr/bin/env python3
"""Export the hero's animation strips into Assets/Art/Sprites/hero/ at game scale.

The hero art is a 168x168 SpriteCook sprite (hero_base.png). anim.py cuts it
into layers (wing, halo, legs, sword arm) and poses them frame by frame at full
size; this script shrinks each frame to the 320x180 game scale, adds a dark
outline and rewrites the hero entry in sprites.json:

    python3 tools/hero_cutout/export.py
    py tools/hero_cutout/export.py --out scratch/sprites   # Windows; write somewhere else

Needs Python 3 with Pillow and NumPy. A new file under Assets/ gets its .meta
(unity_meta); an existing .meta is never touched.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import anim  # noqa: E402

sys.path.insert(0, os.path.join(os.path.dirname(HERE), "unity"))
import unity_meta  # noqa: E402

SPRITES = os.path.join(unity_meta.ASSETS, "Art", "Sprites")

SCALE = 0.25  # 165 px tall source -> about 41 px in game
OUTLINE = (24, 16, 32, 255)
# The player script plays these names; "run" uses the cut-out walk cycle.
NAMES = {"idle": "idle", "walk": "run", "attack": "attack", "jump": "jump", "hurt": "hurt"}
LOOPING = {"idle", "run"}
# Attack plays faster so its 6 frames fit Player.ATTACK_TIME (0.28 s) and the
# strike frame lines up with the hitbox window.
FPS = {"attack": 21}


def shrink(img: Image.Image) -> Image.Image:
    """Area-average with premultiplied alpha, then hard alpha and an outline."""
    w, h = round(img.width * SCALE), round(img.height * SCALE)
    a = np.array(img).astype(float) / 255
    a[..., :3] *= a[..., 3:4]
    small = np.array(Image.fromarray((a * 255).astype(np.uint8)).resize((w, h), Image.BOX)).astype(float) / 255
    alpha = small[..., 3:4]
    rgb = np.where(alpha > 0, small[..., :3] / np.maximum(alpha, 1e-6), 0)
    rgb = np.clip((rgb - 0.5) * 1.12 + 0.5, 0, 1)  # keep detail from washing out
    solid = alpha[..., 0] > 0.45
    out = np.zeros((h, w, 4), np.uint8)
    out[solid, :3] = (rgb[solid] * 255).astype(np.uint8)
    out[solid, 3] = 255
    edge = np.zeros_like(solid)
    edge[1:] |= solid[:-1]
    edge[:-1] |= solid[1:]
    edge[:, 1:] |= solid[:, :-1]
    edge[:, :-1] |= solid[:, 1:]
    out[edge & ~solid] = OUTLINE
    return Image.fromarray(out)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=SPRITES, help="the sprites folder to write into (default: Assets/Art/Sprites)")
    out_root = os.path.abspath(parser.parse_args().out)
    out_dir = os.path.join(out_root, "hero")
    os.makedirs(out_dir, exist_ok=True)
    anims = anim.build()
    frame_w, frame_h = round(anim.FW * SCALE), round(anim.FH * SCALE)
    entry = {
        "frame_size": [frame_w, frame_h],
        "origin": [round((anim.OX + 85) * SCALE), frame_h],
        "anchor": "feet",
        "animations": {},
    }
    for source_name, game_name in NAMES.items():
        frames, fps = anims[source_name]
        strip = Image.new("RGBA", (frame_w * len(frames), frame_h))
        for i, frame in enumerate(frames):
            strip.alpha_composite(shrink(frame), (i * frame_w, 0))
        file_name = f"hero_{game_name}.png"
        strip.save(os.path.join(out_dir, file_name))
        unity_meta.ensure_meta(os.path.join(out_dir, file_name))
        entry["animations"][game_name] = {
            "file": file_name,
            "frames": len(frames),
            "fps": FPS.get(game_name, fps),
            "loop": game_name in LOOPING,
        }
    spec_path = os.path.join(out_root, "sprites.json")
    source = spec_path if os.path.exists(spec_path) else os.path.join(SPRITES, "sprites.json")
    with open(source, encoding="utf-8") as f:
        spec = json.load(f)
    spec["hero"] = entry
    with open(spec_path, "w", encoding="utf-8") as f:
        json.dump(spec, f, indent=2)
        f.write("\n")
    unity_meta.ensure_meta(spec_path)
    print(f"hero: {frame_w}x{frame_h} frames, origin {entry['origin']}")


if __name__ == "__main__":
    main()
