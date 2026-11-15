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
import itertools
import math
import os
import random
import sys

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_reach
import tv2_earth_map

WORLD_MAP = os.path.join(OUT, "world_map.txt")
LIFT = 64  # rows of sky over the world's first layout: room for a Paradise that is a place and not a stair
WIDTH, HEIGHT = 540, 780 + LIFT
AIR = "."
GROUND_LINE = 96 + LIFT  # the row the Earth's surface lies about
SURFACE_LINE = 104 + LIFT  # the row the rock behind the caves starts at: under every dip of the surface
PIT = (6, 18)  # the Pit's columns: first, one past the last
PIT_LEDGE = 14  # its ledges run from this column to its east side
PIT_ROWS = (132 + LIFT, 738 + LIFT)  # its top, its floor
POND = (288, 306)  # the columns of the meadow the Earth's pond lies in
WEST = 61  # the column the ways down at the world's west side start at, going west
EAST = WIDTH - 24  # the column the ways down at its east side start at, going east
WELL = (EAST + 11, EAST + 19)  # the Well's columns, past the east ends of the circles: first, one past the last
DOOR_X = (38, 41)  # the columns of an antechamber's ladder to the Pit
LADDER = 6  # rows between the ledges only the Wings climb

# The regions, top to bottom: (key, first row). What each is built of: its ground, the
# wall behind its tunnels, its ledges, the lamp on a tunnel's wall, the lamp standing
# in a hall, the columns between lamps, whether its halls have a wall behind them and
# whether it is built (square, no slopes) rather than grown.
BANDS = [
    ("paradise", 0),
    ("earth", 76),
    ("limbo", 172),
    ("lust", 276),
    ("gluttony", 332),
    ("greed", 382),
    ("wrath", 440),
    ("heresy", 492),
    ("violence", 600),
    ("fraud", 656),
    ("treachery", 706),
]
BANDS = [(key, top + LIFT if top else 0) for key, top in BANDS]
# The floor each pass of the road starts on and ends on. A pass runs from one side of the
# world to the other; Limbo and Heresy, the great circles, are two passes each, one under
# the other. A west end is a multiple of six, as the Pit's ledges are.
FLOORS = {
    "limbo": (186, 204),
    "limbo_low": (240, 258),
    "lust": (292, 312),
    "gluttony": (348, 364),
    "greed": (398, 420),
    "wrath": (456, 474),
    "heresy": (508, 528),
    "heresy_low": (564, 582),
    "violence": (616, 636),
    "fraud": (672, 688),
    "treachery": (722, 744),
}
FLOORS = {key: (a + LIFT, b + LIFT) for key, (a, b) in FLOORS.items()}
GATE_HALL = 150 + LIFT  # the floor of the hall of the Gate, on the Earth
# Parts of a circle that are of another ground than the rest of it, so a circle is
# several places and not one: (first column, column after the last, first row, row after
# the last, the ground). Limbo's lower pass runs into the marble of the castle; the
# wood and the burning sand of Violence; the ice of the lake itself.
ZONES = {
    "limbo": [(300, WIDTH, 226 + LIFT, 276 + LIFT, "M")],
    "gluttony": [(200, 360, 0, HEIGHT, "D")],
    "wrath": [(360, WIDTH, 0, HEIGHT, "U")],
    "heresy": [(0, 190, 492 + LIFT, 546 + LIFT, "H")],
    "violence": [(190, 360, 0, HEIGHT, "D"), (0, 190, 0, HEIGHT, "E")],
    "treachery": [(0, 270, 0, HEIGHT, "I")],
}
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
LAMPS = {theme[i] for theme in THEMES.values() for i in (3, 4)}
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
        self.roads: list = []  # every pass of the road: (region, the road, its trail, its locked columns)
        self.well_doors: list = []  # the floor of every door into the Well
        self.pit_doors: list = []  # the floor of every tunnel into the Pit, and the region it comes from
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
            # The Earth's: a bed of clay a few rows under the turf, where the field says so.
            if key == "earth" and 3 <= depth <= 4 and field(55, x, 0, 9.0) > 0.15:
                return "C"
            return "D" if depth < 6 + int(round(2 * wave(31, x, 7.0))) else "S"
        edge = x + int(round(5 * wave(77, y, 8.0)))
        for x0, x1, y0, y1, char in ZONES.get(key, ()):
            if x0 <= edge < x1 and y0 <= y < y1:
                return char
        return THEMES[key][0]

    def fill(self):
        for x in range(WIDTH):
            rise = 5.6 * wave(11, x, 30.0) + 2.2 * wave(12, x, 9.0)
            self.surface[x] = max(GROUND_LINE - 8, min(GROUND_LINE + 4, int(round(GROUND_LINE - 1 + rise))))
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
        """A column of a passage: `tall` cells of air standing on a floor two cells thick.
        Its roof never breaks into a space dug over it: two cells of ground stay between,
        so a road that comes back under itself does not take the floor from over its head."""
        for y in range(floor - 1, floor - tall - 1, -1):
            if y < floor - 3 and self.t[y][x] in GROUND and (self.owner[y - 1][x] or self.owner[y - 2][x]):
                break
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
            "earth": [("O", "S", 260, 13), ("C", "DS", 90, 14)],
            "limbo": [("S", "H", 80, 16)],
            "greed": [("$", "N", 480, 8)],
            "treachery": [("I", "Z", 60, 14)],
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
        if not self.lamps or self.since < self.theme["gap"] * getattr(self, "sparse", 1):
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
                # A roof that swells and hangs, and a floor that rises a step here and there.
                tall += max(0, int(round(1.5 + 1.7 * wave(self.owner * 7 + 1, x, 6.0) + 0.9 * wave(self.owner * 7 + 4, x, 2.4))))
                if dy == 0 and n >= 9 and 2 <= i < n - 2 and wave(self.owner * 7 + 6, x, 4.0) > 0.42:
                    floor -= 1
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
        if name in SHADES:
            self.w.mark("sign", cells[3][0], cells[3][1] - 1, "shade", *SHADES[name].split())
        self.x += self.d * n
        self.floor += dy
        return cells

    def cavern(self, n: int, h: int = 28, char: str | None = None, name: str | None = None):
        """A great cave of n columns, `h` rows high at its middle, the rock behind the caves
        showing all through it: the road crosses its floor, and two stairs of ledges go up
        toward its roof, with something at the top of one for whoever climbs it."""
        world = self.w
        cells, roof = [], {}
        for i in range(n):
            x = self.x + self.d * i
            arch = math.sin(math.pi * (i + 0.5) / n) ** 0.45
            tall = max(7, int(round(h * (0.3 + 0.7 * arch) + 2.0 * wave(self.owner * 7 + 5, x, 6.0))))
            world.column(x, self.floor, tall, self.owner, False, char)
            self.lamp(x, self.floor, False)
            self.root.trail[x] = self.floor
            cells.append((x, self.floor))
            roof[x] = tall
        x0 = min(roof)
        for k, share in enumerate((0.28, 0.66)):
            base = x0 + int(n * share)
            row, step, top = self.floor - 3, 0, None
            while True:
                columns = range(base + (6 if step % 2 else 0), base + (6 if step % 2 else 0) + 4)
                if any(c not in roof or self.floor - roof[c] > row - 5 for c in columns):
                    break
                for c in columns:
                    world.t[row][c] = self.theme["ledge"]
                top = (columns[1], row - 1)
                row -= 3
                step += 1
            if top and k == 0:
                self.relic(*top)
            elif top:
                world.put(top[0], top[1], self.theme["floor_lamp"])
        self.last_roof = roof
        if name:
            world.view(name, self.x + self.d * (n - 1) / 2 + 0.5, self.floor - 9)
        if name in TABLETS:
            world.mark("sign", cells[n // 2][0], self.floor - 1, "tablet", *TABLETS[name].split())
        self.x += self.d * n
        return cells

    def ledge(self, dy: int):
        """The floor drops `dy` rows in one step: down is easy, back up is not."""
        self.floor += dy
        self.raised = (3, dy)

    def pool(self, n: int, liquid: str, depth: int = 3, h: int = 7, stones=None, wall: bool = True, bridge: bool = False):
        """The floor gives way to a liquid for n columns. One that hurts is crossed on
        stones two tiles wide, three apart (or on a bridge of ledges); the others are swum.
        `stones` may be "crumble" or "blink": then what is crossed on is the game's own
        platforms, a tile over the liquid, which give way under the hero or come and go."""
        if stones is None:
            stones = liquid in "L!%" and not bridge
        # Into a liquid that is swum, sometimes, a fall of it from the roof.
        fall = n // 2 if liquid not in "L!%" and not bridge and stones is False and n >= 12 and hash01(self.x, self.floor, 71) < 0.55 else -1
        cells = []
        for i in range(n):
            x = self.x + self.d * i
            # "mixed": a stone, a platform that gives way, one that comes and goes, by turns.
            kind = (True, "crumble", "blink")[(i // 5) % 3] if stones == "mixed" else stones
            stone = kind is True and i % 5 in (3, 4)
            if isinstance(kind, str) and i % 5 == 3 and i + 1 < n:
                left = min(x, x + self.d)
                self.w.mark("platform", left, self.floor - 1, 2, kind, round(0.35 * (i // 5), 2))
                self.w.gates.update(((left, self.floor - 1), (left + 1, self.floor - 1)))
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
            if i == fall:
                for y in range(self.floor - tall, self.floor):
                    self.w.t[y][x] = liquid
            elif wall or stone:
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
        for side in (-2, 2):
            self.w.put(x + side, self.floor - 3, self.theme["wall_lamp"])

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
            self.w.column(x, self.floor, tall, self.owner, i in (0, 10))
            self.w.square.update((x, y) for y in range(self.floor - 9, self.floor + 2))
            cells.append(x)
        for x in cells[4:7]:
            self.w.solid(x, self.floor - 1, force=True)
        self.w.mark("ability", cells[5], self.floor - 2, ability)
        # Behind the plinth, on the wall, the sign of the ability, as large as the room.
        for row, line in enumerate(EMBLEMS[ability]):
            for column, c in enumerate(line if self.d > 0 or ability != "LightDash" else line[::-1]):
                if c == "#":
                    raise_wall(self.w, self.theme["wall"], min(cells) + 2 + column, self.floor - 8 + row)
        self.w.mark("sign", cells[1], self.floor - 1, "tablet", *SHRINE_WORDS[ability].split())
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
        self.w.mark("checkpoint", first - 6, floor - 1, "pit")
        self.w.mark("sign", first - 10, floor - 1, "post", *"The Pit is over this room. Its ladder is for the winged; rest here, and it will carry you between the rooms you have rested in.".split())
        self.w.pit_doors.append((top, self.theme["key"]))
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
    firm(world, range(a - 1, a + 5), range(trail[a] + 2, low + 11))
    firm(world, range(b - 1, b + 5), range(trail[b] + 2, low + 11))
    firm(world, range(a + 4, b), range(low - 10, low + 11))
    side = Path(world, a + 4, low, 1, road)
    side.root = side  # its own trail: what is dug under it is dug under it, not under the road above
    side.span = b - (a + 4)
    side.hatch = a
    # A side road is dim: a lamp is seen a long way, so it has few of them, and between them
    # only the light of its region's air. The Earth's air has none, so there the lamps stay nearer.
    side.sparse = 4.5 if low > dict(BANDS)["limbo"] else 2.4
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
    nook.root = nook
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
    return nook, cells


# What a region's road is made of between its set pieces: the height of its tunnels, of
# its halls (from, to), its liquid, and how often each kind of piece comes (`weights`).
KITS = {
    "earth": dict(h=6, hall=(8, 11), liquid="W", weights=dict(walk=4, hall=3, pool=1, rise=1)),
    "limbo": dict(h=6, hall=(9, 13), liquid="~", weights=dict(walk=3, hall=5, pool=1, rise=1)),
    "lust": dict(h=6, hall=(11, 15), liquid=None, weights=dict(walk=3, hall=3, rise=3, climb=4)),
    "gluttony": dict(h=4, hall=(7, 9), liquid="&", weights=dict(walk=4, hall=2, pool=5, rise=1, climb=1)),
    "greed": dict(h=6, hall=(10, 13), liquid=None, flat=True, weights=dict(walk=3, hall=5, rise=2, climb=1, gate=1)),
    "wrath": dict(h=6, hall=(8, 11), liquid="~", weights=dict(walk=2, hall=2, pool=6, siphon=2, climb=1)),
    "heresy": dict(h=5, hall=(8, 11), liquid="L", platforms="crumble", weights=dict(walk=3, hall=4, pool=3, rise=3, chasm=2, gauntlet=2)),
    "violence": dict(h=6, hall=(10, 14), liquid="!", weights=dict(walk=3, hall=3, pool=3, rise=1, climb=2, slab=1, chasm=1, gauntlet=2)),
    "fraud": dict(h=6, hall=(8, 10), liquid="%", platforms="blink", weights=dict(walk=2, hall=1, pool=5, bridge=3, gate=1, climb=2, slab=1, chasm=2, gauntlet=2)),
    "treachery": dict(h=6, hall=(11, 15), liquid="W", floor="I", platforms="crumble", weights=dict(walk=3, hall=6, pool=2, climb=1)),
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
            "gauntlet": k["liquid"] is not None and k["liquid"] in HURTS and left >= 74,
        }
        names = [name for name, weight in k["weights"].items() if fits[name] and name != last for _ in range(weight)]
        piece = dice.choice(names or ["walk"])
        last = piece
        if piece == "walk":
            wide = dice.randint(6, 11)
            dark = wide >= 8 and left >= 64 and dice.random() < 0.6
            if dark:
                # A long stretch with no light of its own in its air and no lamp in it, before it
                # or just past it: a lamp is seen a long way, so the dark needs room.
                wide += 12
                road.since = -(wide + road.theme["gap"])
                for x in range(road.x - road.d * 8, road.x, road.d):
                    for y in range(road.floor - k["h"] - 2, road.floor):
                        if road.w.t[y][x] in LAMPS:
                            road.w.t[y][x] = AIR
            cells = road.walk(wide, int(max(-2, min(2, round(drift / 2)))), h=k["h"], char=None)
            if dark:
                x0, x1 = span_of(cells)
                road.w.mark("dark", x0, road.floor - k["h"] - 6, x1 - x0 + 1, k["h"] + 8)
        elif piece == "gauntlet":
            # A long crossing on what does not stay: a stone, a slab that gives way, one that comes and goes.
            road.walk(3, h=k["h"])
            road.pool(28, k["liquid"], 3, h=k["h"] + 2, stones="mixed")
            road.walk(3, h=k["h"])
        elif piece == "hall":
            wide = dice.randint(14, 24)
            tall = dice.randint(*k["hall"])
            cells = road.hall(wide, tall, flat=k.get("flat", False), char=k.get("floor"))
            if tall >= 11 and wide >= 18 and dice.random() < 0.5:
                road.perch(min(c[0] for c in cells) + wide // 2 - 2, road.floor - 3)
        elif piece in ("pool", "bridge"):
            liquid = k["liquid"]
            road.walk(2, h=k["h"])
            unsteady = k["platforms"] if "platforms" in k and dice.random() < 0.45 else None
            if piece == "bridge":
                road.pool(13, liquid, 3, h=k["h"] + 1, bridge=True)
            elif liquid in HURTS:
                road.pool(dice.choice([8, 13]), liquid, 3, h=k["h"] + 1, stones=unsteady)
            elif unsteady:
                road.pool(13, liquid, 4, h=k["h"] + 1, stones=unsteady)
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
    dip = 2 if left >= 34 else 0  # a long one sags in the middle, so it is not a box
    if dip:
        side.walk(4, dip, rough=False)
        left -= 8
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
    if dip:
        side.walk(4, -dip, rough=False)
    side.walk(3, rough=False)


def side_roads(world: World, log=print):
    """Gives every circle its side roads, where there is room for them: low roads under
    its plain stretches (one with a relic in a room, one flooded, one behind a gate to
    come back for) and attics over its tunnels. The places are searched for, not written
    down: a side road that would cut into anything is tried somewhere else."""
    later = {"limbo": "seal", "lust": "seal", "gluttony": "seal", "earth": "barrier"}
    # What a wall that is not one hides, by turns: mostly a relic, sometimes more life or another draught.
    rewards = itertools.cycle(("Heart", "relic", "Vial", "relic", "relic"))
    for region, road, trail, locked in world.roads:
        if region not in KITS or len(trail) < 80:
            continue
        columns = sorted(trail)
        lo, hi = columns[0] + 8, columns[-1] - 12
        lows = attics = secrets = deeps = 0
        wanted = max(3, (hi - lo) // 58)
        for i in range(wanted):
            centre = lo + (i + 0.5) * (hi - lo) / wanted
            kind = i % 3
            lay = lambda side, kind=kind, region=region: stroll(  # noqa: E731
                side, region, gate=later.get(region, "barrier") if kind == 2 else None, wet=kind == 1
            )
            for length, shift in [(l, s) for l in (92, 76, 64, 52, 40, 30) for s in (0, 9, -9, 18, -18, 27, -27)]:
                try:
                    side = low_road(road, trail, locked, int(centre - length / 2 + shift), int(centre + length / 2 + shift), lay=lay)
                except Clash:
                    continue
                lows += 1
                if i % 2 == 0:
                    # Past the foot of its west hatch, a wall that is not one: the best of what is hidden is behind these.
                    try:
                        secret_room(world, side, side.hatch - 1, side.floor, -1, reward=next(rewards))
                        secrets += 1
                    except Clash:
                        pass
                if side.span >= 40:
                    # And under a long low road, sometimes, a third way: short, dark, with something in it.
                    start = side.hatch + 4
                    try:
                        low_road(side, side.trail, side.locked, start + 5, start + side.span - 9, depth=13, lay=lambda deep: stroll(deep, region))
                        deeps += 1
                    except Clash:
                        pass
                break
        nooks = max(2, wanted // 3)
        for i in range(nooks):
            centre = lo + (i + 0.25) * (hi - lo) / nooks
            for shift, east in [(s, e) for s in (0, 7, -7, 14, -14, 21, -21, 28, -28) for e in (True, False)]:
                try:
                    nook, cells = attic(road, trail, locked, int(centre + shift), wings=i % 2 == 0, east=east)
                except Clash:
                    continue
                attics += 1
                if i % 2 == 1:
                    try:
                        secret_room(world, nook, cells[-1][0] + nook.d, cells[-1][1], nook.d, 5, reward=next(rewards))
                        secrets += 1
                    except Clash:
                        pass
                break
        log(f"  {region}: {lows} low roads, {deeps} under them, {attics} attics, {secrets} secrets")


def secret_room(world: World, parent: Path, wall_x: int, floor: int, d: int, wide: int = 6, reward: str = "relic"):
    """A room behind a wall that looks like any other until it is struck: the wall is three
    cells of a column (`wall_x`), broken by the sword; the room runs on from it. Checked
    whole before anything is dug."""
    if any(world.t[y][wall_x] not in GROUND or world.owner[y][wall_x] for y in range(floor - 5, floor + 2)):
        raise Clash(f"no wall for a secret at column {wall_x}")
    if world.t[floor - 1][wall_x - d] in GROUND:
        raise Clash(f"nowhere to stand before the secret at column {wall_x}")
    columns = [wall_x + d * i for i in range(1, wide + 2)]
    firm(world, columns, range(floor - 6, floor + 2))
    room = Path(world, wall_x + d, floor, d, parent, lamps=False)
    room.root = room
    cells = room.walk(wide, h=4, rough=False, square=True)
    for y in range(floor - 3, floor):
        world.open(wall_x, y, room.owner, True)
        world.gates.add((wall_x, y))
    world.square.update((wall_x, y) for y in range(floor - 5, floor + 2))
    xs = [c[0] for c in cells]
    world.mark("secret", wall_x, floor - 3, 1, 3, min(xs), floor - 4, max(xs), floor - 1, world.t[floor][wall_x])
    if reward == "relic":
        world.mark("relic", cells[-2][0], floor - 1)
    else:
        world.mark("upgrade", cells[-2][0], floor - 1, reward)
    world.put(cells[-1][0], floor - 1, "^")
    world.links.append(((wall_x - d, floor - 1), (wall_x + d, floor - 1), 0, True))


def sink(world: World, upper, lower, x: int):
    """A way straight down from one circle's road into the next one's: three columns of
    the floor that only a Smite breaks, and a clean shaft under them through the roof of
    the road below. A way down for the hero who comes back with the Smite; not a way up."""
    _, road_u, trail_u, locked_u = upper
    _, road_l, trail_l, locked_l = lower
    a = level_run(trail_u, x, wide=5)
    hole = range(a + 1, a + 4)
    if any(c in locked_u for c in range(a - 1, a + 6)) or any(c in locked_l or c not in trail_l for c in range(a, a + 5)):
        raise Clash(f"no room for a sink at column {x}")
    top, low = trail_u[a], trail_l[a + 1]
    if any(trail_l[c] != low for c in hole) or low - top < 22:
        raise Clash(f"the road under column {x} is not level, or too near")
    if any(mark[1] in range(a, a + 5) and mark[2] == top - 1 for mark in world.marks):
        raise Clash(f"something stands on the floor at column {x}")
    roofs = [next((y for y in range(low - 1, 0, -1) if world.t[y][c] in GROUND), 0) for c in hole]
    if max(roofs) - min(roofs) > 3 or min(roofs) < top + 8:
        raise Clash(f"the roof under column {x} will not take a shaft")
    firm(world, range(a, a + 5), range(top + 2, min(roofs) + 1))
    for c, roof in zip(hole, roofs):
        firm(world, [c], range(top + 2, roof + 1))
    shaft = Path(world, a + 1, top, 1, road_u, lamps=False)
    world.befriend(shaft.owner, road_l.owner)
    for c, roof in zip(hole, roofs):
        for y in range(top, roof + 1):
            world.open(c, y, shaft.owner, True)
        world.gates.update(((c, top), (c, top + 1)))
        world.square.update((c, y) for y in range(top - 1, roof + 2))
    world.mark("seal", a + 1, top, "slam", 3, 2)
    world.links.append(((a, top - 1), (a + 2, low - 1), NEED["Smite"], False))
    locked_u.update(range(a, a + 5))
    locked_l.update(range(a, a + 5))


def sinks(world: World, log=print):
    """Sinks from each pass of the road into the one under it, where both run level one
    over the other: the hidden ways down through the middle of the world."""
    passes = [entry for entry in world.roads if entry[0] in KITS and entry[0] != "earth"]
    made = 0
    for upper, lower in zip(passes, passes[1:]):
        for share in (0.35, 0.68):
            x = int(WEST + 24 + share * (EAST - WEST - 48))
            for shift in (0, 6, -6, 12, -12, 18, -18, 24, -24, 30, -30):
                try:
                    sink(world, upper, lower, x + shift)
                    made += 1
                    break
                except Clash:
                    continue
    log(f"  {made} sinks between the passes")


# --- What is written and drawn on the world: signs, numbers, the signs of the abilities ----

# What a shade says in the first hall of a circle (by the hall's name), what is cut on the
# tablet in its great cave, and on the one at each shrine.
SHADES = {
    "earth_crossroads": "Two ways part here. Down and east, the Gate. West, the old mine, and under its end the head of the Pit.",
    "earth_gate": "Past this gate there is no sky. What you carry in is what you have.",
    "limbo_gate": "We are not punished here. We only wait, and have forgotten for what.",
    "lust": "The wind took us and has not set us down. Keep to the ground while it blows.",
    "gluttony": "It has rained here since before I came. Mind the pools: they are deeper than they look.",
    "greed": "All this gold and nothing to buy. The light you seek is in the middle vault.",
    "heresy": "Every tomb here is lit from within. Do not trust the slabs over the fire: they give way.",
    "violence_wood": "The trees were people. Go gently; and do not try the river without wings of some kind.",
    "fraud_ledge": "Nothing here is what it says. The bridges that come and go at least keep time.",
    "treachery": "Colder than the fire was hot. The ice is thin where it is clear.",
}
TABLETS = {
    "earth_mine": "The miners went down for copper and came up with stories.",
    "limbo_fields": "Here those who wait raised a hall of columns to remember the sun by.",
    "lust_spire": "The spires were lovers once, they say, worn thin by the wind.",
    "gluttony_mire": "The Maw. It is always open and never full.",
    "greed_vault": "The great vault. Its door has stood open an age: there was never a thief to fear.",
    "wrath_shore": "The far shore of the Styx. The trees drowned standing.",
    "heresy_city": "Dis, the city. Its towers burn and are not consumed.",
    "violence_shore": "The wood of those who harmed themselves. No leaf, no bird.",
    "fraud_wells": "The old viaduct. Half its arches were never real.",
    "treachery_lake": "He is fast in the ice to the waist, and has not moved since the ice came.",
}
SHRINE_WORDS = {
    "LightDash": "Be light for a moment, and light will not stop you.",
    "WallGrip": "What will not let you pass will let you climb.",
    "HolyLight": "Night is a seal. Light is the hand that breaks it.",
    "Glide": "Fall slowly, and the far side comes to you.",
    "Smite": "Come down like judgement on what is cracked.",
    "Wings": "You came down on foot. You need not go up so.",
}
# The sign of each ability, on the wall of its shrine: an arrow, a hand, a star, a kite, a hammer, wings.
EMBLEMS = {
    "LightDash": ["..#....", "...##..", "#######", "...##..", "..#...."],
    "WallGrip": ["#.#.#.#", "#.#.#.#", "#######", ".#####.", "..###.."],
    "HolyLight": ["...#...", ".#.#.#.", "#######", ".#.#.#.", "...#..."],
    "Glide": ["...#...", "..###..", ".#####.", "#######", "...#..."],
    "Smite": ["#######", "#######", "...#...", "...#...", "...#..."],
    "Wings": ["#.....#", "##...##", "###.###", ".#####.", "..#.#.."],
}
DIGITS = {
    1: [".#.", "##.", ".#.", ".#.", "###"],
    2: ["###", "..#", "###", "#..", "###"],
    3: ["###", "..#", "###", "..#", "###"],
    4: ["#.#", "#.#", "###", "..#", "..#"],
    5: ["###", "#..", "###", "..#", "###"],
    6: ["###", "#..", "###", "#.#", "###"],
    7: ["###", "..#", ".#.", ".#.", ".#."],
    8: ["###", "#.#", "###", "#.#", "###"],
    9: ["###", "#.#", "###", "..#", "###"],
}


def circle_of(key: str) -> int:
    """The number of a circle of Hell by its region's key; 0 for the Earth and the sky."""
    return max([k for k, _ in BANDS].index(key) - 1, 0)


def numeral(world: World, char: str, x: int, y: int, n: int, scale: int = 1):
    """A circle's number on the wall behind, its top left at a cell."""
    for row, line in enumerate(DIGITS.get(n, [])):
        for column, c in enumerate(line):
            if c == "#":
                for dy in range(scale):
                    for dx in range(scale):
                        raise_wall(world, char, x + column * scale + dx, y + row * scale + dy)


def road_of(world: World, region: str):
    return next(entry for entry in world.roads if entry[0] == region)


# --- What fills the rock: caves nobody walks into ------------------------------------


def pockets(world: World, log=print):
    """Caves in the rock that no road leads to, seen in passing: a third of them with a
    pool of the region's liquid in them, a third with crystals. They are dug last, only
    where nothing else is, so the rock between the roads is not one black mass."""
    made = 0
    for index, (key, top) in enumerate(BANDS):
        if key == "paradise":
            continue
        bottom = BANDS[index + 1][1] if index + 1 < len(BANDS) else HEIGHT - 6
        top = max(top, SURFACE_LINE + 10)
        liquid = KITS.get(key, {}).get("liquid") or "W"
        for k in range((bottom - top) * WIDTH // 230):
            cx = 26 + int(hash01(index, k, 91) * (WIDTH - 52))
            cy = top + 5 + int(hash01(index, k, 92) * max(bottom - top - 10, 1))
            rx, ry = 4 + int(hash01(index, k, 93) * 7), 2 + int(hash01(index, k, 94) * 3)
            cells = {
                (x, y)
                for y in range(cy - ry - 1, cy + ry + 2)
                for x in range(cx - rx - 1, cx + rx + 2)
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1 + 0.3 * field(600 + index, x, y, 3.0)
            }
            if len(cells) < 14:
                continue
            try:
                firm(world, range(cx - rx - 3, cx + rx + 4), range(cy - ry - 3, cy + ry + 4))
            except Clash:
                continue
            owner = world.new_owner()
            for x, y in sorted(cells):
                world.open(x, y, owner, False)
            low = max(y for _, y in cells)
            kind = int(hash01(index, k, 95) * 3)
            if kind == 0:
                for x, y in cells:
                    if y >= low - 1:
                        world.t[y][x] = liquid
            elif kind == 1 and key == "earth":
                # The Earth's are lined with copper, which gives a warm light: no crystals there.
                for x, y in cells:
                    for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                        if (nx, ny) not in cells and world.t[ny][nx] in "SDC" and hash01(nx, ny, 96) < 0.6:
                            world.t[ny][nx] = "O"
            elif kind == 1:
                floors = sorted((x, y) for x, y in cells if (x, y + 1) not in cells and (x, y - 1) in cells)
                for x, y in floors[:: max(len(floors) // 2, 1)][:2]:
                    world.t[y][x] = "^"
            made += 1
    log(f"  {made} caves in the rock that no road leads to")


# --- What stands on the Earth ----------------------------------------------------------


def tree(world: World, x: int, ground: int, tall: int):
    """A tree on the wall behind: a trunk and a round crown."""
    for y in range(ground - tall, ground):
        raise_wall(world, "w", x, y)
    wide, high = 2 + tall // 4, 2
    for dy in range(-high - 1, high + 1):
        for dx in range(-wide - 1, wide + 2):
            if (dx / (wide + 0.6)) ** 2 + ((dy + 0.5) / (high + 0.6)) ** 2 <= 1:
                raise_wall(world, "l", x + dx, ground - tall - 1 + dy)


def surface(world: World, earth_road):
    """What stands on the Earth: trees along the meadows, what is left of a house by the
    start, the old shaft down into the mine, and something lying in the pond."""
    surf = world.surface
    start, mouth = world.spawn[0], EAST - 70
    _, road, trail, locked = earth_road

    # The trees, the village, the mill and the graves are pictures now, stood once the world
    # is laid (tv2_earth_map.py); `tree` above is what Paradise's own trees still use.
    world.mark("sign", start + 3, surf[start + 3] - 1, "post", *"The Gate is east, under the arch. The Pit is west, under the meadow. The stair of light past the pond is not for you yet.".split())
    # Something lies in the pond.
    world.mark("relic", (POND[0] + POND[1]) // 2, surf[POND[0]] + 2)
    # The old shaft: a second way into the mine, straight down from the meadow.
    for near in (WEST + 110, WEST + 150, WEST + 80, WEST + 190):
        try:
            a = level_run(trail, near)
            if any(c in locked for c in range(a - 1, a + 5)) or any(abs(c - tx) < 4 for c in range(a, a + 4) for tx in (start,)):
                raise Clash("no room")
            floor = trail[a]
            top = max(surf[c] for c in range(a, a + 4))
            roofs = [next((y for y in range(floor - 1, 0, -1) if world.t[y][c] in GROUND), 0) for c in range(a, a + 4)]
            if min(roofs) < top + 8:
                raise Clash("too near the mine")
            firm(world, [a - 1, a + 4], range(top + 2, min(roofs) + 1))
            for c, roof in zip(range(a, a + 4), roofs):
                firm(world, [c], range(top + 2, roof + 1))
        except Clash:
            continue
        shaft = Path(world, a, top, 1, road, lamps=False)
        world.befriend(shaft.owner, world.owner[floor - 1][a])
        for c, roof in zip(range(a, a + 4), roofs):
            for y in range(min(surf[c], top), roof + 1):
                world.open(c, y, shaft.owner, True)
            world.t[top][c] = '"'
            world.square.update((c, y) for y in range(top - 1, floor))
        for row in range(top + 3, floor - 2, 3):
            for c in (a, a + 1):
                world.t[row][c] = '"'
        locked.update(range(a, a + 4))
        world.put(a - 1, surf[a - 1] - 1, "T")
        world.mark("sign", a - 3, surf[a - 3] - 1, "post", *"The old shaft. It goes down to the mine.".split())
        world.view("earth_shaft", a + 2, top + 6)
        break


def wisp(world: World, x0: int, x1: int, top: int, thick: int) -> bool:
    """A small cloud adrift between the islands, where the sky is empty: nothing of the
    road stands within a few cells of it. Whether there was room for it."""
    if x0 < 4 or x1 >= WIDTH - 4 or top < 8:
        return False
    for y in range(top - 7, top + thick + 5):
        for x in range(x0 - 5, x1 + 6):
            if world.t[y][x] != AIR or world.walls[y][x] != AIR:
                return False
    if any(x0 - 6 <= x <= x1 + 6 and top - 8 <= y <= top + thick + 5 for _, x, y, *_ in world.marks):
        return False
    island(world, x0, x1, top, thick)
    return True


def descend_west(road: Path, dy: int, region: str, kind: str = "shaft", lower_char: str | None = None) -> Path:
    """The west end of a pass of the road: the way down to the next one, the antechambers
    at both ends of it, and the next pass, which starts east from the foot of the way down."""
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
    lower.root = following  # it is the next pass's room: nothing of that pass is dug under it
    lower.antechamber(lower_char)
    return following


def well_door(road: Path, far: int, floor: int):
    """A door from the east end of a circle into the Well: a short tunnel and a seal a
    bolt of Holy Light breaks."""
    door = Path(road.w, far, floor, 1, road, lamps=False)
    if EAST + 6 > far:
        door.walk(EAST + 6 - far, h=4, rough=False, square=True)
    door.gate("seal")
    road.w.well_doors.append(floor)
    road.w.mark("sign", far, floor - 1, "post", *"The Well runs past every circle. Night seals this door; a bolt of light opens it.".split())


def turn_east(road: Path, dy: int, region: str, kind: str = "shaft", upper_door: bool = True, lower_door: bool = True) -> Path:
    """The east end of a pass of the road: the way down to the next one, which starts west
    from its foot, and a door into the Well for each of the two circles that meet here."""
    top = road.floor
    wide = 6 if kind == "shaft" else 4
    (road.shaft if kind == "shaft" else road.smooth)(dy, wide, back=True)
    if upper_door:
        well_door(road, EAST + wide, top)
    if lower_door:
        well_door(road, EAST + wide, top + dy)
    return road.begin(region)


def well(world: World):
    """The Well: a shaft down the world's east edge past the east end of every circle, with
    ledges to climb. Its doors are sealed until the hero has the Holy Light; from then on
    it is the quick way between the circles he has been to, and a way into ones he has not."""
    rows = sorted(set(world.well_doors))
    owner = world.new_owner()
    for other in list(world.friends):
        if other != owner:
            world.befriend(owner, other)
    top, floor = rows[0] - 9, rows[-1]
    for y in range(top, floor):
        for x in range(*WELL):
            world.open(x, y, owner, False)
        world.square.update((x, y) for x in range(WELL[0] - 1, WELL[1] + 1))
    for x in range(*WELL):
        world.solid(x, floor)
        world.solid(x, floor + 1)
    # A ledge at every door, and between two doors as many as keep them three rows apart or less.
    ledges = []
    for upper, lower in zip(rows, rows[1:]):
        steps = -(-(lower - upper) // 3)
        ledges += [upper + round(i * (lower - upper) / steps) for i in range(steps)]
    for row in ledges:
        for x in range(WELL[0], WELL[0] + 4):
            world.t[row][x] = world.theme_at(row)["ledge"]
    for row in rows:
        world.put(WELL[0], row - 1, world.theme_at(row)["floor_lamp"])
        # Each door's circle is written on the far wall.
        numeral(world, world.theme_at(row)["wall"], WELL[1] - 4, row - 9, circle_of(world.theme_at(row)["key"]), 1)
    world.view("well", (WELL[0] + WELL[1]) / 2 - 12, (top + floor) // 2)


# --- Buildings: what stands on the wall behind a great cave ---------------------------


def raise_wall(world: World, char: str, x: int, y: int):
    """A cell of a building on the wall behind, where there is air to see it against."""
    if 2 <= x < WIDTH - 2 and 2 <= y < HEIGHT - 2 and world.t[y][x] not in GROUND and (x, y) not in world.walled:
        world.walls[y][x] = char


def colonnade(world: World, char: str, x0: int, x1: int, floor: int, tall: int, every: int = 6):
    """A row of columns two cells thick under a lintel and a low pediment."""
    for x in range(x0, x1 + 1, every):
        for y in range(floor - tall, floor):
            raise_wall(world, char, x, y)
            raise_wall(world, char, x + 1, y)
    for row, inset in ((floor - tall - 1, 0), (floor - tall - 2, 0), (floor - tall - 3, 4), (floor - tall - 4, 8)):
        for x in range(x0 - 1 + inset, x1 + 3 - inset):
            raise_wall(world, char, x, row)


def tower(world: World, char: str, x: int, floor: int, wide: int, tall: int):
    """A tower with a crenellated top and rows of windows."""
    for y in range(floor - tall, floor):
        for c in range(x, x + wide):
            window = (floor - y) % 5 == 3 and (c - x) % 3 == 1 and y > floor - tall + 2
            if not window:
                raise_wall(world, char, c, y)
    for c in range(x, x + wide, 2):
        raise_wall(world, char, c, floor - tall - 1)


def spire(world: World, char: str, x: int, floor: int, tall: int, half: int = 2):
    """A needle of rock: widest at its foot."""
    for i in range(tall):
        wide = max(0, int(round(half * (1 - i / tall))))
        for c in range(x - wide, x + wide + 1):
            raise_wall(world, char, c, floor - 1 - i)


def dead_tree(world: World, char: str, x: int, floor: int, tall: int):
    """A great bare tree: a trunk and boughs that fork upwards."""
    for i in range(tall):
        raise_wall(world, char, x, floor - 1 - i)
        raise_wall(world, char, x + 1, floor - 1 - i)
    for k, start in enumerate(range(tall // 3, tall - 1, 3)):
        side = 1 if k % 2 == 0 else -1
        for j in range(1, 3 + (tall - start) // 3):
            raise_wall(world, char, x + (1 if side > 0 else 0) + side * j, floor - 1 - start - j)
            if j > 2:
                raise_wall(world, char, x + (1 if side > 0 else 0) + side * j, floor - 2 - start - j)


def arches(world: World, char: str, x0: int, x1: int, floor: int, tall: int):
    """A viaduct: piers and round arches under a deck."""
    span = 8
    for x in range(x0, x1 + 1):
        along = (x - x0) % span
        for i in range(tall):
            under = i < tall - 2 and 0 < along < span - 1 and i < tall - 2 - abs(along - span / 2 + 0.5) * 0.9 + 2
            if not under:
                raise_wall(world, char, x, floor - 1 - i)


GIANT = [
    "......#####......",
    ".....#######.....",
    ".....##.#.##.....",
    ".....#######.....",
    "......#####......",
    "...###########...",
    "..#############..",
    ".###############.",
    "###.#########.###",
    "###.#########.###",
    "##..#########..##",
    "##..#########..##",
    "....#########....",
    "....#########....",
    "...###########...",
]


def giant(world: World, char: str, x: int, floor: int):
    """A giant fast in the ice to the waist: his head, his shoulders, his arms hanging."""
    for i, line in enumerate(reversed(GIANT)):
        for j, c in enumerate(line):
            if c == "#":
                raise_wall(world, char, x - len(line) // 2 + j, floor - 1 - i)


def vault_door(world: World, char: str, x: int, floor: int, radius: int = 7):
    """The round door of a vault: a ring with spokes."""
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            far = math.hypot(dx, dy)
            if radius - 1.6 <= far <= radius + 0.4 or (far < radius - 1 and (dx == 0 or dy == 0 or abs(dx) == abs(dy)) and far > 1.5) or far <= 1.5:
                raise_wall(world, char, x + dx, floor - 1 - radius + dy)


def teeth(world: World, char: str, x0: int, x1: int, floor: int, roof: dict):
    """A maw: fangs of rock standing from the floor and hanging from the roof."""
    for k, x in enumerate(range(x0, x1, 5)):
        tall = 4 + (k * 7) % 5
        spire(world, char, x, floor, tall, 1)
        top = floor - roof.get(x, 12)
        for i in range(3 + (k * 5) % 4):
            for c in range(x + 2 - (1 if i < 2 else 0), x + 3 + (1 if i < 2 else 0)):
                raise_wall(world, char, c, top + i)


# --- The regions -------------------------------------------------------------------


class Course:
    """Where a pass of the road is along its way: `at(t)` is the column and `floor(t)` the
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


def span_of(cells) -> tuple[int, int]:
    xs = [c[0] for c in cells]
    return min(xs), max(xs)


def earth(world: World) -> Path:
    surface = world.surface
    start = (POND[0] + POND[1]) // 2 - 28
    world.spawn = (start, surface[start] - 1)
    world.view("earth", start + 2, GROUND_LINE - 4)
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
    # Under the sky the arch is a picture (`earth_gateway`, tv2_earth_map.py); its piers go on
    # down the wall behind, into the ground.
    for x in (mouth + 4, mouth + 5, mouth - 10, mouth - 11):
        for y in range(surface[x], surface[x] + 6):
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
    world.mark("sign", cells[6][0], cells[6][1] - 1, "tablet", *TABLETS["earth_mine"].split())
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
    # The Gate in its hall is a picture (`earth_gate_arch`, stood by tv2_earth_map.py).
    world.gate_arch = (x0 + 11, road.floor)
    world.put(x0 + 9, road.floor - 1, "*")
    world.put(x0 + 13, road.floor - 1, "*")
    return turn_east(road, FLOORS["limbo"][0] - road.floor, "limbo")


def limbo(road: Path) -> Path:
    """The first circle, and a great one: west along the ash galleries to the Light Dash,
    down at the Pit, and back east under them through the fields of the forgotten."""
    world = road.w
    way = Course(road, WEST, FLOORS["limbo"][1])
    road.hall(16, 10, name="limbo_gate")
    road.rest()
    way.ramble(0.2)
    road.walk(4)
    road.pool(13, "~", 4, stones=True)
    road.walk(4)
    way.ramble(0.4)
    cells = road.hall(30, 12, 2, name="limbo")
    road.perch(cells[15][0] - 2, cells[15][1] - 4, relic=True)
    road.rest()
    way.ramble(0.6, lift=-4)
    road.walk(8, 2)
    road.ledge(5)
    road.shrine("LightDash")
    road.gate("barrier")
    road.walk(8, 2)
    road.gate("barrier")
    road.rest()
    have = ("LightDash",)
    way.ramble(0.96, have)
    road.gate("barrier")
    road.reach(WEST, way.f1)
    road = descend_west(road, FLOORS["limbo_low"][0] - way.f1, "limbo")

    way = Course(road, EAST, FLOORS["limbo_low"][1])
    road.walk(8)
    way.ramble(0.26, have)
    road.hall(24, 10, name="limbo_crypts")
    road.rest()
    way.ramble(0.58, have)
    cells = road.cavern(76, 28, name="limbo_fields")
    x0, x1 = span_of(cells)
    colonnade(world, "h", x0 + 22, x1 - 26, road.floor, 13)
    road.rest()
    way.ramble(0.95, have)
    road.gate("barrier")
    road.walk(3)
    road.rest()
    road.reach(EAST, way.f1)
    return turn_east(road, FLOORS["lust"][0] - way.f1, "lust", "smooth", upper_door=False)


def lust(road: Path) -> Path:
    world = road.w
    have = ("LightDash",)
    way = Course(road, WEST, FLOORS["lust"][1])
    road.walk(8)
    cells = road.hall(30, 16, name="lust")
    x0, x1 = span_of(cells)
    road.perch(x0 + 5, road.floor - 3)
    road.perch(x0 + 12, road.floor - 6)
    road.perch(x0 + 19, road.floor - 9, relic=True)
    # The tempest: a wind that blows against the way in gusts.
    world.mark("wind", x0, road.floor - 12, x1 - x0 + 1, 12, 50, 0, 2.5, 2.5)
    way.ramble(0.22, have)
    road.walk(8, 2)
    road.ledge(6)
    road.shrine("WallGrip")
    road.smooth(-12)
    road.walk(8)
    road.smooth(-7)
    road.walk(6)
    road.smooth(way.floor(0.36) + 4 - road.floor)
    road.walk(4)
    road.rest()
    have += ("WallGrip",)
    way.ramble(0.58, have)
    cells = road.cavern(72, 30, name="lust_spire")
    x0, x1 = span_of(cells)
    for k, x in enumerate(range(x0 + 12, x1 - 8, 11)):
        spire(world, "r", x, road.floor, 12 + (k * 7) % 9, 2)
    # Lifts: a fall is slowed in them, and a glide is carried up.
    for x in (x0 + 8, x1 - 14):
        world.mark("wind", x, road.floor - 24, 6, 24, 0, 150, 0, 0)
    road.rest()
    way.ramble(0.96, have)
    road.rest()
    road.reach(WEST, way.f1)
    return descend_west(road, FLOORS["gluttony"][0] - way.f1, "gluttony")


def gluttony(road: Path) -> Path:
    world = road.w
    have = ("LightDash", "WallGrip")
    way = Course(road, EAST, FLOORS["gluttony"][1])
    road.walk(8, h=4)
    way.ramble(0.18, have)
    road.walk(3, h=4)
    cells = road.pool(23, "&", 6)
    road.relic(cells[11][0], cells[11][1] + 5)
    road.walk(3, h=4)
    way.ramble(0.42, have)
    road.hall(28, 9, name="gluttony")
    road.rest()
    road.gate("barrier")
    way.ramble(0.6, have)
    cells = road.cavern(64, 24, name="gluttony_mire")
    x0, x1 = span_of(cells)
    teeth(world, "u", x0 + 6, x1 - 6, road.floor, road.last_roof)
    road.rest()
    way.ramble(0.95, have)
    road.rest()
    road.reach(EAST, way.f1)
    return turn_east(road, FLOORS["greed"][0] - way.f1, "greed")


def greed(road: Path) -> Path:
    world = road.w
    have = ("LightDash", "WallGrip")
    way = Course(road, WEST, FLOORS["greed"][1])
    road.hall(16, 10, flat=True)
    road.rest()
    way.ramble(0.12, have)
    cells = road.hall(28, 12, flat=True, name="greed")
    x0, _ = span_of(cells)
    road.perch(x0 + 3, road.floor - 3, 5)
    road.perch(x0 + 12, road.floor - 6, 5, relic=True)
    road.perch(x0 + 20, road.floor - 3, 5)
    way.ramble(0.3, have, lift=-4)
    road.walk(8, 2)
    road.ledge(5)
    road.shrine("HolyLight")
    road.gate("seal")
    road.walk(8, 2)
    road.gate("seal")
    road.rest()
    have += ("HolyLight",)
    way.ramble(0.6, have)
    cells = road.cavern(70, 26, name="greed_vault")
    x0, x1 = span_of(cells)
    vault_door(world, "n", (x0 + x1) // 2, road.floor, 8)
    road.rest()
    way.ramble(0.88, have)
    road.hall(22, 11, flat=True, name="greed_treasury")
    road.gate("seal")
    way.ramble(0.96, have)
    road.reach(WEST, way.f1)
    return descend_west(road, FLOORS["wrath"][0] - way.f1, "wrath")


def wrath(road: Path) -> Path:
    world = road.w
    have = ("LightDash", "WallGrip", "HolyLight")
    way = Course(road, EAST, FLOORS["wrath"][1])
    road.walk(8)
    way.ramble(0.14, have)
    road.walk(3)
    road.pool(34, "~", 6, h=9, wall=False)
    road.walk(4)
    road.siphon(12)
    road.walk(4)
    way.ramble(0.42, have)
    road.walk(4)
    cells = road.pool(27, "~", 4, h=11, wall=False)
    world.view("wrath", cells[13][0], cells[13][1] - 5)
    road.walk(4)
    road.rest()
    road.gate("seal")
    way.ramble(0.6, have)
    cells = road.cavern(70, 26, name="wrath_shore")
    x0, x1 = span_of(cells)
    for k, x in enumerate(range(x0 + 10, x1 - 8, 13)):
        dead_tree(world, "y", x, road.floor, 11 + (k * 5) % 7)
    road.rest()
    way.ramble(0.95, have)
    road.rest()
    road.reach(EAST, way.f1)
    return turn_east(road, FLOORS["heresy"][0] - way.f1, "heresy")


def heresy(road: Path) -> Path:
    """The sixth circle, the other great one: west through the tombs to the Glide, down at
    the Pit, and back east under them through the city of Dis."""
    world = road.w
    have = ("LightDash", "WallGrip", "HolyLight")
    way = Course(road, WEST, FLOORS["heresy"][1])
    road.hall(16, 9)
    road.rest()
    road.walk(4, h=5)
    road.pool(13, "L", 3, stones="crumble")
    road.hall(28, 9, name="heresy")
    road.rest()
    way.ramble(0.34, have)
    road.shaft(-9)
    road.walk(8)
    road.shrine("Glide")
    road.chasm(11, "L")
    road.walk(6)
    road.chasm(11, "L")
    world.view("heresy_chasm", road.x + 14, road.floor - 5)
    road.walk(3)
    road.smooth(way.floor(0.5) + 2 - road.floor)
    road.walk(4)
    road.rest()
    have += ("Glide",)
    way.ramble(0.95, have)
    road.gate("barrier")
    road.reach(WEST, way.f1)
    road = descend_west(road, FLOORS["heresy_low"][0] - way.f1, "heresy")

    way = Course(road, EAST, FLOORS["heresy_low"][1])
    road.walk(8)
    way.ramble(0.28, have)
    road.hall(26, 10, name="heresy_tombs")
    road.rest()
    way.ramble(0.56, have)
    cells = road.cavern(78, 30, name="heresy_city")
    x0, x1 = span_of(cells)
    for k, x in enumerate(range(x0 + 10, x1 - 14, 15)):
        tower(world, "b", x, road.floor, 7, 13 + (k * 5) % 9)
    road.rest()
    way.ramble(0.94, have)
    road.gate("barrier")
    road.walk(3)
    road.rest()
    road.reach(EAST, way.f1)
    return turn_east(road, FLOORS["violence"][0] - way.f1, "violence", upper_door=False)


def violence(road: Path) -> Path:
    world = road.w
    have = ("LightDash", "WallGrip", "HolyLight", "Glide")
    way = Course(road, WEST, FLOORS["violence"][1])
    road.hall(16, 9)
    road.rest()
    # The river of blood: nobody crosses it who cannot glide.
    road.walk(3)
    road.chasm(11, "!")
    road.walk(4)
    way.ramble(0.2, have)
    cells = road.hall(30, 13, name="violence_wood")
    x0, _ = span_of(cells)
    road.perch(x0 + 6, road.floor - 3)
    road.perch(x0 + 13, road.floor - 6, relic=True)
    road.perch(x0 + 20, road.floor - 3)
    road.rest()
    way.ramble(0.4, have, lift=-6)
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
    way.ramble(0.7, have)
    cells = road.cavern(70, 28, name="violence_shore")
    x0, x1 = span_of(cells)
    for k, x in enumerate(range(x0 + 9, x1 - 8, 12)):
        dead_tree(world, "v", x, road.floor, 13 + (k * 7) % 8)
    road.rest()
    way.ramble(0.96, have)
    road.reach(WEST, way.f1)
    return descend_west(road, FLOORS["fraud"][0] - way.f1, "fraud", "slab")


def fraud(road: Path) -> Path:
    world = road.w
    have = ("LightDash", "WallGrip", "HolyLight", "Glide", "Smite")
    way = Course(road, EAST, FLOORS["fraud"][1])
    road.walk(7)
    way.ramble(0.16, have)
    road.walk(3)
    road.pool(13, "%", 3, bridge=True)
    world.view("fraud", road.x - 8, road.floor - 5)
    road.walk(4)
    way.ramble(0.4, have)
    road.hall(22, 10, name="fraud_ledge")
    road.rest()
    road.walk(3)
    road.pool(18, "%", 3, stones="blink")
    road.walk(3)
    way.ramble(0.62, have)
    cells = road.cavern(66, 24, name="fraud_wells")
    x0, x1 = span_of(cells)
    arches(world, "f", x0 + 8, x1 - 8, road.floor, 12)
    road.rest()
    way.ramble(0.95, have)
    road.rest()
    road.reach(EAST, way.f1)
    return turn_east(road, FLOORS["treachery"][0] - way.f1, "treachery", "smooth")


def treachery(road: Path) -> Path:
    world = road.w
    have = ("LightDash", "WallGrip", "HolyLight", "Glide", "Smite")
    end = FLOORS["treachery"][1]
    road.hall(16, 9)
    road.rest()
    # The ice is entered through a slab nobody breaks without the Smite.
    road.walk(3)
    road.slab(9)
    road.walk(4)
    way = Course(road, WEST + 3, end - 12)
    way.ramble(0.14, have)
    road.hall(40, 15, char="I", name="treachery")
    road.walk(2)
    road.pool(13, "W", 5, stones="crumble")
    road.walk(4)
    way.ramble(0.4, have)
    road.gate("barrier")
    cells = road.cavern(82, 30, char="I", name="treachery_lake")
    x0, x1 = span_of(cells)
    giant(world, "i", (x0 + x1) // 2, road.floor)
    # The wind off the lake, in gusts.
    world.mark("wind", x0, road.floor - 9, x1 - x0 + 1, 9, 55, 0, 2.0, 3.0)
    road.rest()
    road.gate("seal")
    ramble(road, WEST + 40, way.floor(0.93), have)
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
    # A chain hangs the whole way down, and each circle's number is on the wall over its tunnel.
    for y in range(top + 5, floor):
        if y % 4:
            raise_wall(world, world.theme_at(y)["wall"], PIT_LEDGE - 2, y)
    for door, key in world.pit_doors:
        numeral(world, THEMES[key][1], PIT[0] + 1, door - 16, circle_of(key), 2)
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
            if crown == "G" and deep > 3:
                world.t[top + 1][x] = "D"


def heaven_tree(world: World, x: int, ground: int, tall: int, crown: str):
    """A tree of Paradise on the wall behind: a pale trunk, a crown of blossom or of gold."""
    for y in range(ground - tall, ground):
        raise_wall(world, "t", x, y)
        if tall >= 7:
            raise_wall(world, "t", x + 1, y)
    # A crown of three rounds: a great one, and a lesser one low at either side.
    wide = 2 + tall // 5
    top = ground - tall - 2
    for cx, cy, rx, ry in ((0, 0, wide, wide), (-wide, 2, 2, 2), (wide + (tall >= 7), 2, 2, 2)):
        for dy in range(-ry - 1, ry + 2):
            for dx in range(-rx - 1, rx + 2):
                if (dx / (rx + 0.6)) ** 2 + (dy / (ry + 0.6)) ** 2 <= 1:
                    raise_wall(world, crown, x + cx + dx, top + cy + dy)


def ladder(world: World, x: int, top: int, ground: int, wide: int = 4) -> list[int]:
    """Ledges of gilt one over another from the row `top` down toward the ground at row
    `ground`, six rows apart: the Wings' own stair. The lowest is in a plain jump's reach
    of the ground, so the stair is seen to begin there. Returns their rows."""
    rows = list(range(top, ground - 3, LADDER))
    if ground - rows[-1] > LADDER:
        rows.append(ground - 3)
    for row in rows:
        for dx in range(wide):
            if world.t[row][x + dx] == AIR:
                world.t[row][x + dx] = ","
    return rows


def falling(world: World, x: int, top: int, bottom: int, wide: int = 2):
    """Water falling through the air from the row `top` to the row before `bottom`."""
    for y in range(top, bottom):
        for dx in range(wide):
            if world.t[y][x + dx] == AIR:
                world.t[y][x + dx] = "|"


def gable(world: World, cx: int, base: int, half: int, rows: int, run: int):
    """A roof of gold in steps: from the row `base` up, `rows` rows, each `run` columns
    narrower at either end. It is gold all through: gold gives light, so a block of it has
    no dark heart, and the roof is seen from the Earth at night."""
    for k in range(rows):
        row, reach = base - k, half - run * k
        for x in range(cx - reach, cx + reach + 1):
            world.t[row][x] = "K"
            world.square.add((x, row))


def marble(world: World, columns, rows):
    for x in columns:
        for y in rows:
            world.t[y][x] = "M"
            world.square.add((x, y))


def golden(world: World, columns, rows):
    """A pier of gold: what a gate of Paradise hangs in. Gold gives light, so it has no dark heart."""
    for x in columns:
        for y in rows:
            world.t[y][x] = "K"
            world.square.add((x, y))


def hollow(world: World, columns, rows, wall: str | None = "m"):
    """Air inside something built, with the wall behind it."""
    for x in columns:
        for y in rows:
            world.t[y][x] = AIR
            world.square.add((x, y))
            if wall:
                world.walls[y][x] = wall


SLANT = {"K": ("j", "k"), "M": ("a", "e")}  # what is built, cut on the slant: rising to the right, to the left
ANGEL = [
    "....ggg....",
    "...g...g...",
    "....xxx....",
    "....xxx....",
    ".x...x...x.",
    "xxx.xxx.xxx",
    "xxxxxxxxxxx",
    ".xxxxxxxxx.",
    "..xxxxxxx..",
    "...xxxxx...",
    "....xxx....",
    "....xxx....",
    "...xxxxx...",
    "...xxxxx...",
    "..xxxxxxx..",
]


def slant(world: World, x0: int, x1: int, y0: int, y1: int):
    """Where what is built of marble or gold steps up by one cell, the step is cut on the
    slant: a roof and a dome are lines, not stairs."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            below = world.t[y + 1][x]
            if world.t[y][x] != AIR or world.t[y - 1][x] != AIR or below not in SLANT:
                continue
            right, left = world.t[y][x + 1], world.t[y][x - 1]
            if right == below and left == AIR and world.t[y - 1][x + 1] == AIR:
                world.t[y][x] = SLANT[below][0]
            elif left == below and right == AIR and world.t[y - 1][x - 1] == AIR:
                world.t[y][x] = SLANT[below][1]


def pitched(world: World, x0: int, x1: int, base: int, char: str = "K") -> int:
    """A roof at 45 degrees over the columns x0 .. x1, its eaves on the row `base`: each row a
    cell narrower at either end, the ends cut on the slant. Returns its top row."""
    row = base
    while x0 <= x1:
        for x in range(x0, x1 + 1):
            world.t[row][x] = char
            world.square.add((x, row))
        if world.t[row][x0 - 1] == AIR:
            world.t[row][x0 - 1] = SLANT[char][0]
        if world.t[row][x1 + 1] == AIR:
            world.t[row][x1 + 1] = SLANT[char][1]
        x0, x1, row = x0 + 1, x1 - 1, row - 1
    return row + 1


def cupola(world: World, cx: float, base: int, outer: float, inner: float, fill: str | None = "z", char: str = "K") -> int:
    """The upper half of a ball, hollow: a shell of gold from the row `base` up, the wall
    `fill` inside it with stars of gilt in it, its steps cut on the slant. Returns its top row."""
    top = base
    for dy in range(int(outer) + 2):
        y = base - dy
        for x in range(int(cx - outer) - 1, int(cx + outer) + 2):
            d = math.hypot(x - cx, dy)
            if inner < d <= outer:
                world.t[y][x] = char
                world.square.add((x, y))
                top = min(top, y)
            elif d <= inner and fill:
                # The inside of a dome is a night sky: stars of gilt in its shadow.
                world.walls[y][x] = "X" if dy > 1 and d < inner - 1 and hash01(x, y, 77) < 0.045 else fill
    slant(world, int(cx - outer) - 2, int(cx + outer) + 2, top - 1, base)
    return top


def finial(world: World, x: int, foot: int, tall: int = 4):
    """A rod of gilt with a bar across it, on the top of something."""
    for y in range(foot - tall, foot):
        raise_wall(world, "X", x, y)
    for dx in (-1, 1):
        raise_wall(world, "X", x + dx, foot - tall + 1)


def pillar(world: World, x: int, top: int, foot: int, wide: int = 2):
    """A column on the wall in front of the wall, in bright marble: a shaft from the row
    `top` down to the row before `foot`, a capital and a base a cell wider at either side."""
    for y in range(top, foot):
        for dx in range(wide):
            raise_wall(world, "x", x + dx, y)
    for y in (top, foot - 1):
        for dx in (-1, wide):
            raise_wall(world, "x", x + dx, y)


def arch(world: World, x0: int, x1: int, top: int, spring: int, char: str = "x"):
    """The wall over a round arch: the columns x0 .. x1 from the row `top` down to the row
    `spring`, but for the half round the arch leaves open."""
    middle, radius = (x0 + x1) / 2, (x1 - x0 + 1) / 2
    for y in range(top, spring + 1):
        for x in range(x0, x1 + 1):
            if math.hypot(x - middle, y - spring - 0.5) > radius:
                raise_wall(world, char, x, y)


def lancet(world: World, x: int, top: int, foot: int, half: int = 1, glass: str | None = "o"):
    """A tall window with a pointed head, `half` cells either side of the column x: stained
    glass, or (no glass) the open sky."""
    for y in range(top, foot):
        reach = min(half, y - top)
        for dx in range(-reach, reach + 1):
            world.walls[y][x + dx] = glass or AIR


def rose(world: World, cx: int, cy: int, radius: int):
    """A round window of warm glass in a ring of gilt, with a cross of gilt through it."""
    for dy in range(-radius - 1, radius + 2):
        for dx in range(-radius - 1, radius + 2):
            d = math.hypot(dx, dy)
            if d <= radius + 1.2:
                world.walls[cy + dy][cx + dx] = "q" if d <= radius and dx and dy else "X"


def chandelier(world: World, x: int, top: int, drop: int, wide: int = 1):
    """Lanterns hung from a roof on a chain of gilt."""
    for y in range(top, top + drop):
        raise_wall(world, "X", x, y)
    for dx in range(-wide, wide + 1):
        world.put(x + dx, top + drop, "J")


def statue(world: World, x: int, foot: int):
    """One of the host in white marble, wings up, a ring of gilt over its head: seven times
    the hero's height, on the wall behind."""
    for row, line in enumerate(ANGEL):
        for column, c in enumerate(line):
            if c != ".":
                raise_wall(world, "X" if c == "g" else "x", x + column, foot - len(ANGEL) + row)


def belfry(world: World, x0: int, x1: int, foot: int, tall: int) -> int:
    """A tower on the wall behind, standing on a roof: marble in shadow between two bright
    corner strips, a slit of glass, an open arch near the top with a bell of gilt in it, and
    over it a spire of gold that is stood on. Returns the spire's top row."""
    top = foot - tall
    for y in range(top, foot):
        for x in range(x0, x1 + 1):
            raise_wall(world, "x" if x in (x0, x1) else "m", x, y)
    middle = (x0 + x1) // 2
    for y in range(top + 2, top + 7):
        for x in range(x0 + 2, x1 - 1):
            if y > top + 2 or x0 + 3 <= x <= x1 - 3:
                world.walls[y][x] = AIR
    for y in range(top + 4, top + 7):
        for x in (middle, middle + 1):
            world.walls[y][x] = "X"
    for y in range(top + 9, foot - 2):
        for x in (middle, middle + 1):
            world.walls[y][x] = "o"
    for x in range(x0 - 1, x1 + 2):
        world.t[top - 1][x] = "K"
        world.square.add((x, top - 1))
    peak = pitched(world, x0, x1, top - 2)
    finial(world, middle, peak, 4)
    return peak


SOULS = {
    "garden": "You climbed out of all of it. Three lights sleep here: one in the wind to the west, one in the stone to the east, one in the hollow under your feet. Wake them, and the door above will know you.",
    "orchard": "Nothing here is owed. The wind will carry you if you open your hands to it.",
    "palace": "Every door in this house you have opened before, in a darker place. That is all the trial there is.",
    "temple": "The door counts lights, not deeds.",
    "stair": "Half way. Look down once, and not again.",
}


def say(world: World, kind: str, x: int, y: int, words: str):
    world.mark("sign", x, y, kind, *words.split())


def paradise(world: World):
    """Heaven over the Earth, entered by three stairs only the Wings climb. It is not a road
    but a place with a middle: the Garden, a great island right over the start, with a lake
    on it; over the Garden the Temple, whose door opens when three lights are lit; and the
    three lights at the ends of three ways that may be gone in any order, each asking for
    what the journey gave: west over gulfs of wind to the Orchard (the Glide), east over a
    bridge to the House of Trials (the Light Dash, the Wall Grip, the Holy Light), and down
    into the Hollow inside the Garden's own cloud (the Smite or the Holy Light). Water falls
    from the Temple into the lake, through the Hollow and on down into the Earth's pond."""
    surf = world.surface
    centre = (POND[0] + POND[1]) // 2
    hub = GROUND_LINE - 48  # the Garden's ground
    temple = hub - 46  # the top of the Temple's island
    world.clear = []  # the columns of the Earth's surface the stairs stand on: nothing is planted there

    # --- The Garden ----------------------------------------------------------------
    hx0, hx1 = centre - 69, centre + 63
    island(world, hx0, hx1, hub, 20, "G")
    # The lake, level with the grass.
    lake = range(centre - 12, centre + 13)
    for x in lake:
        for y in range(hub, hub + 3):
            world.t[y][x] = "W"
        for y in (hub + 3, hub + 4):
            world.t[y][x] = "M"  # a basin of marble
            world.square.add((x, y))
    # The Hollow under it: a grotto inside the cloud, with the lake's water coming down through it.
    floor = hub + 12
    grotto = range(centre - 35, centre + 20)
    hollow(world, grotto, range(hub + 5, floor), "c")
    for x in range(grotto[0] - 1, grotto[-1] + 2):
        for y in range(hub + 4, floor + 1):
            world.walls[y][x] = "c"
    assert all(world.t[y][x] in GROUND for x in range(grotto[0] - 2, grotto[-1] + 8) for y in (floor, floor + 1, floor + 3)), "the Garden's cloud is too thin for the Hollow"
    for x in range(centre - 7, centre + 7):
        for y in (floor, floor + 1):
            world.t[y][x] = "W"
    falling(world, centre - 1, hub + 5, floor)
    for x in (grotto[0] + 3, centre - 13, centre + 10, grotto[-1] - 2):
        world.put(x, floor - 1, "^")
    world.mark("relic", centre + 3, floor + 1)
    for x in range(centre - 26, centre - 21):
        world.t[floor][x] = "K"
    world.mark("beacon", centre - 24, floor - 1, "Deep")
    say(world, "tablet", centre + 13, floor - 1, "The lake over this roof is the sky's own cup. What spills from it the Earth drinks.")
    world.view("paradise_hollow", centre - 8, floor - 5)
    # Its two doors. From the stair: a seal of night a bolt of Holy Light breaks.
    door = range(grotto[-1] + 1, grotto[-1] + 6)
    hollow(world, door, range(floor - 4, floor), "c")
    seal = door[2]
    world.mark("seal", seal, floor - 4, "bolt", 1, 4)
    world.gates.update((seal, y) for y in range(floor - 4, floor))
    world.links.append(((seal + 1, floor - 1), (seal - 1, floor - 1), NEED["HolyLight"], True))
    # From the Garden: a cracked slab in the grass, and the drop under it.
    slab = range(centre - 33, centre - 30)
    for x in slab:
        for y in range(hub, hub + 5):
            world.t[y][x] = AIR
            world.walls[y][x] = "c"
        world.gates.update(((x, hub), (x, hub + 1)))
        world.square.update((x + dx, y) for dx in (-1, 0, 1) for y in range(hub - 1, hub + 6))
    world.mark("seal", slab[0], hub, "slam", 3, 2)
    above, below = (slab[0] - 1, hub - 1), (slab[1], floor - 1)
    world.links.append((above, below, NEED["Smite"], False))
    world.links.append((below, above, NEED["Smite"] | NEED["WallGrip"], False))
    # The stair from the Earth comes up through the Garden: a round well in the cloud, the
    # ledges in it, and the sealed door of the Hollow in its side.
    well_x = door[-1] + 1
    for x in range(well_x, well_x + 6):
        for y in range(hub, hub + 24):
            if world.t[y][x] in GROUND:
                world.t[y][x] = AIR
                world.walls[y][x] = "c"
        world.square.update((x + dx, y) for dx in (-1, 0, 1) for y in range(hub - 1, hub + 25))
    ground = min(surf[x] for x in range(well_x - 2, well_x + 8))
    ladder(world, well_x, hub, ground)
    for x in range(well_x, well_x + 6):
        world.t[hub][x] = ","
    world.clear.append((well_x - 8, well_x + 13))
    say(world, "post", well_x + 8, surf[well_x + 8] - 1, "A stair of light, up through the cloud. It is for the winged.")
    world.view("paradise_stair", well_x + 3, hub + 16)
    # The water of heaven, from the Garden's underside down into the Earth's pond.
    under = next(y for y in range(hub + 5, hub + 30) if world.t[y][centre] == AIR and y > floor)
    falling(world, centre - 1, under, surf[POND[0]])
    world.view("earth_falls", centre + 4, surf[POND[0]] - 8)
    # What grows and stands in the Garden.
    for x, tall, crown in ((hx0 + 19, 8, "p"), (hx0 + 28, 5, "g"), (centre - 22, 7, "p"), (centre - 16, 5, "p"), (centre + 17, 6, "p"), (centre + 41, 8, "p"), (centre + 47, 5, "g")):
        heaven_tree(world, x, hub, tall, crown)
    # A gate at the Garden's east end, before the bridge: two columns, a round arch, a rod of
    # gilt on it. At its west end two tall columns, and between them the stair to the Temple.
    x0 = hx1 - 12
    pillar(world, x0, hub - 9, hub)
    pillar(world, x0 + 9, hub - 9, hub)
    arch(world, x0 + 2, x0 + 8, hub - 14, hub - 10)
    for x in range(x0 - 1, x0 + 12):
        raise_wall(world, "x", x, hub - 15)
    finial(world, x0 + 5, hub - 15, 3)
    for x in (centre - 64, centre - 57):
        pillar(world, x, hub - 18, hub)
        finial(world, x, hub - 18, 3)
    # A pavilion over the well of the stair: four columns and a roof of gold.
    for x in (well_x - 3, well_x + 7):
        pillar(world, x, hub - 8, hub)
    golden(world, range(well_x - 5, well_x + 11), [hub - 9])
    finial(world, well_x + 3, pitched(world, well_x - 4, well_x + 9, hub - 10), 3)
    for x in (hx0 + 14, centre - 14, centre + 14, centre + 44):
        world.put(x, hub - 1, "J")
    world.mark("checkpoint", well_x + 9, hub - 1)
    world.put(well_x + 10, hub - 1, "J")
    say(world, "soul", centre + 21, hub - 1, SOULS["garden"])
    say(world, "post", hx0 + 6, hub - 1, "West, over the gulfs of wind: the Orchard. Open your hands and fall slowly.")
    say(world, "post", hx1 - 6, hub - 1, "East, over the bridge: the House of Trials.")
    # A rainbow stands in the lake, round the falling water.
    world.mark("picture", centre, hub - 1, "backdrop_rainbow")
    world.view("paradise_garden", centre, hub - 6)

    # --- The Temple, over the Garden --------------------------------------------------
    # A great church of marble and gold: a hall behind an arcade, glass in its bays, a dome
    # over its middle that is open to the hall below, a bell tower at either end.
    tx0, tx1 = centre - 58, centre + 58
    island(world, tx0, tx1, temple, 14, "M")
    f = temple - 3  # the Temple's floor: three steps over its island
    for k, reach in enumerate((46, 44, 42)):
        marble(world, range(centre - reach, centre + reach + 1), [temple - 1 - k])
    hall = 22  # the height of the hall under its roof
    hollow(world, range(centre - 36, centre + 37), range(f - hall, f))
    for side in (-1, 1):
        marble(world, [centre + side * k for k in (37, 38, 39)], range(f - hall, f))
    # The roof: a slab with a cornice of gold, open in the middle to the dome.
    marble(world, range(centre - 41, centre + 42), (f - hall - 2, f - hall - 1))
    golden(world, range(centre - 42, centre + 43), [f - hall - 3])
    eaves = f - hall - 3  # the row of the cornice: what is stood on up here stands on it
    drum = eaves - 8  # the row the dome springs from
    hollow(world, range(centre - 12, centre + 13), range(drum, f - hall), "z")
    for side in (-1, 1):
        marble(world, [centre + side * 13, centre + side * 14], range(drum, eaves + 1))
    apex = cupola(world, centre, drum, 15.4, 13.2)
    # The lantern on the dome's top: four posts, a cap of gold, a spire and a rod.
    for x in (centre - 2, centre + 2):
        for y in range(apex - 4, apex):
            raise_wall(world, "x", x, y)
    golden(world, range(centre - 3, centre + 4), [apex - 5])
    finial(world, centre, pitched(world, centre - 2, centre + 2, apex - 6), 4)
    # Inside. The wall behind: glass in every bay, tall and pointed, and a rose over the altar.
    piers = (-35, -26, -17, 16, 25, 34)  # the west column of each pier of the arcade, from the middle
    for off in (-30, -21, 21, 30):
        lancet(world, centre + off, f - 15, f - 3, 1)
    for off in (-9, 9):
        lancet(world, centre + off, f - 17, f - 3, 1)
    rose(world, centre, f - 13, 4)
    for off in (-8, -4, 0, 4, 8):
        lancet(world, centre + off, drum + 1, drum + 7, 0)
    # The arcade in front of it: piers with capitals, round arches between them, a band over all.
    for off in piers:
        pillar(world, centre + off, f - 17, f)
    for west, east in ((-33, -27), (-24, -18), (18, 24), (27, 33)):
        arch(world, centre + west, centre + east, f - 21, f - 18)
    for x in list(range(centre - 36, centre - 12)) + list(range(centre + 13, centre + 37)):
        raise_wall(world, "x", x, f - hall)
        if abs(x - centre) <= 15:
            for y in range(f - 21, f - 17):
                raise_wall(world, "x", x, y)
    for off in (-34, -25, -16, 17, 26, 35):
        for y in range(f - 21, f - 17):
            for x in (centre + off - 1, centre + off):
                raise_wall(world, "x", x, y)
    # Lights: a chandelier in every bay, a ring of them under the dome, lanterns at the piers.
    for off in (-30, -21, 21, 30):
        chandelier(world, centre + off, f - 21, 3)
    for off, drop in ((-8, 2), (-4, 3), (0, 4), (4, 3), (8, 2)):
        chandelier(world, centre + off, eaves, drop, 0)
    for off in (-32, -23, -14, 14, 23, 32):
        world.put(centre + off, f - 1, "J")
    # The altar: two steps of gold, cut on the slant, and on them the end of the journey.
    golden(world, range(centre - 6, centre + 7), [f - 1])
    golden(world, range(centre - 4, centre + 5), [f - 2])
    slant(world, centre - 8, centre + 8, f - 3, f - 1)
    world.mark("end", centre, f - 3)
    # A ladder of gilt up into the dome, for the winged: galleries in the drum and under the
    # crown, and on the highest a draught.
    for row in range(f - 7, drum - 5, -LADDER):
        for x in range(centre - 9, centre - 5):
            world.t[row][x] = ","
    crown = drum - 8
    for x in range(centre - 8, centre + 3):
        world.t[crown][x] = ","
    world.mark("upgrade", centre, crown - 1, "Vial")
    world.view("paradise_dome", centre, drum - 4)
    # A glory stands behind the dome: rings and rays of pale gold in the sky.
    world.mark("picture", centre, drum + 19, "backdrop_glory")
    # Its one door, in the west wall: shut until the three lights are lit.
    hollow(world, range(centre - 39, centre - 36), range(f - 5, f))
    world.mark("sanctum", centre - 38, f - 5, 5, 3)
    # Outside. A porch at either end: three columns under a band and a gable of gilt.
    for side in (-1, 1):
        for k in (56, 51, 46):
            x = centre + side * k - (0 if side < 0 else 1)
            pillar(world, x, f - 16, temple)
        for x in range(centre + side * 57, centre + side * 39, -side):
            for y in (f - 18, f - 17):
                raise_wall(world, "x", x, y)
        mid = centre + side * 49
        for k in range(7):
            for x in range(mid - 7 + k, mid + 8 - k):
                raise_wall(world, "X" if k in (0, 6) or abs(x - mid) == 7 - k else "m", x, f - 19 - k)
    # A bell tower on the roof at either end, and a way up and over: ledges at the ends of the
    # house to the cornice, and at either side of the dome onto its back.
    for side in (-1, 1):
        x0 = centre + side * 41 if side < 0 else centre + 34
        belfry(world, x0, x0 + 7, eaves, 18)
        for row in range(f - 7, eaves, -LADDER):
            for x in range(centre + side * 43 - (0 if side < 0 else 3), centre + side * 43 + (4 if side < 0 else 1)):
                if world.t[row][x] == AIR:
                    world.t[row][x] = ","
        for row, off in ((eaves - 5, 21), (eaves - 11, 19)):
            for x in range(centre + side * off - (0 if side < 0 else 3), centre + side * off + (4 if side < 0 else 1)):
                world.t[row][x] = ","
    world.mark("checkpoint", centre - 54, temple - 1)
    world.put(centre - 55, temple - 1, "J")
    say(world, "soul", centre - 48, temple - 1, SOULS["temple"])
    world.view("paradise", centre, f - 9)
    world.view("paradise_temple", centre - 46, f - 11)
    world.view("paradise_spires", centre - 22, eaves - 12)
    # The water of heaven starts here: out of the Temple's cloud, down into the Garden's lake.
    under = next(y for y in range(temple + 1, temple + 24) if world.t[y][centre] == AIR)
    falling(world, centre - 1, under, hub)
    # The stair of light from the Garden to the Temple, a cloud half way up it, and beside
    # the house a wind that blows upward for the one who glides.
    for row in range(hub - LADDER, temple, -LADDER):
        for x in range(tx0 - 4, tx0):
            world.t[row][x] = ","
    island(world, tx0 - 22, tx0 - 8, hub - 24, 3)
    heaven_tree(world, tx0 - 16, hub - 24, 5, "p")
    world.mark("relic", tx0 - 12, hub - 25)
    say(world, "soul", tx0 - 19, hub - 25, SOULS["stair"])
    world.mark("wind", tx1 + 2, temple - 8, 6, hub - temple + 6, 0, 150, 0, 0)

    # --- West: the Orchard, over the gulfs of wind -------------------------------------
    orchard = hub - 16
    for i, (x0, x1, top) in enumerate(((hx0 - 24, hx0 - 12, hub - 2), (hx0 - 50, hx0 - 36, hub - 6), (hx0 - 76, hx0 - 62, hub - 10))):
        island(world, x0, x1, top, 3)
        world.put((x0 + x1) // 2, top - 1, "J")
        # A wind that blows upward in gusts in the gulf east of it.
        world.mark("wind", x1 + 1, top - 14, 11, 22, 0, 120, 2.5, 1.5)
    ox0, ox1 = hx0 - 168, hx0 - 88
    island(world, ox0, ox1, orchard, 12, "G")
    for x in range(ox1 + 4, ox1 + 8):
        world.t[orchard + 3][x] = ","
    for x, tall in ((ox0 + 5, 6), (ox0 + 13, 8), (ox0 + 68, 5), (ox0 + 76, 8)):
        heaven_tree(world, x, orchard, tall, "g")
    # A round temple: columns in a ring under a band, a dome of gold on them, lanterns hung
    # from its crown, and under it the light of the West. One of the host stands at either hand.
    ring = ox0 + 40
    for off in (-11, -7, 6, 10):
        pillar(world, ring + off, orchard - 11, orchard)
    for x in range(ring - 13, ring + 14):
        for y in (orchard - 13, orchard - 12):
            raise_wall(world, "x", x, y)
    finial(world, ring, cupola(world, ring, orchard - 14, 12.4, 10.6), 4)
    chandelier(world, ring, orchard - 24, 9)
    for x in range(ring - 2, ring + 3):
        world.t[orchard][x] = "K"
    world.mark("beacon", ring, orchard - 1, "West")
    statue(world, ring - 26, orchard)
    statue(world, ring + 16, orchard)
    world.mark("checkpoint", ox1 - 10, orchard - 1)
    world.put(ox1 - 11, orchard - 1, "J")
    say(world, "soul", ox1 - 22, orchard - 1, SOULS["orchard"])
    world.mark("relic", ox0 + 3, orchard - 1)
    world.view("paradise_orchard", ring, orchard - 6)
    # A high cloud over the Orchard, with a heart on it.
    for row in range(orchard - LADDER, orchard - 31, -LADDER):
        for x in range(ring + 30, ring + 34):
            world.t[row][x] = ","
    island(world, ring + 10, ring + 28, orchard - 36, 3)
    world.mark("upgrade", ring + 19, orchard - 37, "Heart")
    # The second stair from the Earth: under the Orchard's west end.
    ground = min(surf[x] for x in range(ox0 - 6, ox0 + 2))
    ladder(world, ox0 - 4, orchard + 2, ground)
    world.clear.append((ox0 - 12, ox0 + 6))
    say(world, "post", ox0 + 3, surf[ox0 + 3] - 1, "A stair of light. Over it, they say, trees with leaves of gold.")

    # --- East: the House of Trials, over the bridge ------------------------------------
    marble(world, range(hx1 + 1, hx1 + 19), [hub])
    for west in (hx1 + 2, hx1 + 11):
        arch(world, west, west + 6, hub + 1, hub + 4)
    for x in (hx1 + 1, hx1 + 9, hx1 + 10, hx1 + 18):
        for y in range(hub + 1, hub + (10 if x in (hx1 + 9, hx1 + 10) else 7)):
            raise_wall(world, "x", x, y)
    for x in range(hx1 + 2, hx1 + 18, 3):
        raise_wall(world, "x", x, hub - 1)
    island(world, hx1 + 19, hx1 + 32, hub, 4, "M")
    world.mark("checkpoint", hx1 + 25, hub - 1)
    world.put(hx1 + 26, hub - 1, "J")
    say(world, "post", hx1 + 22, hub - 1, "The House of Trials: a door for the swift, a wall for the clinging, a seal for the bright.")
    house = hub - 8  # the ground the House stands on
    for x in range(hx1 + 33, hx1 + 36):
        world.t[hub - 3][x] = ","
    for x in range(hx1 + 36, hx1 + 39):
        world.t[hub - 6][x] = ","
    ix0, ix1 = hx1 + 40, hx1 + 146
    island(world, ix0, ix1, house, 14, "M")
    px0, px1 = ix0 + 12, ix0 + 96
    upper, roof = house - 14, house - 28
    hollow(world, range(px0 + 2, px1 - 1), range(roof + 2, house))
    marble(world, (px0, px0 + 1, px1 - 1, px1), range(roof, house))
    marble(world, range(px0, px1 + 1), (roof, roof + 1))
    shaft = range(px0 + 54, px0 + 58)
    marble(world, [x for x in range(px0 + 2, px1 - 1) if x not in shaft], (upper, upper + 1))
    # Windows: where the wall behind is left out the sky shows, and lights the room.
    for off in (9, 31, 45, 69, 79):
        lancet(world, px0 + off, house - 11, house - 3, 1, None)
    for off in (9, 27, 46, 65, 75):
        lancet(world, px0 + off, upper - 11, upper - 3, 1)
    # Columns along both halls, in front of the wall.
    for off in (4, 13, 24, 35, 50, 62, 73, 81):
        pillar(world, px0 + off, upper + 2, house, 1)
    for off in (4, 13, 23, 33, 43, 50, 62, 70, 81):
        pillar(world, px0 + off, roof + 2, upper, 1)
    # The doors of the House, on the ground at either end, and a balcony door upstairs to the west.
    hollow(world, (px0, px0 + 1, px1 - 1, px1), range(house - 4, house), None)
    hollow(world, (px0, px0 + 1), range(upper - 4, upper), None)
    for x in range(px0 - 5, px0):
        world.t[upper][x] = ","
    # Downstairs: a veil of light across the hall (the Light Dash), then a chimney with nothing
    # to stand on up to the floor above (the Wall Grip).
    veil = px0 + 18
    golden(world, range(veil - 2, veil + 3), range(upper + 2, house - 4))
    world.mark("barrier", veil, house - 4, 4)
    world.gates.update((veil, y) for y in range(house - 4, house))
    world.links.append(((veil - 1, house - 1), (veil + 1, house - 1), NEED["LightDash"], True))
    for x in (shaft[0] - 2, shaft[0] - 1, shaft[-1] + 1, shaft[-1] + 2):
        golden(world, [x], range(upper + 2, house - 4))
    world.links.append(((shaft[1], house - 1), (shaft[0] - 1, upper - 1), NEED["WallGrip"], False))
    # Upstairs: a seal of night (the Holy Light), and past it the light of the East.
    night = px0 + 38
    golden(world, range(night - 2, night + 3), range(roof + 2, upper - 4))
    world.mark("seal", night, upper - 4, "bolt", 1, 4)
    world.gates.update((night, y) for y in range(upper - 4, upper))
    world.links.append(((night + 1, upper - 1), (night - 1, upper - 1), NEED["HolyLight"], True))
    light = px0 + 18
    for x in range(light - 2, light + 3):
        world.t[upper][x] = "K"
    world.mark("beacon", light, upper - 1, "East")
    for x in (light - 6, light + 6):
        world.put(x, upper - 1, "*")
    world.mark("relic", px1 - 10, upper - 1)
    for x in (px0 + 6, px0 + 26, px0 + 46, px0 + 66, px0 + 78):
        world.put(x, house - 1, "J")
    for x in (px0 + 30, px0 + 46, px0 + 64, px0 + 78):
        world.put(x, upper - 1, "J")
    say(world, "tablet", px0 + 4, house - 1, SOULS["palace"])
    # Over the House. A keep in its middle under an onion of gold, entered through the roof
    # from the hall upstairs, with something at its top; a small dome on either wing; a
    # tower at either end.
    dome = (px0 + px1) // 2
    keep = roof - 18  # the keep's own roof
    marble(world, (dome - 9, dome - 8, dome + 8, dome + 9), range(keep, roof))
    hollow(world, range(dome - 7, dome + 8), range(keep + 2, roof))
    marble(world, range(dome - 10, dome + 11), (keep, keep + 1))
    golden(world, range(dome - 11, dome + 12), [keep - 1])
    rose(world, dome, roof - 10, 3)
    for off in (-5, 5):
        lancet(world, dome + off, roof - 14, roof - 3, 0)
    for dy in range(-8, 9):
        for dx in range(-8, 9):
            if math.hypot(dx, dy) <= 8.3:
                world.t[keep - 9 + dy][dome + dx] = "K"
                world.square.add((dome + dx, keep - 9 + dy))
    for k, half in enumerate((2, 1, 1, 0)):
        golden(world, range(dome - half, dome + half + 1), [keep - 18 - k])
    slant(world, dome - 10, dome + 10, keep - 22, keep - 2)
    finial(world, dome, keep - 21, 4)
    hatch_x = range(dome + 3, dome + 7)
    hollow(world, hatch_x, (roof, roof + 1))
    for row in list(range(upper - LADDER, roof - 1, -LADDER)) + [roof] + list(range(roof - LADDER, keep + 3, -LADDER)):
        for x in hatch_x:
            world.t[row][x] = ","
    world.mark("relic", dome - 3, roof - 1)
    world.put(dome - 5, roof - 1, "J")
    for wing in ((px0 + 8 + dome - 10) // 2, (dome + 10 + px1 - 8) // 2):
        finial(world, wing, cupola(world, wing, roof - 1, 6.4, 4.6), 3)
    for tower in (px0 - 1, px1 - 6):
        belfry(world, tower, tower + 7, roof, 20)
    for x in range(px0 + 8, px1 - 7, 2):
        if abs(x - dome) > 11 and world.t[roof - 1][x] == AIR and world.walls[roof - 1][x] == AIR:
            raise_wall(world, "x", x, roof - 1)
    # A porch over the west door: two columns and a gable of gilt.
    for x in (px0 - 7, px0 - 3):
        pillar(world, x, house - 9, house, 1)
    for k in range(4):
        for x in range(px0 - 8 + k, px0 - 1 - k):
            raise_wall(world, "X", x, house - 10 - k)
    for row in range(roof + 4, house - 3, LADDER):
        for x in range(px1 + 2, px1 + 6):
            world.t[row][x] = ","
    world.view("paradise_keep", dome, roof - 14)
    world.view("paradise_palace", dome - 14, house - 12)
    # The third stair from the Earth: under the House's east end.
    ground = min(surf[x] for x in range(ix1 - 2, ix1 + 7))
    ladder(world, ix1 + 1, house + 2, ground)
    world.clear.append((ix1 - 7, ix1 + 11))
    say(world, "post", ix1 - 4, surf[ix1 - 4] - 1, "A stair of light. Over it, a house with a roof of gold.")

    # Small clouds adrift in the empty sky, between the islands and over them.
    adrift = 0
    for k in range(160):
        if adrift == 26:
            break
        x0 = 10 + int(hash01(k, 61) * (WIDTH - 40))
        adrift += wisp(
            world,
            x0,
            x0 + 5 + int(hash01(k, 63) * 9),
            10 + int(hash01(k, 62) * (hub - 2)),
            2 + int(hash01(k, 64) * 2),
        )


def build(log=print) -> World:
    world = World()
    road = earth(world)
    for circle in (limbo, lust, gluttony, greed, wrath, heresy, violence, fraud, treachery):
        road = circle(road)
    pit(world)
    well(world)
    paradise(world)
    surface(world, road_of(world, "earth"))
    sinks(world, log)
    side_roads(world, log)
    pockets(world, log)
    world.shots += [
        ("earth_sunset", "earth", 18.75),
        ("paradise_dawn", "paradise_temple", 6.6),
        ("paradise_garden_sunset", "paradise_garden", 18.6),
        ("paradise_garden_night", "paradise_garden", 22.5),
        ("paradise_orchard_sunset", "paradise_orchard", 18.3),
    ]
    world.veins()
    world.slopes()
    world.wall_up()
    tv2_earth_map.dress(world, sys.modules[__name__], log)
    return world


# --- Checking and writing ------------------------------------------------------------

HEADER = """# The Tiles V2 world, written by tools/tiles_v2/make_world_map.py (edit that, not this):
# Paradise, the Earth and the nine circles of Hell on one map. The terrain grid, a
# blank line, the wall grid; the legend is in tiles_v2.json. `@region key row` is
# where a region starts, `@mark kind column row ...` a thing of the game's that
# stands in a cell (a checkpoint, an ability, a gate, a secret wall, a platform that does
# not stay, a wind, an upgrade, a sign, a stretch with no light in its air, a light of
# Paradise and the door the lights open), `@view` and
# `@shot` places and pictures for the captures.
"""


def check(world: World, log=print) -> list[str]:
    terrain = ["".join(row) for row in world.t]
    log("Working out where the hero can go:")
    reach = tv2_reach.Reach(terrain, world.gates, world.links, log)
    shrines = {m[3][0]: (m[1], m[2]) for m in world.marks if m[0] == "ability"}
    goals = {f"{m[0]} {i}": (m[1], m[2]) for i, m in enumerate(world.marks) if m[0] in ("checkpoint", "relic", "upgrade", "beacon", "end")}
    # What stands, stands on the ground: a sign, a place to rest, a light.
    floating = [
        f"the {m[0]} at ({m[1]}, {m[2]}) stands on nothing"
        for m in world.marks
        if m[0] in ("sign", "checkpoint", "beacon") and world.t[m[2] + 1][m[1]] not in GROUND
    ]
    return floating + tv2_reach.journey(reach, world.spawn, shrines, goals, log)


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
