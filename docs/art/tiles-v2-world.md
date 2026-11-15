# The Tiles V2 world

One map with Paradise in the sky, the Earth under it and the nine circles of
Hell under that, walked from end to end without a loading screen. It is built on
the Tiles V2 standard (`docs/art/tiles-v2.md`) and lives beside the sample map:

| | |
|---|---|
| Map | `Assets/Art/TilesV2/world_map.txt`, 540 x 844 tiles: 13 screens wide and 37 tall at the camera's 40 x 22.5 tiles, 456 000 cells (for scale, the whole world of Animal Well is 640 x 360 tiles of the same screen, 230 000) |
| Written by | `python tools/tiles_v2/make_world_map.py --check` (edit that, not the map) |
| Checked by | `tools/tiles_v2/tv2_reach.py`: the world is played through in a model of the hero before it is written |
| Scene | `Assets/Scenes/Sandbox/TilesV2Descent.unity` (never in the build's scene list) |
| Windows player | **Descender > Tiles V2 > Build Windows world**, `Build/TilesV2World64/DescenderWorld.exe` |
| Pictures | `docs/screenshots/tiles-v2/world/` (`map_labelled.png` is all of it) |

The game itself is untouched: no region scene, tile, prefab or flow of the game
knows of this world. It borrows the game's hero, camera, input, checkpoint,
ability and upgrade pickups, breakable, platforms and wind, as the playground
does. It has no enemies and no bosses yet.

## The shape of the world

```
 rows                                                              the road
   0  PARADISE      the Garden over the start, the Temple over the Garden, three lights
 140  EARTH         the surface (the start), the old mine, the hall of the Gate
 236  1 LIMBO       <- west, then east ->   two passes: the great first circle   Light Dash
 340  2 LUST        <- west    the tempest's shafts and spires                   Wall Grip
 396  3 GLUTTONY    east ->    mud, rain, slime: a short one
 446  4 GREED       <- west    vaults and gold                                   Holy Light
 504  5 WRATH       east ->    the marsh of Styx
 556  6 HERESY      <- west, then east ->   two passes: the city of Dis          Glide
 664  7 VIOLENCE    <- west    blood, wood, sand                                 Smite
 720  8 FRAUD       east ->    ditches and bridges: a short one
 770  9 TREACHERY   <- west    the frozen lake                                   Wings

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
  - **Sinks**: 19 places where one pass runs level over the next, the floor is
    a slab only a Smite breaks, and a clean shaft drops through the roof of the
    road below. The hidden ways down through the middle of the world, for the
    hero who comes back with the Smite.
- **Paradise is the last act, and a place of its own** (the section below):
  three stairs of light stand on the Earth's surface from the first minute,
  and from the first minute water falls out of the sky into the pond by the
  start.
- **The Earth is a country, not a lid.** The surface rolls through woods, a
  village, fields and a graveyard to the Gate, with a pond by the start and an old
  shaft that drops into the mine (the section below). A signpost by the start says
  where the road goes.
- **The Pit and the Well are places.** A chain hangs down the middle of the
  Pit; beside every door into the Pit and the Well the circle's number is cut
  into the wall in lamps, so the hero knows which circle he looks into; a
  tablet by each of the Well's doors names it.
- **Falling never kills.** A fall takes the hero somewhere. Only the liquids
  that burn hurt.

## What the regions are made of

Every region has its own ground, wall, ledge, lamp, liquid, decor, rock behind
its caves, shapes standing far off in them, light in its air and motes in its
air (`REGIONS`, `MATERIALS`, `THEMES`, `KITS`, `FAR`), and a shape and a way of
moving of its own, so it is told apart by more than colour.

| Region | Ground, liquid | Its road | Far off, and its great cave | Underfoot and in the air |
|---|---|---|---|---|
| Paradise | cloud, marble, gold, garden earth; falling water | a Garden in the middle, a Temple over it, three ways out to three lights | heaven: islands far off, shafts of light, a sea of cloud | gold motes; the day's own light; winds that blow upward |
| Earth | grass, meadow, dirt, stone, clay, copper; water | a surface, a long mine, a gate | the old workings: trestles, ladders, a head frame | daylight above, torches below; pollen |
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

## The Earth

The Earth is the first thing the hero sees, so it is dressed as a place people lived
in and left: what stands on it is drawn free, as pictures stood in its cells
(`tv2_earth.py` draws them, `tv2_earth_map.py` says where; `docs/art/tiles-v2.md`, "The
Earth's pictures"). None of it is in the way: the ground, the ledges and the lamps are
what they were, and the play-through is the same.

```
 the Pit  firs   stair  a mixed wood, the old shaft   the village   start   pond  stair  the mill,     the Gate  graves  stair  firs
 |#|      ^^^^   |||    under its pithead             chapel, well,  ruins   ~~~   |||    fields, a      /^^\     + + +   |||    ^^^
                                                      two cottages                        cottage, an
                                                                                          orchard
```

- **Woods.** Firs at the two ends, by the Pit and past the last stair; a mixed wood of
  oaks, firs and thickets round the old shaft; fewer trees in the village; an orchard
  of apple trees by the fields. Four oaks (one in autumn colours), three firs, two
  apple trees, two dead trees and a willow, each tree a picture 6 to 12 tiles high,
  its crown a few bundles lit from the top left, standing on level ground and never on
  a stair of light.
- **The village** on the hill west of the start: a chapel with no roof, its gable and
  bell still standing, graves by it; a well; a cottage whose roof fell in at one end;
  fences. **By the start** the columns of a temple of the old world stand broken
  behind the marble stubs, a drum fallen in the grass.
- **The pond**: a willow on its west bank, reeds at both edges, lilies on the water.
- **East of the pond**: the mill on its rise, sails broken; fields with haystacks, a
  scarecrow, a cart left where it stopped; a cottage still thatched where someone still
  lives, a lamp in its window (seen at dusk and at night) and a lantern by its door.
- **The Gate** on the surface is an arch of dark stone over the mouth of the way down,
  pointed, a glow at its foot; two dead trees by it, and between it and the last stair
  the graves of those who went down before.
- **The meadows flower**: in patches (where a smooth field of noise says so), the turf
  is `meadow`, warmer than the grass, and wild flowers grow on it in clumps.
- **Behind the surface** four ridges, each further one paler, bluer and plainer:
  mountains with snow, hills with a castle, two villages and a tower far off, a wood
  of round crowns and firs, and the near wood; pollen drifts in the air, seen in the
  sun and by the lamps.
- **The old mine** is timbered: a set of timber (a post, a cap, two knee braces)
  every seven columns or so where a tunnel is 3 to 12 rows high, the roof over it
  boarded (`timber_wall`) a few columns either side; track along every level stretch of
  six columns or more between the shaft and the crossroads, its ends stopped with
  timber, a cart of copper ore on one run in three or so (one tipped over); crates,
  sacks and a ladder on wider floors; the old shaft has a pithead of timber with its
  wheel over it and a winch hut by it. The rock behind its caves is beds of earth in
  long waves, and the old workings stand far off in it: trestles one over another,
  ladders, a head frame with its wheel. **The Gate** in its hall under the ground is an
  arch between two piers of dark red stone, a hooded figure in a niche in each, a face
  for a keystone.

The named places of the Earth are `earth`, `earth_falls`, `earth_shaft`, `earth_mine`,
`earth_crossroads`, `earth_gate`, `earth_antechamber`, and the new `earth_village`,
`earth_fields`, `earth_graves`, `earth_wood`, `earth_mouth`; its pictures at other
hours `earth_sunset`, `earth_village_dusk`, `earth_fields_dawn` and `earth_night`.

## Paradise

Paradise is 140 rows of sky, six screens high (`LIFT` rows were added over
the world for it). It is the one region that is not a road: it has a middle,
and three ways out of the middle that are gone in any order.

```
                         a cloud with a draught
                        ___/ roof of gold \___
                       |      THE TEMPLE      |   its door opens when three lights burn
     the Orchard       '----------:-----------'
   (gold trees, a      a stair    :  water          the House of Trials
    ring of columns)   of light   :  falls        (a veil, a chimney, a seal;
   ~~~~~~~~~~~~~~~      and a     :                 a roof of gold, two towers)
     |  gulfs of wind   wind   ___:___  bridge    ~~~~~~~~~~~~~~~~~~~~~~
     |  <-------------------  THE GARDEN  ------------------>   |
     |                        lake, rainbow                     |
     |                        the Hollow inside the cloud       |
   stair                      |  :                            stair
   of light                 stair:  water falls               of light
 ===========================|====:===pond=======================|=======  the Earth
```

- **The Garden** is a great island right over the start, some fifty rows up: grass,
  trees in blossom, a lake with a rainbow standing in it. The stair of light
  from the Earth comes up through a well in its cloud, under a pavilion with a
  roof of gold. A soul by the well says what the place asks for. A gate with a
  round arch stands before the bridge at its east end; at its west end the
  stair to the Temple climbs between two tall columns.
- **The Temple** stands on its own island over the Garden, and it is not a
  church: it is the **citadel of the host**, a castle the angels themselves
  live in, sixty rows from its steps to the pennant on its great tower, thirty
  times the hero's height. Three steps up to a hall 73 tiles long and 22 high
  behind an arcade of white columns and round arches, a blue banner on every
  pier, a lit window, tall and pointed, in every bay, chandeliers on chains of
  gilt. At its middle, on two steps of gold, a throne of gilt under the wings
  of the host: the journey ends there. Over the hall a wall walk under
  battlements; out of its middle a **great tower** under a roof of blue tiles,
  open to the hall below, with a ladder of gilt up it to a gallery under the
  roof where a draught lies (the wall walk goes through the tower by a door in
  either side); a turret hung at either corner of its head; four more towers
  of four heights along the wall walk, the tallest flying pennants, with four
  towers further back between them and a bridge on an arch from tower to
  tower; galleries along the hall half way up it, reached by ledges at its
  end walls; and a **gatehouse** before either end, two towers and between
  them a gate: a round arch of cut stones, a grate of gold drawn up into it,
  the wings of the host over it. Its one door (`sanctum`) is shut until **three
  lights** (`beacon`) burn. A light is a bowl of gold: the hero lights it by
  walking up to it, for good.
- **The three lights** are at the ends of three ways, each asking for what the
  journey gave, so Paradise is the whole game once more, in the sun:
  - **West, the Orchard**: over three gulfs eleven tiles wide, each with a
    wind in it that blows upward in gusts (the Glide), to an island of trees
    with leaves of gold. The light stands in a round temple: columns in a
    ring, a dome of gold on them, lanterns hung from its crown, and one of
    the host in white marble at either hand, seven times the hero's height.
  - **East, the House of Trials**: over a bridge of marble on round arches
    to a second castle: two halls one over the other behind columns, lit
    windows upstairs and open arches down; over them a wall walk under
    battlements, a keep under a great roof of blue with the wings of the host
    on its wall, and six towers of six heights. A veil of light across its
    hall (the Light Dash), a chimney with nothing to stand on up to the floor
    above (the Wall Grip), a seal of night (the Holy Light), and the light
    past it. The way out is a balcony on its west side. The keep is entered
    through the roof of the upper hall, and holds a relic.
  - **Down, the Hollow**: a grotto inside the Garden's own cloud, under the
    lake. A cracked slab in the grass drops into it (the Smite); a sealed door
    opens into it from the well of the stair (the Holy Light).
- **Water falls from heaven to the Earth.** Out of the Temple's cloud, down
  fifty rows into the Garden's lake; through the Hollow; out of the Garden's
  cloud and down into the pond beside the start. It is a liquid of its own
  (`falls`): seen and walked through, neither swum in nor stood on, drawn as it
  is so it shows at night.
- **Three ways in.** Besides the stair by the pond, a stair of light stands
  under the Orchard's west end and one under the House's east end, so a hero
  coming up from the Pit or from the Gate is not sent back to the middle.
- **Up is rewarded.** The citadel's wall walk is climbed from either end and
  goes through its great tower (that is also the way round the house); a
  draught lies under the great tower's roof; a heart on a cloud over the
  Orchard's dome; a relic in the House's keep and one on a cloud half way up
  the stair to the citadel, where a soul stands.
- **How it is built** (the rules, from what builders of block worlds say, in
  the session's research notes): the outline first, and never a box: one mass
  clearly the tallest, towers of different heights, everything thinner toward
  its top, and roofs cut on the slant or steep (marble, gold and the blue
  tiles have slopes: `pitched`, `steeple`, `slant`). Depth in layers, each darker: the wall behind in shadow, bright
  columns in front of it (`pillar`, `arch`), the hero in front of those. One
  bay (a column, an arch, a window) repeated along a hall. Detail in few
  places, with rest between: capitals, battlements, a lit window, a banner,
  the rod on a roof. A small palette: marble light and dark, blue for roofs
  and cloth, gold for what matters, a warm light in the windows. And what a
  thing is, is said by its parts: towers of many heights, battlements,
  banners and gates say castle, where a dome, a bell and a rose window said
  church (`turret`, `battlement`, `skybridge`). And a thing has
  **volume** when its light has a side: a tower is round, so its stone is lit
  toward the sun, plain on its face and in shade away from it, its head
  stands out on corbels and casts a shadow under itself, its foot is wider
  too; its roof rises two cells for one to a point (steep slopes, `cone`),
  the half away from the sun darker; and behind the near towers stand towers
  further back, paler and bluer under paler roofs (`backtower`), so a
  castle has a front and a behind, with the far castles of the backdrop behind
  that. And size against the hero: a door is more than twice him, a column
  eight, the dome thirty.
- **It is drawn as the Earth is**: what stands in Paradise is not laid out of
  tiles but drawn free, at the detail of the Earth's trees and houses, as
  pictures the map stands in its cells (`tv2_heaven.py`, with the Earth's own
  brushes; "Paradise's pictures" in the standard's document). Its **trees**:
  four in blossom and four with leaves of gold, 9 to 13 tiles high, pale
  trunks that part into branches, crowns of rounded bundles. Its **towers**:
  round, of dressed marble in courses, each block its own shade, bands of
  stone round them, windows with a light behind them, a head on corbels, a
  roof of blue tiles laid in rows with a lip of gold and a rod; the ones
  further back paler and plainer; the ones at the great tower's corners hung
  on the wall, their feet narrowing to a point. The **great roofs** (the
  citadel's, the keep's, the pavilion's): rows of tiles, a gabled window with
  a light in it on either face, a crest of gold, a rod. The **citadel's
  hall**: either side one picture (the wall out of the sun, tall windows of
  warm glass in lead, fluted columns with capitals, round arches of cut
  stones, a band with a line of gold, a banner with the wings on every
  column, the chains its lanterns hang on), and behind the high seat one wall
  41 tiles high: a throne of gold with a cushion of blue, the wings of the
  host fanned in a ring of light, windows of cool glass, and ranks of lit
  windows up the tower. The **House's** two halls and its keep have their
  walls drawn (downstairs the windows are openings the sky shows through),
  its columns, the bridges between its towers, its battlements. The Orchard's
  **round temple** (six columns, the far ones paler, a band, a drum with a
  ring of windows, a ribbed dome of gold) and the **host** in marble at
  either hand, wings in three ranks of feathers, hands on a sword. The
  Garden's east gate, the porch of the House, the bridge of marble with its
  rail of balusters and its piers that end in the air. **What stays tiles** is
  what the hero touches: the ground of the islands, the floors, the walls
  that stop him, the roofs he stands on (under each tower's drawn roof are
  the map's own steep roof tiles), the ledges, the piers of gold that hold a
  gate, the glass that lets the light into a hall. None of the pictures is
  solid and none shades.
- **Heaven is behind all of it**, and little bare sky shows (`heaven` layers
  of the backdrop, far to near): a country of cloud, towers of it one behind
  another; castles far off on banks of cloud, towers under pointed roofs of
  blue, keeps, walls on arches, threads of falling water; islands hanging in
  the air; thin cloud high up; a second sea of cloud, the one the citadel's
  island stands in, over the one the Garden's stands in; shafts of light; and
  wisps of cloud that pass in front of the hero on the stairs. A glory of pale
  gold, rings and rays, stands behind the great tower. All of it takes the
  hour's tint: the seas of cloud are pink at sunset, and the far castles stand
  dark against the stars at night.
- **The sky moves.** Pennants fly from the tallest towers, a wave running
  along the cloth (`@mark flag`, `TilesV2Flag`). Petals fall from the trees in
  blossom and leaves of gold from the golden ones, from whichever crowns are
  near the view (`TilesV2Petals`: the upper part of each tree's picture). Where falling water ends, in the lake, in
  the Hollow's pool and in the Earth's pond, spray leaps and a mist hangs
  (`TilesV2Particles.Spray`). Now and then a flock of white birds crosses the
  sky far off in a wedge (`TilesV2Birds`). The rainbow is the sun's: it
  breathes, and it is gone by night (`TilesV2Shimmer`).
- **Small clouds** drift in the empty sky (`wisp`: stood on, like any cloud).

The model plays Paradise as it plays the rest: every light, relic, upgrade and
place to rest is reached, and the hero gets home from all of it. It lets him
through the Temple's door (the lights are goals of their own), and it knows
nothing of the winds, so every gulf is crossed without them.

**Zones.** A circle is not one rock from end to end. Limbo's lower pass runs
into marble in the east (the hall the virtuous raised); the middle of Gluttony
is plain earth; Wrath's east is mud; the west of Heresy's first pass is the
ashstone the city was built on; Violence is bloodstone, then earth under the
wood, then burning sand; the west of Treachery is ice (`ZONES`).

**The road is not a pipe.** Its roof rises and falls in long waves with small
teeth on them, its floor steps up and down by a row, a long stretch dips in
its middle (`walk`, `stroll`), and where it falls from a height into a pool
that is swum, water falls with it.

**The rock is not empty.** Between the roads there are 215 caves no road leads
to (`pockets`): seen from a road through a gap, or not at all. They are the
world's grain, not places to go.

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
- **Fewer gates, each one meant.** A stretch of the road asks for an ability
  once, where it changes how the stretch is crossed; the 113 gates of the first
  layout are 72.
- **A shrine is known by its sign.** Over each ability the wall carries its
  emblem in lamps (`EMBLEMS`), and a tablet by the plinth says in a line what
  the ability is for (`SHRINE_WORDS`).
- **Gauntlets.** In the circles whose liquid burns, a long stretch is crossed
  on stones and platforms that do not stay, some under a wind: the rooms that
  are about moving, not about a key (`gauntlet`).
- **Light tells the roads apart.** The main road is the bright one: a lamp
  every 9 to 14 tiles. A low road has a lamp every 40 to 60 and between them
  only the faint light of its region's air, in the region's colour. And 5
  stretches of the main road, some 20 tiles long, are **dark** (`dark`): no
  lamp in them, before them or just past them, and no light in their air, so
  the hero walks from light into the dark toward the next light. A lamp is seen
  29 tiles off, so the dark needs that much room; it is a passage, never a
  place to jump blind.

## Side roads and secrets

Off the road, each circle has what the critical path does not need:

- **Low roads**: a hatch in the floor (a ledge, dropped through with down and
  jump) leads down to a second way under a stretch of the road and up again
  further on. One holds a relic in a room, one is flooded, one is shut by a gate
  to come back for. They sag in the middle and are dim. Under the longest of
  them there is a third level, a **deep road** reached from the low one
  (9 of them).
- **Attics**: a ladder of ledges through the roof to a nook with a relic, six
  rows apart for the Wings or three for anyone.
- **Secrets**: 22 walls that are not walls. One looks like the rock round it
  but for a few fine cracks and the edge the rock draws against it; two cuts of
  the sword bring it down, and there is a room behind. The chart shows that
  room as rock until the wall is broken, even if the hero has stood beside it.
- **Not every reward is a relic.** Behind the secret walls the rewards go in
  turn: a heart (the game's Kept Light: one more heart), a relic, a draught
  (the game's Vial of the Basin: one more to heal with), a relic, a relic. A
  heart and a draught also lie on high clouds of Paradise. 5 hearts and 5
  draughts in all; the HUD shows the hearts and the draughts he has.
- **Perches** in the halls, the stairs of the great caves, relics under water.
- **Those who are here.** 10 shades stand in the halls and by the gates and
  say a line when the hero stands by them (`SHADES`); 4 souls do in Paradise
  (`SOULS`); 18 tablets tell what a place was (`TABLETS`, the shrines); 30
  posts say where a road goes. The line shows along the foot of the screen
  (`TilesV2Sign`).

The map holds 99 relics, 10 upgrades, 53 places to rest, 23 veils of light,
49 seals and slabs, 18 platforms that do not stay, 8 winds, 62 signs, 5 dark
stretches, 3 lights and the door they open, 902 lamps and about 3900 cells of
ledge. About 40 % of it is open space.

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
4. **The sinks, the side roads, the secrets and the caves in the rock are
   searched for**, after every road is laid (`sinks`, `side_roads`,
   `pockets`): each is checked whole before anything is dug and tried somewhere
   else if it would cut into anything, and none passes under a gate, a shrine
   or a shaft, so none is a way round one.
5. **Noise** shapes only the roofs, the edges between regions and the veins of
   ore. Two spaces that were not meant to meet stop the script (`Clash`), so a
   mistake in the layout cannot quietly open a short cut; a passage's roof
   never breaks into a space dug over it (two cells of ground stay between),
   so a road that comes back under itself does not take the floor from over
   its own head; and what stands (a sign, a place to rest, a light) is checked
   to stand on ground.

A map marks what of the game stands where, for the scene to build
(`TilesV2Things`):

```
@region limbo 236                          a region starts at this row
@mark checkpoint 397 181                   the game's checkpoint (`pit` after it: in an antechamber of the Pit)
@mark ability 230 262 LightDash
@mark barrier 222 260 4                    a light barrier: column, top row, height
@mark seal 524 210 bolt 1 4                a seal: what breaks it (`bolt`, or `slam` for a slab), width, height
@mark secret 241 273 1 3 235 272 240 275 H a wall the sword breaks: its size, the room behind it, the rock it is drawn as
@mark platform 491 571 2 crumble 0.0       a platform that gives way (or `blink`: comes and goes): its length, how far along its cycle
@mark wind 478 344 30 12 50 0 2.5 2.5      a wind: the air it blows in, its speed across and up, how long it blows and rests
@mark relic 267 198
@mark upgrade 297 21 Vial                  an upgrade of the game's: `Heart` or `Vial`
@mark sign 409 181 shade Two ways part ... something that says a line: a `post`, a `tablet`, a `shade` or a `soul`, and the line
@mark dark 135 194 20 14                   a stretch whose air gives no light: column, top row, width, height
@mark beacon 273 123 Deep                  a light of Paradise, and its name
@mark sanctum 259 58 5 3                   the door the lights open: column, top row, height, the lights it asks for
@mark picture 297 111 backdrop_rainbow     one of the tile set's pictures, its foot in the cell (a rainbow)
@mark flag 298 2 -8                        a pennant flying to the right of the cell, moved by so many pixels (none: by none)
@mark end 297 60
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
  shrine (a strand), if a checkpoint, a relic, an upgrade, a light of Paradise
  or the end is never reached, or if with every ability there is a place he
  cannot get home from.

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
| M | the chart: what he has seen so far, a pixel a tile; on it the places to rest (bright once rested at), the shrines not yet emptied and the gates still shut, in the colour of what opens them; beside it the relics found, region by region. The hero stands still while it is open |
| + and -, arrows or W A S D, C (the chart open) | closer (2, 4, 8 times) and back; round the chart; back to the hero |
| E at a rest by the Pit | the list of the other antechambers he has rested in: up, down, Enter to go |
| PgDn / PgUp | jump to the next / last named place |
| F3 | every ability, or none (for trying a circle out of order) |
| F4 | the whole chart |
| R | back to the last rest |
| 1 2 3 4, T, P | the hour of the day (it shows on the surface and in Paradise) |

## Looking it over

To check the map without playing it, photograph all of it at every art pixel:

```
python tools/tiles_v2/photo_world.py              # the world at the day's start
python tools/tiles_v2/photo_world.py --hour 21    # at night
python tools/tiles_v2/photo_world.py --pixels 8   # half the size
python tools/tiles_v2/photo_world.py --sample     # the sample map
```

It takes a minute or two and writes under `Logs/TilesV2/photo/` (or `--out`):
`world.png`, the whole map in one picture to zoom into (8640 x 13504 px, every
torch and blade of grass as the game draws them); `regions/`, the same a region
at a time; and `4k/`, the same cut into 21 sheets of 3840 x 2160 for a viewer
that will not open the whole. The editor runs hidden and must not have the
project open; it builds the world as the game does, stands the map's marks in
it, and photographs it in pieces with the backdrop placed once, so they join
with no seam. The hero and the motes in the regions' air are left out, and the
sky is one plain colour of the hour (the sky's own picture is the size of a
view, not of a world).

It also writes the same with **everything the map marks named on it**
(`--no-labels` leaves this out): `labelled.png` and `labelled/` by region. A
frame stands round the cells of each mark with its name over it: `C1 ...` the
places to rest, the abilities by name, `G1 ...` the gates, `S1 ...` the secret
walls, `R1 ...` the relics, `U1 ...` the upgrades, `T1 ...` the signs, `P1 ...`
the platforms that do not stay, `W1 ...` the winds, `L1 ...` the lights of
Paradise, `D1` the door they open, a violet outline round each dark stretch; a line and a name where each region starts; and the columns and
rows along the edges, a tick every 10 and a number every 20. `labels.txt`
lists every name with what it is, its column, row and region, so "S7 is in the
wrong place" is a thing that can be said.

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
- **The signs are not the game's dialogue**: a line along the foot of the
  screen, with no portraits and no choices, and the shades do not remember
  being spoken to.
- **The hearts, the draughts and the lights of Paradise are not saved**: the
  world has no save of its own, so they are found and lit again on the next
  run.
- **Paradise's pictures are one drawing each**: a tower of a size is the same
  tower wherever it stands (24 towers of 18 kinds), and the hall's banners,
  being part of its picture, do not move (the pennants on the rods do). Nobody
  has yet walked the citadel by hand since its towers became pictures: the
  model plays it, and the tests hold every tower's roof to its tiles.
- **The birds, the petals and the pennants are not in the pictures the editor
  takes** nor in the whole-map photo: they are the scene's, and show in play.
