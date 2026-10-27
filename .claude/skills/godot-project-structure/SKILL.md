---
name: godot-project-structure
description: Where things live in this Godot 4.7 project and how nodes, scenes, autoloads and Resources are meant to depend on each other. Use when adding a file, a scene, an autoload or a new system, or when unsure which node should own some behaviour.
---

# Project structure

Descender is a 2D metroidvania in Godot 4.7 (GDScript only). The game
renders at a 320x180 viewport scaled with `canvas_items` stretch and
integer scaling, with the Mobile renderer (OpenGL3 fallback), so every
world coordinate is a *level pixel* and 16 level pixels are one tile.

## Where files go

| Folder | Holds |
|---|---|
| `scripts/` | Game scripts. Enemies under `scripts/enemies/`, the world editor nodes under `scripts/world/`, menus under `scripts/ui/` |
| `scenes/` | `.tscn` files: `main.tscn`, `title.tscn`, `hud.tscn`, menus, `scenes/enemies/`, `scenes/world/` (one `WorldRegion` scene per region plus `world.tscn`) |
| `resources/` | Data `.tres` files: `resources/enemies/*.tres` (WalkerProfile etc.), `resources/bosses/` |
| `assets/` | Art and sound: `sprites/<name>/<name>.tres` + strips, `sprites.json`, `audio/sfx`, `audio/music`, `shaders/*.gdshader`, `regions/<key>/{parallax.json,fx.json,*.png}`, `ui_theme.tres`, `fonts` |
| `levels/world/` | The TileSet `world_tiles.tres` and the region atlases `tiles/<region>.png` and `<region>_n.png` |
| `dialogue/` | Dialogue JSON (`world_1.json`) |
| `tests/` | Headless test scenes (`unit_test`, `map_pipeline_test`, `smoke_test`) |
| `tools/` | Generators and editor scripts (`*.py`, `*.gd`); excluded from exports |
| `docs/design/` | Design notes in Markdown |

Keep that split: a script that is only data belongs in `resources/` as a
`.tres`, art in `assets/`, and anything a release should not ship in
`tools/` or `tests/` (see `export_presets.cfg` `exclude_filter`).

## Autoloads (in `project.godot`)

`Levels`, `Audio`, `Feedback`, `SaveManager`, `Prompts`, `Settings`,
`LiveMap`, `MapStore`, `ComponentStore`, `DevConsole`. The docs' rule for
when a system deserves an autoload: it tracks its own data, needs global
access, and exists in isolation (audio pool, save slots, settings,
feedback effects). Anything that reaches into other systems' data should be
an ordinary node or a `class_name` with `static func`s instead. Do not add
an autoload for a one-scene concern.

Autoloads are plain `extends Node` scripts with a `##` comment that names
the autoload ("Registered as the SaveManager autoload"). Autoloads that must
keep running while the tree is paused set
`process_mode = Node.PROCESS_MODE_ALWAYS` (Feedback does).

## Scene tree at runtime

`main.tscn` (`Main`) is the entry point: it builds the level through
`LevelBuilder`, owns the player, camera (`GameCamera`), `Backdrop`,
`RegionFx`, `PostFx` and the HUD and menus on `CanvasLayer`s. Menus and
overlays are `CanvasLayer`s with their own `layer` and `process_mode`, so
they draw above the world and survive pausing. The title screen is a
separate main scene swapped with `get_tree().change_scene_to_file`.

## Scenes versus scripts

- A thing that is particular to this game with several nodes (an enemy, a
  menu, a pickup) is a **scene** with a script on its root.
- A reusable helper with no node tree (`AnimLoader`, `TerrainRules`,
  `TileIndex`, `Story`) is a `class_name` script with `static func`s on
  `RefCounted`.
- Tunable data shared by several instances is a **Resource** subclass with
  `@export`s (`WalkerProfile` in `resources/enemies/`), not a dictionary in
  a script.
- Enemies inherit a base scene: `scenes/enemies/walker.tscn` holds the
  body, sprite, hitboxes and rays; `imp.tscn` inherits it and only sets
  `profile` and the sprite's `sprite_frames`. Follow that when adding a body.

## Dependencies between nodes

From the docs' scene organization guide, and how the repo already works:

- A scene should work on its own. A parent injects what a child needs:
  set a property (`enemy.profile = ...`), pass a node reference, or
  connect to the child's signal. Children never `get_parent()` into the
  level and never hard-code `/root/...` paths other than the autoloads.
- Signals report that something happened and are named in the past tense
  (`health_changed`, `died`, `fast_travel_requested`). The parent decides
  what to do about it. Call a method to *start* behaviour.
- Siblings talk through their common ancestor or through a group. The
  camera is found with the `game_camera` group (`Feedback.shake` uses it),
  not by path.
- Cache node references with `@onready var x: Type = $Path`, typed. Prefer
  `@export var node: Type` when the editor should pick it.
- `@tool` scripts that need editor configuration implement
  `_get_configuration_warnings()` rather than a note in a doc.
- Call `queue_free()` to remove nodes, and check `is_instance_valid()`
  before using a node someone else may have freed. Removing a node with
  `remove_child` and keeping the reference is the cheap way to park it.

## Adding something new: checklist

1. Pick the owner from the table above and the nearest existing example.
2. Scene for game-specific node trees, `class_name` script for helpers,
   Resource for data.
3. Doc comment (`##`) at the top of the script explaining what it is and
   which file configures it, like every script here.
4. If it is data the tests check (sprites, sounds, dialogue, enemies), add
   it to the registry file the test reads (see `testing-headless`).
5. Register new scene or resource paths through `preload` constants
   (`const MOTE_SCENE := preload("res://scenes/heal_mote.tscn")`) so a
   broken path fails at load, not mid-game.
