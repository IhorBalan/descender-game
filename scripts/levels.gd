extends Node

## The levels in the order the hero descends through them.
##
## Registered as the Levels autoload. A level is known by its id, which is
## what saves store; the path is where its world export lives (see
## LevelBuilder), ambient names the background sound it starts with and sky
## the three colours of its background gradient (top, middle, bottom). Add
## a level by appending a row here and an export under levels/.

## Behind the world, whose rooms draw their own backgrounds.
const WORLD_SKY: Array[Color] = [
	Color(0.05, 0.05, 0.09), Color(0.08, 0.08, 0.12), Color(0.1, 0.1, 0.15)
]
const LEVELS: Array[Dictionary] = [
	{
		"id": "world_1",
		"name": "World 1: The Descent",
		"path": "res://levels/world/world.json",
		"ambient": "heaven",
		"sky": WORLD_SKY,
	},
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


## The ambient loop for the level (see Audio.play_ambient).
func ambient(id: String) -> String:
	var index := _index(id)
	return LEVELS[index].get("ambient", "") if index >= 0 else ""


## The background gradient, top to bottom.
func sky(id: String) -> Array[Color]:
	var index := _index(id)
	return LEVELS[index].get("sky", WORLD_SKY) if index >= 0 else WORLD_SKY


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
## unknown, such as the ids of the ASCII levels the world replaced, falls
## back to the first level.
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
