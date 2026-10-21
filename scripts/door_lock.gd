class_name DoorLock
extends StaticBody2D

## A seal of angel light across a door of the world. The map tool marks a
## door as locked by an ability (the hero needs it to pass) or by a boss
## (the region's boss must be dead); a boss fight also seals the doors of
## its room while it runs. The seal is solid for everyone until `satisfied`
## says the hero may pass, and then `open` sparks it away. Cells are added
## by the level builder; the node's origin is the level's.

signal touched(lock: DoorLock)

enum Kind { ABILITY, BOSS, ARENA }

const TEXTURE := preload("res://assets/sprites/light_barrier.png")
const TINT_ABILITY := Color(1.0, 0.95, 0.75)
const TINT_BOSS := Color(1.0, 0.7, 0.6)
const TINT_ARENA := Color(1.0, 0.55, 0.45)
const TILE := 16

var kind: Kind = Kind.ABILITY
## The door the seal sits on (its id in the export), for the tests and logs.
var door_id := 0
## For an ability lock: the hero's ability, or -1 for one he cannot learn
## yet, and the map's name for it.
var ability := -1
var ability_name := ""
## For a boss lock: the story flag the boss sets when it dies, and its name.
var boss_flag := ""
var boss_name := ""
## The cells the seal covers, in tiles.
var cells: Array[Vector2i] = []

var _area: Area2D
var _time := randf() * TAU


func _ready() -> void:
	collision_layer = 1
	collision_mask = 0
	_area = Area2D.new()
	_area.collision_layer = 0
	_area.collision_mask = 2
	_area.monitorable = false
	add_child(_area)
	_area.body_entered.connect(_on_body_entered)
	var tint := TINT_ABILITY
	match kind:
		Kind.BOSS:
			tint = TINT_BOSS
		Kind.ARENA:
			tint = TINT_ARENA
	for cell in cells:
		var centre := Vector2(cell.x * TILE + TILE / 2.0, cell.y * TILE + TILE / 2.0)
		var shape := CollisionShape2D.new()
		var rect := RectangleShape2D.new()
		rect.size = Vector2(TILE, TILE)
		shape.shape = rect
		shape.position = centre
		add_child(shape)
		var touch := CollisionShape2D.new()
		var touch_rect := RectangleShape2D.new()
		touch_rect.size = Vector2(TILE + 8.0, TILE + 8.0)
		touch.shape = touch_rect
		touch.position = centre
		_area.add_child(touch)
		var sprite := Sprite2D.new()
		sprite.texture = TEXTURE
		sprite.position = centre
		sprite.modulate = tint
		add_child(sprite)


func _process(delta: float) -> void:
	_time += delta
	modulate.a = lerpf(0.75, 1.0, (sin(_time * 3.0) + 1.0) / 2.0)


## Whether the hero may pass: he has the ability, or the boss is dead. An
## arena seal only opens when the fight ends.
func satisfied(player: Player) -> bool:
	match kind:
		Kind.ABILITY:
			return ability >= 0 and player != null and player.has_ability(ability as Player.Ability)
		Kind.BOSS:
			return Story.has_flag(boss_flag)
	return false


## What the seal tells the hero when he touches it.
func message() -> String:
	match kind:
		Kind.ABILITY:
			return "A seal of light. It wants the %s." % ability_name
		Kind.BOSS:
			return "The seal holds while the %s stands." % boss_name
	return "The room is sealed until the fight is over."


## Breaks the seal: sparks on every cell, then it is gone. `quiet` skips
## the sparks and the sound, for seals a loaded save has already passed.
func open(quiet := false) -> void:
	if not quiet:
		for cell in cells:
			Feedback.spark(
				Vector2(cell.x * TILE + TILE / 2.0, cell.y * TILE + TILE / 2.0),
				Color(1.0, 0.95, 0.7)
			)
		Audio.play("pickup", -4.0)
	set_deferred("collision_layer", 0)
	_area.set_deferred("monitoring", false)
	queue_free()


## The bounds of the seal in level pixels.
func rect() -> Rect2:
	var result := Rect2()
	for i in cells.size():
		var cell := Rect2(Vector2(cells[i]) * TILE, Vector2(TILE, TILE))
		result = cell if i == 0 else result.merge(cell)
	return result


func _on_body_entered(body: Node2D) -> void:
	if body is Player:
		touched.emit(self)
