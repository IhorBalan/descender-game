#!/usr/bin/env python3
"""Writes one editor scene per region of the world export to scenes/world/.

Each scene is a RegionPreview (scripts/region_preview.gd) with its region
set, so a level can be opened and looked at on its own in the Godot editor
instead of the whole 480 x 459 grid at once. Run it again after an export
adds, removes or renames a region:

    python3 tools/gen_region_scenes.py            # levels/world/world.json
    python3 tools/gen_region_scenes.py path/to/world.json
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "region_preview.gd"
OUT = ROOT / "scenes" / "world"


def region_key(name):
    """The folder name of a region, as EdgeDecor.region_key makes it."""
    key = name.lower().strip()
    dot = key.find(". ")
    if dot > 0 and key[:dot].isdigit():
        key = key[dot + 2 :]
    return re.sub(r"[^a-z0-9]+", "_", key).strip("_")


def main():
    world = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "levels" / "world" / "world.json"
    data = json.loads(world.read_text())
    uid = (SCRIPT.with_suffix(".gd.uid")).read_text().strip()
    res_path = "res://" + world.resolve().relative_to(ROOT).as_posix()
    script_res = "res://" + SCRIPT.relative_to(ROOT).as_posix()
    OUT.mkdir(parents=True, exist_ok=True)
    for old in OUT.glob("*.tscn"):
        old.unlink()
    for index, region in enumerate(data["regions"]):
        key = region_key(region["name"])
        node = re.sub(r"[^A-Za-z]", "", region["name"].title()) or "Region"
        lines = [
            '[gd_scene load_steps=2 format=3]',
            "",
            f'[ext_resource type="Script" uid="{uid}" path="{script_res}" id="1"]',
            "",
            f'[node name="{node}" type="Node2D"]',
            'script = ExtResource("1")',
        ]
        if res_path != "res://levels/world/world.json":
            lines.append(f'world_path = "{res_path}"')
        if index != 0:
            lines.append(f"region = {index}")
        lines.append("")
        path = OUT / f"{index:02d}_{key}.tscn"
        path.write_text("\n".join(lines))
        print(path.relative_to(ROOT))


if __name__ == "__main__":
    main()
