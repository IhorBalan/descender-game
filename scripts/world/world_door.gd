@tool
class_name WorldDoor
extends Node2D

## A door of the world: the opening between two rooms, at the cell its
## position falls in. A door sideways ("h") is an opening in a room's side
## wall; a door down ("v") is a gap in a ledge. A lock seals it with a
## DoorLock until the hero has the ability, or has killed the boss of the
## region, it names. A secret door is a passage through the rock, hidden
## until a Smite breaks it (see SecretWall).

const DOOR_COLOR := Color(0.4, 0.9, 1.0, 0.8)
const LOCK_COLOR := Color(1.0, 0.8, 0.2, 0.9)
const SECRET_COLOR := Color(0.8, 0.5, 1.0, 0.9)

@export var id := 0
## The ids of the rooms on either side (see WorldRoom.id).
@export var room_a := 0
@export var room_b := 0
## "h" for a door sideways, "v" for a door down.
@export var dir := "h"
## "" for an open door, "ability" or "boss" for a sealed one.
@export var lock_kind := ""
## For an ability lock: the index into the world's abilities.
@export var lock_ability := 0
## For a boss lock: the index of the region whose boss must fall.
@export var lock_region := 0
@export var secret := false


func _ready() -> void:
	if WorldRegion.overlays_shown():
		queue_redraw()


func _draw() -> void:
	if not WorldRegion.overlays_shown():
		return
	var color := DOOR_COLOR
	if secret:
		color = SECRET_COLOR
	elif not lock_kind.is_empty():
		color = LOCK_COLOR
	draw_circle(Vector2.ZERO, 5.0, color)
	if not lock_kind.is_empty() or secret:
		var text := "secret" if secret else lock_kind
		draw_string(ThemeDB.fallback_font, Vector2(7.0, 4.0), text, 0, -1, 10, color)


## The lock as the map tool exports it, or null for an open door.
func lock() -> Variant:
	match lock_kind:
		"ability":
			return {"kind": "ability", "ability": lock_ability}
		"boss":
			return {"kind": "boss", "region": lock_region}
	return null
