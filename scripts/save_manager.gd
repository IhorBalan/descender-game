extends Node

## Save slots on disk, and the save the level applies when it boots.
##
## Registered as the SaveManager autoload. Each slot is a JSON file under
## user://saves (see capture in main.gd for what goes in it). The title
## screen picks a slot and either clears it (new game) or reads it into
## `pending`; main.gd applies `pending` once the level is built and writes
## back to `current_slot` whenever the game saves.

const SLOT_COUNT := 3
const SAVE_DIR := "user://saves"
const VERSION := 1

## The slot the running game saves to, or -1 when it should not save at all
## (the level scene was started directly, as the smoke test does).
var current_slot := -1
## The save to apply when the level scene boots; empty for a fresh game.
var pending: Dictionary = {}


func slot_path(slot: int) -> String:
	return "%s/slot_%d.json" % [SAVE_DIR, slot + 1]


func has_save(slot: int) -> bool:
	return FileAccess.file_exists(slot_path(slot))


## The slot's save, or an empty dictionary when it is missing or unreadable.
func read(slot: int) -> Dictionary:
	if not has_save(slot):
		return {}
	var text := FileAccess.get_file_as_string(slot_path(slot))
	var data: Variant = JSON.parse_string(text)
	if data is Dictionary and int(data.get("version", 0)) == VERSION:
		return data
	push_warning("Save slot %d is unreadable, treating it as empty" % (slot + 1))
	return {}


func write(slot: int, data: Dictionary) -> bool:
	DirAccess.make_dir_recursive_absolute(SAVE_DIR)
	var file := FileAccess.open(slot_path(slot), FileAccess.WRITE)
	if file == null:
		var reason := error_string(FileAccess.get_open_error())
		push_error("Could not write save slot %d: %s" % [slot + 1, reason])
		return false
	file.store_string(JSON.stringify(data, "\t"))
	file.close()
	return true


func delete(slot: int) -> void:
	if has_save(slot):
		DirAccess.remove_absolute(slot_path(slot))


## The slot saved most recently, or -1 when every slot is empty.
func latest_slot() -> int:
	var latest := -1
	var latest_time := -1.0
	for slot in SLOT_COUNT:
		var data := read(slot)
		if data.is_empty():
			continue
		var saved_at := float(data.get("saved_at", 0.0))
		if saved_at > latest_time:
			latest = slot
			latest_time = saved_at
	return latest


## Clears the slot and makes it the one the new game saves to.
func start_new_game(slot: int) -> void:
	delete(slot)
	current_slot = slot
	pending = {}


## Reads the slot for the level to apply. Returns false when it is empty.
func load_slot(slot: int) -> bool:
	var data := read(slot)
	if data.is_empty():
		return false
	current_slot = slot
	pending = data
	return true


## One line for the slot picker: where the save is and how long it took.
func summary(slot: int) -> String:
	var data := read(slot)
	if data.is_empty():
		return "Empty"
	var checkpoint := int(data.get("checkpoint", -1))
	var place := "Start" if checkpoint < 0 else "Checkpoint %d" % (checkpoint + 1)
	var abilities: Array = data.get("abilities", [])
	var seconds := int(data.get("play_time", 0.0))
	var played := "%d:%02d" % [seconds / 60, seconds % 60]
	if abilities.is_empty():
		return "%s   %s" % [place, played]
	var names: PackedStringArray = []
	for ability in abilities:
		names.append(Player.Ability.keys()[int(ability)].capitalize())
	return "%s   %s   %s" % [place, ", ".join(names), played]
