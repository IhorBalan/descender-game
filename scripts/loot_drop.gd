class_name LootDrop
extends Area2D

## An item lying in the world: a boss's unique loot, or a consumable an
## enemy left. Drawn as a gem in the item's colour with a glow that
## breathes. Touching it puts the item in the pack; a unique sets its
## story flag (`loot:<item>`) so it is never dropped again, and the level
## builder lays it back down by its boss's placement until it is taken, so
## a death or a load cannot lose it. A consumable drop goes with the heal
## motes when the enemies come back (the "loot" group).

signal collected(item_id: String)

const BOB_HEIGHT := 2.0
const BOB_SPEED := 2.5
const SHADOW := Color(0.0, 0.0, 0.0, 0.35)
const SHINE := Color(1.0, 1.0, 1.0, 0.85)

var item_id := ""

var _time := randf() * TAU
var _done := false


func _ready() -> void:
	add_to_group("unique_loot" if Items.is_unique(item_id) else "loot")
	collision_layer = 0
	collision_mask = 2
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(20, 24)
	shape.shape = rect
	shape.position = Vector2(0, -10)
	add_child(shape)
	body_entered.connect(_on_body_entered)


func _process(delta: float) -> void:
	_time += delta
	queue_redraw()


func _draw() -> void:
	var color := Items.color(item_id)
	var lift := -10.0 + sin(_time * BOB_SPEED) * BOB_HEIGHT
	var big := Items.is_unique(item_id)
	var size := 6.0 if big else 4.0
	var glow := 0.25 + sin(_time * 3.0) * 0.1
	draw_circle(Vector2(0.0, lift), size * 2.2, Color(color, glow))
	var gem := PackedVector2Array(
		[
			Vector2(0.0, lift - size),
			Vector2(size * 0.8, lift),
			Vector2(0.0, lift + size),
			Vector2(-size * 0.8, lift),
		]
	)
	var shadow := PackedVector2Array()
	for point in gem:
		shadow.append(point + Vector2(1.0, 1.0))
	draw_colored_polygon(shadow, SHADOW)
	draw_colored_polygon(gem, color)
	draw_rect(Rect2(-1.0, lift - size * 0.6, 1.0, 2.0), SHINE)


func _on_body_entered(body: Node2D) -> void:
	if _done or not body is Player or body.state == Player.State.DEAD:
		return
	_done = true
	set_deferred("monitoring", false)
	body.collect_loot(item_id)
	Audio.play("heal" if Items.is_unique(item_id) else "pickup")
	Feedback.spark(global_position + Vector2(0.0, -10.0), Items.color(item_id))
	collected.emit(item_id)
	queue_free()
