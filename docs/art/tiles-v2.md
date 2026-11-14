# Tiles V2: the map tile standard

A new tile system for the map, built apart from the current region art (which
stays as it is: `Assets/Art/Tiles`, `RegionTiles`, the region scenes). The
look is the classic side-view sandbox style: small square tiles that join on
a fixed frame set, flat-shaded materials with a dark outline, walls behind
the terrain and a layered parallax sky. The art is original, drawn by code;
it copies no other game's assets.

Everything here is made by `tools/tiles_v2/gen_tiles_v2.py`; the numbers in
this document are constants in that file, and the Unity side reads them from
the manifest it writes. Change the code and this page together.

## Grid and scale

| Rule | Value |
|---|---|
| Tile | 16 x 16 px |
| Pixels per unit | 16: one tile is one Unity unit and one collision cell |
| Camera | Pixel Perfect Camera, 320 x 180 (20 x 11.25 tiles), scaled by whole numbers |
| Filter | Point, no compression, no mipmaps |
| Density | One art pixel is one game pixel everywhere: tiles, walls, backdrops. Nothing is scaled, rotated or drawn at another density. |

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
  below the diagonal; their collider is the sprite's outline. Placed by hand.

Mask bits (y up): N 1, NE 2, E 4, SE 8, S 16, SW 32, W 64, NW 128. A frame's
sprite is `tv2_<material>_f<mask>_v<variant>`, a slope's
`tv2_<material>_<slope>_v<variant>`.

Sheet layout (`Assets/Art/TilesV2/<material>.png`): 8 columns; the 47 frames
in ascending mask order fill 6 rows, one such block per variant, then one row
of slopes (each slope's variants side by side).

## Light, outline, shading

- **Light comes from the top left.** An edge open to air on top gets a
  highlight row (ramp 5) under its outline and a dithered row under that; a
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
| grass (on dirt) | `173418` | `1f4a22 2e6a2c 3f8a35 56a83f 79c24f a6dc6a` |
| stone | `1c1d26` | `33353f 464955 5b5e6b 717482 8a8d9a a8abb6` |
| dirt wall | `1e120c` | `2c1b12 362218 40291d 4a3022 553828 60402e` |
| stone wall | `16171d` | `23242c 2b2d36 343641 3d3f4b 474a56 525562` |
| sand | `4a3a1c` | `8c7440 a88c4e c2a660 d6bc74 e6cf8a f3e2a8` |
| clay | `3a1a14` | `6a3024 84402c 9c5236 b4643f c97a4e dc9466` |
| copper (ore nuggets) | `3a1e10` | `6e3a1c 9a5428 c06e34 de8c44 f0ac62 ffd08a` |

Walls are the same frames at about half the value, with no highlight and a
soft two-pixel shade where they meet air, so they sit behind the terrain.

## Materials, blends, layers

| Material | Kind | Join group | Collider |
|---|---|---|---|
| dirt | terrain | earth | Grid (full cell); slopes: Sprite |
| grass | terrain: dirt with turf on every face open to air (deep on top, thin on the sides) | earth | Grid |
| stone | terrain | earth | Grid |
| sand | terrain: loose grains, wind ripples; slopes for dunes | earth | Grid; slopes: Sprite |
| clay | terrain: wavy strata | earth | Grid |
| copper_ore | terrain: stone with lit nuggets of copper (palette `copper`), never on the outline | earth | Grid |
| dirt_wall, stone_wall | wall | walls | none |

**Blends**: `dirt_on_stone`, `dirt_on_sand` and `dirt_on_clay` lay a ragged
band of dirt over each side of a stone, sand or clay tile that touches dirt or
grass (15 side masks x 3 variants), on the Blend tilemap; `sand_on_stone`
lays sand over stone the same way. One overlay a cell: the first blend in
`BLENDS` that applies wins. Ore joins stone with no blend. Descender > Tiles V2 > Paint blends recomputes it from the
Terrain tilemap; run it after painting.

**Tilemaps** under one Grid (cell size 1):

| Tilemap | sortingOrder | Holds |
|---|---|---|
| Walls | -300 | wall tiles, behind everything |
| Terrain | -100 | terrain tiles; TilemapCollider2D merged by a CompositeCollider2D, layer World |
| Blend | -95 | blend overlays, no collider |

**Backdrop**: four layers of 320 x 180 art at 16 PPU, drawn tiled sideways,
back to front: sky (scroll 0, pinned to the view), far mountains (0.1), hills
(0.25), near forest (0.45); a layer's scroll is the share of the camera's
movement it scrolls by (`ParallaxLayer`). Ridges are lit on their left-facing
slopes, the same light as the tiles.

## Light

Underground is dark, as in the classic sandbox games: light is worked out per
tile and spreads from the sky and from torches, dying fast in solid ground.
`TileLight` does the sums, `TileLightMap` shows them, the generator's
`--preview` runs the same sums.

1. **Sky.** Every column is lit with daylight (1) from the top down to its first
   solid tile or wall. A wall shades the sky, so a walled shaft is lit only by
   the light that spreads into it.
2. **Torches.** A torch puts `(1, 0.72, 0.42) x 1.6` on its tile.
3. **Spread.** Each tile ends with the brightest of its own light and of its
   neighbours' light times the share it keeps: **0.9** for air (walls behind it
   included), **0.7** for a solid tile. That is 3 sweeps of the map in all four
   directions, so light follows tunnels round two corners. The surface stays bright
   about 3 tiles down, and a torch reaches about 20 tiles of open cave.
4. **Show.** The result is one Sprite Light2D (blend style 0, Multiply) whose
   texture has one texel per tile (RGBA half), filtered bilinearly between the
   tile centres. It covers the map's tiles plus 2 rows of sky.

| Light | Value |
|---|---|
| Ambient | one Global light, intensity 0.04, colour `(0.55, 0.62, 0.85)`: deep rock is near black |
| Tile light | the Sprite light above, intensity 1 |
| Torch glow | Point, the torch colour, intensity 0.35, radius 3 tiles. It wavers within 8% (`TorchFlame`) and switches off past 30 units from the camera: the flicker on top of the steady tile light |
| Backdrop | Sprite-Unlit: the far daylit world, untouched by the scene's lights |

The light is worked out in `OnEnable`, in the editor too. A game that changes
tiles or torches calls `TileLightMap.Refresh()` afterwards. On a 64 x 30 map this
costs well under a millisecond, and its cost grows with the map's area.

The torch is a prop sheet (`torch.png`, 3 flame frames), drawn unlit: its flame
is the light. On the sample map it is `T`, on a wall cell that is air in the
terrain.

## In Unity

- `Descender > Tiles V2 > Build tiles` makes one `TerrainTile` (a Rule Tile
  that treats its join group as "this" and picks variants by hash) per
  material under `Assets/Art/TilesV2/RuleTiles/`, one per slope and one per
  blend mask. It loads and refills an existing asset, so GUIDs stay.
- `Descender > Tiles V2 > Build sample scene` lays out
  `Assets/Art/TilesV2/sample_map.txt` in `Assets/Scenes/Sandbox/TilesV2Sample.unity`
  (not in the build).
- `Descender > Tiles V2 > Capture sample` renders the surface, the cave and
  the whole map to `Logs/TilesV2/`; `TilesV2Capture.BuildAndCapture` does all
  three from the command line.

## Adding a material

1. Add its palette to `PALETTES` (outline plus a ramp of six) and an entry to
   `MATERIALS` (key, `terrain` or `wall`, a texture function, join group,
   slopes or not). A new look of inside is a `texture_*` function returning
   ramp indices 1-4.
2. If it should blend into another material, add an entry to `BLENDS`.
3. Run `python tools/tiles_v2/gen_tiles_v2.py --preview scratch/v2.png` and
   look at the preview, then `Descender > Tiles V2 > Build tiles` in Unity.
4. `TilesV2Tests` checks that every material has a rule a frame with all its
   variants.
