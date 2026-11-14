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
import random
import sys

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_reach

WORLD_MAP = os.path.join(OUT, "world_map.txt")
WIDTH, HEIGHT = 600, 700
AIR = "."
GROUND_LINE = 100  # the row the Earth's surface lies about
SURFACE_LINE = 108  # the row the rock behind the caves starts at: under every dip of the surface
PIT = (6, 18)  # the Pit's columns: first, one past the last
PIT_LEDGE = 14  # its ledges run from this column to its east side
PIT_ROWS = (138, 678)  # its top, its floor
POND = (318, 336)  # the columns of the meadow the Earth's pond lies in
WEST = 61  # the column the ways down at the world's west side start at, going west
EAST = WIDTH - 12  # the column the ways down at its east side start at, going east
DOOR_X = (38, 41)  # the columns of an antechamber's ladder to the Pit
LADDER = 6  # rows between the ledges only the Wings climb

# The regions, top to bottom: (key, first row). What each is built of: its ground, the
# wall behind its tunnels, its ledges, the lamp on a tunnel's wall, the lamp standing
# in a hall, the columns between lamps, whether its halls have a wall behind them and
# whether it is built (square, no slopes) rather than grown.
BANDS = [
    ("paradise", 0),
    ("earth", 80),
    ("limbo", 180),
    ("lust", 242),
    ("gluttony", 298),
    ("greed", 352),
    ("wrath", 410),
    ("heresy", 466),
    ("violence", 526),
    ("fraud", 584),
    ("treachery", 640),
]
# The floor each circle's road starts on and ends on: at the east side and at the west,
# where its antechamber is (a multiple of six, as the Pit's ledges are).
FLOORS = {
    "limbo": (196, 222),
    "lust": (258, 282),
    "gluttony": (316, 336),
    "greed": (372, 396),
    "wrath": (430, 450),
    "heresy": (486, 510),
    "violence": (544, 570),
    "fraud": (606, 628),
    "treachery": (662, 684),
}
GATE_HALL = 156  # the floor of the hall of the Gate, on the Earth
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
        self.roads: list = []  # every circle's road: (region, the road, its trail, its locked columns)
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
            self.surface[x] = max(GROUND_LINE - 5, min(GROUND_LINE + 4, int(round(GROUND_LINE + rise))))
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
            "earth": [("O", "S", 200, 9), ("C", "DS", 90, 14)],
            "limbo": [("S", "H", 80, 16)],
            "gluttony": [("D", "U", 90, 14)],
            "greed": [("$", "N", 480, 8)],
            "wrath": [("U", "Y", 90, 14)],
            "violence": [("E", "V", 80, 18)],
            "treachery": [("I", "Z", 300, 18)],
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
            for k in range(180):
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
        self.locked: set[int] = set()  # the columns of its gates, shrines and shafts: no side road passes under them

    def begin(self, region: str) -> "Path":
        """Starts a circle's road here: what is laid from now on is that circle's."""
        self.root = self
        self.trail = {}
        self.locked = set()
        self.w.roads.append((region, self, self.trail, self.locked))
        return self

    def lock(self, columns):
        self.root.locked.update(columns)

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
            self.lock([x])
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
        self.lock(columns)
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
        self.lock(columns)
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
            self.lock([x])
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
        self.lock(columns)
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
        self.lock(self.x + self.d * i for i in range(-2, 13))
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
        self.lock(range(0, first + 1))
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


def level_run(trail: dict, x: int, wide: int = 4, search: int = 8) -> int:
    """The first column of the run of `wide` level columns of a road's plain floor nearest to x."""
    for away in range(search + 1):
        for start in (x - away, x + away):
            floors = [trail.get(start + i) for i in range(wide)]
            if floors[0] is not None and all(f == floors[0] for f in floors):
                return start
    raise Clash(f"no level stretch of the road near column {x}")


def firm(world: World, columns, rows):
    """Stops a side road that would cut into something, before anything is dug: every
    cell of the box must still be rock nobody has claimed."""
    for x in columns:
        for y in rows:
            if not (2 <= x < WIDTH - 2 and 2 <= y < HEIGHT - 2) or world.t[y][x] not in GROUND or world.owner[y][x]:
                raise Clash(f"a side road would cut into something at ({x}, {y})")


def hatch(world: World, road: Path, x: int, floor: int, low: int, ledge_side: int) -> None:
    """A way down through a road's floor: four columns from x, covered by a ledge level with
    the floor (dropped through), open to `low`, with a ledge every three rows on one side."""
    columns = range(x, x + 4)
    ledge = world.theme_at(floor)["ledge"]
    for column in columns:
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


def low_road(road: Path, trail: dict, locked: set, x0: int, x1: int, depth: int = 17, lay=None) -> Path:
    """A second way under a stretch of a road: down through a hatch near column x0, along
    under the road and up through a hatch near x1. `lay(side)` lays what is between the
    hatches, a road running east that must end `side.span` columns on; a plain tunnel if
    None. Everything is checked before anything is dug, so a road that does not fit
    (`Clash`) leaves the world as it was."""
    world = road.w
    a, b = level_run(trail, x0), level_run(trail, x1)
    if b - a < 20:
        raise Clash(f"a low road from {x0} to {x1} is too short")
    reach_ = range(a - 1, b + 5)
    if any(x in locked for x in reach_):
        raise Clash(f"a low road from {x0} to {x1} would pass under a gate")
    hatches = set(range(a, a + 4)) | set(range(b, b + 4))
    if any(mark[1] in hatches and trail[mark[1]] == mark[2] + 1 for mark in world.marks):
        raise Clash(f"a hatch of the low road from {x0} would open under something that stands there")
    low = max(trail[x] for x in range(a, b + 4) if x in trail) + depth
    firm(world, range(a - 1, a + 5), range(trail[a] + 2, low + 9))
    firm(world, range(b - 1, b + 5), range(trail[b] + 2, low + 9))
    firm(world, range(a + 4, b), range(low - 10, low + 9))
    side = Path(world, a + 4, low, 1, road)
    side.span = b - (a + 4)
    hatch(world, side, a, trail[a], low, -1)
    hatch(world, side, b, trail[b], low, 1)
    locked.update(range(a, a + 4))
    locked.update(range(b, b + 4))
    start = side.x
    (lay or (lambda r: r.walk(r.span, rough=False)))(side)
    if side.x != start + side.span or side.floor != low:
        raise SystemExit(f"the low road from {x0} ends at ({side.x}, {side.floor}), not ({start + side.span}, {low})")
    return side


def attic(road: Path, trail: dict, locked: set, x: int, wide: int = 9, wings: bool = True, east: bool = True):
    """A nook over a road with a relic in it: a ladder of ledges up through the roof near
    column x (six rows apart for the Wings, three for anyone) and a small room beside its
    head. Checked before anything is dug, as a low road is."""
    world = road.w
    a = level_run(trail, x)
    if any(c in locked for c in range(a - 1, a + 5)):
        raise Clash(f"an attic at {x} would stand on a gate")
    floor = trail[a]
    rise = 12
    top = floor - rise - 6
    first = a + 4 if east else a - 1
    step = 1 if east else -1
    ceilings = [next((y for y in range(floor - 1, 0, -1) if world.t[y][column] in GROUND), 0) for column in range(a, a + 4)]
    if min(ceilings) < floor - 10:
        raise Clash(f"the roof over column {x} is too high for an attic")
    for column, ceiling in zip(range(a, a + 4), ceilings):
        firm(world, [column], range(top - 1, ceiling - 1))
    firm(world, [a - 1 if east else a + 4], range(top - 1, floor - 10))
    firm(world, [first + step * i for i in range(wide + 1)], range(top - 1, floor - rise + 3))
    nook = Path(world, first, floor - rise, step, road, lamps=False)
    for column, ceiling in zip(range(a, a + 4), ceilings):
        for y in range(top, ceiling + 1):
            world.open(column, y, nook.owner, True)
        world.square.update((column, y) for y in range(top - 1, floor))
    side = range(a + 2, a + 4) if east else range(a, a + 2)
    for row in range(floor - rise, floor, LADDER if wings else 3):
        for column in side:
            world.t[row][column] = nook.theme["ledge"]
    cells = nook.walk(wide, h=5, rough=False, square=True)
    world.mark("relic", cells[-2][0], cells[-2][1] - 1)
    world.put(cells[-3][0], cells[-3][1] - 1, "^")
    locked.update(range(a, a + 4))


# What a region's road is made of between its set pieces: the height of its tunnels, of
# its halls (from, to), its liquid, and how often each kind of piece comes (`weights`).
KITS = {
    "earth": dict(h=6, hall=(8, 11), liquid="W", weights=dict(walk=4, hall=3, pool=1, rise=1)),
    "limbo": dict(h=6, hall=(9, 13), liquid="~", weights=dict(walk=3, hall=5, pool=1, rise=1, gate=1)),
    "lust": dict(h=6, hall=(11, 15), liquid=None, weights=dict(walk=3, hall=3, rise=3, climb=4, gate=1)),
    "gluttony": dict(h=4, hall=(7, 9), liquid="&", weights=dict(walk=4, hall=2, pool=5, rise=1, climb=1, gate=1)),
    "greed": dict(h=6, hall=(10, 13), liquid=None, flat=True, weights=dict(walk=3, hall=5, rise=2, climb=1, gate=2)),
    "wrath": dict(h=6, hall=(8, 11), liquid="~", weights=dict(walk=2, hall=2, pool=6, siphon=2, climb=1, gate=1)),
    "heresy": dict(h=5, hall=(8, 11), liquid="L", weights=dict(walk=3, hall=4, pool=3, rise=3, chasm=2, gate=1)),
    "violence": dict(h=6, hall=(10, 14), liquid="!", weights=dict(walk=3, hall=3, pool=3, rise=1, climb=2, slab=2, chasm=1)),
    "fraud": dict(h=6, hall=(8, 10), liquid="%", weights=dict(walk=2, hall=1, pool=5, bridge=3, gate=3, climb=2, slab=1, chasm=2)),
    "treachery": dict(h=6, hall=(11, 15), liquid="W", floor="I", weights=dict(walk=3, hall=6, pool=2, gate=1, climb=1, slab=1)),
}
HURTS = "L!%"


def ramble(road: Path, x_end: int, floor_end: int, have: tuple = (), kit: str | None = None):
    """Lays a stretch of a region's road from where the road is to a column and a floor,
    out of the pieces the region is made of, picked by a fixed dice: tunnels and halls,
    its liquid to cross or swim, a climb and a way down again, and the gates of the
    abilities the hero has by then (`have`). The floor wanders a few rows round the line
    from its start to its end, so the next set piece is where the layout says it is."""
    region = kit or road.theme["key"]
    k = KITS[region]
    dice = random.Random(f"{region}|{road.x}|{road.floor}")
    x_start, floor_start = road.x, road.floor
    n = abs(x_end - x_start)
    last = ""
    grip, dash, bolt, glide, smite = (name in have for name in ("WallGrip", "LightDash", "HolyLight", "Glide", "Smite"))
    while True:
        left = abs(x_end - road.x)
        gap = floor_end - road.floor
        if left < 34:
            break
        want = floor_start + (floor_end - floor_start) * (1 - left / n)
        drift = want - road.floor  # over zero: the road is higher than it should be by now
        closing = left < 70
        if drift >= 8 or (closing and gap >= 7):
            # Down to the line: a shaft of ledges, or (for a hero who can climb back) a clean drop.
            down = int(max(5, min(12, round(max(drift, gap if closing else 0)))))
            road.walk(3, h=k["h"])
            (road.smooth if grip and dice.random() < 0.5 else road.shaft)(down)
            last = "down"
            continue
        if drift <= -6 or (closing and gap <= -7):
            up = int(max(6, min(9, round(max(-drift, -gap if closing else 0)))))
            road.walk(3, h=k["h"])
            (road.smooth if grip and dice.random() < 0.5 else road.shaft)(-up)
            last = "up"
            continue
        fits = {
            "walk": True,
            "hall": left >= 62,
            "pool": k["liquid"] is not None and left >= 60,
            "bridge": k["liquid"] is not None and left >= 60,
            "siphon": left >= 60,
            "rise": not closing and drift >= 0,
            "climb": grip and not closing and drift >= 0,
            "gate": dash or bolt,
            "slab": smite and not closing and drift >= 3,
            "chasm": glide and k["liquid"] is not None and k["liquid"] in HURTS and left >= 64,
        }
        names = [name for name, weight in k["weights"].items() if fits[name] and name != last for _ in range(weight)]
        piece = dice.choice(names or ["walk"])
        last = piece
        if piece == "walk":
            road.walk(dice.randint(6, 11), int(max(-2, min(2, round(drift / 2)))), h=k["h"], char=None)
        elif piece == "hall":
            wide = dice.randint(14, 24)
            tall = dice.randint(*k["hall"])
            cells = road.hall(wide, tall, flat=k.get("flat", False), char=k.get("floor"))
            if tall >= 11 and wide >= 18 and dice.random() < 0.5:
                road.perch(min(c[0] for c in cells) + wide // 2 - 2, road.floor - 3)
        elif piece in ("pool", "bridge"):
            liquid = k["liquid"]
            road.walk(2, h=k["h"])
            if piece == "bridge":
                road.pool(13, liquid, 3, h=k["h"] + 1, bridge=True)
            elif liquid in HURTS:
                road.pool(dice.choice([8, 13]), liquid, 3, h=k["h"] + 1)
            else:
                road.pool(dice.randint(9, 18), liquid, dice.randint(3, 5), h=k["h"] + 1)
            road.walk(2, h=k["h"])
        elif piece == "siphon":
            road.walk(3)
            road.siphon(12)
            road.walk(3)
        elif piece == "rise":
            road.walk(3, h=k["h"])
            road.shaft(-6)
        elif piece == "climb":
            road.walk(3, h=k["h"])
            road.smooth(-dice.randint(7, 8))
        elif piece == "gate":
            kinds = (["barrier"] if dash else []) + (["seal"] if bolt else [])
            road.walk(2, h=k["h"])
            road.gate(dice.choice(kinds))
        elif piece == "slab":
            road.slab(9)
            road.walk(3, h=k["h"])
        elif piece == "chasm":
            road.walk(3, h=k["h"])
            road.chasm(11, k["liquid"])
            road.walk(3, h=k["h"])
    road.reach(x_end, floor_end, h=k["h"])


def stroll(side: Path, region: str, gate: str | None = None, relic: bool = True, wet: bool = False):
    """Lays a low road between its hatches: level, with a room for a relic or a pool to
    cross, and a gate at its west end if it is one to come back for."""
    k = KITS[region]
    left = side.span
    side.walk(3, rough=False)
    left -= 6  # and three at the far end
    if gate and left >= 17:
        side.gate(gate)
        left -= 5
    liquid = k["liquid"]
    if wet and liquid and left >= 21:
        wide = 13 if liquid in HURTS else min(left - 8, 16)
        cells = side.pool(wide, liquid, 3 if liquid in HURTS else 4, h=5)
        left -= wide
        if relic and liquid not in HURTS:
            side.relic(cells[len(cells) // 2][0], cells[len(cells) // 2][1] + 3)
            relic = False
    if left >= 12:
        wide = min(left, 22)
        cells = side.hall(wide, 7, flat=k.get("flat", False))
        left -= wide
        if relic:
            side.relic(cells[len(cells) // 2][0], cells[len(cells) // 2][1] - 1)
            relic = False
    if left > 0:
        cells = side.walk(left, rough=False)
        if relic:
            side.relic(cells[len(cells) // 2][0], cells[len(cells) // 2][1] - 1)
    side.walk(3, rough=False)


def side_roads(world: World, log=print):
    """Gives every circle its side roads, where there is room for them: low roads under
    its plain stretches (one with a relic in a room, one flooded, one behind a gate to
    come back for) and attics over its tunnels. The places are searched for, not written
    down: a side road that would cut into anything is tried somewhere else."""
    later = {"limbo": "seal", "lust": "seal", "gluttony": "seal", "earth": "barrier"}
    for region, road, trail, locked in world.roads:
        if region not in KITS or len(trail) < 80:
            continue
        columns = sorted(trail)
        lo, hi = columns[0] + 8, columns[-1] - 12
        lows = attics = 0
        wanted = max(3, (hi - lo) // 58)
        for i in range(wanted):
            centre = lo + (i + 0.5) * (hi - lo) / wanted
            kind = i % 3
            lay = lambda side, kind=kind, region=region: stroll(  # noqa: E731
                side, region, gate=later.get(region, "barrier") if kind == 2 else None, wet=kind == 1
            )
            for length, shift in [(l, s) for l in (92, 76, 64, 52, 40, 30) for s in (0, 9, -9, 18, -18, 27, -27)]:
                try:
                    low_road(road, trail, locked, int(centre - length / 2 + shift), int(centre + length / 2 + shift), lay=lay)
                    lows += 1
                    break
                except Clash:
                    continue
        nooks = max(2, wanted // 3)
        for i in range(nooks):
            centre = lo + (i + 0.25) * (hi - lo) / nooks
            for shift, east in [(s, e) for s in (0, 7, -7, 14, -14, 21, -21, 28, -28) for e in (True, False)]:
                try:
                    attic(road, trail, locked, int(centre + shift), wings=i % 2 == 0, east=east)
                    attics += 1
                    break
                except Clash:
                    continue
        log(f"  {region}: {lows} low roads, {attics} attics")


def descend_west(road: Path, dy: int, region: str, kind: str = "shaft", lower_char: str | None = None) -> Path:
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
    upper.antechamber()
    following = Path(world, east, top + dy, 1, road).begin(region)
    lower = Path(world, edge_low, top + dy, -1, road)
    lower.root = following  # it is the next circle's room: nothing of that circle is dug under it
    lower.antechamber(lower_char)
    return following


# --- The regions -------------------------------------------------------------------


class Course:
    """Where a circle's road is along its way: `at(t)` is the column and `floor(t)` the
    floor a share t of the way from its start (0) to its end (1)."""

    def __init__(self, road: Path, x_end: int, floor_end: int):
        self.road = road
        self.x0, self.x1 = road.x, x_end
        self.f0, self.f1 = road.floor, floor_end

    def at(self, t: float) -> int:
        return int(round(self.x0 + (self.x1 - self.x0) * t))

    def floor(self, t: float) -> int:
        return int(round(self.f0 + (self.f1 - self.f0) * t))

    def ramble(self, t: float, have: tuple = (), lift: int = 0):
        """Rambles on to a share of the way; `lift` rows over (below zero) or under the line."""
        ramble(self.road, self.at(t), self.floor(t) + lift, have)


def earth(world: World) -> Path:
    surface = world.surface
    start = (POND[0] + POND[1]) // 2 - 28
    world.spawn = (start, surface[start] - 1)
    world.view("earth", start + 2, GROUND_LINE - 8)
    # Ruins by the start: what is left of a colonnade.
    for dx, tall in ((-14, 3), (-13, 1), (-9, 2), (8, 3), (9, 1), (13, 1)):
        x = start + dx
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
    mouth = EAST - 70
    crossroads = GROUND_LINE + 22
    road = Path(world, mouth, surface[mouth], -1).begin("earth")
    road.walk(34, crossroads - surface[mouth], h=5)
    road.hall(16, 9, name="earth_crossroads")
    road.rest()
    # The arch over the mouth, of the City's own brick, on the wall behind: the first sight of what lies under.
    crown = min(surface[x] for x in range(mouth - 12, mouth + 7)) - 9
    for x in (mouth + 4, mouth + 5, mouth - 10, mouth - 11):
        for y in range(crown, surface[x] + 6):
            world.walls[y][x] = "b"
    for x in range(mouth - 12, mouth + 7):
        for y in (crown - 2, crown - 1):
            world.walls[y][x] = "b"
    world.put(mouth + 3, surface[mouth + 3] - 1, "*")

    # West from the crossroads: the old mine, a long way under the meadows, and at its end
    # the antechamber under the Pit's head.
    home = PIT_ROWS[0] + 3 * LADDER
    mine = Path(world, road.x - 6, crossroads, -1, road)
    mine.walk(18, 8)
    way = Course(mine, WEST - 4, home)
    ramble(mine, way.at(0.3), way.floor(0.35), kit="earth")
    cells = mine.hall(22, 9, 2, name="earth_mine")
    mine.relic(cells[11][0], cells[11][1] - 1)
    mine.rest()
    ramble(mine, way.at(0.62), way.floor(0.65), kit="earth")
    mine.walk(3)
    mine.pool(17, "W", 4)
    mine.walk(3)
    ramble(mine, WEST + 2, home - 6, kit="earth")
    mine.walk(6, 6)
    mine.antechamber(name="earth_antechamber")

    # Down from the crossroads and east under the way in, to the hall of the Gate.
    road.shaft(15, back=True)
    ramble(road, EAST - 21, GATE_HALL, kit="earth")
    cells = road.hall(21, 13, name="earth_gate")
    road.rest()
    x0 = min(c[0] for c in cells)
    for x in (x0 + 5, x0 + 6, x0 + 7, x0 + 15, x0 + 16, x0 + 17):
        for y in range(road.floor - 9, road.floor):
            world.walls[y][x] = "b"
    for x in range(x0 + 4, x0 + 19):
        for y in (road.floor - 11, road.floor - 10):
            world.walls[y][x] = "b"
    world.put(x0 + 9, road.floor - 1, "*")
    world.put(x0 + 13, road.floor - 1, "*")
    road.shaft(FLOORS["limbo"][0] - road.floor, back=True)
    return road


def limbo(road: Path) -> Path:
    road.begin("limbo")
    way = Course(road, WEST, FLOORS["limbo"][1])
    road.hall(16, 10, name="limbo_gate")
    road.rest()
    way.ramble(0.2)
    road.walk(4)
    road.pool(13, "~", 4, stones=True)
    road.walk(4)
    way.ramble(0.36)
    cells = road.hall(30, 12, 2, name="limbo")
    road.perch(cells[15][0] - 2, cells[15][1] - 4, relic=True)
    road.rest()
    way.ramble(0.55, lift=-4)
    road.walk(8, 2)
    road.ledge(5)
    road.shrine("LightDash")
    road.gate("barrier")
    road.walk(8, 2)
    road.gate("barrier")
    road.rest()
    have = ("LightDash",)
    way.ramble(0.8, have)
    road.hall(24, 10, name="limbo_fields")
    road.rest()
    way.ramble(0.965, have)
    road.gate("barrier")
    road.reach(WEST, way.f1)
    return descend_west(road, FLOORS["lust"][0] - way.f1, "lust", "smooth")


def lust(road: Path) -> Path:
    have = ("LightDash",)
    way = Course(road, EAST, FLOORS["lust"][1])
    road.walk(8)
    cells = road.hall(30, 16, name="lust")
    x0 = min(c[0] for c in cells)
    road.perch(x0 + 5, road.floor - 3)
    road.perch(x0 + 12, road.floor - 6)
    road.perch(x0 + 19, road.floor - 9, relic=True)
    way.ramble(0.22, have)
    road.walk(8, 2)
    road.ledge(6)
    road.shrine("WallGrip")
    road.smooth(-12)
    road.walk(8)
    road.smooth(-7)
    road.walk(6)
    road.smooth(way.floor(0.34) + 4 - road.floor)
    road.walk(4)
    road.rest()
    have += ("WallGrip",)
    way.ramble(0.54, have)
    road.hall(26, 14, name="lust_hollow")
    road.rest()
    way.ramble(0.8, have)
    cells = road.hall(24, 13, name="lust_spire")
    road.perch(min(c[0] for c in cells) + 10, road.floor - 3, relic=True)
    way.ramble(0.965, have)
    road.rest()
    road.reach(EAST, way.f1)
    road.shaft(FLOORS["gluttony"][0] - way.f1, back=True)
    return road


def gluttony(road: Path) -> Path:
    have = ("LightDash", "WallGrip")
    road.begin("gluttony")
    way = Course(road, WEST, FLOORS["gluttony"][1])
    road.hall(16, 8)
    road.rest()
    way.ramble(0.2, have)
    road.walk(3, h=4)
    cells = road.pool(23, "&", 6)
    road.relic(cells[11][0], cells[11][1] + 5)
    road.walk(3, h=4)
    way.ramble(0.45, have)
    road.hall(28, 9, name="gluttony")
    road.rest()
    road.gate("barrier")
    way.ramble(0.76, have)
    road.hall(22, 8, name="gluttony_mire")
    road.rest()
    way.ramble(0.965, have)
    road.reach(WEST, way.f1)
    return descend_west(road, FLOORS["greed"][0] - way.f1, "greed")


def greed(road: Path) -> Path:
    have = ("LightDash", "WallGrip")
    way = Course(road, EAST, FLOORS["greed"][1])
    road.walk(8)
    cells = road.hall(28, 12, flat=True, name="greed")
    x0 = min(c[0] for c in cells)
    road.perch(x0 + 3, road.floor - 3, 5)
    road.perch(x0 + 12, road.floor - 6, 5, relic=True)
    road.perch(x0 + 20, road.floor - 3, 5)
    way.ramble(0.24, have, lift=-4)
    road.walk(8, 2)
    road.ledge(5)
    road.shrine("HolyLight")
    road.gate("seal")
    road.walk(8, 2)
    road.gate("seal")
    road.rest()
    have += ("HolyLight",)
    way.ramble(0.55, have)
    road.hall(30, 12, flat=True, name="greed_vault")
    road.rest()
    way.ramble(0.86, have)
    road.hall(22, 11, flat=True, name="greed_treasury")
    road.gate("seal")
    way.ramble(0.965, have)
    road.rest()
    road.reach(EAST, way.f1)
    road.shaft(FLOORS["wrath"][0] - way.f1, back=True)
    return road


def wrath(road: Path) -> Path:
    have = ("LightDash", "WallGrip", "HolyLight")
    road.begin("wrath")
    way = Course(road, WEST, FLOORS["wrath"][1])
    road.hall(16, 8)
    road.rest()
    way.ramble(0.16, have)
    road.walk(3)
    road.pool(34, "~", 6, h=9, wall=False)
    road.walk(4)
    road.siphon(12)
    road.walk(4)
    way.ramble(0.45, have)
    road.walk(4)
    cells = road.pool(27, "~", 4, h=11, wall=False)
    road.w.view("wrath", cells[13][0], cells[13][1] - 5)
    road.walk(4)
    road.rest()
    road.gate("seal")
    way.ramble(0.76, have)
    road.hall(24, 10, name="wrath_shore")
    road.rest()
    way.ramble(0.965, have)
    road.reach(WEST, way.f1)
    return descend_west(road, FLOORS["heresy"][0] - way.f1, "heresy")


def heresy(road: Path) -> Path:
    have = ("LightDash", "WallGrip", "HolyLight")
    way = Course(road, EAST, FLOORS["heresy"][1])
    road.walk(8, h=5)
    road.pool(13, "L", 3)
    road.hall(28, 9, name="heresy")
    road.rest()
    way.ramble(0.27, have)
    road.shaft(-9)
    road.walk(8)
    road.shrine("Glide")
    road.chasm(11, "L")
    road.walk(6)
    road.chasm(11, "L")
    road.w.view("heresy_chasm", road.x - 14, road.floor - 5)
    road.walk(3)
    road.smooth(way.floor(0.41) + 2 - road.floor)
    road.walk(4)
    road.rest()
    have += ("Glide",)
    way.ramble(0.64, have)
    road.hall(26, 11, name="heresy_city")
    road.rest()
    way.ramble(0.93, have)
    road.gate("barrier")
    road.walk(3)
    road.rest()
    road.reach(EAST, way.f1)
    road.shaft(FLOORS["violence"][0] - way.f1, back=True)
    return road


def violence(road: Path) -> Path:
    have = ("LightDash", "WallGrip", "HolyLight", "Glide")
    road.begin("violence")
    way = Course(road, WEST, FLOORS["violence"][1])
    road.hall(16, 9)
    road.rest()
    road.walk(2)
    road.pool(18, "!", 3)
    road.walk(4)
    way.ramble(0.22, have)
    cells = road.hall(30, 13, name="violence_wood")
    x0 = min(c[0] for c in cells)
    road.perch(x0 + 6, road.floor - 3)
    road.perch(x0 + 13, road.floor - 6, relic=True)
    road.perch(x0 + 20, road.floor - 3)
    road.rest()
    way.ramble(0.43, have, lift=-6)
    road.walk(3)
    road.smooth(-8)
    road.walk(6)
    road.ledge(5)
    road.shrine("Smite")
    road.slab(10)
    road.walk(4)
    road.hall(28, 10, char="E", name="violence")
    road.rest()
    road.slab(9)
    road.walk(3)
    have += ("Smite",)
    way.ramble(0.8, have)
    road.hall(24, 12, name="violence_shore")
    road.rest()
    way.ramble(0.965, have)
    road.reach(WEST, way.f1)
    return descend_west(road, FLOORS["fraud"][0] - way.f1, "fraud", "slab")


def fraud(road: Path) -> Path:
    have = ("LightDash", "WallGrip", "HolyLight", "Glide", "Smite")
    way = Course(road, EAST, FLOORS["fraud"][1])
    road.walk(7)
    way.ramble(0.16, have)
    road.walk(3)
    road.pool(13, "%", 3, bridge=True)
    road.w.view("fraud", road.x - 8, road.floor - 5)
    road.walk(4)
    way.ramble(0.42, have)
    road.hall(22, 10, name="fraud_ledge")
    road.rest()
    way.ramble(0.72, have)
    road.hall(20, 9, name="fraud_wells")
    road.rest()
    way.ramble(0.965, have)
    road.rest()
    road.reach(EAST, way.f1)
    road.smooth(FLOORS["treachery"][0] - way.f1, back=True)
    return road


def treachery(road: Path) -> Path:
    have = ("LightDash", "WallGrip", "HolyLight", "Glide", "Smite")
    road.begin("treachery")
    end = FLOORS["treachery"][1]
    way = Course(road, WEST + 3, end - 12)
    road.hall(16, 9)
    road.rest()
    way.ramble(0.14, have)
    road.hall(40, 15, char="I", name="treachery")
    road.walk(2)
    road.pool(13, "W", 5)
    road.walk(4)
    way.ramble(0.42, have)
    road.gate("barrier")
    road.hall(34, 13, char="I", name="treachery_lake")
    road.rest()
    road.gate("seal")
    way.ramble(0.93, have)
    road.hall(20, 12, char="I", name="treachery_deep")
    road.rest()
    road.reach(WEST + 19, end - 12)
    road.slab(9)
    road.walk(6, 3)
    road.walk(6)
    road.shrine("Wings")
    room = Path(road.w, road.x, road.floor, -1, road)
    room.antechamber("I", name="treachery_end")
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
    world.view("pit", 12, (top + floor) // 2)


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
    """Islands of cloud over the Earth, climbed from its surface on ledges only the Wings
    reach: a stair, then east from island to island, each a ladder or a glide from the
    last, up to the temple and the gate at the end of everything."""
    surface = world.surface
    ledge = THEMES["paradise"][2]

    def step(x: int, row: int, wide: int = 4):
        for i in range(wide):
            world.t[row][x + i] = ledge

    def colonnade(x0: int, x1: int, top: int, every: int = 6):
        for x in range(x0, x1 + 1, every):
            for y in range(top - 7, top):
                world.walls[y][x] = "m"
        for x in range(x0 - 1, x1 + 2):
            world.walls[top - 8][x] = "m"

    # The stair from the Earth.
    foot = min(surface[x] for x in range(106, 118))
    step(113, foot - 6)
    step(108, foot - 12)
    step(113, foot - 18)
    step(108, foot - 24)
    first = foot - 30
    island(world, 115, 150, first, 5)
    world.mark("checkpoint", 132, first - 1)
    world.put(133, first - 1, "J")
    world.view("paradise_stair", 128, first + 8)
    # Clouds out west of the stair's head, with something on the far one.
    island(world, 98, 103, first + 3, 2)
    island(world, 74, 92, first, 4)
    world.mark("relic", 82, first - 1)
    world.put(87, first - 1, "J")
    # East and up, to the garden.
    step(153, first - 6)
    garden = first - 12
    island(world, 158, 200, garden, 7, "G")
    for x in range(165, 194):
        world.t[garden + 1][x] = "D"
    world.put(162, garden - 1, "J")
    world.put(196, garden - 1, "J")
    world.view("paradise_garden", 179, garden - 5)
    # A high cloud over the garden, with something on it.
    step(176, garden - 6)
    step(176, garden - 12)
    island(world, 181, 196, garden - 18, 3)
    world.mark("relic", 189, garden - 19)
    # Over a gap that is glided, to the colonnade.
    island(world, 210, 244, garden, 5, "M")
    colonnade(214, 238, garden)
    world.mark("checkpoint", 226, garden - 1)
    world.put(227, garden - 1, "J")
    # Up to the orchard.
    step(240, garden - 6)
    orchard = garden - 12
    island(world, 246, 294, orchard, 7, "G")
    for x in range(258, 288):
        world.t[orchard + 1][x] = "D"
    world.put(254, orchard - 1, "J")
    world.put(290, orchard - 1, "J")
    world.view("paradise_orchard", 272, orchard - 5)
    # Another glide, to a bare cloud, and under it a small one with something on it.
    island(world, 304, 336, orchard, 5)
    world.put(320, orchard - 1, "J")
    island(world, 293, 301, orchard + 7, 3)
    world.mark("relic", 298, orchard + 6)
    # Up to the forecourt.
    step(332, orchard - 6)
    court = orchard - 12
    island(world, 338, 372, court, 6, "M")
    colonnade(342, 366, court)
    world.mark("checkpoint", 354, court - 1)
    world.put(355, court - 1, "J")
    # And up to the temple, and the gate at the end of everything.
    step(368, court - 6)
    temple = court - 12
    island(world, 374, 440, temple, 8, "M")
    for x in (384, 385, 392, 393, 400, 401, 410, 411, 418, 419, 426, 427):
        for y in range(temple - 9, temple):
            world.walls[y][x] = "m"
    for row, inset in ((temple - 10, 0), (temple - 11, 5), (temple - 12, 10), (temple - 13, 15), (temple - 14, 20)):
        for x in range(382 + inset, 430 - inset):
            world.t[row][x] = "M"
            world.square.add((x, row))
    for x in (388, 396, 414, 422):
        world.put(x, temple - 1, "J")
    world.mark("end", 405, temple - 1)
    world.view("paradise", 405, temple - 6)
    # A last cloud past the temple, glided to.
    island(world, 450, 468, temple + 4, 3)
    world.mark("relic", 459, temple + 3)
    world.put(463, temple + 3, "J")


def build(log=print) -> World:
    world = World()
    road = earth(world)
    for circle in (limbo, lust, gluttony, greed, wrath, heresy, violence, fraud, treachery):
        road = circle(road)
    pit(world)
    paradise(world)
    side_roads(world, log)
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
