class_name Hazard
extends Area2D

## Spikes. The hero loses health and is put back on the last safe ground.

@export var damage := 1


func _ready() -> void:
	# Only poll while something is inside, so a level full of spikes costs
	# nothing per tick. Polling (rather than body_entered alone) hurts a hero
	# who is still on the spikes when his invulnerability ends.
	set_physics_process(false)
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)


func _on_body_entered(_body: Node2D) -> void:
	set_physics_process(true)


func _on_body_exited(_body: Node2D) -> void:
	if not has_overlapping_bodies():
		set_physics_process(false)


func _physics_process(_delta: float) -> void:
	for body in get_overlapping_bodies():
		if body is Player:
			body.take_hazard_damage(damage)
