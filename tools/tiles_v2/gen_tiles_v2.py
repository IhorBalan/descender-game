#!/usr/bin/env python3
"""Draws the Tiles V2 art into Assets/Art/TilesV2: one sheet a material with its
full autotile frame set, the blend overlays between materials, the parallax
backdrop, and tiles_v2.json, the manifest the Unity builder reads to make the
Rule Tiles. The standard this code encodes is written down in
docs/art/tiles-v2.md; change both together.

    python3 -I tools/tiles_v2/gen_tiles_v2.py                    # Python 3 with Pillow and numpy
    py -I tools/tiles_v2/gen_tiles_v2.py --preview scratch/v2.png  # also lay out the sample map

The standard, in short:
  * a tile is 16 x 16 px, imported at 16 PPU: one tile is one Unity unit and one
    collision cell; nothing is scaled, rotated or drawn at another density;
  * every terrain has the same 47 frames (the 8-neighbour "blob" set: edges,
    outer corners, inner corners) in 3 variants, plus two slopes; walls have
    the 47 frames too;
  * each material has one ramp of six colours plus an outline colour, and
    draws with nothing else;
  * light comes from the top left: an exposed top edge gets a highlight, a
    left edge a lighter rim, bottom and right edges a shade;
  * every edge open to air gets a 1 px outline in the material's outline
    colour; edges between joined materials get none (a blend overlay hides
    the seam).

Everything is drawn from fixed seeds, so a run reproduces the same pixels. A
new file under Assets/ gets its .meta (unity_meta); an existing .meta is never
touched.
"""
from __future__ import annotations

import argparse
import json
import os

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_backdrop
import tv2_earth
import tv2_eden
import tv2_heaven
import tv2_decor
import tv2_liquid
import tv2_preview
import tv2_terrain
from tv2_terrain import BLEND_MASKS, THINGS, draw_blend, draw_frame, draw_slope, ledge_sheet, prop_sheet, things_sheet

# --- Sheets and manifest ------------------------------------------------------


def sheet_for(material: dict) -> tuple[Image.Image, list]:
    """A material's sheet: the 47 frames in variant blocks of 6 rows, then a row
    of slopes (each slope's variants side by side)."""
    rows_per_variant = (len(FRAMES) + COLUMNS - 1) // COLUMNS
    kinds = slopes_of(material)
    slope_rows = (len(kinds) * VARIANTS + COLUMNS - 1) // COLUMNS
    rows = rows_per_variant * VARIANTS + slope_rows
    sheet = np.zeros((rows * TILE, COLUMNS * TILE, 4), dtype=np.uint8)
    sprites = []
    for v in range(VARIANTS):
        for i, mask in enumerate(FRAMES):
            cx, cy = frame_cell(i)
            cy += v * rows_per_variant
            sheet[cy * TILE : (cy + 1) * TILE, cx * TILE : (cx + 1) * TILE] = draw_frame(material, mask, v)
            sprites.append((f"tv2_{material['key']}_f{mask}_v{v}", cx, cy))
    for s, kind in enumerate(kinds):
        for v in range(VARIANTS):
            cx, cy = slope_cell(s, v)
            sheet[cy * TILE : (cy + 1) * TILE, cx * TILE : (cx + 1) * TILE] = draw_slope(material, kind, v)
            sprites.append((f"tv2_{material['key']}_{kind}_v{v}", cx, cy))
    return Image.fromarray(sheet, "RGBA"), sprites


def blend_sheet(blend: dict) -> tuple[Image.Image, list]:
    rows_per_variant = (len(BLEND_MASKS) + COLUMNS - 1) // COLUMNS
    sheet = np.zeros((rows_per_variant * VARIANTS * TILE, COLUMNS * TILE, 4), dtype=np.uint8)
    sprites = []
    for v in range(VARIANTS):
        for i, sides in enumerate(BLEND_MASKS):
            cx, cy = frame_cell(i)
            cy += v * rows_per_variant
            sheet[cy * TILE : (cy + 1) * TILE, cx * TILE : (cx + 1) * TILE] = draw_blend(blend, sides, v)
            sprites.append((f"tv2_{blend['key']}_s{sides}_v{v}", cx, cy))
    return Image.fromarray(sheet, "RGBA"), sprites


def sheet_meta(path: str, image: Image.Image, sprites: list, shapes: dict | None = None) -> str:
    """The .meta of a sheet of tile-sized sprites, each (name, column, row). `shapes`
    gives a sprite its collider outline (points in pixels from its middle) in place
    of the one Unity would trace round its pixels."""
    h = image.size[1]
    text = rect_meta(path, [(name, cx * TILE, h - (cy + 1) * TILE, TILE, TILE, (0.5, 0.5)) for name, cx, cy in sprites])
    for name, points in (shapes or {}).items():
        start = text.index(f"      name: {name}\n")
        empty = "      physicsShape: []\n"
        at = text.index(empty, start)
        lines = "".join(("      - - " if i == 0 else "        - ") + f"{{x: {x}, y: {y}}}\n" for i, (x, y) in enumerate(points))
        text = text[:at] + "      physicsShape:\n" + lines + text[at + len(empty) :]
    return text


def slope_shapes(material: dict) -> dict:
    """The collider of each slope sprite: the clean shape under its diagonal, not
    the ragged outline of its pixels, so a body slides up it and stands on it."""
    h = TILE // 2
    outlines = {
        "up_right": [(-h, -h), (h, -h), (h, h)],
        "up_left": [(-h, -h), (h, -h), (-h, h)],
        "steep_right_low": [(-h, -h), (h, -h), (h, h), (0, h)],
        "steep_right_high": [(0, -h), (h, -h), (h, h)],
        "steep_left_low": [(-h, -h), (h, -h), (0, h), (-h, h)],
        "steep_left_high": [(-h, -h), (0, -h), (-h, h)],
    }
    shapes = {}
    for kind in slopes_of(material):
        for v in range(VARIANTS):
            shapes[f"tv2_{material['key']}_{kind}_v{v}"] = outlines[kind]
    return shapes


def ledge_shapes() -> dict:
    """The collider of each ledge sprite: the strip at the top of its cell that carries."""
    half = TILE // 2
    strip = [(-half, half - LEDGE_THICK), (half, half - LEDGE_THICK), (half, half), (-half, half)]
    return {f"tv2_ledge_{style}_{piece}": strip for style in LEDGES for piece in LEDGE_PIECES}


def rect_meta(path: str, entries: list) -> str:
    """The .meta of a sheet whose sprites are (name, x, y from the bottom, width, height, pivot)."""
    return unity_meta.texture_meta(unity_meta.guid_for(unity_meta.rel(path)), ppu=PPU, point_filter=True, sprites=entries)


def backdrop_meta(path: str, pivot=(0.5, 0.0)) -> str:
    # Full Rect, so the sprite can be drawn tiled. A picture of the Earth's has its pivot on its
    # floor line (tv2_earth.pivot_of), the rest the bottom middle.
    text = unity_meta.texture_meta(unity_meta.guid_for(unity_meta.rel(path)), ppu=PPU, point_filter=True, pivot=pivot)
    return text.replace("spriteMeshType: 1", "spriteMeshType: 0").replace("wrapU: 1", "wrapU: 0")


def manifest() -> dict:
    return {
        "tile": TILE,
        "ppu": PPU,
        "view": {"width": VIEW[0], "height": VIEW[1]},
        "variants": VARIANTS,
        "bits": {"N": N, "NE": NE, "E": E, "SE": SE, "S": S, "SW": SW, "W": W, "NW": NW},
        "frames": FRAMES,
        "slopes": SLOPES,
        "steep": STEEP,
        "materials": [
            {
                "key": m["key"],
                "kind": m["kind"],
                "group": m["group"],
                "sheet": m["key"] + ".png",
                "slopes": m["slopes"],
                "steep": bool(m.get("steep", False)),
                "emits": list(m.get("emits", (0.0, 0.0, 0.0))),
                "open": bool(m.get("open", False)),
            }
            for m in MATERIALS
        ],
        "blends": [
            {"key": b["key"], "onto": b["onto"], "from": b["from"], "sheet": "blend_" + b["key"] + ".png", "masks": BLEND_MASKS}
            for b in BLENDS
        ],
        "legend": [{"character": c, "layer": layer, "key": key, "slope": slope} for c, layer, key, slope in LEGEND],
        "ledges": {"sheet": "ledge.png", "thick": LEDGE_THICK, "keys": LEDGES, "pieces": LEDGE_PIECES},
        "things": {"sheet": "things.png", "names": THINGS},
        "props": [
            {
                "key": p["key"],
                "sheet": p["key"] + ".png",
                "frames": p["frames"],
                "colour": list(p["colour"]),
                "intensity": p["intensity"],
                "embers": p["embers"],
            }
            for p in PROPS
        ],
        "regions": [
            {
                "key": r["key"],
                "name": r["name"],
                "subtitle": r["subtitle"],
                "ambient": list(r["ambient"]),
                "cave": "backdrop_cave_" + r["key"] if r["cave"] else "",
                "caveTop": "backdrop_cave_top_" + r["key"] if r["cave"] else "",
                "far": "backdrop_far_" + r["key"] if r["cave"] else "",
                "air": (
                    {
                        "rate": r["air"][0],
                        "colour": [round(v / 255, 4) for v in hexes(r["air"][1])[0]] + [r["air"][2]],
                        "across": list(r["air"][3]),
                        "up": list(r["air"][4]),
                        "life": r["air"][5],
                        "lit": r["air"][6],
                    }
                    if r["air"]
                    else {"rate": 0, "colour": [1, 1, 1, 0], "across": [0, 0], "up": [0, 0], "life": 1, "lit": False}
                ),
            }
            for r in REGIONS
        ],
        "liquids": [
            {
                "key": q["key"],
                "sheet": q["key"] + ".png",
                "frames": tv2_liquid.LIQUID_FRAMES,
                "fps": q["fps"],
                "lit": q["lit"],
                "keeps": list(q["keeps"]),
                "emits": list(q["emits"]),
                "swim": q["swim"],
                "hazard": q["hazard"],
                "order": q["order"],
            }
            for q in tv2_liquid.LIQUIDS
        ],
        "decor": {
            "sheet": "decor.png",
            "floorSalt": tv2_decor.FLOOR_SALT,
            "ceilingSalt": tv2_decor.CEILING_SALT,
            "rules": [
                {
                    "key": d["key"],
                    "anchor": d["anchor"],
                    "on": d["on"],
                    "where": d["where"],
                    "chance": d["chance"],
                    "variants": d["variants"],
                    "min": d.get("tall", [1, 1])[0],
                    "max": d.get("tall", [1, 1])[1],
                    "first": d.get("first", d["key"]),
                    "middle": d.get("middle", d["key"]),
                    "last": d.get("last", d["key"]),
                }
                for d in tv2_decor.DECOR
            ],
        },
        "shade": {"sheet": "shade.png", "masks": tv2_decor.SHADE_MASKS},
        "glint": {
            "sheet": "glint.png",
            "on": [m["key"] for m in MATERIALS if m.get("ore")],
            "fps": tv2_decor.GLINT_FPS,
            "loop": tv2_decor.GLINT_LOOP,
        },
        "light": {
            "ambient": LIGHT["ambient"],
            "ambientColour": list(LIGHT["ambient_colour"]),
            "airKeeps": LIGHT["air_keeps"],
            "solidKeeps": LIGHT["solid_keeps"],
            "torchColour": list(LIGHT["torch_colour"]),
            "torchIntensity": LIGHT["torch_intensity"],
            "torchGlow": LIGHT["torch_glow"],
            "torchGlowRadius": LIGHT["torch_glow_radius"],
            "wallTint": list(LIGHT["wall_tint"]),
        },
        "day": {
            "length": DAY_LENGTH,
            "start": DAY_START,
            "sunrise": SUN_HOURS[0],
            "sunset": SUN_HOURS[1],
            "keys": [
                {
                    "hour": k["hour"],
                    "light": list(k["light"]),
                    "top": list(k["top"]),
                    "low": list(k["low"]),
                    "tint": list(k["tint"]),
                    "stars": k["stars"],
                }
                for k in DAY_KEYS
            ],
        },
        "sky": {"bands": tv2_backdrop.SKY_BANDS, "order": tv2_backdrop.SKY_ORDER, "cloudDrift": tv2_backdrop.CLOUD_DRIFT, "caveScroll": tv2_backdrop.CAVE_SCROLL, "caveOrder": tv2_backdrop.CAVE_ORDER, "farScroll": FAR_SCROLL},
        "backdrops": tv2_backdrop.BACKDROPS,
    }


def write(path: str, image: Image.Image, meta: str | None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    if unity_meta.inside_assets(path):
        unity_meta.ensure_meta(os.path.dirname(path))  # the folders
        if meta is not None:
            unity_meta.write_if_missing(path + ".meta", meta)
        else:
            unity_meta.ensure_meta(path)


def generate(out: str):
    """Draws and writes everything; returns (the sheets by key, the loose sprites by name)."""
    images: dict[str, Image.Image] = {}
    sprites: dict[str, Image.Image] = {}

    def keep(image: Image.Image, cells: list):
        for name, cx, cy in cells:
            sprites[name] = image.crop((cx * TILE, cy * TILE, (cx + 1) * TILE, (cy + 1) * TILE))

    for material in MATERIALS:
        image, cells = sheet_for(material)
        path = os.path.join(out, material["key"] + ".png")
        write(path, image, sheet_meta(path, image, cells, slope_shapes(material)))
        images[material["key"]] = image
    for blend in BLENDS:
        image, cells = blend_sheet(blend)
        path = os.path.join(out, "blend_" + blend["key"] + ".png")
        write(path, image, sheet_meta(path, image, cells))
        images["blend_" + blend["key"]] = image
    sheets = [(p["key"], prop_sheet(p)) for p in PROPS]
    sheets += [("shade", tv2_decor.shade_sheet()), ("glint", tv2_decor.glint_sheet())]
    sheets += [(q["key"], tv2_liquid.liquid_sheet(q)) for q in tv2_liquid.LIQUIDS]
    sheets += [("ledge", ledge_sheet()), ("things", things_sheet())]
    for key, (image, cells) in sheets:
        path = os.path.join(out, key + ".png")
        write(path, image, sheet_meta(path, image, cells, ledge_shapes() if key == "ledge" else None))
        images[key] = image
        keep(image, cells)
    image, entries = tv2_decor.decor_sheet()
    path = os.path.join(out, "decor.png")
    write(path, image, rect_meta(path, entries))
    images["decor"] = image
    for name, x, y, w, h, _ in entries:
        sprites[name] = image.crop((x, image.height - y - h, x + w, image.height - y))
    for name, image in tv2_backdrop.backdrops().items():
        path = os.path.join(out, name + ".png")
        stood = next((module for module in (tv2_earth, tv2_heaven, tv2_eden) if name in module.SINK_OF), None)
        write(path, image, backdrop_meta(path, stood.pivot_of(name) if stood else (0.5, 0.0)))
        images[name] = image
    path = os.path.join(out, "tiles_v2.json")
    with open(path, "w", newline="\n") as handle:
        json.dump(manifest(), handle, indent=2)
        handle.write("\n")
    if unity_meta.inside_assets(path):
        unity_meta.ensure_meta(path)
    return images, sprites


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=OUT, help="where to write (default: Assets/Art/TilesV2)")
    parser.add_argument("--preview", help="also write the sample map laid out to this PNG")
    parser.add_argument("--hour", type=float, help="the preview's hour of the day, 0 to 24 (default: the day's start)")
    parser.add_argument("--view", help="the preview shows only the camera's view of a place: a @view name of the map, or column,row")
    parser.add_argument("--scale", type=float, default=3, help="the preview's scale (default 3; below 1 for a big map, 0.25 is 4 px a tile)")
    parser.add_argument("--map", default=SAMPLE_MAP, help="the map the preview lays out (default: the sample map)")
    parser.add_argument("--unlit", action="store_true", help="the preview without the tile light")
    args = parser.parse_args(argv)
    art = generate(args.out)
    if args.preview:
        scale = int(args.scale) if args.scale >= 1 else args.scale
        tv2_preview.preview(art, args.preview, args.hour, args.view, scale, not args.unlit, args.map)
    print(f"Tiles V2: {len(MATERIALS)} materials x {len(FRAMES)} frames x {VARIANTS} variants -> {args.out}")


if __name__ == "__main__":
    main()
