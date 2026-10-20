class_name Checkpoint
extends Area2D

## Resting here refills health and healing and sets the respawn point.
## The hero rests automatically on touch. The node's origin is at the base,
## like the hero's, so the respawn point puts his feet on the floor.

signal rested(checkpoint: Checkpoint)

var active := false

@onready var sprite: Sprite2D = $Sprite2D


func _ready() -> void:
	add_to_group("checkpoint")
	body_entered.connect(_on_body_entered)


func _on_body_entered(body: Node2D) -> void:
	# A dead hero falling past, or a respawn landing on an active checkpoint,
	# is not a new rest.
	if body is Player and not active and body.state != Player.State.DEAD:
		body.rest_at(global_position)
		Audio.play("checkpoint")
		for other in get_tree().get_nodes_in_group("checkpoint"):
			other.set_active(other == self)
		rested.emit(self)


func set_active(value: bool) -> void:
	active = value
	sprite.frame = 1 if active else 0
