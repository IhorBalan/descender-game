#!/usr/bin/env python3
"""Write a SpriteFrames resource for every character in godot_assets/sprites/sprites.json.

    python3 tools/sprite_frames.py            # every character
    python3 tools/sprite_frames.py imp hero   # just these

Each character gets godot_assets/sprites/<name>/<name>.tres: one animation per
strip, cut into frames at the spec's frame size, with the spec's fps and
loop flag, and the strip's origin point (feet or centre, in frame pixels)
stored as the resource's `origin` metadata. The game loads these (see
scripts/anim_loader.gd) and the editor opens them in the SpriteFrames
panel, so timing can be tried there; the spec stays the source, so put a
change that should last into sprites.json (or the tool that writes it)
and run this again. The art tools call this themselves after they write
the spec.

Needs only the standard library.
"""
from __future__ import annotations

import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPRITES = os.path.join(ROOT, "godot_assets", "sprites")
SPEC = os.path.join(SPRITES, "sprites.json")


def tres_text(name: str, info: dict) -> str:
    """The .tres text of one character's SpriteFrames."""
    frame_w, frame_h = (int(v) for v in info["frame_size"])
    origin_x, origin_y = (int(v) for v in info["origin"])
    anims = info["animations"]
    ext = []
    subs = []
    entries = []
    for anim_name, anim in anims.items():
        ext_id = "%d_%s" % (len(ext) + 1, anim_name)
        ext.append(
            '[ext_resource type="Texture2D" path="res://godot_assets/sprites/%s/%s" id="%s"]'
            % (name, anim["file"], ext_id)
        )
        frames = []
        for i in range(int(anim["frames"])):
            sub_id = "AtlasTexture_%s_%d" % (anim_name, i)
            subs.append(
                '[sub_resource type="AtlasTexture" id="%s"]\natlas = ExtResource("%s")\n'
                "region = Rect2(%d, 0, %d, %d)\n" % (sub_id, ext_id, i * frame_w, frame_w, frame_h)
            )
            frames.append('{\n"duration": 1.0,\n"texture": SubResource("%s")\n}' % sub_id)
        entries.append(
            '{\n"frames": [%s],\n"loop": %s,\n"name": &"%s",\n"speed": %s\n}'
            % (
                ", ".join(frames),
                "true" if anim["loop"] else "false",
                anim_name,
                _number(anim["fps"]),
            )
        )
    load_steps = len(ext) + len(subs) + 1
    lines = [
        '[gd_resource type="SpriteFrames" load_steps=%d format=3]' % load_steps,
        "",
        *ext,
        "",
        *subs,
        "[resource]",
        "animations = [%s]" % ", ".join(entries),
        "metadata/origin = Vector2(%d, %d)" % (origin_x, origin_y),
        "",
    ]
    return "\n".join(lines)


def _number(value) -> str:
    """A float the way Godot writes one (an integer keeps a trailing .0)."""
    value = float(value)
    return "%d.0" % value if value == int(value) else repr(value)


def write(names: list[str] | None = None) -> list[str]:
    """Writes the resources for these characters (all of them by default)."""
    with open(SPEC, encoding="utf-8") as f:
        spec = json.load(f)
    written = []
    for name in names or spec:
        if name not in spec:
            raise SystemExit("no character called '%s' in %s" % (name, SPEC))
        path = os.path.join(SPRITES, name, "%s.tres" % name)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(tres_text(name, spec[name]))
        written.append(path)
    return written


def main() -> None:
    for path in write(sys.argv[1:]):
        print("wrote", os.path.relpath(path, ROOT))


if __name__ == "__main__":
    main()
