#!/usr/bin/env python3
"""Export the enemy strips into assets/sprites/<name>/ and update sprites.json.

    python3 tools/enemy_sprites/export.py            # write strips and the spec
    python3 tools/enemy_sprites/export.py --preview  # also write preview_enemies.png

Needs Python 3 with Pillow. The rigs are the art thread's prototype sources
(imp.py, angel.py, brute.py, watcher.py on pix.py), with the hurt and death
strips added here, plus the hound and the hanged thing (hound.py, hanged.py)
and the region bodies (slug.py, hoarder.py, burned.py, shade.py, siren.py).
The hero, the NPCs and the other placeholders are not touched. The origins are fixed so the enemies stand where they always did.
"""
from __future__ import annotations

import importlib
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pix import scale, strip  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(HERE))
SPRITES = os.path.join(ROOT, "assets", "sprites")
SPEC = os.path.join(SPRITES, "sprites.json")

## name, module, origin, anchor
CHARACTERS = [
    ("imp", "imp", [24, 40], "feet"),
    ("angel_guard", "angel", [36, 64], "feet"),
    ("brute", "brute", [44, 95], "feet"),
    ("watcher", "watcher", [30, 29], "centre"),
    ("hound", "hound", [28, 32], "feet"),
    ("hanged", "hanged", [20, 64], "feet"),
    ("slug", "slug", [36, 40], "feet"),
    ("hoarder", "hoarder", [28, 48], "feet"),
    ("burned", "burned", [36, 64], "feet"),
    ("shade", "shade", [24, 22], "centre"),
    ("siren", "siren", [32, 24], "centre"),
]
LOOPING = ("idle", "run", "walk")


def main() -> None:
    with open(SPEC) as f:
        spec = json.load(f)
    rows = []
    for name, module, origin, anchor in CHARACTERS:
        anims = importlib.import_module(module).build()
        out = os.path.join(SPRITES, name)
        os.makedirs(out, exist_ok=True)
        fw, fh = anims["idle"][0][0].size
        info = {"frame_size": [fw, fh], "origin": origin, "anchor": anchor, "animations": {}}
        for anim_name, (frames, fps) in anims.items():
            s = strip(frames)
            file_name = f"{name}_{anim_name}.png"
            s.save(os.path.join(out, file_name))
            rows.append(s)
            info["animations"][anim_name] = {"file": file_name, "frames": len(frames),
                                             "fps": fps, "loop": anim_name in LOOPING}
        # The old spec's sheet entry is dropped: the game reads the strips.
        spec[name] = info
        print("wrote", out)
    with open(SPEC, "w") as f:
        json.dump(spec, f, indent=2)
        f.write("\n")
    if "--preview" in sys.argv:
        w = max(r.width for r in rows)
        sheet = Image.new("RGBA", (w, sum(r.height for r in rows)), (90, 100, 130, 255))
        y = 0
        for r in rows:
            sheet.alpha_composite(r, (0, y))
            y += r.height
        path = os.path.join(HERE, "preview_enemies.png")
        scale(sheet, 2).save(path)
        print("wrote", path)


if __name__ == "__main__":
    main()
