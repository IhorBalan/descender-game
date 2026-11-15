#!/usr/bin/env python3
"""Lays a map out the way Unity shows it, for a quick look without the editor:
gen_tiles_v2.py --preview. Not a script.

It follows the same rules as the Unity side, with the same numbers: the frame a
tile shows, the blend overlays, the shade on the walls, the scattered decor, the
liquids' top rows, the tile light with each region's own air, the rock behind
each region's caves and the hour's sky. Nothing moves (frame 0 of every
animation) and the backdrops stand where a camera over the whole map would put
them. A big map is drawn a window at a time: the light is worked out for the
whole map, the tiles only for what shows.
"""
from __future__ import annotations

import os

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_backdrop
import tv2_decor
import tv2_earth
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
        self.regions = properties.get("region", [])
        self.marked = properties.get("mark", [])

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

    def ledge(self, x: int, row: int):
        return LEDGE_CHARS.get(self.terrain[row][x]) if self.inside(x, row) else None

    def prop(self, x: int, row: int):
        return PROP_CHARS.get(self.terrain[row][x]) if self.inside(x, row) else None

    def free(self, x: int, row: int) -> bool:
        return self.inside(x, row) and self.terrain[row][x] == "."

    def marks(self, char: str) -> list[tuple[int, int]]:
        return [(x, row) for row in range(self.height) for x in range(self.width) if self.terrain[row][x] == char]

    def grid(self, chars) -> np.ndarray:
        """Where the terrain grid holds one of `chars`."""
        return np.array([[c in chars for c in row] for row in self.terrain], dtype=bool)


def light_of(layout: Layout):
    """The tile light's two halves for a map (sky, sources) and the grid of cells open to the sky."""
    solid = layout.grid(TERRAIN_CHARS)
    wet = layout.grid(LIQUID_CHARS)
    walled = np.array([[c in SHUT_WALL_CHARS for c in row] for row in layout.walls], dtype=bool)
    shades = solid | wet | walled
    keep = np.where(solid[..., None], LIGHT["solid_keeps"], LIGHT["air_keeps"]).astype(np.float32).repeat(3, -1)
    # The air of a region has a faint light of its own.
    emits = np.array(ambient_rows(layout.regions, layout.height), dtype=np.float32)[:, None, :].repeat(layout.width, 1)
    emits[solid | wet] = 0
    # A stretch the map marks as dark has no light in its air.
    for kind, x, row, rest in layout.marked:
        if kind == "dark" and len(rest) >= 2:
            emits[max(row, 0) : row + int(rest[1]), max(x, 0) : x + int(rest[0])] = 0
    glow = {m["key"]: m["emits"] for m in MATERIALS if "emits" in m}
    liquids = {entry["key"]: entry for entry in tv2_liquid.LIQUIDS}
    props = {p["key"]: np.array(p["colour"]) * p["intensity"] for p in PROPS}
    glowing = {c for c, name in TERRAIN_CHARS.items() if name in glow} | set(LIQUID_CHARS) | set(PROP_CHARS)
    for row, line in enumerate(layout.terrain):
        for x, c in enumerate(line):
            if c not in glowing:
                continue
            if c in LIQUID_CHARS:
                keep[row, x] = liquids[LIQUID_CHARS[c]]["keeps"]
                emits[row, x] = liquids[LIQUID_CHARS[c]]["emits"]
            elif c in PROP_CHARS:
                emits[row, x] = props[PROP_CHARS[c]]
            else:
                emits[row, x] = glow[TERRAIN_CHARS[c]]
    # A world (a map of regions) has open sky everywhere over its surface line.
    surface = layout.surface if layout.regions else 0
    sky, sources = tv2_light.tile_light(solid, shades, keep, emits, surface)
    open_sky = tv2_light.sky_seeds(shades)[..., 0] > 0
    return sky, sources, open_sky


def tile_from(sheet: Image.Image, cx: int, cy: int) -> Image.Image:
    return sheet.crop((cx * TILE, cy * TILE, (cx + 1) * TILE, (cy + 1) * TILE))


class Window:
    """The part of a map being drawn: columns x0 .. x1 - 1, rows y0 .. y1 - 1."""

    def __init__(self, layout: Layout, x0: int = 0, y0: int = 0, x1: int | None = None, y1: int | None = None):
        self.x0, self.y0 = max(x0, 0), max(y0, 0)
        self.x1 = layout.width if x1 is None else min(x1, layout.width)
        self.y1 = layout.height if y1 is None else min(y1, layout.height)
        self.size = ((self.x1 - self.x0) * TILE, (self.y1 - self.y0) * TILE)

    def cells(self):
        for row in range(self.y0, self.y1):
            for x in range(self.x0, self.x1):
                yield x, row

    def at(self, x: int, row: int) -> tuple[int, int]:
        """Where a cell's top left corner is on the window's canvas."""
        return (x - self.x0) * TILE, (row - self.y0) * TILE

    def has(self, x: int, row: int) -> bool:
        return self.x0 <= x < self.x1 and self.y0 <= row < self.y1


def lay_tiles(canvas: Image.Image, layout: Layout, window: Window, images: dict, at, group: dict):
    """Draws a layer of autotiled materials: `at(x, row)` names each cell's tile."""
    rows_per_variant = (len(FRAMES) + COLUMNS - 1) // COLUMNS

    def joins(a, b):
        return a is not None and b is not None and group[a.split(":")[0]] == group[b.split(":")[0]]

    for x, row in window.cells():
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
        canvas.alpha_composite(tile_from(images[key], cx, cy), window.at(x, row))


def lay_blends(canvas: Image.Image, layout: Layout, window: Window, images: dict):
    rows = (len(BLEND_MASKS) + COLUMNS - 1) // COLUMNS
    onto: dict[str, list] = {}
    for blend in BLENDS:
        onto.setdefault(blend["onto"], []).append(blend)
    steps = [(bit, next(b for b in BITS if b[0] == side)) for bit, side in BLEND_SIDES]
    for x, row in window.cells():
        for blend in onto.get(layout.block(x, row), ()):
            sides = 0
            for bit, (_, dx, dy) in steps:
                other = layout.ground(x + dx, row + dy)
                if other is not None and other.split(":")[0] in blend["from"]:
                    sides |= bit
            if sides:
                v = variant_of(x, layout.height - 1 - row, VARIANTS)
                cx, cy = frame_cell(BLEND_MASKS.index(sides))
                canvas.alpha_composite(tile_from(images["blend_" + blend["key"]], cx, cy + v * rows), window.at(x, row))
                break


def shade_cells(layout: Layout, window: Window) -> list[tuple[int, int, int | None]]:
    """The shade a map's walls show: (column, row, mask), mask None for the full tile."""
    out = []
    for x, row in window.cells():
        if layout.wall(x, row) is None or layout.wall(x, row) in OPEN_WALLS:
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


def tile_across(canvas: Image.Image, layer: Image.Image, top: int, bottom: int | None = None, left: int = 0):
    """Repeats a picture across the canvas from the row `top`, and down to `bottom` when given;
    `left` is where the picture's own left edge is (it repeats from the world's edge)."""
    if bottom is None:
        bottom = top + layer.height
    y = top
    while y < bottom:
        part = layer if y + layer.height <= bottom else layer.crop((0, 0, layer.width, bottom - y))
        if y + part.height > 0 and y < canvas.height:
            for x0 in range(left, canvas.width, layer.width):
                if x0 + layer.width > 0:
                    canvas.alpha_composite(part, (x0, y)) if y >= 0 and x0 >= 0 else canvas.alpha_composite(
                        part.crop((max(-x0, 0), max(-y, 0), part.width, part.height)), (max(x0, 0), max(y, 0))
                    )
        y += layer.height


class Lighting:
    """A map's light, worked out once for all its windows."""

    def __init__(self, layout: Layout):
        self.sky, self.sources, self.open_sky = light_of(layout)

    def over(self, window: Window, daylight) -> np.ndarray:
        """The light per pixel of a window."""
        # A tile either side, so the filter between tile centres has what the whole map would give it.
        h, w = self.sky.shape[:2]
        x0, y0, x1, y1 = max(window.x0 - 1, 0), max(window.y0 - 1, 0), min(window.x1 + 1, w), min(window.y1 + 1, h)
        both = tv2_light.combine(self.sky[y0:y1, x0:x1], self.sources[y0:y1, x0:x1], daylight)
        full = tv2_light.smooth(both, ((x1 - x0) * TILE, (y1 - y0) * TILE))
        left, top = (window.x0 - x0) * TILE, (window.y0 - y0) * TILE
        full = full[top : top + window.size[1], left : left + window.size[0]]
        return np.clip(full + np.array(LIGHT["ambient_colour"]) * LIGHT["ambient"], 0, 4)


def render(art, layout: Layout, window: Window, lighting: Lighting | None, hour: float | None = None) -> Image.Image:
    """A window of a map at one art pixel a pixel; `lighting` None for the map without its light."""
    images, sprites = art
    width, height = window.size
    left, top = window.x0 * TILE, window.y0 * TILE
    day = day_at(DAY_START if hour is None else hour)
    group = {m["key"]: m["group"] for m in MATERIALS}
    line = layout.surface * TILE

    # The far world, which the map's light does not touch.
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    if top < line:
        sky = tv2_backdrop.sky_image(width, max(line, TILE), day["top"], day["low"])
        canvas.alpha_composite(sky.crop((0, top, width, min(line, top + height))))
        if day["stars"] > 0:
            tile_across(canvas, tinted(images["backdrop_stars"], (1, 1, 1), day["stars"]), -top, left=-left)
        body, along = sky_arc(day["hour"])
        world_width = layout.width * TILE
        canvas.alpha_composite(
            images["backdrop_" + body],
            (
                max(int(world_width * (0.08 + 0.84 * along)) - 8 - left, 0),
                max(int(line * 0.62 - np.sin(np.pi * along) * line * 0.5) - top, 0),
            ),
        )
        tile_across(canvas, tinted(images["backdrop_clouds"], day["tint"]), 8 - top, left=-left)
        ridges = sorted((b for b in tv2_backdrop.BACKDROPS if b["kind"] == "ridge"), key=lambda b: b["order"])
        for key in [b["key"] for b in ridges]:
            tile_across(canvas, tinted(images[key], day["tint"]), line - 2 * TILE - tv2_backdrop.BACKDROP_H - top, left=-left)
        # Nothing of the far world shows under the surface line.
        if line - top < height:
            canvas.paste((0, 0, 0, 255), (0, max(line - top, 0), width, height))

    # Everything the tile light falls on, back to front: the rock behind each region's caves first.
    world = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for region, first, end in region_bands(layout.regions, layout.height):
        if not region["cave"]:
            continue
        first = max(first, layout.surface)
        if first >= end:
            continue
        tile_across(world, images["backdrop_cave_" + region["key"]], first * TILE - top, end * TILE - top, left=-left)
        ragged = images["backdrop_cave_top_" + region["key"]]
        tile_across(world, ragged, first * TILE - ragged.height - top, left=-left)
    # The walls, tinted back; and over them the open ones (a tree), which are not.
    walls = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lay_tiles(walls, layout, window, images, lambda x, row: None if layout.wall(x, row) in OPEN_WALLS else layout.wall(x, row), group)
    world.alpha_composite(tinted(walls, LIGHT["wall_tint"]))
    foliage = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lay_tiles(foliage, layout, window, images, lambda x, row: layout.wall(x, row) if layout.wall(x, row) in OPEN_WALLS else None, group)
    for x, row, mask in shade_cells(layout, window):
        world.alpha_composite(sprites[tv2_decor.shade_sprite(mask)], window.at(x, row))
    world.alpha_composite(foliage)
    # The pictures a map stands (the Earth's trees, houses, timber): behind the decor, on the
    # ground (track), on the water (lilies), or unlit (a lamp in a window).
    stood = {b["key"]: b for b in tv2_backdrop.BACKDROPS if b["kind"] == "picture"}
    later: dict[str, list] = {"track": [], "water": [], "glow": []}
    for kind, x, row, rest in layout.marked:
        if kind != "picture" or not rest or rest[0] not in stood or rest[0] not in images:
            continue
        entry, image = stood[rest[0]], images[rest[0]]
        px, py = window.at(x, row)
        at = (px + TILE // 2 - image.width // 2, py + TILE - image.height + tv2_earth.SINK_OF.get(rest[0], 0))
        if at[0] > width or at[1] > height or at[0] + image.width < 0 or at[1] + image.height < 0:
            continue
        if not entry["lit"]:
            later["glow"].append((image, at))
        elif entry["order"] > 20:
            later["water"].append((image, at))
        elif entry["order"] > -100:
            later["track"].append((image, at))
        elif entry["order"] < -300:
            continue
        else:
            paste_clipped(world, image, at)
    open_sky = lighting.open_sky if lighting else np.zeros((layout.height, layout.width), dtype=bool)
    for x, row, name in decor_of(layout, open_sky):
        if window.has(x, row):
            px, py = window.at(x, row)
            if py >= tv2_decor.BLEED:
                world.alpha_composite(sprites[name], (px, py - tv2_decor.BLEED))
    lay_tiles(world, layout, window, images, layout.ground, group)
    lay_blends(world, layout, window, images)
    for image, at in later["track"]:
        paste_clipped(world, image, at)
    for x, row in window.cells():
        ledge = layout.ledge(x, row)
        if ledge:
            left, right = layout.ledge(x - 1, row) == ledge, layout.ledge(x + 1, row) == ledge
            piece = "middle" if left and right else "right" if left else "left" if right else "single"
            world.alpha_composite(sprites[f"tv2_ledge_{ledge}_{piece}"], window.at(x, row))

    water = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    glowing = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lit_liquid = {entry["key"]: entry["lit"] for entry in tv2_liquid.LIQUIDS}
    for x, row in window.cells():
        liquid = layout.liquid(x, row)
        if liquid:
            part = "body" if layout.liquid(x, row - 1) == liquid else "surface"
            target = water if lit_liquid[liquid] else glowing
            target.alpha_composite(sprites[f"tv2_{liquid}_{part}_0"], window.at(x, row))
        prop = layout.prop(x, row)
        if prop:
            glowing.alpha_composite(sprites[f"tv2_{prop}_0"], window.at(x, row))
    for kind, x, row, _ in layout.marked:
        if window.has(x, row) and kind in MARK_COLOURS:
            px, py = window.at(x, row)
            glowing.paste(MARK_COLOURS[kind], (px + 5, py + 4, px + 11, py + 16))

    for image, at in later["water"]:
        paste_clipped(water, image, at)
    for image, at in later["glow"]:
        paste_clipped(glowing, image, at)
    if lighting:
        light = lighting.over(window, day["light"])
        world, water = lit_by(world, light), lit_by(water, light)
    canvas.alpha_composite(world)
    canvas.alpha_composite(water)
    canvas.alpha_composite(glowing)
    return canvas


def paste_clipped(canvas: Image.Image, image: Image.Image, at):
    """alpha_composite that lets the picture hang over the canvas's edges."""
    x, y = at
    left, top = max(0, -x), max(0, -y)
    right, bottom = min(image.width, canvas.width - x), min(image.height, canvas.height - y)
    if right <= left or bottom <= top:
        return
    canvas.alpha_composite(image.crop((left, top, right, bottom)), (x + left, y + top))


# What stands for the things a map marks (the game's own objects), in a preview.
MARK_COLOURS = {
    "checkpoint": (255, 240, 160, 255),
    "ability": (120, 255, 240, 255),
    "relic": (255, 200, 60, 255),
    "barrier": (255, 255, 200, 255),
    "seal": (200, 120, 255, 255),
    "secret": (150, 150, 150, 255),
    "upgrade": (255, 120, 140, 255),
    "sign": (200, 170, 120, 255),
    "platform": (190, 170, 140, 255),
    "end": (255, 255, 255, 255),
}

_DECOR: dict = {}


def decor_of(layout: Layout, open_sky: np.ndarray) -> list[tuple[int, int, str]]:
    """A map's scattered decor, worked out once."""
    if id(layout) not in _DECOR:
        _DECOR[id(layout)] = tv2_decor.scatter(
            layout.block, layout.free, lambda x, row: layout.inside(x, row) and open_sky[row, x], layout.width, layout.height
        )
    return _DECOR[id(layout)]


def preview(art, path: str, hour: float | None = None, view: str | None = None, scale: int = 3, lit: bool = True, source: str = SAMPLE_MAP):
    """Writes a map (or the camera's view of one of its named places) to a PNG. A map too
    big to draw whole at this scale is drawn in bands and made smaller: `scale` below 1."""
    layout = Layout(*read_map(source))
    lighting = Lighting(layout) if lit else None
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    if view:
        if view in layout.views:
            cx, cy = layout.views[view]
        else:
            cx, cy = (float(v) for v in view.split(","))
        wide, tall = VIEW[0] // TILE, -(-VIEW[1] // TILE)
        left = max(0, min(layout.width - wide, int(round(cx - wide / 2))))
        top = max(0, min(layout.height - tall, int(round(cy - tall / 2))))
        canvas = render(art, layout, Window(layout, left, top, left + wide, top + tall), lighting, hour)
        canvas.resize((canvas.width * scale, canvas.height * scale), Image.NEAREST).save(path)
        return
    # The whole map, a band of rows at a time.
    px = max(int(round(TILE * scale)), 1)  # pixels a tile, in the picture
    out = Image.new("RGB", (layout.width * px, layout.height * px))
    band = 48
    for y0 in range(0, layout.height, band):
        window = Window(layout, 0, y0, layout.width, y0 + band)
        canvas = render(art, layout, window, lighting, hour).convert("RGB")
        size = (layout.width * px, (window.y1 - window.y0) * px)
        out.paste(canvas.resize(size, Image.NEAREST if px >= TILE else Image.BOX), (0, y0 * px))
    out.save(path)
