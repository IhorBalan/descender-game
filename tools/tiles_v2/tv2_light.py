#!/usr/bin/env python3
"""The Tiles V2 tile light, as TileLight.cs works it out, for the preview. Not a
script.

Light is a value per tile and per colour channel. It starts from seeds (the open
sky, torches, lava, glowing ore) and spreads to the eight neighbours: a tile
passes on the share of its light its medium keeps (air most, rock little, water
more blue than red), that share to the power of the square root of two for a
diagonal step, so a lone light is round and not a diamond. A diagonal step
between two solid tiles is shut. The sky and the other sources are spread apart,
so the colour of the daylight can change without spreading again:
light = max(sky x daylight, sources).
"""
from __future__ import annotations

import numpy as np
from PIL import Image

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

DIAGONAL = 2**0.5
STEPS = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)]


def moved(a: np.ndarray, dx: int, dy: int, fill=0) -> np.ndarray:
    """a moved by (dx, dy): out[y, x] = a[y - dy, x - dx], `fill` coming in."""
    out = np.full_like(a, fill)
    h, w = a.shape[:2]
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    out[yd, xd] = a[ys, xs]
    return out


def spread(seeds: np.ndarray, keep: np.ndarray, solid: np.ndarray) -> np.ndarray:
    """Spreads the seeds (h, w, 3) until nothing changes: every tile ends with the
    brightest of its own seed and of each neighbour's light times the share that
    neighbour keeps (`keep`, per channel)."""
    light = seeds.astype(np.float32).copy()
    straight = keep.astype(np.float32)
    diagonal = straight**DIAGONAL
    for _ in range(4 * (seeds.shape[0] + seeds.shape[1])):
        best = light
        for dx, dy in STEPS:
            passed = moved(light * (diagonal if dx and dy else straight), dx, dy)
            if dx and dy:
                # The two tiles the step squeezes between: both solid shuts it.
                shut = moved(solid, dx, 0, False) & moved(solid, 0, dy, False)
                passed = np.where(shut[..., None], 0, passed)
            best = np.maximum(best, passed)
        if np.abs(best - light).max() < 1e-5:
            break
        light = best
    return light


def sky_seeds(blocks: np.ndarray) -> np.ndarray:
    """Daylight down every column to its first tile that shades the sky (a solid
    tile, a wall, a liquid); rows run top first."""
    h, w = blocks.shape
    seeds = np.zeros((h, w, 3), dtype=np.float32)
    for x in range(w):
        for y in range(h):
            if blocks[y, x]:
                break
            seeds[y, x] = 1.0
    return seeds


def tile_light(solid: np.ndarray, shades: np.ndarray, keep: np.ndarray, emits: np.ndarray):
    """The two halves of the light per tile, rows top first: the sky's (for a white
    daylight) and the sources'. `shades` marks what stops the sky, `keep` is each
    tile's share per channel, `emits` each tile's own light."""
    sky = spread(sky_seeds(shades), keep, solid)
    sources = spread(emits, keep, solid)
    return sky, sources


def combine(sky: np.ndarray, sources: np.ndarray, daylight=(1.0, 1.0, 1.0)) -> np.ndarray:
    return np.maximum(sky * np.array(daylight, dtype=np.float32), sources)


def smooth(light: np.ndarray, size: tuple[int, int]) -> np.ndarray:
    """The light per pixel: filtered between tile centres, as the light texture is."""
    return np.stack([np.asarray(Image.fromarray(light[..., c], "F").resize(size, Image.BILINEAR)) for c in range(3)], -1)


def light_preview(canvas: Image.Image, terrain: list[str], walls: list[str], torches) -> Image.Image:
    """The Unity lighting for the preview: the tile light, filtered smooth between
    tile centres, on a faint ambient."""
    h, w = len(terrain), canvas.width // TILE

    def grid(rows, chars):
        return np.array([[x < len(r) and r[x] in chars for x in range(w)] for r in rows[:h]], dtype=bool)

    solid = grid(terrain, TERRAIN_CHARS)
    keep = np.where(solid, LIGHT["solid_keeps"], LIGHT["air_keeps"]).astype(np.float32)[..., None].repeat(3, -1)
    emits = np.zeros((h, w, 3), dtype=np.float32)
    for tx, ty in torches:
        emits[ty, tx] = np.array(LIGHT["torch_colour"]) * LIGHT["torch_intensity"]
    sky, sources = tile_light(solid, solid | grid(walls, WALL_CHARS), keep, emits)
    lit = smooth(combine(sky, sources), canvas.size) + LIGHT["ambient"]
    rgb = np.asarray(canvas.convert("RGB"), dtype=np.float32) * np.clip(lit, 0, 1.6)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
