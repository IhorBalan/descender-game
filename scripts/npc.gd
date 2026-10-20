class_name Npc
extends Interactable

## Someone to talk to. Uses one of the character strips from
## assets/sprites with a tint, stands still on its idle animation and turns
## to face the hero when he comes close.

var npc_name := ""
var character := "hero"
var tint := Color.WHITE
## Whether the strips face left (the hero's do not, the guards' do).
var faces_left := false

var sprite: AnimatedSprite2D


func _ready() -> void:
	var spec := AnimLoader.spec(character)
	if not spec.is_empty():
		var frame_h := float(spec["frame_size"][1])
		area_size = Vector2(32, frame_h)
		prompt_offset = Vector2(0, -frame_h - 8.0)
	super()
	sprite = AnimatedSprite2D.new()
	AnimLoader.setup(sprite, character)
	sprite.modulate = tint
	sprite.play("idle")
	add_child(sprite)
	move_child(sprite, 0)


func _process(_delta: float) -> void:
	if not is_player_near():
		return
	for body in get_overlapping_bodies():
		if body is Player:
			var toward_left := body.global_position.x < global_position.x
			sprite.flip_h = toward_left != faces_left
			return
