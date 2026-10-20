class_name HitSpark
extends CPUParticles2D

## A one-shot burst of sparks that frees itself when the last one has faded.


func _ready() -> void:
	add_to_group("hit_spark")
	emitting = true
	get_tree().create_timer(lifetime + 0.05).timeout.connect(queue_free)
