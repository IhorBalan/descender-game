extends Node

## The levels in the order the hero descends through them, plus any
## registered at run time.
##
## Registered as the Levels autoload. A level is known by its id, which is
## what saves store; the path is where its world export lives (see
## LevelBuilder), ambient names the background sound it starts with and sky
## the three colours of its background gradient (top, middle, bottom). Add
## a level to the descent by appending a row to LEVELS and an export under
## levels/. Other levels come and go with register(): maps downloaded from
## the editor (see MapStore) and any world.json named on the command line
## (`--level=res://levels/world3/world.json`, see Main). They are not part
## of the descent: nothing follows them.

## Behind the world, whose rooms draw their own backgrounds.
const WORLD_SKY: Array[Color] = [
	Color(0.05, 0.05, 0.09), Color(0.08, 0.08, 0.12), Color(0.1, 0.1, 0.15)
]
const LEVELS: Array[Dictionary] = [
	{
		"id": "world_1",
		"name": "World 1: The Descent",
		"path": "res://scenes/world/world.tscn",
		# The export the world was, which older saves name.
		"was": "res://levels/world/world.json",
		"ambient": "heaven",
		"sky": WORLD_SKY,
	},
]

## The id of the level a new game starts in.
var first_id: String:
	get:
		return LEVELS[0]["id"]

## Levels registered at run time.
var _extra: Array[Dictionary] = []


## Adds (or replaces) a level outside the descent: id, name, path, and
## optionally ambient and sky.
func register(level: Dictionary) -> void:
	var id := str(level.get("id", ""))
	if id.is_empty() or str(level.get("path", "")).is_empty():
		return
	unregister(id)
	var entry := level.duplicate()
	entry["name"] = str(level.get("name", id))
	entry["ambient"] = str(level.get("ambient", "heaven"))
	if not entry.get("sky") is Array:
		entry["sky"] = WORLD_SKY
	_extra.append(entry)


func unregister(id: String) -> void:
	for i in _extra.size():
		if _extra[i]["id"] == id:
			_extra.remove_at(i)
			return


## Registers a world export by its path (the id is the path) and returns the
## id, or an empty string when there is no such file.
func register_path(path: String) -> String:
	if not path.ends_with(".json") or not FileAccess.file_exists(path):
		return ""
	register({"id": path, "name": path.get_base_dir().get_file().capitalize(), "path": path})
	return path


## The levels registered at run time, in registration order.
func extra() -> Array[Dictionary]:
	return _extra.duplicate()


func has(id: String) -> bool:
	return not _find(id).is_empty()


func display_name(id: String) -> String:
	var level := _find(id)
	return str(level["name"]) if not level.is_empty() else id


## The ambient loop for the level (see Audio.play_ambient).
func ambient(id: String) -> String:
	var level := _find(id)
	return str(level.get("ambient", "")) if not level.is_empty() else ""


## The background gradient, top to bottom.
func sky(id: String) -> Array[Color]:
	var level := _find(id)
	if level.is_empty():
		return WORLD_SKY
	var colors: Array[Color] = []
	for c in level.get("sky", WORLD_SKY):
		colors.append(c)
	return colors if colors.size() >= 3 else WORLD_SKY


func path(id: String) -> String:
	var level := _find(id)
	return str(level["path"]) if not level.is_empty() else ""


## The level after this one in the descent, or an empty string after the
## last (and after any level outside the descent).
func next_id(id: String) -> String:
	var index := _index(id)
	if index < 0 or index + 1 >= LEVELS.size():
		return ""
	return LEVELS[index + 1]["id"]


## Turns whatever a save or the command line calls a level into a known id.
## Accepts ids and map paths (older saves stored the path, some of them the
## export the world was made from; a path to an existing export is
## registered on the spot); anything unknown, such as
## the ids of the ASCII levels the world replaced or a downloaded map that
## is gone, falls back to the first level.
func resolve(value: String) -> String:
	for level in LEVELS + _extra:
		if level["id"] == value or level["path"] == value or level.get("was", "") == value:
			return level["id"]
	var registered := register_path(value)
	return registered if not registered.is_empty() else first_id


## The level with this id, or an empty dictionary.
func _find(id: String) -> Dictionary:
	for level in LEVELS:
		if level["id"] == id:
			return level
	for level in _extra:
		if level["id"] == id:
			return level
	return {}


func _index(id: String) -> int:
	for i in LEVELS.size():
		if LEVELS[i]["id"] == id:
			return i
	return -1
