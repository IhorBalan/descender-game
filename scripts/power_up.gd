class_name PowerUp
extends Area2D

## A timed power-up lying in a room: a Halo (the next hit is turned aside),
## a Lit Blade (the sword hits harder) or Zeal (faster dashes and feet).
## Touching it gives the hero the power for Player.POWER_TIME seconds; it is
## lost on death, and the level builder puts the pickup back with the
## enemies when the hero respawns. The node's origin is at the base. See
## docs/design/upgrades.md.

signal collected(power: Player.Power)

const TEXTURES := {
	Player.Power.HALO: preload("res://assets/sprites/power_halo.png"),
	Player.Power.BLADE: preload("res://assets/sprites/power_blade.png"),
	Player.Power.ZEAL: preload("res://assets/sprites/power_zeal.png"),
}
## The HUD line shown when the power-up is taken.
const MESSAGES := {
	Player.Power.HALO: "Halo: the next hit is turned aside",
	Player.Power.BLADE: "Lit Blade: the sword hits harder for a while",
	Player.Power.ZEAL: "Zeal: faster dashes and feet for a while",
}
const COLORS := {
	Player.Power.HALO: Color(1.0, 0.85, 0.4),
	Player.Power.BLADE: Color(1.0, 0.7, 0.3),
	Player.Power.ZEAL: Color(0.6, 0.9, 1.0),
}
const BOB_HEIGHT := 2.0
const BOB_SPEED := 4.0

var power: Player.Power = Player.Power.HALO

var _time := randf() * TAU
var _sprite: Sprite2D


func _ready() -> void:
	collision_layer = 0
	collision_mask = 2
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(20, 28)
	shape.shape = rect
	shape.position = Vector2(0, -14)
	add_child(shape)
	_sprite = Sprite2D.new()
	_sprite.texture = TEXTURES[power]
	_sprite.scale = Vector2(2, 2)
	_sprite.position = Vector2(0, -16)
	add_child(_sprite)
	body_entered.connect(_on_body_entered)


func _process(delta: float) -> void:
	_time += delta
	_sprite.position.y = -16.0 + sin(_time * BOB_SPEED) * BOB_HEIGHT


func _on_body_entered(body: Node2D) -> void:
	if not body is Player or body.state == Player.State.DEAD:
		return
	set_deferred("monitoring", false)
	body.grant_power(power)
	Audio.play("pickup")
	Feedback.spark(global_position + Vector2(0.0, -16.0), COLORS[power])
	collected.emit(power)
	queue_free()
