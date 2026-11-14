#!/usr/bin/env python3
"""Writes the Tiles V2 sample map, Assets/Art/TilesV2/sample_map.txt: a small world
that shows every part of the standard and can be walked in the playground.

    python3 -I tools/tiles_v2/make_sample_map.py

From west to east on the surface: a meadow with a hill and a pond, the mouth of
a sloping tunnel, a shaft open to the sky, then a desert of dunes. Under it: a
dirt cave, a long torch-lit hall in the stone with a gallery of copper to its
west, a wide cavern with no wall behind it and a lake on its floor to its east,
a cave under the dunes where sand trickles from the roof, and at the bottom a
cavern of lava with islands to cross on. Everything is placed by the numbers
below and a fixed seed, so a run writes the same map.
"""
from __future__ import annotations

import os

import numpy as np

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

WIDTH, HEIGHT = 128, 64
AIR = "."
DESERT_FROM = 92  # the first column of sand
SURFACE_LINE = 20  # the row the cave backdrop starts at: under every surface dip

# The surface: (column, the row of its top tile), straight between the points.
SURFACE = [
    (0, 17), (8, 17), (11, 15), (19, 15), (22, 17), (41, 17), (43, 16), (57, 16), (58, 17), (68, 17),
    (71, 15), (80, 15), (82, 17), (95, 17), (98, 14), (104, 14), (106, 16), (111, 16), (114, 13),
    (121, 13), (123, 15), (127, 15),
]  # fmt: skip

# Caves: ellipses (centre column, centre row, half width, half height) cut out of the ground.
DIRT_CAVE = [(60, 24, 9, 2.6), (70, 24.5, 6, 2.2), (54, 24.5, 3, 2.0)]
TORCH_HALL = [(38, 34.5, 9, 3.2), (52, 34, 9, 3.6), (66, 34.5, 10, 3.2), (76, 35, 5, 2.4)]
ORE_GALLERY = [(14, 37, 7, 2.8), (24, 36, 6, 2.2)]
OPEN_CAVERN = [(96, 36, 13, 6.5), (108, 38, 10, 6.0), (90, 40, 7, 4.5)]
DUNE_CAVE = [(107, 22.5, 9, 2.4), (100, 23, 3, 2.0)]
LAVA_CAVERN = [(40, 51.5, 16, 4.2), (62, 51, 16, 4.6), (84, 51.5, 16, 4.2)]
LAVA_BASIN = (27, 97, 53, 58)  # the cavern's floor is cut square down to here (first/last column, first/last row)
# Shafts and passages: (first column, last column, first row, last row), cut square.
PASSAGES = [
    (62, 64, 17, 22),  # the open shaft from the meadow into the dirt cave
    (72, 74, 26, 31),  # from the dirt cave down into the hall
    (26, 31, 34, 36),  # the hall's west door to the gallery
    (79, 86, 34, 36),  # the hall's east door to the cavern
    (100, 102, 24, 31),  # from the dune cave down into the cavern
    (40, 42, 37, 49),  # from the hall down to the lava
    (86, 88, 43, 49),  # from the cavern down to the lava
]
NO_WALL = OPEN_CAVERN + LAVA_CAVERN  # the cave backdrop shows behind these
LAKE = (92, 119, 41, 44)  # water fills the cavern's floor from this row down (first/last column, first/last row)
DAM = (89, 91, 41, 46)  # the stone that holds the lake back from the shaft beside it
LAVA_FROM = 56  # lava fills the bottom cavern from this row down
ISLANDS = [(27, 31), (38, 46), (56, 64), (72, 76), (84, 97)]  # stone standing in the lava, to its brim: columns
TORCHES = [
    (50, 19), (58, 23), (68, 23), (36, 34), (48, 33), (58, 33), (68, 34), (78, 35), (14, 36), (24, 36),
    (105, 22), (113, 22), (95, 33), (112, 36), (85, 39), (103, 38), (115, 37), (41, 44), (30, 52), (62, 49),
    (94, 52),
]  # fmt: skip
SPAWN = (5, 16)
# Named places, as (column, row) of the middle of the camera's view of them (40 x 22.5 tiles).
VIEWS = {
    "surface": (20, 11.5),
    "pond": (33, 15),
    "tunnel": (54, 19),
    "hall": (54, 33),
    "ore": (20, 37),
    "cavern": (103, 37),
    "dunes": (108, 13),
    "trickle": (107, 24),
    "lava": (62, 51),
}


def surface_rows() -> list[int]:
    xs = [p[0] for p in SURFACE]
    rows = [p[1] for p in SURFACE]
    top = [int(round(v)) for v in np.interp(np.arange(WIDTH), xs, rows)]
    for x in range(1, WIDTH):
        top[x] = max(top[x - 1] - 1, min(top[x - 1] + 1, top[x]))
    return top


def inside(shapes, x: int, row: int, grow: float = 0.0) -> bool:
    return any(((x - cx) / (rx + grow)) ** 2 + ((row - cy) / (ry + grow)) ** 2 <= 1.0 for cx, cy, rx, ry in shapes)


def build() -> tuple[list[str], list[str]]:
    r = rng("sample_map", 3)
    top = surface_rows()
    ground = np.full((HEIGHT, WIDTH), AIR, dtype="<U1")
    wall = np.full((HEIGHT, WIDTH), AIR, dtype="<U1")

    # The ground: dirt over stone in the meadow, sand over stone in the desert,
    # the line between them wandering by a tile or two.
    wander = np.round(periodic_noise(WIDTH, r)).astype(int)
    for x in range(WIDTH):
        desert = x >= DESERT_FROM
        stone_from = (21 if desert else 28) + wander[x]
        for row in range(top[x], HEIGHT):
            ground[row, x] = "S" if row >= stone_from else ("A" if desert else "D")
            wall[row, x] = "s" if row >= stone_from + 1 else "d"
        wall[top[x], x] = AIR  # nothing shows behind the top tile's ragged edge but the sky

    # Pockets: clay and stone in the dirt, dirt and ore in the stone.
    for _ in range(9):
        blot(ground, r, "C", "D", r.randint(2, DESERT_FROM - 6), r.randint(20, 26), r.randint(5, 9), top)
    for _ in range(8):
        blot(ground, r, "S", "D", r.randint(2, DESERT_FROM - 6), r.randint(22, 27), r.randint(5, 10), top)
    for _ in range(16):
        blot(ground, r, "D", "S", r.randint(2, WIDTH - 3), r.randint(30, 60), r.randint(6, 14))
    for _ in range(26):
        blot(ground, r, "O", "S", r.randint(2, WIDTH - 3), r.randint(29, 61), r.randint(3, 7))
    for _ in range(9):
        blot(ground, r, "O", "S", r.randint(6, 30), r.randint(32, 42), r.randint(4, 8))

    # The caves, their edges roughened (never the floors: those stay walkable).
    caves = DIRT_CAVE + TORCH_HALL + ORE_GALLERY + OPEN_CAVERN + DUNE_CAVE + LAVA_CAVERN
    for row in range(HEIGHT):
        for x in range(WIDTH):
            if inside(caves, x, row):
                ground[row, x] = AIR
            elif inside(caves, x, row, 0.9) and r.random() < 0.35 and ground[min(row + 1, HEIGHT - 1), x] != AIR:
                ground[row, x] = AIR
    x0, x1, row0, row1 = LAVA_BASIN
    ground[row0 : row1 + 1, x0 : x1 + 1] = AIR
    for x0, x1, row0, row1 in PASSAGES:
        ground[row0 : row1 + 1, x0 : x1 + 1] = AIR
    for row in range(HEIGHT):
        for x in range(WIDTH):
            if inside(NO_WALL, x, row, 1.5):
                wall[row, x] = AIR
    for x0, x1, row0, row1 in PASSAGES[5:]:
        wall[row0 : row1 + 1, x0 - 1 : x1 + 2] = np.where(np.arange(row0, row1 + 1)[:, None] > 40, AIR, wall[row0 : row1 + 1, x0 - 1 : x1 + 2])

    # The sloping tunnel from the meadow down into the dirt cave: each column a
    # step lower, a slope tile on every step, three tiles of air over it.
    for k in range(0, 10):
        x, slope_row = 44 + k, 15 + k
        ground[slope_row - 3 : slope_row, x] = AIR
        if ground[slope_row + 1, x] == AIR:
            ground[slope_row + 1, x] = "D"
        ground[slope_row, x] = "}" if k else ground[slope_row, x]
    ground[22:26, 53:56] = AIR

    # The pond in the meadow and the lake on the cavern's floor.
    for x in range(27, 36):
        depth = 3 if 28 < x < 34 else 2
        ground[17 : 17 + depth, x] = "W"
        wall[17 : 17 + depth, x] = AIR  # the sky shows through the pond
    x0, x1, row0, row1 = DAM
    ground[row0 : row1 + 1, x0 : x1 + 1] = "S"
    x0, x1, row0, row1 = LAKE
    for row in range(row0, row1 + 1):
        for x in range(x0, x1 + 1):
            if ground[row, x] == AIR:
                ground[row, x] = "W"
    # Ground under the lake, so it does not hang over the lava cavern's roof.
    for x in range(x0, x1 + 1):
        if ground[row1 + 1, x] == AIR:
            ground[row1 + 1, x] = "S"

    # The lava: islands of stone first, then lava wherever the cavern's floor is open.
    for a, b in ISLANDS:
        for x in range(a, b + 1):
            for row in range(LAVA_FROM - 1, HEIGHT):
                if ground[row, x] == AIR:
                    ground[row, x] = "S"
    for row in range(LAVA_FROM, HEIGHT):
        for x in range(WIDTH):
            if ground[row, x] == AIR:
                ground[row, x] = "L"

    # Grass on the meadow's top tiles, then slopes on every one-tile step of the surface.
    for x in range(WIDTH):
        row = top[x]
        if ground[row, x] == "D" and (row == 0 or ground[row - 1, x] == AIR):
            ground[row, x] = "G"
    for x in range(WIDTH):
        row = top[x]
        for side, kind in ((-1, "up_right"), (1, "up_left")):
            other = x + side
            if not 0 <= other < WIDTH or top[other] != row + 1:
                continue
            # The neighbour is a tile lower: a slope on top of it, rising to this column.
            if ground[row, other] != AIR or ground[row + 1, other] not in "GA" or ground[row, x] not in "GA":
                continue
            sand = ground[row + 1, other] == "A"
            ground[row, other] = {("up_right", False): "/", ("up_left", False): "\\", ("up_right", True): "(", ("up_left", True): ")"}[(kind, sand)]
    for k in range(1, 4):
        if ground[15 + k, 44 + k] == "}":
            ground[15 + k, 44 + k] = "\\"
            if ground[16 + k, 44 + k] == "D":
                ground[16 + k, 44 + k] = "G"

    for x, row in TORCHES:
        if ground[row, x] == AIR:
            ground[row, x] = TORCH_CHAR
        else:
            raise SystemExit(f"torch at {x},{row} is not in the open ({ground[row, x]!r})")
    if ground[SPAWN[1], SPAWN[0]] != AIR:
        raise SystemExit("the hero's start is not in the open")
    ground[SPAWN[1], SPAWN[0]] = SPAWN_CHAR
    return ["".join(row) for row in ground], ["".join(row) for row in wall]


def periodic_noise(width: int, r) -> np.ndarray:
    xs = np.arange(width)
    total = np.zeros(width)
    for k, amp in ((2, 1.2), (5, 0.8), (9, 0.5)):
        total += amp * np.sin(2 * np.pi * k * xs / width + r.uniform(0, 6.283))
    return total


def blot(ground: np.ndarray, r, char: str, into: str, x: int, row: int, size: int, top=None):
    """A pocket of `char` grown from a cell by a random walk, only through `into`
    (and, given the surface, never in its top two tiles)."""
    for _ in range(size * 3):
        deep = top is None or (0 <= x < WIDTH and row >= top[x] + 2)
        if deep and 0 <= row < HEIGHT and 0 <= x < WIDTH and ground[row, x] == into:
            ground[row, x] = char
        x += r.choice([-1, 0, 1])
        row += r.choice([-1, 0, 0, 1])


# Pictures worth taking of the map: (name, the place it shows, the hour of the day).
SHOTS = [
    ("surface_sunset", "surface", 18.75),
    ("surface_night", "surface", 23.0),
    ("pond_morning", "pond", 6.9),
    ("dunes_sunset", "dunes", 18.6),
    ("tunnel_dusk", "tunnel", 19.6),
]

HEADER = """# Tiles V2 sample map, written by tools/tiles_v2/make_sample_map.py (edit that, not this).
# First grid, what stands in a cell: G grass, D dirt, S stone, A sand, C clay, O copper ore,
# slopes / \\ (grass) ( ) (sand) { } (dirt) [ ] (stone), W water, L lava, T a wall torch,
# P the hero's start, . air. Second grid, the wall behind it: d dirt wall, s stone wall.
# Row 0 is the top. @surface is the row the cave backdrop starts at; @view names a place
# (the middle of the camera's view of it); @shot names a picture: a place at an hour.
"""


def main():
    terrain, walls = build()
    lines = [HEADER.rstrip("\n"), f"@surface {SURFACE_LINE}"]
    lines += [f"@view {name} {x:g} {row:g}" for name, (x, row) in VIEWS.items()]
    lines += [f"@shot {name} {view} {hour:g}" for name, view, hour in SHOTS]
    text = "\n".join(lines + terrain + [""] + walls) + "\n"
    with open(SAMPLE_MAP, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    print(f"Tiles V2: wrote the {WIDTH} x {HEIGHT} sample map to {SAMPLE_MAP}")


if __name__ == "__main__":
    main()
