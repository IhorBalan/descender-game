class_name Hazard
extends Area2D

## Spikes. The hero loses health and is put back on the last safe ground.

@export var damage := 1


func _physics_process(_delta: float) -> void:
	# Polled rather than body_entered so a hero standing on the spikes after
	# his invulnerability ends is hurt again. Cheap while nothing overlaps.
	if not has_overlapping_bodies():
		return
	for body in get_overlapping_bodies():
		if body is Player:
			body.take_hazard_damage(damage)
