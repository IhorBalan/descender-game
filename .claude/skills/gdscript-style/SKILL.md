---
name: gdscript-style
description: How GDScript is written in this repo so it passes gdlint and gdformat (gdtoolkit 4.5.0) and matches the existing code: static typing, doc comments, constants, naming, warnings. Use whenever writing or editing a .gd file.
---

# GDScript style

CI runs `gdlint scripts tests tools` and `gdformat --check scripts tests
tools` with gdtoolkit 4.5.0. Run them locally before pushing:

```
pip install "gdtoolkit==4.5.0"
gdlint scripts tests tools
gdformat scripts tests tools   # rewrites; --check only reports
```

`.gdlintrc` raises `max-file-lines` to 3000 and `max-public-methods` to 30
on purpose (the player and the smoke test are long); do not raise them
further, split the file instead.

## Layout of a script (the docs' order, which gdformat expects)

```gdscript
@tool                      # only when the editor runs it
class_name Walker
extends CharacterBody2D

## One paragraph on what this is, which files configure it and which
## scene uses it. Every script here starts like this.

signal died                # past tense; typed params: health_changed(current: int, max_value: int)

enum State { PATROL, CHASE }

const GRAVITY := 700.0     # tuning constants at the top, grouped with a
const MAX_FALL_SPEED := 260.0   # `# Heading` comment and `##` docs

@export var damage := 1    # exported, then public vars, then private

var state := State.PATROL
var _timer := 0.0          # private members start with _

@onready var sprite: AnimatedSprite2D = $Sprite


func _ready() -> void:     # built-in callbacks first, then public, then private
	pass


func take_damage(amount: int) -> void:
	pass


func _on_body_entered(body: Node2D) -> void:
	pass
```

Tabs for indentation, two blank lines between functions, one between
members groups, lines under 100 columns (gdformat wraps). `snake_case` for
functions, variables, signals and file names; `PascalCase` for classes and
enums; `CONSTANT_CASE` for constants and enum values.

## Static typing everywhere

- Declare types on every variable, parameter and return value:
  `func slot_path(slot: int) -> String:`, `-> void` on callbacks.
- Infer with `:=` when the right side makes the type obvious
  (`var path := "%s/%s" % [a, b]`, `const RUN_SPEED := 100.0`). Write the
  type when it is not obvious or you want a wider one
  (`var pending: Dictionary = {}`, `var _queue: Array[Array] = []`).
- Typed arrays and dictionaries: `Array[String]`, `Array[WorldLight]`,
  `Dictionary[String, int]`.
- Cast loads: `load(path) as SpriteFrames`. Check `ResourceLoader.exists`
  first when a missing file should be survivable, and `push_error` it.
- `@onready var sprite: AnimatedSprite2D = $Sprite`, never untyped `$Sprite`.
- Use `is` checks on duck-typed bodies: `if body is Player:`.
- `StringName` literals (`&"SFX"`) for bus and group names; `String` for
  data keys.

## Doc comments

`##` comments document the class and its public members and show in the
editor. Write them for every script, signal, exported property, public
function and tuning constant whose purpose is not obvious. Keep plain `#`
for implementation notes inside functions. A comment says *why* ("Only poll
while something is inside, so a level full of spikes costs nothing per
tick"), not what the line already says.

## Exports

`@export var type := "enemy"` with a `##` line above. Group related ones
with `@export_group("Swing")`, bound numbers with
`@export_range(0.0, 1.0, 0.05)`, pick files with
`@export_file("*.json")`, enums with `@export var kind: Attack`. Resources
with exports are the project's data format (`WalkerProfile`); keep them
plain data, no node references.

## Constants and magic numbers

Every tuning number is a named `const` at the top of the script, derived
where possible (`JUMP_VELOCITY := -2.0 * JUMP_HEIGHT / JUMP_TIME_TO_APEX`).
Scenes and scripts are preloaded into constants:
`const MOTE_SCENE := preload("res://scenes/heal_mote.tscn")`.

## Warnings are errors in spirit

Keep the script editor free of warnings. Prefix unused parameters with `_`
(`func _on_body_exited(_body: Node2D)`), avoid shadowing built-ins, do not
compare floats with `==`, return a value on every path, and do not leave
`@warning_ignore` without a comment saying why.

## Patterns the repo uses

- `match state:` for state machines, with a `_process_<state>` function
  per state when they grow.
- `_check(ok, label)` style assertions in tests, never `assert()` in game
  code (release builds strip it).
- Signals connected in `_ready` with `body_entered.connect(_on_body_entered)`;
  callbacks named `_on_<signal>` or `_on_<node>_<signal>`.
- `set_physics_process(false)` to park a node instead of early returns
  every tick.
- Format strings with `%`: `"%s/%s/%s.tres" % [dir, name, name]`.
- Doc-style `## Heading` comments group the top of long scripts (`# Chasing`
  then the constants that belong to it).
