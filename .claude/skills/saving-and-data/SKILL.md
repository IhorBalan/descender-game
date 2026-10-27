---
name: saving-and-data
description: Persistent and static data: SaveManager JSON slots under user://saves, Settings in a ConfigFile, data paths, JSON files the game reads (sprites.json, parallax.json, dialogue), Resource subclasses with @export, preload/load and ResourceLoader, and versioning saves. Use when reading or writing files, adding a saved field, a setting, or a data-driven resource.
---

# Saving and data

## Paths

- `res://` is the project (read-only in exports, so never write there).
- `user://` is per-user writable storage
  (`~/.local/share/godot/app_userdata/<name>/` on Linux,
  `%APPDATA%\Godot\app_userdata\<name>\` on Windows). Saves go to
  `user://saves/slot_N.json` (`SaveManager.SAVE_DIR`), settings to
  `user://settings.cfg` (`Settings.PATH`), tests scratch under
  `user://<test>_...` and clean up after.
- `ProjectSettings.globalize_path("user://x")` for an OS path;
  `DirAccess.make_dir_recursive_absolute` before the first write.
- `OS.has_feature("editor")` separates editor-only paths (live map, sheet
  loading) from exported ones.

## Save slots (`SaveManager`)

- `SLOT_COUNT = 3`, `VERSION = 1`, `Difficulty` enum with per-difficulty
  tables. `read(slot)` parses JSON into a `Dictionary` (empty when missing
  or malformed), `write(slot, data)` stores `JSON.stringify(data, "\t")`
  and reports the open error with `error_string`.
- The title screen sets `current_slot` and `pending`; `main.gd` applies
  `pending` after the level is built (`_apply_save`) and `capture_save()`
  gathers the state to write (`save_game()` on checkpoints).
  `current_slot = -1` means never write (the smoke test starts the level
  scene directly).
- Save data is a plain `Dictionary` of built-in types (ints, strings,
  arrays, nested dictionaries), never nodes or Resources; JSON turns all
  numbers into floats, so cast on read (`int(data.get("hearts", 0))`).
- Adding a field: write it in `capture_save`, read it with a default in
  `_apply_save`, and extend the unit test that writes and reads back the
  last slot. Bump `VERSION` only when old saves cannot be read with
  defaults; then branch on the stored version when applying.
- Upgrades and similar collectibles are identified by stable ids (scene
  order per region for `WorldPlacement`), so reordering placements breaks
  saves. Prefer explicit ids for anything new that is saved.

## Settings (`Settings`, ConfigFile)

```gdscript
var config := ConfigFile.new()
if config.load(PATH) == OK:
    fullscreen = bool(config.get_value("display", "fullscreen", true))
config.set_value("audio", "music", music_volume)
config.save(PATH)
```

Sections: `display`, `audio`, `game`, plus key and pad bindings per
action. Every value is read with a default and clamped, then `apply()`
pushes it to the window, buses and `InputMap`. A new setting is: a var with
default, `load_settings`, `save_settings`, `apply`, a `reset_*`, and a
control in `SettingsMenu`.

## Static data files

The game reads JSON for anything an editor or script generates:
`assets/sprites/sprites.json`, `assets/regions/<key>/parallax.json` and
`fx.json`, `dialogue/world_1.json`, region `edges.json`. Patterns:

- `FileAccess.get_file_as_string(path)` then `JSON.parse_string` (returns
  `null` on error; check `is Dictionary`). For line errors use a `JSON`
  instance: `json.parse(text)`, `json.get_error_line()`.
- The reader documents the schema in its `##` comment (`RegionFx`,
  `Backdrop`); the unit test checks cross-references (every file named
  exists, every id resolves). Add a check when adding a key that names
  another file.
- Keys are strings; use `data.get("key", default)` and `typeof` checks
  rather than assuming.

## Resources (`.tres`)

Typed, inspectable data: `WalkerProfile`, `BossProfile`, `SpriteFrames`,
the TileSet, the theme. A Resource subclass is
`class_name WalkerProfile extends Resource` with `@export` fields and
groups; instances live in `resources/`. Rules from the docs:

- `preload("res://...")` into a `const` for things known at compile time
  (scenes, shaders, scripts); `load()` for paths built at runtime;
  `ResourceLoader.exists(path)` before `load` when missing is survivable.
- Loaded resources are **cached and shared**: changing a property on a
  loaded `.tres` changes it for every user. `duplicate()` (or
  `resource_local_to_scene = true`) when an instance must diverge, as
  `Feedback.flash` gives each sprite its own `ShaderMaterial`.
- Resources can reference other resources and be saved with
  `ResourceSaver.save(res, path)` (tools do this for `SpriteFrames`);
  the game itself never saves `.tres`.
- Do not store node references or runtime state in a Resource; it is
  data, and `@tool` code may load it in the editor.
- `.tres` files are text and diff well; keep `uid://` lines intact (the
  `.uid` sidecar files next to scripts must be committed too).
- Saved game state is JSON, not Resources: `ResourceLoader` would execute
  scripts embedded in a user-supplied file.

## Background loading

Levels stream in chunks synchronously (fast enough at 64x64 tiles). For a
big load (a whole region's art, a new scene) use
`ResourceLoader.load_threaded_request(path)`, poll
`load_threaded_get_status(path, progress)` from `_process`, then
`load_threaded_get`. Keep the loading state in the node that asked for it.
