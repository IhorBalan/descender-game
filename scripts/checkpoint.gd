class_name Checkpoint
extends Area2D

## Resting here refills health and healing and sets the respawn point.
## The hero rests automatically on touch. Standing at it and pressing the
## talk key makes camp: the pack, the skills and the save (see
## CheckpointMenu). The node's origin is at the base, like the hero's, so
## the respawn point puts his feet on the floor.

signal rested(checkpoint: Checkpoint)
## The hero pressed the talk key here; main.gd opens the camp.
signal camp_requested(checkpoint: Checkpoint)

const PROMPT_OFFSET := Vector2(0.0, -64.0)

## The flag prefix a rested checkpoint is kept under in the story flags,
## by its index in the level's list (the save's `checkpoint` is the same
## index), so fast travel knows where the hero has been.
const RESTED_FLAG_PREFIX := "rested:"

var active := false

var _player_near := false
var _prompt: Label

@onready var sprite: Sprite2D = $Sprite2D


## The story flag a checkpoint sets once the hero has rested at it.
static func rested_flag(index: int) -> String:
	return "%s%d" % [RESTED_FLAG_PREFIX, index]


func _ready() -> void:
	add_to_group("checkpoint")
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)
	_prompt = Label.new()
	_prompt.add_theme_font_size_override("font_size", Interactable.PROMPT_FONT_SIZE)
	_prompt.add_theme_color_override("font_color", Interactable.PROMPT_COLOR)
	_prompt.add_theme_color_override("font_shadow_color", Interactable.PROMPT_SHADOW)
	_prompt.add_theme_constant_override("shadow_offset_x", 1)
	_prompt.add_theme_constant_override("shadow_offset_y", 1)
	_prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_prompt.size = Vector2(60, 10)
	_prompt.position = PROMPT_OFFSET - Vector2(30, 5)
	_prompt.visible = false
	add_child(_prompt)


func _unhandled_input(event: InputEvent) -> void:
	if _player_near and event.is_action_pressed("interact"):
		camp_requested.emit(self)


func is_player_near() -> bool:
	return _player_near


func _on_body_entered(body: Node2D) -> void:
	if not body is Player:
		return
	# Near even while dead: a body that falls in here and respawns here
	# does not enter again, and the camp still has to open (main.gd keeps
	# a dead hero out of it).
	_player_near = true
	_prompt.text = "%s  camp" % _interact_key_name()
	_prompt.visible = true
	# A dead hero falling past, or a respawn landing on an active
	# checkpoint, is not a new rest.
	if not active and body.state != Player.State.DEAD:
		body.rest_at(global_position)
		Audio.play("checkpoint")
		for other in get_tree().get_nodes_in_group("checkpoint"):
			other.set_active(other == self)
		rested.emit(self)


func _on_body_exited(body: Node2D) -> void:
	if body is Player:
		_player_near = false
		_prompt.visible = false


func set_active(value: bool) -> void:
	active = value
	sprite.frame = 1 if active else 0


## The key bound to interact, from the Settings autoload; looked up by node
## because the headless test compiles this script before autoloads exist.
func _interact_key_name() -> String:
	var settings := get_node_or_null("/root/Settings")
	if settings == null:
		return "E"
	return settings.key_names(&"interact")
