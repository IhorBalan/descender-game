---
name: export-and-release
description: Shipping builds: the Linux and Windows presets in export_presets.cfg, headless export commands, the release workflow triggered by v* tags, feature tags and editor-vs-export differences, PCK embedding, and what to check in an exported build. Use when changing export settings, cutting a release, or debugging something that works in the editor but not in a build.
---

# Export and release

## Presets (`export_presets.cfg`)

Two presets, `Linux` and `Windows`, both `export_filter = "all_resources"`
with `exclude_filter = "tools/*,tests/*,docs/*,*.py"`, `embed_pck = true`
(a single executable), console wrapper on, x86_64. Tweak presets in the
editor (Project → Export) and commit the file; the `.cfg` is the source of
truth the workflow uses. A new platform is a new preset plus a step in the
release workflow. Keep `tools/` and `tests/` excluded, and never include
`user://`-style files.

## Build locally

```
# once: Editor → Manage Export Templates (or download the .tpz for 4.7.2)
godot --headless --path . --import
godot --headless --path . --export-release "Linux" export/linux/descender.x86_64
godot --headless --path . --export-release "Windows" export/windows/descender.exe
godot --headless --path . --export-debug "Linux" export/linux/descender_debug.x86_64   # keeps asserts and the debugger
```

`export/` is git-ignored. Run the exported binary, not the editor, to test
anything about paths, saves or performance.

## Release workflow (`.github/workflows/release.yml`)

Pushing a tag `v*` (`git tag v0.3.0 && git push origin v0.3.0`) downloads
Godot 4.7.2 and its export templates into
`~/.local/share/godot/export_templates/4.7.2.stable`, imports, exports
both presets, zips them and attaches them to a GitHub release named after
the tag with generated release notes. Ordinary pushes build nothing.
Bumping `GODOT_VERSION` means bumping it in `tests.yml` and
`project.godot`'s `config/features` too.

## Editor versus export

Things that differ, and how the repo handles them:

- `OS.has_feature("editor")` is true only in the editor; `LiveMap`,
  sheet loading and some backdrop paths branch on it. Use it, not
  `OS.is_debug_build()`, for editor-only tooling. `OS.has_feature("debug")`
  / `"release"`, `"linux"`, `"windows"`, `"headless"` are the other useful
  tags; custom tags can be set per preset.
- `assert()` is stripped in release exports; never rely on it for game
  logic.
- `res://` is read-only in an export and files are inside the PCK:
  `DirAccess` listing of `res://` works only for imported resources;
  anything `.json` the game reads must be a recognised import or listed
  so it is packed (`all_resources` plus the exclude filter packs loose
  `.json` here; check that a new data folder is not excluded).
- Only imported resources are packed by default; a texture loaded with
  `Image.load` from a raw path works in the editor and fails in the
  export. Use `load("res://...png")` on imported textures.
- `user://` location differs per platform; nothing hard-codes it.
- Window settings: the export honours `project.godot` display settings;
  `Settings._apply_window` sets the size on start, so a changed base
  resolution needs `window_size_for` updated.

## Checking a build

1. Launch it from a clean `user://` (delete the app_userdata folder) and
   start a new game: title, save slot, first region, a checkpoint save,
   quit, relaunch, continue.
2. Watch the console wrapper for `ERROR` lines about missing resources
   (the first sign of an excluded file) or shader compile failures on the
   OpenGL3 fallback (`--rendering-driver opengl3` to force it).
3. Confirm the window scale and fullscreen toggles, vsync, and the
   volume sliders (bus layout packed).
4. Check size: `embed_pck` builds are around the sum of the assets; a
   jump means something under `tools/` or `docs/` slipped in.

## Version and PCK notes

- `application/config/version` in `project.godot` is what a build
  reports; set it with the tag.
- DLC or patch PCKs are possible (`ProjectSettings.load_resource_pack`)
  but the project ships one embedded PCK; keep it that way unless the
  game grows levels after release.
- Command-line flags work on the export too: `--headless` to run tests
  against a build, `--rendering-driver`, `--resolution`, `--fullscreen`,
  `--verbose`.
