#!/usr/bin/env python3
"""Builds the Paradise level of Tiles V2 from its plan (tv2_paradise_plan.py, the
wireframe as data) and writes it as a map the standard reads.

    python tools/tiles_v2/make_paradise_map.py           # write Assets/Art/TilesV2/paradise_map.txt
    python tools/tiles_v2/make_paradise_map.py --check   # and play it through in the model

A cell of the plan is CELL tiles. A room is the cells the plan gives it, with a shell
inside their edge: a floor two tiles thick, a ceiling and walls one. Between two rooms
that is a wall of two and a floor of three; toward the open sky it is cloud. A storey is
a row of cells: its floor is where a door's sill is, and inside a room of several
storeys it is a platform that covers part of the room, with ledges up through the gaps.
What joins two rooms is cut where the plan says: a doorway in a wall, a way through a
floor; and a gate is built as what only its ability passes (a sill only the Wings clear,
a veil for the Light Dash, a seal, a smooth chimney, a gulf to glide).

The model (tv2_reach.py) then plays it: with nothing the hero reaches the Wings, with
them the Light Dash, with both every shrine and the way out; nowhere can he strand
himself; and the late rooms are shut to him and open to one who has everything.
"""
from __future__ import annotations

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tv2_heaven
import tv2_paradise_dress
import tv2_paradise_plan as plan
import tv2_reach
from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PARADISE_MAP = os.path.join(ROOT, "Assets", "Art", "TilesV2", "paradise_map.txt")

CELL = (20, 12)  # tiles a cell of the plan is: across, down
CW, CH = CELL
LEFT, TOP, RIGHT, BOTTOM = 1, 2, 1, 3  # cells of open sky round the plan
COL0, ROW0 = 2 - LEFT, -TOP  # the plan's column and row at the map's first tile
WIDTH, HEIGHT = (120 - COL0 + 1 + RIGHT) * CW, (26 - ROW0 + BOTTOM) * CH
AIR = "."
NEED = tv2_reach.BIT
GROUND = set(TERRAIN_CHARS)
CLOUD = "Q"
LEDGE = ","

# What each zone is built of: its ground (floors and platforms), the wall behind a room
# that is roofed (none: the sky), and the lamp that stands on its floors.
STYLE = {
    1: dict(ground="G", under="D", back="d", lamp="J"),
    2: dict(ground="À", under="À", back="Õ", lamp="å"),
    3: dict(ground="Ä", under="Ä", back="Ù", lamp="J"),
    4: dict(ground="È", under="È", back="d", lamp="J"),
    5: dict(ground="M", under="M", back="m", lamp="J"),
    6: dict(ground="Í", under="Í", back="Ú", lamp="^"),
    7: dict(ground="Ð", under="Ð", back="Û", lamp="*"),
}
# The rooms that are roofed: under a canopy, in a trunk, in the ground, in a house. The rest are under the sky.
ROOFED = {
    "roots", "h1", "l1",
    "orchard", "undergrove", "rootway", "rows", "canopy", "l2", "grove", "lantern", "crownlet", "eastwalk", "h2",
    "underway", "h3", "hollowlog", "rootcave", "l3", "nest",
    "burrow", "h4",
    "alcove", "cistern", "h5", "l5",
    "boiler", "shaft", "h6", "works", "l6", "drain",
    "bottom", "h7", "niche",
}  # fmt: skip


def X(i: int) -> int:
    """The first column of tiles of a column of cells."""
    return (i - COL0) * CW


def Y(j: int) -> int:
    """The first row of tiles of a row of cells."""
    return (j - ROW0) * CH


def S(j: int) -> int:
    """The row of tiles a storey's floor is on, for the storey in a row of cells."""
    return Y(j + 1) - 2


def hash01(*parts) -> float:
    n = 2166136261
    for p in parts:
        n = ((n ^ (int(p) & 0xFFFFFFFF)) * 16777619) & 0xFFFFFFFF
    n ^= n >> 15
    n = (n * 2246822519) & 0xFFFFFFFF
    n ^= n >> 13
    return n / 4294967296.0


class Room:
    def __init__(self, name: str, spec):
        self.name = name
        self.zone, self.kind, self.i0, self.j0, self.i1, self.j1 = spec
        self.x0, self.y0, self.x1, self.y1 = X(self.i0), Y(self.j0), X(self.i1 + 1) - 1, Y(self.j1 + 1) - 1
        self.style = STYLE[self.zone]
        self.roofed = name in ROOFED
        self.cover: dict[int, set[int]] = {j: set() for j in range(self.j0, self.j1)}  # the columns each upper storey's platform covers
        self.keep: dict[int, set[int]] = {j: set() for j in range(self.j0, self.j1 + 1)}  # columns that must be floor
        self.clear: dict[int, set[int]] = {j: set() for j in range(self.j0, self.j1 + 1)}  # columns that must not be
        self.winged = self.zone >= 4  # the hero has the Wings by the time he is here: its ledges are further apart

    @property
    def storeys(self):
        return range(self.j0, self.j1 + 1)

    def inner(self):
        return range(self.x0 + 1, self.x1)


class Level:
    def __init__(self):
        self.t = [[AIR] * WIDTH for _ in range(HEIGHT)]
        self.walls = [[AIR] * WIDTH for _ in range(HEIGHT)]
        self.room_at = [[None] * WIDTH for _ in range(HEIGHT)]  # the room a tile is in (its shell too)
        self.marks: list = []
        self.views: list = []
        self.shots: list = []
        self.gates: set = set()
        self.links: list = []
        self.spawn = (0, 0)
        self.rooms = {name: Room(name, spec) for name, spec in plan.ROOMS.items()}
        self.late_cells: dict[str, tuple[int, int]] = {}  # a cell to stand in, in each late room
        self.notes: list[str] = []

    def mark(self, kind: str, x: int, y: int, *rest):
        self.marks.append((kind, x, y, [str(r) for r in rest]))

    def view(self, name: str, x: float, y: float):
        self.views.append((name, x, y))

    def solid(self, x: int, y: int, char: str):
        self.t[y][x] = char

    def open(self, x: int, y: int):
        self.t[y][x] = AIR

    def stand(self, key: str, x: int, row: int, dx: int = 0, dy: int = 0):
        """Stands a picture of the tile set with its floor line at the foot of a cell of air."""
        assert self.t[row][x] not in GROUND, f"{key} would be marked in the ground at ({x}, {row})"
        self.mark("picture", x, row, key, *([dx, dy] if dx or dy else []))

    def say(self, kind: str, x: int, y: int, words: str):
        self.mark("sign", x, y, kind, *words.split())


# --- The shells -----------------------------------------------------------------------


def shells(level: Level):
    """Every room's floor, ceiling and walls, in its zone's stone; and the wall behind the roofed ones."""
    for room in level.rooms.values():
        g, u = room.style["ground"], room.style["under"]
        for y in range(room.y0, room.y1 + 1):
            for x in range(room.x0, room.x1 + 1):
                level.room_at[y][x] = room
                edge = x in (room.x0, room.x1) or y == room.y0
                if y >= room.y1 - 1:
                    level.t[y][x] = g if y == room.y1 - 1 else u
                elif edge:
                    level.t[y][x] = u
                if room.roofed:
                    level.walls[y][x] = room.style["back"]


def sky_side(level: Level, x: int, y: int) -> bool:
    return not (0 <= x < WIDTH and 0 <= y < HEIGHT) or level.room_at[y][x] is None


def skin(level: Level):
    """Where a room's shell faces the open sky it is cloud, and more cloud lies on it: a
    bank over a ceiling and beside a wall, a deeper ragged belly under a floor."""
    for room in level.rooms.values():
        for x in range(room.x0, room.x1 + 1):
            if sky_side(level, x, room.y0 - 1):
                level.t[room.y0][x] = CLOUD
                for d in (1, 2):
                    put_cloud(level, x, room.y0 - d)
            if sky_side(level, x, room.y1 + 1):
                deep = 3 + int(2.5 * (0.5 + 0.5 * math.sin(x * 0.21 + room.i0)) + 2 * hash01(x // 3, room.j1, 5))
                for d in range(1, deep + 1):
                    put_cloud(level, x, room.y1 + d)
        for y in range(room.y0, room.y1 - 1):
            for x, out in ((room.x0, -1), (room.x1, 1)):
                if sky_side(level, x + out, y):
                    level.t[y][x] = CLOUD
                    for d in (1, 2):
                        put_cloud(level, x + out * d, y)


def put_cloud(level: Level, x: int, y: int):
    if 0 <= x < WIDTH and 0 <= y < HEIGHT and level.room_at[y][x] is None:
        level.t[y][x] = CLOUD


# --- What joins the rooms -------------------------------------------------------------


def hole_columns(i: int) -> range:
    return range(X(i) + 8, X(i) + 12)


def plan_links(level: Level):
    """Says what each link needs of the storeys it touches (floor to stand on by a door,
    under a way up; nothing where a sill must be out of reach), before the platforms are laid."""
    for a, b, side, line, along, kind in plan.LINKS:
        ra, rb = level.rooms[a], level.rooms[b]
        if side == "v":
            left, right = (ra, rb) if ra.i1 == line - 1 else (rb, ra)
            wide = range(1, 7)
            left.keep[along].update(left.x1 - d for d in wide)
            right.keep[along].update(right.x0 + d for d in wide)
            if kind == "W" and along - 1 in left.cover:
                left.clear[along - 1].update(left.x1 - d for d in range(1, 10))
            if kind == "W" and along - 1 in right.cover:
                right.clear[along - 1].update(right.x0 + d for d in range(1, 10))
            if kind == "Gr":
                # The chimney stands in the room the late one opens off: nothing at the sill, floor under it.
                late, other = (left, right) if left.kind == "late" else (right, left)
                edge = other.x0 if other is right else other.x1
                step = 1 if other is right else -1
                other.keep[along].difference_update(edge + step * d for d in range(1, 7))
                other.clear[along].update(edge + step * d for d in range(1, 5))
                if along + 1 <= other.j1:
                    other.keep[along + 1].update(edge + step * d for d in range(1, 8))
        else:
            upper, lower = ra, rb
            columns = hole_columns(along)
            upper.keep[line - 1].update(range(columns[0] - 6, columns[-1] + 7))
            lower.keep[line].update(range(columns[0] - 6, columns[-1] + 7))
            if kind == "Gl" and lower.kind == "late":
                # A pit round the hole, walled off east of it: no floor over the pit, none at the
                # top storey for eleven tiles east of the wall, floor beyond that and under it.
                wall = gulf_wall(columns)
                for j in range(upper.j0, upper.j1):
                    upper.clear[j].update(range(upper.x0 + 1, wall + 1))
                    upper.keep[j].difference_update(range(upper.x0 + 1, wall + 1))
                upper.clear[upper.j0].update(range(wall + 1, wall + 12))
                upper.keep[upper.j0].update(range(wall + 12, upper.x1))
                for j in range(upper.j0 + 1, upper.j1):
                    upper.keep[j].update(range(wall + 1, upper.x1))


def platforms(level: Level):
    """Lays each upper storey's platform: over about two thirds of the room from one side
    and the next storey's from the other, so the way up winds; then what the links asked."""
    for room in level.rooms.values():
        inner = list(room.inner())
        count = len(inner)
        for k, j in enumerate(range(room.j0, room.j1)):
            from_west = (k + room.i0 + room.j0) % 2 == 0
            reach = max(6, int(count * (0.62 if count > 30 else 0.5)))
            cover = set(inner[:reach] if from_west else inner[-reach:])
            if count > 70:
                # A long room has a second platform in the far third, and a gap between.
                far = inner[-count // 4 :] if from_west else inner[: count // 4]
                cover = set(inner[: count // 2] if from_west else inner[-count // 2 :]) | set(far)
            cover |= room.keep[j]
            cover -= room.clear[j]
            cover &= set(inner)
            room.cover[j] = cover
        # A way up to every storey: a gap in its platform at least five wide, beside the
        # platform, with floor under the gap's end. Where what was asked left none, one is
        # opened in what nobody asked for.
        for j in range(room.j1 - 1, room.j0 - 1, -1):
            cover = room.cover[j]
            lower = set(inner) if j + 1 == room.j1 else room.cover[j + 1]
            if not cover:
                continue

            def spots(cover=cover, lower=lower):
                found = []
                for x in inner[: len(inner) - 4]:
                    span = range(x, x + 5)
                    if any(c in cover for c in span):
                        continue
                    if x - 1 in cover and all(c in lower for c in range(x, x + 3)):
                        found.append(x)
                    elif x + 5 in cover and all(c in lower for c in range(x + 2, x + 5)):
                        found.append(x)
                return found

            if spots():
                continue
            free = [
                x
                for x in inner[: len(inner) - 4]
                if all(c in cover and c in lower and c not in room.keep[j] for c in range(x, x + 5)) and (x - 1 in cover or x + 5 in cover)
            ]
            if not free:
                level.notes.append(f"{room.name}: nowhere to open a way up to its storey in row {j}")
                continue
            middle = (room.x0 + room.x1) / 2
            x = min(free, key=lambda c: abs(c + 2 - middle))
            cover.difference_update(range(x, x + 5))
        g, u = room.style["ground"], room.style["under"]
        for j in range(room.j0, room.j1):
            for x in room.cover[j]:
                level.t[S(j)][x] = g
                level.t[S(j) + 1][x] = u


def ladder(level: Level, x: int, low: int, high: int, winged: bool, wide: int = 3):
    """Ledges from the floor a hero stands on at row `low` up to one at row `high`, one
    over another: four rows apart (a plain jump), or six where he has the Wings by then."""
    step = 6 if winged else 4
    rows = list(range(low - step, high, -step))
    for stand in rows:
        for dx in range(wide):
            if level.t[stand + 1][x + dx] == AIR:
                level.t[stand + 1][x + dx] = LEDGE


def ways_up(level: Level):
    """Between a storey and the one over it, a ladder of ledges at either end of every gap
    of the upper platform that has floor under it: beside the platform's edge, so the top
    ledge is a step from it."""
    for room in level.rooms.values():
        for j in range(room.j0, room.j1):
            upper = room.cover[j]
            lower = set(room.inner()) if j + 1 == room.j1 else room.cover[j + 1]
            made = 0
            xs = sorted(room.inner())
            n = 0
            while n < len(xs):
                if xs[n] in upper:
                    n += 1
                    continue
                start = n
                while n < len(xs) and xs[n] not in upper:
                    n += 1
                g0, g1 = xs[start], xs[n - 1]
                if g1 - g0 + 1 < 3:
                    continue
                spots = []
                if g0 - 1 in upper:
                    spots.append(g0)
                if g1 + 1 in upper and (not spots or g1 - 2 > g0 + 2):
                    spots.append(g1 - 2)
                for x in spots:
                    if all(c in lower for c in range(x, x + 3)):
                        ladder(level, x, S(j + 1) - 1, S(j) - 1, room.winged)
                        made += 1
            if not made and upper:
                level.notes.append(f"{room.name}: no way up to its storey in row {j}")


def cut(level: Level, columns, rows):
    for y in rows:
        for x in columns:
            level.t[y][x] = AIR


def links(level: Level):
    """Cuts every link of the plan, and builds its gate."""
    for a, b, side, line, along, kind in plan.LINKS:
        ra, rb = level.rooms[a], level.rooms[b]
        if side == "v":
            left, right = (ra, rb) if ra.i1 == line - 1 else (rb, ra)
            door(level, left, right, along, kind)
        else:
            way_down(level, ra, rb, line, along, kind)


def door(level: Level, left: Room, right: Room, j: int, kind: str):
    """A doorway through the two columns of wall between two rooms, on a storey."""
    c0, c1 = left.x1, right.x0
    s = S(j)
    stand = s - 1
    if kind in ("door", "boss"):
        tall = 7 if kind == "boss" else 5
        cut(level, (c0, c1), range(s - tall, s))
        if kind == "boss":
            level.say("tablet", c0 - 4, stand, "The Warden keeps this door. No one is at it.")
    elif kind == "W":
        # A sill five tiles up: more than a jump, less than two.
        cut(level, (c0, c1), range(s - 9, s - 5))
    elif kind == "D":
        cut(level, (c0, c1), range(s - 4, s))
        level.mark("barrier", c0, s - 4, 4)
        level.gates.update((c0, y) for y in range(s - 4, s))
        level.links.append(((c0 - 1, stand), (c1, stand), NEED["LightDash"], True))
    elif kind == "HL":
        cut(level, (c0, c1), range(s - 4, s))
        level.mark("seal", c0, s - 4, "bolt", 1, 4)
        level.gates.update((c0, y) for y in range(s - 4, s))
        level.links.append(((c0 - 1, stand), (c1, stand), NEED["HolyLight"], True))
    elif kind == "hidden":
        # A wall that is not one: struck from the room that is on the road.
        hidden, other = (left, right) if left.kind == "hidden" else (right, left)
        wall = c1 if hidden is left else c0  # the column of the room the hero comes from
        cut(level, (c0, c1), range(s - 3, s))
        level.gates.update((wall, y) for y in range(s - 3, s))
        level.mark("secret", wall, s - 3, 1, 3, hidden.x0 + 1, hidden.y0 + 1, hidden.x1 - 1, hidden.y1 - 2, other.style["under"])
        level.links.append(((c0 - 1, stand), (c1 + 1, stand), 0, True))
    elif kind == "Gr":
        # A doorway with nothing to stand on before it but a smooth chimney ten tiles deep.
        late, other = (left, right) if left.kind == "late" else (right, left)
        cut(level, (c0, c1), range(s - 5, s))
        step = 1 if other is right else -1
        edge = other.x0 if other is right else other.x1
        wall = edge + step * 3
        for y in range(s - 6, s + 10):
            level.t[y][wall] = other.style["under"]
        for d in (1, 2):
            level.t[s - 6][edge + step * d] = other.style["under"]
            for y in range(s - 5, s + 10):
                level.t[y][edge + step * d] = AIR
        sill = (c0 if other is right else c1, stand)
        level.links.append(((edge + step, S(j + 1) - 1), sill, NEED["WallGrip"], False))
    else:
        raise SystemExit(f"no door of kind {kind}")


def way_down(level: Level, upper: Room, lower: Room, line: int, i: int, kind: str):
    """A way through the floor between a room and the one under it: a hole four tiles
    wide, and under it ledges to come up by (or none, or a gate)."""
    columns = list(hole_columns(i))
    top = Y(line) - 2  # the upper room's floor
    above, below = top - 1, S(line) - 1  # where the hero stands over it and under it
    catch = (columns[0], top + 3)  # where he stands on the ledge under the hole
    if kind == "TS":
        level.say("tablet", columns[0], below, "Something is over this stone that eyes do not find.")
        return
    if kind == "Sm":
        cut(level, columns[:3], range(top, top + 3))
        level.mark("seal", columns[0], top, "slam", 3, 2)
        level.gates.update((x, y) for x in columns[:3] for y in (top, top + 1))
        for x in columns[:2]:
            level.t[top + 5][x] = LEDGE
        level.t[top + 8][columns[2]] = level.t[top + 8][columns[2] + 1] = LEDGE
        level.links.append(((columns[0] - 1, above), (columns[0], top + 4), NEED["Smite"], True))
        return
    cut(level, columns, range(top, top + 3))
    if kind == "oneway":
        return
    if kind == "Gr":
        level.links.append(((columns[2], below), (columns[0] - 1, above), NEED["WallGrip"], False))
        return
    if kind == "Gl":
        if upper.kind == "late":
            # A wind blows up through the hole: only a glide rides it.
            level.mark("wind", columns[0], top - 2, 4, below - top + 3, 0, 150, 0, 0)
            level.links.append(((columns[1], below), (columns[0] - 1, above), NEED["Glide"], False))
        else:
            gulf(level, upper, lower, columns, top)
        return
    if kind == "W":
        for x in columns[:2]:
            level.t[top + 6][x] = LEDGE  # one ledge half way: six up to it, six up from it
        return
    for x in columns[:2]:
        level.t[top + 4][x] = LEDGE
    for x in range(columns[0] - 2, columns[0] + 2):
        level.t[top + 8][x] = LEDGE
    if kind in ("D", "hidden"):
        # A box round the hole in the upper room, shut by a veil or by a wall that is not one.
        hidden = upper if upper.kind == "hidden" else lower if lower.kind == "hidden" else None
        stone = upper.style["under"]
        x0, x1 = columns[0] - 2, columns[-1] + 2
        open_side = x1 if x1 + 6 < upper.x1 else x0
        shut_side = x0 if open_side == x1 else x1
        lid = top - (5 if kind == "D" else 4)  # a veil is four tiles high; a wall that is not one, three
        for y in range(lid, top):
            level.t[y][shut_side] = stone
        for x in range(x0, x1 + 1):
            level.t[lid][x] = stone
        rows = range(lid + 1, top)
        level.gates.update((open_side, y) for y in rows)
        out = open_side + (1 if open_side == x1 else -1)
        inside = open_side - (1 if open_side == x1 else -1)
        if kind == "D":
            level.mark("barrier", open_side, top - 4, 4)
            level.links.append(((inside, above), (out, above), NEED["LightDash"], True))
        else:
            level.mark("secret", open_side, top - 3, 1, 3, hidden.x0 + 1, hidden.y0 + 1, hidden.x1 - 1, hidden.y1 - 2, stone)
            level.links.append(((inside, above), (out, above), 0, True))
    elif kind != "door":
        raise SystemExit(f"no way down of kind {kind}")


def gulf_wall(columns) -> int:
    """The column of the wall east of a gulf's pit."""
    return columns[-1] + 5


def gulf(level: Level, upper: Room, lower: Room, columns, top: int):
    """The way down into a late room past a gulf only a glide crosses: the hole is in a pit
    walled off from the rest of the upper room, open only over the wall's top, eleven
    tiles from the nearest floor (plan_links cleared the storeys for it). A ledge under
    the wall's top to land on, a ladder up to it from the pit's floor, and the Wings from
    it to the wall's top to glide back."""
    stone = upper.style["under"]
    wall = gulf_wall(columns)
    high = S(upper.j0)
    for y in range(high, upper.y1 - 1):
        level.t[y][wall] = stone
        for x in range(upper.x0 + 1, wall):
            if level.t[y][x] == LEDGE:
                level.t[y][x] = AIR  # the ladders the storeys' gaps were given: the pit has its own
    for x in range(wall - 3, wall):
        level.t[high + 6][x] = LEDGE
    ladder(level, wall - 3, upper.y1 - 2, high + 5, False)
    for x in columns[:2]:
        level.t[top + 4][x] = LEDGE
    for x in range(columns[0] - 2, columns[0] + 2):
        level.t[top + 8][x] = LEDGE


# --- What stands in the cells -----------------------------------------------------------


def things(level: Level):
    taken = {}
    for room in level.rooms.values():
        for i in range(room.i0, room.i1 + 1):
            for j in range(room.j0, room.j1 + 1):
                taken[(i, j)] = room
    n = 0
    for kind, i, j, what in plan.THINGS:
        room = taken[(i, j)]
        x, y = X(i) + 15, S(j) - 1  # east of the cell's middle, where a way through the floor would be
        if kind == "entry":
            level.spawn = (X(i) + 4, y)
        elif kind == "shrine":
            n += 1
            level.mark("checkpoint", x, y)
            level.t[y][x + 2] = room.style["lamp"]
        elif kind == "ability":
            level.mark("ability", x, y - 1, what)
        elif kind == "upgrade":
            level.mark("upgrade", x, y, what)
        elif kind == "exit":
            level.mark("end", x, y, "The Low Gate", "|", "the garden is behind you")
    return n


def plan_things(level: Level):
    """What stands in a cell stands on floor: asks the storeys for it."""
    taken = {}
    for room in level.rooms.values():
        for i in range(room.i0, room.i1 + 1):
            for j in range(room.j0, room.j1 + 1):
                taken[(i, j)] = room
    for kind, i, j, _ in plan.THINGS:
        taken[(i, j)].keep[j].update(range(X(i) + 4, X(i) + 19))


def rewards(level: Level):
    """Something in every room off the road: a relic in a side room and in a hidden one,
    a heart or a draught in a late one."""
    late = 0
    for room in level.rooms.values():
        if room.kind == "main":
            continue
        x = room.x0 + 4 if (room.i0 + room.j0) % 2 else room.x1 - 4
        y = room.y1 - 2
        if room.kind != "late":
            # On the highest storey that has floor there: a thing off the road is worth a climb.
            y = next((S(j) - 1 for j in range(room.j0, room.j1) if x in room.cover[j]), y)
        if level.t[y][x] != AIR or level.t[y + 1][x] not in GROUND:
            x = next(c for c in room.inner() if level.t[y][c] == AIR and level.t[y + 1][c] in GROUND and level.t[y - 1][c] == AIR)
        if room.kind == "late":
            late += 1
            level.mark("upgrade", x, y, "Heart" if late % 2 else "Vial")
            level.late_cells[room.name] = (x, y)
        elif room.name != "ledge":  # the ledge over the first room holds the plan's own heart
            level.mark("relic", x, y)


# --- Checking -----------------------------------------------------------------------------


def check(level: Level, log=print) -> list[str]:
    """Plays the level through in the model. Returns what is wrong, as sentences."""
    wrong = [f"plan: {w}" for w in plan.check()] + list(level.notes)
    terrain = ["".join(row) for row in level.t]
    log("Working out where the hero can go:")
    reach = tv2_reach.Reach(terrain, level.gates, level.links, log)
    node = reach.at

    def at(cell):
        """The part of the level a cell is in; None, and a sentence, if the hero cannot be in it."""
        try:
            return node(cell)
        except SystemExit:
            wrong.append(f"the hero cannot stand at {cell}")
            return None

    reach.at = at
    start = reach.at(level.spawn)
    spots = {(m[0], m[3][0] if m[3] else ""): (m[1], m[2]) for m in level.marks if m[0] == "ability"}
    # An ability hangs a tile over where the hero stands to take it.
    wings, dash = ((x, y + 1) for x, y in (spots[("ability", "Wings")], spots[("ability", "LightDash")]))
    end = next((m[1], m[2]) for m in level.marks if m[0] == "end")
    late = set(level.late_cells.values())
    goals = {f"{m[0]} at ({m[1]}, {m[2]})": (m[1], m[2]) for m in level.marks if m[0] in ("checkpoint", "relic", "upgrade", "end") and (m[1], m[2]) not in late}

    def strands(seen, goal, have, when):
        stuck = seen - reach.leads_to(reach.at(goal), have)
        if stuck:
            cells = reach.cells_of(stuck)
            wrong.append(f"{when} the hero can strand himself in {len(cells)} cells, such as {cells[:3]}")

    have = 0
    seen = reach.reached(start, have)
    log(f"  with nothing: {len(reach.cells_of(seen))} cells in reach")
    if reach.at(wings) not in seen:
        wrong.append("with nothing the hero does not reach the Wings")
    if reach.at(dash) in seen:
        wrong.append("the hero reaches the Light Dash before the Wings")
    strands(seen, wings, have, "before the Wings")
    have |= NEED["Wings"]
    seen = reach.reached(start, have)
    log(f"  with the Wings: {len(reach.cells_of(seen))} cells in reach")
    if reach.at(dash) not in seen:
        wrong.append("with the Wings the hero does not reach the Light Dash")
    strands(seen, dash, have, "before the Light Dash")
    have |= NEED["LightDash"]
    seen = reach.reached(start, have)
    log(f"  with the Wings and the Light Dash: {len(reach.cells_of(seen))} cells in reach")
    for name, cell in goals.items():
        if reach.at(cell) not in seen:
            wrong.append(f"the hero never reaches the {name}")
    strands(seen, end, have, "with the Wings and the Light Dash")
    for name, cell in level.late_cells.items():
        if reach.at(cell) in seen:
            wrong.append(f"the late room {name} is open to a hero with only the Wings and the Light Dash")
    # Nothing he can reach is outside the rooms.
    out = [c for c in reach.cells_of(seen) if level.room_at[c[1]][c[0]] is None]
    if out:
        wrong.append(f"the hero can get out of the rooms: {len(out)} cells, such as {out[:3]}")
    everything = sum(NEED.values())
    seen = reach.reached(start, everything)
    log(f"  with everything: {len(reach.cells_of(seen))} cells in reach")
    for name, cell in level.late_cells.items():
        if reach.at(cell) not in seen and name != "l6":  # l6 waits for true sight, which this standard has not
            wrong.append(f"the late room {name} is shut even to a hero with everything")
    strands(seen, end, everything, "with everything")
    floating = [f"the {m[0]} at ({m[1]}, {m[2]}) stands on nothing" for m in level.marks if m[0] in ("sign", "checkpoint") and level.t[m[2] + 1][m[1]] not in GROUND]
    return wrong + floating


# --- Building and writing ---------------------------------------------------------------

HEADER = """# The Paradise level of Tiles V2, written by tools/tiles_v2/make_paradise_map.py from
# tools/tiles_v2/tv2_paradise_plan.py (edit those, not this): seven zones from the Waking
# Hollow to the Low Gate. The terrain grid, a blank line, the wall grid; the legend is in
# tiles_v2.json. `@mark`, `@view` and `@shot` are as in the world's map.
"""


def build(log=print) -> Level:
    level = Level()
    shells(level)
    skin(level)
    plan_links(level)
    plan_things(level)
    platforms(level)
    ways_up(level)
    links(level)
    shrines = things(level)
    rewards(level)
    tv2_paradise_dress.dress(level, sys.modules[__name__], log)
    log(f"  {len(level.rooms)} rooms, {len(plan.LINKS)} links, {shrines} shrines")
    return level


def write(level: Level, path: str):
    level.t[level.spawn[1]][level.spawn[0]] = SPAWN_CHAR
    lines = [HEADER.rstrip("\n"), f"@surface {HEIGHT - 2}", "@region paradise 0"]
    lines += [f"@view {name} {x:g} {y:g}" for name, x, y in level.views]
    lines += [f"@shot {name} {view} {hour:g}" for name, view, hour in level.shots]
    lines += [f"@mark {kind} {x} {y} {' '.join(rest)}".rstrip() for kind, x, y, rest in level.marks]
    lines += ["".join(row) for row in level.t]
    lines.append("")
    lines += ["".join(row) for row in level.walls]
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=PARADISE_MAP, help="where to write (default: Assets/Art/TilesV2/paradise_map.txt)")
    parser.add_argument("--check", action="store_true", help="play the level through in the model and fail if it cannot be")
    args = parser.parse_args(argv)
    level = build()
    wrong = check(level) if args.check else []
    write(level, args.out)
    print(f"Paradise: {WIDTH} x {HEIGHT} tiles, {len(level.marks)} marks, {len(level.views)} views -> {args.out}")
    if wrong:
        raise SystemExit("The level cannot be played through:\n  " + "\n  ".join(wrong))


if __name__ == "__main__":
    main()
