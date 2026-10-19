class_name Hud
extends CanvasLayer

## Health, healing charges and short messages.

var _message_timer := 0.0

@onready var status: StatusBar = $Status
@onready var message: Label = $Message


func _process(delta: float) -> void:
	if _message_timer > 0.0:
		_message_timer -= delta
		if _message_timer <= 0.0:
			message.visible = false


func set_health(current: int, max_value: int) -> void:
	status.health = current
	status.max_health = max_value
	status.queue_redraw()


func set_charges(current: int, max_value: int) -> void:
	status.charges = current
	status.max_charges = max_value
	status.queue_redraw()


## A negative duration keeps the message until the next one.
func show_message(text: String, duration: float = 2.5) -> void:
	message.text = text
	message.visible = true
	_message_timer = duration if duration > 0.0 else 0.0
