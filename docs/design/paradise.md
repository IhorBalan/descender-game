# Paradise: level design

Status: **draft for approval**. Replaces the prototype Paradise (the 480-tile
region ported from the Godot prototype at tag `godot-last`), which stays playable
in `Main.unity` until the new one is ready. Paradise is rebuilt from scratch in
Unity, as the game's first production-quality region.

Story source: `docs/story/script.md`, sections 4 (Heaven 1 and 2) and 5.

**Scope of this phase: the map, atmosphere and lighting.** Enemies come
later: the layout marks where encounters will go (arenas, patrol ledges,
flying lanes) but places no enemies. The Warden boss arena is built as a
space; the fight is added with the enemies.

---

## 1. Pillars

1. **Breathtaking, then wrong.** Paradise is the most beautiful place in the
   game. Over its length the beauty turns uncanny: the garden is a farm. The
   player should feel it before any line of dialogue says it.
2. **Movement feels like flying.** Long flowing runs along curved branches,
   big jumps over luminous voids, few dead stops. The level is built to the
   hero's numbers so that a good player never has to stop to think about
   whether a jump is possible.
3. **Big, never empty.** A 20-minute straight run across, about 2 hours of
   play with exploration. Every screen has something to look at, something
   to take, or somewhere to go.
4. **Read at a glance.** What you can stand on, what hurts you, what is
   background: clear on every screen, however rich the art.

## 2. Scale and metrics

### Size

| | Value |
| --- | --- |
| Straight run, side to side, no stops | **~20 min** |
| Run speed | 100 px/s = 6.25 tiles/s |
| Width | **7,500 tiles** (120,000 level px, 7,500 Unity units) |
| One screen | 20 x 11.25 tiles (the camera's own size, 5.625 units; 320 x 180 at 16 px/tile) |
| Width in screens | ~375 |
| Height | **120 rows** (the prototype has 51): canopy above, roots below |
| Play time, first visit | ~2 h with exploration, side paths and story |

A tile is 16 level px, one Unity unit. Region coordinates stay below 32,767
tiles, so `RegionTileData`'s int16 cells still fit.

### Movement (from `PlayerTuning`)

| Move | Value | In tiles |
| --- | --- | --- |
| Jump height | 72 px | 4.5 |
| Jump air time (same height) | 0.81 s | about 5 across at full speed |
| Double jump (Wings) | +56 px | 3.5 more, 8 in total |
| Dash | 260 px/s x 0.14 s, ending at 160 px/s | about 2.5 |
| Wall grip, wall jump, glide, slam | later abilities | |

### Construction rules

The numbers the layout is built to, with a margin so jumps feel generous:

| Before Wings | With Wings | With Wings + dash |
| --- | --- | --- |
| Gap on the main path: **4 tiles** max | **7** max | **9** max |
| Step up on the main path: **4 tiles** max | **7** max | |
| Optional or secret jumps: up to 4.5 / 5 (exact limit) | 8 / 9.5 | 11 |

- **Main path:** never needs a perfect jump. **Side paths and secrets** may.
- **Falls:** a fall of more than 2 screens (22 tiles) always lands somewhere
  safe or soft. No falls into nothing: Paradise has no bottomless pits on the
  main path (it is Heaven); hazards are thorns, light and guards.
- **Camera:** the hero sees at least **8 tiles ahead** of any jump he must
  make, and every hazard is on screen before it can be touched.
- **Flow:** runs of at least 30 tiles where holding right, with jumps
  timed to the rhythm, never stops the hero. One in every few screens is a
  flowing run like this.
- **Rest:** a save shrine every **400 to 600 tiles** (about 1 to 1.5 min of
  running; 15 in total), always on the main path and always visible from a
  distance.

## 3. Story in Paradise

Paradise holds the script's Heaven 1 and Heaven 2 beats (1 to 11): zones 1
to 4 are Heaven 1 (the outskirts, where the hero suspects), zones 5 to 7
are Heaven 2 (the terraces and the gate, where he sees). One continuous
region, so the slide from beautiful to wrong is never broken. The hero
leaves through the Low Gate at the far end, down into Ruined Earth.

| # | Beat | Zone | How it is delivered |
| --- | --- | --- | --- |
| 1 | Coming to: Ansel wakes half-made; the Voice says go back | 1 | trigger `opening` |
| 2 | Mereth in the orchard: Tobiah went to the Low Gate "talking the whole way" | 2 | NPC |
| 3 | The harvest token, Tobiah's tally scratched in | 2 | item, hidden near Mereth |
| 4 | The feather: Wings | 3 | ability pickup, end of zone 3 |
| 5 | The ledger page: "Gathered this season: 40. Short: 3." | 4 | item |
| 6 | Ithuriel, the guard who sat down, by a save shrine | 4 | NPC, two talks |
| 9 | The terraces: the gathered walking in lines; the Wheel mural; prayer card, tally stone, Maker's glove | 5 | environment + items |
| 10 | The Making-house from outside: "You left your other half on the table." / "Keep it." | 6 | trigger |
| 7 | The basin shard | 6 | item |
| 8 | The Low Gate's outer door: the Voice, louder | 7 | trigger `gate` |
| 11 | The Warden of the Low Gate: boss; drop through the gate | 7 | boss |

## 4. The route

Seven zones from west to east. Each has its own colour, shape, mechanic and
landmark, so the player always knows where they are.

| Zone | Name | Tiles | Run | Colour | Main mechanic |
| --- | --- | --- | --- | --- | --- |
| 1 | The Waking Hollow | 0 to 400 | 1 min | dawn gold and rose | move, jump, attack (teaching) |
| 2 | The Orchard Under the Canopy | 400 to 1,300 | 2.4 min | deep teal and cyan glow | glowing-fruit lights, soft bounce pods |
| 3 | The Great Vines | 1,300 to 2,600 | 3.5 min | acid green and lime | running along curved branches, vertical climbs |
| 4 | The Tallied Fields | 2,600 to 3,700 | 2.9 min | warm gold meadow | Wings: big jumps; first guard fights |
| 5 | The Terraces | 3,700 to 5,100 | 3.7 min | ivory and pale gold | stepped climbs, light barriers, light dash |
| 6 | The Making-house Approach | 5,100 to 6,300 | 3.2 min | pearl, losing its colour | pipes, moving machinery, uncanny quiet |
| 7 | The Low Gate | 6,300 to 7,500 | 3.2 min | cold silver and blue | the gate, the Warden boss, the drop |

### Zone 1: The Waking Hollow (0 to 400)

- **Feel:** a sheltered hollow of giant curled leaves at sunrise. Safe, soft,
  warm. The first screen is the most beautiful thing the player has seen.
- **Teaching:** walking and jumping first; attacking on harmless seed pods
  that burst into light; a first gap of 3 tiles, then 4.
- **Beat 1** at the start. A save shrine at 300.
- **Secret:** a ledge 6 tiles up, out of reach until Wings. The player sees it
  on the first screen and remembers it.
- **Landmark:** the Rising Leaf, a single enormous curled leaf the hero runs up
  and out of the hollow along.

### Zone 2: The Orchard Under the Canopy (400 to 1,300)

- **Feel:** under a dense canopy the endless sun only reaches in shafts. The
  light comes from the orchard itself: glowing fruit, luminous fungi, motes in
  swarms. Teal and cyan, quiet, magical.
- **Mechanic:** **bounce pods**, giant soft seed heads that throw the hero
  6 tiles up. They teach vertical play before Wings, and come back later as
  shortcuts.
- **Beats 2 and 3:** Mereth tends a tree full of tally-marked fruit; the token
  is hidden in the roots below her (a short side path).
- **Hidden truth:** every tree is numbered and the fruit grows in exact rows.
  Beautiful, and a little too tidy.
- **Landmark:** the Lantern Tree, a huge tree whose fruit lights a whole
  screen. A save shrine sits in its roots.

### Zone 3: The Great Vines (1,300 to 2,600)

- **Feel:** out of the orchard into a forest of giant vines and trunks in
  acid green: twisting, spiralling, layered deep into the mist. The flowing
  heart of Paradise.
- **Mechanic:** running along **curved branches** that rise and fall like
  waves; long climbs up hollow trunks using pods and ledges; the first
  **hazard**, thorned briars, which hurt but never kill outright.
- **Encounter spaces (enemies later):** open air lanes between the vines
  for flying enemies.
- **Beat 4:** the feather at the top of the Crown Vine, the highest point of
  the region so far. Getting Wings lets the hero jump off the top into a long
  flowing descent built for double jumps: the reward is to feel the new
  ability straight away.
- **Landmark:** the Crown Vine, a trunk tall enough to fill five screens.

### Zone 4: The Tallied Fields (2,600 to 3,700)

- **Feel:** out from under the trees into open golden meadows under the full
  sun. Wide, bright, big sky. In the distance, lines of the gathered walk
  calmly, all in the same direction.
- **Mechanic:** **Wings play**: wide gaps of 6 to 7 tiles over glowing
  streams, high floating islands. Wide flat stretches
  laid out as patrol grounds for the angel guards to come; Ithuriel's calm
  will show that not all of them fight.
- **Beats 5 and 6:** the ledger page in a field-keeper's hut; Ithuriel sits
  by the Field Shrine save point, humming Tobiah's song.
- **Backtrack:** from here Wings opens the secrets of zones 1 to 3; a fast
  return path (a gust that carries the hero back up the Crown Vine) makes it
  painless.
- **Landmark:** the Sundial, a huge stone sundial whose shadow never moves.

### Zone 5: The Terraces (3,700 to 5,100)

- **Feel:** white and gold terraces stepping up a mountainside, gardens on
  every level, waterfalls of light. Order everywhere: straight lines,
  symmetry. The Wheel mural covers a whole cliff.
- **Mechanic:** **vertical climbing** up the terraces; **light barriers** that
  only open from the far side (one-way doors, shortcuts back); the **light
  dash** found halfway, then used to cross gaps of 8 to 9 tiles.
- **Encounter spaces (enemies later):** terrace landings sized for guard
  fights; air above the gaps for flyers.
- **Beat 9:** the gathered walk past the hero without looking at him. Items:
  the child's tally stone, the Warden's prayer card ("What is pressed is not
  lost"), the Maker's glove.
- **Landmark:** the Wheel mural, the biggest single image in the region,
  seen from below as the hero climbs.

### Zone 6: The Making-house Approach (5,100 to 6,300)

- **Feel:** the most important change in the level. The colour drains to
  pearl. The music thins. The garden is still beautiful, but now there are
  **pipes** among the roots, warm and humming, all running one way. The
  gathered walk in through a vast white door and do not come out.
- **Mechanic:** **machinery**: moving platforms on the pipes, steam vents
  that lift the hero, timed shutters. Precision rises a little.
- **Beats 10 and 7:** the Making-house trigger where the hero sees the door;
  the basin shard in a drain below it (a side path into the pipes).
- **Landmark:** the Making-house itself, in the background for the whole
  zone, growing closer.

### Zone 7: The Low Gate (6,300 to 7,500)

- **Feel:** past the edge of the garden: cold silver cliffs, wind, the sky
  turning from gold to steel blue. Below, through cloud, dark.
- **Mechanic:** a **descent**: glide-like falls between cliff ledges, wind
  that pushes, a last climb to the gate.
- **Beat 8:** the Voice at the outer door. **Beat 11:** the Warden of the Low
  Gate, in an open arena at the cliff's edge (built now as a space; the
  fight comes with the enemies). After it, the hero drops
  through the gate: the region ends, Ruined Earth begins.
- **Landmark:** the Low Gate, a vast ring of silver in the cliff, visible as a
  small shape from zone 5 on, so the player always knows where the road goes.

### Totals

| | Count |
| --- | --- |
| Save shrines | 15 |
| NPCs | 2 (Mereth, Ithuriel) |
| Story items | 7 (token, ledger page, tally stone, prayer card, Maker's glove, basin shard, plus the feather) |
| Abilities | 2 (Wings in zone 3, light dash in zone 5) |
| Secrets | 3 to 5 per zone, at least one needing an ability from a later zone |
| Bosses | 1 arena (the Warden; the fight comes later) |

## 5. Visual direction

**A sunlit celestial garden** (art direction set 2026-11-03 from the user's
reference). Paradise is built on cliffs and terraces rising out of a sea of
clouds: white and cream marble with gold trim (round arches on slender
columns, arcades, aqueduct bridges, colonnades, balustrades, sun banners,
small lanterns), all of it overgrown: ivy and vines draping from every arch,
white lilies and blossoms, moss and grass on every top. Waterfalls pour from
terrace edges and arcades into the clouds far below. Bright blue sky, soft
cumulus, warm golden sunlight with god-rays. Beyond: floating islands with
waterfalls and small white temples, a ring gate, snowy peaks in blue haze, and
on the highest island a great tree with a white-gold crown, the region's far
landmark. The story's turn keeps its place: the beauty is real, and from
zone 4 on the order (rows, symmetry, the pipes) creeps in until zone 6 drains
it to pearl.

### Everything is connected (hard rules, checked by the builder)

1. **Nothing floats without a reason.** Every walkable surface stands on
   something the eye accepts: rock that goes down into the clouds, or
   architecture whose columns reach the ground, a cliff, or the cloud sea.
2. **Bridges, walkways, balconies and landings are architecture.** A deck
   has columns (and arches between them) down to the first surface below it,
   or into the cloud sea. The builder adds them and refuses to build a deck
   with no support.
3. **Rock that floats is an island**, declared as such: a rocky underside,
   roots and vines hanging, usually water falling off it.
4. **Ground goes down.** Terrain bodies reach the cloud sea at the bottom of
   the band; the clouds hide where they end.
5. **Water comes from somewhere and goes somewhere:** a waterfall starts at a
   stream, a pool or an aqueduct channel and falls into a pool or the clouds.
6. **Props sit on surfaces** (their height is found on the terrain), hang from
   something (banners, lanterns, ivy from arches and decks), or are far
   enough back to be scenery.

**Layers, back to front:** sky; far peaks; floating islands and the golden
tree; nearer cliffs and arcades (all hazed toward the sky colour with
distance); the cloud sea; the play layer (fully lit, crisp); foreground
foliage (dark, out of focus, a few per screen, never hiding a hazard).

**Light and effects:** warm key light from the upper right, cool sky fill in
the shadows; bloom on sunlit marble edges, water and lanterns; god-rays;
waterfalls animated with mist at their feet; drifting petals and pollen in
the sun; plants swaying and bending away from the hero.

**Readability:** walkable tops have a bright grass or moss lip; columns and
arches behind the play layer are slightly cooler and softer than the decks
they hold; hazards are the only red and magenta things in the level.

**Audio cues (for the sound pass):** wind and distant water as the bed,
waterfalls positional, birdsong in zones 1 to 4 that thins from zone 5 on.

## 6. Tech plan

- **Built apart from the prototype.** The prototype game is not changed:
  `Main.unity`, `World.asset` and the prototype's `Region_paradise.unity` stay
  as they are. The new Paradise has its own entry scene,
  `Assets/Scenes/Paradise/ParadiseMain.unity` (a copy of Main whose
  `WorldStreamer` plays `Assets/Data/Paradise/ParadiseWorld.asset`), its own
  region root `Region_paradise_new.unity` and its zone scenes, all under
  `Assets/Scenes/Paradise/`. Play it from ParadiseMain, or use
  **Descender > Play from here** in any Paradise scene.
- **Unity is the source.** The new Paradise is authored in Unity, not generated
  from Godot. `ParadiseLayout` + `ParadiseGreybox` (Descender > Paradise > Build
  greybox) write the greybox; once the art pass starts, the zone scenes are
  edited directly.
- **Curved ground: 2D SpriteShape** (`com.unity.2d.spriteshape` 16.0.0).
  Spline-edited ground and branches with a lit top edge and a filled body; the
  colliders are sampled from the same curves, so what you see is what you stand
  on. One-way branches use platform effectors on the Ledge layer.
- **Zones as streamed sub-scenes.** `RegionZones` on the region root loads the
  zones within a margin of the camera and unloads the rest, one at a time;
  entering the world waits for the zones around the arrival point.
- **The band is 7,500 x 120**, in its own world (`ParadiseWorld.asset`), so no
  other region moves.
- **Post-processing on** in ParadiseMain only: bloom, colour adjustments,
  vignette, a URP Volume per zone, blended at the borders. One tonemapper
  (Neutral) for the whole region: tonemapping cannot blend between zones.
- **Art in code:** textures and props generated (Python/numpy, like
  `tools/gen_region_art.py`, plus shaders: wind sway, light shafts, the glow
  shader). Every generated piece has a name and a size, so a painted
  replacement can be dropped in later with no other change.
- **Budgets per screen:** under 300 draw calls with batching; at most
  12 lit point lights in view; particles under 2,000 alive; 60 fps on a
  mid-range PC at 1080p.
- **Tools:** an editor window to place save shrines, items and NPCs on a
  zone; a "play from here" button that starts the game at the selected spot;
  the scripted playtest extended to run each zone's main path and check it
  can be completed.

## 7. Production plan

1. **Approve this document.**
2. **Tech base:** SpriteShape, the zone streaming, the 120-row band,
   post-processing, play-from-here.
3. **Greybox all seven zones:** plain shapes, all paths, saves, beats,
   encounter spaces; checked against the construction rules by a script and
   by play.
   The whole region playable end to end.
4. **Art, zone by zone:** zones 1 and 2 first, as the quality bar for the
   rest. Then 3 to 7.
5. **Polish:** feel (camera, particles reacting to the hero), audio hooks,
   performance, a full timed run (target 20 min).

## 8. Open questions

- **Region order:** the script goes Heaven, Purgatory, Earth; the world goes
  Paradise, Ruined Earth, Purgatory. This document ends Paradise at the Low
  Gate, dropping into Ruined Earth as the world does today.
- **Heaven 2:** in Paradise, as zones 5 to 7 (recommended). If it should be a
  separate region instead, Paradise ends after zone 4 (3,700 tiles, about
  10 min across).
- **Enemies:** deferred to a later phase (decided 2026-11-03). The layout
  leaves the spaces for them.
- **The prototype's extras:** the 3 power-ups and 2 upgrades of the
  prototype Paradise are not placed yet; they go where the economy design
  (`docs/design/economy.md`, `upgrades.md`) wants them.
