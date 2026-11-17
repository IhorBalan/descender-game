#!/usr/bin/env python3
"""Dresses the Paradise level: what make_paradise_map.py's rooms stand full of, zone by
zone, and what lights them. Not a script: `dress` is called by `make_paradise_map.build`
once the rooms, their platforms and what joins them are made.

Nothing here is in the hero's way. Pictures stand behind him (tv2_eden.py, tv2_heaven.py
and the Earth's own, tv2_earth.py); lamps and falling water are passed through; the
cloud laid round the rooms is outside them. The model plays the level after this, so
what would be in the way is found.

What each zone is, from `docs/design/paradise.md`:
1 the Waking Hollow, great leaves at sunrise; 2 the Orchard under its canopy, lit by its
own fruit, the trees in exact rows; 3 the Great Vines; 4 the Tallied Fields, wheat in
counted rows under the full sun; 5 the Terraces, marble and gold, the Wheel; 6 the
Making-house Approach, pearl and pipes; 7 the Low Gate, silver cliffs and the ring.
"""
from __future__ import annotations

import math

import tv2_paradise_plan as plan
from tv2_base import *  # noqa: F401,F403

IVY = {1: "green", 2: "teal", 3: "green", 4: "green", 5: "green", 6: "pale", 7: "frost"}
# What stands along a zone's floors, in turn: (keys, tiles between two of them).
ALONG = {
    1: [(["eden_leaf_0", "eden_leaf_1", "eden_leaf_2", "eden_leaf_3"], 13), (["heaven_blossom_0", "heaven_blossom_1", "heaven_blossom_2", "heaven_blossom_3"], 17), (["heaven_flowers_0", "heaven_flowers_1", "heaven_flowers_2", "heaven_flowers_3"], 6)],
    2: [(["eden_teal_tree_0", "eden_teal_tree_1", "eden_teal_tree_2"], 8)],
    3: [(["eden_vine_0", "eden_vine_1", "eden_vine_2"], 13), (["eden_leaf_1", "eden_leaf_3", "earth_shrub_0", "earth_shrub_1"], 10), (["eden_ruin_0", "eden_sword", "eden_ruin_1"], 37)],
    4: [(["eden_wheat_0", "eden_wheat_1", "eden_wheat_2", "eden_wheat_3"], 6), (["heaven_gold_0", "heaven_gold_1", "heaven_gold_2", "heaven_gold_3"], 27), (["earth_haystack_0", "earth_fence_0", "earth_haystack_1", "earth_fence_1"], 19), (["eden_sword", "eden_ruin_2", "eden_knight", "eden_ruin_0", "eden_knight_1"], 43)],
    5: [(["heaven_urn", "heaven_hedge_1", "heaven_flowers_0", "heaven_bench", "heaven_hedge_2", "earth_iron_fence_0", "heaven_flowers_2", "eden_ruin_1"], 7), (["heaven_blossom_1", "heaven_gold_2"], 31), (["eden_knight", "eden_buttress_0", "eden_ruin_0", "eden_knight_1", "eden_buttress_1", "eden_ruin_2"], 37)],
    6: [(["eden_gear", "eden_pipe_v_12", "eden_piston", "eden_pipe_v_6", "eden_pipe_v_12", "eden_piston"], 11), (["eden_ruin_1", "eden_knight", "eden_ruin_2", "eden_sword", "eden_knight_1"], 17)],
    7: [(["eden_crag_0", "eden_crag_1", "eden_crag_2"], 9), (["eden_sword", "eden_ruin_0", "earth_dead_tree_0", "eden_ruin_2", "eden_sword", "earth_dead_tree_1", "eden_knight", "eden_knight_1"], 13), (["eden_buttress_0", "eden_buttress_1"], 41)],
}
LAMPS = "JåT*^ÅÝ"  # what stands on a floor and gives light
SAID = {
    "voice": "Go back. You are not finished. Go back and lie down.",
    "mereth": "Tobiah? He went down to the Low Gate, talking the whole way. Mind the fruit. Every one is counted.",
    "token": "A harvest token. Someone has scratched a tally into it: forty, less three.",
    "ledger": "A page of the ledger. Gathered this season: 40. Short: 3.",
    "ithuriel": "I was set to guard this road. I sat down instead. Go on. I will not look.",
    "stone": "A child's tally stone, worn smooth where a thumb held it.",
    "card": "A card, in a careful hand: What is pressed is not lost.",
    "glove": "A maker's glove, white, too large for any hand you know.",
    "gathered": "We are walking to the door. It is a good door. Walk with us.",
    "making": "You left your other half on the table. ... Keep it.",
    "shard": "A shard of a basin, still warm. It hums when you hold it.",
    "gate": "This is the last door that is mine. Past it I cannot call you back.",
}


def floors_of(level, room, m, lamps: bool = False):
    """The runs of floor a hero can stand on in a room: (row he stands in, first column,
    last column). A storey's floor, or a hill a tile over it. With `lamps`, a lamp that
    stands on a floor does not end its run."""
    runs = []
    free = {m.AIR} | (set(LAMPS) if lamps else set())
    for j in room.storeys:
        for y in (m.S(j) - 1, m.S(j) - 2):
            x = room.x0 + 1
            while x < room.x1:
                if level.t[y + 1][x] in m.GROUND and level.t[y][x] in free:
                    start = x
                    while x < room.x1 and level.t[y + 1][x] in m.GROUND and level.t[y][x] in free:
                        x += 1
                    if y == m.S(j) - 1 or x - start >= 4:
                        runs.append((y, start, x - 1))
                else:
                    x += 1
    return runs


class Dresser:
    def __init__(self, level, m):
        self.level, self.m = level, m
        self.busy = {(mk[1], mk[2]) for mk in level.marks if mk[0] != "zone"}  # cells something of the game's stands in
        self.count = 0

    def clear(self, x: int, y: int, wide: int = 2) -> bool:
        """Whether a cell is air with nothing of the game's within `wide` of it."""
        level, m = self.level, self.m
        if not (1 <= x < m.WIDTH - 1) or level.t[y][x] != m.AIR:
            return False
        return not any((x + d, y) in self.busy or (x + d, y - 1) in self.busy for d in range(-wide, wide + 1))

    def stand(self, key: str, x: int, y: int, dx: int = 0, dy: int = 0) -> bool:
        if self.level.t[y][x] in self.m.GROUND:
            return False
        self.level.stand(key, x, y, dx, dy)
        self.count += 1
        return True

    def say(self, kind: str, x: int, y: int, what: str):
        """Something that says a line, on the nearest clear floor at or east of a place."""
        level, m = self.level, self.m
        for step in range(0, 12):
            for sx in (x + step, x - step):
                if self.clear(sx, y, 1) and level.t[y + 1][sx] in m.GROUND:
                    level.say(kind, sx, y, SAID[what])
                    self.busy.add((sx, y))
                    return sx
        raise SystemExit(f"nowhere to stand the {kind} that says {what!r} near ({x}, {y})")

    def lamp(self, x: int, y: int, char: str) -> bool:
        if self.level.t[y][x] == self.m.AIR and (x, y) not in self.busy:
            self.level.t[y][x] = char
            self.busy.add((x, y))
            return True
        return False


def clouds(level, m):
    """Banks of cloud round the rooms, where the plan has nothing: thick here, thin there,
    so the level is rooms in cloud and not rooms in frames. Three tiles at the least under
    a floor; the open sky is left open."""
    near = [[99] * m.WIDTH for _ in range(m.HEIGHT)]
    front = []
    for y in range(m.HEIGHT):
        for x in range(m.WIDTH):
            if level.room_at[y][x] is not None:
                near[y][x] = 0
                front.append((x, y))
    for d in range(1, 9):
        nxt = []
        for x, y in front:
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < m.WIDTH and 0 <= ny < m.HEIGHT and near[ny][nx] > d:
                    near[ny][nx] = d
                    nxt.append((nx, ny))
        front = nxt
    for y in range(m.HEIGHT):
        for x in range(m.WIDTH):
            d = near[y][x]
            if d == 0 or d > 8:
                continue
            swell = 2.2 + 2.6 * (0.5 + 0.5 * math.sin(x * 0.13 + y * 0.07)) + 2.4 * (0.5 + 0.5 * math.sin(x * 0.047 - y * 0.11 + 1.7))
            if d <= swell:
                level.t[y][x] = m.CLOUD


def billows(level, m, d: Dresser):
    """A cloud's tiles end in steps: billows hang along every underside of cloud that faces air."""
    import tv2_heaven

    for y in range(2, m.HEIGHT - 2):
        for x in range(2, m.WIDTH - 2):
            if level.t[y][x] != m.CLOUD or level.t[y + 1][x] != m.AIR or level.walls[y + 1][x] != m.AIR:
                continue
            step = level.t[y][x - 1] != m.CLOUD or level.t[y][x + 1] != m.CLOUD
            if step or x % 4 == 0:
                d.stand(f"heaven_puff_{int(m.hash01(x, y, 71) * tv2_heaven.PUFFS)}", x, y + 1)


def along(level, m, d: Dresser, room, runs):
    """What a zone's floors are stood with. What is planted in rows (the orchard's trees,
    the wheat) stands in rows, with a stretch here and there where most are gone;
    everything else stands in ones, twos and threes with room between, no two groups
    alike. And on every long floor one stretch is left bare: somewhere to stop."""
    for n, (keys, apart) in enumerate(ALONG[room.zone]):
        for y, x0, x1 in runs:
            rest = x0 + 8 + int(m.hash01(room.i0, y, 91) * max(1, x1 - x0 - 16)) if x1 - x0 >= 30 else -99
            if n == 0 and rest > 0 and room.zone == 5 and d.clear(rest, y, 2):
                d.stand("heaven_bench", rest, y)
                d.busy.add((rest, y))
            if apart <= 8:
                xs = [
                    x
                    for x in range(x0 + 2 + (n * 3) % apart, x1 - 1, apart)
                    if abs(x - rest) >= 6 and not (m.hash01(x // (apart * 5), y, 23) < 0.25 and m.hash01(x, y, 29) < 0.75) and d.clear(x, y, 1)
                ]
                for x in xs:
                    pick = keys
                    if keys[0].startswith("eden_wheat"):  # a row ends in its tally post
                        pick = keys[:2] if x + apart not in xs else keys[2:]
                    d.stand(pick[int(m.hash01(x, y, 40 + n) * len(pick))], x, y)
                continue
            x = x0 + 2 + int(m.hash01(room.i0, y, n) * apart)
            while x < x1 - 1:
                for _ in range(1 + int(m.hash01(x, y, 20 + n) * 2.6)):
                    if x >= x1 - 1:
                        break
                    if abs(x - rest) >= 6 and d.clear(x, y, 1):
                        d.stand(keys[int(m.hash01(x, y, 40 + n) * len(keys))], x, y, int((m.hash01(x, y, 50) - 0.5) * 12), 0)
                    x += max(2, int(apart * (0.3 + 0.3 * m.hash01(x, y, 60 + n))))
                x += max(3, int(apart * (0.7 + 1.5 * m.hash01(x, y, 70 + n))))


# What stands far off behind a zone's floors: (picture, tiles wide, tiles high).
FAR = {
    1: ("eden_far_hollow", 36, 12),
    2: ("eden_far_orchard", 36, 10),
    3: ("eden_far_vines", 30, 26),
    4: ("eden_far_fields", 44, 9),
    5: ("eden_far_terraces", 40, 12),
    7: ("eden_far_cliffs", 40, 17),
}
FRONT = {1: 0, 2: 1, 3: 0, 4: 2, 5: 3, 6: 4, 7: 5}
UNDER = {1: "root", 2: "root", 3: "root", 4: "root", 5: "marble", 6: "pearl", 7: "silver"}
MOSS = {5: "green", 6: "pale", 7: "frost"}
# What is in a zone's air: (kind of life, art pixels over the floor), in turn along a floor.
AIRS = {
    1: [("butterflies", 16), ("pollen", 40)],
    2: [("fireflies", 24)],
    3: [("butterflies", 16), ("pollen", 48)],
    4: [("pollen", 40), ("butterflies", 16)],
    5: [("leaves", 96), ("pollen", 48)],
    6: [("smoke", 64)],
    7: [("snow", 150)],
}
RAYS = {1, 2, 5, 6}  # where the sun is low enough to stand in shafts
MISTS = {2, 3, 7}


def far_fits(level, m, room, x: int, y: int, wide: int, tall: int) -> bool:
    """Whether a far picture can stand behind a floor: inside its room with room to slide,
    under its room's top, and with floor under most of it (its foot is cut straight)."""
    half = wide // 2
    if x - half - 3 <= room.x0 or x + half + 3 >= room.x1 or y - tall + 3 < room.y0:
        return False
    under = sum(level.t[y + 1][c] in m.GROUND or level.t[y + 2][c] in m.GROUND for c in range(x - half, x + half + 1))
    return under >= 0.82 * (2 * half + 1)


def far(level, m, x: int, y: int, key: str):
    level.mark("life", x, y, "far", key, 0, 0)


def depth(level, m, d: Dresser, room, runs):
    """Far and near, and what is in the air between. Behind a zone's floors, more of the
    zone going away, passed slower than the world (`life far`); here and there a dark
    shape close to the eye (`life front`); shafts of low sun where the hour has them,
    mist where it lies, and the zone's own motes in the air."""
    if room.kind in ("hidden", "late"):
        return 0
    made = 0
    storeys = {m.S(j) - 1 for j in room.storeys}
    mains = [run for run in runs if run[0] in storeys]
    spec = FAR.get(room.zone)
    if spec and room.name != "crownvine":
        key, wide, tall = spec
        for y in sorted(storeys, reverse=True):
            placed = 0
            for x in range(room.x0 + wide // 2 + 4 + int(m.hash01(room.i0, y, 3) * 5), room.x1 - wide // 2 - 3, wide + 12):
                if far_fits(level, m, room, x, y, wide, tall):
                    far(level, m, x, y, key)
                    placed += 1
            made += placed
            if placed and tall > 12:
                break  # a tall one is not stood again in the storey over it: it is already there
    for n, (y, x0, x1) in enumerate(mains):
        long = x1 - x0 + 1
        if long >= 30:
            for x in range(x0 + 12 + int(m.hash01(room.i0, y, 7) * 10), x1 - 11, 41):
                if x - room.x0 > 12 and room.x1 - x > 12 and d.clear(x, y, 4) and all(level.t[y + 1][c] in m.GROUND for c in range(x - 3, x + 4)):
                    level.mark("life", x, y, "front", f"eden_front_{FRONT[room.zone]}", 0, 0)
                    made += 1
        if room.zone in RAYS and long >= 16:
            for x in range(x0 + 6 + int(m.hash01(room.i0, y, 11) * 9), x1 - 5, 29):
                if all(level.t[y - u][x] == m.AIR for u in range(0, 10)):
                    level.mark("life", x, y, "rays", f"eden_rays_{int(m.hash01(x, y, 13) * 2)}", 0, 0)
                    made += 1
        if room.zone in MISTS and y == room.y1 - 2 and long >= 22:
            for x in range(x0 + 10, x1 - 9, 23):
                if level.t[y][x] == m.AIR and all(level.t[y + 1][c] in m.GROUND and (level.t[y][c] == m.AIR or level.t[y][c] in LAMPS) for c in range(x - 9, x + 10)):
                    d.stand("eden_mist", x, y)
        airs = AIRS[room.zone]
        if long >= 14:
            for k, x in enumerate(range(x0 + 7 + int(m.hash01(room.i0, y, 17) * 8), x1 - 5, 37)):
                kind, up = airs[(k + n) % len(airs)]
                if kind == "smoke" and not d.clear(x, y, 0):
                    continue
                if all(level.t[y - u][x] == m.AIR for u in range(0, min(9, up // 16 + 2))):
                    level.mark("life", x, y, kind, "-", 0, up)
                    made += 1
    if room.roofed and room.zone in (3, 6, 7):
        for y, x0, x1 in mains:
            for x in range(x0 + 9 + int(m.hash01(room.i0, y, 19) * 6), x1 - 4, 27):
                top = y
                while top > room.y0 and level.t[top - 1][x] == m.AIR:
                    top -= 1
                if 4 <= y - top <= 9:
                    level.mark("life", x, top, "drip", "-", 0, 14)
                    made += 1
    if not room.roofed and room.zone in (1, 4, 5) and room.x1 - room.x0 >= 50:
        level.mark("life", (room.x0 + room.x1) // 2, room.y0 + 5, "birds", "-", 0, 0)
        made += 1
    return made


def wear(level, m, d: Dresser, room, runs):
    """What time has done to the floors of cut stone: a crack down from the lip here, and
    there what has grown over it: moss on the Terraces, pale lichen in the Making-house,
    rime by the Gate."""
    tint = MOSS.get(room.zone)
    if tint is None:
        return
    for y, x0, x1 in runs:
        x = x0 + 2 + int(m.hash01(room.i0, y, 31) * 8)
        while x < x1 - 2:
            h = m.hash01(x, y, 33)
            if h < 0.42:
                v = int(m.hash01(x, y, 35) * 3)
                half = (2, 3, 2)[v]
                if all(level.t[y + 1][c] in m.GROUND and level.t[y][c] == m.AIR for c in range(x - half, x + half + 1)):
                    d.stand(f"eden_moss_{tint}_{v}", x, y)
            elif h < 0.66:
                if all(level.t[y + 1][c] in m.GROUND and level.t[y][c] == m.AIR for c in range(x - 1, x + 2)) and level.t[y + 2][x] in m.GROUND:
                    d.stand(f"eden_crack_{int(m.hash01(x, y, 37) * 3)}", x, y)
            x += 5 + int(m.hash01(x, y, 39) * 9)


def undersides(level, m, d: Dresser, room):
    """A floor in the air is not a plank: under it hangs what it is made of: earth and
    roots in the gardens, a mass of its own stone after."""
    kind = UNDER[room.zone]
    for j in range(room.j0, room.j1):
        y = m.S(j) + 2  # the row under the platform
        xs = [x for x in sorted(room.cover[j]) if level.t[y - 1][x] in m.GROUND and level.t[y][x] == m.AIR]
        x = xs[0] if xs else 0
        while xs and x <= xs[-1]:
            for v, wide, tall, chance in ((1, 9, 4, 0.3), (0, 6, 3, 0.7), (2, 4, 2, 0.9)):
                cells = [(c, yy) for c in range(x, x + wide) for yy in range(y, y + tall)]
                if m.hash01(x, y, 43 + v) < chance and all(c in room.cover[j] and level.t[y - 1][c] in m.GROUND and level.t[yy][c] == m.AIR for c, yy in cells):
                    d.stand(f"eden_under_{kind}_{v}", x + wide // 2, y)
                    x += wide - 1  # the next begins where this ends: they join
                    break
            else:
                x += 1
                continue
            x += int(m.hash01(x, y, 47) * 4)


def hangers(level, m, d: Dresser, room):
    """Ivy from the underside of every platform and ceiling, in the zone's own leaf."""
    tint = IVY[room.zone]
    for y in range(room.y0 + 1, room.y1 - 3):
        for x in range(room.x0 + 2, room.x1 - 1):
            under = level.t[y - 1][x] in m.GROUND and level.t[y][x] == m.AIR and level.t[y + 1][x] == m.AIR
            if under and m.hash01(x, y, 9) < (0.16 if room.zone != 6 else 0.07) and level.t[y - 1][x - 1] in m.GROUND and level.t[y - 1][x + 1] in m.GROUND:
                if room.zone >= 6 and m.hash01(x, y, 13) < 0.4:
                    d.stand(f"eden_chain_{int(m.hash01(x, y, 5) * 2)}", x, y)  # chains, where the garden ends
                else:
                    d.stand(f"eden_ivy_{tint}_{int(m.hash01(x, y, 3) * 3)}", x, y)


def columns(level, m, d: Dresser, room):
    """What holds a platform up: under each one, down to the floor below, the zone's own
    column: a stem, a hanging of leaves, a post, a pillar of marble, a pipe."""
    for j in range(room.j0, room.j1):
        top, foot = m.S(j) + 2, m.S(j + 1) - 1
        xs = sorted(room.cover[j])
        for x in xs:
            if (x - room.x0) % 9 != 4 or x + 1 not in room.cover[j] or x - 1 not in room.cover[j]:
                continue
            if any(level.t[y][x] != m.AIR or level.t[y][x + 1] != m.AIR for y in range(top, foot + 1)) or level.t[foot + 1][x] not in m.GROUND:
                continue
            if room.zone == 5:
                d.stand("eden_pillar_10x2", x, foot)
            elif room.zone == 6:
                d.stand("eden_pipe_v_12", x, foot)
            elif room.zone in (1, 2, 3, 4):
                char = {1: "t", 2: "w", 3: "l", 4: "w"}[room.zone]
                for y in range(top, foot + 1):
                    if not room.roofed or True:
                        level.walls[y][x] = char


def lamps(level, m, d: Dresser, room, runs):
    made = 0
    for y, x0, x1 in runs:
        for x in range(x0 + 7, x1 - 3, 19 if room.zone != 2 else 9):
            if d.clear(x, y, 0) and level.t[y - 1][x] == m.AIR:
                made += d.lamp(x, y, room.style["lamp"])
    return made


def orchard_light(level, m, d: Dresser, room, runs):
    """The orchard is lit by its fruit: over its floors, at the height of a tree's crown, in rows."""
    for y, x0, x1 in runs:
        for x in range(x0 + 4, x1 - 2, 8):
            for up in (6, 7):
                if all(level.t[y - u][x] == m.AIR for u in range(0, up + 2)):
                    d.lamp(x + (up - 6) * 2, y - up, "Ý")
                    break


def lantern_tree(level, m, d: Dresser, room):
    """The Lantern Tree: a trunk six tiles through from the room's floor to its top, a bough
    out of it over every storey, and fruit that lights the whole of it."""
    middle = (room.x0 + room.x1) // 2
    for y in range(room.y0 + 1, room.y1 - 1):
        for x in range(middle - 3, middle + 3):
            level.walls[y][x] = "w"
    for k, j in enumerate(room.storeys):
        y = m.S(j) - 5
        if level.t[y][middle + (6 if k % 2 else -7)] == m.AIR:
            d.stand(f"eden_bough_{k % 2}", middle + (10 if k % 2 == 0 else -11), m.S(j) - 2 if level.t[m.S(j) - 2][middle + (10 if k % 2 == 0 else -11)] == m.AIR else y)
        for dx in range(-15, 16, 5):
            x = middle + dx
            fy = m.S(j) - 7 - (abs(dx) // 5) % 2
            if room.x0 < x < room.x1 and level.t[fy][x] == m.AIR:
                d.lamp(x, fy, "Ý")
    # Its crown fills the top of the room: a screen of leaves and fruit.
    for y in (room.y0 + 23, room.y0 + 22, room.y0 + 24, room.y0 + 21, room.y0 + 25):
        if level.t[y][middle] == m.AIR:
            d.stand("eden_lantern_crown", middle, y)
            break


def crown_vine(level, m, d: Dresser, room):
    """The Crown Vine: its trunk fills the middle third of its room from foot to top, and the climb goes round it."""
    third = (room.x1 - room.x0) // 3
    for y in range(room.y0 + 1, room.y1 - 1):
        for x in range(room.x0 + third, room.x1 - third + 1):
            level.walls[y][x] = "Ù"
    # A bough out of it over every storey, to the side the floor is on: what the hero climbs is its limbs.
    for k, j in enumerate(room.storeys):
        y = m.S(j) - 2
        for key, x in (("eden_vine_bough_0", room.x1 - third + 8), ("eden_vine_bough_1", room.x0 + third - 8)):
            if (k + (key[-1] == "1")) % 2 == 0 and room.x0 + 9 < x < room.x1 - 9 and level.t[y][x] == m.AIR and level.t[y + 2][x] in m.GROUND:
                d.stand(key, x, y)


def waterfalls(level, m, d: Dresser, room):
    """On the Terraces water falls from the end of a platform to the floor under it."""
    for j in range(room.j0, room.j1):
        xs = sorted(room.cover[j])
        ends = [x for x in xs if x + 1 not in room.cover[j] and x + 4 < room.x1] + [x for x in xs if x - 1 not in room.cover[j] and x - 4 > room.x0]
        for n, x in enumerate(ends[:2]):
            fx = x + 2 if x + 1 not in room.cover[j] else x - 3
            rows = range(m.S(j), m.S(j + 1))
            if all(level.t[y][fx] == m.AIR and level.t[y][fx + 1] == m.AIR for y in rows) and level.t[m.S(j + 1)][fx] in m.GROUND and not any((fx + e, m.S(j + 1) - 1) in d.busy for e in range(-2, 4)):
                for y in rows:
                    level.t[y][fx] = level.t[y][fx + 1] = "|"
                level.mark("life", fx, m.S(j + 1) - 1, "splash", "-", 8, 2)


def gateways(level, m, d: Dresser):
    """Every doorway stands in an arch: grown, in the garden's first zones; of old cut stone
    after. The Warden's has fog standing in it. And where two rooms that differ meet at a
    door, they are let into one another: the ground of each runs a way into the other's,
    a roofed room's wall thins out into the open one and lets the sky into itself, and
    each zone's growth stands a little way over the line. So a door is a change that
    comes on, not a line that is crossed."""
    rooms = level.rooms
    for a, b, side, line, along_, kind in plan.LINKS:
        if side != "v":
            continue
        ra, rb = rooms[a], rooms[b]
        left, right = (ra, rb) if ra.i1 == line - 1 else (rb, ra)
        c0, c1, s = left.x1, right.x0, m.S(along_)
        if kind in ("door", "boss", "D", "HL") and level.t[s - 1][c0] == m.AIR:
            d.stand("eden_arch_vine" if max(left.zone, right.zone) <= 3 else "eden_arch_stone", c0, s - 1, 8, 0)
        if kind == "boss":
            d.stand("eden_fog", c0, s - 1, 8, 0)
        if left.zone == right.zone and left.roofed == right.roofed:
            continue
        # The ground of each a way into the other, in runs of two tiles, fewer the further in.
        for dx in range(1, 12):
            for room, other, x in ((left, right, c0 - dx), (right, left, c1 + dx)):
                if left.zone != right.zone and m.hash01(x // 2, along_, 5) < (1 - dx / 12) * 0.55:
                    for y, char in ((s, other.style["ground"]), (s + 1, other.style["under"])):
                        if level.t[y][x] in (room.style["ground"], room.style["under"]):
                            level.t[y][x] = char
        # The walls behind: a roofed room's thins out into the open one, and is holed by the sky near the door.
        rows = range(max(left.y0, right.y0) + 1, min(left.y1, right.y1) - 1)
        # Not tile by tile, which would be a checker: row by row, as far as a slow wave says,
        # so the edge between the two is one ragged line, like the edge of a wood.
        for y in rows:
            wave = 0.5 + 0.5 * math.sin(y * 0.55 + line) * math.cos(y * 0.23 + along_)
            reach = 2 + int(6 * wave + 2 * m.hash01(y // 2, line, 11))
            for room, other, step, edge in ((left, right, -1, c0), (right, left, 1, c1)):
                for dx in range(1, reach + 1):
                    x = edge + step * dx
                    if not (room.x0 < x < room.x1):
                        break
                    if other.roofed and not room.roofed:
                        level.walls[y][x] = other.style["back"]
                    elif room.roofed and not other.roofed and dx <= reach // 3:
                        level.walls[y][x] = m.AIR
                    elif room.roofed and other.roofed and room.style["back"] != other.style["back"] and dx <= reach // 2:
                        level.walls[y][x] = other.style["back"]
        # Each zone's growth a little way over the line.
        if left.zone != right.zone:
            for room, other, x in ((left, right, c0 - 6), (right, left, c1 + 6), (left, right, c0 - 12)):
                keys = ALONG[other.zone][0][0]
                if d.clear(x, s - 1, 1) and level.t[s][x] in m.GROUND:
                    d.stand(keys[int(m.hash01(x, s, 17) * len(keys))], x, s - 1)


def old_halls(level, m, d: Dresser, room, runs):
    """A roofed room of the terraces, the Making-house or the Gate has windows of an old hall down its wall, a light behind each."""
    if not room.roofed or room.zone < 5:
        return
    for y, x0, x1 in runs:
        for x in range(x0 + 6, x1 - 4, 12):
            if d.clear(x, y, 2) and all(level.t[y - u][x] == m.AIR for u in range(1, 9)):
                d.stand("eden_window", x, y)


def bonfires(level, m, d: Dresser):
    """By every shrine a fire burns round a twisted sword: a place to rest, in an old place."""
    for kind, x, y, _ in list(level.marks):
        if kind == "checkpoint" and level.t[y][x + 2] == "Å":
            d.stand("eden_bonfire", x + 2, y)


def centre_floor(level, m, room, wide: int = 3):
    """A place in the middle of a room's lowest floor: (column, row he would stand in)."""
    y = room.y1 - 2
    mid = (room.x0 + room.x1) // 2
    for step in range(0, 20):
        for x in (mid + step, mid - step):
            if all(level.t[y][x + e] == m.AIR and level.t[y + 1][x + e] in m.GROUND for e in range(-wide, wide + 1)):
                return x, y
    return mid, y


def vista(level, room, m, zoom: float, hold: bool = False, look_up: int = 0):
    """The view draws back over a room's lowest storey."""
    top = room.y1 - 11
    level.mark("vista", room.x0 + 2, top, room.x1 - room.x0 - 3, 10, int(round(zoom * 100)), int(hold), 0, 0, look_up)


def dress(level, m, log=print):
    clouds(level, m)
    d = Dresser(level, m)
    rooms = level.rooms
    lit = 0
    for room in rooms.values():
        runs = floors_of(level, room, m)
        if room.name == "lantern":
            lantern_tree(level, m, d, room)
        if room.name == "crownvine":
            crown_vine(level, m, d, room)
        columns(level, m, d, room)
        if room.zone == 5 and room.kind == "main":
            waterfalls(level, m, d, room)
        if room.zone == 2:
            orchard_light(level, m, d, room, runs)
        old_halls(level, m, d, room, runs)
        lit += lamps(level, m, d, room, runs)
    gateways(level, m, d)
    bonfires(level, m, d)

    # The story's people and things, where the design puts them.
    def floor_of(name, storey=None):
        r = rooms[name]
        return m.S(r.j1 if storey is None else storey) - 1

    x = d.say("shade", rooms["hollow"].x0 + 9, floor_of("hollow", 21), "voice")
    x = d.say("soul", rooms["orchard"].x0 + 30, floor_of("orchard"), "mereth")
    d.stand("eden_mereth", x - 3, floor_of("orchard"))
    d.say("tablet", rooms["undergrove"].x0 + 20, floor_of("undergrove"), "token")
    d.say("tablet", rooms["fields"].x0 + 40, floor_of("fields"), "ledger")
    x = d.say("soul", rooms["sundial"].x0 + 30, floor_of("sundial"), "ithuriel")
    d.stand("eden_ithuriel", x + 3, floor_of("sundial"))
    d.say("tablet", rooms["sidegarden"].x0 + 20, floor_of("sidegarden"), "stone")
    d.say("soul", rooms["alcove"].x0 + 8, floor_of("alcove"), "gathered")
    d.say("tablet", rooms["alcove"].x0 + 28, floor_of("alcove"), "card")
    d.say("tablet", rooms["cistern"].x0 + 40, floor_of("cistern"), "glove")
    d.say("shade", rooms["door"].x0 + 20, floor_of("door"), "making")
    d.say("tablet", rooms["drain"].x0 + 40, floor_of("drain"), "shard")
    d.say("shade", rooms["approach"].x0 + 50, floor_of("approach"), "gate")

    # The landmarks.
    for name, key, lift in (("sundial", "eden_sundial", 0), ("wheel", "eden_wheel", 0), ("door", "eden_making_door", 0), ("arena", "eden_low_gate", 0)):
        x, y = centre_floor(level, m, rooms[name])
        d.stand(key, x, y)
    # The Rising Leaf: one leaf as tall as a tower behind the stair, and another over it.
    rising = rooms["rising"]
    x, y = centre_floor(level, m, rising)
    d.stand("eden_great_leaf", x, y)
    for j in rising.storeys[1:]:
        y = m.S(j) - 1
        spots = [c for c in range(rising.x0 + 14, rising.x1 - 13) if level.t[y][c] == m.AIR and level.t[y + 1][c] in m.GROUND]
        if y < rising.y1 - 40 and y - 36 > rising.y0 and spots:
            d.stand("eden_great_leaf", spots[len(spots) // 2], y)
            break
    # The Making-house behind its door; and from afar, nearer room by room.
    x, y = centre_floor(level, m, rooms["door"])
    d.stand("eden_far_house_2", x, y)
    walk, arcade = rooms["pipewalk"], rooms["arcade"]
    far(level, m, arcade.x1 - 14, arcade.y1 - 2, "eden_far_house_0")
    far(level, m, walk.x0 + 24, walk.y1 - 2, "eden_far_house_0")
    far(level, m, walk.x1 - 22, walk.y1 - 2, "eden_far_house_1")
    # The Low Gate from afar: from the Terraces on.
    far(level, m, arcade.x0 + 30, arcade.y1 - 2, "eden_far_gate_0")
    far(level, m, rooms["promenade"].x1 - 26, rooms["promenade"].y1 - 2, "eden_far_gate_0")
    head = rooms["ledgehead"]
    far(level, m, (head.x0 + head.x1) // 2, head.y1 - 2, "eden_far_gate_1")
    # The Wheel over the way up the Terraces.
    for name in ("stairfoot", "firstterrace", "climb"):
        r = rooms[name]
        for j in (r.storeys[0], r.storeys[len(r.storeys) // 2]) if len(r.storeys) > 4 else (r.storeys[0],):
            far(level, m, (r.x0 + r.x1) // 2 + (5 if name == "stairfoot" else 0), m.S(j) - 1, "eden_far_wheel")
    x, y = centre_floor(level, m, rooms["fields"])
    d.stand("earth_well", x + 14, y)
    x, y = centre_floor(level, m, rooms["meadow"])
    d.stand("earth_cottage", x, y)
    x, y = centre_floor(level, m, rooms["promenade"])
    d.stand("heaven_fountain", x, y, 8)
    for name in ("climb", "stairfoot"):
        x, y = centre_floor(level, m, rooms[name])
        d.stand("heaven_angel_0", x - 8, y)
    for name in ("landing", "sundial", "promenade", "pipewalk", "door"):
        x, y = centre_floor(level, m, rooms[name])
        d.stand("eden_gathered", x - 14, y)

    alive = 0
    for room in rooms.values():
        runs = floors_of(level, room, m, lamps=True)
        alive += depth(level, m, d, room, runs)
        along(level, m, d, room, runs)
        wear(level, m, d, room, runs)
        undersides(level, m, d, room)
        hangers(level, m, d, room)
    billows(level, m, d)

    # Where the view draws back: at the landmarks.
    vista(level, rooms["lantern"], m, 1.6, True, 6)
    vista(level, rooms["sundial"], m, 1.7, False, 6)
    vista(level, rooms["wheel"], m, 2.0, True, 10)
    vista(level, rooms["door"], m, 1.7, True, 5)
    vista(level, rooms["arena"], m, 2.0, True, 8)

    # Every room says which zone it is of; and the pictures worth taking.
    for zone, (key, name) in plan.ZONES.items():
        for r in rooms.values():
            if r.zone == zone:
                level.mark("zone", r.x0, r.y0, r.x1 - r.x0 + 1, r.y1 - r.y0 + 1, zone, *name.split())
                # And its own hour: the day turns to it as the hero comes in.
                level.mark("mood", r.x0, r.y0, r.x1 - r.x0 + 1, r.y1 - r.y0 + 1, int(round(m.MOODS[zone] * 100)))
    views = {
        "hollow": ("hollow", 21), "rising": ("rising", 19), "orchard": ("orchard", 18), "lantern": ("lantern", 19), "rows": ("rows", 18),
        "vines": ("vineway", 15), "tangle": ("tangle", 12), "crown": ("crownvine", 0), "fields": ("fields", 9), "sundial": ("sundial", 10),
        "terraces": ("promenade", 2), "wheel": ("wheel", 2), "climb": ("climb", 5), "making": ("pipewalk", 2), "works": ("works", 6),
        "door": ("door", 6), "cliffs": ("cliffs", 10), "gate": ("arena", 8),
        "leafrun": ("leafrun", 17), "meadow": ("meadow", 9), "arcade": ("arcade", 2), "ledgehead": ("ledgehead", 6), "approach": ("approach", 8),
    }  # fmt: skip
    for name, (room, storey) in views.items():
        r = rooms[room]
        level.view(f"eden_{name}", (r.x0 + r.x1) / 2, m.S(storey) - 8)
    # Every view is taken at its room's own hour; these are the same places at another.
    level.shots += [("eden_orchard_night", "eden_orchard", 22.5), ("eden_gate_night", "eden_gate", 23.0), ("eden_fields_noon", "eden_fields", 12.0)]
    log(f"  dressed: {d.count} pictures, {lit} lamps, {alive} of life")
