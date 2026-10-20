extends Node

## The levels in the order the hero descends through them.
##
## Registered as the Levels autoload. A level is known by its id, which is
## what saves store; the path is where its ASCII map lives, ambient names
## its background sound, tiles its tile sheet and sky the three colours of
## its background gradient (top, middle, bottom). Add a level by appending a
## row here and a map under levels/.

const HEAVEN_SKY: Array[Color] = [
	Color(0.33, 0.58, 0.94), Color(0.62, 0.8, 0.98), Color(0.92, 0.95, 1.0)
]
const EARTH_SKY: Array[Color] = [
	Color(0.16, 0.1, 0.18), Color(0.42, 0.22, 0.2), Color(0.72, 0.5, 0.34)
]
const HEAVEN_TILES := "res://assets/sprites/tiles.png"
const EARTH_TILES := "res://assets/sprites/tiles_earth.png"
const LEVELS: Array[Dictionary] = [
	{
		"id": "heaven_1",
		"name": "Heaven 1: The Outskirts",
		"path": "res://levels/level_01.txt",
		"ambient": "heaven",
		"sky": HEAVEN_SKY,
		"tiles": HEAVEN_TILES,
	},
	{
		"id": "heaven_2",
		"name": "Heaven 2: The Inner City",
		"path": "res://levels/level_02.txt",
		"ambient": "heaven",
		"sky": HEAVEN_SKY,
		"tiles": HEAVEN_TILES,
	},
	{
		"id": "earth_hub",
		"name": "Earth: Hollowmere",
		"path": "res://levels/earth_hub.txt",
		"ambient": "earth",
		"sky": EARTH_SKY,
		"tiles": EARTH_TILES,
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
	return LEVELS[index].get("sky", HEAVEN_SKY) if index >= 0 else HEAVEN_SKY


## The tile sheet (fill, top, spikes), 16x16 each.
func tiles(id: String) -> String:
	var index := _index(id)
	return LEVELS[index].get("tiles", HEAVEN_TILES) if index >= 0 else HEAVEN_TILES


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
