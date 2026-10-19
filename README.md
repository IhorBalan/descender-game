# Descender

A side-view metroidvania built in Godot 4: a man with half-finished angel
powers fights his way down from Heaven, through Purgatory and the nine
circles of Hell, to rescue his brother.

This repository holds the first playable prototype: one level with the
hero, movement, melee combat, enemies, a checkpoint and limited healing.

## Running it

1. Install [Godot 4.7](https://godotengine.org/download) (standard build, no
   .NET needed).
2. Open `project.godot` in the editor and press **F5**, or run
   `godot --path .` from this folder.

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
| Restart | R                   | Start          |

Jump height depends on how long the button is held. Dashing gives a few
invulnerability frames. Healing has three charges that come back when the
hero rests at a checkpoint, which is also where he respawns after dying.
Spikes cost one heart and put the hero back on the last solid ground.

## Layout

| Path | What it is |
| ---- | ---------- |
| `scenes/main.tscn`, `scripts/main.gd` | Boots the level, spawns the hero, wires HUD and camera |
| `scripts/player.gd` | Hero state machine and tuning constants |
| `scripts/enemies/walker.gd` | Ground patroller (imp, angel guard) |
| `scripts/enemies/flyer.gd` | Hovering Watcher that chases when close |
| `scripts/level_builder.gd` | Builds tiles, collision, spikes, enemies and objects from an ASCII map |
| `levels/level_01.txt` | The level. Edit it in any text editor |
| `scripts/anim_loader.gd` | Builds animations from the strips in `assets/sprites/<name>/` and `sprites.json` |
| `assets/sprites/` | Character strips from the art thread, plus generated tiles, checkpoint and gate |
| `tools/gen_placeholders.py` | Regenerates the tiles, checkpoint and gate PNGs |
| `tools/write_level.py` | Prints the level with a column ruler (`--write` regenerates the draft) |
| `tests/smoke_test.gd` | Headless test that drives the hero and checks the basics |

### Level legend

```
#  solid tile      ^  spikes           P  hero start
w  imp (walker)    g  angel guard      f  watcher (flyer)
C  checkpoint      G  goal gate        .  empty
```

Tiles are 16x16. The hero is about two and a half tiles tall and jumps
just over four tiles high; a seven tile pit needs a jump plus a dash.

## Tests

```
godot --headless --path . --script tests/smoke_test.gd
```

Exits non-zero when a check fails. The same test runs on every pull request
through `.github/workflows/tests.yml`.

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
