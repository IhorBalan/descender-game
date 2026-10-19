class_name Hazard
extends Area2D

## Spikes. The hero loses health and is put back on the last safe ground.

@export var damage := 1


func _physics_process(_delta: float) -> void:
	for body in get_overlapping_bodies():
		if body is Player:
			body.take_hazard_damage(damage)
