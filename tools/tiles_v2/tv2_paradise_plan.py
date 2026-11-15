#!/usr/bin/env python3
"""The plan of the Paradise level: the wireframe ("Paradise (Heaven) wireframe, draft 2")
read off as data. Seven zones, their rooms on a grid of cells, and what joins the rooms.
Not a script: `make_paradise_map.py` builds the level from it.

A cell is one square of the wireframe; `make_paradise_map.CELL` says how many tiles it
is. Columns count from the wireframe's left (the first room is in column 2), rows from
its top (the Crown Vine's top is row 0).

The story the zones carry is `docs/design/paradise.md`'s: the hero wakes in the Waking
Hollow with nothing, finds the Wings at the top of the Crown Vine and the Light Dash on
the Terraces, and leaves through the Low Gate. What a later part of the game gives
(Wall Grip, Holy Light, Glide, Smite, true sight) opens the `late` rooms: here they are
seen and not entered.
"""
from __future__ import annotations

ZONES = {
    1: ("hollow", "The Waking Hollow"),
    2: ("orchard", "The Orchard Under the Canopy"),
    3: ("vines", "The Great Vines"),
    4: ("fields", "The Tallied Fields"),
    5: ("terraces", "The Terraces"),
    6: ("making", "The Making-house Approach"),
    7: ("gate", "The Low Gate"),
}

# name: (zone, kind, first column, first row, last column, last row). Kinds: `main` the
# road, `opt` a room off it, `late` one an ability from later opens, `hidden` one behind a
# wall that is not one.
ROOMS = {
    # 1. The Waking Hollow
    "hollow": (1, "main", 2, 20, 4, 22),
    "rising": (1, "main", 5, 16, 6, 22),
    "leafrun": (1, "main", 7, 16, 10, 17),
    "ledge": (1, "opt", 2, 18, 3, 19),
    "roots": (1, "opt", 3, 23, 5, 24),
    "h1": (1, "hidden", 2, 23, 2, 24),
    "l1": (1, "late", 3, 25, 3, 25),
    # 2. The Orchard Under the Canopy
    "orchard": (2, "main", 11, 16, 14, 18),
    "undergrove": (2, "opt", 12, 19, 14, 21),
    "rootway": (2, "opt", 14, 22, 20, 23),
    "rows": (2, "main", 15, 17, 18, 18),
    "canopy": (2, "opt", 15, 15, 19, 16),
    "l2": (2, "late", 20, 15, 20, 15),
    "grove": (2, "main", 19, 17, 20, 21),
    "lantern": (2, "main", 21, 12, 22, 21),
    "crownlet": (2, "opt", 21, 10, 22, 11),
    "eastwalk": (2, "main", 23, 14, 25, 15),
    "h2": (2, "hidden", 23, 19, 24, 21),
    # 3. The Great Vines
    "vineway": (3, "main", 26, 14, 31, 15),
    "underway": (3, "opt", 26, 16, 31, 17),
    "trunk": (3, "main", 32, 8, 33, 15),
    "nest": (3, "opt", 32, 6, 33, 7),
    "bough": (3, "main", 34, 8, 37, 9),
    "h3": (3, "hidden", 34, 10, 35, 11),
    "tangle": (3, "main", 38, 9, 41, 12),
    "hollowlog": (3, "opt", 36, 13, 41, 14),
    "foot": (3, "main", 42, 11, 44, 12),
    "rootcave": (3, "opt", 42, 13, 44, 15),
    "crownvine": (3, "main", 45, 0, 47, 15),
    "l3": (3, "late", 44, 2, 44, 5),
    # 4. The Tallied Fields
    "descent": (4, "main", 48, 0, 50, 7),
    "landing": (4, "main", 48, 8, 52, 9),
    "meadow": (4, "main", 53, 8, 55, 9),
    "hayloft": (4, "opt", 53, 5, 55, 7),
    "burrow": (4, "opt", 53, 10, 56, 11),
    "h4": (4, "hidden", 57, 10, 59, 11),
    "fields": (4, "main", 56, 7, 60, 9),
    "perch": (4, "opt", 58, 6, 58, 6),
    "sundial": (4, "main", 61, 8, 65, 10),
    "l4": (4, "late", 61, 4, 64, 7),
    # 5. The Terraces
    "stairfoot": (5, "main", 66, 8, 68, 11),
    "sidegarden": (5, "opt", 66, 6, 67, 7),
    "firstterrace": (5, "main", 68, 5, 69, 7),
    "alcove": (5, "opt", 68, 3, 69, 4),
    "climb": (5, "main", 70, 1, 71, 7),
    "h5": (5, "hidden", 72, 5, 73, 6),
    "l5": (5, "late", 72, 3, 73, 4),
    "promenade": (5, "main", 72, 1, 78, 2),
    "wheel": (5, "main", 79, 0, 81, 2),
    "arcade": (5, "main", 82, 1, 86, 2),
    "cistern": (5, "opt", 82, 3, 85, 4),
    # 6. The Making-house Approach
    "pipewalk": (6, "main", 87, 1, 91, 2),
    "boiler": (6, "opt", 87, 3, 90, 5),
    "shaft": (6, "main", 92, 1, 93, 6),
    "h6": (6, "hidden", 92, 7, 94, 8),
    "works": (6, "main", 94, 5, 97, 6),
    "l6": (6, "late", 95, 3, 96, 4),
    "drain": (6, "opt", 95, 7, 98, 10),
    "door": (6, "main", 98, 5, 101, 6),
    # 7. The Low Gate
    "ledgehead": (7, "main", 102, 5, 104, 6),
    "cliffs": (7, "main", 105, 6, 107, 15),
    "eyrie": (7, "opt", 102, 14, 104, 16),
    "l7": (7, "late", 102, 17, 105, 18),
    "bottom": (7, "main", 108, 14, 110, 16),
    "h7": (7, "hidden", 109, 12, 110, 13),
    "lastclimb": (7, "main", 111, 7, 112, 16),
    "approach": (7, "main", 113, 7, 117, 8),
    "niche": (7, "opt", 114, 9, 115, 9),
    "arena": (7, "main", 118, 6, 120, 8),
}

# What joins two rooms: (room, room, side, line, along, kind). Side `v`: a door in the
# wall on the line between column `line - 1` and column `line`, in row `along`. Side `h`:
# a way through the floor on the line between row `line - 1` and row `line`, in column
# `along`; the first room named is the upper one. Kinds: `door` open; `oneway` down only;
# `hidden` a wall that is not one; `boss` the door of the arena; and a gate by ability:
# `W` Wings, `D` Light Dash (both found here), `Gr` Wall Grip, `HL` Holy Light, `Gl`
# Glide, `Sm` Smite, `TS` true sight (all from later: the gate of a late room).
LINKS = [
    # 1
    ("hollow", "rising", "v", 5, 21, "door"),
    ("hollow", "roots", "h", 23, 3, "door"),
    ("rising", "leafrun", "v", 7, 17, "door"),
    ("ledge", "hollow", "h", 20, 3, "W"),
    ("h1", "roots", "v", 3, 24, "hidden"),
    ("roots", "l1", "h", 25, 3, "Sm"),
    ("leafrun", "orchard", "v", 11, 17, "door"),
    # 2
    ("orchard", "undergrove", "h", 19, 13, "oneway"),
    ("undergrove", "rootway", "h", 22, 14, "door"),
    ("grove", "rootway", "h", 22, 20, "door"),
    ("orchard", "rows", "v", 15, 18, "door"),
    ("rows", "grove", "v", 19, 18, "door"),
    ("canopy", "rows", "h", 17, 17, "door"),
    ("canopy", "l2", "v", 20, 15, "HL"),
    ("grove", "lantern", "v", 21, 19, "door"),
    ("crownlet", "lantern", "h", 12, 21, "W"),
    ("lantern", "eastwalk", "v", 23, 15, "door"),
    ("lantern", "h2", "v", 23, 20, "hidden"),
    ("eastwalk", "vineway", "v", 26, 15, "door"),
    # 3
    ("vineway", "underway", "h", 16, 29, "door"),
    ("vineway", "trunk", "v", 32, 15, "door"),
    ("nest", "trunk", "h", 8, 33, "W"),
    ("trunk", "bough", "v", 34, 9, "door"),
    ("bough", "h3", "h", 10, 34, "hidden"),
    ("bough", "tangle", "v", 38, 9, "door"),
    ("tangle", "hollowlog", "h", 13, 40, "door"),
    ("tangle", "foot", "v", 42, 12, "door"),
    ("foot", "rootcave", "h", 13, 43, "door"),
    ("foot", "crownvine", "v", 45, 12, "door"),
    ("rootcave", "crownvine", "v", 45, 14, "door"),
    ("l3", "crownvine", "v", 45, 4, "Gr"),
    # An open door on the wireframe; built as a sill for the Wings, since the hero climbing the vine passes it on
    # his way to them, and the fields beyond let no one back who has not got them.
    ("crownvine", "descent", "v", 48, 4, "W"),
    ("crownvine", "landing", "v", 48, 9, "W"),  # the wireframe's shortcut back into the vine: it opens from the fields' side
    # 4
    ("descent", "landing", "h", 8, 49, "oneway"),
    ("landing", "meadow", "v", 53, 9, "W"),
    ("hayloft", "meadow", "h", 8, 54, "W"),
    ("meadow", "burrow", "h", 10, 54, "door"),
    ("burrow", "h4", "v", 57, 11, "hidden"),
    ("hayloft", "fields", "v", 56, 7, "W"),
    ("meadow", "fields", "v", 56, 9, "door"),
    ("perch", "fields", "h", 7, 58, "W"),
    ("fields", "sundial", "v", 61, 9, "door"),
    ("l4", "sundial", "h", 8, 63, "Gl"),
    ("sundial", "stairfoot", "v", 66, 9, "door"),
    # 5
    ("sidegarden", "stairfoot", "h", 8, 67, "oneway"),
    ("firstterrace", "stairfoot", "h", 8, 68, "W"),
    ("sidegarden", "firstterrace", "v", 68, 7, "D"),
    ("alcove", "firstterrace", "h", 5, 69, "D"),
    ("alcove", "climb", "v", 70, 4, "W"),
    ("firstterrace", "climb", "v", 70, 6, "W"),
    ("climb", "promenade", "v", 72, 2, "door"),
    ("climb", "h5", "v", 72, 6, "hidden"),
    ("promenade", "l5", "h", 3, 73, "Sm"),
    ("promenade", "wheel", "v", 79, 2, "door"),
    ("wheel", "arcade", "v", 82, 2, "D"),
    ("arcade", "cistern", "h", 3, 84, "door"),
    ("arcade", "pipewalk", "v", 87, 2, "door"),
    # 6
    ("pipewalk", "boiler", "h", 3, 89, "door"),
    ("pipewalk", "shaft", "v", 92, 2, "door"),
    ("shaft", "h6", "h", 7, 93, "hidden"),
    ("shaft", "works", "v", 94, 6, "door"),
    ("l6", "works", "h", 5, 96, "TS"),
    ("works", "drain", "h", 7, 96, "door"),
    ("works", "door", "v", 98, 6, "door"),
    ("door", "drain", "h", 7, 98, "Gr"),
    ("door", "ledgehead", "v", 102, 6, "door"),
    # 7
    ("ledgehead", "cliffs", "v", 105, 6, "door"),
    ("eyrie", "cliffs", "v", 105, 15, "W"),  # the wireframe's shortcut out of the eyrie
    ("eyrie", "l7", "h", 17, 103, "Gl"),
    ("cliffs", "bottom", "v", 108, 15, "door"),
    ("h7", "bottom", "h", 14, 110, "hidden"),
    ("bottom", "lastclimb", "v", 111, 15, "door"),
    ("lastclimb", "approach", "v", 113, 8, "door"),
    ("approach", "niche", "h", 9, 115, "W"),
    ("approach", "arena", "v", 118, 8, "boss"),
]

# What stands in a cell: (kind, column, row, what). The hero comes in at `entry` and goes
# out at `exit`. A `shrine` is a place to rest.
THINGS = [
    ("entry", 2, 21, ""),
    ("shrine", 7, 17, ""),
    ("upgrade", 2, 19, "Heart"),
    ("shrine", 12, 18, ""),
    ("shrine", 22, 14, ""),
    ("shrine", 23, 15, ""),
    ("shrine", 34, 9, ""),
    ("shrine", 43, 12, ""),
    ("shrine", 46, 0, ""),
    ("ability", 47, 0, "Wings"),
    ("shrine", 59, 9, ""),
    ("shrine", 63, 10, ""),
    ("npc", 68, 4, "gathered"),
    ("shrine", 76, 1, ""),
    ("shrine", 80, 0, ""),
    ("ability", 80, 1, "LightDash"),
    ("shrine", 89, 1, ""),
    ("shrine", 96, 6, ""),
    ("encounter", 98, 6, ""),
    ("shrine", 102, 6, ""),
    ("shrine", 111, 12, ""),
    ("shrine", 116, 8, ""),
    ("boss", 120, 6, ""),
    ("exit", 119, 8, ""),
]


def check() -> list[str]:
    """What is wrong with the plan as data: rooms that overlap, a link between rooms that do not touch where it says."""
    wrong = []
    taken = {}
    for name, (_, _, i0, j0, i1, j1) in ROOMS.items():
        for i in range(i0, i1 + 1):
            for j in range(j0, j1 + 1):
                if (i, j) in taken:
                    wrong.append(f"{name} and {taken[(i, j)]} both have cell ({i}, {j})")
                taken[(i, j)] = name
    for a, b, side, line, along, kind in LINKS:
        first = (line - 1, along) if side == "v" else (along, line - 1)
        second = (line, along) if side == "v" else (along, line)
        if {taken.get(first), taken.get(second)} != {a, b}:
            wrong.append(f"{a} - {b} ({kind}): the cells either side of it are {taken.get(first)} and {taken.get(second)}")
        elif side == "h" and taken.get(first) != a:
            wrong.append(f"{a} - {b}: the upper room is named second")
    for kind, i, j, _ in THINGS:
        if (i, j) not in taken:
            wrong.append(f"{kind} at ({i}, {j}) is in no room")
    return wrong


if __name__ == "__main__":
    problems = check()
    print("\n".join(problems) or f"{len(ROOMS)} rooms, {len(LINKS)} links, {len(THINGS)} things: nothing wrong")
