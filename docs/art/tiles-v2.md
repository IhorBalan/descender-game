# Tiles V2: the map tile standard

A new tile system for the map, built apart from the current region art (which
stays as it is: `Assets/Art/Tiles`, `RegionTiles`, the region scenes). The
look is the classic side-view sandbox style: small square tiles that join on
a fixed frame set, flat-shaded materials with a dark outline, walls behind
the terrain, light worked out tile by tile, and a layered parallax sky. The
art is original, drawn by code; it copies no other game's assets.

Everything here is made by the Python tools under `tools/tiles_v2/`; the
numbers in this document are constants there, and the Unity side reads them
from the manifest the generator writes (`Assets/Art/TilesV2/tiles_v2.json`).
Change the code and this page together.

| Step | What | Where |
|---|---|---|
| 1 | Draw the sheets and write the manifest | `python tools/tiles_v2/gen_tiles_v2.py` |
| 2 | Make the tiles and the tile set from them | Descender > Tiles V2 > Build tiles |
| 3 | A world is built from a text map and the tile set when a scene loads | `TilesV2World` |

Pictures of the sample world are in `docs/screenshots/tiles-v2/`: its named
places (`surface`, `pond`, `tunnel`, `hall`, `ore`, `cavern`, `dunes`,
`trickle`, `lava`) as the camera frames them, its pictures at other hours
(`surface_sunset`, `surface_night`, `pond_morning`, `dunes_sunset`,
`tunnel_dusk`), the whole map (`map.png`), and a frame from the Windows
playground with the hero in it (`playground.png`). Five of them at 1920 x
1080, for showing the map, are in `docs/screenshots/tiles-v2/presentation/`.

## Grid and scale

| Rule | Value |
|---|---|
| Tile | 16 x 16 px |
| Pixels per unit | 16: one tile is one Unity unit and one collision cell |
| Camera | Pixel Perfect Camera, 640 x 360 (40 x 22.5 tiles), scaled by whole numbers: twice as far back as the game's own 320 x 180, so the world shows and not only the hero. `VIEW` in `tv2_base.py`; the sky and the backdrops are drawn for it and the scenes' cameras take it from the tile set |
| Filter | Point, no compression, no mipmaps |
| Density | One art pixel is one game pixel everywhere: tiles, walls, decor, backdrops, particles. Nothing is scaled, rotated or drawn at another density. |

## Frames

Every material has the same frame set, so a new material is a palette and a
texture, not new rules.

- **47 autotile frames**: the 8-neighbour "blob" set. A tile looks at its
  eight neighbours; an edge (N, E, S, W) is open or joined, and a corner
  (NE, SE, SW, NW) counts only when both edges beside it are joined. That
  leaves 47 distinct frames: the middle, 4 edges, outer corners, inner
  corners and every mix.
- **3 variants** of each frame, picked by a hash of the cell (not Perlin
  noise), so neighbours differ and repeats do not line up.
- **Slopes** (terrain only): `up_right` and `up_left`, 45 degrees, solid
  below the diagonal. A slope's collider is the clean triangle under its
  diagonal, written into the sheet's `.meta`, not the ragged outline of its
  pixels: the hero walks up it and stands on it. A slope goes on a one-tile
  step of the ground, on the lower side.

Mask bits (y up): N 1, NE 2, E 4, SE 8, S 16, SW 32, W 64, NW 128. A frame's
sprite is `tv2_<material>_f<mask>_v<variant>`, a slope's
`tv2_<material>_<slope>_v<variant>`.

Sheet layout (`Assets/Art/TilesV2/<material>.png`): 8 columns; the 47 frames
in ascending mask order fill 6 rows, one such block per variant, then one row
of slopes (each slope's variants side by side).

## Outline and shading

- **The art's light comes from the top left.** An edge open to air on top gets
  a highlight row (ramp 5) under its outline and a dithered row under that; a
  left edge a lighter rim; bottom and right edges a shade (ramp 1).
- **Outline**: a 1 px outline in the material's outline colour wherever the
  tile meets air (8-neighbour, so inner corners get one too). Outer corners
  are rounded by a morphological opening (2-3 px cut).
- **Joined edges have no outline.** Tiles of one join group (dirt, grass,
  stone: `earth`) meet seamlessly; a blend overlay hides the change of
  material.
- **Textures** use ramp 1-4 only; ramp 5 is kept for the lit rims so they read.

## Palettes

Each material draws with one ramp of six colours (darkest to brightest) and
one outline colour, nothing else.

| Material | Outline | Ramp |
|---|---|---|
| dirt | `2a1810` | `452a1c 5e3a26 7a4c31 93603d ad774c c6915e` |
| grass (on dirt) | `15301a` | `1b4426 28652e 3c8634 58a53c 82c04a b4da64`: hue-shifted, the shadows lean blue and the lit turf yellow |
| meadow grass (on dirt) | `1c3214` | `2a4a1c 3e6a26 5a8a2e 7ea83a a8c44a d2dc6a`: warmer than the grass |
| stone | `1c1d26` | `33353f 464955 5b5e6b 717482 8a8d9a a8abb6` |
| dirt wall | `1e120c` | `2c1b12 362218 40291d 4a3022 553828 60402e` |
| stone wall | `16171d` | `23242c 2b2d36 343641 3d3f4b 474a56 525562` |
| sand | `4a3a1c` | `8c7440 a88c4e c2a660 d6bc74 e6cf8a f3e2a8` |
| clay | `2e1712` | `4e2c22 62382a 764634 8a5640 9e684e b07c60`: muted, so it does not read as lava in the dark |
| copper (ore nuggets) | `3a1e10` | `6e3a1c 9a5428 c06e34 de8c44 f0ac62 ffd08a` |
| timber wall (the mine's boards) | `120c08` | `1e150e 251a12 2c1f16 33251a 3a2a1e 423024` |
| the Earth's cave rock (backdrop) | | `0d0a09 15110e 1d1814 262019 302820`: beds of earth, warmer than the circles' rock |

Walls are the same frames at about half the value, with no highlight and a
soft two-pixel shade where they meet air, and the whole wall layer is
multiplied by the wall tint `(0.68, 0.70, 0.80)`: a wall is further back than
the ground, so a passage reads darker than the rock round it. Decor draws
with the ramp of what it grows from (grass, stone, dirt) plus a few colours of
its own (petals, cactus green, mushroom caps); liquids have their own
(`tv2_liquid.py`).

## Materials and layers

| Material | Kind | Join group | Collider |
|---|---|---|---|
| dirt | terrain | earth | Grid (full cell); slopes: a triangle |
| grass | terrain: dirt with turf on every face open to air (deep on top, thin on the sides) | earth | Grid; slopes: a triangle |
| stone | terrain | earth | Grid; slopes: a triangle |
| sand | terrain: loose grains, wind ripples; slopes for dunes | earth | Grid; slopes: a triangle |
| clay | terrain: wavy strata | earth | Grid |
| copper_ore | terrain: stone with lit nuggets of copper (palette `copper`), never on the outline; gives a little light and glints | earth | Grid |
| meadow | terrain: dirt under a warmer turf, where the wild flowers grow thick; slopes | earth | Grid; slopes: a triangle |
| dirt_wall, stone_wall | wall | walls | none |
| timber_wall | wall: boards laid across (the `planks` texture), for the old mine's roofs | walls | none |

The world's regions (`docs/art/tiles-v2-world.md`) add a ground and a wall
each, drawn with the same frames from their own palettes:

| Region | Ground | Wall | Notes |
|---|---|---|---|
| Paradise | cloud (its own join group, `sky`; gives a soft light, so its underside is no shadow), marble (bricks, no slopes) | marble_wall | |
| Limbo | ashstone | ash_wall | |
| Lust | rose_rock | rose_wall | |
| Gluttony | mud | mud_wall | |
| Greed | darkstone, gold_ore (gives light, glints) | dark_wall | |
| Wrath | swamp_rock | swamp_wall | |
| Heresy | dis_brick (bricks, no slopes) | brick_wall | |
| Violence | bloodstone, ember_sand | blood_wall | |
| Fraud | slate | slate_wall | |
| Treachery | frost_rock, ice (gives a cold light) | ice_wall | |

Some walls stand under the open sky: a tree's trunk and its crown
(`wood_wall`, `leaf_wall`), Paradise's own trees (`birch_wall`, `blossom_wall`,
`gold_leaf_wall`) and a column of bright marble (`pillar_wall`). They are
**open** walls (`open` in `MATERIALS`, `TerrainTile.Open`): to the light and to
what grows the cell behind them has no wall, so a tree does not shut out the
sky and the grass under it is still under the sky; and they are laid on a
tilemap of their own (`Foliage`), in front of the other walls and not tinted
back, so blossom is as bright as it is drawn. Crowns are drawn with the
`leaves` texture (rows of small rounds), a column with `flutes`.

Paradise also adds `mist_wall` (the inside of a cloud: a wall like any other),
`shadow_wall` (marble the sun does not reach: a recess, the inside of a dome)
and `gold_brick`, gold in blocks, which gives a warm light: a roof of it has
no dark heart and shows from the Earth at night. Marble and gold have slopes,
so a roof is a line and not a stair. Three more open walls are for what is set
on a building: `glass_wall` and `rose_glass_wall`, stained glass (the `glass`
texture: panes in a lattice of lead; a glass's ramp is hues, not shades, the
first of them the lead), which lets the light through as a window does, and
`gilt_wall`, gold worked thin (the `plain` texture), for a rod on a spire, a
chain, a bell, a star.

A **ledge** is a thin platform: a strip 5 px deep at the top of its cell, on
its own tilemap on the game's Ledge layer with a one-way `PlatformEffector2D`,
so the hero jumps up through it and drops through it with down and jump (as he
does through the game's own ledges). Four kinds (`plank`, `grate`, `gilt`,
`rime`), each of four pieces picked by whether the ledge goes on to the left
and right. A ledge passes light and carries no decor.

A world is these tilemaps under one Grid (cell size 1) and the things round
them, all on the Default sorting layer, back to front:

| Layer | sortingOrder | Holds | Light |
|---|---|---|---|
| Sky | -1010 | the sky's bands, painted from the hour's two colours | none |
| Stars, sun and moon | -1008, -1006 | pinned to the view | none |
| Clouds | -1004 | drift along the top of the view | none |
| Heaven, far | -1000, -996 | over a world with regions: islands hanging far off | none |
| Ridges | -990, -980, -970 | far mountains, hills, near forest | none |
| Heaven, near | -966, -964 | a sea of cloud, shafts of light | none |
| Cave rock | -960 and up, 4 a region | the rock behind the caves, each region's own under the surface line; what stands far off in it; the ragged strip along its top | tile light |
| Walls | -300 | wall tiles, tinted back | tile light |
| Shade | -290 | the shade of the ground on the walls | tile light |
| Foliage | -285 | the open walls: trees, columns in the open; not tinted | tile light |
| Decor | -110 | what stands on or hangs from the ground | tile light |
| Terrain | -100 | ground tiles; TilemapCollider2D merged by a CompositeCollider2D, layer World | tile light |
| Ledges | -98 | the one-way ledges, layer Ledge | tile light |
| Gates | -97 | a world's light barriers, seals and slabs (`TilesV2Things`) | seals: tile light; a veil of light: none |
| Blend | -95 | blend overlays | tile light |
| Torches | -60 | the lamps: torches, braziers, crystals, lanterns | none: they are the light |
| Glow | -40 | glints of ore | none |
| (the hero) | 0 | | |
| Liquids | 20 to 26 | each liquid on its own tilemap, in front of the hero | water, the Styx, tar, slime: tile light; lava, blood and falling water: none |
| Cloud in front | 30 | wisps of the sea of cloud that pass in front of the hero | none |
| Particles | 60 | dust, embers, sand, sparks | dust and sand take the tile light |

## The map

A map is text (`TilesV2Map`), so a scene holds none of it:

```
# a comment
@surface 20
@view hall 54 33
@shot hall_night hall 23
................
..../GGG\.......      the first grid: what stands in each cell
GGGGDDDDDGGWWGGG
DDDDDDDDDDDDDDDD

................      a blank line, then the second grid: the wall behind each cell
................
................
dddddddddddddddd
```

Row 0 is the top. `@surface` is the row the cave rock starts at (under every
dip of the surface); `@view name column row` names a place by the middle of
the camera's view of it, for the captures and the playground (a view that
would show past the map's edge is moved inside it); `@shot name view hour`
names a picture worth taking: a place at an hour of the day. The characters
are the legend in the manifest:

| Character | Layer | Stands for |
|---|---|---|
| `G` `D` `S` `A` `C` `O` | terrain | grass, dirt, stone, sand, clay, copper ore |
| `/` `\` | terrain | grass slopes: up to the right, up to the left |
| `(` `)` | terrain | sand slopes |
| `{` `}` | terrain | dirt slopes |
| `[` `]` | terrain | stone slopes |
| `W` `L` | liquid | water, lava |
| `T` | prop | a wall torch |
| `P` | mark | the hero's start |
| `d` `s` | wall (second grid) | dirt wall, stone wall |
| `.` | | air |
| `Q` `M` `H` `R` `U` `N` `$` `Y` `B` `V` `E` `F` `I` `Z` | terrain | the regions' grounds: cloud, marble, ashstone, rose rock, mud, darkstone, gold ore, swamp rock, the brick of Dis, bloodstone, ember sand, slate, ice, frost rock |
| `1 2` `3 4` `5 6` `7 8` `9 0` `< >` `: ;` `+ -` `= _` | terrain | their slopes, up to the right and up to the left: cloud, ashstone, rose rock, mud, darkstone, swamp rock, bloodstone, slate, frost rock |
| `~` `!` `%` `&` | liquid | the Styx, blood, tar, slime |
| `*` `^` `J` | prop | a brazier, a crystal, a lantern |
| `"` `'` `,` `?` | ledge | a ledge of planks, an iron grate, gilt, rime |
| `m` `h` `r` `u` `n` `y` `b` `v` `f` `i` | wall (second grid) | the regions' walls |
| `w` `l` | wall (second grid) | a tree's trunk and its crown: open walls |
| `t` `p` `g` `x` | wall (second grid) | Paradise's open walls: a pale trunk, blossom, leaves of gold, a column |
| `o` `q` `X` | wall (second grid) | more of them: stained glass, cool and warm; gilt work |
| `c` `z` | wall (second grid) | the inside of a cloud; marble in shadow |
| `K` | terrain | gold in blocks |
| `a` `e` `j` `k` | terrain | the slopes of marble and of gold, up to the right and up to the left |
| `\|` | liquid | falling water |
| `Ñ` `ñ` `Ö` | terrain | the Earth's meadow in flower, and its slopes up to the right and up to the left |
| `Ø` | wall (second grid) | the boards of the old mine |
| `ö` | prop | a miner's lamp hung on a nail (the Earth's mine) |

A map of several regions also says where each starts and what of the game's
stands where:

```
@region limbo 172             the region `limbo` (the tile set's key) starts at row 150
@mark checkpoint 179 159      something of the game's in a cell: kind, column, row, and what the kind needs
@mark ability 111 175 LightDash
@mark barrier 104 173 4       a veil of light: its top cell, its height
@mark seal 120 322 bolt 1 4   a seal a bolt breaks (or `slam`: a slab a Smite breaks): its top left cell, width, height
@mark relic 136 162
```

A mark may also be `secret` (a wall the sword breaks, with a room behind it),
`platform` (one that gives way or comes and goes), `wind`, `upgrade` (a heart
or a draught of the game's), `sign` (a post, a tablet, a shade or a soul that
says a line), `dark` (a stretch whose air gives no light), `beacon` (a light
the hero lights by walking up to it), `sanctum` (a door that opens when enough
lights burn) or `picture` (one of the tile set's pictures stood in a cell): the
world's document lists them all. `TilesV2World` lays the tiles and takes the dark stretches;
`TilesV2Things.Build` stands the other marks in the world from the game's own
parts (its checkpoint, ability and upgrade pickup prefabs, a collider on the
LightBarrier layer, a `Breakable`, a `Crumble`, a `Blinker`, a `ForceZone`) and
from `TilesV2Sign` and `TilesV2Beacon`, which a scene's runner does at the
start of play.

The sample map, `Assets/Art/TilesV2/sample_map.txt` (128 x 64), is written by
`tools/tiles_v2/make_sample_map.py`: a meadow with a hill and a pond, the
mouth of a sloping tunnel, a shaft open to the sky, a desert of dunes; under
them a dirt cave, a torch-lit hall, a gallery of copper, a wide cavern with
no wall behind it and a lake on its floor, a cave under the dunes where sand
trickles from the roof, and a cavern of lava at the bottom.

## Laid by rule

Four layers are never painted by hand: `TilesV2Painter` lays them from the
cells alone, so painting again changes nothing and the generator's preview
shows the same map. For a map painted with the Tile Palette, Descender > Tiles
V2 > Paint by rule lays them on the tilemaps that are there.

**Blends.** `dirt_on_stone`, `dirt_on_sand` and `dirt_on_clay` lay a ragged
band of dirt over each side of a stone, sand or clay tile that touches dirt or
grass (15 side masks x 3 variants); `sand_on_stone` lays sand over stone the
same way. One overlay a cell: the first blend in `BLENDS` that applies wins.
Ore joins stone with no blend.

**Shade.** The ground casts a shade on the wall behind it. A wall cell with no
full tile in it shows the tile for the ground round it: every side that has
ground, and a corner only when neither side beside it has (46 masks). The
art's light comes from the top left, so ground above or to the left casts
further: the shade's alpha by distance in pixels is 0.50 0.42 0.34 0.26 0.18
0.11 0.05 under a tile, 0.44 0.35 0.26 0.18 0.11 0.05 to the right of one, 0.32
0.20 0.11 0.05 over or to the left of one, less off a corner; where two meet
they multiply. Behind a full tile that has open air beside it the wall gets
the full shade (0.52), seen through the tile's rounded corners.

**Decor.** For each open face of a full ground tile, at most one decor that
grows on its material, picked by a hash of the cell (`TilesV2Painter.Hash`,
the generator's `cell_hash`): first what stands on floors, then what hangs
from ceilings. A decor sprite is a tile wide and 2 px taller at each end, so
it tucks behind the ragged edge it stands on. Slopes carry none, and a torch's
cell and the hero's start stay clear. The rules (`DECOR` in `tv2_decor.py`),
in the order they are tried:

| Decor | Grows | On | Where | Share of fitting cells | Cells tall |
|---|---|---|---|---|---|
| flower (4) | floor | grass | open to the sky | 0.10 | 1 |
| bush (2) | floor | grass | sky | 0.05 | 1 |
| grass_tall (2) | floor | grass | sky | 0.20 | 1 |
| tuft (3) | floor | grass | sky | 0.38 | 1 |
| cactus (2) | floor | sand | sky | 0.11 | 2 to 3: base, middle with an arm, top |
| dry_bush (2) | floor | sand | sky | 0.08 | 1 |
| bones | floor | sand | anywhere | 0.02 | 1 |
| mushroom (3) | floor | dirt, clay, stone | cave | 0.06 | 1 |
| stalagmite (3) | floor | stone, copper ore | cave | 0.10 | 1 |
| pebble (3) | floor | stone, dirt, clay, copper ore | cave | 0.12 | 1 |
| vine (2) | ceiling | grass | anywhere | 0.55 | 1 to 3 |
| root (2) | ceiling | dirt | cave | 0.22 | 1 |
| stalactite (3) | ceiling | stone, copper ore | cave | 0.18 | 1 |
| wildflower (4) | floor | meadow | sky | 0.34 | 1: three to five poppies, daisies, cornflowers or buttercups, never one alone |
| grass_tall, tuft | floor | meadow | sky | 0.22, 0.30 | 1 |
| fern (2) | floor | grass | sky | 0.06 | 1 |
| field_stone (3) | floor | grass, meadow | sky | 0.04 | 1 |
| cobweb (2) | ceiling | stone, dirt, copper ore | cave | 0.05 | 1 |

The last five are the Earth's and come at the end of `DECOR`, after the regions' own, so
every rule before them keeps the share of the cells it had.

"Open to the sky" is the light's own test: nothing over the cell in its column
shades it.

**Glints.** Every full tile of a material that glints (copper ore) gets the
glint on the unlit Glow layer: nothing most of the time, and now and then a
dot and a cross of light on one of the nuggets of the variant the tile shows,
each cell starting the loop (26 frames at 10 a second) at its own moment.

## Liquids

A liquid (`LiquidTile`) fills whole cells. Its top cell shows the surface
frames (the liquid stands 3 px under the cell's top, with a ripple that
travels), every cell under it the body frames; 8 frames each.

| Liquid | Drawn | Frames a second | Share of light a tile passes on (r, g, b) | Light it gives | The hero |
|---|---|---|---|---|---|
| water | see-through (alpha 0.59), takes the tile light, in front of the hero | 7 | 0.74, 0.86, 0.93: the deep is blue | none | swims (the game's `Triggers.Water`) |
| lava | opaque, unlit | 4 | 0.86, 0.80, 0.72 | 1.5, 0.8, 0.3 | is hurt (the game's `HazardArea`) |
| styx | see-through, dark teal, takes the tile light | 5 | 0.60, 0.80, 0.76 | none | swims |
| blood | opaque, unlit | 4 | 0.84, 0.60, 0.60 | 0.8, 0.14, 0.12 | is hurt |
| tar | opaque, takes the tile light | 3 | 0.50, 0.50, 0.55 | none | is hurt |
| slime | see-through, green | 4 | 0.70, 0.88, 0.60 | 0.05, 0.12, 0.03 | swims |
| falls | see-through, unlit: streaks that run down (`fall`), the same from top to foot | 12 | 0.90, 0.94, 0.98 | 0.10, 0.13, 0.17 | walks through: no area is made for it |

A liquid is drawn `clear` (see-through, glints that come and go) or `thick`
(opaque, a crust and spots that swell): `LIQUIDS` in `tv2_liquid.py`.

An area is made over every stretch of a liquid: a run of columns whose liquid
spans the same rows, so its top is the true surface above it.

## Backdrop

`TilesV2Backdrops` makes the layers from the manifest's list. A layer's scroll
is the share of the camera's movement it scrolls by, each way on its own (0
stays on screen, 1 moves with the world); a layer that repeats jumps by whole
repeats to stay under the camera, and everything snaps to whole art pixels
(`ParallaxLayer`).

| Layer | Scroll across, up | Notes |
|---|---|---|
| sky, stars, sun, moon | pinned to the view | `Sky`: seven bands between the hour's two colours, meeting in an ordered 4 x 4 dither, painted on a texture the size of the camera's view; the sun and the moon cross the view on an arc |
| clouds | 0.04, 0 | along the top of the view; drift 0.25 units a second |
| ridges: mountains, hills, mid wood, near wood | 0.1 / 0.17 / 0.25 / 0.45, 1 | stand on the world's horizon, their feet 2 tiles over the surface line (`tv2_earth.py`). Further is paler, bluer and plainer: the mountains (1280 px wide) are the blue of the low sky, snow on the peaks, a band of haze at their foot; the hills (`backdrop_earth_hills`, 1920 px) carry a castle, two villages and a tower, each once; the woods mix round crowns and firs, lit on the left |
| cave rock | 0.55, 0.55 | a 128 px picture that tiles both ways, each region's own colours (`REGIONS`), shown only in that region's rows under the surface line: a sprite mask over the region, with a custom range so it cuts that region's picture and no other. It takes the tile light, so it is black until something lights the cave. A map that names no regions is all the Earth's |
| what stands far off | 0.78, 0.78 | a 640 x 360 picture for each region (`FAR`: columns, spires, fangs, heaps, bare trees, towers with lit windows, a viaduct, shards), a little lighter than the rock, over it and cut to the region as it is; nearer than the rock, so it moves more |
| cave rock's top | 0.55, 1 | a ragged strip along the top of each region's rock, so one region's rock runs into the next one's |
| heaven (`heaven`; only over a map with regions) | each its own | five layers, each standing `rise` tiles over the surface line for a camera on that line and keeping the camera company on its way up by the share `scrollUp` leaves (what is far follows it): islands far off (0.06 across, 0.25 up) and nearer (0.16, 0.4), a sea of cloud (0.5, 1; it drifts), shafts of light (0.3, 0.6), and wisps of cloud fixed in the world that drift in front of the hero. All but the light take the hour's tint |
| a picture (`picture`) | fixed in the world | stood by a map's mark, its foot in the mark's cell: a rainbow; the Earth's trees, buildings and the timber of its mine (below) |

A cell shows the cave rock when it has no wall and lies under the surface
line: leave the walls out of a cavern to open it onto the rock.

## The Earth's pictures

What the Earth is dressed with is drawn free, not on the tile grid: a picture
(`tv2_earth.py`) stood in a cell of air by the map (`@mark picture column row key`),
its foot the bottom middle of the cell. Its sprite's pivot is not its bottom but its
floor line (`pivot_of`): what is under that line (roots and the foot of a post, 16 px;
a building's plinth of stone, 2 or 3 tiles) goes on down behind the ground, so nothing
stands on air, and a building stands on a slope as well as on the level. None of it is
solid and none of it shades: it is lit by the tile light as the ground is.

| Order | Pictures |
|---|---|
| -287 | behind the decor and the ground, in front of the walls and their shade: trees (4 oaks, one in autumn colours, 3 firs, 2 apple trees, 2 birches, 2 dead trees, a willow, and the old oak, a landmark 15 tiles high), 3 thickets, a log, stumps, toadstools, a washing line, a kitchen garden, a bench, a water butt, a kennel, two cottages (a ruin with its roof fallen in, and one still thatched), a chapel without its roof, a well, a windmill, fences, a cart, haystacks, a scarecrow, 4 kinds of grave, the Gate's arch, the pithead and its winch hut, the colonnade by the start, reeds, a jetty; in the mine, sets of timber for tunnels 3 to 12 rows high, crates, sacks, a ladder, rubble, lumps of copper, the lift's cage in the old shaft, roots hanging from a roof under a tree |
| -99 | in front of the ground they lie on: the mine's track and its carts, puddles |
| 27 | on the water, in front of the hero as the water is: lilies, the pond's deep, the willow in it |

How they are drawn (the research notes, `earth-art-research.md` in the session's notes):
the light from the top left; ramps that shift hue (shadows cool, the lit side warm); a
crown is a few rounded bundles shaded as balls, each with a dark rim where it lies over
the one behind and a gap or two where the branches show, never one ball on a stick; the
props of the background darker and less saturated than the ground the hero walks on.

## Life

What moves and what comes with the hour is made by `TilesV2Life` from the map's `life`
marks (`@mark life column row kind picture dx dy`, `dx` and `dy` art pixels from the foot
of the cell), built with the world; the clock tells it the hour (`DayNight.Applied`).

| Kind | What | When |
|---|---|---|
| smoke | grey pixels from a chimney, leaning with a light wind | always |
| leaves | from an autumn crown, falling slowly and rocking | always |
| fireflies | points of light wandering low (unlit) | their rate goes with the night |
| butterflies | bright specks over a meadow | with the day |
| splash | drops thrown up where falling water meets a pool | always |
| drip | a drop now and then from a mine's roof, over a puddle | always |
| sails | a mill's sails, four frames of a quarter turn (`SailFrames` a second) | always |
| glow | an unlit picture shown only at dusk and at night: a lit window, the glow in the Gate | `NightAt`: full from 20:30 to 5:00, none from 7:30 to 18:00 |
| rays | shafts of low sun, unlit and faint | `DawnAt`: at their most at 7:00 and 18:40 |
| front | a dark shape close to the eye (tall grass, a bough), drawn in front of the hero (order 40) and moving 1.3 times as fast as the world across (`ParallaxLayer` may scroll up to 2 across) | always |

The lights of the far villages (`backdrop_earth_windows`, a layer of heaven placed exactly
as the hills are but not tinted) come on with the night as the glows do.

## Light

Underground is dark, as in the classic sandbox games: light is worked out per
tile and per colour channel, and spreads from the sky and from what gives
light. `TileLight` does the sums, `TileLightMap` shows them, `tv2_light.py`
runs the same sums for the preview. 1 is full daylight.

1. **Sky.** Every column is lit from the top down to its first cell that
   shades the sky: a ground tile, a wall or a liquid. So a walled shaft and a
   pond's depths are lit only by the light that spreads into them. An open
   wall (a tree) shades nothing. In a map of regions the sky is also
   everywhere over the surface line that nothing shades
   (`TileLightMap.SetOpenSky`, `sky_seeds`' `surface`): an island of Paradise
   does not put the land under it in the dark.
2. **Sources.** The air of a region has a faint light of its own
   (`REGIONS`: none on the Earth and in Paradise, where the sky and the lamps
   do the work; a colour for each circle), given by every cell of air in the
   region's rows and turning into the next region's over 6 rows either side of
   the edge between them (`TilesV2World.AirLight`). The air of a stretch a
   map marks `dark` gives none (`TileLightMap.SetDark`): there the lamps and
   what spreads in from beside it are all the light there is. The lamps are four props
   (`PROPS`): a torch `(1, 0.72, 0.42) x 1.6`, a miner's lamp `(1, 0.78, 0.48) x 1.5`, a brazier `(1, 0.60, 0.32) x 2`,
   a crystal `(0.55, 0.82, 1) x 1.3`, a lantern `(1, 0.93, 0.72) x 1.2`. A torch puts `(1, 0.72, 0.42) x 1.6` on its tile, lava `(1.5,
   0.8, 0.3)` on each of its tiles, copper ore `(0.30, 0.17, 0.07)`.
3. **Spread.** Each tile ends with the brightest of its own light and of each
   of its eight neighbours' light times the share that neighbour's medium
   keeps: **0.91** for air (a wall behind it changes nothing), **0.56** for a
   ground tile, a liquid's own share per channel. A diagonal step takes the
   share to the power of the square root of two, its true length, so a lone
   light is round and not a diamond; a diagonal between two solid tiles is
   shut. The sweeps repeat until nothing changes. The top tile of the ground
   is bright (0.91) and the fourth under it dark (0.16); a torch still gives a
   tenth of full daylight 29 tiles down an open cave, and nothing 6 tiles
   into rock.
4. **The hour.** The sky and the sources are spread apart. The light shown is
   the brighter of the sky's half times the daylight's colour and the
   sources' half, so the hour changes the daylight without spreading again.
5. **Show.** The result is one Sprite Light2D (blend style 0, Multiply) whose
   texture has one texel per tile (RGBA half, alpha 1), filtered bilinearly
   between the tile centres. It covers the map's tiles plus 4 rows of sky.

| Light | Value |
|---|---|
| Ambient | one Global light, intensity 0.04, colour `(0.55, 0.62, 0.85)`: deep rock is near black. A world in a scene with its own global light is told not to make it. |
| Tile light | the Sprite light above, intensity 1 |
| Torch glow | Point, the torch colour, intensity 0.35, radius 3 tiles. It wavers within 8% (`TorchFlame`) and switches off past 30 units from the camera: the flicker on top of the steady tile light |
| Unlit | the far backdrop, flames, lava, glints, embers and sparks |

A game that changes tiles or torches calls `TileLightMap.Refresh()`
afterwards; its cost grows with the map's area (two spreads of the grid).

## Day and night

`DayNight` runs the clock: a whole day takes 480 seconds of play and a scene
starts at 10:00. The hour's values are mixed between these keys, round the
clock (`DAY_KEYS` in `tv2_base.py`):

| Hour | Daylight (what the sky's half of the light is multiplied by) | Sky, top to low | Tint of ridges and clouds | Stars |
|---|---|---|---|---|
| 0:00 to 4:30 | 0.30, 0.36, 0.60 | `04060f` to `101a36` | 0.20, 0.25, 0.44 | 1 |
| 5:45 | 0.42, 0.36, 0.44 | `1a2250` to `c9705a` | 0.50, 0.42, 0.52 | 0.5 |
| 6:45 | 0.86, 0.66, 0.52 | `2e4a96` to `f2a86c` | 0.95, 0.74, 0.64 | 0 |
| 8:30 | 1, 0.96, 0.90 | `3868bc` to `b6dcf0` | 1, 0.97, 0.93 | 0 |
| 12:00 | 1, 1, 1 | `3b6fc0` to `a9d6ef` | 1, 1, 1 | 0 |
| 16:30 | 1, 0.97, 0.90 | `3a69b6` to `b9d8ea` | 1, 0.96, 0.90 | 0 |
| 18:30 | 0.95, 0.64, 0.44 | `2c2f78` to `f08c4e` | 0.96, 0.66, 0.52 | 0 |
| 19:45 | 0.44, 0.34, 0.46 | `141440` to `7a3f5c` | 0.48, 0.38, 0.52 | 0.5 |
| 21:00 | 0.30, 0.36, 0.60 | `04060f` to `101a36` | 0.20, 0.25, 0.44 | 1 |

The sun is up from 5:30 to 19:30 and the moon the rest of the clock. A torch
shows more at night, because the light is the brighter of the two halves.

## Particles

`TilesV2Particles` sets every system up in code from the map; a particle is
one art pixel. They are pushed by a force of their own (the project has no 3D
physics for a particle system to take gravity from).

| Particles | Where | Light |
|---|---|---|
| Dust | drifts in a circle of 4.5 tiles round every lamp, up to 14 motes | takes the tile light: it shows only where there is light |
| A region's air | motes born all over the view, riding with the camera; only the region the camera is in gives (`TilesV2Air`): pollen on the Earth (it takes the tile light: seen in the sun and by the lamps), gold motes in Paradise, a wind of petals in Lust, rain in Gluttony, embers in Heresy, snow in Treachery | as `REGIONS` says for each |
| Embers | rise from every flame | none |
| Sand | trickles from under 45% of the sand tiles that hang over two or more empty cells (a hash of the cell picks them), and lands on what is below | takes the tile light |
| Sparks | jump from every run of the surface of a liquid that glows | none |

## In Unity

- **Descender > Tiles V2 > Build tiles** makes, under
  `Assets/Art/TilesV2/RuleTiles/`, one `TerrainTile` per material (a Rule
  Tile that treats its join group as "this" and picks variants by hash), one
  per slope and one per blend mask, the decor and shade tiles, the
  `LiquidTile`s and the `GlintTile`; then `TilesV2Set.asset`, the one asset a
  world is built from (the legend's tiles, the rules, the liquids, the
  backdrops and the numbers of the light and of the day); then the Tile
  Palette. It loads and refills an existing asset, so GUIDs stay.
- **`TilesV2World`** builds a world from a map (a `TextAsset`) and the tile
  set, the way `RegionTiles` builds a region: when the game starts, and in the
  editor as a preview that is never saved (Build on its context menu builds
  it again after a change to the map). So a scene with a whole world in it is
  a few lines.
- **Descender > Tiles V2 > Build sample scenes** makes the two scenes under
  `Assets/Scenes/Sandbox/` from the sample map. Neither is in the build's
  scene list. `TilesV2Sample.unity` is the map alone with a camera;
  `TilesV2Playground.unity` is the same map made playable.
- **Descender > Tiles V2 > Paint by rule** lays blends, shade, decor and
  glints on a map painted by hand.
- **Descender > Tiles V2 > Capture sample** renders every named place of the
  map at the day's start, every picture of it (`@shot`) and the whole map, at
  one art pixel a pixel, to `Logs/TilesV2/`; `TilesV2Capture.BuildAndCapture`
  does it all from the command line.

### The playground

`TilesV2Playground.unity` stands on its own: the hero's prefab with every
ability, the game's camera (`GameCamera`), its input and its sounds, in the
sample world, with no title, no saves and no HUD. Open it and press Play, or
build a Windows player of it alone with **Descender > Tiles V2 > Build Windows
playground** (`Build/TilesV2Windows64/DescenderTilesV2.exe`; the build's scene
list never hears of it).

| Key | Does |
|---|---|
| A D or arrows, Space, Shift, J | move, jump, dash, sword: the game's own keys |
| 1 2 3 4 | morning, noon, sunset, night |
| T (held) | the day runs 60 times faster |
| P | stops and starts the day |
| PgDn, PgUp | the next and the last named place of the map |
| R | back to the start |
| F2 | puts out and lights the hero's lantern, a small light the playground gives him so he can be seen in a cave no torch reaches (it is not part of the standard: it shines through rock) |
| F1 | hides the help |
| Esc | quits |

Started with `-tilesV2Shots <folder>` the player photographs the hero at the
start, every named place and every picture of the map instead, and quits:
that is how a build is checked. It asks the camera for the frames itself, so `-batchmode` works and
no window opens.

## Tools

| Module | Does |
|---|---|
| `gen_tiles_v2.py` | the entry: writes every sheet, its `.meta` and the manifest; `--preview out.png` lays the sample map out the way Unity shows it (`--hour 19`, `--view hall`, `--scale 4`, `--unlit`) |
| `tv2_base.py` | the shared vocabulary: palettes, materials, blends, the map's legend, the hash, the numbers of the light and of the day |
| `tv2_terrain.py` | textures, the 47 frames, slopes, blends, the lamps, the ledges, the things a map marks |
| `tv2_decor.py` | decor sprites and rules, the scatter, the shade tiles, the glints |
| `tv2_liquid.py` | the liquids |
| `tv2_backdrop.py` | the sky's bands, clouds, stars, sun, moon, ridges, the cave rock |
| `tv2_earth.py` | the Earth's pictures (trees, buildings, the mine's timber and track), its ridges, its cave rock and old workings |
| `tv2_earth_map.py` | where the Earth's pictures stand, its meadows, the mine's boards (called by `make_world_map.build`) |
| `tv2_light.py` | the tile light's sums |
| `tv2_preview.py` | the preview |
| `make_sample_map.py` | writes the sample map |
| `make_world_map.py` | writes the world (`docs/art/tiles-v2-world.md`); `--check` plays it through first |
| `tv2_reach.py` | the model of the hero the world is checked with |
| `photo_world.py` | photographs a whole map from the Unity editor at every art pixel, for looking it over zoomed in: one picture, one for each region, and 4K sheets; and the same with every mark of the map named on it, with the list of the names |

`gen_tiles_v2.py --map Assets/Art/TilesV2/world_map.txt --preview out.png
--scale 0.25` lays the whole world out at 4 px a tile; `--view limbo` shows the
camera's view of one of its places.

A new file under `Assets/` gets its `.meta` with a GUID derived from its path;
an existing `.meta` is never touched. When a sheet's sprites change, delete
its `.meta` and run the generator: the GUID and the sprite ids are derived
from names, so every reference stays.

## Adding to it

- **A material**: add its palette to `PALETTES` (outline plus a ramp of six)
  and an entry to `MATERIALS` (key, `terrain` or `wall`, a texture function,
  join group, slopes or not, `emits` if it gives light); a character for it
  in `LEGEND`; a blend in `BLENDS` if it should reach over another material.
- **A decor**: a function in `tv2_decor.py` that draws it on a `Pad`, its
  name in `DRAWERS`, and a rule in `DECOR`. The order of the rules is the
  order they are tried in.
- **A liquid**: an entry in `LIQUIDS` and a frame function in `DRAW`, and a
  character in `LEGEND`.
- **A region**: its palettes, ground and wall, an entry in `REGIONS` (name,
  the light in its air, the colours of its cave rock, what drifts in its air),
  decor rules for its ground, and a theme in `make_world_map.py`.
- Then run the generator with `--preview`, look, and build the tiles in
  Unity. `TilesV2Tests` and `TilesV2LightTests` hold the standard to its
  rules; `TilesV2PlaygroundTests` walks the hero through the sample world;
  `TilesV2WorldTests` and `TilesV2DescentTests` hold the world.
