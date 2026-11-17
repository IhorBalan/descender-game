# Tiles V2: the Paradise level

Paradise as a level of its own, in the tiles and pictures of Tiles V2: seven
zones from the Waking Hollow to the Low Gate, laid out to the wireframe
("Paradise (Heaven) wireframe, draft 2"). The zones, their feel and their story
are `docs/design/paradise.md`'s. It is separate from the game, as all of Tiles
V2 is, and separate from the Tiles V2 world (`tiles-v2-world.md`), whose own
Paradise over the Earth is untouched.

| | |
|---|---|
| Map | `Assets/Art/TilesV2/paradise_map.txt`, 2420 x 372 tiles |
| Made by | `python tools/tiles_v2/make_paradise_map.py --check` |
| Scene | `Assets/Scenes/Sandbox/TilesV2Paradise.unity` (not in the build's scene list) |
| Windows player | `TilesV2Build.Paradise` -> `Build/TilesV2Paradise64/DescenderParadise.exe` |
| Photo of all of it | `python tools/tiles_v2/photo_world.py --paradise` -> `Logs/TilesV2/photo_paradise/` |

## From the wireframe to a map

The wireframe was a picture; nothing in the repository held it as data. It was
read off the picture (its cells are 15 px squares) into
`tools/tiles_v2/tv2_paradise_plan.py`, and checked against the picture cell by
cell:

- **69 rooms** on a grid of cells, each of a zone and a kind: on the road
  (`main`), off it (`opt`), behind a wall that is not one (`hidden`), or opened
  by an ability from later in the game (`late`).
- **75 links**: what joins two rooms, where, and how: an open door, a way down
  only, a hidden wall, the boss's door, or a gate by ability. The gates'
  colours on the picture are read as: yellow the Wings, cyan the Light Dash
  (both found in Paradise), brown Wall Grip, pale yellow Holy Light, pale green
  Glide, orange Smite, grey true sight (all from later).
- **What stands in a cell**: where the hero comes in, the 16 shrines, the Wings
  at the top of the Crown Vine, the Light Dash on the Terraces, a heart, an
  encounter space, the boss, the way out.

`make_paradise_map.py` builds the map from the plan:

- **A cell is 20 x 12 tiles.** The plan's 119 columns and 26 rows of cells,
  with a margin of sky, are 2420 x 372 tiles.
- **A room** is its cells with a shell inside their edge: a floor two tiles
  thick, a ceiling and walls one. Between two rooms that makes a wall of two
  and a floor of three, in each zone's own stone. Toward the open sky the
  shell is cloud, and banks of cloud lie round the rooms (`clouds`), with
  billows on every underside: the level is rooms in cloud.
- **A storey** is a row of cells. A door's sill is on its storey's floor. In a
  room of several storeys an upper storey's floor is a platform over part of
  the room, from one side and the next from the other, with ladders of ledges
  up through the gaps: four rows apart where the hero has only his jump (zones
  1 to 3), six where he has the Wings by then (zones 4 to 7).
- **A link** is cut where the plan says. A gate is built as what only its
  ability passes: a sill five tiles up for the Wings (a hole with one ledge six
  under it, through a floor); a veil of light for the Light Dash; a seal for
  Holy Light; a slab for Smite; a smooth chimney for Wall Grip; a wind up a
  hole, or a gulf eleven tiles wide over a walled pit, for Glide. A hidden wall
  is the standard's `secret`. The room true sight would open is built and
  stays shut: the standard has no such ability.

Two things differ from the picture, on purpose. The door from the Crown Vine
into the Tallied Fields is an open door there; here it is a sill for the
Wings, because the hero climbing the vine passes it on his way to them, and
the fields let no one back who has not got them. And the two "shortcuts that
open from the far side" are built as sills for the Wings: the hero has none
when he first passes them and has them when he comes back.

## What the model checks

`--check` plays the level in the model the world is checked with
(`tv2_reach.py`):

- with nothing, the hero reaches the Wings, and not the Light Dash;
- with the Wings he reaches the Light Dash;
- with both he reaches every shrine, every relic and upgrade outside the late
  rooms, and the way out;
- at no point can he get somewhere he cannot go on from;
- he cannot get out of the rooms;
- every late room is shut to him, and open to a hero with everything (but the
  one that waits for true sight).

## The zones

Each zone has its ground, the wall behind its roofed rooms, its lamp and what
it is dressed with (`tv2_paradise_dress.py`, pictures from `tv2_eden.py`,
`tv2_heaven.py` and the Earth's `tv2_earth.py`). Every room is marked with its
zone's name (`@mark zone`), and the name is shown when the hero comes into it.

| Zone | Ground | It is |
|---|---|---|
| 1 The Waking Hollow | grass on earth | great leaves taller than a house, trees in blossom, flowers; the Voice at the start |
| 2 The Orchard Under the Canopy | loam under moss that glows (`moss`) | roofed by a canopy (`canopy_wall`), so lit only by its own fruit (the `glowfruit` lamp) and its mushrooms; teal trees in exact rows; the Lantern Tree, a trunk six tiles through with a bough over every storey; Mereth |
| 3 The Great Vines | living wood under moss (`vine`) | trunks of three stems twisted together, taller than two screens; the Crown Vine, whose trunk fills the middle of a room sixteen cells high, the Wings at its top |
| 4 The Tallied Fields | tilled earth under wheat (`gold_meadow`) | wheat in counted rows with a tally post at each row's end, haystacks and fences, trees of gold leaf, a cottage, the Sundial; Ithuriel; the gathered walking far off |
| 5 The Terraces | marble | pillars under every platform, urns, clipped green, a fountain, water falling from the ends of the terraces, angels in marble, the Wheel mural |
| 6 The Making-house Approach | pearl (`pearl`) | pipes of brass down every wall and under every floor, gears, pale ivy; the door of the Making-house |
| 7 The Low Gate | silver rock (`silver`) | crags, braziers, frost on the ivy; the Low Gate, a ring of silver with the dark under Paradise in it; the Warden's arena, empty |

The view draws back (`@mark vista`) at the Lantern Tree, the Sundial, the
Wheel, the Making-house's door and the Low Gate.

## Going from room to room

A door is a change that comes on, not a line that is crossed (`gateways`):

- **A doorway is as high as its storey lets it be** (eight tiles), and stands
  in an arch behind whoever goes through: two trunks grown together overhead
  in the garden's first three zones, two piers and a pointed arch of old cut
  stone after.
- **Where two zones meet at a door, each runs a way into the other**: the
  ground of each in runs of two tiles, fewer the further in, for a dozen
  tiles; and one or two of each zone's own growth stand over the line.
- **Where a roofed room meets an open one**, the roofed room's wall reaches
  out into the open one and is let into by the sky, row by row as far as a
  slow wave says, so the edge between them is one ragged line like the edge
  of a wood, and the light falls off along it instead of at the door. (Tile by
  tile it was a checker.)

## An old, great place

The level is meant to be read as one place, old and grand and emptied, as well
as seven gardens:

- **Its hour does not pass.** The map says its own hour (`@hour 17.4 still`):
  the sun low and gold, and it does not move: the design's "endless sun". The
  keys that change the hour in the sandbox still do.
- **A fire by every shrine**, lit round a twisted sword: where one rests.
- **Fog stands in the Warden's door**, from sill to lintel, drifting. It stops
  nobody: the arena is still an empty space.
- **Old stone among the gardens**, more of it the further on: statues of
  hooded keepers leaning on great swords, flying buttresses, columns broken off
  aslant with a drum fallen by them, swords left standing in the ground, an
  iron fence, dead trees by the Gate; windows of an old hall, with a light
  behind them, down the walls of the roofed rooms from the Terraces on; chains
  among the ivy where the garden ends.
- **Fire for light** from the third zone on: torches, braziers. The Hollow
  keeps its lanterns, the orchard its fruit, the Making-house its cold
  crystals.

The sky is the world's heaven: its far layers go up with the camera by more
under a sky higher than the world's (`TilesV2Backdrops.TallestSetSky`), so the
country of cloud and the far castles are behind every room, high or low.

## What is not there

- **Nobody has played it by hand.** The model says it can be played through;
  how it feels is not known.
- **No enemies and no boss.** The encounter space and the Warden's arena are
  built as spaces, as the design's first phase asks.
- **The design's own mechanics are not built**: bounce pods, briars that hurt,
  moving machinery, light barriers that open from one side. The level uses
  what the standard has: ledges, the gates above, winds.
- **The size is the wireframe's, not the design document's.** The document
  asks for a 20 minute run across; at 20 x 12 tiles a cell the road here is
  shorter. A bigger cell would lengthen it.
- **The rooms are laid out by rule**, not by hand: platforms from alternate
  sides, ladders in the gaps. Each zone looks its own, but two rooms of a zone
  are built the same way.
- **Trees and trunks taller than their room** show over the cloud above it.
- **The "old place" is a look, not a way of playing.** There are no fights, no
  souls to lose, no doors that open from one side only but the ones the plan
  names; the fog is a picture.
