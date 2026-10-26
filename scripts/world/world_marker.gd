@tool
class_name WorldMarker
extends Node2D

## A point of interest of the world, standing with its feet at its
## position: start (where a new game puts the hero), save (a checkpoint),
## boss (a boss with its profile by label, see Boss.id_for), ability (a
## feather, by the ability's name in its label), secret, shop, gate, npc,
## clock (data until something uses them).

const MARKER_COLOR := Color(1.0, 0.9, 0.3, 0.9)

@export var id := 0
@export var type := "save"
@export var label := ""
## The id of the room the marker stands in, or -1.
@export var room := -1


func _ready() -> void:
	if WorldRegion.overlays_shown():
		queue_redraw()


func _draw() -> void:
	if not WorldRegion.overlays_shown():
		return
	draw_rect(Rect2(Vector2(-5.0, -10.0), Vector2(10.0, 10.0)), MARKER_COLOR)
	var text := type
	if not label.is_empty():
		text += ": " + label
	draw_string(ThemeDB.fallback_font, Vector2(8.0, -2.0), text, 0, -1, 12, MARKER_COLOR)
