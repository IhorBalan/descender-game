#!/usr/bin/env python3
"""Export the NPC strips into Assets/Art/Sprites/<name>/ and update sprites.json.

    python3 tools/npc_sprites/export.py                   # write strips and the spec
    python3 tools/npc_sprites/export.py --preview         # also write preview_npcs.png
    py tools/npc_sprites/export.py --out scratch/sprites  # Windows; write somewhere else

Needs Python 3 with Pillow. A new file under Assets/ gets its .meta
(unity_meta); an existing .meta is never touched. The rig and the characters are in npcs.py; the
hero, the enemies and the other placeholders are not touched.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import npcs  # noqa: E402
from pix import scale, strip  # noqa: E402

sys.path.insert(0, os.path.join(os.path.dirname(HERE), "unity"))
import unity_meta  # noqa: E402

SPRITES = os.path.join(unity_meta.ASSETS, "Art", "Sprites")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--out", default=SPRITES, help="the sprites folder to write into (default: Assets/Art/Sprites)")
    parser.add_argument("--preview", action="store_true", help="also write preview_npcs.png beside this script")
    args = parser.parse_args()
    out_root = os.path.abspath(args.out)
    spec_path = os.path.join(out_root, "sprites.json")
    with open(spec_path if os.path.exists(spec_path) else os.path.join(SPRITES, "sprites.json")) as f:
        spec = json.load(f)
    rows = []
    for name in npcs.CHARACTERS:
        anims = npcs.build(name)
        out = os.path.join(out_root, name)
        os.makedirs(out, exist_ok=True)
        first = anims["idle"][0][0]
        fw, fh = first.size
        bottom = max(y for y in range(fh) for x in range(fw) if first.getpixel((x, y))[3] > 0)
        info = {"frame_size": [fw, fh], "origin": [fw // 2, bottom + 1], "anchor": "feet",
                "animations": {}}
        for anim_name, (frames, fps) in anims.items():
            s = strip(frames)
            file_name = f"{name}_{anim_name}.png"
            s.save(os.path.join(out, file_name))
            unity_meta.ensure_meta(os.path.join(out, file_name))
            rows.append(s)
            info["animations"][anim_name] = {"file": file_name, "frames": len(frames),
                                             "fps": fps, "loop": True}
        spec[name] = info
        print("wrote", out)
    with open(spec_path, "w") as f:
        json.dump(spec, f, indent=2)
        f.write("\n")
    unity_meta.ensure_meta(spec_path)
    if args.preview:
        w = max(r.width for r in rows)
        sheet = Image.new("RGBA", (w, sum(r.height for r in rows)), (90, 100, 130, 255))
        y = 0
        for r in rows:
            sheet.alpha_composite(r, (0, y))
            y += r.height
        path = os.path.join(HERE, "preview_npcs.png")
        scale(sheet, 3).save(path)
        print("wrote", path)


if __name__ == "__main__":
    main()
