class_name LoreItem
extends Interactable

## A relic the hero can examine: pressing interact shows its description,
## after which it is gone for the rest of the save (flag `item:<id>`). The
## level builder skips relics the save already has.

const TEXTURE := preload("res://godot_assets/sprites/relic.png")
const BOB_HEIGHT := 1.5
const BOB_SPEED := 2.5

var item_id := ""

var _time := randf() * TAU
var _sprite: Sprite2D


static func flag_for(id: String) -> String:
	return "item:%s" % id


func _ready() -> void:
	area_size = Vector2(28, 32)
	prompt_offset = Vector2(0, -30)
	super()
	_sprite = Sprite2D.new()
	_sprite.texture = TEXTURE
	_sprite.position = Vector2(0, -8)
	# It bobs every frame in _process, so physics interpolation stays off for it.
	_sprite.physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	add_child(_sprite)
	move_child(_sprite, 0)


func _process(delta: float) -> void:
	_time += delta
	_sprite.position.y = -8.0 + sin(_time * BOB_SPEED) * BOB_HEIGHT


func on_dialogue_finished(_entry: Dictionary) -> void:
	Story.set_flag(flag_for(item_id))
	# Stop listening right away so a second press cannot examine it again
	# before queue_free runs.
	set_deferred("monitoring", false)
	queue_free()
