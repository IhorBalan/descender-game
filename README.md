# Descender

A side-view metroidvania built in Godot 4: a man with half-finished angel
powers fights his way down from Heaven, through Purgatory and the nine
circles of Hell, to rescue his brother.

This repository holds the first playable prototype: two Heaven levels with
the hero, movement, melee combat, enemies, checkpoints and limited healing,
behind a title screen with three save slots.

## Running it

1. Install [Godot 4.7](https://godotengine.org/download) (standard build, no
   .NET needed).
2. Open `project.godot` in the editor and press **F5**, or run
   `godot --path .` from this folder.

To jump straight into a level while testing, run the level scene with its
id: `godot --path . scenes/main.tscn -- --level=heaven_2`. The ids are
listed in `scripts/levels.gd`.

The game renders at **320x180** and scales up by whole numbers, so the
window is 1280x720 by default.

## Controls

| Action  | Keyboard            | Gamepad        |
| ------- | ------------------- | -------------- |
| Move    | A / D or arrows     | Left stick / D-pad |
| Jump    | Space, W or Up      | A              |
| Attack  | J or X              | X              |
| Dash    | K or Shift          | RB or B        |
| Heal    | H                   | Y              |
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
passes through walls of angel light, which block everyone else. Abilities
stay unlocked after death. Healing has three charges that come back when the
hero rests at a checkpoint, which is also where he respawns after dying.
Resting at a checkpoint also saves the game: the level, the checkpoint, the
unlocked abilities and the time played go to the slot the game was started
from, as JSON under `user://saves/`. Loading puts the hero back at that
checkpoint, rested, with the feathers he already took gone from the level.
Restarting with R does the same from the progress so far. Reaching the goal
gate fades out, saves, and starts the next level with the abilities so far;
the gate of the last level ends the prototype.
Spikes cost one heart and put the hero back on the last solid ground.
Imps and angel guards swing when the hero stands close: they stop, glow
orange while winding up, then strike in front of them and pause to recover.
Hitting one during the wind-up cancels the swing. A hit enemy shows its
health for a moment, and a killed one sometimes leaves a mote of light
that restores a heart (angel guards always do).

## Layout

| Path | What it is |
| ---- | ---------- |
| `scenes/title.tscn`, `scripts/ui/title.gd` | Title screen: new game, continue, load a slot, quit |
| `scenes/pause_menu.tscn`, `scripts/ui/pause_menu.gd` | Pause menu: resume, save, quit to title |
| `scenes/settings_menu.tscn`, `scripts/ui/settings_menu.gd` | Settings screen: volume, fullscreen, keyboard and gamepad bindings |
| `scripts/settings.gd` | The `Settings` autoload: reads, saves and applies the settings |
| `scripts/save_manager.gd` | The `SaveManager` autoload: save slots on disk and the save the level applies on boot |
| `scripts/levels.gd` | The `Levels` autoload: the levels in order, by id, with their names and map paths |
| `scenes/main.tscn`, `scripts/main.gd` | Boots a level, spawns the hero, wires HUD and camera, applies and writes saves, moves on to the next level at the gate |
| `scripts/screen_fade.gd` | Fades the screen to black between levels |
| `scripts/audio.gd` | The `Audio` autoload: sound effects and the ambient loop |
| `scripts/feedback.gd` | The `Feedback` autoload: hit-stop, screen shake, hit flash and sparks |
| `assets/audio/`, `tools/gen_sounds.py` | Placeholder sounds and the script that synthesises them |
| `scripts/player.gd` | Hero state machine and tuning constants |
| `scripts/enemies/walker.gd` | Ground patroller (imp, angel guard) with a telegraphed melee swing |
| `scripts/enemies/flyer.gd` | Hovering Watcher that chases when close |
| `scripts/enemy_health_bar.gd` | Health bar that appears over a hit enemy |
| `scripts/heal_mote.gd` | Mote of light some enemies drop; touching it restores a heart |
| `scripts/level_builder.gd` | Builds tiles, collision, spikes, enemies and objects from an ASCII map |
| `scripts/ability_pickup.gd` | The feather that unlocks an angel ability on touch |
| `scripts/light_barrier.gd` | Wall of light the hero can only light-dash through |
| `levels/level_01.txt`, `levels/level_02.txt` | The levels. Edit them in any text editor |
| `scripts/anim_loader.gd` | Builds animations from the strips in `assets/sprites/<name>/` and `sprites.json` |
| `assets/sprites/` | Character strips from the art thread, plus generated tiles, checkpoint and gate |
| `assets/ui_theme.tres` | The menus' font size and button styles |
| `tools/gen_placeholders.py` | Regenerates the tiles, checkpoint, gate, feather and light barrier PNGs |
| `tools/hero_cutout/` | The hero's SpriteCook base art and the cut-out animation script; `export.py` rewrites the hero strips |
| `tools/write_level.py` | Prints the level with a column ruler (`--write` regenerates the draft) |
| `tests/smoke_test.tscn` | Headless test that drives the hero and checks the basics |

### Level legend

```
#  solid tile      ^  spikes           P  hero start
w  imp (walker)    g  angel guard      f  watcher (flyer)
C  checkpoint      G  goal gate        .  empty
W  feather (unlocks the wings)       L  feather (unlocks the light dash)
|  wall of light (needs the light dash)
```

Tiles are 16x16. The hero is about two and a half tiles tall and jumps
just over four tiles high, or eight with the wings; a seven tile pit needs a
jump plus a dash. The climb to the goal gate needs the wings, and the wall of
light in front of it needs the light dash.

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
