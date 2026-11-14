# The Tiles V2 world

One map with Paradise in the sky, the Earth under it and the nine circles of
Hell under that, walked from end to end without a loading screen. It is built on
the Tiles V2 standard (`docs/art/tiles-v2.md`) and lives beside the sample map:

| | |
|---|---|
| Map | `Assets/Art/TilesV2/world_map.txt`, 208 x 624 tiles: 5 screens wide and 28 tall at the camera's 40 x 22.5 tiles, 130 000 cells (for scale, the whole world of Animal Well is 640 x 360 tiles of the same screen) |
| Written by | `python tools/tiles_v2/make_world_map.py --check` (edit that, not the map) |
| Checked by | `tools/tiles_v2/tv2_reach.py`: the world is played through in a model of the hero before it is written |
| Scene | `Assets/Scenes/Sandbox/TilesV2Descent.unity` (never in the build's scene list) |
| Windows player | **Descender > Tiles V2 > Build Windows world**, `Build/TilesV2World64/DescenderWorld.exe` |
| Pictures | `docs/screenshots/tiles-v2/world/` |

The game itself is untouched: no region scene, tile, prefab or flow of the game
knows of this world. It borrows the game's hero, camera, input, checkpoint and
ability pickup, as the playground does.

## The shape of the world

```
 rows
   0  PARADISE      islands of cloud; the temple and the gate at the end of everything
  62  EARTH         the surface (the start), the old mine, the hall of the Gate
 150  1 LIMBO       <- west    ash galleries        Light Dash
 206  2 LUST        east ->    the tempest's shafts Wall Grip
 254  3 GLUTTONY    <- west    mud, rain, slime
 298  4 GREED       east ->    vaults and gold      Holy Light
 350  5 WRATH       <- west    the marsh of Styx
 398  6 HERESY      east ->    the city of Dis      Glide
 454  7 VIOLENCE    <- west    blood, wood, sand    Smite
 506  8 FRAUD       east ->    ditches and bridges
 554  9 TREACHERY   <- west    the frozen lake      Wings
        |
        +-- the Pit: a shaft down the west edge from the Earth to the ninth circle
```

- **The road winds down.** Each circle is crossed from one end to the other and
  the way down is at its far end, so the circles are walked west and east in
  turn. One main direction per circle is its own signpost: what leaves the road
  up or down is a side road.
- **The Pit is the spine.** Every circle has an antechamber at its west end: a
  place to rest and a ladder of ledges up into a tunnel to the Pit. The ledges
  are six rows apart, more than a jump and less than two, so only the Wings climb
  them; and the Wings wait at the very bottom of the ninth circle, in a room
  whose only other way out is the way in. Until then the Pit is a sight (its
  ladder hangs in every antechamber: the lock before the key); after it, it is
  the fast road to every circle and the way up to Paradise.
- **Paradise is the last act.** A stair of the same ledges stands on the
  Earth's surface from the first minute; with the Wings it leads up to the
  islands, the temple and the gate that ends the journey.
- **Falling never kills.** A fall takes the hero somewhere: into a lower hall,
  down the Pit to its foot. Only the liquids that burn hurt.

## What the regions are made of

Every region has its own ground, wall, ledge, lamp, liquid, decor, rock behind
its caves, light in its air and motes in its air (`REGIONS`, `MATERIALS`,
`THEMES`), and a shape of its own, so it is told apart by more than colour.

| Region | Ground, liquid | Its shape | Its air |
|---|---|---|---|
| Paradise | cloud, marble, garden earth | islands in open sky, climbed | gold motes; the day's own light |
| Earth | grass, dirt, stone, copper; water | a surface, a mine, a gate | daylight above, torches below |
| Limbo | ashstone; the Acheron (swum) | wide, quiet galleries; graves | grey, a fog of pale motes |
| Lust | rose rock | tall shafts climbed and fallen down | a wind of petals |
| Gluttony | mud; slime (swum) | low sagging tunnels, deep pools | rain |
| Greed | darkstone, gold ore | square vaults, lanterns, heaps of coin | gold dust |
| Wrath | swamp rock; the Styx (swum) | a marsh: lakes, an underwater passage | marsh light |
| Heresy | the brick of Dis; lava | built halls, a tower, fire to glide over | embers |
| Violence | bloodstone, burning sand; blood | a river, a wood of dead trees, a desert | ash |
| Fraud | slate; tar | ditch after ditch, bridged | cold, nearly dark |
| Treachery | frost rock, ice; water under ice | great ice halls, crystals | snow |

**Transitions.** Where two regions meet nothing switches: the edge between
their rocks wanders by a few rows and each rock is flecked with the other's,
with a blend overlay on every touching tile; the light in the air of one turns
into the other's over twelve rows; the rock behind the caves changes along a
ragged strip; and the way down is a shaft the hero travels, so he sees all of
it. The region says its name when he arrives.

## The road and its gates

The hero starts with a jump and a dash. Each ability is found in a shrine (the
game's ability pickup on a plinth), in the order the road needs them:

| Ability | Found in | Opens |
|---|---|---|
| Light Dash | Limbo | a veil of light across the road (the game's LightBarrier layer) |
| Wall Grip | Lust | a smooth shaft with nothing to stand on, upwards |
| Holy Light | Greed | a seal of night (a `Breakable` a bolt breaks) |
| Glide | Heresy | a gap of eleven tiles over fire |
| Smite | Violence | a cracked slab in the floor (a `Breakable` a slam breaks) |
| Wings | Treachery | the ladders: the Pit, Paradise, the attics |

The rules the layout follows:

- **A shrine is a room left only by using what it gives**, and is entered by a
  drop too high to climb back: the hero cannot fail to learn the ability, and
  cannot be stranded (the key to the trap is in it).
- **The same gate follows at once**, and again before the way down: three uses.
- **Gates of earlier abilities keep coming back** in later circles, mixed, so
  nothing is used once and forgotten (Fraud asks for every one).
- **Jumps asked for are well inside what he can do.** Stones in a liquid that
  hurts are two tiles wide and three apart (he clears five); a ledge is three
  rows up (he jumps four and a half). Nothing stands at the edge of his reach,
  so what cannot be jumped looks it.
- **A place to rest** stands at the start of every circle, after its set piece,
  and in every antechamber.

## Side roads

Off the road, each circle has what the critical path does not need:

- **Low roads**: a hatch in the floor (a ledge, dropped through with down and
  jump) leads down to a second way under a stretch of the road and up again
  further on. Some hold a relic in a crypt or under a pool; some are shut by a
  gate of an ability found later, a reason to come back.
- **Attics**: a ladder of ledges through the roof to a nook with a relic. Most
  are six rows apart: for the Wings, on the way back up.
- **Perches** in the big halls, and relics under water.

Nineteen relics are scattered this way; the playground counts them. The map also holds 26 places to rest, 8 veils of light, 12 seals and slabs, 222 lamps and about 1100 cells of ledge.

## How the map is made

`make_world_map.py` writes the map in three layers, the way Dead Cells
assembles a level (a fixed frame, hand-made pieces, noise only in the small):

1. **The frame** is written by hand: the rows of each region, the Pit, where
   each way down is.
2. **Each circle is a short script** of pieces laid end to end from where the
   last one stopped: `walk`, `hall`, `pool`, `siphon`, `chasm`, `shaft` (ledges
   to climb), `smooth` (a shaft for Wall Grip), `gate`, `slab`, `shrine`, `rest`,
   and the side roads `low_road` and `attic`. A piece is a few numbers; the
   script reads like the walk it describes.
3. **Noise** shapes only the roofs, the edges between regions and the veins of
   ore. Two spaces that were not meant to meet stop the script (`Clash`), so a
   mistake in the layout cannot quietly open a short cut.

A map marks what of the game stands where, for the scene to build:

```
@region limbo 150            a region starts at this row
@mark checkpoint 179 159     the game's checkpoint
@mark ability 111 175 LightDash
@mark barrier 104 173 4      a light barrier: column, top row, height
@mark seal 120 322 bolt 1 4  a seal: what breaks it, width, height
@mark seal 106 469 slam 3 2
@mark relic 136 162
@mark end 190 19
```

## How it is checked

`--check` plays the world through before it is written (`tv2_reach.py`):

- The hero is a box 14 x 31 px with the game's own numbers (jump 72 px in
  0.45 s, 100 px/s across, the second jump, the glide's fall). The model lets
  him run at 85 % of his speed and tests each jump's whole arc against the
  tiles, so **what it calls reachable he reaches with room to spare**.
- It walks from the start with nothing and takes each ability as its shrine
  comes within reach. The world fails if an ability comes out of order, if two
  come at once, if any place he can have reached has no way on to the next
  shrine (a strand), if a checkpoint, a relic or the end is never reached, or if
  with every ability there is a place he cannot get home from.

What it does not prove: that a gate cannot be passed early by a trick. The
model is cautious in one direction only. Sequence breaks are left to play
testing, as they are in every game of the kind.

## Playing it

Open the scene and press Play, or run the Windows player. The hero starts on the
Earth's surface with nothing.

| Key | |
|---|---|
| A D / arrows, Space, Shift, J, L, H | move, jump, dash, sword, cast, heal |
| Down + Space | drop through a ledge |
| M | the chart: what he has seen so far, a pixel a tile |
| PgDn / PgUp | jump to the next / last named place |
| F3 | every ability, or none (for trying a circle out of order) |
| F4 | the whole chart |
| R | back to the last rest |
| 1 2 3 4, T, P | the hour of the day (it shows on the surface and in Paradise) |

## Where the rules come from

The layout follows what the makers of the genre have said about it; the notes
and their sources are in the session's research file
(`metroidvania-research.md`). The ones this world leans on:

- A fixed frame, hand-made pieces and noise only in the small (Dead Cells);
  nobody credible argues for a metroidvania map generated without a frame.
- Region first, then a graph of rooms, then the space (Kynan Pearson); show
  the lock before the key; an ability's room is left only by using it, and it is
  asked for three times in a row.
- A one-way drop is safe when the key to the way back is inside it (Hollow
  Knight's wall jump, Super Metroid's high jump); falling does not kill
  (Symphony of the Night).
- Jumps asked for are 60 to 80 % of the reach; nothing is built at 100 to 120 %
  (Yacht Club Games).
- Free vertical movement comes last, because it undoes earlier gates (Super
  Metroid's space jump): here the Wings, which turn the whole world back into
  one open place for the last act.
- A consistent main direction is a signpost; secrets lie across it.
- Every dead end pays out (Igarashi); the chart never knows the land before the
  player does (Team Cherry).
