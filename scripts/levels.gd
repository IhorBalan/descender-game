extends Node

## The levels in the order the hero descends through them.
##
## Registered as the Levels autoload. A level is known by its id, which is
## what saves store; the path is where its ASCII map lives. Add a level by
## appending a row here and a map under levels/.

const LEVELS: Array[Dictionary] = [
	{"id": "heaven_1", "name": "Heaven 1: The Outskirts", "path": "res://levels/level_01.txt"},
	{"id": "heaven_2", "name": "Heaven 2: The Inner City", "path": "res://levels/level_02.txt"},
]

## The id of the level a new game starts in.
var first_id: String:
	get:
		return LEVELS[0]["id"]


func has(id: String) -> bool:
	return _index(id) >= 0


func display_name(id: String) -> String:
	var index := _index(id)
	return LEVELS[index]["name"] if index >= 0 else id


func path(id: String) -> String:
	var index := _index(id)
	return LEVELS[index]["path"] if index >= 0 else ""


## The level after this one, or an empty string after the last.
func next_id(id: String) -> String:
	var index := _index(id)
	if index < 0 or index + 1 >= LEVELS.size():
		return ""
	return LEVELS[index + 1]["id"]


## Turns whatever a save or the command line calls a level into a known id.
## Accepts ids and map paths (older saves stored the path); anything
## unknown falls back to the first level.
func resolve(value: String) -> String:
	for level in LEVELS:
		if level["id"] == value or level["path"] == value:
			return level["id"]
	return first_id


func _index(id: String) -> int:
	for i in LEVELS.size():
		if LEVELS[i]["id"] == id:
			return i
	return -1
