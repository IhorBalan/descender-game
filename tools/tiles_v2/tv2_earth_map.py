#!/usr/bin/env python3
"""Dresses the Earth of the Tiles V2 world: says where each of its pictures stands
(tv2_earth.py draws them), where the meadows are in flower, which of the mine's walls are
boarded. Not a script: make_world_map.build calls `dress` once the world is laid, its
slopes cut and its walls put up.

Nothing here changes where the hero can go. A picture is a sprite stood in a cell
(`@mark picture column row key`), never solid; a meadow is grass of another colour; a
boarded wall is a wall. The one lamp it adds (a lantern by the cottage that is still lived
in) stands in air on the ground, as every lamp does.

The Earth from west to east (the columns are the world's, 540 of them):

    the Pit | a wood of firs | stair | a mixed wood, the old shaft under its pithead |
    the village on its hill: a chapel, a well, two cottages | the ruins by the start |
    the pond and its willow | stair | the mill and the fields, a cottage still lived in,
    an orchard | the Gate | the graves before it | stair | firs
"""
from __future__ import annotations

from tv2_base import *  # noqa: F401,F403  (the standard's shared vocabulary)
import tv2_earth

AIR = "."
PICTURE = "picture"
LIFE = "life"  # TilesV2Life's own marks


class Dresser:
    def __init__(self, world, m):
        self.w, self.m = world, m
        self.t, self.walls = world.t, world.walls
        self.width = m.WIDTH
        self.ground = set(TERRAIN_CHARS)
        self.taken: list[tuple[int, int]] = []  # column spans already built on
        self.marked = {(mk[1], mk[2]) for mk in world.marks}
        self.clear = list(getattr(world, "clear", []))
        self.stood = 0
        self.track_cells: set = set()
        self.lives: dict[str, int] = {}
        self.trees: list[tuple[int, int, str]] = []
        self.posts: list[tuple[int, int]] = []

    # --- Where things are -----------------------------------------------------------

    def top(self, x: int) -> int | None:
        """The row of the first ground under the sky at a column of the Earth's surface, or
        None if the first thing there is not ground (water, a ledge, a hole)."""
        for y in range(self.m.GROUND_LINE - 24, self.m.SURFACE_LINE + 10):
            c = self.t[y][x]
            if c == AIR or c in PROP_CHARS or c == SPAWN_CHAR:
                continue
            return y if c in self.ground else None
        return None

    def solid_top(self, x: int) -> int | None:
        """As top, but only a full tile (no slope) counts."""
        y = self.top(x)
        return y if y is not None and ":" not in TERRAIN_CHARS[self.t[y][x]] else None

    def is_clear(self, x0: int, x1: int) -> bool:
        return any(not (x1 < a or x0 > b) for a, b in self.clear)

    def is_taken(self, x0: int, x1: int) -> bool:
        return any(not (x1 < a or x0 > b) for a, b in self.taken)

    def has_mark(self, x0: int, x1: int, y0: int, y1: int) -> bool:
        return any(x0 <= x <= x1 and y0 <= y <= y1 for x, y in self.marked)

    def site(self, lo: int, hi: int, wide: int, sag: int) -> tuple[int, int] | None:
        """The best place between columns lo and hi for something `wide` columns wide whose
        ground may differ by `sag` rows under it: (its first column, the highest ground
        row under it), the flattest and nearest the middle of the range."""
        best = None
        for x0 in range(lo, hi - wide + 1):
            tops = [self.top(x) for x in range(x0, x0 + wide)]
            if any(v is None for v in tops) or self.is_clear(x0, x0 + wide) or self.is_taken(x0, x0 + wide - 1):
                continue
            spread = max(tops) - min(tops)
            if spread > sag or self.has_mark(x0 - 1, x0 + wide, min(tops) - 12, max(tops) + 1):
                continue
            score = spread * 10 + abs(x0 + wide / 2 - (lo + hi) / 2) * 0.1
            if best is None or score < best[0]:
                best = (score, x0, min(tops))
        return None if best is None else (best[1], best[2])

    def stand(self, key: str, x: int, ground: int, span: tuple[int, int] | None = None):
        """Stands a picture on the top of the ground row `ground` at column x: the map marks
        the cell over it (the picture's pivot is its floor line, tv2_earth.pivot_of)."""
        assert key in tv2_earth.PICTURE_KEYS, key
        row = ground - 1
        if self.t[row][x] in self.ground:
            raise ValueError(f"{key} would be marked in the ground at ({x}, {row})")
        self.w.mark(PICTURE, x, row, key)
        if key.startswith("earth_rail"):
            self.track_cells.add((x, row))
        else:
            self.marked.add((x, row))
        self.stood += 1
        if span:
            self.taken.append(span)

    def life(self, kind: str, x: int, row: int, key: str = "-", dx: int = 0, dy: int = 0):
        """A mark for TilesV2Life in cell (x, row): `kind` (smoke, leaves, fireflies, butterflies,
        splash, drip, sails, glow, rays, front), the picture it shows (or -), and where from the
        foot of the cell it stands, in art pixels (dx right, dy up)."""
        assert key == "-" or key in tv2_earth.PICTURE_KEYS, key
        self.w.mark(LIFE, x, row, kind, key, dx, dy)
        self.lives[kind] = self.lives.get(kind, 0) + 1

    def building(self, key: str, lo: int, hi: int, sag: int = 2, foot: int | None = None) -> tuple[int, int] | None:
        """Builds a picture (on its plinth, which goes down behind the ground) somewhere between lo and hi;
        `foot` is how many columns it stands on (all but the outermost of its width if not
        said); returns (its middle column, its floor row: the highest ground under it)."""
        wide = -(-tv2_earth.size_of(key)[0] // TILE)
        foot = foot or max(wide - 2, 2)
        found = self.site(lo, hi, foot, sag)
        if not found:
            return None
        x0, floor = found
        middle = x0 + foot // 2
        self.stand(key, middle, floor, (middle - wide // 2, middle + wide // 2))
        return middle, floor

    # --- The surface ---------------------------------------------------------------

    def meadows(self):
        """Grass in flower in patches where the field of the hash says so: through the village,
        the fields, a clearing in the wood. The slopes of a patch flower too."""
        to = {"G": "Ñ", "/": "ñ", "\\": "Ö"}
        patches = [(84, 150), (180, 262), (336, 410), (455, 498)]
        for x in range(3, self.width - 3):
            if not any(a <= x <= b for a, b in patches):
                continue
            if self.m.field(4242, x, 0, 9.0) < -0.05:
                continue
            for y in range(self.m.GROUND_LINE - 24, self.m.SURFACE_LINE + 10):
                c = self.t[y][x]
                if c in to:
                    self.t[y][x] = to[c]
                    if c == "G":
                        break
                elif c != AIR and c not in PROP_CHARS:
                    break

    def tree(self, key: str, x: int, sag: int = 0) -> bool:
        """A tree on level ground at x (the ground a column either side the same, or `sag`
        rows apart), where nothing is built and no stair stands."""
        # The trunk stands on a full tile; a column either side may be a slope.
        tops = [self.top(x - 1), self.solid_top(x), self.top(x + 1)]
        if None in tops or max(tops) - min(tops) > sag or self.is_clear(x - 3, x + 3) or self.is_taken(x - 2, x + 2):
            return False
        if self.has_mark(x - 1, x + 1, tops[0] - 3, tops[0]):
            return False
        # A crown does not stand in another one: two trees at least half their widths apart.
        half = tv2_earth.size_of(key)[0] / TILE / 2
        if any(abs(x - tx) < 0.55 * (half + tv2_earth.size_of(k)[0] / TILE / 2) for tx, _, k in self.trees):
            return False
        self.stand(key, x, tops[1])
        self.trees.append((x, tops[1], key))
        return True

    def woods(self, lo: int, hi: int, kinds: list[str], gap: tuple[int, int], salt: int):
        x = lo
        while x < hi:
            kind = kinds[int(self.m.hash01(x, salt) * len(kinds))]
            if self.tree(kind, x, sag=1):
                x += gap[0] + int(self.m.hash01(x, salt + 1) * (gap[1] - gap[0]))
            else:
                x += 1

    def surface(self):
        m, w = self.m, self.w
        start = w.spawn[0]
        mouth = m.EAST - 70
        # The old shaft, by the post that names it: a pithead of timber over it, a winch hut by it.
        shaft = next((mk[1] + 3 for mk in w.marks if mk[0] == "sign" and mk[3][1:4] == ["The", "old", "shaft."]), None)
        if shaft is not None:
            row = max(self.top(c) or 0 for c in (shaft - 1, shaft + 4))
            self.stand("earth_pithead", shaft + 1, row, (shaft - 2, shaft + 5))
            self.building("earth_winch_hut", shaft + 6, shaft + 14, sag=0)
            self.building("earth_crates_0", shaft - 9, shaft - 3, sag=0)
        # By the start: a temple of the old world stood here; its columns behind the stubs.
        tops = [self.top(c) for c in range(start - 13, start + 14)]
        self.stand("earth_colonnade", start, self.top(start) or max(v for v in tops if v is not None), (start - 13, start + 13))
        # The village on the hill west of the start.
        ruin = self.building("earth_cottage_ruin", start - 44, start - 15)
        if ruin:
            self.building("earth_kennel", ruin[0] + 6, ruin[0] + 12, sag=0)
            self.building("earth_bench", ruin[0] - 12, ruin[0] - 5, sag=0)
        chapel = self.building("earth_chapel", 180, 222)
        well = self.building("earth_well", 196, start - 16, sag=0)
        if well:
            self.building("earth_barrel", well[0] + 3, well[0] + 8, sag=0)
            self.building("earth_cart", well[0] - 12, well[0] - 3, sag=0)
            self.building("earth_haystack_1", well[0] + 6, well[0] + 16, sag=0)
        for lo, hi, v in ((178, 200, 0), (200, 236, 1), (222, 252, 2)):
            self.building(f"earth_fence_{v}", lo, hi, sag=0)
        if chapel:
            for k, dx in enumerate((-9, -12, 8)):
                x = chapel[0] + dx
                top = self.solid_top(x)
                if top is not None and not self.is_taken(x, x) and not self.is_clear(x, x):
                    self.stand(f"earth_grave_{k % 4}", x, top, (x, x))
        # The pond: a willow on its west bank, reeds at its edges, lilies on the water.
        pond0, pond1 = m.POND
        for x in (pond0 - 2, pond0 - 1, pond0 - 3, pond0 - 4, pond0 - 5, pond0 - 6, pond0):
            if self.tree("earth_willow", x, sag=1):
                break
        water = next((y for y in range(m.GROUND_LINE - 10, m.SURFACE_LINE + 6) if self.t[y][pond0 + 4] == "W"), None)
        for x, v in ((pond0 + 1, 0), (pond1 - 2, 1), (pond0 + 2, 2)):
            top = self.top(x)
            if top is not None:
                self.life("sway", x, top - 1, f"earth_reeds_{v}")
        if water is not None:
            for x, v in ((pond0 + 4, 0), (pond0 + 8, 1)):
                if self.t[water][x] == "W" and self.t[water][x - 1] == "W" and self.t[water][x + 1] == "W":
                    self.stand(f"earth_lilies_{v}", x, water + 1)
        # East of the stair by the pond: the mill on its rise, the fields, a cottage someone
        # still lives in (a lantern by its door), an orchard.
        mill = self.building("earth_windmill", 336, 358, sag=1, foot=4)
        if mill:
            self.life("sails", mill[0], mill[1] - 1, "earth_sails_0", 2, 124)
        home = self.building("earth_cottage", 356, 384)
        if home:
            self.life("glow", home[0], home[1] - 1, "earth_cottage_glow")
            self.life("smoke", home[0], home[1] - 1, "-", -37, 106)
            for lo, hi, key in ((home[0] - 14, home[0] - 6, "earth_washing"), (home[0] + 6, home[0] + 13, "earth_barrel"), (home[0] + 7, home[0] + 16, "earth_garden")):
                self.building(key, lo, hi, sag=0)
            lamp = home[0] + 4
            if self.t[home[1] - 1][lamp] == AIR and self.solid_top(lamp) == home[1]:
                self.t[home[1] - 1][lamp] = "J"
        for lo, hi, key in ((380, 392, "earth_haystack_0"), (386, 400, "earth_scarecrow"), (392, 404, "earth_haystack_1"), (352, 366, "earth_cart"), (396, 408, "earth_fence_2")):
            self.building(key, lo, hi, sag=0)
        fence = self.building("earth_fence_0", 378, 392, sag=0)
        if fence:
            # The cat sits on the fence's left post.
            self.stand("earth_cat", fence[0] - 1, fence[1])
        for lo, hi, v in ((398, 412, 0), (404, 420, 1), (340, 352, 1)):
            self.building(f"earth_sheep_{v}", lo, hi, sag=0)
        # The Gate: its arch of dark stone over the mouth of the way down; dead trees by it.
        gate_floor = self.top(mouth + 2) or m.GROUND_LINE
        self.stand("earth_gateway", mouth - 5, gate_floor, (mouth - 14, mouth + 4))
        self.life("glow", mouth - 5, gate_floor - 1, "earth_gate_glow")
        for x in (mouth - 18, mouth + 11):
            for dx in (0, 1, -1, 2):
                if self.tree("earth_dead_tree_" + str((x // 3) % 2), x + dx):
                    break
        # The Gate itself, in its hall under the ground.
        if getattr(w, "gate_arch", None):
            self.stand("earth_gate_arch", *w.gate_arch)
        # The graves of those who went down before, between the Gate and the stair.
        x, k = mouth + 16, 0
        while x < m.EAST - 18:
            top = self.solid_top(x)
            if top is not None and self.solid_top(x + 1) == top and not self.is_clear(x - 1, x + 1) and not self.has_mark(x - 1, x + 1, top - 3, top):
                self.stand(f"earth_grave_{int(m.hash01(x, 71) * 4)}", x, top, (x, x))
                k += 1
                x += 2 + int(m.hash01(x, 72) * 3)
            else:
                x += 1
        # The woods, last, wherever nothing was built: firs in the west and the east, a mixed
        # wood round the shaft, fewer trees in the village, an orchard by the fields.
        oaks = [f"earth_oak_{v}" for v in range(tv2_earth.OAKS - 1)] * 2 + [f"earth_oak_{tv2_earth.OAKS - 1}"]
        birches = [f"earth_birch_{v}" for v in range(2)]
        # The old oak, the landmark of the wood: on the widest level ground there.
        found = self.site(96, 150, 7, 1)
        if found:
            x0, floor = found
            self.stand("earth_giant_oak", x0 + 3, floor, (x0 - 4, x0 + 10))
            self.trees.append((x0 + 3, floor, "earth_giant_oak"))
        pines = [f"earth_pine_{v}" for v in range(tv2_earth.PINES)]
        shrubs = [f"earth_shrub_{v}" for v in range(tv2_earth.SHRUBS)]
        apples = [f"earth_apple_{v}" for v in range(tv2_earth.APPLES)]
        self.woods(20, 48, pines + pines + shrubs[:1], (3, 6), 301)
        self.woods(66, 178, oaks + pines + pines + birches + shrubs, (3, 7), 302)
        self.woods(178, start - 12, oaks + apples + birches + shrubs, (8, 13), 303)
        # Under the trees of the wood: logs, stumps, toadstools.
        for x in range(70, 176, 9):
            kind = ("earth_log", "earth_stump_0", "earth_stump_1", "earth_toadstools")[int(m.hash01(x, 401) * 4)]
            self.building(kind, x, x + 6, sag=0)
        self.woods(start - 12, start + 14, shrubs, (5, 9), 309)
        self.woods(start + 14, pond0 - 4, oaks[:3] + shrubs, (6, 10), 304)
        self.woods(m.POND[1] + 3, 314, oaks, (6, 9), 305)
        self.woods(336, 380, oaks[:3] + shrubs + apples, (9, 14), 306)
        self.woods(380, 426, apples + apples + shrubs, (4, 7), 307)
        self.woods(m.EAST - 18, m.WIDTH - 4, pines + pines + oaks[3:], (3, 6), 308)

    def lives_of_the_surface(self):
        """What moves on the surface and what comes with the hour: leaves from the autumn
        oaks, fireflies by the water and in the woods at night, butterflies over the meadows
        by day, the splash where the falls land in the pond, shafts of low sun through the
        trees at dawn and at sunset, and dark shapes close to the eye."""
        m = self.m
        for x, ground, key in self.trees:
            if key.endswith(f"oak_{tv2_earth.OAKS - 1}"):
                self.life("leaves", x, ground - 1, "-", 0, 96)
        pond0, pond1 = m.POND
        for x in (pond0 - 1, 110, 215, 384, 520):
            top = self.top(x)
            if top is not None:
                self.life("fireflies", x, top - 1, "-", 0, 24)
        for x in range(3, self.width - 3):
            top = self.top(x)
            if top is not None and self.t[top][x] == "Ñ" and m.hash01(x, 402) < 0.035:
                self.life("butterflies", x, top - 1, "-", 0, 16)
        # The splash: where the falling water meets the pond.
        for x in range(pond0, pond1):
            for y in range(m.GROUND_LINE - 16, m.SURFACE_LINE + 4):
                if self.t[y][x] == "|" and self.t[y + 1][x] == "W":
                    self.life("splash", x, y + 1, "-", 0, 13)
                    break
        # The pond's deep and the willow in it.
        water = next((y for y in range(m.GROUND_LINE - 10, m.SURFACE_LINE + 6) if self.t[y][pond0 + 4] == "W"), None)
        if water is not None:
            self.stand("earth_depth", (pond0 + pond1) // 2 - 1, water + 1)
            self.stand("earth_shine", (pond0 + pond1) // 2 - 1, water + 1)
            willow = next(((x, g) for x, g, k in self.trees if k == "earth_willow"), None)
            if willow and self.t[water][willow[0] + 4] == "W":
                self.stand("earth_reflection", willow[0] + 4, water + 1)
        for x in (110, 228, 372):
            top = self.top(x)
            if top is not None:
                self.life("rays", x, top - 1, "earth_rays")
        x, k = 24, 0
        while x < self.width - 24:
            top = self.top(x)
            if top is not None and not self.is_clear(x - 3, x + 3) and self.t[top][x] in self.ground:
                self.life("front", x, top - 1, f"earth_front_{k % 4}")
                k += 1
                x += 28 + int(m.hash01(x, 403) * 26)
            else:
                x += 1

    def more(self):
        """The third round: the graveyard, the fire by the start, paths, the damp, the boat,
        the birds and the rain."""
        m, w = self.m, self.w
        start = w.spawn[0]
        mouth = m.EAST - 70
        # The graveyard: a crypt at its west end, iron railing, crows on two headstones.
        self.building("earth_crypt", mouth + 12, mouth + 22, sag=0)
        for lo, hi, v in ((mouth + 6, mouth + 14, 0), (mouth + 20, mouth + 30, 0), (mouth + 34, mouth + 46, 1)):
            self.building(f"earth_iron_fence_{v}", lo, hi, sag=1, foot=2)
        graves = [mk for mk in w.marks if mk[0] == PICTURE and mk[3][0].startswith("earth_grave_") and mk[1] > mouth]
        for k, mk in enumerate(graves[1:6:3]):
            if mk[3][0] in ("earth_grave_0", "earth_grave_1"):
                self.w.mark(PICTURE, mk[1], mk[2], f"earth_crow_{k % 2}")
        # A fire by the start, a log to sit on by it.
        for dx in (-5, -6, -4, -7):
            x = start + dx
            top = self.solid_top(x)
            if top is not None and self.t[top - 1][x] == AIR and not self.has_mark(x, x, top - 2, top - 1):
                self.t[top - 1][x] = "Å"
                self.building("earth_log", x - 5, x - 1, sag=0)
                break
        # Paths: earth trodden from the village to the pond and on to the mill, on the level.
        for lo, hi in ((200, start + 14), (m.POND[1] + 2, 345)):
            for x in range(lo, hi):
                if self.is_clear(x, x):
                    continue
                y = self.top(x)
                if y is None or self.t[y][x] not in "GÑ":
                    continue
                if self.top(x - 1) == y and self.top(x + 1) == y:
                    self.t[y][x] = "ü"
        # Mushrooms that glow, in the damp by the water of the caves under the Earth.
        glowing, last = 0, -99
        dark = [(mk[1], mk[2], int(mk[3][0]), int(mk[3][1])) for mk in w.marks if mk[0] == "dark"]
        for x in range(30, self.width - 30):
            if x - last < 14:
                continue
            for y in range(m.SURFACE_LINE + 4, next(r for k, r in m.BANDS if k == "limbo") - 4):
                if self.t[y][x] != AIR or self.t[y + 1][x] not in self.ground or ":" in TERRAIN_CHARS[self.t[y + 1][x]]:
                    continue
                if any(dx0 - 30 <= x <= dx0 + dw + 30 and dy0 - 16 <= y <= dy0 + dh + 16 for dx0, dy0, dw, dh in dark):
                    continue
                wet = any(self.t[y + dy][x + dx] == "W" for dx in range(-4, 5) for dy in range(-2, 3))
                if wet and (x, y) not in self.marked:
                    self.t[y][x] = "å"
                    glowing += 1
                    last = x
                    break
        # The boat on the mine's pool.
        boat = None
        for y in range(m.SURFACE_LINE + 4, next(r for k, r in m.BANDS if k == "limbo") - 4):
            run = [x for x in range(60, m.EAST - 130) if self.t[y][x] == "W" and self.t[y - 1][x] == AIR and self.walls[y][x] != AIR]
            if len(run) >= 8:
                boat = (run[len(run) // 2], y)
                break
        if boat:
            self.stand("earth_boat", boat[0], boat[1])
        # Birds far off over the surface; rain now and then.
        for x in (90, 250, 420):
            self.life("birds", x, m.GROUND_LINE - 16)
        self.life("rain", start, m.GROUND_LINE - 12)
        return glowing

    def roots(self):
        """Roots out of the roof of a cave or tunnel that lies under a tree."""
        made = 0
        for x, ground, key in self.trees:
            for y in range(ground + 2, ground + 14):
                if self.t[y][x] == AIR and self.t[y - 1][x] in self.ground and self.t[y + 1][x] == AIR:
                    self.stand(f"earth_roots_{int(self.m.hash01(x, 404) * 3)}", x, y + 1)
                    made += 1
                    break
        return made

    # --- The mine --------------------------------------------------------------------

    def tunnel(self, x: int, floor: int) -> int:
        """How many cells of walled air stand over a cell of floor in the mine (0 if it is no
        floor of a tunnel there)."""
        if self.t[floor][x] not in self.ground or ":" in TERRAIN_CHARS[self.t[floor][x]]:
            return 0
        rows = 0
        y = floor - 1
        while y > 0 and self.t[y][x] == AIR and self.walls[y][x] in ("s", "d", "Ø"):
            rows += 1
            y -= 1
        return rows if self.t[y][x] in self.ground else 0

    def mine(self):
        m = self.m
        top = m.SURFACE_LINE + 2
        bottom = next(row for key, row in m.BANDS if key == "limbo") - 4
        posts = rails = carts = heaps = 0
        for floor in range(top, bottom):
            run = []
            for x in range(3, self.width - 3):
                rows = self.tunnel(x, floor)
                level = rows >= 3 and self.t[floor - 1][x] == AIR and not self.has_mark(x, x, floor - 2, floor - 1)
                # Timber every seven columns or so, where the tunnel is low enough to need it.
                if (
                    rows in tv2_earth.POST_HEIGHTS
                    and not any(abs(px - x) < 7 and abs(py - floor) < 8 for px, py, _ in self.posts)
                    and self.tunnel(x - 1, floor) and self.tunnel(x + 1, floor)
                    and all(self.t[y][x] == AIR for y in range(floor - rows, floor))
                    and not self.has_mark(x - 1, x + 1, floor - rows, floor)
                ):
                    self.stand(f"earth_post_{rows}", x, floor)
                    self.posts.append((x, floor, rows))
                    posts += 1
                    # The roof over the timber is boarded (lagging), a few columns either side.
                    for bx in range(x - 5, x + 6):
                        roof = next((y for y in range(floor - 1, 0, -1) if self.t[y][bx] in self.ground), None)
                        if roof is None or floor - roof > 13:
                            continue
                        for y in (roof, roof + 1, roof + 2):
                            if self.walls[y][bx] in ("s", "d") and (y > roof or self.t[y][bx] == AIR):
                                self.walls[y][bx] = "Ø"
                if level and 60 <= x <= m.EAST - 130:
                    run.append(x)
                    continue
                rails += self.track(run, floor)
                carts += self.cart(run, floor)
                run = []
            rails += self.track(run, floor)
            carts += self.cart(run, floor)
        # What the miners left, on the floors of the halls and wider tunnels.
        x, last = 66, -99
        while x < m.EAST - 20:
            x += 1
            if x - last < 22 + int(m.hash01(last, 10) * 12):
                continue
            for floor in range(top, bottom):
                rows = self.tunnel(x, floor)
                cells = [(c, floor) for c in (x - 1, x, x + 1, x + 2)]
                if (
                    rows >= 4
                    and all(self.tunnel(c, floor) >= 3 for c in (x - 1, x + 1, x + 2))
                    and not self.has_mark(x - 1, x + 2, floor - 4, floor)
                    and not any(cell in self.track_cells for cell in cells)
                ):
                    if all(self.t[floor - 1][c] == AIR for c in (x - 1, x, x + 1, x + 2)):
                        self.stand(f"earth_crates_{int(m.hash01(x, floor, 9) * 3)}", x, floor)
                        heaps += 1
                        last = x
                        break
        # Every other torch of the mine is a miner's lamp hung from the cap of the nearest
        # timber instead (the same lamp moved a few columns at most, so the light of the road
        # and its dark stretches stay as they were).
        lamps = 0
        for y in range(top, bottom):
            for x in range(60, m.EAST - 20):
                if self.t[y][x] != "T" or m.hash01(x, y, 405) >= 0.5:
                    continue
                near = sorted((abs(px - x), px, pf, pr) for px, pf, pr in self.posts if abs(px - x) <= 4 and pf - pr <= y < pf)
                for _, px, pf, pr in near:
                    cell = pf - pr
                    if self.t[cell][px] == AIR and (px, cell) not in self.marked:
                        self.t[y][x] = AIR
                        self.t[cell][px] = "ö"
                        lamps += 1
                        break
        # Rubble where the roof came down, puddles under a drip, copper by the road.
        x, last = 64, -99
        made = {"rubble": 0, "puddle": 0, "nugget": 0}
        kinds = ["rubble", "puddle", "nugget", "nugget", "puddle"]
        while x < m.EAST - 24:
            x += 1
            if x - last < 13:
                continue
            for floor in range(top, bottom):
                if self.tunnel(x, floor) < 3:
                    continue
                cells = [(c, floor) for c in range(x - 1, x + 3)]
                if any(cell in self.track_cells for cell in cells) or self.has_mark(x - 2, x + 3, floor - 4, floor):
                    continue
                if not all(self.tunnel(c, floor) >= 3 and self.t[floor - 1][c] == AIR for c in range(x - 1, x + 3)):
                    continue
                kind = kinds[int(m.hash01(x, floor, 406) * len(kinds))]
                key = {"rubble": f"earth_rubble_{x % 2}", "puddle": "earth_puddle", "nugget": f"earth_nugget_{x % 2}"}[kind]
                self.stand(key, x, floor)
                made[kind] += 1
                if kind == "puddle":
                    roof = next(y for y in range(floor - 1, 0, -1) if self.t[y][x] in self.ground)
                    self.life("drip", x, roof + 1, "-", 0, 16)
                last = x
                break
        # The lift's cage, hung in the old shaft under its pithead.
        shaft = next((mk[1] + 3 for mk in self.w.marks if mk[0] == "sign" and mk[3][1:4] == ["The", "old", "shaft."]), None)
        if shaft is not None:
            top_row = next(y for y in range(self.m.GROUND_LINE - 20, bottom) if self.t[y][shaft + 1] == '"')
            self.stand("earth_cage", shaft + 2, top_row + 5)
        self.mined = (lamps, made)
        return posts, rails, carts, heaps

    def track(self, run: list[int], floor: int) -> int:
        """Track along a level run of the mine's floor at least six columns long."""
        if len(run) < 6:
            return 0
        for x in run:
            key = "earth_rail_start" if x == run[0] else "earth_rail_end" if x == run[-1] else "earth_rail_mid"
            self.stand(key, x, floor)
        return len(run)

    def cart(self, run: list[int], floor: int) -> int:
        """A cart on one run of track in three, now and then one tipped over at its end."""
        if len(run) < 9 or self.m.hash01(run[0], floor, 11) > 0.6:
            return 0
        tipped = self.m.hash01(run[0], floor, 12) < 0.3
        x = run[-3] if tipped else run[len(run) // 2]
        self.stand(f"earth_minecart_{1 if tipped else 0}", x, floor)
        return 1


def dress(world, m, log=print):
    """Dresses the Earth of a world laid by make_world_map (`m`, the module)."""
    dresser = Dresser(world, m)
    dresser.meadows()
    dresser.surface()
    posts, rails, carts, heaps = dresser.mine()
    dresser.lives_of_the_surface()
    rooted = dresser.roots()
    glowing = dresser.more()
    start = world.spawn[0]
    world.view("earth_village", start - 50, m.GROUND_LINE - 6)
    world.view("earth_fields", 364, m.GROUND_LINE - 6)
    world.view("earth_graves", m.EAST - 38, m.GROUND_LINE - 6)
    world.view("earth_wood", 110, m.GROUND_LINE - 6)
    world.view("earth_mouth", m.EAST - 73, m.GROUND_LINE - 6)
    world.shots += [
        ("earth_village_dusk", "earth_village", 19.4),
        ("earth_fields_dawn", "earth_fields", 6.9),
        ("earth_night", "earth", 22.5),
    ]
    lamps, made = dresser.mined
    log(
        f"  the Earth dressed: {dresser.stood} pictures, {posts} sets of timber, {rails} tiles of track, {carts} carts, "
        f"{heaps} heaps, {lamps} hung lamps, {made}, roots under {rooted} trees, {glowing} glowing mushrooms; life: {dresser.lives}"
    )
    return dresser
