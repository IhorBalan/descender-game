class_name Story
extends RefCounted

## Story flags: what the hero has seen, who he has talked to and which relics
## he has picked up. Flags are plain strings, set once and never cleared
## within a run; dialogue entries check them (see DialogueData) and main.gd
## writes them into the save. Held in static variables rather than an
## autoload so scripts the headless test compiles before the autoloads are
## registered can still reach them.

static var flags: Dictionary = {}


## A new game starts with nothing seen.
static func reset() -> void:
	flags = {}


static func set_flag(flag: String) -> void:
	if not flag.is_empty():
		flags[flag] = true


static func has_flag(flag: String) -> bool:
	return flags.has(flag)


static func has_all(names: Array) -> bool:
	for flag in names:
		if not has_flag(str(flag)):
			return false
	return true


static func has_any(names: Array) -> bool:
	for flag in names:
		if has_flag(str(flag)):
			return true
	return false


## The flags as a sorted list, for the save file.
static func to_list() -> Array[String]:
	var result: Array[String] = []
	for flag in flags:
		result.append(str(flag))
	result.sort()
	return result


## Replaces the flags with the ones from a save.
static func from_list(values: Array) -> void:
	flags = {}
	for value in values:
		var flag := str(value)
		if not flag.is_empty():
			flags[flag] = true
