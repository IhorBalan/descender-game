# The Tiles V2 world

One map with Paradise in the sky, the Earth under it and the nine circles of
Hell under that, walked from end to end without a loading screen. It is built on
the Tiles V2 standard (`docs/art/tiles-v2.md`) and lives beside the sample map:

| | |
|---|---|
| Map | `Assets/Art/TilesV2/world_map.txt`, 540 x 780 tiles: 13 screens wide and 35 tall at the camera's 40 x 22.5 tiles, 421 000 cells (for scale, the whole world of Animal Well is 640 x 360 tiles of the same screen, 230 000) |
| Written by | `python tools/tiles_v2/make_world_map.py --check` (edit that, not the map) |
| Checked by | `tools/tiles_v2/tv2_reach.py`: the world is played through in a model of the hero before it is written |
| Scene | `Assets/Scenes/Sandbox/TilesV2Descent.unity` (never in the build's scene list) |
| Windows player | **Descender > Tiles V2 > Build Windows world**, `Build/TilesV2World64/DescenderWorld.exe` |
| Pictures | `docs/screenshots/tiles-v2/world/` (`map_labelled.png` is all of it) |

The game itself is untouched: no region scene, tile, prefab or flow of the game
knows of this world. It borrows the game's hero, camera, input, checkpoint,
ability pickup, breakable, platforms and wind, as the playground does. It has no
enemies and no bosses yet.

## The shape of the world

```
 rows                                                              the road
   0  PARADISE      islands of cloud; the temple and the gate at the end of everything
  76  EARTH         the surface (the start), the old mine, the hall of the Gate
 172  1 LIMBO       <- west, then east ->   two passes: the great first circle   Light Dash
 276  2 LUST        <- west    the tempest's shafts and spires                   Wall Grip
 332  3 GLUTTONY    east ->    mud, rain, slime: a short one
 382  4 GREED       <- west    vaults and gold                                   Holy Light
 440  5 WRATH       east ->    the marsh of Styx
 492  6 HERESY      <- west, then east ->   two passes: the city of Dis          Glide
 600  7 VIOLENCE    <- west    blood, wood, sand                                 Smite
 656  8 FRAUD       east ->    ditches and bridges: a short one
 706  9 TREACHERY   <- west    the frozen lake                                   Wings

   the Pit (west edge) ---- every circle ---- the Well (east edge)
```

- **The road winds down.** A pass of the road crosses the world from one side
  to the other, and the way down is at its far end. Limbo and Heresy, the great
  circles, are two passes each, one under the other, so the regions are not all
  of a size: the first is the largest, as it should be, and Gluttony and Fraud
  are one compact pass.
- **Three ways run down through all of it**, each opened by a different
  ability, so the world is a net and not one line:
  - **The Pit**, a shaft down the west edge. Every circle has an antechamber
    there: a place to rest and a ladder of ledges up into a tunnel to the Pit,
    six rows apart, more than a jump and less than two, so only the Wings climb
    them; and the Wings wait at the very bottom, in a room whose only other way
    out is the way in. Until then the Pit is a sight (its ladder hangs in every
    antechamber: the lock before the key) and a way to travel between the
    antechambers already rested in; after, it is the road up to Paradise.
  - **The Well**, a shaft down the east edge with ledges anyone climbs. Every
    circle's east end has a door into it, sealed until a bolt of Holy Light
    breaks it: from the fourth circle on it joins every circle the hero has
    been in, and lets him into the ones he has not, where their own gates still
    stand.
  - **Sinks**: 28 places where one pass runs level over the next, the floor is
    a slab only a Smite breaks, and a clean shaft drops through the roof of the
    road below. The hidden ways down through the middle of the world, for the
    hero who comes back with the Smite.
- **Paradise is the last act.** A stair of ledges stands on the Earth's surface
  from the first minute; with the Wings it leads up through ten islands to the
  temple and the gate that ends the journey.
- **Falling never kills.** A fall takes the hero somewhere. Only the liquids
  that burn hurt.

## What the regions are made of

Every region has its own ground, wall, ledge, lamp, liquid, decor, rock behind
its caves, shapes standing far off in them, light in its air and motes in its
air (`REGIONS`, `MATERIALS`, `THEMES`, `KITS`, `FAR`), and a shape and a way of
moving of its own, so it is told apart by more than colour.

| Region | Ground, liquid | Its road | Far off, and its great cave | Underfoot and in the air |
|---|---|---|---|---|
| Paradise | cloud, marble, garden earth | islands in open sky, climbed and glided between | the open sky | gold motes; the day's own light |
| Earth | grass, dirt, stone, copper; water | a surface, a long mine, a gate | stalagmites | daylight above, torches below |
| Limbo | ashstone; the Acheron (swum) | wide, quiet galleries; graves | broken columns; a colonnade in the fields | a fog of pale motes |
| Lust | rose rock | shafts climbed and fallen down | spires | a wind in gusts against the way in; lifts that slow a fall and carry a glide up; petals |
| Gluttony | mud; slime (swum) | low sagging tunnels, deep pools | fangs of rock: a maw | rain |
| Greed | darkstone, gold ore | square vaults, lanterns, heaps of coin | heaps and pillars; the round door of a vault | gold dust |
| Wrath | swamp rock; the Styx (swum) | lakes, a passage dived through | bare trees | marsh light |
| Heresy | the brick of Dis; lava | built halls, a tower, fire to glide over | towers with lit windows; the city | platforms over the lava that give way; embers |
| Violence | bloodstone, burning sand; blood | a river, a wood, a desert | bare trees; a great dead wood | ash |
| Fraud | slate; tar | ditch after ditch, bridged | a viaduct | platforms over the tar that come and go |
| Treachery | frost rock, ice; water under ice | great ice halls, crystals | shards; a giant fast in the ice | thin ice that gives way; a wind off the lake in gusts; snow |

**Transitions.** Where two regions meet nothing switches: the edge between
their rocks wanders by a few rows and each rock is flecked with the other's,
with a blend overlay on every touching tile; the light in the air of one turns
into the other's over twelve rows; the rock behind the caves changes along a
ragged strip; and the way down is a shaft the hero travels, so he sees all of
it. The region says its name when he arrives.

**Great caves.** Every circle has one: 64 to 82 columns wide and up to 30 rows
high, two screens of open space with no wall behind it, so the rock and what
stands far off show all through it. The road crosses its floor; two stairs of
ledges go up toward its roof, a relic at the top of one; and a building stands
on the wall behind (`colonnade`, `tower`, `spire`, `dead_tree`, `arches`,
`vault_door`, `teeth`, `giant`): the landmark the circle is remembered by.

## The road and its gates

The hero starts with a jump and a dash. Each ability is found in a shrine (the
game's ability pickup on a plinth), in the order the road needs them:

| Ability | Found in | Opens |
|---|---|---|
| Light Dash | Limbo | a veil of light across the road (the game's LightBarrier layer) |
| Wall Grip | Lust | a smooth shaft with nothing to stand on, upwards |
| Holy Light | Greed | a seal of night (a `Breakable` a bolt breaks); the Well's doors |
| Glide | Heresy | a gap of eleven tiles over fire |
| Smite | Violence | a cracked slab in the floor (a `Breakable` a slam breaks); the sinks |
| Wings | Treachery | the ladders: the Pit, Paradise, the attics |

The rules the layout follows:

- **A shrine is a room left only by using what it gives**, and is entered by a
  drop too high to climb back: the hero cannot fail to learn the ability, and
  cannot be stranded (the key to the trap is in it).
- **The same gate follows at once**, and again before the way down.
- **Gates of earlier abilities keep coming back** in later circles, mixed, so
  nothing is used once and forgotten (Fraud asks for every one).
- **The Well does not let a circle be skipped for its ability.** Violence opens
  with a river of blood nobody crosses without the Glide, and Treachery with a
  slab nobody breaks without the Smite. A circle that gives no ability (Wrath)
  may be passed by, as the genre allows.
- **Jumps asked for are well inside what he can do.** Stones in a liquid that
  hurts are two tiles wide and three apart (he clears five); a ledge is three
  rows up (he jumps four and a half). Nothing stands at the edge of his reach,
  so what cannot be jumped looks it.
- **A place to rest** stands at the start of every pass, after its set pieces,
  and in every antechamber.

## Side roads and secrets

Off the road, each circle has what the critical path does not need:

- **Low roads**: a hatch in the floor (a ledge, dropped through with down and
  jump) leads down to a second way under a stretch of the road and up again
  further on. One holds a relic in a room, one is flooded, one is shut by a gate
  to come back for.
- **Attics**: a ladder of ledges through the roof to a nook with a relic, six
  rows apart for the Wings or three for anyone.
- **Secrets**: 26 walls that are not walls. One looks like the rock round it
  but for a few fine cracks and the edge the rock draws against it; two cuts of
  the sword bring it down, and there is a room behind with a relic in it. The
  chart shows that room as rock until the wall is broken, even if the hero has
  stood beside it.
- **Perches** in the halls, the stairs of the great caves, relics under water.

The map holds 93 relics, 52 places to rest, 41 veils of light, 72 seals and
slabs, 13 platforms that do not stay, 4 winds, 767 lamps and about 3400 cells of
ledge. About a third of it is open space.

## How the map is made

`make_world_map.py` writes the map in layers, the way Dead Cells assembles a
level (a fixed frame, hand-made pieces, noise only in the small):

1. **The frame** is written by hand: the rows of each region, the floor each
   pass of the road starts and ends on (`FLOORS`), the Pit, the Well.
2. **Each circle is a short script** of set pieces laid end to end from where
   the last one stopped: `hall`, `cavern`, `pool`, `siphon`, `chasm`, `shaft`
   (ledges to climb), `smooth` (a shaft for Wall Grip), `gate`, `slab`,
   `shrine`, `rest`, and the buildings behind its great cave. A set piece is a
   few numbers; the script reads like the walk it describes.
3. **Between the set pieces the road rambles** (`ramble`): a stretch laid out
   of the pieces the region is made of (`KITS`), by a fixed dice, using only the
   gates of the abilities the hero has by then, and steered so the floor stays
   a few rows round the line to the next set piece.
4. **The sinks, the side roads and the secrets are searched for**, after every
   road is laid (`sinks`, `side_roads`): each is checked whole before anything
   is dug and tried somewhere else if it would cut into anything, and none
   passes under a gate, a shrine or a shaft, so none is a way round one.
5. **Noise** shapes only the roofs, the edges between regions and the veins of
   ore. Two spaces that were not meant to meet stop the script (`Clash`), so a
   mistake in the layout cannot quietly open a short cut.

A map marks what of the game stands where, for the scene to build
(`TilesV2Things`):

```
@region limbo 172                          a region starts at this row
@mark checkpoint 397 117                   the game's checkpoint (`pit` after it: in an antechamber of the Pit)
@mark ability 230 198 LightDash
@mark barrier 222 196 4                    a light barrier: column, top row, height
@mark seal 524 146 bolt 1 4                a seal: what breaks it (`bolt`, or `slam` for a slab), width, height
@mark secret 464 164 1 3 458 163 463 166 S a wall the sword breaks: its size, the room behind it, the rock it is drawn as
@mark platform 491 507 2 crumble 0.0       a platform that gives way (or `blink`: comes and goes): its length, how far along its cycle
@mark wind 478 280 30 12 50 0 2.5 2.5      a wind: the air it blows in, its speed across and up, how long it blows and rests
@mark relic 267 134
@mark end 405 18
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
model is cautious in one direction only; it knows nothing of the winds; and a
platform that does not stay is a floor to it. Sequence breaks are left to play
testing, as they are in every game of the kind.

## Playing it

Open the scene and press Play, or run the Windows player. The hero starts on the
Earth's surface with nothing.

| Key | |
|---|---|
| A D / arrows, Space, Shift, J, L, H | move, jump, dash, sword, cast, heal |
| Down + Space | drop through a ledge |
| M | the chart: what he has seen so far, a pixel a tile; on it the places to rest (bright once rested at), the shrines not yet emptied and the gates still shut, in the colour of what opens them; beside it the relics found, region by region |
| E at a rest by the Pit | the list of the other antechambers he has rested in: up, down, Enter to go |
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
- Regions are not of a size: the first is the largest and the connecting ones a
  quarter to a half of a normal one (Hollow Knight's rooms by region).
- Show the lock before the key; an ability's room is left only by using it, and
  it is asked for again at once (Kynan Pearson).
- A one-way drop is safe when the key to the way back is inside it (Hollow
  Knight's wall jump, Super Metroid's high jump); falling does not kill
  (Symphony of the Night).
- Jumps asked for are 60 to 80 % of the reach; nothing is built at 100 to 120 %
  (Yacht Club Games).
- Free vertical movement comes last, because it undoes earlier gates (Super
  Metroid's space jump): here the Wings, which turn the whole world back into
  one open place for the last act.
- Important places are reached by more than one way, and the way a new ability
  opens is itself a short cut: the Well, the sinks, the Pit.
- A landmark to steer by in every region; secrets lie across the main way and
  are not on the map until found (Scott Rogers; Team Cherry).
- Every dead end pays out (Igarashi).

## What it is not yet

- **No enemies and no bosses**: it is a world to walk, not yet one to fight in.
- **The ice is not slippery**: the game's hero has no ground that slides, and
  this world does not change the hero. Treachery has thin ice and wind instead.
- **Nobody has played it through by hand.** The model has, and tests walk
  parts of it.
