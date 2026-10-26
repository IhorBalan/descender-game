@tool
class_name WorldRoom
extends Node2D

## A room of the world: a rectangle on the room grid (30 x 17 tiles a cell)
## that the map page draws once the hero has stood in it, that a boss
## fight seals, and that the doors join. Its position is its top-left
## corner and `size` its extent, in level pixels from the region's corner.

const ROOM_COLOR := Color(1.0, 1.0, 1.0, 0.35)
const BOSS_COLOR := Color(1.0, 0.5, 0.3, 0.6)
const LABEL_COLOR := Color(1.0, 1.0, 1.0, 0.9)

## The room's id in the world: what doors, markers and the map page refer to.
@export var id := 0
## "room", "boss", "save", "secret"... as the map tool kinds them.
@export var kind := "room"
## The area of the region the room belongs to (a region may have several).
@export var area := 0
## How deep the room is along the descent, for the map page.
@export var depth := 0
## A name for the room, or "" for one the map tool left unnamed.
@export var room_name := ""
## The ability the room holds, for an ability room, else "".
@export var ability := ""
@export var size := Vector2(480.0, 272.0):
	set(value):
		size = value
		queue_redraw()


func _ready() -> void:
	if WorldRegion.overlays_shown():
		queue_redraw()


func _draw() -> void:
	if not WorldRegion.overlays_shown():
		return
	draw_rect(Rect2(Vector2.ZERO, size), BOSS_COLOR if kind == "boss" else ROOM_COLOR, false, 2.0)
	var label := "room %d" % id
	if not room_name.is_empty():
		label += ": " + room_name
	draw_string(ThemeDB.fallback_font, Vector2(6.0, 18.0), label, 0, -1, 14, LABEL_COLOR)


## The room's rect in level pixels from the region's corner.
func rect() -> Rect2:
	return Rect2(position, size)
