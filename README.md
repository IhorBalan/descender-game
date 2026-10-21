# Descender

A side-view metroidvania built in Godot 4: a man with half-finished angel
powers fights his way down from Heaven, through Purgatory and the nine
circles of Hell, to rescue his brother.

This repository holds the first playable prototype: the whole world as one
connected map, exported from the map tool, with the hero, movement, melee
combat, enemies, checkpoints, limited healing and the first scenes of the
story, behind a title screen with three save slots.

## Running it

1. Install [Godot 4.7](https://godotengine.org/download) (standard build, no
   .NET needed).
2. Open `project.godot` in the editor and press **F5**, or run
   `godot --path .` from this folder.

To jump straight into a level while testing, run the level scene with its
id: `godot --path . scenes/main.tscn -- --level=world_1`. The ids are
listed in `scripts/levels.gd`. Two more arguments help with playtesting the
deep regions: `--checkpoint=<n>` starts the hero rested at the level's nth
checkpoint (counted from the top, as the HUD names them) and
`--abilities=all` unlocks every ability and opens the ability seals, so
`godot --path . scenes/main.tscn -- --level=world_1 --checkpoint=12 --abilities=all`
drops a fully able hero in the lower circles. They only apply to a game
started without a save.

The game renders at **320x180** and scales up by whole numbers, so the
window is 1280x720 by default.

## Controls

| Action  | Keyboard            | Gamepad        |
| ------- | ------------------- | -------------- |
| Move    | A / D or arrows     | Left stick / D-pad |
| Drop    | S or Down, with jump | Stick or D-pad down, with A |
| Jump    | Space, W or Up      | A              |
| Attack  | J or X              | X              |
| Dash    | K or Shift          | RB or B        |
| Cast    | L or C              | RT             |
| Heal    | H                   | Y              |
| Talk    | E                   | LB             |
| Pause   | Esc                 | Start          |
| Restart | R                   |                |

The title screen is driven with the arrow keys or the left stick, Space or
A to choose and Esc or B to go back. **New Game** picks a slot (an occupied
one asks to be pressed twice before it is overwritten), **Continue** resumes
the most recent save and **Load Game** picks any slot.

**Settings**, on the title screen and in the pause menu, has the master
volume, a fullscreen toggle and the bindings: pick an action, press the
new key (a key taken from another action is moved, not shared) or Esc to
keep the old one. The **Bindings for** button flips the list between the
keyboard and the gamepad, which are rebound separately. Settings live in
`user://settings.cfg`.

Pausing opens a menu to resume, save or quit to the title. Saving there
records the last checkpoint, which is where the save will resume.

The top left of the screen shows hearts, healing charges, the angel
abilities unlocked so far and a small bar that fills as the dash comes
back. Jump height depends on how long the button is held. Dashing gives a few
invulnerability frames. Picking up the feather unlocks the **wings**: press
jump again in the air for a second, slightly lower jump. The second feather,
by the checkpoint, unlocks the **light dash**: the dash goes further and
passes through walls of angel light, which block everyone else. Deeper
down, the **wall grip** feather lets the hero cling to a wall by holding
towards it, sliding slowly, and spring away from it with jump (which also
gives the wings and the dash back); the **glide** feather lets him press
jump again while falling, once the wings' jump is spent, and hold it to
float down. **Holy Light** throws a bolt of light from the hand with the
cast key: it flies straight, hurts the first enemy it meets and breaks on
the world. **Smite** is down and attack in the air: the hero slams to the
ground, through any ledge in the way, and the landing hurts everything
around his feet for two. Abilities stay unlocked after death. Healing has three charges that come back when the
hero rests at a checkpoint, which is also where he respawns after dying.
Resting at a checkpoint also saves the game: the level, the checkpoint, the
unlocked abilities, the story flags and the time played go to the slot the
game was started from, as JSON under `user://saves/`. Loading puts the hero back at that
checkpoint, rested, with the feathers he already took gone from the level.
Restarting with R does the same from the progress so far.

Thin ledges can be jumped up through from below, and the hero drops
through one by holding down and pressing jump.

The map's **locked doors** are seals of angel light across the shafts and
doorways between rooms. A seal lifted by an ability breaks the moment the
hero learns it; one held by a boss breaks when that boss dies. Touching a
seal says what it wants. Every boss room with a profile holds a **boss**:
walking in seals the room's doors, plays the boss's lines and shows its
health bar at the bottom of the screen. The boss walks at the hero, swings
with a wide telegraphed sweep and charges across the room when he keeps
away; at half health it quickens and leaps after every charge. Killing it
sets a story flag (`boss:<id>`), saves the game, opens the doors it held
and plays its last lines; a dead boss stays dead in the save. Every boss
room has a profile now (the strips of the guard, imp, brute or hero, scaled
and tinted, with their own numbers): the Seraph Warden, the Penitent, the
Limbo Lord, the Keeper of the Palace, the Bloated One, the Warden of the
Vault, Gorrow the Fury, the Cursed Bishop, the Champion of the Red Arena,
the False King and, at the bottom, the Lowest. Beating Gorrow in Wrath is
the story's reveal: it sets the `reveal` flag the survivors on Earth react
to, and Gorrow no longer waits in Lust afterwards (an NPC placement's
`unless` flag).

Hidden in the levels are **permanent upgrades**: a Kept Light adds a heart
and a Vial of the Basin adds a healing charge, for the rest of the save. Each
reads like a relic when it is taken, and a taken one stays gone. The sword
is hardened at **Old Pell's forge** on Earth: his third talk uses his own
stock of church iron (one more damage), and a bar of Church Iron found in
Heresy, carried back to him, adds another. **Vael the Bargainer** in Greed
trades once the hero knows the name below: a memory (flag `sold_winter`)
for the clock key (`item:clock_key`). Nothing is bought with a currency;
a dialogue entry's `sets` may be a list of flags for trades like these. There are
also **power-ups** lying in rooms: a Halo turns the next hit aside, a Lit
Blade makes the sword hit harder and Zeal makes the hero run and dash faster,
each for twenty seconds. Power-ups are lost on death and come back with the
enemies. The design, with how many of each the whole game needs and where
they go, is in [`docs/design/upgrades.md`](docs/design/upgrades.md). Reaching a goal
gate fades out, saves, and starts the next level with the abilities so far;
the gate of the last level ends the prototype. The world has no gate yet:
it is one map from top to bottom.
Spikes cost one heart and put the hero back on the last solid ground.
Imps and angel guards swing when the hero stands close: they stop, glow
orange while winding up, then strike in front of them and pause to recover.
Hitting one during the wind-up cancels the swing. A hit enemy shows its
health for a moment, and a killed one sometimes leaves a mote of light
that restores a heart (angel guards always do).

The story is told in text boxes. Standing next to someone, or to a relic,
shows the talk key above them; pressing it opens a box at the bottom of the
screen, and the same key (or jump) turns the page. The hero stands still and
cannot be hurt while a box is open. People say different things once the
hero has heard their first lines, relics are gone once read, and a few spots
in the level speak on their own the first time the hero walks through. What
he has seen is kept as story flags in the save. Everyone has a few unique
conversations, in order, some of them unlocked by what the hero has found;
once those are used up they rotate through a handful of repeat lines, one
per talk. The script for the whole game, with the lines that are in the
levels and the ones that are not yet, is in
[`docs/story/script.md`](docs/story/script.md).

Below Paradise the hero lands in Ruined Earth, the hub region under the
hole in the sky, with its own ambient tint and sound, where four survivors
(Old Pell, Hanne, Corporal Dace and Sister Ava) and three relics tell what
the war looks like from the ground. The passage at its right end leads on
to Purgatory.

Below the hub the world is peopled: imps, angel guards, watchers and the
**brute** (slow, eight hits, a two-heart swing) stand through Purgatory
and circles one to five at a rising density, all listed in
`placements.json`. Fallen imps in Limbo and Lust shout a line from the
script as they wind up. The story goes on down there too: the names in
the Purgatory shaft, the Ferryman (who hands over the clock's hand), the
imp's tally bone in Limbo, Gorrow waiting by the seal in Lust, the wall in
Gluttony that says DON'T FOLLOW, and Vael the Bargainer in Greed. In
Heresy the hero sees the Basin's pipes, meets Tobiah's messenger (a
sent-down angel Tobiah speaks through) and can find the Choir's tablet and
the clock weight; in Violence Vael waits again, with the pipe section and
the clock chain below him. Fraud is the last camp: the army rests there
(`passive` enemies, which patrol and never swing), an imp offers a tally
bone, and the roster lies among them. At the bottom of Treachery the
Lowest speaks before the fight and after it, and the last line is a
choice (left and right pick, interact confirms): carry him or sit down.
Either ends the game. The clock in the Time Clock room, once all five
pieces are found, sends the hero back up to the start of the world
(`warp` on its entry), where the Gardener waits in the Warden's room for
the last fight and the third ending. A dialogue entry's `then` names the
entry that follows it, a line's `choices` list the options with their
`then`, and an enemy placement may name a boss profile as its `character`
and a flag it `requires` (read whenever the enemies spawn).

## The world

The map is one world, 480 x 459 tiles, exported by the map tool as
`levels/world/world.json` with three tile sheets next to it: the diffuse
tiles, their normal map and the emissive glow. It is twelve regions stacked
top to bottom: Paradise, Ruined Earth, Purgatory and the nine circles from
Limbo to Treachery, each with an ambient colour that tints the screen while
the hero is in it. The export also holds the rooms, the doors between them
(some locked by an ability or a boss), 853 point lights and the markers.

`scripts/level_builder.gd` builds it at boot: one `TileMapLayer` per layer
(Background, Decor, Terrain, Glow), collision merged from the terrain kinds
(solid, one-way ledge, hazard), a `PointLight2D` per light (without shadows,
which would need an occluder on every tile), and the markers: the start
puts the hero down, every save is a checkpoint, the ability markers for the
wings and the halo dash are feathers, every boss marker with a profile is
a `Boss` (the rest get an angel guard standing in), and every locked door
gets a seal (`scripts/door_lock.gd`). All six abilities have feathers now.
The other markers (secrets, shops, the gate, the clock) are data in the
JSON until there is something for them to do.

Everything else that stands in the world is listed in
`levels/world/placements.json`, which also names the dialogue file: an
`objects` list, each with a `type` and the tile cell `x`, `y` its feet are
on. The types are `npc`, `item` and `trigger` (story objects, with the
fields the dialogue system wants), `enemy` (`character`: `imp`,
`angel_guard`, `watcher` or `brute`), `upgrade` (`heart`, `vial` or `iron`), `power`
(`halo`, `blade` or `zeal`) and `barrier` (a wall of light `height` cells
tall). Upgrades are numbered by their order in the list, so a save keeps
them straight as long as that order does not change.

To bring in a new export, unzip it over `levels/world/` and keep
`placements.json`. The tool's own `import_world.gd` is not used: the game
reads the JSON directly, so the map stays editable in the tool. This
settles the level pipeline question (#34): big maps come from the tool as
tile layers, and the hand-written ASCII levels of the first prototype are
gone.

## Layout

| Path | What it is |
| ---- | ---------- |
| `scenes/title.tscn`, `scripts/ui/title.gd` | Title screen: new game, continue, load a slot, quit |
| `scenes/pause_menu.tscn`, `scripts/ui/pause_menu.gd` | Pause menu: resume, save, quit to title |
| `scenes/settings_menu.tscn`, `scripts/ui/settings_menu.gd` | Settings screen: volume, fullscreen, keyboard and gamepad bindings |
| `scripts/settings.gd` | The `Settings` autoload: reads, saves and applies the settings |
| `scripts/save_manager.gd` | The `SaveManager` autoload: save slots on disk and the save the level applies on boot |
| `scripts/story.gd` | Story flags: who the hero has talked to, what he has seen and read; saved with the game |
| `scripts/dialogue/dialogue_data.gd` | Reads a level's dialogue file and picks the entry whose flags match |
| `scenes/dialogue_box.tscn`, `scripts/ui/dialogue_box.gd` | The text box: speaker, typed text, paging |
| `scripts/interactable.gd` | Base for things the hero can talk to or examine; shows the talk key when he is close |
| `scripts/npc.gd`, `scripts/lore_item.gd`, `scripts/story_trigger.gd` | Someone to talk to, a relic with a description, a spot that speaks on its own |
| `dialogue/world_1.json` | Every line in the world, by id, with the flags each needs and sets, and whether it plays once |
| `levels/world/placements.json` | Where the world's NPCs, relics, triggers, enemies, upgrades, power-ups and walls of light stand, and the NPCs' unique and repeat lines |
| `tools/npc_sprites/` | The NPC rig (`npcs.py`) and `export.py`, which draws the idle and talk strips into `assets/sprites/<name>/` |
| `docs/story/script.md` | The story: names, beats from Heaven to both endings, and the script |
| `scripts/levels.gd` | The `Levels` autoload: the levels in order, by id, with their names, export paths, sky and starting ambient sound |
| `scenes/main.tscn`, `scripts/main.gd` | Boots a level, spawns the hero, wires HUD and camera, applies and writes saves, moves on to the next level at the gate |
| `scripts/screen_fade.gd` | Fades the screen to black between levels |
| `scripts/audio.gd` | The `Audio` autoload: sound effects and the ambient loop |
| `scripts/feedback.gd` | The `Feedback` autoload: hit-stop, screen shake, hit flash and sparks |
| `assets/audio/`, `tools/gen_sounds.py` | Placeholder sounds and the script that synthesises them |
| `scripts/player.gd` | Hero state machine and tuning constants |
| `scripts/enemies/walker.gd` | Ground patroller (imp, angel guard, brute) with a telegraphed melee swing and, for some, a bark |
| `scripts/enemies/flyer.gd` | Hovering Watcher that chases when close |
| `scenes/enemies/boss.tscn`, `scripts/enemies/boss.gd` | A boss: profiles by marker label, two phases, the fight main.gd runs around it |
| `scripts/door_lock.gd` | A seal of light on a locked door, or on a boss room's doors during the fight |
| `scripts/enemy_health_bar.gd` | Health bar that appears over a hit enemy |
| `scripts/heal_mote.gd` | Mote of light some enemies drop; touching it restores a heart |
| `scripts/world_map.gd` | Reads a world export: layers, terrain kinds, regions, rooms, doors, markers and lights |
| `scripts/level_builder.gd` | Builds the tile layers, collision, lights, enemies and objects from a world export and its placements |
| `scripts/ability_pickup.gd` | The feather that unlocks an angel ability on touch |
| `scripts/light_bolt.gd` | The bolt of Holy Light the hero throws |
| `scripts/upgrade.gd`, `scripts/power_up.gd` | A permanent upgrade (heart, healing charge) and a timed power-up (Halo, Lit Blade, Zeal) |
| `docs/design/upgrades.md` | The upgrade and power-up design: kinds, counts, placement rules |
| `scripts/light_barrier.gd` | Wall of light the hero can only light-dash through |
| `levels/world/` | The world export (`world.json` and its three tile sheets) and `placements.json` |
| `scripts/anim_loader.gd` | Builds animations from the strips in `assets/sprites/<name>/` and `sprites.json` |
| `assets/sprites/` | Character strips from the art thread and the NPC rig, plus the generated checkpoint, gate and pickups |
| `assets/ui_theme.tres` | The menus' font size and button styles |
| `tools/gen_placeholders.py` | Regenerates the checkpoint, gate, feather, light barrier, relic, upgrade and power-up PNGs |
| `tools/hero_cutout/` | The hero's SpriteCook base art and the cut-out animation script; `export.py` rewrites the hero strips |
| `tests/smoke_test.tscn` | Headless test that drives the hero and checks the basics |

## Tests

```
godot --headless --path . res://tests/smoke_test.tscn
```

Exits non-zero when a check fails. The same test runs on every pull request
through `.github/workflows/tests.yml`. It runs as a scene rather than a
`--script` so the autoloads exist before the game scripts compile.

## Sounds

Every sound under `assets/audio/` is a synthesised placeholder. To change
one, edit its recipe in `tools/gen_sounds.py` and run
`python3 tools/gen_sounds.py` (needs numpy). Effects are played by name
(`Audio.play("jump")`), so a real recording only has to keep the file name.

The same workflow also lints and format-checks GDScript with
[gdtoolkit](https://github.com/Scony/godot-gdscript-toolkit). To run it locally:

```
pip install "gdtoolkit==4.5.0"
gdlint scripts tests tools
gdformat scripts tests tools   # rewrites files; add --check to only report
```

## Pull request reviews

Every pull request gets an automatic review from Claude Code (see
`.github/workflows/claude-code-review.yml`). The full review runs when a PR is
opened, reopened or marked ready for review; drafts are skipped. Later pushes
get a short follow-up that looks only at the new commits and reports blocking
bugs only. What the reviewer checks, skips and how many nits it may post is set
in [`REVIEW.md`](REVIEW.md).
