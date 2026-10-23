class_name Interactable
extends Area2D

## Something in the level the hero can talk to or examine: an NPC, a relic
## or an invisible story trigger. Lists the dialogue ids to try, in priority
## order; main.gd picks the first whose flags match and plays it. Shows the
## interact key, or the pad button, above itself while the hero stands close. The node's origin
## is at the base, like the hero's, so a level cell places it on the floor.

## Fired when the hero presses interact in range (or, for a trigger, walks
## in). main.gd listens and runs the dialogue.
signal interacted(interactable: Interactable)

const PROMPT_FONT_SIZE := 8
const PROMPT_SHADOW := Color(0.0, 0.0, 0.0, 0.8)

## Dialogue ids to try, first match wins (see DialogueData.pick). These are
## the unique lines; mark them `once` in the dialogue file.
var dialogue_ids: Array = []
## Repeat lines, used when nothing in dialogue_ids applies any more. Each
## talk takes the next one, round robin.
var rotate_ids: Array = []
## The hero's half of the area, in level pixels.
var area_size := Vector2(28, 40)
## Where the key prompt floats, relative to the base.
var prompt_offset := Vector2(0, -48)
## Triggers have no prompt and fire on touch instead of on a key press.
var automatic := false

var _player_near := false
var _prompt: PromptLabel
var _rotation := 0


func _ready() -> void:
	collision_layer = 0
	collision_mask = 2
	monitorable = false
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = area_size
	shape.shape = rect
	shape.position = Vector2(0, -area_size.y / 2.0)
	add_child(shape)
	if not automatic:
		_prompt = PromptLabel.new()
		_prompt.template = "{interact}"
		_prompt.add_theme_font_size_override("normal_font_size", PROMPT_FONT_SIZE)
		_prompt.add_theme_color_override("font_shadow_color", PROMPT_SHADOW)
		_prompt.add_theme_constant_override("shadow_offset_x", 1)
		_prompt.add_theme_constant_override("shadow_offset_y", 1)
		_prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_prompt.size = Vector2(40, 12)
		_prompt.position = prompt_offset - Vector2(20, 6)
		_prompt.visible = false
		add_child(_prompt)
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)


func _unhandled_input(event: InputEvent) -> void:
	if automatic or not _player_near:
		return
	if event.is_action_pressed("interact"):
		# main.gd marks the event handled when it starts a dialogue; while
		# one is already open the press falls through to the box instead.
		interacted.emit(self)


func is_player_near() -> bool:
	return _player_near


## The repeat lines starting from the next one in turn, so the first that
## applies is the one to play. Advances the turn.
func rotation_ids() -> Array:
	var result: Array = []
	var count := rotate_ids.size()
	for i in count:
		result.append(rotate_ids[(_rotation + i) % count])
	if count > 0:
		_rotation = (_rotation + 1) % count
	return result


## Called by main.gd once the dialogue has been read to the end.
func on_dialogue_finished(_entry: Dictionary) -> void:
	pass


func _on_body_entered(body: Node2D) -> void:
	if not body is Player or body.state == Player.State.DEAD:
		return
	_player_near = true
	if automatic:
		interacted.emit(self)
	else:
		_prompt.visible = true


func _on_body_exited(body: Node2D) -> void:
	if body is Player:
		_player_near = false
		if _prompt:
			_prompt.visible = false
