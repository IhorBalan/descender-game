#!/usr/bin/env python3
"""Works out where the hero can get to on a Tiles V2 map, and with which of his
abilities: what make_world_map.py checks its world with. Not a script.

The model is cautious: what it calls reachable the hero can reach with room to
spare, so a world it passes can be played through; what it calls out of reach he
may still get to with a clever jump. He is a box 14 x 31 px who walks (a step of
one tile up or down counts as walking), swims, falls, and jumps along the arcs
his tuning gives (PlayerTuning: 72 px high in 0.45 s, down in 0.36 s, 100 px/s
across, of which the model allows 85, plus the reach of one dash). The Wings add
a second jump of 56 px at the top of the first, the Glide a fall of 45 px/s. The
things only an ability passes (a light barrier, a seal, a smooth shaft to climb
by Wall Grip) are links the map's maker names.
"""
from __future__ import annotations

import math

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_liquid

ABILITIES = ["LightDash", "WallGrip", "HolyLight", "Glide", "Smite", "Wings"]  # the order the world hands them out in
BIT = {name: 1 << i for i, name in enumerate(ABILITIES)}

DT = 1.0 / 30.0
HALF, TALL = 7, 31  # the hero's box, px
RUN = 85.0  # px/s across in the air: 100 in the game
DASH = 30.0  # px a dash adds to a jump: 36 in the game
JUMP, RISE, FALL_TIME = 72.0, 0.45, 0.36
SECOND, GLIDE, MAX_FALL = 56.0, 45.0, 280.0
G_UP, G_DOWN = 2 * JUMP / RISE**2, 2 * JUMP / FALL_TIME**2


def arc(apex: float, second: bool, glide: bool, lowest: float) -> tuple[list[float], int]:
    """The hero's height (px over where he left the ground) every DT of a jump that
    tops out at `apex`, down to `lowest`; and the sample it tops out at."""
    heights, t, v, y = [0.0], 0.0, math.sqrt(2 * G_UP * apex), 0.0
    jumped = not second
    while True:
        t += DT
        if v > 0:
            y += v * DT - 0.5 * G_UP * DT * DT
            v -= G_UP * DT
            if v <= 0 and not jumped:
                v, jumped = math.sqrt(2 * G_UP * SECOND), True
        elif glide:
            y -= GLIDE * DT
        else:
            v = max(v - G_DOWN * DT, -MAX_FALL)
            y += v * DT
        heights.append(y)
        if y < lowest:
            break
    top = max(range(len(heights)), key=lambda k: heights[k])
    return heights, top


class Profile:
    """A way of jumping: what it needs, how far it looks for somewhere to land, its arcs."""

    def __init__(self, name: str, need: int, across: int, up: int, down: int, apexes, second=False, glide=False, edge_only=False):
        self.name, self.need, self.across, self.up, self.down, self.edge_only = name, need, across, up, down, edge_only
        self.arcs = [arc(a, second, glide, -TILE * (down + 1)) for a in apexes]


PROFILES = [
    Profile("jump", 0, 6, 4, 10, (JUMP, 52.0, 36.0, 22.0)),
    Profile("wings", BIT["Wings"], 9, 7, 10, (JUMP,), second=True),
    Profile("glide", BIT["Glide"], 14, 4, 8, (JUMP,), glide=True, edge_only=True),
]


class Reach:
    def __init__(self, terrain: list[str], gates: set, links: list, log=print):
        self.h, self.w = len(terrain), len(terrain[0])
        liquids = {entry["key"]: entry for entry in tv2_liquid.LIQUIDS}
        solid = {c for c, name in TERRAIN_CHARS.items() if ":" not in name}
        hazard = {c for c, key in LIQUID_CHARS.items() if liquids[key]["hazard"]}
        swim = {c for c, key in LIQUID_CHARS.items() if liquids[key]["swim"]}
        w, h = self.w, self.h
        # 0 free, 1 solid (or shut), 2 a liquid to swim in, 3 a liquid that hurts: no way through, nothing to stand on.
        self.cell = bytearray(w * h)
        self.ledge = bytearray(w * h)
        for y, row in enumerate(terrain):
            for x, c in enumerate(row):
                if c in solid or (x, y) in gates:
                    self.cell[y * w + x] = 1
                elif c in hazard:
                    self.cell[y * w + x] = 3
                elif c in swim:
                    self.cell[y * w + x] = 2
                elif c in LEDGE_CHARS:
                    self.ledge[y * w + x] = 1
        self.links = links
        self.log = log
        self.build()

    # --- The cells -----------------------------------------------------------------

    def free(self, x: int, y: int) -> bool:
        return 0 <= x < self.w and 0 <= y < self.h and self.cell[y * self.w + x] in (0, 2)

    def stands(self, x: int, y: int) -> bool:
        """Whether the hero can stand with his feet in a cell."""
        w = self.w
        if not (0 <= x < w and 1 <= y < self.h - 1):
            return False
        i = y * w + x
        return self.cell[i] == 0 and self.cell[i - w] in (0, 2) and (self.cell[i + w] == 1 or self.ledge[i + w] == 1)

    def swims(self, x: int, y: int) -> bool:
        return 0 <= x < self.w and 0 <= y < self.h and self.cell[y * self.w + x] == 2

    def find(self, i: int) -> int:
        parent = self.parent
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def join(self, a: int, b: int):
        a, b = self.find(a), self.find(b)
        if a != b:
            self.parent[a] = b

    def clear(self, x0: float, feet: float) -> bool:
        """Whether the hero's box fits with its middle at x0 and its feet at `feet` (px, y down)."""
        w, cell = self.w, self.cell
        for cy in range(int((feet - TALL) // TILE), int((feet - 0.01) // TILE) + 1):
            if cy < 0 or cy >= self.h:
                return False
            for cx in range(int((x0 - HALF) // TILE), int((x0 + HALF - 0.01) // TILE) + 1):
                if cx < 0 or cx >= w or cell[cy * w + cx] in (1, 3):
                    return False
        return True

    # --- Jumps ---------------------------------------------------------------------

    def jumps(self, profile: Profile, x: int, y: int, tx: int, ty: int) -> bool:
        """Whether a jump of a profile takes the hero from standing in (x, y) to standing in (tx, ty)."""
        rise = (y - ty) * TILE
        distance = abs(tx - x) * TILE
        side = 1 if tx > x else -1
        x0, feet0 = x * TILE + TILE / 2, (y + 1) * TILE
        for heights, top in profile.arcs:
            if heights[top] < rise + 3:
                continue
            land = next((k for k in range(top, len(heights)) if heights[k] <= rise), None)
            if land is None:
                continue
            late = next((k for k in range(top + 1) if heights[k] >= rise + 3), top) if rise > 0 else 0
            for start, end in ((0, land), (late, land), (0, min(land, int(math.ceil(distance / RUN / DT))))):
                if end <= start and distance > 0:
                    continue
                if distance > RUN * (end - start) * DT + DASH:
                    continue
                for k in range(1, land + 1):
                    along = 0.0 if k <= start else 1.0 if k >= end else (k - start) / (end - start)
                    height = rise if k == land else heights[k]
                    if not self.clear(x0 + side * distance * along, feet0 - height):
                        break
                else:
                    return True
        return False

    # --- The graph -----------------------------------------------------------------

    def build(self):
        w, h = self.w, self.h
        stand = [self.stands(x, y) for y in range(h) for x in range(w)]
        swim = [c == 2 for c in self.cell]
        self.node = [a or b for a, b in zip(stand, swim)]
        self.parent = list(range(w * h))
        for y in range(1, h - 1):
            for x in range(w - 1):
                i = y * w + x
                if stand[i]:
                    # A step of a tile up or down is walking; so is wading into a liquid.
                    for dy in (-1, 0, 1):
                        j = i + 1 + dy * w
                        if stand[j] and self.free(x if dy < 0 else x + 1, y - 2 if dy < 0 else y - 1 + min(dy, 0)):
                            self.join(i, j)
                if swim[i]:
                    if swim[i + 1]:
                        self.join(i, i + 1)
                    if swim[i + w]:
                        self.join(i, i + w)
        for y in range(3, h - 1):
            for x in range(1, w - 1):
                i = y * w + x
                if not swim[i] or self.cell[i - w] == 2:
                    continue
                # Out of a liquid from its surface: onto a bank up to two tiles over it.
                for nx in (x - 1, x + 1):
                    for ty in range(y - 2, y + 1):
                        if stand[ty * w + nx] and all(self.free(x, yy) for yy in range(ty - 1, y)):
                            self.join(i, ty * w + nx)
        self.stand, self.swim = stand, swim
        self.edges: dict[int, dict[int, list[int]]] = {}  # component -> component -> the sets of abilities known to do it
        self.falls()
        for a, b, need, both in self.links:
            ia, ib = a[1] * w + a[0], b[1] * w + b[0]
            if not self.node[ia] or not self.node[ib]:
                raise SystemExit(f"a link ends in a cell the hero cannot be in: {a} -> {b}")
            self.edge(ia, ib, need)
            if both:
                self.edge(ib, ia, need)
        for profile in PROFILES:
            count = self.leaps(profile)
            self.log(f"  {profile.name}: {count} ways found")

    def edge(self, i: int, j: int, need: int):
        a, b = self.find(i), self.find(j)
        if a == b:
            return
        ways = self.edges.setdefault(a, {}).setdefault(b, [])
        if not any(way & ~need == 0 for way in ways):
            ways[:] = [way for way in ways if need & ~way != 0] + [need]

    def falls(self):
        """Stepping off an edge, or dropping through a ledge, and falling straight down."""
        w, h = self.w, self.h
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                i = y * w + x
                if not self.stand[i]:
                    continue
                starts = [(nx, y) for nx in (x - 1, x + 1) if self.free(nx, y) and self.free(nx, y - 1) and not self.node[y * w + nx]]
                if self.ledge[i + w]:
                    starts.append((x, y + 1))
                for nx, yy in starts:
                    while yy + 1 < h and self.cell[(yy + 1) * w + nx] == 0 and not self.ledge[(yy + 1) * w + nx]:
                        yy += 1
                    if yy + 1 < h and self.cell[(yy + 1) * w + nx] == 2:
                        self.edge(i, (yy + 1) * w + nx, 0)
                    elif self.stand[yy * w + nx]:
                        self.edge(i, yy * w + nx, 0)

    def leaps(self, profile: Profile) -> int:
        w, h = self.w, self.h
        found = 0
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                i = y * w + x
                if not self.stand[i]:
                    continue
                if profile.edge_only and self.stand[i - 1] and self.stand[i + 1]:
                    continue
                a = self.find(i)
                known = self.edges.setdefault(a, {})
                for ty in range(max(y - profile.up, 1), min(y + profile.down, h - 2) + 1):
                    base = ty * w
                    for tx in range(max(x - profile.across, 1), min(x + profile.across, w - 2) + 1):
                        j = base + tx
                        if not self.stand[j]:
                            continue
                        b = self.find(j)
                        if b == a or (b in known and any(way & ~profile.need == 0 for way in known[b])):
                            continue
                        if profile.edge_only and abs(tx - x) < 6:
                            continue
                        if self.jumps(profile, x, y, tx, ty):
                            self.edge(i, j, profile.need)
                            found += 1
        return found

    # --- Questions -----------------------------------------------------------------

    def at(self, cell) -> int:
        i = cell[1] * self.w + cell[0]
        if not self.node[i]:
            raise SystemExit(f"the hero cannot be in the cell {cell}")
        return self.find(i)

    def reached(self, start: int, have: int) -> set[int]:
        """Every component the hero gets to from one with a set of abilities."""
        seen, todo = {start}, [start]
        while todo:
            a = todo.pop()
            for b, ways in self.edges.get(a, {}).items():
                if b not in seen and any(way & ~have == 0 for way in ways):
                    seen.add(b)
                    todo.append(b)
        return seen

    def leads_to(self, goal: int, have: int) -> set[int]:
        """Every component from which the hero gets to one with a set of abilities."""
        back: dict[int, list[int]] = {}
        for a, known in self.edges.items():
            for b, ways in known.items():
                if any(way & ~have == 0 for way in ways):
                    back.setdefault(b, []).append(a)
        seen, todo = {goal}, [goal]
        while todo:
            b = todo.pop()
            for a in back.get(b, ()):
                if a not in seen:
                    seen.add(a)
                    todo.append(a)
        return seen

    def cells_of(self, components: set[int]) -> list[tuple[int, int]]:
        w = self.w
        return [(i % w, i // w) for i in range(w * self.h) if self.node[i] and self.find(i) in components]


def journey(reach: Reach, spawn, shrines: dict, goals: dict, log=print) -> list[str]:
    """Plays the world through in the model: from the start with nothing, taking each
    ability as its shrine comes within reach. Returns what is wrong, as sentences:
    an ability that comes out of order, a place he could walk into and never
    progress from, a goal he never reaches."""
    wrong: list[str] = []
    start = reach.at(spawn)
    have, order = 0, []
    while True:
        seen = reach.reached(start, have)
        new = [name for name in ABILITIES if not have & BIT[name] and name in shrines and reach.at(shrines[name]) in seen]
        log(f"  with [{', '.join(order) or 'nothing'}]: {len(reach.cells_of(seen))} cells in reach; next: {', '.join(new) or 'none'}")
        if not new:
            break
        if len(new) > 1:
            wrong.append(f"with [{', '.join(order)}] the hero finds more than one ability: {', '.join(new)}")
        expected = next(name for name in ABILITIES if not have & BIT[name])
        if new[0] != expected:
            wrong.append(f"the hero finds {new[0]} before {expected}")
        # Wherever he can have walked to so far, the next shrine must still be within reach.
        goal = reach.at(shrines[new[0]])
        stuck = seen - reach.leads_to(goal, have)
        if stuck:
            cells = reach.cells_of(stuck)
            wrong.append(f"before {new[0]} the hero can strand himself in {len(cells)} cells, such as {cells[:3]}")
        for name in new:
            have |= BIT[name]
            order.append(name)
    missing = [name for name in ABILITIES if name in shrines and not have & BIT[name]]
    if missing:
        wrong.append(f"the hero never finds: {', '.join(missing)}")
    seen = reach.reached(start, have)
    for name, cell in goals.items():
        if reach.at(cell) not in seen:
            wrong.append(f"the hero never reaches {name} at {cell}")
    stuck = seen - reach.leads_to(start, have)
    if stuck:
        cells = reach.cells_of(stuck)
        wrong.append(f"with every ability the hero still cannot get back to the start from {len(cells)} cells, such as {cells[:3]}")
    return wrong
