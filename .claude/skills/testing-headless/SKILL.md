---
name: testing-headless
description: The headless test scenes (unit_test, map_pipeline_test, smoke_test), how to run them locally and in CI, how to add checks, the EXPECTED_CHECKS counters, lint with gdtoolkit, and what the Claude PR review looks for. Use before opening a PR, when a test fails, or when adding anything that moves or any data the tests cross-check.
---

# Testing

## Run

```
godot --headless --path . --import                 # once after a fresh checkout or asset change
godot --headless --path . res://tests/unit_test.tscn
godot --headless --path . res://tests/map_pipeline_test.tscn
godot --headless --path . res://tests/smoke_test.tscn
pip install "gdtoolkit==4.5.0" && gdlint scripts tests tools && gdformat --check scripts tests tools
```

CI (`.github/workflows/tests.yml`) runs exactly that with Godot 4.7.2 on
every pull request: a `lint` job and a `smoke-test` job. Each test scene
exits non-zero when a check fails; the tests are **scenes**, not
`--script`s, so the autoloads exist before game scripts compile. The
`tools/*.gd` scripts (`check_terrains.gd`, screenshots) are `SceneTree`
scripts run with `--script`.

## The three tests

- **`tests/unit_test.gd`** (seconds, no world). `_ready` calls every
  `test_*` method in order; `_check(ok, label)` counts. The data half
  cross-checks files the game reads: placements against sprites, dialogue
  ids, `then`/choices/flags, `sprites.json` strips against their
  `SpriteFrames`, enemy bodies against profiles and scenes, region PNGs,
  and every `Audio.play("...")` against a file. The unit half covers story
  flags, dialogue, quests, the levels registry, upgrades and save slots
  (writes and restores the last slot). Scratch goes under
  `user://unit_test_*`, cleaned up at the end.
- **`tests/map_pipeline_test.gd`** (`EXPECTED_CHECKS = 235`): the level
  registry, export reader, Firestore parsing, map store cache, free-placed
  sprites, backdrop lookup. Builds under `user://map_pipeline_test/`.
- **`tests/smoke_test.gd`** (`EXPECTED_CHECKS = 675`,
  `LAST_TICK = 2380`): one scripted run through the whole game. It
  `seed(1)`s, clears `MapStore.chosen_level`, sets itself
  `PROCESS_MODE_ALWAYS` (so pause checks tick), steps aside as current
  scene, instantiates `main.tscn` and then drives the hero in
  `_physics_process` with `match _ticks:` cases, pressing actions with
  `_press("jump", true)` (`Input.action_press`) and asserting with
  `_check`. Helpers teleport the hero (`_place_hero_for_enemy`), spawn
  enemies (`_spawn_walker(character, behaviour, distance)`,
  `_spawn_boss`), watch shots and shake. If the last step never runs,
  the post-`LAST_TICK` guard exits 1 so a script error cannot pass.

## Adding a check

1. Pick the test: data cross-reference or pure logic goes in
   `unit_test.gd` as a new `test_<thing>` (or a `_check` in the existing
   one); anything that needs the level built or the hero moving goes in
   `smoke_test.gd`.
2. In the smoke test, add a `match` case at a free tick after the related
   steps, press and release actions on different ticks (a press needs a
   physics tick to be read; Area2D overlaps need one more), and check
   observable state: `_player.state`, `global_position`,
   `Audio.last_played` / `frame_played`, `Feedback.spark_count()`,
   `dusts_raised`, HUD text, `SaveManager.read(...)`.
3. Update `EXPECTED_CHECKS` by the number of `_check` calls you added
   (and `LAST_TICK` if you added a later tick). The report fails on a
   mismatch, which is how a skipped branch is caught.
4. Keep ticks deterministic: no `randi()` without the seed, no
   wall-clock timers (`Feedback.hit_stop` uses real time; tests wait for
   `is_hit_stopped()` to clear rather than counting ticks).
5. Run all three locally; the smoke test takes about a minute headless.

## Writing testable game code

- Read input through actions (so `Input.action_press` works), make state
  observable (public `state`, counters like `spark_count()`), expose
  `last_played` style records instead of asking the test to sniff nodes.
- Guard window and display calls with
  `DisplayServer.get_name() == "headless"`; audio works headless (a dummy
  driver), rendering does not, so never assert on pixels.
- `LevelBuilder.streaming = false` / `load_all()` when a test needs the
  whole grid; the smoke test keeps streaming on because the game does.
- A scene that must load in CI must have every `ext_resource` path and
  `uid` valid; the review checks for broken ones. Commit the `.uid`
  files next to new scripts.

## Debugging a failing run

The failing `_check` prints `FAIL <label>`; the smoke test also prints
the tick. Reproduce with the same command, add a temporary `print` of the
state at that tick, and remember the first failure is usually upstream
(an enemy that did not spawn because its profile name changed). For
import errors run `--import --verbose`. Godot prints script errors to
stderr with `res://` paths and line numbers; CI logs keep them.

## Lint

`gdlint` with `.gdlintrc` (file lines 3000, public methods 30; the rest
default) and `gdformat`. Run `gdformat` (without `--check`) to fix
formatting; fix lint findings by hand (unused args get a `_` prefix,
long functions are split). See `gdscript-style`.

## Pull request review

`REVIEW.md` tells the Claude reviewer to focus on gameplay logic, physics
layers and masks, Area2D use, `_process` vs `_physics_process`, signal
and node path correctness, broken `ext_resource`/`uid`, crashes, and CI;
findings are *Blocking* or *Nit* (at most three nits), and `assets/**`,
`*.import`, `*.uid`, docs screenshots and `art/**` are skipped. Write the
PR so those are easy to verify: say which test covers the change and the
new `EXPECTED_CHECKS`. A PR squash-merges automatically once lint, the
smoke test and the review are clean.
