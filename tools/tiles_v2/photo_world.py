#!/usr/bin/env python3
"""Photographs the whole Tiles V2 world from the Unity editor, every art pixel of it,
so it can be looked over zoomed in: every torch, ledge and blade of grass.

    python tools/tiles_v2/photo_world.py                    # the world, as it is at the day's start
    python tools/tiles_v2/photo_world.py --hour 21          # at night
    python tools/tiles_v2/photo_world.py --pixels 8         # half the size (16 is every art pixel)
    python tools/tiles_v2/photo_world.py --sample           # the sample map
    python tools/tiles_v2/photo_world.py --paradise         # the Paradise level (to Logs/TilesV2/photo_paradise)
    python tools/tiles_v2/photo_world.py --out D:/pictures  # somewhere else than Logs/TilesV2/photo
    python tools/tiles_v2/photo_world.py --no-labels        # without the labelled pictures

It writes, under the folder:

    world.png             the whole map in one picture (the world at 16 px a tile is 8640 x 12480)
    regions/NN_key.png    the same, a region at a time, top to bottom
    4k/rowR_colC.png      the same, cut into sheets of 3840 x 2160, for a viewer that will not open the whole
    labelled.png          the whole map with what the map marks named on it: every rest (C1 ...), ability,
                          gate (G1 ...), secret (S1 ...), relic (R1 ...), upgrade (U1 ...), sign (T1 ...),
                          platform that does not stay (P1 ...), wind (W1 ...), light of Paradise (L1 ...),
                          the door they open (D1) and dark stretch, the regions'
                          names, and columns and rows along its edges
    labelled/NN_key.png   the same, a region at a time
    labels.txt            the list of those names: what each is, its column and row, its region

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

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WORLD_SCENE = "Assets/Scenes/Sandbox/TilesV2Descent.unity"
PARADISE_SCENE = "Assets/Scenes/Sandbox/TilesV2Paradise.unity"
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


def join(pieces: str, out: str, labels: bool = True) -> tuple[list[str], tuple[int, int]]:
    """Joins the pieces into the whole picture, the regions and the 4K sheets, and the same
    with the map's marks named on it; returns what it wrote and the whole picture's size."""
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
    for folder in ("regions", "4k", "labelled"):
        shutil.rmtree(os.path.join(out, folder), ignore_errors=True)
    for name in ("labelled.png", "labels.txt"):
        if os.path.exists(os.path.join(out, name)):
            os.remove(os.path.join(out, name))
    for number, region in enumerate(index["regions"]):
        save(whole.crop((0, region["top"] * px, whole.width, region["end"] * px)), "regions", f"{number:02d}_{region['key']}.png")
    wide, tall = SHEET
    if whole.width > wide or whole.height > tall:
        for row, top in enumerate(range(0, whole.height, tall)):
            for column, left in enumerate(range(0, whole.width, wide)):
                sheet = whole.crop((left, top, min(left + wide, whole.width), min(top + tall, whole.height)))
                save(sheet, "4k", f"row{row + 1}_col{column + 1}.png")
    if labels:
        size = whole.size
        whole, lines = label(whole, index)
        save(whole, "labelled.png")
        for number, region in enumerate(index["regions"]):
            save(whole.crop((0, region["top"] * px, whole.width, region["end"] * px)), "labelled", f"{number:02d}_{region['key']}.png")
        with open(os.path.join(out, "labels.txt"), "w", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")
        written.append(os.path.join(out, "labels.txt"))
        return written, size
    return written, whole.size


# How each kind of mark is named on the labelled picture: its letter (none: its own
# words) and its colour.
LABELS = {
    "checkpoint": ("C", (120, 235, 140)),
    "ability": ("", (255, 225, 90)),
    "barrier": ("G", (255, 150, 70)),
    "seal": ("G", (255, 150, 70)),
    "secret": ("S", (200, 200, 210)),
    "relic": ("R", (255, 205, 60)),
    "upgrade": ("U", (255, 120, 150)),
    "sign": ("T", (215, 180, 125)),
    "platform": ("P", (150, 215, 255)),
    "wind": ("W", (170, 240, 240)),
    "end": ("", (255, 255, 255)),
    "beacon": ("L", (255, 240, 150)),
    "sanctum": ("D", (255, 240, 150)),
}
DARK = (190, 120, 255)


def font(size: int):
    """A font of a size in pixels, or the small one every Pillow has."""
    try:
        return ImageFont.load_default(size)
    except TypeError:  # a Pillow from before 10.1
        return ImageFont.load_default()


def named(index: dict) -> list[dict]:
    """The marks to name on the picture: each with its name (a letter and its number among
    its kind, counted from the top of the map down and from the left), what it is in
    words, the cells it takes up (left, top, wide, tall) and its region."""
    counts: dict[str, int] = {}
    out = []
    for mark in sorted(index["marks"], key=lambda m: (m["row"], m["column"])):
        kind, x, row, words = mark["kind"], mark["column"], mark["row"], mark["words"].split()
        if kind not in LABELS:
            continue
        letter, colour = LABELS[kind]
        box, what = (x, row, 1, 1), kind
        if kind == "barrier":
            box, what = (x, row, 1, int(words[0])), "gate: a barrier of light (Light Dash)"
        elif kind == "seal":
            box = (x, row, int(words[1]), int(words[2]))
            what = "gate: a seal a bolt breaks (Holy Light)" if words[0] == "bolt" else "gate: a slab a slam breaks (Smite)"
        elif kind == "secret":
            box, what = (x, row, int(words[0]), int(words[1])), "secret wall"
        elif kind == "platform":
            box, what = (x, row, int(words[0]), 1), f"platform that {'crumbles' if words[1] == 'crumble' else 'blinks'}"
        elif kind == "wind":
            box, what = (x, row, int(words[0]), int(words[1])), "wind"
        elif kind == "upgrade":
            what = "upgrade: " + ("a heart" if words[0] == "Heart" else "a draught")
        elif kind == "sign":
            what = f"{words[0]}: " + " ".join(words[1:])
        elif kind == "checkpoint":
            what = "rest by the Pit (travel)" if words[:1] == ["pit"] else "rest"
        elif kind == "ability":
            what = "ability: " + words[0]
        elif kind == "beacon":
            what = "a light of Paradise: " + words[0]
        elif kind == "sanctum":
            box, what = (x, row, 1, int(words[0])), f"the door {words[1]} lights open"
        if letter:
            counts[letter] = counts.get(letter, 0) + 1
            name = f"{letter}{counts[letter]}"
        else:
            name = words[0] if kind == "ability" else "END"
        region = next((r["key"] for r in index["regions"] if r["top"] <= row < r["end"]), "")
        out.append(dict(name=name, what=what, box=box, colour=colour, region=region))
    return out


def label(whole: Image.Image, index: dict) -> tuple[Image.Image, list[str]]:
    """The picture with its marks named on it, and the list of the names in words."""
    px = index["pixels"]
    picture = whole.copy()
    draw = ImageDraw.Draw(picture)
    small, big = font(max(9, px + px // 4)), font(max(12, px * 3))
    line = max(1, px // 8)
    # The dark stretches: an outline, no name.
    for mark in index["marks"]:
        if mark["kind"] == "dark":
            wide, tall = (int(word) for word in mark["words"].split()[:2])
            x, y = mark["column"] * px, mark["row"] * px
            draw.rectangle((x, y, x + wide * px - 1, y + tall * px - 1), outline=DARK, width=line)
    # The regions: a line where each starts and its name under it.
    for region in index["regions"]:
        y = region["top"] * px
        draw.line((0, y, picture.width, y), fill=(255, 255, 255), width=line)
        text = f"{region['key']}  (rows {region['top']} .. {region['end'] - 1})"
        at = (3 * px, y + px // 2)
        draw.rectangle(draw.textbbox(at, text, font=big), fill=(0, 0, 0))
        draw.text(at, text, font=big, fill=(255, 255, 255))
    # Columns along the top and the foot, rows along both sides: a tick every 10, a number every 20.
    for column in range(0, index["width"], 10):
        x = column * px
        for y0 in (0, picture.height - px):
            draw.line((x, y0, x, y0 + px - 1), fill=(255, 255, 255), width=line)
        if column % 20 == 0:
            for y in (px, picture.height - 3 * px):
                draw.rectangle(draw.textbbox((x + 2, y), str(column), font=small), fill=(0, 0, 0))
                draw.text((x + 2, y), str(column), font=small, fill=(255, 255, 255))
    for row in range(0, index["height"], 10):
        y = row * px
        for x0 in (0, picture.width - px):
            draw.line((x0, y, x0 + px - 1, y), fill=(255, 255, 255), width=line)
        if row % 20 == 0:
            text = str(row)
            for x in (px, picture.width - px - int(draw.textlength(text, font=small)) - 2):
                draw.rectangle(draw.textbbox((x, y + 2), text, font=small), fill=(0, 0, 0))
                draw.text((x, y + 2), text, font=small, fill=(255, 255, 255))
    # The marks: a frame round the cells of each and its name over it.
    lines = []
    for mark in named(index):
        x, row, wide, tall = mark["box"]
        left, top = x * px, row * px
        draw.rectangle((left - line, top - line, left + wide * px + line - 1, top + tall * px + line - 1), outline=mark["colour"], width=line)
        width = int(draw.textlength(mark["name"], font=small))
        high = getattr(small, "size", 10)
        at = (min(max(left + (wide * px - width) // 2, 0), picture.width - width - 2), max(top - high - 3 * line - 2, 0))
        draw.rectangle(draw.textbbox(at, mark["name"], font=small), fill=(0, 0, 0))
        draw.text(at, mark["name"], font=small, fill=mark["colour"])
        lines.append(f"{mark['name']:<10} column {x:>3}  row {row:>3}  {mark['region']:<10} {mark['what']}")
    return picture, lines


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=os.path.join(ROOT, "Logs", "TilesV2", "photo"), help="where to write (default: Logs/TilesV2/photo)")
    parser.add_argument("--pixels", type=int, default=16, choices=range(1, 17), metavar="1..16", help="pixels a tile (default 16: every art pixel)")
    parser.add_argument("--hour", type=float, help="the hour of the day, 0 to 24 (default: the scene's own, the day's start)")
    parser.add_argument("--sample", action="store_true", help="the sample map instead of the world")
    parser.add_argument("--paradise", action="store_true", help="the Paradise level instead of the world (to Logs/TilesV2/photo_paradise unless --out says)")
    parser.add_argument("--no-labels", action="store_true", help="leave out the pictures with the map's marks named on them")
    args = parser.parse_args(argv)
    if args.paradise and args.out == parser.get_default("out"):
        args.out = os.path.join(ROOT, "Logs", "TilesV2", "photo_paradise")
    out = os.path.abspath(args.out)
    pieces = os.path.join(out, "pieces")
    shutil.rmtree(pieces, ignore_errors=True)
    os.makedirs(pieces, exist_ok=True)
    started = time.time()
    photograph(pieces, SAMPLE_SCENE if args.sample else PARADISE_SCENE if args.paradise else WORLD_SCENE, args.pixels, args.hour)
    print("Joining the pieces...")
    written, size = join(pieces, out, not args.no_labels)
    shutil.rmtree(pieces, ignore_errors=True)
    megabytes = sum(os.path.getsize(path) for path in written) / (1024 * 1024)
    print(f"The whole map: {written[0]} ({size[0]} x {size[1]} px)")
    print(f"and {len(written) - 1} files beside it (by region, in 4K sheets, with labels); {megabytes:.0f} MB in all, {time.time() - started:.0f} s.")
    if not args.no_labels:
        print(f"What is where, by name: {os.path.join(out, 'labels.txt')}")


if __name__ == "__main__":
    main()
