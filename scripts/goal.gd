class_name Goal
extends Area2D

## The gate at the end of the level.

signal reached

var _done := false


func _ready() -> void:
	body_entered.connect(_on_body_entered)


func _on_body_entered(body: Node2D) -> void:
	if body is Player and not _done and body.state != Player.State.DEAD:
		_done = true
		# The hero freezes in response, which touches his attack shape, and
		# physics shapes cannot change while the physics server is reporting
		# overlaps; so the signal waits for the step to end.
		_emit_reached.call_deferred()


func _emit_reached() -> void:
	reached.emit()
