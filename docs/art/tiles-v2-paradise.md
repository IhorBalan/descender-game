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
| Pictures | `docs/screenshots/tiles-v2/paradise/`: the whole, then each view at its zone's own hour |

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
  the room, from one side and the next from the other, with a way up through
  each gap: the zone's own (below), or a ladder of ledges where that does not
  fit: four rows apart where the hero has only his jump (zones 1 to 3), six
  where he has the Wings by then (zones 4 to 7).
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

## Each zone built its own way

The rooms of a zone are climbed and shaped as the zone is (`STYLE`, `CLIMB`,
`ways_up`, `shaped` in `make_paradise_map.py`):

| Zone | The way up a storey | Its ledges | Its floors |
|---|---|---|---|
| 1 The Waking Hollow | ladders of ledges; **the Rising Leaf is a stair**, run up in one go | planks | low hills, also in the air |
| 2 The Orchard | ladders | planks | level: it is roofed and planted in rows |
| 3 The Great Vines | ladders | planks | **boughs that swell**: the floors in the air rise and dip |
| 4 The Tallied Fields | **stepping islets** of ground, a jump with the Wings apart | planks | hills, and **streams** seven tiles wide to wade or clear |
| 5 The Terraces | **stairs** of gilt treads, two tiles to a step where there is room | gilt | terraces: level, with a low rise here and there on the ground |
| 6 The Making-house | ladders | grating | level: it is built |
| 7 The Low Gate | **stepping islets** of rock down the cliffs (the Eyrie keeps ladders: an islet by its gulf would be a way over a wall only a glide should cross) | rime | broken ground, also in the air |

**The shape of the ground.** A floor is not a slab. The lowest floor of an
open room rises by a tile in long low hills, cut on the slant at either end;
in the zones that are grown and not built the floors in the air swell the
same way. A hill never stands by a door, over a way down, where the plan
stands something, or at a floor's end where a way up comes. The model does
not stand the hero on a slope, so it sees a hill as a step of one tile, which
he jumps; in play he walks up it. And under every floor in the air hangs
what it is made of (`undersides`): the earth it was lifted with and the roots
out of it in the gardens, a mass of its own stone, broken off, after. Those
are pictures behind the hero, in shadow: nothing to bump a head on.

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

- **The sun does not move, and the road is a day.** In no zone does the hour
  pass (the design's "endless sun"), but each zone has its own (`MOODS`, a
  `@mark mood` on every room): the Hollow and the orchard at dawn (6:48), the
  Vines in the morning (10:30), the Fields in the gold of late afternoon
  (16:48), the Terraces toward sunset (17:54), the Making-house at sunset
  (18:36), the Gate at dusk (19:24). The day turns to a zone's hour as the
  hero comes into it, over two and a half seconds (`TilesV2Playground.Turn`),
  so the light, the sky, the shafts of sun and the lit windows are each
  zone's own. The keys that change the hour in the sandbox still do: an hour
  set by hand stays until he goes on to another zone. The map's own hour
  (`@hour 6.8 still`) is the one he wakes at.
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

## Depth, air and life

Behind a zone's floors there is more of the zone, and before them something
close to the eye (`depth` in `tv2_paradise_dress.py`; the standard's `life`
marks):

- **Far off** (`life far`, pictures `eden_far_*`): the great leaves and the
  blossom going away in the Hollow; the orchard's rows, tree behind tree; more
  trunks, as great, pale in the mist among the Vines; fields to the horizon
  with a line of the gathered on a ridge; arcade over arcade behind the
  Terraces; spire behind spire by the Gate. They are small and gone toward
  the colour of the air, lit as the room is, and are passed a little slower
  than the world (`TilesV2Life.FarScroll`).
- **What is ahead is seen before it is reached.** The Wheel stands over the
  way up the Terraces from the stair's foot on. The Making-house is on the
  horizon from the end of the Terraces, nearer in each of three sizes along
  its approach, and behind its own door at the last. The Low Gate is seen in
  its cliff from the Terraces, and nearer from the head of the cliffs.
- **Close to the eye** (`life front`, `eden_front_*`): a dark shape of the
  zone's own kind here and there on a long floor, passed in front of faster
  than the world: blades of a great leaf, a low branch with its fruit, ears
  of wheat, a broken balustrade, a pipe and its valve, split rock. The leaf,
  the branch and the wheat sway.
- **In the air**: shafts of low sun where the hour has them (`life rays`: the
  Hollow, the orchard, the Terraces, the Making-house); mist along the lowest
  floors of the orchard, the Vines and the Gate (`eden_mist`, drifting);
  pollen in the warm zones and butterflies by day, fireflies under the
  canopy, falling leaves on the Terraces, steam in the Making-house, snow
  coming down slowly at the Gate, drips from the roofs of roofed rooms, a
  splash where the Terraces' water lands, birds far off over the open rooms.
- **What moves**: the gathered walk (their line is eight pictures a step
  apart, so it never ends and never arrives); pistons work beside the gears
  in the Making-house; the fog and the mist drift; the trees and what is
  close to the eye sway.

**What is bright glows.** The level's scene has a glow of its own
(`TilesV2Sample.ParadiseGlow`, the profile `TilesV2ParadiseGlow.asset`): its
camera renders post-processing, and a global Volume blooms what is over a
high threshold, softly: lamps, fruit, fire, crystals, white stone in the sun.
Captures of the scene are taken with it. The standard's other scenes have
none.

## Fewer repeats, and wear

- **Groups, not rows** (`along`). What stands on a floor stands in ones, twos
  and threes with room between, each a few pixels off its cell, no two groups
  alike. What is planted in rows stays in rows, since the orchard and the
  wheat are counted, but a stretch here and there has most of its trees
  gone, and a row of wheat ends in its tally post. On every long floor one
  stretch is left bare: somewhere to stop (on the Terraces, a bench).
- **More kinds**: wheat in four, a keeper's statue with its head gone, pistons.
- **Wear** (`wear`): cracks down from the lip of the cut stone, and what has
  grown over it: moss on the Terraces, pale lichen in the Making-house, rime
  by the Gate.

## The great things

- **The Rising Leaf** is a stair of treads up its room, with a leaf as tall
  as a tower (`eden_great_leaf`) behind it.
- **The Lantern Tree** has its crown: a screen of teal leaves and fruit across
  the top of its room (`eden_lantern_crown`), over a trunk that is a picture
  from the floor up, on its roots.
- **The Crown Vine** puts a bough out over every storey, to the side the
  floor is on: what the hero climbs is its limbs, round the plait of its stems.
- **The Wheel** is seen from below all the way up the Terraces.
- **The Low Gate** is a ring thirty-two tiles across, a third again what it
  was, sunk three tiles into the ground it is set in, so it fills its arena
  from floor to cloud.

The sky is the world's heaven: its far layers go up with the camera by more
under a sky higher than the world's (`TilesV2Backdrops.TallestSetSky`), so the
country of cloud and the far castles are behind every room, high or low.

## The second pass: nothing flat, nothing bare, nothing said twice

What was looked at, texture by texture, and redone (`tv2_eden.py`,
`tv2_backdrop.py`, `tv2_terrain.py`, `tv2_paradise_dress.py`):

- **No chequer anywhere in the sky.** The sky's bands met in an ordered
  dither and its cloud and its castles thinned out in one: at the size the
  game is seen at that is a screen door. The sky is twenty-four even bands
  that meet in a line (`Sky.Band`), and what thins out (a cloud's foot, a
  shaft of light, the glory, the rainbow) does it in steps of see-through
  (`tv2_backdrop.stepped`).
- **What is close to the eye has a form.** A front was a black shape. It is
  three tones of its zone's own colour, with a rim where the light catches it
  from the upper left and its ribs, veins and facets in the middle tone; and
  what of it goes under its floor line thins out to nothing (`front`).
- **Nothing is a bare bar.** A garden's platforms were held up by a column of
  wall tiles. They stand on a picture of their zone's own, ten tiles tall
  (`post`, `eden_post_*`): the jointed stalk of a great leaf, a slender trunk
  that forks under its load, two stems twisted about each other, a post of
  timber with its braces and a binding of rope. The Lantern Tree's trunk was
  six columns of wall; it is a picture a storey tall, stood one over another
  from the floor to the crown (`eden_lantern_trunk`: bark in ridges that
  wander up it, a knot, a hollow with a fruit alight in it), on its roots
  (`eden_lantern_root`). The Crown Vine's was a slab of wall; it is a plait of
  six stems (`eden_crown_trunk`).
- **Nothing is a plank on the air.** Under every ledge hangs what it sits on,
  in the make of the ledge (`corbel`, `eden_corbel_*`): braces of timber to a
  post, or a clod of earth and its roots, in the gardens; a bracket of marble
  on the Terraces; brackets of iron, a chain and its hook, a pipe's elbow in
  the Making-house; a shard of rock and its ice by the Gate. A stair's treads
  have one each, so a stair has an underside.
- **Bigger than a tile.** Set in the ground of every floor, every so often,
  is something of the zone's own that is wider than a tile (`inlay`,
  `eden_inlay_*`): a boulder, roots, a seam of pebbles in the earth; the rings
  of its growth in the wood of the Vines; a frieze, a rosette, a tally cut in
  the marble; a plate, a pipe, a dial let into the pearl; a vein of ice in
  the rock. And the tiles themselves: the earth has a stone or two and a
  lighter seam in it, the turf is deeper, and the wall of leaves behind a
  roofed room is soft leaf shapes in two tones a step apart
  (`texture_foliage`), not a field of specks.
- **The dark under a roof.** In a roofed room a veil of dark hangs under the
  roof and under every floor in the air, deepest against what it is under and
  gone in four tiles (`eden_gloom_*`).
- **Ground on cloud is not a line.** Where a room's ground lies on the cloud
  it is set in, billows of the cloud lap up over its foot (`seams`).
- **Redrawn**, because they were flat shapes among drawn things: the great
  leaves (lit on the half that faces the dawn, veined, bitten, with dew), the
  Sundial (rings and hour lines cut in a worn face, a blade for a gnomon),
  the Wheel (a rim that is a roll of metal, studded; a hub that is an eye),
  the Low Gate (thirty-six stones round, a sign cut in each, a gem at each
  quarter, rime on its crown and ice hanging in it, and deep on deep of dark
  inside), the Making-house from afar (an arcade, pilasters, a roof-walk, a
  lantern on its dome), the far fields (plot beside plot, hedges, poplars;
  no edge), the crags (split rock, facet by facet, snow on every shoulder),
  the keepers' statues (a hood, a mantle in folds, both arms brought to the
  sword: they were bells), wheat (a close stand with heavy ears), ivy (a
  leaf to either side all the way down).

## Light, look and what answers the hero

- **A lamp's floor is the lamp's colour.** Light over full daylight is eased
  toward a ceiling a quarter over it, all three of its parts by the same
  share (`TileLight.Soften`, `Knee`, `Ease`): the floor under a brazier is
  orange, not a yellow burnt flat where its red and green both ran past
  white. Lamps stand closer in the zones whose hour is late.
- **Each zone has its own look** (`LOOK`, a `@mark grade` on every room): a
  turn of the dial, not a filter. The Hollow is seen through the rose of the
  dawn; the orchard deeper and cooler, its lights the brighter for it; the
  Vines greener; the Fields gold; the Terraces peach; the Making-house with a
  little of its colour gone; the Gate cold and hard. A look is a global
  Volume of its own (`TilesV2Things.Looks`, URP's Color Adjustments), as
  strong as the camera is near a room that has it (`TilesV2Things.Look`), so
  going through a doorway turns one into the next over `GradeBlend` tiles.
  The corners of the view are drawn in a little (a vignette in the glow's
  profile). Captures are taken with the look of the room they show; a
  photograph of the whole map with none.
- **What grows bends as he goes through it** (`life brush`, `eden_brush_*`):
  grass with a flower in it, a fern of the orchard with its tips alight, the
  fronds under the Vines, wheat that has seeded itself off its row, flowers
  on tall stalks along a terrace, grass with the frost on it. It is drawn
  before the hero, knee high to waist high; it leans away from him, the more
  the faster he goes, and springs back with a swing or two when he has gone
  by (`TilesV2Life.Bend`).

## Rooms composed round what stands in them

Seven rooms are not laid by the rule (`COMPOSED` in `make_paradise_map.py`):
each is built round the great thing of its zone, so that is seen whole, with
its room for a frame. The links the plan asks for are still kept, the ways up
are still found by `ways_up`, and the model plays them like any other room.

| Room | How it is composed |
|---|---|
| The Waking Hollow | a mound in the middle to climb onto, a bough to either side over it |
| The Lantern Tree | its floors are its boughs: to one side of the trunk and then the other, each a little past it, so the climb goes round it |
| The Crown Vine | the same, round the plait of its stems |
| The Sundial | tiers to either side, stepping back as they go up; over the Sundial nothing but, high up, the floor the way on goes by |
| The Wheel | tiers to either side, and ladders, not stairs, up them; the one floor the plan needs in the middle lies under the hub, so the eye of the Wheel looks out over it |
| The Making-house's door | a gallery to either side of the door's bay |
| The Low Gate | tiers to either side of the ring: nothing between the eye and the Gate |

## At 32 pixels to the tile

The level ships at 32 pixels to the tile, as the whole standard does
(`docs/art/tiles-v2.md`, "Draft and density"): a view is 1280 x 720 and still
40 x 22.5 tiles, so every room and every view is composed as it was. Drawn at
the finer size are the insides of the level's ground and walls, the turf on
them, its trees and great leaves, and three of its great things: the Low Gate
(a true round, the grain of its stone, finer ice, more and smaller stars,
wisps in its dark), the Wheel (its rim, its spokes and its eye) and the
Sundial; and what is in every view: the posts under the platforms, what is
under every ledge, the earth and roots or the stone under every floor in the
air, and the ivy; and the Lantern Tree's trunk and roots, the Crown Vine's
plait, the twisting vines of zone 3, the boughs and the wheat; the brush
that bends as the hero goes through it, what is set in the ground of the
floors, the crags by the Gate, the keepers' statues, and the lamps (the
torches, braziers, crystals, lanterns and glowfruit); the Making-house's
pipes, gears, pistons and door and the gathered going in, the arches, the
window, the buttresses, the ruins, the swords, the people, the chains, the
fog and the mist, the shafts of sun, the cracks and the moss; what stands
far off behind every zone, the Lantern Tree's crown, and the dark shapes
that pass before the eye; and of what it borrows, the billows under its
clouds, the garden's flowers, clipped green, benches, urns and columns, and
the Earth's thickets, dead trees, fences and haystacks, the host in marble
and the fountain; the gathered walking round the Wheel; and the sky over it
(`docs/art/tiles-v2.md`). Everything
else is its draft through Scale2x, which is the picture it was with smoother
edges, until it is drawn again.

## What is not there

- **Nobody has played it by hand.** The model says it can be played through;
  how it feels is not known.
- **Some of the level is not yet drawn at 32.** The Wheel's wall is its
  draft doubled, with a grain laid over it; so are a well and a cottage it
  borrows once each from the Earth.
- **The hero and the game's own things are 16 to the tile**: his pixels are
  twice the world's. They are the game's art, not the standard's.
- **On a 1080p screen a pixel of the art is one and a half of the screen's**:
  even and steady, but not the hard squares it is on 1440p and 4K.
- **No enemies and no boss.** The encounter space and the Warden's arena are
  built as spaces, as the design's first phase asks.
- **The design's own mechanics are not built**: bounce pods, briars that hurt,
  moving machinery, light barriers that open from one side. The level uses
  what the standard has: ledges, the gates above, winds.
- **The size is the wireframe's, not the design document's.** The document
  asks for a 20 minute run across; at 20 x 12 tiles a cell the road here is
  shorter. A bigger cell would lengthen it.
- **Sixty-two of the sixty-nine rooms are still laid out by rule**: platforms
  from alternate sides, the zone's way up in the gaps. Seven are composed
  (above); the others of a zone are built the same way as one another.
- **The looks are light.** A zone's look is contrast, saturation and a colour
  it is seen through, within a fifth of the dial: no curves, no tone map of
  its own. The game's post-processing profiles are not used here.
- **Only the brush answers the hero.** The grass of the tiles themselves, the
  trees and the ivy do not bend as he passes; the sway of those is the
  wind's. Water does not show him back.
- **No relief from the lamps.** The tiles have no normal maps: a lamp lights
  a floor evenly, it does not pick out its stones.
- **The cloud under a floor is still cloud**: billows lap its top, but under
  them it is a plain bank.
- **The gathered walk in place.** Their line moves; they are small figures far
  off, and none of them is a person one can meet.
- **Slopes are only the ends of hills.** The model does not stand the hero on
  a slope, so there are no long ramps: a hill is a tile high.
- **A far thing can show past its room's wall** by a tile or two when the
  camera is two rooms away: it slides, and nothing cuts it at the wall.
- **Pictures do not show motion**: what sways, drifts, walks and falls is seen
  only in play.
- **Trees and trunks taller than their room** show over the cloud above it.
- **The "old place" is a look, not a way of playing.** There are no fights, no
  souls to lose, no doors that open from one side only but the ones the plan
  names; the fog is a picture.
