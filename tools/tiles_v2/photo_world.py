#!/usr/bin/env python3
"""Photographs the whole Tiles V2 world from the Unity editor, every art pixel of it,
so it can be looked over zoomed in: every torch, ledge and blade of grass.

    python tools/tiles_v2/photo_world.py                    # the world, as it is at the day's start
    python tools/tiles_v2/photo_world.py --hour 21          # at night
    python tools/tiles_v2/photo_world.py --pixels 8         # half the size (16 is every art pixel)
    python tools/tiles_v2/photo_world.py --sample           # the sample map
    python tools/tiles_v2/photo_world.py --out D:/pictures  # somewhere else than Logs/TilesV2/photo

It writes, under the folder:

    world.png             the whole map in one picture (the world at 16 px a tile is 8640 x 12480)
    regions/NN_key.png    the same, a region at a time, top to bottom
    4k/rowR_colC.png      the same, cut into sheets of 3840 x 2160, for a viewer that will not open the whole

The editor runs without a window (on Windows hidden, so it does not take the
keyboard), builds the world as the game does and photographs it in pieces a
texture can hold (Descender.EditorTools.TilesV2.TilesV2Capture.PhotoWorld); this
tool joins them. The editor must not have the project open already. What it
changes of the project's settings while it runs is put back as it was.
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import shutil
import subprocess
import sys
import time

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WORLD_SCENE = "Assets/Scenes/Sandbox/TilesV2Descent.unity"
SAMPLE_SCENE = "Assets/Scenes/Sandbox/TilesV2Sample.unity"
METHOD = "Descender.EditorTools.TilesV2.TilesV2Capture.PhotoWorld"
LOG = os.path.join("Logs", "tiles-v2-photo.log")
SHEET = (3840, 2160)
# What an editor of another patch version rewrites when it opens the project.
KEPT = ["ProjectSettings/*.asset", "ProjectSettings/*.txt", "ProjectSettings/*.json", "Assets/UniversalRenderPipelineGlobalSettings.asset"]


def editor() -> str:
    """The Unity editor to run: UNITY_EDITOR, or the project's version under Unity Hub, or
    failing that the newest editor of the same release that is installed."""
    chosen = os.environ.get("UNITY_EDITOR")
    if chosen:
        return chosen
    version = ""
    with open(os.path.join(ROOT, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("m_EditorVersion:"):
                version = line.split(":", 1)[1].strip()
    hubs = [
        ("C:/Program Files/Unity/Hub/Editor", "Editor/Unity.exe"),
        ("/Applications/Unity/Hub/Editor", "Unity.app/Contents/MacOS/Unity"),
        (os.path.expanduser("~/Unity/Hub/Editor"), "Editor/Unity"),
    ]
    for hub, inside in hubs:
        exact = os.path.join(hub, version, inside)
        if os.path.exists(exact):
            return exact
    release = ".".join(version.split(".")[:2])
    for hub, inside in hubs:
        near = sorted(glob.glob(os.path.join(hub, release + ".*", inside)))
        if near:
            print(f"Unity {version} is not installed; using {near[-1]}.")
            return near[-1]
    raise SystemExit(f"Unity {version} was not found. Install it through Unity Hub or set UNITY_EDITOR.")


def photograph(folder: str, scene: str, pixels: int, hour: float | None) -> None:
    """Runs the editor to photograph the scene's world in pieces into a folder."""
    unity = editor()
    arguments = ["-batchmode", "-projectPath", ROOT, "-logFile", os.path.join(ROOT, LOG)]
    arguments += ["-executeMethod", METHOD, "-tilesV2Out", folder, "-tilesV2Scene", scene, "-tilesV2Pixels", str(pixels)]
    if hour is not None:
        arguments += ["-tilesV2Hour", repr(float(hour))]
    if os.name == "nt":
        hidden = os.path.join(ROOT, "tools", "hooks", "unity-background.ps1")
        command = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", hidden, unity] + arguments
    else:
        command = [unity] + arguments
    kept = {}
    for pattern in KEPT:
        for path in glob.glob(os.path.join(ROOT, pattern)):
            with open(path, "rb") as handle:
                kept[path] = handle.read()
    os.makedirs(os.path.join(ROOT, "Logs"), exist_ok=True)
    print("Photographing in the Unity editor (a minute or so)...")
    try:
        code = subprocess.call(command, cwd=ROOT)
    finally:
        for path, before in kept.items():
            with open(path, "rb") as handle:
                now = handle.read()
            if now != before:
                with open(path, "wb") as handle:
                    handle.write(before)
    if code != 0 or not os.path.exists(os.path.join(folder, "photo.json")):
        lines = []
        if os.path.exists(os.path.join(ROOT, LOG)):
            with open(os.path.join(ROOT, LOG), encoding="utf-8", errors="replace") as handle:
                lines = [line.rstrip() for line in handle if "error CS" in line or "Exception" in line or "another Unity instance" in line]
        raise SystemExit(
            f"The editor did not photograph the world (exit {code}). Is the project open in Unity? See {LOG}.\n" + "\n".join(lines[:12])
        )


def join(pieces: str, out: str) -> tuple[list[str], tuple[int, int]]:
    """Joins the pieces into the whole picture, the regions and the 4K sheets; returns what it wrote and the whole picture's size."""
    with open(os.path.join(pieces, "photo.json"), encoding="utf-8") as handle:
        index = json.load(handle)
    px, side = index["pixels"], index["piece"]
    whole = Image.new("RGB", (index["width"] * px, index["height"] * px))
    for name in index["pieces"]:
        row, column = (int(part) for part in name[len("piece_") : -len(".png")].split("_"))
        with Image.open(os.path.join(pieces, name)) as piece:
            whole.paste(piece.convert("RGB"), (column * side * px, row * side * px))
    written = []

    def save(image: Image.Image, *parts: str):
        path = os.path.join(out, *parts)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        image.save(path, compress_level=3)
        written.append(path)

    save(whole, "world.png")
    for folder in ("regions", "4k"):
        shutil.rmtree(os.path.join(out, folder), ignore_errors=True)
    for number, region in enumerate(index["regions"]):
        save(whole.crop((0, region["top"] * px, whole.width, region["end"] * px)), "regions", f"{number:02d}_{region['key']}.png")
    wide, tall = SHEET
    if whole.width > wide or whole.height > tall:
        for row, top in enumerate(range(0, whole.height, tall)):
            for column, left in enumerate(range(0, whole.width, wide)):
                sheet = whole.crop((left, top, min(left + wide, whole.width), min(top + tall, whole.height)))
                save(sheet, "4k", f"row{row + 1}_col{column + 1}.png")
    return written, whole.size


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=os.path.join(ROOT, "Logs", "TilesV2", "photo"), help="where to write (default: Logs/TilesV2/photo)")
    parser.add_argument("--pixels", type=int, default=16, choices=range(1, 17), metavar="1..16", help="pixels a tile (default 16: every art pixel)")
    parser.add_argument("--hour", type=float, help="the hour of the day, 0 to 24 (default: the scene's own, the day's start)")
    parser.add_argument("--sample", action="store_true", help="the sample map instead of the world")
    args = parser.parse_args(argv)
    out = os.path.abspath(args.out)
    pieces = os.path.join(out, "pieces")
    shutil.rmtree(pieces, ignore_errors=True)
    os.makedirs(pieces, exist_ok=True)
    started = time.time()
    photograph(pieces, SAMPLE_SCENE if args.sample else WORLD_SCENE, args.pixels, args.hour)
    print("Joining the pieces...")
    written, size = join(pieces, out)
    shutil.rmtree(pieces, ignore_errors=True)
    megabytes = sum(os.path.getsize(path) for path in written) / (1024 * 1024)
    print(f"The whole map: {written[0]} ({size[0]} x {size[1]} px)")
    print(f"and {len(written) - 1} pictures by region and in 4K sheets beside it; {megabytes:.0f} MB in all, {time.time() - started:.0f} s.")


if __name__ == "__main__":
    main()
