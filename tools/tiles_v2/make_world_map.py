#!/usr/bin/env python3
"""Writes the Tiles V2 world, Assets/Art/TilesV2/world_map.txt: one map with Paradise
in the sky, the Earth under it and the nine circles of Hell under that, walked
from end to end without a break.

    python3 tools/tiles_v2/make_world_map.py            # write the map
    python3 tools/tiles_v2/make_world_map.py --check    # and play it through in the model (tv2_reach.py)

How the world is laid out (docs/art/tiles-v2-world.md says why):

- The frame is fixed and written here by hand: which region lies where, the one
  road through each, where its gates, shrines and resting places are. Only the
  rock's small shapes come from noise.
- The road winds down: each circle is crossed from one end to the other, and the
  way down to the next is at its far end, so the circles are walked east to west
  and west to east in turn.
- The Pit is the spine: a shaft down the world's west edge, from the Earth to the
  ninth circle. Every circle has an antechamber at its west end with a ladder of
  ledges to the Pit, too far apart to climb without the Wings, which wait at the
  very bottom. So the Pit is the fast road once the descent is done, and the way
  up to Paradise.
- An ability is found in a room that is left only by using it, and what it opens
  is needed again at once (a gate of the same kind follows).
"""
from __future__ import annotations

import argparse
import math
import os
import sys

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_reach

WORLD_MAP = os.path.join(OUT, "world_map.txt")
WIDTH, HEIGHT = 208, 624
AIR = "."
SURFACE_LINE = 90  # the row the rock behind the caves starts at: under every dip of the surface
PIT = (6, 18)  # the Pit's columns: first, one past the last
PIT_LEDGE = 14  # its ledges run from this column to its east side
PIT_ROWS = (108, 600)  # its top, its floor
POND = (112, 128)  # the columns of the meadow the Earth's pond lies in
DOOR_X = (38, 41)  # the columns of an antechamber's ladder to the Pit
LADDER = 6  # rows between the ledges only the Wings climb

# The regions, top to bottom: (key, first row). What each is built of: its ground, the
# wall behind its tunnels, its ledges, the lamp on a tunnel's wall, the lamp standing
# in a hall, the columns between lamps, whether its halls have a wall behind them and
# whether it is built (square, no slopes) rather than grown.
BANDS = [
    ("paradise", 0),
    ("earth", 62),
    ("limbo", 150),
    ("lust", 206),
    ("gluttony", 254),
    ("greed", 298),
    ("wrath", 350),
    ("heresy", 398),
    ("violence", 454),
    ("fraud", 506),
    ("treachery", 554),
]
THEMES = {
    "paradise": ("M", "m", ",", "J", "J", 8, False, True),
    "earth": ("S", "s", '"', "T", "T", 10, True, False),
    "limbo": ("H", "h", '"', "T", "J", 12, False, False),
    "lust": ("R", "r", "'", "T", "*", 11, False, False),
    "gluttony": ("U", "u", '"', "T", "*", 13, False, False),
    "greed": ("N", "n", "'", "J", "J", 9, False, False),
    "wrath": ("Y", "y", '"', "T", "*", 13, False, False),
    "heresy": ("B", "b", "'", "*", "*", 9, True, True),
    "violence": ("V", "v", "'", "T", "*", 11, False, False),
    "fraud": ("F", "f", "'", "T", "T", 14, True, False),
    "treachery": ("Z", "i", "?", "^", "^", 9, False, False),
}
THEME_KEYS = ("ground", "wall", "ledge", "wall_lamp", "floor_lamp", "gap", "hall_wall", "built")
GROUND = set(TERRAIN_CHARS)
LIQUID = set(LIQUID_CHARS)
NEED = tv2_reach.BIT


class Clash(Exception):
    pass


def hash01(*parts) -> float:
    """A fixed number in 0 .. 1 for a set of whole numbers."""
    h = 2166136261
    for p in parts:
        h = ((h ^ (int(p) & 0xFFFFFFFF)) * 16777619) & 0xFFFFFFFF
        h ^= h >> 13
    h = (h * 0x5BD1E995) & 0xFFFFFFFF
    return ((h ^ (h >> 15)) & 0xFFFFFF) / float(1 << 24)


def wave(key: int, x: float, scale: float) -> float:
    """Smooth noise along a line, -1 .. 1."""
    i = math.floor(x / scale)
    t = x / scale - i
    t = t * t * (3 - 2 * t)
    return (hash01(key, i) * (1 - t) + hash01(key, i + 1) * t) * 2 - 1


def field(key: int, x: float, y: float, scale: float) -> float:
    """Smooth noise over the plane, -1 .. 1."""
    i, j = math.floor(x / scale), math.floor(y / scale)
    u, v = x / scale - i, y / scale - j
    u, v = u * u * (3 - 2 * u), v * v * (3 - 2 * v)
    top = hash01(key, i, j) * (1 - u) + hash01(key, i + 1, j) * u
    low = hash01(key, i, j + 1) * (1 - u) + hash01(key, i + 1, j + 1) * u
    return (top * (1 - v) + low * v) * 2 - 1


class World:
    def __init__(self):
        self.t = [[AIR] * WIDTH for _ in range(HEIGHT)]
        self.walls = [[AIR] * WIDTH for _ in range(HEIGHT)]
        self.owner = [[0] * WIDTH for _ in range(HEIGHT)]
        self.walled: set = set()
        self.square: set = set()  # cells whose steps stay square: nothing built is sloped
        self.friends: dict[int, set] = {}
        self.marks: list = []
        self.views: list = []
        self.shots: list = []
        self.links: list = []
        self.gates: set = set()
        self.spawn = (0, 0)
        self.surface = [0] * WIDTH
        self.fill()

    # --- The rock ------------------------------------------------------------------

    def band(self, x: int, y: int) -> int:
        """The region a cell's rock belongs to: the edges between regions wander."""
        index = 0
        for i, (_, top) in enumerate(BANDS):
            if i and y >= top + int(round(3 * wave(900 + i, x, 13.0))):
                index = i
        return index

    def theme_at(self, y: int) -> dict:
        index = max(i for i, (_, top) in enumerate(BANDS) if y >= top)
        return dict(zip(THEME_KEYS, THEMES[BANDS[index][0]]), key=BANDS[index][0])

    def rock(self, x: int, y: int) -> str:
        """What the ground is made of at a cell, were it solid."""
        key = BANDS[self.band(x, y)][0]
        if key in ("paradise", "earth"):
            depth = y - self.surface[x]
            if depth <= 0:
                return "G"
            return "D" if depth < 6 + int(round(2 * wave(31, x, 7.0))) else "S"
        return THEMES[key][0]

    def fill(self):
        for x in range(WIDTH):
            rise = 3.6 * wave(11, x, 26.0) + 1.4 * wave(12, x, 8.0)
            self.surface[x] = max(77, min(86, int(round(82 + rise))))
        # The pond lies in a level meadow, and the ground steps down to it.
        level = max(self.surface[x] for x in range(*POND))
        for x in range(WIDTH):
            away = max(POND[0] - x, x - POND[1] + 1, 0)
            self.surface[x] = max(self.surface[x], level - away) if away else level
        for x in range(WIDTH):
            for y in range(self.surface[x], HEIGHT):
                self.t[y][x] = self.rock(x, y)

    # --- Carving -------------------------------------------------------------------

    def new_owner(self, parent: int | None = None) -> int:
        owner = len(self.friends) + 1
        self.friends[owner] = set()
        if parent:
            self.befriend(owner, parent)
        return owner

    def befriend(self, a: int, b: int):
        self.friends[a].add(b)
        self.friends[b].add(a)

    def open(self, x: int, y: int, owner: int, wall: bool = True):
        """Makes a cell air for one of the world's spaces. Two spaces that were not
        meant to meet must not: that is a mistake in the layout, and it stops here."""
        if not (2 <= x < WIDTH - 2 and 2 <= y < HEIGHT - 2):
            raise Clash(f"({x}, {y}) is at the map's edge")
        for nx, ny in ((x, y), (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            other = self.owner[ny][nx]
            if other and other != owner and other not in self.friends[owner]:
                raise Clash(f"space {owner} runs into space {other} at ({nx}, {ny})")
        if self.t[y][x] in GROUND or self.t[y][x] in LIQUID:
            self.t[y][x] = AIR
        self.owner[y][x] = owner
        if wall:
            self.walled.add((x, y))

    def solid(self, x: int, y: int, char: str | None = None, force: bool = False):
        """Makes a cell ground (its region's own, or `char`) if it is not already."""
        if force or self.t[y][x] not in GROUND or (char and self.t[y][x] != char):
            self.t[y][x] = char or self.rock(x, y)
            self.owner[y][x] = 0
            self.walled.discard((x, y))

    def column(self, x: int, floor: int, tall: int, owner: int, wall: bool = True, char: str | None = None):
        """A column of a passage: `tall` cells of air standing on a floor two cells thick."""
        for y in range(floor - tall, floor):
            self.open(x, y, owner, wall)
        for y in (floor, floor + 1):
            self.solid(x, y, char if y == floor else None)

    def put(self, x: int, y: int, char: str):
        if self.t[y][x] == AIR:
            self.t[y][x] = char

    def mark(self, kind: str, x: int, y: int, *rest):
        self.marks.append((kind, x, y, [str(r) for r in rest]))

    def view(self, name: str, x: float, y: float):
        self.views.append((name, x, y))

    # --- Finishing -----------------------------------------------------------------

    def blot(self, key: int, char: str, into: str, x: int, y: int, size: int):
        """A patch of one ground in another, grown from a cell by a random walk."""
        for step in range(size):
            if 2 <= x < WIDTH - 2 and 2 <= y < HEIGHT - 2 and self.t[y][x] in into and (x, y) not in self.square:
                self.t[y][x] = char
            turn = int(hash01(key, step) * 4)
            x += (1, -1, 0, 0)[turn]
            y += (0, 0, 1, -1)[turn]

    def veins(self):
        """Ore and the second grounds, scattered through each region's rock."""
        plans = {
            "earth": [("O", "S", 60, 9), ("C", "DS", 26, 14)],
            "limbo": [("S", "H", 22, 16)],
            "gluttony": [("D", "U", 30, 14)],
            "greed": [("$", "N", 150, 8)],
            "wrath": [("U", "Y", 30, 14)],
            "violence": [("E", "V", 26, 18)],
            "treachery": [("I", "Z", 90, 18)],
        }
        for index, (key, top) in enumerate(BANDS):
            bottom = BANDS[index + 1][1] if index + 1 < len(BANDS) else HEIGHT
            for n, (char, into, count, size) in enumerate(plans.get(key, [])):
                for k in range(count):
                    x = 2 + int(hash01(index, n, k, 1) * (WIDTH - 4))
                    y = top + 3 + int(hash01(index, n, k, 2) * (bottom - top - 6))
                    self.blot(index * 1000 + n * 100 + k, char, into, x, y, size)
        # Where two regions meet, each one's rock is flecked with the other's.
        for index in range(2, len(BANDS)):
            upper, lower = THEMES[BANDS[index - 1][0]][0], THEMES[BANDS[index][0]][0]
            top = BANDS[index][1]
            for k in range(70):
                x = 2 + int(hash01(index, k, 3) * (WIDTH - 4))
                dy = int(hash01(index, k, 4) * 7)
                self.blot(index * 77 + k, upper, lower, x, top + 2 + dy, 5)
                self.blot(index * 79 + k, lower, upper, x, top - 3 - dy, 5)

    def slopes(self):
        """A step of one tile becomes a slope where the ground can be sloped: the
        air cell over the low side, against the high side."""
        slope_of = {name.split(":")[0] + ":" + name.split(":")[1]: c for c, name in TERRAIN_CHARS.items() if ":" in name}
        t = self.t

        def hard(x, y):
            return t[y][x] in GROUND and ":" not in TERRAIN_CHARS[t[y][x]]

        def empty(x, y):
            return t[y][x] == AIR

        marked = {(mark[1], mark[2]) for mark in self.marks}  # what stands in a cell stands on the level
        for y in range(3, HEIGHT - 3):
            for x in range(3, WIDTH - 3):
                if t[y][x] != AIR or (x, y) in self.square or (x, y) in marked or not hard(x, y + 1) or not empty(x, y - 1):
                    continue
                for side, kind in ((1, "up_right"), (-1, "up_left")):
                    if hard(x + side, y) and empty(x - side, y) and empty(x + side, y - 1) and (x + side, y) not in self.square:
                        char = slope_of.get(TERRAIN_CHARS[t[y][x + side]] + ":" + kind)
                        if char and hash01(x, y, 5) < 0.8:
                            t[y][x] = char
                        break

    def wall_up(self):
        """The wall behind every walled space, and a cell round it so no edge shows."""
        for x, y in self.walled:
            key = self.theme_at(y)["key"]
            char = THEMES[key][1]
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if self.walls[ny][nx] == AIR and ((dx == 0 and dy == 0) or self.t[ny][nx] in GROUND):
                        self.walls[ny][nx] = "d" if key == "earth" and self.rock(nx, ny) in "GD" else char


class Path:
    """One road through the world, laid a piece at a time from where the last one ended:
    `x` is the next column, `floor` the row of the ground there, `d` the way it runs
    (1 east, -1 west)."""

    def __init__(self, world: World, x: int, floor: int, d: int, parent: "Path | None" = None, lamps: bool = True):
        self.w, self.x, self.floor, self.d = world, x, floor, d
        self.owner = world.new_owner(parent.owner if parent else None)
        self.lamps = lamps
        self.since = 6
        self.raised = (0, 0)
        self.root = parent.root if parent else self
        self.trail: dict[int, int] = {}  # the floor under each column of plain tunnel or hall this road and its branches laid

    @property
    def theme(self) -> dict:
        return self.w.theme_at(self.floor)

    @property
    def stand(self) -> tuple[int, int]:
        """The cell the hero stands in at the last column laid."""
        return (self.x - self.d, self.floor - 1)

    def lamp(self, x: int, floor: int, wall: bool):
        self.since += 1
        if not self.lamps or self.since < self.theme["gap"]:
            return
        cell = (x, floor - 3) if wall else (x, floor - 1)
        if self.w.t[cell[1]][cell[0]] == AIR:
            self.w.t[cell[1]][cell[0]] = self.theme["wall_lamp"] if wall else self.theme["floor_lamp"]
            self.since = 0

    def walk(self, n: int, dy: int = 0, h: int = 6, wall: bool = True, rough: bool = True, char: str | None = None, square: bool = False):
        """A tunnel of n columns whose floor ends `dy` rows lower."""
        if n < abs(dy):
            raise Clash(f"a walk of {n} columns cannot drop {dy} rows")
        built = self.theme["built"] or square
        cells = []
        for i in range(n):
            x = self.x + self.d * i
            floor = self.floor + int(round(dy * (i + 1) / n))
            tall = h
            if rough and not built:
                tall += int(round(1.2 + 1.2 * wave(self.owner * 7 + 1, x, 5.0)))
            if self.raised[0] > 0:
                tall += self.raised[1]
                self.raised = (self.raised[0] - 1, self.raised[1])
            self.w.column(x, floor, tall, self.owner, wall, char)
            if built:
                self.w.square.update((x, y) for y in range(floor - tall - 1, floor + 2))
            self.lamp(x, floor, wall)
            cells.append((x, floor))
            self.root.trail[x] = floor
        self.x += self.d * n
        self.floor += dy
        return cells

    def reach(self, x: int, floor: int, **more):
        """A tunnel on to a column and a floor."""
        return self.walk(abs(x - self.x), floor - self.floor, **more)

    def hall(self, n: int, h: int = 11, dy: int = 0, wall: bool | None = None, char: str | None = None, flat: bool = False, name: str | None = None):
        """A cavern of n columns: its roof an arch, the rock behind the caves showing through it."""
        if wall is None:
            wall = self.theme["hall_wall"]
        built = self.theme["built"] or flat
        cells = []
        for i in range(n):
            x = self.x + self.d * i
            floor = self.floor + int(round(dy * (i + 1) / n))
            tall = h
            if not built:
                arch = math.sin(math.pi * (i + 0.5) / n) ** 0.6
                tall = max(5, int(round(h * (0.45 + 0.55 * arch) + 1.3 * wave(self.owner * 7 + 2, x, 4.0))))
            if self.raised[0] > 0:
                tall = max(tall, 5 + self.raised[1])
                self.raised = (self.raised[0] - 1, self.raised[1])
            self.w.column(x, floor, tall, self.owner, wall, char)
            if built:
                self.w.square.update((x, y) for y in range(floor - tall - 1, floor + 2))
            self.lamp(x, floor, wall)
            cells.append((x, floor))
            self.root.trail[x] = floor
        if name:
            self.w.view(name, self.x + self.d * (n - 1) / 2 + 0.5, self.floor + dy / 2 - min(h, 14) / 2)
        self.x += self.d * n
        self.floor += dy
        return cells

    def ledge(self, dy: int):
        """The floor drops `dy` rows in one step: down is easy, back up is not."""
        self.floor += dy
        self.raised = (3, dy)

    def pool(self, n: int, liquid: str, depth: int = 3, h: int = 7, stones: bool | None = None, wall: bool = True, bridge: bool = False):
        """The floor gives way to a liquid for n columns. One that hurts is crossed on
        stones two tiles wide, three apart (or on a bridge of ledges); the others are swum."""
        if stones is None:
            stones = liquid in "L!%" and not bridge
        cells = []
        for i in range(n):
            x = self.x + self.d * i
            stone = stones and i % 5 in (3, 4)
            tall = h + int(round(1.2 + 1.2 * wave(self.owner * 7 + 3, x, 5.0)))
            for y in range(self.floor - tall, self.floor + depth):
                self.w.open(x, y, self.owner, wall)
            for y in range(self.floor, self.floor + depth):
                if stone:
                    self.w.solid(x, y)
                    self.w.square.add((x, y))
                else:
                    self.w.t[y][x] = self.theme["ledge"] if bridge and y == self.floor else liquid
            for y in (self.floor + depth, self.floor + depth + 1):
                self.w.solid(x, y)
            if wall or stone:
                self.lamp(x, self.floor, wall)
            cells.append((x, self.floor))
        self.x += self.d * n
        return cells

    def siphon(self, n: int, liquid: str = "~", depth: int = 6):
        """A flooded dip under a wall of rock that comes down into the liquid: dived through."""
        for i in range(n):
            x = self.x + self.d * i
            inner = 2 <= i < n - 2
            for y in range(self.floor - (0 if inner else 6), self.floor + depth):
                if inner and y < self.floor + 2:
                    continue
                self.w.open(x, y, self.owner, True)
                if y >= self.floor:
                    self.w.t[y][x] = liquid
            for y in (self.floor + depth, self.floor + depth + 1):
                self.w.solid(x, y)
            if inner:
                for y in range(self.floor - 2, self.floor + 2):
                    self.w.solid(x, y)
        self.x += self.d * n

    def chasm(self, n: int = 11, liquid: str = "L", h: int = 9):
        """A gap too wide to jump, a liquid that hurts three rows down in it: glided over."""
        for i in range(n):
            x = self.x + self.d * i
            for y in range(self.floor - h, self.floor + 6):
                self.w.open(x, y, self.owner, True)
                if y >= self.floor + 3:
                    self.w.t[y][x] = liquid
            for y in (self.floor + 6, self.floor + 7):
                self.w.solid(x, y)
            self.w.square.update((x, y) for y in range(self.floor - 1, self.floor + 3))
        self.w.square.update((self.x - self.d, y) for y in range(self.floor - 1, self.floor + 3))
        self.w.square.update((self.x + self.d * n, y) for y in range(self.floor - 1, self.floor + 3))
        self.x += self.d * n

    def shaft(self, dy: int, w: int = 6, back: bool = False):
        """A shaft `dy` rows down (or up, below zero) with a ledge every three rows on the
        side its upper floor is on, the other half open to fall through. It is left
        at the far side, or with `back` on the side it was entered, under (or over)
        the road that led here."""
        top, bottom = min(self.floor, self.floor + dy), max(self.floor, self.floor + dy)
        columns = [self.x + self.d * i for i in range(w)]
        ledges = columns[:3] if dy > 0 or back else columns[-3:]
        for x in columns:
            for y in range(top - 6, bottom):
                self.w.open(x, y, self.owner, True)
            for y in (bottom, bottom + 1):
                self.w.solid(x, y)
            self.w.square.update((x, y) for y in range(top - 6, bottom + 2))
        row = top
        while row <= bottom - 2:
            for x in ledges:
                self.w.t[row][x] = self.theme["ledge"]
            row += 3
        self.floor += dy
        if back:
            self.d = -self.d
            self.x = columns[0] + self.d
        else:
            self.x = columns[-1] + self.d

    def smooth(self, dy: int, w: int = 4, back: bool = False):
        """A shaft with nothing to stand on: fallen down freely, climbed only by Wall Grip."""
        top, bottom = min(self.floor, self.floor + dy), max(self.floor, self.floor + dy)
        columns = [self.x + self.d * i for i in range(w)]
        before = self.stand
        for x in columns:
            for y in range(top - 6, bottom):
                self.w.open(x, y, self.owner, True)
            for y in (bottom, bottom + 1):
                self.w.solid(x, y)
            self.w.square.update((x, y) for y in range(top - 6, bottom + 2))
        self.floor += dy
        if back:
            self.d = -self.d
            self.x = columns[0] + self.d
        else:
            self.x = columns[-1] + self.d
        if dy > 0:
            self.w.links.append(((columns[0], bottom - 1), before, NEED["WallGrip"], False))
        else:
            self.w.links.append((before, (self.x, top - 1), NEED["WallGrip"], False))

    def frame(self) -> tuple[int, int]:
        """Five square columns four tiles high: where a gate stands. Returns its middle column and its top row."""
        start = self.x
        for i in range(5):
            x = self.x + self.d * i
            self.w.column(x, self.floor, 4, self.owner, True)
            self.w.square.update((x, y) for y in range(self.floor - 6, self.floor + 2))
        self.x += self.d * 5
        self.since += 5
        return start + self.d * 2, self.floor - 4

    def gate(self, kind: str):
        """A gate across the road: a light barrier (Light Dash goes through it) or a seal a bolt of Holy Light breaks."""
        x, top = self.frame()
        need = NEED["LightDash"] if kind == "barrier" else NEED["HolyLight"]
        if kind == "barrier":
            self.w.mark("barrier", x, top, 4)
        else:
            self.w.mark("seal", x, top, "bolt", 1, 4)
        self.w.gates.update((x, y) for y in range(top, top + 4))
        self.w.links.append(((x - self.d, self.floor - 1), (x + self.d, self.floor - 1), need, True))

    def slab(self, dy: int):
        """A cracked slab in the floor that only a Smite breaks, and the drop under it."""
        columns = [self.x + self.d * i for i in range(5)]
        for x in columns:
            self.w.column(x, self.floor, 6, self.owner, True)
            self.w.square.update((x, y) for y in range(self.floor - 7, self.floor + 2))
        hole = columns[1:4]
        for x in hole:
            for y in range(self.floor, self.floor + dy):
                self.w.t[y][x] = AIR
                self.w.open(x, y, self.owner, True)
            for y in (self.floor + dy, self.floor + dy + 1):
                self.w.solid(x, y)
            self.w.gates.update(((x, self.floor), (x, self.floor + 1)))
            self.w.square.update((x, y) for y in range(self.floor, self.floor + dy + 2))
        self.w.mark("seal", min(hole), self.floor, "slam", 3, 2)
        above, below = (columns[0], self.floor - 1), (hole[1], self.floor + dy - 1)
        self.w.links.append((above, below, NEED["Smite"], False))
        self.w.links.append((below, above, NEED["Smite"] | NEED["WallGrip"], False))
        self.x = columns[4]
        self.floor += dy

    def shrine(self, ability: str):
        """The room an ability waits in: eleven square columns, a plinth in the middle, a light either side."""
        cells = []
        for i in range(11):
            x = self.x + self.d * i
            tall = 7 if i in (0, 10) else 8
            if self.raised[0] > 0:
                tall += self.raised[1]
                self.raised = (self.raised[0] - 1, self.raised[1])
            self.w.column(x, self.floor, tall, self.owner, True)
            self.w.square.update((x, y) for y in range(self.floor - 9, self.floor + 2))
            cells.append(x)
        for x in cells[4:7]:
            self.w.solid(x, self.floor - 1, force=True)
        self.w.mark("ability", cells[5], self.floor - 2, ability)
        for x in (cells[2], cells[8]):
            self.w.put(x, self.floor - 1, "^" if self.theme["floor_lamp"] != "J" else "J")
        self.x += self.d * 11
        self.since = 0

    def rest(self):
        """A place to rest: the game's checkpoint, with a lamp beside it."""
        x, y = self.stand
        self.w.mark("checkpoint", x, y)
        self.w.put(x + self.d, y, "J")

    def relic(self, x: int, y: int):
        self.w.mark("relic", x, y)

    def perch(self, x: int, row: int, wide: int = 4, relic: bool = False):
        """A ledge hung in a hall, with something on it worth the climb."""
        for i in range(wide):
            self.w.t[row][x + i] = self.theme["ledge"]
        if relic:
            self.relic(x + wide // 2, row - 1)

    def antechamber(self, char: str | None = None, name: str | None = None):
        """The room at a circle's west end: a place to rest, and the ladder to the Pit,
        its ledges too far apart for anyone without the Wings."""
        floor = self.floor
        first = self.x
        for x in range(first, 29, -1):
            self.w.column(x, floor, 10, self.owner, False, char)
            self.w.square.update((x, y) for y in range(floor - 11, floor + 2))
        self.x = 29
        # The hole in the roof and the tunnel from it to the Pit, twelve rows over the floor.
        top = floor - 2 * LADDER
        for x in range(PIT[1], DOOR_X[1]):
            for y in range(top - 4, top):
                self.w.open(x, y, self.owner, True)
            self.w.square.update((x, y) for y in range(top - 5, top + 2))
            if not DOOR_X[0] <= x:
                for y in (top, top + 1):
                    self.w.solid(x, y)
        for x in range(*DOOR_X):
            for y in (top, top + 1):
                self.w.open(x, y, self.owner, True)
            self.w.t[top][x] = self.theme["ledge"]
            self.w.t[top + LADDER][x] = self.theme["ledge"]
        self.w.put(PIT[1] + 1, top - 1, self.theme["floor_lamp"])
        self.w.mark("checkpoint", first - 6 * 1, floor - 1)
        self.w.put(first - 5, floor - 1, "J")
        self.w.put(32, floor - 1, self.theme["floor_lamp"])
        if name:
            self.w.view(name, 42, floor - 7)


def level_run(road: Path, x: int, wide: int = 4, search: int = 8) -> int:
    """The first column of the run of `wide` level columns of a road's plain floor nearest to x."""
    trail = road.root.trail
    for away in range(search + 1):
        for start in (x - away, x + away):
            floors = [trail.get(start + i) for i in range(wide)]
            if floors[0] is not None and all(f == floors[0] for f in floors):
                return start
    raise Clash(f"no level stretch of the road near column {x}")


def firm(world: World, x: int, rows: range):
    """Stops the layout where a side road would cut into something: every cell must still be rock."""
    for y in rows:
        if world.t[y][x] not in GROUND or world.owner[y][x]:
            raise Clash(f"a side road would cut into something at ({x}, {y})")


def hatch(world: World, road: Path, x: int, floor: int, low: int, ledge_side: int) -> None:
    """A way down through a road's floor: four columns from x, covered by a ledge level with
    the floor (dropped through), open to `low`, with a ledge every three rows on one side."""
    columns = range(x, x + 4)
    ledge = world.theme_at(floor)["ledge"]
    if any(mark[1] in columns and mark[2] == floor - 1 for mark in world.marks):
        raise Clash(f"a hatch at column {x} would open under something that stands there")
    for column in columns:
        firm(world, column, range(floor + 2, low + 2))
        for y in range(floor, low):
            world.open(column, y, road.owner, True)
        for y in (low, low + 1):
            world.solid(column, y)
        world.square.update((column, y) for y in range(floor - 1, low + 2))
        world.t[floor][column] = ledge
    side = list(columns)[:2] if ledge_side < 0 else list(columns)[2:]
    for row in range(floor + 3, low - 1, 3):
        for column in side:
            world.t[row][column] = ledge


def low_road(road: Path, x0: int, x1: int, depth: int = 17, lay=None) -> Path:
    """A second way under a stretch of a road: down through a hatch near column x0, along
    under the road and up through a hatch near x1. `lay(side)` lays what is between the
    hatches, a road running east that must end `side.span` columns on; a plain tunnel if None."""
    world = road.w
    trail = road.root.trail
    a, b = level_run(road, x0), level_run(road, x1)
    if b - a < 12:
        raise Clash(f"a low road from {x0} to {x1} is too short")
    low = max(trail[x] for x in range(a, b + 4) if x in trail) + depth
    side = Path(world, a + 4, low, 1, road)
    side.theme_gap = 2
    side.span = b - (a + 4)
    hatch(world, side, a, trail[a], low, -1)
    hatch(world, side, b, trail[b], low, 1)
    for x in range(a + 4, b):
        firm(world, x, range(low - 10, low + 2))
    start = side.x
    (lay or (lambda r: r.walk(r.span)))(side)
    if side.x != start + side.span or side.floor != low:
        raise Clash(f"the low road from {x0} ends at ({side.x}, {side.floor}), not ({start + side.span}, {low})")
    return side


def attic(road: Path, x: int, wide: int = 9, wings: bool = True, east: bool = True):
    """A nook over a road with a relic in it: a ladder of ledges up through the roof near
    column x (six rows apart for the Wings, three for anyone) and a small room beside its head."""
    world = road.w
    a = level_run(road, x)
    floor = road.root.trail[a]
    rise = 12
    nook = Path(world, a + 4 if east else a - 1, floor - rise, 1 if east else -1, road, lamps=False)
    top = floor - rise - 6
    for column in range(a, a + 4):
        ceiling = next(y for y in range(floor - 1, 0, -1) if world.t[y][column] in GROUND)
        firm(world, column, range(top - 1, ceiling - 1))
        for y in range(top, ceiling + 1):
            world.open(column, y, nook.owner, True)
        world.square.update((column, y) for y in range(top - 1, floor))
    side = range(a + 2, a + 4) if east else range(a, a + 2)
    for row in range(floor - rise, floor, LADDER if wings else 3):
        for column in side:
            world.t[row][column] = nook.theme["ledge"]
    for i in range(wide):
        column = nook.x + nook.d * i
        firm(world, column, range(top - 1, floor - rise + 3))
    cells = nook.walk(wide, h=5, rough=False, square=True)
    world.mark("relic", cells[-2][0], cells[-2][1] - 1)
    world.put(cells[-3][0], cells[-3][1] - 1, "^")


def descend_west(road: Path, dy: int, kind: str = "shaft", upper_char: str | None = None, lower_char: str | None = None) -> Path:
    """The end of a westward circle: the way down to the next one, the antechambers of
    both, and the next circle's road, which starts east from the foot of the way down."""
    world = road.w
    top = road.floor
    if kind == "slab":
        first = road.x
        road.slab(dy)
        edge_top, edge_low = first - 5, first - 4
        east = first
    else:
        wide = 6 if kind == "shaft" else 4
        first = road.x
        (road.shaft if kind == "shaft" else road.smooth)(dy, wide)
        edge_top = edge_low = first - wide
        east = first + 1
    upper = Path(world, edge_top, top, -1, road)
    upper.antechamber(upper_char)
    lower = Path(world, edge_low, top + dy, -1, road)
    lower.antechamber(lower_char)
    following = Path(world, east, top + dy, 1, road)
    return following


# --- The regions -------------------------------------------------------------------


def earth(world: World) -> Path:
    surface = world.surface
    world.spawn = (96, surface[96] - 1)
    world.view("earth", 100, 74)
    # Ruins by the start: what is left of a colonnade.
    for x, tall in ((84, 3), (85, 1), (88, 2), (104, 3), (105, 1), (108, 1)):
        for y in range(surface[x] - tall, surface[x]):
            world.t[y][x] = "M"
            world.square.add((x, y))
    # A pond.
    low = surface[POND[0]]
    for x in range(POND[0] + 2, POND[1] - 2):
        for y in range(low, low + 3):
            world.t[y][x] = "W"
        world.t[low + 3][x] = "D"

    # The way down: from a mouth under the Gate's arch, west and down to the crossroads.
    road = Path(world, 176, surface[176], -1)
    road.walk(30, 102 - surface[176], h=5)
    road.hall(14, 9, name="earth_crossroads")
    road.rest()
    # The arch over the mouth, of the City's own brick: the first sight of what lies under.
    crown = min(surface[x] for x in range(164, 183)) - 9
    for x in (180, 181, 166, 165):
        for y in range(crown, surface[x] + 6):
            world.walls[y][x] = "b"
    for x in range(164, 183):
        for y in (crown - 2, crown - 1):
            world.walls[y][x] = "b"
    world.put(179, surface[179] - 1, "*")

    # West from the crossroads: the old mine, and at its end the antechamber under the Pit's head.
    mine = Path(world, 126, 102, -1, road)
    mine.walk(18, 8)
    cells = mine.hall(18, 8, 2, name="earth_mine")
    mine.relic(cells[9][0], cells[9][1] - 1)
    mine.walk(14, 8)
    mine.pool(13, "W", 4)
    mine.walk(6, 6)
    mine.antechamber(name="earth_antechamber")

    # Down from the crossroads and east under the way in, to the hall of the Gate.
    road.shaft(15, back=True)
    road.walk(20, 3)
    road.hall(16, 9)
    road.walk(7, 4)
    road.hall(19, 13, name="earth_gate")
    road.rest()
    for x in (180, 181, 182, 190, 191, 192):
        for y in range(road.floor - 9, road.floor):
            world.walls[y][x] = "b"
    for x in range(179, 194):
        for y in (road.floor - 11, road.floor - 10):
            world.walls[y][x] = "b"
    world.put(184, road.floor - 1, "*")
    world.put(188, road.floor - 1, "*")
    low_road(road, 138, 164, lay=lambda r: (r.walk(6), r.hall(r.span - 12, 8), r.walk(6)))
    road.shaft(160 - road.floor, back=True)
    return road


def limbo(road: Path) -> Path:
    road.hall(16, 10, name="limbo_gate")
    road.rest()
    road.walk(10, 4)
    road.pool(13, "~", 4, stones=True)
    road.walk(8, 2)
    cells = road.hall(22, 11, 2, name="limbo")
    road.perch(cells[11][0] - 2, cells[11][1] - 4, relic=True)
    road.walk(8, 4)
    road.ledge(5)
    road.shrine("LightDash")
    road.gate("barrier")
    road.walk(8, 3)
    road.hall(18, 9, 3)
    road.gate("barrier")
    road.reach(61, 186)

    def crypt(r: Path):
        r.walk(4)
        cells = r.hall(r.span - 8, 7)
        r.relic(cells[len(cells) // 2][0], cells[len(cells) // 2][1] - 1)
        r.walk(4)

    low_road(road, 128, 143, lay=crypt)
    low_road(road, 76, 90)
    return descend_west(road, 36, "smooth")


def lust(road: Path) -> Path:
    road.walk(8)
    cells = road.hall(26, 15, name="lust")
    x0 = min(c[0] for c in cells)
    road.perch(x0 + 5, road.floor - 3)
    road.perch(x0 + 11, road.floor - 6)
    road.perch(x0 + 17, road.floor - 9, relic=True)
    road.walk(8, 2)
    road.ledge(6)
    road.shrine("WallGrip")
    road.smooth(-12)
    road.walk(8)
    road.smooth(-7)
    road.walk(6)
    road.smooth(19)
    road.hall(20, 10, 2, name="lust_hollow")
    road.rest()
    road.walk(8, 4)
    road.smooth(-8)
    road.walk(5)
    road.smooth(14)
    road.reach(196, 246)

    def grotto(r: Path):
        r.walk(3)
        r.gate("barrier")
        cells = r.hall(r.span - 11, 8)
        r.relic(cells[-3][0], cells[-3][1] - 1)
        r.walk(3)

    low_road(road, 72, 90, lay=grotto)
    low_road(road, 143, 154)
    attic(road, 120, east=False)
    road.shaft(22, back=True)
    return road


def gluttony(road: Path) -> Path:
    road.hall(14, 8)
    road.rest()
    road.walk(10, 3, h=4)
    road.pool(18, "&", 5)
    road.walk(8, 2, h=4)
    road.hall(24, 9, name="gluttony")
    road.gate("barrier")
    cells = road.pool(23, "&", 6)
    road.relic(cells[11][0], cells[11][1] + 5)
    road.walk(3)
    road.smooth(-7)
    road.walk(8)
    road.smooth(13)
    road.reach(61, 288)

    def larder(r: Path):
        r.walk(3)
        r.pool(r.span - 6, "&", 4, h=5)
        r.walk(3)

    low_road(road, 124, 140, lay=larder)
    low_road(road, 173, 190)
    attic(road, 79)
    return descend_west(road, 30)


def greed(road: Path) -> Path:
    road.walk(8)
    cells = road.hall(24, 12, flat=True, name="greed")
    x0 = min(c[0] for c in cells)
    road.perch(x0 + 3, road.floor - 3, 5)
    road.perch(x0 + 10, road.floor - 6, 5, relic=True)
    road.perch(x0 + 17, road.floor - 3, 5)
    road.walk(8, 3)
    road.ledge(5)
    road.shrine("HolyLight")
    road.gate("seal")
    road.walk(8, 2)
    road.gate("seal")
    road.hall(26, 12, flat=True, name="greed_vault")
    road.rest()
    road.gate("barrier")
    road.smooth(-8)
    road.walk(6)
    road.smooth(14)
    road.walk(8, 3)
    road.gate("seal")
    road.reach(196, 340)

    def hoard(r: Path):
        r.walk(3)
        r.gate("seal")
        cells = r.hall(r.span - 11, 7, flat=True)
        for k in (2, len(cells) // 2, len(cells) - 3):
            r.relic(cells[k][0], cells[k][1] - 1) if k == len(cells) // 2 else None
        r.walk(3)

    low_road(road, 66, 88, lay=hoard)
    low_road(road, 133, 152)
    attic(road, 166)
    road.shaft(24, back=True)
    return road


def wrath(road: Path) -> Path:
    road.hall(14, 8)
    road.rest()
    road.walk(6, 2)
    road.pool(28, "~", 6, h=8, wall=False)
    road.walk(6)
    road.siphon(12)
    road.walk(4)
    cells = road.pool(23, "~", 4, h=10, wall=False)
    road.w.view("wrath", cells[11][0], cells[11][1] - 5)
    road.walk(4)
    road.rest()
    road.gate("seal")
    road.walk(6, 3)
    road.smooth(-8)
    road.walk(5)
    road.smooth(21)
    road.reach(61, 390)
    return descend_west(road, 30)


def heresy(road: Path) -> Path:
    road.walk(8, h=5)
    road.pool(13, "L", 3)
    road.hall(26, 9, name="heresy")
    road.rest()
    road.shaft(-9)
    road.walk(8)
    road.shrine("Glide")
    road.chasm(11, "L")
    road.walk(6)
    road.chasm(11, "L")
    road.w.view("heresy_chasm", road.x - 14, road.floor - 5)
    road.walk(3)
    road.smooth(27)
    road.hall(15, 11, name="heresy_city")
    road.gate("barrier")
    road.reach(196, 444)

    def catacomb(r: Path):
        r.walk(3)
        r.pool(8, "L", 3, h=5)
        cells = r.walk(r.span - 14)
        r.relic(cells[len(cells) // 2][0], cells[len(cells) // 2][1] - 1)
        r.walk(3)

    low_road(road, 85, 104, lay=catacomb)
    road.shaft(26, back=True)
    return road


def violence(road: Path) -> Path:
    road.hall(14, 9)
    road.rest()
    road.pool(18, "!", 3)
    road.walk(6, 2)
    cells = road.hall(28, 13, name="violence_wood")
    x0 = min(c[0] for c in cells)
    road.perch(x0 + 6, road.floor - 3)
    road.perch(x0 + 13, road.floor - 6, relic=True)
    road.perch(x0 + 20, road.floor - 3)
    road.smooth(-8)
    road.walk(6)
    road.ledge(5)
    road.shrine("Smite")
    road.slab(10)
    road.walk(4)
    road.hall(22, 10, char="E", name="violence")
    road.rest()
    road.slab(10)
    road.reach(61, 498)

    def roots(r: Path):
        r.walk(3)
        r.pool(8, "!", 3, h=5)
        cells = r.walk(r.span - 11)
        r.relic(cells[-2][0], cells[-2][1] - 1)

    low_road(road, 132, 152, lay=roots)
    attic(road, 120, east=False)
    return descend_west(road, 30, "slab")


def fraud(road: Path) -> Path:
    road.walk(7)
    road.pool(13, "%", 3)
    road.walk(5)
    road.gate("barrier")
    road.pool(13, "%", 3, bridge=True)
    road.w.view("fraud", road.x - 10, road.floor - 5)
    road.walk(5)
    road.smooth(-7)
    road.walk(5)
    road.smooth(10)
    road.walk(3)
    road.chasm(11, "%")
    road.walk(4)
    road.gate("seal")
    road.pool(18, "%", 3)
    road.walk(3)
    road.slab(9)
    road.hall(17, 10, name="fraud_ledge")
    road.reach(196, 546)
    road.rest()
    low_road(road, 171, 183)
    attic(road, 62)
    road.smooth(26, back=True)
    return road


def treachery(road: Path) -> Path:
    road.hall(16, 9)
    road.rest()
    road.walk(10, 4)
    road.hall(34, 14, char="I", name="treachery")
    road.pool(13, "W", 5)
    road.walk(6, 3)
    road.gate("barrier")
    road.hall(26, 12, char="I", name="treachery_lake")
    road.rest()
    road.gate("seal")
    road.shaft(12)
    road.slab(9)
    road.walk(6, 6)
    road.shrine("Wings")
    room = Path(road.w, road.x, road.floor, -1, road)
    room.antechamber("I", name="treachery_end")

    def under_ice(r: Path):
        r.walk(3)
        cells = r.pool(r.span - 6, "W", 4, h=5)
        r.relic(cells[len(cells) // 2][0], cells[len(cells) // 2][1] + 3)
        r.walk(3)

    low_road(road, 138, 164, lay=under_ice)
    low_road(road, 88, 106)
    return road


def pit(world: World):
    """The shaft down the world's west edge, with a ledge for the Wings every six rows."""
    owner = world.new_owner()
    for other in list(world.friends):
        if other != owner:
            world.befriend(owner, other)
    top, floor = PIT_ROWS
    for y in range(top, floor):
        inset = max(0, 3 - (y - top))  # the head of the shaft is a dome
        for x in range(PIT[0] + inset, PIT[1] - inset):
            world.open(x, y, owner, False)
    for x in range(PIT[0], PIT[1]):
        world.solid(x, floor)
        world.solid(x, floor + 1)
    for row in range(top + LADDER, floor, LADDER):
        for x in range(PIT_LEDGE, PIT[1]):
            world.t[row][x] = world.theme_at(row)["ledge"]
    for row in range(top + 2 * LADDER, floor, 2 * LADDER):
        world.put(PIT[1] - 1, row - 1, world.theme_at(row)["floor_lamp"])
    world.view("pit", 12, 330)


def island(world: World, x0: int, x1: int, top: int, thick: int, crown: str | None = None):
    """A cloud with a flat top: columns x0 .. x1, `thick` rows deep at its middle."""
    middle, half = (x0 + x1) / 2, (x1 - x0) / 2 + 0.5
    for x in range(x0, x1 + 1):
        t = abs(x - middle) / half
        deep = max(1, int(round(thick * math.sqrt(max(1 - t * t, 0)) + 0.8 * wave(700 + top, x, 3.0))))
        for y in range(top, top + deep):
            world.t[y][x] = "Q"
        if crown and deep > 1:
            world.t[top][x] = crown
            world.square.add((x, top))


def paradise(world: World):
    """Islands of cloud over the Earth, climbed from its surface on ledges only the Wings reach."""
    surface = world.surface
    ledge = THEMES["paradise"][2]

    def step(x: int, row: int, wide: int = 4):
        for i in range(wide):
            world.t[row][x + i] = ledge

    # The stair from the Earth.
    foot = min(surface[x] for x in range(48, 58))
    step(53, foot - 6)
    step(48, foot - 12)
    step(53, foot - 18)
    step(48, foot - 24)
    first = foot - 30
    island(world, 55, 86, first, 5)
    world.mark("checkpoint", 72, first - 1)
    world.put(73, first - 1, "J")
    world.view("paradise_stair", 70, first + 8)
    # East and up, to the garden.
    step(89, first - 6)
    garden = first - 12
    island(world, 94, 127, garden, 6, "G")
    for x in range(100, 122):
        world.t[garden + 1][x] = "D"
    world.put(98, garden - 1, "J")
    world.put(123, garden - 1, "J")
    world.view("paradise_garden", 110, garden - 5)
    # A high cloud over the garden, with something on it.
    step(108, garden - 6)
    step(108, garden - 12)
    island(world, 113, 125, garden - 18, 3)
    world.mark("relic", 119, garden - 19)
    # A cloud out west of the stair's head.
    island(world, 40, 44, first + 3, 2)
    island(world, 22, 36, first, 4)
    world.mark("relic", 28, first - 1)
    world.put(32, first - 1, "J")
    # Over the gap that is glided, to the colonnade.
    island(world, 137, 164, garden, 5, "M")
    for x in (141, 147, 153, 159):
        for y in range(garden - 7, garden):
            world.walls[y][x] = "m"
    for x in range(140, 161):
        world.walls[garden - 8][x] = "m"
    world.mark("checkpoint", 150, garden - 1)
    world.put(151, garden - 1, "J")
    # Up to the temple, and the gate at the end of everything.
    step(158, garden - 6)
    step(152, garden - 12)
    temple = garden - 18
    island(world, 156, 197, temple, 7, "M")
    for x in (164, 165, 172, 173, 180, 181, 188, 189):
        for y in range(temple - 9, temple):
            world.walls[y][x] = "m"
    for row, inset in ((temple - 10, 0), (temple - 11, 4), (temple - 12, 8), (temple - 13, 12)):
        for x in range(162 + inset, 192 - inset):
            world.t[row][x] = "M"
            world.square.add((x, row))
    for x in (168, 185):
        world.put(x, temple - 1, "J")
    world.mark("end", 177, temple - 1)
    world.view("paradise", 172, temple - 6)
    return temple


def build() -> World:
    world = World()
    road = earth(world)
    for circle in (limbo, lust, gluttony, greed, wrath, heresy, violence, fraud, treachery):
        road = circle(road)
    pit(world)
    paradise(world)
    world.shots += [("earth_sunset", "earth", 18.75), ("paradise_dawn", "paradise", 6.6), ("paradise_garden_night", "paradise_garden", 22.5)]
    world.veins()
    world.slopes()
    world.wall_up()
    return world


# --- Checking and writing ------------------------------------------------------------

HEADER = """# The Tiles V2 world, written by tools/tiles_v2/make_world_map.py (edit that, not this):
# Paradise, the Earth and the nine circles of Hell on one map. The terrain grid, a
# blank line, the wall grid; the legend is in tiles_v2.json. `@region key row` is
# where a region starts, `@mark kind column row ...` a thing of the game's that
# stands in a cell, `@view` and `@shot` places and pictures for the captures.
"""


def check(world: World, log=print) -> list[str]:
    terrain = ["".join(row) for row in world.t]
    log("Working out where the hero can go:")
    reach = tv2_reach.Reach(terrain, world.gates, world.links, log)
    shrines = {m[3][0]: (m[1], m[2]) for m in world.marks if m[0] == "ability"}
    goals = {f"{m[0]} {i}": (m[1], m[2]) for i, m in enumerate(world.marks) if m[0] in ("checkpoint", "relic", "end")}
    return tv2_reach.journey(reach, world.spawn, shrines, goals, log)


def write(world: World, path: str):
    world.t[world.spawn[1]][world.spawn[0]] = SPAWN_CHAR
    lines = [HEADER.rstrip("\n"), f"@surface {SURFACE_LINE}"]
    lines += [f"@region {key} {top}" for key, top in BANDS]
    lines += [f"@view {name} {x:g} {y:g}" for name, x, y in world.views]
    lines += [f"@shot {name} {view} {hour:g}" for name, view, hour in world.shots]
    lines += [f"@mark {kind} {x} {y} {' '.join(rest)}".rstrip() for kind, x, y, rest in world.marks]
    lines += ["".join(row) for row in world.t]
    lines.append("")
    lines += ["".join(row) for row in world.walls]
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=WORLD_MAP, help="where to write (default: Assets/Art/TilesV2/world_map.txt)")
    parser.add_argument("--check", action="store_true", help="play the world through in the model and fail if it cannot be")
    args = parser.parse_args(argv)
    world = build()
    wrong = check(world) if args.check else []
    write(world, args.out)
    print(f"World: {WIDTH} x {HEIGHT} tiles, {len(world.marks)} marks, {len(world.views)} views -> {args.out}")
    if wrong:
        raise SystemExit("The world cannot be played through:\n  " + "\n  ".join(wrong))


if __name__ == "__main__":
    main()
