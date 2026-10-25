class_name AbilityPickup
extends Area2D

## A feather shed by the angels' system. Touching it unlocks one of the
## hero's half-finished angel powers for the rest of the run: pickups are
## not rebuilt on respawn and the ability stays unlocked. The node's origin
## is at the base, like the hero's, so a level cell places it on the floor.

signal collected(ability: Player.Ability)

const BOB_HEIGHT := 2.0
const BOB_SPEED := 3.0

@export var ability: Player.Ability = Player.Ability.WINGS

var _time := randf() * TAU
var _sprite_rest_y := 0.0

@onready var sprite: Sprite2D = $Sprite2D


func _ready() -> void:
	_sprite_rest_y = sprite.position.y
	# It bobs every frame in _process, so physics interpolation stays off for it.
	sprite.physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	body_entered.connect(_on_body_entered)


func _process(delta: float) -> void:
	_time += delta
	sprite.position.y = _sprite_rest_y + sin(_time * BOB_SPEED) * BOB_HEIGHT


func _on_body_entered(body: Node2D) -> void:
	if not body is Player or body.state == Player.State.DEAD:
		return
	# Stop listening right away so a second overlap in the same step cannot
	# collect the feather twice before queue_free runs.
	set_deferred("monitoring", false)
	body.unlock(ability)
	Audio.play("ability")
	Feedback.spark(global_position + Vector2(0.0, -8.0), Color(1.0, 0.95, 0.6))
	collected.emit(ability)
	queue_free()
