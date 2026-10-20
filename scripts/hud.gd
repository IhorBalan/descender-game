class_name Hud
extends CanvasLayer

## Health, healing charges and short messages.

var _message_timer := 0.0
## Messages waiting for the current timed one to end: [text, duration].
var _queue: Array[Array] = []

@onready var status: StatusBar = $Status
@onready var message: Label = $Message


func _process(delta: float) -> void:
	if _message_timer > 0.0:
		_message_timer -= delta
		if _message_timer <= 0.0:
			message.visible = false
			if not _queue.is_empty():
				var next: Array = _queue.pop_front()
				show_message(next[0], next[1])


func set_health(current: int, max_value: int) -> void:
	status.health = current
	status.max_health = max_value
	status.queue_redraw()


func set_charges(current: int, max_value: int) -> void:
	status.charges = current
	status.max_charges = max_value
	status.queue_redraw()


## A negative duration keeps the message until the next one. Showing a
## message drops whatever was queued behind the previous one.
func show_message(text: String, duration: float = 2.5) -> void:
	_queue.clear()
	message.text = text
	message.visible = true
	_message_timer = duration if duration > 0.0 else 0.0


## Shows the message once the current timed one has run out, or at once
## when nothing timed is showing.
func queue_message(text: String, duration: float = 2.5) -> void:
	if _message_timer > 0.0:
		_queue.append([text, duration])
	else:
		show_message(text, duration)
