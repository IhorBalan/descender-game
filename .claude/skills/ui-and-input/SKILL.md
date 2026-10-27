---
name: ui-and-input
description: Menus, HUD and input: Control layout and anchors, the shared ui_theme.tres and its type variations, CanvasLayer menus that pause the tree, focus and gamepad navigation, InputMap actions and rebinding through Settings, prompts with {action} templates. Use when building or changing UI, handling input, pausing, or showing text to the player.
---

# UI and input

## Input actions

Defined in `project.godot` `[input]`: `move_left`, `move_right`,
`move_down`, `jump`, `attack`, `dash`, `cast`, `heal`, `interact`,
`restart`, `pause`, `page_prev`, `page_next`, `dev_console`, plus Godot's
`ui_*` set for menus. Every new control is an **action** with keyboard and
joypad events, never a raw key check. Rebindable actions are listed in
`Settings.REBINDABLE_ACTIONS` with labels in `ACTION_LABELS`;
`Settings.bind_key` / `bind_pad_button` edit the `InputMap` at runtime
(`InputMap.action_erase_events`, `action_add_event`) and save to
`user://settings.cfg`. Adding an action means: `project.godot` entry, the
two lists in `Settings`, and a prompt template if it is shown to the player.

Reading input:

- Gameplay: `Input.is_action_pressed / just_pressed` inside
  `_physics_process` with a buffer (see `character-movement-2d`).
  `Input.get_axis("move_left", "move_right")` for the stick.
- Menus and one-shot actions: `_unhandled_input(event)` with
  `event.is_action_pressed("pause")`, then
  `get_viewport().set_input_as_handled()` so the game beneath does not
  also see it. `_unhandled_input` only runs when no `Control` consumed the
  event, which is what a menu over the game wants.
- Text entry (dev console) uses `_input` and `LineEdit`.
- Tests inject with `Input.action_press(action)` / `action_release`
  (`smoke_test._press`), so logic must read actions, not raw events.

## Prompts

`Prompts` autoload renders templates with `{action}` placeholders
(`"Press {interact} to talk"`) into BBCode for the current device, and
`PromptLabel extends RichTextLabel` shows them, updating when bindings
change. Use it for any text that names a button.

## Menus

Pattern from `PauseMenu` (`scenes/pause_menu.tscn`):

- A `CanvasLayer` with a high `layer` (10) and
  `process_mode = Node.PROCESS_MODE_ALWAYS` (3 in the `.tscn`) so it runs while the
  tree is paused. `open()` sets `visible = true` and
  `get_tree().paused = true`; `close()` reverses it. Sub-pages (settings,
  quest log, inventory, map) are children toggled by `visible`, and the
  parent's `close` hides any page still open.
- Autoloads that must keep running under pause set
  `PROCESS_MODE_ALWAYS` (`Audio` ducks the music while paused, `Feedback`,
  `DevConsole`). Game nodes inherit the default and freeze.
- `@onready var settings_menu: SettingsMenu = $Panel/SettingsMenu` typed
  paths for children; signals (`closed`, `save_requested`,
  `fast_travel_requested(index)`) tell `Main` what the player chose. The
  menu never reaches into the level itself.
- `Audio.wire_menu(root)` connects hover and press sounds to every
  `BaseButton` under a menu root; call it in `_ready`.
- Scene changes (title to game) go through `ScreenFade.fade_out()`,
  `await` the returned signal, then `get_tree().change_scene_to_file`.

## Layout

- Use containers (`VBoxContainer`, `HBoxContainer`, `MarginContainer`,
  `CenterContainer`, `GridContainer`) and anchors
  (`set_anchors_preset(Control.PRESET_FULL_RECT)`) rather than fixed
  positions; the base canvas is 320x180 and the window scales it, so
  everything is in those units and fonts are pixel fonts from
  `assets/fonts`.
- Full-screen scrims and the post grade are `ColorRect`s with
  `mouse_filter = MOUSE_FILTER_IGNORE` so they do not eat clicks.
- `size_flags_horizontal = SIZE_EXPAND_FILL` to stretch inside a box;
  `custom_minimum_size` for bars (`HUD` draws health, boss and XP bars as
  `ColorRect` fills of fixed widths).
- HUD elements built in code (`hud.gd`) keep their numbers as constants
  (`HEALTH_BAR_WIDTH`, colours) at the top.

## Theme

`assets/ui_theme.tres` is applied at the root of each menu scene. Type
variations `Title`, `Heading`, `Subheading`, `Muted` style labels:
`theme_type_variation = &"Heading"` in the `.tscn`, or
`label.theme_type_variation = &"Muted"` in code. Add a new variation to the
theme instead of overriding colours per node; `add_theme_color_override`
is for a one-off (the HUD's boss bar hurt colour).

## Focus and gamepad

Menus must be playable on a pad: give the first button focus on open
(`button.grab_focus()`), keep `focus_mode = FOCUS_ALL` on anything
selectable, and let the containers' automatic neighbours handle
`ui_up/down`. Set `focus_neighbor_*` only when the automatic ones cross
pages. Hidden pages must not hold focus (hide them, then grab focus on
the page shown). `ui_cancel` closes a page; `pause` toggles the menu.

## Text

`Label` for plain text, `RichTextLabel` with `bbcode_enabled` for prompts
and dialogue (`DialogueBox` types at `Settings.chars_per_second()` using
`visible_characters`). Keep strings in the script constants or the
dialogue JSON, not spread through `.tscn` files, so the unit test and a
later localisation can find them.

## Window and display

`Settings` owns fullscreen, window scale (`window_size_for`), vsync and
applies them with `DisplayServer.window_set_mode` / `window_set_size`;
it skips all of that when `DisplayServer.get_name() == "headless"`, which
is how the tests run. Follow that guard for anything that touches the
window.
