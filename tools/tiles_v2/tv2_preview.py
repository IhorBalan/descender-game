#!/usr/bin/env python3
"""Lays the sample map out the way Unity shows it, for a quick look without the
editor: gen_tiles_v2.py --preview. Not a script.

It follows the same rules as the Unity side, with the same numbers: the frame a
tile shows, the blend overlays, the shade on the walls, the scattered decor, the
liquids' top rows, the tile light and the hour's sky. Nothing moves (frame 0 of
every animation) and the backdrops stand where a camera over the whole map would
put them.
"""
from __future__ import annotations

import os

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_backdrop
import tv2_decor
import tv2_light
import tv2_liquid
from tv2_terrain import BLEND_MASKS, BLEND_SIDES


class Layout:
    """The map as grids the rules read: rows run top first."""

    def __init__(self, terrain: list[str], walls: list[str], properties: dict):
        self.height, self.width = len(terrain), max(len(row) for row in terrain)
        self.terrain = [row.ljust(self.width, ".") for row in terrain]
        self.walls = [row.ljust(self.width, ".") for row in walls]
        self.surface = int(properties.get("surface", self.height))
        self.views = properties.get("view", {})

    def inside(self, x: int, row: int) -> bool:
        return 0 <= x < self.width and 0 <= row < self.height

    def ground(self, x: int, row: int):
        """The terrain tile at a cell as `key` or `key:slope`; None for none."""
        return TERRAIN_CHARS.get(self.terrain[row][x]) if self.inside(x, row) else None

    def block(self, x: int, row: int):
        """The material of a full ground tile at a cell; None for air or a slope."""
        name = self.ground(x, row)
        return name if name and ":" not in name else None

    def wall(self, x: int, row: int):
        return WALL_CHARS.get(self.walls[row][x]) if self.inside(x, row) else None

    def liquid(self, x: int, row: int):
        return LIQUID_CHARS.get(self.terrain[row][x]) if self.inside(x, row) else None

    def free(self, x: int, row: int) -> bool:
        return self.inside(x, row) and self.terrain[row][x] == "."

    def marks(self, char: str) -> list[tuple[int, int]]:
        return [(x, row) for row in range(self.height) for x in range(self.width) if self.terrain[row][x] == char]

    def grid(self, test) -> np.ndarray:
        return np.array([[bool(test(x, row)) for x in range(self.width)] for row in range(self.height)], dtype=bool)


def light_of(layout: Layout):
    """The tile light's two halves for a map (sky, sources) and the grid of cells open to the sky."""
    solid = layout.grid(lambda x, row: layout.ground(x, row) is not None)
    shades = solid | layout.grid(lambda x, row: layout.wall(x, row) or layout.liquid(x, row))
    keep = np.where(solid[..., None], LIGHT["solid_keeps"], LIGHT["air_keeps"]).astype(np.float32).repeat(3, -1)
    emits = np.zeros((layout.height, layout.width, 3), dtype=np.float32)
    glow = {m["key"]: m["emits"] for m in MATERIALS if "emits" in m}
    liquids = {entry["key"]: entry for entry in tv2_liquid.LIQUIDS}
    for row in range(layout.height):
        for x in range(layout.width):
            block = layout.block(x, row)
            liquid = layout.liquid(x, row)
            if block in glow:
                emits[row, x] = glow[block]
            if liquid:
                keep[row, x] = liquids[liquid]["keeps"]
                emits[row, x] = liquids[liquid]["emits"]
    for x, row in layout.marks(TORCH_CHAR):
        emits[row, x] = np.array(LIGHT["torch_colour"]) * LIGHT["torch_intensity"]
    sky, sources = tv2_light.tile_light(solid, shades, keep, emits)
    open_sky = tv2_light.sky_seeds(shades)[..., 0] > 0
    return sky, sources, open_sky


def tile_from(sheet: Image.Image, cx: int, cy: int) -> Image.Image:
    return sheet.crop((cx * TILE, cy * TILE, (cx + 1) * TILE, (cy + 1) * TILE))


def lay_tiles(canvas: Image.Image, layout: Layout, images: dict, at, group: dict):
    """Draws a layer of autotiled materials: `at(x, row)` names each cell's tile."""
    rows_per_variant = (len(FRAMES) + COLUMNS - 1) // COLUMNS

    def joins(a, b):
        return a is not None and b is not None and group[a.split(":")[0]] == group[b.split(":")[0]]

    for row in range(layout.height):
        for x in range(layout.width):
            name = at(x, row)
            if name is None:
                continue
            key, _, slope = name.partition(":")
            v = variant_of(x, layout.height - 1 - row, VARIANTS)
            if slope:
                cx, cy = SLOPES.index(slope) * VARIANTS + v, rows_per_variant * VARIANTS
            else:
                mask = 0
                for bit, dx, dy in BITS:
                    if joins(name, at(x + dx, row + dy)):
                        mask |= bit
                cx, cy = frame_cell(FRAMES.index(canonical(mask)))
                cy += v * rows_per_variant
            canvas.alpha_composite(tile_from(images[key], cx, cy), (x * TILE, row * TILE))


def lay_blends(canvas: Image.Image, layout: Layout, images: dict):
    rows = (len(BLEND_MASKS) + COLUMNS - 1) // COLUMNS
    done = set()
    for blend in BLENDS:
        sheet = images["blend_" + blend["key"]]
        for row in range(layout.height):
            for x in range(layout.width):
                if layout.block(x, row) != blend["onto"] or (x, row) in done:
                    continue
                sides = 0
                for bit, side in BLEND_SIDES:
                    _, dx, dy = next(b for b in BITS if b[0] == side)
                    other = layout.ground(x + dx, row + dy)
                    if other is not None and other.split(":")[0] in blend["from"]:
                        sides |= bit
                if sides:
                    v = variant_of(x, layout.height - 1 - row, VARIANTS)
                    cx, cy = frame_cell(BLEND_MASKS.index(sides))
                    canvas.alpha_composite(tile_from(sheet, cx, cy + v * rows), (x * TILE, row * TILE))
                    done.add((x, row))


def shade_cells(layout: Layout) -> list[tuple[int, int, int | None]]:
    """The shade a map's walls show: (column, row, mask), mask None for the full tile."""
    out = []
    for row in range(layout.height):
        for x in range(layout.width):
            if layout.wall(x, row) is None:
                continue
            around = 0
            for bit, dx, dy in BITS:
                if layout.block(x + dx, row + dy):
                    around |= bit
            if layout.block(x, row):
                if around != 255:
                    out.append((x, row, None))
            elif tv2_decor.shade_canonical(around):
                out.append((x, row, tv2_decor.shade_canonical(around)))
    return out


def tinted(image: Image.Image, tint, alpha: float = 1.0) -> Image.Image:
    a = np.asarray(image, dtype=np.float32).copy()
    a[..., :3] *= np.array(tint, dtype=np.float32)
    a[..., 3] *= alpha
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def lit_by(image: Image.Image, light: np.ndarray) -> Image.Image:
    a = np.asarray(image, dtype=np.float32).copy()
    a[..., :3] *= light
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def tile_across(canvas: Image.Image, layer: Image.Image, top: int, rows: range | None = None):
    for x0 in range(0, canvas.width, layer.width):
        if rows is None:
            canvas.alpha_composite(layer, (x0, top))
        else:
            for y0 in rows:
                canvas.alpha_composite(layer, (x0, y0))


def render(art, hour: float | None = None, lit: bool = True) -> tuple[Image.Image, Layout]:
    """The whole sample map at one art pixel a pixel."""
    images, sprites = art
    layout = Layout(*read_map(SAMPLE_MAP))
    width, height = layout.width * TILE, layout.height * TILE
    day = day_at(DAY_START if hour is None else hour)
    group = {m["key"]: m["group"] for m in MATERIALS}
    line = layout.surface * TILE

    # The far world, which the map's light does not touch.
    back = tv2_backdrop.sky_image(width, max(line, TILE), day["top"], day["low"])
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    canvas.alpha_composite(back)
    if day["stars"] > 0:
        tile_across(canvas, tinted(images["backdrop_stars"], (1, 1, 1), day["stars"]), 0)
    body, along = sky_arc(day["hour"])
    canvas.alpha_composite(
        images["backdrop_" + body],
        (int(width * (0.08 + 0.84 * along)) - 8, int(line * 0.62 - np.sin(np.pi * along) * line * 0.5)),
    )
    tile_across(canvas, tinted(images["backdrop_clouds"], day["tint"]), 8)
    for key in ("backdrop_far", "backdrop_mid", "backdrop_near"):
        tile_across(canvas, tinted(images[key], day["tint"]), line - 2 * TILE - tv2_backdrop.BACKDROP_H)

    # Everything the tile light falls on, back to front.
    world = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    cave = images["backdrop_cave"]
    tile_across(world, cave, 0, range(line, height, cave.height))
    tile_across(world, images["backdrop_cave_top"], line - images["backdrop_cave_top"].height)
    walls = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lay_tiles(walls, layout, images, layout.wall, group)
    world.alpha_composite(tinted(walls, LIGHT["wall_tint"]))
    for x, row, mask in shade_cells(layout):
        world.alpha_composite(sprites[tv2_decor.shade_sprite(mask)], (x * TILE, row * TILE))
    sky, sources, open_sky = light_of(layout)
    decor = tv2_decor.scatter(
        layout.block, layout.free, lambda x, row: layout.inside(x, row) and open_sky[row, x], layout.width, layout.height
    )
    for x, row, name in decor:
        world.alpha_composite(sprites[name], (x * TILE, row * TILE - tv2_decor.BLEED))
    lay_tiles(world, layout, images, layout.ground, group)
    lay_blends(world, layout, images)

    water = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    glowing = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lit_liquid = {entry["key"]: entry["lit"] for entry in tv2_liquid.LIQUIDS}
    for row in range(layout.height):
        for x in range(layout.width):
            liquid = layout.liquid(x, row)
            if liquid:
                part = "body" if layout.liquid(x, row - 1) == liquid else "surface"
                target = water if lit_liquid[liquid] else glowing
                target.alpha_composite(sprites[f"tv2_{liquid}_{part}_0"], (x * TILE, row * TILE))
    for x, row in layout.marks(TORCH_CHAR):
        glowing.alpha_composite(sprites["tv2_torch_0"], (x * TILE, row * TILE))

    if lit:
        light = tv2_light.smooth(tv2_light.combine(sky, sources, day["light"]), (width, height))
        light = np.clip(light + np.array(LIGHT["ambient_colour"]) * LIGHT["ambient"], 0, 4)
        world, water = lit_by(world, light), lit_by(water, light)
    canvas.alpha_composite(world)
    canvas.alpha_composite(water)
    canvas.alpha_composite(glowing)
    return canvas, layout


def preview(art, path: str, hour: float | None = None, view: str | None = None, scale: int = 3, lit: bool = True):
    """Writes the sample map (or the 320 x 180 view of one of its named places) to a PNG."""
    canvas, layout = render(art, hour, lit)
    if view:
        if view in layout.views:
            cx, cy = layout.views[view]
        else:
            cx, cy = (float(v) for v in view.split(","))
        left = int(round(cx * TILE - 160))
        top = int(round(cy * TILE - 90))
        left = max(0, min(canvas.width - 320, left))
        top = max(0, min(canvas.height - 180, top))
        canvas = canvas.crop((left, top, left + 320, top + 180))
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    canvas.resize((canvas.width * scale, canvas.height * scale), Image.NEAREST).save(path)
